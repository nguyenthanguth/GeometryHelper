using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A TEXT of a DXF file, as AutoCAD's <c>DBText</c>: one line of words at <see cref="Position"/>, <see cref="Height"/>
    /// high, turned by <see cref="Rotation"/> in the plane of its <see cref="Normal"/>, held by its point as
    /// <see cref="HorizontalMode"/> and <see cref="VerticalMode"/> say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text "Left base 30" at (2, 3, 0), 2.5 high, turned by 30 degrees, is <see cref="Rotation"/> 0.5236 radians,
    /// <see cref="Justify"/> <see cref="DxfAttachmentPoint.BaseLeft"/> and <see cref="IsDefaultAlignment"/>. Its points are
    /// given in world coordinates, as AutoCAD gives them, though the file writes them in the system of the normal's plane: a
    /// text on the normal (0, 0, -1) whose file says (3, 4, 5) stands at (-3, 4, -5).
    /// </para>
    /// <para>
    /// A property set writes its groups as <see cref="DxfWriter"/> writes a number, the angles in degrees as the file holds
    /// them, every other group of the record kept as it was; a property given the value it has changes nothing.
    /// <c>new DxfDBText { TextString = "B-12", Position = new GeoPoint3(10, 20, 0), Height = 3.5 }</c> makes one in code, to be
    /// added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>. <see cref="DxfEntity.GetGeometry"/>
    /// gives the <see cref="DxfText"/> <see cref="DxfReader"/> reads of it.
    /// </para>
    /// </remarks>
    public class DxfDBText : DxfEntity
    {
        // The groups of a TEXT in the order AutoCAD writes them: AcDbText twice, the second holding the vertical mode.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 11, 21, 31, 210, 220, 230, 100, 73 });

        // The groups of the MTEXT an ATTRIB or ATTDEF embeds (101 on), in the order ezdxf writes them.
        private static readonly int[] EmbeddedOrder = { 101, 10, 20, 30, 40, 41, 46, 71, 72, 3, 1, 7, 210, 220, 230, 11, 21, 31, 42, 43, 50, 73, 44 };

        // The codes an ATTRIB or ATTDEF holds in its second subclass, AcDbAttribute or AcDbAttributeDefinition.
        private static readonly HashSet<int> AttributeCodes = new HashSet<int> { 2, 3, 70, 73, 74, 280 };

        // AutoCAD's justification of a text, by its groups 72 and 73 (74 of an attribute).
        private static readonly int[,] Justifications =
        {
            { 10, 7, 4, 1 },
            { 11, 8, 5, 2 },
            { 12, 9, 6, 3 },
            { 13, 14, 15, 16 },
            { 21, 22, 23, 24 },
            { 17, 18, 19, 20 },
        };

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a text with no words at the origin, 2.5 high, as AutoCAD's <c>new DBText()</c>, on layer 0 in the text
        /// style Standard, in no file; give it its words and place before it is drawn.
        /// </summary>
        public DxfDBText()
            : base("TEXT", new[]
            {
                new DxfTypedValue(100, "AcDbText"), new DxfTypedValue(10, "0"), new DxfTypedValue(20, "0"), new DxfTypedValue(30, "0"),
                new DxfTypedValue(40, "2.5"), new DxfTypedValue(1, string.Empty), new DxfTypedValue(100, "AcDbText"),
            })
        {
        }

        /// <summary>The object of a record of a file.</summary>
        private protected DxfDBText(DxfItem item)
            : base(item)
        {
        }

        /// <summary>A text made in code from its groups, as <see cref="DxfEntity"/> makes one.</summary>
        private protected DxfDBText(string dxfName, IEnumerable<DxfTypedValue> groups)
            : base(dxfName, groups)
        {
        }

        /// <summary>
        /// Gets or sets the words of the text (group 1), as AutoCAD's <c>TextString</c>: as the file writes them, its special
        /// characters such as <c>%%d</c> kept, but a letter written as its escape, <c>\U+00E9</c>, read as the letter; empty
        /// where the record gives none.
        /// </summary>
        /// <value>Set, one line of words; a letter the file's code page lacks is written as its escape.</value>
        /// <exception cref="ArgumentNullException">Thrown when the words set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the words set have a line break in them.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public string TextString
        {
            get => Unescaped(TextValue(1) ?? string.Empty);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                // The words it has, a line break read from an escape among them, change nothing.
                if (string.Equals(value, TextString, StringComparison.Ordinal))
                {
                    return;
                }

                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("A text is one line: its words have no line break in them.", nameof(value));
                }

                SetText(Pair(1, value));
            }
        }

        /// <summary>
        /// Gets the words of the text with the codes of its fields in their places, as AutoCAD's <c>getTextWithFieldCodes</c>:
        /// the code of its field (<see cref="DxfDBObject.GetField()"/>), each field it holds standing as its code in its markers,
        /// at every depth. The TEXT that reads Drawn 27/10/2026, the date a field, gives
        /// <c>Drawn %&lt;\AcVar Date \f "dd/MM/yyyy"&gt;%</c>; one with no field gives its <see cref="TextString"/>.
        /// </summary>
        /// <returns>The words with the codes of their fields.</returns>
        public string GetTextWithFieldCodes()
        {
            DxfField field = GetField();
            return field == null ? TextString : DxfField.WithCodes(field);
        }

        /// <summary>
        /// Makes the text's fields words, as AutoCAD's <c>ConvertFieldToText</c>: the field of its words taken out with its
        /// children, as <see cref="DxfDBObject.RemoveField()"/> takes it out, and its words (1) kept as they are, the values AutoCAD
        /// last gave the fields among them. Nothing where it has no field.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public void ConvertFieldToText() => RemoveField();

        /// <summary>
        /// Gets or sets where the text stands (groups 10, 20, 30), as AutoCAD's <c>Position</c>: in world coordinates, the file's
        /// point taken from the system of the <see cref="Normal"/>'s plane; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public GeoPoint3 Position
        {
            get => OcsToWorld(Normal, TextPoint(10) ?? Origin);

            set
            {
                CheckPoint(value);

                if (!SamePoint(value, Position))
                {
                    SetText(TextPointGroups(10, WorldToOcs(Normal, value)));
                }
            }
        }

        /// <summary>
        /// Gets or sets the second point of the text (groups 11, 21, 31), as AutoCAD's <c>AlignmentPoint</c>: in world
        /// coordinates, the point a text not aligned left on its baseline is held by; the origin where the record gives none,
        /// as for such a text.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public GeoPoint3 AlignmentPoint
        {
            get => OcsToWorld(Normal, TextPoint(11) ?? Origin);

            set
            {
                CheckPoint(value);

                if (!SamePoint(value, AlignmentPoint))
                {
                    SetText(TextPointGroups(11, WorldToOcs(Normal, value)));
                }
            }
        }

        /// <summary>Gets or sets how high the text is (group 40), as AutoCAD's <c>Height</c>; 2.5 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public double Height
        {
            get => TextDouble(40, 2.5);

            set
            {
                if (!Same(value, Height))
                {
                    Guard.Positive(value, nameof(value), "The height of a text has to be a finite number above nought.");
                    SetText(NumberGroup(40, value, Height));
                }
            }
        }

        /// <summary>
        /// Gets or sets how far the text is turned (group 50, in degrees), as AutoCAD's <c>Rotation</c>: in radians,
        /// counter-clockwise from the x axis of the system of its normal's plane; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public double Rotation
        {
            get => TextDouble(50, 0.0) * (Math.PI / 180.0);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, Rotation))
                {
                    SetText(Pair(50, Numbers.Exact(DxfOcsTransform.Degrees(value))));
                }
            }
        }

        /// <summary>Gets or sets how wide the letters are against their height (group 41), as AutoCAD's <c>WidthFactor</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public double WidthFactor
        {
            get => TextDouble(41, 1.0);

            set
            {
                if (!Same(value, WidthFactor))
                {
                    Guard.Positive(value, nameof(value), "A width factor has to be a finite number above nought.");
                    SetText(NumberGroup(41, value, WidthFactor));
                }
            }
        }

        /// <summary>
        /// Gets or sets how far the letters lean (group 51, in degrees), as AutoCAD's <c>Oblique</c>: in radians, clockwise
        /// from upright; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public double Oblique
        {
            get => TextDouble(51, 0.0) * (Math.PI / 180.0);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, Oblique))
                {
                    SetText(Pair(51, Numbers.Exact(DxfOcsTransform.Degrees(value))));
                }
            }
        }

        /// <summary>
        /// Gets or sets how the text stands across its point (group 72), as AutoCAD's <c>HorizontalMode</c>;
        /// <see cref="DxfTextHorizontalMode.TextLeft"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the mode set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public DxfTextHorizontalMode HorizontalMode
        {
            get => (DxfTextHorizontalMode)TextInt(72, 0);

            set
            {
                if (value == HorizontalMode)
                {
                    return;
                }

                if (value < DxfTextHorizontalMode.TextLeft || value > DxfTextHorizontalMode.TextFit)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A horizontal mode is one of AutoCAD's, 0 to 5.");
                }

                SetText(WholeGroup(72, (int)value, (int)HorizontalMode));
            }
        }

        /// <summary>
        /// Gets or sets how the text stands up and down from its point (group 73 of a TEXT, 74 of an ATTRIB or ATTDEF), as
        /// AutoCAD's <c>VerticalMode</c>; <see cref="DxfTextVerticalMode.TextBase"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the mode set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public DxfTextVerticalMode VerticalMode
        {
            get => (DxfTextVerticalMode)TextInt(VerticalCode, 0);

            set
            {
                if (value == VerticalMode)
                {
                    return;
                }

                if (value < DxfTextVerticalMode.TextBase || value > DxfTextVerticalMode.TextTop)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A vertical mode is one of AutoCAD's, 0 to 3.");
                }

                SetText(WholeGroup(VerticalCode, (int)value, (int)VerticalMode));
            }
        }

        /// <summary>
        /// Gets or sets the justification of the text, as AutoCAD's <c>Justify</c>: its <see cref="HorizontalMode"/> and
        /// <see cref="VerticalMode"/> taken together, a text centred on its middle <see cref="DxfAttachmentPoint.MiddleCenter"/>;
        /// <see cref="DxfAttachmentPoint.BaseLeft"/> where the record gives neither.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the justification set is not one a text has, 1 to 24.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public DxfAttachmentPoint Justify
        {
            get
            {
                int horizontal = TextInt(72, 0);
                int vertical = TextInt(VerticalCode, 0);
                return horizontal >= 0 && horizontal <= 5 && vertical >= 0 && vertical <= 3 ? (DxfAttachmentPoint)Justifications[horizontal, vertical] : DxfAttachmentPoint.BaseLeft;
            }

            set
            {
                // The justification it reads as changes nothing, a pair of modes AutoCAD has none for among them.
                if (value == Justify)
                {
                    return;
                }

                for (int horizontal = 0; horizontal <= 5; horizontal++)
                {
                    for (int vertical = 0; vertical <= 3; vertical++)
                    {
                        if (Justifications[horizontal, vertical] == (int)value)
                        {
                            SetText(Join(WholeGroup(72, horizontal, TextInt(72, 0)), WholeGroup(VerticalCode, vertical, TextInt(VerticalCode, 0))));
                            return;
                        }
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(value), value, "A text is justified by one of AutoCAD's points, 1 to 24.");
            }
        }

        /// <summary>Gets whether the text is aligned left on its baseline, its modes both nought, as AutoCAD's <c>IsDefaultAlignment</c>.</summary>
        public bool IsDefaultAlignment => TextInt(72, 0) == 0 && TextInt(VerticalCode, 0) == 0;

        /// <summary>Gets or sets whether the text is written backwards (flag 2 of group 71), as AutoCAD's <c>IsMirroredInX</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public bool IsMirroredInX
        {
            get => (TextInt(71, 0) & 2) != 0;
            set => SetText(WholeGroup(71, value ? TextInt(71, 0) | 2 : TextInt(71, 0) & ~2, TextInt(71, 0)));
        }

        /// <summary>Gets or sets whether the text is written upside down (flag 4 of group 71), as AutoCAD's <c>IsMirroredInY</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public bool IsMirroredInY
        {
            get => (TextInt(71, 0) & 4) != 0;
            set => SetText(WholeGroup(71, value ? TextInt(71, 0) | 4 : TextInt(71, 0) & ~4, TextInt(71, 0)));
        }

        /// <summary>
        /// Gets or sets the name of the text style of the text (group 7), as AutoCAD's <c>TextStyleName</c>: as the style table
        /// names it, its escapes read as letters; <c>Standard</c> where the record gives none.
        /// </summary>
        /// <value>Set, the name of a style of the drawing's text style table, letters of either case the same, written as the
        /// table names it; any name for a text in no drawing, which <see cref="DxfBlockTableRecord.AppendEntity"/> checks. The
        /// style the text has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take, or the drawing has no text style
        /// of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public string TextStyleName
        {
            get
            {
                string name = DxfReader.NameOf(TextValue(7) ?? string.Empty);
                return name.Length == 0 ? "Standard" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, TextStyleName, StringComparison.OrdinalIgnoreCase))
                {
                    SetText(Pair(7, TextStyleToWrite(value, nameof(value))));
                }
            }
        }

        /// <summary>
        /// Gets or sets the text style of the text, as AutoCAD's <c>TextStyleId</c> gives its id: the record of
        /// <see cref="TextStyleName"/> in the text style table of the text's drawing; null where it has none of that name, or the
        /// text is in no drawing and was made in code. <c>text.TextStyle = db.TextStyleTable["GH_TXT"]</c> writes 7 GH_TXT.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is erased, in no drawing, or of another drawing than the
        /// text's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public DxfTextStyleTableRecord TextStyle
        {
            get => TextStyleNamed(TextStyleName);
            set => TextStyleName = TextStyleOf(value, nameof(value));
        }

        /// <summary>
        /// Gets or sets the normal of the plane the text stands in (groups 210, 220, 230), as AutoCAD's <c>Normal</c>; of unit
        /// length, (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length; the text's two points stay where they are in
        /// the world, written anew in the system of the new plane.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public GeoVector3 Normal
        {
            get => TextNormal();

            set
            {
                double length = value.Length;

                if (!value.IsValid || !(length > 0.0) || double.IsInfinity(length))
                {
                    throw new ArgumentException("A normal has to be a direction of finite numbers and some length: " + value + " is not.", nameof(value));
                }

                GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
                GeoVector3 now = Normal;

                if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
                {
                    return;
                }

                GeoPoint3 position = Position;
                bool aligned = TextPoint(11) != null;
                GeoPoint3 alignment = AlignmentPoint;
                SetText(Join(
                    TextPointGroups(10, WorldToOcs(unit, position)),
                    aligned ? TextPointGroups(11, WorldToOcs(unit, alignment)) : new KeyValuePair<int, string>[0],
                    TextNormalGroups(unit)));
            }
        }

        /// <summary>
        /// Gets or sets how far the text stands up along its <see cref="Normal"/> (group 39), as AutoCAD's <c>Thickness</c>;
        /// nought where the record gives none.
        /// </summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        public double Thickness
        {
            get => GetThickness();

            set
            {
                if (!IsAttribute)
                {
                    SetThickness(value);
                    return;
                }

                Guard.Finite(value, nameof(value), "A thickness has to be a finite number.");

                if (!Same(value, Thickness))
                {
                    SetText(Pair(39, value == 0.0 ? null : Numbers.Exact(value + 0.0)));
                }
            }
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the text, as AutoCAD's <c>TransformBy</c> does, and as ezdxf's <c>transform()</c>
        /// carries a text: its points carried through the system of its plane, its rotation turned, its height and width factor
        /// stretched as its upright and its baseline are, its lean changed where the plane is scaled unlike along its two axes,
        /// its thickness and normal carried. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the plane of the text, or shears an attribute
        /// of several lines, whose MTEXT cannot lean.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // The identity leaves the text as it is: no angle is taken through degrees and back.
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            KeyValuePair<int, string>[] groups = TransformGroups(transform);
            List<DxfTypedValue> embedded = IsAttribute ? EmbeddedMoved(transform) : null;

            if (embedded != null)
            {
                Rewrite(groups, null, embedded);
            }
            else
            {
                SetText(groups);
            }
        }

        [DxfType("TEXT")]
        internal static DxfEntity Create(DxfItem item) => new DxfDBText(item);

        /// <summary>
        /// The groups a transformation gives the text, for its INSERT to write with those of its other attributes at once; none
        /// for the identity; null where one of them would land in the wrong part of the record, which <see cref="DxfEntity.TransformBy(GeoTransform3)"/>
        /// then writes alone.
        /// </summary>
        internal KeyValuePair<int, string>[] CarriedGroups(GeoTransform3 transform)
        {
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return new KeyValuePair<int, string>[0];
            }

            KeyValuePair<int, string>[] groups = TransformGroups(transform);
            return IsAttribute && (Embedded || Misplaced(groups)) ? null : groups;
        }

        /// <summary>
        /// Refuses a text style the file of the block has not, unless the entity is a copy of another drawing that has it, which
        /// is copied with it; and leaves out the embedded MTEXT (101) of an attribute added to
        /// a drawing older than R2018, which has no such object, its text's part keeping the words of its group 1.
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (owner.File != null && !owner.File.Supports(1032))
            {
                DropEmbedded();
            }

            CheckTextStyle(owner.File, Item.File.Origin, TextValue(7), "entity");
        }

        /// <summary>The text style the record names (group 7), as the file writes it; null where it names none.</summary>
        internal string WrittenTextStyle => TextValue(7);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>Whether the record is an ATTRIB or ATTDEF, whose own groups after the text's are those of the attribute.</summary>
        private protected virtual bool IsAttribute => false;

        /// <summary>The code of the vertical mode: 73 on a TEXT, 74 on an ATTRIB or ATTDEF, where 73 is the field length.</summary>
        private protected virtual int VerticalCode => 73;

        /// <summary>
        /// The text of the first group of a code in the part of the record a text's groups are in: the type's own part of a TEXT;
        /// of an ATTRIB or ATTDEF, its AcDbText part for a text's code and the part after it for an attribute's (2, 3, 70, 73,
        /// 74 and 280), as ezdxf reads them; the whole record in a file of R12. Null where it has none.
        /// </summary>
        private protected string TextValue(int code)
        {
            if (!IsAttribute)
            {
                return GetString(code);
            }

            IReadOnlyList<DxfTypedValue> own = GetOwnGroups(0);
            bool later = AttributeCodes.Contains(code);
            int segment = -1;

            foreach (DxfTypedValue group in own)
            {
                // An embedded MTEXT (101) repeats the groups of a text: they are its own.
                if (group.TypeCode == 101)
                {
                    break;
                }

                if (group.TypeCode == 100)
                {
                    segment++;
                    continue;
                }

                if (segment >= 0 && (later ? segment == 0 : segment > 0))
                {
                    continue;
                }

                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>The number of a group of the text's part, as the reader reads it; the fallback where it has none.</summary>
        private protected double TextDouble(int code, double fallback) => Numbers.TryRead(TextValue(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>The whole number of a group of the text's part; the fallback where it has none.</summary>
        private protected int TextInt(int code, int fallback) => int.TryParse(TextValue(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>Sets groups of the text's part in one writing of the record, as <see cref="SetText(KeyValuePair{int, string}[])"/> does.</summary>
        private protected void SetText(params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length == 0)
            {
                return;
            }

            if (!IsAttribute || !Misplaced(groups))
            {
                SetGroups(groups);
                return;
            }

            Rewrite(groups, null);
        }

        /// <summary>
        /// Writes the own part of an ATTRIB or ATTDEF anew in one writing: the groups given set in the part their code belongs
        /// to, and the lock of its position (the 280 after the tag) set where one is given; every other group kept as it was.
        /// For a record whose text's part lacks a code the attribute's part has, as ezdxf's of R2018 repeats 71, 72 and 11.
        /// Given a type, the attribute's part has it as its 71 (1 one line, 2 an ATTRIB of several, 4 an ATTDEF of several),
        /// and a 72 of nought after it where it has none, as AutoCAD and ezdxf write them from R2018.
        /// </summary>
        private protected void Rewrite(KeyValuePair<int, string>[] groups, string locked, IReadOnlyList<DxfTypedValue> newEmbedded = null, int? type = null)
        {
            List<DxfTypedValue> own = new List<DxfTypedValue>(GetOwnGroups(0));

            if (own.Count == 0 || own[0].TypeCode != 100)
            {
                SetGroups(groups);
                return;
            }

            // The text's part, the attribute's, and an embedded MTEXT (101 on), which is kept as it is.
            int embedded = own.FindIndex(1, g => g.TypeCode == 101);
            int end = embedded < 0 ? own.Count : embedded;
            int split = own.FindIndex(1, end - 1, g => g.TypeCode == 100);
            List<DxfTypedValue> text = own.GetRange(1, (split < 0 ? end : split) - 1);
            List<DxfTypedValue> rest = split < 0 ? new List<DxfTypedValue>() : own.GetRange(split, end - split);
            List<DxfTypedValue> tail = newEmbedded != null ? new List<DxfTypedValue>(newEmbedded) : own.GetRange(end, own.Count - end);

            foreach (KeyValuePair<int, string> group in groups)
            {
                if (AttributeCodes.Contains(group.Key))
                {
                    Put(rest, group, true);
                }
                else
                {
                    Put(text, group, false);
                }
            }

            if (locked != null)
            {
                int tag = rest.FindIndex(g => g.TypeCode == 2);
                int at = tag < 0 ? -1 : rest.FindIndex(tag, g => g.TypeCode == 280);

                if (at >= 0)
                {
                    rest[at] = DxfTypedValue.Read(280, locked);
                }
                else
                {
                    int after = rest.FindLastIndex(g => g.TypeCode == 2 || g.TypeCode == 70 || g.TypeCode == 73 || g.TypeCode == 74);
                    rest.Insert(after < 0 ? rest.Count : after + 1, DxfTypedValue.Read(280, locked));
                }
            }

            if (type.HasValue && rest.Count > 0)
            {
                string typed = type.Value.ToString(CultureInfo.InvariantCulture);
                int at = rest.FindIndex(1, g => g.TypeCode == 71);

                if (at >= 0)
                {
                    rest[at] = int.TryParse(rest[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int now) && now == type.Value ? rest[at] : DxfTypedValue.Read(71, typed);
                }
                else
                {
                    int after = rest.FindLastIndex(g => g.TypeCode == 2 || g.TypeCode == 3 || g.TypeCode == 70 || g.TypeCode == 73 || g.TypeCode == 74 || g.TypeCode == 280);
                    rest.Insert(after < 0 ? rest.Count : after + 1, DxfTypedValue.Read(71, typed));
                    at = after < 0 ? rest.Count - 1 : after + 1;

                    if (rest.FindIndex(1, g => g.TypeCode == 72) < 0)
                    {
                        rest.Insert(at + 1, DxfTypedValue.Read(72, "0"));
                    }
                }
            }

            var codes = new HashSet<int>();
            var run = new List<KeyValuePair<int, string>>();

            for (int i = 1; i < own.Count; i++)
            {
                codes.Add(own[i].TypeCode);
            }

            foreach (DxfTypedValue group in text)
            {
                run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            foreach (DxfTypedValue group in rest)
            {
                run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            foreach (DxfTypedValue group in tail)
            {
                run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            ReplaceGroups(0, codes, run.ToArray());
        }

        /// <summary>Leaves out the embedded MTEXT (group 101 on) of an attribute, its text's part keeping the words of its group 1; nothing where it has none.</summary>
        internal void DropEmbedded()
        {
            if (IsAttribute && Embedded)
            {
                Rewrite(new KeyValuePair<int, string>[0], null, new DxfTypedValue[0]);
            }
        }

        /// <summary>The 71 an ATTRIB or ATTDEF of several lines writes in its attribute's part from R2018: 2 for an ATTRIB, 4 for an ATTDEF, as ezdxf writes them.</summary>
        private protected virtual int SeveralLines => 2;

        /// <summary>
        /// The value (group 1) of an attribute of several lines of the words of its MTEXT: of an ATTRIB every line joined by
        /// spaces, as AutoCAD 2021 writes it.
        /// </summary>
        private protected virtual string ValueOfLines(string text) => text.Replace('\n', ' ');

        /// <summary>
        /// The MTEXT an attribute is made of, as AutoCAD's <c>MTextAttribute</c> gives it: a new MTEXT in no file of the groups
        /// of its embedded object, with the attribute's look; null for an attribute of one line.
        /// </summary>
        private protected DxfMText MTextOf()
        {
            List<DxfTypedValue> tail = EmbeddedTail();

            if (tail == null)
            {
                return null;
            }

            DxfMText mtext = DxfMText.OfEmbedded(tail);
            mtext.SetPropertiesFrom(this);
            return mtext;
        }

        /// <summary>
        /// Makes the attribute of several lines, of an MTEXT, as AutoCAD's <c>MTextAttribute</c> setter and ezdxf's
        /// <c>set_mtext</c> do: the MTEXT's own groups embedded after a 101, those AutoCAD always writes put in where it lacks
        /// them; its text's part made to agree, its value as <see cref="ValueOfLines"/> gives it, the MTEXT's location its two points
        /// in the system of its plane, its direction its rotation, its attachment its justification, its style and height its
        /// own, its width factor, lean and mirroring taken out; its 71 the type of an attribute of several lines.
        /// </summary>
        private protected void SetMText(DxfMText value)
        {
            // Null, what an attribute of one line reads, changes nothing on one.
            if (value == null && !Embedded)
            {
                return;
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            List<DxfTypedValue> theirs = DxfMText.OwnOf(value);
            List<DxfTypedValue> tail = EmbeddedTail();

            // The MTEXT it is made of, given back, changes nothing.
            if (tail != null && tail.Count == theirs.Count && tail.Zip(theirs, (a, b) => a.TypeCode == b.TypeCode && string.Equals(a.Value, b.Value, StringComparison.Ordinal)).All(s => s))
            {
                return;
            }

            Several();
            string style = DxfMText.StyleOf(value);
            CheckStyle(style, nameof(value));
            GeoVector3 normal = value.Normal;
            GeoPoint3 location = WorldToOcs(normal, value.Location);
            GeoPoint3 along = WorldToOcs(normal, new GeoPoint3(value.Direction.X, value.Direction.Y, value.Direction.Z));
            double degrees = Math.Atan2(along.Y, along.X) * (180.0 / Math.PI) + 0.0;
            int attachment = (int)value.Attachment;
            int horizontal = attachment >= 1 && attachment <= 9 ? (attachment - 1) % 3 : 0;
            int vertical = attachment >= 1 && attachment <= 9 ? 3 - (attachment - 1) / 3 : 3;
            string text = value.Text;
            var embedded = new List<DxfTypedValue> { DxfTypedValue.Read(101, "Embedded Object") };
            embedded.AddRange(DxfMText.EmbeddedOf(value));
            Rewrite(
                Join(
                    text.Length > 0 ? Pair(1, ValueOfLines(text)) : new KeyValuePair<int, string>[0],
                    TextPointGroups(10, location),
                    TextPointGroups(11, location),
                    NumberGroup(50, degrees, TextDouble(50, 0.0)),
                    TextNormalGroups(normal),
                    WholeGroup(72, horizontal, TextInt(72, 0)),
                    WholeGroup(VerticalCode, vertical, TextInt(VerticalCode, 0)),
                    string.Equals(style, TextValue(7), StringComparison.Ordinal) ? new KeyValuePair<int, string>[0] : Pair(7, style),
                    NumberGroup(40, value.TextHeight, Height),
                    TextValue(41) != null ? Pair(41, null) : new KeyValuePair<int, string>[0],
                    TextValue(51) != null ? Pair(51, null) : new KeyValuePair<int, string>[0],
                    TextValue(71) != null ? Pair(71, null) : new KeyValuePair<int, string>[0]),
                null,
                embedded,
                SeveralLines);
        }

        /// <summary>
        /// Makes the attribute of several lines or of one, as AutoCAD's <c>IsMTextAttribute</c> setter does: of several, an MTEXT
        /// made of its value where its text stands, as <see cref="SetMText"/> embeds one; of one, its embedded MTEXT taken out,
        /// its text's part keeping the words of its group 1, its 71 the type of one line. Nothing where it is as asked.
        /// </summary>
        private protected void SetSeveral(bool value)
        {
            if (value == Embedded)
            {
                return;
            }

            if (!value)
            {
                Rewrite(new KeyValuePair<int, string>[0], null, new DxfTypedValue[0], AttributeValue(71) != null ? 1 : (int?)null);
                return;
            }

            Several();
            string words = TextString.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");
            var made = new DxfMText { Contents = words };
            SetMText(Placed(made));
        }

        /// <summary>
        /// Places the attribute's embedded MTEXT again where its text stands, as AutoCAD's <c>UpdateMTextAttribute</c> does after
        /// the attribute is moved or set from its definition: its location, height, direction, normal, style and attachment
        /// those of the text's part, its words and width kept; nothing for an attribute of one line, or one that agrees.
        /// </summary>
        private protected void UpdateMText()
        {
            List<DxfTypedValue> tail = EmbeddedTail();

            if (tail == null)
            {
                return;
            }

            DxfMText placed = Placed(DxfMText.OfEmbedded(tail));
            var moved = new List<DxfTypedValue> { DxfTypedValue.Read(101, "Embedded Object") };
            moved.AddRange(DxfMText.OwnOf(placed));
            List<DxfTypedValue> now = new List<DxfTypedValue> { DxfTypedValue.Read(101, "Embedded Object") };
            now.AddRange(tail);

            if (moved.Count == now.Count && moved.Zip(now, (a, b) => a.TypeCode == b.TypeCode && string.Equals(a.Value, b.Value, StringComparison.Ordinal)).All(s => s))
            {
                return;
            }

            Rewrite(new KeyValuePair<int, string>[0], null, moved);
        }

        /// <summary>
        /// An MTEXT in no file placed where the attribute's text stands: at its second point where it is not aligned left on
        /// its baseline, else its first, in the world; its letters as high, along its rotation, on its normal, in its style,
        /// held by the point of its justification, a baseline taken as the bottom.
        /// </summary>
        private DxfMText Placed(DxfMText mtext)
        {
            int horizontal = TextInt(72, 0);
            int vertical = TextInt(VerticalCode, 0);
            int across = horizontal == 1 || horizontal == 4 ? 1 : horizontal == 2 ? 2 : 0;
            int down = vertical == 3 ? 0 : vertical == 2 || (horizontal == 4 && vertical == 0) ? 1 : 2;
            GeoVector3 normal = Normal;
            double angle = Rotation;
            GeoPoint3 along = OcsToWorld(normal, new GeoPoint3(Math.Cos(angle), Math.Sin(angle), 0.0));
            mtext.Normal = normal;
            mtext.Location = (horizontal != 0 || vertical != 0) && TextPoint(11).HasValue ? AlignmentPoint : Position;
            mtext.Direction = new GeoVector3(along.X, along.Y, along.Z);
            mtext.TextHeight = Height;
            mtext.Attachment = (DxfAttachmentPoint)(down * 3 + across + 1);
            DxfMText.SetStyleOf(mtext, TextValue(7) ?? "Standard");
            return mtext;
        }

        /// <summary>Refuses an attribute of several lines in a file older than R2018 (AC1032), which has no MTEXT embedded in one.</summary>
        private void Several()
        {
            if (!Item.File.Supports(1032))
            {
                throw new InvalidOperationException("A DXF older than R2018 (AC1032) has no attribute of several lines: an MTEXT embedded in an ATTRIB or ATTDEF came with it.");
            }
        }

        /// <summary>The groups of the attribute's embedded MTEXT, after its 101; null where it has none.</summary>
        private List<DxfTypedValue> EmbeddedTail()
        {
            List<DxfTypedValue> own = new List<DxfTypedValue>(GetOwnGroups(0));
            int at = own.FindIndex(g => g.TypeCode == 101);
            return at < 0 ? null : own.GetRange(at + 1, own.Count - at - 1);
        }

        /// <summary>The text of the first group of a code in the attribute's part of an ATTRIB or ATTDEF, as its type (71) from R2018; null where it has none.</summary>
        private string AttributeValue(int code)
        {
            int segment = -1;

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == 101)
                {
                    break;
                }

                if (group.TypeCode == 100)
                {
                    segment++;
                }
                else if (segment > 0 && group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>Refuses a text style of a name the file's text style table does not list, as <see cref="Appending"/> does.</summary>
        private void CheckStyle(string style, string parameter)
        {
            if (!IsResident || style == null)
            {
                return;
            }

            string name = DxfReader.NameOf(style);

            foreach (DxfItem item in Item.File.TableItems("STYLE"))
            {
                if (string.Equals(DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new ArgumentException("The text style " + name + " is not in the file's text style table.", parameter);
        }

        /// <summary>
        /// The groups of an attribute's embedded MTEXT (101 on) moved by a transformation, as ezdxf moves the MTEXT it stands
        /// for: its insertion point (10) and direction (11, its angle 50 given up for it) carried, its extrusion (210) made anew,
        /// its height (40) and width (41) stretched and the heights <c>\H</c> of its words with them; null where the attribute
        /// has none.
        /// </summary>
        private List<DxfTypedValue> EmbeddedMoved(GeoTransform3 transform)
        {
            List<DxfTypedValue> tail = new List<DxfTypedValue>();
            bool inside = false;

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                inside |= group.TypeCode == 101;

                if (inside)
                {
                    tail.Add(group);
                }
            }

            if (tail.Count == 0)
            {
                return null;
            }

            string Value(int code)
            {
                for (int i = 1; i < tail.Count; i++)
                {
                    if (tail[i].TypeCode == code)
                    {
                        return tail[i].Value;
                    }
                }

                return null;
            }

            double Number(int code, double fallback) => Numbers.TryRead(Value(code), out double value) && Guard.IsFinite(value) ? value : fallback;

            GeoVector3 Vector(int code, GeoVector3 fallback)
                => Value(code) == null && Value(code + 10) == null && Value(code + 20) == null ? fallback : new GeoVector3(Number(code, 0.0), Number(code + 10, 0.0), Number(code + 20, 0.0));

            GeoVector3 extrusion = Vector(210, new GeoVector3(0.0, 0.0, 1.0));
            var ocs = new DxfOcsTransform(extrusion, transform);
            GeoVector3 direction;

            if (Value(11) != null || Value(21) != null || Value(31) != null)
            {
                direction = Vector(11, new GeoVector3(1.0, 0.0, 0.0));
            }
            else if (Value(50) != null)
            {
                double angle = Number(50, 0.0) * (Math.PI / 180.0);
                direction = new DxfOcsTransform.Axes(extrusion).ToWorld(new GeoVector3(Math.Cos(angle), Math.Sin(angle), 0.0));
            }
            else
            {
                direction = new GeoVector3(1.0, 0.0, 0.0);
            }

            // The MTEXT of a multiline attribute has no lean: one that would lean it is refused, before anything is written.
            if (DxfOcsTransform.Shears(transform, extrusion, direction))
            {
                throw new ArgumentException("The transformation shears the attribute's lines of MTEXT, which cannot lean.", nameof(transform));
            }

            GeoVector3 insert = Vector(10, new GeoVector3(0.0, 0.0, 0.0));
            GeoPoint3 moved = transform.Transform(new GeoPoint3(insert.X, insert.Y, insert.Z));
            GeoVector3 newDirection = transform.Transform(direction);
            double oldHeight = Number(40, 0.0);
            var groups = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(10, Numbers.Exact(moved.X + 0.0)),
                new KeyValuePair<int, string>(20, Numbers.Exact(moved.Y + 0.0)),
                new KeyValuePair<int, string>(30, Numbers.Exact(moved.Z + 0.0)),
                new KeyValuePair<int, string>(11, Numbers.Exact(newDirection.X + 0.0)),
                new KeyValuePair<int, string>(21, Numbers.Exact(newDirection.Y + 0.0)),
                new KeyValuePair<int, string>(31, Numbers.Exact(newDirection.Z + 0.0)),
                new KeyValuePair<int, string>(50, null),
            };

            GeoVector3 newExtrusion = ocs.NewExtrusion;

            if (Value(210) != null || newExtrusion.X != 0.0 || newExtrusion.Y != 0.0 || newExtrusion.Z != 1.0)
            {
                groups.Add(new KeyValuePair<int, string>(210, Numbers.Exact(newExtrusion.X + 0.0)));
                groups.Add(new KeyValuePair<int, string>(220, Numbers.Exact(newExtrusion.Y + 0.0)));
                groups.Add(new KeyValuePair<int, string>(230, Numbers.Exact(newExtrusion.Z + 0.0)));
            }

            double factor = 1.0;

            if (oldHeight > 0.0)
            {
                GeoVector3 upright = DxfOcsTransform.Cross(extrusion, direction);
                GeoVector3 newUpright = transform.Transform(upright.Multiply(oldHeight / upright.Length));
                double cos = newDirection.DotProduct(newUpright) / (newDirection.Length * newUpright.Length);
                double newHeight = newUpright.Length * Math.Sin(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))));
                groups.Add(new KeyValuePair<int, string>(40, Numbers.Exact(newHeight + 0.0)));
                factor = Math.Abs(newHeight - oldHeight) <= 1E-9 * Math.Max(Math.Abs(newHeight), Math.Abs(oldHeight)) ? 1.0 : newHeight / oldHeight;
            }

            if (Value(41) != null)
            {
                double width = transform.Transform(direction.Multiply(Number(41, 0.0) / direction.Length)).Length;
                groups.Add(new KeyValuePair<int, string>(41, Numbers.Exact(width + 0.0)));
            }

            foreach (KeyValuePair<int, string> group in groups)
            {
                PutEmbedded(tail, group);
            }

            if (factor != 1.0)
            {
                for (int i = 1; i < tail.Count; i++)
                {
                    if (tail[i].TypeCode == 1 || tail[i].TypeCode == 3)
                    {
                        tail[i] = DxfTypedValue.Read(tail[i].TypeCode, DxfMText.ScaledHeights(tail[i].Value, factor));
                    }
                }
            }

            return tail;
        }

        /// <summary>Puts a group in an embedded MTEXT: in the place of its first, or where ezdxf writes it; out where its value is null.</summary>
        private static void PutEmbedded(List<DxfTypedValue> tail, KeyValuePair<int, string> group)
        {
            int at = tail.FindIndex(1, g => g.TypeCode == group.Key);

            if (at >= 0)
            {
                if (group.Value == null)
                {
                    tail.RemoveAt(at);
                }
                else
                {
                    tail[at] = DxfTypedValue.Read(group.Key, group.Value);
                }

                return;
            }

            if (group.Value == null)
            {
                return;
            }

            int rank = Array.IndexOf(EmbeddedOrder, group.Key);
            int put = 1;

            for (int i = 1; i < tail.Count; i++)
            {
                int their = Array.IndexOf(EmbeddedOrder, tail[i].TypeCode);

                if (their >= 0 && their < rank)
                {
                    put = i + 1;
                }
            }

            tail.Insert(put, DxfTypedValue.Read(group.Key, group.Value));
        }

        /// <summary>Whether a flag of an attribute's group 70 is set.</summary>
        private protected bool Flag(int mask) => (TextInt(70, 0) & mask) != 0;

        /// <summary>Sets or clears a flag of an attribute's group 70; nothing where it is as it was.</summary>
        private protected void SetFlag(int mask, bool on)
        {
            int now = TextInt(70, 0);
            SetText(WholeGroup(70, on ? now | mask : now & ~mask, now));
        }

        /// <summary>Sets the tag of an attribute (group 2), checked as AutoCAD checks one.</summary>
        private protected void SetTag(string value)
        {
            // The tag it has changes nothing, though AutoCAD would not take it.
            if (value != null && string.Equals(value, TextValue(2) ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            CheckTag(value, nameof(value));
            SetText(Pair(2, value));
        }

        /// <summary>Refuses a tag AutoCAD does not take: null, empty, or with a space or line break in it.</summary>
        private protected static void CheckTag(string value, string parameter)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (value.Length == 0 || value.IndexOf(' ') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A tag is a word, with no space or line break in it: '" + value + "' is not one.", parameter);
            }
        }

        /// <summary>Sets the field length of an attribute (group 73).</summary>
        private protected void SetFieldLength(int value)
        {
            if (value == TextInt(73, 0))
            {
                return;
            }

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A field length is nought or more.");
            }

            SetText(WholeGroup(73, value, TextInt(73, 0)));
        }

        /// <summary>
        /// Whether the position of an attribute is locked in its block: the group 280 after its tag, as ezdxf writes it from
        /// R2007 and AutoCAD from R2010, where AutoCAD's 280 before the tag is the version of the attribute.
        /// </summary>
        private protected bool Locked
        {
            get
            {
                IReadOnlyList<DxfTypedValue> own = GetOwnGroups(0);
                bool tagged = false;
                int segment = -1;

                foreach (DxfTypedValue group in own)
                {
                    if (group.TypeCode == 101)
                    {
                        break;
                    }

                    if (group.TypeCode == 100)
                    {
                        segment++;
                    }
                    else if (segment > 0 && group.TypeCode == 2)
                    {
                        tagged = true;
                    }
                    else if (tagged && group.TypeCode == 280)
                    {
                        return int.TryParse(group.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int locked) && locked != 0;
                    }
                }

                return false;
            }
        }

        /// <summary>Sets whether the position of an attribute is locked in its block, refused before R2007, which has no such group.</summary>
        private protected void SetLocked(bool value)
        {
            if (value == Locked)
            {
                return;
            }

            if (!Item.File.Supports(1021))
            {
                throw new InvalidOperationException("A DXF older than R2007 (AC1021) has no lock of an attribute's position.");
            }

            Rewrite(new KeyValuePair<int, string>[0], value ? "1" : "0");
        }

        /// <summary>Whether the attribute is made of an MTEXT: its record holds an embedded object (group 101).</summary>
        private protected bool Embedded
        {
            get
            {
                foreach (DxfTypedValue group in GetOwnGroups(0))
                {
                    if (group.TypeCode == 101)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The point of a code of the text's part, a coordinate the record leaves out nought; null where it has none of the three.</summary>
        private protected GeoPoint3? TextPoint(int code)
        {
            bool x = TryNumber(code, out double px);
            bool y = TryNumber(code + 10, out double py);
            bool z = TryNumber(code + 20, out double pz);
            return x || y || z ? new GeoPoint3(px, py, pz) : (GeoPoint3?)null;
        }

        /// <summary>The groups that set a point of the text's part: the coordinates that differ, to the bit, and those it leaves out with them.</summary>
        private protected KeyValuePair<int, string>[] TextPointGroups(int code, GeoPoint3 value)
        {
            CheckPoint(value);
            GeoPoint3 now = TextPoint(code) ?? Origin;
            return Changes(code, new[] { value.X, value.Y, value.Z }, new[] { now.X, now.Y, now.Z });
        }

        /// <summary>
        /// The groups that set the normal of the text's part: none where it is the normal read, to the bit; else each group that
        /// differs from the number the record holds, as one not of unit length, (1, 2, 3), is read made so.
        /// </summary>
        private protected KeyValuePair<int, string>[] TextNormalGroups(GeoVector3 unit)
        {
            GeoVector3 now = TextNormal();

            if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
            {
                return new KeyValuePair<int, string>[0];
            }

            TryNumber(210, out double x);
            TryNumber(220, out double y);
            TryNumber(230, out double z);
            return Changes(210, new[] { unit.X, unit.Y, unit.Z }, new[] { x, y, z });
        }

        /// <summary>The group that sets a number, none where it is the one read, to the bit.</summary>
        private protected static KeyValuePair<int, string>[] NumberGroup(int code, double value, double now)
            => Same(value, now) ? new KeyValuePair<int, string>[0] : Pair(code, Numbers.Exact(value + 0.0));

        /// <summary>The group that sets a whole number, none where it is the one read.</summary>
        private protected static KeyValuePair<int, string>[] WholeGroup(int code, int value, int now)
            => value == now ? new KeyValuePair<int, string>[0] : Pair(code, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>One group to be set.</summary>
        private protected static KeyValuePair<int, string>[] Pair(int code, string value) => new[] { new KeyValuePair<int, string>(code, value) };

        /// <summary>
        /// The groups a transformation gives the text, as ezdxf's <c>Text.transform()</c> gives them: through the system of its
        /// plane, the second point carried where the record has one.
        /// </summary>
        private protected KeyValuePair<int, string>[] TransformGroups(GeoTransform3 transform)
        {
            GeoVector3 normal = Normal;
            var ocs = new DxfOcsTransform(normal, transform);

            if (!ocs.NewExtrusion.IsValid)
            {
                throw new ArgumentException("The transformation flattens the plane of the " + Item.Type + ".", nameof(transform));
            }

            GeoPoint3 insert = ocs.TransformVertex(TextPoint(10) ?? Origin);
            GeoPoint3? aligned = TextPoint(11);
            double oldRotation = TextDouble(50, 0.0);
            double newRotation = ocs.TransformDegrees(oldRotation);
            double xScale = ocs.TransformLength(FromDegrees(oldRotation));
            double yScale = ocs.TransformLength(FromDegrees(oldRotation + 90.0));
            double oblique = TextDouble(51, 0.0);

            if (!ocs.ScaleUniform)
            {
                GeoVector3 leaning = ocs.TransformDirection(FromDegrees(oldRotation + 90.0 - oblique));
                oblique = newRotation + 90.0 - Math.Atan2(leaning.Y, leaning.X) * (180.0 / Math.PI);
                yScale *= Math.Cos(oblique * (Math.PI / 180.0));
            }

            double width = TextDouble(41, 1.0) * (xScale / yScale);
            double height = TextDouble(40, 2.5) * yScale;
            double thickness = Thickness;

            if (!Guard.IsFinite(width) || !Guard.IsFinite(height) || !(height > 0.0) || !(width > 0.0))
            {
                throw new ArgumentException("The transformation flattens the " + Item.Type + ".", nameof(transform));
            }

            return Join(
                TextPointGroups(10, insert),
                aligned.HasValue ? TextPointGroups(11, ocs.TransformVertex(aligned.Value)) : new KeyValuePair<int, string>[0],
                NumberGroup(40, height, TextDouble(40, 2.5)),
                NumberGroup(50, newRotation, oldRotation),
                NumberGroup(41, width, TextDouble(41, 1.0)),
                NumberGroup(51, oblique, TextDouble(51, 0.0)),
                thickness != 0.0 ? NumberGroup(39, ocs.TransformThickness(thickness), thickness) : new KeyValuePair<int, string>[0],
                TextNormalGroups(ocs.NewExtrusion));
        }

        /// <summary>A text with its escapes of R2000 read as the letters they stand for, <c>\U+00E9</c> an e acute, the rest as written.</summary>
        internal static string Unescaped(string text)
        {
            int at = text.IndexOf("\\U+", StringComparison.Ordinal);

            if (at < 0)
            {
                return text;
            }

            var plain = new StringBuilder(text.Length);
            int from = 0;

            while (at >= 0)
            {
                if (at + 7 <= text.Length && int.TryParse(text.Substring(at + 3, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int letter))
                {
                    plain.Append(text, from, at - from).Append((char)letter);
                    from = at + 7;
                }
                else
                {
                    plain.Append(text, from, at + 3 - from);
                    from = at + 3;
                }

                at = text.IndexOf("\\U+", from, StringComparison.Ordinal);
            }

            return plain.Append(text, from, text.Length - from).ToString();
        }

        private static GeoVector3 FromDegrees(double degrees)
        {
            double radians = degrees * (Math.PI / 180.0);
            return new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0);
        }

        private static void CheckPoint(GeoPoint3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }
        }

        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        /// <summary>Puts a group in a part: in the place of the first of its code, or where the order puts it; taken out where its value is null.</summary>
        private void Put(List<DxfTypedValue> part, KeyValuePair<int, string> group, bool attribute)
        {
            int at = part.FindIndex(g => g.TypeCode == group.Key);

            if (at >= 0)
            {
                if (group.Value == null)
                {
                    part.RemoveAt(at);
                }
                else
                {
                    part[at] = DxfTypedValue.Read(group.Key, group.Value);
                }

                return;
            }

            if (group.Value == null)
            {
                return;
            }

            IReadOnlyList<int> order = GroupOrder;
            int second = -1;

            for (int k = 1; k < order.Count && second < 0; k++)
            {
                second = order[k] == 100 ? k : -1;
            }

            int rank = -1;

            for (int k = attribute ? second + 1 : 1; k < (attribute || second < 0 ? order.Count : second) && rank < 0; k++)
            {
                rank = order[k] == group.Key ? k : -1;
            }

            int put = attribute && part.Count > 0 && part[0].TypeCode == 100 ? 1 : 0;

            for (int i = 0; i < part.Count; i++)
            {
                int their = -1;

                for (int k = attribute ? second + 1 : 1; k < (attribute || second < 0 ? order.Count : second) && their < 0; k++)
                {
                    their = order[k] == part[i].TypeCode ? k : -1;
                }

                if (their >= 0 && their < rank)
                {
                    put = i + 1;
                }
            }

            part.Insert(put, DxfTypedValue.Read(group.Key, group.Value));
        }

        /// <summary>Whether a group to be set of an ATTRIB or ATTDEF would land in the wrong part: its code missing from its own part and found in the other.</summary>
        private bool Misplaced(KeyValuePair<int, string>[] groups)
        {
            IReadOnlyList<DxfTypedValue> own = GetOwnGroups(0);
            var text = new HashSet<int>();
            var rest = new HashSet<int>();
            var tail = new HashSet<int>();
            int segment = -1;
            bool embedded = false;

            foreach (DxfTypedValue group in own)
            {
                embedded |= group.TypeCode == 101;

                if (embedded)
                {
                    tail.Add(group.TypeCode);
                }
                else if (group.TypeCode == 100)
                {
                    segment++;
                }
                else if (segment > 0)
                {
                    rest.Add(group.TypeCode);
                }
                else if (segment == 0)
                {
                    text.Add(group.TypeCode);
                }
            }

            if (segment < 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, string> group in groups)
            {
                bool attribute = AttributeCodes.Contains(group.Key);
                bool home = attribute ? rest.Contains(group.Key) : text.Contains(group.Key);
                bool away = (attribute ? text.Contains(group.Key) : rest.Contains(group.Key)) || tail.Contains(group.Key);

                if (!home && away)
                {
                    return true;
                }
            }

            return false;
        }

        private GeoVector3 TextNormal()
        {
            bool any = TryNumber(210, out double x) | TryNumber(220, out double y) | TryNumber(230, out double z);
            double length = Math.Sqrt(x * x + y * y + z * z);

            if (!any || !(length > 0.0) || double.IsInfinity(length))
            {
                return new GeoVector3(0.0, 0.0, 1.0);
            }

            return Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(x, y, z) : new GeoVector3(x / length, y / length, z / length);
        }

        private bool TryNumber(int code, out double value)
        {
            if (Numbers.TryRead(TextValue(code), out value) && Guard.IsFinite(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }

        /// <summary>The groups of a code and those 10 and 20 above it whose numbers differ, to the bit, and those the part leaves out with them.</summary>
        private KeyValuePair<int, string>[] Changes(int code, double[] values, double[] now)
        {
            bool differs = false;

            for (int i = 0; i < values.Length; i++)
            {
                differs |= !Same(values[i], now[i]);
            }

            if (!differs)
            {
                return new KeyValuePair<int, string>[0];
            }

            var changes = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < values.Length; i++)
            {
                int at = code + 10 * i;

                if (!Same(values[i], now[i]) || TextValue(at) == null)
                {
                    changes.Add(new KeyValuePair<int, string>(at, Numbers.Exact(values[i] + 0.0)));
                }
            }

            return changes.ToArray();
        }
    }
}
