using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The cells of the ACAD_TABLE of <see cref="DxfDimensionDrawings"/> (R2000, R2007 and R2018) as AutoCAD's <c>Table</c>
    /// has them: values of their kinds with their units and formats, the look of a cell set as its own override with its flag,
    /// overrides taken away, the cells walked and indexed, and the cell a point hits on a table flat, turned and mirrored.
    /// </summary>
    public class DxfTableCellsTests
    {
        // The drawing of R2018 read as one of R2007, whose cells AutoCAD writes in the same form.
        private static readonly string R2007 = DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1021\n");

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        private static string TableRecord(string text) => Records(text).Single(r => HandleOf(r) == "F6");

        [Fact]
        public void ACellsValueKindUnitAndFormat_AreThoseOfItsValue_AndATextAloneBeforeR2007()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, R2007, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase tableDb = DxfDatabase.Parse(text);
                DxfTable table = Table(tableDb);

                Assert.Equal(DxfDataType.String, table.DataType(1, 0));
                Assert.Equal(DxfUnitType.Unitless, table.UnitType(1, 0));
                Assert.Equal(string.Empty, table.Format(1, 0));
                Assert.Equal(string.Empty, table.GetDataFormat(1, 0, 0));
                Assert.Equal(new DxfDataTypeParameter(DxfDataType.String, DxfUnitType.Unitless), table.GetDataType(2, 0, 0));
            }

            string valued = DxfDimensionDrawings.R2018.Replace(
                " 90\n4\n  1\nB-12\n 94\n0\n300\n\n302\nB-12\n",
                " 90\n2\n140\n12.5\n 94\n1\n300\n%lu2%pr2\n302\n12.50\n");
            DxfDatabase readDb = DxfDatabase.Parse(valued);
            DxfTable read = Table(readDb);

            Assert.Equal(DxfDataType.Double, read.DataType(2, 0));
            Assert.Equal(DxfUnitType.Distance, read.UnitType(2, 0));
            Assert.Equal("%lu2%pr2", read.Format(2, 0));
            Assert.Equal(12.5, read.Value(2, 0));
            Assert.Equal("12.50", read.TextString(2, 0));
        }

        [Fact]
        public void AValueSet_IsWrittenOfItsKind_ItsUnitAndFormatKept_AndAsATextBeforeR2007()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.SetValue(2, 0, 12.5);
            table.SetValue(1, 1, 0, 42);
            table.SetValue(1, 0, "Item");

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "F6");
            string record = TableRecord(saved);
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n2\n140\n12.5\n 94\n0\n300\n\n302\n12.5\n304\nACVALUE_END\n", record, StringComparison.Ordinal);
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n1\n 91\n42\n 94\n0\n300\n\n302\n42\n304\nACVALUE_END\n", record, StringComparison.Ordinal);
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nItem\n 94\n0\n300\n\n302\nItem\n304\nACVALUE_END\n  7\nGH_TXT\n", record, StringComparison.Ordinal);

            DxfDatabase readDb = DxfDatabase.Parse(saved);
            DxfTable read = Table(readDb);
            Assert.Equal(12.5, read.Value(2, 0));
            Assert.Equal(DxfDataType.Double, read.DataType(2, 0));
            Assert.Equal(42, read.GetValue(1, 1, 0));
            Assert.Equal(DxfDataType.Long, read.DataType(1, 1));
            Assert.Equal("Item", read.Value(1, 0));
            Assert.Equal("12.5", read.TextString(2, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));

            // The value it has, of its kind, writes nothing; a long within 32 bits is the whole number it holds.
            read.SetValue(2, 0, 12.5);
            read.SetValue(1, 1, 42L);
            read.ResetValue(0, 1);
            Assert.Equal(saved, readDb.ToText());

            read.ResetValue(2, 0);
            Assert.Equal(DxfDataType.String, read.DataType(2, 0));
            Assert.Equal(string.Empty, read.TextString(2, 0));

            Refused<ArgumentException>(() => read.SetValue(2, 0, new DateTime(2026, 10, 26)), "date or a point");
            Refused<ArgumentException>(() => read.SetValue(2, 0, new GeoPoint3(1, 2, 3)), "date or a point");
            Refused<ArgumentOutOfRangeException>(() => read.SetValue(2, 0, 1L << 40), "32 bits");
            Refused<ArgumentOutOfRangeException>(() => read.SetValue(2, 0, double.NaN), "finite");
            Refused<InvalidOperationException>(() => read.SetValue(2, 1, "x"), "holds a block");
            Refused<InvalidOperationException>(() => read.ResetValue(2, 1), "holds a block");
            Assert.Throws<ArgumentNullException>(() => read.SetValue(2, 0, null));

            // Before R2007 a cell holds its text alone: the value is written as its text.
            string old = DxfDimensionDrawings.R2000;
            DxfDatabase before = DxfDatabase.Parse(old);
            Table(before).SetValue(2, 0, 12.5);
            Table(before).SetValue(1, 1, 7);
            string written = before.ToText();
            OnlyTheseChanged(old, written, "F6");
            Assert.Contains("145\n0.0\n  1\n12.5\n171\n", TableRecord(written), StringComparison.Ordinal);
            DxfDatabase againDb = DxfDatabase.Parse(written);
            DxfTable again = Table(againDb);
            Assert.Equal("12.5", again.Value(2, 0));
            Assert.Equal("7", again.TextString(1, 1));
            Assert.Equal(DxfDataType.String, again.DataType(2, 0));
        }

        [Fact]
        public void AFormatAndAKindSet_AreWrittenInTheValue_TheValueTurnedWhereItIsOfTheKind()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            table.SetTextString(2, 0, "12");

            table.SetDataType(2, 0, DxfDataType.Long, DxfUnitType.Unitless);
            Assert.Equal(12, table.Value(2, 0));
            table.SetDataType(2, 0, 0, new DxfDataTypeParameter(DxfDataType.Double, DxfUnitType.Distance));
            Assert.Equal(12.0, table.Value(2, 0));
            Assert.Equal(DxfUnitType.Distance, table.UnitType(2, 0));
            table.SetFormat(2, 0, "%lu2%pr2");
            table.SetDataFormat(2, 0, 0, "%lu2%pr2");

            string record = TableRecord(db.ToText());
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n2\n140\n12\n 94\n1\n300\n%lu2%pr2\n302\n12\n304\nACVALUE_END\n", record, StringComparison.Ordinal);
            Assert.Equal("%lu2%pr2", Table(DxfDatabase.Parse(db.ToText())).GetDataFormat(2, 0));

            // The unit alone is written in place; a number turned into a text is the text it shows.
            table.SetDataType(2, 0, DxfDataType.Double, DxfUnitType.Area);
            Assert.Equal(12.0, table.Value(2, 0));
            table.SetDataType(2, 0, DxfDataType.String, DxfUnitType.Unitless);
            Assert.Equal("12", table.Value(2, 0));
            Assert.Equal("%lu2%pr2", table.Format(2, 0));

            Refused<ArgumentException>(() => table.SetDataType(1, 0, DxfDataType.Double, DxfUnitType.Unitless), "Mark is not a number");
            Refused<ArgumentException>(() => table.SetDataType(1, 0, DxfDataType.Long, DxfUnitType.Unitless), "Mark is not a whole number");
            Refused<ArgumentException>(() => table.SetDataType(1, 0, DxfDataType.Date, DxfUnitType.Unitless), "no groups");
            Refused<ArgumentOutOfRangeException>(() => table.SetDataType(1, 0, DxfDataType.String, (DxfUnitType)64), "DxfUnitType");
            Refused<ArgumentException>(() => table.SetFormat(1, 0, "a\nb"), "one line");

            // Before R2007 a cell has no value to give a kind or a format; the text and no format it reads as write nothing.
            string old = DxfDimensionDrawings.R2000;
            DxfDatabase before = DxfDatabase.Parse(old);
            Table(before).SetDataType(1, 0, DxfDataType.String, DxfUnitType.Unitless);
            Table(before).SetFormat(1, 0, string.Empty);
            Assert.Equal(old, before.ToText());
            Refused<InvalidOperationException>(() => Table(before).SetFormat(1, 0, "%lu2"), "before R2007");
            Refused<InvalidOperationException>(() => Table(before).SetDataType(1, 0, DxfDataType.Long, DxfUnitType.Unitless), "before R2007");
        }

        [Fact]
        public void AFormatAndAKindAsTheCellHasThem_WriteNothing_AndNeitherSetsAFlagOfItsOverrides()
        {
            string text = DxfDimensionDrawings.R2018.Replace(
                " 90\n4\n  1\nB-12\n 94\n0\n300\n\n302\nB-12\n",
                " 90\n2\n140\n12.5\n 94\n1\n300\n%lu2%pr2\n302\n12.50\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            DxfCell cell = table.Cells[2, 0];

            table.SetFormat(2, 0, "%lu2%pr2");
            table.SetDataFormat(2, 0, 0, "%lu2%pr2");
            cell.DataFormat = "%lu2%pr2";
            table.SetDataType(2, 0, DxfDataType.Double, DxfUnitType.Distance);
            table.SetDataType(2, 0, 0, new DxfDataTypeParameter(DxfDataType.Double, DxfUnitType.Distance));
            cell.DataType = new DxfDataTypeParameter(DxfDataType.Double, DxfUnitType.Distance);
            Assert.Equal(text, db.ToText());

            // A format and a kind written anew are in the cell's value, not among its overrides.
            table.SetFormat(2, 0, "%lu2%pr3");
            table.SetDataType(2, 0, DxfDataType.String, DxfUnitType.Unitless);
            Assert.Equal("12.50", table.Value(2, 0));
            Assert.Equal(DxfCellProperties.Invalid, table.GetOverrides(2, 0, 0));
            Assert.Empty(table.CellStyleOverrides(2, 0));
        }

        [Fact]
        public void AUnitOfSeveralFlags_IsWrittenAsItIs_AndOneOfAFlagNoneHasIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            table.SetDataType(1, 0, DxfDataType.String, DxfUnitType.Angle | DxfUnitType.AngleNotTransformed);
            Assert.Contains("  1\nMark\n 94\n65538\n300\n", TableRecord(db.ToText()), StringComparison.Ordinal);
            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(DxfUnitType.Angle | DxfUnitType.AngleNotTransformed, read.UnitType(1, 0));

            Refused<ArgumentOutOfRangeException>(() => table.SetDataType(1, 0, DxfDataType.String, (DxfUnitType)(64 | 2)), "flags of DxfUnitType");
            Refused<ArgumentOutOfRangeException>(() => table.SetDataType(1, 0, DxfDataType.String, (DxfUnitType)(-1)), "flags of DxfUnitType");
        }

        [Fact]
        public void AValueOfAnotherType_IsRefused_ByWhatItIs()
        {
            DxfTable table = Table(DxfDatabase.Parse(DxfDimensionDrawings.R2018));

            Refused<ArgumentException>(() => table.SetValue(2, 0, 3u), "a UInt32 is none of them");
            Refused<ArgumentException>(() => table.SetValue(2, 0, 2.5m), "a Decimal is none of them");
            Refused<ArgumentException>(() => table.SetValue(2, 0, true), "a Boolean is none of them");
            Refused<ArgumentException>(() => table.SetValue(2, 0, new GeoPoint2(1, 2)), "a date or a point is not");
            ArgumentException date = Assert.Throws<ArgumentException>(() => table.SetValue(2, 0, new DateTime(2026, 10, 26)));
            Assert.Contains("does not describe a cell's value", date.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheLookOfACellSet_IsWrittenAsItsOwnOverride_InTheOrderOfTheReference_WithItsFlag()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, R2007, DxfDimensionDrawings.R2018 })
            {
                bool r2000 = text == DxfDimensionDrawings.R2000;
                string flags = r2000 ? "177" : " 91";
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                table.SetAlignment(1, 1, DxfCellAlignment.TopLeft);
                table.SetContentColor(1, 0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
                table.SetTextHeight(2, 0, 0, 2.5);
                table.SetTextStyle(2, 0, db.TextStyleTable["GH_TXT"]);
                table.SetBackgroundColor(2, 1, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3));
                table.SetBackgroundColorNone(2, 1, false);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                string record = TableRecord(saved);

                // Each after the cell's head (and its value from R2007 on), in the order 7, 140, 170, 64, 63, ..., 283.
                Assert.Contains(flags + "\n1\n178\n", record, StringComparison.Ordinal);
                Assert.Contains((r2000 ? "  1\nQty\n" : "302\nQty\n304\nACVALUE_END\n") + "170\n1\n171\n", record, StringComparison.Ordinal);
                Assert.Contains(flags + "\n57\n178\n", record, StringComparison.Ordinal);
                Assert.Contains("  7\nGH_TXT\n140\n3.5\n170\n5\n 64\n1\n171\n", record, StringComparison.Ordinal);
                Assert.Contains(flags + "\n48\n178\n", record, StringComparison.Ordinal);
                Assert.Contains((r2000 ? "  1\nB-12\n" : "302\nB-12\n304\nACVALUE_END\n") + "  7\nGH_TXT\n140\n2.5\n171\n", record, StringComparison.Ordinal);
                Assert.Contains(flags + "\n6\n178\n", record, StringComparison.Ordinal);
                Assert.EndsWith((r2000 ? "300\nM12\n" : "304\nACVALUE_END\n") + " 63\n3\n283\n1\n", record, StringComparison.Ordinal);

                DxfDatabase readDb = DxfDatabase.Parse(saved);
                DxfTable read = Table(readDb);
                Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(1, 1));
                Assert.Equal(1, read.ContentColor(1, 0).ColorIndex);
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
                Assert.Equal(2.5, read.TextHeight(2, 0));
                Assert.Equal("GH_TXT", read.TextStyle(2, 0).Name);
                Assert.Equal(3, read.BackgroundColor(2, 1).ColorIndex);
                Assert.False(read.IsBackgroundColorNone(2, 1));
                Assert.Equal(new[] { DxfTableStyleOverride.CellBackgroundFillNone, DxfTableStyleOverride.CellBackgroundColor }, read.CellStyleOverrides(2, 1));
                Assert.Equal("M12", read.GetBlockAttributeValue(2, 1, DxfDatabase.Parse(saved).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
                Assert.False(read.IsTitleSuppressed);
                Assert.Equal(DxfFlowDirection.TopToBottom, read.FlowDirection);

                // Each set to what it has writes nothing.
                read.SetAlignment(1, 1, DxfCellAlignment.TopLeft);
                read.SetContentColor(1, 0, 0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
                read.SetTextHeight(2, 0, 2.5);
                read.SetTextStyle(1, 0, readDb.TextStyleTable["gh_txt"]);
                read.SetBackgroundColor(2, 1, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3));
                read.SetBackgroundColorNone(2, 1, false);
                Assert.Equal(saved, readDb.ToText());

                Refused<ArgumentException>(() => read.SetContentColor(1, 1, DxfColor.FromRgb(255, 128, 0)), "AutoCAD's index");
                Refused<ArgumentOutOfRangeException>(() => read.SetTextHeight(1, 1, 0.0), "above nought");
                Refused<ArgumentOutOfRangeException>(() => read.SetAlignment(1, 1, (DxfCellAlignment)10), "1 to 9");
                Assert.Throws<ArgumentNullException>(() => read.SetBackgroundColor(1, 1, null));
            }
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ACellThatGivesNoFlags_IsGivenThoseOfTheOverridesItHolds_SoNoneIsLost(string version)
        {
            bool r2000 = version == "R2000";
            string text = r2000
                ? DxfDimensionDrawings.R2000.Replace("176\n0\n177\n49\n178\n", "176\n0\n178\n")
                : DxfDimensionDrawings.R2018.Replace("176\n0\n 91\n49\n178\n", "176\n0\n178\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            Assert.Equal(DxfCellAlignment.MiddleCenter, table.Alignment(1, 0));

            table.SetContentColor(1, 0, DxfColor.ByLayer);

            // Its flags in 177 before R2007 and in 91 from R2007 on, after its 176.
            string record = TableRecord(db.ToText());
            string value = r2000 ? "  1\nMark\n" : " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n 94\n0\n300\n\n302\nMark\n304\nACVALUE_END\n";
            Assert.Contains("176\n0\n" + (r2000 ? "177" : " 91") + "\n57\n178\n0\n145\n0.0\n" + value + "  7\nGH_TXT\n140\n3.5\n170\n5\n 64\n256\n171\n", record, StringComparison.Ordinal);
            DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
            DxfTable read = Table(readDb);
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Equal(3.5, read.TextHeight(1, 0));
            Assert.True(read.ContentColor(1, 0).IsByLayer);
        }

        [Fact]
        public void ACellsTurnAutofitAndKind_AreSetInItsHead()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                Assert.Equal(DxfRotationAngle.Degrees000, table.TextRotation(1, 1));
                Assert.False(table.IsAutoScale(2, 1));

                table.SetTextRotation(1, 1, DxfRotationAngle.Degrees270);
                table.SetRotation(1, 0, 0, 0.25);
                table.SetIsAutoScale(2, 1, 0, true);
                table.SetScale(2, 1, 0, 0.5);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                DxfDatabase readDb = DxfDatabase.Parse(saved);
                DxfTable read = Table(readDb);
                Assert.Equal(DxfRotationAngle.Degrees270, read.TextRotation(1, 1));
                Assert.Equal(Bits(3.0 * (Math.PI / 2.0)), Bits(read.GetRotation(1, 1, 0)));
                Assert.Equal(0.25, read.GetRotation(1, 0, 0));
                Assert.Equal(DxfRotationAngle.DegreesUnknown, read.TextRotation(1, 0));
                Assert.True(read.GetIsAutoScale(2, 1, 0));
                Assert.Equal(0.5, read.BlockScale(2, 1));
                Assert.Contains("174\n1\n175\n", TableRecord(saved), StringComparison.Ordinal);

                read.SetTextRotation(1, 1, DxfRotationAngle.Degrees270);
                read.SetAutoScale(2, 1, true);
                read.SetCellType(1, 1, DxfTableCellType.TextCell);
                Assert.Equal(saved, readDb.ToText());

                Refused<InvalidOperationException>(() => read.SetAutoScale(1, 1, true), "holds a text");
                Refused<ArgumentOutOfRangeException>(() => read.SetTextRotation(1, 1, DxfRotationAngle.DegreesUnknown), "a quarter");
                Refused<InvalidOperationException>(() => read.SetCellType(1, 1, DxfTableCellType.BlockCell), "with its block");
                Refused<ArgumentOutOfRangeException>(() => read.SetCellType(2, 1, DxfTableCellType.MultipleContentCell), "a text or a block");

                // The block cell made a text cell loses its block, its scale and its attribute values, and holds an empty text.
                read.SetCellType(2, 1, DxfTableCellType.TextCell);
                string record = TableRecord(readDb.ToText());
                Assert.DoesNotContain("\n340\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("\n331\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("\n179\n", record, StringComparison.Ordinal);
                DxfDatabase againDb = DxfDatabase.Parse(readDb.ToText());
                DxfTable again = Table(againDb);
                Assert.Equal(DxfTableCellType.TextCell, again.CellType(2, 1));
                Assert.Equal(string.Empty, again.TextString(2, 1));
                Assert.Null(again.GetBlockTableRecord(2, 1));
                again.SetTextString(2, 1, "M12");
                Assert.Equal("M12", Table(DxfDatabase.Parse(againDb.ToText())).TextString(2, 1));
            }
        }

        [Fact]
        public void ACellsOverridesTakenAway_LeaveItToItsTableStyle_AndOneItHasNotIsRefused()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, table.GetOverrides(1, 0, 0));
                Assert.Equal(DxfCellProperties.Invalid, table.GetOverrides(1, 1, 0));

                table.SetOverrides(1, 0, 0, DxfCellProperties.Alignment);
                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                DxfTable read = Table(readDb);
                Assert.Equal(DxfCellProperties.Alignment, read.GetOverrides(1, 0, 0));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
                Assert.Null(read.TextHeight(1, 0));
                Assert.Null(read.TextStyle(1, 0));
                Assert.DoesNotContain("GH_TXT", TableRecord(db.ToText()), StringComparison.Ordinal);

                Refused<ArgumentException>(() => read.SetOverrides(1, 0, 0, DxfCellProperties.Alignment | DxfCellProperties.ContentColor), "by its setter");
                string kept = readDb.ToText();
                read.SetOverrides(1, 0, 0, DxfCellProperties.Alignment);
                read.RemoveAllOverrides(1, 1);
                Assert.Equal(kept, readDb.ToText());

                read.RemoveAllOverrides(1, 0);
                DxfDatabase bareDb = DxfDatabase.Parse(readDb.ToText());
                DxfTable bare = Table(bareDb);
                Assert.Null(bare.Alignment(1, 0));
                Assert.Empty(bare.CellStyleOverrides(1, 0));
                Assert.Equal("Mark", bare.TextString(1, 0));
            }
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ACellsOwnKindOfValue_IsKept_WhenItsOverridesAreSetOrAllTakenAway(string version)
        {
            // The header cell (1, 0) of flags 49 given the flag 0x40000 of its own kind of value: 262 193.
            bool r2000 = version == "R2000";
            string flags = r2000 ? "177" : " 91";
            string text = r2000
                ? DxfDimensionDrawings.R2000.Replace("176\n0\n177\n49\n178\n", "176\n0\n177\n262193\n178\n")
                : DxfDimensionDrawings.R2018.Replace("176\n0\n 91\n49\n178\n", "176\n0\n 91\n262193\n178\n");
            DxfCellProperties all = DxfCellProperties.DataType | DxfCellProperties.Alignment | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight;
            Assert.Equal(all, Table(DxfDatabase.Parse(text)).GetOverrides(1, 0, 0));

            DxfDatabase setDb = DxfDatabase.Parse(text);
            Table(setDb).SetOverrides(1, 0, 0, DxfCellProperties.Invalid);
            DxfDatabase removedDb = DxfDatabase.Parse(text);
            Table(removedDb).RemoveAllOverrides(1, 0);
            DxfDatabase keptDb = DxfDatabase.Parse(text);
            Table(keptDb).SetOverrides(1, 0, 0, DxfCellProperties.Alignment);

            foreach (DxfDatabase db in new[] { setDb, removedDb })
            {
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                Assert.Contains("176\n0\n" + flags + "\n262144\n178\n", TableRecord(saved), StringComparison.Ordinal);
                Assert.DoesNotContain("GH_TXT", TableRecord(saved), StringComparison.Ordinal);
                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.Equal(DxfCellProperties.DataType, read.GetOverrides(1, 0, 0));
                Assert.Null(read.Alignment(1, 0));
                Assert.Equal("Mark", read.TextString(1, 0));
            }

            Assert.Equal(setDb.ToText(), removedDb.ToText());
            Assert.Equal(DxfCellProperties.DataType | DxfCellProperties.Alignment, Table(keptDb).GetOverrides(1, 0, 0));
            Assert.Contains("176\n0\n" + flags + "\n262145\n178\n", TableRecord(keptDb.ToText()), StringComparison.Ordinal);

            // Its kind alone left, nothing is taken away, given or not.
            string bare = setDb.ToText();
            Table(setDb).SetOverrides(1, 0, 0, DxfCellProperties.Invalid);
            Table(setDb).SetOverrides(1, 0, 0, DxfCellProperties.DataType);
            Table(setDb).RemoveAllOverrides(1, 0);
            Assert.Equal(bare, setDb.ToText());
        }

        [Fact]
        public void TheOverridesOfEveryCellOfATableOf16001Rows_AreTakenAwayInTimeLinearInItsGroups()
        {
            // 16 001 rows of 2 columns, every second cell of its own text style, height and alignment, flags 49.
            var cells = new System.Text.StringBuilder();

            for (int i = 0; i < 32002; i++)
            {
                cells.Append("171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n").Append(i % 2 == 0 ? "49" : "0").Append("\n178\n0\n145\n0.0\n 92\n0\n");
                cells.Append("301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nC").Append(i).Append("\n 94\n0\n300\n\n302\nC").Append(i).Append("\n304\nACVALUE_END\n");
                cells.Append(i % 2 == 0 ? "  7\nGH_TXT\n140\n3.5\n170\n5\n" : string.Empty);
            }

            string text = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1032\n  9\n$HANDSEED\n  5\n100\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n"
                + "  0\nACAD_TABLE\n  5\n50\n100\nAcDbEntity\n  8\n0\n100\nAcDbBlockReference\n  2\n*T1\n 10\n0.0\n 20\n0.0\n 30\n0.0\n100\nAcDbTable\n"
                + " 91\n16001\n 92\n2\n 93\n8\n" + string.Concat(Enumerable.Repeat("141\n8.0\n", 16001)) + "142\n40.0\n142\n30.0\n" + cells
                + " 40\n1.5\n  0\nENDSEC\n  0\nEOF\n";
            DxfDatabase cellsDb = DxfDatabase.Parse(text);
            DxfTable table = Table(cellsDb);
            DxfDatabase bothDb = DxfDatabase.Parse(text);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            table.ClearTableStyleOverrides(2);
            Table(bothDb).ClearTableStyleOverrides(0);

            watch.Stop();
            Assert.True(watch.Elapsed.TotalSeconds < 5.0, "Clearing the overrides of 32 002 cells twice took " + watch.Elapsed.TotalSeconds + " s.");
            DxfTable cleared = Table(DxfDatabase.Parse(cellsDb.ToText()));
            DxfTable both = Table(DxfDatabase.Parse(bothDb.ToText()));
            Assert.Equal(1.5, cleared.HorizontalCellMargin);
            Assert.Equal(0.06, both.HorizontalCellMargin);
            Assert.Empty(both.TableStyleOverrides());

            foreach (DxfTable read in new[] { cleared, both })
            {
                Assert.All(new[] { 0, 1, 8000, 15999, 16000 }, r =>
                {
                    Assert.Equal(DxfCellProperties.Invalid, read.GetOverrides(r, 0, 0));
                    Assert.Null(read.TextHeight(r, 0));
                    Assert.Equal("C" + (2 * r), read.TextString(r, 0));
                    Assert.Equal("C" + (2 * r + 1), read.TextString(r, 1));
                });
            }

            Assert.DoesNotContain("GH_TXT", cellsDb.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheTablesOwnOverrides_AreReadFromItsFlags_AndClearedWithOrWithoutItsCells()
        {
            string text = DxfDimensionDrawings.R2000
                .Replace("\n 92\n2\n 93\n0\n 94\n0\n", "\n 92\n2\n 93\n8\n 94\n3\n")
                .Replace("331\n4C\n300\nM12\n", "331\n4C\n300\nM12\n 40\n1.5\n 64\n1\n 65\n2\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            Assert.Equal(new[] { DxfTableStyleOverride.HorizontalCellMargin, DxfTableStyleOverride.TitleHorizontalTopColor, DxfTableStyleOverride.TitleHorizontalInsideColor }, table.TableStyleOverrides());
            Assert.Equal(1.5, table.HorizontalCellMargin);

            table.ClearTableStyleOverrides(1);
            string record = TableRecord(db.ToText());
            Assert.Contains("\n 92\n2\n 93\n0\n 94\n0\n", record, StringComparison.Ordinal);
            Assert.EndsWith("331\n4C\n300\nM12\n", record, StringComparison.Ordinal);
            DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
            DxfTable read = Table(readDb);
            Assert.Empty(read.TableStyleOverrides());
            Assert.Equal(0.06, read.HorizontalCellMargin);
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));

            read.ClearTableStyleOverrides(2);
            DxfDatabase bareDb = DxfDatabase.Parse(readDb.ToText());
            DxfTable bare = Table(bareDb);
            Assert.Null(bare.Alignment(1, 0));
            Assert.Equal("M12", bare.GetBlockAttributeValue(2, 1, bareDb.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));

            string clean = bareDb.ToText();
            bare.ClearTableStyleOverrides(0);
            Assert.Equal(clean, bareDb.ToText());
            Refused<ArgumentOutOfRangeException>(() => bare.ClearTableStyleOverrides(3), "0 for the table");
        }

        [Fact]
        public void TheCellsWalked_AreRowByRow_AndIndexedSetAsAutoCADsCells()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            Assert.Equal(new[] { "Parts", string.Empty, "Mark", "Qty", "B-12", string.Empty }, table.Select(c => c.TextString));
            Assert.Equal(new[] { "(0, 0)", "(0, 1)", "(1, 0)", "(1, 1)", "(2, 0)", "(2, 1)" }, table.Select(c => c.ToString()));
            Assert.Equal(new DxfCellRange(0, 0, 2, 1), table.Cells);
            Assert.Equal(table.Cells[1, 0], table.ElementAt(2));
            Assert.Equal(new[] { 10.0, 8.0, 8.0 }, table.Rows.Select(r => r.Height));
            Assert.Equal(30.0, table.Columns[1].Width);
            Assert.Equal(2, table.Columns.Count);

            DxfCell mark = table.Cells[1, 0];
            Assert.Equal(DxfCellAlignment.MiddleCenter, mark.Alignment);
            Assert.Equal(3.5, mark.TextHeight);
            Assert.Equal("GH_TXT", mark.TextStyle.Name);
            Assert.Equal(DxfTableCellType.BlockCell, table.Cells[2, 1].CellType);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), table.Cells[0, 1].GetMergeRange());

            mark.TextString = "Item";
            mark.TextHeight = null;
            table.Cells[2, 0].Value = 3;
            table.Cells[1, 1].Alignment = DxfCellAlignment.BottomRight;
            table.Rows[0].Height = 12.0;
            table.Columns[1].Width = 35.0;

            // A background set through the cell, then taken away with whether it is filled.
            DxfCell title = table.Cells[0, 0];
            title.BackgroundColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2);
            title.IsBackgroundColorNone = false;
            Assert.Equal(DxfCellProperties.BackgroundColor, table.GetOverrides(0, 0, 0));
            title.BackgroundColor = null;
            Assert.Equal(DxfCellProperties.Invalid, table.GetOverrides(0, 0, 0));
            Assert.Null(title.IsBackgroundColorNone);

            DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
            DxfTable read = Table(readDb);
            Assert.Equal("Item", read.TextString(1, 0));
            Assert.Null(read.TextHeight(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Equal(3, read.Value(2, 0));
            Assert.Equal(DxfCellAlignment.BottomRight, read.Alignment(1, 1));
            Assert.Equal(28.0, read.Height);
            Assert.Equal(75.0, read.Width);

            Refused<ArgumentOutOfRangeException>(() => table.Cells[3, 0], "not in the range");
            Refused<InvalidOperationException>(() => new DxfCellRange(0, 0, 1, 1)[0, 0], "of no table");
            Refused<ArgumentOutOfRangeException>(() => table.Rows[3], "3 of rows");
        }

        [Fact]
        public void APointHitsTheCellItLiesOn_SeenAlongAnyDirection_OnATableFlatTurnedOrMirrored()
        {
            DxfDatabase tableDb = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(tableDb);
            var down = new GeoVector3(0, 0, -1);

            // Columns 40 and 30 wide from x 300, rows 10, 8 and 8 high from y -100 down.
            Hit(table.HitTest(new GeoPoint3(350, -112, 5), down), 1, 1);
            Hit(table.HitTest(new GeoPoint3(310, -105, 0), down), 0, 0);
            Hit(table.HitTest(new GeoPoint3(360, -105, 0), down), 0, 0);
            Hit(table.HitTest(new GeoPoint3(340, -112, 0), down), 1, 0);
            Hit(table.HitTest(new GeoPoint3(305, -125, 10), new GeoVector3(1, 0, -1)), 2, 0);
            Hit(table.HitTest(new GeoPoint3(350, -112, 0), new GeoVector3(1, 0, 0)), 1, 1);
            Missed(table.HitTest(new GeoPoint3(350, -112, 5), new GeoVector3(1, 0, 0)));
            Missed(table.HitTest(new GeoPoint3(371, -112, 0), down));
            Missed(table.HitTest(new GeoPoint3(350, -127, 0), down));
            Refused<ArgumentException>(() => table.HitTest(new GeoPoint3(350, -112, 0), new GeoVector3(0, 0, 0)), "some length");

            // Turned a quarter about the origin: at (100, 300, 0), its columns along y and its rows along x.
            DxfDatabase turnedDb = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable turned = Table(turnedDb);
            turned.TransformBy(GeoTransform3.RotationZ(Math.PI / 2.0));
            Hit(turned.HitTest(new GeoPoint3(114, 355, 0), down), 1, 1);
            Hit(turned.HitTest(new GeoPoint3(122, 310, 0), down), 2, 0);

            // Mirrored in the plane x = 0: at (-300, -100, 0), its columns along -x, read from below.
            DxfDatabase mirroredDb = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable mirrored = Table(mirroredDb);
            mirrored.TransformBy(GeoTransform3.Scaling(-1.0, 1.0, 1.0));
            Hit(mirrored.HitTest(new GeoPoint3(-355, -114, 3), down), 1, 1);
            Hit(mirrored.HitTest(new GeoPoint3(-320, -122, 0), down), 2, 0);
            Missed(mirrored.HitTest(new GeoPoint3(-290, -114, 0), down));
        }

        [Fact]
        public void ACellsContentIsPlacedByItsAlignment_InFromItsSidesByTheMargins()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            Near(new GeoPoint3(355, -114, 0), table.AttachmentPoint(1, 1));
            Near(new GeoPoint3(320, -114, 0), table.AttachmentPoint(1, 0));
            Near(new GeoPoint3(320, -118.06, 0), table.AttachmentPoint(2, 0));
            Near(new GeoPoint3(335, -105, 0), table.AttachmentPoint(0, 1));

            table.SetAlignment(1, 1, DxfCellAlignment.TopLeft);
            Near(new GeoPoint3(340.06, -110.06, 0), table.AttachmentPoint(1, 1));
            table.SetAlignment(1, 1, DxfCellAlignment.BottomRight);
            Near(new GeoPoint3(369.94, -117.94, 0), table.Cells[1, 1].AttachmentPoint);
        }

        [Fact]
        public void ACellsContentIsPlacedFromItsTopAsItIsSeen_WhenTheRowsFlowUp()
        {
            // The rows flow up from y -100: the cell (2, 0) from y -82 to -74, its top at -74.
            string text = DxfDimensionDrawings.R2018;
            int last = text.LastIndexOf("304\nACVALUE_END\n", StringComparison.Ordinal) + "304\nACVALUE_END\n".Length;
            DxfDatabase db = DxfDatabase.Parse(text.Insert(last, " 70\n1\n"));
            DxfTable table = Table(db);
            Assert.Equal(DxfFlowDirection.BottomToTop, table.FlowDirection);
            Near(new GeoPoint3(300, -82, 0), table.GetCellExtents(2, 0, true)[0]);
            Near(new GeoPoint3(300, -74, 0), table.GetCellExtents(2, 0, true)[3]);

            Near(new GeoPoint3(320, -74.06, 0), table.AttachmentPoint(2, 0));
            Near(new GeoPoint3(355, -86, 0), table.AttachmentPoint(1, 1));
            table.SetAlignment(2, 0, DxfCellAlignment.TopCenter);
            Near(new GeoPoint3(320, -74.06, 0), table.AttachmentPoint(2, 0));
            table.SetAlignment(2, 0, DxfCellAlignment.BottomLeft);
            Near(new GeoPoint3(300.06, -81.94, 0), table.AttachmentPoint(2, 0));
            table.SetAlignment(1, 1, DxfCellAlignment.TopRight);
            Near(new GeoPoint3(369.94, -82.06, 0), table.Cells[1, 1].AttachmentPoint);
        }

        [Fact]
        public void AMergedBlocksContent_IsPlacedByItsTopLeftCell_FromEveryCellOfIt()
        {
            // The title, (0, 0) to (0, 1), of its own alignment in its top left cell.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetAlignment(0, 0, DxfCellAlignment.TopLeft);

            Near(new GeoPoint3(300.06, -100.06, 0), table.AttachmentPoint(0, 0));
            Near(new GeoPoint3(300.06, -100.06, 0), table.AttachmentPoint(0, 1));
            Near(new GeoPoint3(300.06, -100.06, 0), table.Cells[0, 1].AttachmentPoint);
        }

        [Fact]
        public void TheBlockOfMergedCellsACellIsIn_GivesTheCellsOfItsTable()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            Assert.Equal("Parts", table.GetMergeRange(0, 1)[0, 0].TextString);
            Assert.Equal(table.Cells[0, 1], table.GetMergeRange(0, 1)[0, 1]);
            Assert.True(table.IsMergedCell(0, 1, out DxfCellRange title));
            Assert.Equal("Parts", title[0, 0].TextString);
            Assert.Equal("Qty", table.Cells[1, 1].GetMergeRange()[1, 1].TextString);
            Refused<ArgumentOutOfRangeException>(() => table.GetMergeRange(1, 1)[1, 0], "not in the range");
        }

        private static void Hit(DxfTableHitTestInfo hit, int row, int column)
        {
            Assert.Equal(DxfTableHitTestType.Cell, hit.Type);
            Assert.Equal(row, hit.Row);
            Assert.Equal(column, hit.Column);
        }

        private static void Missed(DxfTableHitTestInfo hit)
        {
            Assert.Equal(DxfTableHitTestType.None, hit.Type);
            Assert.Equal(-1, hit.Row);
            Assert.Equal(-1, hit.Column);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.Equal(expected.X, actual.X, 9);
            Assert.Equal(expected.Y, actual.Y, 9);
            Assert.Equal(expected.Z, actual.Z, 9);
        }

        private static void Refused<T>(Func<object> action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }
    }
}
