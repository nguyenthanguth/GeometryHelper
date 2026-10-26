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
    /// An MTEXT of a DXF file, as AutoCAD's <c>MText</c>: words of several lines with their formatting, held at
    /// <see cref="Location"/> by its <see cref="Attachment"/>, <see cref="TextHeight"/> high, wrapping at <see cref="Width"/>,
    /// reading along <see cref="Direction"/> in the plane of its <see cref="Normal"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An MTEXT of <see cref="Contents"/> <c>{\fArial|b1;Bold}\PLine two</c> at (5, 5, 0), 2.5 high and 40 wide, turned by
    /// 30 degrees, reads as <see cref="Text"/> "Bold" and "Line two" on two lines, <see cref="Rotation"/> 0.5236 radians and
    /// <see cref="Direction"/> (0.866, 0.5, 0). Its location and direction are in world coordinates, as the file writes them.
    /// </para>
    /// <para>
    /// A property set writes its groups as <see cref="DxfWriter"/> writes a number, every other group of the record kept as
    /// it was; a property given the value it has changes nothing. A file of R12 has no MTEXT: one made in code, <c>new
    /// DxfMText { Contents = "B-12\\PM16", Location = new GeoPoint3(10, 20, 0) }</c>, is written there by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> as one TEXT for each of its paragraphs, B-12 and M16, its
    /// formatting left out as <see cref="Text"/> leaves it out; the lines are not wrapped at its width, as AutoCAD's SAVEAS to
    /// R12 wraps them by the letters of the font, which the library does not read. The MTEXT itself stays in no file. <see cref="DxfEntity.GetGeometry"/> gives the <see cref="DxfText"/> <see cref="DxfReader"/> reads of it.
    /// </para>
    /// <para>
    /// Before R2018 an MTEXT's columns after its first are MTEXTs of their own, which it links to by handle in its extended
    /// data (<c>ACAD_MTEXT_COLUMNS</c>). It is one text to AutoCAD and ezdxf: moved by
    /// <see cref="DxfEntity.TransformBy(GeoTransform3)"/> or its <see cref="Location"/>, <see cref="Rotation"/>,
    /// <see cref="Direction"/> or <see cref="Normal"/>, it moves them with it;
    /// given a <see cref="TextHeight"/> or <see cref="Height"/>, it gives them the same; erased, it takes them with it. Its
    /// <see cref="Contents"/> holds the words of its first column alone. A copy of it (<see cref="DxfEntity.Clone"/>,
    /// <see cref="DxfEntity.GetTransformedCopy(GeoTransform3)"/>, an INSERT's <see cref="DxfBlockReference.Explode"/>) holds the
    /// words of them all itself, after a column break <c>\N</c> each for static columns, and links to none of them, so that no
    /// two MTEXTs claim the same columns: three static columns one, two and three come out as <c>one\Ntwo\Nthree</c>.
    /// </para>
    /// </remarks>
    public sealed partial class DxfMText : DxfEntity
    {
        // The groups of AcDbMText in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 40, 41, 46, 71, 72, 3, 1, 7, 210, 220, 230, 11, 21, 31, 42, 43, 50, 73, 44, 90, 63, 421, 431, 45, 441 });

        private static readonly int[] Words = { 1, 3 };

        // The groups of the object an MTEXT of R2018 embeds for its columns (101 on), in the order ezdxf and AutoCAD write them.
        private static readonly int[] EmbeddedOrder = { 101, 70, 10, 20, 30, 11, 21, 31, 40, 41, 42, 43, 71, 72, 44, 45, 73, 74, 46 };

        // Single spacing of the lines against the height of the letters, as AutoCAD spaces them.
        private const double SingleSpacing = 5.0 / 3.0;

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes an MTEXT with no words at the origin, 2.5 high, held at its top left, as AutoCAD's <c>new MText()</c>, on
        /// layer 0 in the text style Standard, in no file; give it its words and place before it is drawn.
        /// </summary>
        public DxfMText()
            : base("MTEXT", new[]
            {
                new DxfTypedValue(100, "AcDbMText"), new DxfTypedValue(10, "0"), new DxfTypedValue(20, "0"), new DxfTypedValue(30, "0"),
                new DxfTypedValue(40, "2.5"), new DxfTypedValue(71, "1"), new DxfTypedValue(1, string.Empty),
            })
        {
        }

        private DxfMText(DxfItem item)
            : base(item)
        {
        }

        /// <summary>An MTEXT made in code of its own groups, its marker AcDbMText first.</summary>
        private DxfMText(IEnumerable<DxfTypedValue> groups)
            : base("MTEXT", groups)
        {
        }

        /// <summary>
        /// Gets or sets the words of the MTEXT with their formatting (groups 3 and 1), as AutoCAD's <c>Contents</c>: as the file
        /// writes them, <c>\P</c> a new paragraph, <c>{\H1.5x;Big}</c> letters half as high again, a letter beyond the code page
        /// <c>\U+00E9</c>; empty where the record gives none. Before R2018 an MTEXT whose later columns are MTEXTs of their own
        /// holds only the words of its first: those of the others are in their MTEXTs, as AutoCAD and ezdxf keep them.
        /// </summary>
        /// <value>Set, words with their formatting codes, with no line break: written in pieces of 250 characters, the last in
        /// group 1, a piece not ending between a caret and the character it goes with. A letter the file's code page lacks is
        /// written as its escape, which reads back as the code <c>\U+</c>.</value>
        /// <exception cref="ArgumentNullException">Thrown when the words set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the words set have a line break in them: a new paragraph is <c>\P</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public string Contents
        {
            get
            {
                var text = new StringBuilder();
                string last = null;

                foreach (DxfTypedValue group in GetOwnGroups(0))
                {
                    if (group.TypeCode == 101)
                    {
                        break;
                    }

                    if (group.TypeCode == 3)
                    {
                        text.Append(group.Value);
                    }
                    else if (group.TypeCode == 1 && last == null)
                    {
                        last = group.Value;
                    }
                }

                return text.Append(last ?? string.Empty).ToString();
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("The words of an MTEXT have no line break in them: a new paragraph is written \\P.", nameof(value));
                }

                if (string.Equals(value, Contents, StringComparison.Ordinal))
                {
                    return;
                }

                var groups = new List<KeyValuePair<int, string>>();
                int from = 0;

                while (value.Length - from > 250)
                {
                    int piece = value[from + 249] == '^' ? 249 : 250;
                    groups.Add(new KeyValuePair<int, string>(3, value.Substring(from, piece)));
                    from += piece;
                }

                groups.Add(new KeyValuePair<int, string>(1, value.Substring(from)));
                ReplaceGroups(0, Words, groups.ToArray());
            }
        }

        /// <summary>
        /// Gets the words of the MTEXT without their formatting, as AutoCAD's <c>Text</c>: as <see cref="DxfReader"/> reads them,
        /// a new paragraph a line feed, a stack <c>\S1^2;</c> as 1/2, <c>%%d</c> a degree sign and an escape its letter; the
        /// words of its own column only, where its later columns are MTEXTs of their own, as <see cref="Contents"/> says.
        /// </summary>
        public string Text
        {
            get
            {
                if (Item.Shape is DxfText read)
                {
                    return read.Value;
                }

                // An MTEXT the reader leaves out, as one of no height, is read as one of height 1 at the origin would be.
                var record = new DxfReader.Record("MTEXT", 0);
                record.Add(10, "0");
                record.Add(20, "0");
                record.Add(40, "1");

                foreach (DxfTypedValue group in GetOwnGroups(0))
                {
                    if (group.TypeCode == 101)
                    {
                        break;
                    }

                    if (group.TypeCode == 3 || group.TypeCode == 1)
                    {
                        record.Add(group.TypeCode, group.Value);
                    }
                }

                foreach (object shape in DxfReader.ShapesOf(record, Tolerance.Default))
                {
                    if (shape is DxfText text)
                    {
                        return text.Value;
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the words of the MTEXT with the codes of its fields in their places, as AutoCAD's <c>getMTextWithFieldCodes</c>:
        /// the code of its field (<see cref="DxfDBObject.GetField()"/>), its formatting codes with it, each field it holds standing
        /// as its code in its markers, at every depth. The MTEXT <c>{\H1.5x;Length} 2400 mm</c>, the length a field, gives
        /// <c>{\H1.5x;Length} %&lt;\AcObjProp Object(%&lt;\_ObjIdx 0&gt;%).Length \f "%lu2"&gt;% mm</c>, the object named by its index
        /// among the field's objects (331) as the file holds it; one with no field gives its <see cref="Contents"/>.
        /// </summary>
        /// <returns>The words with their formatting and the codes of their fields.</returns>
        public string GetMTextWithFieldCodes()
        {
            DxfField field = GetField();
            return field == null ? Contents : DxfField.WithCodes(field);
        }

        /// <summary>
        /// Makes the MTEXT's fields words, as AutoCAD's <c>ConvertFieldToText</c>: the field of its words taken out with its
        /// children, as <see cref="DxfDBObject.RemoveField()"/> takes it out, and its <see cref="Contents"/> (3 and 1) kept as they
        /// are, the values AutoCAD last gave the fields among them. Nothing where it has no field.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public void ConvertFieldToText() => RemoveField();

        /// <summary>
        /// Gets or sets where the MTEXT is held (groups 10, 20, 30), as AutoCAD's <c>Location</c>: in world coordinates, as the
        /// file writes it; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public GeoPoint3 Location
        {
            get => Point(10, Origin);
            set => Carried(() => SetAt(10, value, Origin));
        }

        /// <summary>Gets or sets how high the letters are (group 40), as AutoCAD's <c>TextHeight</c>; 2.5 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public double TextHeight
        {
            get => Number(40, 2.5);

            set
            {
                if (!Same(value, TextHeight))
                {
                    Guard.Positive(value, nameof(value), "The height of a text has to be a finite number above nought.");
                    List<DxfMText> columns = ColumnTexts();
                    SetNumber(40, value, 2.5);

                    foreach (DxfMText column in columns)
                    {
                        column.TextHeight = value;
                    }
                }
            }
        }

        /// <summary>Gets or sets the width the lines wrap at (group 41), as AutoCAD's <c>Width</c>; nought, no wrapping, where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public double Width
        {
            get => Number(41, 0.0);

            set
            {
                if (!Same(value, Width))
                {
                    Guard.NonNegative(value, nameof(value), "The width of an MTEXT has to be a finite number of nought or more.");
                    SetNumber(41, value, 0.0);
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the box the MTEXT is given (group 46), as AutoCAD's <c>Height</c>; where the record gives
        /// none, the height of its columns of one height, from its embedded object (41) or its extended data of ACAD
        /// (<c>ACAD_MTEXT_DEFINED_HEIGHT</c>), as ezdxf reads it; nought where it gives neither. A file older than R2007 (AC1021)
        /// has no group 46: the MTEXT of R2000 in three static columns 30 high has the height 30 of its extended data.
        /// </summary>
        /// <value>Set, written as group 46 from R2007; before it in <c>ACAD_MTEXT_DEFINED_HEIGHT</c>, which an MTEXT of R2000 has
        /// only in columns of one height.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or a height is set in a file older than
        /// R2007 on an MTEXT that is not in columns of one height.</exception>
        public double Height
        {
            get => Value(46) != null ? Number(46, 0.0) : Columns().Defined ?? 0.0;

            set
            {
                if (!Same(value, Height))
                {
                    Guard.NonNegative(value, nameof(value), "The height of an MTEXT's box has to be a finite number of nought or more.");

                    if (!Item.File.Supports(1021))
                    {
                        ColumnSet columns = Columns();

                        if (!columns.InXData || columns.Type == DxfColumnType.NoColumns || (columns.Type == DxfColumnType.DynamicColumns && !columns.Auto))
                        {
                            throw new InvalidOperationException("A DXF older than R2007 (AC1021) has no height of an MTEXT's box but in the extended data of its columns.");
                        }

                        columns.Defined = value;
                        WriteColumns(columns, false);
                        HeightToColumns();
                        return;
                    }

                    Set(new KeyValuePair<int, string>(46, Numbers.Exact(value + 0.0)));
                    HeightToColumns();
                }
            }
        }

        /// <summary>Gets the width the MTEXT's words take (group 42) as the file writes it, as AutoCAD's <c>ActualWidth</c>; nought where it gives none.</summary>
        public double ActualWidth => Number(42, 0.0);

        /// <summary>Gets the height the MTEXT's words take (group 43) as the file writes it, as AutoCAD's <c>ActualHeight</c>; nought where it gives none.</summary>
        public double ActualHeight => Number(43, 0.0);

        /// <summary>
        /// Gets or sets how far the MTEXT is turned, as AutoCAD's <c>Rotation</c>: in radians from the x axis of the system of
        /// its normal's plane, from nought up to 2 pi; read from its direction (11) where it has one, which wins, else from its
        /// angle (50, in degrees).
        /// </summary>
        /// <value>Set, written as the record has it: as its direction where it has one, as its angle too where it has that.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public double Rotation
        {
            get
            {
                if (HasDirection())
                {
                    GeoVector3 direction = Plane(Normal, Point(11, Origin).ToVector());
                    return Turn(Math.Atan2(direction.Y, direction.X));
                }

                return Turn(Number(50, 0.0) * (Math.PI / 180.0));
            }

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, Rotation))
                {
                    Carried(() => Set(Turned(Normal, value, HasDirection(), Value(50) != null || !HasDirection())));
                }
            }
        }

        /// <summary>
        /// Gets or sets the direction the MTEXT reads along (groups 11, 21, 31), as AutoCAD's <c>Direction</c>: in world
        /// coordinates, of unit length; where the record gives none, the x axis of the system of its plane turned by its angle (50).
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length; the angle (50) the record has, written to agree.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public GeoVector3 Direction
        {
            get
            {
                if (HasDirection())
                {
                    return DxfOcsTransform.Normalized(Point(11, Origin).ToVector());
                }

                double angle = Number(50, 0.0) * (Math.PI / 180.0);
                return World(Normal, new GeoVector3(Math.Cos(angle), Math.Sin(angle), 0.0));
            }

            set
            {
                GeoVector3 now = Direction;

                // The direction it reads, given back, changes nothing: made of unit length a second time it may not be the same to the bit.
                if (Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z))
                {
                    return;
                }

                double length = value.Length;

                if (!value.IsValid || !(length > 0.0) || double.IsInfinity(length))
                {
                    throw new ArgumentException("A direction has to be of finite numbers and some length: " + value + " is not.", nameof(value));
                }

                GeoVector3 unit = DxfOcsTransform.Normalized(value);

                if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
                {
                    return;
                }

                GeoVector3 plane = Plane(Normal, unit);
                var groups = new List<KeyValuePair<int, string>>(PointChanges(11, new GeoPoint3(unit.X, unit.Y, unit.Z), Origin));

                if (Value(50) != null)
                {
                    groups.Add(new KeyValuePair<int, string>(50, Numbers.Exact(Math.Atan2(plane.Y, plane.X) * (180.0 / Math.PI) + 0.0)));
                }

                Carried(() => Set(groups.ToArray()));
            }
        }

        /// <summary>
        /// Gets or sets the point the MTEXT is held by (group 71), as AutoCAD's <c>Attachment</c>;
        /// <see cref="DxfAttachmentPoint.TopLeft"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the point set is not one an MTEXT has, 1 to 9.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public DxfAttachmentPoint Attachment
        {
            get => (DxfAttachmentPoint)WholeNumber(71, 1);

            set
            {
                if (value == Attachment)
                {
                    return;
                }

                if (value < DxfAttachmentPoint.TopLeft || value > DxfAttachmentPoint.BottomRight)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "An MTEXT is held by one of its nine points, 1 to 9.");
                }

                SetWhole(71, (int)value, 1);
            }
        }

        /// <summary>
        /// Gets or sets which way the lines follow one another (group 72), as AutoCAD's <c>FlowDirection</c>;
        /// <see cref="DxfFlowDirection.LeftToRight"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the direction set is not one of AutoCAD's, 0 to 5.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public DxfFlowDirection FlowDirection
        {
            get => (DxfFlowDirection)WholeNumber(72, 1);

            set
            {
                if (value == FlowDirection)
                {
                    return;
                }

                if (value < DxfFlowDirection.NotSet || value > DxfFlowDirection.ByStyle)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A flow direction is one of AutoCAD's, 0 to 5.");
                }

                SetWhole(72, (int)value, 1);
            }
        }

        /// <summary>
        /// Gets or sets how the spacing of the lines is taken (group 73), as AutoCAD's <c>LineSpacingStyle</c>;
        /// <see cref="DxfLineSpacingStyle.AtLeast"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the style set is not one of AutoCAD's, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public DxfLineSpacingStyle LineSpacingStyle
        {
            get => (DxfLineSpacingStyle)WholeNumber(73, 1);

            set
            {
                if (value == LineSpacingStyle)
                {
                    return;
                }

                if (value != DxfLineSpacingStyle.AtLeast && value != DxfLineSpacingStyle.Exactly)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A line spacing style is 1, at least, or 2, exactly.");
                }

                SetWhole(73, (int)value, 1);
            }
        }

        /// <summary>
        /// Gets or sets the spacing of the lines against that of the text style (group 44), as AutoCAD's
        /// <c>LineSpacingFactor</c>; 1 where the record gives none.
        /// </summary>
        /// <value>Set, from 0.25 to 4, as AutoCAD takes it.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is outside 0.25 to 4.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public double LineSpacingFactor
        {
            get => Number(44, 1.0);

            set
            {
                if (Same(value, LineSpacingFactor))
                {
                    return;
                }

                if (!(value >= 0.25 && value <= 4.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A line spacing factor runs from 0.25 to 4, as AutoCAD takes it.");
                }

                SetNumber(44, value, 1.0);
            }
        }

        /// <summary>
        /// Gets or sets the name of the text style of the MTEXT (group 7), as AutoCAD's <c>TextStyleName</c>: as the style table
        /// names it, its escapes read as letters; <c>Standard</c> where the record gives none.
        /// </summary>
        /// <value>Set, the name of a style of the drawing's text style table, letters of either case the same, written as the
        /// table names it; any name for an MTEXT in no drawing, which <see cref="DxfBlockTableRecord.AppendEntity"/> checks. The
        /// style the MTEXT has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take, or the drawing has no text style
        /// of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public string TextStyleName
        {
            get
            {
                string name = DxfReader.NameOf(Value(7) ?? string.Empty);
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
                    string written = TextStyleToWrite(value, nameof(value));
                    List<DxfMText> columns = ColumnTexts();
                    Set(new KeyValuePair<int, string>(7, written));

                    // The MTEXTs of its later columns before R2018 take it too, as they take its height.
                    foreach (DxfMText column in columns)
                    {
                        column.TextStyleName = written;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the text style of the MTEXT, as AutoCAD's <c>TextStyleId</c> gives its id: the record of
        /// <see cref="TextStyleName"/> in the text style table of the MTEXT's drawing; null where it has none of that name, or the
        /// MTEXT is in no drawing and was made in code.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is erased, in no drawing, or of another drawing than the
        /// MTEXT's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public DxfTextStyleTableRecord TextStyle
        {
            get => TextStyleNamed(TextStyleName);
            set => TextStyleName = TextStyleOf(value, nameof(value));
        }

        /// <summary>
        /// Gets or sets the normal of the plane the MTEXT stands in (groups 210, 220, 230), as AutoCAD's <c>Normal</c>; of unit
        /// length, (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length; the MTEXT keeps its location and its
        /// <see cref="Rotation"/> in the new plane, its direction written anew where the record has one.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public GeoVector3 Normal
        {
            get => MNormal();

            set
            {
                KeyValuePair<int, string>[] normal = NormalChanges(value);

                if (normal.Length == 0)
                {
                    return;
                }

                double length = value.Length;
                GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
                Carried(() => Set(Join(normal, HasDirection() ? Turned(unit, Rotation, true, false) : new KeyValuePair<int, string>[0])));
            }
        }

        /// <summary>Gets or sets whether a background is drawn behind the words (flag 1 of group 90), as AutoCAD's <c>BackgroundFill</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2004 (AC1018).</exception>
        public bool BackgroundFill
        {
            get => (WholeNumber(90, 0) & 1) != 0;
            set => SetFill(1, value);
        }

        /// <summary>Gets or sets whether the background takes the colour of the drawing's background (flag 2 of group 90), as AutoCAD's <c>UseBackgroundColor</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2004 (AC1018).</exception>
        public bool UseBackgroundColor
        {
            get => (WholeNumber(90, 0) & 2) != 0;
            set => SetFill(2, value);
        }

        /// <summary>
        /// Gets or sets how far the background reaches past the words, against their height (group 45), as AutoCAD's
        /// <c>BackgroundScaleFactor</c>; 1.5 where the record gives none, as AutoCAD gives it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is outside 1 to 5, as AutoCAD takes it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2004 (AC1018).</exception>
        public double BackgroundScaleFactor
        {
            get => Number(45, 1.5);

            set
            {
                if (!Same(value, BackgroundScaleFactor))
                {
                    if (!(value >= 1.0 && value <= 5.0))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), value, "A background scale factor runs from 1 to 5, as AutoCAD takes it.");
                    }

                    Since(1018, "background of an MTEXT");
                    SetNumber(45, value, 1.5);
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the background (group 63, and 421 for red, green and blue, 431 for its book's name), as
        /// AutoCAD's <c>BackgroundFillColor</c>; null where the record gives none.
        /// </summary>
        /// <value>Set, a colour of the index or a true colour: the index written, and the red, green and blue of a true colour
        /// with it, as <see cref="DxfEntity.Color"/> writes them; null takes them out.</value>
        /// <exception cref="ArgumentException">Thrown when the colour set is by layer, by block or of no index or true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2004 (AC1018).</exception>
        public DxfColor BackgroundFillColor
        {
            get
            {
                int? index = WholeOrNull(63);
                int? rgb = WholeOrNull(421);

                if (rgb.HasValue)
                {
                    return DxfColor.OfRgb(rgb.Value & 0xFFFFFF, Value(431), index);
                }

                return index.HasValue ? DxfColor.OfIndex(index.Value) : null;
            }

            set
            {
                if (Equals(value, BackgroundFillColor))
                {
                    return;
                }

                if (value == null)
                {
                    Set(new KeyValuePair<int, string>(63, null), new KeyValuePair<int, string>(421, null), new KeyValuePair<int, string>(431, null));
                    return;
                }

                if (!value.IsByAci && !value.IsByColor)
                {
                    throw new ArgumentException("A background is of a colour of the index or a true colour: " + value.ColorMethod + " is neither.", nameof(value));
                }

                Since(1018, "background of an MTEXT");
                Set(
                    new KeyValuePair<int, string>(63, value.ColorIndex.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<int, string>(421, value.IsByColor ? value.ColorValue.ToString(CultureInfo.InvariantCulture) : null),
                    new KeyValuePair<int, string>(431, null));
            }
        }

        /// <summary>
        /// Gets or sets how the MTEXT is set in columns, as AutoCAD's <c>ColumnType</c>: from its embedded object from R2018 (group
        /// 71), else from its extended data of ACAD (75), as ezdxf reads them; <see cref="DxfColumnType.NoColumns"/> where it
        /// gives none.
        /// </summary>
        /// <value>
        /// Set, the columns written in the form of the file's version, as AutoCAD writes them: from R2018 (AC1032) the MTEXT's
        /// embedded object (group 101 on), before it the extended data of ACAD, between <c>ACAD_MTEXT_COLUMN_INFO_BEGIN</c> and
        /// <c>_END</c>, with <c>ACAD_MTEXT_DEFINED_HEIGHT</c>; the other form, where the record has it, taken out. An MTEXT of no
        /// columns given some has one column as wide as its <see cref="Width"/>, with no gutter, of auto height where they are
        /// dynamic; <see cref="SetStaticColumns"/> and <see cref="SetDynamicColumns"/> give the rest at once.
        /// <see cref="DxfColumnType.NoColumns"/> takes the columns out, the linked MTEXTs of the columns of a file before R2018
        /// (<c>ACAD_MTEXT_COLUMNS</c>) left as MTEXTs of their own, their words where they stand.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of AutoCAD's, 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public DxfColumnType ColumnType
        {
            get => Columns().Type;

            set
            {
                if (value == ColumnType)
                {
                    return;
                }

                if (value < DxfColumnType.NoColumns || value > DxfColumnType.DynamicColumns)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A column type is one of AutoCAD's, 0 to 2.");
                }

                ColumnSet columns = Columns();

                if (columns.Type == DxfColumnType.NoColumns && value != DxfColumnType.NoColumns)
                {
                    columns.Count = Math.Max(columns.Count, 1);
                    columns.Width = columns.Width > 0.0 ? columns.Width : Width;
                    columns.Auto = value == DxfColumnType.DynamicColumns;
                }

                columns.Type = value;
                WriteColumns(columns.Pad(Height));
            }
        }

        /// <summary>
        /// Gets or sets how many columns the MTEXT has (group 72 of its embedded object, 76 of its extended data), as AutoCAD's
        /// <c>ColumnCount</c>: where the embedded object gives none, as for dynamic columns of auto height, worked out from the
        /// width of all the columns (42) as ezdxf works it out; nought where the MTEXT has no columns.
        /// </summary>
        /// <value>Set, 1 or more, written as <see cref="ColumnType"/> says; columns of their own heights given one of the MTEXT's
        /// <see cref="Height"/> for each column added.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count set is below 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or has no columns: give it some first.</exception>
        public int ColumnCount
        {
            get => Columns().Count;

            set
            {
                if (value == ColumnCount)
                {
                    return;
                }

                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "An MTEXT in columns has 1 column or more.");
                }

                ColumnSet columns = WithColumns();
                columns.Count = value;
                WriteColumns(columns.Pad(Height));
            }
        }

        /// <summary>Gets or sets how wide each column is (group 44 of its embedded object, 48 of its extended data), as AutoCAD's <c>ColumnWidth</c>; nought where it gives none.</summary>
        /// <value>Set, a finite number above nought, written as <see cref="ColumnType"/> says, the MTEXT's <see cref="Width"/> made
        /// the same, as <see cref="SetStaticColumns"/> makes it.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or has no columns: give it some first.</exception>
        public double ColumnWidth
        {
            get => Columns().Width;

            set
            {
                if (Same(value, ColumnWidth))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "The width of a column has to be a finite number above nought.");
                ColumnSet columns = WithColumns();
                columns.Width = value;
                Width = value;
                WriteColumns(columns);
            }
        }

        /// <summary>Gets or sets the space between the columns (group 45 of its embedded object, 49 of its extended data), as AutoCAD's <c>ColumnGutterWidth</c>; nought where it gives none.</summary>
        /// <value>Set, a finite number of nought or more, written as <see cref="ColumnType"/> says.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or has no columns: give it some first.</exception>
        public double ColumnGutterWidth
        {
            get => Columns().Gutter;

            set
            {
                if (Same(value, ColumnGutterWidth))
                {
                    return;
                }

                Guard.NonNegative(value, nameof(value), "The gutter between columns has to be a finite number of nought or more.");
                ColumnSet columns = WithColumns();
                columns.Gutter = value;
                WriteColumns(columns);
            }
        }

        /// <summary>
        /// Gets or sets whether the columns are all of one height, the MTEXT's <see cref="Height"/> (group 73 of its embedded
        /// object, 79 of its extended data), as AutoCAD's <c>ColumnAutoHeight</c>; false where it gives none.
        /// </summary>
        /// <value>Set, written as <see cref="ColumnType"/> says; dynamic columns set to heights of their own given the MTEXT's
        /// <see cref="Height"/> each, to be changed by <see cref="SetColumnHeight"/>.</value>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or has no columns: give it some first.</exception>
        public bool ColumnAutoHeight
        {
            get => Columns().Auto;

            set
            {
                if (value == ColumnAutoHeight)
                {
                    return;
                }

                ColumnSet columns = WithColumns();
                columns.Auto = value;
                WriteColumns(columns.Pad(Height));
            }
        }

        /// <summary>Gets or sets whether the columns run from right to left (group 74 of its embedded object, 78 of its extended data), as AutoCAD's <c>ColumnFlowReversed</c>; false where it gives none.</summary>
        /// <value>Set, written as <see cref="ColumnType"/> says.</value>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or has no columns: give it some first.</exception>
        public bool ColumnFlowReversed
        {
            get => Columns().Reversed;

            set
            {
                if (value == ColumnFlowReversed)
                {
                    return;
                }

                ColumnSet columns = WithColumns();
                columns.Reversed = value;
                WriteColumns(columns);
            }
        }

        /// <summary>
        /// Gets the height of a column (the groups 46 of its embedded object, the 1040 after a 50 of its extended data), as
        /// AutoCAD's <c>GetColumnHeight</c>: for columns of their own heights; nought for one the MTEXT gives no height.
        /// </summary>
        /// <param name="index">The column, from nought.</param>
        /// <returns>Its height.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought.</exception>
        public double GetColumnHeight(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A column is counted from nought.");
            }

            List<double> heights = Columns().Heights;
            return index < heights.Count ? heights[index] : 0.0;
        }

        /// <summary>
        /// Sets the height of a column of dynamic columns of their own heights, as AutoCAD's <c>SetColumnHeight</c>: the 46 of
        /// its place in the embedded object from R2018, the 1040 of its place after the 50 of the extended data of ACAD before.
        /// Columns of one height take the MTEXT's <see cref="Height"/> instead.
        /// </summary>
        /// <param name="index">The column, from nought to <see cref="ColumnCount"/> less one.</param>
        /// <param name="height">Its height, a finite number of nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a column, or the height is below
        /// nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or its columns are not dynamic ones of
        /// their own heights.</exception>
        public void SetColumnHeight(int index, double height)
        {
            Guard.NonNegative(height, nameof(height), "The height of a column has to be a finite number of nought or more.");
            ColumnSet columns = Columns();

            if (columns.Type != DxfColumnType.DynamicColumns || columns.Auto)
            {
                throw new InvalidOperationException("Only dynamic columns of their own heights (ColumnAutoHeight false) have a height each: the others take the MTEXT's Height.");
            }

            if (index < 0 || index >= columns.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The MTEXT has " + columns.Count.ToString(CultureInfo.InvariantCulture) + " columns, counted from nought.");
            }

            if (index < columns.Heights.Count && Same(columns.Heights[index], height))
            {
                return;
            }

            columns.Pad(Height).Heights[index] = height;
            WriteColumns(columns);
        }

        /// <summary>
        /// Sets the MTEXT in static columns, as AutoCAD's <c>SetStaticColumns</c>: as many columns as given, each as wide as
        /// given and as high as the MTEXT's <see cref="Height"/>, a gutter between them; written as <see cref="ColumnType"/>
        /// says, the MTEXT's <see cref="Width"/> (41, and 40 of its embedded object) made the column's, as AutoCAD and ezdxf make
        /// it. Three columns 12.5 wide with gutters of 2.5 take 42.5 across, the MTEXT 12.5 wide.
        /// </summary>
        /// <param name="width">How wide each column is, a finite number above nought.</param>
        /// <param name="gutter">The space between two columns, a finite number of nought or more.</param>
        /// <param name="count">How many columns, 1 or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a width is outside what it takes, or the count is below 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public void SetStaticColumns(double width, double gutter, int count)
        {
            Guard.Positive(width, nameof(width), "The width of a column has to be a finite number above nought.");
            Guard.NonNegative(gutter, nameof(gutter), "The gutter between columns has to be a finite number of nought or more.");

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "An MTEXT in columns has 1 column or more.");
            }

            ColumnSet columns = Columns();
            columns.Type = DxfColumnType.StaticColumns;
            columns.Width = width;
            columns.Gutter = gutter;
            columns.Count = count;
            columns.Auto = false;
            columns.Heights.Clear();
            Width = width;
            WriteColumns(columns);
        }

        /// <summary>
        /// Sets the MTEXT in dynamic columns, as AutoCAD's <c>SetDynamicColumns</c>: columns as wide as given, a gutter between
        /// them, which AutoCAD fills with the words as they flow; all as high as the MTEXT's <see cref="Height"/> where their
        /// height is automatic, else each of its own, given the MTEXT's <see cref="Height"/> to start with and set by
        /// <see cref="SetColumnHeight"/>. The count of columns is kept, 1 where the MTEXT had none; written as
        /// <see cref="ColumnType"/> says, the MTEXT's <see cref="Width"/> made the column's, as <see cref="SetStaticColumns"/> makes it.
        /// </summary>
        /// <param name="width">How wide each column is, a finite number above nought.</param>
        /// <param name="gutter">The space between two columns, a finite number of nought or more.</param>
        /// <param name="autoHeight">Whether the columns are all of one height.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a width is outside what it takes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public void SetDynamicColumns(double width, double gutter, bool autoHeight)
        {
            Guard.Positive(width, nameof(width), "The width of a column has to be a finite number above nought.");
            Guard.NonNegative(gutter, nameof(gutter), "The gutter between columns has to be a finite number of nought or more.");
            ColumnSet columns = Columns();
            columns.Type = DxfColumnType.DynamicColumns;
            columns.Width = width;
            columns.Gutter = gutter;
            columns.Count = Math.Max(columns.Count, 1);
            columns.Auto = autoHeight;
            Width = width;
            WriteColumns(columns.Pad(Height));
        }

        /// <summary>
        /// Gets or sets the distance from one line of the MTEXT to the next, as AutoCAD's <c>LineSpaceDistance</c>: the
        /// <see cref="LineSpacingFactor"/> times single spacing, which is 5/3 of the <see cref="TextHeight"/>, as AutoCAD spaces
        /// lines: 5 for letters 3 high at a factor of 1.
        /// </summary>
        /// <value>Set, written as the factor that gives it, which runs from 0.25 to 4: from 1.25 to 20 for letters 3 high.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance set is not a finite number, or gives a factor outside 0.25 to 4.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public double LineSpaceDistance
        {
            get => LineSpacingFactor * TextHeight * SingleSpacing;

            set
            {
                if (Same(value, LineSpaceDistance))
                {
                    return;
                }

                Guard.Finite(value, nameof(value), "A line spacing has to be a finite number.");
                double factor = value / (TextHeight * SingleSpacing);

                if (!(factor >= 0.25 && factor <= 4.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A line spacing runs from 0.25 to 4 times single spacing, 5/3 of the height of the letters.");
                }

                LineSpacingFactor = factor;
            }
        }

        /// <summary>
        /// Gets or sets whether a frame is drawn about the MTEXT (flag 16 of group 90), as AutoCAD's <c>ShowBorders</c>: the
        /// text frame of AutoCAD 2016 on, drawn in the colour of the words, as ezdxf reads and writes it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2004 (AC1018).</exception>
        public bool ShowBorders
        {
            get => (WholeNumber(90, 0) & 16) != 0;
            set => SetFill(16, value);
        }

        /// <summary>
        /// Gets or sets how see-through the background of the MTEXT is (group 441), as AutoCAD's <c>BackgroundTransparency</c>:
        /// written as group 440 of an entity is, 0x02000000 and the alpha for an alpha, 0x01000000 for by block; by layer where
        /// the record gives none. AutoCAD and BricsCAD keep it but draw nothing by it, as ezdxf notes.
        /// </summary>
        /// <value>Set, by layer taking the group out.</value>
        /// <exception cref="ArgumentException">Thrown when the transparency set is of no method AutoCAD writes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or set in a file older than R2007 (AC1021).</exception>
        public DxfTransparency BackgroundTransparency
        {
            get
            {
                int raw = WholeNumber(441, 0);

                switch ((raw >> 24) & 0xFF)
                {
                    case 2: return new DxfTransparency((byte)(raw & 0xFF));
                    case 1: return new DxfTransparency(DxfTransparencyMethod.ByBlock);
                    default: return new DxfTransparency(DxfTransparencyMethod.ByLayer);
                }
            }

            set
            {
                if (value == BackgroundTransparency)
                {
                    return;
                }

                string raw;

                switch (value.Method)
                {
                    case DxfTransparencyMethod.ByLayer: raw = null; break;
                    case DxfTransparencyMethod.ByBlock: raw = 0x01000000.ToString(CultureInfo.InvariantCulture); break;
                    case DxfTransparencyMethod.ByAlpha: raw = (0x02000000 | value.Alpha).ToString(CultureInfo.InvariantCulture); break;
                    default: throw new ArgumentException("A transparency is by layer, by block or of an alpha: " + value.Method + " is none of them.", nameof(value));
                }

                Since(1021, "transparency of an MTEXT's background");
                Set(new KeyValuePair<int, string>(441, raw));
            }
        }

        /// <summary>
        /// Holds the MTEXT by another of its points where it stands, as AutoCAD's <c>SetAttachmentMovingLocation</c>: its
        /// <see cref="Attachment"/> set and its <see cref="Location"/> moved to that point of its box, so that the words stay
        /// where they are. The box is <see cref="Width"/> wide, the <see cref="ActualWidth"/> where the MTEXT does not wrap, and
        /// <see cref="ActualHeight"/> high, as the file writes them, along its <see cref="Direction"/> and up from it in its
        /// plane: the MTEXT 40 wide held at its top left at (20, 40, 0), held at its top centre, stands at (40, 40, 0).
        /// </summary>
        /// <param name="value">The point to hold it by, one of the nine of an MTEXT.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the point is not one an MTEXT has, 1 to 9.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or the move needs a width or height the
        /// file does not give: an MTEXT with no width (41) and no width of its words (42), or no height of its words (43).</exception>
        public void SetAttachmentMovingLocation(DxfAttachmentPoint value)
        {
            if (value < DxfAttachmentPoint.TopLeft || value > DxfAttachmentPoint.BottomRight)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "An MTEXT is held by one of its nine points, 1 to 9.");
            }

            DxfAttachmentPoint now = Attachment;

            if (value == now || now < DxfAttachmentPoint.TopLeft || now > DxfAttachmentPoint.BottomRight)
            {
                Attachment = value;
                return;
            }

            int across = ((int)value - 1) % 3 - ((int)now - 1) % 3;
            int down = ((int)value - 1) / 3 - ((int)now - 1) / 3;
            double width = Width > 0.0 ? Width : Value(42) != null ? ActualWidth : double.NaN;
            double height = Value(43) != null ? ActualHeight : double.NaN;

            if (across != 0 && double.IsNaN(width))
            {
                throw new InvalidOperationException("The MTEXT gives no width (41) nor width of its words (42) to move it across by.");
            }

            if (down != 0 && double.IsNaN(height))
            {
                throw new InvalidOperationException("The MTEXT gives no height of its words (43) to move it up or down by.");
            }

            GeoVector3 along = Direction;
            GeoVector3 up = DxfOcsTransform.Cross(Normal, along);
            GeoVector3 shift = new GeoVector3(0.0, 0.0, 0.0);

            if (across != 0)
            {
                shift = shift.Add(along.Multiply(across * width / 2.0));
            }

            if (down != 0)
            {
                shift = shift.Add(up.Multiply(-down * height / 2.0));
            }

            GeoPoint3 location = Location;
            Set(Join(
                PointChanges(10, new GeoPoint3(location.X + shift.X, location.Y + shift.Y, location.Z + shift.Z), Origin),
                new[] { new KeyValuePair<int, string>(71, ((int)value).ToString(CultureInfo.InvariantCulture)) }));
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the MTEXT, as AutoCAD's <c>TransformBy</c> does, and as ezdxf's <c>transform()</c>
        /// carries one: its location and direction carried, its angle (50) given up for its direction, its height the stretch of
        /// its upright square to its direction, its width the stretch of its direction, the heights <c>\H</c> of its words scaled
        /// with it, its normal carried. A scale unlike along its direction and its upright stretches its box, as ezdxf stretches
        /// it; one that leans them is refused, an MTEXT having no lean.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the MTEXT, or shears it: its direction and
        /// upright no longer square.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // The identity leaves the MTEXT as it is: its angle is not turned into a direction.
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            // An MTEXT has no lean: a transformation that leans its frame is refused, before anything is written, as a circle
            // refuses one that makes it an ellipse.
            GeoVector3 frame = HasDirection() ? Point(11, Origin).ToVector() : Value(50) != null ? Direction : new GeoVector3(1.0, 0.0, 0.0);

            if (DxfOcsTransform.Shears(transform, Normal, frame))
            {
                throw new ArgumentException("The transformation shears the MTEXT, which cannot lean: its direction and its upright would no longer be square.", nameof(transform));
            }

            // A copy holds the words of its column MTEXTs before it is moved, as it is not moved with them; an MTEXT of a
            // drawing moves them with it.
            Unlink();
            List<DxfMText> linked = ColumnTexts();
            GeoVector3 normal = Normal;
            var ocs = new DxfOcsTransform(normal, transform);
            GeoVector3 oldDirection = HasDirection() ? Point(11, Origin).ToVector() : Value(50) != null ? Direction : new GeoVector3(1.0, 0.0, 0.0);
            GeoVector3 newDirection = transform.Transform(oldDirection);
            GeoVector3 upright = DxfOcsTransform.Cross(normal, oldDirection);
            double oldHeight = TextHeight;
            GeoVector3 newUpright = transform.Transform(upright.Multiply(oldHeight / upright.Length));
            double newHeight = newUpright.Length * Math.Sin(Between(newDirection, newUpright));

            if (!ocs.NewExtrusion.IsValid || !(newHeight > 0.0) || !Guard.IsFinite(newHeight) || !(newDirection.Length > 0.0))
            {
                throw new ArgumentException("The transformation flattens the MTEXT.", nameof(transform));
            }

            var groups = new List<KeyValuePair<int, string>>();

            if (!Same(newHeight, oldHeight))
            {
                groups.Add(new KeyValuePair<int, string>(40, Numbers.Exact(newHeight + 0.0)));
            }

            if (Value(41) != null)
            {
                double width = transform.Transform(oldDirection.Multiply(Width / oldDirection.Length)).Length;

                if (!Same(width, Width))
                {
                    groups.Add(new KeyValuePair<int, string>(41, Numbers.Exact(width + 0.0)));
                }
            }

            groups.AddRange(PointChanges(10, transform.Transform(Location), Origin));
            groups.AddRange(PointChanges(11, new GeoPoint3(newDirection.X, newDirection.Y, newDirection.Z), Origin));

            if (Value(50) != null)
            {
                groups.Add(new KeyValuePair<int, string>(50, null));
            }

            groups.AddRange(NormalChanges(ocs.NewExtrusion));

            // Columns are stretched as ezdxf stretches them: their widths along the direction, their heights along the upright.
            ColumnSet columns = Columns();
            double across = transform.Transform(oldDirection.Divide(oldDirection.Length)).Length;
            double up = transform.Transform(upright.Divide(upright.Length)).Length;
            bool inColumns = columns.Type != DxfColumnType.NoColumns && (columns.Embedded || columns.InXData);

            if (inColumns && Value(46) != null && !Same(Height * up, Height))
            {
                groups.Add(new KeyValuePair<int, string>(46, Numbers.Exact(Height * up + 0.0)));
            }

            string contents = Contents;
            string scaled = Math.Abs(oldHeight) > 1E-12 && !Close(oldHeight, newHeight) ? ScaledHeights(contents, newHeight / oldHeight) : contents;
            Set(groups.ToArray());
            Contents = scaled;

            if (inColumns)
            {
                columns.Width *= across;
                columns.Gutter *= across;
                columns.Defined = columns.Defined * up;

                for (int i = 0; i < columns.Heights.Count; i++)
                {
                    columns.Heights[i] *= up;
                }

                WriteColumns(columns, columns.Embedded);
            }

            foreach (DxfMText column in linked)
            {
                column.TransformBy(transform);
            }

            if (linked.Count > 0)
            {
                HeightToColumns();
            }
        }

        /// <summary>
        /// Erases the MTEXT, as AutoCAD's <c>Erase</c> does, as <see cref="DxfEntity.Erase"/> says; an MTEXT whose later
        /// columns are MTEXTs of their own (<c>ACAD_MTEXT_COLUMNS</c>, before R2018) takes them with it, as AutoCAD erases one
        /// text.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is in no file, or is erased already.</exception>
        public override void Erase()
        {
            List<DxfMText> linked = ColumnTexts();
            base.Erase();

            foreach (DxfMText column in linked)
            {
                column.Erase();
            }
        }

        /// <summary>
        /// The column MTEXTs the MTEXT links to (<c>ACAD_MTEXT_COLUMNS</c>), in the order it names them: those of its block
        /// with those handles, not erased; none for an MTEXT in no drawing, or one that links to none.
        /// </summary>
        private List<DxfMText> ColumnTexts()
        {
            var found = new List<DxfMText>();
            IReadOnlyList<string> handles = IsResident && !IsErased ? LinkedColumns() : new string[0];

            if (handles.Count == 0 || !(Owner is DxfBlockTableRecord block))
            {
                return found;
            }

            var columns = new Dictionary<string, DxfMText>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfMText mtext in block.AllEntities().OfType<DxfMText>())
            {
                if (mtext != this && mtext.Handle != null && !columns.ContainsKey(mtext.Handle))
                {
                    columns.Add(mtext.Handle, mtext);
                }
            }

            foreach (string handle in handles)
            {
                if (columns.TryGetValue(handle, out DxfMText column) && !found.Contains(column))
                {
                    found.Add(column);
                }
            }

            return found;
        }

        /// <summary>
        /// Makes a change to the MTEXT's place, direction or normal, and moves its column MTEXTs by the same change of its
        /// system, its location, direction and normal taken as an origin and axes; nothing more where the change leaves them
        /// as they were, to the bit.
        /// </summary>
        private void Carried(Action change)
        {
            List<DxfMText> linked = ColumnTexts();

            if (linked.Count == 0)
            {
                change();
                return;
            }

            GeoPoint3 location = Location;
            GeoVector3 direction = Direction;
            GeoVector3 normal = Normal;
            GeoTransform3 before = Frame();
            change();
            GeoPoint3 newLocation = Location;
            GeoVector3 newDirection = Direction;
            GeoVector3 newNormal = Normal;

            if (Same(location.X, newLocation.X) && Same(location.Y, newLocation.Y) && Same(location.Z, newLocation.Z)
                && Same(direction.X, newDirection.X) && Same(direction.Y, newDirection.Y) && Same(direction.Z, newDirection.Z)
                && Same(normal.X, newNormal.X) && Same(normal.Y, newNormal.Y) && Same(normal.Z, newNormal.Z))
            {
                return;
            }

            if (!before.TryGetInverse(out GeoTransform3 undo, Tolerance.Default))
            {
                return;
            }

            GeoTransform3 move = Frame().Multiply(undo);

            foreach (DxfMText column in linked)
            {
                column.TransformBy(move);
            }
        }

        /// <summary>The system of the MTEXT: its location the origin, its direction x, its normal z.</summary>
        private GeoTransform3 Frame()
        {
            GeoVector3 z = Normal;
            GeoVector3 x = Direction;
            GeoVector3 y = DxfOcsTransform.Cross(z, x);
            GeoPoint3 o = Location;
            return new GeoTransform3(new double[,]
            {
                { x.X, y.X, z.X, o.X },
                { x.Y, y.Y, z.Y, o.Y },
                { x.Z, y.Z, z.Z, o.Z },
                { 0.0, 0.0, 0.0, 1.0 },
            });
        }

        /// <summary>
        /// Gives the column MTEXTs the height of the MTEXT's columns of one height, as ezdxf's <c>set_linked_columns_xdata</c>
        /// writes them: their own <c>ACAD_MTEXT_DEFINED_HEIGHT</c>, and their group 46 where the MTEXT has one; for columns of
        /// their own heights, their <c>ACAD_MTEXT_DEFINED_HEIGHT</c> taken out. Nothing where they are as that already.
        /// </summary>
        private void HeightToColumns()
        {
            List<DxfMText> linked = ColumnTexts();
            ColumnSet columns = linked.Count == 0 ? null : Columns();

            if (columns == null || columns.Type == DxfColumnType.NoColumns)
            {
                return;
            }

            if (columns.Type == DxfColumnType.DynamicColumns && !columns.Auto)
            {
                foreach (DxfMText column in linked)
                {
                    List<DxfTypedValue> data = (column.GetXDataForApplication("ACAD") ?? new DxfTypedValue[0]).Skip(1).ToList();

                    if (Cut(data, "ACAD_MTEXT_DEFINED_HEIGHT") >= 0)
                    {
                        column.XData = new[] { new DxfTypedValue(1001, "ACAD") }.Concat(data).ToList();
                    }
                }

                return;
            }

            double height = Height;
            bool group = Value(46) != null;

            foreach (DxfMText column in linked)
            {
                if (group && !Same(column.Number(46, double.NaN), height))
                {
                    column.Set(new KeyValuePair<int, string>(46, Numbers.Exact(height + 0.0)));
                }

                List<DxfTypedValue> data = (column.GetXDataForApplication("ACAD") ?? new DxfTypedValue[0]).Skip(1).ToList();
                int at = Find(data, "ACAD_MTEXT_DEFINED_HEIGHT");
                int end = at < 0 ? -1 : End(data, at);
                bool same = at >= 0 && data.GetRange(at, end - at + 1).Any(g => g.TypeCode == 1040 && Numbers.TryRead(g.Value, out double now) && Same(now, height));

                if (same)
                {
                    continue;
                }

                var block = new[]
                {
                    new DxfTypedValue(1000, "ACAD_MTEXT_DEFINED_HEIGHT_BEGIN"),
                    new DxfTypedValue(1070, "46"),
                    new DxfTypedValue(1040, Numbers.Exact(height + 0.0)),
                    new DxfTypedValue(1000, "ACAD_MTEXT_DEFINED_HEIGHT_END"),
                };

                if (at >= 0)
                {
                    data.RemoveRange(at, end - at + 1);
                }

                data.InsertRange(at >= 0 ? at : data.Count, block);
                column.XData = new[] { new DxfTypedValue(1001, "ACAD") }.Concat(data).ToList();
            }
        }

        /// <summary>
        /// Refuses a text style the file of the block has not, unless the MTEXT is a copy of another drawing that has it, which
        /// is copied with it, as a <see cref="DxfDBText"/> refuses one. Writes the columns of an MTEXT made in code, or copied
        /// from another drawing, in the form of the drawing it is added to: from R2018 in its embedded object, before it in the
        /// extended data of ACAD, as <see cref="ColumnType"/> says; a copy of one whose later columns are MTEXTs of their own
        /// holding their words itself first (<see cref="Unlink"/>).
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            CheckTextStyle(owner.File, Item.File.Origin, Value(7), "entity");
            Unlink();
            ColumnSet columns = Columns();

            if (owner.File != null && columns.Type != DxfColumnType.NoColumns && owner.File.Supports(1032) != columns.Embedded)
            {
                WriteColumns(columns, owner.File.Supports(1032));
            }
        }

        /// <summary>
        /// The TEXTs the MTEXT is written as in a drawing of R12 (<paramref name="target"/>), which has no MTEXT, each in no file
        /// with the MTEXT's look: one for each paragraph (<c>\P</c>, a column break <c>\N</c> taken as one), not wrapped at its
        /// width, which takes the letters of its font; its words without their formatting as <see cref="Text"/> reads them, a
        /// height of a code in the words left out; <see cref="TextHeight"/> high, along its
        /// <see cref="Direction"/> in the plane of its <see cref="Normal"/>, the lines <see cref="LineSpaceDistance"/> apart, the
        /// whole of them standing where its <see cref="Attachment"/> holds them, and each held at the left, the middle or the right
        /// of its baseline as the MTEXT is held; in its text style where that drawing has it, else in Standard (in its own, with
        /// no drawing given). A line with no words is left out, its place kept.
        /// </summary>
        internal List<DxfEntity> R12Texts(DxfFile target)
        {
            List<string> lines = Paragraphs();
            double height = TextHeight;
            double pitch = LineSpaceDistance;
            GeoVector3 normal = Normal;
            GeoVector3 up = DxfOcsTransform.Normalized(normal.CrossProduct(Direction));
            int attachment = (int)Attachment;
            attachment = attachment >= 1 && attachment <= 9 ? attachment - 1 : 0;
            int row = attachment / 3;
            int column = attachment % 3;
            double first = row == 0 ? -height : row == 1 ? (height + (lines.Count - 1) * pitch) / 2.0 - height : (lines.Count - 1) * pitch;
            string style = Value(7);
            DxfFile source = IsResident ? Item.File : Item.File.Origin;

            // The style is kept where the drawing has it, or is given it by the copying, by name; else Standard.
            style = style != null && (target == null
                || target.TableItems("STYLE").Exists(s => string.Equals(DxfReader.NameOf(s.File.ValueOf(s, 2) ?? string.Empty), DxfReader.NameOf(style), StringComparison.OrdinalIgnoreCase))
                || target.CopiesRecordFrom(source, "STYLE", style)) ? style : null;
            double degrees = DxfOcsTransform.Degrees(Rotation);
            GeoPoint3 location = Location;
            var texts = new List<DxfEntity>();

            for (int k = 0; k < lines.Count; k++)
            {
                if (lines[k].Trim().Length == 0)
                {
                    continue;
                }

                double rise = first - k * pitch;
                GeoPoint3 at = WorldToOcs(normal, new GeoPoint3(location.X + up.X * rise, location.Y + up.Y * rise, location.Z + up.Z * rise));
                var own = new List<KeyValuePair<int, string>>
                {
                    G(100, "AcDbText"), G(10, Numbers.Exact(at.X + 0.0)), G(20, Numbers.Exact(at.Y + 0.0)), G(30, Numbers.Exact(at.Z + 0.0)),
                    G(40, Numbers.Exact(height + 0.0)), G(1, lines[k]), G(50, Numbers.Exact(degrees + 0.0)),
                };

                if (style != null)
                {
                    own.Add(G(7, style));
                }

                if (column != 0)
                {
                    own.Add(G(72, column.ToString(CultureInfo.InvariantCulture)));
                    own.Add(G(11, Numbers.Exact(at.X + 0.0)));
                    own.Add(G(21, Numbers.Exact(at.Y + 0.0)));
                    own.Add(G(31, Numbers.Exact(at.Z + 0.0)));
                }

                own.Add(G(210, Numbers.Exact(normal.X + 0.0)));
                own.Add(G(220, Numbers.Exact(normal.Y + 0.0)));
                own.Add(G(230, Numbers.Exact(normal.Z + 0.0)));
                own.Add(G(100, "AcDbText"));
                texts.Add(Of(Item.File.TextOf(Item, own)));
            }

            return texts;
        }

        private static KeyValuePair<int, string> G(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>
        /// The paragraphs of the MTEXT's words without their formatting, as <see cref="Text"/> reads them, a column break
        /// (<c>\N</c>) beginning one as a new paragraph (<c>\P</c>) does; blank paragraphs at the end left out, as ezdxf leaves
        /// them out, so that <c>a\P\Pb\P</c> is the three lines a, nothing and b.
        /// </summary>
        private List<string> Paragraphs()
        {
            var record = new DxfReader.Record("MTEXT", 0);
            record.Add(10, "0");
            record.Add(20, "0");
            record.Add(40, "1");

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == 101)
                {
                    break;
                }

                if (group.TypeCode == 3 || group.TypeCode == 1)
                {
                    record.Add(group.TypeCode, ColumnBreaksAsParagraphs(group.Value));
                }
            }

            string text = string.Empty;

            foreach (object shape in DxfReader.ShapesOf(record, Tolerance.Default))
            {
                if (shape is DxfText read)
                {
                    text = read.Value;
                    break;
                }
            }

            var lines = new List<string>(text.Split('\n'));

            while (lines.Count > 1 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return lines;
        }

        /// <summary>Words with each column break (<c>\N</c>) written as a new paragraph (<c>\P</c>), an escaped backslash (<c>\\</c>) kept as it is.</summary>
        private static string ColumnBreaksAsParagraphs(string words)
        {
            var made = new StringBuilder(words.Length);

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == '\\' && i + 1 < words.Length && (words[i + 1] == '\\' || words[i + 1] == 'N'))
                {
                    made.Append(words[i + 1] == 'N' ? "\\P" : "\\\\");
                    i++;
                    continue;
                }

                made.Append(words[i]);
            }

            return made.ToString();
        }

        /// <summary>
        /// Gets the texts the MTEXT breaks into, as AutoCAD's <c>Explode</c> does: one TEXT for each paragraph (<c>\P</c>, a column
        /// break <c>\N</c> taken as one), its formatting and the heights of its codes left out as <see cref="Text"/> leaves them
        /// out, as an MTEXT is written into a drawing of R12 (see <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>): each
        /// in its text style, its height, turn and plane, the lines spaced as <see cref="LineSpaceDistance"/> says. A paragraph is
        /// not wrapped at <see cref="Width"/>, as AutoCAD wraps it by the letters of its font, which the library does not read, and
        /// a part of it in another font or height is not given a TEXT of its own, as AutoCAD gives it. B-12 and M16 on two lines, 2.5 high, held at the
        /// top left at (10, 20), come out as two TEXTs on the baselines 17.5 and 13.33 high.
        /// </summary>
        /// <returns>The <see cref="DxfDBText"/> objects, in no file, to be added to a drawing by
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode() => R12Texts(null).AsReadOnly();

        [DxfType("MTEXT")]
        internal static DxfEntity Create(DxfItem item) => new DxfMText(item);

        /// <summary>A new MTEXT in no file of the groups an ATTRIB or ATTDEF embeds of one (those after its 101), as AutoCAD's <c>MTextAttribute</c> gives it.</summary>
        internal static DxfMText OfEmbedded(IEnumerable<DxfTypedValue> groups)
            => new DxfMText(new[] { new DxfTypedValue(100, "AcDbMText") }.Concat(groups));

        /// <summary>
        /// The handles of the column MTEXTs the MTEXT links to, the 1005s of its <c>ACAD_MTEXT_COLUMNS</c>, as a file before
        /// R2018 writes the columns after its first; empty where it links to none.
        /// </summary>
        private IReadOnlyList<string> LinkedColumns()
        {
            List<DxfTypedValue> data = (GetXDataForApplication("ACAD") ?? new DxfTypedValue[0]).ToList();
            int at = Find(data, "ACAD_MTEXT_COLUMNS");
            return at < 0 ? new string[0] : data.GetRange(at, End(data, at) - at + 1).Where(g => g.TypeCode == 1005).Select(g => g.Value.Trim()).ToList();
        }

        /// <summary>
        /// Makes a copy in no file, one of an MTEXT whose later columns are MTEXTs of their own (<c>ACAD_MTEXT_COLUMNS</c>, before
        /// R2018), hold their words itself and link to none of them, so that no two MTEXTs claim the same columns: the words of
        /// each column MTEXT of the drawing it was copied from put after its own, after a column break (<c>\N</c>) for static
        /// columns, run on for dynamic ones; its columns kept. Nothing for an MTEXT of a file, or one that links to none.
        /// </summary>
        internal void Unlink()
        {
            IReadOnlyList<string> handles = Item.File.IsDetached ? LinkedColumns() : new string[0];

            if (handles.Count == 0)
            {
                return;
            }

            var words = new List<string> { Contents };
            DxfDatabase origin = Item.File.Origin?.Database;

            if (origin != null)
            {
                var columns = new Dictionary<string, DxfMText>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfBlockTableRecord block in origin.BlockTable)
                {
                    foreach (DxfMText mtext in block.AllEntities().OfType<DxfMText>())
                    {
                        if (mtext.Handle != null && !columns.ContainsKey(mtext.Handle))
                        {
                            columns.Add(mtext.Handle, mtext);
                        }
                    }
                }

                foreach (string handle in handles)
                {
                    if (columns.TryGetValue(handle, out DxfMText column) && !column.IsErased)
                    {
                        words.Add(column.Contents);
                    }
                }
            }

            bool breaks = Columns().Type == DxfColumnType.StaticColumns;

            for (int i = 0; breaks && i < words.Count - 1; i++)
            {
                words[i] = words[i].EndsWith("\\N", StringComparison.Ordinal) ? words[i] : words[i] + "\\N";
            }

            List<DxfTypedValue> data = GetXDataForApplication("ACAD").Skip(1).ToList();
            Cut(data, "ACAD_MTEXT_COLUMNS");
            XData = new[] { new DxfTypedValue(1001, "ACAD") }.Concat(data).ToList();
            Contents = string.Concat(words);
        }

        /// <summary>The text style of an MTEXT as its group 7 writes it, escapes and all; Standard where it gives none.</summary>
        internal static string StyleOf(DxfMText mtext) => mtext.Value(7) ?? "Standard";

        /// <summary>Gives an MTEXT the text style of a name as a group 7 writes it; nothing where it has it.</summary>
        internal static void SetStyleOf(DxfMText mtext, string style)
        {
            if (!string.Equals(mtext.Value(7) ?? "Standard", style, StringComparison.Ordinal))
            {
                mtext.Set(new KeyValuePair<int, string>(7, style));
            }
        }

        /// <summary>The MTEXT's own groups, after its marker and before an embedded object of its columns (101), as an ATTRIB or ATTDEF embeds them.</summary>
        internal static List<DxfTypedValue> OwnOf(DxfMText mtext)
            => mtext.GetOwnGroups(0).Skip(1).TakeWhile(g => g.TypeCode != 101).ToList();

        /// <summary>
        /// The groups an ATTRIB or ATTDEF embeds of an MTEXT, after its 101: the MTEXT's own, and those AutoCAD always writes
        /// put in where it has none, as ezdxf puts them: its location, height 2.5, width and box height nought, held at its top
        /// left, flowing by its style, the style Standard, and single spacing at least.
        /// </summary>
        internal static List<DxfTypedValue> EmbeddedOf(DxfMText mtext)
        {
            List<DxfTypedValue> own = OwnOf(mtext);
            var defaults = new[]
            {
                new KeyValuePair<int, string>(10, "0"), new KeyValuePair<int, string>(20, "0"), new KeyValuePair<int, string>(30, "0"),
                new KeyValuePair<int, string>(40, "2.5"), new KeyValuePair<int, string>(41, "0"), new KeyValuePair<int, string>(46, "0"),
                new KeyValuePair<int, string>(71, "1"), new KeyValuePair<int, string>(72, "5"), new KeyValuePair<int, string>(7, "Standard"),
                new KeyValuePair<int, string>(73, "1"), new KeyValuePair<int, string>(44, "1"),
            };

            foreach (KeyValuePair<int, string> group in defaults)
            {
                if (own.FindIndex(g => g.TypeCode == group.Key) < 0)
                {
                    Put(own, group);
                }
            }

            return own;
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The words with every height <c>\H</c> given in drawing units scaled, as ezdxf scales them: to three figures; one against the height (<c>\H1.5x;</c>) kept.</summary>
        internal static string ScaledHeights(string contents, double factor)
        {
            if (contents.IndexOf("\\H", StringComparison.Ordinal) < 0)
            {
                return contents;
            }

            factor = Math.Abs(factor);
            string[] parts = contents.Split(new[] { "\\H" }, StringSplitOptions.None);
            var scaled = new StringBuilder(parts[0]);

            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                int index = 0;

                while (index < part.Length && ((part[index] >= '0' && part[index] <= '9') || part[index] == '.'))
                {
                    index++;
                }

                scaled.Append("\\H");

                if (index < part.Length && part[index] == 'x')
                {
                    scaled.Append(part);
                    continue;
                }

                if (double.TryParse(part.Substring(0, index), NumberStyles.Float, CultureInfo.InvariantCulture, out double size))
                {
                    scaled.Append(General(size * factor));
                }

                scaled.Append(part, index, part.Length - index);
            }

            return scaled.ToString();
        }

        /// <summary>A number to three figures as Python's <c>%.3g</c> writes it.</summary>
        private static string General(double value)
        {
            if (value == 0.0)
            {
                return "0";
            }

            string e = value.ToString("E2", CultureInfo.InvariantCulture);
            int exponent = int.Parse(e.Substring(e.IndexOf('E') + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);

            if (exponent < -4 || exponent >= 3)
            {
                string mantissa = e.Substring(0, e.IndexOf('E'));
                mantissa = mantissa.IndexOf('.') >= 0 ? mantissa.TrimEnd('0').TrimEnd('.') : mantissa;
                return mantissa + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
            }

            string fixedPoint = value.ToString("F" + Math.Max(0, 2 - exponent).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return fixedPoint.IndexOf('.') >= 0 ? fixedPoint.TrimEnd('0').TrimEnd('.') : fixedPoint;
        }

        private static double Turn(double angle)
        {
            const double Round = 2.0 * Math.PI;
            double turned = angle % Round;
            turned = turned < 0.0 ? turned + Round : turned;
            return turned >= Round ? turned - Round : turned + 0.0;
        }

        private static double Between(GeoVector3 a, GeoVector3 b)
        {
            double cos = a.DotProduct(b) / (a.Length * b.Length);
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos)));
        }

        private static bool Close(double a, double b) => Math.Abs(a - b) <= Math.Max(1E-9 * Math.Max(Math.Abs(a), Math.Abs(b)), 0.0);

        /// <summary>A direction of the world in the system of a normal's plane, as the reader takes one there.</summary>
        private static GeoVector3 Plane(GeoVector3 normal, GeoVector3 direction)
        {
            GeoPoint3 p = WorldToOcs(normal, new GeoPoint3(direction.X, direction.Y, direction.Z));
            return new GeoVector3(p.X, p.Y, p.Z);
        }

        /// <summary>A direction of the system of a normal's plane in the world.</summary>
        private static GeoVector3 World(GeoVector3 normal, GeoVector3 direction)
        {
            GeoPoint3 p = OcsToWorld(normal, new GeoPoint3(direction.X, direction.Y, direction.Z));
            return new GeoVector3(p.X, p.Y, p.Z);
        }

        private bool HasDirection() => Value(11) != null || Value(21) != null || Value(31) != null;

        /// <summary>
        /// The text of the first group of a code of the MTEXT itself: in its own part, before an embedded object (group 101),
        /// which repeats the location, direction and sizes for its columns; null where it has none.
        /// </summary>
        private string Value(int code)
        {
            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == 101)
                {
                    break;
                }

                if (group.TypeCode == code && code != 100)
                {
                    return group.Value;
                }
            }

            return null;
        }

        private double Number(int code, double fallback) => Numbers.TryRead(Value(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int WholeNumber(int code, int fallback) => int.TryParse(Value(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private bool TryNumber(int code, out double value)
        {
            if (Numbers.TryRead(Value(code), out value) && Guard.IsFinite(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }

        /// <summary>The point of a code and the two above it, as the reader reads one: a coordinate left out nought, the fallback where all are.</summary>
        private GeoPoint3 Point(int code, GeoPoint3 fallback)
        {
            bool x = TryNumber(code, out double px);
            bool y = TryNumber(code + 10, out double py);
            bool z = TryNumber(code + 20, out double pz);
            return x || y || z ? new GeoPoint3(px, py, pz) : fallback;
        }

        /// <summary>The normal (210, 220, 230) as the reader reads it: of unit length, (0, 0, 1) where the record gives none.</summary>
        private GeoVector3 MNormal()
        {
            bool any = TryNumber(210, out double x) | TryNumber(220, out double y) | TryNumber(230, out double z);
            double length = Math.Sqrt(x * x + y * y + z * z);

            if (!any || !(length > 0.0) || double.IsInfinity(length))
            {
                return new GeoVector3(0.0, 0.0, 1.0);
            }

            return Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(x, y, z) : new GeoVector3(x / length, y / length, z / length);
        }

        /// <summary>The groups that set a point: the coordinates that differ from those read, to the bit, and those left out with them.</summary>
        private KeyValuePair<int, string>[] PointChanges(int code, GeoPoint3 value, GeoPoint3 fallback)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }

            GeoPoint3 now = Point(code, fallback);
            return Changes(code, new[] { value.X, value.Y, value.Z }, new[] { now.X, now.Y, now.Z });
        }

        /// <summary>The groups that set the normal: none where it is the one read; else each that differs from the number the record holds.</summary>
        private KeyValuePair<int, string>[] NormalChanges(GeoVector3 value)
        {
            double length = value.Length;

            if (!value.IsValid || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A normal has to be a direction of finite numbers and some length: " + value + " is not.", nameof(value));
            }

            GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
            GeoVector3 now = MNormal();

            if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
            {
                return new KeyValuePair<int, string>[0];
            }

            TryNumber(210, out double x);
            TryNumber(220, out double y);
            TryNumber(230, out double z);
            return Changes(210, new[] { unit.X, unit.Y, unit.Z }, new[] { x, y, z });
        }

        private KeyValuePair<int, string>[] Changes(int code, double[] values, double[] now)
        {
            var changes = new List<KeyValuePair<int, string>>();
            bool differs = false;

            for (int i = 0; i < values.Length; i++)
            {
                differs |= !Same(values[i], now[i]);
            }

            for (int i = 0; differs && i < values.Length; i++)
            {
                int at = code + 10 * i;

                if (!Same(values[i], now[i]) || Value(at) == null)
                {
                    changes.Add(new KeyValuePair<int, string>(at, Numbers.Exact(values[i] + 0.0)));
                }
            }

            return changes.ToArray();
        }

        private void SetAt(int code, GeoPoint3 value, GeoPoint3 fallback) => Set(PointChanges(code, value, fallback));

        private void SetNumber(int code, double value, double fallback)
        {
            Guard.Finite(value, nameof(value), "The number has to be a finite number.");

            if (!Same(value, Number(code, fallback)))
            {
                Set(new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)));
            }
        }

        private void SetWhole(int code, int value, int fallback)
        {
            if (value != WholeNumber(code, fallback))
            {
                Set(new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// Sets groups of the MTEXT itself, as <see cref="SetOwn"/> does; then, where the MTEXT is in columns, the values its
        /// columns repeat, its attachment, direction, location, width and height in the embedded object or the height in the
        /// extended data, written to agree.
        /// </summary>
        private void Set(params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length == 0)
            {
                return;
            }

            SetOwn(groups);
            ColumnSet columns = Columns();

            if (columns.Type != DxfColumnType.NoColumns && (columns.Embedded || columns.InXData))
            {
                WriteColumns(columns, columns.Embedded);
            }
        }

        /// <summary>
        /// Sets groups of the MTEXT itself in one writing: as <see cref="DxfEntity"/> sets them, or, where a code the MTEXT
        /// itself lacks is in its embedded object, by writing its own part anew with the embedded object kept as it is.
        /// </summary>
        private void SetOwn(KeyValuePair<int, string>[] groups)
        {
            List<DxfTypedValue> own = new List<DxfTypedValue>(GetOwnGroups(0));
            int embedded = own.FindIndex(g => g.TypeCode == 101);

            if (embedded < 0 || own.Count == 0 || own[0].TypeCode != 100)
            {
                SetGroups(groups);
                return;
            }

            List<DxfTypedValue> head = own.GetRange(1, embedded - 1);
            bool misplaced = false;

            foreach (KeyValuePair<int, string> group in groups)
            {
                misplaced |= head.FindIndex(g => g.TypeCode == group.Key) < 0 && own.FindIndex(embedded, g => g.TypeCode == group.Key) >= 0;
            }

            if (!misplaced)
            {
                SetGroups(groups);
                return;
            }

            foreach (KeyValuePair<int, string> group in groups)
            {
                Put(head, group);
            }

            var codes = new HashSet<int>();
            var run = new List<KeyValuePair<int, string>>();

            for (int i = 1; i < own.Count; i++)
            {
                codes.Add(own[i].TypeCode);
            }

            foreach (DxfTypedValue group in head)
            {
                run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            for (int i = embedded; i < own.Count; i++)
            {
                run.Add(new KeyValuePair<int, string>(own[i].TypeCode, own[i].Value));
            }

            ReplaceGroups(0, codes, run.ToArray());
        }

        /// <summary>Puts a group in the MTEXT's own part: in the place of the first of its code, or where the order puts it; out where its value is null.</summary>
        private static void Put(List<DxfTypedValue> part, KeyValuePair<int, string> group)
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

            int rank = Rank(group.Key);
            int put = 0;

            for (int i = 0; i < part.Count; i++)
            {
                int their = Rank(part[i].TypeCode);

                if (their >= 0 && their < rank)
                {
                    put = i + 1;
                }
            }

            part.Insert(put, DxfTypedValue.Read(group.Key, group.Value));
        }

        private static int Rank(int code)
        {
            for (int k = 1; k < Order.Count; k++)
            {
                if (Order[k] == code)
                {
                    return k;
                }
            }

            return -1;
        }

        /// <summary>The groups of the MTEXT's embedded object (101 on), which set it in columns from R2018; empty where it has none.</summary>
        private List<DxfTypedValue> EmbeddedGroups()
        {
            var tail = new List<DxfTypedValue>();
            bool embedded = false;

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                embedded |= group.TypeCode == 101;

                if (embedded)
                {
                    tail.Add(group);
                }
            }

            return tail;
        }

        /// <summary>
        /// The columns of the MTEXT as a list of codes and values: those of its embedded object from R2018 (71 type, 72 count, 73
        /// auto height, 74 reversed, 44 width, 45 gutter, 46 each height), else those of its extended data of ACAD between
        /// ACAD_MTEXT_COLUMN_INFO_BEGIN and _END, written there as a 1070 naming the code (75 type, 76 count, 79 auto height, 78
        /// reversed, 48 width, 49 gutter, 50 the count of heights) and the value after it, as ezdxf reads them.
        /// </summary>
        private ColumnSet Columns()
        {
            var read = new ColumnSet();
            List<DxfTypedValue> embedded = EmbeddedGroups();

            if (embedded.Count > 0)
            {
                read.Embedded = true;
                bool counted = false;
                double total = 0.0;

                foreach (DxfTypedValue group in embedded)
                {
                    switch (group.TypeCode)
                    {
                        case 71: read.Type = (DxfColumnType)Whole(group.Value); break;
                        case 72: read.Count = Whole(group.Value); counted = true; break;
                        case 73: read.Auto = Whole(group.Value) != 0; break;
                        case 74: read.Reversed = Whole(group.Value) != 0; break;
                        case 44: read.Width = Real(group.Value); break;
                        case 45: read.Gutter = Real(group.Value); break;
                        case 46: read.Heights.Add(Real(group.Value)); break;
                        case 41: read.Defined = read.Defined ?? Real(group.Value); break;
                        case 42: total = Real(group.Value); break;
                    }
                }

                // No count, as AutoCAD writes for dynamic columns of auto height: from the heights, else the width of all the
                // columns, as ezdxf works it out.
                if (counted && read.Count == 0)
                {
                    double each = Math.Abs(read.Width + read.Gutter);
                    read.Count = read.Heights.Count > 0 ? read.Heights.Count : total > 0.0 && each > 1E-6 ? (int)Math.Round((total + read.Gutter) / each, MidpointRounding.ToEven) : 0;
                }

                return read;
            }

            IReadOnlyList<DxfTypedValue> acad = GetXDataForApplication("ACAD");
            bool inside = false;
            bool defined = false;
            int? code = null;
            int heights = 0;

            foreach (DxfTypedValue group in acad ?? new DxfTypedValue[0])
            {
                if (group.TypeCode == 1000)
                {
                    string name = group.Value.Trim();
                    inside = name == "ACAD_MTEXT_COLUMN_INFO_BEGIN" || (inside && name != "ACAD_MTEXT_COLUMN_INFO_END");
                    defined = name == "ACAD_MTEXT_DEFINED_HEIGHT_BEGIN" || (defined && name != "ACAD_MTEXT_DEFINED_HEIGHT_END");
                    read.InXData |= inside;
                    continue;
                }

                if (defined)
                {
                    read.Defined = group.TypeCode == 1040 ? Real(group.Value) : read.Defined;
                    continue;
                }

                if (!inside)
                {
                    continue;
                }

                if (heights > 0 && group.TypeCode == 1040)
                {
                    read.Heights.Add(Real(group.Value));
                    heights--;
                    continue;
                }

                if (code == null)
                {
                    code = Whole(group.Value);
                    continue;
                }

                switch (code.Value)
                {
                    case 75: read.Type = (DxfColumnType)Whole(group.Value); break;
                    case 76: read.Count = Whole(group.Value); break;
                    case 79: read.Auto = Whole(group.Value) != 0; break;
                    case 78: read.Reversed = Whole(group.Value) != 0; break;
                    case 48: read.Width = Real(group.Value); break;
                    case 49: read.Gutter = Real(group.Value); break;
                    case 50: heights = Whole(group.Value); break;
                }

                code = null;
            }

            return read;
        }

        /// <summary>The columns of the MTEXT, to be changed; refused where it has none.</summary>
        private ColumnSet WithColumns()
        {
            ColumnSet columns = Columns();

            if (columns.Type == DxfColumnType.NoColumns)
            {
                throw new InvalidOperationException("The MTEXT is in no columns: give it some first, by ColumnType, SetStaticColumns or SetDynamicColumns.");
            }

            return columns;
        }

        /// <summary>
        /// Writes the columns of the MTEXT in the form of its file's version, as <see cref="ColumnType"/> says, the other form
        /// taken out; none, or <see cref="DxfColumnType.NoColumns"/>, takes both out. Nothing is written where they are as
        /// they were: a value of the record the same as the one to be written keeps its text.
        /// </summary>
        private void WriteColumns(ColumnSet columns) => WriteColumns(columns, Item.File.Supports(1032));

        private void WriteColumns(ColumnSet columns, bool embedded)
        {
            if (columns != null && columns.Type == DxfColumnType.NoColumns)
            {
                columns = null;
            }

            WriteEmbedded(embedded ? columns : null);
            WriteColumnData(embedded ? null : columns);
            HeightToColumns();
        }

        /// <summary>
        /// Writes the columns in the MTEXT's embedded object as ezdxf and AutoCAD write it from R2018: its attachment (70),
        /// direction (10) and location (11) and width (40) as the MTEXT's own, its column height (41) the MTEXT's
        /// <see cref="Height"/>, the width (42) and height (43) of all the columns, then the columns themselves; none takes the
        /// embedded object out.
        /// </summary>
        private void WriteEmbedded(ColumnSet columns)
        {
            List<DxfTypedValue> own = new List<DxfTypedValue>(GetOwnGroups(0));
            int at = own.FindIndex(g => g.TypeCode == 101);

            if (own.Count == 0 || own[0].TypeCode != 100 || (columns == null && at < 0))
            {
                return;
            }

            List<DxfTypedValue> tail = at < 0 ? new List<DxfTypedValue> { DxfTypedValue.Read(101, "Embedded Object") } : own.GetRange(at, own.Count - at);
            int end = at < 0 ? own.Count : at;

            if (columns != null)
            {
                GeoVector3 direction = Direction;
                GeoPoint3 location = Location;
                double defined = Value(46) != null ? Height : columns.Defined ?? 0.0;
                int count = columns.Count;
                double total = count > 0 ? count * columns.Width + (count - 1) * columns.Gutter : 0.0;
                bool manual = columns.Type == DxfColumnType.DynamicColumns && !columns.Auto;
                double height = manual ? (columns.Heights.Count > 0 ? columns.Heights.Max() : 0.0) : defined;

                PutWhole(tail, 70, (int)Attachment);
                PutReal(tail, 10, direction.X);
                PutReal(tail, 20, direction.Y);
                PutReal(tail, 30, direction.Z);
                PutReal(tail, 11, location.X);
                PutReal(tail, 21, location.Y);
                PutReal(tail, 31, location.Z);
                PutReal(tail, 40, Width);
                PutReal(tail, 41, defined);
                PutReal(tail, 42, total);
                PutReal(tail, 43, height);
                PutWhole(tail, 71, (int)columns.Type);
                PutWhole(tail, 72, columns.Type == DxfColumnType.DynamicColumns && columns.Auto ? 0 : count);
                PutReal(tail, 44, columns.Width);
                PutReal(tail, 45, columns.Gutter);
                PutWhole(tail, 73, columns.Auto ? 1 : 0);
                PutWhole(tail, 74, columns.Reversed ? 1 : 0);
                PutHeights(tail, manual ? columns.Heights : new List<double>());
            }
            else
            {
                tail.Clear();
            }

            var codes = new HashSet<int>();
            var run = new List<KeyValuePair<int, string>>();

            for (int i = 1; i < own.Count; i++)
            {
                codes.Add(own[i].TypeCode);
            }

            foreach (DxfTypedValue group in own.GetRange(1, end - 1).Concat(tail))
            {
                codes.Add(group.TypeCode);
                run.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            ReplaceGroups(0, codes, run.ToArray());
        }

        /// <summary>
        /// Writes the columns in the extended data of ACAD as ezdxf and AutoCAD write it before R2018: ACAD_MTEXT_COLUMN_INFO
        /// in its place, or first, and ACAD_MTEXT_DEFINED_HEIGHT after it for columns of one height; the linked columns
        /// (ACAD_MTEXT_COLUMNS) kept where they are. None takes the three out. The other data of ACAD is kept as it is.
        /// </summary>
        private void WriteColumnData(ColumnSet columns)
        {
            ColumnSet now = Columns();

            if (columns == null ? !now.InXData && !HasBlock("ACAD_MTEXT_DEFINED_HEIGHT") && !HasBlock("ACAD_MTEXT_COLUMNS") : now.InXData && !now.Embedded && SameData(now, columns))
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> acad = GetXDataForApplication("ACAD");
            List<DxfTypedValue> data = acad == null ? new List<DxfTypedValue>() : acad.Skip(1).ToList();
            List<DxfTypedValue> info = null;
            List<DxfTypedValue> height = null;

            if (columns != null)
            {
                bool manual = columns.Type == DxfColumnType.DynamicColumns && !columns.Auto;
                info = new List<DxfTypedValue>
                {
                    new DxfTypedValue(1000, "ACAD_MTEXT_COLUMN_INFO_BEGIN"),
                    new DxfTypedValue(1070, "75"), Short((int)columns.Type),
                    new DxfTypedValue(1070, "79"), Short(columns.Auto ? 1 : 0),
                    new DxfTypedValue(1070, "76"), Short(columns.Count),
                    new DxfTypedValue(1070, "78"), Short(columns.Reversed ? 1 : 0),
                    new DxfTypedValue(1070, "48"), new DxfTypedValue(1040, Numbers.Exact(columns.Width + 0.0)),
                    new DxfTypedValue(1070, "49"), new DxfTypedValue(1040, Numbers.Exact(columns.Gutter + 0.0)),
                };

                if (manual)
                {
                    info.Add(new DxfTypedValue(1070, "50"));
                    info.Add(Short(columns.Heights.Count));
                    info.AddRange(columns.Heights.Select(h => new DxfTypedValue(1040, Numbers.Exact(h + 0.0))));
                }

                info.Add(new DxfTypedValue(1000, "ACAD_MTEXT_COLUMN_INFO_END"));

                if (!manual)
                {
                    double defined = Value(46) != null ? Height : columns.Defined ?? 0.0;
                    height = new List<DxfTypedValue>
                    {
                        new DxfTypedValue(1000, "ACAD_MTEXT_DEFINED_HEIGHT_BEGIN"),
                        new DxfTypedValue(1070, "46"),
                        new DxfTypedValue(1040, Numbers.Exact(defined + 0.0)),
                        new DxfTypedValue(1000, "ACAD_MTEXT_DEFINED_HEIGHT_END"),
                    };
                }
            }

            // Each block of the columns replaced in its place, or taken out; one the data has none of put where AutoCAD writes
            // it: the column info first, the defined height after the linked columns, else after the column info.
            var rest = new List<DxfTypedValue>();
            bool infoPut = false;
            bool heightPut = false;

            for (int i = 0; i < data.Count; i++)
            {
                string block = data[i].TypeCode == 1000 ? data[i].Value.Trim() : null;
                bool isInfo = block == "ACAD_MTEXT_COLUMN_INFO_BEGIN";
                bool isHeight = block == "ACAD_MTEXT_DEFINED_HEIGHT_BEGIN";
                bool isLinks = block == "ACAD_MTEXT_COLUMNS_BEGIN";

                if (!isInfo && !isHeight && !isLinks)
                {
                    rest.Add(data[i]);
                    continue;
                }

                int end = End(data, i);

                if (isInfo && info != null && !infoPut)
                {
                    rest.AddRange(info);
                    infoPut = true;
                }
                else if (isHeight && height != null && !heightPut)
                {
                    rest.AddRange(height);
                    heightPut = true;
                }
                else if (isLinks && columns != null)
                {
                    rest.AddRange(data.GetRange(i, end - i + 1));
                }

                i = end;
            }

            if (info != null && !infoPut)
            {
                rest.InsertRange(0, info);
            }

            if (height != null && !heightPut)
            {
                int links = Find(rest, "ACAD_MTEXT_COLUMNS");
                rest.InsertRange(links >= 0 ? End(rest, links) + 1 : End(rest, Find(rest, "ACAD_MTEXT_COLUMN_INFO")) + 1, height);
            }

            var written = new List<DxfTypedValue> { new DxfTypedValue(1001, "ACAD") };
            written.AddRange(rest);
            XData = written;

            DxfTypedValue Short(int value) => new DxfTypedValue(1070, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Whether the columns of the extended data read as those to be written, the height of one height among them.</summary>
        private bool SameData(ColumnSet now, ColumnSet columns)
        {
            bool manual = columns.Type == DxfColumnType.DynamicColumns && !columns.Auto;
            double defined = Value(46) != null ? Height : columns.Defined ?? 0.0;

            return now.Type == columns.Type && now.Count == columns.Count && now.Auto == columns.Auto && now.Reversed == columns.Reversed
                && Same(now.Width, columns.Width) && Same(now.Gutter, columns.Gutter)
                && (manual ? now.Heights.Count == columns.Heights.Count && now.Heights.Zip(columns.Heights, Same).All(s => s) : now.Heights.Count == 0 && now.Defined.HasValue && Same(now.Defined.Value, defined));
        }

        /// <summary>Whether the extended data of ACAD holds a block of a name, between its _BEGIN and _END.</summary>
        private bool HasBlock(string name) => Find((GetXDataForApplication("ACAD") ?? new DxfTypedValue[0]).ToList(), name) >= 0;

        /// <summary>Where a block of the extended data of ACAD begins (its 1000 name_BEGIN); -1 where it has none.</summary>
        private static int Find(List<DxfTypedValue> data, string name)
            => data.FindIndex(g => g.TypeCode == 1000 && string.Equals(g.Value.Trim(), name + "_BEGIN", StringComparison.Ordinal));

        /// <summary>Where the block beginning at a place ends (its 1000 name_END), the last group where it has no end.</summary>
        private static int End(List<DxfTypedValue> data, int begin)
        {
            string name = data[begin].Value.Trim();
            string end = name.Substring(0, name.Length - "_BEGIN".Length) + "_END";
            int at = data.FindIndex(begin + 1, g => g.TypeCode == 1000 && string.Equals(g.Value.Trim(), end, StringComparison.Ordinal));
            return at < 0 ? data.Count - 1 : at;
        }

        /// <summary>Takes a block of a name out of the extended data, and gives where it began; -1 where it had none.</summary>
        private static int Cut(List<DxfTypedValue> data, string name)
        {
            int begin = Find(data, name);

            if (begin >= 0)
            {
                data.RemoveRange(begin, End(data, begin) - begin + 1);
            }

            return begin;
        }

        /// <summary>Sets a number of an embedded object: the text of its first group kept where it reads the same, to the bit; put where ezdxf writes it where it has none.</summary>
        private static void PutReal(List<DxfTypedValue> tail, int code, double value)
        {
            int at = tail.FindIndex(1, g => g.TypeCode == code);

            if (at >= 0 && Numbers.TryRead(tail[at].Value, out double now) && Same(now, value))
            {
                return;
            }

            PutText(tail, code, Numbers.Exact(value + 0.0), at);
        }

        /// <summary>Sets a whole number of an embedded object as <see cref="PutReal"/> sets a number.</summary>
        private static void PutWhole(List<DxfTypedValue> tail, int code, int value)
        {
            int at = tail.FindIndex(1, g => g.TypeCode == code);

            if (at >= 0 && int.TryParse(tail[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int now) && now == value)
            {
                return;
            }

            PutText(tail, code, value.ToString(CultureInfo.InvariantCulture), at);
        }

        private static void PutText(List<DxfTypedValue> tail, int code, string text, int at)
        {
            if (at >= 0)
            {
                tail[at] = DxfTypedValue.Read(code, text);
                return;
            }

            tail.Insert(Place(tail, code), DxfTypedValue.Read(code, text));
        }

        /// <summary>The heights of the columns (46) of an embedded object, each kept where it reads the same, put after the columns' other groups.</summary>
        private static void PutHeights(List<DxfTypedValue> tail, List<double> heights)
        {
            List<int> at = Enumerable.Range(0, tail.Count).Where(i => tail[i].TypeCode == 46).ToList();
            bool same = at.Count == heights.Count;

            for (int i = 0; same && i < at.Count; i++)
            {
                same = Numbers.TryRead(tail[at[i]].Value, out double now) && Same(now, heights[i]);
            }

            if (same)
            {
                return;
            }

            tail.RemoveAll(g => g.TypeCode == 46);
            tail.InsertRange(Place(tail, 46), heights.Select(h => DxfTypedValue.Read(46, Numbers.Exact(h + 0.0))));
        }

        /// <summary>Where a group of an embedded object goes that it has none of: after the last that ezdxf writes before it.</summary>
        private static int Place(List<DxfTypedValue> tail, int code)
        {
            int rank = Array.IndexOf(EmbeddedOrder, code);
            int put = 1;

            for (int i = 1; i < tail.Count; i++)
            {
                int their = Array.IndexOf(EmbeddedOrder, tail[i].TypeCode);

                if (their >= 0 && their <= rank)
                {
                    put = i + 1;
                }
            }

            return put;
        }

        /// <summary>
        /// The columns of an MTEXT as read: their type, count, whether of auto height and reversed, the width of each and of the
        /// gutter, the heights of columns of their own heights, and the height of columns of one height where the record gives
        /// it; and whether they were read from an embedded object or the extended data.
        /// </summary>
        private sealed class ColumnSet
        {
            internal DxfColumnType Type { get; set; }

            internal int Count { get; set; }

            internal bool Auto { get; set; }

            internal bool Reversed { get; set; }

            internal double Width { get; set; }

            internal double Gutter { get; set; }

            internal List<double> Heights { get; } = new List<double>();

            internal double? Defined { get; set; }

            internal bool Embedded { get; set; }

            internal bool InXData { get; set; }

            /// <summary>The heights of columns of their own heights made as many as the columns, those added of a height given; the rest left alone.</summary>
            internal ColumnSet Pad(double height)
            {
                if (Type == DxfColumnType.DynamicColumns && !Auto)
                {
                    while (Heights.Count < Count)
                    {
                        Heights.Add(height);
                    }

                    if (Heights.Count > Count)
                    {
                        Heights.RemoveRange(Count, Heights.Count - Count);
                    }
                }
                else
                {
                    Heights.Clear();
                }

                return this;
            }
        }

        private static int Whole(string text) => int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        private static double Real(string text) => Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : 0.0;

        private int? WholeOrNull(int code) => int.TryParse(Value(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : (int?)null;

        /// <summary>The groups that turn the MTEXT to an angle in a plane: its direction, its angle, or both.</summary>
        private KeyValuePair<int, string>[] Turned(GeoVector3 normal, double angle, bool direction, bool degrees)
        {
            var groups = new List<KeyValuePair<int, string>>();

            if (direction)
            {
                GeoVector3 along = World(normal, new GeoVector3(Math.Cos(angle), Math.Sin(angle), 0.0));
                groups.AddRange(PointChanges(11, new GeoPoint3(along.X, along.Y, along.Z), Origin));
            }

            if (degrees)
            {
                groups.Add(new KeyValuePair<int, string>(50, Numbers.Exact(DxfOcsTransform.Degrees(angle))));
            }

            return groups.ToArray();
        }

        private void SetFill(int mask, bool on)
        {
            int now = WholeNumber(90, 0);
            int next = on ? now | mask : now & ~mask;

            if (next != now)
            {
                Since(1018, "background of an MTEXT");
                SetWhole(90, next, 0);
            }
        }

        private void Since(int version, string what)
        {
            if (!Item.File.Supports(version))
            {
                string release = version == 1018 ? "R2004" : version == 1021 ? "R2007" : "R2018";
                throw new InvalidOperationException("A DXF older than " + release + " (AC" + version.ToString(CultureInfo.InvariantCulture) + ") has no " + what + ".");
            }
        }
    }
}
