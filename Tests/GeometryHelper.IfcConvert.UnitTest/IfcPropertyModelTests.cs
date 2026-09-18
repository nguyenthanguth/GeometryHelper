using System.Collections.Generic;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Tests for IfcPropertyValue and IfcPropertySet models (Plan G - Property Access).
    /// These tests are pure unit tests and do not require a real IFC file or engine.
    /// </summary>
    public class IfcPropertyModelTests
    {
        // ─── IfcPropertyValue ───────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertyValue_Constructor_StoresAllFields()
        {
            var pv = new IfcPropertyValue("Thickness", 0.25, "IfcLengthMeasure", "m");

            Assert.Equal("Thickness", pv.Name);
            Assert.Equal(0.25, pv.Value);
            Assert.Equal("IfcLengthMeasure", pv.TypeName);
            Assert.Equal("m", pv.Unit);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertyValue_NullArguments_DefaultsToEmpty()
        {
            var pv = new IfcPropertyValue(null, null, null, null);

            Assert.Equal(string.Empty, pv.Name);
            Assert.Null(pv.Value);
            Assert.Equal(string.Empty, pv.TypeName);
            Assert.Equal(string.Empty, pv.Unit);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertyValue_ToString_FormatsCorrectly()
        {
            var pv = new IfcPropertyValue("Width", 0.5, "IfcLengthMeasure", "m");
            string str = pv.ToString();

            Assert.Contains("Width", str);
            Assert.Contains("0.5", str);
            Assert.Contains("m", str);
            Assert.Contains("IfcLengthMeasure", str);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertyValue_ToString_NoUnit_ExcludesUnitSection()
        {
            var pv = new IfcPropertyValue("IsExternal", true, "IfcBoolean");
            string str = pv.ToString();

            // No trailing unit should appear (unit is empty)
            Assert.Contains("IsExternal", str);
            Assert.Contains("True", str);
            Assert.DoesNotContain(" []", str); // Should not have empty unit bracket
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertyValue_BooleanValue_StoresAsObject()
        {
            var pv = new IfcPropertyValue("IsExternal", true, "IfcBoolean");
            Assert.True((bool)pv.Value);
        }

        // ─── IfcPropertySet ──────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_Constructor_StoresProperties()
        {
            var props = new[]
            {
                new IfcPropertyValue("IsExternal", true, "IfcBoolean"),
                new IfcPropertyValue("LoadBearing", false, "IfcBoolean"),
                new IfcPropertyValue("FireRating", "REI90", "IfcLabel"),
            };

            var pset = new IfcPropertySet("Pset_WallCommon", props);

            Assert.Equal("Pset_WallCommon", pset.Name);
            Assert.Equal(3, pset.Properties.Count);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_TryGetProperty_CaseInsensitive()
        {
            var props = new[] { new IfcPropertyValue("IsExternal", true, "IfcBoolean") };
            var pset = new IfcPropertySet("Pset_WallCommon", props);

            // Exact case
            Assert.True(pset.TryGetProperty("IsExternal", out var val1));
            Assert.NotNull(val1);

            // Lower case
            Assert.True(pset.TryGetProperty("isexternal", out var val2));
            Assert.NotNull(val2);

            // Upper case
            Assert.True(pset.TryGetProperty("ISEXTERNAL", out var val3));
            Assert.NotNull(val3);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_TryGetProperty_MissingKey_ReturnsFalse()
        {
            var pset = new IfcPropertySet("Pset_WallCommon", new[]
            {
                new IfcPropertyValue("IsExternal", true, "IfcBoolean")
            });

            bool found = pset.TryGetProperty("NonExistentProperty", out var val);

            Assert.False(found);
            Assert.Null(val);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_NullProperties_DoesNotThrow()
        {
            var pset = new IfcPropertySet("EmptyPset", (IEnumerable<IfcPropertyValue>)null);

            Assert.Equal("EmptyPset", pset.Name);
            Assert.Empty(pset.Properties);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_NullPropertyValues_AreSkipped()
        {
            var props = new IfcPropertyValue[]
            {
                new IfcPropertyValue("Valid", 1.0, "IfcReal"),
                null,
                null,
                new IfcPropertyValue("Also_Valid", "X", "IfcLabel"),
            };

            var pset = new IfcPropertySet("TestPset", props);

            Assert.Equal(2, pset.Properties.Count);
            Assert.True(pset.Properties.ContainsKey("Valid"));
            Assert.True(pset.Properties.ContainsKey("Also_Valid"));
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_DuplicateNames_LastWins()
        {
            // When two properties have the same name, the last one should win (dict overwrite)
            var props = new[]
            {
                new IfcPropertyValue("Thickness", 0.1, "IfcLengthMeasure", "m"),
                new IfcPropertyValue("Thickness", 0.25, "IfcLengthMeasure", "m"),
            };

            var pset = new IfcPropertySet("Pset_WallCommon", props);

            Assert.Single(pset.Properties);
            pset.TryGetProperty("Thickness", out var val);
            Assert.Equal(0.25, val.Value); // Last value wins
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void IfcPropertySet_ToString_ContainsNameAndCount()
        {
            var pset = new IfcPropertySet("Pset_WallCommon", new[]
            {
                new IfcPropertyValue("IsExternal", true, "IfcBoolean"),
                new IfcPropertyValue("FireRating", "REI90", "IfcLabel"),
            });

            string str = pset.ToString();
            Assert.Contains("Pset_WallCommon", str);
            Assert.Contains("2", str);
        }

        // ─── IfcStoreCache.GetProperties integration ──────────────────────────────

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void GetProperties_NullGuid_ReturnsEmptyDictionary()
        {
            using var store = Xbim.Ifc.IfcStore.Create(CreateCredentials(),
                XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
            using var cache = new IfcStoreCache(store);

            var props = cache.GetProperties(null);
            Assert.NotNull(props);
            Assert.Empty(props);

            props = cache.GetProperties(string.Empty);
            Assert.Empty(props);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void GetProperty_NonExistentProduct_ReturnsNull()
        {
            using var store = Xbim.Ifc.IfcStore.Create(CreateCredentials(),
                XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
            using var cache = new IfcStoreCache(store);

            var val = cache.GetProperty("DOES_NOT_EXIST", "Pset_WallCommon", "IsExternal");
            Assert.Null(val);
        }

        [Fact]
        [Trait("Category", "PropertyModel")]
        public void GetProperty_ExistingProductButNoPropertySets_ReturnsNull()
        {
            using (var store = Xbim.Ifc.IfcStore.Create(CreateCredentials(),
                XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            {
                string guid;
                using (var txn = store.BeginTransaction("Add wall"))
                {
                    var wall = store.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    wall.Name = "NakedWall";
                    guid = wall.GlobalId;
                    txn.Commit();
                }

                using (var cache = new IfcStoreCache(store))
                {
                    var props = cache.GetProperties(guid);
                    Assert.NotNull(props);
                    Assert.Empty(props);

                    var val = cache.GetProperty(guid, "Pset_WallCommon", "IsExternal");
                    Assert.Null(val);
                }
            }
        }

        private static XbimEditorCredentials CreateCredentials()
        {
            return new XbimEditorCredentials
            {
                ApplicationDevelopersName = "GeometryHelper",
                ApplicationFullName = "GeometryHelper.UnitTest",
                ApplicationIdentifier = "GH",
                ApplicationVersion = "3.2.0",
                EditorsFamilyName = "Unit",
                EditorsGivenName = "Test",
                EditorsOrganisationName = "GeometryHelper"
            };
        }
    }
}