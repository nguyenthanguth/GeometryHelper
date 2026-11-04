using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An IMAGEDEF of a DXF file, as AutoCAD's <c>RasterImageDef</c>: the picture file the IMAGEs of a drawing show, under its
    /// name in the dictionary of images (<see cref="GetImageDictionary"/>), with the size in pixels and the size of a pixel
    /// AutoCAD read from the file when it loaded it.
    /// </summary>
    /// <remarks>
    /// The library reads no picture: what AutoCAD learns by loading the file (its colours, its kind, whether it is found) it
    /// has not, and the path is kept as the file writes it. A PNG of 4 by 3 pixels at 96 dots an inch, loaded by AutoCAD 2021,
    /// is an IMAGEDEF of the size (4, 3) and pixels of 0.2646 mm, its resolution in centimetres.
    /// </remarks>
    public sealed class DxfRasterImageDef : DxfDBObject
    {
        // The groups of AcDbRasterImageDef in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 90, 1, 10, 20, 11, 21, 280, 281 });

        internal DxfRasterImageDef(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the path of the picture file (group 1), as AutoCAD's <c>SourceFileName</c>: as the file writes it, a path
        /// of the machine it was drawn on or one relative to the drawing, such as <c>C:\pictures\px43.png</c>.
        /// </summary>
        /// <value>Set, the path as it is to be written; the IMAGEs that show the definition show that file once AutoCAD finds it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public string SourceFileName
        {
            get => Text(1) ?? string.Empty;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, SourceFileName, StringComparison.Ordinal))
                {
                    InFile("be changed").SetValues(Item, new[] { new KeyValuePair<int, string>(1, value) }, Order, false);
                }
            }
        }

        /// <summary>Gets the size of the picture in pixels (groups 10, 20), as AutoCAD's <c>Size</c>: (4, 3) for a picture of 4 by 3; (0, 0) where the record gives none.</summary>
        public GeoVector2 Size => new GeoVector2(Real(10, 0.0), Real(20, 0.0));

        /// <summary>
        /// Gets the size of one pixel in millimetres (groups 11, 21), as AutoCAD's <c>ResolutionMMPerPixel</c>: (0.2646, 0.2646)
        /// for a picture of 96 dots an inch; (1, 1) where the record gives none, as ezdxf and ACadSharp read none.
        /// </summary>
        public GeoVector2 ResolutionMMPerPixel => new GeoVector2(Real(11, 1.0), Real(21, 1.0));

        /// <summary>
        /// Gets the unit the picture's resolution is given in (group 281), as AutoCAD's <c>ResolutionUnits</c>: centimetres (2)
        /// or inches (5) for a picture that says its resolution, none (0) for one that does not.
        /// </summary>
        public DxfUnit ResolutionUnits
        {
            get
            {
                int unit = Whole(281, 0);
                return unit >= 0 && unit <= 20 ? (DxfUnit)unit : DxfUnit.None;
            }
        }

        /// <summary>Gets whether AutoCAD had the picture loaded when it saved the drawing (group 280), as AutoCAD's <c>IsLoaded</c> was then; true where the record gives none.</summary>
        public bool IsLoaded => Whole(280, 1) != 0;

        /// <summary>
        /// Gets the dictionary of the images of a drawing, as AutoCAD's <c>GetImageDictionary</c> gives its id: ACAD_IMAGE_DICT of
        /// the named object dictionary, each <see cref="DxfRasterImageDef"/> under its name; null where the drawing has none.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The dictionary, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static DxfDBDictionary GetImageDictionary(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfDBDictionary root = database.NamedObjectsDictionary;
            return root != null && root.Contains("ACAD_IMAGE_DICT") ? root.GetAt("ACAD_IMAGE_DICT") as DxfDBDictionary : null;
        }

        /// <summary>Gives the definition as a line for debugging: its type, its handle and its path.</summary>
        /// <returns>The line, such as <c>IMAGEDEF 79 C:\pictures\px43.png</c>.</returns>
        public override string ToString() => "IMAGEDEF " + (Handle ?? string.Empty) + " " + SourceFileName;

        /// <summary>The first group of a code of the definition's own part; null where it has none.</summary>
        private string Text(int code)
        {
            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        private double Real(int code, double fallback) => Numbers.TryRead(Text(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int Whole(int code, int fallback) => int.TryParse(Text(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }
}
