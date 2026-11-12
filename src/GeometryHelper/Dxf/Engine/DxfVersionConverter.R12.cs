using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

        // The blocks made for what stood in a space, in the order they were made.
        private readonly List<List<Record>> _made = new List<List<Record>>();

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
                            record.Groups[i] = new Group(code, record.Groups[i].Value.ToUpperInvariant());
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
            bool own = false;
            var flags = new List<string>(2);

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

                string value = group.Value;

                if (IsName(type, group.Code))
                {
                    value = value.ToUpperInvariant();
                }

                if (type == "BLOCK" && (group.Code == 2 || group.Code == 3))
                {
                    value = SpaceName(record.Groups[i].Value) ?? value;
                }

                if (type == "STYLE" && group.Code == 3 && IsTrueType(value))
                {
                    value = "txt";
                }

                if (type == "DIMENSION" && group.Code == 70)
                {
                    value = Whole(70, Number(value, out int bits) ? bits & ~32 : 0).Value;
                }

                made.Groups.Add(new Group(group.Code, value));
            }

            // An attribute keeps its version and whether its position is locked (the two 280 of R2010) as the extended data of AcDbAttr.
            if ((type == "ATTRIB" || type == "ATTDEF") && flags.Count == 2)
            {
                made.Groups.Add(new Group(1001, "AcDbAttr"));
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
                        names[handle] = name.ToUpperInvariant();
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
                        names[handle] = name.ToUpperInvariant();
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
            bool space,
            Dictionary<string, List<Record>> pieces,
            Dictionary<string, Record> ends,
            Dictionary<string, string> layers,
            ref int number
        )
        {
            var made = new List<Record>(section.Records.Count);
            bool skipping = false;
            bool paper = false;

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

                    if (!space || piece.Direct)
                    {
                        // Inside a block they are written where the entity stood; so is a curve made a polyline or a line, which
                        // keeps the handle.
                        bool first = true;

                        foreach (Record part in content)
                        {
                            Record converted = R12Record(part);

                            if (converted == null)
                            {
                                continue;
                            }

                            if (first && piece.Direct)
                            {
                                int at = converted.Groups.FindIndex(g => g.Code == 5);

                                if (at >= 0)
                                {
                                    converted.Groups[at] = new Group(5, handle);
                                }
                            }

                            first = false;
                            made.Add(converted);
                        }

                        continue;
                    }

                    number++;
                    string block = "*" + piece.Letter + number.ToString(CultureInfo.InvariantCulture);
                    made.Add(Insert(record, block));
                    _made.Add(NewBlock(block, content, ends[piece.Block]));
                    continue;
                }

                Record one = type == "VIEWPORT" ? Viewport(record, layers) : R12Record(record);

                if (one != null)
                {
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

                    made.Add(one);
                }
            }

            section.Records.Clear();
            section.Records.AddRange(made);
        }

        /// <summary>The INSERT that stands where an entity stood in a space: its handle, layer, linetype, colour and space, at the origin.</summary>
        private Record Insert(Record old, string block)
        {
            var insert = new Record();
            insert.Groups.Add(new Group(0, "INSERT"));

            foreach (int code in new[] { 5, 8, 6, 62, 67 })
            {
                Group found = old.Groups.FirstOrDefault(g => g.Code == code);

                if (found.Value != null)
                {
                    insert.Groups.Add(
                        new Group(code, code == 8 || code == 6 ? found.Value.ToUpperInvariant() : found.Value)
                    );
                }
            }

            insert.Groups.Add(new Group(2, block));
            insert.Groups.Add(new Group(10, "0.0"));
            insert.Groups.Add(new Group(20, "0.0"));
            insert.Groups.Add(new Group(30, "0.0"));
            return insert;
        }

        /// <summary>The block of the pieces made for an entity of a space: anonymous (flag 1), at the origin, its end with the handle the scratch block's had.</summary>
        private List<Record> NewBlock(string name, List<Record> content, Record end)
        {
            var block = new List<Record>(content.Count + 2);
            var head = new Record();
            head.Groups.Add(new Group(0, "BLOCK"));
            head.Groups.Add(new Group(8, "0"));
            head.Groups.Add(new Group(2, name));
            head.Groups.Add(Whole(70, 1));
            head.Groups.Add(new Group(10, "0.0"));
            head.Groups.Add(new Group(20, "0.0"));
            head.Groups.Add(new Group(30, "0.0"));
            head.Groups.Add(new Group(3, name));
            head.Groups.Add(new Group(1, string.Empty));
            block.Add(head);

            foreach (Record part in content)
            {
                Record converted = R12Record(part);

                if (converted != null)
                {
                    block.Add(converted);
                }
            }

            block.Add(R12Record(end));
            return block;
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
                    made.Groups.Add(new Group(code, code == 8 ? found.Value.ToUpperInvariant() : found.Value));
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

        /// <summary>Breaks what R12 cannot hold in the text of the drawing, before it is read.</summary>
        private string R12Break(string text)
        {
            DxfDatabase database = DxfDatabase.Parse(text);

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

            Dictionary<string, string> layers = LayerNames();
            _blockNames = BlockNames();
            Section blocks = Find("BLOCKS");
            Section entities = Find("ENTITIES");
            var pieces = new Dictionary<string, List<Record>>(StringComparer.OrdinalIgnoreCase);
            var ends = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
            int number = 0;

            if (blocks != null)
            {
                pieces = TakeScratch(blocks, out ends);
                number = HighestAnonymous(blocks);
            }

            // The spaces first, in the order their entities stand in, as the blocks made for them are numbered by it.
            if (entities != null)
            {
                R12Content(entities, true, pieces, ends, layers, ref number);
            }

            if (blocks != null)
            {
                R12Content(blocks, false, pieces, ends, layers, ref number);

                foreach (List<Record> block in _made)
                {
                    blocks.Records.AddRange(block);
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
