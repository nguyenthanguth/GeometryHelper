using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 18 and 19: an edge, a curved chain and a curved loop against the rim of an ellipse in space, dispatched as
    /// <see cref="GeoCircle3"/> dispatches them. An edge is asked as the segment or the arc it is
    /// (GeoCircle3.Intersection.cs:463-466, 481-484; GeoCircle3.Collision.cs:133-136). A chain or a loop is asked edge by
    /// edge, in its own order, each edge's crossings in that edge's order, a corner on the rim named once
    /// (ArcChain3.Reach.cs:27-41, ArcChain3.Crossings.cs:368-375, 755-762); it touches when one of its edges does
    /// (ArcChain3.Reach.cs:46-57), so a loop round the ellipse, or inside the disc, touches nothing: an edge has no inside.
    /// The chains have no TryIntersectWith, as the circle's have none. Every chain lies in the plane of the 300 by 100
    /// ellipse of <see cref="Ellipse3Oracle.Tilted()"/> unless it says otherwise; the 2D ellipse laid out answers it edge by edge.
    /// </summary>
    public class Ellipse3QueryChainTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double Close = 1E-9;

        /// <summary>The rim at y = 50: x = ±300 √(1 - 0.25) = ±259.8076.</summary>
        private static readonly double AtFifty = 300 * Math.Sqrt(0.75);

        /// <summary>A quarter of the circle of radius 150 about the centre, from (0, 150) to (150, 0) of the frame.</summary>
        private static GeoArc3 Quarter(Specimen e)
        {
            double h = 150 * Math.Sqrt(0.5);
            return GeoArc3.FromThreePoints(e.At(0, 150, 0), e.At(h, h, 0), e.At(150, 0, 0));
        }

        /// <summary>
        /// The crossings of the 2D ellipse with edges lying in the plane, edge by edge, a point already found not found
        /// again: each segment laid out as a segment, each arc as the arc through its ends and its middle.
        /// </summary>
        private static GeoPoint3[] EdgeByEdge(Specimen e, IEnumerable<GeoEdge3> edges)
        {
            GeoEllipse2 flat = e.Flat();
            var found = new List<GeoPoint3>();
            foreach (GeoEdge3 edge in edges)
            {
                GeoPoint2[] crossings = edge.IsArc
                    ? flat.GetIntersections(GeoArc2.FromThreePoints(e.Foot(edge.ToArc().StartPoint), e.Foot(edge.ToArc().MidPoint), e.Foot(edge.ToArc().EndPoint)), Tol)
                    : flat.GetIntersections(new GeoLine2(e.Foot(edge.StartPoint), e.Foot(edge.EndPoint)), Tol);
                foreach (GeoPoint2 point in crossings)
                {
                    GeoPoint3 lifted = e.Lift(point);
                    if (!found.Any(p => p.DistanceTo(lifted) <= Tol.EqualPoint))
                    {
                        found.Add(lifted);
                    }
                }
            }

            return found.ToArray();
        }

        [Fact]
        public void AStraightEdgeIsAskedAsTheSegmentItIs()
        {
            Specimen e = Tilted();
            var edge = new GeoEdge3(e.At(-400, 50, 0), e.At(400, 50, 0));
            GeoPoint3[] expected = { e.At(-AtFifty, 50, 0), e.At(AtFifty, 50, 0) };

            SamePointsInOrder(expected, e.Build().GetIntersections(edge, Tol), Close);
            Assert.True(e.Build().CollidesWith(edge, Tol));
            Assert.True(e.Build().TryIntersectWith(edge, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(expected, tried, Close);
        }

        [Fact]
        public void ACurvedEdgeIsAskedAsTheArcItIs()
        {
            // The quarter of radius 150 crosses the rim once, at (118.585, 91.856).
            Specimen e = Tilted();
            var edge = new GeoEdge3(Quarter(e));
            GeoPoint3 expected = e.At(Math.Sqrt(14062.5), Math.Sqrt(8437.5), 0);

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(edge, Tol));

            Assert.True(point.DistanceTo(expected) <= Close, $"{point}");
            Assert.True(e.Build().CollidesWith(edge, Tol));
            Assert.True(e.Build().TryIntersectWith(edge, out _, Tol));

            // An arc edge inside the disc, short of the rim, touches nothing.
            var inside = new GeoEdge3(GeoArc3.FromThreePoints(e.At(0, 50, 0), e.At(35.36, 35.36, 0), e.At(50, 0, 0)));
            Assert.Empty(e.Build().GetIntersections(inside, Tol));
            Assert.False(e.Build().CollidesWith(inside, Tol));
            Assert.False(e.Build().TryIntersectWith(inside, out _, Tol));
        }

        [Fact]
        public void ACurvedChainIsCrossedEdgeByEdgeInItsOwnOrder()
        {
            // From (-400, 0) straight to (0, 150), which cuts the rim twice, at s = 0.400 and 0.483 along it; round the
            // quarter to (150, 0), cutting it once at (118.585, 91.856); straight on to (400, 0), cutting it at (300, 0).
            Specimen e = Tilted();
            var chain = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(e.At(-400, 0, 0), e.At(0, 150, 0)), new GeoEdge3(Quarter(e)), new GeoEdge3(e.At(150, 0, 0), e.At(400, 0, 0)),
            });

            GeoPoint3[] crossings = e.Build().GetIntersections(chain, Tol);

            Assert.Equal(4, crossings.Length);
            SamePointsInOrder(EdgeByEdge(e, chain.GetEdges()), crossings, Close);
            Assert.True(crossings[3].DistanceTo(e.At(300, 0, 0)) <= Close, $"{crossings[3]}");
            Assert.True(e.Build().CollidesWith(chain, Tol));
        }

        [Fact]
        public void ACornerOfAChainOnTheRimIsNamedOnce()
        {
            // (150, 0) to (300, 0), the end of the major axis, then on to (400, 100): both edges reach the rim at the corner.
            Specimen e = Tilted();
            var chain = new GeoPolylineArc3(new[] { e.At(150, 0, 0), e.At(300, 0, 0), e.At(400, 100, 0) });

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(chain, Tol));

            Assert.True(point.DistanceTo(e.At(300, 0, 0)) <= Close, $"{point}");
        }

        [Fact]
        public void AChainStandingAcrossThePlaneIsCrossedWhereItsEdgesPierceTheRim()
        {
            // Two straight edges: up through the rim point at t = 2 along N + 0.3 U, then on through the rim point at t = 4.
            Specimen e = Tilted();
            GeoPoint3 first = e.Rim(2.0), second = e.Rim(4.0);
            GeoVector3 d = e.N.Add(e.U.Multiply(0.3)).Normalize();
            GeoPoint3 below = first.Add(d.Multiply(-40)), above = first.Add(d.Multiply(40));
            GeoPoint3 beyond = above.Add(above.GetVectorTo(second).Multiply(1.5));
            var chain = new GeoPolylineArc3(new[] { below, above, beyond });

            SamePointsInOrder(new[] { first, second }, e.Build().GetIntersections(chain, Tol), Close);
            Assert.True(e.Build().CollidesWith(chain, Tol));
        }

        [Fact]
        public void ACurvedLoopIsCrossedEdgeByEdgeAndALoopRoundTheEllipseOrInsideTheDiscTouchesNothing()
        {
            // x = ±100, y = ±200 crosses the rim at (±100, ±94.28) on its long sides. The loop (±400, ±200) runs round the
            // ellipse and (±50, ±20) inside it: neither has an edge that reaches the rim.
            Specimen e = Tilted();
            var across = new GeoPolygonArc3(new[] { e.At(-100, -200, 0), e.At(100, -200, 0), e.At(100, 200, 0), e.At(-100, 200, 0) });
            var round = new GeoPolygonArc3(new[] { e.At(-400, -200, 0), e.At(400, -200, 0), e.At(400, 200, 0), e.At(-400, 200, 0) });
            var inside = new GeoPolygonArc3(new[] { e.At(-50, -20, 0), e.At(50, -20, 0), e.At(50, 20, 0), e.At(-50, 20, 0) });

            GeoPoint3[] crossings = e.Build().GetIntersections(across, Tol);

            Assert.Equal(4, crossings.Length);
            SamePointsInOrder(EdgeByEdge(e, across.GetEdges()), crossings, Close);
            Assert.True(e.Build().CollidesWith(across, Tol));
            Assert.Empty(e.Build().GetIntersections(round, Tol));
            Assert.False(e.Build().CollidesWith(round, Tol));
            Assert.Empty(e.Build().GetIntersections(inside, Tol));
            Assert.False(e.Build().CollidesWith(inside, Tol));
        }

        [Fact]
        public void ANullChainOrLoopIsRefused()
        {
            // As the circle's are (ArcChain3.Crossings.cs:370-373, 391-394, 757-760, 778-781).
            GeoEllipse3 ellipse = Tilted().Build();

            Assert.Throws<ArgumentNullException>(() => ellipse.GetIntersections((GeoPolylineArc3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.CollidesWith((GeoPolylineArc3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.GetIntersections((GeoPolygonArc3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.CollidesWith((GeoPolygonArc3)null, Tol));
        }
    }
}
