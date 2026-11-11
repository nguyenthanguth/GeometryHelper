using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>COPYCLIP, CUTCLIP, COPYBASE, PASTECLIP, PASTEBLOCK and PASTEORIG through a clipboard in memory, as AutoCAD's.</summary>
    public class ClipboardTests
    {
        private static CadCommandSession Session(CadDocument document, ICadClipboard clipboard)
        {
            CadCommandSession session = EditingDrawings.Session(document);
            session.Clipboard = clipboard;
            return session;
        }

        [Fact]
        public void ALineCopiedAndPasted_IsPutByTheLowerLeftCornerOfWhatWasCopied_InOneUndoStep()
        {
            CadDocument document = Plan();
            var clipboard = new CadMemoryClipboard();
            CadCommandSession session = Session(document, clipboard);

            Run(session, "COPYCLIP", new[] { "A1" }, Enter);
            CadClipboardData data = clipboard.GetData();
            Assert.NotNull(data);
            Assert.Equal(1, data.Count);
            int before = All<DxfLine>(document.ModelSpace).Count;

            Run(session, "PASTECLIP", P(1000, 500));

            DxfLine pasted = All<DxfLine>(document.ModelSpace).Last();
            Assert.Equal(before + 1, All<DxfLine>(document.ModelSpace).Count);
            Near(new GeoPoint3(1000, 500, 0), pasted.StartPoint);
            Near(new GeoPoint3(1100, 500, 0), pasted.EndPoint);
            Assert.Equal("Steel", pasted.Layer);
            Assert.Equal("PASTECLIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(before, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void ObjectsPastedIntoAnotherDrawing_BringTheirLayer_ButTakeTheTargetsOwnOfTheSameName()
        {
            CadDocument source = Plan();
            var clipboard = new CadMemoryClipboard();
            Run(Session(source, clipboard), "COPYCLIP", new[] { "A1", "A7" }, Enter);

            // A drawing with no layer Steel takes it as the copy had it, red, and the block BOX with it.
            CadDocument blank = Blank();
            Run(Session(blank, clipboard), "PASTECLIP", P(0, 0));
            Assert.Equal(1, blank.Database.LayerTable["Steel"].Color.ColorIndex);
            Assert.True(blank.Database.BlockTable.Has("BOX"));
            Assert.Single(All<DxfBlockReference>(blank.ModelSpace));

            // A drawing whose Steel is blue keeps it blue.
            CadDocument other = Blank();
            Assert.True(CadLayerEditor.Add(other, "Steel").Succeeded);
            Assert.True(CadLayerEditor.SetColor(other, "Steel", 5).Succeeded);
            Run(Session(other, clipboard), "PASTECLIP", P(0, 0));
            Assert.Equal(5, other.Database.LayerTable["Steel"].Color.ColorIndex);
            Assert.Equal("Steel", Assert.Single(All<DxfLine>(other.ModelSpace)).Layer);
            AssertReopens(other);
        }

        [Fact]
        public void CutClip_ErasesWhatItCopies_InOneUndoStep_AndPasteBringsItBack()
        {
            CadDocument document = Plan();
            var clipboard = new CadMemoryClipboard();
            CadCommandSession session = Session(document, clipboard);

            Run(session, "CUTCLIP", new[] { "A3" }, Enter);

            Assert.Null(document.FindEntity("A3"));
            Assert.Equal("CUTCLIP", document.NextUndo.Name);
            Run(session, "PASTECLIP", P(190, -10));
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(200, 0, 0), circle.Center);
        }

        [Fact]
        public void CopyBase_AsksTheBasePointFirst_AndPasteClipPutsThatPointAtTheInsertionPoint()
        {
            CadDocument document = Plan();
            var clipboard = new CadMemoryClipboard();
            CadCommandSession session = Session(document, clipboard);

            Run(session, "COPYBASE");
            Assert.Equal("Specify base point:", session.PromptText);
            Run(session, P(200, 0), new[] { "A3" }, Enter, "PASTECLIP", P(0, 1000));

            DxfCircle pasted = All<DxfCircle>(document.ModelSpace).Last();
            Near(new GeoPoint3(0, 1000, 0), pasted.Center);
        }

        [Fact]
        public void PasteBlock_MakesOneBlockNamedAsAutoCADNamesIt_AndAnInsertOfItAtThePoint()
        {
            CadDocument document = Plan();
            var clipboard = new CadMemoryClipboard();
            CadCommandSession session = Session(document, clipboard);

            Run(session, "COPYCLIP", new[] { "A1", "A2" }, Enter, "PASTEBLOCK", P(400, 400));

            DxfBlockReference insert = All<DxfBlockReference>(document.ModelSpace).Last();
            Assert.Matches("^A\\$C[0-9A-F]{8}$", insert.Name);
            Near(new GeoPoint3(400, 400, 0), insert.Position);
            Assert.Equal(2, insert.BlockTableRecord.Cast<DxfEntity>().Count());
            Assert.Equal("PASTEBLOCK", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void PasteOrig_PastesWhereTheObjectsWere_InAnotherDrawingOnly()
        {
            CadDocument source = Plan();
            var clipboard = new CadMemoryClipboard();
            CadCommandSession session = Session(source, clipboard);
            Run(session, "COPYCLIP", new[] { "A3" }, Enter, "PASTEORIG");
            Assert.Single(All<DxfCircle>(source.ModelSpace));
            Assert.Contains(session.History, l =>
                l.StartsWith("PASTEORIG pastes objects copied from another drawing", System.StringComparison.Ordinal)
            );

            CadDocument blank = Blank();
            Run(Session(blank, clipboard), "PASTEORIG");
            Near(new GeoPoint3(200, 0, 0), Assert.Single(All<DxfCircle>(blank.ModelSpace)).Center);
        }

        [Fact]
        public void TextOfAnotherApp_IsPastedAsAnMText_ItsLinesItsParagraphs()
        {
            CadDocument document = Blank();
            var clipboard = new CadMemoryClipboard();
            clipboard.SetData(new CadClipboardData(null, "Steel S355\r\nBolts {M20} 8.8\\\r\n"));

            Run(Session(document, clipboard), "PASTECLIP", P(10, 20));

            DxfMText mtext = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal("Steel S355\\PBolts \\{M20\\} 8.8\\\\", mtext.Contents);
            Near(new GeoPoint3(10, 20, 0), mtext.Location);
        }

        [Fact]
        public void TextsCopied_GoOnTheClipboardAsPlainTextToo_AndTheObjectsReadBackFromThePrivateFormat()
        {
            CadDocument document = Plan();
            var clipboard = new CadMemoryClipboard();
            Run(Session(document, clipboard), "COPYCLIP", new[] { "A6", "A1" }, Enter);

            CadClipboardData data = clipboard.GetData();
            Assert.Equal("NOTE", data.Text);
            CadClipboardData again = CadClipboardData.FromFormat(data.ToFormat(), data.Text);
            Assert.Equal(data.Drawing, again.Drawing);
            Assert.Equal(data.Source, again.Source);
            Assert.Equal("plan.dxf", again.SourceName);
            Assert.Equal(2, again.Count);
            Assert.Equal(2, DxfDatabase.Parse(again.Drawing).ModelSpace.Cast<DxfEntity>().Count());
        }

        [Fact]
        public void TheKeysOfTheClipboard_AreAutoCADs()
        {
            Assert.Equal("COPYCLIP", CadShortcuts.Default.Keys["Ctrl+C"]);
            Assert.Equal("CUTCLIP", CadShortcuts.Default.Keys["Ctrl+X"]);
            Assert.Equal("PASTECLIP", CadShortcuts.Default.Keys["Ctrl+V"]);
            Assert.Equal("COPYBASE", CadShortcuts.Default.Keys["Ctrl+Shift+C"]);
            Assert.Equal("PASTEBLOCK", CadShortcuts.Default.Keys["Ctrl+Shift+V"]);
        }

        [Fact]
        public void PasteAskingItsPoint_PreviewsTheObjectsAtTheCursor_BeforeTheDrawingHasTheirBlockOrLayer()
        {
            var clipboard = new CadMemoryClipboard();
            Run(Session(Plan(), clipboard), "COPYCLIP", new[] { "A1", "A7" }, Enter);

            CadDocument blank = Blank();
            CadCommandSession session = Run(Session(blank, clipboard), "PASTECLIP");
            Assert.StartsWith("Specify insertion point", session.PromptText);
            int history = session.History.Count;
            Assert.True(session.Prompt.Preview.Items(P(300, 200)).Count > 1);

            // Nothing came into the drawing or the history for the preview: the block and the layer come with the paste.
            Assert.Equal(history, session.History.Count);
            Assert.False(blank.IsModified);
            Assert.False(blank.Database.BlockTable.Has("BOX"));
            PreviewTests.PreviewMakes(session, P(300, 200));
            Assert.True(blank.Database.BlockTable.Has("BOX"));

            Run(session, "PASTEBLOCK");
            PreviewTests.PreviewMakes(session, P(-40, 70));
        }

        [Fact]
        public void TextOfAnotherAppAskingItsPoint_PreviewsTheMTextAtTheCursor()
        {
            var clipboard = new CadMemoryClipboard();
            clipboard.SetData(new CadClipboardData(null, "Steel S355\r\nBolts"));
            CadCommandSession session = Run(Session(Blank(), clipboard), "PASTECLIP");

            PreviewTests.PreviewMakes(session, P(10, 20));
        }

        [Fact]
        public void PasteWithNothingOnTheClipboard_SaysSo_AndChangesNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document, new CadMemoryClipboard()), "PASTECLIP");

            Assert.Contains("The Clipboard holds nothing to paste.", session.History);
            Assert.False(document.IsModified);
        }
    }
}
