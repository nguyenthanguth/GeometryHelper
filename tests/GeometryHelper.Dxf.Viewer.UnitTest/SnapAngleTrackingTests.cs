using System;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Object snap tracking following the snap as AutoCAD's does: its level and upright lines from a point acquired turned by
    /// SNAPANG, or on the two axes of the isometric plane, the polar lines from the base point turned with them, and
    /// PolarSnap stepping along a tracking line from the point acquired.
    /// </summary>
    public class SnapAngleTrackingTests
    {
        private static readonly double Cos30 = Math.Sqrt(3.0) / 2.0;

        private static PickIndex Empty()
        {
            CadDocument blank = Blank();
            return blank.GetPickIndex(blank.ModelSpace);
        }

        /// <summary>A point moved a distance square off a line running at an angle in degrees, to its left.</summary>
        private static GeoPoint2 Off(GeoPoint2 point, double degrees, double distance)
        {
            double a = degrees * Math.PI / 180.0;
            return P(point.X - distance * Math.Sin(a), point.Y + distance * Math.Cos(a));
        }

        private static ObjectSnap Tracking() =>
            new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint, Tracking = true };

        [Fact]
        public void TrackingLines_TurnWithTheSnapAngle()
        {
            ObjectSnap snap = Tracking();
            snap.SnapAngle = Math.PI / 6.0;
            snap.AcquireTrackPoint(P(0, 0));

            // 10 along 30 degrees, the cursor 0.3 off it.
            CadSnapResult along = snap.Snap(Empty(), Off(P(10 * Cos30, 5), 30, 0.3), 1.0);
            Assert.Equal(CadSnapKind.Tracking, along.Kind);
            Near(P(10 * Cos30, 5), along.Point, 1E-9);

            // Upright to the turned axis: 120 degrees.
            CadSnapResult up = snap.Snap(Empty(), Off(P(-5, 10 * Cos30), 120, -0.2), 1.0);
            Assert.Equal(CadSnapKind.Tracking, up.Kind);
            Near(P(-5, 10 * Cos30), up.Point, 1E-9);

            // The level line of the world is no tracking line now.
            Assert.Equal(CadSnapKind.None, snap.Snap(Empty(), P(20, 0.3), 1.0).Kind);
        }

        [Fact]
        public void TrackingLines_RunOnTheAxesOfTheIsoplane()
        {
            ObjectSnap snap = Tracking();
            snap.Isoplane = CadIsoplane.Top;
            snap.AcquireTrackPoint(P(0, 0));

            // On the top plane: 30 and 150 degrees, both ways.
            Near(P(-10 * Cos30, 5), snap.Snap(Empty(), Off(P(-10 * Cos30, 5), 150, 0.3), 1.0).Point, 1E-9);
            Near(P(10 * Cos30, -5), snap.Snap(Empty(), Off(P(10 * Cos30, -5), 330, 0.3), 1.0).Point, 1E-9);
            Assert.Equal(CadSnapKind.None, snap.Snap(Empty(), P(0.3, 10), 1.0).Kind);

            // On the left plane: 90 and 150 degrees.
            snap.Isoplane = CadIsoplane.Left;
            Assert.Equal(CadSnapKind.Tracking, snap.Snap(Empty(), P(0.3, 10), 1.0).Kind);
            Assert.Equal(CadSnapKind.None, snap.Snap(Empty(), P(10 * Cos30, 5.3), 1.0).Kind);
        }

        [Fact]
        public void APolarLineFromTheBasePoint_TurnsWithTheSnapAngle_WhereItCrossesATrackingLine()
        {
            ObjectSnap snap = Tracking();
            snap.SnapAngle = Math.PI / 6.0;
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 2.0;
            snap.AcquireTrackPoint(P(0, 20));

            // The polar line at 30 degrees from (0, 0) meets the line at 300 degrees from (0, 20) at (8.6603, 5).
            CadSnapResult crossing = snap.Snap(Empty(), P(8.8, 5.1), 1.0, P(0, 0));
            Assert.Equal(CadSnapKind.Tracking, crossing.Kind);
            Near(P(10 * Cos30, 5), crossing.Point, 1E-9);
        }

        [Fact]
        public void PolarSnap_StepsAlongATrackingLineFromThePointAcquired()
        {
            ObjectSnap snap = Tracking();
            snap.GridSnap = true;
            snap.PolarSnap = true;
            snap.PolarDistance = 25;
            snap.AcquireTrackPoint(P(10, 20));

            // 37 along the level line goes back to 25, 40 on to 50.
            CadSnapResult back = snap.Snap(Empty(), P(47, 20.3), 1.0);
            Assert.Equal(CadSnapKind.Tracking, back.Kind);
            Near(P(35, 20), back.Point, 1E-9);
            Near(P(60, 20), snap.Snap(Empty(), P(50, 19.7), 1.0).Point, 1E-9);

            // Grid snap of the rectangular kind leaves the line's foot as it is.
            snap.PolarSnap = false;
            Near(P(47, 20), snap.Snap(Empty(), P(47, 20.3), 1.0).Point, 1E-9);
        }
    }
}
