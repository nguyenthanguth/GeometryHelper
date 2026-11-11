using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// <para>
    /// The library reads no picture: what AutoCAD learns by loading the file (its colours, its kind, whether it is found) it
    /// has not, and the path is kept as the file writes it. A PNG of 4 by 3 pixels at 96 dots an inch, loaded by AutoCAD 2021,
    /// is an IMAGEDEF of the size (4, 3) and pixels of 0.2646 mm, its resolution in centimetres.
    /// </para>
    /// <para>
    /// Made in code, a definition is given its path and its size in pixels, which AutoCAD reads from the file and the library
    /// cannot, and added to a drawing's dictionary of images under a name:
    /// <c>DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db); images.SetAt(DxfRasterImageDef.SuggestName(images, path), new DxfRasterImageDef(path, new GeoVector2(4, 3)));</c>
    /// An image is then made of it by <see cref="DxfRasterImage(DxfRasterImageDef)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfRasterImageDef : DxfDBObject
    {
        // The groups of AcDbRasterImageDef in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[] { 100, 90, 1, 10, 20, 11, 21, 280, 281 }
        );

        // The groups a definition made in code is written with after its marker, as AutoCAD 2021 writes a new one of a picture
        // loaded, but for the size of a pixel, (1, 1) as AutoCAD writes it of a picture it has not loaded.
        private readonly List<KeyValuePair<int, string>> _made;

        /// <summary>
        /// Initializes a definition made in code of a picture file, as AutoCAD's <c>new RasterImageDef()</c> given its
        /// <c>SourceFileName</c> and loaded: the path as it is to be written, and the size in pixels the caller gives, which
        /// AutoCAD reads from the file when it loads it and the library does not; a pixel (1, 1) in no unit, and loaded (280 of
        /// 1), as ezdxf writes a new one, so that AutoCAD loads the picture when it opens the drawing. Added to a drawing by
        /// <see cref="DxfDBDictionary.SetAt"/> of its dictionary of images (<see cref="CreateImageDictionary"/>).
        /// </summary>
        /// <param name="sourceFileName">The path of the picture, as it is to be written: <c>C:\pictures\px43.png</c>. AutoCAD
        /// 2021 refuses a path it does not find (eFileAccessErr); the library writes it as given.</param>
        /// <param name="size">The size of the picture in pixels across and up, each above nought.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a part of the size is not a finite number above nought.</exception>
        public DxfRasterImageDef(string sourceFileName, GeoVector2 size)
        {
            if (sourceFileName == null)
            {
                throw new ArgumentNullException(nameof(sourceFileName));
            }

            Guard.Positive(size.X, nameof(size), "A picture is some pixels across.");
            Guard.Positive(size.Y, nameof(size), "A picture is some pixels up.");
            _made = new List<KeyValuePair<int, string>>
            {
                Pair(90, "0"),
                Pair(1, sourceFileName),
                Pair(10, Numbers.Exact(size.X + 0.0)),
                Pair(20, Numbers.Exact(size.Y + 0.0)),
                Pair(11, "1"),
                Pair(21, "1"),
                Pair(280, "1"),
                Pair(281, "0"),
            };
        }

        internal DxfRasterImageDef(DxfItem item)
            : base(item) { }

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
                    Set(Pair(1, value));
                }
            }
        }

        /// <summary>Gets the size of the picture in pixels (groups 10, 20), as AutoCAD's <c>Size</c>: (4, 3) for a picture of 4 by 3; (0, 0) where the record gives none.</summary>
        public GeoVector2 Size => new GeoVector2(Real(10, 0.0), Real(20, 0.0));

        /// <summary>
        /// Gets or sets the size of one pixel in millimetres (groups 11, 21), as AutoCAD's <c>ResolutionMMPerPixel</c>: (0.2646,
        /// 0.2646) for a picture of 96 dots an inch; (1, 1) where the record gives none, as ezdxf and ACadSharp read none, and for
        /// a definition made in code.
        /// </summary>
        /// <value>Set, each part written as the number given, as AutoCAD 2021 writes 0.5 set.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a part set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public GeoVector2 ResolutionMMPerPixel
        {
            get => new GeoVector2(Real(11, 1.0), Real(21, 1.0));
            set
            {
                Guard.Positive(value.X, nameof(value), "A pixel is some millimetres across.");
                Guard.Positive(value.Y, nameof(value), "A pixel is some millimetres up.");
                GeoVector2 now = ResolutionMMPerPixel;

                if (
                    BitConverter.DoubleToInt64Bits(value.X) != BitConverter.DoubleToInt64Bits(now.X)
                    || BitConverter.DoubleToInt64Bits(value.Y) != BitConverter.DoubleToInt64Bits(now.Y)
                )
                {
                    Set(Pair(11, Numbers.Exact(value.X + 0.0)), Pair(21, Numbers.Exact(value.Y + 0.0)));
                }
            }
        }

        /// <summary>
        /// Gets or sets the unit the picture's resolution is given in (group 281), as AutoCAD's <c>ResolutionUnits</c>: centimetres (2)
        /// or inches (5) for a picture that says its resolution, none (0) for one that does not.
        /// </summary>
        /// <value>Set, one of the three the DXF reference and ezdxf take, written as its number: AutoCAD 2021 wrote 5 for inches.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not none, centimetres or inches.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public DxfUnit ResolutionUnits
        {
            get
            {
                int unit = Whole(281, 0);
                return unit >= 0 && unit <= 20 ? (DxfUnit)unit : DxfUnit.None;
            }
            set
            {
                if (value != DxfUnit.None && value != DxfUnit.Centimeter && value != DxfUnit.Inch)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The resolution of a picture is in no unit, centimetres or inches, as the DXF reference has it."
                    );
                }

                if (value != ResolutionUnits)
                {
                    Set(Pair(281, ((int)value).ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        /// <summary>Gets whether AutoCAD had the picture loaded when it saved the drawing (group 280), as AutoCAD's <c>IsLoaded</c> was then; true where the record gives none, and for a definition made in code.</summary>
        public bool IsLoaded => Whole(280, 1) != 0;

        /// <summary>
        /// Gets how many images of the drawing are associated with the definition, as AutoCAD 2021's <c>GetEntityCount</c>
        /// counts them: those whose IMAGEDEF_REACTOR the definition lists, an image made in code and appended, or associated by
        /// <see cref="DxfRasterImage.AssociateRasterDef"/>; a copy, which AutoCAD's <c>Clone</c> appended leaves with no reactor,
        /// is not counted, as AutoCAD counts it not. Two images in a block placed by two INSERTs count once each.
        /// </summary>
        /// <param name="locked">Whether one of them is on a locked layer.</param>
        /// <returns>The count; nought for a definition in no file.</returns>
        public int GetEntityCount(out bool locked)
        {
            locked = false;

            if (Item == null || !IsResident || IsErased)
            {
                return 0;
            }

            DxfFile file = Item.File;
            int count = 0;

            foreach (string reactor in file.ReactorsOf(Item))
            {
                DxfItem held = file.ObjectItem(reactor);
                DxfEntity shown =
                    held == null || held.Type != "IMAGEDEF_REACTOR"
                        ? null
                        : file.Database?.EntityByHandle(file.OwnerOf(held));

                if (shown is DxfRasterImage && !shown.IsErased)
                {
                    count++;
                    locked |=
                        file.Database.LayerTable.Has(shown.Layer) && file.Database.LayerTable[shown.Layer].IsLocked;
                }
            }

            return count;
        }

        /// <summary>
        /// Erases the definition, as AutoCAD 2021's <c>Erase</c> of a <c>RasterImageDef</c>: its entry of the dictionary of
        /// images taken out, and with it every image of the drawing that shows it, as AutoCAD takes those associated with it.
        /// AutoCAD leaves an image of no reactor pointing at the definition erased, which the library does not: a copy is
        /// erased with the rest.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is in no file, or is erased.</exception>
        public override void Erase()
        {
            DxfFile file = InFile("be erased");
            DxfDatabase database = file.Database;

            if (database != null)
            {
                var showing = new List<DxfEntity>();

                foreach (DxfBlockTableRecord block in database.BlockTable)
                {
                    foreach (DxfEntity entity in block)
                    {
                        if (
                            entity is DxfRasterImage image
                            && !(entity is DxfWipeout)
                            && string.Equals(image.ImageDefHandle, Handle, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            showing.Add(entity);
                        }
                    }
                }

                foreach (DxfEntity entity in showing)
                {
                    entity.Erase();
                }
            }

            DxfDBDictionary.EraseFromOwner(this, file);
        }

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
            return root != null && root.Contains("ACAD_IMAGE_DICT")
                ? root.GetAt("ACAD_IMAGE_DICT") as DxfDBDictionary
                : null;
        }

        /// <summary>
        /// Gets the dictionary of the images of a drawing, made where it has none, as AutoCAD's <c>CreateImageDictionary</c>
        /// makes it: ACAD_IMAGE_DICT of the named object dictionary, owned by it, of entries kept (281 of 1). The one the drawing
        /// has where it has one, where AutoCAD 2021 makes a new one in its place and erases the old with its definitions.
        /// </summary>
        /// <param name="database">The drawing, of R14 (AC1014) or later.</param>
        /// <returns>The dictionary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing is older than R14, which has no images, or has no
        /// named object dictionary.</exception>
        public static DxfDBDictionary CreateImageDictionary(DxfDatabase database)
        {
            DxfDBDictionary found = GetImageDictionary(database);

            if (found != null)
            {
                return found;
            }

            database.File.Requires(1014, "images", "R14 (AC1014)");
            DxfDBDictionary root = database.NamedObjectsDictionary;

            if (root == null)
            {
                throw new InvalidOperationException(
                    "The drawing has no named object dictionary to hold its dictionary of images."
                );
            }

            var images = new DxfDBDictionary();
            root.SetAt("ACAD_IMAGE_DICT", images);
            return images;
        }

        /// <summary>
        /// Gets a name for a definition of a picture in a dictionary of images, as AutoCAD 2021's <c>SuggestName</c> gives it:
        /// the file's name without its folder or extension, <c>px43</c> for <c>C:\pictures\px43.png</c> and <c>Photo 1</c> for
        /// <c>pictures\Photo 1.JPG</c>; and where the dictionary has that name, letters of either case the same, the name and
        /// <c>_1</c>, or <c>_2</c> and on, the first it has not.
        /// </summary>
        /// <param name="imageDictionary">The dictionary of images.</param>
        /// <param name="newImagePathName">The path of the picture.</param>
        /// <returns>The name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the dictionary or the path is null.</exception>
        public static string SuggestName(DxfDBDictionary imageDictionary, string newImagePathName)
        {
            if (imageDictionary == null)
            {
                throw new ArgumentNullException(nameof(imageDictionary));
            }

            if (newImagePathName == null)
            {
                throw new ArgumentNullException(nameof(newImagePathName));
            }

            string name = newImagePathName.Substring(newImagePathName.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            int dot = name.LastIndexOf('.');
            name = dot > 0 ? name.Substring(0, dot) : name;

            if (!imageDictionary.Contains(name))
            {
                return name;
            }

            for (long n = 1; ; n++)
            {
                string next = name + "_" + n.ToString(CultureInfo.InvariantCulture);

                if (!imageDictionary.Contains(next))
                {
                    return next;
                }
            }
        }

        /// <summary>Gives the definition as a line for debugging: its type, its handle and its path.</summary>
        /// <returns>The line, such as <c>IMAGEDEF 79 C:\pictures\px43.png</c>.</returns>
        public override string ToString() => "IMAGEDEF " + (Handle ?? string.Empty) + " " + SourceFileName;

        /// <summary>The groups a definition made in code is written with, after its subclass marker, in AutoCAD's order.</summary>
        internal IReadOnlyList<KeyValuePair<int, string>> MadeGroups => _made.AsReadOnly();

        private static KeyValuePair<int, string> Pair(int code, string value) =>
            new KeyValuePair<int, string>(code, value);

        /// <summary>The first group of a code of the definition's own part, as made in code or as the file writes it; null where it has none.</summary>
        private string Text(int code)
        {
            if (Item == null)
            {
                return _made.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();
            }

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>Sets groups of the definition, each in its place, as made in code or in its record.</summary>
        private void Set(params KeyValuePair<int, string>[] values)
        {
            if (Item != null)
            {
                InFile("be changed").SetValues(Item, values, Order, false);
                return;
            }

            foreach (KeyValuePair<int, string> value in values)
            {
                int at = _made.FindIndex(g => g.Key == value.Key);
                _made[at] = value;
            }
        }

        private double Real(int code, double fallback) =>
            Numbers.TryRead(Text(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int Whole(int code, int fallback) =>
            int.TryParse(Text(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
    }
}
