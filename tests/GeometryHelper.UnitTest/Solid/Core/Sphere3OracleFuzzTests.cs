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
    /// The ball in space against every shape it meets, on random shapes and random balls, held to an oracle in closed form that shares no
    /// code with the library (<see cref="Sphere3Oracle"/>): the distance, whether they collide, where the surface of the ball is crossed
    /// and the shortest line, from both sides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every run draws 2 000 shapes and balls of each of the 18 kinds the infinite line is asked about, 36 000 pairs: a fifth of the balls
    /// laid anywhere, two fifths about a point of the shape, a fifth with a radius set so that the gap to the shape is one of 0, 0.0005,
    /// 0.002, 0.3 and 5, and a fifth so large that the shape lies inside. Setting the environment variable <c>SPHERE3_FUZZ</c> to a
    /// count draws that many of each kind, and <c>SPHERE3_SEED</c> changes the seed.
    /// </para>
    /// <para>
    /// What is held where: the distance to the oracle, to 5E-7; the collision to the gap being within the point tolerance, but for a gap
    /// within a millionth of it; the crossings to the places the surface meets the shape, where the tolerance does not decide them (see
    /// <see cref="Sphere3Oracle"/>); the shortest line to the distance, starting on the surface and ending on the shape, or of no length
    /// where the shape reaches into the ball. Asked from either side the two answer the same to the bit, and every form without a
    /// tolerance answers what the form with the default one does.
    /// </para>
    /// </remarks>
    public class Sphere3OracleFuzzTests
    {
        private static int Cases => int.TryParse(Environment.GetEnvironmentVariable("SPHERE3_FUZZ"), out int n) ? n : 2000;

        private static int Seed => int.TryParse(Environment.GetEnvironmentVariable("SPHERE3_SEED"), out int n) ? n : 20261020;

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        public static TheoryData<string> KindRows()
        {
            var data = new TheoryData<string>();

            foreach (string kind in XLine3Gen.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        private enum Layout
        {
            Anywhere,
            About,
            Gap,
            Around
        }

        /// <summary>What a run has judged, for it to say it looked at something.</summary>
        private sealed class Tally
        {
            internal int Collided;
            internal int Crossed;
            internal int Judged;
        }

        private static XLineSpecimen Draw(string kind, Random random)
        {
            XLineSpecimen specimen;

            do
            {
                specimen = XLine3Gen.Make(kind, random);
            }
            while (!Sphere3Oracle.IsSound(specimen));

            return specimen;
        }

        private static GeoSphere3 MakeBall(Random r, XLineSpecimen specimen, Layout layout)
        {
            switch (layout)
            {
                case Layout.Anywhere:
                    return new GeoSphere3(XLine3Gen.RandomPoint(r, 150).ToPoint(), Between(r, 5, 120));

                case Layout.About:
                {
                    Vec on = specimen.PointOn(r);
                    Vec at = on + XLine3Gen.RandomUnit(r) * Between(r, 0, 40);

                    return new GeoSphere3(at.ToPoint(), Between(r, 5, 80));
                }

                case Layout.Gap:
                {
                    double[] gaps = { 0.0, 0.0005, 0.002, 0.3, 5.0 };

                    for (int attempt = 0; attempt < 50; attempt++)
                    {
                        Vec at = XLine3Gen.RandomPoint(r, 150);
                        double distance = Sphere3Oracle.Reading(specimen, at);
                        double radius = distance - gaps[r.Next(gaps.Length)];

                        if (radius >= 1.0)
                        {
                            return new GeoSphere3(at.ToPoint(), radius);
                        }
                    }

                    return new GeoSphere3(XLine3Gen.RandomPoint(r, 150).ToPoint(), 20);
                }

                default:
                {
                    Vec on = specimen.PointOn(r);

                    return new GeoSphere3(on.ToPoint(), Between(r, 300, 600));
                }
            }
        }

        [Theory]
        [MemberData(nameof(KindRows))]
        public void ABallAskedOfAShape_AnswersWhatTheOracleDoes_FromBothSides(string kind)
        {
            var random = new Random(Seed + 7919 * Array.IndexOf(XLine3Gen.Kinds, kind));
            var failures = new List<string>();
            var tally = new Tally();
            Layout[] layouts = { Layout.Anywhere, Layout.About, Layout.About, Layout.Gap, Layout.Around };

            for (int i = 0; i < Cases; i++)
            {
                XLineSpecimen specimen = Draw(kind, random);
                Layout layout = layouts[i % layouts.Length];
                GeoSphere3 ball = MakeBall(random, specimen, layout);

                try
                {
                    Check(specimen, ball, tally);
                }
                catch (Exception e)
                {
                    failures.Add(kind + " #" + i + " " + layout + ": " + e.Message + "\n   shape " + specimen.Shape + "\n   ball  " + ball);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of " + Cases + " pairs of " + kind + " went wrong:\n" + string.Join("\n", failures.Take(8)));

            // The run asked what it meant to: a good share of the pairs touch, cross and are measured, so that it is not judging balls
            // that all miss.
            Assert.True(tally.Collided >= Cases / 10, kind + ": only " + tally.Collided + " of " + Cases + " pairs collided");

            if (Sphere3Oracle.Elements(XLine3Gen.Make(kind, new Random(1))) != null)
            {
                Assert.True(tally.Judged >= Cases / 4, kind + ": only " + tally.Judged + " of " + Cases + " pairs had their crossings judged");
                Assert.True(tally.Crossed >= Cases / 20, kind + ": only " + tally.Crossed + " pairs of " + Cases + " crossed with the crossings judged");
            }
        }

        private static void AssertSamePoints(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points against " + expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]), what + ": point " + i + " " + actual[i] + " against " + expected[i]);
            }
        }

        private static void AssertSameLine(GeoLine3 line, GeoLine3 turned, string what)
        {
            Assert.True(line.StartPoint.Equals(turned.EndPoint) && line.EndPoint.Equals(turned.StartPoint), what + ": " + line + " against " + turned + " turned round");
        }

        private static void Check(XLineSpecimen specimen, GeoSphere3 ball, Tally tally)
        {
            object shape = specimen.Shape;
            Vec center = Vec.Of(ball.Center);
            double reading = Sphere3Oracle.Reading(specimen, center);
            double gap = Math.Max(0.0, reading - ball.Radius);
            double slack = 5E-7 * Math.Max(1.0, reading);

            // The distance, with and without a tolerance where the shape takes one, from either side.
            double distance = Distance(ball, shape);

            Assert.True(Math.Abs(distance - gap) <= slack, "distance " + distance + " against " + gap);
            Assert.True(distance.Equals(Distance(shape, ball)), "the distance is not the same from the shape");

            if (Answers(ball, "DistanceTo", shape))
            {
                Assert.True(distance.Equals(Distance(ball, shape, Strict)), "the form with a tolerance gives another distance");
                Assert.True(distance.Equals(Distance(shape, ball, Strict)), "the form with a tolerance gives another distance from the shape");
            }


            // The collision.
            tally.Collided += shape is GeoPoint3 && gap <= 1E-3 ? 1 : 0;

            if (!(shape is GeoPoint3))
            {
                bool collides = Collides(ball, shape, Strict);

                if (Math.Abs(gap - 1E-3) > 1E-6)
                {
                    Assert.True(collides == (gap <= 1E-3), "collides " + collides + " at a gap of " + gap);
                }

                Assert.True(collides == Collides(shape, ball, Strict), "the collision is not the same from the shape");
                Assert.True(collides == Collides(ball, shape), "the form without a tolerance collides otherwise");
                Assert.True(collides == Collides(shape, ball), "the form without a tolerance collides otherwise from the shape");

                tally.Collided += collides ? 1 : 0;
            }

            // The shortest line.
            GeoLine3 line = Shortest(ball, shape, Strict);

            Assert.True(line.StartPoint.IsValid && line.EndPoint.IsValid, "the shortest line is not a number");
            AssertSameLine(line, Shortest(shape, ball, Strict), "the shortest line from the shape");
            Assert.True(line.Equals(Shortest(ball, shape)), "the form without a tolerance gives another shortest line");
            AssertSameLine(line, Shortest(shape, ball), "the shortest line from the shape without a tolerance");

            // The nearest point of a polygon or a face keeps a foot that lands within the point tolerance past its edge, as the library's
            // own shortest line from them to a point does, so the line may end that much beyond the shape.
            double beyond = shape is GeoPolygon3 || shape is GeoFace3 ? 1E-3 + 1E-6 : 1E-6;

            if (gap > 1E-6)
            {
                Assert.True(Math.Abs(Vec.Of(line.StartPoint).DistanceTo(center) - ball.Radius) <= 1E-9 * Math.Max(1.0, ball.Radius), "the line does not start on the surface");
                Assert.True(Sphere3Oracle.Reading(specimen, Vec.Of(line.EndPoint)) <= beyond, "the line does not end on the shape");
                Assert.True(Math.Abs(line.Length - gap) <= slack + beyond, "the line is " + line.Length + " long against a gap of " + gap);
            }
            else if (reading < ball.Radius - 1E-6)
            {
                Assert.True(line.Length == 0.0, "the line has a length of " + line.Length + " where the shape reaches into the ball");
                Assert.True(Sphere3Oracle.Reading(specimen, Vec.Of(line.StartPoint)) <= beyond, "the line of no length does not lie on the shape");
            }

            // The crossings.
            List<Sphere3Oracle.Element> elements = Sphere3Oracle.Elements(specimen);

            if (elements == null)
            {
                Assert.False(Answers(ball, "GetIntersections", shape), "a shape with no crossings answers them");
                return;
            }

            GeoPoint3[] crossings = Crossings(ball, shape, Strict);

            AssertSamePoints(crossings, Crossings(shape, ball, Strict), "the crossings from the shape");
            AssertSamePoints(crossings, Crossings(ball, shape), "the crossings without a tolerance");
            AssertSamePoints(crossings, Crossings(shape, ball), "the crossings from the shape without a tolerance");
            Assert.True(Try(ball, shape, out GeoPoint3[] tried, Strict) == crossings.Length > 0, "TryIntersectWith disagrees with the crossings");
            AssertSamePoints(crossings, tried, "TryIntersectWith");
            Assert.True(Try(shape, ball, out tried, Strict) == crossings.Length > 0, "TryIntersectWith from the shape disagrees with the crossings");
            Assert.True(Try(ball, shape, out tried) == crossings.Length > 0, "TryIntersectWith without a tolerance disagrees with the crossings");

            foreach (GeoPoint3 point in crossings)
            {
                Assert.True(Math.Abs(Vec.Of(point).DistanceTo(center) - ball.Radius) <= 1E-3 + 1E-9, "a crossing lies " + Math.Abs(Vec.Of(point).DistanceTo(center) - ball.Radius) + " off the surface");
            }

            Vec[] expected = Sphere3Oracle.Crossings(elements, center, ball.Radius, out bool delicate);

            if (delicate)
            {
                return;
            }

            tally.Judged++;
            tally.Crossed += expected.Length > 0 ? 1 : 0;

            Assert.True(expected.Length == crossings.Length, crossings.Length + " crossings against " + expected.Length + ": " + string.Join(" ", crossings));

            foreach (Vec point in expected)
            {
                Assert.True(crossings.Any(c => Vec.Of(c).DistanceTo(point) <= 1E-6), "the crossing " + point.ToPoint() + " is not found");
            }

            foreach (GeoPoint3 point in crossings)
            {
                Assert.True(expected.Any(e => e.DistanceTo(Vec.Of(point)) <= 1E-6), "the crossing " + point + " is not one");
            }
        }
    }
}
