using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The Rows, Columns and Levels options of ARRAYRECT and ARRAYPOLAR as AutoCAD 2021's console asks them (probed through its
    /// standard input over a line of 100): the number, the distance between with Total as "Enter total distance between start
    /// and end rows", where nought is "Value must be nonzero." and asked again, and for Levels a distance too.
    /// </summary>
    public class ArrayRowsAndLevelsTests
    {
        private const string Grip =
            "Select grip to edit array or [ASsociative/Base point/COUnt/Spacing/COLumns/Rows/Levels/eXit]<eXit>:";

        private static CadCommandSession Rectangular() => Run(Session(Plan()), "ARRAYRECT", new[] { "A1" }, Enter);

        [Fact]
        public void LevelsAsksTheNumberThenTheDistanceBetweenLevels_AndTheGripAgain()
        {
            CadCommandSession session = Rectangular();

            Run(session, "L");
            Assert.Equal("Enter the number of levels or [Expression] <1>:", session.PromptText);

            Run(session, "4");
            Assert.Equal("Specify the distance between levels or [Total/Expression] <1.0000>:", session.PromptText);

            Run(session, "20");
            Assert.Equal(Grip, session.PromptText);
        }

        [Fact]
        public void LevelsTotal_AsksTheTotalDistanceBetweenStartAndEndLevels()
        {
            CadCommandSession session = Rectangular();

            Run(session, "L", "3", "T");

            Assert.Equal("Enter total distance between start and end levels <2.0000>:", session.PromptText);

            Run(session, "30");
            Assert.Equal(Grip, session.PromptText);
        }

        [Fact]
        public void ATotalDistanceOfColumnsAndRows_IsAskedAsTheTotalBetweenStartAndEnd()
        {
            CadCommandSession session = Rectangular();

            Run(session, "COL", "4", "T");
            Assert.Equal("Enter total distance between start and end columns <450.0000>:", session.PromptText);

            Run(session, "300", "R", "3", "T");
            Assert.StartsWith("Enter total distance between start and end rows <", session.PromptText);
        }

        [Theory]
        [InlineData("COL", "4", "Specify the distance between columns")]
        [InlineData("L", "2", "Specify the distance between levels")]
        public void ADistanceOfNought_IsValueMustBeNonzero_AndAskedAgain(string option, string count, string message)
        {
            CadCommandSession session = Rectangular();
            Run(session, option, count);
            string prompt = session.PromptText;
            Assert.StartsWith(message, prompt);
            int before = session.History.Count;

            Run(session, "0");

            Assert.Equal(new[] { prompt + " 0", "Value must be nonzero." }, session.History.Skip(before));
            Assert.Equal(prompt, session.PromptText);

            Run(session, "T");
            string total = session.PromptText;
            before = session.History.Count;
            Run(session, "0");

            Assert.Equal(new[] { total + " 0", "Value must be nonzero." }, session.History.Skip(before));
            Assert.Equal(total, session.PromptText);
        }

        [Fact]
        public void TextAtTheTotalDistance_IsRequiresNumericDistanceOrTwoPoints()
        {
            CadCommandSession session = Rectangular();
            Run(session, "COL", "4", "T");
            string total = session.PromptText;
            int before = session.History.Count;

            Run(session, "T");

            Assert.Equal(
                new[] { total + " T", "Requires numeric distance or two points." },
                session.History.Skip(before)
            );
        }

        [Fact]
        public void PolarRowsAndLevels_AskTheSameWords()
        {
            CadCommandSession session = Run(Session(Plan()), "ARRAYPOLAR", new[] { "A1" }, Enter, P(50, 50));

            Run(session, "ROW", "3", "T");
            Assert.Equal("Enter total distance between start and end rows <2.0000>:", session.PromptText);

            Run(session, "40", Enter, "L", "2");
            Assert.Equal("Specify the distance between levels or [Total/Expression] <1.0000>:", session.PromptText);

            Run(session, "T");
            Assert.Equal("Enter total distance between start and end levels <1.0000>:", session.PromptText);

            Run(session, "30");
            Assert.EndsWith("ROTate items/eXit]<eXit>:", session.PromptText);
        }
    }
}
