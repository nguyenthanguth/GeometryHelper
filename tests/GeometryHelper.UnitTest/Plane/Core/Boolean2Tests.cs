using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Combining regions (Boolean2 and the GeoPolygon2 and GeoFace2 members that delegate to it), checked
    /// against areas worked out by hand, against the identities any correct union, intersection and difference
    /// satisfy, and point by point against containment in the operands.
    /// </summary>
    public class Boolean2Tests
    {
        private static GeoPolygon2 P(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) { points[i] = new GeoPoint2(xy[2 * i], xy[2 * i + 1]); }
            return new GeoPolygon2(points);
        }

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => P(x0, y0, x1, y0, x1, y1, x0, y1);

        private static double Area(GeoFace2[] faces) => faces.Sum(f => f.Area);

        private static bool Inside(GeoFace2[] faces, GeoPoint2 point) => faces.Any(f => f.Contains(point));

        #region Hand-worked cases

        [Fact]
        public void OverlappingSquares_GiveTheExpectedAreas()
        {
            GeoPolygon2 a = Box(0, 0, 10, 10);
            GeoPolygon2 b = Box(5, 5, 15, 15);

            Assert.Equal(175.0, Area(a.Union(b)), 9);
            Assert.Equal(25.0, Area(a.Intersect(b)), 9);
            Assert.Equal(75.0, Area(a.Subtract(b)), 9);
            Assert.Equal(150.0, Area(a.Xor(b)), 9);

            Assert.Single(a.Union(b));
            Assert.Equal(8, a.Union(b)[0].Boundary.VertexCount);
            Assert.Equal(6, a.Subtract(b)[0].Boundary.VertexCount);
        }

        [Fact]
        public void Intersection_OfSquares_IsTheExactOverlap()
        {
            GeoFace2 overlap = Box(0, 0, 10, 10).Intersect(Box(5, 5, 15, 15)).Single();

            Assert.Empty(overlap.Holes);
            Assert.Equal(4, overlap.Boundary.VertexCount);
            Assert.All(new[] { new GeoPoint2(5, 5), new GeoPoint2(10, 5), new GeoPoint2(10, 10), new GeoPoint2(5, 10) },
                corner => Assert.Contains(overlap.Boundary.Vertices, v => v.DistanceTo(corner) < 1e-12));
        }

        [Fact]
        public void DisjointSquares_StayApart()
        {
            GeoPolygon2 a = Box(0, 0, 10, 10);
            GeoPolygon2 b = Box(20, 0, 30, 10);

            Assert.Equal(2, a.Union(b).Length);
            Assert.Empty(a.Intersect(b));
            Assert.Equal(100.0, Area(a.Subtract(b)), 9);
            Assert.Equal(2, a.Xor(b).Length);
        }

        [Fact]
        public void SquareInsideASquare_CutsAHole()
        {
            GeoFace2 frame = Box(0, 0, 20, 20).Subtract(Box(5, 5, 15, 15)).Single();

            Assert.Single(frame.Holes);
            Assert.Equal(300.0, frame.Area, 9);
            Assert.False(frame.Boundary.IsClockwise);
            Assert.True(frame.Holes[0].IsClockwise);
            Assert.False(frame.Contains(new GeoPoint2(10, 10)));
            Assert.True(frame.Contains(new GeoPoint2(2, 2)));

            // The other way round, nothing is left.
            Assert.Empty(Box(5, 5, 15, 15).Subtract(Box(0, 0, 20, 20)));
        }

        [Fact]
        public void SquaresSharingAnEdge_MergeIntoOne()
        {
            GeoFace2 merged = Box(0, 0, 10, 10).Union(Box(10, 0, 20, 10)).Single();

            Assert.Equal(200.0, merged.Area, 9);
            Assert.Equal(4, merged.Boundary.VertexCount);

            // Sharing only an edge, they overlap nowhere.
            Assert.Empty(Box(0, 0, 10, 10).Intersect(Box(10, 0, 20, 10)));
        }

        [Fact]
        public void SquaresTouchingAtACorner_StayTwoSimpleFaces()
        {
            GeoFace2[] pair = Box(0, 0, 10, 10).Union(Box(10, 10, 20, 20));

            Assert.Equal(2, pair.Length);
            Assert.All(pair, face => Assert.True(face.Boundary.IsSimple()));
            Assert.Equal(200.0, Area(pair), 9);
        }

        [Fact]
        public void FaceWithAHole_AndAPatchOverIt_UnionFillsTheHole()
        {
            var frame = new GeoFace2(Box(0, 0, 20, 20), new[] { Box(5, 5, 15, 15) });
            var patch = new GeoFace2(Box(4, 4, 16, 16));

            GeoFace2 whole = frame.Union(patch).Single();
            Assert.Empty(whole.Holes);
            Assert.Equal(400.0, whole.Area, 9);

            // Intersecting with the patch leaves the rim round the hole.
            GeoFace2 rim = frame.Intersect(patch).Single();
            Assert.Single(rim.Holes);
            Assert.Equal(144.0 - 100.0, rim.Area, 9);

            // A hole in the tool is not taken out: subtracting the frame leaves the hole's area.
            GeoFace2 plug = patch.Subtract(frame).Single();
            Assert.Equal(100.0, plug.Area, 9);
        }

        [Fact]
        public void Slab_WithManyOpenings_CutInOneCall()
        {
            GeoPolygon2 slab = Box(0, 0, 100, 40);
            var openings = new[] { Box(10, 10, 20, 20), Box(30, 10, 40, 20), Box(50, 10, 60, 20), Box(95, 30, 105, 35) };

            GeoFace2 cut = slab.Subtract(openings).Single();

            // Three openings lie inside and become holes; the fourth crosses the edge and notches it.
            Assert.Equal(3, cut.Holes.Count);
            Assert.Equal(4000.0 - 300.0 - 25.0, cut.Area, 9);
            Assert.Equal(Area(Boolean2.Subtract(slab, openings)), cut.Area, 12);
        }

        [Fact]
        public void UnionOfMany_JoinsOverlapsAndKeepsGaps()
        {
            var tiles = new List<GeoPolygon2>();
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if (i == 2 && j == 2) { continue; }
                    tiles.Add(Box(i * 10, j * 10, i * 10 + 10.5, j * 10 + 10.5));
                }
            }

            GeoFace2 floor = Boolean2.Union(tiles).Single();

            // The missing middle tile leaves a hole, less the overlaps of its neighbours.
            Assert.Single(floor.Holes);
            Assert.Equal(9.5 * 9.5, floor.Holes[0].Area, 9);
            Assert.Equal(50.5 * 50.5 - 9.5 * 9.5, floor.Area, 9);

            Assert.Empty(Boolean2.Union(new GeoPolygon2[0]));
        }

        [Fact]
        public void BowTie_IsReadUnderTheEvenOddRule()
        {
            var bow = P(0, 0, 10, 10, 10, 0, 0, 10);

            // Two triangles of 25 each, touching at the middle.
            Assert.Equal(50.0, Area(bow.Union(bow)), 9);
            Assert.Equal(2, bow.Union(bow).Length);
            Assert.Equal(25.0, Area(bow.Intersect(Box(0, 0, 5, 10))), 9);
        }

        [Fact]
        public void FarFromTheOrigin_TheAnswerIsExact()
        {
            var shift = new GeoVector2(4e6, -3e6);
            GeoFace2 overlap = Box(0, 0, 10, 10).Translate(shift).Intersect(Box(5, 5, 15, 15).Translate(shift)).Single();

            Assert.Equal(25.0, overlap.Area, 6);
            Assert.Contains(overlap.Boundary.Vertices, v => v.DistanceTo(new GeoPoint2(5, 5).Add(shift)) < 1e-8);
        }

        [Fact]
        public void BadArguments_AreRefused()
        {
            GeoPolygon2 square = Box(0, 0, 1, 1);

            Assert.Throws<ArgumentNullException>(() => Boolean2.Union(square, (GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Intersect((GeoPolygon2)null, square));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Union((IEnumerable<GeoPolygon2>)null));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Union(new[] { square, null }));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Subtract(square, (IEnumerable<GeoPolygon2>)null));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Union(new GeoFace2(square), (GeoFace2)null));
        }

        [Fact]
        public void CoreAndInstance_Agree()
        {
            GeoPolygon2 a = Box(0, 0, 10, 10);
            GeoPolygon2 b = P(5, -2, 12, 4, 6, 13);
            var fa = new GeoFace2(a, new[] { Box(2, 2, 4, 4) });
            var fb = new GeoFace2(b);

            Assert.Equal(Boolean2.Union(a, b), a.Union(b));
            Assert.Equal(Boolean2.Intersect(a, b, Tolerance.Global), a.Intersect(b, Tolerance.Global));
            Assert.Equal(Boolean2.Subtract(a, b), a.Subtract(b));
            Assert.Equal(Boolean2.Xor(a, b), a.Xor(b));
            Assert.Equal(Boolean2.Subtract(a, new[] { b }), a.Subtract(new[] { b }));
            Assert.Equal(Boolean2.Union(fa, fb), fa.Union(fb));
            Assert.Equal(Boolean2.Intersect(fa, fb), fa.Intersect(fb));
            Assert.Equal(Boolean2.Subtract(fa, fb, Tolerance.Global), fa.Subtract(fb, Tolerance.Global));
            Assert.Equal(Boolean2.Xor(fa, fb), fa.Xor(fb));
            Assert.Equal(Boolean2.Subtract(fa, new[] { fb }), fa.Subtract(new[] { fb }));
        }

        #endregion

        #region Properties

        private static IEnumerable<GeoPolygon2> RandomStars(Random rng, int count, double spread)
        {
            for (int t = 0; t < count; t++)
            {
                int n = 5 + rng.Next(20);
                var points = new GeoPoint2[n];
                double cx = rng.NextDouble() * spread, cy = rng.NextDouble() * spread;

                for (int i = 0; i < n; i++)
                {
                    double angle = 2 * Math.PI * i / n;
                    double radius = 2 + rng.NextDouble() * 8;
                    points[i] = new GeoPoint2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                }

                yield return new GeoPolygon2(points);
            }
        }

        [Fact]
        public void RandomPairs_SatisfyTheAreaIdentities()
        {
            var rng = new Random(314);
            GeoPolygon2[] stars = RandomStars(rng, 200, 12).ToArray();

            for (int k = 0; k + 1 < stars.Length; k += 2)
            {
                GeoPolygon2 a = stars[k];
                GeoPolygon2 b = stars[k + 1];

                double union = Area(a.Union(b));
                double both = Area(a.Intersect(b));
                double aOnly = Area(a.Subtract(b));
                double bOnly = Area(b.Subtract(a));
                double either = Area(a.Xor(b));

                Assert.Equal(a.Area + b.Area, union + both, 6);
                Assert.Equal(a.Area, aOnly + both, 6);
                Assert.Equal(b.Area, bOnly + both, 6);
                Assert.Equal(union - both, either, 6);
            }
        }

        [Fact]
        public void RandomPairs_AgreeWithContainmentPointByPoint()
        {
            var rng = new Random(2718);
            GeoPolygon2[] stars = RandomStars(rng, 60, 10).ToArray();
            int probes = 0;

            for (int k = 0; k + 1 < stars.Length; k += 2)
            {
                GeoPolygon2 a = stars[k];
                GeoPolygon2 b = stars[k + 1];
                GeoFace2[] union = a.Union(b), both = a.Intersect(b), aOnly = a.Subtract(b), either = a.Xor(b);

                for (int i = 0; i < 200; i++)
                {
                    var p = new GeoPoint2(rng.NextDouble() * 30 - 10, rng.NextDouble() * 30 - 10);

                    // Points on or next to an outline could go either way; the rest must agree exactly.
                    if (a.GetClosestPointOnBoundary(p).DistanceTo(p) < 1e-6 || b.GetClosestPointOnBoundary(p).DistanceTo(p) < 1e-6)
                    {
                        continue;
                    }

                    bool inA = a.Contains(p), inB = b.Contains(p);
                    Assert.Equal(inA || inB, Inside(union, p));
                    Assert.Equal(inA && inB, Inside(both, p));
                    Assert.Equal(inA && !inB, Inside(aOnly, p));
                    Assert.Equal(inA != inB, Inside(either, p));
                    probes++;
                }
            }

            Assert.True(probes > 5000);
        }

        [Fact]
        public void EveryResult_IsSimpleAndWoundTheStandardWay()
        {
            var rng = new Random(99);
            GeoPolygon2[] stars = RandomStars(rng, 80, 8).ToArray();

            for (int k = 0; k + 1 < stars.Length; k += 2)
            {
                foreach (GeoFace2 face in stars[k].Union(stars[k + 1]).Concat(stars[k].Subtract(stars[k + 1])).Concat(stars[k].Xor(stars[k + 1])))
                {
                    Assert.True(face.Boundary.IsSimple());
                    Assert.False(face.Boundary.IsClockwise);
                    Assert.All(face.Holes, hole => Assert.True(hole.IsClockwise));
                    Assert.All(face.Holes, hole => Assert.True(hole.IsSimple()));
                }
            }
        }

        [Fact]
        public void ARigidMotion_MovesTheAnswerWithIt()
        {
            var rng = new Random(5);
            GeoPolygon2[] stars = RandomStars(rng, 20, 8).ToArray();
            var shift = new GeoVector2(1234.5, -678.9);

            for (int k = 0; k + 1 < stars.Length; k += 2)
            {
                GeoPolygon2 a = stars[k], b = stars[k + 1];
                GeoPolygon2 ma = a.RotateBy(0.8, new GeoPoint2(0, 0)).Translate(shift);
                GeoPolygon2 mb = b.RotateBy(0.8, new GeoPoint2(0, 0)).Translate(shift);

                Assert.Equal(Area(a.Union(b)), Area(ma.Union(mb)), 6);
                Assert.Equal(Area(a.Intersect(b)), Area(ma.Intersect(mb)), 6);
                Assert.Equal(a.Subtract(b).Length, ma.Subtract(mb).Length);
            }
        }

        #endregion
    }
}
