using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests;

public class VdfSerializerTests
{
    private static VdfSerializerSettings GetSettings(IEnumerable<string>? conditionals = null, KeyValuesFormat format = KeyValuesFormat.Auto)
    {
        return new VdfSerializerSettings
        {
            DefinedConditionals = conditionals?.ToList() ?? [],
            Format = format
        };
    }

    [Fact]
    public void Constructor_InvalidSettings_Throws()
    {
        var settings = new VdfSerializerSettings { UsesConditionals = true, DefinedConditionals = null! };
        Assert.Throws<VdfException>(() => new VdfSerializer(settings));
    }

    [Fact]
    public void Serialize_BasicObject_WritesCorrectFormat()
    {
        var serializer = new VdfSerializer(GetSettings());
        var root = new VProperty("UserConfig", new VObject { { "Resolution", new VValue("1920x1080") } });
        using var writer = new StringWriter();

        serializer.Serialize(writer, root);

        string output = writer.ToString();
        Assert.Contains("\"UserConfig\"", output);
    }

    [Fact]
    public async Task DeserializeAsync_Conditionals_FiltersCorrectKeys()
    {
        var settings = GetSettings(["WINDOWS"]);
        var serializer = new VdfSerializer(settings);

        string vdf = """
        "AppState"
        {
            "WinPath" "C:/Steam" [$WINDOWS]
            "LinPath" "/home/steam" [$LINUX]
        }
        """;
        using var reader = new StringReader(vdf);

        var result = await serializer.DeserializeAsync(reader);
        var obj = (VObject)result.Value;

        // DEBUG: Log exactly which keys were kept
        Console.WriteLine($"Keys in result: {string.Join(", ", obj.Properties().Select(p => p.Key))}");

        // DEBUG: Check the conditional tokens of a property before filtering
        // (You may need to temporarily disable filtering in VdfSerializer to see this)
        foreach (var prop in obj.Properties())
        {
            if (prop.Conditional != null)
            {
                var tokens = string.Join(", ", prop.Conditional.Tokens.Select(t => $"Type: {t.TokenType}, Name: '{t.Name}'"));
                Console.WriteLine($"Key: {prop.Key}, Conditional Tokens: {tokens}");
            }
        }

        Assert.True(obj.ContainsKey("WinPath"), "WinPath was incorrectly filtered out.");
        Assert.False(obj.ContainsKey("LinPath"), "LinPath was incorrectly included.");
    }



    [Fact]
    public async Task DeserializeAsync_KV3TypeHints_ParsesCorrectType()
    {
        var settings = GetSettings(format: KeyValuesFormat.Kv3);
        var serializer = new VdfSerializer(settings);

        string vdf = "\"Data\" { \"Age\" \"int:25\" }";
        using var reader = new StringReader(vdf);

        var result = await serializer.DeserializeAsync(reader);
        var ageValue = (VValue)result.Value["Age"]!;

        Assert.Equal("25", ageValue.Value);
        Assert.Equal("int", ageValue.TypeHint);
    }

    [Fact]
    public async Task SerializeAsync_VValueConditional_WritesCorrectly()
    {
        var serializer = new VdfSerializer(GetSettings());
        var cond = new VConditional
        {
            new VConditional.Token(VConditional.TokenType.Constant, "WIN64")
        };

        var prop = new VProperty("PlatformKey", new VValue("active")) { Conditional = cond };
        var root = new VProperty("Root", new VObject { prop });

        using var writer = new StringWriter();
        await serializer.SerializeAsync(writer, root);

        Assert.Contains("[$WIN64]", writer.ToString());
    }

    [Fact]
    public void Deserialize_NestedObjects_CorrectHierarchy()
    {
        var serializer = new VdfSerializer(GetSettings());
        string vdf = "\"A\" { \"B\" { \"C\" \"D\" } }";
        using var reader = new StringReader(vdf);

        var result = serializer.Deserialize(reader);

        Assert.Equal("D", result.Value["B"]?["C"]?.ToString());
    }

    [Fact]
    public void Settings_Default_HasCorrectValues()
    {
        var settings = VdfSerializerSettings.Default;

        Assert.True(settings.UsesConditionals);
        Assert.False(settings.UsesEscapeSequences);
        Assert.Equal(4096, settings.MaximumTokenSize);
        Assert.Empty(settings.DefinedConditionals!);
    }

    [Fact]
    public void Serializer_Constructor_ThrowsWhenConditionalsMissing()
    {
        Assert.Throws<VdfException>(() => new VdfSerializer(new VdfSerializerSettings
        {
            UsesConditionals = true,
            DefinedConditionals = null
        }));
    }

    [Fact]
    public void Deserialize_SimpleObject_ReturnsCorrectTree()
    {
        string vdf = "\"Root\" { \"Key\" \"Value\" }";
        var serializer = new VdfSerializer(VdfSerializerSettings.Common);

        using var reader = new StringReader(vdf);
        VProperty result = serializer.Deserialize(reader);

        Assert.Equal("Root", result.Key);
        Assert.Equal("Value", result.Value["Key"]?.ToString());
    }

    [Fact]
    public void Deserialize_WithConditionals_FiltersCorrectly()
    {
        var settings = new VdfSerializerSettings
        {
            UsesConditionals = true,
            DefinedConditionals = [VConditional.Windows]
        };
        var serializer = new VdfSerializer(settings);

        string vdf = """
        "Root"
        {
            "WinKey" "Value" [$WINDOWS]
            "LinuxKey" "Value" [$LINUX]
        }
        """;

        using var reader = new StringReader(vdf);

        Console.WriteLine("Starting Deserialization...");
        VProperty result = serializer.Deserialize(reader);
        VObject rootObj = (VObject)result.Value;

        // Debug: List all keys that made it into the final object
        var keys = rootObj.Properties().Select(p => p.Key).ToList();
        Console.WriteLine($"Keys found in Root: {string.Join(", ", keys)}");

        foreach (var prop in rootObj.Properties())
        {
            Console.WriteLine($"Processing Key: {prop.Key}");
            if (prop.Conditional != null)
            {
                var tokens = string.Join(" ", prop.Conditional.Tokens.Select(t => t.Name ?? t.TokenType.ToString()));
                bool eval = prop.Conditional.Evaluate(settings.DefinedConditionals!);
                Console.WriteLine($"  - Conditional Tokens: [{tokens}]");
                Console.WriteLine($"  - Evaluates to: {eval}");
            }
            else
            {
                Console.WriteLine("  - No conditional found for this property.");
            }
        }

        Assert.Contains(rootObj.Properties(), p => p.Key == "WinKey");
        Assert.DoesNotContain(rootObj.Properties(), p => p.Key == "LinuxKey");
    }


    [Fact]
    public void Deserialize_KV3TypeHint_ParsesCorrectly()
    {
        var settings = new VdfSerializerSettings { Format = KeyValuesFormat.Kv3 };
        var serializer = new VdfSerializer(settings);

        string vdf = "\"Root\" { \"Health\" \"int:100\" }";

        using var reader = new StringReader(vdf);
        VProperty result = serializer.Deserialize(reader);

        var obj = Assert.IsType<VObject>(result.Value);
        VValue val = Assert.IsType<VValue>(obj["Health"]);

        Assert.Equal("100", val.Value);
        Assert.Equal("int", val.TypeHint);
    }

    [Fact]
    public void Deserialize_MalformedObject_ThrowsVdfException()
    {
        string vdf = "\"Root\" { \"Key\" \"Value\" ";
        var serializer = new VdfSerializer(VdfSerializerSettings.Common);

        using var reader = new StringReader(vdf);

        var ex = Assert.Throws<VdfException>(() => serializer.Deserialize(reader));
        Assert.Contains("never closed", ex.Message);
    }

    [Fact]
    public async Task DeserializeAsync_ValidStream_ReturnsProperty()
    {
        string vdf = "\"Root\" { \"Key\" \"Value\" }";
        var serializer = new VdfSerializer(VdfSerializerSettings.Common);

        using var reader = new StringReader(vdf);
        VProperty result = await serializer.DeserializeAsync(reader);

        Assert.Equal("Root", result.Key);
        Assert.Equal("Value", result.Value["Key"]?.ToString());
    }

    [Fact]
    public void Settings_Default_ReturnsExpectedValues()
    {
        var settings = VdfSerializerSettings.Default;

        Assert.True(settings.UsesConditionals);
        Assert.False(settings.UsesEscapeSequences);
        Assert.Equal(4096, settings.MaximumTokenSize);
        Assert.Empty(settings.DefinedConditionals!);
    }

    [Fact]
    public void Settings_Common_ReturnsExpectedValues()
    {
        var settings = VdfSerializerSettings.Common;

        Assert.False(settings.UsesConditionals);
        Assert.True(settings.UsesEscapeSequences);
        Assert.Empty(settings.DefinedConditionals!);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10000)]
    public void MaximumTokenSize_ValidValue_SetsSuccessfully(int size)
    {
        var settings = new VdfSerializerSettings { MaximumTokenSize = size };
        Assert.Equal(size, settings.MaximumTokenSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaximumTokenSize_InvalidValue_ThrowsArgumentOutOfRangeException(int size)
    {
        var settings = new VdfSerializerSettings();
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaximumTokenSize = size);
    }
}