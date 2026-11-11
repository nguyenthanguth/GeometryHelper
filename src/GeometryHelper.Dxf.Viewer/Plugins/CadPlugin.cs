using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>
    /// A command plugin made from its assemblies: the methods marked <see cref="CadCommandAttribute"/> as commands, the
    /// properties marked <see cref="CadSettingAttribute"/> as settings, found by reflection, and added to the command lines
    /// of the open drawings with <see cref="AddTo"/>.
    /// </summary>
    /// <remarks>
    /// Every run of one of its commands is one undo step, whatever it edits through the document, and a run that throws has
    /// every change of it taken back, the error written on the command line with the plugin's name and logged as a Warn by
    /// the command line. A plugin edits only through the public members of <see cref="CadCommandContext"/> and
    /// <see cref="CadDocument"/>, so the owners' gate (<see cref="CadDocument.EditGate"/>) refuses what it refuses for any
    /// command. <see cref="CadPluginManager"/> loads and unloads plugins from files; a plugin's own tests can make one from
    /// its assembly with <see cref="FromAssemblies"/> and run its commands in a <see cref="CadCommandSession"/>.
    /// </remarks>
    public sealed class CadPlugin
    {
        private readonly List<Registration> _registrations = new List<Registration>();

        private CadPlugin(string name, string version, Version requiredApiVersion)
        {
            Name = name;
            Version = version;
            RequiredApiVersion = requiredApiVersion;
        }

        /// <summary>Gets the version of the plugin API this build of the presenter has: 1.0.</summary>
        public static Version ApiVersion { get; } = new Version(1, 0);

        /// <summary>Gets the plugin's name: the name of its manifest, or of its assembly.</summary>
        public string Name { get; }

        /// <summary>Gets the plugin's version: its manifest's, or its first assembly's informational or assembly version.</summary>
        public string Version { get; }

        /// <summary>Gets the least version of the plugin API its assemblies state they need; 1.0 where none states one.</summary>
        public Version RequiredApiVersion { get; }

        /// <summary>Gets the commands, in ordinal order of their names.</summary>
        public IReadOnlyList<CadPluginCommand> Commands { get; private set; }

        /// <summary>Gets the settings, by the full name of the class that declares each, then in the order the class declares them.</summary>
        public IReadOnlyList<CadPluginSetting> Settings { get; private set; }

        /// <summary>Gets what was left out while the plugin was read, a line each: a method marked as a command that cannot be one, a name taken twice.</summary>
        public IReadOnlyList<string> Problems { get; private set; }

        /// <summary>Gets whether its commands still run; false once it is unloaded, after which a run of one that was going on fails.</summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Makes a plugin from its assemblies, already loaded. A plugin that states a newer version of the plugin API than
        /// <see cref="ApiVersion"/>, or that has no command, is refused; a method marked as a command that cannot be one is
        /// left out and said in <see cref="Problems"/>.
        /// </summary>
        /// <param name="name">The plugin's name; the name of the first assembly where null or empty.</param>
        /// <param name="assemblies">The assemblies, read in ordinal order of their full names.</param>
        /// <param name="version">The plugin's version, as its manifest gives it; the first assembly's where null.</param>
        /// <returns>The plugin.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the assemblies are null.</exception>
        /// <exception cref="CadPluginException">Thrown when the plugin needs a newer API, states an API version that is not
        /// one, or has no command.</exception>
        public static CadPlugin FromAssemblies(string name, IEnumerable<Assembly> assemblies, string version = null) =>
            MakeFrom(name, assemblies, version, true);

        /// <summary>A plugin from its assemblies; one with no command refused only where commands are needed, as they are not for one that holds drawing plugins.</summary>
        internal static CadPlugin MakeFrom(
            string name,
            IEnumerable<Assembly> assemblies,
            string version,
            bool needsCommand
        )
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var sorted = new List<Assembly>();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly != null)
                {
                    sorted.Add(assembly);
                }
            }

            sorted.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            if (string.IsNullOrEmpty(name))
            {
                name = sorted.Count > 0 ? sorted[0].GetName().Name : "(no assembly)";
            }

            Version required = new Version(1, 0);

            foreach (Assembly assembly in sorted)
            {
                var api = (CadPluginApiAttribute)Attribute.GetCustomAttribute(assembly, typeof(CadPluginApiAttribute));

                if (api == null)
                {
                    continue;
                }

                if (!System.Version.TryParse(api.Version, out Version stated))
                {
                    throw new CadPluginException(
                        name,
                        "The plugin "
                            + name
                            + " states the API version \""
                            + api.Version
                            + "\", which is not a version."
                    );
                }

                if (stated > required)
                {
                    required = stated;
                }
            }

            if (required > ApiVersion)
            {
                throw new CadPluginException(
                    name,
                    "The plugin "
                        + name
                        + " needs version "
                        + required
                        + " of the plugin API, and this presenter has "
                        + ApiVersion
                        + ": it needs a newer presenter."
                );
            }

            var plugin = new CadPlugin(
                name,
                version ?? (sorted.Count > 0 ? VersionOf(sorted[0]) : string.Empty),
                required
            );
            plugin.Read(sorted);

            if (plugin.Commands.Count == 0 && needsCommand)
            {
                string why = plugin.Problems.Count > 0 ? " " + string.Join(" ", plugin.Problems) : string.Empty;
                throw new CadPluginException(
                    name,
                    "The plugin "
                        + name
                        + " has no command: no public method marked [CadCommand] that takes a CadCommandContext and returns IEnumerable<CadPrompt>."
                        + why
                );
            }

            return plugin;
        }

        /// <summary>
        /// Adds the commands to a registry, and their aliases to its shortcuts. A command whose name is already a command or an
        /// alias of the registry is left out, and so is an alias already taken; each is said in the lines returned.
        /// </summary>
        /// <param name="registry">The registry, as a <see cref="CadCommandSession"/> of a drawing has it.</param>
        /// <returns>What was left out, a line each; empty when every command and alias was added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the plugin is unloaded, or already added to the registry.</exception>
        public IReadOnlyList<string> AddTo(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (!IsEnabled)
            {
                throw new InvalidOperationException("The plugin " + Name + " is unloaded.");
            }

            if (Find(registry) != null)
            {
                throw new InvalidOperationException("The plugin " + Name + " is already added to the registry.");
            }

            var refused = new List<string>();
            var registration = new Registration(registry);
            var aliases = new List<KeyValuePair<string, string>>();

            foreach (CadPluginCommand command in Commands)
            {
                CadCommand taken = registry.Find(command.Name);

                if (taken != null)
                {
                    refused.Add(
                        "The command "
                            + command.Name
                            + " of the plugin "
                            + Name
                            + " is not added: "
                            + command.Name
                            + " already runs "
                            + taken.Name
                            + "."
                    );
                    continue;
                }

                registry.Add(command.Command);
                registration.Commands.Add(command.Command);

                if (command.Alias == null)
                {
                    continue;
                }

                if (
                    registry.Shortcuts.Aliases.TryGetValue(command.Alias, out string owner)
                    || registry.Find(command.Alias) != null
                )
                {
                    refused.Add(
                        "The alias "
                            + command.Alias
                            + " of the command "
                            + command.Name
                            + " is not added: it is already the alias of "
                            + (owner ?? registry.Find(command.Alias).Name)
                            + "."
                    );
                    continue;
                }

                aliases.Add(new KeyValuePair<string, string>(command.Alias, command.Name));
                registration.Aliases.Add(command.Alias);
            }

            if (aliases.Count > 0)
            {
                aliases.AddRange(registry.Shortcuts.Aliases);
                registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
            }

            _registrations.Add(registration);
            return refused;
        }

        /// <summary>Takes the commands and aliases <see cref="AddTo"/> added out of a registry again; a command since replaced there is left.</summary>
        /// <param name="registry">The registry.</param>
        /// <returns>Whether the plugin was added to it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        public bool RemoveFrom(CadCommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            Registration registration = Find(registry);

            if (registration == null)
            {
                return false;
            }

            _registrations.Remove(registration);

            foreach (CadCommand command in registration.Commands)
            {
                if (ReferenceEquals(registry.Find(command.Name), command))
                {
                    registry.Remove(command.Name);
                }
            }

            if (registration.Aliases.Count > 0)
            {
                var aliases = new List<KeyValuePair<string, string>>();

                foreach (KeyValuePair<string, string> alias in registry.Shortcuts.Aliases)
                {
                    if (!registration.Aliases.Contains(alias.Key))
                    {
                        aliases.Add(alias);
                    }
                }

                registry.Shortcuts = new CadShortcuts(aliases, registry.Shortcuts.Keys);
            }

            return true;
        }

        /// <inheritdoc/>
        public override string ToString() => Name + " " + Version;

        /// <summary>Switches the plugin off: taken out of every registry, a run going on fails at its next step.</summary>
        internal void Disable()
        {
            while (_registrations.Count > 0)
            {
                RemoveFrom(_registrations[_registrations.Count - 1].Registry);
            }

            IsEnabled = false;
        }

        private Registration Find(CadCommandRegistry registry)
        {
            foreach (Registration registration in _registrations)
            {
                if (ReferenceEquals(registration.Registry, registry))
                {
                    return registration;
                }
            }

            return null;
        }

        private void Read(List<Assembly> assemblies)
        {
            var commands = new SortedDictionary<string, CadPluginCommand>(StringComparer.OrdinalIgnoreCase);
            var settings = new List<CadPluginSetting>();
            var problems = new List<string>();

            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in TypesOf(assembly, problems))
                {
                    ReadType(type, commands, settings, problems);
                }
            }

            Commands = new List<CadPluginCommand>(commands.Values);
            Settings = settings;
            Problems = problems;
        }

        private void ReadType(
            Type type,
            SortedDictionary<string, CadPluginCommand> commands,
            List<CadPluginSetting> settings,
            List<string> problems
        )
        {
            const BindingFlags all =
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly;
            var methods = new List<MethodInfo>();
            var properties = new List<PropertyInfo>();

            foreach (MethodInfo method in type.GetMethods(all))
            {
                if (method.IsDefined(typeof(CadCommandAttribute), false))
                {
                    methods.Add(method);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(all))
            {
                if (property.IsDefined(typeof(CadSettingAttribute), false))
                {
                    properties.Add(property);
                }
            }

            if (methods.Count == 0 && properties.Count == 0)
            {
                return;
            }

            // The order the class declares them in, which reflection does not promise to give.
            methods.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            properties.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            string where = type.FullName;

            if (!type.IsVisible || type.IsGenericTypeDefinition || type.IsInterface)
            {
                problems.Add(
                    "The class "
                        + where
                        + " is left out: a plugin's commands and settings are in a public class that is not generic."
                );
                return;
            }

            object instance = null;
            bool needsInstance =
                methods.Exists(m => !m.IsStatic)
                || properties.Exists(p => !(p.GetGetMethod(true) ?? p.GetSetMethod(true)).IsStatic);

            if (needsInstance)
            {
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Exception cause =
                        exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException
                            : exception;
                    problems.Add(
                        "The class "
                            + where
                            + " is left out: it could not be made with a public constructor that takes nothing ("
                            + cause.Message
                            + ")."
                    );
                    return;
                }
            }

            foreach (MethodInfo method in methods)
            {
                var attribute = (CadCommandAttribute)
                    Attribute.GetCustomAttribute(method, typeof(CadCommandAttribute), false);
                string at = where + "." + method.Name;
                string problem = Unusable(method, attribute);

                if (problem != null)
                {
                    problems.Add("The command " + attribute.Name + " (" + at + ") is left out: " + problem);
                    continue;
                }

                string name = attribute.Name.Trim().ToUpperInvariant();

                if (commands.ContainsKey(name))
                {
                    problems.Add(
                        "The command "
                            + name
                            + " ("
                            + at
                            + ") is left out: the plugin has a command of that name already."
                    );
                    continue;
                }

                string alias = string.IsNullOrWhiteSpace(attribute.Alias)
                    ? null
                    : attribute.Alias.Trim().ToUpperInvariant();

                if (alias != null && (alias.IndexOf(' ') >= 0 || string.Equals(alias, name, StringComparison.Ordinal)))
                {
                    problems.Add(
                        "The alias "
                            + alias
                            + " of the command "
                            + name
                            + " is left out: an alias is one word other than the name."
                    );
                    alias = null;
                }

                if (alias != null && (commands.ContainsKey(alias) || AliasTaken(commands, alias)))
                {
                    problems.Add(
                        "The alias " + alias + " of the command " + name + " is left out: the plugin uses it already."
                    );
                    alias = null;
                }

                var body =
                    (Func<CadCommandContext, IEnumerable<CadPrompt>>)(
                        method.IsStatic
                            ? Delegate.CreateDelegate(typeof(Func<CadCommandContext, IEnumerable<CadPrompt>>), method)
                            : Delegate.CreateDelegate(
                                typeof(Func<CadCommandContext, IEnumerable<CadPrompt>>),
                                instance,
                                method
                            )
                    );
                commands.Add(
                    name,
                    new CadPluginCommand(this, name, alias, attribute.Description, attribute.UsesSelection, body)
                );
            }

            foreach (PropertyInfo property in properties)
            {
                var attribute = (CadSettingAttribute)
                    Attribute.GetCustomAttribute(property, typeof(CadSettingAttribute), false);
                CadSettingKind? kind = CadPluginSetting.KindOf(property.PropertyType);
                MethodInfo get = property.GetGetMethod();
                MethodInfo set = property.GetSetMethod();

                if (kind == null || get == null || set == null || property.GetIndexParameters().Length > 0)
                {
                    problems.Add(
                        "The setting "
                            + attribute.Label
                            + " ("
                            + where
                            + "."
                            + property.Name
                            + ") is left out: a setting is a property with a public get and set, of type double, int, bool, string or an enumeration."
                    );
                    continue;
                }

                try
                {
                    settings.Add(new CadPluginSetting(property, get.IsStatic ? null : instance, attribute, kind.Value));
                }
                catch (Exception exception)
                {
                    Exception cause =
                        exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException
                            : exception;
                    problems.Add(
                        "The setting "
                            + attribute.Label
                            + " ("
                            + where
                            + "."
                            + property.Name
                            + ") is left out: its value could not be read ("
                            + cause.Message
                            + ")."
                    );
                }
            }
        }

        /// <summary>Why a method marked as a command cannot be one, or null where it can.</summary>
        private static string Unusable(MethodInfo method, CadCommandAttribute attribute)
        {
            string name = attribute.Name.Trim();

            if (name.Length == 0 || name.IndexOf(' ') >= 0)
            {
                return "a command's name is one word.";
            }

            if (!method.IsPublic)
            {
                return "it is not public.";
            }

            ParameterInfo[] parameters = method.GetParameters();
            bool takesContext = parameters.Length == 1 && parameters[0].ParameterType == typeof(CadCommandContext);

            if (method.ContainsGenericParameters)
            {
                return "it is generic.";
            }

            if (takesContext && method.ReturnType == typeof(void))
            {
                return "it returns void; write it as an iterator, returning IEnumerable<CadPrompt>, that yields each prompt and reads the answer from the context's Input, as the presenter's own commands do.";
            }

            if (!takesContext || method.ReturnType != typeof(IEnumerable<CadPrompt>))
            {
                return "a command takes one CadCommandContext and returns IEnumerable<CadPrompt>.";
            }

            return null;
        }

        private static bool AliasTaken(SortedDictionary<string, CadPluginCommand> commands, string alias)
        {
            foreach (CadPluginCommand command in commands.Values)
            {
                if (string.Equals(command.Alias, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The public types of an assembly, in ordinal order of their full names; those that cannot be read said in the problems.</summary>
        private static List<Type> TypesOf(Assembly assembly, List<string> problems)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
                Exception first =
                    exception.LoaderExceptions != null && exception.LoaderExceptions.Length > 0
                        ? exception.LoaderExceptions[0]
                        : exception;
                problems.Add("Some classes of " + assembly.GetName().Name + " could not be read: " + first?.Message);
            }

            var found = new List<Type>();

            foreach (Type type in types)
            {
                if (type != null && type.IsClass)
                {
                    found.Add(type);
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return found;
        }

        private static string VersionOf(Assembly assembly)
        {
            var informational = (AssemblyInformationalVersionAttribute)
                Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute));

            if (informational != null && !string.IsNullOrEmpty(informational.InformationalVersion))
            {
                // The build's source revision, which the SDK adds after a '+', says nothing to a user.
                string text = informational.InformationalVersion;
                int plus = text.IndexOf('+');
                return plus > 0 ? text.Substring(0, plus) : text;
            }

            return assembly.GetName().Version?.ToString() ?? string.Empty;
        }

        /// <summary>Runs one command of the plugin: one undo step, a failure taken back with the plugin's name.</summary>
        internal IEnumerable<CadPrompt> Run(CadPluginCommand command, CadCommandContext context)
        {
            // Opened first, so that an edit made through the document's own methods joins the step and a failure takes it back.
            context.OpenUndoStep();
            IEnumerable<CadPrompt> prompts;

            try
            {
                prompts = command.Body(context);
            }
            catch (Exception exception)
            {
                throw Failed(command, exception);
            }

            if (prompts == null)
            {
                yield break;
            }

            using (IEnumerator<CadPrompt> body = prompts.GetEnumerator())
            {
                while (true)
                {
                    if (!IsEnabled)
                    {
                        throw new CadPluginException(
                            Name,
                            "The plugin " + Name + " was unloaded while " + command.Name + " ran."
                        );
                    }

                    bool more;

                    try
                    {
                        more = body.MoveNext();
                    }
                    catch (Exception exception)
                    {
                        throw Failed(command, exception);
                    }

                    if (!more)
                    {
                        yield break;
                    }

                    yield return body.Current;
                }
            }
        }

        private CadPluginException Failed(CadPluginCommand command, Exception exception) =>
            new CadPluginException(
                Name,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of the plugin {1} failed: {2}",
                    command.Name,
                    Name,
                    exception.Message
                ),
                exception
            );

        private sealed class Registration
        {
            internal Registration(CadCommandRegistry registry)
            {
                Registry = registry;
            }

            internal CadCommandRegistry Registry { get; }

            internal List<CadCommand> Commands { get; } = new List<CadCommand>();

            internal HashSet<string> Aliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>One command of a plugin: its name, alias and description as its attribute gives them, and the command the registry runs.</summary>
    public sealed class CadPluginCommand
    {
        internal CadPluginCommand(
            CadPlugin plugin,
            string name,
            string alias,
            string description,
            bool usesSelection,
            Func<CadCommandContext, IEnumerable<CadPrompt>> body
        )
        {
            Plugin = plugin;
            Name = name;
            Alias = alias;
            Description = description;
            Body = body;
            Command = new CadCommand(name, c => plugin.Run(this, c), usesSelection);
        }

        /// <summary>Gets the plugin.</summary>
        public CadPlugin Plugin { get; }

        /// <summary>Gets the name, in capitals: NUMCIRCLES.</summary>
        public string Name { get; }

        /// <summary>Gets the alias, in capitals, as NC; null for none.</summary>
        public string Alias { get; }

        /// <summary>Gets what the command does; null where the plugin does not say.</summary>
        public string Description { get; }

        /// <summary>Gets the command a registry runs: the plugin's method, one undo step a run.</summary>
        public CadCommand Command { get; }

        internal Func<CadCommandContext, IEnumerable<CadPrompt>> Body { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A plugin refused, or one of its commands failed: the message names the plugin, and the exception it threw is the inner one.</summary>
    public sealed class CadPluginException : Exception
    {
        /// <summary>Makes the exception.</summary>
        /// <param name="pluginName">The plugin's name.</param>
        /// <param name="message">What happened, the plugin named.</param>
        /// <param name="innerException">The exception the plugin threw; null for none.</param>
        public CadPluginException(string pluginName, string message, Exception innerException = null)
            : base(message, innerException)
        {
            PluginName = pluginName;
        }

        /// <summary>Gets the plugin's name.</summary>
        public string PluginName { get; }
    }
}
