using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A curved loop grown until a gap closes gives the space it closes round as a hole, wound against the loop, as the
    /// straight offsets give it, however the hole's pieces were put together.
    /// </summary>
    /// <remarks>
    /// A hole runs against the loop it came from, and so does a fold turned inside out. Two such loops were taken for
    /// folds by their winding alone and dropped: one put together only out of the order its runs were cut in, and a lens
    /// of two arcs. A hole lies inside a loop running as the source runs; a fold turned inside out does not. Every
    /// expected area is computed by hand, as written beside it, and agrees with the straight path on the source
    /// flattened to 0.0005.
    /// </remarks>
    public class OffsetFaultS9HoleTests
    {
        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        /// <summary>The loop begun from every vertex, and run the other way.</summary>
        private static IEnumerable<GeoPolygonArc2> EveryWay(GeoPolygonArc2 loop)
        {
            int n = loop.VertexCount;

            for (int start = 0; start < n; start++)
            {
                yield return new GeoPolygonArc2(
                    Enumerable.Range(0, n).Select(i => loop.Vertices[(i + start) % n]),
                    Enumerable.Range(0, n).Select(i => loop.Bulges[(i + start) % n]));
            }

            yield return loop.Reverse();
        }

        /// <summary>The loops that run against the source.</summary>
        private static GeoPolygonArc2[] Holes(GeoPolygonArc2[] loops, GeoPolygonArc2 source)
            => loops.Where(loop => loop.IsClockwise != source.IsClockwise).ToArray();

        /// <summary>
        /// The square of side 100 about the origin with a lens cut out of it, tips (-20, 0) and the mouth (20, -0.5) to
        /// (20, 0.5), each side an arc of bulge -0.5 sagging 10 from its chord, radius 25.00195, the slot 1 wide out to
        /// the right side.
        /// </summary>
        private static GeoPolygonArc2 LensCavity() => Loop(
            new[] { P(-50, -50), P(50, -50), P(50, -0.5), P(20, -0.5), P(-20, 0), P(20, 0.5), P(50, 0.5), P(50, 50), P(-50, 50) },
            0.0, 0.0, 0.0, -0.5, -0.5);

        /// <summary>
        /// The square of side 100 about the origin with a cavity 60 by 60 whose bottom and top bow 20 into it, bulge 2/3,
        /// radius 32.5 about (0, -42.5) and (0, 42.5), opened by a slot 2 wide from (30, 25) out to the right side.
        /// </summary>
        private static GeoPolygonArc2 WaistedCavity() => Loop(
            new[] { P(-50, -50), P(50, -50), P(50, 25), P(30, 25), P(30, -30), P(-30, -30), P(-30, 30), P(30, 30), P(30, 27), P(50, 27), P(50, 50), P(-50, 50) },
            0.0, 0.0, 0.0, 0.0, 2.0 / 3.0, 0.0, 2.0 / 3.0);

        [Fact]
        public void ALensShapedCavityGrownUntilItsMouthCloses_ComesBackAsAHoleOfTwoArcs()
        {
            // The bottom side runs from (20, -0.5) to (-20, 0): chord c = sqrt(1 600.25), radius c (1 + 0.25) / 2, centre
            // (0.1875, 14.75) by the chord's middle and the sagitta c / 4. Grown by 3, its offset is the circle of radius
            // R = r - 3 about it, and the top side's the same circle mirrored in y = 0, so the hole is the lens of the two:
            // twice R^2 acos(h / R) - h sqrt(R^2 - h^2), h = 14.75, 327.8416, tips at 0.1875 -+ sqrt(R^2 - h^2) on y = 0.
            // Both tips stand further than 3 from the mouth's corners, so nothing of the mouth is left in the hole.
            double chord = Math.Sqrt(1600.25);
            double radius = chord * 1.25 / 2.0 - 3.0;
            double half = Math.Sqrt(radius * radius - 14.75 * 14.75);
            double lens = 2.0 * (radius * radius * Math.Acos(14.75 / radius) - 14.75 * half);

            foreach (GeoPolygonArc2 source in EveryWay(LensCavity()))
            {
                GeoPolygonArc2[] grown = Offset2.Offset(source, 3.0, Round, Thousandth);

                Assert.Equal(2, grown.Length);
                GeoPolygonArc2 hole = Assert.Single(Holes(grown, source));
                Assert.Equal(lens, hole.Area, 1E-6);
                Assert.Contains(hole.Vertices, v => v.DistanceTo(P(0.1875 - half, 0)) <= 1E-6);
                Assert.Contains(hole.Vertices, v => v.DistanceTo(P(0.1875 + half, 0)) <= 1E-6);
                Assert.True(WorstOffDistance(PiecesOf(hole), PiecesOf(source), 3.0) <= 1E-9);
            }
        }

        [Fact]
        public void ACavityWhoseSidesBowInGrownUntilItPinches_ComesBackAsTwoHoles()
        {
            // Grown by 11 the bowed sides move out to radius R = 43.5 about (0, -+42.5) and pinch the cavity at
            // x = -+sqrt(R^2 - 42.5^2); each side of the pinch keeps the hole between x = -+19 and there, outside both
            // circles: 2 (42.5 (x0 - a) - (F(x0) - F(a))), F(x) = (x sqrt(R^2 - x^2) + R^2 asin(x / R)) / 2, 28.57016.
            // The slot is far enough up for nothing of it to reach either hole.
            const double R = 43.5;
            double x0 = -Math.Sqrt(R * R - 42.5 * 42.5);
            double a = -19.0;
            Func<double, double> f = x => (x * Math.Sqrt(R * R - x * x) + R * R * Math.Asin(x / R)) / 2.0;
            double each = 2.0 * (42.5 * (x0 - a) - (f(x0) - f(a)));

            foreach (GeoPolygonArc2 source in EveryWay(WaistedCavity()))
            {
                GeoPolygonArc2[] grown = Offset2.Offset(source, 11.0, Round, Thousandth);
                GeoPolygonArc2[] holes = Holes(grown, source);

                Assert.Equal(3, grown.Length);
                Assert.Equal(2, holes.Length);
                Assert.All(holes, hole => Assert.Equal(each, hole.Area, 1E-6));
                Assert.All(holes, hole => Assert.True(WorstOffDistance(PiecesOf(hole), PiecesOf(source), 11.0) <= 1E-9));
            }
        }

        [Fact]
        public void ARingWhoseMouthClosesInOrder_KeepsItsHoleAsBefore()
        {
            // Guard: the square of side 100 with a cavity 60 by 60 opened by a mouth 4 wide, grown by 3 with round corners.
            // The hole is the cavity shrunk to 54 by 54, 2 916, and the bit between the circles of 3 about the mouth's
            // corners (80, 48) and (80, 52) left of x = 77: 2 (6 - sqrt 5 - 4.5 asin(2/3)), 0.95950.
            GeoPolygonArc2 ring = Loop(new[]
            {
                P(0, 0), P(100, 0), P(100, 48), P(80, 48), P(80, 20), P(20, 20), P(20, 80), P(80, 80), P(80, 52), P(100, 52),
                P(100, 100), P(0, 100),
            });

            GeoPolygonArc2[] grown = Offset2.Offset(ring, 3.0, Round, Thousandth);

            Assert.Equal(2, grown.Length);
            GeoPolygonArc2 hole = Assert.Single(Holes(grown, ring));
            Assert.Equal(2916.0 + 2.0 * (6.0 - Math.Sqrt(5.0) - 4.5 * Math.Asin(2.0 / 3.0)), hole.Area, 1E-6);
        }

        [Fact]
        public void ALensTurnedInsideOutWhereNothingIsLeft_IsStillNoAnswer()
        {
            // Guard: the notch the audit drew as GA 298, shrunk by 2.5 at 0.1 with an unlimited miter. Nothing of it stands
            // 2.5 inside, and the straight path gives nothing too; what is left of the fold is a lens of 0.033 running
            // against the loop, inside no loop that runs with it.
            GeoPolygonArc2 notch = Loop(
                new[]
                {
                    P(-19.17112017687155, 12.203761643576019), P(-16.730586243320037, 16.923089717676937),
                    P(-13.185408897953149, 15.089751268649046), P(-12.629366363844245, 16.164986120145521),
                    P(-16.174543709211132, 17.998324569173413), P(-14.577630229355096, 21.08632040229331),
                    P(-8.35983909825299, 17.870877439031791), P(-12.953329045769447, 8.9883186803145),
                },
                0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, 0.1);
            var options = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity, 0.0);

            Assert.Empty(Offset2.Offset(notch, -2.5, options, Tenth));
        }
    }
}
