using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A spline and a shape in the plane, laid in a plane of space tilted any way and as far from the origin as a million, must be
    /// what they were: the spline in space answers the same distance, the same places and the same collision as the spline in the plane,
    /// which was reviewed on its own. The two share the maths of the curve and nothing of the maths of the partner.
    /// </summary>
    /// <remarks>
    /// The shapes are those the plane's fuzz draws (<see cref="Spline2MeetShapes"/>), but the rectangle and the curved loop: a curved loop
    /// is a region in the plane and an outline in space, and the shortest segment of a flat region the spline lies in is of no length in space
    /// and the gap to the outline in the plane. A pair whose answer in the plane turns on a few rounding errors, a least of the distance at
    /// the point tolerance, is left out, as it is from the oracle fuzz.
    /// </remarks>
    public class Spline3EmbeddingTests
    {
        private readonly ITestOutputHelper _output;

        public Spline3EmbeddingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in Spline2MeetShapes.Kinds.Where(k => k != "rectangle" && k != "loop"))
            {
                data.Add(kind);
            }

            return data;
        }

        private static GeoCoordinateSystem3 RandomFrame(Random random)
        {
            double[] far = { 0.0, 1E3, 1E5, 1E6 };
            double reach = far[random.Next(far.Length)];
            GeoPoint3 origin = new GeoPoint3(reach * (random.NextDouble() - 0.5), reach * (random.NextDouble() - 0.5), reach * (random.NextDouble() - 0.5));
            GeoVector3 x = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
            GeoVector3 y = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);

            return new GeoCoordinateSystem3(origin, x.Normalize(), y.Normalize());
        }

        private static object Embed(GeoCoordinateSystem3 frame, object shape)
        {
            switch (shape)
            {
                case GeoLine2 s: return PlanarMap.ToLine3(frame, s);
                case GeoRay2 s: return PlanarMap.ToRay3(frame, s);
                case GeoXLine2 s: return PlanarMap.ToXLine3(frame, s);
                case GeoPolyline2 s: return PlanarMap.ToPolyline3(frame, s);
                case GeoPolygon2 s: return PlanarMap.ToPolygon3(frame, s);
                case GeoTriangle2 s: return PlanarMap.ToTriangle3(frame, s);
                case GeoFace2 s: return PlanarMap.ToFace3(frame, s);
                case GeoArc2 s: return PlanarMap.ToArc3(frame, s);
                case GeoCircle2 s: return new GeoCircle3(PlanarMap.ToPoint3(frame, s.Center), frame.ZAxis, s.Radius);
                case GeoEllipse2 s: return PlanarMap.ToEllipse3(frame, s);
                case GeoEdge2 s: return PlanarMap.ToEdge3(frame, s);
                case GeoPolylineArc2 s: return PlanarMap.ToPolylineArc3(frame, s);
                default: throw new NotSupportedException(shape.GetType().Name);
            }
        }

        private static string Name(SplineOracle o, string kind, int draw) => kind + ", draw " + draw + ": degree " + o.Degree + ", " + o.Count + " points " + string.Join(" ", o.Points.Select(v => v.ToString("R"))) + ", knots " + string.Join(" ", o.Knots.Select(v => v.ToString("R"))) + ", weights " + string.Join(" ", o.Weights.Select(v => v.ToString("R")));

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ASplineAndAShapeLaidInSpace_AnswerWhatTheyAnsweredInThePlane(string kind)
        {
            var random = new Random(81000 + Array.IndexOf(Spline2MeetShapes.Kinds, kind));
            int checkedCount = 0;
            int left = 0;
            int places = 0;
            double worst = 0.0;

            for (int draw = 0; draw < 30; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, 2, maxDegree: 5, thinSpans: draw % 4 == 0);
                GeoSpline2 flat = Spline2MeetFuzzTests.Make(o);
                double[] box = new double[4];
                box[0] = o.Points.Where((v, i) => i % 2 == 0).Min();
                box[1] = o.Points.Where((v, i) => i % 2 == 1).Min();
                box[2] = o.Points.Where((v, i) => i % 2 == 0).Max();
                box[3] = o.Points.Where((v, i) => i % 2 == 1).Max();
                double size = Math.Max(1.0, Math.Max(box[2] - box[0], box[3] - box[1]));
                double coordinates = Math.Max(1.0, o.Points.Max(Math.Abs));

                for (int k = 0; k < 3; k++)
                {
                    MeetShape shape = Spline2MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[2]), 0.5 * (box[1] + box[3]), size);
                    Spline2MeetOracle.Answer expected = Spline2MeetOracle.Ask(o, shape, 1E-3);
                    double slack = 1E-9 * size + 64.0 * 2.220446049250313E-16 * coordinates * 16.0;

                    if (Math.Abs(expected.Distance - 1E-3) <= 4.0 * slack || expected.Leasts.Any(v => Math.Abs(v - 1E-3) <= 4.0 * slack) || expected.LongestLevel >= Math.Min(1.0, 0.25 * size))
                    {
                        left++;
                        continue;
                    }

                    GeoCoordinateSystem3 frame = RandomFrame(random);
                    GeoSpline3 laid = flat.ToSpline3(frame);
                    object shape3 = Embed(frame, shape.Shape);
                    string label = Name(o, kind, draw) + " in " + frame + " against " + string.Join("; ", shape.Parts);

                    double inPlane = XLine2Probe.Distance(flat, shape.Shape, XLine2Probe.Strict);
                    double inSpace = XLine3Probe.Distance(laid, shape3, XLine3Probe.Strict);
                    double reach = 1E-7 * size + 1E-9 * Math.Max(coordinates, Math.Abs(frame.Origin.X) + Math.Abs(frame.Origin.Y) + Math.Abs(frame.Origin.Z));
                    worst = Math.Max(worst, Math.Abs(inSpace - inPlane) / size);

                    Assert.True(Math.Abs(inSpace - inPlane) <= reach, label + ": the distance is " + inSpace.ToString("R") + " in space and " + inPlane.ToString("R") + " in the plane");
                    Assert.Equal(inPlane <= 1E-3, XLine3Probe.Collides(laid, shape3, XLine3Probe.Strict));

                    GeoPoint2[] flatPlaces = XLine2Probe.Crossings(flat, shape.Shape, XLine2Probe.Strict);
                    GeoPoint3[] spacePlaces = XLine3Probe.Crossings(laid, shape3, XLine3Probe.Strict);
                    Assert.True(
                        flatPlaces.Length == spacePlaces.Length,
                        label + ": " + spacePlaces.Length + " places in space " + string.Join(" ", spacePlaces) + " and " + flatPlaces.Length + " in the plane " + string.Join(" ", flatPlaces));

                    for (int i = 0; i < flatPlaces.Length; i++)
                    {
                        GeoPoint3 expectedPlace = PlanarMap.ToPoint3(frame, flatPlaces[i]);
                        Assert.True(spacePlaces.Any(p => p.DistanceTo(expectedPlace) <= 2E-3 + reach), label + ": the place " + expectedPlace + " of the plane is not among " + string.Join(" ", spacePlaces));
                    }

                    places += flatPlaces.Length;
                    checkedCount++;
                }
            }

            _output.WriteLine(kind + ": " + checkedCount + " pairs laid in space agree with the plane (" + places + " places, " + left + " left out as at the tolerance); worst distance difference " + worst.ToString("0.0E+0") + " of the size");
            Assert.True(checkedCount > 30, "too few pairs checked: " + checkedCount);
        }
    }
}
