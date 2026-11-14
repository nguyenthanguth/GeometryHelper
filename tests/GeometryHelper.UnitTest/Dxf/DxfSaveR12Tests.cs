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
    /// A drawing saved as R12 (AC1009), <see cref="DxfDatabase.Save(string, string)"/>, as AutoCAD 2021 writes the same drawing
    /// as R12 (its own sample of one drawing in each version, which the rules were read from): the four sections of R12, the
    /// header of R12, tables with no handles and no block records, names in capitals, entities with the groups R12 has, and
    /// the kinds R12 lacks broken into the lines, texts and polylines it holds, in a block of their own (<c>*U</c> or <c>*X</c>)
    /// where they stood in a space and where they stood in a block, or left out.
    /// </summary>
    public class DxfSaveR12Tests
    {
        private static DxfDatabase Ezdxf() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

        /// <summary>The text of the drawing saved as R12 and the sentences it said.</summary>
        private static string R12(DxfDatabase database, out IReadOnlyList<string> said)
        {
            using (var stream = new MemoryStream())
            {
                said = database.Save(stream, "AC1009");
                return Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }
        }

        private static string R12(DxfDatabase database) => R12(database, out _);

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
        }

        /// <summary>The records of the sections, by section: a record runs from its group 0 to the next; a header variable from its group 9.</summary>
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

        private static IEnumerable<Record> All(Dictionary<string, List<Record>> sections) =>
            sections.Values.SelectMany(s => s);

        [Fact]
        public void ADrawingSavedAsR12_HasTheFourSectionsOfR12_AndItsVersion()
        {
            DxfDatabase database = Ezdxf();
            string text = R12(database);
            Dictionary<string, List<Record>> sections = Sections(text);

            Assert.Equal(new[] { "BLOCKS", "ENTITIES", "HEADER", "TABLES" }, sections.Keys.OrderBy(k => k).ToArray());
            Assert.Equal("AC1009", sections["HEADER"].Single(r => r.Type == "$ACADVER").First(1));
            Assert.Equal("AC1032", database.Version);

            // It reads back as the drawing of R12 it is.
            DxfDatabase again = DxfDatabase.Parse(text);
            Assert.Equal("AC1009", again.Version);
            Assert.Equal(text, again.ToText());
        }

        [Fact]
        public void TheHeaderOfR12_HoldsTheVariablesR12Has_AndHandlingOn()
        {
            List<Record> header = Sections(R12(Ezdxf()))["HEADER"];
            string[] names = header.Select(r => r.Type).ToArray();

            Assert.Equal("$ACADVER", names[0]);
            Assert.Contains("$INSBASE", names);
            Assert.Contains("$EXTMIN", names);
            Assert.Contains("$DIMSTYLE", names);
            Assert.Contains("$HANDSEED", names);
            Assert.Equal("1", header.Single(r => r.Type == "$HANDLING").First(70).Trim());

            foreach (
                string later in new[]
                {
                    "$ACADMAINTVER",
                    "$DWGCODEPAGE",
                    "$CELTSCALE",
                    "$DIMLUNIT",
                    "$INSUNITS",
                    "$CMLSTYLE",
                    "$LASTSAVEDBY",
                }
            )
            {
                Assert.DoesNotContain(later, names);
            }

            // The text style, layer and linetype the drawing is drawn with are named in capitals.
            Assert.Equal("STANDARD", header.Single(r => r.Type == "$TEXTSTYLE").First(7));
            Assert.Equal(
                header.Single(r => r.Type == "$CLAYER").First(8).ToUpperInvariant(),
                header.Single(r => r.Type == "$CLAYER").First(8)
            );
        }

        [Fact]
        public void TheTablesOfR12_AreInItsOrder_WithNoBlockRecordsAndNoHandles_NamedInCapitals()
        {
            List<Record> tables = Sections(R12(Ezdxf()))["TABLES"];
            string[] order = tables.Where(r => r.Type == "TABLE").Select(r => r.First(2)).ToArray();

            Assert.Equal(new[] { "VPORT", "LTYPE", "LAYER", "STYLE", "VIEW", "UCS", "APPID", "DIMSTYLE" }, order);

            foreach (Record record in tables)
            {
                // A dimension style's 5 is its arrowhead's block name, not a handle.
                Assert.True(record.Type == "DIMSTYLE" || !record.Has(5), record.Type + " has a handle");
                Assert.False(record.Has(100), record.Type + " has a subclass marker");
                Assert.False(record.Has(330), record.Type + " has an owner");

                if (record.Type != "TABLE" && record.Type != "ENDTAB")
                {
                    Assert.Equal(record.First(2).ToUpperInvariant(), record.First(2));
                }
            }

            // ByBlock and ByLayer are not linetypes of a table of R12; Continuous is, in capitals.
            Record[] linetypes = tables.Where(r => r.Type == "LTYPE").ToArray();
            Assert.Single(linetypes);
            Assert.Equal("CONTINUOUS", linetypes[0].First(2));
            Assert.Equal(
                tables.Count(r => r.Type == "LAYER"),
                int.Parse(
                    tables.Single(r => r.Type == "TABLE" && r.First(2) == "LAYER").First(70).Trim(),
                    CultureInfo.InvariantCulture
                )
            );
        }

        [Fact]
        public void NoGroupAnywhereInR12_IsASubclassMarker_AnOwner_OrAReactorList()
        {
            Dictionary<string, List<Record>> sections = Sections(R12(Ezdxf()));

            foreach (Record record in All(sections).Where(r => !r.Type.StartsWith("$", StringComparison.Ordinal)))
            {
                Assert.False(record.Has(100), record.Type);
                Assert.False(record.Has(102), record.Type);
                Assert.False(record.Has(330), record.Type);
                Assert.False(record.Has(360), record.Type);
            }
        }

        [Fact]
        public void TheBlocksOfR12_AreTheSpacesAndTheUserBlocks_AndNoLayoutOfPaperSpace()
        {
            List<Record> blocks = Sections(R12(Ezdxf()))["BLOCKS"];
            string[] names = blocks.Where(r => r.Type == "BLOCK").Select(r => r.First(2)).ToArray();

            Assert.Equal("$MODEL_SPACE", names[0]);
            Assert.Equal("$PAPER_SPACE", names[1]);
            Assert.Contains("BOLT", names);
            Assert.Contains("FRAME", names);
            Assert.Contains("*D1", names);
            Assert.DoesNotContain(names, n => n.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, n => n.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("1", blocks.First(r => r.Type == "BLOCK" && r.First(2) == "$PAPER_SPACE").First(67));
            Assert.Equal(names.Length, blocks.Count(r => r.Type == "ENDBLK"));
            Assert.All(blocks.Where(r => r.Type == "BLOCK"), r => Assert.False(r.Has(5)));
            Assert.All(blocks.Where(r => r.Type == "ENDBLK"), r => Assert.True(r.Has(5)));
        }

        [Fact]
        public void ALightweightPolyline_IsAPolylineOfR12_WithItsHandle_ItsVerticesAndASeqend()
        {
            DxfDatabase database = Ezdxf();
            var lw = new DxfPolyline();
            lw.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            lw.AddVertexAt(1, new GeoPoint2(10, 0), 0.5, 0.0, 0.0);
            lw.AddVertexAt(2, new GeoPoint2(10, 10), 0.0, 0.0, 0.0);
            lw.Closed = true;
            database.ModelSpace.AppendEntity(lw);
            string handle = lw.Handle;

            List<Record> entities = Sections(R12(database))["ENTITIES"];

            Assert.DoesNotContain(entities, r => r.Type == "LWPOLYLINE");
            int at = entities.FindIndex(r => r.Type == "POLYLINE" && r.First(5) == handle);
            Assert.True(at >= 0);
            Assert.Equal("1", entities[at].First(66).Trim());
            Assert.Equal("1", entities[at].First(70).Trim());
            Assert.Equal(
                new[] { "VERTEX", "VERTEX", "VERTEX", "SEQEND" },
                entities.Skip(at + 1).Take(4).Select(r => r.Type)
            );
            Assert.Equal("0.5", entities[at + 2].First(42));
        }

        [Fact]
        public void AnEllipseAndASpline_AreThe3dPolylinesOfR12_OfOneHundredAndTwentyEightSegments()
        {
            DxfDatabase database = Ezdxf();
            var ellipse = new DxfEllipse(
                new GeoPoint3(68.0, 4.0, 0.0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(-5.0, 0.0, 0.0),
                0.5,
                0.0,
                2.0 * Math.PI
            );
            database.ModelSpace.AppendEntity(ellipse);

            List<Record> entities = Sections(R12(database))["ENTITIES"];

            int at = entities.FindIndex(r => r.Type == "POLYLINE" && r.First(5) == ellipse.Handle);
            Assert.True(at >= 0);
            Assert.Equal("8", entities[at].First(70).Trim());
            List<Record> vertices = entities.Skip(at + 1).TakeWhile(r => r.Type == "VERTEX").ToList();
            Assert.Equal(129, vertices.Count);
            Assert.Equal("32", vertices[0].First(70).Trim());

            // The first vertex is the start of the ellipse and the second one 128th of the turn along it.
            Assert.Equal(63.0, double.Parse(vertices[0].First(10), CultureInfo.InvariantCulture), 3);
            Assert.Equal(4.0, double.Parse(vertices[0].First(20), CultureInfo.InvariantCulture), 3);
            double step = 2.0 * Math.PI / 128.0;
            Assert.Equal(
                68.0 - 5.0 * Math.Cos(step),
                double.Parse(vertices[1].First(10), CultureInfo.InvariantCulture),
                3
            );
            Assert.Equal(
                4.0 - 2.5 * Math.Sin(step),
                double.Parse(vertices[1].First(20), CultureInfo.InvariantCulture),
                3
            );

            // The splines of the drawing: each a polyline of 32 vertices for each of its control points (AutoCAD 2021, as probed).
            DxfSpline[] splines = database.ModelSpace.OfType<DxfSpline>().ToArray();
            Assert.Equal(2, splines.Length);

            foreach (DxfSpline spline in splines)
            {
                int place = entities.FindIndex(r => r.Type == "POLYLINE" && r.First(5) == spline.Handle);
                Assert.True(place >= 0);
                Assert.Equal("8", entities[place].First(70).Trim());
                Assert.Equal(
                    32 * spline.NumControlPoints,
                    entities.Skip(place + 1).TakeWhile(r => r.Type == "VERTEX").Count()
                );
            }
        }

        [Fact]
        public void ARayAndAnInfiniteLine_AreLinesOfR12_WithTheirHandles()
        {
            DxfDatabase database = Ezdxf();
            DxfEntity ray = database.ModelSpace.OfType<DxfRay>().Single();
            DxfEntity xline = database.ModelSpace.OfType<DxfXline>().Single();

            List<Record> entities = Sections(R12(database))["ENTITIES"];

            Assert.DoesNotContain(entities, r => r.Type == "RAY" || r.Type == "XLINE");
            Assert.Contains(entities, r => r.Type == "LINE" && r.First(5) == ray.Handle);
            Assert.Contains(entities, r => r.Type == "LINE" && r.First(5) == xline.Handle);
        }

        [Fact]
        public void AnMtextInModelSpace_IsAnInsertOfABlockOfItsTexts_AndInABlock_TheSame()
        {
            DxfDatabase database = Ezdxf();
            DxfMText inSpace = database.ModelSpace.OfType<DxfMText>().Single();
            string handle = inSpace.Handle;
            DxfBlockTableRecord home = database.BlockTable.First(b => !b.IsLayout && b.OfType<DxfMText>().Any());
            string homeName = home.Name.ToUpperInvariant();
            int linesBefore = home.OfType<DxfLine>().Count();

            IReadOnlyList<string> said;
            Dictionary<string, List<Record>> sections = Sections(R12(database, out said));

            Assert.DoesNotContain(All(sections), r => r.Type == "MTEXT");
            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == handle);
            string block = insert.First(2);
            Assert.StartsWith("*U", block, StringComparison.Ordinal);
            Assert.Equal("0.0", insert.First(10));
            Assert.Equal("0.0", insert.First(20));

            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == block);
            Assert.True(start >= 0);
            Assert.Equal("1", blocks[start].First(70).Trim());
            Assert.Equal("TEXT", blocks[start + 1].Type);
            Assert.Equal("ENDBLK", blocks.Skip(start + 1).First(r => r.Type != "TEXT").Type);

            // In a block it is an INSERT of a block of its texts too, as AutoCAD 2021 writes it (a dimension's text, a table's cells).
            int from = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == homeName);
            int to = blocks.FindIndex(from, r => r.Type == "ENDBLK");
            List<Record> content = blocks.Skip(from + 1).Take(to - from - 1).ToList();
            Assert.DoesNotContain(content, r => r.Type == "TEXT");
            Assert.Equal(linesBefore, content.Count(r => r.Type == "LINE"));
            Record inner = content.First(r =>
                r.Type == "INSERT" && r.First(2).StartsWith("*U", StringComparison.Ordinal)
            );
            int innerStart = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == inner.First(2));
            Assert.Equal("TEXT", blocks[innerStart + 1].Type);
            Assert.Contains(said, s => s.Contains("MTEXT"));
        }

        [Fact]
        public void AHatchALeaderAndAMultileader_AreBrokenIntoTheLinesAndTextsOfABlockEach_AndSaid()
        {
            DxfDatabase database = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfEntity[] broken = database
                .ModelSpace.Where(e => e is DxfHatch || e is DxfLeader || e is DxfMLeader)
                .ToArray();
            Assert.Contains(broken, e => e is DxfHatch hatch && hatch.IsSolidFill);
            Assert.Contains(broken, e => e is DxfHatch hatch && !hatch.IsSolidFill);

            IReadOnlyList<string> said;
            Dictionary<string, List<Record>> sections = Sections(R12(database, out said));

            Assert.DoesNotContain(All(sections), r =>
                r.Type == "HATCH" || r.Type == "LEADER" || r.Type == "MULTILEADER"
            );

            foreach (DxfEntity entity in broken)
            {
                Record insert = sections["ENTITIES"]
                    .SingleOrDefault(r => r.Type == "INSERT" && r.First(5) == entity.Handle);
                Assert.True(insert != null, entity.Handle + " said: " + string.Join(" | ", said));
                Assert.Matches("^[*][UX][0-9]+$", insert.First(2));
                List<Record> blocks = sections["BLOCKS"];
                int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
                string[] types = blocks
                    .Skip(start + 1)
                    .TakeWhile(r => r.Type != "ENDBLK")
                    .Select(r => r.Type)
                    .Distinct()
                    .ToArray();
                Assert.NotEmpty(types);

                // A hatch of a pattern is its lines in a block of the letter X; one filled solid its SOLIDs and the LINEs of the
                // sides of its strips, in a U.
                if (entity is DxfHatch pattern)
                {
                    Assert.Equal(pattern.IsSolidFill ? 'U' : 'X', insert.First(2)[1]);
                    Assert.Equal(pattern.IsSolidFill ? "SOLID" : "LINE", types[0]);
                    Assert.All(types, t => Assert.True(t == "SOLID" || t == "LINE"));
                }
            }

            Assert.Contains(said, s => s.Contains("HATCH"));
            Assert.Contains(said, s => s.Contains("LEADER"));
        }

        [Fact]
        public void ASolidHatchOfASquareWithASquareHole_IsTheSolidsOfItsRing_OfTheAreaBetweenThem()
        {
            DxfDatabase database = Ezdxf();
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new double[4]
            );
            hatch.AppendLoop(
                DxfHatchLoopTypes.Default,
                new[] { new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60), new GeoPoint2(40, 60) },
                new double[4]
            );
            database.ModelSpace.AppendEntity(hatch);
            string handle = hatch.Handle;

            Dictionary<string, List<Record>> sections = Sections(R12(database));

            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == handle);
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            List<Record> content = blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
            List<Record> solids = content.Where(r => r.Type == "SOLID").ToList();

            // The ring of a square 100 by 100 and a hole 20 by 20 is triangles, each the third corner written again as the fourth,
            // and the LINEs of the sides of its strips; the corners are in the plane turned over, x negated.
            Assert.Equal(8, solids.Count);
            Assert.All(content, r => Assert.True(r.Type == "SOLID" || r.Type == "LINE"));
            Assert.All(solids, r => Assert.Equal(r.First(12), r.First(13)));
            Assert.All(solids, r => Assert.Equal("-1", r.First(230).Trim().Replace(".0", string.Empty)));
            Assert.All(solids, r => Assert.True(double.Parse(r.First(10), CultureInfo.InvariantCulture) <= 0.0));
            double area = 0.0;

            foreach (Record solid in solids)
            {
                double[] v = new[] { 10, 20, 11, 21, 12, 22 }
                    .Select(c => double.Parse(solid.First(c), CultureInfo.InvariantCulture))
                    .ToArray();
                area += Math.Abs((v[2] - v[0]) * (v[5] - v[1]) - (v[4] - v[0]) * (v[3] - v[1])) / 2.0;
            }

            Assert.Equal(10000.0 - 400.0, area, 6);
        }

        [Fact]
        public void AMesh_IsTheThreeDPolylinesOfItsEdges_EachEdgeOnce()
        {
            DxfDatabase database = Ezdxf();
            string meshHandle = Sections(database.ToText())["ENTITIES"].Single(r => r.Type == "MESH").First(5);

            Dictionary<string, List<Record>> sections = Sections(R12(database));

            Record insert = sections["ENTITIES"].Single(r => r.Type == "INSERT" && r.First(5) == meshHandle);
            List<Record> blocks = sections["BLOCKS"];
            int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            List<Record> content = blocks.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();

            Assert.Equal("POLYLINE", content[0].Type);
            Assert.Equal("8", content[0].First(70).Trim());
            Assert.Equal(
                new[] { "POLYLINE", "VERTEX", "VERTEX", "SEQEND" },
                content.Take(4).Select(r => r.Type).ToArray()
            );
            Assert.Equal(content.Count / 4, content.Count(r => r.Type == "POLYLINE"));
        }

        [Fact]
        public void TheAnonymousBlocksMade_AreNumberedOnFromTheHighestOfTheDrawing_InTheOrderOfTheirHandles()
        {
            DxfDatabase database = Ezdxf();
            Dictionary<string, List<Record>> sections = Sections(R12(database));

            // The INSERTs of the blocks made, those of the spaces and of the blocks together, with the handle of the entity each stood for.
            Record[] inserts = sections["ENTITIES"]
                .Concat(sections["BLOCKS"])
                .Where(r => r.Type == "INSERT" && r.First(5) != null && Regex.IsMatch(r.First(2), "^[*][UX][0-9]+$"))
                .ToArray();
            int highest = database
                .BlockTable.Select(b => Regex.Match(b.Name ?? string.Empty, "^[*][A-Z]([0-9]+)$"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max();
            Record[] made = inserts
                .Where(r => int.Parse(r.First(2).Substring(2), CultureInfo.InvariantCulture) > highest)
                .OrderBy(r => long.Parse(r.First(5), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();
            int[] numbers = made.Select(r => int.Parse(r.First(2).Substring(2), CultureInfo.InvariantCulture))
                .ToArray();

            Assert.NotEmpty(numbers);
            Assert.Equal(Enumerable.Range(highest + 1, numbers.Length).ToArray(), numbers);
            Assert.Contains(made, r => sections["BLOCKS"].Contains(r));
            Assert.Contains(made, r => sections["ENTITIES"].Contains(r));
            Assert.All(made, r =>
                Assert.Single(sections["BLOCKS"], b => b.Type == "BLOCK" && b.First(2) == r.First(2))
            );
        }

        [Fact]
        public void TheKindsR12CannotHold_AreLeftOutAndSaid_AnImage()
        {
            DxfDatabase database = Ezdxf();
            Assert.Contains(database.ModelSpace, e => e.GetType().Name == "DxfRasterImage");

            IReadOnlyList<string> said;
            Dictionary<string, List<Record>> sections = Sections(R12(database, out said));

            Assert.DoesNotContain(All(sections), r => r.Type == "IMAGE" || r.Type == "MESH");
            Assert.Contains(said, s => s.StartsWith("Left out", StringComparison.Ordinal) && s.Contains("IMAGE 1"));
        }

        [Fact]
        public void ADimension_IsKeptWithItsBlock_AndTheBitOf32OfItsFlagsIsGone()
        {
            DxfDatabase database = Ezdxf();
            DxfEntity dimension = database.ModelSpace.First(e => e is DxfDimension);
            Record source = Sections(database.ToText())["ENTITIES"]
                .Single(r => r.Type == "DIMENSION" && r.First(5) == dimension.Handle);
            Assert.True((int.Parse(source.First(70), CultureInfo.InvariantCulture) & 32) != 0);

            Record record = Sections(R12(database))["ENTITIES"]
                .Single(r => r.Type == "DIMENSION" && r.First(5) == dimension.Handle);

            Assert.Equal(source.First(2).ToUpperInvariant(), record.First(2));
            Assert.Equal(0, int.Parse(record.First(70), CultureInfo.InvariantCulture) & 32);
            Assert.False(record.Has(3));
            Assert.False(record.Has(71));
        }

        [Fact]
        public void TheLayerNamesOfEntities_AreInCapitals_AsTheyAreInTheLayerTable()
        {
            DxfDatabase database = Ezdxf();
            Dictionary<string, List<Record>> sections = Sections(R12(database));
            HashSet<string> layers = new HashSet<string>(
                sections["TABLES"].Where(r => r.Type == "LAYER").Select(r => r.First(2)),
                StringComparer.Ordinal
            );

            Assert.Contains("STEEL", layers);

            // The drawing's entities that stand on a layer of its table name it as the table does; one of the drawing
            // names a layer its table has not (VIEWPORTS), which is in capitals as well.
            foreach (Record record in sections["ENTITIES"].Concat(sections["BLOCKS"]).Where(r => r.Has(8)))
            {
                Assert.Equal(record.First(8).ToUpperInvariant(), record.First(8));
            }

            Assert.Contains(sections["ENTITIES"], r => r.First(8) == "STEEL");
        }

        [Fact]
        public void APaperSpaceViewport_KeepsItsViewInTheMviewDataOfR12()
        {
            DxfDatabase database = Ezdxf();
            Record viewport = Sections(R12(database))["ENTITIES"].Single(r => r.Type == "VIEWPORT");

            Assert.True(viewport.Has(68));
            Assert.True(viewport.Has(69));
            Assert.False(viewport.Has(12));
            int[] codes = viewport.Groups.Where(g => g.Key >= 1000).Select(g => g.Key).ToArray();
            Assert.Equal(1001, codes[0]);
            Assert.Equal("ACAD", viewport.Groups.First(g => g.Key == 1001).Value);
            Assert.Equal("MVIEW", viewport.Groups.First(g => g.Key == 1000).Value);
            Assert.Equal("16", viewport.Groups.First(g => g.Key == 1070).Value.Trim());
            Assert.Equal(2, codes.Count(c => c == 1010));
            Assert.Equal(7 + 7, codes.Count(c => c == 1040));
        }

        [Fact]
        public void ADrawingOfR2000_IsSavedAsR12_Too_AndOneOfR12AsItIs()
        {
            DxfDatabase old = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            string text = R12(old);

            Assert.Equal("AC1009", DxfDatabase.Parse(text).Version);

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            string own = R12(r12, out IReadOnlyList<string> said);
            Assert.Empty(said);
            Assert.Equal(r12.ToText(), own);
        }

        [Fact]
        public void ADrawingOfR14_SavedAsR12_IsRefused_AsItIsForTheOthers()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000.Replace("AC1015", "AC1014"));

            using (var stream = new MemoryStream())
            {
                Assert.Throws<NotSupportedException>(() => db.Save(stream, "AC1009"));
                Assert.Equal(0, stream.Length);
            }
        }

        [Fact]
        public void TextBeyondTheCodePage_IsWrittenAsEscapes_AndTheLayerOfItStaysAMatch()
        {
            DxfDatabase database = Ezdxf();
            string text = R12(database);

            // The layer Wand€Außen is in the table and on an entity, in capitals, as \U+ escapes where the page has no letter.
            Assert.Contains("WAND", text);
            Assert.DoesNotContain("Wand", text);
            Dictionary<string, List<Record>> sections = Sections(text);
            string name = sections["TABLES"]
                .Where(r => r.Type == "LAYER")
                .Select(r => r.First(2))
                .Single(n => n.StartsWith("WAND", StringComparison.Ordinal));
            Assert.Contains(sections["ENTITIES"], r => r.First(8) == name);
        }
    }
}
