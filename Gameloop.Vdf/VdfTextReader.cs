using System.Buffers;

namespace Gameloop.Vdf;

/// <summary>
/// Initializes a new instance of the <see cref="VdfTextReader"/> class with a specified <see cref="TextReader"/> and <see cref="VdfSerializerSettings"/>.
/// </summary>
/// <param name="reader">The <see cref="TextReader"/> from which the VDF data will be read.</param>
/// <param name="settings">The <see cref="VdfSerializerSettings"/> used to configure the reader's behavior.</param>

public class VdfTextReader(TextReader reader, VdfSerializerSettings settings) : VdfReader(settings)
{
    /// <summary>
    /// The default size for the internal character buffer used to batch reads from the <see cref="TextReader"/>.
    /// </summary>
    private const int DefaultBufferSize = 1024;

    /// <summary>
    /// An optimized lookup table used to quickly identify structural VDF characters, 
    /// delimiters, and escape symbols during the tokenization process.
    /// </summary>
    private static readonly SearchValues<char> Delimiters = SearchValues.Create(
        [VdfStructure.Quote, VdfStructure.ObjectStart, VdfStructure.ObjectEnd,
         VdfStructure.ArrayStart, VdfStructure.ArrayEnd,
         VdfStructure.Comment, VdfStructure.ConditionalStart, VdfStructure.Escape]);

    /// <summary>
    /// The underlying <see cref="TextReader"/> from which the VDF text data is read.
    /// </summary>
    /// <remarks>
    /// This field is initialized from the primary constructor. If <see cref="VdfReader.CloseInput"/> 
    /// is <c>true</c>, this reader will be disposed when the <see cref="VdfTextReader"/> is closed.
    /// </remarks>
    private readonly TextReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <summary>
    /// An internal buffer used to store a chunk of characters read from the underlying <see cref="TextReader"/> 
    /// to minimize I/O overhead.
    /// </summary>
    private readonly char[] _charBuffer = new char[DefaultBufferSize];

    /// <summary>
    /// An internal buffer used to accumulate characters for the current VDF token as it is being parsed. 
    /// Its size is determined by the <see cref="VdfSerializerSettings.MaximumTokenSize"/> setting.
    /// </summary>
    private readonly char[] _tokenBuffer = new char[settings.MaximumTokenSize];

    /// <summary>
    /// Tracking variables for the parser's state:
    /// <list type="bullet">
    /// <item><description><c>_charPos</c>: The current read position within <see cref="_charBuffer"/>.</description></item>
    /// <item><description><c>_charsLen</c>: The total number of valid characters currently held in <see cref="_charBuffer"/>.</description></item>
    /// <item><description><c>_tokenSize</c>: The number of characters currently stored in <see cref="_tokenBuffer"/> for the active token.</description></item>
    /// </list>
    /// </summary>
    private int _charPos, _charsLen, _tokenSize;

    /// <summary>
    /// State flags used to track the parser's context within the current line or token:
    /// <list type="bullet">
    /// <item><description><c>_isQuoted</c>: Indicates if the parser is currently inside a double-quoted string.</description></item>
    /// <item><description><c>_isComment</c>: Indicates if the parser is currently reading a line comment (//).</description></item>
    /// <item><description><c>_isConditional</c>: Indicates if the parser is currently inside a platform conditional block ([$...]).</description></item>
    /// </list>
    /// </summary>
    private bool _isQuoted, _isComment, _isConditional;

    /// <summary>
    /// Initializes a new instance of the <see cref="VdfTextReader"/> class using the specified <see cref="TextReader"/> and default settings.
    /// </summary>
    /// <param name="reader">The <see cref="TextReader"/> containing the VDF data to read.</param>
    public VdfTextReader(TextReader reader) : this(reader, VdfSerializerSettings.Default) { }

    public override bool ReadToken()
    {
        // Skip whitespace and determine if another token is available.
        if (!SeekToken())
        {
            CurrentState = VdfState.Finished;
            return false;
        }

        _tokenSize = 0;
        while (EnsureBuffer())
        {
            ReadOnlySpan<char> buffer = _charBuffer.AsSpan(_charPos, _charsLen - _charPos);
            char curChar = buffer[0];

            #region Comment Handling
            // If currently parsing a comment, accumulate characters until a line break is found.
            if (_isComment)
            {
                if (curChar is VdfStructure.CarriageReturn or VdfStructure.NewLine)
                {
                    _isComment = false;
                    Value = new string(_tokenBuffer, 0, _tokenSize);
                    CurrentState = VdfState.Comment;
                    return true;
                }
                CheckBuffer();
                _tokenBuffer[_tokenSize++] = curChar;
                _charPos++;
                continue;
            }

            // Detect the start of a line comment (//) when not inside a quoted string.
            if (!_isQuoted && _tokenSize == 0 && curChar == VdfStructure.Comment && buffer.Length > 1 && buffer[1] == VdfStructure.Comment)
            {
                _isComment = true;
                _charPos += 2;
                continue;
            }
            #endregion

            #region Escape Handling
            // Processes backslash escape sequences (e.g., \n, \t).
            if (curChar == VdfStructure.Escape)
            {
                _charPos++;
                if (!EnsureBuffer()) throw new VdfException("Incomplete escape sequence at end of file.");

                CheckBuffer();
                // If escape sequences are disabled in settings, treat the backslash as a literal.
                // Otherwise, convert the following character (e.g., 'n') to its control character (\n).
                _tokenBuffer[_tokenSize++] = !Settings.UsesEscapeSequences ? curChar : _charBuffer[_charPos].FromVdfEscape();
                _charPos++;
                continue;
            }
            #endregion

            #region Termination
            // A token ends if we hit a closing quote (in a quoted string) 
            // or whitespace (in an unquoted string).
            if (curChar == VdfStructure.Quote || (!_isQuoted && char.IsWhiteSpace(curChar)))
            {
                Value = new string(_tokenBuffer, 0, _tokenSize);
                CurrentState = VdfState.Property;
                if (curChar == VdfStructure.Quote) _charPos++;
                return true;
            }
            #endregion

            #region Conditional Logic
            // Handles platform conditionals like [$WIN32].
            bool isConditionalStart = !_isQuoted && curChar == VdfStructure.ConditionalStart
                                  && buffer.Length > 1 && buffer[1] == VdfStructure.ConditionalConstant && _tokenSize == 0;
            if (_isConditional || isConditionalStart)
            {
                if (isConditionalStart && !_isConditional)
                {
                    _isConditional = true;
                    Value = curChar.ToString();
                    CurrentState = VdfState.Conditional;
                    _charPos++;
                    return true;
                }

                // Skip the '$' prefix commonly found in VDF conditionals.
                if (curChar == VdfStructure.ConditionalConstant)
                {
                    _charPos++;
                    continue;
                }

                // Attempt to finalize the conditional token (e.g., reaching ']').
                if (HandleConditional(curChar, buffer))
                {
                    CurrentState = VdfState.Conditional;
                    return true;
                }

                CheckBuffer();
                _tokenBuffer[_tokenSize++] = curChar;
                _charPos++;
                continue;
            }

            #endregion

            #region Structural Elements
            // Handles braces {} for objects and brackets [] for arrays.
            if (curChar is VdfStructure.ObjectStart or VdfStructure.ObjectEnd or VdfStructure.ArrayStart or VdfStructure.ArrayEnd)
            {
                if (_isQuoted)
                {
                    // If inside quotes, these characters are just part of the string.
                    CheckBuffer();
                    _tokenBuffer[_tokenSize++] = curChar;
                    _charPos++;
                }
                else if (_tokenSize != 0)
                {
                    // If we hit a structural character but have a pending token name, 
                    // return the name first.
                    Value = new string(_tokenBuffer, 0, _tokenSize);
                    CurrentState = VdfState.Property;
                    return true;
                }
                else
                {
                    // Finalize the structural token and set the appropriate state.
                    Value = curChar.ToString();
                    CurrentState = curChar switch
                    {
                        VdfStructure.ObjectStart or VdfStructure.ObjectEnd => VdfState.Object,
                        VdfStructure.ArrayStart => VdfState.ArrayStart,
                        VdfStructure.ArrayEnd => VdfState.ArrayEnd,
                        _ => VdfState.Property
                    };
                    _charPos++;
                    return true;
                }
                continue;
            }
            #endregion

            #region Vectorized Bulk Fast-Forward
            // Optimization: Use SIMD-accelerated SearchValues to skip characters 
            // until the next structural delimiter or escape character is found.
            int nextDelim = buffer.IndexOfAny(Delimiters);
            if (nextDelim > 0 && !_isComment && !_isConditional)
            {
                int available = _tokenBuffer.Length - _tokenSize;
                int copyLen = Math.Min(nextDelim, available);

                if (nextDelim > available)
                    throw new VdfException($"Token size exceeded the maximum limit.");

                // Bulk copy characters from the read buffer to the token buffer.
                buffer[..copyLen].CopyTo(_tokenBuffer.AsSpan(_tokenSize));
                _tokenSize += copyLen;
                _charPos += copyLen;
                continue;
            }
            #endregion

            // Fallback for single-character processing.
            CheckBuffer();
            _tokenBuffer[_tokenSize++] = curChar;
            _charPos++;
        }
        CurrentState = VdfState.Finished;
        return false;
    }

    /// <summary>
    /// Processes characters within a conditional block (e.g., [$WIN32]), identifying constants, operators, and terminators.
    /// </summary>
    /// <param name="curChar">The current character being inspected.</param>
    /// <param name="buffer">The current span of the read buffer for multi-character operator lookahead.</param>
    /// <returns><c>true</c> if a complete conditional token was identified and set; otherwise, <c>false</c>.</returns>
    private bool HandleConditional(char curChar, ReadOnlySpan<char> buffer)
    {
        // Finalize a constant name if we hit whitespace or an operator
        if (_tokenSize > 0 && (char.IsWhiteSpace(curChar) || curChar is VdfStructure.ConditionalOr
            or VdfStructure.ConditionalAnd or VdfStructure.ConditionalEnd))
        {
            string val = new(_tokenBuffer, 0, _tokenSize);
            // Strip the optional '$' prefix from constants
            Value = val.StartsWith(VdfStructure.ConditionalConstant) ? val[1..] : val;
            CurrentState = VdfState.Conditional;
            return true;
        }

        // Handle multi-character logical operators (|| and &&)
        if (curChar is VdfStructure.ConditionalOr or VdfStructure.ConditionalAnd)
        {
            Value = buffer[..2].ToString();
            CurrentState = VdfState.Conditional;
            _charPos += 2;
            return true;
        }

        // Handle single-character structural elements ([, ], !)
        if (curChar is VdfStructure.ConditionalStart or VdfStructure.ConditionalEnd or VdfStructure.ConditionalNot)
        {
            Value = curChar.ToString();
            CurrentState = VdfState.Conditional;
            // Exit conditional state once we hit the closing bracket
            _isConditional = curChar != VdfStructure.ConditionalEnd;
            _charPos++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Advances the reader position to the start of the next non-whitespace token.
    /// </summary>
    /// <returns><c>true</c> if a token start was found; <c>false</c> if the end of the stream was reached.</returns>
    private bool SeekToken()
    {
        while (EnsureBuffer())
        {
            char cur = _charBuffer[_charPos];
            if (char.IsWhiteSpace(cur))
            {
                _charPos++;
                continue;
            }

            // Determine if the upcoming token is wrapped in quotes
            _isQuoted = (cur == VdfStructure.Quote);
            if (_isQuoted) _charPos++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ensures that the internal character buffer contains data, performing a read from the source if necessary.
    /// </summary>
    /// <returns><c>true</c> if data is available in the buffer; otherwise, <c>false</c>.</returns>
    private bool EnsureBuffer()
    {
        if (_charPos < _charsLen) return true;

        int remaining = _charsLen - _charPos;
        if (remaining > 0) _charBuffer[0] = _charBuffer[_charPos];

        _charsLen = _reader.Read(_charBuffer, remaining, DefaultBufferSize - remaining) + remaining;
        _charPos = 0;
        return _charsLen > 0;
    }

    /// <inheritdoc />
    public override void Close()
    {
        base.Close();
        if (CloseInput) _reader.Dispose();
    }

    /// <summary>
    /// Validates that the token buffer has enough remaining capacity to store additional characters.
    /// </summary>
    /// <param name="count">The number of characters intended to be added.</param>
    /// <exception cref="VdfException">Thrown if the addition would exceed <see cref="VdfSerializerSettings.MaximumTokenSize"/>.</exception>
    private void CheckBuffer(int count = 1)
    {
        if (_tokenSize + count > _tokenBuffer.Length)
            throw new VdfException($"Token size exceeded the maximum limit of {Settings.MaximumTokenSize} characters.");
    }

    /// <summary>
    /// Asynchronously reads the next VDF token from the source and updates <see cref="VdfReader.CurrentState"/>.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous read operation, containing <c>true</c> if a token 
    /// was successfully read; <c>false</c> if the end of the stream was reached.
    /// </returns>
    /// <exception cref="VdfException">Thrown if an incomplete escape sequence or invalid token size is encountered.</exception>
    public override async Task<bool> ReadTokenAsync()
    {
        // Asynchronously skip whitespace to find the start of the next token.
        if (!await SeekTokenAsync())
        {
            CurrentState = VdfState.Finished;
            return false;
        }

        _tokenSize = 0;
        while (await EnsureBufferAsync())
        {
            ReadOnlySpan<char> buffer = _charBuffer.AsSpan(_charPos, _charsLen - _charPos);
            char curChar = buffer[0];

            #region Comment Handling
            // Accumulate characters until a newline is reached if the parser is in a comment state.
            if (_isComment)
            {
                if (curChar is VdfStructure.CarriageReturn or VdfStructure.NewLine)
                {
                    _isComment = false;
                    Value = new string(_tokenBuffer, 0, _tokenSize);
                    CurrentState = VdfState.Comment;
                    return true;
                }
                CheckBuffer();
                _tokenBuffer[_tokenSize++] = curChar;
                _charPos++;
                continue;
            }

            // Detect the start of a double-slash comment.
            if (!_isQuoted && _tokenSize == 0 && curChar == VdfStructure.Comment && buffer.Length > 1 && buffer[1] == VdfStructure.Comment)
            {
                _isComment = true;
                _charPos += 2;
                continue;
            }
            #endregion

            #region Escape Handling
            // Handles backslash escape sequences asynchronously if the buffer needs refilling.
            if (curChar == VdfStructure.Escape)
            {
                _charPos++;
                if (!await EnsureBufferAsync()) throw new VdfException("Incomplete escape sequence.");

                CheckBuffer();
                // Decodes the escape character (e.g., 'n' to '\n') unless escape sequences are disabled.
                _tokenBuffer[_tokenSize++] = !Settings.UsesEscapeSequences ? curChar : _charBuffer[_charPos].FromVdfEscape();
                _charPos++;
                continue;
            }
            #endregion

            #region Conditional Logic
            // Handles Valve platform conditionals like [$WIN32].
            bool isConditionalStart = !_isQuoted && curChar == VdfStructure.ConditionalStart
                                  && buffer.Length > 1 && buffer[1] == VdfStructure.ConditionalConstant && _tokenSize == 0;
            if (_isConditional || isConditionalStart)
            {
                if (isConditionalStart && !_isConditional)
                {
                    _isConditional = true;
                    Value = curChar.ToString();
                    CurrentState = VdfState.Conditional;
                    _charPos++;
                    return true;
                }
                if (curChar == VdfStructure.ConditionalConstant)
                {
                    _charPos++;
                    continue;
                }

                if (HandleConditional(curChar, buffer))
                {
                    CurrentState = VdfState.Conditional;
                    return true;
                }

                CheckBuffer();
                _tokenBuffer[_tokenSize++] = curChar;
                _charPos++;
                continue;
            }

            #endregion

            #region Structural Elements
            // Processes structural delimiters: braces for objects and brackets for arrays.
            if (curChar is VdfStructure.ObjectStart or VdfStructure.ObjectEnd or VdfStructure.ArrayStart or VdfStructure.ArrayEnd)
            {
                if (_isQuoted)
                {
                    CheckBuffer();
                    _tokenBuffer[_tokenSize++] = curChar;
                    _charPos++;
                }
                else if (_tokenSize != 0)
                {
                    // Return the accumulated property name before returning the structural character.
                    Value = new string(_tokenBuffer, 0, _tokenSize);
                    CurrentState = VdfState.Property;
                    return true;
                }
                else
                {
                    Value = curChar.ToString();
                    CurrentState = curChar switch
                    {
                        VdfStructure.ObjectStart or VdfStructure.ObjectEnd => VdfState.Object,
                        VdfStructure.ArrayStart => VdfState.ArrayStart,
                        VdfStructure.ArrayEnd => VdfState.ArrayEnd,
                        _ => VdfState.Property
                    };
                    _charPos++;
                    return true;
                }
                continue;
            }
            #endregion

            #region Termination
            // A token is terminated by a closing quote or whitespace (if unquoted).
            if (curChar == VdfStructure.Quote || (!_isQuoted && char.IsWhiteSpace(curChar)))
            {
                Value = new string(_tokenBuffer, 0, _tokenSize);
                CurrentState = VdfState.Property;
                if (curChar == VdfStructure.Quote) _charPos++;
                return true;
            }
            #endregion

            #region Vectorized Bulk Fast-Forward
            // Performance optimization: Bulk copy characters until the next delimiter is found.
            int nextDelim = buffer.IndexOfAny(Delimiters);
            if (nextDelim > 0 && !_isComment && !_isConditional)
            {
                int available = _tokenBuffer.Length - _tokenSize;
                int copyLen = Math.Min(nextDelim, available);

                if (nextDelim > available)
                    throw new VdfException($"Token size exceeded the maximum limit.");

                buffer[..copyLen].CopyTo(_tokenBuffer.AsSpan(_tokenSize));
                _tokenSize += copyLen;
                _charPos += copyLen;
                continue;
            }
            #endregion

            // Fallback for character-by-character accumulation.
            CheckBuffer();
            _tokenBuffer[_tokenSize++] = curChar;
            _charPos++;
        }
        CurrentState = VdfState.Finished;
        return false;
    }

    /// <summary>
    /// Asynchronously advances the reader position to the start of the next non-whitespace token.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation, containing <c>true</c> if a token 
    /// start was found; <c>false</c> if the end of the stream was reached.
    /// </returns>
    private async Task<bool> SeekTokenAsync()
    {
        while (await EnsureBufferAsync())
        {
            char cur = _charBuffer[_charPos];
            if (char.IsWhiteSpace(cur))
            {
                _charPos++;
                continue;
            }

            // Determine if the upcoming token is wrapped in quotes
            _isQuoted = (cur == VdfStructure.Quote);
            if (_isQuoted) _charPos++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Asynchronously ensures that the internal character buffer contains data, performing a read from the source if necessary.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation, containing <c>true</c> if data 
    /// is available in the buffer; otherwise, <c>false</c>.
    /// </returns>
    private async Task<bool> EnsureBufferAsync()
    {
        if (_charPos < _charsLen) return true;

        int remaining = _charsLen - _charPos;
        if (remaining > 0) _charBuffer[0] = _charBuffer[_charPos];

        _charsLen = await _reader.ReadAsync(_charBuffer.AsMemory(remaining, DefaultBufferSize - remaining)) + remaining;
        _charPos = 0;
        return _charsLen > 0;
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        if (CloseInput) _reader.Dispose();
        await base.DisposeAsyncCore();
    }
}
