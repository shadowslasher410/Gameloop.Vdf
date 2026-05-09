using Gameloop.Vdf.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Gameloop.Vdf;

/// <summary>
/// Coordinates the serialization and deserialization of VDF data using configured <see cref="VdfSerializerSettings"/>.
/// </summary>
public class VdfSerializer
{
    /// <summary>
    /// Gets the settings that define how VDF data is formatted, parsed, and handled.
    /// </summary>
    public required VdfSerializerSettings Settings { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VdfSerializer"/> class.
    /// </summary>
    public VdfSerializer() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="VdfSerializer"/> class with specific settings.
    /// </summary>
    /// <param name="settings">The settings to use for serialization and deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="settings"/> is null.</exception>
    /// <exception cref="VdfException">Thrown if conditionals are enabled but no defined conditionals list is provided.</exception>
    [SetsRequiredMembers]
    public VdfSerializer(VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;

        if (Settings is { UsesConditionals: true, DefinedConditionals: null })
            throw new VdfException("DefinedConditionals must be set when UsesConditionals=true.");
    }

    /// <summary>
    /// Serializes a <see cref="VToken"/> structure into a <see cref="TextWriter"/> as VDF text.
    /// </summary>
    /// <param name="textWriter">The destination for the VDF text.</param>
    /// <param name="value">The root token to serialize.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public void Serialize(TextWriter textWriter, VToken value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using VdfWriter vdfWriter = new VdfTextWriter(textWriter, Settings);
        value.WriteTo(vdfWriter);
    }

    /// <summary>
    /// Deserializes VDF data from a <see cref="TextReader"/> into a <see cref="VProperty"/> tree.
    /// </summary>
    /// <param name="textReader">The source for the VDF text.</param>
    /// <returns>The root <see cref="VProperty"/> of the VDF structure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="textReader"/> is null.</exception>
    /// <exception cref="VdfException">Thrown if the VDF data is incomplete or malformed.</exception>
    public VProperty Deserialize(TextReader textReader)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        using VdfReader vdfReader = new VdfTextReader(textReader, Settings);

        if (!vdfReader.ReadToken())
            throw new VdfException("Incomplete VDF data at beginning of file.");

        // Skip leading comments to reach the root property
        while (vdfReader.CurrentState == VdfState.Comment)
            if (!vdfReader.ReadToken())
                throw new VdfException("Incomplete VDF data after root comment.");

        return ReadProperty(vdfReader);
    }

    /// <summary>
    /// Loads a VDF file from the specified path for inclusion or merging.
    /// </summary>
    /// <param name="path">The file system path to the VDF file.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    /// <exception cref="VdfException">Thrown if the file does not exist.</exception>
    private static VProperty LoadBaseFile(string path)
    {
        if (!File.Exists(path)) throw new VdfException($"Base file not found: {path}");
        return VdfConvert.Deserialize(File.ReadAllText(path));
    }

    /// <summary>
    /// Reads a <see cref="VProperty"/> from the reader, handling base file inclusions, scalar values, and objects.
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    /// <exception cref="VdfException">Thrown if the VDF structure is malformed or incomplete.</exception>
    private VProperty ReadProperty(VdfReader reader)
    {
        // Handle Valve's "#base" inclusion directive.
        if (reader.Value == "#base")
        {
            reader.ReadToken();
            return LoadBaseFile(reader.Value!);
        }

        VProperty result = new(reader.Value!, null!);

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after property key.");

        // Skip any comments placed between a key and its value.
        while (reader.CurrentState == VdfState.Comment)
            if (!reader.ReadToken())
                throw new VdfException("Incomplete VDF data after property comment.");

        switch (reader.CurrentState)
        {
            case VdfState.Property or VdfState.Value:
                result.Value = ParseScalar(reader);

                // Look ahead for an optional conditional block (e.g., [$WIN32]) after the value.
                if (!reader.ReadToken())
                    return result;

                if (reader.CurrentState == VdfState.Conditional)
                {
                    result.Conditional = ReadConditional(reader);
                }
                break;

            case VdfState.Object or VdfState.ObjectStart:
                result.Value = ReadObject(reader);
                break;

            default:
                throw new VdfException($"Unexpected state deserializing property (key: {result.Key}, state: {reader.CurrentState}).");
        }

        return result;
    }

    /// <summary>
    /// Reads a <see cref="VObject"/> (collection of properties) from the reader until a closing brace is found.
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>The deserialized <see cref="VObject"/>.</returns>
    /// <exception cref="VdfException">Thrown if the object is never closed or contains unexpected states.</exception>
    private VObject ReadObject(VdfReader reader)
    {
        VObject result = [];
        string objectEnd = VdfStructure.ObjectEnd.ToString();

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after object start.");

        while (reader.CurrentState != VdfState.Finished && reader.CurrentState != VdfState.Closed)
        {
            // Exit if we hit the closing brace '}'.
            if (reader.CurrentState == VdfState.Object && reader.Value == objectEnd)
                return result;

            if (reader.CurrentState == VdfState.Comment)
            {
                result.Add(VValue.CreateComment(reader.Value ?? string.Empty));
                if (!reader.ReadToken()) break;
            }
            else if (reader.CurrentState == VdfState.Property || reader.CurrentState == VdfState.Value)
            {
                VProperty prop = ReadProperty(reader);

                // Only add the property if conditionals are disabled or if the conditional evaluates to true.
                if (!Settings.UsesConditionals || prop.Conditional == null || prop.Conditional.Evaluate(Settings.DefinedConditionals!))
                    result.Add(prop);

                if (reader.CurrentState == VdfState.Finished || reader.CurrentState == VdfState.Closed)
                    break;
            }
            else
            {
                throw new VdfException($"Unexpected state {reader.CurrentState} at '{reader.Value}'.");
            }
        }
        throw new VdfException("Unexpected end of file: Object was never closed with '}'.");
    }

    /// <summary>
    /// Reads and parses a conditional expression block from the reader.
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>A <see cref="VConditional"/> containing the expression tokens.</returns>
    /// <exception cref="VdfException">Thrown if tokens are invalid or the block is unclosed.</exception>
    private static VConditional ReadConditional(VdfReader reader)
    {
        VConditional result = [];

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after conditional start.");

        string condEnd = VdfStructure.ConditionalEnd.ToString();
        while (reader.CurrentState == VdfState.Conditional && reader.Value != condEnd)
        {
            // Map the raw strings to specific conditional token types.
            VConditional.Token token = reader.Value switch
            {
                "!" => new(VConditional.TokenType.Not),
                "&&" => new(VConditional.TokenType.And),
                "||" => new(VConditional.TokenType.Or),
                string s => new(VConditional.TokenType.Constant, s.StartsWith('$') ? s[1..] : s),
                _ => throw new VdfException($"Unexpected conditional token: {reader.Value}")
            };

            result.Add(token);

            if (!reader.ReadToken())
                throw new VdfException("Incomplete VDF data after conditional expression.");
        }

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after conditional end.");

        return result;
    }

    /// <summary>
    /// Determines the appropriate VDF token type based on the reader's current state and delegates to the specific read method.
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>A <see cref="VToken"/> representing the parsed value, array, or object.</returns>
    /// <exception cref="VdfException">Thrown if the current reader state does not correspond to a valid value type.</exception>
    private VToken ReadValue(VdfReader reader)
    {
        return reader.CurrentState switch
        {
            VdfState.ObjectStart or VdfState.Object => ReadObject(reader),
            VdfState.ArrayStart => ReadArray(reader),
            VdfState.Value or VdfState.Property => ParseScalar(reader),
            _ => throw new VdfException($"Unexpected state {reader.CurrentState} for value.")
        };
    }

    /// <summary>
    /// Parses a single string value, handling optional KeyValues3 type hinting (e.g., "int:123").
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>A <see cref="VValue"/> containing the processed string and optional type hint.</returns>
    private VValue ParseScalar(VdfReader reader)
    {
        string raw = reader.Value ?? string.Empty;

        // KV3 format uses a colon to separate type hints from the value.
        if (Settings.Format == KeyValuesFormat.Kv3 && raw.Contains(':'))
        {
            int colonIndex = raw.IndexOf(':');
            string hint = raw[..colonIndex].ToLowerInvariant();
            string val = raw[(colonIndex + 1)..];

            // Strip surrounding quotes from the value portion if present.
            if (val.Length >= 2 && val.StartsWith('"') && val.EndsWith('"'))
                val = val[1..^1];

            return new VValue(val) { TypeHint = hint };
        }

        return new VValue(raw);
    }

    /// <summary>
    /// Reads a VDF array structure, typically found in newer KeyValues formats, until the closing bracket is reached.
    /// </summary>
    /// <param name="reader">The <see cref="VdfReader"/> to read from.</param>
    /// <returns>A <see cref="VObject"/> populated with array elements using empty keys.</returns>
    /// <exception cref="VdfException">Thrown if the array structure is incomplete or improperly terminated.</exception>
    private VObject ReadArray(VdfReader reader)
    {
        VObject result = [];
        reader.ReadToken();

        while (reader.CurrentState != VdfState.ArrayEnd)
        {
            if (reader.CurrentState == VdfState.Comment)
            {
                result.Add(VValue.CreateComment(reader.Value!));
            }
            else
            {
                // Array items are represented as VProperties with empty keys in KV3.
                result.Add(new VProperty(string.Empty, ReadValue(reader)));
            }

            if (!reader.ReadToken()) throw new VdfException("Incomplete array.");

            // Support comma-separated values often found in KV3 array structures.
            if (reader.Value == ",") reader.ReadToken();
        }

        // Advance past the closing bracket.
        reader.ReadToken();
        return result;
    }

    /// <summary>
    /// Asynchronously serializes a <see cref="VToken"/> structure into a <see cref="TextWriter"/> as VDF text.
    /// </summary>
    /// <param name="textWriter">The destination for the VDF text.</param>
    /// <param name="value">The root token to serialize.</param>
    /// <returns>A task representing the asynchronous serialization operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public async Task SerializeAsync(TextWriter textWriter, VToken value)
    {
        ArgumentNullException.ThrowIfNull(value);
        await using VdfWriter vdfWriter = new VdfTextWriter(textWriter, Settings);
        await SerializeTokenAsync(vdfWriter, value);
    }

    /// <summary>
    /// Asynchronously deserializes VDF data from a <see cref="TextReader"/> into a <see cref="VProperty"/> tree.
    /// </summary>
    /// <param name="textReader">The source for the VDF text.</param>
    /// <returns>A task containing the root <see cref="VProperty"/> of the VDF structure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="textReader"/> is null.</exception>
    /// <exception cref="VdfException">Thrown if the VDF data is incomplete or malformed.</exception>
    public async Task<VProperty> DeserializeAsync(TextReader textReader)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        using VdfReader vdfReader = new VdfTextReader(textReader, Settings);

        if (!await vdfReader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data at beginning of file.");

        // Skip leading comments
        while (vdfReader.CurrentState == VdfState.Comment)
            if (!await vdfReader.ReadTokenAsync())
                throw new VdfException("Incomplete VDF data after root comment.");

        return await ReadPropertyAsync(vdfReader);
    }

    /// <summary>
    /// Recursively and asynchronously serializes a specific <see cref="VToken"/> to the writer.
    /// </summary>
    private static async Task SerializeTokenAsync(VdfWriter writer, VToken token)
    {
        switch (token)
        {
            case VProperty prop:
                await writer.WriteKeyAsync(prop.Key);
                await SerializeTokenAsync(writer, prop.Value);
                // Serialize the conditional block immediately after the value if it exists.

                if (prop.Value is VValue && prop.Conditional != null)
                    await writer.WriteConditionalAsync(prop.Conditional.Tokens);
                break;

            case VObject obj:
                await writer.WriteObjectStartAsync();
                foreach (VToken child in obj)
                    await SerializeTokenAsync(writer, child);
                await writer.WriteObjectEndAsync();
                break;

            case VValue val:
                if (val.Type == VTokenType.Comment)
                    await writer.WriteCommentAsync(val.ToString());
                else
                    await writer.WriteValueAsync(val.ToString(), val.TypeHint);
                break;

            default:
                throw new VdfException($"Cannot serialize token type {token.GetType().Name} asynchronously.");
        }
    }

    /// <summary>
    /// Asynchronously reads a <see cref="VProperty"/> from the reader.
    /// </summary>
    private async Task<VProperty> ReadPropertyAsync(VdfReader reader)
    {
        if (reader.Value == "#base")
        {
            await reader.ReadTokenAsync();
            return LoadBaseFile(reader.Value!);
        }

        VProperty result = new(reader.Value!, null!);

        if (!await reader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data after property key.");

        while (reader.CurrentState == State.Comment)
            if (!await reader.ReadTokenAsync())
                throw new VdfException("Incomplete VDF data after property comment.");

        switch (reader.CurrentState)
        {
            case State.Property or State.Value:
                result.Value = ParseScalar(reader);

                if (!await reader.ReadTokenAsync()) return result;
                if (reader.CurrentState == State.Conditional)
                {
                    result.Conditional = await ReadConditionalAsync(reader);
                }      
                break;

            case State.Object or State.ObjectStart:
                result.Value = await ReadObjectAsync(reader);
                break;

            default:
                throw new VdfException($"Unexpected state ({reader.CurrentState}).");
        }

        return result;
    }

    /// <summary>
    /// Asynchronously reads a <see cref="VObject"/> until the closing brace is reached.
    /// </summary>
    private async Task<VObject> ReadObjectAsync(VdfReader reader)
    {
        VObject result = [];
        if (!await reader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data after object start.");

        string objectEnd = VdfStructure.ObjectEnd.ToString();
        while (reader.CurrentState != VdfState.Finished && reader.CurrentState != VdfState.Closed)
        {
            if (reader.CurrentState == VdfState.Object && reader.Value == objectEnd)
            {
                await reader.ReadTokenAsync();
                return result;
            }

            if (reader.CurrentState == VdfState.Comment)
            {
                result.Add(VValue.CreateComment(reader.Value!));
                if (!await reader.ReadTokenAsync()) break;
            }
            else if (reader.CurrentState == VdfState.Property || reader.CurrentState == VdfState.Value)
            {
                VProperty prop = await ReadPropertyAsync(reader);
                if (!Settings.UsesConditionals || prop.Conditional == null || prop.Conditional.Evaluate(Settings.DefinedConditionals!))
                    result.Add(prop);
            }
            else
            {
                if (!await reader.ReadTokenAsync()) break;
            }
        }

        throw new VdfException("Unexpected end of file: Object was never closed with '}'.");
    }

    /// <summary>
    /// Asynchronously reads and parses a conditional block (e.g. [$WIN32]).
    /// </summary>
    private static async Task<VConditional> ReadConditionalAsync(VdfReader reader)
    {
        VConditional result = [];
        if (!await reader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data after conditional start.");

        string condEnd = VdfStructure.ConditionalEnd.ToString();
        while (reader.CurrentState == VdfState.Conditional && reader.Value != condEnd)
        {
            VConditional.Token token = reader.Value switch
            {
                "!" => new(VConditional.TokenType.Not),
                "&&" => new(VConditional.TokenType.And),
                "||" => new(VConditional.TokenType.Or),
                string s => new(VConditional.TokenType.Constant, s.StartsWith('$') ? s[1..] : s),
                _ => throw new VdfException($"Unexpected conditional token: {reader.Value}")
            };
            result.Add(token);

            if (!await reader.ReadTokenAsync())
                throw new VdfException("Incomplete VDF data after conditional expression.");
        }

        if (!await reader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data after conditional end.");

        return result;
    }
}


public class VdfSerializerSettings
{
    public static VdfSerializerSettings Default => new();
    public static VdfSerializerSettings Common => new(true, false, []);

    [SetsRequiredMembers]
    public VdfSerializerSettings() { }

    [SetsRequiredMembers]
    private VdfSerializerSettings(bool escape, bool conditionals, IReadOnlyList<string>? defined)
    {
        UsesEscapeSequences = escape;
        UsesConditionals = conditionals;
        DefinedConditionals = defined;
    }

    public KeyValuesFormat Format { get; init; } = KeyValuesFormat.Auto;

    public bool UsesEscapeSequences { get; set; } = false;

    public bool UsesConditionals { get; set; } = true;

    public required IReadOnlyList<string>? DefinedConditionals { get; set; } = [];

    public int MaximumTokenSize
    {
        get => field;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 4096;
}
