using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A spline against a partner far smaller or far larger than itself, which the fuzz of the public Try methods found working for
    /// more than ten seconds: a circle of radius 1E-4 or 1E-12 on a line, and a piece of a circle of radius a million against a curve
    /// of a few tens. Each is answered in milliseconds, and where the two meet they meet once, as dense sampling of the two finds.
    /// </summary>
    public class Spline2TinyAndHugePartnersTests
    {
        private static readonly Tolerance Default = new Tolerance(1E-3, 1E-5);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// Asks a question on another thread, and fails when it is not answered within ten seconds or takes longer than the time given.
        /// </summary>
        private static T Answered<T>(Func<T> ask, double seconds)
        {
            var watch = Stopwatch.StartNew();
            Task<T> task = Task.Run(ask);
            Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "no answer after 10 s");
            Assert.True(watch.Elapsed.TotalSeconds < seconds, "took " + watch.Elapsed.TotalSeconds + " s");

            return task.Result;
        }

        private static void AssertPlaces(GeoPoint2[] actual, double within, params GeoPoint2[] expected)
        {
            Assert.True(actual.Length == expected.Length, actual.Length + " points " + string.Join(" ", actual) + ", " + expected.Length + " expected");

            foreach (GeoPoint2 place in expected)
            {
                Assert.True(actual.Any(p => p.DistanceTo(place) <= within), "no point near " + place + " in " + string.Join(" ", actual));
            }
        }

        [Fact]
        public void AnEllipseOfTenAcrossThePieceOfACircleOfRadiusAMillion_IsCrossedTwice_AndTheHugePieceIsNotLookedAtAllAlong()
        {
            // The circle near (1E6, 0) is the line x = 1E6 - y * y / 2E6, which the ellipse of radii 10 and 5 about (1E6, 0) crosses at
            // y = 5 and -5, 1.25E-5 short of x = 1E6. The pieces of the circle are 1.5 million long, and were looked at every 0.31 of them
            // for a stretch along the ellipse: millions of looks, where now only the 20 or so of them near the ellipse are.
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 1E6));
            var ellipse = new GeoEllipse2(P(1E6, 0), GeoVector2.XAxis, 10, 5);

            GeoPoint2[] places = Answered(() => circle.GetIntersections(ellipse, Default), 1.0);
            GeoPoint2[] back = Answered(() => ellipse.GetIntersections(circle, Default), 1.0);

            AssertPlaces(places, 1E-6, P(1E6 - 1.25E-5, 5), P(1E6 - 1.25E-5, -5));
            AssertPlaces(back, 1E-6, P(1E6 - 1.25E-5, 5), P(1E6 - 1.25E-5, -5));
        }

        [Theory]
        [InlineData(1E-12)]
        [InlineData(1E-6)]
        [InlineData(1E-4)]
        public void APolylineThroughTheCentreOfATinyCircleSpline_MeetsItOnce(double radius)
        {
            // The circle is smaller than the point tolerance, 1E-3: all of it is within the tolerance of the polyline, and its two crossings
            // are one place. It lies along nothing, and is not looked for a stretch along: the circle of radius 1E-12 had the polyline looked at
            // every 3E-14 of its length near it.
            GeoSpline2 polyline = GeoSpline2.FromPolyline(new GeoPolyline2(P(0, 0), P(100, 0), P(0, 100)));
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(100, 0), radius));

            GeoPoint2[] places = Answered(() => polyline.GetIntersections(circle, Default), 1.0);
            GeoPoint2[] back = Answered(() => circle.GetIntersections(polyline, Default), 1.0);

            AssertPlaces(places, 1E-3, P(100, 0));
            AssertPlaces(back, 1E-3, P(100, 0));
            Assert.True(polyline.TryIntersectWith(circle, out GeoPoint2[] tried, Default));
            Assert.Single(tried);
        }

        [Fact]
        public void APolylineThatCrossesATinyEllipseManyTimes_IsAnsweredQuickly()
        {
            // Twenty edges through the middle of an ellipse of radius 1E-4, each within the tolerance of all of it. The ellipse, a part no
            // larger than the tolerance but not straight, was cut into 2 048 straight parts for each edge to be measured against, which
            // took 0.15 s an edge; it is measured as it is against the edge, which is straight.
            var corners = new GeoPoint2[41];

            // Each edge from an even corner passes through (100, 0), the middle of its corners; each from an odd one passes 1 above it.
            for (int i = 0; i <= 40; i++)
            {
                corners[i] = i % 2 == 0 ? P(50, 50 + i) : P(150, -49 - i);
            }

            GeoSpline2 polyline = GeoSpline2.FromPolyline(new GeoPolyline2(corners));
            GeoEllipse2 ellipse = GeoEllipse2.FromCircle(new GeoCircle2(P(100, 0), 1E-4));

            GeoPoint2[] places = Answered(() => polyline.GetIntersections(ellipse, Default), 1.0);

            Assert.NotEmpty(places);
            Assert.All(places, p => Assert.True(p.DistanceTo(P(100, 0)) <= 2E-4, p + " is not on the ellipse"));
        }

        [Fact]
        public void ACurveOfTwentyPetalsThroughATinyEllipse_IsAnsweredQuickly()
        {
            // A cubic through (100, 0) and a point 80 from it, turn about, twenty times: each petal passes through the ellipse of radius 1E-4.
            // A part of the ellipse no larger than the tolerance but not straight was cut until straight against each part of the curve near
            // it, not the curve cut until its parts were as small: the twenty took 7.9 s on .NET Framework.
            var fit = new List<GeoPoint2> { P(100, 0) };

            for (int i = 0; i < 20; i++)
            {
                double angle = 2.0 * Math.PI * i / 20.0;
                fit.Add(P(100 + 80 * Math.Cos(angle), 80 * Math.Sin(angle)));
                fit.Add(P(100, 0));
            }

            GeoSpline2 curve = GeoSpline2.Interpolate(fit, 3, Default);
            GeoEllipse2 ellipse = GeoEllipse2.FromCircle(new GeoCircle2(P(100, 0), 1E-4));

            GeoPoint2[] places = Answered(() => curve.GetIntersections(ellipse, Default), 0.5);

            Assert.NotEmpty(places);
            Assert.All(places, p => Assert.True(p.DistanceTo(P(100, 0)) <= 2E-4, p + " is not on the ellipse"));
        }

        [Fact]
        public void APolylineThroughACircleSplineOfRadius1E12_AHundredFromTheOrigin_MeetsIt()
        {
            // A hundred from the origin the snap is 1E-11, more than the circle is across: every point of the polyline a thousandth of the
            // circle's size from a meeting is within the rounding of the circle, so each meeting read as the polyline running along the
            // circle, and none was given.
            GeoSpline2 polyline = GeoSpline2.FromPolyline(new GeoPolyline2(P(0, 0), P(100, 0), P(0, 100)));
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(P(100, 0), 1E-12));

            GeoPoint2[] places = Answered(() => polyline.GetIntersections(circle, new Tolerance(1E-6, 1E-9)), 1.0);

            AssertPlaces(places, 1E-6, P(100, 0));
        }
    }
}
