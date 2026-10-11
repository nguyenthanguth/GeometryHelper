using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Three answers a body gave about a flat region that its distance contradicted, found while writing the tests of
    /// the collisions in space, and fixed since. Each body is the box 40 by 40 by 20 from the origin.
    /// </summary>
    /// <remarks>
    /// A face with holes used to be asked as its outline and then refined by the body's triangle edges piercing it, so
    /// contact the face's own edges made was lost once it had a hole; and a polygon was asked against the body's raw
    /// faces, which run straight across every opening, so a region passing through an opening touched a face that is
    /// not there. Both are now read against the body's material.
    /// </remarks>
    public class BodyCollisionBreakTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private static GeoSolid3 Box() => Block(Pt(0, 0, 0), Pt(40, 40, 20));

        [Fact]
        public void AFaceWithAHoleTouchingABodyAtACorner_CollidesWithIt()
        {
            // A diamond upright in y = 10 standing on its corner (20, 10, 20), on the top face; the hole is 8 above.
            // The outline alone collides, and is nought away.
            GeoPolygon3 diamond = Polygon(Pt(20, 10, 20), Pt(30, 10, 30), Pt(20, 10, 40), Pt(10, 10, 30));
            GeoFace3 face = Holed(diamond, Across(10, 18, 28, 22, 32));

            Assert.True(diamond.CollidesWith(Box(), Within));
            Assert.True(face.CollidesWith(Box(), Within));
            Assert.True(Box().CollidesWith(face, Within));
        }

        [Fact]
        public void AFaceWithAHoleCrossingTheTopOfABody_CollidesWithIt()
        {
            // Upright in y = 10 from x = 12 to 28 and z = 12 to 40, so it crosses the top at z = 20 between x = 12 and
            // 28. Its centroid, at z = 26, is outside, and the top's diagonals meet y = 10 at x = 10 or 30, off it.
            GeoPolygon3 strip = Across(10, 12, 12, 28, 40);
            GeoFace3 face = Holed(strip, Across(10, 16, 30, 24, 36));

            Assert.True(strip.CollidesWith(Box(), Within));
            Assert.True(face.CollidesWith(Box(), Within));
        }

        [Fact]
        public void APolygonThroughABodysOpeningWithoutTouchingIt_DoesNotCollideWithIt()
        {
            // An opening 10 by 10 from x, y = 15 to 25 right through; the polygon is upright in y = 20 from x = 17 to
            // 23, 2 clear of the walls, and runs from z = -10 to 30. Its distance to the body is 2.
            GeoSolid3 pierced = Box().WithOpenings(new[] { Block(Pt(15, 15, -5), Pt(25, 25, 25)) });
            GeoPolygon3 through = Across(20, 17, -10, 23, 30);

            Assert.Equal(2.0, through.DistanceTo(pierced, Within), 9);
            Assert.False(through.CollidesWith(pierced, Within));
            Assert.False(new GeoFace3(through).CollidesWith(pierced, Within));
        }
    }
}
