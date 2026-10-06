using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Whether an ellipse touches other shapes. As for the circle, touching is reading the ellipse as a region with the
    /// point tolerance as slack: a shape a gap off the rim smaller than the tolerance touches it (Collision2.cs:50-53 for
    /// two circles, 66-69 for a segment), a shape inside the ellipse touches it however far it is from the rim
    /// (Collision2.cs:66-69, 124-138), and so does a closed shape round the ellipse (Collision2.cs:82-89, 102-112, 714-735).
    /// A face is touched only where it has material: an ellipse inside one of its holes does not touch it, one round a hole
    /// does (Face2.cs:336-393). Each shape is placed above the top of the ellipse a gap of 0.0004 or 0.002 off, either
    /// side of the tolerance 0.001 by a clear margin (Ellipse2Oracle, "Shapes a gap above the top").
    /// </summary>
    public class Ellipse2CollisionTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private const double Within = 0.0004;

        private const double Beyond = 0.002;

        private static GeoEllipse2 Upright() => Ellipse2Oracle.Upright(300, 100);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        #region A gap above the top

        [Fact]
        public void ASegment_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.LineAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.LineAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void APolyline_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.PolylineAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.PolylineAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void APolygon_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.PolygonAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.PolygonAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void ARectangle_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.RectangleAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.RectangleAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void ATriangle_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.TriangleAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.TriangleAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void AFace_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(new GeoFace2(Ellipse2Oracle.PolygonAbove(ellipse, Within)), Strict));
            Assert.False(ellipse.CollidesWith(new GeoFace2(Ellipse2Oracle.PolygonAbove(ellipse, Beyond)), Strict));
        }

        [Fact]
        public void AnEdge_TouchesWithinTheTolerance_AndNotBeyondIt_StraightOrCurved()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 within = Ellipse2Oracle.LineAbove(ellipse, Within);
            GeoLine2 beyond = Ellipse2Oracle.LineAbove(ellipse, Beyond);

            Assert.True(ellipse.CollidesWith(new GeoEdge2(within.StartPoint, within.EndPoint), Strict));
            Assert.False(ellipse.CollidesWith(new GeoEdge2(beyond.StartPoint, beyond.EndPoint), Strict));
            Assert.True(ellipse.CollidesWith(new GeoEdge2(Ellipse2Oracle.ArcAbove(ellipse, Within)), Strict));
            Assert.False(ellipse.CollidesWith(new GeoEdge2(Ellipse2Oracle.ArcAbove(ellipse, Beyond)), Strict));
        }

        [Fact]
        public void ACircle_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.CircleAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.CircleAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void AnArc_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.ArcAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.ArcAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void ACurvedChain_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.ChainAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.ChainAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void ACurvedLoop_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.LoopAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.LoopAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void AnEllipse_TouchesWithinTheTolerance_AndNotBeyondIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.EllipseAbove(ellipse, Within), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.EllipseAbove(ellipse, Beyond), Strict));
        }

        [Fact]
        public void TwoEllipses_TouchOrNotTheSameAskedFromEither()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoEllipse2 within = Ellipse2Oracle.EllipseAbove(ellipse, Within);
            GeoEllipse2 beyond = Ellipse2Oracle.EllipseAbove(ellipse, Beyond);

            Assert.True(within.CollidesWith(ellipse, Strict));
            Assert.False(beyond.CollidesWith(ellipse, Strict));
        }

        [Fact]
        public void TheTipOfAThinEllipse_IsTouchedWithinTheTolerance_InDrawingUnits()
        {
            // 1 000 by 1, a segment across the line of the major axis 0.0004 or 0.002 beyond the tip. Read in the unit
            // frame both gaps would be under a millionth.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);

            Assert.True(thin.CollidesWith(Ellipse2Oracle.FrameLine(thin, 1000.0004, -3, 1000.0004, 3), Strict));
            Assert.False(thin.CollidesWith(Ellipse2Oracle.FrameLine(thin, 1000.002, -3, 1000.002, 3), Strict));
            Assert.True(thin.CollidesWith(Ellipse2Oracle.FrameCircle(thin, 1050.0004, 0, 50), Strict));
            Assert.False(thin.CollidesWith(Ellipse2Oracle.FrameCircle(thin, 1050.002, 0, 50), Strict));
        }

        #endregion

        #region Inside, round and across

        [Fact]
        public void ACurveInsideTheEllipse_TouchesIt()
        {
            // A segment, a polyline, an arc and a curved chain well inside the rim: the ellipse is a region.
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameLine(ellipse, -100, 0, 100, 20), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FramePolyline(ellipse, -100, 0, 0, 50, 100, 0), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameArc(ellipse, 0, 0, 60, 0.3, 2.0), Strict));
            Assert.True(ellipse.CollidesWith(new GeoPolylineArc2(Ellipse2Oracle.FramePoints(ellipse, -100, 0, 0, 30, 100, 0), new[] { 0.3, -0.3, 0.0 }), Strict));
        }

        [Fact]
        public void AClosedShapeInsideTheEllipse_OrRoundIt_TouchesIt()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2[] round = Ellipse2Oracle.FramePoints(ellipse, -1000, -400, 1000, -400, 0, 900);

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FramePolygon(ellipse, -50, -20, 50, -20, 0, 40), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 200, -400, 200), Strict));
            Assert.True(ellipse.CollidesWith(new GeoRectangle2(ellipse.Center, 20, 10, 0.0), Strict));
            Assert.True(ellipse.CollidesWith(new GeoRectangle2(ellipse.Center, 800, 800, 0.0), Strict));
            Assert.True(ellipse.CollidesWith(new GeoTriangle2(round[0], round[1], round[2]), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameCircle(ellipse, 50, 0, 30), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 500), Strict));
            Assert.True(ellipse.CollidesWith(new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, -400, -200, 400, -200, 400, 200, -400, 200), new[] { 0.2, 0.0, 0.2, 0.0 }), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.2, 200, 50), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.0, 400, 300), Strict));
        }

        [Fact]
        public void AShapeAcrossTheRim_TouchesIt()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameLine(ellipse, 400, 50, -400, 50), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameCircle(ellipse, 300, 0, 50), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, Math.PI / 2, 300, 100), Strict));
            Assert.True(ellipse.CollidesWith(Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, 0.0, Math.PI), Strict));
        }

        [Fact]
        public void AnEllipseInAHoleOfAFace_DoesNotTouchIt_ButOneRoundAHoleDoes()
        {
            // The hole of the first face is 400 by 160 about the centre, 60 clear of the rim all round: no material is
            // reached. The second face's hole, 100 by 40, lies inside the ellipse, whose rim crosses the material round it.
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 outline = Ellipse2Oracle.FramePolygon(ellipse, -2000, -2000, 2000, -2000, 2000, 2000, -2000, 2000);
            GeoPolygon2 bigHole = Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 160, -400, 160);
            GeoPolygon2 smallHole = Ellipse2Oracle.FramePolygon(ellipse, -50, -20, 50, -20, 50, 20, -50, 20);

            Assert.False(ellipse.CollidesWith(new GeoFace2(outline, new[] { bigHole }), Strict));
            Assert.True(ellipse.CollidesWith(new GeoFace2(outline, new[] { smallHole }), Strict));
        }

        [Fact]
        public void AnEllipseAcrossTheRimOfAHole_TouchesTheFace()
        {
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 outline = Ellipse2Oracle.FramePolygon(ellipse, -2000, -2000, 2000, -2000, 2000, 2000, -2000, 2000);
            GeoPolygon2 hole = Ellipse2Oracle.FramePolygon(ellipse, 250, -20, 350, -20, 350, 20, 250, 20);

            Assert.True(ellipse.CollidesWith(new GeoFace2(outline, new[] { hole }), Strict));
        }

        [Fact]
        public void ShapesWellApart_DoNotTouch()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.FrameLine(ellipse, 100, 250, 450, -50), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.FramePolygon(ellipse, 330, -40, 420, 40, 330, 120), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.FrameCircle(ellipse, 0, 250, 50), Strict));
            Assert.False(ellipse.CollidesWith(Ellipse2Oracle.FrameEllipse(ellipse, 1000, 0, 0.3, 200, 50), Strict));
        }

        #endregion
    }
}
