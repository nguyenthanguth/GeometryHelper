using System;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Colours as a scene writes them: #RRGGBB, and the cycle of colours a layer gets when its writer names none.
    /// </summary>
    public static class SceneColor
    {
        private static readonly string[] _cycle =
        {
            "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728", "#9467BD",
            "#8C564B", "#E377C2", "#7F7F7F", "#BCBD22", "#17BECF",
        };

        /// <summary>
        /// Gets the colour a layer gets when it is the n-th to be made without a colour of its own.
        /// </summary>
        /// <param name="index">The number of layers made before it.</param>
        public static string FromCycle(int index) => _cycle[((index % _cycle.Length) + _cycle.Length) % _cycle.Length];

        /// <summary>
        /// Reads a colour written as #RRGGBB, in either case.
        /// </summary>
        /// <param name="color">The text.</param>
        /// <param name="red">The red part, 0 to 255.</param>
        /// <param name="green">The green part, 0 to 255.</param>
        /// <param name="blue">The blue part, 0 to 255.</param>
        /// <returns>true when the text is a colour; otherwise, false.</returns>
        public static bool TryParse(string color, out byte red, out byte green, out byte blue)
        {
            red = green = blue = 0;

            if (color == null || color.Length != 7 || color[0] != '#')
            {
                return false;
            }

            return byte.TryParse(color.Substring(1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out red)
                && byte.TryParse(color.Substring(3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out green)
                && byte.TryParse(color.Substring(5, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out blue);
        }

        /// <summary>
        /// Writes a colour in capitals, as #RRGGBB.
        /// </summary>
        /// <param name="color">The colour; null stays null.</param>
        /// <exception cref="ArgumentException">Thrown when the text is not #RRGGBB.</exception>
        public static string Normalize(string color)
        {
            if (color == null)
            {
                return null;
            }

            if (!TryParse(color, out byte red, out byte green, out byte blue))
            {
                throw new ArgumentException("A colour is written #RRGGBB, as in #1F77B4; this one is '" + color + "'.", nameof(color));
            }

            return "#" + red.ToString("X2", CultureInfo.InvariantCulture) + green.ToString("X2", CultureInfo.InvariantCulture) + blue.ToString("X2", CultureInfo.InvariantCulture);
        }
    }
}
