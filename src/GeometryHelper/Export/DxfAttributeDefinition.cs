using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An ATTDEF of a DXF file, as AutoCAD's <c>AttributeDefinition</c>: an attribute of a block, the text an INSERT of the
    /// block is asked to give, named by its <see cref="Tag"/>, with its <see cref="Prompt"/> and default value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block BOLT whose ATTDEF has the tag TAG1, the prompt Size and the value M20 asks each INSERT of it for a size, M20
    /// unless it is given another; <see cref="DxfDBText.TextString"/> is that default, and every member of
    /// <see cref="DxfDBText"/> reads and sets its text as it does a TEXT's.
    /// </para>
    /// <para>
    /// Its groups are read as ezdxf reads them: those of the text from its part AcDbText, the prompt, tag, flags, field length
    /// and vertical mode (group 74) from its part AcDbAttributeDefinition. <c>new DxfAttributeDefinition(position, "M20",
    /// "TAG1", "Size", null)</c> makes one in code, to be added to a block by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfAttributeDefinition : DxfDBText
    {
        // The groups of an ATTDEF in the order AutoCAD writes them: AcDbText, then AcDbAttributeDefinition.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 11, 21, 31, 210, 220, 230, 100, 3, 2, 70, 73, 74 });

        /// <summary>
        /// Initializes an attribute definition with no tag, prompt or value at the origin, 2.5 high, as AutoCAD's
        /// <c>new AttributeDefinition()</c>, in no file; give it its tag before it is added to a block.
        /// </summary>
        public DxfAttributeDefinition()
            : this(new GeoPoint3(0.0, 0.0, 0.0), string.Empty, string.Empty, string.Empty, null, false)
        {
        }

        /// <summary>
        /// Initializes an attribute definition, as AutoCAD's <c>new AttributeDefinition(position, value, tag, prompt, style)</c>:
        /// at a point, with its default value, tag and prompt, 2.5 high, in a text style, in no file until it is added to a block.
        /// </summary>
        /// <param name="position">Where it stands, in world coordinates.</param>
        /// <param name="value">Its default value, one line.</param>
        /// <param name="tag">Its tag, a word with no space in it.</param>
        /// <param name="prompt">What an INSERT of the block is asked; empty for none.</param>
        /// <param name="style">Its text style, of the file it is to be added to; null for Standard.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value, tag or prompt is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, the value or prompt has a line
        /// break in it, or the tag is empty or has a space in it.</exception>
        public DxfAttributeDefinition(GeoPoint3 position, string value, string tag, string prompt, DxfTextStyleTableRecord style)
            : this(position, value, tag, prompt, style?.Name, true)
        {
        }

        private DxfAttributeDefinition(GeoPoint3 position, string value, string tag, string prompt, string style, bool check)
            : base("ATTDEF", Groups(position, value, tag, prompt, style, check))
        {
        }

        private DxfAttributeDefinition(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the tag of the attribute (group 2), as AutoCAD's <c>Tag</c>: the name an INSERT's ATTRIB of it has, such as <c>TAG1</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the tag set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tag set is empty or has a space or line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public string Tag
        {
            get => TextValue(2) ?? string.Empty;
            set => SetTag(value);
        }

        /// <summary>Gets or sets what an INSERT of the block is asked for the attribute (group 3), as AutoCAD's <c>Prompt</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the prompt set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the prompt set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public string Prompt
        {
            get => Unescaped(TextValue(3) ?? string.Empty);

            set
            {
                if (value != null && string.Equals(value, Prompt, StringComparison.Ordinal))
                {
                    return;
                }

                CheckLine(value, nameof(value));
                SetText(Pair(3, value));
            }
        }

        /// <summary>Gets or sets whether the attribute is not drawn (flag 1 of group 70), as AutoCAD's <c>Invisible</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public bool Invisible
        {
            get => Flag(1);
            set => SetFlag(1, value);
        }

        /// <summary>Gets or sets whether the attribute's value is fixed, the same in every INSERT (flag 2 of group 70), as AutoCAD's <c>Constant</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public bool Constant
        {
            get => Flag(2);
            set => SetFlag(2, value);
        }

        /// <summary>Gets or sets whether the attribute's value is checked when it is given (flag 4 of group 70), as AutoCAD's <c>Verifiable</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public bool Verifiable
        {
            get => Flag(4);
            set => SetFlag(4, value);
        }

        /// <summary>Gets or sets whether the attribute takes its default value without asking (flag 8 of group 70), as AutoCAD's <c>Preset</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public bool Preset
        {
            get => Flag(8);
            set => SetFlag(8, value);
        }

        /// <summary>Gets or sets the length of the attribute's field (group 73), as AutoCAD's <c>FieldLength</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public int FieldLength
        {
            get => TextInt(73, 0);
            set => SetFieldLength(value);
        }

        /// <summary>
        /// Gets or sets whether the attribute keeps its place in the block when the block is edited (the group 280 after its tag),
        /// as AutoCAD's <c>LockPositionInBlock</c>; false where the record gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased, or set in a file older than R2007 (AC1021).</exception>
        public bool LockPositionInBlock
        {
            get => Locked;
            set => SetLocked(value);
        }

        /// <summary>Gets or sets whether the attribute is a text of several lines, an MTEXT embedded in its record (group 101), as AutoCAD's <c>IsMTextAttributeDefinition</c>.</summary>
        /// <value>
        /// Set true, an MTEXT made of its default value embedded where its text stands, as
        /// <see cref="MTextAttributeDefinition"/> embeds one, its backslashes and braces written as the codes <c>\\</c>,
        /// <c>\{</c> and <c>\}</c>. Set false, the MTEXT taken out, the text keeping its first line, as an attribute of one line
        /// (71 of 1).
        /// </value>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased, or set true in a file older than R2018 (AC1032).</exception>
        public bool IsMTextAttributeDefinition
        {
            get => Embedded;
            set => SetSeveral(value);
        }

        /// <summary>
        /// Gets or sets the MTEXT the definition is made of where it is of several lines, as AutoCAD's
        /// <c>MTextAttributeDefinition</c>: as <see cref="DxfAttributeReference.MTextAttribute"/> gets and sets one, its type
        /// (71) 4, as ezdxf writes the definition of an attribute of several lines.
        /// </summary>
        /// <value>Get, a new MTEXT in no file with the definition's look; null for a definition of one line. Set, embedded and
        /// the text made to agree; the MTEXT it is made of, given back, changes nothing, and so does null given to a definition of
        /// one line.</value>
        /// <exception cref="ArgumentNullException">Thrown when null is set on a definition of several lines.</exception>
        /// <exception cref="ArgumentException">Thrown when the MTEXT's text style is not in the file's text style table.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased, or set in a file older than R2018 (AC1032).</exception>
        public DxfMText MTextAttributeDefinition
        {
            get => MTextOf();
            set => SetMText(value);
        }

        /// <summary>
        /// Places the definition's MTEXT where its text stands, as AutoCAD's <c>UpdateMTextAttributeDefinition</c>, as
        /// <see cref="DxfAttributeReference.UpdateMTextAttribute"/> places an attribute's; nothing for a definition of one line.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public void UpdateMTextAttributeDefinition() => UpdateMText();

        [DxfType("ATTDEF")]
        internal static DxfEntity CreateDefinition(DxfItem item) => new DxfAttributeDefinition(item);

        /// <summary>Refuses a definition with no tag, which AutoCAD and ezdxf do not keep, or with a text style the file has not.</summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (Tag.Length == 0)
            {
                throw new ArgumentException("An ATTDEF is named by its tag: give it one before it is added.", "entity");
            }

            base.Appending(owner);
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <inheritdoc/>
        private protected override bool IsAttribute => true;

        /// <inheritdoc/>
        private protected override int VerticalCode => 74;

        /// <inheritdoc/>
        private protected override int SeveralLines => 4;

        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 position, string value, string tag, string prompt, string style, bool check)
        {
            if (!position.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + position + " is not.", nameof(position));
            }

            CheckLine(value, nameof(value));
            CheckLine(prompt, nameof(prompt));

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

            groups.Add(new DxfTypedValue(100, "AcDbAttributeDefinition"));
            groups.Add(new DxfTypedValue(3, prompt));
            groups.Add(new DxfTypedValue(2, tag));
            groups.Add(new DxfTypedValue(70, "0"));
            return groups;
        }

        private static void CheckLine(string value, string parameter)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The words are one line, with no line break in them.", parameter);
            }
        }
    }
}
