using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What is behind the scene in a screenshot.
    /// </summary>
    public enum ScreenshotBackground
    {
        /// <summary>
        /// The colour the views have in the window.
        /// </summary>
        Window,

        /// <summary>
        /// White.
        /// </summary>
        White,

        /// <summary>
        /// Nothing: the picture has an alpha channel, and the scene stands on transparency.
        /// </summary>
        Transparent,
    }

    /// <summary>
    /// How a screenshot is taken: of the active view or of all the views, at one, two or four times the size of the window, over the window's colour,
    /// white or nothing. It holds the sums of the picture, which are the same wherever the picture is drawn.
    /// </summary>
    public sealed class ScreenshotOptions
    {
        private int _scale = 1;

        /// <summary>
        /// Gets or sets whether the picture is of all the views shown, laid out as they are in the window, and not of the active view alone.
        /// </summary>
        public bool AllViews { get; set; } = true;

        /// <summary>
        /// Gets or sets how many times the size of the window the picture is: 1, 2 or 4. Another number is taken as 1.
        /// </summary>
        public int Scale
        {
            get => _scale;
            set => _scale = IsScale(value) ? value : 1;
        }

        /// <summary>
        /// Gets or sets what is behind the scene.
        /// </summary>
        public ScreenshotBackground Background { get; set; } = ScreenshotBackground.White;

        /// <summary>
        /// Gets whether a number is a scale a picture can be taken at: 1, 2 or 4.
        /// </summary>
        /// <param name="scale">The number.</param>
        public static bool IsScale(int scale) => scale == 1 || scale == 2 || scale == 4;

        /// <summary>
        /// Gets the size of the picture, in pixels: the area the views share in the window, or the cell of the active view where that alone is taken, times the scale.
        /// </summary>
        /// <param name="layout">The views.</param>
        /// <param name="width">The width of the area the views share in the window.</param>
        /// <param name="height">The height of the area.</param>
        /// <param name="gap">The space between two views in the window.</param>
        /// <param name="pixelsWidth">The width of the picture.</param>
        /// <param name="pixelsHeight">The height of the picture.</param>
        /// <exception cref="ArgumentNullException">Thrown when the layout is null.</exception>
        public void PictureSize(ViewLayout layout, double width, double height, double gap, out int pixelsWidth, out int pixelsHeight)
        {
            double w = width;
            double h = height;

            if (!AllViews)
            {
                ViewCell cell = Cells(layout, width, height, gap)[0];
                pixelsWidth = Math.Max(1, (int)Math.Ceiling(cell.Width));
                pixelsHeight = Math.Max(1, (int)Math.Ceiling(cell.Height));
                return;
            }

            pixelsWidth = Math.Max(1, (int)Math.Ceiling(w * Scale));
            pixelsHeight = Math.Max(1, (int)Math.Ceiling(h * Scale));
        }

        /// <summary>
        /// Gets where each view is in the picture, in pixels of the picture.
        /// </summary>
        /// <param name="layout">The views.</param>
        /// <param name="width">The width of the area the views share in the window.</param>
        /// <param name="height">The height of the area.</param>
        /// <param name="gap">The space between two views in the window.</param>
        /// <returns>
        /// The cells of the views as they stand in the window, each scaled, for all the views; for the active view alone, one cell, the whole of the
        /// picture, which is as large as the area of the window its view fills, scaled.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the layout is null.</exception>
        public IReadOnlyList<ViewCell> Cells(ViewLayout layout, double width, double height, double gap)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            IReadOnlyList<ViewCell> cells = layout.Cells(width, height, gap);

            if (!AllViews)
            {
                ViewCell active = cells.First(c => c.Slot == layout.Active);
                return new[] { new ViewCell(active.Slot, 0.0, 0.0, active.Width * Scale, active.Height * Scale) };
            }

            return cells.Select(c => new ViewCell(c.Slot, c.X * Scale, c.Y * Scale, c.Width * Scale, c.Height * Scale)).ToList();
        }
    }
}
