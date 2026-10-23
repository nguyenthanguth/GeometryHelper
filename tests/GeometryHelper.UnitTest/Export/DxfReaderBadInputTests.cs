using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Enums;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// What <see cref="DxfReader"/> does with a file that is not right: one it cannot read is refused naming the line, an
    /// entity with no shape to it is counted and the others read; and how it reads the unit a file is in.
    /// </summary>
    public class DxfReaderBadInputTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDocument Plan(string text) => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });

        // ----- Files that cannot be read ---------------------------------------------------------------------------------

        [Fact]
        public void ABinaryDxf_IsRefusedSayingSo_FromTextAndFromBytes()
        {
            string binary = "AutoCAD Binary DXF\r\n\u001a\0\0\0SECTION";

            Assert.Contains("binary", Assert.Throws<FormatException>(() => DxfReader.Parse(binary)).Message);
            Assert.Contains("binary", Assert.Throws<FormatException>(() => DxfReader.Read(new MemoryStream(Encoding.ASCII.GetBytes(binary)))).Message);
        }

        [Fact]
        public void AFileCutShort_IsRefused_NamingTheLineItEndsOn()
        {
            string whole = R2000(G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"));
            string noEof = whole.Substring(0, whole.LastIndexOf("  0\nEOF", StringComparison.Ordinal));
            string noValue = noEof + " 10\n";

            FormatException first = Assert.Throws<FormatException>(() => DxfReader.Parse(noEof));
            FormatException second = Assert.Throws<FormatException>(() => DxfReader.Parse(noValue));

            Assert.Contains("cut short", first.Message);
            Assert.Contains("line " + noEof.Count(c => c == '\n'), first.Message);
            Assert.Contains("line " + noValue.Count(c => c == '\n'), second.Message);
        }

        [Fact]
        public void AGroupCodeThatIsNotANumber_IsRefused_NamingItsLine()
        {
            string text = "  0\nSECTION\nabc\nENTITIES\n  0\nENDSEC\n  0\nEOF\n";
            FormatException refused = Assert.Throws<FormatException>(() => DxfReader.Parse(text));

            Assert.Contains("line 3", refused.Message);
            Assert.Contains("'abc'", refused.Message);
        }

        [Fact]
        public void AFileWithNoEntitiesSectionOrASectionLeftOpen_OrNothingAtAll_IsRefused()
        {
            string noEntities = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 0, "ENDSEC", 0, "EOF");
            string open = G(0, "SECTION", 2, "ENTITIES", 0, "LINE", 8, "0", 0, "EOF");

            Assert.Contains("ENTITIES", Assert.Throws<FormatException>(() => DxfReader.Parse(noEntities)).Message);
            Assert.Contains("ENDSEC", Assert.Throws<FormatException>(() => DxfReader.Parse(open)).Message);
            Assert.Throws<FormatException>(() => DxfReader.Parse(string.Empty));
            Assert.Throws<ArgumentNullException>(() => DxfReader.Parse(null));
            Assert.Throws<ArgumentNullException>(() => DxfReader.Read((Stream)null));
            Assert.Throws<ArgumentNullException>(() => DxfReader.Read((string)null));
        }

        // ----- Entities with no shape to them ----------------------------------------------------------------------------

        [Fact]
        public void EntitiesWithNoShapeToThem_AreCountedUnderTheirTypes_AndTheOthersRead()
        {
            string text = R2000(G(
                0, "LINE", 8, "0", 10, "5", 20, "5", 11, "5", 21, "5",
                0, "LINE", 8, "0", 10, "0", 20, "0", 11, "0.0004", 21, "0",
                0, "LINE", 8, "0", 10, "NaN", 20, "0", 11, "1", 21, "0",
                0, "LINE", 8, "0", 10, "0", 20, "Infinity", 11, "1", 21, "0",
                0, "LINE", 8, "0", 10, "0", 20, "0", 11, "10", 21, "0",
                0, "LWPOLYLINE", 8, "0", 90, 1, 70, 0, 10, "1", 20, "1",
                0, "LWPOLYLINE", 8, "0", 90, 3, 70, 1, 10, "1", 20, "1", 10, "1", 20, "1.0001", 10, "1.0002", 20, "1",
                0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "0",
                0, "ARC", 8, "0", 10, "0", 20, "0", 40, "0", 50, "0", 51, "90",
                0, "ELLIPSE", 8, "0", 10, "0", 20, "0", 11, "0", 21, "0", 31, "0", 40, "0.5",
                0, "ELLIPSE", 8, "0", 10, "0", 20, "0", 11, "10", 21, "0", 31, "0", 40, "0",
                0, "SPLINE", 8, "0", 70, 8, 71, 3, 40, "0", 40, "1", 10, "0", 20, "0", 10, "1", 20, "1",
                0, "3DFACE", 8, "0", 10, "1", 20, "1", 30, "1", 11, "1", 21, "1", 31, "1", 12, "1", 22, "1", 32, "1", 13, "1", 23, "1", 33, "1",
                0, "RAY", 8, "0", 10, "0", 20, "0", 11, "0", 21, "0", 31, "0",
                0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "3"));

            var log = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            GeometryHelperLog.Writer = (level, message, exception) => log.Add(level + " " + message);

            DxfDocument read;

            try
            {
                read = Plan(text);
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            Assert.Equal(new[] { "LINE", "CIRCLE" }, read.Entities.Select(e => e.Type).ToArray());
            Assert.Equal(new[] { "3DFACE", "ARC", "CIRCLE", "ELLIPSE", "LINE", "LWPOLYLINE", "RAY", "SPLINE" }, read.Skipped.Keys.ToArray());
            Assert.Equal(4, read.Skipped["LINE"]);
            Assert.Equal(2, read.Skipped["LWPOLYLINE"]);
            Assert.Equal(2, read.Skipped["ELLIPSE"]);

            // One Debug line for each type, saying why, and one Info line for the call.
            Assert.Equal(8, log.Count(l => l.StartsWith("Debug DxfReader: ", StringComparison.Ordinal)));
            Assert.Contains("Debug DxfReader: 4 LINE not read: 2 of no length, 2 with a number that is not a finite number.", log);
            Assert.True(log.Exists(l => l.StartsWith("Info DxfReader: 2 entities read from a text, 13 skipped (3DFACE 1, ARC 1, CIRCLE 1, ELLIPSE 2, LINE 4, LWPOLYLINE 2, RAY 1, SPLINE 1)", StringComparison.Ordinal)), string.Join(" | ", log));
        }

        [Fact]
        public void ALineShorterThanThePointTolerance_IsASegmentWithinATighterOne()
        {
            string text = R2000(G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "0.0004", 21, "0"));

            Assert.Empty(Plan(text).Entities);
            Assert.Single(DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = new Tolerance(1E-5, 1E-7) }).Entities);
        }

        [Fact]
        public void APeriodicSplineWithKnotsNotClamped_IsClampedByKnotInsertion_TheSameCurveOverTheSameSpan()
        {
            // A closed periodic cubic of four control points, its knots uniform as AutoCAD writes them: the curve is that of
            // the knots 3 to 4, and the spline read is the same curve there.
            string text = R2000(G(
                0, "SPLINE", 8, "0", 70, 11, 71, 3, 72, 8, 73, 4, 74, 0,
                40, "0", 40, "1", 40, "2", 40, "3", 40, "4", 40, "5", 40, "6", 40, "7",
                10, "0", 20, "0", 30, "0", 10, "10", 20, "0", 30, "0", 10, "10", 20, "10", 30, "0", 10, "0", 20, "10", 30, "0"));

            DxfDocument read = Plan(text);
            var spline = Assert.IsType<GeoSpline2>(Assert.Single(read.Entities).Shape);
            var control = new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) };
            double[] knots = { 0, 1, 2, 3, 4, 5, 6, 7 };

            Assert.Equal(new[] { 3.0, 3, 3, 3, 4, 4, 4, 4 }, spline.Knots.ToArray());

            for (int i = 0; i <= 8; i++)
            {
                double u = 3.0 + i / 8.0;
                GeoPoint2 expected = DeBoor(3, control, knots, u);
                GeoPoint2 actual = spline.GetPointAtKnot(u);
                Assert.True(expected.DistanceTo(actual) < 1E-12, u + ": " + expected + " read as " + actual);
            }

            Assert.Empty(read.Skipped);
        }

        /// <summary>The point of a B-spline at a parameter, by de Boor's rule, its knots clamped or not.</summary>
        private static GeoPoint2 DeBoor(int p, GeoPoint2[] points, double[] knots, double u)
        {
            int k = p;

            while (k + 1 < points.Length && u >= knots[k + 1])
            {
                k++;
            }

            var d = new GeoPoint2[p + 1];

            for (int j = 0; j <= p; j++)
            {
                d[j] = points[j + k - p];
            }

            for (int r = 1; r <= p; r++)
            {
                for (int j = p; j >= r; j--)
                {
                    double a = (u - knots[j + k - p]) / (knots[j + 1 + k - r] - knots[j + k - p]);
                    d[j] = new GeoPoint2((1 - a) * d[j - 1].X + a * d[j].X, (1 - a) * d[j - 1].Y + a * d[j].Y);
                }
            }

            return d[p];
        }

        // ----- Units -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ADrawingInMetres_IsScaledToMillimetresWhereAsked_ItsAnglesBulgesRatiosKnotsAndWeightsKept()
        {
            string text = File("AC1015", 6, string.Empty, G(
                0, "LINE", 8, "0", 39, "0.5", 10, "1", 20, "2", 30, "3", 11, "4", 21, "5", 31, "6",
                0, "ARC", 8, "0", 10, "1", 20, "1", 30, "0", 40, "2", 50, "30", 51, "60",
                0, "LWPOLYLINE", 8, "0", 90, 2, 70, 0, 38, "0.25", 10, "0", 20, "0", 42, "0.5", 10, "1", 20, "0",
                0, "ELLIPSE", 8, "0", 10, "1", 20, "1", 30, "0", 11, "2", 21, "0", 31, "0", 40, "0.5",
                0, "SPLINE", 8, "0", 70, 12, 71, 2, 72, 6, 73, 3, 74, 0, 40, "0", 40, "0", 40, "0", 40, "1", 40, "1", 40, "1", 41, "1", 41, "0.5", 41, "1",
                10, "0", 20, "0", 30, "0", 10, "1", 20, "1", 30, "0", 10, "2", 20, "0", 30, "0"));

            DxfDocument kept = Plan(text);
            DxfDocument scaled = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, ToMillimetres = true, Tolerance = Tol });

            Assert.Equal(DxfUnits.Metres, kept.Units);
            Assert.Equal(DxfUnits.Metres, scaled.Units);
            Assert.Equal(new GeoPoint3(4, 5, 6), Assert.IsType<GeoLine3>(kept.Entities[0].Shape).EndPoint);
            Assert.Equal(new GeoPoint3(4000, 5000, 6000), Assert.IsType<GeoLine3>(scaled.Entities[0].Shape).EndPoint);
            Assert.Equal(500.0, scaled.Entities[0].Thickness);

            var arc = Assert.IsType<GeoArc2>(scaled.Entities[1].Shape);
            Assert.Equal(2000.0, arc.Radius);
            Assert.Equal(new GeoPoint2(1000, 1000), arc.Center);
            Assert.Equal(Math.PI / 6, arc.StartAngle, 15);

            var chain = Assert.IsType<GeoPolylineArc3>(scaled.Entities[2].Shape);
            Assert.Equal(250.0, chain.Vertices[0].Z);
            Assert.Equal(0.5, chain.Bulges[0]);

            var ellipse = Assert.IsType<GeoEllipse2>(scaled.Entities[3].Shape);
            Assert.Equal(2000.0, ellipse.MajorRadius);
            Assert.Equal(1000.0, ellipse.MinorRadius);

            var spline = Assert.IsType<GeoSpline2>(scaled.Entities[4].Shape);
            Assert.Equal(new[] { 1.0, 0.5, 1.0 }, spline.Weights.ToArray());
            Assert.Equal(new[] { 0.0, 0, 0, 1, 1, 1 }, spline.Knots.ToArray());
            Assert.Equal(new GeoPoint2(1000, 1000), spline.ControlPoints[1]);
            Assert.Empty(scaled.Warnings);
        }

        [Fact]
        public void ADrawingInInches_IsScaledBy25Point4_AndOneWithNoUnitOrAnUnknownOne_IsKeptAndWarnedAbout()
        {
            string Line(int units) => File("AC1015", units, string.Empty, G(0, "POINT", 8, "0", 10, "2", 20, "3", 30, "0"));
            var options = new DxfReadOptions { PlanAs2D = true, ToMillimetres = true, Tolerance = Tol };

            DxfDocument inches = DxfReader.Parse(Line(1), options);
            DxfDocument none = DxfReader.Parse(Line(0), options);
            DxfDocument unknown = DxfReader.Parse(Line(99), options);

            Assert.Equal(new GeoPoint2(50.8, 76.19999999999999), Assert.IsType<GeoPoint2>(inches.Entities[0].Shape));
            Assert.Equal(DxfUnits.Inches, inches.Units);
            Assert.Equal(new GeoPoint2(2, 3), Assert.IsType<GeoPoint2>(none.Entities[0].Shape));
            Assert.Contains(none.Warnings, w => w.Contains("no unit"));
            Assert.Equal(DxfUnits.Unitless, unknown.Units);
            Assert.Equal(new GeoPoint2(2, 3), Assert.IsType<GeoPoint2>(unknown.Entities[0].Shape));
            Assert.Contains(unknown.Warnings, w => w.Contains("99"));
        }

        [Fact]
        public void AVersionOutsideR12ToR2018_IsReadAndWarnedAbout()
        {
            DxfDocument read = DxfReader.Parse(File("AC1006", 0, string.Empty, G(0, "POINT", 8, "0", 10, "1", 20, "1")));

            Assert.Single(read.Entities);
            Assert.Contains(read.Warnings, w => w.Contains("AC1006"));
        }
    }
}
