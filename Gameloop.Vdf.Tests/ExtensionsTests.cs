using Gameloop.Vdf.Linq;
using Xunit;

namespace Gameloop.Vdf.Tests;

public class ExtensionsTests
{
    #region Value Extension Tests

    [Fact]
    public void Value_WhenCollectionHasItem_ReturnsConvertedValue()
    {
        var tokens = new List<VValue> { new("100") };

        Assert.Equal(100, tokens.Value<int>());
        Assert.Equal("100", tokens.Value<string>());
    }

    [Fact]
    public void Value_OnEmptyEnumerable_ReturnsDefault()
    {
        IEnumerable<VToken> tokens = [];

        Assert.Null(tokens.Value<string>());
        Assert.Equal(0, tokens.Value<int>());
    }

    [Fact]
    public void Value_OnNullEnumerable_ThrowsArgumentNullException()
    {
        IEnumerable<VToken> tokens = null!;
        Assert.Throws<ArgumentNullException>(() => tokens.Value<string>());
    }

    #endregion

    #region Convert Extension Tests

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Convert_VdfBooleanStrings_ReturnsExpectedBool(string input, bool expected)
    {
        var token = new VValue(input);

        Assert.Equal(expected, token.Convert<VToken, bool>());
        Assert.Equal(expected, token.Convert<VToken, bool?>());
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("3.14", 3.14f)]
    public void Convert_NumericStrings_UsesInvariantCulture(string input, object expected)
    {
        var token = new VValue(input);

        if (expected is int expectedInt)
            Assert.Equal(expectedInt, token.Convert<VValue, int>());
        else if (expected is float expectedFloat)
            Assert.Equal(expectedFloat, token.Convert<VValue, float>());
    }

    [Fact]
    public void Convert_DirectMatch_ReturnsOriginalToken()
    {
        var token = new VValue("test");
        var result = token.Convert<VValue, VValue>();

        Assert.Same(token, result);
    }

    [Fact]
    public void Convert_NullScenarios_ReturnsDefault()
    {
        VToken? nullToken = null;
        var tokenWithNullValue = new VValue(null);

        Assert.Null(nullToken.Convert<VToken, int?>());
        Assert.Null(tokenWithNullValue.Convert<VValue, int?>());
    }

    [Fact]
    public void Convert_NonVValueToken_ThrowsInvalidCastException()
    {
        VToken token = new VObject();
        Assert.Throws<InvalidCastException>(() => token.Convert<VToken, int>());
    }

    #endregion

    #region ToLowerSafe Tests

    [Theory]
    [InlineData("HELLO", "hello")]
    [InlineData("STEAM_DECK", "steam_deck")]
    [InlineData(null, null)]
    public void ToLowerSafe_Scenarios_ReturnsExpectedResult(string? input, string? expected)
    {
        Assert.Equal(expected, input.ToLowerSafe());
    }

    #endregion
}
