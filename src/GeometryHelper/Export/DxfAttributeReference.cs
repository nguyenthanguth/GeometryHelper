using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An ATTRIB of a DXF file, as AutoCAD's <c>AttributeReference</c>: the value an INSERT gives one of its block's
    /// attributes, a text of the INSERT's own named by its <see cref="Tag"/>, reached through
    /// <see cref="DxfBlockReference.AttributeCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The INSERT of the block BOLT whose attribute TAG1 reads M24 has one with <see cref="Tag"/> TAG1 and
    /// <see cref="DxfDBText.TextString"/> M24; every member of <see cref="DxfDBText"/> reads and sets its text as it does a
    /// TEXT's, its points in world coordinates. Setting one writes the ATTRIB's record in its place among the INSERT's, every
    /// other record keeping its bytes.
    /// </para>
    /// <para>
    /// The ATTRIB belongs to its INSERT, its <see cref="DxfEntity.Owner"/>: it is moved with it, erased with it, or erased
    /// alone, the INSERT's other ATTRIBs keeping their handles. One made in code, <c>new DxfAttributeReference(position,
    /// "M24", "TAG1", null)</c> or filled from its definition by
    /// <see cref="SetAttributeFromBlock(DxfAttributeDefinition, GeoTransform3)"/>, is added to an INSERT by
    /// <see cref="DxfAttributeCollection.AppendAttribute"/>, not to a block. Its groups are read as ezdxf reads them: those of
    /// the text from its part AcDbText, the tag, flags, field length and vertical mode (group 74) from its part AcDbAttribute.
    /// </para>
    /// </remarks>
    public sealed class DxfAttributeReference : DxfDBText
    {
        // The groups of an ATTRIB in the order AutoCAD writes them: AcDbText, then AcDbAttribute.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 11, 21, 31, 210, 220, 230, 100, 2, 70, 73, 74 });

        // The codes of the text's part of an attribute or its definition.
        private static readonly HashSet<int> TextCodes = new HashSet<int> { 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 11, 21, 31, 210, 220, 230 };

        /// <summary>
        /// Initializes an attribute with no tag or value at the origin, 2.5 high, as AutoCAD's <c>new AttributeReference()</c>,
        /// in no file; give it its tag, or fill it from its definition, before it is added to an INSERT.
        /// </summary>
        public DxfAttributeReference()
            : base("ATTRIB", Groups(new GeoPoint3(0.0, 0.0, 0.0), string.Empty, string.Empty, null, false))
        {
        }

        /// <summary>
        /// Initializes an attribute, as AutoCAD's <c>new AttributeReference(position, value, tag, style)</c>: at a point, with
        /// its value and tag, 2.5 high, in a text style, in no file until it is added to an INSERT.
        /// </summary>
        /// <param name="position">Where it stands, in world coordinates.</param>
        /// <param name="value">Its value, one line.</param>
        /// <param name="tag">Its tag, a word with no space in it.</param>
        /// <param name="style">Its text style, of the file it is to be added to; null for Standard.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value or tag is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, the value has a line break in
        /// it, or the tag is empty or has a space in it.</exception>
        public DxfAttributeReference(GeoPoint3 position, string value, string tag, DxfTextStyleTableRecord style)
            : base("ATTRIB", Groups(position, value, tag, style?.Name, true))
        {
        }

        internal DxfAttributeReference(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the tag of the attribute (group 2), as AutoCAD's <c>Tag</c>: the name its block's definition gives it, such as <c>TAG1</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set is empty or has a space or line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is erased.</exception>
        public string Tag
        {
            get => TextValue(2) ?? string.Empty;
            set => SetTag(value);
        }

        /// <summary>Gets or sets whether the attribute is not drawn (flag 1 of group 70), as AutoCAD's <c>Invisible</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is erased.</exception>
        public bool Invisible
        {
            get => Flag(1);
            set => SetFlag(1, value);
        }

        /// <summary>Gets whether the attribute's value is fixed by its definition (flag 2 of group 70), as AutoCAD's <c>IsConstant</c>.</summary>
        public bool IsConstant => Flag(2);

        /// <summary>Gets whether the attribute's value is checked when it is given (flag 4 of group 70), as AutoCAD's <c>IsVerifiable</c>.</summary>
        public bool IsVerifiable => Flag(4);

        /// <summary>Gets whether the attribute takes its default value without asking (flag 8 of group 70), as AutoCAD's <c>IsPreset</c>.</summary>
        public bool IsPreset => Flag(8);

        /// <summary>Gets or sets the length of the attribute's field (group 73), as AutoCAD's <c>FieldLength</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is erased.</exception>
        public int FieldLength
        {
            get => TextInt(73, 0);
            set => SetFieldLength(value);
        }

        /// <summary>
        /// Gets or sets whether the attribute keeps its place in the block when the block is edited (the group 280 after its tag),
        /// as AutoCAD's <c>LockPositionInBlock</c>; false where the record gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is erased, or set in a file older than R2007 (AC1021).</exception>
        public bool LockPositionInBlock
        {
            get => Locked;
            set => SetLocked(value);
        }

        /// <summary>Gets whether the attribute is a text of several lines, an MTEXT embedded in its record (group 101), as AutoCAD's <c>IsMTextAttribute</c>.</summary>
        public bool IsMTextAttribute => Embedded;

        /// <summary>
        /// Fills the attribute from its definition in a block, as AutoCAD's <c>SetAttributeFromBlock</c>: the definition's text
        /// (its words as the default value, its height, rotation, alignment, style and the rest), tag, flags, field length and
        /// lock, and its look, then placed by the transformation that places the block, an INSERT's
        /// <see cref="DxfBlockReference.BlockTransform"/>. Its prompt is not an attribute's and is left out.
        /// </summary>
        /// <param name="definition">The definition, of the block the INSERT places.</param>
        /// <param name="blockTransform">The transformation that places the block.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition or the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the text.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is erased.</exception>
        public void SetAttributeFromBlock(DxfAttributeDefinition definition, GeoTransform3 blockTransform)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (blockTransform == null)
            {
                throw new ArgumentNullException(nameof(blockTransform));
            }

            List<DxfTypedValue> theirs = Before101(definition.Item.File.OwnGroupsOf(definition.Item, 0));
            List<DxfTypedValue> own = Before101(GetOwnGroups(0));
            bool markers = own.Count > 0 && own[0].TypeCode == 100;
            var run = new List<KeyValuePair<int, string>>();
            int segment = theirs.Count > 0 && theirs[0].TypeCode == 100 ? -1 : 0;
            bool tagged = false;
            string locked = null;
            var attribute = new List<KeyValuePair<int, string>>();

            foreach (DxfTypedValue group in theirs)
            {
                if (group.TypeCode == 100)
                {
                    segment++;
                    continue;
                }

                bool inText = segment <= 0 && TextCodes.Contains(group.TypeCode);

                if (inText)
                {
                    run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
                else if (group.TypeCode == 2 || group.TypeCode == 70 || group.TypeCode == 73 || group.TypeCode == 74)
                {
                    tagged |= group.TypeCode == 2;
                    attribute.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
                else if (group.TypeCode == 280 && tagged && locked == null)
                {
                    locked = group.Value;
                }
            }

            if (markers)
            {
                run.Add(new KeyValuePair<int, string>(100, "AcDbAttribute"));
            }

            run.AddRange(attribute);

            if (locked != null && Item.File.Supports(1021))
            {
                run.Add(new KeyValuePair<int, string>(280, locked));
            }

            var codes = new HashSet<int>(TextCodes) { 2, 3, 70, 73, 74, 280 };

            if (markers)
            {
                for (int i = 1; i < own.Count; i++)
                {
                    codes.Add(own[i].TypeCode);
                }
            }

            // The attribute's embedded MTEXT (101 on) goes; the definition's, where it has one, takes its place, to be placed
            // with the rest by the block's placing.
            IReadOnlyList<DxfTypedValue> all = GetOwnGroups(0);

            for (int i = own.Count; i < all.Count; i++)
            {
                codes.Add(all[i].TypeCode);
            }

            IReadOnlyList<DxfTypedValue> defined = definition.Item.File.OwnGroupsOf(definition.Item, 0);

            if (markers)
            {
                for (int i = theirs.Count; i < defined.Count; i++)
                {
                    run.Add(new KeyValuePair<int, string>(defined[i].TypeCode, defined[i].Value));
                }
            }

            ReplaceGroups(0, codes, run.ToArray());
            SetPropertiesFrom(definition);
            TransformBy(blockTransform);
        }

        /// <summary>
        /// Places the attribute again from its definition, as AutoCAD's <c>SetAttributeFromBlock</c> of one transformation does:
        /// the definition of its tag in the block its INSERT places, by <see cref="SetAttributeFromBlock(DxfAttributeDefinition, GeoTransform3)"/>.
        /// </summary>
        /// <param name="blockTransform">The transformation that places the block.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is of no INSERT, or its block has no definition of its tag.</exception>
        public void SetAttributeFromBlock(GeoTransform3 blockTransform)
        {
            if (blockTransform == null)
            {
                throw new ArgumentNullException(nameof(blockTransform));
            }

            string tag = Tag;
            DxfAttributeDefinition definition = (FollowedEntity as DxfBlockReference)?.BlockTableRecord?
                .OfType<DxfAttributeDefinition>()
                .FirstOrDefault(d => string.Equals(d.Tag, tag, StringComparison.OrdinalIgnoreCase));

            if (definition == null)
            {
                throw new InvalidOperationException("The ATTRIB " + tag + " is of no INSERT whose block defines it.");
            }

            SetAttributeFromBlock(definition, blockTransform);
        }

        [DxfType("ATTRIB")]
        internal static DxfEntity CreateReference(DxfItem item) => new DxfAttributeReference(item);

        /// <summary>The order of an ATTRIB's groups, for its INSERT to write those of all its attributes at once.</summary>
        internal static IReadOnlyList<int> AttributeOrder => Order;

        /// <summary>Which record of its INSERT the attribute is; 0 for one of no INSERT.</summary>
        internal int Record => FollowerIndex;

        /// <summary>Refuses: an attribute is added to an INSERT by <see cref="DxfAttributeCollection.AppendAttribute"/>, not to a block.</summary>
        internal override void Appending(DxfBlockTableRecord owner)
            => throw new ArgumentException("An ATTRIB belongs to an INSERT: add it by the INSERT's AttributeCollection.AppendAttribute.", "entity");

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <inheritdoc/>
        private protected override bool IsAttribute => true;

        /// <inheritdoc/>
        private protected override int VerticalCode => 74;

        private static List<DxfTypedValue> Before101(IReadOnlyList<DxfTypedValue> groups)
        {
            var before = new List<DxfTypedValue>();

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 101)
                {
                    break;
                }

                before.Add(group);
            }

            return before;
        }

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position, string value, string tag, string style, bool check)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The value of an attribute is one line, with no line break in it.", nameof(value));
            }

            if (check)
            {
                CheckTag(tag, nameof(tag));
            }

            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbText"),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
                new DxfTypedValue(40, "2.5"),
                new DxfTypedValue(1, value),
            };

            if (style != null)
            {
                groups.Add(new DxfTypedValue(7, style));
            }

            groups.Add(new DxfTypedValue(100, "AcDbAttribute"));
            groups.Add(new DxfTypedValue(2, tag));
            groups.Add(new DxfTypedValue(70, "0"));
            return groups;
        }
    }
}
