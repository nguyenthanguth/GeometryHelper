using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A setting kept in the drawing and changed in place (the snap and grid of a viewport, ORTHOMODE) is an undo step that
    /// keeps no copy of the drawing (<see cref="CadDocument.ApplySetting"/>): its undo and redo write the value back and read
    /// nothing again, an undo of another step reads the drawing and puts the settings changed since on it, and a step that
    /// joins the one before it is taken back with it, as AutoCAD 2021's U takes F7 pressed after a LINE with the line.
    /// </summary>
    public class SettingStepUndoTests
    {
        // The setting under test is the drawing's text height, a value of the header written in place.
        private static string Height(CadDocument document) =>
            document.Database.Textsize.ToString("R", CultureInfo.InvariantCulture);

        private static CadEditResult Set(
            CadDocument document,
            double value,
            string name = "SETVAR",
            bool joinsPrevious = false
        )
        {
            double before = document.Database.Textsize;
            return document.ApplySetting(
                name,
                "TEXTSIZE",
                () => Height(document),
                () => document.Database.Textsize = value,
                (doc, forward) => doc.Database.Textsize = forward ? value : before,
                joinsPrevious
            );
        }

        private static void Draw(CadDocument document, double y) =>
            document.ApplyEdit("LINE", null, s =>
                s.Append(s.Document.ModelSpace, new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(100, y, 0)))
            );

        private static int Lines(CadDocument document) => All<DxfLine>(document.ModelSpace).Count();

        [Fact]
        public void ASettingStep_UndoAndRedoWriteTheValue_AndReadTheDrawingNoMore()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            DxfDatabase database = document.Database;

            CadEditResult result = Set(document, 7.5);

            Assert.True(result.Succeeded);
            Assert.Single(result.Records);
            Assert.Equal("TEXTSIZE", result.Records[0].Property);
            Assert.Equal("7.5", result.Records[0].NewValue);
            Assert.Equal("SETVAR", document.NextUndo.Name);
            Assert.True(document.IsModified);

            Assert.True(document.Undo());
            Assert.Same(database, document.Database);
            Assert.Equal(start, document.Database.Textsize);
            Assert.False(document.IsModified);

            Assert.True(document.Redo());
            Assert.Same(database, document.Database);
            Assert.Equal(7.5, document.Database.Textsize);
        }

        [Fact]
        public void ASettingThatStaysAsItWas_MakesNoStep()
        {
            CadDocument document = Blank();

            CadEditResult result = Set(document, document.Database.Textsize);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Records);
            Assert.False(document.CanUndo);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void AnEditUndone_LeavesTheSettingsChangedBeforeIt_AndRedoBringsTheEditBack()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            Draw(document, 10);
            Set(document, 7.5);
            Draw(document, 20);

            // The drawing is read back for the line; the setting changed between the lines is put on it.
            Assert.True(document.Undo());
            Assert.Equal(7.5, document.Database.Textsize);
            Assert.Equal(1, Lines(document));

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.Textsize);
            Assert.Equal(1, Lines(document));

            Assert.True(document.Undo());
            Assert.Equal(0, Lines(document));

            Assert.True(document.Redo());
            Assert.True(document.Redo());
            Assert.Equal(7.5, document.Database.Textsize);
            Assert.True(document.Redo());
            Assert.Equal(7.5, document.Database.Textsize);
            Assert.Equal(2, Lines(document));
        }

        [Fact]
        public void AStepThatJoinsThePreviousOne_IsTakenBackAndPutAgainWithIt_UnderItsName()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            Draw(document, 10);

            Set(document, 7.5, "TEXTSIZE", true);
            Set(document, 9.0, "TEXTSIZE", true);
            Assert.Equal("LINE", document.NextUndo.Name);
            Draw(document, 20);

            // One U takes the second line; the next the line with both settings; REDO puts the line and both together.
            Assert.True(document.Undo());
            Assert.Equal(9.0, document.Database.Textsize);
            Assert.Equal(1, Lines(document));

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.Textsize);
            Assert.Equal(0, Lines(document));
            Assert.False(document.CanUndo);

            Assert.True(document.Redo());
            Assert.Equal(9.0, document.Database.Textsize);
            Assert.Equal(1, Lines(document));
            Assert.True(document.Redo());
            Assert.Equal(2, Lines(document));
            Assert.False(document.CanRedo);
        }

        [Fact]
        public void AStepThatJoinsThePreviousOne_WithNoStepBeforeIt_IsAStepOfItsOwn()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            DxfDatabase database = document.Database;

            Set(document, 7.5, "TEXTSIZE", true);
            Assert.Equal("TEXTSIZE", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Same(database, document.Database);
            Assert.Equal(start, document.Database.Textsize);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void SettingsChangedInsideAGroup_AreOneStepWithTheGroup()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            DxfDatabase database = document.Database;

            document.BeginGroup("GRID");
            Set(document, 7.5);
            Set(document, 9.0);
            document.EndGroup();

            Assert.Equal("GRID", document.NextUndo.Name);
            Assert.Equal(2, document.NextUndo.Records.Count);
            Assert.True(document.Undo());
            Assert.Same(database, document.Database);
            Assert.Equal(start, document.Database.Textsize);
            Assert.False(document.CanUndo);

            Assert.True(document.Redo());
            Assert.Equal(9.0, document.Database.Textsize);
        }

        [Fact]
        public void AGroupCancelled_LeavesTheSettingsOfTheStepsBeforeIt()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            Set(document, 7.5);

            document.BeginGroup("GRID");
            Set(document, 9.0);
            Draw(document, 10);
            document.CancelGroup();

            Assert.Equal(7.5, document.Database.Textsize);
            Assert.Equal(0, Lines(document));
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.Textsize);
        }

        [Fact]
        public void SettingSteps_BetweenViewSteps_ArePutOnAgainWhenAnEditAfterThemIsUndone()
        {
            CadDocument document = Blank();
            double start = document.Database.Textsize;
            CadCommandSession session = Session(document);
            var view = new CadView(800, 600);
            view.ZoomTo(new DisplayBox(0, 0, 100, 50));
            session.View = view;
            Run(session, "LINE", P(0, 0), P(100, 50), Enter);
            Set(document, 7.5);
            Run(session, "ZOOM", "2X");
            Set(document, 9.0);
            Run(session, "LINE", P(0, 10), P(100, 60), Enter);

            Run(session, "U");
            Assert.Equal(9.0, document.Database.Textsize);
            Assert.Equal(1, Lines(document));

            Run(session, "U");
            Assert.Equal(7.5, document.Database.Textsize);
            Run(session, "U");
            Run(session, "U");
            Assert.Equal(start, document.Database.Textsize);
            Assert.Equal(1, Lines(document));

            Run(session, "U");
            Assert.Equal(0, Lines(document));
        }
    }
}
