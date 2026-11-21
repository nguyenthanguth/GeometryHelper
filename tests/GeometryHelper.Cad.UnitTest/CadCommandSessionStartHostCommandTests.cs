using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <see cref="CadCommandSession.Start(CadCommand)"/> (P3 D708): a host starts a command that is in no registry, as a
    /// script's question is, and it asks and ends as any command does without becoming the command Enter repeats.
    /// </summary>
    public sealed class CadCommandSessionStartHostCommandTests
    {
        /// <summary>A command that asks a point, then a distance, and keeps both answers; its body notes when it ends.</summary>
        private sealed class Asking
        {
            public GeoPoint2? Point;
            public double? Distance;
            public bool BodyEnded;

            public CadCommand Command => new CadCommand("HOSTASK", Body);

            private IEnumerable<CadPrompt> Body(CadCommandContext c)
            {
                try
                {
                    yield return CadPrompt.ForPoint("Specify host point");
                    Point = c.Input.Point;
                    yield return CadPrompt.ForDistance("Specify host distance");
                    Distance = c.Input.Number;
                }
                finally
                {
                    BodyEnded = true;
                }
            }
        }

        [Fact]
        public void AHostCommand_AsksItsPrompts_AndEndsLikeAnyCommand()
        {
            CadCommandSession session = Session(Blank());
            var asking = new Asking();

            bool started = session.Start(asking.Command);

            Assert.True(started);
            Assert.True(session.IsRunning);
            Assert.Equal("HOSTASK", session.Current.Name);
            Assert.Equal("Specify host point:", session.PromptText);
            Assert.True(session.Feed("3,4"));
            Assert.Equal("Specify host distance:", session.PromptText);
            Assert.True(session.Feed("12.5"));
            Assert.False(session.IsRunning);
            Near(P(3, 4), asking.Point.Value);
            Assert.Equal(12.5, asking.Distance.Value, 9);
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.True(asking.BodyEnded);
        }

        [Fact]
        public void AHostCommandStartedWhileACommandRuns_IsRefused_AndTheRunningOneKeepsAsking()
        {
            CadCommandSession session = Run(Session(Blank()), "CIRCLE");
            var asking = new Asking();

            bool started = session.Start(asking.Command);

            Assert.False(started);
            Assert.Equal("CIRCLE", session.Current.Name);
            Assert.False(asking.BodyEnded);
            Assert.Null(asking.Point);
        }

        [Fact]
        public void AHostCommand_LeavesTheLastCommandAsItWas_SoEnterRepeatsTheUsersCommand()
        {
            // The user drew a circle, then the host asked a question of its own. Enter at the Command prompt runs CIRCLE
            // again, not the host's command, and LastCommandName still says CIRCLE.
            CadCommandSession session = Run(Session(Blank()), "CIRCLE", "0,0", "5");
            var asking = new Asking();
            session.Start(asking.Command);
            Run(session, "1,1", "2");

            Assert.Equal("CIRCLE", session.LastCommandName);
            session.Enter();
            Assert.Equal("CIRCLE", session.Current.Name);
        }

        [Fact]
        public void AHostCommandOnAFreshSession_LeavesNoLastCommand()
        {
            CadCommandSession session = Session(Blank());
            var asking = new Asking();

            session.Start(asking.Command);
            Run(session, "1,1", "2");

            Assert.Null(session.LastCommandName);
            session.Enter();
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void EscAtAHostCommand_EndsItsBodyAtOnce_AsCancelled_WithCancelOnTheCommandLine()
        {
            CadCommandSession session = Session(Blank());
            var asking = new Asking();
            session.Start(asking.Command);
            Assert.False(asking.BodyEnded);

            session.Escape();

            Assert.False(session.IsRunning);
            Assert.True(asking.BodyEnded);
            Assert.Equal(CadCommandOutcome.Cancelled, session.LastOutcome);
            Assert.Null(asking.Distance);
            Assert.Equal("Specify host point: *Cancel*", session.History.Last());
            Assert.Null(session.LastCommandName);
        }

        [Fact]
        public void ACommandStartedByNameOverAHostCommand_EndsItFirstAsEscWould()
        {
            CadCommandSession session = Session(Blank());
            var asking = new Asking();
            session.Start(asking.Command);

            session.Start("CIRCLE");

            Assert.True(asking.BodyEnded);
            Assert.Equal("CIRCLE", session.Current.Name);
        }

        [Fact]
        public void AHostCommandThatDraws_IsOneUndoStepNamedAfterIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            var command = new CadCommand("HOSTDRAW", Drawing);

            session.Start(command);
            Run(session, "0,0", "5");

            Assert.Equal(1, document.EntityCount);
            Assert.Equal("HOSTDRAW", document.NextUndo.Name);
        }

        private static IEnumerable<CadPrompt> Drawing(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify centre");
            GeoPoint2 centre = c.Input.Point;
            yield return CadPrompt.ForDistance("Specify radius", centre);
            double radius = c.Input.Number;
            c.Apply(null, scope =>
                scope.Append(
                    c.Document.ModelSpace,
                    new GeometryHelper.Dxf.DxfCircle { Center = new GeoPoint3(centre.X, centre.Y, 0), Radius = radius }
                )
            );
        }

        [Fact]
        public void ANullCommand_Throws()
        {
            CadCommandSession session = Session(Blank());

            Assert.Throws<ArgumentNullException>(() => session.Start((CadCommand)null));
        }
    }
}
