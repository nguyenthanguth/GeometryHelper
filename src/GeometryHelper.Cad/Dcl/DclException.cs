using System;
using System.Globalization;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A DCL file that cannot be loaded: a syntax error, a tile not defined, a predefined tile defined again, an include not
    /// found or one that loops. The message is what <c>load_dialog</c> shows, in the lines AutoCAD 2021's box has:
    /// <c>Error in dialog file</c>, <c>"path", line n:</c> and what is wrong; a tile not defined is a semantic error of the
    /// whole file and its first line is <c>Semantic error(s) in DCL file path.</c> instead (D338).
    /// </summary>
    internal sealed class DclException : Exception
    {
        internal DclException(string file, int line, string what, string symbol = null, bool semantic = false)
            : base(Format(file, line, what, semantic))
        {
            File = file;
            Line = line;
            What = what;
            Symbol = symbol;
            Semantic = semantic;
        }

        /// <summary>Gets the file the error is in.</summary>
        internal string File { get; }

        /// <summary>Gets the line, from 1.</summary>
        internal int Line { get; }

        /// <summary>
        /// Gets what is wrong, the lines of the message after the path line (after the first line, for a semantic error): for a
        /// syntax error <c>syntax error.</c> and <c>Symbol: "x".</c> on a line of its own.
        /// </summary>
        internal string What { get; }

        /// <summary>Gets the token a syntax error or a tile defined again stopped at; null for the other errors.</summary>
        internal string Symbol { get; }

        /// <summary>Gets whether this is a semantic error of the file (a tile not defined), not an error at one line.</summary>
        internal bool Semantic { get; }

        /// <summary>Makes the error of a token the grammar has no place for: <c>syntax error.</c> and the token.</summary>
        internal static DclException SyntaxError(string file, int line, string symbol) =>
            new DclException(
                file,
                line,
                string.Format(CultureInfo.InvariantCulture, "syntax error.\nSymbol: \"{0}\".", symbol),
                symbol
            );

        /// <summary>Makes the error of an entry or a tile that is not ended by a <c>;</c>: no symbol is named.</summary>
        internal static DclException MissingSemicolon(string file, int line) =>
            new DclException(file, line, "missing semicolon.");

        private static string Format(string file, int line, string what, bool semantic) =>
            semantic
                ? string.Format(CultureInfo.InvariantCulture, "Semantic error(s) in DCL file {0}.\n{1}", file, what)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Error in dialog file\n\"{0}\", line {1}:\n{2}",
                    file,
                    line,
                    what
                );
    }
}
