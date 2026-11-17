using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A writer of Encapsulated PostScript of its own, for PSOUT: a level 2 file (<c>%!PS-Adobe-3.0 EPSF-3.0</c>) of a
    /// <see cref="CadVectorPicture"/>, in vectors, of the size of the output box asked for, in points of 1/72 inch, its
    /// <c>%%BoundingBox</c> the box rounded outwards. A line is its own <c>moveto</c>, <c>lineto</c> and <c>stroke</c> with its
    /// colour, its line weight in points and its dashes; a fill is its rings closed and filled by <c>eofill</c>, the even-odd
    /// rule; a text is set in one of the base 13 fonts (Helvetica, Times or Courier, in the look of its bold and italic) at its
    /// height, angle, width factor and slant, aligned by <c>stringwidth</c>. Everything is clipped to the output box. The file is
    /// 7-bit text with a line feed at the end of each line; a letter beyond ASCII is written as its octal code of Latin-1.
    /// </summary>
    internal static class CadEpsWriter
    {
        /// <summary>The points to the inch of PostScript.</summary>
        internal const double PointsPerInch = 72.0;

        /// <summary>Writes a picture as an Encapsulated PostScript file.</summary>
        /// <param name="picture">The picture.</param>
        /// <param name="area">The part of the picture's space that is drawn, in drawing units.</param>
        /// <param name="scale">The points to a drawing unit.</param>
        /// <param name="width">The width of the output box, in points.</param>
        /// <param name="height">The height of the output box, in points.</param>
        /// <param name="title">The title the file's comments give.</param>
        /// <returns>The bytes of the file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the picture is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the area is empty, or the scale, the width or the height is not above nought.</exception>
        public static byte[] Write(
            CadVectorPicture picture,
            DisplayBox area,
            double scale,
            double width,
            double height,
            string title
        )
        {
            if (picture == null)
            {
                throw new ArgumentNullException(nameof(picture));
            }

            if (area.IsEmpty)
            {
                throw new ArgumentException("The area is empty.", nameof(area));
            }

            if (!(scale > 0.0) || !(width > 0.0) || !(height > 0.0))
            {
                throw new ArgumentException("The scale, the width and the height must be above nought.");
            }

            // The area centred in the output box.
            double offsetX = (width - area.Width * scale) / 2.0 - area.MinX * scale;
            double offsetY = (height - area.Height * scale) / 2.0 - area.MinY * scale;
            var eps = new StringBuilder();
            eps.Append("%!PS-Adobe-3.0 EPSF-3.0\n");
            eps.Append("%%Creator: GeometryHelper PSOUT\n");
            eps.Append("%%Title: ").Append(Comment(title)).Append('\n');
            eps.Append("%%BoundingBox: 0 0 ")
                .Append(((long)Math.Ceiling(width - 1E-9)).ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(((long)Math.Ceiling(height - 1E-9)).ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            eps.Append("%%HiResBoundingBox: 0 0 ").Append(N(width)).Append(' ').Append(N(height)).Append('\n');
            eps.Append("%%LanguageLevel: 2\n%%Pages: 1\n%%EndComments\n");
            eps.Append("%%BeginProlog\n");
            eps.Append("/m{moveto}bind def /l{lineto}bind def /c{closepath}bind def /s{stroke}bind def\n");
            eps.Append("/C{setrgbcolor}bind def /W{setlinewidth}bind def\n");
            eps.Append("%%EndProlog\n%%Page: 1 1\ngsave\n");
            eps.Append("newpath 0 0 m ")
                .Append(N(width))
                .Append(" 0 l ")
                .Append(N(width))
                .Append(' ')
                .Append(N(height))
                .Append(" l 0 ")
                .Append(N(height))
                .Append(" l c clip newpath\n1 setlinejoin\n");

            foreach (CadVectorItem item in picture.Items)
            {
                switch (item)
                {
                    case CadVectorStroke stroke:
                        WriteStroke(eps, stroke, scale, offsetX, offsetY);
                        break;
                    case CadVectorFill fill:
                        WriteFill(eps, fill, scale, offsetX, offsetY);
                        break;
                    case CadVectorText text:
                        WriteText(eps, text, scale, offsetX, offsetY);
                        break;
                }
            }

            eps.Append("grestore\nshowpage\n%%Trailer\n%%EOF\n");
            return Encoding.ASCII.GetBytes(eps.ToString());
        }

        /// <summary>A number with at most three decimals, nought for a number that rounds to it, never in the exponent form.</summary>
        private static string N(double value)
        {
            string text = Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        /// <summary>A colour as its three parts, each between 0 and 1, to three decimals.</summary>
        private static string Rgb(int rgb) =>
            N(((rgb >> 16) & 0xFF) / 255.0) + " " + N(((rgb >> 8) & 0xFF) / 255.0) + " " + N((rgb & 0xFF) / 255.0);

        /// <summary>A title as a line of a comment: no line feed, nothing beyond ASCII.</summary>
        private static string Comment(string title)
        {
            var line = new StringBuilder();

            foreach (char letter in title ?? string.Empty)
            {
                line.Append(letter >= ' ' && letter < 127 ? letter : '?');
            }

            return line.ToString();
        }

        private static void WriteStroke(StringBuilder eps, CadVectorStroke stroke, double s, double ox, double oy)
        {
            eps.Append(Rgb(stroke.Rgb)).Append(" C ").Append(N(stroke.Weight * PointsPerInch / 25.4)).Append(" W ");

            if (stroke.Dashes != null && stroke.Dashes.Length > 0)
            {
                eps.Append('[');

                for (int i = 0; i < stroke.Dashes.Length; i++)
                {
                    // A dot is a dash of nought; a hundredth of a point is as long as one can be seen.
                    eps.Append(i > 0 ? " " : string.Empty).Append(N(Math.Max(stroke.Dashes[i] * s, 0.01)));
                }

                eps.Append("] 0 setdash ");
            }
            else
            {
                eps.Append("[] 0 setdash ");
            }

            for (int i = 0; i + 1 < stroke.Xy.Length; i += 2)
            {
                eps.Append(N(stroke.Xy[i] * s + ox))
                    .Append(' ')
                    .Append(N(stroke.Xy[i + 1] * s + oy))
                    .Append(i == 0 ? " m " : " l ");
            }

            eps.Append(stroke.Closed ? "c s\n" : "s\n");
        }

        private static void WriteFill(StringBuilder eps, CadVectorFill fill, double s, double ox, double oy)
        {
            eps.Append(Rgb(fill.Rgb)).Append(" C newpath\n");

            foreach (double[] ring in fill.Rings)
            {
                if (ring.Length < 6)
                {
                    continue;
                }

                for (int i = 0; i + 1 < ring.Length; i += 2)
                {
                    eps.Append(N(ring[i] * s + ox))
                        .Append(' ')
                        .Append(N(ring[i + 1] * s + oy))
                        .Append(i == 0 ? " m " : " l ");
                }

                eps.Append("c\n");
            }

            eps.Append("eofill\n");
        }

        private static void WriteText(StringBuilder eps, CadVectorText text, double s, double ox, double oy)
        {
            double em = text.Em * s;
            double slant = em * Math.Tan(text.Oblique);
            eps.Append(Rgb(text.Rgb)).Append(" C gsave ");
            eps.Append(N(text.X * s + ox)).Append(' ').Append(N(text.Y * s + oy)).Append(" translate ");
            eps.Append(N(text.Rotation * 180.0 / Math.PI)).Append(" rotate ");
            eps.Append('/').Append(FontOf(text.Face, text.Bold, text.Italic)).Append(" findfont [");
            eps.Append(N(em * text.WidthFactor)).Append(" 0 ").Append(N(slant)).Append(' ').Append(N(em));
            eps.Append(" 0 0] makefont setfont 0 0 m ");
            eps.Append(Literal(text.Text));
            eps.Append(
                text.Align == 1 ? " dup stringwidth pop -2 div 0 rmoveto"
                : text.Align == 2 ? " dup stringwidth pop neg 0 rmoveto"
                : string.Empty
            );
            eps.Append(" show grestore\n");
        }

        /// <summary>
        /// The base font a face is set in, in the look of bold and italic: Times for a face of Times, Georgia, Cambria, Garamond
        /// or Palatino, Courier for a face of Courier, Consolas or Lucida Console, Helvetica for any other.
        /// </summary>
        internal static string FontOf(string face, bool bold, bool italic)
        {
            string name = (face ?? string.Empty).ToLowerInvariant();
            bool times =
                name.Contains("times")
                || name.Contains("georgia")
                || name.Contains("cambria")
                || name.Contains("garamond")
                || name.Contains("palatino");
            bool courier = name.Contains("courier") || name.Contains("consolas") || name.Contains("lucida console");

            if (times)
            {
                return bold ? (italic ? "Times-BoldItalic" : "Times-Bold") : (italic ? "Times-Italic" : "Times-Roman");
            }

            string family = courier ? "Courier" : "Helvetica";
            return family + (bold ? (italic ? "-BoldOblique" : "-Bold") : (italic ? "-Oblique" : string.Empty));
        }

        /// <summary>A text as a PostScript string: in parentheses, a backslash, a parenthesis, a control or a letter beyond ASCII as its escape.</summary>
        private static string Literal(string text)
        {
            var literal = new StringBuilder("(");

            foreach (char letter in text)
            {
                if (letter == '\\' || letter == '(' || letter == ')')
                {
                    literal.Append('\\').Append(letter);
                }
                else if (letter >= ' ' && letter < 127)
                {
                    literal.Append(letter);
                }
                else
                {
                    int code = letter < 256 ? letter : '?';
                    literal.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
                }
            }

            return literal.Append(')').ToString();
        }
    }
}
