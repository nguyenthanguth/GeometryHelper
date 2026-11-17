using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The commands that ask the corners or the points of a selection themselves, as STRETCH and the crossing and the fence of
    /// TRIM and EXTEND do, draw what AutoCAD 2021 draws at "Select objects:": the box of a window or a crossing, the polygon of
    /// CP, the line of a fence, as the options of "Select objects:" do, and no rubber band from the first point. The other
    /// prompts that ask the opposite corner of a box, as ZOOM Window does, draw a rectangle and no line either.
    /// </summary>
    public sealed class OwnCornerPromptBandTests
    {
        [Fact]
        public void StretchAskingTheOppositeCorner_BandsABoxFromTheFirstCorner()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "STRETCH", P(110, -10));

            Assert.Equal("Specify opposite corner:", session.PromptText);
            Assert.NotNull(session.Prompt.Band);
            Assert.Equal(CadSelectionBandKind.Box, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(110, -10) }, session.Prompt.Band.Points.ToArray());
        }

        [Theory]
        [InlineData("C", CadSelectionBandKind.Crossing)]
        [InlineData("W", CadSelectionBandKind.Window)]
        public void StretchWithCrossingOrWindowTyped_BandsThatBox(string typed, CadSelectionBandKind kind)
        {
            CadCommandSession session = Session(Plan());
            Run(session, "STRETCH", typed, P(110, -10));

            Assert.Equal("Specify opposite corner:", session.PromptText);
            Assert.NotNull(session.Prompt.Band);
            Assert.Equal(kind, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(110, -10) }, session.Prompt.Band.Points.ToArray());
        }

        [Fact]
        public void StretchWithACrossingPolygon_BandsThePolygonThroughTheTwoPointsGiven()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "STRETCH", "CP", P(90, -10));

            Assert.Equal("Specify endpoint of line or [Undo]:", session.PromptText);
            Assert.Equal(CadSelectionBandKind.CrossingPolygon, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(90, -10) }, session.Prompt.Band.Points.ToArray());

            Run(session, P(110, -10));

            Assert.Equal(CadSelectionBandKind.CrossingPolygon, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(90, -10), P(110, -10) }, session.Prompt.Band.Points.ToArray());

            Run(session, "U");

            Assert.Equal(new[] { P(90, -10) }, session.Prompt.Band.Points.ToArray());
        }

        [Fact]
        public void TrimsCrossingBox_IsBandedAsACrossing()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "TRIM", "C", P(50, 10));

            Assert.Equal("Specify opposite corner:", session.PromptText);
            Assert.NotNull(session.Prompt.Band);
            Assert.Equal(CadSelectionBandKind.Crossing, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(50, 10) }, session.Prompt.Band.Points.ToArray());
        }

        [Fact]
        public void ExtendsFence_IsBandedThroughItsPointsGiven()
        {
            CadCommandSession session = Session(Plan());
            Run(session, "EXTEND", "F", P(50, -10), P(60, 10));

            Assert.Equal("Specify next fence point or [Undo]:", session.PromptText);
            Assert.NotNull(session.Prompt.Band);
            Assert.Equal(CadSelectionBandKind.Fence, session.Prompt.Band.Kind);
            Assert.Equal(new[] { P(50, -10), P(60, 10) }, session.Prompt.Band.Points.ToArray());
        }

        [Fact]
        public void ZoomWindowAskingTheOppositeCorner_AsksForARectangle_NotALine()
        {
            CadCommandSession session = Session(Plan());
            session.View = new CadView(800, 600);
            Run(session, "ZOOM", "W", P(10, 10));

            Assert.Equal("Specify opposite corner:", session.PromptText);
            Assert.True(session.Prompt.AsksCorner);
            Assert.Null(session.Prompt.Band);
        }

        [Fact]
        public void OnlyAPromptWithABasePointThatAsksACornerAsksForARectangle()
        {
            Assert.True(CadPrompt.ForPoint("Specify opposite corner", P(1, 1)).AsksCorner);
            Assert.True(CadPrompt.ForPoint("Specify opposite corner point", P(1, 1)).AsksCorner);
            Assert.False(CadPrompt.ForPoint("Specify opposite corner").AsksCorner);
            Assert.False(CadPrompt.ForPoint("Specify second point", P(1, 1)).AsksCorner);
            Assert.False(CadPrompt.ForDistance("Specify corner distance", P(1, 1)).AsksCorner);
        }
    }
}
