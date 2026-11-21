using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// What a callback of a plugin is given when the window runs it: a ribbon button's, a DRAWINGS button's, a menu item's, a
    /// button's <c>CanRun</c> or a contextual tab's <c>ShowWhen</c>. A callback edits the drawing through
    /// <see cref="CadDocument.ApplyEdit"/> (one undo step each), or the editors commands use, as a command does.
    /// </summary>
    public sealed class CadPluginContext
    {
        private readonly Action<string> _write;
        private readonly Action<string> _runCommand;

        internal CadPluginContext(
            CadDocument document,
            IReadOnlyList<string> selected,
            bool isCommandRunning,
            Action<string> write,
            Action<string> runCommand
        )
        {
            Document = document;
            Selected = selected ?? Array.Empty<string>();
            IsCommandRunning = isCommandRunning;
            _write = write;
            _runCommand = runCommand;
        }

        /// <summary>Gets the drawing the window shows; null where none is open.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the handles of the objects selected, in ordinal order; empty for none.</summary>
        public IReadOnlyList<string> Selected { get; }

        /// <summary>Gets whether a command is running on the command line.</summary>
        public bool IsCommandRunning { get; }

        /// <summary>Writes a line on the command line, as a command's message is.</summary>
        /// <param name="line">The line.</param>
        public void Write(string line) => _write?.Invoke(line ?? string.Empty);

        /// <summary>
        /// Starts a command as the presenter's ribbon buttons do, a command running cancelled first, as AutoCAD's ^C^C is:
        /// "NUMCIRCLES", "_NUMCIRCLES" and ".NUMCIRCLES" start the same command.
        /// </summary>
        /// <param name="command">The command's name.</param>
        public void RunCommand(string command) => _runCommand?.Invoke(command ?? string.Empty);
    }
}
