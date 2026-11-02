using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
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
    /// the value it has sets its bit all the same and changes nothing else, as AutoCAD 2021 sets it (<see cref="ArrowSize"/>
    /// is written anew too, and a dogleg's length lands the leaders anew, <see cref="DoglegLength"/>); the colour of the words, the content, the block, the direction of the attachment, the annotation
    /// scale, the places and the style change nothing. The sizes (<see cref="TextHeight"/>, <see cref="LandingGap"/>,
    /// <see cref="ArrowSize"/>, <see cref="DoglegLength"/> and <see cref="BlockScale"/>) are read and set unscaled, as AutoCAD
    /// 2021's API reads and sets them, and drawn times the <see cref="Scale"/>. What AutoCAD works out
    /// when it draws the MULTILEADER (where the leaders meet the words, the box of the words, the breaks) is not worked out
    /// here: the library writes the data AutoCAD draws it from. Where AutoCAD 2021 moves the words or block, or lands a leader
    /// anew, when a property is set, so that each leader still meets the content (the gap, the doglegs, the height, the scale,
    /// an attachment, the words given anew, a leader's last vertex), the library moves them as AutoCAD does where that needs no
    /// font: words met on their left, by AutoCAD's rules of the lines of words, and a move of words met on their right that
    /// keeps them as wide; what AutoCAD lands as far right as its font makes the words wide is left as it was.
    /// </para>
    /// <para>
    /// <see cref="DxfEntity.GetGeometry"/> gives the lines of its leaders, as <see cref="DxfReader"/> reads a MULTILEADER, so
    /// that <see cref="DxfEntity.IntersectWith"/> meets it on them: for each leader line a <see cref="GeoPolyline3"/> from its
    /// first vertex, where its arrow points, through its vertices and the last point of its leader to the end of the dogleg, a
    /// vertex on the one before it left out, along the path <see cref="Explode"/> draws with the line and its dogleg; splined,
    /// a <see cref="GeoSpline3"/> fitted through those points, leaving along the first segment and reaching along the dogleg.
    /// Both start at the arrow's point, where <see cref="Explode"/> starts its line at the back of the arrow, and the spline
    /// of a line splined of its own runs on through the dogleg, where the spline exploded ends at the dogleg's start, so that
    /// the two part near the arrow and near the words; a multileader whose leader lines are all splined has no dogleg drawn,
    /// and its splines run to the end the dogleg would have, exploded too. The line above is met by the line y = -3 from (780, -3, 0)
    /// to (795, -3, 0) at (788, -3, 0), on its way up to the dogleg. Its words or block, its arrowheads and the lines AutoCAD
    /// draws under the words are not among them, and invisible lines give none.
    /// </para>
    /// </remarks>
    public sealed partial class DxfMLeader : DxfEntity
    {
        private const string ByBlock = "-1056964608";

        // The text style the words given to a MULTILEADER in no drawing name, which it takes when it is added to one.
        private string wordsTextStyle;

        /// <summary>
        /// Initializes a MULTILEADER of no content and no leaders, its plane the floor, as AutoCAD's <c>new MLeader()</c>, on layer
        /// 0 in no file: added to a drawing, it takes the drawing's multileader style (CMLEADERSTYLE, else Standard), with the
        /// style's linetype of the leader lines (341) and its arrow block (342) where it names them, as AutoCAD 2021 writes them,
        /// and the text style Standard. Give it leaders with <see cref="AddLeaderLine(GeoPoint3)"/> and words with <see cref="MText"/>.
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
        /// its words (304), place (12), direction (13), height (the context's 41), width (43), spacing of its lines (45 and 170),
        /// the point it is held by (171, at the top on the left, middle or right) and, in a drawing, its text style (340), by name;
        /// null where the MULTILEADER holds no words.
        /// </summary>
        /// <value>
        /// Set, an MTEXT whose words, place, direction, height, width, spacing and point are written into the context data, the
        /// height's bit of the overrides set; a MULTILEADER that held no words is given an MTEXT as ezdxf writes one, its block
        /// taken out with the handle of it (344), its content type the words and the base point of its content (10 of the context
        /// data) their place. Null takes the words out, as <see cref="ContentType"/> set to none does. Words given anew where
        /// leaders meet them on a side keep their landing, as AutoCAD 2021 keeps it, the place given not taken: met on their left
        /// only, they stay where they were, moved up by as much more as the left is met below their top, three lines met at their
        /// middle given at (120, 80, 0) for "ABC" at (100, 50, 0) standing at (100, 56.67, 0); met on their right, they stay where
        /// they were where they are as wide as before, and take the place given where AutoCAD would land them by their font. A
        /// leader meeting them on a side of attachment 8 underlines them (<c>\L</c>, <see cref="TextAttachmentType"/>). Given to a
        /// MULTILEADER in a drawing, the words' text style (<see cref="DxfMText.TextStyleName"/>, Standard where they name none)
        /// is the MULTILEADER's (343, and the words' 340), as AutoCAD 2021 takes it, with the bit of the overrides of the text
        /// style (11) where it was not the MULTILEADER's already: words of TS2 on a multileader of Standard's words make it of
        /// TS2, and a style set later leaves it so. Words given to a MULTILEADER in no drawing keep the text style they name when
        /// it is added to a drawing that has one of the name, with the bit where it is not the style's, as AutoCAD 2021 keeps the
        /// words of a multileader made in memory: words of TS2 make it of TS2; words that name none take its style's text style.
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
                // The words as the record writes them, carets and all.
                var mtext = new DxfMText();
                mtext.SetWritten(Text(m, 304) ?? string.Empty);
                mtext.Location = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));
                mtext.TextHeight = Positive(Num(Text(ctx.Head, 41), 4.0), 4.0);
                mtext.Width = Math.Max(0.0, Num(Text(m, 43), 0.0));
                mtext.Attachment = (DxfAttachmentPoint)Math.Max(1, Math.Min(3, Whole(Text(m, 171), 1)));

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
                string textStyle = Text(m, 340)?.Trim();
                DxfDatabase database = Database();

                if (textStyle != null && database != null && database.TryGetObject(textStyle, out DxfDBObject found) && found is DxfTextStyleTableRecord record)
                {
                    mtext.TextStyleName = record.Name;
                }
                else if (database == null && wordsTextStyle != null)
                {
                    mtext.TextStyleName = wordsTextStyle;
                }

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

                bool had = HasSection(ctx, 290, MTextCodes);

                if (!had)
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

                // The words as they were, where AutoCAD 2021 keeps them landed.
                GeoPoint3 wasAt = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));
                string wasLook = WordsLook(ctx);
                double wasBelow = Below(ctx, AttachmentOf(rec, true), Height(ctx));
                bool lined = AllLined(rec, ctx);
                Put(m, 304, value.Written, ContextOrder);
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

                // The words' text style given to the MULTILEADER, as AutoCAD 2021 takes it (t-mtext-ts2, t-ts2-current); kept for
                // when it is added where it is in no drawing (f6-def-words-ts2).
                DxfDatabase database = Database();
                wordsTextStyle = database == null ? value.NamedTextStyle : null;

                if (database != null && database.TextStyleTable.Has(value.TextStyleName))
                {
                    string textStyle = database.TextStyleTable[value.TextStyleName].Handle;
                    Section(ctx.Head, 290, MTextCodes, out from, out to);
                    Put(ctx.Head, 340, textStyle, ContextOrder, from, to);

                    if (!string.Equals(Text(rec.After, 343)?.Trim(), textStyle, StringComparison.OrdinalIgnoreCase))
                    {
                        Put(rec.After, 343, textStyle, TopOrder);
                        SetBit(rec, 11);
                    }
                }

                Underline(rec, ctx, lined);

                if (had)
                {
                    Relocated(rec, ctx, wasAt, wasLook, wasBelow);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets where the words are held (group 12 of the context's MTEXT), as AutoCAD's <c>TextLocation</c>, in the
        /// world; the base point of the content (10 of the context data) where the MULTILEADER holds no words.
        /// </summary>
        /// <value>Set, the words moved there, and the base point of the content and the point each leader's dogleg starts at
        /// (10 of the context data and of each leader) moved with them, as AutoCAD 2021 moves them: words moved from
        /// (100, 50, 0) to (120, 80, 0) move a dogleg from (90, 48, 0) to (110, 78, 0), the leader lines' own vertices
        /// staying.</value>
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

                GeoPoint3 was = PointOf(ctx.Head.GetRange(from, to - from), 12, new GeoPoint3(0.0, 0.0, 0.0));
                PutPoint(ctx.Head, 12, value, ContextOrder, from, to);

                // The landings and the base point move with the words, as AutoCAD 2021 moves them.
                GeoPoint3 Moved(GeoPoint3 p) => new GeoPoint3(p.X + (value.X - was.X), p.Y + (value.Y - was.Y), p.Z + (value.Z - was.Z));
                MovePoint(ctx.Head, 10, Moved, ContextOrder, 0, -1);

                foreach (LeaderPart leader in ctx.Leaders)
                {
                    MovePoint(leader.Head, 10, Moved, LeaderOrder, 0, -1);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets how high the letters are, as AutoCAD's <c>TextHeight</c>: unscaled, as AutoCAD 2021's API reads it, the
        /// height drawn (group 41 of the context data) over the <see cref="Scale"/>; 4 where the record gives none. Words drawn 8
        /// high at a scale of 2 read 4, and <see cref="MText"/> gives them 8 high, as drawn.
        /// </summary>
        /// <value>Set, written times the <see cref="Scale"/>, as AutoCAD 2021 writes it: 5 at a scale of 2 gives 41 of 10. Words
        /// met by leaders on their left only are moved to keep those landings, as AutoCAD 2021 moves them: up by as much more as
        /// the left is met below their top, words "ABC" at (100, 50, 0) met at the middle of their top line moving to (100, 53, 0)
        /// for a height of 10 from 4. Words met on their right are left where they are, which AutoCAD lands by their font's
        /// width.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double TextHeight
        {
            get => Unscaled(Context(Read()), 41, 4.0);
            set => SetContextNumber(41, value, 4.0, 16, true, Reheighted);
        }

        /// <summary>Gets or sets the colour of the words (group 92, and 90 of the context's MTEXT), as AutoCAD's <c>TextColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfColor TextColor
        {
            get => DxfMLeaderColors.Of(Text(Read().After, 92) ?? ByBlock);
            set => SetColour(92, 90, 290, MTextCodes, value, 15, false);
        }

        /// <summary>
        /// Gets or sets where the leaders on the left meet the words (group 173, and 174 of the context data), as AutoCAD's
        /// <c>TextAttachmentType</c>; the right (95, and 175 of the context data) is left as it is, as AutoCAD 2021 leaves it.
        /// </summary>
        /// <value>Set, as <see cref="SetTextAttachmentType"/> sets the left: three lines met on both sides at the middle of their
        /// top line, set to the bottom of the bottom line, stand at (100, 65.33, 0) for (100, 50, 0), the right kept at the middle
        /// of the top line and its dogleg moved with the words.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfTextAttachmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfTextAttachmentType TextAttachmentType
        {
            get => GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader);
            set => SetTextAttachmentType(value, DxfLeaderDirectionType.LeftLeader);
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
        /// the side and that of its context data, and the side's bit of the overrides. Where leaders meet the words on that side,
        /// the words or the landings move as AutoCAD 2021 moves them, the words running along the plane: the left set, the words
        /// move up by as much more as the left is met below their top, the leaders on the right with them; the right set, the
        /// words move so where no leader meets them on the left, else the leaders on the right are landed lower by as much. Three
        /// lines at (100, 50, 0) met on both sides at the middle of their top line, the left set to the bottom of the bottom line,
        /// stand at (100, 65.33, 0), the dogleg on their right from (120.67, 63.33, 0). Attachment 8, the underline of every line,
        /// underlines the words where a leader meets them on its side, as AutoCAD writes it: "ABC" becomes <c>\LABC</c>, words
        /// that begin with <c>\L</c> or have no letters left; set away from it, <c>\L</c> is taken off the words again.
        /// </summary>
        /// <param name="textAttachmentType">Where they meet.</param>
        /// <param name="leaderDirection">The side.</param>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfTextAttachmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the top or the bottom is set in a file older than R2010 (AC1024), or the MULTILEADER is erased.</exception>
        public void SetTextAttachmentType(DxfTextAttachmentType textAttachmentType, DxfLeaderDirectionType leaderDirection)
        {
            AttachmentCodes(leaderDirection, out int code, out int context, out _, out int bit);
            bool vertical = code == 272 || code == 273;

            if (textAttachmentType == GetTextAttachmentType(leaderDirection))
            {
                // The bit alone, as AutoCAD 2021 sets it; the top and the bottom have none before R2010.
                if (!vertical || Item.File.Supports(1024))
                {
                    Mark(bit);
                }

                return;
            }

            Attachment(textAttachmentType);

            if (vertical)
            {
                Item.File.Requires(1024, "the attachment of the top and the bottom of a MULTILEADER", "R2010 (AC1024)");
            }

            Rec rec = Read();
            Ctx ctx = Context(rec);
            bool lined = AllLined(rec, ctx);
            int was = Whole(Text(rec.After, code), 1);
            string text = ((int)textAttachmentType).ToString(CultureInfo.InvariantCulture);
            SetTop(rec, code, text, bit);

            if (vertical)
            {
                Put(ctx.Tail, context, text, new[] { 272, 273 });
            }
            else
            {
                Put(ctx.Head, context, text, ContextOrder);
                Reattached(rec, ctx, code == 173, was, (int)textAttachmentType);
                Underline(rec, ctx, lined);
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
                    Mark(14);
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
        /// <value>Set, bit 30 of the overrides set with it, as AutoCAD 2021 sets it, the value it has too.</value>
        /// <exception cref="InvalidOperationException">Thrown when it is changed in a file older than R2013 (AC1027), or the MULTILEADER is erased.</exception>
        public bool ExtendLeaderToText
        {
            get => Whole(Text(Read().After, 295), 0) != 0;

            set
            {
                if (value != ExtendLeaderToText)
                {
                    Item.File.Requires(1027, "a MULTILEADER's leaders run on to its words", "R2013 (AC1027)");
                    SetTopFlag(295, value, !value, 30);
                }
                else if (Item.File.Supports(1027))
                {
                    Mark(30);
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
        /// block, and the base point then put where a leader from the left meets the block, as AutoCAD 2021 keeps it, which
        /// <see cref="BlockScale"/> and <see cref="BlockRotation"/> keep the block met at. The values of the block's attributes
        /// are given by <see cref="SetBlockAttribute"/>.
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
                bool had = HasSection(ctx, 296, BlockCodes);

                if (!had)
                {
                    DropSection(ctx, 290, MTextCodes);
                    MakeBlock(rec, ctx);
                    SetTop(rec, 172, "1", 10);
                }

                Section(ctx.Head, 296, BlockCodes, out int from, out int to);
                Put(ctx.Head, 341, value.Handle, ContextOrder, from, to);
                SetTop(rec, 344, value.Handle, 19);

                // The base point of the content where a leader from the left meets the block, as AutoCAD 2021 keeps it.
                if (!had && BlockMeets(rec, ctx, -double.MaxValue, out _, out GeoPoint3 left))
                {
                    PutPoint(ctx.Head, 10, left, ContextOrder);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets where the block is placed (group 15 of the block of the context data), as AutoCAD's <c>BlockPosition</c>,
        /// in the world; the base point of the content (10 of the context data) where the MULTILEADER holds no block. Set, the
        /// base point of the content and the point each leader's dogleg starts at (10 of the context data and of each leader)
        /// move with the block, and the block's matrix (its groups 47) is left as it is, as AutoCAD 2021 moves and leaves them:
        /// the box of lines from (-10, -5) to (20, 15) moved from (100, 50, 0) to (130, 60, 0) moves a dogleg from (82, 55, 0) to
        /// (112, 65, 0), the leader lines' own vertices staying; the matrix is written anew where <see cref="BlockScale"/> or
        /// <see cref="BlockRotation"/> is set.
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

                // The landings and the base point move with the block, its matrix left, as AutoCAD 2021 moves them
                // (k5-BOX-moved-left, d4-k-blockscale2-moved).
                GeoPoint3 Moved(GeoPoint3 p) => new GeoPoint3(p.X + (value.X - was.X), p.Y + (value.Y - was.Y), p.Z + (value.Z - was.Z));
                MovePoint(rec.Context.Head, 10, Moved, ContextOrder, 0, -1);

                foreach (LeaderPart leader in rec.Context.Leaders)
                {
                    MovePoint(leader.Head, 10, Moved, LeaderOrder, 0, -1);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets how the block is scaled along its axes, as AutoCAD's <c>BlockScale</c>: unscaled, as AutoCAD 2021's API
        /// reads it, the MULTILEADER's own (group 10), else the scale the block is drawn with (16 of the block of the context
        /// data) over the <see cref="Scale"/>; 1 each way where the record gives neither. A block drawn 2 at a scale of 2 reads
        /// 1.
        /// </summary>
        /// <value>Set, written in the MULTILEADER's own and, times the <see cref="Scale"/>, as the block is drawn, as AutoCAD 2021
        /// writes them: 3 at a scale of 2 gives 10 of 3 and 16 of 6. The block then moves as AutoCAD 2021 moves it, with leaders
        /// or none: so that a leader from the left meets it at the base point of the content (10 of the context data), and each
        /// leader on a side is landed anew at it, its matrix (47) made its transform there. The box of lines from (-10, -5) to
        /// (20, 15) placed at (100, 50, 0), met from (82, 55, 0) on its left, scaled by 2 stands at (110, 45, 0), the dogleg kept;
        /// met on its right, the dogleg starts at (158, 55, 0) for (128, 55, 0). The scale it has, set again, does all of this too,
        /// as AutoCAD 2021 does: the box moved to (130, 60, 0) and given its scale of 1 has its matrix written there; scaled by 2 by
        /// <see cref="DxfEntity.TransformBy"/>, it is drawn at 1 again.</value>
        /// <exception cref="ArgumentException">Thrown when a scale set is not a finite number other than nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public GeoVector3 BlockScale
        {
            get
            {
                Rec rec = Read();

                if (Text(rec.After, 10) != null || rec.Context == null || BlockText(rec, 16) == null)
                {
                    return PointOf(rec.After, 10, new GeoPoint3(1.0, 1.0, 1.0)).ToVector();
                }

                double scale = ScaleOf(rec.Context);
                return PointOf(BlockGroups(rec, out _, out _), 16, new GeoPoint3(scale, scale, scale)).ToVector().Multiply(1.0 / scale);
            }

            set
            {
                if (!value.IsValid || value.X == 0.0 || value.Y == 0.0 || value.Z == 0.0)
                {
                    throw new ArgumentException("A block is scaled by finite numbers other than nought: " + value + " is not.", nameof(value));
                }

                // Set as it is too, as AutoCAD 2021 sets it (c3-k-moved-blockscale1, c3-k-transform2-blockscale1).
                var p = new GeoPoint3(value.X, value.Y, value.Z);
                Rec rec = Read();
                PutPoint(rec.After, 10, p, TopOrder);
                SetBit(rec, 21);
                Ctx ctx = Context(rec);
                Section(ctx.Head, 296, BlockCodes, out int from, out int to);

                if (from >= 0)
                {
                    double scale = ScaleOf(ctx);
                    PutPoint(ctx.Head, 16, new GeoPoint3(p.X * scale, p.Y * scale, p.Z * scale), ContextOrder, from, to);
                    Rebased(rec, ctx);
                }

                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets how far the block is turned, as AutoCAD's <c>BlockRotation</c>, in radians: the MULTILEADER's own (43), as
        /// AutoCAD 2021's API reads it, else as the block is drawn, group 46 of the block of the context data; nought where the
        /// record gives neither. A block turned a quarter by <see cref="DxfEntity.TransformBy"/> is drawn turned a quarter and
        /// reads nought, as AutoCAD reads it.
        /// </summary>
        /// <value>Set, even to the turn it has, written in both, the turn it is drawn with (46) taken from the x axis of the
        /// MULTILEADER's plane as the block's own plane sees it, as AutoCAD 2021 writes it: 30 degrees on a multileader turned a
        /// quarter by <see cref="DxfEntity.TransformBy"/> draws the block turned 120. The block then moves as
        /// <see cref="BlockScale"/> says, as AutoCAD 2021 moves it: the box of lines from (-10, -5) to (20, 15) placed at
        /// (100, 50, 0), turned by 30 degrees, stands at (105.49, 48.17, 0), still met at (90, 55, 0) on its left.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double BlockRotation
        {
            get
            {
                Rec rec = Read();
                return Num(Text(rec.After, 43), Num(BlockText(rec, 46), 0.0));
            }

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                // Set as it is too, as AutoCAD 2021 sets it (c3-k-moved-blockrot0).
                Rec rec = Read();
                PutNumber(rec.After, 43, value, 0.0, TopOrder);
                SetBit(rec, 22);
                Ctx ctx = Context(rec);
                Section(ctx.Head, 296, BlockCodes, out int from, out int to);

                if (from >= 0)
                {
                    // Drawn turned from the plane's x axis as the block's own plane sees it, as AutoCAD 2021 draws it
                    // (k-rot90-blockrot30, k-mirrorxy-blockrot30): nought where the plane is the world's.
                    GeoVector3 normal = PointOf(ctx.Head.GetRange(from, to - from), 14, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
                    GeoVector3 planeX = new DxfOcsTransform.Axes(normal.Length > 0.0 ? normal : new GeoVector3(0.0, 0.0, 1.0)).FromWorld(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                    double plane = Math.Atan2(planeX.Y, planeX.X);
                    PutNumber(ctx.Head, 46, plane == 0.0 ? value : value + plane, 0.0, ContextOrder, from, to);
                    Rebased(rec, ctx);
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
            set => SetColour(93, 93, 296, BlockCodes, value, 20, true);
        }

        /// <summary>Gets or sets where the leaders meet the block (group 176), as AutoCAD's <c>BlockConnectionType</c>; at its box where the record gives none.</summary>
        /// <value>Set, written in 176 and in the context data (177), which AutoCAD 2021 reads it from.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfBlockConnectionType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public DxfBlockConnectionType BlockConnectionType
        {
            get => (DxfBlockConnectionType)Whole(Text(Read().After, 176), 0);

            set
            {
                SetTopEnum(176, (int)value, (int)BlockConnectionType, 0, 1, 23);
                Rec rec = Read();

                if (rec.Context != null && Whole(Text(rec.Context.Head, 177), 0) != (int)value)
                {
                    Put(rec.Context.Head, 177, ((int)value).ToString(CultureInfo.InvariantCulture), ContextOrder);
                    Write(rec);
                }
            }
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
            var attribute = new DxfAttributeReference { Tag = attDefId.Tag };

            if (at >= 0)
            {
                // The value as the entry writes it, carets and all.
                attribute.SetWrittenText(Text(after, 302, at) ?? string.Empty);
            }
            else
            {
                attribute.TextString = attDefId.TextString;
            }

            return attribute;
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
        /// Gets or sets the size of the arrows, as AutoCAD's <c>ArrowSize</c>: unscaled, as AutoCAD 2021's API reads it, the size
        /// the arrows are drawn with (140 of the context data) over the <see cref="Scale"/>, else the MULTILEADER's own (42); 4
        /// where the record gives neither. Set, the size is written in 42 and, times the <see cref="Scale"/>, in 140;
        /// <see cref="Scale"/> and <see cref="DxfEntity.TransformBy"/> scale 140 alone, as AutoCAD 2021 scales it, so that a
        /// MULTILEADER of arrows 4 scaled by 2 by <see cref="DxfEntity.TransformBy"/> reads 8, its 42 still 4, until the size is
        /// set again. A leader line of no size of its own reads 42 (<see cref="GetArrowSize"/>), as AutoCAD 2021 reads it.
        /// </summary>
        /// <value>Set, written anew even where it is the size the MULTILEADER has, as AutoCAD 2021 writes it, the bit of the
        /// overrides set: 5 at a scale of 2 gives 42 of 5 and arrows drawn 10, and 4 set again on a MULTILEADER of arrows 4 scaled
        /// by 2 by <see cref="DxfEntity.TransformBy"/>, drawn 8, draws them 4 again. The sizes leader lines have of their own stay.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double ArrowSize
        {
            get
            {
                Rec rec = Read();
                return rec.Context == null || Text(rec.Context.Head, 140) == null ? Num(Text(rec.After, 42), 4.0) : Unscaled(rec.Context, 140, 4.0);
            }

            set
            {
                Guard.NonNegative(value, nameof(value), "An arrow's size has to be a finite number, nought or more.");
                Rec rec = Read();
                PutNumber(rec.After, 42, value, 4.0, TopOrder);
                SetBit(rec, 9);
                Ctx ctx = Context(rec);
                PutNumber(ctx.Head, 140, value * Num(Text(ctx.Head, 40), 1.0), 4.0, ContextOrder);
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
                    Mark(8);
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

        /// <summary>
        /// Gets or sets whether the leaders have doglegs (group 291), as AutoCAD's <c>EnableDogleg</c>, as the record says it. A
        /// multileader whose leader lines are splined (<see cref="LeaderLineType"/>) draws none whatever this says, as AutoCAD
        /// 2021 draws none, its own <c>EnableDogleg</c> reading false for it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public bool EnableDogleg
        {
            get => Whole(Text(Read().After, 291), 1) != 0;
            set => SetTopFlag(291, value, EnableDogleg, 6);
        }

        /// <summary>
        /// Gets or sets how long the doglegs are, as AutoCAD's <c>DoglegLength</c>: unscaled, as AutoCAD 2021's API reads it, the
        /// length the first leader's dogleg is drawn (its 40) over the <see cref="Scale"/>, else, where the MULTILEADER has no
        /// leader or its first meets the words at their top or bottom, its own (group 41, unscaled); 8 where the record gives
        /// none. Doglegs drawn 16 by
        /// <see cref="DxfEntity.TransformBy"/> by 2, 41 still 8, read 16; the first leader given 15 by
        /// <see cref="SetDoglegLength"/> reads 15.
        /// </summary>
        /// <value>Set, written as the MULTILEADER's, and as each leader's (40) times the scale, as AutoCAD 2021 writes them: 3 at a
        /// scale of 2 gives 41 of 3 and doglegs of 6 (40); the bit of the overrides set. The content moves or the leaders are
        /// landed anew as AutoCAD 2021 does it: with one leader, on one side, the words or block move away from it by as much as
        /// its dogleg grew, words at (100, 50, 0) met on the right to (93, 50, 0) for a dogleg of 15 from 8; with leaders on both
        /// sides, or several on one, the content stays and each dogleg's start moves away from it, from (90, 48, 0) to (83, 48, 0)
        /// on the left and from (120.67, 48, 0) to (127.67, 48, 0) on the right, and every leader on the left of the words or on
        /// a side of a block is landed anew at it, as AutoCAD 2021 lands it, one moved off its landing snapped back, the length
        /// the doglegs have set too.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double DoglegLength
        {
            get
            {
                Rec rec = Read();
                LeaderPart first = rec.Context != null && rec.Context.Leaders.Count > 0 ? rec.Context.Leaders[0] : null;

                if (first == null || Text(first.Head, 40) == null || Whole(Text(first.Head, 271) ?? Text(first.Tail, 271), 0) != 0)
                {
                    return Num(Text(rec.After, 41), 8.0);
                }

                return Num(Text(first.Head, 40), 8.0) / ScaleOf(rec.Context);
            }

            set
            {
                if (Same(value, DoglegLength))
                {
                    Rec same = Read();
                    SetBit(same, 7);

                    if (same.Context != null)
                    {
                        Relengthened(same, same.Context, new List<KeyValuePair<LeaderPart, double>>());
                    }

                    Write(same);
                    return;
                }

                Guard.NonNegative(value, nameof(value), "A dogleg's length has to be a finite number, nought or more.");
                Rec rec = Read();
                PutNumber(rec.After, 41, value, 8.0, TopOrder);
                SetBit(rec, 7);

                Ctx ctx = Context(rec);
                double scale = Num(Text(ctx.Head, 40), 1.0);
                var before = new List<KeyValuePair<LeaderPart, double>>();

                foreach (LeaderPart leader in ctx.Leaders)
                {
                    before.Add(new KeyValuePair<LeaderPart, double>(leader, Num(Text(leader.Head, 40), 1.0)));
                    PutNumber(leader.Head, 40, value * scale, 8.0, LeaderOrder);
                }

                Relengthened(rec, ctx, before);
                Write(rec);
            }
        }

        /// <summary>
        /// Gets or sets the gap between the landing and the words, as AutoCAD's <c>LandingGap</c>: unscaled, as AutoCAD 2021's
        /// API reads it, the gap drawn (group 145 of the context data) over the <see cref="Scale"/>; 2 where the record gives
        /// none. A gap drawn 4 at a scale of 2 reads 2.
        /// </summary>
        /// <value>Set, written times the <see cref="Scale"/>, as AutoCAD 2021 writes it: 3 at a scale of 2 gives 145 of 6. The
        /// words met on their sides moved as AutoCAD 2021 moves them, with leaders or none: along the plane's x axis by as much as
        /// the gap drawn grew, the leaders on their left kept and those on their right moved by twice it; a gap of 5 for 2 moves
        /// words at (100, 50, 0) to (103, 50, 0) and a dogleg on their right from (120.67, 48, 0) to (126.67, 48, 0). A block has
        /// no gap and stays.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double LandingGap
        {
            get => Unscaled(Context(Read()), 145, 2.0);
            set => SetContextNumber(145, value, 2.0, 5, false, (rec, ctx, was) => Regapped(rec, ctx, Num(Text(ctx.Head, 145), 2.0) - was));
        }

        /// <summary>Gets the direction of a leader's dogleg (groups 11, 21 and 31 of the leader), as AutoCAD's <c>GetDogleg</c>, in the world.</summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        public GeoVector3 GetDogleg(int leaderIndex) => PointOf(FindLeader(Read(), leaderIndex).Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();

        /// <summary>
        /// Gives a leader's dogleg its direction, as AutoCAD's <c>SetDogleg</c>. A leader of no landing yet (<see cref="AddLeader"/>),
        /// or one whose dogleg is turned to another direction, is landed on the side of the content its dogleg now runs from,
        /// where the library can tell where it meets the content, as AutoCAD 2021 lands it: along x on the left of the words, on
        /// either side of a block; on the right of the words, which AutoCAD lands by their font's width, it keeps its point. The
        /// box of lines from (-10, -5) to (20, 15) at (100, 50, 0) is met against x from (128, 55, 0), where a leader just added
        /// lands, and where its leader from the left lands once turned; a vertex then given a line of it goes before the landing.
        /// </summary>
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
            LeaderPart leader = FindLeader(rec, leaderIndex);
            GeoVector3 was = Unit(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            PutPoint(leader.Head, 11, new GeoPoint3(vector.X, vector.Y, vector.Z), LeaderOrder);

            // Landed at the content, as AutoCAD 2021 lands it (a1-setdogleg-addfirst, a1-k-negdogleg-only, k-left-setdogleg-neg).
            bool landed = HasLanding(leader);

            if (!landed || Unit(vector).Subtract(was).Length > 1E-9)
            {
                LandAnew(rec, Context(rec), new List<LeaderPart> { leader });

                if (!landed)
                {
                    Put(leader.Head, 290, "1", LeaderOrder);
                }
            }

            Write(rec);
        }

        /// <summary>
        /// Gets how long a leader's dogleg is, as AutoCAD's <c>GetDoglegLength</c>: its length as drawn (group 40 of the leader, 1
        /// where the leader gives none, as ezdxf reads one) over the <see cref="Scale"/>, as AutoCAD 2021 gives it, 8 for a
        /// dogleg drawn 16 at a scale of 2; nought for a leader that meets the words at their top or bottom, which has none.
        /// </summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <returns>The length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index.</exception>
        public double GetDoglegLength(int leaderIndex)
        {
            Rec rec = Read();
            LeaderPart leader = FindLeader(rec, leaderIndex);

            if (Whole(Text(leader.Head, 271) ?? Text(leader.Tail, 271), 0) != 0)
            {
                return 0.0;
            }

            double length = Num(Text(leader.Head, 40), 1.0);
            double scale = Num(Text(Context(rec).Head, 40), 1.0);
            return Math.Abs(scale) > 1E-9 ? length / scale : length;
        }

        /// <summary>
        /// Gives a leader's dogleg its length, as AutoCAD's <c>SetDoglegLength</c>: drawn times the <see cref="Scale"/> (group 40
        /// of the leader), as AutoCAD 2021 writes it, 2 for 1 at a scale of 2, and the bit of the overrides of the doglegs (7) set,
        /// the length it has too. The content or the leader moves as <see cref="DoglegLength"/> says: the content, where the
        /// leader is the only one of its side and none meets the content on the other; else the leader's start alone, as AutoCAD
        /// 2021 moves it: of two leaders landed at (150, 38, 0) on the left of words at (160, 40, 0), the second given 15 starts at
        /// (143, 38, 0), the words and the first staying.
        /// </summary>
        /// <param name="leaderIndex">The index of the leader.</param>
        /// <param name="doglegLength">The length, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no leader of the index, or the length is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetDoglegLength(int leaderIndex, double doglegLength)
        {
            Guard.NonNegative(doglegLength, nameof(doglegLength), "A dogleg's length has to be a finite number, nought or more.");
            Rec rec = Read();
            LeaderPart leader = FindLeader(rec, leaderIndex);
            double drawn = doglegLength * Num(Text(Context(rec).Head, 40), 1.0);
            SetBit(rec, 7);

            if (!Same(drawn, Num(Text(leader.Head, 40), double.NaN)))
            {
                var before = new List<KeyValuePair<LeaderPart, double>> { new KeyValuePair<LeaderPart, double>(leader, Num(Text(leader.Head, 40), 1.0)) };
                PutNumber(leader.Head, 40, drawn, double.NaN, LeaderOrder);
                Relengthened(rec, Context(rec), before);
            }
            else
            {
                Relengthened(rec, Context(rec), new List<KeyValuePair<LeaderPart, double>>());
            }

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
            set => SetColour(91, -1, 0, null, value, 1, true);
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
        /// <value>Set, the name of a style of ACAD_MLEADERSTYLE, letters of either case the same: its handle written, and the
        /// style's linetype of the leader lines (341, the style's 340), its arrow block (342, the style's 341) and the text style
        /// of its words (343 and the words' 340, the style's 342) taken where the MULTILEADER does not override them (bits 2, 8
        /// and 11 of its 90), as AutoCAD 2021 writes them. A style of no arrow block, AutoCAD's closed filled arrow, takes the
        /// MULTILEADER's arrow block away, but where it has one of its own.</value>
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
                        TakeStyleLook(rec, Database());
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
        /// context data scaled by the new over the old, as ezdxf's <c>set_scale</c> scales them, the width and the height of the
        /// words (43 and 44 of its MTEXT) with them, as AutoCAD 2021 scales them, words 60 by 30 then 120 by 60 at a scale of 2,
        /// and words in columns their height alone, their width, columns and gutter (43, 142 to 144) kept, as AutoCAD keeps them;
        /// and past it the length of each leader's dogleg as drawn (40), which ezdxf leaves, so that it keeps its size against
        /// the words; the MULTILEADER's own length of the doglegs (41) is left unscaled, as AutoCAD 2021 leaves it: Standard's 8
        /// at a scale of 2 is still 8, drawn 16. Words met by leaders on their left only, the doglegs alike, move to keep those
        /// landings, as AutoCAD moves them: "ABC" at (100, 50, 0) met from (90, 48, 0) stand at (110, 52, 0) at a scale of 2, the
        /// base point of the content (10) moved by the doglegs to (106, 48, 0); met by several leaders on their left, they move by
        /// the gap alone and up, and the leaders are landed anew, "ABC" at (160, 40, 0) met from (150, 38, 0) twice standing at
        /// (162, 42, 0), both doglegs from (142, 38, 0). Words met on their right stay where they are, which AutoCAD moves by the
        /// font's width. A block is drawn scaled too (16 of its context data, and its matrix, 47, made its
        /// transform where it stood), as AutoCAD 2021 scales it, its <see cref="BlockScale"/> still read the same; met by one
        /// leader, it moves so that it meets the leader's longer dogleg at its end, the dogleg's start kept: the box of lines from
        /// (-10, -5) to (20, 15) placed at (100, 50, 0), met from (82, 55, 0), stands at (118, 45, 0) at a scale of 2. Met on
        /// both sides, or by several leaders on one, it stays and the leaders are landed anew at it, from (64, 60, 0) and
        /// (156, 60, 0) for that box. The scale it has sets the bit of the overrides (24) alone.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public double Scale
        {
            get => Num(Text(Context(Read()).Head, 40), 1.0);

            set
            {
                if (Same(value, Scale))
                {
                    Mark(24);
                    return;
                }

                Guard.Positive(value, nameof(value), "A scale has to be a finite number above nought.");
                Rec rec = Read();
                Ctx ctx = Context(rec);
                double old = Num(Text(ctx.Head, 40), 1.0);
                double factor = old > 0.0 ? value / old : 1.0;
                double wasGap = Num(Text(ctx.Head, 145), 2.0);
                double wasHeight = Height(ctx);
                PutNumber(ctx.Head, 40, value, 1.0, ContextOrder);

                foreach (int code in new[] { 41, 140, 145 })
                {
                    if (Text(ctx.Head, code) != null)
                    {
                        PutNumber(ctx.Head, code, Num(Text(ctx.Head, code), 0.0) * factor, 0.0, ContextOrder);
                    }
                }

                // The width and height of the words, as AutoCAD 2021 scales them: 60 by 30 at a scale of 1 is 120 by 60 at 2.
                // Words in columns (173 of 1 or 2) keep their width, columns and gutter, their height alone scaled.
                Section(ctx.Head, 290, MTextCodes, out int from, out int to);
                bool columned = from >= 0 && Whole(Text(ctx.Head.GetRange(from, to - from), 173), 0) != 0;

                foreach (int code in columned ? new[] { 44 } : new[] { 43, 44 })
                {
                    Section(ctx.Head, 290, MTextCodes, out from, out to);
                    int at = from < 0 ? -1 : ctx.Head.FindIndex(from, to - from, g => g.Key == code);

                    if (at >= 0)
                    {
                        PutNumber(ctx.Head, code, Num(ctx.Head[at].Value, 0.0) * factor, 0.0, ContextOrder, from, to);
                    }
                }

                var before = new List<KeyValuePair<LeaderPart, double>>();

                foreach (LeaderPart leader in ctx.Leaders)
                {
                    before.Add(new KeyValuePair<LeaderPart, double>(leader, Num(Text(leader.Head, 40), 1.0)));

                    if (Text(leader.Head, 40) != null)
                    {
                        PutNumber(leader.Head, 40, Num(Text(leader.Head, 40), 1.0) * factor, 1.0, LeaderOrder);
                    }
                }

                Rescaled(rec, ctx, before, Num(Text(ctx.Head, 145), 2.0) - wasGap, wasHeight);
                Reblocked(rec, ctx, factor);

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
        /// Gets how many vertices a leader line has, as AutoCAD's <c>VerticesCount</c>: its own (10 of the line), then the end of
        /// its leader's landing (<see cref="GetLastVertex"/>), which every line of the leader ends at; its own alone where the
        /// leader has no landing yet (<see cref="AddLeader"/>), none for a line just added to it, as AutoCAD 2021 counts them.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public int VerticesCount(int leaderLineIndex) => Vertices(Read(), leaderLineIndex, out _, out _).Count;

        /// <summary>Gets a vertex of a leader line, as AutoCAD's <c>GetVertex</c>, in the world: the last the end of its leader's landing, as <see cref="GetLastVertex"/> gives it.</summary>
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

        /// <summary>
        /// Gets the last vertex of a leader line, as AutoCAD's <c>GetLastVertex</c>: the point of its leader the dogleg starts at
        /// (10 of the leader); where the leader meets the words on their left or right and its dogleg is not drawn (no
        /// <see cref="EnableDogleg"/>, or its leader lines splined), the end the dogleg would have, which the line runs on to, as
        /// AutoCAD 2021 gives it: a line from (0, 0, 0) whose leader's dogleg of 8 starts at (90, 48, 0) ends at (98, 48, 0) once
        /// the dogleg is taken away.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public GeoPoint3 GetLastVertex(int leaderLineIndex) => GetVertex(leaderLineIndex, VerticesCount(leaderLineIndex) - 1);

        /// <summary>
        /// Moves a vertex of a leader line, as AutoCAD's <c>SetVertex</c>: one of its own; the last, the end of its leader's
        /// landing (<see cref="GetLastVertex"/>), which moves the end of every line of the leader with it, and, for a leader on a
        /// side of the content, the words or block and the landings of the other leaders on a side by as much, as AutoCAD 2021
        /// moves them: words "ABC" at (100, 50, 0) met from (90, 48, 0), the last vertex set to (50, 60, 0), stand at (60, 62, 0).
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
            WriteVertices(rec, line, leader, vertices, index == vertices.Count - 1);
        }

        /// <summary>Moves the first vertex of a leader line, as AutoCAD's <c>SetFirstVertex</c>, as <see cref="SetVertex"/> moves one.</summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetFirstVertex(int leaderLineIndex, GeoPoint3 point) => SetVertex(leaderLineIndex, 0, point);

        /// <summary>
        /// Moves the last vertex of a leader line, as AutoCAD's <c>SetLastVertex</c>: the point of its leader the dogleg starts
        /// at, or, where the dogleg is not drawn, that point so that the end the dogleg would have is there (<see cref="GetLastVertex"/>):
        /// (98, 48, 0) set to (50, 60, 0) puts the dogleg's start at (42, 60, 0), as AutoCAD 2021 puts it. The words or block and
        /// the other landings move with it, as <see cref="SetVertex"/> says; <see cref="AddLastVertex"/> moves neither, as AutoCAD's.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetLastVertex(int leaderLineIndex, GeoPoint3 point) => SetVertex(leaderLineIndex, VerticesCount(leaderLineIndex) - 1, point);

        /// <summary>
        /// Puts a vertex in before the first of a leader line, where its arrow goes, as AutoCAD's <c>AddFirstVertex</c>. On a line
        /// of a leader of no landing yet (<see cref="AddLeader"/>), the vertex becomes the leader's landing, as AutoCAD 2021 takes
        /// it, the line of one vertex and the content left where it is: (-20, 80, 0) given so, then the last vertex set to
        /// (40, 70, 0), moves words at (100, 50, 0) by (60, -10, 0), to (160, 40, 0).
        /// </summary>
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
        /// Adds a vertex after the last of a leader line, as AutoCAD's <c>AddLastVertex</c>: it becomes the end of the leader's
        /// landing (<see cref="GetLastVertex"/>), the old one a vertex of the line, and every line of the leader ends at it. On a
        /// line of a leader of no landing yet it becomes the landing, as <see cref="AddFirstVertex"/> says.
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
        /// Takes the last vertex of a leader line out, as AutoCAD's <c>RemoveLastVertex</c>: the vertex before it becomes the end
        /// of the leader's landing (<see cref="GetLastVertex"/>); a line keeps two vertices.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has two vertices or fewer, or the MULTILEADER is erased.</exception>
        public void RemoveLastVertex(int leaderLineIndex) => RemoveVertex(leaderLineIndex, false);

        /// <summary>
        /// Adds a leader of no lines, as AutoCAD's <c>AddLeader</c>: its index (90) one above every leader's, its dogleg along the
        /// x axis of the plane, as long as <see cref="DoglegLength"/> times the <see cref="Scale"/>, written as ezdxf writes a
        /// leader, and of no landing yet (290 of nought), as AutoCAD 2021 writes it: it draws no dogleg, a line added to it has
        /// no vertex, and the first vertex given a line of it (<see cref="AddFirstVertex"/> or <see cref="AddLastVertex"/>)
        /// becomes its landing, the content left where it is, unless <see cref="SetDogleg"/> lands it first. Words at
        /// (100, 50, 0) given a leader whose line is given (-20, 80, 0) are met there, the line of one vertex, its dogleg drawn
        /// from (-20, 80, 0) to (-12, 80, 0).
        /// </summary>
        /// <returns>The index of the leader.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public int AddLeader()
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            int index = AddLeaderAt(rec, ctx, PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0)), null, false);
            Write(rec);
            return index;
        }

        /// <summary>
        /// Adds to the context data a leader of no lines whose dogleg starts at a point, along the x axis of the plane or a
        /// direction given, as long as <see cref="DoglegLength"/> times the scale, its index (90) one above every leader's, written
        /// as ezdxf writes a leader; landed there, or of no landing (290 of nought, the point kept).
        /// </summary>
        private int AddLeaderAt(Rec rec, Ctx ctx, GeoPoint3 start, GeoVector3? direction = null, bool landed = true)
        {
            int index = 0;

            foreach (LeaderPart other in ctx.Leaders)
            {
                index = Math.Max(index, other.Index + 1);
            }

            GeoPoint3 along = direction.HasValue ? Pt(direction.Value) : PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0));
            var head = new List<KeyValuePair<int, string>> { G(290, landed ? "1" : "0"), G(291, "1") };
            head.AddRange(PointGroups(10, start));
            head.AddRange(PointGroups(11, along));
            head.Add(G(90, index));
            head.Add(G(40, Num(Text(rec.After, 41), 8.0) * Num(Text(ctx.Head, 40), 1.0)));
            var tail = new List<KeyValuePair<int, string>>();

            if (Item.File.Supports(1024))
            {
                tail.Add(G(271, Whole(Text(rec.After, 271), 0)));
            }

            ctx.Leaders.Add(LeaderPart.Made(head, tail));
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
        /// arrow is, the line added to the first leader whose dogleg runs from the point's side towards the content. Where none
        /// does and the point is on the left of words held at their top left and met on their sides, along the plane, the line
        /// is added to a new leader landed as AutoCAD 2021 lands it: its dogleg along the x axis of the plane, as long as
        /// <see cref="DoglegLength"/> times the <see cref="Scale"/>, ending the <see cref="LandingGap"/> short of the words' left
        /// edge, as far below their top as the attachment of the left side (<see cref="TextAttachmentType"/>) says: nought at the
        /// top of the top line, half the height of the words at its middle, all of it at its bottom and a fifth more under it;
        /// the middle of the words, the middle of the bottom line, its bottom and a fifth more under it as the lines of the words
        /// stand, each 5/3 of their height times their spacing below the one above, the lines counted by their breaks (<c>\P</c>),
        /// as words that do not wrap. Words "ABC" 4 high held at (100, 50, 0), met at the middle of their top line with a gap of
        /// 2 and a dogleg of 8, are met by a leader from (0, 0, 0) whose dogleg runs from (90, 48, 0) to (98, 48, 0); three lines
        /// met at their middle, from (90, 41.33, 0). A block is met on the side of its box the point is on (its middle along x),
        /// with no gap, its dogleg ending where the block's shapes meet the line along x through the middle of its box (connected
        /// at its extents) or through its position (at its base), the outermost such point on that side, else that point of the
        /// line; the box is the block's own, of the shapes the library reads, carried as the block is drawn and boxed in the
        /// plane: the box of lines from (-10, -5) to (20, 15) placed at (100, 50, 0) is met from (0, 0, 0) by a dogleg from (82, 55, 0)
        /// to (90, 55, 0), and from (250, 0, 0) by one from (128, 55, 0) to (120, 55, 0). Elsewhere, the line goes to the first
        /// leader, one made at the base point of the content where there is none: on the right of the words, which AutoCAD lands
        /// as far right as its font makes them wide; for words turned, held otherwise or met at their top or bottom. A leader of
        /// no landing yet (<see cref="AddLeader"/>) is passed over, as AutoCAD 2021 passes it. Words a leader meets on a side of
        /// attachment 8 are underlined (<see cref="SetTextAttachmentType"/>).
        /// </summary>
        /// <param name="point">The point, in the world.</param>
        /// <returns>The index of the line.</returns>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public int AddLeaderLine(GeoPoint3 point)
        {
            Finite(point, nameof(point));
            Rec rec = Read();
            Ctx ctx = Context(rec);
            LeaderPart chosen = null;

            foreach (LeaderPart leader in ctx.Leaders)
            {
                GeoVector3 dogleg = PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                GeoPoint3 start = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));

                if (HasLanding(leader) && dogleg.DotProduct(point.GetVectorTo(start)) > 0.0)
                {
                    chosen = leader;
                    break;
                }
            }

            if (chosen == null)
            {
                // A new leader on the left of the words or on either side of a block, landed as AutoCAD 2021 lands it; else the
                // first, made at the base point.
                if (LeftLanding(rec, ctx, point, out GeoPoint3 landing))
                {
                    AddLeaderAt(rec, ctx, landing);
                    chosen = ctx.Leaders[ctx.Leaders.Count - 1];
                }
                else if (ContentType == DxfContentType.BlockContent && BlockLanding(rec, ctx, point, out landing, out GeoVector3 dogleg))
                {
                    AddLeaderAt(rec, ctx, landing, dogleg);
                    chosen = ctx.Leaders[ctx.Leaders.Count - 1];
                }
                else
                {
                    chosen = ctx.Leaders.Find(HasLanding);

                    if (chosen == null)
                    {
                        AddLeaderAt(rec, ctx, PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0)));
                        chosen = ctx.Leaders[ctx.Leaders.Count - 1];
                    }
                }
            }

            int index = NextLine(rec);
            var line = LinePart.Made(new[] { G(91, index), G(92, ByBlock) });
            line.SetVertices(new List<GeoPoint3> { point });
            chosen.Lines.Add(line);
            Underline(rec, ctx, false);
            Write(rec);
            return index;
        }

        /// <summary>
        /// Where AutoCAD 2021 lands a new leader from a point on the left of the words (<see cref="AddLeaderLine(GeoPoint3)"/>):
        /// the point its dogleg starts at, where the words are held at their top left (171 of 1) along the plane's x axis, met on
        /// their sides (271 of nought) at a point of the left side the words' height and lines give (173 of 0 to 8), and the
        /// point is on their left; false elsewhere.
        /// </summary>
        private static bool LeftLanding(Rec rec, Ctx ctx, GeoPoint3 point, out GeoPoint3 landing)
        {
            landing = point;
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0 || Whole(Text(rec.After, 271), 0) != 0)
            {
                return false;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            GeoVector3 x = Unit(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoVector3 y = Unit(PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector());
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoPoint3 words = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));

            if (Whole(Text(m, 171), 1) != 1 || !(x.Length > 0.0) || !(y.Length > 0.0) || along.Subtract(x).Length > 1E-9
                || !(point.GetVectorTo(words).DotProduct(x) > 0.0))
            {
                return false;
            }

            double below = Below(ctx, AttachmentOf(rec, true), Height(ctx));

            if (double.IsNaN(below))
            {
                return false;
            }

            double back = Num(Text(ctx.Head, 145), 2.0) + (Num(Text(rec.After, 41), 8.0) * Num(Text(ctx.Head, 40), 1.0));
            landing = words.Add(x.Multiply(-back)).Add(y.Multiply(-below));
            return true;
        }

        /// <summary>How many paragraph breaks (<c>\P</c>) the words of an MTEXT hold as written, an escaped backslash (<c>\\</c>) none.</summary>
        private static int Breaks(string written)
        {
            int breaks = 0;

            for (int i = 0; written != null && i + 1 < written.Length; i++)
            {
                if (written[i] == '\\')
                {
                    breaks += written[i + 1] == 'P' ? 1 : 0;
                    i++;
                }
            }

            return breaks;
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
