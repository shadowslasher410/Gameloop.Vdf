using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests;

public class VConditionalTests
{

    [Fact]
    public void Evaluate_EmptyConditional_ReturnsTrue()
    {
        var cond = new VConditional();
        Assert.True(cond.Evaluate([]));
    }

    [Theory]
    [InlineData(VConditional.Windows, true)]
    [InlineData(VConditional.Linux, false)]
    public void Evaluate_SingleConstant_ReturnsCorrectResult(string platform, bool expected)
    {
        var cond = new VConditional { new(VConditional.TokenType.Constant, platform) };
        Assert.Equal(expected, cond.Evaluate([VConditional.Windows]));
    }

    [Fact]
    public void Evaluate_NotOperator_InvertsResult()
    {
        var cond = new VConditional
        {
            new(VConditional.TokenType.Not),
            new(VConditional.TokenType.Constant, VConditional.Windows)
        };

        Assert.False(cond.Evaluate([VConditional.Windows]));
        Assert.True(cond.Evaluate([VConditional.Linux]));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)] 
    public void Evaluate_OrOperator_ReturnsCorrectResult(bool winDefined, bool linuxDefined, bool expected)
    {
        var cond = new VConditional
        {
            new(VConditional.TokenType.Constant, VConditional.Windows),
            new(VConditional.TokenType.Or),
            new(VConditional.TokenType.Constant, VConditional.Linux)
        };

        var defined = new List<string>();
        if (winDefined) defined.Add(VConditional.Windows);
        if (linuxDefined) defined.Add(VConditional.Linux);

        Assert.Equal(expected, cond.Evaluate(defined));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void Evaluate_AndOperator_ReturnsCorrectResult(bool winDefined, bool linuxDefined, bool expected)
    {
        var cond = new VConditional
        {
            new(VConditional.TokenType.Constant, VConditional.Windows),
            new(VConditional.TokenType.And),
            new(VConditional.TokenType.Constant, VConditional.Linux)
        };

        var defined = new List<string>();
        if (winDefined) defined.Add(VConditional.Windows);
        if (linuxDefined) defined.Add(VConditional.Linux);

        Assert.Equal(expected, cond.Evaluate(defined));
    }

    [Fact]
    public void Evaluate_ComplexLogic_OrAndOperatorsWorkSequentially()
    {
        var cond = new VConditional
        {
            new(VConditional.TokenType.Constant, VConditional.Windows),
            new(VConditional.TokenType.Or),
            new(VConditional.TokenType.Constant, VConditional.OsX),
            new(VConditional.TokenType.And),
            new(VConditional.TokenType.Not),
            new(VConditional.TokenType.Constant, VConditional.Linux)
        };

        Assert.True(cond.Evaluate([VConditional.Windows]));
        Assert.True(cond.Evaluate([VConditional.OsX]));
        Assert.False(cond.Evaluate([VConditional.Windows, VConditional.Linux]));
        Assert.False(cond.Evaluate([VConditional.Ps5]));
    }

    [Fact]
    public void Evaluate_InvalidTokenSequence_ThrowsInvalidOperationException()
    {
        var cond = new VConditional { new(VConditional.TokenType.Or) };
        Assert.Throws<InvalidOperationException>(() => cond.Evaluate([VConditional.Windows]));
    }

    [Fact]
    public void DeepClone_CreatesIndependentAndExactCopy()
    {
        var original = new VConditional
        {
            new(VConditional.TokenType.Not),
            new(VConditional.TokenType.Constant, VConditional.Windows)
        };

        var clone = (VConditional)original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(original, clone);
        Assert.Equal(original.Tokens.Count, clone.Tokens.Count);

        for (int i = 0; i < original.Tokens.Count; i++)
        {
            Assert.Equal(original.Tokens[i].TokenType, clone.Tokens[i].TokenType);
            Assert.Equal(original.Tokens[i].Name, clone.Tokens[i].Name);
        }
    }

    [Fact]
    public void DeepEquals_CorrectlyIdentifiesEqualityAndDifferences()
    {
        var cond1 = new VConditional { new(VConditional.TokenType.Constant, "A") };
        var cond2 = new VConditional { new(VConditional.TokenType.Constant, "A") };
        var cond3 = new VConditional { new(VConditional.TokenType.Constant, "B") };

        Assert.True(VToken.DeepEquals(cond1, cond2));
        Assert.False(VToken.DeepEquals(cond1, cond3));
        Assert.False(cond1.Equals(cond2));
    }
}