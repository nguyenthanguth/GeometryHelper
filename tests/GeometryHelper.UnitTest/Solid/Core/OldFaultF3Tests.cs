using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two triangles a hundred-thousandth of a radian apart whose sides cross in one plane, found touching or not by which
    /// of them is asked first.
    /// </summary>
    /// <remarks>
    /// The test of two triangles reads whether they lie in one plane from the first triangle's plane and the second's
    /// first corner only. No side of either pierces the other here, so the answer falls to that one corner.
    /// </remarks>
    public class OldFaultF3Tests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        // The floor triangle in z = 0.
        private static GeoTriangle3 Floor() => Tri(Pt(0, 0, 0), Pt(50, 0, 0), Pt(0, 30, 0));

        // Its side from (-10, 10) to (60, 10) lies in z = 0 and runs across the floor, crossing the floor's side x = 0 at
        // (0, 10, 0); its first corner stands 0.0013 over the floor, 0.3 of the planar tolerance past it, 100 from that
        // side: a tilt of 1.3E-5 radians, far under the angle tolerance of one degree.
        private static GeoTriangle3 Tilted() => Tri(Pt(25, 110, 0.0013), Pt(-10, 10, 0), Pt(60, 10, 0));

        [Fact]
        public void TwoTrianglesWhoseSidesCrossInOnePlane_CollideWhicheverIsAskedFirst()
        {
            // The oracle: both sides lie in z = 0 and cross at (0, 10, 0), so the triangles are nought apart. Against
            // each other's plane: the tilted side's other two run from 1.3E-3 down to nought at (-10, 10) and (60, 10),
            // both outside the floor, so neither crosses it; the floor stands 1.3E-4 under the tilted plane at y = 0 and
            // 2.6E-4 over it at y = 30, so the floor lies in that plane within the planar tolerance, and the two lie in one
            // plane as the flat shapes read it.
            Assert.Equal(0.0, Floor().DistanceTo(Tilted(), Within), 9);

            Assert.True(Tilted().CollidesWith(Floor(), Within));
            Assert.True(Floor().CollidesWith(Tilted(), Within));
        }
    }
}
