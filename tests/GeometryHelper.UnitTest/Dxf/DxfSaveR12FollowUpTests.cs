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
    /// side databases at R12 beside their R2018): the hatch as R14 held it in the extended data of the INSERT it is broken into
    /// and the boundary that names it, a multileader's version, the polylines of an ellipse and a spline in AutoCAD's own form,
    /// a MESH as the edges its record lists, the pieces of an MTEXT without the groups that are as they are by default, what
    /// breaks in a block as an INSERT of a block of its pieces, and names that come to one told apart.
    /// </summary>
    public class DxfSaveR12FollowUpTests
    {
        private const double Near = 1E-9;

        private static DxfDatabase Ezdxf() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

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

            internal bool Has(int code) => Groups.Any(g => g.Key == code);

            /// <summary>The groups of the extended data, as <c>code=value</c>.</summary>
            internal string[] Xdata() =>
                Groups.Where(g => g.Key >= 1000).Select(g => g.Key + "=" + g.Value.Trim()).ToArray();
        }

        /// <summary>The records of the sections: a record runs from its group 0 to the next, a header variable from its group 9.</summary>
        private static Dictionary<string, List<Record>> Sections(string text)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var sections = new Dictionary<string, List<Record>>();
            List<Record> current = null;
            Record record = null;
            bool header = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    string name = lines[i + 3];
                    sections[name] = current = new List<Record>();
                    header = name == "HEADER";
                    record = null;
                    i += 2;
                    continue;
                }

                if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    current = null;
                    record = null;
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (header ? code == 9 : code == 0)
                {
                    record = new Record(value);
                    current.Add(record);
                    continue;
                }

                record?.Groups.Add(new KeyValuePair<int, string>(code, value));
            }

            return sections;
        }

        private static List<Record> BlockNamed(Dictionary<string, List<Record>> sections, string name)
        {
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == name);
            Assert.True(start >= 0, "no block " + name);
            return blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
        }

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        /// <summary>A square of four LINEs 100 by 60, added to model space.</summary>
        private static DxfLine[] Square(DxfDatabase database)
        {
            var corners = new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 60, 0),
                new GeoPoint3(0, 60, 0),
            };
            var lines = new DxfLine[4];

            for (int i = 0; i < 4; i++)
            {
                lines[i] = new DxfLine(corners[i], corners[(i + 1) % 4]);
                database.ModelSpace.AppendEntity(lines[i]);
            }

            return lines;
        }

        private static DxfHatch HatchOf(DxfDatabase database, DxfLine[] boundary, string pattern)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, pattern);
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60), new GeoPoint2(0, 60) },
                new double[4]
            );
            database.ModelSpace.AppendEntity(hatch);
            hatch.SetAssociatedObjects(new[] { (IReadOnlyList<DxfEntity>)boundary });
            return hatch;
        }

        /// <summary>
        /// The first hatch of a pattern of the drawing of a hatch, a leader and a multileader, made to follow four lines added to
        /// model space (its loop is the one polyline), with the record it was written as.
        /// </summary>
        private static DxfHatch PatternHatch(DxfDatabase database, out DxfLine[] boundary, out Record source)
        {
            DxfHatch hatch = database.ModelSpace.OfType<DxfHatch>().First(h => !h.IsSolidFill && !h.IsGradient);
            boundary = Square(database);
            hatch.SetAssociatedObjects(new[] { (IReadOnlyList<DxfEntity>)boundary });
            string handle = hatch.Handle;
            source = Sections(database.ToText())["ENTITIES"].Single(r => r.Type == "HATCH" && r.First(5) == handle);
            return hatch;
        }

        // ---- the hatch and the multileader --------------------------------------------------------------------------------------

        [Fact]
        public void APatternHatchOnFourLines_IsAnInsertThatHoldsTheHatchAsR14DoesInItsExtendedData()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch hatch = PatternHatch(database, out DxfLine[] square, out Record source);

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == hatch.Handle);
            string[] xdata = insert.Xdata();
            string name = source.First(2);
            int loops = int.Parse(source.First(91).Trim(), CultureInfo.InvariantCulture);

            // ACAD, HATCH and its name, scale and angle (in radians), what it follows and its seed point, then R14's own data.
            Assert.Equal(
                new[] { "1001=ACAD", "1000=HATCH", "1002={", "1070=19", "1000=" + name },
                xdata.Take(5).ToArray()
            );
            Assert.StartsWith("1040=", xdata[5], StringComparison.Ordinal);
            Assert.Equal(Number(source.First(41)), Number(xdata[5].Substring(5)), 9);
            Assert.StartsWith("1040=", xdata[6], StringComparison.Ordinal);
            Assert.Equal(Number(source.First(52)) * Math.PI / 180.0, Number(xdata[6].Substring(5)), 9);
            Assert.Equal("1000=ASC_BOUNDS", xdata[7]);
            Assert.Equal("1070=" + loops, xdata[8]);
            Assert.Equal("1070=1", xdata[9]);
            Assert.Equal(
                new[] { "1070=4" }.Concat(square.Select(l => "1005=" + l.Handle)).ToArray(),
                xdata.Skip(10).Take(5).ToArray()
            );
            Assert.Equal("1000=ASC_SEEDPOINT", xdata[15]);

            int data = Array.IndexOf(xdata, "1000=R14_HATCH_DATA");
            Assert.True(data > 15);
            Assert.Equal("1000=" + hatch.Handle, xdata[data + 1]);

            // The three axes of the hatch's own system, each as 1011, 1021 and 1031 twice, then its elevation and normal.
            Assert.Equal(
                new[]
                {
                    "1011=1.0",
                    "1021=0.0",
                    "1031=0.0",
                    "1021=0.0",
                    "1031=0.0",
                    "1011=0.0",
                    "1021=0.0",
                    "1031=0.0",
                    "1021=1.0",
                    "1031=0.0",
                    "1011=0.0",
                    "1021=0.0",
                    "1031=0.0",
                    "1021=0.0",
                    "1031=1.0",
                    "1040=0.0",
                    "1010=0.0",
                    "1020=0.0",
                    "1030=1.0",
                    "1000=" + name,
                },
                xdata.Skip(data + 2).Take(20).ToArray()
            );

            // Not solid, associative, the loops it was written with; the last groups are the seed's height and the end.
            Assert.Equal(new[] { "1070=0", "1070=1", "1071=" + loops }, xdata.Skip(data + 22).Take(3).ToArray());
            Assert.Equal(new[] { "1040=0.0", "1002=}" }, xdata.Skip(xdata.Length - 2).ToArray());
        }

        [Fact]
        public void TheLinesAPatternHatchFollows_NameTheHatchInTheirOwnExtendedData()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch hatch = PatternHatch(database, out DxfLine[] square, out Record _);

            Dictionary<string, List<Record>> sections = Sections(R12(database));

            foreach (DxfLine line in square)
            {
                Record record = sections["ENTITIES"].Single(r => r.Type == "LINE" && r.First(5) == line.Handle);
                Assert.Equal(
                    new[] { "1001=ACAD", "1000=ASC_BOUNDS", "1002={", "1070=19", "1005=" + hatch.Handle, "1002=}" },
                    record.Xdata()
                );
            }
        }

        [Fact]
        public void ASolidHatch_HoldsOnlyR14DataInItsInsert_AndItsBoundaryNamesNothing()
        {
            DxfDatabase database = Drawing();
            DxfLine[] square = Square(database);
            DxfHatch hatch = HatchOf(database, square, "SOLID");

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            string[] xdata = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == hatch.Handle).Xdata();

            Assert.Equal(
                new[] { "1001=ACAD", "1002={", "1000=R14_HATCH_DATA", "1000=" + hatch.Handle },
                xdata.Take(4).ToArray()
            );
            Assert.DoesNotContain("1000=HATCH", xdata);
            Assert.DoesNotContain("1000=ASC_BOUNDS", xdata);
            Assert.All(square, l =>
                Assert.Empty(sections["ENTITIES"].Single(r => r.Type == "LINE" && r.First(5) == l.Handle).Xdata())
            );
        }

        [Fact]
        public void AHatchsPieces_AreByBlockWhereItsLinesAreAPattern_AndOfTheLinetypeContinuousWhereTheyAreAFill()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch pattern = database.ModelSpace.OfType<DxfHatch>().First(h => !h.IsSolidFill && !h.IsGradient);
            DxfHatch solid = database.ModelSpace.OfType<DxfHatch>().First(h => h.IsSolidFill);
            pattern.ColorIndex = 256;
            solid.ColorIndex = 256;

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            List<Record> lines = BlockNamed(
                sections,
                sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == pattern.Handle).First(2)
            );
            List<Record> fill = BlockNamed(
                sections,
                sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == solid.Handle).First(2)
            );

            Assert.All(lines, l => Assert.Equal("0", l.First(62).Trim()));
            Assert.All(lines, l => Assert.False(l.Has(6)));
            Assert.All(fill, s => Assert.Equal("CONTINUOUS", s.First(6)));
            Assert.All(fill, s => Assert.False(s.Has(62)));
        }

        [Fact]
        public void AMultileader_IsAnInsertThatNamesItsVersion_AndTheApplicationIsInTheTable()
        {
            DxfDatabase database = Ezdxf();
            DxfMLeader leader = database.ModelSpace.OfType<DxfMLeader>().First();

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == leader.Handle);

            Assert.Equal(new[] { "1001=ACAD_MLEADERVER", "1070=2" }, insert.Xdata());
            Record table = sections["TABLES"].First(r => r.Type == "TABLE" && r.First(2) == "APPID");
            Assert.Contains(sections["TABLES"], r => r.Type == "APPID" && r.First(2) == "ACAD_MLEADERVER");
            Assert.NotNull(table);
        }

        // ---- the polylines of an ellipse and a spline ---------------------------------------------------------------------------

        [Fact]
        public void AnEllipse_IsA3dPolylineThatHoldsItsFirstVertexAsItsPlace_WithSixFigureVertices()
        {
            DxfDatabase database = Drawing();
            var ellipse = new DxfEllipse(
                new GeoPoint3(10, 20, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(30, 0, 0),
                0.5,
                0,
                2 * Math.PI
            );
            database.ModelSpace.AppendEntity(ellipse);

            List<Record> entities = Sections(R12(database))["ENTITIES"];
            Record polyline = entities.Single(r => r.Type == "POLYLINE");
            Record[] vertices = entities.Where(r => r.Type == "VERTEX").ToArray();
            Record end = entities.Single(r => r.Type == "SEQEND");

            Assert.Equal(ellipse.Handle, polyline.First(5));
            Assert.Equal(new[] { 5, 8, 10, 20, 30, 66, 70 }, polyline.Groups.Select(g => g.Key).ToArray());
            Assert.Equal(40.0, Number(polyline.First(10)), 9);
            Assert.Equal(20.0, Number(polyline.First(20)), 9);
            Assert.Equal(129, vertices.Length);
            Assert.Equal(new[] { 8, 5, 10, 20, 30, 70 }, vertices[0].Groups.Select(g => g.Key).ToArray());
            Assert.Equal("40.0000", vertices[0].First(10));
            Assert.Equal("20.0000", vertices[0].First(20));
            Assert.Equal("0.00000", vertices[0].First(30));
            Assert.Equal(new[] { 8, 5 }, end.Groups.Select(g => g.Key).ToArray());
        }

        [Fact]
        public void AnEllipseFarFromTheOrigin_HasVerticesAsCWritesSixFigures()
        {
            DxfDatabase database = Drawing();
            database.ModelSpace.AppendEntity(
                new DxfEllipse(
                    new GeoPoint3(1234567.5, -7654321.25, 0),
                    new GeoVector3(0, 0, 1),
                    new GeoVector3(300000, 0, 0),
                    0.5,
                    0,
                    2 * Math.PI
                )
            );

            Record vertex = Sections(R12(database))["ENTITIES"].First(r => r.Type == "VERTEX");

            Assert.Equal("1.53457e+06", vertex.First(10));
            Assert.Equal("-7.65432e+06", vertex.First(20));
        }

        [Fact]
        public void ASpline_IsA3dPolylineOfZerosOfFiveDecimals_WithTheLayerBeforeTheHandle()
        {
            DxfDatabase database = Ezdxf();
            DxfSpline spline = database.ModelSpace.OfType<DxfSpline>().First();

            List<Record> entities = Sections(R12(database))["ENTITIES"];
            int at = entities.FindIndex(r => r.Type == "POLYLINE" && r.First(5) == spline.Handle);
            Assert.True(at >= 0);
            Record polyline = entities[at];

            Assert.Equal(new[] { 8, 5, 66, 10, 20, 30, 70 }, polyline.Groups.Select(g => g.Key).ToArray());
            Assert.Equal(
                new[] { "0.00000", "0.00000", "0.00000" },
                new[] { polyline.First(10), polyline.First(20), polyline.First(30) }
            );
            Record vertex = entities[at + 1];
            Assert.Equal(new[] { 8, 5, 10, 20, 30, 70 }, vertex.Groups.Select(g => g.Key).ToArray());
            Assert.Matches("^-?[0-9.]+(e[-+][0-9]{2})?$", vertex.First(10));
        }

        // ---- the pieces ---------------------------------------------------------------------------------------------------------

        [Fact]
        public void ATextMadeOfAnMtext_HasNoTurnOfNought_AndNoExtrusionOfNoughtNoughtOne()
        {
            DxfDatabase database = Ezdxf();
            string text = R12(database);

            foreach (Record record in Sections(text)["BLOCKS"].Where(r => r.Type == "TEXT"))
            {
                if (record.Has(50))
                {
                    Assert.True(Math.Abs(Number(record.First(50))) > 1E-12, "a turn of nought is by default");
                }

                if (record.Has(210))
                {
                    Assert.False(
                        Math.Abs(Number(record.First(210))) < Near
                            && Math.Abs(Number(record.First(220))) < Near
                            && Math.Abs(Number(record.First(230)) - 1.0) < Near,
                        "the extrusion (0, 0, 1) is by default"
                    );
                }
            }
        }

        [Fact]
        public void AnMline_IsItsLinesElementByElement_AndNoneIsOfTheLinetypeByLayer()
        {
            DxfDatabase database = Ezdxf();
            var builder = new DxfMlineBuilder((DxfMlineStyle)database.MLStyleDictionary.GetAt("Standard"))
            {
                Scale = 20.0,
                Justification = DxfMlineJustification.Zero,
            };
            builder.Points.Add(new GeoPoint3(0, 0, 0));
            builder.Points.Add(new GeoPoint3(50, 0, 0));
            builder.Points.Add(new GeoPoint3(50, 30, 0));
            DxfUnknownEntity mline = builder.Build();
            database.ModelSpace.AppendEntity(mline);

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == mline.Handle);
            Record[] lines = BlockNamed(sections, insert.First(2)).Where(r => r.Type == "LINE").ToArray();

            // Two elements along two segments: the first element's two lines, the second's two, each segment ending where the next starts.
            Assert.Equal(4, lines.Length);
            Assert.Equal(Number(lines[0].First(11)), Number(lines[1].First(10)), 6);
            Assert.Equal(Number(lines[0].First(21)), Number(lines[1].First(20)), 6);
            Assert.Equal(Number(lines[2].First(11)), Number(lines[3].First(10)), 6);
            Assert.Equal(Number(lines[2].First(21)), Number(lines[3].First(20)), 6);
            Assert.NotEqual(Number(lines[0].First(20)), Number(lines[2].First(20)), 3);
            Assert.All(lines, l => Assert.False(l.Has(6)));
        }

        [Fact]
        public void AMesh_IsItsEdgesAsItsRecordListsThem_EachFromTheLesserVertex()
        {
            // The tetrahedron of the drawing lists no edges: it is given three, each from the one vertex to the other, not by order.
            int[] listed = { 3, 1, 0, 2, 2, 3 };
            string text = new Regex("( 94\r?\n)0(\r?\n)( 95\r?\n0\r?\n)").Replace(
                DxfFileEzdxfDrawings.R2018,
                m =>
                    m.Groups[1].Value
                    + "3"
                    + m.Groups[2].Value
                    + string.Concat(listed.Select(v => " 90" + m.Groups[2].Value + v + m.Groups[2].Value))
                    + m.Groups[3].Value,
                1
            );
            DxfDatabase database = DxfDatabase.Parse(text);
            Record mesh = Sections(database.ToText())["ENTITIES"].Single(r => r.Type == "MESH");
            var points = new List<double[]>();
            var edges = new List<int[]>();
            List<KeyValuePair<int, string>> groups = mesh.Groups;
            int i = groups.FindIndex(g => g.Key == 92) + 1;

            for (int count = int.Parse(groups[i - 1].Value.Trim(), CultureInfo.InvariantCulture); count > 0; count--)
            {
                points.Add(new[] { Number(groups[i].Value), Number(groups[i + 1].Value), Number(groups[i + 2].Value) });
                i += 3;
            }

            int edgeAt = groups.FindIndex(g => g.Key == 94);
            int edgeCount = int.Parse(groups[edgeAt].Value.Trim(), CultureInfo.InvariantCulture);
            Assert.True(edgeCount > 0);

            for (int k = 0; k < edgeCount; k++)
            {
                edges.Add(
                    new[]
                    {
                        int.Parse(groups[edgeAt + 1 + 2 * k].Value.Trim(), CultureInfo.InvariantCulture),
                        int.Parse(groups[edgeAt + 2 + 2 * k].Value.Trim(), CultureInfo.InvariantCulture),
                    }
                );
            }

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == mesh.First(5));
            List<Record> block = BlockNamed(sections, insert.First(2));
            Record[] vertices = block.Where(r => r.Type == "VERTEX").ToArray();

            Assert.Equal(2 * edgeCount, vertices.Length);

            for (int k = 0; k < edgeCount; k++)
            {
                for (int end = 0; end < 2; end++)
                {
                    double[] expected = points[edges[k][end]];
                    Record vertex = vertices[2 * k + end];
                    Assert.Equal(expected[0], Number(vertex.First(10)), 6);
                    Assert.Equal(expected[1], Number(vertex.First(20)), 6);
                    Assert.Equal(expected[2], Number(vertex.First(30)), 6);
                }
            }
        }

        [Fact]
        public void ATableCellsPieces_AreByLayer_WhereItsBlockDrewThemByBlock()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = database.ModelSpace.OfType<DxfTable>().First();

            // The block the table is drawn by draws everything by block.
            foreach (DxfEntity entity in table.BlockTableRecord)
            {
                entity.ColorIndex = 0;
                entity.Linetype = "ByBlock";
            }

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == table.Handle);
            List<Record> block = BlockNamed(sections, insert.First(2));

            Assert.NotEmpty(block);
            Assert.DoesNotContain(block, r => r.Has(6) && r.First(6).Trim().ToUpperInvariant() == "BYBLOCK");
            Assert.DoesNotContain(block, r => r.Type == "LINE" && r.Has(62) && r.First(62).Trim() == "0");
        }

        // ---- in a block --------------------------------------------------------------------------------------------------------

        [Fact]
        public void AnEllipseInABlock_IsAPolylineWhereItStood_AndAnInfiniteLineInABlock_IsALineOfNoLength()
        {
            DxfDatabase database = Drawing();
            var block = new DxfBlockTableRecord { Name = "Parts" };
            database.BlockTable.Add(block);
            var ellipse = new DxfEllipse(
                new GeoPoint3(10, 20, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(30, 0, 0),
                0.5,
                0,
                2 * Math.PI
            );
            block.AppendEntity(ellipse);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var xline = new DxfXline { BasePoint = new GeoPoint3(5, 5, 0), UnitDir = new GeoVector3(1, 0, 0) };
            block.AppendEntity(xline);
            database.ModelSpace.AppendEntity(new DxfBlockReference(GeoPoint3.Origin, block));
            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                Dictionary<string, List<Record>> sections = Sections(
                    Encoding.GetEncoding(1252).GetString(stream.ToArray())
                );
                List<Record> inside = BlockNamed(sections, "PARTS");

                Assert.Contains(inside, r => r.Type == "POLYLINE" && r.First(5) == ellipse.Handle);
                Assert.DoesNotContain(inside, r => r.Type == "INSERT");
                Assert.DoesNotContain(inside, r => r.Type == "XLINE");

                // The infinite line is a LINE of no length at its base point, as AutoCAD cuts it in a block.
                Record point = inside.Single(r => r.Type == "LINE" && r.First(5) == xline.Handle);
                Assert.Equal(5.0, Number(point.First(10)), 9);
                Assert.Equal(5.0, Number(point.First(11)), 9);
                Assert.Equal(5.0, Number(point.First(21)), 9);
                Assert.Equal(2, inside.Count(r => r.Type == "LINE"));
            }
        }

        [Fact]
        public void ABlockMadeInADrawingOfAColour_HasThatColourOnItsBlockAndItsEnd()
        {
            DxfDatabase database = Ezdxf();
            database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 193);

            Dictionary<string, List<Record>> sections = Sections(R12(database));
            Record insert = sections["ENTITIES"]
                .First(r => r.Type == "INSERT" && Regex.IsMatch(r.First(2), "^[*][UX][0-9]+$"));
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            int end = blocks.FindIndex(start, r => r.Type == "ENDBLK");

            Assert.Equal("193", insert.First(62).Trim());
            Assert.Equal("193", blocks[start].First(62).Trim());
            Assert.Equal("193", blocks[end].First(62).Trim());
        }

        // ---- names ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void TwoNamesThatComeToOne_AreToldApart_InTheTablesAndInEveryEntityThatNamesThem()
        {
            DxfDatabase database = Drawing();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "A{1}" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "A_1_" });
            var first = new DxfBlockTableRecord { Name = "B{1}" };
            var second = new DxfBlockTableRecord { Name = "B_1_" };
            database.BlockTable.Add(first);
            database.BlockTable.Add(second);
            first.AppendEntity(new DxfLine(GeoPoint3.Origin, new GeoPoint3(1, 1, 0)));
            second.AppendEntity(new DxfLine(GeoPoint3.Origin, new GeoPoint3(2, 2, 0)));
            var line = new DxfLine(GeoPoint3.Origin, new GeoPoint3(3, 3, 0)) { Layer = "A{1}" };
            database.ModelSpace.AppendEntity(line);
            database.ModelSpace.AppendEntity(new DxfBlockReference(GeoPoint3.Origin, first));
            database.ModelSpace.AppendEntity(new DxfBlockReference(GeoPoint3.Origin, second));

            Dictionary<string, List<Record>> sections = Sections(R12(database));

            // The one that cleaning changed takes the number, the one it leaves keeps its name, as AutoCAD 2021 writes them.
            string[] layers = sections["TABLES"].Where(r => r.Type == "LAYER").Select(r => r.First(2)).ToArray();
            Assert.Contains("A_1_0", layers);
            Assert.Contains("A_1_", layers);
            Assert.Equal(layers.Length, layers.Distinct().Count());
            Assert.Equal("A_1_0", sections["ENTITIES"].Single(r => r.Type == "LINE").First(8));

            string[] blocks = sections["BLOCKS"].Where(r => r.Type == "BLOCK").Select(r => r.First(2)).ToArray();
            Assert.Contains("B_1_0", blocks);
            Assert.Contains("B_1_", blocks);
            string[] inserts = sections["ENTITIES"].Where(r => r.Type == "INSERT").Select(r => r.First(2)).ToArray();
            Assert.Equal(new[] { "B_1_0", "B_1_" }, inserts);
        }
    }
}
