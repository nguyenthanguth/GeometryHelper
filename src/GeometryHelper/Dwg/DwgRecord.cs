using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// One record of a DXF file as the DWG writer takes it: its type, its groups in order, and what its head says: its
    /// handle, its owner, its reactors and its extension dictionary. The groups of its own data start at its first
    /// subclass marker (100) and end at its extended data (1001), which are kept apart.
    /// </summary>
    internal sealed class DwgRecord
    {
        private static readonly List<KeyValuePair<int, string>> None = new List<KeyValuePair<int, string>>();

        /// <summary>The type, group 0, such as <c>LINE</c> or <c>LAYER</c>.</summary>
        public string Type;

        /// <summary>Every group after group 0, in order.</summary>
        public readonly List<KeyValuePair<int, string>> Groups = new List<KeyValuePair<int, string>>();

        /// <summary>The groups of the record's own data: from its first subclass marker up to its extended data.</summary>
        public List<KeyValuePair<int, string>> Body = None;

        /// <summary>The extended data, groups 1001 and up.</summary>
        public List<KeyValuePair<int, string>> XData = None;

        /// <summary>The handle, group 5 (105 of a dimension style); 0 for none.</summary>
        public ulong Handle;

        /// <summary>The owner, the group 330 of the head; 0 for none.</summary>
        public ulong Owner;

        /// <summary>The persistent reactors, the groups 330 between <c>{ACAD_REACTORS</c> and <c>}</c>.</summary>
        public readonly List<ulong> Reactors = new List<ulong>();

        /// <summary>The extension dictionary, the group 360 between <c>{ACAD_XDICTIONARY</c> and <c>}</c>; 0 for none.</summary>
        public ulong XDictionary;

        /// <summary>Of a TABLE record of the TABLES section: the records of the table.</summary>
        public List<DwgRecord> Records;

        /// <summary>Of a block of the BLOCKS section, its BLOCK record: its entities and its ENDBLK.</summary>
        public List<DwgRecord> Entities;

        /// <summary>Of a POLYLINE or an INSERT with attributes: its vertices or attributes, in order.</summary>
        public List<DwgRecord> Owned;

        /// <summary>Of a POLYLINE or an INSERT with attributes: the SEQEND that closes it.</summary>
        public DwgRecord SeqEnd;

        /// <summary>Of an entity: whether it is in paper space, group 67.</summary>
        public bool Paper;

        /// <summary>The handle of the block record an entity is written in, or of the entity that owns it; set by the writer.</summary>
        public ulong Container;

        /// <summary>Sorts the groups into the head, the data and the extended data.</summary>
        public void Split()
        {
            int i = 0;
            bool inHead = true;

            for (; i < Groups.Count && inHead; i++)
            {
                KeyValuePair<int, string> group = Groups[i];

                switch (group.Key)
                {
                    case 5:
                    case 105:
                        Handle = Hex(group.Value);
                        break;
                    case 330:
                        Owner = Hex(group.Value);
                        break;
                    case 102:
                        string name = group.Value.Trim();

                        if (name == "{ACAD_XDICTIONARY")
                        {
                            for (i++; i < Groups.Count && Groups[i].Key != 102; i++)
                            {
                                XDictionary = Hex(Groups[i].Value);
                            }
                        }
                        else if (name == "{ACAD_REACTORS")
                        {
                            for (i++; i < Groups.Count && Groups[i].Key != 102; i++)
                            {
                                Reactors.Add(Hex(Groups[i].Value));
                            }
                        }
                        else if (name.StartsWith("{", StringComparison.Ordinal))
                        {
                            while (i + 1 < Groups.Count && Groups[i + 1].Key != 102)
                            {
                                i++;
                            }

                            i++;
                        }

                        break;
                    case 100:
                    case 1001:
                        inHead = false;
                        i--;
                        break;
                    case 67:
                        Paper = Int(group.Value) == 1;
                        break;
                }
            }

            // A record with no subclass marker, a CLASS, is data from its first group.
            int start = inHead ? 0 : Math.Min(i, Groups.Count);
            int end = Groups.FindIndex(start, group => group.Key == 1001);

            if (end < 0)
            {
                end = Groups.Count;
            }

            Body = Groups.GetRange(start, end - start);
            XData = Groups.GetRange(end, Groups.Count - end);
        }

        /// <summary>The groups of the subclass <paramref name="subclass"/> of the data, from its marker to the next; all the data where it is null.</summary>
        public DwgGroups Of(string subclass) => new DwgGroups(Body, subclass);

        /// <summary>A hex handle as DXF writes it; 0 for an empty or broken one.</summary>
        public static ulong Hex(string value)
        {
            ulong.TryParse(value.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong handle);
            return handle;
        }

        /// <summary>A whole number as DXF writes it; 0 for a broken one.</summary>
        public static int Int(string value)
        {
            string text = value.Trim();

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return number;
            }

            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long wide)
                ? unchecked((int)wide)
                : 0;
        }

        /// <summary>A real number as DXF writes it; 0 for a broken one.</summary>
        public static double Double(string value)
        {
            double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
            return number;
        }

        /// <inheritdoc/>
        public override string ToString() => Type + " " + Handle.ToString("X", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A run of the groups of a record, those of one subclass, read by code: the first value of a code, or each value of
    /// a code in turn.
    /// </summary>
    internal sealed class DwgGroups
    {
        private readonly List<KeyValuePair<int, string>> _groups;
        private readonly int _start;
        private readonly int _end;

        /// <summary>The groups of <paramref name="subclass"/> among <paramref name="groups"/>, all of them where it is null; none where the subclass is not there.</summary>
        public DwgGroups(List<KeyValuePair<int, string>> groups, string subclass)
        {
            _groups = groups;
            _start = 0;
            _end = groups.Count;

            if (subclass == null)
            {
                return;
            }

            _start = groups.FindIndex(group => group.Key == 100 && group.Value.Trim() == subclass);

            if (_start < 0)
            {
                _start = _end = 0;
                return;
            }

            _start++;
            int next = groups.FindIndex(_start, group => group.Key == 100);
            _end = next < 0 ? groups.Count : next;
        }

        /// <summary>Whether the subclass was found or the run has any group.</summary>
        public bool Any => _end > _start;

        /// <summary>The groups of the run, in order.</summary>
        public IEnumerable<KeyValuePair<int, string>> All
        {
            get
            {
                for (int i = _start; i < _end; i++)
                {
                    yield return _groups[i];
                }
            }
        }

        /// <summary>Whether the run has a group of <paramref name="code"/>.</summary>
        public bool Has(int code) => IndexOf(code) >= 0;

        /// <summary>The first value of <paramref name="code"/>; <paramref name="otherwise"/> where there is none.</summary>
        public string Text(int code, string otherwise = "")
        {
            int at = IndexOf(code);
            return at < 0 ? otherwise : _groups[at].Value;
        }

        /// <summary>The first value of <paramref name="code"/> as a whole number; <paramref name="otherwise"/> where there is none.</summary>
        public int Int(int code, int otherwise = 0)
        {
            int at = IndexOf(code);
            return at < 0 ? otherwise : DwgRecord.Int(_groups[at].Value);
        }

        /// <summary>The first value of <paramref name="code"/> as a real number; <paramref name="otherwise"/> where there is none.</summary>
        public double Double(int code, double otherwise = 0.0)
        {
            int at = IndexOf(code);
            return at < 0 ? otherwise : DwgRecord.Double(_groups[at].Value);
        }

        /// <summary>The first value of <paramref name="code"/> as a handle; 0 where there is none.</summary>
        public ulong Handle(int code)
        {
            int at = IndexOf(code);
            return at < 0 ? 0 : DwgRecord.Hex(_groups[at].Value);
        }

        /// <summary>The point of <paramref name="code"/>: its first values of the code, the code plus 10 and plus 20; <paramref name="x"/>, <paramref name="y"/> and <paramref name="z"/> for each that is not there.</summary>
        public double[] Point(int code, double x = 0.0, double y = 0.0, double z = 0.0)
        {
            return new[] { Double(code, x), Double(code + 10, y), Double(code + 20, z) };
        }

        /// <summary>Every value of <paramref name="code"/>, in order.</summary>
        public List<string> Each(int code)
        {
            var values = new List<string>();

            for (int i = _start; i < _end; i++)
            {
                if (_groups[i].Key == code)
                {
                    values.Add(_groups[i].Value);
                }
            }

            return values;
        }

        private int IndexOf(int code)
        {
            for (int i = _start; i < _end; i++)
            {
                if (_groups[i].Key == code)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
