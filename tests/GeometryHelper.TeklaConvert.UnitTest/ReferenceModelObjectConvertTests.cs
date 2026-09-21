using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// The IFC side of <see cref="ReferenceModelObjectConvert"/>: frames and GlobalIds as the Tekla side would read
    /// them, and a real IFC file.
    /// </summary>
    [Collection("IfcEngine")]
    public class ReferenceModelObjectConvertTests
    {
        // The identity work plane: TransformationMatrixToLocal of the global plane.
        private static readonly TSG.Matrix Global = TSG.MatrixFactory.ToCoordinateSystem(new TSG.CoordinateSystem());

        private static ReferenceModelConvert.Frame Frame(string file, double scale, TSG.CoordinateSystem insertedAt)
        {
            return new ReferenceModelConvert.Frame(
                file,
                ReferenceModelConvert.CreateOptions(scale, null, false),
                ReferenceModelConvert.ComposeIfcToWorkPlane(insertedAt, Global));
        }

        [Fact]
        public void ConvertRequests_OneFileInsertedTwice_PlacesTheObjectOncePerReferenceModel()
        {
            using (TwoWallsIfcFile file = new TwoWallsIfcFile())
            {
                var frames = new Dictionary<int, ReferenceModelConvert.Frame>
                {
                    // At (1000, 2000, 0), turned 90 degrees about Z, full size.
                    [1] = Frame(file.FilePath, 1.0, new TSG.CoordinateSystem(new TSG.Point(1000, 2000, 0), new TSG.Vector(0, 1, 0), new TSG.Vector(-1, 0, 0))),
                    // 10 m up, at twice the size.
                    [2] = Frame(file.FilePath, 2.0, new TSG.CoordinateSystem(new TSG.Point(0, 0, 10000), new TSG.Vector(1, 0, 0), new TSG.Vector(0, 1, 0))),
                };

                List<IfcProductGeometry> geometries = ReferenceModelObjectConvert.ConvertRequests(
                    new[] { (1, TwoWallsIfcFile.WallA), (2, TwoWallsIfcFile.WallA) }, frames);

                Assert.Equal(2, geometries.Count);
                Assert.All(geometries, g => Assert.Equal(TwoWallsIfcFile.WallA, g.GlobalId));

                // Wall_A is x -500..500, y -250..250, z 0..2000 mm; turned, it is x -250..250, y -500..500.
                ReferenceModelConvertTests.AssertBox(geometries[0].BoundingBox, new GeoPoint3(750, 1500, 0), new GeoPoint3(1250, 2500, 2000));
                ReferenceModelConvertTests.AssertBox(geometries[1].BoundingBox, new GeoPoint3(-1000, -500, 10000), new GeoPoint3(1000, 500, 14000));
                Assert.Equal(1e9, geometries[0].TotalVolume, 0);
                Assert.Equal(8e9, geometries[1].TotalVolume, 0);
            }
        }

        [Fact]
        public void ConvertRequests_LeavesOutWhatCannotBeRead_AndKeepsTheOrderGiven()
        {
            using (TwoWallsIfcFile file = new TwoWallsIfcFile())
            {
                string missing = Path.Combine(Path.GetTempPath(), $"teklaconvert_missing_{Guid.NewGuid():N}.ifc");
                TSG.CoordinateSystem origin = new TSG.CoordinateSystem();
                var frames = new Dictionary<int, ReferenceModelConvert.Frame>
                {
                    [1] = Frame(file.FilePath, 1.0, origin),
                    [3] = Frame(missing, 1.0, origin),
                };

                var requests = new (int, string)[]
                {
                    (1, TwoWallsIfcFile.WallB),
                    (1, "0000000000000000000099"),   // not in the file
                    (1, TwoWallsIfcFile.WallA),
                    (1, TwoWallsIfcFile.WallB),      // the same object again
                    (2, TwoWallsIfcFile.WallA),      // a reference model with no frame (not IFC)
                    (3, TwoWallsIfcFile.WallA),      // a file that cannot be read
                    (1, null),                       // an object with no GlobalId
                };

                List<IfcProductGeometry> geometries = ReferenceModelObjectConvert.ConvertRequests(requests, frames);

                Assert.Equal(new[] { TwoWallsIfcFile.WallB, TwoWallsIfcFile.WallA }, geometries.Select(g => g.GlobalId));
                Assert.All(geometries, g => Assert.Single(g.Solids));
            }
        }

        [Fact]
        public void ConvertRequests_WhatIsLeftOut_IsLogged()
        {
            using (TwoWallsIfcFile file = new TwoWallsIfcFile())
            using (LogCapture log = new LogCapture())
            {
                string missing = Path.Combine(Path.GetTempPath(), $"teklaconvert_missing_{Guid.NewGuid():N}.ifc");
                TSG.CoordinateSystem origin = new TSG.CoordinateSystem();
                var frames = new Dictionary<int, ReferenceModelConvert.Frame>
                {
                    [1] = Frame(file.FilePath, 1.0, origin),
                    [3] = Frame(missing, 1.0, origin),
                };

                ReferenceModelObjectConvert.ConvertRequests(new[] { (1, "0000000000000000000099"), (3, TwoWallsIfcFile.WallA) }, frames);

                // A GlobalId the file does not hold is an ordinary miss; a file that cannot be read is worth a warning.
                Assert.Contains(log.Entries, e => e.Level == GeometryHelperLogLevel.Debug
                    && e.Message.Contains("0000000000000000000099") && e.Message.Contains(Path.GetFileName(file.FilePath)));
                Assert.Contains(log.Entries, e => e.Level == GeometryHelperLogLevel.Warn && e.Message.Contains(missing));
            }
        }

        [Fact]
        public void ConvertRequests_ReturnsTheWholeProduct_WithItsPlacementCarriedAlong()
        {
            using (TwoWallsIfcFile file = new TwoWallsIfcFile())
            {
                TSG.CoordinateSystem insertedAt = new TSG.CoordinateSystem(new TSG.Point(0, 0, 500), new TSG.Vector(1, 0, 0), new TSG.Vector(0, 1, 0));
                var frames = new Dictionary<int, ReferenceModelConvert.Frame> { [1] = Frame(file.FilePath, 1.0, insertedAt) };

                IfcProductGeometry wallB = ReferenceModelObjectConvert.ConvertRequests(new[] { (1, TwoWallsIfcFile.WallB) }, frames).Single();

                Assert.Equal("Wall_B", wallB.Name);
                Assert.Equal("IfcWall", wallB.IfcType);

                // Wall_B's own frame is at (10, 0, 5) m; the reference model lifts it by 500 mm.
                GeoPoint3 origin = wallB.Placement.Transform(new GeoPoint3(0, 0, 0));
                Assert.True(origin.IsEqualTo(new GeoPoint3(10000, 0, 5500)), $"placement origin {origin}");
            }
        }
    }
}
