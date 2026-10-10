using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decision 6: an ellipse whose radii are equal is a circle, and answers as <see cref="GeoCircle3"/> does to 1E-12 of
    /// its size, whichever way its major axis lies in the plane. Where the zero of t matters, it is compared made by
    /// <see cref="GeoEllipse3.FromCircle"/>. The one place they part is the centre, and the points straight above and
    /// below it, which every rim point is as near: the ellipse gives the end of its minor axis (2D A1), the circle its
    /// zero-parameter point.
    /// </summary>
    public class Ellipse3AsCircleTests
    {
        private static readonly Tolerance Bands = new Tolerance(1E-3, 1E-5, Tolerance.Default.EqualAngleRad, 0.1);

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        private static readonly GeoCircle3 Circle = new GeoCircle3(new GeoPoint3(100, 200, 300), new GeoVector3(1, -1, 2), 250);

        /// <summary>The circle as an ellipse with its axis turned 0.4 rad from the circle's zero direction.</summary>
        private static GeoEllipse3 Turned()
        {
            Circle.GetPlane().GetAxes(out GeoVector3 u, out GeoVector3 v);
            GeoVector3 axis = u.Multiply(Math.Cos(0.4)).Add(v.Multiply(Math.Sin(0.4)));
            return new GeoEllipse3(Circle.Center, Circle.Normal, axis, 250, 250);
        }

        /// <summary>Points about the circle: inside and out, on the rim and a hair off it, above, below and far off.</summary>
        private static GeoPoint3[] Probes()
        {
            Circle.GetPlane().GetAxes(out GeoVector3 u, out GeoVector3 v);
            GeoVector3 n = Circle.Normal;
            GeoPoint3 c = Circle.Center;
            GeoPoint3 At(double x, double y, double h) => c.Add(u.Multiply(x)).Add(v.Multiply(y)).Add(n.Multiply(h));
            double r = 250;

            return new[]
            {
                At(30, -40, 0), At(-120, 90, 0), At(r * 0.6, r * 0.8, 0), At(r * 0.6 * 1.000002, r * 0.8 * 1.000002, 0),
                At(-r * 1.000008, 0, 0), At(0, -r * 0.999992, 0), At(400, 300, 0), At(30, -40, 0.05), At(30, -40, 0.2),
                At(-200, 50, -80), At(900, -700, 450), At(r * 0.28, r * 0.96, 0.05),
            };
        }

        [Fact]
        public void APointIsPlacedAsTheCirclePlacesItWhateverTheAxis()
        {
            foreach (GeoEllipse3 ellipse in new[] { Turned(), GeoEllipse3.FromCircle(Circle) })
            {
                foreach (GeoPoint3 point in Probes())
                {
                    Assert.Equal(Circle.Locate(point, Bands), ellipse.Locate(point, Bands));
                    Assert.Equal(Circle.Contains(point, Bands), ellipse.Contains(point, Bands));
                    Assert.Equal(Circle.IsPointOn(point, Bands), ellipse.IsPointOn(point, Bands));
                }
            }
        }

        [Fact]
        public void APointIsMeasuredAsTheCircleMeasuresItWhateverTheAxis()
        {
            // The distance to the disc, the nearest point of the disc and the nearest rim point, to 1E-12 of 250.
            var tolerance = new Tolerance(1E-12 * 250, 1E-12);

            foreach (GeoEllipse3 ellipse in new[] { Turned(), GeoEllipse3.FromCircle(Circle) })
            {
                foreach (GeoPoint3 point in Probes())
                {
                    Assert.Equal(Circle.DistanceTo(point), ellipse.DistanceTo(point), 1E-12 * 250);
                    Assert.True(Circle.GetClosestPointOnSurface(point).IsEqualTo(ellipse.GetClosestPointOnSurface(point), tolerance), $"surface, {point}");
                    Assert.True(Circle.GetClosestPointOnBoundary(point).IsEqualTo(ellipse.GetClosestPointOnBoundary(point), tolerance), $"rim, {point}");
                }
            }
        }

        [Fact]
        public void TheMeasuresAndTheBoxAreTheCircles()
        {
            foreach (GeoEllipse3 ellipse in new[] { Turned(), GeoEllipse3.FromCircle(Circle) })
            {
                Assert.Equal(Circle.Area, ellipse.Area, 1E-12 * Circle.Area);
                Assert.Equal(Circle.Length, ellipse.Length, 1E-12 * Circle.Length);
                Assert.True(ellipse.IsCircle(Bands));
                Assert.True(Circle.GetAabb().Min.IsEqualTo(ellipse.GetAabb().Min, Tight));
                Assert.True(Circle.GetAabb().Max.IsEqualTo(ellipse.GetAabb().Max, Tight));
            }
        }

        [Fact]
        public void MadeFromTheCircleItWalksTheRimAsTheCircleDoesWrappingIncluded()
        {
            // Parameters before nought and past one, lengths before nought and past the rim: the circle wraps them all
            // (Parametrization3.cs:435-469), and so does the ellipse.
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(Circle);
            double length = Circle.Length;

            foreach (double p in new[] { -0.3, 0.0, 0.2, 0.45, 0.9, 1.4, 3.05 })
            {
                Assert.True(Circle.GetPointAtParameter(p).IsEqualTo(ellipse.GetPointAtParameter(p), Tight), $"p = {p}");
                Assert.Equal(Circle.GetDistanceAtParameter(p), ellipse.GetDistanceAtParameter(p), 1E-12 * length);
            }

            foreach (double d in new[] { -100.0, 0.0, 500.0, 1200.0, 2000.0, 5000.0 })
            {
                Assert.True(Circle.GetPointAtDistance(d).IsEqualTo(ellipse.GetPointAtDistance(d), Tight), $"d = {d}");
                Assert.Equal(Circle.GetParameterAtDistance(d), ellipse.GetParameterAtDistance(d), 1E-12);
            }
        }

        [Fact]
        public void MadeFromTheCircleItReadsAPointsParameterAsTheCircleDoesAwayFromTheAxis()
        {
            // Off the circle's axis, the nearest rim point is one and the same, so are its parameter and its length. The
            // two probes nearest a turn's end are left out: there nought and one are a rounding apart.
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(Circle);
            double length = Circle.Length;

            foreach (GeoPoint3 point in Probes())
            {
                double expected = Circle.GetParameterAtPoint(point, Bands);

                if (expected < 1E-9 || expected > 1 - 1E-9)
                {
                    continue;
                }

                Assert.Equal(expected, ellipse.GetParameterAtPoint(point, Bands), 1E-12);
                Assert.Equal(Circle.GetDistanceAtPoint(point, Bands), ellipse.GetDistanceAtPoint(point, Bands), 1E-12 * length);
            }
        }

        [Fact]
        public void TheCentreIsTheOnePlaceTheEllipseAndTheCirclePart()
        {
            // At the centre, and 70 above it, the circle gives its zero-parameter point and parameter 0
            // (Projection3.cs:727-730, Parametrization3.cs:487-490); the ellipse, even made from the circle, the end of its
            // minor axis and parameter 0.25 (2D A1).
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(Circle);
            Circle.GetPlane().GetAxes(out GeoVector3 u, out GeoVector3 v);

            foreach (GeoPoint3 point in new[] { Circle.Center, Circle.Center.Add(Circle.Normal.Multiply(70)) })
            {
                Assert.True(Circle.GetClosestPointOnBoundary(point).IsEqualTo(Circle.Center.Add(u.Multiply(250)), Tight));
                Assert.Equal(0.0, Circle.GetParameterAtPoint(point, Bands));

                Assert.True(ellipse.GetClosestPointOnBoundary(point).IsEqualTo(Circle.Center.Add(v.Multiply(250)), Tight));
                Assert.Equal(0.25, ellipse.GetParameterAtPoint(point, Bands), 1E-12);
            }
        }
    }
}
