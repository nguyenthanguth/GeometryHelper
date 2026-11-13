using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The bodies of SAT text, as a DXF of R2000 to R2010 gives them, turned into the SAB a DWG of R2013 and later keeps.
    /// </content>
    internal sealed partial class DwgDrawing
    {
        /// <summary>
        /// Turns the SAT of each REGION, 3DSOLID and BODY that holds text (a DXF of R2000 to R2010: its lines after
        /// <c>AcDbModelerGeometry</c>, no GUID) into the SAB of <paramref name="form"/>, as AutoCAD 2021 does when it saves such
        /// a drawing as a DWG of R2018: the entity names its data by a GUID (the one its extended data kept, which is taken out,
        /// else one made of its handle, so that the same drawing gives the same bytes), the data joins <see cref="AcisData"/>,
        /// and the date of the SAT stays the date of the SAB. A body whose SAT the grammars do not place (ACIS 4.00 of R2000,
        /// a record or a subtype they leave out) is not changed, and the writer leaves it out. The number of bodies turned is
        /// returned.
        /// </summary>
        public int SatBodiesToSab(AcisFormat form)
        {
            int turned = 0;
            var held = new HashSet<ulong>();

            foreach (KeyValuePair<ulong, byte[]> data in AcisData)
            {
                held.Add(data.Key);
            }

            foreach (DwgRecord record in All)
            {
                if (
                    (record.Type != "3DSOLID" && record.Type != "REGION" && record.Type != "BODY")
                    || held.Contains(record.Handle)
                )
                {
                    continue;
                }

                int at = record.Body.FindIndex(g => g.Key == 100 && g.Value.Trim() == "AcDbModelerGeometry");

                if (at < 0)
                {
                    continue;
                }

                int end = at + 1;
                var lines = new List<string>();
                var line = new StringBuilder();
                bool named = false;

                for (; end < record.Body.Count && record.Body[end].Key != 100; end++)
                {
                    KeyValuePair<int, string> group = record.Body[end];

                    if (group.Key == 2)
                    {
                        named = true;
                    }
                    else if (group.Key == 1 || group.Key == 3)
                    {
                        line.Append(group.Value);

                        if (group.Key == 1)
                        {
                            lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                            line.Clear();
                        }
                    }
                }

                if (named || lines.Count == 0)
                {
                    continue;
                }

                byte[] sab = DwgSat.ToSab(lines, form, null);

                if (sab == null)
                {
                    continue;
                }

                Guid guid = ChangeGuid(record) ?? GuidOfHandle(record.Handle);
                record.Body.RemoveRange(at + 1, end - at - 1);
                record.Body.Insert(at + 1, new KeyValuePair<int, string>(290, "1"));
                record.Body.Insert(at + 2, new KeyValuePair<int, string>(2, guid.ToString("B")));
                ForgetChangeGuid(record);
                AcisData.Add(new KeyValuePair<ulong, byte[]>(record.Handle, sab));
                held.Add(record.Handle);
                turned++;
            }

            return turned;
        }

        /// <summary>A GUID made of a handle alone: its eight bytes, then their complements.</summary>
        private static Guid GuidOfHandle(ulong handle)
        {
            var bytes = new byte[16];
            Array.Copy(BitConverter.GetBytes(handle), 0, bytes, 0, 8);
            Array.Copy(BitConverter.GetBytes(~handle), 0, bytes, 8, 8);
            return new Guid(bytes);
        }

        /// <summary>
        /// The identity AutoCAD keeps of a body in its extended data of R2010 and before (ACAD_OBJECT_CHANGE_GUID: a 1070 of 1,
        /// the first part of the GUID as a 1071, two 1070s, then eight bytes as 1070s); null where the record has none.
        /// </summary>
        private static Guid? ChangeGuid(DwgRecord record)
        {
            int at = record.XData.FindIndex(g => g.Key == 1001 && g.Value.Trim() == "ACAD_OBJECT_CHANGE_GUID");

            if (at < 0 || at + 12 >= record.XData.Count)
            {
                return null;
            }

            var parts = new long[11];

            for (int i = 0; i < 11; i++)
            {
                KeyValuePair<int, string> group = record.XData[at + 2 + i];

                if (
                    (group.Key != 1070 && group.Key != 1071)
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

            return new Guid((int)parts[0], (short)parts[1], (short)parts[2], tail);
        }

        /// <summary>Takes the extended data of the identity of a body out of a record: the DWG names it in the entity.</summary>
        private static void ForgetChangeGuid(DwgRecord record)
        {
            int at = record.XData.FindIndex(g => g.Key == 1001 && g.Value.Trim() == "ACAD_OBJECT_CHANGE_GUID");

            if (at < 0)
            {
                return;
            }

            int next = record.XData.FindIndex(at + 1, g => g.Key == 1001);
            record.XData.RemoveRange(at, (next < 0 ? record.XData.Count : next) - at);
        }
    }
}
