using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// BREAK, LAYOFF, LAYFRZ, LAYLCK and LAYULK as AutoCAD 2021's console words them (probed through its standard input): a
    /// point on nothing at BREAK is "No object found", at the layer commands "Nothing Selected."; LAYOFF and LAYFRZ write their
    /// settings first, offer [Settings/Undo] and keep their Viewports and Block nesting settings; U with nothing to undo says
    /// "Everything has been undone."; Enter at LAYLCK and LAYULK is "Nothing selected." and the prompt again.
    /// </summary>
    public class LayerPromptWordingTests
    {
        private const string Settings = "Current settings: Viewports=Vpfreeze, Block nesting level=Block";

        [Fact]
        public void BreakPickedOnNothing_SaysNoObjectFound_AndAsksAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "BREAK");
            int before = session.History.Count;

            Run(session, "700,700");

            Assert.Equal(new[] { "Select object: 700,700", "No object found" }, session.History.Skip(before));
            Assert.Equal("Select object:", session.PromptText);
        }

        [Theory]
        [InlineData("LAYOFF", "turned off")]
        [InlineData("LAYFRZ", "frozen")]
        public void ALayerCommandStartsWithItsSettings_AndOffersSettingsAndUndo(string command, string what)
        {
            CadCommandSession session = Run(Session(Plan()), command);

            Assert.Equal(Settings, session.History.Last());
            Assert.Equal("Select an object on the layer to be " + what + " or [Settings/Undo]:", session.PromptText);
        }

        [Theory]
        [InlineData("LAYOFF")]
        [InlineData("LAYFRZ")]
        [InlineData("LAYLCK")]
        [InlineData("LAYULK")]
        public void ALayerCommandPickedOnNothing_SaysNothingSelected_AndAsksAgain(string command)
        {
            CadCommandSession session = Run(Session(Plan()), command);
            string prompt = session.PromptText;
            int before = session.History.Count;

            Run(session, "700,700");

            Assert.Equal(new[] { prompt + " 700,700", "Nothing Selected." }, session.History.Skip(before));
            Assert.Equal(prompt, session.PromptText);
        }

        [Theory]
        [InlineData("LAYLCK")]
        [InlineData("LAYULK")]
        public void EnterAtLockOrUnlock_SaysNothingSelected_AndAsksUntilAnObjectOrEsc(string command)
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), command);
            string prompt = session.PromptText;

            Run(session, Enter, Enter);

            Assert.Equal(
                new[] { "Nothing selected.", "Nothing selected." },
                session.History.Where(l => l.StartsWith("Nothing"))
            );
            Assert.True(session.IsRunning);
            Assert.Equal(prompt, session.PromptText);

            Run(session, (object)new[] { "A1" });

            Assert.False(session.IsRunning);
        }

        [Theory]
        [InlineData("LAYOFF")]
        [InlineData("LAYFRZ")]
        public void EnterAtLayoffOrLayfrz_EndsTheCommand(string command)
        {
            CadCommandSession session = Run(Session(Plan()), command, Enter);

            Assert.False(session.IsRunning);
        }

        [Theory]
        [InlineData("LAYOFF")]
        [InlineData("LAYFRZ")]
        public void UndoWithNothingDone_SaysEverythingHasBeenUndone_AndAfterOneDone_SaysNothing(string command)
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Session(document), command, new[] { "A1" });
            int before = session.History.Count;

            Run(session, "U");
            Assert.Single(session.History.Skip(before));
            Assert.DoesNotContain("Everything has been undone.", session.History);

            Run(session, "U");
            Assert.Equal("Everything has been undone.", session.History.Last());
        }

        [Fact]
        public void LayoffSettings_AskTheTypeThenTheViewportOrTheBlockNesting_AndKeepTheAnswer()
        {
            CadCommandSession session = Run(Session(Plan()), "LAYOFF", "S");
            Assert.Equal("Enter setting type for [Viewports/Block selection]:", session.PromptText);

            Run(session, "V");
            Assert.Equal("In paper space viewport use [Vpfreeze/Off] <Vpfreeze>:", session.PromptText);
            Run(session, "O");
            Assert.Equal("Select an object on the layer to be turned off or [Settings/Undo]:", session.PromptText);

            Run(session, "S", "B");
            Assert.Equal("Enter Block Selection nesting level [Block/Entity/None] <Block>:", session.PromptText);
            Run(session, "N");
            Run(session, Esc, "LAYOFF");
            Assert.Equal("Current settings: Viewports=Off, Block nesting level=None", session.History.Last());

            Run(session, "S", "V");
            Assert.Equal("In paper space viewport use [Vpfreeze/Off] <Off>:", session.PromptText);
        }

        [Fact]
        public void LayfrzSettings_OfferFreezeInPlaceOfOff_AndAreItsOwn()
        {
            CadCommandSession session = Run(Session(Plan()), "LAYFRZ", "S", "V");
            Assert.Equal("In paper space viewport use [Freeze/Vpfreeze] <Vpfreeze>:", session.PromptText);

            // Freeze is shown as Off in the settings, as AutoCAD 2021 shows it, and LAYOFF keeps its own.
            Run(session, "F", Esc, "LAYFRZ");
            Assert.Equal("Current settings: Viewports=Off, Block nesting level=Block", session.History.Last());
            Run(session, Esc, "LAYOFF");
            Assert.Equal(Settings, session.History.Last());
        }

        [Fact]
        public void LayerSettingsAnsweredWithText_AreInvalidOptionKeyword_AndEnterKeepsTheSetting()
        {
            CadCommandSession session = Run(Session(Plan()), "LAYFRZ", "S");

            Refuses(session, "abc", "Invalid option keyword.");
            Assert.Equal("Enter setting type for [Viewports/Block selection]:", session.PromptText);

            Run(session, "B");
            Refuses(session, "Nested", "Invalid option keyword.");
            Assert.Equal("Enter Block Selection nesting level [Block/Entity/None] <Block>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Select an object on the layer to be frozen or [Settings/Undo]:", session.PromptText);
        }

        private static void Refuses(CadCommandSession session, string typed, params string[] words)
        {
            string prompt = session.PromptText;
            int before = session.History.Count;

            Run(session, typed);

            Assert.Equal(new[] { prompt + " " + typed }.Concat(words), session.History.Skip(before));
        }
    }
}
