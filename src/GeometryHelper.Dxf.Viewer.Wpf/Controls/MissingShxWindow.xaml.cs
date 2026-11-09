using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Missing SHX files dialog, shown once as a drawing opens whose text styles name SHX fonts not found: the files,
    /// the styles that use them and what they are drawn in, a replacement for each or the FONTALT replacement kept, and Do not
    /// show again. Its logic is <see cref="MissingShxViewModel"/>.
    /// </summary>
    public partial class MissingShxWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model; OK applies nothing itself, the caller calls <see cref="MissingShxViewModel.Apply"/>.</param>
        public MissingShxWindow(MissingShxViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.BrowseFont = PickFont;
        }

        /// <summary>Gets the view model.</summary>
        public MissingShxViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, MissingShxViewModel model) => new MissingShxWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        /// <summary>Browse… of a replacement list: an SHX or TrueType file; null where none was chosen.</summary>
        private string PickFont()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Font files (*.shx;*.ttf;*.ttc;*.otf)|*.shx;*.ttf;*.ttc;*.otf|All files (*.*)|*.*", Title = "Select Font File" };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        /// <summary>A click on the words Do not show again ticks its box, as on a label.</summary>
        private void OnDoNotShowAgainText(object sender, MouseButtonEventArgs e) => Model.DoNotShowAgain = !Model.DoNotShowAgain;

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
