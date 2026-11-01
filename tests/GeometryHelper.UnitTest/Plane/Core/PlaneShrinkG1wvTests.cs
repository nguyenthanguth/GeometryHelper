using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A polygon or a face in the plane shrunk or sheared keeps every corner and has its area scaled, however narrow it comes
    /// out: a sliver 0.005 wide shrunk by a tenth is kept 0.0005 wide, under the global tolerance, since the plane has no
    /// flatness to check again, and its twins in space now carry a shrunk shape too.
    /// </summary>
    public class PlaneShrinkG1wvTests
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // A shear of X along Y by 0.5, then a scaling by 0.1: areas come out a hundredth of what they were.
        private static GeoTransform2 ShearAndShrink()
            => GeoTransform2.Scaling(0.1) * new GeoTransform2(new double[,] { { 1, 0.5, 0 }, { 0, 1, 0 }, { 0, 0, 1 } });

        [Fact]
        public void ASliverFiveThousandthsWideShrunkAndShearedToUnderAThousandth_KeepsItsCornersAndAHundredthOfItsArea()
        {
            var sliver = new GeoPolygon2(P(0, 0), P(30, 0), P(30, 0.005), P(0, 0.005));

            GeoPolygon2 moved = sliver.TransformBy(ShearAndShrink());

            Assert.Equal(4, moved.VertexCount);
            Assert.Equal(sliver.Area / 100.0, moved.Area, 12);
        }

        [Fact]
        public void AFaceWithANarrowHoleShrunkAndSheared_KeepsTheHoleAndAHundredthOfItsArea()
        {
            var face = new GeoFace2(
                new GeoPolygon2(P(0, 0), P(30, 0), P(30, 20), P(0, 20)),
                new[] { new GeoPolygon2(P(5, 5), P(5, 5.005), P(25, 5.005), P(25, 5)) });

            GeoFace2 moved = face.TransformBy(ShearAndShrink());

            Assert.Single(moved.Holes);
            Assert.Equal(4, moved.Holes[0].VertexCount);
            Assert.Equal(face.Area / 100.0, moved.Area, 9);
        }
    }
}
