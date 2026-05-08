using Gameloop.Vdf.Linq;
using System.Text;

namespace Gameloop.Vdf.Tests;

public class VdfConvertTests
{
    private const string SampleVdf = "\"Root\"\n{\n\t\"Key\" \"Value\"\n}";

    [Fact]
    public void Serialize_ValidToken_ReturnsFormattedVdfString()
    {
        var root = new VProperty("Root", new VObject
        {
            { "Key", new VValue("Value") }
        });

        string result = VdfConvert.Serialize(root);

        Assert.Contains("\"Root\"", result);
        Assert.Contains("\"Key\"", result);
        Assert.Contains("\"Value\"", result);
    }

    [Fact]
    public void Serialize_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VdfConvert.Serialize(null!));
    }

    [Fact]
    public void Deserialize_ValidString_ReturnsCorrectVProperty()
    {
        VProperty result = VdfConvert.Deserialize(SampleVdf);

        Assert.Equal("Root", result.Key);
        Assert.IsType<VObject>(result.Value);
        Assert.Equal("Value", result.Value["Key"]?.ToString());
    }

    [Fact]
    public void Deserialize_FromStream_WorksCorrectly()
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(SampleVdf);
        using var stream = new MemoryStream(byteArray);

        VProperty result = VdfConvert.Deserialize(stream);

        Assert.Equal("Root", result.Key);
        Assert.Equal("Value", result.Value["Key"]?.ToString());
    }

    [Fact]
    public void Deserialize_TextReader_ReturnsVProperty()
    {
        using var reader = new StringReader(SampleVdf);
        VProperty result = VdfConvert.Deserialize(reader);

        Assert.NotNull(result);
        Assert.Equal("Root", result.Key);
    }

    [Fact]
    public void Deserialize_NullReader_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VdfConvert.Deserialize((TextReader)null!));
        Assert.Throws<ArgumentNullException>(() => VdfConvert.Deserialize((Stream)null!));
    }

    [Fact]
    public void DeserializeObject_ValidVdf_ReturnsUnwrappedVObject()
    {
        VObject result = VdfConvert.DeserializeObject(SampleVdf);

        Assert.NotNull(result);
        Assert.Equal("Value", result["Key"]?.ToString());
    }

    [Fact]
    public void DeserializeObject_WhenRootIsNotObject_ThrowsVdfException()
    {
        string invalidRootVdf = "\"Root\" \"Value\"";

        Assert.Throws<VdfException>(() => VdfConvert.DeserializeObject(invalidRootVdf));
    }

    [Fact]
    public void VdfException_InitializesWithCorrectData()
    {
        string message = "Test error";
        var inner = new Exception("Inner error");

        var ex = new VdfException(message, inner);

        Assert.Equal(message, ex.Message);
        Assert.Equal(inner, ex.InnerException);
        Assert.IsType<Exception>(ex, exactMatch: false);
    }

    [Fact]
    public void VdfException_WithMessageOnly_SetsDefaultsCorrectly()
    {
        string message = "An error occurred.";
        var ex = new VdfException(message);

        Assert.Equal(message, ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void VdfException_IsCatchable()
    {
        static void ThrowIt() => throw new VdfException("Expected");

        var ex = Assert.Throws<VdfException>(ThrowIt);
        Assert.Equal("Expected", ex.Message);
    }
}