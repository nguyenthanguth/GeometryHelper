using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A layer of the LayerWalk dialog's list: its name and whether anything is drawn on it.</summary>
    public sealed class CadLayerWalkRow
    {
        internal CadLayerWalkRow(string name, bool isUsed)
        {
            Name = name;
            IsUsed = isUsed;
        }

        /// <summary>Gets the layer's name.</summary>
        public string Name { get; }

        /// <summary>Gets whether an object is on the layer, or a block, a style or the header names it; an unused layer is the one Purge Layers deletes.</summary>
        public bool IsUsed { get; }
    }

    /// <summary>
    /// The LayerWalk dialog of LAYWALK, as data its command and the window share: the layers listed, the filter that narrows
    /// them, the layers walked, and the choice to put the layers back as they were when it closes. Walking layers turns every
    /// layer but those selected off and thaws those selected, as AutoCAD's LayerWalk shows only the objects on the layers
    /// selected; with none selected every layer is as it was. A drawing of the layers WALL, DOOR and TRIM, walked on DOOR, shows
    /// the objects on DOOR alone; closed with Restore on exit, it has all three as they were. The changes are edits of the
    /// command's one undo step.
    /// </summary>
    public sealed class CadLayerWalkRequest
    {
        private readonly CadLayerShowing _showing;
        private string _filter = string.Empty;

        internal CadLayerWalkRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _showing = new CadLayerShowing(document, "LAYWALK");
            Refresh();
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets every layer of the drawing, in ordinal order of the names, letters of either case alike.</summary>
        public IReadOnlyList<CadLayerWalkRow> Rows { get; private set; } = Array.Empty<CadLayerWalkRow>();

        /// <summary>
        /// Gets or sets the words the list is narrowed by, a wild-card pattern as the AutoCAD filter box takes it
        /// (<c>WALL*</c>, <c>*-EXT,DOOR?</c>); empty or <c>*</c> lists every layer.
        /// </summary>
        public string Filter
        {
            get => _filter;
            set => _filter = (value ?? string.Empty).Trim();
        }

        /// <summary>Gets the layers the filter lists, in the order of <see cref="Rows"/>.</summary>
        public IReadOnlyList<CadLayerWalkRow> Listed =>
            _filter.Length == 0 || _filter == "*"
                ? Rows
                : Rows.Where(r => CadWildcard.IsMatch(r.Name, _filter, true)).ToList();

        /// <summary>Gets the names of the layers walked now; empty where every layer is as it was.</summary>
        public IReadOnlyList<string> Selected => _showing.Shown;

        /// <summary>Gets or sets whether closing the dialog puts the layers back as they were; true until set, as AutoCAD's Restore on exit.</summary>
        public bool RestoreOnExit { get; set; } = true;

        /// <summary>Gets or sets what the dialog closed with: Cancel for Close, SelectObjects for Select objects, which asks on the canvas and shows the dialog again.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>Gets what the last change said where it was refused; null where it was made.</summary>
        public string Message { get; private set; }

        /// <summary>
        /// Walks layers: every layer but those named is turned off, those named turned on and thawed. A name no layer has is
        /// left out; none given shows every layer as it was.
        /// </summary>
        /// <param name="names">The layers selected in the list.</param>
        /// <returns>Whether it was done; <see cref="Message"/> says why not.</returns>
        public bool Walk(IReadOnlyCollection<string> names)
        {
            CadEditResult result = _showing.Show(
                (names ?? Array.Empty<string>())
                    .Where(n => Document.Database.LayerTable.Has(n))
                    .Select(n => Document.Database.LayerTable[n].Name)
                    .ToList()
            );
            Message = result.Succeeded ? null : result.Message;
            return result.Succeeded;
        }

        /// <summary>
        /// Select objects: walks the layers of the objects picked on the canvas, as AutoCAD's button selects them in the list.
        /// </summary>
        /// <param name="handles">The handles of the objects picked.</param>
        /// <returns>Whether it was done; false where no object had a layer.</returns>
        public bool WalkLayersOf(IEnumerable<string> handles)
        {
            var names = new List<string>();

            foreach (string handle in handles ?? Array.Empty<string>())
            {
                DxfEntity entity = Document.FindEntity(handle);

                if (entity != null && Document.Database.LayerTable.Has(entity.Layer))
                {
                    names.Add(Document.Database.LayerTable[entity.Layer].Name);
                }
            }

            return names.Count > 0 && Walk(names);
        }

        /// <summary>
        /// Purge Layers: deletes the layers named that nothing uses, as the dialog's menu does; a layer in use, the current
        /// layer, 0 and Defpoints are kept. A layer deleted leaves the list.
        /// </summary>
        /// <param name="names">The layers selected in the list.</param>
        /// <returns>The names of the layers deleted, in the order given.</returns>
        public IReadOnlyList<string> Purge(IEnumerable<string> names)
        {
            var purged = new List<string>();

            foreach (string name in names ?? Array.Empty<string>())
            {
                if (!Document.Database.LayerTable.Has(name))
                {
                    continue;
                }

                string layer = Document.Database.LayerTable[name].Name;

                // A layer turned off for the walk is deleted the same: its switches are no reason to keep it.
                if (CadLayerEditor.Delete(Document, layer).Succeeded)
                {
                    purged.Add(layer);
                    _showing.Forget(layer);
                }
            }

            Refresh();
            return purged;
        }

        /// <summary>Puts every layer back as it was before the walk, as closing with Restore on exit does.</summary>
        /// <returns>Whether it was done.</returns>
        public bool Restore()
        {
            CadEditResult result = _showing.Back();
            Message = result.Succeeded ? null : result.Message;
            return result.Succeeded;
        }

        /// <summary>Lists the layers again, after a layer is deleted.</summary>
        internal void Refresh()
        {
            Rows = Document
                .Database.LayerTable.Select(l => new CadLayerWalkRow(l.Name, l.IsUsed))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();
        }
    }
}
