using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An OLE2FRAME of a DXF file, as AutoCAD's <c>Ole2Frame</c>: an OLE object shown in a frame, a picture pasted, a sheet of
    /// Excel embedded or a document linked. The library reads where it stands and what it is, gives the picture of a static
    /// one, and moves, scales and sizes it as AutoCAD 2021 does, upright along x and y.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The object is its binary (every group 310 in turn, whatever length its 90 gives): AutoCAD's head of 128 bytes, which
    /// holds the four corners of the frame in world coordinates, then the compound file the object is saved in, as Windows'
    /// OLE writes it. The groups 10 and 11 are copies of two of the corners, which AutoCAD 2021 does not read: where a file another
    /// writer moved gives other corners there, the head's are taken, as AutoCAD takes them, and the difference is said once
    /// in the log at Debug. AutoCAD keeps the turn and look of the frame beside the binary, in the extended data of ACAD
    /// between OLEBEGIN and OLEEND (<see cref="Rotation"/>, <see cref="LockAspect"/>, <see cref="AutoOutputQuality"/>), and
    /// how it is plotted in group 73 (<see cref="OutputQuality"/>).
    /// </para>
    /// <para>
    /// A picture of 3 by 2 pixels pasted with its lower left corner at (10, 20, 0) and stretched to 30 by 20 has its head's
    /// corners (10, 40, 0), (40, 40, 0), (40, 20, 0) and (10, 20, 0): <see cref="Location"/> (10, 20, 0), <see cref="WcsWidth"/>
    /// 30, <see cref="WcsHeight"/> 20, the box of <see cref="GeometricExtents"/> from (10, 20, 0) to (40, 40, 0).
    /// </para>
    /// <para>
    /// A binary too short to hold the corners, or one that holds a corner not of finite numbers, gives those of 10 and 11 as
    /// the corners of a frame along the world's x and y, as ezdxf and ACadSharp read every frame; a compound file damaged gives
    /// the kind of the object (group 71) and its user type (group 3) as the record writes them, and no picture. Where AutoCAD
    /// 2021 cannot read the compound file it empties the frame when it opens the drawing, its corners and user type gone.
    /// </para>
    /// <para>
    /// It is copied, by <see cref="DxfEntity.Clone"/>, into this drawing or another, with its binary and its extended data as
    /// they are, and given the marks of the space it is copied into; read and saved, its record is written byte for byte.
    /// <see cref="DxfEntity.TransformBy"/> moves its corners in the head of the binary, which AutoCAD reads, and in groups 10
    /// and 11; AutoCAD 2021 keeps a frame upright, so a move, a scale by one factor and a mirror about a line along x or y are
    /// taken, the picture not turned over, and a turn, a shear or a stretch refused, as is an INSERT exploded with such a
    /// placing, which leaves the frame out. AutoCAD's <c>OleObject</c>, the object of the running
    /// application, and <c>Position2d</c>, the frame in pixels of the window it is shown in, are not given: a file holds
    /// neither. The OLEFRAME of AutoCAD R13, an object of OLE 1, has no class of AutoCAD's .NET API, and stays a
    /// <see cref="DxfUnknownEntity"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfOle2Frame : DxfEntity
    {
        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        // The own groups in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[] { 100, 70, 3, 10, 20, 30, 11, 21, 31, 71, 72, 73, 90, 310, 1 }
        );

        // Whether 10 and 11 have been checked against the head's corners for the log, or the log told the head holds none.
        private bool _told;

        /// <summary>
        /// Makes a static picture of a bitmap or a PNG, as AutoCAD 2021 makes one of a bitmap pasted: the .bmp given, or the
        /// .bmp of 32 bits a pixel the PNG is read into, its alpha laid over white, as the stream <c>CONTENTS</c> of a compound
        /// file of the class <c>StaticDib</c>, as Windows' OLE saves it, behind AutoCAD's head of 128 bytes and with the
        /// extended data AutoCAD writes for it. The library's own: AutoCAD's .NET API makes no OLE
        /// object. The frame stands along x and y with its lower left corner at the origin, at the picture's natural size in
        /// millimetres, its pixels across and up over its pixels a metre (3 780, 96 dots an inch, where the picture gives none):
        /// a picture of 610 by 224 pixels of 96 dots an inch is 161.38 by 59.26. Placed and sized by <see cref="Position3d"/>,
        /// <see cref="WcsWidth"/> or <see cref="DxfEntity.TransformBy"/>; added to a drawing by
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>, which gives it the marks of its space.
        /// </summary>
        /// <param name="picture">The bytes of a .bmp file (a BITMAPFILEHEADER, then a BITMAPINFOHEADER of 40 bytes or one of its
        /// later forms, of 1, 4, 8, 16, 24 or 32 bits a pixel, not compressed: BI_RGB, or BI_BITFIELDS for 16 and 32), or of a
        /// .png file of any kind PNG has, of no more than 64 million pixels. A JPEG or another kind is refused: the library
        /// reads no other, and the caller turns it into one of these first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the picture is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the bytes are not such a picture, with the reason.</exception>
        public DxfOle2Frame(byte[] picture)
            : base("OLE2FRAME", NewGroups(picture))
        {
            var data = new List<DxfTypedValue> { new DxfTypedValue(1001, "ACAD") };
            data.AddRange(NewOleData(WcsWidth, WcsHeight));
            XData = data;
        }

        private DxfOle2Frame(DxfItem item)
            : base(item) { }

        #region Where it is

        /// <summary>
        /// Gets the lower left corner of the frame, as AutoCAD 2021's <c>Location</c> gives it from the head of its binary: (10,
        /// 20, 0) for the picture pasted with its lower left corner there. The DXF reference has group 10 for it, which is the
        /// upper left corner, and ODA's documents too; AutoCAD 2021 gives the lower left. The origin for a frame of no corners, its
        /// binary holding none and its record no 10 or 11, as AutoCAD 2021 gives a frame it could not read.
        /// </summary>
        public GeoPoint3 Location => Corners()[3];

        /// <summary>
        /// Gets the four corners of the frame in world coordinates, as AutoCAD 2021's <c>Position3d</c> gives them from the head
        /// of its binary, as they are held, of any shape AutoCAD stores: turned, sheared or out of the plane xy. The picture 30 by
        /// 20 with its lower left corner at (10, 20, 0) gives the upper left (10, 40, 0), upper right (40, 40, 0), lower left
        /// (10, 20, 0) and lower right (40, 20, 0).
        /// </summary>
        /// <value>
        /// Set, the frame is put on the corners given, written into the head of its binary and into groups 10 and 11 as AutoCAD
        /// writes them: a frame along x and y only, as AutoCAD 2021 keeps every frame it moves or sizes; the upper left (10, 60,
        /// 0) and lower right (70, 20, 0) of a rectangle 60 by 40 make the frame 60 wide and 40 high. The size it had at 100
        /// percent (<see cref="ScaleWidth"/>) is kept.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when a corner is not of finite numbers, the rectangle is not along x and y
        /// to the point tolerance (its top and bottom along x, its sides along y, its upper left above and left of its lower
        /// right), or has no width or height.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, or its binary holds no head to write the
        /// corners into.</exception>
        public DxfRectangle3d Position3d
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return new DxfRectangle3d(corners[0], corners[1], corners[3], corners[2]);
            }
            set
            {
                GeoPoint3[] corners = { value.UpperLeft, value.UpperRight, value.LowerRight, value.LowerLeft };
                double tolerance = Tolerance.Default.EqualPoint;

                foreach (GeoPoint3 corner in corners)
                {
                    if (!corner.IsValid)
                    {
                        throw new ArgumentException(
                            "The corners of an OLE frame have to be of finite numbers: " + corner + " is not.",
                            nameof(value)
                        );
                    }
                }

                bool upright =
                    Math.Abs(corners[0].Y - corners[1].Y) <= tolerance
                    && Math.Abs(corners[3].Y - corners[2].Y) <= tolerance
                    && Math.Abs(corners[0].X - corners[3].X) <= tolerance
                    && Math.Abs(corners[1].X - corners[2].X) <= tolerance;

                if (
                    !upright
                    || !(corners[1].X - corners[0].X > tolerance)
                    || !(corners[0].Y - corners[3].Y > tolerance)
                )
                {
                    throw new ArgumentException(
                        "An OLE frame stands along x and y, its upper left above and left of its lower right, as AutoCAD 2021 keeps it: "
                            + value
                            + " does not.",
                        nameof(value)
                    );
                }

                WriteCorners(corners);
            }
        }

        /// <summary>
        /// Gets the width of the frame, as AutoCAD 2021's <c>WcsWidth</c> works it out: the x of the upper right corner less that
        /// of the upper left, its run along x and not the length of its top, so that the frame 30 wide turned 30 degrees about z
        /// is 25.98 wide; 30 for the picture 30 by 20.
        /// </summary>
        /// <value>
        /// Set, the frame is made that wide with its upper left corner kept, as AutoCAD 2021 sizes it, and as high in the same
        /// measure where <see cref="LockAspect"/> is true: the picture 30 by 20 set 60 wide is 60 by 40, or 60 by 20 unlocked.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, has no width to size, or its binary holds
        /// no head to write the corners into.</exception>
        public double WcsWidth
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return corners[1].X - corners[0].X;
            }
            set => Size(value, true, nameof(value));
        }

        /// <summary>
        /// Gets the height of the frame, as AutoCAD 2021's <c>WcsHeight</c> works it out: the y of the upper left corner less
        /// that of the lower left, its run along y; 20 for the picture 30 by 20.
        /// </summary>
        /// <value>
        /// Set, the frame is made that high with its upper left corner kept, and as wide in the same measure where
        /// <see cref="LockAspect"/> is true: the picture 30 by 20 set 40 high is 60 by 40.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, has no height to size, or its binary
        /// holds no head to write the corners into.</exception>
        public double WcsHeight
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return corners[0].Y - corners[3].Y;
            }
            set => Size(value, false, nameof(value));
        }

        /// <summary>
        /// Gets the angle the frame is said to be turned by, in radians, as AutoCAD 2021's <c>Rotation</c> reads it: the number
        /// AutoCAD keeps in the extended data (code 40 between OLEBEGIN and OLEEND), not worked out from the corners, which it
        /// keeps along x and y as it turns a frame; the number as it is stored, a NaN another writer stored given as it is
        /// (AutoCAD 2021 writes 0 for the NaN it holds in memory after a quarter turn); 0 where the record has no such data or
        /// no number there.
        /// </summary>
        public double Rotation => OleData(40, 0.0, true);

        /// <summary>
        /// Gets the width of the frame in percent of its width at 100 percent, which AutoCAD keeps in the extended data (code 41
        /// between OLEBEGIN and OLEEND) as it pastes the object and does not change as it scales or sizes the frame: the picture
        /// pasted 83.58 wide and scaled by a half in AutoCAD 2021 is 41.79 wide with 83.58 kept, 50 percent. 100 where the
        /// record keeps no such width; 0 for a frame of no width. AutoCAD's console gives 100 for every frame it reads; its
        /// palette shows the scale against the kept width.
        /// </summary>
        /// <value>
        /// Set, the frame is made that share of the kept width with its upper left corner kept, and the same share of the kept
        /// height where <see cref="LockAspect"/> is true, as AutoCAD 2021 sizes it: the picture kept 83.58 by 30.67 set to 50 is
        /// 41.79 by 15.33.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the percentage is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, has no width to size, or its binary holds
        /// no head to write the corners into.</exception>
        public double ScaleWidth
        {
            get => Scale(true);
            set => SizeByScale(value, true, nameof(value));
        }

        /// <summary>
        /// Gets the height of the frame in percent of its height at 100 percent, which AutoCAD keeps in the extended data (code
        /// 42 between OLEBEGIN and OLEEND), as <see cref="ScaleWidth"/> gives the width's: 100 where the record keeps none; 0
        /// for a frame of no height.
        /// </summary>
        /// <value>
        /// Set, the frame is made that share of the kept height with its upper left corner kept, and the same share of the kept
        /// width where <see cref="LockAspect"/> is true.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the percentage is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, has no height to size, or its binary
        /// holds no head to write the corners into.</exception>
        public double ScaleHeight
        {
            get => Scale(false);
            set => SizeByScale(value, false, nameof(value));
        }

        #endregion

        #region How it is shown

        /// <summary>
        /// Gets whether a change of the width or height of the frame changes the other with it, as AutoCAD 2021's
        /// <c>LockAspect</c> reads it from the extended data (code 71 between OLEBEGIN and OLEEND, 1 for locked); true where the
        /// record has none, as AutoCAD gives a frame read with no extended data.
        /// </summary>
        /// <value>Set, written there; the extended data AutoCAD writes for a frame is made where the record has none.</value>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public bool LockAspect
        {
            get => OleData(71, 1.0) != 0.0;
            set => SetOleData(71, value ? 1 : 0);
        }

        /// <summary>
        /// Gets how the object is plotted, as AutoCAD 2021's <c>OutputQuality</c> reads it from group 73: 0 monochrome, 1 low
        /// graphics, 2 high graphics, as AutoCAD writes a picture pasted; 1 where the record has none, as AutoCAD gives a new
        /// frame.
        /// </summary>
        /// <value>Set, written in group 73: 0, 1 or 2, the three of AutoCAD 2021's palette.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is above 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public byte OutputQuality
        {
            get => (byte)Math.Max(0, Math.Min(255, GetInt(73, 1)));
            set
            {
                if (value > 2)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The plot quality of an OLE frame is 0 (monochrome), 1 (low graphics) or 2 (high graphics), as AutoCAD 2021's palette offers."
                    );
                }

                SetGroups(new KeyValuePair<int, string>(73, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// Gets whether the plot quality is chosen by the kind of object, as AutoCAD 2021's <c>AutoOutputQuality</c> reads it
        /// from the extended data (code 70 between OLEBEGIN and OLEEND): 1 chosen so, as AutoCAD writes a picture pasted, 0 the
        /// <see cref="OutputQuality"/> kept; 1 where the record has none.
        /// </summary>
        public byte AutoOutputQuality => (byte)Math.Max(0.0, Math.Min(255.0, OleData(70, 1.0)));

        #endregion

        #region What it is

        /// <summary>
        /// Gets the kind of the object, as AutoCAD's <c>Type</c>, MFC's <c>COleClientItem::GetType</c>, finds it in the compound
        /// file: <see cref="DxfOle2FrameItemType.Link"/> where it is of the class of a link or its <c>\1Ole</c> stream says it is
        /// linked, <see cref="DxfOle2FrameItemType.Static"/> where it is of the class of a static picture (a bitmap, a metafile, an
        /// enhanced metafile), <see cref="DxfOle2FrameItemType.Embedded"/> for any other class, as a sheet of Excel or a file
        /// packed. Where the compound file is damaged, the kind group 71 gives; 0, no kind, where that is none of the three
        /// either, as ObjectARX's <c>getType</c> gives 0 on an error.
        /// </summary>
        public DxfOle2FrameItemType Type
        {
            get
            {
                DxfCompoundFile file = DxfOleData.Compound(Binary(), out _);

                if (file != null)
                {
                    return DxfOleData.TypeOf(file);
                }

                int written = GetInt(71, 0);
                return written >= 1 && written <= 3 ? (DxfOle2FrameItemType)written : 0;
            }
        }

        /// <summary>
        /// Gets the name of the kind of the object, as AutoCAD's <c>UserType</c>: <c>Picture (Device Independent Bitmap)</c> for a
        /// bitmap pasted. AutoCAD 2021 asks Windows' registry for the name of the class of the compound file; the library gives
        /// the name of a static picture's class as Windows names it (<c>Picture (Metafile)</c>, <c>Picture (Enhanced
        /// Metafile)</c>), and for another class the name its <c>\1CompObj</c> stream holds, as <c>OLE Package</c>; where the
        /// compound file is damaged or holds no name, group 3 as the record writes it; empty where it has none.
        /// </summary>
        public string UserType
        {
            get
            {
                DxfCompoundFile file = DxfOleData.Compound(Binary(), out _);
                return (file == null ? null : DxfOleData.UserTypeOf(file)) ?? GetString(3) ?? string.Empty;
            }
        }

        /// <summary>Gets whether the object is linked to a document outside the drawing, as AutoCAD's <c>IsLinked</c>: whether <see cref="Type"/> is <see cref="DxfOle2FrameItemType.Link"/>.</summary>
        public bool IsLinked => Type == DxfOle2FrameItemType.Link;

        /// <summary>
        /// Gets the source a linked object is linked to, as ObjectARX's <c>getLinkName</c> words it: the display name of the
        /// moniker its <c>\1Ole</c> stream holds, its absolute one where it has one, such as
        /// <c>C:\Drawings\cost.xlsx!Sheet1!R1C1:R4C3</c>, its file and the item in it; empty for an object not linked, and where
        /// the moniker is not of a file, an item or a composite of them. AutoCAD 2021 throws for a static object; the library
        /// gives it empty.
        /// </summary>
        public string LinkName => Link(true);

        /// <summary>
        /// Gets the file a linked object is linked to, as ObjectARX's <c>getLinkPath</c> words it: the path of the first file of
        /// its moniker, <c>C:\Drawings\cost.xlsx</c> for the link to a range of that workbook; empty for an object not linked,
        /// as <see cref="LinkName"/> says.
        /// </summary>
        public string LinkPath => Link(false);

        /// <summary>
        /// Gives the compound file the object is saved in, as it is written after the head of the binary: as many bytes as the
        /// head counts, or as there are where it counts more; null where the binary is too short to hold its head of 128 bytes.
        /// The library's own, as ODA's <c>getCompoundDocument</c>: AutoCAD's .NET API gives none. Written to a file, it is a
        /// compound file Windows opens, a .doc of OLE's structured storage.
        /// </summary>
        /// <returns>The bytes, a new array on each call.</returns>
        public byte[] GetCompoundDocument() => DxfOleData.CompoundBytes(Binary());

        /// <summary>
        /// Gives the picture of a static object as the bytes of its file, the library's own: of a bitmap pasted, the .bmp its
        /// <c>CONTENTS</c> stream holds, as Windows wrote it; of a metafile, a placeable .wmf; of an enhanced metafile, the .emf.
        /// The picture of 3 by 2 pixels of 24 bits gives a .bmp of 78 bytes, of 96 dots an inch where it was so made. None for an
        /// object embedded or linked, which AutoCAD shows through its application, and for a compound file damaged, which is said
        /// in the log at Debug.
        /// </summary>
        /// <param name="format">The kind of file given; <see cref="DxfOlePictureFormat.None"/> where none is.</param>
        /// <returns>The bytes, a new array on each call; null where there is no picture.</returns>
        public byte[] GetPictureData(out DxfOlePictureFormat format)
        {
            format = DxfOlePictureFormat.None;
            DxfCompoundFile file = DxfOleData.Compound(Binary(), out string why);
            byte[] picture = file == null ? null : DxfOleData.Picture(file, out format, out why);

            if (picture == null && why != null)
            {
                GeometryHelperLog.Debug(
                    "DxfOle2Frame: the OLE2FRAME " + (Handle ?? string.Empty) + " gives no picture: " + why + "."
                );
            }

            return picture;
        }

        #endregion

        #region As an entity

        /// <summary>
        /// Gets the box of the frame, as AutoCAD 2021's <c>GeometricExtents</c>: that of its four corners, z among them, (10, 20,
        /// 0) to (40, 40, 0) for the picture 30 by 20 with its lower left corner at (10, 20, 0).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for a frame of no corners, its binary holding none and its record no
        /// 10 or 11, as accoreconsole writes one it could not read: AutoCAD 2021 gives it none either, and
        /// <see cref="DxfDatabase.UpdateExt"/> passes it over.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                GeoPoint3[] corners = Corners(out bool held);

                if (!held)
                {
                    throw new InvalidOperationException(
                        TypeAndHandle + " holds no corners, in its binary or in groups 10 and 11, to give it extents."
                    );
                }

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
        /// Gets the lines the frame breaks into, as AutoCAD 2021's <c>Explode</c> breaks an OLE2FRAME into four LINEs along its
        /// edges: a LINE along each side of the frame of <see cref="DxfEntity.GetGeometry"/>, from the upper left corner round
        /// by the upper right, each with the frame's look, a side of no length left out. The picture 30 by 20 gives four, from
        /// (10, 40, 0) to (40, 40, 0) and round. The object itself is not kept. A frame of no corners, its binary holding none and
        /// its record no 10 or 11, gives no lines: AutoCAD 2021 gives four of no length, which the library leaves out, as it does
        /// for an IMAGE.
        /// </summary>
        /// <returns>The lines, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            List<GeoPoint3> corners = Frame();
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
        /// Moves the frame as AutoCAD 2021 moves one, standing along x and y: its four corners moved by a transformation that
        /// takes x along x and y along y, each by one scale, written into the head of its binary, which AutoCAD reads, and into
        /// groups 10 and 11; mirrored about a line along x or y, or turned half round, the frame is the box of its corners so
        /// moved, the picture not turned over, as AutoCAD 2021 mirrors one. Scaled, the size of its text (code 43 of the extended
        /// data) is scaled with it and its size at 100 percent kept, as AutoCAD writes them. A frame of no corners is left as it
        /// is.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for a transformation that would turn, shear or stretch the frame out of
        /// its stand along x and y, or out of the plane xy: AutoCAD 2021 keeps a frame upright and only moves it for a turn.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the binary holds no head to write the corners into.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            GeoPoint3[] corners = Corners(out bool held);

            if (!held)
            {
                return;
            }

            double factor = UprightFactor(transform, out bool straight);
            var moved = new GeoPoint3[4];

            for (int k = 0; k < 4; k++)
            {
                moved[k] = transform.Transform(corners[k]);
            }

            if (!straight)
            {
                double left = Math.Min(Math.Min(moved[0].X, moved[1].X), Math.Min(moved[2].X, moved[3].X));
                double right = Math.Max(Math.Max(moved[0].X, moved[1].X), Math.Max(moved[2].X, moved[3].X));
                double bottom = Math.Min(Math.Min(moved[0].Y, moved[1].Y), Math.Min(moved[2].Y, moved[3].Y));
                double top = Math.Max(Math.Max(moved[0].Y, moved[1].Y), Math.Max(moved[2].Y, moved[3].Y));
                double z = (moved[0].Z + moved[1].Z + moved[2].Z + moved[3].Z) / 4.0;
                moved = new[]
                {
                    new GeoPoint3(left, top, z),
                    new GeoPoint3(right, top, z),
                    new GeoPoint3(right, bottom, z),
                    new GeoPoint3(left, bottom, z),
                };
            }

            WriteCorners(moved);

            if (factor != 1.0)
            {
                double size = OleData(43, double.NaN);

                if (Guard.IsFinite(size))
                {
                    SetOleData(43, size * factor);
                }
            }
        }

        /// <summary>
        /// Called once the frame is written into a block: a frame copied into model space or a layout is given the marks AutoCAD
        /// 2021 writes for that space, the head's signature (0x5580 in model space, 0x5581 in paper space), its u32 at 102 (1 and
        /// 0) and group 72 (0 and 1). A frame of another block, or one whose binary holds no head, is left as it is.
        /// </summary>
        internal override void Appended(bool madeInCode)
        {
            string block = BlockName;
            bool model = string.Equals(block, "*Model_Space", StringComparison.OrdinalIgnoreCase);
            bool paper = block != null && block.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

            if (model || paper)
            {
                MarkSpace(paper);
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <inheritdoc/>
        private protected override IReadOnlyList<object> OwnGeometry()
        {
            var kept = new List<GeoPoint3>();

            foreach (GeoPoint3 corner in Frame())
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

        [DxfType("OLE2FRAME")]
        internal static DxfEntity Create(DxfItem item) => new DxfOle2Frame(item);

        #endregion

        #region What it is made of

        /// <summary>The bytes of the binary (310), all of them.</summary>
        private byte[] Binary() => DxfOleData.Binary(GetOwnGroups(0), -1);

        /// <summary>
        /// The own groups of a static picture of a bitmap, as AutoCAD 2021 writes one pasted in model space: the user type, its
        /// upper left and lower right corners, static (71 of 3), high graphics (73 of 2), the binary in lines of 32 bytes and
        /// the closing <c>OLE</c>.
        /// </summary>
        private static DxfTypedValue[] NewGroups(byte[] picture)
        {
            if (picture == null)
            {
                throw new ArgumentNullException(nameof(picture));
            }

            byte[] bitmap = picture;
            string why;

            if (DxfPng.IsPng(picture) && (bitmap = DxfPng.ToBitmap(picture, out why)) == null)
            {
                throw new ArgumentException(
                    "The bytes are not a PNG an OLE frame takes: " + why + ".",
                    nameof(picture)
                );
            }

            if (
                !DxfOleData.TryBitmap(bitmap, out int across, out int up, out int perMetreX, out int perMetreY, out why)
            )
            {
                throw new ArgumentException(
                    "The bytes are not a bitmap or a PNG an OLE frame takes: " + why + ".",
                    nameof(picture)
                );
            }

            double width = across * 1000.0 / perMetreX;
            double height = up * 1000.0 / perMetreY;
            GeoPoint3[] corners =
            {
                new GeoPoint3(0.0, height, 0.0),
                new GeoPoint3(width, height, 0.0),
                new GeoPoint3(width, 0.0, 0.0),
                Origin,
            };
            byte[] binary = DxfOleData.StaticPicture(
                bitmap,
                corners,
                (int)(across * 100000L / perMetreX),
                (int)(up * 100000L / perMetreY)
            );
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbOle2Frame"),
                new DxfTypedValue(70, "2"),
                new DxfTypedValue(3, DxfOleData.DibUserType),
                new DxfTypedValue(10, Numbers.Exact(corners[0].X)),
                new DxfTypedValue(20, Numbers.Exact(corners[0].Y)),
                new DxfTypedValue(30, "0.0"),
                new DxfTypedValue(11, Numbers.Exact(corners[2].X)),
                new DxfTypedValue(21, Numbers.Exact(corners[2].Y)),
                new DxfTypedValue(31, "0.0"),
                new DxfTypedValue(71, "3"),
                new DxfTypedValue(72, "0"),
                new DxfTypedValue(73, "2"),
                new DxfTypedValue(90, binary.Length.ToString(CultureInfo.InvariantCulture)),
            };

            for (int at = 0; at < binary.Length; at += 32)
            {
                groups.Add(
                    new DxfTypedValue(
                        310,
                        BitConverter.ToString(binary, at, Math.Min(32, binary.Length - at)).Replace("-", string.Empty)
                    )
                );
            }

            groups.Add(new DxfTypedValue(1, "OLE"));
            return groups.ToArray();
        }

        /// <summary>
        /// The one scale of a transformation that keeps a frame along x and y: x taken along x, y along y and z along z, either
        /// way, each by that scale, to the vector tolerance; <paramref name="straight"/> where neither x nor y is turned about.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for any other transformation.</exception>
        private static double UprightFactor(GeoTransform3 transform, out bool straight)
        {
            GeoVector3 x = transform.Transform(new GeoVector3(1.0, 0.0, 0.0));
            GeoVector3 y = transform.Transform(new GeoVector3(0.0, 1.0, 0.0));
            GeoVector3 z = transform.Transform(new GeoVector3(0.0, 0.0, 1.0));
            double scale = Math.Abs(x.X);
            double tolerance = Tolerance.Default.EqualVector * scale;
            bool upright =
                scale > 0.0
                && Guard.IsFinite(scale)
                && Math.Abs(x.Y) <= tolerance
                && Math.Abs(x.Z) <= tolerance
                && Math.Abs(y.X) <= tolerance
                && Math.Abs(y.Z) <= tolerance
                && Math.Abs(z.X) <= tolerance
                && Math.Abs(z.Y) <= tolerance
                && Math.Abs(Math.Abs(y.Y) - scale) <= tolerance
                && Math.Abs(Math.Abs(z.Z) - scale) <= tolerance;

            if (!upright)
            {
                throw new ArgumentException(
                    "An OLE frame stands along x and y and is moved, scaled by one factor or mirrored about a line along x or y only: AutoCAD 2021 keeps it upright and does not turn, shear or stretch it.",
                    nameof(transform)
                );
            }

            straight = x.X > 0.0 && y.Y > 0.0;
            return scale;
        }

        /// <summary>
        /// Puts the frame on four corners, upper left, upper right, lower right and lower left: into the head of its binary,
        /// which AutoCAD reads, then into groups 10 and 11, its upper left and lower right, where the record has them, the
        /// extents of the drawing grown to hold it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the binary holds no head to write the corners into.</exception>
        private void WriteCorners(GeoPoint3[] corners)
        {
            RewriteHead(head =>
            {
                for (int k = 0; k < 4; k++)
                {
                    Array.Copy(BitConverter.GetBytes(corners[k].X), 0, head, 2 + 24 * k, 8);
                    Array.Copy(BitConverter.GetBytes(corners[k].Y), 0, head, 10 + 24 * k, 8);
                    Array.Copy(BitConverter.GetBytes(corners[k].Z), 0, head, 18 + 24 * k, 8);
                }
            });

            var groups = new List<KeyValuePair<int, string>>();

            if (GetString(10) != null)
            {
                groups.AddRange(PointGroups(10, corners[0], Origin));
            }

            if (GetString(11) != null)
            {
                groups.AddRange(PointGroups(11, corners[2], Origin));
            }

            if (groups.Count > 0)
            {
                SetGroups(groups.ToArray());
            }
        }

        /// <summary>
        /// Gives the frame the marks of model or paper space AutoCAD 2021 writes: the head's signature and its u32 at 102, and
        /// group 72; nothing where the binary holds no head.
        /// </summary>
        private void MarkSpace(bool paper)
        {
            try
            {
                RewriteHead(head =>
                {
                    head[0] = (byte)(paper ? 0x81 : 0x80);
                    head[1] = 0x55;
                    Array.Copy(BitConverter.GetBytes(paper ? 0 : 1), 0, head, 102, 4);
                });
            }
            catch (InvalidOperationException)
            {
                return;
            }

            SetGroups(new KeyValuePair<int, string>(72, paper ? "1" : "0"));
        }

        /// <summary>
        /// Writes the head of the binary anew as <paramref name="edit"/> changes its 128 bytes: only the lines of binary those
        /// bytes are in are written, each keeping its count of bytes and the case of its letters; nothing where the bytes are as
        /// they were.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the binary holds no head, or its first lines are not pairs of
        /// hexadecimal digits that can be written back as they were.</exception>
        private void RewriteHead(Action<byte[]> edit)
        {
            var places = new List<int>();
            IReadOnlyList<DxfTypedValue> own = Item.File.OwnGroupsOf(Item, 0, places);
            var lines = new List<string>();
            int first = -1;
            int last = -1;
            int bytes = 0;

            for (int i = 0; i < own.Count && bytes < DxfOleData.HeadSize; i++)
            {
                if (own[i].TypeCode != 310)
                {
                    if (first >= 0)
                    {
                        break;
                    }

                    continue;
                }

                first = first < 0 ? i : first;
                last = i;
                lines.Add(own[i].Value);
                bytes += DxfOleData.Digits(own[i].Value) / 2;
            }

            byte[] head = DxfOleData.Binary(own, DxfOleData.HeadSize);

            if (first < 0 || bytes < DxfOleData.HeadSize || head.Length < DxfOleData.HeadSize)
            {
                throw new InvalidOperationException(
                    TypeAndHandle
                        + " holds no head of "
                        + DxfOleData.HeadSize
                        + " bytes in its binary to write the frame into."
                );
            }

            byte[] was = (byte[])head.Clone();
            edit(head);

            if (StructuralEquals(was, head))
            {
                return;
            }

            string[] written = DxfOleData.Patched(lines, head);

            if (written == null)
            {
                throw new InvalidOperationException(
                    TypeAndHandle
                        + " has first lines of binary that are not pairs of hexadecimal digits, which could not be written back as they were."
                );
            }

            var groups = new List<KeyValuePair<int, string>>(written.Length);

            foreach (string line in written)
            {
                groups.Add(new KeyValuePair<int, string>(310, line));
            }

            Item.File.SpliceRecord(Item, 0, places[2 * first], places[2 * last + 1], groups, false);
        }

        /// <summary>Whether two arrays of bytes are the same, byte for byte.</summary>
        private static bool StructuralEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The width (or height) in percent of the one kept at 100 percent in the extended data, as <see cref="ScaleWidth"/> says.</summary>
        private double Scale(bool width)
        {
            double now = width ? WcsWidth : WcsHeight;

            if (now == 0.0)
            {
                return 0.0;
            }

            double kept = OleData(width ? 41 : 42, double.NaN);
            return Guard.IsFinite(kept) && kept != 0.0 ? now / kept * 100.0 : 100.0;
        }

        /// <summary>Sizes the frame to a share of its size kept at 100 percent, as <see cref="ScaleWidth"/> says.</summary>
        private void SizeByScale(double percent, bool width, string parameter)
        {
            if (!Guard.IsFinite(percent) || !(percent > 0.0))
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    percent,
                    "A scale of an OLE frame is a finite percentage above nought."
                );
            }

            double now = width ? WcsWidth : WcsHeight;
            double kept = OleData(width ? 41 : 42, double.NaN);
            double whole = Guard.IsFinite(kept) && kept > 0.0 ? kept : now;
            double other = OleData(width ? 42 : 41, double.NaN);
            double otherNow = width ? WcsHeight : WcsWidth;
            double otherWhole = Guard.IsFinite(other) && other > 0.0 ? other : otherNow;
            Resize(width, whole * percent / 100.0, otherWhole * percent / 100.0, parameter);
        }

        /// <summary>Sizes the frame along one side, the other in the same measure where the aspect is locked, as <see cref="WcsWidth"/> says.</summary>
        private void Size(double value, bool width, string parameter)
        {
            if (!Guard.IsFinite(value) || !(value > 0.0))
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    value,
                    "A side of an OLE frame is a finite length above nought."
                );
            }

            double now = width ? WcsWidth : WcsHeight;

            if (!(now > 0.0))
            {
                throw new InvalidOperationException(
                    TypeAndHandle + " has no " + (width ? "width" : "height") + " to size."
                );
            }

            double otherNow = width ? WcsHeight : WcsWidth;
            Resize(width, value, otherNow * value / now, parameter);
        }

        /// <summary>
        /// Puts the frame at a new size, its upper left corner kept: <paramref name="side"/> along the side asked, and
        /// <paramref name="other"/> along the other where the aspect is locked, the other side kept where it is not.
        /// </summary>
        private void Resize(bool width, double side, double other, string parameter)
        {
            GeoPoint3[] corners = Corners(out bool held);

            if (!held)
            {
                throw new InvalidOperationException(TypeAndHandle + " holds no corners to size.");
            }

            double w = width ? side : WcsWidth;
            double h = width ? WcsHeight : side;

            if (LockAspect)
            {
                if (width)
                {
                    h = other;
                }
                else
                {
                    w = other;
                }
            }

            if (!Guard.IsFinite(w) || !Guard.IsFinite(h) || !(w > 0.0) || !(h > 0.0))
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    side,
                    "The frame would be " + w + " by " + h + ", not a finite size above nought."
                );
            }

            GeoPoint3 upperLeft = corners[0];
            WriteCorners(
                new[]
                {
                    upperLeft,
                    new GeoPoint3(upperLeft.X + w, upperLeft.Y, corners[1].Z),
                    new GeoPoint3(upperLeft.X + w, upperLeft.Y - h, corners[2].Z),
                    new GeoPoint3(upperLeft.X, upperLeft.Y - h, corners[3].Z),
                }
            );
        }

        /// <summary>
        /// Sets a number of the extended data of ACAD between OLEBEGIN and OLEEND, as AutoCAD keeps it: a 1070 of the code, then
        /// a 1070 for 70 to 72 or a 1040 for 40 to 44. The data AutoCAD writes for a new frame is made where the record has none,
        /// and the code put before OLEEND where the data has it not.
        /// </summary>
        private void SetOleData(int code, double value)
        {
            int kind = code >= 70 ? 1070 : 1040;
            string text = kind == 1070 ? ((int)value).ToString(CultureInfo.InvariantCulture) : Numbers.Exact(value);
            var groups = new List<DxfTypedValue>(XData);
            int start = groups.FindIndex(g =>
                g.TypeCode == 1001 && string.Equals(g.Value?.Trim(), "ACAD", StringComparison.OrdinalIgnoreCase)
            );

            if (start < 0)
            {
                start = groups.Count;
                groups.Add(new DxfTypedValue(1001, "ACAD"));
            }

            int end = groups.FindIndex(start + 1, g => g.TypeCode == 1001);
            end = end < 0 ? groups.Count : end;
            int begin = groups.FindIndex(start + 1, end - start - 1, g =>
                g.TypeCode == 1000 && string.Equals(g.Value?.Trim(), "OLEBEGIN", StringComparison.Ordinal)
            );

            if (begin < 0)
            {
                groups.InsertRange(end, NewOleData(WcsWidth, WcsHeight));
                begin = end;
            }

            int close = groups.FindIndex(begin + 1, g =>
                g.TypeCode == 1000 && string.Equals(g.Value?.Trim(), "OLEEND", StringComparison.Ordinal)
            );
            close = close < 0 ? groups.Count : close;

            for (int i = begin + 1; i + 1 < close; i++)
            {
                if (groups[i].TypeCode == 1000)
                {
                    continue;
                }

                if (
                    groups[i].TypeCode == 1070
                    && int.TryParse(
                        groups[i].Value?.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int at
                    )
                )
                {
                    if (at == code)
                    {
                        groups[i + 1] = new DxfTypedValue(kind, text);
                        XData = groups;
                        return;
                    }

                    i++;
                }
            }

            groups.InsertRange(
                close,
                new[]
                {
                    new DxfTypedValue(1070, code.ToString(CultureInfo.InvariantCulture)),
                    new DxfTypedValue(kind, text),
                }
            );
            XData = groups;
        }

        /// <summary>The extended data AutoCAD 2021 writes for a picture pasted of a size, from OLEBEGIN to OLEEND.</summary>
        internal static List<DxfTypedValue> NewOleData(double width, double height)
        {
            string sixth = Numbers.Exact(1.0 / 6.0);
            return new List<DxfTypedValue>
            {
                new DxfTypedValue(1000, "OLEBEGIN"),
                new DxfTypedValue(1070, "70"),
                new DxfTypedValue(1070, "1"),
                new DxfTypedValue(1070, "71"),
                new DxfTypedValue(1070, "1"),
                new DxfTypedValue(1070, "40"),
                new DxfTypedValue(1040, "0.0"),
                new DxfTypedValue(1070, "41"),
                new DxfTypedValue(1040, Numbers.Exact(width)),
                new DxfTypedValue(1070, "42"),
                new DxfTypedValue(1040, Numbers.Exact(height)),
                new DxfTypedValue(1070, "72"),
                new DxfTypedValue(1070, "0"),
                new DxfTypedValue(1070, "3"),
                new DxfTypedValue(1000, string.Empty),
                new DxfTypedValue(1070, "90"),
                new DxfTypedValue(1071, "12"),
                new DxfTypedValue(1070, "43"),
                new DxfTypedValue(1040, sixth),
                new DxfTypedValue(1070, "4"),
                new DxfTypedValue(1000, string.Empty),
                new DxfTypedValue(1070, "91"),
                new DxfTypedValue(1071, "12"),
                new DxfTypedValue(1070, "44"),
                new DxfTypedValue(1040, sixth),
                new DxfTypedValue(1000, "OLEEND"),
            };
        }

        /// <summary>The frame from the upper left corner round by the upper right, the first again at the end; none for a frame of no corners.</summary>
        private List<GeoPoint3> Frame()
        {
            GeoPoint3[] corners = Corners(out bool held);
            return held
                ? new List<GeoPoint3> { corners[0], corners[1], corners[2], corners[3], corners[0] }
                : new List<GeoPoint3>();
        }

        /// <summary>The four corners, as <see cref="Corners(out bool)"/> gives them.</summary>
        private GeoPoint3[] Corners() => Corners(out _);

        /// <summary>
        /// The four corners, upper left, upper right, lower right and lower left: those of the head of the binary; where it
        /// holds none, those of the frame along x and y from 10, its upper left, to 11, its lower right; where the record holds
        /// neither, the origin four times, as AutoCAD 2021 gives a frame it could not read, and <paramref name="held"/> false.
        /// The log is told once where 10 and 11 are not the head's upper left and lower right to the point tolerance, or the
        /// head holds none.
        /// </summary>
        private GeoPoint3[] Corners(out bool held)
        {
            // The groups up to the line of the binary that ends the corners: AutoCAD writes 10 and 11 before it.
            int digits = 0;
            List<DxfTypedValue> groups = Item.File.OwnGroupsUpTo(Item, g =>
                g.TypeCode == 310 && (digits += DxfOleData.Digits(g.Value) & ~1) >= 2 * DxfOleData.CornersEnd
            );
            held = true;
            GeoPoint3 upperLeft;
            GeoPoint3 lowerRight;

            if (DxfOleData.TryCorners(DxfOleData.Binary(groups, DxfOleData.CornersEnd), out GeoPoint3[] corners))
            {
                // 10 and 11 are read once, for the log alone: the head's corners are the frame's.
                if (!_told)
                {
                    _told = true;
                    PointOf(groups, 10, out upperLeft);
                    PointOf(groups, 11, out lowerRight);

                    if (
                        !corners[0].IsEqualTo(upperLeft, Tolerance.Default)
                        || !corners[2].IsEqualTo(lowerRight, Tolerance.Default)
                    )
                    {
                        GeometryHelperLog.Debug(
                            "DxfOle2Frame: the OLE2FRAME "
                                + (Handle ?? string.Empty)
                                + " gives the corners "
                                + upperLeft
                                + " and "
                                + lowerRight
                                + " in groups 10 and 11, and "
                                + corners[0]
                                + " and "
                                + corners[2]
                                + " in its binary, which AutoCAD reads: the binary's are taken."
                        );
                    }
                }

                return corners;
            }

            held = PointOf(groups, 10, out upperLeft) | PointOf(groups, 11, out lowerRight);

            if (!_told)
            {
                _told = true;
                GeometryHelperLog.Debug(
                    "DxfOle2Frame: the binary of the OLE2FRAME "
                        + (Handle ?? string.Empty)
                        + " holds no corners: "
                        + (held ? "those of groups 10 and 11 are taken." : "nor do groups 10 and 11, and it has none.")
                );
            }

            return new[]
            {
                upperLeft,
                new GeoPoint3(lowerRight.X, upperLeft.Y, upperLeft.Z),
                lowerRight,
                new GeoPoint3(upperLeft.X, lowerRight.Y, lowerRight.Z),
            };
        }

        /// <summary>
        /// A point of the groups read (10, 20, 30 for 10), each coordinate the first of its code, as <see cref="DxfEntity"/> reads
        /// one: a coordinate left out nought; where the groups hold none of the three, the point of the whole record; false, and
        /// the origin, where that holds none either.
        /// </summary>
        private bool PointOf(List<DxfTypedValue> groups, int code, out GeoPoint3 point)
        {
            var values = new double[3];
            var seen = new bool[3];
            var found = new bool[3];

            foreach (DxfTypedValue group in groups)
            {
                int k = (group.TypeCode - code) / 10;

                if (group.TypeCode >= code && (group.TypeCode - code) % 10 == 0 && k < 3 && !seen[k])
                {
                    seen[k] = true;
                    found[k] = Numbers.TryRead(group.Value, out double value) && Guard.IsFinite(value);
                    values[k] = found[k] ? value : 0.0;
                }
            }

            // AutoCAD writes 10 and 11 before the binary; another writer may write them after it, in the groups not read yet.
            if (!(found[0] || found[1] || found[2]))
            {
                for (int k = 0; k < 3; k++)
                {
                    found[k] = Numbers.TryRead(GetString(code + 10 * k), out double value) && Guard.IsFinite(value);
                    values[k] = found[k] ? value : 0.0;
                }
            }

            bool any = found[0] || found[1] || found[2];
            point = any ? new GeoPoint3(values[0], values[1], values[2]) : Origin;
            return any;
        }

        /// <summary>
        /// A number of the extended data of ACAD between OLEBEGIN and OLEEND, where AutoCAD keeps each as a 1070 of its code and
        /// the group of its value after it; the fallback where the record has none of the code, or no number for it, and where
        /// it is not finite but <paramref name="stored"/> asks for the number as it is stored.
        /// </summary>
        private double OleData(int code, double fallback, bool stored = false)
        {
            IReadOnlyList<DxfTypedValue> data = GetXDataForApplication("ACAD");
            bool inside = false;

            for (int i = 0; data != null && i < data.Count; i++)
            {
                DxfTypedValue group = data[i];

                if (group.TypeCode == 1000)
                {
                    string text = group.Value?.Trim();
                    inside =
                        string.Equals(text, "OLEBEGIN", StringComparison.Ordinal)
                        || (inside && !string.Equals(text, "OLEEND", StringComparison.Ordinal));
                    continue;
                }

                // A code, then its value: the pairs are passed over two at a time.
                if (inside && group.TypeCode == 1070 && i + 1 < data.Count)
                {
                    DxfTypedValue value = data[++i];

                    if (
                        int.TryParse(
                            group.Value?.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int at
                        )
                        && at == code
                    )
                    {
                        // NaN and the infinities are read as .NET writes them where they are asked for as stored.
                        bool read =
                            Numbers.TryRead(value.Value, out double number)
                            || (
                                stored
                                && double.TryParse(
                                    value.Value?.Trim(),
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out number
                                )
                            );
                        return read && (stored || Guard.IsFinite(number)) ? number : fallback;
                    }
                }
            }

            return fallback;
        }

        /// <summary>The link's display name, or its path, where the object is linked; empty otherwise.</summary>
        private string Link(bool name)
        {
            DxfCompoundFile file = DxfOleData.Compound(Binary(), out _);

            if (
                file == null
                || DxfOleData.TypeOf(file) != DxfOle2FrameItemType.Link
                || !DxfOleData.TryLink(file, out string shown, out string path)
            )
            {
                return string.Empty;
            }

            return name ? shown : path;
        }

        #endregion
    }
}
