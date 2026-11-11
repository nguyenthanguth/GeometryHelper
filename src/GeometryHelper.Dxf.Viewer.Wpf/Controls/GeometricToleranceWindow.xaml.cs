using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Geometric Tolerance dialog, which TOLERANCE, LEADER's Tolerance and QLEADER's Tolerance show: two rows of a
    /// symbol, two tolerances and three datums, the height of a projected zone and the datum identifier. A symbol box opens
    /// <see cref="GdtSymbolWindow"/>, a material condition box <see cref="GdtMaterialWindow"/>, the dialog dimmed under them.
    /// Its logic is <see cref="GeometricToleranceViewModel"/>.
    /// </summary>
    public partial class GeometricToleranceWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public GeometricToleranceWindow(GeometricToleranceViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public GeometricToleranceViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, GeometricToleranceViewModel model)
        {
            new GeometricToleranceWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnSymbol(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is ToleranceRowViewModel row))
            {
                return;
            }

            using (DialogDim.Over(Root))
            {
                if (GdtSymbolWindow.Pick(this, new Point(40, 100), row.Symbol, out char? picked))
                {
                    row.Symbol = picked;
                }
            }
        }

        private void OnMaterial(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is MaterialCellViewModel cell))
            {
                return;
            }

            using (DialogDim.Over(Root))
            {
                if (GdtMaterialWindow.Pick(this, new Point(150, 104), cell.Material, out CadMaterialCondition picked))
                {
                    cell.Material = picked;
                }
            }
        }

        private void OnDiameter(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ToleranceCellViewModel cell)
            {
                cell.Diameter = !cell.Diameter;
            }
        }

        private void OnProjectedZone(object sender, RoutedEventArgs e) => Model.ProjectedZone = !Model.ProjectedZone;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // A box being typed in gives its text before the boxes are taken.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Model.Ok();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}
