using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>One thing a vector picture draws, with the colour it is drawn in.</summary>
    internal abstract class CadVectorItem
    {
        protected CadVectorItem(int rgb)
        {
            Rgb = rgb & 0xFFFFFF;
        }

        /// <summary>The colour as 0xRRGGBB, as it shows on a white sheet: white is already black.</summary>
        public int Rgb { get; }
    }

    /// <summary>A line of straight pieces: a line, a polyline, an arc taken in short chords, the stroke of a letter of an SHX font.</summary>
    internal sealed class CadVectorStroke : CadVectorItem
    {
        public CadVectorStroke(double[] xy, bool closed, int rgb, double weight, double[] dashes)
            : base(rgb)
        {
            Xy = xy;
            Closed = closed;
            Weight = weight;
            Dashes = dashes;
        }

        /// <summary>The vertices as X and Y in turn, in drawing units.</summary>
        public double[] Xy { get; }

        /// <summary>Whether the last vertex joins the first.</summary>
        public bool Closed { get; }

        /// <summary>The line weight in millimetres; nought for the thinnest line.</summary>
        public double Weight { get; }

        /// <summary>The dashes in drawing units, on and off in turn; null for a continuous line.</summary>
        public double[] Dashes { get; }
    }

    /// <summary>An area filled with its colour by the even-odd rule: a ring inside another is a hole.</summary>
    internal sealed class CadVectorFill : CadVectorItem
    {
        public CadVectorFill(IReadOnlyList<double[]> rings, int rgb)
            : base(rgb)
        {
            Rings = rings;
        }

        /// <summary>The rings, each as X and Y in turn, closed implicitly.</summary>
        public IReadOnlyList<double[]> Rings { get; }
    }

    /// <summary>
    /// One line of words in a font of the system, set on its baseline. A text of several lines is several of these, each
    /// already put where its baseline goes.
    /// </summary>
    internal sealed class CadVectorText : CadVectorItem
    {
        public CadVectorText(
            string text,
            double x,
            double y,
            double height,
            double rotation,
            int align,
            string face,
            bool bold,
            bool italic,
            double widthFactor,
            double oblique,
            int rgb
        )
            : base(rgb)
        {
            Text = text;
            X = x;
            Y = y;
            Height = height;
            Rotation = rotation;
            Align = align;
            Face = face;
            Bold = bold;
            Italic = italic;
            WidthFactor = widthFactor;
            Oblique = oblique;
        }

        /// <summary>The words of the line, with no line feed.</summary>
        public string Text { get; }

        /// <summary>The X of the point on the baseline the line is aligned to.</summary>
        public double X { get; }

        /// <summary>The Y of the point on the baseline the line is aligned to.</summary>
        public double Y { get; }

        /// <summary>The height of a capital letter, in drawing units.</summary>
        public double Height { get; }

        /// <summary>The angle of the baseline in radians, counter-clockwise.</summary>
        public double Rotation { get; }

        /// <summary>Where the point lies along the line: 0 at its start, 1 at its middle, 2 at its end.</summary>
        public int Align { get; }

        /// <summary>The family of the font: Arial.</summary>
        public string Face { get; }

        /// <summary>Whether the letters are bold.</summary>
        public bool Bold { get; }

        /// <summary>Whether the letters are italic.</summary>
        public bool Italic { get; }

        /// <summary>The width of a letter relative to the font's own; 1 for none.</summary>
        public double WidthFactor { get; }

        /// <summary>How far the letters lean, in radians clockwise from upright.</summary>
        public double Oblique { get; }

        /// <summary>The height of the font's em, which a capital letter is 0.72 of, as the canvas sets it.</summary>
        public double Em => Height / 0.72;
    }

    /// <summary>
    /// What the vector writers (WMFOUT, PSOUT, SVGOUT) put on their pages: the display list of a space (or the objects
    /// asked for), flattened to lines, fills and single lines of text with their colours, line weights and dashes resolved,
    /// in drawing units with Y up. A colour that would vanish on the white sheet (white, within 2 per cent of it) is black,
    /// as it plots; a mask (a wipeout) is a white fill. Left out: the pictures of images, the model that the viewports of a
    /// layout show, the grey ground of a field, and the tint of a hatch's loops. The part of a block outside its XCLIP is
    /// left out by its vertices: a line is cut where its vertices leave the clip, a fill or a text is kept where its box's
    /// middle is inside.
    /// </summary>
    internal sealed class CadVectorPicture
    {
        /// <summary>The cell of a line step between two lines of a text, as the canvas sets it: 1.25 capital heights.</summary>
        private const double LineStep = 1.25;

        private readonly List<CadVectorItem> _items = new List<CadVectorItem>();

        private CadVectorPicture() { }

        /// <summary>The things the picture draws, in the order of the drawing, the first at the bottom.</summary>
        public IReadOnlyList<CadVectorItem> Items => _items;

        /// <summary>The box of the picture, in drawing units: the window asked for, or the box that holds what is drawn; empty where nothing is.</summary>
        public DisplayBox Box { get; private set; } = DisplayBox.Empty;

        /// <summary>The number of items of the display list left out for want of a vector form: images and viewports.</summary>
        public int Skipped { get; private set; }

        /// <summary>Whether nothing is drawn.</summary>
        public bool IsEmpty => _items.Count == 0 || Box.IsEmpty;

        /// <summary>Makes the picture of a space.</summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space.</param>
        /// <param name="handles">The handles of the objects drawn; null or empty for all of them.</param>
        /// <param name="window">The part of the space drawn, in drawing units; an empty box for the box of what is drawn.</param>
        /// <returns>The picture.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the space is null.</exception>
        public static CadVectorPicture Of(
            CadDocument document,
            CadSpace space,
            IReadOnlyCollection<string> handles,
            DisplayBox window
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            var picture = new CadVectorPicture();
            HashSet<string> wanted =
                handles == null || handles.Count == 0 ? null : new HashSet<string>(handles, StringComparer.Ordinal);
            DisplayBox box = DisplayBox.Empty;
            var rays = new List<DisplayRay2>();

            foreach (DisplayItem2 item in document.GetDisplay(space).Items)
            {
                if (
                    item.IsNotPlotted
                    || (wanted != null && (item.Handle == null || !wanted.Contains(item.Handle)))
                    || (!window.IsEmpty && !(item is DisplayRay2) && !item.Bounds.Intersects(window))
                )
                {
                    continue;
                }

                if (item is DisplayViewport2 || item is DisplayImage2)
                {
                    picture.Skipped++;
                    continue;
                }

                if (item is DisplayRay2 ray)
                {
                    rays.Add(ray);
                    continue;
                }

                box = box.Union(item.VisibleBounds);
                picture.Add(item);
            }

            picture.Box = window.IsEmpty ? box : window;

            // A line without end runs as far as the picture does.
            foreach (DisplayRay2 ray in rays)
            {
                if (
                    !picture.Box.IsEmpty
                    && ray.TryClip(picture.Box, out double x0, out double y0, out double x1, out double y1)
                )
                {
                    picture._items.Add(
                        new CadVectorStroke(
                            new[] { x0, y0, x1, y1 },
                            false,
                            Visible(ray.Style),
                            WeightOf(ray.Style),
                            ray.Style?.Dashes
                        )
                    );
                }
            }

            return picture;
        }

        private static double WeightOf(DisplayStyle style) => style == null ? 0.0 : Math.Max(0.0, style.LineWeight);

        private static double Luminance(int rgb) =>
            (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;

        /// <summary>The colour an item is drawn in on the white sheet: its own, and black for white and what is within 2 per cent of it.</summary>
        private static int Visible(DisplayStyle style)
        {
            int rgb = style == null ? 0 : style.Rgb;
            return Luminance(rgb) >= 0.98 ? 0x000000 : rgb;
        }

        private void Add(DisplayItem2 item)
        {
            DisplayClipRegion clip = item.BlockClip;
            DisplayStyle style = item.Style;
            int rgb = Visible(style);
            double weight = WeightOf(style);

            switch (item)
            {
                case DisplayPolyline2 chain:
                    AddStroke(chain.Xy, chain.Closed, rgb, weight, style?.Dashes, clip);
                    break;
                case DisplaySegments2 segments:
                    IReadOnlyList<double> xy = segments.Xy;

                    for (int i = 0; i + 3 < xy.Count; i += 4)
                    {
                        AddStroke(
                            new[] { xy[i], xy[i + 1], xy[i + 2], xy[i + 3] },
                            false,
                            rgb,
                            weight,
                            style?.Dashes,
                            clip
                        );
                    }

                    break;
                case DisplayArc2 arc:
                    AddStroke(arc.Flatten(Math.Max(arc.Radius * 1E-4, 1E-9)), false, rgb, weight, style?.Dashes, clip);
                    break;
                case DisplayFill2 fill:
                    AddFill(fill, clip);
                    break;
                case DisplayText2 text:
                    AddText(text, rgb, weight, clip);
                    break;
            }
        }

        /// <summary>A line as it is, or cut into the runs of its vertices that lie inside a clip.</summary>
        private void AddStroke(
            IReadOnlyList<double> xy,
            bool closed,
            int rgb,
            double weight,
            double[] dashes,
            DisplayClipRegion clip
        )
        {
            if (xy.Count < 4)
            {
                return;
            }

            if (clip == null)
            {
                var all = new double[xy.Count];

                for (int i = 0; i < all.Length; i++)
                {
                    all[i] = xy[i];
                }

                _items.Add(new CadVectorStroke(all, closed, rgb, weight, dashes));
                return;
            }

            var run = new List<double>();
            int count = xy.Count / 2;

            for (int i = 0; i < count; i++)
            {
                if (clip.Contains(xy[2 * i], xy[2 * i + 1]))
                {
                    run.Add(xy[2 * i]);
                    run.Add(xy[2 * i + 1]);
                }
                else
                {
                    EndRun(run, rgb, weight, dashes);
                }
            }

            if (closed && run.Count > 0 && clip.Contains(xy[0], xy[1]) && run.Count == xy.Count)
            {
                _items.Add(new CadVectorStroke(run.ToArray(), true, rgb, weight, dashes));
                return;
            }

            EndRun(run, rgb, weight, dashes);
        }

        private void EndRun(List<double> run, int rgb, double weight, double[] dashes)
        {
            if (run.Count >= 4)
            {
                _items.Add(new CadVectorStroke(run.ToArray(), false, rgb, weight, dashes));
            }

            run.Clear();
        }

        private void AddFill(DisplayFill2 fill, DisplayClipRegion clip)
        {
            if (clip != null)
            {
                DisplayBox bounds = fill.Bounds;

                if (
                    bounds.IsEmpty
                    || !clip.Contains((bounds.MinX + bounds.MaxX) / 2.0, (bounds.MinY + bounds.MaxY) / 2.0)
                )
                {
                    return;
                }
            }

            if (fill.Rings.Count > 0)
            {
                _items.Add(new CadVectorFill(fill.Rings, fill.IsMask ? 0xFFFFFF : Visible(fill.Style)));
            }
        }

        private void AddText(DisplayText2 text, int rgb, double weight, DisplayClipRegion clip)
        {
            if (clip != null)
            {
                DisplayBox bounds = text.Bounds;

                if (
                    bounds.IsEmpty
                    || !clip.Contains((bounds.MinX + bounds.MaxX) / 2.0, (bounds.MinY + bounds.MaxY) / 2.0)
                )
                {
                    return;
                }
            }

            if (text.IsQuick)
            {
                DisplayBox box = text.Bounds;
                _items.Add(
                    new CadVectorStroke(
                        new[] { box.MinX, box.MinY, box.MaxX, box.MinY, box.MaxX, box.MaxY, box.MinX, box.MaxY },
                        true,
                        rgb,
                        weight,
                        null
                    )
                );
                return;
            }

            if (text.Parts != null)
            {
                foreach (DisplayItem2 part in text.Parts)
                {
                    Add(part);
                }

                return;
            }

            if (text.Strokes != null)
            {
                foreach (double[] stroke in text.Strokes)
                {
                    AddStroke(stroke, false, rgb, weight, null, null);
                }

                return;
            }

            string[] lines = text.Text.Split('\n');
            int n = lines.Length;
            double height = text.Height;
            double step = LineStep * height;
            double bottom = -(n - 1) * step;
            double shift =
                text.Vertical == 0 ? 0.0
                : text.Vertical == 1 ? -bottom
                : text.Vertical == 2 ? -(height + bottom) / 2.0
                : -height;
            double sin = Math.Sin(text.Rotation);
            double cos = Math.Cos(text.Rotation);
            string face = FaceOf(text.FontName);

            for (int i = 0; i < n; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (line.Length == 0)
                {
                    continue;
                }

                // The baseline of line i lies shift - i * step above the point, along the text's up.
                double up = shift - i * step;
                _items.Add(
                    new CadVectorText(
                        line,
                        text.X - up * sin,
                        text.Y + up * cos,
                        height,
                        text.Rotation,
                        text.Horizontal,
                        face,
                        text.IsBold,
                        text.IsItalic,
                        text.WidthFactor,
                        text.Oblique,
                        rgb
                    )
                );
            }
        }

        /// <summary>The family a text's font names: the family of a TrueType style, the family of the file of one, Arial for an SHX font or none.</summary>
        private static string FaceOf(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return "Arial";
            }

            string extension = System.IO.Path.GetExtension(fontName).ToLowerInvariant();

            if (extension == ".ttf" || extension == ".ttc" || extension == ".otf")
            {
                return CadTrueTypeFonts.FamilyOf(fontName) ?? System.IO.Path.GetFileNameWithoutExtension(fontName);
            }

            return extension.Length > 0 ? "Arial" : fontName;
        }
    }
}
