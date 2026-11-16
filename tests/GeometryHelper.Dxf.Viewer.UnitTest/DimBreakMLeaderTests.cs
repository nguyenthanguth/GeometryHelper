using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIMBREAK on a multileader against AutoCAD 2021's console run through standard input (16 November, some 150 breaks of
    /// leader lines): a multileader of words with a leader line from (0, 0) to (100, -1.25) at the style's break size 0.125
    /// and arrows 0.18. The break is kept in the leader line (90, 11 and 12) and named in a BREAKDATA of the multileader's
    /// extension dictionary, as for a dimension; the line is drawn from the back of its arrow (0.179986, -0.00225).
    /// </summary>
    public class DimBreakMLeaderTests
    {
        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        /// <summary>A multileader of words whose one leader line runs from (0, 0) to (100, -1.25), the break size 0.125 and the arrows 0.18, in a drawing of its own.</summary>
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

        private static void Line(CadDocument document, double x1, double y1, double x2, double y2) =>
            document.Database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(x1, y1, 0.0), new GeoPoint3(x2, y2, 0.0))
            );

        /// <summary>The breaks of a leader line as "x1,y1,x2,y2" each.</summary>
        private static List<string> Breaks(DxfMLeader mleader, int segment = 0) =>
            mleader
                .GetBreaks(mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes().Single()).Single())
                .Where(b => b.Segment == segment)
                .Select(b => string.Join(",", new[] { b.Start.X, b.Start.Y, b.End.X, b.End.Y }.Select(Rounded)))
                .ToList();

        private static string Rounded(double value) =>
            Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The 91 of each break point reference of the drawing's BREAKDATA, as written.</summary>
        private static List<string> References(CadDocument document) =>
            Regex
                .Matches(
                    document.Database.ToText().Replace("\r\n", "\n"),
                    @"AcDbBreakPointRef\n\s*71\n(\d+)\n\s*72\n\d+\n\s*91\n(-?\d+)\n"
                )
                .Cast<Match>()
                .Select(m => m.Groups[1].Value + ":" + m.Groups[2].Value)
                .ToList();

        [Fact]
        public void AutoBreaksTheLeaderLineWhereALineCrossesIt_AndNamesTheLineInTheBreakData()
        {
            // AutoCAD 2021: a line at x 40 across the leader line breaks it from (39.9375, -0.499219) to (40.0625, -0.500781), the
            // break size 0.125 along the line; the line is named in a BREAKDATA (71 of 0, 91 of -1) and names it among its reactors.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 40, -10, 40, 10);
            Line(document, 40, -10, 40, -5);
            string crossing = All<DxfLine>(document.ModelSpace)[0].Handle;

            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Assert.Equal(new[] { "39.9375,-0.4992,40.0625,-0.5008" }, Breaks(mleader));
            Assert.Equal(new[] { "0:-1" }, References(document));
            Assert.Contains(
                crossing,
                document.Database.ToText().Replace("\r\n", "\n").Split('\n').Select(s => s.Trim())
            );
            AssertReopens(document);
        }

        [Fact]
        public void AutoBreaksAtEveryCrossing_ThoseThatMeetMerged_InTheOrderAlongTheLine()
        {
            // AutoCAD 2021: a line at x 40 and a circle of 5 about (70, 0) give three breaks, the circle's two a break size
            // wide each, from the arrow on.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 40, -10, 40, 10);
            document.Database.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(70, 0, 0), new GeoVector3(0, 0, 1), 5)
            );

            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Assert.Equal(
                new[]
                {
                    "39.9375,-0.4992,40.0625,-0.5008",
                    "65.0041,-0.8126,65.1291,-0.8141",
                    "74.849,-0.9356,74.974,-0.9372",
                },
                Breaks(mleader)
            );
            Assert.Equal(new[] { "0:-1", "0:-1" }, References(document));
        }

        [Fact]
        public void AutoNamesALineThatCrossesOnlyTheDogleg_ButBreaksNothing()
        {
            // AutoCAD 2021: a line at x 100.2 crosses the dogleg (from (100, -1.25) to (100.36, -1.25)), not the leader line:
            // it is named, the line is whole. A line at x 0 through the arrow's tip is named and breaks nothing either.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 100.2, -10, 100.2, 10);
            Line(document, 0, -10, 0, 10);
            Line(document, 40, -10, 40, -5);

            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Assert.Empty(Breaks(mleader));
            Assert.Equal(new[] { "0:-1", "0:-1" }, References(document));
        }

        public static readonly IEnumerable<object[]> ManualRows = new[]
        {
            // The picks, the breaks left (x1, y1, x2, y2 of each), the references kept: AutoCAD 2021's.
            new object[] { "30,0;35,0", "29.9953,-0.3749,34.9945,-0.4374" },
            new object[] { "35,0;30,0", "29.9953,-0.3749,34.9945,-0.4374" },
            new object[] { "30,2;35,2", "29.9703,-0.3746,34.9695,-0.4371" },
            new object[] { "30,0;130,0", "29.9953,-0.3749,100,-1.25" },
            new object[] { "-30,0;35,0", "0.18,-0.0022,34.9945,-0.4374" },
            new object[] { "90,0;105,0", "89.9859,-1.1248,100,-1.25" },
            new object[] { "30,0;35,0;60,0;70,0", "29.9953,-0.3749,34.9945,-0.4374 59.9906,-0.7499,69.9891,-0.8749" },
            new object[] { "30,0;50,0;40,0;60,0", "29.9953,-0.3749,59.9906,-0.7499" },
            new object[] { "30,0;35,0;32,0;34,0", "29.9953,-0.3749,34.9945,-0.4374" },
            new object[] { "30,0;30,0", "" },
        };

        [Theory]
        [MemberData(nameof(ManualRows))]
        public void ManualBreaksBetweenTheFeetOfTwoPoints_AsAutoCad2021Does(string picks, string breaks)
        {
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            double[] n = picks.Split(';', ',').Select(Number).ToArray();

            for (int i = 0; i + 3 < n.Length; i += 4)
            {
                Run(session, "DIMBREAK", new[] { mleader.Handle }, "M", P(n[i], n[i + 1]), P(n[i + 2], n[i + 3]));
            }

            Assert.Equal(breaks.Length == 0 ? new string[0] : breaks.Split(' '), Breaks(mleader));
            Assert.Equal(n.Length / 4, References(document).Count);
            Assert.All(References(document), r => Assert.Equal("2:0", r));
            AssertReopens(document);
        }

        [Fact]
        public void ABrokenLeaderLine_IsDrawnInPieces_WhereExplodeGivesItWhole()
        {
            // AutoCAD 2021's Explode gives the line whole (a polyline from the back of the arrow, 0.179986, to the landing);
            // it is drawn cut at its breaks: here from the arrow to 39.9375 and from 40.0625 to the landing.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 40, -10, 40, 10);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");
            var broken = Assert.Single(All<DxfMLeader>(document.ModelSpace));

            Assert.Single(broken.Explode().OfType<DxfPolyline>());
            List<DxfPolyline> pieces = broken.ExplodeBroken().OfType<DxfPolyline>().ToList();
            Assert.Equal(2, pieces.Count);
            Assert.Equal(0.179986, pieces[0].GetPoint2dAt(0).X, 5);
            Assert.Equal(39.9375, pieces[0].GetPoint2dAt(1).X, 4);
            Assert.Equal(40.0625, pieces[1].GetPoint2dAt(0).X, 4);
            Assert.Equal(100.0, pieces[1].GetPoint2dAt(1).X, 4);
        }

        [Fact]
        public void RemoveTakesTheBreaksOutOfTheLeaderLine_AndTheBreakDataOutOfTheMultileader()
        {
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            Line(document, 40, -10, 40, 10);
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");
            Run(session, "DIMBREAK", new[] { mleader.Handle }, "M", P(60, 0), P(70, 0));
            Assert.Equal(2, Breaks(mleader).Count);

            Run(session, "DIMBREAK", new[] { mleader.Handle }, "R");

            Assert.Empty(Breaks(mleader));
            Assert.Empty(References(document));
            Assert.Null(mleader.ExtensionDictionary);
            Assert.True(document.Undo());
            Assert.Equal(2, Breaks(Entity<DxfMLeader>(document, mleader.Handle)).Count);
        }

        [Fact]
        public void TheBreakGapIsTheStylesTimesTheScale()
        {
            // AutoCAD 2021: at scale 2 the gap is 0.25, from 39.875 to 40.125 about a line at x 40.
            DxfMLeader mleader = Leader(out CadDocument document, out CadCommandSession session);
            mleader.Scale = 2.0;
            Line(document, 40, -10, 40, 10);

            Run(session, "DIMBREAK", new[] { mleader.Handle }, "A");

            Assert.Single(Breaks(mleader));
            double[] gap = Breaks(mleader)[0].Split(',').Select(Number).ToArray();
            Assert.Equal(39.875, gap[0], 3);
            Assert.Equal(40.125, gap[2], 3);
        }
    }
}
