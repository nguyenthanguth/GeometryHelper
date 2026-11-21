using System;

namespace GeometryHelper.Cad.Python
{
    /// <summary>A run of Python that started or ended.</summary>
    public sealed class CadPythonRunEventArgs : EventArgs
    {
        internal CadPythonRunEventArgs(
            string name,
            CadDocument document,
            CadPythonOutcome? outcome,
            string error,
            string path
        )
        {
            Name = name;
            Path = path;
            Document = document;
            Outcome = outcome;
            Error = error;
        }

        /// <summary>Gets the name of the undo step the run makes: <c>PYRUN</c>, the name of a command PYLOAD defined, <c>PYLOAD</c> or <c>Python</c>.</summary>
        public string Name { get; }

        /// <summary>Gets the full path of the file a PYRUN or PYLOAD run reads, so that a window can name it (<c>PYRUN circles.py</c>); null for a palette entry, a command PYLOAD defined, and a run refused before its file was read.</summary>
        public string Path { get; }

        /// <summary>Gets the drawing the run works on.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets how the run ended; null when it has only started.</summary>
        public CadPythonOutcome? Outcome { get; }

        /// <summary>Gets the last line of the traceback, <c>ZeroDivisionError: division by zero</c>, or the reason a run was refused; null where there is none.</summary>
        public string Error { get; }
    }
}
