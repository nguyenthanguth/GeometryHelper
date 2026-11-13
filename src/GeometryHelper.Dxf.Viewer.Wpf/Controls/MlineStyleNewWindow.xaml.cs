using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's Create New Multiline Style dialog: the name and the style to start with; its logic is <see cref="MlineStyleNewViewModel"/>.</summary>
    public partial class MlineStyleNewWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public MlineStyleNewWindow(MlineStyleNewViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            Loaded += (s, e) => NameBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public MlineStyleNewViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether Continue was pressed with a name taken.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when Continue was pressed.</returns>
        public static bool Show(Window owner, MlineStyleNewViewModel model) =>
            new MlineStyleNewWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnContinue(object sender, RoutedEventArgs e)
        {
            if (Model.TryContinue())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
