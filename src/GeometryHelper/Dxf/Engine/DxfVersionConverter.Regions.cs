using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region Regions

        /// <summary>
        /// Gives each REGION the ACIS data of the target version, where that is another form than its own: the body is read
        /// from the SAT of the record or the SAB of the section ACDSDATA, and written again as the target writes it (SAT of the
        /// ACIS or ASM of R2000, R2004, R2007 or R2010, SAB of R2013 or R2018), so that a region goes from the text of R2010 to
        /// the data store of R2013, and back, and keeps the form AutoCAD 2021 gives it in each version. A region whose body is
        /// not flat faces of lines and arcs of ellipses (the library reads no other) is left as it is: its text kept going
        /// down to an older text, its binary dropped going down below R2013, as for any other body.
        /// </summary>
        private void Regions()
        {
            AcisFormat from = AcisFormat.For(DxfVersions.Name(_source));
            AcisFormat to = AcisFormat.For(DxfVersions.Name(_target));

            if (from == null || to == null || (from.Version == to.Version && from.Binary == to.Binary))
            {
                return;
            }

            Dictionary<string, Record> store = Store();
            var written = new List<KeyValuePair<string, byte[]>>();

            foreach (string name in new[] { "BLOCKS", "ENTITIES" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (record.Dropped || record.Type != "REGION")
                    {
                        continue;
                    }

                    string handle = HandleOf(record);
                    int at = record.Groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbModelerGeometry");

                    if (at < 0)
                    {
                        continue;
                    }

                    int end = at + 1;

                    while (
                        end < record.Groups.Count
                        && record.Groups[end].Code != 100
                        && record.Groups[end].Code != 102
                        && record.Groups[end].Code < 1000
                    )
                    {
                        end++;
                    }

                    AcisBody body = ReadRegion(record, at + 1, end, handle, store, out string guid);

                    if (body == null)
                    {
                        continue;
                    }

                    var fresh = new List<DxfTypedValue>();
                    byte[] sab = DxfUnknownEntity.RegionData(body, to, fresh);
                    var groups = new List<Group>();

                    foreach (DxfTypedValue value in fresh)
                    {
                        groups.Add(
                            value.TypeCode == 70 || value.TypeCode == 290
                                ? Whole(value.TypeCode, 1)
                                : new Group(value.TypeCode, value.TypeCode == 2 && guid != null ? guid : value.Value)
                        );
                    }

                    record.Groups.RemoveRange(at + 1, end - at - 1);
                    record.Groups.InsertRange(at + 1, groups);

                    if (sab != null)
                    {
                        written.Add(new KeyValuePair<string, byte[]>(handle, sab));
                    }
                }
            }

            if (written.Count > 0)
            {
                Keep(written);
            }
        }

        /// <summary>The records of the source's section ACDSDATA that hold ACIS data, by the handle of the entity they name (group 320).</summary>
        private Dictionary<string, Record> Store()
        {
            var store = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
            Section data = Find("ACDSDATA");

            if (data == null)
            {
                return store;
            }

            foreach (Record record in data.Records)
            {
                if (record.Groups.Count > 0 && record.Type == "ACDSRECORD")
                {
                    foreach (Group group in record.Groups)
                    {
                        if (group.Code == 320 && !store.ContainsKey(group.Value.Trim()))
                        {
                            store.Add(group.Value.Trim(), record);
                        }
                    }
                }
            }

            return store;
        }

        /// <summary>
        /// The body of a REGION's ACIS data, groups from <paramref name="from"/> to <paramref name="end"/>: its SAT lines, or the
        /// SAB of the record the data store holds for the handle. The GUID the groups name (2) is given back.
        /// </summary>
        private static AcisBody ReadRegion(
            Record record,
            int from,
            int end,
            string handle,
            Dictionary<string, Record> store,
            out string guid
        )
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            bool binary = false;
            guid = null;

            for (int i = from; i < end; i++)
            {
                Group group = record.Groups[i];

                if (group.Code == 2)
                {
                    binary = true;
                    guid = group.Value;
                }
                else if (group.Code == 1 || group.Code == 3)
                {
                    line.Append(group.Value);

                    if (group.Code == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            if (!binary)
            {
                return lines.Count == 0 ? null : AcisReader.FromSat(lines);
            }

            if (handle == null || !store.TryGetValue(handle, out Record data))
            {
                return null;
            }

            var hex = new StringBuilder();
            int size = -1;

            foreach (Group group in data.Groups)
            {
                if (group.Code == 94)
                {
                    int.TryParse(group.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                }
                else if (group.Code == 310)
                {
                    hex.Append(group.Value.Trim());
                }
            }

            if (size <= 0 || hex.Length < 2 * size)
            {
                return null;
            }

            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = byte.Parse(hex.ToString(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return AcisReader.FromSab(bytes);
        }

        /// <summary>
        /// Puts the SAB of regions into the data store of the target (R2013 or R2018): the record the store holds for a handle
        /// given the new bytes, one the store has not added; a store made, with the schemas AutoCAD writes for ACIS data, where
        /// the file had none.
        /// </summary>
        private void Keep(List<KeyValuePair<string, byte[]>> written)
        {
            if (_target < 1027)
            {
                return;
            }

            Section data = Find("ACDSDATA");

            if (data == null)
            {
                data = new Section("ACDSDATA");
                var head = new Record();
                head.Groups.Add(Whole(70, 2));
                head.Groups.Add(Whole(71, 8));
                data.Records.Add(head);

                foreach (string schema in GeometryHelper.Dwg.DwgTranscoder.AcisSchemas)
                {
                    string[] parts = schema.Split('|');
                    var made = new Record();

                    for (int i = 0; i + 1 < parts.Length; i += 2)
                    {
                        int code = int.Parse(parts[i], CultureInfo.InvariantCulture);
                        string value = parts[i + 1];
                        made.Groups.Add(
                            code >= 90 && code <= 99 || code == 280 || code == 291 || code == 282 || code == 284
                                ? Whole(code, int.Parse(value, CultureInfo.InvariantCulture))
                                : new Group(code, value)
                        );
                    }

                    data.Records.Add(made);
                }

                _sections.Add(data);
            }

            foreach (KeyValuePair<string, byte[]> one in written)
            {
                var record = new Record();
                record.Groups.Add(new Group(0, "ACDSRECORD"));
                record.Groups.Add(Whole(90, 0));
                record.Groups.Add(new Group(2, "AcDbDs::ID"));
                record.Groups.Add(Whole(280, 10));
                record.Groups.Add(new Group(320, one.Key));
                record.Groups.Add(new Group(2, "ASM_Data"));
                record.Groups.Add(Whole(280, 15));
                record.Groups.Add(Whole(94, one.Value.Length));

                for (int at = 0; at < one.Value.Length; at += 127)
                {
                    var hex = new StringBuilder(254);

                    for (int i = at; i < Math.Min(at + 127, one.Value.Length); i++)
                    {
                        hex.Append(one.Value[i].ToString("X2", CultureInfo.InvariantCulture));
                    }

                    record.Groups.Add(new Group(310, hex.ToString()));
                }

                int old = data.Records.FindIndex(r =>
                    r.Groups.Count > 0
                    && r.Type == "ACDSRECORD"
                    && r.Groups.Exists(g =>
                        g.Code == 320 && string.Equals(g.Value.Trim(), one.Key, StringComparison.OrdinalIgnoreCase)
                    )
                );

                if (old >= 0)
                {
                    data.Records[old] = record;
                }
                else
                {
                    data.Records.Add(record);
                }
            }
        }

        #endregion
    }
}
