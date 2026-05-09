namespace Gameloop.Vdf.Linq;

/// <summary>
/// Represents a leaf value in a VDF structure, such as a string, number, or comment.
/// </summary>
/// <param name="value">The underlying data value.</param>
/// <param name="type">The specific token type (defaults to <see cref="VTokenType.Value"/>).</param>
public class VValue(object? value, VTokenType type = VTokenType.Value) : VToken
{
    /// <summary>Gets or sets the underlying value of this token.</summary>
    public object? Value { get; set; } = value;

    /// <summary>Gets the VDF token type (e.g., Value or Comment).</summary>
    public override VTokenType Type => type;

    /// <summary>
    /// Gets or sets an optional type hint used during serialization or parsing to influence 
    /// how the value is treated (e.g., forcing a string to be treated as a number).
    /// </summary>
    public string? TypeHint
    {
        get => field;
        set => field = value?.ToLowerInvariant();
    }

    /// <summary>Creates a deep copy of this value, including its type and type hint.</summary>
    /// <returns>A new <see cref="VValue"/> instance.</returns>
    public override VToken DeepClone() => new VValue(Value, Type) { TypeHint = TypeHint };

    /// <summary>
    /// Serializes the value or comment to the provided <see cref="VdfWriter"/>.
    /// </summary>
    /// <param name="writer">The writer used to output the VDF format.</param>
    public override void WriteTo(VdfWriter writer)
    {
        if (Type == VTokenType.Comment)
            writer.WriteComment(Value?.ToString() ?? string.Empty);
        else
            writer.WriteValue(this);
    }

    /// <summary>Returns the string representation of the underlying value.</summary>
    public override string ToString() => Value?.ToString() ?? string.Empty;

    /// <summary>Creates a new <see cref="VValue"/> initialized as a comment.</summary>
    /// <param name="value">The comment text.</param>
    /// <returns>A comment token.</returns>
    public static VValue CreateComment(string value) => new(value, VTokenType.Comment);

    /// <summary>Creates a new <see cref="VValue"/> containing an empty string.</summary>
    /// <returns>An empty value token.</returns>
    public static VValue CreateEmpty() => new(string.Empty);

    /// <summary>
    /// Compares this value with another token for equality based on type, string content, and type hint.
    /// </summary>
    /// <param name="token">The token to compare against.</param>
    /// <returns>True if the values are equivalent; otherwise, false.</returns>
    protected override bool DeepEquals(VToken token)
    {
        if (token is not VValue other) return false;

        return Type == other.Type &&
               Equals(Value?.ToString(), other.Value?.ToString()) &&
               TypeHint == other.TypeHint;
    }
}