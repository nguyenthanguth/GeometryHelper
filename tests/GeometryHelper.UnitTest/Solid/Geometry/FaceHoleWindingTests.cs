using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face holds its holes wound the same way as its boundary, which is what lets a body take them away from its
    /// volume by plain subtraction. The plane winds a hole against its boundary, and the faces lifted from it kept
    /// that: the booleans of flat shapes, the offsets of faces, anything laid out in a plane and put back.
    /// </summary>
    public class FaceHoleWindingTests
    {
        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z, bool up)
            => up
                ? new GeoPolygon3(new[] { new GeoPoint3(x0, y0, z), new GeoPoint3(x1, y0, z), new GeoPoint3(x1, y1, z), new GeoPoint3(x0, y1, z) })
                : new GeoPolygon3(new[] { new GeoPoint3(x0, y0, z), new GeoPoint3(x0, y1, z), new GeoPoint3(x1, y1, z), new GeoPoint3(x1, y0, z) });

        private static GeoPolygon3 Wall(GeoPoint3 a, GeoPoint3 b, double height)
            => new GeoPolygon3(new[] { a, b, new GeoPoint3(b.X, b.Y, b.Z + height), new GeoPoint3(a.X, a.Y, a.Z + height) });

        /// <summary>
        /// A plate ten by ten by one with a two by two hole through it, its top face carrying the hole wound
        /// either way.
        /// </summary>
        private static GeoSolid3 Plate(bool topHoleAlongTheBoundary)
        {
            var faces = new[]
            {
                new GeoFace3(Square(0, 0, 10, 10, 0, false), new[] { Square(4, 4, 6, 6, 0, false) }),
                new GeoFace3(Square(0, 0, 10, 10, 1, true), new[] { Square(4, 4, 6, 6, 1, topHoleAlongTheBoundary) }),
                new GeoFace3(Wall(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(0, 10, 0), new GeoPoint3(0, 0, 0), 1)),

                // The walls of the hole face into it, away from the material.
                new GeoFace3(Wall(new GeoPoint3(4, 4, 0), new GeoPoint3(4, 6, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(4, 6, 0), new GeoPoint3(6, 6, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(6, 6, 0), new GeoPoint3(6, 4, 0), 1)),
                new GeoFace3(Wall(new GeoPoint3(6, 4, 0), new GeoPoint3(4, 4, 0), 1)),
            };

            return new GeoSolid3(faces);
        }

        [Fact]
        public void AHoleGivenAgainstTheBoundary_IsHeldAsTheBoundaryRuns()
        {
            var face = new GeoFace3(Square(0, 0, 10, 10, 0, true), new[] { Square(4, 4, 6, 6, 0, false) });

            Assert.True(face.Holes[0].Normal.IsCodirectionalTo(face.Normal));
            Assert.Equal(96.0, face.Area, 9);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ABodyMeasuresItsMaterial_WhicheverWayItsFacesHoleWasGiven(bool alongTheBoundary)
        {
            GeoSolid3 plate = Plate(alongTheBoundary);

            Assert.True(plate.IsClosed());
            Assert.Equal(96.0, plate.Volume, 9);
        }

        [Fact]
        public void AFaceWithAHoleOutOfTheFlatBooleans_HoldsItAsTheBoundaryRuns()
        {
            GeoFace3[] ring = Boolean3.Subtract(new GeoFace3(Square(0, 0, 10, 10, 0, true)), new GeoFace3(Square(4, 4, 6, 6, 0, true)));

            GeoFace3 face = Assert.Single(ring);
            GeoPolygon3 hole = Assert.Single(face.Holes);
            Assert.True(hole.Normal.IsCodirectionalTo(face.Normal));
        }

        [Fact]
        public void TheCentroid_TakesAwayTheHolesInTheFaces()
        {
            // A plate a hundred square and ten thick with a ten by ten hole near one corner, cut in so that its top
            // and bottom faces carry the hole: the material is pulled away from that corner.
            GeoSolid3 plate = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 10)).ToObb().ToSolid()
                .WithOpenings(new[] { new GeoAabb3(new GeoPoint3(80, 80, -5), new GeoPoint3(90, 90, 15)).ToObb().ToSolid() });
            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
            Assert.Contains(material.Faces, face => face.Holes.Count > 0);

            double expected = (100000.0 * 50 - 1000.0 * 85) / 99000.0;
            Assert.Equal(99000.0, material.Volume, 6);
            Assert.Equal(expected, material.Centroid.X, 9);
            Assert.Equal(expected, material.Centroid.Y, 9);
            Assert.Equal(5.0, material.Centroid.Z, 9);
        }
    }
}
