using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space, straight shapes and points lying in one plane, or off it by up to the planar tolerance, are as far apart
    /// as they truly are, and collide exactly where that gap is within the point tolerance. They used to be laid out in
    /// their plane and asked the old band tests of the plane: a segment starting 0.0005 off the square 100 by 100 in its
    /// plane was nought away at a tolerance of 0.001, from either end; a square whose corner stood 0.0012 off the end of
    /// a segment was nought away and collided; a segment hovering 0.0003 over the square was nought away, and one
    /// 0.0009 up and 0.0009 out, 0.00127 away, collided; a point 0.0005 off the square was nought away.
    /// </summary>
    public class Collision3InPlaneExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A plane in space: an origin, two axes in it and its normal.</summary>
        private sealed class Frame
        {
            public GeoPoint3 O;
            public GeoVector3 X;
            public GeoVector3 Y;
            public GeoVector3 N;

            /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate, 200 on the floor and 3 500 out there.</summary>
            public double Allowance;

            public GeoPoint3 P(double x, double y, double h = 0) => new GeoPoint3(
                O.X + x * X.X + y * Y.X + h * N.X, O.Y + x * X.Y + y * Y.Y + h * N.Y, O.Z + x * X.Z + y * Y.Z + h * N.Z);

            public GeoVector3 V(double x, double y) => new GeoVector3(x * X.X + y * Y.X, x * X.Y + y * Y.Y, x * X.Z + y * Y.Z);
        }

        /// <summary>The floor z = 0, or a plane turned 0.6 rad about Z and 0.8 about X, through (3000.25, -1500.5, 800.125).</summary>
        private static Frame On(string plane)
        {
            if (plane == "floor")
            {
                return new Frame { O = new GeoPoint3(0, 0, 0), X = GeoVector3.XAxis, Y = GeoVector3.YAxis, N = GeoVector3.ZAxis, Allowance = 1E-11 };
            }

            double ca = Math.Cos(0.6), sa = Math.Sin(0.6), cb = Math.Cos(0.8), sb = Math.Sin(0.8);
            return new Frame
            {
                O = new GeoPoint3(3000.25, -1500.5, 800.125),
                X = new GeoVector3(ca, sa, 0),
                Y = new GeoVector3(-sa * cb, ca * cb, sb),
                N = new GeoVector3(sa * sb, -ca * sb, cb),
                Allowance = 2E-10,
            };
        }

        /// <summary>The square 100 by 100 with a corner at the frame's origin.</summary>
        private static GeoPolygon3 Square(Frame f) => Box(f, 0, 0, 100, 100);

        private static GeoPolygon3 Box(Frame f, double left, double bottom, double right, double top, double h = 0) =>
            new GeoPolygon3(f.P(left, bottom, h), f.P(right, bottom, h), f.P(right, top, h), f.P(left, top, h));

        /// <summary>The square with a hole 20 by 20 in its middle.</summary>
        private static GeoFace3 Plate(Frame f) => new GeoFace3(Square(f), new[] { Box(f, 40, 40, 60, 60) });

        /// <summary>The box under the square, 50 deep: an axis-aligned box on the floor, an oriented one on any plane.</summary>
        private static GeoObb3 Block(Frame f) => new GeoObb3(f.P(50, 50, -25), 100, 100, 50, f.X, f.Y);

        /// <summary>The double nearest 100.0005: half a tolerance out from the side x = 100, its gap exact.</summary>
        private const double Off = 100.0005;

        private static readonly double Half = Off - 100;

        /// <summary>
        /// Asks a member that takes no tolerance at the test's own. It reads the tolerance in force on this thread, so it is
        /// asked inside a scope of that tolerance, which reaches no other thread; Tolerance.Global is never set.
        /// </summary>
        private static double Within(Tolerance tolerance, Func<double> distance)
        {
            using (Tolerance.Use(tolerance))
            {
                return distance();
            }
        }

        private static void AssertGap(double expected, double actual, double allowance)
        {
            Assert.InRange(actual, expected - allowance, expected + allowance);
        }

        /// <summary>Both ways round the distance is the gap, and both ways round they collide exactly where it is within the tolerance.</summary>
        private static void AssertMeasured(Frame f, (double Gap, double There, double Back, bool Collides, bool CollidesBack) m)
        {
            AssertGap(m.Gap, m.There, f.Allowance);
            AssertGap(m.Gap, m.Back, f.Allowance);
            Assert.Equal(m.Gap <= Tol.EqualPoint, m.Collides);
            Assert.Equal(m.Gap <= Tol.EqualPoint, m.CollidesBack);
        }

        private static IEnumerable<object[]> OnBothPlanes(params string[] kinds)
        {
            foreach (string plane in new[] { "floor", "turned" })
            {
                foreach (string kind in kinds)
                {
                    yield return new object[] { kind, plane };
                }
            }
        }

        #region T1: half a tolerance off, in the plane

        public static IEnumerable<object[]> InThePlaneCases() => OnBothPlanes(
            "segment starting off the side", "segment ending off the side", "segment starting off the triangle",
            "segment starting off the face", "segment in the hole off its rim", "two squares side to side",
            "polyline starting off the side", "ray starting off the side", "two rays leaving each other",
            "ray leaving the polyline's start", "segment off the block's top", "segment off the solid's top");

        [Theory]
        [MemberData(nameof(InThePlaneCases))]
        public void AStraightShapeHalfAToleranceOffInThePlane_IsThatFarEitherWayRoundAndCollides(string kind, string plane)
        {
            Frame f = On(plane);
            Tolerance t = Tol;
            GeoPolygon3 square = Square(f);
            var away = new GeoLine3(f.P(Off, 50), f.P(150, 50));

            switch (kind)
            {
                case "segment starting off the side":
                    AssertMeasured(f, (Half, square.DistanceTo(away, t), away.DistanceTo(square, t), square.CollidesWith(away, t), away.CollidesWith(square, t)));
                    break;

                case "segment ending off the side":
                {
                    var toward = new GeoLine3(f.P(150, 50), f.P(Off, 50));
                    AssertMeasured(f, (Half, square.DistanceTo(toward, t), toward.DistanceTo(square, t), square.CollidesWith(toward, t), toward.CollidesWith(square, t)));
                    break;
                }

                case "segment starting off the triangle":
                {
                    // Below the side y = 0 of the triangle (0, 0), (100, 0), (0, 100), running away from it.
                    var tri = new GeoTriangle3(f.P(0, 0), f.P(100, 0), f.P(0, 100));
                    var line = new GeoLine3(f.P(50, -Half), f.P(50, -50));
                    AssertMeasured(f, (Half, tri.DistanceTo(line, t), line.DistanceTo(tri, t), tri.CollidesWith(line, t), line.CollidesWith(tri, t)));
                    break;
                }

                case "segment starting off the face":
                {
                    GeoFace3 plate = Plate(f);
                    AssertMeasured(f, (Half, plate.DistanceTo(away, t), away.DistanceTo(plate, t), plate.CollidesWith(away, t), away.CollidesWith(plate, t)));
                    break;
                }

                case "segment in the hole off its rim":
                {
                    // Inside the hole, starting half a tolerance in from its rim x = 40 and running into the hole.
                    GeoFace3 plate = Plate(f);
                    double x = 40 + Half;
                    var line = new GeoLine3(f.P(x, 50), f.P(50, 50));
                    AssertMeasured(f, (x - 40, plate.DistanceTo(line, t), line.DistanceTo(plate, t), plate.CollidesWith(line, t), line.CollidesWith(plate, t)));
                    break;
                }

                case "two squares side to side":
                {
                    GeoPolygon3 other = Box(f, Off, 0, 200, 100);
                    AssertMeasured(f, (Half, square.DistanceTo(other, t), other.DistanceTo(square, t), square.CollidesWith(other, t), other.CollidesWith(square, t)));
                    break;
                }

                case "polyline starting off the side":
                {
                    var chain = new GeoPolyline3(f.P(Off, 50), f.P(150, 50), f.P(150, 80));
                    AssertMeasured(f, (Half, square.DistanceTo(chain, t), chain.DistanceTo(square, t), square.CollidesWith(chain, t), chain.CollidesWith(square, t)));
                    break;
                }

                case "ray starting off the side":
                {
                    var ray = new GeoRay3(f.P(Off, 50), f.V(1, 0));
                    AssertMeasured(f, (Half, square.DistanceTo(ray, t), ray.DistanceTo(square, t), square.CollidesWith(ray, t), ray.CollidesWith(square, t)));
                    break;
                }

                case "two rays leaving each other":
                {
                    // Origins 0.0003 apart along X, each pointing away from the other.
                    var a = new GeoRay3(f.P(0, 0), f.V(1, 0));
                    var b = new GeoRay3(f.P(-0.0003, 0), f.V(-1, 0));
                    AssertMeasured(f, (0.0003, a.DistanceTo(b, t), b.DistanceTo(a, t), a.CollidesWith(b, t), b.CollidesWith(a, t)));
                    break;
                }

                case "ray leaving the polyline's start":
                {
                    // The polyline (0, 0), (60, 0), (60, 30); the ray starts half a tolerance behind its start, going back.
                    var chain = new GeoPolyline3(f.P(0, 0), f.P(60, 0), f.P(60, 30));
                    var ray = new GeoRay3(f.P(-Half, 0), f.V(-1, 0));
                    AssertMeasured(f, (Half, chain.DistanceTo(ray, t), ray.DistanceTo(chain, t), chain.CollidesWith(ray, t), ray.CollidesWith(chain, t)));
                    break;
                }

                case "segment off the block's top":
                {
                    // In the plane of the block's top face, starting off its side.
                    if (plane == "floor")
                    {
                        var aabb = new GeoAabb3(new GeoPoint3(0, 0, -50), new GeoPoint3(100, 100, 0));
                        AssertMeasured(f, (Half, aabb.DistanceTo(away, t), away.DistanceTo(aabb, t), aabb.CollidesWith(away, t), away.CollidesWith(aabb, t)));
                    }
                    else
                    {
                        GeoObb3 obb = Block(f);
                        AssertMeasured(f, (Half, obb.DistanceTo(away, t), away.DistanceTo(obb, t), obb.CollidesWith(away, t), away.CollidesWith(obb, t)));
                    }

                    break;
                }

                default:
                {
                    GeoSolid3 solid = Block(f).ToSolid();
                    AssertMeasured(f, (Half, solid.DistanceTo(away, t), away.DistanceTo(solid, t), solid.CollidesWith(away, t), away.CollidesWith(solid, t)));
                    break;
                }
            }
        }

        #endregion

        #region T2: past the tolerance at a corner

        public static IEnumerable<object[]> PastTheToleranceCases() => OnBothPlanes(
            "square starting off a segment's end", "segment off the block's corner", "segment off the solid's corner");

        [Theory]
        [MemberData(nameof(PastTheToleranceCases))]
        public void AShapeOneAndAFifthTolerancesOffACorner_IsThatFarAndDoesNotCollide(string kind, string plane)
        {
            // 0.00085 off in both directions of the plane: 0.0012021 away, past the tolerance.
            Frame f = On(plane);
            Tolerance t = Tol;
            double d = 100.00085 - 100;
            double gap = d * Math.Sqrt(2);

            switch (kind)
            {
                case "square starting off a segment's end":
                {
                    // The segment from (0, 0) to (60, 0); the square's first corner at (60 + d, d), the square running away.
                    var line = new GeoLine3(f.P(0, 0), f.P(60, 0));
                    var square = new GeoPolygon3(f.P(60 + d, d), f.P(160 + d, d), f.P(160 + d, 100 + d), f.P(60 + d, 100 + d));
                    AssertMeasured(f, (gap, line.DistanceTo(square, t), square.DistanceTo(line, t), line.CollidesWith(square, t), square.CollidesWith(line, t)));
                    break;
                }

                case "segment off the block's corner":
                {
                    var line = new GeoLine3(f.P(100 + d, 100 + d), f.P(150, 150));
                    if (plane == "floor")
                    {
                        var aabb = new GeoAabb3(new GeoPoint3(0, 0, -50), new GeoPoint3(100, 100, 0));
                        AssertMeasured(f, (gap, aabb.DistanceTo(line, t), line.DistanceTo(aabb, t), aabb.CollidesWith(line, t), line.CollidesWith(aabb, t)));
                    }
                    else
                    {
                        GeoObb3 obb = Block(f);
                        AssertMeasured(f, (gap, obb.DistanceTo(line, t), line.DistanceTo(obb, t), obb.CollidesWith(line, t), line.CollidesWith(obb, t)));
                    }

                    break;
                }

                default:
                {
                    var line = new GeoLine3(f.P(100 + d, 100 + d), f.P(150, 150));
                    GeoSolid3 solid = Block(f).ToSolid();
                    AssertMeasured(f, (gap, solid.DistanceTo(line, t), line.DistanceTo(solid, t), solid.CollidesWith(line, t), line.CollidesWith(solid, t)));
                    break;
                }
            }
        }

        #endregion

        #region T3: off the plane by up to the planar tolerance, measured in space

        public static IEnumerable<object[]> OffThePlaneCases() => OnBothPlanes(
            "segment 0.0003 over the square", "segment 0.0009 up and 0.0009 out", "square 0.0003 over the square",
            "segment 0.0003 over the block's top");

        [Theory]
        [MemberData(nameof(OffThePlaneCases))]
        public void AShapeOffThePlaneByLessThanThePlanarTolerance_IsAsFarAsItIsInSpace(string kind, string plane)
        {
            Frame f = On(plane);
            Tolerance t = Tol;
            GeoPolygon3 square = Square(f);

            switch (kind)
            {
                case "segment 0.0003 over the square":
                {
                    var line = new GeoLine3(f.P(20, 20, 0.0003), f.P(30, 40, 0.0003));
                    AssertMeasured(f, (0.0003, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }

                case "segment 0.0009 up and 0.0009 out":
                {
                    // Beside the side x = 100, 0.0009 out and 0.0009 up: sqrt(2) 0.0009 = 0.00127 away, past the tolerance.
                    var line = new GeoLine3(f.P(100.0009, 50, 0.0009), f.P(150, 50, 0.0009));
                    double gap = Math.Sqrt(2) * (100.0009 - 100);
                    AssertMeasured(f, (gap, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }

                case "square 0.0003 over the square":
                {
                    GeoPolygon3 over = Box(f, 50, 50, 150, 150, 0.0003);
                    AssertMeasured(f, (0.0003, square.DistanceTo(over, t), over.DistanceTo(square, t), square.CollidesWith(over, t), over.CollidesWith(square, t)));
                    break;
                }

                default:
                {
                    var line = new GeoLine3(f.P(20, 20, 0.0003), f.P(30, 40, 0.0003));
                    if (plane == "floor")
                    {
                        var aabb = new GeoAabb3(new GeoPoint3(0, 0, -50), new GeoPoint3(100, 100, 0));
                        AssertMeasured(f, (0.0003, aabb.DistanceTo(line, t), line.DistanceTo(aabb, t), aabb.CollidesWith(line, t), line.CollidesWith(aabb, t)));
                    }
                    else
                    {
                        GeoObb3 obb = Block(f);
                        AssertMeasured(f, (0.0003, obb.DistanceTo(line, t), line.DistanceTo(obb, t), obb.CollidesWith(line, t), line.CollidesWith(obb, t)));
                    }

                    break;
                }
            }
        }

        #endregion

        #region T4: a point

        public static IEnumerable<object[]> PointCases() => OnBothPlanes(
            "square", "face outline", "face hole", "solid's top", "under the square's plane");

        [Theory]
        [MemberData(nameof(PointCases))]
        public void APointHalfAToleranceOff_IsThatFarEitherWayRound(string kind, string plane)
        {
            // No member says whether a point collides; the distance is asked both ways round.
            Frame f = On(plane);
            Tolerance t = Tol;
            GeoPoint3 off = f.P(Off, 50);

            switch (kind)
            {
                case "square":
                {
                    GeoPolygon3 square = Square(f);
                    AssertGap(Half, Within(t, () => square.DistanceTo(off)), f.Allowance);
                    AssertGap(Half, Within(t, () => off.DistanceTo(square)), f.Allowance);
                    break;
                }

                case "face outline":
                    AssertGap(Half, Plate(f).DistanceTo(off, t), f.Allowance);
                    AssertGap(Half, off.DistanceTo(Plate(f), t), f.Allowance);
                    break;

                case "face hole":
                {
                    // In the hole, half a tolerance in from its rim x = 40.
                    double x = 40 + Half;
                    GeoPoint3 p = f.P(x, 50);
                    AssertGap(x - 40, Plate(f).DistanceTo(p, t), f.Allowance);
                    AssertGap(x - 40, p.DistanceTo(Plate(f), t), f.Allowance);
                    break;
                }

                case "solid's top":
                {
                    // In the plane of the solid's top face, off its side.
                    GeoSolid3 solid = Block(f).ToSolid();
                    AssertGap(Half, solid.DistanceTo(off, t), f.Allowance);
                    AssertGap(Half, off.DistanceTo(solid, t), f.Allowance);
                    break;
                }

                default:
                {
                    // 0.0004 under the square's plane and half a tolerance out: 0.00064 away.
                    GeoPolygon3 square = Square(f);
                    GeoPoint3 p = f.P(Off, 50, -0.0004);
                    double gap = Math.Sqrt(Half * Half + 0.0004 * 0.0004);
                    AssertGap(gap, Within(t, () => square.DistanceTo(p)), f.Allowance);
                    AssertGap(gap, Within(t, () => p.DistanceTo(square)), f.Allowance);
                    break;
                }
            }
        }

        #endregion

        #region T10: a collision missed within the tolerance at a segment's end

        public static IEnumerable<object[]> EndCases() => OnBothPlanes("polygon", "polyline's end");

        [Theory]
        [MemberData(nameof(EndCases))]
        public void AnEdgePassingNineTenthsOfAToleranceOffASegmentsEnd_IsThatFarAndCollides(string kind, string plane)
        {
            // The segment from (0, 0) to (60, 0). A square lies beyond its end E = (60, 0) along the diagonal d = (1, 1) / sqrt 2,
            // its near side square to d and 0.0009 out along it: E is the segment's nearest point, 0.0009 from that side.
            Frame f = On(plane);
            Tolerance t = Tol;
            double g = 0.0009;
            double r = Math.Sqrt(0.5);
            double cx = 60 + g * r, cy = g * r;
            GeoPoint3 Q(double along, double aside) => f.P(cx + along * r - aside * r, cy + along * r + aside * r);
            var square = new GeoPolygon3(Q(0, -30), Q(40, -30), Q(40, 30), Q(0, 30));
            GeoLine3 seg = new GeoLine3(f.P(0, 0), f.P(60, 0));

            if (kind == "polygon")
            {
                AssertMeasured(f, (g, seg.DistanceTo(square, t), square.DistanceTo(seg, t), seg.CollidesWith(square, t), square.CollidesWith(seg, t)));
            }
            else
            {
                var chain = new GeoPolyline3(f.P(0, 30), f.P(0, 0), f.P(60, 0));
                AssertMeasured(f, (g, chain.DistanceTo(square, t), square.DistanceTo(chain, t), chain.CollidesWith(square, t), square.CollidesWith(chain, t)));
            }
        }

        #endregion

        #region Controls: what already reads right and must stay so

        public static IEnumerable<object[]> ControlCases() => OnBothPlanes(
            "segment crossing the square", "segment piercing the square", "segment five tolerances off",
            "segments crossing in plan 0.0005 apart", "segment starting on the side");

        [Theory]
        [MemberData(nameof(ControlCases))]
        public void ACrossingAPiercingAWideGapOrATouch_ReadsAsItDidAndCollidesWithinTheTolerance(string kind, string plane)
        {
            Frame f = On(plane);
            Tolerance t = Tol;
            GeoPolygon3 square = Square(f);

            switch (kind)
            {
                case "segment crossing the square":
                {
                    var line = new GeoLine3(f.P(50, 50), f.P(150, 50));
                    AssertMeasured(f, (0.0, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }

                case "segment piercing the square":
                {
                    var line = new GeoLine3(f.P(50, 50, -1), f.P(50, 50, 1));
                    AssertMeasured(f, (0.0, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }

                case "segment five tolerances off":
                {
                    var line = new GeoLine3(f.P(100.005, 50), f.P(150, 50));
                    AssertMeasured(f, (100.005 - 100, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }

                case "segments crossing in plan 0.0005 apart":
                {
                    // One along X in the plane, the other along Y 0.0005 above it: the gap is the height.
                    var a = new GeoLine3(f.P(0, 0), f.P(10, 0));
                    var b = new GeoLine3(f.P(5, -5, 0.0005), f.P(5, 5, 0.0005));
                    AssertMeasured(f, (0.0005, Within(t, () => a.DistanceTo(b)), Within(t, () => b.DistanceTo(a)), a.CollidesWith(b, t), b.CollidesWith(a, t)));
                    break;
                }

                default:
                {
                    var line = new GeoLine3(f.P(100, 50), f.P(150, 50));
                    AssertMeasured(f, (0.0, square.DistanceTo(line, t), line.DistanceTo(square, t), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                    break;
                }
            }
        }

        #endregion

        #region The convention kept: a segment shorter than the tolerance is read as its start

        [Fact]
        public void ASegmentShorterThanTheTolerance_IsStillMeasuredFromItsStart()
        {
            // 0.0005 long, upright at x = -0.0003 across the end (0, 0, 0) of a segment along X: its nearest point is 0.0003
            // away, but a segment shorter than EqualPoint is read as its start (-0.0003, 0.00025, 0), 0.00039051 away. The
            // item keeps this (A3); the test is here so that it does not move unnoticed.
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0));
            var stub = new GeoLine3(new GeoPoint3(-0.0003, 0.00025, 0), new GeoPoint3(-0.0003, -0.00025, 0));
            double fromStart = Math.Sqrt(0.0003 * 0.0003 + 0.00025 * 0.00025);

            AssertGap(fromStart, Within(Tol, () => line.DistanceTo(stub)), 1E-11);
            AssertGap(fromStart, Within(Tol, () => stub.DistanceTo(line)), 1E-11);
        }

        #endregion
    }
}
