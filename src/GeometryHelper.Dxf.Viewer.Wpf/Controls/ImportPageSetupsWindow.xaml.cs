using System;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Import Page Setups, from Import… of the Page Setup Manager: the page setups of the drawing chosen, each ticked to be
    /// copied into this one. Its logic is <see cref="ImportPageSetupsViewModel"/>.
    /// </summary>
    public partial class ImportPageSetupsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ImportPageSetupsWindow(ImportPageSetupsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public ImportPageSetupsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, ImportPageSetupsViewModel model) =>
            new ImportPageSetupsWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.CanOk)
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);
    }
}
