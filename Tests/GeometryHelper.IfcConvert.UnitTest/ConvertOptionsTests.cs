using System;
using System.IO;
using System.Linq;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Tests for IfcConvertOptions: SkipNames, CoordinateSpace (Local vs Global),
    /// and IfcProductMetadata behavior.
    /// </summary>
    [Collection("IfcEngine")]
    public class ConvertOptionsTests
    {
        // Two walls at different positions
        private const string TwoWallsStep = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('two_walls.ifc','2026-09-18T00:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0000000000000000000001',$,'TwoWalls',$,$,$,$,$,#2);
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
#15=IFCWALL('0000000000000000000002',$,'Wall_A',$,$,#14,#13,$,$);
#16=IFCCARTESIANPOINT((10.,0.,5.));
#17=IFCAXIS2PLACEMENT3D(#16,$,$);
#18=IFCLOCALPLACEMENT($,#17);
#19=IFCWALL('0000000000000000000003',$,'Wall_B',$,$,#18,#13,$,$);
ENDSEC;
END-ISO-10303-21;
";

        private const string WallAGuid = "0000000000000000000002";
        private const string WallBGuid = "0000000000000000000003";

        [Fact]
        [Trait("Category", "SkipNames")]
        public void SkipNames_MatchingName_ReturnsEmptyGeometry()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"skip_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wall = cache.GetProducts<IIfcWall>().FirstOrDefault(w => w.Name?.ToString() == "Wall_A");
                    Assert.NotNull(wall);

                    // Skip "Wall_A" by name
                    var options = new IfcConvertOptions();
                    options.SkipNames.Add("Wall_A");

                    var geom = wall.ToProductGeometry(options);
                    Assert.NotNull(geom);
                    Assert.True(geom.IsEmpty, "Skipped product should produce empty geometry");
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "SkipNames")]
        public void SkipNames_NotMatching_ReturnsNormalGeometry()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"noskip_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wall = cache.GetProducts<IIfcWall>().FirstOrDefault(w => w.Name?.ToString() == "Wall_A");
                    Assert.NotNull(wall);

                    // Skip "Wall_B" — Wall_A should still be converted
                    var options = new IfcConvertOptions();
                    options.SkipNames.Add("Wall_B");

                    var geom = wall.ToProductGeometry(options);
                    Assert.NotNull(geom);
                    Assert.False(geom.IsEmpty, "Non-skipped product should produce geometry");
                    Assert.NotEmpty(geom.Solids);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "OnlyNames")]
        public void OnlyNames_NameNotListed_ReturnsEmptyGeometry()
        {
            RunTwoWalls(cache =>
            {
                var options = new IfcConvertOptions();
                options.OnlyNames.Add("Wall_B");

                Assert.True(cache.GetGeometry(WallAGuid, options).IsEmpty, "A product not named should produce empty geometry");
                Assert.NotEmpty(cache.GetGeometry(WallBGuid, options).Solids);
            });
        }

        [Fact]
        [Trait("Category", "OnlyNames")]
        public void OnlyNames_WildcardIgnoringCase_KeepsTheProductsItMatches()
        {
            RunTwoWalls(cache =>
            {
                var options = new IfcConvertOptions { OnlyNames = { "wall_*" } };

                Assert.NotEmpty(cache.GetGeometry(WallAGuid, options).Solids);
                Assert.NotEmpty(cache.GetGeometry(WallBGuid, options).Solids);
            });
        }

        [Fact]
        [Trait("Category", "OnlyNames")]
        public void OnlyNames_ThroughAddOnlyNames_KeepsTheProductsNamed()
        {
            RunTwoWalls(cache =>
            {
                // As read from a settings file: spaces around the name.
                var options = new IfcConvertOptions().AddOnlyNames(" Wall_B ");

                Assert.True(cache.GetGeometry(WallAGuid, options).IsEmpty);
                Assert.NotEmpty(cache.GetGeometry(WallBGuid, options).Solids);
            });
        }

        [Fact]
        [Trait("Category", "OnlyNames")]
        public void OnlyNames_AndSkipNames_SkipWins()
        {
            RunTwoWalls(cache =>
            {
                var options = new IfcConvertOptions { OnlyNames = { "Wall_*" }, SkipNames = { "Wall_A" } };

                Assert.True(cache.GetGeometry(WallAGuid, options).IsEmpty);
                Assert.NotEmpty(cache.GetGeometry(WallBGuid, options).Solids);
            });
        }

        [Fact]
        [Trait("Category", "OnlyNames")]
        public void OnlyNames_ModelWideAndCached_EachListGetsItsOwnGeometry()
        {
            RunTwoWalls(cache =>
            {
                // The same product read with two lists: the cache must not hand one list's result to the other.
                Assert.True(cache.GetGeometry(WallAGuid, new IfcConvertOptions { OnlyNames = { "Wall_B" } }).IsEmpty);
                Assert.NotEmpty(cache.GetGeometry(WallAGuid, new IfcConvertOptions { OnlyNames = { "Wall_A" } }).Solids);
                Assert.True(cache.GetGeometry(WallAGuid, new IfcConvertOptions { SkipNames = { "Wall_A" } }).IsEmpty);

                // Model-wide, every product is still visited, and those not named come back empty.
                var byGuid = cache.EnumerateGeometries(new IfcConvertOptions { OnlyNames = { "Wall_B" } }).ToDictionary(g => g.GlobalId);
                Assert.True(byGuid[WallAGuid].IsEmpty);
                Assert.NotEmpty(byGuid[WallBGuid].Solids);
            });
        }

        [Theory]
        [Trait("Category", "OnlyNames")]
        [InlineData("Bolt assembly", new string[0], true)]
        [InlineData("Bolt assembly", null, true)]
        [InlineData("Bolt assembly", new[] { "BEAM", "PLATE" }, false)]
        [InlineData("PLATE", new[] { "BEAM", "PLATE" }, true)]
        [InlineData("plate", new[] { "BEAM", "PLATE" }, true)]
        [InlineData("GWPPLATE", new[] { "*PLATE" }, true)]
        [InlineData("GWPPLATE", new[] { "PLATE" }, false)]
        [InlineData(null, new[] { "BEAM" }, false)]
        [InlineData("", new[] { "BEAM" }, false)]
        [InlineData(null, new[] { "*" }, true)]
        [InlineData("", new[] { "*" }, true)]
        public void IsNameIncluded_MatchesLikeSkipNames(string name, string[] onlyNames, bool expected)
        {
            Assert.Equal(expected, ProductConvert.IsNameIncluded(name, onlyNames));
        }

        private static void RunTwoWalls(Action<IfcStoreCache> test)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"only_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    test(cache);
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "CoordinateSpace")]
        public void CoordinateSpace_Global_AppliesPlacementTranslation()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"global_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wallB = cache.GetProducts<IIfcWall>().FirstOrDefault(w => w.Name?.ToString() == "Wall_B");
                    Assert.NotNull(wallB);

                    var optsGlobal = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Global };
                    var geomGlobal = wallB.ToProductGeometry(optsGlobal);

                    Assert.NotNull(geomGlobal);
                    Assert.NotEmpty(geomGlobal.Solids);

                    // Wall_B is placed at (10, 0, 5) — centroid X should be near 10
                    var centroid = geomGlobal.Solids[0].Centroid;
                    Assert.True(centroid.X > 5.0, $"Global X centroid should be ~10, but was {centroid.X}");
                    Assert.True(centroid.Z > 1.0, $"Global Z centroid should be ~6, but was {centroid.Z}");
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "CoordinateSpace")]
        public void CoordinateSpace_Local_DoesNotApplyPlacementTranslation()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"local_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wallB = cache.GetProducts<IIfcWall>().FirstOrDefault(w => w.Name?.ToString() == "Wall_B");
                    Assert.NotNull(wallB);

                    var optsLocal = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Local };
                    var geomLocal = wallB.ToProductGeometry(optsLocal);

                    Assert.NotNull(geomLocal);
                    Assert.NotEmpty(geomLocal.Solids);

                    // In Local space, the geometry is not displaced by the (10,0,5) placement
                    // centroid X should be near 0 (centered on X axis in local space)
                    var centroid = geomLocal.Solids[0].Centroid;
                    Assert.True(Math.Abs(centroid.X) < 2.0, $"Local X centroid should be near 0, but was {centroid.X}");
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "CoordinateSpace")]
        public void CoordinateSpace_Global_Vs_Local_DifferentCentroids()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"gvsl_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    // Wall_B placed at (10, 0, 5) — Global and Local centroids must differ
                    var optsGlobal = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Global };
                    var optsLocal = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Local };

                    GeoSolid3 globalSolid = model.GetSolid(WallBGuid, optsGlobal);
                    GeoSolid3 localSolid  = model.GetSolid(WallBGuid, optsLocal);

                    Assert.NotNull(globalSolid);
                    Assert.NotNull(localSolid);

                    // The X centroids must differ because placement offset is (10, 0, 5)
                    double deltaX = Math.Abs(globalSolid.Centroid.X - localSolid.Centroid.X);
                    Assert.True(deltaX > 5.0, $"Centroid X difference between Global and Local should be large (>5), was {deltaX}");
                }
            }
            finally { TryDelete(tempFile); }
        }

        // ─── IfcProductMetadata ───────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "Metadata")]
        public void IfcProductMetadata_Constructor_StoresAllFields()
        {
            var meta = new IfcProductMetadata("GUID-001", "BeamA", "IfcBeam", "B1");

            Assert.Equal("GUID-001", meta.GlobalId);
            Assert.Equal("BeamA", meta.Name);
            Assert.Equal("IfcBeam", meta.IfcType);
            Assert.Equal("B1", meta.Tag);
        }

        [Fact]
        [Trait("Category", "Metadata")]
        public void IfcProductMetadata_NullArguments_DefaultToEmpty()
        {
            var meta = new IfcProductMetadata(null, null, null);

            Assert.Equal(string.Empty, meta.GlobalId);
            Assert.Equal(string.Empty, meta.Name);
            Assert.Equal(string.Empty, meta.IfcType);
            Assert.Equal(string.Empty, meta.Tag);
        }

        [Fact]
        [Trait("Category", "Metadata")]
        public void IfcProductMetadata_ToString_ContainsTypeAndName()
        {
            var meta = new IfcProductMetadata("G1", "Wall_01", "IfcWall");
            string str = meta.ToString();

            Assert.Contains("IfcWall", str);
            Assert.Contains("Wall_01", str);
        }

        [Fact]
        [Trait("Category", "Catalog")]
        public void GetProductCatalog_MultipleElements_ContainsCorrectMetadata()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"catalog_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    var catalog = model.GetProductCatalog();
                    Assert.Equal(2, catalog.Count);

                    var names = catalog.Select(m => m.Name).OrderBy(n => n).ToList();
                    Assert.Equal("Wall_A", names[0]);
                    Assert.Equal("Wall_B", names[1]);

                    // All entries must have IfcType = "IfcWall"
                    Assert.All(catalog, m => Assert.Equal("IfcWall", m.IfcType));

                    // All GlobalIds must be non-empty and unique
                    var guids = catalog.Select(m => m.GlobalId).ToList();
                    Assert.All(guids, g => Assert.NotEmpty(g));
                    Assert.Equal(guids.Count, guids.Distinct().Count());
                }
            }
            finally { TryDelete(tempFile); }
        }

        [Fact]
        [Trait("Category", "Catalog")]
        public void EnumerateGeometries_AllProductsReturnNonNullGeometry()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"enum_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, TwoWallsStep);

            try
            {
                using (var model = IfcStoreCache.Open(tempFile))
                {
                    var geometries = model.EnumerateGeometries().ToList();
                    Assert.Equal(2, geometries.Count);
                    Assert.All(geometries, g => Assert.NotNull(g));
                    Assert.All(geometries, g => Assert.Equal("IfcWall", g.IfcType));
                }
            }
            finally { TryDelete(tempFile); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}