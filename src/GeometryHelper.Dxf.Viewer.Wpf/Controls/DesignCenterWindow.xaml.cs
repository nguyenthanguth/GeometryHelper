using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's DesignCenter palette, which ADCENTER shows beside the window and leaves open until ADCCLOSE: the tabs Folders,
    /// Open Drawings and History over a tree of folders, drawings and their kinds of content, the list of what the one selected
    /// holds with pictures, and a preview and description under it. A double click or the right-click menu inserts a block,
    /// copies a layer, style, linetype or layout into the drawing shown, or attaches an external reference; a row dragged onto
    /// the canvas does the same where it is dropped. Its logic is <see cref="DesignCenterViewModel"/>.
    /// </summary>
    public partial class DesignCenterWindow : Window
    {
        private static DesignCenterWindow _open;
        private MainViewModel _main;
        private Point _dragStart;
        private DesignCenterItemViewModel _dragItem;

        /// <summary>Makes the palette over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public DesignCenterWindow(DesignCenterViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.LoadRequested += OnLoadRequested;
        }

        /// <summary>Gets the view model.</summary>
        public DesignCenterViewModel Model { get; }

        /// <summary>Gets the palette's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the palette over a window, left open beside it: the one open already is brought to the front. The model is told
        /// it is open, so that the key given ADCENTER closes it again.
        /// </summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="mainModel">The window's view model, whose drawings it lists.</param>
        /// <param name="model">The view model.</param>
        public static void ShowPalette(Window owner, MainViewModel mainModel, DesignCenterViewModel model)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            var window = new DesignCenterWindow(model) { Owner = owner };
            window._main = mainModel;
            _open = window;
            mainModel.PropertyChanged += window.OnMainChanged;
            window.Closed += (s, e) =>
            {
                mainModel.PropertyChanged -= window.OnMainChanged;
                mainModel.NoteDesignCenterShown(false);
                _open = null;
            };
            window.Activated += (s, e) => model.RefreshOpenDrawings();
            mainModel.NoteDesignCenterShown(true);
            window.Show();
        }

        /// <summary>Closes the palette that is open, as ADCCLOSE does; nothing where none is.</summary>
        public static void ClosePalette() => _open?.Close();

        private void OnMainChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Document))
            {
                Model.RefreshOpenDrawings();
            }
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (Model != null)
            {
                Model.Tab = (string)((RadioButton)sender).Content;
            }
        }

        private void OnLoadRequested(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter =
                    "Drawings (*.dwg;*.dws;*.dwt;*.dxf)|*.dwg;*.dws;*.dwt;*.dxf|Drawing (*.dwg)|*.dwg|Standards (*.dws)|*.dws|Template (*.dwt)|*.dwt|DXF (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = "Load DesignCenter",
            };

            if (dialog.ShowDialog(this) == true)
            {
                FoldersTab.IsChecked = true;
                Model.Load(dialog.FileName);
            }
        }

        private void OnItemsSelected(object sender, SelectionChangedEventArgs e) =>
            Model.SetSelectedItems(ItemList.SelectedItems.Cast<DesignCenterItemViewModel>().ToList());

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemOf(e.OriginalSource as DependencyObject) is DesignCenterItemViewModel item)
            {
                Model.Activate(item);
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(ItemList);
            _dragItem = ItemOf(e.OriginalSource as DependencyObject);
        }

        private void OnItemMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null)
            {
                return;
            }

            Vector moved = e.GetPosition(ItemList) - _dragStart;

            if (
                Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance
            )
            {
                return;
            }

            string text = Model.DragText(
                ItemList.SelectedItems.Contains(_dragItem)
                    ? ItemList.SelectedItems.Cast<DesignCenterItemViewModel>().ToList()
                    : new[] { _dragItem }
            );
            _dragItem = null;

            if (text != null)
            {
                var data = new DataObject();
                data.SetData(DesignCenterDrag.Format, text);
                DragDrop.DoDragDrop(ItemList, data, DragDropEffects.Copy);
            }
        }

        /// <summary>The right-click menu: what the rows selected can be given, built as it opens; none offered, it does not open.</summary>
        private void OnItemMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var actions = Model.ActionsFor(ItemList.SelectedItems.Cast<DesignCenterItemViewModel>().ToList());

            if (actions.Count == 0)
            {
                e.Handled = true;
                return;
            }

            var menu = new ContextMenu { Style = (Style)FindResource("GhContextMenu") };

            foreach (DesignCenterAction action in actions)
            {
                DesignCenterAction run = action;
                var item = new MenuItem { Header = run.Text, Style = (Style)FindResource("GhMenuItem") };
                item.Click += (s, a) => run.Run();
                menu.Items.Add(item);
            }

            ItemList.ContextMenu = menu;
        }

        private static DesignCenterItemViewModel ItemOf(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ListBoxItem box)
                {
                    return box.DataContext as DesignCenterItemViewModel;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}
