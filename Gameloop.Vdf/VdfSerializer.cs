using Gameloop.Vdf.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Gameloop.Vdf;

public class VdfSerializer
{
    public required VdfSerializerSettings Settings { get; init; }

    public VdfSerializer() { }

    [SetsRequiredMembers]
    public VdfSerializer(VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;

        if (Settings is { UsesConditionals: true, DefinedConditionals: null })
            throw new VdfException("DefinedConditionals must be set when UsesConditionals=true.");
    }

    public void Serialize(TextWriter textWriter, VToken value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using VdfWriter vdfWriter = new VdfTextWriter(textWriter, Settings);
        value.WriteTo(vdfWriter);
    }

    public VProperty Deserialize(TextReader textReader)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        using VdfReader vdfReader = new VdfTextReader(textReader, Settings);

        if (!vdfReader.ReadToken())
            throw new VdfException("Incomplete VDF data at beginning of file.");

        while (vdfReader.CurrentState == VdfState.Comment)
            if (!vdfReader.ReadToken())
                throw new VdfException("Incomplete VDF data after root comment.");

        return ReadProperty(vdfReader);
    }

    private static VProperty LoadBaseFile(string path)
    {
        if (!File.Exists(path)) throw new VdfException($"Base file not found: {path}");
        return VdfConvert.Deserialize(File.ReadAllText(path));
    }

    private VProperty ReadProperty(VdfReader reader)
    {
        if (reader.Value == "#base")
        {
            reader.ReadToken();
            return LoadBaseFile(reader.Value!);
        }

        VProperty result = new(reader.Value!, null!);

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after property key.");

        while (reader.CurrentState == VdfState.Comment)
            if (!reader.ReadToken())
                throw new VdfException("Incomplete VDF data after property comment.");

        switch (reader.CurrentState)
        {
            case VdfState.Property or VdfState.Value:
                result.Value = ParseScalar(reader);

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



    private VObject ReadObject(VdfReader reader)
    {
        VObject result = [];
        string objectEnd = VdfStructure.ObjectEnd.ToString();

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after object start.");

        while (reader.CurrentState != VdfState.Finished && reader.CurrentState != VdfState.Closed)
        {
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

    private static VConditional ReadConditional(VdfReader reader)
    {
        VConditional result = [];

        if (!reader.ReadToken())
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

            if (!reader.ReadToken())
                throw new VdfException("Incomplete VDF data after conditional expression.");
        }

        if (!reader.ReadToken())
            throw new VdfException("Incomplete VDF data after conditional end.");

        return result;
    }

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

    private VValue ParseScalar(VdfReader reader)
    {
        string raw = reader.Value ?? string.Empty;

        if (Settings.Format == KeyValuesFormat.Kv3 && raw.Contains(':'))
        {
            int colonIndex = raw.IndexOf(':');
            string hint = raw[..colonIndex].ToLowerInvariant();
            string val = raw[(colonIndex + 1)..];

            if (val.Length >= 2 && val.StartsWith('"') && val.EndsWith('"'))
                val = val[1..^1];

            return new VValue(val) { TypeHint = hint };
        }

        return new VValue(raw);
    }


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
                result.Add(new VProperty(string.Empty, ReadValue(reader)));
            }

            if (!reader.ReadToken()) throw new VdfException("Incomplete array.");
            if (reader.Value == ",") reader.ReadToken();
        }

        reader.ReadToken();
        return result;
    }

    public async Task SerializeAsync(TextWriter textWriter, VToken value)
    {
        ArgumentNullException.ThrowIfNull(value);
        await using VdfWriter vdfWriter = new VdfTextWriter(textWriter, Settings);
        await SerializeTokenAsync(vdfWriter, value);
    }

    public async Task<VProperty> DeserializeAsync(TextReader textReader)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        using VdfReader vdfReader = new VdfTextReader(textReader, Settings);

        if (!await vdfReader.ReadTokenAsync())
            throw new VdfException("Incomplete VDF data at beginning of file.");

        while (vdfReader.CurrentState == VdfState.Comment)
            if (!await vdfReader.ReadTokenAsync())
                throw new VdfException("Incomplete VDF data after root comment.");

        return await ReadPropertyAsync(vdfReader);
    }

    private static async Task SerializeTokenAsync(VdfWriter writer, VToken token)
    {
        switch (token)
        {
            case VProperty prop:
                await writer.WriteKeyAsync(prop.Key);
                await SerializeTokenAsync(writer, prop.Value);
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
