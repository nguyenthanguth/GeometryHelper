using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLEADERSTYLE (MLS) and the edits of the multileader styles, as AutoCAD 2021's Multileader Style Manager makes them: each
    /// one undo step; MLEADER drawing in the current style, and a style changed giving its values to its multileaders.
    /// </summary>
    public class MLeaderStyleCommandTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>The presenter's metric drawing, its CLASSES listing DICTIONARYVAR so that CMLEADERSTYLE can be set, as a drawing of AutoCAD's.</summary>
        private static CadDocument MetricWithVariables()
        {
            string text = CadTemplate.CreateMetric().ToText().Replace("\r\n", "\n");
            string classes = "  0\nSECTION\n  2\nCLASSES\n";
            Assert.Contains(classes, text);
            text = text.Replace(classes, classes + "  0\nCLASS\n  1\nDICTIONARYVAR\n  2\nAcDbDictionaryVar\n  3\nObjectDBX Classes\n 90\n0\n 91\n1\n280\n0\n281\n0\n");
            return CadDocument.From(DxfDatabase.Parse(text), "metric.dxf");
        }

        private static DxfMLeaderStyle Style(CadDocument document, string name) => (DxfMLeaderStyle)document.Database.MLeaderStyleDictionary[name];

        [Fact]
        public void AMultileaderStyleAdded_OnADrawingWithNoObjectsSection_MakesTheNamedObjectDictionary_AsAutoCadOpensOne()
        {
            // A new drawing of R2013 written without its OBJECTS section, as a program that writes only the tables, blocks and
            // entities saves one.
            string text = new DxfDatabase("AC1027").ToText().Replace("\r\n", "\n");
            int objects = text.IndexOf("  0\nSECTION\n  2\nOBJECTS\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nENDSEC\n", objects, StringComparison.Ordinal) + "  0\nENDSEC\n".Length;
            CadDocument document = CadDocument.From(DxfDatabase.Parse(text.Substring(0, objects) + text.Substring(end)), "noobjects.dxf");
            Assert.Null(document.Database.NamedObjectsDictionary);

            Assert.True(CadMLeaderStyleEditor.AddStandard(document).Succeeded);
            Assert.True(CadMLeaderStyleEditor.Add(document, "Detail notes", "Standard").Succeeded);

            Assert.NotNull(Style(document, "Standard"));
            Assert.NotNull(Style(document, "Detail notes"));
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Null(document.Database.MLeaderStyleDictionary);
        }

        [Fact]
        public void MLeader_DrawsInItsStyle_ItsTextHeightAndArrowsTheStyles()
        {
            // The style Standard of the drawing has words and arrows 4 high; TEXTSIZE is 2.5.
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal(4.0, mleader.TextHeight);
            Assert.Equal(4.0, mleader.ArrowSize);
            Assert.Equal(4.0, mleader.MText.TextHeight);
            AssertReopens(document);
        }

        [Fact]
        public void MLeader_DrawsInTheCurrentStyle_ANewStyleMetric()
        {
            CadDocument document = MetricWithVariables();
            Assert.True(CadMLeaderStyleEditor.AddStandard(document).Succeeded);
            Assert.True(CadMLeaderStyleEditor.Add(document, "Detail notes", "Standard").Succeeded);
            var notes = CadMLeaderStyleSettings.Of(Style(document, "Detail notes"));
            notes.TextHeight = 3.5;
            notes.ArrowSize = 3;
            notes.TextColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            Assert.True(CadMLeaderStyleEditor.Apply(document, "Detail notes", notes).Succeeded);
            Assert.True(CadMLeaderStyleEditor.SetCurrent(document, "Detail notes").Succeeded);
            Assert.Equal("Detail notes", CadMLeaderStyleEditor.Current(document.Database));

            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal("Detail notes", mleader.MLeaderStyle);
            Assert.Equal(3.5, mleader.TextHeight);
            Assert.Equal(3.0, mleader.ArrowSize);
            Assert.Equal(1, mleader.TextColor.ColorIndex);
            AssertReopens(document);
        }

        [Fact]
        public void ANewStyle_IsMetric_AndEachEditIsOneUndoStep()
        {
            CadDocument document = Metric();
            string start = document.Database.ToText();

            Assert.True(CadMLeaderStyleEditor.AddStandard(document).Succeeded);
            DxfMLeaderStyle standard = Style(document, "Standard");
            Assert.Equal(2.5, standard.ArrowSize);
            Assert.Equal(3.75, standard.BreakSize);
            Assert.Equal(8.0, standard.DoglegLength);
            Assert.Equal(2.5, standard.TextHeight);
            Assert.Equal(2.0, standard.LandingGap);
            Assert.Equal("Standard", standard.TextStyle.Name);
            Assert.Equal("ByBlock", standard.LeaderLineLinetype.Name);

            Assert.True(CadMLeaderStyleEditor.Add(document, "Balloon", "Standard").Succeeded);
            Assert.Equal(new[] { "Balloon", "Standard" }, CadMLeaderStyleEditor.Styles(document.Database));
            Assert.False(CadMLeaderStyleEditor.Add(document, "balloon", null).Succeeded);

            document.Undo();
            Assert.Equal(new[] { "Standard" }, CadMLeaderStyleEditor.Styles(document.Database));
            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AStyleChanged_GivesItsMultileadersItsValues_ButThoseTheyHaveOfTheirOwn()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Session(document);
            Run(session, "MLD", P(0, 0), P(30, 20), "ONE");
            Run(session, "MLD", P(100, 0), P(130, 20), "TWO");
            DxfMLeader[] both = All<DxfMLeader>(document.ModelSpace).ToArray();
            string own = both[1].Handle;
            Assert.True(document.ApplyEdit("Properties", new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, own, "Text height", "4", "6") }, s => ((DxfMLeader)s.Document.FindEntity(own)).TextHeight = 6).Succeeded);

            var settings = CadMLeaderStyleSettings.Of(Style(document, "Standard"));
            settings.TextHeight = 2.5;
            settings.ArrowSize = 2.5;
            Assert.True(CadMLeaderStyleEditor.Apply(document, "Standard", settings).Succeeded);

            DxfMLeader first = Entity<DxfMLeader>(document, both[0].Handle);
            DxfMLeader second = Entity<DxfMLeader>(document, own);
            Assert.Equal(2.5, first.TextHeight);
            Assert.Equal(2.5, first.ArrowSize);
            Assert.Equal(6.0, second.TextHeight);
            Assert.Equal(2.5, second.ArrowSize);
            AssertReopens(document);

            document.Undo();
            Assert.Equal(4.0, Entity<DxfMLeader>(document, both[0].Handle).TextHeight);
            Assert.Equal(4.0, Style(document, "Standard").TextHeight);
        }

        [Fact]
        public void Delete_IsRefusedForStandard_TheCurrentStyle_AndAStyleInUse()
        {
            CadDocument document = BlankWithMLeaderStyle();
            Assert.True(CadMLeaderStyleEditor.Add(document, "Unused", null).Succeeded);
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "ONE");

            Assert.Equal("The Standard multileader style cannot be deleted.", CadMLeaderStyleEditor.WhyNotDeleted(document.Database, "Standard"));
            Assert.False(CadMLeaderStyleEditor.Delete(document, "Standard").Succeeded);
            Assert.Null(CadMLeaderStyleEditor.WhyNotDeleted(document.Database, "Unused"));
            Assert.Single(CadMLeaderStyleEditor.Uses(document.Database, "Standard"));
            Assert.True(CadMLeaderStyleEditor.Delete(document, "Unused").Succeeded);
            Assert.Equal(new[] { "Standard" }, CadMLeaderStyleEditor.Styles(document.Database));
        }

        [Fact]
        public void MLeaderStyle_AsksTheWindowForTheManager_AndSaysTheCurrentStyleWithNone()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Session(document);
            Run(session, "MLS");
            Assert.Equal("Current multileader style: \"Standard\". The Multileader Style Manager is shown by the window.", session.History.Last());

            string asked = null;
            session.UiRequested += (sender, e) =>
            {
                asked = e.Name;
                e.Handled = true;
            };
            Run(session, "MLEADERSTYLE");
            Assert.Equal("MLEADERSTYLE", asked);
        }
    }
}
