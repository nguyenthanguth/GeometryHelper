using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Startup Suite of Load/Unload Applications, as the design draws it: 460 by 360, the AutoLISP files loaded into every
    /// drawing opened, with Add… and Remove; its logic is <see cref="StartupSuiteViewModel"/>.
    /// </summary>
    public partial class StartupSuiteWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public StartupSuiteWindow(StartupSuiteViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public StartupSuiteViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window, which waits until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when the dialog was closed with its Close button.</returns>
        public static bool Show(Window owner, StartupSuiteViewModel model)
        {
            model.AskFiles = () => AskFiles(owner);
            return new StartupSuiteWindow(model) { Owner = owner }.ShowDialog() == true;
        }

        /// <summary>The Open dialog of Add…: AutoLISP files (*.lsp), several at once; null where none was chosen.</summary>
        private static string[] AskFiles(Window owner)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add to the Startup Suite",
                CheckFileExists = true,
                Multiselect = true,
                Filter = "AutoLISP files (*.lsp)|*.lsp",
            };
            return dialog.ShowDialog(owner) == true ? dialog.FileNames : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAdd(object sender, RoutedEventArgs e) => Model.Add();

        private void OnRemove(object sender, RoutedEventArgs e) => Model.Remove();

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
