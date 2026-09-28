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

        /// <summary>
        /// Many objects of two reference models, shuffled, with repeats and a GlobalId the file does not hold: each
        /// comes back once, in the order first asked for, placed by its own reference model, however many are
        /// converted at once.
        /// </summary>
        [Fact]
        public void ConvertRequests_ManyObjects_ComeBackInTheOrderAsked_EachPlacedByItsReferenceModel()
        {
            using (ManyWallsIfcFile file = new ManyWallsIfcFile(120))
            {
                var frames = new Dictionary<int, ReferenceModelConvert.Frame>
                {
                    [1] = Frame(file.FilePath, 1.0, new TSG.CoordinateSystem()),
                    // 10 m up.
                    [2] = Frame(file.FilePath, 1.0, new TSG.CoordinateSystem(new TSG.Point(0, 0, 10000), new TSG.Vector(1, 0, 0), new TSG.Vector(0, 1, 0))),
                };

                var random = new Random(7);
                List<(int, string)> requests = Enumerable.Range(0, 120)
                    .OrderBy(k => random.Next())
                    .Select(k => (1 + k % 2, ManyWallsIfcFile.Guid(k)))
                    .ToList();
                requests.Insert(10, (1, "0000000000000000000099"));   // not in the file
                requests.Add(requests[5]);                             // the same object again
                requests.Add((3 - requests[7].Item1, requests[7].Item2));   // the same wall through the other reference model

                List<IfcProductGeometry> geometries = ReferenceModelObjectConvert.ConvertRequests(requests, frames);

                List<(int, string)> expected = requests.Where(r => r.Item2.StartsWith("W", StringComparison.Ordinal)).Distinct().ToList();
                Assert.Equal(expected.Select(r => r.Item2), geometries.Select(g => g.GlobalId));

                for (int i = 0; i < expected.Count; i++)
                {
                    // Wall k is x 2k -0.5..+0.5, y -0.25..0.25, z 0..2 m, in millimetres here.
                    int k = int.Parse(expected[i].Item2.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                    double z = expected[i].Item1 == 2 ? 10000 : 0;
                    ReferenceModelConvertTests.AssertBox(geometries[i].BoundingBox, new GeoPoint3(2000 * k - 500, -250, z), new GeoPoint3(2000 * k + 500, 250, z + 2000));
                    Assert.Equal(1e9, geometries[i].TotalVolume, 0);
                }
            }
        }

        /// <summary>
        /// The objects are converted on several threads, and a tolerance scope belongs to the thread that opened it.
        /// Each object is converted under the tolerance in force where ConvertRequests was called all the same, as when
        /// they were all converted there one by one: a face turned round, for one, checks its holes against it. What
        /// each object logs is logged on the thread converting it, so the writer sees the tolerance it had.
        /// </summary>
        [Fact]
        public void ConvertRequests_ConvertsEveryObjectUnderTheToleranceInForceWhereItIsCalled()
        {
            using (ManyWallsIfcFile file = new ManyWallsIfcFile(1))
            {
                var frames = new Dictionary<int, ReferenceModelConvert.Frame> { [1] = Frame(file.FilePath, 1.0, new TSG.CoordinateSystem()) };

                // GlobalIds the file does not hold: each is looked for, and logged, on whichever thread takes it.
                List<(int, string)> requests = Enumerable.Range(0, 200)
                    .Select(k => (1, "M" + k.ToString("D21", System.Globalization.CultureInfo.InvariantCulture)))
                    .ToList();

                var seen = new List<double>();
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) =>
                {
                    if (message.StartsWith("M", StringComparison.Ordinal))
                    {
                        lock (seen)
                        {
                            seen.Add(Tolerance.Global.EqualPoint);
                        }

                        // Long enough for the other threads to take their share rather than the caller all of it.
                        System.Threading.Thread.Sleep(1);
                    }
                };

                try
                {
                    using (Tolerance.Use(new Tolerance(0.25, 0.25)))
                    {
                        ReferenceModelObjectConvert.ConvertRequests(requests, frames);
                    }
                }
                finally
                {
                    GeometryHelperLog.Writer = null;
                }

                Assert.Equal(200, seen.Count);
                Assert.All(seen, equalPoint => Assert.Equal(0.25, equalPoint));
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
