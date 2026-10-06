using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Moving, turning and transforming an ellipse. A move and a turn keep the radii bit for bit; any affine
    /// transformation that does not flatten the plane gives an ellipse again, whose rim is the image of the old rim. The
    /// sign of the new major axis is free unless the result is a circle, so these tests read the image through the rim
    /// and through the change of the eccentric angle along it (<see cref="Ellipse2Oracle.AngleOf"/>), which do not
    /// depend on that sign: under a transformation keeping the turn of the plane the angle advances with the old one,
    /// under one turning it over the angle runs back.
    /// </summary>
    public class Ellipse2TransformTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static readonly GeoPoint2 Pivot = new GeoPoint2(10, 20);

        private static GeoPoint2 Turned(GeoPoint2 point, double angle) => new GeoPoint2(
            Pivot.X + (point.X - Pivot.X) * Math.Cos(angle) - (point.Y - Pivot.Y) * Math.Sin(angle),
            Pivot.Y + (point.X - Pivot.X) * Math.Sin(angle) + (point.Y - Pivot.Y) * Math.Cos(angle));

        /// <summary>
        /// Asserts that the image of the old rim lies on the new one, and that walking the old rim in steps of a
        /// twentieth of a turn walks the new one in steps of the same size, forward or back.
        /// </summary>
        private static void AssertImageOfRim(GeoEllipse2 before, GeoTransform2 transform, GeoEllipse2 after, bool reversed)
        {
            const double step = 2 * Math.PI / 20;
            double previous = Ellipse2Oracle.AngleOf(after, transform.Transform(Ellipse2Oracle.Rim(before, 0.05)));

            for (int k = 1; k <= 20; k++)
            {
                GeoPoint2 image = transform.Transform(Ellipse2Oracle.Rim(before, 0.05 + k * step));
                Assert.InRange(Ellipse2Oracle.GapToRim(after, image), 0.0, 1E-9);

                double angle = Ellipse2Oracle.AngleOf(after, image);
                double change = Math.IEEERemainder(angle - previous, 2 * Math.PI);
                Assert.Equal(reversed ? -step : step, change, 1E-9);
                previous = angle;
            }
        }

        [Fact]
        public void TranslateMovesTheCentreAndKeepsTheRestBitForBit()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            var step = new GeoVector2(1000.5, 7);
            GeoEllipse2 moved = ellipse.Translate(step);

            Assert.Equal(new GeoPoint2(1040.5, -18), moved.Center);
            Assert.Equal(ellipse.MajorAxis, moved.MajorAxis);
            Assert.Equal(ellipse.MajorRadius, moved.MajorRadius);
            Assert.Equal(ellipse.MinorRadius, moved.MinorRadius);

            // The operators are the same move, forward and back.
            Assert.Equal(moved, ellipse + step);
            Assert.Equal(ellipse.Translate(-step), ellipse - step);
        }

        [Fact]
        public void RotateByTurnsTheAxisAndTheZeroOfTheAngleWithIt()
        {
            // Turned 0.9 about (10, 20): the radii are kept bit for bit, and each rim point at t goes to the rim point at
            // t of the turned ellipse.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoEllipse2 turned = ellipse.RotateBy(0.9, Pivot);

            Assert.Equal(300.0, turned.MajorRadius);
            Assert.Equal(100.0, turned.MinorRadius);
            Assert.True(turned.Center.IsEqualTo(Turned(ellipse.Center, 0.9), Tight));

            for (int k = 0; k < 12; k++)
            {
                double t = k * 0.55;
                Assert.True(turned.GetPointAtAngle(t).IsEqualTo(Turned(ellipse.GetPointAtAngle(t), 0.9), Tight));
            }
        }

        [Fact]
        public void ARotationByTransformDrawsWhatRotateByDraws()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoTransform2 rotation = GeoTransform2.Rotation(Pivot, 0.9);
            GeoEllipse2 transformed = ellipse.TransformBy(rotation);

            Assert.True(transformed.IsEqualTo(ellipse.RotateBy(0.9, Pivot), Tight));
            AssertImageOfRim(ellipse, rotation, transformed, false);
        }

        [Fact]
        public void AnUnevenScalingCanMakeTheMinorAxisTheMajor()
        {
            // 300 by 100 along X, stretched four times along Y: the minor radius becomes 400 and takes over as the major,
            // along Y; the old major radius, 300, is the minor now. The centre (40, -25) goes to (40, -100).
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);
            GeoTransform2 scaling = GeoTransform2.Scaling(1, 4);
            GeoEllipse2 scaled = ellipse.TransformBy(scaling);

            Assert.Equal(400.0, scaled.MajorRadius, 1E-9);
            Assert.Equal(300.0, scaled.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(scaled.MajorAxis.Y), 1E-12);
            Assert.True(scaled.Center.IsEqualTo(new GeoPoint2(40, -100), Tight));
            AssertImageOfRim(ellipse, scaling, scaled, false);
        }

        [Fact]
        public void AShearTurnsACircleIntoAnEllipseOfTheSameArea()
        {
            // The shear (x + y, y) has singular values the golden ratio and its inverse, 1.618034 and 0.618034, so a
            // circle of radius 100 becomes 161.803 by 61.803, its major axis along (0.8507, 0.5257), 31.7 degrees up,
            // and its area kept at 10 000 pi since the shear's determinant is one. The shear also moves by (5, -3).
            GeoEllipse2 circle = GeoEllipse2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 100));
            var shear = new GeoTransform2(new double[,] { { 1, 1, 5 }, { 0, 1, -3 }, { 0, 0, 1 } });
            GeoEllipse2 sheared = circle.TransformBy(shear);

            Assert.Equal(161.80339887498948, sheared.MajorRadius, 1E-9);
            Assert.Equal(61.80339887498948, sheared.MinorRadius, 1E-9);
            Assert.Equal(10000 * Math.PI, sheared.Area, 1E-6);
            Assert.Equal(0.0, sheared.MajorAxis.CrossProduct(new GeoVector2(0.8506508083520399, 0.5257311121191336)), 1E-12);
            Assert.True(sheared.Center.IsEqualTo(new GeoPoint2(5, -3), Tight));
            AssertImageOfRim(circle, shear, sheared, false);
        }

        [Fact]
        public void AMirrorRunsTheAngleTheOtherWayRoundAsSeenFromTheOldEllipse()
        {
            // Mirrored in the line through the origin along (100, 30): the radii stay 300 and 100, the rim's image is
            // the new rim, and walking the old rim forward walks the new one back.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 30)));
            GeoEllipse2 mirrored = ellipse.TransformBy(mirror);

            Assert.Equal(300.0, mirrored.MajorRadius, 1E-9);
            Assert.Equal(100.0, mirrored.MinorRadius, 1E-9);
            AssertImageOfRim(ellipse, mirror, mirrored, true);
        }

        [Fact]
        public void ATransformationThatFlattensThePlaneIsRefused()
        {
            // Squashed flat along Y, and squashed to 1E-13 of its height, both under the 1E-12 of the size of the
            // transformation the spec allows. A uniform scaling by a millionth flattens nothing, however small it makes
            // the ellipse: its determinant is 1E-12 of a size of 1E-12, a ratio of one.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            Assert.Throws<InvalidOperationException>(() => ellipse.TransformBy(GeoTransform2.Scaling(1, 0)));
            Assert.Throws<InvalidOperationException>(() => ellipse.TransformBy(GeoTransform2.Scaling(1, 1E-13)));

            GeoEllipse2 tiny = ellipse.TransformBy(GeoTransform2.Scaling(1E-6));
            Assert.Equal(3E-4, tiny.MajorRadius, 1E-15);
            Assert.Equal(1E-4, tiny.MinorRadius, 1E-15);
        }

        [Fact]
        public void ATransformationAndItsInverseGiveTheEllipseBack()
        {
            // A shear, an uneven scaling, a turn and a move, and the same with a mirror after: undone, each gives the
            // 300 by 100 back, its axis up to sign.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoTransform2 mixed = GeoTransform2.Translation(new GeoVector2(-70, 15))
                * GeoTransform2.Rotation(1.3)
                * GeoTransform2.Scaling(2.5, 0.7)
                * new GeoTransform2(new double[,] { { 1, 0.4, 0 }, { 0, 1, 0 }, { 0, 0, 1 } });
            GeoTransform2 mirrored = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(3, 4), new GeoPoint2(-5, 9))) * mixed;

            foreach (GeoTransform2 transform in new[] { mixed, mirrored })
            {
                GeoEllipse2 back = ellipse.TransformBy(transform).TransformBy(transform.Inverse(Tight));

                Assert.True(back.IsEqualTo(ellipse, Tight));
            }

            AssertImageOfRim(ellipse, mixed, ellipse.TransformBy(mixed), false);
            AssertImageOfRim(ellipse, mirrored, ellipse.TransformBy(mirrored), true);
        }

        [Fact]
        public void ACircleTurnedAndScaledEvenlyStaysACircleAndCarriesItsAxisRound()
        {
            // A circle of radius 50 about (30, 40), turned 0.7 about (10, 20) and doubled: a circle of radius 100. A
            // circle's axis is the transformed axis, so the zero of t turns with it, to (cos 0.7, sin 0.7), and each rim
            // point at t goes to the rim point at t.
            GeoEllipse2 circle = GeoEllipse2.FromCircle(new GeoCircle2(new GeoPoint2(30, 40), 50));
            GeoTransform2 transform = GeoTransform2.Rotation(Pivot, 0.7) * GeoTransform2.Scaling(2);
            GeoEllipse2 after = circle.TransformBy(transform);

            Assert.Equal(100.0, after.MajorRadius, 1E-12 * 100);
            Assert.Equal(100.0, after.MinorRadius, 1E-12 * 100);
            Assert.True(after.MajorAxis.IsEqualTo(new GeoVector2(Math.Cos(0.7), Math.Sin(0.7)), new Tolerance(1E-12, 1E-12)));
            Assert.True(after.Center.IsEqualTo(transform.Transform(circle.Center), Tight));

            for (int k = 0; k < 12; k++)
            {
                double p = k / 12.0;
                Assert.True(after.GetPointAtParameter(p).IsEqualTo(transform.Transform(circle.GetPointAtParameter(p)), new Tolerance(1E-12 * 100, 1E-12)));
            }
        }

        [Fact]
        public void AnUnevenScalingThatEvensTheRadiiGivesACircleAlongTheOldAxis()
        {
            // 200 by 50 along X, halved along X and doubled along Y: both radii come to 100, and as a circle its axis is
            // the old one transformed, (0.5, 0) made unit: X again, not whichever way an SVD would pick.
            GeoEllipse2 ellipse = new GeoEllipse2(new GeoPoint2(40, -25), GeoVector2.XAxis, 200, 50);
            GeoEllipse2 circle = ellipse.TransformBy(GeoTransform2.Scaling(0.5, 2));

            Assert.Equal(100.0, circle.MajorRadius, 1E-12 * 100);
            Assert.Equal(100.0, circle.MinorRadius, 1E-12 * 100);
            Assert.True(circle.MajorAxis.IsEqualTo(GeoVector2.XAxis, new Tolerance(1E-12, 1E-12)));
            Assert.True(circle.Center.IsEqualTo(new GeoPoint2(20, -50), Tight));
        }
    }
}
