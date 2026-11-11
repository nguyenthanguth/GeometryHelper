using System;
using System.Collections.Generic;
using System.Reflection;
using GeometryHelper.Dxf.Viewer.Plugins;

namespace GeometryHelper.Drawing.Plugins
{
    /// <summary>
    /// The drawing plugins at hand, found by name when a drawing runs its plugin runs again (<see cref="Drawing.Plugins"/>):
    /// those added in code, and those the presenter's plugin manager loads from a .dll or a .ghplugin of the kind "drawing".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Given a <see cref="CadPluginManager"/>, the catalog reads the drawing plugins out of every plugin it loads, so that
    /// they are loaded, unloaded, reloaded and installed as command plugins are (A14 of the plan): one plugin.json with
    /// <c>"kind": "drawing"</c>, the same shadow copy, trusted folders, startup list and load log. A plugin unloaded is
    /// dropped from the catalog at once, so that on .NET 8 its load context can be freed; a drawing that ran it keeps its
    /// runs, to be run again once it is loaded again.
    /// </para>
    /// <para>
    /// A plugin is a public class derived from <see cref="DrawingPlugin"/> that is not abstract and has a public constructor
    /// that takes nothing. A .dll with one that needs a newer API than <see cref="DrawingPlugin.ApiVersion"/>, or one named as
    /// a plugin at hand, is refused with the reason. Plugins are added, loaded and unloaded on the window's thread;
    /// <see cref="Plugins"/> and <see cref="Find"/> may be read on the thread a drawing is prepared on.
    /// </para>
    /// </remarks>
    public sealed class DrawingPluginCatalog
    {
        private readonly List<DrawingPlugin> _added = new List<DrawingPlugin>();
        private readonly List<KeyValuePair<IReadOnlyList<Assembly>, List<DrawingPlugin>>> _loaded =
            new List<KeyValuePair<IReadOnlyList<Assembly>, List<DrawingPlugin>>>();
        private readonly CadPluginManager _manager;
        private readonly object _gate = new object();

        /// <summary>Makes a catalog of the plugins added in code only.</summary>
        public DrawingPluginCatalog() { }

        /// <summary>Makes a catalog of the plugins added in code and those a plugin manager loads, now and later.</summary>
        /// <param name="manager">The manager; it finds the drawing plugins of what it loads through this catalog from now on.</param>
        /// <exception cref="ArgumentNullException">Thrown when the manager is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the manager finds drawing plugins for another catalog already.</exception>
        public DrawingPluginCatalog(CadPluginManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            if (manager.DrawingPluginFinder != null)
            {
                throw new InvalidOperationException(
                    "The plugin manager finds drawing plugins for another catalog already."
                );
            }

            manager.DrawingPluginFinder = Read;
            manager.PluginsChanged += (s, e) => Prune();
        }

        /// <summary>
        /// Gets the plugins at hand, in ordinal order of their names, either case the same, then of their types' names. It may
        /// be read on the thread a drawing is prepared on.
        /// </summary>
        public IReadOnlyList<DrawingPlugin> Plugins
        {
            get
            {
                var all = new List<DrawingPlugin>();

                lock (_gate)
                {
                    all.AddRange(_added);

                    foreach (KeyValuePair<IReadOnlyList<Assembly>, List<DrawingPlugin>> loaded in _loaded)
                    {
                        all.AddRange(loaded.Value);
                    }
                }

                all.Sort(
                    (a, b) =>
                    {
                        int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                        return order != 0
                            ? order
                            : string.CompareOrdinal(
                                a.GetType().AssemblyQualifiedName,
                                b.GetType().AssemblyQualifiedName
                            );
                    }
                );
                return all;
            }
        }

        /// <summary>Adds a plugin made in code.</summary>
        /// <param name="plugin">The plugin.</param>
        /// <exception cref="ArgumentNullException">Thrown when the plugin is null.</exception>
        /// <exception cref="ArgumentException">Thrown when its name is empty or taken, or it needs a newer API.</exception>
        public void Add(DrawingPlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            string refusal = Refusal(plugin, new DrawingPlugin[0]);

            if (refusal != null)
            {
                throw new ArgumentException(refusal, nameof(plugin));
            }

            lock (_gate)
            {
                _added.Add(plugin);
            }
        }

        /// <summary>Finds a plugin at hand by its name, either case the same.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The plugin, or null.</returns>
        public DrawingPlugin Find(string name)
        {
            foreach (DrawingPlugin plugin in Plugins)
            {
                if (string.Equals(plugin.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return plugin;
                }
            }

            return null;
        }

        /// <summary>The drawing plugins of a plugin the manager loads, kept with its assemblies; their names, for the manager.</summary>
        private IReadOnlyList<string> Read(CadPluginManifest manifest, IReadOnlyList<Assembly> assemblies)
        {
            string name = manifest?.Name ?? (assemblies.Count > 0 ? assemblies[0].GetName().Name : string.Empty);

            // A plugin reloaded is unloaded first: its old build leaves the catalog before its name is judged taken.
            Prune();

            if (
                manifest != null
                && manifest.MinimumApiVersion > DrawingPlugin.ApiVersion
                && string.Equals(manifest.Kind, CadPluginManifest.DrawingKind, StringComparison.Ordinal)
            )
            {
                throw new CadPluginException(
                    name,
                    "The plugin "
                        + name
                        + " needs version "
                        + manifest.MinimumApiVersion
                        + " of the drawing plugin API, and this library has "
                        + DrawingPlugin.ApiVersion
                        + "."
                );
            }

            var types = new List<Type>();

            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in assembly.GetExportedTypes())
                {
                    if (
                        !type.IsAbstract
                        && typeof(DrawingPlugin).IsAssignableFrom(type)
                        && type.GetConstructor(Type.EmptyTypes) != null
                    )
                    {
                        types.Add(type);
                    }
                }
            }

            types.Sort((a, b) => string.CompareOrdinal(a.AssemblyQualifiedName, b.AssemblyQualifiedName));
            var found = new List<DrawingPlugin>();
            var names = new List<string>();

            foreach (Type type in types)
            {
                DrawingPlugin plugin;

                try
                {
                    plugin = (DrawingPlugin)Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Exception cause =
                        exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException
                            : exception;
                    throw new CadPluginException(
                        name,
                        "The drawing plugin " + type.FullName + " could not be made: " + cause.Message,
                        cause
                    );
                }

                string refusal = Refusal(plugin, found);

                if (refusal != null)
                {
                    throw new CadPluginException(name, refusal);
                }

                found.Add(plugin);
                names.Add(plugin.Name);
            }

            if (found.Count > 0)
            {
                lock (_gate)
                {
                    _loaded.Add(new KeyValuePair<IReadOnlyList<Assembly>, List<DrawingPlugin>>(assemblies, found));
                }
            }

            return names;
        }

        /// <summary>Why a plugin cannot join the catalog, or null where it can.</summary>
        private string Refusal(DrawingPlugin plugin, IEnumerable<DrawingPlugin> alsoNew)
        {
            if (string.IsNullOrWhiteSpace(plugin.Name))
            {
                return "The drawing plugin " + plugin.GetType().FullName + " has no name.";
            }

            if (plugin.RequiredApiVersion > DrawingPlugin.ApiVersion)
            {
                return "The drawing plugin "
                    + plugin.Name
                    + " needs version "
                    + plugin.RequiredApiVersion
                    + " of the drawing plugin API, and this library has "
                    + DrawingPlugin.ApiVersion
                    + ".";
            }

            var taken = new List<DrawingPlugin>(Plugins);
            taken.AddRange(alsoNew);

            return taken.Exists(p => string.Equals(p.Name, plugin.Name, StringComparison.OrdinalIgnoreCase))
                ? "A drawing plugin named " + plugin.Name + " is at hand already."
                : null;
        }

        /// <summary>Lets go of the plugins of what the manager no longer has loaded.</summary>
        private void Prune()
        {
            if (_manager == null)
            {
                return;
            }

            IReadOnlyList<CadLoadedPlugin> loaded = _manager.Plugins;

            lock (_gate)
            {
                _loaded.RemoveAll(entry => !Exists(loaded, entry.Key));
            }
        }

        private static bool Exists(IReadOnlyList<CadLoadedPlugin> loaded, IReadOnlyList<Assembly> assemblies)
        {
            foreach (CadLoadedPlugin plugin in loaded)
            {
                if (ReferenceEquals(plugin.Assemblies, assemblies))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
