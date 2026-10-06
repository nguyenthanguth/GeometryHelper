using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Whether an ellipse holds a segment, a circle or another ellipse whole. As the circle's: a segment is held when both
    /// its ends are, the region being convex (Containment2.cs:651-655), and an end within the point tolerance outside the
    /// rim still counts as held, as a point does (Containment2.cs:148-151). A circle is held when its centre is inside and
    /// its centre's gap to the rim is at least its radius less the point tolerance, so one touching the rim from inside is
    /// held (Containment2.cs:635-638 for two circles). An ellipse is held when no point of its rim lies more than the
    /// point tolerance outside this one (the coder's rule, accepted by the lead), so one touching the rim from inside is
    /// held as a circle touching a circle is; the points asked must be its rim's, since the centre of a larger ellipse
    /// round this one can lie inside this one. Every case sits clear of the tolerance: overhangs of 0.0005 and 0.002
    /// against 0.001.
    /// </summary>
    public class Ellipse2RegionContainmentTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoEllipse2 Upright() => Ellipse2Oracle.Upright(300, 100);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        #region Segments

        [Fact]
        public void ASegmentWithBothEndsInside_IsHeld_AndOneWithAnEndOutside_IsNot()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameLine(ellipse, -250, -30, 200, 60), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameLine(ellipse, -250, -30, 310, 0), Strict));
        }

        [Fact]
        public void ASegmentEndingHalfAThousandthOutside_IsHeld_AndTwoThousandthsOutside_IsNot()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(new GeoLine2(ellipse.Center, Ellipse2Oracle.OffRim(ellipse, 2.2, 0.0005)), Strict));
            Assert.False(ellipse.Contains(new GeoLine2(ellipse.Center, Ellipse2Oracle.OffRim(ellipse, 2.2, 0.002)), Strict));
        }

        [Fact]
        public void AChordOfTheRim_IsHeld()
        {
            // From the rim at t = 0.5 to the rim at t = 3: both ends on it, every point between inside.
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(new GeoLine2(Ellipse2Oracle.Rim(ellipse, 0.5), Ellipse2Oracle.Rim(ellipse, 3.0)), Strict));
        }

        #endregion

        #region Circles

        [Fact]
        public void ACircleWellInside_IsHeld_AndOneApartOrRoundTheEllipse_IsNot()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 50, 0, 30), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 250, 50), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 500), Strict));
        }

        [Fact]
        public void ACircleWhoseCentreIsInsideButWhichOverhangsTheRim_IsNotHeld()
        {
            // About (0, 50) with radius 80: the centre is 50 from the top.
            GeoEllipse2 ellipse = Tilted();

            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 50, 80), Strict));
        }

        [Fact]
        public void ACircleTouchingTheTopFromInside_IsHeld()
        {
            // About (0, 50) with radius 50: it reaches the top (0, 100), and curves harder than the rim there (50 against
            // 900), so it stays inside everywhere else. Its centre's gap is exactly its radius.
            GeoEllipse2 ellipse = Upright();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 50, 50), Strict));
        }

        [Fact]
        public void ACircleOverhangingByHalfAThousandth_IsHeld_AndByTwoThousandths_IsNot()
        {
            // Moved up from touching: its centre's gap falls to 49.9995 and 49.998 against the radius 50.
            GeoEllipse2 ellipse = Upright();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 50.0005, 50), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameCircle(ellipse, 0, 50.002, 50), Strict));
        }

        #endregion

        #region Ellipses

        [Fact]
        public void ASmallerEllipseInside_IsHeld_ConcentricOrNot()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.2, 200, 50), Strict));
            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 100, 20, 0.3, 80, 30), Strict));
            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.0, 250, 0.5), Strict));
        }

        [Fact]
        public void AnEllipseRoundThisOne_IsNotHeld_ThoughItsCentreIsInside()
        {
            // 400 by 300 about the same centre: no crossing, and its centre is this one's, but none of its rim is inside.
            GeoEllipse2 ellipse = Tilted();

            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.0, 400, 300), Strict));
        }

        [Fact]
        public void AnEllipseAcrossTheRim_OrApart_IsNotHeld()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, Math.PI / 2, 300, 100), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 100, 0, 0.0, 300, 100), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 1000, 0, 0.3, 200, 50), Strict));
        }

        [Fact]
        public void AnEllipseTouchingTheTopFromInside_IsHeld_AndOneOverhangingByTwoThousandths_IsNot()
        {
            // 150 by 80 about (0, 20): its top is this one's, where it curves harder (radius 281 against 900), and the rest
            // of it is inside. Moved up 0.0005 it overhangs by that, within the tolerance; moved up 0.002, beyond it.
            GeoEllipse2 ellipse = Upright();

            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 20, 0, 150, 80), Strict));
            Assert.True(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 20.0005, 0, 150, 80), Strict));
            Assert.False(ellipse.Contains(Ellipse2Oracle.FrameEllipse(ellipse, 0, 20.002, 0, 150, 80), Strict));
        }

        [Fact]
        public void TheSameEllipse_IsHeld()
        {
            // As a circle holds itself (Containment2.cs:635-638): no crossings, and its rim is on this rim.
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.Contains(ellipse.Clone(), Strict));
        }

        #endregion
    }
}
