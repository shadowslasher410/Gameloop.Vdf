
using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests;

public class VdfTextWriterTests
{
    private static VdfSerializerSettings GetSettings(bool escapes = false, KeyValuesFormat format = KeyValuesFormat.Auto) => new()
    {
        DefinedConditionals = [],
        UsesEscapeSequences = escapes,
        Format = format
    };

    [Fact]
    public void WriteKey_ValidString_WritesQuotes()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());

        writer.WriteKey("MyKey");

        Assert.Equal("\"MyKey\"", sw.ToString());
    }

    [Fact]
    public void WriteValue_WithKV3TypeHint_WritesPrefix()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings(format: KeyValuesFormat.Kv3));

        writer.WriteValue("123", "int");

        Assert.Contains("int:\"123\"", sw.ToString());
    }

    [Fact]
    public void WriteObject_IndentsNestedContent()
    {
        using var sw = new StringWriter { NewLine = "\n" };
        var writer = new VdfTextWriter(sw, GetSettings());

        writer.WriteKey("Root");
        writer.WriteObjectStart();
        writer.WriteKey("Inner");
        writer.WriteValue("Val");
        writer.WriteObjectEnd();

        string output = sw.ToString();
        Assert.Contains("\n\t\"Inner\"", output);
    }

    [Fact]
    public void WriteEscapedString_WithSpecialChars_EscapesCorrectly()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings(escapes: true));

        writer.WriteKey("key\n\\");

        Assert.Equal("\"key\\n\\\\\"", sw.ToString());
    }

    [Fact]
    public async Task WriteConditionalAsync_MultipleTokens_WritesCorrectExpression()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());
        var tokens = new List<VConditional.Token>
    {
        new(VConditional.TokenType.Constant, "WIN"),
        new(VConditional.TokenType.Or),
        new(VConditional.TokenType.Not),
        new(VConditional.TokenType.Constant, "OSX")
    };

        await writer.WriteKeyAsync("Key");
        await writer.WriteValueAsync("Value");
        await writer.WriteConditionalAsync(tokens);

        Assert.Contains("[$WIN||!$OSX]", sw.ToString());
    }

    [Fact]
    public void WriteComment_AddsDoubleSlashes()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());

        writer.WriteComment("Hello World");

        Assert.Contains("//Hello World", sw.ToString());
    }

    [Fact]
    public async Task WriteArray_WritesStructuralBrackets()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());

        await writer.WriteArrayStartAsync();
        await writer.WriteValueAsync("item1");
        await writer.WriteArrayEndAsync();

        string output = sw.ToString();
        Assert.StartsWith("[", output);
        Assert.EndsWith("]", output);
    }

    [Fact]
    public void Close_WithCloseOutput_DisposesTextWriter()
    {
        var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings()) { CloseOutput = true };

        writer.Close();

        Assert.Throws<ObjectDisposedException>(() => sw.Write("test"));
    }
    [Fact]
    public async Task WriteEscapedStringAsync_HandlesMultipleSpecialChars()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings(escapes: true));

        await writer.WriteKeyAsync("\tHello\nWorld\\");

        Assert.Equal("\"\\tHello\\nWorld\\\\\"", sw.ToString());
    }

    [Fact]
    public async Task AutoCompleteAsync_AddsAssignmentSpace()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());

        await writer.WriteKeyAsync("Key");
        await writer.WriteValueAsync("Value");

        Assert.Equal("\"Key\" \"Value\"", sw.ToString());
    }

    [Fact]
    public async Task AutoCompleteAsync_IncrementsIndentationAcrossLevels()
    {
        using var sw = new StringWriter { NewLine = "\n" };
        var writer = new VdfTextWriter(sw, GetSettings());

        await writer.WriteObjectStartAsync(); // Level 0 -> 1
        await writer.WriteKeyAsync("K1");
        await writer.WriteValueAsync("V1");
        await writer.WriteObjectStartAsync(); // Level 1 -> 2
        await writer.WriteKeyAsync("K2");
        await writer.WriteValueAsync("V2");
        await writer.WriteObjectEndAsync();   // Level 2 -> 1
        await writer.WriteObjectEndAsync();   // Level 1 -> 0 (triggers State.Finished)

        string output = sw.ToString();
        Assert.Contains("\n\t\t\"K2\"", output);
        Assert.EndsWith("\n}", output.TrimEnd());
    }

    [Fact]
    public async Task DisposeAsync_FlushesAndClosesStream()
    {
        var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings()) { CloseOutput = true };

        await writer.WriteKeyAsync("Test");
        await writer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await writer.WriteKeyAsync("Fail"));
    }

    [Fact]
    public async Task WriteEscapedStringAsync_NoEscapes_WritesRaw()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings(escapes: false));

        await writer.WriteKeyAsync("raw\nstring");

        Assert.Equal("\"raw\nstring\"", sw.ToString());
    }
    [Fact]
    public async Task WriteObject_EndsWithNewlineAtRoot()
    {
        using var sw = new StringWriter { NewLine = "\n" };
        var writer = new VdfTextWriter(sw, GetSettings());

        await writer.WriteKeyAsync("Root");
        await writer.WriteObjectStartAsync();
        await writer.WriteObjectEndAsync();

        Assert.EndsWith("\n", sw.ToString());
    }

    [Fact]
    public void WriteValue_KV3_VerifiesHintPlacement()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings(format: KeyValuesFormat.Kv3));

        writer.WriteValue("true", "bool");

        Assert.Equal("bool:\"true\"", sw.ToString().TrimStart());
    }

    [Fact]
    public async Task WriteKeyAsync_EmptyString_WritesEmptyQuotes()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());

        await writer.WriteKeyAsync("");

        Assert.Equal("\"\"", sw.ToString());
    }


    [Fact]
    public async Task WriteConditionalAsync_TriggersAssignmentBeforeStart()
    {
        using var sw = new StringWriter();
        var writer = new VdfTextWriter(sw, GetSettings());
        var tokens = new List<VConditional.Token> { new(VConditional.TokenType.Constant, "X") };

        await writer.WriteKeyAsync("K");
        await writer.WriteValueAsync("V");
        await writer.WriteConditionalAsync(tokens);

        Assert.Equal("\"K\" \"V\" [$X]", sw.ToString());
    }
}
