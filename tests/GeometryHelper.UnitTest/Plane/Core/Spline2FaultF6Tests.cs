using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A part cut off a spline shorter than the point tolerance can be flattened; a touch far from the origin is one place; a flat arc
    /// spline that is one circle with an arc lies along it; and a spline and its near copy give the same places whichever is asked and
    /// however the pair is turned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fit through (0, 0), (30, 20), (60, -10), (100, 0) cut at a millionth of its domain gave a part 0.000245 long whose
    /// <c>Flatten</c> threw: its corners all lay within the point tolerance of its start, and a chain merges them into one point.
    /// </para>
    /// <para>
    /// A spline touching the circle spline of radius 100 a million from the origin was given three places: the touch, and two points
    /// 0.002 either side of it, 2.3E-8 off the circle, which read as meetings under the snap of 1E-7 and are on the slope of the distance;
    /// the circle gives one. Ten million out, where the two points are 4E-9 off and the numbers are evaluated to 3.6E-8, it is still two
    /// (parked). Two crossings a few snaps deep stay two, as the exact curves have them.
    /// </para>
    /// <para>
    /// The spline of an arc of radius 849 821 and the arc of that circle moved and grown less than the tolerance were given two
    /// places, the ends of their overlap, where the arcs give none: the circle of so flat a spline cannot be read to a hundredth of the
    /// tolerance, and the pair was cut as two curves. They are one circle to the rule however far the reading may be off.
    /// </para>
    /// <para>
    /// A spline and its copy moved a few ten-thousandths answered by which of the two was asked first, which turning the pair changes:
    /// the overhang of one past the other's end ended the stretch they lie along; a point square from the other's end was a least of the
    /// distance read as to that end, where the distance falls on into the stretch; and a crossing found inside the stretch was lost to a
    /// meeting a few units of rounding nearer, which no stretch keeps. Five of sixty such copies turned by 0.7 of a radian gave a place
    /// more or fewer.
    /// </para>
    /// <para>
    /// Inside a stretch along, a change of sign of the offset by less than a hundredth of the tolerance was given as a crossing where the
    /// stretch swung further elsewhere, and once it was asked to swing that far at each change itself, a cubic that crosses a line three
    /// times with lobes of 7E-6 between was given none, though it goes from 1.1E-4 under the line to 4.4E-4 over it. Small lobes are now
    /// taken with their neighbours, and between two lobes that swing further to either side the curves cross once. A sample of the
    /// offset that was nought exactly hid the change beside it, and two lobes either side read as one side; a change that bisects onto the
    /// jump of the offset at a corner, or onto a crossing so shallow that the curves run along each other to the snap, gave none.
    /// </para>
    /// <para>
    /// And the draw that once found a spline on one line 5.8E-6 too far from an ellipse, fixed before, is kept here as a test of its own.
    /// </para>
    /// </remarks>
    public class Spline2FaultF6Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        [Theory]
        [InlineData(1E-6, 0)]
        [InlineData(1.0 - 1E-6, 1)]
        public void APartCutOffWithinThePointToleranceOfAnEnd_FlattensToTheChainOfItsCorners(double share, int which)
        {
            GeoSpline2 spline = GeoSpline2.Interpolate(new[] { P(0, 0), P(30, 20), P(60, -10), P(100, 0) }, 3, Thousandth);
            GeoSpline2[] parts = spline.Split(share);
            GeoSpline2 part = parts[which];

            Assert.InRange(part.Length, 1E-4, 1E-3);

            GeoPolyline2 chain = part.Flatten(1E-3);
            GeoPoint2 start = part.GetPointAtParameter(0.0);
            GeoPoint2 end = part.GetPointAtParameter(1.0);

            Assert.True(chain.Vertices.Count >= 2);
            Assert.Equal(start, chain.Vertices[0]);
            Assert.Equal(end, chain.Vertices[chain.Vertices.Count - 1]);
            Assert.InRange(Math.Abs(chain.Length - part.Length), 0.0, 1E-9);

            // The other part is flattened as before.
            Assert.True(parts[1 - which].Flatten(1E-3).Vertices.Count > 100);
        }

        [Theory]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void ASplineTouchingACircleSplineFarFromTheOrigin_TouchesItOnce_AsTheCircleIs(double at)
        {
            // A quadratic through (at - 300, at + 100.5), (at, at + 100) and (at + 300, at + 100.5) sits on the top of the circle of radius
            // 100 about (at, at), 2.3E-8 off it 0.002 either side of the touch: a million out the snap is 1E-7.
            var circle = new GeoCircle2(P(at, at), 100);
            GeoSpline2 ring = GeoSpline2.FromCircle(circle);
            GeoSpline2 bow = GeoSpline2.Interpolate(new[] { P(at - 300, at + 100.5), P(at, at + 100), P(at + 300, at + 100.5) }, 2, Thousandth);
            GeoPoint2 top = P(at, at + 100);

            Assert.Single(bow.GetIntersections(circle, Thousandth));
            Assert.InRange(Assert.Single(bow.GetIntersections(ring, Thousandth)).DistanceTo(top), 0.0, 2E-3);
            Assert.InRange(Assert.Single(ring.GetIntersections(bow, Thousandth)).DistanceTo(top), 0.0, 2E-3);
        }

        [Fact]
        public void ALineCuttingACircleSplineOfRadius10000InAChordOf0009_MeetsItTwice()
        {
            // The chord is 1.01E-9 deep, a snap at a size of 10 000: two crossings, as the circle has them.
            double r = 10000.0, half = 0.0045, depth = r - Math.Sqrt(r * r - half * half);
            var circle = new GeoCircle2(P(0, 0), r);
            var line = new GeoLine2(P(-20, r - depth), P(20, r - depth));

            Assert.Equal(2, line.GetIntersections(circle, Thousandth).Length);
            Assert.Equal(2, GeoSpline2.FromCircle(circle).GetIntersections(line, Thousandth).Length);
        }

        [Theory]
        [InlineData(1E5, 0.05, 1E-8)]
        [InlineData(1E6, 0.5, 3E-8)]
        [InlineData(3E6, 0.5, 3E-7)]
        [InlineData(3E6, 5.0, 3E-7)]
        public void ABowThatASegmentSplineCutsAFewSnapsDeepFarOut_IsCrossedTwice(double at, double rise, double depth)
        {
            // The bow is x² rise / 90 000 over its lowest point, and the segment the depth above it cuts it at x = +-300 √(depth / rise).
            GeoSpline2 bow = GeoSpline2.Interpolate(new[] { P(at - 300, at + 100 + rise), P(at, at + 100), P(at + 300, at + 100 + rise) }, 2, Thousandth);
            GeoSpline2 segment = GeoSpline2.FromControlPoints(1, new[] { P(at - 50, at + 100 + depth), P(at + 50, at + 100 + depth) });
            double half = 300.0 * Math.Sqrt(depth / rise);

            GeoPoint2[] places = bow.GetIntersections(segment, Thousandth);

            Assert.Equal(2, places.Length);
            Assert.Equal(2, segment.GetIntersections(bow, Thousandth).Length);
            Assert.Contains(places, p => Math.Abs(p.X - at + half) < 1E-3);
            Assert.Contains(places, p => Math.Abs(p.X - at - half) < 1E-3);
        }

        [Fact]
        public void AnArcSplineAMillionOutAgainstItsOwnFlattening_MeetsItAtEveryCorner()
        {
            // The arc of radius 100 turning 0.1 radians, flattened within 1E-7 into 2 048 chords: every corner is on it, as at the origin.
            GeoSpline2 arc = GeoSpline2.FromArc(new GeoArc2(P(1E6, 1E6), 100, 0.3, 0.4));
            GeoPolyline2 chain = arc.Flatten(1E-7);

            Assert.Equal(2049, chain.Vertices.Count);
            Assert.True(arc.GetIntersections(chain, Thousandth).Length >= chain.Vertices.Count);
        }

        [Fact]
        public void ACubicThatCrossesALineThriceWithLobesUnderAHundredthOfTheTolerance_CrossesItOnce()
        {
            // The cubic's offset from the line is 1.1E-4 under it at the start and 4.4E-4 over it at the end, and crosses it at 20, 40 and 60
            // with lobes of 7E-6 between, under the hundredth of the tolerance a crossing inside a stretch along swings to: once, at the
            // change in the middle, 40, whichever is asked and turned.
            GeoSpline2 line = GeoSpline2.FromControlPoints(3, new[] { P(0, 0), P(33.333333333333336, 0), P(66.66666666666667, 0), P(100, 0) });
            GeoSpline2 cubic = GeoSpline2.FromControlPoints(3, new[]
            {
                P(0, -0.00011040000000000001), P(33.333333333333336, 0.00022693333333333334), P(66.66666666666667, -0.00035573333333333327), P(100, 0.0004415999999999999),
            });

            AssertCrossesAt(line, cubic, 40.0);
        }

        [Fact]
        public void AQuarticThatCrossesALineFourTimesWithASmallMiddleLobe_CrossesItAtTheOuterTwo()
        {
            // The offset is 1E-10 (x - 20)(x - 40)(x - 60)(x - 80): lobes of 3.8E-4, 1.5E-5, 9E-6, 1.5E-5 and 3.8E-4. The middle one is noise,
            // and the two lobes either side of it swing the same way: the crossings at 20 and 80.
            GeoSpline2 line = GeoSpline2.FromControlPoints(4, new[] { P(0, 0), P(25, 0), P(50, 0), P(75, 0), P(100, 0) });
            GeoSpline2 quartic = GeoSpline2.FromControlPoints(4, new[] { P(0, 0.000384), P(25, -0.000616), P(50, 0.00071733333333333333), P(75, -0.000616), P(100, 0.000384) });

            Assert.Equal(9E-6, quartic.DistanceTo(P(50, 0)), 9);
            AssertCrossesAt(line, quartic, 20.0, 80.0);
        }

        [Fact]
        public void APolylineSplineAndItsCopyMovedAlongItsCorners_CrossAtEveryCornerOrOnceAcrossShallowEdges()
        {
            // A zigzag of 45 degrees moved 2.6E-5 along X: its edges are 1.8E-5 off each other, either way in turn, and cross at each corner.
            // With two shallow edges between, 8.7E-7 off each other either way, the two steep ones either side swing opposite ways: once.
            var move = new GeoVector2(2.6E-5, 0);
            GeoSpline2 zigzag = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(10, 10), P(20, 0), P(30, 10), P(40, 0) });
            GeoSpline2 shallow = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(10, 10), P(40, 9), P(70, 10), P(80, 0) });

            AssertCrossesAt(zigzag, zigzag.Translate(move), 10.0, 20.0, 30.0);
            AssertCrossesAt(shallow, shallow.Translate(move), 40.0);
        }

        [Theory]
        [InlineData(62.5)]
        [InlineData(60.9375)]
        public void ACopyCrossingALineAtASampleOfItsOffset_CrossesItOnce(double root)
        {
            // A polyline 2.3E-9 (x - 20)(x - 40)(x - root) off the line, with a corner at the root, where the offset sampled in 64 or 65
            // steps along the stretch is nought exactly: lobes of 1.15E-4 under and 4.1E-4 over the line either side of two small ones.
            var corners = Enumerable.Range(0, 11).Select(i => 10.0 * i).Concat(new[] { root }).Distinct().OrderBy(x => x);
            GeoSpline2 copy = GeoSpline2.FromControlPoints(1, corners.Select(x => P(x, 2.3E-9 * (x - 20) * (x - 40) * (x - root))).ToArray());
            GeoSpline2 line = GeoSpline2.FromControlPoints(1, new[] { P(0, 0), P(100, 0) });

            AssertCrossesOnce(line, copy);
        }

        [Fact]
        public void TheCubicThatCrossesALineThriceAMillionOut_CrossesItOnce()
        {
            // A million out the crossing in the middle, at 40, is so shallow that the two run along each other within the snap, 1E-7, for
            // 0.1 either side of it, which is no crossing; the next change is.
            double o = 1E6;
            GeoSpline2 line = GeoSpline2.FromControlPoints(3, new[] { P(o, o), P(o + 33.333333333333336, o), P(o + 66.66666666666667, o), P(o + 100, o) });
            GeoSpline2 cubic = GeoSpline2.FromControlPoints(3, new[]
            {
                P(o, o - 0.00011040000000000001), P(o + 33.333333333333336, o + 0.00022693333333333334), P(o + 66.66666666666667, o - 0.00035573333333333327), P(o + 100, o + 0.0004415999999999999),
            });

            AssertCrossesOnce(line, cubic);
        }

        [Fact]
        public void APolylineSplineAndItsCopyWhoseOneChangeIsAJumpAtACorner_CrossOnce()
        {
            // The copy moved 2.6E-5 is 2.2E-5 to one side of the second edge and 1.6E-5 to the other of the rest: the change of the offset
            // between them is at the corner (13.47, 27.67), where it jumps across nought, and the crossing is found beside it.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(1, new[]
            {
                P(-9.69378137015448, -6.831675226256099), P(0.5228475716537107, -25.33829304172578), P(13.473887910821423, 27.670696320790192),
                P(14.352296164423365, 17.7753015271273), P(18.023392282437246, -7.1473311619587845), P(21.68079689689018, -11.745057488672927),
                P(23.61878139601032, -27.34590659725755),
            });

            AssertCrossesOnce(spline, spline.Translate(new GeoVector2(1.81077685984494E-05, -1.865767178361249E-05)));
        }

        [Theory]
        [InlineData(1E6)]
        [InlineData(-1E6)]
        public void TwoSegmentSplinesAMillionOutCrossingAtASlopeOf25E8_CrossOnceAtTheirCrossing(double o)
        {
            // They run along each other within the snap for 0.4 either side of the crossing at 50, which bisects to no crossing; Newton's
            // method from one side settles on it, and from the other stalls 0.35 off it, which was given in its place.
            GeoSpline2 first = GeoSpline2.FromControlPoints(1, new[] { P(o, o), P(o + 100, o) });
            GeoSpline2 second = GeoSpline2.FromControlPoints(1, new[] { P(o, o - 1.25E-5), P(o + 100, o + 1.25E-5) });
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), 0.7);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -0.7);

            foreach (GeoPoint2[] places in new[]
            {
                first.GetIntersections(second, Thousandth),
                second.GetIntersections(first, Thousandth),
                first.TransformBy(turn).GetIntersections(second.TransformBy(turn), Thousandth).Select(p => p.TransformBy(back)).ToArray(),
            })
            {
                Assert.InRange(Math.Abs(Assert.Single(places).X - o - 50.0), 0.0, 1E-3);
            }
        }

        [Fact]
        public void AnArcSplineAMillionOutAgainstItsFlatteningIntoSixtyFiveThousandChords_IsAnsweredInSeconds()
        {
            // Each meeting that is one only to the snap is checked against the meetings to the digits round it, not against all 131 073.
            GeoSpline2 arc = GeoSpline2.FromArc(new GeoArc2(P(1E6, 1E6), 100, 0.3, 2.3));
            GeoPolyline2 chain = arc.Flatten(2E-8);
            var watch = Stopwatch.StartNew();

            GeoPoint2[] places = arc.GetIntersections(chain, Thousandth);

            Assert.Equal(65537, chain.Vertices.Count);
            Assert.True(places.Length >= chain.Vertices.Count);
            Assert.InRange(watch.Elapsed.TotalSeconds, 0.0, 6.0);
        }

        /// <summary>
        /// Two curves cross once, asked either way and turned by 0.7 of a radian, at a point of both.
        /// </summary>
        private static void AssertCrossesOnce(GeoSpline2 first, GeoSpline2 second)
        {
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), 0.7);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -0.7);

            foreach (GeoPoint2[] places in new[]
            {
                first.GetIntersections(second, Thousandth),
                second.GetIntersections(first, Thousandth),
                first.TransformBy(turn).GetIntersections(second.TransformBy(turn), Thousandth).Select(p => p.TransformBy(back)).ToArray(),
            })
            {
                GeoPoint2 place = Assert.Single(places);
                Assert.InRange(first.DistanceTo(place), 0.0, 1E-6);
                Assert.InRange(second.DistanceTo(place), 0.0, 1E-6);
            }
        }

        /// <summary>
        /// Two curves along X cross at these places, asked either way and turned by 0.7 of a radian, and collide.
        /// </summary>
        private static void AssertCrossesAt(GeoSpline2 first, GeoSpline2 second, params double[] xs)
        {
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), 0.7);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -0.7);

            foreach (GeoPoint2[] places in new[]
            {
                first.GetIntersections(second, Thousandth),
                second.GetIntersections(first, Thousandth),
                first.TransformBy(turn).GetIntersections(second.TransformBy(turn), Thousandth).Select(p => p.TransformBy(back)).ToArray(),
            })
            {
                Assert.Equal(xs.Length, places.Length);

                foreach (double x in xs)
                {
                    Assert.Contains(places, p => Math.Abs(p.X - x) < 1E-3);
                }
            }

            Assert.True(first.CollidesWith(second, Thousandth));
        }

        [Fact]
        public void AFlatArcSplineAndAnArcOfItsCircleMovedLessThanTheTolerance_MeetNowhere_AsTheArcsDo()
        {
            var centre = P(979.534295378036, 364.2462377269968);
            double radius = 849820.7326302916;
            double move = 0.000114591323311716;
            double angle = 5.0135053818051265;
            var arc = new GeoArc2(centre, radius, 1.7763877141901236, 1.7763877141901236 - 0.007015186556453309, true);
            var other = new GeoArc2(P(centre.X + move * Math.Cos(angle), centre.Y + move * Math.Sin(angle)), radius - 0.00019831970813605917, 1.7714472582854681, 1.7714472582854681 - 0.013354062483875022, true);
            GeoSpline2 spline = GeoSpline2.FromArc(arc);

            Assert.Empty(arc.GetIntersections(other, Thousandth));
            Assert.Empty(spline.GetIntersections(other, Thousandth));
            Assert.Empty(other.GetIntersections(spline, Thousandth));
            Assert.Empty(spline.GetIntersections(new GeoCircle2(other.Center, other.Radius), Thousandth));
            Assert.True(spline.CollidesWith(other, Thousandth));
        }

        /// <summary>
        /// A spline and its copy moved, asked either way and turned by 0.7 of a radian: as many places each way, each of them a point
        /// of both curves, where they cross.
        /// </summary>
        private static void AssertTheSameWhicheverWay(GeoSpline2 spline, GeoVector2 move, int expected)
        {
            GeoSpline2 copy = spline.Translate(move);
            GeoTransform2 turn = GeoTransform2.Rotation(P(0, 0), 0.7);
            GeoTransform2 back = GeoTransform2.Rotation(P(0, 0), -0.7);

            GeoPoint2[] places = spline.GetIntersections(copy, Thousandth);
            GeoPoint2[] asked = copy.GetIntersections(spline, Thousandth);
            GeoPoint2[] turned = spline.TransformBy(turn).GetIntersections(copy.TransformBy(turn), Thousandth).Select(p => p.TransformBy(back)).ToArray();

            Assert.Equal(expected, places.Length);
            Assert.Equal(expected, asked.Length);
            Assert.Equal(expected, turned.Length);

            foreach (GeoPoint2 place in places.Concat(turned))
            {
                Assert.InRange(spline.DistanceTo(place), 0.0, 1E-9);
                Assert.InRange(copy.DistanceTo(place), 0.0, 1E-9);
                Assert.Contains(places, p => p.DistanceTo(place) < 1E-6);
            }
        }

        [Fact]
        public void ACopyThatOverhangsTheEndOfASpline_GivesTheSamePlacesWhicheverIsAsked()
        {
            // The copy ends 0.0007 past the spline's end; asked from the copy the stretch along them ended short of it, and the end of
            // the spline, 1.5E-5 from the copy, was given as a place.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(2, new[]
            {
                P(11.145065157276147, -23.358664202205215), P(-25.06507479355907, 10.221748678116942), P(14.831623567655509, 12.513038191251942),
                P(-21.560436120052096, -7.946054617849204), P(-28.635031403338086, -22.31766032628606),
            });

            AssertTheSameWhicheverWay(spline, new GeoVector2(-0.00031086647951441887, -0.0006647301861314643), 2);
        }

        [Fact]
        public void ACopyWhoseStartIsSquareFromTheSplinesStart_GivesNoPlaceThere()
        {
            // The point of the copy square from the spline's start is 0.0005 from it, and the distance falls from there on to the crossing
            // 0.11 along: no least, but read as the distance to the spline's start it was one.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(3, new[]
            {
                P(-12.225403293140886, -9.97391013427354), P(-16.287349204666608, 5.912460459355479), P(-7.5055616616763015, -19.356271070128432),
                P(5.196753467990433, -10.297554303099192), P(14.028597541166747, -20.991967204488798), P(9.386412705940387, 26.254452437280886),
            });

            AssertTheSameWhicheverWay(spline, new GeoVector2(0.0003758292984923857, 0.00056110800854353), 2);
        }

        [Fact]
        public void ACrossingInsideTheStretchACopyLiesAlong_IsGivenWhicheverIsAsked()
        {
            // The crossing at (-8.973, 12.223) inside a stretch along was found by the offset changing sign and lost to a meeting found
            // nearer by a few units of rounding, which is no crossing; so was the one at (-8.201, -13.571) turned.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(4, new[]
            {
                P(-29.208839228008333, -29.239437980223187), P(10.574688827886565, -1.5779355315388799), P(-10.461179781919896, 23.843027564623867),
                P(-16.57373824462934, 2.6700865629455506), P(-26.05396927150617, -21.536769984074294), P(5.075757231132945, -11.3943708415117),
                P(28.706179903217674, 4.130605614804943),
            });

            AssertTheSameWhicheverWay(spline, new GeoVector2(0.0005506786728196616, 9.642891641517413E-05), 3);
        }

        [Fact]
        public void ASplineOnOneLineAgainstAnEllipseSevenHundredAcross_IsAsFarAsItIs_ToABillionthOfTheSize()
        {
            // The draw of a run with four times the draws that found the distance 5.8E-6 too great: a spline of degree 4 whose control
            // points lie on one line to the four places they are given to, going back and forth along it, against an ellipse 709 by 99. The
            // hair of the nearest approach was then a hundred-millionth of the size and is a billionth now. The distance, 50.21894075548782,
            // is the least of the ellipse's distance to 400 001 points of the spline, brought down by golden sections.
            double[] xy =
            {
                668.5171, 171.218, 716.7793, 87.3742, 965.1187, -344.0553, 1038.2734, -471.1438, 723.1073, 76.3807,
                839.9177, -126.5489, 593.8112, 301.0013, 999.1978, -403.2595, 1130.5672, -631.4818, 836.6781, -120.9209,
            };
            double[] knots =
            {
                -3.554, -3.554, -3.554, -3.554, -3.554, -3.2816503765237317, -3.0570896596007473, -2.5295528850853417, -2.323912166608231,
                -1.8721801269955598, -1.5179999999999998, -1.5179999999999998, -1.5179999999999998, -1.5179999999999998, -1.5179999999999998,
            };
            var spline = new GeoSpline2(4, Enumerable.Range(0, 10).Select(i => P(xy[2 * i], xy[2 * i + 1])), knots);
            var ellipse = new GeoEllipse2(P(1165.1204377918925, 95.5979710904034), new GeoVector2(-0.7063017126460878, -0.7079109341670061), 354.51068914118105, 49.37420340985297);

            double distance = spline.DistanceTo(ellipse, Thousandth);

            Assert.InRange(distance, 50.21894075548782 - 1E-9, 50.21894075548782 + 1E-9 * 709);
            Assert.Equal(distance, ellipse.DistanceTo(spline, Thousandth));
        }
    }
}
