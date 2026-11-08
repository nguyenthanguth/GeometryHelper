using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>
    /// The command plugins of the presenter, as AutoCAD's NETLOAD and APPLOAD keep them (A14 of the plan): loaded from a
    /// .dll, a .ghplugin package or the plugin folder, unloaded and loaded again while the presenter runs, their commands
    /// added to the command line of every drawing open (<see cref="Attach"/>), every load written in the load log
    /// (<see cref="Log"/>) and logged with its full path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Shadow copy.</b> A plugin's files are copied to a folder of the manager's own (<see cref="ShadowFolder"/>) and
    /// loaded from there, so that the .dll a plugin's author builds is never locked: build it again and PLUGINRELOAD picks
    /// it up. On .NET 8 each plugin has a collectible load context, which unloading frees; on .NET Framework (hosted in
    /// Tekla) nothing can be unloaded, so PLUGINUNLOAD only takes the plugin's commands away and a new build is taken when
    /// the presenter restarts (D19). <see cref="CadPluginLoader"/> is what differs.
    /// </para>
    /// <para>
    /// <b>Trusted folders</b>, as AutoCAD's TRUSTEDPATHS (D21): loading a plugin runs its code with the user's rights, so a
    /// file outside <see cref="TrustedFolders"/> is loaded only when <see cref="ConfirmUntrusted"/> says yes, and never where
    /// it is not set.
    /// </para>
    /// <para>
    /// <b>At every start</b> (<see cref="LoadAtStart"/>) the plugins installed in <see cref="PluginFolder"/> are loaded, then
    /// those on the startup list, which is kept with the trusted folders in the JSON file <see cref="SettingsPath"/>.
    /// </para>
    /// <para>The manager is used from one thread, the window's.</para>
    /// </remarks>
    public sealed class CadPluginManager : IDisposable
    {
        /// <summary>The name of the command that loads a plugin by its path: PLUGINLOAD, alias NETLOAD.</summary>
        public const string LoadCommand = "PLUGINLOAD";

        /// <summary>The name of the command that unloads a plugin by its name: PLUGINUNLOAD.</summary>
        public const string UnloadCommand = "PLUGINUNLOAD";

        /// <summary>The name of the command that loads a plugin again from its file by its name: PLUGINRELOAD.</summary>
        public const string ReloadCommand = "PLUGINRELOAD";

        private const string LoadAlias = "NETLOAD";

        private readonly CadPluginLoader _loader;
        private readonly List<CadLoadedPlugin> _plugins = new List<CadLoadedPlugin>();
        private readonly List<CadCommandRegistry> _registries = new List<CadCommandRegistry>();
        private readonly List<CadPluginLogEntry> _log = new List<CadPluginLogEntry>();
        private readonly List<string> _startup = new List<string>();
        private readonly List<string> _trusted = new List<string>();
        private readonly CadCommand[] _commands;
        private int _loads;

        /// <summary>Makes the manager of the presenter's plugins, reading its startup list and trusted folders from <paramref name="settingsPath"/>.</summary>
        /// <param name="loader">The loader of the runtime: <c>new CadPluginAssemblyLoader()</c> of GeometryHelper.Dxf.Viewer.Wpf.</param>
        /// <param name="pluginFolder">The plugin folder: each plugin installed in a folder of its own with its plugin.json; always trusted.</param>
        /// <param name="settingsPath">The JSON file of the startup list and the trusted folders; read when it exists, written on each change.</param>
        /// <param name="shadowFolder">The folder the plugins' files are copied to and loaded from; a folder of this process's
        /// under the temporary folder where null, those left by processes that have ended deleted.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loader, the plugin folder or the settings path is null.</exception>
        public CadPluginManager(CadPluginLoader loader, string pluginFolder, string settingsPath, string shadowFolder = null)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            PluginFolder = Path.GetFullPath(pluginFolder ?? throw new ArgumentNullException(nameof(pluginFolder)));
            SettingsPath = Path.GetFullPath(settingsPath ?? throw new ArgumentNullException(nameof(settingsPath)));

            if (shadowFolder == null)
            {
                string root = Path.Combine(Path.GetTempPath(), "GeometryHelper", "PluginCopies");
                DeleteLeftCopies(root);
                shadowFolder = Path.Combine(root, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            }

            ShadowFolder = Path.GetFullPath(shadowFolder);
            _commands = new[]
            {
                new CadCommand(LoadCommand, LoadByPath),
                new CadCommand(UnloadCommand, c => ByName(c, "unload", Unload)),
                new CadCommand(ReloadCommand, c => ByName(c, "reload", Reload)),
            };
            ReadSettings();
        }

        /// <summary>Raised when a plugin is loaded, unloaded, installed or uninstalled, or put on or off the startup list.</summary>
        public event EventHandler PluginsChanged;

        /// <summary>Raised when a line is added to <see cref="Log"/>.</summary>
        public event EventHandler LogChanged;

        /// <summary>Gets the plugin folder, always trusted, whose plugins are loaded at every start.</summary>
        public string PluginFolder { get; }

        /// <summary>Gets the JSON file of the startup list and the trusted folders.</summary>
        public string SettingsPath { get; }

        /// <summary>Gets the folder the plugins' files are copied to and loaded from.</summary>
        public string ShadowFolder { get; }

        /// <summary>Gets whether unloading a plugin frees its code (.NET 8), or only switches it off (.NET Framework).</summary>
        public bool CanUnload => _loader.CanUnload;

        /// <summary>
        /// Gets or sets what is asked before a plugin outside the trusted folders is loaded: given the file's full path, it
        /// says whether to load it, as a dialog that asks the user does. Null, as it is until set, loads none.
        /// </summary>
        public Func<string, bool> ConfirmUntrusted { get; set; }

        /// <summary>Gets the trusted folders, full paths: the plugin folder first, then those added, in the order they were added.</summary>
        public IReadOnlyList<string> TrustedFolders
        {
            get
            {
                var folders = new List<string> { PluginFolder };
                folders.AddRange(_trusted);
                return folders;
            }
        }

        /// <summary>Gets the paths of the startup list, loaded at every start after the plugin folder, in the order they were put on it.</summary>
        public IReadOnlyList<string> StartupList => _startup.ToArray();

        /// <summary>Gets the plugins known in this session, loaded or not, in ordinal order of their names, either case the same, then of their paths.</summary>
        public IReadOnlyList<CadLoadedPlugin> Plugins => _plugins.ToArray();

        /// <summary>Gets the load log, the oldest line first; <see cref="LogChanged"/> is raised as it grows.</summary>
        public IReadOnlyList<CadPluginLogEntry> Log => _log.AsReadOnly();

        /// <summary>Finds a plugin known in this session by its name, either case the same.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The plugin, or null.</returns>
        public CadLoadedPlugin Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string key = Unquote(name);
            return _plugins.Find(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds the commands of the loaded plugins to a registry, and those of every plugin loaded later, with PLUGINLOAD
        /// (alias NETLOAD), PLUGINUNLOAD and PLUGINRELOAD, which ask on the command line for a path or a name. A command the
        /// registry has already is left, so that the window's own PLUGINLOAD, which shows a file dialog, can be added first.
        /// </summary>
        /// <param name="registry">The registry of a drawing's command line.</param>
        /// <returns>The commands and aliases left out as taken, a line each.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        public IReadOnlyList<string> Attach(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var refused = new List<string>();

            if (_registries.Contains(registry))
            {
                return refused;
            }

            _registries.Add(registry);

            foreach (CadCommand command in _commands)
            {
                if (registry.Find(command.Name) == null)
                {
                    registry.Add(command);
                }
            }

            if (!registry.Shortcuts.Aliases.ContainsKey(LoadAlias) && registry.Find(LoadAlias) == null)
            {
                var aliases = new List<KeyValuePair<string, string>>(registry.Shortcuts.Aliases) { new KeyValuePair<string, string>(LoadAlias, LoadCommand) };
                registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
            }

            foreach (CadLoadedPlugin plugin in _plugins)
            {
                if (plugin.IsLoaded)
                {
                    AddDistinct(refused, plugin.Plugin.AddTo(registry));
                }
            }

            return refused;
        }

        /// <summary>Takes the plugins' commands and those <see cref="Attach"/> added out of a registry, as when its drawing is closed.</summary>
        /// <param name="registry">The registry.</param>
        /// <returns>Whether it was attached.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        public bool Detach(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (!_registries.Remove(registry))
            {
                return false;
            }

            foreach (CadLoadedPlugin plugin in _plugins)
            {
                plugin.Plugin?.RemoveFrom(registry);
            }

            foreach (CadCommand command in _commands)
            {
                if (ReferenceEquals(registry.Find(command.Name), command))
                {
                    registry.Remove(command.Name);
                }
            }

            if (registry.Shortcuts.Aliases.TryGetValue(LoadAlias, out string target) && string.Equals(target, LoadCommand, StringComparison.OrdinalIgnoreCase))
            {
                var aliases = new List<KeyValuePair<string, string>>();

                foreach (KeyValuePair<string, string> alias in registry.Shortcuts.Aliases)
                {
                    if (!string.Equals(alias.Key, LoadAlias, StringComparison.OrdinalIgnoreCase))
                    {
                        aliases.Add(alias);
                    }
                }

                registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
            }

            return true;
        }

        /// <summary>
        /// Loads a plugin for this session, as PLUGINLOAD and NETLOAD do: from a .dll (with the plugin.json beside it, where
        /// there is one), a .ghplugin package, or the folder of a plugin installed. Its commands are added to every registry
        /// attached. Refused, with the reason, where the path is outside the trusted folders and not confirmed, the plugin
        /// needs a newer API, a plugin of its name or path is loaded already, or it has no command.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public CadPluginResult Load(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return LoadFrom(Unquote(path), CadPluginAction.Load, false);
        }

        /// <summary>
        /// Unloads a plugin, as PLUGINUNLOAD does: its commands go from every registry, and a run of one going on fails at its
        /// next step. On .NET 8 its load context is freed; on .NET Framework its code stays loaded, switched off, until the
        /// presenter restarts. It stays in <see cref="Plugins"/>, so that it can be loaded again.
        /// </summary>
        /// <param name="name">The plugin's name, either case.</param>
        /// <returns>What came of it.</returns>
        public CadPluginResult Unload(string name)
        {
            CadLoadedPlugin plugin = Find(name);

            if (plugin == null || !plugin.IsLoaded)
            {
                return Refuse(CadPluginAction.Unload, name ?? string.Empty, string.Empty, false, "No plugin " + name + " is loaded.");
            }

            UnloadQuietly(plugin);
            string message = CanUnload
                ? "Unloaded " + plugin.Name + "."
                : "Switched " + plugin.Name + " off: its commands are gone, and its code stays loaded until the presenter restarts.";
            Record(CadPluginAction.Unload, plugin.Name, plugin.Path, plugin.IsTrusted, true, message);
            Changed();
            return new CadPluginResult(true, message, plugin, null);
        }

        /// <summary>
        /// Loads a plugin again from its file, as PLUGINRELOAD does, unloading it first where it is loaded: a plugin's author
        /// changes its code, builds it and reloads it with the presenter running. On .NET Framework the build loaded first in
        /// the session stays, and the result says so.
        /// </summary>
        /// <param name="name">The plugin's name, either case.</param>
        /// <returns>What came of it.</returns>
        public CadPluginResult Reload(string name)
        {
            CadLoadedPlugin plugin = Find(name);

            if (plugin == null)
            {
                return Refuse(CadPluginAction.Reload, name ?? string.Empty, string.Empty, false, "No plugin " + name + " is known in this session.");
            }

            // A plugin whose file has gone stays loaded.
            if (!File.Exists(plugin.Path) && !Directory.Exists(plugin.Path))
            {
                return Refuse(CadPluginAction.Reload, plugin.Name, plugin.Path, plugin.IsTrusted, "There is no file " + plugin.Path + " to reload " + plugin.Name + " from.");
            }

            if (plugin.IsLoaded)
            {
                UnloadQuietly(plugin);
            }

            return LoadFrom(plugin.Path, CadPluginAction.Reload, false);
        }

        /// <summary>
        /// Installs a .ghplugin package: unpacked into a folder of the plugin folder named by its id, the plugin of the same
        /// name or folder unloaded and replaced first, then loaded. A package outside the trusted folders is installed only
        /// when confirmed, as a load is.
        /// </summary>
        /// <param name="packagePath">The path of the package.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public CadPluginResult Install(string packagePath)
        {
            if (packagePath == null)
            {
                throw new ArgumentNullException(nameof(packagePath));
            }

            string path = Path.GetFullPath(Unquote(packagePath));
            string file = Path.GetFileName(path);

            if (!string.Equals(Path.GetExtension(path), CadPluginPackage.Extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                return Refuse(CadPluginAction.Install, file, path, false, "There is no package " + path + ": a plugin is installed from a .ghplugin file.");
            }

            CadPluginManifest manifest;

            try
            {
                manifest = CadPluginManifest.Read(path);
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException || exception is UnauthorizedAccessException)
            {
                return Refuse(CadPluginAction.Install, file, path, false, "The package " + path + " cannot be read: " + exception.Message, exception);
            }

            string refusal = manifest.Refusal();
            bool trusted = IsTrusted(path);

            if (refusal != null)
            {
                return Refuse(CadPluginAction.Install, manifest.Name, path, trusted, refusal);
            }

            if (manifest.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || manifest.Id == "." || manifest.Id == "..")
            {
                return Refuse(CadPluginAction.Install, manifest.Name, path, trusted, "The plugin's id \"" + manifest.Id + "\" cannot name a folder.");
            }

            if (!trusted && !Confirmed(path))
            {
                return Refuse(CadPluginAction.Install, manifest.Name, path, false, NotTrusted(path));
            }

            string folder = Path.Combine(PluginFolder, manifest.Id);

            foreach (CadLoadedPlugin old in _plugins.ToArray())
            {
                if (string.Equals(old.Path, folder, StringComparison.OrdinalIgnoreCase) || string.Equals(old.Name, manifest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (old.IsLoaded)
                    {
                        UnloadQuietly(old);
                    }

                    _plugins.Remove(old);
                }
            }

            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }

                CadPluginPackage.Unpack(path, folder);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is FormatException)
            {
                Changed();
                return Refuse(CadPluginAction.Install, manifest.Name, path, trusted, "The package " + path + " cannot be unpacked into " + folder + ": " + exception.Message, exception);
            }

            Record(CadPluginAction.Install, manifest.Name, path, trusted, true, "Installed " + manifest.Name + " " + manifest.Version + " into " + folder + ".");
            return LoadFrom(folder, CadPluginAction.Load, true);
        }

        /// <summary>Takes a plugin installed in the plugin folder out of it, unloading it first.</summary>
        /// <param name="name">The plugin's name, either case.</param>
        /// <returns>What came of it; refused for a plugin not installed, which PLUGINUNLOAD unloads.</returns>
        public CadPluginResult Uninstall(string name)
        {
            CadLoadedPlugin plugin = Find(name);

            if (plugin == null || !plugin.IsInstalled)
            {
                return Refuse(CadPluginAction.Uninstall, name ?? string.Empty, plugin?.Path ?? string.Empty, false, "No plugin " + name + " is installed in the plugin folder.");
            }

            if (plugin.IsLoaded)
            {
                UnloadQuietly(plugin);
            }

            _plugins.Remove(plugin);

            try
            {
                Directory.Delete(plugin.Path, true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Changed();
                return Refuse(CadPluginAction.Uninstall, plugin.Name, plugin.Path, true, "The folder " + plugin.Path + " cannot be deleted: " + exception.Message, exception);
            }

            string message = "Uninstalled " + plugin.Name + ".";
            Record(CadPluginAction.Uninstall, plugin.Name, plugin.Path, true, true, message);
            Changed();
            return new CadPluginResult(true, message, plugin, null);
        }

        /// <summary>Puts a plugin on the startup list, or takes it off, and writes the list; a plugin installed is loaded at every start anyway.</summary>
        /// <param name="name">The plugin's name, either case.</param>
        /// <param name="loadAtStartup">Whether it is to be loaded at every start.</param>
        /// <returns>False where no plugin of the name is known, or it is installed.</returns>
        public bool SetLoadAtStartup(string name, bool loadAtStartup)
        {
            CadLoadedPlugin plugin = Find(name);

            if (plugin == null || plugin.IsInstalled)
            {
                return false;
            }

            int at = _startup.FindIndex(p => string.Equals(p, plugin.Path, StringComparison.OrdinalIgnoreCase));

            if (loadAtStartup && at < 0)
            {
                _startup.Add(plugin.Path);
            }
            else if (!loadAtStartup && at >= 0)
            {
                _startup.RemoveAt(at);
            }

            plugin.LoadAtStartup = loadAtStartup;
            WriteSettings();
            Changed();
            return true;
        }

        /// <summary>Adds a trusted folder, whose plugins load without asking, and writes the list.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>False where it is trusted already.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the folder is null.</exception>
        public bool AddTrustedFolder(string folder)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            string full = Path.GetFullPath(Unquote(folder)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (string trusted in TrustedFolders)
            {
                if (string.Equals(trusted.TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            _trusted.Add(full);
            WriteSettings();
            return true;
        }

        /// <summary>Takes a trusted folder off the list and writes it; the plugin folder stays trusted.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>Whether it was on the list.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the folder is null.</exception>
        public bool RemoveTrustedFolder(string folder)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            string full = Path.GetFullPath(Unquote(folder)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int at = _trusted.FindIndex(t => string.Equals(t, full, StringComparison.OrdinalIgnoreCase));

            if (at < 0)
            {
                return false;
            }

            _trusted.RemoveAt(at);
            WriteSettings();
            return true;
        }

        /// <summary>Gets whether a file or folder lies in one of the trusted folders.</summary>
        /// <param name="path">The path.</param>
        /// <returns>Whether it is trusted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool IsTrusted(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string full = Path.GetFullPath(Unquote(path));

            foreach (string folder in TrustedFolders)
            {
                string inside = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (full.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Loads what is loaded at every start: each plugin installed in the plugin folder (a folder with its plugin.json), in
        /// ordinal order of the folders' names, then the startup list in its order. Each is a line of the log; a plugin
        /// refused does not stop the others.
        /// </summary>
        /// <returns>What came of each, in the order they were loaded.</returns>
        public IReadOnlyList<CadPluginResult> LoadAtStart()
        {
            var results = new List<CadPluginResult>();

            if (Directory.Exists(PluginFolder))
            {
                var folders = new List<string>(Directory.GetDirectories(PluginFolder));
                folders.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (string folder in folders)
                {
                    if (File.Exists(Path.Combine(folder, CadPluginManifest.FileName)) && !IsLoaded(folder))
                    {
                        results.Add(LoadFrom(folder, CadPluginAction.Load, false));
                    }
                }
            }

            foreach (string path in _startup.ToArray())
            {
                if (!IsLoaded(path))
                {
                    results.Add(LoadFrom(path, CadPluginAction.Load, false));
                }
            }

            return results;
        }

        /// <summary>Unloads every plugin and deletes the copies of their files that can be deleted; a copy the runtime still holds is deleted at a later start.</summary>
        public void Dispose()
        {
            foreach (CadLoadedPlugin plugin in _plugins)
            {
                if (plugin.IsLoaded)
                {
                    UnloadQuietly(plugin);
                }
            }

            TryDelete(ShadowFolder);
        }

        private CadPluginResult LoadFrom(string path, CadPluginAction action, bool installing)
        {
            string full;

            try
            {
                full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return Refuse(action, path, path, false, "\"" + path + "\" is not a path: " + exception.Message);
            }

            string file = Path.GetFileName(full);
            string extension = Path.GetExtension(full);
            bool isFolder = Directory.Exists(full);
            bool isPackage = !isFolder && string.Equals(extension, CadPluginPackage.Extension, StringComparison.OrdinalIgnoreCase);
            bool isDll = !isFolder && string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase);

            if (!(isFolder || ((isPackage || isDll) && File.Exists(full))))
            {
                return Refuse(action, file, full, false, "There is no plugin " + full + ": a plugin is a .dll, a .ghplugin, or the folder of one installed.");
            }

            bool trusted = installing || IsTrusted(full);
            CadPluginManifest manifest = null;
            string manifestPath = isPackage ? full : Path.Combine(isFolder ? full : Path.GetDirectoryName(full), CadPluginManifest.FileName);

            if (isPackage || isFolder || File.Exists(manifestPath))
            {
                try
                {
                    manifest = CadPluginManifest.Read(manifestPath);
                }
                catch (Exception exception) when (exception is FormatException || exception is IOException || exception is UnauthorizedAccessException)
                {
                    return Refuse(action, file, full, trusted, "The manifest of " + full + " cannot be read: " + exception.Message, exception);
                }

                string refusal = manifest.Refusal();

                if (refusal != null)
                {
                    return Refuse(action, manifest.Name, full, trusted, refusal);
                }
            }

            string name = manifest?.Name ?? Path.GetFileNameWithoutExtension(full);

            foreach (CadLoadedPlugin loaded in _plugins)
            {
                if (loaded.IsLoaded && (string.Equals(loaded.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(loaded.Path, full, StringComparison.OrdinalIgnoreCase)))
                {
                    return Refuse(action, name, full, trusted, "The plugin " + loaded.Name + " is loaded already, from " + loaded.Path + "; PLUGINRELOAD loads it again.");
                }
            }

            if (!trusted && !Confirmed(full))
            {
                return Refuse(action, name, full, false, NotTrusted(full));
            }

            string copies = Path.Combine(ShadowFolder, (++_loads).ToString("D3", CultureInfo.InvariantCulture) + "-" + Safe(name));
            IReadOnlyList<Assembly> assemblies = null;
            CadPlugin plugin;

            try
            {
                List<string> files = Copy(full, isFolder, isPackage, copies);
                assemblies = _loader.Load(name, files, isDll ? Path.GetDirectoryName(full) : null);
                plugin = CadPlugin.FromAssemblies(name, assemblies, manifest?.Version);
            }
            catch (Exception exception)
            {
                if (assemblies != null)
                {
                    _loader.Unload(assemblies);
                }

                TryDelete(copies);
                string reason = exception is CadPluginException ? exception.Message : "The plugin " + name + " cannot be loaded from " + full + ": " + exception.Message;
                return Refuse(action, name, full, trusted, reason, exception is CadPluginException ? null : exception);
            }

            var warnings = new List<string>(plugin.Problems);

            if (!_loader.CanUnload && FirstBuildKept(assemblies, copies))
            {
                warnings.Add("The build of " + name + " loaded first in this session runs: .NET Framework takes a new build when the presenter restarts.");
            }

            foreach (CadCommandRegistry registry in _registries)
            {
                AddDistinct(warnings, plugin.AddTo(registry));
            }

            _plugins.RemoveAll(p => !p.IsLoaded && (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Path, full, StringComparison.OrdinalIgnoreCase)));
            bool installed = isFolder && IsIn(full, PluginFolder);
            var entry = new CadLoadedPlugin(name, full, manifest, installed, trusted)
            {
                Plugin = plugin,
                Assemblies = assemblies,
                ShadowFolder = copies,
                LoadAtStartup = installed || _startup.Exists(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase)),
            };
            _plugins.Add(entry);
            _plugins.Sort(Compare);

            var names = new List<string>();

            foreach (CadPluginCommand command in plugin.Commands)
            {
                names.Add(command.Name);
            }

            string message = (action == CadPluginAction.Reload ? "Reloaded " : "Loaded ") + name + (plugin.Version.Length > 0 ? " " + plugin.Version : string.Empty) + ": " + string.Join(", ", names) + ".";
            Record(action, name, full, trusted, true, message + (warnings.Count > 0 ? " " + string.Join(" ", warnings) : string.Empty));
            Changed();
            return new CadPluginResult(true, message, entry, warnings);
        }

        /// <summary>Copies a plugin's files into its folder of copies; the .dll files to load, in ordinal order.</summary>
        private static List<string> Copy(string path, bool isFolder, bool isPackage, string copies)
        {
            Directory.CreateDirectory(copies);

            if (isPackage)
            {
                CadPluginPackage.Unpack(path, copies);
            }
            else if (isFolder)
            {
                CopyFolder(path, copies);
            }
            else
            {
                string dll = Path.Combine(copies, Path.GetFileName(path));
                File.Copy(path, dll, true);
                string pdb = Path.ChangeExtension(path, ".pdb");

                if (File.Exists(pdb))
                {
                    File.Copy(pdb, Path.ChangeExtension(dll, ".pdb"), true);
                }

                return new List<string> { dll };
            }

            var files = new List<string>(Directory.GetFiles(copies, "*.dll", SearchOption.TopDirectoryOnly));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }

            foreach (string folder in Directory.GetDirectories(from))
            {
                CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
            }
        }

        /// <summary>Whether the runtime gave the assemblies loaded before in the session rather than the copies just made, as .NET Framework does.</summary>
        private static bool FirstBuildKept(IReadOnlyList<Assembly> assemblies, string copies)
        {
            foreach (Assembly assembly in assemblies)
            {
                if (!IsIn(assembly.Location, copies))
                {
                    return true;
                }
            }

            return false;
        }

        private void UnloadQuietly(CadLoadedPlugin plugin)
        {
            plugin.Plugin.Disable();
            IReadOnlyList<Assembly> assemblies = plugin.Assemblies;
            plugin.Plugin = null;
            plugin.Assemblies = null;
            _loader.Unload(assemblies);

            // On .NET Framework the files stay in use; they go at a later start.
            if (_loader.CanUnload)
            {
                TryDelete(plugin.ShadowFolder);
            }
        }

        private bool IsLoaded(string path)
            => _plugins.Exists(p => p.IsLoaded && string.Equals(p.Path, path.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));

        private bool Confirmed(string path)
        {
            Func<string, bool> confirm = ConfirmUntrusted;

            if (confirm == null)
            {
                return false;
            }

            try
            {
                return confirm(path);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPluginManager: asking whether to load " + path + " failed; it is not loaded.", exception);
                return false;
            }
        }

        private static string NotTrusted(string path) => path + " is outside the trusted folders, and loading it was not confirmed.";

        private CadPluginResult Refuse(CadPluginAction action, string plugin, string path, bool trusted, string reason, Exception exception = null)
        {
            Record(action, plugin, path, trusted, false, reason, exception);
            return new CadPluginResult(false, reason, null, null);
        }

        private void Record(CadPluginAction action, string plugin, string path, bool trusted, bool succeeded, string result, Exception exception = null)
        {
            _log.Add(new CadPluginLogEntry(DateTime.Now, plugin, action, path, trusted, succeeded, result));
            string line = "CadPluginManager: " + action + " " + plugin + " from " + path + (trusted ? " (trusted): " : " (not trusted): ") + result;

            if (exception != null)
            {
                GeometryHelperLog.Warn(line, exception);
            }
            else
            {
                GeometryHelperLog.Info(line);
            }

            LogChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Changed() => PluginsChanged?.Invoke(this, EventArgs.Empty);

        private IEnumerable<CadPrompt> LoadByPath(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter the path of a plugin (.dll or .ghplugin)");

            if (c.Input.Kind != CadInputKind.Text || string.IsNullOrWhiteSpace(c.Input.Text))
            {
                yield break;
            }

            c.Write(Load(c.Input.Text).ToString());
        }

        private IEnumerable<CadPrompt> ByName(CadCommandContext c, string verb, Func<string, CadPluginResult> act)
        {
            var names = new List<string>();

            foreach (CadLoadedPlugin plugin in _plugins)
            {
                if (plugin.IsLoaded || verb == "reload")
                {
                    names.Add(plugin.Name);
                }
            }

            if (names.Count == 0)
            {
                c.Write("No plugin is " + (verb == "reload" ? "known" : "loaded") + ".");
                yield break;
            }

            c.Write("Plugins: " + string.Join(", ", names) + ".");
            yield return CadPrompt.ForString("Enter the name of the plugin to " + verb, names.Count == 1 ? names[0] : null);
            string name = c.Input.Kind == CadInputKind.Text && !string.IsNullOrWhiteSpace(c.Input.Text) ? c.Input.Text : (names.Count == 1 ? names[0] : null);

            if (name == null)
            {
                yield break;
            }

            c.Write(act(name).ToString());
        }

        private void ReadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            try
            {
                Settings settings;

                using (FileStream stream = File.OpenRead(SettingsPath))
                {
                    settings = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                }

                foreach (string path in settings?.Startup ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(path) && !_startup.Exists(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        _startup.Add(path);
                    }
                }

                foreach (string folder in settings?.Trusted ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(folder) && !_trusted.Exists(t => string.Equals(t, folder, StringComparison.OrdinalIgnoreCase)))
                    {
                        _trusted.Add(folder);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SerializationException || exception is System.Xml.XmlException)
            {
                GeometryHelperLog.Warn("CadPluginManager: the plugin settings " + SettingsPath + " cannot be read; no plugin is on the startup list and only the plugin folder is trusted.", exception);
            }
        }

        private void WriteSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var settings = new Settings { Startup = new List<string>(_startup), Trusted = new List<string>(_trusted) };

                using (var stream = new MemoryStream())
                {
                    using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
                    {
                        new DataContractJsonSerializer(typeof(Settings)).WriteObject(writer, settings);
                    }

                    File.WriteAllText(SettingsPath, Encoding.UTF8.GetString(stream.ToArray()).Replace("\\/", "/").Replace("\r\n", "\n") + "\n");
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn("CadPluginManager: the plugin settings " + SettingsPath + " cannot be written.", exception);
            }
        }

        private static void AddDistinct(List<string> lines, IEnumerable<string> more)
        {
            foreach (string line in more)
            {
                if (!lines.Contains(line))
                {
                    lines.Add(line);
                }
            }
        }

        private static int Compare(CadLoadedPlugin a, CadLoadedPlugin b)
        {
            int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return order != 0 ? order : string.CompareOrdinal(a.Path, b.Path);
        }

        private static bool IsIn(string path, string folder)
            => !string.IsNullOrEmpty(path) && path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string Unquote(string text)
        {
            string trimmed = text.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"' ? trimmed.Substring(1, trimmed.Length - 2).Trim() : trimmed;
        }

        private static string Safe(string name)
        {
            var safe = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == ' ' ? '_' : c);
            }

            return safe.ToString();
        }

        private static void TryDelete(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Debug("CadPluginManager: the copies in " + folder + " are still in use; they are deleted at a later start.");
            }
        }

        /// <summary>Deletes the folders of copies left by processes that have ended, each named by its process id.</summary>
        private static void DeleteLeftCopies(string root)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            int self = Process.GetCurrentProcess().Id;
            var folders = new List<string>(Directory.GetDirectories(root));
            folders.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in folders)
            {
                if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id == self || Running(id))
                {
                    continue;
                }

                TryDelete(folder);
            }
        }

        private static bool Running(int id)
        {
            try
            {
                using (Process process = Process.GetProcessById(id))
                {
                    return !process.HasExited;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // A process of another user's, which may not be asked: its copies are left alone.
                return true;
            }
        }

        [DataContract]
        private sealed class Settings
        {
            [DataMember(Name = "startup", Order = 0)]
            public List<string> Startup { get; set; }

            [DataMember(Name = "trusted", Order = 1)]
            public List<string> Trusted { get; set; }
        }
    }
}
