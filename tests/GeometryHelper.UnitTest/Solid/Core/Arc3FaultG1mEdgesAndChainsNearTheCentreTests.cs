using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The distance from a curved edge in space, and from a curved chain or loop, to a point is the exact one, also for a
    /// point near the centre of one of its bends, as it is for an arc since G1d. A point whose foot in the bend's plane fell
    /// within the point tolerance of the centre was carried to the bend's start first, so on a bend whose radius is about the
    /// tolerance a point 0.0003 from it read 0.00122 from it, and was off it.
    /// </summary>
    /// <remarks>
    /// Every bend here is a half circle of radius as large as the tolerance, about (1000, -2000, 500), in the plan or tilted
    /// 30 degrees about X; the chain runs 10 straight out from each end of it along its diameter, and the loop closes it with
    /// three straight sides 10 away on the other side of the diameter. The points are placed by the bend's own start and
    /// middle, and the expected distances follow from the circle alone and are read again off 20 000 points of each edge.
    /// </remarks>
    public class Arc3FaultG1mEdgesAndChainsNearTheCentreTests
    {
        private static readonly GeoPoint3 Centre = new GeoPoint3(1000, -2000, 500);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        private static GeoArc3 HalfCircle(double radius, double degrees)
        {
            double tilt = degrees * Math.PI / 180.0;

            return new GeoArc3(Centre, new GeoVector3(0, -Math.Sin(tilt), Math.Cos(tilt)), radius, 0.0, Math.PI);
        }

        /// <summary>The point of the arc's circle at an angle from its start, towards its middle, and a height over its plane.</summary>
        private static GeoPoint3 Around(GeoArc3 arc, double angle, double fromCentre, double height)
        {
            GeoVector3 toStart = arc.Center.GetVectorTo(arc.StartPoint).Normalize();
            GeoVector3 toMiddle = arc.Center.GetVectorTo(arc.MidPoint).Normalize();

            return arc.Center
                .Add(toStart.Multiply(fromCentre * Math.Cos(angle)))
                .Add(toMiddle.Multiply(fromCentre * Math.Sin(angle)))
                .Add(arc.Normal.Multiply(height));
        }

        /// <summary>A point in the arc's plane, so far along the way to its start and so far along the way to its middle.</summary>
        private static GeoPoint3 InPlane(GeoArc3 arc, double towardsStart, double towardsMiddle) => Around(arc, 0.0, towardsStart, 0.0)
            .Add(arc.Center.GetVectorTo(arc.MidPoint).Normalize().Multiply(towardsMiddle));

        /// <summary>The bend, with a straight leg 10 long running out from each end of it along its diameter.</summary>
        private static List<GeoEdge3> ChainEdges(GeoArc3 arc)
        {
            var bend = new GeoEdge3(arc);

            return new List<GeoEdge3>
            {
                new GeoEdge3(InPlane(arc, 10.0, 0.0), bend.StartPoint),
                bend,
                new GeoEdge3(bend.EndPoint, InPlane(arc, -10.0, 0.0)),
            };
        }

        /// <summary>The bend, closed by three straight sides 10 away on the side of the diameter it does not bulge to.</summary>
        private static List<GeoEdge3> LoopEdges(GeoArc3 arc)
        {
            var bend = new GeoEdge3(arc);
            GeoPoint3 lowLeft = InPlane(arc, -10.0, -10.0);
            GeoPoint3 lowRight = InPlane(arc, 10.0, -10.0);

            return new List<GeoEdge3>
            {
                bend,
                new GeoEdge3(bend.EndPoint, lowLeft),
                new GeoEdge3(lowLeft, lowRight),
                new GeoEdge3(lowRight, bend.StartPoint),
            };
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
        [InlineData(1E-3, 0.0)]
        [InlineData(1E-3, 30.0)]
        [InlineData(1E-2, 0.0)]
        [InlineData(1E-2, 30.0)]
        public void APointNearTheCentreOfAnEdgeAsLargeAsTheTolerance_IsTheExactDistanceFromIt(double equalPoint, double degrees)
        {
            // At a tolerance of 0.001, the half circle of radius 0.001 as an edge and the point 0.0007 from its centre under
            // its middle: 0.0003 from it, and on it. Carried to the start it stood 0.00122 away and was off. Raised by 0.0004 it
            // is 0.0005 away.
            Tolerance t = At(equalPoint);
            double r = equalPoint;
            GeoArc3 arc = HalfCircle(r, degrees);
            var edge = new GeoEdge3(arc);
            GeoPoint3 under = Around(arc, Math.PI / 2, 0.7 * r, 0.0);
            GeoPoint3 above = Around(arc, Math.PI / 2, 0.7 * r, 0.4 * r);
            double slack = 1E-11;

            Assert.InRange(edge.DistanceTo(under, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.InRange(Sampled(new[] { edge }, under), 0.3 * r - 1E-9, 0.3 * r + 1E-9);
            Assert.InRange(edge.DistanceTo(above, t), 0.5 * r - slack, 0.5 * r + slack);
            Assert.InRange(under.DistanceTo(edge, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.True(edge.IsPointOn(under, t));
            Assert.True(edge.IsPointOn(above, t));
            Assert.Equal(PointLocation.OnSide, edge.Locate(under, t));
        }

        [Theory]
        [InlineData(1E-3, 0.0)]
        [InlineData(1E-3, 30.0)]
        [InlineData(1E-2, 0.0)]
        [InlineData(1E-2, 30.0)]
        public void APointNearTheCentreOfABendOfAChainAsLargeAsTheTolerance_IsTheExactDistanceFromIt(double equalPoint, double degrees)
        {
            // The same half circle as the middle edge of a chain and as one side of a loop, and the same point: 0.0003 from
            // the bend, where each end of it is 0.00122 away. Carried to the bend's start, the chain and the loop read 0.00122.
            Tolerance t = At(equalPoint);
            double r = equalPoint;
            GeoArc3 arc = HalfCircle(r, degrees);
            List<GeoEdge3> chainEdges = ChainEdges(arc);
            List<GeoEdge3> loopEdges = LoopEdges(arc);
            var chain = new GeoPolylineArc3(chainEdges);
            var loop = new GeoPolygonArc3(loopEdges);
            GeoPoint3 under = Around(arc, Math.PI / 2, 0.7 * r, 0.0);
            GeoPoint3 above = Around(arc, Math.PI / 2, 0.7 * r, 0.4 * r);
            double slack = 1E-11;

            Assert.InRange(chain.DistanceTo(under, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.InRange(Sampled(chainEdges, under), 0.3 * r - 1E-9, 0.3 * r + 1E-9);
            Assert.InRange(chain.DistanceTo(above, t), 0.5 * r - slack, 0.5 * r + slack);
            Assert.InRange(under.DistanceTo(chain, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.True(chain.IsPointOn(under, t));
            Assert.Equal(PointLocation.OnSide, chain.Locate(under, t));

            Assert.InRange(loop.DistanceTo(under, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.InRange(Sampled(loopEdges, under), 0.3 * r - 1E-9, 0.3 * r + 1E-9);
            Assert.InRange(loop.DistanceTo(above, t), 0.5 * r - slack, 0.5 * r + slack);
            // Raised past the planar tolerance the point is off the loop's plane, and its signed distance is the distance:
            // the root of 0.3 squared and 2 squared, 2.022 radii. Carried to the start it read 2.343.
            GeoPoint3 high = Around(arc, Math.PI / 2, 0.7 * r, 2.0 * r);
            Assert.InRange(loop.SignedDistanceTo(high, t), Math.Sqrt(4.09) * r - slack, Math.Sqrt(4.09) * r + slack);
            Assert.InRange(under.DistanceTo(loop, t), 0.3 * r - slack, 0.3 * r + slack);
            Assert.True(loop.IsPointOn(under, t));
        }

        [Fact]
        public void APointMovedOffAnEdgeOrAChainAsLargeAsTheTolerance_IsNoFurtherFromItThanItMoved()
        {
            // Each point of the bend, every ten degrees, moved 0.00029 straight towards the centre: 0.00029 from the bend, and
            // no further from the edge or the chain than from the point it left, which is on them. Carried to the start, the
            // one moved from 120 degrees read 0.00149, five times as far as it had moved.
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);
            var edge = new GeoEdge3(arc);
            var chain = new GeoPolylineArc3(ChainEdges(arc));
            var loop = new GeoPolygonArc3(LoopEdges(arc));

            for (int degrees = 0; degrees <= 180; degrees += 10)
            {
                double angle = degrees * Math.PI / 180.0;
                GeoPoint3 onArc = Around(arc, angle, 1E-3, 0.0);
                GeoPoint3 moved = Around(arc, angle, 0.71E-3, 0.0);
                double away = onArc.DistanceTo(moved);

                Assert.InRange(edge.DistanceTo(moved, t), 0.29E-3 - 1E-11, 0.29E-3 + 1E-11);
                Assert.True(edge.DistanceTo(moved, t) <= edge.DistanceTo(onArc, t) + away + 1E-11, $"edge, moved from {degrees} degrees");
                Assert.True(chain.DistanceTo(moved, t) <= chain.DistanceTo(onArc, t) + away + 1E-11, $"chain, moved from {degrees} degrees");
                Assert.True(loop.DistanceTo(moved, t) <= loop.DistanceTo(onArc, t) + away + 1E-11, $"loop, moved from {degrees} degrees");
            }
        }

        [Fact]
        public void APointExactlyAtTheCentreOfAnEdgeAsLargeAsTheTolerance_IsItsRadiusFromIt()
        {
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);

            Assert.InRange(new GeoEdge3(arc).DistanceTo(Centre, t), 1E-3 - 1E-11, 1E-3 + 1E-11);
            Assert.InRange(new GeoPolylineArc3(ChainEdges(arc)).DistanceTo(Centre, t), 1E-3 - 1E-11, 1E-3 + 1E-11);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TheShortestLineFromABallCentredNearTheCentreOfABend_IsAsLongAsTheDistanceAndLandsAtTheContact(int shape)
        {
            // The lower half of the circle of radius 50 about the origin, as an edge (0), as the bend of a chain with a leg 100
            // up from its start (1), and as one side of a loop closed by three sides 100 up (2); the ball of radius 49.99 about
            // (0, -0.0009, 0), at a tolerance of a thousandth. The bend's bottom (0, -50, 0) is 49.9991 from the centre of the
            // ball, so the ball is 0.0091 short of it. The centre was carried to the bend's start (-50, 0, 0), 0.01 away.
            Tolerance t = At(1E-3);
            var bend = new GeoEdge3(GeoArc3.FromThreePoints(new GeoPoint3(-50, 0, 0), new GeoPoint3(0, -50, 0), new GeoPoint3(50, 0, 0), t));
            var ball = new GeoSphere3(new GeoPoint3(0, -0.0009, 0), 49.99);
            var upLeft = new GeoPoint3(-50, 100, 0);
            var upRight = new GeoPoint3(50, 100, 0);

            double distance;
            GeoLine3 line;
            GeoLine3 back;

            if (shape == 0)
            {
                distance = ball.DistanceTo(bend, t);
                line = ball.GetShortestLineTo(bend, t);
                back = bend.GetShortestLineTo(ball, t);
            }
            else if (shape == 1)
            {
                var chain = new GeoPolylineArc3(new[] { new GeoEdge3(upLeft, bend.StartPoint), bend });
                distance = ball.DistanceTo(chain, t);
                line = ball.GetShortestLineTo(chain, t);
                back = chain.GetShortestLineTo(ball, t);
            }
            else
            {
                var loop = new GeoPolygonArc3(new[] { bend, new GeoEdge3(bend.EndPoint, upRight), new GeoEdge3(upRight, upLeft), new GeoEdge3(upLeft, bend.StartPoint) });
                distance = ball.DistanceTo(loop, t);
                line = ball.GetShortestLineTo(loop, t);
                back = loop.GetShortestLineTo(ball, t);
            }

            double slack = 1E-12 * ball.Radius;

            Assert.InRange(distance, 0.0091 - 1E-11, 0.0091 + 1E-11);
            Assert.InRange(line.Length, distance - slack, distance + slack);
            Assert.InRange(line.StartPoint.DistanceTo(ball.Center), ball.Radius - slack, ball.Radius + slack);
            Assert.InRange(line.EndPoint.DistanceTo(new GeoPoint3(0, -50, 0)), 0.0, 1E-9);
            Assert.InRange(back.Length, distance - slack, distance + slack);
            Assert.True(back.StartPoint.IsEqualTo(line.EndPoint, Tolerance.Default));
        }

        [Fact]
        public void ABallNearTheCentreOfAnEdgeOrAChainAsLargeAsTheTolerance_IsTheExactDistanceFromItAndTouchesIt()
        {
            // At a tolerance of 0.001, the half circle of radius 0.001 tilted 30 degrees and a ball of radius 0.0001 about the
            // point 0.0007 from its centre under its middle: 0.0002 from the edge and from the chain, and touching both.
            // Carried to the start, the centre of the ball stood 0.00122 from them, and the ball 0.00112, too far to touch.
            Tolerance t = At(1E-3);
            GeoArc3 arc = HalfCircle(1E-3, 30.0);
            var edge = new GeoEdge3(arc);
            List<GeoEdge3> chainEdges = ChainEdges(arc);
            var chain = new GeoPolylineArc3(chainEdges);
            var ball = new GeoSphere3(Around(arc, Math.PI / 2, 0.7E-3, 0.0), 1E-4);

            Assert.InRange(ball.DistanceTo(edge, t), 2E-4 - 1E-11, 2E-4 + 1E-11);
            Assert.InRange(ball.DistanceTo(chain, t), 2E-4 - 1E-11, 2E-4 + 1E-11);
            Assert.InRange(Sampled(chainEdges, ball.Center) - ball.Radius, 2E-4 - 1E-9, 2E-4 + 1E-9);
            Assert.InRange(edge.DistanceTo(ball, t), 2E-4 - 1E-11, 2E-4 + 1E-11);
            Assert.True(ball.CollidesWith(edge, t));
            Assert.True(edge.CollidesWith(ball, t));
            Assert.True(ball.CollidesWith(chain, t));
            Assert.True(chain.CollidesWith(ball, t));
        }

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>The point 0.0007 under the middle of the half circle of radius 0.001 about the origin in the plan.</summary>
        private static readonly GeoPoint3 UnderTheMiddle = new GeoPoint3(0, 0.0007, 0);

        /// <summary>The middle of that half circle, the point of it nearest <see cref="UnderTheMiddle"/>.</summary>
        private static readonly GeoPoint3 Middle = new GeoPoint3(0, 0.001, 0);

        [Fact]
        public void ALoopCutByAPointNearTheCentreOfABendAsLargeAsTheTolerance_ComesApartAtThePointNearestIt()
        {
            // At a tolerance of 0.001, the loop (0.01, -0.01, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, -0.01, 0) whose second
            // edge is the half circle of radius 0.001 through (0, 0.001, 0), and the point (0, 0.0007, 0), 0.0003 from it. Read
            // as off the loop, the cut was refused; read as on it but cut at the start's parameter, it was given as made with
            // the loop opened at its first vertex, cut nowhere. It is cut at the middle of the bend.
            Tolerance t = At(1E-3);
            var loop = new GeoPolygonArc3(
                new[] { new GeoPoint3(0.01, -0.01, 0), new GeoPoint3(0.001, 0, 0), new GeoPoint3(-0.001, 0, 0), new GeoPoint3(-0.01, -0.01, 0) },
                new[] { 0.0, 1.0, 0.0, 0.0 },
                new[] { Up, Up, Up, Up },
                t);

            Assert.True(loop.IsPointOn(UnderTheMiddle, t));
            Assert.True(loop.TrySplitBy(UnderTheMiddle, out GeoPolylineArc3[] pieces, t));
            Assert.Single(pieces);
            Assert.InRange(pieces[0].StartPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.InRange(pieces[0].EndPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.InRange(pieces[0].Length, loop.Length - 1E-12, loop.Length + 1E-12);
        }

        [Fact]
        public void ALoopCutAtItsFirstVertex_IsStillCutThere()
        {
            Tolerance t = At(1E-3);
            var loop = new GeoPolygonArc3(
                new[] { new GeoPoint3(0.01, -0.01, 0), new GeoPoint3(0.001, 0, 0), new GeoPoint3(-0.001, 0, 0), new GeoPoint3(-0.01, -0.01, 0) },
                new[] { 0.0, 1.0, 0.0, 0.0 },
                new[] { Up, Up, Up, Up },
                t);

            Assert.True(loop.TrySplitBy(loop[0], out GeoPolylineArc3[] pieces, t));
            Assert.Single(pieces);
            Assert.InRange(pieces[0].StartPoint.DistanceTo(loop[0]), 0.0, 1E-12);
            Assert.InRange(pieces[0].EndPoint.DistanceTo(loop[0]), 0.0, 1E-12);
        }

        [Fact]
        public void AChainCutOrTrimmedByAPointNearTheCentreOfABendAsLargeAsTheTolerance_ComesApartAtThePointNearestIt()
        {
            // The chain (0.01, 0, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, 0, 0) with a bulge of 1 on its middle edge holds
            // the point (0, 0.0007, 0), 0.0003 from it, and refused to be cut or trimmed there. It comes apart at the middle of
            // the bend, and each trim ends there.
            Tolerance t = At(1E-3);
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0.01, 0, 0), new GeoPoint3(0.001, 0, 0), new GeoPoint3(-0.001, 0, 0), new GeoPoint3(-0.01, 0, 0) },
                new[] { 0.0, 1.0, 0.0 },
                new[] { Up, Up, Up },
                t);

            Assert.True(chain.IsPointOn(UnderTheMiddle, t));
            Assert.True(chain.TrySplitBy(UnderTheMiddle, out GeoPolylineArc3[] pieces, t));
            Assert.Equal(2, pieces.Length);
            Assert.InRange(pieces[0].EndPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.InRange(pieces[1].StartPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.InRange(pieces[0].Length + pieces[1].Length, chain.Length - 1E-12, chain.Length + 1E-12);

            Assert.True(chain.TryTrimTo(UnderTheMiddle, LineEnd.End, out GeoPolylineArc3 kept, t));
            Assert.InRange(kept.EndPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.True(chain.TryTrimTo(UnderTheMiddle, LineEnd.Start, out GeoPolylineArc3 rest, t));
            Assert.InRange(rest.StartPoint.DistanceTo(Middle), 0.0, 1E-12);
        }

        [Fact]
        public void AnArcCutByAPointNearItsCentre_ComesApartAtThePointNearestIt()
        {
            // The half circle of radius 0.001 from (0.001, 0, 0) through (0, 0.001, 0) to (-0.001, 0, 0) and the point
            // (0, 0.0007, 0): cut at the middle, as the chain and the loop it bends in are. Its parameter read at the
            // tolerance was the start's, which left the arc whole. The centre itself has no direction, and leaves it whole.
            Tolerance t = At(1E-3);
            GeoArc3 arc = new GeoEdge3(new GeoPoint3(0.001, 0, 0), new GeoPoint3(-0.001, 0, 0), 1.0, Up).ToArc();

            Assert.True(arc.TrySplitAt(UnderTheMiddle, out GeoArc3[] pieces, t));
            Assert.Equal(2, pieces.Length);
            Assert.InRange(pieces[0].EndPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.InRange(pieces[1].StartPoint.DistanceTo(Middle), 0.0, 1E-12);
            Assert.False(arc.TrySplitAt(arc.Center, out _, t));
        }

        [Theory]
        [InlineData(0.0005, 0.0003)]
        [InlineData(-0.0005, 0.0003)]
        [InlineData(0.0, 0.0007)]
        [InlineData(-1E-12, 1E-12)]
        public void AnArcCutByAPointOffItNearItsCentre_IsLeftWhole(double x, double y)
        {
            // The half circle of radius 50 from (50, 0, 0) through (0, 50, 0) to (-50, 0, 0), at a tolerance of a thousandth,
            // and points some 50 off it within the tolerance of its centre, whose direction from the centre is no surer than
            // the tolerance. Read at nought, (0.0005, 0.0003, 0) cut it at (42.87, 25.72, 0) and (-0.0005, 0.0003, 0), 0.001
            // away, at (-42.87, 25.72, 0); they are read as at the centre, as the arc's nearest point reads them, and cut
            // nothing.
            Tolerance t = At(1E-3);
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(50, 0, 0), new GeoPoint3(0, 50, 0), new GeoPoint3(-50, 0, 0), t);

            Assert.False(arc.TrySplitAt(new GeoPoint3(x, y, 0), out GeoArc3[] pieces, t));
            Assert.Single(pieces);
        }
    }
}
