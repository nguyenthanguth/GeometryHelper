using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An OLE2FRAME of a DXF file, as AutoCAD's <c>Ole2Frame</c>: an OLE object shown in a frame, a picture pasted, a sheet of
    /// Excel embedded or a document linked. The library reads where it stands and what it is, and gives the picture of a
    /// static one; it does not move it yet, nor make one.
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
    /// they are; read and saved, its record is written byte for byte. It is not moved yet: <see cref="DxfEntity.TransformBy"/>
    /// throws <see cref="NotSupportedException"/>, as AutoCAD reads the frame from the corners of the binary, which the library
    /// does not rewrite, and an INSERT exploded leaves it out. AutoCAD's <c>OleObject</c>, the object of the running
    /// application, and <c>Position2d</c>, the frame in pixels of the window it is shown in, are not given: a file holds
    /// neither. The OLEFRAME of AutoCAD R13, an object of OLE 1, has no class of AutoCAD's .NET API, and stays a
    /// <see cref="DxfUnknownEntity"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfOle2Frame : DxfEntity
    {
        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        // Whether 10 and 11 have been checked against the head's corners for the log, or the log told the head holds none.
        private bool _told;

        private DxfOle2Frame(DxfItem item)
            : base(item)
        {
        }

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
        public DxfRectangle3d Position3d
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return new DxfRectangle3d(corners[0], corners[1], corners[3], corners[2]);
            }
        }

        /// <summary>
        /// Gets the width of the frame, as AutoCAD 2021's <c>WcsWidth</c> works it out: the x of the upper right corner less that
        /// of the upper left, its run along x and not the length of its top, so that the frame 30 wide turned 30 degrees about z
        /// is 25.98 wide; 30 for the picture 30 by 20.
        /// </summary>
        public double WcsWidth
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return corners[1].X - corners[0].X;
            }
        }

        /// <summary>
        /// Gets the height of the frame, as AutoCAD 2021's <c>WcsHeight</c> works it out: the y of the upper left corner less
        /// that of the lower left, its run along y; 20 for the picture 30 by 20.
        /// </summary>
        public double WcsHeight
        {
            get
            {
                GeoPoint3[] corners = Corners();
                return corners[0].Y - corners[3].Y;
            }
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
        /// Gets the width of the frame in percent of the width it had when the drawing was read, as AutoCAD 2021's
        /// <c>ScaleWidth</c> gives it: 100 for every frame read, the library not changing its size yet; 0 for a frame of no
        /// width. It is not of the extended data's size (codes 41 and 42) nor of the natural size of the object, which AutoCAD
        /// 2021 does not read for it.
        /// </summary>
        public double ScaleWidth => WcsWidth == 0.0 ? 0.0 : 100.0;

        /// <summary>
        /// Gets the height of the frame in percent of the height it had when the drawing was read, as AutoCAD 2021's
        /// <c>ScaleHeight</c> gives it: 100 for every frame read; 0 for a frame of no height.
        /// </summary>
        public double ScaleHeight => WcsHeight == 0.0 ? 0.0 : 100.0;

        #endregion

        #region How it is shown

        /// <summary>
        /// Gets whether a change of the width or height of the frame changes the other with it, as AutoCAD 2021's
        /// <c>LockAspect</c> reads it from the extended data (code 71 between OLEBEGIN and OLEEND, 1 for locked); true where the
        /// record has none, as AutoCAD gives a frame read with no extended data.
        /// </summary>
        public bool LockAspect => OleData(71, 1.0) != 0.0;

        /// <summary>
        /// Gets how the object is plotted, as AutoCAD 2021's <c>OutputQuality</c> reads it from group 73: 0 monochrome, 1 low
        /// graphics, 2 high graphics, as AutoCAD writes a picture pasted; 1 where the record has none, as AutoCAD gives a new
        /// frame.
        /// </summary>
        public byte OutputQuality => (byte)Math.Max(0, Math.Min(255, GetInt(73, 1)));

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
                GeometryHelperLog.Debug("DxfOle2Frame: the OLE2FRAME " + (Handle ?? string.Empty) + " gives no picture: " + why + ".");
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
                    throw new InvalidOperationException(TypeAndHandle + " holds no corners, in its binary or in groups 10 and 11, to give it extents.");
                }

                double[] box = { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };

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
        /// Refuses to move the frame: the library does not rewrite the corners of its binary yet, and AutoCAD 2021 reads the
        /// frame from them, not from groups 10 and 11.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
            => throw new NotSupportedException("An OLE2FRAME is not moved by the library yet: AutoCAD reads its frame from the corners in its binary, which the library does not rewrite.");

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

            return kept.Count < 2 ? Array.AsReadOnly(new object[0]) : Array.AsReadOnly(new object[] { new GeoPolyline3(kept.ToArray(), true) });
        }

        [DxfType("OLE2FRAME")]
        internal static DxfEntity Create(DxfItem item) => new DxfOle2Frame(item);

        #endregion

        #region What it is made of

        /// <summary>The bytes of the binary (310), all of them.</summary>
        private byte[] Binary() => DxfOleData.Binary(GetOwnGroups(0), -1);

        /// <summary>The frame from the upper left corner round by the upper right, the first again at the end; none for a frame of no corners.</summary>
        private List<GeoPoint3> Frame()
        {
            GeoPoint3[] corners = Corners(out bool held);
            return held ? new List<GeoPoint3> { corners[0], corners[1], corners[2], corners[3], corners[0] } : new List<GeoPoint3>();
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
            List<DxfTypedValue> groups = Item.File.OwnGroupsUpTo(Item, g => g.TypeCode == 310 && (digits += DxfOleData.Digits(g.Value) & ~1) >= 2 * DxfOleData.CornersEnd);
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

                    if (!corners[0].IsEqualTo(upperLeft, Tolerance.Default) || !corners[2].IsEqualTo(lowerRight, Tolerance.Default))
                    {
                        GeometryHelperLog.Debug("DxfOle2Frame: the OLE2FRAME " + (Handle ?? string.Empty) + " gives the corners " + upperLeft + " and " + lowerRight + " in groups 10 and 11, and " + corners[0] + " and " + corners[2] + " in its binary, which AutoCAD reads: the binary's are taken.");
                    }
                }

                return corners;
            }

            held = PointOf(groups, 10, out upperLeft) | PointOf(groups, 11, out lowerRight);

            if (!_told)
            {
                _told = true;
                GeometryHelperLog.Debug("DxfOle2Frame: the binary of the OLE2FRAME " + (Handle ?? string.Empty) + " holds no corners: " + (held ? "those of groups 10 and 11 are taken." : "nor do groups 10 and 11, and it has none."));
            }

            return new[] { upperLeft, new GeoPoint3(lowerRight.X, upperLeft.Y, upperLeft.Z), lowerRight, new GeoPoint3(upperLeft.X, lowerRight.Y, lowerRight.Z) };
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
                    inside = string.Equals(text, "OLEBEGIN", StringComparison.Ordinal) || (inside && !string.Equals(text, "OLEEND", StringComparison.Ordinal));
                    continue;
                }

                // A code, then its value: the pairs are passed over two at a time.
                if (inside && group.TypeCode == 1070 && i + 1 < data.Count)
                {
                    DxfTypedValue value = data[++i];

                    if (int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int at) && at == code)
                    {
                        // NaN and the infinities are read as .NET writes them where they are asked for as stored.
                        bool read = Numbers.TryRead(value.Value, out double number) || (stored && double.TryParse(value.Value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number));
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

            if (file == null || DxfOleData.TypeOf(file) != DxfOle2FrameItemType.Link || !DxfOleData.TryLink(file, out string shown, out string path))
            {
                return string.Empty;
            }

            return name ? shown : path;
        }

        #endregion
    }
}
