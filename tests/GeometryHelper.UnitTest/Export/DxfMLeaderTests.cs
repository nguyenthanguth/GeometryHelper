using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The MULTILEADER as a <see cref="DxfMLeader"/>: read as ezdxf 1.4.5 reads it, its content, leaders and lines set and read
    /// back with the bits of the overrides AutoCAD sets, leaders and lines added and taken out, made in code and appended,
    /// erased, and moved as ezdxf moves one; every other record of the file keeping its bytes.
    /// </summary>
    public class DxfMLeaderTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static string[] Drawings => new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheMultileadersOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

                Assert.Equal(DxfContentType.MTextContent, words.ContentType);
                Assert.True(words.HasContent());
                DxfMText mtext = words.MText;
                Assert.Equal("Line one\\Ptwo", mtext.Contents);
                Assert.Equal(Bits(new GeoPoint3(800, 0, 0)), Bits(mtext.Location));
                Assert.Equal(2.5, mtext.TextHeight);
                Assert.Equal(DxfAttachmentPoint.TopLeft, mtext.Attachment);
                Assert.Null(mtext.Handle);
                Assert.Equal(Bits(new GeoPoint3(800, 0, 0)), Bits(words.TextLocation));
                Assert.Equal(2.5, words.TextHeight);
                Assert.Equal(4.0, words.ArrowSize);
                Assert.Null(words.ArrowSymbol);
                Assert.Equal(8.0, words.DoglegLength);
                Assert.Equal(2.0, words.LandingGap);
                Assert.True(words.EnableLanding);
                Assert.True(words.EnableDogleg);
                Assert.False(words.EnableFrameText);
                Assert.Equal(DxfLeaderType.StraightLeader, words.LeaderLineType);
                Assert.True(words.LeaderLineColor.IsByBlock);
                Assert.Equal(DxfLineWeight.ByBlock, words.LeaderLineWeight);
                Assert.True(words.TextColor.IsByBlock);
                Assert.Equal(DxfTextAttachmentType.AttachmentMiddleOfTop, words.TextAttachmentType);
                Assert.Equal(DxfTextAttachmentType.AttachmentMiddleOfTop, words.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader));
                Assert.Equal(DxfTextAttachmentType.AttachmentCenter, words.GetTextAttachmentType(DxfLeaderDirectionType.TopLeader));
                Assert.Equal(DxfTextAlignmentType.LeftAlignment, words.TextAlignmentType);
                Assert.Equal(DxfTextAngleType.HorizontalAngle, words.TextAngleType);
                Assert.Equal(DxfTextAttachmentDirection.AttachmentHorizontal, words.TextAttachmentDirection);
                Assert.Equal(1.0, words.Scale);
                Assert.Equal("Standard", words.MLeaderStyle);
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(words.Normal));
                Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(words.GetPlane().Origin));
                Assert.Null(words.BlockContent);

                // Two leaders, the first of two lines; ezdxf numbers the lines of each leader from nought, so the second
                // leader's line is 0 again, and the index names the first leader's.
                Assert.Equal(2, words.LeaderCount);
                Assert.Equal(3, words.LeaderLineCount);
                Assert.Equal(new[] { 0, 1 }, words.GetLeaderIndexes());
                Assert.Equal(new[] { 0, 1 }, words.GetLeaderLineIndexes(0));
                Assert.Equal(new[] { 0 }, words.GetLeaderLineIndexes(1));
                Assert.Equal(0, words.GetLeaderIndex(1));
                Assert.Equal(3, words.VerticesCount(0));
                Assert.Equal(Bits(new GeoPoint3(780, -10, 0)), Bits(words.GetFirstVertex(0)));
                Assert.Equal(Bits(new GeoPoint3(790, -5, 0)), Bits(words.GetVertex(0, 1)));
                Assert.Equal(Bits(new GeoPoint3(790, -1.25, 0)), Bits(words.GetLastVertex(0)));
                Assert.Equal(Bits(new GeoPoint3(790, -1.25, 0)), Bits(words.GetLastVertex(1)));
                Assert.Equal(Bits(new GeoVector3(-1, -0.0, -0.0)), Bits(words.GetDogleg(1)));
                Assert.Equal(8.0, words.GetDoglegLength(1));
                Assert.Throws<ArgumentOutOfRangeException>(() => words.VerticesCount(5));

                DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");
                Assert.Equal(DxfContentType.BlockContent, block.ContentType);
                Assert.Null(block.MText);
                Assert.Equal("GKTAG", block.BlockContent.Name);
                Assert.Equal(Bits(new GeoPoint3(900, 0, 0)), Bits(block.BlockPosition));
                Assert.Equal(Bits(new GeoVector3(2, 2, 2)), Bits(block.BlockScale));
                Assert.Equal(Bits(0.2617993877991494), Bits(block.BlockRotation));
                Assert.Equal(1, block.BlockColor.ColorIndex);
                Assert.Equal(DxfBlockConnectionType.ConnectExtents, block.BlockConnectionType);
                var definition = (DxfAttributeDefinition)db.BlockTable["GKTAG"].Single(e => e is DxfAttributeDefinition);
                DxfAttributeReference attribute = block.GetBlockAttribute(definition);
                Assert.Equal("NUM", attribute.Tag);
                // ezdxf writes the values of a block's attributes from R2007 on: the drawing of R2000 gives the definition's own.
                Assert.Equal(text == DxfHatchLeaderDrawings.R2018 ? "42" : string.Empty, attribute.TextString);
                Assert.Equal(Bits(new GeoPoint3(890.3407417371093, -2.5881904510252074, 0)), Bits(block.TextLocation));
                Assert.Throws<InvalidOperationException>(() => block.TextLocation = new GeoPoint3(1, 2, 3));
            }
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings.Concat(new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 }))
            {
                SettingWhatItHasChangesNothing<DxfMLeader>(text, m =>
                {
                    m.ContentType = m.ContentType;
                    m.TextHeight = m.TextHeight;
                    m.TextColor = m.TextColor;
                    m.TextAttachmentType = m.TextAttachmentType;
                    m.TextAlignmentType = m.TextAlignmentType;
                    m.TextAngleType = m.TextAngleType;
                    m.TextAttachmentDirection = m.TextAttachmentDirection;
                    m.EnableFrameText = m.EnableFrameText;
                    m.ExtendLeaderToText = m.ExtendLeaderToText;
                    m.ArrowSize = m.ArrowSize;
                    m.ArrowSymbol = m.ArrowSymbol;
                    m.EnableLanding = m.EnableLanding;
                    m.EnableDogleg = m.EnableDogleg;
                    m.DoglegLength = m.DoglegLength;
                    m.LandingGap = m.LandingGap;
                    m.LeaderLineColor = m.LeaderLineColor;
                    m.LeaderLineType = m.LeaderLineType;
                    m.LeaderLineWeight = m.LeaderLineWeight;
                    m.MLeaderStyle = m.MLeaderStyle;
                    m.Scale = m.Scale;
                    m.EnableAnnotationScale = m.EnableAnnotationScale;
                    m.BlockScale = m.BlockScale;
                    m.BlockRotation = m.BlockRotation;
                    m.BlockColor = m.BlockColor;
                    m.BlockConnectionType = m.BlockConnectionType;

                    foreach (DxfLeaderDirectionType side in new[] { DxfLeaderDirectionType.LeftLeader, DxfLeaderDirectionType.RightLeader, DxfLeaderDirectionType.TopLeader, DxfLeaderDirectionType.BottomLeader })
                    {
                        m.SetTextAttachmentType(m.GetTextAttachmentType(side), side);
                    }

                    if (m.ContentType == DxfContentType.MTextContent)
                    {
                        m.TextLocation = m.TextLocation;
                    }

                    if (m.ContentType == DxfContentType.BlockContent)
                    {
                        m.BlockContent = m.BlockContent;
                        m.BlockPosition = m.BlockPosition;
                    }

                    foreach (int leader in m.GetLeaderIndexes())
                    {
                        m.SetDogleg(leader, m.GetDogleg(leader));
                        m.SetDoglegLength(leader, m.GetDoglegLength(leader));
                    }

                    foreach (int line in m.GetLeaderLineIndexes(m.GetLeaderIndexes()[0]))
                    {
                        for (int i = 0; i < m.VerticesCount(line); i++)
                        {
                            m.SetVertex(line, i, m.GetVertex(line, i));
                        }
                    }

                    m.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Set and read back

        [Fact]
        public void TheWordsAndTheirLook_AreWrittenInTheMultileaderAndItsContext_WithTheBitsOfTheOverrides()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader mleader = ByHandle<DxfMLeader>(db, "8A");

                mleader.TextLocation = new GeoPoint3(1, 21, 0);
                mleader.TextHeight = 3.5;
                mleader.ArrowSize = 2.0;
                mleader.DoglegLength = 5.0;
                mleader.TextColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
                mleader.LeaderLineColor = DxfColor.FromRgb(255, 128, 0);
                mleader.TextAlignmentType = DxfTextAlignmentType.CenterAlignment;
                mleader.TextAttachmentType = DxfTextAttachmentType.AttachmentMiddle;
                mleader.EnableFrameText = true;
                mleader.LandingGap = 1.0;
                OnlyTheseChanged(text, db.ToText(), "8A");

                string record = Records(db.ToText()).Single(r => HandleOf(r) == "8A");
                Assert.Contains("\n 92\n-1023410173\n", record, StringComparison.Ordinal);
                Assert.Contains("\n 91\n-1023442944\n", record, StringComparison.Ordinal);

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "8A");
                Assert.Equal(Bits(new GeoPoint3(1, 21, 0)), Bits(back.TextLocation));
                Assert.Equal(3.5, back.TextHeight);
                Assert.Equal(3.5, back.MText.TextHeight);
                Assert.Equal(2.0, back.ArrowSize);
                Assert.Equal(5.0, back.DoglegLength);
                Assert.Equal(5.0, back.GetDoglegLength(0));
                Assert.Equal(3, back.TextColor.ColorIndex);
                Assert.Equal(0xFF8000, back.LeaderLineColor.ColorValue);
                Assert.Equal(DxfTextAlignmentType.CenterAlignment, back.TextAlignmentType);
                Assert.Equal(DxfAttachmentPoint.TopCenter, back.MText.Attachment);
                Assert.Equal(DxfTextAttachmentType.AttachmentMiddle, back.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader));
                Assert.True(back.EnableFrameText);
                Assert.Equal(1.0, back.LandingGap);
            }
        }

        [Fact]
        public void TheOverridesOfTheStyle_GainTheBitOfAPropertySet()
        {
            // The MULTILEADER of R2000 of ezdxf overrides every property (2147483647): one made in code overrides none, and
            // setting its arrow's size sets bit 9 alone.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);
            mleader.ArrowSize = 3.0;
            Assert.Contains("\n 90\n512\n", Records(db.ToText()).Single(r => HandleOf(r) == mleader.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void TheVerticesOfALeaderLine_AreItsOwnThenThePointItsLeaderStartsTheDoglegAt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader mleader = ByHandle<DxfMLeader>(db, "B4");

                mleader.SetFirstVertex(0, new GeoPoint3(770, -20, 0));
                mleader.AddFirstVertex(1, new GeoPoint3(770, 20, 0));
                mleader.SetLastVertex(0, new GeoPoint3(791, -1.25, 0));
                mleader.RemoveFirstVertex(0);
                OnlyTheseChanged(text, db.ToText(), "B4");

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4");
                Assert.Equal(2, back.VerticesCount(0));
                Assert.Equal(Bits(new GeoPoint3(790, -5, 0)), Bits(back.GetFirstVertex(0)));
                Assert.Equal(3, back.VerticesCount(1));
                Assert.Equal(Bits(new GeoPoint3(770, 20, 0)), Bits(back.GetFirstVertex(1)));

                // The last vertex is the leader's: every line of it ends there.
                Assert.Equal(Bits(new GeoPoint3(791, -1.25, 0)), Bits(back.GetLastVertex(1)));
                Assert.Throws<InvalidOperationException>(() => back.RemoveFirstVertex(0));

                back.AddLastVertex(0, new GeoPoint3(792, 0, 0));
                Assert.Equal(3, back.VerticesCount(0));
                Assert.Equal(Bits(new GeoPoint3(791, -1.25, 0)), Bits(back.GetVertex(0, 1)));
                back.RemoveLastVertex(0);
                Assert.Equal(Bits(new GeoPoint3(791, -1.25, 0)), Bits(back.GetLastVertex(0)));
            }
        }

        [Fact]
        public void LeadersAndLines_AreAddedAndTakenOut_EachIndexOneAboveTheOthers()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader mleader = ByHandle<DxfMLeader>(db, "8A");

                int leader = mleader.AddLeader();
                Assert.Equal(1, leader);
                int line = mleader.AddLeaderLine(leader);
                Assert.Equal(1, line);
                mleader.AddFirstVertex(line, new GeoPoint3(10, 30, 0));
                int other = mleader.AddLeaderLine(new GeoPoint3(-25, 15, 0));
                Assert.Equal(2, other);
                Assert.Equal(0, mleader.GetLeaderIndex(other));
                OnlyTheseChanged(text, db.ToText(), "8A");

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "8A");
                Assert.Equal(2, back.LeaderCount);
                Assert.Equal(3, back.LeaderLineCount);
                Assert.Equal(new[] { 0, 2 }, back.GetLeaderLineIndexes(0));
                Assert.Equal(Bits(new GeoPoint3(10, 30, 0)), Bits(back.GetFirstVertex(1)));
                Assert.Equal(Bits(new GeoPoint3(-2, 18, 0)), Bits(back.GetLastVertex(1)));
                Assert.Equal(Bits(new GeoPoint3(-25, 15, 0)), Bits(back.GetFirstVertex(2)));

                back.RemoveLeaderLine(2);
                back.RemoveLeader(1);
                Assert.Equal(1, back.LeaderCount);
                Assert.Equal(1, back.LeaderLineCount);
                Assert.Throws<ArgumentOutOfRangeException>(() => back.RemoveLeader(1));
            }
        }

        [Fact]
        public void TheContent_IsGivenAsWordsOrABlock_AndTakenOut()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
            DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");

            var mtext = new DxfMText { Contents = "new", Location = new GeoPoint3(805, 1, 0), TextHeight = 5.0 };
            words.MText = mtext;
            block.MText = mtext;
            words.BlockContent = db.BlockTable["GKTAG"];
            words.BlockScale = new GeoVector3(3, 3, 3);
            words.BlockRotation = 0.5;
            OnlyTheseChanged(text, db.ToText(), "B4", "BA");

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            DxfMLeader nowBlock = ByHandle<DxfMLeader>(back, "B4");
            Assert.Equal(DxfContentType.BlockContent, nowBlock.ContentType);
            Assert.Null(nowBlock.MText);
            Assert.Equal("GKTAG", nowBlock.BlockContent.Name);
            Assert.Equal(Bits(new GeoVector3(3, 3, 3)), Bits(nowBlock.BlockScale));
            Assert.Equal(0.5, nowBlock.BlockRotation);

            DxfMLeader nowWords = ByHandle<DxfMLeader>(back, "BA");
            Assert.Equal(DxfContentType.MTextContent, nowWords.ContentType);
            Assert.Equal("new", nowWords.MText.Contents);
            Assert.Equal(5.0, nowWords.TextHeight);
            Assert.Equal(Bits(new GeoPoint3(805, 1, 0)), Bits(nowWords.TextLocation));
            Assert.Null(nowWords.BlockContent);

            nowWords.ContentType = DxfContentType.NoneContent;
            Assert.False(nowWords.HasContent());
            Assert.Null(nowWords.MText);
            Assert.Throws<InvalidOperationException>(() => nowWords.ContentType = DxfContentType.BlockContent);
        }

        [Fact]
        public void TheValueOfAnAttributeOfTheBlock_IsWrittenFromR2007On()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");
            var definition = (DxfAttributeDefinition)db.BlockTable["GKTAG"].Single(e => e is DxfAttributeDefinition);

            block.SetBlockAttribute(definition, new DxfAttributeReference { TextString = "43" });
            OnlyTheseChanged(text, db.ToText(), "BA");
            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal("43", ByHandle<DxfMLeader>(back, "BA").GetBlockAttribute((DxfAttributeDefinition)back.BlockTable["GKTAG"].Single(e => e is DxfAttributeDefinition)).TextString);

            DxfDatabase old = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000);
            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfMLeader>(old, "BA").SetBlockAttribute((DxfAttributeDefinition)old.BlockTable["GKTAG"].Single(e => e is DxfAttributeDefinition), new DxfAttributeReference { TextString = "43" }));
        }

        #endregion

        #region Made in code, appended and erased

        [Fact]
        public void AMultileaderMadeInCode_TakesTheDrawingsStyles_AndIsRefusedByR12()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var mleader = new DxfMLeader { Layer = "steel" };
                mleader.MText = new DxfMText { Contents = "A-1", Location = new GeoPoint3(10, 5, 0), TextHeight = 2.5 };
                int line = mleader.AddLeaderLine(new GeoPoint3(0, 0, 0));

                if (text == DxfFileEzdxfDrawings.R12)
                {
                    Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(mleader));
                    continue;
                }

                db.ModelSpace.AppendEntity(mleader);
                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), mleader.Handle);
                Assert.Equal("Standard", back.MLeaderStyle);
                Assert.Equal("A-1", back.MText.Contents);
                Assert.Equal(DxfContentType.MTextContent, back.ContentType);
                Assert.Equal(1, back.LeaderCount);
                Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(back.GetFirstVertex(line)));
                Assert.Equal(2, back.VerticesCount(line));
            }
        }

        [Fact]
        public void AnErasedMultileader_IsGone_TheRestKept_AndRefusesToBeSet()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader mleader = ByHandle<DxfMLeader>(db, "B4");

            mleader.Erase();
            OnlyTheseChanged(text, db.ToText(), "B4");
            Assert.Throws<InvalidOperationException>(() => mleader.ArrowSize = 1.0);
        }

        #endregion

        #region Moved

        [Fact]
        public void AMultileaderMoved_HasItsContentLeadersAndSizesMovedAsEzdxfMovesThem()
        {
            // Turned 30 degrees, scaled 2 and moved by (5, -7, 3), as ezdxf's transform() moves it (cmptransform.py: every group
            // the same); scaled unlike along its axes, refused, as ezdxf refuses it.
            double c = Math.Sqrt(3.0);
            var transform = new GeoTransform3(new double[,] { { c, -1, 0, 5 }, { 1, c, 0, -7 }, { 0, 0, 2, 3 }, { 0, 0, 0, 1 } });

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                ByHandle<DxfMLeader>(db, "B4").TransformBy(transform);
                ByHandle<DxfMLeader>(db, "BA").TransformBy(transform);
                OnlyTheseChanged(text, db.ToText(), "B4", "BA");

                DxfDatabase back = DxfDatabase.Parse(db.ToText());
                DxfMLeader words = ByHandle<DxfMLeader>(back, "B4");
                Assert.True(words.TextLocation.IsEqualTo(transform.Transform(new GeoPoint3(800, 0, 0)), Tol));
                Assert.True(words.GetFirstVertex(0).IsEqualTo(transform.Transform(new GeoPoint3(780, -10, 0)), Tol));
                Assert.Equal(5.0, words.TextHeight, 12);
                Assert.Equal(8.0, words.ArrowSize, 12);
                Assert.Equal(2.0, words.Scale, 12);
                Assert.Equal(16.0, words.GetDoglegLength(0), 12);
                Assert.True(words.GetDogleg(0).IsEqualTo(new GeoVector3(c / 2, 0.5, 0), Tol));

                DxfMLeader block = ByHandle<DxfMLeader>(back, "BA");
                Assert.True(block.BlockPosition.IsEqualTo(transform.Transform(new GeoPoint3(900, 0, 0)), Tol));
                Assert.True(block.BlockScale.IsEqualTo(new GeoVector3(4, 4, 4), Tol));
                Assert.Equal(0.2617993877991494 + Math.PI / 6, block.BlockRotation, 12);
            }

            DxfMLeader refused = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            Assert.Throws<ArgumentException>(() => refused.TransformBy(new GeoTransform3(new double[,] { { 2, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } })));
        }

        [Fact]
        public void MoveMLeader_MovesAllOrAllButTheArrowsOrTheContentAndDoglegs()
        {
            var by = new GeoVector3(1, 2, 0);

            foreach (DxfMoveType type in new[] { DxfMoveType.MoveAllPoints, DxfMoveType.MoveAllExceptArrowHeaderPoints, DxfMoveType.MoveContentAndDoglegPoints })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
                DxfMLeader mleader = ByHandle<DxfMLeader>(db, "B4");
                mleader.MoveMLeader(by, type);

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4");
                Assert.Equal(Bits(new GeoPoint3(801, 2, 0)), Bits(back.TextLocation));
                Assert.Equal(Bits(new GeoPoint3(791, 0.75, 0)), Bits(back.GetLastVertex(0)));
                Assert.Equal(Bits(type == DxfMoveType.MoveAllPoints ? new GeoPoint3(781, -8, 0) : new GeoPoint3(780, -10, 0)), Bits(back.GetFirstVertex(0)));
                Assert.Equal(Bits(type == DxfMoveType.MoveContentAndDoglegPoints ? new GeoPoint3(790, -5, 0) : new GeoPoint3(791, -3, 0)), Bits(back.GetVertex(0, 1)));
            }
        }

        #endregion

        #region Review round 1

        [Fact]
        public void TheSizeOfTheArrows_IsWrittenTheSameInTheMultileaderAndItsContext_SetAndMoved()
        {
            // Set to 6, both groups 6; scaled 2 with a turn of 30 degrees, both the same 12 to the rounding, and read back so;
            // set to 6 again, both 6.
            double c = Math.Sqrt(3.0);
            var transform = new GeoTransform3(new double[,] { { c, -1, 0, 5 }, { 1, c, 0, -7 }, { 0, 0, 2, 3 }, { 0, 0, 0, 1 } });

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
                words.ArrowSize = 6.0;
                Assert.Equal(new[] { 6.0, 6.0 }, ArrowSizes(db, "B4"));

                words.TransformBy(transform);
                double[] moved = ArrowSizes(db, "B4");
                Assert.Equal(moved[0], moved[1]);
                Assert.Equal(12.0, moved[0], 12);
                Assert.Equal(12.0, ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4").ArrowSize, 12);

                // Set again once scaled, as AutoCAD's ArrowSize is: 6 in both, not 6 in one and 12 in the other.
                words.ArrowSize = 6.0;
                Assert.Equal(new[] { 6.0, 6.0 }, ArrowSizes(db, "B4"));
                Assert.Equal(6.0, ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4").ArrowSize);
            }
        }

        [Fact]
        public void TheMatrixOfTheBlock_MovesWithTheBlock_PlacedOrMoved()
        {
            // BA's block at (900, 0, 0) under a matrix of no translation: placed at (910, 5, 0), its translation (10, 5, 0);
            // moved by (1, 2, 3) besides, (11, 7, 3).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");
            Assert.Equal(new[] { 0.0, 0.0, 0.0 }, Translation(db, "BA"));

            block.BlockPosition = new GeoPoint3(910, 5, 0);
            Assert.Equal(new[] { 10.0, 5.0, 0.0 }, Translation(db, "BA"));

            block.MoveMLeader(new GeoVector3(1, 2, 3), DxfMoveType.MoveAllPoints);
            Assert.Equal(new[] { 11.0, 7.0, 3.0 }, Translation(db, "BA"));
            Assert.Equal(Bits(new GeoPoint3(911, 7, 3)), Bits(ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "BA").BlockPosition));
        }

        [Fact]
        public void ThePlaneAndExtentsOfAMultileader_AreThoseOfItsContextAndPoints()
        {
            // B4: the entity's GetPlane is the MULTILEADER's; its box holds every vertex of its two leaders' lines, from (780, -10)
            // and (780, 10), and the point of its words, (800, 0).
            DxfMLeader words = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            DxfEntity entity = words;
            Assert.True(entity.GetPlane().Normal.IsEqualTo(words.Normal, Tol));
            Assert.True(entity.GetPlane().Origin.IsEqualTo(words.GetPlane().Origin, Tol));

            GeoAabb3 box = entity.GeometricExtents;
            Assert.True(box.Contains(words.TextLocation, Tol));
            Assert.True(box.Contains(words.GetFirstVertex(0), Tol));
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(780, -10, 0), Tol));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(800, 10, 0), Tol));
        }

        [Fact]
        public void AMultileaderSheared_IsRefused_AsOneScaledUnlikeAlongItsAxes()
        {
            // S(2, 1) after R(45 degrees): the axes as long as each other, at 37 degrees from square.
            double h = Math.Sqrt(0.5);
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Throws<ArgumentException>(() => ByHandle<DxfMLeader>(db, "B4").TransformBy(new GeoTransform3(new double[,] { { 2 * h, -2 * h, 0, 0 }, { h, h, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } })));
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheScale_ScalesTheDoglegOfEachLeaderWithThatOfTheMultileader()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
            double dogleg = words.GetDoglegLength(0);
            double scale = words.Scale;

            words.Scale = 2.0 * scale;
            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4");
            Assert.Equal(2.0 * scale, back.Scale, 12);
            Assert.Equal(2.0 * dogleg, back.GetDoglegLength(0), 12);
        }

        /// <summary>The size of the arrows a multileader's record gives in its context data (140), then in its own groups (42).</summary>
        private static double[] ArrowSizes(DxfDatabase db, string handle)
        {
            string record = Records(db.ToText()).Single(r => HandleOf(r) == handle);
            int end = record.IndexOf("\n301\n}", StringComparison.Ordinal);
            return new[] { After(record.Substring(0, end), "\n140\n"), After(record.Substring(end), "\n 42\n") };
        }

        /// <summary>The translation of the matrix of the block of a multileader's context data: its groups 47 at 3, 7 and 11.</summary>
        private static double[] Translation(DxfDatabase db, string handle)
        {
            string[] lines = Records(db.ToText()).Single(r => HandleOf(r) == handle).Split('\n');
            var matrix = new System.Collections.Generic.List<double>();

            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i] == " 47")
                {
                    matrix.Add(double.Parse(lines[i + 1], System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            Assert.Equal(16, matrix.Count);
            return new[] { matrix[3], matrix[7], matrix[11] };
        }

        /// <summary>The number written after the first group of a code in a run of text.</summary>
        private static double After(string text, string code)
        {
            int from = text.IndexOf(code, StringComparison.Ordinal) + code.Length;
            return double.Parse(text.Substring(from, text.IndexOf('\n', from) - from), System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
