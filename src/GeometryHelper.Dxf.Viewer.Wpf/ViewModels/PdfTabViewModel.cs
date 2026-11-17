using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The PDF underlays selected on the canvas and the contextual PDF Underlay tab over them, in the look of the Image tab:
    /// Adjust (Fade and Contrast, 0 to 100, Monochrome and Adjust colors for background), Clipping (Create Clipping Boundary,
    /// Remove Clipping), Options (Show Underlay, the Frame box of PDFFRAME, Underlay Layers) and Close. Each value written is one
    /// undo step named after the command that changes the same thing (PDFADJUST, PDFCLIP), made by <see cref="CadPdfEditor"/>.
    /// </summary>
    public sealed class PdfTabViewModel : ObservableObject
    {
        private static readonly string[] Frames =
        {
            "Hide Frames",
            "Display and Plot Frames",
            "Display but Not Plot Frames",
        };

        private readonly MainViewModel _model;
        private readonly string[] _handles;
        private DxfPdfUnderlay _first;

        /// <summary>Makes the tab over underlays.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="handles">The handles of the underlays the tab changes, in ordinal order.</param>
        public PdfTabViewModel(MainViewModel model, IReadOnlyList<string> handles)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _handles = (handles ?? throw new ArgumentNullException(nameof(handles))).ToArray();
            CreateClipCommand = new DelegateCommand(
                _ => _model.RunOnSelectedImages("PDFCLIP", "New"),
                _ => _handles.Length == 1
            );
            RemoveClipCommand = new DelegateCommand(
                _ => Edit(CadPdfEditor.RemoveClipping(Document, _handles)),
                _ => HasClipBoundary
            );
            LayersCommand = new DelegateCommand(
                _ => _model.RunOnSelectedImages("PDFLAYERS"),
                _ => _handles.Length == 1
            );
            CloseCommand = new DelegateCommand(_ => Document?.Selection.Clear());
            Refresh();
        }

        /// <summary>Gets the handles of the underlays the tab changes.</summary>
        public IReadOnlyList<string> Handles => _handles;

        /// <summary>Gets the command of Create Clipping Boundary: PDFCLIP's New boundary on the underlay, which asks for the boundary on the canvas.</summary>
        public ICommand CreateClipCommand { get; }

        /// <summary>Gets the command of Remove Clipping: the underlays show their whole pages again.</summary>
        public ICommand RemoveClipCommand { get; }

        /// <summary>Gets the command of Underlay Layers: PDFLAYERS on the underlay.</summary>
        public ICommand LayersCommand { get; }

        /// <summary>Gets the command of Close Underlay: lets go of the underlays selected, which hides the tab.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Gets the line under the tab's levels: how many underlays are selected.</summary>
        public string Caption =>
            _handles.Length == 1 ? "1 PDF underlay selected." : _handles.Length.ToString() + " PDF underlays selected.";

        /// <summary>Gets whether an underlay selected has a clip boundary, so that Remove Clipping has something to remove.</summary>
        public bool HasClipBoundary =>
            _handles.Any(h => Document?.FindEntity(h) is DxfPdfUnderlay underlay && underlay.HasClipBoundary);

        /// <summary>Gets or sets the fade, 0 to 100, of the first underlay selected; set, every underlay selected is given it.</summary>
        public int Fade
        {
            get => _first?.Fade ?? 0;
            set => Level("Fade", value);
        }

        /// <summary>Gets or sets the contrast, 0 to 100, of the first underlay selected; set, every underlay selected is given it.</summary>
        public int Contrast
        {
            get => _first?.Contrast ?? 100;
            set => Level("Contrast", value);
        }

        /// <summary>Gets or sets whether the underlays are shown in monochrome.</summary>
        public bool Monochrome
        {
            get => _first?.Monochrome ?? false;
            set
            {
                if (value != Monochrome)
                {
                    Edit(CadPdfEditor.SetMonochrome(Document, _handles, value));
                }
            }
        }

        /// <summary>Gets or sets whether the colours of the underlays are adjusted for the background.</summary>
        public bool AdjustColors
        {
            get => _first?.AdjustColorForBackground ?? true;
            set
            {
                if (value != AdjustColors)
                {
                    Edit(CadPdfEditor.SetAdjustForBackground(Document, _handles, value));
                }
            }
        }

        /// <summary>Gets or sets whether the underlays are shown; off, only their frames are.</summary>
        public bool ShowUnderlay
        {
            get => _first?.IsOn ?? true;
            set
            {
                if (value != ShowUnderlay)
                {
                    Edit(CadPdfEditor.SetShown(Document, _handles, value));
                }
            }
        }

        /// <summary>Gets the words of the Frame box's items: PDFFRAME 0, 1 and 2.</summary>
        public IReadOnlyList<string> FrameNames => Frames;

        /// <summary>Gets or sets the Frame box's choice, the name of the drawing's PDFFRAME; set, PDFFRAME is set as the command line sets it.</summary>
        public string Frame
        {
            get => Frames[Math.Max(0, Math.Min(2, Document?.PdfFrame ?? 1))];
            set
            {
                int index = Array.IndexOf(Frames, value);

                if (index >= 0 && index != (Document?.PdfFrame ?? 1))
                {
                    _model.RunWithAnswers(
                        "PDFFRAME",
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    );
                }

                Raise(nameof(Frame));
            }
        }

        private CadDocument Document => _model.Document;

        /// <summary>Reads the underlays again from the drawing and tells the controls: the values the tab shows follow an undo or a command.</summary>
        internal void Refresh()
        {
            _first = _handles.Length == 0 ? null : Document?.FindEntity(_handles[0]) as DxfPdfUnderlay;
            Raise(string.Empty);
            (CreateClipCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (RemoveClipCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (LayersCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        private void Level(string option, int value)
        {
            if (Document != null)
            {
                Edit(CadPdfEditor.SetLevel(Document, _handles, option, value));
            }
        }

        private void Edit(CadEditResult result)
        {
            if (!result.Succeeded)
            {
                _model.Session?.Write(result.Message);
            }

            Refresh();
        }
    }
}
