using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ACIS data of a planar region, written as AutoCAD 2021 writes it: the SAT of R2000 to R2010 and the SAB of R2013 and
    /// R2018, compared line for line with what AutoCAD's own <c>DxfOut</c> wrote for the same regions (see
    /// <see cref="AcisAutoCadSamples"/>). A number is compared to nine places and the point AutoCAD gives the plane, which is
    /// any point of it, is left out: AutoCAD's circles carry errors of 1E-16 in their points.
    /// </summary>
    public class AcisWriterTests
    {
        private static readonly AcisVec Up = new AcisVec(0.0, 0.0, 1.0);

        private static AcisVec P(double x, double y) => new AcisVec(x, y, 0.0);

        private static AcisCurve L(double x1, double y1, double x2, double y2) => AcisCurve.Line(P(x1, y1), P(x2, y2));

        private static IReadOnlyList<AcisCurve> Rectangle(double x, double y, double w, double h) =>
            new[] { L(x, y, x + w, y), L(x + w, y, x + w, y + h), L(x + w, y + h, x, y + h), L(x, y + h, x, y) };

        private static IReadOnlyList<AcisCurve> Circle(double x, double y, double radius) =>
            new[] { AcisCurve.Whole(P(x, y), Up, P(radius, 0.0), 1.0) };

        private static readonly IReadOnlyList<IReadOnlyList<AcisCurve>> NoHoles = new IReadOnlyList<AcisCurve>[0];

        private static byte[] Bytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static List<string> Sat(AcisBody body, string version)
        {
            AcisFormat format = AcisFormat.For(version);
            return AcisWriter.Sat(body.Records(format), format);
        }

        /// <summary>A line with every number rounded to nine places and the origin of a plane left out.</summary>
        private static string Normal(string line)
        {
            string[] words = line.TrimEnd().Split(' ');
            var kept = new List<string>();

            // The origin of a plane is the three numbers after its pointers: from the fifth word where it has a history
            // pointer (R2004 and later), else from the third.
            int origin =
                words[0] != "plane-surface" ? -1
                : words.Length > 3 && words[3][0] == '$' ? 4
                : 2;

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];

                if (i >= origin && i < origin + 3 && origin > 0)
                {
                    kept.Add("o");
                }
                else if (
                    word.Length > 0
                    && word[0] != '$'
                    && word[0] != '@'
                    && double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                )
                {
                    kept.Add(Math.Round(value + 0.0, 9).ToString("0.#########", CultureInfo.InvariantCulture));
                }
                else
                {
                    kept.Add(word);
                }
            }

            return string.Join(" ", kept);
        }

        private static void SameAs(string[] expected, IEnumerable<string> actual)
        {
            Assert.Equal(expected.Select(Normal).ToList(), actual.Select(Normal).ToList());
        }

        /// <summary>
        /// As <see cref="SameAs"/>, but a vertex is compared by its point alone: which of its edges AutoCAD names as the owner
        /// of a vertex of a loop of two edges is not the first along the loop, and any edge at the vertex serves.
        /// </summary>
        private static void Loosely(string[] expected, IEnumerable<string> actual)
        {
            string Vertex(string line) =>
                line.StartsWith("vertex ", StringComparison.Ordinal)
                    ? "vertex " + line.TrimEnd(' ', '#').Split(' ').Last()
                    : line;

            Assert.Equal(
                expected.Select(Vertex).Select(Normal).ToList(),
                actual.Select(Vertex).Select(Normal).ToList()
            );
        }

        [Fact]
        public void ARectangle40By30_IsWrittenAsAutoCadWritesItForR2004()
        {
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), NoHoles, 1E-3);

            SameAs(AcisAutoCadSamples.Rect2004, Sat(body, "AC1018"));
        }

        [Fact]
        public void ARectangle40By30_IsWrittenAsAutoCadWritesItForR2000_WithNoHistoryNumbers()
        {
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), NoHoles, 1E-3);

            SameAs(AcisAutoCadSamples.Rect2000, Sat(body, "AC1015"));
        }

        [Fact]
        public void ARectangle40By30_IsWrittenAsAutoCadWritesItForR2010_WithANumberOnEachVertex()
        {
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), NoHoles, 1E-3);

            SameAs(AcisAutoCadSamples.Rect2010, Sat(body, "AC1024"));
        }

        [Fact]
        public void ACircleOfRadius7_IsOneClosedEdgeOfOneVertex()
        {
            AcisBody body = AcisBody.Create(Circle(0, 0, 7), NoHoles, 1E-3);

            SameAs(AcisAutoCadSamples.Circle2010, Sat(body, "AC1024"));
            SameAs(AcisAutoCadSamples.Circle2000, Sat(body, "AC1015"));
        }

        [Fact]
        public void AStadiumOfTwoLinesAndTwoHalfCircles_IsWrittenAsAutoCadWritesIt()
        {
            var stadium = new[]
            {
                L(0, 0, 30, 0),
                AcisCurve.Arc(P(30, 5), Up, P(30, 0), Math.PI),
                L(30, 10, 0, 10),
                AcisCurve.Arc(P(0, 5), Up, P(0, 10), Math.PI),
            };

            SameAs(AcisAutoCadSamples.Stadium2007, Sat(AcisBody.Create(stadium, NoHoles, 1E-3), "AC1021"));
        }

        [Fact]
        public void ARectangleWithACircularHole_HasTheHoleFirstAndRunningClockwise()
        {
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), new[] { Circle(20, 15, 5) }, 1E-3);

            SameAs(AcisAutoCadSamples.RectHole2007, Sat(body, "AC1021"));
            SameAs(AcisAutoCadSamples.RectHole2000, Sat(body, "AC1015"));
        }

        [Fact]
        public void ARectangleWithTwoCircularHoles_IsWrittenAsAutoCadWritesIt()
        {
            AcisBody body = AcisBody.Create(
                Rectangle(0, 0, 40, 30),
                new[] { Circle(30, 15, 3), Circle(10, 15, 4) },
                1E-3
            );

            SameAs(AcisAutoCadSamples.TwoHoles2010, Sat(body, "AC1024"));
        }

        [Fact]
        public void ARectangleWithARectangularHole_TurnsTheHoleClockwise()
        {
            // The hole is given counter-clockwise from (10, 10); AutoCAD runs it clockwise from (18, 10).
            var hole = new[] { L(18, 10, 10, 10), L(10, 10, 10, 16), L(10, 16, 18, 16), L(18, 16, 18, 10) };
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), new IReadOnlyList<AcisCurve>[] { hole }, 1E-3);

            SameAs(AcisAutoCadSamples.RectInRect2004, Sat(body, "AC1018"));
        }

        [Fact]
        public void AnEllipse14By7_IsOneClosedEdge_ItsMajorAxisTurnedOverAsAutoCadWritesIt()
        {
            var ellipse = new[] { AcisCurve.Whole(P(5, 5), Up, P(14, 0), 0.5) };

            SameAs(AcisAutoCadSamples.Ellipse2000, Sat(AcisBody.Create(ellipse, NoHoles, 1E-3), "AC1015"));
        }

        [Fact]
        public void AHalfEllipseAndItsChord_HoldAnArcOfParametersNoughtToPi()
        {
            var half = new[] { L(-10, 0, 10, 0), AcisCurve.Ellipse(P(0, 0), Up, P(10, 0), 0.5, 0.0, Math.PI) };

            Loosely(AcisAutoCadSamples.HalfEllipse2000, Sat(AcisBody.Create(half, NoHoles, 1E-3), "AC1015"));
        }

        [Fact]
        public void ARectangleRunClockwise_IsTurnedToRunCounterClockwiseAboutPlusZ()
        {
            var clockwise = new[] { L(0, 0, 0, 6), L(0, 6, 10, 6), L(10, 6, 10, 0), L(10, 0, 0, 0) };

            AcisBody body = AcisBody.Create(clockwise, NoHoles, 1E-3);

            Assert.Equal(1.0, body.Faces[0].Normal.Z, 12);
            Assert.Equal(60.0, body.Area, 9);
            Assert.Equal(32.0, body.Perimeter, 9);
        }

        [Fact]
        public void ALoopInATiltedPlane_TakesTheNormalTheLoopRunsAbout()
        {
            // A triangle (0, 0, 0), (10, 0, 0), (10, 10, 10): its area is 70.7107 and its normal (0, -0.7071, 0.7071).
            var triangle = new[]
            {
                AcisCurve.Line(new AcisVec(0, 0, 0), new AcisVec(10, 0, 0)),
                AcisCurve.Line(new AcisVec(10, 0, 0), new AcisVec(10, 10, 10)),
                AcisCurve.Line(new AcisVec(10, 10, 10), new AcisVec(0, 0, 0)),
            };

            AcisBody body = AcisBody.Create(triangle, NoHoles, 1E-3);

            Assert.Equal(70.7106781186548, body.Area, 9);
            Assert.Equal(-0.7071067811865476, body.Faces[0].Normal.Y, 9);
            Assert.Equal(0.7071067811865476, body.Faces[0].Normal.Z, 9);
        }

        [Fact]
        public void AnEllipseArcAndAHole_HaveTheAreaAndPerimeterAutoCadGives()
        {
            // AutoCAD: an ellipse 14 by 7 has area 307.8760800517997 and perimeter 67.81913754383379; a circle of radius 7
            // has 153.93804002589985 and 43.982297150257104; the stadium has 378.53981633974485 and 91.41592653589794.
            Assert.Equal(
                307.8760800517997,
                AcisBody.Create(new[] { AcisCurve.Whole(P(5, 5), Up, P(14, 0), 0.5) }, NoHoles, 1E-3).Area,
                9
            );
            Assert.Equal(
                67.81913754383379,
                AcisBody.Create(new[] { AcisCurve.Whole(P(5, 5), Up, P(14, 0), 0.5) }, NoHoles, 1E-3).Perimeter,
                9
            );
            AcisBody stadium = AcisBody.Create(
                new[]
                {
                    L(0, 0, 30, 0),
                    AcisCurve.Arc(P(30, 5), Up, P(30, 0), Math.PI),
                    L(30, 10, 0, 10),
                    AcisCurve.Arc(P(0, 5), Up, P(0, 10), Math.PI),
                },
                NoHoles,
                1E-3
            );
            Assert.Equal(378.53981633974485, stadium.Area, 9);
            Assert.Equal(91.41592653589794, stadium.Perimeter, 9);
        }

        [Fact]
        public void ARectangleWithAHole_HasTheAreaAndPerimeterAutoCadGives()
        {
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), new[] { Circle(20, 15, 5) }, 1E-3);

            Assert.Equal(1121.4601836602551, body.Area, 9);
            Assert.Equal(171.41592653589794, body.Perimeter, 9);
        }

        [Fact]
        public void AnOpenChain_IsRefused()
        {
            var open = new[] { L(0, 0, 10, 0), L(10, 0, 10, 10) };

            Assert.Throws<ArgumentException>(() => AcisBody.Create(open, NoHoles, 1E-3));
        }

        [Fact]
        public void LinesGivenOutOfOrderAndTurned_AreJoinedIntoOneLoop()
        {
            var jumbled = new[] { L(40, 30, 40, 0), L(0, 0, 40, 0), L(40, 30, 0, 30), L(0, 30, 0, 0) };

            AcisBody body = AcisBody.Create(jumbled, NoHoles, 1E-3);

            Assert.Equal(1200.0, body.Area, 9);
            Assert.Equal(4, body.Faces[0].Loops[0].Curves.Count);
        }

        [Theory]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheSabOfR2013AndR2018_ReadsBackAsTheSatOfTheSameRecords(string version)
        {
            AcisFormat format = AcisFormat.For(version);
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), NoHoles, 1E-3);
            List<AcisRecord> records = body.Records(format);

            byte[] sab = AcisWriter.Sab(records, format, "Fri Nov 13 08:05:36 2026");
            string text = DwgSat.ToSat(sab);

            Assert.NotNull(text);
            string[] lines = text.Split('\n').Where(l => l.Length > 0).ToArray();
            Assert.StartsWith(format.Version.ToString(CultureInfo.InvariantCulture) + " 27 2 ", lines[0]);
            SameAs(
                DwgSat
                    .ToSat(Bytes(AcisAutoCadSamples.Rect2018))
                    .Split((char)10)
                    .Where(l => l.Length > 0)
                    .Skip(1)
                    .ToArray(),
                lines.Skip(1)
            );
        }

        [Fact]
        public void TheSabOfARectangleWithAHole_IsTheSabAutoCadWritesForR2018_ByteForByteButForTheSmallestDigits()
        {
            AcisFormat format = AcisFormat.For("AC1032");
            AcisBody body = AcisBody.Create(Rectangle(0, 0, 40, 30), new[] { Circle(20, 15, 5) }, 1E-3);

            byte[] mine = AcisWriter.Sab(body.Records(format), format, "Fri Nov 13 08:05:36 2026");
            byte[] theirs = Bytes(AcisAutoCadSamples.RectHole2018);

            Assert.Equal(theirs.Length, mine.Length);
            Assert.Equal(theirs.Take(31), mine.Take(31));
            Assert.Equal(
                DwgSat.ToSat(theirs).Split('\n').Select(Normal),
                DwgSat.ToSat(mine).Split('\n').Select(Normal)
            );
        }
    }
}
