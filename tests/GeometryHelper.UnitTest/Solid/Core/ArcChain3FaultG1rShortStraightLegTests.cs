using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The distance from a curved chain or loop in space to a point is measured to each straight edge as it is, however short,
    /// as it is in the plane: a straight edge no longer than the point tolerance was read as its start point, so a point beside
    /// its far end stood as far from the chain as from that start, and could read as off it.
    /// </summary>
    /// <remarks>
    /// Every chain here lies in a plane tilted 30 degrees about X through (1000, -2000, 500), and is drawn in that plane's own
    /// x and y: a straight edge from (-10, 0) to (0, 0), the lower half of the circle of radius 5 about (5, 0) on to (10, 0),
    /// and a straight leg 0.6 tolerances long on up to (10, 0.6 t), along the half circle's way out. The loop closes the bend
    /// and that leg through (0, 10). The expected distances follow from the drawing and are read again off 20 000 points of
    /// each edge.
    /// </remarks>
    public class ArcChain3FaultG1rShortStraightLegTests
    {
        private static readonly GeoPoint3 Origin = new GeoPoint3(1000, -2000, 500);
        private static readonly GeoVector3 AlongX = new GeoVector3(1, 0, 0);
        private static readonly GeoVector3 AlongY = new GeoVector3(0, Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        private static readonly GeoVector3 Normal = new GeoVector3(0, -Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6));

        /// <summary>A tolerance small enough to keep a leg 0.6 tolerances long when the chain or loop is built.</summary>
        private static readonly Tolerance Build = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        /// <summary>The point at x, y in the tilted plane and z over it.</summary>
        private static GeoPoint3 At(double x, double y, double z = 0.0)
            => Origin.Add(AlongX.Multiply(x)).Add(AlongY.Multiply(y)).Add(Normal.Multiply(z));

        /// <summary>The lower half of the circle of radius 5 about (5, 0), from (0, 0) to (10, 0).</summary>
        private static GeoEdge3 Bend() => new GeoEdge3(GeoArc3.FromThreePoints(At(0, 0), At(5, -5), At(10, 0), Build));

        /// <summary>The straight edge, the bend and the short leg 0.6 tolerances long.</summary>
        private static List<GeoEdge3> ChainEdges(double equalPoint) => new List<GeoEdge3>
        {
            new GeoEdge3(At(-10, 0), At(0, 0)),
            Bend(),
            new GeoEdge3(At(10, 0), At(10, 0.6 * equalPoint)),
        };

        /// <summary>The bend, the short leg, and the two straight sides back through (0, 10).</summary>
        private static GeoPolygonArc3 Loop(double equalPoint)
        {
            GeoEdge3 bend = Bend();

            return new GeoPolygonArc3(
                new[] { At(0, 0), At(10, 0), At(10, 0.6 * equalPoint), At(0, 10) },
                new[] { bend.Bulge, 0.0, 0.0, 0.0 },
                new[] { bend.Normal, default(GeoVector3), default(GeoVector3), default(GeoVector3) },
                Build);
        }

        /// <summary>The distance from a run of edges to a point, read off 20 000 points along each edge.</summary>
        private static double Sampled(IEnumerable<GeoEdge3> edges, GeoPoint3 point)
        {
            double best = double.MaxValue;

            foreach (GeoEdge3 edge in edges)
            {
                for (int i = 0; i <= 20000; i++)
                {
                    best = Math.Min(best, edge.GetPointAtParameter(i / 20000.0).DistanceTo(point));
                }
            }

            return best;
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void APointBesideTheEndOfAStraightLegShorterThanTheTolerance_IsTheExactDistanceFromTheChain(double equalPoint)
        {
            // At a tolerance of 0.001, the leg from (10, 0) to (10, 0.0006) and the point 0.0009 beside its end, out from the
            // bend, or 0.0009 over it: 0.0009 from the chain. Read as its start (10, 0), the leg stood 0.00108 away, the root of
            // 0.0006 squared and 0.0009 squared, and nothing else of the chain is nearer.
            Tolerance t = At(equalPoint);
            List<GeoEdge3> edges = ChainEdges(equalPoint);
            var chain = new GeoPolylineArc3(edges);
            GeoPoint3 beside = At(10 + 0.9 * equalPoint, 0.6 * equalPoint);
            GeoPoint3 over = At(10, 0.6 * equalPoint, 0.9 * equalPoint);
            double exact = 0.9 * equalPoint;
            double slack = 1E-11;

            Assert.Equal(3, chain.EdgeCount);
            Assert.InRange(beside.DistanceTo(At(10, 0)), Math.Sqrt(1.17) * equalPoint - slack, Math.Sqrt(1.17) * equalPoint + slack);
            Assert.InRange(Sampled(edges, beside), exact - 1E-9, exact + 1E-9);
            Assert.InRange(chain.DistanceTo(beside, t), exact - slack, exact + slack);
            Assert.InRange(chain.DistanceTo(over, t), exact - slack, exact + slack);
            Assert.InRange(beside.DistanceTo(chain, t), exact - slack, exact + slack);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void APointBesideTheEndOfAStraightLegShorterThanTheTolerance_IsOnTheChain(double equalPoint)
        {
            // The same points, 0.9 tolerances from the chain, are on it; 1.08 tolerances from the leg's start, they were off.
            Tolerance t = At(equalPoint);
            var chain = new GeoPolylineArc3(ChainEdges(equalPoint));
            GeoPoint3 beside = At(10 + 0.9 * equalPoint, 0.6 * equalPoint);
            GeoPoint3 over = At(10, 0.6 * equalPoint, 0.9 * equalPoint);

            Assert.True(chain.IsPointOn(beside, t));
            Assert.True(chain.IsPointOn(over, t));
            Assert.Equal(PointLocation.OnSide, chain.Locate(beside, t));
            Assert.False(chain.IsPointOn(At(10 + 1.1 * equalPoint, 0.6 * equalPoint), t));
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void APointBesideAStraightSideShorterThanTheTolerance_IsTheExactDistanceFromTheLoopAndOnIt(double equalPoint)
        {
            // At a tolerance of 0.001, the point 0.00098 out beside the middle of the side from (10, 0) to (10, 0.0006): 0.00098
            // from the loop, and on it. Read as its start, the side stood 0.001025 away, the root of 0.0003 squared and 0.00098
            // squared, as far as its end is, and the point was off the loop.
            Tolerance t = At(equalPoint);
            GeoPolygonArc3 loop = Loop(equalPoint);
            GeoPoint3 beside = At(10 + 0.98 * equalPoint, 0.3 * equalPoint);
            double exact = 0.98 * equalPoint;
            double slack = 1E-11;

            Assert.Equal(4, loop.EdgeCount);
            Assert.InRange(Sampled(loop.GetEdges(), beside), exact - 1E-9, exact + 1E-9);
            Assert.InRange(loop.DistanceTo(beside, t), exact - slack, exact + slack);
            Assert.InRange(beside.DistanceTo(loop, t), exact - slack, exact + slack);
            Assert.True(loop.IsPointOn(beside, t));

            // On the loop, the point is still more than the tolerance from both corners of the short side, which an edge on
            // its own reads as its start, so it cuts nothing, and the cut is refused with the loop opened at its start.
            Assert.False(loop.TrySplitBy(beside, out GeoPolylineArc3[] pieces, t));
            Assert.Single(pieces);
            Assert.InRange(pieces[0].StartPoint.DistanceTo(loop[0]), 0.0, 1E-12);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void APointBesideAStraightLegShorterThanTheToleranceInsideAChain_IsOnItAndCutsNothing(double equalPoint)
        {
            // The hairpin from (0, 0) to (10, 0), up the leg 0.6 tolerances long and back to (0, 0.6 t), and the point 0.98
            // tolerances out beside the middle of the leg: on the chain, 0.98 tolerances from it, and further than the tolerance
            // from both ends of the leg, so the chain is not cut there and comes back whole.
            Tolerance t = At(equalPoint);
            var chain = new GeoPolylineArc3(new List<GeoEdge3>
            {
                new GeoEdge3(At(0, 0), At(10, 0)),
                new GeoEdge3(At(10, 0), At(10, 0.6 * equalPoint)),
                new GeoEdge3(At(10, 0.6 * equalPoint), At(0, 0.6 * equalPoint)),
            });
            GeoPoint3 beside = At(10 + 0.98 * equalPoint, 0.3 * equalPoint);

            Assert.InRange(chain.DistanceTo(beside, t), 0.98 * equalPoint - 1E-11, 0.98 * equalPoint + 1E-11);
            Assert.True(chain.IsPointOn(beside, t));
            Assert.False(chain.TrySplitBy(beside, out GeoPolylineArc3[] pieces, t));
            Assert.Single(pieces);
            Assert.Equal(3, pieces[0].EdgeCount);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void TheClosestPointBesideAStraightLegShorterThanTheTolerance_IsStillTheLegsStart(double equalPoint)
        {
            // The projection keeps the snap, as in the plane: beside the end of the short leg of the chain the closest point is
            // the leg's start (10, 0), 1.08 tolerances away, where the distance is 0.9 tolerances; 0.98 tolerances out beside the
            // short side of the loop a sixth of the way up, it is (10, 0), 0.985 tolerances away, where the distance is 0.98.
            Tolerance t = At(equalPoint);
            var chain = new GeoPolylineArc3(ChainEdges(equalPoint));
            GeoPolygonArc3 loop = Loop(equalPoint);
            GeoPoint3 besideEnd = At(10 + 0.9 * equalPoint, 0.6 * equalPoint);
            GeoPoint3 besideLow = At(10 + 0.98 * equalPoint, 0.1 * equalPoint);
            double slack = 1E-11;

            Assert.InRange(chain.GetClosestPointOnBoundary(besideEnd, t).DistanceTo(At(10, 0)), 0.0, slack);
            Assert.InRange(chain.GetShortestLineTo(besideEnd, t).Length, Math.Sqrt(1.17) * equalPoint - slack, Math.Sqrt(1.17) * equalPoint + slack);
            Assert.InRange(loop.DistanceTo(besideLow, t), 0.98 * equalPoint - slack, 0.98 * equalPoint + slack);
            Assert.InRange(loop.GetClosestPointOnBoundary(besideLow, t).DistanceTo(At(10, 0)), 0.0, slack);
            Assert.InRange(loop.GetShortestLineTo(besideLow, t).Length, Math.Sqrt(0.9704) * equalPoint - slack, Math.Sqrt(0.9704) * equalPoint + slack);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void TheShortestLineFromABallBesideAStraightLegShorterThanTheTolerance_IsAsLongAsTheDistanceAndLandsOnTheLeg(double equalPoint)
        {
            // The ball of radius 0.5 tolerances about the point 0.9 tolerances beside the leg's end is 0.4 tolerances from the
            // chain, and its shortest line lands on that end. Read as its start, the leg stood 0.58 tolerances away and the line
            // landed on (10, 0).
            Tolerance t = At(equalPoint);
            var chain = new GeoPolylineArc3(ChainEdges(equalPoint));
            var ball = new GeoSphere3(At(10 + 0.9 * equalPoint, 0.6 * equalPoint), 0.5 * equalPoint);
            GeoPoint3 end = At(10, 0.6 * equalPoint);
            double exact = 0.4 * equalPoint;
            double slack = 1E-11;

            Assert.InRange(ball.DistanceTo(chain, t), exact - slack, exact + slack);

            GeoLine3 line = ball.GetShortestLineTo(chain, t);

            Assert.InRange(line.Length, exact - slack, exact + slack);
            Assert.InRange(line.EndPoint.DistanceTo(end), 0.0, 1E-9);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-2)]
        public void ALoneStraightEdgeShorterThanTheTolerance_IsStillMeasuredFromItsStart(double equalPoint)
        {
            // The convention kept, as it is for a straight GeoEdge2 or a segment you give: alone, the leg is read as its start,
            // so the point 0.9 tolerances beside its end is 1.08 tolerances from it. Only the edges of a chain or a loop are the
            // edges they are.
            Tolerance t = At(equalPoint);
            var leg = new GeoEdge3(At(10, 0), At(10, 0.6 * equalPoint));
            GeoPoint3 beside = At(10 + 0.9 * equalPoint, 0.6 * equalPoint);
            double read = Math.Sqrt(1.17) * equalPoint;

            Assert.InRange(leg.DistanceTo(beside, t), read - 1E-11, read + 1E-11);
        }
    }
}
