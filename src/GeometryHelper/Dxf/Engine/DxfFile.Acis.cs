using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dwg;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        private Dictionary<ulong, byte[]> _acisData;
        private int _acisDataAt = -1;
        private List<Piece> _acdsHome;
        private Piece _acdsEnd;

        #region The data store of ACIS data (the section ACDSDATA, from R2013)

        /// <summary>
        /// The SAB of the entity of a handle, as the section ACDSDATA holds it: the bytes of the ACDSRECORD that names the
        /// handle (group 320), its lines of hex (310) joined; null where the file has no record for it. Read once for every
        /// handle, and again after the file has been changed.
        /// </summary>
        internal byte[] AcisData(string handle)
        {
            if (handle == null || !TryHex(handle.Trim(), out ulong key) || key == 0)
            {
                return null;
            }

            if (_acisData == null || _acisDataAt != _editCount)
            {
                _acisData = ReadAcisData();
                _acisDataAt = _editCount;
            }

            return _acisData.TryGetValue(key, out byte[] bytes) ? bytes : null;
        }

        private Dictionary<ulong, byte[]> ReadAcisData()
        {
            var data = new Dictionary<ulong, byte[]>();
            var records = new List<Rec>();

            foreach (Section section in _sections)
            {
                if (section.Name == "ACDSDATA")
                {
                    records.AddRange(Current(section.First, section.Last + 1));
                }
            }

            // A section made for the first record put in a file, which the file as read does not list.
            if (_acdsHome != null)
            {
                foreach (Piece piece in _acdsHome)
                {
                    records.AddRange(piece.Records);
                }
            }

            foreach (Rec record in records)
            {
                if (record.Type != "ACDSRECORD")
                {
                    continue;
                }

                ulong handle = 0;
                int size = -1;
                var hex = new StringBuilder();
                EachGroup(record, (code, start, end) =>
                {
                    string text = Text(record, start, end).Trim();

                    if (code == 320)
                    {
                        TryHex(text, out handle);
                    }
                    else if (code == 94)
                    {
                        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                    }
                    else if (code == 310)
                    {
                        hex.Append(text);
                    }
                });

                if (handle != 0 && size > 0 && hex.Length >= 2 * size && !data.ContainsKey(handle))
                {
                    byte[] bytes = DwgObjectWriter.Unhex(hex.ToString());
                    Array.Resize(ref bytes, size);
                    data.Add(handle, bytes);
                }
            }

            return data;
        }

        /// <summary>
        /// Puts the SAB of an entity into the section ACDSDATA, as AutoCAD writes it: a record of the handle (320) and the bytes
        /// in lines of 127 (94 and 310), before the section's end. A file with no section is given one, before its EOF, with the
        /// schemas AutoCAD writes for ACIS data.
        /// </summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="sab">The SAB.</param>
        internal void AddAcisData(string handle, byte[] sab)
        {
            var record = new Builder(this);
            record.Group(0, "ACDSRECORD");
            record.Group(90, "0");
            record.Group(2, "AcDbDs::ID");
            record.Group(280, "10");
            record.Group(320, handle);
            record.Group(2, "ASM_Data");
            record.Group(280, "15");
            record.Group(94, sab.Length.ToString(CultureInfo.InvariantCulture));
            var hex = new StringBuilder(254);

            for (int at = 0; at < sab.Length; at += 127)
            {
                hex.Clear();

                for (int i = at; i < Math.Min(at + 127, sab.Length); i++)
                {
                    hex.Append(sab[i].ToString("X2", CultureInfo.InvariantCulture));
                }

                record.Group(310, hex.ToString());
            }

            var piece = new Piece(new List<Rec> { record.Done("ACDSRECORD") });

            if (_acdsHome != null)
            {
                _acdsHome.Insert(_acdsHome.IndexOf(_acdsEnd), piece);
                _editCount++;
                return;
            }

            foreach (Section section in _sections)
            {
                if (section.Name == "ACDSDATA")
                {
                    PutBefore(section.Last, piece);
                    return;
                }
            }

            // No section: SECTION, the schemas, the record and ENDSEC go before the EOF, the last record of the file.
            var records = new List<Rec>();
            var open = new Builder(this);
            open.Group(0, "SECTION");
            open.Group(2, "ACDSDATA");
            open.Group(70, "2");
            open.Group(71, "8");
            records.Add(open.Done("SECTION"));

            foreach (string schema in DwgTranscoder.AcisSchemas)
            {
                string[] parts = schema.Split('|');
                var b = new Builder(this);

                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    b.Group(int.Parse(parts[i], CultureInfo.InvariantCulture), parts[i + 1]);
                }

                records.Add(b.Done(parts[1]));
            }

            var head = new Piece(records);
            var close = new Builder(this);
            close.Group(0, "ENDSEC");
            _acdsEnd = new Piece(new List<Rec> { close.Done("ENDSEC") });
            int eof = _starts.Length - 1;
            _acdsHome = PutBefore(eof, head);
            _acdsHome.Insert(_acdsHome.Count - 1, piece);
            _acdsHome.Insert(_acdsHome.Count - 1, _acdsEnd);
            _editCount++;
        }

        #endregion
    }
}
