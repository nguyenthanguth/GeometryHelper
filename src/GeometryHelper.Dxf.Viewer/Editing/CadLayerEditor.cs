using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The edits of the layer table, as the layer box, the Layer manager and the LAYER command make them: each one undo step
    /// through <see cref="CadDocument.ApplyEdit"/>, refused as AutoCAD refuses it where AutoCAD would not do it.
    /// </summary>
    public static class CadLayerEditor
    {
        /// <summary>Gets whether a layer of the drawing is locked; false for a layer it has not.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The layer's name.</param>
        /// <returns>True when locked.</returns>
        public static bool IsLocked(DxfDatabase database, string name)
        {
            if (database == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            DxfLayerTableRecord layer = database.LayerTable.Has(name) ? database.LayerTable[name] : null;
            return layer != null && layer.IsLocked;
        }

        /// <summary>Makes a layer current, as the layer box does; a frozen layer is refused, as AutoCAD refuses it.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetCurrent(CadDocument document, string name)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            if (layer.IsFrozen)
            {
                return Refuse("Cannot set a frozen layer current.");
            }

            string old = document.Database.Clayer;
            string set = layer.Name;
            return document.ApplyEdit("LAYER", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Current layer", old, set) }, s => s.Database.Clayer = set);
        }

        /// <summary>Adds a layer of the colour White (7) and the linetype Continuous, as AutoCAD's New layer.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name.</param>
        /// <returns>What came of it; refused where the drawing has a layer of the name, either case.</returns>
        public static CadEditResult Add(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Refuse("A layer needs a name.");
            }

            string trimmed = name.Trim();

            if (document.Database.LayerTable.Has(trimmed))
            {
                return Refuse("Layer \"" + trimmed + "\" already exists.");
            }

            return document.ApplyEdit("LAYER", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layer", null, trimmed) }, s => s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = trimmed, ColorIndex = 7 }));
        }

        /// <summary>Switches a layer on or off; the current layer may be switched off, as in AutoCAD.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="on">Whether it is to be on.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetOn(CadDocument document, string name, bool on)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            return Set(document, layer, "On", !layer.IsOff, on, l => l.IsOff = !on);
        }

        /// <summary>Freezes or thaws a layer; the current layer is not frozen, as AutoCAD refuses it.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="frozen">Whether it is to be frozen.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetFrozen(CadDocument document, string name, bool frozen)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            if (frozen && string.Equals(layer.Name, document.Database.Clayer, StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("Cannot freeze layer \"" + layer.Name + "\".  It is the CURRENT layer.");
            }

            return Set(document, layer, "Freeze", layer.IsFrozen, frozen, l => l.IsFrozen = frozen);
        }

        /// <summary>Locks or unlocks a layer: the objects on a locked layer are drawn but not changed.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="locked">Whether it is to be locked.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetLocked(CadDocument document, string name, bool locked)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            return Set(document, layer, "Lock", layer.IsLocked, locked, l => l.IsLocked = locked);
        }

        /// <summary>Gives a layer a colour of AutoCAD's index, 1 to 255.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="colorIndex">The index.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetColor(CadDocument document, string name, int colorIndex)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            if (colorIndex < 1 || colorIndex > 255)
            {
                return Refuse("A layer's colour is 1 to 255.");
            }

            string old = layer.ColorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string set = colorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string layerName = layer.Name;
            return document.ApplyEdit("LAYER", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Color of " + layerName, old, set) }, s => s.Database.LayerTable[layerName].ColorIndex = (short)colorIndex);
        }

        /// <summary>
        /// Deletes a layer, as the Layer manager deletes one: only a layer nothing uses. Refused, with the reason, for layer 0,
        /// Defpoints, the current layer, a layer of an external reference (Base|Steel), a layer an entity is on, in any space or
        /// block, and a layer an object points at, as a viewport that freezes it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            string layerName = layer.Name;

            if (layerName == "0" || string.Equals(layerName, "Defpoints", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("Layer \"" + layerName + "\" cannot be deleted: layer 0 and Defpoints are kept.");
            }

            if (string.Equals(layerName, document.Database.Clayer, StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("Layer \"" + layerName + "\" cannot be deleted: it is the current layer.");
            }

            if (layer.IsDependent)
            {
                return Refuse("Layer \"" + layerName + "\" cannot be deleted: it is part of an external reference.");
            }

            if (layer.IsUsed)
            {
                return Refuse("Layer \"" + layerName + "\" cannot be deleted: objects are on it.");
            }

            return document.ApplyEdit("LAYER", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete layer", layerName, null) }, s => s.Database.LayerTable[layerName].Erase());
        }

        /// <summary>
        /// Gives a layer a linetype the drawing has, as the Layer manager does; one it has not is refused, as the library keeps
        /// no standard linetypes of its own to load one from (AutoCAD's acad.lin is Autodesk's and is not shipped).
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="linetype">The linetype's name, as Continuous or DASHED.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetLinetype(CadDocument document, string name, string linetype)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            string wanted = (linetype ?? string.Empty).Trim();

            if (wanted.Length == 0 || string.Equals(wanted, "ByLayer", StringComparison.OrdinalIgnoreCase) || string.Equals(wanted, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse("A layer's linetype is one of the drawing's linetypes, not ByLayer or ByBlock.");
            }

            if (!document.Database.LinetypeTable.Has(wanted))
            {
                return Refuse("The drawing has no linetype \"" + wanted + "\": load it into the drawing first.");
            }

            string set = document.Database.LinetypeTable[wanted].Name;
            string layerName = layer.Name;
            var record = new CadEditRecord(CadEditRecordKind.TableChanged, null, "Linetype of " + layerName, layer.LinetypeName, set);
            return document.ApplyEdit("LAYER", new[] { record }, s => s.Database.LayerTable[layerName].LinetypeName = set);
        }

        /// <summary>Gives a layer a lineweight: Default, or one of AutoCAD's widths; ByLayer and ByBlock are refused, as a layer cannot take them.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The layer's name.</param>
        /// <param name="lineWeight">The lineweight.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetLineWeight(CadDocument document, string name, DxfLineWeight lineWeight)
        {
            DxfLayerTableRecord layer = Find(document, name, out CadEditResult refused);

            if (layer == null)
            {
                return refused;
            }

            if (lineWeight == DxfLineWeight.ByLayer || lineWeight == DxfLineWeight.ByBlock || lineWeight == DxfLineWeight.ByDIPs || !Enum.IsDefined(typeof(DxfLineWeight), lineWeight))
            {
                return Refuse("A layer's lineweight is Default or one of AutoCAD's widths, not ByLayer or ByBlock.");
            }

            string layerName = layer.Name;
            var record = new CadEditRecord(CadEditRecordKind.TableChanged, null, "Lineweight of " + layerName, layer.LineWeight.ToString(), lineWeight.ToString());
            return document.ApplyEdit("LAYER", new[] { record }, s => s.Database.LayerTable[layerName].LineWeight = lineWeight);
        }

        private static CadEditResult Set(CadDocument document, DxfLayerTableRecord layer, string what, bool old, bool value, Action<DxfLayerTableRecord> write)
        {
            string layerName = layer.Name;
            var record = new CadEditRecord(CadEditRecordKind.TableChanged, null, what + " " + layerName, old ? "Yes" : "No", value ? "Yes" : "No");
            return document.ApplyEdit("LAYER", new[] { record }, s => write(s.Database.LayerTable[layerName]));
        }

        private static DxfLayerTableRecord Find(CadDocument document, string name, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfLayerTableRecord layer = string.IsNullOrWhiteSpace(name) || !document.Database.LayerTable.Has(name.Trim()) ? null : document.Database.LayerTable[name.Trim()];
            refused = layer == null ? Refuse("Cannot find layer \"" + name + "\".") : null;
            return layer;
        }

        private static CadEditResult Refuse(string message) => CadEditResult.Refuse(message);
    }
}
