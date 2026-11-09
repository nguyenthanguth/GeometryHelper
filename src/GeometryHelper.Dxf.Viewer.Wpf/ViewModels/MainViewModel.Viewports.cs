using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Model space worked in through a viewport of a layout: the MODEL/PAPER button of the status bar, Ctrl+R, and the scale
    /// of the viewport worked through or selected, which the status bar's list shows and sets.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private ICommand _toggleSpaceCommand;

        /// <summary>Gets whether model space is worked in: on the Model tab, or through a viewport of the layout shown.</summary>
        public bool IsWorkingInModel => IsModelSpace || _current?.Document.ActiveViewport != null;

        /// <summary>
        /// Gets the MODEL/PAPER button's command: in a layout it runs MSPACE on the paper and PSPACE through a viewport, as
        /// AutoCAD's button does; nothing on the Model tab or while a command runs.
        /// </summary>
        public ICommand ToggleSpaceCommand => _toggleSpaceCommand ?? (_toggleSpaceCommand = new DelegateCommand(_ => ToggleSpace()));

        /// <summary>
        /// Gets the scales the status bar's list offers for a viewport: <see cref="CadViewports.StandardScales"/>, after the
        /// viewport's own where it is none of them, as 1:37.5.
        /// </summary>
        public IReadOnlyList<string> ViewportScales
        {
            get
            {
                string own = ViewportScale;

                if (own == null || CadViewports.StandardScales.Contains(own))
                {
                    return CadViewports.StandardScales;
                }

                var all = new List<string> { own };
                all.AddRange(CadViewports.StandardScales);
                return all;
            }
        }

        /// <summary>Gets whether the status bar shows a viewport's scale: one is worked through, or one alone is selected in a layout.</summary>
        public bool HasViewportScale => ScaledViewport() != null;

        /// <summary>
        /// Gets or sets the scale of the viewport worked through, else of the one viewport selected, as 1:50; null for none.
        /// Set, a standard scale or one typed as 1:37.5 makes the viewport's view height its frame's over the scale, one undo
        /// step; a refusal (a display locked, a name that is no scale) is said on the command line.
        /// </summary>
        public string ViewportScale
        {
            get
            {
                DxfViewport viewport = ScaledViewport();
                return viewport == null ? null : CadViewports.ScaleName(viewport.CustomScale);
            }

            set
            {
                DxfViewport viewport = ScaledViewport();

                if (viewport == null || value == null || string.Equals(value, ViewportScale, StringComparison.Ordinal))
                {
                    return;
                }

                CadEditResult result;

                if (CadViewports.StandardScales.Contains(value))
                {
                    result = CadPropertyEditor.Set(_current.Document, new[] { viewport.Handle }, "Standard scale", value);
                }
                else if (CadViewports.TryReadScale(value, out double scale))
                {
                    result = CadPropertyEditor.Set(_current.Document, new[] { viewport.Handle }, "Custom scale", scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    result = CadEditResult.Refuse("A scale is written as 1:50 or 2:1.");
                }

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }

                RaiseViewportScale();
            }
        }

        /// <summary>
        /// Works through the next viewport of the layout shown, as Ctrl+R in AutoCAD; nothing on the paper or in model space.
        /// </summary>
        /// <returns>Whether another viewport is worked through.</returns>
        public bool CycleViewport() => _current != null && _current.Document.NextViewport();

        private void ToggleSpace()
        {
            if (_current == null || IsModelSpace || _current.Session.IsRunning)
            {
                return;
            }

            StartCommand(_current.Document.ActiveViewport != null ? "PSPACE" : "MSPACE");
        }

        /// <summary>The viewport whose scale the status bar shows: the one worked through, else the one viewport selected in the layout shown.</summary>
        private DxfViewport ScaledViewport()
        {
            CadDocument document = _current?.Document;

            if (document == null || IsModelSpace)
            {
                return null;
            }

            if (document.ActiveViewport is DxfViewport through)
            {
                return through;
            }

            IReadOnlyCollection<string> handles = document.Selection.Handles;
            return handles.Count == 1 && document.FindEntity(handles.First()) is DxfViewport selected && selected.Number > 1 && selected.Height > 0.0 ? selected : null;
        }

        private void RaiseViewportScale()
        {
            Raise(nameof(HasViewportScale));
            Raise(nameof(ViewportScales));
            Raise(nameof(ViewportScale));
            Raise(nameof(IsSpaceButton));
            Raise(nameof(HasStatusScale));
            Raise(nameof(StatusScale));
            Raise(nameof(IsViewportLocked));
        }

        /// <summary>Gets whether MODEL/PAPER is a button: in a layout, where it runs MSPACE and PSPACE; on the Model tab it is a label.</summary>
        public bool IsSpaceButton => _current != null && !IsModelSpace;

        /// <summary>
        /// Gets whether the status bar shows a scale after the toggles: on the Model tab the annotation scale (CANNOSCALE), in a
        /// layout the scale of the viewport worked through or selected; none on the paper with no viewport selected.
        /// </summary>
        public bool HasStatusScale => _current != null && (IsModelSpace || HasViewportScale);

        /// <summary>Gets the scale the status bar shows: CANNOSCALE on the Model tab, as 1:1; the viewport's in a layout, as 1:20.</summary>
        public string StatusScale => _current == null ? null : IsModelSpace ? _current.Document.Database.Cannoscale : ViewportScale;

        /// <summary>
        /// Gets whether the display of the viewport whose scale is shown is locked, the padlock closed and the scale grey; false
        /// where no viewport's scale is shown, the padlock then hidden (<see cref="HasViewportScale"/>).
        /// </summary>
        public bool IsViewportLocked => ScaledViewport()?.Locked == true;

        /// <summary>Gets the padlock's command: locks or unlocks the display of the viewport worked through or selected, one undo step (its Display locked).</summary>
        public ICommand ToggleViewportLockCommand => _toggleLockCommand ?? (_toggleLockCommand = new DelegateCommand(_ => ToggleViewportLock()));

        /// <summary>
        /// Gets the items of the scale list the status bar opens. In a layout, as AutoCAD 2021's: Scale to fit, the standard scales
        /// (1:1 to 1:100, then 2:1 to 100:1), and Custom..., the viewport's own ticked, every item grey while its display is locked.
        /// On the Model tab, the scales of the drawing's list, CANNOSCALE ticked.
        /// </summary>
        /// <returns>The items; empty where no scale is shown.</returns>
        public IReadOnlyList<CadMenuItemViewModel> ScaleMenu()
        {
            var items = new List<CadMenuItemViewModel>();

            if (!HasStatusScale)
            {
                return items;
            }

            string own = StatusScale;

            if (IsModelSpace)
            {
                foreach (string name in AnnotationScales())
                {
                    string chosen = name;
                    items.Add(new CadMenuItemViewModel(name, () => SetAnnotationScale(chosen), true, null, string.Equals(name, own, StringComparison.OrdinalIgnoreCase)));
                }

                return items;
            }

            bool open = !IsViewportLocked;
            items.Add(new CadMenuItemViewModel("Scale to fit", ScaleToFit, open, null, false));
            items.Add(CadMenuItemViewModel.Separator());
            bool enlarging = false;

            foreach (string name in CadViewports.StandardScales)
            {
                if (!enlarging && !name.StartsWith("1:", StringComparison.Ordinal))
                {
                    enlarging = true;
                    items.Add(CadMenuItemViewModel.Separator());
                }

                string chosen = name;
                items.Add(new CadMenuItemViewModel(name, () => ViewportScale = chosen, open, null, string.Equals(name, own, StringComparison.Ordinal)));
            }

            items.Add(CadMenuItemViewModel.Separator());
            items.Add(new CadMenuItemViewModel("Custom...", () => StartCommand("VPSCALE"), open, "Asks the scale on the command line, as 1:37.5 (SCALELISTEDIT is not built)", false));
            return items;
        }

        private ICommand _toggleLockCommand;

        /// <summary>The names of the drawing's annotation scales, in the order of its list; 1:1 alone where it has none.</summary>
        private IReadOnlyList<string> AnnotationScales()
        {
            var names = new List<string>();

            try
            {
                DxfObjectContextCollection list = _current.Document.Database.ObjectContextManager?.GetContextCollection("ACDB_ANNOTATIONSCALES");

                foreach (DxfObjectContext scale in (IEnumerable<DxfObjectContext>)list ?? Array.Empty<DxfObjectContext>())
                {
                    names.Add(scale.Name);
                }
            }
            catch (Exception)
            {
                // A list that cannot be read offers the annotation scale alone.
            }

            if (names.Count == 0)
            {
                names.Add(_current.Document.Database.Cannoscale);
            }

            return names;
        }

        /// <summary>Scale to fit: the viewport's view fitted to the extents of model space, one undo step, as AutoCAD's item.</summary>
        private void ScaleToFit()
        {
            DxfViewport viewport = ScaledViewport();
            DisplayBox box = _current.Document.GetDisplay(_current.Document.ModelSpace).Bounds;

            if (viewport == null || box.IsEmpty)
            {
                return;
            }

            CadViewports.Fit(viewport, box, out GeoPoint2 center, out double height);
            CadEditResult result = _current.Document.SetViewportView(viewport.Handle, center, height, "ZOOM");

            if (!result.Succeeded)
            {
                Say(result.Message);
            }

            RaiseViewportScale();
        }

        /// <summary>The annotation scale of model space set from the list, one undo step, as CANNOSCALE typed sets it.</summary>
        private void SetAnnotationScale(string name)
        {
            string old = _current.Document.Database.Cannoscale;

            if (!string.Equals(old, name, StringComparison.Ordinal))
            {
                CadEditResult result = _current.Document.ApplyEdit("CANNOSCALE", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "CANNOSCALE", old, name) }, s => s.Database.Cannoscale = name);

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }
            }

            RaiseViewportScale();
        }

        private void ToggleViewportLock()
        {
            DxfViewport viewport = ScaledViewport();

            if (viewport == null)
            {
                return;
            }

            CadEditResult result = CadPropertyEditor.Set(_current.Document, new[] { viewport.Handle }, "Display locked", viewport.Locked ? "No" : "Yes");

            if (!result.Succeeded)
            {
                Say(result.Message);
            }

            RaiseViewportScale();
        }

        /// <summary>Model space entered through a viewport or left: the status bar, Properties and the canvas follow.</summary>
        private void OnActiveViewportChanged(object sender, EventArgs e)
        {
            if (_current != null && ReferenceEquals(sender, _current.Document))
            {
                Raise(nameof(IsWorkingInModel));
                RaiseSpace();
            }
        }
    }
}
