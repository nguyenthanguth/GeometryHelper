using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Growing and shrinking regions (Offset2 for polygons and faces), checked three ways: against results
    /// worked out by hand and by formula, against the properties an offset must have, and against an
    /// independent library (Clipper2, referenced by this test project only) on random polygons, where the
    /// area of the symmetric difference between the two answers must vanish.
    /// </summary>
    public class PolygonOffsetTests
    {
        private static GeoPolygon2 P(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) { points[i] = new GeoPoint2(xy[2 * i], xy[2 * i + 1]); }
            return new GeoPolygon2(points);
        }

        private static readonly GeoPolygon2 Square = P(0, 0, 10, 0, 10, 10, 0, 10);

        private static readonly OffsetOptions SharpAlways = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);

        private static void AssertSameLoop(GeoPolygon2 expected, GeoPolygon2 actual, double tolerance = 1e-9)
        {
            Assert.Equal(expected.VertexCount, actual.VertexCount);
            Assert.True(expected.IsClockwise == actual.IsClockwise, "the loops run opposite ways");

            // Same loop, possibly starting at another vertex.
            int shift = Enumerable.Range(0, actual.VertexCount).FirstOrDefault(k => actual[k].DistanceTo(expected[0]) <= tolerance);
            for (int i = 0; i < expected.VertexCount; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[(i + shift) % actual.VertexCount]) <= tolerance,
                    $"vertex {i}: expected {expected[i]}, got {actual[(i + shift) % actual.VertexCount]}");
            }
        }

        #region Exact results

        [Fact]
        public void Square_GrowsAndShrinksExactlyWithSharpCorners()
        {
            GeoPolygon2[] grown = Square.Offset(2.0);
            Assert.Single(grown);
            AssertSameLoop(P(-2, -2, 12, -2, 12, 12, -2, 12), grown[0]);

            GeoPolygon2[] shrunk = Square.Offset(-2.0);
            Assert.Single(shrunk);
            AssertSameLoop(P(2, 2, 8, 2, 8, 8, 2, 8), shrunk[0]);
        }

        [Fact]
        public void Square_ShrunkToOrPastItsHalfWidth_Vanishes()
        {
            Assert.Empty(Square.Offset(-5.0));
            Assert.Empty(Square.Offset(-6.0));
            Assert.Single(Square.Offset(-4.9));
        }

        [Fact]
        public void Square_WithEachJoin_HasTheAreaTheFormulaGives()
        {
            const double d = 2.0;

            // Sharp: every side moves out by d.
            Assert.Equal(14.0 * 14.0, Square.Offset(d, OffsetJoin.Miter)[0].Area, 9);

            // Chamfer: each quarter-turn corner adds 2 d^2 tan(22.5 deg) rather than d^2.
            double chamfer = 100 + 40 * d + 4 * 2 * d * d * Math.Tan(Math.PI / 8);
            Assert.Equal(chamfer, Square.Offset(d, OffsetJoin.Chamfer)[0].Area, 9);

            // Round: Steiner's formula, approached from below by chords no farther than the arc tolerance.
            double exact = 100 + 40 * d + Math.PI * d * d;
            double round = Square.Offset(d, OffsetJoin.Round)[0].Area;
            Assert.True(round < exact);
            Assert.True(exact - round < 4 * (Math.PI / 2 * d) * OffsetOptions.AutomaticArcToleranceRatio * d, $"round area {round} too far below {exact}");

            // A finer arc tolerance gets closer.
            double fine = Square.Offset(d, new OffsetOptions(OffsetJoin.Round, arcTolerance: 1e-6))[0].Area;
            Assert.True(exact - fine < 1e-4);
        }

        [Fact]
        public void ConvexPolygon_SharpOffset_MatchesTheMiterFormula()
        {
            // A' = A + P d + d^2 * sum(tan(turn / 2)) for a convex polygon grown with sharp corners.
            var hexagon = P(0, 0, 6, -1, 10, 3, 9, 8, 3, 9, -1, 5);
            const double d = 1.5;
            double turns = 0.0;

            for (int i = 0; i < hexagon.VertexCount; i++)
            {
                GeoVector2 incoming = hexagon.GetEdgeAt((i - 1 + hexagon.VertexCount) % hexagon.VertexCount).Direction;
                GeoVector2 outgoing = hexagon.GetEdgeAt(i).Direction;
                turns += Math.Tan(Math.Abs(incoming.GetSignedAngleTo(outgoing)) / 2.0);
            }

            double expected = hexagon.Area + hexagon.Length * d + d * d * turns;
            Assert.Equal(expected, hexagon.Offset(d, SharpAlways)[0].Area, 8);
        }

        [Fact]
        public void ConvexPolygon_GrownThenShrunk_ComesBack()
        {
            var hexagon = P(0, 0, 6, -1, 10, 3, 9, 8, 3, 9, -1, 5);

            GeoPolygon2 grown = hexagon.Offset(2.0, SharpAlways).Single();
            GeoPolygon2 back = grown.Offset(-2.0, SharpAlways).Single();

            AssertSameLoop(hexagon, back, 1e-8);
        }

        [Fact]
        public void LShape_GrowsAndShrinksExactly()
        {
            var l = P(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10);

            AssertSameLoop(P(-1, -1, 11, -1, 11, 5, 5, 5, 5, 11, -1, 11), l.Offset(1.0).Single());
            AssertSameLoop(P(1, 1, 9, 1, 9, 3, 3, 3, 3, 9, 1, 9), l.Offset(-1.0).Single());
        }

        [Fact]
        public void Clockwise_Polygon_ComesBackClockwise()
        {
            var clockwise = P(0, 0, 0, 10, 10, 10, 10, 0);

            GeoPolygon2 grown = clockwise.Offset(2.0).Single();
            Assert.True(grown.IsClockwise);
            Assert.Equal(196.0, grown.Area, 9);

            // Growing is outward whichever way the vertices run.
            Assert.Equal(36.0, clockwise.Offset(-2.0).Single().Area, 9);
        }

        [Fact]
        public void Dumbbell_ShrunkPastItsNeck_SplitsInTwo()
        {
            var dumbbell = P(0, 0, 10, 0, 10, 4, 14, 4, 14, 0, 24, 0, 24, 10, 14, 10, 14, 6, 10, 6, 10, 10, 0, 10);

            GeoPolygon2[] pieces = dumbbell.Offset(-1.5);

            Assert.Equal(2, pieces.Length);
            AssertSameLoop(P(1.5, 1.5, 8.5, 1.5, 8.5, 8.5, 1.5, 8.5), pieces.OrderBy(p => p[0].X).First());
            AssertSameLoop(P(15.5, 1.5, 22.5, 1.5, 22.5, 8.5, 15.5, 8.5), pieces.OrderBy(p => p[0].X).Last());

            // Less than half the neck: still one piece.
            Assert.Single(dumbbell.Offset(-0.9));
        }

        [Fact]
        public void TwoPiecesTouchingAtAPoint_ComeBackAsTwoSimpleLoops()
        {
            // Two 4 x 4 squares overlapping in a 2 x 2 corner, shrunk by 1 with sharp corners: what is left is
            // two squares that touch at exactly one point, (3, 3). They must come back as two loops, not as one
            // loop crossing itself there.
            var overlapping = P(0, 0, 4, 0, 4, 2, 6, 2, 6, 6, 2, 6, 2, 4, 0, 4);

            GeoPolygon2[] pieces = overlapping.Offset(-1.0);

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.True(piece.IsSimple()));
            AssertSameLoop(P(1, 1, 3, 1, 3, 3, 1, 3), pieces.OrderBy(p => p.Vertices.Min(v => v.X)).First());
            AssertSameLoop(P(3, 3, 5, 3, 5, 5, 3, 5), pieces.OrderBy(p => p.Vertices.Min(v => v.X)).Last());
        }

        [Fact]
        public void SharpNotch_GrownPartlyClosed_MatchesClipper()
        {
            // A V notch 14 degrees wide cut 8 deep into a slab. At its bottom the outline turns back by more than
            // a right angle, where the offset edges pass each other without crossing; only the detour through
            // the corner keeps the notch bottom filled.
            var notched = P(0, 0, 20, 0, 20, 10, 11, 10, 10, 2, 9, 10, 0, 10);

            foreach (double d in new[] { 0.5, 1.0, -0.5 })
            {
                foreach ((OffsetJoin ours, JoinType theirs) in new[] { (OffsetJoin.Miter, JoinType.Miter), (OffsetJoin.Chamfer, JoinType.Square) })
                {
                    GeoPolygon2[] result = notched.Offset(d, new OffsetOptions(ours, double.PositiveInfinity));
                    PathsD expected = Oracle(new[] { notched }, d, theirs, Clipper2Lib.FillRule.NonZero);

                    Assert.True(XorArea(ToPaths(result), expected) < 1e-6, $"offset {d} {ours} differs from Clipper");
                    Assert.All(result, loop => Assert.True(loop.IsSimple()));
                }
            }
        }

        [Fact]
        public void SmallPolygonShrunkPastItsWidth_Vanishes()
        {
            // Found by fuzzing with the detour through inside corners taken out of the raw offset: ten short edges
            // about two units across, shrunk by three. Nothing can be left, and the detours are what make the
            // raw loop's folds cancel; without them a piece of 2.1 square units survived.
            var walk = P(1.29120435487293, 0.8529122511717194, 2.0692576855245406, 1.8246495197613428, 2.2463920430178077, 2.5563866007258804,
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
            // Found by fuzzing against Clipper: teeth narrower than twice the distance, shrunk away. Where a
            // tooth is shorter across than the offset, the offset edges of its two sides pass each other without
            // crossing, and only the detour through each original corner keeps the base's outline right.
            var comb = P(0, 0, 12.556810211975506, 0, 12.556810211975506, 1,
                         12.346807620649603, 1, 12.346807620649603, 5.422796114544755, 11.030278669032398, 5.422796114544755,
                         11.030278669032398, 1, 9.675089881091886, 1, 9.675089881091886, 6.886248821339686,
                         9.400917458069006, 6.886248821339686, 9.400917458069006, 1, 8.00142274857565, 1,
                         8.00142274857565, 3.632585188668494, 7.289240777813476, 3.632585188668494, 7.289240777813476, 1,
                         6.227419358178703, 1, 6.227419358178703, 7.821611514697602, 5.277756546427383, 7.821611514697602,
                         5.277756546427383, 1, 4.161423784103907, 1, 4.161423784103907, 6.20141693353719,
                         3.7245509831349137, 6.20141693353719, 3.7245509831349137, 1, 2.459634663052687, 1,
                         2.459634663052687, 8.855549968711822, 2.1069624506435183, 8.855549968711822, 2.1069624506435183, 1,
                         0.6055348466641897, 1, 0.6055348466641897, 4.003288449255418, 0, 4.003288449255418,
                         0, 1);

            foreach (double d in new[] { -0.3, 0.3, -0.12 })
            {
                GeoPolygon2[] round = comb.Offset(d, new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.001));
                PathsD expectedRound = Oracle(new[] { comb }, d, JoinType.Round, Clipper2Lib.FillRule.NonZero, 0.001);
                Assert.True(XorArea(ToPaths(round), expectedRound) < 0.002 * (round.Sum(p => p.Length) + 1.0), $"round offset {d} differs from Clipper");

                GeoPolygon2[] sharp = comb.Offset(d, SharpAlways);
                PathsD expectedSharp = Oracle(new[] { comb }, d, JoinType.Miter, Clipper2Lib.FillRule.NonZero);
                Assert.True(XorArea(ToPaths(sharp), expectedSharp) < 1e-5, $"sharp offset {d} differs from Clipper");
            }
        }

        [Fact]
        public void RingWithASlot_GrownPastTheSlot_EnclosesAHole()
        {
            // A square ring 0..20 round a 5..15 void, cut through at the right by a slot 2 wide.
            var ring = P(0, 0, 20, 0, 20, 9, 15, 9, 15, 5, 5, 5, 5, 15, 15, 15, 15, 11, 20, 11, 20, 20, 0, 20);

            GeoPolygon2[] loops = ring.Offset(1.5);

            Assert.Equal(2, loops.Length);
            AssertSameLoop(P(-1.5, -1.5, 21.5, -1.5, 21.5, 21.5, -1.5, 21.5), loops[0]);

            // The hole follows its outer loop and runs the other way.
            Assert.True(loops[1].IsClockwise);
            Assert.Equal(49.0, loops[1].Area, 9);
            Assert.Equal(529.0 - 49.0, loops.Sum(l => l.SignedArea), 9);

            // As a face, the hole is attached to its boundary.
            GeoFace2 face = new GeoFace2(ring).Offset(1.5).Single();
            Assert.Single(face.Holes);
            Assert.Equal(480.0, face.Area, 9);
        }

        [Fact]
        public void SliverThinPiece_IsNotReturned()
        {
            // Shrinking a 10 x 4 rectangle by 1.99996: what is left is 0.00008 wide, a seam rather than a shape.
            var strip = P(0, 0, 10, 0, 10, 4, 0, 4);

            Assert.Empty(strip.Offset(-1.99996));
            Assert.Single(strip.Offset(-1.99));
        }

        [Fact]
        public void SharpCorner_IsCutOffSquareBeyondTheMiterLimit()
        {
            // A corner of about 2.9 degrees would send the miter 40 distances out.
            var sliver = P(0, 0, 100, 0, 0, 5);

            GeoPolygon2 limited = sliver.Offset(1.0).Single();
            GeoPolygon2 unlimited = sliver.Offset(1.0, SharpAlways).Single();

            Assert.Equal(4, limited.VertexCount);
            Assert.Equal(3, unlimited.VertexCount);
            Assert.True(unlimited.Vertices.Max(v => v.X) > 140);

            // The cut is square to the bisector of the corner, the miter limit times the distance from it.
            GeoPoint2[] cut = limited.Vertices.Where(v => v.X > 100).ToArray();
            Assert.Equal(2, cut.Length);
            Assert.Equal(OffsetOptions.DefaultMiterLimit * 1.0, new GeoLine2(cut[0], cut[1]).GetClosestPointOnBoundary(new GeoPoint2(100, 0)).DistanceTo(new GeoPoint2(100, 0)), 9);
        }

        [Fact]
        public void FarFromTheOrigin_LosesNoPrecision()
        {
            var shift = new GeoVector2(1e6, -2e6);
            GeoPolygon2 grown = Square.Translate(shift).Offset(2.0).Single();

            AssertSameLoop(P(-2, -2, 12, -2, 12, 12, -2, 12).Translate(shift), grown, 1e-8);
        }

        [Fact]
        public void BowTie_IsReadUnderTheEvenOddRule()
        {
            var bow = P(0, 0, 10, 10, 10, 0, 0, 10);

            Assert.False(bow.IsSimple());

            // Two triangles touching at (5, 5), grown into one region.
            GeoPolygon2[] grown = bow.Offset(1.0, SharpAlways);
            Assert.Single(grown);
            Assert.Equal(ClipperArea(Oracle(new[] { bow }, 1.0, JoinType.Miter, Clipper2Lib.FillRule.EvenOdd)), grown.Sum(g => g.SignedArea), 6);
        }

        [Fact]
        public void TinyDistance_ReturnsTheShapeUnchanged()
        {
            GeoPolygon2 same = Square.Offset(0.00005).Single();

            Assert.Equal(Square, same);
            Assert.NotSame(Square, same);
        }

        [Fact]
        public void BadArguments_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Offset2.Offset((GeoPolygon2)null, 1.0));
            Assert.Throws<ArgumentNullException>(() => Square.Offset(1.0, (OffsetOptions)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Square.Offset(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Square.Offset(double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => Offset2.Offset((GeoFace2)null, 1.0));
        }

        [Fact]
        public void CoreAndInstance_Agree()
        {
            var l = P(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10);
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.01);

            Assert.Equal(Offset2.Offset(l, 1.5), l.Offset(1.5));
            Assert.Equal(Offset2.Offset(l, -1.5, Tolerance.Global), l.Offset(-1.5, Tolerance.Global));
            Assert.Equal(Offset2.Offset(l, 1.5, OffsetJoin.Chamfer), l.Offset(1.5, OffsetJoin.Chamfer));
            Assert.Equal(Offset2.Offset(l, 1.5, OffsetJoin.Chamfer, Tolerance.Global), l.Offset(1.5, OffsetJoin.Chamfer, Tolerance.Global));
            Assert.Equal(Offset2.Offset(l, 1.5, options), l.Offset(1.5, options));
            Assert.Equal(Offset2.Offset(l, 1.5, options, Tolerance.Global), l.Offset(1.5, options, Tolerance.Global));
        }

        #endregion

        #region Faces

        [Fact]
        public void Face_GrowingShrinksTheHolesAndShrinkingGrowsThem()
        {
            var face = new GeoFace2(P(0, 0, 20, 0, 20, 20, 0, 20), new[] { P(5, 5, 15, 5, 15, 15, 5, 15) });

            GeoFace2 grown = face.Offset(2.0).Single();
            Assert.Equal(24 * 24 - 6 * 6, grown.Area, 9);
            Assert.Single(grown.Holes);

            GeoFace2 shrunk = face.Offset(-2.0).Single();
            Assert.Equal(16 * 16 - 14 * 14, shrunk.Area, 9);

            // Grown past the hole's half width, the hole closes.
            GeoFace2 closed = face.Offset(6.0).Single();
            Assert.Empty(closed.Holes);
            Assert.Equal(32.0 * 32.0, closed.Area, 9);

            // Shrunk until the frame breaks: the hole grows into the boundary and nothing is left.
            Assert.Empty(face.Offset(-2.5));
        }

        [Fact]
        public void Face_HolesKeepToTheirOwnPiece()
        {
            // Two rooms joined by a corridor, each with a column; shrinking cuts the corridor.
            var outline = P(0, 0, 10, 0, 10, 4, 14, 4, 14, 0, 24, 0, 24, 10, 14, 10, 14, 6, 10, 6, 10, 10, 0, 10);
            var face = new GeoFace2(outline, new[] { P(4, 4, 6, 4, 6, 6, 4, 6), P(18, 4, 20, 4, 20, 6, 18, 6) });

            GeoFace2[] rooms = face.Offset(-1.5);

            Assert.Equal(2, rooms.Length);
            Assert.All(rooms, room => Assert.Single(room.Holes));
            Assert.All(rooms, room => Assert.Equal(7 * 7 - 5 * 5, room.Area, 9));
        }

        [Fact]
        public void Face_ContainmentReadsHolesAsOutside()
        {
            var face = new GeoFace2(P(0, 0, 20, 0, 20, 20, 0, 20), new[] { P(5, 5, 15, 5, 15, 15, 5, 15) });

            Assert.Equal(PointLocation.Inside, face.Locate(new GeoPoint2(2, 2)));
            Assert.Equal(PointLocation.OutSide, face.Locate(new GeoPoint2(10, 10)));
            Assert.Equal(PointLocation.OnSide, face.Locate(new GeoPoint2(5, 10)));
            Assert.Equal(PointLocation.OnSide, face.Locate(new GeoPoint2(0, 10)));
            Assert.Equal(PointLocation.OutSide, face.Locate(new GeoPoint2(30, 10)));
            Assert.True(face.Contains(new GeoPoint2(5, 10)));
            Assert.False(face.Contains(new GeoPoint2(10, 10)));
            Assert.Equal(Containment2.Locate(face, new GeoPoint2(2, 2)), face.Locate(new GeoPoint2(2, 2), Tolerance.Global));
        }

        [Fact]
        public void Face_MeasuresAndCopies()
        {
            var face = new GeoFace2(P(0, 0, 20, 0, 20, 20, 0, 20), new[] { P(5, 5, 15, 5, 15, 15, 5, 15) });

            Assert.Equal(300.0, face.Area, 9);
            Assert.Equal(80.0 + 40.0, face.Length, 9);
            Assert.Equal(face, face.Clone());
            Assert.NotSame(face.Boundary, face.Clone().Boundary);
            Assert.Equal(face.Area, face.Translate(new GeoVector2(3, 4)).Area, 9);
            Assert.Equal(face.Area, face.RotateBy(0.7, new GeoPoint2(1, 1)).Area, 6);
            Assert.Throws<ArgumentNullException>(() => new GeoFace2(null));
            Assert.Throws<ArgumentException>(() => new GeoFace2(Square, new GeoPolygon2[] { null }));
            Assert.Contains("Holes:1", face.ToString());
        }

        #endregion

        #region Properties

        private static IEnumerable<GeoPolygon2> RandomStars(int seed, int count)
        {
            var rng = new Random(seed);

            for (int t = 0; t < count; t++)
            {
                int n = 5 + rng.Next(30);
                var points = new GeoPoint2[n];
                double cx = rng.NextDouble() * 20 - 10, cy = rng.NextDouble() * 20 - 10;

                for (int i = 0; i < n; i++)
                {
                    double angle = 2 * Math.PI * i / n + rng.NextDouble() * 0.5 / n;
                    double radius = 3 + rng.NextDouble() * 12;
                    points[i] = new GeoPoint2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                }

                yield return new GeoPolygon2(points);
            }
        }

        [Fact]
        public void RoundOffset_KeepsEveryVertexAtTheDistanceFromTheOriginal()
        {
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.001);
            int checkedVertices = 0;

            foreach (GeoPolygon2 star in RandomStars(41, 60))
            {
                foreach (double d in new[] { 0.7, -0.7, 2.5, -1.6 })
                {
                    foreach (GeoPolygon2 loop in star.Offset(d, options))
                    {
                        foreach (GeoPoint2 v in loop.Vertices)
                        {
                            double distance = star.GetClosestPointOnBoundary(v).DistanceTo(v);

                            // Vertices where two chords cross sit slightly inside the true arc.
                            Assert.InRange(distance, Math.Abs(d) - 0.0011, Math.Abs(d) + 1e-7);
                            Assert.Equal(d > 0 ? PointLocation.OutSide : PointLocation.Inside, star.Locate(v));
                            checkedVertices++;
                        }
                    }
                }
            }

            Assert.True(checkedVertices > 5000);
        }

        [Fact]
        public void EveryResult_IsSimple()
        {
            foreach (GeoPolygon2 star in RandomStars(43, 60))
            {
                foreach (double d in new[] { 0.7, -0.7, 2.5, -2.5 })
                {
                    foreach (OffsetJoin join in new[] { OffsetJoin.Miter, OffsetJoin.Round, OffsetJoin.Chamfer })
                    {
                        foreach (GeoPolygon2 loop in star.Offset(d, join))
                        {
                            Assert.True(loop.IsSimple(), $"offset {d} {join} of a star gave a loop that is not simple");
                        }
                    }
                }
            }
        }

        [Fact]
        public void ARigidMotion_MovesTheOffsetWithIt()
        {
            foreach (GeoPolygon2 star in RandomStars(47, 30))
            {
                double angle = 1.234;
                var shift = new GeoVector2(5000, -7000);
                GeoPolygon2 moved = star.RotateBy(angle, new GeoPoint2(0, 0)).Translate(shift);

                foreach (double d in new[] { 1.1, -1.1 })
                {
                    GeoPolygon2[] a = star.Offset(d, OffsetJoin.Round);
                    GeoPolygon2[] b = moved.Offset(d, OffsetJoin.Round);

                    Assert.Equal(a.Length, b.Length);
                    Assert.True(Math.Abs(a.Sum(p => p.SignedArea) - b.Sum(p => p.SignedArea)) < 1e-6);

                    PathsD expected = ToPaths(a.Select(p => p.RotateBy(angle, new GeoPoint2(0, 0)).Translate(shift)));
                    Assert.True(XorArea(expected, ToPaths(b)) < 1e-4);
                }
            }
        }

        #endregion

        #region Against Clipper2

        private static PathsD ToPaths(IEnumerable<GeoPolygon2> polygons)
        {
            var paths = new PathsD();
            foreach (GeoPolygon2 polygon in polygons)
            {
                paths.Add(new PathD(polygon.Vertices.Select(v => new PointD(v.X, v.Y))));
            }
            return paths;
        }

        private static PathsD Oracle(IEnumerable<GeoPolygon2> polygons, double d, JoinType join, Clipper2Lib.FillRule rule, double arcTolerance = 0.0)
        {
            // Clipper inflates a set of paths as the region they fill, so the input is first reduced to that
            // region under the rule our own offset reads it with.
            PathsD region = Clipper.Union(ToPaths(polygons), new PathsD(), rule, 8);
            return Clipper.InflatePaths(region, d, join, EndType.Polygon, 1e9, 8, arcTolerance);
        }

        private static double ClipperArea(PathsD paths) => Clipper.Area(paths);

        private static double XorArea(PathsD a, PathsD b)
        {
            return Math.Abs(Clipper.Area(Clipper.Xor(a, b, Clipper2Lib.FillRule.NonZero, 8)));
        }

        [Theory]
        [InlineData(OffsetJoin.Miter, JoinType.Miter)]
        [InlineData(OffsetJoin.Chamfer, JoinType.Square)]
        public void RandomStars_MatchClipperExactly(OffsetJoin ours, JoinType theirs)
        {
            var options = new OffsetOptions(ours, double.PositiveInfinity);
            int compared = 0;

            foreach (GeoPolygon2 star in RandomStars(2026, 120))
            {
                foreach (double d in new[] { 0.3, -0.3, 1.7, -1.7, 4.0, -4.0 })
                {
                    GeoPolygon2[] result = star.Offset(d, options);
                    PathsD expected = Oracle(new[] { star }, d, theirs, Clipper2Lib.FillRule.NonZero);

                    double xor = XorArea(ToPaths(result), expected);
                    Assert.True(xor < 1e-5 * (1 + Math.Abs(ClipperArea(expected))), $"star {compared}: offset {d} differs from Clipper by area {xor}");
                    compared++;
                }
            }

            Assert.Equal(720, compared);
        }

        [Fact]
        public void RandomStars_RoundMatchClipperWithinTheArcTolerance()
        {
            const double arcTolerance = 0.0005;
            var options = new OffsetOptions(OffsetJoin.Round, arcTolerance: arcTolerance);

            foreach (GeoPolygon2 star in RandomStars(7, 80))
            {
                foreach (double d in new[] { 0.5, -0.5, 2.0, -2.0 })
                {
                    GeoPolygon2[] result = star.Offset(d, options);
                    PathsD expected = Oracle(new[] { star }, d, JoinType.Round, Clipper2Lib.FillRule.NonZero, arcTolerance);

                    // Both draw the arcs as chords within the tolerance, placed differently; the gap between
                    // them is bounded by the tolerance along the length of the outline.
                    double xor = XorArea(ToPaths(result), expected);
                    double outline = result.Sum(p => p.Length) + 1.0;
                    Assert.True(xor < 2.0 * arcTolerance * outline, $"offset {d} differs from Clipper by area {xor}");
                }
            }
        }

        [Fact]
        public void RectilinearRegions_WithManySharedLines_MatchClipper()
        {
            // Unions of grid-aligned rectangles: collinear edges, T junctions and touching corners everywhere,
            // which is what CAD outlines are made of and what breaks careless offsetting.
            var rng = new Random(99);
            int compared = 0;

            for (int t = 0; t < 150; t++)
            {
                var rects = new PathsD();
                for (int k = 0; k < 2 + rng.Next(5); k++)
                {
                    double x = rng.Next(0, 20), y = rng.Next(0, 20), w = 1 + rng.Next(8), h = 1 + rng.Next(8);
                    rects.Add(new PathD(new[] { new PointD(x, y), new PointD(x + w, y), new PointD(x + w, y + h), new PointD(x, y + h) }));
                }

                PathsD region = Clipper.Union(rects, new PathsD(), Clipper2Lib.FillRule.NonZero, 8);

                // Build faces from the region: each outer loop with the holes inside it.
                List<GeoFace2> faces = ToFaces(region);

                foreach (double d in new[] { 0.5, -0.5, 1.0, -1.0, 2.25 })
                {
                    PathsD expected = Clipper.InflatePaths(region, d, JoinType.Miter, EndType.Polygon, 1e9, 8);
                    var ours = new List<GeoPolygon2>();

                    foreach (GeoFace2 face in faces)
                    {
                        foreach (GeoFace2 piece in face.Offset(d, SharpAlways))
                        {
                            ours.Add(piece.Boundary.IsClockwise ? Reverse(piece.Boundary) : piece.Boundary);
                            ours.AddRange(piece.Holes.Select(h => h.IsClockwise ? h : Reverse(h)));
                        }
                    }

                    // Faces are offset one at a time, so growing faces may overlap; union them before comparing.
                    PathsD merged = Clipper.Union(ToPaths(ours), new PathsD(), Clipper2Lib.FillRule.NonZero, 8);
                    double xor = XorArea(merged, expected);
                    Assert.True(xor < 1e-5, $"case {t}, offset {d}: differs from Clipper by area {xor}");
                    compared++;
                }
            }

            Assert.Equal(750, compared);
        }

        private static GeoPolygon2 Reverse(GeoPolygon2 polygon) => new GeoPolygon2(polygon.Vertices.Reverse());

        private static List<GeoFace2> ToFaces(PathsD region)
        {
            var outers = region.Where(Clipper.IsPositive).Select(p => new GeoPolygon2(p.Select(q => new GeoPoint2(q.x, q.y)))).ToList();
            var holes = region.Where(p => !Clipper.IsPositive(p)).Select(p => new GeoPolygon2(p.Select(q => new GeoPoint2(q.x, q.y)))).ToList();
            var faces = new List<GeoFace2>();

            foreach (GeoPolygon2 outer in outers)
            {
                var inside = holes.Where(h => outer.Contains(h.GetEdgeAt(0).MidPoint) &&
                                              !outers.Any(o => o != outer && o.Area < outer.Area && o.Contains(h.GetEdgeAt(0).MidPoint)))
                                  .ToList();
                faces.Add(new GeoFace2(outer, inside));
            }

            return faces;
        }

        #endregion

        #region Simplicity

        [Fact]
        public void IsSimple_TellsCrossingsTouchesAndFoldsFromCleanPolygons()
        {
            Assert.True(Square.IsSimple());
            Assert.True(P(0, 0, 10, 0, 10, 4, 4, 4, 4, 10, 0, 10).IsSimple());

            // Edges crossing.
            Assert.False(P(0, 0, 10, 10, 10, 0, 0, 10).IsSimple());

            // A vertex touching an edge it does not belong to.
            Assert.False(P(0, 0, 10, 0, 10, 10, 5, 0.00005, 0, 10).IsSimple());

            // An edge folding back over its neighbour.
            Assert.False(P(0, 0, 10, 0, 5, 0, 5, 5).IsSimple());

            // Three points in a line.
            Assert.False(P(0, 0, 5, 0, 10, 0).IsSimple());

            Assert.Equal(Intersection2.IsSimple(Square), Square.IsSimple(Tolerance.Global));
            Assert.Throws<ArgumentNullException>(() => Intersection2.IsSimple(null));
        }

        [Fact]
        public void IsSimple_HoldsForEveryStar()
        {
            foreach (GeoPolygon2 star in RandomStars(3, 100))
            {
                Assert.True(star.IsSimple());
            }
        }

        #endregion
    }
}
