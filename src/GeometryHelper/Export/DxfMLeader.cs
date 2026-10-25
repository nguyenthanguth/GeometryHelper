using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Export.DxfLeaderGroups;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A MULTILEADER of a DXF file, as AutoCAD's <c>MLeader</c>: words of an MTEXT or a block (its content) with leaders that
    /// point from it, each leader a dogleg and one or more leader lines, drawn by its multileader style with the properties it
    /// overrides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file holds a MULTILEADER as AutoCAD keeps one: its own properties, then its context data (groups 300 to 301) with
    /// the content, the plane and the leaders (302 to 303), each with its lines (304 to 305). A MULTILEADER of the words
    /// "Line one" held at (800, 0, 0) with one leader whose line runs from (780, -10, 0) to its dogleg at (790, -1.25, 0) has
    /// <see cref="ContentType"/> <see cref="DxfContentType.MTextContent"/>, <see cref="LeaderCount"/> 1, <see cref="LeaderLineCount"/>
    /// 1, and its line 0 has <see cref="VerticesCount"/> 2: <see cref="GetFirstVertex"/> (780, -10, 0) where its arrow is, and
    /// <see cref="GetLastVertex"/> (790, -1.25, 0), the point of its leader the dogleg starts at, which every line of the leader
    /// ends at.
    /// </para>
    /// <para>
    /// Leaders and lines are named by their indices, as AutoCAD names them (90 of a leader, 91 of a line), not by their place.
    /// A property set writes the MULTILEADER's own group and the one of its context data AutoCAD keeps with it, and sets its bit
    /// of the overrides of the style (group 90), as AutoCAD sets it for a property given a value of its own. A property given
    /// the value it has changes nothing. What AutoCAD works out when it draws the MULTILEADER (where the leaders meet the words,
    /// the box of the words, the breaks) is not worked out here: the library writes the data AutoCAD draws it from.
    /// </para>
    /// </remarks>
    public sealed partial class DxfMLeader : DxfEntity
    {
        private const string ByBlock = "-1056964608";

        /// <summary>
        /// Initializes a MULTILEADER of no content and no leaders, its plane the floor, as AutoCAD's <c>new MLeader()</c>, on layer
        /// 0 in no file: added to a drawing, it takes the drawing's multileader style (CMLEADERSTYLE, else Standard) and the text
        /// style Standard. Give it leaders with <see cref="AddLeaderLine(GeoPoint3)"/> and words with <see cref="MText"/>.
        /// </summary>
        public DxfMLeader()
            : base("MULTILEADER", Fresh())
        {
        }

        private DxfMLeader(DxfItem item)
            : base(item)
        {
        }

        [DxfType("MULTILEADER")]
        internal static DxfEntity Create(DxfItem item) => new DxfMLeader(item);

        /// <summary>
        /// Refuses to break the multileader into other entities: AutoCAD's <c>Explode</c> gives its words or block and the lines
        /// of its leaders where they meet them, with its doglegs and arrows, all of which AutoCAD works out when it draws it and the
        /// library does not work out. Its <see cref="MText"/> and its leaders' vertices are read as they are.
        /// </summary>
        /// <returns>Nothing: it throws.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
            => throw new NotSupportedException("A multileader's parts, where its leaders meet its words, its doglegs and its arrows, are worked out by AutoCAD when it draws it, which the library does not do: explode it in AutoCAD, or read its MText and leaders.");

        #region The content

        /// <summary>
        /// Gets or sets what the leaders point to (group 172), as AutoCAD's <c>ContentType</c>;
        /// <see cref="DxfContentType.MTextContent"/> where the record gives none.
        /// </summary>
        /// <value>
        /// Set to <see cref="DxfContentType.NoneContent"/>, the MTEXT and the block of the context data are taken out; set to
        /// another, the MULTILEADER has to hold that content already: words are given by <see cref="MText"/>, a block by
        /// <see cref="BlockContent"/>, and a feature control frame is not written by the library.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfContentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the content set is not held, or the MULTILEADER is erased.</exception>
        public DxfContentType ContentType
        {
            get => (DxfContentType)Whole(Text(Read().After, 172), 2);

            set
            {
                if (value == ContentType)
                {
                    return;
                }

                if (value < DxfContentType.NoneContent || value > DxfContentType.ToleranceContent)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A content is none, a block, words or a feature control frame, 0 to 3.");
                }

                Rec rec = Read();
                Ctx ctx = Context(rec);

                if (value != DxfContentType.NoneContent)
                {
                    bool held = value == DxfContentType.MTextContent ? HasSection(ctx, 290, MTextCodes) : value == DxfContentType.BlockContent && HasSection(ctx, 296, BlockCodes);

                    if (!held)
                    {
                        throw new InvalidOperationException("The MULTILEADER holds no " + value + ": give it words by MText or a block by BlockContent.");
                    }
                }
                else
                {
                    DropSection(ctx, 290, MTextCodes);
                    DropSection(ctx, 296, BlockCodes);
                }

                SetTop(rec, 172, ((int)value).ToString(CultureInfo.InvariantCulture), 10);
                Write(rec);
            }
        }

        /// <summary>Gets whether the MULTILEADER holds the content its type says, as AutoCAD's <c>HasContent</c>: words or a block in its context data.</summary>
        /// <returns>Whether it has content.</returns>
        public bool HasContent()
        {
            Ctx ctx = Read().Context;

            switch (ContentType)
            {
                case DxfContentType.MTextContent: return ctx != null && HasSection(ctx, 290, MTextCodes);
                case DxfContentType.BlockContent: return ctx != null && HasSection(ctx, 296, BlockCodes);
                case DxfContentType.ToleranceContent: return true;
                default: return false;
            }
        }

        /// <summary>
        /// Gets or sets the words, as AutoCAD's <c>MText</c>: a <see cref="DxfMText"/> in no file of the MTEXT of the context data,
        /// its words (304), place (12), direction (13), height (the context's 41), width (43), spacing of its lines (45 and 170)
        /// and the point it is held by (171, at the top on the left, middle or right); null where the MULTILEADER holds no words.
        /// </summary>
        /// <value>
        /// Set, an MTEXT whose words, place, direction, height, width, spacing and point are written into the context data, the
        /// height's bit of the overrides set; a MULTILEADER that held no words is given an MTEXT as ezdxf writes one, its block
        /// taken out with the handle of it (344), its content type the words and the base point of its content (10 of the context
        /// data) their place. Null takes the words out, as <see cref="ContentType"/> set to none does.
        /// </value>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfMText MText
        {
            get
            {
                Ctx ctx = Read().Context;

                if (ctx == null)
                {
                    return null;
                }

                Section(ctx.Head, 290, MTextCodes, out int from, out int to);

                if (from < 0)
                {
                    return null;
                }

                List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
                var mtext = new DxfMText
                {
                    Contents = Text(m, 304) ?? string.Empty,
                    Location = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0)),
                    TextHeight = Positive(Num(Text(ctx.Head, 41), 4.0), 4.0),
                    Width = Math.Max(0.0, Num(Text(m, 43), 0.0)),
                    Attachment = (DxfAttachmentPoint)Math.Max(1, Math.Min(3, Whole(Text(m, 171), 1))),
                };

                GeoPoint3 normal = PointOf(m, 11, new GeoPoint3(0.0, 0.0, 1.0));
                GeoPoint3 direction = PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0));

                if (new GeoVector3(normal.X, normal.Y, normal.Z).Length > 0.0)
                {
                    mtext.Normal = new GeoVector3(normal.X, normal.Y, normal.Z);
                }

                if (new GeoVector3(direction.X, direction.Y, direction.Z).Length > 0.0)
                {
                    mtext.Direction = new GeoVector3(direction.X, direction.Y, direction.Z);
                }

                double spacing = Num(Text(m, 45), 1.0);

                if (spacing >= 0.25 && spacing <= 4.0)
                {
                    mtext.LineSpacingFactor = spacing;
                }

                int style = Whole(Text(m, 170), 1);
                mtext.LineSpacingStyle = style == 2 ? DxfLineSpacingStyle.Exactly : DxfLineSpacingStyle.AtLeast;
                return mtext;
            }

            set
            {
                Rec rec = Read();
                Ctx ctx = Context(rec);

                if (value == null)
                {
                    if (HasSection(ctx, 290, MTextCodes))
                    {
                        DropSection(ctx, 290, MTextCodes);

                        if (ContentType == DxfContentType.MTextContent)
                        {
                            SetTop(rec, 172, "0", 10);
                        }

                        Write(rec);
                    }

                    return;
                }

                if (!HasSection(ctx, 290, MTextCodes))
                {
                    DropSection(ctx, 296, BlockCodes);
                    Put(rec.After, 344, null, TopOrder);
                    MakeMText(rec, ctx);
                    SetTop(rec, 172, "2", 10);

                    // The base point of the content, where a leader added starts its dogleg, at the words.
                    PutPoint(ctx.Head, 10, value.Location, ContextOrder);
                }

                Section(ctx.Head, 290, MTextCodes, out int from, out int to);
                List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
                Put(m, 304, value.Contents, ContextOrder);
                PutPoint(m, 12, value.Location, ContextOrder);
                PutPoint(m, 13, new GeoPoint3(value.Direction.X, value.Direction.Y, value.Direction.Z), ContextOrder);
                PutPoint(m, 11, new GeoPoint3(value.Normal.X, value.Normal.Y, value.Normal.Z), ContextOrder);
                PutNumber(m, 43, value.Width, 0.0, ContextOrder);
                PutNumber(m, 45, value.LineSpacingFactor, 1.0, ContextOrder);
                Put(m, 170, ((int)value.LineSpacingStyle).ToString(CultureInfo.InvariantCulture), ContextOrder);
                Put(m, 171, (((int)value.Attachment - 1) % 3 + 1).ToString(CultureInfo.InvariantCulture), ContextOrder);
                ctx.Head.RemoveRange(from, to - from);
                ctx.Head.InsertRange(from, m);

                if (!Same(Num(Text(ctx.Head, 41), 4.0), value.TextHeight))
                {
                    PutNumber(ctx.Head, 41, value.TextHeight, 4.0, ContextOrder);
                    SetBit(rec, 16);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets where the words are held (group 12 of the context's MTEXT), as AutoCAD's <c>TextLocation</c>, in the
        /// world; the base point of the content (10 of the context data) where the MULTILEADER holds no words.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when another place is set on a MULTILEADER that holds no words, or one that is erased.</exception>
        public GeoPoint3 TextLocation
        {
            get => ContentPoint(290, MTextCodes, 12);

            set
            {
                if (SamePoint(value, TextLocation))
                {
                    return;
                }

                Finite(value, nameof(value));
                Rec rec = Read();
                Ctx ctx = Context(rec);
                Section(ctx.Head, 290, MTextCodes, out int from, out int to);

                if (from < 0)
                {
                    throw NoWords();
                }

                PutPoint(ctx.Head, 12, value, ContextOrder, from, to);
                Write(rec);
            }
        }

        /// <summary>Gets or sets how high the letters are (group 41 of the context data, as scaled), as AutoCAD's <c>TextHeight</c>; 4 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double TextHeight
        {
            get => Num(Text(Context(Read()).Head, 41), 4.0);
            set => SetContextNumber(41, value, 4.0, 16, true);
        }

        /// <summary>Gets or sets the colour of the words (group 92, and 90 of the context's MTEXT), as AutoCAD's <c>TextColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfColor TextColor
        {
            get => DxfMLeaderColors.Of(Text(Read().After, 92) ?? ByBlock);
            set => SetColour(92, 90, 290, MTextCodes, value, 15);
        }

        /// <summary>
        /// Gets or sets where the leaders on the left meet the words (group 173, and 174 of the context data), as AutoCAD's
        /// <c>TextAttachmentType</c>; set, those on the right (95, and 175 of the context data) are given it too.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfTextAttachmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfTextAttachmentType TextAttachmentType
        {
            get => GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader);

            set
            {
                if (value == TextAttachmentType && value == GetTextAttachmentType(DxfLeaderDirectionType.RightLeader))
                {
                    return;
                }

                Attachment(value);
                Rec rec = Read();
                Ctx ctx = Context(rec);
                string text = ((int)value).ToString(CultureInfo.InvariantCulture);
                SetTop(rec, 173, text, 12);
                SetTop(rec, 95, text, 25);
                Put(ctx.Head, 174, text, ContextOrder);
                Put(ctx.Head, 175, text, ContextOrder);
                Write(rec);
            }
        }

        /// <summary>
        /// Gets where the leaders of a side meet the words, as AutoCAD's <c>GetTextAttachmentType</c>: the left (group 173), the
        /// right (95), the top (273) or the bottom (272), the last two from R2010.
        /// </summary>
        /// <param name="leaderDirection">The side.</param>
        /// <returns>Where they meet: AutoCAD's middle of the top line for the sides, its middle for the top and the bottom, where the record gives none.</returns>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        public DxfTextAttachmentType GetTextAttachmentType(DxfLeaderDirectionType leaderDirection)
        {
            AttachmentCodes(leaderDirection, out int code, out _, out int fallback, out _);
            return (DxfTextAttachmentType)Whole(Text(Read().After, code), fallback);
        }

        /// <summary>
        /// Sets where the leaders of a side meet the words, as AutoCAD's <c>SetTextAttachmentType</c>: the MULTILEADER's group of
        /// the side and that of its context data, and the side's bit of the overrides.
        /// </summary>
        /// <param name="textAttachmentType">Where they meet.</param>
        /// <param name="leaderDirection">The side.</param>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfTextAttachmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the top or the bottom is set in a file older than R2010 (AC1024), or the MULTILEADER is erased.</exception>
        public void SetTextAttachmentType(DxfTextAttachmentType textAttachmentType, DxfLeaderDirectionType leaderDirection)
        {
            AttachmentCodes(leaderDirection, out int code, out int context, out _, out int bit);

            if (textAttachmentType == GetTextAttachmentType(leaderDirection))
            {
                return;
            }

            Attachment(textAttachmentType);

            if (code == 272 || code == 273)
            {
                Item.File.Requires(1024, "the attachment of the top and the bottom of a MULTILEADER", "R2010 (AC1024)");
            }

            Rec rec = Read();
            string text = ((int)textAttachmentType).ToString(CultureInfo.InvariantCulture);
            SetTop(rec, code, text, bit);
            Ctx ctx = Context(rec);

            if (code == 272 || code == 273)
            {
                Put(ctx.Tail, context, text, new[] { 272, 273 });
            }
            else
            {
                Put(ctx.Head, context, text, ContextOrder);
            }

            Write(rec);
        }

        /// <summary>
        /// Gets or sets how the words are lined up (group 175, 176 of the context data, and 171 of its MTEXT), as AutoCAD's
        /// <c>TextAlignmentType</c>; on the left where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfTextAlignmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfTextAlignmentType TextAlignmentType
        {
            get => (DxfTextAlignmentType)Whole(Text(Read().After, 175), 0);

            set
            {
                if (value == TextAlignmentType)
                {
                    return;
                }

                if (value < DxfTextAlignmentType.LeftAlignment || value > DxfTextAlignmentType.RightAlignment)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Words are lined up on the left, in the middle or on the right, 0 to 2.");
                }

                Rec rec = Read();
                Ctx ctx = Context(rec);
                string text = ((int)value).ToString(CultureInfo.InvariantCulture);
                SetTop(rec, 175, text, 14);
                Put(ctx.Head, 176, text, ContextOrder);
                Section(ctx.Head, 290, MTextCodes, out int from, out int to);

                if (from >= 0)
                {
                    Put(ctx.Head, 171, ((int)value + 1).ToString(CultureInfo.InvariantCulture), ContextOrder, from, to);
                }

                Write(rec);
            }
        }

        /// <summary>Gets or sets how the words are turned (group 174), as AutoCAD's <c>TextAngleType</c>; level where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfTextAngleType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfTextAngleType TextAngleType
        {
            get => (DxfTextAngleType)Whole(Text(Read().After, 174), 1);
            set => SetTopEnum(174, (int)value, (int)TextAngleType, 0, 2, 13);
        }

        /// <summary>
        /// Gets or sets which sides of the words the leaders meet (group 271, and 271 of each leader, from R2010), as AutoCAD's
        /// <c>TextAttachmentDirection</c>; the left and the right where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction set is not one of <see cref="DxfTextAttachmentDirection"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when it is changed in a file older than R2010 (AC1024), or the MULTILEADER is erased.</exception>
        public DxfTextAttachmentDirection TextAttachmentDirection
        {
            get => (DxfTextAttachmentDirection)Whole(Text(Read().After, 271), 0);

            set
            {
                if (value == TextAttachmentDirection)
                {
                    return;
                }

                if (value < DxfTextAttachmentDirection.AttachmentHorizontal || value > DxfTextAttachmentDirection.AttachmentVertical)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Leaders meet the words on the left and right, 0, or the top and bottom, 1.");
                }

                Item.File.Requires(1024, "the attachment direction of a MULTILEADER", "R2010 (AC1024)");
                Rec rec = Read();
                string text = ((int)value).ToString(CultureInfo.InvariantCulture);
                SetTop(rec, 271, text, 27);

                foreach (LeaderPart leader in Context(rec).Leaders)
                {
                    Put(leader.Tail, 271, text, new[] { 271 });
                }

                Write(rec);
            }
        }

        /// <summary>Gets or sets whether a frame is drawn round the words (group 292), as AutoCAD's <c>EnableFrameText</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public bool EnableFrameText
        {
            get => Whole(Text(Read().After, 292), 0) != 0;
            set => SetTopFlag(292, value, EnableFrameText, 17);
        }

        /// <summary>Gets or sets whether the leaders run on to the words (group 295, from R2013), as AutoCAD's <c>ExtendLeaderToText</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when it is changed in a file older than R2013 (AC1027), or the MULTILEADER is erased.</exception>
        public bool ExtendLeaderToText
        {
            get => Whole(Text(Read().After, 295), 0) != 0;

            set
            {
                if (value != ExtendLeaderToText)
                {
                    Item.File.Requires(1027, "a MULTILEADER's leaders run on to its words", "R2013 (AC1027)");
                    SetTopFlag(295, value, !value, -1);
                }
            }
        }

        /// <summary>
        /// Gets or sets the block the leaders point to (group 344, and 341 of the context data), as AutoCAD's
        /// <c>BlockContentId</c> gives its id; null where the MULTILEADER points to none, or to a block the drawing has none of.
        /// </summary>
        /// <value>
        /// Set, a block of the MULTILEADER's drawing: its handle written, the bit of the overrides set; a MULTILEADER that held no
        /// block is given one as ezdxf writes it, at the base point of its content, its words taken out and its content type the
        /// block. The values of the block's attributes are given by <see cref="SetBlockAttribute"/>.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the block set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block set is not of the MULTILEADER's drawing, or is model or paper space.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfBlockTableRecord BlockContent
        {
            get
            {
                Rec rec = Read();
                string handle = BlockText(rec, 341) ?? Text(rec.After, 344);
                return handle == null ? null : BlockByHandle(handle.Trim());
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (ReferenceEquals(value, BlockContent) && ContentType == DxfContentType.BlockContent)
                {
                    return;
                }

                if (value.Handle == null || BlockByHandle(value.Handle) != value || value.IsLayout)
                {
                    throw new ArgumentException("The content of a MULTILEADER is a block of its own drawing, not a layout.", nameof(value));
                }

                Rec rec = Read();
                Ctx ctx = Context(rec);

                if (!HasSection(ctx, 296, BlockCodes))
                {
                    DropSection(ctx, 290, MTextCodes);
                    MakeBlock(rec, ctx);
                    SetTop(rec, 172, "1", 10);
                }

                Section(ctx.Head, 296, BlockCodes, out int from, out int to);
                Put(ctx.Head, 341, value.Handle, ContextOrder, from, to);
                SetTop(rec, 344, value.Handle, 19);
                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets where the block is placed (group 15 of the block of the context data), as AutoCAD's <c>BlockPosition</c>,
        /// in the world; the base point of the content (10 of the context data) where the MULTILEADER holds no block. Set, the
        /// translation of the block's matrix (its groups 47) moves with it.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when another place is set on a MULTILEADER that holds no block, or one that is erased.</exception>
        public GeoPoint3 BlockPosition
        {
            get => ContentPoint(296, BlockCodes, 15);

            set
            {
                if (SamePoint(value, BlockPosition))
                {
                    return;
                }

                Finite(value, nameof(value));
                Rec rec = Read();
                BlockGroups(rec, out int from, out int to);
                GeoPoint3 was = PointOf(rec.Context.Head.GetRange(from, to - from), 15, value);
                PutPoint(rec.Context.Head, 15, value, ContextOrder, from, to);
                Shift(rec.Context.Head, value.X - was.X, value.Y - was.Y, value.Z - was.Z);
                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets how the block is scaled along its axes, as AutoCAD's <c>BlockScale</c>: as it is drawn, group 16 of the
        /// block of the context data, else the MULTILEADER's own (10); 1 each way where the record gives neither.
        /// </summary>
        /// <value>Set, written in both.</value>
        /// <exception cref="ArgumentException">Thrown when a scale set is not a finite number other than nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public GeoVector3 BlockScale
        {
            get
            {
                Rec rec = Read();
                GeoPoint3 own = PointOf(rec.After, 10, new GeoPoint3(1.0, 1.0, 1.0));
                return (BlockText(rec, 16) != null ? PointOf(BlockGroups(rec, out _, out _), 16, own) : own).ToVector();
            }

            set
            {
                if (!value.IsValid || value.X == 0.0 || value.Y == 0.0 || value.Z == 0.0)
                {
                    throw new ArgumentException("A block is scaled by finite numbers other than nought: " + value + " is not.", nameof(value));
                }

                var p = new GeoPoint3(value.X, value.Y, value.Z);

                if (SamePoint(p, Pt(BlockScale)))
                {
                    return;
                }

                Rec rec = Read();
                PutPoint(rec.After, 10, p, TopOrder);
                SetBit(rec, 21);
                Ctx ctx = Context(rec);
                Section(ctx.Head, 296, BlockCodes, out int from, out int to);

                if (from >= 0)
                {
                    PutPoint(ctx.Head, 16, p, ContextOrder, from, to);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets how far the block is turned, as AutoCAD's <c>BlockRotation</c>, in radians: as it is drawn, group 46 of
        /// the block of the context data, else the MULTILEADER's own (43); nought where the record gives neither.
        /// </summary>
        /// <value>Set, written in both.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double BlockRotation
        {
            get
            {
                Rec rec = Read();
                double own = Num(Text(rec.After, 43), 0.0);
                return Num(BlockText(rec, 46), own);
            }

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (Same(value, BlockRotation))
                {
                    return;
                }

                Rec rec = Read();
                PutNumber(rec.After, 43, value, 0.0, TopOrder);
                SetBit(rec, 22);
                Ctx ctx = Context(rec);
                Section(ctx.Head, 296, BlockCodes, out int from, out int to);

                if (from >= 0)
                {
                    PutNumber(ctx.Head, 46, value, 0.0, ContextOrder, from, to);
                }

                Write(rec);
            }
        }

        /// <summary>Gets or sets the colour of the block (group 93, and 93 of the context data), as AutoCAD's <c>BlockColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfColor BlockColor
        {
            get => DxfMLeaderColors.Of(Text(Read().After, 93) ?? ByBlock);
            set => SetColour(93, 93, 296, BlockCodes, value, 20);
        }

        /// <summary>Gets or sets where the leaders meet the block (group 176), as AutoCAD's <c>BlockConnectionType</c>; at its box where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfBlockConnectionType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfBlockConnectionType BlockConnectionType
        {
            get => (DxfBlockConnectionType)Whole(Text(Read().After, 176), 0);
            set => SetTopEnum(176, (int)value, (int)BlockConnectionType, 0, 1, 23);
        }

        /// <summary>
        /// Gets the value of an attribute of the block, as AutoCAD's <c>GetBlockAttribute</c>: an ATTRIB in no file of the tag of
        /// the attribute's definition and the value the MULTILEADER gives it (group 302 after the 330 of the definition's handle);
        /// the definition's own value where the MULTILEADER gives none.
        /// </summary>
        /// <param name="attDefId">The definition, an ATTDEF of the block.</param>
        /// <returns>The attribute.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        public DxfAttributeReference GetBlockAttribute(DxfAttributeDefinition attDefId)
        {
            if (attDefId == null)
            {
                throw new ArgumentNullException(nameof(attDefId));
            }

            List<KeyValuePair<int, string>> after = Read().After;
            int at = AttributeAt(after, attDefId.Handle);
            return new DxfAttributeReference
            {
                Tag = attDefId.Tag,
                TextString = at >= 0 ? Text(after, 302, at) ?? string.Empty : attDefId.TextString,
            };
        }

        /// <summary>
        /// Gives an attribute of the block a value, as AutoCAD's <c>SetBlockAttribute</c>: the 302 of the definition's entry
        /// written, or an entry made after the others (its handle 330, index 177, width 44 of 1 and value 302), as ezdxf writes
        /// one, from R2007.
        /// </summary>
        /// <param name="attDefId">The definition, an ATTDEF of the block, in the drawing.</param>
        /// <param name="attributeValue">The attribute whose value is taken, its <see cref="DxfDBText.TextString"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition or the attribute is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is in no file.</exception>
        /// <exception cref="InvalidOperationException">Thrown in a file older than R2007 (AC1021), or when the MULTILEADER is erased.</exception>
        public void SetBlockAttribute(DxfAttributeDefinition attDefId, DxfAttributeReference attributeValue)
        {
            if (attDefId == null)
            {
                throw new ArgumentNullException(nameof(attDefId));
            }

            if (attributeValue == null)
            {
                throw new ArgumentNullException(nameof(attributeValue));
            }

            if (attDefId.Handle == null)
            {
                throw new ArgumentException("The definition of an attribute is named by its handle, and this one is in no file.", nameof(attDefId));
            }

            string value = attributeValue.TextString;

            if (string.Equals(value, GetBlockAttribute(attDefId).TextString, StringComparison.Ordinal) && AttributeAt(Read().After, attDefId.Handle) >= 0)
            {
                return;
            }

            Item.File.Requires(1021, "the attributes of a MULTILEADER's block", "R2007 (AC1021)");
            Rec rec = Read();
            int at = AttributeAt(rec.After, attDefId.Handle);

            if (at >= 0)
            {
                int text = rec.After.FindIndex(at, g => g.Key == 302);
                rec.After[text] = G(302, value);
            }
            else
            {
                int count = 0;
                int last = -1;

                for (int i = 0; i < rec.After.Count; i++)
                {
                    if (rec.After[i].Key == 330)
                    {
                        count++;
                    }

                    if (rec.After[i].Key == 330 || rec.After[i].Key == 177 || rec.After[i].Key == 44 || rec.After[i].Key == 302 || rec.After[i].Key == 345 || rec.After[i].Key == 94)
                    {
                        last = i;
                    }
                }

                int put = last >= 0 ? last + 1 : Math.Max(0, rec.After.FindIndex(g => g.Key == 294));
                put = last < 0 && rec.After.FindIndex(g => g.Key == 294) < 0 ? rec.After.Count : put;
                rec.After.InsertRange(put, new[] { G(330, attDefId.Handle), G(177, count), G(44, "1.0"), G(302, value) });
            }

            Write(rec);
        }

        #endregion

        #region The arrows, landing and dogleg

        /// <summary>
        /// Gets or sets the size of the arrows (group 42, and 140 of the context data, the same, as ezdxf writes and moves them),
        /// as AutoCAD's <c>ArrowSize</c>; 4 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double ArrowSize
        {
            get => Num(Text(Read().After, 42), 4.0);

            set
            {
                if (Same(value, ArrowSize))
                {
                    return;
                }

                Guard.NonNegative(value, nameof(value), "An arrow's size has to be a finite number, nought or more.");
                Rec rec = Read();
                PutNumber(rec.After, 42, value, 4.0, TopOrder);
                SetBit(rec, 9);
                Ctx ctx = Context(rec);
                PutNumber(ctx.Head, 140, value, 4.0, ContextOrder);
                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets the block drawn as the arrows (group 342), as AutoCAD's <c>ArrowSymbolId</c> gives its id; null for
        /// AutoCAD's closed filled arrow, which needs no block.
        /// </summary>
        /// <value>Set, a block of the MULTILEADER's drawing, its handle written; null takes the group out.</value>
        /// <exception cref="ArgumentException">Thrown when the block set is not of the MULTILEADER's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfBlockTableRecord ArrowSymbol
        {
            get
            {
                string handle = Text(Read().After, 342);
                return handle == null ? null : BlockByHandle(handle.Trim());
            }

            set
            {
                if (ReferenceEquals(value, ArrowSymbol))
                {
                    return;
                }

                if (value != null && (value.Handle == null || BlockByHandle(value.Handle) != value))
                {
                    throw new ArgumentException("The arrow of a MULTILEADER is a block of its own drawing.", nameof(value));
                }

                Rec rec = Read();
                SetTop(rec, 342, value?.Handle, 8);
                Write(rec);
            }
        }

        /// <summary>Gets or sets whether the leaders end in a landing, a dogleg and gap (group 290), as AutoCAD's <c>EnableLanding</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public bool EnableLanding
        {
            get => Whole(Text(Read().After, 290), 1) != 0;
            set => SetTopFlag(290, value, EnableLanding, 4);
        }

        /// <summary>Gets or sets whether the leaders have doglegs (group 291), as AutoCAD's <c>EnableDogleg</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public bool EnableDogleg
        {
            get => Whole(Text(Read().After, 291), 1) != 0;
            set => SetTopFlag(291, value, EnableDogleg, 6);
        }

        /// <summary>
        /// Gets or sets how long the doglegs are (group 41), as AutoCAD's <c>DoglegLength</c>; 8 where the record gives none.
        /// </summary>
        /// <value>Set, written as the MULTILEADER's and as each leader's (40), the bit of the overrides set.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double DoglegLength
        {
            get => Num(Text(Read().After, 41), 8.0);

            set
            {
                if (Same(value, DoglegLength))
                {
                    return;
                }

                Guard.NonNegative(value, nameof(value), "A dogleg's length has to be a finite number, nought or more.");
                Rec rec = Read();
                PutNumber(rec.After, 41, value, 8.0, TopOrder);
                SetBit(rec, 7);

                foreach (LeaderPart leader in Context(rec).Leaders)
                {
                    PutNumber(leader.Head, 40, value, 8.0, LeaderOrder);
                }

                Write(rec);
            }
        }

        /// <summary>Gets or sets the gap between the landing and the words (group 145 of the context data), as AutoCAD's <c>LandingGap</c>; 2 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double LandingGap
        {
            get => Num(Text(Context(Read()).Head, 145), 2.0);
            set => SetContextNumber(145, value, 2.0, 5, false);
        }

        /// <summary>Gets the direction of a leader's dogleg (groups 11, 21 and 31 of the leader), as AutoCAD's <c>GetDogleg</c>, in the world.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        public GeoVector3 GetDogleg(int leaderIndex) => PointOf(FindLeader(Read(), leaderIndex).Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();

        /// <summary>Gives a leader's dogleg its direction, as AutoCAD's <c>SetDogleg</c>.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <param name="vector">The direction, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when the direction has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetDogleg(int leaderIndex, GeoVector3 vector)
        {
            if (!vector.IsValid || !(vector.Length > 0.0))
            {
                throw new ArgumentException("A dogleg runs along a direction of some length: " + vector + " is not.", nameof(vector));
            }

            Rec rec = Read();
            PutPoint(FindLeader(rec, leaderIndex).Head, 11, new GeoPoint3(vector.X, vector.Y, vector.Z), LeaderOrder);
            Write(rec);
        }

        /// <summary>Gets how long a leader's dogleg is (group 40 of the leader), as AutoCAD's <c>GetDoglegLength</c>; 1 where the leader gives none, as ezdxf reads one.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        public double GetDoglegLength(int leaderIndex) => Num(Text(FindLeader(Read(), leaderIndex).Head, 40), 1.0);

        /// <summary>Gives a leader's dogleg its length, as AutoCAD's <c>SetDoglegLength</c>.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <param name="doglegLength">The length, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index, or the length is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetDoglegLength(int leaderIndex, double doglegLength)
        {
            Guard.NonNegative(doglegLength, nameof(doglegLength), "A dogleg's length has to be a finite number, nought or more.");
            Rec rec = Read();
            PutNumber(FindLeader(rec, leaderIndex).Head, 40, doglegLength, double.NaN, LeaderOrder);
            Write(rec);
        }

        #endregion

        #region The leader lines' look

        /// <summary>Gets or sets the colour of the leader lines (group 91), as AutoCAD's <c>LeaderLineColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfColor LeaderLineColor
        {
            get => DxfMLeaderColors.Of(Text(Read().After, 91) ?? ByBlock);
            set => SetColour(91, -1, 0, null, value, 1);
        }

        /// <summary>Gets or sets how the leader lines are drawn (group 170), as AutoCAD's <c>LeaderLineType</c>; straight where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfLeaderType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfLeaderType LeaderLineType
        {
            get => (DxfLeaderType)Whole(Text(Read().After, 170), 1);
            set => SetTopEnum(170, (int)value, (int)LeaderLineType, 0, 2, 0);
        }

        /// <summary>Gets or sets the lineweight of the leader lines (group 171), as AutoCAD's <c>LeaderLineWeight</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfLineWeight LeaderLineWeight
        {
            get => (DxfLineWeight)Whole(Text(Read().After, 171), -2);
            set => SetTopEnum(171, (int)value, (int)LeaderLineWeight, -3, 211, 3);
        }

        #endregion

        #region The style, scale and plane

        /// <summary>
        /// Gets or sets the multileader style (group 340), as AutoCAD's <c>MLeaderStyle</c> gives its id: by its name in the
        /// drawing's dictionary ACAD_MLEADERSTYLE; null where the handle names none of it.
        /// </summary>
        /// <value>Set, the name of a style of ACAD_MLEADERSTYLE, letters of either case the same: its handle written.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no multileader style of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased, or in no drawing.</exception>
        public string MLeaderStyle
        {
            get
            {
                string handle = Text(Read().After, 340)?.Trim();

                foreach (DxfDBDictionaryEntry entry in Styles())
                {
                    if (handle != null && string.Equals(entry.Value?.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Key;
                    }
                }

                return null;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, MLeaderStyle, StringComparison.Ordinal))
                {
                    return;
                }

                foreach (DxfDBDictionaryEntry entry in Styles())
                {
                    if (string.Equals(entry.Key, value.Trim(), StringComparison.OrdinalIgnoreCase) && entry.Value?.Handle != null)
                    {
                        Rec rec = Read();
                        SetTop(rec, 340, entry.Value.Handle, -1);
                        Write(rec);
                        return;
                    }
                }

                throw new ArgumentException("The drawing has no multileader style named " + value + ".", nameof(value));
            }
        }

        /// <summary>
        /// Gets or sets the scale of the MULTILEADER (group 45 from R2007, and 40 of the context data), as AutoCAD's <c>Scale</c>;
        /// 1 where the record gives none.
        /// </summary>
        /// <value>Set, a finite number above nought: the height of the words, the size of the arrows and the landing gap of the
        /// context data scaled by the new over the old, as ezdxf's <c>set_scale</c> scales them, and past it the length of the
        /// doglegs, the MULTILEADER's (41) and each leader's as drawn (40), which ezdxf leaves, so that they keep their size
        /// against the words.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double Scale
        {
            get => Num(Text(Context(Read()).Head, 40), 1.0);

            set
            {
                if (Same(value, Scale))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "A scale has to be a finite number above nought.");
                Rec rec = Read();
                Ctx ctx = Context(rec);
                double old = Num(Text(ctx.Head, 40), 1.0);
                double factor = old > 0.0 ? value / old : 1.0;
                PutNumber(ctx.Head, 40, value, 1.0, ContextOrder);

                foreach (int code in new[] { 41, 140, 145 })
                {
                    if (Text(ctx.Head, code) != null)
                    {
                        PutNumber(ctx.Head, code, Num(Text(ctx.Head, code), 0.0) * factor, 0.0, ContextOrder);
                    }
                }

                if (Text(rec.After, 41) != null)
                {
                    PutNumber(rec.After, 41, Num(Text(rec.After, 41), 8.0) * factor, 8.0, TopOrder);
                }

                foreach (LeaderPart leader in ctx.Leaders)
                {
                    if (Text(leader.Head, 40) != null)
                    {
                        PutNumber(leader.Head, 40, Num(Text(leader.Head, 40), 1.0) * factor, 1.0, LeaderOrder);
                    }
                }

                if (Text(rec.After, 45) != null || Item.File.Supports(1021))
                {
                    PutNumber(rec.After, 45, value, 1.0, TopOrder);
                }

                SetBit(rec, 24);
                Write(rec);
            }
        }

        /// <summary>Gets or sets whether the MULTILEADER is scaled by the annotation scale (group 293), as AutoCAD's <c>EnableAnnotationScale</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public bool EnableAnnotationScale
        {
            get => Whole(Text(Read().After, 293), 0) != 0;
            set => SetTopFlag(293, value, EnableAnnotationScale, -1);
        }

        /// <summary>
        /// Gets the normal of the plane of the MULTILEADER, as AutoCAD's <c>Normal</c>: the x axis of its context's plane (111)
        /// crossed with its y axis (112), of unit length, turned over where the context says so (297).
        /// </summary>
        public GeoVector3 Normal
        {
            get
            {
                List<KeyValuePair<int, string>> head = Context(Read()).Head;
                GeoVector3 x = PointOf(head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                GeoVector3 y = PointOf(head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector();
                GeoVector3 z = DxfOcsTransform.Cross(x, y);
                double length = z.Length;
                z = length > 0.0 ? z.Multiply(1.0 / length) : new GeoVector3(0.0, 0.0, 1.0);
                return Whole(Text(head, 297), 0) != 0 ? z.Multiply(-1.0) : z;
            }
        }

        /// <summary>Gets the plane of the MULTILEADER, as AutoCAD's <c>GetPlane</c>: through the origin of its context's plane (110), of its <see cref="Normal"/>.</summary>
        /// <returns>The plane.</returns>
        public override GeoPlane3 GetPlane() => new GeoPlane3(PointOf(Context(Read()).Head, 110, new GeoPoint3(0.0, 0.0, 0.0)), Normal);

        /// <summary>
        /// Gets the box the MULTILEADER's points fill, as AutoCAD's <c>GeometricExtents</c> reaches them: the vertices of every
        /// leader line (the point its dogleg starts at among them) and the point of its words or block; the size of the words and
        /// of the block, and the arrows, which AutoCAD works out when it draws them, not counted.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER has no line and no content, as AutoCAD throws
        /// for an entity with no extents.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                var points = new List<GeoPoint3>();

                foreach (int leader in GetLeaderIndexes())
                {
                    foreach (int line in GetLeaderLineIndexes(leader))
                    {
                        for (int i = 0; i < VerticesCount(line); i++)
                        {
                            points.Add(GetVertex(line, i));
                        }
                    }
                }

                if (ContentType == DxfContentType.MTextContent)
                {
                    points.Add(TextLocation);
                }
                else if (ContentType == DxfContentType.BlockContent)
                {
                    points.Add(BlockPosition);
                }

                if (points.Count == 0)
                {
                    throw new InvalidOperationException("The MULTILEADER " + (Handle ?? string.Empty) + " has no line and no content: it has no extents.");
                }

                double[] box = { points[0].X, points[0].Y, points[0].Z, points[0].X, points[0].Y, points[0].Z };

                foreach (GeoPoint3 p in points)
                {
                    box[0] = Math.Min(box[0], p.X);
                    box[1] = Math.Min(box[1], p.Y);
                    box[2] = Math.Min(box[2], p.Z);
                    box[3] = Math.Max(box[3], p.X);
                    box[4] = Math.Max(box[4], p.Y);
                    box[5] = Math.Max(box[5], p.Z);
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        #endregion

        #region The leaders and their lines

        /// <summary>Gets how many leaders the MULTILEADER has, as AutoCAD's <c>LeaderCount</c>.</summary>
        public int LeaderCount => Read().Context?.Leaders.Count ?? 0;

        /// <summary>Gets how many leader lines the MULTILEADER has, those of every leader, as AutoCAD's <c>LeaderLineCount</c>.</summary>
        public int LeaderLineCount
        {
            get
            {
                int count = 0;

                foreach (LeaderPart leader in Read().Context?.Leaders ?? new List<LeaderPart>())
                {
                    count += leader.Lines.Count;
                }

                return count;
            }
        }

        /// <summary>Gets the indices of the leaders (90 of each), in their order, as AutoCAD's <c>GetLeaderIndexes</c>.</summary>
        /// <returns>The indices.</returns>
        public IReadOnlyList<int> GetLeaderIndexes() => (Read().Context?.Leaders ?? new List<LeaderPart>()).ConvertAll(l => l.Index).AsReadOnly();

        /// <summary>Gets the indices of the lines of a leader (91 of each), in their order, as AutoCAD's <c>GetLeaderLineIndexes</c>.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The indices.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        public IReadOnlyList<int> GetLeaderLineIndexes(int leaderIndex) => FindLeader(Read(), leaderIndex).Lines.ConvertAll(l => l.Index).AsReadOnly();

        /// <summary>Gets the index of the leader a line is of, as AutoCAD's <c>GetLeaderIndex</c>.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The leader's index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public int GetLeaderIndex(int leaderLineIndex)
        {
            FindLine(Read(), leaderLineIndex, out LeaderPart leader);
            return leader.Index;
        }

        /// <summary>
        /// Gets how many vertices a leader line has, as AutoCAD's <c>VerticesCount</c>: its own (10 of the line), then the point
        /// of its leader the dogleg starts at (10 of the leader), which every line of the leader ends at.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public int VerticesCount(int leaderLineIndex) => Vertices(Read(), leaderLineIndex, out _, out _).Count;

        /// <summary>Gets a vertex of a leader line, as AutoCAD's <c>GetVertex</c>, in the world: the last the point of its leader the dogleg starts at.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no line or vertex of the index.</exception>
        public GeoPoint3 GetVertex(int leaderLineIndex, int index)
        {
            List<GeoPoint3> vertices = Vertices(Read(), leaderLineIndex, out _, out _);
            VertexIndex(vertices, index, nameof(index));
            return vertices[index];
        }

        /// <summary>Gets the first vertex of a leader line, where its arrow is, as AutoCAD's <c>GetFirstVertex</c>.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public GeoPoint3 GetFirstVertex(int leaderLineIndex) => GetVertex(leaderLineIndex, 0);

        /// <summary>Gets the last vertex of a leader line, the point of its leader the dogleg starts at, as AutoCAD's <c>GetLastVertex</c>.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public GeoPoint3 GetLastVertex(int leaderLineIndex) => GetVertex(leaderLineIndex, VerticesCount(leaderLineIndex) - 1);

        /// <summary>
        /// Moves a vertex of a leader line, as AutoCAD's <c>SetVertex</c>: one of its own; the last, the point of its leader the
        /// dogleg starts at, which moves the end of every line of the leader with it.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no line or vertex of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetVertex(int leaderLineIndex, int index, GeoPoint3 point)
        {
            Finite(point, nameof(point));
            Rec rec = Read();
            List<GeoPoint3> vertices = Vertices(rec, leaderLineIndex, out LinePart line, out LeaderPart leader);
            VertexIndex(vertices, index, nameof(index));
            vertices[index] = point;
            WriteVertices(rec, line, leader, vertices);
        }

        /// <summary>Moves the first vertex of a leader line, as AutoCAD's <c>SetFirstVertex</c>, as <see cref="SetVertex"/> moves one.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetFirstVertex(int leaderLineIndex, GeoPoint3 point) => SetVertex(leaderLineIndex, 0, point);

        /// <summary>Moves the last vertex of a leader line, the point of its leader the dogleg starts at, as AutoCAD's <c>SetLastVertex</c>.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetLastVertex(int leaderLineIndex, GeoPoint3 point) => SetVertex(leaderLineIndex, VerticesCount(leaderLineIndex) - 1, point);

        /// <summary>Puts a vertex in before the first of a leader line, where its arrow goes, as AutoCAD's <c>AddFirstVertex</c>.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">The vertex, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void AddFirstVertex(int leaderLineIndex, GeoPoint3 point)
        {
            Finite(point, nameof(point));
            Rec rec = Read();
            List<GeoPoint3> vertices = Vertices(rec, leaderLineIndex, out LinePart line, out LeaderPart leader);
            vertices.Insert(0, point);
            WriteVertices(rec, line, leader, vertices);
        }

        /// <summary>
        /// Adds a vertex after the last of a leader line, as AutoCAD's <c>AddLastVertex</c>: it becomes the point of the leader
        /// the dogleg starts at, the old one a vertex of the line, and every line of the leader ends at it.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">The vertex, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void AddLastVertex(int leaderLineIndex, GeoPoint3 point)
        {
            Finite(point, nameof(point));
            Rec rec = Read();
            List<GeoPoint3> vertices = Vertices(rec, leaderLineIndex, out LinePart line, out LeaderPart leader);
            vertices.Add(point);
            WriteVertices(rec, line, leader, vertices);
        }

        /// <summary>Takes the first vertex of a leader line out, as AutoCAD's <c>RemoveFirstVertex</c>; a line keeps two vertices.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has two vertices or fewer, or the MULTILEADER is erased.</exception>
        public void RemoveFirstVertex(int leaderLineIndex) => RemoveVertex(leaderLineIndex, true);

        /// <summary>
        /// Takes the last vertex of a leader line out, as AutoCAD's <c>RemoveLastVertex</c>: the vertex before it becomes the point
        /// of the leader the dogleg starts at; a line keeps two vertices.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has two vertices or fewer, or the MULTILEADER is erased.</exception>
        public void RemoveLastVertex(int leaderLineIndex) => RemoveVertex(leaderLineIndex, false);

        /// <summary>
        /// Adds a leader of no lines, as AutoCAD's <c>AddLeader</c>: its index (90) one above every leader's, its dogleg starting
        /// at the base point of the content (10 of the context data) along the x axis of the plane, as long as
        /// <see cref="DoglegLength"/>, written as ezdxf writes a leader.
        /// </summary>
        /// <returns>The index of the leader.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public int AddLeader()
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            int index = 0;

            foreach (LeaderPart other in ctx.Leaders)
            {
                index = Math.Max(index, other.Index + 1);
            }

            GeoPoint3 start = PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
            GeoPoint3 along = PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0));
            var head = new List<KeyValuePair<int, string>> { G(290, "1"), G(291, "1") };
            head.AddRange(PointGroups(10, start));
            head.AddRange(PointGroups(11, along));
            head.Add(G(90, index));
            head.Add(G(40, DoglegLength));
            var tail = new List<KeyValuePair<int, string>>();

            if (Item.File.Supports(1024))
            {
                tail.Add(G(271, (int)TextAttachmentDirection));
            }

            ctx.Leaders.Add(LeaderPart.Made(head, tail));
            Write(rec);
            return index;
        }

        /// <summary>
        /// Adds a leader line of no vertices to a leader, as AutoCAD's <c>AddLeaderLine</c> of a leader: its index (91) one above
        /// every line's, its colour BYBLOCK, written as ezdxf writes one. Its arrow goes at a vertex put in by
        /// <see cref="AddFirstVertex"/>.
        /// </summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The index of the line.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public int AddLeaderLine(int leaderIndex)
        {
            Rec rec = Read();
            LeaderPart leader = FindLeader(rec, leaderIndex);
            int index = NextLine(rec);
            leader.Lines.Add(LinePart.Made(new[] { G(91, index), G(92, ByBlock) }));
            Write(rec);
            return index;
        }

        /// <summary>
        /// Adds a leader line from a point, as AutoCAD's <c>AddLeaderLine</c> of a point: the point its first vertex, where its
        /// arrow is, the line added to the first leader whose dogleg runs from the point's side towards the content, the first
        /// leader where none does, a new leader where there is none.
        /// </summary>
        /// <param name="point">The point, in the world.</param>
        /// <returns>The index of the line.</returns>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public int AddLeaderLine(GeoPoint3 point)
        {
            Finite(point, nameof(point));

            if (LeaderCount == 0)
            {
                AddLeader();
            }

            Rec rec = Read();
            List<LeaderPart> leaders = Context(rec).Leaders;
            LeaderPart chosen = leaders[0];

            foreach (LeaderPart leader in leaders)
            {
                GeoVector3 dogleg = PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                GeoPoint3 start = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));

                if (dogleg.DotProduct(point.GetVectorTo(start)) > 0.0)
                {
                    chosen = leader;
                    break;
                }
            }

            int index = NextLine(rec);
            var line = LinePart.Made(new[] { G(91, index), G(92, ByBlock) });
            line.SetVertices(new List<GeoPoint3> { point });
            chosen.Lines.Add(line);
            Write(rec);
            return index;
        }

        /// <summary>Takes a leader out with its lines, as AutoCAD's <c>RemoveLeader</c>; the arrows of its lines (94 and 345) with them.</summary>
        /// <param name="index">The index of the leader.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void RemoveLeader(int index)
        {
            Rec rec = Read();
            LeaderPart leader = FindLeader(rec, index);

            foreach (LinePart line in leader.Lines)
            {
                DropArrow(rec, line.Index);
            }

            Context(rec).Leaders.Remove(leader);
            Write(rec);
        }

        /// <summary>Takes a leader line out, as AutoCAD's <c>RemoveLeaderLine</c>; its arrow (94 and 345) with it, its leader kept.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void RemoveLeaderLine(int leaderLineIndex)
        {
            Rec rec = Read();
            LinePart line = FindLine(rec, leaderLineIndex, out LeaderPart leader);
            leader.Lines.Remove(line);
            DropArrow(rec, leaderLineIndex);
            Write(rec);
        }

        #endregion

        #region Moving

        /// <summary>
        /// Moves the MULTILEADER or a part of it along a vector, as AutoCAD's <c>MoveMLeader</c>: everything, as
        /// <see cref="DxfEntity.TransformBy"/> by the move does; everything but the first vertex of each leader line, where its arrow
        /// is; or the content and the doglegs, the points they start at with them, the leader lines' own vertices staying.
        /// </summary>
        /// <param name="vec">The vector, in the world.</param>
        /// <param name="type">What moves.</param>
        /// <exception cref="ArgumentException">Thrown when the vector is not finite.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfMoveType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void MoveMLeader(GeoVector3 vec, DxfMoveType type)
        {
            if (!vec.IsValid)
            {
                throw new ArgumentException("A move is along a vector of finite numbers: " + vec + " is not.", nameof(vec));
            }

            if (type == DxfMoveType.MoveAllPoints)
            {
                TransformBy(GeoTransform3.Translation(vec));
                return;
            }

            if (type != DxfMoveType.MoveAllExceptArrowHeaderPoints && type != DxfMoveType.MoveContentAndDoglegPoints)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "A move is of everything, all but the arrows, or the content and doglegs, 0 to 2.");
            }

            Rec rec = Read();
            Ctx ctx = Context(rec);
            GeoPoint3 Moved(GeoPoint3 p) => new GeoPoint3(p.X + vec.X, p.Y + vec.Y, p.Z + vec.Z);

            foreach (int code in new[] { 10, 110 })
            {
                MovePoint(ctx.Head, code, Moved, ContextOrder, 0, -1);
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                MovePoint(ctx.Head, 12, Moved, ContextOrder, from, to);
            }

            Section(ctx.Head, 296, BlockCodes, out from, out to);

            if (from >= 0)
            {
                MovePoint(ctx.Head, 15, Moved, ContextOrder, from, to);
                Shift(ctx.Head, vec.X, vec.Y, vec.Z);
            }

            foreach (LeaderPart leader in ctx.Leaders)
            {
                MovePoint(leader.Head, 10, Moved, LeaderOrder, 0, -1);

                foreach (LinePart line in leader.Lines)
                {
                    if (type == DxfMoveType.MoveAllExceptArrowHeaderPoints)
                    {
                        List<GeoPoint3> vertices = line.Vertices();

                        for (int i = 1; i < vertices.Count; i++)
                        {
                            vertices[i] = Moved(vertices[i]);
                        }

                        line.SetVertices(vertices);
                    }
                }
            }

            Write(rec);
        }

        #endregion
    }
}
