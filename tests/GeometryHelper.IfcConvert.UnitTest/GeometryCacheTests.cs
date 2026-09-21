using System;
using System.IO;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Tests for Geometry Cache (Plan A) correctness and options key differentiation.
    /// </summary>
    public class GeometryCacheTests
    {
        // Minimal IFC box: 1.0 x 0.5 x 2.0 m, unit = METRE
        private const string MinimalIfcBoxStep = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('cache_test.ifc','2026-09-18T00:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0000000000000000000001',$,'CacheTestProject',$,$,$,$,$,#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#4=IFCCARTESIANPOINT((0.,0.));
#5=IFCAXIS2PLACEMENT2D(#4,$);
#6=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCAXIS2PLACEMENT3D(#7,$,$);
#9=IFCDIRECTION((0.,0.,1.));
#10=IFCEXTRUDEDAREASOLID(#6,#8,#9,2.);
#11=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.0001,#8,$);
#12=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#10));
#13=IFCPRODUCTDEFINITIONSHAPE($,$,(#12));
#14=IFCLOCALPLACEMENT($,#8);
#15=IFCWALL('0000000000000000000002',$,'CacheWall',$,$,#14,#13,$,$);
ENDSEC;
END-ISO-10303-21;
";

        private const string WallGuid = "0000000000000000000002";

        [Collection("IfcEngine")]
        private class InnerCacheFixture { }

        [Fact]
        [Trait("Category", "Cache")]
        public void GetGeometry_CalledTwice_ReturnsSameObjectFromCache()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cache_same_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    // Two calls with the same guid + default options
                    IfcProductGeometry geom1 = model.GetGeometry(WallGuid);
                    IfcProductGeometry geom2 = model.GetGeometry(WallGuid);

                    // Must be the exact same object reference (cache hit)
                    Assert.Same(geom1, geom2);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "Cache")]
        public void GetGeometry_DifferentOptions_ReturnsDifferentObjects()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cache_diff_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    var opts1 = new IfcConvertOptions { ScaleFactor = 1.0 };
                    var opts2 = new IfcConvertOptions { ScaleFactor = 1000.0 };

                    IfcProductGeometry geom1 = model.GetGeometry(WallGuid, opts1);
                    IfcProductGeometry geom2 = model.GetGeometry(WallGuid, opts2);

                    // Different options => different cache keys => different converted objects
                    Assert.NotSame(geom1, geom2);
                    // Volume should differ by scale factor^3
                    Assert.NotEqual(geom1.TotalVolume, geom2.TotalVolume, 0);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "Cache")]
        public void GetSolid_CalledTwice_ReturnsSameReferenceThroughCache()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cache_solid_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    // GetSolid internally calls GetCachedGeometry, so second call hits cache
                    GeoSolid3 solid1 = model.GetSolid(WallGuid);
                    GeoSolid3 solid2 = model.GetSolid(WallGuid);

                    Assert.NotNull(solid1);
                    Assert.NotNull(solid2);
                    // Same underlying Solids collection from the same cached IfcProductGeometry
                    Assert.Same(solid1, solid2);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "Options")]
        public void GetCacheKey_SameOptions_ProducesSameKey()
        {
            var opts1 = new IfcConvertOptions
            {
                ScaleFactor = 1.0,
                CoordinateSpace = CoordinateSpace.Global,
                TargetUnit = LengthUnit.Original,
                ApplyVoids = false,
                TessellateNonPlanarFaces = true,
                DeflectionTolerance = 0.5
            };
            var opts2 = new IfcConvertOptions
            {
                ScaleFactor = 1.0,
                CoordinateSpace = CoordinateSpace.Global,
                TargetUnit = LengthUnit.Original,
                ApplyVoids = false,
                TessellateNonPlanarFaces = true,
                DeflectionTolerance = 0.5
            };

            Assert.Equal(opts1.GetCacheKey(), opts2.GetCacheKey());
        }

        [Fact]
        [Trait("Category", "Options")]
        public void GetCacheKey_DifferentOptions_ProducesDifferentKeys()
        {
            var defaults = new IfcConvertOptions();
            // Whichever way ApplyVoids defaults, the other setting must be told apart from it.
            var otherVoids = new IfcConvertOptions { ApplyVoids = !defaults.ApplyVoids };
            var scaled = new IfcConvertOptions { ScaleFactor = 1000.0 };
            var local = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Local };

            // All must be distinct
            Assert.NotEqual(defaults.GetCacheKey(), otherVoids.GetCacheKey());
            Assert.NotEqual(defaults.GetCacheKey(), scaled.GetCacheKey());
            Assert.NotEqual(defaults.GetCacheKey(), local.GetCacheKey());
            Assert.NotEqual(otherVoids.GetCacheKey(), scaled.GetCacheKey());
        }

        [Fact]
        [Trait("Category", "Options")]
        public void GetCacheKey_SkipNames_OrderInsensitive()
        {
            var opts1 = new IfcConvertOptions();
            opts1.SkipNames.Add("C");
            opts1.SkipNames.Add("A");
            opts1.SkipNames.Add("B");

            var opts2 = new IfcConvertOptions();
            opts2.SkipNames.Add("A");
            opts2.SkipNames.Add("B");
            opts2.SkipNames.Add("C");

            // Must be the same regardless of insertion order (sorted internally)
            Assert.Equal(opts1.GetCacheKey(), opts2.GetCacheKey());
        }

        [Fact]
        [Trait("Category", "Options")]
        public void GetCacheKey_OnlyNames_OrderAndCaseInsensitive()
        {
            var opts1 = new IfcConvertOptions { OnlyNames = { "PLATE", "beam", "Column*" } };
            var opts2 = new IfcConvertOptions { OnlyNames = { "column*", "Plate", "BEAM" } };

            // Matching ignores order and case, so these read the same products and may share cached geometry.
            Assert.Equal(opts1.GetCacheKey(), opts2.GetCacheKey());
            Assert.NotEqual(new IfcConvertOptions().GetCacheKey(), opts1.GetCacheKey());
        }

        [Fact]
        [Trait("Category", "Options")]
        public void GetCacheKey_SkipNamesAndOnlyNames_AreKeptApart()
        {
            // Skipping "A" and keeping only "A" give opposite results; so do lists that differ only in how they split.
            Assert.NotEqual(new IfcConvertOptions { SkipNames = { "A" } }.GetCacheKey(), new IfcConvertOptions { OnlyNames = { "A" } }.GetCacheKey());
            Assert.NotEqual(new IfcConvertOptions { OnlyNames = { "A,B" } }.GetCacheKey(), new IfcConvertOptions { OnlyNames = { "A", "B" } }.GetCacheKey());
            Assert.NotEqual(new IfcConvertOptions { SkipNames = { "A,B" } }.GetCacheKey(), new IfcConvertOptions { SkipNames = { "A", "B" } }.GetCacheKey());
        }

        [Fact]
        [Trait("Category", "NullSafety")]
        public void GetSolid_NonExistentGuid_ReturnsNull()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cache_null_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    GeoSolid3 solid = model.GetSolid("GUID_DOES_NOT_EXIST");
                    Assert.Null(solid);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "NullSafety")]
        public void GetGeometry_NullOrEmptyGuid_ReturnsNull()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cache_nullguid_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    Assert.Null(model.GetGeometry(null));
                    Assert.Null(model.GetGeometry(string.Empty));
                    Assert.Null(model.GetGeometry("   "));
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "Catalog")]
        public void GetProductCatalog_EmptyStore_ReturnsEmptyList()
        {
            using (var store = IfcStore.Create(CreateCredentials(), XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            using (var cache = new IfcStoreCache(store))
            {
                var catalog = cache.GetProductCatalog();
                Assert.NotNull(catalog);
                Assert.Empty(catalog);
            }
        }

        [Fact]
        [Trait("Category", "ScaleFactor")]
        public void ScaleFactor_1000_ScalesVolumeByFactor3()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"scale_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    // Default scale: volume in model units (m3) = 1.0
                    var optsDefault = new IfcConvertOptions { ScaleFactor = 1.0 };
                    GeoSolid3 solidMetres = model.GetSolid(WallGuid, optsDefault);
                    Assert.Equal(1.0, solidMetres.Volume, 2);

                    // Scale 1000x (m -> mm): volume = 1.0 * 1000^3 = 1e9
                    var optsMm = new IfcConvertOptions { ScaleFactor = 1000.0 };
                    IfcProductGeometry geomMm = model.GetGeometry(WallGuid, optsMm);
                    Assert.True(geomMm.TotalVolume > 1e8, "Volume scaled to mm^3 must be very large");
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "BoundingBox")]
        public void GetGeometry_BoundingBox_MatchesExpectedDimensions()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"aabb_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    IfcProductGeometry geom = model.GetGeometry(WallGuid);
                    Assert.NotNull(geom);

                    // Box: X [-.5, .5], Y [-.25, .25], Z [0, 2]
                    // (rectangle 1x0.5 centered at origin, extruded 2m along Z)
                    var bb = geom.BoundingBox;
                    Assert.True(bb.Max.Z - bb.Min.Z > 1.5, "Height (Z) should be approximately 2m");
                    Assert.True(bb.Max.X - bb.Min.X > 0.5, "Width (X) should be approximately 1m");
                    Assert.True(bb.Max.Y - bb.Min.Y > 0.2, "Depth (Y) should be approximately 0.5m");
                }
            }
            finally { TryDelete(tempFile); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
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