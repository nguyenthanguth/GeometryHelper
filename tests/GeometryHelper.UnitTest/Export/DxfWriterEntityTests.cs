using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// Entities <see cref="DxfReader"/> read, written again by <see cref="DxfWriter.Add(DxfEntity)"/> with their layer, colour,
    /// linetype, lineweight and thickness, and read once more; and the bytes the other overloads write, unchanged by it.
    /// </summary>
    public class DxfWriterEntityTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Fact]
        public void TheOverloadsThatWereThere_WriteTheSameBytesAsBefore()
        {
            // The hash of the file of one shape of each overload, as the writer wrote it before Add(DxfEntity) and the
            // linetypes of read entities were added: the digits of a number can differ between the runtimes, so each has its own.
            string text = DxfReaderRoundTripTests.OneOfEach().ToString();
            bool framework = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal);

            using (var sha = SHA256.Create())
            {
                string hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
                Assert.Equal(framework ? "c236b7ceb81ca7e58dbf6bdccb813b9c851ff8a654be24ed3ba1ec6b2f4de983" : "0d617de3e27ca0697a66874130cc53440e40c84a100edea0b9aaaca0b3650c13", hash);
                Assert.Equal(framework ? 39287 : 39255, text.Length);
            }
        }

        /// <summary>A drawing in AutoCAD's style: every entity the reader maps, most with a look of their own.</summary>
        private static string Drawing()
            => File("AC1015", 4, Layers("steel", "cut"), G(
                0, "LINE", 5, "30", 100, "AcDbEntity", 8, "steel", 6, "DASHED", 62, 1, 370, 35, 420, 16744448, 100, "AcDbLine", 39, "50", 10, "0.1", 20, "0.2", 30, "0.3", 11, "100.7", 21, "4.8721540113315704", 31, "0", 1001, "ACAD", 1000, "a note",
                0, "POINT", 5, "31", 100, "AcDbEntity", 8, "cut", 62, 3, 100, "AcDbPoint", 10, "1.5", 20, "2.5", 30, "0", 39, "2",
                0, "CIRCLE", 5, "32", 100, "AcDbEntity", 8, "steel", 6, "Continuous", 100, "AcDbCircle", 39, "10", 10, "10", 20, "20", 30, "0", 40, "3.3824149791069402",
                0, "ARC", 5, "33", 100, "AcDbEntity", 8, "steel", 370, 50, 100, "AcDbCircle", 10, "10", 20, "20", 30, "0", 40, "7", 100, "AcDbArc", 50, "12.5", 51, "200",
                0, "ARC", 5, "34", 100, "AcDbEntity", 8, "cut", 100, "AcDbCircle", 10, "0", 20, "0", 30, "7", 40, "10", 210, "1", 220, "0", 230, "0", 100, "AcDbArc", 50, "0", 51, "90",
                0, "LWPOLYLINE", 5, "35", 100, "AcDbEntity", 8, "steel", 6, "HIDDEN", 62, 5, 100, "AcDbPolyline", 90, 4, 70, 1, 39, "4", 10, "0", 20, "0", 10, "60", 20, "0", 42, "1", 10, "60", 20, "20", 10, "0", 20, "20", 42, "1",
                0, "LWPOLYLINE", 5, "36", 100, "AcDbEntity", 8, "cut", 100, "AcDbPolyline", 90, 3, 70, 0, 38, "5", 10, "0", 20, "0", 42, "0.5", 10, "10", 20, "0", 10, "10", 20, "10",
                0, "ELLIPSE", 5, "37", 100, "AcDbEntity", 8, "cut", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "30", 21, "0", 31, "0", 210, "0", 220, "0", 230, "1", 40, "0.5", 41, "0", 42, "6.283185307179586",
                0, "ELLIPSE", 5, "38", 100, "AcDbEntity", 8, "cut", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "30", 21, "0", 31, "0", 210, "0", 220, "0", 230, "1", 40, "0.5", 41, "0", 42, "1.5707963267948966",
                0, "SPLINE", 5, "39", 100, "AcDbEntity", 8, "cut", 100, "AcDbSpline", 210, "0", 220, "0", 230, "1", 70, 8, 71, 3, 72, 8, 73, 4, 74, 0,
                40, "0", 40, "0", 40, "0", 40, "0", 40, "1", 40, "1", 40, "1", 40, "1", 10, "0", 20, "0", 30, "0", 10, "100", 20, "150", 30, "0", 10, "200", 20, "150", 30, "0", 10, "300", 20, "0", 30, "0",
                0, "RAY", 5, "3A", 100, "AcDbEntity", 8, "0", 100, "AcDbRay", 10, "5", 20, "5", 30, "0", 11, "0.6", 21, "0.8", 31, "0",
                0, "XLINE", 5, "3B", 100, "AcDbEntity", 8, "0", 100, "AcDbXline", 10, "0", 20, "0", 30, "7", 11, "0", 21, "0", 31, "1",
                0, "3DFACE", 5, "3C", 100, "AcDbEntity", 8, "steel", 62, 2, 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "0", 23, "10", 33, "0",
                0, "3DFACE", 5, "3D", 100, "AcDbEntity", 8, "steel", 100, "AcDbFace", 10, "0", 20, "0", 30, "5", 11, "10", 21, "0", 31, "5", 12, "10", 22, "10", 32, "5", 13, "0", 23, "10", 33, "5",
                0, "SOLID", 5, "3E", 100, "AcDbEntity", 8, "cut", 100, "AcDbTrace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "10", 23, "10", 33, "0",
                0, "POLYLINE", 5, "3F", 100, "AcDbEntity", 8, "steel", 62, 4, 100, "AcDb3dPolyline", 66, 1, 10, "0", 20, "0", 30, "0", 70, 8,
                0, "VERTEX", 5, "40", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb3dPolylineVertex", 10, "0", 20, "0", 30, "0", 70, 32,
                0, "VERTEX", 5, "41", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb3dPolylineVertex", 10, "5", 20, "0", 30, "3", 70, 32,
                0, "SEQEND", 5, "42", 100, "AcDbEntity", 8, "steel"));

        [Fact]
        public void ADrawingReadAndWrittenAgain_ReadsBackTheSameShapesAndTheSameLook()
        {
            foreach (bool plan in new[] { true, false })
            {
                var options = new DxfReadOptions { PlanAs2D = plan, Tolerance = Tol };
                DxfDocument first = DxfReader.Parse(Drawing(), options);
                var writer = new DxfWriter();

                foreach (DxfEntity entity in first.Entities)
                {
                    writer.Add(entity);
                }

                DxfDocument second = DxfReader.Parse(writer.ToString(), options);

                Assert.Empty(second.Skipped);
                Assert.Empty(second.Warnings);
                Assert.Equal(first.Entities.Count, second.Entities.Count);

                for (int i = 0; i < first.Entities.Count; i++)
                {
                    DxfEntity was = first.Entities[i];
                    DxfEntity now = second.Entities[i];
                    string what = was.Type + " " + was.Handle;

                    Assert.Equal(was.Shape.GetType(), now.Shape.GetType());
                    Assert.Equal(was.Layer, now.Layer);
                    Assert.Equal(was.Colour, now.Colour);
                    Assert.Equal(was.TrueColour, now.TrueColour);
                    Assert.Equal(was.Linetype, now.Linetype);
                    Assert.Equal(was.Lineweight, now.Lineweight);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(was.Thickness), BitConverter.DoubleToInt64Bits(now.Thickness));
                    Same(was.Shape, now.Shape, what);
                }
            }
        }

        /// <summary>
        /// The same shape: to the bit, but for what goes through degrees or the system of a tilted plane once more, an arc
        /// and an ellipse, which comes back to the last place or two.
        /// </summary>
        private static void Same(object expected, object actual, string what)
        {
            switch (expected)
            {
                case GeoArc2 a:
                    var b = (GeoArc2)actual;
                    Assert.Equal(a.Center, b.Center);
                    Assert.Equal(a.Radius, b.Radius);
                    Assert.Equal(a.StartAngle, b.StartAngle, 14);
                    Assert.Equal(a.SweptAngle, b.SweptAngle, 14);
                    return;

                case GeoArc3 a:
                    var c = (GeoArc3)actual;
                    Assert.True(a.StartPoint.DistanceTo(c.StartPoint) < 1E-12 && a.EndPoint.DistanceTo(c.EndPoint) < 1E-12 && a.MidPoint.DistanceTo(c.MidPoint) < 1E-12, what);
                    return;

                case GeoEllipse2 a:
                    var e = (GeoEllipse2)actual;
                    Assert.Equal(a.Center, e.Center);
                    Assert.Equal(a.MajorRadius, e.MajorRadius, 12);
                    Assert.Equal(a.MinorRadius, e.MinorRadius, 12);
                    return;
            }

            Assert.True(Numbers(expected).SequenceEqual(Numbers(actual)), what + ": " + string.Join(", ", Numbers(expected)) + " read as " + string.Join(", ", Numbers(actual)));
        }

        /// <summary>The bits of every number of a shape the file holds, in a fixed order.</summary>
        private static List<long> Numbers(object shape)
        {
            var n = new List<double>();
            void P2(GeoPoint2 p) { n.Add(p.X); n.Add(p.Y); }
            void P3(GeoPoint3 p) { n.Add(p.X); n.Add(p.Y); n.Add(p.Z); }

            switch (shape)
            {
                case GeoPoint2 s: P2(s); break;
                case GeoPoint3 s: P3(s); break;
                case GeoLine2 s: P2(s.StartPoint); P2(s.EndPoint); break;
                case GeoLine3 s: P3(s.StartPoint); P3(s.EndPoint); break;
                case GeoCircle2 s: P2(s.Center); n.Add(s.Radius); break;
                case GeoCircle3 s: P3(s.Center); n.Add(s.Radius); break;
                case GeoPolyline2 s: s.Vertices.ToList().ForEach(P2); break;
                case GeoPolyline3 s: s.Vertices.ToList().ForEach(P3); break;
                case GeoPolygon2 s: s.Vertices.ToList().ForEach(P2); break;
                case GeoPolygon3 s: s.Vertices.ToList().ForEach(P3); break;
                case GeoPolygonArc2 s: s.Vertices.ToList().ForEach(P2); n.AddRange(s.Bulges); break;
                case GeoPolygonArc3 s: s.Vertices.ToList().ForEach(P3); n.AddRange(s.Bulges); break;
                case GeoPolylineArc2 s: s.Vertices.ToList().ForEach(P2); n.AddRange(s.Bulges); break;
                case GeoPolylineArc3 s: s.Vertices.ToList().ForEach(P3); n.AddRange(s.Bulges); break;
                case GeoSpline2 s: s.ControlPoints.ToList().ForEach(P2); n.AddRange(s.Knots); n.AddRange(s.Weights); break;
                case GeoSpline3 s: s.ControlPoints.ToList().ForEach(P3); n.AddRange(s.Knots); n.AddRange(s.Weights); break;
                case GeoRay2 s: P2(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); break;
                case GeoRay3 s: P3(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); n.Add(s.Direction.Z); break;
                case GeoXLine3 s: P3(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); n.Add(s.Direction.Z); break;
                case GeoTriangle3 s: P3(s.A); P3(s.B); P3(s.C); break;
                case GeoFace3 s: s.Boundary.Vertices.ToList().ForEach(P3); break;
                case GeoEllipse3 s: P3(s.Center); break;
                default: throw new ArgumentException(shape.GetType().Name);
            }

            return n.ConvertAll(BitConverter.DoubleToInt64Bits);
        }

        [Fact]
        public void TheLookIsWrittenInAutoCADsOrder_OnTheEntityAlone_AndTheThicknessWhereTheEntityHasOne()
        {
            DxfDocument read = DxfReader.Parse(Drawing(), new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });
            string line = new DxfWriter().Add(read.Entities[0]).ToString();
            var thick = new DxfEntity(new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0)), "3DFACE", "0", null, null, null, null, null, 3.0, new DxfGroup[0]);
            string face = new DxfWriter().Add(thick).ToString();
            string chain = new DxfWriter().Add(read.Entities.First(e => e.Type == "POLYLINE")).ToString();

            // The linetype, the colour, the lineweight and the true colour after the layer; the thickness after the subclass.
            Assert.Contains("  8\nsteel\n  6\nDASHED\n 62\n1\n370\n35\n420\n16744448\n100\nAcDbLine\n 39\n50\n 10\n0.1\n", line);
            Assert.DoesNotContain("\n 39\n", face);

            // A 3D polyline's colour is on the POLYLINE, not on its vertices or its end.
            string entities = chain.Substring(chain.IndexOf("ENTITIES", StringComparison.Ordinal));
            Assert.Equal(1, entities.Split('\n').Count(l => l == " 62"));
            Assert.Equal(4, entities.Split('\n').Count(l => l == "  8"));
        }

        [Fact]
        public void ALinetypeTheTableDoesNotHave_IsAddedToItByNameOnce_AndTheGroupsOfTheFileAreNotWritten()
        {
            DxfDocument read = DxfReader.Parse(Drawing(), new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });
            var writer = new DxfWriter();

            foreach (DxfEntity entity in read.Entities)
            {
                writer.Add(entity);
                writer.Add(entity);
            }

            string text = writer.ToString();
            string[] lines = text.Split('\n');
            int table = Array.IndexOf(lines, "LTYPE");

            // ByBlock, ByLayer and Continuous, then DASHED and HIDDEN; Continuous is the table's own, as the entity names it.
            Assert.Equal("5", lines[table + 8]);
            Assert.Equal(1, lines.Count(l => l == "DASHED") - 2 * read.Entities.Count(e => e.Linetype == "DASHED"));
            Assert.Equal(1, lines.Count(l => l == "HIDDEN") - 2 * read.Entities.Count(e => e.Linetype == "HIDDEN"));
            Assert.Equal(new[] { "DASHED", "HIDDEN" }, DxfReader.Parse(text).Entities.Select(e => e.Linetype).Where(t => t != null && t != "Continuous").Distinct().ToArray());
            Assert.DoesNotContain("1000", lines.Where((l, i) => i % 2 == 0).Select(l => l.Trim()));
        }

        [Fact]
        public void AnEntityWhoseShapeHasNoOverload_IsRefusedNamingItsType()
        {
            var text = new DxfEntity("a text", "TEXT", "0", null, null, null, null, null, 0.0, new DxfGroup[0]);
            var ball = new DxfEntity(new GeoSphere3(new GeoPoint3(0, 0, 0), 5), "BALL", "0", null, null, null, null, null, 0.0, new DxfGroup[0]);

            Assert.Contains("String", Assert.Throws<ArgumentException>(() => new DxfWriter().Add(text)).Message);
            Assert.Contains("GeoSphere3", Assert.Throws<ArgumentException>(() => new DxfWriter().Add(ball)).Message);
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((DxfEntity)null));
        }
    }
}
