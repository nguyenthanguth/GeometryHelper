using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Moving and transforming an ellipse in space (decision 8). A move keeps every field but the centre to the bit; any
    /// affine transformation that leaves the plane a plane gives an ellipse again, whose rim is the image of the old rim.
    /// The sign of the new major axis is not read here unless the result is a circle; the image is read through the rim
    /// and through the change of the eccentric angle along it, which do not depend on that sign. The new normal points
    /// where the circle's Transform(GeoPlane3) sends it (GeoTransform3.cs:606-621), pinned on two mirrors.
    /// </summary>
    public class Ellipse3TransformTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        /// <summary>
        /// Asserts that the image of the old rim lies on the new one, and that walking the old rim in steps of a
        /// twentieth of a turn walks the new one in steps of the same size, forward or back.
        /// </summary>
        private static void AssertImageOfRim(Ellipse3Oracle.Specimen before, GeoTransform3 transform, GeoEllipse3 after, bool reversed)
        {
            const double step = 2 * Math.PI / 20;
            double previous = Ellipse3Oracle.AngleIn(after, transform.Transform(before.Rim(0.05)));

            for (int k = 1; k <= 20; k++)
            {
                GeoPoint3 image = transform.Transform(before.Rim(0.05 + k * step));
                Assert.InRange(Ellipse3Oracle.GapToRim(after, image, out double offPlane), 0.0, 1E-9);
                Assert.Equal(0.0, offPlane, 1E-9);

                double angle = Ellipse3Oracle.AngleIn(after, image);
                double change = Math.IEEERemainder(angle - previous, 2 * Math.PI);
                Assert.Equal(reversed ? -step : step, change, 1E-9);
                previous = angle;
            }
        }

        [Fact]
        public void TranslateMovesTheCentreAndKeepsTheRestBitForBit()
        {
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();
            var step = new GeoVector3(1000.5, 7, -3.25);
            GeoEllipse3 moved = ellipse.Translate(step);

            Assert.Equal(new GeoPoint3(1040.5, -18, 56.75), moved.Center);
            Assert.Equal(ellipse.Normal, moved.Normal);
            Assert.Equal(ellipse.MajorAxis, moved.MajorAxis);
            Assert.Equal(ellipse.MinorAxis, moved.MinorAxis);
            Assert.Equal(ellipse.MajorRadius, moved.MajorRadius);
            Assert.Equal(ellipse.MinorRadius, moved.MinorRadius);
        }

        [Fact]
        public void TransformByNullIsRefused()
        {
            // As the circle's (GeoCircle3.cs:102-105).
            Assert.Throws<ArgumentNullException>(() => Ellipse3Oracle.Tilted().Build().TransformBy(null));
        }

        [Fact]
        public void ARigidMotionCarriesEachRimPointToTheRimPointAtTheSameAngle()
        {
            // Turned 0.7 about (1, 2, 3) and moved: the radii are kept to 1E-12 of the size, the normal goes where the
            // plane's does, and the point at t goes to the point at t (A2: the new axis is where the old one went).
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(3, -4, 5)) * GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7);
            GeoEllipse3 moved = ellipse.TransformBy(motion);

            Assert.Equal(300.0, moved.MajorRadius, 1E-12 * 300);
            Assert.Equal(100.0, moved.MinorRadius, 1E-12 * 300);
            Assert.True(moved.Normal.IsEqualTo(motion.Transform(new GeoPlane3(specimen.Center, specimen.N)).Normal, Tight));
            Assert.True(moved.Center.IsEqualTo(motion.Transform(specimen.Center), Tight));

            for (int k = -3; k < 16; k++)
            {
                double t = k * 0.45;
                Assert.True(moved.GetPointAtAngle(t).IsEqualTo(motion.Transform(specimen.Rim(t)), Tight), $"t = {t}");
            }
        }

        [Fact]
        public void AnUnevenScalingTurnsACircleIntoAnEllipse()
        {
            // A circle of 100 about (10, 20, 30) in a level plane, stretched three times along X: 300 by 100 along X about
            // (30, 20, 30), still level. GeoCircle3.TransformBy refuses this (GeoTransform3.cs:700-703).
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100);
            GeoTransform3 stretch = GeoTransform3.Scaling(3, 1, 1);
            GeoEllipse3 stretched = GeoEllipse3.FromCircle(circle).TransformBy(stretch);

            Assert.Equal(300.0, stretched.MajorRadius, 1E-9);
            Assert.Equal(100.0, stretched.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(stretched.MajorAxis.X), 1E-12);
            Assert.True(stretched.Normal.IsEqualTo(GeoVector3.ZAxis, Tight));
            Assert.True(stretched.Center.IsEqualTo(new GeoPoint3(30, 20, 30), Tight));
            Assert.Throws<InvalidOperationException>(() => circle.TransformBy(stretch));

            circle.GetPlane().GetAxes(out GeoVector3 u, out GeoVector3 v);
            AssertImageOfRim(new Ellipse3Oracle.Specimen(circle.Center, u, v, 100, 100), stretch, stretched, false);
        }

        [Fact]
        public void AnUnevenScalingOfATiltedEllipseIsTheImageOfItsRim()
        {
            // The tilted 300 by 100 scaled 2, 0.5 and 1.5 along X, Y and Z, and sheared: whatever the radii come to, the
            // rim's image is the new rim, walked the same way round, the normal where the plane's goes.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var shear = new GeoTransform3(new double[,] { { 1, 0.4, 0, 0 }, { 0, 1, 0.3, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            GeoTransform3 transform = GeoTransform3.Scaling(2, 0.5, 1.5) * shear;
            GeoEllipse3 after = specimen.Build().TransformBy(transform);

            Assert.True(after.MajorRadius >= after.MinorRadius);
            Assert.True(after.Normal.IsEqualTo(transform.Transform(new GeoPlane3(specimen.Center, specimen.N)).Normal, Tight));
            AssertImageOfRim(specimen, transform, after, false);
        }

        [Fact]
        public void AMirrorKeepsTheRadiiAndRunsTheAngleBack()
        {
            // Mirrored in the plane through (5, 0, 0) of normal (1, 0.3, 0.2): the radii stay 300 and 100, the rim's image
            // is the new rim, walking the old rim forward walks the new one back, and the normal is where the circle's
            // Transform(GeoPlane3) sends it, which is the opposite of the image of U crossed with the image of V.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoTransform3 mirror = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(5, 0, 0), new GeoVector3(1, 0.3, 0.2)));
            GeoEllipse3 mirrored = specimen.Build().TransformBy(mirror);
            GeoVector3 planeWay = mirror.Transform(new GeoPlane3(specimen.Center, specimen.N)).Normal;

            Assert.Equal(300.0, mirrored.MajorRadius, 1E-9);
            Assert.Equal(100.0, mirrored.MinorRadius, 1E-9);
            Assert.True(mirrored.Normal.IsEqualTo(planeWay, Tight));
            Assert.True(mirror.Transform(specimen.U).CrossProduct(mirror.Transform(specimen.V)).IsEqualTo(planeWay.Multiply(-1), Tight));
            AssertImageOfRim(specimen, mirror, mirrored, true);
        }

        [Fact]
        public void AMirrorInItsOwnPlaneTurnsTheNormalOverAsTheCirclesDoes()
        {
            // Mirrored in its own plane the disc stays where it is, every rim point on itself, but the normal turns over to
            // -N, as a circle's does, so t runs the other way round.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoTransform3 mirror = GeoTransform3.Mirror(new GeoPlane3(specimen.Center, specimen.N));
            GeoEllipse3 mirrored = specimen.Build().TransformBy(mirror);
            GeoCircle3 circle = new GeoCircle3(specimen.Center, specimen.N, 300).TransformBy(mirror);

            Assert.True(circle.Normal.IsEqualTo(specimen.N.Multiply(-1), Tight));
            Assert.True(mirrored.Normal.IsEqualTo(specimen.N.Multiply(-1), Tight));
            Assert.True(mirrored.Center.IsEqualTo(specimen.Center, Tight));
            Assert.Equal(300.0, mirrored.MajorRadius, 1E-9);
            Assert.Equal(100.0, mirrored.MinorRadius, 1E-9);
            AssertImageOfRim(specimen, mirror, mirrored, true);
        }

        [Fact]
        public void ATransformationThatFoldsThePlaneOntoALineIsRefused()
        {
            // Standing in the plane of normal (1, 1, 0)/√2, its major axis halfway between the level h = (1, -1, 0)/√2 and
            // Z: squashed flat along Z, the plane folds onto h. The ellipse refuses what the circle's plane refuses, a
            // transformation with no inverse (GeoTransform3.cs:608-611); a squash to 1E-14 has one, and gives a thin ellipse.
            GeoEllipse3 standing = Standing();

            Assert.Throws<InvalidOperationException>(() => standing.TransformBy(GeoTransform3.Scaling(1, 1, 0)));
        }

        [Fact]
        public void ASquashThatLeavesThePlaneStandingGivesAThinEllipse()
        {
            // Squashed to 1E-6 the semi-axes are 2E-6 apart in direction, over the bound: an ellipse √(300² + 100²)/√2 =
            // 223.607 long and 3E4 × 1E-6 / 223.607 = 1.342E-4 wide, π × 0.03 in area, in the same standing plane.
            GeoEllipse3 thin = Standing().TransformBy(GeoTransform3.Scaling(1, 1, 1E-6));

            Assert.Equal(223.60679774997897, thin.MajorRadius, 1E-6);
            Assert.Equal(1.3416407864998739E-4, thin.MinorRadius, 1E-10);
            Assert.Equal(Math.PI * 0.03, thin.Area, 1E-9);
            Assert.True(thin.Normal.IsEqualTo(new GeoVector3(1, 1, 0).Normalize(), Tight));
        }

        [Fact]
        public void ATransformationAndItsInverseGiveTheEllipseBack()
        {
            // A shear, an uneven scaling, a turn and a move, and the same with a mirror after: undone, each gives the tilted
            // 300 by 100 back, its axis up to sign.
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();
            var shear = new GeoTransform3(new double[,] { { 1, 0.4, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            GeoTransform3 mixed = GeoTransform3.Translation(new GeoVector3(-70, 15, 8))
                * GeoTransform3.RotationAxis(new GeoVector3(1, -1, 2), 1.3)
                * GeoTransform3.Scaling(2.5, 0.7, 1.2)
                * shear;
            GeoTransform3 mirrored = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(3, 4, 5), new GeoVector3(-5, 9, 1))) * mixed;

            foreach (GeoTransform3 transform in new[] { mixed, mirrored })
            {
                GeoEllipse3 back = ellipse.TransformBy(transform).TransformBy(transform.Inverse(Tight));

                Assert.True(back.IsEqualTo(ellipse, Tight));
            }
        }

        [Fact]
        public void ACircleTurnedAndScaledEvenlyStaysACircleAndCarriesItsZeroRound()
        {
            // A circle of 50 turned 0.7 about (1, 2, 3) and doubled: a circle of 100 to 1E-12 of its size, its axis the
            // image of the old one made unit, so the point at t goes to the point at t.
            var circle = new GeoCircle3(new GeoPoint3(30, 40, 50), new GeoVector3(1, 1, 1), 50);
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(circle);
            GeoTransform3 transform = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7) * GeoTransform3.Scaling(2);
            GeoEllipse3 after = ellipse.TransformBy(transform);

            Assert.Equal(100.0, after.MajorRadius, 1E-12 * 100);
            Assert.Equal(100.0, after.MinorRadius, 1E-12 * 100);
            Assert.True(after.MajorAxis.IsEqualTo(transform.Transform(ellipse.MajorAxis).Normalize(), Tight));

            for (int k = 0; k < 12; k++)
            {
                double t = k * 0.55;
                Assert.True(after.GetPointAtAngle(t).IsEqualTo(transform.Transform(circle.GetPointAtAngle(t)), Tight), $"t = {t}");
            }
        }

        [Fact]
        public void TheTransformationAndTheEllipseTransformAlike()
        {
            // The join to ApiSymmetryTests.TransformingThroughEitherFormGivesTheSameResult, which lists the types by hand.
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(3, -4, 5)).Multiply(GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7));
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();

            Assert.Equal(motion.Transform(ellipse), ellipse.TransformBy(motion));
        }

        /// <summary>
        /// 300 by 100 about the origin standing in the plane of normal (1, 1, 0)/√2, its major axis (h + Z)/√2 with
        /// h = (1, -1, 0)/√2, its minor axis N × U = (h - Z)/√2.
        /// </summary>
        private static GeoEllipse3 Standing()
        {
            GeoVector3 h = new GeoVector3(1, -1, 0).Normalize();
            GeoVector3 u = h.Add(GeoVector3.ZAxis).Normalize();
            return new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(1, 1, 0), u, 300, 100);
        }
    }
}
