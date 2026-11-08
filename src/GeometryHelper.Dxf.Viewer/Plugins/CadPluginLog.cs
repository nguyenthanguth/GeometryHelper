using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>What was done to a plugin, as the load log says it.</summary>
    public enum CadPluginAction
    {
        /// <summary>Loaded from a .dll, a .ghplugin or an installed folder.</summary>
        Load,

        /// <summary>Unloaded, or on .NET Framework switched off.</summary>
        Unload,

        /// <summary>Loaded again from its file.</summary>
        Reload,

        /// <summary>A .ghplugin unpacked into the plugin folder.</summary>
        Install,

        /// <summary>Taken out of the plugin folder.</summary>
        Uninstall,
    }

    /// <summary>One line of the load log the Plugin manager shows: when, which plugin, what, from where, whether trusted, and what came of it.</summary>
    public sealed class CadPluginLogEntry
    {
        internal CadPluginLogEntry(DateTime time, string plugin, CadPluginAction action, string path, bool trusted, bool succeeded, string result)
        {
            Time = time;
            Plugin = plugin;
            Action = action;
            Path = path;
            Trusted = trusted;
            Succeeded = succeeded;
            Result = result;
        }

        /// <summary>Gets when it was done, in local time.</summary>
        public DateTime Time { get; }

        /// <summary>Gets the plugin's name; the file's name where it was refused before its name was read.</summary>
        public string Plugin { get; }

        /// <summary>Gets what was done.</summary>
        public CadPluginAction Action { get; }

        /// <summary>Gets the full path of the file or folder.</summary>
        public string Path { get; }

        /// <summary>Gets whether the path lies in a trusted folder; false where the user confirmed one that does not.</summary>
        public bool Trusted { get; }

        /// <summary>Gets whether it was done; false where it was refused or failed.</summary>
        public bool Succeeded { get; }

        /// <summary>Gets what came of it, in a sentence: "Loaded Number circles 1.0.0: NUMCIRCLES, FAILNOW."</summary>
        public string Result { get; }

        /// <inheritdoc/>
        public override string ToString()
            => Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + Action + " " + Plugin + " " + Path + (Trusted ? " (trusted) " : " (not trusted) ") + Result;
    }

    /// <summary>What came of a load, unload, reload, install or uninstall: whether it was done, the sentence the command line writes, and what was left out.</summary>
    public sealed class CadPluginResult
    {
        internal CadPluginResult(bool succeeded, string message, CadLoadedPlugin plugin, IReadOnlyList<string> warnings)
        {
            Succeeded = succeeded;
            Message = message;
            Plugin = plugin;
            Warnings = warnings ?? Array.Empty<string>();
        }

        /// <summary>Gets whether it was done.</summary>
        public bool Succeeded { get; }

        /// <summary>Gets what came of it, in a sentence, or why it was refused.</summary>
        public string Message { get; }

        /// <summary>Gets the plugin; null where none was found.</summary>
        public CadLoadedPlugin Plugin { get; }

        /// <summary>Gets what was left out, a line each: a method that cannot be a command, a command or alias already taken.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <inheritdoc/>
        public override string ToString() => Warnings.Count == 0 ? Message : Message + " " + string.Join(" ", Warnings);
    }

    /// <summary>
    /// A plugin <see cref="CadPluginManager"/> knows in this session: where it was loaded from, whether it is loaded now,
    /// installed in the plugin folder or on the startup list, and the plugin itself while it is loaded.
    /// </summary>
    public sealed class CadLoadedPlugin
    {
        internal CadLoadedPlugin(string name, string path, CadPluginManifest manifest, bool installed, bool trusted)
        {
            Name = name;
            Path = path;
            Manifest = manifest;
            IsInstalled = installed;
            IsTrusted = trusted;
        }

        /// <summary>Gets the name: the manifest's, or the .dll's without its extension.</summary>
        public string Name { get; }

        /// <summary>Gets the full path it is loaded from: a .dll, a .ghplugin, or the folder of a plugin installed.</summary>
        public string Path { get; }

        /// <summary>Gets the manifest; null for a bare .dll with no plugin.json beside it.</summary>
        public CadPluginManifest Manifest { get; }

        /// <summary>Gets whether it is installed in the plugin folder, and so loaded at every start.</summary>
        public bool IsInstalled { get; }

        /// <summary>Gets whether its path lies in a trusted folder.</summary>
        public bool IsTrusted { get; }

        /// <summary>Gets whether it is on the startup list, loaded at every start.</summary>
        public bool LoadAtStartup { get; internal set; }

        /// <summary>Gets the plugin while it is loaded; null once it is unloaded.</summary>
        public CadPlugin Plugin { get; internal set; }

        /// <summary>Gets whether it is loaded.</summary>
        public bool IsLoaded => Plugin != null;

        /// <summary>Gets the full path of the icon its manifest names, beside its manifest; null where it names none.</summary>
        public string IconPath
        {
            get
            {
                if (Manifest?.Icon == null || string.Equals(System.IO.Path.GetExtension(Path), CadPluginPackage.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string folder = Directory.Exists(Path) ? Path : System.IO.Path.GetDirectoryName(Path);
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, Manifest.Icon));
            }
        }

        internal IReadOnlyList<Assembly> Assemblies { get; set; }

        internal string ShadowFolder { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsLoaded ? " (loaded) " : " (unloaded) ") + Path;
    }
}
