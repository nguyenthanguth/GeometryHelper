using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region The round trip of a visual style

        // The objects made for the round trip of the styles, put into the OBJECTS section once it has been walked.
        private readonly List<Record> _added = new List<Record>();

        /// <summary>The number of flags of how a property is set that a style keeps in its round trip: its 58 properties and 5 more.</summary>
        private const int FlagCount = 63;

        private sealed class TailProperty
        {
            internal char Kind;
            internal string Value;
            internal string Colour;
            internal string Set = "1";
        }

        /// <summary>
        /// The xrecord AutoCAD keeps under a style that its version holds in a form of its own, in the style's extension dictionary
        /// as <c>ACAD_XREC_ROUNDTRIP</c>: what the form has no place for (the 30 properties of R2013 and how each property is set).
        /// Null where the style has none.
        /// </summary>
        private Record RoundTripRecord(Record record)
        {
            int at = record.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at < 0 || at + 1 >= record.Groups.Count || record.Groups[at + 1].Code != 360)
            {
                return null;
            }

            if (_objects == null)
            {
                _objects = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

                foreach (Record one in Find("OBJECTS")?.Records ?? new List<Record>())
                {
                    string handle = HandleOf(one);

                    if (handle != null)
                    {
                        _objects[handle] = one;
                    }
                }
            }

            if (!_objects.TryGetValue(record.Groups[at + 1].Value.Trim(), out Record dictionary))
            {
                return null;
            }

            int entry = dictionary.Groups.FindIndex(g => g.Code == 3 && g.Value == "ACAD_XREC_ROUNDTRIP");

            return
                entry < 0
                || entry + 1 >= dictionary.Groups.Count
                || !_objects.TryGetValue(dictionary.Groups[entry + 1].Value.Trim(), out Record xrecord)
                || xrecord.Type != "XRECORD"
                ? null
                : xrecord;
        }

        /// <summary>
        /// Gives the 28 properties of a style the way each is set that its round trip holds (<c>RTVSPropertyOp0</c> to <c>27</c>,
        /// 1 set, 0 left to the parent style, 2 set by the style itself); a style with none keeps them all set.
        /// </summary>
        private static void ReadFlags(Record xrecord, List<StyleProperty> props)
        {
            if (xrecord == null)
            {
                return;
            }

            for (int i = 0; i < props.Count; i++)
            {
                string set = FlagOf(xrecord.Groups, "RTVSPropertyOp" + i.ToString(CultureInfo.InvariantCulture));

                if (set != null)
                {
                    props[i].Set = set;
                }
            }
        }

        /// <summary>The value (70) after the name (102) given in the groups of an xrecord; null where it is not there.</summary>
        private static string FlagOf(List<Group> groups, string name)
        {
            int at = groups.FindIndex(g => g.Code == 102 && g.Value == name);
            return at < 0 || at + 1 >= groups.Count || groups[at + 1].Code != 70 ? null : groups[at + 1].Value.Trim();
        }

        /// <summary>
        /// Keeps what a version has no place for in the round trip of a style going down, as AutoCAD 2021 does: the 30 properties
        /// R2013 added (<c>RTVSPost2010Prop28</c> to <c>57</c>, each with how it is set) in an xrecord of the extension dictionary
        /// of the style, and from R2007 down, whose style has no word on how a property is set, that of all 63 properties
        /// (<c>RTVSPropertyOp0</c> to <c>62</c>), so that a later upgrade finds the style as it was. A style that already has its
        /// xrecord (a style of R2010 going to R2007) is given the flags.
        /// </summary>
        private void KeepRoundTrip(Record style, List<StyleProperty> props, List<Group> tailGroups, int form)
        {
            Record existing = RoundTripRecord(style);
            var flags = new List<string>(FlagCount);

            foreach (StyleProperty prop in props)
            {
                flags.Add(prop.Set);
            }

            if (existing != null)
            {
                if (form == 0 && FlagOf(existing.Groups, "RTVSPropertyOp0") == null)
                {
                    for (int n = 0; n < TailKinds.Length; n++)
                    {
                        string name =
                            "RTVSPost2010PropOp" + (StyleKinds.Length + n).ToString(CultureInfo.InvariantCulture);
                        flags.Add(FlagOf(existing.Groups, name) ?? "1");
                    }

                    AddFlags(existing.Groups, flags);
                }

                return;
            }

            // A style that has an extension dictionary of another kind keeps it as it is.
            if (style.Groups.Exists(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY"))
            {
                return;
            }

            List<TailProperty> tail = ParseTail(tailGroups);
            string handle = HandleOf(style);
            string dictionary = handle == null ? null : NewHandle();
            string xrecord = dictionary == null ? null : NewHandle();

            if (tail == null || xrecord == null)
            {
                return;
            }

            var body = new List<Group>
            {
                new Group(0, "XRECORD"),
                new Group(5, xrecord),
                new Group(102, "{ACAD_REACTORS"),
                new Group(330, dictionary),
                new Group(102, "}"),
                new Group(330, dictionary),
                new Group(100, "AcDbXrecord"),
                Whole(280, 1),
            };

            foreach (TailProperty prop in tail)
            {
                flags.Add(prop.Set);
            }

            for (int n = 0; n < tail.Count; n++)
            {
                TailEntry(body, StyleKinds.Length + n, tail[n]);
            }

            if (form == 0)
            {
                AddFlags(body, flags);
            }

            var dictionaryRecord = new Record();
            dictionaryRecord.Groups.AddRange(
                new[]
                {
                    new Group(0, "DICTIONARY"),
                    new Group(5, dictionary),
                    new Group(330, handle),
                    new Group(100, "AcDbDictionary"),
                    Whole(280, 1),
                    Whole(281, 1),
                    new Group(3, "ACAD_XREC_ROUNDTRIP"),
                    new Group(360, xrecord),
                }
            );
            var xrecordRecord = new Record { Groups = body };
            _added.Add(dictionaryRecord);
            _added.Add(xrecordRecord);

            style.Groups.InsertRange(
                2,
                new[] { new Group(102, "{ACAD_XDICTIONARY"), new Group(360, dictionary), new Group(102, "}") }
            );
        }

        /// <summary>Takes the flags of how a property is set (<c>RTVSPropertyOp0</c> on) out of the xrecord of a style that goes to R2010, which says it in the style itself.</summary>
        private void DropFlags(Record style)
        {
            Record xrecord = RoundTripRecord(style);
            int at =
                xrecord == null ? -1 : xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "RTVSPropertyOp0");

            if (at >= 0)
            {
                xrecord.Groups.RemoveRange(at, xrecord.Groups.Count - at);
            }
        }

        /// <summary>The 63 flags, each as <c>RTVSPropertyOp</c>, its number and its value (70): the 30 of the tail follow the 28, and 5 more, set.</summary>
        private void AddFlags(List<Group> body, List<string> flags)
        {
            // The 5 flags past the properties follow the style: set, or left to the parent where its properties are.
            while (flags.Count < FlagCount)
            {
                flags.Add(flags[0] == "0" ? "0" : "1");
            }

            for (int i = 0; i < FlagCount; i++)
            {
                body.Add(new Group(102, "RTVSPropertyOp" + i.ToString(CultureInfo.InvariantCulture)));
                body.Add(Whole(70, WholeValue(flags[i])));
            }
        }

        /// <summary>One property of the tail in the xrecord: its value by its kind (280, 90, 140, a colour as its index and its true colour, or the text) and how it is set.</summary>
        private void TailEntry(List<Group> body, int number, TailProperty prop)
        {
            string name = "RTVSPost2010Prop" + number.ToString(CultureInfo.InvariantCulture);

            switch (prop.Kind)
            {
                case '2':
                    body.Add(new Group(102, name));
                    body.Add(Whole(280, WholeValue(prop.Value)));
                    break;
                case '9':
                    body.Add(new Group(102, name));
                    body.Add(Whole(90, WholeValue(prop.Value)));
                    break;
                case '4':
                    body.Add(new Group(102, name));
                    body.Add(new Group(140, prop.Value));
                    break;
                case '1':
                    body.Add(new Group(102, name));
                    body.Add(new Group(1, prop.Value));
                    break;
                default:
                    body.Add(new Group(102, name + "ColorIndex"));
                    body.Add(Whole(90, WholeValue(prop.Value)));
                    body.Add(new Group(102, name + "ColorRGB"));
                    body.Add(Whole(90, prop.Colour == null ? -16777216 : WholeValue(prop.Colour)));
                    break;
            }

            body.Add(new Group(102, name.Replace("Prop", "PropOp")));
            body.Add(Whole(70, WholeValue(prop.Set)));
        }

        /// <summary>The 30 properties of R2013 after the first 28 in the groups of a style, each by its kind; null where they are not there.</summary>
        private static List<TailProperty> ParseTail(List<Group> groups)
        {
            var tail = new List<TailProperty>(TailKinds.Length);
            int at = 0;

            foreach (char kind in TailKinds)
            {
                int code =
                    kind == '9' ? 90
                    : kind == '4' ? 40
                    : kind == '6' ? 62
                    : kind == '1' ? 1
                    : 290;

                if (at >= groups.Count || groups[at].Code != code)
                {
                    return null;
                }

                var prop = new TailProperty { Kind = kind, Value = groups[at++].Value };

                if (kind == '6' && at < groups.Count && groups[at].Code == 420)
                {
                    prop.Colour = groups[at++].Value;
                }

                if (at >= groups.Count || groups[at].Code != 176)
                {
                    return null;
                }

                prop.Set = groups[at++].Value.Trim();
                tail.Add(prop);
            }

            return tail;
        }

        // The record of the header that holds the seed, found once: the cells of a table going down take a handle each.
        private Record _seed;

        /// <summary>The next handle of the drawing (<c>$HANDSEED</c>, which moves on); null where the header has none.</summary>
        private string NewHandle()
        {
            Record seed = _seed
                ?? (_seed = Find("HEADER")?.Records.Find(r => r.Groups[0].Value == "$HANDSEED" && r.Groups.Count > 1));

            if (
                seed == null
                || !ulong.TryParse(
                    seed.Groups[1].Value.Trim(),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out ulong next
                )
            )
            {
                return null;
            }

            seed.Groups[1] = new Group(seed.Groups[1].Code, (next + 1).ToString("X", CultureInfo.InvariantCulture));
            return next.ToString("X", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
