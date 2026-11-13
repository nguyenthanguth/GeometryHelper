using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <content>
    /// A REGION: made from its shape, as AutoCAD 2021 writes it, read back from the ACIS data it holds, and moved by writing the
    /// data of the body it becomes.
    /// </content>
    public sealed partial class DxfUnknownEntity
    {
        private DxfRegionShape _madeShape;
        private byte[] _pendingSab;
        private string _madeFor;
        private bool _copied;

        /// <summary>
        /// Makes a REGION of a shape, for a drawing: its ACIS data written as AutoCAD 2021 writes it for the drawing's version,
        /// SAT text in the groups of the entity for R2000 to R2010 (version 400 of ACIS for R2000, ASM 208, 212 and 215 for R2004,
        /// R2007 and R2010) and, for R2013 and R2018, SAB in the data store of the file (the section ACDSDATA, which
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> writes the record of), the entity naming its data by a GUID.
        /// </summary>
        /// <param name="shape">The shape: a box 40 by 30 with a hole of radius 5 makes a region of area 1 121.46.</param>
        /// <param name="database">The drawing the region is made for; it is added to a block of that drawing.</param>
        /// <returns>The region, on layer 0 in no drawing, to be added by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape or the drawing is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the drawing is of a version before R2000, whose regions are not written.</exception>
        /// <remarks>
        /// The groups are those of AutoCAD's own: for R2000 to R2010 the marker AcDbModelerGeometry, group 70 of 1 and a group 1
        /// for each line of the SAT, each character but a space written as 0x9F less it and a caret followed by a space; for
        /// R2013 and R2018 group 290 of 1 and group 2 with the GUID. A shape of several faces (the union of two regions far apart)
        /// is one region of several lumps, as AutoCAD keeps it. Appended to a drawing of another version than it was made
        /// for, <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> refuses it.
        /// </remarks>
        public static DxfUnknownEntity CreateRegion(DxfRegionShape shape, DxfDatabase database)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            AcisFormat format = AcisFormat.For(database.Version);

            if (format == null)
            {
                throw new NotSupportedException(
                    "A drawing of "
                        + (database.Version ?? "no version")
                        + " has no regions to write: R2000 (AC1015) brought them."
                );
            }

            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbModelerGeometry") };
            byte[] sab = RegionData(shape.Body, format, groups);
            var region = new DxfUnknownEntity(DxfFile.Detached("REGION", groups));
            region._madeShape = shape;
            region._pendingSab = sab;
            region._madeFor = database.Version;
            return region;
        }

        /// <summary>
        /// The groups of a REGION's ACIS data for a format, after its subclass marker: for the SAT of R2000 to R2010 group 70 and
        /// a group 1 for each line, for the SAB of R2013 and R2018 group 290 and the GUID (2) of its record. The SAB itself is
        /// given back, null for text.
        /// </summary>
        internal static byte[] RegionData(AcisBody body, AcisFormat format, List<DxfTypedValue> groups)
        {
            List<AcisRecord> records = body.Records(format);

            if (format.Binary)
            {
                groups.Add(new DxfTypedValue(290, "1"));
                groups.Add(new DxfTypedValue(2, Guid.NewGuid().ToString("B")));
                return AcisWriter.Sab(records, format, CDate(DateTime.Now));
            }

            groups.Add(new DxfTypedValue(70, "1"));

            foreach (KeyValuePair<int, string> line in SatGroups(AcisWriter.Sat(records, format)))
            {
                groups.Add(new DxfTypedValue(line.Key, line.Value));
            }

            return null;
        }

        /// <summary>The groups of the lines of a SAT: each ciphered, a line past 255 characters in pieces of 255, each but the last under group 3, never ending on a caret.</summary>
        internal static List<KeyValuePair<int, string>> SatGroups(IEnumerable<string> lines)
        {
            var groups = new List<KeyValuePair<int, string>>();

            foreach (string line in lines)
            {
                string cipher = Cipher(line);
                int at = 0;

                while (cipher.Length - at > 255)
                {
                    int piece = cipher[at + 254] == '^' ? 254 : 255;
                    groups.Add(new KeyValuePair<int, string>(3, cipher.Substring(at, piece)));
                    at += piece;
                }

                groups.Add(new KeyValuePair<int, string>(1, cipher.Substring(at)));
            }

            return groups;
        }

        /// <summary>
        /// Gets the shape of a REGION: its loops and its area and perimeter, read from the ACIS data it holds, the SAT text of its
        /// groups or the SAB of the data store of its file (the section ACDSDATA); null for an entity of another type, for a REGION
        /// whose data is not there or not readable, and for one whose body is not flat faces of lines and arcs of ellipses.
        /// </summary>
        /// <returns>The shape, or null.</returns>
        /// <remarks>
        /// A region AutoCAD 2021 wrote in a box 40 by 30 gives one loop of four lines, area 1 200 and perimeter 140; the same with a
        /// circular hole of radius 5 two loops, the circle (a hole) first, area 1 121.46 and perimeter 171.42. A region read
        /// from a file is read again on each call.
        /// </remarks>
        public DxfRegionShape GetRegionShape()
        {
            if (DxfName != "REGION")
            {
                return null;
            }

            if (_madeShape != null && !IsResident)
            {
                return _madeShape;
            }

            AcisBody body = ReadBody();
            return body == null ? null : new DxfRegionShape(body);
        }

        /// <summary>
        /// Gives a REGION another shape in place, its handle, owner, layer and look kept, as AutoCAD's <c>BooleanOperation</c>
        /// leaves the first region of a union: the ACIS data is written anew for the drawing's version, in the SAT groups or in
        /// the record of the data store the region already has.
        /// </summary>
        /// <param name="shape">The shape: a box 40 by 30 united with a circle of radius 10 on its right edge, area 1 357.08.</param>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is not a REGION.</exception>
        /// <exception cref="NotSupportedException">Thrown when the region's drawing is of a version before R2000, whose regions are not written.</exception>
        public void SetRegionShape(DxfRegionShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (DxfName != "REGION")
            {
                throw new InvalidOperationException("Only a REGION has a shape: this is a " + DxfName + ".");
            }

            AcisFormat format = AcisFormat.For(RegionVersion);

            if (format == null)
            {
                throw new NotSupportedException(
                    "A drawing of "
                        + (RegionVersion ?? "no version")
                        + " has no regions to write: R2000 (AC1015) brought them."
                );
            }

            WriteRegion(shape.Body, format, false);
        }

        /// <summary>
        /// The parts of a REGION: the curves of the loops of a face, each a new entity with the region's look, a hole's before its
        /// outer loop's; of a region of several faces, a REGION for each face, as AutoCAD's Explode gives them (a union of two
        /// regions far apart breaks into the two regions).
        /// </summary>
        private IReadOnlyList<DxfEntity> RegionParts()
        {
            DxfRegionShape shape = GetRegionShape();

            if (shape == null)
            {
                throw new NotSupportedException(NotReadable());
            }

            var parts = new List<DxfEntity>();
            DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;

            if (shape.Body.Faces.Count > 1 && database != null && AcisFormat.For(database.Version) != null)
            {
                foreach (AcisFace face in shape.Body.Faces)
                {
                    DxfUnknownEntity one = CreateRegion(new DxfRegionShape(new AcisBody(new[] { face })), database);
                    GiveLook(one);
                    parts.Add(one);
                }

                return parts.AsReadOnly();
            }

            foreach (DxfRegionLoop loop in shape.Loops)
            {
                foreach (DxfCurve curve in loop.NewCurves())
                {
                    GiveLook(curve);
                    parts.Add(curve);
                }
            }

            return parts.AsReadOnly();
        }

        private string NotReadable() =>
            "The ACIS data of the REGION "
            + (Handle ?? string.Empty)
            + " is not flat faces of lines and arcs of ellipses, which are all the library reads: explode it in AutoCAD, whose solid modeler breaks it.";

        /// <summary>The body of the ACIS data of the entity, null where it holds none that is readable.</summary>
        private AcisBody ReadBody()
        {
            IReadOnlyList<DxfTypedValue> groups = GetOwnGroups(0);
            var lines = new List<string>();
            var line = new StringBuilder();
            bool binary = false;

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 2)
                {
                    binary = true;
                }
                else if (group.TypeCode == 3 || group.TypeCode == 1)
                {
                    line.Append(group.Value);

                    if (group.TypeCode == 1)
                    {
                        lines.Add(Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            if (binary)
            {
                byte[] sab = SabOfRegion();
                return sab == null ? null : AcisReader.FromSab(sab);
            }

            return lines.Count == 0 ? null : AcisReader.FromSat(lines);
        }

        /// <summary>The SAB of a region of R2013 or R2018: from the data store of its file, or kept with it while it is in none.</summary>
        private byte[] SabOfRegion() => !IsResident && _pendingSab != null ? _pendingSab : Item.File.AcisData(Handle);

        /// <summary>The version of the drawing the ACIS data of the region is written for: its file's, or the one it was made or copied for.</summary>
        private string RegionVersion => IsResident ? Item.File.Version : _madeFor;

        /// <summary>
        /// Moves a REGION by writing the data of the body it becomes: every edge and plane moved, the loops kept in their order, the
        /// data written as the drawing's version writes it, in place of the old (the SAT groups, or the record of the data store).
        /// </summary>
        private void TransformRegion(GeoTransform3 transform)
        {
            AcisBody body = ReadBody();

            if (body == null)
            {
                throw new NotSupportedException(NotReadable());
            }

            WriteRegion(body.Transformed(transform), AcisFormat.For(RegionVersion), false);
        }

        /// <summary>
        /// Writes the ACIS data of a body in the place of the entity's own, in a format of the drawing it is, or is made for. Where
        /// <paramref name="again"/> is false the data is of the version the entity has already (its GUID stays); where it is true
        /// the format is another, and the marker (70, or 290 and the GUID) is written anew with the data.
        /// </summary>
        private void WriteRegion(AcisBody body, AcisFormat format, bool again)
        {
            var written = new List<DxfTypedValue>();
            byte[] sab = RegionData(body, format, written);
            _madeShape = null;

            if (sab == null && !IsResident)
            {
                _pendingSab = null;
            }

            if (sab != null)
            {
                if (IsResident)
                {
                    if (!Item.File.SetAcisData(Handle, sab))
                    {
                        Item.File.AddAcisData(Handle, sab);
                    }
                }
                else
                {
                    _pendingSab = sab;
                }
            }

            if (sab != null && !again)
            {
                return;
            }

            var groups = new List<KeyValuePair<int, string>>();

            foreach (DxfTypedValue group in written)
            {
                // Where the data is of the form the entity has, only its lines are written anew: the marker and the GUID stay.
                if (again || group.TypeCode == 1 || group.TypeCode == 3)
                {
                    groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            ReplaceGroups(0, again ? new[] { 1, 2, 3, 70, 290 } : new[] { 1, 3 }, groups.ToArray());
        }

        /// <summary>A copy of a REGION keeps the data of the original, in a drawing of the version it came from: a record of its own for R2013 and R2018 (the copy's data is carried with it until it is added), a GUID of its own.</summary>
        private protected override void Copied(DxfEntity original)
        {
            if (DxfName != "REGION")
            {
                return;
            }

            var source = (DxfUnknownEntity)original;
            _copied = true;
            _madeFor = source.RegionVersion;
            byte[] sab = source.SabOfRegion();

            if (sab != null)
            {
                _pendingSab = (byte[])sab.Clone();
                ReplaceGroups(0, new[] { 2 }, new KeyValuePair<int, string>(2, Guid.NewGuid().ToString("B")));
            }
        }

        /// <summary>The SAT of a line as DXF holds it: each character but a space 0x9F less it, a caret followed by a space.</summary>
        internal static string Cipher(string line)
        {
            var text = new StringBuilder(line.Length + 8);

            foreach (char c in line)
            {
                char put = c > ' ' ? (char)(159 - c) : c;
                text.Append(put);

                if (put == '^')
                {
                    text.Append(' ');
                }
            }

            return text.ToString();
        }

        /// <summary>The SAT of a line from the value DXF holds: the caret and its space made one, the cipher undone.</summary>
        internal static string Plain(string value)
        {
            var text = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                text.Append(c > ' ' ? (char)(159 - c) : c);

                if (c == '^' && i + 1 < value.Length && value[i + 1] == ' ')
                {
                    i++;
                }
            }

            return text.ToString();
        }

        /// <summary>A date as C's <c>ctime</c> writes it: <c>Fri Nov 13 08:05:36 2026</c>, the day's number padded by a space.</summary>
        internal static string CDate(DateTime when) =>
            when.ToString("ddd MMM ", CultureInfo.InvariantCulture)
            + when.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)
            + when.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture);

        /// <summary>
        /// Refuses a region made for another version of drawing than the block's; a region copied from a drawing of another
        /// version is given the data of the block's version instead, written from the body the copy holds.
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (_madeFor == null || string.Equals(_madeFor, owner.File.Version, StringComparison.Ordinal))
            {
                return;
            }

            AcisFormat format = _copied ? AcisFormat.For(owner.File.Version) : null;
            AcisBody body = format == null ? null : ReadBody();

            if (body == null)
            {
                throw new ArgumentException(
                    "The region was made for a drawing of "
                        + _madeFor
                        + ", whose ACIS data is not that of "
                        + (owner.File.Version ?? "this drawing")
                        + ": make it for the drawing it is added to."
                );
            }

            WriteRegion(body, format, true);
            _madeFor = owner.File.Version;
        }

        /// <summary>A region of R2013 or R2018 is given its record in the data store of the file, under its handle.</summary>
        internal override void Appended(bool madeInCode)
        {
            if (_pendingSab != null)
            {
                Item.File.AddAcisData(Handle, _pendingSab);
                _pendingSab = null;
            }
        }
    }
}
