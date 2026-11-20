using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The dimension variables as system variables, each typed alone at the command line or set by SETVAR as AutoCAD's: DIMASZ
    /// asks "Enter new value for DIMASZ &lt;2.5000&gt;:", DIMSAH takes ON or OFF, DIMBLK1 "Enter new value for DIMBLK1, or . for
    /// default &lt;""&gt;:" and takes the name of an arrowhead, DIMTXSTY a text style of the drawing. They are the variables of
    /// the header that new dimensions take (<see cref="DxfDatabase.GetDimstyleData"/>), a change one step of undo, and the ones
    /// that differ from the current style are its overrides, which -DIMSTYLE Save keeps as a style. DIMSCALE is among them but
    /// is registered with the dimensions and text.
    /// </summary>
    internal static partial class CadCommands
    {
        // The whole numbers of two variables WholeLimits (DIMOVERRIDE's own table) does not hold: the background of the text
        // (none, the drawing's colour, a colour) and the symbol of an arc length (before, above, none).
        private static readonly Dictionary<string, int[]> DimMoreWholeLimits = new Dictionary<string, int[]>(
            StringComparer.Ordinal
        )
        {
            ["Dimtfill"] = new[] { 0, 2 },
            ["Dimarcsym"] = new[] { 0, 2 },
        };

        private static void AddDimVariablesTo(CadCommandRegistry registry)
        {
            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                // DIMSCALE is kept with the dimensions and text, which register it after these.
                if (variable.Name != "Dimscale")
                {
                    registry.AddVariable(DimVariable(variable));
                }
            }
        }

        /// <summary>The name AutoCAD gives the variable: DIMASZ for Dimasz, but DIMFXL for the fixed length and DIMFXLON for whether it is on.</summary>
        private static string DimVariableName(DxfDimVariable variable) =>
            variable.Name == "Dimfxlen" ? "DIMFXL"
            : variable.Name == "DimfxlenOn" ? "DIMFXLON"
            : variable.Name.ToUpperInvariant();

        private static CadSystemVariable DimVariable(DxfDimVariable variable)
        {
            string name = DimVariableName(variable);

            switch (variable.Kind)
            {
                case DxfDimKind.Real:
                    return CadSystemVariable.Number(
                        name,
                        true,
                        Array.IndexOf(SizesAboveNought, variable.Name) >= 0
                            ? CadSystemVariable.RealBound.NonNegative
                            : CadSystemVariable.RealBound.Finite,
                        false,
                        s => (double)DimValue(s, variable),
                        (c, v) => DimSet(c.Database, variable, v)
                    );
                case DxfDimKind.Angle:
                    return CadSystemVariable.Angle(
                        name,
                        true,
                        s => (double)DimValue(s, variable),
                        (c, v) => DimSet(c.Database, variable, v),
                        null,
                        true
                    );
                case DxfDimKind.Whole:
                    int[] limits =
                        WholeLimits.TryGetValue(variable.Name, out int[] known) ? known
                        : DimMoreWholeLimits.TryGetValue(variable.Name, out int[] more) ? more
                        : new[] { 0, short.MaxValue };
                    return CadSystemVariable.Whole(
                        name,
                        true,
                        limits[0],
                        limits[1],
                        s => (int)DimValue(s, variable),
                        (c, v) => DimSet(c.Database, variable, v),
                        null,
                        (c, v) => DimRefused(name, variable, v)
                    );
                case DxfDimKind.Flag:
                    return CadSystemVariable.Switch(
                        name,
                        true,
                        s => (bool)DimValue(s, variable),
                        (c, v) => DimSet(c.Database, variable, v),
                        null
                    );
                case DxfDimKind.Colour:
                    return CadSystemVariable.Whole(
                        name,
                        true,
                        0,
                        256,
                        s => ((DxfColor)DimValue(s, variable)).ColorIndex,
                        (c, v) => DimSet(c.Database, variable, DxfColor.OfIndex(v))
                    );
                case DxfDimKind.Weight:
                    return CadSystemVariable.Whole(
                        name,
                        true,
                        -3,
                        211,
                        s => (int)(DxfLineWeight)DimValue(s, variable),
                        (c, v) => DimSet(c.Database, variable, (DxfLineWeight)v),
                        null,
                        LineweightRefused
                    );
                case DxfDimKind.Letter:
                    return CadSystemVariable.Text(
                        name,
                        true,
                        s => ((char)DimValue(s, variable)).ToString(),
                        (c, typed) =>
                            typed.Length == 1
                                ? DimChange(c, name, variable, typed, d => typed[0])
                                : "Cannot set " + name + " to that value."
                    );
                case DxfDimKind.Text:
                    return CadSystemVariable.Optional(
                        name,
                        "none",
                        s => (string)DimValue(s, variable),
                        (c, typed) => DimChange(c, name, variable, typed, d => typed),
                        true
                    );
                case DxfDimKind.Block:
                    return CadSystemVariable.Optional(
                        name,
                        "default",
                        s => DimArrowName(DimValue(s, variable) as DxfBlockTableRecord),
                        (c, typed) =>
                        {
                            string arrow = ArrowheadName(c.Database, typed);

                            return arrow == null
                                ? "Block \"" + typed + "\" not found."
                                : DimChange(
                                    c,
                                    name,
                                    variable,
                                    (CadArrowheads.BlockOf(arrow) ?? arrow).TrimStart('_'),
                                    d => CadArrowheads.Ensure(d, arrow)
                                );
                        },
                        true
                    );
                case DxfDimKind.Linetype:
                    return CadSystemVariable.Optional(
                        name,
                        "default",
                        s => (DimValue(s, variable) as DxfSymbolTableRecord)?.Name ?? string.Empty,
                        (c, typed) =>
                        {
                            string found =
                                typed.Length == 0
                                    ? string.Empty
                                    : c
                                        .Database.LinetypeTable.Select(r => r.Name)
                                        .FirstOrDefault(n =>
                                            string.Equals(n, typed, StringComparison.OrdinalIgnoreCase)
                                        );

                            return found == null
                                ? "Linetype \"" + typed + "\" not found."
                                : DimChange(c, name, variable, found, d =>
                                    found.Length == 0 ? null : d.LinetypeTable[found]
                                );
                        },
                        true
                    );
                default:
                    return CadSystemVariable.Text(
                        name,
                        true,
                        s => (DimValue(s, variable) as DxfSymbolTableRecord)?.Name ?? string.Empty,
                        (c, typed) =>
                        {
                            string found = c
                                .Database.TextStyleTable.Select(r => r.Name)
                                .FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase));

                            return found == null
                                ? "Cannot find text style \"" + typed + "\"."
                                : DimChange(c, name, variable, found, d => d.TextStyleTable[found]);
                        }
                    );
            }
        }

        /// <summary>The value of a dimension variable the drawing's header gives new dimensions.</summary>
        private static object DimValue(CadCommandSession session, DxfDimVariable variable) =>
            session.Document.Database.GetDimstyleData().Get(variable);

        /// <summary>Writes a value of a dimension variable into the header, as the dimension style data it makes up.</summary>
        private static void DimSet(DxfDatabase database, DxfDimVariable variable, object value)
        {
            DxfDimStyleTableRecord data = database.GetDimstyleData();
            data.Keep(variable, value);
            database.SetDimstyleData(data);
        }

        /// <summary>Why the header would not take a whole number the variable's own range let by (DIMAUNIT 4), or null.</summary>
        private static string DimRefused(string name, DxfDimVariable variable, int value)
        {
            try
            {
                DxfDatabase.CheckDimRange(variable, value, nameof(value));
                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return "Cannot set " + name + " to that value.";
            }
        }

        /// <summary>
        /// Makes a value of a variable of a text, a block, a linetype or a text style as one step of undo, the value made inside
        /// the edit, where the drawing may be given a block; gives null, or the reason the edit failed.
        /// </summary>
        private static string DimChange(
            CadCommandContext c,
            string name,
            DxfDimVariable variable,
            string shown,
            Func<DxfDatabase, object> make
        )
        {
            object old = DimValue(c.Session, variable);
            string before =
                old is DxfBlockTableRecord block ? DimArrowName(block)
                : old is DxfSymbolTableRecord record ? record.Name
                : old?.ToString() ?? string.Empty;

            // The value it has already changes nothing, and is no step of undo.
            if (string.Equals(before, shown, StringComparison.Ordinal))
            {
                return null;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$" + name, before, shown) },
                s =>
                {
                    object value = make(s.Database);

                    if (!variable.SameValue(old, value))
                    {
                        DimSet(s.Database, variable, value);
                    }
                }
            );

            return result.Succeeded ? null : result.Message;
        }

        /// <summary>The name a variable of an arrowhead shows: nothing for AutoCAD's own closed filled one, else its block's name without the underscore.</summary>
        private static string DimArrowName(DxfBlockTableRecord block) =>
            block == null ? string.Empty : CadArrowheads.ListedName(block);
    }
}
