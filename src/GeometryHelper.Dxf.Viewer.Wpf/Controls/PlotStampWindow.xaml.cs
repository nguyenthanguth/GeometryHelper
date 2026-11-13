using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>AutoCAD's Plot Stamp dialog, which PLOTSTAMP and the PLOT dialog show; its logic is <see cref="PlotStampViewModel"/>.</summary>
    public partial class PlotStampWindow : Window
    {
        /// <summary>Makes the dialog over its view model, giving it Advanced Options and the log file's Browse.</summary>
        /// <param name="model">The view model.</param>
        public PlotStampWindow(PlotStampViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.ShowAdvanced = advanced => PlotStampAdvancedWindow.Show(this, advanced);
            model.BrowseLogFile = name =>
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
                    FileName = name,
                    OverwritePrompt = false,
                };
                return dialog.ShowDialog(this) == true ? dialog.FileName : null;
            };
        }

        /// <summary>Gets the view model.</summary>
        public PlotStampViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window and says whether OK was pressed with the values taken.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when OK was pressed.</returns>
        public static bool Show(Window owner, PlotStampViewModel model) =>
            new PlotStampWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnAdvanced(object sender, RoutedEventArgs e) => Model.Advanced();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.TryAccept())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
