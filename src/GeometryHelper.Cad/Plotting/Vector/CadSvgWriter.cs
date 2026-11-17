using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A writer of SVG for SVGOUT (a command of the presenter's own: AutoCAD 2021 writes none): a <see cref="CadVectorPicture"/>
    /// as a standalone SVG 1.1 file in UTF-8, its viewBox the picture's box in drawing units with y turned down, and its width and
    /// height in pixels the longer side 1 333.33 of them (13.9 inches at 96 to the inch, as the metafile's 20 000 units are at
    /// 1 440). A line is a polyline or polygon of its colour with a stroke as wide as its line weight would be on that page
    /// (0.25 millimetres is 0.94 pixel), its dashes in drawing units; a fill is a path of its rings with the even-odd rule; a text is a
    /// text element on its baseline with its font family, size, weight, style, anchor, angle, width factor and slant.
    /// </summary>
    internal static class CadSvgWriter
    {
        /// <summary>The length of the longer side of the page, in pixels.</summary>
        internal const double LongerPixels = 20000.0 / 1440.0 * 96.0;

        /// <summary>Writes a picture as an SVG file.</summary>
        /// <param name="picture">The picture.</param>
        /// <returns>The bytes of the file, UTF-8 with no byte order mark.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the picture is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the picture is empty.</exception>
        public static byte[] Write(CadVectorPicture picture)
        {
            if (picture == null)
            {
                throw new ArgumentNullException(nameof(picture));
            }

            if (picture.IsEmpty)
            {
                throw new ArgumentException("An SVG of nothing is not written.", nameof(picture));
            }

            DisplayBox box = picture.Box;
            double longer = Math.Max(Math.Max(box.Width, box.Height), 1E-9);
            double pixelsPerUnit = LongerPixels / longer;
            double width = Math.Max(box.Width, 1E-9);
            double height = Math.Max(box.Height, 1E-9);
            var svg = new StringBuilder();
            svg.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
                .Append(N(width * pixelsPerUnit))
                .Append("\" height=\"")
                .Append(N(height * pixelsPerUnit))
                .Append("\" viewBox=\"0 0 ")
                .Append(N(width))
                .Append(' ')
                .Append(N(height))
                .Append("\">\n");

            foreach (CadVectorItem item in picture.Items)
            {
                switch (item)
                {
                    case CadVectorStroke stroke:
                        WriteStroke(svg, stroke, box, pixelsPerUnit);
                        break;
                    case CadVectorFill fill:
                        WriteFill(svg, fill, box);
                        break;
                    case CadVectorText text:
                        WriteText(svg, text, box);
                        break;
                }
            }

            svg.Append("</svg>\n");
            return new UTF8Encoding(false).GetBytes(svg.ToString());
        }

        /// <summary>A number with at most four decimals, never in the exponent form.</summary>
        private static string N(double value)
        {
            string text = Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        private static string Colour(int rgb) => "#" + (rgb & 0xFFFFFF).ToString("x6", CultureInfo.InvariantCulture);

        private static string Escape(string text)
        {
            var escaped = new StringBuilder();

            foreach (char letter in text)
            {
                switch (letter)
                {
                    case '&':
                        escaped.Append("&amp;");
                        break;
                    case '<':
                        escaped.Append("&lt;");
                        break;
                    case '>':
                        escaped.Append("&gt;");
                        break;
                    case '"':
                        escaped.Append("&quot;");
                        break;
                    default:
                        // A control character other than the tab has no place in XML 1.0.
                        escaped.Append(letter < ' ' && letter != '\t' ? ' ' : letter);
                        break;
                }
            }

            return escaped.ToString();
        }

        private static void WriteStroke(StringBuilder svg, CadVectorStroke stroke, DisplayBox box, double pixelsPerUnit)
        {
            double weight = Math.Max(stroke.Weight / 25.4 * 96.0, 0.5) / pixelsPerUnit;
            svg.Append(stroke.Closed ? "<polygon points=\"" : "<polyline points=\"");

            for (int i = 0; i + 1 < stroke.Xy.Length; i += 2)
            {
                svg.Append(i == 0 ? string.Empty : " ")
                    .Append(N(stroke.Xy[i] - box.MinX))
                    .Append(',')
                    .Append(N(box.MaxY - stroke.Xy[i + 1]));
            }

            svg.Append("\" fill=\"none\" stroke=\"").Append(Colour(stroke.Rgb)).Append("\" stroke-width=\"");
            svg.Append(N(weight)).Append('"');

            if (stroke.Dashes != null && stroke.Dashes.Length > 0)
            {
                svg.Append(" stroke-dasharray=\"");

                for (int i = 0; i < stroke.Dashes.Length; i++)
                {
                    svg.Append(i == 0 ? string.Empty : ",").Append(N(Math.Max(stroke.Dashes[i], weight / 10.0)));
                }

                svg.Append('"');
            }

            svg.Append("/>\n");
        }

        private static void WriteFill(StringBuilder svg, CadVectorFill fill, DisplayBox box)
        {
            svg.Append("<path d=\"");

            foreach (double[] ring in fill.Rings)
            {
                if (ring.Length < 6)
                {
                    continue;
                }

                for (int i = 0; i + 1 < ring.Length; i += 2)
                {
                    svg.Append(i == 0 ? "M" : " L")
                        .Append(N(ring[i] - box.MinX))
                        .Append(' ')
                        .Append(N(box.MaxY - ring[i + 1]));
                }

                svg.Append(" Z ");
            }

            svg.Append("\" fill=\"").Append(Colour(fill.Rgb)).Append("\" fill-rule=\"evenodd\" stroke=\"none\"/>\n");
        }

        private static void WriteText(StringBuilder svg, CadVectorText text, DisplayBox box)
        {
            svg.Append("<text transform=\"translate(")
                .Append(N(text.X - box.MinX))
                .Append(' ')
                .Append(N(box.MaxY - text.Y))
                .Append(") rotate(")
                .Append(N(-text.Rotation * 180.0 / Math.PI))
                .Append(')');

            if (Math.Abs(text.WidthFactor - 1.0) > 1E-9)
            {
                svg.Append(" scale(").Append(N(text.WidthFactor)).Append(" 1)");
            }

            if (Math.Abs(text.Oblique) > 1E-9)
            {
                svg.Append(" skewX(").Append(N(-text.Oblique * 180.0 / Math.PI)).Append(')');
            }

            svg.Append("\" font-family=\"")
                .Append(Escape(text.Face))
                .Append("\" font-size=\"")
                .Append(N(text.Em))
                .Append('"');

            if (text.Bold)
            {
                svg.Append(" font-weight=\"bold\"");
            }

            if (text.Italic)
            {
                svg.Append(" font-style=\"italic\"");
            }

            svg.Append(" text-anchor=\"")
                .Append(
                    text.Align == 1 ? "middle"
                    : text.Align == 2 ? "end"
                    : "start"
                )
                .Append("\" fill=\"")
                .Append(Colour(text.Rgb))
                .Append("\" xml:space=\"preserve\">")
                .Append(Escape(text.Text))
                .Append("</text>\n");
        }
    }
}
