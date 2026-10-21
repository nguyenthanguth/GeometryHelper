using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A thin box has its faces, read from its own numbers rather than against the global tolerance.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoObb3.GetFaces"/> refused any box at most twice the global point tolerance on some axis, "a zero size
    /// on some axis", so a plate 0.002 thick had no faces and no solid. A side is nought now only when it is nought to the
    /// rounding of the box's own numbers.
    /// </remarks>
    public class BoxFacesFaultLN2ThinTests
    {
        private static GeoObb3 Plate(double thickness)
            => new GeoObb3(new GeoCoordinateSystem3(new GeoPoint3(1, 2, 3), GeoVector3.XAxis, GeoVector3.YAxis), thickness, 50, 30);

        [Theory]
        [InlineData(0.002)]
        [InlineData(0.0002)]
        public void AThinPlate_HasItsSixFaces(double thickness)
        {
            GeoPolygon3[] faces = Plate(thickness).GetFaces();

            Assert.Equal(6, faces.Length);
            Assert.Equal(2.0 * (thickness * 50 + thickness * 30 + 50 * 30), faces.Sum(f => f.Area), 9);
            Assert.Equal(thickness * 50 * 30, Plate(thickness).ToSolid().GetVolume(new Tolerance(1E-6, 1E-6)), 9);
        }

        [Fact]
        public void TheFacesOfAThinPlate_FaceOutwards()
        {
            GeoObb3 plate = Plate(0.002);

            foreach (GeoPolygon3 face in plate.GetFaces())
            {
                GeoVector3 outward = plate.Center.GetVectorTo(face.Centroid);

                Assert.True(face.Normal.DotProduct(outward) > 0.0);
            }
        }

        [Fact]
        public void ABoxWithNoThickness_IsStillRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Plate(0.0).GetFaces());
            Assert.Throws<InvalidOperationException>(() => Plate(0.0).ToSolid());
        }

        [Fact]
        public void AnOrdinaryBox_HasTheFacesItHad()
        {
            var box = new GeoObb3(new GeoPoint3(1E5, -2E5, 30), 40, 25, 12, new GeoVector3(0.3, 0.7, -0.2), new GeoVector3(-0.6, 0.2, 0.1));
            GeoPoint3[] c = box.GetCorners();
            GeoPolygon3[] faces = box.GetFaces();
            GeoPolygon3 bottom = new GeoPolygon3(c[0], c[3], c[2], c[1]);

            Assert.Equal(bottom.Normal, faces[0].Normal);
            Assert.Equal(bottom.Area, faces[0].Area);
            Assert.Equal(bottom.Vertices, faces[0].Vertices);
        }
    }
}
