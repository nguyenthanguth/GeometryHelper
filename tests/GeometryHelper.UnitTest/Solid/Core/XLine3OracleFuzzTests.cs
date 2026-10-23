using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The infinite line in space against every shape it meets, on random shapes and random lines, held to an oracle in closed
    /// form that shares no code with the library (<see cref="XLineSpecimen"/>): the distance, whether they collide, where the line
    /// crosses the shape and the shortest line, from both sides, with the line taken as two rays, turned round and moved along
    /// itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every run draws 1 500 shapes and lines of each of the 18 kinds, 27 000 pairs, a fifth of the lines laid anywhere, two
    /// fifths through a point of the shape, from anywhere along themselves up to a million away, a fifth along an axis and a
    /// fifth moved a few thousandths off a point of the shape, close enough to the point tolerance for it to decide. Setting
    /// the environment variable <c>XLINE3_FUZZ</c> to a count draws that many of each kind, and <c>XLINE3_SEED</c> changes the
    /// seed.
    /// </para>
    /// <para>
    /// What is held where: the distance to the oracle, to 5E-7, where the shape is measured (the curved shapes are not, as for
    /// a ray); the collision to the distance being within the point tolerance, but for a distance within a millionth of the
    /// tolerance; the crossings to the places the line meets the shape exactly, where the line does not pass within four
    /// thousandths of what it meets, so that the tolerance does not decide; the shortest line to the distance, starting on the
    /// line and ending on the shape. Asked from either side the two answer the same to the bit, and every form without a
    /// tolerance answers what the form with the global one does.
    /// </para>
    /// </remarks>
    public class XLine3OracleFuzzTests
    {
        private static int Cases => int.TryParse(Environment.GetEnvironmentVariable("XLINE3_FUZZ"), out int n) ? n : 1500;

        private static int Seed => int.TryParse(Environment.GetEnvironmentVariable("XLINE3_SEED"), out int n) ? n : 20261020;

        public static TheoryData<string> KindRows()
        {
            var data = new TheoryData<string>();

            foreach (string kind in XLine3Gen.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(KindRows))]
        public void ALineAskedOfAShape_AnswersWhatTheOracleDoes_FromBothSides(string kind)
        {
            var random = new Random(Seed + 7919 * Array.IndexOf(XLine3Gen.Kinds, kind));
            var failures = new List<string>();
            var tally = new Tally();
            XLine3Gen.Layout[] layouts =
            {
                XLine3Gen.Layout.Random, XLine3Gen.Layout.Through, XLine3Gen.Layout.Through, XLine3Gen.Layout.Axis, XLine3Gen.Layout.NearlyThrough
            };

            for (int i = 0; i < Cases; i++)
            {
                XLineSpecimen specimen = XLine3Gen.Make(kind, random);
                XLine3Gen.Layout layout = layouts[i % layouts.Length];
                GeoXLine3 line = XLine3Gen.MakeLine(random, specimen, layout);

                try
                {
                    Check(specimen, line, tally);
                }
                catch (Exception e)
                {
                    failures.Add(kind + " #" + i + " " + layout + ": " + e.Message + "\n   shape " + specimen.Shape + "\n   line  " + line);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of " + Cases + " pairs of " + kind + " went wrong:\n" + string.Join("\n", failures.Take(8)));

            // The run asked what it meant to: a good share of the pairs touch, cross and are measured, so that it is not
            // judging shapes the lines all miss.
            if (kind != "Point3")
            {
                Assert.True(tally.Collided >= Cases / 10, kind + ": only " + tally.Collided + " of " + Cases + " pairs collided");
                Assert.True(tally.Crossed >= Cases / 10, kind + ": only " + tally.Crossed + " pairs of " + Cases + " crossed with the crossings judged");
            }

            Assert.True(tally.Judged >= Cases / 3 || kind == "Point3", kind + ": only " + tally.Judged + " of " + Cases + " pairs had their crossings judged");
        }

        /// <summary>What a run has judged, for it to say it looked at something.</summary>
        private sealed class Tally
        {
            internal int Collided;
            internal int Judged;
            internal int Crossed;
        }

        private static void AssertSamePoints(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points against " + expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]), what + ": point " + i + " " + actual[i] + " against " + expected[i]);
            }
        }

        private static void Check(XLineSpecimen specimen, GeoXLine3 line, Tally tally)
        {
            object shape = specimen.Shape;
            bool isPoint = shape is GeoPoint3;
            var oracleLine = new Ln(line);
            double distance = specimen.Distance(oracleLine);
            double slack = 5E-7 * Math.Max(1.0, distance);

            // The questions the line asks, the shape asks too, and the other way about, in each form.
            foreach (string query in new[] { "DistanceTo", "CollidesWith", "GetIntersections", "GetShortestLineTo" })
            {
                if (query == "CollidesWith" && isPoint)
                {
                    continue;
                }

                Assert.True(Answers(line, query, shape) == Answers(shape, query, line), query + " is one-sided");
            }

            // Whether they collide: the distance within the point tolerance, but for a hair either side of it.
            if (!isPoint)
            {
                bool fromLine = Collides(line, shape, Strict);
                bool fromShape = Collides(shape, line, Strict);

                tally.Collided += fromLine ? 1 : 0;
                Assert.Equal(fromLine, fromShape);
                Assert.Equal(Collides(line, shape, Tolerance.Global), Collides(line, shape));
                Assert.Equal(Collides(shape, line, Tolerance.Global), Collides(shape, line));

                // The collision of an ellipse with a segment reads the tolerance where the segment crosses the plane of the ellipse, and
                // so misses a segment that passes the rim within the tolerance at a slant, whatever its length: the line, answered as a
                // segment, shares that, and an ellipse is exempt in the band from half the tolerance up to it.
                bool inherited = specimen.Kind == "Ellipse3" && distance > 0.5 * Strict.EqualPoint && distance <= Strict.EqualPoint;

                if (Math.Abs(distance - Strict.EqualPoint) > 1E-6 && !inherited)
                {
                    Assert.True(distance <= Strict.EqualPoint == fromLine, "collides " + fromLine + " where the oracle's distance is " + distance);
                }
            }

            // The distance: the oracle's, the same from both sides to the bit, and the line taken as two rays.
            if (Answers(line, "DistanceTo", shape))
            {
                double fromLine = Distance(line, shape, Strict);
                double fromShape = Distance(shape, line, Strict);

                Assert.Equal(fromLine, fromShape);
                Assert.Equal(Distance(line, shape, Tolerance.Global), Distance(line, shape));
                Assert.Equal(Distance(shape, line, Tolerance.Global), Distance(shape, line));
                Assert.True(Math.Abs(fromLine - distance) <= slack, "distance " + fromLine + " against the oracle's " + distance);

                var ahead = new GeoRay3(line.Origin, line.Direction);
                var behind = new GeoRay3(line.Origin, line.Direction.Negate());

                if (!(shape is GeoXLine3) && RayMeasures(ahead, shape))
                {
                    double byRays = Math.Min(RayDistance(ahead, shape), RayDistance(behind, shape));

                    Assert.True(Math.Abs(fromLine - byRays) <= slack, "distance " + fromLine + " against " + byRays + " by two rays");
                }

                // Turned round, and moved along itself, it is the same line.
                Assert.True(Math.Abs(Distance(line.Reverse(), shape, Strict) - fromLine) <= slack, "turned round " + Distance(line.Reverse(), shape, Strict) + " against " + fromLine);
                Assert.True(Math.Abs(Distance(new GeoXLine3(line.GetPointAtDistance(37.5), line.Direction), shape, Strict) - fromLine) <= slack, "moved along itself");
            }

            // The crossings.
            if (Answers(line, "GetIntersections", shape))
            {
                GeoPoint3[] fromLine = Crossings(line, shape, Strict);
                GeoPoint3[] fromShape = Crossings(shape, line, Strict);

                AssertSamePoints(fromLine, fromShape, "the crossings from both sides");
                AssertSamePoints(Crossings(line, shape, Tolerance.Global), Crossings(line, shape), "the plain form");
                AssertSamePoints(Crossings(shape, line, Tolerance.Global), Crossings(shape, line), "the plain form from the shape");

                Assert.True(HasTry(line, shape) == HasTry(shape, line), "TryIntersectWith is one-sided");

                if (HasTry(line, shape))
                {
                    bool tried = Try(line, shape, out GeoPoint3[] points, Strict);
                    bool triedBack = Try(shape, line, out GeoPoint3[] pointsBack, Strict);

                    Assert.Equal(fromLine.Length > 0, tried);
                    Assert.Equal(tried, triedBack);
                    AssertSamePoints(fromLine, points, "Try from the line");
                    AssertSamePoints(fromLine, pointsBack, "Try from the shape");
                }

                if (!specimen.Delicate(oracleLine))
                {
                    Vec[] expected = specimen.Meetings(oracleLine);

                    tally.Judged++;
                    tally.Crossed += expected.Length > 0 ? 1 : 0;

                    Assert.True(expected.Length == fromLine.Length, fromLine.Length + " crossings (" + string.Join(" ", fromLine) + ") against the oracle's " + expected.Length + " (" + string.Join(" ", expected.Select(v => v.ToPoint())) + ")");

                    for (int k = 0; k < expected.Length; k++)
                    {
                        Assert.True(expected[k].DistanceTo(Vec.Of(fromLine[k])) <= 1E-6, "crossing " + k + " at " + fromLine[k] + " against the oracle's " + expected[k].ToPoint());
                    }
                }
            }

            // The shortest line: as long as the distance, from the line to the shape and back.
            if (Answers(line, "GetShortestLineTo", shape))
            {
                GeoLine3 leaving = Shortest(line, shape, Strict);
                GeoLine3 landing = Shortest(shape, line, Strict);

                Assert.Equal(leaving, landing.Reverse());
                Assert.Equal(Shortest(line, shape, Tolerance.Global), Shortest(line, shape));
                Assert.Equal(Shortest(shape, line, Tolerance.Global), Shortest(shape, line));

                bool clear = distance > 1.5 * Strict.EqualPoint || distance == 0.0 || distance < 1E-9;

                if (clear)
                {
                    Assert.True(Math.Abs(leaving.Length - distance) <= slack, "the shortest line is " + leaving.Length + " long, the distance " + distance);
                }

                Assert.True(oracleLine.Perp(Vec.Of(leaving.StartPoint)).Length <= 1E-6, "the shortest line leaving the line starts off it at " + leaving.StartPoint);
                // Where the line reaches the shape within the point tolerance the segment kernels' shortest line may land on a crossing
                // read with the tolerance's slack, up to the tolerance off the shape, a line through a body just outside the rim of a face.
                Assert.True(specimen.DistanceFrom(Vec.Of(leaving.EndPoint)) <= 1E-6 + (distance <= 1.5 * Strict.EqualPoint ? 1.5 * Strict.EqualPoint : 0.0), "the shortest line ends " + specimen.DistanceFrom(Vec.Of(leaving.EndPoint)) + " off the shape at " + leaving.EndPoint);
            }
        }

        private static bool RayMeasures(GeoRay3 ray, object shape)
        {
            return ray.GetType().GetMethods().Any(m => m.Name == "DistanceTo" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == shape.GetType());
        }

        private static double RayDistance(GeoRay3 ray, object shape)
        {
            return Answers(ray, "DistanceTo", shape) ? XLine3Probe.Distance(ray, shape, Strict) : XLine3Probe.Distance(ray, shape);
        }
    }
}
