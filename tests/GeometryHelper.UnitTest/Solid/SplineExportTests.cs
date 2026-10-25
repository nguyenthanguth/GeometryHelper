using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GeometryHelper;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A spline in the writers (SPL-6): the SPLINE entity of its own knots, weights and control points in DXF, the Bézier
    /// curves it is made of in SVG where SVG has them, and chords no further from it than the tolerance asked for in SVG
    /// otherwise, in OBJ and in WKT.
    /// </summary>
    public class SplineExportTests
    {
        private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";

        private static readonly Tolerance Tol = Tolerance.Default;

        // .NET Framework's parser can land a step away from the double a text is; this one does not.
        private static double Parse(string text) => ExportTests.ParseExactly(text);

        // The cubic arch of one Bézier piece: it rises to 112.5 at (150, 112.5), its control points to 150.
        private static GeoSpline2 Arch() => new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

        // A cubic of four pieces over a domain that is not [0, 1], with knots that do not print short.
        private static GeoSpline2 Wave() => new GeoSpline2(
            3,
            new[] { new GeoPoint2(10.125, -3.5), new GeoPoint2(60, 80.25), new GeoPoint2(120.5, -40), new GeoPoint2(180, 95.75), new GeoPoint2(240.25, -10), new GeoPoint2(300, 60), new GeoPoint2(355.5, 5) },
            new[] { 2.0, 2.0, 2.0, 2.0, 2.0 + 1.0 / 3.0, 3.1, 4.0 - 1E-7, 5.0, 5.0, 5.0, 5.0 });

        private static GeoSpline2 Circle() => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 100));

        // ----- A DXF file is pairs of lines, a group code and its value -----------------------------------------------

        private sealed class Entity
        {
            public string Type { get; set; }

            public List<KeyValuePair<int, string>> Pairs { get; } = new List<KeyValuePair<int, string>>();

            public string Text(int code) => Pairs.First(p => p.Key == code).Value;

            public double Number(int code) => Parse(Text(code));

            public bool Has(int code) => Pairs.Any(p => p.Key == code);

            public List<double> All(int code) => Pairs.Where(p => p.Key == code).Select(p => Parse(p.Value)).ToList();

            public List<GeoPoint3> ControlPoints()
            {
                List<double> x = All(10);
                List<double> y = All(20);
                List<double> z = All(30);
                return Enumerable.Range(0, x.Count).Select(i => new GeoPoint3(x[i], y[i], z[i])).ToList();
            }

            public IEnumerable<string> Markers => Pairs.Where(p => p.Key == 100).Select(p => p.Value);
        }

        private static List<Entity> EntitiesOf(DxfWriter writer)
        {
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');
            var entities = new List<Entity>();
            bool inside = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "ENDSEC")
                {
                    inside = false;
                }
                else if (code == 2 && i > 0 && lines[i - 2].Trim() == "0" && lines[i - 1] == "SECTION")
                {
                    inside = value == "ENTITIES";
                }
                else if (inside && code == 0)
                {
                    entities.Add(new Entity { Type = value });
                }
                else if (inside)
                {
                    entities[entities.Count - 1].Pairs.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            return entities;
        }

        private static GeoPoint3 Header(DxfWriter writer, string name)
        {
            string[] lines = writer.ToString().Split('\n');
            int at = Array.IndexOf(lines, name);
            return new GeoPoint3(Parse(lines[at + 2]), Parse(lines[at + 4]), Parse(lines[at + 6]));
        }

        private static void SameBits(IEnumerable<double> expected, IEnumerable<double> actual)
        {
            Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v + 0.0)).ToList(), actual.Select(v => BitConverter.DoubleToInt64Bits(v + 0.0)).ToList());
        }

        // ----- DXF -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ACubicInThePlane_IsASplineOfItsDegreeKnotsAndControlPoints_FlaggedPlanar()
        {
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(Arch(), "curves")));

            Assert.Equal("SPLINE", entity.Type);
            Assert.Equal(new[] { "AcDbEntity", "AcDbSpline" }, entity.Markers);
            Assert.Equal("curves", entity.Text(8));
            Assert.Equal(new GeoPoint3(0, 0, 1), new GeoPoint3(entity.Number(210), entity.Number(220), entity.Number(230)));
            Assert.Equal(8, (int)entity.Number(70));
            Assert.Equal(3, (int)entity.Number(71));
            Assert.Equal(8, (int)entity.Number(72));
            Assert.Equal(4, (int)entity.Number(73));
            Assert.Equal(0, (int)entity.Number(74));
            Assert.Equal(new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }, entity.All(40));
            Assert.False(entity.Has(41));
            Assert.Equal(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 150, 0), new GeoPoint3(200, 150, 0), new GeoPoint3(300, 0, 0) }, entity.ControlPoints());
            Assert.False(entity.Has(11));
        }

        [Fact]
        public void TheGroupsComeInTheOrderOfTheDxfReference()
        {
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(Circle())));
            List<int> codes = entity.Pairs.Select(p => p.Key).SkipWhile(code => code != 210).ToList();

            // Extrusion, flags, degree, the three counts, the three tolerances, then every knot, every weight and every control point.
            var expected = new List<int> { 210, 220, 230, 70, 71, 72, 73, 74, 42, 43, 44 };
            expected.AddRange(Enumerable.Repeat(40, 12));
            expected.AddRange(Enumerable.Repeat(41, 9));

            for (int i = 0; i < 9; i++)
            {
                expected.AddRange(new[] { 10, 20, 30 });
            }

            Assert.Equal(expected, codes);
            Assert.Equal(1E-10, entity.Number(42));
            Assert.Equal(1E-10, entity.Number(43));
            Assert.Equal(1E-10, entity.Number(44));
        }

        [Fact]
        public void ACircle_IsARationalClosedSpline_ItsWeightsWrittenToTheBit()
        {
            GeoSpline2 circle = Circle();
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(circle)));

            // A whole turn ends on its start exactly, so the circle is flagged closed.
            Assert.True(circle.StartPoint.Equals(circle.EndPoint));
            Assert.Equal(1 | 4 | 8, (int)entity.Number(70));
            Assert.Equal(2, (int)entity.Number(71));
            Assert.Equal(12, (int)entity.Number(72));
            Assert.Equal(9, (int)entity.Number(73));
            SameBits(circle.Knots, entity.All(40));
            SameBits(circle.Weights, entity.All(41));
            SameBits(circle.ControlPoints.SelectMany(p => new[] { p.X, p.Y, 0.0 }), entity.ControlPoints().SelectMany(p => new[] { p.X, p.Y, p.Z }));
        }

        [Fact]
        public void ASplineIsFlaggedClosedOnlyWhereItsEndsAreOnePointExactly_NotWhereTheyAreWithinTheTolerance()
        {
            GeoPoint2[] loop = { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100), new GeoPoint2(0, 0) };
            var closed = new GeoSpline2(2, loop, new double[] { 0, 0, 0, 1, 2, 3, 3, 3 });
            loop[4] = new GeoPoint2(0, 1E-6);
            var near = new GeoSpline2(2, loop, new double[] { 0, 0, 0, 1, 2, 3, 3, 3 });

            Assert.True(near.IsClosed(Tol));
            Assert.Equal(1 | 8, (int)Assert.Single(EntitiesOf(new DxfWriter().Add(closed))).Number(70));
            Assert.Equal(8, (int)Assert.Single(EntitiesOf(new DxfWriter().Add(near))).Number(70));

            var space = new GeoSpline3(2, closed.ControlPoints.Select(p => new GeoPoint3(p.X, p.Y, p.X * p.Y / 100.0)), closed.Knots);
            Assert.Equal(1, (int)Assert.Single(EntitiesOf(new DxfWriter().Add(space))).Number(70));
        }

        [Fact]
        public void AWholeTurn_EndsOnItsStartExactly_InThePlaneAndInSpace_WhereverItStarts()
        {
            var random = new Random(31);

            for (int i = 0; i < 200; i++)
            {
                double R(double s) => (random.NextDouble() * 2.0 - 1.0) * s;
                double start = R(10.0);
                var flat = GeoSpline2.FromArc(new GeoArc2(new GeoPoint2(R(1E5), R(1E5)), 1.0 + random.NextDouble() * 1000.0, start, start));
                var space = GeoSpline3.FromArc(new GeoArc3(new GeoPoint3(R(1E5), R(1E5), R(1E5)), new GeoVector3(R(1.0), R(1.0), 1.5), 1.0 + random.NextDouble() * 1000.0, start, start));
                var round = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(R(1E5), R(1E5), R(1E5)), new GeoVector3(R(1.0), 1.5, R(1.0)), 1.0 + random.NextDouble() * 1000.0));

                Assert.True(flat.StartPoint.Equals(flat.EndPoint), flat.StartPoint + " " + flat.EndPoint);
                Assert.True(space.StartPoint.Equals(space.EndPoint), space.StartPoint + " " + space.EndPoint);
                Assert.True(round.StartPoint.Equals(round.EndPoint), round.StartPoint + " " + round.EndPoint);
            }

            // An arc short of a whole turn ends where it ends.
            var most = GeoSpline2.FromArc(new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, 6.0));
            Assert.False(most.StartPoint.Equals(most.EndPoint));
        }

        [Fact]
        public void AnOpenSpline_IsNotFlaggedClosed_NorRational()
        {
            Assert.Equal(8, (int)Assert.Single(EntitiesOf(new DxfWriter().Add(Wave()))).Number(70));
        }

        [Fact]
        public void EveryNumberOfASplineReadsBackToTheBit_InThePlaneAndInSpace()
        {
            var random = new Random(20261021);

            for (int round = 0; round < 200; round++)
            {
                int degree = 1 + random.Next(5);
                int count = degree + 1 + random.Next(6);
                double R() => (random.NextDouble() * 2.0 - 1.0) * 1E5 / 3.0;
                var knots = new List<double>();
                double start = R();

                for (int i = 0; i <= degree; i++)
                {
                    knots.Add(start);
                }

                double at = start;

                for (int i = 0; i < count - degree - 1; i++)
                {
                    at += random.NextDouble() + 1E-3;
                    knots.Add(at);
                }

                at += random.NextDouble() + 1E-3;

                for (int i = 0; i <= degree; i++)
                {
                    knots.Add(at);
                }

                double[] weights = round % 2 == 0 ? null : Enumerable.Range(0, count).Select(i => 0.25 + random.NextDouble() * 3.0).ToArray();
                var flat = new GeoSpline2(degree, Enumerable.Range(0, count).Select(i => new GeoPoint2(R(), R())), knots, weights);
                var space = new GeoSpline3(degree, Enumerable.Range(0, count).Select(i => new GeoPoint3(R(), R(), R())), knots, weights);

                Entity a = Assert.Single(EntitiesOf(new DxfWriter().Add(flat)));
                Entity b = Assert.Single(EntitiesOf(new DxfWriter().Add(space)));

                SameBits(flat.Knots, a.All(40));
                SameBits(space.Knots, b.All(40));
                SameBits(flat.IsRational ? flat.Weights : new double[0], a.All(41));
                SameBits(space.IsRational ? space.Weights : new double[0], b.All(41));
                SameBits(flat.ControlPoints.SelectMany(p => new[] { p.X, p.Y, 0.0 }), a.ControlPoints().SelectMany(p => new[] { p.X, p.Y, p.Z }));
                SameBits(space.ControlPoints.SelectMany(p => new[] { p.X, p.Y, p.Z }), b.ControlPoints().SelectMany(p => new[] { p.X, p.Y, p.Z }));
                Assert.Equal(degree, (int)b.Number(71));
            }
        }

        [Fact]
        public void ASplineInATiltedPlane_IsPlanar_ItsNormalTurnedUpAsTheExtrusion()
        {
            // The circle of radius 25 on the normal (-1, -1, 0) is written on (0.7071, 0.7071, 0), as an arc on it would be.
            var circle = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(-1, -1, 0), 25));
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(circle)));
            var extrusion = new GeoVector3(entity.Number(210), entity.Number(220), entity.Number(230));

            Assert.Equal(1 | 4 | 8, (int)entity.Number(70));
            Assert.True(extrusion.IsEqualTo(new GeoVector3(1, 1, 0).Normalize(), new Tolerance(1E-12, 1E-12, 1E-12, 1E-12)), extrusion.ToString());

            // Control points in world coordinates, as they are.
            SameBits(circle.ControlPoints.SelectMany(p => new[] { p.X, p.Y, p.Z }), entity.ControlPoints().SelectMany(p => new[] { p.X, p.Y, p.Z }));
        }

        [Fact]
        public void ASplineOutOfOnePlane_IsNotPlanar_AndHasNoExtrusion()
        {
            var bent = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(100, 100, 100) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(bent)));

            Assert.Equal(0, (int)entity.Number(70));
            Assert.False(entity.Has(210));
        }

        [Fact]
        public void ASplineGrowsTheExtentsOfTheDrawingByTheBoxItFills_NotByItsControlPoints()
        {
            DxfWriter writer = new DxfWriter().Add(Arch());

            Assert.Equal(new GeoPoint3(0, 0, 0), Header(writer, "$EXTMIN"));
            Assert.True(Header(writer, "$EXTMAX").DistanceTo(new GeoPoint3(300, 112.5, 0)) < 1E-9, Header(writer, "$EXTMAX").ToString());

            var lifted = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 155), new GeoPoint3(200, 0, 155), new GeoPoint3(300, 0, 5) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            DxfWriter space = new DxfWriter().Add(lifted);

            Assert.True(Header(space, "$EXTMAX").DistanceTo(new GeoPoint3(300, 0, 117.5)) < 1E-9, Header(space, "$EXTMAX").ToString());
        }

        [Fact]
        public void TheOldShapesAreWrittenAsTheyWereWhenASplineIsAddedAfterThem()
        {
            Func<DxfWriter> old = () => new DxfWriter()
                .Add(new GeoArc2(new GeoPoint2(1, 2), 30, 0.5, 2.0), "arcs")
                .Add(new GeoLine3(GeoPoint3.Origin, new GeoPoint3(1, 2, 3)))
                .Add(new GeoEllipse2(new GeoPoint2(5, 5), new GeoVector2(1, 1), 40, 20));

            List<Entity> before = EntitiesOf(old());
            List<Entity> after = EntitiesOf(old().Add(Arch()).Add(GeoSpline3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10))));

            Assert.Equal(before.Count + 2, after.Count);

            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Type, after[i].Type);
                Assert.Equal(before[i].Pairs, after[i].Pairs);
            }

            Assert.Equal(new[] { "SPLINE", "SPLINE" }, after.Skip(before.Count).Select(e => e.Type));
        }

        [Fact]
        public void ANullSplineIsRefusedBeforeAnythingIsWritten()
        {
            var writer = new DxfWriter().Add(new GeoPoint2(1, 1));
            string before = writer.ToString();

            Assert.Throws<ArgumentNullException>(() => writer.Add((GeoSpline2)null));
            Assert.Throws<ArgumentNullException>(() => writer.Add((GeoSpline3)null, "new"));
            Assert.Equal(before, writer.ToString());
        }

        // ----- SVG -----------------------------------------------------------------------------------------------------

        private static string PathOf(SvgWriter writer, string colour)
        {
            XElement path = XDocument.Parse(writer.ToString()).Descendants(Ns + "path").Single(p => (string)p.Attribute("stroke") == colour);
            return (string)path.Attribute("d");
        }

        /// <summary>
        /// The Bézier pieces of a spline, by cutting it at each of its inner knots: each piece is then clamped at both ends and has
        /// degree + 1 control points.
        /// </summary>
        private static List<GeoSpline2> Pieces(GeoSpline2 spline)
        {
            var pieces = new List<GeoSpline2>();
            GeoSpline2 rest = spline;

            foreach (double knot in spline.Knots.Distinct().Where(k => k > spline.DomainStart && k < spline.DomainEnd))
            {
                GeoSpline2[] cut = rest.SplitAtKnot(knot);
                pieces.Add(cut[0]);
                rest = cut[1];
            }

            pieces.Add(rest);
            return pieces;
        }

        private static void PathIsThePieces(GeoSpline2 spline, string command)
        {
            string[] words = PathOf(new SvgWriter().Add(spline, 0.01, "red"), "red").Split(' ');
            List<GeoSpline2> pieces = Pieces(spline);
            int degree = spline.Degree;

            Assert.Equal("M", words[0]);
            Assert.Equal(spline.StartPoint.X, Parse(words[1]), 6);
            Assert.Equal(spline.StartPoint.Y, Parse(words[2]), 6);
            Assert.Equal(3 + pieces.Count * (1 + 2 * degree), words.Length);

            for (int k = 0; k < pieces.Count; k++)
            {
                int at = 3 + k * (1 + 2 * degree);
                Assert.Equal(command, words[at]);
                Assert.Equal(degree + 1, pieces[k].ControlPointCount);

                for (int j = 1; j <= degree; j++)
                {
                    // The picture has six places.
                    Assert.True(Math.Abs(pieces[k].ControlPoints[j].X - Parse(words[at + 2 * j - 1])) <= 1E-6);
                    Assert.True(Math.Abs(pieces[k].ControlPoints[j].Y - Parse(words[at + 2 * j])) <= 1E-6);
                }
            }
        }

        [Fact]
        public void ACubicBezierInASvgPicture_IsOneCubicCommand()
        {
            Assert.Equal("M 0 0 C 100 150 200 150 300 0", PathOf(new SvgWriter().Add(Arch(), 0.01, "red"), "red"));
        }

        [Fact]
        public void ACubicOfSeveralPieces_IsACubicCommandForEachOfItsBezierPieces()
        {
            PathIsThePieces(Wave(), "C");
        }

        [Fact]
        public void AQuadraticIsAQuadraticCommandForEachPiece_AndASplineOfDegreeOneALineForEach()
        {
            var quadratic = new GeoSpline2(2, new[] { new GeoPoint2(0, 0), new GeoPoint2(50, 80), new GeoPoint2(120, -20), new GeoPoint2(200, 40), new GeoPoint2(260, 0) }, new double[] { 0, 0, 0, 0.3, 0.7, 1, 1, 1 });
            var straight = GeoSpline2.FromPolyline(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 50), new GeoPoint2(170, 80)));

            PathIsThePieces(quadratic, "Q");
            PathIsThePieces(straight, "L");
            Assert.Equal("M 0 0 L 100 0 L 100 50 L 170 80", PathOf(new SvgWriter().Add(straight, 0.01, "red"), "red"));
        }

        private static List<GeoPoint2> ChordsOf(string d)
        {
            string[] words = d.Split(' ');
            var points = new List<GeoPoint2>();

            for (int i = 0; i < words.Length; i += 3)
            {
                Assert.Equal(i == 0 ? "M" : "L", words[i]);
                points.Add(new GeoPoint2(Parse(words[i + 1]), Parse(words[i + 2])));
            }

            return points;
        }

        [Fact]
        public void ACircle_HasNoCurveInSvg_AndIsDrawnThroughChordsWithinTheTolerance()
        {
            GeoSpline2 circle = Circle();
            List<GeoPoint2> points = ChordsOf(PathOf(new SvgWriter().Add(circle, 0.01, "red"), "red"));

            // The corners the library cuts it at, every one on the circle to the six places of the picture.
            Assert.Equal(257, points.Count);
            Assert.All(points, p => Assert.True(Math.Abs(p.DistanceTo(GeoPoint2.Origin) - 100.0) < 2E-6, p.ToString()));

            for (int i = 0; i + 1 < points.Count; i++)
            {
                GeoPoint2 middle = points[i].GetMiddlePoint(points[i + 1]);
                Assert.True(100.0 - middle.DistanceTo(GeoPoint2.Origin) <= 0.01 + 2E-6);
            }
        }

        [Fact]
        public void ASplineOfDegreeFour_IsDrawnThroughChordsWithinTheTolerance()
        {
            GeoSpline2 quartic = GeoSpline2.FromControlPoints(4, new[] { new GeoPoint2(0, 0), new GeoPoint2(40, 90), new GeoPoint2(100, -60), new GeoPoint2(160, 120), new GeoPoint2(220, -30), new GeoPoint2(260, 40) });
            List<GeoPoint2> points = ChordsOf(PathOf(new SvgWriter().Add(quartic, 0.05, "red"), "red"));

            Assert.True(points.Count > 10);

            for (int i = 0; i + 1 < points.Count; i++)
            {
                for (int s = 1; s < 8; s++)
                {
                    GeoPoint2 along = points[i].Add(points[i].GetVectorTo(points[i + 1]).Multiply(s / 8.0));
                    Assert.True(quartic.DistanceTo(along, Tol) <= 0.05 + 2E-6);
                }
            }
        }

        [Fact]
        public void ASplineGrowsTheSvgPictureByTheBoxItFills()
        {
            double[] box = ((string)XDocument.Parse(new SvgWriter().Add(Arch(), 0.01).ToString()).Root.Attribute("viewBox")).Split(' ').Select(Parse).ToArray();

            // 300 by 112.5 and a margin of a twentieth of 300 on every side.
            Assert.Equal(new[] { -15.0, -127.5, 330.0, 142.5 }, box);
        }

        [Fact]
        public void ASvgSplineThatNeedsTooManyChords_IsRefused_AndLeavesThePictureAsItWas()
        {
            var writer = new SvgWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10)));
            string before = writer.ToString();

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.Add(GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(5000, 5000), 1000)), 1E-9));
            Assert.Equal(before, writer.ToString());
        }

        [Fact]
        public void ASvgSplineWithAToleranceThatIsNoNumberOrNull_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SvgWriter().Add(Arch(), 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SvgWriter().Add(Arch(), double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SvgWriter().Add(Circle(), double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new SvgWriter().Add((GeoSpline2)null, 0.1));
        }

        // ----- OBJ -----------------------------------------------------------------------------------------------------

        private static List<GeoPoint3> Vertices(string obj)
        {
            return obj.Split('\n').Where(l => l.StartsWith("v ", StringComparison.Ordinal))
                .Select(l => l.Split(' ')).Select(w => new GeoPoint3(Parse(w[1]), Parse(w[2]), Parse(w[3]))).ToList();
        }

        [Fact]
        public void ASplineInAnObjFile_IsALineOfChordsWithinTheTolerance_ItsEndsExact()
        {
            var spline = new GeoSpline3(3, new[] { new GeoPoint3(13.5, -168.25, 2), new GeoPoint3(100, 0, 40), new GeoPoint3(100, 100, 0), new GeoPoint3(190, 120.75, 85.5), new GeoPoint3(220, 30, 10) }, new[] { 0, 0, 0, 0, 0.4, 1, 1, 1, 1 }, new[] { 1.0, 0.7, 2.5, 0.9, 1.3 });
            string obj = new ObjWriter().Add(spline, 0.02, "spline").ToString();
            List<GeoPoint3> points = Vertices(obj);

            Assert.Contains("o spline", obj);
            Assert.Equal(spline.Flatten(0.02).VertexCount, points.Count);
            SameBits(new[] { spline.StartPoint.X, spline.StartPoint.Y, spline.StartPoint.Z, spline.EndPoint.X, spline.EndPoint.Y, spline.EndPoint.Z }, new[] { points[0].X, points[0].Y, points[0].Z, points[points.Count - 1].X, points[points.Count - 1].Y, points[points.Count - 1].Z });

            for (int i = 0; i + 1 < points.Count; i++)
            {
                Assert.True(spline.DistanceTo(points[i], Tol) < 1E-9);
                Assert.True(spline.DistanceTo(points[i].GetMiddlePoint(points[i + 1]), Tol) <= 0.02);
            }
        }

        [Fact]
        public void AnObjSplineKeepsChordsShorterThanThePointTolerance_WhereFlattenMergesThem()
        {
            // A cubic 0.004 across, cut within 1E-7: its chords are shorter than the default point tolerance of 1E-3.
            var tiny = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.001, 0.003, 0), new GeoPoint3(0.003, 0.003, 0), new GeoPoint3(0.004, 0, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            var corners = new List<double>();
            tiny.Curve.Flatten(1E-7, corners);

            Assert.Equal(corners.Count / 3, Vertices(new ObjWriter().Add(tiny, 1E-7).ToString()).Count);
            Assert.True(corners.Count / 3 > 8);
        }

        [Fact]
        public void AnObjSplineWithAToleranceThatIsNoNumberOrNull_IsRefused()
        {
            var spline = GeoSpline3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(spline, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(spline, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(spline, 1E-300));
            Assert.Throws<ArgumentNullException>(() => new ObjWriter().Add((GeoSpline3)null, 0.1));
        }

        // ----- WKT -----------------------------------------------------------------------------------------------------

        private static List<double> NumbersOf(string wkt, string tag)
        {
            Assert.StartsWith(tag + " (", wkt);
            Assert.EndsWith(")", wkt);
            return wkt.Substring(tag.Length + 2, wkt.Length - tag.Length - 3).Split(new[] { ", ", " " }, StringSplitOptions.None).Select(Parse).ToList();
        }

        [Fact]
        public void ASplineInWkt_IsALineStringOfItsChords_FromItsFirstControlPointToItsLast()
        {
            GeoSpline2 circle = Circle();
            List<double> numbers = NumbersOf(Wkt.Write(circle, 0.01), "LINESTRING");
            var corners = new List<double>();
            circle.Curve.Flatten(0.01, corners);

            Assert.Equal(257 * 2, numbers.Count);
            SameBits(new[] { circle.StartPoint.X, circle.StartPoint.Y }, numbers.Take(2));
            // A whole turn ends on its start exactly.
            SameBits(new[] { circle.EndPoint.X, circle.EndPoint.Y }, numbers.Skip(numbers.Count - 2));
            Assert.True(circle.IsClosed(Tol));
            SameBits(corners.Skip(2).Take(corners.Count - 4), numbers.Skip(2).Take(numbers.Count - 4));
        }

        [Fact]
        public void ASplineInSpaceInWkt_IsALineStringZ_OfThePointsAnObjFileHas()
        {
            var spline = new GeoSpline3(2, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(50, 80, 20), new GeoPoint3(120, -20, 40), new GeoPoint3(200, 40, 0) }, new double[] { 0, 0, 0, 0.5, 1, 1, 1 });
            List<double> numbers = NumbersOf(Wkt.Write(spline, 0.01), "LINESTRING Z");
            List<GeoPoint3> obj = Vertices(new ObjWriter().Add(spline, 0.01).ToString());

            SameBits(obj.SelectMany(p => new[] { p.X, p.Y, p.Z }), numbers);
        }

        [Fact]
        public void AWktSplineWithAToleranceThatIsNoNumberOrNull_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(Arch(), 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(GeoSpline3.FromCircle(new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10)), double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoSpline2)null, 0.1));
            Assert.Throws<ArgumentNullException>(() => Wkt.Write((GeoSpline3)null, 0.1));
        }
    }
}
