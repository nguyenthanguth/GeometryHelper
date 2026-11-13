using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PEDIT Multiple with 3D polylines against AutoCAD 2021's own console (13 November, the 3DPOLY (0, 0, 0), (100, 0, 10),
    /// (100, 100, 20), (0, 100, 30) selected alone): it asks no question about lines, arcs and splines and offers the options
    /// of a 2D polyline (Close/Open/Join/Width/Fit/Spline/Decurve/Ltype gen/Reverse/Undo); Close, Open, Spline, Decurve and
    /// Reverse do to the 3D polyline what they do alone, Width asks its question and Ltype gen its own and neither changes it,
    /// Fit changes it not at all, Join asks the fuzz distance and adds no segment.
    /// </summary>
    public class PeditMultiple3dTests
    {
        private const string ManyOptions =
            "Enter an option [Close/Open/Join/Width/Fit/Spline/Decurve/Ltype gen/Reverse/Undo]:";

        private static readonly GeoPoint3[] Corners =
        {
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(100, 0, 10),
            new GeoPoint3(100, 100, 20),
            new GeoPoint3(0, 100, 30),
        };

        private static CadDocument Drawn(out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Session(document);
            document.ApplyEdit("draw", null, s =>
                s.Append(document.ModelSpace, new DxfPolyline3d(DxfPoly3dType.SimplePoly, Corners, false))
            );
            return document;
        }

        private static DxfPolyline3d Only(CadDocument document) =>
            Assert.Single(All<DxfPolyline3d>(document.ModelSpace));

        private static List<GeoPoint3> Frame(DxfPolyline3d polyline) =>
            polyline.Where(v => v.VertexType != DxfVertex3dType.FitVertex).Select(v => v.Position).ToList();

        [Fact]
        public void Multiple_OfA3dPolylineAlone_AsksNoQuestionAndOffersTheOptionsOfA2dPolyline()
        {
            Drawn(out CadCommandSession session);

            Run(session, "PEDIT", "M", "ALL", Enter);

            Assert.Equal(ManyOptions, session.PromptText);
            Assert.DoesNotContain(session.History, line => line.StartsWith("Convert Lines"));
        }

        [Fact]
        public void Multiple_ClosesOpensReversesSplinesAndStraightensA3dPolyline_AsItDoesAlone()
        {
            CadDocument document = Drawn(out CadCommandSession session);
            Run(session, "PEDIT", "M", "ALL", Enter);

            Run(session, "C");
            Assert.True(Only(document).Closed);
            Run(session, "O");
            Assert.False(Only(document).Closed);

            Run(session, "R");
            Assert.Equal(new[] { Corners[3], Corners[2], Corners[1], Corners[0] }, Frame(Only(document)));

            // SPLINETYPE 6 and SPLINESEGS 8: the 9 vertices of the curve and the 4 of the frame, as AutoCAD 2021 stored them.
            Run(session, "S");
            DxfPolyline3d fitted = Only(document);
            Assert.Equal(DxfPoly3dType.CubicSplinePoly, fitted.PolyType);
            Assert.Equal(13, fitted.Count());

            Run(session, "D");
            DxfPolyline3d straight = Only(document);
            Assert.Equal(DxfPoly3dType.SimplePoly, straight.PolyType);
            Assert.Equal(4, straight.Count());
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Multiple_WidthFitAndLtypeGenOnA3dPolyline_AskTheirQuestionsAndChangeNothing()
        {
            CadDocument document = Drawn(out CadCommandSession session);
            Run(session, "PEDIT", "M", "ALL", Enter);

            Run(session, "W");
            Assert.Equal("Specify new width for all segments:", session.PromptText);
            Run(session, "5");
            Assert.Equal(ManyOptions, session.PromptText);
            Run(session, "F");
            Assert.Equal(ManyOptions, session.PromptText);
            Run(session, "L");
            Assert.Equal("Enter polyline linetype generation option [ON/OFF]", session.PromptText);
            Run(session, "ON");

            DxfPolyline3d polyline = Only(document);
            Assert.Equal(DxfPoly3dType.SimplePoly, polyline.PolyType);
            Assert.Equal(Corners.ToList(), Frame(polyline));
        }

        [Fact]
        public void Multiple_JoinWithA3dPolyline_AsksTheFuzzDistanceAndAddsNoSegment()
        {
            CadDocument document = Drawn(out CadCommandSession session);
            document.ApplyEdit("draw", null, s =>
                s.Append(
                    document.ModelSpace,
                    new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { Corners[3], new GeoPoint3(0, 200, 40) }, false)
                )
            );
            Run(session, "PEDIT", "M", "ALL", Enter);

            Run(session, "J");
            Assert.Contains("Join Type = Extend", session.History);
            Assert.Equal("Enter fuzz distance or [Jointype] <0.0000>:", session.PromptText);
            Run(session, "5");

            Assert.Contains("0 segments added to polyline", session.History);
            Assert.Equal(2, All<DxfPolyline3d>(document.ModelSpace).Count);
            Assert.Equal(ManyOptions, session.PromptText);
        }

        [Fact]
        public void Multiple_OfA2dAndA3dPolyline_ClosesBoth()
        {
            CadDocument document = Drawn(out CadCommandSession session);
            Run(session, "PLINE", P(200, 0), P(210, 0), P(210, 10), Enter);

            Run(session, "PEDIT", "M", "ALL", Enter, "C");

            Assert.True(Only(document).Closed);
            Assert.True(Assert.Single(All<DxfPolyline>(document.ModelSpace)).Closed);
        }
    }
}
