using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    public static partial class CadLayerEditor
    {
        /// <summary>
        /// Translates layers, as AutoCAD's Layer Translator does for the mappings made: the layers translated to are made where
        /// the drawing has none (with the linetype copied from the standards file where the drawing has none) and given the
        /// colour, linetype, lineweight, transparency and plot style of the mapping where it has them; every object of a layer
        /// translated, in model space, the layouts and the blocks (those of the blocks left out where the settings say so), the
        /// attributes of block references among them, is put on the layer it is translated to; the layers translated from are
        /// deleted but for layer 0, Defpoints and any layer another mapping translates to. A drawing with 40 lines on OLD-WALL
        /// and a mapping of OLD-WALL to WALL (colour 3) has the 40 lines on WALL, WALL green, and no layer OLD-WALL afterwards.
        /// One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="mappings">The mappings, each a layer of the drawing and the layer it goes to.</param>
        /// <param name="settings">The translator's settings: what to force to ByLayer, and whether blocks are translated.</param>
        /// <param name="log">Receives the lines of the transaction log, in order; may be null.</param>
        /// <returns>
        /// What came of it; refused, nothing changed, where no layer is mapped, a layer is not in the drawing or belongs to an
        /// external reference, or a name to translate to is not one AutoCAD takes. Failed, the drawing taken back, where a layer
        /// cannot be deleted after its objects are moved.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the mappings or the settings are null.</exception>
        public static CadEditResult Translate(
            CadDocument document,
            IReadOnlyList<CadLayerMapping> mappings,
            CadLayerTranslatorSettings settings,
            IList<string> log
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DxfDatabase database = document.Database;
            var targets = new List<string>();
            var specs = new Dictionary<string, CadTranslateLayer>(StringComparer.OrdinalIgnoreCase);
            var goes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var kept = new List<string>();

            foreach (CadLayerMapping mapping in mappings)
            {
                DxfLayerTableRecord from = Find(document, mapping.From, out CadEditResult refused);

                if (from == null)
                {
                    return refused;
                }

                string to = mapping.To.Name.Trim();

                if (!IsLayerName(to))
                {
                    return Refuse("\"" + to + "\" is an invalid layer name.");
                }

                if (from.IsDependent)
                {
                    return Refuse(
                        "Layer \"" + from.Name + "\" is part of an external reference and is not translated."
                    );
                }

                if (database.LayerTable.Has(to) && database.LayerTable[to].IsDependent)
                {
                    return Refuse("Layer \"" + to + "\" is part of an external reference and takes no objects.");
                }

                string target = database.LayerTable.Has(to) ? database.LayerTable[to].Name : to;
                bool same = string.Equals(from.Name, target, StringComparison.OrdinalIgnoreCase);

                if (
                    !same
                    && (from.Name == "0" || string.Equals(from.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                )
                {
                    kept.Add(from.Name);
                    continue;
                }

                goes[from.Name] = target;

                if (!specs.ContainsKey(target))
                {
                    targets.Add(target);
                }

                specs[target] = mapping.To;
            }

            if (goes.Count == 0)
            {
                return Refuse("No layer is mapped.");
            }

            List<DxfEntity> moved = EntitiesOn(
                database,
                goes.Keys.Select(n => database.LayerTable[n]),
                settings.TranslateObjectsInBlocks
            );
            var planned = new List<CadEditRecord>();

            foreach (DxfEntity entity in moved)
            {
                if (entity.Handle != null && document.FindEntity(entity.Handle) != null)
                {
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            entity.Handle,
                            "Layer",
                            entity.Layer,
                            goes[entity.Layer]
                        )
                    );
                }
            }

            // Where the objects in blocks are left, the layers they are on stay.
            var left = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!settings.TranslateObjectsInBlocks)
            {
                var movedSet = new HashSet<DxfEntity>(moved);
                left.UnionWith(
                    EntitiesOn(database, goes.Keys.Select(n => database.LayerTable[n]))
                        .Where(e => !movedSet.Contains(e))
                        .Select(e => e.Layer)
                );
            }

            var deleted = goes
                .Keys.Where(n =>
                    !targets.Contains(n, StringComparer.OrdinalIgnoreCase)
                    && !left.Contains(n)
                    && n != "0"
                    && !string.Equals(n, "Defpoints", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();

            foreach (string name in targets)
            {
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        database.LayerTable.Has(name) ? "Translate to layer" : "New layer",
                        null,
                        name
                    )
                );
            }

            foreach (string name in deleted)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete layer", name, null));
            }

            if (log != null)
            {
                foreach (
                    string from in goes
                        .Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(n => n, StringComparer.Ordinal)
                )
                {
                    int count = moved.Count(e => string.Equals(e.Layer, from, StringComparison.OrdinalIgnoreCase));
                    log.Add(
                        "Layer "
                            + from
                            + (
                                string.Equals(from, goes[from], StringComparison.OrdinalIgnoreCase)
                                    ? " keeps its name: "
                                    : " translated to layer " + goes[from] + ": "
                            )
                            + count.ToString(CultureInfo.InvariantCulture)
                            + (count == 1 ? " object." : " objects.")
                    );
                }

                foreach (string name in targets.Where(t => !database.LayerTable.Has(t)))
                {
                    log.Add("Layer " + name + " created.");
                }

                foreach (string name in deleted)
                {
                    log.Add("Layer " + name + " deleted.");
                }

                foreach (
                    string name in goes
                        .Keys.Where(n => left.Contains(n))
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                )
                {
                    log.Add("Layer " + name + " is kept: objects in blocks are still on it.");
                }

                foreach (string name in kept)
                {
                    log.Add("Layer " + name + " is kept: layer 0 and Defpoints are not translated to another layer.");
                }
            }

            // A viewport that freezes a layer is thawed first, so that the layer can be deleted.
            if (deleted.Count > 0)
            {
                CadViewportLayerEditor.SetFrozenInAllViewports(document, deleted, false);
            }

            string current = database.Clayer;
            return document.ApplyEdit("LAYTRANS", planned, s =>
            {
                foreach (string name in targets)
                {
                    WriteTranslated(s.Database, name, specs[name], log);
                }

                foreach (DxfEntity entity in moved)
                {
                    if (entity.Handle != null && s.IsHandled(entity.Handle))
                    {
                        continue;
                    }

                    entity.Layer = goes[entity.Layer];

                    if (settings.ForceColorByLayer && !entity.Color.IsByLayer)
                    {
                        entity.Color = DxfColor.ByLayer;
                    }

                    if (
                        settings.ForceLinetypeByLayer
                        && !string.Equals(entity.Linetype, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        entity.Linetype = "ByLayer";
                    }

                    if (settings.ForceTransparencyByLayer && !entity.Transparency.IsByLayer)
                    {
                        entity.Transparency = DxfTransparency.ByLayer;
                    }
                }

                // The current layer translated away is replaced by the layer it went to, a frozen one by layer 0.
                if (deleted.Contains(current, StringComparer.OrdinalIgnoreCase))
                {
                    string next = goes[s.Database.LayerTable[current].Name];
                    s.Database.Clayer = s.Database.LayerTable[next].IsFrozen ? "0" : next;
                }

                foreach (string name in deleted)
                {
                    s.Database.LayerTable[name].Erase();
                }
            });
        }

        /// <summary>
        /// Makes a layer a mapping translates to, or gives the one the drawing has the mapping's settings: the colour, lineweight,
        /// transparency, plot style where the drawing has one of that name, and the linetype, copied from the standards file where
        /// the drawing has none of the name and Continuous where the file has none either.
        /// </summary>
        private static void WriteTranslated(
            DxfDatabase database,
            string name,
            CadTranslateLayer spec,
            IList<string> log
        )
        {
            string linetype = spec.LinetypeName;

            if (
                string.IsNullOrEmpty(linetype)
                || (!database.LinetypeTable.Has(linetype) && !CadSymbolCopy.Linetype(database, spec.Source, linetype))
            )
            {
                if (
                    !string.IsNullOrEmpty(linetype)
                    && !linetype.Equals("Continuous", StringComparison.OrdinalIgnoreCase)
                )
                {
                    log?.Add(
                        "Linetype "
                            + linetype
                            + " is not in the drawing or the standards file: Continuous is used for layer "
                            + name
                            + "."
                    );
                }

                linetype = "Continuous";
            }

            if (!database.LayerTable.Has(name))
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = name, ColorIndex = 7 });
            }

            DxfLayerTableRecord layer = database.LayerTable[name];

            // A drawing older than AutoCAD 2004 has no true colours and no transparency: those of the mapping are left out.
            bool modern = database.Version == null || string.CompareOrdinal(database.Version, "AC1018") >= 0;

            if (layer.Color.ToString() != spec.Color.ToString() && (modern || !spec.Color.IsByColor))
            {
                layer.Color = spec.Color;
            }

            if (
                !string.Equals(
                    layer.LinetypeName,
                    database.LinetypeTable[linetype].Name,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                layer.LinetypeName = database.LinetypeTable[linetype].Name;
            }

            if (layer.LineWeight != spec.LineWeight)
            {
                layer.LineWeight = spec.LineWeight;
            }

            if (modern && TransparencyPercent(layer.Transparency) != spec.TransparencyPercent)
            {
                layer.Transparency = new DxfTransparency(
                    (byte)Math.Floor(255.0 * (100 - Math.Max(0, Math.Min(90, spec.TransparencyPercent))) / 100.0)
                );
            }

            DxfDBDictionary styles = database.PlotStyleNameDictionary;
            string plotStyle =
                string.IsNullOrEmpty(spec.PlotStyleName) || database.PlotStyleMode
                    ? null
                    : styles
                        ?.Select(e => e.Key)
                        .FirstOrDefault(k => string.Equals(k, spec.PlotStyleName, StringComparison.OrdinalIgnoreCase));

            if (plotStyle != null && !string.Equals(layer.PlotStyleName, plotStyle, StringComparison.Ordinal))
            {
                layer.PlotStyleName = plotStyle;
            }
        }

        /// <summary>
        /// The objects on layers of the drawing, as <see cref="EntitiesOn(DxfDatabase, IEnumerable{DxfLayerTableRecord})"/> finds
        /// them, those of the blocks' definitions left out where <paramref name="inBlocks"/> is false: only the objects of model
        /// space and the layouts, and the attributes of the block references in them.
        /// </summary>
        private static List<DxfEntity> EntitiesOn(
            DxfDatabase database,
            IEnumerable<DxfLayerTableRecord> layers,
            bool inBlocks
        )
        {
            if (inBlocks)
            {
                return EntitiesOn(database, layers);
            }

            var wanted = new HashSet<string>(layers.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            var found = new List<DxfEntity>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (block.IsFromExternalReference || !block.IsLayout)
                {
                    continue;
                }

                foreach (DxfEntity entity in block)
                {
                    if (entity.IsErased)
                    {
                        continue;
                    }

                    if (wanted.Contains(entity.Layer))
                    {
                        found.Add(entity);
                    }

                    if (entity is DxfBlockReference reference)
                    {
                        found.AddRange(reference.AttributeCollection.Where(a => wanted.Contains(a.Layer)));
                    }
                }
            }

            return found;
        }
    }
}
