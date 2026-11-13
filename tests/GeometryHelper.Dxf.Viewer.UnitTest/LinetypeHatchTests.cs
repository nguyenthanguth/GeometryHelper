using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// C9: linetypes read from .lin files and made, LINETYPE, LTSCALE, CELTSCALE and CELTYPE; hatches made by a point picked or
    /// objects selected, their patterns, islands, solid and gradient fills, HATCHEDIT and BOUNDARY, in AutoCAD's words.
    /// </summary>
    public class LinetypeHatchTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A folder of its own, deleted after the test.</summary>
        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c9-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string Text(DxfDatabase database) => database.ToText().Replace("\r\n", "\n");

        /// <summary>A drawing of four LINEs round each square given, by its least corner and side.</summary>
        private static CadDocument Squares(params double[] squares)
        {
            var database = new DxfDatabase("AC1027");

            for (int s = 0; s + 2 < squares.Length + 1; s += 3)
            {
                double x = squares[s];
                double y = squares[s + 1];
                double side = squares[s + 2];
                GeoPoint3[] corners =
                {
                    new GeoPoint3(x, y, 0),
                    new GeoPoint3(x + side, y, 0),
                    new GeoPoint3(x + side, y + side, 0),
                    new GeoPoint3(x, y + side, 0),
                };

                for (int i = 0; i < 4; i++)
                {
                    database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
                }
            }

            return CadDocument.From(database, "squares.dxf");
        }

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        private static void AuditsClean(CadDocument document)
        {
            var bytes = new MemoryStream();
            document.Database.Save(bytes);
            bytes.Position = 0;
            DxfDatabase read = DxfDatabase.Open(bytes);
            Assert.Equal(0, read.Audit(false).Count);
            AssertReopens(document);
        }

        [Fact]
        public void ALinFile_IsReadByValue_DashesTextsShapesAndTheirTurns()
        {
            IReadOnlyList<CadLinetypeDefinition> read = CadLinetypeFile.Parse(
                ";; a comment\r\n"
                    + "*DASHED2,Dashed (.5x) __ __ __\r\n"
                    + "A,6.35,-3.175\r\n"
                    + "*GAS,Gas ----GAS----\n"
                    + "A,12.7,-5.08,[\"GAS\",STANDARD,S=2.54,R=0.0,X=-2.54,Y=-1.27],-6.35\n"
                    + "*FENCE,Fence ----0----0----\n"
                    + "A,6.35,-2.54,[CIRC1,ltypeshp.shx,x=-2.54,s=2.54],-2.54,[\"Up\",STANDARD,U=90,S=1],-1,[BOX,ltypeshp.shx,A=1.5707963r]\n"
                    + "*BROKEN,Broken\n"
                    + "A,1,-1,[\"X\",STANDARD,Q=1]\n"
            );

            Assert.Equal(4, read.Count);
            CadLinetypeDefinition dashed = read[0];
            Assert.Equal("DASHED2", dashed.Name);
            Assert.Equal("Dashed (.5x) __ __ __", dashed.Description);
            Assert.Equal(new[] { 6.35, -3.175 }, dashed.Elements.Select(e => e.Length).ToArray());
            Assert.Equal(9.525, dashed.PatternLength, 12);
            Assert.False(dashed.IsComplex);

            CadLinetypeElement gas = read[1].Elements[1];
            Assert.Equal(CadLinetypeElementKind.Text, gas.Kind);
            Assert.Equal("GAS", gas.Value);
            Assert.Equal("STANDARD", gas.Style);
            Assert.Equal(-5.08, gas.Length);
            Assert.Equal(2.54, gas.Scale);
            Assert.Equal(CadLinetypeRotation.Relative, gas.RotationKind);
            Assert.Equal(-2.54, gas.X);
            Assert.Equal(-1.27, gas.Y);

            IReadOnlyList<CadLinetypeElement> fence = read[2].Elements;
            Assert.Equal(CadLinetypeElementKind.Shape, fence[1].Kind);
            Assert.Equal("CIRC1", fence[1].Value);
            Assert.Equal("ltypeshp.shx", fence[1].Style);
            Assert.Equal(CadLinetypeRotation.Upright, fence[2].RotationKind);
            Assert.Equal(90.0, fence[2].Rotation);
            Assert.Equal(CadLinetypeRotation.Absolute, fence[3].RotationKind);
            Assert.Equal(90.0, fence[3].Rotation, 5);

            Assert.Null(read[2].Fault);
            Assert.NotNull(read[3].Fault);
            Assert.Empty(read[3].Elements);

            // Written back as the file writes it, and read the same.
            CadLinetypeDefinition again = CadLinetypeFile.Parse(read[1].ToString()).Single();
            Assert.Equal(read[1].PatternText, again.PatternText);
            Assert.Equal("A,12.7,-5.08,[\"GAS\",STANDARD,S=2.54,R=0,X=-2.54,Y=-1.27],-6.35", again.PatternText);
        }

        [Fact]
        public void APatFile_IsReadByValue_AndThePresentersOwnSetKeepsItsSeventeenPatternsFirst()
        {
            IReadOnlyList<CadHatchPattern> read = CadPatternFile.Parse(
                "; mine\n*BOXES,Boxes ; a comment\n0, 0,0, 0,10, 5,-5\n90, 0,0, 0,10, 5,-5\n*BAD,Four numbers\n0, 0,0, 1\n*ROWS\n0,0,0,0,2\n"
            );

            Assert.Equal(new[] { "BOXES", "ROWS" }, read.Select(p => p.Name).ToArray());
            Assert.Equal("Boxes", read[0].Description);
            CadHatchPatternLine second = read[0].Lines[1];
            Assert.Equal(90.0, second.Angle);
            Assert.Equal(10.0, second.Across);
            Assert.Equal(new[] { 5.0, -5.0 }, second.Dashes.ToArray());

            Assert.Equal(
                new[]
                {
                    "ANSI31",
                    "ANSI32",
                    "ANSI33",
                    "ANSI34",
                    "ANSI37",
                    "ANGLE",
                    "BRICK",
                    "CROSS",
                    "DASH",
                    "DOTS",
                    "EARTH",
                    "HONEY",
                    "LINE",
                    "NET",
                    "SQUARE",
                    "STEEL",
                    "ZIGZAG",
                },
                CadPatternFile.BuiltIn.Take(17).Select(p => p.Name).ToArray()
            );
        }

        [Fact]
        public void TheOwnPatterns_HaveTheNamesAutoCadUsersType_TheAnsiIsoAndArchitecturalOnes()
        {
            IReadOnlyList<CadHatchPattern> own = CadPatternFile.BuiltIn;
            string[] added =
            {
                "ANSI35",
                "ANSI36",
                "ANSI38",
                "GRASS",
                "GRATE",
                "HEX",
                "INSUL",
                "NET3",
                "TRIANG",
                "AR-B816",
                "AR-BRSTD",
                "AR-CONC",
                "AR-HBONE",
                "AR-PARQ1",
                "AR-RROOF",
                "AR-RSHKE",
                "AR-SAND",
            };
            string[] names = added
                .Concat(Enumerable.Range(2, 14).Select(i => "ISO" + i.ToString("00") + "W100"))
                .ToArray();

            // A pattern the reader refuses is left out, so each name found is a pattern read whole.
            Assert.Equal(48, own.Count);
            Assert.All(names, n => Assert.NotNull(CadPatternFile.FindBuiltIn(n)));
            Assert.Equal(own.Count, own.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Null(CadPatternFile.FindBuiltIn("SOLID"));

            // ANSI31 as before: one line at 45 degrees, 3.175 apart, solid.
            CadHatchPatternLine ansi = Assert.Single(CadPatternFile.FindBuiltIn("ANSI31").Lines);
            Assert.Equal(45.0, ansi.Angle);
            Assert.Equal(3.175, ansi.Across);
            Assert.Empty(ansi.Dashes);

            // ISO 02 for a pen of 1.0 mm: a dash 12 and a gap 3; ISO 15 two lines of one period of 39 together.
            CadHatchPatternLine iso = Assert.Single(CadPatternFile.FindBuiltIn("ISO02W100").Lines);
            Assert.Equal(new[] { 12.0, -3.0 }, iso.Dashes.ToArray());
            CadHatchPattern iso15 = CadPatternFile.FindBuiltIn("ISO15W100");
            Assert.Equal(2, iso15.Lines.Count);
            Assert.All(iso15.Lines, l => Assert.Equal(39.0, l.Dashes.Sum(d => Math.Abs(d)), 12));

            // AR-BRSTD: bricks 200 by 100 with joints of 10, courses 110 apart.
            CadHatchPattern brick = CadPatternFile.FindBuiltIn("AR-BRSTD");
            Assert.Equal(4, brick.Lines.Count);
            Assert.Equal(110.0, brick.Lines[0].Across);
            Assert.Equal(new[] { 100.0, -120.0 }, brick.Lines[2].Dashes.ToArray());
        }

        [Fact]
        public void AHatchOfArBrstd_IsWrittenWithItsFourLines_AndReadBack()
        {
            CadDocument document = Squares(0, 0, 1000);
            document.HatchSettings.PatternName = "AR-BRSTD";
            Start(document, "HATCH", P(500, 500), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal("AR-BRSTD", hatch.PatternName);
            Assert.Equal(4, hatch.NumberOfPatternDefinitions);
            Assert.NotEmpty(document.GetDisplay(document.ModelSpace).Items);

            var bytes = new MemoryStream();
            document.Database.Save(bytes);
            bytes.Position = 0;
            DxfHatch read = DxfDatabase.Open(bytes).ModelSpace.OfType<DxfHatch>().Single();
            Assert.Equal("AR-BRSTD", read.PatternName);
            Assert.Equal(DxfHatchPatternType.PreDefined, read.PatternType);
            Assert.Equal(4, read.NumberOfPatternDefinitions);
            DxfPatternDefinition head = read.GetPatternDefinitionAt(2);
            Assert.Equal(Math.PI / 2.0, head.Angle, 12);
            Assert.Equal(new[] { 100.0, -120.0 }, head.GetDashes().ToArray());
            Assert.Equal(1000000.0, read.Area, 6);
            AuditsClean(document);
        }

        [Fact]
        public void AnAcadisoFileInTheSupportFolders_IsNotPreferred_TheOwnLinetypesAndPatternsAre()
        {
            Assert.Empty(CadSupportFiles.DefaultFolders);

            InFolder(folder =>
            {
                // A pattern file and a linetype file of AutoCAD's names, their ANSI31 10 apart and their DASHED 50 long.
                File.WriteAllText(Path.Combine(folder, "acadiso.pat"), "*ANSI31,Not ours\n45, 0,0, 0,10\n");
                File.WriteAllText(Path.Combine(folder, "acadiso.lin"), "*DASHED,Not ours\nA,40,-10\n");
                CadDocument document = Squares(0, 0, 30);
                document.SupportFolders = new[] { folder };
                Assert.Equal(CadLinetypeFile.BuiltInName, CadSupportFiles.DefaultLinetypeFile(document));

                Start(document, "HATCH", P(5, 5), Enter);
                Assert.Equal(3.175 / Math.Sqrt(2.0), -OnlyHatch(document).GetPatternDefinitionAt(0).OffsetX, 9);

                CadCommandSession session = Start(document, "-LINETYPE", "L", "DASHED");
                Assert.Equal("Enter linetype file to search <GeometryHelper.lin>:", session.PromptText);
                Run(session, Enter, Enter);
                Assert.Equal(7.5, document.Database.LinetypeTable["DASHED"].PatternLength, 12);
            });
        }

        [Fact]
        public void TheOwnLinetypes_AreWrittenAsAutoCadWritesThem_ReadBack_AndDrawnWithTheirDashes()
        {
            CadDocument document = Blank();
            CadLinetypeLoad load = CadLinetypes.Load(
                document,
                CadLinetypeFile.BuiltIn.Where(l => l.Name == "DASHED" || l.Name == "GAS_LINE"),
                CadLinetypeFile.BuiltInName
            );

            Assert.True(load.Result.Succeeded);
            Assert.Equal(new[] { "DASHED", "GAS_LINE" }, load.Loaded.ToArray());
            string text = Text(document.Database);
            Assert.Contains(
                "  2\nDASHED\n 70\n0\n  3\nDashed __ __ __ __ __ __ __\n 72\n65\n 73\n2\n 40\n7.5\n 49\n6\n 74\n0\n 49\n-1.5\n 74\n0\n",
                text
            );
            string standard = document
                .Database.TextStyleTable.First(s =>
                    string.Equals(s.Name, "Standard", StringComparison.OrdinalIgnoreCase)
                )
                .Handle;
            Assert.Contains(
                " 49\n-2.4\n 74\n2\n 75\n0\n340\n"
                    + standard
                    + "\n 46\n1.2\n 50\n0\n 44\n-1.2\n 45\n-0.6\n  9\nGAS\n 49\n-3\n 74\n0\n",
                text
            );

            DxfLinetypeTableRecord gas = document.Database.LinetypeTable["GAS_LINE"];
            Assert.Equal("GAS", gas.TextAt(1));
            Assert.Equal(11.4, gas.PatternLength, 12);

            // Loaded again, each is already there and nothing more is done.
            CadLinetypeLoad again = CadLinetypes.Load(
                document,
                CadLinetypeFile.BuiltIn.Where(l => l.Name == "DASHED"),
                CadLinetypeFile.BuiltInName
            );
            Assert.Equal(new[] { "DASHED" }, again.Present.ToArray());
            Assert.Empty(again.Loaded);

            Run(Start(document, "CELTYPE", "dashed"), "LINE", P(0, 0), P(100, 0), Enter);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal("DASHED", line.Linetype);
            DisplayItem2 drawn = Assert.Single(document.GetDisplay(document.ModelSpace).Items);
            Assert.Equal(new[] { 6.0, 1.5 }, drawn.Style.Dashes);
            AuditsClean(document);
        }

        [Fact]
        public void TheOwnSet_HasTheNamesAutoCadUsersType_EachAtHalfAndDoubleSize_AndTheIsoTypesForAPenOfOneMillimetre()
        {
            IReadOnlyList<CadLinetypeDefinition> own = CadLinetypeFile.BuiltIn;
            var bases = new[] { "BORDER", "CENTER", "DASHDOT", "DASHED", "DIVIDE", "DOT", "HIDDEN", "PHANTOM" };
            var counts = new[] { 6, 4, 4, 2, 6, 2, 2, 6 };
            var expected = bases
                .SelectMany(b => new[] { b, b + "2", b + "X2" })
                .Concat(Enumerable.Range(2, 14).Select(i => "ACAD_ISO" + i.ToString("00") + "W100"))
                .Concat(new[] { "GAS_LINE" })
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(39, own.Count);
            Assert.Equal(expected, own.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.All(own, l => Assert.Null(l.Fault));
            Assert.DoesNotContain(own, l => string.Equals(l.Name, "Continuous", StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < bases.Length; i++)
            {
                double[] full = own.Single(l => l.Name == bases[i]).Elements.Select(e => e.Length).ToArray();
                Assert.Equal(counts[i], full.Length);
                Assert.Equal(
                    full.Select(x => x / 2.0).ToArray(),
                    own.Single(l => l.Name == bases[i] + "2").Elements.Select(e => e.Length).ToArray()
                );
                Assert.Equal(
                    full.Select(x => x * 2.0).ToArray(),
                    own.Single(l => l.Name == bases[i] + "X2").Elements.Select(e => e.Length).ToArray()
                );
            }

            // The nine of before keep their lengths.
            Assert.Equal(
                new[] { 6.0, -1.5 },
                own.Single(l => l.Name == "DASHED").Elements.Select(e => e.Length).ToArray()
            );
            Assert.Equal(
                new[] { 3.0, -0.75 },
                own.Single(l => l.Name == "DASHED2").Elements.Select(e => e.Length).ToArray()
            );
            Assert.Equal(
                new[] { 12.0, -1.5, 3.0, -1.5 },
                own.Single(l => l.Name == "CENTER").Elements.Select(e => e.Length).ToArray()
            );
            Assert.Equal(11.4, own.Single(l => l.Name == "GAS_LINE").PatternLength, 12);

            // ISO 128-20 for a pen of 1.0 mm: a dash 12, a gap 3, a space 18, a long dash 24, a short dash 6, a dot nought.
            double[] Iso(int type) =>
                own.Single(l => l.Name == "ACAD_ISO" + type.ToString("00") + "W100")
                    .Elements.Select(e => e.Length)
                    .ToArray();
            Assert.Equal(new[] { 12.0, -3.0 }, Iso(2));
            Assert.Equal(new[] { 12.0, -18.0 }, Iso(3));
            Assert.Equal(new[] { 24.0, -3.0, 0.0, -3.0 }, Iso(4));
            Assert.Equal(new[] { 0.0, -3.0 }, Iso(7));
            Assert.Equal(new[] { 24.0, -3.0, 6.0, -3.0, 6.0, -3.0 }, Iso(9));
            Assert.Equal(new[] { 12.0, -3.0, 12.0, -3.0, 0.0, -3.0, 0.0, -3.0, 0.0, -3.0 }, Iso(15));
            Assert.Equal(
                new[] { 2, 2, 4, 6, 8, 2, 4, 6, 4, 6, 6, 8, 8, 10 },
                Enumerable.Range(2, 14).Select(t => Iso(t).Length).ToArray()
            );
        }

        [Fact]
        public void DashedX2_IsLoadedFromTheOwnSetByMinusLinetype_AndWritesItsLtypeRecord()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(
                document,
                "-LINETYPE",
                "L",
                "DASHEDX2",
                CadLinetypeFile.BuiltInName,
                Enter
            );

            Assert.False(session.IsRunning);
            Assert.Contains("Linetype DASHEDX2 loaded.", session.History);
            Assert.Contains(
                "  2\nDASHEDX2\n 70\n0\n  3\nDashed, double size\n 72\n65\n 73\n2\n 40\n15\n 49\n12\n 74\n0\n 49\n-3\n 74\n0\n",
                Text(document.Database)
            );
            Assert.Equal(15.0, document.Database.LinetypeTable["DASHEDX2"].PatternLength, 12);
            AuditsClean(document);
        }

        [Fact]
        public void LtscaleAndCeltscale_AreAskedAsAutoCadAsksThem_RedrawTheDashes_AndUndoTakesThemBack()
        {
            CadDocument document = Blank();
            CadLinetypes.Load(
                document,
                CadLinetypeFile.BuiltIn.Where(l => l.Name == "HIDDEN"),
                CadLinetypeFile.BuiltInName
            );
            CadCommandSession session = Start(document, "CELTYPE", "HIDDEN", "LINE", P(0, 0), P(100, 0), Enter);
            Assert.Equal(new[] { 3.0, 1.5 }, document.GetDisplay(document.ModelSpace).Items.Single().Style.Dashes);

            Run(session, "LTS");
            Assert.Equal("Enter new linetype scale factor <1.0000>:", session.PromptText);
            Run(session, "2");
            Assert.Equal(2.0, document.Database.Ltscale);
            Assert.Equal("Regenerating model.", session.History.Last());
            Assert.Equal(new[] { 6.0, 3.0 }, document.GetDisplay(document.ModelSpace).Items.Single().Style.Dashes);

            Run(session, "LTSCALE", "-1");
            Assert.Equal("Value must be positive and nonzero.", session.History.Last());
            Assert.Equal(2.0, document.Database.Ltscale);

            Run(session, "CELTSCALE");
            Assert.Equal("Enter new value for CELTSCALE <1.0000>:", session.PromptText);
            Run(session, "0.5", "LINE", P(0, 10), P(100, 10), Enter);
            DxfLine second = All<DxfLine>(document.ModelSpace).Last();
            Assert.Equal(0.5, second.LinetypeScale);
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items, i =>
                i.Style.Dashes != null && i.Style.Dashes[0] == 3.0
            );

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(1.0, document.Database.Celtscale);
            Assert.True(document.Undo());
            Assert.Equal(1.0, document.Database.Ltscale);
            Assert.Equal(new[] { 3.0, 1.5 }, document.GetDisplay(document.ModelSpace).Items.Single().Style.Dashes);
        }

        [Fact]
        public void LinetypeTyped_LoadsFromTheUsersOwnFile_CreatesOne_AndSetRefusesOneNotLoaded()
        {
            InFolder(folder =>
            {
                File.WriteAllText(
                    Path.Combine(folder, "mine.lin"),
                    "*MYDASH,Mine __ __\nA,10,-5\n*MYGAS,Mine with GAS\nA,10,-4,[\"GAS\",STANDARD,S=2,R=0,X=-2,Y=-1],-6\n"
                );
                CadDocument document = Blank();
                document.SupportFolders = new[] { folder };

                CadCommandSession session = Start(document, "-LINETYPE");
                Assert.Equal("Current line type:  \"ByLayer\"", session.History.Last());
                Assert.Equal("Enter an option [?/Create/Load/Set]:", session.PromptText);
                Run(session, "L", "my*");
                Assert.Equal("Enter linetype file to search <GeometryHelper.lin>:", session.PromptText);
                Run(session, "mine");
                Assert.Contains("Linetype MYDASH loaded.", session.History);
                Assert.Contains("Linetype MYGAS loaded.", session.History);
                Assert.Equal(-4.0, document.Database.LinetypeTable["MYGAS"].DashLengthAt(1));
                Assert.Equal("GAS", document.Database.LinetypeTable["MYGAS"].TextAt(1));

                Run(session, "S", "NOTHERE");
                Assert.Equal("Linetype NOTHERE not found. Use the Load option to load it.", session.History.Last());
                Run(session, "S", "mydash", Enter);
                Assert.False(session.IsRunning);
                Assert.Equal("MYDASH", document.Database.Celtype);

                // Create appends to the file and does not load, as AutoCAD; Load then loads it.
                string file = Path.Combine(folder, "mine.lin");
                Run(session, "-LT", "C", "WIDE", file, "Wide dashes", "A,20,-10");
                Assert.Contains("New linetype definition saved to file.", session.History);
                Assert.False(document.Database.LinetypeTable.Has("WIDE"));
                Run(session, "L", "WIDE", "mine.lin", Enter);
                Assert.Equal(30.0, document.Database.LinetypeTable["WIDE"].PatternLength);
                Assert.Contains("*WIDE,Wide dashes\r\nA,20,-10\r\n", File.ReadAllText(file));

                // One undo step for each load.
                Assert.True(document.Undo());
                Assert.False(document.Database.LinetypeTable.Has("WIDE"));
                AuditsClean(document);
            });
        }

        [Fact]
        public void HatchByAPointInASquareWithAnIsland_FillsAsTheIslandStyleSays_AndFollowsItsLines()
        {
            // A square 100 by 100, an island 60 by 60 in it, and an island 20 by 20 in that.
            foreach (
                (DxfHatchStyle style, double area, int loops) in new[]
                {
                    (DxfHatchStyle.Normal, 6800.0, 3),
                    (DxfHatchStyle.Outer, 6400.0, 2),
                    (DxfHatchStyle.Ignore, 10000.0, 1),
                }
            )
            {
                CadDocument document = Squares(0, 0, 100, 20, 20, 60, 40, 40, 20);
                document.HatchSettings.IslandStyle = style;

                CadCommandSession session = Start(document, "H");
                Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
                Run(session, P(10, 10), Enter);

                DxfHatch hatch = OnlyHatch(document);
                Assert.Equal(style, hatch.HatchStyle);
                Assert.Equal(loops, hatch.NumberOfLoops);
                Assert.Equal(area, hatch.Area, 6);
                Assert.True(hatch.Associative);
                Assert.Equal(4, hatch.GetAssociatedObjectsAt(0).Count);

                // No loop is flagged Derived: AutoCAD 2021 then wants the pixel size (47), which the library does not write.
                Assert.Equal(DxfHatchLoopTypes.External, hatch.LoopTypeAt(0) & ~DxfHatchLoopTypes.Polyline);
                Assert.All(Enumerable.Range(0, hatch.NumberOfLoops), i =>
                    Assert.Equal(DxfHatchLoopTypes.Default, hatch.LoopTypeAt(i) & DxfHatchLoopTypes.Derived)
                );
                Assert.Equal("ANSI31", hatch.PatternName);
                Assert.Equal(1, hatch.NumberOfPatternDefinitions);
                Assert.Equal("HATCH", document.NextUndo.Name);
                AuditsClean(document);
            }
        }

        [Fact]
        public void HatchByAPointInsideTheIsland_FillsTheIslandAlone_AndAPointOutsideFindsNothing()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "HATCH", P(50, 50));
            Run(session, P(500, 500));
            Assert.Equal("Valid hatch boundary not found.", session.History.Last());
            Run(session, Enter);

            Assert.Equal(3600.0, OnlyHatch(document).Area, 6);
        }

        [Fact]
        public void HatchInACircle_AndInLinesCrossingIt_FindsTheAreaOnTheArcs()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 10.0));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0)));
            CadDocument document = CadDocument.From(database, "circle.dxf");

            Start(document, "HATCH", P(0, 5), Enter);
            DxfHatch half = OnlyHatch(document);
            Assert.Equal(Math.PI * 50.0, half.Area, 9);
            Assert.Equal(2, half.GetAssociatedObjects().Count);

            // Selected, the circle is a loop of its own.
            document.Undo();
            string circle = All<DxfCircle>(document.ModelSpace).Single().Handle;
            Start(document, "HATCH", "S", new[] { circle }, Enter);
            Assert.Equal(Math.PI * 100.0, OnlyHatch(document).Area, 9);
            AuditsClean(document);
        }

        [Fact]
        public void ThePatternLines_AreWrittenForAnsi31AtFortyFive_AndForLinesOfTheUsersOwnDoubled()
        {
            CadDocument document = Squares(0, 0, 30);
            document.HatchSettings.Angle = 45.0;
            Start(document, "HATCH", P(5, 5), Enter);

            DxfPatternDefinition line = Assert.Single(
                Enumerable
                    .Range(0, OnlyHatch(document).NumberOfPatternDefinitions)
                    .Select(OnlyHatch(document).GetPatternDefinitionAt)
            );
            Assert.Equal(Math.PI / 2.0, line.Angle, 12);
            Assert.Equal(-3.175, line.OffsetX, 12);
            Assert.Equal(0.0, line.OffsetY, 12);
            Assert.Equal(Math.PI / 4.0, OnlyHatch(document).PatternAngle, 12);

            document.Undo();
            document.HatchSettings = new CadHatchSettings
            {
                Type = CadHatchType.UserDefined,
                Angle = 30.0,
                Spacing = 5.0,
                Double = true,
            };
            Start(document, "HATCH", P(5, 5), Enter);
            DxfHatch user = OnlyHatch(document);
            Assert.Equal(DxfHatchPatternType.UserDefined, user.PatternType);
            Assert.Equal(5.0, user.PatternSpace);
            Assert.True(user.PatternDouble);
            Assert.Equal(2, user.NumberOfPatternDefinitions);
            DxfPatternDefinition first = user.GetPatternDefinitionAt(0);
            Assert.Equal(-2.5, first.OffsetX, 12);
            Assert.Equal(5.0 * Math.Sqrt(3.0) / 2.0, first.OffsetY, 12);
            Assert.Equal(120.0, user.GetPatternDefinitionAt(1).Angle * 180.0 / Math.PI, 9);
            AuditsClean(document);
        }

        [Fact]
        public void SolidAndGradient_AreWrittenAsTheSettingsSay_AndTheSettingsAreAskedTypedWithNoWindow()
        {
            CadDocument document = Squares(0, 0, 30);
            CadCommandSession session = Start(document, "HATCH", "T");
            Assert.Equal("Enter a pattern name or [?/Solid/User defined/Gradient] <ANSI31>:", session.PromptText);
            Run(session, "S", P(5, 5), Enter);
            Assert.True(OnlyHatch(document).IsSolidFill);
            Assert.False(OnlyHatch(document).IsGradient);
            Assert.Equal(CadHatchType.Solid, document.HatchSettings.Type);

            document.Undo();
            document.HatchSettings = new CadHatchSettings
            {
                Type = CadHatchType.Gradient,
                GradientName = "GR_CYLIN",
                Angle = 30.0,
                GradientCentered = false,
                GradientColor1 = DxfColor.FromRgb(255, 0, 0),
                GradientColor2 = DxfColor.FromRgb(0, 128, 0),
            };
            Start(document, "HATCH", P(5, 5), Enter);
            DxfHatch gradient = OnlyHatch(document);
            Assert.True(gradient.IsGradient);
            Assert.Equal("CYLINDER", gradient.GradientName);
            Assert.Equal(Math.PI / 6.0, gradient.GradientAngle, 12);
            Assert.Equal(0.25f, gradient.GradientShift);
            Assert.Equal(255, gradient.GetGradientColors()[0].Color.Red);
            Assert.Equal(128, gradient.GetGradientColors()[1].Color.Green);

            // The hatch lies behind its boundary, as AutoCAD's HPDRAWORDER of 3 puts it: its fill is drawn first.
            Assert.IsType<DisplayFill2>(document.GetDisplay(document.ModelSpace).Items.First());

            CadHatchSettings read = CadHatchSettings.Of(gradient);
            Assert.Equal("GR_CYLIN", read.GradientName);
            Assert.False(read.GradientCentered);
            AuditsClean(document);
        }

        [Fact]
        public void HatchEdit_ChangesTheScaleAsOneUndoStep_AndRefusesAHatchTheModelOwns()
        {
            CadDocument document = Squares(0, 0, 30);
            Start(document, "HATCH", P(5, 5), Enter);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(3.175 / Math.Sqrt(2.0), -hatch.GetPatternDefinitionAt(0).OffsetX, 9);

            CadCommandSession session = Start(document, "HE", new[] { hatch.Handle });
            Assert.Equal("Enter a pattern name or [?/Solid/User defined/Gradient] <ANSI31>:", session.PromptText);
            Run(session, Enter, "2", Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(2.0, OnlyHatch(document).PatternScale);
            Assert.Equal(6.35 / Math.Sqrt(2.0), -OnlyHatch(document).GetPatternDefinitionAt(0).OffsetX, 9);
            Assert.Equal("HATCHEDIT", document.NextUndo.Name);

            document.Undo();
            Assert.Equal(1.0, OnlyHatch(document).PatternScale);

            // A section hatch of a model drawing may only be moved: HATCHEDIT is refused before it asks anything.
            new CadOwnerTag("Test.Model", "SECTION-1").WriteTo(OnlyHatch(document));
            document.Owners.Register(new MoveOnlyOwner());
            CadCommandSession refused = Start(document, "HATCHEDIT", new[] { OnlyHatch(document).Handle });
            Assert.False(refused.IsRunning);
            Assert.Equal("This hatch belongs to the model and can only be moved.", refused.History.Last());
        }

        [Fact]
        public void Boundary_MakesAClosedPolylineOfTheAreaAndOfItsIsland()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "BO", P(10, 10), Enter);

            Assert.Contains("BOUNDARY created 2 polylines", session.History);
            List<DxfPolyline> made = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(2, made.Count);
            Assert.All(made, p => Assert.True(p.Closed));
            Assert.Equal(
                new[] { 3600.0, 10000.0 },
                made.Select(p => Math.Round(p.ToGeoPolygonArc2().Area, 6)).OrderBy(a => a).ToArray()
            );
        }

        private sealed class MoveOnlyOwner : ICadEntityOwner
        {
            public string Name => "Test.Model";

            public CadEditRule GetEditRule(DxfEntity entity) => CadEditRule.MoveOnly;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }
    }
}
