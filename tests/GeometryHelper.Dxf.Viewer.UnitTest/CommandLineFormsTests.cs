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
    /// Batch 4 of acad.pgp's 2D commands, the command-line forms and SAVE: -PAN, -MTEXT, -LAYOUT, -XBIND and SAVE, in the
    /// words of AutoCAD 2021 (the prompts and the messages were read from its console by accoreconsole probes).
    /// </summary>
    public class CommandLineFormsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "forms-" + Guid.NewGuid().ToString("N")
        );

        public CommandLineFormsTests()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "xref"));
            Directory.CreateDirectory(Path.Combine(_folder, "plans"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Said(CadCommandSession session) => string.Join("\n", session.History);

        private static List<string> Names(CadDocument document) => document.Spaces.Select(s => s.Name).ToList();

        private static CadView ViewOf(CadDocument document, CadCommandSession session)
        {
            var view = new CadView(1000, 500);
            session.View = view;
            return view;
        }

        #region -PAN

        [Fact]
        public void MinusPan_AsksTheBasePointAndTheSecondPoint_AndPansAsPanDoes()
        {
            CadDocument typed = Blank();
            CadCommandSession session = Session(typed);
            CadView typedView = ViewOf(typed, session);
            Run(session, "LINE", P(0, 0), P(100, 50), Enter);

            Run(session, "-PAN");
            Assert.Equal("Specify base point or displacement:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, P(10, 0));
            Assert.False(session.IsRunning);

            CadDocument plain = Blank();
            CadCommandSession other = Session(plain);
            CadView plainView = ViewOf(plain, other);
            Run(other, "LINE", P(0, 0), P(100, 50), Enter);
            Run(other, "PAN", P(0, 0), P(10, 0));

            Assert.Equal(plainView.CenterX, typedView.CenterX, 9);
            Assert.Equal(plainView.CenterY, typedView.CenterY, 9);
            Assert.NotEqual(plainView.CenterX, new CadView(1000, 500).CenterX);
        }

        [Fact]
        public void MinusPan_RefusesEnterAtTheBasePoint_AndAsksAgain_WherePanEndsAtIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            ViewOf(document, session);

            Run(session, "-PAN", Enter);
            Assert.Contains("Requires two points, or a displacement followed by ENTER.", session.History);
            Assert.True(session.IsRunning);
            Assert.Equal("Specify base point or displacement:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);

            Run(session, "PAN", Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(
                1,
                Said(session).Split('\n').Count(l => l.StartsWith("Requires two points", StringComparison.Ordinal))
            );
        }

        [Fact]
        public void MinusPan_TakesADisplacementAsPanDoes()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(document, session);
            double center = view.CenterX;

            Run(session, "-PAN", P(10, 0), Enter);

            Assert.False(session.IsRunning);
            Assert.NotEqual(center, view.CenterX);
        }

        #endregion

        #region -MTEXT

        [Fact]
        public void MinusMtext_AsksTheCornersAndOptionsOfMtext_ThenTheLinesTyped_UntilABlankOne()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-MTEXT");
            Assert.Equal("Specify first corner:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal(
                "Specify opposite corner or [Height/Justify/Line spacing/Rotation/Style/Width/Columns]:",
                session.PromptText
            );
            Run(session, P(100, -50));
            Assert.Equal("MText:", session.PromptText);
            Run(session, "Hello");
            Assert.Equal("MText:", session.PromptText);
            Run(session, "World");
            Assert.Equal("MText:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            DxfMText text = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal("Hello\\PWorld", text.Contents);
            Assert.Equal(100.0, text.Width, 9);
            Assert.Equal(DxfAttachmentPoint.TopLeft, text.Attachment);
            Near(new GeoPoint3(0, 0, 0), text.Location);
            AssertReopens(document);
        }

        [Fact]
        public void MinusMtext_TakesTheOptionsOfMtext_HeightAndWidthAndJustification()
        {
            CadDocument document = Blank();
            Start(document, "-T", P(0, 100), "H", "7", "J", "MC", "W", "80", "line one", Enter);

            DxfMText text = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal("line one", text.Contents);
            Assert.Equal(7.0, text.TextHeight, 9);
            Assert.Equal(80.0, text.Width, 9);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, text.Attachment);
            Near(new GeoPoint3(0, 100, 0), text.Location);
        }

        [Fact]
        public void MinusMtext_WithNoLineTyped_MakesAnMtextOfNoWords_AsAutoCadDoes_OneUndoStep()
        {
            CadDocument document = Blank();
            string before = document.Database.ToText();
            Start(document, "-MTEXT", P(0, 200), P(100, 150), Enter);

            DxfMText text = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal(string.Empty, text.Contents);
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfMText>(document.ModelSpace));
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void Mtext_StillAsksItsWordsInTheEditor_AndMakesNothingOfAnEmptyAnswer()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MTEXT", P(0, 0), P(100, -50));
            Assert.Equal("Enter text:", session.PromptText);
            Run(session, Enter);

            Assert.Empty(All<DxfMText>(document.ModelSpace));
        }

        #endregion

        #region -LAYOUT

        /// <summary>A drawing with the sheet Layout1 holding a line from (10, 10) to (110, 60), and the block name it has.</summary>
        private static CadDocument Sheets()
        {
            var database = new DxfDatabase("AC1027");
            DxfLayout layout = database.Layouts.First(l => l.LayoutName == "Layout1");
            layout.Initialize();
            layout.BlockTableRecord.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(110, 60, 0)));
            return CadDocument.From(database, "sheets.dxf");
        }

        [Fact]
        public void MinusLayout_OffersAutoCadsOptions_SetTakenByEnter()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LO");

            Assert.Equal(
                "Enter layout option [Copy/Delete/New/Template/Rename/SAveas/Set/?] <set>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.Equal("Enter layout to make current <Model>:", session.PromptText);
            Run(session, "layout1");
            Assert.Equal("Layout1", document.CurrentSpace.Name);

            Run(session, "-LAYOUT", "S", "Nowhere");
            Assert.Equal("Cannot find layout \"Nowhere\".", session.History.Last());
            Run(session, "-LAYOUT", "S", "Model");
            Assert.Same(document.ModelSpace, document.CurrentSpace);
        }

        [Fact]
        public void MinusLayout_QuestionMark_ListsTheLayoutsByNameWithTheirBlocks()
        {
            CadDocument document = Sheets();
            Start(document, "-LAYOUT", "N", "Plan");
            string first = document.Spaces.Single(s => s.Name == "Layout1").Block.Name;
            string second = document.Spaces.Single(s => s.Name == "Plan").Block.Name;

            CadCommandSession session = Start(document, "-LAYOUT", "?");

            Assert.False(session.IsRunning);
            string[] listed = session.History.Skip(session.History.Count - 4).ToArray();
            Assert.Equal(
                new[]
                {
                    "Active Layouts:",
                    string.Empty,
                    "Layout: Layout1       Block name: " + first + ".",
                    "Layout: Plan          Block name: " + second + ".",
                },
                listed
            );
        }

        [Fact]
        public void MinusLayout_New_OffersTheFirstLayoutNNotTaken_AndSaysNothingOfTheMaking()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "N");
            Assert.Equal("Enter new Layout name <Layout2>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal(new[] { "Model", "Layout1", "Layout2" }, Names(document));
            Assert.DoesNotContain(session.History, l => l.StartsWith("Layout \"", StringComparison.Ordinal));
            Assert.NotNull(document.Spaces[2].Layout.GetViewports().FirstOrDefault(v => v.Number == 1));

            Start(document, "-LAYOUT", "N", "Plan");
            Assert.Equal(new[] { "Model", "Layout1", "Layout2", "Plan" }, Names(document));

            // Layout2 gone, the first free number is 2 again.
            Run(session, "-LAYOUT", "D", "Layout2");
            Run(session, "-LAYOUT", "N");
            Assert.Equal("Enter new Layout name <Layout2>:", session.PromptText);
            Run(session, Esc);
            AssertReopens(document);
        }

        [Fact]
        public void MinusLayout_New_RefusesANameTakenOrOneAutoCadDoesNotTake()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "N", "LAYOUT1");
            Assert.Equal("Layout \"LAYOUT1\" already exists, not created.", session.History.Last());

            Run(session, "-LAYOUT", "N", "a/b");
            Assert.Equal(
                new[]
                {
                    "Invalid characters in Layout name: \"a/b\".",
                    "The following characters are not valid in layout names:  <>/\\\":;?*|,=`",
                },
                session.History.Skip(session.History.Count - 2)
            );

            Run(session, "-LAYOUT", "N", new string('q', 256));
            Assert.Equal("Invalid layout name.", session.History.Last());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
        }

        [Fact]
        public void MinusLayout_Rename_AsksTheLayoutThenTheNewName_AndAllowsAChangeOfCase()
        {
            CadDocument document = Sheets();
            Start(document, "-LAYOUT", "N", "Z");
            CadCommandSession session = Start(document, "-LAYOUT", "R");
            Assert.Equal("Enter layout to rename <Model>:", session.PromptText);
            Run(session, "Layout1");
            Assert.Equal("Enter new layout name:", session.PromptText);
            Run(session, "Sheet1");

            Assert.Equal("Layout \"Layout1\" renamed to \"Sheet1\".", session.History.Last());
            Assert.Equal(new[] { "Model", "Sheet1", "Z" }, Names(document));
            Assert.Equal("Sheet1", document.Spaces[1].Layout.LayoutName);

            Run(session, "-LAYOUT", "R", "Sheet1", "SHEET1");
            Assert.Equal("Layout \"Sheet1\" renamed to \"SHEET1\".", session.History.Last());
            Run(session, "-LAYOUT", "R", "SHEET1", "z");
            Assert.Equal("Layout \"z\" already exists, not renamed.", session.History.Last());
            Run(session, "-LAYOUT", "R", "Nope");
            Assert.Equal("Layout \"Nope\" does not exist.", session.History.Last());
            Run(session, "-LAYOUT", "R", "Model");
            Assert.Equal("May not rename Model Tab.", session.History.Last());
            Run(session, "-LAYOUT", "R", "SHEET1", Enter);
            Assert.Equal("Invalid layout name.", session.History.Last());
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "Model", "Sheet1", "Z" }, Names(document));
        }

        [Fact]
        public void MinusLayout_Copy_MakesALayoutOfTheSheetAndItsEntities_AsOneUndoStep()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "C");
            Assert.Equal("Enter name of layout to copy <Model>:", session.PromptText);
            Run(session, "Layout1");
            Assert.Equal("Enter layout name for copy <Layout1 (2)>:", session.PromptText);
            string before = document.Database.ToText();
            Run(session, Enter);

            Assert.Equal("Layout \"Layout1\" copied to \"Layout1 (2)\".", session.History.Last());
            Assert.Equal(new[] { "Model", "Layout1", "Layout1 (2)" }, Names(document));
            CadSpace original = document.Spaces[1];
            CadSpace copy = document.Spaces[2];
            DxfLine line = Assert.Single(All<DxfLine>(copy));
            Near(new GeoPoint3(10, 10, 0), line.StartPoint);
            Near(new GeoPoint3(110, 60, 0), line.EndPoint);
            Assert.NotEqual(All<DxfLine>(original)[0].Handle, line.Handle);
            Assert.Equal(original.Layout.GetViewports().Count, copy.Layout.GetViewports().Count);
            Assert.Equal(original.Layout.PlotPaperSize, copy.Layout.PlotPaperSize);
            Assert.NotEqual(original.Block.Name, copy.Block.Name);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
            Assert.Equal(before, document.Database.ToText());
            Assert.True(document.Redo());
            Assert.Equal(new[] { "Model", "Layout1", "Layout1 (2)" }, Names(document));
        }

        [Fact]
        public void MinusLayout_Copy_NamesTheCopyPastOneTaken_AndRefusesWhatAutoCadRefuses()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "C", "Layout1", Enter);
            Run(session, "-LAYOUT", "C", "Layout1");
            Assert.Equal("Enter layout name for copy <Layout1 (3)>:", session.PromptText);
            Run(session, "layout1 (2)");
            Assert.Equal("Layout \"layout1 (2)\" already exists, not copied.", session.History.Last());

            Run(session, "-LAYOUT", "C", "Nope");
            Assert.Equal("Layout \"Nope\" does not exist.", session.History.Last());
            Run(session, "-LAYOUT", "C", "Model");
            Assert.Equal("May not copy Model tab.", session.History.Last());
            Run(session, "-LAYOUT", "C", "Layout1", "a<b");
            Assert.Equal(
                "The following characters are not valid in layout names:  <>/\\\":;?*|,=`",
                session.History.Last()
            );
            Assert.Equal(new[] { "Model", "Layout1", "Layout1 (2)" }, Names(document));
        }

        [Fact]
        public void MinusLayout_Delete_TakesTheLayoutAndItsBlockOut_AsOneUndoStep()
        {
            CadDocument document = Sheets();
            Start(document, "-LAYOUT", "N", "Plan");
            string before = document.Database.ToText();
            string block = document.Spaces.Single(s => s.Name == "Plan").Block.Name;
            CadCommandSession session = Start(document, "-LAYOUT", "D");
            Assert.Equal("Enter name of layout to delete <Model>:", session.PromptText);
            Run(session, "plan");

            Assert.Equal("Layout \"plan\" deleted.", session.History.Last());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
            Assert.False(document.Database.BlockTable.Has(block));
            Assert.DoesNotContain(document.Database.Layouts, l => l.LayoutName == "Plan");
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "Model", "Layout1", "Plan" }, Names(document));
            Assert.Equal(before, document.Database.ToText());
            Assert.True(document.Redo());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
        }

        [Fact]
        public void MinusLayout_Delete_OfTheLayoutShown_ShowsAnotherOne_AndOfTheLastSheetLeavesANewLayout1()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "N", "Plan");
            Run(session, "-LAYOUT", "S", "Plan");
            Assert.Equal("Plan", document.CurrentSpace.Name);

            Run(session, "-LAYOUT", "D", "Plan");
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
            Assert.Contains(document.CurrentSpace, document.Spaces);

            Run(session, "-LAYOUT", "D", "Layout1");
            Assert.Equal("Layout \"Layout1\" deleted.", session.History.Last());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
            Assert.Empty(All<DxfLine>(document.Spaces[1]));
            Assert.NotNull(document.Spaces[1].Layout.GetViewports().FirstOrDefault(v => v.Number == 1));
            AssertReopens(document);
        }

        [Fact]
        public void MinusLayout_Delete_RefusesModelAndANameNotThere()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "D", "Model");
            Assert.Equal("May not delete Model tab.", session.History.Last());
            Run(session, "-LAYOUT", "D", "Nope");
            Assert.Equal("Cannot find layout \"Nope\", nothing deleted.", session.History.Last());
            Assert.Equal(new[] { "Model", "Layout1" }, Names(document));
        }

        [Fact]
        public void MinusLayout_TemplateAndSaveas_SayTheyAreNotBuilt_AndTheAliasReachesTheCommand()
        {
            CadDocument document = Sheets();
            CadCommandSession session = Start(document, "-LAYOUT", "T");
            Assert.False(session.IsRunning);
            Assert.Equal("A layout is not brought in from a template file here.", session.History.Last());
            Run(session, "-LAYOUT", "SA");
            Assert.False(session.IsRunning);
            Assert.Equal("A layout is not saved to a template file (*.dwt) here.", session.History.Last());

            Assert.Equal("-LAYOUT", CadShortcuts.Default.Aliases["-LO"]);
            Assert.Equal("-LAYOUT", CadCommandRegistry.CreateDefault().Find("-LO").Name);
            Assert.Equal("LAYOUT", CadCommandRegistry.CreateDefault().Find("LO").Name);
        }

        [Fact]
        public void Layout_StillAsksItsOwnOptions_NewTheDefault()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LAYOUT");

            Assert.Equal("Enter layout option [New/Rename/Set/?] <New>:", session.PromptText);
        }

        #endregion

        #region -XBIND

        /// <summary>
        /// A metric drawing of a line on the layer GRID, its linetype DASHED, and an INSERT of the block TAG, a line from (0, 0)
        /// to (0, 10) on layer 0, saved in the folder xref as grid.dxf.
        /// </summary>
        private string Grid()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var dashed = new DxfLinetypeTableRecord
            {
                Name = "DASHED",
                AsciiDescription = "Dashed __ __ __",
                NumDashes = 2,
            };
            dashed.SetDashLengthAt(0, 12.7);
            dashed.SetDashLengthAt(1, -6.35);
            database.LinetypeTable.Add(dashed);
            database.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "GRID",
                    ColorIndex = 3,
                    LinetypeName = "DASHED",
                }
            );
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "AXIS", ColorIndex = 5 });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 0, 0), tag));
            string path = Path.Combine(_folder, "xref", "grid.dxf");
            database.Save(path);
            return path;
        }

        /// <summary>A metric drawing saved as host.dxf and opened, with grid.dxf attached at (0, 0).</summary>
        private CadDocument Host(out CadCommandSession session)
        {
            string grid = Grid();
            DxfDatabase database = CadTemplate.CreateMetric();
            string path = Path.Combine(_folder, "plans", "host.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);
            return document;
        }

        [Fact]
        public void MinusXbind_AsksTheKindOfSymbolThenItsNames_AndBindsALayerAsGridDollarZeroDollarGrid()
        {
            CadDocument document = Host(out CadCommandSession session);
            string attached = document.Database.ToText();
            Assert.True(document.Database.LayerTable["grid|GRID"].IsDependent);

            Run(session, "-XBIND");
            Assert.Equal("Enter symbol type to bind [Block/Dimstyle/LAyer/LType/Style]:", session.PromptText);
            Run(session, "LA");
            Assert.Equal("Enter dependent Layer name(s):", session.PromptText);
            Run(session, "grid|GRID");

            Assert.Equal("1 Layer(s) bound.", session.History.Last());
            Assert.Equal("Enter symbol type to bind [Block/Dimstyle/LAyer/LType/Style]:", session.PromptText);
            Assert.False(document.Database.LayerTable.Has("grid|GRID"));
            DxfLayerTableRecord layer = document.Database.LayerTable["grid$0$GRID"];
            Assert.False(layer.IsDependent);
            Assert.Equal(3, layer.ColorIndex);
            Assert.True(document.Database.LayerTable.Has("grid|AXIS"));

            // The reference is still drawn.
            Assert.NotEmpty(document.GetDisplay(document.ModelSpace).Items);

            Run(session, Enter);
            Assert.False(session.IsRunning);
            AssertReopens(document);
            Assert.Equal("-XBIND", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(attached, document.Database.ToText());
            Assert.True(document.Redo());
            Assert.True(document.Database.LayerTable.Has("grid$0$GRID"));
        }

        [Fact]
        public void MinusXbind_Linetypes_ArePickedByWildcards_TheLayersOnThemFollowingTheNewName()
        {
            CadDocument document = Host(out CadCommandSession session);
            Assert.Equal("grid|DASHED", document.Database.LayerTable["grid|GRID"].LinetypeName);

            Run(session, "-XBIND", "LT", "grid|D*");
            Assert.Equal("1 Linetype(s) bound.", session.History.Last());
            Assert.True(document.Database.LinetypeTable.Has("grid$0$DASHED"));
            Assert.False(document.Database.LinetypeTable.Has("grid|DASHED"));
            Assert.Equal("grid$0$DASHED", document.Database.LayerTable["grid|GRID"].LinetypeName);

            Run(session, "LA", "grid|GRID,GRID,grid|AXIS");
            Assert.Equal("2 Layer(s) bound.", session.History.Last());
            Assert.Equal("grid$0$DASHED", document.Database.LayerTable["grid$0$GRID"].LinetypeName);
            Assert.Equal(5, document.Database.LayerTable["grid$0$AXIS"].ColorIndex);
            AssertReopens(document);
        }

        [Fact]
        public void MinusXbind_SaysNoMatchingNamesFound_ForAnOwnSymbolOrAnyOtherName_AndAsksTheKindAgain()
        {
            CadDocument document = Host(out CadCommandSession session);
            string attached = document.Database.ToText();

            Run(session, "-XBIND", "LA", "0");
            Assert.Equal("No matching Layer names found.", session.History.Last());
            Assert.Equal("Enter symbol type to bind [Block/Dimstyle/LAyer/LType/Style]:", session.PromptText);
            Run(session, "LA", "grid|NOPE");
            Assert.Equal("No matching Layer names found.", session.History.Last());
            Run(session, "S", "grid|*");
            Assert.Equal("No matching Style names found.", session.History.Last());
            Run(session, "D", "*");
            Assert.Equal("No matching Dimstyle names found.", session.History.Last());
            Run(session, "B", "NOPE");
            Assert.Equal("No matching Block names found.", session.History.Last());
            Run(session, "LA", "grid|GRID");
            Run(session, "LA", "grid|GRID");
            Assert.Equal("No matching Layer names found.", session.History.Last());
            Run(session, Enter);
            Assert.False(session.IsRunning);

            // Only the one binding was a step; the others changed nothing.
            Assert.True(document.Undo());
            Assert.Equal(attached, document.Database.ToText());
        }

        [Fact]
        public void MinusXbind_Block_MakesTheBlockOfTheDrawingReferencedAGridDollarZeroDollarTag_ItsEntitiesCopied()
        {
            CadDocument document = Host(out CadCommandSession session);
            string attached = document.Database.ToText();

            Run(session, "-XBIND", "B", "grid|TAG");

            Assert.Equal("1 Block(s) bound.", session.History.Last());
            DxfBlockTableRecord bound = document.Database.BlockTable["grid$0$TAG"];
            Assert.False(bound.IsDependent);
            DxfLine line = Assert.IsType<DxfLine>(Assert.Single(bound.Cast<DxfEntity>()));
            Near(new GeoPoint3(0, 10, 0), line.EndPoint);
            Assert.True(document.Database.BlockTable["grid"].IsFromExternalReference);
            AssertReopens(document);

            Run(session, "B", "grid|TAG");
            Assert.Equal("1 Block(s) bound.", session.History.Last());
            Assert.True(document.Database.BlockTable.Has("grid$1$TAG"));
            Run(session, Enter);

            // The command is one step, whatever it bound.
            Assert.True(document.Undo());
            Assert.Equal(attached, document.Database.ToText());
        }

        [Fact]
        public void Xbind_AndItsAliases_ReachTheSameQuestions()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal("XBIND", CadShortcuts.Default.Aliases["XB"]);
            Assert.Equal("-XBIND", CadShortcuts.Default.Aliases["-XB"]);
            Assert.Equal("XBIND", registry.Find("XB").Name);
            Assert.Equal("-XBIND", registry.Find("-XB").Name);
            CadCommandSession session = Start(Blank(), "XBIND");
            Assert.Equal("Enter symbol type to bind [Block/Dimstyle/LAyer/LType/Style]:", session.PromptText);
        }

        #endregion

        #region SAVE

        [Fact]
        public void Save_AsksTheFileAndSaysThatAFileThereIsReplacedOnlyOnYes_TheDrawingKnownByTheNewName()
        {
            string path = Path.Combine(_folder, "plans", "first.dxf");
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Start(document, "SAVE");
            Assert.Equal("Save drawing as <" + path + ">:", session.PromptText);

            string other = Path.Combine(_folder, "plans", "second");
            Run(session, other);

            Assert.False(session.IsRunning);
            Assert.True(File.Exists(other + ".dxf"));
            Assert.Equal(other + ".dxf", document.FileName);

            // The file is there now: AutoCAD asks before it is written over, and No leaves it.
            Run(session, "SAVE", Enter);
            Assert.Contains("A drawing with this name already exists.", session.History);
            Assert.Equal("Do you want to replace it? <N>:", session.PromptText);
            File.WriteAllText(other + ".dxf", "kept");
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal("kept", File.ReadAllText(other + ".dxf"));

            Run(session, "SAVE", Enter, "Y");
            Assert.NotEqual("kept", File.ReadAllText(other + ".dxf"));
            Assert.False(document.IsModified);
        }

        [Fact]
        public void Save_OfANameEndingInDwg_WritesADwg_AndOfNoEnding_TheFormatOfTheDrawing()
        {
            string path = Path.Combine(_folder, "plans", "first.dxf");
            CadTemplate.CreateMetric().Save(path);
            CadDocument document = CadDocument.Open(path);

            Start(document, "SAVE", Path.Combine(_folder, "plans", "copy.dwg"));
            Assert.True(File.Exists(Path.Combine(_folder, "plans", "copy.dwg")));
            Assert.True(CadDrawingFile.IsDwgName(document.FileName));

            document = CadDocument.Open(path);
            Start(document, "SAVE", Path.Combine(_folder, "plans", "plain"));
            Assert.True(File.Exists(Path.Combine(_folder, "plans", "plain.dxf")));
        }

        #endregion
    }
}
