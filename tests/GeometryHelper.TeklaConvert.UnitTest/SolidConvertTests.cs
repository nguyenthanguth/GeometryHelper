using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// A Tekla solid read as its faces come: each face its loops of corners, boundary first, and the normal Tekla gives
    /// it. Tekla does not promise which way round a loop is walked, and its own cuts leave faces a little out of flat;
    /// the body has to come out closed and facing out all the same. Reading the faces out of Tekla itself needs a running
    /// Tekla and is not covered here.
    /// </summary>
    public class SolidConvertTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoVector3 Up = GeoVector3.ZAxis;

        [Fact]
        public void AFaceWalkedAgainstItsNormalIsTurnedToFaceIt()
        {
            GeoPoint3[] clockwise = { P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0) };

            Assert.True(FaceConvert.TryReadFaces(Up, new[] { clockwise }, Default, out GeoFace3[] faces));

            GeoFace3 face = Assert.Single(faces);
            Assert.True(face.Normal.IsCodirectionalTo(Up, Default));
        }

        [Fact]
        public void AFaceOutOfFlatComesAsTrianglesFacingTheWayTeklaSays()
        {
            // One corner a millimetre up, and walked against the normal.
            GeoPoint3[] clockwise = { P(0, 0, 0), P(0, 100, 0), P(100, 100, 1), P(100, 0, 0) };

            Assert.True(FaceConvert.TryReadFaces(Up, new[] { clockwise }, Default, out GeoFace3[] faces));

            Assert.Equal(2, faces.Length);
            Assert.All(faces, face => Assert.True(face.Normal.Z > 0.99));
        }

        [Fact]
        public void AFaceWithNoAreaIsLeftOut()
        {
            GeoPoint3[] line = { P(0, 0, 0), P(50, 0, 0), P(100, 0, 0) };

            Assert.False(FaceConvert.TryReadFaces(Up, new[] { line }, Default, out GeoFace3[] faces));
            Assert.Empty(faces);
        }

        [Fact]
        public void APlateWithACornerOfItsHoleOutOfFlatComesOutClosed()
        {
            // 200 x 200 x 20 with a 40 x 40 hole through it, one corner of the hole's top rim pushed 0.3 mm out each way:
            // the hole in the top face is off the face's plane, and the two walls of the hole meeting there are out of
            // flat. Read a face at a time, into polygons, the top face lost its hole and the two walls were refused.
            GeoPoint3 b0 = P(0, 0, 0), b1 = P(200, 0, 0), b2 = P(200, 200, 0), b3 = P(0, 200, 0);
            GeoPoint3 t0 = P(0, 0, 20), t1 = P(200, 0, 20), t2 = P(200, 200, 20), t3 = P(0, 200, 20);
            GeoPoint3 hb0 = P(80, 80, 0), hb1 = P(120, 80, 0), hb2 = P(120, 120, 0), hb3 = P(80, 120, 0);
            GeoPoint3 ht0 = P(80, 80, 20), ht1 = P(120, 80, 20), ht2 = P(120.3, 120.3, 20.3), ht3 = P(80, 120, 20);

            // Each face as Tekla might walk it, either way round, with the normal pointing out of the material.
            var teklaFaces = new List<(GeoVector3 Outward, GeoPoint3[][] Loops)>
            {
                (Up, new[] { new[] { t0, t1, t2, t3 }, new[] { ht0, ht1, ht2, ht3 } }),
                (Up.Negate(), new[] { new[] { b0, b1, b2, b3 }, new[] { hb3, hb2, hb1, hb0 } }),
                (GeoVector3.YAxis.Negate(), new[] { new[] { b0, b1, t1, t0 } }),
                (GeoVector3.XAxis, new[] { new[] { t1, t2, b2, b1 } }),
                (GeoVector3.YAxis, new[] { new[] { b2, b3, t3, t2 } }),
                (GeoVector3.XAxis.Negate(), new[] { new[] { t3, t0, b0, b3 } }),
                (GeoVector3.YAxis, new[] { new[] { hb0, hb1, ht1, ht0 } }),
                (GeoVector3.XAxis.Negate(), new[] { new[] { hb1, hb2, ht2, ht1 } }),
                (GeoVector3.YAxis.Negate(), new[] { new[] { ht3, ht2, hb2, hb3 } }),
                (GeoVector3.XAxis, new[] { new[] { hb3, hb0, ht0, ht3 } }),
            };

            var faces = new List<GeoFace3>();
            foreach ((GeoVector3 outward, GeoPoint3[][] loops) in teklaFaces)
            {
                Assert.True(FaceConvert.TryReadFaces(outward, loops, Default, out GeoFace3[] read));
                faces.AddRange(read);
            }

            Assert.True(SolidConvert.TryAssemble(faces, out GeoSolid3 plate));

            Assert.True(plate.IsClosed(Default));
            Assert.InRange(plate.GetVolume(), (200.0 * 200.0 - 40.0 * 40.0) * 20.0 * 0.99, (200.0 * 200.0 - 40.0 * 40.0) * 20.0 * 1.01);
            Assert.Equal(PointLocation.OutSide, plate.Locate(P(100, 100, 10), Default));
            Assert.Equal(PointLocation.Inside, plate.Locate(P(40, 40, 10), Default));
        }

        [Fact]
        public void ABodyWhoseNormalsAllPointInIsTurnedInsideOut()
        {
            GeoPoint3[] c =
            {
                P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0),
                P(0, 0, 100), P(100, 0, 100), P(100, 100, 100), P(0, 100, 100),
            };

            // Every normal Tekla gives points into the cube: the whole surface came in reversed.
            var teklaFaces = new List<(GeoVector3 Inward, int[] Loop)>
            {
                (Up, new[] { 0, 1, 2, 3 }), (Up.Negate(), new[] { 4, 5, 6, 7 }),
                (GeoVector3.YAxis, new[] { 0, 1, 5, 4 }), (GeoVector3.XAxis.Negate(), new[] { 1, 2, 6, 5 }),
                (GeoVector3.YAxis.Negate(), new[] { 2, 3, 7, 6 }), (GeoVector3.XAxis, new[] { 3, 0, 4, 7 }),
            };

            var faces = new List<GeoFace3>();
            foreach ((GeoVector3 inward, int[] loop) in teklaFaces)
            {
                Assert.True(FaceConvert.TryReadFaces(inward, new[] { loop.Select(i => c[i]).ToArray() }, Default, out GeoFace3[] read));
                faces.AddRange(read);
            }

            Assert.True(SolidConvert.TryAssemble(faces, out GeoSolid3 cube));

            Assert.Same(cube, cube.TurnOutwards());
            Assert.Equal(1E6, cube.GetVolume(), 6);
            Assert.Equal(PointLocation.Inside, cube.Locate(P(50, 50, 50), Default));
        }

        [Fact]
        public void FewerThanFourFacesMakeNoBody()
        {
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(0, 100, 0))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 100, 0), P(0, 0, 100))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 0, 100), P(100, 0, 0))),
            };

            Assert.False(SolidConvert.TryAssemble(faces, out GeoSolid3 body));
            Assert.Null(body);
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);
    }
}
