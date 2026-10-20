using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The tree over the boxes of a spline's pieces: that it reaches exactly the pieces a plain scan would, that the point queries it
    /// speeds up answer to the bit what they answered before it, and that it holds at a hundred thousand spans.
    /// </summary>
    public class Spline2IndexTests
    {
        private struct Nearest : IVisit
        {
            internal double Reach;
            internal int Best;

            public double Limit => Reach;

            public void Piece(int piece, double bound)
            {
                if (bound < Reach || (bound == Reach && piece < Best))
                {
                    Reach = bound;
                    Best = piece;
                }
            }
        }

        private struct AllPairs : IPairVisit
        {
            internal double Reach;
            internal List<long> Found;

            public double Limit => Reach;

            public void Pair(int first, int second) => Found.Add(((long)first << 32) | (uint)second);
        }

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        private static GeoSpline2 Wander(Random r, int spans, int degree, double size)
        {
            var points = new List<GeoPoint2>();
            double x = 0.0;
            double y = 0.0;

            for (int i = 0; i < spans + degree; i++)
            {
                x += Between(r, -size, size);
                y += Between(r, -size, size);
                points.Add(new GeoPoint2(x, y));
            }

            return GeoSpline2.FromControlPoints(degree, points);
        }

        [Fact]
        public void ThePiecesTheTreeReaches_AreExactlyThoseWithinTheLimit_ForPointsNearAndFar()
        {
            var random = new Random(61000);

            for (int draw = 0; draw < 60; draw++)
            {
                GeoSpline2 spline = Wander(random, 1 + random.Next(draw % 5 == 0 ? 3000 : 60), 1 + random.Next(4), 10.0);
                NurbsCurve curve = spline.Curve;
                BezierPieces pieces = curve.Pieces;
                PieceIndex index = curve.Index;

                for (int k = 0; k < 20; k++)
                {
                    GeoPoint2 on = spline.GetPointAtParameter(random.NextDouble());
                    var query = new[] { on.X + Between(random, -300, 300) * (k % 3), on.Y + Between(random, -300, 300) * (k % 3) };
                    var region = new PointBound(query);
                    double limit = new[] { 0.0, 1.0, 10.0, 100.0, 1E6 }[random.Next(5)];

                    var found = new List<int>();
                    index.Collect(ref region, limit, found);
                    found.Sort();
                    var expected = Enumerable.Range(0, pieces.Count).Where(i => region.Bound(pieces.BoxMin, pieces.BoxMax, i * 2) <= limit).ToList();

                    Assert.Equal(expected, found);

                    // The nearest piece by its box, the lowest where several are as near.
                    var nearest = new Nearest { Reach = double.PositiveInfinity, Best = 0 };
                    index.Search(ref region, ref nearest);
                    int lowest = 0;
                    double least = double.PositiveInfinity;

                    for (int i = 0; i < pieces.Count; i++)
                    {
                        double bound = region.Bound(pieces.BoxMin, pieces.BoxMax, i * 2);

                        if (bound < least)
                        {
                            least = bound;
                            lowest = i;
                        }
                    }

                    Assert.Equal(lowest, nearest.Best);
                }
            }
        }

        [Fact]
        public void ThePairsOfPiecesTheTwoTreesReach_AreExactlyThoseWithinTheLimit()
        {
            var random = new Random(61100);

            for (int draw = 0; draw < 30; draw++)
            {
                GeoSpline2 a = Wander(random, 1 + random.Next(draw % 4 == 0 ? 400 : 30), 1 + random.Next(4), 10.0);
                GeoSpline2 b = Wander(random, 1 + random.Next(draw % 4 == 0 ? 400 : 30), 1 + random.Next(4), 10.0);
                BezierPieces pa = a.Curve.Pieces;
                BezierPieces pb = b.Curve.Pieces;

                foreach (double limit in new[] { 0.0, 0.5, 5.0, 40.0 })
                {
                    var visit = new AllPairs { Reach = limit, Found = new List<long>() };
                    a.Curve.Index.Pairs(b.Curve.Index, ref visit);
                    visit.Found.Sort();
                    var expected = new List<long>();

                    for (int i = 0; i < pa.Count; i++)
                    {
                        for (int j = 0; j < pb.Count; j++)
                        {
                            double gx = Math.Max(0.0, Math.Max(pb.BoxMin[2 * j] - pa.BoxMax[2 * i], pa.BoxMin[2 * i] - pb.BoxMax[2 * j]));
                            double gy = Math.Max(0.0, Math.Max(pb.BoxMin[2 * j + 1] - pa.BoxMax[2 * i + 1], pa.BoxMin[2 * i + 1] - pb.BoxMax[2 * j + 1]));

                            if (Math.Sqrt(gx * gx + gy * gy) <= limit)
                            {
                                expected.Add(((long)i << 32) | (uint)j);
                            }
                        }
                    }

                    Assert.Equal(expected, visit.Found);
                }
            }
        }

        /// <summary>
        /// The point queries of 120 random splines in the plane and 60 in space, of up to 400 spans each, every double as its bits, hashed.
        /// The hash was taken from the build before the tree, with the ends of a rational spline exact (81e5f86), on the same draws; the tree
        /// must not move one bit.
        /// </summary>
        [Fact]
        public void ThePointQueriesOfEverySpline_AreBitForBitWhatTheyWereBeforeTheTree()
        {
            Assert.Equal("d45f362cf40821c4c4953a52d47b7f42fb13b661fccfbda23e304d53ddc35a11", Hash(Record(120) + RecordInSpace(60)));
        }

        /// <summary>
        /// The same queries of 120 splines in the plane and 60 in space that all have more than 24 spans, up to 425, where the tree is walked
        /// and not the boxes looked at one by one: in the draws above most have a dozen spans or fewer, and take the plain scan. The hash was
        /// taken from the build at 81e5f86, which has no tree, on the same draws; the tree must not move one bit of what it answers.
        /// </summary>
        [Fact]
        public void ThePointQueriesOfSplinesOfMoreThanTwentyFourSpans_AreBitForBitWhatTheyWereBeforeTheTree()
        {
            Assert.Equal("b44dea7db5f9f1ac9f2882b748f3043602355317478713d6103b82cdd146647b", Hash(Record(120, 25, 23456) + RecordInSpace(60, 25, 65432)));
        }

        private static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
            }
        }

        private static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("x");

        private static int Run(List<double> knots)
        {
            int run = 1;

            for (int i = knots.Count - 1; i > 0 && knots[i] == knots[i - 1]; i--)
            {
                run++;
            }

            return run;
        }

        private static double Round(double value, double scale) => Math.Floor(value * scale + 0.5) / scale;

        private static GeoSpline2 Draw(Random r, int spans, int degree, bool rational, double size, bool multiples)
        {
            int count = degree + spans;
            var points = new List<GeoPoint2>();
            double x = Between(r, -size, size);
            double y = Between(r, -size, size);

            for (int i = 0; i < count; i++)
            {
                x += Between(r, -size, size) * 0.1;
                y += Between(r, -size, size) * 0.1;
                points.Add(new GeoPoint2(Round(x, 10000.0), Round(y, 10000.0)));
            }

            var knots = new List<double>();

            for (int i = 0; i <= degree; i++)
            {
                knots.Add(0.0);
            }

            int inner = count - degree - 1;
            double at = 0.0;

            for (int i = 0; i < inner; i++)
            {
                if (!(multiples && i > 0 && r.Next(4) == 0 && Run(knots) < degree))
                {
                    at += Between(r, 0.2, 1.0);
                }

                knots.Add(at);
            }

            double end = at + Between(r, 0.2, 1.0);

            for (int i = 0; i <= degree; i++)
            {
                knots.Add(end);
            }

            double[] weights = null;

            if (rational)
            {
                weights = new double[count];

                for (int i = 0; i < count; i++)
                {
                    weights[i] = Round(Between(r, 0.3, 4.0), 1000.0);
                }
            }

            return new GeoSpline2(degree, points, knots, weights);
        }

        private static string Record(int seeds, int spansFloor = 0, int seed = 12345)
        {
            var text = new StringBuilder();
            var random = new Random(seed);

            for (int s = 0; s < seeds; s++)
            {
                int degree = 1 + random.Next(5);
                int spans = spansFloor + 1 + random.Next(s % 10 == 0 ? 400 : 12);
                double size = new[] { 10.0, 100.0, 1000.0 }[random.Next(3)];
                GeoSpline2 spline;

                try
                {
                    spline = Draw(random, spans, degree, random.Next(2) == 0, size, random.Next(2) == 0);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                for (int q = 0; q < 40; q++)
                {
                    double reach = size * 2.5;
                    GeoPoint2 p;

                    switch (q % 4)
                    {
                        case 0:
                            p = new GeoPoint2(Between(random, -reach, reach), Between(random, -reach, reach));
                            break;
                        case 1:
                            p = spline.GetPointAtParameter(random.NextDouble());
                            break;
                        case 2:
                        {
                            GeoPoint2 b = spline.GetPointAtParameter(random.NextDouble());
                            p = new GeoPoint2(b.X + Between(random, -0.01, 0.01), b.Y + Between(random, -0.01, 0.01));
                            break;
                        }

                        default:
                            p = spline.ControlPoints[random.Next(spline.ControlPointCount)];
                            break;
                    }

                    double d = spline.DistanceTo(p);
                    double k = spline.GetKnotAtPoint(p);
                    GeoPoint2 c = spline.GetClosestPointOnBoundary(p);
                    GeoLine2 line = spline.GetShortestLineTo(p);
                    text.Append(s).Append(' ').Append(q).Append(' ').Append(Bits(d)).Append(' ').Append(Bits(k)).Append(' ')
                        .Append(Bits(c.X)).Append(' ').Append(Bits(c.Y)).Append(' ').Append(Bits(line.StartPoint.X)).Append(' ').Append(Bits(line.EndPoint.Y)).Append('\n');
                }
            }

            return text.ToString();
        }

        private static string RecordInSpace(int seeds, int spansFloor = 0, int seed = 54321)
        {
            var text = new StringBuilder();
            var random = new Random(seed);

            for (int s = 0; s < seeds; s++)
            {
                int degree = 1 + random.Next(5);
                int spans = spansFloor + 1 + random.Next(s % 10 == 0 ? 400 : 12);
                double size = new[] { 10.0, 100.0, 1000.0 }[random.Next(3)];
                bool rational = random.Next(2) == 0;
                bool multiples = random.Next(2) == 0;
                int count = degree + spans;
                var points = new List<GeoPoint3>();
                double x = Between(random, -size, size);
                double y = Between(random, -size, size);
                double z = Between(random, -size, size);

                for (int i = 0; i < count; i++)
                {
                    x += Between(random, -size, size) * 0.1;
                    y += Between(random, -size, size) * 0.1;
                    z += Between(random, -size, size) * 0.1;
                    points.Add(new GeoPoint3(Round(x, 10000.0), Round(y, 10000.0), Round(z, 10000.0)));
                }

                var knots = new List<double>();

                for (int i = 0; i <= degree; i++)
                {
                    knots.Add(0.0);
                }

                int inner = count - degree - 1;
                double at = 0.0;

                for (int i = 0; i < inner; i++)
                {
                    if (!(multiples && i > 0 && random.Next(4) == 0 && Run(knots) < degree))
                    {
                        at += Between(random, 0.2, 1.0);
                    }

                    knots.Add(at);
                }

                double end = at + Between(random, 0.2, 1.0);

                for (int i = 0; i <= degree; i++)
                {
                    knots.Add(end);
                }

                double[] weights = null;

                if (rational)
                {
                    weights = new double[count];

                    for (int i = 0; i < count; i++)
                    {
                        weights[i] = Round(Between(random, 0.3, 4.0), 1000.0);
                    }
                }

                GeoSpline3 spline;

                try
                {
                    spline = new GeoSpline3(degree, points, knots, weights);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                for (int q = 0; q < 40; q++)
                {
                    double reach = size * 2.5;
                    GeoPoint3 p;

                    switch (q % 4)
                    {
                        case 0:
                            p = new GeoPoint3(Between(random, -reach, reach), Between(random, -reach, reach), Between(random, -reach, reach));
                            break;
                        case 1:
                            p = spline.GetPointAtParameter(random.NextDouble());
                            break;
                        case 2:
                        {
                            GeoPoint3 b = spline.GetPointAtParameter(random.NextDouble());
                            p = new GeoPoint3(b.X + Between(random, -0.01, 0.01), b.Y + Between(random, -0.01, 0.01), b.Z + Between(random, -0.01, 0.01));
                            break;
                        }

                        default:
                            p = spline.ControlPoints[random.Next(spline.ControlPointCount)];
                            break;
                    }

                    double d = spline.DistanceTo(p);
                    double k = spline.GetKnotAtPoint(p);
                    GeoPoint3 c = spline.GetClosestPointOnBoundary(p);
                    GeoLine3 line = spline.GetShortestLineTo(p);
                    text.Append("3 ").Append(s).Append(' ').Append(q).Append(' ').Append(Bits(d)).Append(' ').Append(Bits(k)).Append(' ')
                        .Append(Bits(c.X)).Append(' ').Append(Bits(c.Y)).Append(' ').Append(Bits(c.Z)).Append(' ').Append(Bits(line.StartPoint.X)).Append(' ').Append(Bits(line.EndPoint.Z)).Append('\n');
                }
            }

            return text.ToString();
        }

        [Fact]
        public void AHundredThousandSpans_AnswerAPointQueryAsAScanOfTheirSegmentsDoes()
        {
            // A polyline of 100 000 segments as a spline of degree one: the oracle is a scan over the segments, with nothing of the spline in it.
            var random = new Random(61200);
            var vertices = new List<GeoPoint2>();
            double angle = 0.0;
            double x = 0.0;
            double y = 0.0;

            for (int i = 0; i <= 100000; i++)
            {
                vertices.Add(new GeoPoint2(x, y));
                angle += Between(random, -0.2, 0.2);
                x += Math.Cos(angle) * 0.5 + 0.2;
                y += Math.Sin(angle) * 0.5;
            }

            var spline = GeoSpline2.FromPolyline(new GeoPolyline2(vertices));
            Assert.Equal(100000, spline.ControlPointCount - 1);

            for (int k = 0; k < 120; k++)
            {
                GeoPoint2 on = vertices[random.Next(vertices.Count)];
                double reach = new[] { 0.0, 0.01, 1.0, 50.0, 5000.0 }[k % 5];
                var q = new GeoPoint2(on.X + Between(random, -reach, reach), on.Y + Between(random, -reach, reach));

                double least = double.MaxValue;

                for (int i = 0; i + 1 < vertices.Count; i++)
                {
                    double dx = vertices[i + 1].X - vertices[i].X;
                    double dy = vertices[i + 1].Y - vertices[i].Y;
                    double t = Math.Max(0.0, Math.Min(1.0, ((q.X - vertices[i].X) * dx + (q.Y - vertices[i].Y) * dy) / (dx * dx + dy * dy)));
                    double ex = q.X - (vertices[i].X + t * dx);
                    double ey = q.Y - (vertices[i].Y + t * dy);
                    least = Math.Min(least, Math.Sqrt(ex * ex + ey * ey));
                }

                Assert.True(Math.Abs(spline.DistanceTo(q) - least) <= 1E-9 * (1.0 + least), "query " + k + ": " + spline.DistanceTo(q).ToString("R") + " against " + least.ToString("R"));
            }
        }
    }
}
