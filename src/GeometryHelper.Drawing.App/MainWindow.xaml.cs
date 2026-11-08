using System;
using System.Windows;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.App
{
    /// <summary>
    /// The main window of GeometryHelper Drawing: the presenter's <see cref="CadWorkspace"/> in a frame of its own, the title
    /// bar drawn by the workspace and the window's minimise, maximise and close buttons at its right.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Makes the window over a view model.</summary>
        /// <param name="model">The view model; its drawing, if any, is shown.</param>
        public MainWindow(MainViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            StateChanged += (s, e) => Workspace.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);

            // Drawings with changes not saved are asked about once, for all of them, before the window closes.
            Closing += (s, e) => e.Cancel = !Model.ConfirmExit();
        }

        /// <summary>Gets the view model.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the workspace the window holds.</summary>
        public CadWorkspace WorkspaceView => Workspace;

        private void OnMinimise(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximise(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
