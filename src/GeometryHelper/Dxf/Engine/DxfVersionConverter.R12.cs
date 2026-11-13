using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The conversion to R12 (AC1009), as AutoCAD 2021 writes one drawing as R12: four sections only; the header variables R12
    /// has; the tables of R12 in its order, with no handles and no block records, names in capitals; blocks without handles
    /// but their end's, the spaces as <c>$MODEL_SPACE</c> and <c>$PAPER_SPACE</c>; the entities with the groups R12 has, no
    /// subclass markers, owners or reactors; and what R12 lacks broken into what it holds (<see cref="DxfR12Explosion"/>).
    /// </summary>
    internal sealed partial class DxfVersionConverter
    {
        private DxfR12Explosion.Plan _plan;

        // What the entities broken were, by kind.
        private readonly SortedDictionary<string, int> _broken = new SortedDictionary<string, int>(
            StringComparer.Ordinal
        );

        // The names of the blocks by the handles of their block records, which R2007 and later name an arrowhead by.
        private Dictionary<string, string> _blockNames = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );

        // The blocks made for what stood in a space or a block, each with the INSERT that stands for it: AutoCAD numbers them
        // by the handle of the entity they were made of, once the drawing is through.
        private readonly List<MadeBlock> _made = new List<MadeBlock>();

        private sealed class MadeBlock
        {
            internal MadeBlock(ulong key, int order, char letter, Record insert, List<Record> content)
            {
                Key = key;
                Order = order;
                Letter = letter;
                Insert = insert;
                Content = content;
            }

            /// <summary>The handle of the entity the block was made of, as a number.</summary>
            internal ulong Key { get; }

            /// <summary>Where the entity stood among the others, to settle two with one handle.</summary>
            internal int Order { get; }

            /// <summary>The letter of the block's name: <c>X</c> for a hatch of lines, <c>U</c> for the rest.</summary>
            internal char Letter { get; }

            /// <summary>The INSERT of the block, which names it once it is numbered.</summary>
            internal Record Insert { get; }

            /// <summary>The block's records, from its BLOCK to its ENDBLK.</summary>
            internal List<Record> Content { get; }
        }

        // Whether an attribute was written with its flags as the extended data of AcDbAttr, which the table of applications then holds.
        private bool _attributeApplication;

        #region The header

        // The variables of R12 in the order AutoCAD writes them, and the value of those a later version has not.
        private static readonly string[] R12Variables = (
            "$ACADVER $INSBASE $EXTMIN $EXTMAX $LIMMIN $LIMMAX $ORTHOMODE $REGENMODE $FILLMODE $QTEXTMODE $MIRRTEXT $DRAGMODE"
            + " $LTSCALE $OSMODE $ATTMODE $TEXTSIZE $TRACEWID $TEXTSTYLE $CLAYER $CELTYPE $CECOLOR $DIMSCALE $DIMASZ $DIMEXO"
            + " $DIMDLI $DIMRND $DIMDLE $DIMEXE $DIMTP $DIMTM $DIMTXT $DIMCEN $DIMTSZ $DIMTOL $DIMLIM $DIMTIH $DIMTOH $DIMSE1"
            + " $DIMSE2 $DIMTAD $DIMZIN $DIMBLK $DIMASO $DIMSHO $DIMPOST $DIMAPOST $DIMALT $DIMALTD $DIMALTF $DIMLFAC $DIMTOFL"
            + " $DIMTVP $DIMTIX $DIMSOXD $DIMSAH $DIMBLK1 $DIMBLK2 $DIMSTYLE $DIMCLRD $DIMCLRE $DIMCLRT $DIMTFAC $DIMGAP"
            + " $LUNITS $LUPREC $SKETCHINC $FILLETRAD $AUNITS $AUPREC $MENU $ELEVATION $PELEVATION $THICKNESS $LIMCHECK"
            + " $BLIPMODE $CHAMFERA $CHAMFERB $SKPOLY $TDCREATE $TDUPDATE $TDINDWG $TDUSRTIMER $USRTIMER $ANGBASE $ANGDIR"
            + " $PDMODE $PDSIZE $PLINEWID $COORDS $SPLFRAME $SPLINETYPE $SPLINESEGS $ATTDIA $ATTREQ $HANDLING $HANDSEED"
            + " $SURFTAB1 $SURFTAB2 $SURFTYPE $SURFU $SURFV $UCSNAME $UCSORG $UCSXDIR $UCSYDIR $PUCSNAME $PUCSORG $PUCSXDIR"
            + " $PUCSYDIR $USERI1 $USERI2 $USERI3 $USERI4 $USERI5 $USERR1 $USERR2 $USERR3 $USERR4 $USERR5 $WORLDVIEW"
            + " $SHADEDGE $SHADEDIF $TILEMODE $MAXACTVP $PLIMCHECK $PEXTMIN $PEXTMAX $PLIMMIN $PLIMMAX $UNITMODE $VISRETAIN"
            + " $PLINEGEN $PSLTSCALE"
        ).Split(' ');

        // The variables R12 has and R2000 has not, with the value AutoCAD gives a new drawing: HANDLING is always on, as R12
        // keeps its handles only so.
        private static readonly Dictionary<string, int> R12Added = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["$DRAGMODE"] = 2,
            ["$OSMODE"] = 0,
            ["$BLIPMODE"] = 0,
            ["$COORDS"] = 1,
            ["$ATTDIA"] = 0,
            ["$ATTREQ"] = 1,
            ["$HANDLING"] = 1,
        };

        // The variables whose value is the name of a symbol: the group of the name, which R12 writes in capitals.
        private static readonly Dictionary<string, int> R12Named = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["$TEXTSTYLE"] = 7,
            ["$CLAYER"] = 8,
            ["$CELTYPE"] = 6,
            ["$DIMSTYLE"] = 2,
            ["$DIMBLK"] = 1,
            ["$DIMBLK1"] = 1,
            ["$DIMBLK2"] = 1,
            ["$UCSNAME"] = 2,
            ["$PUCSNAME"] = 2,
        };

        // The kind of symbol each of those variables names.
        private static readonly Dictionary<string, string> R12NamedKind = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["$TEXTSTYLE"] = "STYLE",
            ["$CLAYER"] = "LAYER",
            ["$CELTYPE"] = "LTYPE",
            ["$DIMSTYLE"] = "DIMSTYLE",
            ["$DIMBLK"] = "BLOCK",
            ["$DIMBLK1"] = "BLOCK",
            ["$DIMBLK2"] = "BLOCK",
            ["$UCSNAME"] = "UCS",
            ["$PUCSNAME"] = "UCS",
        };

        /// <summary>The header of R12: the variables it has, in its order, those a later version has not left out.</summary>
        private void R12Header(Section header)
        {
            var source = new Dictionary<string, Record>(StringComparer.Ordinal);

            foreach (Record record in header.Records)
            {
                source[record.Groups[0].Value] = record;
            }

            var made = new List<Record>(R12Variables.Length);

            foreach (string name in R12Variables)
            {
                if (name == "$ACADVER")
                {
                    var version = new Record();
                    version.Groups.Add(new Group(9, name));
                    version.Groups.Add(new Group(1, DxfVersions.Name(_target)));
                    made.Add(version);
                    continue;
                }

                if (name == "$HANDLING" || !source.TryGetValue(name, out Record record))
                {
                    if (R12Added.TryGetValue(name, out int value))
                    {
                        var added = new Record();
                        added.Groups.Add(new Group(9, name));
                        added.Groups.Add(Whole(70, value));
                        made.Add(added);
                    }

                    continue;
                }

                if (R12Named.TryGetValue(name, out int code))
                {
                    for (int i = 1; i < record.Groups.Count; i++)
                    {
                        if (record.Groups[i].Code == code)
                        {
                            record.Groups[i] = new Group(code, R12Name(record.Groups[i].Value, R12NamedKind[name]));
                        }
                    }
                }

                made.Add(record);
            }

            header.Records.Clear();
            header.Records.AddRange(made);
        }

        #endregion

        #region The groups

        private static HashSet<int> Codes(params object[] parts)
        {
            var codes = new HashSet<int>();

            foreach (object part in parts)
            {
                if (part is int single)
                {
                    codes.Add(single);
                }
                else
                {
                    foreach (string word in ((string)part).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        codes.Add(int.Parse(word, CultureInfo.InvariantCulture));
                    }
                }
            }

            return codes;
        }

        // What every entity has in R12: its handle, linetype, layer, thickness, colour, space and extrusion.
        private const string R12Common = "5 6 8 39 62 67 210 220 230";

        private const string R12Text = "1 10 20 30 40 41 50 51 7 71 72 11 21 31 73";

        // The groups of each kind of record in R12, apart from the extended data (1000 and on), which every record keeps.
        private static readonly Dictionary<string, HashSet<int>> R12Groups = new Dictionary<string, HashSet<int>>(
            StringComparer.Ordinal
        )
        {
            ["LINE"] = Codes(R12Common, "10 20 30 11 21 31"),
            ["POINT"] = Codes(R12Common, "10 20 30 50"),
            ["CIRCLE"] = Codes(R12Common, "10 20 30 40"),
            ["ARC"] = Codes(R12Common, "10 20 30 40 50 51"),
            ["TRACE"] = Codes(R12Common, "10 20 30 11 21 31 12 22 32 13 23 33"),
            ["SOLID"] = Codes(R12Common, "10 20 30 11 21 31 12 22 32 13 23 33"),
            ["3DFACE"] = Codes(R12Common, "10 20 30 11 21 31 12 22 32 13 23 33 70"),
            ["TEXT"] = Codes(R12Common, R12Text),
            ["ATTDEF"] = Codes(R12Common, R12Text, "2 3 70 74"),
            ["ATTRIB"] = Codes(R12Common, R12Text, "2 70 74"),
            ["SHAPE"] = Codes(R12Common, "2 10 20 30 40 50 41 51"),
            ["INSERT"] = Codes(R12Common, "66 2 10 20 30 41 42 43 50 70 71 44 45"),
            ["POLYLINE"] = Codes(R12Common, "66 10 20 30 40 41 70 71 72 73 74 75"),
            ["VERTEX"] = Codes(R12Common, "10 20 30 40 41 42 50 70 71 72 73 74"),
            ["SEQEND"] = Codes(R12Common),
            ["DIMENSION"] = Codes(
                R12Common,
                "1 2 10 20 30 11 21 31 12 22 32 13 23 33 14 24 34 15 25 35 16 26 36 40 50 51 52 53 70"
            ),
            ["BLOCK"] = Codes("8 2 70 10 20 30 3 1 67 62"),
            ["ENDBLK"] = Codes(R12Common),
            ["VPORT"] = Codes(
                "2 10 11 12 13 14 15 16 17 20 21 22 23 24 25 26 27 36 37 40 41 42 43 44 50 51 70 71 72 73 74 75 76 77 78"
            ),
            ["LTYPE"] = Codes("2 3 40 49 70 72 73"),
            ["LAYER"] = Codes("2 6 62 70"),
            ["STYLE"] = Codes("2 3 4 40 41 42 50 70 71"),
            ["VIEW"] = Codes("2 10 11 12 20 21 22 31 32 40 41 42 43 44 50 70 71"),
            ["UCS"] = Codes("2 70 10 20 30 11 21 31 12 22 32"),
            ["APPID"] = Codes("2 70"),
            ["DIMSTYLE"] = Codes(
                "2 3 4 5 6 7 40 41 42 43 44 45 46 47 48 70 71 72 73 74 75 76 77 78 140 141 142 143 144 145 146 147 170 171 172 173 174 175 176 177 178"
            ),
        };

        // The records of R12 that have no extended data.
        private static readonly HashSet<string> R12NoExtendedData = new HashSet<string>(StringComparer.Ordinal)
        {
            "BLOCK",
            "ENDBLK",
            "VPORT",
            "LTYPE",
            "LAYER",
            "STYLE",
            "VIEW",
            "UCS",
            "APPID",
            "DIMSTYLE",
        };

        // The tables of R12, in the order AutoCAD writes them.
        private static readonly string[] R12Tables =
        {
            "VPORT",
            "LTYPE",
            "LAYER",
            "STYLE",
            "VIEW",
            "UCS",
            "APPID",
            "DIMSTYLE",
        };

        // The groups that name a symbol in each record: R12 writes names in capitals.
        private static bool IsName(string type, int code) =>
            code == 8
            || code == 6
            || code == 7
            || (code == 2 && type != "SHAPE" && type != "ATTDEF" && type != "ATTRIB")
            || (code == 3 && type == "BLOCK");

        /// <summary>The longest name of a symbol R12 holds, as AutoCAD 2021 cuts it.</summary>
        private const int R12NameLength = 26;

        /// <summary>
        /// A symbol's name as AutoCAD 2021 writes it into R12: in capitals, a character that is no letter, digit, <c>$</c>, <c>-</c> or
        /// <c>_</c> made an underscore (a <c>*</c> first, as an anonymous block has it, kept), the name cut to 26 characters:
        /// <c>{18b73a54-fa43-428c-adcb-c1c7bbdf2c84}</c> is <c>_18B73A54-FA43-428C-ADCB-C</c> and <c>With space</c> is
        /// <c>WITH_SPACE</c>. A name R12 does not allow makes AutoCAD discard the drawing: "Improper table entry name".
        /// </summary>
        private static string R12Name(string name)
        {
            var made = new StringBuilder(Math.Min(name.Length, R12NameLength));

            for (int i = 0; i < name.Length && made.Length < R12NameLength; i++)
            {
                char c = name[i];
                bool keep =
                    (c <= 0xFF && char.IsLetterOrDigit(c)) || c == '$' || c == '-' || c == '_' || (c == '*' && i == 0);
                made.Append(keep ? char.ToUpperInvariant(c) : '_');
            }

            return made.ToString();
        }

        // The names of each kind of symbol (LAYER, LTYPE, STYLE, VIEW, UCS, DIMSTYLE, BLOCK) that R12 writes other than as
        // R12Name makes them, by the name the drawing has: two names that come to one are told apart.
        private readonly Dictionary<string, Dictionary<string, string>> _renamed = new Dictionary<
            string,
            Dictionary<string, string>
        >(StringComparer.Ordinal);

        /// <summary>
        /// A symbol's name as R12 writes it where two come to one: a name that cleaning changed (<c>A{1}</c> is <c>A_1_</c>) while
        /// another of the same kind is that name already (<c>A_1_</c>) takes a number after it, as AutoCAD 2021 gives one: <c>A_1_0</c>,
        /// and the next <c>A_1_1</c>. A name that cleaning leaves but for its capitals keeps its own.
        /// </summary>
        private void MakeNamesUnique(Section tables, Section blocks)
        {
            var names = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string kind in new[] { "LAYER", "LTYPE", "STYLE", "VIEW", "UCS", "DIMSTYLE" })
            {
                names[kind] =
                    tables == null
                        ? new List<string>()
                        : tables.Records.Where(r => r.Type == kind).Select(Name).Where(n => n != null).ToList();
            }

            names["BLOCK"] =
                blocks == null
                    ? new List<string>()
                    : blocks.Records.Where(r => r.Type == "BLOCK").Select(Name).Where(n => n != null).ToList();

            foreach (KeyValuePair<string, List<string>> kind in names)
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // The names cleaning leaves are kept first.
                foreach (string name in kind.Value)
                {
                    if (string.Equals(R12Name(name), name, StringComparison.OrdinalIgnoreCase))
                    {
                        used.Add(R12Name(name));
                    }
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (string name in kind.Value)
                {
                    string cleaned = R12Name(name);

                    if (string.Equals(cleaned, name, StringComparison.OrdinalIgnoreCase) || map.ContainsKey(name))
                    {
                        continue;
                    }

                    string candidate = cleaned;

                    for (int number = 0; !used.Add(candidate); number++)
                    {
                        string suffix = number.ToString(CultureInfo.InvariantCulture);
                        candidate =
                            cleaned.Substring(0, Math.Min(cleaned.Length, R12NameLength - suffix.Length)) + suffix;
                    }

                    if (!string.Equals(candidate, cleaned, StringComparison.Ordinal))
                    {
                        map[name] = candidate;
                    }
                }

                if (map.Count > 0)
                {
                    _renamed[kind.Key] = map;
                }
            }
        }

        /// <summary>A symbol's name of a kind as R12 writes it: as <see cref="R12Name(string)"/>, but told apart from another that comes to the same.</summary>
        private string R12Name(string name, string kind) =>
            kind != null
            && _renamed.TryGetValue(kind, out Dictionary<string, string> map)
            && map.TryGetValue(name, out string unique)
                ? unique
                : R12Name(name);

        /// <summary>The kind of symbol a group names in a record: LAYER for 8, LTYPE for 6, STYLE for 7, and for 2 and 3 that of the record; null for none.</summary>
        private static string NameKind(string type, int code)
        {
            switch (code)
            {
                case 8:
                    return "LAYER";
                case 6:
                case 7:
                    return type == "DIMSTYLE" ? "BLOCK"
                        : code == 6 ? "LTYPE"
                        : "STYLE";
                case 2:
                case 3:
                    switch (type)
                    {
                        case "LAYER":
                        case "LTYPE":
                        case "STYLE":
                        case "VIEW":
                        case "UCS":
                        case "DIMSTYLE":
                            return type;
                        case "BLOCK":
                        case "INSERT":
                        case "DIMENSION":
                            return "BLOCK";
                    }

                    return null;
            }

            return null;
        }

        /// <summary>Whether a value is a number written as nought.</summary>
        private static bool IsZero(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            && Math.Abs(number) < 1E-12;

        /// <summary>Whether a record holds the extrusion (0, 0, 1) in its groups 210, 220 and 230, which R12 leaves out as it is the default.</summary>
        private static bool HasPlainNormal(Record record)
        {
            double x = double.NaN;
            double y = double.NaN;
            double z = double.NaN;

            foreach (Group group in record.Groups)
            {
                if (
                    (group.Code == 210 || group.Code == 220 || group.Code == 230)
                    && double.TryParse(group.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                )
                {
                    if (group.Code == 210)
                    {
                        x = value;
                    }
                    else if (group.Code == 220)
                    {
                        y = value;
                    }
                    else
                    {
                        z = value;
                    }
                }
            }

            return Math.Abs(x) < 1E-12 && Math.Abs(y) < 1E-12 && Math.Abs(z - 1.0) < 1E-12;
        }

        /// <summary>A record as R12 holds it: the groups it has, names in capitals; null where R12 has no such record.</summary>
        private Record R12Record(Record record)
        {
            string type = record.Type;

            if (!R12Groups.TryGetValue(type, out HashSet<int> codes))
            {
                return null;
            }

            var made = new Record();
            made.Groups.Add(record.Groups[0]);
            bool embedded = false;
            bool anonymousBlock = false;
            bool own = false;
            var flags = new List<string>(2);
            bool plainNormal = HasPlainNormal(record);

            for (int i = 1; i < record.Groups.Count; i++)
            {
                Group group = record.Groups[i];

                // The embedded object of a multi-line attribute (R2018) repeats the groups of the text: R12 has none of it.
                embedded = embedded || group.Code == 101;

                // An attribute's own subclass (AcDbAttribute, AcDbAttributeDefinition) holds the tag, the prompt, the flags and the
                // vertical justification; what it says of its text beyond them is the multi-line form's (R2018).
                if (group.Code == 100)
                {
                    own = group.Value == "AcDbAttribute" || group.Value == "AcDbAttributeDefinition";
                }

                if (own && group.Code == 280)
                {
                    flags.Add(group.Value.Trim());
                }

                if (
                    own
                    && group.Code < 1000
                    && group.Code != 2
                    && group.Code != 3
                    && group.Code != 70
                    && group.Code != 74
                )
                {
                    continue;
                }

                if (embedded && group.Code < 1000)
                {
                    continue;
                }

                if (group.Code >= 1000)
                {
                    // Only an entity keeps its extended data in R12: a record of a table and a block lose it.
                    if (R12NoExtendedData.Contains(type))
                    {
                        continue;
                    }

                    // The overrides of a dimension name an arrowhead's block by its handle (343, 344, 342) from R2007: R12 by its name (6, 7, 5).
                    if (
                        type == "DIMENSION"
                        && group.Code == 1070
                        && i + 1 < record.Groups.Count
                        && record.Groups[i + 1].Code == 1005
                        && Number(group.Value, out int variable)
                        && variable >= 340
                        && variable <= 348
                    )
                    {
                        int named =
                            variable == 342 ? 5
                            : variable == 343 ? 6
                            : variable == 344 ? 7
                            : 0;

                        if (named != 0 && _blockNames.TryGetValue(record.Groups[i + 1].Value.Trim(), out string block))
                        {
                            made.Groups.Add(Whole(1070, named));
                            made.Groups.Add(new Group(1000, block));
                        }

                        i++;
                        continue;
                    }

                    made.Groups.Add(group);
                    continue;
                }

                // A dimension style names its arrowheads' blocks by handle from R2007 (342, 343, 344): R12 by name (5, 6, 7).
                if (type == "DIMSTYLE" && group.Code >= 342 && group.Code <= 344)
                {
                    if (_blockNames.TryGetValue(group.Value.Trim(), out string arrow))
                    {
                        int at = made.Groups.FindIndex(g => g.Code == group.Code - 337);

                        if (at < 0)
                        {
                            made.Groups.Add(new Group(group.Code - 337, arrow));
                        }
                    }

                    continue;
                }

                if (!codes.Contains(group.Code))
                {
                    continue;
                }

                // What is as the default is as AutoCAD leaves it out: a flag of 0 on a polyline or a vertex, a text turned by 0, and
                // the extrusion (0, 0, 1) of an entity.
                if (
                    (plainNormal && (group.Code == 210 || group.Code == 220 || group.Code == 230))
                    || (
                        group.Code == 6
                        && string.Equals(group.Value.Trim(), "BYLAYER", StringComparison.OrdinalIgnoreCase)
                    )
                    || (group.Code == 62 && group.Value.Trim() == "256")
                    || (
                        ((type == "POLYLINE" || type == "VERTEX") && group.Code == 70)
                        || (type == "TEXT" && group.Code == 50)
                    ) && IsZero(group.Value)
                )
                {
                    continue;
                }

                string value = group.Value;

                if (IsName(type, group.Code))
                {
                    value = type == "APPID" ? value.ToUpperInvariant() : R12Name(value, NameKind(type, group.Code));
                }

                if (type == "BLOCK" && (group.Code == 2 || group.Code == 3))
                {
                    value = SpaceName(record.Groups[i].Value) ?? value;
                }

                // A dynamic block is written as the anonymous block AutoCAD names it, and so are the INSERTs of it.
                if (
                    _plan != null
                    && (
                        (type == "BLOCK" && (group.Code == 2 || group.Code == 3))
                        || (type == "INSERT" && group.Code == 2)
                    )
                    && _plan.Renamed.TryGetValue(group.Value.Trim(), out string anonymous)
                )
                {
                    value = anonymous;
                    anonymousBlock = type == "BLOCK";
                }

                if (type == "STYLE" && group.Code == 3 && IsTrueType(value))
                {
                    value = "txt";
                }

                if (type == "DIMENSION" && group.Code == 70)
                {
                    value = Whole(70, Number(value, out int bits) ? bits & ~32 : 0).Value;
                }

                // A block named anonymous has the flag of one (1), or AutoCAD discards the drawing: "Invalid anonymous block".
                if (anonymousBlock && type == "BLOCK" && group.Code == 70)
                {
                    value = Whole(70, Number(value, out int flagged) ? flagged | 1 : 1).Value;
                }

                made.Groups.Add(new Group(group.Code, value));
            }

            // An attribute keeps its version and whether its position is locked (the two 280 of R2010) as the extended data of AcDbAttr.
            if ((type == "ATTRIB" || type == "ATTDEF") && flags.Count == 2)
            {
                made.Groups.Add(new Group(1001, "AcDbAttr"));
                _attributeApplication = true;
                made.Groups.Add(
                    Whole(
                        1070,
                        int.TryParse(flags[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version)
                            ? version
                            : 0
                    )
                );
                made.Groups.Add(
                    Whole(
                        1070,
                        int.TryParse(flags[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int locked)
                            ? locked
                            : 0
                    )
                );
            }

            return type == "DIMSTYLE" ? Complete(made) : made;
        }

        // The groups of a dimension style of R12 in the order AutoCAD writes them, each with the value of a new drawing's own style
        // (the imperial STANDARD): a style of a later version holds only the values that differ from them.
        private static readonly KeyValuePair<int, string>[] R12DimensionDefaults =
        {
            new KeyValuePair<int, string>(3, string.Empty),
            new KeyValuePair<int, string>(4, string.Empty),
            new KeyValuePair<int, string>(5, string.Empty),
            new KeyValuePair<int, string>(6, string.Empty),
            new KeyValuePair<int, string>(7, string.Empty),
            new KeyValuePair<int, string>(40, "1.0"),
            new KeyValuePair<int, string>(41, "0.18"),
            new KeyValuePair<int, string>(42, "0.0625"),
            new KeyValuePair<int, string>(43, "0.38"),
            new KeyValuePair<int, string>(44, "0.18"),
            new KeyValuePair<int, string>(45, "0.0"),
            new KeyValuePair<int, string>(46, "0.0"),
            new KeyValuePair<int, string>(47, "0.0"),
            new KeyValuePair<int, string>(48, "0.0"),
            new KeyValuePair<int, string>(140, "0.18"),
            new KeyValuePair<int, string>(141, "0.09"),
            new KeyValuePair<int, string>(142, "0.0"),
            new KeyValuePair<int, string>(143, "25.4"),
            new KeyValuePair<int, string>(144, "1.0"),
            new KeyValuePair<int, string>(145, "0.0"),
            new KeyValuePair<int, string>(146, "1.0"),
            new KeyValuePair<int, string>(147, "0.09"),
            new KeyValuePair<int, string>(71, "0"),
            new KeyValuePair<int, string>(72, "0"),
            new KeyValuePair<int, string>(73, "1"),
            new KeyValuePair<int, string>(74, "1"),
            new KeyValuePair<int, string>(75, "0"),
            new KeyValuePair<int, string>(76, "0"),
            new KeyValuePair<int, string>(77, "0"),
            new KeyValuePair<int, string>(78, "0"),
            new KeyValuePair<int, string>(170, "0"),
            new KeyValuePair<int, string>(171, "2"),
            new KeyValuePair<int, string>(172, "0"),
            new KeyValuePair<int, string>(173, "0"),
            new KeyValuePair<int, string>(174, "0"),
            new KeyValuePair<int, string>(175, "0"),
            new KeyValuePair<int, string>(176, "0"),
            new KeyValuePair<int, string>(177, "0"),
            new KeyValuePair<int, string>(178, "0"),
        };

        /// <summary>A dimension style with every group R12 asks of one: its name and flags, then each value as it has it or as a new drawing has it.</summary>
        private Record Complete(Record style)
        {
            var made = new Record();
            made.Groups.Add(style.Groups[0]);

            foreach (int code in new[] { 2, 70 })
            {
                Group found = style.Groups.FirstOrDefault(g => g.Code == code);
                made.Groups.Add(
                    found.Value != null ? found
                    : code == 70 ? Whole(70, 0)
                    : new Group(2, string.Empty)
                );
            }

            foreach (KeyValuePair<int, string> value in R12DimensionDefaults)
            {
                Group found = style.Groups.FirstOrDefault(g => g.Code == value.Key);

                if (found.Value != null)
                {
                    made.Groups.Add(found);
                }
                else
                {
                    bool whole = value.Key >= 70 && value.Key <= 78 || value.Key >= 170 && value.Key <= 178;
                    made.Groups.Add(
                        whole
                            ? Whole(value.Key, int.Parse(value.Value, CultureInfo.InvariantCulture))
                            : new Group(value.Key, value.Value)
                    );
                }
            }

            return made;
        }

        /// <summary>The name R12 gives a space: <c>$MODEL_SPACE</c> and <c>$PAPER_SPACE</c>; null for any other block.</summary>
        private static string SpaceName(string name)
        {
            if (string.Equals(name, "*Model_Space", StringComparison.OrdinalIgnoreCase))
            {
                return "$MODEL_SPACE";
            }

            return string.Equals(name, "*Paper_Space", StringComparison.OrdinalIgnoreCase) ? "$PAPER_SPACE" : null;
        }

        /// <summary>Whether a font file is a TrueType one, which R12 cannot name: AutoCAD writes txt in its place.</summary>
        private static bool IsTrueType(string file)
        {
            string name = file.Trim();
            return name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region The tables

        /// <summary>The tables of R12 in its order, each with the records it holds as R12 holds them, no block records and no handles.</summary>
        private void R12TableSection(Section tables)
        {
            var records = new Dictionary<string, List<Record>>(StringComparer.Ordinal);
            string current = null;

            foreach (Record record in tables.Records)
            {
                switch (record.Type)
                {
                    case "TABLE":
                        current = record.Groups.FirstOrDefault(g => g.Code == 2).Value;

                        if (current != null && !records.ContainsKey(current))
                        {
                            records[current] = new List<Record>();
                        }

                        break;
                    case "ENDTAB":
                        current = null;
                        break;
                    default:
                        if (current != null && Array.IndexOf(R12Tables, current) >= 0)
                        {
                            Record made = R12Record(record);

                            // ByBlock and ByLayer are no linetypes of a table of R12.
                            if (
                                made != null
                                && !(
                                    current == "LTYPE"
                                    && (
                                        string.Equals(Name(made), "BYBLOCK", StringComparison.Ordinal)
                                        || string.Equals(Name(made), "BYLAYER", StringComparison.Ordinal)
                                    )
                                )
                            )
                            {
                                records[current].Add(made);
                            }
                        }

                        break;
                }
            }

            // The extended data of AcDbAttr has its application in the table, or AutoCAD discards the drawing: "Invalid application
            // name in 1001 group". AutoCAD writes it last, as it is.
            if (
                _attributeApplication
                && records.TryGetValue("APPID", out List<Record> applications)
                && !applications.Any(a => string.Equals(Name(a), "AcDbAttr", StringComparison.OrdinalIgnoreCase))
            )
            {
                var application = new Record();
                application.Groups.Add(new Group(0, "APPID"));
                application.Groups.Add(new Group(2, "AcDbAttr"));
                application.Groups.Add(Whole(70, 0));
                applications.Add(application);
            }

            // The application the extended data of a broken multileader names is in the table, as AutoCAD registers it.
            if (
                records.TryGetValue("APPID", out List<Record> listed)
                && !listed.Any(a => string.Equals(Name(a), "ACAD_MLEADERVER", StringComparison.OrdinalIgnoreCase))
            )
            {
                var version = new Record();
                version.Groups.Add(new Group(0, "APPID"));
                version.Groups.Add(new Group(2, "ACAD_MLEADERVER"));
                version.Groups.Add(Whole(70, 0));
                listed.Add(version);
            }

            tables.Records.Clear();

            foreach (string name in R12Tables)
            {
                List<Record> list = records.TryGetValue(name, out List<Record> found) ? found : new List<Record>();
                var head = new Record();
                head.Groups.Add(new Group(0, "TABLE"));
                head.Groups.Add(new Group(2, name));
                head.Groups.Add(Whole(70, list.Count));
                tables.Records.Add(head);
                tables.Records.AddRange(list);
                var end = new Record();
                end.Groups.Add(new Group(0, "ENDTAB"));
                tables.Records.Add(end);
            }
        }

        private static string Name(Record record)
        {
            foreach (Group group in record.Groups)
            {
                if (group.Code == 2)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>Gives a record the name of a block made: its group 2, and for the BLOCK itself its group 3.</summary>
        private static void Rename(Record record, string name)
        {
            for (int i = 0; i < record.Groups.Count; i++)
            {
                Group group = record.Groups[i];

                if (group.Code == 2 || (group.Code == 3 && record.Type == "BLOCK"))
                {
                    record.Groups[i] = new Group(group.Code, name);
                }
            }
        }

        #endregion

        #region The blocks and the entities

        /// <summary>The names of the layers by their handles, for the viewports that freeze layers by handle.</summary>
        private Dictionary<string, string> LayerNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Section tables = Find("TABLES");

            if (tables != null)
            {
                foreach (Record record in tables.Records.Where(r => r.Type == "LAYER"))
                {
                    string handle = HandleOf(record);
                    string name = Name(record);

                    if (handle != null && name != null)
                    {
                        names[handle] = R12Name(name, "LAYER");
                    }
                }
            }

            return names;
        }

        /// <summary>The names of the blocks by the handles of their block records, in capitals.</summary>
        private Dictionary<string, string> BlockNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Section tables = Find("TABLES");

            if (tables != null)
            {
                foreach (Record record in tables.Records.Where(r => r.Type == "BLOCK_RECORD"))
                {
                    string handle = HandleOf(record);
                    string name = Name(record);

                    if (handle != null && name != null)
                    {
                        names[handle] = R12Name(name, "BLOCK");
                    }
                }
            }

            return names;
        }

        /// <summary>The records of the blocks the pieces were made in, by block, and the section without them.</summary>
        private Dictionary<string, List<Record>> TakeScratch(Section blocks, out Dictionary<string, Record> ends)
        {
            var pieces = new Dictionary<string, List<Record>>(StringComparer.OrdinalIgnoreCase);
            ends = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
            var kept = new List<Record>(blocks.Records.Count);
            List<Record> into = null;
            string name = null;

            foreach (Record record in blocks.Records)
            {
                if (record.Type == "BLOCK")
                {
                    name = Name(record);

                    if (name != null && name.StartsWith(DxfR12Explosion.Scratch, StringComparison.OrdinalIgnoreCase))
                    {
                        into = pieces[name] = new List<Record>();
                        continue;
                    }
                }
                else if (record.Type == "ENDBLK" && into != null)
                {
                    ends[name] = record;
                    into = null;
                    continue;
                }

                if (into != null)
                {
                    into.Add(record);
                }
                else
                {
                    kept.Add(record);
                }
            }

            blocks.Records.Clear();
            blocks.Records.AddRange(kept);
            return pieces;
        }

        /// <summary>The highest number of an anonymous block of the drawing, <c>*D3</c>, <c>*U25</c>: the next one made is numbered above it.</summary>
        private static int HighestAnonymous(Section blocks)
        {
            int highest = 0;

            foreach (Record record in blocks.Records.Where(r => r.Type == "BLOCK"))
            {
                string name = Name(record);

                if (
                    name != null
                    && name.Length > 2
                    && name[0] == '*'
                    && char.IsLetter(name[1])
                    && int.TryParse(name.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                )
                {
                    highest = Math.Max(highest, number);
                }
            }

            return highest;
        }

        private void R12Content(
            Section section,
            Dictionary<string, List<Record>> pieces,
            Dictionary<string, Record> ends,
            Dictionary<string, string> layers
        )
        {
            var made = new List<Record>(section.Records.Count);
            bool skipping = false;
            bool paper = false;
            Record owner = null;

            foreach (Record record in section.Records)
            {
                string type = record.Type;
                string handle = HandleOf(record);

                if (skipping)
                {
                    // The block of another layout goes to its end: R12 has the one paper space.
                    skipping = type != "ENDBLK";
                    continue;
                }

                if (type == "BLOCK")
                {
                    string name = Name(record) ?? string.Empty;

                    if (name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase) && SpaceName(name) == null)
                    {
                        skipping = true;
                        continue;
                    }
                }

                if (handle != null && _plan.Left.TryGetValue(handle, out string lost))
                {
                    _left[lost] = _left.TryGetValue(lost, out int count) ? count + 1 : 1;
                    continue;
                }

                if (handle != null && _plan.Pieces.TryGetValue(handle, out DxfR12Explosion.Piece piece))
                {
                    _broken[piece.Type] = _broken.TryGetValue(piece.Type, out int total) ? total + 1 : 1;
                    List<Record> content = pieces[piece.Block];

                    if (piece.Direct)
                    {
                        // A curve made a polyline or a line stands where the entity stood, with its handle.
                        List<Record> parts = R12Parts(content);

                        if (parts.Count > 0)
                        {
                            int at = parts[0].Groups.FindIndex(g => g.Code == 5);

                            if (at >= 0)
                            {
                                parts[0].Groups[at] = new Group(5, handle);
                            }
                        }

                        made.AddRange(parts);
                        continue;
                    }

                    // The rest, in a space or in a block alike, is an INSERT of a block of its pieces, numbered once the drawing is
                    // through.
                    Record insert = Insert(record, null);
                    made.Add(insert);
                    _made.Add(
                        new MadeBlock(
                            ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong key)
                                ? key
                                : ulong.MaxValue,
                            _made.Count,
                            piece.Letter,
                            insert,
                            NewBlock(null, content, ends[piece.Block])
                        )
                    );
                    continue;
                }

                Record one = type == "VIEWPORT" ? Viewport(record, layers) : R12Record(record);

                if (one != null)
                {
                    if (handle != null && _bounds.TryGetValue(handle, out List<string> followers))
                    {
                        AddBounds(one, followers);
                    }

                    if (type == "BLOCK")
                    {
                        paper = Name(one) == "$PAPER_SPACE";
                    }

                    // The block of paper space and its end are in paper space: AutoCAD writes 67 of 1 on both.
                    if ((type == "BLOCK" || type == "ENDBLK") && paper && !one.Groups.Exists(g => g.Code == 67))
                    {
                        one.Groups.Insert(1, new Group(67, "1"));
                    }

                    if (type == "ENDBLK")
                    {
                        paper = false;
                    }

                    // The vertices of a polyline and its end carry its linetype and colour, as AutoCAD writes them: a polyline
                    // made of a lightweight one lacks them, which AUDIT mends ("linetype != owner's").
                    if (type == "POLYLINE")
                    {
                        owner = one;
                    }
                    else if (owner != null && (type == "VERTEX" || type == "SEQEND"))
                    {
                        Inherit(owner, one);
                    }

                    made.Add(one);
                }
            }

            section.Records.Clear();
            section.Records.AddRange(made);
        }

        /// <summary>Gives a vertex or an end of a polyline the linetype (6) and colour (62) the polyline has and it has not, after its layer.</summary>
        private static void Inherit(Record owner, Record part)
        {
            foreach (int code in new[] { 62, 6 })
            {
                Group found = owner.Groups.FirstOrDefault(g => g.Code == code);

                if (found.Value != null && !part.Groups.Exists(g => g.Code == code))
                {
                    int layer = part.Groups.FindIndex(g => g.Code == 8);
                    part.Groups.Insert(layer + 1, found);
                }
            }
        }

        /// <summary>The value of a header variable of the drawing, by its group; null where the header has none.</summary>
        private string HeaderValue(string name, int code)
        {
            Section header = Find("HEADER");
            Record variable = header?.Records.FirstOrDefault(r => r.Groups[0].Value == name);

            if (variable == null)
            {
                return null;
            }

            Group found = variable.Groups.FirstOrDefault(g => g.Code == code);
            return found.Value?.Trim();
        }

        /// <summary>
        /// The INSERT that stands where an entity stood in a space: its handle and space, at the origin, on the drawing's current
        /// layer, in its current colour and linetype where it has them (<c>$CLAYER</c>, <c>$CECOLOR</c>, <c>$CELTYPE</c>), as
        /// AutoCAD writes it; the look of the entity is on its pieces. A hatch's is of the linetype CONTINUOUS.
        /// </summary>
        private Record Insert(Record old, string block)
        {
            var insert = new Record();
            insert.Groups.Add(new Group(0, "INSERT"));
            Group handle = old.Groups.FirstOrDefault(g => g.Code == 5);

            if (handle.Value != null)
            {
                insert.Groups.Add(handle);
            }

            insert.Groups.Add(new Group(8, R12Name(HeaderValue("$CLAYER", 8) ?? "0", "LAYER")));
            string line = HeaderValue("$CELTYPE", 6);

            if (
                old.Type == "HATCH"
                || (line != null && !string.Equals(line, "BYLAYER", StringComparison.OrdinalIgnoreCase))
            )
            {
                insert.Groups.Add(new Group(6, old.Type == "HATCH" ? "CONTINUOUS" : R12Name(line, "LTYPE")));
            }

            string colour = HeaderValue("$CECOLOR", 62);

            if (old.Type != "HATCH" && colour != null && Number(colour, out int index) && index != 256)
            {
                insert.Groups.Add(Whole(62, index));
            }

            Group space = old.Groups.FirstOrDefault(g => g.Code == 67);

            if (space.Value != null)
            {
                insert.Groups.Add(space);
            }

            insert.Groups.Add(new Group(2, block ?? string.Empty));
            insert.Groups.Add(new Group(10, "0.0"));
            insert.Groups.Add(new Group(20, "0.0"));
            insert.Groups.Add(new Group(30, "0.0"));

            // A hatch keeps its own extended data (the colours of its gradient, an annotative scale) in capitals and holds itself as
            // R14 did in the application ACAD; a multileader says which version of it made the pieces.
            if (old.Type == "HATCH")
            {
                insert.Groups.AddRange(HatchData(old));
            }
            else if (old.Type == "MULTILEADER")
            {
                insert.Groups.Add(new Group(1001, "ACAD_MLEADERVER"));
                insert.Groups.Add(Whole(1070, 2));
            }

            return insert;
        }

        /// <summary>
        /// The block of the pieces made for an entity of a space or a block: anonymous (flag 1), at the origin, in the current colour
        /// of the drawing where it is not by layer, as the INSERT is, its end with the handle the scratch block's had.
        /// </summary>
        private List<Record> NewBlock(string name, List<Record> content, Record end)
        {
            var block = new List<Record>(content.Count + 2);
            var head = new Record();
            head.Groups.Add(new Group(0, "BLOCK"));
            head.Groups.Add(new Group(8, "0"));
            string colour = HeaderValue("$CECOLOR", 62);
            int index = 256;
            bool coloured = colour != null && Number(colour, out index) && index != 256;

            if (coloured)
            {
                head.Groups.Add(Whole(62, index));
            }

            head.Groups.Add(new Group(2, name ?? string.Empty));
            head.Groups.Add(Whole(70, 1));
            head.Groups.Add(new Group(10, "0.0"));
            head.Groups.Add(new Group(20, "0.0"));
            head.Groups.Add(new Group(30, "0.0"));
            head.Groups.Add(new Group(3, name ?? string.Empty));
            head.Groups.Add(new Group(1, string.Empty));
            block.Add(head);
            block.AddRange(R12Parts(content));
            Record last = R12Record(end);

            if (coloured)
            {
                last.Groups.Insert(Math.Max(last.Groups.FindIndex(g => g.Code == 8), 0) + 1, Whole(62, index));
            }

            block.Add(last);
            return block;
        }

        /// <summary>
        /// The pieces of a broken entity as R12 holds them. A 3D polyline made of an ellipse or a spline is written as AutoCAD writes
        /// that one: the ellipse's polyline carries its first vertex in its own place (10, 20, 30) and the spline's zeros of five
        /// decimals, the layer of each before the handle, every vertex with six figures as C's <c>%#.6g</c> gives them.
        /// </summary>
        private List<Record> R12Parts(List<Record> content)
        {
            var parts = new List<Record>(content.Count);
            char sampled = '\0';
            Record owner = null;

            for (int index = 0; index < content.Count; index++)
            {
                Record part = content[index];
                Record converted = R12Record(part);

                if (converted == null)
                {
                    continue;
                }

                if (
                    converted.Type == "TEXT"
                    && HandleOf(part) is string own
                    && _plan.Sampled.TryGetValue(own, out char form)
                    && form == 'A'
                )
                {
                    OwnText(converted);
                }

                if (converted.Type == "POLYLINE")
                {
                    string handle = HandleOf(part);
                    sampled = handle != null && _plan.Sampled.TryGetValue(handle, out char kind) ? kind : '\0';

                    if (sampled != '\0')
                    {
                        Record first = content.Skip(index + 1).FirstOrDefault(r => r.Type == "VERTEX");
                        Sample(converted, sampled, first);
                    }
                }
                else if (sampled != '\0')
                {
                    Sample(converted, sampled, null);
                }

                // The vertices of a polyline and its end carry its linetype and colour, as AutoCAD writes them.
                if (converted.Type == "POLYLINE")
                {
                    owner = converted;
                }
                else if (owner != null && (converted.Type == "VERTEX" || converted.Type == "SEQEND"))
                {
                    Inherit(owner, converted);
                }

                if (converted.Type == "SEQEND")
                {
                    sampled = '\0';
                }

                parts.Add(converted);
            }

            return parts;
        }

        /// <summary>
        /// Gives one record of the polyline made of an ellipse (<c>E</c>) or a spline (<c>S</c>) AutoCAD's form of it. A vertex and
        /// the end have the layer before the handle and a vertex six figures; the polyline of an ellipse keeps the handle first and
        /// holds its first vertex (<paramref name="first"/>) as its own place, ahead of its flag of vertices, the spline's zeros
        /// of five decimals.
        /// </summary>
        private static void Sample(Record record, char kind, Record first)
        {
            string type = record.Type;
            int handle = record.Groups.FindIndex(g => g.Code == 5);
            int layer = record.Groups.FindIndex(g => g.Code == 8);

            if (handle >= 0 && layer > handle && !(type == "POLYLINE" && kind == 'E'))
            {
                Group moved = record.Groups[layer];
                record.Groups.RemoveAt(layer);
                record.Groups.Insert(handle, moved);
            }

            if (type == "POLYLINE")
            {
                for (int i = 0; i < record.Groups.Count; i++)
                {
                    int axis = record.Groups[i].Code / 10 - 1;

                    if (record.Groups[i].Code % 10 == 0 && axis >= 0 && axis <= 2)
                    {
                        string value = "0.00000";

                        if (kind == 'E' && first != null)
                        {
                            Group start = first.Groups.FirstOrDefault(g => g.Code == record.Groups[i].Code);
                            value = start.Value == null ? "0.0" : Real(start.Value);
                        }

                        record.Groups[i] = new Group(record.Groups[i].Code, value);
                    }
                }

                // The ellipse's polyline has its flag of vertices (66) after its place.
                int flag = record.Groups.FindIndex(g => g.Code == 66);
                int last = record.Groups.FindLastIndex(g => g.Code == 30);

                if (kind == 'E' && flag >= 0 && last > flag)
                {
                    Group moved = record.Groups[flag];
                    record.Groups.RemoveAt(flag);
                    record.Groups.Insert(last, moved);
                }

                return;
            }

            if (type == "VERTEX")
            {
                for (int i = 0; i < record.Groups.Count; i++)
                {
                    Group group = record.Groups[i];

                    if (
                        (group.Code == 10 || group.Code == 20 || group.Code == 30)
                        && double.TryParse(
                            group.Value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double value
                        )
                    )
                    {
                        record.Groups[i] = new Group(group.Code, SixFigures(value));
                    }
                }
            }
        }

        /// <summary>
        /// Gives the TEXT of the words of an angular dimension AutoCAD's form of it: the layer, the height, the handle, the colour,
        /// the justifications (72 and 71, of nought) and the place of six figures, then the words. "90%%d" is written 8, 40, 5, 62,
        /// 72, 71, 10 (29.6400), 20, 30 (0.00000) and 1.
        /// </summary>
        private static void OwnText(Record record)
        {
            var groups = new List<Group> { record.Groups[0] };
            int[] order = { 8, 40, 5, 62 };

            foreach (int code in order)
            {
                int at = record.Groups.FindIndex(g => g.Code == code);

                if (at >= 0)
                {
                    groups.Add(record.Groups[at]);
                }
            }

            groups.Add(new Group(72, "0"));
            groups.Add(new Group(71, "0"));

            foreach (int code in new[] { 10, 20, 30 })
            {
                int at = record.Groups.FindIndex(g => g.Code == code);

                if (
                    at >= 0
                    && double.TryParse(
                        record.Groups[at].Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value
                    )
                )
                {
                    groups.Add(new Group(code, SixFigures(value)));
                }
            }

            int words = record.Groups.FindIndex(g => g.Code == 1);

            if (words >= 0)
            {
                groups.Add(record.Groups[words]);
            }

            record.Groups.Clear();
            record.Groups.AddRange(groups);
        }

        /// <summary>A real number as a group of AutoCAD's R12 holds it: a whole one with its <c>.0</c>.</summary>
        private static string Real(string text)
        {
            string trimmed = text.Trim();
            return trimmed.IndexOfAny(new[] { '.', 'E', 'e' }) < 0 ? trimmed + ".0" : trimmed;
        }

        /// <summary>A number as C's <c>%#.6g</c> writes it, six figures with their trailing zeros: 40 is <c>40.0000</c>, 0 is <c>0.00000</c>, 1534567.5 is <c>1.53457e+06</c>.</summary>
        internal static string SixFigures(double value)
        {
            if (value == 0.0)
            {
                return BitConverter.DoubleToInt64Bits(value) < 0 ? "-0.00000" : "0.00000";
            }

            string scientific = value.ToString("E5", CultureInfo.InvariantCulture);
            int e = scientific.IndexOf('E');
            int exponent = int.Parse(
                scientific.Substring(e + 1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );

            if (exponent < -4 || exponent >= 6)
            {
                string digits = scientific.Substring(0, e);
                return digits
                    + "e"
                    + (exponent < 0 ? "-" : "+")
                    + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
            }

            int decimals = 5 - exponent;
            string fixedPoint = value.ToString(
                "F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
            return decimals == 0 ? fixedPoint + "." : fixedPoint;
        }

        /// <summary>
        /// A paper-space viewport as R12 holds it: its place, size, status and number, the rest of its view in the extended data
        /// of ACAD MVIEW, as AutoCAD writes it: the target and direction of the view, its twist, height, centre, lens and clipping,
        /// the flags of its status, the snap and grid, and the layers it freezes.
        /// </summary>
        private Record Viewport(Record source, Dictionary<string, string> layers)
        {
            var made = new Record();
            made.Groups.Add(new Group(0, "VIEWPORT"));

            foreach (int code in new[] { 5, 67, 8, 10, 20, 30, 40, 41, 68, 69 })
            {
                Group found = source.Groups.FirstOrDefault(g => g.Code == code);

                if (found.Value != null)
                {
                    made.Groups.Add(new Group(code, code == 8 ? R12Name(found.Value, "LAYER") : found.Value));
                }
            }

            string Value(int code, string empty)
            {
                Group found = source.Groups.FirstOrDefault(g => g.Code == code);
                return found.Value == null ? empty : found.Value.Trim();
            }

            Number(Value(90, "0"), out int status);
            Number(Value(72, "1000"), out int circle);
            int icon = ((status >> 5) & 1) | (((status >> 6) & 1) << 1);

            made.Groups.Add(new Group(1001, "ACAD"));
            made.Groups.Add(new Group(1000, "MVIEW"));
            made.Groups.Add(new Group(1002, "{"));
            made.Groups.Add(Whole(1070, 16));

            foreach (int[] point in new[] { new[] { 17, 27, 37 }, new[] { 16, 26, 36 } })
            {
                made.Groups.Add(new Group(1010, Value(point[0], "0.0")));
                made.Groups.Add(new Group(1020, Value(point[1], "0.0")));
                made.Groups.Add(new Group(1030, Value(point[2], point[0] == 16 ? "1.0" : "0.0")));
            }

            foreach (int code in new[] { 51, 45, 12, 22, 42, 43, 44 })
            {
                made.Groups.Add(new Group(1040, Value(code, code == 42 ? "50.0" : "0.0")));
            }

            made.Groups.Add(Whole(1070, status & 31));
            made.Groups.Add(Whole(1070, circle));
            made.Groups.Add(Whole(1070, 1));
            made.Groups.Add(Whole(1070, icon));
            made.Groups.Add(Whole(1070, (status >> 8) & 1));
            made.Groups.Add(Whole(1070, (status >> 9) & 1));
            made.Groups.Add(Whole(1070, (status >> 10) & 1));
            made.Groups.Add(Whole(1070, 0));

            foreach (int code in new[] { 50, 13, 23, 14, 24, 15, 25 })
            {
                made.Groups.Add(
                    new Group(1040, Value(code, code == 14 || code == 24 || code == 15 || code == 25 ? "0.5" : "0.0"))
                );
            }

            made.Groups.Add(Whole(1070, 0));
            made.Groups.Add(new Group(1002, "{"));

            foreach (Group frozen in source.Groups.Where(g => g.Code == 331))
            {
                if (layers.TryGetValue(frozen.Value.Trim(), out string layer))
                {
                    made.Groups.Add(new Group(1003, layer));
                }
            }

            made.Groups.Add(new Group(1002, "}"));
            made.Groups.Add(new Group(1002, "}"));
            return made;
        }

        #endregion

        #region The run

        /// <summary>Breaks what R12 cannot hold in the text of the drawing, before it is read, its words measured in the fonts of the folders given.</summary>
        private string R12Break(string text, string fontSearchPath)
        {
            DxfDatabase database = DxfDatabase.Parse(text);
            database.FontSearchPath = fontSearchPath ?? string.Empty;

            if (DxfVersions.Number(database.Version) < 1015)
            {
                throw new NotSupportedException(
                    "A drawing of R12 to R14 is saved as it is: its records are not those of R2000 and later."
                );
            }

            _plan = DxfR12Explosion.Prepare(database);
            return database.ToText();
        }

        /// <summary>Gives the drawing read the form of R12.</summary>
        private void RunR12()
        {
            Section header = Find("HEADER");
            _source = header == null ? -1 : VersionOf(header);

            if (_source < 1015)
            {
                throw new NotSupportedException(
                    "A drawing of R12 to R14 is saved as it is: its records are not those of R2000 and later."
                );
            }

            Section blocks = Find("BLOCKS");
            Section entities = Find("ENTITIES");
            MakeNamesUnique(Find("TABLES"), blocks);
            Dictionary<string, string> layers = LayerNames();
            _blockNames = BlockNames();
            var pieces = new Dictionary<string, List<Record>>(StringComparer.OrdinalIgnoreCase);
            var ends = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
            int number = 0;

            if (blocks != null)
            {
                pieces = TakeScratch(blocks, out ends);
                number = HighestAnonymous(blocks);
            }

            // What each associative hatch follows comes first: the entity says so in its own extended data.
            if (entities != null)
            {
                CollectBounds(entities);
            }

            if (blocks != null)
            {
                CollectBounds(blocks);
            }

            if (entities != null)
            {
                R12Content(entities, pieces, ends, layers);
            }

            if (blocks != null)
            {
                R12Content(blocks, pieces, ends, layers);

                // The blocks made are numbered on from the highest of the drawing in the order of the handles of the entities they
                // were made of, those of the spaces and of the blocks together, as AutoCAD numbers them.
                _made.Sort(
                    (a, b) =>
                    {
                        int byHandle = a.Key.CompareTo(b.Key);
                        return byHandle != 0 ? byHandle : a.Order.CompareTo(b.Order);
                    }
                );

                foreach (MadeBlock made in _made)
                {
                    number++;
                    string name = "*" + made.Letter + number.ToString(CultureInfo.InvariantCulture);
                    Rename(made.Insert, name);
                    Rename(made.Content[0], name);
                    blocks.Records.AddRange(made.Content);
                }
            }

            _left.TryGetValue("LAYOUT", out int already);

            if (_plan.Layouts > 0)
            {
                _left["LAYOUT"] = already + _plan.Layouts;
            }

            if (header != null)
            {
                R12Header(header);
            }

            Section tables = Find("TABLES");

            if (tables != null)
            {
                R12TableSection(tables);
            }

            // Only the four sections of R12 are written, in its order.
            var ordered = new List<Section>();

            foreach (string name in new[] { "HEADER", "TABLES", "BLOCKS", "ENTITIES" })
            {
                Section section = Find(name);

                if (section != null)
                {
                    ordered.Add(section);
                }
            }

            _sections.Clear();
            _sections.AddRange(ordered);
        }

        #endregion
    }
}
