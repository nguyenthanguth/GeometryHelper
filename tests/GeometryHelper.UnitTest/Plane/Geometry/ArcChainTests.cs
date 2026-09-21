using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Chains and loops that carry arcs: the edge that may bulge, the chain built from bulges as AutoCAD
    /// holds one, and the crossing into the straight world where arcs become chords.
    /// </summary>
    public class ArcChainTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        [Fact]
        public void AnEdgeIsAStraightSegmentUntilItCarriesABulge()
        {
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(30, 40));

            Assert.False(straight.IsArc);
            Assert.Equal(50.0, straight.Length, 9);
            Assert.Equal(50.0, straight.GetChord().Length, 9);
            Assert.True(straight.ToLine().EndPoint.IsEqualTo(new GeoPoint2(30, 40), Tight));
            Assert.Throws<InvalidOperationException>(() => straight.ToArc());

            // A half turn: the arc is longer than the chord by exactly pi over two.
            var bulged = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), 1.0);

            Assert.True(bulged.IsArc);
            Assert.Equal(20.0, bulged.GetChord().Length, 9);
            Assert.Equal(Math.PI * 10.0, bulged.Length, 9);
            Assert.Equal(1.0, bulged.ToArc().Bulge, 9);
            Assert.Throws<InvalidOperationException>(() => bulged.ToLine());
        }

        [Fact]
        public void AnEdgeMeasuresAndReversesLikeTheArcItHolds()
        {
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), 0.5);
            GeoArc2 arc = edge.ToArc();

            Assert.True(edge.GetPointAtParameter(0.5).IsEqualTo(arc.MidPoint, Tight));
            Assert.Equal(0.0, edge.DistanceTo(arc.MidPoint), 8);
            Assert.True(edge.GetClosestPointOnBoundary(new GeoPoint2(10, -100)).IsEqualTo(arc.MidPoint, new Tolerance(1E-6, 1E-6)));

            GeoEdge2 back = edge.Reverse();
            Assert.Equal(-0.5, back.Bulge, 12);
            Assert.True(back.StartPoint.IsEqualTo(edge.EndPoint, Tight));
            Assert.Equal(edge.Length, back.Length, 9);

            // Round trip through the arc keeps the number AutoCAD stores.
            Assert.Equal(edge.Bulge, new GeoEdge2(edge.ToArc()).Bulge, 12);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), double.NaN));
        }

        [Fact]
        public void AChainHoldsVerticesAndBulgesTheWayAPolylineDoes()
        {
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(40, 20) },
                new[] { 1.0, 0.0 });

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(2, chain.EdgeCount);
            Assert.True(chain.HasArcs);
            Assert.Equal(1.0, chain.GetBulgeAt(0), 12);
            Assert.Equal(0.0, chain.GetBulgeAt(1), 12);

            Assert.True(chain.GetEdgeAt(0).IsArc);
            Assert.False(chain.GetEdgeAt(1).IsArc);

            // Measured along the arc, not across it.
            Assert.Equal(Math.PI * 10.0 + Math.Sqrt(800.0), chain.Length, 8);

            // A chain with no bulges says so.
            Assert.False(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 1) }).HasArcs);
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc2(new[] { new GeoPoint2(0, 0) }));
            Assert.Throws<ArgumentNullException>(() => new GeoPolylineArc2((System.Collections.Generic.IEnumerable<GeoPoint2>)null));
        }

        [Fact]
        public void AChainCanBeBuiltFromEdgesThatRunEndToEnd()
        {
            var edges = new[]
            {
                new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), 1.0),
                new GeoEdge2(new GeoPoint2(20, 0), new GeoPoint2(40, 20))
            };

            var chain = new GeoPolylineArc2(edges);

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(1.0, chain.GetBulgeAt(0), 12);

            // Edges that do not meet are refused rather than joined with a jump.
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc2(new[]
            {
                new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0)),
                new GeoEdge2(new GeoPoint2(25, 0), new GeoPoint2(40, 0))
            }));
        }

        [Fact]
        public void WideningAStraightChainLosesNothingAndFlatteningBringsItBack()
        {
            var straight = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var widened = new GeoPolylineArc2(straight);

            Assert.False(widened.HasArcs);
            Assert.Equal(straight.Length, widened.Length, 9);

            // No arcs, so nothing to approximate: the same chain comes back.
            GeoPolyline2 back = widened.Flatten();
            Assert.True(back.IsEqualTo(straight));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(2.0)]
        [InlineData(0.05)]
        public void FlatteningKeepsEveryPieceWithinTheChordTolerance(double chordTolerance)
        {
            // A quarter turn of radius 1000 between two straight runs.
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(-500, 0), new GeoPoint2(0, 0), new GeoPoint2(1000, 1000), new GeoPoint2(1500, 1000) },
                new[] { 0.0, Math.Tan(Math.PI / 8.0), 0.0 });

            GeoPolyline2 flat = chain.Flatten(chordTolerance);
            GeoArc2 arc = chain.GetEdgeAt(1).ToArc();
            double allowed = chordTolerance > 0.0 ? chordTolerance : arc.Radius * 0.002;

            // The ends are kept exactly, and the straight runs are not cut up.
            Assert.True(flat[0].IsEqualTo(chain[0], Tight));
            Assert.True(flat[flat.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1], Tight));
            Assert.True(flat.VertexCount > chain.VertexCount);

            // Every vertex of the flattened arc lies on it, and every chord stays within the tolerance.
            for (int i = 0; i < flat.VertexCount - 1; i++)
            {
                GeoPoint2 middle = new GeoPoint2((flat[i].X + flat[i + 1].X) * 0.5, (flat[i].Y + flat[i + 1].Y) * 0.5);

                if (arc.DistanceTo(flat[i]) <= 1E-6 && arc.DistanceTo(flat[i + 1]) <= 1E-6)
                {
                    Assert.True(arc.DistanceTo(middle) <= allowed + 1E-9);
                }
            }
        }

        [Fact]
        public void ALoopClosesItselfAndFlattensToAPolygon()
        {
            // A square running counter-clockwise, with its top side bulged into a half circle. The side runs
            // from (100,100) to (0,100), so a negative bulge sweeps clockwise and leans into the square.
            var scooped = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 0.0, -1.0, 0.0 });

            Assert.Equal(4, scooped.VertexCount);
            Assert.Equal(4, scooped.EdgeCount);
            Assert.True(scooped.HasArcs);

            // Three straight sides of 100 and one half circle of radius 50, whichever way it leans.
            Assert.Equal(300.0 + Math.PI * 50.0, scooped.Length, 8);

            GeoPolygon2 flat = scooped.Flatten();
            Assert.True(flat.VertexCount > 4);

            // Scooped out: the half circle is taken off the square, and the chords lie inside the arc so a
            // little of it comes back.
            double halfCircle = Math.PI * 50.0 * 50.0 * 0.5;
            Assert.True(flat.Area < 100.0 * 100.0);
            Assert.InRange(flat.Area, 100.0 * 100.0 - halfCircle, 100.0 * 100.0 - halfCircle * 0.99);

            // The same side with the other sign leans out instead, and adds the half circle on.
            var domed = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 0.0, 1.0, 0.0 });

            Assert.Equal(scooped.Length, domed.Length, 8);
            Assert.InRange(domed.Flatten().Area, 100.0 * 100.0 + halfCircle * 0.99, 100.0 * 100.0 + halfCircle);

            // A repeated first vertex says nothing a loop does not already say.
            var repeated = new GeoPolygonArc2(new[]
            {
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 0)
            });
            Assert.Equal(3, repeated.VertexCount);
            Assert.Throws<ArgumentException>(() => new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 1) }));
        }

        [Fact]
        public void ReversingAChainOrALoopTurnsEveryArcWithIt()
        {
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(40, 0) },
                new[] { 0.7, -0.3 });

            GeoPolylineArc2 back = chain.Reverse();

            Assert.Equal(chain.Length, back.Length, 8);
            Assert.True(back[0].IsEqualTo(chain[chain.VertexCount - 1], Tight));
            Assert.Equal(0.3, back.GetBulgeAt(0), 12);
            Assert.Equal(-0.7, back.GetBulgeAt(1), 12);
            Assert.True(chain.IsEqualTo(back.Reverse()));

            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(50, 80) },
                new[] { 0.4, 0.0, -0.2 });

            GeoPolygonArc2 other = loop.Reverse();
            Assert.Equal(loop.Length, other.Length, 8);
            Assert.True(loop.IsEqualTo(other.Reverse()));
            Assert.False(loop.IsEqualTo(other));
        }

        [Fact]
        public void EqualityComparesTheCurveAndIgnoresWhereALoopStarts()
        {
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.5, 0.0, 0.0, 0.0 });

            var started = new GeoPolygonArc2(
                new[] { new GeoPoint2(100, 100), new GeoPoint2(0, 100), new GeoPoint2(0, 0), new GeoPoint2(100, 0) },
                new[] { 0.0, 0.0, 0.5, 0.0 });

            Assert.True(loop.IsEqualTo(started));
            Assert.Equal(loop, loop.Clone());
            Assert.Equal(loop.GetHashCode(), loop.Clone().GetHashCode());
            Assert.False(loop.Equals(started));   // exactly equal means the same starting vertex too
            Assert.Contains("GeoPolygonArc2", loop.ToString());
        }
        [Fact]
        public void ALoopGivesTheAreaItEnclosesWithoutFlatteningFirst()
        {
            GeoPoint2[] square =
            {
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100)
            };

            double halfCircle = Math.PI * 50.0 * 50.0 * 0.5;

            // Exact, not an approximation: the square plus or minus the half disc the side draws.
            Assert.Equal(10000.0 + halfCircle, new GeoPolygonArc2(square, new[] { 0.0, 0.0, 1.0, 0.0 }).Area, 9);
            Assert.Equal(10000.0 - halfCircle, new GeoPolygonArc2(square, new[] { 0.0, 0.0, -1.0, 0.0 }).Area, 9);
            Assert.Equal(10000.0, new GeoPolygonArc2(square).Area, 9);

            // Rounding the corners takes off the square of the radius less its quarter disc, four times.
            GeoPolygonArc2 rounded = new GeoPolygonArc2(square).Fillet(10.0);
            Assert.Equal(10000.0 - 4.0 * (100.0 - Math.PI * 100.0 * 0.25), rounded.Area, 9);

            // Flattening lands on the same answer as the tolerance tightens, from inside.
            Assert.True(rounded.Flatten(1.0).Area < rounded.Flatten(0.001).Area);
            Assert.Equal(rounded.Area, rounded.Flatten(0.0001).Area, 2);

            // The sign follows the way the loop runs; the area itself does not.
            GeoPolygonArc2 other = rounded.Reverse();
            Assert.False(rounded.IsClockwise);
            Assert.True(other.IsClockwise);
            Assert.Equal(rounded.Area, other.Area, 9);
            Assert.Equal(-rounded.SignedArea, other.SignedArea, 9);
        }
    }
}
