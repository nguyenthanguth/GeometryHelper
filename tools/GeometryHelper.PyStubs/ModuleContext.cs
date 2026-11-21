using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// What one stub module has used from elsewhere while it was written: the <c>typing</c> names, the stubs of the .NET
    /// namespaces (<c>System-stubs</c>) and the types of our other namespaces, so that its import lines can be written
    /// afterwards. A type of another namespace is imported by its simple name, and under an alias only when that name is
    /// already taken in the module.
    /// </summary>
    internal sealed class ModuleContext
    {
        private readonly HashSet<string> _taken;
        private readonly Dictionary<string, string> _importedAs = new Dictionary<string, string>(
            StringComparer.Ordinal
        );
        private readonly SortedSet<string> _imports = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _bclModules = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _typing = new SortedSet<string>(StringComparer.Ordinal);

        public ModuleContext(string ns, IEnumerable<string> ownNames)
        {
            Namespace = ns;
            _taken = new HashSet<string>(ownNames, StringComparer.Ordinal);
        }

        /// <summary>The .NET namespace this module stands for.</summary>
        public string Namespace { get; }

        /// <summary>Uses a name of the <c>typing</c> module and gives it back.</summary>
        public string Typing(string name)
        {
            _typing.Add(name);
            return name;
        }

        /// <summary>Uses a class of the stubs of a .NET namespace and gives its dotted name, <c>System.Array</c>.</summary>
        public string Bcl(string ns, string name)
        {
            _bclModules.Add(ns);
            return ns + "." + name;
        }

        /// <summary>The name to write for a type of ours, with its import recorded when it is of another namespace.</summary>
        public string Ours(Type type)
        {
            if (type.Namespace == Namespace)
            {
                return type.Name;
            }

            if (_importedAs.TryGetValue(type.FullName, out string known))
            {
                return known;
            }

            string local = type.Name;
            if (_taken.Contains(local))
            {
                string ns = type.Namespace.StartsWith("GeometryHelper.", StringComparison.Ordinal)
                    ? type.Namespace.Substring("GeometryHelper.".Length)
                    : type.Namespace;
                local = "_" + ns.Replace('.', '_') + "_" + type.Name;
                while (_taken.Contains(local))
                {
                    local += "_";
                }
            }

            _taken.Add(local);
            _importedAs[type.FullName] = local;
            _imports.Add(
                "from " + type.Namespace + " import " + type.Name + (local == type.Name ? "" : " as " + local)
            );
            return local;
        }

        /// <summary>The import lines of the module: the .NET stubs, <c>typing</c>, then the types of our other namespaces.</summary>
        public string ImportLines()
        {
            var text = new StringBuilder();
            if (_bclModules.Count > 0)
            {
                text.Append("import System\n");
                foreach (string module in _bclModules)
                {
                    if (module != "System")
                    {
                        text.Append("import ").Append(module).Append('\n');
                    }
                }
            }

            if (_typing.Count > 0)
            {
                text.Append("from typing import ").Append(string.Join(", ", _typing)).Append('\n');
            }

            foreach (string line in _imports)
            {
                text.Append(line).Append('\n');
            }

            return text.ToString();
        }
    }
}
