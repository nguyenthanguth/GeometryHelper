using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// <see cref="GeoXLine2.ClipTo(GeoRectangle2, Tolerance)"/> against random rectangles, upright and turned, and lines
    /// across them, along their sides at a hair's offset and a hair's tilt, and past their corners, checked against the
    /// distance of the line from the rectangle worked out in closed form.
    /// </summary>
    /// <remarks>
    /// A line that crosses the rectangle is clipped to it exactly: both ends are where the oracle crosses a side, to a
    /// millionth. A line within the point tolerance of it is clipped to the rectangle grown by the tolerance, so its ends
    /// are no more than a tolerance outside it along either axis; one further than the tolerance times the square root of
    /// two is clipped to nothing, and between the two the answer is the tolerance's to give. Whatever it gives, its ends lie
    /// on the line, run the way the line runs, and are never further out than a tolerance, however flat the angle.
    /// </remarks>
    public class XLine2ClipFuzzTests
    {
        private static readonly int Draws = int.TryParse(Environment.GetEnvironmentVariable("XLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 4000;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static double Between(Random random, double lo, double hi) => lo + (hi - lo) * random.NextDouble();

        /// <summary>The distance of a point from a rectangle: nought inside it, and the length of the overshoot outside.</summary>
        private static double Outside(GeoPoint2 p, GeoPoint2 c, double width, double height, double angle, out double overX, out double overY)
        {
            double x = (p.X - c.X) * Math.Cos(angle) + (p.Y - c.Y) * Math.Sin(angle);
            double y = -(p.X - c.X) * Math.Sin(angle) + (p.Y - c.Y) * Math.Cos(angle);

            overX = Math.Max(0, Math.Abs(x) - width / 2);
            overY = Math.Max(0, Math.Abs(y) - height / 2);
            return Math.Sqrt(overX * overX + overY * overY);
        }

        [Fact]
        public void ClippingToARectangle_AgreesWithTheDistanceOfTheLineFromIt_OnLinesAcrossAlongAndPastItsCorners()
        {
            var random = new Random(20261021);
            double tolerance = Strict.EqualPoint;
            int crossed = 0;
            int grazed = 0;
            int missed = 0;

            for (int draw = 0; draw < Draws; draw++)
            {
                var center = P(Between(random, -60, 60), Between(random, -60, 60));
                double width = Between(random, 10, 120);
                double height = Between(random, 10, 120);
                double angle = random.Next(3) == 0 ? Math.PI / 6 : random.Next(3) == 0 ? 0.0 : Between(random, 0, Math.PI);
                var rect = new GeoRectangle2(center, width, height, angle);

                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                GeoPoint2 Corner(double sx, double sy) => P(center.X + sx * width / 2 * cos - sy * height / 2 * sin, center.Y + sx * width / 2 * sin + sy * height / 2 * cos);
                GeoPoint2[] corners = { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) };

                GeoXLine2 line;
                double[] offsets = { 0.0, 0.0005, -0.0005, 0.00099, 0.002, -0.002, 0.05, -1.0 };
                double[] tilts = { 0.0, 1E-14, 1E-10, 1E-6, -1E-6, 1E-3 };

                switch (draw % 3)
                {
                    case 0:
                    {
                        double turn = Between(random, 0, 2 * Math.PI);

                        line = new GeoXLine2(P(center.X + Between(random, -150, 150), center.Y + Between(random, -150, 150)), new GeoVector2(Math.Cos(turn), Math.Sin(turn)));
                        break;
                    }

                    case 1:
                    {
                        // Along a side, a hair off it and a hair tilted, its origin near or a million along.
                        int side = random.Next(4);
                        GeoPoint2 a = corners[side];
                        GeoPoint2 b = corners[(side + 1) % 4];
                        double length = a.DistanceTo(b);
                        var along = new GeoVector2((b.X - a.X) / length, (b.Y - a.Y) / length);
                        var outward = new GeoVector2(along.Y, -along.X);
                        double offset = offsets[random.Next(offsets.Length)];
                        double tilt = tilts[random.Next(tilts.Length)];
                        double at = random.Next(2) == 0 ? Between(random, -150, 150) : Between(random, 1E5, 1E6) * (random.Next(2) == 0 ? 1 : -1);
                        var mid = P((a.X + b.X) / 2 + offset * outward.X + at * along.X, (a.Y + b.Y) / 2 + offset * outward.Y + at * along.Y);

                        line = new GeoXLine2(mid, new GeoVector2(along.X - tilt * outward.X, along.Y - tilt * outward.Y));
                        break;
                    }

                    default:
                    {
                        // Past a corner by a hair, at any slant.
                        GeoPoint2 corner = corners[random.Next(4)];
                        double turn = Between(random, 0, 2 * Math.PI);
                        var direction = new GeoVector2(Math.Cos(turn), Math.Sin(turn));
                        double gap = offsets[random.Next(offsets.Length)];

                        line = new GeoXLine2(P(corner.X - gap * direction.Y, corner.Y + gap * direction.X), direction);
                        break;
                    }
                }

                var shape = new OracleShape();

                for (int i = 0; i < 4; i++)
                {
                    shape.Pieces.Add(new SegmentPiece(corners[i], corners[(i + 1) % 4]));
                }

                double gapToRect = shape.Distance(line);
                GeoLine2? clipped = line.ClipTo(rect, Strict);
                string at2 = "draw " + draw + ": " + line + " against rectangle " + center + " " + width + " by " + height + " at " + angle + ", the gap " + gapToRect + ", gave " + (clipped.HasValue ? clipped.Value.StartPoint + " -> " + clipped.Value.EndPoint : "nothing");

                if (gapToRect > 1.4143 * tolerance)
                {
                    Assert.True(!clipped.HasValue, at2 + ": clipped a line that misses");
                    missed++;
                    continue;
                }

                if (gapToRect > tolerance)
                {
                    continue;
                }

                Assert.True(clipped.HasValue, at2 + ": nothing from a line that touches");

                GeoLine2 result = clipped.Value;
                double diagonal = Math.Sqrt(width * width + height * height);

                foreach (GeoPoint2 end in new[] { result.StartPoint, result.EndPoint })
                {
                    Outside(end, center, width, height, angle, out double overX, out double overY);

                    Assert.True(overX <= tolerance + 1E-9 && overY <= tolerance + 1E-9, at2 + ": an end lies outside by " + overX + ", " + overY);
                    Assert.True(Math.Abs(OraclePiece.Signed(line, end)) <= 1E-7 * (1 + Math.Abs(line.Origin.X) / 1E3 + Math.Abs(line.Origin.Y) / 1E3), at2 + ": an end lies off the line");
                }

                Assert.True(result.Length <= diagonal + 4 * tolerance + 1E-6, at2 + ": longer than the diagonal");
                Assert.True(result.Length == 0.0 || line.Direction.X * (result.EndPoint.X - result.StartPoint.X) + line.Direction.Y * (result.EndPoint.Y - result.StartPoint.Y) > 0, at2 + ": runs against the line");

                // A line through the rectangle is clipped exactly to where it crosses its sides.
                GeoPoint2[] crossings = shape.Crossings(line);

                if (gapToRect == 0.0 && crossings.Length == 2 && crossings[0].DistanceTo(crossings[1]) > 0.01 && !shape.Delicate(line, 0.01))
                {
                    bool inOrder = line.GetDistanceAtPoint(crossings[0]) < line.GetDistanceAtPoint(crossings[1]);
                    GeoPoint2 first = inOrder ? crossings[0] : crossings[1];
                    GeoPoint2 second = inOrder ? crossings[1] : crossings[0];

                    Assert.True(result.StartPoint.DistanceTo(first) <= 1E-6 && result.EndPoint.DistanceTo(second) <= 1E-6, at2 + ": not clipped to the crossings " + first + " and " + second);
                    crossed++;
                }
                else
                {
                    grazed++;
                }
            }

            Assert.True(crossed > Draws / 10 && grazed > Draws / 10 && missed > Draws / 10, "crossed " + crossed + ", grazed " + grazed + ", missed " + missed);
        }

        [Fact]
        public void ALineAlongTheTopOfASquareAHairOffAtAHairsTilt_IsClippedAlongTheSide_NotOffToTheEndsOfTheEarth()
        {
            // The repro that gave (-275, 50.000225): the square 100 by 100 about the origin, the line through (0, 50.0005)
            // along (1, 1E-6). It runs 0.0005 above the top side all the way, so it touches it, and is clipped to its length.
            var square = new GeoRectangle2(P(0, 0), 100, 100);

            foreach (double slope in new[] { 1E-6, 1E-10, 1E-14, -1E-6, -1E-10 })
            {
                GeoLine2? clipped = new GeoXLine2(P(0, 50.0005), new GeoVector2(1, slope)).ClipTo(square, Strict);

                Assert.True(clipped.HasValue, "slope " + slope);
                Assert.InRange(clipped.Value.StartPoint.X, -50.0011, -49.999);
                Assert.InRange(clipped.Value.EndPoint.X, 49.999, 50.0011);
                Assert.InRange(clipped.Value.StartPoint.Y, 50.0004, 50.0006);
            }
        }

        [Fact]
        public void ARotatedRectangleClippedAHairOffASide_IsClippedAlongIt()
        {
            // At a sixth of a turn, a line along the top side, 0.0005 outside it: clipped to the side, both ways of offset.
            var rectangle = new GeoRectangle2(P(0, 0), 100, 40, Math.PI / 6);
            var along = new GeoVector2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
            var up = new GeoVector2(-along.Y, along.X);

            foreach (double offset in new[] { 20.0005, -20.0005, 19.9995 })
            {
                GeoLine2? clipped = new GeoXLine2(P(offset * up.X - 300 * along.X, offset * up.Y - 300 * along.Y), along).ClipTo(rectangle, Strict);

                Assert.True(clipped.HasValue, "offset " + offset);
                Assert.InRange(clipped.Value.Length, 99.99, 100.01);
            }
        }
    }
}
