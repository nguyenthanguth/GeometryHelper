using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLEADER in a drawing with no multileader style makes Standard first, as AutoCAD 2021's drawing always has it (probe
    /// xref-bind: a DXF read with none is given Standard, text and arrows 0.18, landing 0.36, gap 0.09); and a style of no
    /// content draws a leader with nothing at its landing, asking no text, as AutoCAD 2021's MLEADER does.
    /// </summary>
    public class MLeaderStandardTests
    {
        [Fact]
        public void MLeaderInADrawingOfNoMultileaderStyle_MakesStandardFirst_AndDrawsInIt_AsOneUndoStep()
        {
            CadDocument document = Blank();
            string start = document.Database.ToText();
            Assert.Null(CadMLeaderStyleEditor.Find(document.Database, "Standard"));

            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal("B-12", mleader.MText.Text);
            Assert.Equal("Standard", mleader.MLeaderStyle);
            DxfMLeaderStyle standard = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            Assert.Equal(2.5, standard.TextHeight);
            Assert.Equal(2.5, standard.ArrowSize);
            Assert.Equal("Standard", document.Database.MLeaderstyle);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AnImperialDrawing_IsGivenAutoCadsImperialStandard()
        {
            var database = new DxfDatabase("AC1027") { Measurement = DxfMeasurementValue.English };
            CadDocument document = CadDocument.From(database, "imperial.dxf");

            Run(Session(document), "MLD", P(0, 0), P(3, 2), "B-12");

            DxfMLeaderStyle standard = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            Assert.Equal(0.18, standard.TextHeight);
            Assert.Equal(0.18, standard.ArrowSize);
            Assert.Equal(0.36, standard.DoglegLength);
            Assert.Equal(0.09, standard.LandingGap);
            Assert.Equal(0.125, standard.BreakSize);
        }

        [Fact]
        public void AStyleOfABlock_AsksItsAttributes_AndPlacesTheBlockPastTheLanding_AsAutoCadDoes()
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfDatabase database = document.Database;
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 5));
            tag.AppendEntity(
                new DxfAttributeDefinition
                {
                    Tag = "TAGNUMBER",
                    Prompt = "Enter tag number",
                    TextString = "TAGNUMBER",
                    Height = 2.5,
                }
            );
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(database, "Standard");
            style.ContentType = DxfContentType.BlockContent;
            style.Block = tag;
            style.DoglegLength = 0.36;
            CadCommandSession session = Session(document);

            Run(session, "MLD", P(0, 0), P(20, 10), "7");

            Assert.Contains("Enter attribute values", session.History);
            Assert.Contains("Enter tag number <TAGNUMBER>: 7", session.History);
            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal(DxfContentType.BlockContent, mleader.ContentType);
            Assert.Equal("TAG", mleader.BlockContent.Name);
            DxfAttributeDefinition definition = tag.Cast<DxfEntity>().OfType<DxfAttributeDefinition>().Single();
            Assert.Equal("7", mleader.GetBlockAttribute(definition).TextString);

            // AutoCAD 2021 put the circle of radius 5 at (25.36, 10, 0): past the landing by the dogleg and the radius.
            Assert.Equal(25.36, mleader.BlockPosition.X, 9);
            Assert.Equal(10, mleader.BlockPosition.Y, 9);
            AssertReopens(document);
        }

        [Fact]
        public void AStyleOfNoContent_DrawsALeaderWithNothingAtItsLanding_AskingNoText()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadMLeaderStyleEditor.Find(document.Database, "Standard").ContentType = DxfContentType.NoneContent;
            CadCommandSession session = Session(document);

            Run(session, "MLD", P(0, 0), P(30, 20));

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, l => l.StartsWith("Enter text", System.StringComparison.Ordinal));
            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Assert.Equal(DxfContentType.NoneContent, mleader.ContentType);
            Assert.Null(mleader.MText);
            Assert.Equal(1, mleader.LeaderCount);

            // As AutoCAD 2021 drew one: the line from the arrow, the dogleg from the landing away from it.
            Assert.Equal(new GeoPoint3(0, 0, 0), mleader.GetFirstVertex(0));
            Assert.Equal(new GeoVector3(1, 0, 0), mleader.GetDogleg(0));
            Assert.Matches(
                @"LEADER\{\n290\n\s*1\n291\n\s*1\n 10\n30(\.0)?\n 20\n20(\.0)?\n",
                document.Database.ToText()
            );
            AssertReopens(document);
        }
    }
}
