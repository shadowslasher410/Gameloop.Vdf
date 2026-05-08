using System.Text.Json;
using System.Text.Json.Nodes;
using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests
{
    public class VdfJsonConversionTests
    {
        [Fact]
        public void ToJson_SimpleVObject_ReturnsCorrectJsonObject()
        {
            VObject vdf = new()
            {
                { "AppID", new VValue(440) },
                { "Name", new VValue("Team Fortress 2") }
            };

            JsonObject json = vdf.ToJson();

            Assert.Equal(440, json["AppID"]?.GetValue<int>());
            Assert.Equal("Team Fortress 2", json["Name"]?.GetValue<string>());
        }

        [Fact]
        public void ToJson_DuplicateKey_ThrowHandling_ThrowsException()
        {
            VObject vdf = [new VProperty("Dup", new VValue(1)), new VProperty("Dup", new VValue(2))];

            var settings = new VdfJsonConversionSettings { ValueDuplicateKeyHandling = DuplicateKeyHandling.Throw };

            Assert.Throws<VdfException>(() => vdf.ToJson(settings));
        }

        [Fact]
        public void ToJson_DuplicateObject_MergeHandling_MergesCorrectly()
        {
            VObject vdf =
            [
                new VProperty("User", new VObject { { "Name", new VValue("Gabe") } }),
                new VProperty("User", new VObject { { "ID", new VValue(1) } }),
            ];

            var settings = new VdfJsonConversionSettings { ObjectDuplicateKeyHandling = DuplicateKeyHandling.Merge };
            JsonObject json = vdf.ToJson(settings);

            Assert.Equal("Gabe", json["User"]?["Name"]?.GetValue<string>());
            Assert.Equal(1, json["User"]?["ID"]?.GetValue<int>());
        }

        [Fact]
        public void ToVdf_JsonTypes_MapToCorrectVdfValues()
        {
            using JsonDocument doc = JsonDocument.Parse("""
                {
                    "IsActive": true,
                    "Count": 5,
                    "Description": "Test"
                }
                """);

            VObject vdf = (VObject)doc.RootElement.ToVdf();

            Assert.Equal("1", vdf["IsActive"]?.ToString());
            Assert.Equal("5", vdf["Count"]?.ToString());
            Assert.Equal("Test", vdf["Description"]?.ToString());
        }

        [Fact]
        public void ToVdf_JsonArray_MapsToVdfObjectWithIndexKeys()
        {
            using JsonDocument doc = JsonDocument.Parse("[ \"A\", \"B\" ]");

            VObject vdf = (VObject)doc.RootElement.ToVdf(KeyValuesFormat.Kv1);

            Assert.Equal("A", vdf["0"]?.ToString());
            Assert.Equal("B", vdf["1"]?.ToString());
        }

        [Fact]
        public void ToVdf_Kv3Format_AddsTypeHints()
        {
            using JsonDocument doc = JsonDocument.Parse("true");

            VToken vdf = doc.RootElement.ToVdf(KeyValuesFormat.Kv3);

            Assert.IsType<VValue>(vdf);
            Assert.Equal("boolean", ((VValue)vdf).TypeHint);
        }

        [Fact]
        public void Merge_JsonObjects_DeepMergesCorrectly()
        {
            JsonObject target = new()
            {
                ["Level1"] = new JsonObject { ["Key1"] = "Val1" }
            };
            JsonObject source = new()
            {
                ["Level1"] = new JsonObject { ["Key2"] = "Val2" }
            };

            target.Merge(source);

            Assert.Equal("Val1", target["Level1"]?["Key1"]?.GetValue<string>());
            Assert.Equal("Val2", target["Level1"]?["Key2"]?.GetValue<string>());
        }

        [Fact]
        public void Settings_ValueMerge_ThrowsArgumentException()
        {
            var settings = new VdfJsonConversionSettings();
            Assert.Throws<ArgumentException>(() => settings.ValueDuplicateKeyHandling = DuplicateKeyHandling.Merge);
        }
    }
}
