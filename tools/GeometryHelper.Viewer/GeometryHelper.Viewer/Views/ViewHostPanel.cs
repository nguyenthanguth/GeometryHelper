using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// The panel the views stand in: it puts each view that is shown in the cell the <see cref="ViewLayout"/> gives it. A child is one view's frame,
    /// with its number in its Tag; one that is not shown is collapsed and takes no place.
    /// </summary>
    internal sealed class ViewHostPanel : Panel
    {
        /// <summary>
        /// The space between two views, in pixels.
        /// </summary>
        internal const double Gap = 2.0;

        /// <summary>
        /// Gets or sets the layout that says which views are shown and where.
        /// </summary>
        internal ViewLayout Layout { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Layout != null && !double.IsInfinity(availableSize.Width) && !double.IsInfinity(availableSize.Height))
            {
                foreach (ViewCell cell in Layout.Cells(availableSize.Width, availableSize.Height, Gap))
                {
                    FrameElement(cell.Slot)?.Measure(new Size(cell.Width, cell.Height));
                }
            }

            return new Size(double.IsInfinity(availableSize.Width) ? 0.0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0.0 : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Layout != null)
            {
                foreach (ViewCell cell in Layout.Cells(finalSize.Width, finalSize.Height, Gap))
                {
                    FrameElement(cell.Slot)?.Arrange(new Rect(cell.X, cell.Y, cell.Width, cell.Height));
                }
            }

            return finalSize;
        }

        private UIElement FrameElement(int slot) => InternalChildren.OfType<FrameworkElement>().FirstOrDefault(e => e.Tag is int number && number == slot);
    }
}
