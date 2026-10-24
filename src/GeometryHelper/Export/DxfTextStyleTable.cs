using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The text style table of a DXF file, as AutoCAD's <c>TextStyleTable</c>, read: its styles in the order of the file, each
    /// a <see cref="DxfTextStyleTableRecord"/>, found by name. Styles are not added or changed yet.
    /// </summary>
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

        /// <summary>Gets an enumerator over the styles, in the order of the file.</summary>
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
            => throw new NotSupportedException("Text styles are read and not added yet: a later release adds them.");
    }

    /// <summary>
    /// A text style of a DXF file, as AutoCAD's <c>TextStyleTableRecord</c>, read from its STYLE record: its name, its font,
    /// its height, width and slant.
    /// </summary>
    /// <remarks>
    /// The style ROMANS of the font romans.shx, 2.5 high, three quarters wide and slanted 15 degrees is <c>FileName</c>
    /// romans.shx, <see cref="TextSize"/> 2.5, <see cref="XScale"/> 0.75 and <see cref="ObliquingAngle"/> 0.2618 radians.
    /// </remarks>
    public sealed class DxfTextStyleTableRecord : DxfSymbolTableRecord
    {
        private DxfTextStyleTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets the file of the style's font (group 3), as AutoCAD's <c>FileName</c>: <c>romans.shx</c>, <c>arial.ttf</c>; empty where the record gives none.</summary>
        public string FileName => Item.File.ValueOf(Item, 3) ?? string.Empty;

        /// <summary>Gets the file of the style's big font (group 4), as AutoCAD's <c>BigFontFileName</c>; empty where the record gives none.</summary>
        public string BigFontFileName => Item.File.ValueOf(Item, 4) ?? string.Empty;

        /// <summary>Gets the fixed height of the style's texts (group 40), as AutoCAD's <c>TextSize</c>: nought where each text gives its own.</summary>
        public double TextSize => Real(40, 0.0);

        /// <summary>Gets how wide the style's letters are for their height (group 41), as AutoCAD's <c>XScale</c>: 1 where the record gives none.</summary>
        public double XScale => Real(41, 1.0);

        /// <summary>Gets the slant of the style's letters (group 50) in radians, as AutoCAD's <c>ObliquingAngle</c>: the record writes degrees.</summary>
        public double ObliquingAngle => Real(50, 0.0) * Math.PI / 180.0;

        /// <summary>Gets the height last used with the style (group 42), as AutoCAD's <c>PriorSize</c>.</summary>
        public double PriorSize => Real(42, 0.0);

        /// <summary>Gets the flags of the style's text generation (group 71), as AutoCAD's <c>FlagBits</c>: 2 backwards, 4 upside down.</summary>
        public byte FlagBits => (byte)Whole(71);

        /// <summary>Gets whether the style is of shapes, not letters (flag 1 of group 70), as AutoCAD's <c>IsShapeFile</c>.</summary>
        public bool IsShapeFile => (Flags & 1) != 0;

        /// <summary>Gets whether the style's texts run down (flag 4 of group 70), as AutoCAD's <c>IsVertical</c>.</summary>
        public bool IsVertical => (Flags & 4) != 0;

        /// <summary>The object of a STYLE record, made once.</summary>
        internal static DxfTextStyleTableRecord Of(DxfItem item) => item.Wrapper as DxfTextStyleTableRecord ?? (DxfTextStyleTableRecord)item.KeepWrapper(new DxfTextStyleTableRecord(item));

        private double Real(int code, double fallback) => Numbers.TryRead(Item.File.ValueOf(Item, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int Whole(int code) => int.TryParse(Item.File.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
    }
}
