using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What a drawing saved as R12 (AC1009) holds as AutoCAD 2021 writes it, found by probing its console (Database.DxfOut of
    /// side databases at R12 beside their R2018): extended data with the names of its applications and layers in capitals and
    /// its points of 1011 to 1013 in five groups, and the widths of a lightweight polyline as the defaults of its header.
    /// </summary>
    public class DxfSaveR12FollowUp4Tests
    {
        private static DxfDatabase Drawing() => new DxfDatabase("AC1032");

        private static string R12(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                return Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }
        }

        private sealed class Record
        {
            internal Record(string type)
            {
                Type = type;
                Groups = new List<KeyValuePair<int, string>>();
            }

            internal string Type { get; }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal string First(int code) => Groups.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();

            internal double Number(int code) => double.Parse(First(code), CultureInfo.InvariantCulture);

            /// <summary>The groups of the extended data, as <c>code=value</c>.</summary>
            internal string[] Xdata() =>
                Groups.Where(g => g.Key >= 1000).Select(g => g.Key + "=" + g.Value.Trim()).ToArray();
        }

        /// <summary>The records of the ENTITIES section: a record runs from its group 0 to the next.</summary>
        private static List<Record> Entities(string text) => Section(text, "ENTITIES");

        /// <summary>The records of a section.</summary>
        private static List<Record> Section(string text, string name)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var records = new List<Record>();
            bool inside = false;
            Record record = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    inside = lines[i + 3] == name;
                    record = null;
                    i += 2;
                }
                else if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    inside = false;
                    record = null;
                }
                else if (inside && code == 0)
                {
                    record = new Record(value);
                    records.Add(record);
                }
                else if (inside)
                {
                    record?.Groups.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            return records;
        }

        // ---- extended data --------------------------------------------------------------------------------------------------------

        [Fact]
        public void TheExtendedDataOfALine_HasItsApplicationsAndLayersInCapitals_AndItsWorldPointsInFiveGroups()
        {
            DxfDatabase database = Drawing();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "a{b}" });
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            database.ModelSpace.AppendEntity(line);
            line.XData = new[]
            {
                new DxfTypedValue(1001, "gh_Mixed"),
                new DxfTypedValue(1003, "a{b}"),
                new DxfTypedValue(1010, "1"),
                new DxfTypedValue(1020, "2"),
                new DxfTypedValue(1030, "3"),
                new DxfTypedValue(1011, "4"),
                new DxfTypedValue(1021, "5"),
                new DxfTypedValue(1031, "6"),
                new DxfTypedValue(1012, "7"),
                new DxfTypedValue(1022, "8"),
                new DxfTypedValue(1032, "9"),
                new DxfTypedValue(1013, "0"),
                new DxfTypedValue(1023, "0"),
                new DxfTypedValue(1033, "1"),
                new DxfTypedValue(1070, "7"),
            };

            Record saved = Entities(R12(database)).Single(r => r.Type == "LINE");

            // AutoCAD 2021's own R12: GH_MIXED, the layer as the table of layers writes it (A_B_), a point of 1010 in its three
            // groups, and each of 1011 to 1013 as x, then 0.0 and 0.0, then y and z.
            Assert.Equal(
                new[]
                {
                    "1001=GH_MIXED",
                    "1003=A_B_",
                    "1010=1",
                    "1020=2",
                    "1030=3",
                    "1011=4",
                    "1021=0.0",
                    "1031=0.0",
                    "1021=5",
                    "1031=6",
                    "1012=7",
                    "1022=0.0",
                    "1032=0.0",
                    "1022=8",
                    "1032=9",
                    "1013=0",
                    "1023=0.0",
                    "1033=0.0",
                    "1023=0",
                    "1033=1",
                    "1070=7",
                },
                saved.Xdata()
            );
        }

        // ---- the pattern of a hatch in a tilted plane -----------------------------------------------------------------------------

        private static DxfHatch Pattern(DxfDatabase database, GeoVector3 normal)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(0.0, 0.0, 0.0, 0.0, 2.0, null) });
            hatch.Normal = normal;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 10), new GeoPoint2(0, 10) },
                new double[4]
            );
            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        /// <summary>The LINEs a hatch is broken into, in the block of the INSERT that stands for it.</summary>
        private static List<Record> Lines(DxfDatabase database, DxfHatch hatch)
        {
            string text = R12(database);
            Record insert = Entities(text).Single(r => r.Type == "INSERT" && r.First(5) == hatch.Handle);
            List<Record> blocks = Section(text, "BLOCKS");
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            return blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").Where(r => r.Type == "LINE").ToList();
        }

        [Fact]
        public void TheLinesOfAPatternInATiltedPlane_CarryTheNormalOfTheHatch()
        {
            DxfDatabase database = Drawing();
            double root = 1.0 / Math.Sqrt(2.0);
            DxfHatch hatch = Pattern(database, new GeoVector3(root, 0, root));

            // AutoCAD 2021: each LINE of ANSI31 on the plane with the normal (0.707, 0, 0.707) has it as its extrusion.
            List<Record> lines = Lines(database, hatch);
            Assert.True(lines.Count > 3, "the pattern has lines");
            Assert.All(lines, r =>
            {
                Assert.Equal(root, r.Number(210), 6);
                Assert.Equal(0.0, r.Number(220), 6);
                Assert.Equal(root, r.Number(230), 6);
            });
        }

        [Fact]
        public void TheLinesOfAPatternInThePlaneOfTheDrawing_CarryNoNormal()
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Pattern(database, new GeoVector3(0, 0, 1));

            List<Record> lines = Lines(database, hatch);
            Assert.True(lines.Count > 3, "the pattern has lines");
            Assert.All(lines, r => Assert.Null(r.First(210)));
        }

        // ---- a round fill ---------------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(5.0, 62, 64)]
        [InlineData(20.0, 62, 64)]
        [InlineData(23.0, 32, 32)]
        [InlineData(25.0, 34, 36)]
        [InlineData(30.0, 74, 76)]
        [InlineData(40.0, 86, 88)]
        [InlineData(50.0, 48, 48)]
        [InlineData(100.0, 138, 140)]
        [InlineData(200.0, 98, 100)]
        public void ACircleFilledSolid_IsCutIntoTheChordsAutoCadCutsIt(double radius, int solids, int lines)
        {
            DxfDatabase database = Drawing();
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                new DxfHatchLoop(
                    DxfHatchLoopTypes.External,
                    new object[] { new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), radius) }
                )
            );
            database.ModelSpace.AppendEntity(hatch);

            // AutoCAD 2021's own R12 of circles of 5 to 200 units: the circle is a polygon of the fewest chords that stand
            // 0.1 off it at the most, 33 at the least (36 for a radius of 25, 71 for one of 100), whose strips are the SOLIDs
            // and whose slanted sides are the LINEs. A polygon of an even count has its sides at the poles left out.
            string text = R12(database);
            Record insert = Entities(text).Single(r => r.Type == "INSERT" && r.First(5) == hatch.Handle);
            List<Record> blocks = Section(text, "BLOCKS");
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            List<Record> pieces = blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
            Assert.Equal(solids, pieces.Count(r => r.Type == "SOLID"));
            Assert.Equal(lines, pieces.Count(r => r.Type == "LINE"));
        }

        // ---- the flags of an attribute --------------------------------------------------------------------------------------------

        private static string[] ApplicationsNamed(bool locked)
        {
            DxfDatabase database = Drawing();
            var tagged = new DxfBlockTableRecord { Name = "Tagged" };
            database.BlockTable.Add(tagged);
            var definition = new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "N1", "NOTE", "Note", null);
            tagged.AppendEntity(definition);
            definition.LockPositionInBlock = locked;

            // R2010 writes the version of an attribute (280) before its tag, and then whether its place is locked.
            string written = Regex.Replace(database.ToText(), @"(AcDbAttributeDefinition\r?\n)", "${1}280\n0\n");

            // AutoCAD writes the flag of the place where it is free too, as the 280 after the flags of the attribute.
            if (!locked)
            {
                written = Regex.Replace(written, @"(\n  2\r?\nNOTE\r?\n 70\r?\n0\r?\n)", "${1}280\n0\n");
            }

            string text = R12(DxfDatabase.Parse(written));
            return Section(text, "BLOCKS")
                .Where(r => r.Type == "ATTDEF")
                .SelectMany(r => r.Groups.Where(g => g.Key == 1001).Select(g => g.Value))
                .ToArray();
        }

        [Fact]
        public void AnAttributeOfVersionNoughtWhosePlaceIsFree_HasNoExtendedData_AsAutoCadWritesIt()
        {
            // AutoCAD 2021's own R12 of an attribute definition made with its place free has no AcDbAttr (a locked one has it).
            Assert.Empty(ApplicationsNamed(false));
            Assert.Equal(new[] { "AcDbAttr" }, ApplicationsNamed(true));
        }

        // ---- the polyline of a spline ---------------------------------------------------------------------------------------------

        [Fact]
        public void ASplineOfSixControlPoints_IsA3dPolylineOf192Vertices_32ForEachPoint()
        {
            DxfDatabase database = Drawing();
            database.ModelSpace.AppendEntity(
                new DxfSpline(
                    3,
                    false,
                    false,
                    false,
                    new[]
                    {
                        new GeoPoint3(0, 0, 0),
                        new GeoPoint3(10, 8, 0),
                        new GeoPoint3(20, 1, 0),
                        new GeoPoint3(30, 9, 0),
                        new GeoPoint3(40, 2, 0),
                        new GeoPoint3(50, 8, 0),
                    },
                    new double[] { 0, 0, 0, 0, 1, 2, 3, 3, 3, 3 },
                    new double[] { 1, 1, 1, 1, 1, 1 },
                    1E-10,
                    1E-10
                )
            );

            // AutoCAD 2021 writes 128 vertices for a spline of four control points, 192 for six and 256 for eight, whatever the
            // degree.
            Assert.Equal(192, Entities(R12(database)).Count(r => r.Type == "VERTEX"));
        }

        [Fact]
        public void ASplineThroughFivePoints_HasTheVerticesOfItsCubicOfSevenControlPoints()
        {
            DxfDatabase database = Drawing();
            var spline = new DxfSpline();
            database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(10, 8, 0),
                    new GeoPoint3(20, 1, 0),
                    new GeoPoint3(30, 9, 0),
                    new GeoPoint3(40, 2, 0),
                },
                3,
                1E-10
            );

            // AutoCAD 2021: a spline through 2 to 100 points is the polyline of 32 vertices for each of the control points of the
            // cubic it keeps, two more than the fit points: 224 for five.
            Assert.Equal(224, Entities(R12(database)).Count(r => r.Type == "VERTEX"));
        }

        // ---- the widths of a lightweight polyline ---------------------------------------------------------------------------

        private static string[] Widths(Record record) =>
            new[] { 40, 41 }.Select(c => record.First(c)).Select(v => v == null ? null : v.Trim()).ToArray();

        private static List<Record> Polyline(DxfDatabase database)
        {
            List<Record> entities = Entities(R12(database));
            Assert.Equal("POLYLINE", entities[0].Type);
            return entities.TakeWhile(r => r.Type != "SEQEND").ToList();
        }

        private static DxfPolyline Lightweight(DxfDatabase database, double[] start, double[] end)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < start.Length; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(i * 10, i % 2 * 5), 0.0, start[i], end[i]);
            }

            database.ModelSpace.AppendEntity(polyline);
            return polyline;
        }

        [Fact]
        public void ALightweightPolylineOfConstantWidth_HasTheWidthInItsHeader_AndNoneOnItsVertices()
        {
            DxfDatabase database = Drawing();
            var polyline = new DxfPolyline { Closed = true };
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(20, 0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(20, 10), 0.0, 0.0, 0.0);
            database.ModelSpace.AppendEntity(polyline);
            polyline.ConstantWidth = 2.0;

            // AutoCAD 2021: 40 and 41 of 2 on the polyline after its flag, none on a vertex.
            List<Record> records = Polyline(database);
            Assert.Equal(new[] { "2", "2" }, Widths(records[0]));
            Assert.All(records.Skip(1), r => Assert.Equal(new string[] { null, null }, Widths(r)));
        }

        [Fact]
        public void TheFirstVertexsWidths_AreTheDefaultsOfTheHeader_AndAVertexHoldsOnlyWhatDiffersFromThem()
        {
            DxfDatabase database = Drawing();
            Lightweight(database, new[] { 1.0, 2.0, 0.0 }, new[] { 2.0, 1.0, 0.0 });

            // AutoCAD 2021: the header 1 and 2; the first vertex none; the second 2 and 1; the last, which differs by being
            // nought, the noughts written.
            List<Record> records = Polyline(database);
            Assert.Equal(new[] { "1", "2" }, Widths(records[0]));
            Assert.Equal(new string[] { null, null }, Widths(records[1]));
            Assert.Equal(new[] { "2", "1" }, Widths(records[2]));
            Assert.Equal(new[] { "0.0", "0.0" }, Widths(records[3]));
        }

        [Fact]
        public void AWidthOfNought_IsLeftOutOfTheHeader_AndAVertexOfTheOtherWidthHoldsOnlyThat()
        {
            DxfDatabase database = Drawing();
            Lightweight(database, new[] { 0.0, 1.0, 1.0 }, new[] { 2.0, 1.0, 1.0 });

            // AutoCAD 2021: only 41 of 2 in the header; the first vertex none; the others hold the 40 of 1 (the end, 1, is not
            // 2) and their 41 of 1.
            List<Record> records = Polyline(database);
            Assert.Equal(new[] { null, "2" }, Widths(records[0]));
            Assert.Equal(new string[] { null, null }, Widths(records[1]));
            Assert.Equal(new[] { "1", "1" }, Widths(records[2]));
        }

        [Fact]
        public void APolylineOfNoWidth_HasNoWidthAnywhere()
        {
            DxfDatabase database = Drawing();
            Lightweight(database, new[] { 0.0, 0.0, 0.0 }, new[] { 0.0, 0.0, 0.0 });

            Assert.All(Polyline(database), r => Assert.Equal(new string[] { null, null }, Widths(r)));
        }
    }
}
