using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Numbers typed in a table's cells, as AutoCAD 2021's <c>SetValue</c> of a text with <c>ConvertTextToValue</c> reads them:
    /// decimals, powers of ten and fractions as numbers in the drawing's units, two or three numbers with commas as a point,
    /// and the autofill grip carrying each on in its format.
    /// </summary>
    public class TableTypedValuesTests
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

        private static string[] Down(CadDocument document, string handle, int column, int from, int to) =>
            Enumerable
                .Range(from, to - from + 1)
                .Select(r => Entity<DxfTable>(document, handle).TextString(r, column))
                .ToArray();

        private static void FillDown(CadDocument document, string handle, int column) =>
            Assert.True(
                CadTableEditor
                    .Fill(
                        document,
                        handle,
                        new DxfCellRange(3, column, 5, column),
                        new DxfCellRange(2, column, 2, column),
                        DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.CopyFormat
                    )
                    .Succeeded
            );

        [Fact]
        public void ADecimalTyped_IsANumberShownInTheDrawingsUnits_TopRight_AndTheAutofillCountsOnInThem()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);
            Assert.Equal(2, document.Database.Lunits);
            Assert.Equal(4, document.Database.Luprec);

            // AutoCAD 2021: 1.5 a double of the format of the drawing's units, shown 1.5000 at LUPREC 4, the cell right aligned.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1.5").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(DxfDataType.Double, table.DataType(2, 0));
            Assert.Equal(1.5, Assert.IsType<double>(table.Value(2, 0)));
            Assert.Equal("%lu6", table.Format(2, 0));
            Assert.Equal("1.5000", table.TextString(2, 0));
            Assert.Equal(DxfCellAlignment.TopRight, table.Alignment(2, 0));

            FillDown(document, handle, 0);
            Assert.Equal(new[] { "2.5000", "3.5000", "4.5000" }, Down(document, handle, 0, 3, 5));
            Assert.Equal("%lu6", Entity<DxfTable>(document, handle).Format(5, 0));

            // At LUPREC 2, 0.125 is shown 0.13 and 0.015 0.02, rounded as AutoCAD 2021 rounds them, half away from nought.
            document.Database.Luprec = 2;
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, " 0.125 ").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 1, "0.015").Succeeded);
            Assert.Equal("0.13", Entity<DxfTable>(document, handle).TextString(2, 1));
            Assert.Equal("0.02", Entity<DxfTable>(document, handle).TextString(3, 1));
            AssertReopens(document);
        }

        [Fact]
        public void PowersOfTenTyped_AreShownInPowersOfTen_AndCountOnInThem()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1e3").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(1000.0, Assert.IsType<double>(table.Value(2, 0)));
            Assert.Equal("%lu1", table.Format(2, 0));
            Assert.Equal("1.0000E+03", table.TextString(2, 0));

            FillDown(document, handle, 0);
            Assert.Equal(new[] { "1.0010E+03", "1.0020E+03", "1.0030E+03" }, Down(document, handle, 0, 3, 5));
        }

        [Fact]
        public void AFractionTyped_IsItsNumberShownAsAFraction_AndCountsOnInHalves()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // 1/2 is 0.5 shown 1/2 (%lu5), 1-1/2 and 2 3/4 mixed numbers, 1/3 shown to the nearest sixteenth at LUPREC 4.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1/2").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "1-1/2").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "-1/2").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 3, "1/3").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 4, "2 3/4").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(0.5, Assert.IsType<double>(table.Value(2, 0)));
            Assert.Equal("%lu5", table.Format(2, 0));
            Assert.Equal(
                new[] { "1/2", "1 1/2", "-1/2", "5/16", "2 3/4" },
                Enumerable.Range(0, 5).Select(c => table.TextString(2, c)).ToArray()
            );
            Assert.Equal(1.5, Assert.IsType<double>(table.Value(2, 1)));

            FillDown(document, handle, 0);
            Assert.Equal(new[] { "1 1/2", "2 1/2", "3 1/2" }, Down(document, handle, 0, 3, 5));
        }

        [Fact]
        public void TwoNumbersWithACommaTyped_AreAPoint_NotRightAligned_AndTheAutofillCopiesIt()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1,5").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "1.5, -2, 3").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            Assert.Equal(DxfDataType.Point3d, table.DataType(2, 0));
            Assert.Equal("%lu6", table.Format(2, 0));
            Assert.Equal("1.0000, 5.0000, 0.0000", table.TextString(2, 0));
            Assert.Equal("1.5000, -2.0000, 3.0000", table.TextString(2, 1));
            Assert.Null(table.Alignment(2, 0));

            FillDown(document, handle, 0);
            Assert.Equal(Enumerable.Repeat("1.0000, 5.0000, 0.0000", 3).ToArray(), Down(document, handle, 0, 3, 5));
            Assert.Equal(DxfDataType.Point3d, Entity<DxfTable>(document, handle).DataType(5, 0));

            // 1,500 is AutoCAD's 1 500 of thousands, which the library does not write: never a point.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "1,500").Succeeded);
            Assert.NotEqual(DxfDataType.Point3d, Entity<DxfTable>(document, handle).DataType(2, 2));
            Assert.Equal("1,500", Entity<DxfTable>(document, handle).TextString(2, 2));
            AssertReopens(document);
        }

        [Fact]
        public void AWholeNumberTyped_IsRightAligned_ButACellOfItsOwnAlignmentKeepsIt()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "12").Succeeded);
            Assert.Equal(DxfCellAlignment.TopRight, Entity<DxfTable>(document, handle).Alignment(2, 0));

            Entity<DxfTable>(document, handle).SetAlignment(2, 1, DxfCellAlignment.MiddleLeft);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "1.5").Succeeded);
            Assert.Equal(DxfCellAlignment.MiddleLeft, Entity<DxfTable>(document, handle).Alignment(2, 1));

            // Words are not aligned.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "HEA300").Succeeded);
            Assert.Null(Entity<DxfTable>(document, handle).Alignment(2, 2));
        }

        [Fact]
        public void ANumberTypedOverANumber_TakesTheKindAndFormatTheCellHolds()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // As AutoCAD 2021 wrote them: 12 over 1.25 the double 12 shown 12.0000, 1.5 over 12 the whole number 1, 1.5 over
            // 1/4 shown 1 1/2, and 12 in a cell made a number of two places 12.00.
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1.25").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "12").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "12").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "1.5").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "1/4").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 2, "1.5").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            table.SetValue(2, 3, 7.0);
            table.SetFormat(2, 3, "%lu2%pr2");
            Assert.True(CadTableEditor.SetText(document, handle, 2, 3, "12").Succeeded);

            table = Entity<DxfTable>(document, handle);
            Assert.Equal(12.0, Assert.IsType<double>(table.Value(2, 0)));
            Assert.Equal("12.0000", table.TextString(2, 0));
            Assert.Equal(1, Assert.IsType<int>(table.Value(2, 1)));
            Assert.Equal("1 1/2", table.TextString(2, 2));
            Assert.Equal("%lu2%pr2", table.Format(2, 3));
            Assert.Equal("12.00", table.TextString(2, 3));
            Assert.Null(table.Alignment(2, 3));
        }

        [Fact]
        public void AFillSeries_WritesEachNumberInTheFormatOfItsSource()
        {
            CadDocument document = Metric();
            string handle = Schedule(document);

            // 1/2 and 1.5 filled on: 2 1/2, 3.5000, 4 1/2 and 5.5000, the formats of the two sources in turn, as AutoCAD 2021
            // filled them; a series into a cell of a format of its own, without CopyFormat, takes the source's.
            Assert.True(CadTableEditor.SetText(document, handle, 1, 0, "1/2").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "1.5").Succeeded);
            Assert.True(
                CadTableEditor
                    .Fill(
                        document,
                        handle,
                        new DxfCellRange(3, 0, 5, 0),
                        new DxfCellRange(1, 0, 2, 0),
                        DxfTableFillOptions.GenerateSeries
                    )
                    .Succeeded
            );
            Assert.Equal(new[] { "2 1/2", "3.5000", "4 1/2" }, Down(document, handle, 0, 3, 5));

            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "1.5").Succeeded);
            DxfTable table = Entity<DxfTable>(document, handle);
            table.SetValue(3, 1, 7.0);
            table.SetFormat(3, 1, "%lu2%pr2");
            Assert.Equal("7.00", table.TextString(3, 1));
            Assert.True(
                CadTableEditor
                    .Fill(
                        document,
                        handle,
                        new DxfCellRange(3, 1, 3, 1),
                        new DxfCellRange(2, 1, 2, 1),
                        DxfTableFillOptions.GenerateSeries
                    )
                    .Succeeded
            );
            Assert.Equal("%lu6", Entity<DxfTable>(document, handle).Format(3, 1));
            Assert.Equal("2.5000", Entity<DxfTable>(document, handle).TextString(3, 1));
        }
    }
}
