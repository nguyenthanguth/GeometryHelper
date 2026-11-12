using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The command line's AutoComplete list, drawn from the <see cref="MainViewModel.Suggestions"/> of its data context: the
    /// workspace shows it in a popup above the command line and under the box of dynamic input at the cursor, and a screenshot
    /// draws it alone. It shows <see cref="VisibleRows"/> rows at a time, the rest scrolled to with the wheel or the arrows,
    /// as the maintainer asked: typing A shows ARC, ATTDEF, ALIGN, AREA and ARRAY, the others below. A click on a row runs it.
    /// </summary>
    public partial class CommandSuggestionList : UserControl
    {
        /// <summary>The rows shown at a time; the list is as tall as its first five, a rule between groups with them.</summary>
        public const int VisibleRows = 5;

        /// <summary>Makes the list; its data context is the window's view model.</summary>
        public CommandSuggestionList()
        {
            InitializeComponent();
        }

        /// <summary>Scrolls the row chosen with the arrows into sight.</summary>
        /// <param name="index">The index of the row among the suggestions; one out of range is passed over.</param>
        public void ShowRow(int index)
        {
            Rows.UpdateLayout();

            if (index >= 0 && Rows.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement row)
            {
                row.BringIntoView();
            }
        }

        /// <summary>
        /// Measures the list as tall as its first <see cref="VisibleRows"/> rows, where there are more: the rows are measured
        /// whole inside the scroller, and the height of the first five, a rule before one with it, holds the scroller to them.
        /// </summary>
        /// <param name="constraint">The size the list may take.</param>
        /// <returns>The size it takes.</returns>
        protected override Size MeasureOverride(Size constraint)
        {
            Size size = base.MeasureOverride(constraint);
            double height = double.PositiveInfinity;

            if (Rows.Items.Count > VisibleRows)
            {
                height = 0.0;

                for (int i = 0; i < VisibleRows; i++)
                {
                    // Rows not made yet leave the height as it is, for the next measure.
                    if (!(Rows.ItemContainerGenerator.ContainerFromIndex(i) is UIElement row))
                    {
                        return size;
                    }

                    height += row.DesiredSize.Height;
                }
            }

            if (
                Math.Abs(Scroller.MaxHeight - height) > 0.5
                && !(double.IsInfinity(height) && double.IsInfinity(Scroller.MaxHeight))
            )
            {
                Scroller.MaxHeight = height;
                size = base.MeasureOverride(constraint);
            }

            return size;
        }

        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            if (
                DataContext is MainViewModel model
                && ((FrameworkElement)sender).DataContext is CommandSuggestionViewModel row
            )
            {
                model.RunSuggestion(row);
                e.Handled = true;
            }
        }
    }
}
