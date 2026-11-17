using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The contextual Block Editor tab of the ribbon, over a <see cref="BlockEditorRibbonViewModel"/>.</summary>
    public partial class BlockEditorRibbon : UserControl
    {
        /// <summary>Makes the tab.</summary>
        public BlockEditorRibbon()
        {
            InitializeComponent();
        }

        /// <summary>The Parameter button: a menu of the parameters under it, each starting BPARAMETER with its kind answered.</summary>
        private void OnParameterMenu(object sender, RoutedEventArgs e) =>
            Open(sender as Button, (DataContext as BlockEditorRibbonViewModel)?.ParameterItems);

        /// <summary>The Action button: a menu of the actions under it, each starting BACTION with its kind answered.</summary>
        private void OnActionMenu(object sender, RoutedEventArgs e) =>
            Open(sender as Button, (DataContext as BlockEditorRibbonViewModel)?.ActionItems);

        private static void Open(Button button, IReadOnlyList<CadMenuItemViewModel> items)
        {
            if (button == null || items == null)
            {
                return;
            }

            ContextMenu menu = CadMenus.Make(items);
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
