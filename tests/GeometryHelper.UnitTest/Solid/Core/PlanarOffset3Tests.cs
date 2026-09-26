using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Offsetting flat shapes in space within their own plane (Offset3 for polygons, faces, polylines and
    /// circles), and telling simple polygons from self-touching ones. The random cases are laid out in a frame
    /// of the test's own and compared with Clipper2, referenced by this test project only.
    /// </summary>
    public class PlanarOffset3Tests
    {
        /// <summary>
        /// A frame of the test's own: an origin, a unit normal and two axes square to it, u x v = n.
        /// </summary>
        private sealed class Frame
        {
            public Frame(GeoPoint3 origin, GeoVector3 normal, GeoVector3 hint)
            {
                Origin = origin;
                Normal = normal.Normalize();
                U = hint.Subtract(Normal.Multiply(hint.DotProduct(Normal))).Normalize();
                V = Normal.CrossProduct(U);
            }

            public GeoPoint3 Origin { get; }
            public GeoVector3 Normal { get; }
            public GeoVector3 U { get; }
            public GeoVector3 V { get; }

            public GeoPoint3 ToWorld(double x, double y) => Origin.Add(U.Multiply(x)).Add(V.Multiply(y));

            public PointD ToLocal(GeoPoint3 p)
            {
                GeoVector3 d = Origin.GetVectorTo(p);
                return new PointD(d.DotProduct(U), d.DotProduct(V));
            }

            public GeoPolygon3 Polygon(params double[] xy)
            {
                var points = new GeoPoint3[xy.Length / 2];
                for (int i = 0; i < points.Length; i++) { points[i] = ToWorld(xy[2 * i], xy[2 * i + 1]); }
                return new GeoPolygon3(points);
            }
        }

        private static readonly Frame Tilted = new Frame(new GeoPoint3(100, -200, 300), new GeoVector3(1, 2, 3), new GeoVector3(1, 0, 0));

        private static readonly OffsetOptions SharpAlways = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);

        private static double SignedArea(GeoPolygon3 polygon, GeoVector3 normal) => polygon.Area * Math.Sign(polygon.Normal.DotProduct(normal));

        private static void AssertInPlane(Frame frame, GeoPolygon3 polygon)
        {
            foreach (GeoPoint3 v in polygon.Vertices)
            {
                Assert.True(Math.Abs(frame.Origin.GetVectorTo(v).DotProduct(frame.Normal)) < 1e-8, $"vertex {v} is off the plane");
            }
        }

        #region Polygon

        [Fact]
        public void Square_InATiltedPlane_GrowsAndShrinksExactly()
        {
            GeoPolygon3 square = Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10);

            GeoPolygon3 grown = square.Offset(2.0).Single();
            Assert.Equal(196.0, grown.Area, 8);
            Assert.True(grown.Normal.IsCodirectionalTo(square.Normal));
            AssertInPlane(Tilted, grown);
            Assert.Contains(grown.Vertices, v => v.DistanceTo(Tilted.ToWorld(-2, -2)) < 1e-8);
            Assert.Contains(grown.Vertices, v => v.DistanceTo(Tilted.ToWorld(12, 12)) < 1e-8);

            GeoPolygon3 shrunk = square.Offset(-2.0).Single();
            Assert.Equal(36.0, shrunk.Area, 8);
            Assert.Contains(shrunk.Vertices, v => v.DistanceTo(Tilted.ToWorld(2, 2)) < 1e-8);

            Assert.Empty(square.Offset(-5.0));
        }

        [Fact]
        public void Flipped_Polygon_StillGrowsOutward_AndKeepsItsNormal()
        {
            GeoPolygon3 square = Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10).Flip();

            GeoPolygon3 grown = square.Offset(2.0).Single();
            Assert.Equal(196.0, grown.Area, 8);
            Assert.True(grown.Normal.IsCodirectionalTo(square.Normal));
        }

        [Fact]
        public void Dumbbell_SplitsAndRing_EnclosesAHole()
        {
            GeoPolygon3 dumbbell = Tilted.Polygon(0, 0, 10, 0, 10, 4, 14, 4, 14, 0, 24, 0, 24, 10, 14, 10, 14, 6, 10, 6, 10, 10, 0, 10);
            GeoPolygon3[] pieces = dumbbell.Offset(-1.5);
            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.Equal(49.0, piece.Area, 8));

            GeoPolygon3 ring = Tilted.Polygon(0, 0, 20, 0, 20, 9, 15, 9, 15, 5, 5, 5, 5, 15, 15, 15, 15, 11, 20, 11, 20, 20, 0, 20);
            GeoPolygon3[] loops = ring.Offset(1.5);
            Assert.Equal(2, loops.Length);
            Assert.Equal(529.0, loops[0].Area, 8);
            Assert.Equal(49.0, loops[1].Area, 8);
            Assert.True(loops[0].Normal.IsCodirectionalTo(ring.Normal));
            Assert.True(loops[1].Normal.IsCodirectionalTo(ring.Normal.Negate()));
            Assert.Equal(480.0, loops.Sum(l => SignedArea(l, ring.Normal)), 8);
        }

        [Fact]
        public void RoundAndChamfer_HaveTheAreasTheFormulasGive()
        {
            GeoPolygon3 square = Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10);
            const double d = 2.0;

            double chamfer = 100 + 40 * d + 4 * 2 * d * d * Math.Tan(Math.PI / 8);
            Assert.Equal(chamfer, square.Offset(d, OffsetJoin.Chamfer).Single().Area, 8);

            double exact = 100 + 40 * d + Math.PI * d * d;
            double round = square.Offset(d, new OffsetOptions(OffsetJoin.Round, arcTolerance: 1e-6)).Single().Area;
            Assert.True(round < exact && exact - round < 1e-4, $"round area {round} against {exact}");
        }

        [Fact]
        public void FarFromTheOrigin_LosesNoPrecision()
        {
            var far = new Frame(new GeoPoint3(3e6, -4e6, 2e5), new GeoVector3(0.3, -0.2, 1), new GeoVector3(1, 1, 0));
            GeoPolygon3 grown = far.Polygon(0, 0, 10, 0, 10, 10, 0, 10).Offset(2.0).Single();

            Assert.Equal(196.0, grown.Area, 5);
            Assert.Contains(grown.Vertices, v => v.DistanceTo(far.ToWorld(12, 12)) < 1e-6);
        }

        [Fact]
        public void TinyDistance_AndBadArguments()
        {
            GeoPolygon3 square = Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(square, square.Offset(0.00005).Single());
            Assert.Throws<ArgumentNullException>(() => Offset3.Offset((GeoPolygon3)null, 1.0));
            Assert.Throws<ArgumentNullException>(() => square.Offset(1.0, (OffsetOptions)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => square.Offset(double.NaN));
        }

        [Fact]
        public void CoreAndInstance_Agree()
        {
            GeoPolygon3 l = Tilted.Polygon(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10);
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.01);

            Assert.Equal(Offset3.Offset(l, 1.5), l.Offset(1.5));
            Assert.Equal(Offset3.Offset(l, -1.5, Tolerance.Global), l.Offset(-1.5, Tolerance.Global));
            Assert.Equal(Offset3.Offset(l, 1.5, OffsetJoin.Chamfer), l.Offset(1.5, OffsetJoin.Chamfer));
            Assert.Equal(Offset3.Offset(l, 1.5, OffsetJoin.Chamfer, Tolerance.Global), l.Offset(1.5, OffsetJoin.Chamfer, Tolerance.Global));
            Assert.Equal(Offset3.Offset(l, 1.5, options), l.Offset(1.5, options));
            Assert.Equal(Offset3.Offset(l, 1.5, options, Tolerance.Global), l.Offset(1.5, options, Tolerance.Global));
        }

        #endregion

        #region Against Clipper2

        private static IEnumerable<(Frame Frame, double[] Xy)> RandomStars(int seed, int count)
        {
            var rng = new Random(seed);

            for (int t = 0; t < count; t++)
            {
                var frame = new Frame(
                    new GeoPoint3(rng.NextDouble() * 1000 - 500, rng.NextDouble() * 1000 - 500, rng.NextDouble() * 1000 - 500),
                    new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5 + 0.01),
                    new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5 + 0.01, rng.NextDouble() - 0.5));

                int n = 5 + rng.Next(25);
                var xy = new double[2 * n];
                for (int i = 0; i < n; i++)
                {
                    double angle = 2 * Math.PI * i / n, radius = 3 + rng.NextDouble() * 10;
                    xy[2 * i] = radius * Math.Cos(angle);
                    xy[2 * i + 1] = radius * Math.Sin(angle);
                }

                yield return (frame, xy);
            }
        }

        private static PathsD Local(Frame frame, IEnumerable<GeoPolygon3> polygons)
        {
            var paths = new PathsD();
            foreach (GeoPolygon3 polygon in polygons)
            {
                paths.Add(new PathD(polygon.Vertices.Select(frame.ToLocal)));
            }
            return paths;
        }

        private static double XorArea(PathsD a, PathsD b) => Math.Abs(Clipper.Area(Clipper.Xor(a, b, FillRule.NonZero, 8)));

        [Theory]
        [InlineData(OffsetJoin.Miter, JoinType.Miter)]
        [InlineData(OffsetJoin.Chamfer, JoinType.Square)]
        [InlineData(OffsetJoin.Round, JoinType.Round)]
        public void RandomStarsInRandomPlanes_MatchClipper(OffsetJoin ours, JoinType theirs)
        {
            var options = new OffsetOptions(ours, double.PositiveInfinity, 0.0005);
            int compared = 0;

            foreach ((Frame frame, double[] xy) in RandomStars(88, 100))
            {
                GeoPolygon3 star = frame.Polygon(xy);
                var local = new PathsD { new PathD(Enumerable.Range(0, xy.Length / 2).Select(i => new PointD(xy[2 * i], xy[2 * i + 1]))) };

                foreach (double d in new[] { 0.4, -0.4, 2.0, -2.0 })
                {
                    GeoPolygon3[] result = star.Offset(d, options);
                    PathsD expected = Clipper.InflatePaths(local, d, theirs, EndType.Polygon, 1e9, 8, 0.0005);

                    foreach (GeoPolygon3 loop in result)
                    {
                        AssertInPlane(frame, loop);
                    }

                    double limit = ours == OffsetJoin.Round ? 2 * 0.0005 * (result.Sum(r => r.Length) + 1) : 1e-5;
                    double xor = XorArea(Local(frame, result), expected);
                    Assert.True(xor < limit, $"{ours} offset {d} of star {compared}: differs from Clipper by area {xor}");
                    compared++;
                }
            }

            Assert.Equal(400, compared);
        }

        /// <summary>
        /// The XY plane, where the frame the library lays a polygon out in coincides with the world axes, so the
        /// shared solver sees exactly the coordinates written here.
        /// </summary>
        private static readonly Frame Flat = new Frame(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, GeoVector3.XAxis);

        [Fact]
        public void TwoPiecesTouchingAtAPoint_ComeBackAsTwoSimpleLoops()
        {
            // Two 4 x 4 squares overlapping in a 2 x 2 corner, shrunk by 1: two squares touching at (3, 3). The
            // solver must walk the boundary so that they come back as two loops, not as one crossing itself.
            GeoPolygon3 overlapping = Flat.Polygon(0, 0, 4, 0, 4, 2, 6, 2, 6, 6, 2, 6, 2, 4, 0, 4);

            GeoPolygon3[] pieces = overlapping.Offset(-1.0);

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.True(piece.IsSimple()));
            Assert.All(pieces, piece => Assert.Equal(4.0, piece.Area, 9));
        }

        [Fact]
        public void SmallPolygonShrunkPastItsWidth_Vanishes()
        {
            // Found by fuzzing with the detour through inside corners taken out of the raw offset: ten short edges
            // about two units across, shrunk by three. Nothing can be left, and the detours are what make the raw
            // loop's folds cancel; without them a piece of 2.1 square units survived.
            GeoPolygon3 walk = Flat.Polygon(
                1.29120435487293, 0.8529122511717194, 2.0692576855245406, 1.8246495197613428, 2.2463920430178077, 2.5563866007258804,
                2.111849016130124, 3.2240605527713724, 2.131484777697378, 3.5052053578503806, 1.5969035822287925, 4.292744418060094,
                1.204212114951118, 4.359962079953233, 0.6624590592209036, 4.244928638591521, 0.2404842875843622, 3.88891733400727,
                0.14789238177608988, 3.7046090201323696);

            foreach (OffsetJoin join in new[] { OffsetJoin.Miter, OffsetJoin.Round, OffsetJoin.Chamfer })
            {
                Assert.Empty(walk.Offset(-3.0, join));
            }
        }

        [Fact]
        public void CombWithTeethNarrowerThanTheOffset_MatchesClipper()
        {
            // Found by fuzzing the shared solver against Clipper: teeth narrower than twice the distance, shrunk
            // away. The offset edges of a tooth's two sides pass each other without crossing, and only the detour
            // through each original corner keeps the base's outline right; the arcs of the two corners at a
            // tooth's foot also meet almost exactly at vertices of both, which a crossing test demanding clear
            // separation missed.
            double[] xy =
            {
                0, 0, 12.556810211975506, 0, 12.556810211975506, 1, 12.346807620649603, 1, 12.346807620649603, 5.422796114544755,
                11.030278669032398, 5.422796114544755, 11.030278669032398, 1, 9.675089881091886, 1, 9.675089881091886, 6.886248821339686,
                9.400917458069006, 6.886248821339686, 9.400917458069006, 1, 8.00142274857565, 1, 8.00142274857565, 3.632585188668494,
                7.289240777813476, 3.632585188668494, 7.289240777813476, 1, 6.227419358178703, 1, 6.227419358178703, 7.821611514697602,
                5.277756546427383, 7.821611514697602, 5.277756546427383, 1, 4.161423784103907, 1, 4.161423784103907, 6.20141693353719,
                3.7245509831349137, 6.20141693353719, 3.7245509831349137, 1, 2.459634663052687, 1, 2.459634663052687, 8.855549968711822,
                2.1069624506435183, 8.855549968711822, 2.1069624506435183, 1, 0.6055348466641897, 1, 0.6055348466641897, 4.003288449255418,
                0, 4.003288449255418, 0, 1
            };

            GeoPolygon3 comb = Flat.Polygon(xy);
            var local = new PathsD { new PathD(Enumerable.Range(0, xy.Length / 2).Select(i => new PointD(xy[2 * i], xy[2 * i + 1]))) };

            foreach (double d in new[] { -0.3, 0.3, -0.12 })
            {
                GeoPolygon3[] round = comb.Offset(d, new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.001));
                PathsD expectedRound = Clipper.InflatePaths(local, d, JoinType.Round, EndType.Polygon, 1e9, 8, 0.001);
                Assert.True(XorArea(Local(Flat, round), expectedRound) < 0.002 * (round.Sum(p => p.Length) + 1.0), $"round offset {d} differs from Clipper");

                GeoPolygon3[] sharp = comb.Offset(d, SharpAlways);
                PathsD expectedSharp = Clipper.InflatePaths(local, d, JoinType.Miter, EndType.Polygon, 1e9, 8, 0.0);
                Assert.True(XorArea(Local(Flat, sharp), expectedSharp) < 1e-5, $"sharp offset {d} differs from Clipper");
            }
        }

        [Fact]
        public void ARigidMotion_MovesTheOffsetWithIt()
        {
            GeoPolygon3 l = Tilted.Polygon(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10);

            foreach (GeoTransform3 motion in new[]
            {
                GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 1.3),
                GeoTransform3.Translation(new GeoVector3(5000, -7000, 300)).Multiply(GeoTransform3.RotationAxis(new GeoVector3(-3, 1, 2), 2.1)),
                GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1, 0, 0), new GeoVector3(1, 1, 1)))
            })
            {
                GeoPolygon3 moved = l.TransformBy(motion);

                foreach (double d in new[] { 1.2, -1.2 })
                {
                    GeoPolygon3[] a = l.Offset(d, OffsetJoin.Round);
                    GeoPolygon3[] b = moved.Offset(d, OffsetJoin.Round);

                    Assert.Equal(a.Length, b.Length);
                    Assert.Equal(a.Sum(p => p.Area), b.Sum(p => p.Area), 6);

                    // The offset of the moved polygon is the moved offset: compare in the moved polygon's plane.
                    var frame = new Frame(moved[0], moved.Normal, moved[0].GetVectorTo(moved[1]));
                    Assert.True(XorArea(Local(frame, a.Select(p => p.TransformBy(motion))), Local(frame, b)) < 1e-6);
                }
            }
        }

        #endregion

        #region Face

        [Fact]
        public void Face_HolesShrinkAsTheBoundaryGrows()
        {
            var face = new GeoFace3(Tilted.Polygon(0, 0, 20, 0, 20, 20, 0, 20), new[] { Tilted.Polygon(5, 5, 15, 5, 15, 15, 5, 15) });

            GeoFace3 grown = face.Offset(2.0).Single();
            Assert.Equal(24 * 24 - 6 * 6, grown.Area, 8);
            Assert.Single(grown.Holes);
            Assert.True(grown.Normal.IsCodirectionalTo(face.Normal));
            Assert.True(grown.Holes[0].Normal.IsCodirectionalTo(face.Normal.Negate()));

            Assert.Equal(16 * 16 - 14 * 14, face.Offset(-2.0).Single().Area, 8);
            Assert.Empty(face.Offset(6.0).Single().Holes);
            Assert.Empty(face.Offset(-2.5));

            Assert.Equal(Offset3.Offset(face, 2.0), face.Offset(2.0));
            Assert.Equal(Offset3.Offset(face, 2.0, OffsetJoin.Round, Tolerance.Global), face.Offset(2.0, OffsetJoin.Round, Tolerance.Global));
        }

        [Fact]
        public void Face_ShrunkIntoRooms_KeepsEachColumnInItsRoom()
        {
            GeoPolygon3 outline = Tilted.Polygon(0, 0, 10, 0, 10, 4, 14, 4, 14, 0, 24, 0, 24, 10, 14, 10, 14, 6, 10, 6, 10, 10, 0, 10);
            var face = new GeoFace3(outline, new[] { Tilted.Polygon(4, 4, 6, 4, 6, 6, 4, 6), Tilted.Polygon(18, 4, 20, 4, 20, 6, 18, 6) });

            GeoFace3[] rooms = face.Offset(-1.5);

            Assert.Equal(2, rooms.Length);
            Assert.All(rooms, room => Assert.Single(room.Holes));
            Assert.All(rooms, room => Assert.Equal(49.0 - 25.0, room.Area, 8));
        }

        #endregion

        #region Polyline

        [Fact]
        public void Polyline_InTheXYPlane_OffsetsLikeTheFlatOne()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 7), new GeoPoint3(10, 0, 7), new GeoPoint3(10, 10, 7));

            GeoPolyline3 left = chain.OffsetInPlane(1.0, GeoVector3.ZAxis).Single();
            Assert.Equal(new[] { new GeoPoint3(0, 1, 7), new GeoPoint3(9, 1, 7), new GeoPoint3(9, 10, 7) }, left.Vertices.Select(Round));

            // Seen from below, left is the other side.
            GeoPolyline3 fromBelow = chain.OffsetInPlane(1.0, GeoVector3.ZAxis.Negate()).Single();
            Assert.Equal(new[] { new GeoPoint3(0, -1, 7), new GeoPoint3(11, -1, 7), new GeoPoint3(11, 10, 7) }, fromBelow.Vertices.Select(Round));
            Assert.Equal(chain.OffsetInPlane(-1.0, GeoVector3.ZAxis).Single().Vertices.Select(Round), fromBelow.Vertices.Select(Round));
        }

        private static GeoPoint3 Round(GeoPoint3 p) => new GeoPoint3(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(p.Z, 9));

        [Fact]
        public void Polyline_InAVerticalPlane_UsesThatPlane()
        {
            // A frame of a portal in the XZ plane, seen from -Y: its left there is up for a chain running along +X.
            var portal = new GeoPolyline3(new GeoPoint3(0, 5, 0), new GeoPoint3(0, 5, 4), new GeoPoint3(6, 5, 4), new GeoPoint3(6, 5, 0));

            GeoPolyline3 inner = portal.OffsetInPlane(-0.5, GeoVector3.YAxis.Negate()).Single();

            Assert.Equal(new[] { new GeoPoint3(0.5, 5, 0), new GeoPoint3(0.5, 5, 3.5), new GeoPoint3(5.5, 5, 3.5), new GeoPoint3(5.5, 5, 0) }, inner.Vertices.Select(Round));
            Assert.All(inner.Vertices, v => Assert.Equal(5.0, v.Y, 9));
        }

        [Fact]
        public void StraightPolyline_TakesThePlaneSquareToTheNormal()
        {
            var straight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(10, 0, 0));

            GeoPolyline3 moved = straight.OffsetInPlane(2.0, new GeoVector3(1, 0, 1)).Single();

            Assert.Equal(new[] { new GeoPoint3(0, 2, 0), new GeoPoint3(10, 2, 0) }, moved.Vertices.Select(Round));
        }

        [Fact]
        public void Polyline_WithNoPlaneOrNoSide_IsRefused()
        {
            var twisted = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(10, 10, 10));
            var flat = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0));

            Assert.Throws<ArgumentException>(() => twisted.OffsetInPlane(1.0, GeoVector3.ZAxis));
            Assert.Throws<ArgumentException>(() => flat.OffsetInPlane(1.0, GeoVector3.XAxis));
            Assert.Throws<ArgumentException>(() => flat.OffsetInPlane(1.0, GeoVector3.Zero));
            Assert.Throws<ArgumentException>(() => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)).OffsetInPlane(1.0, GeoVector3.XAxis));
            Assert.Throws<ArgumentNullException>(() => Offset3.OffsetInPlane((GeoPolyline3)null, 1.0, GeoVector3.ZAxis));
        }

        [Fact]
        public void Polyline_Hairpin_IsCutIntoTwoPieces()
        {
            var hairpin = new GeoPolyline3(new GeoPoint3(0, 0, 3), new GeoPoint3(10, 0, 3), new GeoPoint3(10, 1, 3), new GeoPoint3(0, 1, 3));

            GeoPolyline3[] inside = hairpin.OffsetInPlane(2.0, GeoVector3.ZAxis);

            Assert.Equal(2, inside.Length);
            Assert.Equal(new[] { new GeoPoint3(0, 2, 3), new GeoPoint3(10, 2, 3) }, inside[0].Vertices.Select(Round));
            Assert.Equal(new[] { new GeoPoint3(10, -1, 3), new GeoPoint3(0, -1, 3) }, inside[1].Vertices.Select(Round));

            Assert.Equal(
                Offset3.OffsetInPlane(hairpin, 2.0, GeoVector3.ZAxis, OffsetJoin.Round, Tolerance.Global),
                hairpin.OffsetInPlane(2.0, GeoVector3.ZAxis, OffsetJoin.Round, Tolerance.Global));
        }

        #endregion

        #region Circle and simplicity

        [Fact]
        public void Circle_OffsetChangesOnlyTheRadius()
        {
            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 0), 5.0);

            Assert.True(circle.TryOffset(2.0, out GeoCircle3 grown));
            Assert.Equal(7.0, grown.Radius, 12);
            Assert.True(grown.Normal.IsEqualTo(circle.Normal));
            Assert.False(circle.TryOffset(-5.0, out GeoCircle3 unchanged));
            Assert.Equal(circle, unchanged);
            Assert.Equal(Offset3.TryOffset(circle, 1.0, out GeoCircle3 a), circle.TryOffset(1.0, out GeoCircle3 b, Tolerance.Global));
            Assert.Equal(a, b);
        }

        [Fact]
        public void IsSimple_TellsCrossingsTouchesAndFolds()
        {
            Assert.True(Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10).IsSimple());
            Assert.True(Tilted.Polygon(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10).IsSimple());
            Assert.False(Tilted.Polygon(0, 0, 10, 10, 10, 0, 0, 10, -5, 5).IsSimple());
            Assert.False(Tilted.Polygon(0, 0, 10, 0, 10, 10, 5, 0.00005, 0, 10).IsSimple());
            Assert.False(Tilted.Polygon(0, 0, 10, 0, 5, 0, 5, 5).IsSimple());

            GeoPolygon3 square = Tilted.Polygon(0, 0, 10, 0, 10, 10, 0, 10);
            Assert.Equal(Intersection3.IsSimple(square), square.IsSimple(Tolerance.Global));
            Assert.Throws<ArgumentNullException>(() => Intersection3.IsSimple(null));
        }

        #endregion
    }
}
