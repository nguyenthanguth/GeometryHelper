using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// AutoCAD's Load/Unload Applications, from APPLOAD, as the design draws it: 720 by 600, a folder browser over Load and the
    /// Loaded Applications and History list tabs; its logic is <see cref="AppLoadViewModel"/>. The Startup Suite's briefcase
    /// takes an AutoLISP file dragged from the list or from Explorer.
    /// </summary>
    public partial class AppLoadWindow : Window
    {
        private Point _dragStart;
        private bool _dragging;

        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public AppLoadWindow(AppLoadViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>Gets the view model.</summary>
        public AppLoadViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window, which waits until it is closed, and the Startup Suite over it where Contents… asks.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True when the dialog was closed with its Close button.</returns>
        public static bool Show(Window owner, AppLoadViewModel model)
        {
            var window = new AppLoadWindow(model) { Owner = owner };
            model.ShowStartupSuite = suite => StartupSuiteWindow.Show(window, suite);
            return window.ShowDialog() == true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBack(object sender, RoutedEventArgs e) => Model.Back();

        private void OnUp(object sender, RoutedEventArgs e) => Model.Up();

        private void OnLoad(object sender, RoutedEventArgs e) => Model.Load();

        private void OnAct(object sender, RoutedEventArgs e) => Model.Act();

        private void OnContents(object sender, RoutedEventArgs e) => Model.ShowContents();

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnEntriesChosen(object sender, SelectionChangedEventArgs e) =>
            Model.ChooseEntries(EntryList.SelectedItems.Cast<AppLoadEntry>());

        private void OnRowsChosen(object sender, SelectionChangedEventArgs e) =>
            Model.ChooseRows(RowList.SelectedItems.Cast<AppLoadRow>());

        private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntryOf(e.OriginalSource as DependencyObject) is AppLoadEntry entry)
            {
                Model.Activate(entry);
            }
        }

        private AppLoadEntry EntryOf(DependencyObject source) =>
            (ItemsControl.ContainerFromElement(EntryList, source) as ListBoxItem)?.DataContext as AppLoadEntry;

        private void OnEntryMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(EntryList);
            _dragging = EntryOf(e.OriginalSource as DependencyObject) is AppLoadEntry entry && !entry.IsFolder;
        }

        /// <summary>A file of the list pulled out of it carries the files chosen, as Explorer's own drag does, to the briefcase.</summary>
        private void OnEntryMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_dragging)
            {
                return;
            }

            Vector moved = e.GetPosition(EntryList) - _dragStart;

            if (
                Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance
            )
            {
                return;
            }

            _dragging = false;
            string[] files = EntryList
                .SelectedItems.Cast<AppLoadEntry>()
                .Where(f => !f.IsFolder)
                .Select(f => f.Path)
                .ToArray();

            if (files.Length > 0)
            {
                var data = new DataObject(DataFormats.FileDrop, files);
                DragDrop.DoDragDrop(EntryList, data, DragDropEffects.Copy);
            }
        }

        private void OnBriefcaseDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnBriefcaseDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                Model.AddToStartupSuite(files);
            }

            e.Handled = true;
        }
    }
}
