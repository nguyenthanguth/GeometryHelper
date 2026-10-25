using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
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
    /// DxfMText { Contents = "B-12", Location = new GeoPoint3(10, 20, 0) }</c>, is refused there by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>. <see cref="DxfEntity.GetGeometry"/> gives the
    /// <see cref="DxfText"/> <see cref="DxfReader"/> reads of it.
    /// </para>
    /// </remarks>
    public sealed class DxfMText : DxfEntity
    {
        // The groups of AcDbMText in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 40, 41, 46, 71, 72, 3, 1, 7, 210, 220, 230, 11, 21, 31, 42, 43, 50, 73, 44, 90, 63, 421, 431, 45, 441 });

        private static readonly int[] Words = { 1, 3 };

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

        /// <summary>
        /// Gets or sets the words of the MTEXT with their formatting (groups 3 and 1), as AutoCAD's <c>Contents</c>: as the file
        /// writes them, <c>\P</c> a new paragraph, <c>{\H1.5x;Big}</c> letters half as high again, a letter beyond the code page
        /// <c>\U+00E9</c>; empty where the record gives none.
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
        /// a new paragraph a line feed, a stack <c>\S1^2;</c> as 1/2, <c>%%d</c> a degree sign and an escape its letter.
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
        /// Gets or sets where the MTEXT is held (groups 10, 20, 30), as AutoCAD's <c>Location</c>: in world coordinates, as the
        /// file writes it; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public GeoPoint3 Location
        {
            get => Point(10, Origin);
            set => SetAt(10, value, Origin);
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
                    SetNumber(40, value, 2.5);
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
        /// Gets or sets the height of the box the MTEXT is given (group 46), as AutoCAD's <c>Height</c>; nought where the record
        /// gives none. A file older than R2007 (AC1021) has no such group.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased, or a height is set in a file older than R2007.</exception>
        public double Height
        {
            get => Number(46, 0.0);

            set
            {
                if (!Same(value, Height))
                {
                    Guard.NonNegative(value, nameof(value), "The height of an MTEXT's box has to be a finite number of nought or more.");
                    Since(1021, "the height of an MTEXT's box");
                    SetNumber(46, value, 0.0);
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
                    Set(Turned(Normal, value, HasDirection(), Value(50) != null || !HasDirection()));
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

                Set(groups.ToArray());
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
        /// Gets the name of the text style of the MTEXT (group 7), as AutoCAD's <c>TextStyleName</c>: as the style table names it,
        /// its escapes read as letters; <c>Standard</c> where the record gives none.
        /// </summary>
        public string TextStyleName
        {
            get
            {
                string name = DxfReader.NameOf(Value(7) ?? string.Empty);
                return name.Length == 0 ? "Standard" : name;
            }
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
                Set(Join(normal, HasDirection() ? Turned(unit, Rotation, true, false) : new KeyValuePair<int, string>[0]));
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

                    Since(1018, "the background of an MTEXT");
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

                Since(1018, "the background of an MTEXT");
                Set(
                    new KeyValuePair<int, string>(63, value.ColorIndex.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<int, string>(421, value.IsByColor ? value.ColorValue.ToString(CultureInfo.InvariantCulture) : null),
                    new KeyValuePair<int, string>(431, null));
            }
        }

        /// <summary>
        /// Gets how the MTEXT is set in columns, as AutoCAD's <c>ColumnType</c>: from its embedded object from R2018 (group 71),
        /// else from its extended data of ACAD (75), as ezdxf reads them; <see cref="DxfColumnType.NoColumns"/> where it gives none.
        /// </summary>
        public DxfColumnType ColumnType => Columns().TryGetValue("type", out object type) ? (DxfColumnType)(int)type : DxfColumnType.NoColumns;

        /// <summary>Gets how many columns the MTEXT has (group 72 of its embedded object, 76 of its extended data), as AutoCAD's <c>ColumnCount</c>; nought where it gives none.</summary>
        public int ColumnCount => Columns().TryGetValue("count", out object count) ? (int)count : 0;

        /// <summary>Gets how wide each column is (group 44 of its embedded object, 48 of its extended data), as AutoCAD's <c>ColumnWidth</c>; nought where it gives none.</summary>
        public double ColumnWidth => Columns().TryGetValue("width", out object width) ? (double)width : 0.0;

        /// <summary>Gets the space between the columns (group 45 of its embedded object, 49 of its extended data), as AutoCAD's <c>ColumnGutterWidth</c>; nought where it gives none.</summary>
        public double ColumnGutterWidth => Columns().TryGetValue("gutter", out object gutter) ? (double)gutter : 0.0;

        /// <summary>Gets whether the columns are all of one height (group 73 of its embedded object, 79 of its extended data), as AutoCAD's <c>ColumnAutoHeight</c>; false where it gives none.</summary>
        public bool ColumnAutoHeight => Columns().TryGetValue("auto", out object auto) && (bool)auto;

        /// <summary>Gets whether the columns run from right to left (group 74 of its embedded object, 78 of its extended data), as AutoCAD's <c>ColumnFlowReversed</c>; false where it gives none.</summary>
        public bool ColumnFlowReversed => Columns().TryGetValue("reversed", out object reversed) && (bool)reversed;

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

            var heights = (List<double>)Columns()["heights"];
            return index < heights.Count ? heights[index] : 0.0;
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the MTEXT, as AutoCAD's <c>TransformBy</c> does, and as ezdxf's <c>transform()</c>
        /// carries one: its location and direction carried, its angle (50) given up for its direction, its height the stretch of
        /// its upright square to its direction, its width the stretch of its direction, the heights <c>\H</c> of its words scaled
        /// with it, its normal carried.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the MTEXT.</exception>
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
            string contents = Contents;
            string scaled = Math.Abs(oldHeight) > 1E-12 && !Close(oldHeight, newHeight) ? ScaledHeights(contents, newHeight / oldHeight) : contents;
            Set(groups.ToArray());
            Contents = scaled;
        }

        [DxfType("MTEXT")]
        internal static DxfEntity Create(DxfItem item) => new DxfMText(item);

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
        /// Sets groups of the MTEXT itself in one writing: as <see cref="DxfEntity"/> sets them, or, where a code the MTEXT
        /// itself lacks is in its embedded object, by writing its own part anew with the embedded object kept as it is.
        /// </summary>
        private void Set(params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length == 0)
            {
                return;
            }

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
        private Dictionary<string, object> Columns()
        {
            var read = new Dictionary<string, object>(StringComparer.Ordinal);
            var heights = new List<double>();
            List<DxfTypedValue> embedded = EmbeddedGroups();

            if (embedded.Count > 0)
            {
                foreach (DxfTypedValue group in embedded)
                {
                    switch (group.TypeCode)
                    {
                        case 71: read["type"] = Whole(group.Value); break;
                        case 72: read["count"] = Whole(group.Value); break;
                        case 73: read["auto"] = Whole(group.Value) != 0; break;
                        case 74: read["reversed"] = Whole(group.Value) != 0; break;
                        case 44: read["width"] = Real(group.Value); break;
                        case 45: read["gutter"] = Real(group.Value); break;
                        case 46: heights.Add(Real(group.Value)); break;
                    }
                }

                read["heights"] = heights;
                return read;
            }

            IReadOnlyList<DxfTypedValue> acad = GetXDataForApplication("ACAD");
            bool inside = false;
            int? code = null;
            int counted = 0;

            foreach (DxfTypedValue group in acad ?? new DxfTypedValue[0])
            {
                if (group.TypeCode == 1000)
                {
                    inside = group.Value.Trim() == "ACAD_MTEXT_COLUMN_INFO_BEGIN" || (inside && group.Value.Trim() != "ACAD_MTEXT_COLUMN_INFO_END");
                    continue;
                }

                if (!inside)
                {
                    continue;
                }

                if (counted > 0 && group.TypeCode == 1040)
                {
                    heights.Add(Real(group.Value));
                    counted--;
                    continue;
                }

                if (code == null)
                {
                    code = Whole(group.Value);
                    continue;
                }

                switch (code.Value)
                {
                    case 75: read["type"] = Whole(group.Value); break;
                    case 76: read["count"] = Whole(group.Value); break;
                    case 79: read["auto"] = Whole(group.Value) != 0; break;
                    case 78: read["reversed"] = Whole(group.Value) != 0; break;
                    case 48: read["width"] = Real(group.Value); break;
                    case 49: read["gutter"] = Real(group.Value); break;
                    case 50: counted = Whole(group.Value); break;
                }

                code = null;
            }

            read["heights"] = heights;
            return read;
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
                Since(1018, "the background of an MTEXT");
                SetWhole(90, next, 0);
            }
        }

        private void Since(int version, string what)
        {
            if (!Item.File.Supports(version))
            {
                throw new InvalidOperationException("A DXF older than AC" + version.ToString(CultureInfo.InvariantCulture) + " has no " + what + ".");
            }
        }
    }
}
