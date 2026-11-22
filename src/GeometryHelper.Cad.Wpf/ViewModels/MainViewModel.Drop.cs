using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Files dropped on the window, as AutoCAD 2021 takes them: a drawing opens and a plugin package installs, an AutoLISP,
    /// compiled or script file runs the line a drafter would type, and any other file gets a box that says no program is
    /// associated with it.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private static readonly string[] LoadedByDrop = { ".lsp", ".fas", ".vlx" };

        /// <summary>
        /// Gets or sets how the window shows a message with OK alone, under a title, when a file is dropped that nothing can
        /// take: the title is <c>Drag and Drop</c> and the text <c>No association..D:\x\notes.txt</c> (two dots, as AutoCAD
        /// writes it). Null where no window does, when the file is passed over.
        /// </summary>
        public Action<string, string> ShowDropMessage { get; set; }

        /// <summary>
        /// Takes the files dropped on the window, each in the order given. A drawing opens, as before, and a <c>.ghplugin</c>
        /// installs. A <c>.lsp</c>, <c>.fas</c> or <c>.vlx</c> runs <c>(LOAD "D:/x/tools.lsp")</c> as if typed at the command
        /// line of the drawing shown, so that the echo, the SECURELOAD question and the <c>; error:</c> line are those of a typed
        /// load; a <c>.scr</c> runs SCRIPT and answers its prompt with the path in quotes; a <c>.arx</c>, <c>.dbx</c> or
        /// <c>.dvb</c> is refused as APPLOAD refuses it; any other file gets the Drag and Drop box (see
        /// <see cref="ShowDropMessage"/>). While a command or a routine waits, and with no drawing open, those files are
        /// passed over without a word, as AutoCAD passes them over.
        /// </summary>
        /// <param name="files">The full paths, in the order of the drop.</param>
        internal void DropFiles(IReadOnlyList<string> files)
        {
            foreach (string file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                if (CadDrawingFile.IsDrawingName(file))
                {
                    Open(file);
                }
                else if (file.EndsWith(".ghplugin", StringComparison.OrdinalIgnoreCase))
                {
                    InstallPlugin(file);
                }
                else if (_current != null && !_current.Session.IsRunning && !_current.Session.IsLispWaiting)
                {
                    DropFile(System.IO.Path.GetFullPath(file));
                }
            }
        }

        // One file that is not a drawing or a plugin package, dropped with the command prompt showing.
        private void DropFile(string full)
        {
            string extension = System.IO.Path.GetExtension(full).ToLowerInvariant();

            if (Array.IndexOf(LoadedByDrop, extension) >= 0)
            {
                // The typed line of AutoCAD's echo: LOAD in capitals, the path in quotes with forward slashes.
                Execute("(LOAD \"" + full.Replace('\\', '/') + "\")");
            }
            else if (extension == ".scr")
            {
                Execute("_SCRIPT");
                Execute("\"" + full + "\"");
            }
            else if (Array.IndexOf(RefusedApplicationTypes, extension) >= 0)
            {
                LoadApplication(full);
            }
            else
            {
                ShowDropMessage?.Invoke("Drag and Drop", "No association.." + full);
            }
        }
    }
}
