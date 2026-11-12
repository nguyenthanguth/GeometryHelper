using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's QuickCalc palette, which QUICKCALC shows: one at a time, open beside the drawing while commands run. Its logic
    /// is <see cref="QuickCalcViewModel"/>.
    /// </summary>
    public partial class QuickCalcWindow : Window
    {
        private static QuickCalcWindow _open;

        /// <summary>Makes the palette over its view model.</summary>
        /// <param name="model">The view model.</param>
        public QuickCalcWindow(QuickCalcViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public QuickCalcViewModel Model { get; }

        /// <summary>Gets the palette's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the palette over a window, or brings the one open to the front.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void ShowPalette(Window owner, QuickCalcViewModel model)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            var window = new QuickCalcWindow(model) { Owner = owner };
            _open = window;
            window.Closed += (s, e) => _open = null;
            window.Show();
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnInputKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Model.Evaluate();
                InputBox.CaretIndex = InputBox.Text.Length;
                e.Handled = true;
            }
        }

        private void OnKey(object sender, RoutedEventArgs e)
        {
            Model.Press((string)((Button)sender).Content);
            InputBox.CaretIndex = InputBox.Text.Length;
        }

        private void OnEquals(object sender, RoutedEventArgs e) => Model.Evaluate();

        private void OnRecall(object sender, MouseButtonEventArgs e) =>
            Model.Recall(((ListBox)sender).SelectedItem as QuickCalcLine);

        private void OnClear(object sender, RoutedEventArgs e) => Model.Clear();

        private void OnClearHistory(object sender, RoutedEventArgs e) => Model.ClearHistory();

        private void OnPaste(object sender, RoutedEventArgs e) => Model.PasteToCommandLine();

        private void OnCoordinates(object sender, RoutedEventArgs e) => Model.GetCoordinates();

        private void OnDistance(object sender, RoutedEventArgs e) => Model.Distance();

        private void OnAngle(object sender, RoutedEventArgs e) => Model.Angle();

        private void OnIntersection(object sender, RoutedEventArgs e) => Model.Intersection();

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
