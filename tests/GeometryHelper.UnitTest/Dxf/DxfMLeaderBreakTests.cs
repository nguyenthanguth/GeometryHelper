using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The breaks DIMBREAK leaves in the leader lines of a MULTILEADER, as AutoCAD 2021 writes them (probed in its console, a
    /// leader line from (0, 0) to (100, -1.25) crossed by a line at x 40 has 90 of 0, 11 of (39.9375, -0.499219) and 12 of
    /// (40.0625, -0.500781) between its vertex and its 91): read, written, and drawn, where AutoCAD's own Explode gives the
    /// line whole.
    /// </summary>
    public class DxfMLeaderBreakTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        /// <summary>A multileader of words 4 high held at (100, 50) with one line from (0, 0), in a drawing of its own.</summary>
        private static DxfMLeader Leader(DxfDatabase database)
        {
            var mleader = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "ABC",
                    Location = P(100, 50),
                    TextHeight = 4.0,
                },
            };
            mleader.AddLeaderLine(P(0, 0));
            database.ModelSpace.AppendEntity(mleader);
            return mleader;
        }

        private static DxfDatabase Drawing() => DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

        private static int LineOf(DxfMLeader mleader) =>
            mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes().Single()).Single();

        private static GeoPoint3 Along(GeoPoint3 a, GeoPoint3 b, double fraction) =>
            new GeoPoint3(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction, 0.0);

        [Fact]
        public void ABreak_IsKeptAfterTheVerticesAndBeforeTheIndexOfItsLine_AsAutoCadWritesIt()
        {
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);
            int line = LineOf(mleader);
            string before = database.ToText();
            GeoPoint3 start = Along(mleader.GetVertex(line, 0), mleader.GetVertex(line, 1), 0.4);
            GeoPoint3 end = Along(mleader.GetVertex(line, 0), mleader.GetVertex(line, 1), 0.5);

            mleader.SetBreaks(line, new[] { new DxfLeaderBreak(0, start, end) });

            DxfLeaderBreak got = Assert.Single(mleader.GetBreaks(line));
            Assert.Equal(0, got.Segment);
            Assert.True(start.IsEqualTo(got.Start, Close));
            Assert.True(end.IsEqualTo(got.End, Close));
            Assert.Matches(
                @"LEADER_LINE\{\n\s*10\n[^\n]*\n\s*20\n[^\n]*\n\s*30\n[^\n]*\n\s*90\n0\n\s*11\n[^\n]*\n\s*21\n[^\n]*\n\s*31\n[^\n]*\n\s*12\n[^\n]*\n\s*22\n[^\n]*\n\s*32\n[^\n]*\n\s*91\n0\n",
                database.ToText().Replace("\r\n", "\n")
            );

            // Taken away, the record is as it was, to the bit.
            mleader.SetBreaks(line, new DxfLeaderBreak[0]);
            Assert.Empty(mleader.GetBreaks(line));
            Assert.Equal(before, database.ToText());
        }

        [Fact]
        public void ExplodeGivesALineWhole_AsAutoCadDoes_AndExplodeBrokenGivesItCutAtItsBreak()
        {
            // The break from 40 to 50 per cent of the segment leaves two polylines, the first ending where the break starts, the
            // second starting where it ends.
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);
            int line = LineOf(mleader);
            GeoPoint3 a = mleader.GetVertex(line, 0);
            GeoPoint3 b = mleader.GetVertex(line, 1);
            GeoPoint3 start = Along(a, b, 0.4);
            GeoPoint3 end = Along(a, b, 0.5);
            mleader.SetBreaks(line, new[] { new DxfLeaderBreak(0, start, end) });

            DxfPolyline whole = Assert.Single(mleader.Explode().OfType<DxfPolyline>());
            List<DxfPolyline> pieces = mleader.ExplodeBroken().OfType<DxfPolyline>().ToList();

            Assert.Equal(2, whole.NumberOfVertices);
            Assert.Equal(2, pieces.Count);
            Assert.Equal(start.X, pieces[0].GetPoint2dAt(1).X, 9);
            Assert.Equal(start.Y, pieces[0].GetPoint2dAt(1).Y, 9);
            Assert.Equal(end.X, pieces[1].GetPoint2dAt(0).X, 9);
            Assert.Equal(end.Y, pieces[1].GetPoint2dAt(0).Y, 9);
            Assert.Equal(whole.GetPoint2dAt(0).X, pieces[0].GetPoint2dAt(0).X, 9);
            Assert.Equal(whole.GetPoint2dAt(1).X, pieces[1].GetPoint2dAt(1).X, 9);
        }

        [Fact]
        public void ExplodeBrokenOfALineWithNoBreak_IsWhatExplodeGives()
        {
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);

            IReadOnlyList<DxfEntity> whole = mleader.Explode();
            IReadOnlyList<DxfEntity> cut = mleader.ExplodeBroken();

            Assert.Equal(whole.Count, cut.Count);
            Assert.Equal(whole.Select(e => e.GetType()), cut.Select(e => e.GetType()));
            Assert.Equal(
                whole.OfType<DxfPolyline>().Single().GetPoint2dAt(0),
                cut.OfType<DxfPolyline>().Single().GetPoint2dAt(0)
            );
        }

        [Fact]
        public void AStretchRunsOnAcrossAVertexWhereNoBreakStopsIt()
        {
            // A line of two segments, broken in each: three polylines, the middle one through the vertex.
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);
            int line = LineOf(mleader);
            mleader.AddLastVertex(line, P(300, -80));
            GeoPoint3 a = mleader.GetVertex(line, 0);
            GeoPoint3 v = mleader.GetVertex(line, 1);
            GeoPoint3 c = mleader.GetVertex(line, 2);
            Assert.Equal(3, mleader.VerticesCount(line));
            mleader.SetBreaks(
                line,
                new[]
                {
                    new DxfLeaderBreak(1, Along(v, c, 0.5), Along(v, c, 0.6)),
                    new DxfLeaderBreak(0, Along(a, v, 0.2), Along(a, v, 0.3)),
                }
            );

            Assert.Equal(new[] { 0, 1 }, mleader.GetBreaks(line).Select(b => b.Segment));
            List<DxfPolyline> pieces = mleader.ExplodeBroken().OfType<DxfPolyline>().ToList();

            Assert.Equal(3, pieces.Count);
            Assert.Equal(2, pieces[0].NumberOfVertices);
            Assert.Equal(3, pieces[1].NumberOfVertices);
            Assert.Equal(v.X, pieces[1].GetPoint2dAt(1).X, 9);
            Assert.Equal(2, pieces[2].NumberOfVertices);
            Assert.Equal(c.X, pieces[2].GetPoint2dAt(1).X, 9);
        }

        [Fact]
        public void TheBreaksOfAMultileaderMoveWithIt()
        {
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);
            int line = LineOf(mleader);
            GeoPoint3 start = Along(mleader.GetVertex(line, 0), mleader.GetVertex(line, 1), 0.4);
            GeoPoint3 end = Along(mleader.GetVertex(line, 0), mleader.GetVertex(line, 1), 0.5);
            mleader.SetBreaks(line, new[] { new DxfLeaderBreak(0, start, end) });

            mleader.TransformBy(GeoTransform3.Translation(new GeoVector3(10, 20, 0)));

            DxfLeaderBreak moved = Assert.Single(mleader.GetBreaks(line));
            Assert.True(P(start.X + 10, start.Y + 20).IsEqualTo(moved.Start, Close));
            Assert.True(P(end.X + 10, end.Y + 20).IsEqualTo(moved.End, Close));
        }

        [Fact]
        public void SetBreaks_RefusesWhatCannotBeKept()
        {
            DxfDatabase database = Drawing();
            DxfMLeader mleader = Leader(database);
            int line = LineOf(mleader);

            Assert.Throws<ArgumentNullException>(() => mleader.SetBreaks(line, null));
            Assert.Throws<ArgumentException>(() =>
                mleader.SetBreaks(line, new[] { new DxfLeaderBreak(-1, P(1, 1), P(2, 2)) })
            );
            Assert.Throws<ArgumentException>(() =>
                mleader.SetBreaks(line, new[] { new DxfLeaderBreak(0, P(double.NaN, 1), P(2, 2)) })
            );
            Assert.Throws<ArgumentOutOfRangeException>(() => mleader.SetBreaks(line + 5, new DxfLeaderBreak[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => mleader.GetBreaks(line + 5));
        }
    }
}
