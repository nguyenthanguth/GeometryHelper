using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Xml.Linq;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
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
                            ParseExactly(parts[1]),
                            ParseExactly(parts[2]),
                            ParseExactly(parts[3])));
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

        private static readonly Tolerance Fine = new Tolerance(1E-5, 1E-5, 1E-5, 1E-5);

        /// <summary>The volume the faces enclose about the origin, each face fanned from its first corner.</summary>
        private static double SignedVolume(IReadOnlyList<GeoPoint3> vertices, IEnumerable<int[]> faces)
        {
            double volume = 0.0;

            foreach (int[] face in faces)
            {
                GeoVector3 a = GeoPoint3.Origin.GetVectorTo(vertices[face[0] - 1]);

                for (int i = 1; i + 1 < face.Length; i++)
                {
                    GeoVector3 b = GeoPoint3.Origin.GetVectorTo(vertices[face[i] - 1]);
                    GeoVector3 c = GeoPoint3.Origin.GetVectorTo(vertices[face[i + 1] - 1]);
                    volume += a.DotProduct(b.CrossProduct(c)) / 6.0;
                }
            }

            return volume;
        }

        /// <summary>The area the faces cover, each face fanned from its first corner.</summary>
        private static double Covered(IReadOnlyList<GeoPoint3> vertices, IEnumerable<int[]> faces)
        {
            double area = 0.0;

            foreach (int[] face in faces)
            {
                GeoPoint3 a = vertices[face[0] - 1];

                for (int i = 1; i + 1 < face.Length; i++)
                {
                    area += a.GetVectorTo(vertices[face[i] - 1]).CrossProduct(a.GetVectorTo(vertices[face[i + 1] - 1])).Length / 2.0;
                }
            }

            return area;
        }

        /// <summary>The points of a line a chord at a time, closed or open as it was written.</summary>
        private static List<GeoPoint3> Along(string obj)
        {
            var (vertices, _, lines) = Read(obj);
            return Assert.Single(lines).Select(i => vertices[i - 1]).ToList();
        }

        [Fact]
        public void APointIsOnePRecordAtItsPlace()
        {
            string obj = new ObjWriter().Add(new GeoPoint3(1.5, -2, 3E-9), "pin").Add(new GeoPoint3(4, 5, 6)).ToString();
            var (vertices, faces, lines) = Read(obj);

            Assert.Equal(new[] { new GeoPoint3(1.5, -2, 3E-9), new GeoPoint3(4, 5, 6) }, vertices);
            Assert.Empty(faces);
            Assert.Empty(lines);
            Assert.Contains("o pin\nv 1.5 -2 3E-09\np 1\n", obj);
            Assert.Contains("o shape2\nv 4 5 6\np 2\n", obj);
        }

        [Fact]
        public void ATriangleIsOneFaceOnItsOwnCorners()
        {
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(0, 40, 0));
            var (vertices, faces, _) = Read(new ObjWriter().Add(new GeoPoint3(9, 9, 9)).Add(triangle, "t").ToString());

            Assert.Equal(new[] { 2, 3, 4 }, Assert.Single(faces));
            Assert.Equal(triangle.B, vertices[2]);
            Assert.Equal(600.0, Covered(vertices, faces), 9);
        }

        [Fact]
        public void AConvexHexagonInSpaceIsOneFaceOfSix()
        {
            // A regular hexagon of radius 10 on the plane z = y / 2.
            var hexagon = new GeoPolygon3(Enumerable.Range(0, 6).Select(i =>
            {
                double y = 10 * Math.Sin(i * Math.PI / 3);
                return new GeoPoint3(10 * Math.Cos(i * Math.PI / 3), y, y / 2);
            }));

            string obj = new ObjWriter().Add(hexagon, "hexagon").ToString();
            var (vertices, faces, _) = Read(obj);

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Assert.Single(faces));
            Assert.Equal(6, vertices.Count);
            Assert.Equal(hexagon.Area, Covered(vertices, faces), 9);
        }

        [Fact]
        public void AnLShapedPolygonIsFourTrianglesThatSumToItsAreaAndKeepItsWinding()
        {
            var l = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0),
                new GeoPoint3(50, 50, 0), new GeoPoint3(50, 100, 0), new GeoPoint3(0, 100, 0));

            var (vertices, faces, _) = Read(new ObjWriter().Add(l, Tolerance.Default, "l").ToString());

            Assert.Equal(4, faces.Count);
            Assert.All(faces, f => Assert.Equal(3, f.Length));
            Assert.Equal(7500.0, Covered(vertices, faces), 9);

            foreach (int[] f in faces)
            {
                GeoVector3 turn = vertices[f[0] - 1].GetVectorTo(vertices[f[1] - 1]).CrossProduct(vertices[f[0] - 1].GetVectorTo(vertices[f[2] - 1]));
                Assert.True(turn.DotProduct(l.Normal) > 0.0);
            }
        }

        [Fact]
        public void DelegationPin_APolygonWrittenWithoutAToleranceIsWrittenAtTheGlobalOne()
        {
            // A pin on the forwarding, nothing more: it cannot fail while the global tolerance equals the default one, since
            // neither polygon is thin enough to tell two tolerances apart.
            var l = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0),
                new GeoPoint3(50, 50, 0), new GeoPoint3(50, 100, 0), new GeoPoint3(0, 100, 0));
            var hexagon = new GeoPolygon3(Enumerable.Range(0, 6).Select(i => new GeoPoint3(10 * Math.Cos(i * Math.PI / 3), 10 * Math.Sin(i * Math.PI / 3), 0)));

            Assert.Equal(new ObjWriter().Add(l, Tolerance.Global, "l").ToString(), new ObjWriter().Add(l, "l").ToString());
            Assert.Equal(new ObjWriter().Add(hexagon, Tolerance.Global, "h").ToString(), new ObjWriter().Add(hexagon, "h").ToString());
        }

        [Fact]
        public void AThinPolygonThatIsNotConvexIsAnEmptyObjectAtTheGlobalToleranceAndItsTrianglesAtATightOne()
        {
            // By design (X-D3): a polygon that is one face is written as it is, and one that is not is cut into triangles
            // within the tolerance, which at the global one count a sliver this thin as no triangle at all.
            var thinL = new GeoPolygon3(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 0.0004, 0),
                new GeoPoint3(0.0004, 0.0004, 0), new GeoPoint3(0.0004, 100, 0), new GeoPoint3(0, 100, 0),
            }, Fine);
            var thinStrip = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 0.0004, 0), new GeoPoint3(0, 0.0004, 0) }, Fine);

            string atGlobal = new ObjWriter().Add(thinL, "thinL").ToString();
            var (_, globalFaces, _) = Read(atGlobal);
            var (fineVertices, fineFaces, _) = Read(new ObjWriter().Add(thinL, Fine, "thinL").ToString());
            var (_, stripFaces, _) = Read(new ObjWriter().Add(thinStrip, "thinStrip").ToString());

            Assert.Contains("o thinL\n", atGlobal);
            Assert.Empty(globalFaces);
            Assert.Equal(4, fineFaces.Count);
            Assert.Equal(thinL.Area, Covered(fineVertices, fineFaces), 9);
            Assert.Equal(new[] { 1, 2, 3, 4 }, Assert.Single(stripFaces));
        }

        [Fact]
        public void APentagramTurnsOneWayAtEveryCornerButIsNotWrittenAsOneFace()
        {
            // The five points of a pentagon of radius 100 taken every second one: it turns left at each, and round twice.
            var pentagram = new GeoPolygon3(Enumerable.Range(0, 5).Select(k =>
                new GeoPoint3(100 * Math.Cos(4 * Math.PI * k / 5), 100 * Math.Sin(4 * Math.PI * k / 5), 3)));

            var (_, faces, _) = Read(new ObjWriter().Add(pentagram, "star").ToString());

            Assert.DoesNotContain(faces, f => f.Length != 3);
        }

        [Fact]
        public void ARoundedLoopInSpaceIsAClosedLineWithEveryChordWithinTheTolerance()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 7), GeoVector3.XAxis, GeoVector3.YAxis);
            GeoPolygonArc2 flat = new GeoPolygonArc2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100))).Fillet(20);
            var loop = new GeoPolygonArc3(frame, flat);

            List<GeoPoint3> points = Along(new ObjWriter().Add(loop, 0.1, "loop").ToString());

            Assert.True(points.Count > 4 * 8);
            Assert.True(points[0].DistanceTo(points[points.Count - 1]) < 1E-9);
            Assert.All(points, p => Assert.Equal(7.0, p.Z, 9));

            for (int i = 0; i + 1 < points.Count; i++)
            {
                var middle = new GeoPoint2((points[i].X + points[i + 1].X) / 2.0, (points[i].Y + points[i + 1].Y) / 2.0);
                Assert.True(flat.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
                Assert.True(flat.GetClosestPointOnBoundary(new GeoPoint2(points[i].X, points[i].Y)).DistanceTo(new GeoPoint2(points[i].X, points[i].Y)) < 1E-6);
            }
        }

        [Fact]
        public void ACircleOfRadiusOneHundredAtAChordOfATenthIsAClosedLineWithNoSagOverATenth()
        {
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 1), 100);

            List<GeoPoint3> points = Along(new ObjWriter().Add(circle, 0.1, "ring").ToString());

            Assert.True(points[0].DistanceTo(points[points.Count - 1]) < 1E-9);
            Assert.Equal(72, points.Count);                                  // 71 chords of about 8.8
            Assert.All(points, p => Assert.Equal(100.0, p.DistanceTo(circle.Center), 6));
            Assert.All(points, p => Assert.Equal(0.0, circle.Center.GetVectorTo(p).DotProduct(circle.Normal), 6));

            for (int i = 0; i + 1 < points.Count; i++)
            {
                GeoPoint3 middle = points[i].GetMiddlePoint(points[i + 1]);
                Assert.True(circle.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }
        }

        [Fact]
        public void AnArcIsAnOpenLineFromItsStartToItsEnd()
        {
            var arc = new GeoArc3(new GeoPoint3(0, 0, 5), GeoVector3.ZAxis, 100, 0, Math.PI / 2);

            List<GeoPoint3> points = Along(new ObjWriter().Add(arc, 0.1, "arc").ToString());

            Assert.True(points[0].DistanceTo(arc.StartPoint) < 1E-9);
            Assert.True(points[points.Count - 1].DistanceTo(arc.EndPoint) < 1E-9);
            Assert.Equal(19, points.Count);                                  // 18 chords of about 8.7

            for (int i = 0; i + 1 < points.Count; i++)
            {
                GeoPoint3 middle = points[i].GetMiddlePoint(points[i + 1]);
                Assert.True(arc.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }
        }

        [Fact]
        public void AnEllipseThreeHundredByOneHundredIsAClosedLineOnTheEllipseWithNoSagOverATenth()
        {
            var ellipse = new GeoEllipse3(new GeoPoint3(0, 0, 40), GeoVector3.ZAxis, new GeoVector3(1, 1, 0), 300, 100);

            List<GeoPoint3> points = Along(new ObjWriter().Add(ellipse, 0.1, "ellipse").ToString());

            Assert.True(points[0].DistanceTo(points[points.Count - 1]) < 1E-9);
            Assert.All(points, p => Assert.Equal(0.0, ellipse.GetClosestPointOnBoundary(p).DistanceTo(p), 6));

            for (int i = 0; i + 1 < points.Count; i++)
            {
                GeoPoint3 middle = points[i].GetMiddlePoint(points[i + 1]);
                Assert.True(ellipse.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }
        }

        [Fact]
        public void ACurveIsWrittenOnlyAtAChordToleranceThatIsAPositiveNumber()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var arc = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 10, 0, 1);
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 10, 5);
            var loop = new GeoPolygonArc3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0)));

            foreach (double bad in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(circle, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(arc, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(ellipse, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(loop, bad));
            }
        }

        [Fact]
        public void ARayIsALineOfTheLengthGiven()
        {
            var ray = new GeoRay3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 3, 4));

            List<GeoPoint3> points = Along(new ObjWriter().Add(ray, 50, "ray").ToString());

            Assert.Equal(2, points.Count);
            Assert.Equal(ray.Origin, points[0]);
            Assert.True(points[1].DistanceTo(new GeoPoint3(1, 32, 43)) < 1E-9);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(ray, -1));
        }

        [Fact]
        public void ABoxLandsWhereItStandsAndTurnsOutwards()
        {
            var aabb = new GeoAabb3(new GeoPoint3(10, 20, 30), new GeoPoint3(40, 40, 40));
            var turned = new GeoObb3(new GeoCoordinateSystem3(new GeoPoint3(5, 6, 7), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0)), 30, 20, 10);

            var (aVertices, aFaces, _) = Read(new ObjWriter().Add(aabb, "aabb").ToString());
            var (oVertices, oFaces, _) = Read(new ObjWriter().Add(turned, "obb").ToString());

            var aCorners = new List<GeoPoint3>();
            var oCorners = new List<GeoPoint3>();
            GeoCoordinateSystem3 frame = turned.CoordinateSystem;

            foreach (double x in new[] { -1.0, 1.0 })
            {
                foreach (double y in new[] { -1.0, 1.0 })
                {
                    foreach (double z in new[] { -1.0, 1.0 })
                    {
                        aCorners.Add(new GeoPoint3(x < 0 ? 10 : 40, y < 0 ? 20 : 40, z < 0 ? 30 : 40));
                        oCorners.Add(turned.Center
                            .Add(frame.XAxis.Multiply(15 * x)).Add(frame.YAxis.Multiply(10 * y)).Add(frame.ZAxis.Multiply(5 * z)));
                    }
                }
            }

            Assert.Equal(12, aFaces.Count);
            Assert.Equal(12, oFaces.Count);
            AssertSameCorners(aCorners, aVertices);
            AssertSameCorners(oCorners, oVertices);
            AssertTurnsOutwards(aVertices, aFaces, new GeoPoint3(25, 30, 35));
            AssertTurnsOutwards(oVertices, oFaces, turned.Center);
            Assert.Equal(30.0 * 20 * 10, SignedVolume(aVertices, aFaces), 6);
            Assert.Equal(30.0 * 20 * 10, SignedVolume(oVertices, oFaces), 6);
        }

        /// <summary>Every vertex written is one of the corners, and every corner is written.</summary>
        private static void AssertSameCorners(IReadOnlyList<GeoPoint3> corners, IReadOnlyList<GeoPoint3> written)
        {
            Assert.All(written, w => Assert.Contains(corners, c => c.DistanceTo(w) < 1E-9));
            Assert.All(corners, c => Assert.Contains(written, w => c.DistanceTo(w) < 1E-9));
        }

        /// <summary>Every triangle's normal, from the way it is wound, points away from a point inside the body.</summary>
        private static void AssertTurnsOutwards(IReadOnlyList<GeoPoint3> vertices, IEnumerable<int[]> faces, GeoPoint3 inside)
        {
            foreach (int[] f in faces)
            {
                GeoPoint3 a = vertices[f[0] - 1];
                GeoVector3 normal = a.GetVectorTo(vertices[f[1] - 1]).CrossProduct(a.GetVectorTo(vertices[f[2] - 1]));
                Assert.True(normal.DotProduct(inside.GetVectorTo(a)) > 0.0);
            }
        }

        [Fact]
        public void ACellOfAGridIsAnObjectNamedAfterItsIndexesAndStandsWhereItIsTurnedOutwards()
        {
            GeoCellGrid3 grid = Box(0, 0, 0, 100, 50, 50).ToCells(CellOptions3.Grid(50, 50, 50), Tolerance.Default);
            GeoCell3 cell = grid.Cells.Single(c => c.I == 1 && c.J == 0 && c.K == 0);

            string obj = new ObjWriter().Add(cell).ToString();
            var (vertices, faces, _) = Read(obj);

            Assert.Contains("o cell_1_0_0\n", obj);
            Assert.Equal(12, faces.Count);
            AssertSameCorners(new[]
            {
                new GeoPoint3(50, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 50, 0), new GeoPoint3(100, 50, 0),
                new GeoPoint3(50, 0, 50), new GeoPoint3(100, 0, 50), new GeoPoint3(50, 50, 50), new GeoPoint3(100, 50, 50),
            }, vertices);
            AssertTurnsOutwards(vertices, faces, new GeoPoint3(75, 25, 25));
            Assert.Equal(50.0 * 50 * 50, SignedVolume(vertices, faces), 6);
            Assert.Contains("o part_1_0_0\n", new ObjWriter().Add(cell, "part").ToString());
            Assert.Contains("o part_1_0_0\n", new ObjWriter().Add(grid, "part").ToString());
        }

        [Fact]
        public void AFaceWrittenAtAToleranceLeavesItsHoleOpen()
        {
            var plate = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var hole = new GeoPolygon3(new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0));
            var face = new GeoFace3(plate, new[] { hole });

            var (vertices, faces, _) = Read(new ObjWriter().Add(face, Tolerance.Default, "face").ToString());

            Assert.Equal(10000.0 - 400.0, Covered(vertices, faces), 6);
        }

        [Fact]
        public void DelegationPin_AFaceAndABodyWrittenWithoutAToleranceAreWrittenAtTheGlobalOne()
        {
            // A pin on the forwarding, nothing more: the thin slab is the one shape the two tolerances read differently, and
            // the plain call must give what the global tolerance gives, not the tight one.
            var plate = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var face = new GeoFace3(plate, new[] { new GeoPolygon3(new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0)) });
            GeoSolid3 slab = Slab(100, 100, 0.0008, Fine);

            Assert.Equal(new ObjWriter().Add(face, Tolerance.Global, "face").ToString(), new ObjWriter().Add(face, "face").ToString());
            Assert.Equal(new ObjWriter().Add(slab, Tolerance.Global, "slab").ToString(), new ObjWriter().Add(slab, "slab").ToString());
            Assert.NotEqual(new ObjWriter().Add(slab, Fine, "slab").ToString(), new ObjWriter().Add(slab, "slab").ToString());
        }

        [Fact]
        public void ASlabEightHundredthsOfAMillimetreThickIsMeshedWholeAtATightToleranceNotAtTheGlobalOne()
        {
            GeoSolid3 slab = Slab(100, 100, 0.0008, Fine);

            var (vertices, faces, _) = Read(new ObjWriter().Add(slab, Fine, "slab").ToString());
            var (_, globalFaces, _) = Read(new ObjWriter().Add(slab, "slab").ToString());

            Assert.Equal(12, faces.Count);
            Assert.Equal(100.0 * 100 * 0.0008, SignedVolume(vertices, faces), 6);

            // At the global tolerance the four sides, 0.0008 high, are thinner than a triangle may be and are dropped; the
            // 4 triangles of the two big faces are what is left.
            Assert.Equal(4, globalFaces.Count);
        }

        private static GeoSolid3 Slab(double x, double y, double z, Tolerance tolerance)
        {
            GeoPoint3[] c =
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(x, 0, 0), new GeoPoint3(x, y, 0), new GeoPoint3(0, y, 0),
                new GeoPoint3(0, 0, z), new GeoPoint3(x, 0, z), new GeoPoint3(x, y, z), new GeoPoint3(0, y, z),
            };

            GeoFace3 Face(params int[] corners) => new GeoFace3(new GeoPolygon3(corners.Select(i => c[i]), tolerance), null, tolerance);

            return new GeoSolid3(Face(0, 3, 2, 1), Face(4, 5, 6, 7), Face(0, 1, 5, 4), Face(2, 3, 7, 6), Face(1, 2, 6, 5), Face(3, 0, 4, 7));
        }

        /// <summary>One bracketed group of well-known text: either groups of its own, or the points it lists.</summary>
        private sealed class Group
        {
            public List<Group> Groups { get; } = new List<Group>();

            public List<double[]> Points { get; } = new List<double[]>();
        }

        /// <summary>Reads well-known text back into its head and nested groups, parentheses and numbers only.</summary>
        private static (string Head, Group Body) Parse(string wkt)
        {
            int open = wkt.IndexOf('(');
            string head = (open < 0 ? wkt : wkt.Substring(0, open)).Trim();

            if (open < 0)
            {
                return (head, new Group());
            }

            int at = open;
            Group body = ParseGroup(wkt, ref at);

            // A head with a Z carries three numbers to a point and one without it two.
            AssertDimensions(body, head.EndsWith(" Z", StringComparison.Ordinal) ? 3 : 2, head);
            return (head, body);
        }

        private static void AssertDimensions(Group group, int dimensions, string head)
        {
            Assert.All(group.Points, p => Assert.True(p.Length == dimensions, head + " carries a point of " + p.Length + " numbers"));
            Assert.All(group.Groups, g => AssertDimensions(g, dimensions, head));
        }

        private static Group ParseGroup(string text, ref int at)
        {
            var group = new Group();
            at++;

            while (true)
            {
                while (text[at] == ' ')
                {
                    at++;
                }

                if (text[at] == '(')
                {
                    group.Groups.Add(ParseGroup(text, ref at));
                }
                else
                {
                    int start = at;

                    while (text[at] != ',' && text[at] != ')')
                    {
                        at++;
                    }

                    group.Points.Add(text.Substring(start, at - start).Trim().Split(' ').Select(ParseExactly).ToArray());
                }

                while (text[at] == ' ')
                {
                    at++;
                }

                if (text[at++] == ')')
                {
                    return group;
                }
            }
        }

        private static double Turn(List<double[]> ring)
        {
            double twice = 0.0;

            for (int i = 0; i + 1 < ring.Count; i++)
            {
                twice += ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1];
            }

            return twice / 2.0;
        }

        private static void AssertClosed(List<double[]> ring)
        {
            Assert.True(ring.Count >= 4);
            Assert.Equal(ring[0], ring[ring.Count - 1]);
        }

        /// <summary>The area vector of a ring in space, which points along the normal the ring turns counter-clockwise about.</summary>
        private static GeoVector3 AreaVector(List<double[]> ring)
        {
            GeoPoint3 origin = new GeoPoint3(ring[0][0], ring[0][1], ring[0][2]);
            GeoVector3 sum = GeoVector3.Zero;

            for (int i = 1; i + 1 < ring.Count; i++)
            {
                GeoVector3 a = origin.GetVectorTo(new GeoPoint3(ring[i][0], ring[i][1], ring[i][2]));
                GeoVector3 b = origin.GetVectorTo(new GeoPoint3(ring[i + 1][0], ring[i + 1][1], ring[i + 1][2]));
                sum = sum.Add(a.CrossProduct(b).Multiply(0.5));
            }

            return sum;
        }

        [Fact]
        public void ARectangleIsOneClosedCounterClockwiseRing()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(50, 20), 30, 10, Math.PI / 6);

            (string head, Group body) = Parse(Wkt.Write(rectangle));
            List<double[]> ring = Assert.Single(body.Groups).Points;

            Assert.Equal("POLYGON", head);
            AssertClosed(ring);
            Assert.Equal(300.0, Turn(ring), 9);
            Assert.Equal(5, ring.Count);
            Assert.Contains(ring, p => Math.Abs(p[0] - rectangle.GetVertices()[0].X) < 1E-9 && Math.Abs(p[1] - rectangle.GetVertices()[0].Y) < 1E-9);
        }

        [Fact]
        public void ATriangleDrawnClockwiseIsWrittenCounterClockwiseAndOneInSpaceAsItIs()
        {
            var clockwise = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0, 10), new GeoPoint2(10, 0));
            var inSpace = new GeoTriangle3(new GeoPoint3(0, 0, 5), new GeoPoint3(30, 0, 5), new GeoPoint3(0, 40, 5));

            Assert.Equal("POLYGON ((10 0, 0 10, 0 0, 10 0))", Wkt.Write(clockwise));
            Assert.Equal("POLYGON Z ((0 0 5, 30 0 5, 0 40 5, 0 0 5))", Wkt.Write(inSpace));
        }

        [Fact]
        public void AFaceInSpaceIsOneBoundaryCounterClockwiseAndItsHolesClockwiseAboutTheNormal()
        {
            // Tilted: the plane z = y / 2.
            GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, y / 2.0);
            var plate = new GeoPolygon3(P(0, 0), P(100, 0), P(100, 100), P(0, 100));
            var hole = new GeoPolygon3(P(40, 40), P(60, 40), P(60, 60), P(40, 60));
            var face = new GeoFace3(plate, new[] { hole });

            (string head, Group body) = Parse(Wkt.Write(face));

            Assert.Equal("POLYGON Z", head);
            Assert.Equal(2, body.Groups.Count);
            AssertClosed(body.Groups[0].Points);
            AssertClosed(body.Groups[1].Points);
            GeoVector3 boundary = AreaVector(body.Groups[0].Points);
            GeoVector3 inner = AreaVector(body.Groups[1].Points);

            Assert.True(boundary.DotProduct(face.Normal) > 0.0);
            Assert.True(inner.DotProduct(face.Normal) < 0.0);
            Assert.Equal(face.Area, boundary.Length - inner.Length, 6);
        }

        [Fact]
        public void PointsAreOneMultiPointAndSegmentsAndChainsOneMultiLineString()
        {
            (string head, Group points) = Parse(Wkt.Write(new[] { new GeoPoint2(1, 2), new GeoPoint2(3, 4) }));
            (string headZ, Group pointsZ) = Parse(Wkt.Write(new[] { new GeoPoint3(1, 2, 3), new GeoPoint3(4, 5, 6) }));
            (string lines, Group legs) = Parse(Wkt.Write(new[] { new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1)), new GeoLine2(new GeoPoint2(2, 2), new GeoPoint2(3, 5)) }));
            (string chains, Group links) = Parse(Wkt.Write(new[]
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(1, 1, 0)),
                new GeoPolyline3(new GeoPoint3(5, 5, 5), new GeoPoint3(6, 6, 6)),
            }));

            Assert.Equal("MULTIPOINT", head);
            Assert.Equal(new[] { 1.0, 2.0 }, points.Groups[0].Points.Single());
            Assert.Equal(new[] { 3.0, 4.0 }, points.Groups[1].Points.Single());
            Assert.Equal("MULTIPOINT Z", headZ);
            Assert.Equal(new[] { 4.0, 5.0, 6.0 }, pointsZ.Groups[1].Points.Single());
            Assert.Equal("MULTILINESTRING", lines);
            Assert.Equal(2, legs.Groups.Count);
            Assert.Equal(new[] { 3.0, 5.0 }, legs.Groups[1].Points[1]);
            Assert.Equal("MULTILINESTRING Z", chains);
            Assert.Equal(new[] { 3, 2 }, links.Groups.Select(g => g.Points.Count));
            Assert.Equal("MULTIPOINT EMPTY", Wkt.Write(new GeoPoint2[0]));
            Assert.Equal("MULTIPOINT Z EMPTY", Wkt.Write(new GeoPoint3[0]));
            Assert.Equal("MULTILINESTRING EMPTY", Wkt.Write(new GeoLine2[0]));
            Assert.Equal("MULTILINESTRING Z EMPTY", Wkt.Write(new GeoPolyline3[0]));
        }

        [Fact]
        public void SeveralFacesInSpaceAreAMultiPolygonOfEachWithItsHole()
        {
            var plate = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)),
                new[] { new GeoPolygon3(new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0)) });
            var other = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 9), new GeoPoint3(10, 0, 9), new GeoPoint3(0, 10, 9)));

            (string head, Group body) = Parse(Wkt.Write(new[] { plate, other }));

            Assert.Equal("MULTIPOLYGON Z", head);
            Assert.Equal(new[] { 2, 1 }, body.Groups.Select(g => g.Groups.Count));
            Assert.All(body.Groups.SelectMany(g => g.Groups), g => AssertClosed(g.Points));
            Assert.Equal("MULTIPOLYGON Z EMPTY", Wkt.Write(new GeoFace3[0]));
        }

        [Fact]
        public void AMeshOfAGridIsAMultiPolygonOfOneRingPerFaceCoveringItsArea()
        {
            GeoMesh2 mesh = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200))
                .ToMesh(MeshOptions.Grid(100, 100), Tolerance.Default);
            GeoMesh3 flat = new GeoPolygon3(new GeoPoint3(0, 0, 8), new GeoPoint3(200, 0, 8), new GeoPoint3(200, 200, 8), new GeoPoint3(0, 200, 8))
                .ToMesh(MeshOptions.Grid(100, 100));

            (string head, Group body) = Parse(Wkt.Write(mesh));
            (string headZ, Group bodyZ) = Parse(Wkt.Write(flat));

            Assert.Equal("MULTIPOLYGON", head);
            Assert.Equal(mesh.FaceCount, body.Groups.Count);
            Assert.All(body.Groups, g => AssertClosed(Assert.Single(g.Groups).Points));
            Assert.Equal(mesh.Area, body.Groups.Sum(g => Turn(g.Groups[0].Points)), 6);
            Assert.All(body.Groups, g => Assert.True(Turn(g.Groups[0].Points) > 0.0));

            Assert.Equal("MULTIPOLYGON Z", headZ);
            Assert.Equal(flat.FaceCount, bodyZ.Groups.Count);
            Assert.Equal(flat.Area, bodyZ.Groups.Sum(g => AreaVector(g.Groups[0].Points).Length), 6);
            Assert.All(bodyZ.Groups, g => Assert.True(AreaVector(g.Groups[0].Points).DotProduct(flat.Normal) > 0.0));
        }

        [Fact]
        public void ABoxIsAPolyhedralSurfaceOfTwelveTrianglesEnclosingItsVolume()
        {
            GeoSolid3 box = Box(10, 20, 30, 40, 40, 40);

            (string head, Group body) = Parse(Wkt.Write(box));

            Assert.Equal("POLYHEDRALSURFACE Z", head);
            Assert.Equal(12, body.Groups.Count);

            double volume = 0.0;

            foreach (Group triangle in body.Groups)
            {
                List<double[]> ring = Assert.Single(triangle.Groups).Points;
                AssertClosed(ring);
                Assert.Equal(4, ring.Count);
                GeoVector3 a = new GeoVector3(ring[0][0], ring[0][1], ring[0][2]);
                GeoVector3 b = new GeoVector3(ring[1][0], ring[1][1], ring[1][2]);
                GeoVector3 c = new GeoVector3(ring[2][0], ring[2][1], ring[2][2]);
                volume += a.DotProduct(b.CrossProduct(c)) / 6.0;
            }

            Assert.Equal(30.0 * 20 * 10, volume, 6);
        }

        [Fact]
        public void ABodyWithAnOpeningIsAPolyhedralSurfaceOfWhereItsMaterialEnds()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

            (_, Group body) = Parse(Wkt.Write(plate));

            Assert.Equal(plate.TriangulateSurface().Length, body.Groups.Count);
            Assert.True(body.Groups.Count > 12);
            Assert.Equal("POLYHEDRALSURFACE Z EMPTY", Wkt.Write(Box(0, 0, 0, 10, 10, 10).WithOpenings(new[] { Box(-1, -1, -1, 11, 11, 11) })));
        }

        [Fact]
        public void ACircleOfRadiusOneHundredAtAChordOfATenthIsACounterClockwiseRingWithNoSagOverATenth()
        {
            var circle = new GeoCircle2(new GeoPoint2(10, 20), 100);

            (string head, Group body) = Parse(Wkt.Write(circle, 0.1));
            List<double[]> ring = Assert.Single(body.Groups).Points;

            Assert.Equal("POLYGON", head);
            AssertClosed(ring);
            Assert.Equal(72, ring.Count);                                    // 71 chords
            Assert.True(Turn(ring) > 0.0);
            Assert.All(ring, p => Assert.Equal(100.0, Math.Sqrt((p[0] - 10) * (p[0] - 10) + (p[1] - 20) * (p[1] - 20)), 6));

            for (int i = 0; i + 1 < ring.Count; i++)
            {
                double x = (ring[i][0] + ring[i + 1][0]) / 2.0 - 10;
                double y = (ring[i][1] + ring[i + 1][1]) / 2.0 - 20;
                Assert.True(100.0 - Math.Sqrt(x * x + y * y) <= 0.1 + 1E-9);
            }
        }

        [Fact]
        public void AnArcInThePlaneIsAChainFromItsStartToItsEndWhicheverWayItTurns()
        {
            var counter = new GeoArc2(new GeoPoint2(0, 0), 100, 0, Math.PI / 2);
            var clockwise = new GeoArc2(new GeoPoint2(0, 0), 100, Math.PI / 2, 0, true);

            foreach (GeoArc2 arc in new[] { counter, clockwise })
            {
                (string head, Group body) = Parse(Wkt.Write(arc, 0.1));
                List<double[]> chain = body.Points;

                Assert.Equal("LINESTRING", head);
                Assert.Equal(arc.StartPoint.X, chain[0][0], 9);
                Assert.Equal(arc.StartPoint.Y, chain[0][1], 9);
                Assert.Equal(arc.EndPoint.X, chain[chain.Count - 1][0], 9);
                Assert.Equal(arc.EndPoint.Y, chain[chain.Count - 1][1], 9);
                Assert.Equal(19, chain.Count);
            }
        }

        [Fact]
        public void AnEllipseInThePlaneIsACounterClockwiseRingOnTheEllipse()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(5, 6), new GeoVector2(1, 1), 300, 100);

            (string head, Group body) = Parse(Wkt.Write(ellipse, 0.1));
            List<double[]> ring = Assert.Single(body.Groups).Points;

            Assert.Equal("POLYGON", head);
            AssertClosed(ring);
            Assert.True(Turn(ring) > 0.0);
            Assert.All(ring, p => Assert.Equal(0.0, ellipse.GetClosestPointOnBoundary(new GeoPoint2(p[0], p[1])).DistanceTo(new GeoPoint2(p[0], p[1])), 6));

            for (int i = 0; i + 1 < ring.Count; i++)
            {
                var middle = new GeoPoint2((ring[i][0] + ring[i + 1][0]) / 2.0, (ring[i][1] + ring[i + 1][1]) / 2.0);
                Assert.True(ellipse.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }
        }

        [Fact]
        public void ARoundedLoopAndChainInThePlaneAreWrittenThroughTheirChords()
        {
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));
            GeoPolygonArc2 loop = new GeoPolygonArc2(square).Fillet(20);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) }).Fillet(30);

            (string head, Group body) = Parse(Wkt.Write(loop, 0.1));
            List<double[]> ring = Assert.Single(body.Groups).Points;
            (string chainHead, Group chainBody) = Parse(Wkt.Write(chain, 0.1));

            Assert.Equal("POLYGON", head);
            AssertClosed(ring);
            Assert.True(Turn(ring) > 0.0);
            Assert.InRange(Turn(ring), loop.Area - 10.0, loop.Area);        // the chords lie inside the arcs, and lose a sliver of it

            for (int i = 0; i + 1 < ring.Count; i++)
            {
                var middle = new GeoPoint2((ring[i][0] + ring[i + 1][0]) / 2.0, (ring[i][1] + ring[i + 1][1]) / 2.0);
                Assert.True(loop.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }

            Assert.Equal("LINESTRING", chainHead);
            Assert.Equal(0.0, chainBody.Points[0][0], 9);
            Assert.Equal(100.0, chainBody.Points[chainBody.Points.Count - 1][0], 9);
            Assert.Equal(100.0, chainBody.Points[chainBody.Points.Count - 1][1], 9);
            Assert.True(chainBody.Points.Count > 5);
        }

        [Fact]
        public void CurvesInSpaceAreWrittenThroughTheirChordsOnTheirOwnPlane()
        {
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 1), 100);
            var arc = new GeoArc3(new GeoPoint3(0, 0, 5), GeoVector3.ZAxis, 100, 0, Math.PI / 2);
            var ellipse = new GeoEllipse3(new GeoPoint3(0, 0, 40), GeoVector3.ZAxis, new GeoVector3(1, 1, 0), 300, 100);
            var loop = new GeoPolygonArc3(new GeoCoordinateSystem3(new GeoPoint3(0, 0, 7), GeoVector3.XAxis, GeoVector3.YAxis),
                new GeoPolygonArc2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100))).Fillet(20));
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0))).Fillet(30);

            (string circleHead, Group circleBody) = Parse(Wkt.Write(circle, 0.1));
            (string arcHead, Group arcBody) = Parse(Wkt.Write(arc, 0.1));
            (string ellipseHead, Group ellipseBody) = Parse(Wkt.Write(ellipse, 0.1));
            (string loopHead, Group loopBody) = Parse(Wkt.Write(loop, 0.1));
            (string barHead, Group barBody) = Parse(Wkt.Write(bar, 0.1));

            List<double[]> ring = Assert.Single(circleBody.Groups).Points;
            Assert.Equal("POLYGON Z", circleHead);
            AssertClosed(ring);
            Assert.All(ring, p => Assert.Equal(100.0, Math.Sqrt((p[0] - 10) * (p[0] - 10) + (p[1] - 20) * (p[1] - 20) + (p[2] - 30) * (p[2] - 30)), 6));
            Assert.True(AreaVector(ring).DotProduct(circle.Normal) > 0.0);

            Assert.Equal("LINESTRING Z", arcHead);
            Assert.Equal(19, arcBody.Points.Count);
            Assert.Equal(arc.EndPoint.Y, arcBody.Points[arcBody.Points.Count - 1][1], 9);

            Assert.Equal("POLYGON Z", ellipseHead);
            List<double[]> ellipseRing = Assert.Single(ellipseBody.Groups).Points;
            AssertClosed(ellipseRing);
            Assert.All(ellipseRing, p => Assert.Equal(0.0, ellipse.GetClosestPointOnBoundary(new GeoPoint3(p[0], p[1], p[2])).DistanceTo(new GeoPoint3(p[0], p[1], p[2])), 6));

            for (int i = 0; i + 1 < ellipseRing.Count; i++)
            {
                var middle = new GeoPoint3((ellipseRing[i][0] + ellipseRing[i + 1][0]) / 2.0, (ellipseRing[i][1] + ellipseRing[i + 1][1]) / 2.0, (ellipseRing[i][2] + ellipseRing[i + 1][2]) / 2.0);
                Assert.True(ellipse.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }

            Assert.Equal("POLYGON Z", loopHead);
            List<double[]> loopRing = Assert.Single(loopBody.Groups).Points;
            AssertClosed(loopRing);
            Assert.All(loopRing, p => Assert.Equal(7.0, p[2], 9));

            GeoPolygonArc2 flat = new GeoPolygonArc2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100))).Fillet(20);

            for (int i = 0; i + 1 < loopRing.Count; i++)
            {
                var middle = new GeoPoint2((loopRing[i][0] + loopRing[i + 1][0]) / 2.0, (loopRing[i][1] + loopRing[i + 1][1]) / 2.0);
                Assert.True(flat.GetClosestPointOnBoundary(middle).DistanceTo(middle) <= 0.1 + 1E-9);
            }

            Assert.Equal("LINESTRING Z", barHead);
            Assert.Equal(100.0, barBody.Points[barBody.Points.Count - 1][1], 9);
            Assert.True(barBody.Points.Count > 5);
        }

        [Fact]
        public void ACurveIsWrittenAsTextOnlyAtAChordToleranceThatIsAPositiveNumber()
        {
            var circle2 = new GeoCircle2(GeoPoint2.Origin, 10);
            var arc2 = new GeoArc2(GeoPoint2.Origin, 10, 0, 1);
            var ellipse2 = new GeoEllipse2(GeoPoint2.Origin, GeoVector2.XAxis, 10, 5);
            var circle3 = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var arc3 = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 10, 0, 1);
            var ellipse3 = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 10, 5);
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(0, 1));
            var loop2 = new GeoPolygonArc2(square);
            var chain2 = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 0) });
            var loop3 = new GeoPolygonArc3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0)));
            var chain3 = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));

            foreach (double bad in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(circle2, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(arc2, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(ellipse2, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(circle3, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(arc3, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(ellipse3, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(loop2, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(chain2, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(loop3, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(chain3, bad));
            }
        }

        [Fact]
        public void ARayIsASegmentOfTheLengthGiven()
        {
            Assert.Equal("LINESTRING (1 2, 4 6)", Wkt.Write(new GeoRay2(new GeoPoint2(1, 2), new GeoVector2(3, 4)), 5));
            Assert.Equal("LINESTRING Z (1 2 3, 1 32 43)", Wkt.Write(new GeoRay3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 3, 4)), 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(new GeoRay2(GeoPoint2.Origin, GeoVector2.XAxis), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis), double.NaN));
        }

        [Fact]
        public void TheShapesTheWktWriterTakesRefuseNull()
        {
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoMesh2)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoMesh3)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoFace3>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoPoint2>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoPoint3>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoLine2>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoLine3>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoPolyline2>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((IEnumerable<GeoPolyline3>)null));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write(new GeoPolyline3[] { null }));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoPolygonArc2)null, 0.1));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoPolylineArc3)null, 0.1));
        }

        private const double Thousandth = 0.001;

        /// <summary>The points of a WKT chain or ring, whether it is written in the plane or in space.</summary>
        private static List<GeoPoint3> Chain(string wkt)
        {
            (_, Group body) = Parse(wkt);
            List<double[]> points = body.Points.Count > 0 ? body.Points : body.Groups[0].Points;
            return points.Select(p => new GeoPoint3(p[0], p[1], p.Length > 2 ? p[2] : 0.0)).ToList();
        }

        /// <summary>
        /// The most any chord of a chain stands from the curve at its middle, where that is furthest: <paramref name="away"/>
        /// says how far a point is from the curve. Only chords whose two ends lie on it are measured.
        /// </summary>
        private static double WorstSag(IReadOnlyList<GeoPoint3> points, Func<GeoPoint3, double> away)
        {
            double worst = 0.0;
            int measured = 0;

            for (int i = 0; i + 1 < points.Count; i++)
            {
                if (away(points[i]) > 1E-6 || away(points[i + 1]) > 1E-6)
                {
                    continue;
                }

                measured++;
                worst = Math.Max(worst, away(points[i].GetMiddlePoint(points[i + 1])));
            }

            Assert.True(measured > 0);
            return worst;
        }

        /// <summary>
        /// The most any chord of a chain stands inside a circle at its middle, for the chords whose two ends lie on a circle of
        /// the radius about one of the centres.
        /// </summary>
        private static double WorstSag(IReadOnlyList<GeoPoint3> points, double radius, params GeoPoint3[] centres)
        {
            double worst = 0.0;
            int measured = 0;

            for (int i = 0; i + 1 < points.Count; i++)
            {
                foreach (GeoPoint3 centre in centres)
                {
                    if (Math.Abs(points[i].DistanceTo(centre) - radius) < 1E-6 && Math.Abs(points[i + 1].DistanceTo(centre) - radius) < 1E-6)
                    {
                        measured++;
                        worst = Math.Max(worst, radius - points[i].GetMiddlePoint(points[i + 1]).DistanceTo(centre));
                        break;
                    }
                }
            }

            Assert.True(measured > 0);
            return worst;
        }

        [Fact]
        public void ACircleOfRadiusFiftyThousandAtAThousandthIsWithinTheToleranceAsTheTessellationCapWouldNotHaveLeftIt()
        {
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 1), 50000);
            var flat = new GeoCircle2(new GeoPoint2(10, 20), 50000);
            List<GeoPoint3> obj = Along(new ObjWriter().Add(circle, Thousandth, "c").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(circle, Thousandth));
            List<GeoPoint3> wkt2 = Chain(Wkt.Write(flat, Thousandth));

            // 15 708 chords of about 20: the library's own cut used to stop at 4 096 a turn and stray 0.0147 at 0.01 here.
            Assert.Equal(15709, obj.Count);
            Assert.Equal(15709, wkt.Count);
            Assert.Equal(15709, wkt2.Count);
            Assert.True(WorstSag(obj, 50000, circle.Center) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt, 50000, circle.Center) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt2, 50000, new GeoPoint3(10, 20, 0)) <= Thousandth + 1E-9);
            Assert.True(WorstSag(obj, 50000, circle.Center) > 0.9 * Thousandth);
        }

        [Fact]
        public void AQuarterArcOfRadiusFiftyThousandAtAThousandthIsWithinTheTolerance()
        {
            var arc = new GeoArc3(new GeoPoint3(0, 0, 5), GeoVector3.ZAxis, 50000, 0, Math.PI / 2);
            var flat = new GeoArc2(new GeoPoint2(0, 0), 50000, 0, Math.PI / 2);

            List<GeoPoint3> obj = Along(new ObjWriter().Add(arc, Thousandth, "a").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(arc, Thousandth));
            List<GeoPoint3> wkt2 = Chain(Wkt.Write(flat, Thousandth));

            Assert.Equal(3928, obj.Count);
            Assert.Equal(3928, wkt.Count);
            Assert.Equal(3928, wkt2.Count);
            Assert.True(WorstSag(obj, 50000, arc.Center) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt, 50000, arc.Center) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt2, 50000, GeoPoint3.Origin) <= Thousandth + 1E-9);
            Assert.True(obj[0].DistanceTo(arc.StartPoint) < 1E-9);
            Assert.True(obj[obj.Count - 1].DistanceTo(arc.EndPoint) < 1E-9);
        }

        [Fact]
        public void AnEllipseOfRadiiFiftyThousandAndTwentyFiveThousandAtAThousandthIsWithinTheTolerance()
        {
            var ellipse = new GeoEllipse3(new GeoPoint3(0, 0, 40), GeoVector3.ZAxis, new GeoVector3(1, 1, 0), 50000, 25000);
            var flat = new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 1), 50000, 25000);

            List<GeoPoint3> obj = Along(new ObjWriter().Add(ellipse, Thousandth, "e").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(ellipse, Thousandth));
            List<GeoPoint3> wkt2 = Chain(Wkt.Write(flat, Thousandth));

            // Past the 4 096 at which the library's cut used to stop, an even spread in the angle: 2 pi over the root of 8 t / a, 15 708 chords.
            Assert.Equal(15709, obj.Count);
            Assert.Equal(15709, wkt.Count);
            Assert.Equal(15709, wkt2.Count);
            Assert.True(WorstSag(obj, p => ellipse.GetClosestPointOnBoundary(p).DistanceTo(p)) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt, p => ellipse.GetClosestPointOnBoundary(p).DistanceTo(p)) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wkt2, p =>
            {
                var q = new GeoPoint2(p.X, p.Y);
                return flat.GetClosestPointOnBoundary(q).DistanceTo(q);
            }) <= Thousandth + 1E-9);
        }

        [Fact]
        public void ABendOfRadiusFiftyThousandInABarAndInALoopIsWithinTheTolerance()
        {
            var bend = new GeoPoint3(50000, 50000, 0);
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100000, 0, 0), new GeoPoint3(100000, 100000, 0))).Fillet(50000);
            var flatBar = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100000, 0), new GeoPoint2(100000, 100000) }).Fillet(50000);
            GeoPolygonArc2 flatLoop = new GeoPolygonArc2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(300000, 0), new GeoPoint2(300000, 200000), new GeoPoint2(0, 200000))).Fillet(50000);
            var loop = new GeoPolygonArc3(new GeoCoordinateSystem3(new GeoPoint3(0, 0, 7), GeoVector3.XAxis, GeoVector3.YAxis), flatLoop);

            List<GeoPoint3> wktBar = Chain(Wkt.Write(bar, Thousandth));
            List<GeoPoint3> wktFlatBar = Chain(Wkt.Write(flatBar, Thousandth));
            List<GeoPoint3> wktLoop = Chain(Wkt.Write(loop, Thousandth));
            List<GeoPoint3> wktFlatLoop = Chain(Wkt.Write(flatLoop, Thousandth));
            List<GeoPoint3> objLoop = Along(new ObjWriter().Add(loop, Thousandth, "loop").ToString());

            Assert.Equal(3928, wktBar.Count(p => Math.Abs(p.DistanceTo(bend) - 50000) < 1E-6));
            Assert.True(WorstSag(wktBar, 50000, bend) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wktFlatBar, 50000, bend) <= Thousandth + 1E-9);

            // The four corners of the loop, the centres of their arcs 50 000 in from them; the loop stands at z = 7.
            Assert.True(WorstSag(wktFlatLoop, 50000, Corners(0)) <= Thousandth + 1E-9);
            Assert.True(WorstSag(wktLoop, 50000, Corners(7)) <= Thousandth + 1E-9);
            Assert.True(WorstSag(objLoop, 50000, Corners(7)) <= Thousandth + 1E-9);
            Assert.True(objLoop[0].DistanceTo(objLoop[objLoop.Count - 1]) < 1E-9);
            Assert.True(wktLoop[0].DistanceTo(wktLoop[wktLoop.Count - 1]) < 1E-9);
        }

        private static GeoPoint3[] Corners(double z)
            => new[] { new GeoPoint3(50000, 50000, z), new GeoPoint3(250000, 50000, z), new GeoPoint3(250000, 150000, z), new GeoPoint3(50000, 150000, z) };

        [Fact]
        public void WhereTheTessellationAlreadyMeetsTheToleranceTheCurvesAreCutAsItCutsThem()
        {
            foreach ((double radius, double tolerance) in new[] { (100.0, 0.1), (3.0, 0.001), (2500.0, 0.01), (0.5, 0.2) })
            {
                var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1), radius);
                var arc = new GeoArc3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1), radius, 0.3, 2.9);
                var ellipse = new GeoEllipse3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1), new GeoVector3(1, 0, 0), 3 * radius, radius);

                Assert.Equal(circle.ToPolylineByChordTolerance(tolerance).Vertices, Along(new ObjWriter().Add(circle, tolerance).ToString()));
                Assert.Equal(arc.ToPolylineByChordTolerance(tolerance).Vertices, Along(new ObjWriter().Add(arc, tolerance).ToString()));
                Assert.Equal(ellipse.ToPolylineByChordTolerance(tolerance).Vertices, Along(new ObjWriter().Add(ellipse, tolerance).ToString()));
                Assert.Equal(Wkt.Write(circle.ToPolygonByChordTolerance(tolerance)), Wkt.Write(circle, tolerance));
                Assert.Equal(Wkt.Write(arc.ToPolylineByChordTolerance(tolerance)), Wkt.Write(arc, tolerance));
                Assert.Equal(Wkt.Write(ellipse.ToPolygonByChordTolerance(tolerance)), Wkt.Write(ellipse, tolerance));
            }
        }

        [Fact]
        public void ACurveThatWouldNeedMoreThanAMillionChordsIsRefusedInBothWriters()
        {
            const double tiny = 1E-12;
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 50000);
            var arc = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 50000, 0, 1);
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 50000, 25000);
            var circle2 = new GeoCircle2(GeoPoint2.Origin, 50000);
            var arc2 = new GeoArc2(GeoPoint2.Origin, 50000, 0, 1);
            var ellipse2 = new GeoEllipse2(GeoPoint2.Origin, GeoVector2.XAxis, 50000, 25000);
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100000, 0, 0), new GeoPoint3(100000, 100000, 0))).Fillet(50000);
            var bar2 = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100000, 0), new GeoPoint2(100000, 100000) }).Fillet(50000);
            var loop2 = new GeoPolygonArc2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100000, 0), new GeoPoint2(100000, 100000), new GeoPoint2(0, 100000))).Fillet(30000);
            var loop3 = new GeoPolygonArc3(new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), loop2);

            var thrown = new List<ArgumentOutOfRangeException>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(circle, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(arc, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(ellipse, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(loop3, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(circle, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(arc, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(ellipse, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(circle2, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(arc2, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(ellipse2, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(bar, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(bar2, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(loop2, tiny)),
                Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(loop3, tiny)),
            };

            Assert.All(thrown, e => Assert.Contains("1048576", e.Message));
            Assert.All(thrown, e => Assert.Equal("chordTolerance", e.ParamName));
        }

        [Fact]
        public void TheBentChainWriterThatWasThereBeforeIsWithinTheToleranceOnALargeRadiusToo()
        {
            var bend = new GeoPoint3(50000, 50000, 0);
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100000, 0, 0), new GeoPoint3(100000, 100000, 0))).Fillet(50000);

            List<GeoPoint3> points = Along(new ObjWriter().Add(bar, Thousandth, "bar").ToString());

            // The library's own cut used to stop at 4 096 chords a turn, which gave 1 025 points on the bend and strayed 0.0147.
            Assert.Equal(3928, points.Count(p => Math.Abs(p.DistanceTo(bend) - 50000) < 1E-6));
            Assert.True(WorstSag(points, 50000, bend) <= Thousandth + 1E-9);
            Assert.True(points[0].DistanceTo(new GeoPoint3(0, 0, 0)) < 1E-9);
            Assert.True(points[points.Count - 1].DistanceTo(new GeoPoint3(100000, 100000, 0)) < 1E-9);
        }

        [Fact]
        public void TheBentChainWriterThatWasThereBeforeCutsAsTheLibraryCutsWhereTheLibraryMeetsTheTolerance()
        {
            foreach ((double radius, double tolerance) in new[] { (30.0, 0.1), (3.0, 0.001), (2500.0, 0.01), (0.5, 0.2) })
            {
                double leg = 4 * radius;
                var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(leg, 0, 0), new GeoPoint3(leg, leg, 2), new GeoPoint3(0, leg, 2))).Fillet(radius);

                List<GeoPoint3> points = Along(new ObjWriter().Add(bar, tolerance).ToString());

                // The library's points, bit for bit, but the end of each arc: that is the edge's own end point here, which the
                // library's last chord, worked out from the circle's angle, may miss by a rounding.
                IReadOnlyList<GeoPoint3> library = bar.ToPolyline3(tolerance).Vertices;

                Assert.Equal(library.Count, points.Count);
                Assert.Equal(2, bar.EdgeCount - bar.GetEdges().Count(e => !e.IsArc));

                int differing = 0;

                for (int i = 0; i < points.Count; i++)
                {
                    if (!library[i].Equals(points[i]))
                    {
                        differing++;
                        Assert.True(library[i].DistanceTo(points[i]) < 1E-9 * (1 + radius));
                    }
                }

                Assert.True(differing <= 2);
                Assert.Equal(bar.StartPoint, points[0]);
                Assert.Equal(bar.EndPoint, points[points.Count - 1]);
            }
        }

        [Fact]
        public void TheBentChainWriterThatWasThereBeforeStillTakesAnyToleranceAboveNoughtInfinityIncluded()
        {
            // Documented, not endorsed: an old member keeps what it accepted. Infinity cuts the bend into one chord.
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0))).Fillet(30);

            Assert.Equal(4, Along(new ObjWriter().Add(bar, double.PositiveInfinity, "bar").ToString()).Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(bar, 0.0));
        }

        [Fact]
        public void ChordsShorterThanThePointToleranceAreKeptAndAClosedCurveClosesOnItsOwnFirstPoint()
        {
            // A circle of radius 1 within 1E-8: 22 215 chords of 2.8E-4, each shorter than the 1E-3 the library's chains drop.
            const double tight = 1E-8;
            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 1);

            List<GeoPoint3> obj = Along(new ObjWriter().Add(circle, tight, "c").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(circle, tight));

            foreach (List<GeoPoint3> points in new[] { obj, wkt })
            {
                Assert.Equal(22216, points.Count);
                Assert.Equal(points[0], points[points.Count - 1]);
                Assert.True(WorstSag(points, 1, circle.Center) <= tight + 1E-12);
            }

            (_, Group body) = Parse(Wkt.Write(new GeoCircle2(new GeoPoint2(1, 2), 1), tight));
            List<double[]> ring = Assert.Single(body.Groups).Points;

            Assert.Equal(22216, ring.Count);
            AssertClosed(ring);
            Assert.True(Turn(ring) > 0.0);
        }

        [Fact]
        public void AnArcOfRadiusTenWithinAHundredMillionthEndsExactlyWhereItEnds()
        {
            var arc = new GeoArc3(new GeoPoint3(4, 5, 6), new GeoVector3(0, 1, 1), 10, 0.3, 2.9);
            var flat = new GeoArc2(new GeoPoint2(4, 5), 10, 0.3, 2.9);

            List<GeoPoint3> obj = Along(new ObjWriter().Add(arc, 1E-8, "a").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(arc, 1E-8));
            List<GeoPoint3> wkt2 = Chain(Wkt.Write(flat, 1E-8));

            foreach (List<GeoPoint3> points in new[] { obj, wkt })
            {
                Assert.Equal(arc.StartPoint, points[0]);
                Assert.Equal(arc.EndPoint, points[points.Count - 1]);
                Assert.True(WorstSag(points, 10, arc.Center) <= 1E-8 + 1E-12);
            }

            Assert.Equal(flat.StartPoint.X, wkt2[0].X);
            Assert.Equal(flat.EndPoint.Y, wkt2[wkt2.Count - 1].Y);
            Assert.True(WorstSag(wkt2, 10, new GeoPoint3(4, 5, 0)) <= 1E-8 + 1E-12);
        }

        [Fact]
        public void AChainWhoseSemicircleIsSmallerThanThePointToleranceIsWrittenWithEveryChord()
        {
            // The arc from (0, 0) to (0.002, 0) has radius 0.001, and at 1E-4 chords of about 0.0009, which the library drops.
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(0.002, 0), new GeoPoint2(5, 0) }, new[] { 1.0, 0.0 });
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(0.002, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5), new GeoPoint2(0, 5) }, new[] { 1.0, 0.0, 0.0, 0.0, 0.0 });

            List<GeoPoint3> wkt = Chain(Wkt.Write(chain, 1E-4));
            (string head, Group body) = Parse(Wkt.Write(loop, 1E-4));
            List<double[]> ring = Assert.Single(body.Groups).Points;

            Assert.Equal("LINESTRING", Parse(Wkt.Write(chain, 1E-4)).Head);
            Assert.Equal(6, wkt.Count);
            Assert.Equal(0.0, wkt[0].X);
            Assert.Equal(5.0, wkt[wkt.Count - 1].X);
            Assert.True(WorstSag(wkt, 0.001, new GeoPoint3(0.001, 0, 0)) <= 1E-4 + 1E-12);

            Assert.Equal("POLYGON", head);
            AssertClosed(ring);
            Assert.Equal(9, ring.Count);
        }

        [Fact]
        public void ABendOfRadiusOneWithinAHundredMillionthIsCutIntoEveryChordInBothWriters()
        {
            // 1 757 chords of 8.9E-4 to the quarter turn, each shorter than the point tolerance.
            var bend = new GeoPoint3(10, 1, 0);
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(11, 0, 0), new GeoPoint3(11, 10, 0))).Fillet(1);

            List<GeoPoint3> obj = Along(new ObjWriter().Add(bar, 1E-7, "bar").ToString());
            List<GeoPoint3> wkt = Chain(Wkt.Write(bar, 1E-7));

            foreach (List<GeoPoint3> points in new[] { obj, wkt })
            {
                Assert.Equal(1758, points.Count(p => Math.Abs(p.DistanceTo(bend) - 1) < 1E-9));
                Assert.True(WorstSag(points, 1, bend) <= 1E-7 + 1E-12);
                Assert.Equal(bar.StartPoint, points[0]);
                Assert.Equal(bar.EndPoint, points[points.Count - 1]);
            }
        }

        [Fact]
        public void TheChordsOfAllTheBendsOfAChainTogetherAreHeldToAMillion()
        {
            // Each of the two bends of radius 50 000 is 600 270 chords at 4.28E-8, under the 1 048 576; both are 1 200 540.
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(150000, 0, 0), new GeoPoint3(150000, 200000, 0), new GeoPoint3(300000, 200000, 0))).Fillet(50000);
            var single = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100000, 0, 0), new GeoPoint3(100000, 100000, 0))).Fillet(50000);
            var flat = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(150000, 0), new GeoPoint2(150000, 200000), new GeoPoint2(300000, 200000) }).Fillet(50000);
            const double tolerance = 4.28E-8;

            Assert.NotEmpty(Wkt.Write(single, tolerance));
            Assert.Contains("1048576", Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(bar, tolerance)).Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(flat, tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(bar, tolerance));
        }

        [Fact]
        public void ATolerancePastWhatIsFiniteIsRefusedInTheWordsOfTheRuleAndTheOldChainWriterStillTakesInfinity()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var bar = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0))).Fillet(30);

            Assert.Contains("finite number above nought", Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(circle, double.PositiveInfinity)).Message);
            Assert.Contains("finite number above nought", Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(circle, double.PositiveInfinity)).Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(bar, double.PositiveInfinity));
            Assert.Equal(4, Along(new ObjWriter().Add(bar, double.PositiveInfinity).ToString()).Count);
        }

        [Fact]
        public void ASquareWithANeedleRunningOutAndBackIsNotOneFace()
        {
            // The needle goes up from (5, 0) to (5, 5) and back to a point a hundred-billionth to its right: its tip turns
            // round on itself, so the polygon turns left at every other corner and goes once round, and is still not convex.
            var needled = new GeoPolygon3(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(5, 5, 0), new GeoPoint3(5 + 1E-11, 0, 0),
                new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0),
            }, Fine);

            var (_, faces, _) = Read(new ObjWriter().Add(needled, Fine, "needle").ToString());

            Assert.NotEmpty(faces);
            Assert.DoesNotContain(faces, f => f.Length != 3);
        }

        /// <summary>
        /// Reads a number the way a correctly rounding parser does: .NET Framework's parser can land a step away, so the double
        /// it gives is moved to a neighbour until the text reads back to it.
        /// </summary>
        internal static double ParseExactly(string text)
        {
            double value = double.Parse(text, CultureInfo.InvariantCulture);

            if (value == 0.0 || ReadsBackTo(text, value))
            {
                return value;
            }

            foreach (long step in new[] { 1L, -1L })
            {
                double neighbour = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + (value < 0.0 ? -step : step));

                if (ReadsBackTo(text, neighbour))
                {
                    return neighbour;
                }
            }

            return value;
        }

        /// <summary>The text a writer gives a double, as the first number of a WKT point.</summary>
        private static string Printed(double value) => Wkt.Write(new GeoPoint2(value, 0)).Substring("POINT (".Length).Split(' ')[0];

        [Fact]
        public void TheWritersPrintTheseNumbersAsTextsThatReadBackToThemThoseTheFrameworkFormatterGetsWrongIncluded()
        {
            // Texts that read back to the numbers, and are also what .NET 10 prints for "R". .NET Framework's "R" gives
            // 4.87215401133157, 3.38241497910694 and -2.16613335402968E-08 for the first three, which are other doubles.
            var expected = new (double Value, string Text)[]
            {
                (4.8721540113315704, "4.8721540113315704"),
                (3.3824149791069402, "3.3824149791069402"),
                (-2.1661333540296802E-08, "-2.1661333540296802E-08"),
                (1894.4963821650001, "1894.496382165"),
                (0.84551240822557006, "0.8455124082255701"),
                (0.1, "0.1"),
                (123456789012345.6, "123456789012345.6"),
                (1E-05, "1E-05"),
                (5E-324, "5E-324"),
                (2.2250738585072014E-308, "2.2250738585072014E-308"),
                (1.7976931348623157E+308, "1.7976931348623157E+308"),
            };

            foreach ((double value, string text) in expected)
            {
                Assert.Equal(text, Printed(value));
                Assert.Equal(text, new ObjWriter().Add(new GeoPoint3(value, 0, 0)).ToString().Split('\n')[2].Split(' ')[1]);
            }
        }

        [Fact]
        public void EveryDoubleThatIsPrintedReadsBackToItselfUnderTheRoundingRuleAndNoOtherDoubleIsNearer()
        {
            var numbers = new List<double> { 4.8721540113315704, 3.3824149791069402, -2.1661333540296802E-08, 1894.4963821650001, 0.84551240822557006 };

            // Hand cases: either side of the powers of two, where the step below is half the step above, the smallest
            // normal number, the subnormals, and the largest.
            foreach (int power in new[] { -1074, -1073, -1030, -1022, -1000, -52, -10, -1, 0, 1, 10, 52, 53, 100, 1000, 1023 })
            {
                double twoToThePower = Math.Pow(2, power);
                long bits = BitConverter.DoubleToInt64Bits(twoToThePower);

                numbers.Add(twoToThePower);
                numbers.Add(BitConverter.Int64BitsToDouble(bits - 1));
                numbers.Add(BitConverter.Int64BitsToDouble(bits + 1));
                numbers.Add(-twoToThePower);
            }

            numbers.Add(double.MaxValue);
            numbers.Add(double.Epsilon);

            var random = new Random(20261018);

            for (int i = 0; i < 5000; i++)
            {
                numbers.Add((random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-9, 9)));
                double bits = BitConverter.Int64BitsToDouble((long)random.Next() << 32 | (uint)random.Next() | (long)random.Next(2) << 63);
                numbers.Add(double.IsNaN(bits) || double.IsInfinity(bits) || bits == 0.0 ? 1.5 : bits);
            }

            foreach (double number in numbers.Where(n => n != 0.0))
            {
                string text = Printed(number);

                Assert.True(ReadsBackTo(text, number), text + " does not read back to " + number.ToString("R", CultureInfo.InvariantCulture));
                Assert.True(text.Length <= 24);
            }
        }

        /// <summary>
        /// Whether the decimal text, taken exactly, lies nearer to the double than to either double beside it, a tie going to
        /// the one whose last bit is even: whether a correctly rounding reader gets the double back. Worked out in integers.
        /// </summary>
        private static bool ReadsBackTo(string text, double value)
        {
            bool negative = text.StartsWith("-", StringComparison.Ordinal);
            string unsigned = negative ? text.Substring(1) : text;
            int e = unsigned.IndexOfAny(new[] { 'E', 'e' });
            int exponent = e < 0 ? 0 : int.Parse(unsigned.Substring(e + 1), CultureInfo.InvariantCulture);
            string mantissa = e < 0 ? unsigned : unsigned.Substring(0, e);
            int point = mantissa.IndexOf('.');
            int fraction = point < 0 ? 0 : mantissa.Length - point - 1;
            BigInteger digits = BigInteger.Parse(mantissa.Replace(".", string.Empty), CultureInfo.InvariantCulture);
            int ten = exponent - fraction;
            BigInteger textNumerator = ten >= 0 ? digits * BigInteger.Pow(10, ten) : digits;
            BigInteger textDenominator = ten >= 0 ? BigInteger.One : BigInteger.Pow(10, -ten);

            if (negative != (value < 0.0))
            {
                return false;
            }

            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
            int distanceToPrevious = Compare(textNumerator, textDenominator, bits, bits - 1);
            int distanceToNext = bits == BitConverter.DoubleToInt64Bits(double.MaxValue) ? -1 : Compare(textNumerator, textDenominator, bits, bits + 1);
            bool even = (bits & 1) == 0;

            return (distanceToPrevious < 0 || (distanceToPrevious == 0 && even)) && (distanceToNext < 0 || (distanceToNext == 0 && even));
        }

        /// <summary>Compares the distance of a text from the double of one set of bits with its distance from another's.</summary>
        private static int Compare(BigInteger numerator, BigInteger denominator, long nearBits, long farBits)
        {
            (BigInteger nearNumerator, BigInteger nearDenominator) = Exactly(nearBits);
            (BigInteger farNumerator, BigInteger farDenominator) = Exactly(farBits);

            BigInteger near = BigInteger.Abs(numerator * nearDenominator - nearNumerator * denominator) * farDenominator;
            BigInteger far = BigInteger.Abs(numerator * farDenominator - farNumerator * denominator) * nearDenominator;

            return near.CompareTo(far);
        }

        /// <summary>The value of a positive double as a whole numerator over a power-of-two denominator.</summary>
        private static (BigInteger Numerator, BigInteger Denominator) Exactly(long bits)
        {
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            int biased = (int)(bits >> 52);
            BigInteger mantissa = biased == 0 ? fraction : fraction | (1L << 52);
            int two = biased == 0 ? -1074 : biased - 1075;

            return two >= 0 ? (mantissa << two, BigInteger.One) : (mantissa, BigInteger.One << -two);
        }
        [Fact]
        public void ARayWithNoDirectionIsRefusedByEveryWriterThatTakesOne()
        {
            Assert.Throws<ArgumentException>(() => new ObjWriter().Add(default(GeoRay3), 10));
            Assert.Throws<ArgumentException>(() => Wkt.Write(default(GeoRay2), 10));
            Assert.Throws<ArgumentException>(() => Wkt.Write(default(GeoRay3), 10));
        }

        [Fact]
        public void AnArcTooShallowForItsCircleToBePlacedIsReadAsItsChordAndTheCornerStaysWhereItIs()
        {
            // The bulge of the first edge is a hair off nought: its circle has a radius of 1E17 and more, a centre far away, or
            // none that is a number. The corner (100, 0, 0) is where the chain turns, and no chord may miss it.
            GeoVector3 up = GeoVector3.ZAxis;
            var corner = new GeoPoint3(100, 0, 0);

            foreach (double bulge in new[] { 1E-16, 1E-12, 1E-200, 1E-310 })
            {
                var chain3 = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), corner, new GeoPoint3(100, 100, 0) }, new[] { bulge, 0.0 }, new[] { up, up });
                var loop3 = new GeoPolygonArc3(
                    new[] { new GeoPoint3(0, 0, 0), corner, new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0) }, new[] { bulge, 0.0, 0.0, 0.0 }, new[] { up, up, up, up });
                var chain2 = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) }, new[] { bulge, 0.0 });
                var loop2 = new GeoPolygonArc2(
                    new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) }, new[] { bulge, 0.0, 0.0, 0.0 });

                var chains = new List<List<GeoPoint3>>
                {
                    Along(new ObjWriter().Add(chain3, 0.1, "chain").ToString()),
                    Chain(Wkt.Write(chain3, 0.1)),
                    Chain(Wkt.Write(chain2, 0.1)),
                };
                var loops = new List<List<GeoPoint3>>
                {
                    Along(new ObjWriter().Add(loop3, 0.1, "loop").ToString()),
                    Chain(Wkt.Write(loop3, 0.1)),
                    Chain(Wkt.Write(loop2, 0.1)),
                };

                foreach (List<GeoPoint3> points in chains.Concat(loops))
                {
                    Assert.All(points, p => Assert.False(double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsNaN(p.Z), "NaN at bulge " + bulge));
                    Assert.Contains(corner, points);
                }

                Assert.All(chains, points => Assert.Equal(3, points.Count));
                Assert.All(loops, points => Assert.Equal(5, points.Count));
                Assert.All(loops, points => Assert.Equal(points[0], points[points.Count - 1]));
            }
        }

        [Fact]
        public void ANegativeNoughtIsWrittenAsMinusNoughtOnEitherRuntime()
        {
            // Exact keeps the sign bit, so -0 is -0 on .NET Framework, which prints 0, and on .NET 10 alike.
            Assert.Equal("POINT (-0 0)", Wkt.Write(new GeoPoint2(-0.0, 0.0)));
            Assert.Contains("v -0 0 -0\n", new ObjWriter().Add(new GeoPoint3(-0.0, 0.0, -0.0)).ToString());
        }

        [Fact]
        public void TheShapesTheObjWriterTakesRefuseNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoPolygon3)null, Tolerance.Default));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoSolid3)null, Tolerance.Default));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoFace3)null, Tolerance.Default));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoPolygonArc3)null, 0.1));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoObb3)null));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoCell3)null));
        }
    }
}
