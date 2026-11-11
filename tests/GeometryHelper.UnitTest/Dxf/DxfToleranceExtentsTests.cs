using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfTolerance.GeometricExtents"/> as AutoCAD 2021's <c>GeometricExtents</c> of a feature control frame
    /// (accoreconsole probes GHTQ1 and GHTQ2, 2026-11-12, the style Standard 0.18 high in Arial): the box of its rows, each
    /// twice the text height high, each cell its ink and the text height wide.
    /// </summary>
    public class DxfToleranceExtentsTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        /// <summary>A drawing of R2018, its style Standard in arial.ttf, its fonts looked for in the Fonts folder of Windows.</summary>
        private static DxfDatabase Arial()
        {
            var db = new DxfDatabase("AC1032");
            db.TextStyleTable["Standard"].FileName = "arial.ttf";
            db.FontSearchPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            return db;
        }

        private static DxfTolerance Frame(DxfDatabase db, string text, GeoPoint3 at, GeoVector3? direction = null)
        {
            var frame = new DxfTolerance { Text = text, Location = at };

            if (direction.HasValue)
            {
                frame.Direction = direction.Value;
            }

            db.ModelSpace.AppendEntity(frame);
            frame.DimensionStyleName = "Standard";
            return frame;
        }

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        [Fact]
        public void TheExtents_AreTheBoxOfTheRows_AsAutoCadGivesThem()
        {
            DxfDatabase db = Arial();

            GeoAabb3 one = Frame(db, "{\\Fgdt;j}%%v0.1", new GeoPoint3(0, 0, 0)).GeometricExtents;
            Is(new GeoPoint3(0, -0.18, 0), one.Min);
            Is(new GeoPoint3(0.92296043656207372, 0.18, 0), one.Max);

            // Two rows: the first the wider, the box down to the foot of the second.
            GeoAabb3 two = Frame(
                db,
                "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%vA%%vB%%vC\n{\\Fgdt;c}%%v0.05",
                new GeoPoint3(0, 10, 0)
            ).GeometricExtents;
            Is(new GeoPoint3(0, 9.46, 0), two.Min);
            Is(new GeoPoint3(2.462701227830832, 10.18, 0), two.Max);

            // Turned along (0.6, 0.8): the box of the turned rectangle.
            GeoAabb3 turned = Frame(
                db,
                "{\\Fgdt;j}%%v0.1",
                new GeoPoint3(50, 0, 0),
                new GeoVector3(0.6, 0.8, 0)
            ).GeometricExtents;
            Is(new GeoPoint3(49.856, -0.108, 0), turned.Min);
            Is(new GeoPoint3(50.697776261937243, 0.846368349249659, 0), turned.Max);

            // A frame of no words is a row of no width about its point.
            GeoAabb3 empty = Frame(db, string.Empty, new GeoPoint3(0, 40, 0)).GeometricExtents;
            Is(new GeoPoint3(0, 39.82, 0), empty.Min);
            Is(new GeoPoint3(0, 40.18, 0), empty.Max);
        }

        [Fact]
        public void CellsOfWordsAndSymbols_AreAsWideAsAutoCads()
        {
            // AutoCAD 2021 (GHTQ1 to GHTQ3): the diameter with 1.5/21 of the height either side, the runouts 2.25/21 on their
            // right; words inked past their pen set what follows on from their ink, and ink left of a cell's start is not
            // counted.
            DxfDatabase db = Arial();
            var widths = new[]
            {
                new KeyValuePair<string, double>("{\\Fgdt;n}0.1{\\Fgdt;m}", 1.0095634379263316),
                new KeyValuePair<string, double>("{\\Fgdt;n}{\\Fgdt;m}", 0.66),
                new KeyValuePair<string, double>("{\\Fgdt;h}A", 0.635232898070555),
                new KeyValuePair<string, double>("{\\Fgdt;t}{\\Fgdt;t}", 1.1464285714285722),
                new KeyValuePair<string, double>("A", 0.34809004092769441),
                new KeyValuePair<string, double>("A{\\Fgdt;n}", 0.54094718378483719),
                new KeyValuePair<string, double>("{\\Fgdt;n}j", 0.41141103098811149),
                new KeyValuePair<string, double>("f{\\Fgdt;n}", 0.44910543753654258),
                new KeyValuePair<string, double>("%%vA%%vB%%vC", 1.0031377899044998),
            };
            double y = 0.0;

            foreach (KeyValuePair<string, double> width in widths)
            {
                GeoAabb3 box = Frame(db, width.Key, new GeoPoint3(0, y, 0)).GeometricExtents;
                Assert.True(
                    Math.Abs(box.Max.X - box.Min.X - width.Value) < 1E-6,
                    width.Key + ": " + (box.Max.X - box.Min.X)
                );
                y += 10.0;
            }
        }

        [Fact]
        public void AFrameOfSymbolsAlone_HasItsExtentsWithNoFonts_OneOfWordsNone()
        {
            // The position 0.45 wide and the flatness 0.552857, as AutoCAD 2021 gives them, need no font.
            var db = new DxfDatabase("AC1032");
            GeoAabb3 symbols = Frame(db, "{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(0, 0, 0)).GeometricExtents;
            Is(new GeoPoint3(0, -0.18, 0), symbols.Min);
            Is(new GeoPoint3(0.45 + 0.5528571428571429, 0.18, 0), symbols.Max);

            DxfTolerance words = Frame(db, "{\\Fgdt;j}%%v0.1", new GeoPoint3(0, 10, 0));
            Assert.Throws<InvalidOperationException>(() => words.GeometricExtents);
        }
    }
}
