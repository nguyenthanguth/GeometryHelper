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
                    pairs.AddRange(Kept(item, c => c == 2 || c == 70 || c == 10 || c == 11 || c == 12));
                    break;
                case "VIEW":
                case "VPORT":
                    pairs.AddRange(Kept(item, c => c < 100));
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

        /// <summary>A block's name, flags (1 anonymous, 2 attributes, 4 external reference), base point and the name of its first entity.</summary>
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

            var groups = new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(2, name),
                LispEntityData.Pair(70, flags),
                LispEntityData.Pair(10, origin),
            };

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
            return block == null ? string.Empty : NameOfRecord(block);
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
