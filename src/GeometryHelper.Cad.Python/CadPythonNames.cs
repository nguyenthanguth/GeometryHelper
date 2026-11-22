namespace GeometryHelper.Cad.Python
{
    /// <summary>The names of the commands the host adds and the step the question of a script is asked in, in one place so that a rename is one edit.</summary>
    internal static class CadPythonNames
    {
        /// <summary>Runs a Python file once.</summary>
        internal const string Run = "PYRUN";

        /// <summary>Loads a Python file that defines commands, and runs its module-level code in a step of this name.</summary>
        internal const string Load = "PYLOAD";

        /// <summary>The undo step of a palette entry.</summary>
        internal const string Entry = "Python";

        /// <summary>The command that carries the question of a script; it is in no registry.</summary>
        internal const string Carrier = "PYTHON";

        /// <summary>The answer of <c>cmd</c> that hands one prompt to the user: one backslash, as AutoLISP's <c>pause</c>.</summary>
        internal const string Pause = "\\";
    }
}
