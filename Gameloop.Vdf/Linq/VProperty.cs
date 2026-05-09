namespace Gameloop.Vdf.Linq;

/// <summary>
/// Represents a VDF property, consisting of a key and a <see cref="VToken"/> value.
/// It can also optionally contain a <see cref="VConditional"/>.
/// </summary>
public class VProperty : VToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VProperty"/> class with a key, value, and optional conditional.
    /// </summary>
    /// <param name="key">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <param name="conditional">The optional conditional block associated with this property.</param>
    public VProperty(string key, VToken value, VConditional? conditional = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Value = value;
        Conditional = conditional;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VProperty"/> class by deep-cloning another property.
    /// </summary>
    /// <param name="other">The existing property to clone.</param>
    public VProperty(VProperty other)
        : this(other.Key, other.Value.DeepClone(), other.Conditional?.DeepClone() as VConditional) { }

    /// <summary>Gets or sets the key (name) of the property.</summary>
    public string Key { get; set; }

    /// <summary>Gets or sets the <see cref="VToken"/> value of the property.</summary>
    /// <remarks>
    /// Setting this property automatically updates the <see cref="VToken.Parent"/> pointer 
    /// of the new value and clears the pointer of the old value.
    /// </remarks>
    public VToken Value
    {
        get => field;
        set
        {
            field?.Parent = null;
            field = value;
            field?.Parent = this;
        }
    }

    /// <summary>
    /// Gets or sets the optional <see cref="VConditional"/> block associated with this property.
    /// </summary>
    /// <remarks>
    /// Setting this property automatically updates the <see cref="VToken.Parent"/> pointer 
    /// of the new conditional and clears the pointer of the previous one.
    /// </remarks>
    public VConditional? Conditional
    {
        get => field;
        set
        {
            field?.Parent = null;
            field = value;
            field?.Parent = this;
        }
    }

    /// <inheritdoc />
    public override VTokenType Type => VTokenType.Property;

    /// <inheritdoc />
    public override VToken DeepClone() => new VProperty(this);

    /// <inheritdoc />
    public override void WriteTo(VdfWriter writer)
    {
        writer.WriteKey(Key);
        Value.WriteTo(writer);
        Conditional?.WriteTo(writer);
    }

    /// <inheritdoc />
    protected override bool DeepEquals(VToken node)
    {
        return node is VProperty other
            && Key == other.Key
            && VToken.DeepEquals(Value, other.Value)
            && VToken.DeepEquals(Conditional, other.Conditional);
    }
}