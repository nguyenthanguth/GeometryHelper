using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The hover menus of a dimension's grips, with the words and prompts of AutoCAD 2021's grip table (<c>acdb24res.dll</c>):
    /// a linear dimension's origins and arrowheads offer Continue Dimension and Baseline Dimension, its arrowheads Flip Arrow
    /// (groups 74 and 75, drawn as AutoCAD 2021 draws them: probed in its console), the grip of the words the ways to move
    /// them.
    /// </summary>
    public sealed class GripMenuDimensionTests
    {
        // A level dimension of 100 from (0, 0) to (100, 0) with its line 30 below (ISO-25: arrowheads 2.5, DIMDLI 3.75).
        // Its grips: 0 and 1 the origins, 2 and 3 the arrowheads at (0, -30) and (100, -30), 4 the words.
        private static CadDocument WithDimension(out CadCommandSession session, out string handle, double width = 100.0)
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "DLI", P(0, 0), P(width, 0), P(width / 2.0, -30));
            handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            return document;
        }

        /// <summary>The flip flags the first dimension of a drawing is written with: groups 74 and 75 of its record, false where it has none.</summary>
        private static bool[] Flips(CadDocument document) => Flips(document.Database);

        private static bool[] Flips(DxfDatabase database)
        {
            string[] lines = database.ToText().Replace("\r", string.Empty).Split('\n');
            int at = Array.FindIndex(lines, i => i.Trim() == "DIMENSION");
            var flips = new bool[2];

            for (int i = at + 1; i + 1 < lines.Length && int.Parse(lines[i].Trim()) != 0; i += 2)
            {
                int code = int.Parse(lines[i].Trim());

                if (code == 74 || code == 75)
                {
                    flips[code - 74] = lines[i + 1].Trim() == "1";
                }
            }

            return flips;
        }

        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        /// <summary>The x range an arrowhead's solid covers whose tip is at an x on the dimension line.</summary>
        private static (double Low, double High) ArrowAt(DxfDimension dimension, double tipX)
        {
            foreach (DxfSolid solid in dimension.DimBlock.OfType<DxfSolid>().Where(s => !s.IsErased))
            {
                double[] xs = Enumerable.Range(0, 4).Select(i => solid.GetPointAt((short)i).X).ToArray();

                if (xs.Any(x => Math.Abs(x - tipX) < 1E-9))
                {
                    return (xs.Min(), xs.Max());
                }
            }

            throw new InvalidOperationException("No arrowhead has its tip at " + tipX + ".");
        }

        /// <summary>The level lines of the dimension's picture on its line, as (from x, to x), sorted.</summary>
        private static List<(double From, double To)> OnTheLine(DxfDimension dimension, double y)
        {
            return dimension
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && Math.Abs(l.StartPoint.Y - y) < 1E-9 && Math.Abs(l.EndPoint.Y - y) < 1E-9)
                .Select(l => (Math.Min(l.StartPoint.X, l.EndPoint.X), Math.Max(l.StartPoint.X, l.EndPoint.X)))
                .OrderBy(l => l.Item1)
                .ThenBy(l => l.Item2)
                .ToList();
        }

        [Fact]
        public void ALinearDimensionsOriginsOfferContinueAndBaseline_ItsArrowheadsFlipArrowToo()
        {
            CadDocument document = WithDimension(out _, out string handle);

            Assert.Equal(new[] { "Stretch", "Continue Dimension", "Baseline Dimension" }, Texts(document, handle, 0));
            Assert.Equal(new[] { "Stretch", "Continue Dimension", "Baseline Dimension" }, Texts(document, handle, 1));
            Assert.Equal(
                new[] { "Stretch", "Continue Dimension", "Baseline Dimension", "Flip Arrow" },
                Texts(document, handle, 2)
            );
            Assert.Equal(
                new[] { "Stretch", "Continue Dimension", "Baseline Dimension", "Flip Arrow" },
                Texts(document, handle, 3)
            );
            Assert.Equal(
                new[]
                {
                    "Stretch",
                    "Move Text Only",
                    "Move with Leader",
                    "Move with Dim Line",
                    "Above Dim Line",
                    "Center Vertically",
                    "Reset Text Position",
                },
                Texts(document, handle, 4)
            );
            Assert.Empty(CadGripMenus.Of(document, handle, 5));
        }

        [Fact]
        public void TheWordsOfARadiusOrAnAngle_OfferTheTextMoves_ButNoContinueOrFlip()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "CIRCLE", P(0, 0), "40");
            Run(session, "DIMRADIUS", P(0, 40), P(60, 60));
            DxfDimension radius = Assert.Single(All<DxfRadialDimension>(document.ModelSpace));
            IReadOnlyList<CadGrip> grips = CadGrips.Of(radius);
            int words = grips.Single(g => g.Kind == CadGripKind.Text).Index;

            Assert.Equal(7, CadGripMenus.Of(document, radius.Handle, words).Count);
            Assert.Empty(CadGripMenus.Of(document, radius.Handle, grips.First(g => g.Kind != CadGripKind.Text).Index));
        }

        [Fact]
        public void FlipArrowOfTheFirstArrowhead_AsksForAPick_ThenPutsItOutsideWithALineBeyondIt_AsAutoCADDrawsIt()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            double a = dimension.Dimasz;

            // Before: both arrowheads inside the extension lines.
            Assert.Equal((0.0, a), ArrowAt(dimension, 0.0));
            Assert.Equal((100.0 - a, 100.0), ArrowAt(dimension, 100.0));

            Assert.True(session.StartGripAction(handle, 2, CadGripAction.FlipArrow));
            Assert.Equal("** FLIP ARROW**", session.History.Last());
            Assert.Equal("Pick to flip arrowhead:", session.PromptText);
            Assert.False(Flips(document)[0]);

            Run(session, P(10, -20));

            Assert.False(session.IsRunning);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.True(Flips(document)[0]);
            Assert.False(Flips(document)[1]);

            // AutoCAD 2021 (a dimension of 100 with arrowheads of 0.18): the first arrowhead on the other side of the extension
            // line, a line an arrowhead long beyond it, and the dimension line drawn from its tip to where it began.
            Assert.Equal((-a, 0.0), ArrowAt(dimension, 0.0));
            Assert.Equal((100.0 - a, 100.0), ArrowAt(dimension, 100.0));
            List<(double From, double To)> line = OnTheLine(dimension, -30);
            Assert.Contains((-2.0 * a, -a), line);
            Assert.Contains((0.0, a), line);
            Assert.Equal("GRIP", document.NextUndo.Name);
            AssertReopens(document);

            // Flipped again it is as it was, and Undo gives each step back.
            Assert.True(session.StartGripAction(handle, 2, CadGripAction.FlipArrow));
            Run(session, P(10, -20));
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(Flips(document)[0]);
            Assert.Equal((0.0, a), ArrowAt(dimension, 0.0));
            Assert.DoesNotContain((-2.0 * a, -a), OnTheLine(dimension, -30));
            Assert.True(document.Undo());
            Assert.True(Flips(document)[0]);
        }

        [Fact]
        public void FlipArrowOfTheSecondArrowhead_FlipsThatOneOnly_AndEscLeavesBothAsTheyWere()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            double a = Entity<DxfRotatedDimension>(document, handle).Dimasz;

            Assert.True(session.StartGripAction(handle, 3, CadGripAction.FlipArrow));
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.NotEqual("GRIP", document.NextUndo.Name);
            Assert.False(Flips(document)[1]);

            Assert.True(session.StartGripAction(handle, 3, CadGripAction.FlipArrow));
            Run(session, P(90, -20));
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(Flips(document)[0]);
            Assert.True(Flips(document)[1]);
            Assert.Equal((100.0, 100.0 + a), ArrowAt(dimension, 100.0));
            Assert.Equal((0.0, a), ArrowAt(dimension, 0.0));
            Assert.Contains((100.0 + a, 100.0 + 2.0 * a), OnTheLine(dimension, -30));
        }

        [Fact]
        public void FlipArrowOfAnArrowheadAlreadyOutside_PutsItInside_AndTakesItsShortLineAway()
        {
            // A dimension of 3 with arrowheads of 2.5: both stand outside, on lines 2.5 long (the words past the second end).
            CadDocument document = WithDimension(out CadCommandSession session, out string handle, 3.0);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            double a = dimension.Dimasz;
            Assert.Equal((-a, 0.0), ArrowAt(dimension, 0.0));
            Assert.Contains((-2.0 * a, -a), OnTheLine(dimension, -30));

            Assert.True(session.StartGripAction(handle, 2, CadGripAction.FlipArrow));
            Run(session, P(0, -20));

            // AutoCAD 2021: inside, with a line an arrowhead long on inward from its back, the one outside gone.
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal((0.0, a), ArrowAt(dimension, 0.0));
            List<(double From, double To)> line = OnTheLine(dimension, -30);
            Assert.Contains((a, 2.0 * a), line);
            Assert.DoesNotContain((-2.0 * a, -a), line);
        }

        [Fact]
        public void AFlippedArrowheadSurvivesTheDrawingBeingWrittenAndReadAgain()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            Assert.True(session.StartGripAction(handle, 3, CadGripAction.FlipArrow));
            Run(session, P(90, -20));

            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            Assert.True(Flips(again)[1]);
            Assert.False(Flips(again)[0]);
        }

        [Fact]
        public void ContinueDimensionFromTheSecondOrigin_AsksForTheEndpoint_AndMakesTheNextOneOnTheSameLine()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.True(session.StartGripAction(handle, 1, CadGripAction.ContinueDimension));
            Assert.Equal("** CONTINUE DIMENSION **", session.History.Last(h => h.StartsWith("** ")));
            Assert.StartsWith("Specify endpoint of dimension line", session.PromptText, StringComparison.Ordinal);
            Assert.DoesNotContain("Select", session.PromptText, StringComparison.Ordinal);

            Run(session, P(300, 0));
            Run(session, Enter);

            Assert.False(session.IsRunning);
            List<DxfRotatedDimension> all = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(2, all.Count);
            DxfRotatedDimension made = all.Single(d => d.Handle != handle);
            Near(new GeoPoint3(100, 0, 0), made.XLine1Point);
            Near(new GeoPoint3(300, 0, 0), made.XLine2Point);
            Assert.Equal(200.0, made.Measurement, 9);
            Assert.Equal(-30.0, made.DimLinePoint.Y, 9);
            Assert.True(document.Undo());
            Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
        }

        [Fact]
        public void BaselineDimensionFromTheFirstOrigin_StartsFromIt_ADimdliFurtherOut()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            double spacing = Entity<DxfRotatedDimension>(document, handle).Dimdli;

            Assert.True(session.StartGripAction(handle, 2, CadGripAction.BaselineDimension));
            Assert.Equal("** BASELINE DIMENSION **", session.History.Last(h => h.StartsWith("** ")));
            Run(session, P(300, 0));
            Run(session, Enter);

            DxfRotatedDimension made = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);
            Near(new GeoPoint3(0, 0, 0), made.XLine1Point);
            Near(new GeoPoint3(300, 0, 0), made.XLine2Point);
            Assert.Equal(300.0, made.Measurement, 9);
            Assert.Equal(-30.0 - spacing, made.DimLinePoint.Y, 9);
        }

        [Fact]
        public void ContinueDimensionFromTheFirstOrigin_GoesOnTheOtherWay_AndEscMakesNothing()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.ContinueDimension));
            Run(session, P(-80, 0));
            Run(session, Esc);

            DxfRotatedDimension made = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);
            Near(new GeoPoint3(0, 0, 0), made.XLine1Point);
            Near(new GeoPoint3(-80, 0, 0), made.XLine2Point);
            Assert.Equal(80.0, made.Measurement, 9);

            Assert.True(session.StartGripAction(handle, 1, CadGripAction.BaselineDimension));
            Run(session, Esc);
            Assert.Equal(2, All<DxfRotatedDimension>(document.ModelSpace).Count);
        }

        [Fact]
        public void MoveWithDimLine_PutsTheWordsAtThePoint_TheLineThroughIt_AndKeepsTheStylesDimtmove()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.MoveWithDimLine));
            Assert.Equal("** MOVE WITH DIM LINE **", session.History.Last());
            Assert.Equal("Specify destination point:", session.PromptText);
            Run(session, P(60, -50));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(dimension.UsingDefaultTextPosition);
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -50, 0), dimension.DimLinePoint);
            Assert.Equal(0, dimension.Dimtmove);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.True(Entity<DxfRotatedDimension>(document, handle).UsingDefaultTextPosition);
        }

        [Fact]
        public void MoveTextOnly_LeavesTheLineWhereItIs_AndMoveWithLeaderDrawsALeaderToTheWords()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            int lines = Entity<DxfRotatedDimension>(document, handle)
                .DimBlock.OfType<DxfLine>()
                .Count(l => !l.IsErased);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.MoveTextOnly));
            Assert.Equal("** MOVE TEXT ONLY **", session.History.Last());
            Assert.Equal("Specify destination point:", session.PromptText);
            Run(session, P(60, -50));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -30, 0), dimension.DimLinePoint);
            Assert.Equal(2, dimension.Dimtmove);
            int alone = dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
            Assert.True(document.Undo());

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.MoveWithLeader));
            Assert.Equal("** MOVE WITH LEADER **", session.History.Last());
            Run(session, P(60, -50));

            dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -30, 0), dimension.DimLinePoint);
            Assert.Equal(1, dimension.Dimtmove);
            Assert.True(
                dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased) > alone,
                "The leader to the words is a line more than the words alone have."
            );
            Assert.True(alone >= lines - 1);
        }

        [Fact]
        public void CenterVerticallyAndAboveDimLine_AskForAPick_ThenSetDimtadAndStandTheWordsOnOrAboveTheLine()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            double Height() =>
                Entity<DxfRotatedDimension>(document, handle)
                    .DimBlock.OfType<DxfMText>()
                    .Single(m => !m.IsErased)
                    .Location.Y;
            Assert.True(Height() > -30.0, "The words stand above the line at DIMTAD 1.");

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.CenterVertically));
            Assert.Equal("** CENTER VERTICALLY **", session.History.Last());
            Assert.Equal("Pick to center text vertically with dimension line:", session.PromptText);
            Run(session, P(50, -30));
            Assert.Equal(0, Entity<DxfRotatedDimension>(document, handle).Dimtad);
            Assert.Equal(-30.0, Height(), 6);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.AboveDimLine));
            Assert.Equal("** ABOVE DIMENSION LINE **", session.History.Last());
            Assert.Equal("Pick to place text above dimension line:", session.PromptText);
            Run(session, P(50, -30));
            Assert.Equal(1, Entity<DxfRotatedDimension>(document, handle).Dimtad);
            Assert.True(Height() > -30.0);
        }

        [Fact]
        public void CenterVerticallyOfWordsPutByHand_BringsThemBackOntoTheLineAtTheSamePlaceAlongIt()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            Assert.True(session.StartGripAction(handle, 4, CadGripAction.MoveTextOnly));
            Run(session, P(60, -50));

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.CenterVertically));
            Run(session, P(0, 0));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(60, -30, 0), dimension.TextPosition);
            Assert.Equal(0, dimension.Dimtad);
        }

        [Fact]
        public void ResetTextPosition_PutsTheWordsWhereTheStylePutsThem_AndEscLeavesThemBe()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);
            Assert.True(session.StartGripAction(handle, 4, CadGripAction.MoveTextOnly));
            Run(session, P(60, -50));
            Assert.False(Entity<DxfRotatedDimension>(document, handle).UsingDefaultTextPosition);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.ResetTextPosition));
            Assert.Equal("** RESET TEXT POSITION **", session.History.Last());
            Assert.Equal("Pick to reset text to default location:", session.PromptText);
            Run(session, Esc);
            Assert.False(Entity<DxfRotatedDimension>(document, handle).UsingDefaultTextPosition);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.ResetTextPosition));
            Run(session, P(0, 0));
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.True(dimension.UsingDefaultTextPosition);
            Assert.Equal(50.0, CadGrips.Of(dimension)[4].Point.X, 6);
        }

        [Fact]
        public void AnItemOfAnotherGrip_StartsNothing()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.False(session.StartGripAction(handle, 0, CadGripAction.FlipArrow));
            Assert.False(session.StartGripAction(handle, 4, CadGripAction.ContinueDimension));
            Assert.False(session.StartGripAction(handle, 2, CadGripAction.MoveTextOnly));
            Assert.False(session.IsRunning);
            Assert.NotEqual("GRIP", document.NextUndo.Name);
        }
    }
}
