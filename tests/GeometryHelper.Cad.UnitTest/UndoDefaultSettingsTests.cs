using System;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// UNDO's settings of a drawing nobody has changed them in: AutoCAD 2021, in its window and in its console, writes
    /// <c>Current settings: Auto = On, Control = All, Combine = No, Layer = No</c> and gives UNDOCTL 13 inside the group a routine runs in (enabled, auto-grouped,
    /// a group active, and neither bit 16 for Combine nor 32 for Layer) on the machine measured (L8 probe m/routines, M6).
    /// </summary>
    public sealed class UndoDefaultSettingsTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        [Fact]
        public void UndoOfAFreshDrawing_SaysCombineNoAndLayerNo()
        {
            CadCommandSession session = Session(Blank());
            int before = session.History.Count;

            Run(session, "UNDO");

            Assert.Contains(
                "Current settings: Auto = On, Control = All, Combine = No, Layer = No",
                System.Linq.Enumerable.Skip(session.History, before)
            );
        }

        [Fact]
        public void UndoctlOfAFreshDrawing_Is5_AndInsideAGroup13()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("5", LispEntityTests.Value(session, "(getvar \"UNDOCTL\")"));
            Run(session, "UNDO", "BE");
            Assert.Equal("13", LispEntityTests.Value(session, "(getvar \"UNDOCTL\")"));
        }
    }
}
