using System.Buffers;
using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf;

/// <summary>
/// Initializes a new instance of the <see cref="VdfTextWriter"/> class with a specified <see cref="TextWriter"/> and <see cref="VdfSerializerSettings"/>.
/// </summary>
/// <param name="writer">The <see cref="TextWriter"/> to which the VDF data will be written.</param>
/// <param name="settings">The <see cref="VdfSerializerSettings"/> used to configure the writer's behavior.</param>
public class VdfTextWriter(TextWriter writer, VdfSerializerSettings settings) : VdfWriter(settings)
{
    /// <summary>
    /// Optimized search values for identifying characters that require VDF escape sequences.
    /// </summary>
    private static readonly SearchValues<char> EscapableChars = SearchValues.Create(
        ['\n', '\t', '\v', '\b', '\r', '\f', '\a', '\\', '?', '\'', '\"']);

    /// <summary>
    /// The underlying <see cref="TextWriter"/> used to output the VDF data.
    /// </summary>
    /// <remarks>
    /// This field is initialized from the primary constructor and its disposal 
    /// is managed based on the <see cref="VdfWriter.CloseOutput"/> setting.
    /// </remarks>
    private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>
    /// Gets or sets the current indentation depth used for pretty-printing the VDF output.
    /// </summary>
    private int IndentationLevel { get; set; } = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="VdfTextWriter"/> class using default settings.
    /// </summary>
    /// <param name="writer">The <see cref="TextWriter"/> to write to.</param>
    public VdfTextWriter(TextWriter writer) : this(writer, VdfSerializerSettings.Default) { }

    /// <inheritdoc />
    public override void WriteKey(string key)
    {
        AutoComplete(State.Key);
        _writer.Write(VdfStructure.Quote);
        WriteEscapedString(key);
        _writer.Write(VdfStructure.Quote);
    }

    /// <inheritdoc />
    public override void WriteValue(VValue value) => WriteValue(value.ToString(), value.TypeHint);

    /// <inheritdoc />
    public override void WriteValue(string value, string? typeHint = null)
    {
        AutoComplete(State.Value);

        // KV3 supports type hinting (e.g., boolean:"1")
        if (Settings.Format == KeyValuesFormat.Kv3 && !string.IsNullOrEmpty(typeHint))
        {
            _writer.Write(typeHint);
            _writer.Write(':');
        }

        _writer.Write(VdfStructure.Quote);
        WriteEscapedString(value);
        _writer.Write(VdfStructure.Quote);
    }

    /// <inheritdoc />
    public override void WriteArrayStart()
    {
        AutoComplete(State.ArrayStart);
        _writer.Write(VdfStructure.ArrayStart);
        IndentationLevel++;
    }

    /// <inheritdoc />
    public override void WriteArrayEnd()
    {
        IndentationLevel--;
        AutoComplete(State.ArrayEnd);
        _writer.Write(VdfStructure.ArrayEnd);
    }

    /// <inheritdoc />
    public override void WriteObjectStart()
    {
        AutoComplete(State.ObjectStart);
        _writer.Write(VdfStructure.ObjectStart);
        IndentationLevel++;
    }

    /// <inheritdoc />
    public override void WriteObjectEnd()
    {
        IndentationLevel--;
        AutoComplete(State.ObjectEnd);
        _writer.Write(VdfStructure.ObjectEnd);

        if (IndentationLevel == 0)
            AutoComplete(State.Finished);
    }

    /// <inheritdoc />
    public override void WriteComment(string text)
    {
        AutoComplete(State.Comment);
        _writer.Write(VdfStructure.Comment);
        _writer.Write(VdfStructure.Comment);
        _writer.Write(text);
    }

    /// <inheritdoc />
    public override void WriteConditional(IReadOnlyList<VConditional.Token> tokens)
    {
        AutoComplete(State.Conditional);
        _writer.Write(VdfStructure.ConditionalStart);

        foreach (var token in tokens)
        {
            switch (token.TokenType)
            {
                case VConditional.TokenType.Constant:
                    _writer.Write(VdfStructure.ConditionalConstant);
                    _writer.Write(token.Name);
                    break;
                case VConditional.TokenType.Not:
                    _writer.Write(VdfStructure.ConditionalNot);
                    break;
                case VConditional.TokenType.Or:
                    _writer.Write(VdfStructure.ConditionalOr);
                    _writer.Write(VdfStructure.ConditionalOr);
                    break;
                case VConditional.TokenType.And:
                    _writer.Write(VdfStructure.ConditionalAnd);
                    _writer.Write(VdfStructure.ConditionalAnd);
                    break;
            }
        }

        _writer.Write(VdfStructure.ConditionalEnd);
    }


    /// <summary>
    /// Automatically manages transitions between states, handling whitespace, indentation, and newlines.
    /// </summary>
    /// <param name="next">The state to transition to.</param>
    private void AutoComplete(State next)
    {
        if (CurrentState == State.Start)
        {
            CurrentState = next;
            return;
        }

        switch (next)
        {
            case State.Value or State.Conditional:
                // Separate keys from values/conditionals with a space
                _writer.Write(VdfStructure.Assign);
                break;

            case State.Key or State.ObjectStart or State.ObjectEnd or
                 State.ArrayStart or State.ArrayEnd or State.Comment:
                // New lines for structural elements with current indentation
                _writer.WriteLine();
                _writer.Write(new string(VdfStructure.Indent, IndentationLevel));
                break;

            case State.Finished:
                _writer.WriteLine();
                break;
        }

        CurrentState = next;
    }

    /// <summary>
    /// Writes a string to the output, escaping special characters based on <see cref="VdfSerializerSettings.UsesEscapeSequences"/>.
    /// </summary>
    /// <param name="str">The string to escape and write.</param>
    private void WriteEscapedString(string str)
    {
        if (!Settings.UsesEscapeSequences)
        {
            _writer.Write(str);
            return;
        }

        ReadOnlySpan<char> span = str.AsSpan();
        while (!span.IsEmpty)
        {
            int next = span.IndexOfAny(EscapableChars);
            if (next == -1)
            {
                _writer.Write(span);
                break;
            }

            if (next > 0) _writer.Write(span[..next]);

            _writer.Write(VdfStructure.Escape);
            _writer.Write(span[next].ToVdfEscape());
            span = span[(next + 1)..];
        }
    }

    /// <inheritdoc />
    public override void Close()
    {
        base.Close();
        if (CloseOutput) _writer.Dispose();
    }

    /// <inheritdoc />
    public override async Task WriteKeyAsync(string key)
    {
        await AutoCompleteAsync(State.Key);
        await _writer.WriteAsync(VdfStructure.Quote);
        await WriteEscapedStringAsync(key);
        await _writer.WriteAsync(VdfStructure.Quote);
    }

    /// <inheritdoc />
    public override async Task WriteValueAsync(string value, string? typeHint = null)
    {
        await AutoCompleteAsync(State.Value);

        if (Settings.Format == KeyValuesFormat.Kv3 && !string.IsNullOrEmpty(typeHint))
        {
            await _writer.WriteAsync(typeHint);
            await _writer.WriteAsync(':');
        }

        await _writer.WriteAsync(VdfStructure.Quote);
        await WriteEscapedStringAsync(value);
        await _writer.WriteAsync(VdfStructure.Quote);
    }

    /// <inheritdoc />
    public override async Task WriteObjectStartAsync()
    {
        await AutoCompleteAsync(State.ObjectStart);
        await _writer.WriteAsync(VdfStructure.ObjectStart);
        IndentationLevel++;
    }

    /// <inheritdoc />
    public override async Task WriteObjectEndAsync()
    {
        IndentationLevel--;
        await AutoCompleteAsync(State.ObjectEnd);
        await _writer.WriteAsync(VdfStructure.ObjectEnd);

        if (IndentationLevel == 0)
            await AutoCompleteAsync(State.Finished);
    }

    /// <inheritdoc />
    public override async Task WriteCommentAsync(string text)
    {
        await AutoCompleteAsync(State.Comment);
        await _writer.WriteAsync(VdfStructure.Comment);
        await _writer.WriteAsync(VdfStructure.Comment);
        await _writer.WriteAsync(text);
    }

    /// <inheritdoc />
    public override async Task WriteArrayStartAsync()
    {
        await AutoCompleteAsync(State.ArrayStart);
        await _writer.WriteAsync(VdfStructure.ArrayStart);
        IndentationLevel++;
    }

    /// <inheritdoc />
    public override async Task WriteArrayEndAsync()
    {
        IndentationLevel--;
        await AutoCompleteAsync(State.ArrayEnd);
        await _writer.WriteAsync(VdfStructure.ArrayEnd);
    }

    /// <inheritdoc />
    public override async Task WriteConditionalAsync(IReadOnlyList<VConditional.Token> tokens)
    {
        await AutoCompleteAsync(State.Conditional);
        await _writer.WriteAsync(VdfStructure.ConditionalStart);

        foreach (var token in tokens)
        {
            switch (token.TokenType)
            {
                case VConditional.TokenType.Constant:
                    await _writer.WriteAsync(VdfStructure.ConditionalConstant);
                    await _writer.WriteAsync(token.Name);
                    break;
                case VConditional.TokenType.Not:
                    await _writer.WriteAsync(VdfStructure.ConditionalNot);
                    break;
                case VConditional.TokenType.Or:
                    await _writer.WriteAsync($"{VdfStructure.ConditionalOr}{VdfStructure.ConditionalOr}");
                    break;
                case VConditional.TokenType.And:
                    await _writer.WriteAsync($"{VdfStructure.ConditionalAnd}{VdfStructure.ConditionalAnd}");
                    break;
            }
        }

        await _writer.WriteAsync(VdfStructure.ConditionalEnd);
    }

    /// <summary>
    /// Asynchronously manages transitions between states, handling whitespace, indentation, and newlines.
    /// </summary>
    /// <param name="next">The state to transition to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task AutoCompleteAsync(State next)
    {
        if (CurrentState == State.Start)
        {
            CurrentState = next;
            return;
        }

        switch (next)
        {
            case State.Value or State.Conditional:
                await _writer.WriteAsync(VdfStructure.Assign);
                break;

            case State.Key or State.ObjectStart or State.ObjectEnd or
                 State.ArrayStart or State.ArrayEnd or State.Comment:
                await _writer.WriteLineAsync();
                await _writer.WriteAsync(new string(VdfStructure.Indent, IndentationLevel));
                break;

            case State.Finished:
                await _writer.WriteLineAsync();
                break;
        }

        CurrentState = next;
    }

    /// <summary>
    /// Asynchronously writes a string to the output, escaping special characters based on <see cref="VdfSerializerSettings.UsesEscapeSequences"/>.
    /// </summary>
    /// <param name="str">The string to escape and write.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    private async Task WriteEscapedStringAsync(string str)
    {
        if (!Settings.UsesEscapeSequences)
        {
            await _writer.WriteAsync(str);
            return;
        }

        int currentPos = 0;
        while (currentPos < str.Length)
        {
            int next = str.AsSpan(currentPos).IndexOfAny(EscapableChars);

            if (next == -1)
            {
                await _writer.WriteAsync(str.AsMemory(currentPos));
                break;
            }

            if (next > 0)
            {
                await _writer.WriteAsync(str.AsMemory(currentPos, next));
            }

            await _writer.WriteAsync(VdfStructure.Escape);
            await _writer.WriteAsync(str[currentPos + next].ToVdfEscape());

            currentPos += next + 1;
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        if (CloseOutput) await _writer.DisposeAsync();
        await base.DisposeAsyncCore();
    }
}
