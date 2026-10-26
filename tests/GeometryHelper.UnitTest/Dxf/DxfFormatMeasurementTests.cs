using System;
using System.Globalization;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfDimension.FormatMeasurement"/>: a measurement written as AutoCAD writes a dimension's, by the variables the
    /// dimension reads. The examples AutoCAD's reference gives of its units, of DIMZIN, DIMRND, DIMPOST and DIMAPOST; 40 lengths
    /// and 30 angles as ezdxf 1.4.5 formats them (<c>format_text</c>, <c>format_angular_text</c>), its rounding of a tie aside;
    /// and the units ezdxf does not write, by AutoCAD's rules.
    /// </summary>
    public class DxfFormatMeasurementTests
    {
        // ezdxf 1.4.5's text of 40 lengths (value, DIMRND, DIMDEC, DIMZIN, DIMDSEP) and 30 angles in radians (DIMAUNIT, DIMADEC,
        // DIMAZIN, DIMDSEP), none at a tie, where ezdxf rounds half to even and AutoCAD half away from nought; the degrees,
        // minutes and seconds those where ezdxf's cutting of the last unit and AutoCAD's rounding of it agree.
        private static readonly Case[] Ezdxf =
        {
            L(0.00076, 0.5, 6, 12, ',', "0"),
            L(-0.000189586, 0.0, 4, 8, ',', "-0,0002"),
            L(0.0576327, 0.0, 3, 8, ',', "0,058"),
            L(0.02481, 0.25, 3, 4, ',', "0"),
            L(4236.997265601, 0.0, 3, 8, ',', "4236,997"),
            L(0.7558204, 0.0, 1, 12, ',', ",8"),
            L(82.265525207, 0.0, 2, 12, ',', "82,27"),
            L(-2e-05, 0.25, 4, 4, '.', "0"),
            L(87.704, 0.0, 6, 12, ',', "87,704"),
            L(1.717, 0.25, 5, 4, '.', "1.75000"),
            L(-46.88404, 5.0, 0, 8, ',', "-45"),
            L(57.5233079, 0.0, 5, 0, ',', "57,52331"),
            L(661.356, 0.0, 3, 0, ',', "661,356"),
            L(0.431, 0.0, 4, 8, '.', "0.431"),
            L(79.868, 0.0, 2, 4, ',', "79,87"),
            L(3.91557, 0.5, 0, 12, ',', "4"),
            L(0.752257797, 0.0, 6, 12, '.', ".752258"),
            L(162.468159639, 5.0, 3, 12, ',', "160"),
            L(0.0008733, 0.25, 2, 0, '.', "0.00"),
            L(19575.3511702, 5.0, 5, 8, '.', "19575"),
            L(0.1626087, 0.0, 4, 8, '.', "0.1626"),
            L(212.21371625, 0.0, 4, 12, ',', "212,2137"),
            L(23.9023233, 0.25, 6, 4, ',', "24,000000"),
            L(5.626e-05, 0.0, 4, 8, ',', "0,0001"),
            L(23286.448828018, 0.0, 2, 12, ',', "23286,45"),
            L(628.571751517, 0.0, 1, 4, '.', "628.6"),
            L(879.9773484, 0.25, 1, 4, ',', "880,0"),
            L(37.7581388, 0.5, 3, 0, '.', "38.000"),
            L(20720.89697, 0.0, 3, 0, '.', "20720.897"),
            L(0.08658, 0.0, 2, 0, '.', "0.09"),
            L(14.578, 0.5, 5, 0, '.', "14.50000"),
            L(1513.452, 5.0, 2, 12, '.', "1515"),
            L(0.001, 0.25, 3, 12, ',', "0"),
            L(257.884, 5.0, 6, 8, ',', "260"),
            L(0.038210612, 0.5, 3, 12, '.', "0"),
            L(20.149, 5.0, 1, 0, '.', "20.0"),
            L(74.42488751, 0.0, 1, 12, '.', "74.4"),
            L(575.7214935, 0.25, 4, 12, '.', "575.75"),
            L(522.1538676, 0.0, 1, 12, ',', "522,2"),
            L(0.063452297, 0.1, 4, 8, ',', "0,1"),
            A(3.0909781052819576, 1, 4, 1, '.', "177\u00B06'0\""),
            A(1.1991348504877664, 3, 4, 1, '.', "1.1991r"),
            A(2.479603126222605, 1, 1, 1, '.', "142\u00B04'"),
            A(6.044703518187082, 0, 6, 3, '.', "346.336\u00B0"),
            A(2.1450096507010312, 1, 2, 1, '.', "122\u00B054'"),
            A(4.679227724596798, 0, 3, 2, '.', "268.1\u00B0"),
            A(5.372540745862699, 2, 0, 3, ',', "342g"),
            A(3.434392387576648, 3, 2, 2, '.', "3.43r"),
            A(1.704314014572463, 2, 4, 1, '.', "108.5000g"),
            A(3.499908749024229, 2, 2, 0, ',', "222,81g"),
            A(6.22102971683306, 0, 0, 1, ',', "356\u00B0"),
            A(0.38048177693476387, 1, 1, 2, '.', "21\u00B048'"),
            A(5.228779546172252, 3, 0, 2, '.', "5r"),
            A(4.7563363709499065, 3, 3, 2, ',', "4,756r"),
            A(3.53365919306101, 2, 2, 2, '.', "224.96g"),
            A(0.46773078624196035, 1, 3, 0, '.', "26\u00B047'56\""),
            A(3.79967115990265, 0, 2, 1, ',', "217,71\u00B0"),
            A(1.7976891295541595, 3, 5, 3, '.', "1.79769r"),
            A(0.6827438658211836, 1, 1, 3, ',', "39\u00B07'"),
            A(0.08028514559173916, 0, 3, 1, ',', "4,600\u00B0"),
            A(5.1155600375953805, 3, 6, 2, ',', "5,11556r"),
            A(2.451432761603838, 0, 4, 0, '.', "140.4568\u00B0"),
            A(0.19189895125677653, 3, 6, 2, '.', "0.191899r"),
            A(3.1956978604016175, 2, 1, 2, ',', "203,4g"),
            A(0.3633426960400571, 3, 2, 3, '.', ".36r"),
            A(2.0315632493213998, 1, 2, 3, ',', "116\u00B024'"),
            A(0.6021385919380436, 3, 3, 0, ',', "0,602r"),
            A(0.21038198803539648, 2, 5, 3, ',', "13,39333g"),
            A(5.534498887960778, 3, 1, 1, '.', "5.5r"),
            A(5.203863400303706, 3, 3, 0, '.', "5.204r"),
        };

        [Fact]
        public void ALengthOrAnAngle_IsWrittenAsEzdxfWritesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension length = ByHandle<DxfRotatedDimension>(db, "61");
            DxfLineAngularDimension2 angle = ByHandle<DxfLineAngularDimension2>(db, "87");

            foreach (Case c in Ezdxf)
            {
                DxfDimension dim = c.Angular ? (DxfDimension)angle : length;
                Plain(dim);
                dim.Dimdsep = c.Separator;

                if (c.Angular)
                {
                    dim.Dimaunit = c.Units;
                    dim.Dimadec = c.Places;
                    dim.Dimazin = c.Zin;
                }
                else
                {
                    dim.Dimrnd = c.Round;
                    dim.Dimdec = c.Places;
                    dim.Dimzin = c.Zin;
                }

                Assert.Equal(c.ToString() + " " + c.Text, c.ToString() + " " + dim.FormatMeasurement(c.Value, string.Empty));
            }
        }

        [Fact]
        public void TheExamplesOfAutoCADsReference_AreWrittenAsItGivesThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            Plain(dim);
            dim.Dimdec = 2;
            dim.Dimfrac = 2;

            // The units of LUNITS and DIMLUNIT, 15.5 to two places.
            Assert.Equal("1.55E+01", Units(dim, 1, 15.5));
            Assert.Equal("15.50", Units(dim, 2, 15.5));
            Assert.Equal("1'-3.50\"", Units(dim, 3, 15.5));
            Assert.Equal("1'-3 1/2\"", Units(dim, 4, 15.5));
            Assert.Equal("15 1/2", Units(dim, 5, 15.5));

            // DIMZIN: 8 leaves out the trailing zeros, 12.5000 is 12.5; 4 the leading, 0.5000 is .5000; 12 both, .5.
            dim.Dimlunit = 2;
            dim.Dimdec = 4;
            dim.Dimzin = 8;
            Assert.Equal("12.5", dim.FormatMeasurement(12.5, string.Empty));
            dim.Dimzin = 4;
            Assert.Equal(".5000", dim.FormatMeasurement(0.5, string.Empty));
            dim.Dimzin = 12;
            Assert.Equal(".5", dim.FormatMeasurement(0.5, string.Empty));

            // DIMZIN of feet and inches: 0 leaves out nought feet and nought inches, 1 shows both, 2 nought feet alone, 3
            // nought inches alone.
            dim.Dimlunit = 4;
            dim.Dimdec = 0;
            string[] six = { "6\"", "0'-6\"", "0'-6\"", "6\"" };
            string[] foot = { "1'", "1'-0\"", "1'", "1'-0\"" };

            for (int zin = 0; zin < 4; zin++)
            {
                dim.Dimzin = zin;
                Assert.Equal(six[zin], dim.FormatMeasurement(6.0, string.Empty));
                Assert.Equal(foot[zin], dim.FormatMeasurement(12.0, string.Empty));
            }

            // DIMRND: to the nearest 0.25, 1.13 is 1.25; to 0.5, 12.3 is 12.5.
            dim.Dimlunit = 2;
            dim.Dimdec = 2;
            dim.Dimzin = 0;
            dim.Dimrnd = 0.25;
            Assert.Equal("1.25", dim.FormatMeasurement(1.13, string.Empty));
            dim.Dimrnd = 0.5;
            Assert.Equal("12.50", dim.FormatMeasurement(12.3, string.Empty));
            dim.Dimrnd = 0.0;

            // DIMPOST: <> stands for the measurement; a text without it is a suffix.
            dim.Dimpost = "<> mm";
            Assert.Equal("60.00 mm", dim.FormatMeasurement(60.0, string.Empty));
            dim.Dimpost = "mm";
            Assert.Equal("60.00mm", dim.FormatMeasurement(60.0, string.Empty));
            dim.Dimpost = "\u00D8<>";
            Assert.Equal("\u00D860.00", dim.FormatMeasurement(60.0, string.Empty));
            dim.Dimpost = string.Empty;

            // DIMAPOST: architectural units, DIMALT on, DIMALTF 25.4, DIMALTD 2 and DIMAPOST mm write 10 units as 10"[254.00mm].
            dim.Dimlunit = 4;
            dim.Dimdec = 4;
            dim.Dimalt = true;
            dim.Dimaltf = 25.4;
            dim.Dimaltd = 2;
            dim.Dimaltu = 2;
            dim.Dimaltz = 0;
            dim.Dimapost = "mm";
            Assert.Equal("10\"[254.00mm]", dim.FormatMeasurement(10.0, string.Empty));
            dim.Dimapost = "~[] mm";
            Assert.Equal("10\"[~254.00 mm]", dim.FormatMeasurement(10.0, string.Empty));

            // [] in the text stands for the alternate measurement, which then follows nowhere else; <> for the primary alone.
            dim.Dimapost = "mm";
            Assert.Equal("10\" (254.00mm)", dim.FormatMeasurement(10.0, "<> ([])"));
            Assert.Equal("10\"[254.00mm] TYP", dim.FormatMeasurement(10.0, "<> TYP"));
            Assert.Equal("254.00mm []", dim.FormatMeasurement(10.0, "[] []"));
            dim.Dimalt = false;
            Assert.Equal("10\" []", dim.FormatMeasurement(10.0, "<> []"));
        }

        [Fact]
        public void AnAngle_IsWrittenInTheUnitsOfDIMAUNIT_45DegreesToFourPlaces()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfPoint3AngularDimension dim = ByHandle<DxfPoint3AngularDimension>(db, "99");
            Plain(dim);
            dim.Dimadec = 4;

            string[] expected = { "45.0000\u00B0", "45\u00B00'0\"", "50.0000g", "0.7854r" };

            for (int units = 0; units < 4; units++)
            {
                dim.Dimaunit = units;
                Assert.Equal(expected[units], dim.FormatMeasurement(Math.PI / 4.0, string.Empty));
            }

            // Degrees, minutes and seconds to each of the nine places, 45.50425 degrees: rounded in the last unit shown.
            string[] places = { "46\u00B0", "45\u00B030'", "45\u00B030'", "45\u00B030'15\"", "45\u00B030'15\"", "45\u00B030'15.3\"", "45\u00B030'15.30\"", "45\u00B030'15.300\"", "45\u00B030'15.3000\"" };
            dim.Dimaunit = 1;

            for (int place = 0; place <= 8; place++)
            {
                dim.Dimadec = place;
                Assert.Equal(places[place], dim.FormatMeasurement(45.50425 * Math.PI / 180.0, string.Empty));
            }

            // 59 minutes 59.6 seconds to whole seconds carry into the degree.
            dim.Dimadec = 4;
            Assert.Equal("1\u00B00'0\"", dim.FormatMeasurement((59.0 / 60.0 + 59.6 / 3600.0) * Math.PI / 180.0, string.Empty));

            // DIMADEC -1 takes the places of DIMDEC; DIMAZIN 2 leaves out the trailing zeros, 1 the leading ones.
            dim.Dimaunit = 0;
            dim.Dimadec = -1;
            dim.Dimdec = 3;
            Assert.Equal("30.000\u00B0", dim.FormatMeasurement(Math.PI / 6.0, string.Empty));
            dim.Dimazin = 2;
            Assert.Equal("30\u00B0", dim.FormatMeasurement(Math.PI / 6.0, string.Empty));
            dim.Dimaunit = 3;
            dim.Dimazin = 1;
            Assert.Equal(".524r", dim.FormatMeasurement(Math.PI / 6.0, string.Empty));

            // DIMLFAC and DIMRND are not the angle's.
            dim.Dimaunit = 0;
            dim.Dimazin = 0;
            dim.Dimlfac = 100.0;
            dim.Dimrnd = 5.0;
            Assert.Equal("30.000\u00B0", dim.FormatMeasurement(Math.PI / 6.0, string.Empty));
        }

        [Fact]
        public void TheUnitsEzdxfDoesNotWrite_AreWrittenByAutoCADsRules()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            Plain(dim);

            // Scientific: one figure before the point, the power of ten of two figures.
            dim.Dimdec = 3;
            Assert.Equal("1.235E-04", Units(dim, 1, 0.00012345));
            Assert.Equal("9.999E+03", Units(dim, 1, 9999.0));
            Assert.Equal("1.000E+04", Units(dim, 1, 9999.9));
            Assert.Equal("0.000E+00", Units(dim, 1, 0.0));
            Assert.Equal("-2.500E+00", Units(dim, 1, -2.5));

            // Past what a decimal holds, the power of ten still of two figures at least, as AutoCAD writes it.
            Assert.Equal("1.000E-20", Units(dim, 1, 1E-20));
            Assert.Equal("1.235E-13", Units(dim, 1, 1.2345E-13));
            Assert.Equal("1.500E+30", Units(dim, 1, 1.5E30));
            Assert.Equal("1.500E+300", Units(dim, 1, 1.5E300));

            // Engineering: 11.9999 inches to two places is a foot; the inches rounded first.
            dim.Dimdec = 2;
            Assert.Equal("1'", Units(dim, 3, 11.9999));
            dim.Dimzin = 1;
            Assert.Equal("1'-0.00\"", Units(dim, 3, 11.9999));
            Assert.Equal("0'-0.50\"", Units(dim, 3, 0.5));
            dim.Dimzin = 12;
            Assert.Equal(".5\"", Units(dim, 3, 0.5));
            Assert.Equal("2'-1.25\"", Units(dim, 3, 25.25));

            // Architectural and fractional to a sixteenth, the fraction reduced; stacked across or aslant, its height DIMTFAC.
            dim.Dimzin = 0;
            dim.Dimdec = 4;
            dim.Dimfrac = 2;
            Assert.Equal("2'-1 3/16\"", Units(dim, 4, 25.1875));
            Assert.Equal("1/2\"", Units(dim, 4, 0.5));
            Assert.Equal("1'-0 1/2\"", Units(dim, 4, 12.5));
            Assert.Equal("3/16", Units(dim, 5, 0.19));
            Assert.Equal("0", Units(dim, 5, 0.01));
            Assert.Equal("-15 1/2", Units(dim, 5, -15.5));
            dim.Dimfrac = 0;
            dim.Dimtfac = 0.7;
            Assert.Equal("1'-3{\\H0.7x;\\S1/2;}\"", Units(dim, 4, 15.5));
            dim.Dimfrac = 1;
            Assert.Equal("15{\\H0.7x;\\S1#2;}", Units(dim, 5, 15.5));
            dim.Dimdec = 0;
            Assert.Equal("16", Units(dim, 5, 15.5));

            // Rounded half away from nought, as the 15 figures the file writes: 2.675 to two places is 2.68.
            dim.Dimdec = 2;
            Assert.Equal("2.68", Units(dim, 2, 2.675));
            Assert.Equal("-2.68", Units(dim, 2, -2.675));
            Assert.Equal("0.00", Units(dim, 2, -0.001));

            // The Windows desktop's: the separator and the groups of the culture of the thread.
            CultureInfo culture = CultureInfo.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("12.345,50", Units(dim, 6, 12345.5));
                dim.Dimzin = 8;
                Assert.Equal("1.234.567,5", Units(dim, 6, 1234567.5));
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.Equal("1,234,567.5", Units(dim, 6, 1234567.5));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Fact]
        public void TheMeasurement_IsScaledByDimlfac_AndPutIntoTheText()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            Plain(dim);
            dim.Dimdec = 2;

            // A detail drawn at 1:100: 0.6 drawn reads 60.
            dim.Dimlfac = 100.0;
            Assert.Equal("60.00", dim.FormatMeasurement(0.6, string.Empty));

            // A negative DIMLFAC is taken as 1 in model space, and as its size in paper space.
            dim.Dimlfac = -2.0;
            Assert.Equal("60.00", dim.FormatMeasurement(60.0, string.Empty));
            var copy = (DxfRotatedDimension)dim.Clone();
            db.PaperSpace.AppendEntity(copy);
            Assert.Equal("120.00", copy.FormatMeasurement(60.0, string.Empty));

            // The text: <> stands for the measurement; empty or null for it alone; a single space for none; else the text.
            dim.Dimlfac = 1.0;
            Assert.Equal("60.00 TYP", dim.FormatMeasurement(60.0, "<> TYP"));
            Assert.Equal("60.00", dim.FormatMeasurement(60.0, null));
            Assert.Equal(string.Empty, dim.FormatMeasurement(60.0, " "));
            Assert.Equal("TYP", dim.FormatMeasurement(60.0, "TYP"));
            Assert.Equal("2x 60.00 <>", dim.FormatMeasurement(60.0, "2x <> <>"));

            Assert.Throws<ArgumentOutOfRangeException>(() => dim.FormatMeasurement(double.NaN, string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => dim.FormatMeasurement(double.PositiveInfinity, string.Empty));
        }

        [Fact]
        public void ADimensionOfTheStyleIso25_WritesAsTheStyleSays()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = ByHandle<DxfRotatedDimension>(db, "61");
            var style = new DxfDimStyleTableRecord { Name = "GH_ISO", Dimdec = 2, Dimdsep = ',', Dimzin = 8, Dimtad = 1 };
            dim.SetDimstyleData(style);

            Assert.Equal("1234,5", dim.FormatMeasurement(1234.5, string.Empty));
            Assert.Equal("60", dim.FormatMeasurement(60.0, string.Empty));
            Assert.Equal("0,5", dim.FormatMeasurement(0.5, string.Empty));
        }

        /// <summary>The dimension given the variables of no prefix, rounding or alternate units, decimal and a point.</summary>
        private static void Plain(DxfDimension dim)
        {
            dim.Dimlunit = 2;
            dim.Dimzin = 0;
            dim.Dimrnd = 0.0;
            dim.Dimlfac = 1.0;
            dim.Dimpost = string.Empty;
            dim.Dimalt = false;
            dim.Dimdsep = '.';
            dim.Dimfrac = 0;
            dim.Dimaunit = 0;
            dim.Dimazin = 0;
        }

        private static string Units(DxfDimension dim, int units, double value)
        {
            dim.Dimlunit = units;
            return dim.FormatMeasurement(value, string.Empty);
        }

        private static Case L(double value, double round, int places, int zin, char separator, string text)
            => new Case { Value = value, Round = round, Places = places, Zin = zin, Separator = separator, Text = text };

        private static Case A(double radians, int units, int places, int azin, char separator, string text)
            => new Case { Angular = true, Value = radians, Units = units, Places = places, Zin = azin, Separator = separator, Text = text };

        /// <summary>A measurement and the variables it is written by, and ezdxf's text of it.</summary>
        private sealed class Case
        {
            public bool Angular { get; set; }

            public double Value { get; set; }

            public double Round { get; set; }

            public int Units { get; set; }

            public int Places { get; set; }

            public int Zin { get; set; }

            public char Separator { get; set; }

            public string Text { get; set; }

            public override string ToString()
                => (Angular ? "angle " : "length ") + Value.ToString("R", CultureInfo.InvariantCulture) + " units " + Units.ToString(CultureInfo.InvariantCulture)
                    + " round " + Round.ToString("R", CultureInfo.InvariantCulture) + " places " + Places.ToString(CultureInfo.InvariantCulture)
                    + " zin " + Zin.ToString(CultureInfo.InvariantCulture) + " '" + Separator + "'";
        }
    }
}
