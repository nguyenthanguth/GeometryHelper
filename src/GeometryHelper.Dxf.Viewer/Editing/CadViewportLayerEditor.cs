using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The edits of layers in the viewports of the layouts, as the VP columns of AutoCAD's Layer manager, VPLAYER and LAYVPI
    /// make them: a layer frozen or thawed in one viewport or in all, frozen in the viewports made from now on, and its
    /// colour, linetype, lineweight, transparency and plot style overridden in one viewport. Each is one undo step through
    /// <see cref="CadDocument.ApplyEdit"/>, refused before anything is written where AutoCAD would not do it.
    /// </summary>
    public static class CadViewportLayerEditor
    {
        /// <summary>
        /// Gets the viewport the VP columns act on, as AutoCAD's Layer manager does in a layout: the one worked through
        /// (<see cref="CadDocument.ActiveViewport"/>), else the one viewport selected on the paper of the layout shown; null on
        /// the Model tab, on the paper with no viewport alone selected, and for the sheet's own viewport.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>The viewport, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static DxfViewport TargetViewport(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.CurrentSpace == null || document.CurrentSpace.IsModel)
            {
                return null;
            }

            if (document.ActiveViewport != null)
            {
                return document.ActiveViewport;
            }

            IReadOnlyCollection<string> handles = document.Selection.Handles;

            foreach (string handle in handles)
            {
                return handles.Count == 1 && document.FindEntity(handle) is DxfViewport viewport && viewport.Number != 1
                    && ReferenceEquals(document.FindSpace(handle), document.CurrentSpace) ? viewport : null;
            }

            return null;
        }

        /// <summary>
        /// Gets the viewports of every layout, the sheets' own left out, in the order of the layout tabs and of each layout's
        /// block: those "in all viewports" acts on.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>The viewports, a new list.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<DxfViewport> LayoutViewports(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var found = new List<DxfViewport>();

            foreach (CadSpace space in document.Spaces)
            {
                if (space.IsModel || space.Block == null)
                {
                    continue;
                }

                foreach (DxfEntity entity in space.Block)
                {
                    if (entity is DxfViewport viewport && !viewport.IsErased && viewport.Number != 1)
                    {
                        found.Add(viewport);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Freezes or thaws layers in viewports, as the VP Freeze column and VPLAYER Freeze and Thaw do: each viewport whose
        /// frozen layers change has one record, the layers it freezes before and after; nothing changed is no edit. A layer is
        /// frozen in a viewport even where it is the current one, as AutoCAD allows.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="viewports">The handles of the viewports.</param>
        /// <param name="layers">The names of the layers.</param>
        /// <param name="frozen">Whether they are to be frozen there.</param>
        /// <param name="name">The name of the undo step, as LAYER or VPLAYER.</param>
        /// <returns>What came of it; refused for a viewport or a layer the drawing has not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the handles, the names or the step's name are null.</exception>
        public static CadEditResult SetFrozen(CadDocument document, IEnumerable<string> viewports, IEnumerable<string> layers, bool frozen, string name = "LAYER")
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (viewports == null)
            {
                throw new ArgumentNullException(nameof(viewports));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            List<string> names = Layers(document, layers, out CadEditResult refused);

            if (names == null)
            {
                return refused;
            }

            var records = new List<CadEditRecord>();
            var targets = new List<string>();

            foreach (string handle in viewports)
            {
                if (!(document.FindEntity(handle) is DxfViewport viewport) || viewport.IsErased)
                {
                    return CadEditResult.Refuse("The drawing has no viewport " + handle + ".");
                }

                string was = Frozen(viewport);
                bool changes = false;

                foreach (string layer in names)
                {
                    changes |= viewport.IsLayerFrozenInViewport(document.Database.LayerTable[layer]) != frozen;
                }

                if (changes && !targets.Contains(viewport.Handle))
                {
                    targets.Add(viewport.Handle);
                    records.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, viewport.Handle, "VP Freeze", was, Joined(Planned(viewport, names, frozen))));
                }
            }

            if (records.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return document.ApplyEdit(name, records, s =>
            {
                var picked = new List<DxfLayerTableRecord>();

                foreach (string layer in names)
                {
                    picked.Add(s.Database.LayerTable[layer]);
                }

                foreach (string handle in targets)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    var viewport = (DxfViewport)s.Document.FindEntity(handle);

                    if (frozen)
                    {
                        viewport.FreezeLayersInViewport(picked);
                    }
                    else
                    {
                        viewport.ThawLayersInViewport(picked);
                    }
                }
            });
        }

        /// <summary>Freezes or thaws layers in every viewport of every layout, as the Layer manager's "VP Freeze Layer in All Viewports" and "VP Thaw Layer in All Viewports".</summary>
        /// <param name="document">The document.</param>
        /// <param name="layers">The names of the layers.</param>
        /// <param name="frozen">Whether they are to be frozen.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the names are null.</exception>
        public static CadEditResult SetFrozenInAllViewports(CadDocument document, IEnumerable<string> layers, bool frozen)
        {
            var handles = new List<string>();

            foreach (DxfViewport viewport in LayoutViewports(document))
            {
                handles.Add(viewport.Handle);
            }

            return SetFrozen(document, handles, layers, frozen);
        }

        /// <summary>
        /// Sets whether a layer is frozen in each viewport made from now on, as the New VP Freeze column and VPLAYER Vpvisdflt
        /// do (flag 2 of the layer's group 70); the viewports there are now keep their own.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="frozen">Whether new viewports freeze it.</param>
        /// <param name="name">The name of the undo step.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetFrozenInNewViewports(CadDocument document, string layer, bool frozen, string name = "LAYER")
        {
            DxfLayerTableRecord record = Layer(document, layer, out CadEditResult refused);

            if (record == null)
            {
                return refused;
            }

            string layerName = record.Name;
            var change = new CadEditRecord(CadEditRecordKind.TableChanged, null, "New VP Freeze " + layerName, record.ViewportVisibilityDefault ? "Yes" : "No", frozen ? "Yes" : "No") { IsLayerLook = true };
            return document.ApplyEdit(name ?? "LAYER", new[] { change }, s => s.Database.LayerTable[layerName].ViewportVisibilityDefault = frozen);
        }

        /// <summary>
        /// Adds a layer frozen in every viewport of every layout there is, as the Layer manager's "New Layer VP Frozen in All
        /// Viewports" and VPLAYER Newfrz: of the colour White and the linetype Continuous, as a new layer is, one undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="layer">The name.</param>
        /// <param name="name">The name of the undo step.</param>
        /// <returns>What came of it; refused where the drawing has a layer of the name, either case.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult AddFrozenInAllViewports(CadDocument document, string layer, string name = "LAYER")
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(layer))
            {
                return CadEditResult.Refuse("A layer needs a name.");
            }

            string trimmed = layer.Trim();

            if (document.Database.LayerTable.Has(trimmed))
            {
                return CadEditResult.Refuse("Layer \"" + trimmed + "\" already exists.");
            }

            var records = new List<CadEditRecord> { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layer", null, trimmed) };
            var handles = new List<string>();

            foreach (DxfViewport viewport in LayoutViewports(document))
            {
                handles.Add(viewport.Handle);
                records.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, viewport.Handle, "VP Freeze", Frozen(viewport), Joined(Planned(viewport, new List<string> { trimmed }, true))));
            }

            return document.ApplyEdit(name ?? "LAYER", records, s =>
            {
                s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = trimmed, ColorIndex = 7 });
                DxfLayerTableRecord added = s.Database.LayerTable[trimmed];

                foreach (string handle in handles)
                {
                    if (!s.IsHandled(handle))
                    {
                        ((DxfViewport)s.Document.FindEntity(handle)).FreezeLayersInViewport(new[] { added });
                    }
                }
            });
        }

        /// <summary>Overrides a layer's colour in a viewport, as the VP Color column: one of the index or a true colour; ByLayer and ByBlock refused.</summary>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The handle of the viewport.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="color">The colour.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetColor(CadDocument document, string viewport, string layer, DxfColor color)
        {
            if (color == null || (!color.IsByAci && !color.IsByColor))
            {
                return CadEditResult.Refuse("A layer's colour in a viewport is one of the index, 1 to 255, or a true colour; not ByLayer or ByBlock.");
            }

            if (color.IsByColor && string.CompareOrdinal(document?.Database.Version, "AC1018") < 0)
            {
                return CadEditResult.Refuse("A drawing older than AutoCAD 2004 has no true colours: pick a colour of the index.");
            }

            return Override(document, viewport, layer, "VP Color", l => l.Color.ToString(), color.ToString(), l => l.Color = color);
        }

        /// <summary>Overrides a layer's linetype in a viewport, as the VP Linetype column: one the drawing has.</summary>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The handle of the viewport.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="linetype">The name of the linetype.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetLinetype(CadDocument document, string viewport, string layer, string linetype)
        {
            string wanted = (linetype ?? string.Empty).Trim();

            if (wanted.Length == 0 || string.Equals(wanted, "ByLayer", StringComparison.OrdinalIgnoreCase) || string.Equals(wanted, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("A layer's linetype in a viewport is one of the drawing's linetypes, not ByLayer or ByBlock.");
            }

            if (document != null && !document.Database.LinetypeTable.Has(wanted))
            {
                return CadEditResult.Refuse("The drawing has no linetype \"" + wanted + "\": load it into the drawing first.");
            }

            return Override(document, viewport, layer, "VP Linetype", l => l.Linetype, wanted, l => l.Linetype = wanted);
        }

        /// <summary>Overrides a layer's lineweight in a viewport, as the VP Lineweight column: Default or one of AutoCAD's widths.</summary>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The handle of the viewport.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="lineWeight">The lineweight.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetLineWeight(CadDocument document, string viewport, string layer, DxfLineWeight lineWeight)
        {
            if (lineWeight == DxfLineWeight.ByLayer || lineWeight == DxfLineWeight.ByBlock || lineWeight == DxfLineWeight.ByDIPs || !Enum.IsDefined(typeof(DxfLineWeight), lineWeight))
            {
                return CadEditResult.Refuse("A layer's lineweight in a viewport is Default or one of AutoCAD's widths, not ByLayer or ByBlock.");
            }

            return Override(document, viewport, layer, "VP Lineweight", l => l.LineWeight.ToString(), lineWeight.ToString(), l => l.LineWeight = lineWeight);
        }

        /// <summary>Overrides a layer's transparency in a viewport, as the VP Transparency column: by alpha, in a drawing of AutoCAD 2004 on.</summary>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The handle of the viewport.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="transparency">The transparency.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetTransparency(CadDocument document, string viewport, string layer, DxfTransparency transparency)
        {
            if (!transparency.IsByAlpha)
            {
                return CadEditResult.Refuse("A layer's transparency in a viewport is from 0 to 90 per cent.");
            }

            if (string.CompareOrdinal(document?.Database.Version, "AC1018") < 0)
            {
                return CadEditResult.Refuse("A drawing older than AutoCAD 2004 has no transparency.");
            }

            return Override(document, viewport, layer, "VP Transparency", l => l.Transparency.ToString(), transparency.ToString(), l => l.Transparency = transparency);
        }

        /// <summary>
        /// Overrides a layer's plot style in a viewport, as the VP Plot Style column: a name of the drawing's plot styles, in a
        /// drawing of named plot styles; refused in one plotted by colour, where the column is grey, as AutoCAD refuses it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The handle of the viewport.</param>
        /// <param name="layer">The name of the layer.</param>
        /// <param name="plotStyle">The name of the plot style.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetPlotStyle(CadDocument document, string viewport, string layer, string plotStyle)
        {
            if (document != null && document.Database.PlotStyleMode)
            {
                return CadEditResult.Refuse("The drawing plots by colour (a CTB table): a layer takes no plot style of its own.");
            }

            string wanted = (plotStyle ?? string.Empty).Trim();
            DxfDBDictionary names = document?.Database.PlotStyleNameDictionary;

            if (document != null && (names == null || !names.Contains(wanted)))
            {
                return CadEditResult.Refuse("The drawing has no plot style \"" + wanted + "\".");
            }

            return Override(document, viewport, layer, "VP Plot Style", l => l.PlotStyleName, wanted, l => l.PlotStyleName = wanted);
        }

        /// <summary>
        /// Takes out every override of layers, in one viewport or in all, as the Layer manager's "Remove Viewport Overrides for":
        /// nothing where they have none.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="layers">The names of the layers.</param>
        /// <param name="viewport">The handle of the viewport; null for every viewport.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the names are null.</exception>
        public static CadEditResult RemoveOverrides(CadDocument document, IEnumerable<string> layers, string viewport)
        {
            List<string> names = Layers(document, layers, out CadEditResult refused);

            if (names == null)
            {
                return refused;
            }

            DxfViewport only = null;

            if (viewport != null && !((only = document.FindEntity(viewport) as DxfViewport) != null))
            {
                return CadEditResult.Refuse("The drawing has no viewport " + viewport + ".");
            }

            var records = new List<CadEditRecord>();

            foreach (string layer in names)
            {
                DxfLayerTableRecord record = document.Database.LayerTable[layer];

                if (only == null ? record.HasOverrides : record.HasViewportOverrides(only))
                {
                    records.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Remove viewport overrides of " + record.Name, viewport ?? "All viewports", null));
                }
            }

            if (records.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return document.ApplyEdit("LAYER", records, s =>
            {
                foreach (string layer in names)
                {
                    DxfLayerTableRecord record = s.Database.LayerTable[layer];

                    if (viewport == null)
                    {
                        record.RemoveAllOverrides();
                    }
                    else
                    {
                        record.RemoveViewportOverrides((DxfViewport)s.Document.FindEntity(viewport));
                    }
                }
            });
        }

        /// <summary>The names of a viewport's frozen layers, joined by commas in its order.</summary>
        private static string Frozen(DxfViewport viewport)
        {
            var names = new List<string>();

            foreach (DxfLayerTableRecord layer in viewport.GetFrozenLayers())
            {
                names.Add(layer.Name);
            }

            return Joined(names);
        }

        /// <summary>The layers a viewport will freeze once layers are frozen in it or thawed.</summary>
        private static List<string> Planned(DxfViewport viewport, List<string> layers, bool frozen)
        {
            var planned = new List<string>();

            foreach (DxfLayerTableRecord layer in viewport.GetFrozenLayers())
            {
                if (frozen || !layers.Exists(n => string.Equals(n, layer.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    planned.Add(layer.Name);
                }
            }

            if (frozen)
            {
                foreach (string layer in layers)
                {
                    if (!planned.Exists(n => string.Equals(n, layer, StringComparison.OrdinalIgnoreCase)))
                    {
                        planned.Add(layer);
                    }
                }
            }

            return planned;
        }

        private static string Joined(IEnumerable<string> names) => string.Join(",", names);

        /// <summary>An override of a layer in a viewport, one undo step of one record.</summary>
        private static CadEditResult Override(CadDocument document, string viewport, string layer, string what, Func<DxfLayerViewportProperties, string> old, string value, Action<DxfLayerViewportProperties> write)
        {
            DxfLayerTableRecord record = Layer(document, layer, out CadEditResult refused);

            if (record == null)
            {
                return refused;
            }

            if (!(document.FindEntity(viewport) is DxfViewport target) || target.IsErased || target.Number == 1)
            {
                return CadEditResult.Refuse("The drawing has no viewport " + viewport + " of a layout to override a layer in.");
            }

            string layerName = record.Name;
            var change = new CadEditRecord(CadEditRecordKind.TableChanged, null, what + " of " + layerName + " in " + target.Handle, old(record.GetViewportOverrides(target)), value);
            return document.ApplyEdit("LAYER", new[] { change }, s => write(s.Database.LayerTable[layerName].GetViewportOverrides((DxfViewport)s.Document.FindEntity(viewport))));
        }

        /// <summary>The names of layers as the table has them, each once, in the order given; null with the refusal where one is missing.</summary>
        private static List<string> Layers(CadDocument document, IEnumerable<string> layers, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            var names = new List<string>();
            refused = null;

            foreach (string layer in layers)
            {
                DxfLayerTableRecord record = Layer(document, layer, out refused);

                if (record == null)
                {
                    return null;
                }

                if (!names.Exists(n => string.Equals(n, record.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    names.Add(record.Name);
                }
            }

            return names;
        }

        private static DxfLayerTableRecord Layer(CadDocument document, string name, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfLayerTableRecord layer = string.IsNullOrWhiteSpace(name) || !document.Database.LayerTable.Has(name.Trim()) ? null : document.Database.LayerTable[name.Trim()];
            refused = layer == null ? CadEditResult.Refuse("Cannot find layer \"" + name + "\".") : null;
            return layer;
        }
    }
}
