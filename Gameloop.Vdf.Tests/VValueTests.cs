using Gameloop.Vdf.Linq;
using Xunit;

namespace Gameloop.Vdf.Tests;

public class VValueTests
{
    [Fact]
    public void Constructor_SetsValueAndType()
    {
        var val = new VValue("test", VTokenType.Value);

        Assert.Equal("test", val.Value);
        Assert.Equal(VTokenType.Value, val.Type);
    }

    [Fact]
    public void TypeHint_Setter_NormalizesToLower()
    {
        var val = new VValue("test") { TypeHint = "INTEGER" };

        Assert.Equal("integer", val.TypeHint);
    }

    [Fact]
    public void DeepClone_CopiesAllProperties()
    {
        var original = new VValue(123, VTokenType.Value) { TypeHint = "int" };

        var clone = (VValue)original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Value, clone.Value);
        Assert.Equal(original.Type, clone.Type);
        Assert.Equal(original.TypeHint, clone.TypeHint);
    }

    [Fact]
    public void ToString_ReturnsValueStringOrEmpty()
    {
        var val = new VValue(100);
        var nullVal = new VValue(null);

        Assert.Equal("100", val.ToString());
        Assert.Equal(string.Empty, nullVal.ToString());
    }

    [Fact]
    public void CreateComment_ReturnsCorrectType()
    {
        var comment = VValue.CreateComment("disclaimer");

        Assert.Equal(VTokenType.Comment, comment.Type);
        Assert.Equal("disclaimer", comment.Value);
    }

    [Fact]
    public void DeepEquals_IdentifiesMatches()
    {
        var v1 = new VValue("a") { TypeHint = "s" };
        var v2 = new VValue("a") { TypeHint = "s" }; // Note: setter lowercases this to "s"
        var v3 = new VValue("b");

        // Use VToken.DeepEquals to trigger the protected override logic
        Assert.True(VToken.DeepEquals(v1, v2));
        Assert.False(VToken.DeepEquals(v1, v3));
    }

    [Fact]
    public void DeepEquals_DifferentTypes_ReturnsFalse()
    {
        var val = new VValue("text", VTokenType.Value);
        var comment = new VValue("text", VTokenType.Comment);

        Assert.False(VToken.DeepEquals(val, comment));
    }

    [Fact]
    public void CreateEmpty_ReturnsEmptyStringValue()
    {
        var empty = VValue.CreateEmpty();

        Assert.Equal(string.Empty, empty.Value);
        Assert.Equal(VTokenType.Value, empty.Type);
    }
}
