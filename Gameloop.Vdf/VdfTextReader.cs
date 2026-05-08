using System.Buffers;

namespace Gameloop.Vdf;

public class VdfTextReader(TextReader reader, VdfSerializerSettings settings) : VdfReader(settings)
{
    private const int DefaultBufferSize = 1024;

    private static readonly SearchValues<char> Delimiters = SearchValues.Create(
        [VdfStructure.Quote, VdfStructure.ObjectStart, VdfStructure.ObjectEnd,
         VdfStructure.ArrayStart, VdfStructure.ArrayEnd,
         VdfStructure.Comment, VdfStructure.ConditionalStart, VdfStructure.Escape]);

    private readonly TextReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly char[] _charBuffer = new char[DefaultBufferSize];
    private readonly char[] _tokenBuffer = new char[settings.MaximumTokenSize];
    private int _charPos, _charsLen, _tokenSize;
    private bool _isQuoted, _isComment, _isConditional;

    public VdfTextReader(TextReader reader) : this(reader, VdfSerializerSettings.Default) { }

    public override bool ReadToken()
    {
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

            if (!_isQuoted && _tokenSize == 0 && curChar == VdfStructure.Comment && buffer.Length > 1 && buffer[1] == VdfStructure.Comment)
            {
                _isComment = true;
                _charPos += 2;
                continue;
            }
            #endregion

            #region Escape Handling
            if (curChar == VdfStructure.Escape)
            {
                _charPos++;
                if (!EnsureBuffer()) throw new VdfException("Incomplete escape sequence at end of file.");

                CheckBuffer();
                _tokenBuffer[_tokenSize++] = !Settings.UsesEscapeSequences ? curChar : _charBuffer[_charPos].FromVdfEscape();
                _charPos++;
                continue;
            }
            #endregion

            #region Termination
            if (curChar == VdfStructure.Quote || (!_isQuoted && char.IsWhiteSpace(curChar)))
            {
                Value = new string(_tokenBuffer, 0, _tokenSize);
                CurrentState = VdfState.Property;
                if (curChar == VdfStructure.Quote) _charPos++;
                return true;
            }
            #endregion

            #region Conditional Logic
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

            #region Vectorized Bulk Fast-Forward
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

            CheckBuffer();
            _tokenBuffer[_tokenSize++] = curChar;
            _charPos++;
        }
        CurrentState = VdfState.Finished;
        return false;
    }

    private bool HandleConditional(char curChar, ReadOnlySpan<char> buffer)
    {
        if (_tokenSize > 0 && (char.IsWhiteSpace(curChar) || curChar is VdfStructure.ConditionalOr
            or VdfStructure.ConditionalAnd or VdfStructure.ConditionalEnd))
        {
            string val = new(_tokenBuffer, 0, _tokenSize);
            Value = val.StartsWith(VdfStructure.ConditionalConstant) ? val[1..] : val;
            CurrentState = VdfState.Conditional;
            return true;
        }

        if (curChar is VdfStructure.ConditionalOr or VdfStructure.ConditionalAnd)
        {
            Value = buffer[..2].ToString();
            CurrentState = VdfState.Conditional;
            _charPos += 2;
            return true;
        }

        if (curChar is VdfStructure.ConditionalStart or VdfStructure.ConditionalEnd or VdfStructure.ConditionalNot)
        {
            Value = curChar.ToString();
            CurrentState = VdfState.Conditional;
            _isConditional = curChar != VdfStructure.ConditionalEnd;
            _charPos++;
            return true;
        }

        return false;
    }

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

            _isQuoted = (cur == VdfStructure.Quote);
            if (_isQuoted) _charPos++;
            return true;
        }
        return false;
    }

    private bool EnsureBuffer()
    {
        if (_charPos < _charsLen) return true;

        int remaining = _charsLen - _charPos;
        if (remaining > 0) _charBuffer[0] = _charBuffer[_charPos];

        _charsLen = _reader.Read(_charBuffer, remaining, DefaultBufferSize - remaining) + remaining;
        _charPos = 0;
        return _charsLen > 0;
    }

    public override void Close()
    {
        base.Close();
        if (CloseInput) _reader.Dispose();
    }
    private void CheckBuffer(int count = 1)
    {
        if (_tokenSize + count > _tokenBuffer.Length)
            throw new VdfException($"Token size exceeded the maximum limit of {Settings.MaximumTokenSize} characters.");
    }

    public override async Task<bool> ReadTokenAsync()
    {
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

            if (!_isQuoted && _tokenSize == 0 && curChar == VdfStructure.Comment && buffer.Length > 1 && buffer[1] == VdfStructure.Comment)
            {
                _isComment = true;
                _charPos += 2;
                continue;
            }
            #endregion

            #region Escape Handling
            if (curChar == VdfStructure.Escape)
            {
                _charPos++;
                if (!await EnsureBufferAsync()) throw new VdfException("Incomplete escape sequence.");

                CheckBuffer();
                _tokenBuffer[_tokenSize++] = !Settings.UsesEscapeSequences ? curChar : _charBuffer[_charPos].FromVdfEscape();
                _charPos++;
                continue;
            }
            #endregion

            #region Conditional Logic
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
            if (curChar == VdfStructure.Quote || (!_isQuoted && char.IsWhiteSpace(curChar)))
            {
                Value = new string(_tokenBuffer, 0, _tokenSize);
                CurrentState = VdfState.Property;
                if (curChar == VdfStructure.Quote) _charPos++;
                return true;
            }
            #endregion

            #region Vectorized Bulk Fast-Forward
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

            CheckBuffer();
            _tokenBuffer[_tokenSize++] = curChar;
            _charPos++;
        }
        CurrentState = VdfState.Finished;
        return false;
    }


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

            _isQuoted = (cur == VdfStructure.Quote);
            if (_isQuoted) _charPos++;
            return true;
        }
        return false;
    }

    private async Task<bool> EnsureBufferAsync()
    {
        if (_charPos < _charsLen) return true;

        int remaining = _charsLen - _charPos;
        if (remaining > 0) _charBuffer[0] = _charBuffer[_charPos];

        _charsLen = await _reader.ReadAsync(_charBuffer.AsMemory(remaining, DefaultBufferSize - remaining)) + remaining;
        _charPos = 0;
        return _charsLen > 0;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (CloseInput) _reader.Dispose();
        await base.DisposeAsyncCore();
    }
}
