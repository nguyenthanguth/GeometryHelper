using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The warning of SECURELOAD 1, as AutoCAD 2021's task dialog "Security - Unsigned Executable File": the file of code that
    /// is about to load is outside the trusted folders, its name, folder and date are shown, and the user answers Always Load,
    /// Load Once or Do Not Load. Do Not Load is the button Enter takes, and the answer to Esc, to the cross and to Alt+F4.
    /// </summary>
    public partial class LoadSecurityWindow : Window
    {
        /// <summary>Makes the warning.</summary>
        /// <param name="model">The warning's state: the file and the answer.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public LoadSecurityWindow(LoadSecurityViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.Answered += (s, e) => DialogResult = model.Answer != CadLoadAnswer.DoNotLoad;
            Loaded += (s, e) => NotButton.Focus();
        }

        /// <summary>Gets the warning's state.</summary>
        public LoadSecurityViewModel Model { get; }

        /// <summary>Gets the warning's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the warning for a file over a window and waits for the answer. Anything but a press of Always Load or Load Once
        /// is Do Not Load.
        /// </summary>
        /// <param name="owner">The window it is shown over, which it is centred on; null for the middle of the screen.</param>
        /// <param name="path">The full path of the file about to load.</param>
        /// <returns>The answer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public static CadLoadAnswer Ask(Window owner, string path)
        {
            var warning = new LoadSecurityWindow(new LoadSecurityViewModel(path));

            if (owner != null && owner.IsVisible)
            {
                warning.Owner = owner;
            }
            else
            {
                warning.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            warning.ShowDialog();
            return warning.Model.Answer;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAlwaysLoad(object sender, RoutedEventArgs e) => Model.AlwaysLoadCommand.Execute(null);

        private void OnLoadOnce(object sender, RoutedEventArgs e) => Model.LoadOnceCommand.Execute(null);

        private void OnDoNotLoad(object sender, RoutedEventArgs e) => Model.DoNotLoadCommand.Execute(null);

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.FootnoteUrl);
    }
}
