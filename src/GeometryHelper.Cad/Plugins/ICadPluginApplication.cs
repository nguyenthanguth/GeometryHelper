namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// The entry point of a plugin that adds to the window, as AutoCAD's <c>IExtensionApplication</c> is: a public class with
    /// a public constructor that takes nothing, which the presenter makes once when the plugin is loaded and
    /// <see cref="Initialize"/>s, so that the plugin adds its ribbon tabs and panels, Properties rows, DRAWINGS buttons and
    /// menu items, canvas menu items and object snaps to the <see cref="CadPluginHost"/>. <see cref="Terminate"/> runs when
    /// the plugin is unloaded or reloaded, or the presenter closes.
    /// </summary>
    /// <remarks>
    /// Every public class of the plugin's assemblies that implements it is made, in ordinal order of its full name, and
    /// terminated in the reverse order. A class that also holds <c>[CadCommand]</c> methods or <c>[CadSetting]</c>
    /// properties is made once, and the commands and the window additions share the one object. A plugin with an application
    /// class and no command is loaded: it adds to the window and nothing to the command line. A plugin that uses these
    /// additions states <c>[assembly: CadPluginApi("1.1")]</c>, so that an older presenter refuses it with its own words.
    /// </remarks>
    public interface ICadPluginApplication
    {
        /// <summary>
        /// Runs once, when the plugin is loaded, before any of its commands: the place to call the host's <c>Add</c> methods.
        /// What it adds is read when it returns. An exception it throws keeps the plugin's commands and drops everything it
        /// added, with a line in <see cref="CadPlugin.Problems"/>.
        /// </summary>
        /// <param name="host">What the plugin adds to; it takes additions only until this method returns.</param>
        void Initialize(CadPluginHost host);

        /// <summary>
        /// Runs once when the plugin is unloaded or reloaded, or the presenter closes, before its commands go: the place to
        /// let go of what the plugin holds. An exception it throws is logged and the unload goes on.
        /// </summary>
        void Terminate();
    }
}
