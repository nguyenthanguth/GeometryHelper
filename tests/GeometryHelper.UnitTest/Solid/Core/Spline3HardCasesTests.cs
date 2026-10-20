using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The cases the review of the splines in the plane found that the first round let through, built again in space, in planes tilted
    /// every way and a hundred thousand or a million from the origin: a near approach at an end that hid a crossing, places given that
    /// were not leasts of the distance, a tangent touch read as a run along, two curves a hair apart that cross twice, a dart on the
    /// second curve, and the sizes where the work could go quadratic: splines of a hundred thousand spans, a polyline of a hundred thousand
    /// edges, a solid of thirty thousand faces.
    /// </summary>
    /// <remarks>
    /// In space two curves that run side by side or cross do so in a plane, so each case is laid in the plane of a frame: the floor, a plane
    /// tilted by the rational axes (1, 2, 2) / 3 and (-2, -1, 2) / 3, and the same tilted plane with its origin 100 000 and a million away.
    /// A distance is read to a ten-millionth of the size of the shapes, and the places to a thousandth. A million out in three coordinates
    /// (1E6, -2E6, 1E6) the numbers are 2.4E-10 apart, and a curve that touches another is within a snap (2E-7) of it for 0.02 either side
    /// of the touch, where the plane's million has 0.01.
    /// </remarks>
    public class Spline3HardCasesTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static readonly Tolerance Fine = new Tolerance(1E-5, 1E-5);
        private static readonly Tolerance Tight = new Tolerance(1E-4, 1E-5);

        public static TheoryData<string> Frames()
        {
            var data = new TheoryData<string>();
            data.Add("floor");
            data.Add("tilted");
            data.Add("a hundred thousand out");
            data.Add("a million out");
            return data;
        }

        private static GeoCoordinateSystem3 Frame(string name)
        {
            GeoPoint3 origin = name == "floor" || name == "tilted" ? P(0, 0, 0) : name == "a million out" ? P(1E6, -2E6, 1E6) : P(1E5, -2E5, 1E5);

            return name == "floor"
                ? new GeoCoordinateSystem3(origin, GeoVector3.XAxis, GeoVector3.YAxis)
                : new GeoCoordinateSystem3(origin, new GeoVector3(1.0 / 3.0, 2.0 / 3.0, 2.0 / 3.0), new GeoVector3(-2.0 / 3.0, -1.0 / 3.0, 2.0 / 3.0));
        }

        /// <summary>Gets the point of a frame at two numbers.</summary>
        private static GeoPoint3 At(GeoCoordinateSystem3 frame, double x, double y) => frame.ToGlobal(new GeoPoint3(x, y, 0.0));

        private static GeoPoint3 At(GeoCoordinateSystem3 frame, double x, double y, double z) => frame.ToGlobal(new GeoPoint3(x, y, z));

        private static GeoSpline3 Ring(GeoCoordinateSystem3 frame, double x, double y, double r) => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(x, y), r)).ToSpline3(frame);

        private static GeoSpline3 Through(int degree, GeoCoordinateSystem3 frame, params double[] xy)
            => GeoSpline3.FromControlPoints(degree, Enumerable.Range(0, xy.Length / 2).Select(i => At(frame, xy[2 * i], xy[2 * i + 1])));

        private static double Where(GeoCoordinateSystem3 frame, GeoPoint3 p, out double y)
        {
            GeoPoint3 local = frame.ToLocal(p);
            y = local.Y;

            return local.X;
        }

        #region A near approach at an end that hid a crossing

        public static TheoryData<string, double, double> CrossingsBehindTheEnds()
        {
            var data = new TheoryData<string, double, double>();

            foreach (string frame in new[] { "floor", "tilted", "a million out" })
            {
                data.Add(frame, 1E4, 1E-5);
                data.Add(frame, 1E5, 1E-5);
                data.Add(frame, 1E6, 1E-3);
                data.Add(frame, 1E6, 1E-5);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(CrossingsBehindTheEnds))]
        public void ACrossingBehindAnApproachAtTheStart_IsFoundInSpace_AndTheDistanceIsNought(string name, double length, double tolerance)
        {
            // A line and a cubic that starts 0.5E-8 of its length off it, the nearest it comes within the first look, then arches away and
            // falls through the line: the start is 0.005 off the line at a million, and the crossing is well beyond it.
            GeoCoordinateSystem3 frame = Frame(name);
            var tol = new Tolerance(tolerance, 1E-5);
            GeoSpline3 line = Through(1, frame, 0, 0, length, 0);
            GeoSpline3 cubic = Through(3, frame, 0, 0.5E-8 * length, length / 3, length / 1000, 2 * length / 3, length / 1000, length, -length / 1000);

            GeoPoint3[] crossings = line.GetIntersections(cubic, tol);

            Assert.Contains(crossings, c => Where(frame, c, out double y) > 0.5 * length && Math.Abs(y) <= 1E-4);
            Assert.True(line.DistanceTo(cubic, tol) <= 1E-9 * length);
            Assert.True(cubic.DistanceTo(line, tol) <= 1E-9 * length);
            Assert.True(line.CollidesWith(cubic, tol));
            Assert.True(cubic.CollidesWith(line, tol));
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public void ANearMissOfNineTenThousandthsAtAMillion_ReadsBelowTheToleranceInSpace(string name)
        {
            // The arch is 0.0009 off the line in the middle; its ends are 0.01 off it, which is what a look at the ends finds first, and
            // 0.01 is a hundred-millionth of the size of the shapes: a bound by that much would have the arch read as 0.01.
            GeoCoordinateSystem3 frame = Frame(name);
            double length = 1E6;
            GeoSpline3 line = Through(1, frame, 0, 0, length, 0);
            GeoSpline3 arch = Through(2, frame, 0, 0.01, length / 2, 2 * 0.0009 - 0.01, length, 0.01);

            Assert.Equal(0.0009, line.DistanceTo(arch, Strict), 5);
            Assert.Equal(0.0009, arch.DistanceTo(line, Strict), 5);
            Assert.True(line.CollidesWith(arch, Strict));
            Assert.True(arch.CollidesWith(line, Strict));
        }

        #endregion

        #region Places that are leasts of the distance

        public static TheoryData<string, int> TangentToTheTop()
        {
            var data = new TheoryData<string, int>();

            foreach (string frame in new[] { "floor", "tilted", "a million out" })
            {
                foreach (int pieces in new[] { 2, 10, 100, 1000 })
                {
                    data.Add(frame, pieces);
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(TangentToTheTop))]
        public void ALineTangentToACircleSplineInSpace_TouchesOnce_HoweverItIsCut(string name, int pieces)
        {
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 ring = Ring(frame, 0, 0, 1000);
            var points = Enumerable.Range(0, pieces).Select(i => At(frame, -500 + 1000.0 * i / (pieces - 1), 1000)).ToArray();
            GeoSpline3 line = GeoSpline3.FromPolyline(new GeoPolyline3(points));
            var segment = new GeoLine3(points[0], points[pieces - 1]);
            GeoPoint3 touch = At(frame, 0, 1000);

            foreach (GeoPoint3[] places in new[] { line.GetIntersections(ring, Strict), ring.GetIntersections(line, Strict), ring.GetIntersections(segment, Strict), segment.GetIntersections(ring, Strict) })
            {
                Assert.True(places.Length >= 1 && places.Length <= 3, places.Length + " places " + string.Join(" ", places));
                Assert.All(places, p => Assert.True(p.DistanceTo(touch) <= (name == "a million out" ? 0.04 : 0.01), "the touch is at " + p));
            }

            Assert.True(ring.DistanceTo(line, Strict) <= 1E-6);
            Assert.True(line.CollidesWith(ring, Strict));
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public void TwoCirclesInSpaceHalfAThousandthApart_CollideAndMeetNowhere(string name)
        {
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 inner = Ring(frame, 0, 0, 1000);
            GeoSpline3 outer = Ring(frame, 0, 0, 1000.0005);

            Assert.InRange(inner.DistanceTo(outer, Strict), 0.0005 - 1E-6, 0.0005 + 1E-6);
            Assert.Equal(inner.DistanceTo(outer, Strict), outer.DistanceTo(inner, Strict));
            Assert.True(inner.CollidesWith(outer, Strict));
            Assert.Empty(inner.GetIntersections(outer, Strict));
            Assert.Empty(outer.GetIntersections(inner, Strict));
            Assert.False(inner.CollidesWith(outer, Tight));
        }

        #endregion

        #region Two crossings a hair apart

        public static TheoryData<string, double> Chords()
        {
            var data = new TheoryData<string, double>();

            // A hundred thousand out in three coordinates a snap is 3E-8, and a chord of 0.004 or 0.02 of the circle of radius 1 000 is as deep
            // as 2E-9 or 5E-8: the line meets the circle to the digits all along it there, and those two are the rounding case.
            foreach (string frame in new[] { "floor", "tilted", "a hundred thousand out" })
            {
                foreach (double chord in new[] { 0.004, 0.02, 0.1, 1.0 })
                {
                    if (frame != "a hundred thousand out" || chord >= 0.1)
                    {
                        data.Add(frame, chord);
                    }
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Chords))]
        public void ALineCuttingACircleSplineInSpaceInAShortChord_MeetsItTwice_FromEitherSide(string name, double chord)
        {
            // A line 40 long that cuts the circle of radius 1000 in a chord: the crossings are at x = +-chord / 2, the circle's own.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 ring = Ring(frame, 0, 0, 1000);
            double y = Math.Sqrt(1000.0 * 1000.0 - 0.25 * chord * chord);
            var line = new GeoLine3(At(frame, -20, y), At(frame, 20, y));

            GeoPoint3[] places = ring.GetIntersections(line, Strict);
            GeoPoint3[] back = line.GetIntersections(ring, Strict);

            Assert.Equal(2, places.Length);
            Assert.Equal(2, back.Length);
            Assert.All(places, p => Assert.True(Math.Abs(Math.Abs(Where(frame, p, out _)) - 0.5 * chord) <= 1E-4, "a crossing at " + p));
            Assert.Equal(0.0, ring.DistanceTo(line, Strict), 6);
        }

        public static TheoryData<string> NearFrames()
        {
            var data = new TheoryData<string>();
            data.Add("floor");
            data.Add("tilted");
            data.Add("a hundred thousand out");
            return data;
        }

        [Theory]
        [MemberData(nameof(NearFrames))]
        public void TwoCirclesInSpaceAtAnAngleOfAHundredThousandthOfARadian_CrossTwice(string name)
        {
            // Radius 1 000 and 1 000.00002 with centres 1E-4 apart cross at (-200, +-979.8) at an angle of 1E-7. A million out a snap is 2E-7,
            // two units either side of a crossing at that angle, and the rounding gives places in them.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 first = Ring(frame, 0, 0, 1000);
            GeoSpline3 second = Ring(frame, 1E-4, 0, 1000.00002);

            GeoPoint3[] places = first.GetIntersections(second, Strict);
            GeoPoint3[] back = second.GetIntersections(first, Strict);

            Assert.Equal(2, places.Length);
            Assert.Equal(places.Length, back.Length);
            Assert.All(places, p =>
            {
                double x = Where(frame, p, out double y);
                Assert.True(Math.Abs(x + 200.0) <= 0.01 && Math.Abs(Math.Abs(y) - 979.8) <= 0.01, "a crossing at " + p);
            });
            Assert.Equal(0.0, first.DistanceTo(second, Strict), 6);
        }

        #endregion

        #region The second curve, a dart

        [Theory]
        [MemberData(nameof(Frames))]
        public void ADartOfAPolylineInSpace_IsCutOutOfTheRunAlongTheLine_AndItsEdgesAreCrossings(string name)
        {
            // A line along the axis and a polyline 2E-4 over it for a thousand, which goes out a unit under it and back between 506 and
            // 508: the stretch it lies along is cut in two, and the dart's edges, at 506.0002 and 507.9998, are crossings, where they were lost.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 line = Through(1, frame, 0, 0, 1000, 0);
            var vertices = new List<double[]>();

            for (int i = 0; i <= 1000; i += 2)
            {
                vertices.Add(new[] { (double)i, 2E-4 });

                if (i == 506)
                {
                    vertices.Add(new[] { 507.0, -1.0 });
                }
            }

            GeoSpline3 near = GeoSpline3.FromPolyline(new GeoPolyline3(vertices.Select(v => At(frame, v[0], v[1])).ToArray()));

            GeoPoint3[] places = line.GetIntersections(near, Strict);
            GeoPoint3[] back = near.GetIntersections(line, Strict);

            Assert.Equal(places.Length, back.Length);
            Assert.True(line.DistanceTo(near, Strict) <= 2E-4 + 1E-6);
            Assert.True(line.CollidesWith(near, Strict));
            Assert.Contains(places, p => Math.Abs(Where(frame, p, out _) - 506.0002) <= 2E-3);
            Assert.Contains(places, p => Math.Abs(Where(frame, p, out _) - 507.9998) <= 2E-3);
            Assert.Contains(back, p => Math.Abs(Where(frame, p, out _) - 506.0002) <= 2E-3);
            Assert.Contains(back, p => Math.Abs(Where(frame, p, out _) - 507.9998) <= 2E-3);
        }

        #endregion

        #region Two curves along each other, in one plane and out of it

        /// <summary>The corners of a polyline spline that runs out along the axis, up, out and back.</summary>
        private static readonly double[] Corners = { 0, 0, 100, 0, 100, 100, 200, 150, 150, 300 };

        private static GeoSpline2 Flat(double dx, double dy) => GeoSpline2.FromControlPoints(1, Enumerable.Range(0, Corners.Length / 2).Select(i => new GeoPoint2(Corners[2 * i] + dx, Corners[2 * i + 1] + dy)));

        [Theory]
        [MemberData(nameof(Frames))]
        public void APolylineSplineAndItsCopyMovedHalfAThousandthAlongItsPlane_CrossAtTheThreeCornersAsInThePlane(string name)
        {
            // The copy is moved (0.0003, 0.0004) along the plane: the two lie along each other within the tolerance all the way, and cross
            // where an edge of one passes a corner of the other, at (100, 0.0004), (100.0003, 100.00015) and (199.99993, 150.00021), the
            // places the two give in the plane. They were lost in space, where the offset of one from the other has no sign.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 spline = Flat(0, 0).ToSpline3(frame);
            GeoSpline3 copy = Flat(0.0003, 0.0004).ToSpline3(frame);
            GeoPoint2[] inPlane = Flat(0, 0).GetIntersections(Flat(0.0003, 0.0004), Strict);

            GeoPoint3[] places = spline.GetIntersections(copy, Strict);
            GeoPoint3[] back = copy.GetIntersections(spline, Strict);

            Assert.Equal(3, inPlane.Length);
            Assert.Equal(inPlane.Length, places.Length);
            Assert.Equal(places.Length, back.Length);

            for (int i = 0; i < places.Length; i++)
            {
                GeoPoint3 local = frame.ToLocal(places[i]);
                Assert.True(Math.Abs(local.X - inPlane[i].X) <= 1E-6 && Math.Abs(local.Y - inPlane[i].Y) <= 1E-6 && Math.Abs(local.Z) <= 1E-6, "the place " + local + " and the plane's " + inPlane[i]);

                // In the floor the two are the plane's numbers, and the places are the plane's to the bit.
                if (name == "floor")
                {
                    Assert.Equal(BitConverter.DoubleToInt64Bits(inPlane[i].X), BitConverter.DoubleToInt64Bits(places[i].X));
                    Assert.Equal(BitConverter.DoubleToInt64Bits(inPlane[i].Y), BitConverter.DoubleToInt64Bits(places[i].Y));
                }
            }

            Assert.Equal(0.0, spline.DistanceTo(copy, Strict));
            Assert.True(spline.CollidesWith(copy, Strict));
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public void ThatCopyLiftedTwoTenThousandthsOffThePlane_ComesNearestAtTheThreeCornersInSpace(string name)
        {
            // Lifted 0.0002 off the plane the two no longer meet: they are 0.0002 apart where they crossed and 0.00036 to 0.00045 apart along
            // the edges between, so the three corners are where the distance is least, the places in space, as the polylines' are.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 spline = Through(1, frame, Corners);
            GeoSpline3 lifted = GeoSpline3.FromControlPoints(1, Enumerable.Range(0, Corners.Length / 2).Select(i => At(frame, Corners[2 * i] + 0.0003, Corners[2 * i + 1] + 0.0004, 0.0002)));
            double[][] near = { new[] { 100, 0.0004 }, new[] { 100.0003, 100.00015 }, new[] { 199.99993, 150.00021 } };

            GeoPoint3[] places = spline.GetIntersections(lifted, Strict);
            GeoPoint3[] back = lifted.GetIntersections(spline, Strict);

            Assert.Equal(3, places.Length);
            Assert.Equal(3, back.Length);

            for (int i = 0; i < 3; i++)
            {
                GeoPoint3 local = frame.ToLocal(places[i]);
                Assert.True(Math.Abs(local.X - near[i][0]) <= 1E-4 && Math.Abs(local.Y - near[i][1]) <= 1E-4, "the place " + local);
            }

            Assert.InRange(spline.DistanceTo(lifted, Strict), 0.0002 - 1E-9, 0.0002 + 1E-9);
            Assert.True(spline.CollidesWith(lifted, Strict));
        }

        /// <summary>A degree-four spline of ten control points with a turn tighter than a millimetre, about 30 across.</summary>
        private static readonly double[] TightTurn =
        {
            -3.78640374206479, -6.7904581755800333, 0.45539147937324209, -11.537363396347109, -6.0850957702112511, -10.041782731811548,
            -9.3524846592728039, -15.657021793663404, -9.6258942831918457, -22.783004975266234, -8.4912876109286213, -17.490137122364434,
            -7.6106566528480339, -18.467368414307558, -2.1744017624320922, -11.570168377506185, 4.4797817131940931, -8.3688637077908687,
            -1.7386922377114802, -11.833547111904171,
        };

        private static GeoSpline3 TightCopy(GeoCoordinateSystem3 frame, double dx, double dy, double lift)
            => GeoSpline3.FromControlPoints(4, Enumerable.Range(0, 10).Select(i => At(frame, TightTurn[2 * i] + dx, TightTurn[2 * i + 1] + dy, lift)));

        [Theory]
        [InlineData(0.0)]
        [InlineData(2E-4)]
        public void AMovedCopyOfATightSplineInATiltedPlane_IsAnsweredAsFastAsInTheFloor(double lift)
        {
            // The copy is moved (-0.00012, -0.00073) along the plane, and lifted off it or not: in the plane of the unit directions (1, 2, 2) / 3
            // and (-2, 1, 0) / sqrt 5 the first round took 51 s for the places, the boxes of its parts lying across the axes and fat, and the
            // floor 0.27 s. Lifted, the two give the same places in either plane, the leasts of the distance where the one turns across the
            // other, which the first round did not give at all.
            var turned = new GeoCoordinateSystem3(P(0, 0, 0), new GeoVector3(1.0 / 3.0, 2.0 / 3.0, 2.0 / 3.0), new GeoVector3(-2.0 / Math.Sqrt(5.0), 1.0 / Math.Sqrt(5.0), 0.0));
            var watch = Stopwatch.StartNew();
            GeoPoint3[] floor = TightCopy(Frame("floor"), 0, 0, 0).GetIntersections(TightCopy(Frame("floor"), -0.00012038380891121747, -0.0007341883646949654, lift), Strict);
            double inFloor = watch.Elapsed.TotalSeconds;
            watch.Restart();
            GeoPoint3[] tilted = TightCopy(turned, 0, 0, 0).GetIntersections(TightCopy(turned, -0.00012038380891121747, -0.0007341883646949654, lift), Strict);
            double inTilted = watch.Elapsed.TotalSeconds;

            Assert.True(inFloor < 10.0, "took " + inFloor + " s in the floor");
            Assert.True(inTilted < 10.0, "took " + inTilted + " s tilted");
            Assert.NotEmpty(floor);

            if (lift > 0.0)
            {
                Assert.Equal(floor.Length, tilted.Length);
            }
        }

        public static TheoryData<string, string> RimsOfThePlane()
        {
            var data = new TheoryData<string, string>();

            foreach (string frame in new[] { "floor", "tilted", "a hundred thousand out", "a million out" })
            {
                foreach (string kind in new[] { "circle", "arc", "arc clockwise", "ellipse", "edge", "chain" })
                {
                    data.Add(frame, kind);
                }
            }

            return data;
        }

        /// <summary>Gets the places of a circle spline and a rim of the plane, in the plane and with both lifted into a frame.</summary>
        private static void Rim(GeoSpline2 flat, GeoCoordinateSystem3 frame, string kind, out GeoPoint2[] inPlane, out GeoPoint3[] places)
        {
            GeoSpline3 spline = flat.ToSpline3(frame);
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.3, 4.0);
            var clockwise = new GeoArc2(new GeoPoint2(0, 0), 100, 4.0, 0.3, true);
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(0.6, 0.8), 100, 90);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(-100, -200), new GeoPoint2(-100, 0), new GeoPoint2(100, 0) }, new[] { 0.0, -1.0, 0.0 });

            switch (kind)
            {
                case "circle":
                    inPlane = flat.GetIntersections(new GeoCircle2(new GeoPoint2(0, 0), 100), Strict);
                    places = spline.GetIntersections(new GeoCircle3(frame.Origin, frame.ZAxis, 100), Strict);
                    break;
                case "arc":
                    inPlane = flat.GetIntersections(arc, Strict);
                    places = spline.GetIntersections(GeometryHelper.Core.PlanarMap.ToArc3(frame, arc), Strict);
                    break;
                case "arc clockwise":
                    inPlane = flat.GetIntersections(clockwise, Strict);
                    places = spline.GetIntersections(GeometryHelper.Core.PlanarMap.ToArc3(frame, clockwise), Strict);
                    break;
                case "ellipse":
                    inPlane = flat.GetIntersections(ellipse, Strict);
                    places = spline.GetIntersections(GeometryHelper.Core.PlanarMap.ToEllipse3(frame, ellipse), Strict);
                    break;
                case "edge":
                    inPlane = flat.GetIntersections(new GeoEdge2(arc), Strict);
                    places = spline.GetIntersections(GeometryHelper.Core.PlanarMap.ToEdge3(frame, new GeoEdge2(arc)), Strict);
                    break;
                default:
                    inPlane = flat.GetIntersections(chain, Strict);
                    places = spline.GetIntersections(GeometryHelper.Core.PlanarMap.ToPolylineArc3(frame, chain), Strict);
                    break;
            }
        }

        [Theory]
        [MemberData(nameof(RimsOfThePlane))]
        public void ACircleSplineMovedUnderTheTolerance_MeetsARimOfItsOwnPlaneAsInThePlane(string name, string kind)
        {
            // A circle spline of radius 100 moved (0.0003, 0.0005) lies within the tolerance of the circle all the way round, and of an arc
            // or an edge of it, and of the chain whose half turn follows it, and crosses each where the plane says: the circle three times,
            // the arc from 0.3 to 4 radians twice either way round, the chain three times. An ellipse 100 by 90 along (0.6, 0.8) inside the
            // circle touches it at the two ends of its long axis. Laid in a plane of space, the pair was worked in space, where the offset of
            // one from the other has no sign: the circle's three places came out as none, the arc's two as one.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline2 flat = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0.0003, 0.0005), 100));

            Rim(flat, frame, kind, out GeoPoint2[] inPlane, out GeoPoint3[] places);

            Assert.Equal(inPlane.Length, places.Length);

            foreach (GeoPoint2 p in inPlane)
            {
                GeoPoint3 lifted = At(frame, p.X, p.Y);
                Assert.True(places.Min(q => q.DistanceTo(lifted)) <= 1E-4, "the plane's place " + p + " is not one in space");

                // In the floor the numbers of a circle and an ellipse are the plane's, and so are the places, to the bit. An arc in space keeps
                // its start as a point, and the angle read back from it is the plane's to rounding.
                if (name == "floor" && (kind == "circle" || kind == "ellipse"))
                {
                    Assert.Contains(places, q => BitConverter.DoubleToInt64Bits(q.X) == BitConverter.DoubleToInt64Bits(p.X) && BitConverter.DoubleToInt64Bits(q.Y) == BitConverter.DoubleToInt64Bits(p.Y) && q.Z == 0.0);
                }
            }
        }

        [Fact]
        public void ASplineAndACopyHalfAThousandthOffWithAWobble_MeetNowhere_InItsPlaneOrLiftedOffIt()
        {
            // A cubic through 30 points of a sine, 20 apart, and a copy 0.0005 across with every other control point a further 0.0001 out or
            // in: the two come 0.00034 near where the wobble brings them nearest but never cross, in the plane. Lifted off it by more than a
            // snap (6E-11 here), every such least was a place, nine of them, and the answer jumped at the snap.
            GeoPoint3[] points = Enumerable.Range(0, 30).Select(i => P(20.0 * i, 50 * Math.Sin(i * 0.4), 0)).ToArray();
            GeoSpline3 spline = GeoSpline3.FromControlPoints(3, points);

            foreach (double lift in new[] { 0.0, 3E-11, 6.06E-11, 1E-8, 1E-6, 1E-4 })
            {
                GeoSpline3 copy = GeoSpline3.FromControlPoints(3, points.Select((p, i) => P(p.X, p.Y + 0.0005 + (i % 2 == 0 ? 0.0001 : -0.0001), lift)));

                Assert.Empty(spline.GetIntersections(copy, Strict));
                Assert.Empty(copy.GetIntersections(spline, Strict));
                Assert.InRange(spline.DistanceTo(copy, Strict), 0.000338, 0.000353);
                Assert.True(spline.CollidesWith(copy, Strict));
            }
        }

        [Fact]
        public void ATwistedCubicOfSixHundredPointsAndItsCopyMovedNineTenThousandths_IsAnsweredInASecond()
        {
            // A cubic winding round and up through 600 points and its copy moved 0.0009 along (0.48, 0.6, 0.64): the two lie along each
            // other within the tolerance the whole way, and every least was read for a place by cutting the pair further, 20 s and two
            // million pairs of parts. Inside the stretch along, a least is looked at once, on its own.
            GeoPoint3[] points = Enumerable.Range(0, 600).Select(i => P(100 * Math.Cos(0.37 * i) + 7 * i, 80 * Math.Sin(0.23 * i), 60 * Math.Cos(0.11 * i) + 3 * i)).ToArray();
            GeoSpline3 spline = GeoSpline3.FromControlPoints(3, points);
            GeoSpline3 copy = GeoSpline3.FromControlPoints(3, points.Select(p => P(p.X + 0.000432, p.Y + 0.00054, p.Z + 0.000576)));
            var watch = Stopwatch.StartNew();

            GeoPoint3[] places = spline.GetIntersections(copy, Strict);
            double seconds = watch.Elapsed.TotalSeconds;
            GeoPoint3[] back = copy.GetIntersections(spline, Strict);

            Assert.True(seconds < 5.0, "took " + seconds + " s");
            Assert.Equal(places.Length, back.Length);
            Assert.All(places, p => Assert.True(spline.DistanceTo(p) <= 1E-9 && copy.DistanceTo(p) <= 1E-3 + 1E-9, "the place " + p));
        }

        [Fact]
        public void ARationalCubicAndItsCopyMovedATenThousandthOutOfItsPlane_MeetOnlyAtTheLeastsOfTheirDistance()
        {
            // A rational cubic about 2 000 across and its copy moved 0.0001 along (0.436, -0.080, 0.896) come nearest at knots -3.4974,
            // -2.6392 and -0.8081, where seen along the gap the copy goes across it. Near the first the distance is so flat, 6E-7 over 90,
            // that a least was settled at -3.5843 on its slope and given as a fourth place, 90 from the first.
            double[] xyz =
            {
                950.3743, -155.2232, 412.5745, -189.8038, -260.696, -307.254, -709.5205, -615.6945, -803.5523, -398.0145, -161.1518, 819.0686,
                -597.2508, 854.1051, -294.6801, 508.42, -623.2203, 71.1448, -476.275, -307.1767, -846.4116, 614.4399, -363.8709, -950.0663,
            };
            double end = -0.16699999999999982;
            double[] knots = { -4.377, -4.377, -4.377, -4.377, -3.9077766196042258, -3.0106095666397321, -1.874461715161464, -1.1716514388797794, end, end, end, end };
            var spline = new GeoSpline3(3, Enumerable.Range(0, 8).Select(i => P(xyz[3 * i], xyz[3 * i + 1], xyz[3 * i + 2])), knots, new[] { 0.986, 1.553, 0.204, 3.875, 0.849, 4.006, 4.232, 4.479 });
            GeoSpline3 copy = spline + new GeoVector3(0.43642229281940431 * 1E-4, -0.079547599395239421 * 1E-4, 0.896218590390039 * 1E-4);

            GeoPoint3[] places = spline.GetIntersections(copy, Strict);
            GeoPoint3[] back = copy.GetIntersections(spline, Strict);

            Assert.Equal(new[] { -3.4974, -2.6392, -0.8081 }, places.Select(p => Math.Round(spline.GetKnotAtPoint(p), 4)).ToArray());
            Assert.Equal(places.Length, back.Length);
        }

        #endregion

        #region A line in the plane of a spline, and over it

        [Theory]
        [MemberData(nameof(Frames))]
        public void ALineThroughAPointOfACubicAMillionthOfARadianOffItsTangent_CrossesItThere_AndIsNoughtFromIt(string name)
        {
            // The line through (90, 31.5), a point of the cubic, turned a millionth of a radian off its tangent there: it crosses the cubic
            // there and again a few ten-thousandths on, one place. The square of the offset has a double root at each crossing, which the
            // rounding lumped, and the distance read 1.6E-10 with no place; the signed offset in the plane of the two has a simple one.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 cubic = Through(3, frame, 0, 0, 100, 50, 200, 50, 300, 0);
            var line = new GeoXLine3(At(frame, 90, 31.5), frame.ToGlobal(new GeoVector3(0.98058087180656506, 0.19611515455741033, 0)));

            GeoPoint3[] places = cubic.GetIntersections(line, Strict);

            Assert.Equal(0.0, cubic.DistanceTo(line, Strict));
            Assert.Equal(0.0, line.DistanceTo(cubic, Strict));
            Assert.Single(places);
            Assert.True(places[0].DistanceTo(At(frame, 90, 31.5)) <= 2E-3, "the place " + places[0]);
            Assert.True(cubic.CollidesWith(line, Strict));
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public void ALineAcrossTheInflectionOfACubic_CrossesItOnce(string name)
        {
            // The cubic (-10, -10), (-10/3, 10), (10/3, -10), (10, 10) is y = x^3 / 100, and the line y = 0.0001 crosses it once, at
            // x = 0.21544; where the cubic is level, at its inflection, its offset from the line stops falling and goes on, which is no place,
            // and was given as one a hair to the side of it.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 cubic = Through(3, frame, -10, -10, -10.0 / 3.0, 10, 10.0 / 3.0, -10, 10, 10);
            var line = new GeoXLine3(At(frame, 0, 1E-4), frame.XAxis);

            GeoPoint3[] places = cubic.GetIntersections(line, Strict);

            Assert.Single(places);
            Assert.True(Math.Abs(Where(frame, places[0], out _) - 0.215443469) <= 1E-6, "the place " + places[0]);
            Assert.Equal(0.0, cubic.DistanceTo(line, Strict));
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public void ALineOverTheInflectionOfACubicHalfAThousandthAboveItsPlane_TouchesItOnce(string name)
        {
            // The line along x half a thousandth over the plane of the cubic: the distance is 0.0005 plus the cube of x squared, a least
            // of the sixth order with no curvature, which is a place, and rises 6E-12 a thousandth of the size along, which is no rise.
            GeoCoordinateSystem3 frame = Frame(name);
            GeoSpline3 cubic = Through(3, frame, -10, -10, -10.0 / 3.0, 10, 10.0 / 3.0, -10, 10, 10);
            var line = new GeoXLine3(At(frame, 0, 0, 5E-4), frame.XAxis);

            GeoPoint3[] places = cubic.GetIntersections(line, Strict);

            Assert.InRange(cubic.DistanceTo(line, Strict), 5E-4 - 1E-9, 5E-4 + 1E-9);
            Assert.True(cubic.CollidesWith(line, Strict));
            Assert.Single(places);
            Assert.True(Math.Abs(Where(frame, places[0], out _)) <= 0.05, "the place " + places[0]);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void AHumpTenSnapsBelowAPlane_IsNoPlace_ButTheTwoCrossingsEitherSideAre(double far)
        {
            // In the plane through (far, far / 2, -far / 5) square to (0.3, -0.5, 0.8), the parabola y = (x - 400)^2 / 1600 - 0.000001 dips a
            // millionth below the plane y = 0 that stands square to its own, ten snaps a million out, and crosses it 0.04 either side of
            // x = 400: the bottom of the dip is a greatest of the distance, and was given as a third place a million out, where the distance
            // there looked level a thousandth of the size either way.
            GeoVector3 normal = new GeoVector3(0.3, -0.5, 0.8).Normalize();
            GeoVector3 along = normal.CrossProduct(GeoVector3.XAxis).Normalize();
            GeoVector3 up = normal.CrossProduct(along);
            GeoPoint3 origin = P(far, 0.5 * far, -0.2 * far);
            Func<double, double, GeoPoint3> at = (x, y) => P(origin.X + x * along.X + y * up.X, origin.Y + x * along.Y + y * up.Y, origin.Z + x * along.Z + y * up.Z);
            GeoSpline3 parabola = GeoSpline3.FromControlPoints(2, new[] { at(0, 100 - 1E-6), at(400, -100 - 1E-6), at(800, 100 - 1E-6) });
            var plane = new GeoPlane3(origin, up);

            GeoPoint3[] places = parabola.GetIntersections(plane, Strict);

            Assert.Equal(2, places.Length);
            Assert.Equal(0.0, parabola.DistanceTo(plane, Strict));
        }

        #endregion

        #region Budgets

        [Theory]
        [MemberData(nameof(Frames))]
        public void TwoHugeCirclesInSpaceNearlyConcentric_AreAsFarApartAsTheyAre_InTheTimeAQuestionGets(string name)
        {
            // Radius 100 000 and 100 000.0005, the second with its centre moved 2E-4 toward 45 degrees: 0.0003 apart that way, in 0.15 s.
            GeoCoordinateSystem3 frame = Frame(name);
            double step = 2E-4 / Math.Sqrt(2.0);
            GeoSpline3 first = Ring(frame, 0, 0, 100000);
            GeoSpline3 second = Ring(frame, step, step, 100000.0005);
            var watch = Stopwatch.StartNew();

            double distance = first.DistanceTo(second, Strict);

            Assert.InRange(distance, 0.00029, 0.0003 + 1E-9);
            Assert.Equal(distance, second.DistanceTo(first, Strict));
            Assert.True(watch.Elapsed.TotalSeconds < 20.0, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        [Fact]
        public void ASplineOfAHundredThousandSpans_AgainstAPlaneAndALine_IsAsFastAsAPointQuery()
        {
            var points = Enumerable.Range(0, 100000).Select(i => P(0.01 * i * Math.Cos(i * 0.05), 0.01 * i * Math.Sin(i * 0.05), 0.001 * i)).ToArray();
            GeoSpline3 spiral = GeoSpline3.FromControlPoints(3, points);
            GeoPlane3 plane = new GeoPlane3(P(0, 0, 50), GeoVector3.ZAxis);
            var line = new GeoLine3(P(-2000, 1, 40), P(2000, 1, 40));
            var watch = Stopwatch.StartNew();

            double toPlane = spiral.DistanceTo(plane, Strict);
            double toLine = spiral.DistanceTo(line, Strict);
            GeoPoint3[] places = spiral.GetIntersections(plane, Strict);
            GeoPoint3[] along = spiral.GetIntersections(line, Strict);

            Assert.Equal(0.0, toPlane);
            Assert.True(toLine >= 0.0);
            Assert.NotEmpty(places);
            Assert.All(places, p => Assert.True(Math.Abs(p.Z - 50.0) <= 1E-6));
            Assert.All(along, p => Assert.True(spiral.DistanceTo(p) <= 1E-6));
            Assert.True(watch.Elapsed.TotalSeconds < 10.0, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        [Fact]
        public void APolylineOfAHundredThousandEdges_AgainstASpline_IsLinear()
        {
            GeoSpline3 arch = new GeoSpline3(3, new[] { P(0, 0, 0), P(100, 0, 150), P(200, 0, 150), P(300, 0, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            var polyline = new GeoPolyline3(Enumerable.Range(0, 100000).Select(i => P(300.0 * i / 99999, 20 + 5 * Math.Sin(i * 0.01), 60 + 40 * Math.Cos(i * 0.007))).ToArray());
            var watch = Stopwatch.StartNew();

            double distance = arch.DistanceTo(polyline, Strict);
            GeoPoint3[] places = arch.GetIntersections(polyline, Strict);

            Assert.True(distance >= 0.0);
            Assert.True(places.Length <= 100);
            Assert.True(watch.Elapsed.TotalSeconds < 10.0, "took " + watch.Elapsed.TotalSeconds + " s");
            Assert.Equal(distance, polyline.DistanceTo(arch, Strict));
        }

        [Fact]
        public void ASolidOfThirtyThousandFaces_AgainstASpline_IsAsFastAsItsFaces()
        {
            GeoSolid3 ball = new GeoSphere3(P(0, 0, 0), 50).ToSolid(0.01);
            var through = GeoSpline3.FromPolyline(new GeoPolyline3(P(-100, 3, 4), P(100, 3, 4)));
            var inside = GeoSpline3.FromPolyline(new GeoPolyline3(P(-10, 1, 2), P(10, 1, 2)));
            var outside = GeoSpline3.FromPolyline(new GeoPolyline3(P(-100, 70, 4), P(100, 70, 4)));
            var watch = Stopwatch.StartNew();

            Assert.True(ball.Faces.Count > 20000);
            Assert.Equal(0.0, through.DistanceTo(ball, Strict));
            Assert.Equal(2, through.GetIntersections(ball, Strict).Length);
            Assert.Equal(0.0, inside.DistanceTo(ball, Strict));
            Assert.Empty(inside.GetIntersections(ball, Strict));
            Assert.InRange(inside.GetShortestLineTo(ball, Strict).Length, 50 - Math.Sqrt(105) - 0.01, 50 - Math.Sqrt(105) + 0.01);
            Assert.InRange(outside.DistanceTo(ball, Strict), Math.Sqrt(70 * 70 + 16) - 50 - 0.01, Math.Sqrt(70 * 70 + 16) - 50 + 0.01);
            Assert.True(watch.Elapsed.TotalSeconds < 60.0, "took " + watch.Elapsed.TotalSeconds + " s");
        }

        #endregion
    }
}
