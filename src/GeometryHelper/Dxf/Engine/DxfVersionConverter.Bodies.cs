using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dwg;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region Solids

        /// <summary>
        /// Gives the ACIS data of each body (a 3DSOLID, a BODY, a surface, and a region the library does not read) the form of
        /// the target version, by turning the records of one form into the other and not by building the geometry again: the SAB
        /// of the data store (R2013 and later) into the SAT of the entity (R2000 to R2010), the SAT into the SAB, and the SAT of
        /// one version of the modeler into that of another. A solid of 12 faces comes out as AutoCAD 2021 writes it in the
        /// version, record for record: the head's version and flags, a vertex's number from R2007, a coedge's in the binary
        /// form; R2000 holds ACIS 4.00, which has no asmheader, no history and no edge parameters (going up they are worked out
        /// from the vertices and the curve). A body whose records the grammars do not place (a tolerant edge, the splines of
        /// R2000) is left as it is going to R2010 and before, its binary left out there (<c>Plan</c>), and left out going to
        /// R2013 and later, which reject a file whose entity holds text.
        /// </summary>
        private void Solids()
        {
            AcisFormat to = AcisFormat.For(DxfVersions.Name(_target));

            if (to == null)
            {
                return;
            }

            AcisFormat text = to.Binary ? null : to;
            Dictionary<string, Record> store = null;
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
                    if (record.Dropped || record.Rebuilt || !Bodies.Contains(record.Type))
                    {
                        continue;
                    }

                    store = store ?? Store();
                    Body(record, to, text, store, written);
                }
            }

            if (written.Count > 0)
            {
                Keep(written);
            }
        }

        private void Body(
            Record record,
            AcisFormat to,
            AcisFormat text,
            Dictionary<string, Record> store,
            List<KeyValuePair<string, byte[]>> written
        )
        {
            int at = record.Groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbModelerGeometry");

            if (at < 0)
            {
                return;
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

            string handle = HandleOf(record);
            var lines = new List<string>();
            var line = new StringBuilder();
            string guid = null;

            for (int i = at + 1; i < end; i++)
            {
                Group group = record.Groups[i];

                if (group.Code == 2)
                {
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

            byte[] sab = null;

            if (guid != null)
            {
                // The data is in the data store of the file: R2013 and R2018 keep it as it is.
                if (
                    to.Binary
                    || handle == null
                    || !store.TryGetValue(handle, out Record data)
                    || StoredSab(data) == null
                )
                {
                    return;
                }

                sab = StoredSab(data);
            }
            else if (lines.Count == 0)
            {
                return;
            }

            List<string> made = null;
            byte[] bytes = null;

            if (to.Binary)
            {
                // A SAT cannot stay: a file of R2013 or later whose entity holds text is read as no drawing.
                string date = DxfUnknownEntity.CDate(DateTime.Now);
                bytes = handle == null ? null : DwgSat.ToSab(lines, to, date);

                if (bytes == null)
                {
                    Drop(record, _left);
                    return;
                }
            }
            else
            {
                string sat = null;

                if (sab != null)
                {
                    // A body ACIS 4.00 cannot hold (a swept spline surface) goes to R2000 as the text of R2010, which AutoCAD reads there.
                    sat = DwgSat.ToSat(sab, text)
                        ?? (_target == 1015 ? DwgSat.ToSat(sab, AcisFormat.For("AC1024")) : null);
                }
                else if (!IsVersionOf(lines, text.Version))
                {
                    sat = DwgSat.ToSat(lines, text);
                }

                if (sat != null)
                {
                    made = new List<string>(sat.Split('\n'));
                    made.RemoveAt(made.Count - 1);
                }
            }

            var groups = new List<Group>();

            if (bytes != null)
            {
                guid = GuidOf(record, end) ?? Guid.NewGuid().ToString("B");
                groups.Add(Whole(290, 1));
                groups.Add(new Group(2, guid));
                written.Add(new KeyValuePair<string, byte[]>(handle, bytes));
                ForgetGuid(record, end);
            }
            else if (made != null)
            {
                groups.Add(Whole(70, 1));

                foreach (KeyValuePair<int, string> piece in DxfUnknownEntity.SatGroups(made))
                {
                    groups.Add(new Group(piece.Key, piece.Value));
                }
            }

            if (groups.Count > 0)
            {
                end = record.Groups.FindIndex(at + 1, g => g.Code == 100 || g.Code == 102 || g.Code >= 1000);
                end = end < 0 ? record.Groups.Count : end;
                record.Groups.RemoveRange(at + 1, end - at - 1);
                record.Groups.InsertRange(at + 1, groups);
                record.Rebuilt = true;
            }

            // From R2007 a solid carries its history (AcDb3dSolid, 350 of nought for none); R2004 and R2000 have no such subclass.
            if (
                record.Type == "3DSOLID"
                && _target >= 1021
                && !record.Groups.Exists(g => g.Code == 100 && g.Value == "AcDb3dSolid")
            )
            {
                int next = record.Groups.FindIndex(at + 1, g => g.Code == 100 || g.Code == 102 || g.Code >= 1000);
                record.Groups.InsertRange(
                    next < 0 ? record.Groups.Count : next,
                    new[] { new Group(100, "AcDb3dSolid"), new Group(350, "0") }
                );
            }
        }

        /// <summary>Whether the first line of SAT data names the version of the modeler: its first whole number.</summary>
        private static bool IsVersionOf(List<string> lines, int version)
        {
            string head = lines.Count > 0 ? lines[0].TrimStart() : string.Empty;
            int space = head.IndexOf(' ');

            return space > 0
                && int.TryParse(
                    head.Substring(0, space),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int found
                )
                && found == version;
        }

        /// <summary>
        /// The identity AutoCAD keeps of a body in its extended data of R2010 and before (ACAD_OBJECT_CHANGE_GUID: a 1070 of 1, the
        /// first part of the GUID as a 1071, two 1070s, then eight bytes as 1070s), the same GUID the entity of R2013 names its data
        /// by (2); null where the record has none.
        /// </summary>
        private static string GuidOf(Record record, int from)
        {
            int at = record.Groups.FindIndex(from, g => g.Code == 1001 && g.Value == "ACAD_OBJECT_CHANGE_GUID");

            if (at < 0 || at + 12 >= record.Groups.Count)
            {
                return null;
            }

            var parts = new long[11];

            for (int i = 0; i < 11; i++)
            {
                Group group = record.Groups[at + 2 + i];

                if (
                    (group.Code != 1070 && group.Code != 1071)
                    || !long.TryParse(
                        group.Value.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parts[i]
                    )
                )
                {
                    return null;
                }
            }

            var tail = new byte[8];

            for (int i = 0; i < 8; i++)
            {
                tail[i] = (byte)parts[3 + i];
            }

            return new Guid((int)parts[0], (short)parts[1], (short)parts[2], tail).ToString("B");
        }

        /// <summary>Takes the extended data of the identity of a body out of a record: the SAB of R2013 names it in the data store instead.</summary>
        private static void ForgetGuid(Record record, int from)
        {
            int at = record.Groups.FindIndex(from, g => g.Code == 1001 && g.Value == "ACAD_OBJECT_CHANGE_GUID");

            if (at < 0)
            {
                return;
            }

            int next = record.Groups.FindIndex(at + 1, g => g.Code == 1001);
            record.Groups.RemoveRange(at, (next < 0 ? record.Groups.Count : next) - at);
        }

        #endregion
    }
}
