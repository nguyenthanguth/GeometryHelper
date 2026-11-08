using System.Collections.Generic;
using System.Reflection;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>
    /// Loads the assemblies of a plugin for <see cref="CadPluginManager"/>, and unloads them, as the runtime allows: on .NET 8
    /// each plugin into a collectible load context of its own, which unloading frees; on .NET Framework into the one
    /// AppDomain, from which nothing is ever unloaded. The presenter's controls give the loader of the runtime they run on
    /// (<c>GeometryHelper.Dxf.Viewer.Wpf.Plugins.CadPluginAssemblyLoader</c>); this library, built for netstandard2.0, has
    /// no load contexts to give one of its own.
    /// </summary>
    public abstract class CadPluginLoader
    {
        /// <summary>Gets whether <see cref="Unload"/> frees a plugin's code, so that a new build of it can be loaded while the presenter runs.</summary>
        public abstract bool CanUnload { get; }

        /// <summary>
        /// Loads a plugin's assemblies from the copies of its files the manager made (its shadow copy). An assembly the
        /// presenter has itself, as GeometryHelper.Dxf.Viewer, is not loaded a second time: the plugin is given the
        /// presenter's.
        /// </summary>
        /// <param name="name">The plugin's name, which names its load context.</param>
        /// <param name="files">The full paths of the .dll files to load, in ordinal order, all in one folder of copies.</param>
        /// <param name="probeFolder">The folder a dependency missing beside the copies is copied from when it is asked for:
        /// the folder of a bare .dll; null where the copies hold all of the plugin's files.</param>
        /// <returns>The assemblies loaded, in the order of the files; those the presenter has itself left out.</returns>
        public abstract IReadOnlyList<Assembly> Load(string name, IReadOnlyList<string> files, string probeFolder);

        /// <summary>Unloads the assemblies <see cref="Load"/> gave, where <see cref="CanUnload"/> says it can; does nothing where it cannot.</summary>
        /// <param name="assemblies">The assemblies.</param>
        public abstract void Unload(IReadOnlyList<Assembly> assemblies);
    }
}
