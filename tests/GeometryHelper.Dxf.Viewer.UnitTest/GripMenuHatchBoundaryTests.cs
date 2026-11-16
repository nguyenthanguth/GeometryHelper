using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The grips of the boundary of a hatch that follows no objects, as AutoCAD 2021's GetGripPoints gives them and its
    /// MoveGripPointsAt moves them (probed in its console on a rectangle 100 by 60, the same with an arc segment, with edges,
    /// and with a circle): after the grip of the middle, a vertex and the middle of the segment leaving it, for each loop.
    /// </summary>
    public sealed class GripMenuHatchBoundaryTests
    {
        private static readonly GeoPoint2[] Rectangle =
        {
            new GeoPoint2(0, 0),
            new GeoPoint2(100, 0),
            new GeoPoint2(100, 60),
            new GeoPoint2(0, 60),
        };

        private static string Hatched(CadDocument document, Action<DxfHatch> loops)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.PatternScale = 2.0;
            loops(hatch);
            document.Database.ModelSpace.AppendEntity(hatch);
            return hatch.Handle;
        }

        private static string RectangleHatch(CadDocument document, double[] bulges = null) =>
            Hatched(document, h => h.AppendLoop(DxfHatchLoopTypes.External, Rectangle, bulges));

        private static DxfHatch Of(CadDocument document, string handle) => Entity<DxfHatch>(document, handle);

        private static GeoPolygonArc3 Polygon(CadDocument document, string handle, int loop = 0) =>
            (GeoPolygonArc3)Of(document, handle).GetLoopAt(loop).Polyline;

        [Fact]
        public void ANonAssociativeRectangleHasItsMiddleThenAVertexAndASegmentMiddleForEachSide_AsAutoCADGivesThem()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Of(document, handle));

            Assert.Equal(9, grips.Count);
            Assert.Equal(CadGripKind.Center, grips[0].Kind);
            Near(P(50, 30), grips[0].Point);
            GeoPoint2[] expected =
            {
                P(0, 0),
                P(50, 0),
                P(100, 0),
                P(100, 30),
                P(100, 60),
                P(50, 60),
                P(0, 60),
                P(0, 30),
            };

            for (int i = 0; i < 8; i++)
            {
                Near(expected[i], grips[i + 1].Point);
                Assert.Equal(i % 2 == 0 ? CadGripKind.Vertex : CadGripKind.Middle, grips[i + 1].Kind);
            }
        }

        [Fact]
        public void AHatchThatFollowsAnObject_KeepsItsOneGrip()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document);
            Assert.Equal(9, CadGrips.Of(Of(document, handle)).Count);

            Of(document, handle).Associative = true;

            Assert.Equal(CadGripKind.Center, Assert.Single(CadGrips.Of(Of(document, handle))).Kind);
        }

        [Fact]
        public void TwoLoopsHaveTheirGripsOneAfterTheOther()
        {
            CadDocument document = Blank();
            string handle = Hatched(document, h =>
            {
                h.AppendLoop(DxfHatchLoopTypes.External, Rectangle, null);
                h.AppendLoop(DxfHatchLoopTypes.Default, new[] { P(20, 20), P(40, 20), P(40, 40), P(20, 40) }, null);
            });

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Of(document, handle));

            Assert.Equal(17, grips.Count);
            Near(P(20, 20), grips[9].Point);
            Near(P(30, 20), grips[10].Point);
            Near(P(40, 40), grips[13].Point);
            Near(P(20, 30), grips[16].Point);
        }

        [Fact]
        public void TheMiddleOfAnArcSegmentIsOnTheArc_AndTheBoundaryGripsHaveNoMenu()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document, new[] { 0.0, 0.5, 0.0, 0.0 });

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Of(document, handle));

            // The bulge 0.5 of (100, 0) to (100, 60) puts the middle of the arc 15 out, at (115, 30); the box the loops fill is
            // 115 wide, so the grip of the middle moves to (57.5, 30), as AutoCAD 2021 gives it.
            Near(P(115, 30), grips[4].Point);
            Near(P(57.5, 30), grips[0].Point);
            Assert.Equal(CadGripKind.Middle, grips[4].Kind);
            Assert.Equal("Stretch", CadGripMenus.Of(document, handle, 0).First().Text);
            Assert.Empty(CadGripMenus.Of(document, handle, 1));
            Assert.Empty(CadGripMenus.Of(document, handle, 4));
        }

        [Fact]
        public void AVertexDragged_MovesAlone_TheOthersKept_AsOneUndoStep()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document);
            string start = document.Database.ToText();

            Assert.True(CadGrips.Drag(document, handle, 1, P(7, 5)).Succeeded);

            GeoPolygonArc3 loop = Polygon(document, handle);
            Assert.Equal(4, loop.VertexCount);
            Near(new GeoPoint3(7, 5, 0), loop[0]);
            Near(new GeoPoint3(100, 0, 0), loop[1]);
            Near(new GeoPoint3(100, 60, 0), loop[2]);
            Near(new GeoPoint3(0, 60, 0), loop[3]);
            Assert.Equal(
                DxfHatchLoopTypes.External,
                Of(document, handle).GetLoopAt(0).LoopType & DxfHatchLoopTypes.External
            );
            Assert.Equal("GRIP", document.NextUndo.Name);

            // The grip after it is the middle of the segment from the moved vertex, as AutoCAD 2021 gives it (53.5, 2.5).
            Near(P(53.5, 2.5), CadGrips.Of(Of(document, handle))[2].Point);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void TheMiddleOfAStraightSegmentDragged_TakesBothItsEndsAlong()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document);

            Assert.True(CadGrips.Drag(document, handle, 2, P(57, 5)).Succeeded);

            GeoPolygonArc3 loop = Polygon(document, handle);
            Near(new GeoPoint3(7, 5, 0), loop[0]);
            Near(new GeoPoint3(107, 5, 0), loop[1]);
            Near(new GeoPoint3(100, 60, 0), loop[2]);
            Near(new GeoPoint3(0, 60, 0), loop[3]);
        }

        [Fact]
        public void TheMiddleOfAnArcSegmentDragged_BendsTheArcThroughThePoint_AsAutoCADBendsIt()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document, new[] { 0.0, 0.5, 0.0, 0.0 });

            // AutoCAD 2021: the grip (115, 30) moved by (7, 5) leaves the ends where they are and the arc's middle at
            // (122.4021, 30), the arc through (100, 0), (122, 35) and (100, 60).
            Assert.True(CadGrips.Drag(document, handle, 4, P(122, 35)).Succeeded);

            GeoPolygonArc3 loop = Polygon(document, handle);
            Near(new GeoPoint3(100, 0, 0), loop[1]);
            Near(new GeoPoint3(100, 60, 0), loop[2]);
            Near(P(122.4021, 30), CadGrips.Of(Of(document, handle))[4].Point, 1E-3);

            CadEditResult refused = CadGrips.Drag(document, handle, 4, P(100, 30));
            Assert.False(refused.Succeeded);
            Near(P(122.4021, 30), CadGrips.Of(Of(document, handle))[4].Point, 1E-3);
        }

        [Fact]
        public void ALoopOfEdgesHasTheSameGrips_AndStaysALoopOfEdgesWhenAVertexMoves()
        {
            CadDocument document = Blank();
            string handle = Hatched(document, h =>
                h.AppendLoop(
                    new DxfHatchLoop(
                        DxfHatchLoopTypes.External,
                        new object[]
                        {
                            new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                            new GeoLine3(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0)),
                            new GeoLine3(new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0)),
                            new GeoLine3(new GeoPoint3(0, 60, 0), new GeoPoint3(0, 0, 0)),
                        }
                    )
                )
            );

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Of(document, handle));
            Assert.Equal(9, grips.Count);
            Near(P(100, 30), grips[4].Point);

            Assert.True(CadGrips.Drag(document, handle, 3, P(107, 5)).Succeeded);

            DxfHatchLoop loop = Of(document, handle).GetLoopAt(0);
            Assert.False(loop.IsPolyline);
            Assert.Equal(4, loop.Curves.Count);
            var first = Assert.IsType<GeoLine3>(loop.Curves[0]);
            Near(new GeoPoint3(107, 5, 0), first.EndPoint);
            var second = Assert.IsType<GeoLine3>(loop.Curves[1]);
            Near(new GeoPoint3(107, 5, 0), second.StartPoint);
        }

        [Fact]
        public void ACircleLoopHasItsFourQuadrants_AndADraggedOneSetsTheRadiusToThePoint()
        {
            CadDocument document = Blank();
            string handle = Hatched(document, h =>
                h.AppendLoop(
                    new DxfHatchLoop(
                        DxfHatchLoopTypes.External,
                        new object[] { new GeoCircle3(new GeoPoint3(50, 30, 0), new GeoVector3(0, 0, 1), 30) }
                    )
                )
            );

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Of(document, handle));

            // East, west, north and south, after the middle, as AutoCAD 2021 gives them.
            Assert.Equal(5, grips.Count);
            Near(P(80, 30), grips[1].Point);
            Near(P(20, 30), grips[2].Point);
            Near(P(50, 60), grips[3].Point);
            Near(P(50, 0), grips[4].Point);
            Assert.All(grips.Skip(1), g => Assert.Equal(CadGripKind.Quadrant, g.Kind));

            // AutoCAD 2021: the east quadrant moved by (7, 5) makes the radius 37.3363 about the same centre.
            Assert.True(CadGrips.Drag(document, handle, 1, P(87, 35)).Succeeded);

            var circle = Assert.IsType<GeoCircle3>(Of(document, handle).GetLoopAt(0).Curves.Single());
            Assert.Equal(Math.Sqrt(37.0 * 37.0 + 25.0), circle.Radius, 9);
            Near(new GeoPoint3(50, 30, 0), circle.Center);
            Assert.False(CadGrips.Drag(document, handle, 1, P(50, 30)).Succeeded);
        }

        [Fact]
        public void AnAssociativeHatchHasNoBoundaryGrip_SoDraggingOneIsRefused()
        {
            CadDocument document = Blank();
            string handle = RectangleHatch(document);
            Of(document, handle).Associative = true;

            Assert.False(CadGrips.Drag(document, handle, 1, P(7, 5)).Succeeded);
            Assert.False(document.CanUndo && document.NextUndo.Name == "GRIP");
        }
    }
}
