using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The words of an MTEXT drawn as AutoCAD lays them out (<see cref="CadMTextLayout"/>).</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>How far past the words the frame of an MTEXT with no mask stands, over its height, less one: AutoCAD's 1.5.</summary>
        private const double FrameFactor = 1.5;

        /// <summary>
        /// The layout of an MTEXT's words in its own frame, in the fonts of the drawing: wrapped at its width, set down its
        /// lines and columns and put at its attachment point. What draws, grips and explodes an MTEXT reads it.
        /// </summary>
        internal CadMTextLayout LayoutOf(DxfMText text)
        {
            CadMTextContent content = CadMTextContent.Parse(text.Contents, text.TextHeight, WidthFactorOf(text.TextStyleName));
            CadFontFace styleFace = FaceOf(text.TextStyleName);
            return CadMTextLayout.Of(
                text,
                content,
                look => look.Font == null ? styleFace : FaceOfFont(look.Font, look.IsShx),
                WidthFactorOf(text.TextStyleName),
                ObliqueOf(text.TextStyleName));
        }

        /// <summary>
        /// The layout of the words of an MTEXT being edited, as <see cref="LayoutOf"/> lays out the MTEXT's own, with where
        /// each place of the editor stands (<see cref="CadMTextLayout.OfEditor"/>).
        /// </summary>
        internal CadMTextLayout EditorLayoutOf(DxfMText text, CadMTextContent content)
        {
            CadFontFace styleFace = FaceOf(text.TextStyleName);
            return CadMTextLayout.OfEditor(
                text,
                content,
                look => look.Font == null ? styleFace : FaceOfFont(look.Font, look.IsShx),
                WidthFactorOf(text.TextStyleName),
                ObliqueOf(text.TextStyleName));
        }

        /// <summary>
        /// The parts of a layout of an MTEXT being edited, as the canvas draws an MTEXT (<see cref="MTextParts"/>), in the
        /// MTEXT's own frame: its location at nought, x along its direction and y up. Letters of no colour of their own are
        /// <paramref name="rgb"/>, those coded ByLayer <paramref name="layerRgb"/>.
        /// </summary>
        internal List<DisplayItem2> EditorParts(DxfMText text, CadMTextLayout layout, int rgb, int layerRgb)
        {
            GeoPoint3 at = text.Location;
            GeoVector3 direction = text.Direction;
            double turn = Math.Atan2(direction.Y, direction.X);
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            var own = new Affine2(cos, -sin, sin, cos, -((cos * at.X) + (sin * at.Y)), (sin * at.X) - (cos * at.Y));
            return MTextParts(text, layout, new Frame(own), new DisplayStyle(rgb, 0.25, null), null, null, layerRgb);
        }

        /// <summary>
        /// The parts an MTEXT is drawn as (<see cref="DisplayText2.Parts"/>): its mask first, its frame, then each run of each
        /// line a text of its own where the layout puts it, in its font (bold and italic faces of a TrueType font), height,
        /// width factor, slant and colour, and the lines under, over and through the runs and of the stacks.
        /// </summary>
        private List<DisplayItem2> MTextParts(DxfMText text, CadMTextLayout layout, Frame frame, DisplayStyle style, string handle, string layerName, int? layerRgb = null)
        {
            var parts = new List<DisplayItem2>(layout.Pieces.Count + layout.Strokes.Count + 2);
            Affine2 m = frame.Matrix;
            double scale = m.Scale;
            GeoVector3 direction = text.Direction;
            double turn = Orient(m, Math.Atan2(direction.Y, direction.X), false, false, out bool backward);
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            GeoPoint3 at = text.Location;
            double ox = m.X(at.X, at.Y);
            double oy = m.Y(at.X, at.Y);
            double flip = backward ? -scale : scale;
            var plain = new DisplayStyle(style.Rgb, style.LineWeight, null);

            if (text.BackgroundFill || text.ShowBorders)
            {
                double grow = ((text.BackgroundFill && text.BackgroundScaleFactor > 0.0 ? text.BackgroundScaleFactor : FrameFactor) - 1.0) * text.TextHeight;
                double[] box = layout.TextBox;
                double x0 = (box[0] - grow) * flip;
                double x1 = (box[2] + grow) * flip;
                double y0 = (box[1] - grow) * scale;
                double y1 = (box[3] + grow) * scale;
                var ring = new[]
                {
                    ox + (x0 * cos) - (y0 * sin), oy + (x0 * sin) + (y0 * cos),
                    ox + (x1 * cos) - (y0 * sin), oy + (x1 * sin) + (y0 * cos),
                    ox + (x1 * cos) - (y1 * sin), oy + (x1 * sin) + (y1 * cos),
                    ox + (x0 * cos) - (y1 * sin), oy + (x0 * sin) + (y1 * cos),
                };

                if (text.BackgroundFill)
                {
                    DisplayStyle mask = text.UseBackgroundColor ? plain : new DisplayStyle(SafeRgb(text.BackgroundFillColor), style.LineWeight, null);
                    parts.Add(new DisplayFill2(new[] { ring }, text.UseBackgroundColor, mask, handle, layerName));
                }

                if (text.ShowBorders)
                {
                    parts.Add(new DisplayPolyline2(ring, true, plain, handle, layerName));
                }
            }

            CadMTextLook last = null;
            DisplayStyle lastStyle = plain;

            foreach (CadMTextLayout.Piece piece in layout.Pieces)
            {
                if (!ReferenceEquals(piece.Look, last))
                {
                    last = piece.Look;
                    int rgb = layerRgb.HasValue && last.ColorIndex >= 256 && !last.TrueColor.HasValue ? layerRgb.Value : RgbOf(last, style, layerName);
                    lastStyle = rgb == style.Rgb ? plain : new DisplayStyle(rgb, style.LineWeight, null);
                }

                double x = piece.X * flip;
                double y = piece.Y * scale;
                double wx = ox + (x * cos) - (y * sin);
                double wy = oy + (x * sin) + (y * cos);
                double h = piece.Height * scale;
                DisplayText2 drawn;

                if (piece.Face.Shx != null)
                {
                    drawn = Text(piece.Face, piece.Text, wx, wy, h, turn, piece.WidthFactor, 0, 0, lastStyle, handle, layerName, piece.Slant, backward);
                }
                else
                {
                    // The canvas sets its letters by the em, the layout by the file's capital: the width factor makes the two one width.
                    drawn = new DisplayText2(piece.Text, wx, wy, h, turn, piece.WidthFactor * piece.Ink.DrawFactor, 0, 0, lastStyle, handle, layerName, piece.Face.Name, piece.Slant, backward);
                    drawn.IsBold = piece.Bold;
                    drawn.IsItalic = piece.Italic;

                    if (piece.Ink.IsRead && piece.Advance > 0.0)
                    {
                        drawn.SetMeasure(piece.Advance * scale, 0.0);
                    }
                }

                parts.Add(drawn);
            }

            last = null;

            foreach (CadMTextLayout.Stroke stroke in layout.Strokes)
            {
                if (!ReferenceEquals(stroke.Look, last))
                {
                    last = stroke.Look;
                    int rgb = layerRgb.HasValue && last.ColorIndex >= 256 && !last.TrueColor.HasValue ? layerRgb.Value : RgbOf(last, style, layerName);
                    lastStyle = rgb == style.Rgb ? plain : new DisplayStyle(rgb, style.LineWeight, null);
                }

                double x0 = stroke.X0 * flip;
                double y0 = stroke.Y0 * scale;
                double x1 = stroke.X1 * flip;
                double y1 = stroke.Y1 * scale;
                parts.Add(new DisplayPolyline2(
                    new[] { ox + (x0 * cos) - (y0 * sin), oy + (x0 * sin) + (y0 * cos), ox + (x1 * cos) - (y1 * sin), oy + (x1 * sin) + (y1 * cos) },
                    false, lastStyle, handle, layerName));
            }

            return parts;
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
    }
}
