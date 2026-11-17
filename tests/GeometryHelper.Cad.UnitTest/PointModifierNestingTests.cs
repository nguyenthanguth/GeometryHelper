using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Which point modifier AutoCAD 2021 takes inside which, probed in its console at the "Specify next point" of a LINE: the
    /// answer of each pair, and what is said where one is refused.
    /// </summary>
    public class PointModifierNestingTests
    {
        // Each row: the modifiers typed one inside the other, then the one tried and whether AutoCAD takes it.
        [Theory]
        [InlineData("TT", "FROM", false)]
        [InlineData("TT", "M2P", true)]
        [InlineData("TT", ".Y", false)]
        [InlineData("TT", "TT", false)]
        [InlineData("TT", "TK", false)]
        [InlineData(".X", "FROM", true)]
        [InlineData(".X", "M2P", true)]
        [InlineData(".X", ".Y", true)]
        [InlineData(".X", "TT", false)]
        [InlineData(".X", "TK", false)]
        [InlineData("TK", "FROM", false)]
        [InlineData("TK", "M2P", true)]
        [InlineData("TK", ".Y", false)]
        [InlineData("TK", "TT", false)]
        [InlineData("TK", "TK", false)]
        [InlineData("FROM", "FROM", true)]
        [InlineData("FROM", "M2P", true)]
        [InlineData("FROM", ".Y", true)]
        [InlineData("FROM", "TT", false)]
        [InlineData("FROM", "TK", false)]
        [InlineData("M2P", "FROM", true)]
        [InlineData("M2P", "M2P", false)]
        [InlineData("M2P", ".Y", true)]
        [InlineData("M2P", "TT", true)]
        [InlineData("M2P", "TK", true)]
        [InlineData("FROM M2P", "TT", true)]
        [InlineData("FROM M2P TT", "M2P", false)]
        [InlineData("FROM M2P TT", "FROM", false)]
        [InlineData("M2P TT", "M2P", false)]
        [InlineData("M2P TT", "FROM", false)]
        [InlineData(".X M2P", "TT", true)]
        [InlineData(".X M2P", "FROM", true)]
        [InlineData("TK M2P", "TK", true)]
        [InlineData("M2P .X", "TT", false)]
        public void AModifierInsideAnother_IsTakenOrRefused_AsAutoCad2021(string outer, string inner, bool taken)
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0");
            Run(session, outer.Split(' ').Cast<object>().ToArray());
            string prompt = session.PromptText;

            Assert.Equal(taken, session.StartPointModifier(inner));

            if (!taken)
            {
                Assert.Equal(prompt, session.PromptText);
            }
        }

        [Theory]
        [InlineData("TT", "_tt")]
        [InlineData("TT", "FROM")]
        [InlineData(".X", "_tk")]
        public void ARefusedModifierAtTtOrAFilter_IsTextForThePromptTheyWereTypedAt_NotAModifierThere(
            string outer,
            string refused
        )
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", outer, refused);

            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void ARefusedModifierAtTheFirstPoint_SaysInvalidPoint_WhereThePromptHasNoOptions()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "_tt", "_tt");

            Assert.Equal("Invalid point.", session.History.Last());
            Assert.Equal("Specify first point:", session.PromptText);
            Assert.Empty(session.TemporaryTrackPoints);
        }

        [Fact]
        public void M2PInsideM2P_IsAskedAgain_AsAutoCad()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "M2P", "M2P");

            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("First point of mid:", session.PromptText);
        }
    }
}
