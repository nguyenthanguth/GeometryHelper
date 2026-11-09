using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The application menu of the G button, drawn from an <see cref="AppMenuViewModel"/>: the workspace shows it in a popup under
    /// the button, and a screenshot draws it alone. The pointer resting on New, Open, Save As, Export, Print or Close shows its
    /// list at the right; a click runs an entry; Enter in the search box runs the first command found.
    /// </summary>
    public partial class AppMenuView : UserControl
    {
        /// <summary>Makes the menu's view; its data context is the menu.</summary>
        public AppMenuView()
        {
            InitializeComponent();
        }

        /// <summary>Puts the keyboard in the search box, as the menu opens.</summary>
        public void FocusSearch() => SearchBox.Focus();

        private AppMenuViewModel Menu => DataContext as AppMenuViewModel;

        private void OnItemEnter(object sender, MouseEventArgs e)
        {
            if (Menu != null && ((FrameworkElement)sender).DataContext is AppMenuItemViewModel item)
            {
                Menu.ShownItem = item;
            }
        }

        private void OnItemClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is AppMenuItemViewModel item)
            {
                Menu?.Choose(item);
            }
        }

        private void OnEntryClick(object sender, RoutedEventArgs e) => OnItemClick(sender, e);

        private void OnRecentClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is RecentDocumentViewModel document)
            {
                Menu?.OpenRecent(document);
            }
        }

        private void OnResultClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is CommandSuggestionViewModel row)
            {
                Menu?.RunSearchResult(row);
            }
        }

        private void OnSearchKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Menu != null && Menu.SearchResults.Count > 0)
            {
                Menu.RunSearchResult(Menu.SearchResults.First());
                e.Handled = true;
            }
        }
    }
}
