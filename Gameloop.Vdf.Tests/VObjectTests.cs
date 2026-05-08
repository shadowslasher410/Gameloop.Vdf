using Gameloop.Vdf.Linq;
using System.Dynamic;

namespace Gameloop.Vdf.Tests
{
    public class VObjectTests
    {
        [Fact]
        public void Add_UpdatesPointersCorrectly()
        {
            var obj = new VObject();
            var child1 = new VValue("first");
            var child2 = new VValue("second");

            obj.Add(child1);
            obj.Add(child2);

            Assert.Equal(obj, child1.Parent);
            Assert.Equal(obj, child2.Parent);
            Assert.Equal(child2, child1.Next);
            Assert.Equal(child1, child2.Previous);
            Assert.Null(child1.Previous);
            Assert.Null(child2.Next);
        }

        [Fact]
        public void RemoveAt_ClearsPointersAndHealsChain()
        {
            var obj = new VObject();
            var child1 = new VValue("1");
            var child2 = new VValue("2");
            var child3 = new VValue("3");
            obj.Add(child1);
            obj.Add(child2);
            obj.Add(child3);

            obj.RemoveAt(1);

            Assert.Null(child2.Parent);
            Assert.Null(child2.Next);
            Assert.Null(child2.Previous);

            Assert.Equal(child3, child1.Next);
            Assert.Equal(child1, child3.Previous);
        }

        [Fact]
        public void DictionaryAccess_GetsAndSetsProperties()
        {
            var obj = new VObject
            {
                ["name"] = new VValue("valve")
            };

            Assert.True(obj.ContainsKey("name"));
            Assert.Equal("valve", ((VValue)obj["name"]!).Value);
        }

        [Fact]
        public void Indexer_OverwritesExistingProperty()
        {
            var obj = new VObject
            {
                { "key", new VValue("old") }
            };
            obj["key"] = new VValue("new");

            Assert.Single(obj.Properties());
            Assert.Equal("new", ((VValue)obj["key"]!).Value);
        }

        [Fact]
        public void DeepClone_CopiesAllChildrenWithNewPointers()
        {
            var original = new VObject();
            var child = new VValue("data");
            original.Add(child);

            var clone = (VObject)original.DeepClone();
            var clonedChild = clone[0];

            Assert.NotSame(original, clone);
            Assert.NotSame(child, clonedChild);
            Assert.Equal(clone, clonedChild.Parent);
            Assert.Equal("data", ((VValue)clonedChild).Value);
        }

        [Fact]
        public void TryGetValue_ReturnsTrueIfKeyExists()
        {
            var obj = new VObject { { "key", new VValue("val") } };

            bool found = obj.TryGetValue("key", out var result);

            Assert.True(found);
            Assert.NotNull(result);
        }

        [Fact]
        public void Interface_IDictionary_KeysAndValues_MatchProperties()
        {
            IDictionary<string, VToken> dict = new VObject
            {
                { "k1", new VValue("v1") },
                { "k2", new VValue("v2") }
            };

            Assert.Equal(2, dict.Keys.Count);
            Assert.Contains("k1", dict.Keys);
            Assert.Equal(2, dict.Values.Count);
        }

        [Fact]
        public void Insert_MaintainsPointerIntegrity()
        {
            var obj = new VObject();
            var first = new VValue("first");
            var last = new VValue("last");
            var middle = new VValue("middle");

            obj.Add(first);
            obj.Add(last);
            obj.Insert(1, middle);

            Assert.Equal(middle, first.Next);
            Assert.Equal(last, middle.Next);
            Assert.Equal(middle, last.Previous);
        }

        [Fact]
        public void Clear_DetachesAllChildren()
        {
            var obj = new VObject();
            var child = new VValue("temp");
            obj.Add(child);

            obj.Clear();

            Assert.Empty(obj);
            Assert.Null(child.Parent);
        }
    }
    public class DynamicMetaObjectTests
    {
        [Fact]
        public void Dynamic_SetMember_UpdatesInternalState()
        {
            VObject vobj = [];
            dynamic dObj = vobj;

            dObj.FriendCode = 12345;

            Assert.True(vobj.ContainsKey("FriendCode"));
            Assert.Equal(12345, vobj["FriendCode"]!.Value<int>());
        }

        [Fact]
        public void Dynamic_GetMember_ReturnsExistingVToken()
        {
            VObject vobj = new()
            {
                { "Name", new VValue("Gabe") }
            };
            dynamic dObj = vobj;

            var result = dObj.Name;

            Assert.IsType<VValue>(result);
            Assert.Equal("Gabe", ((VValue)result).Value);
        }

        [Fact]
        public void Dynamic_SetMember_SupportsNestedDynamicAssignment()
        {
            dynamic dObj = new VObject();
            dObj.Settings = new VObject();

            dObj.Settings.Resolution = "1920x1080";

            VObject vobj = (VObject)dObj;
            VObject settings = (VObject)vobj["Settings"]!;
            Assert.Equal("1920x1080", settings["Resolution"].Value<string>());
        }


        [Fact]
        public void Dynamic_GetMember_ReturnsNullForMissingProperty()
        {
            dynamic dObj = new VObject();

            var result = dObj.MissingProperty;

            Assert.Null(result);
        }

        [Fact]
        public void Dynamic_MemberNames_AreDiscoverable()
        {
            VObject vobj = new()
            {
                { "KeyA", new VValue(1) },
                { "KeyB", new VValue(2) }
            };
            IDynamicMetaObjectProvider provider = vobj;

            var metaObject = provider.GetMetaObject(System.Linq.Expressions.Expression.Constant(vobj));
            var memberNames = metaObject.GetDynamicMemberNames();

            Assert.Contains("KeyA", memberNames);
            Assert.Contains("KeyB", memberNames);
        }
    }

    public class VObjectDynamicProxyTests
    {
        [Fact]
        public void DynamicSet_AddsNewProperty()
        {
            dynamic obj = new VObject();

            obj.Version = "1.0";

            var vobj = (VObject)obj;
            Assert.True(vobj.ContainsKey("Version"));
            Assert.Equal("1.0", ((VValue)vobj["Version"]!).Value);
        }

        [Fact]
        public void DynamicGet_RetrievesExistingProperty()
        {
            var vobj = new VObject
            {
                { "AppID", new VValue(440) }
            };

            dynamic obj = vobj;
            var value = obj.AppID;

            Assert.IsType<VValue>(value);
            Assert.Equal(440, ((VValue)value).Value);
        }

        [Fact]
        public void DynamicSet_WrapsNonVTokenInVValue()
        {
            dynamic obj = new VObject();
            obj.Health = 100;

            var vobj = (VObject)obj;
            Assert.IsType<VValue>(vobj["Health"]);
            Assert.Equal(100, ((VValue)vobj["Health"]!).Value);
        }

        [Fact]
        public void DynamicGet_NonExistentMember_ReturnsNull()
        {
            dynamic obj = new VObject();
            var result = obj.MissingKey;

            Assert.Null(result);
        }

        [Fact]
        public void GetDynamicMemberNames_ReturnsAllPropertyKeys()
        {
            var vobj = new VObject
            {
                { "Key1", new VValue(1) },
                { "Key2", new VValue(2) }
            };

            var metaObject = ((IDynamicMetaObjectProvider)vobj)
                .GetMetaObject(System.Linq.Expressions.Expression.Constant(vobj));

            IEnumerable<string> names = metaObject.GetDynamicMemberNames();

            Assert.Contains("Key1", names);
            Assert.Contains("Key2", names);
            Assert.Equal(2, names.Count());
        }
    }

    public class  VDynamicObjectProxyTests
    {
        [Fact]
        public void DynamicSet_CreatesNewVValue_ForPrimitiveTypes()
        {
            dynamic obj = new VObject();

            obj.AppId = 440;

            var vobj = (VObject)obj;
            Assert.True(vobj.ContainsKey("AppId"));
            Assert.IsType<VValue>(vobj["AppId"]);
            Assert.Equal(440, ((VValue)vobj["AppId"]!).Value);
        }

        [Fact]
        public void DynamicSet_AddsVToken_Directly()
        {
            dynamic obj = new VObject();
            var innerObj = new VObject();

            obj.Settings = innerObj;

            var vobj = (VObject)obj;
            Assert.Same(innerObj, vobj["Settings"]);
        }

        [Fact]
        public void DynamicGet_ReturnsNull_ForMissingMember()
        {
            dynamic obj = new VObject();
            var result = obj.NonExistentProperty;
            Assert.Null(result);
        }

        [Fact]
        public void DynamicGet_RetrievesExistingValue()
        {
            var vobj = new VObject
            {
                { "Version", new VValue(3) }
            };
            dynamic obj = vobj;

            var result = obj.Version;

            Assert.IsType<VValue>(result);
            Assert.Equal(3, ((VValue)result).Value);
        }

        [Fact]
        public void GetDynamicMemberNames_ReturnsAllKeys()
        {
            var vobj = new VObject
            {
                { "Key1", new VValue(1) },
                { "Key2", new VValue(2) }
            };

            var provider = (IDynamicMetaObjectProvider)vobj;
            var meta = provider.GetMetaObject(System.Linq.Expressions.Expression.Constant(vobj));

            var names = meta.GetDynamicMemberNames().ToList();

            Assert.Equal(2, names.Count);
            Assert.Contains("Key1", names);
            Assert.Contains("Key2", names);
        }

        [Fact]
        public void DynamicSet_DeepAssignment_WorksRecursively()
        {
            dynamic obj = new VObject();
            obj.User = new VObject();

            obj.User.Name = "Valve";

            var vobj = (VObject)obj;
            var user = (VObject)vobj["User"]!;
            Assert.Equal("Valve", ((VValue)user["Name"]!).Value);
        }
    }
}
