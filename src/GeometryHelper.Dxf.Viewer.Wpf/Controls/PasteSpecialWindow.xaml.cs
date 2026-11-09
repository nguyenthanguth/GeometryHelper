using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Paste Special dialog, which PASTESPEC shows, as the design draws it: the source, Paste (Paste Link greyed),
    /// the ways to paste, Display As Icon greyed and the result of the way chosen. Its logic is <see cref="PasteSpecialViewModel"/>.
    /// </summary>
    public partial class PasteSpecialWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public PasteSpecialWindow(PasteSpecialViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public PasteSpecialViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and gives the paste chosen.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>PASTECLIP, PASTEBLOCK, PASTEORIG or TEXT; null when cancelled.</returns>
        public static string Show(Window owner, PasteSpecialViewModel model)
            => new PasteSpecialWindow(model) { Owner = owner }.ShowDialog() == true ? model.Answer : null;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
