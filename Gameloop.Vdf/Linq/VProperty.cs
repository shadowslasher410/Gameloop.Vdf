namespace Gameloop.Vdf.Linq;

public class VProperty : VToken
{
    public VProperty(string key, VToken value, VConditional? conditional = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Value = value;
        Conditional = conditional;
    }

    public VProperty(VProperty other)
        : this(other.Key, other.Value.DeepClone(), other.Conditional?.DeepClone() as VConditional) { }

    public string Key { get; set; }

    public VToken Value
    {
        get => field;
        set
        {
            if (field != null) field.Parent = null;
            field = value;
            if (field != null) field.Parent = this;
        }
    }


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

    public override VTokenType Type => VTokenType.Property;

    public override VToken DeepClone() => new VProperty(this);

    public override void WriteTo(VdfWriter writer)
    {
        writer.WriteKey(Key);
        Value.WriteTo(writer);
        Conditional?.WriteTo(writer);
    }

    protected override bool DeepEquals(VToken node)
    {
        return node is VProperty other
            && Key == other.Key
            && VToken.DeepEquals(Value, other.Value)
            && VToken.DeepEquals(Conditional, other.Conditional);
    }
}
