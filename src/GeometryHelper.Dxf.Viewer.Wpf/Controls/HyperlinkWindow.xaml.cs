using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Insert Hyperlink and Edit Hyperlink dialog, which HYPERLINK shows: Link to (Existing File or Web Page, View of
    /// This Drawing, E-mail Address), Text to display and, for a link being edited, Remove Link. Its logic is
    /// <see cref="HyperlinkViewModel"/>.
    /// </summary>
    public partial class HyperlinkWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public HyperlinkWindow(HyperlinkViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public HyperlinkViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window, with Browse… asking a file in the standard open-file dialog; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, HyperlinkViewModel model)
        {
            model.BrowseFile = () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Browse - Select Hyperlink" };
                return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
            };
            new HyperlinkWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e) => Model.Browse();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Accept())
            {
                DialogResult = true;
            }
        }

        private void OnRemove(object sender, RoutedEventArgs e)
        {
            Model.RemoveLink();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}
