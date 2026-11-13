using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// What was left of the acad.pgp gap list in round 3, item R3.2, that is the window's own: OPEN typed, DRAWINGRECOVERY, and
    /// the quick views QVDRAWING and QVLAYOUT with their CLOSE, which show and hide the Drawings list and the Model and layout
    /// tabs, the presenter's own quick views; no thumbnails are drawn.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private bool _spaceTabsShown = true;

        /// <summary>
        /// Gets whether the Model and layout tabs at the foot of the window are shown: QVLAYOUT shows them, QVLAYOUTCLOSE hides
        /// them. Not kept between runs.
        /// </summary>
        public bool AreSpaceTabsShown
        {
            get => _spaceTabsShown;
            private set => Set(ref _spaceTabsShown, value);
        }

        private void AddPgpR32WindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("OPEN", OpenBody));
            registry.Add(new CadCommand("DRAWINGRECOVERY", DrawingRecoveryBody));
            registry.Add(new CadCommand("QVDRAWING", c => RunWindow(() => IsFileListShown = true)));
            registry.Add(new CadCommand("QVDRAWINGCLOSE", c => RunWindow(() => IsFileListShown = false)));
            registry.Add(new CadCommand("QVLAYOUT", c => RunWindow(() => AreSpaceTabsShown = true)));
            registry.Add(new CadCommand("QVLAYOUTCLOSE", c => RunWindow(() => AreSpaceTabsShown = false)));
        }

        /// <summary>
        /// Runs a command of this file that needs no drawing, typed or as an alias, with none open: OPEN (and OPENURL and
        /// DXFIN), the quick views, and DRAWINGRECOVERY, which lists the copies and leaves opening one to OPEN.
        /// </summary>
        private bool RunPgpR32CommandWithoutDocument(string word)
        {
            switch (word.ToUpperInvariant())
            {
                case "OPEN":
                case "OPENURL":
                case "DXFIN":
                    RequestOpenDialog();
                    return true;
                case "QVDRAWING":
                case "QVD":
                    IsFileListShown = true;
                    return true;
                case "QVDRAWINGCLOSE":
                case "QVDC":
                    IsFileListShown = false;
                    return true;
                case "QVLAYOUT":
                case "QVL":
                    AreSpaceTabsShown = true;
                    return true;
                case "QVLAYOUTCLOSE":
                case "QVLC":
                    AreSpaceTabsShown = false;
                    return true;
                case "DRAWINGRECOVERY":
                case "DRM":
                    IReadOnlyList<BackupCopy> copies = BackupCopies();

                    foreach (string line in BackupLines(copies))
                    {
                        Say(line);
                    }

                    if (copies.Count > 0)
                    {
                        Say("Open the one wanted with OPEN.");
                    }

                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The window's Open dialog; with no window to show it, a line that Ctrl+O does.</summary>
        private void RequestOpenDialog()
        {
            if (OpenRequested == null)
            {
                Say("OPEN is shown by the window: Ctrl+O.");
            }
            else
            {
                OpenRequested(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// OPEN: the window's Open dialog; with FILEDIA 0, "Enter name of drawing to open:" on the command line, the drawing
        /// opened once the command has ended. A name with no extension is looked for as .dwg and then as .dxf, and one with no
        /// folder in the folder of the drawing shown and then in the folder the app runs in; a file not found is told, "Cannot
        /// find the specified drawing file."
        /// </summary>
        private IEnumerable<CadPrompt> OpenBody(CadCommandContext c)
        {
            if (c.Session.FileDialogs)
            {
                _afterCommand = RequestOpenDialog;
                yield break;
            }

            yield return CadPrompt.ForString("Enter name of drawing to open");
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

            if (typed.Length == 0)
            {
                yield break;
            }

            string found = FindDrawing(c.Document, typed);

            if (found == null)
            {
                c.Write("Cannot find the specified drawing file.");
                yield break;
            }

            _afterCommand = () => Open(found);
        }

        /// <summary>The file a name typed names: as it is, then with .dwg and with .dxf where it has no extension; null where there is none.</summary>
        private static string FindDrawing(CadDocument document, string typed)
        {
            var folders = new List<string>();

            if (Path.IsPathRooted(typed))
            {
                folders.Add(string.Empty);
            }
            else
            {
                if (document.HasFile && !string.IsNullOrEmpty(document.FileName))
                {
                    folders.Add(Path.GetDirectoryName(Path.GetFullPath(document.FileName)));
                }

                folders.Add(Directory.GetCurrentDirectory());
            }

            string[] names = Path.HasExtension(typed)
                ? new[] { typed }
                : new[] { typed, typed + ".dwg", typed + ".dxf" };

            foreach (string folder in folders)
            {
                foreach (string name in names)
                {
                    try
                    {
                        string path = folder.Length == 0 ? name : Path.Combine(folder, name);

                        if (File.Exists(path))
                        {
                            return Path.GetFullPath(path);
                        }
                    }
                    catch (Exception ex)
                        when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                    {
                        // A name that is no path names no file.
                    }
                }
            }

            return null;
        }

        /// <summary>A copy autosave kept beside a drawing: its file, the drawing it is a copy of, and how many saves ago.</summary>
        private sealed class BackupCopy
        {
            public BackupCopy(string path, string original, int age)
            {
                Path = path;
                Original = original;
                Age = age;
            }

            public string Path { get; }

            public string Original { get; }

            public int Age { get; }
        }

        /// <summary>
        /// The copies autosave left beside the drawings listed, in the order of the folder, the drawing and the age, the newest
        /// first: plan.autosave1.dxf of plan.dxf, plan.dwg.autosave2.dxf of plan.dwg, in each folder of the Drawings list and
        /// of the drawings open.
        /// </summary>
        private IReadOnlyList<BackupCopy> BackupCopies()
        {
            var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KnownFile file in _files.Values)
            {
                string path = file.Document != null && file.Document.HasFile ? file.Document.FileName : file.Path;

                if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path))
                {
                    folders.Add(Path.GetDirectoryName(Path.GetFullPath(path)));
                }
            }

            var copies = new List<BackupCopy>();

            foreach (string folder in folders)
            {
                string[] names;

                try
                {
                    names = Directory.Exists(folder)
                        ? Directory.GetFiles(folder, "*.autosave*.dxf")
                        : Array.Empty<string>();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string path in names)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    int at = name.LastIndexOf(".autosave", StringComparison.OrdinalIgnoreCase);

                    if (
                        at > 0
                        && int.TryParse(
                            name.Substring(at + 9),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int age
                        )
                    )
                    {
                        copies.Add(new BackupCopy(path, name.Substring(0, at), age));
                    }
                }
            }

            return copies
                .OrderBy(b => Path.GetDirectoryName(b.Path), StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => Path.GetDirectoryName(b.Path), StringComparer.Ordinal)
                .ThenBy(b => b.Original, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Original, StringComparer.Ordinal)
                .ThenBy(b => b.Age)
                .ThenBy(b => b.Path, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The lines that list the copies: "  1  plan.autosave1.dxf  12 KB  saved 2026-11-13 08:12", or that there are none.</summary>
        private static IEnumerable<string> BackupLines(IReadOnlyList<BackupCopy> copies)
        {
            if (copies.Count == 0)
            {
                yield return "No backup files were found beside the drawings listed.";
                yield break;
            }

            yield return "Backup files:";

            for (int i = 0; i < copies.Count; i++)
            {
                var info = new FileInfo(copies[i].Path);
                yield return (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(3)
                    + "  "
                    + info.Name
                    + "  "
                    + Math.Max(1L, (info.Length + 1023L) / 1024L).ToString(CultureInfo.InvariantCulture)
                    + " KB  saved "
                    + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// DRAWINGRECOVERY (no palette is built, so on the command line): the copies autosave keeps beside the drawings listed
        /// are listed, "Enter number of the backup file to open &lt;none&gt;:", and the one chosen is opened once the command
        /// has ended; a number out of the list is told and asked again.
        /// </summary>
        private IEnumerable<CadPrompt> DrawingRecoveryBody(CadCommandContext c)
        {
            IReadOnlyList<BackupCopy> copies = BackupCopies();

            foreach (string line in BackupLines(copies))
            {
                c.Write(line);
            }

            if (copies.Count == 0)
            {
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForString("Enter number of the backup file to open", "none");

                if (
                    c.Input.Kind != CadInputKind.Text
                    || c.Input.Text.Trim().Length == 0
                    || c.Input.Text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
                )
                {
                    yield break;
                }

                if (
                    int.TryParse(c.Input.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int chosen)
                    && chosen >= 1
                    && chosen <= copies.Count
                )
                {
                    string path = copies[chosen - 1].Path;
                    _afterCommand = () => Open(path);
                    yield break;
                }

                c.Write("Requires a number from 1 to " + copies.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
    }
}
