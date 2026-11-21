using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The questions a script asks through the carrier (D706, D709): each <c>get_*</c> gives what the user typed or picked, a
    /// keyword as the prompt writes it, <c>None</c> for Enter and the default where there is one, and a prompt that was
    /// refused or asked at the wrong time says why. The user is the tests' owner (D704) giving one scripted answer each time
    /// the script waits.
    /// </summary>
    public sealed class PromptTests : PythonHostTestBase
    {
        public PromptTests(PythonHostFixture fixture)
            : base(fixture) { }

        [Fact]
        public void APointTyped_IsAGeoPoint2_AndTheQuestionAndTheAnswerAreOnTheCommandLine()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "p = cad.get_point('Where')", "print(p.X, p.Y)" }, Typed("10,20"));

            Assert.Equal(new[] { "10.0 20.0" }, Printed);
            Assert.Single(Owner.Asked);
            Assert.StartsWith("Where", Owner.Asked[0], StringComparison.Ordinal);
            Assert.Contains("Where: 10,20", session.History);
        }

        [Fact]
        public void APointPicked_IsTheSamePointAsTyped()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "p = cad.get_point('Where')", "print(p.X, p.Y)" }, Picked(5, 6));

            Assert.Equal(new[] { "5.0 6.0" }, Printed);
        }

        [Fact]
        public void EnterAtAPointWithNoDefault_IsNone()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(repr(cad.get_point('Where')))" }, EnterKey);

            Assert.Equal(new[] { "None" }, Printed);
        }

        [Fact]
        public void EnterAtAPointWhoseDefaultIsAKeyword_IsTheKeyword()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "print(repr(cad.get_point('Where', keywords=['Close'], default='Close')))" },
                EnterKey
            );

            Assert.Equal(new[] { "'Close'" }, Printed);
        }

        [Fact]
        public void AKeywordTypedByItsCapitalAtAPoint_IsTheKeywordAsThePromptWritesIt()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "print(repr(cad.get_point('Where', keywords=['Close', 'Undo'])))" },
                Typed("c")
            );

            Assert.Equal(new[] { "'Close'" }, Printed);
        }

        [Fact]
        public void ADistanceTyped_IsANumber()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(cad.get_distance('Radius'))" }, Typed("12.5"));

            Assert.Equal(new[] { "12.5" }, Printed);
        }

        [Fact]
        public void ADistancePickedFromABasePoint_IsMeasuredFromIt()
        {
            // The point (3, 4) is 5 from (0, 0).
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "from GeometryHelper.Geometry import GeoPoint2",
                    "print(cad.get_distance('Radius', GeoPoint2(0, 0)))",
                },
                Picked(3, 4)
            );

            Assert.Equal(new[] { "5.0" }, Printed);
        }

        [Fact]
        public void AnAngleTypedInDegrees_IsReturnedInRadians()
        {
            // 90 degrees is pi / 2 = 1.5707963267948966, shown to nine places.
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(round(cad.get_angle('Angle'), 9))" }, Typed("90"));

            Assert.Equal(new[] { "1.570796327" }, Printed);
        }

        [Fact]
        public void AnIntegerTyped_IsAnInt()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "n = cad.get_integer('How many')", "print(n, type(n).__name__)" },
                Typed("7")
            );

            Assert.Equal(new[] { "7 int" }, Printed);
        }

        [Fact]
        public void AnIntegerAnswerThatIsNotANumber_IsAskedAgain()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(cad.get_integer('How many'))" }, Typed("abc"), Typed("7"));

            Assert.Equal(new[] { "7" }, Printed);
            Assert.Equal(2, Owner.Asked.Count);
            Assert.Contains("Requires an integer value.", session.History);
        }

        [Fact]
        public void EnterAtAnIntegerWithADefault_IsTheDefault()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(cad.get_integer('How many', default=5))" }, EnterKey);

            Assert.Equal(new[] { "5" }, Printed);
        }

        [Fact]
        public void ARealTyped_IsAFloat()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "x = cad.get_real('Factor')", "print(x, type(x).__name__)" },
                Typed("2.5")
            );

            Assert.Equal(new[] { "2.5 float" }, Printed);
        }

        [Fact]
        public void ARealAnswerThatIsNotANumber_IsAskedAgain()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(cad.get_real('Factor'))" }, Typed("x"), Typed("2.5"));

            Assert.Equal(new[] { "2.5" }, Printed);
            Assert.Contains("Requires numeric value.", session.History);
        }

        [Fact]
        public void ARealIsReadInInvariantCulture_WhateverTheCultureOfTheUser()
        {
            // In vi-VN the decimal separator is the comma, so "2,5" would be 2.5 there; the module reads 2.5 as two and a half
            // in every culture, refuses 2,5 and asks again.
            CadCommandSession session = NewSession();

            InCulture("vi-VN", () =>
                RunLines(session, new[] { UsesCad, "print(cad.get_real('Factor'))" }, Typed("2,5"), Typed("2.5"))
            );

            Assert.Equal(new[] { "2.5" }, Printed);
            Assert.Contains("Requires numeric value.", session.History);
        }

        [Fact]
        public void AStringWithSpaces_IsTakenWhole()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(repr(cad.get_string('Name')))" }, Typed("two words"));

            Assert.Equal(new[] { "'two words'" }, Printed);
        }

        [Fact]
        public void EnterAtAString_IsNone_OrTheDefault()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "print(repr(cad.get_string('Name')))",
                    "print(repr(cad.get_string('Name', default='Anon')))",
                },
                EnterKey,
                EnterKey
            );

            Assert.Equal(new[] { "None", "'Anon'" }, Printed);
        }

        [Fact]
        public void AKeywordTypedByItsCapital_IsTheKeywordAsThePromptWritesIt()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(repr(cad.get_keyword('Mode', ['Yes', 'No'])))" }, Typed("n"));

            Assert.Equal(new[] { "'No'" }, Printed);
        }

        [Fact]
        public void EnterAtAKeywordPrompt_IsTheDefaultOrNone()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "print(repr(cad.get_keyword('Mode', ['Yes', 'No'], default='No')))",
                    "print(repr(cad.get_keyword('Mode', ['Yes', 'No'])))",
                },
                EnterKey,
                EnterKey
            );

            Assert.Equal(new[] { "'No'", "None" }, Printed);
        }

        [Fact]
        public void AnObjectPicked_IsItsHandle()
        {
            CadCommandSession session = NewSession();
            string handle = AddLine(session.Document, 0).Handle;

            RunLines(session, new[] { UsesCad, "print(repr(cad.get_entity('Pick a line')))" }, Objects(handle));

            Assert.Equal(new[] { "'" + handle + "'" }, Printed);
        }

        [Fact]
        public void ASelection_IsTheHandlesPickedUntilEnter()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            string second = AddLine(session.Document, 50).Handle;
            string[] sorted = new[] { first, second }.OrderBy(h => h, StringComparer.Ordinal).ToArray();

            RunLines(
                session,
                new[] { UsesCad, "print(sorted(cad.get_selection()))" },
                Objects(first, second),
                EnterKey
            );

            Assert.Equal(new[] { "['" + sorted[0] + "', '" + sorted[1] + "']" }, Printed);
        }

        [Fact]
        public void EnterWithNothingSelected_IsAnEmptyList()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(cad.get_selection())" }, EnterKey);

            Assert.Equal(new[] { "[]" }, Printed);
        }

        [Fact]
        public void Input_IsAskedAsAStringPrompt_AndEnterIsTheEmptyString()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "print(repr(input('Name? ')))", "print(repr(input('Again? ')))" },
                Typed("Thang"),
                EnterKey
            );

            Assert.Equal(new[] { "'Thang'", "''" }, Printed);
            Assert.Equal(2, Owner.Asked.Count);
            Assert.StartsWith("Name?", Owner.Asked[0], StringComparison.Ordinal);
        }

        [Fact]
        public void EscAtInput_RaisesCancelled()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { "input('Name? ')", "print('not reached')" }, EscKey);

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Empty(Printed);
        }

        [Fact]
        public void ACarrier_LeavesTheLastCommandAsItWas_SoEnterRepeatsTheUsersCommand()
        {
            // The user drew a circle (CIRCLE is the last command). A script's question is not a command of the user's: after
            // it, LastCommandName is still CIRCLE and Enter at the Command prompt starts CIRCLE.
            CadCommandSession session = NewSession();
            session.Feed("CIRCLE");
            session.Feed("0,0");
            session.Feed("5");
            Assert.Equal("CIRCLE", session.LastCommandName);

            RunLines(session, new[] { UsesCad, "cad.get_point('Where')" }, Typed("1,1"));

            Assert.Equal("CIRCLE", session.LastCommandName);
            Assert.False(session.IsRunning);
            session.Enter();
            Assert.Equal("CIRCLE", session.Current.Name);
        }

        [Fact]
        public void ARunAskedWhileACommandWaits_IsRefused_AndTheCommandGoesOn()
        {
            // The user left CIRCLE asking for its centre: a run starts only at the Command prompt (D729), so the script never
            // runs, the refusal is written, and CIRCLE still asks.
            CadCommandSession session = NewSession();
            session.Feed("CIRCLE");

            CadPythonRunEventArgs run = RunLines(session, new[] { UsesCad, "print('ran')" });

            Assert.Equal(CadPythonOutcome.Refused, run.Outcome);
            Assert.Empty(Printed);
            Assert.Contains("A command is running: finish it or press Esc first.", session.History);
            Assert.Equal("CIRCLE", session.Current.Name);
            session.Escape();
        }

        [Fact]
        public void EscAtAPrompt_RaisesCancelled_AndEveryLaterPromptRaisesAtOnceWithoutAsking()
        {
            // The script catches the first Cancelled and asks again: LISP's Abort, a script that catches the first cannot keep
            // asking. The owner is given one answer (Esc) and fails the test if a second question is asked.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "try:",
                    "    cad.get_point('First')",
                    "except cad.Cancelled:",
                    "    print('cancelled first')",
                    "    try:",
                    "        cad.get_point('Second')",
                    "    except cad.Cancelled:",
                    "        print('cancelled second')",
                    "    raise",
                },
                EscKey
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Equal(new[] { "cancelled first", "cancelled second" }, Printed);
            Assert.Single(Owner.Asked);
            Assert.Contains(session.History, line => line.EndsWith("*Cancel*", StringComparison.Ordinal));
        }

        [Fact]
        public void EscAtAPrompt_IsNotSwallowedByAnExceptExceptionInTheScript()
        {
            // Cancelled is a KeyboardInterrupt, not an Exception.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { UsesCad, "try:", "    cad.get_point('Where')", "except Exception:", "    print('swallowed')" },
                EscKey
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Empty(Printed);
        }

        [Fact]
        public void CancelledIsAKeyboardInterrupt()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(issubclass(cad.Cancelled, KeyboardInterrupt))" });

            Assert.Equal(new[] { "True" }, Printed);
        }

        [Fact]
        public void EscAtAnIntegerPrompt_EndsTheRun_AndDoesNotAskAgain()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { UsesCad, "cad.get_integer('How many')" }, EscKey);

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Single(Owner.Asked);
        }

        [Fact]
        public void ACancelledRun_TakesBackWhatItDrew()
        {
            // A circle by command() and a line by the object model, then Esc at a question: the drawing is as before the run.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    AddLineInPython,
                    "add_line(cad.document(), 0)",
                    "cad.command('CIRCLE', (0, 0), 5)",
                    "cad.get_point('Where')",
                },
                EscKey
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.IsInGroup);
            Assert.False(session.Document.CanUndo);
        }

        [Fact]
        public void AfterACancelledRun_TheNextRunAsksAgain()
        {
            // The rule that later prompts raise at once lasts for the run that was cancelled and no longer.
            CadCommandSession session = NewSession();
            RunLines(session, new[] { UsesCad, "cad.get_point('First')" }, EscKey);

            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { UsesCad, "print(repr(cad.get_point('Again')))" },
                EnterKey
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "None" }, Printed);
        }
    }
}
