using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// JOIN and BOUNDARY under PLINETYPE. AutoCAD 2021's console (13 November) joined a line into the POLYLINE PLINE drew at
    /// PLINETYPE 0 and left it a POLYLINE at 0, made it a LWPOLYLINE at 1 and 2; a LWPOLYLINE stayed one at 0. Its console runs
    /// neither the multiple-object JOIN nor BOUNDARY, so what they make at 0 follows what PLINE, POLYGON and PEDIT make.
    /// </summary>
    public class PlineTypeJoinBoundaryTests
    {
        private static List<GeoPoint2> Corners(DxfPolyline2d polyline) =>
            polyline.Select(v => new GeoPoint2(v.Position.X, v.Position.Y)).ToList();

        /// <summary>The polyline PLINE draws from (0, 0) through (100, 0) to (100, 50) at a PLINETYPE, and a line from its end.</summary>
        private static CadDocument Drawn(int made, out CadCommandSession session, out string line)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "PLINETYPE", made.ToString(), "PLINE", P(0, 0), P(100, 0), P(100, 50), Enter);
            Run(session, "LINE", P(100, 50), P(0, 80), Enter);
            line = All<DxfLine>(document.ModelSpace).Single().Handle;
            return document;
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        public void JoinOfALineIntoAPolylineMadeAtNought_LeavesAPolylineAtNought_AndALwpolylineAtOneAndTwo(
            int joinedAt,
            bool stays
        )
        {
            CadDocument document = Drawn(0, out CadCommandSession session, out string line);
            Assert.Single(All<DxfPolyline2d>(document.ModelSpace));

            Run(session, "PLINETYPE", joinedAt.ToString(), "JOIN", P(50, 0), Enter);
            Assert.Equal("Select objects to join:", session.PromptText);
            Run(session, new[] { line }, Enter);

            Assert.Empty(All<DxfLine>(document.ModelSpace));

            if (stays)
            {
                Assert.Empty(All<DxfPolyline>(document.ModelSpace));
                DxfPolyline2d heavy = Assert.Single(All<DxfPolyline2d>(document.ModelSpace));
                List<GeoPoint2> corners = Corners(heavy);
                Assert.Equal(4, corners.Count);
                Near(P(0, 0), corners[0]);
                Near(P(100, 0), corners[1]);
                Near(P(100, 50), corners[2]);
                Near(P(0, 80), corners[3]);
            }
            else
            {
                Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
                DxfPolyline light = Assert.Single(All<DxfPolyline>(document.ModelSpace));
                Assert.Equal(4, light.NumberOfVertices);
                Near(P(0, 80), light.GetPoint2dAt(3));
            }

            AssertReopens(document);
        }

        [Fact]
        public void JoinOfALineIntoALwpolyline_LeavesALwpolylineAtNought()
        {
            CadDocument document = Drawn(2, out CadCommandSession session, out string line);

            Run(session, "PLINETYPE", "0", "JOIN", P(50, 0), Enter, new[] { line }, Enter);

            Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
            Assert.Equal(4, Assert.Single(All<DxfPolyline>(document.ModelSpace)).NumberOfVertices);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void JoinAtOnceOfTwoLines_MakesAPolylineOfTheKindPlineTypeAsks(int plineType)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            DxfLine first = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            DxfLine second = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            document.Database.ModelSpace.AppendEntity(first);
            document.Database.ModelSpace.AppendEntity(second);

            Run(session, "PLINETYPE", plineType.ToString(), "JOIN", new[] { first.Handle, second.Handle }, Enter);

            Assert.Contains("2 objects converted to 1 polyline", session.History);
            Assert.Equal(plineType == 0 ? 1 : 0, All<DxfPolyline2d>(document.ModelSpace).Count);
            Assert.Equal(plineType == 0 ? 0 : 1, All<DxfPolyline>(document.ModelSpace).Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void BoundaryOfASquare_MakesTheClosedPolylineOfTheKindPlineTypeAsks(int plineType)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), P(100, 100), P(0, 100), "C");
            Run(session, "PLINETYPE", plineType.ToString(), "-BOUNDARY", P(50, 50));

            Assert.Contains("BOUNDARY created 1 polyline", session.History);

            if (plineType == 0)
            {
                DxfPolyline2d heavy = Assert.Single(All<DxfPolyline2d>(document.ModelSpace));
                Assert.True(heavy.Closed);
                Assert.Equal(4, heavy.Count());
                Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            }
            else
            {
                Assert.True(Assert.Single(All<DxfPolyline>(document.ModelSpace)).Closed);
                Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
            }
        }
    }
}
