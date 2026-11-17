using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>AutoCAD's Select Template, from NEW; its logic is <see cref="SelectTemplateViewModel"/>.</summary>
    public partial class SelectTemplateWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        public SelectTemplateWindow(SelectTemplateViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.Chosen += (s, e) =>
            {
                // A choice made while the dialog is shown closes it; one made with it not shown (a test) has nothing to close.
                if (IsVisible)
                {
                    DialogResult = true;
                }
            };
        }

        /// <summary>Gets the view model.</summary>
        public SelectTemplateViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether a template, or none, was chosen.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when a choice was made; false for Cancel.</returns>
        public static bool Show(Window owner, SelectTemplateViewModel model)
        {
            model.BrowseFile = folder => Browse(owner, folder);
            return new SelectTemplateWindow(model) { Owner = owner }.ShowDialog() == true;
        }

        /// <summary>The Open dialog of Browse: Drawing Template (*.dwt), Drawing (*.dwg) and DXF (*.dxf), starting in the template folder where it is there.</summary>
        private static string Browse(Window owner, string folder)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Template",
                CheckFileExists = true,
                Filter = "Drawing Template (*.dwt)|*.dwt|Drawing (*.dwg)|*.dwg|DXF (*.dxf)|*.dxf",
            };

            if (Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnOpen(object sender, RoutedEventArgs e) => Model.Open();

        private void OnOpenMetric(object sender, RoutedEventArgs e) => Model.OpenMetric();

        private void OnOpenImperial(object sender, RoutedEventArgs e) => Model.OpenImperial();

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
