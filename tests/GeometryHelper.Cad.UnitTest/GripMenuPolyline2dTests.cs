using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The grips and hover menus of a 2D POLYLINE of straight and arc segments, as AutoCAD 2021 gives a light polyline's
    /// (probed: a heavy one of four vertices has seven grips, the vertices and the middles of its segments, and a closed one of
    /// three six): the same items, the same edits, one undo step each.
    /// </summary>
    public sealed class GripMenuPolyline2dTests
    {
        private static string Add(CadDocument document, bool closed, double[] bulges, params double[] xy)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < xy.Length; i += 2)
            {
                points.Add(new GeoPoint3(xy[i], xy[i + 1], 0));
            }

            var heavy = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0.0, closed, 0, 0, bulges);
            document.Database.ModelSpace.AppendEntity(heavy);
            return heavy.Handle;
        }

        // A rectangle 100 by 50, closed: vertices 0 to 3, then the middles of its four segments (grips 4 to 7).
        private static string Rectangle(CadDocument document) =>
            Add(document, true, new double[4], 0, 0, 100, 0, 100, 50, 0, 50);

        private static List<DxfVertex2d> Vertices(CadDocument document, string handle) =>
            Entity<DxfPolyline2d>(document, handle).ToList();

        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        [Fact]
        public void AClosedRectangleHasAGripOnEachVertexAndEachSegmentMiddle_AsALightPolylineHas()
        {
            CadDocument document = Blank();
            string handle = Rectangle(document);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(document.FindEntity(handle));

            Assert.Equal(8, grips.Count);
            Assert.Equal(4, grips.Count(g => g.Kind == CadGripKind.Vertex));
            Assert.Equal(4, grips.Count(g => g.Kind == CadGripKind.SegmentMiddle));
            Near(P(100, 0), grips[1].Point);
            Near(P(50, 0), grips[4].Point);
            Near(P(0, 25), grips[7].Point);
            Near(P(1, 0), new GeoPoint2(grips[4].Direction.X, grips[4].Direction.Y));
        }

        [Fact]
        public void AnOpenOneOfThreeVerticesHasFiveGrips_AFittedOneThreeOnItsFrame_AndNoMenu()
        {
            CadDocument document = Blank();
            string open = Add(document, false, new double[3], 0, 0, 100, 0, 100, 100);
            Assert.Equal(5, CadGrips.Of(document.FindEntity(open)).Count);

            var fitted = Entity<DxfPolyline2d>(document, open);
            fitted.ConvertToPolyType(DxfPoly2dType.CubicSplinePoly);
            Assert.Equal(3, CadGrips.Of(fitted).Count);
            Assert.All(CadGrips.Of(fitted), g => Assert.Equal(CadGripKind.Vertex, g.Kind));
            Assert.Empty(CadGripMenus.Of(document, open, 0));
        }

        // The frame (0, 0), (100, 0), (100, 60), (0, 60), fitted. AutoCAD 2021's GetGripPoints gives a fitted polyline the grips
        // of its frame alone (probed: four, at (0, 0), (100, 0), (100, 60) and (0, 60), the curve's own vertices not among them),
        // and its MoveGripPointsAt on the second by (7, 5) fits the curve again through (107, 5).
        private static string Fitted(out CadDocument document, DxfPoly2dType type, bool closed = false)
        {
            document = Blank();
            string handle = Add(document, closed, new double[4], 0, 0, 100, 0, 100, 60, 0, 60);
            Entity<DxfPolyline2d>(document, handle).ConvertToPolyType(type);
            return handle;
        }

        private static List<DxfVertex2d> Drawn(CadDocument document, string handle) =>
            Vertices(document, handle).ToList();

        [Fact]
        public void AFittedPolylineHasAGripOnEachFrameVertexAlone_TheCurvesOwnVerticesHaveNone()
        {
            string curve = Fitted(out CadDocument curved, DxfPoly2dType.FitCurvePoly);
            string spline = Fitted(out CadDocument splined, DxfPoly2dType.CubicSplinePoly);

            Assert.Equal(7, Vertices(curved, curve).Count);
            Assert.Equal(13, Vertices(splined, spline).Count);

            foreach ((CadDocument document, string handle) in new[] { (curved, curve), (splined, spline) })
            {
                IReadOnlyList<CadGrip> grips = CadGrips.Of(document.FindEntity(handle));
                Assert.Equal(4, grips.Count);
                Assert.All(grips, g => Assert.Equal(CadGripKind.Vertex, g.Kind));
                Near(P(0, 0), grips[0].Point);
                Near(P(100, 0), grips[1].Point);
                Near(P(100, 60), grips[2].Point);
                Near(P(0, 60), grips[3].Point);
                Assert.Empty(CadGripMenus.Of(document, handle, 1));
            }
        }

        [Fact]
        public void AFrameVertexOfACurveFitDragged_FitsTheCurveAgainThroughIt_AsAutoCADFitsIt()
        {
            string handle = Fitted(out CadDocument document, DxfPoly2dType.FitCurvePoly);
            string start = document.Database.ToText();

            Assert.True(CadGrips.Drag(document, handle, 1, P(107, 5)).Succeeded);

            // AutoCAD 2021: S(0, 0 b0.2976) c(55.2375, -32.4975 b0.2985) S(107, 5 b0.1232) c(110.3184, 32.8659 b0.119)
            // S(100, 60 b0.2475) c(50.0368, 86.3495 b0.2472) S(0, 60).
            List<DxfVertex2d> vertices = Drawn(document, handle);
            Assert.Equal(7, vertices.Count);
            double[,] expected =
            {
                { 0, 0, 0.2976 },
                { 55.2375, -32.4975, 0.2985 },
                { 107, 5, 0.1232 },
                { 110.3184, 32.8659, 0.119 },
                { 100, 60, 0.2475 },
                { 50.0368, 86.3495, 0.2472 },
                { 0, 60, 0 },
            };

            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(expected[i, 0], vertices[i].Position.X, 2);
                Assert.Equal(expected[i, 1], vertices[i].Position.Y, 2);
                Assert.True(
                    Math.Abs(expected[i, 2] - vertices[i].Bulge) < 1E-4,
                    "The bulge of vertex " + i + " is " + vertices[i].Bulge + "."
                );
            }

            Assert.Equal(DxfPoly2dType.FitCurvePoly, Entity<DxfPolyline2d>(document, handle).PolyType);
            Assert.Equal("GRIP", document.NextUndo.Name);
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AFrameVertexOfACubicSplineDragged_FitsTheSplineAgain_AQuadraticOneByTheHeadersSplinetype()
        {
            string cubic = Fitted(out CadDocument cubed, DxfPoly2dType.CubicSplinePoly);
            string quad = Fitted(out CadDocument quaded, DxfPoly2dType.QuadSplinePoly);

            Assert.True(CadGrips.Drag(cubed, cubic, 1, P(107, 5)).Succeeded);
            Assert.True(CadGrips.Drag(quaded, quad, 1, P(107, 5)).Succeeded);

            // AutoCAD 2021: a cubic on the frame with (107, 5) for its second vertex has 13 vertices, the curve's on from
            // (34.8223, 4.0137) through (77.625, 31.875) to (0, 60); one that was quadratic is fitted by SPLINETYPE 6, a cubic.
            foreach ((CadDocument document, string handle) in new[] { (cubed, cubic), (quaded, quad) })
            {
                List<DxfVertex2d> vertices = Drawn(document, handle);
                Assert.Equal(13, vertices.Count);
                Assert.Equal(DxfPoly2dType.CubicSplinePoly, Entity<DxfPolyline2d>(document, handle).PolyType);
                Near(new GeoPoint3(107, 5, 0), vertices[10].Position);
                Assert.Equal(34.8223, vertices[2].Position.X, 3);
                Assert.Equal(4.0137, vertices[2].Position.Y, 3);
                Assert.Equal(77.625, vertices[5].Position.X, 3);
                Assert.Equal(31.875, vertices[5].Position.Y, 3);
                Assert.Equal(DxfVertex2dType.SplineControlVertex, vertices[10].VertexType);
            }
        }

        [Fact]
        public void AFittedClosedPolylineDragsItsFrameToo_AndItsMenuItemsAreRefused()
        {
            string handle = Fitted(out CadDocument document, DxfPoly2dType.CubicSplinePoly, true);

            Assert.Equal(4, CadGrips.Of(document.FindEntity(handle)).Count);
            Assert.True(CadGrips.Drag(document, handle, 2, P(107, 65)).Succeeded);
            Assert.Contains(Drawn(document, handle), v =>
                Math.Abs(v.Position.X - 107.0) < 1E-9 && Math.Abs(v.Position.Y - 65.0) < 1E-9
            );
            Assert.True(Entity<DxfPolyline2d>(document, handle).Closed);

            Assert.False(CadGrips.AddVertex(document, handle, 1, P(50, 50)).Succeeded);
            Assert.False(CadGrips.RemoveVertex(document, handle, 1).Succeeded);
            Assert.False(CadGrips.ConvertToLine(document, handle, 1).Succeeded);
            Assert.False(Session(document).StartGripAction(handle, 1, CadGripAction.AddVertex));
        }

        [Fact]
        public void TheMenusAreAlightPolylines_VertexStraightSegmentArcSegment()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new[] { 1.0, 0.0, 0.0 }, 0, 0, 100, 0, 100, 100);
            string two = Add(document, false, new double[2], 0, 0, 10, 0);

            Assert.Equal(new[] { "Stretch Vertex", "Add Vertex", "Remove Vertex" }, Texts(document, handle, 1));
            Assert.Equal(new[] { "Stretch", "Add Vertex", "Convert to Line" }, Texts(document, handle, 3));
            Assert.Equal(new[] { "Stretch", "Add Vertex", "Convert to Arc" }, Texts(document, handle, 4));

            Assert.False(CadGripMenus.Of(document, two, 0).Single(i => i.Action == CadGripAction.RemoveVertex).Enabled);
        }

        [Fact]
        public void StretchAVertex_MovesIt_AStraightSegmentMovesBothEnds_AnArcSegmentBends()
        {
            CadDocument document = Blank();
            string handle = Rectangle(document);

            Assert.True(CadGrips.Drag(document, handle, 2, P(110, 60)).Succeeded);
            Near(
                P(110, 60),
                new GeoPoint2(Vertices(document, handle)[2].Position.X, Vertices(document, handle)[2].Position.Y)
            );
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(CadGrips.Drag(document, handle, 4, P(50, -10)).Succeeded);
            Assert.Equal(-10.0, Vertices(document, handle)[0].Position.Y, 9);
            Assert.Equal(-10.0, Vertices(document, handle)[1].Position.Y, 9);

            // The segment from (110, 60) to (0, 50) is the third, its middle grip 6: made an arc, then dragged, it bends.
            Assert.True(CadGrips.ConvertToArc(document, handle, 6, P(55, 70)).Succeeded);
            double was = Vertices(document, handle)[2].Bulge;
            Assert.True(CadGrips.Drag(document, handle, 6, P(55, 100)).Succeeded);
            Assert.NotEqual(was, Vertices(document, handle)[2].Bulge);
        }

        [Fact]
        public void AddVertexAfterVertexOne_MakesFiveVertices_TheNewOneAtIndexTwo_AsOneUndoStep()
        {
            CadDocument document = Blank();
            string handle = Rectangle(document);

            Assert.True(CadGrips.AddVertex(document, handle, 1, P(120, 25)).Succeeded);

            List<DxfVertex2d> vertices = Vertices(document, handle);
            Assert.Equal(5, vertices.Count);
            Near(P(100, 0), new GeoPoint2(vertices[1].Position.X, vertices[1].Position.Y));
            Near(P(120, 25), new GeoPoint2(vertices[2].Position.X, vertices[2].Position.Y));
            Near(P(100, 50), new GeoPoint2(vertices[3].Position.X, vertices[3].Position.Y));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(4, Vertices(document, handle).Count);
        }

        [Fact]
        public void AddVertexOnAnArcSegment_SplitsItInTwoArcs_AHalfCircleIntoTwoQuarters()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new[] { 1.0, 0.0 }, 0, 0, 20, 0);

            // The middle grip of the one segment is grip 2.
            Assert.True(CadGrips.AddVertex(document, handle, 2, P(10, -10)).Succeeded);

            List<DxfVertex2d> vertices = Vertices(document, handle);
            Assert.Equal(3, vertices.Count);
            Assert.Equal(Math.Sqrt(2.0) - 1.0, Math.Abs(vertices[0].Bulge), 4);
            Assert.Equal(Math.Sqrt(2.0) - 1.0, Math.Abs(vertices[1].Bulge), 4);
        }

        [Fact]
        public void AddVertexAfterTheLastVertexOfAnOpenOne_ExtendsItsEnd()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new double[3], 0, 0, 100, 0, 100, 100);

            Assert.True(CadGrips.AddVertex(document, handle, 2, P(150, 120)).Succeeded);

            List<DxfVertex2d> vertices = Vertices(document, handle);
            Assert.Equal(4, vertices.Count);
            Near(P(150, 120), new GeoPoint2(vertices[3].Position.X, vertices[3].Position.Y));
        }

        [Fact]
        public void RemoveVertex_TakesItOut_TheSegmentThatLeavesItsNeighbourBecomingStraight()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new[] { 0.5, 0.5, 0.0 }, 0, 0, 50, 20, 100, 0);
            string rectangle = Rectangle(document);

            Assert.True(CadGrips.RemoveVertex(document, handle, 1).Succeeded);

            List<DxfVertex2d> vertices = Vertices(document, handle);
            Assert.Equal(2, vertices.Count);
            Assert.Equal(0.0, vertices[0].Bulge);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(3, Vertices(document, handle).Count);
            Assert.Equal(0.5, Vertices(document, handle)[0].Bulge, 9);

            Assert.True(CadGrips.RemoveVertex(document, handle, 1).Succeeded);
            Assert.False(CadGrips.RemoveVertex(document, handle, 0).Succeeded);
            Assert.False(CadGrips.RemoveVertex(document, rectangle, 4).Succeeded);
        }

        [Fact]
        public void RemoveVertexChosenFromTheMenu_OfA2dPolyline_TakesItOutAtOnce_AsOneUndoStep()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new[] { 0.0, 0.0, 0.0, 0.0 }, 0, 0, 100, 0, 100, 50, 0, 50);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 2, CadGripAction.RemoveVertex));

            Assert.False(session.IsRunning);
            List<DxfVertex2d> vertices = Vertices(document, handle);
            Assert.Equal(3, vertices.Count);
            Near(P(0, 50), new GeoPoint2(vertices[2].Position.X, vertices[2].Position.Y));
            Assert.True(document.Undo());
            Assert.Equal(4, Vertices(document, handle).Count);
        }

        [Fact]
        public void ConvertToArcThroughAPoint_AndConvertToLine_AreOneUndoStepEach()
        {
            CadDocument document = Blank();
            string handle = Rectangle(document);

            Assert.True(CadGrips.ConvertToArc(document, handle, 4, P(50, -20)).Succeeded);
            Assert.Equal(0.4, Vertices(document, handle)[0].Bulge, 9);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.False(CadGrips.ConvertToArc(document, handle, 4, P(50, -30)).Succeeded);

            Assert.True(CadGrips.ConvertToLine(document, handle, 4).Succeeded);
            Assert.Equal(0.0, Vertices(document, handle)[0].Bulge);
            Assert.False(CadGrips.ConvertToLine(document, handle, 4).Succeeded);
            Assert.True(document.Undo());
            Assert.Equal(0.4, Vertices(document, handle)[0].Bulge, 9);
        }

        [Fact]
        public void TheMenuItems_RunOnA2dPolylineWithTheLightOnesPrompts()
        {
            CadDocument document = Blank();
            string handle = Add(document, false, new[] { 1.0, 0.0, 0.0 }, 0, 0, 100, 0, 100, 100);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 1, CadGripAction.AddVertex));
            Assert.Equal("** ADD VERTEX **", session.History.Last());
            Assert.Equal("Specify new vertex point or [Base point/Copy/Undo/eXit]:", session.PromptText);
            Run(session, P(120, 20));
            Assert.False(session.IsRunning);
            Assert.Equal(4, Vertices(document, handle).Count);

            // The arc segment is the first, its middle grip 4 now (four vertices before it).
            Assert.True(session.StartGripAction(handle, 4, CadGripAction.ConvertToLine));
            Assert.Equal("Pick to convert to line segment:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal(0.0, Vertices(document, handle)[0].Bulge);

            Assert.True(session.StartGripAction(handle, 4, CadGripAction.ConvertToArc));
            Assert.Equal("Specify midpoint of arc segment:", session.PromptText);
            Assert.NotNull(session.Prompt.Preview);
            Run(session, P(50, -20));
            Assert.Equal(0.4, Vertices(document, handle)[0].Bulge, 9);
        }
    }
}
