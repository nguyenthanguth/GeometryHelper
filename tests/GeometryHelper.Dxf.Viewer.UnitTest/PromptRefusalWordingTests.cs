using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What AutoCAD 2021 says to an answer its prompt for a number cannot take, each prompt asked in its console through the
    /// standard input with text, 2.5, nought, a negative number and one past the most: the words, and the prompt asked again.
    /// </summary>
    public class PromptRefusalWordingTests
    {
        /// <summary>Types one answer and checks the history says the answer and the words, and that the same prompt is asked again.</summary>
        private static void Refuses(CadCommandSession session, string typed, string words)
        {
            string prompt = session.PromptText;
            int before = session.History.Count;

            Run(session, typed);

            Assert.Equal(new[] { prompt + " " + typed, words }, session.History.Skip(before));
            Assert.Equal(prompt, session.PromptText);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("2.5")]
        [InlineData("2")]
        [InlineData("1025")]
        [InlineData("0")]
        [InlineData("-3")]
        public void PolygonSidesThatAreNoNumberBetween3And1024_AreRefusedWithTheRange(string typed)
        {
            CadCommandSession session = Run(Session(Plan()), "POLYGON");

            Refuses(session, typed, "Requires an integer between 3 and 1024.");
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("2.5")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("40000")]
        public void RowsAndColumnsOfMinusArray_AreRefusedWithTheRange_AndAskedAgain(string typed)
        {
            CadCommandSession session = Run(Session(Plan()), "-ARRAY", P(50, 0), Enter, "R");

            Refuses(session, typed, "Requires an integer between 1 and 32767.");
            Run(session, "3");
            Assert.StartsWith("Enter the number of columns", session.PromptText);
            Refuses(session, typed, "Requires an integer between 1 and 32767.");
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("2.5")]
        [InlineData("0")]
        [InlineData("-2")]
        [InlineData("40000")]
        public void TheItemsOfAPolarMinusArray_AreRefusedWithTheRange_AndAskedAgain(string typed)
        {
            CadCommandSession session = Run(Session(Plan()), "-ARRAY", P(50, 0), Enter, "P", "0,50");

            Assert.Equal("Enter the number of items in the array:", session.PromptText);
            Refuses(session, typed, "Requires an integer between 1 and 32767.");
        }

        [Theory]
        [InlineData("abc", "Requires a positive nonzero integer or option keyword.")]
        [InlineData("2.5", "Requires a positive nonzero integer or option keyword.")]
        [InlineData("0", "Value must be positive and nonzero.")]
        [InlineData("-1", "Value must be positive and nonzero.")]
        [InlineData("32768", "Requires an integer between 1 and 32767, or option keyword.")]
        [InlineData("100000", "Requires an integer between 1 and 32767, or option keyword.")]
        public void TheCountsOfTheArrayCommands_AreRefusedAsAutoCadRefusesThem_AndAskedAgain(string typed, string words)
        {
            CadCommandSession rectangle = Run(Session(Plan()), "ARRAYRECT", P(50, 0), Enter, "COU");
            Refuses(rectangle, typed, words);

            CadCommandSession levels = Run(Session(Plan()), "ARRAYRECT", P(50, 0), Enter, "L");
            Assert.StartsWith("Enter the number of levels", levels.PromptText);
            Refuses(levels, typed, words);

            CadCommandSession polar = Run(Session(Plan()), "ARRAYPOLAR", P(50, 0), Enter, "50,50", "I");
            Refuses(polar, typed, words);
        }

        [Fact]
        public void ACountThatWasRefused_AskedAgain_TakesTheNumberRight()
        {
            CadCommandSession session = Run(Session(Plan()), "ARRAYRECT", P(50, 0), Enter, "COU", "0", "3");

            Assert.StartsWith("Enter number of rows", session.PromptText);
        }

        [Theory]
        [InlineData("abc", "Requires an integer between 2 and 32767, or option keyword.")]
        [InlineData("2.5", "Requires an integer between 2 and 32767, or option keyword.")]
        [InlineData("1", "Requires an integer between 2 and 32767, or option keyword.")]
        [InlineData("33000", "Requires an integer between 2 and 32767, or option keyword.")]
        public void TheSegmentsOfDivide_AreRefusedWithTheRange(string typed, string words)
        {
            CadCommandSession session = Run(Session(Plan()), "DIVIDE", P(50, 0));

            Refuses(session, typed, words);
        }

        [Theory]
        [InlineData("abc", "Requires numeric distance, two points, or option keyword.")]
        [InlineData("0", "Value must be positive and nonzero.")]
        [InlineData("-5", "Value must be positive and nonzero.")]
        public void TheSegmentLengthOfMeasure_IsRefusedAsAutoCadRefusesIt(string typed, string words)
        {
            CadCommandSession session = Run(Session(Plan()), "MEASURE", P(50, 0));

            Refuses(session, typed, words);
        }

        [Theory]
        [InlineData("abc", "Requires numeric distance or two points.")]
        [InlineData("-5", "Value must be positive.")]
        public void TheChamferDistances_AreRefusedAsAutoCadRefusesThem_AndAskedAgain(string typed, string words)
        {
            CadCommandSession session = Run(Session(Plan()), "CHAMFER", "D");
            Assert.StartsWith("Specify first chamfer distance", session.PromptText);
            Refuses(session, typed, words);

            Run(session, "5");
            Assert.StartsWith("Specify second chamfer distance", session.PromptText);
            Refuses(session, typed, words);
        }

        [Fact]
        public void TheChamferLengthOfTheAngleMethod_IsRefusedWhenNegative_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "CHAMFER", "A");

            Assert.StartsWith("Specify chamfer length on the first line", session.PromptText);
            Refuses(session, "-5", "Value must be positive.");
        }

        [Theory]
        [InlineData("abc", "Requires numeric distance or two points.")]
        [InlineData("-5", "Value must be positive.")]
        public void TheFilletRadius_IsRefusedAsAutoCadRefusesIt_AndAskedAgain(string typed, string words)
        {
            CadCommandSession session = Run(Session(Plan()), "FILLET", "R");

            Assert.StartsWith("Specify fillet radius", session.PromptText);
            Refuses(session, typed, words);
        }

        [Theory]
        [InlineData("abc", "Requires numeric distance, two points, or option keyword.")]
        [InlineData("0", "Value must be positive and nonzero.")]
        [InlineData("-5", "Value must be positive and nonzero.")]
        public void TheOffsetDistance_IsRefusedAsAutoCadRefusesIt(string typed, string words)
        {
            CadCommandSession session = Run(Session(Plan()), "OFFSET");

            Refuses(session, typed, words);
        }
    }
}
