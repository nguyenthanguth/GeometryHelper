using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The kinds of file PNGOUT, JPGOUT and BMPOUT write.</summary>
    public enum CadImageFormat
    {
        /// <summary>A PNG of 8-bit red, green and blue, as PNGOUT writes.</summary>
        Png,

        /// <summary>A JPEG at a fixed quality of 90, as JPGOUT writes.</summary>
        Jpeg,

        /// <summary>A bitmap of 24 bits a pixel, as BMPOUT writes.</summary>
        Bmp,
    }

    /// <summary>
    /// An image PNGOUT, JPGOUT or BMPOUT hands to the window (<see cref="CadUiRequestEventArgs.Image"/>): the space and the
    /// view the drawing area shows, the file, and the objects selected. The window first fills <see cref="FileName"/> from its
    /// Create Raster File dialog; then it draws the view at the drawing area's size, as it is shown but with no cursor, grips
    /// or highlight, and writes the file. A view of 1 200 by 800 pixels gives an image of 1 200 by 800.
    /// </summary>
    public sealed class CadImageJob
    {
        /// <summary>Makes an image to hand to the window.</summary>
        /// <param name="format">The kind of file.</param>
        /// <param name="space">The space shown.</param>
        /// <param name="view">The view of the drawing area: what part of the space is shown, at what zoom, and its size in pixels.</param>
        /// <exception cref="ArgumentNullException">Thrown when the space or the view is null.</exception>
        public CadImageJob(CadImageFormat format, CadSpace space, CadView view)
        {
            Format = format;
            Space = space ?? throw new ArgumentNullException(nameof(space));
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>Gets the kind of file.</summary>
        public CadImageFormat Format { get; }

        /// <summary>Gets the extension of the file, with its dot: .png, .jpg or .bmp.</summary>
        public string Extension =>
            Format == CadImageFormat.Png ? ".png"
            : Format == CadImageFormat.Jpeg ? ".jpg"
            : ".bmp";

        /// <summary>Gets the space shown.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets the view of the drawing area; with no window, one of 1 920 by 1 080 pixels showing the space's extents.</summary>
        public CadView View { get; }

        /// <summary>Gets the width of the image in pixels: the view's, at least 1.</summary>
        public int PixelWidth => Math.Max(1, (int)Math.Round(View.Width));

        /// <summary>Gets the height of the image in pixels: the view's, at least 1.</summary>
        public int PixelHeight => Math.Max(1, (int)Math.Round(View.Height));

        /// <summary>
        /// Gets or sets the file to write. The command sets the one it offers before asking the window, which sets the one its
        /// dialog chose, or null where it was cancelled.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>Gets the handles of the objects selected, in ordinal order; null for all objects and viewports.</summary>
        public IReadOnlyList<string> Handles { get; internal set; }
    }
}
