using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// COPY's Array and mOde options and COPYMODE, as AutoCAD 2021's console asks and does them (probed with a line and a
    /// circle): "Specify second point or [Array] &lt;use first point as displacement&gt;:", "Enter number of items to array:"
    /// and "Specify second point or [Fit]:", the items counted with the original, Fit putting the last copy at the point.
    /// </summary>
    public class CopyArrayAndModeTests
    {
        /// <summary>The plan with its circle A3 (radius 10 about (200, 0)) selected and COPY started.</summary>
        private static CadCommandSession Started(out CadDocument document)
        {
            document = Plan();
            document.Selection.Set(new[] { "A3" });
            CadCommandSession session = Session(document);
            Run(session, "COPY");
            return session;
        }

        private static double[] Centres(CadDocument document) =>
            All<DxfCircle>(document.ModelSpace).Select(c => c.Center.X).ToArray();

        [Fact]
        public void AKeywordWithLowerCaseLettersBeforeItsCapital_IsTakenByThoseLettersAndTheCapitalTogether()
        {
            CadPrompt prompt = CadPrompt.ForKeyword("Enter an option", new[] { "mOde", "eXit", "Undo" });

            Assert.Equal("mOde", prompt.MatchKeyword("O"));
            Assert.Equal("mOde", prompt.MatchKeyword("MO"));
            Assert.Equal("mOde", prompt.MatchKeyword("mod"));
            Assert.Equal("eXit", prompt.MatchKeyword("EX"));
            Assert.Equal("eXit", prompt.MatchKeyword("x"));
            Assert.Null(prompt.MatchKeyword("M"));
            Assert.Null(prompt.MatchKeyword("E"));
            Assert.Equal("Undo", prompt.MatchKeyword("u"));
        }

        [Fact]
        public void TheBasePointPrompt_OffersDisplacementAndMode_AsAutoCadWritesIt()
        {
            CadCommandSession session = Started(out _);

            Assert.Equal("Specify base point or [Displacement/mOde] <Displacement>:", session.PromptText);
            Assert.Equal("Current settings:  Copy mode = Multiple", session.History.Last());
        }

        [Fact]
        public void ModeSingle_AsksTheOptionWithMultipleAsDefault_AndThenOffersMultiple()
        {
            CadCommandSession session = Started(out _);

            Run(session, "MO");
            Assert.Equal("Enter a copy mode option [Single/Multiple] <Multiple>:", session.PromptText);
            Run(session, "S");
            Assert.Equal("Specify base point or [Displacement/mOde/Multiple] <Displacement>:", session.PromptText);
        }

        [Fact]
        public void ModeSingle_MakesOneCopyAndEnds_AndStaysSingleForTheNextCommand()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, "MO", "S", P(200, 0));
            Assert.Equal("Specify second point or [Array] <use first point as displacement>:", session.PromptText);
            Run(session, P(250, 0));

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { 200.0, 250.0 }, Centres(document));

            document.Selection.Set(new[] { "A3" });
            Run(session, "COPY");
            Assert.Equal("Current settings:  Copy mode = Single", session.History.Last());
            Assert.Equal("Specify base point or [Displacement/mOde/Multiple] <Displacement>:", session.PromptText);
        }

        [Fact]
        public void TheMultipleKeywordInSingleMode_ContinuesForThisRunOnly_AndLeavesCopymodeAlone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "COPYMODE");
            Assert.Equal("Enter new value for COPYMODE <0>:", session.PromptText);
            Run(session, "1");

            document.Selection.Set(new[] { "A3" });
            Run(session, "COPY", "M", P(200, 0), P(220, 0), P(240, 0));
            Assert.Equal("Specify second point or [Array/Exit/Undo] <Exit>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { 200.0, 220.0, 240.0 }, Centres(document));

            Run(session, "COPYMODE");
            Assert.Equal("Enter new value for COPYMODE <1>:", session.PromptText);
        }

        [Fact]
        public void ModeMultiple_AfterSingle_GoesBackToTheLoop()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, "MO", "S", "MO");
            Assert.Equal("Enter a copy mode option [Single/Multiple] <Single>:", session.PromptText);
            Run(session, "M");
            Assert.Equal("Specify base point or [Displacement/mOde] <Displacement>:", session.PromptText);
            Run(session, P(200, 0), P(230, 0));
            Assert.Equal("Specify second point or [Array/Exit/Undo] <Exit>:", session.PromptText);
            Assert.True(session.IsRunning);
            Assert.Equal(new[] { 200.0, 230.0 }, Centres(document));
        }

        [Fact]
        public void Array_AsksTheItemsThenTheStep_AndMakesTheCopiesWithTheOriginalCounted()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), "A");
            Assert.Equal("Enter number of items to array:", session.PromptText);
            Run(session, "4");
            Assert.Equal("Specify second point or [Fit]:", session.PromptText);
            Run(session, P(230, 0));

            Assert.Equal(new[] { 200.0, 230.0, 260.0, 290.0 }, Centres(document));
            Assert.Equal("Specify second point or [Array/Exit/Undo] <Exit>:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal("COPY", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void ArrayWithFit_PutsTheLastCopyAtThePoint_AndTheOthersEvenlyBetween()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), "A", "3", "F");
            Assert.Equal("Specify second point or [Array]:", session.PromptText);
            Run(session, P(207, 10));

            var circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(3, circles.Count);
            Near(new GeoPoint3(203.5, 5, 0), circles[1].Center);
            Near(new GeoPoint3(207, 10, 0), circles[2].Center);
        }

        [Fact]
        public void ArrayThenFitThenArray_TurnsBetweenTheTwoWaysOfReadingThePoint()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), "A", "3", "F", "A");
            Assert.Equal("Specify second point or [Fit]:", session.PromptText);
            Run(session, P(210, 0));

            Assert.Equal(new[] { 200.0, 210.0, 220.0 }, Centres(document));
        }

        [Fact]
        public void ArrayItems_BelowTwoOrNotANumber_AreRefusedAndAskedAgain()
        {
            CadCommandSession session = Started(out _);

            Run(session, P(200, 0), "A", "1");
            Assert.Equal("Requires an integer between 2 and 32767.", session.History.Last());
            Assert.Equal("Enter number of items to array:", session.PromptText);
            Run(session, "32768");
            Assert.Equal("Requires an integer between 2 and 32767.", session.History.Last());
            Run(session, "0");
            Assert.Equal("Enter number of items to array:", session.PromptText);
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void ArrayWithNoNumberAndNoDefault_GoesBackToTheCopyPrompt_AsAutoCadDoes()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), "A", Enter);

            Assert.True(session.IsRunning);
            Assert.Equal("Specify second point or [Array/Exit/Undo] <Exit>:", session.PromptText);
            Assert.Single(Centres(document));
        }

        [Fact]
        public void ArrayAskedAgainInTheSameRun_OffersTheLastNumberAsItsDefault_ButNotInANewRun()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), "A", "3", P(220, 0), "A");
            Assert.Equal("Enter number of items to array <3>:", session.PromptText);
            Run(session, Enter, P(205, 0));
            Assert.Equal(new[] { 200.0, 220.0, 240.0, 205.0, 210.0 }, Centres(document));
            Run(session, Enter);

            document.Selection.Set(new[] { "A3" });
            Run(session, "COPY", P(200, 0), "A");
            Assert.Equal("Enter number of items to array:", session.PromptText);
        }

        [Fact]
        public void UndoAfterAnArray_TakesBackEveryCopyOfIt()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, P(200, 0), P(210, 0), "A", "3", P(240, 0));
            Assert.Equal(new[] { 200.0, 210.0, 240.0, 280.0 }, Centres(document));
            Run(session, "U");
            Assert.Equal(new[] { 200.0, 210.0 }, Centres(document));
            Run(session, "U", Enter);
            Assert.Equal(new[] { 200.0 }, Centres(document));
        }

        [Fact]
        public void ArrayInSingleMode_EndsTheCommandAfterTheArray()
        {
            CadCommandSession session = Started(out CadDocument document);

            Run(session, "MO", "S", P(200, 0), "A", "3", P(230, 0));

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { 200.0, 230.0, 260.0 }, Centres(document));
        }
    }
}
