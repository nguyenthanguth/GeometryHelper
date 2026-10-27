using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        // The variables of the header that name a record of a table, by the table the record is of.
        private static readonly KeyValuePair<string, string>[] HeaderNames =
        {
            new KeyValuePair<string, string>("$CLAYER", "LAYER"),
            new KeyValuePair<string, string>("$CELTYPE", "LTYPE"),
            new KeyValuePair<string, string>("$DIMLTYPE", "LTYPE"),
            new KeyValuePair<string, string>("$DIMLTEX1", "LTYPE"),
            new KeyValuePair<string, string>("$DIMLTEX2", "LTYPE"),
            new KeyValuePair<string, string>("$TEXTSTYLE", "STYLE"),
            new KeyValuePair<string, string>("$DIMTXSTY", "STYLE"),
            new KeyValuePair<string, string>("$DIMSTYLE", "DIMSTYLE"),
            new KeyValuePair<string, string>("$DIMBLK", "BLOCK"),
            new KeyValuePair<string, string>("$DIMBLK1", "BLOCK"),
            new KeyValuePair<string, string>("$DIMBLK2", "BLOCK"),
            new KeyValuePair<string, string>("$DIMLDRBLK", "BLOCK"),
            new KeyValuePair<string, string>("$CMLSTYLE", "MLINESTYLE"),
        };

        // The types of entity drawn in a dimension style (group 3).
        private static readonly HashSet<string> DimensionStyled = new HashSet<string>(StringComparer.Ordinal)
        {
            "DIMENSION", "ARC_DIMENSION", "LARGE_RADIAL_DIMENSION", "LEADER", "TOLERANCE",
        };

        // The types of entity drawn by a block they name (group 2).
        private static readonly HashSet<string> BlockDrawn = new HashSet<string>(StringComparer.Ordinal)
        {
            "INSERT", "DIMENSION", "ARC_DIMENSION", "LARGE_RADIAL_DIMENSION", "ACAD_TABLE",
        };

        // The types of entity written in a text style (group 7), a cell of a table among them.
        private static readonly HashSet<string> TextStyled = new HashSet<string>(StringComparer.Ordinal) { "TEXT", "MTEXT", "ATTRIB", "ATTDEF", "ACAD_TABLE" };

        /// <summary>
        /// What the records of a file refer to, counted in one walk of it: each handle a hard pointer or a hard owner names, and
        /// each record of a table named where AutoCAD's database holds a hard pointer to it and the file writes its name.
        /// </summary>
        internal sealed class Uses
        {
            private readonly Dictionary<ulong, int> _handles = new Dictionary<ulong, int>();
            private readonly HashSet<ulong> _pointed = new HashSet<ulong>();
            private readonly Dictionary<string, Dictionary<string, int>> _names = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

            /// <summary>How many hard pointers and hard owners name a handle.</summary>
            internal int OfHandle(string handle) => handle != null && TryHex(handle.Trim(), out ulong key) && _handles.TryGetValue(key, out int count) ? count : 0;

            /// <summary>Whether any pointer names a handle, soft or hard, as <see cref="PointedAt"/> finds one.</summary>
            internal bool IsPointedAt(string handle) => handle != null && TryHex(handle.Trim(), out ulong key) && _pointed.Contains(key);

            internal void Pointed(ulong handle) => _pointed.Add(handle);

            /// <summary>How many groups name a record of a table by its name, letters of either case the same.</summary>
            internal int OfName(string table, string name)
                => name != null && _names.TryGetValue(table, out Dictionary<string, int> names) && names.TryGetValue(name.Trim(), out int count) ? count : 0;

            internal void Handle(ulong handle) => _handles[handle] = (_handles.TryGetValue(handle, out int count) ? count : 0) + 1;

            internal void Name(string table, string name)
            {
                if (name.Length == 0)
                {
                    return;
                }

                if (!_names.TryGetValue(table, out Dictionary<string, int> names))
                {
                    names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _names.Add(table, names);
                }

                names[name] = (names.TryGetValue(name, out int count) ? count : 0) + 1;
            }
        }

        /// <summary>
        /// Counts what the records of the file refer to, in one walk: in every record outside the header, each hard pointer and
        /// hard owner (340 to 349, 360 to 369, 390 to 399, 480 and 481, an extension dictionary's 360 among them) before the
        /// extended data, and each handle of the extended data of any application (1005, as the overrides of a dimension name
        /// its arrows and text style); in the entities of model space, paper space and the blocks, their vertices and
        /// attributes among them, the layer (8), the linetype (6), the text style of a text or of a cell of a table (7), the
        /// dimension style of a dimension, a leader or a tolerance (3) and the block of an INSERT, a dimension or a table (2),
        /// outside the groups of the applications, the arrow an override of a dimension of R12 names by name (1070 5, 6 or 7,
        /// then 1000 the block's name) and the layers a viewport freezes (331, or 1003 of its extended data before R2000); a
        /// layer's and a multiline style's linetype (6), the text style of a table style (7) and the arrow blocks of a
        /// dimension style, of any version (5, 6, 7); and the records the header names (<c>$CLAYER</c>, <c>$CELTYPE</c>,
        /// <c>$TEXTSTYLE</c>, <c>$DIMSTYLE</c>, <c>$DIMBLK</c>, <c>$CMLSTYLE</c> and their kin). Apart from the counts, every
        /// handle a pointer names, soft or hard, as <see cref="PointedAt"/> finds one.
        /// </summary>
        internal Uses CountUses()
        {
            var uses = new Uses();

            foreach (Section section in _sections)
            {
                if (section.Name == "HEADER")
                {
                    continue;
                }

                bool entities = section.Name == "ENTITIES" || section.Name == "BLOCKS";

                foreach (Rec record in Inside(section))
                {
                    List<Pair> pairs = PairsOf(record);
                    int data = DataAt(record, pairs);
                    string type = record.Type;
                    bool braced = false;
                    bool owner = false;
                    bool overrides = false;

                    // A viewport freezes layers by handle (331), held hard as AutoCAD holds them; before R2000 by name (1003 of its
                    // extended data).
                    bool viewport = entities && type == "VIEWPORT";

                    for (int i = 1; i < pairs.Count; i++)
                    {
                        int code = pairs[i].Code;
                        ulong handle;

                        if (i >= data)
                        {
                            // A handle of the extended data points hard, of any application, as the overrides of a dimension name
                            // its arrow blocks and text style (1005 of ACAD DSTYLE); an override of R12 names its arrow by name.
                            overrides = entities && DimensionOverrides(record, pairs, i, overrides);
                            string arrow = overrides ? OverrideArrow(record, pairs, i) : null;

                            if (arrow != null)
                            {
                                uses.Name("BLOCK", arrow);
                            }
                            else if (code == 1005 && TryHex(record.Bytes, pairs[i].ValueStart, pairs[i].ValueEnd, out handle) && handle != 0)
                            {
                                uses.Handle(handle);
                                uses.Pointed(handle);
                            }
                            else if (code == 1003 && viewport && !Supports(1015))
                            {
                                uses.Name("LAYER", DxfReader.NameOf(Text(record, pairs[i])).Trim());
                            }

                            continue;
                        }

                        if (code == 102)
                        {
                            braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                            continue;
                        }

                        if (IsHard(code) || (viewport && code == 331))
                        {
                            if (TryHex(record.Bytes, pairs[i].ValueStart, pairs[i].ValueEnd, out handle) && handle != 0)
                            {
                                uses.Handle(handle);

                                if (!braced)
                                {
                                    uses.Pointed(handle);
                                }
                            }

                            continue;
                        }

                        if (braced)
                        {
                            continue;
                        }

                        // The soft pointers, the record's owner (its first 330) aside, as PointedAt finds them.
                        if ((code >= 320 && code <= 339) || (code >= 350 && code <= 359))
                        {
                            if (!(code == 330 && !owner) && TryHex(record.Bytes, pairs[i].ValueStart, pairs[i].ValueEnd, out handle) && handle != 0)
                            {
                                uses.Pointed(handle);
                            }

                            owner |= code == 330;
                            continue;
                        }

                        string table = NamedTable(entities, type, code);

                        if (table != null)
                        {
                            uses.Name(table, DxfReader.NameOf(Text(record, pairs[i])).Trim());
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, string> variable in HeaderNames)
            {
                IReadOnlyList<DxfTypedValue> groups = HeaderOf(variable.Key);

                if (groups != null && groups.Count > 0 && groups[0].Value != null)
                {
                    uses.Name(variable.Value, DxfReader.NameOf(groups[0].Value).Trim());
                }
            }

            return uses;
        }

        /// <summary>A hard pointer or a hard owner: 340 to 349, 360 to 369, 390 to 399, 480 and 481, as the DXF reference names them.</summary>
        private static bool IsHard(int code) => (code >= 340 && code <= 349) || (code >= 360 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481;

        /// <summary>The table whose record a group names by name, as <see cref="CountUses"/> counts them; null for a group that names none.</summary>
        private static string NamedTable(bool entity, string type, int code)
        {
            switch (code)
            {
                case 8:
                    return entity ? "LAYER" : null;
                case 6:
                    return entity || type == "LAYER" || type == "MLINESTYLE" ? "LTYPE" : type == "DIMSTYLE" ? "BLOCK" : null;
                case 7:
                    return (entity && TextStyled.Contains(type)) || type == "TABLESTYLE" ? "STYLE" : type == "DIMSTYLE" ? "BLOCK" : null;
                case 3:
                    return entity && DimensionStyled.Contains(type) ? "DIMSTYLE" : null;
                case 2:
                    return entity && BlockDrawn.Contains(type) ? "BLOCK" : null;
                case 5:
                    return type == "DIMSTYLE" ? "BLOCK" : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The block an override of a dimension names by name at a group of its extended data, as one of R12 names its arrows
        /// (<c>1070 5</c>, <c>6</c> or <c>7</c>, then <c>1000</c> the block's name, in ACAD's DSTYLE); null for any other group.
        /// </summary>
        private string OverrideArrow(Rec record, List<Pair> pairs, int at)
        {
            if (pairs[at].Code != 1000 || at < 1 || pairs[at - 1].Code != 1070)
            {
                return null;
            }

            string variable = Text(record, pairs[at - 1]).Trim();

            if (variable != "5" && variable != "6" && variable != "7")
            {
                return null;
            }

            string name = DxfReader.NameOf(Text(record, pairs[at])).Trim();
            return name.Length == 0 ? null : name;
        }

        /// <summary>
        /// Whether an override of a dimension of model space, paper space or a block names a block by name as its arrow, as
        /// <see cref="OverrideArrow"/> reads one, letters of either case the same.
        /// </summary>
        internal bool NamesArrowInOverrides(string name)
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "ENTITIES" && section.Name != "BLOCKS")
                {
                    continue;
                }

                foreach (Rec record in Inside(section))
                {
                    List<Pair> pairs = PairsOf(record);
                    bool overrides = false;

                    for (int i = DataAt(record, pairs); i < pairs.Count; i++)
                    {
                        overrides = DimensionOverrides(record, pairs, i, overrides);

                        if (overrides && string.Equals(OverrideArrow(record, pairs, i), name, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>Grows the extents the header is to hold by an entity of model or paper space, as an entity appended grows them.</summary>
        internal void GrowExtentsBy(DxfItem item)
        {
            if (item.Space != null && item.Space.Kind != DxfSpaceKind.Block && !item.IsRemoved)
            {
                GrowBy(item);
            }
        }

        /// <summary>
        /// Copies records of a table of another drawing into this one by their names, those this drawing has not, as
        /// <see cref="Adopt"/> copies those an entity names: a layer, a linetype, a text style or a dimension style.
        /// </summary>
        internal void CopyTableRecords(DxfFile origin, string table, List<string> names) => CopyRecords(origin, names, table);

        /// <summary>
        /// Puts an entity not yet in a file in the place of one of this file, as AutoCAD's <c>handOverTo</c> gives an object's
        /// id to another: the new entity's records written where the old one's stand, in its space and at its place there, with
        /// the old one's handle, the groups of the applications of its record (its reactors, and its extension dictionary where
        /// kept), and its extended data where kept, else the new entity's own; a record that follows the new entity is owned by
        /// that handle. The old records' handles that are not kept, those of the records that followed it, are not given again,
        /// and an extension dictionary not kept is taken out with what it owns.
        /// </summary>
        internal void HandOver(DxfItem item, DxfItem from, bool keepData, bool keepDictionary)
        {
            Editable(item);
            DxfSpace space = item.Space;
            List<Rec> old = RecordsOf(item);
            Rec oldHead = old[0];
            List<Pair> oldPairs = PairsOf(oldHead);
            int oldData = DataAt(oldHead, oldPairs);
            var braces = new List<Pair>();
            bool braced = false;
            bool kept = true;

            for (int i = 1; i < oldData; i++)
            {
                Pair pair = oldPairs[i];

                if (pair.Code == 102)
                {
                    string said = Text(oldHead, pair).Trim();
                    bool opens = said.StartsWith("{", StringComparison.Ordinal);

                    if (opens)
                    {
                        braced = true;
                        kept = keepDictionary || said != "{ACAD_XDICTIONARY";
                    }

                    if (braced && kept)
                    {
                        braces.Add(pair);
                    }

                    braced = opens;
                    continue;
                }

                if (braced && kept)
                {
                    braces.Add(pair);
                }
            }

            string handle = item.Handle;
            string dictionary = keepDictionary ? null : ExtensionDictionaryOf(item);
            DxfItem written = Adopt(space, from);
            List<Rec> made = RecordsOf(written);
            Rec head = made[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            var b = new Builder(this);
            b.Raw(head, pairs[0]);
            bool placed = false;

            for (int i = 1; i < (keepData ? data : pairs.Count); i++)
            {
                Pair pair = pairs[i];

                // The groups of the applications follow the handle, as AutoCAD writes them; first, where the file writes no handles.
                if (!placed && (handle == null || pair.Code == 5))
                {
                    if (handle != null)
                    {
                        b.Raw(head, pair.Start, pair.ValueStart);
                        b.Value(handle);
                        b.Raw(head, pair.ValueEnd, pair.End);
                    }

                    foreach (Pair brace in braces)
                    {
                        b.Raw(oldHead, brace);
                    }

                    placed = true;

                    if (handle != null)
                    {
                        continue;
                    }
                }

                b.Raw(head, pair);
            }

            for (int i = keepData ? oldData : oldPairs.Count; i < oldPairs.Count; i++)
            {
                b.Raw(oldHead, oldPairs[i]);
            }

            var records = new List<Rec> { b.Done(head.Type) };

            for (int r = 1; r < made.Count; r++)
            {
                records.Add(handle != null && WithGroup(made[r], 330, handle, false, out Rec owned) ? owned : made[r]);
            }

            // The new entity's own handle goes with the record it was written as; its followers keep theirs.
            Retire(head);
            written.Home.Remove(written.Piece);
            written.Piece = new Piece(made);
            written.IsRemoved = true;
            space.Removed();

            for (int r = 1; r < old.Count; r++)
            {
                Retire(old[r]);
            }

            RetireFollowers(item);
            SetRecords(item, records);
            item.Changed(records[0].Type);
            TakeFollowers(written, item);

            if (dictionary != null && ObjectItem(dictionary) is DxfItem extension)
            {
                RemoveRecord(extension);
            }

            if (space.Kind != DxfSpaceKind.Block)
            {
                GrowBy(item);
            }

            Edited();
        }
    }
}
