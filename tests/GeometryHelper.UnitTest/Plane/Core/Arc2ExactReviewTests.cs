using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What the review of the exact arc distances found: collisions of curved chains and loops that kept a band at the
    /// ends of their arcs, a circle nearly about an arc's own centre, the point from which an edge is read as its chord,
    /// a stub crossing a chain's straight edge, and vertices that two edges share.
    /// </summary>
    public class Arc2ExactReviewTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        #region Collisions with arcs in chains and loops

        private static readonly GeoPoint2[] Corners = { new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(70, 25), new GeoPoint2(100, 25) };
        private static readonly double[] Bulges = { 0.0, -0.35, 0.0, 0.0 };

        /// <summary>The arc about (25.858, -47.958) of radius 50 whose end stands 0.00012 from the vertex (40, 0) of the chain.</summary>
        private static GeoArc2 NearArc() => new GeoArc2(new GeoPoint2(25.858140813193945, -47.95846739069786), 50.0, 5.3172292646646655, 7.5672292646646655);

        private static double Bulge(GeoArc2 arc) => Math.Tan(arc.SweptAngle / 4);

        public static System.Collections.Generic.IEnumerable<object[]> NearShapes()
        {
            foreach (string a in new[] { "chain", "loop" })
            {
                foreach (string b in new[] { "arc", "one-edge chain", "one-edge loop" })
                {
                    yield return new object[] { a, b };
                }
            }
        }

        [Theory]
        [MemberData(nameof(NearShapes))]
        public void ACurvedChainOrLoopWhoseArcEndStands1Point2TolerancesOffAnArc_IsThatFarAndDoesNotCollideEitherWayRound(string curved, string other)
        {
            Tolerance t = At(1E-4);
            GeoArc2 arc = NearArc();
            GeoPoint2 p0 = arc.StartPoint, p1 = arc.EndPoint;
            double b = Bulge(arc);

            double there, back;
            bool collides, collidesBack;

            GeoPolylineArc2 chain = new GeoPolylineArc2(Corners, Bulges);
            GeoPolygonArc2 loop = new GeoPolygonArc2(Corners, Bulges);

            if (curved == "chain")
            {
                switch (other)
                {
                    case "arc":
                        there = chain.DistanceTo(arc, t); back = arc.DistanceTo(chain, t);
                        collides = chain.CollidesWith(arc, t); collidesBack = arc.CollidesWith(chain, t);
                        break;
                    case "one-edge chain":
                        var oc = new GeoPolylineArc2(new[] { p0, p1 }, new[] { b, 0.0 });
                        there = chain.DistanceTo(oc, t); back = oc.DistanceTo(chain, t);
                        collides = chain.CollidesWith(oc, t); collidesBack = oc.CollidesWith(chain, t);
                        break;
                    default:
                        var ol = new GeoPolygonArc2(new[] { p0, p1, new GeoPoint2(120, -100) }, new[] { b, 0.0, 0.0 });
                        there = chain.DistanceTo(ol, t); back = ol.DistanceTo(chain, t);
                        collides = chain.CollidesWith(ol, t); collidesBack = ol.CollidesWith(chain, t);
                        break;
                }
            }
            else
            {
                switch (other)
                {
                    case "arc":
                        there = loop.DistanceTo(arc, t); back = arc.DistanceTo(loop, t);
                        collides = loop.CollidesWith(arc, t); collidesBack = arc.CollidesWith(loop, t);
                        break;
                    case "one-edge chain":
                        var oc = new GeoPolylineArc2(new[] { p0, p1 }, new[] { b, 0.0 });
                        there = loop.DistanceTo(oc, t); back = oc.DistanceTo(loop, t);
                        collides = loop.CollidesWith(oc, t); collidesBack = oc.CollidesWith(loop, t);
                        break;
                    default:
                        var ol = new GeoPolygonArc2(new[] { p0, p1, new GeoPoint2(120, -100) }, new[] { b, 0.0, 0.0 });
                        there = loop.DistanceTo(ol, t); back = ol.DistanceTo(loop, t);
                        collides = loop.CollidesWith(ol, t); collidesBack = ol.CollidesWith(loop, t);
                        break;
                }
            }

            Assert.InRange(there, 0.00012 - 1E-9, 0.00012 + 1E-9);
            Assert.InRange(back, 0.00012 - 1E-9, 0.00012 + 1E-9);
            Assert.False(collides);
            Assert.False(collidesBack);
        }

        #endregion

        #region A circle whose rim comes within a tolerance of an arc's circle

        [Fact]
        public void AnArcStartingAThousandthOfARadianPastTheNearestPointOfTwoCirclesATolerancePartedAndACircle_DoNotCollide()
        {
            // The circle of radius 10 about the origin and the circle of radius 10 about (0, 20.1) stand 0.1 apart, at a
            // tolerance of 0.1, and touch within it at (0, 10.05). The arc of the second begins 0.0003 of a radian past its
            // lowest point, so it stands a little more than 0.1 from the first: 0.1000009.
            Tolerance t = At(0.1);
            const double past = 0.0003;
            double apart = Math.Sqrt(504.01 - 402 * Math.Cos(past)) - 10;
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);
            var arc = new GeoArc2(new GeoPoint2(0, 20.1), 10, -Math.PI / 2 + past, -Math.PI / 2 + 1.3);
            var chain = new GeoPolylineArc2(new[] { arc.StartPoint, arc.EndPoint }, new[] { Bulge(arc), 0.0 });
            var loop = new GeoPolygonArc2(new[] { arc.StartPoint, arc.EndPoint, new GeoPoint2(40, 15) }, new[] { Bulge(arc), 0.0, 0.0 });

            Assert.InRange(circle.DistanceTo(arc, t), apart - 1E-9, apart + 1E-9);
            Assert.InRange(circle.DistanceTo(chain, t), apart - 1E-9, apart + 1E-9);
            Assert.InRange(circle.DistanceTo(loop, t), apart - 1E-9, apart + 1E-9);
            Assert.False(circle.CollidesWith(arc, t));
            Assert.False(arc.CollidesWith(circle, t));
            Assert.False(circle.CollidesWith(chain, t));
            Assert.False(chain.CollidesWith(circle, t));
            Assert.False(circle.CollidesWith(loop, t));
            Assert.False(loop.CollidesWith(circle, t));
        }

        #endregion

        #region A circle nearly about an arc's own centre

        private static readonly GeoCircle2 NearlyConcentric = new GeoCircle2(new GeoPoint2(0.0007, 0.0005), 49.9995);

        [Fact]
        public void AnArcAboutTheOriginAndACircleWhoseCentreIsWithinATolerance_AreNoughtApart()
        {
            // Truth 0: the circle of radius 49.9995 about (0.0007, 0.0005) reaches the arc of radius 50 about the origin.
            Tolerance t = At(1E-3);
            var arc = new GeoArc2(new GeoPoint2(0, 0), 50, Math.PI, 7.950632344359005);

            Assert.Equal(0.0, arc.DistanceTo(NearlyConcentric, t));
            Assert.Equal(0.0, NearlyConcentric.DistanceTo(arc, t));
            Assert.True(arc.CollidesWith(NearlyConcentric, t));
            Assert.True(NearlyConcentric.CollidesWith(arc, t));

            var edge = new GeoEdge2(arc);
            Assert.Equal(0.0, edge.DistanceTo(NearlyConcentric, t));
            Assert.Equal(0.0, NearlyConcentric.DistanceTo(edge, t));
        }

        [Fact]
        public void ACurvedChainAndACircleWhoseCentreIsWithinAToleranceOfAnArcsCentre_AreNoughtApart()
        {
            Tolerance t = At(1E-3);
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(35.72107653588144, -48.20793182784032), new GeoPoint2(29.767563779901206, -40.17327652320027), new GeoPoint2(-28.347403052789055, 41.18767704256609) },
                new[] { 0.0, 0.98269726311569, 0.0 });

            Assert.Equal(0.0, chain.DistanceTo(NearlyConcentric, t));
            Assert.Equal(0.0, NearlyConcentric.DistanceTo(chain, t));
            Assert.True(chain.CollidesWith(NearlyConcentric, t));
            Assert.True(NearlyConcentric.CollidesWith(chain, t));
        }

        #endregion

        #region Where an edge is read as its chord

        [Theory]
        [InlineData(100.0)]
        [InlineData(10000.0)]
        public void AnEdgeOfBulge3Point2E8_IsMeasuredToWithinTheRoundingOfItsCentre(double length)
        {
            // The edge bows downwards by bulge times half the chord, exactly. A segment above its middle stands that far
            // plus the gap from the apex; one leaving its end vertex touches it. The centre route is good to 2.4E-16 of
            // how far the centre and radius reach; the chord, where it is read in its place, is off by the whole bow.
            const double bulge = 3.2E-8;
            double sag = bulge * length / 2;
            double radius = length / 4 / bulge * (1 + bulge * bulge);
            double reach = 2 * radius + length;
            double allowance = 2.5E-16 * reach;
            Tolerance t = At(1E-3);

            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(length, 0), bulge);
            var inside = new GeoLine2(new GeoPoint2(length / 2, 1.0), new GeoPoint2(length / 2, 0.3));
            var atVertex = new GeoLine2(new GeoPoint2(length, 0), new GeoPoint2(length + 10, 5));

            Assert.InRange(edge.DistanceTo(inside, t), 0.3 + sag - allowance, 0.3 + sag + allowance);
            Assert.InRange(inside.DistanceTo(edge, t), 0.3 + sag - allowance, 0.3 + sag + allowance);
            Assert.InRange(edge.DistanceTo(atVertex, t), -allowance, allowance);
            Assert.InRange(atVertex.DistanceTo(edge, t), -allowance, allowance);
        }

        #endregion

        #region A stub crossing a chain

        [Fact]
        public void AStubShorterThanTheToleranceCrossingAStraightEdgeOfACurvedChain_IsNoughtFromIt()
        {
            Tolerance t = At(1E-3);
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) }, new[] { 0.0, 0.5, 0.0 });
            var overStraight = new GeoLine2(new GeoPoint2(50, -0.0004), new GeoPoint2(50, 0.0002));
            // The arc bows out to the right of the edge from (100, 0) to (100, 100), through (125, 50) at its middle.
            var overArc = new GeoLine2(new GeoPoint2(125 - 0.0004, 50), new GeoPoint2(125 + 0.0002, 50));

            Assert.Equal(0.0, chain.DistanceTo(overStraight, t));
            Assert.Equal(0.0, overStraight.DistanceTo(chain, t));
            Assert.Equal(0.0, chain.DistanceTo(overArc, t));
            Assert.Equal(0.0, overArc.DistanceTo(chain, t));
        }

        #endregion

        #region Vertices that two edges share

        [Fact]
        public void AnEdgeOfBulge1E7SharingItsEndWithASegmentOrAnotherEdge_IsNoughtFromThem()
        {
            Tolerance t = At(1E-3);
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1E-7);
            var segment = new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(150, 30));
            var next = new GeoEdge2(new GeoPoint2(100, 0), new GeoPoint2(150, 30), 0.3);
            var previous = new GeoEdge2(new GeoPoint2(-50, 30), new GeoPoint2(0, 0), 0.3);

            Assert.Equal(0.0, edge.DistanceTo(segment, t));
            Assert.Equal(0.0, segment.DistanceTo(edge, t));
            Assert.Equal(0.0, edge.DistanceTo(next, t));
            Assert.Equal(0.0, next.DistanceTo(edge, t));
            Assert.Equal(0.0, edge.DistanceTo(previous, t));
            Assert.Equal(0.0, previous.DistanceTo(edge, t));
        }

        #endregion

        #region Arcs smaller than the tolerance

        [Fact]
        public void TwoArcsOfARadiusUnderTheToleranceAboutOnePoint_CollideWhereTheirDistanceIsWithinIt()
        {
            // Radii 0.00078 and 0.00088 about the origin, 0.0001 apart, the second turning 273 degrees over the first's span.
            Tolerance t = At(1E-3);
            var first = new GeoArc2(new GeoPoint2(0, 0), 0.00078, 1.32077601486853, 1.67999130783602);
            var second = new GeoArc2(new GeoPoint2(0, 0), 0.00088, 4.86957100208363, 9.64287332907453);

            Assert.InRange(first.DistanceTo(second, t), 0.0001 - 1E-12, 0.0001 + 1E-12);
            Assert.True(first.CollidesWith(second, t));
            Assert.True(second.CollidesWith(first, t));
        }

        #endregion
    }
}
