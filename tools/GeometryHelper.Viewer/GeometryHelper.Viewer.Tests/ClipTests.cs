using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ClipTests
    {
        /// <summary>
        /// A box 30 by 20 by 10 with its low corner x along the x axis.
        /// </summary>
        private static GeoSolid3 Slab(double x)
            => GeoSolid3.Extrude(new GeoPolygon3(new GeoPoint3(x, 0, 0), new GeoPoint3(x + 30, 0, 0), new GeoPoint3(x + 30, 20, 0), new GeoPoint3(x, 20, 0)), new GeoVector3(0, 0, 10));

        private static ScreenMap MapWith(ViewFixture view, ClipSet clips)
            => new ScreenMap(view.Camera, view.Primitives.Bounds, ViewFixture.Width, ViewFixture.Height, clips);

        [Fact]
        public void APlane_KeepsTheSideItsNormalPointsTo_AndAPointOnItIsKept()
        {
            var plane = new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 3));

            Assert.Equal(1.0, plane.Normal.Length, 14);
            Assert.Equal(2.0, plane.SignedDistance(new Vec3(9, 9, 7)), 14);
            Assert.Equal(-5.0, plane.SignedDistance(new Vec3(9, 9, 0)), 14);

            var set = new ClipSet(new[] { plane });
            Assert.True(set.Contains(new Vec3(1, 1, 5)));
            Assert.True(set.Contains(new Vec3(1, 1, 5.5)));
            Assert.False(set.Contains(new Vec3(1, 1, 4.999)));
            Assert.True(set.WithSlack(0.01).Contains(new Vec3(1, 1, 4.999)));
        }

        [Fact]
        public void APlaneIsMovedByAnOffsetAlongItsNormal_FromItsAnchor_AndFlippedStaysWhereItWas()
        {
            var plane = new ClipPlane(new Vec3(1, 2, 3), new Vec3(0, 0, 1));

            ClipPlane moved = plane.WithOffset(10.0);
            ClipPlane flipped = moved.Flipped();

            Assert.Equal(new Vec3(1, 2, 13), moved.Point);
            Assert.Equal(new Vec3(1, 2, 3), moved.Anchor);
            Assert.Equal(new Vec3(1, 2, 13), flipped.Point);
            Assert.Equal(new Vec3(0, 0, -1), flipped.Normal);
            Assert.Equal(-10.0, flipped.Offset);

            // What was kept before is cut now, and the other way round.
            Assert.True(new ClipSet(new[] { moved }).Contains(new Vec3(0, 0, 20)));
            Assert.False(new ClipSet(new[] { flipped }).Contains(new Vec3(0, 0, 20)));
            Assert.True(new ClipSet(new[] { flipped }).Contains(new Vec3(0, 0, 0)));

            Assert.False(plane.WithEnabled(false).Enabled);
            Assert.Throws<ArgumentException>(() => new ClipPlane(default(Vec3), default(Vec3)));
            Assert.Throws<ArgumentException>(() => new ClipPlane(default(Vec3), new Vec3(1, 0, 0), double.NaN));
        }

        [Fact]
        public void TheHandleOfAPlane_IsThePointOfItNearestTheMiddleOfTheScene_AndMovesWithTheOffset()
        {
            var scene = new Bounds3(new Vec3(0, 0, 0), new Vec3(30, 20, 10));
            var plane = new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1));

            Assert.Equal(new Vec3(15, 10, 5), plane.HandleFor(scene));
            Assert.Equal(new Vec3(15, 10, 25), plane.WithOffset(20.0).HandleFor(scene));

            // A slanted plane: the handle is the foot of the perpendicular from the middle, and is on the plane.
            var slanted = new ClipPlane(new Vec3(0, 0, 0), new Vec3(1, 1, 0));
            Vec3 handle = slanted.HandleFor(scene);
            Assert.Equal(0.0, slanted.SignedDistance(handle), 12);
            Assert.Equal(0.0, Vec3.Dot(handle - scene.Center, new Vec3(1, -1, 0)), 12);

            // With no scene the handle is where the plane was set.
            Assert.Equal(new Vec3(0, 0, 5), plane.HandleFor(default(Bounds3)));
        }

        [Fact]
        public void TheSquareLaidOnAPlane_IsCutByTheOtherPlanesAlone_SoTheSixPlanesOfABoxAreItsSixFaces()
        {
            // Keep z >= 5 and x >= 10. A square 100 across laid on the first plane is cut by the second alone, to x from 10 to 50.
            var set = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)), new ClipPlane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)) });
            var square = new[] { new Vec3(-50, -50, 5), new Vec3(50, -50, 5), new Vec3(50, 50, 5), new Vec3(-50, 50, 5) };

            List<Vec3> cut = set.ClipPolygonExcept(square, 0);

            Assert.Equal(4, cut.Count);
            Assert.Equal(10.0, cut.Min(p => p.X), 12);
            Assert.Equal(50.0, cut.Max(p => p.X), 12);
            Assert.Equal(new[] { 5.0 }, cut.Select(p => p.Z).Distinct());
            Assert.Throws<ArgumentNullException>(() => set.ClipPolygonExcept(null, 0));

            // The face x = 0 of a box 30 by 20 by 10, a square 200 across laid on the plane of it, is cut by the other five to the face: 20 by 10, 60 round.
            ClipSet box = ClipSet.FromBox(new Bounds3(new Vec3(0, 0, 0), new Vec3(30, 20, 10)));
            List<Vec3> face = box.ClipPolygonExcept(new[] { new Vec3(0, -100, -100), new Vec3(0, 100, -100), new Vec3(0, 100, 100), new Vec3(0, -100, 100) }, 0);

            Assert.Equal(60.0, SectionBuilder.Length(new[] { new DrawPolyline(face.ToArray(), true) }), 9);
        }

        [Fact]
        public void AClosedLineCutWhereItBeginsAndEnds_IsOneRunAcrossItsStart_WhereItIsOnePiece()
        {
            // The square (0,0) (30,0) (30,20) (0,20), closed, starts at a corner that is kept; the plane keeps x <= 10. Its kept parts are the bottom from 0 to 10, and the
            // top from 10 to 0 with the left side down to the start: across the start they are one piece, 10 + 20 + 10 long.
            var set = new ClipSet(new[] { new ClipPlane(new Vec3(10, 0, 0), new Vec3(-1, 0, 0)) });
            var square = new DrawPolyline(new[] { new Vec3(0, 0, 0), new Vec3(30, 0, 0), new Vec3(30, 20, 0), new Vec3(0, 20, 0) }, true);
            var item = new ItemPrimitives();
            item.Polylines.Add(square);

            ItemPrimitives cut = set.Apply(item);

            DrawPolyline only = Assert.Single(cut.Polylines);
            Assert.False(only.Closed);
            Assert.Equal(4, only.Points.Length);
            Assert.Equal(new Vec3(10, 20, 0), only.Points[0]);
            Assert.Equal(new Vec3(10, 0, 0), only.Points[3]);
            Assert.Equal(40.0, SectionBuilder.Length(cut.Polylines), 12);

            // A line cut in two places that are not joined across the start stays two runs.
            var twice = new ClipSet(new[] { new ClipPlane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)), new ClipPlane(new Vec3(20, 0, 0), new Vec3(-1, 0, 0)) });
            Assert.Equal(2, twice.Apply(item).Polylines.Count);
        }
        [Fact]
        public void ASetHoldsSixPlanesAtMost_AndNeverChanges()
        {
            ClipSet set = ClipSet.Empty;

            for (int i = 0; i < ClipSet.MaxPlanes; i++)
            {
                set = set.Add(new ClipPlane(new Vec3(i, 0, 0), new Vec3(1, 0, 0)));
            }

            Assert.Equal(6, set.Count);
            Assert.Throws<InvalidOperationException>(() => set.Add(new ClipPlane(default(Vec3), new Vec3(0, 1, 0))));
            Assert.Throws<ArgumentException>(() => new ClipSet(Enumerable.Range(0, 7).Select(i => new ClipPlane(default(Vec3), new Vec3(1, 0, 0)))));
            Assert.Equal(0, ClipSet.Empty.Count);

            ClipSet fewer = set.Remove(2);
            Assert.Equal(5, fewer.Count);
            Assert.Equal(6, set.Count);
            Assert.Equal(new Vec3(3, 0, 0), fewer.Planes[2].Point);

            ClipSet replaced = set.Replace(0, new ClipPlane(new Vec3(0, 7, 0), new Vec3(0, 1, 0)));
            Assert.Equal(new Vec3(0, 7, 0), replaced.Planes[0].Point);
            Assert.Equal(new Vec3(0, 0, 0), set.Planes[0].Point);
            Assert.Throws<ArgumentOutOfRangeException>(() => set.Remove(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.Replace(-1, set.Planes[0]));
        }

        [Fact]
        public void APlaneThatIsOff_CutsNothing_AndASetWithNoneOn_IsNotActive()
        {
            var off = new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1), 0.0, false);
            var set = new ClipSet(new[] { off });

            Assert.False(set.IsActive);
            Assert.True(set.Contains(new Vec3(0, 0, -100)));
            Assert.True(set.ClipSegment(new Vec3(0, 0, -100), new Vec3(0, 0, 100), out Vec3 from, out Vec3 to));
            Assert.Equal(new Vec3(0, 0, -100), from);
            Assert.Equal(new Vec3(0, 0, 100), to);
            Assert.True(set.Add(new ClipPlane(default(Vec3), new Vec3(1, 0, 0))).IsActive);
        }

        [Fact]
        public void ASegmentIsCutToTheRegionOfAllThePlanes_AndOneOutsideIsRefused()
        {
            // The slab 10 <= x <= 20 and the half space y >= 0.
            var set = new ClipSet(new[]
            {
                new ClipPlane(new Vec3(10, 0, 0), new Vec3(1, 0, 0)),
                new ClipPlane(new Vec3(20, 0, 0), new Vec3(-1, 0, 0)),
                new ClipPlane(new Vec3(0, 0, 0), new Vec3(0, 1, 0)),
            });

            Assert.True(set.ClipSegment(new Vec3(0, 5, 0), new Vec3(30, 5, 0), out Vec3 from, out Vec3 to));
            ViewFixture.AssertNear(new Vec3(10, 5, 0), from, 1E-12);
            ViewFixture.AssertNear(new Vec3(20, 5, 0), to, 1E-12);

            // Whole inside: the same two points, to the bit.
            var a = new Vec3(12.123456789, 1.5, 3.25);
            var b = new Vec3(18.987654321, 9.5, -3.25);
            Assert.True(set.ClipSegment(a, b, out from, out to));
            Assert.Equal(a, from);
            Assert.Equal(b, to);

            Assert.False(set.ClipSegment(new Vec3(0, 5, 0), new Vec3(9, 5, 0), out _, out _));
            Assert.False(set.ClipSegment(new Vec3(12, -3, 0), new Vec3(18, -1, 0), out _, out _));

            // Cut by two planes each at one end, and missing the region between them: the segment runs across a corner outside it.
            Assert.False(set.ClipSegment(new Vec3(0, 1, 0), new Vec3(11, -5, 0), out _, out _));
        }

        [Fact]
        public void ATriangleIsCutToAConvexPiece_WhoseAreaIsWhatIsLeft()
        {
            var set = new ClipSet(new[] { new ClipPlane(new Vec3(5, 0, 0), new Vec3(1, 0, 0)) });

            // A right triangle with legs 10: the part with x >= 5 is the triangle (5, 0), (10, 0), (5, 5), 12.5 in area.
            List<Vec3> piece = set.ClipPolygon(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(0, 10, 0) });

            Assert.Equal(3, piece.Count);
            double area = 0.0;

            for (int i = 1; i + 1 < piece.Count; i++)
            {
                area += Vec3.Cross(piece[i] - piece[0], piece[i + 1] - piece[0]).Length * 0.5;
            }

            Assert.Equal(12.5, area, 12);
            Assert.Empty(set.ClipPolygon(new[] { new Vec3(0, 0, 0), new Vec3(4, 0, 0), new Vec3(0, 4, 0) }));
        }

        [Fact]
        public void AClipBoxOfASelection_KeepsTheSelectionWhole_AndCutsAwayTheItemsBeyondIt()
        {
            Primitives scene = Primitives.Build(new SceneWriter()
                .Add(SampleShapes.Box(30, 20, 10, false), name: "near")
                .Add(Slab(40), name: "far")
                .Add(new GeoLine3(new GeoPoint3(-50, 5, 5), new GeoPoint3(80, 5, 5)), name: "long")
                .Scene);

            // The box of the first item, with nothing added to it.
            ClipSet box = ClipSet.FromBox(scene.Items[0].Bounds);

            Assert.Equal(6, box.Count);
            Assert.Equal(1.0, box.Planes[0].Normal.Length, 14);

            ItemPrimitives near = box.Apply(scene.Items[0]);
            Assert.Equal(scene.Items[0].Triangles.Count, near.Triangles.Count);

            for (int i = 0; i < near.Triangles.Count; i++)
            {
                Assert.Equal(scene.Items[0].Triangles[i].A, near.Triangles[i].A);
                Assert.Equal(scene.Items[0].Triangles[i].B, near.Triangles[i].B);
                Assert.Equal(scene.Items[0].Triangles[i].C, near.Triangles[i].C);
            }

            Assert.Empty(box.Apply(scene.Items[1]).Triangles);

            // The line through everything is left as the piece inside the box: 30 long.
            ItemPrimitives line = box.Apply(scene.Items[2]);
            Assert.Equal(30.0, SectionBuilder.Length(line.Polylines), 10);

            // With a margin the box is larger on every side by it.
            ClipSet wide = ClipSet.FromBox(scene.Items[0].Bounds, 2.0);
            Assert.True(wide.Contains(new Vec3(-2, -2, -2)));
            Assert.True(wide.Contains(new Vec3(32, 22, 12)));
            Assert.False(wide.Contains(new Vec3(-2.001, 0, 0)));
            Assert.False(box.Contains(new Vec3(-0.001, 0, 0)));
            Assert.Throws<ArgumentException>(() => ClipSet.FromBox(default(Bounds3)));
            Assert.Throws<ArgumentException>(() => ClipSet.FromBox(scene.Items[0].Bounds, -1.0));
        }

        [Fact]
        public void WhatAPlaneCutsAway_IsNotPicked_ThePickGoesThroughToWhatIsBehind()
        {
            ViewFixture view = ViewFixture.Top(new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false)));
            (double x, double y) = view.Pixel(new Vec3(15, 10, 10));

            // Looking down, the top at z = 10 is hit first; with the top cut away (keep z <= 5), the bottom at z = 0 is.
            PickHit before = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y, null, true);
            ClipSet cut = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, -1)) });
            PickHit after = PickEngine.Pick(view.Primitives, view.Geometry, MapWith(view, cut), x, y, null, true);

            Assert.Equal(10.0, before.Point.Z, 9);
            Assert.Equal(0.0, after.Point.Z, 9);

            // Cut away all of it (keep z >= 20): nothing is picked.
            ClipSet all = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, 1)) });
            Assert.Null(PickEngine.Pick(view.Primitives, view.Geometry, MapWith(view, all), x, y, null, true));
        }

        [Fact]
        public void ASnapOnWhatAPlaneCutsAway_IsNotOffered_ButOneOnWhatIsLeftIs()
        {
            // Two segments one over the other, 10 and 0 up, both with an end at (20, 20) in plan.
            ViewFixture view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine3(new GeoPoint3(0, 20, 10), new GeoPoint3(20, 20, 10)), name: "high")
                .Add(new GeoLine3(new GeoPoint3(0, 20, 0), new GeoPoint3(20, 20, 0)), name: "low"));
            (double x, double y) = view.Pixel(new Vec3(20, 20, 0));

            SnapHit all = SnapEngine.Find(view.Geometry, view.Map, x, y, SnapKinds.End, SnapEngine.DefaultRadius, 1E-3, null);
            SnapHit cut = SnapEngine.Find(view.Geometry, MapWith(view, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, -1)) })), x, y, SnapKinds.End, SnapEngine.DefaultRadius, 1E-3, null);
            SnapHit none = SnapEngine.Find(view.Geometry, MapWith(view, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 50), new Vec3(0, 0, 1)) })), x, y, SnapKinds.All, SnapEngine.DefaultRadius, 1E-3, null);

            Assert.Equal(0, all.ItemIndex);
            Assert.Equal(10.0, all.Point.Z);
            Assert.Equal(0.0, cut.Point.Z);
            Assert.Equal(1, cut.ItemIndex);
            Assert.Null(none);
        }

        [Fact]
        public void ABoxSelect_JudgesWhatIsLeft_SoAnItemCutAwayWhollyIsNeverSelected_AndOneCutAwayInPartIsSelectedByItsVisiblePart()
        {
            ViewFixture view = ViewFixture.Top(new SceneWriter()
                .Add(SampleShapes.Box(30, 20, 10, false), name: "box")
                .Add(new GeoLine3(new GeoPoint3(100, 0, 50), new GeoPoint3(100, 10, 50)), name: "above"));
            (double x1, double y1) = view.Pixel(new Vec3(-5, -5, 0));
            (double x2, double y2) = view.Pixel(new Vec3(130, 30, 0));

            // Everything is in the window, with no plane.
            Assert.Equal(new[] { 0, 1 }, BoxSelect.Select(view.Primitives, view.Map, x1, y1, x2, y2, BoxMode.Window, null));

            // Keep z <= 20: the line at z = 50 is gone, and the box is whole.
            ClipSet low = new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 20), new Vec3(0, 0, -1)) });
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, MapWith(view, low), x1, y1, x2, y2, BoxMode.Window, null));
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, MapWith(view, low), x1, y1, x2, y2, BoxMode.Crossing, null));

            // Keep x >= 20 and the window takes the visible part of the box, which is inside, though the box is not; keep x >= 40 and it is gone.
            ClipSet right = new ClipSet(new[] { new ClipPlane(new Vec3(20, 0, 0), new Vec3(1, 0, 0)) });
            (double wx1, double wy1) = view.Pixel(new Vec3(19, -5, 0));
            (double wx2, double wy2) = view.Pixel(new Vec3(31, 30, 0));
            Assert.Empty(BoxSelect.Select(view.Primitives, view.Map, wx1, wy1, wx2, wy2, BoxMode.Window, null));
            Assert.Equal(new[] { 0 }, BoxSelect.Select(view.Primitives, MapWith(view, right), wx1, wy1, wx2, wy2, BoxMode.Window, null));

            ClipSet gone = new ClipSet(new[] { new ClipPlane(new Vec3(40, 0, 0), new Vec3(1, 0, 0)) });
            Assert.DoesNotContain(0, BoxSelect.Select(view.Primitives, MapWith(view, gone), x1, y1, x2, y2, BoxMode.Crossing, null));
        }

        [Fact]
        public void AMapWithNoPlaneOn_IsTheMapItWas_AndAPointCutAwayDoesNotProject()
        {
            ViewFixture view = ViewFixture.Top(new SceneWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10))));
            var off = new ClipSet(new[] { new ClipPlane(default(Vec3), new Vec3(0, 0, 1), 0.0, false) });

            Assert.Null(MapWith(view, off).Clip);
            Assert.Null(MapWith(view, null).Clip);

            ScreenMap cut = MapWith(view, new ClipSet(new[] { new ClipPlane(new Vec3(0, 0, 5), new Vec3(0, 0, 1)) }));

            Assert.True(cut.TryProject(new Vec3(5, 5, 6), out _, out _, out _));
            Assert.False(cut.TryProject(new Vec3(5, 5, 4), out double x, out _, out _));
            Assert.True(double.IsNaN(x));
            Assert.True(cut.IsClipped(new Vec3(5, 5, 4)));
            Assert.False(cut.IsClipped(new Vec3(5, 5, 5)));
            Assert.False(cut.IsInDepth(new Vec3(5, 5, 4)));
            Assert.True(view.Map.TryProject(new Vec3(5, 5, 4), out _, out _, out _));
        }

        [Fact]
        public void TheHandleOfAPlane_IsDraggedAlongItsNormal_ByTheMouseRay()
        {
            // A line up the z axis; a ray square to it passing the line at z = 7, 5 away.
            double t = ClipPlane.ParameterNearestRay(default(Vec3), new Vec3(0, 0, 1), new Vec3(10, 5, 7), new Vec3(-1, 0, 0));
            Assert.Equal(7.0, t, 12);

            // A slanted ray, down and across: it comes nearest the line at z = -4.
            Vec3 direction = new Vec3(-3, 0, -4).Normalized();
            Assert.Equal(-4.0, ClipPlane.ParameterNearestRay(default(Vec3), new Vec3(0, 0, 1), new Vec3(3, 2, 0), direction), 9);

            // A ray along the line gives no answer.
            Assert.True(double.IsNaN(ClipPlane.ParameterNearestRay(default(Vec3), new Vec3(0, 0, 1), new Vec3(1, 1, 0), new Vec3(0, 0, 1))));

            // Moving the mouse moves the plane by the difference of the two answers.
            double start = ClipPlane.ParameterNearestRay(new Vec3(1, 2, 3), new Vec3(0, 0, 1), new Vec3(10, 2, 9), new Vec3(-1, 0, 0));
            double later = ClipPlane.ParameterNearestRay(new Vec3(1, 2, 3), new Vec3(0, 0, 1), new Vec3(10, 2, 14), new Vec3(-1, 0, 0));
            Assert.Equal(5.0, later - start, 12);
        }
    }
}
