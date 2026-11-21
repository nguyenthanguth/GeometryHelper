using System;
using System.Globalization;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A DCL file that cannot be loaded: a syntax error, a tile not defined, an include not found or one that loops. The
    /// message is what <c>load_dialog</c> shows: <c>Error in dialog file "path", line n:</c> and what is wrong.
    /// </summary>
    internal sealed class DclException : Exception
    {
        internal DclException(string file, int line, string what, string symbol = null)
            : base(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Error in dialog file \"{0}\", line {1}:\n{2}",
                    file,
                    line,
                    what
                )
            )
        {
            File = file;
            Line = line;
            What = what;
            Symbol = symbol;
        }

        /// <summary>Gets the file the error is in.</summary>
        internal string File { get; }

        /// <summary>Gets the line, from 1.</summary>
        internal int Line { get; }

        /// <summary>Gets what is wrong, the message's second line.</summary>
        internal string What { get; }

        /// <summary>Gets the token a syntax error stopped at; null for the other errors.</summary>
        internal string Symbol { get; }
    }
}
