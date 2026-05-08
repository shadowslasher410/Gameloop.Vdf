namespace Gameloop.Vdf.Linq;

public class VValue(object? value, VTokenType type = VTokenType.Value) : VToken
{
    public object? Value { get; set; } = value;

    public override VTokenType Type => type;

    public string? TypeHint
    {
        get => field;
        set => field = value?.ToLowerInvariant();
    }

    public override VToken DeepClone() => new VValue(Value, Type) { TypeHint = TypeHint };

    public override void WriteTo(VdfWriter writer)
    {
        if (Type == VTokenType.Comment)
            writer.WriteComment(Value?.ToString() ?? string.Empty);
        else
            writer.WriteValue(this);
    }

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public static VValue CreateComment(string value) => new(value, VTokenType.Comment);
    public static VValue CreateEmpty() => new(string.Empty);

    protected override bool DeepEquals(VToken token)
    {
        if (token is not VValue other) return false;

        return Type == other.Type &&
               Equals(Value?.ToString(), other.Value?.ToString()) &&
               TypeHint == other.TypeHint;
    }
}
