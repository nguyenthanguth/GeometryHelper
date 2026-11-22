using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>ssget</c> with the <c>:L</c> mode, which leaves out the objects on a locked layer, as AutoCAD 2021's accoreconsole took it
    /// (L2 parked probes pb and pb2). The drawing is the probes': a LINE from (0,0) to (10,0) on the locked layer GHLK, a LINE from
    /// (0,5) to (10,5) and a CIRCLE at (20,0) of radius 3, both on layer 0.
    /// </summary>
    public sealed class LispSsgetLockedTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Probe()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "GHLK", IsLocked = true });
            document.ApplyEdit("T", null, s =>
            {
                s.Append(
                    document.ModelSpace,
                    new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "GHLK" }
                );
                s.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
                s.Append(document.ModelSpace, new DxfCircle(new GeoPoint3(20, 0, 0), new GeoVector3(0, 0, 1), 3));
            });
            return Session(document);
        }

        // The lines the session wrote after the form typed, the form's own echo left out.
        private static List<string> Lines(CadCommandSession session, int before) =>
            session.History.Skip(before + 1).Select(l => l.TrimEnd()).ToList();

        private static List<string> Ssget(CadCommandSession session, string form, params object[] answers)
        {
            int before = session.History.Count;
            session.Feed(form);
            Run(session, answers);
            return Lines(session, before);
        }

        [Fact]
        public void AllWithOneObjectOnALockedLayer_SaysHowManyWereFound_ThenThatOneWasLocked_AndKeepsTheRest()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): Select objects: ALL / 3 found / 1 was on a locked layer. / Select objects: ;
            // (sslength s) 2
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\"))", "ALL", Enter);

            Assert.Equal(
                new[] { "Select objects: ALL", "3 found", "1 was on a locked layer.", "Select objects:" },
                lines.Take(4)
            );
            Assert.Equal("2", Value(session, "(sslength s)"));
        }

        [Fact]
        public void APickOnALockedObject_SaysOneFoundAndOneLocked_AndGivesNilWhenNothingElseIsPicked()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): Select objects: 0,0 / 1 found / 1 was on a locked layer. / Select objects: / nil
            List<string> lines = Ssget(session, "(setq s (ssget \":L\"))", "0,0", Enter);

            Assert.Equal(
                new[] { "Select objects: 0,0", "1 found", "1 was on a locked layer.", "Select objects:", "nil" },
                lines.Take(5)
            );
            Assert.Equal("nil", Value(session, "!s"));
        }

        [Fact]
        public void AnObjectPickedAfterALockedOne_IsCountedWithoutTheLockedOne()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): 0,0 gives 1 found, 1 was on a locked layer.; then 0,5 gives "1 found, 1 total"
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\"))", "0,0", "0,5", Enter);

            Assert.Equal(
                new[]
                {
                    "Select objects: 0,0",
                    "1 found",
                    "1 was on a locked layer.",
                    "Select objects: 0,5",
                    "1 found, 1 total",
                    "Select objects:",
                },
                lines.Take(6)
            );
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void AFilter_CountsOnlyTheObjectsItKeeps_TheLockedOnesAmongThem()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): (ssget "_:L" '((0 . "LINE"))) with ALL: 2 found, 1 was on a locked layer.; (sslength s) 1
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\" '((0 . \"LINE\"))))", "ALL", Enter);

            Assert.Equal(
                new[] { "Select objects: ALL", "2 found", "1 was on a locked layer.", "Select objects:" },
                lines.Take(4)
            );
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void AFilterThatKeepsOnlyTheLockedObject_GivesNil()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb2): (ssget "_:L" '((8 . "GHLK"))) with ALL: 1 found, 1 was on a locked layer., nil
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\" '((8 . \"GHLK\"))))", "ALL", Enter);

            Assert.Equal(
                new[] { "Select objects: ALL", "1 found", "1 was on a locked layer.", "Select objects:", "nil" },
                lines.Take(5)
            );
        }

        [Fact]
        public void AWindow_SaysTheLockedObjectsOfIt()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): W -5,-5 25,10 gives 2 found, 1 was on a locked layer.; (sslength s) 1. The window
            // here is -5,-5 15,10, round the two lines: the console's own view took two of the three objects for the larger one.
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\"))", "W", "-5,-5", "15,10", Enter);

            Assert.Contains("2 found", lines);
            Assert.Equal(lines.IndexOf("2 found") + 1, lines.IndexOf("1 was on a locked layer."));
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void Previous_IsFilteredTheSame()
        {
            CadCommandSession session = Probe();
            Value(session, "(ssget \"_X\")");

            // AutoCAD 2021 (console, l2p pb): P after an ssget of the three gives 3 found, 1 was on a locked layer.; (sslength s) 2
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\"))", "P", Enter);

            Assert.Equal(
                new[] { "Select objects: P", "3 found", "1 was on a locked layer.", "Select objects:" },
                lines.Take(4)
            );
            Assert.Equal("2", Value(session, "(sslength s)"));
        }

        [Fact]
        public void EveryObjectOnALockedLayer_IsThreeWere()
        {
            CadCommandSession session = Probe();
            Run(session, "-LAYER", "LOck", "0", Enter);

            // AutoCAD 2021 (console, l2p pb): with all three locked, ALL gives 3 found, 3 were on a locked layer., nil
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L\"))", "ALL", Enter);

            Assert.Equal(
                new[] { "Select objects: ALL", "3 found", "3 were on a locked layer.", "Select objects:", "nil" },
                lines.Take(5)
            );
        }

        [Fact]
        public void SingleWithLocked_WritesNoFoundLine_AndTheLockedMessageComesAlone()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): "_:S:L" with ALL: Select objects: ALL / 1 was on a locked layer. / <Selection set>;
            // (sslength s) 2; the selection ends there, no further Select objects:
            List<string> lines = Ssget(session, "(setq s (ssget \"_:S:L\"))", "ALL");

            Assert.Equal(new[] { "Select objects: ALL", "1 was on a locked layer." }, lines.Take(2));
            Assert.False(session.IsRunning);
            Assert.Equal("2", Value(session, "(sslength s)"));
        }

        [Fact]
        public void SingleWithLocked_ALockedPickEndsItWithNil()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): "_:L:S" and "_:S:L" on the locked line: 1 was on a locked layer., then nil, and the
            // pick ended the selection (the next 0,5 was typed to the Command: line)
            foreach (string mode in new[] { "_:L:S", "_:S:L" })
            {
                List<string> lines = Ssget(session, "(setq s (ssget \"" + mode + "\"))", "0,0");

                Assert.Equal(new[] { "Select objects: 0,0", "1 was on a locked layer.", "nil" }, lines.Take(3));
                Assert.False(session.IsRunning);
            }
        }

        [Fact]
        public void SingleWithLocked_APickOnAnUnlockedObject_WritesNoFoundLine()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb2): "_:L:S" with 0,5: Select objects: 0,5 / <Selection set>; (sslength s1) 1
            List<string> lines = Ssget(session, "(setq s (ssget \"_:L:S\"))", "0,5");

            Assert.DoesNotContain("1 found", lines);
            Assert.False(session.IsRunning);
            Assert.Equal("1", Value(session, "(sslength s)"));
        }

        [Fact]
        public void ASetWithoutLocked_KeepsTheLockedObjects()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): (ssget) with ALL gives 3 found and the set of 3: no :L, no filtering
            List<string> lines = Ssget(session, "(setq s (ssget))", "ALL", Enter);

            Assert.Equal(new[] { "Select objects: ALL", "3 found", "Select objects:" }, lines.Take(3));
            Assert.Equal("3", Value(session, "(sslength s)"));
        }

        [Fact]
        public void TheModeXWithL_IsABadModeString()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): (ssget "_X:L") is "; error: bad ssget mode string"
            Assert.Equal("; error: bad ssget mode string", Value(session, "(ssget \"_X:L\")"));
        }

        [Fact]
        public void LWithAStringForItsFilter_IsABadPointArgument()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (console, l2p pb): (ssget "_:L" "x") is "; error: bad point argument"
            Assert.Equal("; error: bad point argument", Value(session, "(ssget \"_:L\" \"x\")"));
        }
    }
}
