using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Three points share a line, in space and in the plane, by the tolerance they are given: where the middle one lies within
    /// the point tolerance of the line through the other two, or the triangle they make has twice an area of no more than the
    /// square of the point tolerance. The arc in space read only the cross product, against the global vector tolerance,
    /// whatever tolerance was asked, and the arc in the plane only the area, which does not grow with the chord: the points
    /// (0, 0, 7), (5000, 1.2E-10, 7) and (10000, 0, 7) were given an arc of radius 1E17 that missed its own start by 8.8.
    /// An arc that would miss one of its points by more than the tolerance is refused as well.
    /// </summary>
    public class Arc3FaultG1kThreePointsTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        private static GeoPoint3 Tilted(double x, double off, double tilt) => new GeoPoint3(x, off * Math.Cos(tilt), 7 + off * Math.Sin(tilt));

        [Theory]
        [InlineData(5000.0, 1.2E-10)]
        [InlineData(500.0, 1.2E-9)]
        public void ThreePointsAHairOffALongChord_HaveNoArcThroughThem(double half, double off)
        {
            // The middle stands 1.2E-10 off the chord of 10 000, or 1.2E-9 off the chord of 1 000: the arcs given had radii of
            // 1E17 and 1E14 and missed their ends by up to 16.8 and 0.042.
            Tolerance t = Tolerance.Default;

            Assert.Throws<InvalidOperationException>(() => GeoArc3.FromThreePoints(new GeoPoint3(0, 0, 7), new GeoPoint3(half, off, 7), new GeoPoint3(2 * half, 0, 7), t));
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(half, off), new GeoPoint2(2 * half, 0), t));
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromThreePoints(new GeoPoint2(0, 7), new GeoPoint2(half, 7 + off), new GeoPoint2(2 * half, 7), t));
        }

        [Theory]
        [InlineData(1E-3, 0.0)]
        [InlineData(1E-3, 0.7)]
        [InlineData(0.0, 2.0)]
        public void AShallowArcBowingWellPastTheTolerance_HasItsArcThroughItsPoints(double equalPoint, double tilt)
        {
            // The chord of 10 000 with the middle 1 off it, the arc of radius 12 500 000.5, in a plane turned about X by the tilt.
            Tolerance t = At(equalPoint);
            double within = Math.Max(equalPoint, 1E-6);
            GeoPoint3 start = Tilted(0, 0, tilt), middle = Tilted(5000, 1, tilt), end = Tilted(10000, 0, tilt);

            GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end, t);
            GeoArc2 flat = GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(5000, 1), new GeoPoint2(10000, 0), t);

            Assert.InRange(arc.Radius, 12500000.5 - 1E-3, 12500000.5 + 1E-3);
            Assert.InRange(flat.Radius, 12500000.5 - 1E-3, 12500000.5 + 1E-3);
            Assert.InRange(arc.StartPoint.DistanceTo(start), 0.0, within);
            Assert.InRange(arc.EndPoint.DistanceTo(end), 0.0, within);
            Assert.InRange(arc.DistanceTo(middle, t), 0.0, within);
            Assert.InRange(flat.StartPoint.DistanceTo(new GeoPoint2(0, 0)), 0.0, within);
            Assert.InRange(flat.EndPoint.DistanceTo(new GeoPoint2(10000, 0)), 0.0, within);
            Assert.InRange(flat.DistanceTo(new GeoPoint2(5000, 1), t), 0.0, within);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.7)]
        public void ThreePointsAHundredthOffALine_HaveAnArcAtAThousandthAndNoneAt005(double tilt)
        {
            // The middle 0.01 off the chord of 200: the arc of radius 500 000 at a thousandth, a line at 0.05.
            GeoPoint3 start = Tilted(0, 0, tilt), middle = Tilted(100, 0.01, tilt), end = Tilted(200, 0, tilt);

            GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end, At(1E-3));
            GeoArc2 flat = GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(100, 0.01), new GeoPoint2(200, 0), At(1E-3));

            Assert.InRange(arc.Radius, 499999.0, 500001.0);
            Assert.InRange(flat.Radius, 499999.0, 500001.0);
            Assert.InRange(arc.DistanceTo(middle, At(1E-3)), 0.0, 1E-6);
            Assert.Throws<InvalidOperationException>(() => GeoArc3.FromThreePoints(start, middle, end, At(0.05)));
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(100, 0.01), new GeoPoint2(200, 0), At(0.05)));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.7)]
        public void AShortArcOfALargeCircle_IsALineAtAThousandthAndAnArcThroughItsPointsAtNought(double tilt)
        {
            // The arc of radius 1 000 sweeping 0.002 radians about (5000, 3000, 7), its middle 0.0005 off its chord of 2: the
            // AutoCAD bridge reads such an arc at nought, as it was drawn.
            var centre = new GeoPoint3(5000, 3000, 7);
            GeoPoint3 At(double angle) => centre.Add(new GeoVector3(1000 * Math.Cos(angle), 1000 * Math.Sin(angle) * Math.Cos(tilt), 1000 * Math.Sin(angle) * Math.Sin(tilt)));
            GeoPoint3 start = At(0.3), middle = At(0.301), end = At(0.302);
            var flat = new GeoPoint2(5000 + 1000 * Math.Cos(0.301), 3000 + 1000 * Math.Sin(0.301));
            var startFlat = new GeoPoint2(5000 + 1000 * Math.Cos(0.3), 3000 + 1000 * Math.Sin(0.3));
            var endFlat = new GeoPoint2(5000 + 1000 * Math.Cos(0.302), 3000 + 1000 * Math.Sin(0.302));
            Tolerance nought = default(Tolerance);

            Assert.Throws<InvalidOperationException>(() => GeoArc3.FromThreePoints(start, middle, end, Tolerance.Default));
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromThreePoints(startFlat, flat, endFlat, Tolerance.Default));

            GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end, nought);
            GeoArc2 arc2 = GeoArc2.FromThreePoints(startFlat, flat, endFlat, nought);

            Assert.InRange(arc.Radius, 1000.0 - 1E-6, 1000.0 + 1E-6);
            Assert.InRange(arc2.Radius, 1000.0 - 1E-6, 1000.0 + 1E-6);
            Assert.InRange(Math.Abs(arc.SweptAngle), 0.002 - 1E-9, 0.002 + 1E-9);
            Assert.InRange(arc.StartPoint.DistanceTo(start), 0.0, 1E-9);
            Assert.InRange(arc.EndPoint.DistanceTo(end), 0.0, 1E-9);
            Assert.InRange(arc.DistanceTo(middle, nought), 0.0, 1E-9);
            Assert.InRange(arc2.StartPoint.DistanceTo(startFlat), 0.0, 1E-9);
            Assert.InRange(arc2.EndPoint.DistanceTo(endFlat), 0.0, 1E-9);
            Assert.InRange(arc2.DistanceTo(flat, nought), 0.0, 1E-9);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.7)]
        public void AnArcBowingWellOffItsChord_IsTheSameToTheBitAtNoughtAsAtTheDefault(double tilt)
        {
            // The tolerance only decides whether three points are refused; the arc through them is worked out alike.
            GeoPoint3 start = Tilted(0, 0, tilt), middle = Tilted(50, 20, tilt), end = Tilted(100, 0, tilt);

            GeoArc3 atNought = GeoArc3.FromThreePoints(start, middle, end, default(Tolerance));
            GeoArc3 atDefault = GeoArc3.FromThreePoints(start, middle, end, Tolerance.Default);
            GeoArc2 flatAtNought = GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(50, 20), new GeoPoint2(100, 0), default(Tolerance));
            GeoArc2 flatAtDefault = GeoArc2.FromThreePoints(new GeoPoint2(0, 0), new GeoPoint2(50, 20), new GeoPoint2(100, 0), Tolerance.Default);

            Assert.Equal(atDefault, atNought);
            Assert.Equal(flatAtDefault, flatAtNought);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.7)]
        public void ThreePointsAMillionthOffAShortChord_HaveAnArcAtNought(double tilt)
        {
            // The middle 1E-6 off the chord of 2, the arc of radius 500 000: refused at nought in space, which read the
            // cross product against the global vector tolerance.
            GeoPoint3 start = Tilted(0, 0, tilt), middle = Tilted(1, 1E-6, tilt), end = Tilted(2, 0, tilt);
            Tolerance nought = At(0.0);

            GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end, nought);

            Assert.InRange(arc.Radius, 499999.0, 500001.0);
            Assert.InRange(arc.StartPoint.DistanceTo(start), 0.0, 1E-8);
            Assert.InRange(arc.EndPoint.DistanceTo(end), 0.0, 1E-8);
            Assert.Throws<InvalidOperationException>(() => GeoArc3.FromThreePoints(start, middle, end, At(1E-3)));
        }
    }
}
