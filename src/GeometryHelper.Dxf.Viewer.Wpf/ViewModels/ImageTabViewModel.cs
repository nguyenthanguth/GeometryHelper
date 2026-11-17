using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The images selected on the canvas and the contextual Image tab over them, as AutoCAD 2021 shows it: Adjust (Brightness,
    /// Contrast and Fade, 0 to 100), Clipping (Create Clipping Boundary, Remove Clipping), Options (Show Image, Transparency and
    /// the Frame box of the drawing) and Close. Each value written is one undo step named after the command that changes the
    /// same thing (IMAGEADJUST, TRANSPARENCY, IMAGECLIP), made by <see cref="CadImageEditor"/>. The three levels are greyed for
    /// a bitonal picture, which AutoCAD refuses to adjust.
    /// </summary>
    public sealed class ImageTabViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private readonly string[] _handles;
        private DxfRasterImage _first;
        private bool _bitonal;

        /// <summary>Makes the tab over images.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="handles">The handles of the images the tab changes, in ordinal order.</param>
        public ImageTabViewModel(MainViewModel model, IReadOnlyList<string> handles)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _handles = (handles ?? throw new ArgumentNullException(nameof(handles))).ToArray();
            CreateClipCommand = new DelegateCommand(
                _ => _model.RunOnSelectedImages("IMAGECLIP", "New"),
                _ => _handles.Length == 1
            );
            RemoveClipCommand = new DelegateCommand(
                _ => Edit(CadImageEditor.RemoveClipping(Document, _handles)),
                _ => IsClipped
            );
            CloseCommand = new DelegateCommand(_ => Document?.Selection.Clear());
            Refresh();
        }

        /// <summary>Gets the handles of the images the tab changes.</summary>
        public IReadOnlyList<string> Handles => _handles;

        /// <summary>Gets the command of Create Clipping Boundary: IMAGECLIP's New boundary on the image, which asks for the boundary on the canvas.</summary>
        public ICommand CreateClipCommand { get; }

        /// <summary>Gets the command of Remove Clipping: the images show their whole pictures again.</summary>
        public ICommand RemoveClipCommand { get; }

        /// <summary>Gets the command of Close Image: lets go of the images selected, which hides the tab.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Gets the line under the levels: how many images are selected.</summary>
        public string Caption =>
            _handles.Length == 1 ? "1 image selected." : _handles.Length.ToString() + " images selected.";

        /// <summary>Gets whether the levels can be set: false where an image selected is bitonal, which has none to adjust.</summary>
        public bool CanAdjust => !_bitonal;

        /// <summary>Gets whether an image selected is clipped by a boundary of its own, so that Remove Clipping has something to remove.</summary>
        public bool IsClipped =>
            _handles.Any(h => Document?.FindEntity(h) is DxfRasterImage image && CadCommands.Clipped(image));

        /// <summary>Gets or sets the brightness, 0 to 100, of the first image selected; set, every image selected is given it.</summary>
        public int Brightness
        {
            get => _first?.Brightness ?? 50;
            set => Level("Brightness", value);
        }

        /// <summary>Gets or sets the contrast, 0 to 100, of the first image selected; set, every image selected is given it.</summary>
        public int Contrast
        {
            get => _first?.Contrast ?? 50;
            set => Level("Contrast", value);
        }

        /// <summary>Gets or sets the fade, 0 to 100, of the first image selected; set, every image selected is given it.</summary>
        public int Fade
        {
            get => _first?.Fade ?? 0;
            set => Level("Fade", value);
        }

        /// <summary>Gets or sets whether the pictures are shown; off, only their frames are.</summary>
        public bool ShowImage
        {
            get => _first?.ShowImage ?? true;
            set
            {
                if (value != ShowImage)
                {
                    Edit(CadImageEditor.SetShown(Document, _handles, value));
                }
            }
        }

        /// <summary>Gets or sets whether the background of a bitonal picture is transparent, what is under the image showing through.</summary>
        public bool Transparency
        {
            get => _first?.ImageTransparency ?? false;
            set
            {
                if (value != Transparency)
                {
                    Edit(CadImageEditor.SetTransparent(Document, _handles, value));
                }
            }
        }

        /// <summary>Gets the words of the Frame box's items: the drawing's IMAGEFRAME 0, 1 and 2.</summary>
        public IReadOnlyList<string> FrameNames => _model.ImageFrameItems.Select(i => i.Name).ToList();

        /// <summary>Gets or sets the Frame box's choice, the name of the drawing's IMAGEFRAME, as the Insert tab's box sets it.</summary>
        public string Frame
        {
            get => _model.CurrentImageFrame;
            set
            {
                _model.CurrentImageFrame = value;
                Raise(nameof(Frame));
            }
        }

        private CadDocument Document => _model.Document;

        /// <summary>Reads the images again from the drawing and tells the controls: the values the tab shows follow an undo or a command.</summary>
        internal void Refresh()
        {
            _first = _handles.Length == 0 ? null : Document?.FindEntity(_handles[0]) as DxfRasterImage;
            _bitonal = _handles.Any(h => Document?.FindEntity(h) is DxfRasterImage image && IsBitonal(image));
            Raise(string.Empty);
            (CreateClipCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (RemoveClipCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Whether the picture an image shows is bitonal, read from its file where that is found.</summary>
        private bool IsBitonal(DxfRasterImage image)
        {
            string saved = image.Path;
            string found = string.IsNullOrEmpty(saved) || Document == null ? null : Document.Xrefs.Locate(saved);
            return found != null && CadImageFile.IsBitonal(found);
        }

        private void Level(string option, int value)
        {
            if (Document == null || _bitonal)
            {
                return;
            }

            Edit(CadImageEditor.SetLevel(Document, _handles, option, value));
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
