using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>Reads the text of a DCL file into its resolved definitions.</summary>
    internal static class DclParser
    {
        /// <summary>Reads a DCL file: its includes, its definitions and the predefined tiles, each resolved to primitives.</summary>
        /// <param name="text">The file's text.</param>
        /// <param name="path">The file's full path, for the messages and the folder includes are looked for in first.</param>
        /// <param name="include">Finds a file an <c>@include</c> names, given the name as written and the including file's
        /// path: its full path and text, or null when it is not found.</param>
        /// <returns>The file's definitions.</returns>
        /// <exception cref="DclException">Thrown for a syntax error, a tile not defined, an include not found or one that loops.</exception>
        internal static DclFile Parse(
            string text,
            string path,
            Func<string, string, KeyValuePair<string, string>?> include
        )
        {
            throw new NotImplementedException("The DCL parser is part P of L5.");
        }
    }
}
