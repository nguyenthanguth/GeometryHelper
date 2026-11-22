using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The indent of a row of Look in's drop-down: 12 pixels for each level.</summary>
    internal sealed class AppLoadIndent : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new Thickness(value is int level ? 12 * level : 0, 0, 0, 0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// AutoCAD's Load/Unload Applications, from APPLOAD, as the design draws it: 720 by 600, a folder browser (Look in with its
    /// places, Back, Up) over Load and the Loaded Applications and History list tabs; its logic is
    /// <see cref="AppLoadViewModel"/>. The keys are Windows' own for a file dialog: Enter, Backspace and letters in the list,
    /// Alt+Up, Alt+Left, F5, F4 and Alt+Down anywhere. The Startup Suite's briefcase takes an AutoLISP file dragged from the
    /// list or from Explorer, and the Loaded Applications list takes files to load.
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
            model.ShowMessage = (title, message, detail) =>
                CadQuestionWindow.ShowMessage(window, title, message, detail);
            return window.ShowDialog() == true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool alt = e.Key == Key.System;

            if (HandleKey(alt ? e.SystemKey : e.Key, alt, e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// The keys of the dialog: anywhere Alt+Up goes up, Alt+Left back, F5 lists the folder again, F4 and Alt+Down open Look
        /// in (its own box and Files of type keep theirs); in the list Enter opens the folder chosen or loads the files
        /// chosen, and Backspace goes up. Letters find by name through the list's text search.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="alt">Whether Alt is held.</param>
        /// <param name="source">The element that has the focus.</param>
        /// <returns>Whether the key did something, so that it goes no further.</returns>
        internal bool HandleKey(Key key, bool alt, DependencyObject source)
        {
            if (alt)
            {
                switch (key)
                {
                    case Key.Up:
                        Model.Up();
                        return true;
                    case Key.Left:
                        Model.Back();
                        return true;
                    case Key.Down:
                        return OpenLookIn(source);
                    default:
                        return false;
                }
            }

            switch (key)
            {
                case Key.F5:
                    Model.Refresh();
                    return true;
                case Key.F4:
                    return OpenLookIn(source);
                case Key.Back when IsWithin(source, EntryList):
                    Model.Up();
                    return true;
                case Key.Enter when IsWithin(source, EntryList):
                    return EnterInList();
                default:
                    return false;
            }
        }

        private bool OpenLookIn(DependencyObject source)
        {
            if (IsWithin(source, LookInBox) || IsWithin(source, FileTypeBox))
            {
                return false;
            }

            LookInBox.Focus();
            LookInBox.IsDropDownOpen = true;
            return true;
        }

        private bool EnterInList()
        {
            AppLoadEntry[] chosen = EntryList.SelectedItems.Cast<AppLoadEntry>().ToArray();

            if (chosen.Length == 1 && chosen[0].IsFolder)
            {
                Model.Activate(chosen[0]);
                return true;
            }

            if (chosen.Any(e => !e.IsFolder))
            {
                Model.Load();
                return true;
            }

            return false;
        }

        private static bool IsWithin(DependencyObject source, DependencyObject container)
        {
            for (DependencyObject d = source; d != null; d = ParentOf(d))
            {
                if (ReferenceEquals(d, container))
                {
                    return true;
                }
            }

            return false;
        }

        private static DependencyObject ParentOf(DependencyObject d) =>
            d is Visual
                ? VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnBack(object sender, RoutedEventArgs e) => Model.Back();

        private void OnUp(object sender, RoutedEventArgs e) => Model.Up();

        /// <summary>Load, or Enter in File name; a pattern typed stays in the box, selected, as in AutoCAD.</summary>
        private void OnLoad(object sender, RoutedEventArgs e)
        {
            Model.Load();

            if (Model.FileName.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                FileNameBox.Focus();
                FileNameBox.SelectAll();
            }
        }

        private void OnAct(object sender, RoutedEventArgs e) => Model.Act();

        private void OnContents(object sender, RoutedEventArgs e) => Model.ShowContents();

        private void OnHelp(object sender, RoutedEventArgs e) => PlotHelp.Open(Model.HelpUrl);

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = true;

        /// <summary>
        /// A row of Look in chosen opens it. The box follows the model one way only: a two-way binding writes null back when
        /// the model replaces the rows, reads the place again at once, and leaves the box holding a place with no row.
        /// </summary>
        private void OnLookInChosen(object sender, SelectionChangedEventArgs e)
        {
            if (LookInBox.SelectedItem is AppLoadPlace place && !place.IsCurrent)
            {
                Model.Place = place;
            }
        }

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

        private void OnApplicationsDragOver(object sender, DragEventArgs e)
        {
            e.Effects =
                !Model.IsHistoryTab && e.Data.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Files dropped on the Loaded Applications list are loaded in the order of the drop, but after the drop has ended, so
        /// that Explorer is not held while a question about an untrusted file is asked.
        /// </summary>
        private void OnApplicationsDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => Model.LoadDropped(files)),
                    System.Windows.Threading.DispatcherPriority.Background
                );
            }

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
