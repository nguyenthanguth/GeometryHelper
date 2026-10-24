using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfDBTextTests;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
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

        #endregion

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
        public void AnMTextMadeInCode_IsAppendedFromR2000_AndRefusedByR12()
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
            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(new DxfMText { Contents = "x" }));
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
