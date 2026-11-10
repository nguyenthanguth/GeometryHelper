using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A multileader style of a DXF file, as AutoCAD's <c>MLeaderStyle</c>: an MLEADERSTYLE of the dictionary
    /// ACAD_MLEADERSTYLE (<see cref="DxfDatabase.MLeaderStyleDictionary"/>), the look a <see cref="DxfMLeader"/> of the style
    /// takes where it overrides none of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The style Standard of a drawing of ezdxf has words 4 high in its text style Standard, arrows 4 long, a landing gap of 2
    /// and doglegs 8 long, its leaders straight and BYBLOCK, the words met on the left and right at the middle of their top
    /// line; a group the record leaves out reads as that style has it.
    /// </para>
    /// <para>
    /// A style in no file, made by <c>new DxfMLeaderStyle()</c> as AutoCAD's <c>new MLeaderStyle()</c> is made, has the values
    /// of that style Standard, and is added to a drawing by <see cref="DxfDBDictionary.SetAt"/> of its dictionary of
    /// multileader styles: <c>db.MLeaderStyleDictionary.SetAt("GH_NOTES", style)</c> writes every group AutoCAD 2021 holds
    /// of one, in its order, a linetype, an arrow block, a text style or a block the drawing has none of left out. It can be
    /// given instead the values a multileader is drawn with by <see cref="DxfMLeader.GetOverridedMLeaderStyle"/>: its
    /// style's, and those the multileader overrides its own; its blocks are then those of the multileader's drawing.
    /// </para>
    /// <para>
    /// Each value set is written into the record at once, as AutoCAD 2021 holds it (checked by entget): the angles a
    /// leader's segments are kept to in radians, 45 degrees as 0.785398; no limit to the points of a leader as 0; a style
    /// scaled to the layout as a scale of 0. The multileaders of the style are not changed by it: give each the style's
    /// values with <see cref="DxfMLeader.SetFromStyle"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfMLeaderStyle : DxfDBObject
    {
        // The groups of AcDbMLeaderStyle in the order the DXF reference writes them.
        internal static readonly int[] Order =
        {
            179, 170, 171, 172, 90, 40, 41, 173, 91, 340, 92, 290, 42, 291, 43, 3, 341, 44, 300, 342, 174, 178, 175, 176, 93, 45, 292,
            297, 46, 343, 94, 47, 49, 140, 293, 141, 294, 177, 142, 295, 296, 143, 271, 272, 273, 298,
        };

        // The same order with the subclass marker first, as a group is put into the record.
        private static readonly int[] RecordOrder = Marked();

        private readonly List<KeyValuePair<int, string>> _made = new List<KeyValuePair<int, string>>();
        private string _madeName;
        private DxfDatabase _naming;

        /// <summary>
        /// Initializes a multileader style in no file, of the values of the style Standard, as AutoCAD's
        /// <c>new MLeaderStyle()</c>: to be added to a drawing by <see cref="DxfDBDictionary.SetAt"/>, or given the values a
        /// multileader is drawn with by <see cref="DxfMLeader.GetOverridedMLeaderStyle"/>.
        /// </summary>
        public DxfMLeaderStyle()
        {
        }

        internal DxfMLeaderStyle(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets the name of the style, as AutoCAD's <c>Name</c>: the name of its entry in the dictionary that owns it, as
        /// <c>Standard</c>; for a style in no file, the name of the style of the multileader it was given the values of; null
        /// where it has none.
        /// </summary>
        public string Name
        {
            get
            {
                if (Item == null)
                {
                    return _madeName;
                }

                string owner = OwnerHandle;

                if (owner != null && IsResident && Item.File.Database.TryGetObject(owner, out DxfDBObject found) && found is DxfDBDictionary dictionary)
                {
                    foreach (DxfDBDictionaryEntry entry in dictionary)
                    {
                        if (ReferenceEquals(entry.Value, this))
                        {
                            return entry.Key;
                        }
                    }
                }

                return null;
            }
        }

        /// <summary>Gets or sets what the leaders of the style point to (group 170), as AutoCAD's <c>ContentType</c>; words where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not none, a block or words.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfContentType ContentType
        {
            get => (DxfContentType)Whole(Text(170), 2);
            set => SetWhole(170, (int)value, 0, 2, "A multileader style points to none (0), a block (1) or words (2).");
        }

        /// <summary>
        /// Gets or sets how many points a leader line of the style is drawn through at most (group 90), as AutoCAD's
        /// <c>MaxLeaderSegmentsPoints</c>; 2 where the record gives none, and 0 for no limit, as AutoCAD 2021 holds a style
        /// whose Maximum leader points is not ticked.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is neither 0 nor 2 or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public int MaxLeaderSegmentsPoints
        {
            get => Whole(Text(90), 2);
            set
            {
                if (value != 0 && value < 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A leader is drawn through 2 points or more, or 0 for no limit.");
                }

                Set(90, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Gets or sets the angle the first segment of a leader is kept to (group 40, in radians), as AutoCAD's
        /// <c>FirstSegmentAngleConstraint</c>; any where the record gives none or an angle of no step of AutoCAD's.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="DxfAngleConstraint"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfAngleConstraint FirstSegmentAngleConstraint
        {
            get => AngleOf(Text(40));
            set => Set(40, Radians(value));
        }

        /// <summary>
        /// Gets or sets the angle the second segment of a leader is kept to (group 41, in radians), as AutoCAD's
        /// <c>SecondSegmentAngleConstraint</c>; any where the record gives none or an angle of no step of AutoCAD's.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="DxfAngleConstraint"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfAngleConstraint SecondSegmentAngleConstraint
        {
            get => AngleOf(Text(41));
            set => Set(41, Radians(value));
        }

        /// <summary>Gets or sets how the leader lines are drawn (group 173), as AutoCAD's <c>LeaderLineType</c>; straight where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfLeaderType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfLeaderType LeaderLineType
        {
            get => (DxfLeaderType)Whole(Text(173), 1);
            set => SetWhole(173, (int)value, 0, 2, "A leader is invisible (0), straight (1) or splined (2).");
        }

        /// <summary>Gets or sets the colour of the leader lines (group 91), as AutoCAD's <c>LeaderLineColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfColor LeaderLineColor
        {
            get => DxfMLeaderColors.Of(Text(91) ?? ByBlock);
            set => Set(91, DxfMLeaderColors.Text(value, nameof(value)));
        }

        /// <summary>
        /// Gets or sets the linetype of the leader lines (group 340), as AutoCAD's <c>LeaderLineTypeId</c> gives its id; null
        /// where the style names none, or one the drawing has no linetype of. The style Standard of ezdxf names none, AutoCAD
        /// 2021's names ByBlock.
        /// </summary>
        /// <value>Set, a linetype of the style's drawing, its handle written; null takes the group out.</value>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of no drawing, or of another than the style's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfLinetypeTableRecord LeaderLineLinetype
        {
            get => RecordOf(Text(340)) as DxfLinetypeTableRecord;
            set => SetRecord(340, value, "linetype");
        }

        /// <summary>Gets or sets the lineweight of the leader lines (group 92), as AutoCAD's <c>LeaderLineWeight</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lineweight set is out of AutoCAD's range, -3 to 211.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfLineWeight LeaderLineWeight
        {
            get => (DxfLineWeight)Whole(Text(92), -2);
            set => SetWhole(92, (int)value, -3, 211, "A lineweight is -3 to 211.");
        }

        /// <summary>Gets or sets whether the leaders end in a landing (group 290), as AutoCAD's <c>EnableLanding</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool EnableLanding
        {
            get => Whole(Text(290), 1) != 0;
            set => Set(290, value ? "1" : "0");
        }

        /// <summary>Gets or sets the gap between the landing and the words (group 42), as AutoCAD's <c>LandingGap</c>; 2 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double LandingGap
        {
            get => Num(Text(42), 2.0);
            set => SetSize(42, value);
        }

        /// <summary>Gets or sets whether the leaders have doglegs (group 291), as AutoCAD's <c>EnableDogleg</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool EnableDogleg
        {
            get => Whole(Text(291), 1) != 0;
            set => Set(291, value ? "1" : "0");
        }

        /// <summary>Gets or sets how long the doglegs are (group 43), as AutoCAD's <c>DoglegLength</c>; 8 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double DoglegLength
        {
            get => Num(Text(43), 8.0);
            set => SetSize(43, value);
        }

        /// <summary>
        /// Gets or sets the block drawn as the arrows (group 341), as AutoCAD's <c>ArrowSymbolId</c> gives its id; null for
        /// AutoCAD's closed filled arrow, which needs no block, and where the drawing has no block of the handle.
        /// </summary>
        /// <value>Set, a block of the style's drawing, its handle written; null, the closed filled arrow, takes the group out.</value>
        /// <exception cref="ArgumentException">Thrown when the block set is of no drawing, or of another than the style's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfBlockTableRecord ArrowSymbol
        {
            get => BlockOf(Text(341));
            set => SetRecord(341, value, "block");
        }

        /// <summary>Gets or sets the size of the arrows (group 44), as AutoCAD's <c>ArrowSize</c>; 4 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double ArrowSize
        {
            get => Num(Text(44), 4.0);
            set => SetSize(44, value);
        }

        /// <summary>
        /// Gets or sets the words a new multileader of the style is offered (group 300), as AutoCAD's <c>DefaultMText</c>: an
        /// MTEXT in no file of those words; null where the style has none. Set, the words of the MTEXT written, as AutoCAD 2021
        /// holds them, <c>Default Text</c> as 300 of <c>Default Text</c>; null takes them away.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfMText DefaultMText
        {
            get
            {
                string words = Text(300);
                return string.IsNullOrEmpty(words) ? null : new DxfMText { Contents = words };
            }

            set => Set(300, value?.Contents ?? string.Empty);
        }

        /// <summary>Gets where the leaders on the left meet the words (group 174), as AutoCAD's <c>TextAttachmentType</c>; the middle of the top line where the record gives none.</summary>
        public DxfTextAttachmentType TextAttachmentType => GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader);

        /// <summary>
        /// Gets where the leaders of a side meet the words, as AutoCAD's <c>GetTextAttachmentType</c>: the left (group 174), the
        /// right (178), the top (273) or the bottom (272).
        /// </summary>
        /// <param name="leaderDirection">The side.</param>
        /// <returns>Where they meet: the middle of the top line for the sides, the middle for the top and the bottom, where the record gives none.</returns>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        public DxfTextAttachmentType GetTextAttachmentType(DxfLeaderDirectionType leaderDirection)
        {
            int code = SideCode(leaderDirection, nameof(leaderDirection));
            return (DxfTextAttachmentType)Whole(Text(code), code == 174 || code == 178 ? 1 : 9);
        }

        /// <summary>
        /// Sets where the leaders of a side meet the words, as AutoCAD's <c>SetTextAttachmentType</c>: the left (group 174), the
        /// right (178), the top (273) or the bottom (272).
        /// </summary>
        /// <param name="textAttachmentType">Where they meet: one of <see cref="DxfTextAttachmentType"/>.</param>
        /// <param name="leaderDirection">The side.</param>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the place is not one of <see cref="DxfTextAttachmentType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetTextAttachmentType(DxfTextAttachmentType textAttachmentType, DxfLeaderDirectionType leaderDirection)
        {
            int code = SideCode(leaderDirection, nameof(leaderDirection));

            if (textAttachmentType < DxfTextAttachmentType.AttachmentTopOfTop || textAttachmentType > DxfTextAttachmentType.AttachmentLinedCenter)
            {
                throw new ArgumentOutOfRangeException(nameof(textAttachmentType), textAttachmentType, "A leader meets the words at one of the places of DxfTextAttachmentType, 0 to 10.");
            }

            Set(code, ((int)textAttachmentType).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Gets or sets how the words are turned (group 175), as AutoCAD's <c>TextAngleType</c>; level where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfTextAngleType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfTextAngleType TextAngleType
        {
            get => (DxfTextAngleType)Whole(Text(175), 1);
            set => SetWhole(175, (int)value, 0, 2, "Words are turned along the leader (0), level (1) or right reading (2).");
        }

        /// <summary>Gets how the words are lined up (group 176), as AutoCAD's <c>TextAlignmentType</c>; on the left where the record gives none.</summary>
        public DxfTextAlignmentType TextAlignmentType => (DxfTextAlignmentType)Whole(Text(176), 0);

        /// <summary>
        /// Gets or sets the text style of the words (group 342), as AutoCAD's <c>TextStyleId</c> gives its id; null where the
        /// style names none, or one the drawing has no text style of. The style Standard of ezdxf names the text style Standard.
        /// </summary>
        /// <value>Set, a text style of the style's drawing, its handle written; null takes the group out.</value>
        /// <exception cref="ArgumentException">Thrown when the text style set is of no drawing, or of another than the style's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfTextStyleTableRecord TextStyle
        {
            get => RecordOf(Text(342)) as DxfTextStyleTableRecord;
            set => SetRecord(342, value, "text style");
        }

        /// <summary>Gets or sets the colour of the words (group 93), as AutoCAD's <c>TextColor</c>; BYBLOCK where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfColor TextColor
        {
            get => DxfMLeaderColors.Of(Text(93) ?? ByBlock);
            set => Set(93, DxfMLeaderColors.Text(value, nameof(value)));
        }

        /// <summary>Gets or sets how high the letters are (group 45), as AutoCAD's <c>TextHeight</c>; 4 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double TextHeight
        {
            get => Num(Text(45), 4.0);
            set
            {
                Guard.Positive(value, nameof(value), "A text height has to be a finite number above nought.");
                Set(45, DxfTable.AsReal(value));
            }
        }

        /// <summary>Gets or sets whether a frame is drawn round the words (group 292), as AutoCAD's <c>EnableFrameText</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool EnableFrameText
        {
            get => Whole(Text(292), 0) != 0;
            set => Set(292, value ? "1" : "0");
        }

        /// <summary>Gets or sets whether the words are lined up on the left whichever side the leader is on (group 297), as AutoCAD's <c>TextAlignAlwaysLeft</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool TextAlignAlwaysLeft
        {
            get => Whole(Text(297), 0) != 0;
            set => Set(297, value ? "1" : "0");
        }

        /// <summary>
        /// Gets or sets whether a leader runs on along the words to meet them where it lands short of them (group 298), as
        /// AutoCAD's <c>ExtendLeaderToText</c>; false where the record gives none. A file older than R2013 has no place for it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased, or set in a file older than R2013.</exception>
        public bool ExtendLeaderToText
        {
            get => Whole(Text(298), 0) != 0;
            set
            {
                if (Item != null)
                {
                    InFile("be changed").Requires(1027, "a leader extended to its text", "R2013 (AC1027)");
                }

                Set(298, value ? "1" : "0");
            }
        }

        /// <summary>
        /// Gets the block the leaders point to (group 343), as AutoCAD's <c>BlockId</c> gives its id; null where the style names
        /// none, or one the drawing has no block of.
        /// </summary>
        public DxfBlockTableRecord Block => BlockOf(Text(343));

        /// <summary>Gets the colour of the block (group 94), as AutoCAD's <c>BlockColor</c>; BYBLOCK where the record gives none.</summary>
        public DxfColor BlockColor => DxfMLeaderColors.Of(Text(94) ?? ByBlock);

        /// <summary>Gets how the block is scaled along its axes (groups 47, 49 and 140), as AutoCAD's <c>BlockScale</c>; 1 each way where the record gives none.</summary>
        public GeoVector3 BlockScale => new GeoVector3(Num(Text(47), 1.0), Num(Text(49), 1.0), Num(Text(140), 1.0));

        /// <summary>Gets whether the block is turned as <see cref="BlockRotation"/> says (group 294), as AutoCAD's <c>EnableBlockRotation</c>; true where the record gives none.</summary>
        public bool EnableBlockRotation => Whole(Text(294), 1) != 0;

        /// <summary>Gets how far the block is turned (group 141), as AutoCAD's <c>BlockRotation</c>, in radians; nought where the record gives none.</summary>
        public double BlockRotation => Num(Text(141), 0.0);

        /// <summary>Gets where the leaders meet the block (group 177), as AutoCAD's <c>BlockConnectionType</c>; at its box where the record gives none.</summary>
        public DxfBlockConnectionType BlockConnectionType => (DxfBlockConnectionType)Whole(Text(177), 0);

        /// <summary>
        /// Gets or sets the scale of the style (group 142), as AutoCAD's <c>Scale</c>; 1 where the record gives none, and 0
        /// for a style whose multileaders are scaled to the layout, as AutoCAD 2021 holds Scale multileaders to layout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double Scale
        {
            get => Num(Text(142), 1.0);
            set => SetSize(142, value);
        }

        /// <summary>Gets or sets the size of the gap a leader is broken by where it crosses another object (group 143), as AutoCAD's <c>BreakSize</c>; 3.75 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double BreakSize
        {
            get => Num(Text(143), 3.75);
            set => SetSize(143, value);
        }

        /// <summary>Gets or sets which sides of the words the leaders meet (group 271), as AutoCAD's <c>TextAttachmentDirection</c>; the left and the right where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction set is not one of <see cref="DxfTextAttachmentDirection"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfTextAttachmentDirection TextAttachmentDirection
        {
            get => (DxfTextAttachmentDirection)Whole(Text(271), 0);
            set => SetWhole(271, (int)value, 0, 1, "Leaders meet the words on the left and right (0) or the top and bottom (1).");
        }

        /// <summary>Gives the style as a line for debugging: its type and its handle.</summary>
        /// <returns>The line, such as <c>MLEADERSTYLE 2C</c>.</returns>
        public override string ToString() => "MLEADERSTYLE " + (Handle ?? string.Empty);

        /// <summary>The ByBlock colour as a MULTILEADER and its style pack it.</summary>
        private const string ByBlock = "-1056964608";

        /// <summary>The groups of the style after its subclass marker: those of its record, or those it was given in no file.</summary>
        internal List<KeyValuePair<int, string>> Groups()
        {
            if (Item == null)
            {
                return new List<KeyValuePair<int, string>>(_made);
            }

            IReadOnlyList<DxfTypedValue> own = Item.File.ObjectGroupsOf(Item);
            var groups = new List<KeyValuePair<int, string>>(own.Count);

            foreach (DxfTypedValue group in own)
            {
                if (group.TypeCode != 100)
                {
                    groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            return groups;
        }

        /// <summary>
        /// The groups a style made in code is written with in a drawing: every group AutoCAD 2021 holds of one, in its order, its
        /// own where it has them and the style Standard's where not (the extension of the leaders, 298, from R2013); a handle of
        /// a record the drawing has none of the kind of left out, and every handle where the style named those of another drawing.
        /// </summary>
        internal List<KeyValuePair<int, string>> MadeGroups(DxfDatabase database)
        {
            bool foreign = _naming != null && !ReferenceEquals(_naming, database);
            var groups = new List<KeyValuePair<int, string>>(Order.Length);

            foreach (int code in Order)
            {
                if (code == 298 && !database.File.Supports(1027))
                {
                    continue;
                }

                string value = DxfLeaderGroups.Text(_made, code);

                if (code == 340 || code == 341 || code == 342 || code == 343)
                {
                    if (!foreign && value != null && Fits(database, value.Trim(), code))
                    {
                        groups.Add(new KeyValuePair<int, string>(code, value.Trim()));
                    }

                    continue;
                }

                groups.Add(new KeyValuePair<int, string>(code, value ?? Standard(code)));
            }

            return groups;
        }

        /// <summary>Gives a style in no file the groups, the name and the drawing its blocks are read in.</summary>
        internal void Fill(List<KeyValuePair<int, string>> groups, string name, DxfDatabase naming)
        {
            _made.Clear();
            _made.AddRange(groups);
            _madeName = name;
            _naming = naming;
        }

        /// <summary>The text of the first group of a code; null where the style has none.</summary>
        internal string Text(int code) => DxfLeaderGroups.Text(Groups(), code);

        /// <summary>Sets the annotative flag of the style, its group 296, as <see cref="DxfDBObject.Annotative"/> sets it.</summary>
        internal void SetAnnotativeGroup(bool on) => Set(296, on ? "1" : "0");

        /// <summary>The value the style Standard of ezdxf, and AutoCAD 2021's new style, has for a group of no handle.</summary>
        private static string Standard(int code)
        {
            switch (code)
            {
                case 179: return "2";
                case 170: return "2";
                case 171: return "1";
                case 172: return "0";
                case 90: return "2";
                case 40: return DxfTable.AsReal(0.0);
                case 41: return DxfTable.AsReal(0.0);
                case 173: return "1";
                case 91: return ByBlock;
                case 92: return "-2";
                case 290: return "1";
                case 42: return DxfTable.AsReal(2.0);
                case 291: return "1";
                case 43: return DxfTable.AsReal(8.0);
                case 3: return string.Empty;
                case 44: return DxfTable.AsReal(4.0);
                case 300: return string.Empty;
                case 174: return "1";
                case 178: return "1";
                case 175: return "1";
                case 176: return "0";
                case 93: return ByBlock;
                case 45: return DxfTable.AsReal(4.0);
                case 292: return "0";
                case 297: return "0";
                case 46: return DxfTable.AsReal(4.0);
                case 94: return ByBlock;
                case 47: return DxfTable.AsReal(1.0);
                case 49: return DxfTable.AsReal(1.0);
                case 140: return DxfTable.AsReal(1.0);
                case 293: return "1";
                case 141: return DxfTable.AsReal(0.0);
                case 294: return "1";
                case 177: return "0";
                case 142: return DxfTable.AsReal(1.0);
                case 295: return "0";
                case 296: return "0";
                case 143: return DxfTable.AsReal(3.75);
                case 271: return "0";
                case 272: return "9";
                case 273: return "9";
                default: return "0";
            }
        }

        /// <summary>Whether a drawing has a record of the kind a handle group of the style names: a linetype (340), a block (341, 343), a text style (342).</summary>
        private static bool Fits(DxfDatabase database, string handle, int code)
        {
            if (handle.Length == 0 || handle == "0" || !database.TryGetObject(handle, out DxfDBObject found))
            {
                return false;
            }

            return code == 340 ? found is DxfLinetypeTableRecord : code == 342 ? found is DxfTextStyleTableRecord : found is DxfBlockTableRecord;
        }

        /// <summary>Writes a group: put in the record in its place, or among the groups of a style in no file; null takes it out.</summary>
        private void Set(int code, string value)
        {
            if (Item == null)
            {
                Put(_made, code, value);
                return;
            }

            InFile("be changed").SetValues(Item, new[] { new KeyValuePair<int, string>(code, value) }, RecordOrder, false);
        }

        private void SetWhole(int code, int value, int least, int most, string message)
        {
            if (value < least || value > most)
            {
                throw new ArgumentOutOfRangeException("value", value, message);
            }

            Set(code, value.ToString(CultureInfo.InvariantCulture));
        }

        private void SetSize(int code, double value)
        {
            Guard.NonNegative(value, "value", "A size of a multileader style has to be a finite number of nought or more.");
            Set(code, DxfTable.AsReal(value));
        }

        /// <summary>Writes the handle of a record of the style's drawing; a style in no file reads its records in the record's drawing from then on.</summary>
        private void SetRecord(int code, DxfDBObject record, string kind)
        {
            if (record == null)
            {
                Set(code, null);
                return;
            }

            DxfDatabase database = record.IsResident ? record.Item.File.Database : null;

            if (database == null || (Item != null && !ReferenceEquals(database, Naming())) || (Item == null && _naming != null && !ReferenceEquals(database, _naming)))
            {
                throw new ArgumentException("The " + kind + " of a multileader style is one of the style's own drawing.", "value");
            }

            if (Item == null)
            {
                _naming = database;
            }

            Set(code, record.Handle);
        }

        /// <summary>Puts a group among groups in the order of the record, the first of its code replaced; null takes it out.</summary>
        private static void Put(List<KeyValuePair<int, string>> groups, int code, string value)
        {
            int at = groups.FindIndex(g => g.Key == code);

            if (value == null)
            {
                if (at >= 0)
                {
                    groups.RemoveAt(at);
                }

                return;
            }

            if (at >= 0)
            {
                groups[at] = new KeyValuePair<int, string>(code, value);
                return;
            }

            int place = Array.IndexOf(Order, code);
            int before = groups.FindIndex(g => Array.IndexOf(Order, g.Key) > place);
            groups.Insert(before < 0 ? groups.Count : before, new KeyValuePair<int, string>(code, value));
        }

        /// <summary>The code of the attachment of a side: the left 174, the right 178, the top 273, the bottom 272.</summary>
        private static int SideCode(DxfLeaderDirectionType side, string parameter)
        {
            switch (side)
            {
                case DxfLeaderDirectionType.LeftLeader: return 174;
                case DxfLeaderDirectionType.RightLeader: return 178;
                case DxfLeaderDirectionType.TopLeader: return 273;
                case DxfLeaderDirectionType.BottomLeader: return 272;
                default: throw new ArgumentException("A side is the left, the right, the top or the bottom: " + side + " is none of them.", parameter);
            }
        }

        /// <summary>The angle a constraint is held as, in radians: its number of steps of 15 degrees.</summary>
        private static string Radians(DxfAngleConstraint constraint)
        {
            if (!Enum.IsDefined(typeof(DxfAngleConstraint), constraint))
            {
                throw new ArgumentOutOfRangeException("value", constraint, "An angle constraint is one of DxfAngleConstraint.");
            }

            return DxfTable.AsReal((int)constraint * Math.PI / 12.0);
        }

        /// <summary>The constraint of an angle held in radians: its number of steps of 15 degrees where it is one of AutoCAD's, else any.</summary>
        private static DxfAngleConstraint AngleOf(string text)
        {
            double steps = Num(text, 0.0) * 12.0 / Math.PI;
            int whole = (int)Math.Round(steps);
            return Math.Abs(steps - whole) <= 1E-6 && Enum.IsDefined(typeof(DxfAngleConstraint), whole) ? (DxfAngleConstraint)whole : DxfAngleConstraint.DegreesAny;
        }

        /// <summary>The block of a handle in the drawing the style reads its blocks in; null where there is none.</summary>
        private DxfBlockTableRecord BlockOf(string handle) => handle == null ? null : DxfMLeader.BlockByHandle(Naming(), handle.Trim());

        /// <summary>The object of a handle in the drawing the style reads its records in; null where there is none.</summary>
        private DxfDBObject RecordOf(string handle)
        {
            DxfDatabase database = Naming();
            handle = handle?.Trim();
            return handle != null && handle.Length > 0 && handle != "0" && database != null && database.TryGetObject(handle, out DxfDBObject found) ? found : null;
        }

        /// <summary>The drawing the style reads its blocks and records in: its own, or for one in no file the multileader's it was given the values of.</summary>
        private DxfDatabase Naming() => Item == null ? _naming : IsResident ? Item.File.Database : null;

        private static int[] Marked()
        {
            var marked = new int[Order.Length + 1];
            marked[0] = 100;
            Array.Copy(Order, 0, marked, 1, Order.Length);
            return marked;
        }
    }
}
