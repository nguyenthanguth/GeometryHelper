using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The coder's smoke checks of the ellipse in space: that it answers as the circle in space does when its radii are
    /// equal, how it is seen square onto a plane, the round trip through the plane, moving it, what it refuses, and the
    /// nearest point of its rim against a dense sample.
    /// </summary>
    public class GeoEllipse3SmokeTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static GeoVector3 RandomUnit(Random random)
        {
            while (true)
            {
                var v = new GeoVector3(2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1);
                double length = v.Length;

                if (length > 0.1 && length <= 1.0)
                {
                    return v.Multiply(1.0 / length);
                }
            }
        }

        private static GeoPoint3 RandomPoint(Random random, double reach)
        {
            return new GeoPoint3(reach * (2 * random.NextDouble() - 1), reach * (2 * random.NextDouble() - 1), reach * (2 * random.NextDouble() - 1));
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double scale)
        {
            Assert.True(expected.DistanceTo(actual) <= 1E-12 * scale, $"{expected} against {actual}");
        }

        [Fact]
        public void AnEllipseMadeFromACircleAnswersAsTheCircleDoes()
        {
            var random = new Random(11);

            for (int i = 0; i < 200; i++)
            {
                double radius = 1 + 999 * random.NextDouble();
                var circle = new GeoCircle3(RandomPoint(random, 2000), RandomUnit(random), radius);
                GeoEllipse3 ellipse = GeoEllipse3.FromCircle(circle);
                double scale = radius + circle.Center.DistanceTo(GeoPoint3.Origin);

                Assert.Equal(circle.Length, ellipse.Length, 1E-12 * circle.Length);
                Assert.Equal(circle.Area, ellipse.Area, 1E-12 * circle.Area);

                for (int k = 0; k < 8; k++)
                {
                    double angle = 2 * Math.PI * random.NextDouble();
                    Near(circle.GetPointAtAngle(angle), ellipse.GetPointAtAngle(angle), scale);

                    double parameter = 3 * random.NextDouble() - 1;
                    Near(circle.GetPointAtParameter(parameter), ellipse.GetPointAtParameter(parameter), scale);
                    Assert.Equal(circle.GetDistanceAtParameter(parameter), ellipse.GetDistanceAtParameter(parameter), 1E-12 * circle.Length);

                    double distance = 3 * circle.Length * random.NextDouble() - circle.Length;
                    Near(circle.GetPointAtDistance(distance), ellipse.GetPointAtDistance(distance), scale);
                    Assert.Equal(circle.GetParameterAtDistance(distance), ellipse.GetParameterAtDistance(distance), 1E-12);

                    GeoPoint3 point = circle.Center.Add(RandomPoint(random, 2 * radius).GetVectorTo(GeoPoint3.Origin));
                    Near(circle.GetClosestPointOnBoundary(point), ellipse.GetClosestPointOnBoundary(point), scale + 2 * radius);
                    Near(circle.GetClosestPointOnSurface(point), ellipse.GetClosestPointOnSurface(point), scale + 2 * radius);
                    Assert.Equal(circle.DistanceTo(point), ellipse.DistanceTo(point), 1E-12 * (scale + 2 * radius));
                    Assert.Equal(circle.GetParameterAtPoint(point, Tol), ellipse.GetParameterAtPoint(point), 1E-12);

                    // A point on the disc, on the rim and a hair off the plane are read alike.
                    GeoPoint3 onRim = circle.GetPointAtAngle(angle);
                    GeoPoint3 inside = circle.Center.Add(circle.Center.GetVectorTo(onRim).Multiply(random.NextDouble() * 0.99));
                    GeoPoint3 above = inside.Add(circle.Normal.Multiply(0.002));
                    Assert.Equal(circle.Locate(onRim, Tol), ellipse.Locate(onRim, Tol));
                    Assert.Equal(circle.Locate(inside, Tol), ellipse.Locate(inside, Tol));
                    Assert.Equal(circle.Locate(above, Tol), ellipse.Locate(above, Tol));
                    Assert.Equal(circle.Contains(point, Tol), ellipse.Contains(point, Tol));
                    Assert.Equal(circle.IsPointOn(onRim, Tol), ellipse.IsPointOn(onRim, Tol));
                }

                GeoAabb3 box = circle.GetAabb();
                GeoAabb3 ellipseBox = ellipse.GetAabb();
                Near(box.Min, ellipseBox.Min, scale);
                Near(box.Max, ellipseBox.Max, scale);

                Assert.True(ellipse.IsCircle(Tol));
                Assert.True(ellipse.IsValid);
            }
        }

        [Fact]
        public void AnEllipseOfEqualRadiiWithAnyAxisAnswersAsTheCircleWhereTheAngleDoesNotMatter()
        {
            var random = new Random(12);

            for (int i = 0; i < 100; i++)
            {
                double radius = 1 + 999 * random.NextDouble();
                var circle = new GeoCircle3(RandomPoint(random, 2000), RandomUnit(random), radius);
                var ellipse = new GeoEllipse3(circle.Center, circle.Normal, RandomUnit(random), radius, radius);
                double scale = radius + circle.Center.DistanceTo(GeoPoint3.Origin);

                for (int k = 0; k < 8; k++)
                {
                    GeoPoint3 point = circle.Center.Add(RandomPoint(random, 2 * radius).GetVectorTo(GeoPoint3.Origin));
                    Near(circle.GetClosestPointOnBoundary(point), ellipse.GetClosestPointOnBoundary(point), scale + 2 * radius);
                    Assert.Equal(circle.DistanceTo(point), ellipse.DistanceTo(point), 1E-12 * (scale + 2 * radius));
                }

                Assert.True(ellipse.IsEqualTo(GeoEllipse3.FromCircle(circle), Tol));
            }
        }

        [Fact]
        public void ACircleOfRadius300Tilted60DegreesFromThePlanIsAnEllipse300By150InPlan()
        {
            // Tilted about the Y axis: the rim keeps its full width along Y and is foreshortened along X by cos 60°.
            double tilt = Math.PI / 3;
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(Math.Sin(tilt), 0, Math.Cos(tilt)), 300);
            var plan = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);

            GeoEllipse3 seen = circle.ProjectToPlane(plan);

            Assert.Equal(300.0, seen.MajorRadius, 1E-9);
            Assert.Equal(150.0, seen.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(seen.MajorAxis.Y), 1E-12);
            Assert.Equal(GeoVector3.ZAxis, seen.Normal);
            Near(new GeoPoint3(10, 20, 0), seen.Center, 300);

            GeoEllipse2 laid = circle.ProjectToEllipse2(GeoCoordinateSystem3.Global);

            Assert.Equal(300.0, laid.MajorRadius, 1E-9);
            Assert.Equal(150.0, laid.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(laid.MajorAxis.Y), 1E-12);
            Assert.Equal(10.0, laid.Center.X, 1E-12);
            Assert.Equal(20.0, laid.Center.Y, 1E-12);

            // Tilted the other way it is the same ellipse.
            var away = new GeoCircle3(circle.Center, new GeoVector3(-Math.Sin(tilt), 0, Math.Cos(tilt)), 300);
            Assert.True(away.ProjectToPlane(plan).IsEqualTo(seen, Tol));

            // Every point of the circle projected lies on the rim of what is seen.
            for (int i = 0; i < 36; i++)
            {
                GeoPoint3 p = circle.GetPointAtParameter(i / 36.0);
                Assert.True(seen.IsPointOn(new GeoPoint3(p.X, p.Y, 0), Tol));
            }
        }

        [Fact]
        public void ACircleParallelToThePlaneIsSeenAsTheSameCircleItsAngleStartingWhereItDid()
        {
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 70), new GeoVector3(0, 0, 1), 120);
            GeoEllipse3 seen = circle.ProjectToPlane(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));

            Assert.Equal(seen.MajorRadius, seen.MinorRadius, 1E-12 * 120);

            for (int i = 0; i < 12; i++)
            {
                GeoPoint3 p = circle.GetPointAtAngle(i * Math.PI / 6);
                Near(new GeoPoint3(p.X, p.Y, 0), seen.GetPointAtAngle(i * Math.PI / 6), 200);
            }
        }

        [Fact]
        public void AnEllipseLiftedIntoAFrameAndLaidBackOutIsTheSameEllipse()
        {
            var random = new Random(13);

            for (int i = 0; i < 300; i++)
            {
                var frame = new GeoCoordinateSystem3(RandomPoint(random, 5000), RandomUnit(random), RandomUnit(random));
                double a = 1 + 999 * random.NextDouble();
                double b = a / (1 + 999 * random.NextDouble());
                double angle = 2 * Math.PI * random.NextDouble();
                var flat = new GeoEllipse2(new GeoPoint2(3000 * random.NextDouble(), -3000 * random.NextDouble()), new GeoVector2(Math.Cos(angle), Math.Sin(angle)), a, b);

                GeoEllipse3 lifted = flat.ToEllipse3(frame);

                Assert.Equal(flat.MajorRadius, lifted.MajorRadius);
                Assert.Equal(flat.MinorRadius, lifted.MinorRadius);
                Assert.Equal(frame.ZAxis, lifted.Normal);
                Assert.True(lifted.IsValid);

                GeoEllipse2 back = lifted.ProjectToEllipse2(frame);
                double size = a + 5000;

                Assert.Equal(flat.MajorRadius, back.MajorRadius, 1E-12 * a);
                Assert.Equal(flat.MinorRadius, back.MinorRadius, 1E-12 * a);
                Assert.True(flat.Center.DistanceTo(back.Center) <= 1E-12 * size);
                Assert.True(Math.Abs(Math.Abs(flat.MajorAxis.DotProduct(back.MajorAxis)) - 1.0) <= 1E-12);

                // The angle runs as it did in the plane.
                double t = 2 * Math.PI * random.NextDouble();
                GeoPoint2 there = flat.GetPointAtAngle(t);
                Near(frame.ToGlobal(new GeoPoint3(there.X, there.Y, 0)), lifted.GetPointAtAngle(t), size);
            }
        }

        [Fact]
        public void ATransformationAndItsInverseBringTheEllipseBack()
        {
            var random = new Random(14);

            for (int i = 0; i < 200; i++)
            {
                double a = 1 + 999 * random.NextDouble();
                double b = a / (1 + 99 * random.NextDouble());
                var ellipse = new GeoEllipse3(RandomPoint(random, 2000), RandomUnit(random), RandomUnit(random), a, b);

                var matrix = new double[4, 4];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        matrix[r, c] = (r == c ? 1.5 : 0.0) + (random.NextDouble() - 0.5);
                    }

                    matrix[r, 3] = 1000 * (random.NextDouble() - 0.5);
                }

                matrix[3, 3] = 1.0;
                var transform = new GeoTransform3(matrix);

                GeoEllipse3 moved = ellipse.TransformBy(transform);
                GeoEllipse3 back = moved.TransformBy(transform.Inverse(Tol));

                Assert.True(back.IsEqualTo(ellipse, new Tolerance(1E-7, 1E-9)), $"{ellipse} came back as {back}");
                Assert.True(moved.IsValid);

                // Every point of the old rim goes onto the new one.
                for (int k = 0; k < 12; k++)
                {
                    GeoPoint3 image = transform.Transform(ellipse.GetPointAtParameter(k / 12.0));
                    Assert.True(moved.IsPointOn(image, Tol), $"{image} is {moved.DistanceTo(image)} off {moved}");
                }
            }
        }

        [Fact]
        public void AnUnevenScalingStretchesACircleIntoAnEllipse()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100);
            GeoEllipse3 stretched = GeoEllipse3.FromCircle(circle).TransformBy(GeoTransform3.Scaling(3, 1, 1));

            Assert.Equal(300.0, stretched.MajorRadius, 1E-9);
            Assert.Equal(100.0, stretched.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(stretched.MajorAxis.X), 1E-12);
            Assert.Throws<InvalidOperationException>(() => circle.TransformBy(GeoTransform3.Scaling(3, 1, 1)));
        }

        [Fact]
        public void AMirrorTurnsTheNormalWhereItTurnsACirclesAndKeepsTheRim()
        {
            var random = new Random(15);

            for (int i = 0; i < 50; i++)
            {
                double radius = 10 + 990 * random.NextDouble();
                var circle = new GeoCircle3(RandomPoint(random, 2000), RandomUnit(random), radius);
                var mirror = GeoTransform3.Mirror(new GeoPlane3(RandomPoint(random, 500), RandomUnit(random)));

                GeoCircle3 mirroredCircle = circle.TransformBy(mirror);
                GeoEllipse3 mirrored = GeoEllipse3.FromCircle(circle).TransformBy(mirror);

                Assert.True(mirrored.Normal.IsEqualTo(mirroredCircle.Normal, new Tolerance(1E-9, 1E-12)));
                Near(mirroredCircle.Center, mirrored.Center, radius + 3000);
                Assert.Equal(mirroredCircle.Radius, mirrored.MajorRadius, 1E-12 * radius);
                Assert.Equal(mirroredCircle.Radius, mirrored.MinorRadius, 1E-12 * radius);

                // The zero of the angle goes where the old one went; the angle then runs the other way round.
                Near(mirror.Transform(circle.GetPointAtAngle(0)), mirrored.GetPointAtAngle(0), radius + 3000);
                Near(mirror.Transform(circle.GetPointAtAngle(0.3)), mirrored.GetPointAtAngle(-0.3), radius + 3000);
            }

            // An ellipse in the plan mirrored in the plan faces down.
            var plan = new GeoEllipse3(new GeoPoint3(0, 0, 5), GeoVector3.ZAxis, GeoVector3.XAxis, 300, 100);
            GeoEllipse3 down = plan.TransformBy(GeoTransform3.Mirror(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis)));
            Assert.Equal(-1.0, down.Normal.Z, 1E-15);
            Assert.Equal(-5.0, down.Center.Z, 1E-12);
            Assert.Equal(300.0, down.MajorRadius, 1E-9);
            Assert.Equal(100.0, down.MinorRadius, 1E-9);
        }

        [Fact]
        public void TranslateKeepsEveryFieldButTheCentreExactly()
        {
            var ellipse = new GeoEllipse3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 2, 3), new GeoVector3(3, -1, 0.2), 300, 100);
            GeoEllipse3 moved = ellipse.Translate(new GeoVector3(10, 20, 30));

            Assert.Equal(ellipse.Normal, moved.Normal);
            Assert.Equal(ellipse.MajorAxis, moved.MajorAxis);
            Assert.Equal(ellipse.MajorRadius, moved.MajorRadius);
            Assert.Equal(ellipse.MinorRadius, moved.MinorRadius);
            Assert.Equal(new GeoPoint3(11, 22, 33), moved.Center);
        }

        [Fact]
        public void AFlatteningTransformationAndAnEllipseSeenEdgeOnAreRefused()
        {
            var plan = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 100);

            Assert.Throws<InvalidOperationException>(() => plan.TransformBy(GeoTransform3.Scaling(1, 0, 1)));
            Assert.Throws<InvalidOperationException>(() => plan.TransformBy(GeoTransform3.Scaling(0, 0, 0)));
            Assert.Throws<ArgumentNullException>(() => plan.TransformBy(null));

            // Refused wherever the circle's plane is, even flattened along Z, which would keep the plane (B3).
            Assert.Throws<InvalidOperationException>(() => plan.TransformBy(GeoTransform3.Scaling(1, 1, 0)));
            Assert.Throws<InvalidOperationException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100).TransformBy(GeoTransform3.Scaling(1, 1, 0)));

            // A squash that can be undone gives a thin ellipse.
            GeoEllipse3 squashed = plan.TransformBy(GeoTransform3.Scaling(1, 1E-4, 1));
            Assert.True(squashed.IsValid);
            Assert.Equal(300.0, squashed.MajorRadius, 1E-9);
            Assert.Equal(0.01, squashed.MinorRadius, 1E-12);

            var wall = new GeoPlane3(GeoPoint3.Origin, GeoVector3.YAxis);
            Assert.Throws<InvalidOperationException>(() => plan.ProjectToPlane(wall));
            Assert.Throws<InvalidOperationException>(() => plan.ProjectToEllipse2(new GeoCoordinateSystem3(wall)));
            Assert.Throws<InvalidOperationException>(() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.XAxis, 5).ProjectToPlane(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis)));

            // Tilted a hair off edge-on it is a thin ellipse, not a refusal.
            var nearly = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 1, 1E-6), GeoVector3.XAxis, 300, 100);
            GeoEllipse3 thin = nearly.ProjectToPlane(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));
            Assert.Equal(300.0, thin.MajorRadius, 1E-9);
            Assert.Equal(1E-4, thin.MinorRadius, 1E-12);
        }

        [Fact]
        public void TheConstructorRefusesWhatDrawsNoEllipse()
        {
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.Zero, GeoVector3.XAxis, 3, 1));
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.Zero, 3, 1));
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, new GeoVector3(0, 0, 5), 3, 1));
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, new GeoVector3(double.NaN, 0, 0), 3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 3, 0));
            Assert.False(default(GeoEllipse3).IsValid);

            // A tilted axis is laid into the plane.
            var laid = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, new GeoVector3(1, 0, 1), 3, 1);
            Assert.Equal(GeoVector3.XAxis, laid.MajorAxis);
            Assert.Equal(GeoVector3.YAxis, laid.MinorAxis);
        }

        [Fact]
        public void TheNearestPointOfTheRimAgreesWithADenseSample()
        {
            var random = new Random(16);
            double worst = 0.0;

            for (int i = 0; i < 200; i++)
            {
                double a = 1 + 999 * random.NextDouble();
                double b = a / (1 + 999 * random.NextDouble());
                var ellipse = new GeoEllipse3(RandomPoint(random, 2000), RandomUnit(random), RandomUnit(random), a, b);
                GeoPoint3 point = ellipse.Center.Add(GeoPoint3.Origin.GetVectorTo(RandomPoint(random, 1.5 * a)));

                double best = double.MaxValue;
                double bestT = 0.0;
                const int Samples = 20000;

                for (int k = 0; k < Samples; k++)
                {
                    double t = 2 * Math.PI * k / Samples;
                    double gap = ellipse.GetPointAtAngle(t).DistanceTo(point);

                    if (gap < best)
                    {
                        best = gap;
                        bestT = t;
                    }
                }

                // Refined about the best sample by golden-section search.
                double lo = bestT - 2 * Math.PI / Samples;
                double hi = bestT + 2 * Math.PI / Samples;

                for (int k = 0; k < 200; k++)
                {
                    double m1 = hi - 0.618033988749895 * (hi - lo);
                    double m2 = lo + 0.618033988749895 * (hi - lo);

                    if (ellipse.GetPointAtAngle(m1).DistanceTo(point) < ellipse.GetPointAtAngle(m2).DistanceTo(point))
                    {
                        hi = m2;
                    }
                    else
                    {
                        lo = m1;
                    }
                }

                double oracle = Math.Min(best, ellipse.GetPointAtAngle(0.5 * (lo + hi)).DistanceTo(point));
                GeoPoint3 closest = ellipse.GetClosestPointOnBoundary(point);
                double found = closest.DistanceTo(point);

                Assert.True(ellipse.IsPointOn(closest, Tol));
                worst = Math.Max(worst, (found - oracle) / a);
                Assert.True(found <= oracle + 1E-9 * a, $"{found} against {oracle} for {ellipse} and {point}");

                // Off the disc the distance to the disc is the distance to the rim.
                if (!ellipse.Contains(ellipse.GetPlane().Project(point), Tol))
                {
                    Assert.Equal(found, ellipse.DistanceTo(point), 1E-9 * a);
                }
            }

            Assert.True(worst <= 1E-9);
        }

        [Fact]
        public void APointAboveTheCentreOrTheMajorAxisOfATiltedEllipseIsReadOnTheAxisDespiteRounding()
        {
            var random = new Random(17);

            for (int i = 0; i < 2000; i++)
            {
                var ellipse = new GeoEllipse3(RandomPoint(random, 1000), RandomUnit(random), RandomUnit(random), 300, i % 2 == 0 ? 100 : 300);

                GeoPoint3 aboveCentre = ellipse.Center.Add(ellipse.Normal.Multiply(50));
                Assert.Equal(Math.PI / 2, ellipse.GetAngleAtPoint(aboveCentre), 1E-12);
                Assert.Equal(0.25, ellipse.GetParameterAtPoint(aboveCentre), 1E-12);

                if (i % 2 == 1)
                {
                    continue;
                }

                // Above the major axis inside, two points of the rim are equally near; the one on the positive side is given.
                GeoPoint3 aboveAxis = ellipse.Center.Add(ellipse.MajorAxis.Multiply(50)).Add(ellipse.Normal.Multiply(-20));
                GeoPoint3 closest = ellipse.GetClosestPointOnBoundary(aboveAxis);
                Assert.True(ellipse.Center.GetVectorTo(closest).DotProduct(ellipse.MinorAxis) > 0.0);
            }
        }

        [Fact]
        public void LengthAndParameterWrapRoundTheRimAsTheCirclesDo()
        {
            var ellipse = new GeoEllipse3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), new GeoVector3(1, -1, 0), 300, 100);
            var flat = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 300, 100);
            double length = ellipse.Length;

            Assert.Equal(flat.GetDistanceAtParameter(0.3), ellipse.GetDistanceAtParameter(0.3));
            Assert.Equal(ellipse.GetDistanceAtParameter(0.3), ellipse.GetDistanceAtParameter(1.3), 1E-9);
            Assert.Equal(length - ellipse.GetDistanceAtParameter(0.1), ellipse.GetDistanceAtParameter(-0.1), 1E-9);
            Assert.Equal(flat.GetParameterAtDistance(400), ellipse.GetParameterAtDistance(400));
            Assert.Equal(ellipse.GetParameterAtDistance(400), ellipse.GetParameterAtDistance(400 + length), 1E-12);

            // A foot within the vector tolerance of the centre is read at the centre, the end of the minor axis.
            GeoPoint3 nearCentre = ellipse.Center.Add(ellipse.MajorAxis.Multiply(5E-6)).Add(ellipse.Normal.Multiply(30));
            Assert.Equal(0.25, ellipse.GetParameterAtPoint(nearCentre, Tol));
            Assert.Equal(0.25 * length, ellipse.GetDistanceAtPoint(nearCentre, Tol), 1E-9);
        }

        [Fact]
        public void APointIsOnTheDiscOnlyWithinThePlanarToleranceOfThePlane()
        {
            var ellipse = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 100);

            Assert.Equal(PointLocation.Inside, ellipse.Locate(new GeoPoint3(100, 50, 0), Tol));
            Assert.Equal(PointLocation.Inside, ellipse.Locate(new GeoPoint3(100, 50, 0.0009), Tol));
            Assert.Equal(PointLocation.OutSide, ellipse.Locate(new GeoPoint3(100, 50, 0.0011), Tol));
            Assert.Equal(PointLocation.OnSide, ellipse.Locate(new GeoPoint3(300.0009, 0, 0), Tol));
            Assert.Equal(PointLocation.OnSide, ellipse.Locate(new GeoPoint3(0, 99.9991, 0.0005), Tol));
            Assert.Equal(PointLocation.OutSide, ellipse.Locate(new GeoPoint3(0, 100.002, 0), Tol));
            Assert.Equal(50.0, ellipse.DistanceTo(new GeoPoint3(0, 0, 50)));
            Assert.Equal(50.0, ellipse.DistanceTo(new GeoPoint3(330, 0, 40)), 1E-12);
            Assert.Equal(new GeoPoint3(0, 100, 0), ellipse.GetClosestPointOnBoundary(new GeoPoint3(0, 0, 7)));
            Assert.Equal(new GeoPoint3(10, 20, 0), ellipse.GetClosestPointOnSurface(new GeoPoint3(10, 20, 9)));
        }

        [Fact]
        public void TheFlatteningAndTheMeshAreThoseOfThePlaneLiftedIntoTheEllipsesPlane()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(100, 200, 300), new GeoVector3(1, 1, 0.3), new GeoVector3(-1, 1, 2));
            var flat = new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 1000, 1);
            GeoEllipse3 ellipse = flat.ToEllipse3(frame);

            GeoPolygon2 polygon2 = flat.ToPolygon();
            GeoPolygon3 polygon3 = ellipse.ToPolygon();

            Assert.Equal(polygon2.Vertices.Count, polygon3.Vertices.Count);

            for (int i = 0; i < polygon2.Vertices.Count; i++)
            {
                Near(frame.ToGlobal(new GeoPoint3(polygon2.Vertices[i].X, polygon2.Vertices[i].Y, 0)), polygon3.Vertices[i], 2000);
            }

            Assert.True(polygon3.Normal.IsEqualTo(ellipse.Normal, Tol));
            Assert.Equal(flat.ToPolyline(24).Vertices.Count, ellipse.ToPolyline(24).Vertices.Count);
            Assert.Equal(flat.ToPolygonBySpacing(50).Vertices.Count, ellipse.ToPolygonBySpacing(50).Vertices.Count);

            var round = new GeoEllipse3(new GeoPoint3(5, 5, 5), new GeoVector3(0, 1, 1), GeoVector3.XAxis, 300, 100);
            GeometryHelper.Meshing.GeoMesh3 mesh = round.ToMesh(GeometryHelper.Meshing.MeshKind.Triangles);
            Assert.True(mesh.FaceCount > 0);
            Assert.Equal(round.ToPolygon().Area, mesh.Area, 1E-6 * round.Area);

            var sliver = new GeoEllipse3(GeoPoint3.Origin, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 0.0005);
            Assert.Equal(0, sliver.ToMesh(new GeometryHelper.Meshing.MeshOptions(GeometryHelper.Meshing.MeshKind.Triangles), GeometryHelper.Meshing.MeshPlacement3.World, Tol).FaceCount);
        }

        [Fact]
        public void AGridLaidOnTheEllipsesOwnAxesRunsAlongItsMajorAxisAsOverItsFlattenedOutline()
        {
            // The ellipse 1000 by 200 in the plan with its axis along (1, 1, 0), turned 0.5 rad about X.
            double cos = Math.Cos(0.5), sin = Math.Sin(0.5);
            var normal = new GeoVector3(0, -sin, cos);
            var axis = new GeoVector3(1, cos, sin).Multiply(1 / Math.Sqrt(2));
            var ellipse = new GeoEllipse3(new GeoPoint3(10, 20, 30), normal, axis, 1000, 200);
            var grid = GeometryHelper.Meshing.MeshOptions.Grid(200, 200);
            var own = GeometryHelper.Meshing.MeshPlacement3.Own;

            GeometryHelper.Meshing.GeoMesh3 mesh = ellipse.ToMesh(grid, own, Tol);
            GeometryHelper.Meshing.GeoMesh3 outline = ellipse.ToPolygon().ToMesh(grid, own, Tol);

            Assert.True(Math.Abs(mesh.Frame.XAxis.DotProduct(ellipse.MajorAxis)) >= 1 - 1E-12, $"{mesh.Frame.XAxis} against {ellipse.MajorAxis}");
            Assert.Equal(outline.FaceCount, mesh.FaceCount);
            Assert.Equal(20, mesh.FaceCount);

            // A circle has no axis of its own, and its grid runs level, as a circle's does.
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), normal, 500);
            Assert.True(GeoEllipse3.FromCircle(circle).ToMesh(grid, own, Tol).Frame.XAxis.IsEqualTo(circle.ToMesh(grid, own, Tol).Frame.XAxis, Tol));
        }
    }
}
