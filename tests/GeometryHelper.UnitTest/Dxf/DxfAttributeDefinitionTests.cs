using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ATTDEF as a <see cref="DxfAttributeDefinition"/>: read as ezdxf 1.4.5 reads it, set and read back, made in code and
    /// added to a block, erased, and moved.
    /// </summary>
    public class DxfAttributeDefinitionTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheAttdefsOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads the ATTDEF 37 of BOLT (35 of R12): tag MARK, no prompt or value, at (0, -1, 0), 0.5 high.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfAttributeDefinition mark = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

                Assert.Equal("MARK", mark.Tag);
                Assert.Equal(string.Empty, mark.Prompt);
                Assert.Equal(string.Empty, mark.TextString);
                Assert.Equal(Bits(new GeoPoint3(0.0, -1.0, 0.0)), Bits(mark.Position));
                Assert.Equal(0.5, mark.Height);
                Assert.False(mark.Invisible);
                Assert.False(mark.Constant);
                Assert.False(mark.Verifiable);
                Assert.False(mark.Preset);
                Assert.Equal(0, mark.FieldLength);
                Assert.False(mark.IsMTextAttributeDefinition);
                Assert.Equal("BOLT", mark.BlockName);
                Assert.Same(db.BlockTable["BOLT"], mark.Owner);
                Assert.Empty(mark.GetGeometry());
            }
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfAttributeDefinition>(text, a =>
                {
                    a.Tag = a.Tag;
                    a.Prompt = a.Prompt;
                    a.TextString = a.TextString;
                    a.Invisible = a.Invisible;
                    a.Constant = a.Constant;
                    a.Verifiable = a.Verifiable;
                    a.Preset = a.Preset;
                    a.FieldLength = a.FieldLength;
                    a.LockPositionInBlock = a.LockPositionInBlock;
                    a.Position = a.Position;
                    a.Height = a.Height;
                    a.VerticalMode = a.VerticalMode;
                    a.HorizontalMode = a.HorizontalMode;
                    a.IsMTextAttributeDefinition = a.IsMTextAttributeDefinition;
                    a.MTextAttributeDefinition = a.MTextAttributeDefinition;
                    a.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AnAttdefGivenAnMText_EmbedsIt_AsADefinitionOfSeveralLines_AndOneMadeOfOneLineAgainKeepsItsFirst()
        {
            // ezdxf 1.4.5 writes the type of the definition of an attribute of several lines 4. Given back, nothing changes;
            // UpdateMTextAttributeDefinition of a definition that agrees writes nothing either.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeDefinition mark = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            string before = db.ToText();

            mark.MTextAttributeDefinition = new DxfMText { Contents = "M20\\Pgrade 8.8", Location = new GeoPoint3(0, -1, 0), TextHeight = 0.5 };

            OnlyTheseChanged(before, db.ToText(), "37");
            Assert.Contains("AcDbAttributeDefinition\n  3\n\n  2\nMARK\n 70\n0\n 74\n3\n 71\n4\n 72\n0\n101\nEmbedded Object\n", Records(db.ToText()).Single(r => HandleOf(r) == "37"), StringComparison.Ordinal);
            DxfAttributeDefinition again = DxfDatabase.Parse(db.ToText()).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            Assert.True(again.IsMTextAttributeDefinition);
            Assert.Equal("M20\\Pgrade 8.8", again.TextString);
            Assert.Equal("M20\\Pgrade 8.8", again.MTextAttributeDefinition.Contents);
            Assert.Equal(DxfAttachmentPoint.TopLeft, again.Justify);

            string saved = db.ToText();
            mark.MTextAttributeDefinition = mark.MTextAttributeDefinition;
            mark.UpdateMTextAttributeDefinition();
            Assert.Equal(saved, db.ToText());

            mark.IsMTextAttributeDefinition = false;
            Assert.Equal("M20", mark.TextString);
            Assert.Null(mark.MTextAttributeDefinition);
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single().IsMTextAttributeDefinition = true);
        }

        [Fact]
        public void AnAttdefGivenNewTagPromptFlagsAndModes_IsThatRecordWrittenAnew_ReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfAttributeDefinition mark = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
                string handle = mark.Handle;

                mark.Tag = "SIZE";
                mark.Prompt = "Size of the bolt";
                mark.TextString = "M20";
                mark.Constant = true;
                mark.Verifiable = true;
                mark.Preset = true;
                mark.FieldLength = 8;
                mark.VerticalMode = DxfTextVerticalMode.TextTop;
                mark.HorizontalMode = DxfTextHorizontalMode.TextRight;
                mark.AlignmentPoint = new GeoPoint3(2.0, -1.0, 0.0);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, handle);

                DxfAttributeDefinition again = DxfDatabase.Parse(saved).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
                Assert.Equal("SIZE", again.Tag);
                Assert.Equal("Size of the bolt", again.Prompt);
                Assert.Equal("M20", again.TextString);
                Assert.True(again.Constant && again.Verifiable && again.Preset && !again.Invisible);
                Assert.Equal(8, again.FieldLength);
                Assert.Equal(DxfAttachmentPoint.TopRight, again.Justify);
                Assert.Equal(Bits(new GeoPoint3(2.0, -1.0, 0.0)), Bits(again.AlignmentPoint));
                Assert.Contains(" 74\n3\n", Records(saved).Single(r => HandleOf(r) == handle), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AnAttdefMadeInCode_IsAddedToABlockOfEachVersion_AndOneWithNoTagIsRefused()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfAttributeDefinition(new GeoPoint3(1.0, 2.0, 0.0), "S355", "GRADE", "Grade", null) { Height = 1.25 };

                db.BlockTable["BOLT"].AppendEntity(made);
                Assert.Equal("BOLT", made.BlockName);

                DxfAttributeDefinition again = DxfDatabase.Parse(db.ToText()).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single(a => a.Tag == "GRADE");
                Assert.Equal("S355", again.TextString);
                Assert.Equal("Grade", again.Prompt);
                Assert.Equal(1.25, again.Height);
                Assert.Equal(Bits(new GeoPoint3(1.0, 2.0, 0.0)), Bits(again.Position));

                Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfAttributeDefinition()));
                Assert.Throws<ArgumentException>(() => new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "v", "TWO WORDS", string.Empty, null));
            }
        }

        [Fact]
        public void AnAttdefMovedAndErased_IsMovedThenGoesFromItsBlock()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeDefinition mark = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

            mark.TransformBy(GeoTransform3.Translation(new GeoVector3(1.0, 1.0, 0.0)));
            Assert.Equal(Bits(new GeoPoint3(1.0, 0.0, 0.0)), Bits(mark.Position));

            mark.Erase();
            Assert.Empty(db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>());
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "37");
        }
    }
}
