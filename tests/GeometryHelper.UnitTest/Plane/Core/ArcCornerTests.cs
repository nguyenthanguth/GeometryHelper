using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting and rounding the corners of a chain that may already carry arcs: what a fillet leaves behind,
    /// which corners are refused, and why a corner against a curve is left alone.
    /// </summary>
    public class ArcCornerTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        private static GeoPolygonArc2 Square(params double[] bulges)
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                bulges);
        }

        /// <summary>
        /// Checks that an arc introduced at a corner truly touches the two edges it sits between.
        /// </summary>
        private static void AssertTangent(GeoPolygonArc2 loop, int arcIndex)
        {
            GeoEdge2 arcEdge = loop.GetEdgeAt(arcIndex);
            GeoEdge2 before = loop.GetEdgeAt((arcIndex - 1 + loop.EdgeCount) % loop.EdgeCount);
            GeoEdge2 after = loop.GetEdgeAt((arcIndex + 1) % loop.EdgeCount);

            Assert.True(arcEdge.IsArc);

            GeoArc2 arc = arcEdge.ToArc();

            // The arc starts where the edge before it stops and stops where the next one starts, and its
            // centre stands a radius away from both of those edges: that is what tangency means here.
            Assert.True(arc.StartPoint.IsEqualTo(before.EndPoint, Tight));
            Assert.True(arc.EndPoint.IsEqualTo(after.StartPoint, Tight));
            Assert.Equal(arc.Radius, ReachOf(before, arc.Center), 6);
            Assert.Equal(arc.Radius, ReachOf(after, arc.Center), 6);
        }

        /// <summary>
        /// Gets how far a point stands from the endless curve an edge lies on: the line a segment carries,
        /// or the whole circle an arc was cut from.
        /// </summary>
        private static double ReachOf(GeoEdge2 edge, GeoPoint2 point)
        {
            if (edge.IsArc)
            {
                GeoArc2 arc = edge.ToArc();

                return Math.Abs(arc.Center.DistanceTo(point) - arc.Radius);
            }

            GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);
            GeoVector2 across = edge.StartPoint.GetVectorTo(point);

            return Math.Abs(along.CrossProduct(across)) / along.Length;
        }

        [Fact]
        public void FilletingALoopPutsAnArcAtEveryCornerThatFits()
        {
            GeoPolygonArc2 rounded = Square(0, 0, 0, 0).Fillet(10.0);

            // Four shortened sides and four quarter turns, alternating.
            Assert.Equal(8, rounded.EdgeCount);
            Assert.Equal(4, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.True(rounded.HasArcs);

            foreach (GeoEdge2 edge in rounded.GetEdges().Where(edge => edge.IsArc))
            {
                GeoArc2 arc = edge.ToArc();
                Assert.Equal(10.0, arc.Radius, 8);
                Assert.Equal(Math.PI / 2.0, Math.Abs(arc.SweptAngle), 8);
            }

            for (int i = 0; i < rounded.EdgeCount; i++)
            {
                if (rounded.GetEdgeAt(i).IsArc)
                {
                    AssertTangent(rounded, i);
                }
            }

            // Each side gives up the radius at both ends, and the four corners become one whole circle.
            Assert.Equal(4.0 * 80.0 + Math.PI * 2.0 * 10.0, rounded.Length, 8);

            // A rounded corner takes a little off the square: the square of the radius less its quarter disc.
            double perCorner = 100.0 - Math.PI * 100.0 * 0.25;
            Assert.InRange(rounded.Flatten(0.001).Area, 10000.0 - 4.0 * perCorner - 1.0, 10000.0 - 4.0 * perCorner + 0.01);
        }

        [Fact]
        public void FilletingAnOpenChainLeavesItsTwoFreeEndsWhereTheyWere()
        {
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) });

            GeoPolylineArc2 rounded = chain.Fillet(10.0);

            Assert.Equal(3, rounded.EdgeCount);
            Assert.Equal(1, rounded.GetEdges().Count(edge => edge.IsArc));

            // The ends of an open chain belong to nobody, so no corner can take anything from them.
            Assert.True(rounded[0].IsEqualTo(chain[0], Tight));
            Assert.True(rounded[rounded.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1], Tight));

            GeoArc2 arc = rounded.GetEdgeAt(1).ToArc();
            Assert.Equal(10.0, arc.Radius, 8);
            Assert.True(arc.StartPoint.IsEqualTo(new GeoPoint2(90, 0), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint2(100, 10), Tight));
        }

        [Fact]
        public void ACornerThatMeetsAnArcIsRoundedAgainstIt()
        {
            // The right-hand side of the square bulges, so the two corners at its ends are against a curve.
            GeoPolygonArc2 rounded = Square(0, 0.5, 0, 0).Fillet(10.0);

            // All four corners rounded, and the bulge that was already there makes five arcs.
            Assert.Equal(8, rounded.EdgeCount);
            Assert.Equal(5, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.True(rounded.IsSimple());

            // Every fillet touches both of its neighbours, whether they curve or not.
            for (int i = 0; i < rounded.EdgeCount; i++)
            {
                GeoEdge2 edge = rounded.GetEdgeAt(i);

                if (edge.IsArc && Math.Abs(edge.ToArc().Radius - 10.0) <= 1E-9)
                {
                    AssertTangent(rounded, i);
                }
            }

            // Cutting an arc back keeps the circle it was cut from: same centre, same radius, less sweep.
            GeoArc2 was = Square(0, 0.5, 0, 0).GetEdgeAt(1).ToArc();
            GeoArc2 now = rounded.GetEdges().Single(edge => edge.IsArc && edge.ToArc().Radius > 20.0).ToArc();

            Assert.Equal(was.Radius, now.Radius, 8);
            Assert.True(was.Center.IsEqualTo(now.Center, Tight));
            Assert.True(Math.Abs(now.SweptAngle) < Math.Abs(was.SweptAngle));
        }

        [Fact]
        public void ACornerWithTooLittleEdgeIsRefusedAndItsNeighboursStillFit()
        {
            // A radius of 60 needs 60 off each side of every corner, and a side is only 100 long.
            GeoPolygonArc2 rounded = Square(0, 0, 0, 0).Fillet(60.0);

            // Opposite corners can be rounded together; the two between them cannot.
            Assert.Equal(2, rounded.GetEdges().Count(edge => edge.IsArc));

            foreach (GeoEdge2 edge in rounded.GetEdges().Where(edge => !edge.IsArc))
            {
                Assert.True(edge.Length >= -1E-9);
            }

            // Nothing is rounded at all when no corner can pay for it.
            GeoPolygonArc2 untouched = Square(0, 0, 0, 0).Fillet(500.0);
            Assert.False(untouched.HasArcs);
            Assert.Equal(4, untouched.EdgeCount);
        }

        [Fact]
        public void ChamferingACurvedLoopCutsOnlyTheCornersBetweenStraightEdges()
        {
            GeoPolygonArc2 cut = Square(0, 0.5, 0, 0).Chamfer(10.0);

            // A chamfer is measured straight back along each edge, and there is no straight way back along
            // a curve, so only the two corners between straight edges are cut.
            // Two corners cut into straight pieces; the bulge is still the only arc.
            Assert.Equal(1, cut.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(6, cut.EdgeCount);

            // Each cut corner gives up ten off each side and puts back a chord of ten root two.
            Assert.Equal(Square(0, 0.5, 0, 0).Length - 2.0 * (20.0 - Math.Sqrt(200.0)), cut.Length, 6);
        }

        [Fact]
        public void TheSameCornerWorkWhicheverWayItIsAskedFor()
        {
            GeoPolygonArc2 loop = Square(0, 0, 0, 0);

            Assert.True(Corner2.Fillet(loop, 10.0, Tolerance.Global).IsEqualTo(loop.Fillet(10.0)));
            Assert.True(Corner2.Chamfer(loop, 10.0, 10.0, Tolerance.Global).IsEqualTo(loop.Chamfer(10.0)));
            Assert.True(loop.Fillet(10.0, Tolerance.Global).IsEqualTo(loop.Fillet(10.0)));
            Assert.True(loop.Chamfer(10.0, 10.0, Tolerance.Global).IsEqualTo(loop.Chamfer(10.0)));

            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) });

            Assert.True(Corner2.Fillet(chain, 10.0, Tolerance.Global).IsEqualTo(chain.Fillet(10.0)));
            Assert.True(Corner2.Chamfer(chain, 10.0, 10.0, Tolerance.Global).IsEqualTo(chain.Chamfer(10.0)));

            // Nothing to work with is refused rather than guessed at.
            Assert.Throws<ArgumentNullException>(() => Corner2.Fillet((GeoPolygonArc2)null, 10.0, Tolerance.Global));
            Assert.Throws<ArgumentNullException>(() => Corner2.Chamfer((GeoPolylineArc2)null, 10.0, 10.0, Tolerance.Global));
            Assert.Throws<ArgumentOutOfRangeException>(() => loop.Fillet(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => loop.Chamfer(-1.0));
        }

        [Fact]
        public void ChamferingACurvelessLoopAgreesWithTheStraightWorld()
        {
            var straight = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

            GeoPolygon2 cutStraight = straight.Chamfer(10.0);
            GeoPolygonArc2 cutWide = new GeoPolygonArc2(straight).Chamfer(10.0);

            Assert.False(cutWide.HasArcs);
            Assert.True(cutWide.Flatten().IsEqualTo(cutStraight));
        }
        [Fact]
        public void AFilletThatEatsAWholeSideStillLeavesTheArcsBehind()
        {
            // A radius of fifty on a square of side one hundred takes fifty off each end of every side:
            // exactly all of it. What is left is four quarter turns and no straight edge at all.
            GeoPolygonArc2 rounded = Square(0, 0, 0, 0).Fillet(50.0);

            Assert.Equal(4, rounded.EdgeCount);
            Assert.Equal(4, rounded.GetEdges().Count(edge => edge.IsArc));

            foreach (GeoEdge2 edge in rounded.GetEdges())
            {
                GeoArc2 arc = edge.ToArc();
                Assert.Equal(50.0, arc.Radius, 8);
                Assert.True(arc.Center.IsEqualTo(new GeoPoint2(50, 50), new Tolerance(1E-8, 1E-8)));
            }

            // Four quarter turns about the same centre are a circle.
            Assert.Equal(Math.PI * 100.0, rounded.Length, 8);
            Assert.Equal(Math.PI * 2500.0, rounded.Area, 6);
        }

        [Fact]
        public void AChamferThatEatsAWholeSideLeavesTheDiamondBehind()
        {
            GeoPolygonArc2 cut = Square(0, 0, 0, 0).Chamfer(50.0);

            Assert.False(cut.HasArcs);
            Assert.Equal(4, cut.EdgeCount);
            Assert.Equal(4.0 * Math.Sqrt(5000.0), cut.Length, 8);
            Assert.Equal(5000.0, cut.Area, 8);
        }
    }
}
