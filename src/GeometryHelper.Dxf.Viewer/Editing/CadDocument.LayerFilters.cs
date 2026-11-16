using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The layer filter the drawing looks through: the one chosen in the Layer manager's tree, whether it is inverted, and
    /// whether the layer box of the ribbon lists only the layers it lets through (AutoCAD's Apply layer filter to layer toolbar).
    /// </summary>
    public sealed partial class CadDocument
    {
        private IReadOnlyList<string> _layerFilterPath;
        private bool _layerFilterRead;
        private bool _invertLayerFilter;
        private bool _layerFilterOnToolbar;

        /// <summary>Raised when the chosen layer filter, its inversion or the use of it for the layer box changes.</summary>
        public event EventHandler LayerFilterChanged;

        /// <summary>
        /// Gets or sets the names, from the top, of the filter the Layer manager lists the layers of: <c>Steel</c>, <c>Beams</c> for
        /// the filter Beams nested in Steel; the name of one of the three AutoCAD makes (All Used Layers); empty for All. Read from
        /// the filter the drawing marks as current (group 290 of its XRECORD) until first set.
        /// </summary>
        public IReadOnlyList<string> LayerFilterPath
        {
            get
            {
                if (!_layerFilterRead)
                {
                    _layerFilterRead = true;
                    CadLayerFilter marked = CadLayerFilters
                        .All(CadLayerFilters.Read(Database))
                        .FirstOrDefault(f => f.IsCurrent);
                    _layerFilterPath = marked?.Path;
                }

                return _layerFilterPath ?? Array.Empty<string>();
            }
            set
            {
                IReadOnlyList<string> wanted = value ?? Array.Empty<string>();

                if (_layerFilterRead && SamePath(LayerFilterPath, wanted))
                {
                    return;
                }

                _layerFilterRead = true;
                _layerFilterPath = wanted.Count == 0 ? null : wanted;
                LayerFilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets whether the layers the filter lets through are left out and the others listed, as the Layer manager's Invert filter does.</summary>
        public bool InvertLayerFilter
        {
            get => _invertLayerFilter;
            set
            {
                if (_invertLayerFilter != value)
                {
                    _invertLayerFilter = value;
                    LayerFilterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Gets or sets whether the layer box lists only the layers the filter lets through, the current layer always among them.</summary>
        public bool ApplyLayerFilterToToolbar
        {
            get => _layerFilterOnToolbar;
            set
            {
                if (_layerFilterOnToolbar != value)
                {
                    _layerFilterOnToolbar = value;
                    LayerFilterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Writes the filter the Layer manager has chosen into the drawing as the current one (group 290), one undo step, unless the
        /// drawing marks it already. A filter AutoCAD makes is not written: All marks none.
        /// </summary>
        /// <returns>What came of it.</returns>
        public CadEditResult KeepLayerFilter()
        {
            IReadOnlyList<string> path = LayerFilterPath;
            bool made =
                path.Count == 1
                && CadLayerFilters.Dynamic.Any(d => string.Equals(d.Key, path[0], StringComparison.OrdinalIgnoreCase));
            return CadLayerFilters.SetCurrent(this, made ? Array.Empty<string>() : path);
        }

        /// <summary>
        /// Gets the layers the layer box lists: all of them, or where <see cref="ApplyLayerFilterToToolbar"/> is on those the chosen
        /// filter lets through (the others where <see cref="InvertLayerFilter"/> is on) and the current layer; in no order.
        /// </summary>
        /// <returns>The layers.</returns>
        public IReadOnlyList<CadLayerInfo> ToolbarLayers()
        {
            if (!_layerFilterOnToolbar)
            {
                return Layers;
            }

            HashSet<string> shown = CadLayerFilters.Visible(Database, LayerFilterPath, _invertLayerFilter, null);
            return Layers
                .Where(l =>
                    shown.Contains(l.Name) || string.Equals(l.Name, Database.Clayer, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        private static bool SamePath(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
            a.Count == b.Count
            && a.Zip(b, (x, y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase)).All(same => same);
    }
}
