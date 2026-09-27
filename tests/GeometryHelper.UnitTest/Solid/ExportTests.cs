using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Writing geometry out to look at: OBJ for space, SVG for the plane, WKT for both.
    /// </summary>
    public class ExportTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>Reads the vertices and triangles of an OBJ file back, to measure what was written.</summary>
        private static (List<GeoPoint3> Vertices, List<int[]> Faces, List<int[]> Lines) Read(string obj)
        {
            var vertices = new List<GeoPoint3>();
            var faces = new List<int[]>();
            var lines = new List<int[]>();

            foreach (string row in obj.Split('\n'))
            {
                string[] parts = row.Split(' ');

                switch (parts[0])
                {
                    case "v":
                        vertices.Add(new GeoPoint3(
                            double.Parse(parts[1], CultureInfo.InvariantCulture),
                            double.Parse(parts[2], CultureInfo.InvariantCulture),
                            double.Parse(parts[3], CultureInfo.InvariantCulture)));
                        break;
                    case "f":
                        faces.Add(parts.Skip(1).Select(int.Parse).ToArray());
                        break;
                    case "l":
                        lines.Add(parts.Skip(1).Select(int.Parse).ToArray());
                        break;
                }
            }

            return (vertices, faces, lines);
        }

        [Fact]
        public void ABodyWrittenAsObjReadsBackToTheSameVolume()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });
            string obj = new ObjWriter().Add(plate, "plate").ToString();
            var (vertices, faces, _) = Read(obj);

            Assert.Contains("o plate", obj);
            Assert.All(faces, f => Assert.All(f, i => Assert.InRange(i, 1, vertices.Count)));

            double volume = faces.Sum(f =>
            {
                GeoVector3 a = GeoPoint3.Origin.GetVectorTo(vertices[f[0] - 1]);
                GeoVector3 b = GeoPoint3.Origin.GetVectorTo(vertices[f[1] - 1]);
                GeoVector3 c = GeoPoint3.Origin.GetVectorTo(vertices[f[2] - 1]);
                return a.DotProduct(b.CrossProduct(c)) / 6.0;
            });

            Assert.Equal(100.0 * 100 * 20 - 20.0 * 20 * 20, Math.Abs(volume), 6);
        }

        [Fact]
        public void EachObjectNumbersItsVerticesAfterTheLast()
        {
            var bar = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0));
            string obj = new ObjWriter().Add(Box(0, 0, 0, 1, 1, 1)).Add(bar, "bar").ToString();
            var (vertices, faces, lines) = Read(obj);

            Assert.Equal(12, faces.Count);
            Assert.Equal(new[] { 37, 38, 39 }, Assert.Single(lines));
            Assert.Equal(39, vertices.Count);
            Assert.Contains("o shape1", obj);
            Assert.Contains("o bar", obj);
        }

        [Fact]
        public void NumbersAreWrittenExactlyWhateverTheCulture()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var far = new GeoPoint3(123456.78901234567, -0.1, 3E-9);

                var (vertices, _, _) = Read(new ObjWriter().Add(new GeoLine3(far, GeoPoint3.Origin)).ToString());

                Assert.Equal(far, vertices[0]);
                Assert.Equal("POINT Z (123456.78901234567 -0.1 3E-09)", Wkt.Write(far));
                Assert.Contains("cx=\"0.5\" cy=\"1.5\"", new SvgWriter().Add(new GeoPoint2(0.5, 1.5)).ToString());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        public void WktClosesRingsAndWindsThemTheWayTheFormatAsks()
        {
            // Drawn clockwise, the boundary comes out counter-clockwise; the hole the other way round.
            var clockwise = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(0, 10), new GeoPoint2(10, 10), new GeoPoint2(10, 0));
            var hole = new GeoPolygon2(new GeoPoint2(4, 4), new GeoPoint2(6, 4), new GeoPoint2(6, 6), new GeoPoint2(4, 6));

            Assert.Equal("POINT (1 2)", Wkt.Write(new GeoPoint2(1, 2)));
            Assert.Equal("LINESTRING (0 0, 3 4)", Wkt.Write(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(3, 4))));
            Assert.Equal("POLYGON ((10 0, 10 10, 0 10, 0 0, 10 0))", Wkt.Write(clockwise));
            Assert.Equal("POLYGON ((10 0, 10 10, 0 10, 0 0, 10 0), (4 6, 6 6, 6 4, 4 4, 4 6))", Wkt.Write(new GeoFace2(clockwise, new[] { hole })));
            Assert.Equal("MULTIPOLYGON EMPTY", Wkt.Write(new GeoFace2[0]));
            Assert.StartsWith("MULTIPOLYGON (((", Wkt.Write(clockwise.Union(hole.Translate(new GeoVector2(20, 0)))));
            Assert.Equal("POLYGON Z ((0 0 1, 1 0 1, 0 1 1, 0 0 1))", Wkt.Write(new GeoPolygon3(new GeoPoint3(0, 0, 1), new GeoPoint3(1, 0, 1), new GeoPoint3(0, 1, 1))));
        }

        [Fact]
        public void AnSvgPictureIsWellFormedAndDrawsArcsAsArcs()
        {
            var plate = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 50), new GeoPoint2(0, 50));
            var turn = new GeoArc2(new GeoPoint2(50, 25), 10, 0, Math.PI / 2);
            var face = new GeoFace2(plate, new[] { new GeoPolygon2(new GeoPoint2(10, 10), new GeoPoint2(20, 10), new GeoPoint2(20, 20), new GeoPoint2(10, 20)) });

            string svg = new SvgWriter()
                .Add(face, "black", "#eeeeee")
                .Add(turn, "red")
                .Add(new GeoPolygonArc2(plate).Fillet(5), "blue")
                .Add(new GeoPoint2(50, 25), "green")
                .Add(new GeoCircle2(new GeoPoint2(80, 25), 5), "\"<odd>\"")
                .ToString();

            XDocument picture = XDocument.Parse(svg);
            XNamespace ns = "http://www.w3.org/2000/svg";

            Assert.Equal(ns + "svg", picture.Root.Name);
            Assert.Contains("A 10 10 0 0 1 50 35", svg);                      // a quarter turn counter-clockwise
            Assert.Contains(picture.Descendants(ns + "path"), p => (string)p.Attribute("fill-rule") == "evenodd");
            Assert.Equal(2, picture.Descendants(ns + "circle").Count());
            Assert.Contains("viewBox=\"-5 -55 110 60\"", svg);
        }
    }
}
