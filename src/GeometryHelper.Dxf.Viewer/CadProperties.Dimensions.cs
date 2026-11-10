using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The rows of a dimension, as AutoCAD's palette heads them: Misc, Lines &amp; Arrows, Text, Fit and Primary Units.</summary>
    public static partial class CadProperties
    {
        private static readonly string[] OnOffChoices = { "On", "Off" };
        private static readonly string[] VerticalChoices = { "Centered", "Above", "Outside", "JIS", "Below" };
        private static readonly string[] HorizontalChoices = { "Centered", "At Ext Line 1", "At Ext Line 2", "Over Ext Line 1", "Over Ext Line 2" };
        private static readonly string[] FitChoices = { "Both text and arrows", "Arrows", "Text", "Best fit" };
        private static readonly string[] MovementChoices = { "Move dim line with text", "Move text, add leader", "Move text, no leader" };
        private static readonly string[] UnitChoices = { "Scientific", "Decimal", "Engineering", "Architectural", "Fractional", "Windows desktop" };

        /// <summary>Reads a dimension's row as text.</summary>
        internal delegate string DimensionRead(DxfDimension dimension, Func<double, string> angle);

        /// <summary>Works out how a value typed into a dimension's row is written: null and the writer, or the reason it cannot be.</summary>
        internal delegate string DimensionParse(DxfDatabase database, string text, out Action<DxfDimension> write);

        /// <summary>One row of a dimension: its group, label, how it is read and written, and the values it offers.</summary>
        internal sealed class DimensionRow
        {
            public DimensionRow(string group, string label, DimensionRead read, DimensionParse parse, IReadOnlyList<string> choices = null)
            {
                Group = group;
                Label = label;
                Read = read;
                Parse = parse;
                Choices = choices;
            }

            public string Group { get; }

            public string Label { get; }

            public DimensionRead Read { get; }

            /// <summary>Null for a row that is read only.</summary>
            public DimensionParse Parse { get; }

            public IReadOnlyList<string> Choices { get; }
        }

        /// <summary>
        /// The rows of a dimension the palette shows and the Dimension Style dialog's tabs hold, each read from the dimension's
        /// override of a variable, else its style's, and written as an override of it (<see cref="CadDimStyleEditor.Override"/>).
        /// </summary>
        internal static IReadOnlyList<DimensionRow> DimensionRows => LazyRows.Value;

        // Made on first use, after the lists of choices of both files of the class are.
        private static readonly Lazy<DimensionRow[]> LazyRows = new Lazy<DimensionRow[]>(() => new[]
        {
            new DimensionRow("Misc", "Dim style", (d, a) => d.DimensionStyleName, ParseStyle),

            new DimensionRow("Lines & Arrows", "Arrow 1", (d, a) => CadArrowheads.NameOf(d.Dimsah ? d.Dimblk1 : d.Dimblk), (DxfDatabase db, string t, out Action<DxfDimension> w) => ParseArrow(db, t, true, out w), CadArrowheads.Names),
            new DimensionRow("Lines & Arrows", "Arrow 2", (d, a) => CadArrowheads.NameOf(d.Dimsah ? d.Dimblk2 : d.Dimblk), (DxfDatabase db, string t, out Action<DxfDimension> w) => ParseArrow(db, t, false, out w), CadArrowheads.Names),
            Distance("Lines & Arrows", "Arrow size", d => d.Dimasz, (d, v) => d.Dimasz = v),
            new DimensionRow("Lines & Arrows", "Dim line lineweight", (d, a) => LineWeightText(d.Dimlwd), (DxfDatabase db, string t, out Action<DxfDimension> w) => Weight(t, (d, v) => d.Dimlwd = v, out w)),
            new DimensionRow("Lines & Arrows", "Ext line lineweight", (d, a) => LineWeightText(d.Dimlwe), (DxfDatabase db, string t, out Action<DxfDimension> w) => Weight(t, (d, v) => d.Dimlwe = v, out w)),
            Switch("Lines & Arrows", "Dim line 1", d => !d.Dimsd1, (d, v) => d.Dimsd1 = !v),
            Switch("Lines & Arrows", "Dim line 2", d => !d.Dimsd2, (d, v) => d.Dimsd2 = !v),
            Colour("Lines & Arrows", "Dim line color", d => d.Dimclrd, (d, v) => d.Dimclrd = v),
            Linetype("Lines & Arrows", "Dim line linetype", d => d.Dimltype, (d, v) => d.Dimltype = v),
            Distance("Lines & Arrows", "Dim line ext", d => d.Dimdle, (d, v) => d.Dimdle = v),
            Linetype("Lines & Arrows", "Ext line 1 linetype", d => d.Dimltex1, (d, v) => d.Dimltex1 = v),
            Linetype("Lines & Arrows", "Ext line 2 linetype", d => d.Dimltex2, (d, v) => d.Dimltex2 = v),
            Switch("Lines & Arrows", "Ext line 1", d => !d.Dimse1, (d, v) => d.Dimse1 = !v),
            Switch("Lines & Arrows", "Ext line 2", d => !d.Dimse2, (d, v) => d.Dimse2 = !v),
            Colour("Lines & Arrows", "Ext line color", d => d.Dimclre, (d, v) => d.Dimclre = v),
            Distance("Lines & Arrows", "Ext line ext", d => d.Dimexe, (d, v) => d.Dimexe = v),
            Distance("Lines & Arrows", "Ext line offset", d => d.Dimexo, (d, v) => d.Dimexo = v),

            Colour("Text", "Text color", d => d.Dimclrt, (d, v) => d.Dimclrt = v),
            new DimensionRow("Text", "Text height", (d, a) => Length(d.Dimtxt), (DxfDatabase db, string t, out Action<DxfDimension> w) => Real(t, true, true, (d, v) => d.Dimtxt = v, out w, "Text height")),
            new DimensionRow("Text", "Text offset", (d, a) => Length(d.Dimgap), (DxfDatabase db, string t, out Action<DxfDimension> w) => Real(t, false, false, (d, v) => d.Dimgap = v, out w, "Text offset")),
            Switch("Text", "Text outside align", d => d.Dimtoh, (d, v) => d.Dimtoh = v),
            Choice("Text", "Text pos hor", HorizontalChoices, 0, d => d.Dimjust, (d, v) => d.Dimjust = v),
            Choice("Text", "Text pos vert", VerticalChoices, 0, d => d.Dimtad, (d, v) => d.Dimtad = v),
            new DimensionRow("Text", "Text style", (d, a) => d.TextStyle?.Name ?? "Standard", ParseTextStyle),
            Switch("Text", "Text inside align", d => d.Dimtih, (d, v) => d.Dimtih = v),
            new DimensionRow("Text", "Text rotation", (d, a) => a(d.TextRotation), (DxfDatabase db, string t, out Action<DxfDimension> w) => Real(t, false, false, (d, v) => d.TextRotation = Turn(v), out w, "Text rotation")),
            new DimensionRow("Text", "Measurement", (d, a) => IsAngular(d) ? a(d.Measurement) : Length(d.Measurement), null),

            Switch("Fit", "Dim line forced", d => d.Dimtofl, (d, v) => d.Dimtofl = v),
            Switch("Fit", "Dim line inside", d => d.Dimsoxd, (d, v) => d.Dimsoxd = v),
            Distance("Fit", "Dim scale overall", d => d.Dimscale, (d, v) => d.Dimscale = v),
            Choice("Fit", "Fit", FitChoices, 0, d => d.Dimatfit, (d, v) => d.Dimatfit = v),
            Switch("Fit", "Text inside", d => d.Dimtix, (d, v) => d.Dimtix = v),
            Choice("Fit", "Text movement", MovementChoices, 0, d => d.Dimtmove, (d, v) => d.Dimtmove = v),

            new DimensionRow("Primary Units", "Decimal separator", (d, a) => d.Dimdsep == ' ' ? "Space" : d.Dimdsep.ToString(), ParseSeparator),
            new DimensionRow("Primary Units", "Dim prefix", (d, a) => Prefix(d.Dimpost), (DxfDatabase db, string t, out Action<DxfDimension> w) => Post(t, true, out w)),
            new DimensionRow("Primary Units", "Dim suffix", (d, a) => Suffix(d.Dimpost), (DxfDatabase db, string t, out Action<DxfDimension> w) => Post(t, false, out w)),
            Distance("Primary Units", "Dim roundoff", d => d.Dimrnd, (d, v) => d.Dimrnd = v),
            new DimensionRow("Primary Units", "Dim scale linear", (d, a) => Length(d.Dimlfac), (DxfDatabase db, string t, out Action<DxfDimension> w) => Real(t, true, true, (d, v) => d.Dimlfac = v, out w, "Dim scale linear")),
            Choice("Primary Units", "Dim units", UnitChoices, 1, d => d.Dimlunit, (d, v) => d.Dimlunit = v),
            Switch("Primary Units", "Suppress leading zeros", d => (d.Dimzin & 4) != 0, (d, v) => d.Dimzin = v ? d.Dimzin | 4 : d.Dimzin & ~4, true),
            Switch("Primary Units", "Suppress trailing zeros", d => (d.Dimzin & 8) != 0, (d, v) => d.Dimzin = v ? d.Dimzin | 8 : d.Dimzin & ~8, true),
            new DimensionRow("Primary Units", "Precision", (d, a) => PrecisionText(IsAngular(d) ? d.Dimadec : d.Dimdec), ParsePrecision, PrecisionChoices()),
        });

        /// <summary>The groups of a dimension: those of <see cref="DimensionRows"/> and its text override, in AutoCAD's order.</summary>
        private static IEnumerable<CadPropertyGroup> DimensionGroups(DxfDimension dimension, Formats f)
        {
            var groups = new List<CadPropertyGroup>();
            var rows = new List<CadPropertyRow>();
            string title = null;

            foreach (DimensionRow row in DimensionRows)
            {
                if (row.Group != title && rows.Count > 0)
                {
                    groups.Add(new CadPropertyGroup(title, rows));
                    rows = new List<CadPropertyRow>();
                }

                title = row.Group;
                IReadOnlyList<string> choices = row.Label == "Dim style" ? CadDimStyleEditor.Styles(f.Database).Select(s => s.Name).ToList()
                    : row.Label == "Text style" ? TextStyles(f.Database) : row.Choices;
                rows.Add(new CadPropertyRow(row.Label, Read(() => row.Read(dimension, f.Plain), string.Empty), row.Parse == null, choices));

                if (row.Label == "Measurement")
                {
                    rows.Add(new CadPropertyRow("Text override", Read(() => dimension.DimensionText, string.Empty), false));
                }
            }

            groups.Add(new CadPropertyGroup(title, rows));
            return groups;
        }

        private static DimensionRow Distance(string group, string label, Func<DxfDimension, double> get, Action<DxfDimension, double> set)
            => new DimensionRow(group, label, (d, a) => Length(get(d)), (DxfDatabase db, string t, out Action<DxfDimension> w) => Real(t, false, true, set, out w, label));

        private static DimensionRow Switch(string group, string label, Func<DxfDimension, bool> get, Action<DxfDimension, bool> set, bool yesNo = false)
        {
            string[] words = yesNo ? YesNoChoices : OnOffChoices;
            return new DimensionRow(
                group,
                label,
                (d, a) => get(d) ? words[0] : words[1],
                (DxfDatabase db, string t, out Action<DxfDimension> w) =>
                {
                    bool on = string.Equals(t, words[0], StringComparison.OrdinalIgnoreCase);
                    w = d => set(d, on);
                    return on || string.Equals(t, words[1], StringComparison.OrdinalIgnoreCase) ? null : label + " is " + words[0] + " or " + words[1] + ".";
                },
                words);
        }

        private static DimensionRow Choice(string group, string label, string[] words, int first, Func<DxfDimension, int> get, Action<DxfDimension, int> set)
            => new DimensionRow(
                group,
                label,
                (d, a) =>
                {
                    int index = get(d) - first;
                    return index >= 0 && index < words.Length ? words[index] : get(d).ToString(CultureInfo.InvariantCulture);
                },
                (DxfDatabase db, string t, out Action<DxfDimension> w) =>
                {
                    int index = Array.FindIndex(words, x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase));
                    w = d => set(d, index + first);
                    return index >= 0 ? null : label + " is one of " + string.Join(", ", words) + ".";
                },
                words);

        private static DimensionRow Colour(string group, string label, Func<DxfDimension, DxfColor> get, Action<DxfDimension, DxfColor> set)
            => new DimensionRow(
                group,
                label,
                (d, a) => ColorText(get(d)),
                (DxfDatabase db, string t, out Action<DxfDimension> w) =>
                {
                    string error = CadPropertyEditor.ParseColor(t, out DxfColor color);
                    w = d => set(d, color);
                    return error;
                });

        private static DimensionRow Linetype(string group, string label, Func<DxfDimension, DxfLinetypeTableRecord> get, Action<DxfDimension, DxfLinetypeTableRecord> set)
            => new DimensionRow(
                group,
                label,
                (d, a) => get(d)?.Name ?? "ByBlock",
                (DxfDatabase db, string t, out Action<DxfDimension> w) =>
                {
                    w = null;

                    if (t.Length == 0 || !db.LinetypeTable.Has(t))
                    {
                        return "The drawing has no linetype \"" + t + "\".";
                    }

                    string name = db.LinetypeTable[t].Name;
                    w = d => set(d, db.LinetypeTable[name]);
                    return null;
                });

        private static string Real(string text, bool positive, bool notNegative, Action<DxfDimension, double> set, out Action<DxfDimension> write, string label)
        {
            bool number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsNaN(value) && !double.IsInfinity(value);
            write = d => set(d, value);

            if (!number)
            {
                return label + " is a number.";
            }

            if (positive && !(value > 0.0))
            {
                return label + " is a number above nought.";
            }

            return notNegative && value < 0.0 ? label + " is a number, nought or more." : null;
        }

        private static string Weight(string text, Action<DxfDimension, DxfLineWeight> set, out Action<DxfDimension> write)
        {
            string error = CadPropertyEditor.ParseLineWeight(text, out DxfLineWeight weight);
            write = d => set(d, weight);
            return error;
        }

        private static string ParseStyle(DxfDatabase db, string text, out Action<DxfDimension> write)
        {
            write = null;

            if (text.Length == 0 || !db.DimStyleTable.Has(text))
            {
                return "Dimension style \"" + text + "\" not found.";
            }

            string name = db.DimStyleTable[text].Name;
            write = d => d.DimensionStyleName = name;
            return null;
        }

        private static string ParseTextStyle(DxfDatabase db, string text, out Action<DxfDimension> write)
        {
            write = null;

            if (text.Length == 0 || !db.TextStyleTable.Has(text))
            {
                return "The drawing has no text style \"" + text + "\".";
            }

            string name = db.TextStyleTable[text].Name;
            write = d => d.TextStyle = db.TextStyleTable[name];
            return null;
        }

        /// <summary>
        /// An arrowhead of one end, as the palette sets it: the arrows made separate (DIMSAH), the other end keeping the arrow it
        /// showed, as AutoCAD sets them.
        /// </summary>
        private static string ParseArrow(DxfDatabase db, string text, bool first, out Action<DxfDimension> write)
        {
            write = null;
            string name = CadArrowheads.Names.FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));

            if (name == null && text.Length > 0 && !text.StartsWith("*", StringComparison.Ordinal) && db.BlockTable.Has(text))
            {
                name = db.BlockTable[text].Name;
            }

            if (name == null)
            {
                return "The drawing has no arrowhead \"" + text + "\".";
            }

            write = d =>
            {
                string other = CadArrowheads.NameOf(d.Dimsah ? (first ? d.Dimblk2 : d.Dimblk1) : d.Dimblk);
                DxfBlockTableRecord block = CadArrowheads.Ensure(db, name);
                DxfBlockTableRecord kept = CadArrowheads.Ensure(db, other);
                d.Dimsah = true;
                d.Dimblk1 = first ? block : kept;
                d.Dimblk2 = first ? kept : block;
            };
            return null;
        }

        private static string ParseSeparator(DxfDatabase db, string text, out Action<DxfDimension> write)
        {
            char separator = string.Equals(text, "Space", StringComparison.OrdinalIgnoreCase) ? ' ' : text.Length == 1 ? text[0] : '\0';
            write = d => d.Dimdsep = separator;
            return separator == '.' || separator == ',' || separator == ' ' ? null : "Decimal separator is a period (.), a comma (,) or Space.";
        }

        private static string Prefix(string post)
        {
            int at = (post ?? string.Empty).IndexOf("<>", StringComparison.Ordinal);
            return at < 0 ? string.Empty : post.Substring(0, at);
        }

        private static string Suffix(string post)
        {
            post = post ?? string.Empty;
            int at = post.IndexOf("<>", StringComparison.Ordinal);
            return at < 0 ? post : post.Substring(at + 2);
        }

        /// <summary>A prefix or suffix written into DIMPOST about its &lt;&gt;, as the palette writes them: "Ø" and " TYP" make "Ø&lt;&gt; TYP".</summary>
        private static string Post(string text, bool prefix, out Action<DxfDimension> write)
        {
            write = d =>
            {
                string before = prefix ? text : Prefix(d.Dimpost);
                string after = prefix ? Suffix(d.Dimpost) : text;
                d.Dimpost = before.Length == 0 ? after : before + "<>" + after;
            };
            return text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0 ? "A prefix or suffix is on one line." : null;
        }

        private static string PrecisionText(int places) => places <= 0 ? "0" : "0." + new string('0', Math.Min(8, places));

        private static IReadOnlyList<string> PrecisionChoices()
        {
            var choices = new List<string>();

            for (int i = 0; i <= 8; i++)
            {
                choices.Add(PrecisionText(i));
            }

            return choices;
        }

        private static string ParsePrecision(DxfDatabase db, string text, out Action<DxfDimension> write)
        {
            int places = text == "0" ? 0 : text.StartsWith("0.", StringComparison.Ordinal) && text.Substring(2).All(c => c == '0') ? text.Length - 2
                : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int typed) ? typed : -1;
            write = d =>
            {
                if (IsAngular(d))
                {
                    d.Dimadec = places;
                }
                else
                {
                    d.Dimdec = places;
                }
            };
            return places >= 0 && places <= 8 ? null : "Precision is 0 to 8 places, as 0.00.";
        }

        private static double Turn(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            radians -= Math.Floor(radians / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return radians >= 2.0 * Math.PI ? 0.0 : radians;
        }
    }
}
