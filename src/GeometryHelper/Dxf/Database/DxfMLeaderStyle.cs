using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A multileader style of a DXF file, as AutoCAD's <c>MLeaderStyle</c>: an MLEADERSTYLE of the dictionary
    /// ACAD_MLEADERSTYLE (<see cref="DxfDatabase.MLeaderStyleDictionary"/>), the look a <see cref="DxfMLeader"/> of the style
    /// takes where it overrides none of its own; read as the file holds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The style Standard of a drawing of ezdxf has words 4 high in its text style Standard, arrows 4 long, a landing gap of 2
    /// and doglegs 8 long, its leaders straight and BYBLOCK, the words met on the left and right at the middle of their top
    /// line; a group the record leaves out reads as that style has it.
    /// </para>
    /// <para>
    /// A style in no file, made by <c>new DxfMLeaderStyle()</c> as AutoCAD's <c>new MLeaderStyle()</c> is made, is given the
    /// values a multileader is drawn with by <see cref="DxfMLeader.GetOverridedMLeaderStyle"/>: its style's, and those the
    /// multileader overrides its own; its blocks are then those of the multileader's drawing. The style is read and not
    /// written: a dictionary does not take one made in code, and AutoCAD's own setters of a style are not part of the library.
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

        private readonly List<KeyValuePair<int, string>> _made = new List<KeyValuePair<int, string>>();
        private string _madeName;
        private DxfDatabase _naming;

        /// <summary>
        /// Initializes a multileader style in no file, of the values of the style Standard, as AutoCAD's
        /// <c>new MLeaderStyle()</c>, to be given those a multileader is drawn with by
        /// <see cref="DxfMLeader.GetOverridedMLeaderStyle"/>.
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

        /// <summary>Gets what the leaders of the style point to (group 170), as AutoCAD's <c>ContentType</c>; words where the record gives none.</summary>
        public DxfContentType ContentType => (DxfContentType)Whole(Text(170), 2);

        /// <summary>Gets how many points a leader line of the style is drawn through at most (group 90), as AutoCAD's <c>MaxLeaderSegmentsPoints</c>; 2 where the record gives none.</summary>
        public int MaxLeaderSegmentsPoints => Whole(Text(90), 2);

        /// <summary>Gets how the leader lines are drawn (group 173), as AutoCAD's <c>LeaderLineType</c>; straight where the record gives none.</summary>
        public DxfLeaderType LeaderLineType => (DxfLeaderType)Whole(Text(173), 1);

        /// <summary>Gets the colour of the leader lines (group 91), as AutoCAD's <c>LeaderLineColor</c>; BYBLOCK where the record gives none.</summary>
        public DxfColor LeaderLineColor => DxfMLeaderColors.Of(Text(91) ?? ByBlock);

        /// <summary>
        /// Gets the linetype of the leader lines (group 340), as AutoCAD's <c>LeaderLineTypeId</c> gives its id; null where the
        /// style names none, or one the drawing has no linetype of. The style Standard of ezdxf names none.
        /// </summary>
        public DxfLinetypeTableRecord LeaderLineLinetype => RecordOf(Text(340)) as DxfLinetypeTableRecord;

        /// <summary>Gets the lineweight of the leader lines (group 92), as AutoCAD's <c>LeaderLineWeight</c>; BYBLOCK where the record gives none.</summary>
        public DxfLineWeight LeaderLineWeight => (DxfLineWeight)Whole(Text(92), -2);

        /// <summary>Gets whether the leaders end in a landing (group 290), as AutoCAD's <c>EnableLanding</c>; true where the record gives none.</summary>
        public bool EnableLanding => Whole(Text(290), 1) != 0;

        /// <summary>Gets the gap between the landing and the words (group 42), as AutoCAD's <c>LandingGap</c>; 2 where the record gives none.</summary>
        public double LandingGap => Num(Text(42), 2.0);

        /// <summary>Gets whether the leaders have doglegs (group 291), as AutoCAD's <c>EnableDogleg</c>; true where the record gives none.</summary>
        public bool EnableDogleg => Whole(Text(291), 1) != 0;

        /// <summary>Gets how long the doglegs are (group 43), as AutoCAD's <c>DoglegLength</c>; 8 where the record gives none.</summary>
        public double DoglegLength => Num(Text(43), 8.0);

        /// <summary>
        /// Gets the block drawn as the arrows (group 341), as AutoCAD's <c>ArrowSymbolId</c> gives its id; null for AutoCAD's
        /// closed filled arrow, which needs no block, and where the drawing has no block of the handle.
        /// </summary>
        public DxfBlockTableRecord ArrowSymbol => BlockOf(Text(341));

        /// <summary>Gets the size of the arrows (group 44), as AutoCAD's <c>ArrowSize</c>; 4 where the record gives none.</summary>
        public double ArrowSize => Num(Text(44), 4.0);

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
            switch (leaderDirection)
            {
                case DxfLeaderDirectionType.LeftLeader: return (DxfTextAttachmentType)Whole(Text(174), 1);
                case DxfLeaderDirectionType.RightLeader: return (DxfTextAttachmentType)Whole(Text(178), 1);
                case DxfLeaderDirectionType.TopLeader: return (DxfTextAttachmentType)Whole(Text(273), 9);
                case DxfLeaderDirectionType.BottomLeader: return (DxfTextAttachmentType)Whole(Text(272), 9);
                default: throw new ArgumentException("A side is the left, the right, the top or the bottom: " + leaderDirection + " is none of them.", nameof(leaderDirection));
            }
        }

        /// <summary>Gets how the words are turned (group 175), as AutoCAD's <c>TextAngleType</c>; level where the record gives none.</summary>
        public DxfTextAngleType TextAngleType => (DxfTextAngleType)Whole(Text(175), 1);

        /// <summary>Gets how the words are lined up (group 176), as AutoCAD's <c>TextAlignmentType</c>; on the left where the record gives none.</summary>
        public DxfTextAlignmentType TextAlignmentType => (DxfTextAlignmentType)Whole(Text(176), 0);

        /// <summary>
        /// Gets the text style of the words (group 342), as AutoCAD's <c>TextStyleId</c> gives its id; null where the style names
        /// none, or one the drawing has no text style of. The style Standard of ezdxf names the text style Standard.
        /// </summary>
        public DxfTextStyleTableRecord TextStyle => RecordOf(Text(342)) as DxfTextStyleTableRecord;

        /// <summary>Gets the colour of the words (group 93), as AutoCAD's <c>TextColor</c>; BYBLOCK where the record gives none.</summary>
        public DxfColor TextColor => DxfMLeaderColors.Of(Text(93) ?? ByBlock);

        /// <summary>Gets how high the letters are (group 45), as AutoCAD's <c>TextHeight</c>; 4 where the record gives none.</summary>
        public double TextHeight => Num(Text(45), 4.0);

        /// <summary>Gets whether a frame is drawn round the words (group 292), as AutoCAD's <c>EnableFrameText</c>.</summary>
        public bool EnableFrameText => Whole(Text(292), 0) != 0;

        /// <summary>Gets whether the words are lined up on the left whichever side the leader is on (group 297), as AutoCAD's <c>TextAlignAlwaysLeft</c>.</summary>
        public bool TextAlignAlwaysLeft => Whole(Text(297), 0) != 0;

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

        /// <summary>Gets the scale of the style (group 142), as AutoCAD's <c>Scale</c>; 1 where the record gives none.</summary>
        public double Scale => Num(Text(142), 1.0);

        /// <summary>Gets the size of the gap a leader is broken by where it crosses another object (group 143), as AutoCAD's <c>BreakSize</c>; 3.75 where the record gives none.</summary>
        public double BreakSize => Num(Text(143), 3.75);

        /// <summary>Gets which sides of the words the leaders meet (group 271), as AutoCAD's <c>TextAttachmentDirection</c>; the left and the right where the record gives none.</summary>
        public DxfTextAttachmentDirection TextAttachmentDirection => (DxfTextAttachmentDirection)Whole(Text(271), 0);

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
    }
}
