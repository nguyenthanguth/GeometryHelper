using System;
using System.Windows;
using System.Windows.Data;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The main window of a standalone app of the presenter: the <see cref="CadWorkspace"/> in a frame of its own, the title
    /// bar drawn by the workspace and the window's minimise, maximise and close buttons at its right. GeometryHelper DXF shows
    /// it as it is; GeometryHelper Drawing adds the model drawing to its workspace.
    /// </summary>
    public partial class CadAppWindow : Window
    {
        /// <summary>Makes the window over a view model.</summary>
        /// <param name="model">The view model; its drawing, if any, is shown.</param>
        /// <param name="appName">
        /// The name of the app, before the drawing's title in the title bar and on the start screen: "GeometryHelper DXF" gives
        /// the title "GeometryHelper DXF  plan.dxf" with plan.dxf open.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the view model or the name is null.</exception>
        public CadAppWindow(MainViewModel model, string appName)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            InitializeComponent();
            Workspace.AppName = appName;
            SetBinding(TitleProperty, new Binding(nameof(MainViewModel.Title)) { StringFormat = appName.Replace("{", "{{").Replace("}", "}}") + "  {0}" });
            DataContext = model;
            StateChanged += (s, e) => Workspace.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);

            // Drawings with changes not saved are asked about once, for all of them, before the window closes.
            Closing += (s, e) => e.Cancel = !Model.ConfirmExit();
        }

        /// <summary>Gets the view model.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the name of the app the window shows.</summary>
        public string AppName { get; }

        /// <summary>Gets the workspace the window holds.</summary>
        public CadWorkspace WorkspaceView => Workspace;

        private void OnMinimise(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximise(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
