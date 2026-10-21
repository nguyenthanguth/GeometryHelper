using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A triangle whose corners lie on one line to rounding has no plane, and is measured as its sides.
    /// </summary>
    /// <remarks>
    /// The gap kept the plane of the cross product of such a triangle, whose direction is the rounding of the products: an arc
    /// passing 9E-16 from the middle of the triangle (0.1, 0.2, 0.3), (70.3, 20.7, 11.1) and the middle of the two, twice
    /// its area 2.5E-13, did not collide with it and was given two points (594 of 3 000 arcs near such triangles), and a point
    /// on a side of another stood 0.843 from it. The plane is dropped now where twice the area is within a few parts in 1E16
    /// of the longest side squared, as <see cref="GeoTriangle3.Normal"/> reads it. A sliver that has a plane stands off it,
    /// to the rounding of its normal, by as much as its corners do, and an arc is read against it so: arcs near triangles
    /// 1E-11 high collided otherwise than their sides said for 13 of 3 000.
    /// </remarks>
    public class TriangleFaultLN4CollinearTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoPoint3 A = new GeoPoint3(0.1, 0.2, 0.3);

        private static readonly GeoPoint3 B = new GeoPoint3(70.3, 20.7, 11.1);

        private static readonly GeoPoint3 Middle = new GeoPoint3((0.1 + 70.3) / 2, (0.2 + 20.7) / 2, (0.3 + 11.1) / 2);

        private static readonly GeoTriangle3 Collinear = new GeoTriangle3(A, B, Middle);

        /// <summary>An arc in the plane square to the side, dipping to <paramref name="off"/> over its middle.</summary>
        private static GeoArc3 Dipping(double off)
        {
            GeoVector3 along = A.GetVectorTo(B);
            GeoVector3 side = along.CrossProduct(GeoVector3.ZAxis).Normalize();
            GeoVector3 up = side.CrossProduct(along).Normalize();

            return GeoArc3.FromThreePoints(
                Middle.Add(up.Multiply(off + 1)).Add(side.Multiply(-3)), Middle.Add(up.Multiply(off)), Middle.Add(up.Multiply(off + 1)).Add(side.Multiply(3)), Default);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void AnArcDippingWithinTheToleranceOfTheLine_CollidesAndIsGivenItsLowestPoint(double off)
        {
            GeoArc3 arc = Dipping(off);

            Assert.True(Arc3.CollidesWith(arc, Collinear, Default));
            Assert.True(Assert.Single(Arc3.GetIntersections(arc, Collinear, Default)).DistanceTo(arc.MidPoint) < 1E-9);
        }

        [Fact]
        public void AnArcDippingFurtherThanTheTolerance_MissesIt()
        {
            GeoArc3 arc = Dipping(0.0011);

            Assert.False(Arc3.CollidesWith(arc, Collinear, Default));
            Assert.Empty(Arc3.GetIntersections(arc, Collinear, Default));
        }

        [Fact]
        public void AnArcThroughTheMiddle_CollidesWithIt()
        {
            var arc = new GeoArc3(Middle.Add(new GeoVector3(0, 0, 5)), new GeoVector3(20.5, -70.2, 0), 5, 0, 6);

            Assert.True(Arc3.CollidesWith(arc, Collinear, Default));
            Assert.NotEmpty(Arc3.GetIntersections(arc, Collinear, Default));
        }

        [Fact]
        public void AnArcPassingNearASliverWhoseNormalIsMostlyRounding_CollidesWithIt()
        {
            // 1E-11 high over its side 100 long: its corners stand off the plane of its cross product by its rounding, up to a tenth, which
            // the gap took for flat and so ruled the arc clear, 0.000095 from the sliver.
            var sliver = new GeoTriangle3(
                new GeoPoint3(4.0767664015650595, 8.403841819802226, 1.5144763288621217),
                new GeoPoint3(75.20450224059516, -42.59911254061243, 49.91623263875975),
                new GeoPoint3(39.64063432107841, -17.097635360405224, 25.715354483813318));
            var arc = new GeoArc3(
                new GeoPoint3(31.655260075260102, -1.9249805757814578, 18.11740498997277),
                new GeoVector3(-0.2991985783599241, 0.09460561973430275, 0.9494893298094976),
                7.7156257651353375,
                0,
                2);

            Assert.True(sliver.IsDegenerate(Default));
            Assert.True(Arc3.CollidesWith(arc, sliver, Default));
            Assert.NotEmpty(Arc3.GetIntersections(arc, sliver, Default));
        }

        [Fact]
        public void APointOnASideOfATriangleCollinearToRounding_IsOnIt()
        {
            var triangle = new GeoTriangle3(
                new GeoPoint3(5.842443437242155, 4.3388700132904905, 1.8095674513883737),
                new GeoPoint3(1.416801915031768, 7.172257219432319, 5.460463223838199),
                new GeoPoint3(3.6296226761369614, 5.755563616361405, 3.6350153376132868));
            var onSide = new GeoPoint3(1.8307779963589077, 6.907221172885283, 5.118957135369981);

            Assert.True(triangle.DistanceTo(onSide) < 1E-12);
            Assert.True(triangle.GetShortestLineTo(onSide).Length < 1E-12);
        }

        [Fact]
        public void AnEllipseAgainstATriangleOnALine_IsAnsweredAsItsSides()
        {
            var line = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 0, 0));
            var ellipse = new GeoEllipse3(new GeoPoint3(50, 0, 5), new GeoVector3(0, 1, 0), new GeoVector3(0, 0, 1), 5, 3);

            Assert.True(ellipse.CollidesWith(line, Default));
            Assert.True(Assert.Single(ellipse.GetIntersections(line, Default)).DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-9);
        }
    }
}
