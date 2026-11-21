namespace GeometryHelper.Cad.Python
{
    /// <summary>The names of the commands the host adds and the step the question of a script is asked in, in one place so that a rename is one edit.</summary>
    internal static class CadPythonNames
    {
        /// <summary>Shows the console palette (added by the window, not by the host).</summary>
        internal const string Show = "PYTHON";

        /// <summary>Hides the console palette (added by the window, not by the host).</summary>
        internal const string Hide = "PYTHONCLOSE";

        /// <summary>Runs a Python file once.</summary>
        internal const string Run = "PYRUN";

        /// <summary>Loads a Python file that defines commands, and runs its module-level code in a step of this name.</summary>
        internal const string Load = "PYLOAD";

        /// <summary>The undo step of a palette entry.</summary>
        internal const string Entry = "Python";

        /// <summary>The command that carries the question of a script; it is in no registry.</summary>
        internal const string Carrier = "PYTHON";
    }
}
