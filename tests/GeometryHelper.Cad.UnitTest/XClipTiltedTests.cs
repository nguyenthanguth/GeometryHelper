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
    /// A clip made in a user coordinate system tilted out of the plane of the drawing (AutoCAD 2021 writes one; the presenter
    /// works in plan systems alone, so the tests make it through the clip's own matrices), as AutoCAD 2021 keeps, draws and
    /// drags it: probes of accoreconsole with a UCS at (100, 50, 20) turned 30 degrees about X, the rectangle (10, 10) to
    /// (90, 60) typed in it, and MoveGripPointsAt called on the clip's grips.
    /// </summary>
    public class XClipTiltedTests
    {
        private static readonly double Cos30 = Math.Cos(Math.PI / 6.0);

        private static readonly GeoTransform3 Tilted = CadXClips.WorldToClip(
            new CadUcs(new GeoPoint3(100, 50, 20), GeoVector3.XAxis, new GeoVector3(0.0, Cos30, 0.5))
        );

        /// <summary>
        /// A block GHT of a square 300 by 300 at height 0 with its origin at (5, 7, 0), inserted as it is given and clipped to
        /// the rectangle (10, 10) to (90, 60) of the tilted system.
        /// </summary>
        private static CadDocument Clipped(
            out CadCommandSession session,
            out string handle,
            GeoPoint3 at = default,
            double scale = 1.0,
            double degrees = 0.0,
            GeoPoint3 origin = default
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHT", Origin = origin };
            database.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(300, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(300, 300), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 300), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            var insert = new DxfBlockReference(at, block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
                Rotation = degrees * Math.PI / 180.0,
            };
            database.ModelSpace.AppendEntity(insert);
            insert.ClipTo(new[] { new GeoPoint2(10, 10), new GeoPoint2(90, 60) }, Tilted);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xcliptilted.dxf");
            session = Session(document);
            return document;
        }

        private static DxfBlockReference Reference(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static void AssertPoint(double x, double y, GeoPoint2 actual, int places = 6)
        {
            Assert.Equal(x, actual.X, places);
            Assert.Equal(y, actual.Y, places);
        }

        [Fact]
        public void TheGrips_StandWhereTheBoundaryIsSeenFromAbove_AsAutoCadGivesThem()
        {
            CadDocument document = Clipped(out _, out string handle);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Reference(document, handle));

            // AutoCAD 2021's GetGripPoints: the corners (110, 58.66, 25), (190, 58.66, 25), (190, 101.96, 50), (110, 101.96, 50).
            Assert.Equal(6, grips.Count);
            AssertPoint(150, 50 + 10 * Cos30, grips[1].Point);
            AssertPoint(110, 50 + 10 * Cos30, grips[2].Point);
            AssertPoint(190, 50 + 10 * Cos30, grips[3].Point);
            AssertPoint(190, 50 + 60 * Cos30, grips[4].Point);
            AssertPoint(110, 50 + 60 * Cos30, grips[5].Point);
        }

        [Fact]
        public void ACornerDragged_MovesBySteps_OfTheCursorsOffsetAlongTheBoundarysAxes_AsAutoCadMovesIt()
        {
            CadDocument document = Clipped(out _, out string handle);

            // AutoCAD 2021, MoveGripPointsAt of the corner (110, 58.66, 25) by (5, 3, 0): the corner (10, 10) of the boundary
            // goes to (15, 10 + 3 cos 30), the offset's parts along the boundary's X and Y axes.
            Assert.True(document.GripDrag(handle, 2, P(115, 50 + 10 * Cos30 + 3)).Succeeded);

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(2, points.Count);
            AssertPoint(15, 10 + 3 * Cos30, points[0], 9);
            AssertPoint(90, 60, points[1], 9);
        }

        [Fact]
        public void ACornerDragged_OfAReferenceScaledTurnedLiftedAndThenMoved_StillMovesByTheOffsetOnTheAxes()
        {
            CadDocument document = Clipped(
                out CadCommandSession session,
                out string handle,
                new GeoPoint3(50, 20, 5),
                2.0,
                30.0,
                new GeoPoint3(5, 7, 0)
            );

            // AutoCAD 2021: corner 3 (190, 58.66) by (7, -4, 0) gives (97, 10 - 4 cos 30) and the rectangle (10, 6.5359) to (97, 60).
            Assert.True(document.GripDrag(handle, 3, P(197, 50 + 10 * Cos30 - 4)).Succeeded);
            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            AssertPoint(10, 10 - 4 * Cos30, points[0], 9);
            AssertPoint(97, 60, points[1], 9);

            // The reference moved by (30, 10, 0) takes its clip along; corner 4 (227, 111.96) by (-3, 2, 0) gives (94, 60 + 2 cos 30).
            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(30, 10));
            Assert.True(document.GripDrag(handle, 4, P(224, 50 + 60 * Cos30 + 10 + 2)).Succeeded);
            points = Reference(document, handle).SpatialFilter.Points;
            AssertPoint(10, 10 - 4 * Cos30, points[0], 9);
            AssertPoint(94, 60 + 2 * Cos30, points[1], 9);
        }

        [Fact]
        public void TheBlockIsShownWhereTheBoundarysPrismCutsItsPlane_AndTheFrameWhereTheBoundaryStands()
        {
            CadDocument document = Clipped(out _, out string handle);

            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle)
                .ToList();

            // AutoCAD 2021, PNGOUT of a grid of this block in plan: the grid lines of the block's plane show from y 73.09 to
            // 130.83, where the lines through the corners along the boundary's normal meet the plane (110, 58.66, 25) and
            // (110, 101.96, 50) stand at, while the frame stands at the boundary itself, y 58.66 to 101.96.
            DisplayItem2 shown = Assert.Single(items, i => !i.IsClipFrame);
            DisplayBox region = shown.BlockClip.Bounds;
            Assert.Equal(110.0, region.MinX, 6);
            Assert.Equal(190.0, region.MaxX, 6);
            Assert.Equal(50 + 10 * Cos30 + 25 * 0.5 / Cos30, region.MinY, 6);
            Assert.Equal(50 + 60 * Cos30 + 50 * 0.5 / Cos30, region.MaxY, 6);

            DisplayItem2 frame = Assert.Single(items, i => i.IsClipFrame);
            Assert.Equal(50 + 10 * Cos30, frame.Bounds.MinY, 6);
            Assert.Equal(50 + 60 * Cos30, frame.Bounds.MaxY, 6);
        }

        [Fact]
        public void AClipSquareToThePlane_IsDrawnInPlaceAsBefore()
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHS" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 40), block);
            database.ModelSpace.AppendEntity(insert);
            insert.ClipTo(
                new[] { new GeoPoint2(10, 10), new GeoPoint2(90, 60) },
                CadXClips.WorldToClip(new CadUcs(new GeoPoint3(0, 0, 25), GeoVector3.XAxis, GeoVector3.YAxis))
            );
            CadDocument document = CadDocument.From(database, "xclipflat.dxf");

            DisplayItem2 shown = Assert.Single(document.GetDisplay(document.ModelSpace).Items, i =>
                i.Handle == insert.Handle && !i.IsClipFrame
            );

            // A clip square to the block's plane, though 15 apart from it, shows what is over the boundary: its corners.
            Assert.Equal(10.0, shown.BlockClip.Bounds.MinX, 9);
            Assert.Equal(90.0, shown.BlockClip.Bounds.MaxX, 9);
            Assert.Equal(10.0, shown.BlockClip.Bounds.MinY, 9);
            Assert.Equal(60.0, shown.BlockClip.Bounds.MaxY, 9);
        }
    }
}
