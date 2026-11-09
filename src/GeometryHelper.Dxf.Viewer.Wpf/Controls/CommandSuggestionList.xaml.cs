using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The command line's AutoComplete list, drawn from the <see cref="MainViewModel.Suggestions"/> of its data context: the
    /// workspace shows it in a popup above the command line, and a screenshot draws it alone. A click on a row runs it.
    /// </summary>
    public partial class CommandSuggestionList : UserControl
    {
        /// <summary>Makes the list; its data context is the window's view model.</summary>
        public CommandSuggestionList()
        {
            InitializeComponent();
        }

        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel model && ((FrameworkElement)sender).DataContext is CommandSuggestionViewModel row)
            {
                model.RunSuggestion(row);
                e.Handled = true;
            }
        }
    }
}
