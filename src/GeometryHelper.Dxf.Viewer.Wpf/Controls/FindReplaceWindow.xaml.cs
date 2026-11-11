using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Find and Replace dialog, which FIND shows: the text, where to look, the results with Zoom and Select, the
    /// Search Options and the Text Types. Its logic is <see cref="FindReplaceViewModel"/>.
    /// </summary>
    public partial class FindReplaceWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public FindReplaceWindow(FindReplaceViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) => FindBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public FindReplaceViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, FindReplaceViewModel model) =>
            new FindReplaceWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnFindNext(object sender, RoutedEventArgs e) => Model.FindNext();

        private void OnReplace(object sender, RoutedEventArgs e) => Model.Replace();

        private void OnReplaceAll(object sender, RoutedEventArgs e) => Model.ReplaceAll();

        private void OnZoom(object sender, RoutedEventArgs e) => Model.Zoom();

        private void OnSelect(object sender, RoutedEventArgs e)
        {
            Model.Select();
            Close();
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
