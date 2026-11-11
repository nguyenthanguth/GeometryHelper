using System;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PolarSnap, AutoCAD's SNAPTYPE 1: with SNAP and POLAR on, the cursor held on a polar angle from the base point is put a
    /// whole number of POLARDIST from it, the snap X spacing where POLARDIST is nought; the grid is not snapped to, and off a
    /// polar angle the cursor goes free.
    /// </summary>
    public class PolarSnapTests
    {
        private static PickIndex Empty()
        {
            CadDocument blank = Blank();
            return blank.GetPickIndex(blank.ModelSpace);
        }

        private static ObjectSnap PolarSnap(double distance) =>
            new ObjectSnap(Tight)
            {
                IsOn = false,
                GridSnap = true,
                Polar = true,
                PolarAngle = Math.PI / 4.0,
                PolarSnap = true,
                PolarDistance = distance,
            };

        [Fact]
        public void APolarDistanceOf25_PutsTheCursorHeldOnAPolarAngleAtWholeSteps()
        {
            ObjectSnap snap = PolarSnap(25);
            GeoPoint2 from = P(10, 20);

            // 37 along the level line goes back to 25, 40 on to 50.
            CadSnapResult level = snap.Snap(Empty(), P(47, 20.4), 2.0, from);
            Assert.Equal(CadSnapKind.Polar, level.Kind);
            Near(P(35, 20), level.Point, 1E-9);
            Near(P(60, 20), snap.Snap(Empty(), P(50, 19.6), 2.0, from).Point, 1E-9);

            // 74 along 45 degrees: three steps of 25.
            double d = 74.0 / Math.Sqrt(2.0);
            CadSnapResult slant = snap.Snap(Empty(), P(10 + d, 20 + d + 0.5), 2.0, from);
            Assert.Equal(CadSnapKind.Polar, slant.Kind);
            double s = 75.0 / Math.Sqrt(2.0);
            Near(P(10 + s, 20 + s), slant.Point, 1E-9);
        }

        [Fact]
        public void APolarDistanceOfNought_StepsByTheSnapXSpacing()
        {
            ObjectSnap snap = PolarSnap(0);
            snap.GridX = 4;
            snap.GridY = 100;

            // 20.15 along 45 degrees: five steps of 4, where the grid of 4 by 100 would have put it at (16, 0).
            double s = 20.0 / Math.Sqrt(2.0);
            Near(P(s, s), snap.Snap(Empty(), P(14.1, 14.4), 1.0, P(0, 0)).Point, 1E-9);
        }

        [Fact]
        public void OffAPolarAngle_OrWithNoBasePoint_TheCursorGoesFree_AndTheGridIsNotSnappedTo()
        {
            ObjectSnap snap = PolarSnap(25);

            CadSnapResult free = snap.Snap(Empty(), P(31, 13), 1.0, P(0, 0));
            Assert.Equal(CadSnapKind.None, free.Kind);
            Near(P(31, 13), free.Point, 1E-12);

            CadSnapResult first = snap.Snap(Empty(), P(31, 13), 1.0);
            Assert.Equal(CadSnapKind.None, first.Kind);
            Near(P(31, 13), first.Point, 1E-12);
        }

        [Fact]
        public void WithSnapOff_ThePolarAngleHoldsTheCursorWhereItIs()
        {
            ObjectSnap snap = PolarSnap(25);
            snap.GridSnap = false;

            CadSnapResult held = snap.Snap(Empty(), P(37, 0.4), 1.0, P(0, 0));
            Assert.Equal(CadSnapKind.Polar, held.Kind);
            Near(P(37, 0), held.Point, 1E-9);
        }
    }
}
