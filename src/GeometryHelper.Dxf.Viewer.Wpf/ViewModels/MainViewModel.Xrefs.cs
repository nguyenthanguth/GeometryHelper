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
        private static readonly string[] ImageFrameNames =
        {
            "Hide Frames",
            "Display and Plot Frames",
            "Display but Not Plot Frames",
        };

        // The drawings whose referenced files are followed, and the references of each announced as changed: looked up only.
        private readonly Dictionary<CadDocument, EventHandler> _referenceWatchers =
            new Dictionary<CadDocument, EventHandler>();
        private readonly Dictionary<CadDocument, HashSet<string>> _announcedReferences =
            new Dictionary<CadDocument, HashSet<string>>();
        private bool _watchReferenceFiles;

        /// <summary>Gets or sets how the workspace shows the External References palette, beside the window and left open.</summary>
        public Action<ExternalReferencesViewModel> ShowExternalReferences { get; set; }

        /// <summary>Gets or sets how the workspace shows the attach dialog; the action it closed with is in its request.</summary>
        public Func<AttachReferenceViewModel, bool> ShowAttachReference { get; set; }

        /// <summary>
        /// Gets or sets how the workspace shows the Bind Xrefs/DGN Underlays dialog of the palette's Bind…; the action it closed
        /// with is in it. With none set, the drawings are bound by BINDTYPE as it stands.
        /// </summary>
        public Action<XrefBindViewModel> ShowXrefBind { get; set; }

        /// <summary>Gets or sets how the workspace shows the box of a bind refused, over the lines -XREF Bind wrote.</summary>
        public Action<XrefBindRefusedViewModel> ShowXrefBindRefused { get; set; }

        /// <summary>
        /// Gets or sets whether the files the open drawings' external references were read from are watched, so that one
        /// changed on disk is announced on the command line of its drawing, as AutoCAD 2021's status bar announces it (XREFNOTIFY
        /// 2): "External Reference Files Have Changed: grid needs reloading." once for each change, until it is reloaded. Off
        /// until set, as in a test; the workspace sets it.
        /// </summary>
        public bool WatchReferenceFiles
        {
            get => _watchReferenceFiles;
            set
            {
                _watchReferenceFiles = value;

                foreach (CadDocument document in _referenceWatchers.Keys.ToList())
                {
                    document.Xrefs.WatchFiles = value;
                }
            }
        }

        /// <summary>
        /// Says on the command line of a drawing which of its external references changed on disk since they were read and since
        /// they were last announced, in one line: "External Reference Files Have Changed: grid needs reloading. Reload it with
        /// -XREF Reload or in the External References palette." A reference reloaded since is announced again when its file
        /// next changes. The watcher of <see cref="WatchReferenceFiles"/> calls it on the window's thread.
        /// </summary>
        internal void AnnounceChangedReferences(CadDocument document)
        {
            KnownFile file = _files.Values.FirstOrDefault(f => f.Document == document);

            if (file?.Session == null)
            {
                return;
            }

            if (!_announcedReferences.TryGetValue(document, out HashSet<string> announced))
            {
                announced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _announcedReferences[document] = announced;
            }

            var changed = new List<string>();
            var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CadExternalReference reference in document.Xrefs.GetReferences())
            {
                if (!reference.IsImage && reference.Status == CadReferenceStatus.NeedsReloading)
                {
                    stale.Add(reference.Name);

                    if (announced.Add(reference.Name))
                    {
                        changed.Add(reference.Name);
                    }
                }
            }

            announced.IntersectWith(stale);

            if (changed.Count == 0)
            {
                return;
            }

            bool one = changed.Count == 1;
            file.Session.Write(
                "External Reference Files Have Changed: "
                    + string.Join(", ", changed)
                    + (one ? " needs" : " need")
                    + " reloading. Reload "
                    + (one ? "it" : "them")
                    + " with -XREF Reload or in the External References palette."
            );

            if (file == _current)
            {
                SyncHistory();
            }
        }

        /// <summary>Follows the files a drawing's external references were read from, while <see cref="WatchReferenceFiles"/> is set.</summary>
        private void WatchReferences(CadDocument document)
        {
            System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current?.Dispatcher
                ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            EventHandler handler = (sender, e) =>
                dispatcher.BeginInvoke(new Action(() => AnnounceChangedReferences(document)));
            _referenceWatchers[document] = handler;
            document.Xrefs.FilesChanged += handler;
            document.Xrefs.WatchFiles = _watchReferenceFiles;
        }

        /// <summary>Stops following the files of a drawing closed.</summary>
        private void StopWatchingReferences(CadDocument document)
        {
            if (_referenceWatchers.TryGetValue(document, out EventHandler handler))
            {
                document.Xrefs.FilesChanged -= handler;
                _referenceWatchers.Remove(document);
            }

            _announcedReferences.Remove(document);
        }

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

        /// <summary>
        /// The palette's Bind…: the Bind Xrefs/DGN Underlays dialog for the drawings named, started from BINDTYPE; OK writes the
        /// bind type chosen back to BINDTYPE, kept by the session, then runs -XREF Bind with the names, one undo step for them
        /// all. Where it binds nothing, the lines it wrote are shown in a box as well; the "Duplicate Block" lines of Insert
        /// stay on the command line alone.
        /// </summary>
        internal void BindXrefs(IReadOnlyList<string> names)
        {
            if (_current == null)
            {
                return;
            }

            CadCommandSession session = _current.Session;
            var dialog = new XrefBindViewModel(
                names,
                session.Remembered.TryGetValue("bindtype", out double kept) ? (int)kept : 0
            );

            if (ShowXrefBind != null)
            {
                ShowXrefBind(dialog);

                if (dialog.Action != CadDialogAction.Ok)
                {
                    return;
                }

                session.Remembered["bindtype"] = dialog.BindType;
            }

            int written = session.History.Count;
            RunWithAnswers("-XREF", "B", string.Join(",", names.Select(ExternalReferencesViewModel.Pattern)));
            var refused = new List<string>();

            for (int i = written; i < session.History.Count; i++)
            {
                if (IsBindRefusal(session.History[i]))
                {
                    refused.Add(session.History[i]);
                }
            }

            if (refused.Count > 0)
            {
                ShowXrefBindRefused?.Invoke(new XrefBindRefusedViewModel(refused, BindRefusalHint(refused)));
            }
        }

        /// <summary>Whether a line is one -XREF Bind writes where it binds nothing, in AutoCAD 2021's words.</summary>
        private static bool IsBindRefusal(string line) =>
            line.EndsWith("\" is nested and cannot be bound.", StringComparison.Ordinal)
            || line == "Cannot bind unloaded xref(s).  Reload them first:"
            || (
                line.StartsWith("Xref \"", StringComparison.Ordinal)
                && line.EndsWith("\" is unloaded.", StringComparison.Ordinal)
            )
            || line.EndsWith("\" is currently not found. Reload or detach before binding.", StringComparison.Ordinal)
            || line == "No matching xref names found."
            || line.StartsWith("Error: At least one of the specified xrefs failed to bind", StringComparison.Ordinal);

        /// <summary>What to do after a bind refused, by its first line: bind the parent of a nested drawing, reload one unloaded, or find a file.</summary>
        private string BindRefusalHint(List<string> refused)
        {
            string first = refused[0];
            string hint = string.Empty;

            if (first.EndsWith("is nested and cannot be bound.", StringComparison.Ordinal))
            {
                string name = Quoted(first);
                string parent = _current
                    .Document.Xrefs.GetReferences()
                    .FirstOrDefault(r =>
                        r.IsNested && !r.IsImage && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)
                    )
                    ?.ParentName;

                if (parent != null)
                {
                    hint =
                        "Select its parent, "
                        + parent
                        + ", and bind that: "
                        + name
                        + " is bound with it, as "
                        + parent
                        + "$0$… and "
                        + name
                        + "$0$….";
                }
            }
            else if (first.StartsWith("Cannot bind unloaded", StringComparison.Ordinal))
            {
                hint =
                    "Reload "
                    + string.Join(", ", refused.Skip(1).Select(Quoted))
                    + ", then bind again. No reference of the selection was bound.";
            }
            else if (
                first.EndsWith("is currently not found. Reload or detach before binding.", StringComparison.Ordinal)
            )
            {
                hint =
                    "The file of "
                    + Quoted(first)
                    + ", or of a reference it places, is not found. Select New Path, Reload or Detach it, then bind again.";
            }

            return hint.Length == 0
                ? "The same lines are on the command line."
                : hint + " The same lines are on the command line.";
        }

        /// <summary>The name between the first and the last double quote of a line.</summary>
        private static string Quoted(string line)
        {
            int start = line.IndexOf('"');
            int end = line.LastIndexOf('"');
            return end > start ? line.Substring(start + 1, end - start - 1) : line;
        }

        /// <summary>Gets the items of the image frame box of Insert › Reference, IMAGEFRAME 0, 1 and 2 in turn, the drawing's ticked.</summary>
        public IReadOnlyList<StyleBoxItemViewModel> ImageFrameItems =>
            ImageFrameNames
                .Select(
                    (n, i) =>
                        new StyleBoxItemViewModel(
                            n,
                            i == ImageFrame,
                            "IMAGEFRAME " + i.ToString(CultureInfo.InvariantCulture),
                            false
                        )
                )
                .ToList();

        /// <summary>
        /// Gets or sets the image frame box's choice, the name of the drawing's IMAGEFRAME: Hide Frames (0), Display and Plot
        /// Frames (1, also where the drawing has no image yet) or Display but Not Plot Frames (2). Set, IMAGEFRAME is run with
        /// its value, one undo step; a drawing with no image yet is given its variables of images, as AutoCAD 2021 gives them.
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
                DxfRasterVariables variables =
                    database?.NamedObjectsDictionary?.Contains("ACAD_IMAGE_VARS") == true
                        ? database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS") as DxfRasterVariables
                        : null;
                DxfFrameSetting frame = variables == null ? DxfFrameSetting.ImageFrameAbove : variables.ImageFrame;

                // IMAGEFRAME 2 is kept as frames shown and not plotted (3), as AutoCAD 2021 writes it.
                return frame == DxfFrameSetting.ImageFrameOff ? 0
                    : frame == DxfFrameSetting.ImageFrameOnNoPlot ? 2
                    : 1;
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
