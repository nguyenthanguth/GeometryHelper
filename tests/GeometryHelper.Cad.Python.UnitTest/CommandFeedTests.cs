using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// <c>command(...)</c> (D715): a presenter command run to its end on the session of the run, each answer fed in order as
    /// the user would give it.
    /// </summary>
    public sealed class CommandFeedTests : PythonHostTestBase
    {
        public CommandFeedTests(PythonHostFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ACircleByTuples_IsDrawnAtTheCentreWithTheRadius()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { UsesCad, "cad.command('CIRCLE', (10, 20), 50)" });

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document));
            Assert.Equal(50, circle.Radius, 6);
            Assert.Equal(10, circle.Center.X, 6);
            Assert.Equal(20, circle.Center.Y, 6);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AGeoPoint2_IsAPickedPoint_AndAFloatIsTypedInInvariantCulture()
        {
            // In vi-VN 2.5 would be typed as "2,5" if the host used the culture of the user, and the radius would be refused.
            CadCommandSession session = NewSession();

            InCulture("vi-VN", () =>
                RunLines(
                    session,
                    new[]
                    {
                        UsesCad,
                        "from GeometryHelper.Geometry import GeoPoint2",
                        "cad.command('CIRCLE', GeoPoint2(10, 20), 2.5)",
                    }
                )
            );

            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document));
            Assert.Equal(2.5, circle.Radius, 6);
            Assert.Equal(10, circle.Center.X, 6);
        }

        [Fact]
        public void ALineWithAnEmptyStringAsItsLastAnswer_EndsWithEnter()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "cad.command('_LINE', (0, 0), (100, 0), '')" });

            DxfLine line = Assert.Single(All<DxfLine>(session.Document));
            Assert.Equal(0, line.StartPoint.X, 6);
            Assert.Equal(100, line.EndPoint.X, 6);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AListOfHandles_AnswersSelectObjects_AndThenEnter()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            string second = AddLine(session.Document, 50).Handle;
            AddLine(session.Document, 100);

            RunLines(session, new[] { UsesCad, "cad.command('ERASE', ['" + first + "', '" + second + "'])" });

            Assert.Equal(1, session.Document.EntityCount);
            Assert.Null(session.Document.FindEntity(first));
            Assert.Null(session.Document.FindEntity(second));
        }

        [Fact]
        public void ACommandThatStillAsksAfterTheLastAnswer_IsCancelled_AndRaisesARuntimeErrorWithTheQuestion()
        {
            // CIRCLE given only its centre still asks for the radius.
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "try:",
                    "    cad.command('CIRCLE', (0, 0))",
                    "except RuntimeError as e:",
                    "    print(e)",
                }
            );

            string message = Assert.Single(Printed);
            Assert.StartsWith("CIRCLE still asked 'Specify radius of circle", message, StringComparison.Ordinal);
            Assert.EndsWith("' after the last answer.", message, StringComparison.Ordinal);
            Assert.False(session.IsRunning);
            Assert.Equal(0, session.Document.EntityCount);
        }

        [Fact]
        public void AnAnswerTheCommandRefuses_RaisesAValueErrorNamingIt_AndCancelsTheCommand()
        {
            // "abc" is no radius: the session's Feed says so by returning false.
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "try:",
                    "    cad.command('CIRCLE', (0, 0), 'abc')",
                    "except ValueError as e:",
                    "    print(e)",
                }
            );

            string message = Assert.Single(Printed);
            Assert.StartsWith("CIRCLE did not take ", message, StringComparison.Ordinal);
            Assert.Contains("abc", message);
            Assert.Contains("Specify radius of circle", message);
            Assert.False(session.IsRunning);
            Assert.Equal(0, session.Document.EntityCount);
        }
    }
}
