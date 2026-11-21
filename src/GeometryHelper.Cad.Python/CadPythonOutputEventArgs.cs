using System;

namespace GeometryHelper.Cad.Python
{
    /// <summary>One whole line a script wrote, or an error the host reports.</summary>
    public sealed class CadPythonOutputEventArgs : EventArgs
    {
        internal CadPythonOutputEventArgs(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }

        /// <summary>Gets one whole line, without the line end.</summary>
        public string Text { get; }

        /// <summary>Gets whether the line is from stderr or is part of a traceback or a refusal.</summary>
        public bool IsError { get; }
    }
}
