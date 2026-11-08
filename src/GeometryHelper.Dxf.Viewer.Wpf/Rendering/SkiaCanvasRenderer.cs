using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>How the canvas draws a space.</summary>
    public sealed class CanvasOptions
    {
        /// <summary>Gets or sets the colour of the canvas behind the drawing as 0xRRGGBB; black by default.</summary>
        public int Background { get; set; }

        /// <summary>Gets or sets the colour of the paper of a layout as 0xRRGGBB; white by default.</summary>
        public int Paper { get; set; } = 0xFFFFFF;

        /// <summary>Gets or sets the colour of selected entities and their grips as 0xRRGGBB.</summary>
        public int SelectionColor { get; set; } = 0x3D9BFF;

        /// <summary>Gets or sets whether line weights are drawn; off, every line is one pixel wide, as AutoCAD shows a drawing with lineweights hidden.</summary>
        public bool ShowLineWeights { get; set; }

        /// <summary>Gets or sets the handles of the selected entities; may be null.</summary>
        public IReadOnlyCollection<string> Selected { get; set; }

        /// <summary>Gets or sets the handle of the entity under the cursor, drawn lighter; may be null.</summary>
        public string Hover { get; set; }

        /// <summary>Gets or sets the name of the font the text is drawn in.</summary>
        public string FontName { get; set; } = "Arial";

        /// <summary>Gets or sets what white and black lines (AutoCAD's colour 7) are drawn as on a dark canvas, 0xRRGGBB: white by default.</summary>
        public int VisibleLineColor { get; set; } = 0xFFFFFF;

        /// <summary>
        /// Gets or sets the colour the entities of the layer <see cref="HiddenLayerName"/> are drawn in, 0xRRGGBB, as Global
        /// settings gives hidden lines; null, the default, for their own colour.
        /// </summary>
        public int? HiddenLineColor { get; set; }

        /// <summary>Gets or sets the layer of the hidden lines <see cref="HiddenLineColor"/> colours: Hidden by default, as a model drawing names it.</summary>
        public string HiddenLayerName { get; set; } = "Hidden";

        /// <summary>Gets or sets the colour of the grips as 0xRRGGBB: #3D9BFF by default.</summary>
        public int GripColor { get; set; } = 0x3D9BFF;
    }

    /// <summary>
    /// Draws a display list onto a SkiaSharp canvas: lines, arcs flattened to the pixel, fills, text, and the viewports of
    /// a layout clipped to their frames. It draws what the list holds and asks nothing of the drawing, so it can draw to
    /// a window's bitmap or to a PNG for a test.
    /// </summary>
    public sealed class SkiaCanvasRenderer : IDisposable
    {
        private readonly Dictionary<string, SKTypeface> _faces = new Dictionary<string, SKTypeface>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Draws a space of a document.</summary>
        /// <param name="canvas">The canvas to draw on.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space to draw.</param>
        /// <param name="view">What part of it is shown and at what zoom.</param>
        /// <param name="options">How to draw it.</param>
        public void Render(SKCanvas canvas, CadDocument document, CadSpace space, CadView view, CanvasOptions options)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            options = options ?? new CanvasOptions();
            canvas.Clear(Color(options.Background, 255));
            DisplayList2 list = document.GetDisplay(space);
            Affine2 m = view.DrawingToScreen;

            if (!space.IsModel)
            {
                DrawPaper(canvas, space.PaperBounds, m, options);
            }

            var selected = options.Selected == null ? null : new HashSet<string>(options.Selected, StringComparer.OrdinalIgnoreCase);
            DrawList(canvas, list, m, view.VisibleBox, options, selected, null, space.IsModel ? options.Background : options.Paper);
        }

        /// <summary>Releases the typefaces.</summary>
        public void Dispose()
        {
            foreach (SKTypeface face in _faces.Values)
            {
                face.Dispose();
            }

            _faces.Clear();
        }

        private static SKColor Color(int rgb, byte alpha) => new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);

        private static double Luminance(int rgb) => (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;

        /// <summary>
        /// White drawn on a light background would vanish, as would black on a dark one: AutoCAD's colour 7 turns black on
        /// white paper and white on black. Only white and black are turned; yellow on white paper and blue on black stay as
        /// they are, as AutoCAD shows them. On a dark background both are drawn in the colour Global settings gives visible
        /// lines, white by default.
        /// </summary>
        private static int Visible(int rgb, int background, int onDark = 0xFFFFFF)
        {
            if (Luminance(background) > 0.6 && Luminance(rgb) >= 0.98)
            {
                return 0x000000;
            }

            if (Luminance(background) < 0.4 && (Luminance(rgb) <= 0.02 || (Luminance(rgb) >= 0.98 && onDark != 0xFFFFFF)))
            {
                return onDark;
            }

            return rgb;
        }

        private void DrawPaper(SKCanvas canvas, DisplayBox sheet, Affine2 m, CanvasOptions options)
        {
            if (sheet.IsEmpty)
            {
                return;
            }

            float x0 = (float)m.X(sheet.MinX, sheet.MaxY);
            float y0 = (float)m.Y(sheet.MinX, sheet.MaxY);
            float x1 = (float)m.X(sheet.MaxX, sheet.MinY);
            float y1 = (float)m.Y(sheet.MaxX, sheet.MinY);
            using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 90), IsAntialias = true })
            {
                canvas.DrawRect(x0 + 4, y0 + 4, x1 - x0, y1 - y0, shadow);
            }

            using (var paper = new SKPaint { Color = Color(options.Paper, 255) })
            {
                canvas.DrawRect(x0, y0, x1 - x0, y1 - y0, paper);
            }
        }

        private void DrawList(SKCanvas canvas, DisplayList2 list, Affine2 m, DisplayBox visible, CanvasOptions options, HashSet<string> selected, ISet<string> frozen, int background)
        {
            double zoom = m.Scale;

            if (zoom <= 0.0)
            {
                return;
            }

            using (var stroke = new SKPaint { IsStroke = true, IsAntialias = true, StrokeCap = SKStrokeCap.Butt, StrokeJoin = SKStrokeJoin.Miter })
            using (var fill = new SKPaint { IsStroke = false, IsAntialias = true })
            {
                foreach (DisplayItem2 item in list.Items)
                {
                    if (!item.Bounds.Intersects(visible))
                    {
                        continue;
                    }

                    if (frozen != null && frozen.Contains(item.Layer ?? string.Empty))
                    {
                        continue;
                    }

                    bool isSelected = selected != null && item.Handle != null && selected.Contains(item.Handle);
                    bool isHover = options.Hover != null && string.Equals(item.Handle, options.Hover, StringComparison.OrdinalIgnoreCase);
                    DrawItem(canvas, item, m, zoom, options, stroke, fill, isSelected, isHover, background);
                }
            }
        }

        private void DrawItem(SKCanvas canvas, DisplayItem2 item, Affine2 m, double zoom, CanvasOptions options, SKPaint stroke, SKPaint fill, bool isSelected, bool isHover, int background)
        {
            int own = options.HiddenLineColor.HasValue && item.Layer != null && string.Equals(item.Layer, options.HiddenLayerName, StringComparison.OrdinalIgnoreCase)
                ? options.HiddenLineColor.Value
                : item.Style.Rgb;
            int rgb = isSelected ? options.SelectionColor : Visible(own, background, options.VisibleLineColor);
            byte alpha = isHover && !isSelected ? (byte)170 : (byte)255;
            SKColor color = Color(rgb, alpha);
            float width = options.ShowLineWeights ? (float)Math.Max(1.0, item.Style.LineWeight * 96.0 / 25.4) : 1f;
            stroke.Color = color;
            stroke.StrokeWidth = isSelected || isHover ? Math.Max(width, 2f) : width;
            stroke.PathEffect = null;
            double[] dashes = item.Style.Dashes;

            if (dashes != null && dashes.Length > 0 && !isSelected)
            {
                stroke.PathEffect = DashEffect(dashes, zoom);
            }
            else if (isSelected)
            {
                stroke.PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0f);
            }

            switch (item)
            {
                case DisplayPolyline2 chain:
                    DrawChain(canvas, chain, m, stroke, fill, color);
                    break;
                case DisplaySegments2 segments:
                    DrawSegments(canvas, segments, m, stroke);
                    break;
                case DisplayArc2 arc:
                    DrawArc(canvas, arc, m, zoom, stroke);
                    break;
                case DisplayFill2 area:
                    DrawFill(canvas, area, m, fill, options, background, item.Style.Rgb, isSelected, color);
                    break;
                case DisplayText2 text:
                    DrawText(canvas, text, m, zoom, fill, color, options);
                    break;
                case DisplayViewport2 viewport:
                    DrawViewport(canvas, viewport, m, options, stroke, background, isSelected);
                    break;
            }

            stroke.PathEffect?.Dispose();
            stroke.PathEffect = null;
        }

        private static SKPathEffect DashEffect(double[] dashes, double zoom)
        {
            var intervals = new float[dashes.Length % 2 == 0 ? dashes.Length : dashes.Length * 2];
            double total = 0.0;

            for (int i = 0; i < intervals.Length; i++)
            {
                double length = dashes[i % dashes.Length] * zoom;
                intervals[i] = (float)Math.Max(length, i % 2 == 0 ? 0.5 : 1.0);
                total += intervals[i];
            }

            return total < 3.0 ? null : SKPathEffect.CreateDash(intervals, 0f);
        }

        private static void DrawChain(SKCanvas canvas, DisplayPolyline2 chain, Affine2 m, SKPaint stroke, SKPaint fill, SKColor color)
        {
            IReadOnlyList<double> xy = chain.Xy;
            int n = chain.Count;

            if (n == 1)
            {
                fill.Color = color;
                canvas.DrawCircle((float)m.X(xy[0], xy[1]), (float)m.Y(xy[0], xy[1]), 1.5f, fill);
                return;
            }

            if (n == 2 && !chain.Closed)
            {
                canvas.DrawLine((float)m.X(xy[0], xy[1]), (float)m.Y(xy[0], xy[1]), (float)m.X(xy[2], xy[3]), (float)m.Y(xy[2], xy[3]), stroke);
                return;
            }

            using (var path = new SKPath())
            {
                path.MoveTo((float)m.X(xy[0], xy[1]), (float)m.Y(xy[0], xy[1]));

                for (int i = 1; i < n; i++)
                {
                    path.LineTo((float)m.X(xy[2 * i], xy[2 * i + 1]), (float)m.Y(xy[2 * i], xy[2 * i + 1]));
                }

                if (chain.Closed)
                {
                    path.Close();
                }

                canvas.DrawPath(path, stroke);
            }
        }

        private static void DrawSegments(SKCanvas canvas, DisplaySegments2 segments, Affine2 m, SKPaint stroke)
        {
            IReadOnlyList<double> xy = segments.Xy;

            using (var path = new SKPath())
            {
                for (int i = 0; i + 3 < xy.Count; i += 4)
                {
                    float x0 = (float)m.X(xy[i], xy[i + 1]);
                    float y0 = (float)m.Y(xy[i], xy[i + 1]);
                    float x1 = (float)m.X(xy[i + 2], xy[i + 3]);
                    float y1 = (float)m.Y(xy[i + 2], xy[i + 3]);

                    if (Math.Abs(x1 - x0) < 0.5f && Math.Abs(y1 - y0) < 0.5f)
                    {
                        // A dot of the pattern, a segment of no length, is drawn as a pixel.
                        path.AddRect(new SKRect(x0 - 0.5f, y0 - 0.5f, x0 + 0.5f, y0 + 0.5f));
                        continue;
                    }

                    path.MoveTo(x0, y0);
                    path.LineTo(x1, y1);
                }

                canvas.DrawPath(path, stroke);
            }
        }

        private static void DrawArc(SKCanvas canvas, DisplayArc2 arc, Affine2 m, double zoom, SKPaint stroke)
        {
            double tolerance = 0.3 / zoom;
            double[] xy = arc.Flatten(tolerance);
            int n = xy.Length / 2;

            using (var path = new SKPath())
            {
                path.MoveTo((float)m.X(xy[0], xy[1]), (float)m.Y(xy[0], xy[1]));

                for (int i = 1; i < n; i++)
                {
                    path.LineTo((float)m.X(xy[2 * i], xy[2 * i + 1]), (float)m.Y(xy[2 * i], xy[2 * i + 1]));
                }

                if (arc.IsCircle)
                {
                    path.Close();
                }

                canvas.DrawPath(path, stroke);
            }
        }

        private static void DrawFill(SKCanvas canvas, DisplayFill2 area, Affine2 m, SKPaint fill, CanvasOptions options, int background, int styleRgb, bool isSelected, SKColor color)
        {
            using (var path = new SKPath { FillType = SKPathFillType.EvenOdd })
            {
                foreach (double[] ring in area.Rings)
                {
                    int n = ring.Length / 2;

                    if (n < 3)
                    {
                        continue;
                    }

                    path.MoveTo((float)m.X(ring[0], ring[1]), (float)m.Y(ring[0], ring[1]));

                    for (int i = 1; i < n; i++)
                    {
                        path.LineTo((float)m.X(ring[2 * i], ring[2 * i + 1]), (float)m.Y(ring[2 * i], ring[2 * i + 1]));
                    }

                    path.Close();
                }

                fill.Color = area.IsMask ? Color(options.Paper == 0 ? background : background, 255) : (isSelected ? new SKColor(color.Red, color.Green, color.Blue, 140) : color);
                canvas.DrawPath(path, fill);
            }
        }

        /// <summary>
        /// The typeface of a text: the family a TrueType style names; the file a style names, from the Fonts folder of Windows,
        /// or the family of its name where it is not there; the canvas's own for an SHX font, which SkiaSharp cannot draw, and
        /// for none.
        /// </summary>
        private SKTypeface Face(string name, string fallback)
        {
            string key = (name ?? string.Empty) + "|" + fallback;

            if (!_faces.TryGetValue(key, out SKTypeface face))
            {
                string extension = name == null ? string.Empty : Path.GetExtension(name).ToLowerInvariant();

                if (name == null || extension == ".shx" || (extension.Length > 0 && extension != ".ttf" && extension != ".ttc" && extension != ".otf"))
                {
                    face = SKTypeface.FromFamilyName(fallback);
                }
                else if (extension.Length > 0)
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Path.GetFileName(name));
                    face = (File.Exists(path) ? SKTypeface.FromFile(path) : null) ?? SKFontManager.Default.MatchFamily(Path.GetFileNameWithoutExtension(name)) ?? SKTypeface.FromFamilyName(fallback);
                }
                else
                {
                    face = SKFontManager.Default.MatchFamily(name) ?? SKTypeface.FromFamilyName(fallback);
                }

                face = face ?? SKTypeface.Default;
                _faces[key] = face;
            }

            return face;
        }

        private void DrawText(SKCanvas canvas, DisplayText2 text, Affine2 m, double zoom, SKPaint fill, SKColor color, CanvasOptions options)
        {
            double heightPx = text.Height * zoom;

            if (heightPx < 2.5)
            {
                return;
            }

            double cos = Math.Cos(text.Rotation);
            double sin = Math.Sin(text.Rotation);
            double angle = Math.Atan2(m.B * cos + m.D * sin, m.A * cos + m.C * sin);
            float px = (float)m.X(text.X, text.Y);
            float py = (float)m.Y(text.X, text.Y);

            using (var font = new SKFont(Face(text.FontName, options.FontName), (float)(heightPx / 0.72)))
            {
                font.ScaleX = (float)text.WidthFactor;
                font.SkewX = (float)-Math.Tan(text.Oblique);
                fill.Color = color;
                string[] lines = text.Text.Split('\n');
                int n = lines.Length;
                double lineStep = heightPx * 1.25;
                double top = heightPx;
                double bottom = -(n - 1) * lineStep;
                double shift = text.Vertical == 0 ? 0.0 : text.Vertical == 1 ? -bottom : text.Vertical == 2 ? -(top + bottom) / 2.0 : -top;

                canvas.Save();
                canvas.Translate(px, py);
                canvas.RotateRadians((float)angle);

                if (text.IsBackward)
                {
                    canvas.Scale(-1f, 1f);
                }

                for (int i = 0; i < n; i++)
                {
                    float width = font.MeasureText(lines[i]);
                    float x = text.Horizontal == 0 ? 0f : text.Horizontal == 1 ? -width / 2f : -width;
                    float y = (float)(-shift + i * lineStep);
                    canvas.DrawText(lines[i], x, y, SKTextAlign.Left, font, fill);
                }

                canvas.Restore();
            }
        }

        private void DrawViewport(SKCanvas canvas, DisplayViewport2 viewport, Affine2 m, CanvasOptions options, SKPaint stroke, int background, bool isSelected)
        {
            float x0 = (float)m.X(viewport.Bounds.MinX, viewport.Bounds.MaxY);
            float y0 = (float)m.Y(viewport.Bounds.MinX, viewport.Bounds.MaxY);
            float x1 = (float)m.X(viewport.Bounds.MaxX, viewport.Bounds.MinY);
            float y1 = (float)m.Y(viewport.Bounds.MaxX, viewport.Bounds.MinY);
            Affine2 inner = viewport.ModelToPaper.Then(m);
            canvas.Save();
            canvas.ClipRect(new SKRect(x0, y0, x1, y1));
            int paperBackground = options.Paper;
            double rotation = viewport.Twist;
            double cos = Math.Cos(-rotation);
            double sin = Math.Sin(-rotation);
            double halfW = viewport.Width / 2.0 / viewport.Scale;
            double halfH = viewport.Height / 2.0 / viewport.Scale;
            double reach = Math.Sqrt(halfW * halfW + halfH * halfH);
            var visible = new DisplayBox(viewport.ViewCenterX - reach, viewport.ViewCenterY - reach, viewport.ViewCenterX + reach, viewport.ViewCenterY + reach);
            DrawList(canvas, viewport.Model, inner, visible, options, null, viewport.FrozenLayers, paperBackground);
            canvas.Restore();

            if (cos == 0.0 && sin == 0.0)
            {
                return;
            }

            stroke.Color = isSelected ? Color(options.SelectionColor, 255) : Color(Visible(0x808080, paperBackground), 255);
            stroke.StrokeWidth = isSelected ? 2f : 1f;
            canvas.DrawRect(x0, y0, x1 - x0, y1 - y0, stroke);
        }
    }
}
