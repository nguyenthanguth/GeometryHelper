using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// SETBYLAYER, as AutoCAD 2021 asks it (the prompts are those of its own string table): "Current active settings:" with the
    /// properties it will change, "Select objects or [Settings]:", then "Change ByBlock to ByLayer? [Yes/No] &lt;Yes&gt;:" and
    /// "Include blocks? [Yes/No] &lt;Yes&gt;:", and the words of the count: "3 objects modified, 1 object did not need to be
    /// changed." Settings asks the typed form of AutoCAD's dialog, "Enter a property to toggle
    /// [Color/LType/LWeight/Transparency/Material/PStyle] &lt;done&gt;:", each answer switching one property on or off in the
    /// variable SETBYLAYERMODE (1 colour, 2 linetype, 4 lineweight, 8 material, 16 plot style, 32 transparency, 64 change ByBlock to
    /// ByLayer, 128 include blocks; 255 in a new session). Each selected object has the properties on set to ByLayer; an object
    /// whose property is ByBlock is left alone where ByBlock is not to be changed. Included blocks are the objects of the block
    /// definition of each block reference selected, nested references too, and the attributes of the reference, which any other
    /// reference of the same block shows changed as well.
    /// </summary>
    internal static partial class CadCommands
    {
        private const int SetByLayerColor = 1;
        private const int SetByLayerLinetype = 2;
        private const int SetByLayerLineweight = 4;
        private const int SetByLayerMaterial = 8;
        private const int SetByLayerPlotStyle = 16;
        private const int SetByLayerTransparency = 32;
        private const int SetByLayerByBlock = 64;
        private const int SetByLayerBlocks = 128;

        private static readonly KeyValuePair<string, int>[] SetByLayerProperties =
        {
            new KeyValuePair<string, int>("Color", SetByLayerColor),
            new KeyValuePair<string, int>("LType", SetByLayerLinetype),
            new KeyValuePair<string, int>("LWeight", SetByLayerLineweight),
            new KeyValuePair<string, int>("Transparency", SetByLayerTransparency),
            new KeyValuePair<string, int>("Material", SetByLayerMaterial),
            new KeyValuePair<string, int>("PStyle", SetByLayerPlotStyle),
        };

        private static int SetByLayerMode(CadCommandSession session) => (int)Kept(session, "setbylayermode", 255.0);

        /// <summary>The properties of the mode that apply to the drawing: plot style only where it has named plot styles.</summary>
        private static int SetByLayerActive(CadCommandContext c)
        {
            int mode = SetByLayerMode(c.Session) & 63;
            return c.Database.PlotStyleMode ? mode & ~SetByLayerPlotStyle : mode;
        }

        /// <summary>"Current active settings: Color Linetype ..." for the properties the mode has on, "(empty)" for none.</summary>
        private static string SetByLayerSettingsText(CadCommandContext c)
        {
            int mode = SetByLayerActive(c);
            var names = new List<string>();

            foreach (KeyValuePair<string, int> property in SetByLayerProperties)
            {
                if ((mode & property.Value) != 0)
                {
                    names.Add(
                        property.Key == "LType" ? "Linetype"
                        : property.Key == "LWeight" ? "Lineweight"
                        : property.Key == "PStyle" ? "PlotStyle"
                        : property.Key
                    );
                }
            }

            return "Current active settings: " + (names.Count == 0 ? "(empty)" : string.Join(" ", names));
        }

        private static IEnumerable<CadPrompt> SetByLayer(CadCommandContext c)
        {
            c.Write(SetByLayerSettingsText(c));
            IReadOnlyList<string> handles;

            if (c.PreSelected.Count > 0)
            {
                handles = c.PreSelected;
                c.Session.RememberSelection(handles);
            }
            else
            {
                while (true)
                {
                    yield return CadPrompt.ForSelection("Select objects", new[] { "Settings" });

                    if (!c.Input.Is("Settings"))
                    {
                        break;
                    }

                    foreach (CadPrompt prompt in SetByLayerToggle(c))
                    {
                        yield return prompt;
                    }
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                handles = c.Input.Handles;
            }

            c.ClearsSelection = true;
            var chosen = new List<DxfEntity>();
            int locked = 0;

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                    continue;
                }

                chosen.Add(entity);
            }

            if (locked > 0)
            {
                c.Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            if (chosen.Count == 0)
            {
                yield break;
            }

            int mode = SetByLayerMode(c.Session);
            yield return CadPrompt.ForKeyword(
                "Change ByBlock to ByLayer?",
                new[] { "Yes", "No" },
                (mode & SetByLayerByBlock) != 0 ? "Yes" : "No"
            );
            bool byBlock = c.Input.Is("Yes") || (c.Input.IsNone && (mode & SetByLayerByBlock) != 0);

            if (c.Input.Kind != CadInputKind.Keyword && !c.Input.IsNone)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Include blocks?",
                new[] { "Yes", "No" },
                (mode & SetByLayerBlocks) != 0 ? "Yes" : "No"
            );
            bool blocks = c.Input.Is("Yes") || (c.Input.IsNone && (mode & SetByLayerBlocks) != 0);

            if (c.Input.Kind != CadInputKind.Keyword && !c.Input.IsNone)
            {
                yield break;
            }

            // The two answers are the dialog's two last boxes, kept in the variable as they are set.
            mode =
                (mode & ~(SetByLayerByBlock | SetByLayerBlocks))
                | (byBlock ? SetByLayerByBlock : 0)
                | (blocks ? SetByLayerBlocks : 0);
            c.Remember("setbylayermode", mode);
            int what = SetByLayerActive(c);

            if (what == 0)
            {
                c.Write("No properties selected to change.");
                yield break;
            }

            var reached = new List<DxfEntity>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inBlocks = new List<DxfEntity>();

            foreach (DxfEntity entity in chosen)
            {
                reached.Add(entity);

                if (blocks && entity is DxfBlockReference insert)
                {
                    SetByLayerNested(insert, seen, inBlocks);
                }
            }

            var planned = new List<CadEditRecord>();

            foreach (DxfEntity entity in chosen)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, entity.Handle, "SetByLayer"));
            }

            if (inBlocks.Count > 0)
            {
                // Objects of block definitions are seen through every reference of the block: the spaces are drawn again.
                planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "SetByLayer", null, "blocks"));
            }

            int changed = 0;
            int unchanged = 0;
            CadEditResult result = c.Apply(planned, s =>
            {
                changed = 0;
                unchanged = 0;

                foreach (DxfEntity entity in reached.Concat(inBlocks))
                {
                    if (entity.IsErased)
                    {
                        continue;
                    }

                    if (SetByLayerOne(c.Database, entity, what, byBlock))
                    {
                        changed++;
                    }
                    else
                    {
                        unchanged++;
                    }
                }
            });

            if (result.Succeeded)
            {
                c.Write(SetByLayerCount(changed, unchanged));
            }
        }

        /// <summary>The typed form of the Settings dialog: one property switched at a time until Enter.</summary>
        private static IEnumerable<CadPrompt> SetByLayerToggle(CadCommandContext c)
        {
            bool named = !c.Database.PlotStyleMode;

            while (true)
            {
                c.Write(SetByLayerSettingsText(c));
                CadPrompt ask = CadPrompt.ForKeyword(
                    "Enter a property to toggle",
                    named
                        ? new[] { "Color", "LType", "LWeight", "Transparency", "Material", "PStyle" }
                        : new[] { "Color", "LType", "LWeight", "Transparency", "Material" },
                    "done"
                );
                yield return named ? ask : ask.Hiding("PStyle");

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("PStyle") && !named)
                {
                    c.Write("Plot style cannot be changed when PSTYLEMODE is 1.");
                    continue;
                }

                foreach (KeyValuePair<string, int> property in SetByLayerProperties)
                {
                    if (c.Input.Is(property.Key))
                    {
                        c.Remember("setbylayermode", SetByLayerMode(c.Session) ^ property.Value);
                    }
                }
            }
        }

        /// <summary>The objects of the block definition of a reference, those of the blocks nested in it too, each block once, and the attributes of the reference.</summary>
        private static void SetByLayerNested(DxfBlockReference insert, HashSet<string> seen, List<DxfEntity> found)
        {
            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                found.Add(attribute);
            }

            DxfBlockTableRecord record = insert.BlockTableRecord;

            if (record == null || record.IsFromExternalReference || record.IsLayout || !seen.Add(record.Name))
            {
                return;
            }

            foreach (DxfEntity inside in record)
            {
                found.Add(inside);

                if (inside is DxfBlockReference nested)
                {
                    SetByLayerNested(nested, seen, found);
                }
            }
        }

        /// <summary>
        /// Sets the properties of an entity that the mode has on to ByLayer, ByBlock ones too where asked; whether any changed.
        /// </summary>
        private static bool SetByLayerOne(DxfDatabase db, DxfEntity entity, int what, bool byBlock)
        {
            bool changed = false;

            if ((what & SetByLayerColor) != 0 && !entity.Color.IsByLayer && (byBlock || !entity.Color.IsByBlock))
            {
                entity.Color = DxfColor.ByLayer;
                changed = true;
            }

            if (
                (what & SetByLayerLinetype) != 0
                && !string.Equals(entity.Linetype, "ByLayer", StringComparison.OrdinalIgnoreCase)
                && (byBlock || !string.Equals(entity.Linetype, "ByBlock", StringComparison.OrdinalIgnoreCase))
            )
            {
                entity.Linetype = "ByLayer";
                changed = true;
            }

            if (
                (what & SetByLayerLineweight) != 0
                && entity.LineWeight != DxfLineWeight.ByLayer
                && (byBlock || entity.LineWeight != DxfLineWeight.ByBlock)
            )
            {
                entity.LineWeight = DxfLineWeight.ByLayer;
                changed = true;
            }

            if (
                (what & SetByLayerTransparency) != 0
                && (entity.Transparency.IsByAlpha || (byBlock && entity.Transparency.IsByBlock))
            )
            {
                entity.Transparency = DxfTransparency.ByLayer;
                changed = true;
            }

            if ((what & SetByLayerMaterial) != 0 && SetByLayerPointed(db, entity, 347, db.MaterialDictionary, byBlock))
            {
                changed = true;
            }

            if (
                (what & SetByLayerPlotStyle) != 0
                && SetByLayerPointed(db, entity, 390, db.PlotStyleNameDictionary, byBlock)
            )
            {
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Takes the material or plot style an entity names out, so that it takes its layer's: nothing where it names none, or the
        /// entry called ByLayer, or ByBlock where ByBlock is not to be changed.
        /// </summary>
        private static bool SetByLayerPointed(
            DxfDatabase db,
            DxfEntity entity,
            int code,
            DxfDBDictionary names,
            bool byBlock
        )
        {
            DxfFile file = entity.Item.File;
            string handle = file.CommonValueOf(entity.Item, code)?.Trim();

            if (string.IsNullOrEmpty(handle))
            {
                return false;
            }

            foreach (string kept in new[] { "ByLayer", "ByBlock" })
            {
                if (
                    names != null
                    && names.Contains(kept)
                    && string.Equals(names.GetAt(kept).Handle, handle, StringComparison.OrdinalIgnoreCase)
                )
                {
                    if (kept == "ByLayer" || !byBlock)
                    {
                        return false;
                    }
                }
            }

            file.SetCommon(entity.Item, code, null);
            return true;
        }

        /// <summary>The line AutoCAD ends SETBYLAYER with, by how many objects changed and how many did not need to.</summary>
        private static string SetByLayerCount(int changed, int unchanged)
        {
            if (changed == 0)
            {
                return "All object properties already set to ByLayer; no objects modified.";
            }

            string done =
                changed == 1
                    ? "1 object modified"
                    : changed.ToString(CultureInfo.InvariantCulture) + " objects modified";

            if (unchanged == 0)
            {
                return done + ".";
            }

            return done
                + ", "
                + (unchanged == 1 ? "1 object" : unchanged.ToString(CultureInfo.InvariantCulture) + " objects")
                + " did not need to be changed.";
        }
    }
}
