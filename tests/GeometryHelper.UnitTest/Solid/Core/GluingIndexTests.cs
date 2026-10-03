using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Gluing cells back into a body used to compare every face with every face and every edge with every edge;
    /// it now looks only at the ones an index says could match. Each step is checked here against the way it was
    /// done before, copied below as it stood, on the faces and edges that cutting a body really produces: the
    /// answer must be the same, in the same order, to the last bit.
    /// </summary>
    public class GluingIndexTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// The cells of a body cut the way its openings are cut in: every face plane of a few cutters splits the
        /// cells whose box meets the cutter's box, and only those — so the wall between a cell that was split and
        /// one that was not is divided differently on its two sides.
        /// </summary>
        private static List<GeoSolid3> Cells(int seed)
        {
            var rng = new Random(seed);
            GeoSolid3 body = Box(0, 0, 0, 200, 150, 40);

            if (rng.NextDouble() < 0.5)
            {
                body = body.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(100, 75, 20), new GeoVector3(rng.NextDouble(), rng.NextDouble(), 1), rng.NextDouble()));
            }

            GeoPoint3 centre = body.GetAabb().Center;
            var cells = new List<GeoSolid3> { body };
            int cutters = rng.Next(1, 4);

            for (int c = 0; c < cutters; c++)
            {
                var at = new GeoPoint3(centre.X + rng.Next(-70, 71), centre.Y + rng.Next(-50, 51), centre.Z);
                GeoSolid3 cutter = rng.NextDouble() < 0.5
                    ? GeoSolid3.Cylinder(at.Add(new GeoVector3(0, 0, -60)), at.Add(new GeoVector3(rng.Next(-10, 11), rng.Next(-10, 11), 60)), rng.Next(5, 20), 8 + 2 * rng.Next(0, 5))
                    : Box(at.X - rng.Next(5, 30), at.Y - rng.Next(5, 30), at.Z - 60, at.X + rng.Next(5, 30), at.Y + rng.Next(5, 30), at.Z + 60);

                GeoAabb3 reach = cutter.GetAabb();

                foreach (GeoPlane3 plane in Splition3.CollectFacePlanes(cutter, Tol))
                {
                    var divided = new List<GeoSolid3>();

                    foreach (GeoSolid3 cell in cells)
                    {
                        if (cell.GetAabb().CollidesWith(reach, Tol) && Splition3.TrySplitBy(cell, plane, out GeoSolid3 above, out GeoSolid3 below, Tol))
                        {
                            divided.Add(above);
                            divided.Add(below);
                        }
                        else
                        {
                            divided.Add(cell);
                        }
                    }

                    cells = divided;
                }
            }

            return cells;
        }

        #region The way it was done before, as it stood

        private static List<GeoFace3> DropMatchedPairsAsBefore(List<GeoFace3> faces)
        {
            bool[] dropped = new bool[faces.Count];

            for (int i = 0; i < faces.Count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                for (int j = i + 1; j < faces.Count; j++)
                {
                    if (dropped[j])
                    {
                        continue;
                    }

                    if (faces[i].Boundary.IsEqualTo(faces[j].Boundary.Flip(), Tol))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
                        break;
                    }
                }
            }

            return faces.Where((face, i) => !dropped[i]).ToList();
        }

        private static List<GeoFace3> CancelBackToBackAsBefore(List<GeoFace3> faces)
        {
            int count = faces.Count;
            var normals = new GeoVector3[count];
            var boxes = new GeoAabb3[count];

            for (int i = 0; i < count; i++)
            {
                normals[i] = faces[i].Boundary.Normal;
                boxes[i] = faces[i].GetAabb();
            }

            double speck = Tol.EqualPoint * Tol.EqualPoint;
            List<int>[] against = null;

            for (int i = 0; i < count; i++)
            {
                GeoPlane3 plane = faces[i].GetPlane();

                for (int j = i + 1; j < count; j++)
                {
                    if (normals[i].DotProduct(normals[j]) >= 0.0
                        || !boxes[i].CollidesWith(boxes[j], Tol)
                        || !Boolean3.LiesIn(plane, faces[j], Tol)
                        || Boolean3.Intersect(faces[i], faces[j], Tol).Sum(f => f.Area) <= speck)
                    {
                        continue;
                    }

                    against = against ?? new List<int>[count];
                    (against[i] = against[i] ?? new List<int>()).Add(j);
                    (against[j] = against[j] ?? new List<int>()).Add(i);
                }
            }

            if (against == null)
            {
                return faces;
            }

            var kept = new List<GeoFace3>(count);

            for (int i = 0; i < count; i++)
            {
                if (against[i] == null)
                {
                    kept.Add(faces[i]);
                    continue;
                }

                var left = new List<GeoFace3> { faces[i] };

                foreach (int j in against[i])
                {
                    var rest = new List<GeoFace3>();

                    foreach (GeoFace3 piece in left)
                    {
                        rest.AddRange(Boolean3.Subtract(piece, faces[j], Tol));
                    }

                    left = rest;
                }

                foreach (GeoFace3 piece in left)
                {
                    if (piece.Area > speck)
                    {
                        kept.Add(piece);
                    }
                }
            }

            return kept;
        }

        private static void CancelOpposedEdgesAsBefore(List<GeoLine3> edges)
        {
            var ends = new List<GeoPoint3>(edges.Count * 2);

            foreach (GeoLine3 edge in edges)
            {
                ends.Add(edge.StartPoint);
                ends.Add(edge.EndPoint);
            }

            var resolved = new List<GeoLine3>(edges.Count);
            var cuts = new List<double>();

            foreach (GeoLine3 edge in edges)
            {
                cuts.Clear();

                foreach (GeoPoint3 end in ends)
                {
                    if (Containment3.IsPointOn(edge, end, Tol))
                    {
                        cuts.Add(Parametrization3.GetDistanceAtPoint(edge, end));
                    }
                }

                resolved.AddRange(Splition3.SplitAtDistances(edge, cuts, Tol));
            }

            edges.Clear();
            edges.AddRange(resolved);

            bool[] dropped = new bool[edges.Count];

            for (int i = 0; i < edges.Count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (dropped[j])
                    {
                        continue;
                    }

                    if (edges[i].StartPoint.IsEqualTo(edges[j].EndPoint, Tol) &&
                        edges[i].EndPoint.IsEqualTo(edges[j].StartPoint, Tol))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
                        break;
                    }
                }
            }

            for (int i = edges.Count - 1; i >= 0; i--)
            {
                if (dropped[i])
                {
                    edges.RemoveAt(i);
                }
            }
        }

        #endregion

        private static void AssertSameFaces(List<GeoFace3> expected, List<GeoFace3> actual, string where)
        {
            Assert.True(expected.Count == actual.Count, $"{where}: {expected.Count} faces before, {actual.Count} now");

            for (int k = 0; k < expected.Count; k++)
            {
                Assert.True(expected[k].Boundary.Vertices.SequenceEqual(actual[k].Boundary.Vertices), $"{where}: face {k} differs");
                Assert.Equal(expected[k].Holes.Count, actual[k].Holes.Count);

                for (int h = 0; h < expected[k].Holes.Count; h++)
                {
                    Assert.True(expected[k].Holes[h].Vertices.SequenceEqual(actual[k].Holes[h].Vertices), $"{where}: face {k}, hole {h} differs");
                }
            }
        }

        private static void AssertSameEdges(List<GeoLine3> expected, List<GeoLine3> actual, string where)
        {
            Assert.True(expected.Count == actual.Count, $"{where}: {expected.Count} edges before, {actual.Count} now");

            for (int k = 0; k < expected.Count; k++)
            {
                Assert.True(expected[k].StartPoint.Equals(actual[k].StartPoint) && expected[k].EndPoint.Equals(actual[k].EndPoint), $"{where}: edge {k} differs");
            }
        }

        /// <summary>
        /// The edges of each group of faces sharing a plane, walked with the material to the left, as merging the
        /// faces of a glued body reads them.
        /// </summary>
        private static IEnumerable<List<GeoLine3>> CoplanarEdgeGroups(List<GeoFace3> faces)
        {
            var planes = new List<GeoPlane3>();
            var groups = new List<List<GeoFace3>>();

            foreach (GeoFace3 face in faces)
            {
                GeoPlane3 plane = face.GetPlane();
                int group = planes.FindIndex(p => p.IsEqualTo(plane, Tol));

                if (group < 0)
                {
                    planes.Add(plane);
                    groups.Add(new List<GeoFace3>());
                    group = groups.Count - 1;
                }

                groups[group].Add(face);
            }

            foreach (List<GeoFace3> group in groups.Where(g => g.Count > 1))
            {
                var edges = new List<GeoLine3>();

                foreach (GeoFace3 face in group)
                {
                    foreach (IReadOnlyList<GeoPoint3> ring in LoopAssembly.EnumerateMaterialRings(face))
                    {
                        for (int i = 0; i < ring.Count; i++)
                        {
                            edges.Add(new GeoLine3(ring[i], ring[(i + 1) % ring.Count]));
                        }
                    }
                }

                yield return edges;
            }
        }

        [Fact]
        public void GluingTheCellsOfCutBodiesGivesWhatComparingEveryPairGave()
        {
            int walls = 0, backToBack = 0, groups = 0;

            for (int seed = 0; seed < 12; seed++)
            {
                List<GeoFace3> faces = Cells(seed).SelectMany(cell => cell.Faces).ToList();

                List<GeoFace3> before = DropMatchedPairsAsBefore(faces);
                List<GeoFace3> now = Boolean3.DropMatchedPairs(faces, Tol);

                Assert.Equal(before.Count, now.Count);
                for (int k = 0; k < before.Count; k++)
                {
                    Assert.Same(before[k], now[k]);
                }

                walls += faces.Count - now.Count;

                List<GeoFace3> cancelledBefore = CancelBackToBackAsBefore(before);
                List<GeoFace3> cancelledNow = Boolean3.CancelBackToBack(now, Tol);
                AssertSameFaces(cancelledBefore, cancelledNow, $"seed {seed}");
                backToBack += before.Count - cancelledBefore.Count;

                foreach (List<GeoLine3> edges in CoplanarEdgeGroups(cancelledBefore))
                {
                    var expected = new List<GeoLine3>(edges);
                    var actual = new List<GeoLine3>(edges);

                    CancelOpposedEdgesAsBefore(expected);
                    LoopAssembly.CancelOpposedEdges(actual, Tol);

                    AssertSameEdges(expected, actual, $"seed {seed}");
                    groups++;
                }
            }

            // Every kind of work must have been done for the comparison to say anything.
            Assert.True(walls > 500, $"only {walls} faces paired off");
            Assert.True(backToBack > 20, $"only {backToBack} faces lay back to back unmatched");
            Assert.True(groups > 30, $"only {groups} coplanar groups merged");
        }

        /// <summary>
        /// Edges meeting at T-junctions, and ends nudged to either side of the tolerance: where an index could miss
        /// a match the full comparison makes, or find one it does not.
        /// </summary>
        [Fact]
        public void CancellingEdgesNearTheToleranceGivesWhatComparingEveryPairGave()
        {
            var rng = new Random(77);

            for (int round = 0; round < 40; round++)
            {
                // Turned into a general direction in space, so that no axis lines the edges up.
                GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.3, -0.7, 0.64), 0.9);
                List<GeoLine3> edges = Strips(rng).Select(e => new GeoLine3(e.StartPoint.TransformBy(turn), e.EndPoint.TransformBy(turn))).ToList();

                var expected = new List<GeoLine3>(edges);
                var actual = new List<GeoLine3>(edges);

                CancelOpposedEdgesAsBefore(expected);
                LoopAssembly.CancelOpposedEdges(actual, Tol);

                AssertSameEdges(expected, actual, $"round {round}");
            }
        }

        /// <summary>
        /// The same strips lying square to an axis, as the caps of cells cut along it lie: sorted along that axis, every end
        /// falls in the run of every edge, and an index sorting along another must find what the full comparison finds.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void CancellingEdgesSquareToAnAxisGivesWhatComparingEveryPairGave(int axis)
        {
            var rng = new Random(78 + axis);

            for (int round = 0; round < 40; round++)
            {
                GeoPoint3 Square(GeoPoint3 p) => axis == 0 ? new GeoPoint3(p.Z + 500, p.X, p.Y) : axis == 1 ? new GeoPoint3(p.X, p.Z + 500, p.Y) : new GeoPoint3(p.X, p.Y, p.Z + 500);
                List<GeoLine3> edges = Strips(rng).Select(e => new GeoLine3(Square(e.StartPoint), Square(e.EndPoint))).ToList();

                var expected = new List<GeoLine3>(edges);
                var actual = new List<GeoLine3>(edges);

                CancelOpposedEdgesAsBefore(expected);
                LoopAssembly.CancelOpposedEdges(actual, Tol);

                AssertSameEdges(expected, actual, $"axis {axis}, round {round}");
            }
        }

        [Fact]
        public void CancellingTheEdgesOfALargeCapSquareToXStaysCheap()
        {
            // The rim of a cap square to X, as a drum of 65 536 sides cut across its length leaves. With its ends sorted along
            // X, every end fell in the run of every edge: the 16 384 edges of a smaller one took half a second.
            const int sides = 65536;
            var edges = new List<GeoLine3>(sides);

            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                edges.Add(new GeoLine3(new GeoPoint3(5000, 600 * Math.Cos(a0), 600 * Math.Sin(a0)), new GeoPoint3(5000, 600 * Math.Cos(a1), 600 * Math.Sin(a1))));
            }

            var watch = Stopwatch.StartNew();
            LoopAssembly.CancelOpposedEdges(edges, Tol);
            watch.Stop();

            Assert.Equal(sides, edges.Count);

            // Generous on purpose: this is a guard against trying every end, not a benchmark.
            Assert.True(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// The edges of a rectangle cut into strips, and each strip into rectangles of its own, so that the strips' long
        /// sides face several short ones: every interior line is a T-junction. Their ends are nudged by up to twice the
        /// tolerance, the same nudge wherever a corner is shared, or not.
        /// </summary>
        private static List<GeoLine3> Strips(Random rng)
        {
            var rectangles = new List<(double X0, double Y0, double X1, double Y1)>();
            double y = 0;

            while (y < 100)
            {
                double height = Math.Min(100 - y, rng.Next(5, 30));
                double x = 0;

                while (x < 120)
                {
                    double width = Math.Min(120 - x, rng.Next(5, 40));
                    rectangles.Add((x, y, x + width, y + height));
                    x += width;
                }

                y += height;
            }

            var nudges = new Dictionary<(double, double), GeoVector3>();
            double scale = rng.NextDouble() < 0.5 ? 0.0 : Tol.EqualPoint * 2.0;
            bool shared = rng.NextDouble() < 0.5;

            GeoPoint3 Corner(double cx, double cy)
            {
                GeoVector3 nudge;

                if (!shared || !nudges.TryGetValue((cx, cy), out nudge))
                {
                    nudge = new GeoVector3((rng.NextDouble() - 0.5) * scale, (rng.NextDouble() - 0.5) * scale, (rng.NextDouble() - 0.5) * scale);
                    nudges[(cx, cy)] = nudge;
                }

                return new GeoPoint3(cx, cy, 0).Add(nudge);
            }

            var edges = new List<GeoLine3>();

            foreach (var r in rectangles)
            {
                GeoPoint3 a = Corner(r.X0, r.Y0), b = Corner(r.X1, r.Y0), c = Corner(r.X1, r.Y1), d = Corner(r.X0, r.Y1);
                edges.Add(new GeoLine3(a, b));
                edges.Add(new GeoLine3(b, c));
                edges.Add(new GeoLine3(c, d));
                edges.Add(new GeoLine3(d, a));
            }

            return edges;
        }

        /// <summary>
        /// The bodies cutting openings gives are what they were: the material's volume, closed, a point in a hole
        /// outside and a point in the material inside.
        /// </summary>
        [Fact]
        public void APlateWithManyHolesIsCutAsBefore()
        {
            var rng = new Random(5);

            for (int round = 0; round < 6; round++)
            {
                var holes = new List<GeoSolid3>();
                var centres = new List<GeoPoint3>();
                double radius = 6;

                while (holes.Count < 3 + round)
                {
                    var c = new GeoPoint3(rng.Next(-200, 201), rng.Next(-200, 201), 0);

                    if (centres.Any(o => o.DistanceTo(c) < 4 * radius))
                    {
                        continue;
                    }

                    centres.Add(c);
                    holes.Add(GeoSolid3.Cylinder(c.Add(new GeoVector3(0, 0, -30)), c.Add(new GeoVector3(0, 0, 20)), radius, 12));
                }

                GeoSolid3 plate = Box(-250, -250, -20, 250, 250, 0).WithOpenings(holes);

                Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
                Assert.True(material.IsClosed());
                // Each hole is a prism running right through the plate, 20 of its 50 inside it, and no two meet.
                double expected = 500.0 * 500.0 * 20.0 - holes.Sum(hole => hole.GetVolume() * 20.0 / 50.0);
                Assert.Equal(expected, material.GetVolume(), 6);

                foreach (GeoPoint3 c in centres)
                {
                    Assert.Equal(PointLocation.OutSide, material.Locate(c.Add(new GeoVector3(0, 0, -10))));
                    Assert.Equal(PointLocation.Inside, material.Locate(c.Add(new GeoVector3(2 * radius, 0, -10))));
                }
            }
        }
    }
}
