using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The nearest point of a face or a polygon to a point whose foot lands a hair outside the material, within the
    /// point tolerance: the inside test is tolerant, so the foot is kept, and it lies no further than the point tolerance
    /// from the material, as the documentation of the projection says.
    /// </summary>
    /// <remarks>
    /// The face is the square 10 by 10 in z = 0 with the hole from (4, 4) to (6, 6). Each point stands 3 over the plane,
    /// its foot 0.0005 outside the material, half the point tolerance. The material's nearest point under it is worked by
    /// hand: the rim straight beside the foot.
    /// </remarks>
    public class OldFaultF7Tests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private static GeoFace3 Plate() => Holed(Square(0, 0, 10, 10), Square(4, 4, 6, 6));

        private static void AssertWithinThePointToleranceOf(GeoPoint3 rim, GeoPoint3 foot)
        {
            Assert.True(foot.DistanceTo(rim) <= Within.EqualPoint, "the foot " + foot + " is " + foot.DistanceTo(rim) + " from the material");
            Assert.Equal(0.0, foot.Z, 12);
        }

        [Fact]
        public void APointWhoseFootIsAHairPastTheOutline_IsJoinedWithinThePointToleranceOfTheOutline()
        {
            // The foot (10.0005, 5, 0) is past the side x = 10; the nearest point of the material is (10, 5, 0).
            GeoPoint3 point = Pt(10.0005, 5, 3);
            GeoPoint3 rim = Pt(10, 5, 0);

            AssertWithinThePointToleranceOf(rim, Plate().GetClosestPointOnBoundary(point, Within));
            AssertWithinThePointToleranceOf(rim, Plate().GetShortestLineTo(point, Within).StartPoint);
        }

        [Fact]
        public void APointWhoseFootIsAHairInsideAHole_IsJoinedWithinThePointToleranceOfTheHolesRim()
        {
            // The foot (4.0005, 5, 0) is in the hole, past its side x = 4; the nearest point of the material is (4, 5, 0).
            GeoPoint3 point = Pt(4.0005, 5, 3);
            GeoPoint3 rim = Pt(4, 5, 0);

            AssertWithinThePointToleranceOf(rim, Plate().GetClosestPointOnBoundary(point, Within));
            AssertWithinThePointToleranceOf(rim, Plate().GetShortestLineTo(point, Within).StartPoint);
        }

        [Fact]
        public void APointWhoseFootIsAHairPastAPolygonsOutline_IsJoinedWithinThePointToleranceOfTheOutline()
        {
            // The square alone: the foot (10.0005, 5, 0) is past its side x = 10.
            GeoPolygon3 square = Square(0, 0, 10, 10);
            GeoPoint3 point = Pt(10.0005, 5, 3);
            GeoPoint3 rim = Pt(10, 5, 0);

            AssertWithinThePointToleranceOf(rim, Projection3.ProjectToPolygon(square, point, Within));
            AssertWithinThePointToleranceOf(rim, square.GetShortestLineTo(point, Within).StartPoint);
        }

        [Fact]
        public void APointWhoseFootIsAHairPastTheOutline_IsAsFarAsTheRimToRounding()
        {
            // 3 straight down to the kept foot, against 3.00000004 to the rim: the two differ by less than a ten-millionth.
            GeoPoint3 point = Pt(10.0005, 5, 3);

            Assert.Equal(System.Math.Sqrt(9 + 0.0005 * 0.0005), Plate().DistanceTo(point, Within), 7);
        }
    }
}
