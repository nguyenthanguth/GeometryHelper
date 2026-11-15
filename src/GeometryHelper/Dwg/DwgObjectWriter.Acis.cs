using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// REGION, 3DSOLID and BODY. From R2013 the entity carries the GUID of its ACIS data, which the data store of the file holds
    /// (<see cref="DwgDataStoreWriter"/>); in R2004, R2007 and R2010 the entity holds the SAB itself, and in R2000 the SAT text.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        private Dictionary<ulong, byte[]> _acis;

        /// <summary>
        /// Whether an ACIS entity is written. From R2013 it names the GUID of its data (group 2) and the section ACDSDATA holds
        /// that data under its handle. In R2004, R2007 and R2010 it is written where <see cref="DwgDrawing.AcisData"/> holds
        /// the SAB of its version (20800, 21200 or 21500, as <see cref="DwgDrawing.SatBodiesToSab"/> makes it of the SAT a DXF
        /// of those versions gives). In R2000 it is written where its SAT is the text of ACIS 4.00 (<see cref="SatTextOf"/>).
        /// </summary>
        private bool HasAcisData(DwgRecord record)
        {
            if (!R2004Plus)
            {
                return SatTextOf(record) != null;
            }

            if (_acis == null)
            {
                _acis = new Dictionary<ulong, byte[]>();

                foreach (KeyValuePair<ulong, byte[]> data in _drawing.AcisData)
                {
                    _acis[data.Key] = data.Value;
                }
            }

            if (!_acis.TryGetValue(record.Handle, out byte[] sab))
            {
                return false;
            }

            return R2013Plus
                ? Guid.TryParse(record.Of("AcDbModelerGeometry").Text(2).Trim(), out Guid _)
                : sab.Length > 19 && BitConverter.ToInt32(sab, 15) == SabVersion;
        }

        /// <summary>The version of the SAB a file of R2004 (20800), R2007 (21200) or R2010 (21500) holds.</summary>
        private int SabVersion =>
            _version >= 1024 ? 21500
            : _version >= 1021 ? 21200
            : 20800;

        /// <summary>
        /// A REGION, 3DSOLID or BODY of R2013 and later (<paramref name="solid"/> for a 3DSOLID): a wireframe of no wires
        /// nor silhouettes, as AutoCAD writes it, no materials of faces, whether it is modified (290), the GUID of its data
        /// in the store, and of a 3DSOLID the handle of its history.
        /// </summary>
        private void WriteModelerGeometry(bool solid)
        {
            if (!R2013Plus)
            {
                WriteModelerGeometryInFile(solid);
                return;
            }

            DwgGroups geometry = _record.Of("AcDbModelerGeometry");
            byte[] guid = Guid.Parse(geometry.Text(2).Trim()).ToByteArray();
            // The wireframe as AutoCAD writes it for a new solid: there, with no point, no wires and no silhouettes.
            WriteEmptyWireframe();
            _data.BitLong(0);
            _data.Bit(geometry.Int(290) != 0);
            _data.BitLong(BitConverter.ToInt32(guid, 0));
            _data.BitShort(BitConverter.ToInt16(guid, 4));
            _data.BitShort(BitConverter.ToInt16(guid, 6));
            _data.Bytes(guid, 8, 8);
            _data.BitLong(0);

            if (solid)
            {
                H(3, _record.Of("AcDb3dSolid").Handle(350));
            }
        }

        /// <summary>
        /// A REGION, 3DSOLID or BODY of R2000 to R2010 (<paramref name="solid"/> for a 3DSOLID): the SAB in the entity
        /// as its bytes, after the flags that say it is there (not empty, and version 2); in R2000 the SAT text of ACIS 4.00 in blocks
        /// (version 1) in its place, with no count of materials and no history; then the wireframe AutoCAD writes
        /// for a new solid, and from R2007 the count of materials of faces, 0, and of a 3DSOLID the handle of its history.
        /// </summary>
        private void WriteModelerGeometryInFile(bool solid)
        {
            _data.Bit(false);

            if (!R2004Plus)
            {
                // AutoCAD 2000 sets the bit after the empty one, which later versions leave at 0.
                _data.Bit(true);
                WriteSatText(SatTextOf(_record));
                WriteEmptyWireframe();
                return;
            }

            _data.Bit(false);
            _data.BitShort(2);
            _data.Bytes(_acis[_record.Handle]);
            WriteEmptyWireframe();

            if (R2007Plus)
            {
                _data.BitLong(0);
            }

            if (solid && R2007Plus)
            {
                H(3, _record.Of("AcDb3dSolid").Handle(350));
            }
        }

        /// <summary>The largest block of the SAT text AutoCAD 2000 puts into an entity.</summary>
        private const int SatBlock = 4096;

        /// <summary>
        /// The SAT text of an ACIS entity as the file of R2000 keeps it: each line as the groups of its DXF hold it (each
        /// character but a space as 0x9F less it, a line past 255 bytes in pieces) followed by a carriage return and a line
        /// feed, and a line that starts with a space starting with a tab. Null where the entity names its data by a GUID, holds
        /// no line, or is not ACIS 4.00 (the head <c>400 0 1 0</c>, as AutoCAD 2000 writes it), which no other form can stand in for.
        /// </summary>
        internal static byte[] SatTextOf(DwgRecord record)
        {
            var text = new List<byte>();
            var line = new System.Text.StringBuilder();
            int lines = 0;

            foreach (KeyValuePair<int, string> group in record.Of("AcDbModelerGeometry").All)
            {
                if (group.Key == 2)
                {
                    return null;
                }

                if (group.Key != 1 && group.Key != 3)
                {
                    continue;
                }

                line.Append(group.Value);

                if (group.Key == 1)
                {
                    string held = line.ToString();

                    if (lines == 0 && !DxfUnknownEntity.Plain(held).StartsWith("400 ", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // DXF writes a caret followed by a space; the file has the caret alone.
                    var bytes = new System.Text.StringBuilder(held.Length);

                    for (int i = 0; i < held.Length; i++)
                    {
                        bytes.Append(held[i]);

                        if (held[i] == '^' && i + 1 < held.Length && held[i + 1] == ' ')
                        {
                            i++;
                        }
                    }

                    if (bytes.Length > 0 && bytes[0] == ' ')
                    {
                        bytes[0] = '\t';
                    }

                    text.AddRange(System.Text.Encoding.UTF8.GetBytes(bytes.ToString()));
                    text.Add(13);
                    text.Add(10);
                    lines++;
                    line.Clear();
                }
            }

            return lines == 0 ? null : text.ToArray();
        }

        /// <summary>
        /// The SAT text in blocks of at most <see cref="SatBlock"/> bytes, each with its size ahead of it and a 0 after the
        /// last, as AutoCAD 2000 lays it out (4 096 bytes, 4 096, then the rest).
        /// </summary>
        private void WriteSatText(byte[] text)
        {
            _data.BitShort(1);

            for (int at = 0; at < text.Length; at += SatBlock)
            {
                int size = Math.Min(SatBlock, text.Length - at);
                _data.BitLong(size);
                _data.Bytes(text, at, size);
            }

            _data.BitLong(0);
        }

        /// <summary>The wireframe of a new solid: there, with no point, no wires and no silhouettes.</summary>
        private void WriteEmptyWireframe()
        {
            _data.Bit(true);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(true);
        }
    }
}
