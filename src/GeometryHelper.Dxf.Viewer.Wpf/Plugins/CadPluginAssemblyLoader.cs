using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GeometryHelper.Dxf.Viewer.Plugins;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace GeometryHelper.Dxf.Viewer.Wpf.Plugins
{
    /// <summary>
    /// The loader of plugins for the runtime the presenter runs on, given to <see cref="CadPluginManager"/>. On .NET 8 each
    /// plugin is loaded into a collectible <c>AssemblyLoadContext</c> of its own, which unloading frees, so that a new build
    /// is taken without a restart and two plugins' dependencies do not clash (D19 of the plan). On .NET Framework, hosted in
    /// Tekla, every plugin is loaded into the one AppDomain with <c>Assembly.LoadFrom</c>, and nothing is unloaded: unloading
    /// only switches a plugin off.
    /// </summary>
    /// <remarks>
    /// Either way the presenter's own assemblies (GeometryHelper, GeometryHelper.Dxf.Viewer, ...) are never loaded from a
    /// plugin's folder: a plugin is given the presenter's, so that the <see cref="CadCommandContext"/> it is handed is of the
    /// type it was built against. A dependency a plugin asks for is taken from its folder of copies, copied there first from
    /// the folder of a bare .dll where it is missing.
    /// </remarks>
    public sealed class CadPluginAssemblyLoader : CadPluginLoader
    {
        /// <summary>The presenter's own assemblies, by simple name, never loaded from a plugin's folder.</summary>
        private static readonly string[] Own =
        {
            typeof(GeometryHelperLog).Assembly.GetName().Name,
            typeof(CadPlugin).Assembly.GetName().Name,
            typeof(CadPluginAssemblyLoader).Assembly.GetName().Name,
        };

#if NET8_0_OR_GREATER
        /// <summary>Gets true: a plugin's load context is freed when it is unloaded.</summary>
        public override bool CanUnload => true;

        /// <inheritdoc/>
        public override IReadOnlyList<Assembly> Load(string name, IReadOnlyList<string> files, string probeFolder)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var loaded = new List<Assembly>();

            if (files.Count == 0)
            {
                return loaded;
            }

            var context = new PluginContext(name, Path.GetDirectoryName(files[0]), probeFolder);

            try
            {
                foreach (string file in files)
                {
                    if (!PluginContext.IsShared(AssemblyName.GetAssemblyName(file)))
                    {
                        loaded.Add(context.LoadFromAssemblyPath(file));
                    }
                }
            }
            catch
            {
                context.Unload();
                throw;
            }

            if (loaded.Count == 0)
            {
                context.Unload();
            }

            return loaded;
        }

        /// <inheritdoc/>
        public override void Unload(IReadOnlyList<Assembly> assemblies)
        {
            if (assemblies == null || assemblies.Count == 0)
            {
                return;
            }

            if (AssemblyLoadContext.GetLoadContext(assemblies[0]) is PluginContext context)
            {
                context.Unload();
            }
        }

        /// <summary>The collectible load context of one plugin.</summary>
        private sealed class PluginContext : AssemblyLoadContext
        {
            private readonly string _folder;
            private readonly string _probeFolder;

            internal PluginContext(string name, string folder, string probeFolder)
                : base("GeometryHelper plugin " + name, true)
            {
                _folder = folder;
                _probeFolder = probeFolder;
            }

            /// <summary>Whether the presenter has an assembly of the name, or can load one: then the plugin is given the presenter's.</summary>
            internal static bool IsShared(AssemblyName name)
            {
                if (Array.Exists(Own, n => string.Equals(n, name.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                foreach (Assembly assembly in Default.Assemblies)
                {
                    if (string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                try
                {
                    Default.LoadFromAssemblyName(new AssemblyName(name.Name));
                    return true;
                }
                catch (Exception exception) when (exception is FileNotFoundException || exception is FileLoadException || exception is BadImageFormatException)
                {
                    return false;
                }
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                // Null hands the name to the default context: the presenter's own assembly.
                if (IsShared(assemblyName))
                {
                    return null;
                }

                string path = Locate(_folder, _probeFolder, assemblyName.Name);
                return path != null ? LoadFromAssemblyPath(path) : null;
            }
        }
#else
        private static readonly object Gate = new object();

        /// <summary>The folders of copies of the plugins loaded, the latest first, each with the folder its dependencies are copied from.</summary>
        private static readonly List<KeyValuePair<string, string>> Folders = new List<KeyValuePair<string, string>>();

        private static bool _resolving;

        /// <summary>Gets false: .NET Framework cannot unload an assembly, so unloading only switches a plugin off.</summary>
        public override bool CanUnload => false;

        /// <inheritdoc/>
        public override IReadOnlyList<Assembly> Load(string name, IReadOnlyList<string> files, string probeFolder)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var loaded = new List<Assembly>();

            if (files.Count == 0)
            {
                return loaded;
            }

            lock (Gate)
            {
                if (!_resolving)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                    _resolving = true;
                }

                Folders.Insert(0, new KeyValuePair<string, string>(Path.GetDirectoryName(files[0]), probeFolder));
            }

            foreach (string file in files)
            {
                if (Shared(AssemblyName.GetAssemblyName(file).Name) == null)
                {
                    loaded.Add(Assembly.LoadFrom(file));
                }
            }

            return loaded;
        }

        /// <summary>Does nothing: an assembly loaded into an AppDomain stays until the process ends.</summary>
        /// <param name="assemblies">The assemblies.</param>
        public override void Unload(IReadOnlyList<Assembly> assemblies)
        {
        }

        /// <summary>The presenter's own assembly of a simple name, loaded from outside the plugins' copies; null where there is none.</summary>
        private static Assembly Shared(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase) || assembly.IsDynamic)
                {
                    continue;
                }

                bool own = Array.Exists(Own, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

                if (own || !InCopies(assembly.Location))
                {
                    return assembly;
                }
            }

            return null;
        }

        private static bool InCopies(string location)
        {
            lock (Gate)
            {
                foreach (KeyValuePair<string, string> folder in Folders)
                {
                    if (!string.IsNullOrEmpty(location) && location.StartsWith(folder.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            Assembly shared = Shared(name.Name);

            if (shared != null)
            {
                return shared;
            }

            // The folder of the plugin that asks first, then every plugin's, the latest loaded first.
            string asking = args.RequestingAssembly != null && !args.RequestingAssembly.IsDynamic ? Path.GetDirectoryName(args.RequestingAssembly.Location) : null;
            var folders = new List<KeyValuePair<string, string>>();

            lock (Gate)
            {
                folders.AddRange(Folders.FindAll(f => string.Equals(f.Key, asking, StringComparison.OrdinalIgnoreCase)));
                folders.AddRange(Folders.FindAll(f => !string.Equals(f.Key, asking, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (KeyValuePair<string, string> folder in folders)
            {
                string path = Locate(folder.Key, folder.Value, name.Name);

                if (path != null)
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }
#endif

        /// <summary>The path of a dependency in a plugin's folder of copies, copied there from the probe folder where missing; null where neither has it.</summary>
        private static string Locate(string folder, string probeFolder, string name)
        {
            string path = Path.Combine(folder, name + ".dll");

            if (File.Exists(path))
            {
                return path;
            }

            string original = probeFolder == null ? null : Path.Combine(probeFolder, name + ".dll");

            if (original == null || !File.Exists(original))
            {
                return null;
            }

            File.Copy(original, path, true);
            return path;
        }
    }
}
