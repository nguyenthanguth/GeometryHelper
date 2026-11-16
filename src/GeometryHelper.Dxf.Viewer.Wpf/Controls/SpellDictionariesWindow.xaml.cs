using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Dictionaries dialog of the MTEXT editor's Edit Dictionaries: the main dictionary, the custom dictionary file and its
    /// words, with Add and Delete. Its logic is <see cref="SpellDictionariesViewModel"/>.
    /// </summary>
    public partial class SpellDictionariesWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public SpellDictionariesWindow(SpellDictionariesViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) => WordBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public SpellDictionariesViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window until Close.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, SpellDictionariesViewModel model) =>
            new SpellDictionariesWindow(model) { Owner = owner }.ShowDialog();

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnWordKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Model.Add();
                e.Handled = true;
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
