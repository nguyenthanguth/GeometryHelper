using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <content>
    /// A REGION: made from its shape, as AutoCAD 2021 writes it, and read back from the ACIS data it holds.
    /// </content>
    public sealed partial class DxfUnknownEntity
    {
        private DxfRegionShape _madeShape;
        private byte[] _pendingSab;
        private string _madeFor;

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
        /// <exception cref="ArgumentException">Thrown when the shape holds more than one face, which is not written.</exception>
        /// <remarks>
        /// The groups are those of AutoCAD's own: for R2000 to R2010 the marker AcDbModelerGeometry, group 70 of 1 and a group 1
        /// for each line of the SAT, each character but a space written as 0x9F less it and a caret followed by a space; for
        /// R2013 and R2018 group 290 of 1 and group 2 with the GUID. Appended to a drawing of another version than it was made
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

            if (shape.Body.Faces.Count != 1)
            {
                throw new ArgumentException("A region of more than one face is not written.", nameof(shape));
            }

            List<AcisRecord> records = shape.Body.Records(format);
            var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbModelerGeometry") };
            byte[] sab = null;

            if (format.Binary)
            {
                sab = AcisWriter.Sab(records, format, CDate(DateTime.Now));
                groups.Add(new DxfTypedValue(290, "1"));
                groups.Add(new DxfTypedValue(2, Guid.NewGuid().ToString("B")));
            }
            else
            {
                groups.Add(new DxfTypedValue(70, "1"));

                foreach (string line in AcisWriter.Sat(records, format))
                {
                    string cipher = Cipher(line);

                    // A line past 255 characters goes in pieces of 255, each but the last under group 3, never ending on a caret.
                    int at = 0;

                    while (cipher.Length - at > 255)
                    {
                        int piece = cipher[at + 254] == '^' ? 254 : 255;
                        groups.Add(new DxfTypedValue(3, cipher.Substring(at, piece)));
                        at += piece;
                    }

                    groups.Add(new DxfTypedValue(1, cipher.Substring(at)));
                }
            }

            var region = new DxfUnknownEntity(DxfFile.Detached("REGION", groups));
            region._madeShape = shape;
            region._pendingSab = sab;
            region._madeFor = database.Version;
            return region;
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

        /// <summary>The curves of the loops of a REGION, each a new entity with the region's look, a hole's before its outer loop's.</summary>
        private IReadOnlyList<DxfEntity> RegionParts()
        {
            DxfRegionShape shape = GetRegionShape();

            if (shape == null)
            {
                throw new NotSupportedException(
                    "The ACIS data of the REGION "
                        + (Handle ?? string.Empty)
                        + " is not flat faces of lines and arcs of ellipses, which are all the library reads: explode it in AutoCAD, whose solid modeler breaks it."
                );
            }

            var parts = new List<DxfEntity>();

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
                byte[] sab = Item.File.AcisData(Handle);
                return sab == null ? null : AcisReader.FromSab(sab);
            }

            return lines.Count == 0 ? null : AcisReader.FromSat(lines);
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
        private static string CDate(DateTime when) =>
            when.ToString("ddd MMM ", CultureInfo.InvariantCulture)
            + when.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)
            + when.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture);

        /// <summary>Refuses a region made for another version of drawing than the block's.</summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (_madeFor != null && !string.Equals(_madeFor, owner.File.Version, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The region was made for a drawing of "
                        + _madeFor
                        + ", whose ACIS data is not that of "
                        + (owner.File.Version ?? "this drawing")
                        + ": make it for the drawing it is added to."
                );
            }
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
