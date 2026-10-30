using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfDBTextTests;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The MTEXT as a <see cref="DxfMText"/>: read as ezdxf 1.4.5 reads it, set and read back to the bit, made in code and
    /// appended where the version has MTEXT, erased, and moved as ezdxf moves one.
    /// </summary>
    public class DxfMTextTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheMTextsOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads the MTEXT 73: at (20, 40, 0), 3 high, 40 wide, held at its top left, plain 'Bold\nline two é'.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                Assert.Equal(Bits(new GeoPoint3(20.0, 40.0, 0.0)), Bits(mtext.Location));
                Assert.Equal(3.0, mtext.TextHeight);
                Assert.Equal(40.0, mtext.Width);
                Assert.Equal(DxfAttachmentPoint.TopLeft, mtext.Attachment);
                Assert.Equal("Bold\nline two é", mtext.Text);
                Assert.StartsWith("{\\f", mtext.Contents, StringComparison.Ordinal);
                Assert.Equal(0.0, mtext.Rotation);
                Assert.Equal(Bits(new GeoVector3(1, 0, 0)), Bits(mtext.Direction));
                Assert.Equal(DxfFlowDirection.LeftToRight, mtext.FlowDirection);
                Assert.Equal(DxfLineSpacingStyle.AtLeast, mtext.LineSpacingStyle);
                Assert.Equal(1.0, mtext.LineSpacingFactor);
                Assert.Equal("Standard", mtext.TextStyleName);
                Assert.False(mtext.BackgroundFill);
                Assert.Null(mtext.BackgroundFillColor);
                Assert.Equal(DxfColumnType.NoColumns, mtext.ColumnType);
                Assert.Equal(0, mtext.ColumnCount);

                var theirs = (DxfText)DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tolerance.Default }).Shapes.Single(s => s.Handle == "73").Shape;
                var ours = (DxfText)Assert.Single(mtext.GetGeometry());
                Assert.Equal(Bits(theirs.Position), Bits(ours.Position));
                Assert.Equal(theirs.Value, ours.Value);
            }
        }

        [Fact]
        public void TheColumnsOfAnEmbeddedObject_AreReadFromIt_NotTakenForTheMTextsOwnGroups()
        {
            // ezdxf 1.4.5 writes an MTEXT of R2018 set in 3 static columns 10 wide, 2 apart, with an embedded object (101)
            // that repeats its location (11) and width (40): the MTEXT's own groups are read before it.
            string text = DxfFileEzdxfDrawings.R2018.Replace(
                "  0\nMTEXT\n  5\n73\n",
                "  0\nMTEXT\n  5\nF73\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbMText\n 10\n1\n 20\n2\n 30\n0\n 40\n2.5\n 41\n10\n 71\n1\n  1\ncolumns\n101\nEmbedded Object\n 70\n1\n 10\n1\n 20\n0\n 30\n0\n 11\n1\n 21\n2\n 31\n0\n 40\n10\n 41\n0\n 42\n34\n 43\n5\n 71\n1\n 72\n3\n 44\n10\n 45\n2\n 73\n0\n 74\n0\n 46\n0\n  0\nMTEXT\n  5\n73\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText columns = ByHandle<DxfMText>(db, "F73");

            Assert.Equal(DxfColumnType.StaticColumns, columns.ColumnType);
            Assert.Equal(3, columns.ColumnCount);
            Assert.Equal(10.0, columns.ColumnWidth);
            Assert.Equal(2.0, columns.ColumnGutterWidth);
            Assert.False(columns.ColumnAutoHeight);
            Assert.Equal(0.0, columns.GetColumnHeight(0));
            Assert.Equal("columns", columns.Contents);
            Assert.Equal(Bits(new GeoVector3(1, 0, 0)), Bits(columns.Direction));

            // A direction set where the MTEXT has none of its own lands in its own part, the embedded object's 11 kept.
            columns.Direction = new GeoVector3(0, 1, 0);
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "F73");
            Assert.Contains(" 71\n1\n  1\ncolumns\n 11\n0\n 21\n1\n 31\n0\n101\nEmbedded Object\n", record, StringComparison.Ordinal);
            Assert.Contains("\n 11\n1\n 21\n2\n 31\n0\n", record, StringComparison.Ordinal);
            Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "F73").Direction));
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach()
                .Add(new DxfText(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), 2.5, 0.3, "writer text"))
                .Add(new DxfText(new GeoPoint3(4, 5, 6), new GeoVector3(0, 0.6, 0.8), 1.5, 0.2, "writer\nmtext", true, 1.0, 20.0))
                .ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfMText>(text, m =>
                {
                    m.Contents = m.Contents;
                    m.Location = m.Location;
                    m.TextHeight = m.TextHeight;
                    m.Width = m.Width;
                    m.Height = m.Height;
                    m.Rotation = m.Rotation;
                    m.Direction = m.Direction;
                    m.Attachment = m.Attachment;
                    m.FlowDirection = m.FlowDirection;
                    m.LineSpacingStyle = m.LineSpacingStyle;
                    m.LineSpacingFactor = m.LineSpacingFactor;
                    m.Normal = m.Normal;
                    m.BackgroundFill = m.BackgroundFill;
                    m.UseBackgroundColor = m.UseBackgroundColor;
                    m.BackgroundScaleFactor = m.BackgroundScaleFactor;
                    m.BackgroundFillColor = m.BackgroundFillColor;
                    m.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void EveryColumnAndSpacingSetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            // The MTEXT of the embedded object of R2018, and one of R2000 in three static columns of the extended data of ACAD,
            // each number written as AutoCAD writes it, 10 not 10.0.
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, WithEmbeddedColumns(), WithColumnData() })
            {
                SettingWhatItHasChangesNothing<DxfMText>(text, m =>
                {
                    m.ColumnType = m.ColumnType;
                    m.ColumnCount = m.ColumnCount;
                    m.ColumnWidth = m.ColumnWidth;
                    m.ColumnGutterWidth = m.ColumnGutterWidth;
                    m.ColumnAutoHeight = m.ColumnAutoHeight;
                    m.ColumnFlowReversed = m.ColumnFlowReversed;
                    m.LineSpaceDistance = m.LineSpaceDistance;
                    m.ShowBorders = m.ShowBorders;
                    m.BackgroundTransparency = m.BackgroundTransparency;
                    m.SetAttachmentMovingLocation(m.Attachment);
                    m.Attachment = m.Attachment;
                    m.Location = m.Location;
                    m.Width = m.Width;
                });
            }
        }

        #endregion

        #region Columns

        [Fact]
        public void ColumnsSetInAnMTextOfR2018_AreWrittenInItsEmbeddedObject_AndTakenOutAgain()
        {
            // ezdxf 1.4.5 reads them back as columns of type 1, count 3, width 12.5, gutter 2.5, total width 42.5.
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetStaticColumns(12.5, 2.5, 3);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "73");
            string record = Records(saved).Single(r => HandleOf(r) == "73");
            Assert.Contains("101\nEmbedded Object\n 70\n1\n 10\n1\n 20\n0\n 30\n0\n 11\n20\n 21\n40\n 31\n0\n 40\n42.5\n 41\n0\n 42\n42.5\n 43\n0\n 71\n1\n 72\n3\n 44\n12.5\n 45\n2.5\n 73\n0\n 74\n0\n", record, StringComparison.Ordinal);
            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(saved), "73");
            Assert.Equal(DxfColumnType.StaticColumns, again.ColumnType);
            Assert.Equal(3, again.ColumnCount);
            Assert.Equal(12.5, again.ColumnWidth);
            Assert.Equal(2.5, again.ColumnGutterWidth);
            Assert.Equal("{\\fArial|b1;Bold}\\Pline two é", again.Contents);

            // Dynamic columns of their own heights: a height each, the second set to 7.5; the count kept.
            mtext.SetDynamicColumns(10.0, 2.0, false);
            mtext.SetColumnHeight(1, 7.5);
            DxfMText dynamic = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Equal(DxfColumnType.DynamicColumns, dynamic.ColumnType);
            Assert.False(dynamic.ColumnAutoHeight);
            Assert.Equal(3, dynamic.ColumnCount);
            Assert.Equal(new[] { 0.0, 7.5, 0.0 }, new[] { dynamic.GetColumnHeight(0), dynamic.GetColumnHeight(1), dynamic.GetColumnHeight(2) });

            // Dynamic columns of auto height write no count (72 of 0), as AutoCAD does: it is read back from the width of all.
            mtext.ColumnAutoHeight = true;
            mtext.ColumnFlowReversed = true;
            DxfMText auto = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Contains(" 72\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == "73"), StringComparison.Ordinal);
            Assert.Equal(3, auto.ColumnCount);
            Assert.True(auto.ColumnFlowReversed);
            Assert.Equal(0.0, auto.GetColumnHeight(1));

            // Taken out, the columns leave the MTEXT as it was but for its width, which they made theirs.
            mtext.ColumnType = DxfColumnType.NoColumns;
            mtext.Width = 40.0;
            OnlyTheseChanged(text, db.ToText(), "73");
            Assert.DoesNotContain("101\nEmbedded Object\n", db.ToText(), StringComparison.Ordinal);
            Assert.Equal(40.0, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").Width);
        }

        [Fact]
        public void ColumnsSetInAnMTextOfR2007_AreWrittenInTheExtendedDataOfAcad_AsEzdxfWritesThem()
        {
            // From R2007 to R2013; an MTEXT of R2000 with no column MTEXTs linked is given none, as AutoCAD 2021 reads none (09d).
            string text = DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetStaticColumns(12.5, 2.5, 3);

            OnlyTheseChanged(text, db.ToText(), "73");
            Assert.Equal(
                new[] { "1001 ACAD", "1000 ACAD_MTEXT_COLUMN_INFO_BEGIN", "1070 75", "1070 1", "1070 79", "1070 0", "1070 76", "1070 3", "1070 78", "1070 0", "1070 48", "1040 12.5", "1070 49", "1040 2.5", "1000 ACAD_MTEXT_COLUMN_INFO_END" },
                ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
            Assert.DoesNotContain("101\nEmbedded Object\n", db.ToText(), StringComparison.Ordinal);

            mtext.SetDynamicColumns(10.0, 2.0, false);
            mtext.SetColumnHeight(2, 4.0);
            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Equal(DxfColumnType.DynamicColumns, again.ColumnType);
            Assert.Equal(4.0, again.GetColumnHeight(2));
            Assert.Contains("1070 50", again.GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
            Assert.DoesNotContain("1000 ACAD_MTEXT_DEFINED_HEIGHT_BEGIN", again.GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));

            // Taken out, the columns leave the MTEXT as it was but for its width, which they made theirs.
            mtext.ColumnType = DxfColumnType.NoColumns;
            mtext.Width = 40.0;
            OnlyTheseChanged(text, db.ToText(), "73");
            Assert.Null(ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").GetXDataForApplication("ACAD"));
        }

        [Fact]
        public void TheColumnsOfAnMTextMadeInCode_AreWrittenInTheFormOfTheDrawingItIsAddedTo()
        {
            // The extended data of R2007, as R2000 reads them as columns only with column MTEXTs linked, which are not made.
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021"), DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfMText { Contents = "a\\Pb\\Pc", Location = new GeoPoint3(1, 2, 0), Width = 20.0 };
                made.SetStaticColumns(20.0, 5.0, 2);

                db.ModelSpace.AppendEntity(made);

                string record = Records(db.ToText()).Single(r => HandleOf(r) == made.Handle);
                bool r2018 = text == DxfFileEzdxfDrawings.R2018;
                Assert.Equal(r2018, record.Contains("101\nEmbedded Object\n"));
                Assert.Equal(!r2018, record.Contains("ACAD_MTEXT_COLUMN_INFO_BEGIN"));
                DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), made.Handle);
                Assert.Equal(DxfColumnType.StaticColumns, again.ColumnType);
                Assert.Equal(2, again.ColumnCount);
                Assert.Equal(5.0, again.ColumnGutterWidth);
            }
        }

        [Fact]
        public void TheValuesTheEmbeddedObjectRepeats_FollowTheMTexts_AndAScaleStretchesTheColumns()
        {
            // The MTEXT held at its middle centre: the embedded object's 70 says so too, as AutoCAD reads the attachment from it.
            DxfDatabase db = DxfDatabase.Parse(WithEmbeddedColumns());
            DxfMText mtext = ByHandle<DxfMText>(db, "F73");

            mtext.Attachment = DxfAttachmentPoint.MiddleCenter;
            mtext.Location = new GeoPoint3(5, 6, 0);
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "F73");
            Assert.Contains("101\nEmbedded Object\n 70\n5\n 10\n1\n 20\n0\n 30\n0\n 11\n5\n 21\n6\n 31\n0\n", record, StringComparison.Ordinal);

            // ezdxf 1.4.5 by the scale (2, 3, 1): column width 20, gutter 4, the height of all 15, the width of all 68.
            mtext.TransformBy(GeoTransform3.Scaling(2.0, 3.0, 1.0));
            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "F73");
            Assert.Equal(20.0, again.ColumnWidth);
            Assert.Equal(4.0, again.ColumnGutterWidth);
            Assert.Equal(3, again.ColumnCount);
            Assert.Contains(" 42\n68\n",Records(db.ToText()).Single(r => HandleOf(r) == "F73"), StringComparison.Ordinal);
        }

        [Fact]
        public void ColumnsSetOnAnMTextOfNone_OrOutsideWhatAutoCADTakes_AreRefused()
        {
            DxfMText mtext = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "73");

            Assert.Throws<InvalidOperationException>(() => mtext.ColumnCount = 2);
            Assert.Throws<InvalidOperationException>(() => mtext.ColumnWidth = 2.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.SetStaticColumns(10.0, 1.0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.SetStaticColumns(-1.0, 1.0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.ColumnType = (DxfColumnType)3);

            mtext.SetStaticColumns(10.0, 1.0, 2);
            Assert.Throws<InvalidOperationException>(() => mtext.SetColumnHeight(0, 5.0));
            mtext.SetDynamicColumns(10.0, 1.0, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.SetColumnHeight(2, 5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.SetColumnHeight(0, -5.0));
        }

        #endregion

        #region Linked columns of R2000

        [Fact]
        public void TheThreeBlocksOfColumnsOfR2000_StayWholeInTheirOrder_WhenTheColumnsAreScaledAndCounted()
        {
            // The MTEXT 5A in three static columns as ezdxf 1.4.5's add_mtext_static_columns writes it before R2018: COLUMN_INFO,
            // COLUMNS naming its linked MTEXTs 5B and 5C, DEFINED_HEIGHT of 30. Scaled by 2 and given 5 columns, each block
            // stays whole and in its place, as ezdxf reads it again (recover and audit 0/0).
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            DxfMText main = ByHandle<DxfMText>(db, "5A");

            string[] Expected(int count) => new[]
            {
                "1001 ACAD", "1000 ACAD_MTEXT_COLUMN_INFO_BEGIN", "1070 75", "1070 1", "1070 79", "1070 0", "1070 76", "1070 " + count, "1070 78", "1070 0",
                "1070 48", "1040 25", "1070 49", "1040 5", "1000 ACAD_MTEXT_COLUMN_INFO_END",
                "1000 ACAD_MTEXT_COLUMNS_BEGIN", "1070 47", "1070 3", "1005 5B", "1005 5C", "1000 ACAD_MTEXT_COLUMNS_END",
                "1000 ACAD_MTEXT_DEFINED_HEIGHT_BEGIN", "1070 46", "1040 60", "1000 ACAD_MTEXT_DEFINED_HEIGHT_END",
            };

            main.TransformBy(GeoTransform3.Scaling(2.0));
            Assert.Equal(Expected(3), ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "5A").GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));

            main.ColumnCount = 5;
            Assert.Equal(Expected(5), ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "5A").GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
        }

        [Fact]
        public void ACopyOfAnMTextWithLinkedColumns_HoldsTheirWordsItself_AndLinksToNoneOfThem()
        {
            // Two MTEXTs naming the same column MTEXTs is a drawing ezdxf does not read: the copy, in its drawing or another,
            // holds the words of all three columns, one\Ntwo\Nthree, its columns kept and its link (ACAD_MTEXT_COLUMNS) gone.
            foreach (string into in new[] { null, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                foreach (bool moved in new[] { false, true })
                {
                    DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
                    DxfDatabase target = into == null ? db : DxfDatabase.Parse(into);
                    DxfMText main = ByHandle<DxfMText>(db, "5A");
                    string before = Records(db.ToText()).Single(r => HandleOf(r) == "5A");
                    var copy = (DxfMText)(moved ? main.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 50, 0))) : main.Clone());

                    target.ModelSpace.AppendEntity(copy);

                    // In R2000 the column data left, with no column MTEXTs linked, are no columns, as AutoCAD 2021 reads them (09d).
                    DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(target.ToText()), copy.Handle);
                    bool r2018 = into == DxfFileEzdxfDrawings.R2018;
                    Assert.Equal("one\\Ntwo\\Nthree", again.Contents);
                    Assert.Equal(r2018 ? DxfColumnType.StaticColumns : DxfColumnType.NoColumns, again.ColumnType);
                    Assert.Equal(r2018 ? 3 : 0, again.ColumnCount);
                    Assert.DoesNotContain("ACAD_MTEXT_COLUMNS_BEGIN", Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);
                    Assert.Equal(before, Records(db.ToText()).Single(r => HandleOf(r) == "5A"));
                }
            }
        }

        [Fact]
        public void AnInsertOfABlockWithLinkedColumns_ExplodesIntoOneMTextOfAllTheirWords()
        {
            // BOLT given the MTEXT 5A and its column MTEXTs 5B and 5C: exploded, its columns come out one MTEXT of all their
            // words, as ezdxf leaves the column MTEXTs out of the block and its main MTEXT holds them; into its owner too.
            string text = DxfFileEzdxfDrawings.R2000.Replace("  0\nENDBLK\n  5\n34\n", LinkedColumns("32") + "  0\nENDBLK\n  5\n34\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));

            IReadOnlyList<DxfEntity> parts = bolt.Explode();

            Assert.Equal(new[] { "DxfCircle", "DxfLine", "DxfAttributeDefinition", "DxfMText" }, parts.Select(p => p.GetType().Name));
            Assert.Equal("one\\Ntwo\\Nthree", ((DxfMText)parts[3]).Contents);
            Assert.DoesNotContain(((DxfMText)parts[3]).GetXDataForApplication("ACAD"), g => g.TypeCode == 1005);

            int count = db.ModelSpace.Count;
            bolt.ExplodeToOwnerSpace();
            DxfMText appended = db.ModelSpace.OfType<DxfMText>().Last();
            Assert.Equal(count + 4, db.ModelSpace.Count);
            Assert.Equal("one\\Ntwo\\Nthree", ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), appended.Handle).Contents);
        }

        [Fact]
        public void AnMTextWithLinkedColumns_MovedOrScaled_CarriesItsColumnMTexts_AsEzdxfCarriesThem()
        {
            // ezdxf 1.4.5's transform() of 5A by the scale 2: 5A at (200, 0, 0), its columns 5B at (230, 0, 0) and 5C at
            // (260, 0, 0), each 5 high, 25 wide, their ACAD_MTEXT_DEFINED_HEIGHT 60; 5A's own words one\N kept. 5A, its 41 25, is
            // as wide as its three columns 25 wide 5 apart, 85, as AutoCAD 2021 read its columns of R2000 (D18, 09a).
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            ByHandle<DxfMText>(db, "5A").TransformBy(GeoTransform3.Scaling(2.0));

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            foreach (var (handle, x) in new[] { ("5A", 200.0), ("5B", 230.0), ("5C", 260.0) })
            {
                DxfMText column = Linked(again, handle);
                Assert.Equal(Bits(new GeoPoint3(x, 0, 0)), Bits(column.Location));
                Assert.Equal(5.0, column.TextHeight);
                Assert.Equal(handle == "5A" ? 85.0 : 25.0, column.Width);
                Assert.Contains("\n 41\n25\n", Records(db.ToText()).Single(r => HandleOf(r) == handle), StringComparison.Ordinal);
                Assert.Equal(60.0, column.Height);
            }

            Assert.Equal("one\\N", ByHandle<DxfMText>(again, "5A").Contents);
            Assert.Equal("two\\N", Linked(again, "5B").Contents);
        }

        [Fact]
        public void AnMTextWithLinkedColumns_GivenAPlaceTurnNormalOrHeight_TakesItsColumnMTextsWithIt()
        {
            // 5A moved to (110, 10, 0): its columns to (125, 10, 0) and (140, 10, 0). Turned a quarter about its location, as
            // ezdxf turns them: to (100, 15, 0) and (100, 30, 0). Its letters 3 high and its box 40 high: theirs too.
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            DxfMText main = ByHandle<DxfMText>(db, "5A");
            string before = db.ToText();

            main.Location = main.Location;
            main.Rotation = main.Rotation;
            main.Normal = main.Normal;
            main.TextHeight = main.TextHeight;
            main.Height = main.Height;
            Assert.Equal(before, db.ToText());

            main.Location = new GeoPoint3(110, 10, 0);
            Near(new GeoPoint3(125, 10, 0), Linked(db, "5B").Location);
            Near(new GeoPoint3(140, 10, 0), Linked(db, "5C").Location);

            main.Location = new GeoPoint3(100, 0, 0);
            main.Rotation = Math.PI / 2.0;
            Near(new GeoPoint3(100, 15, 0), Linked(db, "5B").Location);
            Near(new GeoPoint3(100, 30, 0), Linked(db, "5C").Location);
            Near(Math.PI / 2.0, Linked(db, "5C").Rotation);

            // On the normal (0, 0, -1), whose x axis is the world's -x and y the world's y, turned a quarter it still reads
            // along (0, 1, 0): its columns stand where they stood, on the new normal.
            main.Normal = new GeoVector3(0, 0, -1);
            Near(new GeoVector3(0, 1, 0), main.Direction);
            Near(new GeoVector3(0, 0, -1), Linked(db, "5B").Normal);
            Near(new GeoVector3(0, 1, 0), Linked(db, "5B").Direction);
            Near(new GeoPoint3(100, 15, 0), Linked(db, "5B").Location);

            // Read along the world's x again: its columns turn back about its location.
            main.Direction = new GeoVector3(1, 0, 0);
            Near(new GeoPoint3(115, 0, 0), Linked(db, "5B").Location);

            main.TextHeight = 3.0;
            main.Height = 40.0;
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.All(new[] { "5B", "5C" }, h => Assert.Equal(3.0, Linked(again, h).TextHeight));
            Assert.All(new[] { "5B", "5C" }, h => Assert.Equal(40.0, Linked(again, h).Height));
        }

        [Fact]
        public void AnMTextWithLinkedColumns_GivenColumnsOfTheirOwnHeights_TakesTheDefinedHeightFromItsColumnMTexts()
        {
            // As AutoCAD 2021 wrote its columns of R2000 (point 9): columns of their own heights, 5A's [30, 7, 30], give each
            // column MTEXT its own ACAD_MTEXT_DEFINED_HEIGHT, 7 and 30; made static again of a box 25 high, they have that height.
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            DxfMText main = ByHandle<DxfMText>(db, "5A");

            main.SetDynamicColumns(12.5, 2.5, false);
            main.SetColumnHeight(1, 7.0);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { 30.0, 7.0, 30.0 }, Enumerable.Range(0, 3).Select(i => ByHandle<DxfMText>(again, "5A").GetColumnHeight(i)));
            Assert.Equal(new[] { 7.0, 30.0 }, new[] { "5B", "5C" }.Select(h => Linked(again, h).Height));

            main.SetStaticColumns(12.5, 2.5, 3);
            main.Height = 25.0;
            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.All(new[] { "5B", "5C" }, h => Assert.Equal(25.0, Linked(back, h).Height));
        }

        [Fact]
        public void AnMTextWithLinkedColumns_Erased_TakesItsColumnMTextsWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            DxfMText second = Linked(db, "5B");

            ByHandle<DxfMText>(db, "5A").Erase();

            Assert.True(second.IsErased);
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "5B" || HandleOf(r) == "5C" || HandleOf(r) == "5A");
            Assert.Contains(Records(db.ToText()), r => HandleOf(r) == "73");
        }

        [Fact]
        public void TheHeightOfAnMTextOfR2000_IsTheDefinedHeightOfItsExtendedData_AndIsSetThere()
        {
            // R2000 has no group 46: the box of the columns is 30 high in ACAD_MTEXT_DEFINED_HEIGHT, as ezdxf reads it, and is
            // set there; an MTEXT of R2000 in no columns has nowhere to hold one.
            DxfDatabase db = DxfDatabase.Parse(WithLinkedColumns());
            DxfMText main = ByHandle<DxfMText>(db, "5A");
            Assert.Equal(30.0, main.Height);

            main.Height = 40.0;

            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "5A");
            Assert.Equal(40.0, again.Height);
            Assert.Contains("1040 40", again.GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
            Assert.DoesNotContain("\n 46\n", Records(db.ToText()).Single(r => HandleOf(r) == "5A"), StringComparison.Ordinal);
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ByHandle<DxfMText>(db, "73").Height = 5.0);
            Assert.Equal("A DXF older than R2007 (AC1021) has no height of an MTEXT's box but in the extended data of its columns.", refused.Message);
        }

        #endregion

        #region Spacing, attachment, borders and background

        [Fact]
        public void TheLineSpaceDistance_IsTheFactorTimesFiveThirdsOfTheHeight()
        {
            // The MTEXT 73 of letters 3 high: 5 at the factor 1; 7.5 is the factor 1.5; 0.5 would be 0.1, below what AutoCAD takes.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            Assert.Equal(5.0, mtext.LineSpaceDistance);
            mtext.LineSpaceDistance = 7.5;
            Assert.Equal(1.5, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").LineSpacingFactor);
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.LineSpaceDistance = 0.5);
        }

        [Fact]
        public void AnMTextHeldByAnotherPoint_IsMovedSoItsWordsStayWhereTheyAre()
        {
            // The MTEXT 73 at (20, 40, 0), 40 wide, held at its top left: at its top centre it stands at (40, 40, 0). Its words
            // 6 high (43): at its bottom right, at (60, 34, 0); turned a quarter, along (0, 1, 0), at (26, 80, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetAttachmentMovingLocation(DxfAttachmentPoint.TopCenter);
            Assert.Equal(DxfAttachmentPoint.TopCenter, mtext.Attachment);
            Assert.Equal(Bits(new GeoPoint3(40.0, 40.0, 0.0)), Bits(mtext.Location));
            Assert.Throws<InvalidOperationException>(() => mtext.SetAttachmentMovingLocation(DxfAttachmentPoint.MiddleLeft));

            string high = DxfFileEzdxfDrawings.R2018.Replace(" 41\n40.0\n 71\n1\n", " 41\n40.0\n 71\n1\n 43\n6.0\n");
            DxfMText tall = ByHandle<DxfMText>(DxfDatabase.Parse(high), "73");
            tall.SetAttachmentMovingLocation(DxfAttachmentPoint.BottomRight);
            Near(new GeoPoint3(60.0, 34.0, 0.0), tall.Location);

            DxfMText turned = ByHandle<DxfMText>(DxfDatabase.Parse(high), "73");
            turned.Rotation = Math.PI / 2.0;
            turned.SetAttachmentMovingLocation(DxfAttachmentPoint.BottomRight);
            Near(new GeoPoint3(26.0, 80.0, 0.0), turned.Location);
        }

        [Fact]
        public void TheBordersAndTheTransparencyOfTheBackground_AreTheirGroups_FromTheVersionThatBroughtThem()
        {
            // ShowBorders is flag 16 of group 90, as ezdxf writes a text frame; the transparency 441, 0x02000080 for the alpha 128.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            Assert.False(mtext.ShowBorders);
            Assert.True(mtext.BackgroundTransparency.IsByLayer);

            mtext.ShowBorders = true;
            mtext.BackgroundTransparency = new DxfTransparency(128);

            string record = Records(db.ToText()).Single(r => HandleOf(r) == "73");
            Assert.Contains(" 90\n16\n", record, StringComparison.Ordinal);
            Assert.Contains("441\n33554560\n", record, StringComparison.Ordinal);
            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.True(again.ShowBorders);
            Assert.False(again.BackgroundFill);
            Assert.Equal(new DxfTransparency(128), again.BackgroundTransparency);

            mtext.BackgroundTransparency = new DxfTransparency(DxfTransparencyMethod.ByLayer);
            Assert.DoesNotContain("441\n", Records(db.ToText()).Single(r => HandleOf(r) == "73"), StringComparison.Ordinal);

            DxfMText old = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "73");
            Assert.Throws<InvalidOperationException>(() => old.ShowBorders = true);
            Assert.Throws<InvalidOperationException>(() => old.BackgroundTransparency = new DxfTransparency(DxfTransparencyMethod.ByBlock));
        }

        #endregion

        /// <summary>The R2018 drawing with the MTEXT F73 in three static columns of an embedded object, as ezdxf 1.4.5 writes one.</summary>
        private static string WithEmbeddedColumns() => DxfFileEzdxfDrawings.R2018.Replace(
            "  0\nMTEXT\n  5\n73\n",
            "  0\nMTEXT\n  5\nF73\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbMText\n 10\n1\n 20\n2\n 30\n0\n 40\n2.5\n 41\n10\n 46\n5\n 71\n1\n  1\ncolumns\n101\nEmbedded Object\n 70\n1\n 10\n1\n 20\n0\n 30\n0\n 11\n1\n 21\n2\n 31\n0\n 40\n10\n 41\n5\n 42\n34\n 43\n5\n 71\n1\n 72\n3\n 44\n10\n 45\n2\n 73\n0\n 74\n0\n  0\nMTEXT\n  5\n73\n");

        /// <summary>The R2000 drawing with the MTEXT 73 in three static columns 10 wide of the extended data of ACAD, as ezdxf 1.4.5 writes them.</summary>
        /// <summary>The R2000 drawing with the MTEXT 5A in three static columns linked to the MTEXTs 5B and 5C, in model space.</summary>
        /// <summary>An MTEXT of model space by handle, a linked column among them, which the walk of the block leaves to its MTEXT.</summary>
        private static DxfMText Linked(DxfDatabase db, string handle)
            => db.ModelSpace.AllEntities().OfType<DxfMText>().First(m => m.Handle == handle);

        private static string WithLinkedColumns() => DxfFileEzdxfDrawings.R2000.Replace("  0\nMTEXT\n  5\n73\n", LinkedColumns("17") + "  0\nMTEXT\n  5\n73\n");

        /// <summary>
        /// An MTEXT 5A at (100, 0, 0) in three static columns 12.5 wide, 2.5 apart and 30 high, with its two column MTEXTs 5B and
        /// 5C, as ezdxf 1.4.5's add_mtext_static_columns writes them in R2000, the words one, two and three; owned by a block.
        /// </summary>
        private static string LinkedColumns(string owner)
        {
            string Head(string handle, string x, string words) => "  0\nMTEXT\n  5\n" + handle + "\n330\n" + owner + "\n100\nAcDbEntity\n  8\n0\n100\nAcDbMText\n 10\n" + x
                + "\n 20\n0.0\n 30\n0.0\n 40\n2.5\n 41\n12.5\n 71\n1\n  1\n" + words + "\n1001\nACAD\n";
            const string Height = "1000\nACAD_MTEXT_DEFINED_HEIGHT_BEGIN\n1070\n46\n1040\n30.0\n1000\nACAD_MTEXT_DEFINED_HEIGHT_END\n";
            return Head("5A", "100.0", "one\\N")
                + "1000\nACAD_MTEXT_COLUMN_INFO_BEGIN\n1070\n75\n1070\n1\n1070\n79\n1070\n0\n1070\n76\n1070\n3\n1070\n78\n1070\n0\n1070\n48\n1040\n12.5\n1070\n49\n1040\n2.5\n1000\nACAD_MTEXT_COLUMN_INFO_END\n"
                + "1000\nACAD_MTEXT_COLUMNS_BEGIN\n1070\n47\n1070\n3\n1005\n5B\n1005\n5C\n1000\nACAD_MTEXT_COLUMNS_END\n" + Height
                + Head("5B", "115.0", "two\\N") + Height
                + Head("5C", "130.0", "three") + Height;
        }

        private static string WithColumnData() => DxfFileEzdxfDrawings.R2000.Replace(
            "line two é\n  0\n",
            "line two é\n1001\nACAD\n1000\nACAD_MTEXT_COLUMN_INFO_BEGIN\n1070\n75\n1070\n1\n1070\n79\n1070\n0\n1070\n76\n1070\n3\n1070\n78\n1070\n0\n1070\n48\n1040\n10\n1070\n49\n1040\n2\n1000\nACAD_MTEXT_COLUMN_INFO_END\n1000\nACAD_MTEXT_DEFINED_HEIGHT_BEGIN\n1070\n46\n1040\n5\n1000\nACAD_MTEXT_DEFINED_HEIGHT_END\n  0\n");

        [Fact]
        public void TheDirectionItReads_GivenBack_ChangesNothing_ThoughItsFileWritesOneNotOfUnitLength()
        {
            // The MTEXT 73 given the direction (1, 1, 0): read as made of unit length, (0.7071067811865475, ...), which made of
            // unit length again is (0.7071067811865476, ...). Given back what it reads, it is written as it was.
            foreach (string text in Drawings)
            {
                string tilted = text.Replace("AcDbMText\n 10\n20.0\n 20\n40.0\n 30\n0.0\n", "AcDbMText\n 10\n20.0\n 20\n40.0\n 30\n0.0\n 11\n1\n 21\n1\n 31\n0\n");
                Assert.NotEqual(text, tilted);
                DxfDatabase db = DxfDatabase.Parse(tilted);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                mtext.Direction = mtext.Direction;
                mtext.Rotation = mtext.Rotation;
                mtext.Normal = mtext.Normal;

                Assert.Equal(new UTF8Encoding(false).GetBytes(tilted), Saved(db));
                Near(Math.PI / 4.0, mtext.Rotation);
            }
        }

        #region Changed

        [Fact]
        public void AnMTextGivenNewWordsPlaceAndSizes_IsThatRecordWrittenAnew_ReadBackToTheBit()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");
                string words = "{\\H1.5x;Big}\\PSmall " + new string('x', 600);

                mtext.Contents = words;
                mtext.Location = new GeoPoint3(6.0, 7.0, 0.0);
                mtext.TextHeight = 2.25;
                mtext.Width = 55.0;
                mtext.Rotation = 0.5;
                mtext.Attachment = DxfAttachmentPoint.MiddleCenter;
                mtext.LineSpacingFactor = 1.5;
                bool background = text == DxfFileEzdxfDrawings.R2018;

                if (background)
                {
                    mtext.BackgroundFill = true;
                    mtext.BackgroundFillColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2);
                }
                else
                {
                    // R2000 (AC1015) has no background of an MTEXT, which came with R2004.
                    Assert.Throws<InvalidOperationException>(() => mtext.BackgroundFill = true);
                }

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "73");

                DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(saved), "73");
                Assert.Equal(words, again.Contents);
                Assert.Equal(Bits(new GeoPoint3(6.0, 7.0, 0.0)), Bits(again.Location));
                Assert.Equal(2.25, again.TextHeight);
                Assert.Equal(55.0, again.Width);
                Assert.Equal(Bits(0.5), Bits(again.Rotation));
                Assert.Equal(DxfAttachmentPoint.MiddleCenter, again.Attachment);
                Assert.Equal(1.5, again.LineSpacingFactor);
                Assert.Equal(background, again.BackgroundFill);
                Assert.Equal(background ? 2 : (int?)null, (int?)again.BackgroundFillColor?.ColorIndex);
                Assert.Equal(2, Records(saved).Single(r => HandleOf(r) == "73").Split('\n').Count(l => l.Trim() == "3"));
            }
        }

        [Fact]
        public void AnMTextGivenALineBreak_OrASpacingAutoCADDoesNotTake_IsRefused()
        {
            DxfMText mtext = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "73");

            Assert.Throws<ArgumentException>(() => mtext.Contents = "a\nb");
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.LineSpacingFactor = 5.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => mtext.Attachment = DxfAttachmentPoint.BaseLeft);
        }

        #endregion

        #region Moved

        [Fact]
        public void AnMTextTurnedAndScaled_IsMovedAsEzdxfMovesIt()
        {
            // ezdxf 1.4.5 by the uniform scale by 2 turned about z (M2): insert (10.571843032117073, 94.24772739650206, 1),
            // direction (1.910672978251212, 0.5910404133226791, 0), height 6, width 80, the words' \H scaled with them.
            var scale = new GeoTransform3(new double[,]
            {
                { 1.910672978251212, -0.5910404133226791, 0.0, -4.0 },
                { 0.5910404133226791, 1.910672978251212, 0.0, 6.0 },
                { 0.0, 0.0, 2.0, 1.0 },
                { 0, 0, 0, 1 },
            });
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            mtext.Contents = "{\\H2.5;big} {\\H1.5x;rel}";

            mtext.TransformBy(scale);

            Near(new GeoPoint3(10.571843032117073, 94.24772739650206, 1.0), mtext.Location);
            Near(new GeoVector3(1.910672978251212, 0.5910404133226791, 0.0).Normalize(), mtext.Direction);
            Near(6.0, mtext.TextHeight);
            Near(80.0, mtext.Width);
            Assert.Equal("{\\H5;big} {\\H1.5x;rel}", mtext.Contents);

            // By the turn M1 out of the plane: insert (6.3156121095720135, 39.28354996507255, 7.5390959867609615), its normal
            // (0.3947397981737998, -0.0713924994178759, 0.9160150668873175), height 3 and width 40 kept.
            DxfMText turned = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "73");
            turned.TransformBy(Turn);
            Near(new GeoPoint3(6.3156121095720135, 39.28354996507255, 7.5390959867609615), turned.Location);
            Near(new GeoVector3(0.3947397981737998, -0.0713924994178759, 0.9160150668873175), turned.Normal);
            Near(new GeoVector3(0.7816391739070251, 0.5501172307043584, -0.29395787843858057), turned.Direction);
            Near(3.0, turned.TextHeight);
            Near(40.0, turned.Width);
        }

        #endregion

        #region Made in code

        [Fact]
        public void AnMTextMadeInCode_IsAppendedFromR2000_AndWrittenAsTextsInR12()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfMText { Contents = "new \\Pmtext", Location = new GeoPoint3(2, 2, 0), TextHeight = 2.5, Width = 30.0 };
                db.ModelSpace.AppendEntity(made);

                DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), made.Handle);
                Assert.Equal("new \\Pmtext", again.Contents);
                Assert.Equal("new \nmtext", again.Text);
                Assert.Equal(Bits(new GeoPoint3(2, 2, 0)), Bits(again.Location));
                Assert.Equal(30.0, again.Width);
            }

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var old = new DxfMText { Contents = "x" };
            r12.ModelSpace.AppendEntity(old);
            Assert.False(old.IsResident);
            Assert.Equal("x", r12.ModelSpace.OfType<DxfDBText>().Last().TextString);
        }

        [Fact]
        public void AnMTextErased_GoesFromTheFile_AndRefusesItsSetters()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.Erase();

            Assert.True(mtext.IsErased);
            Assert.Throws<InvalidOperationException>(() => mtext.Width = 1.0);
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "73");
        }

        #endregion
    }
}
