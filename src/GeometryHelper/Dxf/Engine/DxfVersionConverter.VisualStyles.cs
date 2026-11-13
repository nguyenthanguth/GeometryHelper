using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region Visual styles

        // The first 28 properties of a visual style are the same in every version, by their order: the DXF group each has in R2007
        // and R2010, and its kind (9 a whole number, 4 a real, 6 a colour, 2 a yes or no), which R2013 writes as 90, 40, 62 with 420,
        // and 290. The 30 properties R2013 added follow them.
        private static readonly int[] StyleCodes =
        {
            71,
            72,
            73,
            90,
            40,
            41,
            63,
            74,
            91,
            64,
            65,
            75,
            175,
            42,
            92,
            66,
            43,
            76,
            77,
            78,
            67,
            79,
            170,
            171,
            290,
            93,
            44,
            173,
        };

        private const string StyleKinds = "9999446996699496499969992949";

        // The kinds of the 30 that follow, as R2013 writes them.
        private const string TailKinds = "222222222944969962999296491244";

        private Dictionary<string, Record> _objects;

        /// <summary>The 30 properties R2013 added, as AutoCAD 2021 gives them to a style that has no others, in the order they are written: kind, value and where they are not set (1, the usual, or 0).</summary>
        private const string StyleTail =
            "2:0 2:1 2:1 2:0 2:0 2:0 2:0 2:0 2:0 9:50 4:0.0 4:1.0 9:0 6:18,0 9:50 9:3 6:5,255 2:0 9:50 9:50 9:50 2:0 9:50 "
            + "6:256,-16777216@0 4:1.0@0 9:2 1:strokes_ogs.tif 2:0 4:1.0 4:1.0";

        private sealed class StyleProperty
        {
            internal char Kind;
            internal string Value;
            internal string Colour;
            internal string Set = "1";
        }

        /// <summary>
        /// Gives a visual style the form of the target version: R2000 to R2007 (one form, R2000 without true colours), R2010 (each
        /// property followed by how it is set, 176) and R2013 and later (the 28 properties of R2010 in codes of their kind, and 30
        /// more). The properties are the same ones, in the same order, in each (the first 28), so that a style goes from one form
        /// to another with its values: the signs of the opacity and the specular level that R2007 writes by the face modifiers
        /// the style has, the 62 of nought that it writes after them, its 174 and 45 and its 291 at the end. What R2013 added is
        /// read back from the round trip AutoCAD keeps under the style (<c>RTVSPost2010Prop</c>) where there is one, else is what
        /// AutoCAD 2021 gives a style that has no others. False where the style is not in the form its version has.
        /// </summary>
        private bool VisualStyle(Record record)
        {
            List<Group> groups = record.Groups;

            if (!Subclass(groups, "AcDbVisualStyle", out int start, out int end))
            {
                return false;
            }

            var body = groups.GetRange(start + 1, end - start - 1);
            List<StyleProperty> props = ParseStyle(
                body,
                Style(_source),
                out string flag,
                out string name,
                out string type
            );

            if (props == null)
            {
                return false;
            }

            var made = new List<Group>
            {
                new Group(2, name),
                Whole(70, int.Parse(type.Trim(), CultureInfo.InvariantCulture)),
            };
            int form = Style(_target);

            if (form >= 1)
            {
                made.Add(Whole(177, form == 1 ? 2 : 3));
                made.Add(Whole(291, int.Parse(flag.Trim(), CultureInfo.InvariantCulture)));

                if (form == 2)
                {
                    made.Add(Whole(70, StyleKinds.Length + StyleTail.Split(' ').Length));
                }
            }

            int modifiers = 0;

            if (form == 0)
            {
                Number(props[3].Value, out modifiers);
            }

            for (int i = 0; i < props.Count; i++)
            {
                StyleProperty prop = props[i];
                int code = StyleCodes[i];

                switch (prop.Kind)
                {
                    case '9':
                        made.Add(form == 2 ? Whole(90, WholeValue(prop.Value)) : Whole(code, WholeValue(prop.Value)));
                        break;
                    case '2':
                        made.Add(form == 2 ? Whole(290, WholeValue(prop.Value)) : Whole(code, WholeValue(prop.Value)));
                        break;
                    case '4':
                        made.Add(
                            new Group(
                                form == 2 ? 40 : code,
                                code == 40 || code == 41
                                    ? form == 0
                                        ? Signed(prop.Value, code, modifiers)
                                        : prop.Value.TrimStart('-')
                                    : prop.Value
                            )
                        );
                        break;
                    default:
                        made.Add(form == 2 ? Whole(62, WholeValue(prop.Value)) : Whole(code, WholeValue(prop.Value)));

                        if (prop.Colour != null)
                        {
                            made.Add(Whole(form == 2 ? 420 : code + 358, WholeValue(prop.Colour)));
                        }

                        break;
                }

                if (form >= 1)
                {
                    made.Add(Whole(176, WholeValue(prop.Set)));
                }
                else if (code == 41)
                {
                    made.Add(Whole(62, 5));
                }
                else if (code == 290)
                {
                    made.Add(Whole(174, 0));
                }
            }

            if (form == 0)
            {
                made.Add(Whole(291, int.Parse(flag.Trim(), CultureInfo.InvariantCulture)));
                made.Add(new Group(45, "0.0"));
            }
            else if (form == 2)
            {
                made.AddRange(StyleTailGroups(record));
            }

            groups.RemoveRange(start + 1, end - start - 1);
            groups.InsertRange(start + 1, made);

            if (form == 2 && Style(_source) < 2)
            {
                ForgetDictionary(record);
            }

            return true;
        }

        private static int WholeValue(string text) => Number(text, out int value) ? value : 0;

        /// <summary>The opacity and the specular level of R2007, negative where the face modifiers (90) do not use them: bit 1 for the first, bit 2 for the second.</summary>
        private static string Signed(string value, int code, int modifiers)
        {
            string plain = value.TrimStart('-');
            bool used = (modifiers & (code == 40 ? 1 : 2)) != 0;
            bool zero =
                double.TryParse(plain, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && number == 0.0;
            return used || zero || (code != 40 && code != 41) ? plain : "-" + plain;
        }

        /// <summary>
        /// The 28 properties of a style in the form of a version, each as a kind, value, true colour and how it is set; null where
        /// the groups are not those of the form. The name, type and the yes or no of the style's use are given back.
        /// </summary>
        private static List<StyleProperty> ParseStyle(
            List<Group> body,
            int form,
            out string flag,
            out string name,
            out string type
        )
        {
            flag = "0";
            name = null;
            type = null;
            var props = new List<StyleProperty>();
            var data = new List<Group>();
            int i = 0;

            for (; i < body.Count; i++)
            {
                if (body[i].Code == 2 && name == null)
                {
                    name = body[i].Value;
                }
                else if (body[i].Code == 70 && type == null)
                {
                    type = body[i].Value;
                    i++;
                    break;
                }
                else
                {
                    return null;
                }
            }

            if (name == null || type == null)
            {
                return null;
            }

            for (; i < body.Count; i++)
            {
                Group group = body[i];

                if (form == 0 ? (group.Code == 62 || group.Code == 174 || group.Code == 45) : group.Code == 177)
                {
                    continue;
                }

                if (group.Code == 291)
                {
                    flag = group.Value;
                    continue;
                }

                if (form == 2 && data.Count == 0 && group.Code == 70)
                {
                    continue;
                }

                data.Add(group);
            }

            int at = 0;

            for (int k = 0; k < StyleKinds.Length; k++)
            {
                char kind = StyleKinds[k];
                int code =
                    form == 2
                        ? (
                            kind == '9' ? 90
                            : kind == '4' ? 40
                            : kind == '6' ? 62
                            : 290
                        )
                        : StyleCodes[k];

                if (at >= data.Count || data[at].Code != code)
                {
                    return null;
                }

                var prop = new StyleProperty { Kind = kind, Value = data[at++].Value };

                if (kind == '6')
                {
                    int truth = form == 2 ? 420 : code + 358;

                    if (at < data.Count && data[at].Code == truth)
                    {
                        prop.Colour = data[at++].Value;
                    }
                }

                if (form >= 1)
                {
                    if (at >= data.Count || data[at].Code != 176)
                    {
                        return null;
                    }

                    prop.Set = data[at++].Value.Trim();
                }

                props.Add(prop);
            }

            return props;
        }

        /// <summary>
        /// The groups of the 30 properties R2013 added to a style, from the round trip record AutoCAD 2021 keeps for them under the
        /// style's extension dictionary of R2010 and before (<c>RTVSPost2010Prop28</c> to <c>57</c>, each with how it is set);
        /// null where the style has none or it is not whole.
        /// </summary>
        private List<Group> RoundTripTail(Record record)
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

            if (
                entry < 0
                || entry + 1 >= dictionary.Groups.Count
                || !_objects.TryGetValue(dictionary.Groups[entry + 1].Value.Trim(), out Record xrecord)
                || xrecord.Type != "XRECORD"
            )
            {
                return null;
            }

            var groups = new List<Group>();
            List<Group> own = xrecord.Groups;
            int i = own.FindIndex(g => g.Code == 100 && g.Value == "AcDbXrecord") + 1;

            // A 280 after the marker is the cloning flag, which stands before the first property.
            if (i > 0 && i < own.Count && own[i].Code == 280)
            {
                i++;
            }

            for (int n = 0; n < TailKinds.Length; n++)
            {
                char kind = TailKinds[n];
                string prop = "RTVSPost2010Prop" + (StyleKinds.Length + n).ToString(CultureInfo.InvariantCulture);
                string value;
                string colour = null;

                if (kind == '6')
                {
                    if (
                        !Entry(own, ref i, prop + "ColorIndex", out value)
                        || !Entry(own, ref i, prop + "ColorRGB", out colour)
                    )
                    {
                        return null;
                    }
                }
                else if (!Entry(own, ref i, prop, out value))
                {
                    return null;
                }

                if (!Entry(own, ref i, prop.Replace("Prop", "PropOp"), out string set))
                {
                    return null;
                }

                switch (kind)
                {
                    case '2':
                        groups.Add(Whole(290, WholeValue(value)));
                        break;
                    case '9':
                        groups.Add(Whole(90, WholeValue(value)));
                        break;
                    case '4':
                        groups.Add(new Group(40, value));
                        break;
                    case '1':
                        groups.Add(new Group(1, value));
                        break;
                    default:
                        groups.Add(Whole(62, WholeValue(value)));

                        if (WholeValue(colour) != -16777216)
                        {
                            groups.Add(Whole(420, WholeValue(colour)));
                        }

                        break;
                }

                groups.Add(Whole(176, WholeValue(set)));
            }

            return groups;
        }

        /// <summary>The value after a name (<c>102</c>) in the groups of a record, at <paramref name="at"/>; false where the name is not next.</summary>
        private static bool Entry(List<Group> groups, ref int at, string name, out string value)
        {
            value = null;

            if (at + 1 >= groups.Count || groups[at].Code != 102 || groups[at].Value != name)
            {
                return false;
            }

            value = groups[at + 1].Value;
            at += 2;
            return true;
        }

        /// <summary>The groups of the 30 properties R2013 added to a style: those of its round trip record where it keeps one, else the usual ones.</summary>
        private List<Group> StyleTailGroups(Record record)
        {
            List<Group> kept = RoundTripTail(record);

            if (kept != null)
            {
                return kept;
            }

            var groups = new List<Group>();

            foreach (string item in StyleTail.Split(' '))
            {
                string[] parts = item.Split(':');
                string value = parts[1];
                string set = "1";
                int at = value.IndexOf('@');

                if (at >= 0)
                {
                    set = value.Substring(at + 1);
                    value = value.Substring(0, at);
                }

                switch (parts[0])
                {
                    case "2":
                        groups.Add(Whole(290, WholeValue(value)));
                        break;
                    case "9":
                        groups.Add(Whole(90, WholeValue(value)));
                        break;
                    case "4":
                        groups.Add(new Group(40, value));
                        break;
                    case "1":
                        groups.Add(new Group(1, value));
                        break;
                    default:
                        string[] colour = value.Split(',');
                        groups.Add(Whole(62, WholeValue(colour[0])));

                        if (colour[1] != "-16777216")
                        {
                            groups.Add(Whole(420, WholeValue(colour[1])));
                        }

                        break;
                }

                groups.Add(Whole(176, WholeValue(set)));
            }

            return groups;
        }

        /// <summary>Takes the extension dictionary of a style, which holds the properties of R2013 on in R2010 and before, out of the record, and what it owns with it.</summary>
        private void ForgetDictionary(Record record)
        {
            int at = record.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at < 0 || at + 2 >= record.Groups.Count || record.Groups[at + 1].Code != 360)
            {
                return;
            }

            _owning.Add(record.Groups[at + 1].Value.Trim());
            record.Groups.RemoveRange(at, 3);
        }

        #endregion
    }
}
