using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An IMAGE of a DXF file, as AutoCAD's <c>RasterImage</c>: a picture of pixels placed in the drawing, its file named by its
    /// IMAGEDEF (<see cref="ImageDef"/>), shown in a frame or clipped by a boundary. The library reads no pixels: it places,
    /// moves and clips the image as its record says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record holds the corner the image is placed at (groups 10, 20, 30), the lower left corner of its lower left pixel;
    /// the vector one pixel across (11, 21, 31) and one pixel up (12, 22, 32), in world coordinates; its size in pixels (13,
    /// 23); its look (70, 281, 282, 283); and its clip boundary in pixels (71, 91 and the run of 14, 24), whose (-0.5, -0.5)
    /// is the top left corner of the image, x along the pixels across and y down, as AutoCAD 2021 maps them. A picture of 4 by
    /// 3 pixels placed at (10, 20) with a pixel 10 across and 10 up is 40 by 30, from (10, 20) to (50, 50).
    /// </para>
    /// <para>
    /// It is clipped where its display options clip it (<see cref="IsClipped"/>, flag 4 of 70) and its record says a boundary
    /// was set (280 of 1): <see cref="GetVertices"/>, <see cref="DxfEntity.GetGeometry"/>, <see cref="GeometricExtents"/> and
    /// <see cref="Explode"/> follow the boundary then, and the frame of the whole image otherwise, as AutoCAD 2021 does.
    /// <see cref="DxfReader"/> reads no shape of it.
    /// </para>
    /// <para>
    /// A file of R12 has none: an IMAGE is not added to one. Every group the class does not set is kept as it was.
    /// </para>
    /// </remarks>
    public class DxfRasterImage : DxfEntity
    {
        // The groups of AcDbRasterImage in the order AutoCAD 2021 writes them, its inverted clip (290) from R2010 on.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[]
            {
                100,
                90,
                10,
                20,
                30,
                11,
                21,
                31,
                12,
                22,
                32,
                13,
                23,
                340,
                70,
                280,
                281,
                282,
                283,
                290,
                360,
                71,
                91,
                14,
                24,
            }
        );

        // The groups of the run of a clip boundary, rewritten together.
        private static readonly IReadOnlyCollection<int> BoundaryCodes = Array.AsReadOnly(new[] { 71, 91, 14, 24 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoPoint3 Across = new GeoPoint3(1.0, 0.0, 0.0);

        private static readonly GeoPoint3 Up = new GeoPoint3(0.0, 1.0, 0.0);

        // The definition an image made in code shows, until it is appended to the definition's drawing.
        private DxfRasterImageDef _definition;

        /// <summary>
        /// Initializes an image made in code of a definition of a drawing, as AutoCAD's <c>new RasterImage()</c> given its
        /// <c>ImageDefId</c>: one unit a pixel, its corner at the origin, so that the picture of 4 by 3 pixels is (4, 0, 0) by (0, 3,
        /// 0) until it is placed by <see cref="SetOrientation"/>; shown and clipped by the whole picture (70 of 7, 280 of 0), its
        /// brightness and contrast 50 and its fade 0, as AutoCAD 2021 writes a new one. Appended to a block of the definition's
        /// drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>, it is given its IMAGEDEF_REACTOR there, as
        /// AutoCAD's <c>AssociateRasterDef</c> gives one, and counted by <see cref="DxfRasterImageDef.GetEntityCount"/>.
        /// </summary>
        /// <param name="imageDefinition">The definition, of a drawing's dictionary of images.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is in no drawing, or erased.</exception>
        public DxfRasterImage(DxfRasterImageDef imageDefinition)
            : base("IMAGE", NewGroups(imageDefinition))
        {
            _definition = imageDefinition;
        }

        /// <summary>An image or a wipeout made in code from its groups, which begin with its subclass marker.</summary>
        private protected DxfRasterImage(string dxfName, IEnumerable<DxfTypedValue> groups)
            : base(dxfName, groups) { }

        private protected DxfRasterImage(DxfItem item)
            : base(item) { }

        #region Where it is

        /// <summary>
        /// Gets the corner of the image opposite the one it is placed at, as AutoCAD 2021's <c>Position</c> gives it: the
        /// placing corner (10) and the image's width and height along its pixels. The image 40 by 30 placed at (10, 20) gives
        /// (50, 50); the corner it is placed at is the origin of <see cref="GetOrientation"/>.
        /// </summary>
        public GeoPoint3 Position
        {
            get
            {
                GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
                return origin.Add(u).Add(v);
            }
        }

        /// <summary>
        /// Gives the corner the image is placed at and its two sides, as AutoCAD's <c>Orientation</c> gives them: the corner
        /// (group 10), the side along the bottom, the vector of one pixel across (11) times the pixels across (13), and the side
        /// up the left, the vector of one pixel up (12) times the pixels up (23). The image of 4 by 3 pixels 10 apart placed at
        /// (10, 20) gives (10, 20, 0), (40, 0, 0) and (0, 30, 0). The sides keep their lengths, and need not be square: AutoCAD
        /// keeps a sheared image so.
        /// </summary>
        /// <param name="origin">The corner the image is placed at, the lower left of its lower left pixel.</param>
        /// <param name="u">The side along the bottom of the image.</param>
        /// <param name="v">The side up its left.</param>
        public void GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v)
        {
            origin = GetPoint(10, Origin);
            u = PixelU.Multiply(PixelsAcross);
            v = PixelV.Multiply(PixelsUp);
        }

        /// <summary>
        /// Places the image anew, as AutoCAD's <c>Orientation</c> set: at a corner with its two sides, written as the vectors of
        /// one pixel (11 and 12), each side divided by the pixels along it; the clip boundary, in pixels, goes with the image.
        /// The image of 4 by 3 pixels set at (0, 0, 0) with the sides (80, 0, 0) and (0, 60, 0) has pixels 20 across and 20 up.
        /// The sides need not be square or of one scale, as AutoCAD 2021 takes them.
        /// </summary>
        /// <param name="origin">The corner the image is placed at.</param>
        /// <param name="u">The side along its bottom.</param>
        /// <param name="v">The side up its left, not along <paramref name="u"/>.</param>
        /// <exception cref="ArgumentException">Thrown when a point or side is not of finite numbers, a side has no length, or
        /// the two run along one line.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased, or its record gives it no pixels to
        /// divide the sides by.</exception>
        public void SetOrientation(GeoPoint3 origin, GeoVector3 u, GeoVector3 v)
        {
            CheckSides(u, v, nameof(u));
            double across = PixelsAcross;
            double up = PixelsUp;

            if (!(across > 0.0) || !(up > 0.0))
            {
                throw new InvalidOperationException(
                    "The "
                        + Item.Type
                        + " "
                        + (Handle ?? string.Empty)
                        + " is "
                        + across.ToString(CultureInfo.InvariantCulture)
                        + " by "
                        + up.ToString(CultureInfo.InvariantCulture)
                        + " pixels, and has no pixel to place its sides by."
                );
            }

            // A side given as it is read keeps the pixel it is read from, to the bit.
            GetOrientation(out _, out GeoVector3 nowU, out GeoVector3 nowV);
            WriteFrame(
                origin,
                SameVector(u, nowU) ? PixelU : u.Divide(across),
                SameVector(v, nowV) ? PixelV : v.Divide(up)
            );
        }

        /// <summary>Gets the length of the bottom of the image (the side <c>u</c> of <see cref="GetOrientation"/>), as AutoCAD's <c>Width</c>: 40 for 4 pixels 10 across.</summary>
        public double Width => PixelU.Length * PixelsAcross;

        /// <summary>Gets the length of the left of the image (the side <c>v</c> of <see cref="GetOrientation"/>), as AutoCAD's <c>Height</c>: 30 for 3 pixels 10 up.</summary>
        public double Height => PixelV.Length * PixelsUp;

        /// <summary>Gets how many pixels the image is across (group 13), as AutoCAD's <c>ImageWidth</c>: 4 for a picture of 4 by 3; one for a WIPEOUT.</summary>
        public double ImageWidth => PixelsAcross;

        /// <summary>Gets how many pixels the image is up (group 23), as AutoCAD's <c>ImageHeight</c>: 3 for a picture of 4 by 3; one for a WIPEOUT.</summary>
        public double ImageHeight => PixelsUp;

        /// <summary>
        /// Gets the size of the image in pixels, as AutoCAD's <c>ImageSize</c>: the size its record keeps (13, 23), or, not
        /// cached, the size its IMAGEDEF gives (10, 20), the record's where it has none. The library reads no image file: what
        /// AutoCAD reads of the file when it loads it is the size its IMAGEDEF was given.
        /// </summary>
        /// <param name="getCachedValue">Whether the size the record keeps is given rather than its IMAGEDEF's.</param>
        /// <returns>The pixels across and up: (4, 3) for a picture of 4 by 3.</returns>
        public GeoVector2 ImageSize(bool getCachedValue)
        {
            DxfRasterImageDef definition = getCachedValue ? null : ImageDef;
            return definition != null ? definition.Size : new GeoVector2(PixelsAcross, PixelsUp);
        }

        /// <summary>
        /// Gets or sets the angle of the bottom of the image in the world's plane xy, as AutoCAD 2021's <c>Rotation</c>: from the
        /// x axis, counter-clockwise, from 0 to 2 pi, of the vector of one pixel across seen from above; 0 for an image placed
        /// along x, pi for one mirrored about the y axis.
        /// </summary>
        /// <value>Set, as AutoCAD 2021 sets it: the vector of one pixel across turned to the angle in the plane xy and the one up
        /// a quarter turn on from it, each of its length, about the corner the image is placed at; an image standing in another
        /// plane is laid in the plane xy through that corner, and a mirrored or sheared one made square, as AutoCAD lays it. The
        /// angle it has, to the bit, changes nothing.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        public double Rotation
        {
            get
            {
                GeoVector3 u = PixelU;
                double angle = Math.Atan2(u.Y, u.X);
                return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            }
            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (Same(value, Rotation))
                {
                    return;
                }

                double across = PixelU.Length;
                double up = PixelV.Length;
                double cos = Math.Cos(value);
                double sin = Math.Sin(value);
                SetGroups(
                    Join(
                        PointGroups(11, new GeoPoint3(across * cos + 0.0, across * sin + 0.0, 0.0), Across),
                        PointGroups(12, new GeoPoint3(-up * sin + 0.0, up * cos + 0.0, 0.0), Up)
                    )
                );
            }
        }

        /// <summary>
        /// Gets the transformation from the pixels of the image to the world, as AutoCAD 2021's <c>PixelToModelTransform</c>:
        /// the pixel (x, y), counted from the centre of the top left pixel with y down, to its place, the third axis the image's
        /// normal. The picture of 4 by 3 pixels 10 apart placed at (10, 20) takes the pixel (0, 0) to (15, 45, 0) and (-0.5,
        /// -0.5), the top left corner, to (10, 50, 0).
        /// </summary>
        public GeoTransform3 PixelToModelTransform
        {
            get
            {
                GeoPoint3 origin = GetPoint(10, Origin);
                GeoVector3 u = PixelU;
                GeoVector3 v = PixelV;
                GeoVector3 cross = u.CrossProduct(v);
                double length = cross.Length;
                GeoVector3 normal =
                    length > 0.0 && !double.IsInfinity(length) ? cross.Divide(length) : new GeoVector3(0.0, 0.0, 1.0);
                GeoPoint3 corner = origin.Add(u.Multiply(0.5)).Add(v.Multiply(PixelsUp - 0.5));

                return new GeoTransform3(
                    new[,]
                    {
                        { u.X, -v.X, normal.X, corner.X },
                        { u.Y, -v.Y, normal.Y, corner.Y },
                        { u.Z, -v.Z, normal.Z, corner.Z },
                        { 0.0, 0.0, 0.0, 1.0 },
                    }
                );
            }
        }

        #endregion

        #region Its image

        /// <summary>
        /// Gets the IMAGEDEF the image shows (group 340), as AutoCAD's <c>ImageDefId</c> names it: of its drawing, of the drawing
        /// it was copied from for a copy in no file; null for a WIPEOUT, which shows none, and where the drawing has no IMAGEDEF
        /// of the handle.
        /// </summary>
        public DxfRasterImageDef ImageDef
        {
            get
            {
                if (!IsResident && _definition != null)
                {
                    return _definition.IsErased ? null : _definition;
                }

                string handle = GetString(340)?.Trim();
                DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;

                if (
                    string.IsNullOrEmpty(handle)
                    || handle == "0"
                    || database == null
                    || !database.TryGetObject(handle, out DxfDBObject found)
                )
                {
                    return null;
                }

                return found as DxfRasterImageDef;
            }
        }

        /// <summary>
        /// Gets or sets the name of the image, as AutoCAD's <c>Name</c>: the name its IMAGEDEF has in the dictionary of images
        /// (ACAD_IMAGE_DICT), such as <c>px43</c>; null where it shows no IMAGEDEF or none of the dictionary, as a WIPEOUT,
        /// for which AutoCAD 2021 throws.
        /// </summary>
        /// <value>Set, the entry of the definition renamed, as AutoCAD 2021 renames it: every image that shows the definition
        /// has the new name.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is empty, or the dictionary has another entry of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image shows no definition of a dictionary, as a WIPEOUT.</exception>
        public string Name
        {
            get => Images(out DxfRasterImageDef definition)?.NameOf(definition);
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                DxfDBDictionary images = Images(out DxfRasterImageDef definition);

                if (images == null)
                {
                    throw new InvalidOperationException(
                        "The "
                            + Item.Type
                            + " "
                            + (Handle ?? string.Empty)
                            + " shows no IMAGEDEF of a dictionary of images to rename."
                    );
                }

                string now = images.NameOf(definition);

                if (!string.Equals(now, value, StringComparison.Ordinal))
                {
                    Guard.Argument(value.Trim().Length > 0, nameof(value), "An image is named by some letters.");
                    Guard.Argument(
                        !images.Contains(value) || string.Equals(now, value, StringComparison.OrdinalIgnoreCase),
                        nameof(value),
                        "The dictionary of images has an entry " + value + " already."
                    );
                    images.Rename(definition, value);
                }
            }
        }

        /// <summary>
        /// Makes the image show a definition of its drawing, as AutoCAD's <c>AssociateRasterDef</c>: its 340 the definition, its
        /// size in pixels the definition's, its sides kept, so that the picture 40 by 30 of 4 by 3 pixels given one of 8 by 6
        /// stays 40 by 30 of 8 by 6, its boundary scaled with the pixels, a polygon another writer left not closed written closed,
        /// as AutoCAD 2021 cannot open a drawing of an image whose polygon is not; and its IMAGEDEF_REACTOR taken from the definition it
        /// showed to this one, or made, as AutoCAD 2021 makes one, so that <see cref="DxfRasterImageDef.GetEntityCount"/> counts it.
        /// </summary>
        /// <param name="definition">The definition, of the image's drawing; of any drawing for an image made in code in none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is erased, in no drawing, or of another drawing than
        /// the image's.</exception>
        /// <exception cref="NotSupportedException">Thrown for a WIPEOUT, which shows none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        public void AssociateRasterDef(DxfRasterImageDef definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (this is DxfWipeout)
            {
                throw new NotSupportedException("A wipeout shows no picture, and is associated with no IMAGEDEF.");
            }

            Guard.Argument(
                definition.IsResident && !definition.IsErased,
                nameof(definition),
                "The IMAGEDEF is in no drawing, or erased: add it to a dictionary of images first."
            );
            Guard.Argument(
                !IsResident || definition.Item.File == Item.File,
                nameof(definition),
                "The IMAGEDEF is of another drawing than the image."
            );
            DxfRasterImageDef before = ImageDef;
            GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            double across = PixelsAcross;
            double up = PixelsUp;
            GeoVector2 size = definition.Size;
            List<GeoPoint2> held = Boundary();
            bool clipped = GetInt(280, 0) == 1;
            WriteFrame(origin, u.Divide(size.X), v.Divide(size.Y));
            SetGroups(
                new KeyValuePair<int, string>(13, Numbers.Exact(size.X + 0.0)),
                new KeyValuePair<int, string>(23, Numbers.Exact(size.Y + 0.0)),
                new KeyValuePair<int, string>(340, definition.Handle)
            );

            if (clipped)
            {
                var scaled = new List<GeoPoint2>(held.Count);

                foreach (GeoPoint2 pixel in held)
                {
                    scaled.Add(
                        new GeoPoint2((pixel.X + 0.5) * size.X / across - 0.5, (pixel.Y + 0.5) * size.Y / up - 0.5)
                    );
                }

                WriteBoundary(
                    ClipBoundaryType == DxfClipBoundaryType.Invalid ? DxfClipBoundaryType.Rectangle : ClipBoundaryType,
                    scaled,
                    true
                );
            }
            else
            {
                SetClipBoundaryToWholeImage();
            }

            if (!IsResident)
            {
                // Kept as the copy goes into the definition's drawing, where its pointers would be taken for its origin's.
                _definition = definition;
                Item.KeptImageDef = definition.Handle;
                return;
            }

            DxfFile file = Item.File;
            string reactor = GetString(360)?.Trim();
            DxfItem held360 = string.IsNullOrEmpty(reactor) || reactor == "0" ? null : file.ObjectItem(reactor);

            if (held360 != null && held360.Type == "IMAGEDEF_REACTOR")
            {
                if (before != null && before.Item != null && !before.IsErased)
                {
                    file.SetReactor(before.Item, reactor, false);
                }

                file.SetReactor(definition.Item, reactor, true);
            }
            else
            {
                SetString(360, file.AddImageReactor(Item, definition.Item));
            }
        }

        /// <summary>How many corners of its boundary the record holds (the 14 after its 91).</summary>
        private protected int BoundaryCount => Boundary().Count;

        /// <summary>The handle of the IMAGEDEF the record names (340), as written; null where it names none.</summary>
        internal string ImageDefHandle => GetString(340)?.Trim();

        /// <summary>The dictionary of images that names the image's definition, and the definition; null where there is none.</summary>
        private DxfDBDictionary Images(out DxfRasterImageDef definition)
        {
            definition = ImageDef;
            DxfDatabase database = definition?.Item?.File.Database;

            if (
                database == null
                || definition.OwnerHandle == null
                || !database.TryGetObject(definition.OwnerHandle, out DxfDBObject owner)
                || !(owner is DxfDBDictionary images)
            )
            {
                return null;
            }

            return images;
        }

        /// <summary>The groups of a new image of a definition, as AutoCAD 2021 writes one given its <c>ImageDefId</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is in no drawing, or erased.</exception>
        private static IEnumerable<DxfTypedValue> NewGroups(DxfRasterImageDef definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.IsResident || definition.IsErased)
            {
                throw new ArgumentException(
                    "The IMAGEDEF is in no drawing, or erased: add it to a dictionary of images first, by DxfDBDictionary.SetAt.",
                    "imageDefinition"
                );
            }

            GeoVector2 size = definition.Size;
            string across = Numbers.Exact(size.X + 0.0);
            string up = Numbers.Exact(size.Y + 0.0);
            return new[]
            {
                new DxfTypedValue(100, "AcDbRasterImage"),
                new DxfTypedValue(90, "0"),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, "0"),
                new DxfTypedValue(11, "1"),
                new DxfTypedValue(21, "0"),
                new DxfTypedValue(31, "0"),
                new DxfTypedValue(12, "0"),
                new DxfTypedValue(22, "1"),
                new DxfTypedValue(32, "0"),
                new DxfTypedValue(13, across),
                new DxfTypedValue(23, up),
                new DxfTypedValue(340, definition.Handle),
                new DxfTypedValue(70, "7"),
                new DxfTypedValue(280, "0"),
                new DxfTypedValue(281, "50"),
                new DxfTypedValue(282, "50"),
                new DxfTypedValue(283, "0"),
                new DxfTypedValue(290, "0"),
                new DxfTypedValue(71, "1"),
                new DxfTypedValue(91, "2"),
                new DxfTypedValue(14, "-0.5"),
                new DxfTypedValue(24, "-0.5"),
                new DxfTypedValue(14, Numbers.Exact(size.X - 0.5)),
                new DxfTypedValue(24, Numbers.Exact(size.Y - 0.5)),
            };
        }

        /// <summary>
        /// Refuses an image made in code for a block of another drawing than its definition's, where it would point at nothing.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the definition is of another drawing, or erased.</exception>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (_definition != null && !IsResident)
            {
                Guard.Argument(
                    !_definition.IsErased && owner.File != null && _definition.Item.File == owner.File,
                    "entity",
                    "The image shows an IMAGEDEF of another drawing, or one erased: make it of a definition of this drawing."
                );
            }
        }

        /// <summary>
        /// Gives a copy of an image in no file the definition it was made of or <see cref="AssociateRasterDef"/> gave it, to be
        /// shown in the drawing it goes into as the image's is.
        /// </summary>
        private protected override void Copied(DxfEntity original)
        {
            if (original is DxfRasterImage image && image._definition != null && !image.IsResident)
            {
                _definition = image._definition;
                Item.KeptImageDef = image.Item.KeptImageDef;
            }
        }

        /// <summary>
        /// Gives an image made in code, or a copy given a definition by <see cref="AssociateRasterDef"/>, once appended, its
        /// IMAGEDEF_REACTOR, as AutoCAD 2021's <c>AssociateRasterDef</c> gives one.
        /// </summary>
        internal override void Appended(bool madeInCode)
        {
            DxfRasterImageDef definition = _definition;
            _definition = null;
            Item.KeptImageDef = null;

            if (
                definition != null
                && !(this is DxfWipeout)
                && definition.Item != null
                && definition.Item.File == Item.File
            )
            {
                SetString(360, Item.File.AddImageReactor(Item, definition.Item));
            }
        }

        /// <summary>
        /// Gets the file of the picture, as AutoCAD's <c>Path</c>: the path its IMAGEDEF names (group 1), as the file writes it;
        /// null where it shows none, as a WIPEOUT, for which AutoCAD 2021 throws.
        /// </summary>
        public string Path => ImageDef?.SourceFileName;

        #endregion

        #region How it is shown

        /// <summary>
        /// Gets or sets how the image is shown (group 70), as the DXF reference, ezdxf and ACadSharp read the flags; AutoCAD 2021
        /// writes 7 for a new image, and 15 once its transparency is on.
        /// </summary>
        /// <value>Set, the flags written as their number; AutoCAD 2021's own <c>DisplayOptions</c> writes 255 for
        /// <see cref="DxfImageDisplayOptions.Show"/> with <see cref="DxfImageDisplayOptions.Transparent"/>, 9, which the library
        /// does not follow.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the options set have a flag outside 1, 2, 4 and 8.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when the options are set on a WIPEOUT, whose 70 AutoCAD 2021 keeps at 7
        /// whatever it is set to.</exception>
        public DxfImageDisplayOptions DisplayOptions
        {
            get => (DxfImageDisplayOptions)GetInt(70, 0);
            set
            {
                if (((int)value & ~15) != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The display options of an image are the flags 1, 2, 4 and 8."
                    );
                }

                if (value != DisplayOptions)
                {
                    NotOfAWipeout("display options");
                    SetInt(70, (int)value, 0);
                }
            }
        }

        /// <summary>Gets or sets whether the image is shown (flag 1 of group 70), as AutoCAD's <c>ShowImage</c>; set, written as <see cref="DisplayOptions"/> writes it.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as <see cref="DisplayOptions"/> says.</exception>
        public bool ShowImage
        {
            get => (DisplayOptions & DxfImageDisplayOptions.Show) != 0;
            set => SetOption(DxfImageDisplayOptions.Show, value);
        }

        /// <summary>Gets or sets whether the transparent pixels of the image are shown as such (flag 8 of group 70), as AutoCAD's <c>ImageTransparency</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as <see cref="DisplayOptions"/> says.</exception>
        public bool ImageTransparency
        {
            get => (DisplayOptions & DxfImageDisplayOptions.Transparent) != 0;
            set => SetOption(DxfImageDisplayOptions.Transparent, value);
        }

        /// <summary>
        /// Gets or sets whether the image is clipped by its boundary, as AutoCAD 2021's <c>IsClipped</c> reads and sets it: flag 4
        /// of group 70, which AutoCAD sets on every new image. The boundary clips it where its record also says one was set (280
        /// of 1, which <see cref="SetClipBoundary"/> writes): the image clipped by a triangle and given false keeps its triangle,
        /// and shows its whole frame again, its 280 left at 1, as AutoCAD 2021 leaves it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as <see cref="DisplayOptions"/> says.</exception>
        public bool IsClipped
        {
            get => (DisplayOptions & DxfImageDisplayOptions.Clip) != 0;
            set => SetOption(DxfImageDisplayOptions.Clip, value);
        }

        /// <summary>
        /// Gets or sets whether the boundary keeps the image outside it rather than inside (group 290), as ObjectARX's
        /// <c>isClipInverted</c>, which AutoCAD 2021's .NET API does not give; false where the record gives none, as in every
        /// file before R2010. The boundary, its vertices and the lines it explodes into stay as they are.
        /// </summary>
        /// <value>Set true, written 1 from R2010 on; set false, written 0, or left out in a drawing before R2010 that another
        /// writer gave a 290; a copy into a drawing before R2010 leaves its 290 behind.</value>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased, or set true in a file older than R2010
        /// (AC1024), which has no group 290.</exception>
        public bool IsClipInverted
        {
            get => GetInt(290, 0) != 0;
            set
            {
                if (value == IsClipInverted)
                {
                    return;
                }

                // Taken off in a drawing before R2010, as another writer may give one there, by leaving the group out.
                if (!value && !Item.File.Supports(1024))
                {
                    SetString(290, null);
                    return;
                }

                Item.File.Requires(1024, "inverted clip of an image", "R2010 (AC1024)");
                SetString(290, value ? "1" : "0");
            }
        }

        /// <summary>Gets or sets the brightness of the image (group 281), as AutoCAD's <c>Brightness</c>: 0 to 100, 50 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is above 100, as AutoCAD answers eInvalidInput.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as AutoCAD 2021 refuses every value.</exception>
        public byte Brightness
        {
            get => Level(281, 50);
            set => SetLevel(281, value, 50, "brightness");
        }

        /// <summary>Gets or sets the contrast of the image (group 282), as AutoCAD's <c>Contrast</c>: 0 to 100, 50 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is above 100, as AutoCAD answers eInvalidInput.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as AutoCAD 2021 refuses every value.</exception>
        public byte Contrast
        {
            get => Level(282, 50);
            set => SetLevel(282, value, 50, "contrast");
        }

        /// <summary>Gets or sets how faded the image is (group 283), as AutoCAD's <c>Fade</c>: 0 to 100, 0 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is above 100, as AutoCAD answers eInvalidInput.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown when it is set on a WIPEOUT, as AutoCAD 2021 refuses every value.</exception>
        public byte Fade
        {
            get => Level(283, 0);
            set => SetLevel(283, value, 0, "fade");
        }

        #endregion

        #region Its clip boundary

        /// <summary>Gets the kind of the image's clip boundary (group 71), as AutoCAD's <c>ClipBoundaryType</c>; <see cref="DxfClipBoundaryType.Invalid"/> where the record gives neither 1 nor 2.</summary>
        public DxfClipBoundaryType ClipBoundaryType
        {
            get
            {
                int type = GetInt(71, 0);
                return type == 1 || type == 2 ? (DxfClipBoundaryType)type : DxfClipBoundaryType.Invalid;
            }
        }

        /// <summary>
        /// Gets the clip boundary of the image in pixels, as AutoCAD 2021's <c>GetClipBoundary</c> gives it read from a file: a
        /// rectangle of the two corners its record holds as its five corners, the first again at the end, from its least corner,
        /// (x, y) of the least of each, up its x to the greatest y and round, whichever way the two are held; a polygon as its
        /// record holds it. The picture of 4 by 3 pixels not clipped holds (-0.5, -0.5) and (3.5, 2.5),
        /// and gives (-0.5, -0.5), (-0.5, 2.5), (3.5, 2.5), (3.5, -0.5), (-0.5, -0.5). The pixels are counted from the centre
        /// of the top left one, y down; a WIPEOUT's from -0.5 to 0.5 of its frame.
        /// </summary>
        /// <returns>The corners, a new list on each call; none where the record holds none.</returns>
        public IReadOnlyList<GeoPoint2> GetClipBoundary()
        {
            List<GeoPoint2> held = Boundary();

            return (
                ClipBoundaryType == DxfClipBoundaryType.Rectangle && held.Count == 2 ? Corners(held) : held
            ).AsReadOnly();
        }

        /// <summary>
        /// Gets the corners of the image as it is shown, in world coordinates, the first again at the end, as AutoCAD 2021's
        /// <c>GetVertices</c> gives them. Clipped (<see cref="IsClipped"/>, and a boundary set, 280 of 1), the corners of its
        /// boundary taken to the world (<see cref="PixelToModelTransform"/>): a rectangle from its least corner, (x, y) of the
        /// least of each, up its x to the greatest y and on; a polygon in its order. Else the frame of the whole image, from
        /// the corner it is placed at up its left side and round: the picture 40 by 30 placed at (10, 20) gives (10, 20, 0),
        /// (10, 50, 0), (50, 50, 0), (50, 20, 0) and (10, 20, 0); clipped by the triangle of the pixels (0, 0), (3, 0), (1.5,
        /// 2.5), it gives (15, 45, 0), (45, 45, 0), (30, 20, 0) and (15, 45, 0).
        /// </summary>
        /// <returns>The corners, a new list on each call.</returns>
        public IReadOnlyList<GeoPoint3> GetVertices() => Vertices(new Held(GetOwnGroups(0))).AsReadOnly();

        /// <summary>
        /// Clips the image by a boundary, as AutoCAD's <c>SetClipBoundary</c>: a rectangle given by its five corners, the first
        /// again at the end, written as the first and the third, as AutoCAD 2021 writes it; a polygon of three corners or more,
        /// its first again at the end, written as given, the last as the first. The boundary is in pixels, counted from the
        /// centre of the top left pixel with y down, and may cross itself or leave the image, as AutoCAD takes it; its record
        /// is said to be clipped (280 of 1). <see cref="IsClipped"/> is left as it is: the image clips by the boundary where it
        /// is on, as AutoCAD sets it on every new image.
        /// </summary>
        /// <remarks>
        /// The picture of 4 by 3 pixels 10 apart placed at (10, 20), clipped by the rectangle (0.5, 0.5), (0.5, 1.5), (2.5,
        /// 1.5), (2.5, 0.5), (0.5, 0.5), shows the rectangle from (20, 30, 0) to (40, 40, 0). AutoCAD 2021 refuses a rectangle
        /// of two corners or four, and a boundary not closed by its first corner to its rounding, which the library closes
        /// where the last corner is within the point tolerance of <see cref="Tolerance.Default"/> of the first in the world, 0.001
        /// whatever the size of a pixel: on a wipeout's frame of 10 000, a corner 0.0009 of the frame off, 9 away, is not.
        /// </remarks>
        /// <param name="type">The kind of boundary, a rectangle or a polygon.</param>
        /// <param name="clipBoundaryVertices">The corners, in pixels.</param>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind is neither, a corner is not of finite numbers, the last is not
        /// the first, a rectangle is not of five corners or a polygon of four or more, as AutoCAD answers eInvalidInput.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        public void SetClipBoundary(DxfClipBoundaryType type, IReadOnlyList<GeoPoint2> clipBoundaryVertices)
        {
            if (clipBoundaryVertices == null)
            {
                throw new ArgumentNullException(nameof(clipBoundaryVertices));
            }

            Guard.Argument(
                type == DxfClipBoundaryType.Rectangle || type == DxfClipBoundaryType.Poly,
                nameof(type),
                "An image is clipped by a rectangle or a polygon, and AutoCAD answers eInvalidInput for " + type + "."
            );
            int count = clipBoundaryVertices.Count;

            foreach (GeoPoint2 corner in clipBoundaryVertices)
            {
                Guard.Argument(
                    Guard.IsFinite(corner.X) && Guard.IsFinite(corner.Y),
                    nameof(clipBoundaryVertices),
                    "A corner of a boundary has to be of finite numbers: " + corner + " is not."
                );
            }

            Guard.Argument(
                type == DxfClipBoundaryType.Rectangle ? count == 5 : count >= 4,
                nameof(clipBoundaryVertices),
                type == DxfClipBoundaryType.Rectangle
                    ? "A rectangle is given by its five corners, the first again at the end, as AutoCAD 2021 takes it; "
                        + count.ToString(CultureInfo.InvariantCulture)
                        + " were given."
                    : "A polygon is given by three corners or more, the first again at the end, as AutoCAD 2021 takes it; "
                        + count.ToString(CultureInfo.InvariantCulture)
                        + " were given."
            );
            Guard.Argument(
                World(clipBoundaryVertices[count - 1]).IsEqualTo(World(clipBoundaryVertices[0]), Tolerance.Default),
                nameof(clipBoundaryVertices),
                "A boundary ends on its first corner, as AutoCAD 2021 takes it, within the point tolerance in the world: "
                    + clipBoundaryVertices[count - 1]
                    + " is not "
                    + clipBoundaryVertices[0]
                    + "."
            );

            var corners = new List<GeoPoint2>();

            if (type == DxfClipBoundaryType.Rectangle)
            {
                corners.Add(clipBoundaryVertices[0]);
                corners.Add(clipBoundaryVertices[2]);
            }
            else
            {
                for (int i = 0; i < count - 1; i++)
                {
                    corners.Add(clipBoundaryVertices[i]);
                }

                corners.Add(clipBoundaryVertices[0]);
            }

            WriteBoundary(type, corners, true);
        }

        /// <summary>
        /// Clips the image by nothing, as AutoCAD's <c>SetClipBoundaryToWholeImage</c>: its boundary the rectangle of the whole
        /// image, from (-0.5, -0.5) to the pixels across and up less a half, (3.5, 2.5) for a picture of 4 by 3 and (0.5, 0.5)
        /// for a WIPEOUT, and its record said to be clipped by none (280 of 0), as AutoCAD 2021 writes it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        public void SetClipBoundaryToWholeImage() =>
            WriteBoundary(
                DxfClipBoundaryType.Rectangle,
                new List<GeoPoint2> { new GeoPoint2(-0.5, -0.5), new GeoPoint2(PixelsAcross - 0.5, PixelsUp - 0.5) },
                false
            );

        #endregion

        #region As an entity

        /// <summary>
        /// Gets the box of the image as it is shown, as AutoCAD's <c>GeometricExtents</c>: that of <see cref="GetVertices"/>,
        /// (10, 20, 0) to (50, 50, 0) for the picture 40 by 30 placed at (10, 20), (15, 20, 0) to (45, 45, 0) clipped by the
        /// triangle of the pixels (0, 0), (3, 0), (1.5, 2.5); clipped inverted (<see cref="IsClipInverted"/>), which shows what
        /// lies outside the triangle, that of its whole frame, (10, 20, 0) to (50, 50, 0), as AutoCAD 2021 gives it, its vertices
        /// and the lines it explodes into still those of the triangle.
        /// </summary>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                // Clipped inverted, the image shows what lies outside its boundary, and AutoCAD 2021 gives the frame's box.
                var held = new Held(GetOwnGroups(0));
                List<GeoPoint3> corners = held.Inverted ? Frame(held) : Vertices(held);
                double[] box =
                {
                    double.PositiveInfinity,
                    double.PositiveInfinity,
                    double.PositiveInfinity,
                    double.NegativeInfinity,
                    double.NegativeInfinity,
                    double.NegativeInfinity,
                };

                foreach (GeoPoint3 corner in corners)
                {
                    box[0] = Math.Min(box[0], corner.X);
                    box[1] = Math.Min(box[1], corner.Y);
                    box[2] = Math.Min(box[2], corner.Z);
                    box[3] = Math.Max(box[3], corner.X);
                    box[4] = Math.Max(box[4], corner.Y);
                    box[5] = Math.Max(box[5], corner.Z);
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        /// <summary>
        /// Gets the lines the image breaks into, as AutoCAD 2021's <c>Explode</c> breaks an IMAGE and a WIPEOUT: a LINE along each
        /// side of <see cref="GetVertices"/>, the frame of the image not clipped and its boundary clipped, each with the image's
        /// look, a side of no length left out. The picture 40 by 30 placed at (10, 20) gives four LINEs, from (10, 20, 0) to (10,
        /// 50, 0) and round; clipped by a triangle, three. AutoCAD's lines have no look of their own, to take the drawing's when
        /// appended.
        /// </summary>
        /// <returns>The lines, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            List<GeoPoint3> corners = Vertices(new Held(GetOwnGroups(0)));
            var lines = new List<DxfEntity>();
            double tolerance = Tolerance.Default.EqualPoint;

            for (int i = 0; i + 1 < corners.Count; i++)
            {
                if (corners[i].DistanceTo(corners[i + 1]) > tolerance)
                {
                    var line = new DxfLine(corners[i], corners[i + 1]);
                    GiveLook(line);
                    lines.Add(line);
                }
            }

            return lines.AsReadOnly();
        }

        /// <summary>
        /// Moves, turns, scales, mirrors or shears the image, as AutoCAD 2021's <c>TransformBy</c> does: the corner it is placed
        /// at moved, and the vectors of one pixel across and up turned and scaled, as ezdxf and ACadSharp move one, its boundary
        /// in pixels going with it. AutoCAD takes any such transformation: scaled 2 along x alone, the picture 40 by 30 is 80 by
        /// 30; sheared, its sides lean; mirrored about the y axis, it is seen from behind, its pixel across (-10, 0, 0). The
        /// record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentException">Thrown when the transformation takes a side of the image to no length, or the two
        /// onto one line.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            GeoVector3 u = transform.Transform(PixelU);
            GeoVector3 v = transform.Transform(PixelV);
            CheckSides(u, v, nameof(transform));
            WriteFrame(transform.Transform(GetPoint(10, Origin)), u, v);
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<object> OwnGeometry()
        {
            List<GeoPoint3> corners = Vertices(new Held(GetOwnGroups(0)));
            var kept = new List<GeoPoint3>();

            foreach (GeoPoint3 corner in corners)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(corner, Tolerance.Default))
                {
                    kept.Add(corner);
                }
            }

            return kept.Count < 2
                ? Array.AsReadOnly(new object[0])
                : Array.AsReadOnly(new object[] { new GeoPolyline3(kept.ToArray(), true) });
        }

        [DxfType("IMAGE")]
        internal static DxfEntity Create(DxfItem item) => new DxfRasterImage(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        #endregion

        #region What it is made of

        /// <summary>The vector of one pixel across (11, 21, 31); (1, 0, 0) where the record gives none.</summary>
        private GeoVector3 PixelU => Vector(GetPoint(11, Across));

        /// <summary>The vector of one pixel up (12, 22, 32); (0, 1, 0) where the record gives none.</summary>
        private GeoVector3 PixelV => Vector(GetPoint(12, Up));

        /// <summary>The pixels across (13); one where the record gives none.</summary>
        private double PixelsAcross => GetDouble(13, 1.0);

        /// <summary>The pixels up (23); one where the record gives none.</summary>
        private double PixelsUp => GetDouble(23, 1.0);

        /// <summary>The corners of the boundary the record holds (the run of 14, 24 after its count, 91), in its order.</summary>
        private List<GeoPoint2> Boundary()
        {
            IReadOnlyList<DxfTypedValue> groups = GetOwnGroups(0);
            var corners = new List<GeoPoint2>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 14)
                {
                    continue;
                }

                double x = DxfLeaderGroups.Num(groups[i].Value, 0.0);
                double y =
                    i + 1 < groups.Count && groups[i + 1].TypeCode == 24
                        ? DxfLeaderGroups.Num(groups[++i].Value, 0.0)
                        : 0.0;
                corners.Add(new GeoPoint2(x, y));
            }

            return corners;
        }

        /// <summary>A pixel of the boundary in the world, as <see cref="PixelToModelTransform"/> takes it.</summary>
        private GeoPoint3 World(GeoPoint2 pixel) =>
            GetPoint(10, Origin).Add(PixelU.Multiply(pixel.X + 0.5)).Add(PixelV.Multiply(PixelsUp - 0.5 - pixel.Y));

        /// <summary>The five corners of a rectangle held as two, from its least corner up its x and round, as AutoCAD 2021 reads one back.</summary>
        private static List<GeoPoint2> Corners(List<GeoPoint2> held)
        {
            double x0 = Math.Min(held[0].X, held[1].X);
            double y0 = Math.Min(held[0].Y, held[1].Y);
            double x1 = Math.Max(held[0].X, held[1].X);
            double y1 = Math.Max(held[0].Y, held[1].Y);
            return new List<GeoPoint2>
            {
                new GeoPoint2(x0, y0),
                new GeoPoint2(x0, y1),
                new GeoPoint2(x1, y1),
                new GeoPoint2(x1, y0),
                new GeoPoint2(x0, y0),
            };
        }

        /// <summary>The frame of the whole image, from the corner it is placed at up its left side and round, the first again at the end.</summary>
        private static List<GeoPoint3> Frame(Held held)
        {
            GeoPoint3 origin = held.Origin;
            GeoVector3 across = held.U.Multiply(held.Across);
            GeoVector3 side = held.V.Multiply(held.Up);
            return new List<GeoPoint3>
            {
                origin,
                origin.Add(side),
                origin.Add(across).Add(side),
                origin.Add(across),
                origin,
            };
        }

        /// <summary>
        /// What the corners and the box of an image are worked out from, read in one pass over the groups of its record, each
        /// the first of its code and read as the getters read it: the corner (10), the pixels across and up (11, 12) and their
        /// counts (13, 23), the display options (70), whether a boundary was set (280), its kind (71), the inverted clip (290)
        /// and the run of the boundary (14, 24).
        /// </summary>
        private sealed class Held
        {
            internal Held(IReadOnlyList<DxfTypedValue> groups)
            {
                // A slot a code, tried at the first group of the code: 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, then 70, 280,
                // 71, 290; a number found, or not, is not tried again.
                var tried = new bool[Codes.Length];
                var found = new bool[Codes.Length];
                var values = new double[Codes.Length];
                Boundary = new List<GeoPoint2>();

                for (int i = 0; i < groups.Count; i++)
                {
                    int code = groups[i].TypeCode;

                    if (code == 14)
                    {
                        double x = DxfLeaderGroups.Num(groups[i].Value, 0.0);
                        double y =
                            i + 1 < groups.Count && groups[i + 1].TypeCode == 24
                                ? DxfLeaderGroups.Num(groups[++i].Value, 0.0)
                                : 0.0;
                        Boundary.Add(new GeoPoint2(x, y));
                        continue;
                    }

                    int slot = Array.IndexOf(Codes, code);

                    if (slot < 0 || tried[slot])
                    {
                        continue;
                    }

                    tried[slot] = true;
                    string text = groups[i].Value;

                    if (slot >= Wholes)
                    {
                        found[slot] = int.TryParse(
                            text?.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int whole
                        );
                        values[slot] = whole;
                    }
                    else
                    {
                        found[slot] = Numbers.TryRead(text, out double value) && Guard.IsFinite(value);
                        values[slot] = found[slot] ? value : 0.0;
                    }
                }

                Origin = Point(found, values, 0, new GeoPoint3(0.0, 0.0, 0.0));
                GeoPoint3 u = Point(found, values, 3, new GeoPoint3(1.0, 0.0, 0.0));
                GeoPoint3 v = Point(found, values, 6, new GeoPoint3(0.0, 1.0, 0.0));
                U = new GeoVector3(u.X, u.Y, u.Z);
                V = new GeoVector3(v.X, v.Y, v.Z);
                Across = found[9] ? values[9] : 1.0;
                Up = found[10] ? values[10] : 1.0;
                Flags = found[11] ? (int)values[11] : 0;
                Clipping = found[12] ? (int)values[12] : 0;
                Kind = found[13] ? (int)values[13] : 0;
                Inverted = found[14] && values[14] != 0.0;
            }

            /// <summary>The codes read, in their slots: the numbers, then from <see cref="Wholes"/> on the whole numbers.</summary>
            private static readonly int[] Codes = { 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 70, 280, 71, 290 };

            private const int Wholes = 11;

            internal GeoPoint3 Origin { get; }

            internal GeoVector3 U { get; }

            internal GeoVector3 V { get; }

            internal double Across { get; }

            internal double Up { get; }

            internal int Flags { get; }

            internal int Clipping { get; }

            internal int Kind { get; }

            internal bool Inverted { get; }

            internal List<GeoPoint2> Boundary { get; }

            /// <summary>A point of three slots as GetPoint reads one: a coordinate left out nought, the fallback where all are.</summary>
            private static GeoPoint3 Point(bool[] found, double[] values, int slot, GeoPoint3 fallback) =>
                found[slot] || found[slot + 1] || found[slot + 2]
                    ? new GeoPoint3(values[slot], values[slot + 1], values[slot + 2])
                    : fallback;
        }

        /// <summary>The corners of <see cref="GetVertices"/>, as it says.</summary>
        private static List<GeoPoint3> Vertices(Held record)
        {
            GeoPoint3 origin = record.Origin;
            GeoVector3 u = record.U;
            GeoVector3 v = record.V;
            double up = record.Up;
            List<GeoPoint2> held = record.Boundary;

            // Clipped where the display options clip it and its record says a boundary was set, and holds one.
            if (
                !(
                    (record.Flags & 4) != 0
                    && record.Clipping == 1
                    && held.Count >= 2
                    && (record.Kind == 1 || record.Kind == 2)
                )
            )
            {
                return Frame(record);
            }

            List<GeoPoint2> pixels = record.Kind == 1 && held.Count == 2 ? Corners(held) : held;

            var corners = new List<GeoPoint3>(pixels.Count + 1);

            foreach (GeoPoint2 pixel in pixels)
            {
                corners.Add(origin.Add(u.Multiply(pixel.X + 0.5)).Add(v.Multiply(up - 0.5 - pixel.Y)));
            }

            if (!corners[corners.Count - 1].IsEqualTo(corners[0], Tolerance.Default))
            {
                corners.Add(corners[0]);
            }

            return corners;
        }

        /// <summary>Writes the boundary's kind, count and corners in one run, where its kind stands (71), and whether the record is clipped (280).</summary>
        private void WriteBoundary(DxfClipBoundaryType type, List<GeoPoint2> corners, bool clipped)
        {
            // A polygon ends on its first corner to the bit, as AutoCAD 2021 cannot open a drawing of an image whose polygon does
            // not, and reads a wipeout's so left as of no corners: one another writer left open is closed as it is written anew.
            if (
                type == DxfClipBoundaryType.Poly
                && corners.Count >= 3
                && !(
                    Same(corners[0].X, corners[corners.Count - 1].X) && Same(corners[0].Y, corners[corners.Count - 1].Y)
                )
            )
            {
                corners.Add(corners[0]);
            }

            SetInt(280, clipped ? 1 : 0, -1);
            var groups = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(71, ((int)type).ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(91, corners.Count.ToString(CultureInfo.InvariantCulture)),
            };

            foreach (GeoPoint2 corner in corners)
            {
                groups.Add(new KeyValuePair<int, string>(14, Numbers.Exact(corner.X + 0.0)));
                groups.Add(new KeyValuePair<int, string>(24, Numbers.Exact(corner.Y + 0.0)));
            }

            ReplaceGroups(0, BoundaryCodes, groups.ToArray());
        }

        /// <summary>Writes the corner the image is placed at (10) and the vectors of one pixel (11, 12) in one writing.</summary>
        private protected void WriteFrame(GeoPoint3 origin, GeoVector3 u, GeoVector3 v) =>
            SetGroups(
                Join(
                    PointGroups(10, origin, Origin),
                    PointGroups(11, new GeoPoint3(u.X, u.Y, u.Z), Across),
                    PointGroups(12, new GeoPoint3(v.X, v.Y, v.Z), Up)
                )
            );

        /// <summary>Refuses sides not of finite numbers, of no length, or along one line.</summary>
        private protected static void CheckSides(GeoVector3 u, GeoVector3 v, string parameter)
        {
            double a = u.Length;
            double b = v.Length;
            double cross = u.CrossProduct(v).Length;

            if (!u.IsValid || !v.IsValid || !(a > 0.0) || !(b > 0.0) || double.IsInfinity(a) || double.IsInfinity(b))
            {
                throw new ArgumentException(
                    "The sides of an image have to be of finite numbers and some length: "
                        + u
                        + " and "
                        + v
                        + " are not.",
                    parameter
                );
            }

            if (!(cross > Tolerance.Default.EqualVector * a * b))
            {
                throw new ArgumentException(
                    "The sides " + u + " and " + v + " of the image run along one line.",
                    parameter
                );
            }
        }

        /// <summary>A level of the image's look, 0 to 255 as the record gives it; the fallback where it gives none.</summary>
        private byte Level(int code, int fallback) => (byte)Math.Max(0, Math.Min(255, GetInt(code, fallback)));

        /// <summary>Sets a level of the image's look, refused above 100 and on a WIPEOUT, as AutoCAD 2021 refuses them.</summary>
        private void SetLevel(int code, byte value, int fallback, string what)
        {
            if (value > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The " + what + " of an image is from 0 to 100, as AutoCAD answers eInvalidInput above."
                );
            }

            if (value != Level(code, fallback))
            {
                NotOfAWipeout(what);
                SetInt(code, value, fallback);
            }
        }

        /// <summary>Sets one of the display options on or off.</summary>
        private void SetOption(DxfImageDisplayOptions option, bool on)
        {
            DxfImageDisplayOptions now = DisplayOptions;
            DxfImageDisplayOptions then = on ? now | option : now & ~option;

            if (then != now)
            {
                NotOfAWipeout("display options");
                SetInt(70, (int)then, 0);
            }
        }

        /// <summary>Refuses a setting a WIPEOUT has not, as AutoCAD 2021 refuses or ignores it.</summary>
        private void NotOfAWipeout(string what)
        {
            if (this is DxfWipeout)
            {
                throw new NotSupportedException(
                    "A wipeout hides what is under it in the colour of the background, and has no "
                        + what
                        + " of an image to set: AutoCAD 2021 refuses its brightness, contrast and fade, and keeps its display options at 7."
                );
            }
        }

        private static GeoVector3 Vector(GeoPoint3 point) => new GeoVector3(point.X, point.Y, point.Z);

        private static bool SameVector(GeoVector3 a, GeoVector3 b) =>
            Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        #endregion
    }
}
