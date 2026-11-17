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
    /// The tool palettes (TOOLPALETTES, TOOLPALETTESCLOSE): the set and its starter, its JSON file and the import and export of
    /// palettes, cut, copy and paste of tools, tools made from objects of a drawing, and the running of the three kinds of tool
    /// with the layer, colour, linetype and lineweight each sets put back when it ends.
    /// </summary>
    public sealed class ToolPaletteTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "palettes1-tests",
            Guid.NewGuid().ToString("N")
        );

        public ToolPaletteTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private string PathOf(string name) => Path.Combine(_folder, name);

        /// <summary>A square 100 by 100 of four lines on layer 0, so that a hatch dropped inside it has a boundary.</summary>
        private static CadDocument Square()
        {
            var database = new DxfDatabase("AC1027");
            GeoPoint3[] corner =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 100, 0),
                new GeoPoint3(0, 100, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corner[i], corner[(i + 1) % 4]));
            }

            return CadDocument.From(database, "square.dxf");
        }

        /// <summary>A drawing file holding the block TREE, a circle of 5 on layer ROAD, for a block tool to take from.</summary>
        private string TreeFile()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "ROAD", ColorIndex = 3 });
            var tree = new DxfBlockTableRecord { Name = "TREE", Origin = GeoPoint3.Origin };
            database.BlockTable.Add(tree);
            tree.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 5.0) { Layer = "ROAD" });
            string path = PathOf("trees.dxf");
            database.Save(path);
            return path;
        }

        private static CadTool Everything()
        {
            CadTool block = CadTool.ForBlock("Tree", @"C:\lib\trees.dwg", "TREE");
            block.Description = "A tree 3 m high";
            block.Scale = 2.5;
            block.Angle = 90.0;
            block.PromptForScale = true;
            block.PromptForRotation = true;
            block.Explode = true;
            block.Layer = "TREES";
            block.Color = "3";
            block.Linetype = "DASHED";
            block.LineWeight = 25;
            return block;
        }

        [Fact]
        public void TheStarterSet_HasTheSixDocumentedPalettes_AndEveryCommandAndPatternInItExists()
        {
            CadToolPalettes set = CadToolPalettes.CreateStarter();
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal(
                new[] { "Draw", "Modify", "Annotation", "Hatches", "Architectural Hatches", "ISO Hatches" },
                set.Palettes.Select(p => p.Name)
            );
            Assert.False(set.IsModified);
            Assert.Equal("Draw", set.Active.Name);

            foreach (CadTool tool in set.Palettes.SelectMany(p => p.Tools))
            {
                if (tool.Kind == CadToolKind.Command)
                {
                    Assert.NotNull(registry.Find(CadToolRun.Words(tool.Command)[0]));
                    Assert.NotNull(tool.Icon);
                    Assert.NotEqual(string.Empty, tool.Description);
                }
                else if (tool.Kind == CadToolKind.Hatch && tool.HatchType == CadHatchType.Pattern)
                {
                    Assert.NotNull(CadPatternFile.FindBuiltIn(tool.Pattern));
                }
            }

            CadToolPalette iso = set.Palettes.Single(p => p.Name == "ISO Hatches");
            Assert.Equal(14, iso.Tools.Count);
            Assert.Equal("ISO02W100", iso.Tools[0].Name);
            Assert.Equal("ISO15W100", iso.Tools[13].Name);
            Assert.Contains(set.Palettes.Single(p => p.Name == "Hatches").Tools, t =>
                t.Kind == CadToolKind.Hatch && t.HatchType == CadHatchType.Solid
            );
        }

        [Fact]
        public void TheSet_WrittenAndRead_KeepsEveryPaletteToolAndSetting()
        {
            var set = new CadToolPalettes();
            CadToolPalette first = set.Add("Site");
            first.ViewStyle = CadToolViewStyle.List;
            first.IconSize = 48;
            first.Add(Everything());
            first.Add(CadTool.ForText("Notes \"quoted\" and \\ slashed"));
            first.Add(CadTool.ForSeparator());
            CadTool hatch = CadTool.ForHatch("Gravel", "AR-SAND");
            hatch.Angle = 15.0;
            hatch.Scale = 0.5;
            hatch.Associative = false;
            hatch.Layer = "HATCH";
            first.Add(hatch);
            CadTool gradient = CadTool.ForHatch("Fade", "SOLID");
            gradient.HatchType = CadHatchType.Gradient;
            gradient.Gradient = "GR_SPHER";
            first.Add(gradient);
            CadTool circle = CadTool.ForCommand("Circle", "CIRCLE 0,0 50", "Circle");
            circle.Layer = "WALL";
            circle.Color = "ByBlock";
            circle.LineWeight = -2;
            set.Add("Second").ViewStyle = CadToolViewStyle.IconWithText;
            set.Palettes[1].Add(circle);
            set.ActiveIndex = 1;

            Assert.True(CadToolPalettes.TryParse(set.ToJson(), out CadToolPalettes read, out string problem));

            Assert.Null(problem);
            Assert.False(read.IsModified);
            Assert.Equal(1, read.ActiveIndex);
            Assert.Equal(new[] { "Site", "Second" }, read.Palettes.Select(p => p.Name));
            Assert.Equal(CadToolViewStyle.List, read.Palettes[0].ViewStyle);
            Assert.Equal(48, read.Palettes[0].IconSize);
            Assert.Equal(CadToolViewStyle.IconWithText, read.Palettes[1].ViewStyle);
            Assert.Equal(CadToolPalette.DefaultIconSize, read.Palettes[1].IconSize);
            IReadOnlyList<CadTool> tools = read.Palettes[0].Tools;
            Assert.Equal(
                new[]
                {
                    CadToolKind.Block,
                    CadToolKind.Text,
                    CadToolKind.Separator,
                    CadToolKind.Hatch,
                    CadToolKind.Hatch,
                },
                tools.Select(t => t.Kind)
            );

            CadTool tree = tools[0];
            Assert.Equal("Tree", tree.Name);
            Assert.Equal("A tree 3 m high", tree.Description);
            Assert.Equal(@"C:\lib\trees.dwg", tree.File);
            Assert.Equal("TREE", tree.BlockName);
            Assert.Equal(2.5, tree.Scale);
            Assert.Equal(90.0, tree.Angle);
            Assert.True(tree.PromptForScale && tree.PromptForRotation && tree.Explode);
            Assert.Equal("TREES", tree.Layer);
            Assert.Equal("3", tree.Color);
            Assert.Equal("DASHED", tree.Linetype);
            Assert.Equal(25, tree.LineWeight);
            Assert.Equal("Notes \"quoted\" and \\ slashed", tools[1].Name);
            Assert.Equal("AR-SAND", tools[3].Pattern);
            Assert.Equal(15.0, tools[3].Angle);
            Assert.Equal(0.5, tools[3].Scale);
            Assert.False(tools[3].Associative);
            Assert.Equal("HATCH", tools[3].Layer);
            Assert.Null(tools[3].Color);
            Assert.Null(tools[3].LineWeight);
            Assert.Equal(CadHatchType.Gradient, tools[4].HatchType);
            Assert.Equal("GR_SPHER", tools[4].Gradient);
            CadTool again = read.Palettes[1].Tools[0];
            Assert.Equal("CIRCLE 0,0 50", again.Command);
            Assert.Equal("Circle", again.Icon);
            Assert.Equal("ByBlock", again.Color);
            Assert.Equal(-2, again.LineWeight);
            Assert.Equal(set.ToJson(), read.ToJson());
        }

        [Fact]
        public void TheFile_IsWrittenInTwoSpacesOfIndentWithLfAndNoBom_AndReadBackFromDisk()
        {
            CadToolPalettes set = CadToolPalettes.CreateStarter();
            string path = PathOf("sub" + Path.DirectorySeparatorChar + "ToolPalettes.json");

            set.Touch();
            set.Save(path);

            byte[] bytes = File.ReadAllBytes(path);
            Assert.False(set.IsModified);
            Assert.NotEqual(0xEF, bytes[0]);
            string text = File.ReadAllText(path);
            Assert.DoesNotContain("\r", text);
            Assert.StartsWith("{\n  \"format\": \"GeometryHelper.ToolPalettes\",\n  \"version\": 1,", text);
            Assert.Equal(set.ToJson(), CadToolPalettes.Load(path).ToJson());
        }

        [Fact]
        public void AMissingFile_ABrokenFileAndAFileThatIsNotPalettes_EachGiveTheStarterSet()
        {
            string json = PathOf("ToolPalettes.json");
            string expected = CadToolPalettes.CreateStarter().ToJson();

            Assert.Equal(expected, CadToolPalettes.Load(json).ToJson());
            Assert.Equal(expected, CadToolPalettes.Load(null).ToJson());

            File.WriteAllText(json, "this is { not json");
            Assert.Equal(expected, CadToolPalettes.Load(json).ToJson());
            File.WriteAllText(json, "{ \"format\": \"something else\" }");
            Assert.Equal(expected, CadToolPalettes.Load(json).ToJson());
            Assert.False(CadToolPalettes.TryParse("{ \"format\": \"something else\" }", out _, out string problem));
            Assert.Contains("GeometryHelper.ToolPalettes", problem);
        }

        [Fact]
        public void AValueThatIsNotValid_FallsBackToItsDefault_AndAToolOfAnUnknownKindIsLeftOut()
        {
            string json =
                "{ \"format\": \"GeometryHelper.ToolPalettes\", \"version\": 1, \"active\": 9, \"palettes\": ["
                + "{ \"name\": \"Mine\", \"view\": \"nonsense\", \"iconSize\": 5, \"tools\": ["
                + "{ \"kind\": \"block\", \"name\": \"B\", \"block\": \"B\", \"scale\": 0, \"angle\": 30 },"
                + "{ \"kind\": \"hologram\", \"name\": \"H\" },"
                + "{ \"kind\": \"command\", \"name\": \"L\", \"command\": \"LINE\" } ] },"
                + "{ \"name\": \"   \" },"
                + "{ \"name\": \"mine\" } ] }";

            Assert.True(CadToolPalettes.TryParse(json, out CadToolPalettes set, out _));

            Assert.Equal(new[] { "Mine", "mine (2)" }, set.Palettes.Select(p => p.Name));
            Assert.Equal(1, set.ActiveIndex);
            Assert.Equal(CadToolViewStyle.IconOnly, set.Palettes[0].ViewStyle);
            Assert.Equal(CadToolPalette.DefaultIconSize, set.Palettes[0].IconSize);
            Assert.Equal(new[] { "B", "L" }, set.Palettes[0].Tools.Select(t => t.Name));
            Assert.Equal(1.0, set.Palettes[0].Tools[0].Scale);
            Assert.Equal(30.0, set.Palettes[0].Tools[0].Angle);
        }

        [Fact]
        public void PaletteNames_AreKeptApart_IgnoringCase_AndRenamingToItsOwnNameKeepsIt()
        {
            var set = new CadToolPalettes();
            CadToolPalette a = set.Add("Walls");
            CadToolPalette b = set.Add("walls");
            CadToolPalette c = set.Add("  ");
            CadToolPalette d = set.Add(CadToolPalettes.NewPaletteName);

            Assert.Equal(
                new[] { "Walls", "walls (2)", "New Palette", "New Palette (2)" },
                set.Palettes.Select(p => p.Name)
            );
            Assert.True(set.Rename(a, "Walls"));
            Assert.Equal("Walls", a.Name);
            Assert.True(set.Rename(c, "WALLS (2)"));
            Assert.Equal("WALLS (2) (2)", c.Name);
            Assert.True(set.Rename(b, "Doors"));
            Assert.False(set.Rename(b, "   "));
            Assert.False(set.Rename(new CadToolPalette("x"), "Y"));
            Assert.Equal("Doors", b.Name);
            Assert.Equal(3, set.ActiveIndex);
            Assert.Same(d, set.Active);
        }

        [Fact]
        public void RemovingAPalette_KeepsTheOneShownShown_OrTakesTheNextToIt_AndMovingKeepsItToo()
        {
            var set = new CadToolPalettes();
            CadToolPalette a = set.Add("A");
            CadToolPalette b = set.Add("B");
            CadToolPalette c = set.Add("C");
            set.ActiveIndex = 1;

            Assert.True(set.Remove(a));
            Assert.Same(b, set.Active);
            Assert.True(set.Move(c, 0));
            Assert.Equal(new[] { "C", "B" }, set.Palettes.Select(p => p.Name));
            Assert.Same(b, set.Active);
            Assert.True(set.Remove(b));
            Assert.Same(c, set.Active);
            Assert.False(set.Remove(b));
            Assert.True(set.Remove(c));
            Assert.Null(set.Active);
            Assert.Equal(0, set.ActiveIndex);
            Assert.True(set.IsModified);
        }

        [Fact]
        public void CutCopyAndPaste_MoveCopiesOfToolsBetweenPalettes_AndPasteCanRepeat()
        {
            var set = new CadToolPalettes();
            CadToolPalette from = set.Add("From");
            CadToolPalette to = set.Add("To");
            CadTool one = CadTool.ForCommand("One", "LINE");
            CadTool two = CadTool.ForCommand("Two", "CIRCLE");
            CadTool three = CadTool.ForText("Three");
            from.Add(one);
            from.Add(two);
            from.Add(three);

            set.Copy(new[] { one, three });
            IReadOnlyList<CadTool> pasted = set.Paste(to, 0);

            Assert.Equal(new[] { "One", "Three" }, to.Tools.Select(t => t.Name));
            Assert.Equal(new[] { "One", "Two", "Three" }, from.Tools.Select(t => t.Name));
            Assert.NotSame(one, pasted[0]);
            pasted[0].Name = "Changed";
            Assert.Equal("One", one.Name);

            set.Cut(from, new[] { two, CadTool.ForSeparator() });
            set.Paste(to, 1);
            set.Paste(to, 99);

            Assert.Equal(new[] { "One", "Three" }, from.Tools.Select(t => t.Name));
            Assert.Equal(new[] { "Changed", "Two", "Three", "Two" }, to.Tools.Select(t => t.Name));
            Assert.Single(set.Clipboard);
        }

        [Fact]
        public void APaletteExported_IsImportedAfterTheLast_ItsNameMadeUnique_AndShown()
        {
            CadToolPalettes source = CadToolPalettes.CreateStarter();
            string file = PathOf("draw.ghtp");
            CadToolPalettes.Export(file, new[] { source.Palettes[0], source.Palettes[3] });
            CadToolPalettes target = CadToolPalettes.CreateStarter();
            target.ActiveIndex = 2;

            IReadOnlyList<CadToolPalette> added = target.Import(file);

            Assert.Equal(new[] { "Draw (2)", "Hatches (2)" }, added.Select(p => p.Name));
            Assert.Equal(8, target.Palettes.Count);
            Assert.Equal(6, target.ActiveIndex);
            Assert.Equal(source.Palettes[3].Tools.Count, added[1].Tools.Count);
            Assert.Equal(source.Palettes[0].Tools[0].Command, added[0].Tools[0].Command);
            File.WriteAllText(PathOf("bad.ghtp"), "[1, 2]");
            Assert.Throws<FormatException>(() => target.Import(PathOf("bad.ghtp")));
            Assert.Throws<FileNotFoundException>(() => target.Import(PathOf("none.ghtp")));
        }

        [Fact]
        public void AToolMadeFromAnObject_TakesItsCommandAndItsLayerColourLinetypeAndLineweight()
        {
            CadDocument document = Plan();
            var circle = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 7.0)
            {
                Layer = "Steel",
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5),
                LineWeight = DxfLineWeight.LineWeight035,
            };
            document.Database.ModelSpace.AppendEntity(circle);

            CadTool line = CadTool.FromObject(document, Entity<DxfLine>(document, "A1"));
            CadTool made = CadTool.FromObject(document, circle);

            Assert.Equal(CadToolKind.Command, line.Kind);
            Assert.Equal("LINE", line.Command);
            Assert.Equal("Line", line.Icon);
            Assert.Equal("Steel", line.Layer);
            Assert.Equal("ByLayer", line.Color);
            Assert.Equal("ByLayer", line.Linetype);
            Assert.Equal(-1, line.LineWeight);
            Assert.Equal("CIRCLE", made.Command);
            Assert.Equal("5", made.Color);
            Assert.Equal(35, made.LineWeight);
            Assert.Equal("MTEXT", CadTool.FromObject(document, new DxfMText()).Command);
            Assert.Equal("TEXT", CadTool.FromObject(document, Entity<DxfDBText>(document, "A6")).Command);
            Assert.Equal("PLINE", CadTool.FromObject(document, Entity<DxfPolyline>(document, "A5")).Command);
            Assert.Equal("ARC", CadTool.FromObject(document, Entity<DxfArc>(document, "A4")).Command);
        }

        [Fact]
        public void ABlockReferenceBecomesABlockTool_AHatchAHatchTool_AndATrueColourIsLeftUnset()
        {
            CadDocument document = Blank();
            var box = new DxfBlockTableRecord { Name = "BOX", Origin = GeoPoint3.Origin };
            document.Database.BlockTable.Add(box);
            var insert = new DxfBlockReference(GeoPoint3.Origin, box);
            document.Database.ModelSpace.AppendEntity(insert);
            insert.ScaleFactors = new GeoVector3(3, 3, 3);
            insert.Color = DxfColor.FromRgb(10, 20, 30);
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI32");
            hatch.PatternAngle = Math.PI / 4.0;
            hatch.PatternScale = 2.0;
            document.Database.ModelSpace.AppendEntity(hatch);

            CadTool block = CadTool.FromObject(document, insert);
            CadTool tool = CadTool.FromObject(document, hatch);

            Assert.Equal(CadToolKind.Block, block.Kind);
            Assert.Equal("BOX", block.Name);
            Assert.Equal("BOX", block.BlockName);
            Assert.Equal(document.FileName, block.File);
            Assert.Equal(3.0, block.Scale);
            Assert.Null(block.Color);
            Assert.Equal(CadToolKind.Hatch, tool.Kind);
            Assert.Equal("ANSI32", tool.Name);
            Assert.Equal("ANSI32", tool.Pattern);
            Assert.Equal(CadHatchType.Pattern, tool.HatchType);
            Assert.Equal(45.0, tool.Angle, 9);
            Assert.Equal(2.0, tool.Scale);
        }

        [Fact]
        public void NoToolDrawsAViewportOrATable_AndEverythingNullIsRefused()
        {
            CadDocument document = Plan();

            Assert.Null(CadTool.FromObject(document, new DxfViewport()));
            Assert.Throws<ArgumentNullException>(() => CadTool.FromObject(null, new DxfLine()));
            Assert.Throws<ArgumentNullException>(() => CadTool.FromObject(document, null));
            Assert.Throws<ArgumentNullException>(() => CadTool.ForCommand(null, "LINE"));
            Assert.Throws<ArgumentNullException>(() => CadTool.ForCommand("x", null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CadTool.ForSeparator().Scale = 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => CadTool.ForSeparator().Angle = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CadToolPalette("x").IconSize = 8);
            Assert.Throws<ArgumentException>(() => new CadToolPalette(" "));
        }

        [Fact]
        public void ACommandTool_StartsItsCommand_WithItsLayerColourLinetypeAndLineweightCurrent_AndPutsTheDrawingsOwnBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForCommand("Wall", "LINE");
            tool.Layer = "WALLS";
            tool.Color = "1";
            tool.LineWeight = 50;

            Assert.True(session.StartTool(tool));

            Assert.True(session.IsRunning);
            Assert.Equal("WALLS", document.Database.Clayer);
            Assert.Equal(1, document.Database.Cecolor.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, document.Database.Celweight);
            Run(session, P(0, 0), P(10, 0), Enter);

            DxfLine drawn = document.Database.ModelSpace.OfType<DxfLine>().Last();
            Assert.Equal("WALLS", drawn.Layer);
            Assert.Equal(1, drawn.Color.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, drawn.LineWeight);
            Assert.False(session.IsRunning);
            Assert.Equal("0", document.Database.Clayer);
            Assert.True(document.Database.Cecolor.IsByLayer);
            Assert.Equal(DxfLineWeight.ByLayer, document.Database.Celweight);
            Assert.True(document.Database.LayerTable.Has("WALLS"));
        }

        [Fact]
        public void ACommandTool_Cancelled_AlsoPutsTheDrawingsOwnBack_AndTheLayerItMadeGoesWithAnUndo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForCommand("Wall", "^C^C_LINE");
            tool.Layer = "WALLS";
            tool.Linetype = "ByBlock";

            Assert.True(session.StartTool(tool));
            Assert.Equal("ByBlock", document.Database.Celtype);
            Run(session, P(0, 0), Esc);

            Assert.False(session.IsRunning);
            Assert.Equal("0", document.Database.Clayer);
            Assert.Equal("ByLayer", document.Database.Celtype);
        }

        [Fact]
        public void ACommandLineOfSeveralWords_FeedsTheAnswersToTheFirstPrompts()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Assert.True(session.StartTool(CadTool.ForCommand("Disc", "CIRCLE 10,20;50")));

            DxfCircle made = document.Database.ModelSpace.OfType<DxfCircle>().Last();
            Assert.Equal(50.0, made.Radius, 9);
            Assert.Equal(10.0, made.Center.X, 9);
            Assert.Equal(20.0, made.Center.Y, 9);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AToolThatCannotRun_SaysWhy_AndStartsNothing()
        {
            CadCommandSession session = Session(Plan());

            Assert.False(session.StartTool(CadTool.ForCommand("None", "  ")));
            Assert.Equal("The tool None has no command.", session.History.Last());
            Assert.False(session.StartTool(CadTool.ForCommand("Odd", "NOSUCHCOMMAND")));
            Assert.Contains("Unknown command", session.History.Last());
            Assert.False(session.StartTool(CadTool.ForText("Heading")));
            Assert.False(session.StartTool(CadTool.ForSeparator()));
            Assert.False(session.StartTool(CadTool.ForBlock("Ghost", null, "GHOST")));
            Assert.Equal("The drawing has no block GHOST.", session.History.Last());
            Assert.False(session.StartTool(CadTool.ForBlock("Ghost", PathOf("missing.dxf"), "GHOST")));
            Assert.Equal(PathOf("missing.dxf") + " cannot be found.", session.History.Last());
            Assert.False(session.IsRunning);
            Assert.Throws<ArgumentNullException>(() => session.StartTool(null));
        }

        [Fact]
        public void ALayerTheToolNamesThatIsFrozenOrInvalid_IsSaid_AndTheCurrentLayerIsUsed()
        {
            CadDocument document = Plan();
            document.Database.LayerTable["Steel"].IsFrozen = true;
            CadCommandSession session = Session(document);
            CadTool frozen = CadTool.ForCommand("Steel", "LINE");
            frozen.Layer = "Steel";

            Assert.True(session.StartTool(frozen));
            Assert.Equal("0", document.Database.Clayer);
            Assert.Contains("cannot be current", session.History.Reverse().First(l => l.Contains("Steel")));
            Run(session, Esc);

            CadTool invalid = CadTool.ForCommand("Odd", "LINE");
            invalid.Layer = "a|b";
            invalid.Color = "chartreuse";
            invalid.Linetype = "NOSUCH";
            Assert.True(session.StartTool(invalid));
            Assert.Equal("0", document.Database.Clayer);
            Assert.Contains(session.History, l =>
                l.StartsWith("The layer a|b of the tool is not used", StringComparison.Ordinal)
            );
            Assert.Contains(session.History, l =>
                l.StartsWith("The colour chartreuse of the tool is not used", StringComparison.Ordinal)
            );
            Assert.Contains(session.History, l =>
                l.StartsWith("Linetype NOSUCH of the tool is not loaded", StringComparison.Ordinal)
            );
            Run(session, Esc);
        }

        [Fact]
        public void AHatchTool_StartsHatchWithItsPatternAngleAndScale_AndTheSettingsAreTheDrawingsAgainAfterwards()
        {
            CadDocument document = Square();
            CadHatchSettings before = document.HatchSettings;
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForHatch("Steel", "ANSI32");
            tool.Angle = 45.0;
            tool.Scale = 2.0;
            tool.Layer = "HATCHES";

            Assert.True(session.StartTool(tool));

            Assert.Equal("HATCH", session.Current.Name);
            Assert.NotSame(before, document.HatchSettings);
            Assert.Equal("ANSI32", document.HatchSettings.PatternName);
            Assert.Equal(45.0, document.HatchSettings.Angle);
            Assert.Equal(2.0, document.HatchSettings.Scale);
            Assert.Equal("HATCHES", document.Database.Clayer);
            Assert.Equal("ANSI31", before.PatternName);
            Run(session, P(50, 50), Enter);

            DxfHatch hatch = document.Database.ModelSpace.OfType<DxfHatch>().Single();
            Assert.Equal("ANSI32", hatch.PatternName);
            Assert.Equal(2.0, hatch.PatternScale, 9);
            Assert.Equal(Math.PI / 4.0, hatch.PatternAngle, 9);
            Assert.Equal("HATCHES", hatch.Layer);
            Assert.Same(before, document.HatchSettings);
            Assert.Equal("0", document.Database.Clayer);
        }

        [Fact]
        public void AHatchToolDroppedInsideAnArea_HatchesItAtOnce_AndASolidToolMakesASolidFill()
        {
            CadDocument document = Square();
            CadCommandSession session = Session(document);

            Assert.True(session.StartTool(CadTool.ForHatch("Solid", "SOLID"), P(40, 40)));

            DxfHatch hatch = document.Database.ModelSpace.OfType<DxfHatch>().Single();
            Assert.True(hatch.IsSolidFill);
            Assert.False(session.IsRunning);
            Assert.Equal("ANSI31", document.HatchSettings.PatternName);
            Assert.Equal(CadHatchType.Pattern, document.HatchSettings.Type);
        }

        [Fact]
        public void AGradientToolMakesAGradientHatch()
        {
            CadDocument document = Square();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForHatch("Fade", "SOLID");
            tool.HatchType = CadHatchType.Gradient;
            tool.Gradient = "GR_SPHER";

            Assert.True(session.StartTool(tool, P(40, 40)));

            DxfHatch hatch = document.Database.ModelSpace.OfType<DxfHatch>().Single();
            Assert.True(hatch.IsGradient);
            Assert.Equal("SPHERICAL", hatch.GradientName);
        }

        [Fact]
        public void ABlockToolDroppedAtAPoint_PlacesTheBlockThereWithItsScaleAndRotation_AskingNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForBlock("Box", null, "BOX");
            tool.Scale = 2.0;
            tool.Angle = 90.0;
            tool.Layer = "Steel";

            Assert.True(session.StartTool(tool, P(10, 20)));

            Assert.False(session.IsRunning);
            DxfBlockReference placed = document.Database.ModelSpace.OfType<DxfBlockReference>().Last();
            Assert.NotEqual("A7", placed.Handle);
            Assert.Equal("BOX", placed.Name);
            Assert.Equal(10.0, placed.Position.X, 9);
            Assert.Equal(20.0, placed.Position.Y, 9);
            Assert.Equal(2.0, placed.ScaleFactors.X, 9);
            Assert.Equal(2.0, placed.ScaleFactors.Y, 9);
            Assert.Equal(Math.PI / 2.0, placed.Rotation, 9);
            Assert.Equal("Steel", placed.Layer);
            Assert.Equal("0", document.Database.Clayer);
        }

        [Fact]
        public void ABlockToolClicked_AsksTheInsertionPoint_AndTheScaleAndRotationWhereItPromptsForThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForBlock("Box", null, "BOX");
            tool.PromptForScale = true;
            tool.PromptForRotation = true;
            tool.Explode = true;

            Assert.True(session.StartTool(tool));

            Assert.True(session.IsRunning);
            Assert.StartsWith("Specify insertion point", session.PromptText, StringComparison.Ordinal);
            session.Feed(P(300, 300));
            Assert.Contains("scale factor", session.PromptText);
            session.Feed("3");
            Assert.Contains("rotation", session.PromptText);
            session.Feed("0");

            Assert.False(session.IsRunning);
            Assert.Equal(
                4,
                document.Database.ModelSpace.OfType<DxfLine>().Count(l => l.Handle != null && l.StartPoint.X >= 300)
            );
        }

        [Fact]
        public void ABlockToolOfAnotherDrawing_BringsTheBlockInFromItsFile_AndUsesTheDrawingsOwnOnceItHasIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadTool tool = CadTool.ForBlock("Tree", TreeFile(), "TREE");

            Assert.False(document.Database.BlockTable.Has("TREE"));
            Assert.True(session.StartTool(tool, P(5, 5)));
            Assert.True(document.Database.BlockTable.Has("TREE"));
            File.Delete(TreeFile());
            Assert.True(session.StartTool(tool, P(50, 50)));

            Assert.Equal(2, document.Database.ModelSpace.OfType<DxfBlockReference>().Count());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AToolRunWhileACommandIsRunning_EndsThatCommandFirst_AsEscWould()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0));
            Assert.True(session.IsRunning);

            Assert.True(session.StartTool(CadTool.ForCommand("Circle", "CIRCLE")));

            Assert.Equal("CIRCLE", session.Current.Name);
        }

        [Fact]
        public void ATextAndASeparatorAreNeverRunAndCarryNoOverrides()
        {
            Assert.False(CadTool.ForText("x").HasOverrides);
            CadTool line = CadTool.ForCommand("Line", "LINE");
            Assert.False(line.HasOverrides);
            line.LineWeight = -3;
            Assert.True(line.HasOverrides);
            CadTool copy = line.Clone();
            copy.Name = "Other";
            Assert.Equal("Line", line.Name);
            Assert.Equal(-3, copy.LineWeight);
            Assert.Equal(CadToolKind.Command, copy.Kind);
        }

        [Fact]
        public void ToolPalettesAndToolPalettesclose_AskTheWindow_ThroughTheAliasTp()
        {
            var asked = new List<string>();
            CadCommandSession session = Session(Plan());
            session.UiRequested += (sender, e) =>
            {
                asked.Add(e.Name);
                e.Handled = true;
            };

            Run(session, "TOOLPALETTES", "TP", "TOOLPALETTESCLOSE");

            Assert.Equal(new[] { "TOOLPALETTES", "TOOLPALETTES", "TOOLPALETTESCLOSE" }, asked);
        }

        [Fact]
        public void WithNoWindow_TheCommandsSayThereIsNoPalette()
        {
            CadCommandSession session = Run(Session(Plan()), "TOOLPALETTES");
            Assert.Equal(
                "The tool palettes are a palette of the window; there is none to show.",
                session.History.Last()
            );
            Run(session, "TOOLPALETTESCLOSE");
            Assert.Equal(
                "The tool palettes are a palette of the window; there is none to close.",
                session.History.Last()
            );
        }

        [Fact]
        public void APaletteOfBlocks_HasABlockToolForEachNameTakingTheDrawingAsItsSource()
        {
            var set = new CadToolPalettes();

            CadToolPalette palette = set.AddBlocks("trees.dxf", @"C:\lib\trees.dxf", new[] { "TREE", "BUSH" });

            Assert.Equal(new[] { "TREE", "BUSH" }, palette.Tools.Select(t => t.Name));
            Assert.All(palette.Tools, t => Assert.Equal(CadToolKind.Block, t.Kind));
            Assert.All(palette.Tools, t => Assert.Equal(@"C:\lib\trees.dxf", t.File));
            Assert.Equal(new[] { "TREE", "BUSH" }, palette.Tools.Select(t => t.BlockName));
            Assert.Same(palette, set.Active);
        }
    }
}
