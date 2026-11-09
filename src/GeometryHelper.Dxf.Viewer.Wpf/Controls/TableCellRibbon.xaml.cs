using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The contextual Table Cell tab of the ribbon, over a <see cref="TableCellViewModel"/>.</summary>
    public partial class TableCellRibbon : UserControl
    {
        /// <summary>Makes the tab.</summary>
        public TableCellRibbon()
        {
            InitializeComponent();
        }

        /// <summary>A button of a menu (Merge Cells, the alignment, Cell Borders): its menu dropped under it.</summary>
        private void OnMenu(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            ContextMenu menu = button.ContextMenu;

            if (menu != null)
            {
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }
    }
}
