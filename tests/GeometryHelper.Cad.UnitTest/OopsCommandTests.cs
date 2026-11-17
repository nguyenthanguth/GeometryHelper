using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// OOPS as AutoCAD 2021 runs it (probe draw1, accoreconsole through standard input): the objects of the last ERASE, BLOCK
    /// or WBLOCK come back with no prompt and no message; only the last set; none twice; none where the erase was undone.
    /// </summary>
    public class OopsCommandTests
    {
        private static int Count(CadDocument document) => document.ModelSpace.Block.Count();

        [Fact]
        public void Oops_BringsBackTheObjectsErased_WithNoPromptAndNoMessage()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "ERASE", new[] { "A1", "A3" }, Enter);
            Assert.Equal(before - 2, Count(document));
            int written = session.History.Count;

            Run(session, "OOPS");

            Assert.False(session.IsRunning);
            Assert.Equal(before, Count(document));
            Assert.DoesNotContain(session.History.Skip(written), line => !line.Contains("OOPS"));
            DxfLine line = All<DxfLine>(document.ModelSpace)
                .Single(l => l.StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Tight) && l.EndPoint.X == 100.0);
            Assert.Equal("Steel", line.Layer);
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(200, 0, 0), circle.Center);
            Assert.Equal(10.0, circle.Radius, 9);
            AssertReopens(document);
        }

        [Fact]
        public void Oops_BringsBackOnlyTheLastSet_AndASecondOopsDoesNothing()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "ERASE", new[] { "A1" }, Enter);
            Run(session, "ERASE", new[] { "A3" }, Enter);
            Run(session, "OOPS");

            Assert.Equal(before - 1, Count(document));
            Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Single(All<DxfLine>(document.ModelSpace), l => l.Layer == "Locked");
            Assert.DoesNotContain(All<DxfLine>(document.ModelSpace), l => l.Layer == "Steel");

            Run(session, "OOPS");
            Assert.Equal(before - 1, Count(document));
        }

        [Fact]
        public void Oops_AfterDrawingSomethingMore_StillBringsTheLastErasedSetBack()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "ERASE", new[] { "A3" }, Enter);
            Run(session, "LINE", P(0, 300), P(10, 300), Enter);
            Run(session, "OOPS");

            Assert.Equal(before + 1, Count(document));
            Assert.Single(All<DxfCircle>(document.ModelSpace));
        }

        [Fact]
        public void Oops_AfterTheEraseWasUndone_PutsNothingBackTwice()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "ERASE", new[] { "A3" }, Enter);
            Assert.True(document.Undo());
            Assert.Equal(before, Count(document));

            Run(session, "OOPS");

            Assert.Equal(before, Count(document));
        }

        [Fact]
        public void Oops_IsOneUndoStep()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "ERASE", new[] { "A1", "A3" }, Enter);
            Run(session, "OOPS");
            Assert.Equal(before, Count(document));

            Assert.True(document.Undo());
            Assert.Equal(before - 2, Count(document));
        }

        [Fact]
        public void Oops_WithNothingErased_DoesNothing()
        {
            CadDocument document = Plan();
            int before = Count(document);
            CadCommandSession session = Session(document);

            Run(session, "OOPS");

            Assert.False(session.IsRunning);
            Assert.Equal(before, Count(document));
        }

        [Fact]
        public void Oops_AfterBlock_BringsBackTheObjectsTheBlockTookIn_AndTheBlockStays()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(10, 0), Enter);
            Run(session, "LINE", P(0, 10), P(10, 10), Enter);
            string[] both = All<DxfLine>(document.ModelSpace).Select(l => l.Handle).ToArray();

            Run(session, "-BLOCK", "B1", P(0, 0), both, Enter);
            Assert.Empty(document.ModelSpace.Block);

            Run(session, "OOPS");

            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);
            Assert.True(document.Database.BlockTable.Has("B1"));
        }

        [Fact]
        public void Oops_AfterWblockOfObjects_BringsBackWhatTheFileTookFromTheDrawing()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-oops-" + Guid.NewGuid().ToString("N"));

            try
            {
                CadDocument document = Blank();
                CadCommandSession session = Session(document);
                Run(session, "LINE", P(0, 0), P(10, 0), Enter);
                string handle = All<DxfLine>(document.ModelSpace).Single().Handle;

                Run(session, "-W", Path.Combine(folder, "oops"), Enter, P(0, 0), new[] { handle }, Enter);
                Assert.Empty(document.ModelSpace.Block);

                Run(session, "OOPS");

                Assert.Single(All<DxfLine>(document.ModelSpace));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void Oops_DoesNotBringBackThePolylineWipeoutErased()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "RECTANG", P(10, 10), P(40, 30));
            string handle = All<DxfPolyline>(document.ModelSpace).Single().Handle;
            Run(session, "WIPEOUT", "P", new[] { handle }, "Y");

            Run(session, "OOPS");

            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Single(All<DxfWipeout>(document.ModelSpace));
        }
    }
}
