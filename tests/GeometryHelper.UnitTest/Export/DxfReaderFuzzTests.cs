using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// Random shapes written by <see cref="DxfWriter"/> and read back by <see cref="DxfReader"/>, every number to the bit; and
    /// the numbers themselves, read as a correctly rounding parser reads them on .NET Framework as on .NET 10.
    /// </summary>
    public class DxfReaderFuzzTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // ----- Numbers ---------------------------------------------------------------------------------------------------

        [Fact]
        public void EveryDoubleTheWritersPrint_ReadsBackToItself_AndSoDoesItsSeventeenDigitText()
        {
            var random = new Random(20261022);
            var bytes = new byte[8];
            int checkedCount = 0;

            for (int i = 0; i < 200000; i++)
            {
                double value;

                if (i % 2 == 0)
                {
                    random.NextBytes(bytes);
                    value = BitConverter.ToDouble(bytes, 0);

                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }
                }
                else
                {
                    // The coordinates of a model in millimetres.
                    value = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-3, 8));
                }

                Assert.True(Numbers.TryRead(Numbers.Exact(value), out double back), Numbers.Exact(value));
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(back));
                Assert.True(Numbers.TryRead(value.ToString("G17", CultureInfo.InvariantCulture), out double seventeen));
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(seventeen));
                checkedCount++;
            }

            Assert.True(checkedCount > 199000);
        }

        [Fact]
        public void ANumberHalfWayBetweenTwoDoubles_GoesToTheEvenOne_AndOneAHairPastToTheNearer()
        {
            // 2^53 + 1 lies half way between 2^53 and 2^53 + 2, and 2^53 + 3 half way between 2^53 + 2 and 2^53 + 4.
            Assert.Equal(9007199254740992.0, Read("9007199254740993"));
            Assert.Equal(9007199254740996.0, Read("9007199254740995"));
            Assert.Equal(9007199254740994.0, Read("9007199254740993.0000000000000000000001"));
            Assert.Equal(9007199254740992.0, Read("9007199254740992.9999999999999999999999"));

            // Half the smallest subnormal and below is nought; a hair above is the smallest.
            Assert.Equal(0.0, Read("2.4703282292062327E-324"));
            Assert.Equal(double.Epsilon, Read("2.4703282292062328E-324"));
            Assert.Equal(double.Epsilon, Read("4.9406564584124654e-324"));
            Assert.Equal(0.0, Read("1e-400"));

            // The numbers .NET Framework's parser reads a step off.
            Assert.Equal(BitConverter.DoubleToInt64Bits(4.8721540113315704), BitConverter.DoubleToInt64Bits(Read("4.8721540113315704")));
            Assert.Equal(BitConverter.DoubleToInt64Bits(3.3824149791069402), BitConverter.DoubleToInt64Bits(Read("3.3824149791069402")));
            Assert.Equal(BitConverter.DoubleToInt64Bits(-2.1661333540296802E-08), BitConverter.DoubleToInt64Bits(Read("-2.1661333540296802E-08")));
        }

        [Fact]
        public void TheFormsOfANumber_AreReadAsTheyStand_AndWhatIsNotOne_IsNot()
        {
            Assert.Equal(12.5, Read("  12.5  "));
            Assert.Equal(0.5, Read("+.5"));
            Assert.Equal(5.0, Read("5."));
            Assert.Equal(-1500.0, Read("-1.5E+03"));
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(Read("-0")));
            Assert.Equal(0.1, Read("0.1000000000000000055511151231257827021181583404541015625"));

            foreach (string text in new[] { "NaN", "Infinity", "-Infinity", "1e400", "1,5", "0x10", ".e5", "1e", "1e+", "", "  ", "1.2.3", "12a", "--1" })
            {
                Assert.False(Numbers.TryRead(text, out _), text);
            }
        }

        private static double Read(string text)
        {
            Assert.True(Numbers.TryRead(text, out double value), text);
            return value;
        }

        // ----- Shapes ----------------------------------------------------------------------------------------------------

        [Fact]
        public void RandomShapesOfThePlaneAndOfSpace_ReadBackEveryNumberToTheBit()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var random = new Random(seed * 7919);
                var flat = new List<object>();
                var space = new List<object>();

                for (int i = 0; i < 40; i++)
                {
                    flat.AddRange(Flat(random));
                    space.AddRange(Space(random));
                }

                Check(flat, true);
                Check(space, false);
            }
        }

        private static void Check(List<object> shapes, bool plan)
        {
            var writer = new DxfWriter();

            foreach (object shape in shapes)
            {
                Write(writer, shape);
            }

            DxfDocument read = DxfReader.Parse(writer.ToString(), new DxfReadOptions { PlanAs2D = plan, Tolerance = Tol });

            Assert.Empty(read.Skipped);
            Assert.Equal(shapes.Count, read.Shapes.Count);

            for (int i = 0; i < shapes.Count; i++)
            {
                Assert.Equal(shapes[i].GetType(), read.Shapes[i].Shape.GetType());

                double[] expected = Of(shapes[i]);
                double[] actual = Of(read.Shapes[i].Shape);

                Assert.Equal(expected.Length, actual.Length);

                // A chain or loop with arcs lying flat at one height can carry a normal a hair off up, (1.6E-19, 0, 1) as the
                // library fits its plane, which the writer writes as the extrusion: its numbers then go through the plane's
                // own system and come back to the rounding of that plane, within 1E-12 of the largest coordinate. Every other is to the bit.
                bool rounded = shapes[i] is GeoPolylineArc3 || shapes[i] is GeoPolygonArc3;
                double scale = 1.0 + expected.Max(x => Math.Abs(x));

                for (int k = 0; k < expected.Length; k++)
                {
                    string said = shapes[i].GetType().Name + " " + i + ", number " + k + ": " + expected[k].ToString("R", CultureInfo.InvariantCulture) + " read as " + actual[k].ToString("R", CultureInfo.InvariantCulture);

                    if (rounded)
                    {
                        Assert.True(Math.Abs(expected[k] - actual[k]) <= 1E-12 * scale, said);
                    }
                    else
                    {
                        Assert.True(BitConverter.DoubleToInt64Bits(expected[k] + 0.0) == BitConverter.DoubleToInt64Bits(actual[k]), said);
                    }
                }
            }
        }

        private static void Write(DxfWriter writer, object shape)
        {
            switch (shape)
            {
                case GeoPoint2 s: writer.Add(s); break;
                case GeoLine2 s: writer.Add(s); break;
                case GeoPolyline2 s: writer.Add(s); break;
                case GeoPolygon2 s: writer.Add(s); break;
                case GeoPolylineArc2 s: writer.Add(s); break;
                case GeoPolygonArc2 s: writer.Add(s); break;
                case GeoCircle2 s: writer.Add(s); break;
                case GeoRay2 s: writer.Add(s); break;
                case GeoXLine2 s: writer.Add(s); break;
                case GeoSpline2 s: writer.Add(s); break;
                case GeoPoint3 s: writer.Add(s); break;
                case GeoLine3 s: writer.Add(s); break;
                case GeoPolyline3 s: writer.Add(s); break;
                case GeoPolygon3 s: writer.Add(s); break;
                case GeoPolylineArc3 s: writer.Add(s, Tol); break;
                case GeoPolygonArc3 s: writer.Add(s); break;
                case GeoCircle3 s: writer.Add(s); break;
                case GeoRay3 s: writer.Add(s); break;
                case GeoXLine3 s: writer.Add(s); break;
                case GeoTriangle3 s: writer.Add(s); break;
                case GeoSpline3 s: writer.Add(s); break;
                default: throw new ArgumentException(shape.GetType().Name);
            }
        }

        /// <summary>The numbers of a shape that the file holds as they are, in a fixed order.</summary>
        private static double[] Of(object shape)
        {
            var n = new List<double>();
            void P2(GeoPoint2 p) { n.Add(p.X); n.Add(p.Y); }
            void P3(GeoPoint3 p) { n.Add(p.X); n.Add(p.Y); n.Add(p.Z); }

            switch (shape)
            {
                case GeoPoint2 s: P2(s); break;
                case GeoLine2 s: P2(s.StartPoint); P2(s.EndPoint); break;
                case GeoPolyline2 s: s.Vertices.ToList().ForEach(P2); break;
                case GeoPolygon2 s: s.Vertices.ToList().ForEach(P2); break;
                case GeoPolylineArc2 s: s.Vertices.ToList().ForEach(P2); n.AddRange(s.Bulges.Take(s.VertexCount - 1)); break;
                case GeoPolygonArc2 s: s.Vertices.ToList().ForEach(P2); n.AddRange(s.Bulges); break;
                case GeoCircle2 s: P2(s.Center); n.Add(s.Radius); break;
                case GeoRay2 s: P2(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); break;
                case GeoXLine2 s: P2(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); break;
                case GeoSpline2 s: n.Add(s.Degree); s.ControlPoints.ToList().ForEach(P2); n.AddRange(s.Knots); n.AddRange(s.Weights); break;
                case GeoPoint3 s: P3(s); break;
                case GeoLine3 s: P3(s.StartPoint); P3(s.EndPoint); break;
                case GeoPolyline3 s: s.Vertices.ToList().ForEach(P3); break;
                case GeoPolygon3 s: s.Vertices.ToList().ForEach(P3); break;
                case GeoPolylineArc3 s: s.Vertices.ToList().ForEach(P3); n.AddRange(s.Bulges.Take(s.VertexCount - 1)); break;
                case GeoPolygonArc3 s: s.Vertices.ToList().ForEach(P3); n.AddRange(s.Bulges); break;
                case GeoCircle3 s: P3(s.Center); n.Add(s.Radius); n.Add(s.Normal.X); n.Add(s.Normal.Y); n.Add(s.Normal.Z); break;
                case GeoRay3 s: P3(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); n.Add(s.Direction.Z); break;
                case GeoXLine3 s: P3(s.Origin); n.Add(s.Direction.X); n.Add(s.Direction.Y); n.Add(s.Direction.Z); break;
                case GeoTriangle3 s: P3(s.A); P3(s.B); P3(s.C); break;
                case GeoSpline3 s: n.Add(s.Degree); s.ControlPoints.ToList().ForEach(P3); n.AddRange(s.Knots); n.AddRange(s.Weights); break;
                default: throw new ArgumentException(shape.GetType().Name);
            }

            return n.ToArray();
        }

        private static double Coordinate(Random random) => (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(1, 6));

        private static GeoPoint2 Point2(Random random) => new GeoPoint2(Coordinate(random), Coordinate(random));

        private static GeoPoint3 Point3(Random random) => new GeoPoint3(Coordinate(random), Coordinate(random), Coordinate(random));

        private static double Bulge(Random random) => random.Next(3) == 0 ? 0.0 : (random.Next(2) == 0 ? -1 : 1) * (0.05 + random.NextDouble() * 1.5);

        private static List<GeoPoint2> Points2(Random random, int count) => Enumerable.Range(0, count).Select(_ => Point2(random)).ToList();

        private static IEnumerable<object> Flat(Random random)
        {
            yield return Point2(random);
            yield return new GeoLine2(Point2(random), Point2(random));
            yield return new GeoPolyline2(Points2(random, 2 + random.Next(6)));
            yield return new GeoPolygon2(Points2(random, 3 + random.Next(6)));

            int open = 2 + random.Next(5);
            var openBulges = Enumerable.Range(0, open).Select(_ => Bulge(random)).ToList();
            openBulges[0] = 0.5;
            yield return new GeoPolylineArc2(Points2(random, open), openBulges);

            int closed = 3 + random.Next(5);
            var closedBulges = Enumerable.Range(0, closed).Select(_ => Bulge(random)).ToList();
            closedBulges[closed - 1] = -0.25;
            yield return new GeoPolygonArc2(Points2(random, closed), closedBulges);

            yield return new GeoCircle2(Point2(random), 0.01 + random.NextDouble() * 1000);
            yield return new GeoRay2(Point2(random), new GeoVector2(Coordinate(random), Coordinate(random)));
            yield return new GeoXLine2(Point2(random), new GeoVector2(Coordinate(random), Coordinate(random)));
            yield return Spline2(random);
        }

        private static IEnumerable<object> Space(Random random)
        {
            yield return Point3(random);
            yield return new GeoLine3(Point3(random), Point3(random));
            yield return new GeoPolyline3(Enumerable.Range(0, 2 + random.Next(6)).Select(_ => Point3(random)));

            // A polygon in a plane at random, its corners on the plane to rounding.
            GeoPoint3 origin = Point3(random);
            GeoVector3 u = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize();
            GeoVector3 v = u.GetPerpendicularVector();
            yield return new GeoPolygon3(Enumerable.Range(0, 3 + random.Next(5)).Select(k => origin.Add(u.Multiply(Coordinate(random))).Add(v.Multiply(Coordinate(random)))), Tol);

            // A chain and a loop with arcs at a height, about up, written as lightweight polylines at an elevation.
            double height = Coordinate(random);
            var up = new GeoVector3(0, 0, 1);
            GeoPoint3 At(GeoPoint2 p) => new GeoPoint3(p.X, p.Y, height);
            int open = 3 + random.Next(4);
            var openBulges = Enumerable.Range(0, open).Select(_ => Bulge(random)).ToList();
            openBulges[0] = 0.5;
            yield return new GeoPolylineArc3(Points2(random, open).Select(At), openBulges, openBulges.Select(_ => up), Tol);

            int closed = 3 + random.Next(4);
            var closedBulges = Enumerable.Range(0, closed).Select(_ => Bulge(random)).ToList();
            closedBulges[0] = 0.75;
            yield return new GeoPolygonArc3(Points2(random, closed).Select(At), closedBulges, closedBulges.Select(_ => up), Tol);

            yield return new GeoCircle3(Point3(random), up, 0.01 + random.NextDouble() * 1000);
            yield return new GeoRay3(Point3(random), new GeoVector3(Coordinate(random), Coordinate(random), Coordinate(random)));
            yield return new GeoXLine3(Point3(random), new GeoVector3(Coordinate(random), Coordinate(random), Coordinate(random)));
            yield return new GeoTriangle3(Point3(random), Point3(random), Point3(random));
            yield return Spline3(random);
        }

        private static List<double> Knots(Random random, int degree, int count)
        {
            var knots = Enumerable.Repeat(0.0, degree + 1).ToList();
            knots.AddRange(Enumerable.Range(0, count - degree - 1).Select(_ => random.NextDouble()).OrderBy(k => k));
            knots.AddRange(Enumerable.Repeat(1.0, degree + 1));
            return knots;
        }

        private static List<double> Weights(Random random, int count)
            => random.Next(2) == 0 ? null : Enumerable.Range(0, count).Select(_ => 0.3 + random.NextDouble() * 3).ToList();

        private static GeoSpline2 Spline2(Random random)
        {
            int degree = 1 + random.Next(4);
            int count = degree + 1 + random.Next(5);
            return new GeoSpline2(degree, Points2(random, count), Knots(random, degree, count), Weights(random, count));
        }

        private static GeoSpline3 Spline3(Random random)
        {
            int degree = 1 + random.Next(4);
            int count = degree + 1 + random.Next(5);
            return new GeoSpline3(degree, Enumerable.Range(0, count).Select(_ => Point3(random)), Knots(random, degree, count), Weights(random, count));
        }
    }
}
