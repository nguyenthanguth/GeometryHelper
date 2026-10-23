using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Random splines in space against random shapes of each of the eighteen kinds, each answer of the library checked against an
    /// oracle that shares no code with it (<see cref="Spline3MeetOracle"/>): the spline sampled densely over every span, the least of its
    /// distance to each piece of the shape found by golden section round each least of the samples. Both sides of every pair are asked
    /// and must agree to the bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A case draws a spline (degree one to five, knots with repeats and thin spans, weights for one in two) and shapes about it on a
    /// seeded generator, so a failure names its draw and repeats. A pair whose nearest approach is within twice the point tolerance of
    /// the tolerance, or that holds a stretch where the spline stays within the tolerance of the shape and level, is left out, since the
    /// library reads a touch there and a run along, and the oracle does not; those are the cases of
    /// <see cref="Spline3AgainstShapesTests"/> and <c>Spline3HardCasesTests</c>, with the numbers worked out by hand. The skipped
    /// are counted in the test output.
    /// </para>
    /// <para>
    /// In space a spline meets a curve only by coincidence, so a random draw has places only against shapes that are surfaces; the
    /// pairs built to touch a spline's tangent within a few ten-thousandths of a millimetre, or to pass through one of its points,
    /// have them against every kind.
    /// </para>
    /// <para>
    /// The draws per test are those below, or what the environment variable SPLINE_FUZZ_DRAWS says for a longer run by hand.
    /// </para>
    /// </remarks>
    public class Spline3MeetFuzzTests
    {
        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;
        private int _extra;

        public Spline3MeetFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        internal static GeoSpline3 Make(SplineOracle o)
        {
            var points = new GeoPoint3[o.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint3(o.Points[3 * i], o.Points[3 * i + 1], o.Points[3 * i + 2]);
            }

            return new GeoSpline3(o.Degree, points, o.Knots, o.Weights.Any(w => w != 1.0) ? o.Weights : null);
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static bool SameBits(GeoPoint3 p, GeoPoint3 q) => Bits(p.X) == Bits(q.X) && Bits(p.Y) == Bits(q.Y) && Bits(p.Z) == Bits(q.Z);

        private static string Name(SplineOracle o, string kind, int draw) => kind + ", draw " + draw + ": degree " + o.Degree + ", " + o.Count + " points " + string.Join(" ", o.Points.Select(v => v.ToString("R"))) + ", knots " + string.Join(" ", o.Knots.Select(v => v.ToString("R"))) + ", weights " + string.Join(" ", o.Weights.Select(v => v.ToString("R")));

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in Spline3MeetShapes.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        internal static double Reach(SplineOracle o)
        {
            double[] box = BoxOf(o);

            return Math.Max(1.0, Math.Max(box[3] - box[0], Math.Max(box[4] - box[1], box[5] - box[2])));
        }

        internal static double[] BoxOf(SplineOracle o)
        {
            double[] box = { double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue };

            for (int i = 0; i < o.Count; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    box[k] = Math.Min(box[k], o.Points[3 * i + k]);
                    box[3 + k] = Math.Max(box[3 + k], o.Points[3 * i + k]);
                }
            }

            return box;
        }

        private static string Shown(Meet3Shape shape) => " (shape " + string.Join("; ", shape.Parts) + ")";

        /// <summary>
        /// Asks the library and the oracle about one spline and one shape and holds the library to the oracle; gives false for a pair left out.
        /// </summary>
        private bool Check(string name, GeoSpline3 spline, SplineOracle o, Meet3Shape shape, ref double worst)
        {
            double size = Reach(o);
            double coordinates = Math.Max(1.0, o.Points.Max(Math.Abs));
            double slack = 1E-9 * size + 64.0 * 2.220446049250313E-16 * coordinates * 16.0;
            Spline3MeetOracle.Answer expected = Spline3MeetOracle.Ask(o, shape, 1E-3);

            // A pair whose collision, or whose count of places, turns on a few rounding errors, a least of the distance at the point
            // tolerance, is a matter for the hand cases. A near touch within the tolerance is not: it is a place, and counted.
            if (Math.Abs(expected.Distance - 1E-3) <= 4.0 * slack || expected.Leasts.Any(v => Math.Abs(v - 1E-3) <= 4.0 * slack))
            {
                return false;
            }

            // A spline that lies along the shape, within the tolerance and level, for a thousand times the tolerance or a quarter of the
            // size: no place is given there, which is the hand cases' to show.
            if (expected.LongestLevel >= Math.Min(1000.0 * 1E-3, 0.25 * size))
            {
                return false;
            }

            string label = name + " against " + shape.Kind;
            double fromSpline = Distance(spline, shape.Shape, Strict);
            double fromShape = Distance(shape.Shape, spline, Strict);

            Assert.True(Bits(fromSpline) == Bits(fromShape), label + ": the distance is " + fromSpline.ToString("R") + " from the spline and " + fromShape.ToString("R") + " from the shape");
            double error = Math.Abs(fromSpline - expected.Distance);
            worst = Math.Max(worst, error / size);
            Assert.True(error <= slack, label + ": distance " + fromSpline.ToString("R") + ", the oracle says " + expected.Distance.ToString("R") + Shown(shape));

            bool collides = expected.Distance <= 1E-3;
            Assert.Equal(collides, Collides(spline, shape.Shape, Strict));
            Assert.Equal(collides, Collides(shape.Shape, spline, Strict));

            GeoPoint3[] crossings = Crossings(spline, shape.Shape, Strict);
            GeoPoint3[] back = Crossings(shape.Shape, spline, Strict);

            Assert.True(crossings.Length == back.Length && crossings.Zip(back, SameBits).All(b => b), label + ": the crossings differ from the two sides");
            string shown = Shown(shape);

            // The places are the oracle's: every place the oracle found is given, within two thousandths of where the oracle puts it, and
            // none is given that the oracle did not find; every place lies on the spline and within the tolerance of the shape, and no
            // two are within the point tolerance of each other.
            Assert.True(
                crossings.Length == expected.Places.Count,
                label + ": " + crossings.Length + " places " + string.Join(" ", crossings.Select(c => c.ToString())) + ", the oracle's " + expected.Places.Count + ": " + string.Join(" ", expected.Places.Select(c => c[0].ToString("R") + "," + c[1].ToString("R") + "," + c[2].ToString("R"))) + shown);

            foreach (double[] at in expected.Places)
            {
                Assert.True(
                    crossings.Any(c => Math.Sqrt((at[0] - c.X) * (at[0] - c.X) + (at[1] - c.Y) * (at[1] - c.Y) + (at[2] - c.Z) * (at[2] - c.Z)) <= 2E-3 + slack),
                    label + ": the place (" + at[0].ToString("R") + ", " + at[1].ToString("R") + ", " + at[2].ToString("R") + ") the oracle found is not among " + string.Join(" ", crossings.Select(c => c.ToString())) + shown);
            }

            for (int i = 0; i < crossings.Length; i++)
            {
                double onSpline = spline.DistanceTo(crossings[i]);
                double onShape = shape.Crossing.Min(part => part.Gap(crossings[i].X, crossings[i].Y, crossings[i].Z));
                // The nearest point of a spline whose weights are a million to one is good to a few billionths of its size, so the point of the
                // spline a place is, asked for its distance from the spline, is that far off it.
                Assert.True(onSpline <= 10.0 * slack && onShape <= 1E-3 + slack, label + ": the place " + crossings[i] + " is " + onSpline + " off the spline and " + onShape + " off the shape" + shown);

                for (int j = 0; j < i; j++)
                {
                    Assert.True(crossings[i].DistanceTo(crossings[j]) > 1E-3, label + ": the places " + crossings[j] + " and " + crossings[i] + " are within the point tolerance");
                }
            }

            _extra += crossings.Length;

            bool tried = Try(spline, shape.Shape, out GeoPoint3[] points, Strict);
            Assert.Equal(crossings.Length > 0, tried);
            Assert.Equal(crossings.Length, points.Length);

            // The shortest segment runs from the spline to the surface or the outline: of no length where they meet, otherwise as long as
            // the gap, which for a body that holds the spline is the gap out to the skin.
            GeoLine3 leaving = Shortest(spline, shape.Shape, Strict);
            GeoLine3 landing = Shortest(shape.Shape, spline, Strict);

            Assert.True(Math.Abs(leaving.Length - expected.Boundary) <= slack, label + ": the shortest segment is " + leaving.Length.ToString("R") + " long, the oracle says " + expected.Boundary.ToString("R") + shown);
            Assert.True(SameBits(leaving.StartPoint, landing.EndPoint) && SameBits(leaving.EndPoint, landing.StartPoint), label + ": the shortest segments are not the same turned round");
            return true;
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ASplineAndAShapeInSpace_AreTheOraclesToRounding_AndFromEitherSideTheSame(string kind)
        {
            var random = new Random(72000 + Array.IndexOf(Spline3MeetShapes.Kinds, kind));
            double worst = 0.0;
            int checkedCount = 0;
            int left = 0;
            int crossed = 0;

            for (int draw = 0; draw < 15 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, 3, maxDegree: 5, thinSpans: draw % 4 == 0);
                GeoSpline3 spline = Make(o);
                double[] box = BoxOf(o);

                for (int k = 0; k < 3; k++)
                {
                    Meet3Shape shape = Spline3MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[3]), 0.5 * (box[1] + box[4]), 0.5 * (box[2] + box[5]), Reach(o));

                    if (Check(Name(o, kind, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                        crossed += Crossings(spline, shape.Shape, Strict).Length > 0 ? 1 : 0;
                    }
                    else
                    {
                        left++;
                    }
                }
            }

            _output.WriteLine(kind + ": " + checkedCount + " pairs held to the oracle (" + crossed + " with a place, " + left + " left out as at the tolerance); worst distance error " + worst.ToString("0.0E+0") + " of the size of the spline; " + _extra + " places in all, each the oracle's too");
            Assert.True(checkedCount > 15, "too few pairs checked: " + checkedCount);
        }

        /// <summary>
        /// A shape that touches a spline at a point of it, a few ten-thousandths of a millimetre off it either way, along its tangent or
        /// through it: a line, a plane, a circle or an ellipse in a plane that holds the tangent, a ball. The places such a pair has are
        /// a near touch within the point tolerance, one, or crossings, and what lies between two crossings, a greatest, is no place.
        /// </summary>
        private static Meet3Shape TangentShape(Random random, SplineOracle o, string kind, double offset)
        {
            double u = o.Start + (o.End - o.Start) * (0.1 + 0.8 * random.NextDouble());
            double step = 1E-6 * (o.End - o.Start);
            double[] at = o.FastPoint(u);
            double[] before = o.FastPoint(u - step);
            double[] after = o.FastPoint(u + step);
            double[] tangent = Spline3MeetShapes.Unit(new[] { after[0] - before[0], after[1] - before[1], after[2] - before[2] });

            // A direction square to the tangent, drawn at random: the offset goes this way, and so does a ball's or a circle's centre.
            double[] any = Spline3MeetShapes.Direction(random);
            double along = Spline3MeetShapes.Dot(any, tangent);
            double[] m = Spline3MeetShapes.Unit(new[] { any[0] - along * tangent[0], any[1] - along * tangent[1], any[2] - along * tangent[2] });
            double[] w = Spline3MeetShapes.Cross(tangent, m);
            double reach = Reach(o);
            double[] q = { at[0] + m[0] * offset, at[1] + m[1] * offset, at[2] + m[2] * offset };
            double size = reach * Math.Max(0.05, random.NextDouble() * 0.5);
            double side = random.Next(2) == 0 ? 1.0 : -1.0;
            var shape = new Meet3Shape { Kind = kind };

            switch (kind)
            {
                case "segment":
                {
                    var a = new GeoPoint3(q[0] - tangent[0] * size, q[1] - tangent[1] * size, q[2] - tangent[2] * size);
                    var b = new GeoPoint3(q[0] + tangent[0] * size, q[1] + tangent[1] * size, q[2] + tangent[2] * size);
                    shape.Shape = new GeoLine3(a, b);
                    shape.Parts.Add(Meet3Segment.Between(a, b));
                    break;
                }

                case "xline":
                    shape.Shape = new GeoXLine3(new GeoPoint3(q[0], q[1], q[2]), new GeoVector3(tangent[0], tangent[1], tangent[2]));
                    shape.Parts.Add(new Meet3Segment(q[0], q[1], q[2], tangent[0], tangent[1], tangent[2], 0.0, false, true));
                    break;

                case "ray":
                {
                    // Starts a way behind the touch and runs along the tangent through it.
                    double[] from = { q[0] - tangent[0] * size * 0.5, q[1] - tangent[1] * size * 0.5, q[2] - tangent[2] * size * 0.5 };
                    shape.Shape = new GeoRay3(new GeoPoint3(from[0], from[1], from[2]), new GeoVector3(tangent[0], tangent[1], tangent[2]));
                    shape.Parts.Add(new Meet3Segment(from[0], from[1], from[2], tangent[0], tangent[1], tangent[2], 0.0, true, false));
                    break;
                }

                case "polyline":
                {
                    // The tangent segment through the touch, then a leg turned square to the curve's side at its far end.
                    var a = new GeoPoint3(q[0] - tangent[0] * size, q[1] - tangent[1] * size, q[2] - tangent[2] * size);
                    var b = new GeoPoint3(q[0] + tangent[0] * size, q[1] + tangent[1] * size, q[2] + tangent[2] * size);
                    var c = new GeoPoint3(b.X + w[0] * size, b.Y + w[1] * size, b.Z + w[2] * size);
                    shape.Shape = new GeoPolyline3(a, b, c);
                    shape.Parts.Add(Meet3Segment.Between(a, b));
                    shape.Parts.Add(Meet3Segment.Between(b, c));
                    break;
                }

                case "plane":
                    // A plane square to m holds the tangent and is off the curve by the offset at the touch.
                    shape.Shape = new GeoPlane3(new GeoPoint3(q[0], q[1], q[2]), new GeoVector3(m[0], m[1], m[2]));
                    shape.Parts.Add(new Meet3Plane(q[0], q[1], q[2], m[0], m[1], m[2]));
                    break;

                case "sphere":
                {
                    double radius = size;
                    double[] c = { q[0] + m[0] * side * radius, q[1] + m[1] * side * radius, q[2] + m[2] * side * radius };
                    shape.Shape = new GeoSphere3(new GeoPoint3(c[0], c[1], c[2]), radius);
                    shape.Parts.Add(new Meet3Sphere(c[0], c[1], c[2], radius));
                    shape.Inside = (x, y, z) => Math.Sqrt((x - c[0]) * (x - c[0]) + (y - c[1]) * (y - c[1]) + (z - c[2]) * (z - c[2])) <= radius;
                    break;
                }

                default:
                {
                    // A circle or an ellipse in the plane of the tangent and m, passing through q with the tangent: its centre is size along m.
                    double b = kind == "circle" ? size : size * (0.2 + 0.8 * random.NextDouble());
                    double[] c = { q[0] + m[0] * side * b, q[1] + m[1] * side * b, q[2] + m[2] * side * b };
                    var disc = new Meet3Ellipse(c, tangent, m, size, b, true);
                    var rim = new Meet3Ellipse(c, tangent, m, size, b, false);
                    shape.Shape = kind == "circle"
                        ? (object)new GeoCircle3(new GeoPoint3(c[0], c[1], c[2]), new GeoVector3(w[0], w[1], w[2]), size)
                        : new GeoEllipse3(new GeoPoint3(c[0], c[1], c[2]), new GeoVector3(w[0], w[1], w[2]), new GeoVector3(tangent[0], tangent[1], tangent[2]), size, b);
                    shape.Parts.Add(disc);
                    shape.Parts.Add(rim);
                    shape.Rims = new List<Meet3Part> { rim };
                    break;
                }
            }

            return shape;
        }

        [Theory]
        [InlineData("segment")]
        [InlineData("xline")]
        [InlineData("ray")]
        [InlineData("polyline")]
        [InlineData("plane")]
        [InlineData("sphere")]
        [InlineData("circle")]
        [InlineData("ellipse")]
        public void ASplineAndAShapeTouchingItsTangentWithinTheTolerance_HaveTheOraclesPlacesAndNoOthers(string kind)
        {
            var random = new Random(75000 + kind.Length);
            double[] offsets = { 0.0, 1E-4, -1E-4, 4E-4, -4E-4, 9E-4, -9E-4, 1.3E-3, -1.3E-3, 3E-3, -3E-3 };
            double worst = 0.0;
            int checkedCount = 0;
            int touched = 0;

            for (int draw = 0; draw < 12 * Multiplier; draw++)
            {
                SplineOracle o;

                do
                {
                    o = SplineGen.Draw(random, 3, maxDegree: 5);
                }
                while (o.Degree < 2);

                GeoSpline3 spline = Make(o);

                foreach (double offset in offsets)
                {
                    Meet3Shape shape = TangentShape(random, o, kind, offset);

                    if (Check(Name(o, kind + " at " + offset, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                        touched += Crossings(spline, shape.Shape, Strict).Length > 0 ? 1 : 0;
                    }
                }
            }

            _output.WriteLine(kind + ": " + checkedCount + " pairs at the tolerance held to the oracle's places (" + touched + " with a place), " + _extra + " places in all");
            Assert.True(checkedCount > 50, "too few pairs checked: " + checkedCount);
            Assert.True(touched > 30, "too few pairs with a place: " + touched);
        }

        /// <summary>
        /// Lays a spline of the plane in the plane of space square to an axis, 0, 1 or 2 for X, Y or Z, through the origin: its two numbers
        /// are the two other coordinates, in order.
        /// </summary>
        private static GeoSpline3 Lay(GeoSpline2 flat, int axis)
        {
            GeoPoint3[] points = flat.ControlPoints.Select(p => axis == 2 ? new GeoPoint3(p.X, p.Y, 0.0) : axis == 0 ? new GeoPoint3(0.0, p.X, p.Y) : new GeoPoint3(p.X, 0.0, p.Y)).ToArray();

            return new GeoSpline3(flat.Degree, points, flat.Knots.ToArray(), flat.IsRational ? flat.Weights.ToArray() : null);
        }

        private static GeoPoint3 Lay(GeoPoint2 p, int axis) => axis == 2 ? new GeoPoint3(p.X, p.Y, 0.0) : axis == 0 ? new GeoPoint3(0.0, p.X, p.Y) : new GeoPoint3(p.X, 0.0, p.Y);

        [Fact]
        public void ASplineAndItsOwnCopyMovedAFewThousandths_AreNoFartherApartThanTheMove_AndFromEitherSideTheSame()
        {
            var random = new Random(76000);
            double[] shifts = { 1E-4, 5E-4, 9E-4, 1.5E-3, 2E-3 };
            int asked = 0;
            int twins = 0;
            int crossings = 0;

            for (int draw = 0; draw < 8 * Multiplier; draw++)
            {
                double shift = shifts[draw % shifts.Length];

                // A spline of the plane and its copy moved along the plane, laid in a plane of space square to an axis: two curves in one plane
                // are worked out in it, and the answers are the plane's to the bit, the places where the moved copy crosses the spline.
                {
                    SplineOracle drawn = SplineGen.Draw(random, 2, maxDegree: 5);
                    GeoSpline2 twin = Spline2MeetFuzzTests.Make(drawn);
                    double angle = 2.0 * Math.PI * random.NextDouble();
                    GeoSpline2 twinCopy = twin.Translate(new GeoVector2(shift * Math.Cos(angle), shift * Math.Sin(angle)));
                    int axis = draw % 3;
                    GeoSpline3 laid = Lay(twin, axis);
                    GeoSpline3 laidCopy = Lay(twinCopy, axis);
                    string label = Name(drawn, "laid square to axis " + axis + ", copy moved " + shift.ToString("R") + " along it", draw);

                    GeoPoint2[] inPlane = twin.GetIntersections(twinCopy, Strict);
                    GeoPoint3[] inSpace = laid.GetIntersections(laidCopy, Strict);

                    Assert.True(inPlane.Length == inSpace.Length, label + ": " + inSpace.Length + " places in space and " + inPlane.Length + " in the plane");
                    Assert.True(inPlane.Select(p => Lay(p, axis)).Zip(inSpace, SameBits).All(b => b), label + ": the places are not the plane's");
                    Assert.True(Bits(twin.DistanceTo(twinCopy, Strict)) == Bits(laid.DistanceTo(laidCopy, Strict)), label + ": the distance is not the plane's");
                    twins++;
                }

                // A spline of space and its copy moved any way, out of any plane it has.
                SplineOracle o = SplineGen.Draw(random, 3, maxDegree: 5);
                GeoSpline3 spline = Make(o);
                double size = Reach(o);
                double slack = 1E-9 * size + 64.0 * 2.220446049250313E-16 * Math.Max(1.0, o.Points.Max(Math.Abs)) * 16.0;

                {
                    double[] d = Spline3MeetShapes.Direction(random);
                    GeoSpline3 copy = spline + new GeoVector3(d[0] * shift, d[1] * shift, d[2] * shift);
                    string label = Name(o, "copy moved " + shift.ToString("R"), draw);
                    double there = spline.DistanceTo(copy, Strict);
                    double back = copy.DistanceTo(spline, Strict);

                    Assert.True(Bits(there) == Bits(back), label + ": the distance is " + there.ToString("R") + " one way and " + back.ToString("R") + " the other");
                    Assert.True(there <= shift + slack, label + ": the distance " + there.ToString("R") + " is more than the move");
                    Assert.Equal(there <= 1E-3, spline.CollidesWith(copy, Strict));
                    Assert.Equal(there <= 1E-3, copy.CollidesWith(spline, Strict));

                    GeoPoint3[] places = spline.GetIntersections(copy, Strict);
                    GeoPoint3[] places2 = copy.GetIntersections(spline, Strict);

                    Assert.True(places.Length == places2.Length && places.Zip(places2, SameBits).All(b => b), label + ": the places differ from the two sides");

                    for (int i = 0; i < places.Length; i++)
                    {
                        // A place of two splines lies on the one that comes first, and within the tolerance of the other.
                        double toSpline = spline.DistanceTo(places[i]);
                        double toCopy = copy.DistanceTo(places[i]);

                        Assert.True(Math.Min(toSpline, toCopy) <= 10.0 * slack && Math.Max(toSpline, toCopy) <= 1E-3 + 10.0 * slack, label + ": the place " + places[i] + " is " + spline.DistanceTo(places[i]).ToString("R") + " off the spline and " + copy.DistanceTo(places[i]).ToString("R") + " off the copy, slack " + slack.ToString("R"));

                        for (int j = 0; j < i; j++)
                        {
                            Assert.True(places[i].DistanceTo(places[j]) > 1E-3, label + ": two places within the point tolerance");
                        }
                    }

                    // Moved by less than the tolerance, out of the spline's plane where it has one, the copy lies along it all the way, and the
                    // places are where the two would cross if brought into one plane. The oracle, on a spline whose tangent turns smoothly (of
                    // degree three or more with no knot repeated inside), finds each least of the distance and the copy's side across the spline
                    // either way from it: every least whose side is clear two hundredths of the tolerance out either way, the one side and the
                    // other, before the distance comes nearer, is a place (the library reads a hundredth, at steps that double, so one clear only
                    // past its last step before an end is not held to it); and every place is a least where the side changes, or an end.
                    bool smooth = o.Degree >= 3 && o.Knots.Skip(o.Degree + 1).Take(o.Knots.Length - 2 * o.Degree - 2).Distinct().Count() == o.Knots.Length - 2 * o.Degree - 2;

                    if (shift < 1E-3 && smooth && !GeometryHelper.Internal.Nurbs.NurbsCurve.InOnePlane(spline.Curve, copy.Curve, out _))
                    {
                        WouldCrossings(spline, new GeoVector3(d[0] * shift, d[1] * shift, d[2] * shift), 1E-3, out List<double> must, out List<double> may);

                        // A least with no curvature is as near for a stretch, and the place may be anywhere on it: knots are compared, to a
                        // thousandth of the domain.
                        double near = 1E-3 * (spline.DomainEnd - spline.DomainStart);
                        double[] knots = places.Select(p => spline.GetKnotAtPoint(p)).ToArray();
                        string where = "; moved along (" + d[0].ToString("R") + ", " + d[1].ToString("R") + ", " + d[2].ToString("R") + "); places at knots " + string.Join(" ", places.Select(p => spline.GetKnotAtPoint(p).ToString("R")));

                        foreach (double crossing in must)
                        {
                            Assert.True(knots.Any(k => Math.Abs(k - crossing) <= near), label + ": the oracle's would-be crossing at knot " + crossing.ToString("R") + " is not a place" + where);
                        }

                        for (int i = 0; i < places.Length; i++)
                        {
                            GeoPoint3 place = places[i];
                            double knot = knots[i];
                            bool atEnd = Math.Min(place.DistanceTo(spline.StartPoint), Math.Min(place.DistanceTo(spline.EndPoint), Math.Min(place.DistanceTo(copy.StartPoint), place.DistanceTo(copy.EndPoint)))) <= 2E-3;
                            Assert.True(atEnd || may.Any(k => Math.Abs(k - knot) <= near), label + ": the place " + place + " is no least where the copy goes across" + where);
                        }

                        crossings += must.Count;
                    }

                    GeoLine3 leaving = spline.GetShortestLineTo(copy, Strict);
                    GeoLine3 landing = copy.GetShortestLineTo(spline, Strict);

                    Assert.True(Math.Abs(leaving.Length - there) <= slack, label + ": the shortest segment is " + leaving.Length.ToString("R") + " long, the distance " + there.ToString("R"));
                    Assert.True(SameBits(leaving.StartPoint, landing.EndPoint) && SameBits(leaving.EndPoint, landing.StartPoint), label + ": the shortest segments are not the same turned round");
                    asked++;
                }
            }

            _output.WriteLine(twins + " copies in a plane square to an axis, the plane's answers to the bit; " + asked + " copies in space asked, all five queries from both sides, " + crossings + " would-be crossings held to the oracle");
        }

        /// <summary>
        /// Finds, for a spline and its copy moved by a shift shorter than the tolerance, where the two would cross if they were brought into
        /// one plane, by a dense sample of the spline that shares no code with the library's search.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="v">The shift.</param>
        /// <param name="tolerance">The point tolerance.</param>
        /// <param name="must">The leasts that are places for certain: the side clear two hundredths of the tolerance out, the one side and the other, before the distance comes nearer.</param>
        /// <param name="may">The leasts where the side changes at all.</param>
        /// <remarks>
        /// The copy is the spline moved by v, so the gap from a point of the spline to the copy is v less its part along the tangent T,
        /// least where v.T is greatest either way. Seen along the gap g there, as if the two were laid in the plane square to it, the copy
        /// at a point further on stands across the spline by the part of v along g x T, square to the gap and to the spline's direction
        /// at that point, nought at the least itself. Leasts within the tolerance of each other are one, and those within a thousandth of
        /// the domain of an end are left to the ends.
        /// </remarks>
        private static void WouldCrossings(GeoSpline3 spline, GeoVector3 v, double tolerance, out List<double> must, out List<double> may)
        {
            const int Samples = 40000;
            double from = spline.DomainStart;
            double to = spline.DomainEnd;
            var along = new double[Samples + 1];
            var tangents = new GeoVector3[Samples + 1];
            var points = new GeoPoint3[Samples + 1];

            for (int i = 0; i <= Samples; i++)
            {
                double knot = from + (to - from) * i / Samples;
                GeoVector3 d1 = spline.GetDerivativeAtKnot(knot, 1);
                double speed = d1.Length;
                tangents[i] = new GeoVector3(d1.X / speed, d1.Y / speed, d1.Z / speed);
                along[i] = v.X * tangents[i].X + v.Y * tangents[i].Y + v.Z * tangents[i].Z;
                points[i] = spline.GetPointAtKnot(knot);
            }

            double flank = 2E-2 * tolerance;
            must = new List<double>();
            may = new List<double>();
            GeoPoint3? last = null;

            for (int i = 1; i < Samples; i++)
            {
                double here = along[i] * along[i];

                if (!(here > along[i - 1] * along[i - 1] && here >= along[i + 1] * along[i + 1]) || i < Samples / 1000 || i > Samples - Samples / 1000)
                {
                    continue;
                }

                if (Side(v, tangents, along, i, -1, 0.0) * Side(v, tangents, along, i, 1, 0.0) < 0)
                {
                    may.Add(from + (to - from) * i / Samples);
                }

                int left = Side(v, tangents, along, i, -1, flank);
                int right = Side(v, tangents, along, i, 1, flank);

                if (left * right < 0 && (last == null || last.Value.DistanceTo(points[i]) > tolerance))
                {
                    must.Add(from + (to - from) * i / Samples);
                    last = points[i];
                }
            }
        }

        /// <summary>
        /// Gets the side, 1 or -1, the copy's offset across stands on away from a least one way along the sample, once it is a flank out,
        /// and 0 when it goes back across first, or the distance comes nearer than at the least on the way.
        /// </summary>
        private static int Side(GeoVector3 v, GeoVector3[] tangents, double[] along, int at, int way, double flank)
        {
            int sign = 0;
            var gap = new GeoVector3(v.X - along[at] * tangents[at].X, v.Y - along[at] * tangents[at].Y, v.Z - along[at] * tangents[at].Z);

            for (int j = at + way; j > 0 && j < along.Length - 1; j += way)
            {
                GeoVector3 square = gap.CrossProduct(tangents[j]);
                double across = (v.X * square.X + v.Y * square.Y + v.Z * square.Z) / square.Length;

                if (Math.Abs(across) > 1E-12)
                {
                    int now = across < 0.0 ? -1 : 1;

                    if (sign != 0 && now != sign)
                    {
                        return 0;
                    }

                    sign = now;

                    if (Math.Abs(across) > flank)
                    {
                        return sign;
                    }
                }

                // A nearer least on the way: this one is no least of its stretch.
                if (along[j] * along[j] > along[at] * along[at] * (1.0 + 1E-12))
                {
                    return 0;
                }
            }

            return 0;
        }

        public static TheoryData<string> NastyKinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in SplineGen.NastyKinds)
            {
                data.Add(kind);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(NastyKinds))]
        public void ANastySplineInSpace_AgainstTheShapes_IsTheOracle(string nasty)
        {
            var random = new Random(73000 + Array.IndexOf(SplineGen.NastyKinds, nasty));
            string[] kinds = { "segment", "xline", "plane", "polygon", "aabb", "sphere", "circle", "ellipse", "arc", "chain" };
            double worst = 0.0;
            int checkedCount = 0;

            for (int draw = 0; draw < 5 * Multiplier; draw++)
            {
                SplineOracle o;

                try
                {
                    o = SplineGen.Nasty(random, 3, nasty);
                    Make(o);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                GeoSpline3 spline = Make(o);
                double[] box = BoxOf(o);

                foreach (string kind in kinds)
                {
                    Meet3Shape shape = Spline3MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[3]), 0.5 * (box[1] + box[4]), 0.5 * (box[2] + box[5]), Reach(o));

                    if (Check(Name(o, nasty + "/" + kind, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                    }
                }
            }

            _output.WriteLine(nasty + ": " + checkedCount + " pairs held to the oracle; worst distance error " + worst.ToString("0.0E+0") + " of the size of the spline");
            Assert.True(checkedCount > 5, "too few pairs checked: " + checkedCount);
        }

        /// <summary>
        /// A second spline drawn to pass through a point of the first: fit points at random with that point among them, a spline through
        /// them, which meets the first there, within rounding, and wherever else the two happen to cross.
        /// </summary>
        [Fact]
        public void ASplineThroughAPointOfAnotherSplineInSpace_MeetsItThere_FromEitherSide()
        {
            var random = new Random(76000);
            int checkedCount = 0;
            int others = 0;

            for (int draw = 0; draw < 12 * Multiplier; draw++)
            {
                SplineOracle first = SplineGen.Draw(random, 3, maxDegree: 5);
                GeoSpline3 a = Make(first);
                double u = first.Start + (first.End - first.Start) * (0.05 + 0.9 * random.NextDouble());
                double[] at = first.FastPoint(u);
                double reach = Reach(first);
                var fit = new List<GeoPoint3>();
                int count = 3 + random.Next(4);
                int through = random.Next(count);

                for (int i = 0; i < count; i++)
                {
                    fit.Add(i == through
                        ? new GeoPoint3(at[0], at[1], at[2])
                        : new GeoPoint3(at[0] + reach * (random.NextDouble() - 0.5) * 2.0, at[1] + reach * (random.NextDouble() - 0.5) * 2.0, at[2] + reach * (random.NextDouble() - 0.5) * 2.0));
                }

                GeoSpline3 b;

                try
                {
                    b = GeoSpline3.Interpolate(fit, 3, Strict);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                double slack = 1E-6 * reach;
                GeoPoint3 point = new GeoPoint3(at[0], at[1], at[2]);
                GeoPoint3[] along = a.GetIntersections(b, Strict);
                GeoPoint3[] back = b.GetIntersections(a, Strict);
                string label = "draw " + draw + " through " + point + ": " + Name(first, "A", draw) + " | " + string.Join(" ", fit);

                Assert.True(a.DistanceTo(b, Strict) <= 1E-9 * reach, label + ": the distance is " + a.DistanceTo(b, Strict).ToString("R"));
                Assert.Equal(a.DistanceTo(b, Strict), b.DistanceTo(a, Strict));
                Assert.True(along.Any(p => p.DistanceTo(point) <= 2E-3), label + ": no place at the point, in " + string.Join(" ", along));
                Assert.Equal(along.Length, back.Length);

                foreach (GeoPoint3 p in along)
                {
                    Assert.True(a.DistanceTo(p) <= slack && b.DistanceTo(p) <= 1E-3 + slack, label + ": the place " + p + " is off a curve");
                    Assert.Contains(back, q => SameBits(p, q));
                }

                for (int i = 0; i < along.Length; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        Assert.True(along[i].DistanceTo(along[j]) > 1E-3, label + ": two places within the point tolerance of each other");
                    }
                }

                others += along.Length - 1;
                checkedCount++;
            }

            _output.WriteLine(checkedCount + " pairs of splines through a common point, each meeting there (" + others + " other places in all)");
            Assert.True(checkedCount > 5, "too few pairs checked: " + checkedCount);
        }

        /// <summary>
        /// Two circles of splines in one plane of space, tilted any way and as far from the origin as a million: where they meet is given by the
        /// formula of two circles, which shares no code with the library.
        /// </summary>
        [Fact]
        public void TwoCircleSplinesInOnePlaneInSpace_MeetWhereTwoCirclesDo()
        {
            var random = new Random(77000);
            int checkedCount = 0;
            int touching = 0;

            for (int draw = 0; draw < 30 * Multiplier; draw++)
            {
                double[] far = { 0.0, 1E3, 1E5, 1E6 };
                double reach = far[random.Next(far.Length)];
                var frame = new GeoCoordinateSystem3(
                    new GeoPoint3(reach * (random.NextDouble() - 0.5), reach * (random.NextDouble() - 0.5), reach * (random.NextDouble() - 0.5)),
                    new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize(),
                    new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize());
                double r1 = 5.0 + 95.0 * random.NextDouble();
                double r2 = 5.0 + 95.0 * random.NextDouble();
                double angle = 2.0 * Math.PI * random.NextDouble();
                double d = Math.Abs(r1 - r2) + (r1 + r2 - Math.Abs(r1 - r2)) * (0.05 + 0.9 * random.NextDouble());
                double cx = d * Math.Cos(angle);
                double cy = d * Math.Sin(angle);
                GeoSpline3 first = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), r1)).ToSpline3(frame);
                GeoSpline3 second = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(cx, cy), r2)).ToSpline3(frame);

                // The two points: along the line of centres from the first, the foot, and half the chord square to it.
                double along = (d * d + r1 * r1 - r2 * r2) / (2.0 * d);
                double half = Math.Sqrt(Math.Max(0.0, r1 * r1 - along * along));
                double ux = cx / d;
                double uy = cy / d;
                GeoPoint3[] expected =
                {
                    frame.ToGlobal(new GeoPoint3(along * ux - half * uy, along * uy + half * ux, 0.0)),
                    frame.ToGlobal(new GeoPoint3(along * ux + half * uy, along * uy - half * ux, 0.0))
                };

                // A pair within a few thousandths of touching, or of the point tolerance of one another, is a matter for the hand cases.
                if (half < 0.05 || Math.Abs(d - (r1 + r2)) < 0.05 || Math.Abs(d - Math.Abs(r1 - r2)) < 0.05)
                {
                    continue;
                }

                GeoPoint3[] places = first.GetIntersections(second, Strict);
                GeoPoint3[] back = second.GetIntersections(first, Strict);
                string label = "draw " + draw + " in " + frame + ": radii " + r1 + " and " + r2 + " centres " + d + " apart";

                Assert.True(places.Length == 2, label + ": " + places.Length + " places " + string.Join(" ", places));
                Assert.Equal(2, back.Length);
                Assert.All(expected, e => Assert.Contains(places, p => p.DistanceTo(e) <= 1E-5 * Math.Max(1.0, reach * 1E-3) + 1E-6));
                Assert.Equal(0.0, first.DistanceTo(second, Strict));
                touching++;
                checkedCount++;
            }

            _output.WriteLine(checkedCount + " pairs of circle splines in a plane of space, each meeting in the two points of the formula (" + touching + ")");
            Assert.True(checkedCount > 15, "too few pairs checked: " + checkedCount);
        }

        [Fact]
        public void ASplineAndAnotherSplineInSpace_AreTheOracle_AndFromEitherSideTheSame()
        {
            var random = new Random(74000);
            int checkedCount = 0;
            int near = 0;

            for (int draw = 0; draw < 10 * Multiplier; draw++)
            {
                SplineOracle first = SplineGen.Draw(random, 3, maxDegree: 5, thinSpans: draw % 4 == 0);
                SplineOracle second = SplineGen.Draw(random, 3, maxDegree: 5);
                double[] box = BoxOf(first);
                double[] box2 = BoxOf(second);
                var moved = new double[second.Points.Length];
                double[] shift = new double[3];

                // The second is moved to lie about the first.
                for (int k = 0; k < 3; k++)
                {
                    shift[k] = 0.5 * (box[k] + box[3 + k]) - 0.5 * (box2[k] + box2[3 + k]) + (random.NextDouble() - 0.5) * Reach(first);
                }

                for (int i = 0; i < moved.Length; i++)
                {
                    moved[i] = Math.Round(second.Points[i] + shift[i % 3], 4);
                }

                second = new SplineOracle(3, second.Degree, second.Knots, moved, second.Weights.Any(w => w != 1.0) ? second.Weights : null);
                GeoSpline3 a = Make(first);
                GeoSpline3 b = Make(second);
                double size = Math.Max(Reach(first), Reach(second));
                double slack = 1E-6 * size;
                Spline3MeetOracle.SplineAnswer expected = Spline3MeetOracle.AskSplines(first, second, 1E-3, 360);
                string label = "draw " + draw + ": " + Name(first, "A", draw) + " | " + Name(second, "B", draw);

                double ab = a.DistanceTo(b, Strict);
                double ba = b.DistanceTo(a, Strict);
                Assert.True(Bits(ab) == Bits(ba), label + ": the distance is " + ab.ToString("R") + " one way and " + ba.ToString("R") + " the other");

                // A least distance found by the oracle's search is above the true one; the library's is a real pair of points, so it is never above it.
                Assert.True(ab <= expected.Distance + slack, label + ": the library says " + ab.ToString("R") + ", more than the oracle's " + expected.Distance.ToString("R"));

                if (ab > 2E-3 && ab >= expected.Distance - slack)
                {
                    Assert.True(ab >= expected.Distance - slack, label + ": the library says " + ab.ToString("R") + ", the oracle " + expected.Distance.ToString("R"));
                }
                else if (ab > 2E-3)
                {
                    // Below the oracle's: the oracle's grid missed a nearer pair, which the library's segment has to show to be real, the two
                    // ends on the two curves and as far apart as the distance says.
                    GeoLine3 shown = a.GetShortestLineTo(b, Strict);

                    Assert.True(a.DistanceTo(shown.StartPoint) <= 10.0 * slack && b.DistanceTo(shown.EndPoint) <= 10.0 * slack && Math.Abs(shown.Length - ab) <= slack, label + ": the library's " + ab.ToString("R") + " is below the oracle's " + expected.Distance.ToString("R") + " and its segment is not a real pair");
                }

                GeoPoint3[] along = a.GetIntersections(b, Strict);
                GeoPoint3[] other = b.GetIntersections(a, Strict);

                Assert.Equal(along.Length, other.Length);

                foreach (GeoPoint3 p in along)
                {
                    Assert.Contains(other, q => SameBits(p, q));

                    // Every place given is on the first curve and within the tolerance of the second.
                    Assert.True(a.DistanceTo(p) <= slack && b.DistanceTo(p) <= 1E-3 + slack, label + ": the place " + p + " is off a curve");
                }

                foreach (double[] p in expected.Places)
                {
                    Assert.True(along.Any(q => Math.Sqrt((p[0] - q.X) * (p[0] - q.X) + (p[1] - q.Y) * (p[1] - q.Y) + (p[2] - q.Z) * (p[2] - q.Z)) <= 2E-3), label + ": the oracle's place (" + p[0] + ", " + p[1] + ", " + p[2] + ") is missing from " + string.Join(" ", along));
                }

                near += along.Length;
                GeoLine3 line = a.GetShortestLineTo(b, Strict);
                GeoLine3 reversed = b.GetShortestLineTo(a, Strict);

                Assert.True(SameBits(line.StartPoint, reversed.EndPoint) && SameBits(line.EndPoint, reversed.StartPoint), label + ": the shortest segments are not the same turned round");
                Assert.True(Math.Abs(line.Length - ab) <= 1E-9 * size, label + ": the shortest segment is " + line.Length.ToString("R") + " long and the distance " + ab.ToString("R"));
                checkedCount++;
            }

            _output.WriteLine(checkedCount + " pairs of splines in space held to the oracle (" + near + " places in all)");
        }
    }
}
