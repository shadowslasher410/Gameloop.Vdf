using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests
{
    public class VTokenTests
    {

        [Fact]
        public void Children_OfType_FiltersCorrectly()
        {
            var obj = new VObject
            {
                new VProperty("p1", new VValue(1)),
                new VValue("orphan_value")
            };

            var propertiesOnly = obj.Children<VProperty>();

            Assert.Single(propertiesOnly);
            Assert.IsType<VProperty>(propertiesOnly.First());
        }

        [Fact]
        public void Value_Generic_TraversesAndConverts()
        {
            var obj = new VObject
            {
                { "AppID", new VValue("440") }
            };
            int? result = obj.Value<int>("AppID");

            Assert.Equal(440, result);
        }

        [Fact]
        public void Value_Generic_ReturnsDefaultOnMissingKey()
        {
            var obj = new VObject();
            int? result = obj.Value<int?>("Missing");

            Assert.Null(result);
        }

        [Fact]
        public void IsContainer_Extension_ReturnsCorrectValues()
        {
            Assert.True(VTokenType.Object.IsContainer());
            Assert.True(VTokenType.Property.IsContainer());
            Assert.False(VTokenType.Value.IsContainer());
            Assert.False(VTokenType.None.IsContainer());
        }

        [Fact]
        public void DeepEquals_Static_HandlesNullAndReferenceEquality()
        {
            VToken? t1 = null;
            VToken? t2 = null;
            VToken t3 = new VValue("test");

            Assert.True(VToken.DeepEquals(t1, t2));
            Assert.True(VToken.DeepEquals(t3, t3));
            Assert.False(VToken.DeepEquals(t1, t3));
        }

        [Fact]
        public void Indexer_BaseImplementation_ThrowsInvalidOperationException()
        {
            var val = new VValue("test");

            var ex = Assert.Throws<InvalidOperationException>(() => val["key"]);
            Assert.Contains("Cannot access child value on VValue", ex.Message);
        }

        [Fact]
        public void IEnumerable_Implementation_IteratesChildren()
        {
            var obj = new VObject
            {
                new VValue(1),
                new VValue(2)
            };

            int count = 0;
            foreach (var child in obj)
                count++;

            Assert.Equal(2, count);
        }
        [Fact]
        public void Root_ReturnsTopLevelAncestor()
        {
            var rootObj = new VObject();
            var prop = new VProperty("key", new VValue("val"));
            rootObj.Add(prop);

            Assert.Same(rootObj, prop.Value.Root);
            Assert.Same(rootObj, rootObj.Root);
        }

        [Fact]
        public void Path_GeneratesCorrectDotNotation()
        {
            var root = new VObject();
            var childObj = new VObject();
            var leafProp = new VProperty("Health", new VValue(100));
            var playerProp = new VProperty("Player", childObj);
            root.Add(playerProp);
            childObj.Add(leafProp);

            Assert.Equal("Player", childObj.Path);
            Assert.Equal("Player.Health", leafProp.Value.Path);
            Assert.Equal(string.Empty, root.Path);
        }

        [Fact]
        public void Children_OfType_ReturnsFilteredResults()
        {
            var obj = new VObject
            {
                new VProperty("p1", new VValue(1)),
                new VValue("just_a_value")
            };

            var propsOnly = obj.Children<VProperty>();

            Assert.Single(propsOnly);
            Assert.Equal("p1", propsOnly.First().Key);
        }

        [Fact]
        public void Value_T_RetrievesAndConverts()
        {
            var obj = new VObject
            {
                { "AppID", new VValue("440") }
            };

            int? appId = obj.Value<int>("AppID");

            Assert.Equal(440, appId);
        }

        [Fact]
        public void DeepEquals_Static_HandlesNulls()
        {
            VToken? t1 = null;
            VToken? t2 = null;
            VToken t3 = new VValue("val");

            Assert.True(VToken.DeepEquals(t1, t2));
            Assert.False(VToken.DeepEquals(t1, t3));
        }

        [Fact]
        public void Indexer_OnBaseToken_ThrowsInvalidOperationException()
        {
            var val = new VValue("test");

            Assert.Throws<InvalidOperationException>(() => val["anything"]);
        }
    }
}
