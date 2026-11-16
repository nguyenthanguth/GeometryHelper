using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The contextual Hatch Creation and Hatch Editor tabs of the ribbon, over a <see cref="HatchTabViewModel"/>.</summary>
    public partial class HatchRibbon : UserControl
    {
        /// <summary>Makes the tab.</summary>
        public HatchRibbon()
        {
            InitializeComponent();
        }

        /// <summary>A button of a menu (a colour, the origin corners, Match Properties): its menu dropped under it.</summary>
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
