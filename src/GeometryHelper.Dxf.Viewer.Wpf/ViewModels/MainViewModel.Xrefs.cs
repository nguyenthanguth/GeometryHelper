using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The External References palette (XREF, EXTERNALREFERENCES, IMAGE) and the attach dialogs (XATTACH, IMAGEATTACH), each
    /// shown through a function the workspace sets, so that a test drives them with none. With none set, XATTACH and
    /// IMAGEATTACH ask the file's name on the command line. And the image frame box of Insert › Reference, which shows and
    /// sets IMAGEFRAME.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The image frame box's items, IMAGEFRAME 0, 1 and 2, in AutoCAD 2021's words.
        private static readonly string[] ImageFrameNames = { "Hide Frames", "Display and Plot Frames", "Display but Not Plot Frames" };

        /// <summary>Gets or sets how the workspace shows the External References palette, beside the window and left open.</summary>
        public Action<ExternalReferencesViewModel> ShowExternalReferences { get; set; }

        /// <summary>Gets or sets how the workspace shows the attach dialog; the action it closed with is in its request.</summary>
        public Func<AttachReferenceViewModel, bool> ShowAttachReference { get; set; }

        /// <summary>
        /// Starts a command and answers its prompts with the texts given, each as typed and Enter pressed, an empty one as Enter
        /// alone, while the command runs: the palette's actions run -XREF and -IMAGE so, a name with spaces in it whole.
        /// </summary>
        internal void RunWithAnswers(string command, params string[] answers)
        {
            if (_current == null)
            {
                return;
            }

            CadCommandSession session = _current.Session;
            session.Start(command);

            foreach (string answer in answers)
            {
                if (!session.IsRunning)
                {
                    break;
                }

                if (answer.Length == 0)
                {
                    session.Enter();
                }
                else
                {
                    session.Feed(answer);
                }
            }

            SyncHistory();
            RunAfterCommand();
        }

        /// <summary>Gets the items of the image frame box of Insert › Reference, IMAGEFRAME 0, 1 and 2 in turn, the drawing's ticked.</summary>
        public IReadOnlyList<StyleBoxItemViewModel> ImageFrameItems
            => ImageFrameNames.Select((n, i) => new StyleBoxItemViewModel(n, i == ImageFrame, "IMAGEFRAME " + i.ToString(CultureInfo.InvariantCulture), false)).ToList();

        /// <summary>
        /// Gets or sets the image frame box's choice, the name of the drawing's IMAGEFRAME: Hide Frames (0), Display and Plot
        /// Frames (1, also where the drawing has no image yet) or Display but Not Plot Frames (2). Set, IMAGEFRAME is run with
        /// its value, one undo step; a drawing with no image refuses it as IMAGEFRAME does, and the box keeps 1.
        /// </summary>
        public string CurrentImageFrame
        {
            get => _current == null ? null : ImageFrameNames[ImageFrame];
            set
            {
                int index = Array.IndexOf(ImageFrameNames, value);

                if (_current == null || index < 0 || index == ImageFrame)
                {
                    return;
                }

                RunWithAnswers("IMAGEFRAME", index.ToString(CultureInfo.InvariantCulture));
                RaiseStyles();
            }
        }

        /// <summary>The drawing's IMAGEFRAME, read as the command reads it: 1 where the drawing keeps no variables of images.</summary>
        private int ImageFrame
        {
            get
            {
                DxfDatabase database = _current?.Document.Database;
                DxfRasterVariables variables = database?.NamedObjectsDictionary?.Contains("ACAD_IMAGE_VARS") == true ? database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS") as DxfRasterVariables : null;
                int value = variables == null ? 1 : (int)variables.ImageFrame;
                return value < 0 || value > 2 ? 1 : value;
            }
        }

        /// <summary>
        /// A request of the external reference commands: the palette or an attach dialog shown where the workspace shows them,
        /// or the drawing XOPEN picked opened in a tab of its own, as <see cref="Open(string)"/> opens a file.
        /// </summary>
        private void OnXrefRequested(CadUiRequestEventArgs e)
        {
            switch (e.Name)
            {
                case "XREF":
                    if (ShowExternalReferences != null && _current != null)
                    {
                        ShowExternalReferences(new ExternalReferencesViewModel(this));
                        e.Handled = true;
                    }

                    break;
                case "XOPEN":
                    if (e.Payload is string path)
                    {
                        e.Handled = Open(path);
                    }

                    break;
                case "XATTACH":
                case "IMAGEATTACH":
                    if (e.Payload is CadAttachRequest request && ShowAttachReference != null)
                    {
                        e.Handled = ShowAttachReference(new AttachReferenceViewModel(request));
                    }

                    break;
            }
        }
    }
}
