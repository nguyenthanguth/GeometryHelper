using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>entmod</c> of a record of a table (a layer, linetype, text style, dimension style, UCS, view, viewport or
    /// application) and of an XRECORD, as AutoCAD 2021 changes them (the L8 measurements m/entmod, e1 to e4). The list must be
    /// that of a record, with both subclass markers as <c>entget</c> gives it; the groups it gives take the record's values: a
    /// layer's colour 62 (negative for off), flags 70 (1 frozen, 4 locked, the current layer too), linetype 6 (one not loaded is
    /// nil and the line <c>Error: Undefined line type X in LayerTableRecord Y</c>), plot flag 290 and lineweight 370 (one that is
    /// not a lineweight is left out and the list is still taken); a text style's font 3, big font 4, height 40, width factor 41,
    /// slant 50 in radians, generation 71 (a 70 with the shape-file bit is nil); a linetype's description, length and dashes; any
    /// other group of a dimension style, UCS, view or viewport as it is given. Group 2 renames where AutoCAD renames: not layer 0,
    /// Standard, Continuous or an application, not a name another record has, not one the table cannot hold. A refusal is nil and
    /// changes nothing. The change is one step with the routine's, as that of an entity is, and the window's layer list follows
    /// the table's change. An XRECORD takes the groups after its subclass marker <b>added</b> to its data, as AutoCAD adds them,
    /// the flag 280 changing in place. The records of a DICTIONARY and of a BLOCK_RECORD are not changed (parked).
    /// </summary>
    internal static partial class LispDrawing
    {
        private static readonly string[] ModifiableRecords =
        {
            "LAYER",
            "LTYPE",
            "STYLE",
            "DIMSTYLE",
            "UCS",
            "VIEW",
            "VPORT",
            "APPID",
            "XRECORD",
        };

        /// <summary>Whether an item is a record <c>entmod</c> changes by these rules: a table record that is not a block's, or an XRECORD.</summary>
        private static bool IsModifiableRecord(DxfItem item) =>
            item != null
            && item.Space == null
            && item.Parent == null
            && Array.IndexOf(ModifiableRecords, item.Type) >= 0;

        /// <summary>Changes a record to the groups of an entity list; the list as given, or nil where the change is refused.</summary>
        private static object ModifyRecord(
            LispInterpreter lisp,
            CadCommandSession session,
            DxfItem item,
            List<KeyValuePair<int, object>> given,
            object list
        )
        {
            int typePair = given.FindIndex(p => p.Key == 0);

            if (
                typePair >= 0
                && !(
                    given[typePair].Value is string type
                    && string.Equals(type, item.Type, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }

            string[] kind = Array.Find(RecordMarkers, m => m[0] == item.Type);

            // A record is taken from the list entget gives, with both subclass markers; a short list is nil (AutoCAD 2021, e1).
            if (kind != null && !(HasMarker(given, "AcDbSymbolTableRecord") && HasMarker(given, kind[1])))
            {
                return null;
            }

            CadDocument document = session.Document;
            List<KeyValuePair<int, object>> changes = ChangesOf(given);
            Action<CadEditScope> make;

            switch (item.Type)
            {
                case "LAYER":
                    make = ChangeLayer(lisp, document, item, changes);
                    break;
                case "STYLE":
                    make = ChangeTextStyle(document, item, changes);
                    break;
                case "XRECORD":
                    make = ChangeXrecord(item, list);
                    break;
                default:
                    make = ChangeRecord(document, item, changes);
                    break;
            }

            if (make == null)
            {
                return null;
            }

            session.OpenLispStep();
            session.HoldLispStep();
            var planned = new[]
            {
                new CadEditRecord(
                    CadEditRecordKind.TableChanged,
                    null,
                    "Modify " + item.Type,
                    null,
                    NameOfRecord(item)
                ),
            };
            return session.Apply("ENTMOD", planned, make).Succeeded ? list : null;
        }

        private static object LastOf(List<KeyValuePair<int, object>> changes, int code) =>
            changes.FindLast(p => p.Key == code).Value;

        private static bool Has(List<KeyValuePair<int, object>> changes, int code) =>
            changes.Exists(p => p.Key == code);

        /// <summary>A real group's value: a real, or a whole number as the real it is; false for any other kind.</summary>
        private static bool TryReal(object value, out double real)
        {
            real =
                value is double d ? d
                : value is int i ? i
                : double.NaN;
            return !double.IsNaN(real) && !double.IsInfinity(real);
        }

        /// <summary>
        /// The new name a list gives a record, the spaces round it taken off; null where the list gives none or the old one in the
        /// same letters (nothing to rename); false where the name is no string, or AutoCAD refuses it: one the table cannot hold,
        /// or one another record of the table has, letters of either case the same. A name of other letters only is a rename.
        /// </summary>
        private static bool TryRename(
            List<KeyValuePair<int, object>> changes,
            string name,
            Func<string, bool> taken,
            out string wanted
        )
        {
            wanted = null;

            if (!Has(changes, 2))
            {
                return true;
            }

            if (!(LastOf(changes, 2) is string text))
            {
                return false;
            }

            string trimmed = text.Trim();

            if (string.Equals(trimmed, name, StringComparison.Ordinal))
            {
                return true;
            }

            if (!CadLayerEditor.IsLayerName(trimmed))
            {
                return false;
            }

            if (!string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase) && taken(trimmed))
            {
                return false;
            }

            wanted = trimmed;
            return true;
        }

        private static Action<CadEditScope> ChangeLayer(
            LispInterpreter lisp,
            CadDocument document,
            DxfItem item,
            List<KeyValuePair<int, object>> changes
        )
        {
            string name = NameOfRecord(item);
            DxfDatabase database = document.Database;

            if (!database.LayerTable.Has(name))
            {
                return null;
            }

            if (
                (Has(changes, 62) && !(LastOf(changes, 62) is int))
                || (Has(changes, 70) && !(LastOf(changes, 70) is int))
                || (Has(changes, 290) && !(LastOf(changes, 290) is int))
                || (Has(changes, 370) && !(LastOf(changes, 370) is int))
                || (Has(changes, 6) && !(LastOf(changes, 6) is string))
            )
            {
                return null;
            }

            bool hasColour = Has(changes, 62);
            bool hasFlags = Has(changes, 70);
            bool hasPlot = Has(changes, 290);
            int colour = hasColour ? (int)LastOf(changes, 62) : 0;
            int flags = hasFlags ? (int)LastOf(changes, 70) : 0;
            int plotted = hasPlot ? (int)LastOf(changes, 290) : 0;
            string linetype = Has(changes, 6) ? (string)LastOf(changes, 6) : null;

            // A value that is no lineweight is left out, the rest taken (AutoCAD 2021: 370 of 55 gives the list and changes nothing).
            bool hasWeight = Has(changes, 370) && Enum.IsDefined(typeof(DxfLineWeight), (int)LastOf(changes, 370));
            int weight = hasWeight ? (int)LastOf(changes, 370) : 0;

            // A colour of 0 is accepted by AutoCAD and kept as 0: a layer here has the colours 1 to 255 (parked).
            if (hasColour && (Math.Abs(colour) < 1 || Math.Abs(colour) > 255))
            {
                return null;
            }

            if (linetype != null && !database.LinetypeTable.Has(linetype))
            {
                lisp.Write("Error: Undefined line type " + linetype + " in LayerTableRecord " + name);
                return null;
            }

            if (
                !TryRename(changes, name, n => database.LayerTable.Has(n), out string wanted)
                || (wanted != null && (name == "0" || database.LayerTable[name].IsDependent))
            )
            {
                return null;
            }

            return s =>
            {
                DxfLayerTableRecord layer = s.Database.LayerTable[name];

                if (hasColour)
                {
                    layer.ColorIndex = Math.Abs(colour);
                    layer.IsOff = colour < 0;
                }

                if (hasFlags)
                {
                    layer.IsFrozen = (flags & 1) != 0;
                    layer.IsLocked = (flags & 4) != 0;
                }

                if (linetype != null)
                {
                    layer.LinetypeName = s.Database.LinetypeTable[linetype].Name;
                }

                if (hasPlot)
                {
                    layer.IsPlottable = plotted != 0;
                }

                if (hasWeight)
                {
                    layer.LineWeight = (DxfLineWeight)weight;
                }

                if (wanted != null)
                {
                    layer.Name = wanted;
                }
            };
        }

        private static Action<CadEditScope> ChangeTextStyle(
            CadDocument document,
            DxfItem item,
            List<KeyValuePair<int, object>> changes
        )
        {
            string name = NameOfRecord(item);
            DxfDatabase database = document.Database;

            if (!database.TextStyleTable.Has(name))
            {
                return null;
            }

            double height = 0.0;
            double width = 0.0;
            double slant = 0.0;
            bool hasHeight = Has(changes, 40);
            bool hasWidth = Has(changes, 41);
            bool hasSlant = Has(changes, 50);
            bool hasGeneration = Has(changes, 71);
            bool hasFlags = Has(changes, 70);

            if (
                (hasHeight && !TryReal(LastOf(changes, 40), out height))
                || (hasWidth && !TryReal(LastOf(changes, 41), out width))
                || (hasSlant && !TryReal(LastOf(changes, 50), out slant))
                || (hasGeneration && !(LastOf(changes, 71) is int))
                || (hasFlags && !(LastOf(changes, 70) is int))
                || (Has(changes, 3) && !(LastOf(changes, 3) is string))
                || (Has(changes, 4) && !(LastOf(changes, 4) is string))
            )
            {
                return null;
            }

            // AutoCAD 2021 (e2): a 70 with the shape-file bit is nil; a slant is kept as the angle it wraps to (15 radians is 2.43363).
            // A width of nought or less, a slant past 85 degrees and a height below nought, which AutoCAD takes, a style here cannot
            // hold: they are nil (parked).
            slant = hasSlant ? slant % (2.0 * Math.PI) : 0.0;

            if (
                (hasFlags && ((int)LastOf(changes, 70) & 1) != 0)
                || (hasWidth && !(width > 0.0))
                || (hasHeight && height < 0.0)
                || (hasSlant && !(Math.Abs(slant) <= (85.0 * Math.PI / 180.0) + 1E-12))
            )
            {
                return null;
            }

            if (
                !TryRename(changes, name, n => database.TextStyleTable.Has(n), out string wanted)
                || (wanted != null && string.Equals(name, "Standard", StringComparison.OrdinalIgnoreCase))
            )
            {
                return null;
            }

            string file = Has(changes, 3) ? (string)LastOf(changes, 3) : null;
            string big = Has(changes, 4) ? (string)LastOf(changes, 4) : null;
            int generation = hasGeneration ? (int)LastOf(changes, 71) : 0;
            int flags = hasFlags ? (int)LastOf(changes, 70) : 0;
            return s =>
            {
                DxfTextStyleTableRecord style = s.Database.TextStyleTable[name];

                if (file != null)
                {
                    style.FileName = file;
                }

                if (big != null)
                {
                    style.BigFontFileName = big;
                }

                if (hasHeight)
                {
                    style.TextSize = height;
                }

                if (hasWidth)
                {
                    style.XScale = width;
                }

                if (hasSlant)
                {
                    style.ObliquingAngle = slant;
                }

                if (hasGeneration)
                {
                    style.FlagBits = (byte)generation;
                }

                if (hasFlags)
                {
                    style.IsVertical = (flags & 4) != 0;
                }

                if (wanted != null)
                {
                    style.Name = wanted;
                }
            };
        }

        /// <summary>
        /// A linetype, dimension style, UCS, view, viewport or application changed as the groups are given: each group takes the
        /// place of the record's own of its code (a dimension style's 270 and 287 are the file's 277 and 289; the flags 70 of a
        /// dimension style and an application are left as they are). Group 2 renames a linetype (not Continuous), dimension style
        /// (not Standard), UCS, view or viewport; an application is not renamed, and the active viewport (*Active) is not changed.
        /// </summary>
        private static Action<CadEditScope> ChangeRecord(
            CadDocument document,
            DxfItem item,
            List<KeyValuePair<int, object>> changes
        )
        {
            string name = NameOfRecord(item);
            string table = item.Type;
            DxfDatabase database = document.Database;

            if (
                table == "APPID"
                && Has(changes, 2)
                && !string.Equals(LastOf(changes, 2) as string, name, StringComparison.Ordinal)
            )
            {
                return null;
            }

            if (
                (table == "LTYPE" && LinetypeKeeps(name))
                || (table == "VPORT" && string.Equals(name, "*Active", StringComparison.OrdinalIgnoreCase))
            )
            {
                return null;
            }

            if (
                !TryRename(changes, name, n => IndexOfRecord(RecordsOf(document, table), n) >= 0, out string wanted)
                || (
                    wanted != null
                    && table == "DIMSTYLE"
                    && (string.Equals(name, "Standard", StringComparison.OrdinalIgnoreCase) || wanted.IndexOf('$') >= 0)
                )
            )
            {
                return null;
            }

            var applied = new List<KeyValuePair<int, object>>();

            foreach (KeyValuePair<int, object> change in changes)
            {
                if (table == "DIMSTYLE" && (change.Key == 5 || change.Key == 6 || change.Key == 7))
                {
                    continue;
                }

                int code =
                    table != "DIMSTYLE" ? change.Key
                    : change.Key == 270 ? 277
                    : change.Key == 287 ? 289
                    : change.Key;

                if (code == 2 || code == 105 || (code == 70 && (table == "DIMSTYLE" || table == "APPID")))
                {
                    continue;
                }

                applied.Add(LispEntityData.Pair(code, change.Value));
            }

            List<KeyValuePair<int, object>> record = LispEntityData.Read(item.Groups, table);
            LispEntityData.Apply(table, record, applied);
            bool inGroups = table == "UCS" || table == "VIEW" || table == "VPORT";

            if (wanted != null && inGroups)
            {
                int at = record.FindIndex(p => p.Key == 2);

                if (at < 0)
                {
                    return null;
                }

                record[at] = LispEntityData.Pair(2, wanted);
            }

            List<KeyValuePair<int, string>> groups = LispEntityData.ToGroups(table, record);
            return s =>
            {
                item.File.ReplaceTail(item, 0, groups);

                // A linetype and a dimension style are renamed through their table, which every drawing of them follows.
                if (wanted != null && table == "LTYPE")
                {
                    s.Database.LinetypeTable[name].Name = wanted;
                }
                else if (wanted != null && table == "DIMSTYLE")
                {
                    s.Database.DimStyleTable[name].Name = wanted;
                }
            };
        }

        /// <summary>Whether a linetype is one AutoCAD keeps as it is: ByLayer, ByBlock and Continuous.</summary>
        private static bool LinetypeKeeps(string name) => CadLinetypes.IsKept(name);

        /// <summary>
        /// An XRECORD that takes the groups after the subclass marker <c>(100 . "AcDbXrecord")</c> of the list <b>added</b> to its
        /// data, after what it holds (AutoCAD 2021, e3: <c>(subst (cons 1 "b") (assoc 1 r) r)</c> of a record of <c>(1 . "a")
        /// (70 . 5)</c> gives <c>(1 . "a") (70 . 5) (1 . "b") (70 . 5)</c>), the flag of cloning (280) given first changing in
        /// place; nil for a list with no marker, and <c>bad DXF group</c> for a group no XRECORD holds, as <c>entmake</c> says it.
        /// </summary>
        private static Action<CadEditScope> ChangeXrecord(DxfItem item, object list)
        {
            var body = new List<LispCons>();
            bool marked = false;

            for (object cell = list; cell is LispCons cons; cell = cons.Cdr)
            {
                if (!(cons.Car is LispCons group) || !(group.Car is int code))
                {
                    return null;
                }

                if (!marked && code == 100)
                {
                    if (
                        !(group.Cdr is string said)
                        || !string.Equals(said, "AcDbXrecord", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return null;
                    }

                    marked = true;
                }
                else if (marked)
                {
                    body.Add(group);
                }
            }

            if (!marked)
            {
                return null;
            }

            List<KeyValuePair<int, string>> groups = XrecordGroups(body);

            if (groups == null)
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> own = item.Groups;
            int marker = -1;

            for (int i = 0; i < own.Count && marker < 0; i++)
            {
                if (
                    own[i].TypeCode == 100
                    && string.Equals(own[i].Value, "AcDbXrecord", StringComparison.OrdinalIgnoreCase)
                )
                {
                    marker = i;
                }
            }

            if (marker < 0)
            {
                return null;
            }

            // What the record holds after its marker, the flag 280 changed in place where the list gives one first, then the new groups.
            var tail = new List<KeyValuePair<int, string>>();

            for (int i = marker + 1; i < own.Count && own[i].TypeCode < 1000; i++)
            {
                tail.Add(new KeyValuePair<int, string>(own[i].TypeCode, own[i].Value));
            }

            if (groups.Count > 0 && groups[0].Key == 280)
            {
                int flag = tail.FindIndex(p => p.Key == 280);

                if (flag >= 0)
                {
                    tail[flag] = groups[0];
                }
                else
                {
                    tail.Insert(0, groups[0]);
                }

                groups.RemoveAt(0);
            }

            tail.AddRange(groups);
            return s => item.File.ReplaceTail(item, marker + 1, tail);
        }
    }
}
