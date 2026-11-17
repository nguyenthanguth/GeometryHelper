using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The small dialog of the Data Link Manager's Rename: the new name of a data link, dark as the window's other dialogs.</summary>
    public partial class DataLinkNameWindow : Window
    {
        /// <summary>Makes the dialog.</summary>
        /// <param name="present">The name now, which the box opens with; null for none.</param>
        public DataLinkNameWindow(string present)
        {
            InitializeComponent();
            NameBox.Text = present ?? string.Empty;
            Loaded += (s, e) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Asks the new name of a data link over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="present">The name now, which the box opens with.</param>
        /// <returns>The name typed, or null where Cancel was pressed.</returns>
        public static string Ask(Window owner, string present)
        {
            var window = new DataLinkNameWindow(present) { Owner = owner };
            return window.ShowDialog() == true ? window.NameBox.Text : null;
        }

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
