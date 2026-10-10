using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 18 and 19: where the rim of an ellipse in space crosses a plane, a segment or a ray, and whether it touches
    /// one, as <see cref="GeoCircle3"/> answers. The rim is what counts, not the disc (Arc3.Circle.cs:85-91): a segment
    /// through the middle of the disc touches nothing. A plane is crossed at the rim's points on the line the two planes
    /// share (Arc3.cs:137-145), in order along N × the plane's normal (Intersection3.cs:427, Arc3.cs:116); an ellipse lying
    /// in the plane crosses it nowhere and touches it (Arc3.cs:185-193, 208-211). A segment or ray keeps the rim points it
    /// reaches, in order along it (Arc3.cs:250-263, 308-321). TryIntersectWith is true exactly when there is a crossing
    /// (Arc3.cs:226-231). Every body is the 300 by 100 ellipse of <see cref="Ellipse3Oracle.Tilted()"/>, read in its frame
    /// (u along U, v along V, h along N); the answers are worked out by hand or built in.
    /// </summary>
    public class Ellipse3QueryPlaneLineTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>How near an answer built in exactly has to come: rounding at the size of a few hundred.</summary>
        private const double Close = 1E-9;

        /// <summary>How near a touching point has to come: a graze is found from a nearly double root.</summary>
        private const double Touch = 1E-6;

        /// <summary>The x where the line y = 50 of the frame meets the rim of 300 by 100: 300 √(1 - 0.25) = 259.8076.</summary>
        private static readonly double Across = 300 * Math.Sqrt(0.75);

        #region Planes

        [Fact]
        public void APlaneLeaningThroughTwoRimPointsCrossesTheRimThereAlongTheLineThePlanesShare()
        {
            // The plane holds the chord from the rim point at t = 0.7 to the one at t = 3.9 and leans 50 degrees from the
            // ellipse's plane about it. The two planes share the chord's line, which meets the rim at its two ends only.
            Specimen e = Tilted();
            GeoPoint3 first = e.Rim(0.7), second = e.Rim(3.9);
            GeoVector3 normal = LeanAbout(e, first.GetVectorTo(second), 50 * Math.PI / 180);
            var plane = new GeoPlane3(first, normal);
            GeoEllipse3 ellipse = e.Build();

            GeoPoint3[] crossings = ellipse.GetIntersections(plane, Tol);

            SamePointsInOrder(Along(e.N.CrossProduct(normal), first, second), crossings, Close);
            Assert.True(ellipse.CollidesWith(plane, Tol));
            Assert.True(ellipse.TryIntersectWith(plane, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);
        }

        [Fact]
        public void APlaneThroughALineTouchingTheRimGivesTheOnePointItTouches()
        {
            // The plane holds the tangent of the rim at t = 1.2 and leans 40 degrees about it, so it meets the ellipse's
            // plane in a line that touches the rim there and nowhere else.
            Specimen e = Tilted();
            GeoPoint3 touch = e.Rim(1.2);
            var plane = new GeoPlane3(touch, LeanAbout(e, Tangent(e, 1.2), 40 * Math.PI / 180));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(plane, Tol));

            Assert.True(point.DistanceTo(touch) <= Touch, $"{point} is {point.DistanceTo(touch):E2} from {touch}");
            Assert.True(e.Build().CollidesWith(plane, Tol));
        }

        [Fact]
        public void APlaneMissingTheRimByFiveThousandthsTouchesNothing()
        {
            // The same leaning plane moved 0.005 out from the rim, square to the tangent in the ellipse's plane: five
            // times the point tolerance, clear of every graze band.
            Specimen e = Tilted();
            GeoVector3 outward = Tangent(e, 1.2).CrossProduct(e.N);
            var plane = new GeoPlane3(e.Rim(1.2).Add(outward.Multiply(0.005)), LeanAbout(e, Tangent(e, 1.2), 40 * Math.PI / 180));

            Assert.Empty(e.Build().GetIntersections(plane, Tol));
            Assert.False(e.Build().CollidesWith(plane, Tol));
        }

        [Fact]
        public void APlaneParallelToTheEllipseFiveAwayCrossesNothingAndTouchesNothing()
        {
            Specimen e = Tilted();
            var plane = new GeoPlane3(e.At(10, 10, 5), e.N);

            Assert.Empty(e.Build().GetIntersections(plane, Tol));
            Assert.False(e.Build().CollidesWith(plane, Tol));
            Assert.False(e.Build().TryIntersectWith(plane, out GeoPoint3[] tried, Tol));
            Assert.NotNull(tried);
            Assert.Empty(tried);
        }

        [Fact]
        public void AnEllipseLyingInThePlaneCrossesItNowhereButTouchesIt()
        {
            // Its own plane, given through another of its points and with the normal turned over.
            Specimen e = Tilted();
            var plane = new GeoPlane3(e.At(100, 30, 0), e.N.Negate());

            Assert.Empty(e.Build().GetIntersections(plane, Tol));
            Assert.True(e.Build().CollidesWith(plane, Tol));
            Assert.False(e.Build().TryIntersectWith(plane, out GeoPoint3[] tried, Tol));
            Assert.Empty(tried);
        }

        [Fact]
        public void APlaneTiltedAboutTheMinorAxisSoTheRimStandsOffHalfAThousandthIsTheEllipsesOwn()
        {
            // Through the centre, tilted about V by θ with 300 sin θ = 0.0005: the ends of the major axis stand 0.0005 off
            // it, half the planar tolerance, so the ellipse lies in it (Arc3.cs:44-53, the reach being the major radius).
            Specimen e = Tilted();
            var plane = new GeoPlane3(e.Center, LeanAbout(e, e.V, Math.Asin(0.0005 / 300)));

            Assert.Empty(e.Build().GetIntersections(plane, Tol));
            Assert.True(e.Build().CollidesWith(plane, Tol));
        }

        [Fact]
        public void APlaneTiltedAboutTheMinorAxisSoTheRimStandsOffTwoThousandthsCrossesAtTheMinorEnds()
        {
            // The same tilt with 300 sin θ = 0.002: the rim stands 0.002 off at the ends of the major axis, twice the
            // planar tolerance, so the plane crosses it, along V through the centre, at (0, ±100). Read by the minor
            // radius the stand-off would be 100 sin θ = 0.00067 and the ellipse would wrongly lie in the plane. Planes
            // 6.7E-6 rad apart share a line found from a near-singular system, so the points are held to half the
            // tolerance only (a build of the coder's work in progress put them 7.2E-5 off).
            Specimen e = Tilted();
            GeoVector3 normal = LeanAbout(e, e.V, Math.Asin(0.002 / 300));
            var plane = new GeoPlane3(e.Center, normal);

            GeoPoint3[] crossings = e.Build().GetIntersections(plane, Tol);

            SamePointsInOrder(Along(e.N.CrossProduct(normal), e.At(0, 100, 0), e.At(0, -100, 0)), crossings, 0.5 * Tol.EqualPoint);
            Assert.True(e.Build().CollidesWith(plane, Tol));
        }

        #endregion

        #region Segments

        [Fact]
        public void ASegmentAcrossTheEllipseInItsPlaneCrossesTheRimAtBothEndsOfTheChordInOrderAlongIt()
        {
            // From (-400, 50) to (400, 50): the rim at (∓259.8, 50), as the 2D ellipse gives it.
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(-400, 50, 0), e.At(400, 50, 0));

            GeoPoint3[] crossings = e.Build().GetIntersections(line, Tol);

            SamePointsInOrder(new[] { e.At(-Across, 50, 0), e.At(Across, 50, 0) }, crossings, Close);
            Assert.True(e.Build().CollidesWith(line, Tol));
            Assert.True(e.Build().TryIntersectWith(line, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);
        }

        [Fact]
        public void ASegmentTurnedRoundGivesTheCrossingsTurnedRound()
        {
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(400, 50, 0), e.At(-400, 50, 0));

            SamePointsInOrder(new[] { e.At(Across, 50, 0), e.At(-Across, 50, 0) }, e.Build().GetIntersections(line, Tol), Close);
        }

        [Fact]
        public void ASegmentPiercingTheDiscInsideTheRimTouchesNothing()
        {
            // Straight through (30, -20), from 50 below to 50 above. The disc holds the point it passes, but the rim is
            // what a segment is asked about (Arc3.Circle.cs:85-91).
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(30, -20, -50), e.At(30, -20, 50));

            Assert.True(e.Build().Contains(e.At(30, -20, 0), Tol));
            Assert.Empty(e.Build().GetIntersections(line, Tol));
            Assert.False(e.Build().CollidesWith(line, Tol));
            Assert.False(e.Build().TryIntersectWith(line, out _, Tol));
        }

        [Fact]
        public void ASegmentPiercingThePlaneOnTheRimCrossesItThere()
        {
            // Through the rim point at t = 2, leaning along N + 0.3 U, 40 each way.
            Specimen e = Tilted();
            GeoPoint3 rim = e.Rim(2.0);
            GeoVector3 d = e.N.Add(e.U.Multiply(0.3)).Normalize();
            var line = new GeoLine3(rim.Add(d.Multiply(-40)), rim.Add(d.Multiply(40)));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(line, Tol));

            Assert.True(point.DistanceTo(rim) <= Close, $"{point} is {point.DistanceTo(rim):E2} from {rim}");
            Assert.True(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentThroughARimPointAlmostSquareToThePlaneCrossesItThere()
        {
            // 200 long through the end of the major axis, leaning 5E-4 rad from the normal towards U.
            Specimen e = Tilted();
            GeoPoint3 rim = e.Rim(0.0);
            GeoVector3 d = e.N.Multiply(Math.Cos(5E-4)).Add(e.U.Multiply(Math.Sin(5E-4)));
            var line = new GeoLine3(rim.Add(d.Multiply(-100)), rim.Add(d.Multiply(100)));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(line, Tol));

            Assert.True(point.DistanceTo(rim) <= Close, $"{point} is {point.DistanceTo(rim):E2} from {rim}");
            Assert.True(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentThroughARimPointAlmostSquareToThePlaneCrossesACircleItIsThereToo()
        {
            // The radius-100 circle about the origin in z = 0, as an ellipse, and the segment 200 long through (100, 0, 0)
            // leaning 5E-4 rad from Z towards X. The pierce point is on the rim. GeoCircle3 gives no point for this on
            // 81e409e: its graze band for a segment, EP² |D|⁴ on the discriminant (Arc3.cs:95), reads the two crossings of
            // the circle's sphere, 0.1 apart, as one touch at the foot of the centre, 0.05 off the rim. The ellipse is held
            // to the pierce point, not to the circle (decision 19: the one point where it pierces the plane).
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100));
            var rim = new GeoPoint3(100, 0, 0);
            var d = new GeoVector3(Math.Sin(5E-4), 0, Math.Cos(5E-4));
            var line = new GeoLine3(rim.Add(d.Multiply(-100)), rim.Add(d.Multiply(100)));

            GeoPoint3 point = Assert.Single(ellipse.GetIntersections(line, Tol));

            Assert.True(point.DistanceTo(rim) <= Close, $"{point} is {point.DistanceTo(rim):E2} from {rim}");
            Assert.True(ellipse.CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentPiercingThePlaneOutsideTheRimTouchesNothing()
        {
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(400, 0, -50), e.At(400, 0, 50));

            Assert.Empty(e.Build().GetIntersections(line, Tol));
            Assert.False(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentGrazingTheRimInItsPlaneTouchesItOnce()
        {
            // Along the tangent at t = 1, 200 each way from the rim point.
            Specimen e = Tilted();
            GeoPoint3 rim = e.Rim(1.0);
            GeoVector3 d = Tangent(e, 1.0);
            var line = new GeoLine3(rim.Add(d.Multiply(-200)), rim.Add(d.Multiply(200)));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(line, Tol));

            Assert.True(point.DistanceTo(rim) <= Touch, $"{point} is {point.DistanceTo(rim):E2} from {rim}");
            Assert.True(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ALongSegmentCuttingNearTheEndOfTheMajorAxisCrossesTheRimTwice()
        {
            // The line u = 299.995, 2 000 long: it cuts the rim 0.005 inside the end of the major axis, five times the point
            // tolerance, at v = ±100 √(1 - (299.995/300)²) = ±0.5773, so the chord is 1.15 long and two points.
            Specimen e = Tilted();
            double v = 100 * Math.Sqrt(1 - (299.995 / 300) * (299.995 / 300));
            var line = new GeoLine3(e.At(299.995, -1000, 0), e.At(299.995, 1000, 0));

            SamePointsInOrder(new[] { e.At(299.995, -v, 0), e.At(299.995, v, 0) }, e.Build().GetIntersections(line, Tol), Touch);
        }

        [Fact]
        public void ALongSegmentCuttingACircleItIsNearATangentCrossesItTwice()
        {
            // The radius-100 circle about the origin as an ellipse, and the line y = √(100² - 1), 4 000 long: its chord is
            // 2 long and its sag 0.005, five times the point tolerance. GeoCircle3 gives no point for this on 81e409e: its
            // band, EP |D| / 2 = 2 on the half chord for a segment this long (Arc3.cs:95, 102), takes the chord for a touch
            // at its middle, 0.005 off the rim. The ellipse is held to the 2D answer (decision 19).
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100));
            double y = Math.Sqrt(100 * 100 - 1);
            var line = new GeoLine3(new GeoPoint3(-2000, y, 0), new GeoPoint3(2000, y, 0));

            SamePointsInOrder(new[] { new GeoPoint3(-1, y, 0), new GeoPoint3(1, y, 0) }, ellipse.GetIntersections(line, Tol), Touch);
            Assert.True(ellipse.CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentInsideTheDiscCrossesNothingAndTouchesNothing()
        {
            // (-200, 50) to (200, 50) stops short of the rim at ±259.8 both ways.
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(-200, 50, 0), e.At(200, 50, 0));

            Assert.Empty(e.Build().GetIntersections(line, Tol));
            Assert.False(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentFromTheCentreEndingOnTheRimCrossesAtItsEnd()
        {
            Specimen e = Tilted();
            var line = new GeoLine3(e.Center, e.Rim(0.5));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(line, Tol));

            Assert.True(point.DistanceTo(e.Rim(0.5)) <= Close, $"{point} is {point.DistanceTo(e.Rim(0.5)):E2} from the end");
        }

        [Fact]
        public void ASegmentFiveAboveThePlaneCrossingOverTheRimTouchesNothing()
        {
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(-400, 50, 5), e.At(400, 50, 5));

            Assert.Empty(e.Build().GetIntersections(line, Tol));
            Assert.False(e.Build().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentFourTenThousandthsAboveThePlaneCrossesTheRimAsIfInIt()
        {
            // 0.0004 above, 0.4 of the point tolerance: it crosses the rim at (∓259.8, 50) to within that height. Whether
            // the points come back in the plane or at the segment's height is not pinned, only that they are within 0.0005.
            Specimen e = Tilted();
            var line = new GeoLine3(e.At(-400, 50, 0.0004), e.At(400, 50, 0.0004));

            SamePointsInOrder(new[] { e.At(-Across, 50, 0), e.At(Across, 50, 0) }, e.Build().GetIntersections(line, Tol), 0.0005);
            Assert.True(e.Build().CollidesWith(line, Tol));
        }

        #endregion

        #region Rays

        [Fact]
        public void ARayFromOutsideAcrossTheEllipseCrossesTheRimTwiceInOrderAlongIt()
        {
            Specimen e = Tilted();
            var ray = new GeoRay3(e.At(-400, 50, 0), e.U);

            GeoPoint3[] crossings = e.Build().GetIntersections(ray, Tol);

            SamePointsInOrder(new[] { e.At(-Across, 50, 0), e.At(Across, 50, 0) }, crossings, Close);
            Assert.True(e.Build().CollidesWith(ray, Tol));
            Assert.True(e.Build().TryIntersectWith(ray, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);
        }

        [Fact]
        public void ARayFromInsideTheDiscCrossesTheRimOnceAheadOfIt()
        {
            Specimen e = Tilted();
            var ray = new GeoRay3(e.At(0, 50, 0), e.U);

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(ray, Tol));

            Assert.True(point.DistanceTo(e.At(Across, 50, 0)) <= Close, $"{point}");
        }

        [Fact]
        public void ARayPointingAwayFromTheEllipseTouchesNothing()
        {
            Specimen e = Tilted();
            var ray = new GeoRay3(e.At(400, 50, 0), e.U);

            Assert.Empty(e.Build().GetIntersections(ray, Tol));
            Assert.False(e.Build().CollidesWith(ray, Tol));
            Assert.False(e.Build().TryIntersectWith(ray, out _, Tol));
        }

        [Fact]
        public void ARayUpThroughTheDiscInsideTheRimTouchesNothing()
        {
            Specimen e = Tilted();
            var ray = new GeoRay3(e.At(30, -20, -50), e.N);

            Assert.Empty(e.Build().GetIntersections(ray, Tol));
            Assert.False(e.Build().CollidesWith(ray, Tol));
        }

        [Fact]
        public void ARayUpThroughARimPointCrossesItThereAndTheRayTurnedRoundTouchesNothing()
        {
            // From 40 below the rim point at t = 4, leaning along N - 0.5 V; turned round it points away.
            Specimen e = Tilted();
            GeoPoint3 rim = e.Rim(4.0);
            GeoVector3 d = e.N.Add(e.V.Multiply(-0.5)).Normalize();
            GeoPoint3 start = rim.Add(d.Multiply(-40));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(new GeoRay3(start, d), Tol));

            Assert.True(point.DistanceTo(rim) <= Close, $"{point} is {point.DistanceTo(rim):E2} from {rim}");
            Assert.Empty(e.Build().GetIntersections(new GeoRay3(start, d.Negate()), Tol));
            Assert.False(e.Build().CollidesWith(new GeoRay3(start, d.Negate()), Tol));
        }

        #endregion
    }
}
