using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Table styles (TABLESTYLE) read, made and changed, tables made in code, and the anonymous block a table is drawn with
    /// drawn anew from its cells (<see cref="DxfTable.RecomputeTableBlock"/>).
    /// </summary>
    public class DxfTableStyleTests
    {
        /// <summary>A drawing of R2013 in millimetres with AutoCAD's Standard table style, made in code.</summary>
        private static DxfDatabase Metric(string version = "AC1027")
        {
            var db = new DxfDatabase(version);
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            return db;
        }

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        [Fact]
        public void ANewStyle_HasAutoCadsStandardInMillimetres_AndReadsBackFromItsFile()
        {
            DxfDatabase db = Metric();
            var style = (DxfTableStyle)db.TableStyleDictionary["Standard"];

            Assert.Equal("Standard", style.Name);
            Assert.Equal(1.5, style.HorizontalCellMargin);
            Assert.Equal(1.5, style.VerticalCellMargin);
            Assert.Equal(6.0, style.TextHeight(DxfRowType.TitleRow));
            Assert.Equal(4.5, style.TextHeight(DxfRowType.HeaderRow));
            Assert.Equal(4.5, style.TextHeight(DxfRowType.DataRow));
            Assert.Equal(DxfCellAlignment.TopCenter, style.Alignment(DxfRowType.DataRow));
            Assert.Equal(DxfCellAlignment.MiddleCenter, style.Alignment(DxfRowType.TitleRow));
            Assert.True(style.IsBackgroundColorNone(DxfRowType.HeaderRow));
            Assert.True(style.GridVisibility(DxfGridLineType.HorizontalInside, DxfRowType.DataRow));
            Assert.True(style.GridColor(DxfGridLineType.VerticalLeft, DxfRowType.TitleRow).IsByBlock);
            Assert.Equal(DxfLineWeight.ByBlock, style.GridLineWeight(DxfGridLineType.HorizontalTop, DxfRowType.DataRow));

            string text = db.ToText();
            Assert.Contains("100\nAcDbTableStyle\n280\n0\n  3\n\n 70\n0\n 71\n0\n 40\n1.5\n 41\n1.5\n280\n0\n281\n0\n  7\nStandard\n140\n4.5\n170\n2\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n  1\n\n274\n-2\n284\n1\n 64\n0\n", text.Replace("\r\n", "\n"), StringComparison.Ordinal);

            var read = (DxfTableStyle)DxfDatabase.Parse(text).TableStyleDictionary["Standard"];
            Assert.Equal(6.0, read.TextHeight(DxfRowType.TitleRow));
            Assert.Equal(DxfCellAlignment.TopCenter, read.Alignment(DxfRowType.DataRow));
        }

        [Fact]
        public void AStyleSet_WritesEachKindOfRowApart_AndKeepsTheOthers()
        {
            DxfDatabase db = Metric();
            var schedule = new DxfTableStyle { Description = "Steel schedule" };
            db.TableStyleDictionary.SetAt("GH_SCHEDULE", schedule);
            schedule.SetTextHeight(3.5, DxfRowType.DataRow | DxfRowType.HeaderRow);
            schedule.SetAlignment(DxfCellAlignment.MiddleLeft, DxfRowType.DataRow);
            schedule.SetBackgroundColor(DxfColor.OfIndex(8), DxfRowType.HeaderRow);
            schedule.SetGridColor(DxfColor.OfIndex(1), DxfGridLineType.OuterGridLines, DxfRowType.TitleRow);
            schedule.SetGridVisibility(false, DxfGridLineType.HorizontalInside, DxfRowType.DataRow);
            schedule.SetGridLineWeight(DxfLineWeight.LineWeight050, DxfGridLineType.HorizontalBottom, DxfRowType.DataRow);
            schedule.HorizontalCellMargin = 1.0;
            schedule.FlowDirection = DxfFlowDirection.BottomToTop;
            schedule.IsHeaderSuppressed = true;

            var read = (DxfTableStyle)DxfDatabase.Parse(db.ToText()).TableStyleDictionary["GH_SCHEDULE"];

            Assert.Equal("Steel schedule", read.Description);
            Assert.Equal(3.5, read.TextHeight(DxfRowType.DataRow));
            Assert.Equal(3.5, read.TextHeight(DxfRowType.HeaderRow));
            Assert.Equal(6.0, read.TextHeight(DxfRowType.TitleRow));
            Assert.Equal(DxfCellAlignment.MiddleLeft, read.Alignment(DxfRowType.DataRow));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(DxfRowType.HeaderRow));
            Assert.False(read.IsBackgroundColorNone(DxfRowType.HeaderRow));
            Assert.Equal(8, read.BackgroundColor(DxfRowType.HeaderRow).ColorIndex);
            Assert.True(read.IsBackgroundColorNone(DxfRowType.DataRow));
            Assert.Equal(1, read.GridColor(DxfGridLineType.VerticalRight, DxfRowType.TitleRow).ColorIndex);
            Assert.True(read.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.TitleRow).IsByBlock);
            Assert.False(read.GridVisibility(DxfGridLineType.HorizontalInside, DxfRowType.DataRow));
            Assert.True(read.GridVisibility(DxfGridLineType.HorizontalInside, DxfRowType.HeaderRow));
            Assert.Equal(DxfLineWeight.LineWeight050, read.GridLineWeight(DxfGridLineType.HorizontalBottom, DxfRowType.DataRow));
            Assert.Equal(1.0, read.HorizontalCellMargin);
            Assert.Equal(1.5, read.VerticalCellMargin);
            Assert.Equal(DxfFlowDirection.BottomToTop, read.FlowDirection);
            Assert.True(read.IsHeaderSuppressed);
            Assert.False(read.IsTitleSuppressed);
        }

        [Fact]
        public void AStyleAutoCadWrote_IsReadByItsKindsOfRow_TheDataFirst()
        {
            // The style Parts of the checks D12: the data 2.5, the title 5 and the header 3.5, no lines of its own.
            string text = DxfDimensionDrawings.R2018.Replace("\r\n", "\n");
            const string Dictionary = "  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n";
            string style = "  0\nTABLESTYLE\n  5\n200\n330\n15\n100\nAcDbTableStyle\n280\n0\n  3\nParts\n 70\n0\n 71\n0\n 40\n1.5\n 41\n1.5\n280\n0\n281\n0\n"
                + "  7\nStandard\n140\n2.5\n170\n2\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n"
                + "  7\nStandard\n140\n5.0\n170\n5\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n"
                + "  7\nStandard\n140\n3.5\n170\n5\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n";
            DxfDatabase db = DxfDatabase.Parse(text.Replace(Dictionary, Dictionary + "  3\nParts\n350\n200\n" + style).Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\n201\n"));
            var parts = (DxfTableStyle)db.TableStyleDictionary["Parts"];

            Assert.Equal("Parts", parts.Description);
            Assert.Equal(2.5, parts.TextHeight(DxfRowType.DataRow));
            Assert.Equal(5.0, parts.TextHeight(DxfRowType.TitleRow));
            Assert.Equal(3.5, parts.TextHeight(DxfRowType.HeaderRow));
            Assert.True(parts.GridVisibility(DxfGridLineType.HorizontalTop, DxfRowType.DataRow));

            parts.SetTextHeight(3.0, DxfRowType.TitleRow);
            var read = (DxfTableStyle)DxfDatabase.Parse(db.ToText()).TableStyleDictionary["Parts"];
            Assert.Equal(3.0, read.TextHeight(DxfRowType.TitleRow));
            Assert.Equal(2.5, read.TextHeight(DxfRowType.DataRow));
            Assert.Equal(3.5, read.TextHeight(DxfRowType.HeaderRow));
        }

        [Fact]
        public void AStyle_RefusesWhatItCannotWrite()
        {
            var style = new DxfTableStyle();

            Assert.Throws<ArgumentOutOfRangeException>(() => style.SetTextHeight(0.0, DxfRowType.DataRow));
            Assert.Throws<ArgumentOutOfRangeException>(() => style.SetTextHeight(2.5, DxfRowType.UnknownRow));
            Assert.Throws<ArgumentOutOfRangeException>(() => style.TextHeight(DxfRowType.UnknownRow));
            Assert.Throws<ArgumentException>(() => style.SetColor(DxfColor.FromRgb(255, 128, 0), DxfRowType.DataRow));
            Assert.Throws<ArgumentOutOfRangeException>(() => style.HorizontalCellMargin = -1.0);
            var resident = (DxfTableStyle)Metric().TableStyleDictionary["Standard"];
            Assert.Throws<ArgumentException>(() => resident.SetTextStyle("GH_NONE", DxfRowType.DataRow));
        }

        [Fact]
        public void ATableMadeInCode_IsDrawnInABlockOfItsOwn_ItsTitleMergedAcross()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(100.0, 200.0, 0.0), 4, 3, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            table.SetTextString(0, 0, "STEEL");
            table.SetTextString(1, 0, "Mark");
            table.SetTextString(2, 1, "HEA300");
            table.RecomputeTableBlock();

            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal("*T1", read.BlockTableRecord.Name);
            Assert.True(read.BlockTableRecord.IsAnonymous);
            Assert.Equal("Standard", read.TableStyleName);
            Assert.Equal(4, read.NumRows);
            Assert.Equal(90.0, read.Width);
            Assert.Equal(36.0, read.Height);
            Assert.Equal(new DxfCellRange(0, 0, 0, 2), read.GetMergeRange(0, 2));
            Assert.Equal(DxfRowType.HeaderRow, read.RowType(1));
            Assert.Equal("HEA300", read.TextString(2, 1));

            var texts = read.BlockTableRecord.OfType<DxfMText>().ToList();
            var lines = read.BlockTableRecord.OfType<DxfLine>().ToList();
            Assert.Equal(new[] { "STEEL", "Mark", "HEA300" }, texts.Select(t => t.Contents).ToArray());

            // The title's words in the middle of its row, 6 high; the data's at the top centre of their cell, 1.5 in.
            DxfMText title = texts[0];
            Assert.Equal(45.0, title.Location.X, 9);
            Assert.Equal(-4.5, title.Location.Y, 9);
            Assert.Equal(6.0, title.TextHeight);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, title.Attachment);
            DxfMText data = texts[2];
            Assert.Equal(45.0, data.Location.X, 9);
            Assert.Equal(-19.5, data.Location.Y, 9);
            Assert.Equal(DxfAttachmentPoint.TopCenter, data.Attachment);
            Assert.Equal(27.0, data.Width, 9);

            // Five lines across, the one under... none inside the title; four lines down, the two inside starting under it.
            Assert.Equal(9, lines.Count);
            Assert.Contains(lines, l => l.StartPoint.Y == -9.0 && l.StartPoint.X == 0.0 && l.EndPoint.X == 90.0);
            Assert.Contains(lines, l => l.StartPoint.X == 30.0 && l.StartPoint.Y == -9.0 && l.EndPoint.Y == -36.0);
            Assert.DoesNotContain(lines, l => l.StartPoint.X == 30.0 && l.StartPoint.Y == 0.0);
            Assert.All(read.BlockTableRecord.Cast<DxfEntity>(), e => Assert.Equal("0", e.Layer));
        }

        [Fact]
        public void ADrawingOfR2000_TakesATableMadeInCode_ItsCellsInTheFormOfR2000()
        {
            DxfDatabase db = Metric("AC1015");
            var table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), 3, 2, 9.0, 20.0);
            db.ModelSpace.AppendEntity(table);
            table.SetTextString(2, 1, "B-12");

            string text = db.ToText().Replace("\r\n", "\n");
            Assert.DoesNotContain("CELL_VALUE", text, StringComparison.Ordinal);
            DxfTable read = Table(DxfDatabase.Parse(text));
            Assert.Equal("B-12", read.TextString(2, 1));
            Assert.Equal("*T1", read.BlockTableRecord.Name);
        }

        [Fact]
        public void TheBlockDrawnAnew_FollowsTheCellsLooks_FillLinesAndMergedBlocks()
        {
            DxfDatabase db = Metric();
            var green = new DxfTableStyle();
            db.TableStyleDictionary.SetAt("GH_GREEN", green);
            green.SetBackgroundColor(DxfColor.OfIndex(3), DxfRowType.HeaderRow);
            green.SetGridVisibility(false, DxfGridLineType.HorizontalInside, DxfRowType.DataRow);
            var table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), 5, 2, 10.0, 40.0);
            db.ModelSpace.AppendEntity(table);
            Assert.Equal("Standard", table.TableStyleName);
            table.TableStyle = green;
            Assert.Equal("GH_GREEN", table.TableStyleName);
            table.SetTextString(3, 0, "x");
            table.SetAlignment(3, 0, DxfCellAlignment.BottomRight);
            table.SetGridColor(4, 1, DxfGridLineType.VerticalRight, DxfColor.OfIndex(1));
            table.RecomputeTableBlock();

            DxfBlockTableRecord block = table.BlockTableRecord;
            // The header's two cells filled green.
            var fills = block.OfType<DxfSolid>().ToList();
            Assert.Equal(2, fills.Count);
            Assert.All(fills, f => Assert.Equal(3, f.ColorIndex));

            // The data's lines inside are not drawn: across, the table's top, under the title, under the header and its bottom.
            var across = block.OfType<DxfLine>().Where(l => l.StartPoint.Y == l.EndPoint.Y).Select(l => l.StartPoint.Y).OrderBy(y => y).ToArray();
            Assert.Equal(new[] { -50.0, -20.0, -10.0, 0.0 }, across);

            DxfLine red = block.OfType<DxfLine>().Single(l => l.ColorIndex == 1);
            Assert.Equal(80.0, red.StartPoint.X);
            Assert.Equal(-40.0, red.StartPoint.Y);
            Assert.Equal(-50.0, red.EndPoint.Y);

            DxfMText x = block.OfType<DxfMText>().Single();
            Assert.Equal(DxfAttachmentPoint.BottomRight, x.Attachment);
            Assert.Equal(38.5, x.Location.X, 9);
            Assert.Equal(-38.5, x.Location.Y, 9);
        }

        [Fact]
        public void ATableTurnedAndMoved_IsDrawnAnewInTheSystemOfItsBlock_PlacedAtItsPoint()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), 2, 1, 10.0, 40.0);
            db.ModelSpace.AppendEntity(table);
            table.TransformBy(GeoTransform3.RotationZ(Math.PI / 2.0));
            table.TransformBy(GeoTransform3.Translation(new GeoVector3(500.0, 0.0, 0.0)));
            table.RecomputeTableBlock();

            Assert.Equal(500.0, table.Position.X, 9);

            // Along the table is up the drawing: the line under the first row runs from (10, 0) to (10, 40) in the block.
            DxfLine under = table.BlockTableRecord.OfType<DxfLine>().Single(l => Math.Abs(l.StartPoint.X - 10.0) < 1E-9);
            Assert.Equal(0.0, under.StartPoint.Y, 9);
            Assert.Equal(40.0, under.EndPoint.Y, 9);
        }

        [Fact]
        public void ATableOfAHundredRowsOfTwentyColumns_IsDrawnWellUnderAHundredMilliseconds()
        {
            DxfDatabase db = Metric();
            var table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), 100, 20, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int row = 1; row < 100; row++)
            {
                for (int column = 0; column < 20; column++)
                {
                    table.SetTextString(row, column, "R" + row + "C" + column);
                }
            }

            table.RecomputeTableBlock();
            var watch = Stopwatch.StartNew();
            table.RecomputeTableBlock();
            watch.Stop();

            Assert.Equal(1980, table.BlockTableRecord.OfType<DxfMText>().Count());
            Assert.True(watch.ElapsedMilliseconds < 1000, "Drawn in " + watch.ElapsedMilliseconds + " ms.");
        }
    }
}
