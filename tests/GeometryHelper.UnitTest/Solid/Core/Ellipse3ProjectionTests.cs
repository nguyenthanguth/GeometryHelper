using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The rim point nearest a point in space, and the ellipses made by projection: a circle or an ellipse seen square
    /// onto a plane (decision 9), laid out in a frame as a <see cref="GeoEllipse2"/> (decision 10), and a 2D ellipse
    /// lifted into a frame (decision 2). The rim is read as the circle reads its rim (Projection3.cs:723-735), and every
    /// rim point stands at the height of the plane, so the nearest one is the nearest to the foot of the point.
    /// </summary>
    public class Ellipse3ProjectionTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        #region The nearest rim point

        [Fact]
        public void TheNearestRimPointToAPointInSpaceMatchesADenseSample()
        {
            // Inside, outside, above and below, near the tight end of the major axis and past its centre of curvature, far
            // off and close to the rim: the rim point found is on the rim and on the plane, and as near as a dense sample's.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            GeoPoint3[] probes =
            {
                specimen.At(150, 40, 0), specimen.At(150, 40, 90), specimen.At(-280, 5, -15), specimen.At(290, -3, 0),
                specimen.At(100, 0, 40), specimen.At(-500, 400, 250), specimen.At(0, -101, 0), specimen.At(2000, -1500, -800),
                specimen.At(10, 99.9, 0.5), specimen.At(-299.9, 0.01, 0),
            };

            foreach (GeoPoint3 probe in probes)
            {
                Ellipse3Oracle.ClosestOnRim(specimen, probe, out double expected);
                GeoPoint3 closest = ellipse.GetClosestPointOnBoundary(probe);

                Assert.Equal(expected, closest.DistanceTo(probe), 1E-9);
                Assert.InRange(Ellipse3Oracle.GapToRim(ellipse, closest, out double offPlane), 0.0, 1E-9);
                Assert.Equal(0.0, offPlane, 1E-9);
            }
        }

        [Fact]
        public void APointOfThePlaneGetsTheRimPointTheEllipseLaidOutGivesIt()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            foreach (GeoPoint3 point in new[] { specimen.At(150, 40, 0), specimen.At(-420, 260, 0), specimen.At(250, -10, 0), specimen.At(100, 0, 0) })
            {
                GeoPoint3 expected = specimen.Lift(flat.GetClosestPointOnBoundary(specimen.Foot(point)));
                Assert.True(ellipse.GetClosestPointOnBoundary(point).IsEqualTo(expected, Tight));
            }
        }

        [Fact]
        public void TheCentreAndThePointsAboveItGetTheEndOfTheMinorAxis()
        {
            // 2D decision 9 and A1: C + b V, at t = π/2, whether a is b or not, from the centre and straight above or below
            // it. The circle gives its zero-parameter point there instead (Projection3.cs:727-730).
            foreach (Ellipse3Oracle.Specimen specimen in new[] { Ellipse3Oracle.Tilted(), Ellipse3Oracle.Tilted(200, 200) })
            {
                GeoEllipse3 ellipse = specimen.Build();

                foreach (double h in new[] { 0.0, 70.0, -70.0 })
                {
                    Assert.True(ellipse.GetClosestPointOnBoundary(specimen.At(0, 0, h)).IsEqualTo(specimen.At(0, specimen.B, 0), Tight));
                }
            }
        }

        #endregion

        #region Seen square onto a plane

        [Fact]
        public void ACircleTiltedSixtyDegreesIsSeenInPlanAsAnEllipseThreeHundredByOneHundredAndFifty()
        {
            // Radius 300, its plane through X and 60 degrees up from Y: in plan 300 along X and 300 cos 60° = 150 across,
            // π 300 150 = 141 371.67 in area, about the centre's foot on the plane z = -40.
            GeoCircle3 circle = Ellipse3Oracle.CircleTiltedSixty();
            var plan = new GeoPlane3(new GeoPoint3(0, 0, -40), GeoVector3.ZAxis);
            GeoEllipse3 seen = circle.ProjectToPlane(plan);

            Assert.Equal(300.0, seen.MajorRadius, 1E-9);
            Assert.Equal(150.0, seen.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(seen.MajorAxis.X), 1E-12);
            Assert.True(seen.Normal.IsEqualTo(GeoVector3.ZAxis, Tight));
            Assert.True(seen.Center.IsEqualTo(new GeoPoint3(40, -25, -40), Tight));
            Assert.Equal(Math.PI * 300 * 150, seen.Area, 1E-6);

            // The ellipse made from the circle is seen the same.
            Assert.True(GeoEllipse3.FromCircle(circle).ProjectToPlane(plan).IsEqualTo(seen, Tight));
        }

        [Fact]
        public void ProjectingDrawsTheShadowOfTheRimInThePlane()
        {
            // The tilted 300 by 100 onto an oblique plane: the foot of every rim point lies on the rim of what comes back,
            // and its normal is the plane's.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var plane = new GeoPlane3(new GeoPoint3(10, 20, -30), new GeoVector3(0.2, -0.3, 1));
            GeoEllipse3 seen = specimen.Build().ProjectToPlane(plane);

            Assert.True(seen.Normal.IsEqualTo(plane.Normal, Tight));

            for (int k = 0; k < 24; k++)
            {
                GeoPoint3 rim = specimen.Rim(0.05 + k * 2 * Math.PI / 24);
                GeoPoint3 foot = rim.Subtract(plane.Normal.Multiply(plane.Origin.GetVectorTo(rim).DotProduct(plane.Normal)));

                Assert.InRange(Ellipse3Oracle.GapToRim(seen, foot, out double offPlane), 0.0, 1E-9);
                Assert.Equal(0.0, offPlane, 1E-9);
            }
        }

        [Fact]
        public void TheNormalIsThePlanesWhicheverWayTheShapeLeans()
        {
            // The same disc with its normal one way and the other gives the same shadow, its normal the plane's; so do
            // the circle tilted 60 degrees one way and 60 the other. A plane facing down gives a shadow facing down.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var plane = new GeoPlane3(new GeoPoint3(10, 20, -30), new GeoVector3(0.2, -0.3, 1));
            GeoEllipse3 towards = specimen.Build().ProjectToPlane(plane);
            GeoEllipse3 away = specimen.Flipped().Build().ProjectToPlane(plane);

            Assert.True(towards.IsEqualTo(away, Tight));
            Assert.True(towards.Normal.IsEqualTo(plane.Normal, Tight));
            Assert.True(away.Normal.IsEqualTo(plane.Normal, Tight));

            GeoEllipse3 up = Ellipse3Oracle.CircleTiltedSixty(60).ProjectToPlane(GeoPlane3.XY);
            GeoEllipse3 down = Ellipse3Oracle.CircleTiltedSixty(-60).ProjectToPlane(GeoPlane3.XY);
            Assert.True(up.IsEqualTo(down, Tight));

            var facingDown = new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 0, -1));
            Assert.True(Ellipse3Oracle.CircleTiltedSixty().ProjectToPlane(facingDown).Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));
        }

        [Fact]
        public void ACircleParallelToThePlaneStaysACircleWithItsZeroWhereFromCircleHasIt()
        {
            // A circle of 120 at height 50, seen onto z = 0: a circle of 120 to 1E-12 of its size, its major axis and the
            // point at t = 0 those of FromCircle, dropped 50.
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 50), GeoVector3.ZAxis, 120);
            GeoEllipse3 made = GeoEllipse3.FromCircle(circle);
            GeoEllipse3 seen = circle.ProjectToPlane(GeoPlane3.XY);

            Assert.Equal(120.0, seen.MajorRadius, 1E-12 * 120);
            Assert.Equal(120.0, seen.MinorRadius, 1E-12 * 120);
            Assert.True(seen.MajorAxis.IsEqualTo(made.MajorAxis, Tight));
            Assert.True(seen.GetPointAtAngle(0).IsEqualTo(made.GetPointAtAngle(0).Subtract(new GeoVector3(0, 0, 50)), Tight));

            // An ellipse parallel to the plane is only moved onto it.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var parallel = new GeoPlane3(specimen.At(0, 0, -25), specimen.N);
            GeoEllipse3 onto = specimen.Build().ProjectToPlane(parallel);
            Assert.True(onto.IsEqualTo(specimen.Build().Translate(specimen.N.Multiply(-25)), Tight));
        }

        [Fact]
        public void AShapeStandingSquareToThePlaneIsRefused()
        {
            // A circle in a plane holding Z, seen onto XY, is a segment. So is one leaning 1E-14 off square, a hundred
            // times under the 1E-12 the plane may keep.
            var standing = new GeoCircle3(new GeoPoint3(1, 2, 3), GeoVector3.YAxis, 300);
            var nearly = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1E-14), 300);

            foreach (GeoCircle3 circle in new[] { standing, nearly })
            {
                Assert.Throws<InvalidOperationException>(() => circle.ProjectToPlane(GeoPlane3.XY));
                Assert.Throws<InvalidOperationException>(() => GeoEllipse3.FromCircle(circle).ProjectToPlane(GeoPlane3.XY));
                Assert.Throws<InvalidOperationException>(() => circle.ProjectToEllipse2(GeoCoordinateSystem3.Global));
                Assert.Throws<InvalidOperationException>(() => GeoEllipse3.FromCircle(circle).ProjectToEllipse2(GeoCoordinateSystem3.Global));
            }
        }

        [Fact]
        public void AShapeLeaningAHairOffSquareIsSeenAsAThinEllipse()
        {
            // Leaning 1E-6 off square, a million times over the bound: 300 by 300 × 1E-6 = 3E-4, returned (2D decision 17),
            // the minor radius to a millionth of itself.
            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1E-6), 300);
            GeoEllipse3 seen = circle.ProjectToPlane(GeoPlane3.XY);

            Assert.Equal(300.0, seen.MajorRadius, 1E-9);
            Assert.Equal(3E-4, seen.MinorRadius, 3E-10);
            Assert.Equal(1.0, Math.Abs(seen.MajorAxis.X), 1E-12);
        }

        #endregion

        #region Between the plane and space

        [Fact]
        public void TheCircleTiltedSixtyDegreesLaidOutInPlanIsAnEllipseThreeHundredByOneHundredAndFifty()
        {
            GeoEllipse2 plan = Ellipse3Oracle.CircleTiltedSixty().ProjectToEllipse2(GeoCoordinateSystem3.Global);

            Assert.Equal(300.0, plan.MajorRadius, 1E-9);
            Assert.Equal(150.0, plan.MinorRadius, 1E-9);
            Assert.Equal(1.0, Math.Abs(plan.MajorAxis.X), 1E-12);
            Assert.True(plan.Center.IsEqualTo(new GeoPoint2(40, -25), Tight));
        }

        [Fact]
        public void LayingOutInAFrameReadsTheShadowInThatFramesCoordinates()
        {
            // The tilted 300 by 100 laid out in a frame standing at (100, 200, 300), turned and leaning: each rim point,
            // dropped onto the frame's plane and read in its coordinates, lies on the rim of what comes back.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            var frame = new GeoCoordinateSystem3(new GeoPoint3(100, 200, 300), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1));
            GeoEllipse2 laid = specimen.Build().ProjectToEllipse2(frame);

            for (int k = 0; k < 24; k++)
            {
                GeoPoint2 local = PlanarMap.ProjectToPoint2(frame, specimen.Rim(0.05 + k * 2 * Math.PI / 24));
                Assert.InRange(Ellipse2Oracle.GapToRim(laid, local), 0.0, 1E-9);
            }
        }

        [Fact]
        public void AnEllipseLiftedIntoAFrameKeepsItsRadiiBitForBitAndItsAngle()
        {
            // 300 by 100 about (40, -25), its axis 30 degrees up, lifted into the leaning frame: the radii to the bit, the
            // centre where the frame puts (40, -25), the normal the frame's Z, the axis the frame's image of the 2D axis,
            // and each point at t the frame's image of the 2D point at t, since the frame is right-handed.
            GeoEllipse2 flat = Ellipse2Oracle.Tilted();
            var frame = new GeoCoordinateSystem3(new GeoPoint3(100, 200, 300), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1));
            GeoEllipse3 lifted = flat.ToEllipse3(frame);

            Assert.Equal(flat.MajorRadius, lifted.MajorRadius);
            Assert.Equal(flat.MinorRadius, lifted.MinorRadius);
            Assert.Equal(PlanarMap.ToPoint3(frame, flat.Center), lifted.Center);
            Assert.True(lifted.Normal.IsEqualTo(frame.ZAxis, Tight));
            Assert.True(lifted.MajorAxis.IsEqualTo(PlanarMap.ToVector3(frame, flat.MajorAxis), Tight));

            for (int k = -4; k < 20; k++)
            {
                double t = k * 0.39;
                Assert.True(lifted.GetPointAtAngle(t).IsEqualTo(PlanarMap.ToPoint3(frame, flat.GetPointAtAngle(t)), Tight), $"t = {t}");
            }
        }

        [Fact]
        public void LayingOutWhatWasLiftedGivesTheEllipseBack()
        {
            // Decision 10's round trip, in the world frame and in the leaning one: the centre and the radii to 1E-12 of the
            // size, the axis up to its sign. A circle, a 300 by 100 and a 1 000 by 1.
            var frames = new[]
            {
                GeoCoordinateSystem3.Global,
                new GeoCoordinateSystem3(new GeoPoint3(100, 200, 300), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1)),
            };
            var ellipses = new[]
            {
                Ellipse2Oracle.Tilted(),
                new GeoEllipse2(new GeoPoint2(-7, 13), new GeoVector2(1, -2), 1000, 1),
                GeoEllipse2.FromCircle(new GeoCircle2(new GeoPoint2(3, 4), 250)),
            };

            foreach (GeoCoordinateSystem3 frame in frames)
            {
                foreach (GeoEllipse2 flat in ellipses)
                {
                    GeoEllipse2 back = flat.ToEllipse3(frame).ProjectToEllipse2(frame);
                    double size = flat.MajorRadius;

                    Assert.True(back.Center.IsEqualTo(flat.Center, new Tolerance(1E-12 * size + 1E-12, 1E-12)));
                    Assert.Equal(flat.MajorRadius, back.MajorRadius, 1E-12 * size);
                    Assert.Equal(flat.MinorRadius, back.MinorRadius, 1E-12 * size);

                    if (!flat.IsCircle(Tight))
                    {
                        Assert.Equal(0.0, flat.MajorAxis.CrossProduct(back.MajorAxis), 1E-12);
                    }
                }
            }
        }

        #endregion
    }
}
