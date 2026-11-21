using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>A loaded DCL file: its definitions, its own and those it included, each resolved to primitives.</summary>
    internal sealed class DclFile
    {
        private readonly Dictionary<string, DclTile> _definitions = new Dictionary<string, DclTile>(
            StringComparer.Ordinal
        );

        internal DclFile(string path, IEnumerable<DclTile> definitions)
        {
            Path = path;

            foreach (DclTile definition in definitions)
            {
                _definitions[definition.Name] = definition;
            }
        }

        /// <summary>Gets the file's full path.</summary>
        internal string Path { get; }

        /// <summary>Gets the definitions by name, case-sensitive; of two with one name the later is kept.</summary>
        internal IReadOnlyDictionary<string, DclTile> Definitions => _definitions;

        /// <summary>Makes a copy of a dialog to run; null where the name is no dialog of the file.</summary>
        internal DclTile NewDialog(string name) =>
            name != null && _definitions.TryGetValue(name, out DclTile found) && found.Kind == "dialog"
                ? found.Copy()
                : null;
    }
}
