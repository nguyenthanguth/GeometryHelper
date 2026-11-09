using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

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
