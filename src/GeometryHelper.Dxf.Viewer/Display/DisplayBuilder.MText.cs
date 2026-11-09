using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The words of an MTEXT in more than one look, drawn run by run as its codes say.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>How far below its baseline a run is underlined, over its height.</summary>
        private const double UnderlineDrop = 0.2;

        /// <summary>How far above its baseline a run is overlined, over its height.</summary>
        private const double OverlineRise = 1.2;

        /// <summary>
        /// The parts an MTEXT is drawn as run by run (<see cref="CadMTextContent"/>, <see cref="DisplayText2.Parts"/>): each paragraph a line, 1.25 times its tallest letters
        /// below the line before as a TEXT's lines are set; each run a text of its own after the one before, in its font (bold
        /// and italic faces of a TrueType font), height, width factor, slant and colour, with its lines under, over and through
        /// it, standing in its line as <c>\A</c> says; each line placed in the MTEXT's box by its paragraph's alignment, or by
        /// the attachment where it has none, the box as wide as the MTEXT's width or, with none, its widest line, and the box
        /// put at the attachment point. Not drawn: the spacing of <c>\T</c>, the wrapping of words at the width, columns and
        /// a stack's letters one above the other (a stack is drawn as 1/2).
        /// </summary>
        private List<DisplayItem2> MTextRuns(DxfMText text, CadMTextContent content, Frame frame, DisplayStyle style, string handle, string layerName)
        {
            double height = text.TextHeight;
            var parts = new List<DisplayItem2>();
            Affine2 m = frame.Matrix;
            double scale = m.Scale;
            GeoVector3 direction = text.Direction;
            double turn = Orient(m, Math.Atan2(direction.Y, direction.X), false, false, out bool backward);
            int attach = (int)text.Attachment;
            int column = attach >= 1 && attach <= 9 ? (attach - 1) % 3 : 0;
            int row = attach >= 1 && attach <= 9 ? (attach - 1) / 3 : 0;
            CadFontFace styleFace = FaceOf(text.TextStyleName);
            double styleWidth = WidthFactorOf(text.TextStyleName);
            double styleOblique = ObliqueOf(text.TextStyleName);
            var lines = new List<List<Piece>>();
            var widths = new List<double>();
            var tallest = new List<double>();
            double widest = 0.0;

            foreach (CadMTextParagraph paragraph in content.Paragraphs)
            {
                var pieces = new List<Piece>();
                double pen = 0.0;
                double top = 0.0;

                foreach (CadMTextRun run in paragraph.Runs)
                {
                    if (run.Text.Length == 0)
                    {
                        continue;
                    }

                    CadMTextLook look = run.Look;
                    CadFontFace face = look.Font == null ? styleFace : FaceOfFont(look.Font, look.IsShx);
                    double h = (look.Height ?? height) * scale;
                    double w = look.WidthFactor ?? styleWidth;
                    double slant = look.Oblique.HasValue ? look.Oblique.Value * Math.PI / 180.0 : styleOblique;
                    bool bold = face.Shx == null && look.Bold;
                    bool italic = face.Shx == null && look.Italic;
                    double width = Measure(face, run.Text, h, w, bold, italic);
                    pieces.Add(new Piece(run.Text, look, face, h, w, slant, bold, italic, pen, width));
                    pen += width;
                    top = Math.Max(top, h);
                }

                lines.Add(pieces);
                widths.Add(pen);
                tallest.Add(top > 0.0 ? top : height * scale);
                widest = Math.Max(widest, pen);
            }

            double box = text.Width > 0.0 ? text.Width * scale : widest;
            double left = column == 0 ? 0.0 : column == 1 ? -box / 2.0 : -box;
            var baselines = new double[lines.Count];

            for (int k = 0; k < lines.Count; k++)
            {
                baselines[k] = k == 0 ? -tallest[0] : baselines[k - 1] - 1.25 * tallest[k];
            }

            double bottom = baselines[lines.Count - 1];
            double shift = row == 0 ? 0.0 : row == 1 ? -bottom / 2.0 : -bottom;
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            GeoPoint3 at = text.Location;
            double ox = m.X(at.X, at.Y);
            double oy = m.Y(at.X, at.Y);

            for (int k = 0; k < lines.Count; k++)
            {
                char alignment = content.Paragraphs[k].Alignment == '\0' ? "lcr"[column] : content.Paragraphs[k].Alignment;
                double offset = alignment == 'c' ? (box - widths[k]) / 2.0 : alignment == 'r' ? box - widths[k] : 0.0;

                foreach (Piece piece in lines[k])
                {
                    double rise = piece.Look.Align == 1 ? (tallest[k] - piece.Height) / 2.0 : piece.Look.Align == 2 ? tallest[k] - piece.Height : 0.0;
                    double x = left + offset + piece.Pen;
                    double y = baselines[k] + shift + rise;
                    double along = backward ? -x : x;
                    double wx = ox + along * cos - y * sin;
                    double wy = oy + along * sin + y * cos;
                    DisplayStyle look = new DisplayStyle(RgbOf(piece.Look, style, layerName), style.LineWeight, null);

                    DisplayText2 drawn = Text(piece.Face, piece.Words, wx, wy, piece.Height, turn, piece.Width, 0, 0, look, handle, layerName, piece.Slant, backward);
                    drawn.IsBold = piece.Bold;
                    drawn.IsItalic = piece.Italic;
                    parts.Add(drawn);

                    foreach (double level in Levels(piece.Look))
                    {
                        double ly = y + level * piece.Height;
                        double end = backward ? -(x + piece.Measured) : x + piece.Measured;
                        parts.Add(new DisplayPolyline2(
                            new[] { ox + along * cos - ly * sin, oy + along * sin + ly * cos, ox + end * cos - ly * sin, oy + end * sin + ly * cos },
                            false, look, handle, layerName));
                    }
                }
            }

            return parts;
        }

        /// <summary>The heights, over a run's height, its lines are drawn at: under, through and over it.</summary>
        private static IEnumerable<double> Levels(CadMTextLook look)
        {
            if (look.Underline)
            {
                yield return -UnderlineDrop;
            }

            if (look.Strikethrough)
            {
                yield return 0.5;
            }

            if (look.Overline)
            {
                yield return OverlineRise;
            }
        }

        /// <summary>The colour of a run: its true colour or colour of the index, ByLayer the layer's and ByBlock the MTEXT's; the MTEXT's where it gives none.</summary>
        private int RgbOf(CadMTextLook look, DisplayStyle style, string layerName)
        {
            if (look.TrueColor.HasValue)
            {
                return look.TrueColor.Value;
            }

            if (!look.ColorIndex.HasValue || look.ColorIndex.Value == 0)
            {
                return style.Rgb;
            }

            if (look.ColorIndex.Value >= 256)
            {
                LayerState layer = LayerNamed(layerName ?? "0");
                return layer != null ? layer.Rgb : style.Rgb;
            }

            return SafeRgb(DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)look.ColorIndex.Value));
        }

        /// <summary>The face of a font an MTEXT's code switches to, found as a style's font is.</summary>
        private CadFontFace FaceOfFont(string font, bool shx)
        {
            string key = "\\" + (shx ? "F" : "f") + font;

            if (!_fonts.TryGetValue(key, out TextFont found))
            {
                found = new TextFont(shx ? Resolve(font, null, null) : Resolve(null, null, font), 0.0, 1.0);
                _fonts[key] = found;
            }

            return found.Face;
        }

        /// <summary>
        /// How wide a run is: of an SHX font the advances of its letters; of a TrueType font as the canvas measures it
        /// (<see cref="CadTrueTypeFonts.MeasureWidth"/>), or 0.6 of the height a letter where nothing measures.
        /// </summary>
        private static double Measure(CadFontFace face, string words, double height, double widthFactor, bool bold, bool italic)
        {
            if (face.Shx != null)
            {
                double descent = height * face.Shx.Below / face.Shx.Above;
                ShxText.Strokes(face, words, 0.0, 0.0, height, 0.0, widthFactor, 0, 0, 0.0, false, descent, out double width);
                return width;
            }

            Func<string, bool, bool, string, double> measure = CadTrueTypeFonts.MeasureWidth;
            double unit = measure != null ? measure(face.Name, bold, italic, words) : words.Length * 0.6;
            return unit * height * widthFactor;
        }

        /// <summary>A run of an MTEXT measured and placed along its line.</summary>
        private sealed class Piece
        {
            public Piece(string words, CadMTextLook look, CadFontFace face, double height, double width, double slant, bool bold, bool italic, double pen, double measured)
            {
                Words = words;
                Look = look;
                Face = face;
                Height = height;
                Width = width;
                Slant = slant;
                Bold = bold;
                Italic = italic;
                Pen = pen;
                Measured = measured;
            }

            public string Words { get; }

            public CadMTextLook Look { get; }

            public CadFontFace Face { get; }

            public double Height { get; }

            public double Width { get; }

            public double Slant { get; }

            public bool Bold { get; }

            public bool Italic { get; }

            public double Pen { get; }

            public double Measured { get; }
        }
    }
}
