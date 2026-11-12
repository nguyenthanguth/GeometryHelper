using System;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's Column Settings dialog, as AutoCAD 2021's and the design draw it: the column type, number, height
    /// and widths. Its logic is <see cref="ColumnSettingsViewModel"/>.
    /// </summary>
    public partial class ColumnSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ColumnSettingsWindow(ColumnSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            ManualHeight.IsChecked = !model.AutoHeight;
            DialogFault.Follow(this, model, () => model.FaultBox);
        }

        /// <summary>Gets the view model.</summary>
        public ColumnSettingsViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Show(Window owner, ColumnSettingsViewModel model) =>
            new ColumnSettingsWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnManualHeight(object sender, RoutedEventArgs e) => Model.AutoHeight = false;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
