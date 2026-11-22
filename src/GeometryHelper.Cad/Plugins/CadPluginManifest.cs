using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// What a plugin says of itself in its <c>plugin.json</c>, read without loading its code (D18 of the plan):
    /// <code>
    /// {
    ///   "id": "number-circles",
    ///   "name": "Number circles",
    ///   "version": "1.0.0",
    ///   "author": "Nguyen Thang",
    ///   "kind": "command",
    ///   "minimumApiVersion": "1.0",
    ///   "commands": [ "NUMCIRCLES" ],
    ///   "icon": "icon.png"
    /// }
    /// </code>
    /// Only the name is needed; the id is the name where not given, the kind "command", the API version 1.0.
    /// </summary>
    public sealed class CadPluginManifest
    {
        /// <summary>The name of the manifest's file: plugin.json.</summary>
        public const string FileName = "plugin.json";

        /// <summary>The kind of a command plugin, the only kind this presenter loads: "command".</summary>
        public const string CommandKind = "command";

        private CadPluginManifest(Data data)
        {
            Name = data.Name.Trim();
            Id = string.IsNullOrWhiteSpace(data.Id) ? Name : data.Id.Trim();
            Version = data.Version?.Trim() ?? string.Empty;
            Author = data.Author?.Trim() ?? string.Empty;
            Kind = string.IsNullOrWhiteSpace(data.Kind) ? CommandKind : data.Kind.Trim().ToLowerInvariant();
            MinimumApiVersion = string.IsNullOrWhiteSpace(data.MinimumApiVersion)
                ? new Version(1, 0)
                : System.Version.Parse(data.MinimumApiVersion.Trim());
            var commands = new List<string>();

            foreach (string command in data.Commands ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(command))
                {
                    commands.Add(command.Trim().ToUpperInvariant());
                }
            }

            Commands = commands;
            Icon = string.IsNullOrWhiteSpace(data.Icon) ? null : data.Icon.Trim();
        }

        /// <summary>Gets the id, a short name for the plugin; the name where not given.</summary>
        public string Id { get; }

        /// <summary>Gets the name, which the Plugin manager shows and PLUGINUNLOAD and PLUGINRELOAD are given.</summary>
        public string Name { get; }

        /// <summary>Gets the plugin's version, as written; empty where not given.</summary>
        public string Version { get; }

        /// <summary>Gets the author; empty where not given.</summary>
        public string Author { get; }

        /// <summary>Gets the kind, in small letters: "command" for a command plugin.</summary>
        public string Kind { get; }

        /// <summary>Gets the least version of the plugin API the plugin needs; 1.0 where not given.</summary>
        public Version MinimumApiVersion { get; }

        /// <summary>Gets the names of the commands, in capitals, in the order written.</summary>
        public IReadOnlyList<string> Commands { get; }

        /// <summary>Gets the path of the icon, relative to the manifest; null where not given.</summary>
        public string Icon { get; }

        /// <summary>
        /// Says why the presenter does not load the plugin, or null where it may: a kind other than "command", or a newer
        /// version of the plugin API than <see cref="CadPlugin.ApiVersion"/>.
        /// </summary>
        /// <returns>The reason, or null.</returns>
        public string Refusal()
        {
            if (!string.Equals(Kind, CommandKind, StringComparison.Ordinal))
            {
                return "The plugin "
                    + Name
                    + " is of the kind \""
                    + Kind
                    + "\", and this presenter loads command plugins only.";
            }

            if (MinimumApiVersion > CadPlugin.ApiVersion)
            {
                return "The plugin "
                    + Name
                    + " needs version "
                    + MinimumApiVersion
                    + " of the plugin API, and this presenter has "
                    + CadPlugin.ApiVersion
                    + ": it needs a newer presenter.";
            }

            return null;
        }

        /// <summary>Reads a manifest from its JSON text.</summary>
        /// <param name="json">The text.</param>
        /// <returns>The manifest.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not JSON, has no name, or has a minimum API version that is not a version.</exception>
        public static CadPluginManifest Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            Data data;

            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    data = (Data)new DataContractJsonSerializer(typeof(Data)).ReadObject(stream);
                }
            }
            catch (Exception exception)
                when (exception is SerializationException || exception is System.Xml.XmlException)
            {
                throw new FormatException(
                    "The plugin's manifest is not JSON of the form plugin.json has: " + exception.Message,
                    exception
                );
            }

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                throw new FormatException("The plugin's manifest gives no name.");
            }

            if (
                !string.IsNullOrWhiteSpace(data.MinimumApiVersion)
                && !System.Version.TryParse(data.MinimumApiVersion.Trim(), out _)
            )
            {
                throw new FormatException(
                    "The plugin's manifest gives the API version \""
                        + data.MinimumApiVersion
                        + "\", which is not a version."
                );
            }

            return new CadPluginManifest(data);
        }

        /// <summary>Reads a manifest from a plugin.json file, without loading any code.</summary>
        /// <param name="path">The path of the plugin.json.</param>
        /// <returns>The manifest.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown when there is no such file.</exception>
        /// <exception cref="FormatException">Thrown when the manifest cannot be read (see <see cref="Parse"/>).</exception>
        public static CadPluginManifest Read(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <inheritdoc/>
        public override string ToString() => Name + " " + Version;

        [DataContract]
        private sealed class Data
        {
            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "version")]
            public string Version { get; set; }

            [DataMember(Name = "author")]
            public string Author { get; set; }

            [DataMember(Name = "kind")]
            public string Kind { get; set; }

            [DataMember(Name = "minimumApiVersion")]
            public string MinimumApiVersion { get; set; }

            [DataMember(Name = "commands")]
            public List<string> Commands { get; set; }

            [DataMember(Name = "icon")]
            public string Icon { get; set; }
        }
    }
}
