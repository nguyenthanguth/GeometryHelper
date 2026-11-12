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
    /// The grips of a clipped block reference, as AutoCAD 2021's Entity.GetGripPoints gives them (probes GHXE2, GHXE3): its
    /// insertion point, an arrow at the middle of the first side of the boundary, and a grip at each corner; a corner dragged
    /// moves the boundary's corner, the arrow clicked turns the clip inside out; and EXPLODE of a clipped reference, which
    /// breaks the whole block and leaves the clip behind.
    /// </summary>
    public class XClipGripTests
    {
        /// <summary>
        /// A block GHB of a square 100 by 100 and a circle of radius 20, its origin (10, 20), inserted at (1000, 500) and
        /// clipped, by XCLIP in the world's coordinate system, to the rectangle (1020, 520) to (1080, 570).
        /// </summary>
        private static CadDocument Clipped(
            out CadCommandSession session,
            out string handle,
            double scale = 1.0,
            double degrees = 0.0,
            params GeoPoint2[] polygon
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHB", Origin = new GeoPoint3(10, 20, 0) };
            database.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(100, 100), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 100), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            block.AppendEntity(new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20.0));
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
                Rotation = degrees * Math.PI / 180.0,
            };
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipgrips.dxf");
            session = Session(document);

            if (polygon.Length == 0)
            {
                Run(
                    session,
                    "XCLIP",
                    new[] { handle },
                    Enter,
                    "New boundary",
                    "Rectangular",
                    P(1020, 520),
                    P(1080, 570)
                );
            }
            else
            {
                var inputs = new List<object> { "XCLIP", new[] { handle }, Enter, "New boundary", "Polygonal" };
                inputs.AddRange(polygon.Cast<object>());
                inputs.Add(Enter);
                Run(session, inputs.ToArray());
            }

            return document;
        }

        private static DxfBlockReference Reference(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static IReadOnlyList<CadGrip> Grips(CadDocument document, string handle) =>
            CadGrips.Of(Reference(document, handle));

        private static void AssertPoint(double x, double y, GeoPoint2 actual, int places = 6)
        {
            Assert.Equal(x, actual.X, places);
            Assert.Equal(y, actual.Y, places);
        }

        [Fact]
        public void AClippedReference_HasItsInsertionGrip_TheArrowAtTheMiddleOfTheFirstSide_AndAGripAtEachCorner()
        {
            CadDocument document = Clipped(out _, out string handle);

            IReadOnlyList<CadGrip> grips = Grips(document, handle);

            Assert.Equal(6, grips.Count);
            Assert.Equal(CadGripKind.Insertion, grips[0].Kind);
            AssertPoint(1000, 500, grips[0].Point);
            Assert.Equal(CadGripKind.ClipFlip, grips[1].Kind);
            AssertPoint(1050, 520, grips[1].Point);
            Assert.Equal(new[] { 2, 3, 4, 5 }, grips.Skip(2).Select(g => g.Index));
            Assert.All(grips.Skip(2), g => Assert.Equal(CadGripKind.ClipVertex, g.Kind));
            AssertPoint(1020, 520, grips[2].Point);
            AssertPoint(1080, 520, grips[3].Point);
            AssertPoint(1080, 570, grips[4].Point);
            AssertPoint(1020, 570, grips[5].Point);
        }

        [Fact]
        public void TheArrow_PointsAcrossTheFirstSide_TowardsWhatTheClipHides()
        {
            CadDocument document = Clipped(out CadCommandSession session, out string handle);

            // Outside mode hides what lies beyond the first side, the bottom of the rectangle: the arrow points down.
            Assert.Equal(0.0, Grips(document, handle)[1].Direction.X, 9);
            Assert.Equal(-1.0, Grips(document, handle)[1].Direction.Y, 9);

            Assert.True(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);

            // Inside mode hides what the rectangle holds: it points up.
            Assert.Equal(1.0, Grips(document, handle)[1].Direction.Y, 9);
        }

        [Fact]
        public void AReferenceNotClipped_HasItsInsertionGripAlone()
        {
            CadDocument document = Clipped(out CadCommandSession session, out string handle);
            Run(session, "XCLIP", new[] { handle }, Enter, "Delete");

            IReadOnlyList<CadGrip> grips = Grips(document, handle);

            Assert.Single(grips);
            Assert.Equal(CadGripKind.Insertion, grips[0].Kind);
        }

        [Fact]
        public void APolygonClip_HasTheArrowAtTheMiddleOfItsFirstSide_AndAGripAtEachVertex()
        {
            CadDocument document = Clipped(
                out _,
                out string handle,
                1.0,
                0.0,
                P(1010, 510),
                P(1090, 530),
                P(1050, 590)
            );

            IReadOnlyList<CadGrip> grips = Grips(document, handle);

            Assert.Equal(5, grips.Count);
            AssertPoint(1050, 520, grips[1].Point);
            AssertPoint(1010, 510, grips[2].Point);
            AssertPoint(1090, 530, grips[3].Point);
            AssertPoint(1050, 590, grips[4].Point);
        }

        [Fact]
        public void TheGrips_GoWithTheClip_OfAReferenceScaledTurnedAndMoved()
        {
            CadDocument document = Clipped(out _, out string handle, 2.0, 30.0);
            Reference(document, handle).TransformBy(GeoTransform3.Translation(new GeoVector3(100, -40, 0)));

            IReadOnlyList<CadGrip> grips = Grips(document, handle);

            // The rectangle (1020, 520) to (1080, 570) as it stood when it was clipped, carried with the block.
            AssertPoint(1120, 480, grips[2].Point, 4);
            AssertPoint(1180, 480, grips[3].Point, 4);
            AssertPoint(1180, 530, grips[4].Point, 4);
            AssertPoint(1120, 530, grips[5].Point, 4);
            AssertPoint(1150, 480, grips[1].Point, 4);
        }

        [Fact]
        public void ACornerDragged_MovesThatCornerOfTheRectangle_TheOppositeOneStays_InOneUndoStep()
        {
            CadDocument document = Clipped(out _, out string handle);
            string before = document.Database.ToText();

            CadEditResult result = document.GripDrag(handle, 2, P(1010, 510));

            Assert.True(result.Succeeded);
            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(2, points.Count);
            AssertPoint(1010, 510, points[0]);
            AssertPoint(1080, 570, points[1]);
            Assert.Equal("GRIP", document.NextUndo.Name);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void ACornerOnTheOtherSide_MovesItsOwnTwoSides_AsAutoCadDoes()
        {
            CadDocument document = Clipped(out _, out string handle);

            // Corner 3 is the lower right: it takes the right side and the bottom one, (+10, 0) here.
            Assert.True(document.GripDrag(handle, 3, P(1090, 520)).Succeeded);
            Assert.True(document.GripDrag(handle, 2, P(1010, 510)).Succeeded);

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            AssertPoint(1010, 510, points[0]);
            AssertPoint(1090, 570, points[1]);
        }

        [Fact]
        public void ACornerDraggedPastTheOppositeOne_KeepsTheRectangleWithItsCornersInOrder()
        {
            CadDocument document = Clipped(out _, out string handle);

            Assert.True(document.GripDrag(handle, 2, P(1100, 600)).Succeeded);

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            AssertPoint(1080, 570, points[0]);
            AssertPoint(1100, 600, points[1]);
        }

        [Fact]
        public void ACornerDraggedOntoASideOfTheOppositeOne_GivesNoClip_AsAutoCadSaysOfZeroArea()
        {
            CadDocument document = Clipped(out _, out string handle);
            string before = document.Database.ToText();

            CadEditResult result = document.GripDrag(handle, 2, P(1080, 500));

            Assert.False(result.Succeeded);
            Assert.Equal("Boundaries with zero area are not acceptable.", result.Message);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void AVertexOfAPolygonDragged_MovesThatVertexAlone()
        {
            CadDocument document = Clipped(
                out _,
                out string handle,
                1.0,
                0.0,
                P(1010, 510),
                P(1090, 530),
                P(1050, 590)
            );

            Assert.True(document.GripDrag(handle, 4, P(1055, 585)).Succeeded);

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(3, points.Count);
            AssertPoint(1010, 510, points[0]);
            AssertPoint(1090, 530, points[1]);
            AssertPoint(1055, 585, points[2]);
        }

        [Fact]
        public void AVertexDragged_OfAReferenceScaledTurnedAndMoved_LandsWhereTheCursorIs()
        {
            CadDocument document = Clipped(out _, out string handle, 2.0, 30.0);
            Reference(document, handle).TransformBy(GeoTransform3.Translation(new GeoVector3(100, -40, 0)));

            Assert.True(document.GripDrag(handle, 2, P(1110, 470)).Succeeded);

            IReadOnlyList<CadGrip> grips = Grips(document, handle);
            AssertPoint(1110, 470, grips[2].Point, 4);
            AssertPoint(1180, 530, grips[4].Point, 4);

            // The clip is still kept against the block: the reference moved again takes the new corner with it.
            Reference(document, handle).TransformBy(GeoTransform3.Translation(new GeoVector3(-100, 40, 0)));
            AssertPoint(1010, 510, Grips(document, handle)[2].Point, 4);
        }

        [Fact]
        public void TheArrowClicked_TurnsTheClipInsideOut_InOneUndoStep_AndClickedAgainBack()
        {
            CadDocument document = Clipped(out _, out string handle);
            string before = document.Database.ToText();

            CadEditResult flipped = CadGrips.ClickClipFlip(document, handle, 1);

            Assert.True(flipped.Succeeded);
            IReadOnlyList<GeoPoint2> ring = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(11, ring.Count);
            Assert.True(CadXClips.TryUninvert(ring, out IReadOnlyList<GeoPoint2> polygon));
            Assert.Equal(4, polygon.Count);
            AssertPoint(1020, 520, polygon[0]);
            AssertPoint(1080, 570, polygon[2]);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.Equal(6, Grips(document, handle).Count);
            AssertPoint(1050, 520, Grips(document, handle)[1].Point);

            Assert.True(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);

            IReadOnlyList<GeoPoint2> back = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(2, back.Count);
            AssertPoint(1020, 520, back[0]);
            AssertPoint(1080, 570, back[1]);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void TheArrowOnAPolygon_KeepsTheKeyholeOfTheBox_AndTheVerticesAreThePolygons()
        {
            CadDocument document = Clipped(
                out _,
                out string handle,
                1.0,
                0.0,
                P(1010, 510),
                P(1090, 530),
                P(1050, 590)
            );

            Assert.True(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);

            IReadOnlyList<GeoPoint2> ring = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(10, ring.Count);
            IReadOnlyList<CadGrip> grips = Grips(document, handle);
            Assert.Equal(5, grips.Count);
            AssertPoint(1010, 510, grips[2].Point);
            AssertPoint(1090, 530, grips[3].Point);
            AssertPoint(1050, 590, grips[4].Point);
        }

        [Fact]
        public void ACornerOfAnInvertedRectangleDragged_KeepsTheClipInverted()
        {
            CadDocument document = Clipped(out _, out string handle);
            Assert.True(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);

            Assert.True(document.GripDrag(handle, 2, P(1010, 510)).Succeeded);

            IReadOnlyList<GeoPoint2> ring = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(11, ring.Count);
            Assert.True(CadXClips.TryUninvert(ring, out IReadOnlyList<GeoPoint2> polygon));
            AssertPoint(1010, 510, polygon[0]);
            AssertPoint(1080, 510, polygon[1]);
            AssertPoint(1080, 570, polygon[2]);
            AssertPoint(1010, 570, polygon[3]);
            Assert.Equal(6, Grips(document, handle).Count);
            AssertPoint(1010, 510, Grips(document, handle)[2].Point);
        }

        [Fact]
        public void AnArrowNotOnAClippedReference_IsRefused()
        {
            CadDocument document = Clipped(out CadCommandSession session, out string handle);
            Run(session, "XCLIP", new[] { handle }, Enter, "Delete");

            Assert.False(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);
            Assert.False(CadGrips.ClickClipFlip(document, handle, 0).Succeeded);
        }

        [Fact]
        public void ExplodeOfAClippedReference_BreaksTheWholeBlock_AndTheClipIsLeftBehind()
        {
            // AutoCAD 2021 (probe GHXE1): EXPLODE of a block clipped to a rectangle, a polygon, inside out or switched off gives
            // every line, circle and polyline of the block, whole; the clip is not applied and is not kept.
            CadDocument document = Clipped(out CadCommandSession session, out string handle);

            Run(session, "EXPLODE", new[] { handle }, Enter);

            Assert.Null(document.FindEntity(handle));
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Equal(1040.0, circle.Center.X, 9);
            Assert.Equal(530.0, circle.Center.Y, 9);
            DxfPolyline square = All<DxfPolyline>(document.ModelSpace).Single();
            Assert.Equal(990.0, square.GetPoint2dAt(0).X, 9);
            Assert.Equal(480.0, square.GetPoint2dAt(0).Y, 9);
            Assert.Equal(1090.0, square.GetPoint2dAt(2).X, 9);
        }

        [Fact]
        public void ExplodeOfAClippedReferenceInsideOut_BreaksTheWholeBlockToo()
        {
            CadDocument document = Clipped(out CadCommandSession session, out string handle);
            Assert.True(CadGrips.ClickClipFlip(document, handle, 1).Succeeded);

            Run(session, "EXPLODE", new[] { handle }, Enter);

            Assert.Null(document.FindEntity(handle));
            Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void TheKeyholeOfAnInvertedClip_IsTakenBackToItsPolygon_ForEveryEdgeAndTurn()
        {
            var random = new Random(20261112);
            var box = new[] { new GeoPoint2(1000, 500), new GeoPoint2(1100, 600) };

            for (int trial = 0; trial < 400; trial++)
            {
                int n = 3 + random.Next(6);
                bool clockwise = random.Next(2) == 0;
                var polygon = new List<GeoPoint2>();
                double cx = 1050 + random.NextDouble() * 20 - 10;
                double cy = 550 + random.NextDouble() * 20 - 10;

                // A star-shaped polygon round (cx, cy): simple, by the angle of each vertex.
                for (int i = 0; i < n; i++)
                {
                    double angle = (clockwise ? -1.0 : 1.0) * 2.0 * Math.PI * (i + random.NextDouble() * 0.6) / n;
                    double radius = 5.0 + random.NextDouble() * 45.0 * (trial % 4 == 0 ? 1.4 : 1.0);
                    polygon.Add(new GeoPoint2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
                }

                IReadOnlyList<GeoPoint2> ring = CadXClips.Inverted(polygon, box[0], box[1]);

                Assert.True(CadXClips.TryUninvert(ring, out IReadOnlyList<GeoPoint2> back), "trial " + trial);
                Assert.Equal(polygon.Count, back.Count);

                for (int i = 0; i < polygon.Count; i++)
                {
                    Assert.Equal(polygon[i].X, back[i].X, 9);
                    Assert.Equal(polygon[i].Y, back[i].Y, 9);
                }
            }
        }

        [Fact]
        public void AnInvertedKeyhole_IsFoundWhereverTheRingStarts()
        {
            IReadOnlyList<GeoPoint2> ring = CadXClips.Inverted(
                new[] { P(1020, 520), P(1080, 570) },
                P(990, 480),
                P(1090, 580)
            );

            for (int shift = 0; shift < ring.Count; shift++)
            {
                var turned = ring.Skip(shift).Concat(ring.Take(shift)).ToList();

                Assert.True(CadXClips.TryUninvert(turned, out IReadOnlyList<GeoPoint2> polygon), "shift " + shift);
                Assert.Equal(4, polygon.Count);
                Assert.Equal(
                    new[] { 1020.0, 1080.0, 1080.0, 1020.0 }.OrderBy(v => v),
                    polygon.Select(p => Math.Round(p.X, 6)).OrderBy(v => v)
                );
            }
        }

        [Fact]
        public void APolygonThatIsNoKeyhole_IsNotTakenForOne()
        {
            var square = new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
            var eleven = new List<GeoPoint2>();

            for (int i = 0; i < 11; i++)
            {
                eleven.Add(P(50 * Math.Cos(i * 2.0 * Math.PI / 11.0), 50 * Math.Sin(i * 2.0 * Math.PI / 11.0)));
            }

            Assert.False(CadXClips.TryUninvert(square, out _));
            Assert.False(CadXClips.TryUninvert(eleven, out _));
            Assert.False(CadXClips.TryUninvert(new GeoPoint2[0], out _));
        }
    }
}
