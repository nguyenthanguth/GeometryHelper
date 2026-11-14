using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A table's cells opened and typed over as AutoCAD 2021's editor does them: a number opens as its
    /// <c>GetTextString(FormatOption.ForEditing)</c> (1.5, not 1.5000), a text typed over a number is refused, and thousands
    /// with commas and feet and inches typed are numbers of their own formats.
    /// </summary>
    public class TableEditingTextTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>A schedule of five columns, a title, a header and four rows of data, at the origin.</summary>
        private static string Schedule(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "TABLE" && e.Payload is CadInsertTableRequest request)
                {
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
            Run(session, "TABLE", P(0, 0), Esc);
            return Assert.Single(All<DxfTable>(document.ModelSpace)).Handle;
        }

        private static DxfTable TableOf(CadDocument document, string handle) => Entity<DxfTable>(document, handle);

        private static void Type(CadDocument document, string handle, int row, int column, string text) =>
            Assert.True(CadTableEditor.SetText(document, handle, row, column, text).Succeeded, text);

        /// <summary>The texts a number opens as, one after another, each set in the cell (2, 0) with the format given.</summary>
        private static string[] Opened(CadDocument document, string handle, string format, params double[] numbers)
        {
            DxfTable table = TableOf(document, handle);
            var opened = new string[numbers.Length];

            for (int i = 0; i < numbers.Length; i++)
            {
                table.SetValue(2, 0, numbers[i]);
                table.SetFormat(2, 0, format);
                opened[i] = table.TextStringForEditing(2, 0);
            }

            return opened;
        }

        [Fact]
        public void ANumberTyped_OpensInTheEditorAsAutoCADsForEditingText_NotAsItIsShown()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Type(document, handle, 2, 0, "1.5");

            // The cell shows 1.5000 at LUPREC 4 and opens as 1.5; a cell of words opens as it is.
            DxfTable table = TableOf(document, handle);
            Assert.Equal("1.5000", table.TextString(2, 0));
            Assert.Equal("1.5", table.TextStringForEditing(2, 0));
            Type(document, handle, 2, 1, "HEA300\\Pweb");
            Assert.Equal("HEA300\\Pweb", TableOf(document, handle).TextStringForEditing(2, 1));

            CadCommandSession session = Run(Session(document), "TABLEDIT", P(10, -22));
            Assert.Equal("1.5", session.Prompt.InPlace.Value);
            Run(session, Esc);
            Assert.False(session.IsRunning);

            // Opened and closed with the same words, nothing is written.
            session = Run(Session(document), "TABLEDIT", P(10, -22));
            string undo = document.NextUndo.Name;
            Run(session, "1.5");
            Assert.Equal("1.5000", TableOf(document, handle).TextString(2, 0));
            Assert.Equal(undo, document.NextUndo.Name);
        }

        [Fact]
        public void PowersOfTen_Fractions_PointsAndWholeNumbers_OpenInTheirFormsToTheLastFigure()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string[] typed = { "1e3", "1/3", "1-1/2", "1.5,2.25,-0.1", "12" };

            for (int c = 0; c < typed.Length; c++)
            {
                Type(document, handle, 2, c, typed[c]);
            }

            DxfTable table = TableOf(document, handle);
            Assert.Equal(
                new[] { "1.000000000000000E+03", "10923/32768", "1 1/2", "1.5, 2.25, -0.1", "12" },
                Enumerable.Range(0, 5).Select(c => table.TextStringForEditing(2, c)).ToArray()
            );
        }

        [Fact]
        public void ADecimal_OpensWithSixteenFigures_ItsZerosAndTheDrawingsPlacesLeftOff()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            Assert.Equal(
                new[]
                {
                    "0.333333333333333",
                    "0.666666666666667",
                    "123456789.1234568",
                    "0.3",
                    "0.0000000001",
                    "100",
                    "-0.5",
                    "1000000000000000",
                    "1.000000000000000E+16",
                    "-0.000000025",
                    "0",
                    "0",
                    "3.14159265",
                    "1234.56789012345",
                },
                Opened(
                    document,
                    handle,
                    "%lu6",
                    1.0 / 3.0,
                    2.0 / 3.0,
                    123456789.12345679,
                    0.1 + 0.2,
                    1E-10,
                    100.0,
                    -0.5,
                    1E15,
                    1E16,
                    -2.5E-8,
                    0.0,
                    1E-16,
                    3.14159265,
                    1234.56789012345
                )
            );
            Assert.Equal(new[] { "1.5", "1500.5" }, Opened(document, handle, "%lu2%pr2%th44", 1.5, 1500.5));
        }

        [Fact]
        public void Feet_Inches_Fractions_AndPowersOfTen_OpenInTheUnitsOfTheirFormat()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // Architectural: inches to a thirty-two thousandth, the feet and inches that are nought left out.
            Assert.Equal(
                new[]
                {
                    "1'-0 1/2\"",
                    "6'-3 1/4\"",
                    "1'",
                    "7\"",
                    "1/2\"",
                    "-1'-0 1/2\"",
                    "8'-4\"",
                    "0\"",
                    "11 16383/16384\"",
                    "1'-11 32767/32768\"",
                },
                Opened(document, handle, "%lu4", 12.5, 75.25, 12.0, 7.0, 0.5, -12.5, 100.0, 0.0, 11.99994, 23.99996)
            );

            // Engineering: the inches in decimals.
            Assert.Equal(
                new[] { "6'-3.25\"", "1'", "0.5\"", "125'-0.5\"", "6.5\"", "-1'-0.5\"" },
                Opened(document, handle, "%lu3", 75.25, 12.0, 0.5, 1500.5, 6.5, -12.5)
            );

            // Fractional.
            Assert.Equal(
                new[] { "12 1/2", "3277/32768", "100", "-1/2", "0" },
                Opened(document, handle, "%lu5", 12.5, 0.1, 100.0, -0.5, 1E-6)
            );

            // Powers of ten.
            Assert.Equal(
                new[] { "1.000000000000000E+03", "1.500000000000000E-03", "0.000000000000000E+00" },
                Opened(document, handle, "%lu1", 1000.0, 0.0015, 0.0)
            );

            // The format of the drawing's units follows LUNITS.
            document.Database.Lunits = 4;
            Assert.Equal(new[] { "1'-0 1/2\"" }, Opened(document, handle, "%lu6", 12.5));
            document.Database.Lunits = 5;
            Assert.Equal(new[] { "12 1/2" }, Opened(document, handle, "%lu6", 12.5));
            document.Database.Lunits = 1;
            Assert.Equal(new[] { "1.250000000000000E+01" }, Opened(document, handle, "%lu6", 12.5));
        }

        [Fact]
        public void ANumberOfTwoPlacesOrNone_RoundsHalfAwayFromNought_OnEveryRuntime()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            DxfTable table = TableOf(document, handle);
            table.SetValue(2, 0, 1.0);
            table.SetFormat(2, 0, "%lu2%pr2");
            table.SetValue(2, 1, 1.0);
            table.SetFormat(2, 1, "%lu2%pr0");
            table.SetValue(2, 2, 1.0);
            table.SetFormat(2, 2, "%lu2%pr3");

            // 0.015 is 0.01499999999999999944 in binary and AutoCAD, which rounds the 15 figures it writes, shows 0.02; .NET
            // Core, which rounds the exact value, would show 0.01. The sign of a number that rounds to nought stays.
            Type(document, handle, 2, 0, "0.015");
            Type(document, handle, 2, 1, "2.5");
            Type(document, handle, 2, 2, "-0.0004");
            table = TableOf(document, handle);
            Assert.Equal("0.02", table.TextString(2, 0));
            Assert.Equal("3", table.TextString(2, 1));
            Assert.Equal("-0.000", table.TextString(2, 2));
        }

        [Fact]
        public void ATextTypedOverANumber_IsRefused_AndTheNumberStays()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Type(document, handle, 2, 0, "1.5");
            Type(document, handle, 2, 1, "12");
            Type(document, handle, 2, 2, "1,5");
            Type(document, handle, 2, 3, "1/2");
            Type(document, handle, 2, 4, "HEA300");
            DxfTable before = TableOf(document, handle);
            string[] shown = Enumerable.Range(0, 5).Select(c => before.TextString(2, c)).ToArray();
            string undo = document.NextUndo.Name;

            // AutoCAD 2021's SetValue of the text over each of these throws eInvalidInput and keeps the value.
            foreach (string text in new[] { "abc", "12mm", "1/1/2020", "12:30", "$12", "50%" })
            {
                foreach (int column in new[] { 0, 1, 2, 3 })
                {
                    CadEditResult result = CadTableEditor.SetText(document, handle, 2, column, text);
                    Assert.False(result.Succeeded, text + " over column " + column);
                    Assert.True(result.Refused, text + " over column " + column);
                    Assert.Contains(CadTableEditor.CellName(2, column), result.Message);
                }
            }

            // A point is a point, a number a number, over either, and a number past 32 bits is no whole number.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "12").Refused);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1,5").Refused);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "123456789012").Refused);

            DxfTable table = TableOf(document, handle);
            Assert.Equal(shown, Enumerable.Range(0, 5).Select(c => table.TextString(2, c)).ToArray());
            Assert.Equal(1.5, table.Value(2, 0));
            Assert.Equal(12, table.Value(2, 1));
            Assert.Equal(undo, document.NextUndo.Name);

            // Words take any words, and a number over a number is read as before.
            Type(document, handle, 2, 4, "abc");
            Type(document, handle, 2, 0, "2e3");
            Type(document, handle, 2, 1, "1.5");
            table = TableOf(document, handle);
            Assert.Equal("abc", table.Value(2, 4));
            Assert.Equal(2000.0, table.Value(2, 0));
            Assert.Equal(1, table.Value(2, 1));
        }

        [Fact]
        public void ThousandsWithCommasTyped_AreANumberWithTheThousandsFormat_AndAnyOtherCommasAPoint()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string[] typed = { "1,500", "1,500.5", "1,500e2", "-12,500", ",500", "1000,000", "1,50", "1,500,5" };

            for (int c = 0; c < 5; c++)
            {
                Type(document, handle, 2, c, typed[c]);
            }

            for (int c = 0; c < 3; c++)
            {
                Type(document, handle, 3, c, typed[5 + c]);
            }

            DxfTable table = TableOf(document, handle);

            // AutoCAD 2021: 1,500 the whole number 1500 of %th[\,], shown 1,500; 1,500.5 the number of %lu6%th[\,], shown
            // 1,500.5000; 1,500e2 that of %lu1%th[\,]; each right aligned.
            Assert.Equal(DxfDataType.Long, table.DataType(2, 0));
            Assert.Equal(1500, table.Value(2, 0));
            Assert.Equal("%th[\\,]", table.Format(2, 0));
            Assert.Equal("1,500", table.TextString(2, 0));
            Assert.Equal("1500", table.TextStringForEditing(2, 0));
            Assert.Equal(DxfCellAlignment.TopRight, table.Alignment(2, 0));
            Assert.Equal(DxfDataType.Double, table.DataType(2, 1));
            Assert.Equal(1500.5, table.Value(2, 1));
            Assert.Equal("%lu6%th[\\,]", table.Format(2, 1));
            Assert.Equal("1,500.5000", table.TextString(2, 1));
            Assert.Equal("1500.5", table.TextStringForEditing(2, 1));
            Assert.Equal(150000.0, table.Value(2, 2));
            Assert.Equal("%lu1%th[\\,]", table.Format(2, 2));
            Assert.Equal("1.5000E+05", table.TextString(2, 2));
            Assert.Equal(-12500, table.Value(2, 3));
            Assert.Equal("-12,500", table.TextString(2, 3));
            Assert.Equal(500, table.Value(2, 4));
            Assert.Equal("500", table.TextString(2, 4));
            Assert.Equal(1000000, table.Value(3, 0));
            Assert.Equal("1,000,000", table.TextString(3, 0));

            // Not thousands: a comma followed by other than three figures is a point.
            Assert.Equal(DxfDataType.Point3d, table.DataType(3, 1));
            Assert.Equal("1.0000, 50.0000, 0.0000", table.TextString(3, 1));
            Assert.Equal(DxfDataType.Point3d, table.DataType(3, 2));
            Assert.Equal("1.0000, 500.0000, 5.0000", table.TextString(3, 2));

            // Typed over, the cell keeps its kind and format: 2000.5 over the 1,500 is cut and shown with its comma.
            Type(document, handle, 2, 0, "2000.5");
            Type(document, handle, 2, 1, "12");
            table = TableOf(document, handle);
            Assert.Equal(2000, table.Value(2, 0));
            Assert.Equal("2,000", table.TextString(2, 0));
            Assert.Equal("12.0000", table.TextString(2, 1));
        }

        [Fact]
        public void ANumberTypedInACellOfAThousandsFormatOfAutoCADsOwn_IsShownWithItsSeparators()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            DxfTable table = TableOf(document, handle);
            table.SetValue(2, 0, 1.0);
            table.SetFormat(2, 0, "%lu2%pr2%th44");
            table.SetValue(2, 1, 1.0);
            table.SetFormat(2, 1, "%lu2%pr2%th46%ds44");

            Type(document, handle, 2, 0, "1234567.891");
            Type(document, handle, 2, 1, "1500.5");
            table = TableOf(document, handle);

            // AutoCAD 2021: %th44 puts a comma between thousands and %ds44 a comma for the point.
            Assert.Equal("1,234,567.89", table.TextString(2, 0));
            Assert.Equal("1.500,50", table.TextString(2, 1));
            Assert.Equal("1234567.891", table.TextStringForEditing(2, 0));
        }

        [Fact]
        public void FeetAndInchesTyped_AreANumberOfInches_OfTheArchitecturalOrEngineeringFormat()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            string[] typed = { "12'6\"", "12'", "-12'6\"", "1'-2-1/2\"", "6.5\"" };

            for (int c = 0; c < typed.Length; c++)
            {
                Type(document, handle, 2, c, typed[c]);
            }

            Type(document, handle, 3, 0, "12.5'");
            Type(document, handle, 3, 1, "12'6.5\"");
            Type(document, handle, 3, 2, "11.99999\"");
            Type(document, handle, 3, 3, "6 1/2\"");
            Type(document, handle, 3, 4, "1/2'");
            DxfTable table = TableOf(document, handle);

            // AutoCAD 2021: the value in inches, the format %lu4 for feet, whole inches and fractions of an inch, %lu3 where a
            // figure has a decimal point, shown at LUPREC 4, right aligned.
            Assert.Equal(
                new[] { 150.0, 144.0, -150.0, 14.5, 6.5 },
                Enumerable.Range(0, 5).Select(c => Assert.IsType<double>(table.Value(2, c))).ToArray()
            );
            Assert.Equal(
                new[] { "%lu4", "%lu4", "%lu4", "%lu4", "%lu3" },
                Enumerable.Range(0, 5).Select(c => table.Format(2, c)).ToArray()
            );
            Assert.Equal(
                new[] { "12'-6\"", "12'-0\"", "-12'-6\"", "1'-2 1/2\"", "0'-6.5000\"" },
                Enumerable.Range(0, 5).Select(c => table.TextString(2, c)).ToArray()
            );
            Assert.Equal(
                new[] { "12'-6\"", "12'", "-12'-6\"", "1'-2 1/2\"", "6.5\"" },
                Enumerable.Range(0, 5).Select(c => table.TextStringForEditing(2, c)).ToArray()
            );
            Assert.Equal(DxfCellAlignment.TopRight, table.Alignment(2, 0));
            Assert.Equal(
                new[] { 150.0, 150.5, 11.99999, 6.5, 6.0 },
                Enumerable.Range(0, 5).Select(c => Assert.IsType<double>(table.Value(3, c))).ToArray()
            );
            Assert.Equal(
                new[] { "12'-6.0000\"", "12'-6.5000\"", "1'-0.0000\"", "0'-6 1/2\"", "0'-6\"" },
                Enumerable.Range(0, 5).Select(c => table.TextString(3, c)).ToArray()
            );
        }

        [Fact]
        public void FeetAndInchesTypedWrongly_StayWords_OrAreRefusedOverANumber()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // AutoCAD 2021 reads none of these as lengths: a space before the figures, other units, marks out of order.
            string[] typed = { " 12'6\"", "12ft", "12\"6", "12'6\"'", "12 '", "5\"3'", "1.5'6\"" };

            for (int c = 0; c < typed.Length; c++)
            {
                Type(document, handle, 2 + c / 5, c % 5, typed[c]);
            }

            DxfTable table = TableOf(document, handle);

            for (int c = 0; c < typed.Length; c++)
            {
                Assert.Equal(DxfDataType.String, table.DataType(2 + c / 5, c % 5));
                Assert.Equal(typed[c], table.TextString(2 + c / 5, c % 5));
            }

            // Over a whole number, the inches are cut to a whole number, 12'6.5" being 150 and 1/2" nought.
            Type(document, handle, 4, 0, "7");
            Type(document, handle, 4, 0, "12'6.5\"");
            Type(document, handle, 4, 1, "7");
            Type(document, handle, 4, 1, "1/2\"");
            table = TableOf(document, handle);
            Assert.Equal(150, table.Value(4, 0));
            Assert.Equal(0, table.Value(4, 1));
            Assert.True(CadTableEditor.SetText(document, handle, 4, 0, "12ft").Refused);
            Assert.Equal(150, TableOf(document, handle).Value(4, 0));
        }

        [Fact]
        public void DatesAndTimesTyped_AreDatesOfAutoCadsFormats_OpenAsDates_AndRefuseWordsOverThem()
        {
            CultureInfo was = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            try
            {
                CadDocument document = Metric();
                string handle = Schedule(document);

                // AutoCAD 2021 makes 1/1/2020 a date of the format %x, 12:30 a time of %X and both a date of %c, shown in the
                // culture of the machine; each stays where the cell's row put its words.
                Type(document, handle, 2, 0, "1/1/2020");
                Type(document, handle, 2, 1, "12:30");
                Type(document, handle, 2, 2, "1/1/2020 12:30");
                Type(document, handle, 2, 3, "2/30/2020");
                DxfTable table = TableOf(document, handle);
                Assert.Equal(DxfDataType.Date, table.DataType(2, 0));
                Assert.Equal("%x", table.Format(2, 0));
                Assert.Equal("1/1/2020", table.TextString(2, 0));
                Assert.Equal("%X", table.Format(2, 1));
                Assert.Equal("12:30:00 PM", table.TextString(2, 1));
                Assert.Equal("%c", table.Format(2, 2));
                Assert.Equal("1/1/2020 12:30:00 PM", table.TextString(2, 2));

                // A day that does not exist is words, and the editor opens a date as AutoCAD's ForEditing does.
                Assert.Equal(DxfDataType.String, table.DataType(2, 3));
                Assert.Equal("2/30/2020", table.TextString(2, 3));
                Assert.Equal("1/1/2020", table.TextStringForEditing(2, 0));
                Assert.Equal("12:30:00 PM", table.TextStringForEditing(2, 1));
                Assert.Equal("1/1/2020 12:30:00 PM", table.TextStringForEditing(2, 2));

                // The words the editor opened with change nothing, other words over a date are refused and keep it, and another
                // date or time goes in in the cell's own format.
                Assert.Empty(CadTableEditor.SetText(document, handle, 2, 0, "1/1/2020").Records);
                CadEditResult refused = CadTableEditor.SetText(document, handle, 2, 0, "abc");
                Assert.True(refused.Refused);
                Assert.Equal("Cell A3 holds a date, and \"abc\" is not a date.", refused.Message);
                Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "5").Refused);
                Type(document, handle, 2, 0, "12/31/2021");
                Assert.Equal("12/31/2021", TableOf(document, handle).TextString(2, 0));
                AssertReopens(document);
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }
    }
}
