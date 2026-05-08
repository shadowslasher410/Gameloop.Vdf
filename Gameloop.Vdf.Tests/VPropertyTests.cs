using global::Gameloop.Vdf.Linq;
namespace Gameloop.Vdf.Tests;

public class VPropertyTests
{
    [Fact]
    public void Constructor_SetsPropertiesAndPointers()
    {
        var value = new VValue("test_value");
        var property = new VProperty("test_key", value);

        Assert.Equal("test_key", property.Key);
        Assert.Equal(value, property.Value);
        Assert.Equal(property, value.Parent);
    }

    [Fact]
    public void Value_Setter_UpdatesParentPointers()
    {
        var oldValue = new VValue("old");
        var newValue = new VValue("new");
        var property = new VProperty("key", oldValue)
        {
            Value = newValue
        };

        Assert.Null(oldValue.Parent);
        Assert.Equal(property, newValue.Parent);
        Assert.Equal(newValue, property.Value);
    }

    [Fact]
    public void Conditional_Setter_UpdatesParentPointers()
    {
        var cond = new VConditional();
        var property = new VProperty("key", new VValue("val"))
        {
            Conditional = cond
        };

        Assert.Equal(property, cond.Parent);

        property.Conditional = null;
        Assert.Null(cond.Parent);
    }

    [Fact]
    public void DeepClone_CreatesNewInstanceWithCopiedValue()
    {
        var originalCond = new VConditional();
        var original = new VProperty("key", new VValue("val"), originalCond);

        var clone = (VProperty)original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Key, clone.Key);
        Assert.NotSame(original.Value, clone.Value);
        Assert.NotSame(original.Conditional, clone.Conditional);
        Assert.Equal(clone, clone.Value.Parent);
        Assert.Equal(clone, clone.Conditional?.Parent);
    }

    [Fact]
    public void DeepEquals_ReturnsTrueForIdenticalProperties()
    {
        var prop1 = new VProperty("key", new VValue("val"), null);
        var prop2 = new VProperty("key", new VValue("val"), null);

        Assert.True(VToken.DeepEquals(prop1, prop2));
    }

    [Fact]
    public void DeepEquals_ReturnsFalseForDifferentConditionals()
    {
        var cond1 = new VConditional
                {
                    new VConditional.Token(VConditional.TokenType.Constant, "W")
                };

        var prop1 = new VProperty("key", new VValue("val"), cond1);
        var prop2 = new VProperty("key", new VValue("val"), null);

        Assert.False(prop1.Equals(prop2));
    }

    [Fact]
    public void Constructor_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VProperty(null!, new VValue("v")));
    }
    [Fact]
    public void Key_Setter_UpdatesCorrectly()
    {
        var property = new VProperty("old_key", new VValue("val"))
        {
            Key = "new_key"
        };

        Assert.Equal("new_key", property.Key);
    }
}