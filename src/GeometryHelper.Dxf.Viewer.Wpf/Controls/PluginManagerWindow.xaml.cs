using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Plugin manager, as the design draws it: 900 by 700, the actions on top, the plugins of this session, the one
    /// selected with its trusted folders, and the load log. Its logic is <see cref="PluginManagerViewModel"/>, disposed when
    /// the dialog closes; the file dialogs it asks for are shown here.
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public PluginManagerWindow(PluginManagerViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.LoadRequested += (s, e) => Pick("Load a plugin", "Plugins (*.dll;*.ghplugin)|*.dll;*.ghplugin", path => Model.Load(path));
            model.InstallRequested += (s, e) => Pick("Install a plugin package", "Plugin packages (*.ghplugin)|*.ghplugin", path => Model.Install(path));
            model.AddFolderRequested += (s, e) => PickFolder();
            model.SettingsRequested += (s, e) => ShowSettings();
            model.LogLines.CollectionChanged += (s, e) => LogScroll.ScrollToEnd();
            Closed += (s, e) => Model.Dispose();
        }

        /// <summary>Gets the view model.</summary>
        public PluginManagerViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the Open dialog for a plugin's .dll and loads the one chosen, as PLUGINLOAD's window does.</summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="load">What is done with the path chosen.</param>
        public static void PickPlugin(Window owner, Action<string> load) => Pick(owner, "Load a plugin", "Plugins (*.dll;*.ghplugin)|*.dll;*.ghplugin", load);

        /// <summary>Shows the Open dialog for a .ghplugin and installs the one chosen.</summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="install">What is done with the path chosen.</param>
        public static void PickPackage(Window owner, Action<string> install) => Pick(owner, "Install a plugin package", "Plugin packages (*.ghplugin)|*.ghplugin", install);

        private static void Pick(Window owner, string title, string filter, Action<string> then)
        {
            var dialog = new OpenFileDialog { Title = title, Filter = filter + "|All files (*.*)|*.*" };

            if (dialog.ShowDialog(owner) == true)
            {
                then(dialog.FileName);
            }
        }

        private void Pick(string title, string filter, Action<string> then) => Pick(this, title, filter, then);

        /// <summary>Add folder: a Save dialog chooses the folder, as .NET Framework's WPF has no folder dialog.</summary>
        private void PickFolder()
        {
            var dialog = new SaveFileDialog { Title = "Choose a folder to trust", FileName = "Trust this folder", Filter = "Folders|*.folder", OverwritePrompt = false };

            if (dialog.ShowDialog(this) == true)
            {
                Model.AddFolder(Path.GetDirectoryName(dialog.FileName));
            }
        }

        private void ShowSettings()
        {
            PluginSettingsViewModel settings = Model.CreateSettings();

            if (settings != null)
            {
                new PluginSettingsWindow(settings) { Owner = this }.ShowDialog();
            }
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
