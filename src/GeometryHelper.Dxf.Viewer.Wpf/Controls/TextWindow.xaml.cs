using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's text window, which TEXTSCR and F2 show: the history of the command line in a list that follows it, open beside
    /// the drawing while commands run; F2 or Esc inside it closes it. Its logic is <see cref="TextWindowViewModel"/>.
    /// </summary>
    public partial class TextWindow : Window
    {
        private static TextWindow _open;

        /// <summary>Makes the window over its view model.</summary>
        /// <param name="model">The view model.</param>
        public TextWindow(TextWindowViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.Lines.CollectionChanged += OnLinesChanged;
            Closed += (s, e) => model.Lines.CollectionChanged -= OnLinesChanged;
            Loaded += (s, e) => ScrollToEnd();
        }

        /// <summary>Gets the view model.</summary>
        public TextWindowViewModel Model { get; }

        /// <summary>Gets the window's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the window over a window, or brings the one open to the front.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void ShowText(Window owner, TextWindowViewModel model)
        {
            if (_open != null)
            {
                _open.Activate();
                _open.ScrollToEnd();
                return;
            }

            var window = new TextWindow(model) { Owner = owner };
            _open = window;
            window.Closed += (s, e) => _open = null;
            window.Show();
        }

        /// <summary>Copies the lines chosen to the clipboard, or every line where none is chosen.</summary>
        public void CopyLines() => Clipboard.SetText(Model.TextToCopy(History.SelectedItems.Cast<string>().ToList()));

        private void ScrollToEnd()
        {
            if (History.Items.Count > 0)
            {
                History.ScrollIntoView(History.Items[History.Items.Count - 1]);
            }
        }

        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new Action(ScrollToEnd));
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2 || e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyLines();
                e.Handled = true;
            }
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnCopy(object sender, RoutedEventArgs e) => CopyLines();

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
