using Xunit;
using Gameloop.Vdf;
using System.IO;
using System.Threading.Tasks;

namespace Gameloop.Vdf.Tests;

public class VdfTextReaderTests
{
    private static VdfSerializerSettings GetSettings(int maxToken = 1024, bool useEscapes = false) => new()
    {
        DefinedConditionals = [],
        MaximumTokenSize = maxToken,
        UsesEscapeSequences = useEscapes
    };

    [Theory]
    [InlineData("\"key\"", "key", VdfState.Property)]
    [InlineData("{", "{", VdfState.Object)]
    [InlineData("}", "}", VdfState.Object)]
    [InlineData("// my comment\n", " my comment", VdfState.Comment)]
    public void ReadToken_StructuralElements_SetsCorrectState(string input, string expectedValue, VdfState expectedState)
    {
        using var textReader = new StringReader(input);
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        bool hasToken = vdfReader.ReadToken();

        Assert.True(hasToken);
        Assert.Equal(expectedValue, vdfReader.Value);
        Assert.Equal(expectedState, vdfReader.CurrentState);
    }

    [Fact]
    public void ReadToken_UnquotedProperty_StopsAtWhitespace()
    {
        using var textReader = new StringReader("base_key next_token");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        vdfReader.ReadToken();

        Assert.Equal("base_key", vdfReader.Value);
        Assert.Equal(VdfState.Property, vdfReader.CurrentState);
    }

    [Fact]
    public void ReadToken_EscapeSequences_HandlesCorrectly()
    {
        var settings = GetSettings();
        settings.UsesEscapeSequences = true;
        using var textReader = new StringReader("\"key_with_\\\"quote\\\"_and_\\n_newline\"");
        var vdfReader = new VdfTextReader(textReader, settings);

        vdfReader.ReadToken();

        Assert.Equal("key_with_\"quote\"_and_\n_newline", vdfReader.Value);
    }

    [Fact]
    public void ReadToken_VectorizedFastForward_HandlesLongStrings()
    {
        string longString = new('a', 500);
        using var textReader = new StringReader($"\"{longString}\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        vdfReader.ReadToken();

        Assert.Equal(longString, vdfReader.Value);
    }

    [Fact]
    public async Task ReadTokenAsync_Conditionals_HandlesStateChanges()
    {
        using var textReader = new StringReader("[$WINDOWS || ! $LINUX]");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        await vdfReader.ReadTokenAsync();
        Console.WriteLine($"Token 1 - Value: '{vdfReader.Value}', State: {vdfReader.CurrentState}");
        Assert.Equal("[", vdfReader.Value);

        await vdfReader.ReadTokenAsync();
        Console.WriteLine($"Token 2 - Value: '{vdfReader.Value}', State: {vdfReader.CurrentState}");
        Assert.Equal("WINDOWS", vdfReader.Value);
        Assert.Equal(VdfState.Conditional, vdfReader.CurrentState);

        await vdfReader.ReadTokenAsync();
        Console.WriteLine($"Token 3 - Value: '{vdfReader.Value}', State: {vdfReader.CurrentState}");
        Assert.Equal("||", vdfReader.Value);
    }


    [Fact]
    public void ReadToken_TokenTooLong_ThrowsIndexOutOfRangeException()
    {
        var settings = GetSettings(maxToken: 10);
        using var textReader = new StringReader("\"0123456789ABCDE\"");
        var vdfReader = new VdfTextReader(textReader, settings);

        Assert.Throws<VdfException>(() => vdfReader.ReadToken());
    }

    [Fact]
    public void ReadToken_BufferBoundary_HandlesSplitTokens()
    {
        string padding = new(' ', 1020);
        string input = $"{padding}\"cross_boundary\"";

        using var textReader = new StringReader(input);
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        bool result = vdfReader.ReadToken();

        Assert.True(result);
        Assert.Equal("cross_boundary", vdfReader.Value);
    }

    [Fact]
    public async Task ReadTokenAsync_ValidEscape_ReturnsUnescapedChar()
    {
        using var textReader = new StringReader("\"key\\nvalue\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings(useEscapes: true));

        bool hasToken = await vdfReader.ReadTokenAsync();

        Assert.True(hasToken);
        Assert.Equal("key\nvalue", vdfReader.Value);
    }

    [Fact]
    public async Task ReadTokenAsync_IncompleteEscape_ThrowsVdfException()
    {
        using var textReader = new StringReader("\"key\\");
        var vdfReader = new VdfTextReader(textReader, GetSettings(useEscapes: true));

        var ex = await Assert.ThrowsAsync<VdfException>(async () => await vdfReader.ReadTokenAsync());
        Assert.Equal("Incomplete escape sequence.", ex.Message);
    }

    [Fact]
    public async Task ReadTokenAsync_StructuralElements_ReturnsIndividually()
    {
        using var textReader = new StringReader("{}[ ]");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        Assert.True(await vdfReader.ReadTokenAsync());
        Assert.Equal(VdfState.Object, vdfReader.CurrentState);
        Assert.Equal("{", vdfReader.Value);

        Assert.True(await vdfReader.ReadTokenAsync());
        Assert.Equal(VdfState.Object, vdfReader.CurrentState);
        Assert.Equal("}", vdfReader.Value);

        Assert.True(await vdfReader.ReadTokenAsync());
        Assert.Equal(VdfState.ArrayStart, vdfReader.CurrentState);

        Assert.True(await vdfReader.ReadTokenAsync());
        Assert.Equal(VdfState.ArrayEnd, vdfReader.CurrentState);
    }

    [Fact]
    public async Task ReadTokenAsync_QuotedStructural_TreatsAsLiteral()
    {
        using var textReader = new StringReader("\"{ }\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        await vdfReader.ReadTokenAsync();

        Assert.Equal("{ }", vdfReader.Value);
        Assert.Equal(VdfState.Property, vdfReader.CurrentState);
    }

    [Fact]
    public async Task ReadTokenAsync_SeekToken_HandlesLeadingWhitespace()
    {
        using var textReader = new StringReader("    \r\n\t  \"found\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        bool result = await vdfReader.ReadTokenAsync();

        Assert.True(result);
        Assert.Equal("found", vdfReader.Value);
    }

    [Fact]
    public async Task ReadTokenAsync_MultipleCalls_ReturnsFalseAtEnd()
    {
        using var textReader = new StringReader("\"token\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings());

        bool first = await vdfReader.ReadTokenAsync();
        bool second = await vdfReader.ReadTokenAsync();

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task DisposeAsync_ClosesUnderlyingReader()
    {
        var textReader = new StringReader("\"data\"");
        var vdfReader = new VdfTextReader(textReader, GetSettings()) { CloseInput = true };

        await vdfReader.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => textReader.Read());
    }
}
