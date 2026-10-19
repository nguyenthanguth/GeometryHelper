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

        // ===== Round X3: exact curves in space ==========================================================================

        /// <summary>
        /// The arbitrary axis algorithm of the DXF reference, worked here from the groups of an entity back to the world: the
        /// object coordinate system of an extrusion, its x axis from the y or the z axis of the world by the 1/64 rule.
        /// </summary>
        private sealed class Ocs
        {
            private readonly double[] _x;
            private readonly double[] _y;
            private readonly double[] _z;

            public Ocs(double nx, double ny, double nz)
            {
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                _z = new[] { nx / length, ny / length, nz / length };

                double[] seed = Math.Abs(_z[0]) < 1.0 / 64.0 && Math.Abs(_z[1]) < 1.0 / 64.0 ? new[] { 0.0, 1.0, 0.0 } : new[] { 0.0, 0.0, 1.0 };
                _x = Unit(Cross(seed, _z));
                _y = Unit(Cross(_z, _x));
            }

            /// <summary>The system of an entity: its extrusion, or the world's up where it has none, as DXF reads it.</summary>
            public static Ocs Of(Record entity)
                => entity.Has(210) ? new Ocs(entity.Number(210), entity.Number(220), entity.Number(230)) : new Ocs(0.0, 0.0, 1.0);

            public GeoPoint3 ToWorld(double x, double y, double z)
                => new GeoPoint3(
                    x * _x[0] + y * _y[0] + z * _z[0],
                    x * _x[1] + y * _y[1] + z * _z[1],
                    x * _x[2] + y * _y[2] + z * _z[2]);

            private static double[] Cross(double[] a, double[] b)
                => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

            private static double[] Unit(double[] v)
            {
                double length = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                return new[] { v[0] / length, v[1] / length, v[2] / length };
            }
        }

        /// <summary>
        /// A thousand unit normals: the six axes and the eight corners of the cube, normals a hair off the poles, normals either
        /// side of the 1/64 where the arbitrary axis algorithm changes its seed (never on it to the bit, where the reading
        /// program's own rounding would decide), and from a fixed seed the rest over the sphere.
        /// </summary>
        private static List<GeoVector3> ThousandNormals()
        {
            var list = new List<GeoVector3>();
            double[] signs = { 1.0, -1.0 };
            const double edge = 1.0 / 64.0;

            foreach (double s in signs)
            {
                list.Add(new GeoVector3(0, 0, s));
                list.Add(new GeoVector3(0, s, 0));
                list.Add(new GeoVector3(s, 0, 0));

                foreach (double t in signs)
                {
                    foreach (double u in signs)
                    {
                        list.Add(new GeoVector3(s, t, u));
                    }
                }
            }

            foreach (double tilt in new[] { 1E-12, 1E-9, 1E-6, 1E-3, 0.01 })
            {
                foreach (double s in signs)
                {
                    list.Add(new GeoVector3(tilt, 0, s));
                    list.Add(new GeoVector3(0, tilt, s));
                    list.Add(new GeoVector3(-tilt, tilt, s));
                }
            }

            foreach (double d in new[] { -1E-6, -1E-9, -1E-12, 1E-12, 1E-9, 1E-6 })
            {
                foreach (double s in signs)
                {
                    foreach (double other in new[] { 0.0, 0.005, edge + d, -(edge - d), 0.4 })
                    {
                        // One of the two leaning components sits at the edge, the other is anywhere; the third makes it a unit.
                        double rest = Math.Sqrt(1.0 - (edge + d) * (edge + d) - other * other);
                        list.Add(new GeoVector3(s * (edge + d), other, rest));
                        list.Add(new GeoVector3(other, s * (edge + d), -rest));
                    }
                }
            }

            var random = new Random(20261019);

            while (list.Count < 1000)
            {
                // A height and an angle make a point of the sphere.
                double z = random.NextDouble() * 2.0 - 1.0;
                double angle = random.NextDouble() * Math.PI * 2.0;
                double r = Math.Sqrt(1.0 - z * z);
                list.Add(new GeoVector3(r * Math.Cos(angle), r * Math.Sin(angle), z));
            }

            return list.Take(1000).ToList();
        }

        private static GeoArc3 RandomArc(Random random, GeoVector3 normal, bool clockwise)
        {
            var centre = new GeoPoint3(random.NextDouble() * 2000 - 1000, random.NextDouble() * 2000 - 1000, random.NextDouble() * 2000 - 1000);
            double radius = 0.5 + random.NextDouble() * 500.0;
            double start = random.NextDouble() * Math.PI * 6.0 - Math.PI * 3.0;
            double sweep = 0.05 + random.NextDouble() * (Math.PI * 2.0 - 0.1);

            return new GeoArc3(centre, normal, radius, start, start + (clockwise ? -sweep : sweep), clockwise);
        }

        private static void AssertNear(GeoPoint3 expected, GeoPoint3 actual, double within, string what)
            => Assert.True(expected.DistanceTo(actual) <= within, what + ": " + expected + " against " + actual + ", " + expected.DistanceTo(actual) + " apart");

        /// <summary>The start, the middle and the end of an ARC as the groups of the entity place them, by the reference's own rules.</summary>
        private static GeoPoint3[] WrittenArc(Record arc)
        {
            Ocs ocs = Ocs.Of(arc);
            GeoPoint3 centre = arc.Point(0);
            double radius = arc.Number(40);
            double fromDegrees = arc.Number(50);
            double sweepDegrees = (arc.Number(51) - fromDegrees) % 360.0;

            if (sweepDegrees <= 0.0)
            {
                sweepDegrees += 360.0;
            }

            double from = fromDegrees * Math.PI / 180.0;
            double sweep = sweepDegrees * Math.PI / 180.0;

            GeoPoint3 At(double angle) => ocs.ToWorld(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle), centre.Z);

            return new[] { At(from), At(from + sweep / 2.0), At(from + sweep) };
        }

        /// <summary>The normal turned to point up in its largest component, ties going to z, then y, then x; the extrusion the writer chooses.</summary>
        private static GeoVector3 Upward(GeoVector3 n)
        {
            double x = Math.Abs(n.X);
            double y = Math.Abs(n.Y);
            double z = Math.Abs(n.Z);
            double largest = z >= y && z >= x ? n.Z : y >= x ? n.Y : n.X;

            return largest < 0.0 ? n.Multiply(-1.0) : n;
        }

        /// <summary>The extrusion an entity is written with: the groups, or the world's up where it has none.</summary>
        private static GeoVector3 ExtrusionOf(Record entity)
            => entity.Has(210) ? new GeoVector3(entity.Number(210), entity.Number(220), entity.Number(230)) : new GeoVector3(0, 0, 1);

        /// <summary>
        /// An ARC that draws the arc, about the extrusion that points up in the largest component of the arc's normal: from its start to
        /// its end, or from its end to its start where it turns clockwise about that extrusion.
        /// </summary>
        private static void AssertDrawsTheArc(Record entity, GeoArc3 arc)
        {
            Assert.Equal("ARC", entity.Type);

            GeoVector3 up = Upward(arc.Normal);
            GeoVector3 extrusion = ExtrusionOf(entity);
            Assert.Equal(up.X, extrusion.X, 12);
            Assert.Equal(up.Y, extrusion.Y, 12);
            Assert.Equal(up.Z, extrusion.Z, 12);
            Assert.Equal(arc.Radius, entity.Number(40));

            GeoPoint3[] written = WrittenArc(entity);
            double within = 1E-9 * arc.Radius;

            // Counter-clockwise about the arc's own normal is clockwise about its opposite.
            bool backwards = arc.IsClockwise != (up.DotProduct(arc.Normal) < 0.0);

            AssertNear(backwards ? arc.EndPoint : arc.StartPoint, written[0], within, "the first end");
            AssertNear(arc.MidPoint, written[1], within, "the middle");
            AssertNear(backwards ? arc.StartPoint : arc.EndPoint, written[2], within, "the last end");
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AnArcInSpace_IsAnArcOfItsOwnCoordinateSystem_ForAThousandNormalsEitherWayRound()
        {
            var random = new Random(1019);
            List<GeoVector3> normals = ThousandNormals();
            Assert.Equal(1000, normals.Count);

            foreach (GeoVector3 normal in normals)
            {
                foreach (bool clockwise in new[] { false, true })
                {
                    GeoArc3 arc = RandomArc(random, normal, clockwise);
                    Dxf dxf = Dxf.Of(new DxfWriter().Add(arc, "rings"));

                    Record entity = Assert.Single(dxf.Entities);
                    Assert.Equal("rings", entity.Layer);
                    AssertDrawsTheArc(entity, arc);
                }
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ACircleInSpace_IsACircleOfItsOwnCoordinateSystem_ForAThousandNormals()
        {
            var random = new Random(1020);

            foreach (GeoVector3 normal in ThousandNormals())
            {
                GeoArc3 arc = RandomArc(random, normal, false);
                var circle = new GeoCircle3(arc.Center, normal, arc.Radius);
                Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(circle)).Entities);

                Assert.Equal("CIRCLE", entity.Type);

                GeoVector3 up = Upward(circle.Normal);
                GeoVector3 extrusion = ExtrusionOf(entity);
                Assert.Equal(up.X, extrusion.X, 12);
                Assert.Equal(up.Y, extrusion.Y, 12);
                Assert.Equal(up.Z, extrusion.Z, 12);
                Assert.Equal(circle.Radius, entity.Number(40));

                Ocs ocs = Ocs.Of(entity);
                GeoPoint3 centre = entity.Point(0);
                AssertNear(circle.Center, ocs.ToWorld(centre.X, centre.Y, centre.Z), 1E-9 * circle.Radius, "the centre");

                // A point of the circle by the groups is on the circle: at the radius from the centre, in its plane.
                GeoPoint3 on = ocs.ToWorld(centre.X + circle.Radius * Math.Cos(1.1), centre.Y + circle.Radius * Math.Sin(1.1), centre.Z);
                Assert.True(circle.DistanceTo(on) <= 1E-9 * circle.Radius);
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ACircleOfRadius25StandingOnTheNormalOneOneNought_HasTheOcsCentreWorkedByHand()
        {
            // The normal (1, 1, 0) leans past 1/64 on x, so the x axis is the world z crossed with it, (-0.7071, 0.7071, 0), and the
            // y axis is the normal crossed with that, straight up. The centre (10, 20, 30) is then 7.0711 along x, 30 along y
            // and 21.2132 along the normal.
            double s = Math.Sqrt(0.5);
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25);
            Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(circle, "pipes")).Entities);

            Assert.Equal("CIRCLE", entity.Type);
            Assert.Equal("pipes", entity.Layer);
            Assert.Equal(s, entity.Number(210), 12);
            Assert.Equal(s, entity.Number(220), 12);
            Assert.Equal(0.0, entity.Number(230), 12);
            Assert.Equal(10.0 * s, entity.Number(10), 12);
            Assert.Equal(30.0, entity.Number(20), 12);
            Assert.Equal(30.0 * s, entity.Number(30), 12);
            Assert.Equal(25.0, entity.Number(40));
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AQuarterArcOnTheFloor_IsAnArcFromNoughtToNinetyDegreesAboutUp_AndTheSameTurnedTheOtherWayIsWrittenFromItsEnd()
        {
            var up = new GeoVector3(0, 0, 1);
            double s = Math.Sqrt(0.5) * 10.0;

            GeoArc3 counter = GeoArc3.FromThreePoints(new GeoPoint3(10, 0, 5), new GeoPoint3(s, s, 5), new GeoPoint3(0, 10, 5), Tol);
            Record a = Assert.Single(Dxf.Of(new DxfWriter().Add(counter)).Entities);

            Assert.Equal(up.Z, counter.Normal.Z, 12);
            Assert.Equal("ARC", a.Type);

            // Up is what a CIRCLE or ARC has with no extrusion at all, and none is written.
            Assert.False(a.Has(210));
            Assert.Equal(0.0, a.Number(10), 12);
            Assert.Equal(0.0, a.Number(20), 12);
            Assert.Equal(5.0, a.Number(30), 12);
            Assert.Equal(10.0, a.Number(40), 12);
            Assert.Equal(0.0, a.Number(50), 9);
            Assert.Equal(90.0, a.Number(51), 9);

            // The same quarter walked the other way, from (0, 10) to (10, 0): clockwise about up, it can only be written the other way round.
            GeoArc3 clockwise = counter.Reverse();
            Assert.True(clockwise.IsClockwise);
            Assert.Equal(1.0, clockwise.Normal.Z, 12);
            AssertNear(new GeoPoint3(0, 10, 5), clockwise.StartPoint, 1E-9, "where the clockwise one starts");

            Record b = Assert.Single(Dxf.Of(new DxfWriter().Add(clockwise)).Entities);

            Assert.False(b.Has(210));
            Assert.Equal(0.0, b.Number(50), 9);
            Assert.Equal(90.0, b.Number(51), 9);
            AssertDrawsTheArc(b, clockwise);
        }

        /// <summary>The difference of two angles in degrees, round the circle.</summary>
        private static double AngleApart(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return Math.Min(d, 360.0 - d);
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ACircleOrArcLyingOnTheFloor_IsWrittenAsTheOneInThePlaneIsWhicheverWayUpItsNormalIs()
        {
            // The reviewer's repro: a circle of radius 8 about (100, 50, 0) on the normal (0, 0, -1) is a CIRCLE at 100, 50 with no extrusion.
            string flat = new DxfWriter().Add(new GeoCircle2(new GeoPoint2(100, 50), 8), "c").ToString();

            foreach (double z in new[] { 1.0, -1.0 })
            {
                Assert.Equal(flat, new DxfWriter().Add(new GeoCircle3(new GeoPoint3(100, 50, 0), new GeoVector3(0, 0, z), 8), "c").ToString());
            }

            // And the quarter arc from (110, 50, 5) by (107.07, 42.93, 5), exactly 100 + 10 cos 45 and 50 - 10 sin 45, to (100, 40, 5), whose normal points down: centre (100, 50, 5) and
            // angles about up, 270 to 360, as the GeoArc2 of the same three points gives (a clockwise one, written from its end).
            GeoArc3 down = GeoArc3.FromThreePoints(new GeoPoint3(110, 50, 5), new GeoPoint3(100 + 10 * Math.Sqrt(0.5), 50 - 10 * Math.Sqrt(0.5), 5), new GeoPoint3(100, 40, 5), Tol);
            Assert.True(down.Normal.Z < 0.0);

            Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(down)).Entities);
            Assert.False(entity.Has(210));
            Assert.Equal(100.0, entity.Number(10), 9);
            Assert.Equal(50.0, entity.Number(20), 9);
            Assert.Equal(5.0, entity.Number(30), 9);
            Assert.Equal(270.0, entity.Number(50), 6);
            Assert.True(AngleApart(0.0, entity.Number(51)) < 1E-6);

            // Over 400 arcs of the plane, each as a GeoArc2 and as the GeoArc3 through the same three points, either way round: the
            // same groups in the same order, with the same numbers to a billionth of the radius and the angles to a billionth of a degree.
            var random = new Random(77);

            for (int i = 0; i < 400; i++)
            {
                var centre = new GeoPoint2(random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200);
                double radius = 1 + random.NextDouble() * 200;
                double start = random.NextDouble() * Math.PI * 2.0;
                double sweep = 0.3 + random.NextDouble() * 5.2;
                bool clockwise = i % 2 == 1;
                var arc2 = new GeoArc2(centre, radius, start, start + (clockwise ? -sweep : sweep), clockwise);
                double height = random.NextDouble() * 20 - 10;
                GeoPoint3 Lift(GeoPoint2 p) => new GeoPoint3(p.X, p.Y, height);

                GeoArc3 arc3 = GeoArc3.FromThreePoints(Lift(arc2.StartPoint), Lift(arc2.MidPoint), Lift(arc2.EndPoint), Tol);

                Record a = Assert.Single(Dxf.Of(new DxfWriter().Add(arc2)).Entities);
                Record b = Assert.Single(Dxf.Of(new DxfWriter().Add(arc3)).Entities);

                Assert.Equal(a.Pairs.Select(p => p.Code).ToList(), b.Pairs.Select(p => p.Code).ToList());
                Assert.Equal(a.Number(10), b.Number(10), 9);
                Assert.Equal(a.Number(20), b.Number(20), 9);
                Assert.Equal(height, b.Number(30), 9);
                Assert.Equal(a.Number(40), b.Number(40), 9);
                Assert.True(AngleApart(a.Number(50), b.Number(50)) < 1E-9, "start " + a.Number(50) + " against " + b.Number(50));
                Assert.True(AngleApart(a.Number(51), b.Number(51)) < 1E-9, "end " + a.Number(51) + " against " + b.Number(51));
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AnArcOfAWholeTurnInSpace_IsACircle()
        {
            var arc = new GeoArc3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 2, 3), 7, 0.5, 0.5);
            Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(arc)).Entities);

            Assert.Equal("CIRCLE", entity.Type);
            Assert.Equal(7.0, entity.Number(40));
        }

        // ----- A chain or loop whose arcs and vertices lie in one plane: one lightweight polyline in that plane --------------

        /// <summary>The vertices of a LWPOLYLINE as written: for each its x, its y and its bulge (nought when none is given).</summary>
        private static List<double[]> LightweightVertices(Record polyline)
        {
            var vertices = new List<double[]>();

            foreach (Pair pair in polyline.Pairs)
            {
                double value = pair.Code == 10 || pair.Code == 20 || pair.Code == 42 ? ExportTests.ParseExactly(pair.Value) : 0.0;

                if (pair.Code == 10)
                {
                    vertices.Add(new[] { value, 0.0, 0.0 });
                }
                else if (pair.Code == 20)
                {
                    vertices[vertices.Count - 1][1] = value;
                }
                else if (pair.Code == 42)
                {
                    vertices[vertices.Count - 1][2] = value;
                }
            }

            return vertices;
        }

        /// <summary>The middle of the arc a bulge draws between two points of the plane of the extrusion, by the definition of DXF, in the world.</summary>
        private static GeoPoint3 WrittenMiddle(Ocs ocs, double elevation, double[] from, double[] to)
        {
            GeoPoint2 middle = BulgeMiddle(new GeoPoint2(from[0], from[1]), new GeoPoint2(to[0], to[1]), from[2]);
            return ocs.ToWorld(middle.X, middle.Y, elevation);
        }

        /// <summary>
        /// A chain or loop in a plane with the given normal: a star of five to seven corners, half the edges straight and the rest arcs of
        /// either bulge, about the normal or against it.
        /// </summary>
        private static void PlanarEdges(Random random, GeoVector3 normal, int corners, out GeoPoint3[] vertices, out double[] bulges, out GeoVector3[] normals)
        {
            GeoVector3 u = normal.GetPerpendicularVector();
            GeoVector3 v = normal.CrossProduct(u).Normalize();
            var origin = new GeoPoint3(random.NextDouble() * 1000 - 500, random.NextDouble() * 1000 - 500, random.NextDouble() * 1000 - 500);
            double[] sizes = { 0.3, 1.0, -0.6, 2.0, -1.0, 0.0, 0.0, 0.5 };

            vertices = new GeoPoint3[corners];
            bulges = new double[corners];
            normals = new GeoVector3[corners];

            for (int i = 0; i < corners; i++)
            {
                double angle = Math.PI * 2.0 * i / corners + (random.NextDouble() - 0.5) * 0.3;
                double radius = 40.0 + random.NextDouble() * 60.0;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);

                vertices[i] = origin.Add(u.Multiply(x)).Add(v.Multiply(y));
                bulges[i] = sizes[random.Next(sizes.Length)];

                // The same arc is bulge b about the normal or bulge -b about the opposite one.
                bool turned = random.Next(2) == 0;
                bulges[i] = turned ? -bulges[i] : bulges[i];
                normals[i] = bulges[i] == 0.0 ? default(GeoVector3) : (turned ? normal.Multiply(-1.0) : normal);
            }
        }

        private static void AssertDrawsTheEdges(Record entity, IReadOnlyList<GeoEdge3> edges, bool closed, GeoVector3 plane)
        {
            Assert.Equal("LWPOLYLINE", entity.Type);

            Ocs ocs = Ocs.Of(entity);
            double elevation = entity.Number(38);
            List<double[]> written = LightweightVertices(entity);

            // The extrusion is the normal of the plane, or against it, and the vertices are the plane's own.
            GeoVector3 unit = plane.Normalize();
            double lean = entity.Number(210) * unit.X + entity.Number(220) * unit.Y + entity.Number(230) * unit.Z;
            Assert.Equal(1.0, Math.Abs(lean), 9);

            Assert.Equal(closed ? 1 : 0, (int)entity.Number(70));
            Assert.Equal(edges.Count + (closed ? 0 : 1), written.Count);
            Assert.Equal(written.Count, (int)entity.Number(90));

            double size = 100.0;

            for (int i = 0; i < edges.Count; i++)
            {
                AssertNear(edges[i].StartPoint, ocs.ToWorld(written[i][0], written[i][1], elevation), 1E-9 * size, "vertex " + i);

                double[] next = written[(i + 1) % written.Count];
                AssertNear(edges[i].EndPoint, ocs.ToWorld(next[0], next[1], elevation), 1E-9 * size, "vertex " + (i + 1));

                if (edges[i].IsArc)
                {
                    Assert.NotEqual(0.0, written[i][2]);
                    AssertNear(edges[i].MidPoint, WrittenMiddle(ocs, elevation, written[i], next), 1E-9 * size, "the middle of edge " + i);
                }
                else
                {
                    Assert.Equal(0.0, written[i][2]);
                }
            }

            if (!closed)
            {
                Assert.Equal(0.0, written[written.Count - 1][2]);
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void APlanarChainWithArcs_IsOneLightweightPolylineInItsPlane_ForAThousandPlanes()
        {
            var random = new Random(1021);

            foreach (GeoVector3 normal in ThousandNormals())
            {
                PlanarEdges(random, normal, 5 + random.Next(3), out GeoPoint3[] vertices, out double[] bulges, out GeoVector3[] normals);

                // An open chain keeps one fewer bulge than it has vertices.
                bulges[bulges.Length - 1] = 0.0;
                normals[normals.Length - 1] = default(GeoVector3);

                var chain = new GeoPolylineArc3(vertices, bulges, normals, Tol);
                Assert.True(chain.IsPlanar(Tol));

                Dxf dxf = Dxf.Of(new DxfWriter().Add(chain, Tol, "bars"));
                Record entity = Assert.Single(dxf.Entities);

                Assert.Equal("bars", entity.Layer);
                AssertDrawsTheEdges(entity, chain.GetEdges().ToList(), false, normal);
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void APlanarLoopWithArcs_IsOneClosedLightweightPolylineInItsPlane_ForAThousandPlanes()
        {
            var random = new Random(1022);

            foreach (GeoVector3 normal in ThousandNormals())
            {
                PlanarEdges(random, normal, 5 + random.Next(3), out GeoPoint3[] vertices, out double[] bulges, out GeoVector3[] normals);

                var loop = new GeoPolygonArc3(vertices, bulges, normals, Tol);
                Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(loop, "slots")).Entities);

                Assert.Equal("slots", entity.Layer);
                AssertDrawsTheEdges(entity, loop.GetEdges().ToList(), true, normal);
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ASlotEightyByTwentyInAPlaneTiltedThirtyDegrees_IsOneClosedPolylineOfFourVerticesAndTwoHalfCircles()
        {
            // The slot lies in the plane through the origin standing on (0, -sin 30, cos 30): x along the world x, y up the slope.
            // 80 long overall, with straight sides of 60 and a half circle of radius 10 at each end. Written as chords at 0.01 it is 74
            // segments; as itself it is four vertices and two bulges of 1.
            double sin = Math.Sin(Math.PI / 6.0);
            double cos = Math.Cos(Math.PI / 6.0);
            var normal = new GeoVector3(0, -sin, cos);
            GeoPoint3 At(double x, double y) => new GeoPoint3(x, y * cos, y * sin);

            var loop = new GeoPolygonArc3(
                new[] { At(0, 0), At(60, 0), At(60, 20), At(0, 20) },
                new[] { 0.0, 1.0, 0.0, 1.0 },
                new[] { default(GeoVector3), normal, default(GeoVector3), normal },
                Tol);

            Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(loop, "slot")).Entities);
            List<double[]> vertices = LightweightVertices(entity);

            Assert.Equal("LWPOLYLINE", entity.Type);
            Assert.Equal(1.0, entity.Number(70));
            Assert.Equal(4, vertices.Count);
            Assert.Equal(2, entity.Pairs.Count(p => p.Code == 42));
            Assert.Equal(0.0, entity.Number(38), 9);
            Assert.Equal(0.0, entity.Number(210), 9);
            Assert.Equal(-sin, entity.Number(220), 9);
            Assert.Equal(cos, entity.Number(230), 9);

            // In its own plane the corners are at x 0 or 60 and y 0 or 20, however the slope is turned: the vertices go as the loop
            // does, and the plane's own x axis is the world's.
            Assert.Equal(new[] { 0.0, 60.0, 60.0, 0.0 }, vertices.Select(v => v[0]).Select(x => Math.Round(x, 9)).ToArray());
            Assert.Equal(new[] { 0.0, 1.0, 0.0, 1.0 }, vertices.Select(v => Math.Round(Math.Abs(v[2]), 9)).ToArray());
            Assert.Equal(20.0, Math.Abs(vertices[2][1] - vertices[1][1]), 9);
        }

        // ----- A chain that does not lie in one plane: its edges, one entity each -----------------------------------------

        private static GeoPolylineArc3 BentBar(Random random)
        {
            GeoPoint3 at = new GeoPoint3(random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100);
            var vertices = new List<GeoPoint3> { at };
            var bulges = new List<double>();
            var normals = new List<GeoVector3>();

            for (int i = 0; i < 5; i++)
            {
                var step = new GeoVector3(random.NextDouble() * 100 - 50, random.NextDouble() * 100 - 50, random.NextDouble() * 100 - 50);
                GeoPoint3 next = vertices[i].Add(step);

                // Every second edge is straight; an arc bows in a plane the chord lies in, with a normal square to it.
                if (i % 2 == 1)
                {
                    bulges.Add(0.0);
                    normals.Add(default(GeoVector3));
                }
                else
                {
                    var any = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
                    bulges.Add((random.NextDouble() * 1.9 + 0.1) * (random.Next(2) == 0 ? -1.0 : 1.0));
                    normals.Add(step.CrossProduct(any).Normalize());
                }

                vertices.Add(next);
            }

            bulges.Add(0.0);
            normals.Add(default(GeoVector3));
            return new GeoPolylineArc3(vertices, bulges, normals, Tol);
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AChainThatBendsOutOfOnePlane_IsALineOrAnArcForEachEdge_InOrder()
        {
            var random = new Random(1023);

            for (int run = 0; run < 300; run++)
            {
                GeoPolylineArc3 chain = BentBar(random);
                Assert.False(chain.IsPlanar(Tol));

                List<GeoEdge3> edges = chain.GetEdges().ToList();
                Dxf dxf = Dxf.Of(new DxfWriter().Add(chain, Tol, "bars"));

                Assert.Equal(edges.Count, dxf.Entities.Count);
                Assert.All(dxf.Entities, e => Assert.Equal("bars", e.Layer));

                for (int i = 0; i < edges.Count; i++)
                {
                    Record entity = dxf.Entities[i];

                    if (!edges[i].IsArc)
                    {
                        Assert.Equal("LINE", entity.Type);
                        Assert.Equal(edges[i].StartPoint, entity.Point(0));
                        Assert.Equal(edges[i].EndPoint, entity.Point(1));
                        continue;
                    }

                    GeoArc3 arc = edges[i].ToArc();
                    AssertDrawsTheArc(entity, arc);
                }
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ABarWithAnArcInEachOfTwoPlanes_IsTwoArcsEachInItsOwnCoordinateSystemAndTheLinesBetween()
        {
            // A bar 100 long, a quarter bend of radius 20 up, 80 on, a quarter bend of radius 20 to the side, and 60 on.
            GeoPoint3 a = new GeoPoint3(0, 0, 0);
            GeoPoint3 b = new GeoPoint3(100, 0, 0);
            GeoArc3 first = GeoArc3.FromThreePoints(b, new GeoPoint3(100 + 20 * Math.Sin(Math.PI / 4), 0, 20 - 20 * Math.Cos(Math.PI / 4)), new GeoPoint3(120, 0, 20), Tol);
            GeoPoint3 c = new GeoPoint3(120, 0, 100);
            GeoArc3 second = GeoArc3.FromThreePoints(c, new GeoPoint3(120 + 20 * (1 - Math.Cos(Math.PI / 4)), 20 * Math.Sin(Math.PI / 4), 100), new GeoPoint3(140, 20, 100), Tol);

            var bar = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(a, b),
                new GeoEdge3(first),
                new GeoEdge3(first.EndPoint, c),
                new GeoEdge3(second),
                new GeoEdge3(second.EndPoint, new GeoPoint3(140, 80, 100)),
            });

            Assert.False(bar.IsPlanar(Tol));
            Dxf dxf = Dxf.Of(new DxfWriter().Add(bar, Tol));

            Assert.Equal(new[] { "LINE", "ARC", "LINE", "ARC", "LINE" }, dxf.Entities.Select(e => e.Type).ToArray());
            Assert.Equal(20.0, dxf.Entities[1].Number(40), 9);
            Assert.Equal(20.0, dxf.Entities[3].Number(40), 9);

            // The two bends are in different planes, so their extrusions differ: one about y, the other about z.
            Assert.Equal(1.0, Math.Abs(ExtrusionOf(dxf.Entities[1]).Y), 9);
            Assert.Equal(1.0, Math.Abs(ExtrusionOf(dxf.Entities[3]).Z), 9);
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AChainOffItsPlaneByLessThanTheToleranceGivenIsOnePolyline_AndByMoreIsItsEdges()
        {
            // Four corners of a square 100 wide with the last one 0.02 off the plane of the others, and a bend on the first edge.
            var up = new GeoVector3(0, 0, 1);
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0.02) },
                new[] { 0.4, 0.0, 0.0, 0.0 },
                new[] { up, default(GeoVector3), default(GeoVector3), default(GeoVector3) },
                new Tolerance(1E-3, 1E-5, Math.PI / 180.0, 0.05));

            var strict = new Tolerance(1E-3, 1E-5, Math.PI / 180.0, 1E-3);
            var loose = new Tolerance(1E-3, 1E-5, Math.PI / 180.0, 0.05);

            Assert.Equal(new[] { "ARC", "LINE", "LINE" }, Dxf.Of(new DxfWriter().Add(chain, strict)).Entities.Select(e => e.Type).ToArray());
            Assert.Equal(new[] { "LWPOLYLINE" }, Dxf.Of(new DxfWriter().Add(chain, loose)).Entities.Select(e => e.Type).ToArray());
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AChainOnTheFloor_IsWrittenWithTheExtrusionUpAndItsPlainCoordinates_AndTurnedDownItIsTheSame()
        {
            // The plane of a chain has no sign of its own: the extrusion is turned to point up in the largest of its components, so
            // a chain lying on the floor is written with plain x and y and an elevation, whichever way its arcs turn.
            var up = new GeoVector3(0, 0, 1);
            var down = new GeoVector3(0, 0, -1);
            var points = new[] { new GeoPoint3(0, 0, 7), new GeoPoint3(50, 0, 7), new GeoPoint3(50, 30, 7) };

            foreach (GeoVector3 about in new[] { up, down })
            {
                var chain = new GeoPolylineArc3(points, new[] { 0.0, 0.5, 0.0 }, new[] { default(GeoVector3), about, default(GeoVector3) }, Tol);
                Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(chain, Tol)).Entities);
                List<double[]> vertices = LightweightVertices(entity);

                Assert.Equal(0.0, entity.Number(210));
                Assert.Equal(0.0, entity.Number(220));
                Assert.Equal(1.0, entity.Number(230));
                Assert.Equal(7.0, entity.Number(38));
                Assert.Equal(new[] { 0.0, 50.0, 50.0 }, vertices.Select(v => v[0]).ToArray());
                Assert.Equal(new[] { 0.0, 0.0, 30.0 }, vertices.Select(v => v[1]).ToArray());

                // About up the bulge is as it was given, and about down its sign is the other.
                Assert.Equal(about.Z > 0.0 ? 0.5 : -0.5, vertices[1][2]);
            }
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AnArcBowingSquareToThePlaneItStandsOffByLessThanTheTolerance_IsWrittenStraight()
        {
            // The plane is z = 0 (the chain's third vertex settles it). The first edge, 100 along x, bows 1.8E-5 * 50 = 0.0009 up out of it,
            // about the normal (0, 1, 0), which is 0.0009 off the plane at the middle: inside the planar tolerance of 1E-3.
            var across = new GeoVector3(0, 1, 0);
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                new[] { 1.8E-5, 0.0, 0.0 },
                new[] { across, default(GeoVector3), default(GeoVector3) },
                Tol);
            Assert.True(chain.IsPlanar(Tol));

            Record entity = Assert.Single(Dxf.Of(new DxfWriter().Add(chain, Tol)).Entities);
            List<double[]> vertices = LightweightVertices(entity);

            // Written as it bows in the plane, which is not at all: the middle is the chord's, and is within the tolerance of the arc's.
            Assert.Equal("LWPOLYLINE", entity.Type);
            Assert.Equal(0.0, vertices[0][2]);
            GeoPoint3 middle = WrittenMiddle(Ocs.Of(entity), entity.Number(38), vertices[0], vertices[1]);
            Assert.True(middle.DistanceTo(chain.GetEdgeAt(0).MidPoint) <= Tol.EqualPoint, "the middle is " + middle.DistanceTo(chain.GetEdgeAt(0).MidPoint) + " from the arc's");

            // An arc whose normal is 30 degrees off the plane's: its bow in the plane is its sag times the cosine, 0.00075 * 0.866.
            var tilted = new GeoVector3(0, 0.5, Math.Sqrt(0.75));
            var slanted = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                new[] { 1.5E-5, 0.0, 0.0 },
                new[] { tilted, default(GeoVector3), default(GeoVector3) },
                Tol);
            Assert.True(slanted.IsPlanar(Tol));

            Record e2 = Assert.Single(Dxf.Of(new DxfWriter().Add(slanted, Tol)).Entities);
            List<double[]> v2 = LightweightVertices(e2);
            GeoPoint3 arcMiddle = slanted.GetEdgeAt(0).MidPoint;
            GeoPoint3 written = WrittenMiddle(Ocs.Of(e2), e2.Number(38), v2[0], v2[1]);

            Assert.Equal(1.5E-5 * Math.Sqrt(0.75), v2[0][2], 12);
            Assert.Equal(arcMiddle.X, written.X, 9);
            Assert.Equal(arcMiddle.Y, written.Y, 9);
            Assert.Equal(0.0, written.Z, 9);
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AChainOfNoArcWhoseVerticesLieOnOneLine_IsALineForEachEdge()
        {
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 20, 30), new GeoPoint3(20, 40, 60) });
            Dxf dxf = Dxf.Of(new DxfWriter().Add(chain));

            Assert.Equal(new[] { "LINE", "LINE" }, dxf.Entities.Select(e => e.Type).ToArray());
            Assert.Equal(new GeoPoint3(10, 20, 30), dxf.Entities[0].Point(1));
            Assert.Equal(new GeoPoint3(20, 40, 60), dxf.Entities[1].Point(1));
        }

        [Fact]
        [Trait("Step", "X3")]
        public void AnArcOrCircleWithNoRadiusOrNoNormal_IsRefusedBeforeAnythingIsRecorded()
        {
            string before = new DxfWriter().Add(Stub, "keep").ToString();
            var circle = new DxfWriter().Add(Stub, "keep");
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Add(default(GeoCircle3), "new"));
            Assert.True(before == circle.ToString());

            var arc = new DxfWriter().Add(Stub, "keep");
            Assert.ThrowsAny<ArgumentException>(() => arc.Add(default(GeoArc3), "new"));
            Assert.True(before == arc.ToString());
        }

        // ----- Arguments, extents and handles ------------------------------------------------------------------------------

        [Fact]
        [Trait("Step", "X3")]
        public void ANullChainOrLoopIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolylineArc3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((GeoPolygonArc3)null));
        }

        [Fact]
        [Trait("Step", "X3")]
        public void ASpaceCurveWithANumberThatIsNotFiniteIsRefusedBeforeAnythingIsRecorded()
        {
            double nan = double.NaN;
            GeoVector3 up = new GeoVector3(0, 0, 1);
            string before = new DxfWriter().Add(Stub, "keep").ToString();

            var shapes = new Dictionary<string, Action<DxfWriter>>
            {
                { "arc", w => w.Add(new GeoArc3(new GeoPoint3(nan, 0, 0), up, 3, 0, 1), "new") },
                { "circle", w => w.Add(new GeoCircle3(new GeoPoint3(0, nan, 0), up, 3), "new") },
                { "chain", w => w.Add(new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, nan) }), "new") },
            };

            foreach (KeyValuePair<string, Action<DxfWriter>> shape in shapes)
            {
                var writer = new DxfWriter().Add(Stub, "keep");

                Assert.True(Throws<ArgumentException>(() => shape.Value(writer)), shape.Key + " was not refused");
                Assert.True(before == writer.ToString(), shape.Key + " left something behind");
            }
        }

        private static double[] Extent(Dxf dxf, string variable)
            => dxf.Header(variable).Select(p => ExportTests.ParseExactly(p.Value)).ToArray();

        [Fact]
        [Trait("Step", "X3")]
        public void TheExtentsFollowTheCurvesInSpaceNotTheirCirclesNorTheirChords()
        {
            // A circle of radius 25 about (10, 20, 30) on the normal (1, 1, 0): it reaches 25 sqrt(1 - 1/2) = 17.68 in x and in y, 25 in z.
            double reach = 25.0 * Math.Sqrt(0.5);
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25);
            Dxf whole = Dxf.Of(new DxfWriter().Add(circle));

            Assert.Equal(new[] { 10 - reach, 20 - reach, 5.0 }, Extent(whole, "$EXTMIN"), new Near(1E-9));
            Assert.Equal(new[] { 10 + reach, 20 + reach, 55.0 }, Extent(whole, "$EXTMAX"), new Near(1E-9));

            // A quarter of a circle of radius 10 on the floor, from (10, 0) to (0, 10): the box of its ends and the point of it
            // that reaches furthest, (7.07, 7.07), which is no further than the ends.
            GeoArc3 quarter = GeoArc3.FromThreePoints(new GeoPoint3(10, 0, 5), new GeoPoint3(Math.Sqrt(0.5) * 10, Math.Sqrt(0.5) * 10, 5), new GeoPoint3(0, 10, 5), Tol);
            Dxf part = Dxf.Of(new DxfWriter().Add(quarter));

            Assert.Equal(new[] { 0.0, 0.0, 5.0 }, Extent(part, "$EXTMIN"), new Near(1E-9));
            Assert.Equal(new[] { 10.0, 10.0, 5.0 }, Extent(part, "$EXTMAX"), new Near(1E-9));

            // A half circle bowing past its ends: from (0, 0) to (20, 0) over the top of y = 10 in the plane x-y at height 2.
            var half = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 2), new GeoPoint3(20, 0, 2) }, new[] { -1.0, 0.0 }, new[] { new GeoVector3(0, 0, 1), default(GeoVector3) }, Tol);
            Dxf bow = Dxf.Of(new DxfWriter().Add(half, Tol));

            Assert.Equal(new[] { 0.0, 0.0, 2.0 }, Extent(bow, "$EXTMIN"), new Near(1E-9));
            Assert.Equal(new[] { 20.0, 10.0, 2.0 }, Extent(bow, "$EXTMAX"), new Near(1E-9));
        }

        private sealed class Near : IEqualityComparer<double>
        {
            private readonly double _within;

            public Near(double within) => _within = within;

            public bool Equals(double x, double y) => Math.Abs(x - y) <= _within;

            public int GetHashCode(double value) => 0;
        }

        [Fact]
        [Trait("Step", "X3")]
        public void EveryHandleStaysUniqueWhenTheExactCurvesInSpaceAreMixedWithTheRest()
        {
            var up = new GeoVector3(0, 0, 1);
            var bar = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 5) },
                new[] { 0.5, 0.0, 0.0 },
                new[] { up, default(GeoVector3), default(GeoVector3) },
                Tol);

            Dxf dxf = Dxf.Of(new DxfWriter()
                .Add(new GeoCircle3(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 10), "a")
                .Add(new GeoArc3(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 10, 0, 2), "b")
                .Add(Box(0, 0, 0, 10, 10, 10), "box")
                .Add(bar, Tol, "c")
                .Add(new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 1), new GeoPoint3(10, 0, 1), new GeoPoint3(10, 10, 1) }), "d"));

            List<string> handles = dxf.Everything().Where(r => r.Has(5) || r.Has(105)).Select(r => r.Handle).ToList();
            Assert.Equal(handles.Count, handles.Distinct().Count());

            AssertOwners(dxf);
            Assert.Equal(new[] { "0", "a", "b", "box", "c", "d" }, LayerNames(dxf).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        [Trait("Step", "X3")]
        public void TheChordCountsTheDocumentationQuotesAgainstTheExactCurves_AreWhatTheChordOverloadsGive()
        {
            // A circle of radius 25 at 0.1 is 36 chords; a slot 80 by 20 (straight sides of 60) with half circles at its ends, at 0.01, is 74 (36 for each half and 2 sides).
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25);
            Assert.Equal(36, VerticesOf(Dxf.Of(new DxfWriter().Add(circle, 0.1))).Count);

            var up = new GeoVector3(0, 0, 1);
            var slot = new GeoPolygonArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0), new GeoPoint3(60, 20, 0), new GeoPoint3(0, 20, 0) },
                new[] { 0.0, 1.0, 0.0, 1.0 },
                new[] { default(GeoVector3), up, default(GeoVector3), up },
                Tol);
            Assert.Equal(74, VerticesOf(Dxf.Of(new DxfWriter().Add(slot, 0.01))).Count);
        }

        [Fact]
        [Trait("Step", "X3")]
        public void TheChordOverloadsAreWhatTheyWere_AndTheExactOnesAreNotTheirChords()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 100);

            Dxf chords = Dxf.Of(new DxfWriter().Add(circle, 0.1));
            Dxf exact = Dxf.Of(new DxfWriter().Add(circle));

            Assert.Equal("POLYLINE", chords.Entities[0].Type);
            Assert.Equal(new[] { "CIRCLE" }, exact.Entities.Select(e => e.Type).ToArray());
        }
    }
}
