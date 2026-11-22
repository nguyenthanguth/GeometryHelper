using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Files dropped on the window, as AutoCAD 2021 takes them: a drawing is inserted as a block (opened where dropped on the
    /// DRAWINGS list), an AutoLISP,
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
        /// Takes the files dropped on the window, each in the order given. A drawing dropped on the DRAWINGS list opens. One
        /// dropped on the drawing area, where a drawing is open, is inserted as a block as AutoCAD 2021 inserts it: the typed
        /// <c>_-INSERT</c> with the path in quotes, the Units line, and then the insertion point, the scale and the rotation
        /// asked as ever (Esc cancels). AutoCAD inserts one drawing at a time, so with several dropped the first is inserted
        /// and the others are passed over without a word, as a drop is while a command waits. With no drawing open the
        /// drawing opens, as AutoCAD opens it then. A <c>.lsp</c>,
        /// <c>.fas</c> or <c>.vlx</c> runs <c>(LOAD "D:/x/tools.lsp")</c> as if typed at the command
        /// line of the drawing shown, so that the echo, the SECURELOAD question and the <c>; error:</c> line are those of a typed
        /// load; a <c>.scr</c> runs SCRIPT and answers its prompt with the path in quotes; a <c>.arx</c>, <c>.dbx</c> or
        /// <c>.dvb</c> is refused as APPLOAD refuses it; any other file, a <c>.dll</c> and a <c>.ghplugin</c> too (a plugin
        /// goes in the Plugins folder, or loads with PLUGINLOAD), gets the Drag and Drop box (see
        /// <see cref="ShowDropMessage"/>). While a command or a routine waits, and with no drawing open, those files are
        /// passed over without a word, as AutoCAD passes them over.
        /// </summary>
        /// <param name="files">The full paths, in the order of the drop.</param>
        /// <param name="onDrawingList">Whether the drop fell on the DRAWINGS list (or the title bar), where a drawing opens;
        /// false for the drawing area and the rest of the window.</param>
        internal void DropFiles(IReadOnlyList<string> files, bool onDrawingList = false)
        {
            // Decided before the first file, so that a drawing opened by this drop is not the one the next is inserted in.
            bool insert = !onDrawingList && _current != null;

            foreach (string file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                bool waiting = _current != null && (_current.Session.IsRunning || _current.Session.IsLispWaiting);

                if (CadDrawingFile.IsDrawingName(file))
                {
                    if (!insert)
                    {
                        Open(file);
                    }
                    else if (!waiting)
                    {
                        // The typed line of AutoCAD's echo (rows 13 and 14 of the measurements): -INSERT, then its name in quotes.
                        Execute("_-INSERT");
                        Execute("\"" + System.IO.Path.GetFullPath(file) + "\"");
                    }
                }
                else if (_current != null && !waiting)
                {
                    DropFile(System.IO.Path.GetFullPath(file));
                }
            }
        }

        // One file that is not a drawing, dropped with the command prompt showing.
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
