using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// How tall a label is drawn on the screen, so that it can be read at any zoom: the height it was given in millimetres while that
    /// shows as a readable height in pixels, and a readable height at either end of the zoom.
    /// </summary>
    public static class LabelSize
    {
        /// <summary>
        /// The height of a label with no height of its own, in pixels: fourteen.
        /// </summary>
        public const double DefaultPixels = 14.0;

        /// <summary>
        /// The least a label is drawn, in pixels: eleven, the smallest height at which digits are told apart.
        /// </summary>
        public const double MinPixels = 11.0;

        /// <summary>
        /// The most a label is drawn, in pixels: forty.
        /// </summary>
        public const double MaxPixels = 40.0;

        /// <summary>
        /// Gets the height to draw a label at.
        /// </summary>
        /// <param name="worldHeight">The height of its letters in millimetres; nought or less for a label with none of its own.</param>
        /// <param name="pixelSize">The world length of one pixel at the label.</param>
        /// <returns>
        /// <see cref="DefaultPixels"/> for a label with no height; otherwise its height in pixels, no less than <see cref="MinPixels"/> and no
        /// more than <see cref="MaxPixels"/>: a label 5 mm high is 50 pixels where a pixel is 0.1 mm, and is drawn 40; where a pixel is 10 mm it is
        /// half a pixel, and is drawn 11.
        /// </returns>
        public static double Pixels(double worldHeight, double pixelSize)
        {
            if (!(worldHeight > 0.0) || double.IsInfinity(worldHeight) || !(pixelSize > 0.0) || double.IsInfinity(pixelSize))
            {
                return DefaultPixels;
            }

            return Math.Max(MinPixels, Math.Min(MaxPixels, worldHeight / pixelSize));
        }
    }
}
