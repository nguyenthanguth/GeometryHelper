using System;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>
    /// Marks a method of a plugin as a command, as AutoCAD's <c>[CommandMethod]</c> does: <c>[CadCommand("NUMCIRCLES",
    /// Alias = "NC")]</c> on <c>public IEnumerable&lt;CadPrompt&gt; NumberCircles(CadCommandContext c)</c>. The method is an
    /// iterator, as the presenter's own commands are: it yields each prompt and reads the answer from
    /// <see cref="CadCommandContext.Input"/> when it is resumed, so that it runs inside the command line with no thread of
    /// its own. It may be static, or an instance method of a public class with a public constructor that takes nothing, of
    /// which the plugin makes one when it is loaded.
    /// </summary>
    /// <remarks>
    /// A method that returns <c>void</c> and blocks on its prompts, as AutoCAD's do, is refused: it would have to run on a
    /// thread of its own while the window waits, and every edit it made would then reach the canvas and the panels from
    /// that thread, which WPF does not allow.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CadCommandAttribute : Attribute
    {
        /// <summary>Marks a method as the command of a name.</summary>
        /// <param name="name">The name typed to run it, one word: NUMCIRCLES. It is kept in capitals.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadCommandAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Gets the name typed to run the command.</summary>
        public string Name { get; }

        /// <summary>Gets or sets the alias that runs it too, as NC for NUMCIRCLES; null for none.</summary>
        public string Alias { get; set; }

        /// <summary>Gets or sets what the command does, in a few words, as the Plugin manager and the ribbon show it.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets whether the command takes the objects selected before it was started, as MOVE does, so that
        /// <see cref="CadCommandContext.SelectObjects"/> gives them without asking "Select objects:". False until set.
        /// </summary>
        public bool UsesSelection { get; set; }
    }

    /// <summary>
    /// Says which version of the plugin API an assembly needs at least, as <c>[assembly: CadPluginApi("1.0")]</c>. The
    /// presenter refuses to load it, saying why, when its own <see cref="CadPlugin.ApiVersion"/> is older. An assembly
    /// without one is taken as needing 1.0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class CadPluginApiAttribute : Attribute
    {
        /// <summary>Says the least version of the plugin API the assembly needs.</summary>
        /// <param name="version">The version, as "1.0".</param>
        /// <exception cref="ArgumentNullException">Thrown when the version is null.</exception>
        public CadPluginApiAttribute(string version)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        /// <summary>Gets the least version of the plugin API the assembly needs, as written.</summary>
        public string Version { get; }
    }

    /// <summary>
    /// Marks a public property of a plugin's class as a setting the presenter shows and the user changes, as
    /// <c>[CadSetting("Text height", Minimum = 0.1)] public double TextHeight { get; set; } = 2.5;</c>. The property is read
    /// for its default once the plugin is loaded, and its value is set through <see cref="CadPluginSetting.Value"/>; its type
    /// is <c>double</c>, <c>int</c>, <c>bool</c>, <c>string</c> or an enumeration, and the presenter draws a box for it of
    /// its own (D17 of the plan), so a plugin never touches the window.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class CadSettingAttribute : Attribute
    {
        /// <summary>Marks a property as a setting with the label the presenter shows.</summary>
        /// <param name="label">The label: "Text height".</param>
        /// <exception cref="ArgumentNullException">Thrown when the label is null.</exception>
        public CadSettingAttribute(string label)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>Gets the label the presenter shows.</summary>
        public string Label { get; }

        /// <summary>Gets or sets what the setting does, as the presenter shows it under the label; null for nothing.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the least value of a number, inclusive; minus infinity, for none, until set.</summary>
        public double Minimum { get; set; } = double.NegativeInfinity;

        /// <summary>Gets or sets the greatest value of a number, inclusive; infinity, for none, until set.</summary>
        public double Maximum { get; set; } = double.PositiveInfinity;
    }
}
