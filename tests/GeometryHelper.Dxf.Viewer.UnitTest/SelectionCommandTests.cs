using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The commands of C17, each as AutoCAD 2021 runs it: DRAWORDER and TEXTTOFRONT writing the sort table the display follows,
    /// GROUP, -GROUP and UNGROUP with PICKSTYLE taking a group whole, -PURGE keeping what AutoCAD keeps, Quick Select over the
    /// rows of the Properties panel, FIND and its replacements, and SELECTIONCYCLING.
    /// </summary>
    public class SelectionCommandTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private readonly ITestOutputHelper _output;

        public SelectionCommandTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string Put(DxfBlockTableRecord block, DxfEntity entity)
        {
            block.AppendEntity(entity);
            return entity.Handle;
        }

        /// <summary>A line A, a circle B and a line C, drawn in that order, over one another at the origin.</summary>
        private static CadDocument Three(out string a, out string b, out string cc)
        {
            var db = new DxfDatabase("AC1027");
            a = Put(db.ModelSpace, new DxfLine(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0)));
            b = Put(db.ModelSpace, new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10));
            cc = Put(db.ModelSpace, new DxfLine(new GeoPoint3(0, -20, 0), new GeoPoint3(0, 20, 0)));
            return CadDocument.From(db, "three.dxf");
        }

        private static List<string> DrawnOrder(CadDocument document)
        {
            var order = new List<string>();

            foreach (DisplayItem2 item in document.GetDisplay(document.ModelSpace).Items)
            {
                if (item.Handle != null && (order.Count == 0 || order[order.Count - 1] != item.Handle))
                {
                    order.Add(item.Handle);
                }
            }

            return order;
        }

        [Fact]
        public void DraworderBack_SendsTheObjectToTheBackOfTheDisplay_AsOneUndoStep_InTheSortTableAutoCadWrites()
        {
            CadDocument document = Three(out string a, out string b, out string c);
            Assert.Equal(new[] { a, b, c }, DrawnOrder(document));

            CadCommandSession session = Run(Session(document), "DR", new[] { c }, Enter);
            Assert.Equal("Enter object ordering option [Above objects/Under objects/Front/Back] <Back>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { c, a, b }, DrawnOrder(document));
            string text = document.Database.ToText();
            Assert.Contains("SORTENTSTABLE", text);
            Assert.Contains("ACAD_SORTENTS", text);
            Assert.Equal(new[] { c, a, b }, document.Database.ModelSpace.DrawOrderTable.GetFullDrawOrder().Select(e => e.Handle));
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(new[] { a, b, c }, DrawnOrder(document));
        }

        [Fact]
        public void DraworderAboveAndUnder_PutTheObjectsAgainstTheReferenceObjects_AndFrontBringsThemBack()
        {
            CadDocument document = Three(out string a, out string b, out string c);

            Run(Session(document), "DRAWORDER", new[] { a }, Enter, "A", new[] { b }, Enter);
            Assert.Equal(new[] { b, a, c }, DrawnOrder(document));

            Run(Session(document), "DRAWORDER", new[] { c }, Enter, "U", new[] { b }, Enter);
            Assert.Equal(new[] { c, b, a }, DrawnOrder(document));

            Run(Session(document), "DRAWORDER", new[] { c }, Enter, "F");
            Assert.Equal(new[] { b, a, c }, DrawnOrder(document));

            // The order reads back from the file as it was written.
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            Assert.Equal(new[] { b, a, c }, again.ModelSpace.DrawOrderTable.GetFullDrawOrder().Select(e => e.Handle));
        }

        [Fact]
        public void Texttofront_BringsEveryTextToTheFront_AndSaysHowMany()
        {
            var db = new DxfDatabase("AC1027");
            string text = Put(db.ModelSpace, new DxfMText { Contents = "NOTE", Location = new GeoPoint3(0, 0, 0), TextHeight = 2.5 });
            string line = Put(db.ModelSpace, new DxfLine(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0)));
            CadDocument document = CadDocument.From(db, "text.dxf");

            CadCommandSession session = Run(Session(document), "TEXTTOFRONT");
            Assert.Equal("Bring to front [Text/Dimensions/Leaders/All] <All>:", session.PromptText);
            Run(session, "T");

            Assert.Equal("1 object(s) brought to front.", session.History.Last());
            Assert.Equal(new[] { line, text }, document.Database.ModelSpace.DrawOrderTable.GetFullDrawOrder().Select(e => e.Handle));
        }

        [Fact]
        public void Group_MakesAGroupOfTheObjects_AndAPickTakesItWhole_UntilPickstyleIsOff()
        {
            CadDocument document = Three(out string a, out string b, out string c);

            CadCommandSession session = Run(Session(document), "G");
            Assert.Equal("Select objects or [Name/Description]:", session.PromptText);
            Run(session, "N", "BOLTS", "D", "Bolts of grid A", new[] { a, b }, Enter);

            Assert.Equal("Group \"BOLTS\" has been created.", session.History.Last());
            var group = (DxfGroup)document.Database.GroupDictionary.GetAt("BOLTS");
            Assert.Equal("Bolts of grid A", group.Description);
            Assert.Equal(new[] { a, b }, group.GetAllEntityIds().Select(e => e.Handle));
            AssertReopens(document);

            // A pick of the line takes the circle with it; with PICKSTYLE 0 the line alone.
            Run(session, "ERASE", new[] { a });
            Assert.Equal("2 found", session.History.Last());
            Run(session, Esc);

            session.TogglePickStyle();
            Assert.Equal("<Group off>", session.History.Last());
            Run(session, "ERASE", new[] { a });
            Assert.Equal("1 found", session.History.Last());
            Run(session, Esc);

            Assert.True(document.Undo());
            Assert.False(document.Database.GroupDictionary.Contains("BOLTS"));
        }

        [Fact]
        public void GroupWithNoName_IsUnnamed_AndUngroupExplodesTheGroupOfTheObjectPicked()
        {
            CadDocument document = Three(out string a, out string b, out string c);
            CadCommandSession session = Run(Session(document), "GROUP", new[] { b, c }, Enter);

            Assert.Equal("Group \"*A1\" has been created.", session.History.Last());
            Assert.True(((DxfGroup)document.Database.GroupDictionary.GetAt("*A1")).IsAnonymous);

            Run(session, "UNGROUP", new[] { c });
            Assert.Equal("Group \"*A1\" has been exploded.", session.History.Last());
            Assert.False(document.Database.GroupDictionary.Contains("*A1"));
        }

        [Fact]
        public void GroupTyped_AddsRemovesRenamesAndMakesUnselectable_AsAutoCadsCommandLineForm()
        {
            CadDocument document = Three(out string a, out string b, out string c);
            CadCommandSession session = Run(Session(document), "-GROUP");
            Assert.Equal("Enter a group option [?/Order/Add/Remove/Explode/REName/Selectable/Create] <Create>:", session.PromptText);
            Run(session, Enter, "PLATE", "Plate P1", new[] { a }, Enter);
            Assert.Equal("Group \"PLATE\" has been created.", session.History.Last());

            Run(session, "-GROUP", "A", "PLATE", new[] { b, c }, Enter);
            Run(session, "-GROUP", "R", "PLATE", new[] { b }, Enter);
            var group = (DxfGroup)document.Database.GroupDictionary.GetAt("PLATE");
            Assert.Equal(new[] { a, c }, group.GetAllEntityIds().Select(e => e.Handle));

            Run(session, "-GROUP", "REN", "PLATE", "P1");
            Assert.True(document.Database.GroupDictionary.Contains("P1"));
            Assert.False(document.Database.GroupDictionary.Contains("PLATE"));

            Run(session, "-GROUP", "S", "P1");
            Assert.Equal("This group is selectable. Do you want to make it unselectable? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.False(((DxfGroup)document.Database.GroupDictionary.GetAt("P1")).Selectable);

            // An unselectable group is not taken whole.
            Run(session, "ERASE", new[] { a });
            Assert.Equal("1 found", session.History.Last());
            Run(session, Esc);

            Run(session, "-GROUP", "?", Enter);
            Assert.Equal("P1  Plate P1", session.History.Last());
            AssertReopens(document);
        }

        [Fact]
        public void PurgeLayers_AsksEachNameAndDeletesTheUnusedOnes_KeepingZeroDefpointsAndTheCurrentLayer()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Defpoints" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "EMPTY1" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "EMPTY2" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "CURRENT" });
            db.Clayer = "CURRENT";
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "WALLS" });
            CadDocument document = CadDocument.From(db, "purge.dxf");

            CadCommandSession session = Run(Session(document), "-PURGE");
            Assert.StartsWith("Enter type of unused objects to purge [Blocks/Dimstyles/LAyers/LTypes/MUltileaderstyles/Plotstyles/SHapes/textSTyles/Tablestyles/Visualstyles/Regapps/Zero-length geometry/Empty text objects/All]:", session.PromptText);
            Run(session, "LA", Enter);
            Assert.Equal("Verify each name to be purged? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Purge layer \"EMPTY1\"? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Purge layer \"EMPTY2\"? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("1 layer deleted.", session.History.Last());
            string[] layers = document.Database.LayerTable.Select(l => l.Name).ToArray();
            Assert.DoesNotContain("EMPTY1", layers);
            Assert.Contains("EMPTY2", layers);
            Assert.Contains("0", layers);
            Assert.Contains("Defpoints", layers);
            Assert.Contains("CURRENT", layers);
            Assert.Contains("WALLS", layers);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Contains("EMPTY1", document.Database.LayerTable.Select(l => l.Name));
        }

        [Fact]
        public void PurgeZeroLengthAndEmptyText_EraseThemAsOneUndoStep()
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0.0004, 0, 0)));
            string kept = Put(db.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            db.ModelSpace.AppendEntity(new DxfMText { Contents = " ", Location = new GeoPoint3(0, 0, 0), TextHeight = 2.5 });
            CadDocument document = CadDocument.From(db, "zero.dxf");

            CadCommandSession session = Run(Session(document), "-PU", "Z");
            Assert.Equal("1 zero-length geometry object deleted.", session.History.Last());
            Run(session, "-PURGE", "E");
            Assert.Equal("1 empty text object deleted.", session.History.Last());
            Assert.Equal(new[] { kept }, document.Database.ModelSpace.Cast<DxfEntity>().Select(e => e.Handle));
        }

        /// <summary>A plan of lines on layers WALLS and DOORS, 100, 200 and 300 long, and a circle.</summary>
        private static CadDocument Walls()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DOORS" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALLS" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(200, 10, 0)) { Layer = "WALLS" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(300, 20, 0)) { Layer = "DOORS" });
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10) { Layer = "WALLS" });
            return CadDocument.From(db, "walls.dxf");
        }

        [Fact]
        public void QuickSelect_OffersTheTypesPropertiesAndValuesInUse_AndSelectsByEachOperator()
        {
            CadDocument document = Walls();
            var quick = new CadQuickSelect(document);
            Tolerance tolerance = Tolerance.Default;

            Assert.Equal(new[] { CadQuickSelect.Multiple, "Circle", "Line" }, quick.ObjectTypes);
            Assert.Equal(new[] { "Color", "Layer", "Linetype", "Linetype scale", "Lineweight", "Start X", "Start Y", "Start Z", "End X", "End Y", "End Z", "Length", "Angle" }, quick.PropertiesOf("Line"));
            Assert.Equal(new[] { "DOORS", "WALLS" }, quick.ValuesOf("Line", "Layer"));
            Assert.Empty(quick.ValuesOf("Line", "Length"));

            Assert.Equal(2, quick.Match("Line", "Layer", CadQuickSelectOperator.EqualTo, "walls", true, tolerance).Count);
            Assert.Equal(3, quick.Match(CadQuickSelect.Multiple, "Layer", CadQuickSelectOperator.EqualTo, "WALLS", true, tolerance).Count);
            Assert.Single(quick.Match("Line", "Layer", CadQuickSelectOperator.NotEqualTo, "WALLS", true, tolerance));
            Assert.Equal(2, quick.Match("Line", "Length", CadQuickSelectOperator.GreaterThan, "150", true, tolerance).Count);
            Assert.Single(quick.Match("Line", "Length", CadQuickSelectOperator.LessThan, "150", true, tolerance));
            Assert.Single(quick.Match("Line", "Length", CadQuickSelectOperator.EqualTo, "200.0004", true, tolerance));
            Assert.Equal(3, quick.Match("Line", "Layer", CadQuickSelectOperator.Wildcard, "*S", true, tolerance).Count);
            Assert.Single(quick.Match("Line", "Layer", CadQuickSelectOperator.Wildcard, "W*", false, tolerance));
            Assert.Equal(3, quick.Match("Line", null, CadQuickSelectOperator.SelectAll, null, true, tolerance).Count);

            IReadOnlyList<string> walls = quick.Match("Line", "Layer", CadQuickSelectOperator.EqualTo, "WALLS", true, tolerance);
            Assert.Equal("2 item(s) selected.", quick.Apply(walls, false));
            Assert.Equal(2, document.Selection.Count);
            IReadOnlyList<string> circles = new CadQuickSelect(document).Match("Circle", null, CadQuickSelectOperator.SelectAll, null, true, tolerance);
            new CadQuickSelect(document).Apply(circles, true);
            Assert.Equal(3, document.Selection.Count);

            // Apply to: Current selection looks among the objects selected only.
            Assert.Equal(new[] { CadQuickSelect.Multiple, "Circle", "Line" }, new CadQuickSelect(document, null, document.Selection.Handles).ObjectTypes);
            Assert.Equal(3, new CadQuickSelect(document, null, document.Selection.Handles).Count);
        }

        [Fact]
        public void Qselect_WithNoWindow_SaysTheWindowShowsIt()
        {
            CadCommandSession session = Run(Session(Walls()), "QSELECT");
            Assert.Equal("Quick Select is shown by the window.", session.History.Last());
        }

        /// <summary>A text, an MTEXT with formatting, a dimension with its text overridden, a multileader and a block with an attribute and a text of its own, each saying M20.</summary>
        private static CadDocument Bolts(out string text, out string mtext, out string insert)
        {
            var db = new DxfDatabase("AC1027");
            text = Put(db.ModelSpace, new DxfDBText { TextString = "M20 x 60", Height = 2.5 });
            mtext = Put(db.ModelSpace, new DxfMText { Contents = "{\\fArial|b1;Bolts M20}\\PAll M200 holes", Location = new GeoPoint3(0, 10, 0), TextHeight = 2.5 });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = "m20 lower", Height = 2.5, Position = new GeoPoint3(0, 20, 0) });
            var block = new DxfBlockTableRecord { Name = "BOLT" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "M16", "SIZE", "Size", null) { Height = 2.5 });
            block.AppendEntity(new DxfDBText { TextString = "Bolt M20", Height = 2.5 });
            var reference = new DxfBlockReference(new GeoPoint3(50, 0, 0), block);
            insert = Put(db.ModelSpace, reference);
            reference.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(50, 0, 0), "M20", "SIZE", null) { Height = 2.5 });
            return CadDocument.From(db, "bolts.dxf");
        }

        [Fact]
        public void Find_FindsTheTextInTextMtextAttributesAndBlocks_ByCaseAndWholeWord()
        {
            CadDocument document = Bolts(out string text, out string mtext, out string insert);
            var options = new CadFindOptions { MatchCase = true, WholeWords = true };

            IReadOnlyList<CadFindResult> found = CadFind.Find(document, "M20", options, CadFindScope.EntireDrawing);

            Assert.Equal(new[] { "Text", "MText", "Attribute", "Text" }, found.Select(r => r.ObjectType));
            Assert.Equal(new[] { text, mtext, insert, insert }, found.Select(r => r.Handle));
            Assert.Equal("Bolts M20\nAll M200 holes", found[1].Text);
            Assert.Equal(new[] { "Model", "Model", "Model", "BOLT" }, found.Select(r => r.Location));

            // Without whole words M200 counts; without case, m20 too; with no blocks searched, the block's text not.
            Assert.Equal(2, CadFind.Find(document, "M20", new CadFindOptions { MatchCase = true }, CadFindScope.EntireDrawing)[1].Count);
            Assert.Equal(5, CadFind.Find(document, "M20", new CadFindOptions { WholeWords = true }, CadFindScope.EntireDrawing).Count);
            Assert.Equal(3, CadFind.Find(document, "M20", new CadFindOptions { MatchCase = true, WholeWords = true, SearchBlocks = false }, CadFindScope.EntireDrawing).Count);
            Assert.Equal(2, CadFind.Find(document, "M2#", new CadFindOptions { MatchCase = true, WholeWords = true, UseWildcards = true, BlockAttributes = false, SearchBlocks = false }, CadFindScope.EntireDrawing).Count);
            Assert.Single(CadFind.Find(document, "M20", options, CadFindScope.SelectedObjects, new[] { text }));
        }

        [Fact]
        public void Replace_KeepsTheFormattingOfMtext_AndReplaceAllIsOneUndoStep()
        {
            CadDocument document = Bolts(out string text, out string mtext, out string insert);
            var options = new CadFindOptions { MatchCase = true, WholeWords = true };
            IReadOnlyList<CadFindResult> found = CadFind.Find(document, "M20", options, CadFindScope.EntireDrawing);

            Assert.True(CadFind.Replace(document, found[1], "M20", "M24", options).Succeeded);
            Assert.Equal("{\\fArial|b1;Bolts M24}\\PAll M200 holes", Entity<DxfMText>(document, mtext).Contents);

            found = CadFind.Find(document, "M20", options, CadFindScope.EntireDrawing);
            Assert.True(CadFind.ReplaceAll(document, found, "M20", "M24", options).Succeeded);
            Assert.Equal("FIND", document.NextUndo.Name);
            Assert.Equal("M24 x 60", Entity<DxfDBText>(document, text).TextString);
            Assert.Equal("M24", Entity<DxfBlockReference>(document, insert).AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            Assert.Equal("Bolt M24", document.Database.BlockTable["BOLT"].OfType<DxfDBText>().Single(t => !(t is DxfAttributeDefinition)).TextString);
            Assert.Empty(CadFind.Find(document, "M20", options, CadFindScope.EntireDrawing));
            AssertReopens(document);

            // One undo takes every replacement of Replace All back, and leaves that of Replace.
            Assert.True(document.Undo());
            Assert.Equal("M20 x 60", Entity<DxfDBText>(document, text).TextString);
            Assert.Equal("M20", Entity<DxfBlockReference>(document, insert).AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            Assert.Equal("{\\fArial|b1;Bolts M24}\\PAll M200 holes", Entity<DxfMText>(document, mtext).Contents);
        }

        [Fact]
        public void SelectionCycling_IsTwoUntilCtrlWTurnsItOff()
        {
            CadDocument document = Walls();
            CadCommandSession session = Session(document);
            Assert.Equal(2, document.SelectionCycling);
            session.ToggleSelectionCycling();
            Assert.Equal("<Selection Cycling off>", session.History.Last());
            Assert.Equal(0, document.SelectionCycling);
            Run(session, "SELECTIONCYCLING", "1");
            Assert.Equal(1, document.SelectionCycling);
        }

        [Fact]
        public void FindAndQuickSelect_OverAHundredThousandObjects_AreTimed()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });

            for (int i = 0; i < 50000; i++)
            {
                db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, i, 0), new GeoPoint3(100 + (i % 7), i, 0)) { Layer = i % 3 == 0 ? "WALLS" : "0" });
                db.ModelSpace.AppendEntity(new DxfDBText { TextString = "P" + i, Height = 2.5, Position = new GeoPoint3(200, i, 0) });
            }

            CadDocument document = CadDocument.From(db, "big.dxf");
            var watch = Stopwatch.StartNew();
            var quick = new CadQuickSelect(document);
            int walls = quick.Match("Line", "Layer", CadQuickSelectOperator.EqualTo, "WALLS", true, Tolerance.Default).Count;
            long layer = watch.ElapsedMilliseconds;
            watch.Restart();
            int longer = quick.Match("Line", "Length", CadQuickSelectOperator.GreaterThan, "103", true, Tolerance.Default).Count;
            long length = watch.ElapsedMilliseconds;
            watch.Restart();
            int texts = CadFind.Find(document, "P4999#", new CadFindOptions { UseWildcards = true }, CadFindScope.EntireDrawing).Count;
            long find = watch.ElapsedMilliseconds;
            _output.WriteLine("QSELECT Layer = : " + layer + " ms, Length > : " + length + " ms, FIND: " + find + " ms");

            Assert.Equal(16667, walls);
            Assert.Equal(21428, longer);
            Assert.Equal(10, texts);
        }
    }
}
