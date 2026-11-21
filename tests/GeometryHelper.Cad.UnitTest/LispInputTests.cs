using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The functions of AutoLISP that ask the user, typed at the command line as AutoCAD 2021's accoreconsole wrote them
    /// (probes q3, q4, q5, q7 and q8 of the L2 spec): <c>getpoint</c>, <c>getcorner</c>, <c>getdist</c>, <c>getangle</c>,
    /// <c>getorient</c>, <c>getint</c>, <c>getreal</c>, <c>getstring</c>, <c>getkword</c>, <c>initget</c>, <c>entsel</c> and
    /// <c>nentsel</c>, with their prompts, echoes, values and refusals, Esc, and what a routine that waits does to the
    /// session (decisions D34 to D37, D43 to D47, D60). A line of a transcript is compared without its trailing spaces: the
    /// harness drops them too.
    /// </summary>
    public sealed class LispInputTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-input-" + Guid.NewGuid().ToString("N")
        );

        public LispInputTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private static CadCommandSession Typed(params object[] inputs) => Run(Session(Blank()), inputs);

        // The history without the trailing spaces of each line.
        private static string[] Lines(CadCommandSession session) => session.History.Select(l => l.TrimEnd()).ToArray();

        // The same without the blank lines, for a prompt that starts with a new line ("\nNext: ").
        private static string[] Written(CadCommandSession session) => Lines(session).Where(l => l.Length > 0).ToArray();

        // A drawing of one line from (0, 0) to (10, 0), the way an entsel in the console picks it: on its end.
        private static CadCommandSession WithALine() => Typed("LINE", "0,0", "10,0", Enter);

        #region getpoint and getcorner

        [Fact]
        public void GetpointWithNoMessage_EchoesTheTypedTextAloneAndGivesThreeReals()
        {
            // AutoCAD 2021 (q3): Command: (getpoint) / 3,4 / (3.0 4.0 0.0)
            CadCommandSession session = Typed("(getpoint)");

            Assert.True(session.IsRunning);

            Run(session, "3,4");

            Assert.Equal(new[] { "Command: (getpoint)", "3,4", "(3.0 4.0 0.0)" }, Lines(session));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void GetpointWithAMessage_EchoesTheMessageThenTheTypedTextAndKeepsTheZ()
        {
            // AutoCAD 2021 (q3): Pick: 3,4,5 gives (3.0 4.0 5.0); the echo is the prompt then the text, no space added
            CadCommandSession session = Typed("(getpoint \"Pick: \")");

            Assert.Equal("Pick: ", session.PromptText);

            Run(session, "3,4,5");

            Assert.Equal(new[] { "Command: (getpoint \"Pick: \")", "Pick: 3,4,5", "(3.0 4.0 5.0)" }, Lines(session));
        }

        [Fact]
        public void GetpointWithABasePoint_TakesARelativePointFromIt_AndAMessageThatStartsWithANewLineAsksAfterIt()
        {
            // AutoCAD 2021 (q3): (getpoint '(1 1) "\nNext: ") then @2,0 gives (3.0 1.0 0.0); the prompt is Next:
            CadCommandSession session = Typed("(getpoint '(1 1) \"\\nNext: \")");

            Assert.Equal("Next: ", session.PromptText);

            Run(session, "@2,0");

            Assert.Equal(
                new[] { "Command: (getpoint '(1 1) \"\\nNext: \")", "Next: @2,0", "(3.0 1.0 0.0)" },
                Written(session)
            );
        }

        [Fact]
        public void GetpointWithTheMessageBeforeTheBasePoint_AsksTheMessageAndMeasuresFromThePoint()
        {
            // AutoCAD 2021 (runs/hhh): (getpoint "\nSelect next point: " goc) asked Select next point: and took 0,10 as a point; the
            // base point of the rubber band is the second argument (a second argument that is no point is still point: "b", q3)
            CadCommandSession session = Typed("(getpoint \"\\nNext: \" '(1 1))");

            Assert.Equal("Next: ", session.PromptText);

            Run(session, "@2,0");

            Assert.Equal(
                new[] { "Command: (getpoint \"\\nNext: \" '(1 1))", "Next: @2,0", "(3.0 1.0 0.0)" },
                Written(session)
            );
        }

        [Fact]
        public void GetpointAnsweredWithEnter_GivesNil()
        {
            // AutoCAD 2021 (q3): Pick: then nil
            CadCommandSession session = Typed("(getpoint \"Pick: \")", Enter);

            Assert.Equal(new[] { "Command: (getpoint \"Pick: \")", "Pick:", "nil" }, Lines(session));
        }

        [Fact]
        public void GetpointAnsweredWithTextThatIsNoPoint_RefusesAndAsksAgain()
        {
            // AutoCAD 2021 (q3): Pick: abc / Invalid point. / Pick: 5,5 / (5.0 5.0 0.0)
            CadCommandSession session = Typed("(getpoint \"Pick: \")", "abc");

            Assert.Equal(new[] { "Command: (getpoint \"Pick: \")", "Pick: abc", "Invalid point." }, Lines(session));
            Assert.True(session.IsRunning);
            Assert.Equal("Pick: ", session.PromptText);

            Run(session, "5,5");

            Assert.Equal(new[] { "Pick: 5,5", "(5.0 5.0 0.0)" }, Lines(session).Skip(3));
        }

        [Fact]
        public void AFormTypedAtAGetpoint_IsRefusedAsAFormAndAsAPoint_AndIsNotEvaluated()
        {
            // AutoCAD 2021 (q3): Pick: (list 7 8) / Can't reenter LISP. / Invalid point. / Pick: 8,9 / (8.0 9.0 0.0)
            CadCommandSession session = Typed("(setq r 0)", "(getpoint \"Pick: \")");

            Assert.False(session.IsLispInput("(setq r 1)"));
            Assert.False(session.IsLispInput("!r"));

            Run(session, "(setq r 1)");

            Assert.Equal(new[] { "Pick: (setq r 1)", "Can't reenter LISP.", "Invalid point." }, Lines(session).Skip(3));

            // the form was not evaluated: r is still 0 when the getpoint is over
            Run(session, "8,9", "!r");

            Assert.Equal(
                new[]
                {
                    "Pick: (setq r 1)",
                    "Can't reenter LISP.",
                    "Invalid point.",
                    "Pick: 8,9",
                    "(8.0 9.0 0.0)",
                    "Command: !r",
                    "0",
                },
                Lines(session).Skip(3)
            );
        }

        [Fact]
        public void ABangAtAGetpoint_IsRefusedAsAFormIs()
        {
            // AutoCAD 2021 (q7, the forms typed at a getpoint) and spec D36: text starting with ! is not AutoLISP at a get*
            // prompt; q3 showed Can't reenter LISP. for it, then the refusal of the prompt
            CadCommandSession session = Typed("(setq p 1)", "(getpoint \"P: \")", "!p");

            Assert.Equal(new[] { "P: !p", "Can't reenter LISP.", "Invalid point." }, Lines(session).Skip(3));
            Assert.Equal("P: ", session.PromptText);
        }

        [Fact]
        public void EscAtAGetpoint_WritesCancelAndTheErrorLine_AndEndsTheForm()
        {
            // AutoCAD 2021 (q3): Pick: *Cancel* / ; error: Function cancelled
            CadCommandSession session = Typed("(getpoint \"Pick: \")", Esc);

            Assert.Equal(
                new[] { "Command: (getpoint \"Pick: \")", "Pick: *Cancel*", "; error: Function cancelled" },
                Lines(session)
            );
            Assert.False(session.IsRunning);
            Assert.Equal(string.Empty, session.PromptText);
        }

        [Fact]
        public void EscAtAGetpointOfACommandWithItsOwnErrorFunction_CallsItWithTheLocalsInPlace()
        {
            // AutoCAD 2021 (q3, GHX): Command: GHX / P: *Cancel* / X:Function cancelled ("after" is never written)
            CadCommandSession session = Typed(
                "(defun c:ghx (/ *error*) (defun *error* (m) (princ (strcat \"X:\" m)) (princ)) (getpoint \"P: \") (princ \"after\"))",
                "GHX"
            );

            // spec D60: the c: is the command that runs while its prompt is up
            Assert.True(session.IsRunning);
            Assert.Equal("GHX", session.LastCommandName);
            Assert.Equal("P: ", session.PromptText);

            Run(session, Esc);

            Assert.Equal(
                new[]
                {
                    "Command: (defun c:ghx (/ *error*) (defun *error* (m) (princ (strcat \"X:\" m)) (princ)) (getpoint \"P: \") (princ \"after\"))",
                    "C:GHX",
                    "Command: GHX",
                    "P: *Cancel*",
                    "X:Function cancelled",
                },
                Lines(session)
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AGetpointInALoopOfACommand_EndsWithEsc_AndAnErrorFunctionThatTakesNoArgumentPrintsTooManyArgumentsAlone()
        {
            // AutoCAD 2021 (q7, GHH): Command: GHH / Next: 1,1 / Next: *Cancel* / too many arguments (L1 decision 2: *error* is
            // called with the message, and ketthuc takes none)
            CadCommandSession session = Typed(
                "(defun ketthuc () (princ \"kt\") (princ))",
                "(defun c:ghh (/ *error*) (setq *error* ketthuc) (while T (getpoint \"\\nNext: \")))",
                "GHH",
                "1,1"
            );

            Assert.True(session.IsRunning);
            Assert.Equal("Next: ", session.PromptText);

            Run(session, Esc);

            Assert.Equal(
                new[] { "Command: GHH", "Next: 1,1", "Next: *Cancel*", "too many arguments" },
                Written(session).Skip(4)
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void GetcornerFromABasePoint_GivesThreeReals()
        {
            // AutoCAD 2021 (q3): (getcorner '(0 0) "Corner: ") then 5,6 gives (5.0 6.0 0.0)
            CadCommandSession session = Typed("(getcorner '(0 0) \"Corner: \")", "5,6");

            Assert.Equal(
                new[] { "Command: (getcorner '(0 0) \"Corner: \")", "Corner: 5,6", "(5.0 6.0 0.0)" },
                Lines(session)
            );
        }

        #endregion

        #region getdist, getangle and getorient

        [Fact]
        public void GetdistGivenANumber_GivesItAsAReal()
        {
            // AutoCAD 2021 (q3): Dist: 2.5 gives 2.5
            CadCommandSession session = Typed("(getdist \"Dist: \")", "2.5");

            Assert.Equal(new[] { "Command: (getdist \"Dist: \")", "Dist: 2.5", "2.5" }, Lines(session));
        }

        [Fact]
        public void GetdistGivenTwoPoints_AsksForTheSecondWithALeadingSpace_AndGivesTheDistance()
        {
            // AutoCAD 2021 (q3): Dist: 0,0 then " Specify second point: 3,4" gives 5.0
            CadCommandSession session = Typed("(getdist \"Dist: \")", "0,0");

            Assert.True(session.IsRunning);

            Run(session, "3,4");

            Assert.Equal(
                new[] { "Command: (getdist \"Dist: \")", "Dist: 0,0", " Specify second point: 3,4", "5.0" },
                Lines(session)
            );
        }

        [Fact]
        public void GetdistFromABasePoint_TakesOnePoint()
        {
            // AutoCAD 2021 (q3): (getdist '(0 0) "Dist: ") then 6,8 gives 10.0
            CadCommandSession session = Typed("(getdist '(0 0) \"Dist: \")", "6,8");

            Assert.Equal(new[] { "Command: (getdist '(0 0) \"Dist: \")", "Dist: 6,8", "10.0" }, Lines(session));
        }

        [Fact]
        public void GetdistGivenText_RefusesWithRequiresNumericDistanceAndAsksAgain()
        {
            // AutoCAD 2021 (q3): Dist: abc / Requires numeric distance or two points. / Dist: 1 / 1.0
            CadCommandSession session = Typed("(getdist \"Dist: \")", "abc", "1");

            Assert.Equal(
                new[]
                {
                    "Command: (getdist \"Dist: \")",
                    "Dist: abc",
                    "Requires numeric distance or two points.",
                    "Dist: 1",
                    "1.0",
                },
                Lines(session)
            );
        }

        [Fact]
        public void GetangleGivenDegrees_GivesRadians()
        {
            // AutoCAD 2021 (q3): Ang: 45 gives 0.785398 (printed to six figures)
            CadCommandSession session = Typed("(getangle \"Ang: \")", "45");

            Assert.Equal(new[] { "Command: (getangle \"Ang: \")", "Ang: 45", "0.785398" }, Lines(session));
        }

        [Fact]
        public void GetangleFromABasePointGivenAPoint_GivesTheDirectionOfTheLine()
        {
            // AutoCAD 2021 (q3): (getangle '(0 0) "Ang: ") then 0,5 gives 1.5708
            CadCommandSession session = Typed("(getangle '(0 0) \"Ang: \")", "0,5");

            Assert.Equal(new[] { "Command: (getangle '(0 0) \"Ang: \")", "Ang: 0,5", "1.5708" }, Lines(session));
        }

        [Fact]
        public void GetorientGivenDegrees_GivesRadiansFromEastAtTheDefaults()
        {
            // AutoCAD 2021 (q3): Ori: 90 gives 1.5708 (ANGBASE and ANGDIR at their defaults; their effect is not measured)
            CadCommandSession session = Typed("(getorient \"Ori: \")", "90");

            Assert.Equal(new[] { "Command: (getorient \"Ori: \")", "Ori: 90", "1.5708" }, Lines(session));
        }

        #endregion

        #region getreal, getint, getstring and getkword

        [Fact]
        public void GetrealGivenAnInteger_GivesAReal()
        {
            // AutoCAD 2021 (q3): Real: 1.5 gives 1.5; Real: 2 gives 2.0
            CadCommandSession first = Typed("(getreal \"Real: \")", "1.5");
            CadCommandSession second = Typed("(getreal \"Real: \")", "2");

            Assert.Equal("1.5", Lines(first).Last());
            Assert.Equal("2.0", Lines(second).Last());
        }

        [Fact]
        public void GetrealGivenText_RefusesAndAsksAgain_AndEnterGivesNil()
        {
            // AutoCAD 2021 (q3): Real: abc / Requires numeric value. / Real: 2 / 2.0; Real: then Enter gives nil
            CadCommandSession refused = Typed("(getreal \"Real: \")", "abc", "2");
            CadCommandSession empty = Typed("(getreal \"Real: \")", Enter);

            Assert.Equal(
                new[] { "Command: (getreal \"Real: \")", "Real: abc", "Requires numeric value.", "Real: 2", "2.0" },
                Lines(refused)
            );
            Assert.Equal(new[] { "Command: (getreal \"Real: \")", "Real:", "nil" }, Lines(empty));
        }

        [Fact]
        public void GetintGivenTextARealAndAValueOutOfRange_RefusesEachAndTakesAnInteger()
        {
            // AutoCAD 2021 (q3): abc and 1.5 give Requires an integer value., 40000 gives Requires an integer between -32768 and 32767.
            CadCommandSession session = Typed("(getint \"Int: \")", "abc", "1.5", "40000", "7");

            Assert.Equal(
                new[]
                {
                    "Command: (getint \"Int: \")",
                    "Int: abc",
                    "Requires an integer value.",
                    "Int: 1.5",
                    "Requires an integer value.",
                    "Int: 40000",
                    "Requires an integer between -32768 and 32767.",
                    "Int: 7",
                    "7",
                },
                Lines(session)
            );
        }

        [Fact]
        public void GetintWithNoMessage_GivesTheNumberTyped()
        {
            // AutoCAD 2021 (q3): (getint) then 3 gives 3, the echo being the typed text alone
            CadCommandSession session = Typed("(getint)", "3");

            Assert.Equal(new[] { "Command: (getint)", "3", "3" }, Lines(session));
        }

        [Fact]
        public void GetstringWithoutCr_EndsAtTheSpace_AndTheRestOfTheLineIsTheNextInput()
        {
            // AutoCAD 2021 (q3): Str: hello "hello" (the value on the line of the echo, the space having ended the answer), then
            // world ran as a command: Command: world / Unknown command "WORLD".  Press F1 for help.
            CadCommandSession session = Typed("(getstring \"Str: \")");

            Assert.False(session.Prompt.TakesSpaces);

            Run(session, "hello world");

            Assert.Equal(
                new[]
                {
                    "Command: (getstring \"Str: \")",
                    "Str: hello \"hello\"",
                    "Command: world",
                    "Unknown command \"WORLD\".  Press F1 for help.",
                },
                Lines(session)
            );
        }

        [Fact]
        public void GetstringWithCr_TakesTheWholeLineWithItsSpaces()
        {
            // AutoCAD 2021 (q3): (getstring T "Str: ") then hello world gives "hello world"
            CadCommandSession session = Typed("(getstring T \"Str: \")");

            Assert.True(session.Prompt.TakesSpaces);

            Run(session, "hello world");

            Assert.Equal(
                new[] { "Command: (getstring T \"Str: \")", "Str: hello world", "\"hello world\"" },
                Lines(session)
            );
        }

        [Fact]
        public void GetstringAnsweredWithEnter_GivesTheEmptyString()
        {
            // AutoCAD 2021 (q3): Str: then Enter gives ""
            CadCommandSession session = Typed("(getstring \"Str: \")", Enter);

            Assert.Equal(new[] { "Command: (getstring \"Str: \")", "Str:", "\"\"" }, Lines(session));
        }

        [Fact]
        public void AFormTypedAtAGetstring_IsTheTextOfTheAnswer_WithTheMessageThatItIsNotEvaluated()
        {
            // AutoCAD 2021 (q3): S: (list 1 2) / Can't reenter LISP. / "(list 1 2)"
            CadCommandSession session = Typed("(getstring \"S: \")", "(list 1 2)");

            Assert.Equal(
                new[] { "Command: (getstring \"S: \")", "S: (list 1 2)", "Can't reenter LISP.", "\"(list 1 2)\"" },
                Lines(session)
            );
        }

        [Fact]
        public void OnlyAPromptForTextTakesSpaces()
        {
            // spec section 2: TakesSpaces is true for a prompt for text and false for every other kind
            Assert.True(CadPrompt.ForString("Name:").TakesSpaces);
            Assert.False(CadPrompt.ForInteger("Count:").TakesSpaces);
            Assert.False(CadPrompt.ForDistance("Distance:").TakesSpaces);
            Assert.False(CadPrompt.ForSelection().TakesSpaces);

            CadCommandSession session = Typed("(getint \"Int: \")");

            Assert.False(session.Prompt.TakesSpaces);
        }

        [Fact]
        public void GetkwordWithKeywords_TakesTheShortFormInAnyCase_AndGivesTheKeywordAsWritten()
        {
            // AutoCAD 2021 (q3, q4): (initget "Yes No") then y gives "Yes"; with N gives "No"
            CadCommandSession lower = Typed("(progn (initget \"Yes No\") (getkword \"Q: \"))", "y");
            CadCommandSession upper = Typed("(progn (initget \"Yes No\") (getkword \"Q: \"))", "N");

            Assert.Equal("\"Yes\"", Lines(lower).Last());
            Assert.Equal("\"No\"", Lines(upper).Last());
        }

        [Fact]
        public void GetkwordGivenAWordThatIsNoKeyword_RefusesAndAsksAgain()
        {
            // AutoCAD 2021 (q3): Choose [Yes/No]: maybe / Invalid option keyword. / Choose [Yes/No]: N / "No"
            CadCommandSession session = Typed("(initget \"Yes No\")", "(getkword \"Choose [Yes/No]: \")", "maybe", "N");

            Assert.Equal(
                new[]
                {
                    "Command: (initget \"Yes No\")",
                    "nil",
                    "Command: (getkword \"Choose [Yes/No]: \")",
                    "Choose [Yes/No]: maybe",
                    "Invalid option keyword.",
                    "Choose [Yes/No]: N",
                    "\"No\"",
                },
                Lines(session)
            );
        }

        [Fact]
        public void GetkwordWithNoInitget_RefusesEveryWord_AndEscEndsIt()
        {
            // AutoCAD 2021 (q4): K: nothing / Invalid option keyword. / K: *Cancel* / ; error: Function cancelled
            CadCommandSession session = Typed("(getkword \"K: \")", "nothing", Esc);

            Assert.Equal(
                new[]
                {
                    "Command: (getkword \"K: \")",
                    "K: nothing",
                    "Invalid option keyword.",
                    "K: *Cancel*",
                    "; error: Function cancelled",
                },
                Lines(session)
            );
        }

        [Fact]
        public void GetkwordWithAMessageThatStartsWithANewLine_ShowsTheRestAsThePrompt()
        {
            // AutoCAD 2021 (q4): (initget "P S") (getkword "\n pick < P/S > :") then p gives "P"; the prompt is the text after \n
            CadCommandSession session = Typed("(progn (initget \"P S\") (getkword \"\\n pick < P/S > :\"))");

            Assert.Equal(" pick < P/S > :", session.PromptText);

            Run(session, "p");

            Assert.Equal(new[] { " pick < P/S > :p", "\"P\"" }, Written(session).Skip(1));
        }

        #endregion

        #region initget

        [Fact]
        public void InitgetOnALineOfItsOwn_AppliesToTheGetFunctionOfTheNextLine()
        {
            // AutoCAD 2021 (q3): (initget 1) gives nil; then (getpoint "Need: ") refuses Enter with Point or option keyword
            // required. and takes 1,1
            CadCommandSession session = Typed("(initget 1)", "(getpoint \"Need: \")", Enter, "1,1");

            Assert.Equal(
                new[]
                {
                    "Command: (initget 1)",
                    "nil",
                    "Command: (getpoint \"Need: \")",
                    "Need:",
                    "Point or option keyword required.",
                    "Need: 1,1",
                    "(1.0 1.0 0.0)",
                },
                Lines(session)
            );
        }

        [Fact]
        public void InitgetWithKeywordsOnGetkword_RefusesEnterWithBit1()
        {
            // AutoCAD 2021 (q3): (initget 1 "Yes No") then Enter gives Invalid option keyword. and asks again; No gives "No"
            CadCommandSession session = Typed(
                "(initget 1 \"Yes No\")",
                "(getkword \"Choose [Yes/No]: \")",
                Enter,
                "No"
            );

            Assert.Equal(
                new[]
                {
                    "Command: (getkword \"Choose [Yes/No]: \")",
                    "Choose [Yes/No]:",
                    "Invalid option keyword.",
                    "Choose [Yes/No]: No",
                    "\"No\"",
                },
                Lines(session).Skip(2)
            );
        }

        [Fact]
        public void InitgetBit2_RefusesZero()
        {
            // AutoCAD 2021 (q3): Nonzero: 0 / Value must be nonzero. / Nonzero: 3 / 3
            CadCommandSession session = Typed("(initget 2)", "(getint \"Nonzero: \")", "0", "3");

            Assert.Equal(
                new[] { "Command: (getint \"Nonzero: \")", "Nonzero: 0", "Value must be nonzero.", "Nonzero: 3", "3" },
                Lines(session).Skip(2)
            );
        }

        [Fact]
        public void InitgetBit4_RefusesANegativeNumber()
        {
            // AutoCAD 2021 (q3): Nonneg: -1 / Value must be positive. / Nonneg: 2 / 2.0
            CadCommandSession session = Typed("(initget 4)", "(getreal \"Nonneg: \")", "-1", "2");

            Assert.Equal(
                new[]
                {
                    "Command: (getreal \"Nonneg: \")",
                    "Nonneg: -1",
                    "Value must be positive.",
                    "Nonneg: 2",
                    "2.0",
                },
                Lines(session).Skip(2)
            );
        }

        [Fact]
        public void InitgetBits1And2And4OnGetdist_RefuseZeroWithTheTwoWordsTogether()
        {
            // AutoCAD 2021 (q3): (initget 7 "Up") (getdist "Len: ") then 0 gives Value must be positive and nonzero.
            CadCommandSession session = Typed("(progn (initget 7 \"Up\") (getdist \"Len: \"))", "0");

            Assert.Equal(new[] { "Len: 0", "Value must be positive and nonzero." }, Lines(session).Skip(1));
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void InitgetBit128_GivesWhateverWasTypedAsAString()
        {
            // AutoCAD 2021 (q3, q4): (initget 128) then (getpoint "Arb: ") with hello gives "hello"; (getreal "R: ") with abc "abc"
            CadCommandSession point = Typed("(initget 128)", "(getpoint \"Arb: \")", "hello");
            CadCommandSession real = Typed("(initget 128)", "(getreal \"R: \")", "abc");

            Assert.Equal(new[] { "Arb: hello", "\"hello\"" }, Lines(point).Skip(3));
            Assert.Equal(new[] { "R: abc", "\"abc\"" }, Lines(real).Skip(3));
        }

        [Fact]
        public void InitgetKeywordsOnGetpointAndGetdist_AreAnswersToThemInAnyCase()
        {
            // AutoCAD 2021 (q4): (initget "Abc Def") with D and with def at (getpoint) gives "Def"; (initget 7 "Up") with U at
            // (getdist) gives "Up"
            CadCommandSession upper = Typed("(progn (initget \"Abc Def\") (getpoint \"Pt or [Abc/Def]: \"))", "D");
            CadCommandSession lower = Typed("(progn (initget \"Abc Def\") (getpoint \"Pt or [Abc/Def]: \"))", "def");
            CadCommandSession distance = Typed("(progn (initget 7 \"Up\") (getdist \"Len: \"))", "U");

            Assert.Equal("\"Def\"", Lines(upper).Last());
            Assert.Equal("\"Def\"", Lines(lower).Last());
            Assert.Equal("\"Up\"", Lines(distance).Last());
        }

        [Fact]
        public void GetdistWithKeywordsGivenText_RefusesWithTheMessageThatNamesTheKeywords()
        {
            // spec D44 (q3 showed this message with keywords in force): Requires numeric distance, two points, or option keyword.
            CadCommandSession session = Typed("(progn (initget \"Up\") (getdist \"Len: \"))", "abc");

            Assert.Equal(
                new[] { "Len: abc", "Requires numeric distance, two points, or option keyword." },
                Lines(session).Skip(1)
            );
        }

        [Fact]
        public void GetpointWithKeywordsGivenAWordThatIsNoKeyword_RefusesWithPointOrOptionKeywordRequired()
        {
            // spec D44: Point or option keyword required. with keywords in force (q3 gives it for initget 1 and for keywords; the
            // word is no keyword and no point)
            CadCommandSession session = Typed("(progn (initget \"Abc Def\") (getpoint \"P: \"))", "xyz");

            Assert.Equal(new[] { "P: xyz", "Point or option keyword required." }, Lines(session).Skip(1));
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void InitgetHoldsForTheNextGetFunctionOnly()
        {
            // AutoCAD 2021 (q4): (initget "Abc Def") then (getpoint "P1: ") (getpoint "P2: "): A is taken at P1:, D is refused at
            // P2: with Invalid point., and Esc ends it
            CadCommandSession session = Typed(
                "(progn (initget \"Abc Def\") (getpoint \"P1: \") (getpoint \"P2: \"))",
                "A",
                "D",
                Esc
            );

            Assert.Equal(
                new[] { "P1: A", "P2: D", "Invalid point.", "P2: *Cancel*", "; error: Function cancelled" },
                Lines(session).Skip(1)
            );
        }

        [Fact]
        public void InitgetOnALineOfItsOwn_IsSpentByTheNextGet_NotTheOneAfter()
        {
            // AutoCAD 2021 (q3, q4): after (initget "Yes No") (getkword ...) a later (getkword "K: ") has no keywords at all
            CadCommandSession session = Typed(
                "(initget \"Yes No\")",
                "(getkword \"Q: \")",
                "y",
                "(getkword \"K: \")",
                "yes"
            );

            Assert.Equal(
                new[] { "Command: (getkword \"K: \")", "K: yes", "Invalid option keyword." },
                Lines(session).Skip(5)
            );
        }

        [Fact]
        public void InitgetGivenARealOrTheArgumentsInEitherOrder_ErrorsOrTakesThem()
        {
            // AutoCAD 2021 (q7): (initget 1.5) is ; error: bad argument type: fixnump: 1.5; (initget "A" 1) gives nil
            CadCommandSession real = Typed("(initget 1.5)");
            CadCommandSession reversed = Typed("(initget \"A\" 1)");

            Assert.Equal(new[] { "Command: (initget 1.5)", "; error: bad argument type: fixnump: 1.5" }, Lines(real));
            Assert.Equal(new[] { "Command: (initget \"A\" 1)", "nil" }, Lines(reversed));
        }

        #endregion

        #region arguments

        [Fact]
        public void TheGetFunctions_NameTheArgumentThatIsTheWrongType()
        {
            // AutoCAD 2021 (q7)
            Assert.Equal("; error: bad argument type: point: 5", Lines(Typed("(getpoint 5)")).Last());
            Assert.Equal("; error: bad argument type: point: \"b\"", Lines(Typed("(getpoint \"a\" \"b\")")).Last());
            Assert.Equal("; error: bad argument type: stringp 5", Lines(Typed("(getint 5)")).Last());
            Assert.Equal("; error: bad argument type: stringp 5", Lines(Typed("(getkword 5)")).Last());
            Assert.False(Typed("(getpoint 5)").IsRunning);
        }

        #endregion

        #region entsel and nentsel

        [Fact]
        public void EntselGivenAPointOnALineEnd_GivesTheNameAndThePoint()
        {
            // AutoCAD 2021 (q5, q8): (entsel "Pick it: ") then 0,0 gives (<Entity name: 1e4094de0e0> (0.0 0.0 0.0))
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            Run(session, "(entsel \"Pick it: \")", "0,0");

            string[] lines = session.History.Skip(before).Select(l => l.TrimEnd()).ToArray();

            Assert.Equal("Command: (entsel \"Pick it: \")", lines[0]);
            Assert.Equal("Pick it: 0,0", lines[1]);
            Assert.Matches(@"^\(<Entity name: [0-9a-f]+> \(0\.0 0\.0 0\.0\)\)$", lines[2]);
            Assert.Equal(3, lines.Length);
        }

        [Fact]
        public void EntselGivenAPointOnNothing_GivesNilAtOnceWithNoSecondAsk()
        {
            // AutoCAD 2021 (q5): Pick it: 100,100 gives nil, the prompt not asked again
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            Run(session, "(entsel \"Pick it: \")", "100,100");

            Assert.Equal(
                new[] { "Command: (entsel \"Pick it: \")", "Pick it: 100,100", "nil" },
                session.History.Skip(before).Select(l => l.TrimEnd()).ToArray()
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void EntselWithNoMessage_AsksSelectObject()
        {
            // spec D47: the default prompt is "Select object: "
            CadCommandSession session = Typed("(entsel)");

            Assert.Equal("Select object: ", session.PromptText);
        }

        [Fact]
        public void EntselAnsweredWithEnter_GivesNil()
        {
            // AutoCAD 2021 (q8): (entsel "\nPick: ") then Enter gives nil
            CadCommandSession session = Typed("(entsel \"\\nPick: \")", Enter);

            Assert.Equal(new[] { "Command: (entsel \"\\nPick: \")", "Pick:", "nil" }, Written(session));
        }

        [Fact]
        public void EntselWithAKeywordOfInitget_GivesTheKeyword()
        {
            // AutoCAD 2021 (q8): (initget "Kw") (entsel "\nPick or [Kw]: ") then K gives "Kw"
            CadCommandSession session = Typed("(progn (initget \"Kw\") (entsel \"\\nPick or [Kw]: \"))", "K");

            Assert.Equal(new[] { "Pick or [Kw]: K", "\"Kw\"" }, Written(session).Skip(1));
        }

        [Fact]
        public void EntselGivenL_TakesTheLastObject()
        {
            // spec D47 (runs/dch, runs/sd: L at an entsel): a list of the name and a point; the point is not measured.
            // With no message the prompt is the default "Select object: ", so the echo is that prompt and the L (D43).
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            Run(session, "(entsel)", "L");

            string[] lines = session.History.Skip(before).Select(l => l.TrimEnd()).ToArray();

            Assert.Equal("Select object: L", lines[1]);
            Assert.Matches(@"^\(<Entity name: [0-9a-f]+> \(.*\)\)$", lines[2]);
        }

        [Fact]
        public void EntselGivenTextThatIsNeitherAPointNorL_RefusesAndAsksAgain()
        {
            // spec D47 (runs/dch, its first feed): *Invalid selection* then Expects a point or Last, and the prompt again
            CadCommandSession session = Typed("(entsel \"Pick: \")", "abc");

            Assert.Equal(
                new[] { "Command: (entsel \"Pick: \")", "Pick: abc", "*Invalid selection*", "Expects a point or Last" },
                Lines(session)
            );
            Assert.True(session.IsRunning);
            Assert.Equal("Pick: ", session.PromptText);
        }

        [Fact]
        public void EscAtAnEntsel_ThrowsFunctionCancelled()
        {
            // AutoCAD 2021 (q7): (setq e (entsel "\nPick: ")) then Esc gives Pick: *Cancel* / ; error: Function cancelled
            CadCommandSession session = Typed("(setq e (entsel \"\\nPick: \"))", Esc);

            Assert.Equal(
                new[] { "Command: (setq e (entsel \"\\nPick: \"))", "Pick: *Cancel*", "; error: Function cancelled" },
                Written(session)
            );
        }

        [Fact]
        public void NentselGivenAPointOnNothing_GivesNil()
        {
            // AutoCAD 2021 (q5): (nentsel "N: ") then 20,5 gives nil
            CadCommandSession session = Typed("(nentsel \"N: \")", "20,5");

            Assert.Equal(new[] { "Command: (nentsel \"N: \")", "N: 20,5", "nil" }, Lines(session));
        }

        [Fact]
        public void NentselGivenAPointOnALineEnd_GivesTheNameAndThePointAsEntselDoes()
        {
            // spec D47: for an object of a space the same as entsel (a reference; not measured)
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            Run(session, "(nentsel \"N: \")", "10,0");

            Assert.Matches(@"^\(<Entity name: [0-9a-f]+> \(10\.0 0\.0 0\.0\)\)$", session.History.Last().TrimEnd());
            Assert.Equal(3, session.History.Count - before);
        }

        #endregion

        #region a form at a command's prompt, and the session while a routine waits

        [Fact]
        public void AGetpointTypedAtThePromptOfLine_AsksItsOwnPrompt_ThenLineTakesTheValue()
        {
            // AutoCAD 2021 (q7): at LINE's Specify next point prompt (getpoint "X: ") asks X: 5,5 gives (5.0 5.0 0.0) and LINE
            // takes the point
            CadCommandSession session = Typed("LINE", "0,0", "(getpoint \"X: \")");

            Assert.Equal("X: ", session.PromptText);
            Assert.False(session.IsLispInput("(list 1 2)"));

            Run(session, "5,5");

            Assert.Equal(
                new[]
                {
                    "Command: LINE",
                    "Specify first point: 0,0",
                    "Specify next point or [Undo]: (getpoint \"X: \")",
                    "X: 5,5",
                    "(5.0 5.0 0.0)",
                },
                Lines(session)
            );
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);

            Run(session, Enter);

            DxfLine line = Assert.Single(session.Document.Database.ModelSpace.OfType<DxfLine>());

            Assert.Equal(5.0, line.EndPoint.X, 3);
            Assert.Equal(5.0, line.EndPoint.Y, 3);
        }

        [Fact]
        public void LoadLispWhileARoutineWaits_IsRefusedWithCantReenterLisp_AndTheRoutineStillWaits()
        {
            // spec D36: host calls while an evaluation waits are refused with Can't reenter LISP.; LoadLisp returns false
            string path = Path.Combine(_folder, "late.lsp");
            File.WriteAllText(path, "(setq late 1)\n", new UTF8Encoding(false));
            CadCommandSession session = Typed("(setq late 0)", "(getpoint \"P: \")");

            Assert.False(session.LoadLisp(path));

            Assert.Equal("Can't reenter LISP.", Lines(session).Last());
            Assert.True(session.IsRunning);
            Assert.Equal("P: ", session.PromptText);

            Run(session, "1,1", "!late");

            Assert.Equal("0", Lines(session).Last());
        }

        [Fact]
        public void TheGetPromptOfAForm_IsNotTheCommandThatEnterRepeats()
        {
            // spec D60: the prompt of a get function is a small internal command, not the one Enter repeats; after the c: GHW
            // and a typed form with a get of its own, Enter runs GHW again (L1: Enter repeats the last command)
            CadCommandSession session = Typed(
                "(defun c:ghw () (getint \"N: \") (princ))",
                "GHW",
                "4",
                "(getint \"M: \")",
                "5",
                Enter
            );

            Assert.Equal(
                new[] { "Command: GHW", "N: 4", "Command: (getint \"M: \")", "M: 5", "5", "Command: GHW" },
                Lines(session).Skip(2)
            );
            Assert.Equal("N: ", session.PromptText);
            Assert.Equal("GHW", session.LastCommandName);
        }

        #endregion

        #region LASTPOINT through a get function (D65)

        [Fact]
        public void GetpointWithABasePointAnswered_LeavesLastPointAsItWas()
        {
            // AutoCAD 2021 (q10): LASTPOINT (0.0 0.0 0.0) before; after (getpoint '(1 1) "B: ") answered 5,5 still (0.0 0.0 0.0)
            CadCommandSession session = Typed("(getpoint '(1 1) \"B: \")", "5,5");

            Assert.Equal("(5.0 5.0 0.0)", Lines(session).Last());
            Near(new GeoPoint2(0, 0), session.LastPoint);
        }

        [Fact]
        public void GetpointWithABasePointAnsweredByEnter_LeavesLastPointAsItWas()
        {
            // AutoCAD 2021 (q10): (getpoint '(7 7) "C: ") answered by Enter gives nil and LASTPOINT is still (0.0 0.0 0.0)
            CadCommandSession session = Typed("(getpoint '(7 7) \"C: \")", Enter);

            Assert.Equal("nil", Lines(session).Last());
            Near(new GeoPoint2(0, 0), session.LastPoint);
        }

        [Fact]
        public void GetpointWithABasePointCancelled_LeavesLastPointAsItWas()
        {
            // ours: D65, Esc leaves LASTPOINT as it was too
            CadCommandSession session = Typed("(getpoint '(7 7) \"C: \")", Esc);

            Near(new GeoPoint2(0, 0), session.LastPoint);
        }

        [Fact]
        public void ARelativePointAtAGetpointWithABasePoint_IsStillMeasuredFromTheBasePoint()
        {
            // AutoCAD 2021 (q3): @2,0 at (getpoint '(1 1)) is (3.0 1.0 0.0); the last point is not the base point afterwards
            CadCommandSession session = Typed("(getpoint '(1 1) \"Next: \")", "@2,0");

            Assert.Equal("(3.0 1.0 0.0)", Lines(session).Last());
            Near(new GeoPoint2(0, 0), session.LastPoint);
        }

        [Fact]
        public void TheLastPointOfAnEarlierCommand_IsTheOneThatRemainsAfterTheGet()
        {
            // ours: D65, the last point is the one before the get, not the base point or the answer
            CadCommandSession session = Typed("LINE", "3,4", Esc);
            Run(session, "(progn (getpoint '(1 1) \"B: \") (princ))", "5,5");

            Near(new GeoPoint2(3, 4), session.LastPoint);
        }

        #endregion
    }
}
