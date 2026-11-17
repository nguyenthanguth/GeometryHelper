using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    public static partial class CadLayerEditor
    {
        /// <summary>
        /// Merges layers into another, as AutoCAD's LAYMRG does: every object on the layers merged, in model space, in the
        /// layouts, in every block (those of the blocks' definitions too) and the attributes of the block references, is put on
        /// the target layer, and the layers merged are deleted. A drawing with a line on layer WALL and another on layer DOOR,
        /// DOOR merged into WALL, has both lines on WALL afterwards and no layer DOOR. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="names">The names of the layers to merge.</param>
        /// <param name="target">The name of the layer they go into.</param>
        /// <returns>
        /// What came of it; refused, nothing changed, where a layer is not in the drawing, is the target itself, is layer 0 or
        /// Defpoints, the current layer or a layer of an external reference, or no layer is given. Failed, the drawing taken back,
        /// where a layer cannot be deleted after its objects are moved, as one a viewport of a layout freezes.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the names or the target is null.</exception>
        public static CadEditResult Merge(CadDocument document, IReadOnlyCollection<string> names, string target)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            DxfLayerTableRecord into = Find(document, target, out CadEditResult refused);

            if (into == null)
            {
                return refused;
            }

            if (into.IsDependent)
            {
                return Refuse("Invalid layer name.");
            }

            List<DxfLayerTableRecord> sources = Sources(document, names, "merged", out refused);

            if (sources == null)
            {
                return refused;
            }

            if (sources.Exists(s => string.Equals(s.Name, into.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Refuse("Cannot merge a layer with itself. Layer " + into.Name + " is already selected.");
            }

            string targetName = into.Name;
            List<DxfEntity> moved = EntitiesOn(document.Database, sources);
            List<CadEditRecord> planned = Planned(document, moved, CadEditRecordKind.PropertyChanged, targetName);

            foreach (DxfLayerTableRecord source in sources)
            {
                planned.Add(
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "Merge layer", source.Name, targetName)
                );
            }

            string[] merged = sources.Select(s => s.Name).ToArray();
            return document.ApplyEdit("LAYMRG", planned, s =>
            {
                foreach (DxfEntity entity in moved)
                {
                    if (entity.Handle == null || !s.IsHandled(entity.Handle))
                    {
                        entity.Layer = targetName;
                    }
                }

                foreach (string name in merged)
                {
                    s.Database.LayerTable[name].Erase();
                }
            });
        }

        /// <summary>
        /// Deletes layers with everything on them, as AutoCAD's LAYDEL does: every object on the layers, in model space, in the
        /// layouts and in every block (the blocks' definitions too), and the attributes of the block references, is erased,
        /// then the layers. A drawing with 12 lines on layer TMP and a block whose definition holds 4 more has none of the 16
        /// after TMP is deleted. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="names">The names of the layers to delete.</param>
        /// <returns>
        /// What came of it; refused, nothing changed, where a layer is not in the drawing, is layer 0 or Defpoints, the current
        /// layer or a layer of an external reference, or no layer is given. Failed, the drawing taken back, where a layer cannot
        /// be deleted after its objects are erased, as one a viewport of a layout freezes.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the names are null.</exception>
        public static CadEditResult DeleteWithObjects(CadDocument document, IReadOnlyCollection<string> names)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            List<DxfLayerTableRecord> sources = Sources(document, names, "deleted", out CadEditResult refused);

            if (sources == null)
            {
                return refused;
            }

            List<DxfEntity> erased = EntitiesOn(document.Database, sources);
            List<CadEditRecord> planned = Planned(document, erased, CadEditRecordKind.Erased, null);

            foreach (DxfLayerTableRecord source in sources)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete layer", source.Name, null));
            }

            string[] deleted = sources.Select(s => s.Name).ToArray();
            return document.ApplyEdit("LAYDEL", planned, s =>
            {
                foreach (DxfEntity entity in erased)
                {
                    // An entity may have gone with the one it belongs to (the attributes of a reference erased).
                    if (!entity.IsErased && (entity.Handle == null || !s.IsHandled(entity.Handle)))
                    {
                        entity.Erase();
                    }
                }

                foreach (string name in deleted)
                {
                    s.Database.LayerTable[name].Erase();
                }
            });
        }

        /// <summary>
        /// The layers named, in the order given with a name given twice once, each checked as LAYMRG and LAYDEL check it; null,
        /// <paramref name="refused"/> set, where one is not to be taken, or none is given.
        /// </summary>
        private static List<DxfLayerTableRecord> Sources(
            CadDocument document,
            IReadOnlyCollection<string> names,
            string what,
            out CadEditResult refused
        )
        {
            var sources = new List<DxfLayerTableRecord>();

            foreach (string name in names)
            {
                DxfLayerTableRecord layer = Find(document, name, out refused);

                if (layer == null)
                {
                    return null;
                }

                if (sources.Exists(s => string.Equals(s.Name, layer.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string layerName = layer.Name;

                if (string.Equals(layerName, document.Database.Clayer, StringComparison.OrdinalIgnoreCase))
                {
                    refused = Refuse("Layer \"" + layerName + "\" cannot be " + what + ": it is the current layer.");
                    return null;
                }

                if (layerName == "0" || string.Equals(layerName, "Defpoints", StringComparison.OrdinalIgnoreCase))
                {
                    refused = Refuse(
                        "Layer \"" + layerName + "\" cannot be " + what + ": layer 0 and Defpoints are kept."
                    );
                    return null;
                }

                if (layer.IsDependent)
                {
                    refused = Refuse(
                        "Layer \"" + layerName + "\" cannot be " + what + ": it is part of an external reference."
                    );
                    return null;
                }

                sources.Add(layer);
            }

            refused = sources.Count == 0 ? Refuse("No layer is selected.") : null;
            return sources.Count == 0 ? null : sources;
        }

        /// <summary>
        /// The objects on layers of the drawing, in the order of the blocks and of each block: those of model space, the
        /// layouts, every block, and the attributes of each block reference, the blocks of external references left out.
        /// </summary>
        private static List<DxfEntity> EntitiesOn(DxfDatabase database, IEnumerable<DxfLayerTableRecord> layers)
        {
            var wanted = new HashSet<string>(layers.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            var found = new List<DxfEntity>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (block.IsFromExternalReference)
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
                        foreach (DxfAttributeReference attribute in reference.AttributeCollection)
                        {
                            if (wanted.Contains(attribute.Layer))
                            {
                                found.Add(attribute);
                            }
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>The records of the objects an edit changes, those of the spaces only, which the canvas draws again from.</summary>
        private static List<CadEditRecord> Planned(
            CadDocument document,
            IEnumerable<DxfEntity> entities,
            CadEditRecordKind kind,
            string layer
        )
        {
            var planned = new List<CadEditRecord>();

            foreach (DxfEntity entity in entities)
            {
                if (entity.Handle != null && document.FindEntity(entity.Handle) != null)
                {
                    planned.Add(
                        kind == CadEditRecordKind.Erased
                            ? new CadEditRecord(kind, entity.Handle)
                            : new CadEditRecord(kind, entity.Handle, "Layer", entity.Layer, layer)
                    );
                }
            }

            return planned;
        }
    }
}
