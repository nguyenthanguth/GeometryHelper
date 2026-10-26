using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        // The groups of a leader line in the order the DXF reference writes them: its vertices, its breaks and its index, then
        // from R2010 its own type, colour, linetype, lineweight, arrow size and arrow, and the flags of those it overrides.
        private static readonly int[] LineOrder = { 10, 20, 30, 90, 11, 21, 31, 12, 22, 32, 91, 170, 92, 340, 171, 40, 341, 93 };

        // The flags of a leader line's overrides (its 93), as the ODA's specification of DWG gives them: its type, colour,
        // linetype (4), lineweight, arrow size and arrow (32).
        private const int LineTypeFlag = 1;
        private const int LineColorFlag = 2;
        private const int LinetypeFlag = 4;
        private const int LineWeightFlag = 8;
        private const int ArrowSizeFlag = 16;

        // What the MULTILEADER overrides of its style, by the bit of its 90: where its own value is (after its context data,
        // or in the head of the context data), its code there and the style's.
        private static readonly StyleOverride[] StyleOverrides =
        {
            new StyleOverride(0, false, 170, 173), new StyleOverride(1, false, 91, 91), new StyleOverride(2, false, 341, 340),
            new StyleOverride(3, false, 171, 92), new StyleOverride(4, false, 290, 290), new StyleOverride(5, true, 145, 42),
            new StyleOverride(6, false, 291, 291), new StyleOverride(7, false, 41, 43), new StyleOverride(8, false, 342, 341),
            new StyleOverride(9, false, 42, 44), new StyleOverride(10, false, 172, 170), new StyleOverride(11, false, 343, 342),
            new StyleOverride(12, false, 173, 174), new StyleOverride(13, false, 174, 175), new StyleOverride(14, false, 175, 176),
            new StyleOverride(15, false, 92, 93), new StyleOverride(16, true, 41, 45), new StyleOverride(17, false, 292, 292),
            new StyleOverride(19, false, 344, 343), new StyleOverride(20, false, 93, 94), new StyleOverride(21, false, 10, 47),
            new StyleOverride(21, false, 20, 49), new StyleOverride(21, false, 30, 140), new StyleOverride(22, false, 43, 141),
            new StyleOverride(23, false, 176, 177), new StyleOverride(24, true, 40, 142), new StyleOverride(25, false, 95, 178),
            new StyleOverride(27, false, 271, 271), new StyleOverride(28, false, 273, 273), new StyleOverride(29, false, 272, 272),
        };

        #region One leader line's own look

        /// <summary>
        /// Gets how a leader line is drawn, as AutoCAD's <c>GetLeaderLineType</c>: its own type (group 170 of the line) where it
        /// overrides the multileader's (bit 1 of its 93), else <see cref="LeaderLineType"/>.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The type.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public DxfLeaderType GetLeaderLineType(int leaderLineIndex)
        {
            Rec rec = Read();
            return (DxfLeaderType)Whole(LineOwn(rec, leaderLineIndex, LineTypeFlag, 170) ?? Text(rec.After, 170), 1);
        }

        /// <summary>
        /// Gives a leader line a type of its own, as AutoCAD's <c>SetLeaderLineType</c>: its group 170 written and bit 1 of its
        /// flags (93) set, as the DXF reference and the ODA's specification of DWG place them after its index (91), from R2010.
        /// The type the line is drawn with already changes nothing.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="leaderLineType">The type.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index, or the type is not one of <see cref="DxfLeaderType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown in a file older than R2010 (AC1024), or when the MULTILEADER is erased.</exception>
        public void SetLeaderLineType(int leaderLineIndex, DxfLeaderType leaderLineType)
        {
            if (leaderLineType == GetLeaderLineType(leaderLineIndex))
            {
                return;
            }

            if (leaderLineType < DxfLeaderType.InVisibleLeader || leaderLineType > DxfLeaderType.SplineLeader)
            {
                throw new ArgumentOutOfRangeException(nameof(leaderLineType), leaderLineType, "A leader line is invisible, straight or a spline, 0 to 2.");
            }

            SetLineOwn(leaderLineIndex, 170, ((int)leaderLineType).ToString(CultureInfo.InvariantCulture), LineTypeFlag);
        }

        /// <summary>
        /// Gets the colour of a leader line, as AutoCAD's <c>GetLeaderLineColor</c>: its own (group 92 of the line) where it
        /// overrides the multileader's (bit 2 of its 93), or where the line has no flags and its colour is not BYBLOCK, as ezdxf
        /// writes and <see cref="Explode"/> draws such a line; else <see cref="LeaderLineColor"/>.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The colour.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public DxfColor GetLeaderLineColor(int leaderLineIndex)
        {
            Rec rec = Read();
            return DxfMLeaderColors.Of(LineColour(FindLine(rec, leaderLineIndex, out _).Groups) ?? Text(rec.After, 91) ?? ByBlock);
        }

        /// <summary>
        /// The colour of a leader line's own, as the multileader writes one: its 92 where its flags say it overrides the
        /// multileader's, BYBLOCK among them, or where it has no flags and its colour is not BYBLOCK, as ezdxf writes one; null
        /// where it has none, and is drawn in the multileader's.
        /// </summary>
        private static string LineColour(List<KeyValuePair<int, string>> line)
        {
            string own = Own(line, LineColorFlag, 92);

            if (own != null)
            {
                return own;
            }

            string colour = Text(line, 92);
            return Text(line, 93) == null && colour != null && colour.Trim() != ByBlock ? colour : null;
        }

        /// <summary>
        /// Gives a leader line a colour of its own, as AutoCAD's <c>SetLeaderLineColor</c>: its group 92 written and bit 2 of its
        /// flags (93) set, from R2010; <see cref="Explode"/> draws the line in it. The colour the line is drawn in already
        /// changes nothing.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="leaderLineTypeId">The colour, as AutoCAD names the argument.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown in a file older than R2010 (AC1024), or when the MULTILEADER is erased.</exception>
        public void SetLeaderLineColor(int leaderLineIndex, DxfColor leaderLineTypeId)
        {
            string text = DxfMLeaderColors.Text(leaderLineTypeId, nameof(leaderLineTypeId));

            if (Whole(text, 0) == DxfMLeaderColors.Raw(GetLeaderLineColor(leaderLineIndex), nameof(leaderLineTypeId)))
            {
                return;
            }

            SetLineOwn(leaderLineIndex, 92, text, LineColorFlag);
        }

        /// <summary>
        /// Gets the lineweight of a leader line, as AutoCAD's <c>GetLeaderLineWeight</c>: its own (group 171 of the line) where
        /// it overrides the multileader's (bit 8 of its 93), else <see cref="LeaderLineWeight"/>.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The lineweight.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public DxfLineWeight GetLeaderLineWeight(int leaderLineIndex)
        {
            Rec rec = Read();
            return (DxfLineWeight)Whole(LineOwn(rec, leaderLineIndex, LineWeightFlag, 171) ?? Text(rec.After, 171), -2);
        }

        /// <summary>
        /// Gives a leader line a lineweight of its own, as AutoCAD's <c>SetLeaderLineWeight</c>: its group 171 written and bit 8
        /// of its flags (93) set, from R2010. The lineweight the line is drawn with already changes nothing.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="leaderLineWeight">The lineweight.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index, or the lineweight is out of AutoCAD's range, -3 to 211.</exception>
        /// <exception cref="InvalidOperationException">Thrown in a file older than R2010 (AC1024), or when the MULTILEADER is erased.</exception>
        public void SetLeaderLineWeight(int leaderLineIndex, DxfLineWeight leaderLineWeight)
        {
            if (leaderLineWeight == GetLeaderLineWeight(leaderLineIndex))
            {
                return;
            }

            if ((int)leaderLineWeight < -3 || (int)leaderLineWeight > 211)
            {
                throw new ArgumentOutOfRangeException(nameof(leaderLineWeight), leaderLineWeight, "A lineweight is one of AutoCAD's, -3 to 211.");
            }

            SetLineOwn(leaderLineIndex, 171, ((int)leaderLineWeight).ToString(CultureInfo.InvariantCulture), LineWeightFlag);
        }

        /// <summary>
        /// Gets the size of a leader line's arrow, as AutoCAD's <c>GetArrowSize</c> of a line: its own (group 40 of the line)
        /// where it overrides the multileader's (bit 16 of its 93), else <see cref="ArrowSize"/>; unscaled, as
        /// <see cref="ArrowSize"/> is, the arrow drawn times the <see cref="Scale"/>.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public double GetArrowSize(int leaderLineIndex)
        {
            Rec rec = Read();
            return Num(LineOwn(rec, leaderLineIndex, ArrowSizeFlag, 40) ?? Text(rec.After, 42), 4.0);
        }

        /// <summary>
        /// Gives a leader line's arrow a size of its own, as AutoCAD's <c>SetArrowSize</c> of a line: its group 40 written and
        /// bit 16 of its flags (93) set, from R2010; a line of a multileader of arrows 4 long given 2.5 reads 2.5, the others 4.
        /// The size is unscaled, as <see cref="ArrowSize"/> is: <see cref="Explode"/> draws it times the <see cref="Scale"/>, a
        /// line given 4.5 on a multileader of scale 2 drawn 9. The size the arrow has already changes nothing.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="arrowSize">The size, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index, or the size is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown in a file older than R2010 (AC1024), or when the MULTILEADER is erased.</exception>
        public void SetArrowSize(int leaderLineIndex, double arrowSize)
        {
            if (Same(arrowSize, GetArrowSize(leaderLineIndex)))
            {
                return;
            }

            Guard.NonNegative(arrowSize, nameof(arrowSize), "An arrow's size has to be a finite number, nought or more.");
            SetLineOwn(leaderLineIndex, 40, Real(arrowSize), ArrowSizeFlag);
        }

        /// <summary>A leader line's own value of a code where its flags (93) say it overrides the multileader's; null where it does not.</summary>
        private static string LineOwn(Rec rec, int leaderLineIndex, int flag, int code) => Own(FindLine(rec, leaderLineIndex, out _).Groups, flag, code);

        /// <summary>The value of a code of a leader line's groups where its flags (93) say it overrides the multileader's; null where they do not.</summary>
        private static string Own(List<KeyValuePair<int, string>> line, int flag, int code) => (Whole(Text(line, 93), 0) & flag) != 0 ? Text(line, code) : null;

        /// <summary>
        /// Writes a leader line's own value of a code and sets its flag (93), from R2010; a line of no flags whose colour was
        /// read as its own, as ezdxf writes one, is given the flag of its colour with it, so that it keeps its colour.
        /// </summary>
        private void SetLineOwn(int leaderLineIndex, int code, string text, int flag)
        {
            Item.File.Requires(1024, "a leader line's own look", "R2010 (AC1024)");
            Rec rec = Read();
            List<KeyValuePair<int, string>> groups = FindLine(rec, leaderLineIndex, out _).Groups;
            int flags = Whole(Text(groups, 93), 0) | flag;

            if (Text(groups, 93) == null && LineColour(groups) != null)
            {
                flags |= LineColorFlag;
            }

            Put(groups, code, text, LineOrder);
            Put(groups, 93, flags.ToString(CultureInfo.InvariantCulture), LineOrder);
            Write(rec);
        }

        #endregion

        #region The style

        /// <summary>
        /// Gives a style made in code the values the multileader is drawn with, as AutoCAD's <c>getOverridedMLeaderStyle</c>,
        /// named here as the library names its members: those of its multileader style (<see cref="MLeaderStyle"/>), and in
        /// place of each it overrides (a bit of its 90) its own, as ezdxf's <c>MLeaderStyleOverride</c> reads them. A
        /// multileader of the style Standard, its words 2.5 high of its own, gives <see cref="DxfMLeaderStyle.TextHeight"/> 2.5
        /// and the style's arrows, 4 long. An override is the value the multileader's group gives, the style's kept where the
        /// multileader has no group of it, but for the arrows: a multileader of AutoCAD's closed filled arrow gives a style of
        /// no arrow block. Each is read as the multileader writes it, as <c>MLeaderStyleOverride</c> reads it: the height of
        /// the words (bit 16) and the landing gap (bit 5) from the context data, scaled (its 41 and 145, as
        /// <see cref="TextHeight"/> and <see cref="LandingGap"/> read them); the size of the arrows (bit 9) from the
        /// multileader's own 42, unscaled, as the style holds it; the length of the doglegs (bit 7) from its own 41, which is
        /// scaled, as each leader's dogleg is. A multileader of scale 2 with words 8 high, arrows 4 and doglegs 16 of its own
        /// gives a style of words 8 high, arrows 4 and doglegs 16, whose scale is the multileader's 2 where it overrides that
        /// too (bit 24).
        /// </summary>
        /// <param name="mleaderStyle">The style, made in code by <c>new DxfMLeaderStyle()</c>; given these values in place of any it had.</param>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style is one of a drawing, whose record is not written.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the multileader names no multileader style of its drawing, or is in none.</exception>
        public void GetOverridedMLeaderStyle(DxfMLeaderStyle mleaderStyle)
        {
            if (mleaderStyle == null)
            {
                throw new ArgumentNullException(nameof(mleaderStyle));
            }

            if (mleaderStyle.Item != null)
            {
                throw new ArgumentException("The style given is filled in, and has to be made in code by new DxfMLeaderStyle(): " + mleaderStyle + " is of a drawing.", nameof(mleaderStyle));
            }

            Rec rec = Read();
            DxfMLeaderStyle style = StyleOf(rec, out DxfDatabase database) ?? throw new InvalidOperationException("The MULTILEADER " + (Handle ?? string.Empty) + " names no multileader style of its drawing (340).");
            List<KeyValuePair<int, string>> groups = style.Groups();
            int flags = Whole(Text(rec.After, 90), 0);

            foreach (StyleOverride over in StyleOverrides)
            {
                if ((flags & (1 << over.Bit)) == 0)
                {
                    continue;
                }

                string text = Text(over.InContext ? Context(rec).Head : rec.After, over.Code);

                // No block of an arrow is AutoCAD's closed filled arrow, a value of its own.
                if (text != null || over.Bit == 8)
                {
                    Put(groups, over.StyleCode, text, DxfMLeaderStyle.Order);
                }
            }

            mleaderStyle.Fill(groups, style.Name, database);
        }

        /// <summary>
        /// Gives the multileader the values of its multileader style and takes away its overrides of it, as AutoCAD's
        /// <c>SetFromStyle</c>: the style's scale first, then its look of the leader lines, the landing, the dogleg, the arrows,
        /// the words and the block, each written by the property of its name (<see cref="Scale"/>, <see cref="LeaderLineType"/>
        /// ... <see cref="BlockConnectionType"/>) in the multileader and its context data, its linetype (341) and text style
        /// (343, and 340 of its words) by the style's handles; then its overrides (90) set to nought. A multileader of the style
        /// Standard, its words 2.5 high of its own, has words 4 high, as the style. The sizes, unscaled in the style, are scaled
        /// once by the scale the multileader then has: the height of the words, the landing gap and the size of the arrows of
        /// the context data (41, 145 and 140), the multileader's own length of the doglegs (41) and each leader's (40) are the
        /// style's times the scale, and the multileader's own size of the arrows (42) the style's, unscaled; Standard scaled 2
        /// gives words 8 high, a gap of 4, arrows 8 drawn (42 of 4) and doglegs 16. A value the style gives that the multileader
        /// cannot take keeps the multileader's own and its bit: a content (172) or a block (344) it does not hold, a size or a type out of AutoCAD's range, a linetype or a text
        /// style the style names none of or one its drawing has not, an arrow block or a block a copy in no drawing cannot be
        /// given (it keeps its own arrow, and takes the rest), and the attachments of the top and bottom and their direction in a
        /// file older than R2010; so do the bits of no property the library writes (18 and 26). What it cannot take is judged
        /// before anything is written, so that nothing is left half set. The overrides of each leader line (its 93) are the
        /// multileader's own, not the style's, and stay.
        /// </summary>
        /// <returns>Whether the multileader names a multileader style of its drawing it is set from (for a copy in no file, of
        /// the drawing it was copied from); false, and nothing changed, where it names none, or has no context data to be drawn
        /// from.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the multileader is erased and differs from its style.</exception>
        public override bool SetFromStyle()
        {
            DxfMLeaderStyle style = StyleOf(Read(), out DxfDatabase database);

            if (style == null)
            {
                return false;
            }

            int flags = Whole(Text(Read().After, 90), 0);
            // The bits of the properties written below, 0 to 29 but 18 (the words' default content) and 26 (no group known).
            const int Written = ((1 << 30) - 1) & ~((1 << 18) | (1 << 26));
            int kept = flags & ~Written;

            void Keep(int bit) => kept |= flags & (1 << bit);

            // What the multileader cannot take, judged before anything is written: a record the style names that its drawing
            // has not, and a block the multileader cannot be given, a copy in no drawing being given none.
            string arrow = style.Text(341)?.Trim();
            DxfBlockTableRecord arrowBlock = style.ArrowSymbol;
            bool arrowTaken = arrowBlock == null ? arrow == null || arrow.Length == 0 || arrow == "0" : Holds(arrowBlock);
            DxfBlockTableRecord block = style.Block;
            bool blockTaken = block != null && !block.IsLayout && Holds(block);
            string linetype = Named<DxfLinetypeTableRecord>(database, style.Text(340));
            string textStyle = Named<DxfTextStyleTableRecord>(database, style.Text(342));

            // The scale first: setting it scales the sizes the multileader holds, those the style gives then written over.
            if (style.Scale > 0.0 && Guard.IsFinite(style.Scale))
            {
                Scale = style.Scale;
            }
            else
            {
                Keep(24);
            }

            if (style.LeaderLineType >= DxfLeaderType.InVisibleLeader && style.LeaderLineType <= DxfLeaderType.SplineLeader)
            {
                LeaderLineType = style.LeaderLineType;
            }
            else
            {
                Keep(0);
            }

            LeaderLineColor = style.LeaderLineColor;

            if (!TakeHandle(341, linetype, false))
            {
                Keep(2);
            }

            if ((int)style.LeaderLineWeight >= -3 && (int)style.LeaderLineWeight <= 211)
            {
                LeaderLineWeight = style.LeaderLineWeight;
            }
            else
            {
                Keep(3);
            }

            EnableLanding = style.EnableLanding;
            EnableDogleg = style.EnableDogleg;
            TakeSizes(style, Keep);

            if (arrowTaken)
            {
                ArrowSymbol = arrowBlock;
            }
            else
            {
                Keep(8);
            }

            DxfContentType content = style.ContentType;
            Ctx ctx = Context(Read());

            if (content != ContentType)
            {
                if ((content == DxfContentType.MTextContent && HasSection(ctx, 290, MTextCodes)) || (content == DxfContentType.BlockContent && HasSection(ctx, 296, BlockCodes)))
                {
                    ContentType = content;
                }
                else
                {
                    Keep(10);
                }
            }

            if (!TakeHandle(343, textStyle, true))
            {
                Keep(11);
            }

            TakeAttachment(style, DxfLeaderDirectionType.LeftLeader, 12, Keep);
            TakeEnum((int)style.TextAngleType, 0, 2, v => TextAngleType = (DxfTextAngleType)v, 13, Keep);
            TakeEnum((int)style.TextAlignmentType, 0, 2, v => TextAlignmentType = (DxfTextAlignmentType)v, 14, Keep);
            TextColor = style.TextColor;
            EnableFrameText = style.EnableFrameText;

            if (HasSection(Context(Read()), 296, BlockCodes))
            {
                if (blockTaken)
                {
                    BlockContent = block;
                }
                else
                {
                    Keep(19);
                }
            }

            BlockColor = style.BlockColor;
            GeoVector3 scale = style.BlockScale;

            if (scale.IsValid && scale.X != 0.0 && scale.Y != 0.0 && scale.Z != 0.0)
            {
                BlockScale = scale;
            }
            else
            {
                Keep(21);
            }

            if (Guard.IsFinite(style.BlockRotation))
            {
                BlockRotation = style.BlockRotation;
            }
            else
            {
                Keep(22);
            }

            TakeEnum((int)style.BlockConnectionType, 0, 1, v => BlockConnectionType = (DxfBlockConnectionType)v, 23, Keep);
            TakeAttachment(style, DxfLeaderDirectionType.RightLeader, 25, Keep);

            if (Item.File.Supports(1024))
            {
                TakeEnum((int)style.TextAttachmentDirection, 0, 1, v => TextAttachmentDirection = (DxfTextAttachmentDirection)v, 27, Keep);
                TakeAttachment(style, DxfLeaderDirectionType.TopLeader, 28, Keep);
                TakeAttachment(style, DxfLeaderDirectionType.BottomLeader, 29, Keep);
            }
            else
            {
                Keep(27);
                Keep(28);
                Keep(29);
            }

            Rec rec = Read();

            if (Whole(Text(rec.After, 90), 0) != kept)
            {
                Put(rec.After, 90, kept.ToString(CultureInfo.InvariantCulture), TopOrder);
                Write(rec);
            }

            return true;
        }

        /// <summary>The multileader style the record names (340) in the drawing it reads its records in; null where it names none of it.</summary>
        private DxfMLeaderStyle StyleOf(Rec rec, out DxfDatabase database)
        {
            // A copy in no file reads what its record names in the drawing it was copied from.
            database = IsResident ? Database() : Item.File.IsDetached ? Item.File.NamingDrawing?.Database : null;
            string handle = Text(rec.After, 340)?.Trim();
            return handle != null && database != null && database.TryGetObject(handle, out DxfDBObject found) ? found as DxfMLeaderStyle : null;
        }

        /// <summary>Whether a block can be given to the multileader, as <see cref="ArrowSymbol"/> and <see cref="BlockContent"/> take one: a block of its own drawing.</summary>
        private bool Holds(DxfBlockTableRecord block) => block.Handle != null && ReferenceEquals(BlockByHandle(block.Handle), block);

        /// <summary>A handle the style gives where the drawing has a record of the type of it; null where it gives none, or one the drawing has not.</summary>
        private static string Named<T>(DxfDatabase database, string handle) where T : DxfDBObject
        {
            handle = handle?.Trim();
            return handle != null && handle.Length > 0 && handle != "0" && database != null && database.TryGetObject(handle, out DxfDBObject found) && found is T ? handle : null;
        }

        /// <summary>Writes a handle of the style in the multileader's own group of a code, and in its words' 340 where asked; false where there is none to write.</summary>
        private bool TakeHandle(int code, string handle, bool words)
        {
            if (handle == null)
            {
                return false;
            }

            Rec rec = Read();
            bool changed = Put(rec.After, code, handle, TopOrder);

            if (words && rec.Context != null)
            {
                Section(rec.Context.Head, 290, MTextCodes, out int from, out int to);
                changed |= from >= 0 && Put(rec.Context.Head, 340, handle, ContextOrder, from, to);
            }

            if (changed)
            {
                Write(rec);
            }

            return true;
        }

        /// <summary>
        /// Writes the sizes of the style, unscaled in it, scaled once: the height of the words, the landing gap and the size of
        /// the arrows of the context data (41, 145 and 140), the multileader's length of the doglegs (41) and each leader's (40)
        /// the style's times the scale of the context data (40); the multileader's size of the arrows (42) the style's,
        /// unscaled. Each is written where it differs from the one read; a size out of range keeps the multileader's own and its
        /// bit.
        /// </summary>
        private void TakeSizes(DxfMLeaderStyle style, Action<int> keep)
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            double scale = Num(Text(ctx.Head, 40), 1.0);

            bool Size(double value, bool positive) => Guard.IsFinite(value) && (positive ? value > 0.0 : value >= 0.0);

            if (Size(style.TextHeight, true))
            {
                PutSize(ctx.Head, 41, style.TextHeight * scale, 4.0, ContextOrder);
            }
            else
            {
                keep(16);
            }

            if (Size(style.LandingGap, false))
            {
                PutSize(ctx.Head, 145, style.LandingGap * scale, 2.0, ContextOrder);
            }
            else
            {
                keep(5);
            }

            if (Size(style.ArrowSize, false))
            {
                double arrow = style.ArrowSize;
                PutSize(rec.After, 42, arrow, 4.0, TopOrder);

                // Read as Explode reads it: the multileader's own where the context data gives none.
                PutSize(ctx.Head, 140, arrow * scale, arrow, ContextOrder);
            }
            else
            {
                keep(9);
            }

            if (Size(style.DoglegLength, false))
            {
                double dogleg = style.DoglegLength;
                PutSize(rec.After, 41, dogleg * scale, 8.0, TopOrder);

                foreach (LeaderPart leader in ctx.Leaders)
                {
                    // Read as GetDoglegLength reads it, 1 where the leader gives none.
                    PutSize(leader.Head, 40, dogleg * scale, 1.0, LeaderOrder);
                }
            }
            else
            {
                keep(7);
            }

            Write(rec);
        }

        /// <summary>Sets a size in a run where it differs, to the bit, from the one read, the fallback where the run has none.</summary>
        private static void PutSize(List<KeyValuePair<int, string>> groups, int code, double value, double fallback, int[] order)
        {
            if (!Same(Num(Text(groups, code), fallback), value))
            {
                Put(groups, code, Real(value), order);
            }
        }

        private static void TakeEnum(int value, int least, int most, Action<int> set, int bit, Action<int> keep)
        {
            if (value >= least && value <= most)
            {
                set(value);
            }
            else
            {
                keep(bit);
            }
        }

        private void TakeAttachment(DxfMLeaderStyle style, DxfLeaderDirectionType side, int bit, Action<int> keep)
        {
            DxfTextAttachmentType value = style.GetTextAttachmentType(side);

            if (value >= DxfTextAttachmentType.AttachmentTopOfTop && value <= DxfTextAttachmentType.AttachmentLinedCenter)
            {
                SetTextAttachmentType(value, side);
            }
            else
            {
                keep(bit);
            }
        }

        /// <summary>A property of a multileader style a MULTILEADER overrides: the bit of its 90, where its own value is, its code there and the style's.</summary>
        private struct StyleOverride
        {
            internal StyleOverride(int bit, bool inContext, int code, int styleCode)
            {
                Bit = bit;
                InContext = inContext;
                Code = code;
                StyleCode = styleCode;
            }

            internal int Bit { get; }

            internal bool InContext { get; }

            internal int Code { get; }

            internal int StyleCode { get; }
        }

        #endregion

        #region The plane

        /// <summary>
        /// Puts the multileader in a plane, as AutoCAD's <c>SetPlane</c>: the plane of its context data the one given, its
        /// origin (110) the plane's and its x axis (111) the one it had laid in the plane, the x axis of the plane's own system
        /// where it stood along the normal, its y axis (112) across them, or as it was where it lies in the plane already, on
        /// the side the x axis keeps, so that <see cref="Normal"/> is the plane's whichever way 297 turns it; every point moved
        /// straight onto the plane (the base point of its content, the place of its words and block, the last point of each
        /// leader, the vertices of its lines and the points of their breaks); and every direction laid in it (the words'
        /// direction and turn, the block's turn and matrix, the doglegs), the normals of its words and block the plane's, or the
        /// other way where they faced away from its plane before, as the words of a mirrored multileader (297 of 1, its words
        /// of normal -z over a plane of +z) are read from behind it. The multileader of the floor whose words are at (800, 0, 0) put in the plane z = 5 has its
        /// words at (800, 0, 5) and its leaders' vertices 5 up; put in a plane x = 0 its words go to (0, 0, 0). A point and a
        /// direction in the plane already keep their numbers, of whatever length, and axes that lean to each other, as a shear
        /// leaves them, keep theirs, so that the multileader put in its own plane changes nothing.
        /// </summary>
        /// <param name="value">The plane.</param>
        /// <exception cref="ArgumentException">Thrown when the plane is not valid: a point not finite, or a normal of no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the multileader has no context data, or is erased.</exception>
        public void SetPlane(GeoPlane3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A plane has a finite origin and a normal of unit length: " + value + " has not.", nameof(value));
            }

            // The normal the multileader has before it is put in the plane, that its words and block face the side of.
            GeoVector3 before = Normal;
            Rec rec = Read();
            Ctx ctx = Context(rec);
            GeoVector3 n = value.Normal;
            GeoPoint3 origin = value.Origin;
            GeoPoint3 Onto(GeoPoint3 p) => DxfLeader.OntoPlane(p, origin, n);
            var axes = new DxfOcsTransform.Axes(n);
            var worldZ = new GeoPoint3(0.0, 0.0, 1.0);

            // The plane: its origin, its x axis laid in the plane, and its y across them, turned as 297 turns the normal; the y
            // it had where it lies in the plane on that side of the x it had, as a sheared multileader has it.
            bool over = Whole(Text(ctx.Head, 297), 0) != 0;
            GeoVector3 oldX = PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
            GeoVector3 oldY = PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector();
            GeoVector3 x = DxfLeader.InPlane(oldX, n, axes.X);
            GeoVector3 unitX = Math.Abs(x.Length - 1.0) <= 1E-12 ? x : DxfOcsTransform.Normalized(x);
            GeoVector3 y = DxfOcsTransform.Cross(n, unitX);
            y = over ? y.Multiply(-1.0) : y;
            double side = DxfOcsTransform.Cross(x, oldY).DotProduct(n);
            bool leans = SamePoint(Pt(x), Pt(oldX)) && Math.Abs(oldY.DotProduct(n)) <= 1E-12 * oldY.Length && (over ? side < 0.0 : side > 0.0);
            PutWhereNeeded(ctx.Head, 110, origin, new GeoPoint3(0.0, 0.0, 0.0), ContextOrder, 0, -1);
            PutWhereNeeded(ctx.Head, 111, Pt(x), new GeoPoint3(1.0, 0.0, 0.0), ContextOrder, 0, -1);
            PutWhereNeeded(ctx.Head, 112, Pt(leans ? oldY : DxfLeader.Kept(oldY, y)), new GeoPoint3(0.0, 1.0, 0.0), ContextOrder, 0, -1);
            MovePoint(ctx.Head, 10, Onto, ContextOrder, 0, -1);

            // The words: their normal the plane's, facing the side they faced, their place onto it, their direction and turn laid
            // in it.
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
                GeoVector3 oldNormal = PointOf(m, 11, worldZ).ToVector();
                GeoVector3 direction = PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                GeoVector3 facing = Facing(oldNormal, before, n);
                var faced = new DxfOcsTransform.Axes(facing);
                GeoVector3 normal = DxfLeader.Kept(oldNormal, facing);
                string turn = Text(m, 42);
                PutWhereNeeded(ctx.Head, 11, Pt(normal), worldZ, ContextOrder, from, to);
                Section(ctx.Head, 290, MTextCodes, out from, out to);
                MovePoint(ctx.Head, 12, Onto, ContextOrder, from, to);
                Section(ctx.Head, 290, MTextCodes, out from, out to);
                PutWhereNeeded(ctx.Head, 13, Pt(DxfLeader.InPlane(direction, n, unitX)), new GeoPoint3(1.0, 0.0, 0.0), ContextOrder, from, to);

                if (turn != null)
                {
                    Section(ctx.Head, 290, MTextCodes, out from, out to);
                    double radians = Laid(Num(turn, 0.0), oldNormal, normal, n, unitX, faced, out bool turned);

                    if (turned)
                    {
                        PutNumber(ctx.Head, 42, radians, 0.0, ContextOrder, from, to);
                    }
                }
            }

            // The block: its normal the plane's, facing the side it faced, its place onto it, its turn laid in it, its matrix
            // carried with it.
            Section(ctx.Head, 296, BlockCodes, out from, out to);

            if (from >= 0)
            {
                List<KeyValuePair<int, string>> b = ctx.Head.GetRange(from, to - from);
                GeoVector3 oldNormal = PointOf(b, 14, worldZ).ToVector();
                GeoVector3 facing = Facing(oldNormal, before, n);
                var faced = new DxfOcsTransform.Axes(facing);
                GeoVector3 normal = DxfLeader.Kept(oldNormal, facing);
                GeoPoint3 was = PointOf(b, 15, PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0)));
                GeoPoint3 now = Onto(was);
                double radians = Laid(Num(Text(b, 46), 0.0), oldNormal, normal, n, unitX, faced, out bool turned);
                PutWhereNeeded(ctx.Head, 14, Pt(normal), worldZ, ContextOrder, from, to);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
                MovePoint(ctx.Head, 15, Onto, ContextOrder, from, to);

                if (turned)
                {
                    Section(ctx.Head, 296, BlockCodes, out from, out to);
                    PutNumber(ctx.Head, 46, radians, 0.0, ContextOrder, from, to);
                    PutNumber(rec.After, 43, radians, 0.0, TopOrder);
                    Section(ctx.Head, 296, BlockCodes, out from, out to);
                    Matrix(ctx.Head, from, to, Carried(oldNormal, Num(Text(b, 46), 0.0), was, facing, radians, faced, now));
                }
                else if (!SamePoint(was, now))
                {
                    Shift(ctx.Head, now.X - was.X, now.Y - was.Y, now.Z - was.Z);
                }
            }

            // The leaders: their last points onto the plane, their doglegs laid in it, their breaks and their lines' onto it.
            foreach (LeaderPart leader in ctx.Leaders)
            {
                MovePoint(leader.Head, 10, Onto, LeaderOrder, 0, -1);

                if (Text(leader.Head, 11) != null)
                {
                    PutPoint(leader.Head, 11, Pt(DxfLeader.InPlane(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector(), n, unitX)), LeaderOrder);
                }

                OntoAll(leader.Head, new[] { 12, 13 }, Onto);

                foreach (LinePart line in leader.Lines)
                {
                    OntoAll(line.Groups, new[] { 10, 11, 12 }, Onto);
                }
            }

            Write(rec);
        }

        /// <summary>
        /// The normal of the words or the block in the plane of a normal: the plane's, or the other way where they faced away
        /// from the multileader's plane before, as the words of a mirrored multileader do.
        /// </summary>
        private static GeoVector3 Facing(GeoVector3 oldNormal, GeoVector3 before, GeoVector3 n) => oldNormal.DotProduct(before) < 0.0 ? n.Multiply(-1.0) : n;

        /// <summary>Sets a point in a part of a run as <see cref="PutPoint"/> sets it; nothing where the run has none and the point is the one read in its place.</summary>
        private static void PutWhereNeeded(List<KeyValuePair<int, string>> groups, int code, GeoPoint3 value, GeoPoint3 fallback, int[] order, int from, int to)
        {
            to = to < 0 ? groups.Count : to;

            if (groups.FindIndex(from, to - from, g => g.Key == code) >= 0 || !SamePoint(value, fallback))
            {
                PutPoint(groups, code, value, order, from, to);
            }
        }

        /// <summary>
        /// An angle of the words or the block in the system of their normal, laid in the plane: the direction it gives laid in
        /// the plane, and its angle in the system of the plane's normal; false for <paramref name="turned"/> where the normal
        /// and the direction are as they were, the angle then kept.
        /// </summary>
        private static double Laid(double radians, GeoVector3 oldNormal, GeoVector3 normal, GeoVector3 n, GeoVector3 x, DxfOcsTransform.Axes axes, out bool turned)
        {
            var old = new DxfOcsTransform.Axes(oldNormal.Length > 0.0 ? oldNormal : new GeoVector3(0.0, 0.0, 1.0));
            GeoVector3 direction = old.ToWorld(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
            GeoVector3 laid = DxfLeader.InPlane(direction, n, x);
            turned = !(SamePoint(Pt(oldNormal), Pt(normal)) && SamePoint(Pt(direction), Pt(laid)));

            if (!turned)
            {
                return radians;
            }

            GeoVector3 local = axes.FromWorld(laid);
            return Math.Atan2(local.Y, local.X);
        }

        /// <summary>
        /// The move that carries the block to the plane: its frame (the x of its turn, its normal and the y across them) turned
        /// onto the new one about the place it was at, then taken to the place it is at now.
        /// </summary>
        private static GeoTransform3 Carried(GeoVector3 oldNormal, double oldRadians, GeoPoint3 was, GeoVector3 n, double radians, DxfOcsTransform.Axes axes, GeoPoint3 now)
        {
            var old = new DxfOcsTransform.Axes(oldNormal.Length > 0.0 ? oldNormal : new GeoVector3(0.0, 0.0, 1.0));
            GeoVector3 x0 = old.ToWorld(new GeoVector3(Math.Cos(oldRadians), Math.Sin(oldRadians), 0.0));
            GeoVector3 z0 = old.Z;
            GeoVector3 y0 = DxfOcsTransform.Cross(z0, x0);
            GeoVector3 x1 = axes.ToWorld(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
            GeoVector3 y1 = DxfOcsTransform.Cross(n, x1);
            GeoVector3[] from = { x0, y0, z0 };
            GeoVector3[] to = { x1, y1, n };
            var m = new double[4, 4];

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    double sum = 0.0;

                    for (int k = 0; k < 3; k++)
                    {
                        sum += Component(to[k], r) * Component(from[k], c);
                    }

                    m[r, c] = sum;
                }
            }

            double[] p = { was.X, was.Y, was.Z };
            double[] q = { now.X, now.Y, now.Z };

            for (int r = 0; r < 3; r++)
            {
                m[r, 3] = q[r] - (m[r, 0] * p[0] + m[r, 1] * p[1] + m[r, 2] * p[2]);
            }

            m[3, 3] = 1.0;
            return new GeoTransform3(m);
        }

        private static double Component(GeoVector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        /// <summary>Moves every point of some codes in a run straight onto a plane, each with the groups 10 and 20 above it; one already on it keeps its text.</summary>
        private static void OntoAll(List<KeyValuePair<int, string>> groups, int[] codes, Func<GeoPoint3, GeoPoint3> onto)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (Array.IndexOf(codes, groups[i].Key) < 0)
                {
                    continue;
                }

                int code = groups[i].Key;
                GeoPoint3 p = PointAt(groups, i);
                GeoPoint3 q = onto(p);

                if (SamePoint(p, q))
                {
                    continue;
                }

                groups[i] = G(code, q.X);

                for (int k = 1; k <= 2 && i + 1 < groups.Count && groups[i + 1].Key == code + 10 * k; k++)
                {
                    i++;
                    groups[i] = G(code + 10 * k, k == 1 ? q.Y : q.Z);
                }
            }
        }

        #endregion
    }
}
