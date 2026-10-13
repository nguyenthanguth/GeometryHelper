using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A corner beside a nearly flat edge is rounded, and a curved chain's offset is mitred there, as beside a straight one.
    /// </summary>
    /// <remarks>
    /// The fillet's centre, and the point an offset runs its pieces on to, are where two circles cross, and both were
    /// worked out from the first circle. Behind an edge 100 long of bulge 1E-7 lies a circle of radius 2.5E8, and from
    /// there the half chord came out 0.076 off: the corner of the chain from (0, 0) along that edge to (100, 0), then by
    /// bulge 0.4 to (110, 30), was left unrounded, and its offset 2 to the left took a stray corner at (99.06, 1.77). They
    /// are now worked out from the smaller circle.
    /// </remarks>
    public class CollisionFaultB11NearFlatCornerTests
    {
        /// <summary>The chain from (0, 0) by the given bulge to (100, 0), then by bulge 0.4 to (110, 30).</summary>
        private static GeoPolylineArc2 Chain(double bulge)
            => new GeoPolylineArc2(new[] { P(0, 0), P(100, 0), P(110, 30) }, new[] { bulge, 0.4 });

        /// <summary>The arc of bulge 0.4 from (100, 0) to (110, 30), counter-clockwise about a centre to its left.</summary>
        private static GeoArc2 SecondArc() => new GeoEdge2(P(100, 0), P(110, 30), 0.4).ToArc();

        /// <summary>
        /// Where the nearly flat edge from (0, 0) to (100, 0), moved to its left by a distance, passes x, worked out
        /// here. The edge lies on the circle of radius R = 50 (1 + b²) / 2b about (50, k), k = √(R² − 2 500), and moved
        /// by d on the circle of radius ρ = R − d. Its height there is k − √(ρ² − u²) with u = x − 50, written as
        /// (d (2R − d) − 2 500 + u²) / (k + √(ρ² − u²)) so that nothing near R² is taken from anything near R².
        /// </summary>
        private static double FlatEdgeMovedBy(double bulge, double distance, double x)
        {
            double radius = 50 * (1 + bulge * bulge) / (2 * bulge);
            double k = Math.Sqrt(radius * radius - 2500);
            double rho = radius - distance;
            double u = x - 50;

            return (distance * (2 * radius - distance) - 2500 + u * u) / (k + Math.Sqrt(rho * rho - u * u));
        }

        [Theory]
        [InlineData(1E-7)]
        [InlineData(1E-9)]
        public void ACornerBesideANearlyFlatEdge_IsRoundedAsBesideAStraightOne(double bulge)
        {
            // The fillet of radius 3 runs between two new corners, as it does when the first edge is straight; its
            // centre is 3 from the flat edge and 3 inside the arc's circle, and its corners lie within a hundred
            // thousandth of the straight edge's.
            GeoPolylineArc2 straight = Corner2.Fillet(Chain(0.0), 3.0, TenThousandth);
            GeoPolylineArc2 rounded = Corner2.Fillet(Chain(bulge), 3.0, TenThousandth);

            Assert.Equal(4, straight.Vertices.Count);
            Assert.Equal(4, rounded.Vertices.Count);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(rounded.Vertices[i].DistanceTo(straight.Vertices[i]), 0.0, 1E-5);
            }

            GeoArc2 fillet = new GeoEdge2(rounded.Vertices[1], rounded.Vertices[2], rounded.Bulges[1]).ToArc();
            GeoArc2 second = SecondArc();

            Assert.Equal(3.0, fillet.Radius, 6);
            Assert.InRange(Math.Abs(fillet.Center.DistanceTo(second.Center) - (second.Radius - 3)), 0.0, 1E-5);
            Assert.InRange(Math.Abs(fillet.Center.Y - FlatEdgeMovedBy(bulge, 3, fillet.Center.X)), 0.0, 1E-5);
        }

        [Theory]
        [InlineData(1E-7, 2.0)]
        [InlineData(1E-7, -2.0)]
        [InlineData(1E-9, 2.0)]
        [InlineData(1E-9, -2.0)]
        public void AMiteredOffsetBesideANearlyFlatEdge_TurnsWhereTheTwoMovedCurvesCross(double bulge, double distance)
        {
            // The offset is one chain of three corners. Its middle corner lies on both moved curves: the arc's circle,
            // its radius less the distance, and the flat edge moved by the distance, both worked out here.
            GeoPolylineArc2 offset = Assert.Single(Offset2.Offset(Chain(bulge), distance, new OffsetOptions(OffsetJoin.Miter), TenThousandth));

            Assert.Equal(3, offset.Vertices.Count);

            GeoPoint2 corner = offset.Vertices[1];
            GeoArc2 second = SecondArc();

            Assert.InRange(Math.Abs(corner.DistanceTo(second.Center) - (second.Radius - distance)), 0.0, 1E-5);
            Assert.InRange(Math.Abs(corner.Y - FlatEdgeMovedBy(bulge, distance, corner.X)), 0.0, 1E-5);
        }
    }
}
