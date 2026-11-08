using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The groups of the Drawing ribbon tab, as the design draws them: Drawing (New, Update, Settings), Views (Add view,
    /// Section, Detail), Annotate (Auto dimension, Auto marks, Centre lines) and Export (DXF, PDF, Print). Its data context is the
    /// <see cref="DrawingWorkspace"/>; Add view opens a menu of the views the drawing has not.
    /// </summary>
    public partial class DrawingRibbonTab : UserControl
    {
        /// <summary>Makes the tab's groups.</summary>
        public DrawingRibbonTab()
        {
            InitializeComponent();
        }

        private void OnAddView(object sender, RoutedEventArgs e)
        {
            ViewMenu.DataContext = DataContext;
            ViewMenu.PlacementTarget = AddView;
            ViewMenu.Placement = PlacementMode.Bottom;
            ViewMenu.IsOpen = true;
        }
    }
}
