using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The text style table of a DXF file, as AutoCAD's <c>TextStyleTable</c>: its styles in the order of the file, each a
    /// <see cref="DxfTextStyleTableRecord"/>, found by name and added to.
    /// </summary>
    /// <remarks>
    /// <c>db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx", TextSize = 2.5 })</c>
    /// adds the style ROMANS of the font romans.shx, its texts 2.5 high.
    /// </remarks>
    public sealed class DxfTextStyleTable : DxfSymbolTable, IEnumerable<DxfTextStyleTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfTextStyleTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>
        /// Gets the style of a name, letters of either case the same.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no style of the name.</exception>
        public new DxfTextStyleTableRecord this[string key] => (DxfTextStyleTableRecord)base[key];

        /// <summary>Gets an enumerator over the styles, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfTextStyleTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("STYLE").ToArray())
            {
                yield return DxfTextStyleTableRecord.Of(item);
            }
        }

        IEnumerator<DxfTextStyleTableRecord> IEnumerable<DxfTextStyleTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfTextStyleTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfTextStyleTableRecord style))
            {
                throw new ArgumentException("The text style table takes a DxfTextStyleTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            style.AddTo(_file);
        }
    }

    /// <summary>
    /// A text style of a DXF file, as AutoCAD's <c>TextStyleTableRecord</c>, read from its STYLE record: its name, its font,
    /// its height, width and slant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The style ROMANS of the font romans.shx, 2.5 high, three quarters wide and slanted 15 degrees is <c>FileName</c>
    /// romans.shx, <see cref="TextSize"/> 2.5, <see cref="XScale"/> 0.75 and <see cref="ObliquingAngle"/> 0.2618 radians.
    /// </para>
    /// <para>
    /// A style of a file is changed by its setters, its record written anew, every other group kept; a setter given the value
    /// its getter gives changes nothing. A style made in code is of the font txt, its texts of no fixed height, as wide as high
    /// and upright until it is given others, and is written when it is added to a <see cref="DxfTextStyleTable"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfTextStyleTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbTextStyleTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 40, 41, 50, 71, 42, 3, 4 });

        private const string FontApplication = "ACAD";

        private string _fileName = "txt";
        private string _bigFontFileName = string.Empty;
        private double _textSize;
        private double _xScale = 1.0;
        private double _obliquing;
        private bool _vertical;

        /// <summary>
        /// Initializes a style made in code, to be added to a <see cref="DxfTextStyleTable"/> with its
        /// <see cref="DxfSymbolTableRecord.Name"/> given.
        /// </summary>
        public DxfTextStyleTableRecord()
        {
        }

        private DxfTextStyleTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the file of the style's font (group 3), as AutoCAD's <c>FileName</c>: <c>romans.shx</c>, <c>arial.ttf</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public string FileName
        {
            get => Item == null ? _fileName : Item.File.ValueOf(Item, 3) ?? string.Empty;
            set => SetText(3, value, ref _fileName, FileName);
        }

        /// <summary>Gets or sets the file of the style's big font (group 4), as AutoCAD's <c>BigFontFileName</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public string BigFontFileName
        {
            get => Item == null ? _bigFontFileName : Item.File.ValueOf(Item, 4) ?? string.Empty;
            set => SetText(4, value, ref _bigFontFileName, BigFontFileName);
        }

        /// <summary>Gets or sets the fixed height of the style's texts (group 40), as AutoCAD's <c>TextSize</c>: nought where each text gives its own.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double TextSize
        {
            get => Item == null ? _textSize : Real(40, 0.0);

            set
            {
                if (!Same(value, TextSize))
                {
                    Guard.NonNegative(value, nameof(value), "The height of a style's texts has to be a finite number of nought or more.");
                    SetReal(40, value, ref _textSize);
                }
            }
        }

        /// <summary>Gets or sets how wide the style's letters are for their height (group 41), as AutoCAD's <c>XScale</c>: 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double XScale
        {
            get => Item == null ? _xScale : Real(41, 1.0);

            set
            {
                if (!Same(value, XScale))
                {
                    Guard.Positive(value, nameof(value), "The width of a style's letters has to be a finite number above nought.");
                    SetReal(41, value, ref _xScale);
                }
            }
        }

        /// <summary>
        /// Gets or sets the slant of the style's letters (group 50) in radians, as AutoCAD's <c>ObliquingAngle</c>: the record
        /// writes degrees, so 0.2618 radians is written as 15.
        /// </summary>
        /// <value>Set, from -85 to 85 degrees, as AutoCAD takes it, in radians; an angle that reads back as the one the style has changes nothing.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the slant set is not a finite number within 85 degrees of upright.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double ObliquingAngle
        {
            get => Item == null ? _obliquing * Math.PI / 180.0 : Real(50, 0.0) * Math.PI / 180.0;

            set
            {
                if (Same(value, ObliquingAngle))
                {
                    return;
                }

                Guard.Finite(value, nameof(value), "A slant has to be a finite number.");

                if (Math.Abs(value) > 85.0 * Math.PI / 180.0 + 1E-12)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A slant is from -85 to 85 degrees, as AutoCAD takes it.");
                }

                SetReal(50, value * 180.0 / Math.PI, ref _obliquing);
            }
        }

        /// <summary>Gets the height last used with the style (group 42), as AutoCAD's <c>PriorSize</c>: 2.5 for a style made in code.</summary>
        public double PriorSize => Item == null ? 2.5 : Real(42, 0.0);

        /// <summary>Gets the flags of the style's text generation (group 71), as AutoCAD's <c>FlagBits</c>: 2 backwards, 4 upside down.</summary>
        public byte FlagBits => Item == null ? (byte)0 : (byte)Whole(71);

        /// <summary>Gets whether the style is of shapes, not letters (flag 1 of group 70), as AutoCAD's <c>IsShapeFile</c>.</summary>
        public bool IsShapeFile => (Flags & 1) != 0;

        /// <summary>Gets or sets whether the style's texts run down (flag 4 of group 70), as AutoCAD's <c>IsVertical</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool IsVertical
        {
            get => Item == null ? _vertical : (Flags & 4) != 0;

            set
            {
                if (value == IsVertical)
                {
                    return;
                }

                if (Item == null)
                {
                    _vertical = value;
                    return;
                }

                Set(70, ((value ? Flags | 4 : Flags & ~4)).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Gets or sets the TrueType font of the style, as AutoCAD's <c>Font</c>: from the style's extended data of the
        /// application <c>ACAD</c>, its typeface (1000) and its flags (1071), as <see cref="DxfFontDescriptor"/> says; a font of
        /// no typeface where it has none, as a style of an SHX font.
        /// </summary>
        /// <value>
        /// Set, written as AutoCAD writes it, <c>1001 ACAD, 1000 Arial, 1071 34</c>, the data of ACAD replaced; a font of no
        /// typeface takes it out. The font the style has changes nothing. The font's file stays as <see cref="FileName"/> says.
        /// </value>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfFontDescriptor Font
        {
            get
            {
                IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(FontApplication);
                string typeFace = null;
                int flags = 0;

                for (int i = 1; data != null && i < data.Count; i++)
                {
                    if (data[i].TypeCode == 1000 && typeFace == null)
                    {
                        typeFace = data[i].Value;
                    }
                    else if (data[i].TypeCode == 1071 && typeFace != null)
                    {
                        int.TryParse(data[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out flags);
                        break;
                    }
                }

                return DxfFontDescriptor.Of(typeFace ?? string.Empty, flags);
            }

            set
            {
                if (value == Font)
                {
                    return;
                }

                XData = value.TypeFace.Length == 0
                    ? new[] { new DxfTypedValue(1001, FontApplication) }
                    : new[] { new DxfTypedValue(1001, FontApplication), new DxfTypedValue(1000, value.TypeFace), new DxfTypedValue(1071, value.Flags.ToString(CultureInfo.InvariantCulture)) };
            }
        }

        /// <summary>The object of a STYLE record, made once.</summary>
        internal static DxfTextStyleTableRecord Of(DxfItem item) => item.Wrapper as DxfTextStyleTableRecord ?? (DxfTextStyleTableRecord)item.KeepWrapper(new DxfTextStyleTableRecord(item));

        /// <summary>Writes the style made in code into a file's text style table, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            DxfItem item = file.AddTextStyle(Name, _vertical ? 4 : 0, _textSize, _xScale, _obliquing, _fileName, _bigFontFileName);

            if (item == null)
            {
                throw new InvalidOperationException("The file has no text style table to add the style " + Name + " to.");
            }

            Bind(item);
        }

        private void SetText(int code, string value, ref string field, string now)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A file's name is one line, with no line break in it.", nameof(value));
            }

            if (Item == null)
            {
                field = value;
            }
            else if (!string.Equals(value, now, StringComparison.Ordinal))
            {
                Set(code, value);
            }
        }

        private void SetReal(int code, double value, ref double field)
        {
            if (Item == null)
            {
                field = value;
            }
            else
            {
                Set(code, Numbers.Exact(value + 0.0));
            }
        }

        private void Set(int code, string value) => Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(code, value) }, Order, false);

        private double Real(int code, double fallback) => Numbers.TryRead(Item.File.ValueOf(Item, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int Whole(int code) => int.TryParse(Item.File.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
