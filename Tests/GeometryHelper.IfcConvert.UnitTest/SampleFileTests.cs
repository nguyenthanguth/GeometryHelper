using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Tests against the IFC files in TestFiles (xBIM's test models and buildingSMART samples).
    /// Expected volumes are computed by hand from the STEP data of each file.
    /// </summary>
    [Collection("IfcEngine")]
    public class SampleFileTests
    {
        private static string PathOf(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", fileName);

        private static void WithModel(string fileName, Action<IfcStoreCache> test)
        {
            using (IfcStoreCache model = IfcStoreCache.Open(PathOf(fileName)))
            {
                test(model);
            }
        }

        private static double PhysicalVolume(IfcStoreCache model, IfcConvertOptions options = null) =>
            model.GetAllSolids(options).Sum(s => s.Volume);

        private static void AssertRelative(double expected, double actual, double tolerance, string what)
        {
            double error = Math.Abs(actual - expected) / Math.Abs(expected);
            Assert.True(error <= tolerance, $"{what}: {actual:G8} vs expected {expected:G8} ({error:P3} off)");
        }

        // ---------------------------------------------------------------------------------------------
        // 1. Every sample file converts without throwing and yields only sound, positive solids.
        // ---------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> AllSampleFiles() =>
            Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles"), "*.ifc")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new object[] { n });

        [Theory]
        [Trait("Category", "SampleFiles")]
        [MemberData(nameof(AllSampleFiles))]
        public void EverySampleFile_ConvertsToSoundSolids(string fileName)
        {
            WithModel(fileName, model =>
            {
                // ApplyVoids exercises the opening path too; products without openings convert as usual.
                var options = new IfcConvertOptions { ApplyVoids = true, IncludeNonPhysicalProducts = true };
                List<IfcProductGeometry> geometries = model.EnumerateGeometries(options).ToList();

                Assert.Equal(model.ProductCount, geometries.Count);
                foreach (IfcProductGeometry geometry in geometries)
                {
                    Assert.NotNull(geometry.Warnings);
                    foreach (var solid in geometry.Solids)
                    {
                        Assert.True(solid.Volume > 0.0 && !double.IsNaN(solid.Volume) && !double.IsInfinity(solid.Volume),
                            $"{geometry.IfcType} {geometry.GlobalId}: volume {solid.Volume}");
                        Assert.True(solid.GetSignedVolume() > 0.0,
                            $"{geometry.IfcType} {geometry.GlobalId}: faces point inwards");
                    }
                }
            });
        }

        // ---------------------------------------------------------------------------------------------
        // 2. Exact volumes from the STEP data.
        // ---------------------------------------------------------------------------------------------

        [Theory]
        // IfcWall: 5000 x 270 profile extruded 2000 mm.
        [InlineData("wall-extruded-solid.ifc", 5000.0 * 270.0 * 2000.0)]
        // The same 1 x 1 x 2 m block in five representations: extrusion, CSG, B-rep, triangulation, surface model.
        [InlineData("extruded-solid.ifc", 2e9)]
        [InlineData("csg-primitive.ifc", 2e9)]
        [InlineData("brep-model.ifc", 2e9)]
        [InlineData("triangulated-item.ifc", 2e9)]
        [InlineData("surface-model.ifc", 2e9)]
        // Mapped 1000 x 1000 x 2000 block, placed as is.
        [InlineData("mapped-shape-without-transformation.ifc", 2e9)]
        // Same block through a non-uniform operator scaling X and Y by 0.5: 0.25 of the volume.
        [InlineData("mapped-shape-with-transformation.ifc", 5e8)]
        // Four mapped copies, each scaled 0.5 x 0.5 x 1.
        [InlineData("mapped-shape-with-multiple-items.ifc", 4 * 5e8)]
        public void SampleFile_HasExactVolume(string fileName, double expectedVolume)
        {
            WithModel(fileName, model => AssertRelative(expectedVolume, PhysicalVolume(model), 1e-6, fileName));
        }

        [Fact]
        public void MappedItems_ProduceOneSolidPerMapping()
        {
            WithModel("mapped-shape-with-multiple-items.ifc", model => Assert.Equal(4, model.GetAllSolids().Count));
        }

        [Fact]
        public void TargetUnit_ConvertsMillimetreModelToMetres()
        {
            WithModel("wall-extruded-solid.ifc", model =>
            {
                Assert.Equal("MILLIMETRE", model.OriginalLengthUnit);
                AssertRelative(5.0 * 0.27 * 2.0, PhysicalVolume(model, new IfcConvertOptions { TargetUnit = LengthUnit.Meters }), 1e-6, "m3");
            });
        }

        [Fact]
        public void SlabOpenings_CutThroughHoleAndRecess()
        {
            // Slab 200 mm thick; a 100 mm diameter hole through it and a 1000 x 500 x 50 recess in its top.
            double removed = Math.PI * 50.0 * 50.0 * 200.0 + 1000.0 * 500.0 * 50.0;

            WithModel("slab-openings.ifc", model =>
            {
                double whole = model.GetSolidsByType("IfcSlab", new IfcConvertOptions { ApplyVoids = false }).Sum(s => s.Volume);
                var cutGeometry = model.GetGeometriesByType("IfcSlab", new IfcConvertOptions { ApplyVoids = true }).Single();

                Assert.Empty(cutGeometry.Warnings);
                // The round hole is tessellated, so allow half a percent of the removed volume.
                AssertRelative(removed, whole - cutGeometry.TotalVolume, 0.005, "removed volume");
                Assert.All(cutGeometry.Solids, s => Assert.True(s.IsClosed()));
            });
        }

        // ---------------------------------------------------------------------------------------------
        // 3. Different encodings of the same geometry agree.
        // ---------------------------------------------------------------------------------------------

        [Theory]
        // Advanced B-rep (NURBS) against its faceted approximation.
        [InlineData("basin-advanced-brep.ifc", "basin-faceted-brep.ifc", 0.001)]
        // The same cube, IFC4 and IFC4x3 exports.
        [InlineData("cube-advanced-brep.ifc", "Ifc4cube_advanced_brep.ifc", 1e-9)]
        // Curve parameters written in degrees and in radians.
        [InlineData("curve-parameters-in-degrees.ifc", "curve-parameters-in-radians.ifc", 1e-9)]
        // Gauss-Krüger and UTM georeferencing of the same object: large coordinates must not distort it.
        [InlineData("geographic-referencing-gk.ifc", "geographic-referencing-utm.ifc", 1e-9)]
        public void EquivalentEncodings_HaveTheSameVolume(string first, string second, double tolerance)
        {
            double a = 0, b = 0;
            WithModel(first, model => a = PhysicalVolume(model));
            WithModel(second, model => b = PhysicalVolume(model));

            Assert.True(a > 0);
            AssertRelative(a, b, tolerance, first + " vs " + second);
        }

        // ---------------------------------------------------------------------------------------------
        // 4. Units reported from the files.
        // ---------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("wall-extruded-solid.ifc", "MILLIMETRE")]
        [InlineData("P1_cm.ifc", "CENTIMETRE")]
        [InlineData("NewlinesInStrings.ifc", "INCH")]
        [InlineData("cube-advanced-brep.ifc", "METRE")]
        public void OriginalLengthUnit_MatchesFile(string fileName, string unit)
        {
            WithModel(fileName, model => Assert.Equal(unit, model.OriginalLengthUnit));
        }

        // ---------------------------------------------------------------------------------------------
        // 5. Problems found by running every sample file, now reported instead of hidden or fatal.
        // ---------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("linear-placement.ifc")]
        [InlineData("grid-placement.ifc")]
        public void UnsupportedPlacement_IsReported_AndDoesNotStopEnumeration(string fileName)
        {
            WithModel(fileName, model =>
            {
                // Before the fix the first IfcLinearPlacement / IfcGridPlacement threw out of the enumeration.
                var all = model.EnumerateGeometries(new IfcConvertOptions { IncludeNonPhysicalProducts = true }).ToList();

                Assert.Equal(model.ProductCount, all.Count);
                Assert.Contains(all, g => g.Warnings.Any(w => w.Contains("placement cannot be evaluated")));
            });
        }

        [Fact]
        public void FlatBodies_AreDroppedAndReported()
        {
            // Some House.ifc doors carry a four-face body with no thickness beside their leaves.
            WithModel("House.ifc", model =>
            {
                var doors = model.GetGeometriesByType("IfcDoor");

                Assert.Contains(doors, d => d.Warnings.Any(w => w.Contains("enclosing no volume")));
                Assert.All(doors.SelectMany(d => d.Solids), s => Assert.True(s.GetSignedVolume() > 0.0));
            });
        }

        [Fact]
        public void GeometryTheEngineCannotBuild_IsReported()
        {
            // xBIM 5.1 returns an empty, invalid shape for this IfcRevolvedAreaSolid.
            WithModel("beam-revolved-solid.ifc", model =>
            {
                var beam = model.GetGeometriesByType("IfcBeam").Single();

                Assert.False(beam.HasSolids);
                Assert.Contains(beam.Warnings, w => w.Contains("IfcRevolvedAreaSolid") && w.Contains("invalid"));
            });
        }

        [Fact]
        public void HealthySample_HasNoWarnings()
        {
            WithModel("wall-extruded-solid.ifc", model =>
            {
                Assert.All(model.EnumerateGeometries(), g => Assert.Empty(g.Warnings));
            });
        }
    }
}
