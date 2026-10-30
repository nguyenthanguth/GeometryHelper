using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The B4 batch of the AutoCAD parity: SetFromStyle of an object, a dimension, a leader and a multileader; the multileader
    /// style a multileader is drawn with, its overrides in place (GetOverridedMLeaderStyle); one leader line's own look in the
    /// context data (LEADER_LINE 170, 92, 171, 40 and the flags 93); and a leader and a multileader put in a plane. Each change
    /// read back from the text saved, every other record keeping its bytes.
    /// </summary>
    public class DxfLeaderStyleAndPlaneTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const string Red = "-1023410175";

        #region SetFromStyle

        [Fact]
        public void AnObjectOfNoStyle_AnswersFalse_AndChangesNothing()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            db.ModelSpace.AppendEntity(line);
            string before = db.ToText();

            Assert.False(line.SetFromStyle());
            Assert.False(db.LayerTable["0"].SetFromStyle());
            Assert.False(new DxfMLeader().SetFromStyle());
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ADimensionSetFromStyle_LosesItsOverrides_AndReadsItsStyleAgain()
        {
            // 4D overrides GH_DIM: arrows 4.5 over the style's 3.5, words 2.0, the arrow block _CLOSED and the linetype CENTER.
            foreach (string text in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfRotatedDimension dim = db.ModelSpace.OfType<DxfRotatedDimension>().First(d => d.GetXDataForApplication("ACAD") != null);
                DxfDimStyleTableRecord style = db.DimStyleTable[dim.DimensionStyleName];
                Assert.NotEqual(style.Dimasz, dim.Dimasz);

                Assert.True(dim.SetFromStyle());
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, dim.Handle);
                Assert.DoesNotContain("DSTYLE", Records(saved).Single(r => HandleOf(r) == dim.Handle), StringComparison.Ordinal);

                DxfRotatedDimension back = DxfDatabase.Parse(saved).ModelSpace.OfType<DxfRotatedDimension>().Single(d => d.Handle == dim.Handle);
                Assert.Equal(style.Dimasz, back.Dimasz);
                Assert.Equal(style.Dimtxt, back.Dimtxt);
                Assert.Equal(style.Dimblk?.Name, back.Dimblk?.Name);

                // Again: nothing left to take away.
                Assert.True(dim.SetFromStyle());
                Assert.Equal(saved, db.ToText());
            }

            // 4D of R2018: arrows 4.5 of its own over GH_DIM's 3.5.
            DxfRotatedDimension four = DxfDatabase.Parse(DxfDimensionDrawings.R2018).ModelSpace.OfType<DxfRotatedDimension>().Single(d => d.Handle == "4D");
            Assert.Equal(4.5, four.Dimasz);
            four.SetFromStyle();
            Assert.Equal(3.5, four.Dimasz);
        }

        [Fact]
        public void ADimensionInNoDrawing_LosesTheRecordsItHoldsAsOverrides()
        {
            // A block set on a dimension made in code is held until it is appended; set from its style, it is held no more.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = db.ModelSpace.OfType<DxfRotatedDimension>().First();
            var copy = (DxfRotatedDimension)dim.Clone();
            copy.Dimblk = db.BlockTable["_DOT"];
            Assert.Equal("_DOT", copy.Dimblk.Name);

            Assert.True(copy.SetFromStyle());
            Assert.Null(copy.GetXDataForApplication("ACAD")?.FirstOrDefault(g => g.Value == "DSTYLE").Value);
            Assert.NotEqual("_DOT", copy.Dimblk?.Name);
        }

        [Fact]
        public void ALeaderSetFromStyle_LosesItsOverrides_TheColourItWritesKept()
        {
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader leader = ByHandle<DxfLeader>(db, "B2");
                DxfDimStyleTableRecord style = db.DimStyleTable["Standard"];
                Assert.Equal(3.5, leader.Dimasz);

                Assert.True(leader.SetFromStyle());
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "B2");
                Assert.DoesNotContain("DSTYLE", Records(saved).Single(r => HandleOf(r) == "B2"), StringComparison.Ordinal);

                DxfLeader back = ByHandle<DxfLeader>(DxfDatabase.Parse(saved), "B2");
                Assert.Equal(2.5, back.Dimasz);
                Assert.Equal(style.Dimasz, back.Dimasz);
                Assert.Equal(style.Dimgap, back.Dimgap);
                Assert.Equal(style.Dimscale, back.Dimscale);
                Assert.Equal(3, back.NumVertices);

                // B3 has no overrides: nothing changes.
                Assert.True(ByHandle<DxfLeader>(db, "B3").SetFromStyle());
                Assert.Equal(saved, db.ToText());
            }
        }

        [Fact]
        public void AMultileaderSetFromStyle_TakesTheValuesOfItsStyle_AndKeepsTheBitsItCannotTakeAway()
        {
            // B4 overrides every property (90 of 2147483647), its words 2.5 high; its style Standard has words 4 high and names no
            // linetype. Set from the style, B4 has the style's values, and keeps the bits of the linetype (2), of the words'
            // default content (18), of the switch of their alignment (26) and of 30; in R2000 those of the attachments of the
            // top and bottom and their direction (27 to 29) besides.
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
                Assert.Equal(2.5, words.TextHeight);

                Assert.True(words.SetFromStyle());
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "B4");

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(saved), "B4");
                Assert.Equal(4.0, back.TextHeight);
                Assert.Equal(4.0, back.MText.TextHeight);
                Assert.Equal(4.0, back.ArrowSize);
                Assert.Equal(2.0, back.LandingGap);
                Assert.Equal(8.0, back.DoglegLength);
                Assert.Equal(1.0, back.Scale);
                Assert.Equal(DxfTextAngleType.HorizontalAngle, back.TextAngleType);
                Assert.Equal(DxfTextAlignmentType.LeftAlignment, back.TextAlignmentType);
                Assert.Equal(DxfTextAttachmentType.AttachmentMiddleOfTop, back.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader));
                Assert.Equal("Line one\\Ptwo", back.MText.Contents);
                int kept = (1 << 2) | (1 << 18) | (1 << 26) | (1 << 30) | (text == DxfHatchLeaderDrawings.R2000 ? (1 << 27) | (1 << 28) | (1 << 29) : 0);
                Assert.Contains("\n 90\n" + kept.ToString(CultureInfo.InvariantCulture) + "\n170\n", Records(saved).Single(r => HandleOf(r) == "B4"), StringComparison.Ordinal);

                // Again: the values are the style's already, and nothing changes.
                Assert.True(words.SetFromStyle());
                Assert.Equal(saved, db.ToText());
            }
        }

        [Fact]
        public void AMultileaderMadeInCode_GivenAHeightOfItsOwn_IsSetBackToItsStyle_ItsBitsNought()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mleader = new DxfMLeader();
            mleader.MText = new DxfMText { Contents = "A-1", Location = new GeoPoint3(10, 5, 0), TextHeight = 4.0 };
            mleader.AddLeaderLine(new GeoPoint3(0, 0, 0));
            db.ModelSpace.AppendEntity(mleader);
            mleader.TextHeight = 2.5;
            mleader.ArrowSize = 3.0;
            // Its content given as words (bit 10), their height (16) and the size of the arrows (9).
            Assert.Contains("\n 90\n67072\n", Records(db.ToText()).Single(r => HandleOf(r) == mleader.Handle), StringComparison.Ordinal);

            Assert.True(mleader.SetFromStyle());
            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), mleader.Handle);
            Assert.Equal(4.0, back.TextHeight);
            Assert.Equal(4.0, back.ArrowSize);
            Assert.Contains("\n 90\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == mleader.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void AMultileaderSetFromAStyleOfScaleTwo_HasTheStylesSizesScaledOnce()
        {
            // Standard scaled 2: words 4 high, a gap of 2, arrows 4 and doglegs 8, unscaled in the style. B4 is drawn with words
            // 8 high, a gap of 4, arrows 8 and doglegs 16; its own arrow size (42) is the style's, unscaled, its own dogleg (41)
            // scaled, as each leader's (40).
            string text = Edited(DxfHatchLeaderDrawings.R2018, "2C", r => r.Replace("\n142\n1.0\n", "\n142\n2.0\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

            Assert.True(words.SetFromStyle());
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "B4");
            string record = Records(saved).Single(r => HandleOf(r) == "B4");
            Assert.Contains("CONTEXT_DATA{\n 40\n2\n 10\n798.0\n 20\n-1.25\n 30\n0.0\n 41\n8\n140\n8\n145\n4\n", record, StringComparison.Ordinal);

            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(saved), "B4");
            Assert.Equal(2.0, back.Scale);
            Assert.Equal(8.0, back.TextHeight);
            Assert.Equal(8.0, back.MText.TextHeight);
            Assert.Equal(4.0, back.LandingGap);
            Assert.Equal(4.0, back.ArrowSize);
            Assert.Equal(16.0, back.DoglegLength);
            Assert.Equal(16.0, back.GetDoglegLength(0));
            Assert.Equal(16.0, back.GetDoglegLength(1));

            // Again: the sizes are the style's scaled already, and nothing changes.
            Assert.True(words.SetFromStyle());
            Assert.Equal(saved, db.ToText());
        }

        [Fact]
        public void AMultileaderOfNoContextData_IsNotSetFromItsStyle_AndChangesNothing()
        {
            // B4 with its context data (300 to 301) taken out: there is nothing to draw it from, and nothing is set.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "B4", r =>
            {
                int from = r.IndexOf("300\nCONTEXT_DATA{\n", StringComparison.Ordinal);
                int to = r.IndexOf("301\n}\n", StringComparison.Ordinal) + "301\n}\n".Length;
                return r.Substring(0, from) + r.Substring(to);
            });
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.False(ByHandle<DxfMLeader>(db, "B4").SetFromStyle());
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void ACopyInNoDrawing_SetFromAStyleOfAnArrowBlock_KeepsItsArrowAndItsBit_AndTakesTheRest()
        {
            // Standard's arrows are the block GKTAG, which a copy in no drawing cannot be given: it keeps its closed filled arrow
            // and the bit of it, and takes the style's words 4 high; the multileader of the drawing takes the arrow too.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "2C", r => r.Replace("\n 44\n4.0\n", "\n 44\n4.0\n341\nB5\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Assert.Equal("GKTAG", ((DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard")).ArrowSymbol.Name);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
            var copy = (DxfMLeader)words.Clone();

            Assert.True(copy.SetFromStyle());
            Assert.Equal(4.0, copy.TextHeight);
            Assert.Null(copy.ArrowSymbol);
            var drawn = new DxfMLeaderStyle();
            copy.GetOverridedMLeaderStyle(drawn);
            Assert.Null(drawn.ArrowSymbol);
            Assert.Equal(4.0, drawn.TextHeight);

            Assert.True(words.SetFromStyle());
            Assert.Equal("GKTAG", words.ArrowSymbol.Name);
        }

        [Fact]
        public void AStyleNamingARecordTheDrawingHasNot_LeavesTheMultileadersOwn_AndItsBit()
        {
            // Standard names the linetype FFFE and the text style FFFF, which the drawing has not: B4 keeps its linetype 25 and
            // its text style 29, with the bits of them (2 and 11).
            string text = Edited(DxfHatchLeaderDrawings.R2018, "2C", r => r.Replace("\n 91\n-1056964608\n", "\n 91\n-1056964608\n340\nFFFE\n").Replace("\n342\n29\n", "\n342\nFFFF\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

            Assert.True(words.SetFromStyle());
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "B4");
            Assert.Contains("\n341\n25\n", record, StringComparison.Ordinal);
            Assert.Contains("\n343\n29\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("FFF", record, StringComparison.Ordinal);
            int kept = (1 << 2) | (1 << 11) | (1 << 18) | (1 << 26) | (1 << 30);
            Assert.Contains("\n 90\n" + kept.ToString(CultureInfo.InvariantCulture) + "\n170\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void ADimensionOrALeaderOfAStyleTheDrawingHasNot_AnswersFalse_AndChangesNothing()
        {
            string text = Edited(DxfDimensionDrawings.R2018, "4D", r => r.Replace("\n  3\nGH_DIM\n", "\n  3\nNO_SUCH_STYLE\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Assert.False(ByHandle<DxfRotatedDimension>(db, "4D").SetFromStyle());
            Assert.Equal(text, db.ToText());
            Assert.False(new DxfRotatedDimension().SetFromStyle());

            text = Edited(DxfHatchLeaderDrawings.R2018, "B2", r => r.Replace("\n  3\nStandard\n", "\n  3\nNO_SUCH_STYLE\n"));
            db = DxfDatabase.Parse(text);
            Assert.False(ByHandle<DxfLeader>(db, "B2").SetFromStyle());
            Assert.Equal(text, db.ToText());
            Assert.False(new DxfLeader().SetFromStyle());
        }

        #endregion

        #region The multileader style

        [Fact]
        public void TheStylesOfADrawing_AreMLeaderStyles_ReadAsTheFileHoldsThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var style = Assert.IsType<DxfMLeaderStyle>(db.MLeaderStyleDictionary.GetAt("Standard"));

            Assert.Equal("Standard", style.Name);
            Assert.Equal("MLEADERSTYLE 2C", style.ToString());
            Assert.Equal(DxfContentType.MTextContent, style.ContentType);
            Assert.Equal(2, style.MaxLeaderSegmentsPoints);
            Assert.Equal(DxfLeaderType.StraightLeader, style.LeaderLineType);
            Assert.True(style.LeaderLineColor.IsByBlock);
            Assert.Equal(DxfLineWeight.ByBlock, style.LeaderLineWeight);
            Assert.True(style.EnableLanding);
            Assert.Equal(2.0, style.LandingGap);
            Assert.True(style.EnableDogleg);
            Assert.Equal(8.0, style.DoglegLength);
            Assert.Null(style.ArrowSymbol);
            Assert.Equal(4.0, style.ArrowSize);
            Assert.Equal(DxfTextAttachmentType.AttachmentMiddleOfTop, style.TextAttachmentType);
            Assert.Equal(DxfTextAttachmentType.AttachmentMiddleOfTop, style.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader));
            Assert.Equal(DxfTextAttachmentType.AttachmentCenter, style.GetTextAttachmentType(DxfLeaderDirectionType.TopLeader));
            Assert.Equal(DxfTextAttachmentType.AttachmentCenter, style.GetTextAttachmentType(DxfLeaderDirectionType.BottomLeader));
            Assert.Equal(DxfTextAngleType.HorizontalAngle, style.TextAngleType);
            Assert.Equal(DxfTextAlignmentType.LeftAlignment, style.TextAlignmentType);
            Assert.True(style.TextColor.IsByBlock);
            Assert.Equal(4.0, style.TextHeight);
            Assert.False(style.EnableFrameText);
            Assert.False(style.TextAlignAlwaysLeft);
            Assert.Null(style.Block);
            Assert.True(style.BlockColor.IsByBlock);
            Assert.Equal(Bits(new GeoVector3(1, 1, 1)), Bits(style.BlockScale));
            Assert.True(style.EnableBlockRotation);
            Assert.Equal(0.0, style.BlockRotation);
            Assert.Equal(DxfBlockConnectionType.ConnectExtents, style.BlockConnectionType);
            Assert.Equal(1.0, style.Scale);
            Assert.Equal(3.75, style.BreakSize);
            Assert.Equal(DxfTextAttachmentDirection.AttachmentHorizontal, style.TextAttachmentDirection);
            Assert.Throws<ArgumentException>(() => style.GetTextAttachmentType((DxfLeaderDirectionType)9));

            // A style made in code is the style Standard, of no name and in no file.
            var made = new DxfMLeaderStyle();
            Assert.Null(made.Name);
            Assert.Null(made.Handle);
            Assert.Equal(4.0, made.TextHeight);
            Assert.Equal(3.75, made.BreakSize);
        }

        [Fact]
        public void TheOverriddenStyle_IsTheStyles_WithTheMultileadersOwnValuesInPlace()
        {
            // A multileader of Standard given words 2.5 high, arrows 3 long and red leader lines of its own: the style it is
            // drawn with has those, and the style's landing gap and dogleg; the drawing's style keeps its own.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var mleader = new DxfMLeader();
            mleader.MText = new DxfMText { Contents = "A-1", Location = new GeoPoint3(10, 5, 0), TextHeight = 4.0 };
            mleader.AddLeaderLine(new GeoPoint3(0, 0, 0));
            db.ModelSpace.AppendEntity(mleader);
            mleader.TextHeight = 2.5;
            var heightOnly = new DxfMLeaderStyle();
            mleader.GetOverridedMLeaderStyle(heightOnly);
            Assert.Equal(2.5, heightOnly.TextHeight);
            Assert.Equal(4.0, heightOnly.ArrowSize);

            mleader.ArrowSize = 3.0;
            mleader.LeaderLineColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            mleader.ArrowSymbol = db.BlockTable["GKTAG"];
            string before = db.ToText();

            var drawn = new DxfMLeaderStyle();
            mleader.GetOverridedMLeaderStyle(drawn);
            Assert.Equal(before, db.ToText());
            Assert.Null(drawn.Handle);
            Assert.Equal("Standard", drawn.Name);
            Assert.Equal(2.5, drawn.TextHeight);
            Assert.Equal(3.0, drawn.ArrowSize);
            Assert.Equal(1, drawn.LeaderLineColor.ColorIndex);
            Assert.Equal("GKTAG", drawn.ArrowSymbol.Name);
            Assert.Equal(2.0, drawn.LandingGap);
            Assert.Equal(8.0, drawn.DoglegLength);
            Assert.True(drawn.TextColor.IsByBlock);

            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");
            Assert.Equal(4.0, standard.TextHeight);
            Assert.Null(standard.ArrowSymbol);

            // Given again, after the arrow is AutoCAD's closed filled one: the style has no arrow block.
            mleader.ArrowSymbol = null;
            mleader.GetOverridedMLeaderStyle(drawn);
            Assert.Null(drawn.ArrowSymbol);
            Assert.Equal(2.5, drawn.TextHeight);
        }

        [Fact]
        public void TheOverriddenStyle_IsRefusedAStyleOfADrawing_OrAMultileaderOfNoStyle()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");

            Assert.Throws<ArgumentNullException>(() => words.GetOverridedMLeaderStyle(null));
            Assert.Throws<ArgumentException>(() => words.GetOverridedMLeaderStyle(standard));
            Assert.Throws<InvalidOperationException>(() => new DxfMLeader().GetOverridedMLeaderStyle(new DxfMLeaderStyle()));

            // B4 overrides everything: the style it is drawn with has its values, words 2.5 high.
            var drawn = new DxfMLeaderStyle();
            words.GetOverridedMLeaderStyle(drawn);
            Assert.Equal(2.5, drawn.TextHeight);
            Assert.Equal(4.0, standard.TextHeight);
        }

        [Fact]
        public void TheLinetypeAndTextStyleOfAStyle_AreReadBack_AsAMultileaderOverridesThem()
        {
            // Standard names no linetype and the text style 29; B4 overrides both, with the linetype 25 and the text style 29.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var standard = (DxfMLeaderStyle)db.MLeaderStyleDictionary.GetAt("Standard");
            Assert.Null(standard.LeaderLineLinetype);
            Assert.Equal("29", standard.TextStyle.Handle);

            var drawn = new DxfMLeaderStyle();
            ByHandle<DxfMLeader>(db, "B4").GetOverridedMLeaderStyle(drawn);
            Assert.Equal("25", drawn.LeaderLineLinetype.Handle);
            Assert.Equal("29", drawn.TextStyle.Handle);
            Assert.Null(new DxfMLeaderStyle().TextStyle);
        }

        #endregion

        #region One leader line's own look

        [Fact]
        public void ALeaderLineGivenALookOfItsOwn_ReadsIt_TheOtherLinesTheMultileaders()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

            words.SetLeaderLineType(1, DxfLeaderType.SplineLeader);
            words.SetLeaderLineColor(1, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
            words.SetLeaderLineWeight(1, DxfLineWeight.LineWeight050);
            words.SetArrowSize(1, 2.5);
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "B4");

            // In the order of the DXF reference, after the line's index: 170, 92, 171, 40, then the flags 1 + 2 + 8 + 16.
            Assert.Contains("\n 91\n1\n170\n2\n 92\n" + Red + "\n171\n50\n 40\n2.5\n 93\n27\n305\n}\n", Records(saved).Single(r => HandleOf(r) == "B4"), StringComparison.Ordinal);

            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(saved), "B4");
            Assert.Equal(DxfLeaderType.SplineLeader, back.GetLeaderLineType(1));
            Assert.Equal(1, back.GetLeaderLineColor(1).ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, back.GetLeaderLineWeight(1));
            Assert.Equal(2.5, back.GetArrowSize(1));

            // Line 0 and the multileader keep theirs.
            Assert.Equal(DxfLeaderType.StraightLeader, back.GetLeaderLineType(0));
            Assert.True(back.GetLeaderLineColor(0).IsByBlock);
            Assert.Equal(DxfLineWeight.ByBlock, back.GetLeaderLineWeight(0));
            Assert.Equal(4.0, back.GetArrowSize(0));
            Assert.Equal(DxfLeaderType.StraightLeader, back.LeaderLineType);
            Assert.Equal(4.0, back.ArrowSize);

            // The line's colour is drawn: its arrow and lines red, the other line's BYBLOCK.
            Assert.Contains(back.Explode(), e => e.ColorIndex == 1);
        }

        [Fact]
        public void ALeaderLineGivenTheLookItHas_ChangesNothing_AndOldFilesRefuseALookOfItsOwn()
        {
            SettingWhatItHasChangesNothing<DxfMLeader>(DxfHatchLeaderDrawings.R2018, m =>
            {
                foreach (int leader in m.GetLeaderIndexes())
                {
                    foreach (int line in m.GetLeaderLineIndexes(leader))
                    {
                        m.SetLeaderLineType(line, m.GetLeaderLineType(line));
                        m.SetLeaderLineColor(line, m.GetLeaderLineColor(line));
                        m.SetLeaderLineWeight(line, m.GetLeaderLineWeight(line));
                        m.SetArrowSize(line, m.GetArrowSize(line));
                    }
                }

                m.SetPlane(m.GetPlane());
            });

            DxfMLeader old = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "B4");
            Assert.Throws<InvalidOperationException>(() => old.SetLeaderLineType(1, DxfLeaderType.SplineLeader));
            Assert.Throws<InvalidOperationException>(() => old.SetLeaderLineColor(1, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)));
            Assert.Throws<InvalidOperationException>(() => old.SetLeaderLineWeight(1, DxfLineWeight.LineWeight050));
            Assert.Throws<InvalidOperationException>(() => old.SetArrowSize(1, 2.5));

            DxfMLeader words = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            Assert.Throws<ArgumentOutOfRangeException>(() => words.GetLeaderLineType(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => words.SetLeaderLineType(1, (DxfLeaderType)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => words.SetLeaderLineWeight(1, (DxfLineWeight)300));
            Assert.Throws<ArgumentOutOfRangeException>(() => words.SetArrowSize(1, -1.0));
            Assert.Throws<ArgumentNullException>(() => words.SetLeaderLineColor(1, null));
        }

        [Fact]
        public void ALineOfNoFlagsWhoseColourIsNotByBlock_IsOfThatColour_AsEzdxfWritesAndDrawsIt()
        {
            // ezdxf writes a line's colour (92) and no flags: B4's second line made red so.
            string text = DxfHatchLeaderDrawings.R2018.Replace("\n 10\n780.0\n 20\n10.0\n 30\n0.0\n 91\n1\n 92\n-1056964608\n", "\n 10\n780.0\n 20\n10.0\n 30\n0.0\n 91\n1\n 92\n" + Red + "\n");
            Assert.NotEqual(DxfHatchLeaderDrawings.R2018, text);
            DxfMLeader words = ByHandle<DxfMLeader>(DxfDatabase.Parse(text), "B4");

            Assert.Equal(1, words.GetLeaderLineColor(1).ColorIndex);
            Assert.True(words.GetLeaderLineColor(0).IsByBlock);
        }

        [Fact]
        public void ALineOfEzdxfsColour_GivenATypeOfItsOwn_KeepsItsColour()
        {
            // The red of B4's second line is read as its own while it has no flags; given a type of its own, its flags say so.
            string text = DxfHatchLeaderDrawings.R2018.Replace("\n 10\n780.0\n 20\n10.0\n 30\n0.0\n 91\n1\n 92\n-1056964608\n", "\n 10\n780.0\n 20\n10.0\n 30\n0.0\n 91\n1\n 92\n" + Red + "\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

            words.SetLeaderLineType(1, DxfLeaderType.SplineLeader);
            Assert.Equal(1, words.GetLeaderLineColor(1).ColorIndex);
            Assert.Contains("\n 91\n1\n170\n2\n 92\n" + Red + "\n 93\n3\n305\n}\n", Records(db.ToText()).Single(r => HandleOf(r) == "B4"), StringComparison.Ordinal);
        }

        [Fact]
        public void ALineGivenByBlockOfItsOwn_IsDrawnInTheMultileadersOwnColour_UnderARedMultileader()
        {
            // B4's leader lines red, its second line BYBLOCK of its own: that line's polyline and arrow take the multileader's
            // own colour, BYLAYER, as AutoCAD 2021's Explode gives BYBLOCK; the other six parts of its leaders, the two doglegs
            // among them, red.
            DxfMLeader words = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            words.LeaderLineColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            words.SetLeaderLineColor(1, DxfColor.ByBlock);

            List<DxfEntity> leaders = words.Explode().Where(e => !(e is DxfMText)).ToList();
            Assert.Equal(8, leaders.Count);
            Assert.DoesNotContain(leaders, e => e.Color.IsByBlock);
            Assert.Equal(2, leaders.Count(e => e.ColorIndex == 256));
            Assert.Equal(6, leaders.Count(e => e.ColorIndex == 1));
            Assert.Contains(leaders.OfType<DxfPolyline>(), l => l.ColorIndex == 256 && l.GetPoint3dAt(l.NumberOfVertices - 1).IsEqualTo(new GeoPoint3(790, -1.25, 0), Tol) && l.GetPoint3dAt(0).Y > 0.0);
        }

        [Fact]
        public void ALinesOwnArrowSize_IsUnscaled_AndDrawnTimesTheScale()
        {
            // A multileader set from Standard scaled 2: its arrows read 4, as the style's, and are drawn 8. A line given arrows
            // 4.5 of its own reads 4.5 and is drawn 9; the other line keeps the 8 of the multileader.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "2C", r => r.Replace("\n142\n1.0\n", "\n142\n2.0\n"));
            DxfMLeader mleader = TwoLongLines(DxfDatabase.Parse(text));
            Assert.True(mleader.SetFromStyle());
            Assert.Equal(4.0, mleader.GetArrowSize(0));
            Assert.Equal(new[] { 8.0, 8.0 }, DrawnArrows(mleader));

            mleader.SetArrowSize(1, 4.5);
            Assert.Equal(4.5, mleader.GetArrowSize(1));
            Assert.Equal(new[] { 8.0, 9.0 }, DrawnArrows(mleader));

            // Scale 1, a line of arrows 2 of its own, then scaled 2: drawn 4, the other line 8.
            DxfMLeader plain = TwoLongLines(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018));
            plain.SetArrowSize(1, 2.0);
            Assert.Equal(new[] { 4.0, 2.0 }, DrawnArrows(plain));
            plain.Scale = 2.0;
            Assert.Equal(new[] { 8.0, 4.0 }, DrawnArrows(plain));
            Assert.Equal(2.0, plain.GetArrowSize(1));
        }

        /// <summary>A multileader of the words "A-1" at (10, 5, 0) made in a drawing, its two lines from (-100, -50, 0) and (-100, 60, 0), long enough for any arrow here.</summary>
        private static DxfMLeader TwoLongLines(DxfDatabase db)
        {
            var mleader = new DxfMLeader();
            mleader.MText = new DxfMText { Contents = "A-1", Location = new GeoPoint3(10, 5, 0), TextHeight = 4.0 };
            mleader.AddLeaderLine(new GeoPoint3(-100, -50, 0));
            mleader.AddLeaderLine(new GeoPoint3(-100, 60, 0));
            db.ModelSpace.AppendEntity(mleader);
            return mleader;
        }

        /// <summary>The lengths of the closed filled arrows a multileader explodes to, line by line: from the tip of each SOLID (its second corner) to the middle of its back, to 1E-9.</summary>
        private static double[] DrawnArrows(DxfMLeader mleader) => mleader.Explode().OfType<DxfSolid>()
            .Select(a => Math.Round(a.GetPointAt(1).DistanceTo(new GeoPoint3((a.GetPointAt(0).X + a.GetPointAt(2).X) / 2, (a.GetPointAt(0).Y + a.GetPointAt(2).Y) / 2, (a.GetPointAt(0).Z + a.GetPointAt(2).Z) / 2)), 9))
            .ToArray();

        [Fact]
        public void ALineOfATypeWeightAndArrowOfItsOwn_IsDrawnWithThem()
        {
            // B4's lines explode to its words and eight parts, as AutoCAD 2021's Explode gives them: the dogleg of its first
            // leader, a polyline and an arrow for each of its two lines (its second line from (780, 10, 0) to the dogleg at
            // (790, -1.25, 0)), and the dogleg, a polyline and an arrow of its second leader.
            var tip = new GeoPoint3(780, 10, 0);
            GeoVector3 along = tip.GetVectorTo(new GeoPoint3(790, -1.25, 0));
            along = along.Multiply(1.0 / along.Length);
            DxfMLeader words = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            Assert.Equal(9, words.Explode().Count);

            // A lineweight of 0.50 mm and arrows 2 long of its own: its polyline of that weight, from 2 off the tip, its arrow of
            // the multileader's own lineweight, as every arrow is.
            words.SetLeaderLineWeight(1, DxfLineWeight.LineWeight050);
            words.SetArrowSize(1, 2.0);
            IReadOnlyList<DxfEntity> parts = words.Explode();
            Assert.Equal(9, parts.Count);
            Assert.Equal(1, parts.Count(e => e.LineWeight == DxfLineWeight.LineWeight050));
            Assert.Equal(4, parts.Count(e => !(e is DxfMText) && e.LineWeight == DxfLineWeight.ByBlock));
            Assert.All(parts.OfType<DxfSolid>(), a => Assert.Equal(words.LineWeight, a.LineWeight));
            Assert.Contains(parts.OfType<DxfPolyline>(), l => l.GetPoint3dAt(0).IsEqualTo(tip.Add(along.Multiply(2.0)), Tol) && l.LineWeight == DxfLineWeight.LineWeight050);

            // Splined of its own: its arrow and one spline; invisible of its own: nothing of it.
            words.SetLeaderLineType(1, DxfLeaderType.SplineLeader);
            parts = words.Explode();
            Assert.Equal(9, parts.Count);
            Assert.Single(parts.OfType<DxfSpline>());
            words.SetLeaderLineType(1, DxfLeaderType.InVisibleLeader);
            Assert.Equal(7, words.Explode().Count);

            // The multileader's leaders invisible, the line straight of its own: the words, the dogleg of its leader, and that
            // line's polyline and arrow.
            words.LeaderLineType = DxfLeaderType.InVisibleLeader;
            words.SetLeaderLineType(1, DxfLeaderType.StraightLeader);
            parts = words.Explode();
            Assert.Equal(4, parts.Count);
            Assert.Single(parts.OfType<DxfLine>());
            Assert.Single(parts.OfType<DxfPolyline>());
        }

        #endregion

        #region SetPlane

        [Fact]
        public void ALeaderThroughThreeHeights_PutInTheFloor_RunsThroughTheirFeet()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(0, 0, 5));
            leader.AppendVertex(new GeoPoint3(10, 10, 5));
            leader.AppendVertex(new GeoPoint3(20, 10, 8));
            db.ModelSpace.AppendEntity(leader);

            leader.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));
            Assert.Equal(new[] { Bits(new GeoPoint3(0, 0, 0)), Bits(new GeoPoint3(10, 10, 0)), Bits(new GeoPoint3(20, 10, 0)) }, Enumerable.Range(0, 3).Select(i => Bits(leader.VertexAt(i))));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(leader.Normal));
        }

        [Fact]
        public void ALeaderPutInAPlane_HasItsVerticesOnIt_AndItsNormal()
        {
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader leader = ByHandle<DxfLeader>(db, "B2");

                // Raised 5: every vertex 5 up, its normal the floor's still.
                leader.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "B2");
                DxfLeader back = ByHandle<DxfLeader>(DxfDatabase.Parse(saved), "B2");
                Assert.Equal(Bits(new GeoPoint3(600, 0, 5)), Bits(back.FirstVertex));
                Assert.Equal(Bits(new GeoPoint3(618, 30, 5)), Bits(back.LastVertex));
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(back.Normal));

                // Tilted: every vertex on the plane, the normal and the hook's direction of the plane.
                var tilted = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, -0.6, 0.8));
                leader.SetPlane(tilted);
                back = ByHandle<DxfLeader>(DxfDatabase.Parse(db.ToText()), "B2");
                Assert.True(back.Normal.IsEqualTo(tilted.Normal, Tol));

                for (int i = 0; i < back.NumVertices; i++)
                {
                    Assert.True(tilted.IsPointOn(back.VertexAt(i), Tol), back.VertexAt(i).ToString());
                }

                Assert.True(back.IsPlanar);
                Assert.True(back.GetPlane().Normal.IsEqualTo(tilted.Normal, Tol));

                // Put in its own plane again: nothing changes.
                string now = db.ToText();
                leader.SetPlane(tilted);
                Assert.Equal(now, db.ToText());
            }

            Assert.Throws<ArgumentException>(() => new DxfLeader().SetPlane(default(GeoPlane3)));
        }

        [Fact]
        public void AMultileaderPutInAPlane_HasItsPointsOnIt_AndItsPlaneTheOneGiven()
        {
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

                // Raised 5: its words at (800, 0, 5), every vertex 5 up, its plane through (0, 0, 5).
                words.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "B4");
                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(saved), "B4");
                Assert.Equal(Bits(new GeoPoint3(800, 0, 5)), Bits(back.TextLocation));
                Assert.Equal(Bits(new GeoPoint3(780, -10, 5)), Bits(back.GetFirstVertex(0)));
                Assert.Equal(Bits(new GeoPoint3(790, -1.25, 5)), Bits(back.GetLastVertex(1)));
                Assert.Equal(Bits(new GeoPoint3(0, 0, 5)), Bits(back.GetPlane().Origin));
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(back.Normal));

                // In the plane x = 0: its words at (0, 0, 5), its doglegs along y or z.
                words.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)));
                back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4");
                Assert.True(back.TextLocation.IsEqualTo(new GeoPoint3(0, 0, 5), Tol), back.TextLocation.ToString());
                Assert.True(back.Normal.IsEqualTo(new GeoVector3(1, 0, 0), Tol), back.Normal + " " + back.GetPlane());
                Assert.True(back.MText.Normal.IsEqualTo(new GeoVector3(1, 0, 0), Tol), back.MText.Normal.ToString());
                Assert.Equal(0.0, back.GetDogleg(0).X, 12);
                Assert.Equal(1.0, back.GetDogleg(0).Length, 12);
            }
        }

        [Fact]
        public void AMultileaderOfABlockPutInATiltedPlane_HasItsBlockOnIt_ItsMatrixTurnedWithIt()
        {
            var tilted = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, -0.6, 0.8));

            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");
                block.SetPlane(tilted);
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "BA");

                DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(saved), "BA");
                Assert.True(tilted.IsPointOn(back.BlockPosition, Tol), back.BlockPosition.ToString());
                Assert.True(back.Normal.IsEqualTo(tilted.Normal, Tol));
                Assert.True(back.GetPlane().Origin.IsEqualTo(tilted.Origin, Tol));

                foreach (int leader in back.GetLeaderIndexes())
                {
                    Assert.True(Math.Abs(back.GetDogleg(leader).DotProduct(tilted.Normal)) <= Tol.EqualVector);

                    foreach (int line in back.GetLeaderLineIndexes(leader))
                    {
                        for (int i = 0; i < back.VerticesCount(line); i++)
                        {
                            Assert.True(tilted.IsPointOn(back.GetVertex(line, i), Tol), back.GetVertex(line, i).ToString());
                        }
                    }
                }

                // The matrix of no turn before carries the floor's z to the plane's normal now, and the block's place to its new one.
                double[] m = Matrix(saved, "BA");
                var z = new GeoVector3(m[2], m[6], m[10]);
                Assert.True(z.IsEqualTo(tilted.Normal, Tol), z.ToString());
                var moved = new GeoPoint3(m[0] * 900 + m[3], m[4] * 900 + m[7], m[8] * 900 + m[11]);
                Assert.True(moved.IsEqualTo(back.BlockPosition, Tol), moved.ToString());

                // Put in its own plane again: nothing changes.
                block.SetPlane(tilted);
                Assert.Equal(saved, db.ToText());
            }

            Assert.Throws<ArgumentException>(() => ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "BA").SetPlane(default(GeoPlane3)));
        }

        [Fact]
        public void AMirroredMultileader_PutInItsOwnPlane_ChangesNothing_AndPutInATiltedOneIsMirroredThere()
        {
            // B4 mirrored in x: its x axis (-1, 0, 0), its plane turned over (297), its words seen from below, of normal -z.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "B4", r => r
                .Replace("\n111\n1.0\n121\n0.0\n131\n0.0\n112\n0.0\n122\n1.0\n132\n0.0\n297\n0\n", "\n111\n-1.0\n121\n0.0\n131\n0.0\n112\n0.0\n122\n1.0\n132\n0.0\n297\n1\n")
                .Replace("\n 11\n0.0\n 21\n0.0\n 31\n1.0\n340\n29\n", "\n 11\n0.0\n 21\n0.0\n 31\n-1.0\n340\n29\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");
            Assert.True(words.Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol), words.Normal.ToString());

            words.SetPlane(words.GetPlane());
            Assert.Equal(text, db.ToText());

            // Tilted: its plane the one given, its words still seen from behind it, along the x axis laid in it.
            var tilted = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, -0.6, 0.8));
            words.SetPlane(tilted);
            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), "B4");
            Assert.True(back.Normal.IsEqualTo(tilted.Normal, Tol), back.Normal.ToString());
            Assert.True(back.MText.Normal.IsEqualTo(tilted.Normal.Negate(), Tol), back.MText.Normal.ToString());
            Assert.True(back.MText.Direction.IsEqualTo(new GeoVector3(1, 0, 0), Tol), back.MText.Direction.ToString());
            Assert.True(tilted.IsPointOn(back.TextLocation, Tol), back.TextLocation.ToString());
        }

        [Fact]
        public void AMultileaderOfSkewedAxes_PutInItsOwnPlane_KeepsThem()
        {
            // B4's y axis (0.6, 0.8, 0), in its plane but not square to its x axis, as a shear leaves it.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "B4", r => r.Replace("\n112\n0.0\n122\n1.0\n132\n0.0\n297\n0\n", "\n112\n0.6\n122\n0.8\n132\n0.0\n297\n0\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "B4");

            words.SetPlane(words.GetPlane());
            Assert.Equal(text, db.ToText());
            words.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));
            Assert.Contains("\n112\n0.6\n122\n0.8\n132\n0.0\n297\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == "B4"), StringComparison.Ordinal);
        }

        [Fact]
        public void ALeaderOfALongHookAndANormalAHairOffUnitLength_PutInItsOwnPlane_ChangesNothing()
        {
            // B2's hook (2, 0, 0), as a scale of 2 leaves it, and its normal (0, 0, 1.0000000000001), of unit length to 1E-12.
            string text = Edited(DxfHatchLeaderDrawings.R2018, "B2", r => r.Replace("\n340\nB1\n", "\n340\nB1\n210\n0.0\n220\n0.0\n230\n1.0000000000001\n211\n2.0\n221\n0.0\n231\n0.0\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader leader = ByHandle<DxfLeader>(db, "B2");

            leader.SetPlane(leader.GetPlane());
            Assert.Equal(text, db.ToText());
            leader.SetPlane(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)));
            Assert.Contains("\n211\n2.0\n221\n0.0\n231\n0.0\n", Records(db.ToText()).Single(r => HandleOf(r) == "B2"), StringComparison.Ordinal);
        }

        /// <summary>A drawing with the record of a handle edited, the edit made a change.</summary>
        private static string Edited(string text, string handle, Func<string, string> edit)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            string edited = edit(record);
            Assert.NotEqual(record, edited);
            Assert.Contains(record, text, StringComparison.Ordinal);
            return text.Replace(record, edited);
        }

        /// <summary>The matrix of the block of a multileader's context data: its 16 groups 47, row by row.</summary>
        private static double[] Matrix(string text, string handle)
        {
            string[] lines = Records(text).Single(r => HandleOf(r) == handle).Split('\n');
            var matrix = new List<double>();

            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i] == " 47")
                {
                    matrix.Add(double.Parse(lines[i + 1], CultureInfo.InvariantCulture));
                }
            }

            Assert.Equal(16, matrix.Count);
            return matrix.ToArray();
        }

        #endregion
    }
}
