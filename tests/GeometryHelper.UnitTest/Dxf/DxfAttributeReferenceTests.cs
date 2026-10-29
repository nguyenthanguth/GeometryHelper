using System;
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
    /// The ATTRIB as a <see cref="DxfAttributeReference"/>, reached through its INSERT: read as ezdxf 1.4.5 reads it, its text
    /// and attribute groups set in their own parts and read back, refused where it would leave its INSERT.
    /// </summary>
    public class DxfAttributeReferenceTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheAttribsOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads the ATTRIB 67 of R2000 (6A of R12): MARK, B-12, at (50, 59, 0), 2.5 high, on steel, owned by
            // its INSERT 65 (ezdxf's owner; R12 writes none).
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);
                DxfAttributeReference mark = bolt.AttributeCollection.Single();

                Assert.Equal("MARK", mark.Tag);
                Assert.Equal("B-12", mark.TextString);
                Assert.Equal(Bits(new GeoPoint3(50.0, 59.0, 0.0)), Bits(mark.Position));
                Assert.Equal(2.5, mark.Height);
                Assert.Equal("steel", mark.Layer);
                Assert.False(mark.Invisible);
                Assert.False(mark.IsConstant);
                Assert.False(mark.IsVerifiable);
                Assert.False(mark.IsPreset);
                Assert.Equal(0, mark.FieldLength);
                Assert.False(mark.LockPositionInBlock);
                Assert.False(mark.IsMTextAttribute);
                Assert.Equal(DxfAttachmentPoint.BaseLeft, mark.Justify);
                Assert.Same(bolt, mark.Owner);
                Assert.Equal("*Model_Space", mark.BlockName);
                Assert.Equal(text == DxfFileEzdxfDrawings.R12 ? null : bolt.Handle, mark.OwnerHandle);
                Assert.Empty(mark.GetGeometry());
                Assert.NotNull(mark.Handle);
            }
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                byte[] before = Saved(db);

                foreach (DxfAttributeReference a in Everything(db).OfType<DxfBlockReference>().SelectMany(b => b.AttributeCollection).ToList())
                {
                    a.Tag = a.Tag;
                    a.TextString = a.TextString;
                    a.Invisible = a.Invisible;
                    a.FieldLength = a.FieldLength;
                    a.LockPositionInBlock = a.LockPositionInBlock;
                    a.Position = a.Position;
                    a.AlignmentPoint = a.AlignmentPoint;
                    a.Height = a.Height;
                    a.Rotation = a.Rotation;
                    a.WidthFactor = a.WidthFactor;
                    a.Oblique = a.Oblique;
                    a.HorizontalMode = a.HorizontalMode;
                    a.VerticalMode = a.VerticalMode;
                    a.Justify = a.Justify;
                    a.IsMirroredInX = a.IsMirroredInX;
                    a.Normal = a.Normal;
                    a.Thickness = a.Thickness;
                    a.Layer = a.Layer;
                    a.ColorIndex = a.ColorIndex;
                    a.TransformBy(GeoTransform3.Identity);
                }

                Assert.Equal(before, Saved(db));
                Assert.Equal(new UTF8Encoding(false).GetBytes(text), before);
            }
        }

        [Fact]
        public void AnAttribGivenNewTextAndModes_IsItsRecordWrittenAnew_EachGroupInItsOwnPart()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfAttributeReference mark = Bolt(db).AttributeCollection.Single();
                string handle = mark.Handle;

                mark.TextString = "M30";
                mark.Tag = "SIZE";
                mark.Invisible = true;
                mark.FieldLength = 12;
                mark.Height = 1.75;
                mark.Justify = DxfAttachmentPoint.MiddleCenter;
                mark.Thickness = 0.25;

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, handle);

                DxfAttributeReference again = Bolt(DxfDatabase.Parse(saved)).AttributeCollection.Single();
                Assert.Equal("M30", again.TextString);
                Assert.Equal("SIZE", again.Tag);
                Assert.True(again.Invisible);
                Assert.Equal(12, again.FieldLength);
                Assert.Equal(1.75, again.Height);
                Assert.Equal(DxfTextHorizontalMode.TextCenter, again.HorizontalMode);
                Assert.Equal(DxfTextVerticalMode.TextVerticalMid, again.VerticalMode);
                Assert.Equal(0.25, again.Thickness);

                // The vertical mode is the attribute's 74; its 73 is the field length. In R2018 the horizontal mode lands in
                // the text's part, ezdxf's repeated 72 of the attribute's part kept as it was.
                string record = Records(saved).Single(r => HandleOf(r) == handle);
                Assert.Contains(" 73\n12\n", record, StringComparison.Ordinal);
                Assert.Contains(" 74\n2\n", record, StringComparison.Ordinal);

                if (text == DxfFileEzdxfDrawings.R2018)
                {
                    int marker = record.IndexOf("AcDbAttribute", StringComparison.Ordinal);
                    Assert.True(record.IndexOf(" 72\n1\n", StringComparison.Ordinal) < marker, record);
                    Assert.True(record.IndexOf(" 72\n0\n", marker, StringComparison.Ordinal) > marker, record);
                }
            }
        }

        [Fact]
        public void TheLockOfAnAttribsPosition_IsWrittenAfterItsTag_FromR2007_AndRefusedBefore()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeReference mark = Bolt(db).AttributeCollection.Single();

            mark.LockPositionInBlock = true;

            string record = Records(db.ToText()).Single(r => HandleOf(r) == mark.Handle);
            Assert.True(record.IndexOf("280\n1\n", StringComparison.Ordinal) > record.IndexOf("  2\nMARK\n", StringComparison.Ordinal), record);
            Assert.True(Bolt(DxfDatabase.Parse(db.ToText())).AttributeCollection.Single().LockPositionInBlock);

            DxfAttributeReference old = Bolt(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000)).AttributeCollection.Single();
            Assert.Throws<InvalidOperationException>(() => old.LockPositionInBlock = true);
        }

        [Fact]
        public void AnAttribAppendedFromItsDefinition_FollowsTheOthers_WhichKeepTheirHandles()
        {
            // The ATTDEF MARK of BOLT, at (0, -1, 0) and 0.5 high, filled into an attribute of the INSERT 65, which BOLT's
            // placing takes to its BlockTransform of (0, -1, 0), 0.5 times the INSERT's y scale of 1.5 high.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);
                DxfAttributeReference first = bolt.AttributeCollection[0];
                string firstHandle = first.Handle;
                DxfAttributeDefinition definition = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
                var added = new DxfAttributeReference();

                added.SetAttributeFromBlock(definition, bolt.BlockTransform);
                added.TextString = "B-13";
                added.Layer = "notes";
                bolt.AttributeCollection.AppendAttribute(added);

                Assert.Equal(2, bolt.AttributeCollection.Count);
                Assert.Same(added, bolt.AttributeCollection[1]);
                Assert.Same(bolt, added.Owner);
                Assert.Equal(firstHandle, first.Handle);
                Assert.Equal("notes", added.Layer);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, added.Handle, db.LayerTable["notes"].Handle, LayerTableHandle(text));

                DxfBlockReference again = Bolt(DxfDatabase.Parse(saved));
                DxfAttributeReference read = again.AttributeCollection[1];
                Assert.Equal("MARK", read.Tag);
                Assert.Equal("B-13", read.TextString);
                Near(bolt.BlockTransform.Transform(new GeoPoint3(0.0, -1.0, 0.0)), read.Position);
                Near(0.5 * 1.5, read.Height);
                Assert.Equal(firstHandle, again.AttributeCollection[0].Handle);
            }
        }

        [Fact]
        public void AnInsertWithNoAttribs_IsGivenItsFlagAndItsEnd_WithItsFirst()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfBlockReference(new GeoPoint3(10, 20, 0), db.BlockTable["BOLT"]);
                db.ModelSpace.AppendEntity(made);
                var mark = new DxfAttributeReference(new GeoPoint3(10, 19, 0), "B-99", "MARK", null);

                made.AttributeCollection.AppendAttribute(mark);

                string saved = db.ToText();
                var records = Records(saved);
                int at = records.FindIndex(r => HandleOf(r) == made.Handle);
                Assert.Contains(" 66\n1\n", records[at], StringComparison.Ordinal);
                Assert.StartsWith("  0\nATTRIB\n", records[at + 1], StringComparison.Ordinal);
                Assert.StartsWith("  0\nSEQEND\n", records[at + 2], StringComparison.Ordinal);

                DxfAttributeReference again = Assert.Single(ByHandle<DxfBlockReference>(DxfDatabase.Parse(saved), made.Handle).AttributeCollection);
                Assert.Equal("B-99", again.TextString);
                Assert.Equal(Bits(new GeoPoint3(10, 19, 0)), Bits(again.Position));
            }
        }

        [Fact]
        public void AnAttribErased_GoesAlone_AndOneIsNotAddedToABlockOrWithASpacedTag()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfBlockReference bolt = Bolt(db);
                DxfAttributeReference mark = bolt.AttributeCollection.Single();
                string insert = bolt.Handle;
                var second = new DxfAttributeReference(new GeoPoint3(1, 2, 0), "S355", "GRADE", null);
                bolt.AttributeCollection.AppendAttribute(second);
                string secondHandle = second.Handle;

                mark.Erase();

                Assert.True(mark.IsErased);
                Assert.False(bolt.IsErased);
                Assert.Same(second, Assert.Single(bolt.AttributeCollection));
                Assert.Equal(secondHandle, second.Handle);
                Assert.DoesNotContain(Records(db.ToText()), r => r.Contains("\nB-12\n"));
                Assert.Equal(secondHandle, Assert.Single(ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), insert ?? bolt.Handle).AttributeCollection).Handle);

                Assert.Throws<ArgumentException>(() => second.Tag = "TWO WORDS");
                var copy = (DxfAttributeReference)second.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0)));
                Assert.Equal(Bits(new GeoPoint3(2.0, 2.0, 0.0)), Bits(copy.Position));
                Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(copy));
                Assert.Throws<ArgumentException>(() => bolt.AttributeCollection.AppendAttribute(new DxfAttributeReference()));
                Assert.Throws<InvalidOperationException>(() => bolt.AttributeCollection.AppendAttribute(second));
            }
        }

        [Fact]
        public void AnAttribOfSeveralLines_IsCarriedWithItsInsert_ItsEmbeddedMTextWithIt()
        {
            // The ATTRIB MARK of R2018 made an MTEXT of two lines (its embedded object, 101, at (50, 59, 0)). Its INSERT mirrored
            // in x and moved by (10, 20, 0), the embedded MTEXT goes with it, as ezdxf takes it: to (-40, 79, 0), its extrusion
            // (0, 0, -1), reading along (-1, 0, 0). So do the setters that move the INSERT.
            var mirror = new GeoTransform3(new double[,] { { -1, 0, 0, 10 }, { 0, 1, 0, 20 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });

            DxfDatabase db = DxfDatabase.Parse(Embedded);
            DxfBlockReference bolt = Bolt(db);
            DxfAttributeReference mark = bolt.AttributeCollection.Single();
            Assert.True(mark.IsMTextAttribute);

            bolt.TransformBy(mirror);

            string record = EmbeddedPart(db, mark.Handle);
            Assert.Contains(" 10\n-40\n 20\n79\n 30\n0\n", record, StringComparison.Ordinal);
            Assert.Contains("210\n0\n220\n0\n230\n-1\n", record, StringComparison.Ordinal);
            Assert.Contains(" 11\n-1\n 21\n0\n 31\n0\n", record, StringComparison.Ordinal);
            Assert.Contains("  1\nB-12\\Pmore\n", record, StringComparison.Ordinal);

            DxfDatabase moved = DxfDatabase.Parse(Embedded);
            DxfBlockReference other = Bolt(moved);
            other.Position = new GeoPoint3(60.0, 65.0, 0.0);
            string[] lines = EmbeddedPart(moved, other.AttributeCollection.Single().Handle).Split('\n');
            Near(60.0, double.Parse(lines[Array.IndexOf(lines, " 10") + 1], System.Globalization.CultureInfo.InvariantCulture));
            Near(64.0, double.Parse(lines[Array.IndexOf(lines, " 20") + 1], System.Globalization.CultureInfo.InvariantCulture));

            // ezdxf reads the moved file with the MTEXT where it put it.
            Assert.Equal(Bits(new GeoPoint3(-40.0, 79.0, 0.0)), Bits(Bolt(DxfDatabase.Parse(db.ToText())).AttributeCollection.Single().Position));
        }

        [Fact]
        public void AnAttribOfSeveralLines_CopiedIntoADrawingBeforeR2018_LeavesItsEmbeddedMTextOut()
        {
            // R2000 has no MTEXT in an ATTRIB: the copy keeps the first line in its text's part, B-12, and nothing from 101 on.
            DxfDatabase from = DxfDatabase.Parse(Embedded);
            DxfDatabase into = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var copy = (DxfBlockReference)Bolt(from).GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 0, 0)));

            into.ModelSpace.AppendEntity(copy);

            DxfAttributeReference mark = copy.AttributeCollection.Single();
            Assert.False(mark.IsMTextAttribute);
            Assert.Equal("B-12", mark.TextString);
            Assert.DoesNotContain("Embedded Object", Records(into.ToText()).Single(r => HandleOf(r) == mark.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void TheMTextOfAnAttribAndWhetherItHasOne_GivenBack_ChangeNothing()
        {
            foreach (string text in Drawings.Concat(new[] { Embedded }))
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (DxfAttributeReference a in Everything(db).OfType<DxfBlockReference>().SelectMany(b => b.AttributeCollection).ToList())
                {
                    a.IsMTextAttribute = a.IsMTextAttribute;
                    a.MTextAttribute = a.MTextAttribute;
                }

                Assert.Equal(new UTF8Encoding(false).GetBytes(text), Saved(db));
            }
        }

        [Fact]
        public void AnAttribGivenAnMText_EmbedsIt_AndItsTextAgrees_AsEzdxfSetsOne()
        {
            // ezdxf 1.4.5 reads it back as an ATTRIB of type 2 whose text is B-12 at (60, 70, 0), centred on its middle (72 of 1,
            // 74 of 2), 3 high, and whose embedded MTEXT reads 'B-12\PM24' at (60, 70, 0), held at its middle centre (5).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeReference mark = Bolt(db).AttributeCollection.Single();
            string before = db.ToText();

            mark.MTextAttribute = new DxfMText { Contents = "B-12\\PM24", Location = new GeoPoint3(60, 70, 0), TextHeight = 3.0, Attachment = DxfAttachmentPoint.MiddleCenter };

            OnlyTheseChanged(before, db.ToText(), mark.Handle);
            DxfAttributeReference again = Bolt(DxfDatabase.Parse(db.ToText())).AttributeCollection.Single();
            Assert.True(again.IsMTextAttribute);

            // Its text the words of both lines joined by a space, as AutoCAD 2021 writes it (check 6), where ezdxf writes B-12.
            Assert.Equal("B-12 M24", again.TextString);
            Assert.Equal(Bits(new GeoPoint3(60, 70, 0)), Bits(again.Position));
            Assert.Equal(Bits(new GeoPoint3(60, 70, 0)), Bits(again.AlignmentPoint));
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, again.Justify);
            Assert.Equal(3.0, again.Height);
            Assert.Equal("steel", again.MTextAttribute.Layer);
            Assert.Equal("B-12\\PM24", again.MTextAttribute.Contents);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, again.MTextAttribute.Attachment);
            Assert.Equal(Bits(new GeoPoint3(60, 70, 0)), Bits(again.MTextAttribute.Location));
            string record = Records(db.ToText()).Single(r => HandleOf(r) == mark.Handle);
            Assert.Contains("AcDbAttribute\n  2\nMARK\n 70\n0\n 74\n2\n 71\n2\n 72\n0\n101\nEmbedded Object\n", record, StringComparison.Ordinal);

            // Made one of one line again: the MTEXT goes, the text keeps B-12 M24, the type is 1.
            mark.IsMTextAttribute = false;
            Assert.False(mark.IsMTextAttribute);
            Assert.Equal("B-12 M24", mark.TextString);
            Assert.Contains("AcDbAttribute\n  2\nMARK\n 70\n0\n 74\n2\n 71\n1\n 72\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == mark.Handle), StringComparison.Ordinal);
            Assert.Null(mark.MTextAttribute);

            DxfAttributeReference old = Bolt(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000)).AttributeCollection.Single();
            Assert.Throws<InvalidOperationException>(() => old.IsMTextAttribute = true);
            Assert.Throws<InvalidOperationException>(() => old.MTextAttribute = new DxfMText());

            // Null, what an attribute of one line reads, changes nothing on one; an attribute of several lines refuses it.
            string saved = db.ToText();
            mark.MTextAttribute = null;
            Assert.Equal(saved, db.ToText());
            mark.IsMTextAttribute = true;
            Assert.Throws<ArgumentNullException>(() => mark.MTextAttribute = null);
        }

        [Fact]
        public void AnAttribSetFromADefinitionOfSeveralLines_TakesItsMText_AndTheTypeOfAnAttribOfSeveralLines()
        {
            // BOLT's ATTDEF MARK made one of several lines, of type 4: the ATTRIB filled from it is one of several lines of
            // type 2, as ezdxf writes an ATTRIB of several lines, its 72 of nought after it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeDefinition definition = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            definition.MTextAttributeDefinition = new DxfMText { Contents = "M20\\Pgrade 8.8", Location = new GeoPoint3(0, -1, 0), TextHeight = 0.5 };
            DxfBlockReference bolt = Bolt(db);
            DxfAttributeReference mark = bolt.AttributeCollection.Single();

            mark.SetAttributeFromBlock(definition, bolt.BlockTransform);

            Assert.True(mark.IsMTextAttribute);
            Assert.Equal("M20\\Pgrade 8.8", mark.MTextAttribute.Contents);
            string record = Records(db.ToText()).Single(r => HandleOf(r) == mark.Handle);
            Assert.Contains("AcDbAttribute\n  2\nMARK\n 70\n0\n 74\n3\n 71\n2\n 72\n0\n101\nEmbedded Object\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void AnAttribOfOneLineMadeOfSeveral_HoldsItsWordsWhereTheyStand_ItsCodesWrittenAsCodes()
        {
            // The value a\b{c} at (50, 59, 0), left on its baseline: the MTEXT a\\b\{c\} held at its bottom left there, 2.5 high.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeReference mark = Bolt(db).AttributeCollection.Single();
            mark.TextString = "a\\b{c}";

            mark.IsMTextAttribute = true;

            DxfMText made = mark.MTextAttribute;
            Assert.Equal("a\\\\b\\{c\\}", made.Contents);
            Assert.Equal("a\\b{c}", made.Text);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, made.Attachment);
            Assert.Equal(Bits(new GeoPoint3(50, 59, 0)), Bits(made.Location));
            Assert.Equal(2.5, made.TextHeight);
            Assert.Equal("a\\b{c}", mark.TextString);
        }

        [Fact]
        public void AnAttribOfSeveralLinesMovedByItsText_HasItsMTextPlacedAgainByUpdateMTextAttribute()
        {
            // The ATTRIB of two lines with its text moved to (70, 80, 0) and turned a quarter: its MTEXT still stands at
            // (50, 59, 0) until UpdateMTextAttribute places it there, reading along (0, 1, 0), held at its bottom left.
            DxfDatabase db = DxfDatabase.Parse(Embedded);
            DxfAttributeReference mark = Bolt(db).AttributeCollection.Single();

            mark.Position = new GeoPoint3(70, 80, 0);
            mark.Rotation = Math.PI / 2.0;
            Assert.Equal(Bits(new GeoPoint3(50, 59, 0)), Bits(mark.MTextAttribute.Location));

            mark.UpdateMTextAttribute();

            DxfMText placed = Bolt(DxfDatabase.Parse(db.ToText())).AttributeCollection.Single().MTextAttribute;
            Assert.Equal(Bits(new GeoPoint3(70, 80, 0)), Bits(placed.Location));
            Near(new GeoVector3(0, 1, 0), placed.Direction);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, placed.Attachment);
            Assert.Equal("B-12\\Pmore", placed.Contents);
            Assert.Equal(5.0, placed.Width);

            // Once placed, it agrees, and placing it again writes nothing.
            string saved = db.ToText();
            mark.UpdateMTextAttribute();
            Assert.Equal(saved, db.ToText());
        }

        /// <summary>The drawing of R2018 with its ATTRIB MARK made an MTEXT of two lines, as ezdxf writes one.</summary>
        private static string Embedded => DxfFileEzdxfDrawings.R2018.Replace(
            "AcDbAttribute\n  2\nMARK\n 70\n0\n 71\n1\n 72\n0\n  0\nSEQEND\n",
            "AcDbAttribute\n  2\nMARK\n 70\n0\n 71\n2\n 72\n0\n101\nEmbedded Object\n 10\n50.0\n 20\n59.0\n 30\n0.0\n 40\n2.5\n 41\n5.0\n 46\n0.0\n 71\n1\n 72\n5\n  1\nB-12\\Pmore\n  0\nSEQEND\n");

        /// <summary>The embedded object of an ATTRIB's record: from its 101 on.</summary>
        private static string EmbeddedPart(DxfDatabase db, string handle)
        {
            string record = Records(db.ToText()).Single(r => HandleOf(r) == handle);
            int at = record.IndexOf("101\nEmbedded Object\n", StringComparison.Ordinal);
            Assert.True(at > 0, record);
            return record.Substring(at);
        }

        /// <summary>The handle of the TABLE record of the layer table of a text, whose count a new layer raises.</summary>
        private static string LayerTableHandle(string text)
            => HandleOf(Records(text).First(r => r.StartsWith("  0\nTABLE\n  2\nLAYER\n", StringComparison.Ordinal)));

        private static DxfBlockReference Bolt(DxfDatabase db)
            => Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));
    }
}
