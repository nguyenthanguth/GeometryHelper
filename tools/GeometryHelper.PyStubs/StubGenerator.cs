using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// Writes the <c>GeometryHelper-stubs</c> package: one <c>__init__.pyi</c> per .NET namespace of the assemblies, so that
    /// pyright and Pylance complete and check <c>from GeometryHelper.Geometry import GeoPoint3</c> although that module
    /// exists only at run time, made by pythonnet. The assemblies are read, never run (MetadataLoadContext over the
    /// netstandard 2.0 reference assemblies), every list is sorted and the text has line feeds only, so a second run
    /// writes the same bytes.
    /// </summary>
    /// <remarks>
    /// The layout (one stub package per CLR namespace, <c>@overload</c> for each .NET overload, <c>out</c> parameters
    /// returned as a tuple) follows mcneel/pythonstubs and the table of .NET types follows MHDante/pythonnet-stub-generator,
    /// both MIT. No code is taken from either.
    /// </remarks>
    public static class StubGenerator
    {
        private const string RootNamespace = "GeometryHelper";
        private const string PackageFolder = "GeometryHelper-stubs";

        /// <summary>
        /// Reads every <c>GeometryHelper*.dll</c> of <paramref name="libFolder"/> (with its <c>.xml</c> beside it, for the
        /// docstrings) and writes the stubs to <c>GeometryHelper-stubs</c> inside <paramref name="outFolder"/>, deleting
        /// what was there first.
        /// </summary>
        /// <param name="libFolder">The folder with the assemblies and their XML documentation.</param>
        /// <param name="outFolder">The folder to write <c>GeometryHelper-stubs</c> into.</param>
        /// <param name="referenceFolder">The netstandard 2.0 reference assemblies the assemblies are read against.</param>
        /// <returns>What was written and what was left out.</returns>
        /// <exception cref="InvalidOperationException">An assembly or a name cannot be turned into a stub.</exception>
        public static StubReport Generate(string libFolder, string outFolder, string referenceFolder)
        {
            if (!Directory.Exists(libFolder))
            {
                throw new InvalidOperationException("The folder with the assemblies does not exist: " + libFolder);
            }

            if (!Directory.Exists(referenceFolder))
            {
                throw new InvalidOperationException("The reference assemblies are not at " + referenceFolder);
            }

            string[] files = Directory
                .GetFiles(libFolder, "GeometryHelper*.dll")
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
            {
                throw new InvalidOperationException("No GeometryHelper*.dll in " + libFolder);
            }

            string[] references = Directory.GetFiles(referenceFolder, "*.dll");
            var referenceNames = new HashSet<string>(
                references.Select(Path.GetFileName),
                StringComparer.OrdinalIgnoreCase
            );
            IEnumerable<string> others = Directory
                .GetFiles(libFolder, "*.dll")
                .Where(f => !referenceNames.Contains(Path.GetFileName(f)));
            using (
                var context = new MetadataLoadContext(
                    new PathAssemblyResolver(references.Concat(others)),
                    "netstandard"
                )
            )
            {
                Assembly[] assemblies = files
                    .Select(context.LoadFromAssemblyPath)
                    .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
                    .ToArray();
                return Write(assemblies, libFolder, outFolder);
            }
        }

        private static StubReport Write(Assembly[] assemblies, string libFolder, string outFolder)
        {
            var ours = new HashSet<Assembly>(assemblies);
            Func<Type, bool> isOurs = t => ours.Contains(t.Assembly);
            var leftOut = new List<string>();
            var typeAssembly = new Dictionary<Type, string>();
            var byNamespace = new SortedDictionary<string, List<Type>>(StringComparer.Ordinal);
            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in assembly.GetTypes().Where(t => t.IsVisible))
                {
                    string skip = Skipped(type);
                    if (skip != null)
                    {
                        leftOut.Add(skip + ": " + type.FullName);
                        continue;
                    }

                    if (PythonNames.IsReservedTypeName(type.Name))
                    {
                        throw new InvalidOperationException(
                            type.FullName + " has a name the stubs already use for something else."
                        );
                    }

                    typeAssembly[type] = assembly.GetName().Name;
                    if (!byNamespace.TryGetValue(type.Namespace, out List<Type> list))
                    {
                        byNamespace[type.Namespace] = list = new List<Type>();
                    }

                    list.Add(type);
                }
            }

            var docs = new DocLibrary(
                assemblies.Select(a => Path.Combine(libFolder, a.GetName().Name + ".xml")).Where(File.Exists)
            );
            var mapper = new TypeMapper(isOurs);
            string header = Header(assemblies);
            string folder = Path.Combine(outFolder, PackageFolder);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            var types = new Dictionary<string, int>(StringComparer.Ordinal);
            var members = new Dictionary<string, int>(StringComparer.Ordinal);
            var modules = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<Type>> entry in byNamespace)
            {
                List<Type> sorted = entry
                    .Value.OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ThenBy(t => t.FullName, StringComparer.Ordinal)
                    .ToList();
                var module = new ModuleContext(entry.Key, sorted.Select(t => t.Name));
                var writer = new ModuleWriter(module, mapper, docs, isOurs, leftOut);
                var body = new StringBuilder();
                foreach (Type type in sorted)
                {
                    body.Append(writer.WriteType(type, out int count)).Append('\n');
                    string name = typeAssembly[type];
                    types[name] = types.TryGetValue(name, out int t) ? t + 1 : 1;
                    members[name] = members.TryGetValue(name, out int m) ? m + count : count;
                    if (!modules.TryGetValue(name, out HashSet<string> set))
                    {
                        modules[name] = set = new HashSet<string>(StringComparer.Ordinal);
                    }

                    set.Add(entry.Key);
                }

                string imports = module.ImportLines();
                string text =
                    header + imports + (imports.Length > 0 ? "\n" : "") + "\n" + body.ToString().TrimEnd('\n') + "\n";
                string path = Path.Combine(folder, ModulePath(entry.Key));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }

            return new StubReport(
                assemblies
                    .Select(a => a.GetName().Name)
                    .Where(types.ContainsKey)
                    .Select(n => new AssemblyStubs(n, types[n], members[n], modules[n].Count))
                    .ToList(),
                byNamespace.Keys.ToList(),
                leftOut.OrderBy(s => s, StringComparer.Ordinal).ToList(),
                mapper.AnyMapped.ToList(),
                folder
            );
        }

        /// <summary>Why a public type is not written, or null when it is.</summary>
        private static string Skipped(Type type)
        {
            if (type.IsNested)
            {
                return "nested type";
            }

            return type.IsGenericTypeDefinition ? "generic type" : null;
        }

        private static string Header(Assembly[] assemblies)
        {
            AssemblyName main = (
                assemblies.FirstOrDefault(a => a.GetName().Name == RootNamespace) ?? assemblies[0]
            ).GetName();
            string version = main.Name + " " + main.Version.Major + "." + main.Version.Minor + "." + main.Version.Build;
            // Python cannot tell apart .NET overloads that differ only in an out parameter or in being static, and an
            // override may rename a parameter or lack the setter of the property it overrides, so pyright is told not to
            // report those in the stubs themselves.
            return "# Generated by tools/GeometryHelper.PyStubs from "
                + version
                + ". Do not edit.\n"
                + "# pyright: reportOverlappingOverload=false, reportInconsistentOverload=false, "
                + "reportIncompatibleMethodOverride=false, reportIncompatibleVariableOverride=false\n";
        }

        /// <summary>The path of the stub of a namespace inside the package: <c>Dxf/__init__.pyi</c> for <c>GeometryHelper.Dxf</c>.</summary>
        private static string ModulePath(string ns)
        {
            if (ns == RootNamespace)
            {
                return "__init__.pyi";
            }

            if (!ns.StartsWith(RootNamespace + ".", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The namespace " + ns + " is not inside " + RootNamespace + ".");
            }

            return ns.Substring(RootNamespace.Length + 1).Replace('.', Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar
                + "__init__.pyi";
        }
    }
}
