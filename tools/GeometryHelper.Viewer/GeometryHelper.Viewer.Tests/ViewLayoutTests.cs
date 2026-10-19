using System;
using System.Linq;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ViewLayoutTests
    {
        [Fact]
        public void ANewLayout_ShowsTheIsoAlone_AndHasTheFourStandardViewsReady()
        {
            var layout = new ViewLayout();

            Assert.Equal(1, layout.Count);
            Assert.Equal(new[] { 3 }, layout.Shown);
            Assert.Equal(3, layout.Active);
            Assert.Equal(4, layout.Slots.Count);

            ViewCamera plan = layout.Slots[0].Camera;
            ViewCamera front = layout.Slots[1].Camera;
            ViewCamera right = layout.Slots[2].Camera;
            ViewCamera iso = layout.Slots[3].Camera;

            Assert.True(plan.Orthographic);
            Assert.Equal(Math.PI / 2.0, plan.Elevation, 12);
            Assert.True(front.Orthographic);
            Assert.Equal(0.0, front.Elevation, 12);
            Assert.Equal(-Math.PI / 2.0, front.Azimuth, 12);
            Assert.True(right.Orthographic);
            Assert.Equal(0.0, right.Azimuth, 12);
            Assert.False(iso.Orthographic);

            // The window with one view is the window it always was: the iso, in the render mode the viewer starts in.
            Assert.Equal(RenderMode.ShadedEdges, layout.ActiveSlot.Mode);
            Assert.Same(iso, layout.ActiveSlot.Camera);
        }

        [Fact]
        public void FourViews_AreTwoRowsOfTwo_PlanAndFrontAboveRightAndIsoBelow()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);

            var cells = layout.Cells(1002.0, 802.0, 2.0);

            Assert.Equal(new[] { 0, 1, 2, 3 }, cells.Select(c => c.Slot));
            Assert.Equal(new[] { 0.0, 502.0, 0.0, 502.0 }, cells.Select(c => c.X));
            Assert.Equal(new[] { 0.0, 0.0, 402.0, 402.0 }, cells.Select(c => c.Y));
            Assert.All(cells, c => Assert.Equal(500.0, c.Width));
            Assert.All(cells, c => Assert.Equal(400.0, c.Height));
            Assert.Equal(1, layout.SlotAt(700.0, 100.0, 1002.0, 802.0));
            Assert.Equal(2, layout.SlotAt(100.0, 600.0, 1002.0, 802.0));
            Assert.Equal(3, layout.SlotAt(1001.0, 801.0, 1002.0, 802.0));

            // The gap between two views, and the outside, belong to none.
            Assert.Equal(-1, layout.SlotAt(501.0, 100.0, 1002.0, 802.0));
            Assert.Equal(-1, layout.SlotAt(100.0, 401.0, 1002.0, 802.0));
            Assert.Equal(-1, layout.SlotAt(-1.0, 0.0, 1002.0, 802.0));
            Assert.Equal(-1, layout.SlotAt(1002.0, 0.0, 1002.0, 802.0));
        }

        [Fact]
        public void TwoViews_StandSideBySide_AndOneTakesTheWholeArea()
        {
            var layout = new ViewLayout();
            layout.SetCount(2);

            var two = layout.Cells(1002.0, 800.0, 2.0);
            Assert.Equal(2, two.Count);
            Assert.Equal(500.0, two[0].Width);
            Assert.Equal(502.0, two[1].X);
            Assert.All(two, c => Assert.Equal(800.0, c.Height));

            layout.SetCount(1);
            var one = Assert.Single(layout.Cells(640.0, 480.0));
            Assert.Equal(0.0, one.X);
            Assert.Equal(640.0, one.Width);
            Assert.Equal(480.0, one.Height);
        }

        [Fact]
        public void TwoViews_AreThePlanAndTheActiveOne_AndThePlanAndTheFrontWhereThePlanIsActive()
        {
            var layout = new ViewLayout();

            layout.SetCount(2);
            Assert.Equal(new[] { 0, 3 }, layout.Shown);

            layout.SetCount(4);
            layout.Activate(2);
            layout.SetCount(2);
            Assert.Equal(new[] { 0, 2 }, layout.Shown);

            layout.Activate(0);
            Assert.Equal(new[] { 0, 2 }, layout.Shown);

            // Clicking from one of the two to the other does not change which two are shown.
            layout.SetCount(4);
            layout.Activate(0);
            layout.SetCount(2);
            Assert.Equal(new[] { 0, 1 }, layout.Shown);

            layout.SetCount(1);
            Assert.Equal(new[] { 0 }, layout.Shown);
        }

        [Fact]
        public void OnlyAViewThatIsShown_CanBeActivated_AndALayoutOfAnyOtherCount_IsRefused()
        {
            var layout = new ViewLayout();

            Assert.Throws<ArgumentOutOfRangeException>(() => layout.Activate(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetCount(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetCount(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetCount(5));

            layout.SetCount(4);
            layout.Activate(1);
            Assert.Equal(1, layout.Active);

            // Back to one: the view that was active is the one that stays, whichever it is.
            layout.SetCount(1);
            Assert.Equal(new[] { 1 }, layout.Shown);
            Assert.Same(layout.Slots[1], layout.ActiveSlot);
        }

        [Fact]
        public void EachViewKeepsItsOwnCameraAndRenderMode_WhateverTheLayoutIsDoing()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            layout.Slots[0].Mode = RenderMode.Wireframe;
            layout.Slots[1].Mode = RenderMode.Transparent;
            layout.Slots[2].Camera.Target = new Vec3(10, 20, 30);
            layout.Slots[2].Camera.Distance = 77.0;

            layout.Activate(0);
            layout.SetCount(1);
            layout.SetCount(4);

            Assert.Equal(RenderMode.Wireframe, layout.Slots[0].Mode);
            Assert.Equal(RenderMode.Transparent, layout.Slots[1].Mode);
            Assert.Equal(RenderMode.ShadedEdges, layout.Slots[3].Mode);
            Assert.Equal(new Vec3(10, 20, 30), layout.Slots[2].Camera.Target);
            Assert.Equal(77.0, layout.Slots[2].Camera.Distance);
        }

        [Fact]
        public void LinkedViews_FollowTheTargetAndTheScaleOfTheOneThatMoved_FromTheirOwnDirection()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            layout.Slots[0].Linked = true;
            layout.Slots[1].Linked = true;
            layout.Slots[2].Linked = true;

            ViewCamera moved = layout.Slots[0].Camera;
            moved.Target = new Vec3(100, 200, 300);
            moved.Distance = 55.5;
            double frontAzimuth = layout.Slots[1].Camera.Azimuth;

            var changed = layout.SyncLinked(0);

            Assert.Equal(new[] { 1, 2 }, changed);
            Assert.Equal(new Vec3(100, 200, 300), layout.Slots[1].Camera.Target);
            Assert.Equal(55.5, layout.Slots[2].Camera.Distance);

            // The direction each looks from is its own.
            Assert.Equal(frontAzimuth, layout.Slots[1].Camera.Azimuth);
            Assert.True(layout.Slots[1].Camera.Orthographic);

            // One that is not linked is left where it is, and nothing is moved twice.
            Assert.NotEqual(new Vec3(100, 200, 300), layout.Slots[3].Camera.Target);
            Assert.Empty(layout.SyncLinked(0));

            // The view that moved is not moved by itself, and a linked view that is moved takes the others with it.
            layout.Slots[2].Camera.Target = new Vec3(7, 8, 9);
            Assert.Equal(new[] { 0, 1 }, layout.SyncLinked(2));
            Assert.Equal(new Vec3(7, 8, 9), layout.Slots[0].Camera.Target);
        }

        [Fact]
        public void AViewThatIsNotLinked_MovesAlone_AndTakesNoneWithIt()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            layout.Slots[1].Linked = true;
            layout.Slots[2].Linked = true;
            layout.Slots[0].Camera.Target = new Vec3(100, 200, 300);

            Assert.Empty(layout.SyncLinked(0));
            Assert.NotEqual(new Vec3(100, 200, 300), layout.Slots[1].Camera.Target);
            Assert.NotEqual(new Vec3(100, 200, 300), layout.Slots[2].Camera.Target);
        }

        [Fact]
        public void LinkedViews_AreOnlyMovedWhereTheyAreShown()
        {
            var layout = new ViewLayout();
            layout.SetCount(2);
            layout.Slots[0].Linked = true;
            layout.Slots[1].Linked = true;
            layout.Slots[3].Linked = true;
            layout.Slots[0].Camera.Target = new Vec3(1, 2, 3);

            Assert.Equal(new[] { 3 }, layout.SyncLinked(0));
            Assert.NotEqual(new Vec3(1, 2, 3), layout.Slots[1].Camera.Target);
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.SyncLinked(4));
        }

        [Fact]
        public void ANewScene_HasEveryViewFittedAgain_TheOnesNotShownToo_WhenTheyAreShown()
        {
            var layout = new ViewLayout();
            Assert.Equal(new[] { 3 }, layout.UnfittedShown());

            layout.Slots[3].Fitted = true;
            Assert.Empty(layout.UnfittedShown());

            // Four views: the three that were never shown have still to be fitted, and are.
            layout.SetCount(4);
            Assert.Equal(new[] { 0, 1, 2 }, layout.UnfittedShown());
            foreach (int slot in layout.UnfittedShown())
            {
                layout.Slots[slot].Fitted = true;
            }

            Assert.Empty(layout.UnfittedShown());

            // One view again, and a new scene: the three that are not shown lose their fit too, so that they are fitted to it when they are shown, not left looking at the scene before.
            layout.SetCount(1);
            layout.ClearFitted();
            Assert.Equal(new[] { 3 }, layout.UnfittedShown());
            layout.Slots[3].Fitted = true;
            layout.SetCount(4);
            Assert.Equal(new[] { 0, 1, 2 }, layout.UnfittedShown());
            Assert.All(layout.Slots, s => Assert.Equal(s.Fitted, s == layout.Slots[3]));
        }
        [Fact]
        public void ALayout_IsRememberedAndPutBack_ByItsState()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            layout.Activate(2);
            layout.Slots[0].Mode = RenderMode.Wireframe;
            layout.Slots[3].Mode = RenderMode.Shaded;
            layout.Slots[1].Linked = true;
            layout.SetCount(2);

            ViewLayoutState state = layout.Capture();
            var again = new ViewLayout();
            again.Restore(state);

            Assert.Equal(2, again.Count);
            Assert.Equal(new[] { 0, 2 }, again.Shown);
            Assert.Equal(2, again.Active);
            Assert.Equal(RenderMode.Wireframe, again.Slots[0].Mode);
            Assert.Equal(RenderMode.Shaded, again.Slots[3].Mode);
            Assert.True(again.Slots[1].Linked);
            Assert.False(again.Slots[2].Linked);
        }

        [Fact]
        public void AStateThatMakesNoSense_LeavesTheLayoutAsItWas_PartByPart()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);

            layout.Restore(null);
            layout.Restore(new ViewLayoutState { Count = 3, Active = 9, Modes = new[] { "Banana", "Wireframe" }, Linked = new[] { true } });

            Assert.Equal(4, layout.Count);
            Assert.Equal(3, layout.Active);
            Assert.Equal(RenderMode.ShadedEdges, layout.Slots[0].Mode);
            Assert.Equal(RenderMode.Wireframe, layout.Slots[1].Mode);
            Assert.True(layout.Slots[0].Linked);
            Assert.False(layout.Slots[1].Linked);

            // A number that is a mode in no way is not one: "7" parses as an enum, and is not defined.
            layout.Restore(new ViewLayoutState { Count = 4, Active = 3, Modes = new[] { "7" } });
            Assert.Equal(RenderMode.ShadedEdges, layout.Slots[0].Mode);
        }

        [Fact]
        public void TheCellsOfAnAreaNothingFitsIn_AreEmptyAndNotNegative()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);

            Assert.All(layout.Cells(1.0, 1.0, 2.0), c => Assert.True(c.Width >= 0.0 && c.Height >= 0.0));
            Assert.All(layout.Cells(-5.0, -5.0, 2.0), c => Assert.True(c.Width >= 0.0 && c.Height >= 0.0));
        }
    }
}
