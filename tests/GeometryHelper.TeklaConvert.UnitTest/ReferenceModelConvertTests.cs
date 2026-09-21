using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// The parts of <see cref="ReferenceModelConvert"/> that need no running Tekla: the options, the file path, the
    /// placement and the IFC side. Reading a reference model itself can only be checked inside Tekla.
    /// </summary>
    [Collection("IfcEngine")]
    public class ReferenceModelConvertTests
    {
        [Fact]
        public void CreateOptions_WithoutOptions_UsesMillimetresTheScaleAndTheTeklaDefaults()
        {
            IfcConvertOptions options = ReferenceModelConvert.CreateOptions(2.5, null, false);

            Assert.Equal(LengthUnit.Millimeters, options.TargetUnit);
            Assert.Equal(2.5, options.ScaleFactor);
            Assert.Equal(CoordinateSpace.Global, options.CoordinateSpace);

            // Openings are left uncut for speed; a selected assembly still returns its parts.
            Assert.False(options.ApplyVoids);
            Assert.True(options.IncludeAggregatedParts);
        }

        [Fact]
        public void CreateOptions_KeepsTheCallersSettingsAndLeavesTheCallersObjectAlone()
        {
            Tolerance tolerance = new Tolerance(2e-4, 3e-4, 0.02, 4e-4);
            IfcConvertOptions caller = new IfcConvertOptions
            {
                Tolerance = tolerance,
                SkipNames = { "Skip*" },
                OnlyNames = { "BEAM", "PLATE" },
                TargetUnit = LengthUnit.Original,
                ScaleFactor = 7.0,
                CoordinateSpace = CoordinateSpace.Local,
                ApplyVoids = false,
                IncludeAggregatedParts = false,
                IncludeNonPhysicalProducts = true,
                TessellateNonPlanarFaces = false,
                DeflectionTolerance = 3.0,
            };

            IfcConvertOptions options = ReferenceModelConvert.CreateOptions(0.5, caller, false);

            // What belongs to the reference model.
            Assert.Equal(LengthUnit.Millimeters, options.TargetUnit);
            Assert.Equal(0.5, options.ScaleFactor);
            Assert.Equal(CoordinateSpace.Global, options.CoordinateSpace);

            // Everything else is the caller's.
            Assert.False(options.ApplyVoids);
            Assert.False(options.IncludeAggregatedParts);
            Assert.True(options.IncludeNonPhysicalProducts);
            Assert.False(options.TessellateNonPlanarFaces);
            Assert.Equal(3.0, options.DeflectionTolerance);
            Assert.Equal(tolerance, options.Tolerance);
            Assert.Equal(new[] { "Skip*" }, options.SkipNames);
            Assert.Equal(new[] { "BEAM", "PLATE" }, options.OnlyNames.OrderBy(n => n));

            // And the caller's object is a copy away.
            Assert.NotSame(caller, options);
            Assert.Equal(LengthUnit.Original, caller.TargetUnit);
            Assert.Equal(7.0, caller.ScaleFactor);
            Assert.Equal(CoordinateSpace.Local, caller.CoordinateSpace);
        }

        [Fact]
        public void CreateOptions_ForAWholeModel_LeavesAggregatedPartsOut()
        {
            Assert.False(ReferenceModelConvert.CreateOptions(1.0, null, true).IncludeAggregatedParts);
            Assert.False(ReferenceModelConvert.CreateOptions(1.0, new IfcConvertOptions { IncludeAggregatedParts = true }, true).IncludeAggregatedParts);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-2.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void CreateOptions_AScaleThatIsNotPositive_FallsBackToOne(double scale)
        {
            Assert.Equal(1.0, ReferenceModelConvert.CreateOptions(scale, null, false).ScaleFactor);
        }

        [Theory]
        [InlineData(@"C:\Models\House", @"..\Reference\Building.ifc", @"C:\Models\Reference\Building.ifc")]
        [InlineData(@"C:\Models\House", @"Building.IFC", @"C:\Models\House\Building.IFC")]
        [InlineData(@"C:\Models\House", @"D:\Shared\Site.ifczip", @"D:\Shared\Site.ifczip")]
        [InlineData(@"C:\Models\House", @"\\server\share\Bridge.ifcxml", @"\\server\share\Bridge.ifcxml")]
        [InlineData(@"C:\Models\House", @"C:/Models/House/./Sub/../Building.ifc", @"C:\Models\House\Building.ifc")]
        public void ResolveIfcFilePath_ResolvesAgainstTheModelFolder(string modelPath, string activeFilePath, string expected)
        {
            Assert.Equal(expected, ReferenceModelConvert.ResolveIfcFilePath(modelPath, activeFilePath));
        }

        [Theory]
        [InlineData(@"C:\Models\House", @"Plan.dwg")]
        [InlineData(@"C:\Models\House", @"Scan.skp")]
        [InlineData(@"C:\Models\House", @"NoExtension")]
        [InlineData(@"C:\Models\House", "")]
        [InlineData(@"C:\Models\House", null)]
        [InlineData("", @"Building.ifc")]
        [InlineData(null, @"Building.ifc")]
        [InlineData(@"C:\Models\House", "Bad|Name.ifc")]
        public void ResolveIfcFilePath_WithoutAnIfcFile_ReturnsNull(string modelPath, string activeFilePath)
        {
            Assert.Null(ReferenceModelConvert.ResolveIfcFilePath(modelPath, activeFilePath));
        }

        [Fact]
        public void ComposeIfcToWorkPlane_MatchesWhatTeklaDoesWithTheSameFrames()
        {
            // A reference model inserted at (1000, 2000, 300) and turned 90 degrees about Z ...
            TSG.CoordinateSystem frame = new TSG.CoordinateSystem(
                new TSG.Point(1000, 2000, 300), new TSG.Vector(0, 1, 0), new TSG.Vector(-1, 0, 0));

            // ... read while the work plane is that of a part standing on a diagonal. The TransformationMatrixToLocal
            // of that plane is MatrixFactory.ToCoordinateSystem of its frame; a TransformationPlane itself cannot be
            // made without a running Tekla (its constructor goes through Tekla's remoting proxy).
            TSG.CoordinateSystem part = new TSG.CoordinateSystem(
                new TSG.Point(-500, 700, 100), new TSG.Vector(1, 1, 0), new TSG.Vector(0, 0, 1));
            TSG.Matrix globalToWorkPlane = TSG.MatrixFactory.ToCoordinateSystem(part);
            TSG.Matrix ifcToGlobal = TSG.MatrixFactory.FromCoordinateSystem(frame);

            GeoTransform3 composed = ReferenceModelConvert.ComposeIfcToWorkPlane(frame, globalToWorkPlane);

            foreach (TSG.Point ifcPoint in new[] { new TSG.Point(0, 0, 0), new TSG.Point(500, -250, 2000), new TSG.Point(-1234.5, 678.9, -42) })
            {
                TSG.Point expected = globalToWorkPlane.Transform(ifcToGlobal.Transform(ifcPoint));
                GeoPoint3 actual = composed.Transform(ifcPoint.ToGeoPoint3());

                Assert.Equal(expected.X, actual.X, 6);
                Assert.Equal(expected.Y, actual.Y, 6);
                Assert.Equal(expected.Z, actual.Z, 6);
            }
        }

        [Fact]
        public void TransformGeometry_CarriesBodiesSurfacesAndPlacement_AndKeepsTheProduct()
        {
            GeoSolid3 tetrahedron = Tetrahedron(100.0);
            GeoFace3 surface = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)));
            GeoTransform3 placement = GeoTransform3.Translation(new GeoVector3(5, 6, 7));
            IfcProductGeometry original = new IfcProductGeometry(
                "2O2Fr$t4X7Zf8NOew3FNr2", "Wall", "IfcWall", new[] { tetrahedron }, new[] { surface }, placement, "T1", new[] { "#12 IfcExtrudedAreaSolid: note" });

            // Turned 90 degrees about Z, then moved.
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(1000, -2000, 300)) * GeoTransform3.RotationZ(Math.PI / 2.0);

            IfcProductGeometry moved = ReferenceModelConvert.TransformGeometry(original, move, Tolerance.Global);

            Assert.Equal(original.GlobalId, moved.GlobalId);
            Assert.Equal(original.Name, moved.Name);
            Assert.Equal(original.IfcType, moved.IfcType);
            Assert.Equal(original.Tag, moved.Tag);
            Assert.Equal(original.Warnings, moved.Warnings);
            Assert.Single(moved.Solids);
            Assert.Single(moved.OpenSurfaces);

            // (100, 0, 0) turns to (0, 100, 0) and moves to (1000, -1900, 300).
            Assert.Contains(Vertices(moved.Solids[0]), v => v.IsEqualTo(new GeoPoint3(1000, -1900, 300)));
            Assert.Equal(original.Solids[0].Volume, moved.Solids[0].Volume, 6);
            Assert.True(moved.Placement.IsEqualTo(move * placement));

            // The geometry it came from, which may sit in the cache, is untouched.
            Assert.Same(tetrahedron, original.Solids[0]);
            Assert.Contains(Vertices(original.Solids[0]), v => v.IsEqualTo(new GeoPoint3(100, 0, 0)));
        }

        [Fact]
        public void ConvertWholeModel_ReturnsEveryPhysicalProductScaledAndPlaced()
        {
            using (TwoWallsIfcFile file = new TwoWallsIfcFile())
            {
                IfcStoreCache cache = ReferenceModelConvert.TryOpen(file.FilePath);
                Assert.NotNull(cache);

                IfcConvertOptions options = ReferenceModelConvert.CreateOptions(2.0, null, true);
                GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(1000, 2000, 0));

                List<IfcProductGeometry> geometries = ReferenceModelConvert.ConvertWholeModel(cache, options, move);

                Assert.NotNull(geometries);
                Assert.Equal(new[] { TwoWallsIfcFile.WallA, TwoWallsIfcFile.WallB }, geometries.Select(g => g.GlobalId).OrderBy(g => g));

                // 1 x 0.5 x 2 m at scale 2, in millimetres: x -1000..1000, y -500..500, z 0..4000, then moved.
                IfcProductGeometry wallA = geometries.Single(g => g.GlobalId == TwoWallsIfcFile.WallA);
                AssertBox(wallA.BoundingBox, new GeoPoint3(0, 1500, 0), new GeoPoint3(2000, 2500, 4000));
                Assert.Equal(8e9, wallA.TotalVolume, 0);

                // Wall_B sits at (10, 0, 5) m, so 20 m and 10 m away at scale 2.
                IfcProductGeometry wallB = geometries.Single(g => g.GlobalId == TwoWallsIfcFile.WallB);
                AssertBox(wallB.BoundingBox, new GeoPoint3(20000, 1500, 10000), new GeoPoint3(22000, 2500, 14000));
            }
        }

        [Fact]
        public void TryOpen_AFileThatIsMissingOrNotIfc_ReturnsNull()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"teklaconvert_missing_{Guid.NewGuid():N}.ifc");
            Assert.Null(ReferenceModelConvert.TryOpen(missing));

            string broken = Path.Combine(Path.GetTempPath(), $"teklaconvert_broken_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(broken, "This is not an IFC file.");
            try
            {
                Assert.Null(ReferenceModelConvert.TryOpen(broken));
            }
            finally
            {
                IfcStoreCache.ClearGlobalCache(broken);
                File.Delete(broken);
            }
        }

        [Fact]
        public void TryOpen_AFileThatCannotBeRead_IsLoggedAsAWarning()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"teklaconvert_missing_{Guid.NewGuid():N}.ifc");

            using (LogCapture log = new LogCapture())
            {
                Assert.Null(ReferenceModelConvert.TryOpen(missing));

                var entry = Assert.Single(log.Entries);
                Assert.Equal(GeometryHelperLogLevel.Warn, entry.Level);
                Assert.Contains(missing, entry.Message);
                Assert.NotNull(entry.Exception);
            }
        }

        [Fact]
        public void TransformGeometry_ABodyThatCannotBeRebuilt_IsLeftOutAndLogged()
        {
            IfcProductGeometry original = new IfcProductGeometry("2O2Fr$t4X7Zf8NOew3FNr2", "Plate", "IfcPlate", new[] { Tetrahedron(100.0) });

            // Shrunk a million times, its faces fall below the smallest area a polygon may have.
            GeoTransform3 collapse = GeoTransform3.Scaling(1e-6);

            using (LogCapture log = new LogCapture())
            {
                IfcProductGeometry moved = ReferenceModelConvert.TransformGeometry(original, collapse, Tolerance.Global);

                // Left out, and said so both in the product's warnings and in the log.
                Assert.Empty(moved.Solids);
                string warning = Assert.Single(moved.Warnings);
                Assert.Contains("collapsed", warning);

                var entry = Assert.Single(log.Entries);
                Assert.Equal(GeometryHelperLogLevel.Warn, entry.Level);
                Assert.Contains("2O2Fr$t4X7Zf8NOew3FNr2", entry.Message);
                Assert.Contains(warning, entry.Message);
            }
        }

        [Fact]
        public void TransformGeometry_ABodyWithASliverFace_IsKeptWhole()
        {
            // A 100 mm cube whose top carries a sliver triangle of 5e-5 mm2, which the conversion keeps: it builds
            // polygons with an area threshold of EqualPoint squared (1e-8 mm2) rather than EqualVector (1e-4).
            // GeoSolid3.TransformBy checks against Tolerance.Global again and threw on the whole body, which cost
            // 3 of 500 beams of a real Tekla model read with ApplyVoids = true.
            GeoSolid3 box = SliverBox();
            IfcProductGeometry original = new IfcProductGeometry("1eLFzR00fm8Z4tDJ0qCJ0m", "GIRDER", "IfcBeam", new[] { box });
            Assert.Throws<ArgumentException>(() => box.TransformBy(GeoTransform3.Identity));

            IfcConvertOptions options = ReferenceModelConvert.CreateOptions(1.0, null, false);
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(12000, 34000, 5000)) * GeoTransform3.RotationZ(Math.PI / 6.0);

            using (LogCapture log = new LogCapture())
            {
                IfcProductGeometry moved = ReferenceModelConvert.TransformGeometry(original, move, options.Tolerance);

                GeoSolid3 body = Assert.Single(moved.Solids);
                Assert.Equal(box.Faces.Count, body.Faces.Count);
                Assert.Equal(1e6, body.GetSignedVolume(), 3);
                Assert.Empty(moved.Warnings);
                Assert.Empty(log.Entries);
            }
        }

        [Fact]
        public void ResolveIfcFilePath_APathWindowsDoesNotAccept_IsLogged()
        {
            using (LogCapture log = new LogCapture())
            {
                Assert.Null(ReferenceModelConvert.ResolveIfcFilePath(@"C:\Models\House", "Bad|Name.ifc"));

                Assert.Contains(log.Entries, e => e.Level == GeometryHelperLogLevel.Warn && e.Message.Contains("Bad|Name.ifc"));
            }
        }

        internal static IEnumerable<GeoPoint3> Vertices(GeoSolid3 solid)
        {
            return solid.Faces.SelectMany(f => f.Boundary.Vertices);
        }

        internal static void AssertBox(GeoAabb3 box, GeoPoint3 min, GeoPoint3 max)
        {
            Assert.True(box.Min.IsEqualTo(min, new Tolerance(1e-6, 1e-6)), $"min {box.Min} is not {min}");
            Assert.True(box.Max.IsEqualTo(max, new Tolerance(1e-6, 1e-6)), $"max {box.Max} is not {max}");
        }

        // A 100 mm cube whose top is split into a sliver triangle 100 mm long and 1e-6 mm wide, built as the
        // conversion builds it, and the rest. Faces wound so that their normals point out of the body.
        private static GeoSolid3 SliverBox()
        {
            Tolerance construction = new Tolerance(Tolerance.DefaultEqualPoint, Tolerance.DefaultEqualPoint * Tolerance.DefaultEqualPoint);

            GeoPoint3 b0 = new GeoPoint3(0, 0, 0), b1 = new GeoPoint3(100, 0, 0), b2 = new GeoPoint3(100, 100, 0), b3 = new GeoPoint3(0, 100, 0);
            GeoPoint3 t0 = new GeoPoint3(0, 0, 100), t1 = new GeoPoint3(100, 0, 100), t2 = new GeoPoint3(100, 100, 100), t3 = new GeoPoint3(0, 100, 100);
            GeoPoint3 m = new GeoPoint3(50, 1e-6, 100);

            return new GeoSolid3(new[]
            {
                new GeoFace3(new GeoPolygon3(b0, b3, b2, b1)),
                new GeoFace3(new GeoPolygon3(b0, b1, t1, t0)),
                new GeoFace3(new GeoPolygon3(b1, b2, t2, t1)),
                new GeoFace3(new GeoPolygon3(b2, b3, t3, t2)),
                new GeoFace3(new GeoPolygon3(b3, b0, t0, t3)),
                new GeoFace3(new GeoPolygon3(new[] { t0, t1, m }, construction)),
                new GeoFace3(new GeoPolygon3(t0, m, t1, t2, t3)),
            });
        }

        // Faces wound so that their normals point out of the body.
        private static GeoSolid3 Tetrahedron(double size)
        {
            GeoPoint3 a = new GeoPoint3(0, 0, 0);
            GeoPoint3 b = new GeoPoint3(size, 0, 0);
            GeoPoint3 c = new GeoPoint3(0, size, 0);
            GeoPoint3 d = new GeoPoint3(0, 0, size);

            return new GeoSolid3(new[]
            {
                new GeoFace3(new GeoPolygon3(a, c, b)),
                new GeoFace3(new GeoPolygon3(a, b, d)),
                new GeoFace3(new GeoPolygon3(a, d, c)),
                new GeoFace3(new GeoPolygon3(b, c, d)),
            });
        }
    }
}
