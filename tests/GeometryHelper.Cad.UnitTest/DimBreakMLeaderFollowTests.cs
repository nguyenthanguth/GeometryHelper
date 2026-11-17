using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The breaks of a multileader follow the objects they were made at, and DIMBREAK Multiple takes multileaders too, against
    /// AutoCAD 2021's console run through standard input (16 November): a multileader of words whose one leader line runs from
    /// (0, 0) to (100, -1.25), a line at x 40 across it, broken by Auto, then the line or the multileader moved, turned,
    /// scaled or erased. AutoCAD makes the breaks again from its BREAKDATA after each, at the objects named there; a break by
    /// hand is kept as the points were picked and its feet on the leader line are taken again where it is moved.
    /// </summary>
    public class DimBreakMLeaderFollowTests
    {
        private static DxfMLeader Leader(out CadDocument document, out CadCommandSession session)
        {
            document = BlankWithMLeaderStyle();
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            style.BreakSize = 0.125;
            style.ArrowSize = 0.18;
            session = Session(document);
            Run(session, "MLD", P(0, 0), P(100, -1.25), "ABC");
            return Assert.Single(All<DxfMLeader>(document.ModelSpace));
        }

        private static string Line(CadDocument document, double x1, double y1, double x2, double y2)
        {
            var line = new DxfLine(new GeoPoint3(x1, y1, 0.0), new GeoPoint3(x2, y2, 0.0));
            document.Database.ModelSpace.AppendEntity(line);
            return line.Handle;
        }

        private static List<string> Breaks(DxfMLeader mleader) =>
            mleader
                .GetBreaks(mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes().Single()).Single())
                .Select(b =>
                    string.Join(
                        ",",
                        new[] { b.Start.X, b.Start.Y, b.End.X, b.End.Y }.Select(v =>
                            Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture)
                        )
                    )
                )
                .ToList();

        private static int References(CadDocument document) =>
            Regex.Matches(document.Database.ToText(), "AcDbBreakPointRef").Count;

        [Fact]
        public void ALineMovedTenToTheRight_TakesTheBreakWithIt()
        {
            // AutoCAD 2021: the break at x 40 goes to x 50, from (49.9375, -0.6242) to (50.0625, -0.6258).
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            string line = Line(document, 40, -10, 40, 10);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");
            Assert.Equal(new[] { "39.9375,-0.4992,40.0625,-0.5008" }, Breaks(mleader));

            Run(session, "MOVE", new[] { line }, Enter, P(0, 0), P(10, 0));

            Assert.Equal(
                new[] { "49.9375,-0.6242,50.0625,-0.6258" },
                Breaks(Entity<DxfMLeader>(document, mleader.Handle))
            );
            Assert.Equal(1, References(document));
            Assert.True(document.Undo());
            Assert.Equal(
                new[] { "39.9375,-0.4992,40.0625,-0.5008" },
                Breaks(Entity<DxfMLeader>(document, mleader.Handle))
            );
        }

        [Fact]
        public void ALineMovedOff_OrErased_LeavesTheLeaderLineWhole_TheBreakDataKeepingTheOneAndLosingTheOther()
        {
            // AutoCAD 2021: the line moved 100 up crosses nothing, and the BREAKDATA still names it; erased, it is gone from
            // the BREAKDATA, which goes with it.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            string line = Line(document, 40, -10, 40, 10);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Run(session, "MOVE", new[] { line }, Enter, P(0, 0), P(0, 100));

            Assert.Empty(Breaks(Entity<DxfMLeader>(document, mleader.Handle)));
            Assert.Equal(1, References(document));

            Run(session, "ERASE", new[] { line }, Enter);

            Assert.Equal(0, References(document));
            Assert.Null(Entity<DxfMLeader>(document, mleader.Handle).ExtensionDictionary);
        }

        [Fact]
        public void TheMultileaderMoved_IsBrokenAgainAtTheLineWhereItCrossesNow()
        {
            // AutoCAD 2021: moved 10 up, from (39.9375, 9.50078) to (40.0625, 9.49922); moved 80 up, off the line, whole.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 40, -10, 40, 60);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Run(session, "MOVE", new[] { mleader.Handle }, Enter, P(0, 0), P(0, 10));

            Assert.Equal(
                new[] { "39.9375,9.5008,40.0625,9.4992" },
                Breaks(Entity<DxfMLeader>(document, mleader.Handle))
            );

            Run(session, "MOVE", new[] { mleader.Handle }, Enter, P(0, 0), P(0, 80));

            Assert.Empty(Breaks(Entity<DxfMLeader>(document, mleader.Handle)));
        }

        [Fact]
        public void ABreakByHand_StaysAtTheFeetOfItsPointsWhereTheMultileaderIsMoved()
        {
            // AutoCAD 2021 with no view to draw in: a break from (30, 0) to (35, 0), the multileader moved 10 up, is kept as
            // picked and runs from the foot of the first on the moved leader line to the foot of the second: from (30.1203, 9.6235)
            // to (35.1195, 9.561), where moved by hand it was from (29.9953, -0.3749) to (34.9945, -0.4374).
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "M", P(30, 0), P(35, 0));
            Assert.Equal(new[] { "29.9953,-0.3749,34.9945,-0.4374" }, Breaks(mleader));

            Run(session, "MOVE", new[] { mleader.Handle }, Enter, P(0, 0), P(0, 10));

            Assert.Equal(
                new[] { "30.1203,9.6235,35.1195,9.561" },
                Breaks(Entity<DxfMLeader>(document, mleader.Handle))
            );
        }

        [Fact]
        public void Multiple_TakesDimensionsAndMultileadersBoth_AsAutoCad2021Does()
        {
            // AutoCAD 2021: a level dimension, a multileader and a line across both, Multiple and Auto: "2 objects modified",
            // the dimension line broken about x 40 and the leader line too; Remove makes both whole. The second prompt is
            // "Select object to break dimensions or [Auto/Remove] <Auto>:".
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Run(session, "DLI", P(0, 100), P(100, 100), P(50, 120));
            string dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            Line(document, 40, -10, 40, 130);

            Run(session, "DIMBREAK", "M", new[] { dimension, mleader.Handle }, Enter);
            Assert.Equal("Select object to break dimensions or [Auto/Remove] <Auto>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal("2 objects modified", session.History.Last());
            Assert.Single(Breaks(Entity<DxfMLeader>(document, mleader.Handle)));
            Assert.Equal(
                2,
                Entity<DxfRotatedDimension>(document, dimension)
                    .DimBlock.OfType<DxfLine>()
                    .Count(l =>
                        !l.IsErased && Math.Abs(l.StartPoint.Y - 120) < 1E-6 && Math.Abs(l.EndPoint.Y - 120) < 1E-6
                    )
            );

            Run(session, "DIMBREAK", "M", new[] { dimension, mleader.Handle }, Enter, "R");

            Assert.Empty(Breaks(Entity<DxfMLeader>(document, mleader.Handle)));
            Assert.Equal(0, References(document));
        }
    }
}
