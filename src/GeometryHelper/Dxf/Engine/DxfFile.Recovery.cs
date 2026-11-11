using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        /// <summary>A file read from bytes <see cref="DxfRecovery"/> has repaired, said in the log as from where they came.</summary>
        internal static DxfFile Recovered(byte[] bytes, int length, string source) =>
            new DxfFile(bytes, length, source);

        /// <summary>What <see cref="DxfDatabase.Audit"/> reads of an entity: the names its head record gives, and what is wrong with its numbers.</summary>
        internal sealed class Audited
        {
            internal string Layer { get; set; }

            internal string Linetype { get; set; }

            internal string TextStyle { get; set; }

            internal string DimStyle { get; set; }

            internal string Block { get; set; }

            /// <summary>Each number found wrong: the record (0 its own, then those that follow it), the index of the group as <see cref="SetValueAt"/> counts it, its code, its text, its kind, and the value it is given.</summary>
            internal List<Tuple<int, int, int, string, DxfRepairKind, string>> Wrong { get; } =
                new List<Tuple<int, int, int, string, DxfRepairKind, string>>();
        }

        /// <summary>
        /// Reads an entity for the audit: the layer (8), linetype (6), text style (7 of a text or attribute), dimension style (3 of
        /// a dimension, leader or tolerance) and block (2 of an INSERT) of its own record, each the first of its code; and the
        /// groups of a number of its records that are no number or not finite, and the count of a LWPOLYLINE or HATCH that is
        /// not the number of its parts, as <see cref="DxfRecovery"/> reads them.
        /// </summary>
        internal Audited Audit(DxfItem item)
        {
            var audited = new Audited();
            string type = item.Type;
            List<Rec> records = RecordsOf(item);

            for (int r = 0; r < records.Count; r++)
            {
                Rec record = records[r];
                var walk = new GroupWalk(record);
                int index = 0;
                bool counted = r == 0 && (type == "LWPOLYLINE" || type == "HATCH");
                List<int> codes = counted ? new List<int>() : null;
                List<string> values = counted ? new List<string>() : null;

                while (walk.Next(out int code, out int start, out int end))
                {
                    int kind = DxfRecovery.NumberKind(code);

                    if (kind != 0 && !DxfRecovery.IsPlainNumber(record.Bytes, start, end, kind == 1))
                    {
                        string text = Text(record, start, end);
                        int verdict = DxfRecovery.Classify(text, kind == 1, out string value);

                        if (verdict != 0)
                        {
                            audited.Wrong.Add(
                                Tuple.Create(
                                    r,
                                    index,
                                    code,
                                    text,
                                    verdict == 2 ? DxfRepairKind.NonFiniteNumber : DxfRepairKind.BadNumber,
                                    value
                                )
                            );
                        }
                    }

                    if (r == 0)
                    {
                        Take(audited, type, code, record, start, end);
                    }

                    if (counted)
                    {
                        codes.Add(code);
                        values.Add(code == 90 || code == 91 ? Text(record, start, end) : null);
                    }

                    index++;
                }

                int should = 0;
                int at = counted ? DxfRecovery.WrongCount(type, codes, values, out should) : -1;

                if (at >= 0)
                {
                    audited.Wrong.Add(
                        Tuple.Create(
                            0,
                            at,
                            codes[at],
                            values[at],
                            type == "HATCH" ? DxfRepairKind.WrongLoopCount : DxfRepairKind.WrongVertexCount,
                            should.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        )
                    );
                }
            }

            return audited;
        }

        private void Take(Audited audited, string type, int code, Rec record, int start, int end)
        {
            switch (code)
            {
                case 8 when audited.Layer == null:
                    audited.Layer = DxfReader.NameOf(Text(record, start, end));
                    break;
                case 6 when audited.Linetype == null:
                    audited.Linetype = DxfReader.NameOf(Text(record, start, end));
                    break;
                case 7
                    when audited.TextStyle == null
                        && (type == "TEXT" || type == "MTEXT" || type == "ATTRIB" || type == "ATTDEF"):
                    audited.TextStyle = DxfReader.NameOf(Text(record, start, end));
                    break;
                case 3
                    when audited.DimStyle == null && (type == "DIMENSION" || type == "LEADER" || type == "TOLERANCE"):
                    audited.DimStyle = DxfReader.NameOf(Text(record, start, end));
                    break;
                case 2 when audited.Block == null && type == "INSERT":
                    audited.Block = DxfReader.NameOf(Text(record, start, end));
                    break;
            }
        }

        /// <summary>The names the CLASSES section gives the entities and objects it declares (group 1), letters of either case the same.</summary>
        internal HashSet<string> ClassNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Section section in _sections)
            {
                foreach (Rec record in section.Name == "CLASSES" ? Inside(section) : new List<Rec>())
                {
                    if (record.Type == "CLASS")
                    {
                        names.Add((Record(record).Text(1) ?? string.Empty).Trim());
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// The entries of the dictionaries of the OBJECTS section that are broken: a name (3) with no pointer (350 or 360) after
        /// it, a pointer with no name before it, or a pointer that is not a handle (not hexadecimal, or nought). For each, the
        /// dictionary's item, the index of its first group and how many groups it is, as <see cref="RemoveGroupAt"/> counts them,
        /// the name and the pointer (either null where there is none). In the order of the file, and in each dictionary from its
        /// last entry back, so that they are taken out in that order. A pointer to an object the file does not hold is not
        /// broken: AutoCAD writes those of objects it leaves out of a DXF, as those of AutoCAD Architecture.
        /// </summary>
        internal List<Tuple<DxfItem, int, int, string, string>> BrokenEntries()
        {
            var broken = new List<Tuple<DxfItem, int, int, string, string>>();

            foreach (Section section in _sections)
            {
                foreach (Rec record in section.Name == "OBJECTS" ? Inside(section) : new List<Rec>())
                {
                    if (record.Type != "DICTIONARY" && record.Type != "ACDBDICTIONARYWDFLT")
                    {
                        continue;
                    }

                    var walk = new GroupWalk(record);
                    var found = new List<Tuple<DxfItem, int, int, string, string>>();
                    bool own = false;
                    int index = 0;
                    int name = -1;
                    string named = null;
                    string handle = null;

                    while (walk.Next(out int code, out int start, out int end))
                    {
                        string text =
                            code == 5 || code == 100 || code == 3 || code == 350 || code == 360
                                ? Text(record, start, end)
                                : null;
                        handle = code == 5 && handle == null ? text.Trim() : handle;
                        own |= code == 100 && text.Trim() == "AcDbDictionary";

                        if (own && (code == 3 || code == 350 || code == 360))
                        {
                            bool pointer = code != 3;

                            if (name >= 0 && !pointer)
                            {
                                found.Add(Tuple.Create<DxfItem, int, int, string, string>(null, name, 1, named, null));
                            }
                            else if (
                                pointer && (name < 0 || !DxfRecovery.TryHex(text, out ulong pointed) || pointed == 0)
                            )
                            {
                                found.Add(
                                    Tuple.Create<DxfItem, int, int, string, string>(
                                        null,
                                        name < 0 ? index : name,
                                        name < 0 ? 1 : 2,
                                        named,
                                        text.Trim()
                                    )
                                );
                            }

                            name = pointer ? -1 : index;
                            named = pointer ? null : text;
                        }

                        index++;
                    }

                    if (name >= 0)
                    {
                        found.Add(Tuple.Create<DxfItem, int, int, string, string>(null, name, 1, named, null));
                    }

                    DxfItem item = found.Count > 0 ? ObjectItem(handle) : null;

                    for (int i = found.Count - 1; i >= 0 && item != null; i--)
                    {
                        broken.Add(Tuple.Create(item, found[i].Item2, found[i].Item3, found[i].Item4, found[i].Item5));
                    }
                }
            }

            return broken;
        }
    }
}
