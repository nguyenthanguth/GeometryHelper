using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>tblsearch</c>, <c>tblnext</c> and <c>tblobjname</c>: the records of the drawing's tables as the short lists AutoLISP
    /// gives them (a layer is <c>(0 . "LAYER") (2 . name) (70 . flags) (62 . colour) (6 . linetype)</c>; its whole record, with the
    /// owner, the handle and the subclass markers, is the <c>entget</c> of the name <c>tblobjname</c> gives). The table and the
    /// record are named in any case.
    /// </summary>
    internal static partial class LispDrawing
    {
        // The tables AutoLISP knows, by the name it calls them and the name the drawing's file has.
        private static readonly string[][] TableNames =
        {
            new[] { "LAYER", "LAYER" },
            new[] { "LTYPE", "LTYPE" },
            new[] { "STYLE", "STYLE" },
            new[] { "VIEW", "VIEW" },
            new[] { "UCS", "UCS" },
            new[] { "VPORT", "VPORT" },
            new[] { "APPID", "APPID" },
            new[] { "DIMSTYLE", "DIMSTYLE" },
            new[] { "BLOCK", "BLOCK_RECORD" },
        };

        // The codes of a dimension style's list in the order AutoCAD 2021 gives them (q5): the variables that came with R13
        // and the text style of DIMTXSTY.
        private static readonly int[] DimstyleCodes =
        {
            3,
            4,
            5,
            6,
            7,
            40,
            41,
            42,
            43,
            44,
            45,
            46,
            47,
            48,
            140,
            141,
            142,
            143,
            144,
            145,
            146,
            147,
            71,
            72,
            73,
            74,
            75,
            76,
            77,
            78,
            170,
            171,
            172,
            173,
            174,
            175,
            176,
            177,
            178,
            270,
            271,
            272,
            273,
            274,
            340,
            275,
            280,
            281,
            282,
            283,
            284,
            285,
            286,
            287,
            288,
        };

        private sealed class TableState
        {
            internal Dictionary<string, int> Next { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            LispInterpreter,
            TableState
        > TableStates = new System.Runtime.CompilerServices.ConditionalWeakTable<LispInterpreter, TableState>();

        static partial void RegisterTables(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);
            TableState walk = TableStates.GetValue(lisp, _ => new TableState());

            lisp.Define("TBLSEARCH", 2, 3, a =>
            {
                string table = TableArgument(a, 0);
                string name = LispInterpreter.Str(a[1]);
                bool setNext = a.Length > 2 && a[2] != null;
                return lisp.OnOwner(() => TableSearch(session, walk, a, table, name, setNext));
            });
            lisp.Define("TBLNEXT", 1, 2, a =>
            {
                string table = TableArgument(a, 0);
                bool rewind = a.Length > 1 && a[1] != null;
                return lisp.OnOwner(() => TableNext(session, walk, a, table, rewind));
            });
            lisp.Define("TBLOBJNAME", 2, 2, a =>
            {
                string table = TableArgument(a, 0);
                string name = LispInterpreter.Str(a[1]);
                return lisp.OnOwner(() => TableObject(session, a, table, name));
            });
        }

        /// <summary>The upper case name of the table an argument gives; the error of AutoCAD 2021 for one it has not.</summary>
        private static string TableArgument(object[] arguments, int at)
        {
            string text = LispInterpreter.Str(arguments[at]);

            foreach (string[] names in TableNames)
            {
                if (string.Equals(names[0], text, StringComparison.OrdinalIgnoreCase))
                {
                    return names[0];
                }
            }

            // AutoCAD 2021 (q5): (tblsearch "FOO" "x") gives AutoCAD rejected function: invalid table function argument(s): "FOO" "x"
            var shown = new List<string>();

            foreach (object argument in arguments)
            {
                shown.Add(LispInterpreter.Print(argument, true));
            }

            throw new LispException(
                "AutoCAD rejected function: invalid table function argument(s): " + string.Join(" ", shown)
            );
        }

        private static string FileNameOf(string table)
        {
            foreach (string[] names in TableNames)
            {
                if (names[0] == table)
                {
                    return names[1];
                }
            }

            return table;
        }

        /// <summary>
        /// The records of a table, in the order of the table: the blocks of model space and paper space are not among them, and
        /// for <c>tblnext</c> (<paramref name="walking"/>) neither are the linetypes ByBlock and ByLayer, which AutoCAD 2021's
        /// walk of the linetypes leaves out (the dump of every routine run lists Continuous alone).
        /// </summary>
        private static List<DxfItem> RecordsOf(CadDocument document, string table, bool walking = false)
        {
            var records = new List<DxfItem>();

            foreach (DxfItem item in document.Database.File.TableItems(FileNameOf(table)))
            {
                if (item.IsRemoved)
                {
                    continue;
                }

                string name = NameOfRecord(item);

                if (
                    table == "BLOCK"
                    && (
                        string.Equals(name, "*Model_Space", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "*Paper_Space", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    continue;
                }

                if (
                    walking
                    && table == "LTYPE"
                    && (
                        string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    continue;
                }

                records.Add(item);
            }

            return records;
        }

        private static string NameOfRecord(DxfItem item)
        {
            foreach (DxfTypedValue group in item.Groups)
            {
                if (group.TypeCode == 2)
                {
                    return group.Value.Trim();
                }
            }

            return string.Empty;
        }

        private static int IndexOfRecord(List<DxfItem> records, string name)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (string.Equals(NameOfRecord(records[i]), name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static object TableSearch(
            CadCommandSession session,
            TableState walk,
            object[] arguments,
            string table,
            string name,
            bool setNext
        )
        {
            List<DxfItem> records = RecordsOf(session.Document, table);
            int at = IndexOfRecord(records, name);

            if (at < 0)
            {
                return null;
            }

            if (setNext)
            {
                // The next record is the one after it in the walk of the table.
                walk.Next[table] = IndexOfRecord(RecordsOf(session.Document, table, walking: true), name) + 1;
            }

            return RecordList(session.Document, table, records[at]);
        }

        private static object TableNext(
            CadCommandSession session,
            TableState walk,
            object[] arguments,
            string table,
            bool rewind
        )
        {
            if (rewind)
            {
                walk.Next[table] = 0;
            }

            List<DxfItem> records = RecordsOf(session.Document, table, walking: true);
            walk.Next.TryGetValue(table, out int next);

            if (next >= records.Count)
            {
                return null;
            }

            walk.Next[table] = next + 1;
            return RecordList(session.Document, table, records[next]);
        }

        private static object TableObject(CadCommandSession session, object[] arguments, string table, string name)
        {
            List<DxfItem> records = RecordsOf(session.Document, table);
            int at = IndexOfRecord(records, name);

            if (at < 0)
            {
                return null;
            }

            // A block gives the name of its BLOCK entity, whose entget is the list of the block's beginning (AutoCAD 2021, console,
            // l2p pa), not that of its record.
            if (table == "BLOCK")
            {
                foreach (DxfFileBlock block in session.Document.Database.File.Blocks.Values)
                {
                    if (
                        block.Head != null
                        && !block.Head.IsRemoved
                        && !string.IsNullOrEmpty(block.Head.Handle)
                        && string.Equals(block.Name, NameOfRecord(records[at]), StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return new LispEntityName(block.Head.Handle);
                    }
                }
            }

            return new LispEntityName(records[at].Handle);
        }

        #region Making a record

        // The subclass marker each table's record has after AcDbSymbolTableRecord.
        private static readonly string[][] RecordMarkers =
        {
            new[] { "LAYER", "AcDbLayerTableRecord" },
            new[] { "LTYPE", "AcDbLinetypeTableRecord" },
            new[] { "STYLE", "AcDbTextStyleTableRecord" },
            new[] { "APPID", "AcDbRegAppTableRecord" },
            new[] { "DIMSTYLE", "AcDbDimStyleTableRecord" },
            new[] { "UCS", "AcDbUCSTableRecord" },
            new[] { "VIEW", "AcDbViewTableRecord" },
            new[] { "VPORT", "AcDbViewportTableRecord" },
        };

        /// <summary>Whether a type is a record of a table, which <c>entmake</c> makes by its own rules: all of AutoCAD's but BLOCK_RECORD, which it refuses.</summary>
        private static bool IsRecordType(string type) =>
            type == "BLOCK_RECORD" || Array.Exists(RecordMarkers, m => m[0] == type);

        /// <summary>
        /// Makes a record of a table from a list, as <c>entmake</c> does (AutoCAD 2021, l2p pa and p2): the list has both subclass
        /// markers, AcDbSymbolTableRecord and the table's own, and a name no record of the table has and a record can have; else
        /// nil, silently. A layer that names a linetype that is not loaded writes <c>Error: Undefined line type NOSUCH in
        /// LayerTableRecord GHL4</c> with no line break and gives nil. What the list gives not is what a record made by the
        /// commands has (a layer is colour 7, Continuous). The change is one step with the form, and <c>entlast</c> is as it was.
        /// </summary>
        private static object MakeRecord(
            LispInterpreter lisp,
            CadCommandSession session,
            string type,
            List<KeyValuePair<int, object>> given,
            object list
        )
        {
            string[] kind = Array.Find(RecordMarkers, m => m[0] == type);
            string name = given.Find(p => p.Key == 2).Value as string;

            if (
                kind == null
                || !HasMarker(given, "AcDbSymbolTableRecord")
                || !HasMarker(given, kind[1])
                || name == null
            )
            {
                return null;
            }

            try
            {
                DxfSymbolTable.CheckName(name, nameof(name));
            }
            catch (ArgumentException)
            {
                return null;
            }

            CadDocument document = session.Document;

            if (IndexOfRecord(RecordsOf(document, type), name) >= 0)
            {
                return null;
            }

            DxfDatabase database = document.Database;
            int flags = given.Find(p => p.Key == 70).Value is int own ? own : 0;
            var rest = given.FindAll(p =>
                p.Key != -1
                && p.Key != 0
                && p.Key != 2
                && p.Key != 5
                && p.Key != 70
                && p.Key != 100
                && p.Key != 102
                && p.Key != 105
                && p.Key != 330
            );
            Action<CadEditScope> make;

            try
            {
                switch (type)
                {
                    case "LAYER":
                        string linetype = rest.Find(p => p.Key == 6).Value as string ?? "Continuous";

                        if (!database.LinetypeTable.Has(linetype))
                        {
                            lisp.Write("Error: Undefined line type " + linetype + " in LayerTableRecord " + name);
                            return null;
                        }

                        DxfLayerTableRecord layer = NewLayer(name, linetype, flags, rest);
                        make = s => s.Database.LayerTable.Add(layer);
                        break;
                    case "STYLE":
                        DxfTextStyleTableRecord style = NewTextStyle(name, flags, rest);
                        make = s => s.Database.TextStyleTable.Add(style);
                        break;
                    case "LTYPE":
                        make = Linetype(name, flags, rest);
                        break;
                    default:
                        make = Raw(type, kind[1], name, flags, rest);
                        break;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug(
                    "LispDrawing: entmake of a " + type + " record was refused: " + exception.Message
                );
                return null;
            }

            session.OpenLispStep();
            session.HoldLispStep();
            var planned = new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New " + type, null, name) };
            CadEditResult result = session.Apply("ENTMAKE", planned, make);
            return result.Succeeded ? list : null;
        }

        private static bool HasMarker(List<KeyValuePair<int, object>> given, string marker) =>
            given.Exists(p =>
                p.Key == 100
                && p.Value is string text
                && string.Equals(text, marker, StringComparison.OrdinalIgnoreCase)
            );

        private static double RealOf(List<KeyValuePair<int, object>> groups, int code, double otherwise)
        {
            object found = groups.Find(p => p.Key == code).Value;
            return found is double real ? real
                : found is int whole ? whole
                : otherwise;
        }

        /// <summary>A layer of a list: its colour (a negative one is turned off), flags 1 (frozen) and 4 (locked), lineweight and whether it is plotted; the colour 7 and plotted where the list gives none.</summary>
        private static DxfLayerTableRecord NewLayer(
            string name,
            string linetype,
            int flags,
            List<KeyValuePair<int, object>> rest
        )
        {
            int colour = rest.Find(p => p.Key == 62).Value is int given ? given : 7;
            var layer = new DxfLayerTableRecord
            {
                Name = name,
                ColorIndex = Math.Abs(colour),
                LinetypeName = linetype,
                IsOff = colour < 0,
                IsFrozen = (flags & 1) != 0,
                IsLocked = (flags & 4) != 0,
            };

            if (rest.Find(p => p.Key == 370).Value is int weight)
            {
                if (!Enum.IsDefined(typeof(DxfLineWeight), weight))
                {
                    throw new ArgumentException(
                        "A lineweight of " + weight.ToString(CultureInfo.InvariantCulture) + " is not one."
                    );
                }

                layer.LineWeight = (DxfLineWeight)weight;
            }

            if (rest.Find(p => p.Key == 290).Value is int plotted)
            {
                layer.IsPlottable = plotted != 0;
            }

            return layer;
        }

        /// <summary>A text style of a list: its font file (txt where none), big font, fixed height, width, slant (in radians, as the list of AutoCAD gives it) and the vertical flag.</summary>
        private static DxfTextStyleTableRecord NewTextStyle(
            string name,
            int flags,
            List<KeyValuePair<int, object>> rest
        )
        {
            var style = new DxfTextStyleTableRecord
            {
                Name = name,
                FileName = rest.Find(p => p.Key == 3).Value as string ?? "txt",
                BigFontFileName = rest.Find(p => p.Key == 4).Value as string ?? string.Empty,
                TextSize = RealOf(rest, 40, 0.0),
                XScale = RealOf(rest, 41, 1.0),
                ObliquingAngle = RealOf(rest, 50, 0.0),
                IsVertical = (flags & 4) != 0,
            };

            if (rest.Find(p => p.Key == 71).Value is int generation)
            {
                style.FlagBits = (byte)generation;
            }

            return style;
        }

        /// <summary>
        /// A linetype of a list: its description, the length of its pattern and the groups of its dashes, a 74 (0) given after each
        /// dash that gives none, as AutoCAD writes them.
        /// </summary>
        private static Action<CadEditScope> Linetype(string name, int flags, List<KeyValuePair<int, object>> rest)
        {
            string description = rest.Find(p => p.Key == 3).Value as string ?? string.Empty;
            List<KeyValuePair<int, object>> pattern = rest.FindAll(p =>
                p.Key != 3 && p.Key != 72 && p.Key != 73 && p.Key != 40
            );
            var withKinds = new List<KeyValuePair<int, object>>(pattern.Count + 4);

            for (int i = 0; i < pattern.Count; i++)
            {
                withKinds.Add(pattern[i]);

                if (pattern[i].Key == 49 && !(i + 1 < pattern.Count && pattern[i + 1].Key == 74))
                {
                    withKinds.Add(LispEntityData.Pair(74, 0));
                }
            }

            int count = rest.Find(p => p.Key == 73).Value is int dashes
                ? dashes
                : withKinds.FindAll(p => p.Key == 49).Count;
            double length = 0.0;

            foreach (KeyValuePair<int, object> dash in withKinds)
            {
                if (dash.Key == 49)
                {
                    length += Math.Abs(
                        dash.Value is double real ? real
                        : dash.Value is int whole ? whole
                        : 0.0
                    );
                }
            }

            length = RealOf(rest, 40, length);
            List<KeyValuePair<int, string>> groups = LispEntityData.ToGroups("LTYPE", withKinds);
            return s =>
            {
                if (s.Database.File.AddLinetype(name, flags, description, length, count, groups) == null)
                {
                    throw new InvalidOperationException("The drawing has no table of linetypes.");
                }
            };
        }

        /// <summary>
        /// A record that is written as its groups are given, after its name and flags: an application, a dimension style (whose
        /// groups 270 and 287 are the file's 277 and 289, and whose block names 5, 6 and 7 are left to the commands of the arrowheads),
        /// a UCS, a view or a viewport. A UCS has (79 . 0) (146 . 0) and a view (281 . 0) (72 . 0) as the commands make them.
        /// </summary>
        private static Action<CadEditScope> Raw(
            string table,
            string marker,
            string name,
            int flags,
            List<KeyValuePair<int, object>> rest
        )
        {
            var groups = new List<KeyValuePair<int, object>>();

            foreach (KeyValuePair<int, object> pair in rest)
            {
                if (table == "DIMSTYLE" && (pair.Key == 5 || pair.Key == 6 || pair.Key == 7))
                {
                    continue;
                }

                int code =
                    table == "DIMSTYLE"
                        ? (
                            pair.Key == 270 ? 277
                            : pair.Key == 287 ? 289
                            : pair.Key
                        )
                        : pair.Key;
                groups.Add(LispEntityData.Pair(code, pair.Value));
            }

            if (table == "UCS")
            {
                AddMissing(groups, 79, 0);
                AddMissing(groups, 146, 0.0);
            }
            else if (table == "VIEW")
            {
                AddMissing(groups, 281, 0);
                AddMissing(groups, 72, 0);
            }

            List<KeyValuePair<int, string>> text = LispEntityData.ToGroups(table, groups);
            return s =>
            {
                DxfItem item = s.Database.File.AddTableRecord(table, marker, b =>
                {
                    b.Group(2, name);
                    b.Group(70, flags);

                    foreach (KeyValuePair<int, string> group in text)
                    {
                        b.Group(group.Key, group.Value);
                    }
                });

                if (item == null)
                {
                    throw new InvalidOperationException("The drawing has no " + table + " table.");
                }

                if (table == "DIMSTYLE")
                {
                    DxfDimStyleTable.ListHandle(s.Database.File, item.Handle, true);
                }
            };
        }

        #endregion

        #region The lists

        /// <summary>The short list of a record: its type, name and flags first, then what the table's record holds.</summary>
        private static object RecordList(CadDocument document, string table, DxfItem item)
        {
            var pairs = new List<KeyValuePair<int, object>> { LispEntityData.Pair(0, table) };

            switch (table)
            {
                case "DIMSTYLE":
                    pairs.AddRange(DimstyleGroups(document, item));
                    break;
                case "BLOCK":
                    pairs.AddRange(BlockGroups(document, item));
                    break;
                case "LAYER":
                    pairs.AddRange(Kept(item, c => c == 2 || c == 70 || c == 62 || c == 6));
                    break;
                case "UCS":
                    // AutoCAD 2021 (l2p pa): (79 . 0) (146 . 0.0) follow the axes, the record has them or not.
                    pairs.AddRange(
                        Kept(item, c => c == 2 || c == 70 || c == 10 || c == 11 || c == 12 || c == 79 || c == 146)
                    );
                    AddMissing(pairs, 79, 0);
                    AddMissing(pairs, 146, 0.0);
                    break;
                case "VIEW":
                    // AutoCAD 2021 (l2p pa): the groups below 100 but 73, with (72 . 0) after (71 . 0), then the view's own UCS (110, 111,
                    // 112) and (79 . 0) (146 . 0.0), which the record has or not.
                    pairs.AddRange(Kept(item, c => c < 100 && c != 73 && c != 79));
                    int after = pairs.FindLastIndex(p => p.Key == 71);

                    if (!pairs.Exists(p => p.Key == 72))
                    {
                        pairs.Insert(after < 0 ? pairs.Count : after + 1, LispEntityData.Pair(72, 0));
                    }

                    pairs.AddRange(Kept(item, c => c == 110 || c == 111 || c == 112 || c == 79 || c == 146));
                    AddMissing(pairs, 110, new[] { 0.0, 0.0, 0.0 });
                    AddMissing(pairs, 111, new[] { 1.0, 0.0, 0.0 });
                    AddMissing(pairs, 112, new[] { 0.0, 1.0, 0.0 });
                    AddMissing(pairs, 79, 0);
                    AddMissing(pairs, 146, 0.0);
                    break;
                case "VPORT":
                    pairs.AddRange(Kept(item, c => c < 100));
                    break;
                case "LTYPE":
                    // AutoCAD 2021 (l2p pa): no group 74 where it is 0, as every dash of a plain linetype has it.
                    pairs.AddRange(
                        Kept(item, c => true).FindAll(p => !(p.Key == 74 && p.Value is int kind && kind == 0))
                    );
                    break;
                case "STYLE":
                    // AutoCAD 2021 (l2p p3): the obliquing is in radians (0.261799 for 15 degrees), the file has it in degrees.
                    foreach (KeyValuePair<int, object> pair in Kept(item, c => true))
                    {
                        pairs.Add(
                            pair.Key == 50 && pair.Value is double degrees
                                ? LispEntityData.Pair(50, degrees * Math.PI / 180.0)
                                : pair
                        );
                    }

                    break;
                default:
                    pairs.AddRange(Kept(item, c => true));
                    break;
            }

            var items = new List<object>(pairs.Count);

            foreach (KeyValuePair<int, object> pair in pairs)
            {
                items.Add(LispEntityData.ToItem(pair));
            }

            return LispInterpreter.ListOf(items);
        }

        /// <summary>
        /// The tail of a layer's list as AutoCAD 2021 gives it (console, l2p2 pf): the plot flag (290, 1 where the record has none), the
        /// lineweight (370, -3 for the default one), the plot style (390) and the material (347) and visual style (348) objects,
        /// in that order after the colour and linetype. The presenter keeps no material or visual style object, so where the record
        /// names none the two are the name of no object (handle 0): <c>entget</c> of it is nil, as AutoCAD's is for 348.
        /// </summary>
        internal static void CompleteLayer(List<KeyValuePair<int, object>> pairs)
        {
            var tail = new Dictionary<int, object>();

            foreach (int code in new[] { 290, 370, 390, 347, 348 })
            {
                int at = pairs.FindIndex(p => p.Key == code);

                if (at >= 0)
                {
                    tail[code] = pairs[at].Value;
                    pairs.RemoveAt(at);
                }
            }

            pairs.Add(LispEntityData.Pair(290, tail.TryGetValue(290, out object plot) ? plot : 1));
            pairs.Add(LispEntityData.Pair(370, tail.TryGetValue(370, out object weight) ? weight : -3));

            if (tail.TryGetValue(390, out object style))
            {
                pairs.Add(LispEntityData.Pair(390, style));
            }

            pairs.Add(
                LispEntityData.Pair(
                    347,
                    tail.TryGetValue(347, out object material) ? material : new LispEntityName("0")
                )
            );
            pairs.Add(
                LispEntityData.Pair(348, tail.TryGetValue(348, out object visual) ? visual : new LispEntityName("0"))
            );
        }

        /// <summary>Puts a group last where the list has none of its code.</summary>
        private static void AddMissing(List<KeyValuePair<int, object>> pairs, int code, object value)
        {
            if (!pairs.Exists(p => p.Key == code))
            {
                pairs.Add(LispEntityData.Pair(code, value));
            }
        }

        /// <summary>The groups of a record as the file holds them, without its handle, owner, subclass markers and reactors, those a test lets through.</summary>
        private static List<KeyValuePair<int, object>> Kept(DxfItem item, Func<int, bool> wanted)
        {
            var kept = new List<KeyValuePair<int, object>>();

            foreach (KeyValuePair<int, object> pair in LispEntityData.Read(item.Groups, item.Type))
            {
                if (
                    pair.Key == 5
                    || pair.Key == 105
                    || pair.Key == 330
                    || pair.Key == 100
                    || pair.Key == 102
                    || pair.Key == 360
                )
                {
                    continue;
                }

                if (wanted(pair.Key))
                {
                    kept.Add(pair);
                }
            }

            return kept;
        }

        /// <summary>A block's name, flags (1 anonymous, 2 attributes, 4 external reference), description, base point and the name of its first entity.</summary>
        private static List<KeyValuePair<int, object>> BlockGroups(CadDocument document, DxfItem item)
        {
            string name = NameOfRecord(item);
            DxfBlockTableRecord block = document.Database.BlockTable.Has(name)
                ? document.Database.BlockTable[name] as DxfBlockTableRecord
                : null;
            int flags = 0;
            double[] origin = { 0.0, 0.0, 0.0 };
            object first = null;

            if (block != null)
            {
                flags =
                    (block.IsAnonymous ? 1 : 0)
                    | (block.HasAttributeDefinitions ? 2 : 0)
                    | (block.IsFromExternalReference ? 4 : 0);
                origin = new[] { block.Origin.X, block.Origin.Y, block.Origin.Z };

                foreach (DxfItem entity in Items(block))
                {
                    first = new LispEntityName(entity.Handle);
                    break;
                }
            }

            // AutoCAD 2021 (l2p pa, D1108): the description (4) follows the flags, empty where the block has none.
            var groups = new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(2, name),
                LispEntityData.Pair(70, flags),
                LispEntityData.Pair(4, block?.Comments ?? string.Empty),
                LispEntityData.Pair(10, origin),
            };

            // A block with no entity names its ENDBLK (AutoCAD 2021, console, l2p2 pf).
            first = first ?? EndOf(document, name);

            if (first != null)
            {
                groups.Add(LispEntityData.Pair(-2, first));
            }

            return groups;
        }

        /// <summary>
        /// The groups of a dimension style in the order of AutoCAD 2021's list (q5): the variables of R13 with the value the style
        /// has or AutoCAD's own where it gives none, the block names (5, 6, 7) as text, the old DIMUNIT (270) of DIMLUNIT, and the
        /// text style of DIMTXSTY (340) as an entity name.
        /// </summary>
        private static List<KeyValuePair<int, object>> DimstyleGroups(CadDocument document, DxfItem item)
        {
            var groups = new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(2, NameOfRecord(item)),
                LispEntityData.Pair(70, FlagsOf(item)),
            };
            var record = (DxfDimStyleTableRecord)document.Database.DimStyleTable[NameOfRecord(item)];

            foreach (int code in DimstyleCodes)
            {
                switch (code)
                {
                    case 5:
                    case 6:
                    case 7:
                        string handle = record.Text(
                            DxfDimVariable.Named(
                                code == 5 ? "Dimblk"
                                : code == 6 ? "Dimblk1"
                                : "Dimblk2"
                            )
                        );
                        groups.Add(LispEntityData.Pair(code, BlockNameOf(document, handle)));
                        break;
                    case 340:
                        string style = record.Text(DxfDimVariable.Named("Dimtxsty"));
                        groups.Add(
                            LispEntityData.Pair(
                                340,
                                new LispEntityName(string.IsNullOrWhiteSpace(style) ? "0" : style.Trim())
                            )
                        );
                        break;
                    default:
                        DxfDimVariable variable = DxfDimVariable.OfCode(
                            code == 270 ? 277
                                : code == 287 ? 289
                                : code,
                            false
                        );
                        groups.Add(LispEntityData.Pair(code, DimstyleValue(record, variable)));
                        break;
                }
            }

            return groups;
        }

        private static int FlagsOf(DxfItem item)
        {
            foreach (DxfTypedValue group in item.Groups)
            {
                if (group.TypeCode == 70)
                {
                    return int.TryParse(
                        group.Value,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int flags
                    )
                        ? flags
                        : 0;
                }
            }

            return 0;
        }

        private static string BlockNameOf(CadDocument document, string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
            {
                return string.Empty;
            }

            DxfItem block = FindItem(document, handle.Trim());

            if (block == null)
            {
                return string.Empty;
            }

            // The name AutoCAD lists for an arrowhead of its own is its block's without the underscore: None, Dot, ArchTick.
            string name = NameOfRecord(block);
            return document.Database.BlockTable.Has(name)
                ? CadArrowheads.ListedName(document.Database.BlockTable[name])
                : name;
        }

        private static object DimstyleValue(DxfDimStyleTableRecord record, DxfDimVariable variable)
        {
            string text = record.Text(variable) ?? variable.Fallback;

            switch (variable.Kind)
            {
                case DxfDimKind.Real:
                case DxfDimKind.Angle:
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)
                        ? real
                        : 0.0;
                case DxfDimKind.Text:
                    return text ?? string.Empty;
                default:
                    return int.TryParse(
                        text,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int whole
                    )
                        ? whole
                        : 0;
            }
        }

        #endregion
    }
}
