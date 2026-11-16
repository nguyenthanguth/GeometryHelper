using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Associative arrays selected on the canvas: the contextual Array tab, shown while one associative array is selected, as
    /// AutoCAD 2021 shows it, with the parameters of the array to change in it (<see cref="ArrayTabViewModel"/>).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadArrayTab _arrayTab;
        private ArrayTabViewModel _array;

        /// <summary>Gets the contextual Array tab, shown while one associative array is selected.</summary>
        public CadWorkspaceExtension ArrayTab
        {
            get
            {
                if (_arrayTab == null)
                {
                    _arrayTab = new CadArrayTab();
                    _builtInTabs.Add(_arrayTab);
                }

                return _arrayTab;
            }
        }

        /// <summary>Gets the associative array selected now and what the Array tab does to it; null where none is.</summary>
        public ArrayTabViewModel ArrayEditor => _array;

        /// <summary>
        /// Shows the Array tab where the objects selected are one associative array (an INSERT that carries the action of an
        /// array), and keeps it in step with the drawing; hides it where they are not.
        /// </summary>
        /// <param name="handles">The handles of the objects selected.</param>
        private void SyncArrayTab(IReadOnlyList<string> handles)
        {
            string handle = null;

            if (
                _current != null
                && !_current.Session.IsRunning
                && handles != null
                && handles.Count == 1
                && CadArrays.TryRead(_current.Document, handles[0]) != null
            )
            {
                handle = handles[0];
            }

            if (handle == null)
            {
                HideArrayTab();
                return;
            }

            if (_array != null && string.Equals(_array.Handle, handle, StringComparison.OrdinalIgnoreCase))
            {
                _array.Refresh();
                return;
            }

            _array = new ArrayTabViewModel(this, handle);
            ((CadArrayTab)ArrayTab).Show(_array);
            SelectedTab = ArrayTab;
            Raise(nameof(ArrayEditor));
        }

        /// <summary>Hides the Array tab.</summary>
        private void HideArrayTab()
        {
            if (_array == null)
            {
                return;
            }

            _array = null;

            if (_arrayTab != null && _arrayTab.IsTabVisible)
            {
                _arrayTab.Hide();

                if (SelectedTab == _arrayTab || SelectedTab == null)
                {
                    SelectedTab = null;
                }
            }

            Raise(nameof(ArrayEditor));
        }
    }

    /// <summary>The contextual Array tab, in green as a contextual tab, shown while one associative array is selected.</summary>
    internal sealed class CadArrayTab : CadWorkspaceExtension
    {
        public override string TabHeader => "Array";

        public void Show(ArrayTabViewModel array)
        {
            TabContent = array;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}
