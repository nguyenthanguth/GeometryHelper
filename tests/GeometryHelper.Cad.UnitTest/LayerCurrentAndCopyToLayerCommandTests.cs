using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// LAYCUR and COPYTOLAYER, as AutoCAD 2021 asks them (probed in its console): LAYCUR puts the objects selected on the
    /// current layer and says how many; COPYTOLAYER copies them onto a layer named, listed with ?, taken from an object with =
    /// or made, and asks the base point to move the copies by, or eXit.
    /// </summary>
    public class LayerCurrentAndCopyToLayerCommandTests
    {
        [Fact]
        public void TwoObjectsSelected_AreChangedToTheCurrentLayer_AndTheCountIsSaid()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "LAYCUR");
            Assert.Equal("Select objects to be changed to the current layer:", session.PromptText);

            Run(session, new[] { "A1", "A3" }, Enter);

            Assert.Equal("0", document.FindEntity("A1").Layer);
            Assert.Equal("0", document.FindEntity("A3").Layer);
            Assert.Equal("2 objects changed to layer \"0\" (the current layer).", session.History.Last());
        }

        [Fact]
        public void OneObject_IsSaidAsAutoCadSaysIt_WithNoQuotesRoundTheLayer()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetCurrent(document, "Steel").Succeeded);

            CadCommandSession session = Run(Session(document), "LAYCUR", new[] { "A3" }, Enter);

            Assert.Equal("Steel", document.FindEntity("A3").Layer);
            Assert.Equal("One object changed to layer Steel (the current layer).", session.History.Last());
        }

        [Fact]
        public void AnObjectOnALockedLayer_IsLeftWhereItIs_AndSaid()
        {
            CadDocument document = Plan();

            CadCommandSession session = Run(Session(document), "LAYCUR", new[] { "A2", "A8" }, Enter);

            Assert.Equal("Locked", document.FindEntity("A8").Layer);
            Assert.Contains("1 was on a locked layer.", session.History);
        }

        [Fact]
        public void LaycurIsOneUndoStep()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "LAYCUR", new[] { "A1", "A3" }, Enter);
            Assert.Equal("0", document.FindEntity("A1").Layer);

            Run(session, "U");

            Assert.Equal("Steel", document.FindEntity("A1").Layer);
        }

        [Fact]
        public void CopyToLayer_CopiesTheObjects_PutsTheCopiesOnTheLayerNamed_AndAsksTheBasePoint()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "COPYTOLAYER");
            Assert.Equal("Select objects to copy:", session.PromptText);

            Run(session, new[] { "A1" }, Enter);
            Assert.Equal("Specify the destination layer name or [?/= (select object)] <0>:", session.PromptText);

            Run(session, "Steel");
            Assert.Equal("1 object(s) copied and placed on layer \"Steel\".", session.History.Last());
            Assert.Equal("Specify base point or [Displacement/eXit] <eXit>:", session.PromptText);

            Run(session, Enter);

            DxfLine[] lines = All<DxfLine>(document.ModelSpace).Where(l => l.Layer == "Steel").ToArray();
            Assert.Equal(2, lines.Length);
            Assert.Equal(lines[0].StartPoint, lines[1].StartPoint);
            Assert.Equal(lines[0].EndPoint, lines[1].EndPoint);
        }

        [Fact]
        public void TheEnterAtTheDestinationTakesTheCurrentLayer_AndTwoPointsMoveTheCopies()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetCurrent(document, "Steel").Succeeded);

            CadCommandSession session = Run(Session(document), "COPYTOLAYER", new[] { "A2" }, Enter, Enter, P(0, 0));
            Assert.Equal(
                "Specify second point of displacement or <use first point as displacement>:",
                session.PromptText
            );

            Run(session, P(30, 40));

            DxfLine copy = All<DxfLine>(document.ModelSpace).Single(l => l.Layer == "Steel" && l.Handle != "A1");
            Near(new GeometryHelper.Geometry.GeoPoint3(30, 40, 0), copy.StartPoint);
            Near(new GeometryHelper.Geometry.GeoPoint3(30, 90, 0), copy.EndPoint);
            Assert.Equal("0", document.FindEntity("A2").Layer);
        }

        [Fact]
        public void TheDisplacementOption_MovesTheCopiesByTheVectorTyped()
        {
            CadDocument document = Plan();

            Run(Session(document), "-COPYTOLAYER", new[] { "A2" }, Enter, "Steel", "Displacement", P(5, 6));

            DxfLine copy = All<DxfLine>(document.ModelSpace).Single(l => l.Layer == "Steel" && l.Handle != "A1");
            Near(new GeometryHelper.Geometry.GeoPoint3(5, 6, 0), copy.StartPoint);
        }

        [Fact]
        public void ALayerTheDrawingHasNot_IsMadeWhereTheAnswerIsYes_AndLeftAloneWhereItIsNo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "COPYTOLAYER", new[] { "A2" }, Enter, "Cable");
            Assert.Equal("The layer 'Cable' does not exist. Do you want to create it? [Yes/No]:", session.PromptText);

            Run(session, "No");
            Assert.False(document.Database.LayerTable.Has("Cable"));
            Assert.StartsWith("Specify the destination layer name or", session.PromptText);

            Run(session, "Cable", "Yes");
            Assert.True(document.Database.LayerTable.Has("Cable"));
            Assert.Single(All<DxfLine>(document.ModelSpace), l => l.Layer == "Cable");
            Assert.Equal("1 object(s) copied and placed on layer \"Cable\".", session.History.Last());
        }

        [Fact]
        public void TheEqualsOptionTakesTheLayerOfAnObject_AndTheQuestionMarkListsTheLayers()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), "COPYTOLAYER", new[] { "A2" }, Enter, "?", Enter);
            Assert.Equal(new[] { "0", "Locked", "Steel" }, session.History.Skip(session.History.Count - 3));

            Run(session, "=");
            Assert.Equal("Select an object with the desired layer name:", session.PromptText);

            Run(session, (object)new[] { "A1" });
            Assert.Equal("1 object(s) copied and placed on layer \"Steel\".", session.History.Last());
        }

        [Fact]
        public void ANameWithACharacterALayerCannotHave_IsRefused_AndAskedAgain()
        {
            CadDocument document = Plan();

            CadCommandSession session = Run(Session(document), "COPYTOLAYER", new[] { "A2" }, Enter, "a*b");

            Assert.Contains("Invalid characters in layer name.", session.History);
            Assert.StartsWith("Specify the destination layer name or", session.PromptText);
        }

        [Fact]
        public void CopyToLayerIsOneUndoStep_TheCopiesAndTheMoveTogether()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(
                Session(document),
                "COPYTOLAYER",
                new[] { "A1" },
                Enter,
                "0",
                P(0, 0),
                P(10, 10)
            );

            Assert.Equal(4, All<DxfLine>(document.ModelSpace).Count);

            Run(session, "U");

            Assert.Equal(1, All<DxfLine>(document.ModelSpace).Count(l => l.Layer == "Steel"));
            Assert.Equal(3, All<DxfLine>(document.ModelSpace).Count);
        }
    }
}
