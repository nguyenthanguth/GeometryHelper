using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Plugins
{
    /// <summary>
    /// A drawing plugin (D27 of the plan), shaped as a Tekla Structures drawing plugin is: what the user picks
    /// (<see cref="DefineInput"/>), then the run (<see cref="Run"/>), which adds dimensions to a view of a model drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Drawing.RunPlugin"/> runs a plugin in a view and keeps the run in the drawing: the plugin's name and
    /// version, the view, the objects it was given and its settings. Every update of the drawing from the model runs it again
    /// on the objects as they are then, its old dimensions taken out first, so that they follow the model; a hand edit made
    /// to one of them is kept through the update as any hand edit is. A plugin that dimensions the bolts of TC5 in its front
    /// view gives the pitch 300 between them; with the bolts moved 20 further apart and the drawing updated, it gives 340.
    /// </para>
    /// <para>
    /// A plugin is a public class with a public constructor that takes nothing, in a .dll built for netstandard2.0 that
    /// references GeometryHelper.Drawing; <see cref="DrawingPluginCatalog"/> finds it in a .dll or .ghplugin the
    /// presenter's plugin manager loads. It runs while the drawing is made again, maybe on a thread of its own, so it reads
    /// only what its <see cref="DrawingPluginContext"/> gives and keeps nothing between runs.
    /// </para>
    /// </remarks>
    public abstract class DrawingPlugin
    {
        /// <summary>Gets the version of the drawing plugin API this library has: 1.0.</summary>
        public static Version ApiVersion { get; } = new Version(1, 0);

        /// <summary>Gets the plugin's name, which a drawing finds it by when it runs it again: "Bolt dimensions".</summary>
        public abstract string Name { get; }

        /// <summary>Gets the plugin's version, kept with each run: by default its assembly's, "1.0.0.0".</summary>
        public virtual string Version => GetType().Assembly.GetName().Version?.ToString() ?? string.Empty;

        /// <summary>Gets the least version of the drawing plugin API the plugin needs; 1.0 unless it says otherwise.</summary>
        public virtual Version RequiredApiVersion => new Version(1, 0);

        /// <summary>Says what the plugin needs, as Tekla's DefineInput does: a view, objects of some kinds, settings.</summary>
        /// <param name="input">The input to add to.</param>
        public virtual void DefineInput(DrawingPluginInput input)
        {
        }

        /// <summary>Runs the plugin on a view: reads the context, and adds what it makes through it.</summary>
        /// <param name="context">The view, the objects, the settings, and the way to add dimensions.</param>
        public abstract void Run(DrawingPluginContext context);

        /// <inheritdoc/>
        public override string ToString() => Name + " " + Version;
    }

    /// <summary>
    /// What a drawing plugin needs from the user, as it says in <see cref="DrawingPlugin.DefineInput"/>: a view, objects of
    /// some kinds, and settings, each number with its default and range.
    /// </summary>
    /// <remarks>
    /// A run given no objects takes every object of its view of the kinds asked for, every object where none are named: a
    /// plugin that asks for <see cref="DrawingObjectKind.Bolt"/> and is run with no picks is given the four bolts of TC5.
    /// </remarks>
    public sealed class DrawingPluginInput
    {
        private readonly List<DrawingObjectKind> _kinds = new List<DrawingObjectKind>();
        private readonly List<DrawingPluginSetting> _settings = new List<DrawingPluginSetting>();

        /// <summary>
        /// Makes an empty input, for the app to ask a plugin what it needs before it runs it: given to
        /// <see cref="DrawingPlugin.DefineInput"/>, the sample Bolt dimensions fills it with a view, the kind Bolt and the
        /// setting Rounding, 1 by default, from 0.01 to 100.
        /// </summary>
        public DrawingPluginInput()
        {
        }

        /// <summary>Gets what the user is asked when picking the view: "Pick a view"; null where the plugin did not ask.</summary>
        public string ViewPrompt { get; private set; }

        /// <summary>Gets what the user is asked when picking objects: "Pick bolt groups"; null where the plugin did not ask.</summary>
        public string ObjectsPrompt { get; private set; }

        /// <summary>Gets the kinds of the objects asked for, in the order given; empty for any kind.</summary>
        public IReadOnlyList<DrawingObjectKind> ObjectKinds => _kinds;

        /// <summary>Gets the settings, in the order added.</summary>
        public IReadOnlyList<DrawingPluginSetting> Settings => _settings;

        /// <summary>Asks for the view the plugin runs in.</summary>
        /// <param name="prompt">What the user is asked.</param>
        /// <exception cref="ArgumentNullException">Thrown when the prompt is null.</exception>
        public void AddView(string prompt) => ViewPrompt = prompt ?? throw new ArgumentNullException(nameof(prompt));

        /// <summary>Asks for objects of the view, of the kinds given, any kind where none is given.</summary>
        /// <param name="prompt">What the user is asked.</param>
        /// <param name="kinds">The kinds.</param>
        /// <exception cref="ArgumentNullException">Thrown when the prompt or the kinds are null.</exception>
        public void AddObjects(string prompt, params DrawingObjectKind[] kinds)
        {
            ObjectsPrompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            _kinds.Clear();
            _kinds.AddRange(kinds ?? throw new ArgumentNullException(nameof(kinds)));
        }

        /// <summary>Declares a number the plugin reads from <see cref="DrawingPluginContext.Setting"/>, kept with each run.</summary>
        /// <param name="name">The name: "Offset".</param>
        /// <param name="defaultValue">The value where the run gives none.</param>
        /// <param name="minimum">The least value allowed.</param>
        /// <param name="maximum">The greatest value allowed.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or declared already, or the default is outside the range.</exception>
        public void AddSetting(string name, double defaultValue, double minimum = double.NegativeInfinity, double maximum = double.PositiveInfinity)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0 || _settings.Exists(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
            {
                throw new ArgumentException("A setting needs a name of its own; \"" + name + "\" is empty or declared already.", nameof(name));
            }

            if (!(defaultValue >= minimum && defaultValue <= maximum))
            {
                throw new ArgumentException("The default " + defaultValue + " of " + name + " is outside its range.", nameof(defaultValue));
            }

            _settings.Add(new DrawingPluginSetting(name, defaultValue, minimum, maximum));
        }
    }

    /// <summary>A number a drawing plugin declares as a setting: its name, its default and its range.</summary>
    public sealed class DrawingPluginSetting
    {
        internal DrawingPluginSetting(string name, double defaultValue, double minimum, double maximum)
        {
            Name = name;
            Default = defaultValue;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the value where a run gives none.</summary>
        public double Default { get; }

        /// <summary>Gets the least value allowed.</summary>
        public double Minimum { get; }

        /// <summary>Gets the greatest value allowed.</summary>
        public double Maximum { get; }

        /// <inheritdoc/>
        public override string ToString() => Name + " = " + Default.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
