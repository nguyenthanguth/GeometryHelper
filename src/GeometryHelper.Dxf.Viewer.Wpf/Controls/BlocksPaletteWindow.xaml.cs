using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Blocks palette, which INSERT (I) shows: the tabs Current drawing, Recent and Other drawing, the tiles of the
    /// blocks filtered, and the insertion options; a tile clicked closes it for INSERT to place the block. Its logic is
    /// <see cref="BlocksPaletteViewModel"/>.
    /// </summary>
    public partial class BlocksPaletteWindow : Window
    {
        /// <summary>Makes the palette over its view model, opened on the model's tab: Recent where Insert's gallery asks for it.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public BlocksPaletteWindow(BlocksPaletteViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            string tab = model.Tab;
            InitializeComponent();
            DataContext = model;

            // The first tab is checked as the window is made, which sets the model's tab; the tab asked for is checked after it.
            RecentTab.IsChecked = tab == "Recent";
            OtherTab.IsChecked = tab == "Other drawing";
            model.Tab = tab;
        }

        /// <summary>Gets the view model.</summary>
        public BlocksPaletteViewModel Model { get; }

        /// <summary>Gets the palette's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the palette over a window; the block chosen, if any, is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered, Cancel among the answers.</returns>
        public static bool Show(Window owner, BlocksPaletteViewModel model)
        {
            new BlocksPaletteWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (Model != null)
            {
                Model.Tab = (string)((RadioButton)sender).Content;
            }
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Drawing files (*.dxf)|*.dxf|All files (*.*)|*.*", Title = "Select Drawing File" };

            if (dialog.ShowDialog(this) == true && !Model.OpenOther(dialog.FileName))
            {
                MessageBox.Show(this, "The drawing " + dialog.FileName + " cannot be read.", "Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            OtherTab.IsChecked = Model.IsOtherDrawing;
        }

        private void OnTile(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is BlockTileViewModel tile && Model.Choose(tile))
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Request.Action = CadDialogAction.Cancel;
            DialogResult = false;
        }
    }
}
