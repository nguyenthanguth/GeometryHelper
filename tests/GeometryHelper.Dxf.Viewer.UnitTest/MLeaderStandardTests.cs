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

            // And wrote its matrix there: the translation of AutoCAD's 47 groups is the place (25.36, 10).
            Assert.Matches(@"\n\s*47\n25\.36\d*\n", document.Database.ToText());
            AssertReopens(document);
        }

        [Fact]
        public void TheSecondPoint_IsWhereTheLeaderLands_TheWordsStandPastTheDoglegAndTheGap_AsAutoCadLandsThem()
        {
            // AutoCAD 2021 (probe GHAQ, an arrow at (100, 100) and a landing at (150, 150) in a style of a dogleg 0.36, a gap
            // 0.09 and words 0.18 high): the dogleg starts at the landing, the base of the content is at its end, (150.36, 150),
            // and the words are held at (150.45, 150.09), their first line's middle level with the landing.
            CadDocument document = BlankWithMLeaderStyle();
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");

            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            int leader = mleader.GetLeaderIndexes().Single();
            int line = mleader.GetLeaderLineIndexes(leader).Single();
            Assert.Equal(new GeoPoint3(0, 0, 0), mleader.GetFirstVertex(line));
            Assert.Equal(new GeoPoint3(30, 20, 0), mleader.GetLastVertex(line));
            Assert.Equal(new GeoVector3(1, 0, 0), mleader.GetDogleg(leader));
            Assert.Equal(DxfAttachmentPoint.TopLeft, mleader.MText.Attachment);
            Assert.Equal(30 + style.DoglegLength + style.LandingGap, mleader.MText.Location.X, 9);
            Assert.Equal(20 + style.TextHeight / 2.0, mleader.MText.Location.Y, 9);
            AssertReopens(document);
        }

        [Fact]
        public void ALandingOnTheLeftOfTheArrow_HasTheWordsHeldAtTheirRightPastTheDogleg_AsAutoCadLandsThem()
        {
            // AutoCAD 2021 (probe GHAQ, an arrow at (100, 100) and a landing at (50, 150)): the dogleg runs left from the landing,
            // the words are held at their right, 0.45 on from the landing (49.55 for 50), 11 of -1 in the leader.
            CadDocument document = BlankWithMLeaderStyle();
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");

            Run(Session(document), "MLD", P(100, 0), P(30, 20), "B-12");

            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            int leader = mleader.GetLeaderIndexes().Single();
            int line = mleader.GetLeaderLineIndexes(leader).Single();
            Assert.Equal(new GeoPoint3(100, 0, 0), mleader.GetFirstVertex(line));
            Assert.Equal(new GeoPoint3(30, 20, 0), mleader.GetLastVertex(line));
            Assert.Equal(new GeoVector3(-1, 0, 0), mleader.GetDogleg(leader));
            Assert.Equal(DxfAttachmentPoint.TopRight, mleader.MText.Attachment);
            Assert.Equal(30 - style.DoglegLength - style.LandingGap, mleader.MText.Location.X, 9);
            Assert.Equal(20 + style.TextHeight / 2.0, mleader.MText.Location.Y, 9);
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
