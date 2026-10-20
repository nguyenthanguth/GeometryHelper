using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A ball in the writers: the triangles of its surface within a chord tolerance, as a body is written as its triangles, in OBJ,
    /// DXF (a 3DFACE each) and WKT (a POLYHEDRALSURFACE); and what each refuses.
    /// </summary>
    public class SphereExportTests
    {
        private static readonly GeoSphere3 Ball = new GeoSphere3(new GeoPoint3(10.5, -20.25, 7.125), 50.0);

        // A ball of radius 50 cut within 0.1 is 20 by 13 by 13 triangles.
        private const int Triangles = 3380;

        private static double Parse(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static double Away(GeoPoint3 point) => point.DistanceTo(Ball.Center);

        /// <summary>The volume under a closed set of triangles, outwards: the signed volume of each with the centre of the ball as the apex.</summary>
        private static double VolumeOf(IEnumerable<GeoTriangle3> triangles)
        {
            double volume = 0.0;

            foreach (GeoTriangle3 t in triangles)
            {
                GeoVector3 a = Ball.Center.GetVectorTo(t.A);
                GeoVector3 b = Ball.Center.GetVectorTo(t.B);
                GeoVector3 c = Ball.Center.GetVectorTo(t.C);
                volume += a.DotProduct(b.CrossProduct(c)) / 6.0;
            }

            return volume;
        }

        private static GeoPoint3 Point(string[] fields, int first) => new GeoPoint3(Parse(fields[first]), Parse(fields[first + 1]), Parse(fields[first + 2]));

        // ----- OBJ ---------------------------------------------------------------------------------------------------

        private static List<GeoTriangle3> ObjTriangles(string text)
        {
            var vertices = new List<GeoPoint3>();
            var triangles = new List<GeoTriangle3>();

            foreach (string line in text.Split('\n'))
            {
                string[] fields = line.Split(' ');

                if (fields[0] == "v")
                {
                    vertices.Add(Point(fields, 1));
                }
                else if (fields[0] == "f")
                {
                    Assert.Equal(4, fields.Length);
                    triangles.Add(new GeoTriangle3(
                        vertices[int.Parse(fields[1], CultureInfo.InvariantCulture) - 1],
                        vertices[int.Parse(fields[2], CultureInfo.InvariantCulture) - 1],
                        vertices[int.Parse(fields[3], CultureInfo.InvariantCulture) - 1]));
                }
            }

            return triangles;
        }

        [Fact]
        public void ObjOfABall_IsAnObjectOfItsTrianglesOnTheSphere_WoundOutwards()
        {
            string text = new ObjWriter().Add(Ball, 0.1, "ball").ToString();

            Assert.Contains("o ball", text.Split('\n'));
            List<GeoTriangle3> triangles = ObjTriangles(text);

            Assert.Equal(Triangles, triangles.Count);
            Assert.All(triangles, t =>
            {
                Assert.Equal(50.0, Away(t.A), 9);
                Assert.Equal(50.0, Away(t.B), 9);
                Assert.Equal(50.0, Away(t.C), 9);
            });

            // Outwards: the volume under each is positive, and the whole is a little under the ball and never over.
            Assert.All(triangles, t => Assert.True(VolumeOf(new[] { t }) > 0.0));
            Assert.InRange(VolumeOf(triangles), Ball.Volume * 0.99, Ball.Volume);
        }

        [Fact]
        public void ObjOfABall_IsWhatTheTrianglesOfItsSurfaceWriteAsAMesh_ToTheByte()
        {
            Assert.Equal(
                new ObjWriter().Add(Ball.TriangulateSurface(0.1), "ball").ToString(),
                new ObjWriter().Add(Ball, 0.1, "ball").ToString());

            Assert.Equal(
                new ObjWriter().Add(Ball.TriangulateSurface(0.1)).ToString(),
                new ObjWriter().Add(Ball, 0.1).ToString());
        }

        [Fact]
        public void TwoBallsInOneObj_AreTwoObjectsAndTheSecondNumbersItsVerticesOnFromTheFirst()
        {
            var second = new GeoSphere3(new GeoPoint3(200, 0, 0), 10.0);
            string text = new ObjWriter().Add(Ball, 1.0, "first").Add(second, 1.0, "second").ToString();

            Assert.Equal(1, text.Split('\n').Count(l => l == "o first"));
            Assert.Equal(1, text.Split('\n').Count(l => l == "o second"));
            Assert.Equal(Ball.TriangulateSurface(1.0).Length + second.TriangulateSurface(1.0).Length, ObjTriangles(text).Count);
        }

        // ----- DXF ---------------------------------------------------------------------------------------------------

        private sealed class Face
        {
            public string Layer { get; set; }

            public Dictionary<int, string> Pairs { get; } = new Dictionary<int, string>();

            public GeoPoint3 Point(int n) => new GeoPoint3(Parse(Pairs[10 + n]), Parse(Pairs[20 + n]), Parse(Pairs[30 + n]));
        }

        private static List<Face> Faces(DxfWriter writer)
        {
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');
            var faces = new List<Face>();
            bool inEntities = false;
            Face current = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "ENDSEC")
                {
                    inEntities = false;
                    current = null;
                }
                else if (code == 2 && i > 0 && lines[i - 2].Trim() == "0" && lines[i - 1] == "SECTION")
                {
                    inEntities = value == "ENTITIES";
                }
                else if (inEntities && code == 0)
                {
                    current = value == "3DFACE" ? new Face() : null;

                    if (current != null)
                    {
                        faces.Add(current);
                    }
                }
                else if (inEntities && current != null)
                {
                    if (code == 8)
                    {
                        current.Layer = value;
                    }
                    else if (!current.Pairs.ContainsKey(code))
                    {
                        current.Pairs.Add(code, value);
                    }
                }
            }

            return faces;
        }

        [Fact]
        public void DxfOfABall_IsA3dfaceForEachTriangle_OnItsLayer_EveryEdgeVisible_AndClosedOutwards()
        {
            List<Face> faces = Faces(new DxfWriter().Add(Ball, 0.1, "balls"));

            Assert.Equal(Triangles, faces.Count);
            Assert.All(faces, f => Assert.Equal("balls", f.Layer));
            Assert.All(faces, f => Assert.False(f.Pairs.ContainsKey(70), "an edge is hidden"));

            // A triangle repeats its third corner.
            Assert.All(faces, f => Assert.Equal(f.Point(2), f.Point(3)));
            Assert.All(faces, f => Assert.Equal(50.0, Away(f.Point(0)), 9));

            var triangles = faces.Select(f => new GeoTriangle3(f.Point(0), f.Point(1), f.Point(2))).ToList();
            Assert.InRange(VolumeOf(triangles), Ball.Volume * 0.99, Ball.Volume);
        }

        [Fact]
        public void DxfOfABall_IsWhatItsTrianglesWriteAsFaces_ToTheByte()
        {
            Assert.Equal(
                new DxfWriter().Add(Ball.TriangulateSurface(0.5), "balls").ToString(),
                new DxfWriter().Add(Ball, 0.5, "balls").ToString());
        }

        // ----- WKT ---------------------------------------------------------------------------------------------------

        [Fact]
        public void WktOfABall_IsAPolyhedralSurfaceOfItsTrianglesInTheOrderOfTheMesh_ToTheLastDigit()
        {
            string text = Wkt.Write(Ball, 0.1);
            GeoTriangle3[] mesh = Ball.TriangulateSurface(0.1);

            Assert.StartsWith("POLYHEDRALSURFACE Z (((", text, StringComparison.Ordinal);
            Assert.EndsWith("))", text, StringComparison.Ordinal);

            string[] parts = text.Substring("POLYHEDRALSURFACE Z (".Length, text.Length - "POLYHEDRALSURFACE Z (".Length - 1)
                .Split(new[] { "), (" }, StringSplitOptions.None);
            Assert.Equal(mesh.Length, parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                string[] points = parts[i].Replace("(", string.Empty).Replace(")", string.Empty).Split(',');
                Assert.Equal(4, points.Length);

                GeoPoint3[] read = points.Select(p => Point(p.Trim().Split(' '), 0)).ToArray();
                Assert.Equal(mesh[i].A, read[0]);
                Assert.Equal(mesh[i].B, read[1]);
                Assert.Equal(mesh[i].C, read[2]);
                Assert.Equal(read[0], read[3]);
            }
        }

        // ----- What each refuses ---------------------------------------------------------------------------------------

        public static IEnumerable<object[]> BadTolerances => new[]
        {
            new object[] { 0.0 },
            new object[] { -0.1 },
            new object[] { double.NaN },
            new object[] { double.PositiveInfinity },
        };

        [Theory]
        [MemberData(nameof(BadTolerances))]
        public void ATolerance_ThatIsNotAFiniteNumberAboveNought_IsRefusedByEveryWriter_AndNothingIsWritten(double tolerance)
        {
            var obj = new ObjWriter().Add(new GeoPoint3(1, 2, 3));
            var dxf = new DxfWriter().Add(new GeoPoint3(1, 2, 3));
            string objBefore = obj.ToString();
            string dxfBefore = dxf.ToString();

            Assert.Throws<ArgumentOutOfRangeException>(() => obj.Add(Ball, tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => dxf.Add(Ball, tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(Ball, tolerance));

            Assert.Equal(objBefore, obj.ToString());
            Assert.Equal(dxfBefore, dxf.ToString());
        }

        [Fact]
        public void ATolerance_ThatWouldNeedMoreThanAMillionTriangles_IsRefused()
        {
            // Radius 50 within 0.0001 would take three million.
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(Ball, 0.0001));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfWriter().Add(Ball, 0.0001));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(Ball, 0.0001));
        }

        [Fact]
        public void ADefaultBall_HasNoSurfaceToWrite_AndIsRefused()
        {
            GeoSphere3 none = default(GeoSphere3);

            Assert.ThrowsAny<ArgumentException>(() => new ObjWriter().Add(none, 0.1));
            Assert.ThrowsAny<ArgumentException>(() => new DxfWriter().Add(none, 0.1));
            Assert.ThrowsAny<ArgumentException>(() => Wkt.Write(none, 0.1));
        }

        [Fact]
        public void ABallWithACentreThatIsNotANumber_IsRefused_AndNothingIsWritten()
        {
            var lost = new GeoSphere3(new GeoPoint3(double.NaN, 0, 0), 5.0);
            var dxf = new DxfWriter();
            string before = dxf.ToString();

            Assert.ThrowsAny<ArgumentException>(() => dxf.Add(lost, 1.0));
            Assert.ThrowsAny<ArgumentException>(() => new ObjWriter().Add(lost, 1.0));
            Assert.Equal(before, dxf.ToString());
        }
    }
}
