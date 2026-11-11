using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The box the External References palette's Bind… shows where nothing was bound: the lines -XREF Bind wrote on the
    /// command line, as "Xref "column" is nested and cannot be bound.", and what to do next; OK, Enter, Esc or the close
    /// button close it. Its logic is <see cref="XrefBindRefusedViewModel"/>.
    /// </summary>
    public partial class XrefBindRefusedWindow : Window
    {
        /// <summary>Makes the box over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public XrefBindRefusedWindow(XrefBindRefusedViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public XrefBindRefusedViewModel Model { get; }

        /// <summary>Gets the box's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the box over a window until it is closed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        public static void Show(Window owner, XrefBindRefusedViewModel model)
        {
            var box = new XrefBindRefusedWindow(model);

            if (owner != null && owner.IsVisible)
            {
                box.Owner = owner;
            }
            else
            {
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            box.ShowDialog();
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
