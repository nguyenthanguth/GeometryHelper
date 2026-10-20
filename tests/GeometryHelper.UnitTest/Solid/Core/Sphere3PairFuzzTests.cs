using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ball against another ball and against a plane, on random pairs, held to formulas written out here that share no code with
    /// the library: the gap, the collision, the circle the two surfaces share or the point they touch at, the shortest line, and the same
    /// from the other side to the bit.
    /// </summary>
    /// <remarks>
    /// Every run draws 20 000 pairs of balls and 20 000 balls and planes, setting a third of them up with a gap of 0, 0.0005, 0.002
    /// or 0.3 from touching, outside and inside, so that the point tolerance decides some. Where the tolerance decides, within four
    /// thousandths of touching, the kind of meeting is not judged, only that the two sides agree. <c>SPHERE3_FUZZ</c> sets the count of
    /// each and <c>SPHERE3_SEED</c> the seed.
    /// </remarks>
    public class Sphere3PairFuzzTests
    {
        private static readonly Tolerance Strict = XLine3Probe.Strict;

        private static int Cases => int.TryParse(Environment.GetEnvironmentVariable("SPHERE3_FUZZ"), out int n) ? n : 20000;

        private static int Seed => int.TryParse(Environment.GetEnvironmentVariable("SPHERE3_SEED"), out int n) ? n : 20261020;

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        private static double Dist(GeoPoint3 a, GeoPoint3 b) => Vec.Of(a).DistanceTo(Vec.Of(b));

        private static int Pick(Random r, int count) => r.Next(count);

        private static GeoSphere3 Other(Random r, GeoSphere3 ball)
        {
            double[] gaps = { 0.0, 0.0005, 0.002, 0.3 };
            double radius = Between(r, 5, 100);
            Vec direction = XLine3Gen.RandomUnit(r);

            switch (r.Next(6))
            {
                case 0:
                {
                    // Outside, touching to a gap.
                    double d = ball.Radius + radius + gaps[Pick(r, gaps.Length)];

                    return new GeoSphere3((Vec.Of(ball.Center) + direction * d).ToPoint(), radius);
                }

                case 1:
                {
                    // Inside, touching to a gap, the smaller within the larger.
                    double d = Math.Abs(ball.Radius - radius) + gaps[Pick(r, gaps.Length)] * (r.Next(2) == 0 ? 1 : -1);

                    return new GeoSphere3((Vec.Of(ball.Center) + direction * Math.Max(0.0, d)).ToPoint(), radius);
                }

                case 2:
                    return new GeoSphere3((Vec.Of(ball.Center) + direction * Between(r, 0, 0.005)).ToPoint(), radius);

                default:
                    return new GeoSphere3((Vec.Of(ball.Center) + direction * Between(r, 0, 200)).ToPoint(), radius);
            }
        }

        [Fact]
        public void TwoBalls_AreAsFarApartAsTheFormulasSay_AndMeetWhereTheyShould_FromBothSides()
        {
            var random = new Random(Seed);
            var failures = new List<string>();
            int circles = 0;
            int tangents = 0;
            int touching = 0;

            for (int i = 0; i < Cases; i++)
            {
                var ball = new GeoSphere3(XLine3Gen.RandomPoint(random, 100).ToPoint(), Between(random, 5, 100));
                GeoSphere3 other = Other(random, ball);

                try
                {
                    Check(ball, other, ref circles, ref tangents, ref touching);
                }
                catch (Exception e)
                {
                    failures.Add("#" + i + ": " + e.Message + "\n   ball  " + ball + "\n   other " + other);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of " + Cases + " pairs of balls went wrong:\n" + string.Join("\n", failures.Take(8)));
            Assert.True(circles >= Cases / 10, "only " + circles + " pairs crossed in a circle");
            Assert.True(tangents >= Cases / 100, "only " + tangents + " pairs touched at a point");
            Assert.True(touching >= Cases / 10, "only " + touching + " pairs collided");
        }

        private static void Check(GeoSphere3 a, GeoSphere3 b, ref int circles, ref int tangents, ref int touching)
        {
            double d = Dist(a.Center, b.Center);
            double rim = a.Radius + b.Radius;
            double inner = Math.Abs(a.Radius - b.Radius);
            double gap = Math.Max(0.0, d - rim);

            // The gap, from either side, to the bit.
            Assert.True(Math.Abs(a.DistanceTo(b) - gap) <= 1E-9 * Math.Max(1.0, d), "gap " + a.DistanceTo(b) + " against " + gap);
            Assert.True(a.DistanceTo(b).Equals(b.DistanceTo(a)), "the gap differs from the other side");

            bool collides = a.CollidesWith(b, Strict);

            if (Math.Abs(gap - 1E-3) > 1E-6)
            {
                Assert.True(collides == (gap <= 1E-3), "collides " + collides + " at a gap of " + gap);
            }

            Assert.True(collides == b.CollidesWith(a, Strict), "the collision differs from the other side");
            Assert.True(collides == a.CollidesWith(b), "the form without a tolerance collides otherwise");
            touching += collides ? 1 : 0;

            // The shortest line.
            GeoLine3 line = a.GetShortestLineTo(b, Strict);
            GeoLine3 back = b.GetShortestLineTo(a, Strict);

            Assert.True(line.StartPoint.Equals(back.EndPoint) && line.EndPoint.Equals(back.StartPoint), "the shortest line is not the other side's turned round");
            Assert.True(line.Equals(a.GetShortestLineTo(b)), "the form without a tolerance gives another line");

            if (gap > 1E-9 * Math.Max(1.0, d))
            {
                Assert.True(Math.Abs(Dist(line.StartPoint, a.Center) - a.Radius) <= 1E-9 * Math.Max(1.0, a.Radius), "the line does not start on the first surface");
                Assert.True(Math.Abs(Dist(line.EndPoint, b.Center) - b.Radius) <= 1E-9 * Math.Max(1.0, b.Radius), "the line does not end on the second surface");
                Assert.True(Math.Abs(line.Length - gap) <= 1E-9 * Math.Max(1.0, d), "the line is " + line.Length + " long against a gap of " + gap);
            }
            else if (d < rim - 1E-9 * Math.Max(1.0, d))
            {
                Assert.True(line.Length == 0.0, "the line of two balls that meet has a length");
                Assert.True(Dist(line.StartPoint, a.Center) <= a.Radius + 1E-9 * Math.Max(1.0, a.Radius) && Dist(line.StartPoint, b.Center) <= b.Radius + 1E-9 * Math.Max(1.0, b.Radius), "the line of no length is not inside both");
            }

            // The circle and the point.
            bool circle = a.TryGetIntersectionCircle(b, out GeoCircle3 ours, Strict);
            bool theirCircle = b.TryGetIntersectionCircle(a, out GeoCircle3 theirs, Strict);
            bool tangent = a.TryGetTangentPoint(b, out GeoPoint3 touch, Strict);
            bool theirTangent = b.TryGetTangentPoint(a, out GeoPoint3 theirTouch, Strict);

            Assert.True(circle == theirCircle && tangent == theirTangent, "the two sides disagree on how the surfaces meet");
            Assert.True(!(circle && tangent), "the surfaces cross in a circle and touch at a point");
            tangents += tangent ? 1 : 0;
            Assert.True(circle == a.TryGetIntersectionCircle(b, out GeoCircle3 plain), "the form without a tolerance finds another circle");
            Assert.True(tangent == a.TryGetTangentPoint(b, out GeoPoint3 plainTouch), "the form without a tolerance finds another point");

            if (circle)
            {
                Assert.True(ours.Center.Equals(theirs.Center) && ours.Radius.Equals(theirs.Radius), "the circle differs from the other side");
                Assert.True(ours.Normal.Equals(theirs.Normal.Negate()), "the normal of the circle is not turned round from the other side");
                Assert.True(ours.Equals(plain) && touch.Equals(default(GeoPoint3)) && theirTouch.Equals(default(GeoPoint3)), "a circle comes with a point");
            }

            if (tangent)
            {
                Assert.True(Math.Abs(Dist(touch, a.Center) - a.Radius) <= 1E-3 + 1E-9 && Math.Abs(Dist(touch, b.Center) - b.Radius) <= 1E-3 + 1E-9, "the touch is off a surface by " + Math.Abs(Dist(touch, a.Center) - a.Radius) + " and " + Math.Abs(Dist(touch, b.Center) - b.Radius));
                Assert.True(touch.Equals(theirTouch), "the point differs from the other side");
                Assert.True(touch.Equals(plainTouch), "the form without a tolerance finds another point");
            }

            double nearTouch = Math.Min(Math.Abs(d - rim), Math.Abs(d - inner));

            if (nearTouch <= 4E-3 || d <= 4E-3)
            {
                return;
            }

            // Not touching to within the tolerance: the kind of meeting is that of the formulas.
            double along = (d * d + a.Radius * a.Radius - b.Radius * b.Radius) / (2.0 * d);
            double squared = a.Radius * a.Radius - along * along;
            bool crosses = d < rim && d > inner && squared > 4E-3 * 4E-3;

            if (squared > 0.0 && squared <= 4E-3 * 4E-3 && d < rim && d > inner)
            {
                return;
            }

            Assert.True(circle == crosses, "circle " + circle + " where the formulas say " + crosses + " (d " + d + ", sum " + rim + ", difference " + inner + ")");
            Assert.False(tangent, "a touch where the surfaces are not within the tolerance of touching");

            if (!circle)
            {
                return;
            }

            circles++;

            Vec u = (Vec.Of(b.Center) - Vec.Of(a.Center)).Unit();

            Assert.True(Dist(ours.Center, (Vec.Of(a.Center) + u * along).ToPoint()) <= 1E-9 * Math.Max(1.0, d), "the circle is not centred where the formulas say");
            Assert.True(Math.Abs(ours.Radius - Math.Sqrt(squared)) <= 1E-9 * Math.Max(1.0, a.Radius), "the circle has a radius of " + ours.Radius + " against " + Math.Sqrt(squared));
            Assert.True(Vec.Of(ours.Normal).Cross(u).Length <= 1E-12, "the normal of the circle is not along the line between the centres");
            Assert.True(Vec.Of(ours.Normal).Dot(u) > 0.0, "the normal of the circle does not point from the first ball to the second");

            // Every point of the circle lies on both surfaces.
            Vec n = Vec.Of(ours.Normal);
            Vec ux = n.Cross(Math.Abs(n.X) < 0.6 ? new Vec(1, 0, 0) : new Vec(0, 1, 0)).Unit();
            Vec uy = n.Cross(ux);

            for (int k = 0; k < 16; k++)
            {
                double angle = 2.0 * Math.PI * k / 16.0;
                Vec point = Vec.Of(ours.Center) + ux * (ours.Radius * Math.Cos(angle)) + uy * (ours.Radius * Math.Sin(angle));

                Assert.True(Math.Abs(point.DistanceTo(Vec.Of(a.Center)) - a.Radius) <= 1E-8 * Math.Max(1.0, a.Radius), "a point of the circle is off the first surface");
                Assert.True(Math.Abs(point.DistanceTo(Vec.Of(b.Center)) - b.Radius) <= 1E-8 * Math.Max(1.0, b.Radius), "a point of the circle is off the second surface");
            }
        }

        [Fact]
        public void ABallAndAPlane_MeetWhereTheFormulasSay_FromBothSides()
        {
            var random = new Random(Seed + 1);
            var failures = new List<string>();
            int circles = 0;
            int tangents = 0;

            for (int i = 0; i < Cases; i++)
            {
                Vec o = XLine3Gen.RandomPoint(random, 100);
                Vec n = XLine3Gen.RandomUnit(random);
                var plane = new GeoPlane3(o.ToPoint(), new GeoVector3(n.X, n.Y, n.Z));
                double radius = Between(random, 5, 100);
                double[] gaps = { 0.0, 0.0005, 0.002, 0.3, -0.0005, -0.002, -0.3 };
                Vec at;

                if (i % 3 == 0)
                {
                    // Set up so that the plane stands a gap off the surface: the height of the centre is the radius plus the gap.
                    Vec side = XLine3Gen.RandomUnit(random);

                    side = (side - n * side.Dot(n)).Unit();
                    at = o + side * Between(random, 0, 100) + n * ((radius + gaps[random.Next(gaps.Length)]) * (random.Next(2) == 0 ? 1 : -1));
                }
                else
                {
                    at = XLine3Gen.RandomPoint(random, 150);
                }

                var ball = new GeoSphere3(at.ToPoint(), radius);

                try
                {
                    CheckPlane(ball, plane, ref circles, ref tangents);
                }
                catch (Exception e)
                {
                    failures.Add("#" + i + ": " + e.Message + "\n   ball  " + ball + "\n   plane " + plane.Origin + " " + plane.Normal);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of " + Cases + " balls and planes went wrong:\n" + string.Join("\n", failures.Take(8)));
            Assert.True(circles >= Cases / 10, "only " + circles + " planes cut a circle");
            Assert.True(tangents >= Cases / 100, "only " + tangents + " planes touched at a point");
        }

        private static void CheckPlane(GeoSphere3 ball, GeoPlane3 plane, ref int circles, ref int tangents)
        {
            Vec c = Vec.Of(ball.Center);
            Vec n = Vec.Of(plane.Normal).Unit();
            double height = n.Dot(c - Vec.Of(plane.Origin));
            double gap = Math.Max(0.0, Math.Abs(height) - ball.Radius);

            Assert.True(Math.Abs(ball.DistanceTo(plane) - gap) <= 1E-9 * Math.Max(1.0, Math.Abs(height)), "gap " + ball.DistanceTo(plane) + " against " + gap);
            Assert.True(ball.DistanceTo(plane).Equals(plane.DistanceTo(ball)), "the gap differs from the other side");

            bool collides = ball.CollidesWith(plane, Strict);

            if (Math.Abs(gap - 1E-3) > 1E-6)
            {
                Assert.True(collides == (gap <= 1E-3), "collides " + collides + " at a gap of " + gap);
            }

            Assert.True(collides == plane.CollidesWith(ball, Strict), "the collision differs from the other side");

            GeoLine3 line = ball.GetShortestLineTo(plane, Strict);
            GeoLine3 back = plane.GetShortestLineTo(ball, Strict);

            Assert.True(line.StartPoint.Equals(back.EndPoint) && line.EndPoint.Equals(back.StartPoint), "the shortest line is not the other side's turned round");

            if (gap > 1E-9)
            {
                Assert.True(Math.Abs(Dist(line.StartPoint, ball.Center) - ball.Radius) <= 1E-9 * Math.Max(1.0, ball.Radius), "the line does not start on the surface");
                Assert.True(Math.Abs(n.Dot(Vec.Of(line.EndPoint) - Vec.Of(plane.Origin))) <= 1E-9 * Math.Max(1.0, Math.Abs(height)), "the line does not end on the plane");
                Assert.True(Math.Abs(line.Length - gap) <= 1E-9 * Math.Max(1.0, Math.Abs(height)), "the line is " + line.Length + " long against a gap of " + gap);
            }
            else if (Math.Abs(height) < ball.Radius - 1E-9 * Math.Max(1.0, ball.Radius))
            {
                Assert.True(line.Length == 0.0, "the line of a plane through the ball has a length");
                Assert.True(Math.Abs(n.Dot(Vec.Of(line.StartPoint) - Vec.Of(plane.Origin))) <= 1E-9 * Math.Max(1.0, ball.Radius), "the line of no length is off the plane");
            }

            bool circle = ball.TryGetIntersectionCircle(plane, out GeoCircle3 ours, Strict);
            bool theirCircle = plane.TryGetIntersectionCircle(ball, out GeoCircle3 theirs, Strict);
            bool tangent = ball.TryGetTangentPoint(plane, out GeoPoint3 touch, Strict);
            bool theirTangent = plane.TryGetTangentPoint(ball, out GeoPoint3 theirTouch, Strict);

            Assert.True(circle == theirCircle && tangent == theirTangent, "the two sides disagree on how they meet");
            tangents += tangent ? 1 : 0;
            bool plainCircle = ball.TryGetIntersectionCircle(plane, out GeoCircle3 plain);
            bool plainTangent = ball.TryGetTangentPoint(plane, out GeoPoint3 plainTouch);

            Assert.True(circle == plainCircle && tangent == plainTangent, "the forms without a tolerance disagree");

            if (circle)
            {
                Assert.True(ours.Equals(theirs) && ours.Equals(plain), "the circle differs from the other side");
            }

            if (tangent)
            {
                Assert.True(touch.Equals(theirTouch) && touch.Equals(plainTouch), "the point differs from the other side");
            }

            // The depth of the plane into the surface: a circle where it is more than the point tolerance, a touch where it is within it.
            double depth = ball.Radius - Math.Abs(height);

            if (Math.Abs(depth - 1E-3) <= 4E-3 || Math.Abs(depth + 1E-3) <= 4E-3 || Math.Abs(depth) <= 4E-3)
            {
                return;
            }

            Assert.True(circle == (depth > 0.0), "circle " + circle + " at a depth of " + depth);
            Assert.False(tangent, "a touch where the plane is not within the tolerance of the surface");

            if (!circle)
            {
                return;
            }

            circles++;

            Assert.True(Math.Abs(ours.Radius - Math.Sqrt(depth * (ball.Radius + Math.Abs(height)))) <= 1E-9 * Math.Max(1.0, ball.Radius), "the circle has a radius of " + ours.Radius);
            Assert.True(Dist(ours.Center, (c - n * height).ToPoint()) <= 1E-9 * Math.Max(1.0, ball.Radius), "the circle is not centred on the foot of the centre");
            Assert.True(ours.Normal.IsEqualTo(plane.Normal, new Tolerance(1E-3, 1E-12)), "the circle does not take the normal of the plane");
        }

        [Fact]
        public void APlaneTouchingTheBallWithinTheTolerance_GivesTheFootOfTheCentre_AndNoCircle()
        {
            var random = new Random(Seed + 2);

            for (int i = 0; i < 2000; i++)
            {
                Vec n = XLine3Gen.RandomUnit(random);
                Vec o = XLine3Gen.RandomPoint(random, 100);
                var plane = new GeoPlane3(o.ToPoint(), new GeoVector3(n.X, n.Y, n.Z));
                double radius = Between(random, 5, 100);
                double offset = new[] { 0.0, 0.0005, -0.0005 }[random.Next(3)];
                Vec side = XLine3Gen.RandomUnit(random);

                side = (side - n * side.Dot(n)).Unit();

                Vec c = o + side * Between(random, 0, 100) + n * ((radius + offset) * (random.Next(2) == 0 ? 1 : -1));
                var ball = new GeoSphere3(c.ToPoint(), radius);

                Assert.True(ball.TryGetTangentPoint(plane, out GeoPoint3 touch, Strict), "no touch at an offset of " + offset);
                Assert.False(ball.TryGetIntersectionCircle(plane, out _, Strict));

                double height = n.Dot(c - o);

                Assert.True(Dist(touch, (c - n * height).ToPoint()) <= 1E-9 * Math.Max(1.0, radius), "the touch is not the foot of the centre");
            }
        }
    }
}
