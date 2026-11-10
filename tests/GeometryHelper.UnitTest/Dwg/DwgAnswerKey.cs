using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The test drawings and the comparison that proves a DWG read. Each drawing was built in code by a probe plugin
    /// inside AutoCAD 2021, saved as a DWG of AutoCAD 2000, read back by AutoCAD and written as DXF: that DXF is the
    /// answer key, AutoCAD's own word on what the DWG holds. The comparison pairs the records of the key with the records
    /// the reader makes and compares them group by group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD wrote the key from the drawing it read back, and a few things differ for that reason alone; the comparison
    /// allows exactly these:
    /// </para>
    /// <para>
    /// The objects of types newer than R2000 are given new handles on the way, so records are paired up to a renaming of
    /// handles: the header variables, the classes, the tables, the blocks and the entities by their own name or handle,
    /// the objects by following the handles from the root dictionary.
    /// </para>
    /// <para>
    /// <c>$ACADMAINTVER</c>, <c>$HANDSEED</c> and the times (<c>$TDCREATE</c>, <c>$TDUPDATE</c>, <c>$TDINDWG</c>,
    /// <c>$TDUSRTIMER</c>) are those of the later save; <c>$CMLSTYLE</c> differs in case. From R2004 a class counts its
    /// objects (group 91), and AutoCAD makes a few objects as it reads a drawing, so the counts of the key may be higher.
    /// </para>
    /// <para>
    /// A true colour does not exist in R2000: AutoCAD keeps it as round-trip data, extended data that starts with
    /// <c>RTT</c> on an entity and an extension dictionary holding <c>ACAD_XREC_ROUNDTRIP</c> on a table record, and
    /// takes that data away again when it reads the file. The reader leaves it where the file has it.
    /// </para>
    /// <para>
    /// A letter beyond the code page is <c>\U+00B0</c> in one file and the letter in the other. The order of the
    /// OBJECTS section is not compared. A record of a type the reader does not read is missing, and so is what such a
    /// record owns; both sides must agree on the count of each type left under one. A class the key has and the reader
    /// has not is counted as missing under <c>CLASS</c> and its name.
    /// </para>
    /// <para>
    /// AutoCAD adds to a drawing as it reads it or writes its DXF, and the comparison does not count these, on either side:
    /// the extended data <c>DbSaveVer</c> on the head of a table, the entry <c>ACDB_RECOMPOSE_DATA</c> of the root
    /// dictionary with the record it leads to, the dictionary <c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c> with what
    /// it holds, and the visual styles <c>$INTERFEREOBJVS</c> and <c>$INTERFEREVPVS</c>, which a file of R2010 and later
    /// holds as no handle.
    /// </para>
    /// </remarks>
    internal static class DwgAnswerKey
    {
        /// <summary>The header variables that are those of the moment of saving, or that AutoCAD sets as it reads the file.</summary>
        private static readonly string[] OfTheSave =
        {
            "$ACADMAINTVER", "$TDCREATE", "$TDUPDATE", "$HANDSEED", "$TDINDWG", "$TDUSRTIMER", "$INTERFEREOBJVS", "$INTERFEREVPVS",
        };

        private static readonly Regex Escape = new Regex(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.CultureInvariant);

        /// <summary>The DWG of a test drawing.</summary>
        public static byte[] Dwg(string name) => Resource(name + ".dwg.gz");

        /// <summary>The DXF AutoCAD wrote of a test drawing.</summary>
        public static byte[] Key(string name) => Resource(name + ".dxf.gz");

        private static byte[] Resource(string name)
        {
            var assembly = typeof(DwgAnswerKey).Assembly;
            string found = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Dwg.Data." + name, StringComparison.Ordinal));

            using (Stream packed = assembly.GetManifestResourceStream(found))
            using (var unpack = new GZipStream(packed, CompressionMode.Decompress))
            using (var copy = new MemoryStream())
            {
                unpack.CopyTo(copy);
                return copy.ToArray();
            }
        }

        /// <summary>One record of a DXF file: a header variable, a class, the head of a table, or anything with a handle.</summary>
        internal sealed class Record
        {
            public string Section;
            public string Type;
            public string Key;
            public List<KeyValuePair<int, string>> Groups = new List<KeyValuePair<int, string>>();

            /// <summary>The owner: the first 330 that is not one of the reactors, which stand between 102 groups.</summary>
            public string Owner
            {
                get
                {
                    bool inside = false;

                    foreach (KeyValuePair<int, string> group in Groups)
                    {
                        if (group.Key == 102)
                        {
                            inside = group.Value.StartsWith("{", StringComparison.Ordinal);
                        }
                        else if (group.Key == 330 && !inside)
                        {
                            return group.Value;
                        }
                    }

                    return null;
                }
            }

            public override string ToString() => Section + " " + Type + " " + Key + ": " + string.Join(" ", Groups.Select(g => g.Key + "=" + g.Value));
        }

        /// <summary>What a comparison found.</summary>
        internal sealed class Result
        {
            /// <summary>Every difference that is not one of the allowed ones, in words.</summary>
            public List<string> Differences = new List<string>();

            /// <summary>The records of the key that the reader did not make, counted by type.</summary>
            public SortedDictionary<string, int> Missing = new SortedDictionary<string, int>(StringComparer.Ordinal);

            /// <summary>The number of records paired and found the same or different.</summary>
            public int Paired;
        }

        /// <summary>The records of a DXF file in the order of the file, by key.</summary>
        public static List<Record> Parse(byte[] dxf)
        {
            string[] lines = Encoding.GetEncoding(28591).GetString(dxf).Replace("\r", string.Empty).Split('\n');
            var records = new List<Record>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string section = string.Empty;
            Record current = null;

            void Close()
            {
                if (current == null)
                {
                    return;
                }

                Record one = current;
                current = null;

                if (one.Section == "HEADER")
                {
                    one.Key = "H:" + one.Type;
                }
                else if (one.Type == "CLASS")
                {
                    one.Key = "C:" + one.Groups.First(g => g.Key == 1).Value;
                }
                else if (one.Type == "TABLE")
                {
                    one.Key = "T:" + one.Groups.First(g => g.Key == 2).Value;
                }
                else if (one.Type == "ENDTAB")
                {
                    return;
                }
                else
                {
                    int handleCode = one.Type == "DIMSTYLE" ? 105 : 5;
                    one.Key = "X:" + one.Groups.Where(g => g.Key == handleCode).Select(g => g.Value.ToUpperInvariant()).FirstOrDefault();
                }

                if (!keys.Add(one.Key))
                {
                    one.Key += "#" + records.Count.ToString(CultureInfo.InvariantCulture);
                    keys.Add(one.Key);
                }

                records.Add(one);
            }

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0)
                {
                    value = value.Trim();
                    Close();

                    if (value == "SECTION")
                    {
                        section = lines[i + 3].Trim();
                        i += 2;
                    }
                    else if (value != "ENDSEC" && value != "EOF")
                    {
                        current = new Record { Section = section, Type = value };
                    }
                }
                else if (code == 9 && section == "HEADER")
                {
                    Close();
                    current = new Record { Section = section, Type = value.Trim() };
                }
                else if (current != null)
                {
                    current.Groups.Add(new KeyValuePair<int, string>(code, code == 1 || code == 3 ? value : value.Trim()));
                }
            }

            Close();
            return records;
        }

        private static bool IsNumber(int code) => (code >= 10 && code <= 59) || (code >= 110 && code <= 149) || (code >= 210 && code <= 239)
            || (code >= 460 && code <= 469) || (code >= 1010 && code <= 1059);

        private static bool IsHandle(int code) => code == 5 || code == 105 || code == 1005 || (code >= 320 && code <= 369) || (code >= 390 && code <= 399);

        private static string Letters(string text) => Escape.Replace(text, m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

        /// <summary>
        /// Compares the DXF the reader made with the answer key. <paramref name="alsoIgnored"/> names header variables
        /// that differ in this drawing for a reason its test states.
        /// </summary>
        public static Result Compare(byte[] keyDxf, byte[] gotDxf, params string[] alsoIgnored)
        {
            List<Record> keyList = WithoutWhatAutoCadAdds(Parse(keyDxf));
            List<Record> gotList = WithoutWhatAutoCadAdds(Parse(gotDxf));
            Dictionary<string, Record> key = keyList.ToDictionary(r => r.Key, StringComparer.Ordinal);
            Dictionary<string, Record> got = gotList.ToDictionary(r => r.Key, StringComparer.Ordinal);
            var ignored = new HashSet<string>(OfTheSave.Concat(alsoIgnored).Select(n => "H:" + n), StringComparer.Ordinal);
            var gotTypes = new HashSet<string>(gotList.Select(r => r.Type), StringComparer.Ordinal);
            var keyToGot = new Dictionary<string, string>(StringComparer.Ordinal);
            var gotToKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var roundTrip = new HashSet<string>(StringComparer.Ordinal);
            var work = new Queue<string>();
            var result = new Result();
            bool firstObject = true;

            void Pair(string k, string g)
            {
                keyToGot[k] = g;
                gotToKey[g] = k;
                work.Enqueue(k);
            }

            // Everything outside OBJECTS is paired by its own key; of the objects only the first, the root dictionary.
            foreach (Record one in keyList)
            {
                if (!got.ContainsKey(one.Key) || ignored.Contains(one.Key))
                {
                    continue;
                }

                if (one.Section == "OBJECTS")
                {
                    if (!firstObject)
                    {
                        continue;
                    }

                    firstObject = false;
                }

                Pair(one.Key, one.Key);
            }

            var pending = new Queue<string>(keyList.Where(r => r.Section == "OBJECTS" && got.ContainsKey(r.Key)).Select(r => r.Key));

            while (work.Count > 0 || pending.Count > 0)
            {
                if (work.Count == 0)
                {
                    // What hangs under an object of a type that is not read is reached by its own handle.
                    string left = pending.Dequeue();

                    if (!keyToGot.ContainsKey(left) && !gotToKey.ContainsKey(left) && key[left].Type == got[left].Type)
                    {
                        Pair(left, left);
                    }

                    continue;
                }

                string k = work.Dequeue();
                Record a = key[k];
                Record b = got[keyToGot[k]];
                result.Paired++;

                if (a.Section != b.Section || a.Type != b.Type)
                {
                    result.Differences.Add(k + ": the key has " + a.Section + " " + a.Type + ", the reader " + b.Section + " " + b.Type);
                    continue;
                }

                List<KeyValuePair<int, string>> groups = WithoutRoundTrip(a, b, got, roundTrip);

                if (a.Type == "DBCOLOR")
                {
                    // The extended data DBColXD, the index, true colour and name of a colour of a book for releases that do
                    // not know it: AutoCAD takes it away from an R2000 file as it reads it, and writing R2004 to R2013 adds a
                    // second copy. Compared without it.
                    a.Groups = WithoutBookData(a.Groups);
                    groups = WithoutBookData(groups);
                }

                if (a.Type == "XRECORD" && groups.Count > a.Groups.Count && a.Owner != null && key.TryGetValue("X:" + a.Owner.ToUpperInvariant(), out Record holder)
                    && holder.Groups.Any(g => g.Key == 3 && g.Value == "ACAD_XREC_ROUNDTRIP"))
                {
                    // The round-trip data of R2000: AutoCAD takes the values it knows into the object and keeps the rest.
                    groups = groups.GetRange(0, a.Groups.Count);
                }
                string fault = groups.Count != a.Groups.Count ? "the key has " + a.Groups.Count + " groups, the reader " + groups.Count : null;

                bool graphics = false;

                for (int i = 0; i < Math.Min(a.Groups.Count, groups.Count); i++)
                {
                    int code = a.Groups[i].Key;
                    string v1 = a.Groups[i].Value;
                    string v2 = groups[i].Value;
                    bool same = code == groups[i].Key;
                    graphics = code == 310 ? graphics : (code == 92 || code == 160) && a.Section != "OBJECTS";

                    string head = v1.TrimEnd();

                    if (same && code == 1 && (a.Type == "REGION" || a.Type == "3DSOLID" || a.Type == "BODY") && a.Groups.FindIndex(g => g.Key == 1) + 1 == i
                        && head.EndsWith(" o", StringComparison.Ordinal) && v2.StartsWith(head.Substring(0, head.Length - 1), StringComparison.Ordinal))
                    {
                        // The second line of the SAT data of an ACIS entity ends in the date it was saved, counted by its
                        // length; AutoCAD writes it empty (the count 0, "o" as SAT data keeps it) as it writes the DXF.
                        continue;
                    }

                    if (same && graphics && code == 310 && v1.Length == v2.Length)
                    {
                        // The proxy graphics of an entity: AutoCAD draws them anew as it writes the DXF, and the padding
                        // after a text in them holds whatever its memory held. Compared by size.
                        continue;
                    }

                    if (same && IsNumber(code))
                    {
                        same = double.TryParse(v1, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                            && double.TryParse(v2, NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                            && Math.Abs(x - y) <= 1E-9 * Math.Max(1.0, Math.Abs(x));
                    }
                    else if (same && IsHandle(code) && a.Section != "HEADER")
                    {
                        string ka = "X:" + v1.ToUpperInvariant();
                        string kb = "X:" + v2.ToUpperInvariant();

                        if (keyToGot.TryGetValue(ka, out string paired))
                        {
                            same = paired == kb;
                        }
                        else if (gotToKey.ContainsKey(kb))
                        {
                            same = false;
                        }
                        else if (key.ContainsKey(ka) && got.ContainsKey(kb))
                        {
                            // A handle AutoCAD gave anew when it wrote the key: the two records are compared as one.
                            Pair(ka, kb);
                        }
                        else if (key.ContainsKey(ka))
                        {
                            same = !gotTypes.Contains(key[ka].Type);
                        }
                        else
                        {
                            same = ka == kb;
                        }
                    }
                    else if (same && v1 != v2)
                    {
                        same = (k == "H:$CMLSTYLE" && string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase))
                            || (a.Type == "CLASS" && code == 91)
                            || Letters(v1) == Letters(v2)
                            || (long.TryParse(v1, NumberStyles.Integer, CultureInfo.InvariantCulture, out long m)
                                && long.TryParse(v2, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) && m == n);
                    }

                    if (!same)
                    {
                        fault = "group " + i + ": the key has " + code + "=" + v1 + ", the reader " + groups[i].Key + "=" + v2;
                        break;
                    }
                }

                if (fault != null)
                {
                    result.Differences.Add(a.Section + " " + a.Type + " " + k + ": " + fault);
                }
            }

            var keyUnder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var gotUnder = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (Record one in keyList.Where(r => !keyToGot.ContainsKey(r.Key) && !ignored.Contains(r.Key)))
            {
                Count(gotTypes.Contains(one.Type) && UnderUnread(key, one.Key, gotTypes) ? keyUnder : result.Missing,
                    one.Type == "CLASS" ? "CLASS " + one.Key.Substring(2) : one.Type);
            }

            foreach (Record one in gotList.Where(r => !gotToKey.ContainsKey(r.Key) && !ignored.Contains(r.Key) && !roundTrip.Contains(r.Key)))
            {
                if (UnderUnread(got, one.Key, gotTypes))
                {
                    Count(gotUnder, one.Type);
                }
                else
                {
                    result.Differences.Add("the reader made a record the key has not: " + one.Section + " " + one.Type + " " + one.Key);
                }
            }

            if (!keyUnder.SequenceEqual(gotUnder))
            {
                result.Differences.Add("under the types not read the key has " + string.Join(" ", keyUnder) + ", the reader " + string.Join(" ", gotUnder));
            }

            List<string> keyOrder = keyList.Where(r => r.Section != "OBJECTS" && keyToGot.TryGetValue(r.Key, out string g) && g == r.Key).Select(r => r.Key).ToList();
            List<string> gotOrder = gotList.Where(r => r.Section != "OBJECTS" && gotToKey.TryGetValue(r.Key, out string g) && g == r.Key).Select(r => r.Key).ToList();

            if (!keyOrder.SequenceEqual(gotOrder))
            {
                int at = Enumerable.Range(0, Math.Min(keyOrder.Count, gotOrder.Count)).First(i => keyOrder[i] != gotOrder[i]);
                result.Differences.Add("the order differs first where the key has " + keyOrder[at] + " and the reader " + gotOrder[at]);
            }

            return result;
        }

        /// <summary>
        /// The key without what AutoCAD adds to a drawing as it reads it or writes its DXF: the extended data
        /// <c>DbSaveVer</c> on the head of a table, the entry <c>ACDB_RECOMPOSE_DATA</c> of the root dictionary with the
        /// record it leads to, and the dictionary <c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c> with what it holds.
        /// </summary>
        private static List<Record> WithoutWhatAutoCadAdds(List<Record> records)
        {
            var dropped = new HashSet<string>(StringComparer.Ordinal);

            foreach (Record one in records)
            {
                int saveVersion = one.Groups.FindIndex(g => g.Key == 1000 && g.Value == "DbSaveVer");

                if (one.Type == "TABLE" && saveVersion > 0 && one.Groups[saveVersion - 1].Key == 1001)
                {
                    one.Groups.RemoveRange(saveVersion - 1, Math.Min(3, one.Groups.Count - saveVersion + 1));
                }

                foreach (string added in new[] { "ACDB_RECOMPOSE_DATA", "AcDsDecomposeData" })
                {
                    int recompose = one.Groups.FindIndex(g => g.Key == 3 && g.Value == added);

                    if (one.Type == "DICTIONARY" && recompose >= 0 && recompose + 1 < one.Groups.Count)
                    {
                        dropped.Add("X:" + one.Groups[recompose + 1].Value.ToUpperInvariant());
                        one.Groups.RemoveRange(recompose, 2);
                    }
                }

                if (one.Type == "DICTIONARY" && one.Groups.Any(g => g.Key == 3 && g.Value == "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP"))
                {
                    dropped.Add(one.Key);
                    dropped.UnionWith(one.Groups.Where(g => g.Key == 360 || g.Key == 350).Select(g => "X:" + g.Value.ToUpperInvariant()));
                }
            }

            // And what those records own, at any depth.
            for (bool more = true; more;)
            {
                more = false;

                foreach (Record one in records)
                {
                    if (one.Section == "OBJECTS" && one.Owner != null && !dropped.Contains(one.Key) && dropped.Contains("X:" + one.Owner.ToUpperInvariant()))
                    {
                        more |= dropped.Add(one.Key);
                    }
                }
            }

            return records.Where(r => !dropped.Contains(r.Key)).ToList();
        }

        private static void Count(SortedDictionary<string, int> counts, string type)
        {
            counts.TryGetValue(type, out int count);
            counts[type] = count + 1;
        }

        /// <summary>
        /// The groups of the reader's record without the round-trip data the key has not: the extension dictionary that
        /// holds only <c>ACAD_XREC_ROUNDTRIP</c>, whose records are then expected, and the extended data that start with
        /// <c>RTT</c>.
        /// </summary>
        private static List<KeyValuePair<int, string>> WithoutRoundTrip(Record a, Record b, Dictionary<string, Record> got, HashSet<string> roundTrip)
        {
            var groups = new List<KeyValuePair<int, string>>(b.Groups);
            int open = groups.FindIndex(g => g.Key == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (open >= 0 && open + 2 < groups.Count && !a.Groups.Any(g => g.Key == 102 && g.Value == "{ACAD_XDICTIONARY")
                && got.TryGetValue("X:" + groups[open + 1].Value.ToUpperInvariant(), out Record dictionary)
                && dictionary.Type == "DICTIONARY" && dictionary.Groups.Where(g => g.Key == 3).All(g => g.Value == "ACAD_XREC_ROUNDTRIP"))
            {
                roundTrip.Add(dictionary.Key);

                foreach (KeyValuePair<int, string> entry in dictionary.Groups.Where(g => g.Key == 360))
                {
                    roundTrip.Add("X:" + entry.Value.ToUpperInvariant());
                }

                groups.RemoveRange(open, 3);
            }

            int data = groups.FindIndex(g => g.Key == 1001 && g.Value == "ACAD");

            if (data >= 0 && data + 1 < groups.Count && groups[data + 1].Key == 1000 && groups[data + 1].Value.StartsWith("RTT", StringComparison.Ordinal)
                && !a.Groups.Any(g => g.Key == 1000 && g.Value.StartsWith("RTT", StringComparison.Ordinal)))
            {
                int end = groups.FindIndex(data + 1, g => g.Key == 1001);
                groups.RemoveRange(data, (end < 0 ? groups.Count : end) - data);
            }

            return groups;
        }

        /// <summary>The groups without the extended data of ACAD that starts with DBColXD, to their end.</summary>
        private static List<KeyValuePair<int, string>> WithoutBookData(List<KeyValuePair<int, string>> groups)
        {
            int at = groups.FindIndex(g => g.Key == 1001 && g.Value == "ACAD");
            return at >= 0 && at + 1 < groups.Count && groups[at + 1].Value == "DBColXD" ? groups.GetRange(0, at) : groups;
        }

        /// <summary>Whether the owners of a record lead to a record that is not there or is of a type the reader does not read.</summary>
        private static bool UnderUnread(Dictionary<string, Record> records, string key, HashSet<string> gotTypes)
        {
            for (int i = 0; i < 50; i++)
            {
                if (!records.TryGetValue(key, out Record one) || !gotTypes.Contains(one.Type))
                {
                    return true;
                }

                string owner = one.Owner;

                if (one.Section != "OBJECTS" || owner == null || owner == "0")
                {
                    return false;
                }

                key = "X:" + owner.ToUpperInvariant();
            }

            return false;
        }
    }
}
