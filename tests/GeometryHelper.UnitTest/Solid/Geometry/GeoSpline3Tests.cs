using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The spline in space by hand: what it is made of, the circle and the arc in any plane, and each member on numbers that
    /// can be checked on paper. The mathematics is the plane's: the same engine runs both, and the fuzz of
    /// <see cref="SplineOracleFuzzTests"/> runs on both dimensions.
    /// </summary>
    public class GeoSpline3Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoPoint3[] Arch =
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(100, 150, 60), new GeoPoint3(200, 150, 60), new GeoPoint3(300, 0, 0)
        };

        private static readonly double[] BezierKnots = { 0, 0, 0, 0, 1, 1, 1, 1 };

        private static readonly GeoCoordinateSystem3 Floor = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));

        private static GeoSpline3 Cubic() => new GeoSpline3(3, Arch, BezierKnots);

        private static SplineOracle OracleOf(GeoSpline3 spline) => SplineUnderTest.Wrap(spline).Oracle();

        private static void AssertPoint(double x, double y, double z, GeoPoint3 actual, double within)
        {
            Assert.True(Math.Abs(actual.X - x) <= within && Math.Abs(actual.Y - y) <= within && Math.Abs(actual.Z - z) <= within, "expected (" + x + ", " + y + ", " + z + "), got " + actual);
        }

        [Fact]
        public void ACubicOfFourPointsInSpace_IsTheBezierCurve_AtOneHundredAndFiftyOneHundredTwelveAndAHalfFortyFiveInTheMiddle()
        {
            GeoSpline3 spline = Cubic();

            Assert.Equal(3, spline.Degree);
            Assert.False(spline.IsRational);
            Assert.Equal(1.0, spline.DomainEnd);
            AssertPoint(150.0, 112.5, 45.0, spline.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(Arch[0], spline.StartPoint);
            Assert.Equal(Arch[3], spline.EndPoint);
            Assert.False(spline.IsClosed());

            GeoAabb3 box = spline.GetAabb();

            Assert.Equal(300.0, box.SizeX, 12);
            Assert.Equal(112.5, box.SizeY, 12);
            Assert.Equal(45.0, box.SizeZ, 12);
            Assert.Equal(0.0, box.Min.X);
            Assert.Equal(0.0, box.Min.Z);
        }

        [Fact]
        public void TheLengthOfTheArchInSpace_IsWhatTheOracleIntegrates()
        {
            Assert.Equal(OracleOf(Cubic()).Length(), Cubic().Length, 10);
        }

        [Fact]
        public void ACircleInAnyPlane_IsNineControlPoints_EveryPointAtTheRadiusInThePlane_AndTwoPiRLong()
        {
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 2, 2), 50.0);
            GeoSpline3 spline = GeoSpline3.FromCircle(circle);
            GeoPlane3 plane = circle.GetPlane();

            Assert.Equal(2, spline.Degree);
            Assert.Equal(9, spline.ControlPointCount);
            Assert.True(spline.IsRational);
            Assert.True(spline.IsClosed());
            Assert.True(spline.StartPoint.IsEqualTo(circle.GetPointAtAngle(0.0), Tol));

            double worst = 0.0;
            double offPlane = 0.0;

            for (int i = 0; i <= 2000; i++)
            {
                GeoPoint3 p = spline.GetPointAtKnot(i / 2000.0);
                worst = Math.Max(worst, Math.Abs(p.DistanceTo(circle.Center) - 50.0));
                offPlane = Math.Max(offPlane, Math.Abs(plane.SignedDistanceTo(p)));
            }

            Assert.True(worst <= 1E-13, "the worst point is " + worst + " off the radius");
            Assert.True(offPlane <= 1E-13, "the worst point is " + offPlane + " off the plane");
            Assert.True(Math.Abs(spline.Length - 100.0 * Math.PI) <= 1E-12 * 100.0 * Math.PI);
            Assert.Equal(1.0 / 50.0, spline.GetCurvatureAtKnot(0.37), 12);
        }

        [Fact]
        public void AnArcInSpace_RunsFromItsStartToItsEnd_InPiecesOfAQuarterTurnAtMost()
        {
            var arc = new GeoArc3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1), 80.0, 0.5, 0.5 + 3.5);
            GeoSpline3 spline = GeoSpline3.FromArc(arc);

            Assert.Equal(7, spline.ControlPointCount);
            Assert.True(spline.StartPoint.IsEqualTo(arc.StartPoint, Tol));
            Assert.True(spline.EndPoint.IsEqualTo(arc.EndPoint, Tol));
            Assert.Equal(arc.Length, spline.Length, 9);
            Assert.True(spline.GetPointAtKnot(0.5).IsEqualTo(arc.MidPoint, Tol));

            var clockwise = new GeoArc3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1), 80.0, 2.0, 0.5, true);
            GeoSpline3 back = GeoSpline3.FromArc(clockwise);

            Assert.True(back.StartPoint.IsEqualTo(clockwise.StartPoint, Tol));
            Assert.True(back.EndPoint.IsEqualTo(clockwise.EndPoint, Tol));
            Assert.Equal(clockwise.Length, back.Length, 9);
        }

        [Fact]
        public void FromControlPoints_AndAPolyline_AreMadeAsInThePlane()
        {
            var points = Enumerable.Range(0, 7).Select(i => new GeoPoint3(i * 10, i % 2 * 5, i));

            Assert.Equal(new[] { 0, 0, 0, 0, 0.25, 0.5, 0.75, 1, 1, 1, 1 }, GeoSpline3.FromControlPoints(3, points).Knots.ToArray());
            Assert.Equal(Cubic(), GeoSpline3.FromControlPoints(3, Arch));

            var polyline = new GeoPolyline3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0) });
            GeoSpline3 spline = GeoSpline3.FromPolyline(polyline);

            Assert.Equal(new[] { 0, 0, 2.0 / 3.0, 1, 1 }, spline.Knots.ToArray());
            Assert.Equal(150.0, spline.Length, 11);
            Assert.Throws<ArgumentNullException>(() => GeoSpline3.FromPolyline(null));
        }

        [Fact]
        public void WhatTheConstructorRefuses_IsWhatThePlaneRefuses()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline3(0, Arch, BezierKnots));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline3(11, Arch, BezierKnots));
            Assert.Throws<ArgumentException>(() => GeoSpline3.FromControlPoints(4, Arch));
            Assert.Throws<ArgumentException>(() => new GeoSpline3(3, Arch, new double[] { 0, 0, 0, 0, 1, 1, 1 }));
            Assert.Throws<ArgumentException>(() => new GeoSpline3(3, Arch, new double[] { 0, 0, 0, 1, 1, 1, 1, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline3(3, Arch, BezierKnots, new double[] { 1, 1, 0, 1 }));
            Assert.Throws<ArgumentException>(() => new GeoSpline3(3, new[] { new GeoPoint3(1, 2, 3), new GeoPoint3(1, 2, 3), new GeoPoint3(1, 2, 3), new GeoPoint3(1, 2, 3) }, BezierKnots));
            Assert.Throws<ArgumentException>(() => new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, double.NaN, 3), new GeoPoint3(1, 2, 3), new GeoPoint3(4, 2, 3) }, BezierKnots));
            Assert.Throws<ArgumentNullException>(() => new GeoSpline3(3, null, BezierKnots));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cubic().GetPointAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cubic().Split(1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cubic().Flatten(0.0));
        }

        [Fact]
        public void InterpolatingInSpace_MeetsTheFitPoints_AndTheTangents()
        {
            var fit = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 20), new GeoPoint3(200, 0, 40), new GeoPoint3(300, 50, 60), new GeoPoint3(400, 0, 20) };
            GeoSpline3 spline = GeoSpline3.Interpolate(fit, 3, Tol);
            double[] u = new double[fit.Length];

            for (int i = 1; i < fit.Length; i++)
            {
                u[i] = u[i - 1] + fit[i].DistanceTo(fit[i - 1]);
            }

            for (int i = 0; i < fit.Length; i++)
            {
                Assert.True(spline.GetPointAtKnot(u[i] / u[fit.Length - 1]).DistanceTo(fit[i]) <= 1E-9, "point " + i);
            }

            GeoSpline3 tangent = GeoSpline3.Interpolate(fit, new GeoVector3(0, 0, 1), new GeoVector3(0, 1, 0), 3, Tol);

            Assert.True(tangent.GetTangentAtKnot(0.0).IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.True(tangent.GetTangentAtKnot(1.0).IsEqualTo(new GeoVector3(0, 1, 0), Tol));
            Assert.Equal(fit.Length + 2, tangent.ControlPointCount);

            for (int i = 0; i < fit.Length; i++)
            {
                Assert.True(tangent.GetPointAtKnot(u[i] / u[fit.Length - 1]).DistanceTo(fit[i]) <= 1E-9, "point " + i);
            }

            Assert.Throws<ArgumentException>(() => GeoSpline3.Interpolate(fit, GeoVector3.Zero, new GeoVector3(0, 1, 0), 3, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline3.Interpolate(fit, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0), 1, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline3.Interpolate(fit.Take(3), 3, Tol));
            Assert.Throws<ArgumentNullException>(() => GeoSpline3.Interpolate(null, 3, Tol));
        }

        [Fact]
        public void CuttingTheArchInSpaceAtThreeTenths_GivesTwoPiecesMeetingAtNinetyNinetyFourAndAHalfAndThirtySevenPointEight()
        {
            GeoSpline3[] pieces = Cubic().Split(0.3);

            AssertPoint(90.0, 94.5, 37.8, pieces[0].EndPoint, 1E-12);
            Assert.Equal(pieces[0].EndPoint, pieces[1].StartPoint);
            Assert.Equal(Cubic().Length, pieces[0].Length + pieces[1].Length, 10);
        }

        [Fact]
        public void TheReverseTheMoveAndTheTransformation_CarryTheControlPoints()
        {
            GeoSpline3 spline = Cubic();

            Assert.Equal(spline.EndPoint, spline.Reverse().StartPoint);
            // Equal because these knots are dyadic fractions, for which a + b - k is exact; for knots such as 0.3 the knots come back to
            // within the last place, and only the control points come back to the bit (see GeoSplineParameterTests).
            Assert.Equal(spline, spline.Reverse().Reverse());
            Assert.Equal(spline.Clone(), spline);

            GeoSpline3 moved = spline + new GeoVector3(10, -20, 5);
            AssertPoint(160.0, 92.5, 50.0, moved.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(spline, moved - new GeoVector3(10, -20, 5));

            GeoSpline3 turned = spline.TransformBy(GeoTransform3.RotationZ(Math.PI * 0.5));
            AssertPoint(-112.5, 150.0, 45.0, turned.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(spline.Length, turned.Length, 10);
            Assert.Throws<ArgumentNullException>(() => spline.TransformBy(null));
            Assert.Throws<InvalidOperationException>(() => spline.TransformBy(GeoTransform3.Scaling(0.0)));

            GeoSpline3 flat = spline.TransformBy(GeoTransform3.Scaling(1.0, 0.0, 1.0));
            Assert.All(flat.ControlPoints, p => Assert.Equal(0.0, p.Y));
        }

        [Fact]
        public void ASplineInTheFloor_ProjectsToTheSplineItCameFrom_AndOneStandingOnItsEndIsRefused()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), new GeoVector3(0, 0, 1));
            var flat = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0) }, BezierKnots, new double[] { 1, 2, 0.5, 1 });
            GeoSpline3 lifted = PlanarMap.ToSpline3(frame, flat);
            GeoSpline2 back = PlanarMap.ProjectToSpline2(frame, lifted);

            Assert.Equal(flat.Weights.ToArray(), back.Weights.ToArray());
            Assert.Equal(flat.Knots.ToArray(), back.Knots.ToArray());

            for (int i = 0; i < flat.ControlPointCount; i++)
            {
                Assert.True(back.ControlPoints[i].IsEqualTo(flat.ControlPoints[i], Tol));
            }

            Assert.Equal(flat.Length, lifted.Length, 10);
            Assert.Equal(back, lifted.ProjectToSpline2(frame));

            // Seen square onto the floor, a spline tilted out of it comes out shorter and its box is the box of its shadow.
            GeoSpline2 shadow = Cubic().ProjectToSpline2(Floor);
            Assert.True(shadow.Length < Cubic().Length);
            Assert.Equal(new GeoPoint2(150, 112.5).X, shadow.GetPointAtKnot(0.5).X, 12);

            var standing = new GeoSpline3(3, new[] { new GeoPoint3(5, 5, 0), new GeoPoint3(5, 5, 10), new GeoPoint3(5, 5, 20), new GeoPoint3(5, 5, 30) }, BezierKnots);
            Assert.Throws<InvalidOperationException>(() => standing.ProjectToSpline2(Floor));
            Assert.Throws<ArgumentNullException>(() => PlanarMap.ProjectToSpline2(frame, null));
            Assert.Throws<ArgumentNullException>(() => PlanarMap.ToSpline3(frame, null));
        }

        [Fact]
        public void TheDerivativesAndTheTangentOfTheArch_AreThoseOfTheBezierCurve_AndTheCurvatureIsNeverNegative()
        {
            GeoSpline3 spline = Cubic();

            Assert.True(spline.GetDerivativeAtKnot(0.0, 1).IsEqualTo(new GeoVector3(300, 450, 180), Tol));
            Assert.True(spline.GetDerivativeAtKnot(0.0, 2).IsEqualTo(new GeoVector3(0, -900, -360), Tol));
            Assert.True(spline.GetTangentAtKnot(0.5).IsEqualTo(new GeoVector3(1, 0, 0), Tol));
            Assert.True(spline.GetCurvatureAtKnot(0.5) > 0.0);

            var straight = new GeoSpline3(1, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10) }, new double[] { 0, 0, 1, 1 });
            Assert.Equal(0.0, straight.GetCurvatureAtKnot(0.5), 12);

            var stuck = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 100) }, BezierKnots);
            Assert.True(stuck.GetTangentAtKnot(0.0).IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Throws<InvalidOperationException>(() => stuck.GetCurvatureAtKnot(0.0));
        }

        [Fact]
        public void TheDistanceFromAPointInSpace_IsTheExactOne_FromEitherSide_AndTheShortestLineIsAsLongAsIt()
        {
            GeoSpline3 circle = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));

            Assert.Equal(60.0, circle.DistanceTo(new GeoPoint3(160, 0, 0)), 11);
            Assert.Equal(100.0, circle.DistanceTo(new GeoPoint3(0, 0, 0)), 11);
            Assert.Equal(Math.Sqrt(100.0 * 100.0 + 30.0 * 30.0), circle.DistanceTo(new GeoPoint3(0, 0, 30)), 11);
            Assert.Equal(50.0, circle.DistanceTo(new GeoPoint3(100, 0, 50)), 11);
            Assert.Equal(0.0, circle.GetKnotAtPoint(new GeoPoint3(0, 0, 30)));

            GeoSpline3 spline = Cubic();
            var point = new GeoPoint3(150, 200, 45);
            GeoLine3 fromSpline = spline.GetShortestLineTo(point);
            GeoLine3 fromPoint = point.GetShortestLineTo(spline);

            Assert.Equal(point, fromSpline.EndPoint);
            Assert.Equal(fromSpline.StartPoint, fromPoint.EndPoint);
            Assert.Equal(spline.DistanceTo(point), point.DistanceTo(spline));
            Assert.Equal(spline.DistanceTo(point), fromSpline.Length, 12);
            Assert.Equal(spline.GetClosestPointOnBoundary(point), point.GetClosestPointOnBoundary(spline));
            Assert.Equal(spline.DistanceTo(point, Tol), spline.DistanceTo(point));
            Assert.Equal(spline.GetShortestLineTo(point, Tol), fromSpline);
            Assert.Equal(point.DistanceTo(spline, Tol), point.DistanceTo(spline));
            Assert.Throws<ArgumentNullException>(() => point.DistanceTo((GeoSpline3)null));

            GeoPoint3 on = spline.GetPointAtKnot(0.37);
            Assert.True(spline.IsPointOn(on));
            Assert.True(on.IsPointOn(spline, Tol));
            Assert.False(new GeoPoint3(0, 0, 5).IsPointOn(spline));
        }

        [Fact]
        public void ASplineInSpaceFlattenedWithinAHundredth_StaysWithinIt()
        {
            GeoSpline3 spline = Cubic();
            GeoPolyline3 chain = spline.Flatten(0.01);
            var oracle = OracleOf(spline);

            Assert.True(chain.EdgeCount > 20);
            Assert.Equal(spline.StartPoint, chain.Vertices.First());
            Assert.True(chain.Vertices.Last().IsEqualTo(spline.EndPoint, Tol));

            for (int i = 0; i <= 3000; i++)
            {
                double[] p = oracle.FastPoint(i / 3000.0);

                Assert.True(chain.DistanceTo(new GeoPoint3(p[0], p[1], p[2])) <= 0.01 * (1.0 + 1E-9), "at " + i);
            }

            var circle = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 1, 1), 100.0));
            Assert.InRange(circle.Flatten(0.01).EdgeCount, 223, 300);
        }

        [Fact]
        public void TwoSplinesInSpaceAreEqualWhenTheyHoldTheSameNumbers_AndAreDescribed()
        {
            Assert.Equal(Cubic(), new GeoSpline3(3, Arch, BezierKnots));
            Assert.Equal(Cubic().GetHashCode(), new GeoSpline3(3, Arch, BezierKnots).GetHashCode());
            Assert.True(Cubic() == new GeoSpline3(3, Arch, BezierKnots));
            Assert.True(Cubic() != Cubic().Translate(new GeoVector3(0, 0, 1E-9)));
            Assert.NotEqual(Cubic(), new GeoSpline3(3, Arch, BezierKnots, new double[] { 1, 1, 1, 1.5 }));
            Assert.False(Cubic().Equals(null));
            Assert.True((GeoSpline3)null == null);
            Assert.Equal("GeoSpline3[Degree:3, 4 control points, Domain:[0, 1]]", Cubic().ToString());
            Assert.Equal("GeoSpline3[Degree:2, 9 control points, Domain:[0, 1], Rational]", GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0)).ToString());
        }

        [Fact]
        public void WhatTheSplineHandsOut_CannotBeWrittenTo_AndTheNumbersPutInAreCopied()
        {
            GeoPoint3[] points = Arch.ToArray();
            double[] knots = BezierKnots.ToArray();
            var spline = new GeoSpline3(3, points, knots);

            points[0] = new GeoPoint3(9, 9, 9);
            knots[0] = -5.0;

            Assert.Equal(new GeoPoint3(0, 0, 0), spline.ControlPoints[0]);
            Assert.Equal(0.0, spline.Knots[0]);
            Assert.False(spline.ControlPoints is GeoPoint3[]);
            Assert.False(spline.Knots is double[]);
            Assert.False(spline.Weights is double[]);
        }
    }
}
