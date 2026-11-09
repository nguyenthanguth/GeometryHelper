using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Selection cycling with Shift+Space, as AutoCAD's: once armed, each click at the same place selects the next of the
    /// objects that lie there, in place of the one before, the nearest first and round again; a click elsewhere starts over
    /// at the nearest there. Esc disarms it.
    /// </summary>
    internal sealed class CadSelectionCycler
    {
        private GeoPoint2 _at;
        private IReadOnlyList<string> _found = Array.Empty<string>();
        private int _index = -1;
        private IReadOnlyList<string> _chosen = Array.Empty<string>();

        /// <summary>Gets whether Shift+Space armed it.</summary>
        public bool IsArmed { get; private set; }

        /// <summary>Arms it for the clicks that follow.</summary>
        public void Arm()
        {
            IsArmed = true;
            _index = -1;
            _chosen = Array.Empty<string>();
        }

        /// <summary>Disarms it, the selection made kept.</summary>
        public void Disarm()
        {
            IsArmed = false;
            _index = -1;
            _found = Array.Empty<string>();
            _chosen = Array.Empty<string>();
        }

        /// <summary>A click with it armed and no command running: the next object at the place selected in place of the last.</summary>
        /// <returns>The handle selected; null where nothing lies there.</returns>
        public string Next(CadDocument document, CadSpace space, GeoPoint2 at, double aperture)
        {
            IReadOnlyList<string> found = document.GetPickIndex(space).Pick(at.X, at.Y, aperture);
            bool same = _index >= 0 && (at - _at).Length <= aperture && found.Count == _found.Count;

            for (int i = 0; same && i < found.Count; i++)
            {
                same = string.Equals(found[i], _found[i], StringComparison.OrdinalIgnoreCase);
            }

            document.Selection.Remove(_chosen);

            if (found.Count == 0)
            {
                _index = -1;
                _chosen = Array.Empty<string>();
                return null;
            }

            _index = same ? (_index + 1) % found.Count : 0;
            _found = found;
            _at = at;
            _chosen = document.WithGroups(new[] { found[_index] });
            document.Selection.Add(_chosen);
            return found[_index];
        }
    }
}
