using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfSpline.ElevateDegree"/> up to the order of 26 that AutoCAD 2021's SPLINEDIT Elevate order takes (degree 25),
    /// where the library stopped at degree 10. The control points are those AutoCAD's <c>ElevateDegree</c> gave in an
    /// accoreconsole probe, to the 7 decimals it wrote.
    /// </summary>
    public class DxfSplineElevateOrderTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>The cubic of four control points (0, 0), (10, 20), (30, 20), (40, 0) over 0 to 1.</summary>
        private static DxfSpline Cubic() =>
            new DxfSpline(
                3,
                false,
                false,
                false,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(10, 20, 0),
                    new GeoPoint3(30, 20, 0),
                    new GeoPoint3(40, 0, 0),
                },
                new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 },
                null,
                0.0,
                0.0
            );

        /// <summary>The cubic of five control points (0, 0), (10, 20), (20, 5), (30, 25), (40, 0) with an inner knot at 0.4.</summary>
        private static DxfSpline CubicWithAKnot() =>
            new DxfSpline(
                3,
                false,
                false,
                false,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(10, 20, 0),
                    new GeoPoint3(20, 5, 0),
                    new GeoPoint3(30, 25, 0),
                    new GeoPoint3(40, 0, 0),
                },
                new[] { 0.0, 0, 0, 0, 0.4, 1, 1, 1, 1 },
                null,
                0.0,
                0.0
            );

        private static void SamePoints(double[] expected, DxfSpline spline)
        {
            Assert.Equal(expected.Length / 2, spline.NumControlPoints);

            for (int i = 0; i < spline.NumControlPoints; i++)
            {
                GeoPoint3 p = spline.GetControlPointAt(i);
                Assert.InRange(p.X, expected[2 * i] - 1E-6, expected[2 * i] + 1E-6);
                Assert.InRange(p.Y, expected[2 * i + 1] - 1E-6, expected[2 * i + 1] + 1E-6);
                Assert.Equal(0.0, p.Z, 9);
            }
        }

        [Fact]
        public void ACubicRaisedToDegree12_HasTheThirteenPointsAutoCadGave()
        {
            DxfSpline spline = Cubic();
            spline.ElevateDegree(12);

            Assert.Equal(12, spline.Degree);
            Assert.Equal(
                Enumerable.Repeat(0.0, 13).Concat(Enumerable.Repeat(1.0, 13)).ToArray(),
                spline.Knots.ToArray()
            );
            SamePoints(
                new[]
                {
                    0,
                    0,
                    2.5,
                    5,
                    5.4545455,
                    9.0909091,
                    8.7727273,
                    12.2727273,
                    12.3636364,
                    14.5454545,
                    16.1363636,
                    15.9090909,
                    20,
                    16.3636364,
                    23.8636364,
                    15.9090909,
                    27.6363636,
                    14.5454545,
                    31.2272727,
                    12.2727273,
                    34.5454545,
                    9.0909091,
                    37.5,
                    5,
                    40,
                    0,
                },
                spline
            );
        }

        [Fact]
        public void ACubicRaisedToDegree11And15_HasTheEleventhAndFifteenthPointsAutoCadGave()
        {
            DxfSpline eleven = Cubic();
            eleven.ElevateDegree(11);
            SamePoints(
                new[]
                {
                    0,
                    0,
                    2.7272727,
                    5.4545455,
                    6,
                    9.8181818,
                    9.6969697,
                    13.0909091,
                    13.6969697,
                    15.2727273,
                    17.8787879,
                    16.3636364,
                    22.1212121,
                    16.3636364,
                    26.3030303,
                    15.2727273,
                    30.3030303,
                    13.0909091,
                    34,
                    9.8181818,
                    37.2727273,
                    5.4545455,
                    40,
                    0,
                },
                eleven
            );

            DxfSpline fifteen = Cubic();
            fifteen.ElevateDegree(15);
            SamePoints(
                new[]
                {
                    0,
                    0,
                    2,
                    4,
                    4.2857143,
                    7.4285714,
                    6.8131868,
                    10.2857143,
                    9.5384615,
                    12.5714286,
                    12.4175824,
                    14.2857143,
                    15.4065934,
                    15.4285714,
                    18.4615385,
                    16,
                    21.5384615,
                    16,
                    24.5934066,
                    15.4285714,
                    27.5824176,
                    14.2857143,
                    30.4615385,
                    12.5714286,
                    33.1868132,
                    10.2857143,
                    35.7142857,
                    7.4285714,
                    38,
                    4,
                    40,
                    0,
                },
                fifteen
            );
        }

        [Fact]
        public void ACubicRaisedToTheMostDegree25_HasTheTwentySixPointsAutoCadGave_AndIsTheSameCurve()
        {
            DxfSpline spline = Cubic();
            spline.ElevateDegree(25);

            Assert.Equal(25, spline.Degree);
            Assert.Equal(
                Enumerable.Repeat(0.0, 26).Concat(Enumerable.Repeat(1.0, 26)).ToArray(),
                spline.Knots.ToArray()
            );
            SamePoints(
                new[]
                {
                    0,
                    0,
                    1.2,
                    2.4,
                    2.5,
                    4.6,
                    3.8913043,
                    6.6,
                    5.3652174,
                    8.4,
                    6.9130435,
                    10,
                    8.526087,
                    11.4,
                    10.1956522,
                    12.6,
                    11.9130435,
                    13.6,
                    13.6695652,
                    14.4,
                    15.4565217,
                    15,
                    17.2652174,
                    15.4,
                    19.0869565,
                    15.6,
                    20.9130435,
                    15.6,
                    22.7347826,
                    15.4,
                    24.5434783,
                    15,
                    26.3304348,
                    14.4,
                    28.0869565,
                    13.6,
                    29.8043478,
                    12.6,
                    31.473913,
                    11.4,
                    33.0869565,
                    10,
                    34.6347826,
                    8.4,
                    36.1086957,
                    6.6,
                    37.5,
                    4.6,
                    38.8,
                    2.4,
                    40,
                    0,
                },
                spline
            );

            DxfSpline before = Cubic();

            for (int i = 0; i <= 40; i++)
            {
                double t = i / 40.0;
                Assert.True(
                    before.GetPointAtParameter(t).IsEqualTo(spline.GetPointAtParameter(t), Tight),
                    "At " + t + ": " + before.GetPointAtParameter(t) + " against " + spline.GetPointAtParameter(t)
                );
            }
        }

        [Fact]
        public void ACubicWithAnInnerKnotRaisedToDegree12_HasTheTwentyThreePointsAutoCadGave_TheKnotTenTimes()
        {
            DxfSpline spline = CubicWithAKnot();
            spline.ElevateDegree(12);

            Assert.Equal(12, spline.Degree);
            Assert.Equal(10, spline.Knots.Count(k => k == 0.4));
            SamePoints(
                new[]
                {
                    0,
                    0,
                    2.5,
                    5,
                    4.7272727,
                    8.8181818,
                    6.7090909,
                    11.5981818,
                    8.4727273,
                    13.4836364,
                    10.0454545,
                    14.6181818,
                    11.4545455,
                    15.1454545,
                    12.7272727,
                    15.2090909,
                    13.8909091,
                    14.9527273,
                    14.9727273,
                    14.52,
                    16,
                    14.0545455,
                    18.5,
                    13.1681818,
                    21,
                    13.8727273,
                    22.5181818,
                    14.6427273,
                    24.0727273,
                    15.5345455,
                    25.6818182,
                    16.3227273,
                    27.3636364,
                    16.7818182,
                    29.1363636,
                    16.6863636,
                    31.0181818,
                    15.8109091,
                    33.0272727,
                    13.93,
                    35.1818182,
                    10.8181818,
                    37.5,
                    6.25,
                    40,
                    0,
                },
                spline
            );

            DxfSpline before = CubicWithAKnot();

            for (int i = 0; i <= 40; i++)
            {
                double t = i / 40.0;
                Assert.True(before.GetPointAtParameter(t).IsEqualTo(spline.GetPointAtParameter(t), Tight));
            }
        }

        [Fact]
        public void ACubicRaisedToDegree25_HasTheLengthOfTheCubic_AsTheSplinesOwnMeasureGivesIt()
        {
            DxfSpline cubic = Cubic();
            DxfSpline raised = Cubic();
            raised.ElevateDegree(25);

            Assert.Equal(cubic.GetDistanceAtParameter(1.0), raised.GetDistanceAtParameter(1.0), 6);
            Assert.Equal(cubic.GetDistanceAtParameter(0.3), raised.GetDistanceAtParameter(0.3), 6);
        }

        [Fact]
        public void TheDegreeRaisedPast25_IsRefused_AsAutoCadsOrderOf26Is()
        {
            DxfSpline spline = Cubic();

            Assert.Throws<ArgumentOutOfRangeException>(() => spline.ElevateDegree(26));
            Assert.Equal(3, spline.Degree);
        }
    }
}
