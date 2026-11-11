using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A spline in space against a partner far smaller or far larger than itself, which the fuzz of the public Try methods found working
    /// for more than ten seconds: a circle of radius 1E-4 or 1E-12 on a curve, and a circle of radius a million against a curve of a few
    /// tens or a circle of radius 1E-4. Each is answered in milliseconds, with the places dense sampling of the two curves finds.
    /// </summary>
    public class Spline3TinyAndHugePartnersTests
    {
        private static readonly Tolerance Default = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Tight = new Tolerance(1E-6, 1E-9);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static T Answered<T>(Func<T> ask, double seconds)
        {
            var watch = Stopwatch.StartNew();
            Task<T> task = Task.Run(ask);
            Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "no answer after 10 s");
            Assert.True(watch.Elapsed.TotalSeconds < seconds, "took " + watch.Elapsed.TotalSeconds + " s");

            return task.Result;
        }

        private static void AssertPlaces(GeoPoint3[] actual, double within, params GeoPoint3[] expected)
        {
            Assert.True(
                actual.Length == expected.Length,
                actual.Length + " points " + string.Join(" ", actual) + ", " + expected.Length + " expected"
            );

            foreach (GeoPoint3 place in expected)
            {
                Assert.True(
                    actual.Any(p => p.DistanceTo(place) <= within),
                    "no point near " + place + " in " + string.Join(" ", actual)
                );
            }
        }

        [Fact]
        public void AnEllipseOfTenAcrossACircleOfRadiusAMillion_IsCrossedTwice()
        {
            // As in the plane: the circle near (1E6, 0, 0) crosses the ellipse at y = 5 and -5, 1.25E-5 short of x = 1E6.
            GeoSpline3 circle = GeoSpline3.FromCircle(new GeoCircle3(P(0, 0, 0), new GeoVector3(0, 0, 1), 1E6));
            var ellipse = new GeoEllipse3(P(1E6, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 10, 5);

            GeoPoint3[] places = Answered(() => circle.GetIntersections(ellipse, Default), 1.0);

            AssertPlaces(places, 1E-6, P(1E6 - 1.25E-5, 5, 0), P(1E6 - 1.25E-5, -5, 0));
        }

        [Fact]
        public void ACircleOfRadius1E4AndACircleSplineOfRadiusAMillionFarFromIt_DoNotMeet()
        {
            // The circle of radius 1E-4 about the origin in the plane y = 0 is 100 from the tilted circle of radius a million about
            // (100, 0, 0), whose pieces were looked at every 3E-6 of their 1.5 million.
            var small = new GeoCircle3(P(0, 0, 0), new GeoVector3(0, 1, 0), 1E-4);
            GeoSpline3 huge = GeoSpline3.FromCircle(new GeoCircle3(P(100, 0, 0), new GeoVector3(1, 1, 0), 1E6));

            GeoPoint3[] places = Answered(() => small.GetIntersections(huge, Tight), 1.0);

            Assert.Empty(places);
            Assert.False(huge.TryIntersectWith(small, out _, Tight));
        }

        [Fact]
        public void ALineFromTheCentreOfACircleOfRadius1E4InItsPlane_MeetsItOnce()
        {
            // The sliver (0, 0, 0), (100, 0, 0), (50, 1E-7, 0) starts at the centre of the circle in the plane y = 0 and leaves it at (1E-4, 0, 0).
            GeoSpline3 sliver = GeoSpline3.FromControlPoints(1, new[] { P(0, 0, 0), P(100, 0, 0), P(50, 1E-7, 0) });
            var circle = new GeoCircle3(P(0, 0, 0), new GeoVector3(0, 1, 0), 1E-4);

            GeoPoint3[] places = Answered(() => sliver.GetIntersections(circle, Default), 1.0);
            GeoPoint3[] back = Answered(() => circle.GetIntersections(sliver, Default), 1.0);

            AssertPlaces(places, 1E-9, P(1E-4, 0, 0));
            AssertPlaces(back, 1E-9, P(1E-4, 0, 0));
        }

        [Theory]
        [InlineData(1E-12)]
        [InlineData(1E-6)]
        public void APolylineThroughTheCentreOfATinyCircleSpline_MeetsItOnce(double radius)
        {
            GeoSpline3 polyline = GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 0, 0), P(100, 0, 0), P(0, 100, 0)));
            GeoSpline3 circle = GeoSpline3.FromCircle(new GeoCircle3(P(100, 0, 0), new GeoVector3(0, 1, 0), radius));

            GeoPoint3[] places = Answered(() => polyline.GetIntersections(circle, Default), 1.0);

            AssertPlaces(places, 1E-3, P(100, 0, 0));
        }

        [Fact]
        public void ACurveThroughAnEllipseOfRadius1E12StandingUprightInItsPlane_MeetsIt_AndThrowsNothing()
        {
            // The curve lies in the floor; the circle of radius 1E-12 about (100, 0, 0) stands in the plane y = 0, in the floor within the
            // planar tolerance, being no wider, with its major axis square to it: laid out in the floor, it threw "The major axis needs a
            // direction".
            GeoSpline3 curve = GeoSpline3.Interpolate(
                new[] { P(0, 0, 0), P(100, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0), P(0, 0, 0) },
                new GeoVector3(0, 1, 0),
                new GeoVector3(0, 1, 0),
                3
            );
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(new GeoCircle3(P(100, 0, 0), new GeoVector3(0, 1, 0), 1E-12));

            Assert.True(curve.TryIntersectWith(ellipse, out GeoPoint3[] places, Default));
            Assert.True(ellipse.TryIntersectWith(curve, out GeoPoint3[] back, Default));

            AssertPlaces(places, 1E-3, P(100, 0, 0));
            AssertPlaces(back, 1E-3, P(100, 0, 0));
        }

        [Fact]
        public void ACurveThroughACircleSplineOfRadius1E12_MeetsIt_WithinATightTolerance()
        {
            // The curve through (0, 0, 0), (100, 0, 0) and (50, 0, 0), leaving straight down and ending straight up, passes through the
            // circle at (100, 0, 0), where the snap is more than the circle is across.
            GeoSpline3 curve = GeoSpline3.Interpolate(
                new[] { P(0, 0, 0), P(100, 0, 0), P(50, 0, 0) },
                new GeoVector3(0, 0, -1),
                new GeoVector3(0, 0, 1),
                2
            );
            GeoSpline3 circle = GeoSpline3.FromCircle(new GeoCircle3(P(100, 0, 0), new GeoVector3(0, 1, 0), 1E-12));

            GeoPoint3[] places = Answered(() => curve.GetIntersections(circle, Tight), 1.0);

            AssertPlaces(places, 1E-6, P(100, 0, 0));
        }
    }
}
