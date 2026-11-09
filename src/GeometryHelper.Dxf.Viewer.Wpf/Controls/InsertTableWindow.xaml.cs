using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Insert Table dialog (TABLE), dark as the window's other dialogs. Its logic is <see cref="InsertTableViewModel"/>;
    /// the table button beside the style opens the Table Style dialog over it and reads the styles again.
    /// </summary>
    public partial class InsertTableWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public InsertTableWindow(InsertTableViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.CloseRequested += (s, ok) =>
            {
                if (IsLoaded)
                {
                    DialogResult = ok;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public InsertTableViewModel Model { get; }

        /// <summary>Gets or sets what shows the Table Style dialog over this one. Set by whoever shows the dialog.</summary>
        public Action<Window> ShowTableStyles { get; set; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">The view model.</param>
        /// <param name="showTableStyles">What shows the Table Style dialog over the dialog given.</param>
        /// <returns>True: the dialog answered the request, OK or Cancel.</returns>
        public static bool Show(Window owner, InsertTableViewModel model, Action<Window> showTableStyles)
        {
            new InsertTableWindow(model) { Owner = owner, ShowTableStyles = showTableStyles }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTableStyles(object sender, RoutedEventArgs e)
        {
            ShowTableStyles?.Invoke(this);
            Model.ReloadStyles();
        }
    }
}
