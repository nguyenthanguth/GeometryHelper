using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The ACAD_TABLE of <see cref="DxfDimensionDrawings"/> (R2000 and R2018): its rows, columns and cells read, a cell's text,
    /// a row's height and a column's width set, the table moved, and a copy readied for another drawing.
    /// </summary>
    public class DxfTableTests
    {
        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        [Fact]
        public void TheTable_IsReadRowByRowAndCellByCell_InBothForms()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                Assert.Equal("F6", table.Handle);
                Assert.Equal(3, table.NumRows);
                Assert.Equal(2, table.NumColumns);
                Assert.Equal(10.0, table.RowHeight(0));
                Assert.Equal(8.0, table.RowHeight(2));
                Assert.Equal(30.0, table.ColumnWidth(1));
                Assert.Equal(26.0, table.Height);
                Assert.Equal(70.0, table.Width);
                Assert.Equal(Bits(new GeoPoint3(300.0, -100.0, 0.0)), Bits(table.Position));
                Assert.Equal(Bits(new GeoVector3(1.0, 0.0, 0.0)), Bits(table.Direction));
                Assert.Equal("*T12", table.BlockTableRecord.Name);
                Assert.Null(table.TableStyleName);
                Assert.Equal(DxfFlowDirection.TopToBottom, table.FlowDirection);
                Assert.Equal(0.06, table.HorizontalCellMargin);
                Assert.False(table.IsTitleSuppressed);

                Assert.Equal(DxfRowType.TitleRow, table.RowType(0));
                Assert.Equal(DxfRowType.HeaderRow, table.RowType(1));
                Assert.Equal(DxfRowType.DataRow, table.RowType(2));

                Assert.Equal("Parts", table.TextString(0, 0));
                Assert.Equal(string.Empty, table.TextString(0, 1));
                Assert.Equal("Mark", table.TextString(1, 0));
                Assert.Equal("Qty", table.GetTextString(1, 1, 0));
                Assert.Equal("B-12", table.Value(2, 0));
                Assert.Equal(DxfTableCellType.TextCell, table.CellType(2, 0));
                Assert.Equal(DxfTableCellType.BlockCell, table.CellType(2, 1));
                Assert.True(table.IsEmpty(0, 1));
                Assert.False(table.IsEmpty(2, 1));
                Assert.Equal(1, table.GetNumberOfContents(2, 1));

                Assert.True(table.IsMergedCell(0, 1, out DxfCellRange range));
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), range);
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), table.GetMergeRange(0, 0));
                Assert.False(table.IsMergedCell(1, 1, out DxfCellRange alone));
                Assert.Equal(new DxfCellRange(1, 1, 1, 1), alone);

                Assert.Equal(DxfCellAlignment.MiddleCenter, table.Alignment(1, 0));
                Assert.Equal(3.5, table.TextHeight(1, 0));
                Assert.Equal("GH_TXT", table.TextStyle(1, 0).Name);
                Assert.Null(table.Alignment(1, 1));
                Assert.Null(table.TextHeight(2, 0));
                Assert.Null(table.ContentColor(1, 0));
                Assert.Equal(new[] { DxfTableStyleOverride.CellAlignment, DxfTableStyleOverride.CellTextStyle, DxfTableStyleOverride.CellTextHeight }, table.CellStyleOverrides(1, 0));
                Assert.Empty(table.CellStyleOverrides(1, 1));

                Assert.Equal("BOLT", table.GetBlockTableRecord(2, 1).Name);
                Assert.Null(table.GetBlockTableRecord(2, 0));
                Assert.Equal(1.0, table.BlockScale(2, 1));
                DxfAttributeDefinition size = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
                Assert.Equal("M12", table.GetBlockAttributeValue(2, 1, size));

                Refused<ArgumentOutOfRangeException>(() => table.TextString(3, 0), "3 of rows");
                Refused<ArgumentOutOfRangeException>(() => table.RowHeight(-1), "3 of rows");
                Refused<ArgumentOutOfRangeException>(() => table.GetTextString(0, 0, 1), "one content");
            }
        }

        [Fact]
        public void ACellsTextSet_IsWrittenInItsForm_TheRestKept()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                string longText = new string('x', 300);

                table.SetTextString(1, 1, "Count");
                table.SetTextString(2, 0, 0, longText);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                string record = Records(saved).Single(r => HandleOf(r) == "F6");

                if (text == DxfDimensionDrawings.R2018)
                {
                    Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nCount\n 94\n0\n300\n\n302\nCount\n304\nACVALUE_END\n", record, StringComparison.Ordinal);
                }
                else
                {
                    Assert.Contains("145\n0.0\n  1\nCount\n171\n", record, StringComparison.Ordinal);
                }

                Assert.Contains("\n  2\n" + new string('x', 250) + "\n  1\n" + new string('x', 50) + "\n", record, StringComparison.Ordinal);

                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.Equal("Count", read.TextString(1, 1));
                Assert.Equal(longText, read.TextString(2, 0));
                Assert.Equal("Mark", read.TextString(1, 0));
                Assert.Equal("Parts", read.TextString(0, 0));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
                Refused<InvalidOperationException>(() => read.SetTextString(2, 1, "x"), "holds a block");
                Refused<ArgumentException>(() => read.SetTextString(0, 0, "a\nb"), "one line");

                // The block AutoCAD drew the table with is left as it was.
                Assert.Equal(Records(text).Where(r => r.Contains("\n330\nE6\n")), Records(saved).Where(r => r.Contains("\n330\nE6\n")));
            }
        }

        [Fact]
        public void TheSizesSet_AreWrittenInTheHead_AndEverySetterGivenWhatItHasChangesNothing()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.SetTextString(0, 0, table.TextString(0, 0));
            table.SetRowHeight(1, table.RowHeight(1));
            table.SetColumnWidth(0, table.ColumnWidth(0));
            Assert.Equal(text, db.ToText());

            table.SetRowHeight(1, 12.5);
            table.SetColumnWidth(25.0);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "F6");
            DxfTable read = Table(DxfDatabase.Parse(saved));
            Assert.Equal(12.5, read.RowHeight(1));
            Assert.Equal(10.0, read.RowHeight(0));
            Assert.Equal(50.0, read.Width);
            Assert.Equal(30.5, read.Height);
            Refused<ArgumentOutOfRangeException>(() => read.SetRowHeight(0, 0.0), "above nought");
        }

        [Fact]
        public void ACellsCorners_AreItsColumnsAndRowsLaidFromThePoint_AndABlockCellsDataIsSetInPlace()
        {
            string text = DxfDimensionDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            Assert.Equal(new[] { new GeoPoint3(340, -110, 0), new GeoPoint3(370, -110, 0), new GeoPoint3(370, -118, 0), new GeoPoint3(340, -118, 0) }, table.GetCellExtents(1, 1, false));
            Assert.Equal(new[] { new GeoPoint3(300, -100, 0), new GeoPoint3(370, -100, 0), new GeoPoint3(370, -110, 0), new GeoPoint3(300, -110, 0) }, table.GetCellExtents(0, 1, true));
            Assert.Null(table.TableStyle);

            DxfAttributeDefinition size = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            table.SetBlockAttributeValue(2, 1, size, size.TextString);
            table.SetBlockScale(2, 1, 1.0);
            Assert.Equal(text, db.ToText());

            table.SetBlockAttributeValue(2, 1, size, "M16");
            table.SetBlockScale(2, 1, 0.5);
            table.SetBlockRotation(2, 1, Math.PI / 2.0);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "F6");
            DxfTable read = Table(DxfDatabase.Parse(saved));
            Assert.Equal("M16", read.GetBlockAttributeValue(2, 1, size));
            Assert.Equal(0.5, read.GetScale(2, 1, 0));
            Assert.Equal(Math.PI / 2.0, read.BlockRotation(2, 1));
            Refused<InvalidOperationException>(() => read.SetBlockScale(2, 0, 2.0), "holds a text");
        }

        [Fact]
        public void TheTableTurned_TurnsItsBlockWithIt_AndAScaleIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            table.TransformBy(GeoTransform3.Translation(new GeoVector3(5.0, 0.0, 0.0)) * GeoTransform3.RotationZ(Math.PI / 2.0));

            Assert.Equal(100.0 + 5.0, table.Position.X, 12);
            Assert.Equal(300.0, table.Position.Y, 12);
            Assert.Equal(0.0, table.Direction.X, 15);
            Assert.Equal(1.0, table.Direction.Y, 15);
            DxfLine top = table.BlockTableRecord.OfType<DxfLine>().First();
            Assert.Equal(0.0, top.EndPoint.X, 12);
            Assert.Equal(70.0, top.EndPoint.Y, 12);
            Refused<ArgumentException>(() => table.TransformBy(GeoTransform3.Scaling(2.0)), "moved and turned only");
            Assert.Equal("Parts", Table(DxfDatabase.Parse(db.ToText())).TextString(0, 0));
        }

        [Fact]
        public void ACopyForAnotherDrawing_LosesItsAttributeValuesWithTheirCount_AndOneForItsOwnKeepsThem()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(source);

            DxfEntity same = table.Clone();
            source.ModelSpace.AppendEntity(same);
            Assert.Contains("179\n1\n331\n4C\n300\nM12\n", Records(source.ToText()).Single(r => HandleOf(r) == same.Handle), StringComparison.Ordinal);

            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = table.Clone();
            other.ModelSpace.AppendEntity(copy);

            string record = Records(other.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains("\n179\n0\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n331\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n300\nM12\n", record, StringComparison.Ordinal);
            DxfTable read = DxfDatabase.Parse(other.ToText()).ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal("B-12", read.TextString(2, 0));
            Assert.Equal(DxfTableCellType.BlockCell, read.CellType(2, 1));
        }

        [Fact]
        public void ACopyInItsOwnDrawing_SharesThePicture_SoTurningItLeavesTheOriginalsBlock()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            var copy = (DxfTable)table.Clone();
            db.ModelSpace.AppendEntity(copy);
            DxfLine line = db.BlockTable["*T12"].OfType<DxfLine>().Single(l => l.Handle == "EA");

            copy.TransformBy(GeoTransform3.RotationZ(Math.PI / 2.0));

            Assert.Equal(Bits(new GeoPoint3(70.0, -10.0, 0.0)), Bits(line.EndPoint));
            Assert.Equal(0.0, copy.Direction.X, 15);
            Assert.Equal(Bits(new GeoVector3(1.0, 0.0, 0.0)), Bits(table.Direction));
        }

        [Fact]
        public void ATableTilted_TurnsItsPictureOnce_WhereItsCellsNowStand()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            DxfLine line = db.BlockTable["*T12"].OfType<DxfLine>().Single(l => l.Handle == "EA");

            table.TransformBy(GeoTransform3.RotationX(Math.PI / 2.0));

            // The line under the title, its end at the bottom right corner of the cell (0, 1): (370, 0, -110) in the world.
            GeoPoint3 corner = table.GetCellExtents(0, 1, false)[2];
            Assert.Equal(370.0, corner.X, 9);
            Assert.Equal(0.0, corner.Y, 9);
            Assert.Equal(-110.0, corner.Z, 9);
            GeoPoint3 end = Placed(table, line.EndPoint);
            Assert.Equal(corner.X, end.X, 9);
            Assert.Equal(corner.Y, end.Y, 9);
            Assert.Equal(corner.Z, end.Z, 9);
        }

        [Fact]
        public void TheCellsBorderFlags_AreAutoCADsOverrides_GroupedByWhatTheyHold()
        {
            string text = DxfDimensionDrawings.R2000.Replace("177\n49\n", "177\n177\n");
            DxfTable table = Table(DxfDatabase.Parse(text));

            // 177 = 1 + 0x10 + 0x20 + 0x80: the alignment, the text style and height, and the colour of the right border.
            Assert.Equal(new[] { DxfTableStyleOverride.CellAlignment, DxfTableStyleOverride.CellTextStyle, DxfTableStyleOverride.CellTextHeight, DxfTableStyleOverride.CellRightGridColor }, table.CellStyleOverrides(1, 0));
        }

        [Fact]
        public void TheTablesOwnPart_StandsAfterItsLastCell_AndIsNotTakenForTheCells()
        {
            string text = DxfDimensionDrawings.R2000.Replace("331\n4C\n300\nM12\n", "331\n4C\n300\nM12\n 70\n1\n280\n1\n170\n4\n  7\nGH_TXT2\n");
            DxfTable table = Table(DxfDatabase.Parse(text));

            Assert.Equal(DxfFlowDirection.BottomToTop, table.FlowDirection);
            Assert.True(table.IsTitleSuppressed);
            Assert.Equal(DxfRowType.HeaderRow, table.RowType(0));
            Assert.Null(table.Alignment(2, 1));
            Assert.Null(table.TextStyle(2, 1));
            Assert.Equal("M12", table.GetBlockAttributeValue(2, 1, DxfDatabase.Parse(text).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
        }

        [Fact]
        public void ASizeTheFileHoldsOutOfRange_SetAsItIsRead_WritesNothing()
        {
            string text = DxfDimensionDrawings.R2000.Replace("141\n10.0\n", "141\n0.0\n").Replace("340\n48\n144\n1.0\n", "340\n48\n144\n0.0\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.SetRowHeight(0, table.RowHeight(0));
            table.SetBlockScale(2, 1, table.BlockScale(2, 1));
            Assert.Equal(0.0, table.RowHeight(0));
            Assert.Equal(0.0, table.BlockScale(2, 1));
            Assert.Equal(text, db.ToText());
            Refused<ArgumentOutOfRangeException>(() => table.SetBlockScale(2, 1, -1.0), "above nought");
        }

        [Fact]
        public void EveryCellOfALongTable_IsReadInTimeLinearInItsCells()
        {
            // 1 000 rows of 2 columns: the record is read once for all of them, not once a cell.
            string cells = string.Concat(Enumerable.Range(0, 2000).Select(i => "171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n177\n0\n178\n0\n145\n0.0\n  1\nC" + i + "\n"));
            string rows = string.Concat(Enumerable.Repeat("141\n8.0\n", 1000));
            string text = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1015\n  9\n$HANDSEED\n  5\n100\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n"
                + "  0\nACAD_TABLE\n  5\n50\n100\nAcDbEntity\n  8\n0\n100\nAcDbBlockReference\n  2\n*T1\n 10\n0.0\n 20\n0.0\n 30\n0.0\n100\nAcDbTable\n"
                + " 91\n1000\n 92\n2\n" + rows + "142\n40.0\n142\n30.0\n" + cells + "  0\nENDSEC\n  0\nEOF\n";
            DxfTable table = Table(DxfDatabase.Parse(text));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int length = 0;

            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    length += table.TextString(r, c).Length;
                }
            }

            Assert.Equal("C1999", table.TextString(999, 1));
            Assert.Equal(8890, length);
            Assert.True(watch.Elapsed.TotalSeconds < 5.0, "2 000 cells took " + watch.Elapsed.TotalSeconds + " s.");
        }

        private static GeoPoint3 Placed(DxfTable table, GeoPoint3 point)
        {
            GeoVector3 n = table.Normal;
            GeoVector3 x = Math.Abs(n.X) < 1.0 / 64.0 && Math.Abs(n.Y) < 1.0 / 64.0 ? new GeoVector3(0, 1, 0).CrossProduct(n) : new GeoVector3(0, 0, 1).CrossProduct(n);
            x = x.Divide(x.Length);
            GeoVector3 y = n.CrossProduct(x);
            y = y.Divide(y.Length);
            GeoPoint3 p = table.Position;
            return new GeoPoint3(p.X + x.X * point.X + y.X * point.Y + n.X * point.Z, p.Y + x.Y * point.X + y.Y * point.Y + n.Y * point.Z, p.Z + x.Z * point.X + y.Z * point.Y + n.Z * point.Z);
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }
    }
}
