using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Images selected on the canvas: the contextual Image tab, shown while the objects selected are images alone and no
    /// command runs, as AutoCAD 2021 shows it, with the values of the images to change in it (<see cref="ImageTabViewModel"/>).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadImageTab _imageTab;
        private ImageTabViewModel _images;

        /// <summary>Gets the contextual Image tab, shown while images are selected.</summary>
        public CadWorkspaceExtension ImageTab
        {
            get
            {
                if (_imageTab == null)
                {
                    _imageTab = new CadImageTab();
                    _builtInTabs.Add(_imageTab);
                }

                return _imageTab;
            }
        }

        /// <summary>Gets the images selected now and what the Image tab does to them; null where none are.</summary>
        public ImageTabViewModel ImageEditor => _images;

        /// <summary>Starts a command on the images selected, the words after its name answering its prompts in turn, as an object menu does.</summary>
        /// <param name="words">The command's name, then what to answer its prompts.</param>
        internal void RunOnSelectedImages(params string[] words) => RunOnSelection(words);

        /// <summary>
        /// Shows the Image tab where the objects selected are images alone (a wipeout is no image to it) and no command runs, and
        /// keeps it in step with the drawing; hides it where they are not.
        /// </summary>
        /// <param name="handles">The handles of the objects selected, in ordinal order.</param>
        private void SyncImageTab(IReadOnlyList<string> handles)
        {
            if (
                _current == null
                || _current.Session.IsRunning
                || handles == null
                || handles.Count == 0
                || handles.Any(h => !(_current.Document.FindEntity(h) is DxfRasterImage image) || image is DxfWipeout)
                || handles.Any(h => !IsEditableAsItsKind(h))
            )
            {
                HideImageTab();
                return;
            }

            if (_images != null && _images.Handles.SequenceEqual(handles, StringComparer.OrdinalIgnoreCase))
            {
                _images.Refresh();
                return;
            }

            _images = new ImageTabViewModel(this, handles);
            ((CadImageTab)ImageTab).Show(_images);
            SelectedTab = ImageTab;
            Raise(nameof(ImageEditor));
        }

        /// <summary>Hides the Image tab.</summary>
        private void HideImageTab()
        {
            if (_images == null)
            {
                return;
            }

            _images = null;

            if (_imageTab != null && _imageTab.IsTabVisible)
            {
                _imageTab.Hide();

                if (SelectedTab == _imageTab || SelectedTab == null)
                {
                    SelectedTab = null;
                }
            }

            Raise(nameof(ImageEditor));
        }
    }

    /// <summary>The contextual Image tab, in green as a contextual tab, shown while images are selected.</summary>
    internal sealed class CadImageTab : CadWorkspaceExtension
    {
        public override string TabHeader => "Image";

        public void Show(ImageTabViewModel images)
        {
            TabContent = images;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}
