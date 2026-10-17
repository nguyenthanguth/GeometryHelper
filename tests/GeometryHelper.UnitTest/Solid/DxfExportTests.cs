using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Writing geometry out for AutoCAD as a DXF file of version R2000, read back here group by group.
    /// </summary>
    public class DxfExportTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // ----- The reader: a DXF file is pairs of lines, a group code and its value ------------------------------------

        private readonly struct Pair
        {
            public Pair(int code, string value)
            {
                Code = code;
                Value = value;
            }

            public int Code { get; }

            public string Value { get; }
        }

        /// <summary>One object of a section: its type, the value of the 0 that opened it, and the groups after.</summary>
        private sealed class Record
        {
            public string Type { get; set; }

            public List<Pair> Pairs { get; } = new List<Pair>();

            public bool Has(int code) => Pairs.Any(p => p.Code == code);

            public string Text(int code) => Pairs.First(p => p.Code == code).Value;

            // Read as a correctly rounding parser reads it, which .NET Framework's is not.
            public double Number(int code) => ExportTests.ParseExactly(Text(code));

            public List<double> Numbers(int code)
                => Pairs.Where(p => p.Code == code).Select(p => ExportTests.ParseExactly(p.Value)).ToList();

            public string Layer => Text(8);

            public string Handle => Has(5) ? Text(5) : Text(105);

            /// <summary>The point written in groups 10, 20, 30 (n = 0), 11, 21, 31 (n = 1) and so on.</summary>
            public GeoPoint3 Point(int n) => new GeoPoint3(Number(10 + n), Number(20 + n), Number(30 + n));
        }

        private sealed class Dxf
        {
            private readonly Dictionary<string, List<Record>> _records = new Dictionary<string, List<Record>>();
            private readonly Dictionary<string, List<Pair>> _header = new Dictionary<string, List<Pair>>();

            public List<Pair> Pairs { get; } = new List<Pair>();

            public List<string> Sections { get; } = new List<string>();

            public List<Record> Records(string section) => _records[section];

            public List<Record> Entities => _records["ENTITIES"];

            public List<Record> Of(string type) => Entities.Where(e => e.Type == type).ToList();

            public List<Pair> Header(string variable) => _header[variable];

            public static Dxf Read(string text)
            {
                Assert.DoesNotContain("\r", text);
                Assert.EndsWith("\n", text);

                string[] lines = text.Substring(0, text.Length - 1).Split('\n');
                Assert.Equal(0, lines.Length % 2);

                var dxf = new Dxf();

                for (int i = 0; i < lines.Length; i += 2)
                {
                    dxf.Pairs.Add(new Pair(int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture), lines[i + 1]));
                }

                int at = 0;

                while (at < dxf.Pairs.Count)
                {
                    Pair open = dxf.Pairs[at];

                    if (open.Code == 0 && open.Value == "EOF")
                    {
                        Assert.Equal(dxf.Pairs.Count - 1, at);
                        return dxf;
                    }

                    Assert.Equal(0, open.Code);
                    Assert.Equal("SECTION", open.Value);
                    Assert.Equal(2, dxf.Pairs[at + 1].Code);

                    string name = dxf.Pairs[at + 1].Value;
                    dxf.Sections.Add(name);
                    at += 2;

                    var records = new List<Record>();
                    List<Pair> variable = null;

                    while (!(dxf.Pairs[at].Code == 0 && dxf.Pairs[at].Value == "ENDSEC"))
                    {
                        Pair pair = dxf.Pairs[at];

                        if (name == "HEADER")
                        {
                            if (pair.Code == 9)
                            {
                                variable = new List<Pair>();
                                dxf._header.Add(pair.Value, variable);
                            }
                            else
                            {
                                variable.Add(pair);
                            }
                        }
                        else if (pair.Code == 0)
                        {
                            records.Add(new Record { Type = pair.Value });
                        }
                        else
                        {
                            records[records.Count - 1].Pairs.Add(pair);
                        }

                        at++;
                    }

                    dxf._records[name] = records;
                    at++;
                }

                throw new Xunit.Sdk.XunitException("The file does not end with EOF.");
            }

            public static Dxf Of(DxfWriter writer) => Read(writer.ToString());

            /// <summary>Every object of every section but the header, with a handle of its own.</summary>
            public List<Record> Everything() => _records.Values.SelectMany(r => r).ToList();
        }

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>The signed volume under a set of 3DFACEs: a triangle ABC, or a quad as ABC and ACD.</summary>
        private static double VolumeUnder(IEnumerable<Record> faces)
        {
            double volume = 0.0;

            foreach (Record face in faces)
            {
                GeoVector3 a = GeoPoint3.Origin.GetVectorTo(face.Point(0));
                GeoVector3 b = GeoPoint3.Origin.GetVectorTo(face.Point(1));
                GeoVector3 c = GeoPoint3.Origin.GetVectorTo(face.Point(2));
                GeoVector3 d = GeoPoint3.Origin.GetVectorTo(face.Point(3));

                volume += a.DotProduct(b.CrossProduct(c)) / 6.0;

                if (face.Point(3).DistanceTo(face.Point(2)) > 1E-12)
                {
                    volume += a.DotProduct(c.CrossProduct(d)) / 6.0;
                }
            }

            return volume;
        }

        /// <summary>The area of 3DFACEs: a triangle ABC, or a quad as ABC and ACD.</summary>
        private static double AreaOf(IEnumerable<Record> faces)
        {
            double area = 0.0;

            foreach (Record face in faces)
            {
                area += new GeoTriangle3(face.Point(0), face.Point(1), face.Point(2)).Area;

                if (face.Point(3).DistanceTo(face.Point(2)) > 1E-12)
                {
                    area += new GeoTriangle3(face.Point(0), face.Point(2), face.Point(3)).Area;
                }
            }

            return area;
        }

        /// <summary>
        /// Every entity belongs to the model space, but the vertices and the end of a 3D polyline, which belong to it.
        /// </summary>
        private static void AssertOwners(Dxf dxf)
        {
            string modelSpace = dxf.Records("TABLES").Single(r => r.Type == "BLOCK_RECORD" && r.Text(2) == "*Model_Space").Handle;
            string polyline = null;

            foreach (Record entity in dxf.Entities)
            {
                if (entity.Type == "POLYLINE")
                {
                    polyline = entity.Handle;
                }

                bool inside = entity.Type == "VERTEX" || entity.Type == "SEQEND";
                Assert.Equal(inside ? polyline : modelSpace, entity.Text(330));
            }
        }

        /// <summary>The invisible-edge flags of a 3DFACE: 1 for the first edge, then 2, 4 and 8.</summary>
        private static int Flags(Record face) => face.Has(70) ? (int)face.Number(70) : 0;

        /// <summary>The length of the edges of 3DFACEs that are drawn, an edge of no length counting for nothing.</summary>
        private static double VisibleLength(IEnumerable<Record> faces)
        {
            double length = 0.0;

            foreach (Record face in faces)
            {
                GeoPoint3[] corner = { face.Point(0), face.Point(1), face.Point(2), face.Point(3) };

                for (int i = 0; i < 4; i++)
                {
                    if ((Flags(face) & (1 << i)) == 0)
                    {
                        length += corner[i].DistanceTo(corner[(i + 1) % 4]);
                    }
                }
            }

            return length;
        }

        /// <summary>How many edges of 3DFACEs are hidden, an edge of no length not counted.</summary>
        private static int HiddenEdges(IEnumerable<Record> faces)
        {
            int hidden = 0;

            foreach (Record face in faces)
            {
                GeoPoint3[] corner = { face.Point(0), face.Point(1), face.Point(2), face.Point(3) };

                for (int i = 0; i < 4; i++)
                {
                    if ((Flags(face) & (1 << i)) != 0 && corner[i].DistanceTo(corner[(i + 1) % 4]) > 0.0)
                    {
                        hidden++;
                    }
                }
            }

            return hidden;
        }

        // ===== Step 5: the skeleton ===================================================================================

        [Fact]
        [Trait("Step", "5")]
        public void TheSectionsComeInOrderAndTheFileEndsWithEof()
        {
            string text = new DxfWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0))).ToString();
            Dxf dxf = Dxf.Read(text);

            Assert.Equal(new[] { "HEADER", "CLASSES", "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" }, dxf.Sections);
            Assert.Equal("EOF", dxf.Pairs[dxf.Pairs.Count - 1].Value);
            Assert.Equal(0, dxf.Pairs[dxf.Pairs.Count - 1].Code);
            Assert.All(text, c => Assert.True(c < 128, "The file is plain ASCII, as R2000 asks."));
            Assert.Equal('0', text.TrimStart()[0]);
        }

        [Fact]
        [Trait("Step", "5")]
        public void TheHeaderStatesTheVersionTheUnitsAndTheExtents()
        {
            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoLine3(new GeoPoint3(-5, 2, 7), new GeoPoint3(30, -8, 1.5)))
                .Add(new GeoLine2(new GeoPoint2(1, 1), new GeoPoint2(100, 40))));

            Assert.Equal("AC1015", Assert.Single(dxf.Header("$ACADVER")).Value);
            Assert.Equal("4", Assert.Single(dxf.Header("$INSUNITS")).Value);

            List<Pair> min = dxf.Header("$EXTMIN");
            List<Pair> max = dxf.Header("$EXTMAX");
            Assert.Equal(new[] { -5.0, -8.0, 0.0 }, min.Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)));
            Assert.Equal(new[] { 10, 20, 30 }, min.Select(p => p.Code));
            Assert.Equal(new[] { 100.0, 40.0, 7.0 }, max.Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)));

            long seed = long.Parse(Assert.Single(dxf.Header("$HANDSEED")).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            long highest = dxf.Everything().Where(r => r.Has(5) || r.Has(105))
                .Max(r => long.Parse(r.Handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

            Assert.True(seed > highest, "The handle seed has to be past every handle used.");
        }

        [Fact]
        [Trait("Step", "5")]
        public void TheTablesBlocksAndObjectsTheVersionNeedsAreThere()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0))));

            Assert.Equal(
                new[] { "VPORT", "LTYPE", "LAYER", "STYLE", "VIEW", "UCS", "APPID", "DIMSTYLE", "BLOCK_RECORD" },
                dxf.Records("TABLES").Where(r => r.Type == "TABLE").Select(r => r.Text(2)));
            Assert.Equal(new[] { "ByBlock", "ByLayer", "Continuous" }, dxf.Records("TABLES").Where(r => r.Type == "LTYPE").Select(r => r.Text(2)));
            Assert.Equal(new[] { "*Model_Space", "*Paper_Space" }, dxf.Records("TABLES").Where(r => r.Type == "BLOCK_RECORD").Select(r => r.Text(2)));
            Assert.Equal(new[] { "*Model_Space", "*Paper_Space" }, dxf.Records("BLOCKS").Where(r => r.Type == "BLOCK").Select(r => r.Text(2)));
            Assert.Equal(2, dxf.Records("BLOCKS").Count(r => r.Type == "ENDBLK"));
            Assert.Equal("DICTIONARY", dxf.Records("OBJECTS")[0].Type);
            Assert.Equal(9, dxf.Records("TABLES").Count(r => r.Type == "ENDTAB"));
            Assert.Equal(
                new[] { 0.0, 0.0 },
                dxf.Records("TABLES").Where(r => r.Type == "TABLE" && (r.Text(2) == "VIEW" || r.Text(2) == "UCS")).Select(r => r.Number(70)));
        }

        [Fact]
        [Trait("Step", "5")]
        public void EveryHandleIsUniqueAndEveryOwnerExists()
        {
            var writer = new DxfWriter()
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)), "walls")
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 5, 2)), "bars")
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)))
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10)), "walls");
            Dxf dxf = Dxf.Of(writer);

            List<string> handles = dxf.Everything().Where(r => r.Has(5) || r.Has(105)).Select(r => r.Handle).ToList();

            Assert.Equal(handles.Count, handles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(handles, h => Assert.Matches("^[0-9A-F]+$", h));

            var known = new HashSet<string>(handles) { "0" };

            foreach (Record record in dxf.Everything())
            {
                foreach (int code in new[] { 330, 340, 350, 390 })
                {
                    foreach (Pair pair in record.Pairs.Where(p => p.Code == code))
                    {
                        Assert.Contains(pair.Value, known);
                    }
                }
            }

            AssertOwners(dxf);
            Assert.All(dxf.Entities, e => Assert.Equal("AcDbEntity", e.Pairs.First(p => p.Code == 100).Value));
            Assert.All(dxf.Entities, e => Assert.True(long.Parse(e.Handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture) >= 0x100));
        }

        [Fact]
        [Trait("Step", "5")]
        public void ALayerIsCreatedOnFirstUseAndEveryEntityIsOnOneInTheTable()
        {
            var a = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(a, "walls").Add(a).Add(a, "Walls").Add(a, "slab").Add(a, "  ").Add(a, "walls"));

            List<Record> layers = dxf.Records("TABLES").Where(r => r.Type == "LAYER").ToList();

            Assert.Equal(new[] { "0", "walls", "slab" }, layers.Select(l => l.Text(2)));
            Assert.All(layers, l => Assert.Equal(7.0, l.Number(62)));
            Assert.All(layers, l => Assert.Equal("Continuous", l.Text(6)));
            Assert.Equal(new[] { "walls", "0", "walls", "slab", "0", "walls" }, dxf.Entities.Select(e => e.Layer));
            Assert.Equal(
                layers.Count,
                dxf.Records("TABLES").Single(r => r.Type == "TABLE" && r.Text(2) == "LAYER").Number(70));
        }

        [Fact]
        [Trait("Step", "5")]
        public void ALayerNameDxfRefusesIsRewrittenAndOneWithAccentsKeepsItsLettersAsEscapes()
        {
            var a = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            string text = new DxfWriter().Add(a, "wall<1>/ext:*,").Add(a, "bétón").ToString();
            Dxf dxf = Dxf.Read(text);

            string[] names = dxf.Records("TABLES").Where(r => r.Type == "LAYER").Select(l => l.Text(2)).ToArray();

            Assert.Equal("wall_1__ext___", names[1]);
            Assert.Equal("b\\U+00E9t\\U+00F3n", names[2]);
            Assert.All(text, c => Assert.True(c < 128));
        }

        [Fact]
        [Trait("Step", "5")]
        public void ALineIsWrittenWithItsTwoEndsAsALine()
        {
            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoLine2(new GeoPoint2(1, 2), new GeoPoint2(3, 4)))
                .Add(new GeoLine3(new GeoPoint3(1, 2, 3), new GeoPoint3(-4, 5.5, 6)), "bars"));

            Assert.Equal(2, dxf.Entities.Count);
            Assert.All(dxf.Entities, e => Assert.Equal("LINE", e.Type));
            Assert.All(dxf.Entities, e => Assert.Contains(e.Pairs, p => p.Code == 100 && p.Value == "AcDbLine"));
            Assert.Equal(new GeoPoint3(1, 2, 0), dxf.Entities[0].Point(0));
            Assert.Equal(new GeoPoint3(3, 4, 0), dxf.Entities[0].Point(1));
            Assert.Equal(new GeoPoint3(1, 2, 3), dxf.Entities[1].Point(0));
            Assert.Equal(new GeoPoint3(-4, 5.5, 6), dxf.Entities[1].Point(1));
            Assert.Equal("bars", dxf.Entities[1].Layer);
        }

        [Fact]
        [Trait("Step", "5")]
        public void AFileOfNothingIsStillWellFormed()
        {
            Dxf dxf = Dxf.Of(new DxfWriter());

            Assert.Equal(new[] { "HEADER", "CLASSES", "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" }, dxf.Sections);
            Assert.Empty(dxf.Entities);
            Assert.Equal("0", Assert.Single(dxf.Records("TABLES"), r => r.Type == "LAYER").Text(2));
        }

        [Fact]
        [Trait("Step", "5")]
        public void NumbersAreWrittenExactlyWhateverTheCulture()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var far = new GeoPoint3(123456.78901234567, -0.1, 3E-9);
                Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoLine3(far, GeoPoint3.Origin)));

                Assert.Equal(far, dxf.Entities[0].Point(0));
                Assert.Equal(123456.78901234567, dxf.Entities[0].Number(10));
                Assert.Equal(3E-9, dxf.Entities[0].Number(30));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Fact]
        [Trait("Step", "5")]
        public void ASavedFileHoldsTheTextWithoutAByteOrderMark()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dxf-" + Guid.NewGuid().ToString("N") + ".dxf");
            var writer = new DxfWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)));

            try
            {
                writer.Save(path);
                byte[] bytes = System.IO.File.ReadAllBytes(path);

                Assert.True(bytes[0] < 128, "A byte order mark in front of the first group would not be read.");
                Assert.Equal(writer.ToString(), System.Text.Encoding.ASCII.GetString(bytes));

                var sink = new System.IO.StringWriter();
                writer.WriteTo(sink);
                Assert.Equal(writer.ToString(), sink.ToString());
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        [Trait("Step", "5")]
        public void ANullWriterIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().WriteTo(null));
        }

        [Fact]
        [Trait("Step", "7")]
        public void ANullShapeIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolyline2)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolylineArc2)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolygonArc2)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoMesh3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoObb3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((IEnumerable<GeoTriangle3>)null));
        }

        // ===== Step 6: the plane =======================================================================================

        [Fact]
        [Trait("Step", "6")]
        public void APointIsAPoint()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoPoint2(4.5, -2)).Add(new GeoPoint3(1, 2, 3)));

            Assert.Equal(new[] { "POINT", "POINT" }, dxf.Entities.Select(e => e.Type));
            Assert.Equal(new GeoPoint3(4.5, -2, 0), dxf.Entities[0].Point(0));
            Assert.Equal(new GeoPoint3(1, 2, 3), dxf.Entities[1].Point(0));
            Assert.Contains(dxf.Entities[0].Pairs, p => p.Code == 100 && p.Value == "AcDbPoint");
        }

        [Fact]
        [Trait("Step", "6")]
        public void AChainIsAnOpenLightweightPolylineAndEveryClosedShapeIsAClosedOne()
        {
            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5)))
                .Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5), new GeoPoint2(0, 5)))
                .Add(new GeoRectangle2(new GeoPoint2(15, 10), 30, 20))
                .Add(new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(0, 3))));

            Assert.All(dxf.Entities, e => Assert.Equal("LWPOLYLINE", e.Type));
            Assert.All(dxf.Entities, e => Assert.Contains(e.Pairs, p => p.Code == 100 && p.Value == "AcDbPolyline"));
            Assert.Equal(new[] { 3.0, 4.0, 4.0, 3.0 }, dxf.Entities.Select(e => e.Number(90)));
            Assert.Equal(new[] { 0.0, 1.0, 1.0, 1.0 }, dxf.Entities.Select(e => e.Number(70)));
            Assert.Equal(new[] { 0.0, 10.0, 10.0 }, dxf.Entities[0].Numbers(10));
            Assert.Equal(new[] { 0.0, 0.0, 5.0 }, dxf.Entities[0].Numbers(20));
            Assert.Equal(new[] { 0.0, 4.0, 0.0 }, dxf.Entities[3].Numbers(10));
            Assert.All(dxf.Entities, e => Assert.DoesNotContain(e.Pairs, p => p.Code == 42));

            // The rectangle's four corners are its own, 30 by 20.
            Assert.Equal(30.0, dxf.Entities[2].Numbers(10).Max() - dxf.Entities[2].Numbers(10).Min());
            Assert.Equal(20.0, dxf.Entities[2].Numbers(20).Max() - dxf.Entities[2].Numbers(20).Min());
        }

        [Fact]
        [Trait("Step", "6")]
        public void ARotatedRectangleKeepsItsCornersAsTheyAre()
        {
            GeoRectangle2 rectangle = new GeoRectangle2(new GeoPoint2(3, 4), 30, 20, 0.7);
            Dxf dxf = Dxf.Of(new DxfWriter().Add(rectangle));
            Record polyline = Assert.Single(dxf.Entities);
            GeoPoint2[] corners = rectangle.GetVertices();

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(corners[i].X, polyline.Numbers(10)[i], 12);
                Assert.Equal(corners[i].Y, polyline.Numbers(20)[i], 12);
            }
        }

        /// <summary>
        /// The middle of the arc a bulge draws from one vertex to the next, by the definition DXF gives: a positive bulge
        /// turns counter-clockwise, bowing to the right of the way from the first vertex to the second.
        /// </summary>
        private static GeoPoint2 BulgeMiddle(GeoPoint2 p, GeoPoint2 q, double bulge)
        {
            double dx = q.X - p.X;
            double dy = q.Y - p.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            double sagitta = bulge * chord / 2.0;

            return new GeoPoint2((p.X + q.X) / 2.0 + sagitta * dy / chord, (p.Y + q.Y) / 2.0 - sagitta * dx / chord);
        }

        [Fact]
        [Trait("Step", "6")]
        public void ACounterClockwiseEdgeWritesAPositiveBulgeAndAClockwiseOneANegativeBulgeAsDxfDefinesThem()
        {
            // A quarter of the circle of radius 10 about the origin, counter-clockwise, then the same quarter back.
            var quarter = new GeoArc2(GeoPoint2.Origin, 10, 0, Math.PI / 2);
            var forward = new GeoPolylineArc2(new[] { new GeoEdge2(quarter) });
            var back = new GeoPolylineArc2(new[] { new GeoEdge2(quarter.Reverse()) });

            Dxf dxf = Dxf.Of(new DxfWriter().Add(forward).Add(back));

            double up = dxf.Entities[0].Numbers(42).Single();
            double down = dxf.Entities[1].Numbers(42).Single();

            // The sign and the size are those of GeoEdge2.Bulge, tan of a quarter of the sweep.
            Assert.Equal(forward.GetEdgeAt(0).Bulge, up, 12);
            Assert.Equal(back.GetEdgeAt(0).Bulge, down, 12);
            Assert.Equal(Math.Tan(Math.PI / 8), up, 12);
            Assert.Equal(-Math.Tan(Math.PI / 8), down, 12);

            // And they mean what DXF says: the arc through the middle of the quarter, not the other way round.
            GeoPoint2 middle = BulgeMiddle(new GeoPoint2(10, 0), new GeoPoint2(0, 10), up);
            Assert.Equal(10.0 * Math.Cos(Math.PI / 4), middle.X, 12);
            Assert.Equal(10.0 * Math.Sin(Math.PI / 4), middle.Y, 12);

            GeoPoint2 middleBack = BulgeMiddle(new GeoPoint2(0, 10), new GeoPoint2(10, 0), down);
            Assert.Equal(middle.X, middleBack.X, 12);
            Assert.Equal(middle.Y, middleBack.Y, 12);

            // The vertex carrying a bulge is the one the edge leaves; the last vertex of an open chain carries none.
            Assert.Equal(1, dxf.Entities[0].Pairs.Count(p => p.Code == 42));
            Assert.Equal(10.0, dxf.Entities[0].Numbers(10)[0], 12);
            Assert.Equal(0.0, dxf.Entities[0].Numbers(10)[1], 12);
        }

        [Fact]
        [Trait("Step", "6")]
        public void ASemicircleHasTheBulgeOfOne()
        {
            var half = new GeoPolylineArc2(new[] { new GeoEdge2(new GeoArc2(GeoPoint2.Origin, 10, 0, Math.PI)) });
            var other = new GeoPolylineArc2(new[] { new GeoEdge2(new GeoArc2(GeoPoint2.Origin, 10, 0, Math.PI, true)) });
            Dxf dxf = Dxf.Of(new DxfWriter().Add(half).Add(other));

            Assert.Equal(1.0, dxf.Entities[0].Numbers(42).Single(), 12);
            Assert.Equal(-1.0, dxf.Entities[1].Numbers(42).Single(), 12);
        }

        [Fact]
        [Trait("Step", "6")]
        public void ALoopWithAnArcClosingItCarriesTheBulgeOnItsLastVertex()
        {
            // A square 0..10 whose left side bows out as a semicircle: the edge from (0, 10) back to (0, 0).
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) },
                new[] { 0.0, 0.0, 0.0, 1.0 });

            Record polyline = Assert.Single(Dxf.Of(new DxfWriter().Add(loop)).Entities);

            Assert.Equal(1.0, polyline.Number(70));
            Assert.Equal(4.0, polyline.Number(90));
            Assert.Equal(1.0, polyline.Numbers(42).Single(), 12);

            // The 42 follows the 20 of the fourth vertex: groups run 10 20 10 20 10 20 10 20 42.
            int[] codes = polyline.Pairs.Select(p => p.Code).ToArray();
            Assert.Equal(42, codes[codes.Length - 1]);
        }

        [Fact]
        [Trait("Step", "6")]
        public void AFaceIsOneClosedPolylineForItsBoundaryAndOneForEachHole()
        {
            var plate = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60), new GeoPoint2(0, 60));
            var slot = new GeoPolygon2(new GeoPoint2(20, 20), new GeoPoint2(80, 20), new GeoPoint2(80, 40), new GeoPoint2(20, 40));
            var hole = new GeoPolygon2(new GeoPoint2(5, 5), new GeoPoint2(10, 5), new GeoPoint2(10, 10), new GeoPoint2(5, 10));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoFace2(plate, new[] { slot, hole }), "plate"));

            Assert.Equal(3, dxf.Entities.Count);
            Assert.All(dxf.Entities, e => Assert.Equal("LWPOLYLINE", e.Type));
            Assert.All(dxf.Entities, e => Assert.Equal("plate", e.Layer));
            Assert.All(dxf.Entities, e => Assert.Equal(1.0, e.Number(70)));
            Assert.Equal(100.0, dxf.Entities[0].Numbers(10).Max());
            Assert.Equal(80.0, dxf.Entities[1].Numbers(10).Max());
            Assert.Equal(10.0, dxf.Entities[2].Numbers(10).Max());
        }

        [Fact]
        [Trait("Step", "6")]
        public void ACircleIsACircleAndAnEllipseIsAnEllipse()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoCircle2(new GeoPoint2(5, 6), 7.5)));
            Record circle = Assert.Single(dxf.Entities);

            Assert.Equal("CIRCLE", circle.Type);
            Assert.Equal(new GeoPoint3(5, 6, 0), circle.Point(0));
            Assert.Equal(7.5, circle.Number(40));
            Assert.Contains(circle.Pairs, p => p.Code == 100 && p.Value == "AcDbCircle");
        }

        private static GeoPoint2 PointOfArc(Record arc, double degrees)
        {
            double r = arc.Number(40);
            double a = degrees * Math.PI / 180.0;

            return new GeoPoint2(arc.Number(10) + r * Math.Cos(a), arc.Number(20) + r * Math.Sin(a));
        }

        [Fact]
        [Trait("Step", "6")]
        public void ACounterClockwiseArcIsWrittenFromItsStartToItsEnd()
        {
            var arc = new GeoArc2(new GeoPoint2(5, 5), 10, Math.PI / 6, Math.PI * 1.25);
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(arc)).Entities);

            Assert.Equal("ARC", record.Type);
            Assert.Equal(new GeoPoint3(5, 5, 0), record.Point(0));
            Assert.Equal(10.0, record.Number(40));
            Assert.Contains(record.Pairs, p => p.Code == 100 && p.Value == "AcDbArc");
            Assert.Equal(30.0, record.Number(50), 9);
            Assert.Equal(225.0, record.Number(51), 9);

            Assert.True(PointOfArc(record, record.Number(50)).IsEqualTo(arc.StartPoint, Tol));
            Assert.True(PointOfArc(record, record.Number(51)).IsEqualTo(arc.EndPoint, Tol));
        }

        [Fact]
        [Trait("Step", "6")]
        public void AClockwiseArcIsWrittenFromItsEndToItsStart()
        {
            // From 90 degrees round the right to 0: from (5, 15) down to (15, 5) about (5, 5), clockwise.
            var arc = new GeoArc2(new GeoPoint2(5, 5), 10, Math.PI / 2, 0.0, true);
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(arc)).Entities);

            Assert.True(arc.IsClockwise);
            Assert.Equal("ARC", record.Type);
            Assert.True(PointOfArc(record, record.Number(50)).IsEqualTo(arc.EndPoint, Tol));
            Assert.True(PointOfArc(record, record.Number(51)).IsEqualTo(arc.StartPoint, Tol));
            Assert.Equal(0.0, record.Number(50), 9);
            Assert.Equal(90.0, record.Number(51), 9);

            // Counter-clockwise from the written start to the written end passes the arc's middle.
            double sweep = (record.Number(51) - record.Number(50) + 360.0) % 360.0;
            Assert.Equal(arc.Length, sweep * Math.PI / 180.0 * 10.0, 9);
            Assert.True(PointOfArc(record, record.Number(50) + sweep / 2.0).IsEqualTo(arc.MidPoint, Tol));
        }

        [Fact]
        [Trait("Step", "6")]
        public void AnArcThatTurnsAWholeCircleIsACircle()
        {
            var whole = new GeoArc2(new GeoPoint2(1, 2), 3, 0.5, 0.5);
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(whole)).Entities);

            Assert.Equal("CIRCLE", record.Type);
            Assert.Equal(3.0, record.Number(40));
        }

        [Fact]
        [Trait("Step", "6")]
        public void AnEllipseIsWrittenWithItsMajorAxisItsRatioAndAWholeTurn()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(10, 20), new GeoVector2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)), 30, 10);
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(ellipse, "cams")).Entities);

            Assert.Equal("ELLIPSE", record.Type);
            Assert.Equal("cams", record.Layer);
            Assert.Contains(record.Pairs, p => p.Code == 100 && p.Value == "AcDbEllipse");
            Assert.Equal(new GeoPoint3(10, 20, 0), record.Point(0));

            GeoPoint3 axis = record.Point(1);
            Assert.Equal(30.0, Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z), 12);
            Assert.Equal(30.0 * Math.Cos(Math.PI / 6), axis.X, 12);
            Assert.Equal(30.0 * Math.Sin(Math.PI / 6), axis.Y, 12);
            Assert.Equal(10.0 / 30.0, record.Number(40), 12);
            Assert.Equal(0.0, record.Number(41));
            Assert.Equal(2.0 * Math.PI, record.Number(42));
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, new[] { record.Number(210), record.Number(220), record.Number(230) });

            // The curve DXF draws is the centre plus cos t along the axis plus sin t along its quarter turn, shortened by the ratio.
            double ratio = record.Number(40);

            foreach (double t in new[] { 0.0, 0.4, 1.2, 2.5, 4.0, 5.9 })
            {
                var drawn = new GeoPoint2(
                    10 + Math.Cos(t) * axis.X - Math.Sin(t) * ratio * axis.Y,
                    20 + Math.Cos(t) * axis.Y + Math.Sin(t) * ratio * axis.X);

                Assert.True(drawn.IsEqualTo(ellipse.GetPointAtAngle(t), Tol));
            }
        }

        [Fact]
        [Trait("Step", "6")]
        public void ARayIsWrittenAsARayFromItsOriginAlongItsDirection()
        {
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(new GeoRay2(new GeoPoint2(3, 4), new GeoVector2(0, 2)))).Entities);

            Assert.Equal("RAY", record.Type);
            Assert.Contains(record.Pairs, p => p.Code == 100 && p.Value == "AcDbRay");
            Assert.Equal(new GeoPoint3(3, 4, 0), record.Point(0));
            Assert.Equal(new GeoPoint3(0, 1, 0), record.Point(1));
        }

        [Fact]
        [Trait("Step", "6")]
        public void TheExtentsOfTheHeaderFollowTheCurvesNotTheirCircles()
        {
            // A quarter arc reaches 10 and 10 and no further; a half of a circle reaches round only its way.
            Dxf quarter = Dxf.Of(new DxfWriter().Add(new GeoArc2(GeoPoint2.Origin, 10, 0, Math.PI / 2)));
            Assert.Equal(new[] { 0.0, 0.0, 0.0 }, quarter.Header("$EXTMIN").Select(p => Math.Round(double.Parse(p.Value, CultureInfo.InvariantCulture), 9)));
            Assert.Equal(new[] { 10.0, 10.0, 0.0 }, quarter.Header("$EXTMAX").Select(p => Math.Round(double.Parse(p.Value, CultureInfo.InvariantCulture), 9)));

            // From minus 45 to 45 degrees, either way round: it reaches 10 on the right, and cos 45 on the left.
            foreach (GeoArc2 arc in new[]
            {
                new GeoArc2(GeoPoint2.Origin, 10, -Math.PI / 4, Math.PI / 4),
                new GeoArc2(GeoPoint2.Origin, 10, Math.PI / 4, -Math.PI / 4, true),
            })
            {
                Dxf dxf = Dxf.Of(new DxfWriter().Add(arc));
                double[] min = dxf.Header("$EXTMIN").Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)).ToArray();
                double[] max = dxf.Header("$EXTMAX").Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)).ToArray();

                Assert.Equal(10.0 * Math.Cos(Math.PI / 4), min[0], 9);
                Assert.Equal(-10.0 * Math.Sin(Math.PI / 4), min[1], 9);
                Assert.Equal(10.0, max[0], 9);
                Assert.Equal(10.0 * Math.Sin(Math.PI / 4), max[1], 9);
            }

            // A circle 5 about (20, 30); an ellipse 30 by 10 turned 30 degrees about the origin.
            Dxf circle = Dxf.Of(new DxfWriter().Add(new GeoCircle2(new GeoPoint2(20, 30), 5)));
            Assert.Equal(15.0, double.Parse(circle.Header("$EXTMIN")[0].Value, CultureInfo.InvariantCulture), 9);
            Assert.Equal(35.0, double.Parse(circle.Header("$EXTMAX")[1].Value, CultureInfo.InvariantCulture), 9);

            double phi = Math.PI / 6;
            Dxf ellipse = Dxf.Of(new DxfWriter().Add(new GeoEllipse2(GeoPoint2.Origin, new GeoVector2(Math.Cos(phi), Math.Sin(phi)), 30, 10)));
            double halfWidth = Math.Sqrt(900 * Math.Cos(phi) * Math.Cos(phi) + 100 * Math.Sin(phi) * Math.Sin(phi));
            double halfHeight = Math.Sqrt(900 * Math.Sin(phi) * Math.Sin(phi) + 100 * Math.Cos(phi) * Math.Cos(phi));
            Assert.Equal(-halfWidth, double.Parse(ellipse.Header("$EXTMIN")[0].Value, CultureInfo.InvariantCulture), 9);
            Assert.Equal(halfHeight, double.Parse(ellipse.Header("$EXTMAX")[1].Value, CultureInfo.InvariantCulture), 9);

            // A curved chain: the semicircle bows to y = -5 below its chord.
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { 1.0 });
            Dxf bowed = Dxf.Of(new DxfWriter().Add(chain));
            Assert.Equal(-5.0, double.Parse(bowed.Header("$EXTMIN")[1].Value, CultureInfo.InvariantCulture), 9);
            Assert.Equal(0.0, Math.Abs(double.Parse(bowed.Header("$EXTMAX")[1].Value, CultureInfo.InvariantCulture)), 9);
        }

        // ===== Step 7: space ===========================================================================================

        [Fact]
        [Trait("Step", "7")]
        public void ARayInSpaceIsARay()
        {
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(new GeoRay3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 5)))).Entities);

            Assert.Equal("RAY", record.Type);
            Assert.Equal(new GeoPoint3(1, 2, 3), record.Point(0));
            Assert.Equal(new GeoPoint3(0, 0, 1), record.Point(1));
        }

        [Fact]
        [Trait("Step", "7")]
        public void AChainInSpaceIsAThreeDimensionalPolylineWithItsVerticesAndAnEnd()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 2), new GeoPoint3(10, 10, 5));
            var loop = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(chain, "bars").Add(loop));

            Assert.Equal(
                new[] { "POLYLINE", "VERTEX", "VERTEX", "VERTEX", "SEQEND", "POLYLINE", "VERTEX", "VERTEX", "VERTEX", "VERTEX", "SEQEND" },
                dxf.Entities.Select(e => e.Type));

            Record first = dxf.Entities[0];
            Assert.Equal(8.0, first.Number(70));
            Assert.Equal(1.0, first.Number(66));
            Assert.Contains(first.Pairs, p => p.Code == 100 && p.Value == "AcDb3dPolyline");
            Assert.Equal(9.0, dxf.Entities[5].Number(70));

            Assert.All(dxf.Entities.Where(e => e.Type == "VERTEX"), v => Assert.Equal(32.0, v.Number(70)));
            Assert.All(dxf.Entities.Where(e => e.Type == "VERTEX"), v => Assert.Contains(v.Pairs, p => p.Code == 100 && p.Value == "AcDb3dPolylineVertex"));
            Assert.Equal(new GeoPoint3(10, 0, 2), dxf.Entities[2].Point(0));
            Assert.Equal(new GeoPoint3(10, 10, 5), dxf.Entities[3].Point(0));
            Assert.Equal("bars", dxf.Entities[4].Layer);
            Assert.Equal("bars", dxf.Entities[2].Layer);
            Assert.Equal("0", dxf.Entities[5].Layer);
            Assert.Equal("0", dxf.Entities[10].Layer);
        }

        [Fact]
        [Trait("Step", "7")]
        public void AnEllipseInSpaceIsNativeWithItsNormalAndItsAxisInWorldCoordinates()
        {
            GeoVector3 normal = new GeoVector3(1, 2, -1).Normalize();
            GeoVector3 across = new GeoVector3(2, -1, 0);
            GeoVector3 major = across.Normalize();

            var ellipse = new GeoEllipse3(new GeoPoint3(1, 2, 3), normal, major, 40, 15);
            Record record = Assert.Single(Dxf.Of(new DxfWriter().Add(ellipse)).Entities);

            Assert.Equal("ELLIPSE", record.Type);
            Assert.Equal(new GeoPoint3(1, 2, 3), record.Point(0));

            GeoPoint3 axis = record.Point(1);
            Assert.Equal(40.0, Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z), 12);
            Assert.Equal(15.0 / 40.0, record.Number(40), 12);
            Assert.Equal(0.0, record.Number(41));
            Assert.Equal(2.0 * Math.PI, record.Number(42));
            Assert.Equal(normal.X, record.Number(210), 12);
            Assert.Equal(normal.Y, record.Number(220), 12);
            Assert.Equal(normal.Z, record.Number(230), 12);

            // The minor direction is the extrusion crossed with the axis, which is how the ellipse's own is.
            var extrusion = new GeoVector3(record.Number(210), record.Number(220), record.Number(230));
            var along = new GeoVector3(axis.X, axis.Y, axis.Z);
            GeoVector3 minor = extrusion.CrossProduct(along).Multiply(record.Number(40));

            foreach (double t in new[] { 0.0, 0.7, 2.0, 3.3, 5.1 })
            {
                var drawn = new GeoPoint3(
                    1 + Math.Cos(t) * along.X + Math.Sin(t) * minor.X,
                    2 + Math.Cos(t) * along.Y + Math.Sin(t) * minor.Y,
                    3 + Math.Cos(t) * along.Z + Math.Sin(t) * minor.Z);

                Assert.True(drawn.IsEqualTo(ellipse.GetPointAtAngle(t), Tol));
            }
        }

        [Fact]
        [Trait("Step", "7")]
        public void ATriangleIsOneFaceWithItsThirdCornerRepeated()
        {
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 5));
            Record face = Assert.Single(Dxf.Of(new DxfWriter().Add(triangle, "skin")).Entities);

            Assert.Equal("3DFACE", face.Type);
            Assert.Equal("skin", face.Layer);
            Assert.Contains(face.Pairs, p => p.Code == 100 && p.Value == "AcDbFace");
            Assert.Equal(triangle.A, face.Point(0));
            Assert.Equal(triangle.B, face.Point(1));
            Assert.Equal(triangle.C, face.Point(2));
            Assert.Equal(triangle.C, face.Point(3));
        }

        [Fact]
        [Trait("Step", "7")]
        public void ABodyWithAnOpeningWritesAFacePerTriangleOfItsSurfaceAndTheVolumeUnderThemIsItsOwn()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });
            Dxf dxf = Dxf.Of(new DxfWriter().Add(plate, "plate"));

            Assert.All(dxf.Entities, e => Assert.Equal("3DFACE", e.Type));
            Assert.All(dxf.Entities, e => Assert.Equal("plate", e.Layer));
            Assert.Equal(64, dxf.Entities.Count);
            Assert.Equal(plate.TriangulateSurface(Tol).Length, dxf.Entities.Count);

            double expected = 100.0 * 100 * 20 - 20.0 * 20 * 20;
            Assert.Equal(expected, VolumeUnder(dxf.Entities), 6);
            Assert.Equal(expected, plate.GetVolume(Tol), 6);

            // With the tolerance it is given, the same.
            Dxf given = Dxf.Of(new DxfWriter().Add(plate, Tol, "plate"));
            Assert.Equal(dxf.Entities.Count, given.Entities.Count);
            Assert.Equal(expected, VolumeUnder(given.Entities), 6);
        }

        [Fact]
        [Trait("Step", "7")]
        public void ATriangleListAndAFaceWithAHoleAreFacesCoveringTheirArea()
        {
            var plate = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));
            var slot = new GeoPolygon3(new GeoPoint3(20, 20, 0), new GeoPoint3(80, 20, 0), new GeoPoint3(80, 40, 0), new GeoPoint3(20, 40, 0));
            var face = new GeoFace3(plate, new[] { slot });

            Dxf dxf = Dxf.Of(new DxfWriter().Add(face, "plate"));

            Assert.Equal(face.TriangulateSurface(Tol).Length, dxf.Entities.Count);
            Assert.All(dxf.Entities, e => Assert.Equal("3DFACE", e.Type));
            Assert.Equal(100.0 * 60 - 60.0 * 20, AreaOf(dxf.Entities), 6);

            GeoTriangle3[] triangles = face.TriangulateSurface(Tol);
            Dxf listed = Dxf.Of(new DxfWriter().Add((IEnumerable<GeoTriangle3>)triangles));
            Assert.Equal(triangles.Length, listed.Entities.Count);
            Assert.Equal(100.0 * 60 - 60.0 * 20, AreaOf(listed.Entities), 6);

            Dxf given = Dxf.Of(new DxfWriter().Add(face, Tol));
            Assert.Equal(triangles.Length, given.Entities.Count);
        }

        [Fact]
        [Trait("Step", "7")]
        public void AMeshWritesOneFacePerCellThatTurnsOneWayAndTrianglesForTheRest()
        {
            var rectangle = new GeoPolygon3(new GeoPoint3(0, 0, 5), new GeoPoint3(60, 0, 5), new GeoPoint3(60, 40, 5), new GeoPoint3(0, 40, 5));
            GeoMesh3 grid = new GeoFace3(rectangle).ToMesh(MeshOptions.Grid(20, 20));
            Dxf cells = Dxf.Of(new DxfWriter().Add(grid));

            Assert.Equal(6, cells.Entities.Count);
            Assert.All(cells.Entities, e => Assert.Equal("3DFACE", e.Type));
            Assert.Equal(grid.Area, AreaOf(cells.Entities), 6);
            Assert.Equal(2400.0, grid.Area, 6);
            Assert.Equal(6, grid.FaceCount);

            // A hexagon is one face of six corners, which no 3DFACE holds: four triangles.
            var hexagon = new GeoPolygon3(
                new GeoPoint3(10, 0, 0), new GeoPoint3(5, 8.66, 0), new GeoPoint3(-5, 8.66, 0),
                new GeoPoint3(-10, 0, 0), new GeoPoint3(-5, -8.66, 0), new GeoPoint3(5, -8.66, 0));
            GeoMesh3 one = new GeoFace3(hexagon).ToMesh(MeshKind.Convex);
            Dxf hexagonal = Dxf.Of(new DxfWriter().Add(one));

            int expected = Enumerable.Range(0, one.FaceCount).Sum(f => one.GetFaceIndices(f).Length <= 4 ? 1 : one.GetFaceIndices(f).Length - 2);
            Assert.Equal(4, expected);
            Assert.Equal(expected, hexagonal.Entities.Count);
            Assert.Equal(one.Area, AreaOf(hexagonal.Entities), 6);

            // Triangles stay triangles.
            GeoMesh3 triangles = new GeoFace3(rectangle).ToMesh(MeshKind.Triangles);
            Dxf flat = Dxf.Of(new DxfWriter().Add(triangles));
            Assert.Equal(triangles.FaceCount, flat.Entities.Count);
            Assert.Equal(2400.0, AreaOf(flat.Entities), 6);
        }

        [Fact]
        [Trait("Step", "7")]
        public void AMeshFaceThatTurnsBothWaysIsWrittenAsItsTrianglesNotAsOneBentFace()
        {
            // A dart: four corners, one of which turns the wrong way. One 3DFACE would be drawn as the wrong pair of triangles.
            var dart = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 10, 0), new GeoPoint3(0, 20, 0), new GeoPoint3(10, 10, 0));
            GeoMesh3 mesh = new GeoFace3(dart).ToMesh(MeshOptions.Grid(100, 100));

            Assert.Equal(4, mesh.GetFaceIndices(0).Length);
            Assert.Equal(1, mesh.FaceCount);

            Dxf dxf = Dxf.Of(new DxfWriter().Add(mesh));
            Assert.Equal(2, dxf.Entities.Count);
            Assert.Equal(dart.Area, AreaOf(dxf.Entities), 6);
        }

        private static double Sag(Record polyline, IReadOnlyList<GeoPoint3> vertices, Func<GeoPoint3, double> distanceToCurve)
        {
            double worst = 0.0;

            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                GeoPoint3 a = vertices[i];
                GeoPoint3 b = vertices[i + 1];
                var middle = new GeoPoint3((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
                worst = Math.Max(worst, distanceToCurve(middle));
            }

            return worst;
        }

        private static List<GeoPoint3> VerticesOf(Dxf dxf)
            => dxf.Entities.Where(e => e.Type == "VERTEX").Select(v => v.Point(0)).ToList();

        [Fact]
        [Trait("Step", "7")]
        public void ACircleInSpaceIsAClosedPolylineWhoseChordsSagNoMoreThanTheToleranceAsked()
        {
            GeoVector3 normal = new GeoVector3(1, 2, 3).Normalize();
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 7), normal, 100);

            foreach (double chord in new[] { 0.1, 0.01, 2.0 })
            {
                Dxf dxf = Dxf.Of(new DxfWriter().Add(circle, chord, "rings"));
                List<GeoPoint3> vertices = VerticesOf(dxf);

                Assert.Equal(9.0, dxf.Entities[0].Number(70));
                Assert.True(vertices.Count >= 3);
                Assert.All(vertices, v => Assert.Equal(100.0, v.DistanceTo(circle.Center), 6));
                Assert.All(vertices, v => Assert.Equal(0.0, circle.Center.GetVectorTo(v).DotProduct(normal), 6));

                // Round the closing chord too: the last vertex back to the first.
                vertices.Add(vertices[0]);
                double sag = Sag(dxf.Entities[0], vertices, m => 100.0 - m.DistanceTo(circle.Center));

                Assert.True(sag <= chord + 1E-9, "The sag " + sag + " is past the tolerance " + chord);
                Assert.True(sag > chord * 0.2, "The chords are no finer than they need be: " + sag + " for " + chord);
            }
        }

        [Fact]
        [Trait("Step", "7")]
        public void AnArcInSpaceIsAnOpenPolylineFromItsStartToItsEndWithinTheToleranceAsked()
        {
            GeoVector3 normal = new GeoVector3(-1, 1, 2).Normalize();
            GeoArc3 arc = new GeoArc3(new GeoPoint3(1, 2, 3), normal, 50, 0.3, 2.9);
            Dxf dxf = Dxf.Of(new DxfWriter().Add(arc, 0.05));
            List<GeoPoint3> vertices = VerticesOf(dxf);

            Assert.Equal(8.0, dxf.Entities[0].Number(70));
            Assert.True(vertices[0].IsEqualTo(arc.StartPoint, Tol));
            Assert.True(vertices[vertices.Count - 1].IsEqualTo(arc.EndPoint, Tol));
            Assert.All(vertices, v => Assert.True(arc.DistanceTo(v, Tol) < 1E-6));
            Assert.True(Sag(dxf.Entities[0], vertices, m => arc.DistanceTo(m, Tol)) <= 0.05 + 1E-9);

            // Clockwise the same: the start is still the start.
            GeoArc3 clockwise = arc.Reverse();
            List<GeoPoint3> back = VerticesOf(Dxf.Of(new DxfWriter().Add(clockwise, 0.05)));
            Assert.True(back[0].IsEqualTo(clockwise.StartPoint, Tol));
            Assert.True(back[back.Count - 1].IsEqualTo(clockwise.EndPoint, Tol));
        }

        [Fact]
        [Trait("Step", "7")]
        public void ACurvedChainAndALoopInSpaceKeepTheirStraightEdgesAndChordTheirArcs()
        {
            // A slot: two straight sides and a half circle at each end, flat on z = 4.
            var arcRight = GeoArc3.FromThreePoints(new GeoPoint3(60, 0, 4), new GeoPoint3(70, 10, 4), new GeoPoint3(60, 20, 4));
            var arcLeft = GeoArc3.FromThreePoints(new GeoPoint3(0, 20, 4), new GeoPoint3(-10, 10, 4), new GeoPoint3(0, 0, 4));
            var edges = new[]
            {
                new GeoEdge3(new GeoPoint3(0, 0, 4), new GeoPoint3(60, 0, 4)),
                new GeoEdge3(arcRight),
                new GeoEdge3(new GeoPoint3(60, 20, 4), new GeoPoint3(0, 20, 4)),
                new GeoEdge3(arcLeft),
            };
            var loop = new GeoPolygonArc3(edges);
            var chain = new GeoPolylineArc3(edges.Take(3));

            Dxf closed = Dxf.Of(new DxfWriter().Add(loop, 0.02, "slot"));
            List<GeoPoint3> ring = VerticesOf(closed);
            Assert.Equal(9.0, closed.Entities[0].Number(70));
            Assert.All(ring, v => Assert.Equal(0.0, loop.DistanceTo(v, Tol), 6));
            Assert.Contains(ring, v => v.IsEqualTo(new GeoPoint3(0, 0, 4), Tol));
            Assert.Contains(ring, v => v.IsEqualTo(new GeoPoint3(60, 20, 4), Tol));

            ring.Add(ring[0]);
            Assert.True(Sag(closed.Entities[0], ring, m => loop.DistanceTo(m, Tol)) <= 0.02 + 1E-9);

            Dxf open = Dxf.Of(new DxfWriter().Add(chain, 0.02));
            List<GeoPoint3> path = VerticesOf(open);
            Assert.Equal(8.0, open.Entities[0].Number(70));
            Assert.True(path[0].IsEqualTo(chain.StartPoint, Tol));
            Assert.True(path[path.Count - 1].IsEqualTo(chain.EndPoint, Tol));
            Assert.True(Sag(open.Entities[0], path, m => chain.DistanceTo(m, Tol)) <= 0.02 + 1E-9);
        }

        [Fact]
        [Trait("Step", "7")]
        public void ATolerancePastWhatACurveCanBeCutToIsRefusedNotGuessed()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 10);
            var arc = new GeoArc3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 10, 0, 1);
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(arc) });

            foreach (double bad in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new DxfWriter().Add(circle, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DxfWriter().Add(arc, bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DxfWriter().Add(chain, bad));
            }

            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolylineArc3)null, 0.1));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolygonArc3)null, 0.1));
        }

        [Fact]
        [Trait("Step", "7")]
        public void EveryHandleStaysUniqueWhenPolylinesAndFacesAreMixed()
        {
            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 10), 0.1)
                .Add(Box(0, 0, 0, 10, 10, 10), "box")
                .Add(new GeoEllipse3(new GeoPoint3(1, 1, 1), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 5, 2)));

            List<string> handles = dxf.Everything().Where(r => r.Has(5) || r.Has(105)).Select(r => r.Handle).ToList();
            Assert.Equal(handles.Count, handles.Distinct().Count());

            AssertOwners(dxf);
        }

        // ===== The review round ======================================================================================

        private static List<string> LayerNames(Dxf dxf)
            => dxf.Records("TABLES").Where(r => r.Type == "LAYER").Select(l => l.Text(2)).ToList();

        private static readonly GeoLine2 Stub = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        private static bool Throws<T>(Action act) where T : Exception
        {
            try
            {
                act();
                return false;
            }
            catch (T)
            {
                return true;
            }
        }

        [Fact]
        [Trait("Step", "R")]
        public void ALayerNameIsCutAt255CharactersBeforeItIsEscapedNeverThroughAnEscape()
        {
            string x250 = new string('x', 250);
            string x254 = new string('x', 254);
            string x255 = new string('x', 255);
            string wide = new string('ệ', 60);

            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(Stub, x250 + "é")
                .Add(Stub, x254 + "é")
                .Add(Stub, x255 + "é")
                .Add(Stub, wide + "tail" + new string('y', 300))
                .Add(Stub, wide));

            List<string> names = LayerNames(dxf);

            Assert.Equal(x250 + "\\U+00E9", names[1]);
            Assert.Equal(x254 + "\\U+00E9", names[2]);
            Assert.Equal(x255, names[3]);
            Assert.Equal(string.Concat(Enumerable.Repeat("\\U+1EC7", 60)) + "tail" + new string('y', 191), names[4]);
            Assert.Equal(string.Concat(Enumerable.Repeat("\\U+1EC7", 60)), names[5]);
        }

        [Fact]
        [Trait("Step", "R")]
        public void AccentedLettersOfDifferentCaseAreOneLayerNamedAsFirstSeen()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub, "Bétón").Add(Stub, "BÉTÓN").Add(Stub, "bétón"));

            Assert.Equal(new[] { "0", "B\\U+00E9t\\U+00F3n" }, LayerNames(dxf));
            Assert.Equal(
                new[] { "B\\U+00E9t\\U+00F3n", "B\\U+00E9t\\U+00F3n", "B\\U+00E9t\\U+00F3n" },
                dxf.Entities.Select(e => e.Layer));
        }

        [Fact]
        [Trait("Step", "R")]
        public void ACommaInALayerNameIsReplacedAsTheOtherForbiddenCharactersAre()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub, "a,b").Add(Stub, "a_b"));

            Assert.Equal(new[] { "0", "a_b" }, LayerNames(dxf));
        }

        [Fact]
        [Trait("Step", "R")]
        public void EveryLayerPointsAtThePlotStylePlaceholderNormalKeptInADictionaryOfTheRoot()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub, "walls").Add(Stub, "slab"));
            List<Record> objects = dxf.Records("OBJECTS");
            Record root = objects[0];

            string styles = root.Pairs.SkipWhile(p => !(p.Code == 3 && p.Value == "ACAD_PLOTSTYLENAME")).Skip(1).First().Value;
            Record dictionary = objects.Single(o => o.Type == "ACDBDICTIONARYWDFLT");

            Assert.Equal(styles, dictionary.Handle);
            Assert.Equal(root.Handle, dictionary.Text(330));
            Assert.Equal("Normal", dictionary.Pairs.SkipWhile(p => p.Code != 3).First().Value);

            Record placeholder = objects.Single(o => o.Type == "ACDBPLACEHOLDER");
            Assert.Equal(placeholder.Handle, dictionary.Text(350));
            Assert.Equal(placeholder.Handle, dictionary.Text(340));
            Assert.Equal(dictionary.Handle, placeholder.Text(330));

            List<Record> layers = dxf.Records("TABLES").Where(r => r.Type == "LAYER").ToList();
            Assert.Equal(3, layers.Count);
            Assert.All(layers, l => Assert.Equal(placeholder.Handle, l.Text(390)));
        }

        [Fact]
        [Trait("Step", "R")]
        public void TheRootDictionaryHoldsTheGroupTheLayoutAndThePlotStyleDictionariesAndNoMore()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub));
            Record root = dxf.Records("OBJECTS")[0];
            string[] names = root.Pairs.Where(p => p.Code == 3).Select(p => p.Value).ToArray();

            Assert.Equal(new[] { "ACAD_GROUP", "ACAD_LAYOUT", "ACAD_PLOTSTYLENAME" }, names);

            var handles = new HashSet<string>(dxf.Records("OBJECTS").Select(o => o.Handle));
            Assert.All(root.Pairs.Where(p => p.Code == 350), p => Assert.Contains(p.Value, handles));
        }

        [Fact]
        [Trait("Step", "R")]
        public void TheClassesSectionFollowsTheHeaderWithTheThreeClassesTheObjectsUse()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub));

            Assert.Equal(new[] { "HEADER", "CLASSES", "TABLES", "BLOCKS", "ENTITIES", "OBJECTS" }, dxf.Sections);

            List<Record> classes = dxf.Records("CLASSES");

            Assert.All(classes, c => Assert.Equal("CLASS", c.Type));
            Assert.Equal(
                new[] { "ACDBDICTIONARYWDFLT", "ACDBPLACEHOLDER", "LAYOUT" },
                classes.Select(c => c.Text(1)));
            Assert.Equal(
                new[] { "AcDbDictionaryWithDefault", "AcDbPlaceHolder", "AcDbLayout" },
                classes.Select(c => c.Text(2)));

            // The groups run 1, 2, 3, 90, 280, 281 as they do in the CLASSES section ezdxf writes.
            Assert.All(classes, c => Assert.Equal(new[] { 1, 2, 3, 90, 280, 281 }, c.Pairs.Select(p => p.Code)));
            Assert.All(classes, c => Assert.Equal("ObjectDBX Classes", c.Text(3)));
            Assert.All(classes, c => Assert.Equal(new[] { 0.0, 0.0, 0.0 }, new[] { c.Number(90), c.Number(280), c.Number(281) }));

            // Every kind of object the OBJECTS section holds that a class is for is one of the three.
            var named = new HashSet<string>(classes.Select(c => c.Text(1)));
            Assert.All(dxf.Records("OBJECTS").Where(o => o.Type != "DICTIONARY"), o => Assert.Contains(o.Type, named));
        }

        [Fact]
        [Trait("Step", "R")]
        public void AnEmptyBoxIsRefusedAsToObbRefusesItAndLeavesNoLayerBehind()
        {
            string before = new DxfWriter().Add(Stub, "keep").ToString();
            var writer = new DxfWriter().Add(Stub, "keep");

            Assert.True(GeoAabb3.Empty.IsEmpty);
            Assert.Throws<InvalidOperationException>(() => writer.Add(GeoAabb3.Empty, "new"));
            Assert.True(before == writer.ToString());
        }

        [Fact]
        [Trait("Step", "R")]
        public void ALayerNameIsTrimmedAgainAfterTheCutSoItDoesNotEndInASpace()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Stub, new string('w', 254) + " tail"));

            Assert.Equal(new string('w', 254), LayerNames(dxf)[1]);
        }

        [Fact]
        [Trait("Step", "R")]
        public void NumbersTheFrameworkFormatterGetsOneStepWrongAreWrittenWithTheDigitsThatReadBackToThem()
        {
            // Texts that read back to these three, which .NET Framework's "R" prints as 4.87215401133157, 3.38241497910694 and
            // -2.16613335402968E-08, other numbers; they are what .NET 10's "R" prints, but that is not what is asked here.
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoPoint3(4.8721540113315704, 3.3824149791069402, -2.1661333540296802E-08)));

            Assert.Equal("4.8721540113315704", dxf.Entities[0].Text(10));
            Assert.Equal("3.3824149791069402", dxf.Entities[0].Text(20));
            Assert.Equal("-2.1661333540296802E-08", dxf.Entities[0].Text(30));
        }

        [Fact]
        [Trait("Step", "R")]
        public void TheViewportGivesItsLowerLeftCornerInTwoGroupsAndIsOpenedOnWhatWasDrawn()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoLine2(new GeoPoint2(100, 200), new GeoPoint2(300, 250))));
            Record port = dxf.Records("TABLES").Single(r => r.Type == "VPORT");

            Assert.Equal(1, port.Pairs.Count(p => p.Code == 10));
            Assert.Equal(1, port.Pairs.Count(p => p.Code == 20));
            Assert.DoesNotContain(port.Pairs, p => p.Code == 30);
            Assert.Equal(200.0, port.Number(12));
            Assert.Equal(225.0, port.Number(22));
            Assert.Equal(200.0 * 1.1, port.Number(40), 9);
        }

        [Fact]
        [Trait("Step", "R")]
        public void AVertexAndTheEndOfAPolylineBelongToThePolylineNotToTheModelSpace()
        {
            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 2), new GeoPoint3(10, 10, 5)))
                .Add(new GeoLine3(GeoPoint3.Origin, new GeoPoint3(1, 1, 1)))
                .Add(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0))));

            AssertOwners(dxf);

            string modelSpace = dxf.Records("TABLES").Single(r => r.Type == "BLOCK_RECORD" && r.Text(2) == "*Model_Space").Handle;
            Record first = dxf.Entities[0];
            Assert.Equal(modelSpace, first.Text(330));
            Assert.Equal(first.Handle, dxf.Entities[1].Text(330));
            Assert.Equal(first.Handle, dxf.Entities[4].Text(330));
        }

        [Fact]
        [Trait("Step", "R")]
        public void ThePlateWithAHoleDrawsItsRimsOnlyAndItsDiagonalsAreHidden()
        {
            var plate = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));
            var slot = new GeoPolygon3(new GeoPoint3(20, 20, 0), new GeoPoint3(80, 20, 0), new GeoPoint3(80, 40, 0), new GeoPoint3(20, 40, 0));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoFace3(plate, new[] { slot }), "plate"));

            // The rims are 320 round the plate and 160 round the slot, each edge once; every other edge is shared by two triangles.
            Assert.Equal(480.0, VisibleLength(dxf.Entities), 6);
            Assert.True(HiddenEdges(dxf.Entities) > 0);
            Assert.Equal(0, HiddenEdges(dxf.Entities) % 2);
        }

        [Fact]
        [Trait("Step", "R")]
        public void ABodyDrawsTheTwelveEdgesOfItsBoxAndHidesTheDiagonalOfEachSide()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(Box(0, 0, 0, 30, 20, 10)));

            Assert.Equal(12, dxf.Entities.Count);

            // Each side draws its own four edges: 2 x (100 + 80 + 60).
            Assert.Equal(480.0, VisibleLength(dxf.Entities), 6);
            Assert.Equal(12, HiddenEdges(dxf.Entities));
        }

        [Fact]
        [Trait("Step", "R")]
        public void TheDiagonalOfAMeshFaceCutIntoTrianglesIsHiddenAndAGridCellKeepsItsFourEdges()
        {
            var dart = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 10, 0), new GeoPoint3(0, 20, 0), new GeoPoint3(10, 10, 0));
            Dxf cut = Dxf.Of(new DxfWriter().Add(new GeoFace3(dart).ToMesh(MeshOptions.Grid(100, 100))));

            Assert.Equal(2, cut.Entities.Count);
            Assert.Equal(2, HiddenEdges(cut.Entities));
            Assert.Equal(dart.Length, VisibleLength(cut.Entities), 6);

            var rectangle = new GeoPolygon3(new GeoPoint3(0, 0, 5), new GeoPoint3(60, 0, 5), new GeoPoint3(60, 40, 5), new GeoPoint3(0, 40, 5));
            Dxf cells = Dxf.Of(new DxfWriter().Add(new GeoFace3(rectangle).ToMesh(MeshOptions.Grid(20, 20))));

            Assert.Equal(6, cells.Entities.Count);
            Assert.All(cells.Entities, c => Assert.Equal(0, Flags(c)));
        }

        [Fact]
        [Trait("Step", "R")]
        public void TrianglesGivenAsTheyAreKeepEveryEdgeVisible()
        {
            var a = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));
            var b = new GeoTriangle3(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(a).Add(new[] { a, b }));

            Assert.Equal(3, dxf.Entities.Count);
            Assert.All(dxf.Entities, f => Assert.Equal(0, Flags(f)));
        }

        [Fact]
        [Trait("Step", "R")]
        public void ABoxIsSixFacesOfFourCornersWrittenStraightFromItsCorners()
        {
            var aabb = new GeoAabb3(new GeoPoint3(10, 20, 30), new GeoPoint3(40, 60, 100));
            Dxf straight = Dxf.Of(new DxfWriter().Add(aabb));
            Dxf turned = Dxf.Of(new DxfWriter().Add(aabb.ToObb()));
            Dxf slanted = Dxf.Of(new DxfWriter().Add(new GeoObb3(new GeoPoint3(5, 6, 7), 30, 40, 70, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0.5))));

            foreach (Dxf dxf in new[] { straight, turned, slanted })
            {
                Assert.Equal(6, dxf.Entities.Count);
                Assert.All(dxf.Entities, f => Assert.Equal("3DFACE", f.Type));
                Assert.All(dxf.Entities, f => Assert.True(f.Point(3).DistanceTo(f.Point(2)) > 1));
                Assert.All(dxf.Entities, f => Assert.Equal(0, Flags(f)));
                Assert.Equal(30.0 * 40 * 70, VolumeUnder(dxf.Entities), 6);
            }

            Assert.Equal(2.0 * (140 + 200 + 220), VisibleLength(straight.Entities), 6);
        }

        [Fact]
        [Trait("Step", "R")]
        public void AFlatBoxIsWrittenAndDoesNotThrow()
        {
            foreach (double thickness in new[] { 0.002, 0.0 })
            {
                var aabb = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, thickness));
                var obb = new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, thickness);

                Dxf straight = Dxf.Of(new DxfWriter().Add(aabb));
                Dxf turned = Dxf.Of(new DxfWriter().Add(obb));

                Assert.Equal(6, straight.Entities.Count);
                Assert.Equal(6, turned.Entities.Count);
                Assert.Equal(100.0 * 100 * thickness, VolumeUnder(straight.Entities), 9);
                Assert.Equal(100.0 * 100 * thickness, VolumeUnder(turned.Entities), 9);
            }
        }

        [Fact]
        [Trait("Step", "R")]
        public void ANumberTheShortestFormatGetsWrongOnFrameworkIsWrittenWithSeventeenDigits()
        {
            const double reviewers = 0.84551240822557006;
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoPoint3(reviewers, -reviewers, 1E-300)).Add(new GeoPoint2(-0.0, 1.5)));

            Assert.Equal(BitConverter.DoubleToInt64Bits(reviewers), BitConverter.DoubleToInt64Bits(dxf.Entities[0].Number(10)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(-reviewers), BitConverter.DoubleToInt64Bits(dxf.Entities[0].Number(20)));
            Assert.Equal(1E-300, dxf.Entities[0].Number(30));
            Assert.Equal("0", dxf.Entities[1].Text(10));

            // And any other number: 5 000 of them from a fixed seed, over many magnitudes, come back to the last bit.
            var random = new Random(20261018);
            var writer = new DxfWriter();
            var wanted = new List<double>();

            for (int i = 0; i < 5000; i++)
            {
                double value = (random.NextDouble() - 0.5) * Math.Pow(10.0, random.Next(-12, 13));
                wanted.Add(value);
                writer.Add(new GeoPoint2(value, 0.0));
            }

            Dxf many = Dxf.Of(writer);

            for (int i = 0; i < wanted.Count; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(wanted[i]), BitConverter.DoubleToInt64Bits(many.Entities[i].Number(10)));
            }
        }

        private static IEnumerable<KeyValuePair<string, Action<DxfWriter>>> Unfit()
        {
            double nan = double.NaN;
            double inf = double.PositiveInfinity;
            var p2 = new GeoPoint2(1, 2);
            var p3 = new GeoPoint3(1, 2, 3);
            var bad2 = new GeoPoint2(nan, 0);
            var bad3 = new GeoPoint3(0, 0, inf);
            GeoVector3 up = new GeoVector3(0, 0, 1);
            GeoVector3 across = new GeoVector3(1, 0, 0);
            GeoTriangle3 good = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0));

            yield return new KeyValuePair<string, Action<DxfWriter>>("point2", w => w.Add(bad2, "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("point3", w => w.Add(bad3, "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("line2", w => w.Add(new GeoLine2(p2, bad2), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("line3", w => w.Add(new GeoLine3(bad3, p3), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("polyline2", w => w.Add(new GeoPolyline2(new GeoPoint2(0, 0), bad2, new GeoPoint2(5, 5)), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("polygon2", w => w.Add(new GeoPolygon2(new GeoPoint2(0, 0), bad2, new GeoPoint2(5, 5)), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("polyline3", w => w.Add(new GeoPolyline3(GeoPoint3.Origin, bad3), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("rectangle", w => w.Add(new GeoRectangle2(bad2, 3, 4), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("triangle2", w => w.Add(new GeoTriangle2(bad2, p2, new GeoPoint2(4, 4)), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("chain with arcs", w => w.Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), bad2 }), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("circle2", w => w.Add(new GeoCircle2(bad2, 3), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("arc2", w => w.Add(new GeoArc2(bad2, 3, 0, 1), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("ellipse2", w => w.Add(new GeoEllipse2(bad2, new GeoVector2(1, 0), 3, 2), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("ray3", w => w.Add(new GeoRay3(new GeoPoint3(nan, 1, 0), new GeoVector3(1, 1, 0)), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("triangle3", w => w.Add(new GeoTriangle3(new GeoPoint3(nan, 0, 0), p3, new GeoPoint3(4, 4, 4)), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("triangles", w => w.Add(new[] { good, new GeoTriangle3(new GeoPoint3(0, nan, 0), p3, new GeoPoint3(4, 4, 4)) }, "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("ellipse3", w => w.Add(new GeoEllipse3(new GeoPoint3(nan, 0, 0), up, across, 3, 2), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("circle3", w => w.Add(new GeoCircle3(new GeoPoint3(nan, 0, 0), up, 3), 0.1, "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("arc3", w => w.Add(new GeoArc3(new GeoPoint3(nan, 0, 0), up, 3, 0, 1), 0.1, "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("aabb", w => w.Add(new GeoAabb3(new GeoPoint3(nan, 0, 0), p3), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("obb", w => w.Add(new GeoObb3(new GeoPoint3(inf, 0, 0), 1, 2, 3), "new"));
            yield return new KeyValuePair<string, Action<DxfWriter>>("face2", w => w.Add(new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), bad2, new GeoPoint2(5, 5))), "new"));
        }

        [Fact]
        [Trait("Step", "R")]
        public void AnyShapeWithANumberThatIsNotFiniteIsRefusedBeforeAnythingIsRecorded()
        {
            string before = new DxfWriter().Add(Stub, "keep").ToString();

            foreach (KeyValuePair<string, Action<DxfWriter>> shape in Unfit())
            {
                var writer = new DxfWriter().Add(Stub, "keep");

                Assert.True(Throws<ArgumentException>(() => shape.Value(writer)), shape.Key + " was not refused");
                Assert.True(before == writer.ToString(), shape.Key + " left something behind");
            }
        }

        [Fact]
        [Trait("Step", "R")]
        public void AnEdgeWithABulgeTooSmallToBowIsReadByItsChordSoTheExtentsStayFinite()
        {
            foreach (double bulge in new[] { 1E-200, 1E-320 })
            {
                var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { bulge });
                var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, new[] { bulge, 0.0, 0.0 });
                Dxf dxf = Dxf.Of(new DxfWriter().Add(chain).Add(loop));

                double[] min = dxf.Header("$EXTMIN").Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)).ToArray();
                double[] max = dxf.Header("$EXTMAX").Select(p => double.Parse(p.Value, CultureInfo.InvariantCulture)).ToArray();

                Assert.All(min.Concat(max), v => Assert.True(!double.IsNaN(v) && !double.IsInfinity(v)));
                Assert.Equal(new[] { 0.0, 0.0, 0.0 }, min);
                Assert.Equal(new[] { 10.0, 10.0, 0.0 }, max);
                Assert.Equal(bulge, dxf.Entities[0].Number(42));
            }
        }

        private static int ChordsFor(double radius, double sweep, double tolerance)
            => (int)Math.Ceiling(Math.Abs(sweep) / (4.0 * Math.Asin(Math.Sqrt(tolerance / (2.0 * radius)))));

        [Fact]
        [Trait("Step", "R")]
        public void ACircleOfFiftyThousandAtAThousandthKeepsItsSagUnderTheToleranceAsked()
        {
            const double radius = 50000.0;
            const double tolerance = 1E-3;
            GeoVector3 normal = new GeoVector3(1, 2, 3).Normalize();
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 7), normal, radius);
            var arc = new GeoArc3(new GeoPoint3(5, 6, 7), normal, radius, 0.3, 2.9);

            Dxf ring = Dxf.Of(new DxfWriter().Add(circle, tolerance));
            List<GeoPoint3> vertices = VerticesOf(ring);

            Assert.Equal(ChordsFor(radius, 2.0 * Math.PI, tolerance), vertices.Count);
            Assert.True(vertices.Count > 4096);
            vertices.Add(vertices[0]);
            Assert.True(Sag(ring.Entities[0], vertices, m => radius - m.DistanceTo(circle.Center)) <= tolerance + 1E-9);

            Dxf part = Dxf.Of(new DxfWriter().Add(arc, tolerance));
            List<GeoPoint3> path = VerticesOf(part);
            Assert.Equal(ChordsFor(radius, arc.SweptAngle, tolerance) + 1, path.Count);
            Assert.True(Sag(part.Entities[0], path, m => radius - m.DistanceTo(arc.Center)) <= tolerance + 1E-9);
        }

        [Fact]
        [Trait("Step", "R")]
        public void AChainIsCutEdgeByEdgeSoEachArcKeepsTheToleranceAtItsOwnRadius()
        {
            // A long shallow arc of radius about 50 000, then a half circle of radius 10.
            GeoArc3 shallow = GeoArc3.FromThreePoints(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0.025, 0), new GeoPoint3(100, 0, 0));
            GeoArc3 tight = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(110, 10, 0), new GeoPoint3(100, 20, 0));
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(shallow), new GeoEdge3(tight) });
            const double tolerance = 1E-3;

            Dxf dxf = Dxf.Of(new DxfWriter().Add(chain, tolerance));
            List<GeoPoint3> path = VerticesOf(dxf);

            int onShallow = 0;
            int onTight = 0;

            for (int i = 0; i + 1 < path.Count; i++)
            {
                GeoPoint3 a = path[i];
                GeoPoint3 b = path[i + 1];
                var middle = new GeoPoint3((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);

                foreach (GeoArc3 arc in new[] { shallow, tight })
                {
                    if (Math.Abs(a.DistanceTo(arc.Center) - arc.Radius) < 1E-6 && Math.Abs(b.DistanceTo(arc.Center) - arc.Radius) < 1E-6)
                    {
                        Assert.True(arc.Radius - middle.DistanceTo(arc.Center) <= tolerance + 1E-9);

                        if (arc.Radius == shallow.Radius)
                        {
                            onShallow++;
                        }
                        else
                        {
                            onTight++;
                        }
                    }
                }
            }

            Assert.Equal(path.Count - 1, onShallow + onTight);
            Assert.True(onShallow >= ChordsFor(shallow.Radius, shallow.SweptAngle, tolerance));
            Assert.True(onTight >= ChordsFor(tight.Radius, tight.SweptAngle, tolerance));
        }

        [Fact]
        [Trait("Step", "R")]
        public void ChordsShorterThanThePointToleranceAreKeptSoACircleOfRadiusOneStaysWithinAHundredMillionth()
        {
            const double radius = 1.0;
            const double tolerance = 1E-8;
            GeoVector3 up = new GeoVector3(0, 0, 1);
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 7), up, radius);
            var arc = new GeoArc3(new GeoPoint3(5, 6, 7), up, 10.0, 0.25, 1.5);

            Dxf ring = Dxf.Of(new DxfWriter().Add(circle, tolerance));
            List<GeoPoint3> vertices = VerticesOf(ring);

            Assert.Equal(ChordsFor(radius, 2.0 * Math.PI, tolerance), vertices.Count);
            vertices.Add(vertices[0]);
            Assert.True(Sag(ring.Entities[0], vertices, m => radius - m.DistanceTo(circle.Center)) <= tolerance + 1E-12);

            List<GeoPoint3> path = VerticesOf(Dxf.Of(new DxfWriter().Add(arc, tolerance)));
            Assert.Equal(ChordsFor(10.0, arc.SweptAngle, tolerance) + 1, path.Count);
            Assert.Equal(arc.StartPoint, path[0]);
            Assert.Equal(arc.EndPoint, path[path.Count - 1]);
        }

        [Fact]
        [Trait("Step", "R")]
        public void ACircleOfRadiusOneHundredAtATenthIsSeventyOneChords()
        {
            Dxf dxf = Dxf.Of(new DxfWriter().Add(new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 100), 0.1));

            Assert.Equal(71, VerticesOf(dxf).Count);
        }

        [Fact]
        [Trait("Step", "R")]
        public void ACurveThatNeedsMoreThanAMillionChordsIsRefusedAndNothingIsRecorded()
        {
            string before = new DxfWriter().Add(Stub, "keep").ToString();
            GeoVector3 up = new GeoVector3(0, 0, 1);
            var circle = new GeoCircle3(GeoPoint3.Origin, up, 1E6);
            var arc = new GeoArc3(GeoPoint3.Origin, up, 1E6, 0, 3);
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(arc) });
            var loop = new GeoPolygonArc3(new[]
            {
                new GeoEdge3(GeoArc3.FromThreePoints(new GeoPoint3(0, 0, 0), new GeoPoint3(1E6, 1E6, 0), new GeoPoint3(2E6, 0, 0))),
                new GeoEdge3(new GeoPoint3(2E6, 0, 0), new GeoPoint3(2E6, -100, 0)),
                new GeoEdge3(new GeoPoint3(2E6, -100, 0), new GeoPoint3(0, -100, 0)),
                new GeoEdge3(new GeoPoint3(0, -100, 0), new GeoPoint3(0, 0, 0)),
            });

            // Thirty edges that each need about 40 000 chords: each is fine, the thirty together are 1.2 million.
            var edges = new List<GeoEdge3>();

            for (int i = 0; i < 30; i++)
            {
                edges.Add(new GeoEdge3(i % 2 == 0 ? arc : arc.Reverse()));
            }

            var long30 = new GeoPolylineArc3(edges);
            const double each = 7E-4;

            int one = Dxf.Of(new DxfWriter().Add(chain, each)).Entities.Count(e => e.Type == "VERTEX") - 1;
            Assert.InRange(one, ChordsFor(1E6, 3, each), ChordsFor(1E6, 3, each) + 1);
            Assert.True(30 * one > 1048576);

            foreach (Action<DxfWriter> act in new Action<DxfWriter>[]
            {
                w => w.Add(circle, 1E-9, "new"),
                w => w.Add(arc, 1E-9, "new"),
                w => w.Add(chain, 1E-9, "new"),
                w => w.Add(loop, 1E-9, "new"),
                w => w.Add(long30, each, "new"),
            })
            {
                var writer = new DxfWriter().Add(Stub, "keep");
                ArgumentOutOfRangeException refused = Assert.ThrowsAny<ArgumentOutOfRangeException>(() => act(writer));

                Assert.Equal("chordTolerance", refused.ParamName);
                Assert.Contains("1048576", refused.Message);
                Assert.True(before == writer.ToString());
            }
        }
    }
}
