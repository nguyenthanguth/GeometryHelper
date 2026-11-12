using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
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

        /// <summary>Gets or sets the colour of the canvas behind a block open in the Block Editor as 0xRRGGBB; #3B4049 by default, so that the editor is never taken for model space.</summary>
        public int BlockEditorBackground { get; set; } = 0x3B4049;

        /// <summary>The colour behind a space: the Block Editor's, the canvas's of model space, or the paper of a layout.</summary>
        internal int BackgroundOf(CadSpace space) =>
            space.IsModel ? (space.IsBlockEditor ? BlockEditorBackground : Background) : Paper;

        /// <summary>Gets or sets the colour of selected entities and their grips as 0xRRGGBB.</summary>
        public int SelectionColor { get; set; } = 0x3D9BFF;

        /// <summary>Gets or sets whether line weights are drawn; off, every line is one pixel wide, as AutoCAD shows a drawing with lineweights hidden.</summary>
        public bool ShowLineWeights { get; set; }

        /// <summary>Gets or sets the handles of the selected entities, drawn in the selection colour and at least 2 pixels wide; may be null.</summary>
        public IReadOnlyCollection<string> Selected { get; set; }

        /// <summary>
        /// Gets or sets the handle of the entity under the cursor, drawn at least 2 pixels wide in its colour taken halfway to
        /// the selection colour, so that a white line under the cursor turns a light blue; may be null.
        /// </summary>
        public string Hover { get; set; }

        /// <summary>Gets or sets the name of the font the text is drawn in.</summary>
        public string FontName { get; set; } = "Arial";

        /// <summary>Gets or sets what white and black lines (AutoCAD's colour 7) are drawn as on a dark canvas, 0xRRGGBB: white by default.</summary>
        public int VisibleLineColor { get; set; } = 0xFFFFFF;

        /// <summary>Gets or sets the colour of the grips as 0xRRGGBB: #3D9BFF by default.</summary>
        public int GripColor { get; set; } = 0x3D9BFF;

        /// <summary>
        /// Gets or sets the colour of a dynamic block's custom grips as 0xRRGGBB, AutoCAD's GRIPDYNCOLOR: #00BFFF by default,
        /// the colour of index 140 that AutoCAD 2021 starts with.
        /// </summary>
        public int CustomGripColor { get; set; } = 0x00BFFF;
    }

    /// <summary>
    /// Draws a display list onto a SkiaSharp canvas: lines, arcs flattened to the pixel, fills, text, and the viewports of
    /// a layout clipped to their frames or, for a clipped viewport, the outline of its boundary. It draws what the list holds and asks nothing of the drawing, so it can draw to
    /// a window's bitmap or to a PNG for a test. It draws only the items the view shows, found through the space's
    /// <see cref="PickIndex"/>, and spares the detail no eye could see: an item under half a pixel is one pixel of its colour,
    /// a text under 3 pixels high is left out, and a hatch whose lines come closer than 2 pixels is filled with a tint of
    /// its colour. A selected or hovered entity is always drawn whole.
    /// </summary>
    public sealed class SkiaCanvasRenderer : IDisposable
    {
        /// <summary>The farthest, in pixels, a chord of the outline of a clipped viewport may lie from its arc.</summary>
        private const double OutlineTolerance = 0.1;

        /// <summary>An item whose box is smaller than this many pixels each way is drawn as one pixel.</summary>
        private const double TinyPixels = 0.5;

        /// <summary>A text less than this many pixels high is not drawn.</summary>
        private const double LeastTextPixels = 3.0;

        /// <summary>A hatch whose lines come closer than this many pixels is filled with a tint.</summary>
        private const double DenseHatchPixels = 2.0;

        private static readonly ConditionalWeakTable<DisplaySegments2, double[]> SpacingsOf =
            new ConditionalWeakTable<DisplaySegments2, double[]>();

        private static readonly Dictionary<string, SKTypeface> Measured = new Dictionary<string, SKTypeface>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>The flattening of an arc in a plot, in units of the canvas: a twentieth of a point on a PDF page.</summary>
        private const double PlotArcTolerance = 0.05;

        private readonly Dictionary<string, SKTypeface> _faces = new Dictionary<string, SKTypeface>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>The rules of the plot being drawn; null on the screen.</summary>
        private PlotRules _plot;

        /// <summary>
        /// Draws a plot laid on its sheet, as a PDF page or the preview shows it: the space's display list through the page's
        /// transform and then <paramref name="sheetToCanvas"/>, clipped to the printable area and the area plotted, on white.
        /// It draws as a plotter does, not as the screen: every item whatever its size, every text, a hatch's every line, arcs
        /// flattened to a twentieth of a unit of the canvas, colours through the plot style table, and each line at its
        /// lineweight in millimetres on the sheet (scaled with the plot where asked), the thinnest the device draws where
        /// lineweights are off.
        /// </summary>
        /// <param name="canvas">The canvas, a PDF page or a bitmap.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="page">The plot laid on its sheet.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <param name="sheetToCanvas">From the sheet's millimetres, Y upward from its lower left corner, to the canvas.</param>
        /// <param name="lineweightScale">
        /// Units of the canvas per millimetre of lineweight; the scale of <paramref name="sheetToCanvas"/> where 0. An image
        /// sized in pixels gives 96 / 25.4, its lines as wide as at 96 dots an inch.
        /// </param>
        /// <param name="shxText">
        /// Whether the text of an SHX font is written as well as its strokes, in letters of a transparency so near full that no
        /// eye sees them, for a PDF reader's search to find (PDFSHX); false for the page of anything else.
        /// </param>
        internal void RenderPlot(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadPlotPage page,
            CadPlotSettings settings,
            Affine2 sheetToCanvas,
            double lineweightScale = 0.0,
            bool shxText = false
        )
        {
            if (page.IsEmpty)
            {
                return;
            }

            Affine2 m = page.DrawingToSheet.Then(sheetToCanvas);
            DisplayBox drawn = DisplayBox.Empty;

            foreach (
                double[] corner in new[]
                {
                    new[] { page.Area.MinX, page.Area.MinY },
                    new[] { page.Area.MaxX, page.Area.MaxY },
                }
            )
            {
                drawn = drawn.Add(
                    page.DrawingToSheet.X(corner[0], corner[1]),
                    page.DrawingToSheet.Y(corner[0], corner[1])
                );
            }

            var clip = new DisplayBox(
                Math.Max(drawn.MinX, page.Printable.MinX),
                Math.Max(drawn.MinY, page.Printable.MinY),
                Math.Min(drawn.MaxX, page.Printable.MaxX),
                Math.Min(drawn.MaxY, page.Printable.MaxY)
            );

            if (clip.IsEmpty)
            {
                return;
            }

            float x0 = (float)sheetToCanvas.X(clip.MinX, clip.MinY);
            float y0 = (float)sheetToCanvas.Y(clip.MinX, clip.MinY);
            float x1 = (float)sheetToCanvas.X(clip.MaxX, clip.MaxY);
            float y1 = (float)sheetToCanvas.Y(clip.MaxX, clip.MaxY);
            double perMillimetre = lineweightScale > 0.0 ? lineweightScale : sheetToCanvas.Scale;
            _plot = new PlotRules(
                settings.EffectiveStyleTable,
                settings.PlotLineweights ? perMillimetre * (settings.ScaleLineweights ? page.Scale : 1.0) : 0.0,
                shxText
            );
            canvas.Save();

            try
            {
                canvas.ClipRect(new SKRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1)));
                var options = new CanvasOptions { Background = 0xFFFFFF, Paper = 0xFFFFFF };
                DrawList(
                    canvas,
                    document,
                    space,
                    document.GetDisplay(space),
                    m,
                    page.Area,
                    options,
                    null,
                    null,
                    null,
                    null,
                    0xFFFFFF
                );
            }
            finally
            {
                canvas.Restore();
                _plot = null;
            }
        }

        /// <summary>Draws a space of a document.</summary>
        /// <param name="canvas">The canvas to draw on.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space to draw.</param>
        /// <param name="view">What part of it is shown and at what zoom.</param>
        /// <param name="options">How to draw it.</param>
        public void Render(SKCanvas canvas, CadDocument document, CadSpace space, CadView view, CanvasOptions options)
        {
            options = options ?? new CanvasOptions();
            Draw(canvas, document, space, view, options, options.Selected, options.Hover);
        }

        /// <summary>
        /// Draws a space with nothing selected or hovered: the scene the canvas keeps and draws its highlights over, so that a
        /// change of the entity under the cursor costs the entity, not the drawing.
        /// </summary>
        internal void RenderScene(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options
        )
        {
            Draw(canvas, document, space, view, options ?? new CanvasOptions(), null, null);
        }

        /// <summary>
        /// Draws a space with only some of its entities, as PNGOUT writes the objects selected: the scene of
        /// <see cref="RenderScene"/>, its background and a layout's paper, with every other entity left out. A viewport among
        /// them is drawn with what it shows.
        /// </summary>
        /// <param name="canvas">The canvas to draw on.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space to draw.</param>
        /// <param name="view">What part of it is shown and at what zoom.</param>
        /// <param name="options">How to draw it.</param>
        /// <param name="handles">The entities drawn.</param>
        internal void RenderOnly(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options,
            IReadOnlyCollection<string> handles
        )
        {
            Draw(
                canvas,
                document,
                space,
                view,
                options ?? new CanvasOptions(),
                null,
                null,
                new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase)
            );
        }

        /// <summary>
        /// Draws entities highlighted over what the canvas holds, as <see cref="Render"/> draws them selected or hovered: only
        /// their items, in the order of the drawing. A viewport is drawn by its frame and a wipeout by its outline, so that
        /// neither covers what was drawn over it.
        /// </summary>
        /// <param name="canvas">The canvas holding the scene.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space shown.</param>
        /// <param name="view">The view the scene was drawn at.</param>
        /// <param name="options">How the scene was drawn.</param>
        /// <param name="handles">The entities.</param>
        /// <param name="hover">True to draw them as hovered, false as selected.</param>
        /// <param name="pixels">
        /// The pixels the canvas draws on, the whole of them shown unmoved and unclipped; given, several thousand items are drawn
        /// by several threads at once, pixel for pixel as one draws them. Null to draw them all on the calling thread.
        /// </param>
        internal void RenderHighlight(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options,
            IReadOnlyCollection<string> handles,
            bool hover,
            SKPixmap pixels = null
        )
        {
            if (handles == null || handles.Count == 0)
            {
                return;
            }

            Affine2 m = view.DrawingToScreen;
            double zoom = m.Scale;
            int background = options.BackgroundOf(space);

            if (zoom <= 0.0)
            {
                return;
            }

            List<DisplayItem2> items = VisibleItems(document, space, view, handles);

            if (
                pixels != null
                && items.Count >= HighlightTilesLeast
                && Environment.ProcessorCount > 1
                && HighlightTiles.TryDraw(this, canvas, pixels, document, items, m, zoom, options, hover, background)
            )
            {
                return;
            }

            using (var stroke = NewStroke())
            using (var fill = NewFill())
            {
                foreach (DisplayItem2 item in items)
                {
                    DrawHighlighted(canvas, document, item, m, zoom, options, stroke, fill, hover, background);
                }
            }
        }

        /// <summary>
        /// Draws entities faded over what the canvas holds, as AutoCAD 2021 draws the objects SCALE, ROTATE or MOVE is about to
        /// move while their copy follows the cursor: each blanked out where it is drawn, then drawn again a share of its own
        /// colour mixed into the background, a white line on black at 0.2 a dark grey of 51.
        /// </summary>
        /// <param name="canvas">The canvas holding the scene.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space shown.</param>
        /// <param name="view">The view the scene was drawn at.</param>
        /// <param name="options">How the scene was drawn.</param>
        /// <param name="handles">The entities.</param>
        /// <param name="share">The share of their own colour they keep, from 0 to 1.</param>
        internal void RenderFaded(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options,
            IReadOnlyCollection<string> handles,
            double share
        )
        {
            if (handles == null || handles.Count == 0)
            {
                return;
            }

            Affine2 m = view.DrawingToScreen;
            double zoom = m.Scale;
            int background = options.BackgroundOf(space);

            if (zoom <= 0.0)
            {
                return;
            }

            List<DisplayItem2> items = VisibleItems(document, space, view, handles);

            using (var blank = SKColorFilter.CreateBlendMode(Color(background, 255), SKBlendMode.SrcIn))
            using (var stroke = NewStroke())
            using (var fill = NewFill())
            {
                // Drawn as selected, at least two pixels wide, in the background's colour: the objects blanked out.
                stroke.ColorFilter = blank;
                fill.ColorFilter = blank;

                foreach (DisplayItem2 item in items)
                {
                    DrawItem(canvas, document, item, m, zoom, options, stroke, fill, true, false, background);
                }

                stroke.ColorFilter = null;
                fill.ColorFilter = null;

                using (var layer = new SKPaint { Color = new SKColor(0, 0, 0, (byte)Math.Round(255.0 * share)) })
                {
                    canvas.SaveLayer(layer);

                    foreach (DisplayItem2 item in items)
                    {
                        DrawItem(canvas, document, item, m, zoom, options, stroke, fill, false, false, background);
                    }

                    canvas.Restore();
                }
            }
        }

        /// <summary>
        /// Draws display items that are not in the drawing over what the canvas holds, each as the scene draws it: the table a
        /// cell of which is typed in, laid out for the words typed.
        /// </summary>
        /// <param name="canvas">The canvas holding the scene.</param>
        /// <param name="document">The document.</param>
        /// <param name="space">The space shown.</param>
        /// <param name="view">The view the scene was drawn at.</param>
        /// <param name="options">How the scene was drawn.</param>
        /// <param name="items">The items.</param>
        internal void RenderItems(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options,
            IReadOnlyList<DisplayItem2> items
        )
        {
            Affine2 m = view.DrawingToScreen;
            double zoom = m.Scale;

            if (zoom <= 0.0 || items.Count == 0)
            {
                return;
            }

            int background = options.BackgroundOf(space);

            using (var stroke = NewStroke())
            using (var fill = NewFill())
            {
                foreach (DisplayItem2 item in items)
                {
                    DrawItem(canvas, document, item, m, zoom, options, stroke, fill, false, false, background);
                }
            }
        }

        /// <summary>The display items of entities that the view shows, in the order of the drawing, each once.</summary>
        private static List<DisplayItem2> VisibleItems(
            CadDocument document,
            CadSpace space,
            CadView view,
            IReadOnlyCollection<string> handles
        )
        {
            PickIndex index = document.GetPickIndex(space);
            var places = new List<int>();

            foreach (string handle in handles)
            {
                places.AddRange(index.IndicesOf(handle));
            }

            places.Sort();
            DisplayBox visible = view.VisibleBox;
            var items = new List<DisplayItem2>(places.Count);

            for (int k = 0; k < places.Count; k++)
            {
                DisplayItem2 item = index.List.Items[places[k]];

                if ((k > 0 && places[k] == places[k - 1]) || !item.Bounds.Intersects(visible))
                {
                    continue;
                }

                items.Add(item);
            }

            return items;
        }

        /// <summary>The fewest items drawn highlighted that are shared out among threads; fewer are drawn on the calling thread.</summary>
        private const int HighlightTilesLeast = 4000;

        private static SKPaint NewStroke() =>
            new SKPaint
            {
                IsStroke = true,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Butt,
                StrokeJoin = SKStrokeJoin.Miter,
            };

        private static SKPaint NewFill() => new SKPaint { IsStroke = false, IsAntialias = true };

        /// <summary>One item drawn highlighted: a viewport by its frame and a wipeout by its outline, anything else as selected or hovered.</summary>
        private void DrawHighlighted(
            SKCanvas canvas,
            CadDocument document,
            DisplayItem2 item,
            Affine2 m,
            double zoom,
            CanvasOptions options,
            SKPaint stroke,
            SKPaint fill,
            bool hover,
            int background
        )
        {
            if (item is DisplayViewport2 || (item is DisplayFill2 area && area.IsMask))
            {
                stroke.Color = ColorOf(item, options, !hover, hover, background);
                stroke.StrokeWidth = 2f;
                IReadOnlyList<double[]> rings =
                    item is DisplayFill2 mask ? mask.Rings
                    : item is DisplayViewport2 clipped && clipped.Clip != null
                        ? new[] { clipped.Clip.Flatten(OutlineTolerance / zoom) }
                    : new[] { Frame(item.Bounds) };

                using (SKPath path = RingsPath(rings, m))
                {
                    canvas.DrawPath(path, stroke);
                }

                return;
            }

            DrawItem(canvas, document, item, m, zoom, options, stroke, fill, !hover, hover, background);
        }

        /// <summary>
        /// The highlight of many items drawn by several threads at once, pixel for pixel as one thread draws it. The canvas is
        /// cut into tiles, a thread each; an item whose pixels can only fall in one tile is drawn by that tile's thread, and an
        /// item reaching into several is drawn once every one of those threads has drawn what comes before it, by the last to
        /// get there, while the others wait. An item whose pixels cannot be bounded (a text, a hatch, a ray, a picture) reaches
        /// into every tile, so it is drawn with every other thread waiting. So any two items that may touch a pixel in common
        /// are drawn in the order of the drawing, and each draw is the same draw on the whole canvas a single thread makes: no
        /// tile clips anything, which would move the edges of what crosses its border by a shade. All of a drawing of 100 000
        /// entities selected is highlighted in 150 ms rather than 300 on 24 processors.
        /// </summary>
        private static class HighlightTiles
        {
            /// <summary>The most tiles, and so threads.</summary>
            private const int MostTiles = 16;

            /// <summary>
            /// Draws the items highlighted, as <see cref="DrawHighlighted"/> one after another would, on several threads; false,
            /// drawing nothing, where the canvas is not the whole of <paramref name="pixels"/> unmoved and unclipped, or where
            /// the items are laid so that the threads would mostly wait for each other.
            /// </summary>
            public static bool TryDraw(
                SkiaCanvasRenderer renderer,
                SKCanvas canvas,
                SKPixmap pixels,
                CadDocument document,
                List<DisplayItem2> items,
                Affine2 m,
                double zoom,
                CanvasOptions options,
                bool hover,
                int background
            )
            {
                SKRectI device = canvas.DeviceClipBounds;

                if (
                    !canvas.TotalMatrix.IsIdentity
                    || !canvas.IsClipRect
                    || device.Left != 0
                    || device.Top != 0
                    || device.Right != pixels.Width
                    || device.Bottom != pixels.Height
                )
                {
                    return false;
                }

                int n = items.Count;

                // Each item's box in pixels; nought for an item that draws nothing, NaN for one that cannot be bounded.
                var boxes = new double[4 * n];

                for (int i = 0; i < n; i++)
                {
                    if (DrawsNothing(items[i], zoom, renderer))
                    {
                        continue;
                    }

                    if (PixelBox(items[i], m, options, out double x0, out double y0, out double x1, out double y1))
                    {
                        boxes[4 * i] = x0;
                        boxes[4 * i + 1] = y0;
                        boxes[4 * i + 2] = x1;
                        boxes[4 * i + 3] = y1;
                    }
                    else
                    {
                        boxes[4 * i] = double.NaN;
                    }
                }

                // The tiles laid as columns, as rows or as a grid, whichever the threads would finish soonest: a thread waits
                // at each item it shares for the others to reach it, so tiles laid as rows over a drawing made row by row up
                // the sheet draw one after another, and columns draw it side by side.
                int count = Math.Min(MostTiles, Environment.ProcessorCount);
                int grid = (int)Math.Ceiling(Math.Sqrt(count));
                int columns = 0,
                    rows = 0,
                    soonest = int.MaxValue;
                int[] masks = null;

                foreach (
                    int[] layout in new[]
                    {
                        new[] { count, 1 },
                        new[] { 1, count },
                        new[] { grid, Math.Max(1, count / grid) },
                    }
                )
                {
                    int[] laid = Masks(boxes, n, layout[0], layout[1], pixels.Width, pixels.Height, out int finish);

                    if (finish < soonest)
                    {
                        soonest = finish;
                        masks = laid;
                        columns = layout[0];
                        rows = layout[1];
                    }
                }

                if (soonest > n / 2)
                {
                    return false;
                }

                Draw(renderer, pixels, document, items, masks, columns * rows, m, zoom, options, hover, background);
                return true;
            }

            /// <summary>The items drawn by a thread a tile, each tile's in the order of the drawing, those it shares by the last of their tiles to reach them.</summary>
            private static void Draw(
                SkiaCanvasRenderer renderer,
                SKPixmap pixels,
                CadDocument document,
                List<DisplayItem2> items,
                int[] masks,
                int count,
                Affine2 m,
                double zoom,
                CanvasOptions options,
                bool hover,
                int background
            )
            {
                int n = items.Count;
                var queues = new List<int>[count];

                for (int t = 0; t < count; t++)
                {
                    queues[t] = new List<int>(n / count * 2);
                }

                // How many tiles have still to reach each item, and whether it is drawn.
                var waiting = new int[n];
                var drawn = new int[n];

                for (int i = 0; i < n; i++)
                {
                    for (int t = 0; t < count; t++)
                    {
                        if ((masks[i] & (1 << t)) != 0)
                        {
                            queues[t].Add(i);
                            waiting[i]++;
                        }
                    }
                }

                var info = new SKImageInfo(pixels.Width, pixels.Height, pixels.ColorType, pixels.AlphaType);
                IntPtr address = pixels.GetPixels();
                int rowBytes = pixels.RowBytes;
                Exception failure = null;
                var threads = new Thread[count];

                for (int t = 0; t < count; t++)
                {
                    List<int> queue = queues[t];
                    threads[t] = new Thread(() =>
                    {
                        try
                        {
                            using (SKSurface surface = SKSurface.Create(info, address, rowBytes))
                            using (SKPaint stroke = NewStroke())
                            using (SKPaint fill = NewFill())
                            {
                                foreach (int i in queue)
                                {
                                    if (Interlocked.Decrement(ref waiting[i]) > 0)
                                    {
                                        // Another of its tiles draws it, once that one gets there.
                                        while (Volatile.Read(ref drawn[i]) == 0)
                                        {
                                            if (Volatile.Read(ref failure) != null)
                                            {
                                                return;
                                            }

                                            Thread.Yield();
                                        }

                                        continue;
                                    }

                                    renderer.DrawHighlighted(
                                        surface.Canvas,
                                        document,
                                        items[i],
                                        m,
                                        zoom,
                                        options,
                                        stroke,
                                        fill,
                                        hover,
                                        background
                                    );
                                    Volatile.Write(ref drawn[i], 1);
                                }

                                surface.Canvas.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.CompareExchange(ref failure, ex, null);
                        }
                        finally
                        {
                            ReleaseChainPath();
                        }
                    })
                    {
                        IsBackground = true,
                    };
                    threads[t].Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join();
                }

                if (failure != null)
                {
                    throw new InvalidOperationException("The highlight was not drawn: " + failure.Message, failure);
                }
            }

            private static int Clamp(int tile, int tiles) =>
                tile < 0 ? 0
                : tile >= tiles ? tiles - 1
                : tile;

            /// <summary>
            /// The tiles of each item for tiles laid <paramref name="columns"/> by <paramref name="rows"/> over the canvas, every
            /// tile for an item that cannot be bounded; and, counting each item drawn as one step, the step at which the last
            /// thread would finish, each waiting at an item it shares until the others reach it.
            /// </summary>
            private static int[] Masks(
                double[] boxes,
                int n,
                int columns,
                int rows,
                int width,
                int height,
                out int finish
            )
            {
                var masks = new int[n];
                var clocks = new int[columns * rows];
                double tileWidth = (double)width / columns;
                double tileHeight = (double)height / rows;

                for (int i = 0; i < n; i++)
                {
                    int c0 = 0,
                        r0 = 0,
                        c1 = columns - 1,
                        r1 = rows - 1;

                    if (!double.IsNaN(boxes[4 * i]))
                    {
                        c0 = Clamp((int)Math.Floor(boxes[4 * i] / tileWidth), columns);
                        r0 = Clamp((int)Math.Floor(boxes[4 * i + 1] / tileHeight), rows);
                        c1 = Clamp((int)Math.Floor(boxes[4 * i + 2] / tileWidth), columns);
                        r1 = Clamp((int)Math.Floor(boxes[4 * i + 3] / tileHeight), rows);
                    }

                    int mask = 0,
                        at = 0;

                    for (int r = r0; r <= r1; r++)
                    {
                        for (int c = c0; c <= c1; c++)
                        {
                            mask |= 1 << (r * columns + c);
                            at = Math.Max(at, clocks[r * columns + c]);
                        }
                    }

                    for (int r = r0; r <= r1; r++)
                    {
                        for (int c = c0; c <= c1; c++)
                        {
                            clocks[r * columns + c] = at + 1;
                        }
                    }

                    masks[i] = mask;
                }

                finish = 0;

                foreach (int clock in clocks)
                {
                    finish = Math.Max(finish, clock);
                }

                return masks;
            }

            /// <summary>Whether an item draws no pixel: a text of one look under <see cref="LeastTextPixels"/> high on the screen.</summary>
            private static bool DrawsNothing(DisplayItem2 item, double zoom, SkiaCanvasRenderer renderer) =>
                renderer._plot == null
                && item is DisplayText2 text
                && text.Parts == null
                && text.Height * zoom < LeastTextPixels;

            /// <summary>
            /// The box on the canvas, in pixels, that holds every pixel an item drawn highlighted can touch: its box through the
            /// view, grown by the stroke's reach (half its width, four times over at a mitred corner), the shade of its edge and
            /// a pixel more. False for an item whose pixels cannot be so bounded: a text, whose letters may reach past its box, a
            /// hatch, whose tint fills loops its box of lines may not hold, a ray, a picture, and anything else.
            /// </summary>
            private static bool PixelBox(
                DisplayItem2 item,
                Affine2 m,
                CanvasOptions options,
                out double x0,
                out double y0,
                out double x1,
                out double y1
            )
            {
                x0 = y0 = x1 = y1 = 0.0;

                if (
                    !(
                        item is DisplayPolyline2
                        || item is DisplayArc2
                        || item is DisplayFill2
                        || item is DisplayViewport2
                    )
                )
                {
                    return false;
                }

                DisplayBox b = item.Bounds;
                double ax = m.X(b.MinX, b.MinY),
                    ay = m.Y(b.MinX, b.MinY);
                double bx = m.X(b.MaxX, b.MinY),
                    by = m.Y(b.MaxX, b.MinY);
                double cx = m.X(b.MaxX, b.MaxY),
                    cy = m.Y(b.MaxX, b.MaxY);
                double dx = m.X(b.MinX, b.MaxY),
                    dy = m.Y(b.MinX, b.MaxY);
                x0 = Math.Min(Math.Min(ax, bx), Math.Min(cx, dx));
                y0 = Math.Min(Math.Min(ay, by), Math.Min(cy, dy));
                x1 = Math.Max(Math.Max(ax, bx), Math.Max(cx, dx));
                y1 = Math.Max(Math.Max(ay, by), Math.Max(cy, dy));
                double width = Math.Max(2.0, options.ShowLineWeights ? item.Style.LineWeight * 96.0 / 25.4 : 1.0);
                double reach = 2.0 * width + 3.0;
                x0 -= reach;
                y0 -= reach;
                x1 += reach;
                y1 += reach;
                return !double.IsNaN(x0 + y0 + x1 + y1) && !double.IsInfinity(x0 + y0 + x1 + y1);
            }
        }

        private void Draw(
            SKCanvas canvas,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options,
            IReadOnlyCollection<string> selection,
            string hover,
            HashSet<string> only = null
        )
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            canvas.Clear(Color(space.IsBlockEditor ? options.BlockEditorBackground : options.Background, 255));
            DisplayList2 list = document.GetDisplay(space);
            Affine2 m = view.DrawingToScreen;

            if (!space.IsModel)
            {
                DrawPaper(canvas, space.PaperBounds, m, options);
            }

            var selected = selection == null ? null : new HashSet<string>(selection, StringComparer.OrdinalIgnoreCase);
            DrawList(
                canvas,
                document,
                space,
                list,
                m,
                view.VisibleBox,
                options,
                selected,
                hover,
                only,
                null,
                options.BackgroundOf(space)
            );
        }

        /// <summary>The ring of a box, its four corners.</summary>
        private static double[] Frame(DisplayBox box) =>
            new[] { box.MinX, box.MinY, box.MaxX, box.MinY, box.MaxX, box.MaxY, box.MinX, box.MaxY };

        /// <summary>Releases the typefaces.</summary>
        public void Dispose()
        {
            foreach (SKTypeface face in _faces.Values)
            {
                face.Dispose();
            }

            _faces.Clear();
        }

        private static SKColor Color(int rgb, byte alpha) =>
            new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);

        private static double Luminance(int rgb) =>
            (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255.0;

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

            if (
                Luminance(background) < 0.4
                && (Luminance(rgb) <= 0.02 || (Luminance(rgb) >= 0.98 && onDark != 0xFFFFFF))
            )
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

        /// <summary>
        /// The items of a list the view shows, in the order of the list: through the space's pick index where the list is one
        /// the document indexes, else by looking at each. Items under half a pixel are gathered into runs of single pixels of
        /// one colour, each run drawn before the next item larger than that, so the order holds. Where <paramref name="only"/>
        /// is given, the items of other entities are left out.
        /// </summary>
        private void DrawList(
            SKCanvas canvas,
            CadDocument document,
            CadSpace model,
            DisplayList2 list,
            Affine2 m,
            DisplayBox visible,
            CanvasOptions options,
            HashSet<string> selected,
            string hover,
            HashSet<string> only,
            ISet<string> frozen,
            int background
        )
        {
            double zoom = m.Scale;

            if (zoom <= 0.0)
            {
                return;
            }

            IReadOnlyList<DisplayItem2> items = list.Items;
            IReadOnlyList<int> shown = IndexOf(document, list, model)?.IndicesIn(visible);
            int count = shown?.Count ?? items.Count;

            using (
                var stroke = new SKPaint
                {
                    IsStroke = true,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Butt,
                    StrokeJoin = SKStrokeJoin.Miter,
                }
            )
            using (var fill = new SKPaint { IsStroke = false, IsAntialias = true })
            using (var dots = new Dots())
            {
                for (int k = 0; k < count; k++)
                {
                    DisplayItem2 item = items[shown == null ? k : shown[k]];

                    if (shown == null && !item.Bounds.Intersects(visible))
                    {
                        continue;
                    }

                    if (only != null && (item.Handle == null || !only.Contains(item.Handle)))
                    {
                        continue;
                    }

                    if (frozen != null && frozen.Contains(item.Layer ?? string.Empty))
                    {
                        continue;
                    }

                    // The frame of a clip boundary is plotted only where XCLIPFRAME is 1.
                    if (_plot != null && item.IsNotPlotted)
                    {
                        continue;
                    }

                    bool isSelected = selected != null && item.Handle != null && selected.Contains(item.Handle);
                    bool isHover =
                        hover != null && string.Equals(item.Handle, hover, StringComparison.OrdinalIgnoreCase);

                    if (_plot == null && !isSelected && !isHover && IsTiny(item, zoom))
                    {
                        // A text or a wipeout that small shows nothing; anything else one pixel of its colour.
                        if (!(item is DisplayText2) && !(item is DisplayFill2 area && area.IsMask))
                        {
                            DisplayBox b = item.Bounds;
                            double cx = (b.MinX + b.MaxX) / 2.0;
                            double cy = (b.MinY + b.MaxY) / 2.0;
                            dots.Add(
                                canvas,
                                (float)m.X(cx, cy),
                                (float)m.Y(cx, cy),
                                ColorOf(item, options, false, false, background)
                            );
                        }

                        continue;
                    }

                    dots.Flush(canvas);
                    DrawItem(canvas, document, item, m, zoom, options, stroke, fill, isSelected, isHover, background);
                }

                dots.Flush(canvas);
            }
        }

        /// <summary>The pick index of the list of a space the document keeps, model space's for a viewport's; null for a list it no longer keeps.</summary>
        private static PickIndex IndexOf(CadDocument document, DisplayList2 list, CadSpace space)
        {
            if (document == null || space == null)
            {
                return null;
            }

            PickIndex index = document.GetPickIndex(space);
            return ReferenceEquals(index.List, list) ? index : null;
        }

        /// <summary>Whether an item is under half a pixel each way; a point, drawn as a dot of 3 pixels, and a viewport never are.</summary>
        private static bool IsTiny(DisplayItem2 item, double zoom)
        {
            if (item is DisplayViewport2 || (item is DisplayPolyline2 chain && chain.Count == 1))
            {
                return false;
            }

            DisplayBox b = item.Bounds;
            return Math.Max(b.Width, b.Height) * zoom < TinyPixels;
        }

        /// <summary>
        /// The colour an item is drawn in: its own, turned to show on the background; the
        /// selection colour when selected; its own taken halfway to the selection colour when under the cursor.
        /// </summary>
        private SKColor ColorOf(DisplayItem2 item, CanvasOptions options, bool isSelected, bool isHover, int background)
        {
            if (_plot != null)
            {
                return Color(CadPlotStyles.Map(_plot.Table, item.Style.Rgb), 255);
            }

            int rgb = isSelected
                ? options.SelectionColor
                : Visible(item.Style.Rgb, background, options.VisibleLineColor);

            if (isHover && !isSelected)
            {
                int s = options.SelectionColor;
                rgb =
                    ((((rgb >> 16) & 0xFF) + ((s >> 16) & 0xFF)) / 2 << 16)
                    | ((((rgb >> 8) & 0xFF) + ((s >> 8) & 0xFF)) / 2 << 8)
                    | (((rgb & 0xFF) + (s & 0xFF)) / 2);
            }

            return Color(rgb, 255);
        }

        /// <summary>
        /// Draws an item. Of a block reference clipped (XCLIP) it draws what lies inside the boundary alone: the canvas is clipped
        /// to the ring of the item's <see cref="DisplayItem2.BlockClip"/>, and to the ring of each block it stands in, for the drawing.
        /// </summary>
        private void DrawItem(
            SKCanvas canvas,
            CadDocument document,
            DisplayItem2 item,
            Affine2 m,
            double zoom,
            CanvasOptions options,
            SKPaint stroke,
            SKPaint fill,
            bool isSelected,
            bool isHover,
            int background
        )
        {
            if (item.BlockClip == null)
            {
                DrawWhole(canvas, document, item, m, zoom, options, stroke, fill, isSelected, isHover, background);
                return;
            }

            int saved = canvas.Save();

            try
            {
                for (DisplayClipRegion region = item.BlockClip; region != null; region = region.Parent)
                {
                    using (SKPath ring = RingsPath(new[] { region.Ring }, m))
                    {
                        canvas.ClipPath(ring, SKClipOperation.Intersect, true);
                    }
                }

                DrawWhole(canvas, document, item, m, zoom, options, stroke, fill, isSelected, isHover, background);
            }
            finally
            {
                canvas.RestoreToCount(saved);
            }
        }

        private void DrawWhole(
            SKCanvas canvas,
            CadDocument document,
            DisplayItem2 item,
            Affine2 m,
            double zoom,
            CanvasOptions options,
            SKPaint stroke,
            SKPaint fill,
            bool isSelected,
            bool isHover,
            int background
        )
        {
            // A selected or hovered entity is only coloured and thickened: its own dashes stay, as AutoCAD keeps a linetype
            // when it highlights.
            SKColor color = ColorOf(item, options, isSelected, isHover, background);
            float width =
                _plot != null ? (float)(Math.Max(item.Style.LineWeight, 0.0) * _plot.LineweightScale)
                : options.ShowLineWeights ? (float)Math.Max(1.0, item.Style.LineWeight * 96.0 / 25.4)
                : 1f;
            stroke.Color = color;
            stroke.StrokeWidth = isSelected || isHover ? Math.Max(width, 2f) : width;
            stroke.PathEffect = null;
            double[] dashes = item.Style.Dashes;

            if (dashes != null && dashes.Length > 0)
            {
                stroke.PathEffect = DashEffect(dashes, zoom);
            }

            switch (item)
            {
                case DisplayPolyline2 chain:
                    DrawChain(canvas, chain, m, stroke, fill, color);
                    break;
                case DisplaySegments2 segments:
                    DrawSegments(canvas, segments, m, zoom, stroke, fill, color, _plot != null);
                    break;
                case DisplayRay2 ray:
                    DrawRay(canvas, ray, m, stroke);
                    break;
                case DisplayArc2 arc:
                    DrawArc(canvas, arc, m, zoom, stroke, _plot != null ? PlotArcTolerance : 0.3);
                    break;
                case DisplayFill2 area:
                    DrawFill(canvas, area, m, fill, options, background, item.Style.Rgb, isSelected || isHover, color);
                    break;
                case DisplayText2 text when text.Parts != null:
                    // An MTEXT of several looks: its runs and their lines, each in its own colour, all in the selection's when selected.
                    foreach (DisplayItem2 part in text.Parts)
                    {
                        DrawItem(
                            canvas,
                            document,
                            part,
                            m,
                            zoom,
                            options,
                            stroke,
                            fill,
                            isSelected,
                            isHover,
                            background
                        );
                    }

                    break;
                case DisplayText2 text:
                    DrawText(canvas, text, m, zoom, stroke, fill, color, options);
                    break;
                case DisplayViewport2 viewport:
                    DrawViewport(canvas, document, viewport, m, options, stroke, background, isSelected);
                    break;
                case DisplayImage2 picture:
                    DrawPicture(canvas, picture, m, stroke, background, isSelected || isHover);
                    break;
            }

            stroke.PathEffect?.Dispose();
            stroke.PathEffect = null;
        }

        /// <summary>
        /// Gets or sets whether the pictures of images are decoded off the calling thread, the frame alone drawn until one is
        /// ready (<see cref="CadImageCache.Decoded"/>), as the canvas draws them; false, the default, decodes each at once, as a
        /// plot, a test or a screenshot needs.
        /// </summary>
        internal bool LazyImages { get; set; }

        /// <summary>
        /// The picture of an image laid on its parallelogram, clipped by its boundary (outside it where inverted), over the
        /// background where its transparency is off, its brightness, contrast and fade applied; highlighted, its outline alone.
        /// </summary>
        private void DrawPicture(
            SKCanvas canvas,
            DisplayImage2 picture,
            Affine2 m,
            SKPaint stroke,
            int background,
            bool highlighted
        )
        {
            double x0 = m.X(picture.X, picture.Y);
            double y0 = m.Y(picture.X, picture.Y);
            double ux = m.X(picture.X + picture.Ux, picture.Y + picture.Uy) - x0;
            double uy = m.Y(picture.X + picture.Ux, picture.Y + picture.Uy) - y0;
            double vx = m.X(picture.X + picture.Vx, picture.Y + picture.Vy) - x0;
            double vy = m.Y(picture.X + picture.Vx, picture.Y + picture.Vy) - y0;
            double[] frame =
            {
                picture.X,
                picture.Y,
                picture.X + picture.Ux,
                picture.Y + picture.Uy,
                picture.X + picture.Ux + picture.Vx,
                picture.Y + picture.Uy + picture.Vy,
                picture.X + picture.Vx,
                picture.Y + picture.Vy,
            };

            if (highlighted)
            {
                using (
                    SKPath path = RingsPath(
                        picture.Clip != null && !picture.IsClipInverted ? picture.Clip : new[] { frame },
                        m
                    )
                )
                {
                    canvas.DrawPath(path, stroke);
                }

                return;
            }

            if (picture.FilePath == null)
            {
                return;
            }

            SKImage image = CadImageCache.Get(picture.FilePath, picture.FileStamp, _plot != null || !LazyImages);

            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return;
            }

            double w = image.Width;
            double h = image.Height;
            var matrix = new SKMatrix(
                (float)(ux / w),
                (float)(-vx / h),
                (float)(x0 + vx),
                (float)(uy / w),
                (float)(-vy / h),
                (float)(y0 + vy),
                0f,
                0f,
                1f
            );
            canvas.Save();

            try
            {
                using (SKPath outline = RingsPath(new[] { frame }, m))
                {
                    canvas.ClipPath(outline, SKClipOperation.Intersect, true);
                }

                if (picture.Clip != null)
                {
                    using (SKPath clip = RingsPath(picture.Clip, m))
                    {
                        canvas.ClipPath(
                            clip,
                            picture.IsClipInverted ? SKClipOperation.Difference : SKClipOperation.Intersect,
                            true
                        );
                    }
                }

                canvas.Concat(in matrix);

                using (var paint = new SKPaint { IsAntialias = true })
                {
                    if (!picture.IsTransparent)
                    {
                        paint.Color = Color(background, 255);
                        canvas.DrawRect(SKRect.Create((float)w, (float)h), paint);
                        paint.Color = SKColors.Black;
                    }

                    paint.ColorFilter = Adjustments(picture, background);
                    canvas.DrawImage(
                        image,
                        SKRect.Create((float)w, (float)h),
                        SKRect.Create((float)w, (float)h),
                        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                        paint
                    );
                    paint.ColorFilter?.Dispose();
                }
            }
            finally
            {
                canvas.Restore();
            }
        }

        /// <summary>
        /// The brightness, contrast and fade of an image as one colour matrix: contrast scales each channel about its middle by
        /// a fiftieth of the contrast, brightness moves it by a hundredth of what it is over 50, fade takes it that far towards
        /// the background; null for an image at 50, 50 and 0, drawn as it is.
        /// </summary>
        private static SKColorFilter Adjustments(DisplayImage2 picture, int background)
        {
            if (picture.Brightness == 50 && picture.Contrast == 50 && picture.Fade == 0)
            {
                return null;
            }

            float k = picture.Contrast / 50f;
            float shift = 0.5f - 0.5f * k + (picture.Brightness - 50) / 100f;
            float fade = Math.Max(0, Math.Min(100, picture.Fade)) / 100f;
            float keep = 1f - fade;
            float r = ((background >> 16) & 0xFF) / 255f;
            float g = ((background >> 8) & 0xFF) / 255f;
            float b = (background & 0xFF) / 255f;
            return SKColorFilter.CreateColorMatrix(
                new[]
                {
                    keep * k,
                    0f,
                    0f,
                    0f,
                    keep * shift + fade * r,
                    0f,
                    keep * k,
                    0f,
                    0f,
                    keep * shift + fade * g,
                    0f,
                    0f,
                    keep * k,
                    0f,
                    keep * shift + fade * b,
                    0f,
                    0f,
                    0f,
                    1f,
                    0f,
                }
            );
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

        private static void DrawChain(
            SKCanvas canvas,
            DisplayPolyline2 chain,
            Affine2 m,
            SKPaint stroke,
            SKPaint fill,
            SKColor color
        )
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
                canvas.DrawLine(
                    (float)m.X(xy[0], xy[1]),
                    (float)m.Y(xy[0], xy[1]),
                    (float)m.X(xy[2], xy[3]),
                    (float)m.Y(xy[2], xy[3]),
                    stroke
                );
                return;
            }

            canvas.DrawPath(ChainPath(xy, n, m, chain.Closed), stroke);
        }

        // The path a chain or an arc is drawn through on each thread, emptied before each, and its points.
        [ThreadStatic]
        private static SKPath _chainPath;

        [ThreadStatic]
        private static SKPoint[] _chainPoints;

        /// <summary>
        /// The thread's kept path holding the first <paramref name="count"/> points of <paramref name="xy"/> (X and Y in turn)
        /// through <paramref name="m"/> as one chain, closed where asked: the path a builder makes of a move to the first point
        /// and lines to the rest, and so drawn pixel for pixel as that. SkiaSharp 4 would have a builder make each path, but
        /// each path it makes is a native object registered under a lock every thread shares: made once an item, the threads
        /// drawing a highlight together took longer than one alone.
        /// </summary>
        private static SKPath ChainPath(IReadOnlyList<double> xy, int count, Affine2 m, bool closed)
        {
            SKPath path = _chainPath ?? (_chainPath = new SKPath());
            SKPoint[] points = _chainPoints;

            if (points == null || points.Length < count)
            {
                _chainPoints = points = new SKPoint[Math.Max(count, 64)];
            }

            for (int i = 0; i < count; i++)
            {
                points[i] = new SKPoint((float)m.X(xy[2 * i], xy[2 * i + 1]), (float)m.Y(xy[2 * i], xy[2 * i + 1]));
            }

#pragma warning disable CS0618 // Rewind and AddPoly of SKPath, for SKPathBuilder: see above.
            path.Rewind();
            path.AddPoly(new ReadOnlySpan<SKPoint>(points, 0, count), closed);
#pragma warning restore CS0618
            return path;
        }

        /// <summary>Lets go of the path a worker thread drew through, before the thread ends.</summary>
        private static void ReleaseChainPath()
        {
            _chainPath?.Dispose();
            _chainPath = null;
            _chainPoints = null;
        }

        /// <summary>
        /// An XLINE or a RAY: the part of it the canvas shows, its clip taken back into the drawing and grown by a few pixels.
        /// A dashed one starts a whole number of its patterns from its base point, so that its dashes stay put as the view
        /// pans, as AutoCAD's do.
        /// </summary>
        private static void DrawRay(SKCanvas canvas, DisplayRay2 ray, Affine2 m, SKPaint stroke)
        {
            double det = m.Determinant;

            if (det == 0.0)
            {
                return;
            }

            SKRect clip = canvas.LocalClipBounds;
            double margin = stroke.StrokeWidth + 2.0;
            DisplayBox box = DisplayBox.Empty;

            foreach (
                double[] corner in new[]
                {
                    new double[] { clip.Left - margin, clip.Top - margin },
                    new double[] { clip.Right + margin, clip.Top - margin },
                    new double[] { clip.Right + margin, clip.Bottom + margin },
                    new double[] { clip.Left - margin, clip.Bottom + margin },
                }
            )
            {
                // The screen's corner taken back into the drawing by the inverse of the view's matrix.
                double sx = corner[0] - m.E;
                double sy = corner[1] - m.F;
                box = box.Add((m.D * sx - m.C * sy) / det, (m.A * sy - m.B * sx) / det);
            }

            if (!ray.TryClip(box, out double x0, out double y0, out double x1, out double y1))
            {
                return;
            }

            double[] dashes = ray.Style.Dashes;

            if (stroke.PathEffect != null && dashes != null && dashes.Length > 0)
            {
                double period = 0.0;

                foreach (double dash in dashes)
                {
                    period += Math.Abs(dash);
                }

                period *= dashes.Length % 2 == 0 ? 1.0 : 2.0;
                double along = (x0 - ray.X) * ray.Dx + (y0 - ray.Y) * ray.Dy;

                // Not for a pattern so long on the screen that the start would be pushed far off it.
                if (period > 0.0 && period * m.Scale < 1E5)
                {
                    double start = Math.Floor(along / period) * period;
                    x0 = ray.X + start * ray.Dx;
                    y0 = ray.Y + start * ray.Dy;
                }
            }

            canvas.DrawLine((float)m.X(x0, y0), (float)m.Y(x0, y0), (float)m.X(x1, y1), (float)m.Y(x1, y1), stroke);
        }

        /// <summary>
        /// The lines of a hatch's pattern; where in every direction they run they come closer than 2 pixels, the hatch's
        /// loops filled with its colour as thin as the lines would cover them: lines 1 pixel wide 1.6 apart one way fill 62
        /// per cent, a grid of them both ways 86. A plot draws every line.
        /// </summary>
        private static void DrawSegments(
            SKCanvas canvas,
            DisplaySegments2 segments,
            Affine2 m,
            double zoom,
            SKPaint stroke,
            SKPaint fill,
            SKColor color,
            bool plot
        )
        {
            double[] spacings = !plot && segments.Rings.Count > 0 ? Spacings(segments) : null;
            double widest = 0.0;
            double bare = 1.0;

            foreach (double spacing in spacings ?? Array.Empty<double>())
            {
                widest = Math.Max(widest, spacing * zoom);
                bare *= 1.0 - Math.Min(1.0, stroke.StrokeWidth / (spacing * zoom));
            }

            if (widest > 0.0 && widest < DenseHatchPixels)
            {
                fill.Color = color.WithAlpha((byte)Math.Round(color.Alpha * (1.0 - bare)));

                using (SKPath path = RingsPath(segments.Rings, m))
                {
                    canvas.DrawPath(path, fill);
                }

                return;
            }

            IReadOnlyList<double> xy = segments.Xy;

            using (var builder = new SKPathBuilder())
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
                        builder.AddRect(new SKRect(x0 - 0.5f, y0 - 0.5f, x0 + 0.5f, y0 + 0.5f));
                        continue;
                    }

                    builder.MoveTo(x0, y0);
                    builder.LineTo(x1, y1);
                }

                using (SKPath path = builder.Detach())
                    canvas.DrawPath(path, stroke);
            }
        }

        private static void DrawArc(
            SKCanvas canvas,
            DisplayArc2 arc,
            Affine2 m,
            double zoom,
            SKPaint stroke,
            double pixels
        )
        {
            double tolerance = pixels / zoom;
            double[] xy = arc.Flatten(tolerance);
            canvas.DrawPath(ChainPath(xy, xy.Length / 2, m, arc.IsCircle), stroke);
        }

        /// <summary>
        /// The mean distance between the lines of a hatch's pattern in each direction they run, to the nearest degree, that
        /// holds a twentieth of their length at least: the area of the loops over the length of the lines that way, worked out
        /// once per hatch. A grid of lines 3 apart both ways over a square 300 by 300 gives 3 and 3; a dash of a tenth of the
        /// length gives a direction of its own, as far apart as its dashes are thin.
        /// </summary>
        private static double[] Spacings(DisplaySegments2 segments)
        {
            return SpacingsOf.GetValue(segments, s =>
            {
                IReadOnlyList<double> xy = s.Xy;
                var lengths = new double[180];
                double total = 0.0;

                for (int i = 0; i + 3 < xy.Count; i += 4)
                {
                    double dx = xy[i + 2] - xy[i];
                    double dy = xy[i + 3] - xy[i + 1];
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    int degree = ((int)Math.Round(Math.Atan2(dy, dx) * 180.0 / Math.PI) % 180 + 180) % 180;
                    lengths[degree] += length;
                    total += length;
                }

                double area = RingsArea(s.Rings);
                var spacings = new List<double>();

                for (int degree = 0; degree < 180; degree++)
                {
                    if (lengths[degree] > 0.0 && lengths[degree] >= total / 20.0)
                    {
                        spacings.Add(area / lengths[degree]);
                    }
                }

                return spacings.ToArray();
            });
        }

        /// <summary>
        /// The area rings fill even-odd: a ring inside an odd number of others is a hole. A square 100 by 100 with a column
        /// 10 by 10 inside it fills 9 900.
        /// </summary>
        private static double RingsArea(IReadOnlyList<double[]> rings)
        {
            double area = 0.0;

            for (int i = 0; i < rings.Count; i++)
            {
                double[] ring = rings[i];
                int n = ring.Length / 2;

                if (n < 3)
                {
                    continue;
                }

                double twice = 0.0;

                for (int k = 0; k < n; k++)
                {
                    int next = (k + 1) % n;
                    twice += ring[2 * k] * ring[2 * next + 1] - ring[2 * next] * ring[2 * k + 1];
                }

                int depth = 0;

                for (int j = 0; j < rings.Count; j++)
                {
                    depth += j != i && rings[j].Length >= 6 && Inside(rings[j], ring[0], ring[1]) ? 1 : 0;
                }

                area += (depth % 2 == 0 ? 0.5 : -0.5) * Math.Abs(twice);
            }

            return Math.Max(area, 0.0);
        }

        /// <summary>Whether a point lies inside a ring, by the crossings of a ray to the right.</summary>
        private static bool Inside(double[] ring, double x, double y)
        {
            bool inside = false;
            int n = ring.Length / 2;

            for (int k = 0, prev = n - 1; k < n; prev = k++)
            {
                double xk = ring[2 * k];
                double yk = ring[2 * k + 1];
                double xp = ring[2 * prev];
                double yp = ring[2 * prev + 1];

                if ((yk > y) != (yp > y) && x < (xp - xk) * (y - yk) / (yp - yk) + xk)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>The rings of a fill as one path on the screen, filled even-odd so that a ring inside another is a hole.</summary>
        private static SKPath RingsPath(IReadOnlyList<double[]> rings, Affine2 m)
        {
            using (var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd })
            {
                foreach (double[] ring in rings)
                {
                    int n = ring.Length / 2;

                    if (n < 3)
                    {
                        continue;
                    }

                    builder.MoveTo((float)m.X(ring[0], ring[1]), (float)m.Y(ring[0], ring[1]));

                    for (int i = 1; i < n; i++)
                    {
                        builder.LineTo(
                            (float)m.X(ring[2 * i], ring[2 * i + 1]),
                            (float)m.Y(ring[2 * i], ring[2 * i + 1])
                        );
                    }

                    builder.Close();
                }

                return builder.Detach();
            }
        }

        private static void DrawFill(
            SKCanvas canvas,
            DisplayFill2 area,
            Affine2 m,
            SKPaint fill,
            CanvasOptions options,
            int background,
            int styleRgb,
            bool isSelected,
            SKColor color
        )
        {
            fill.Color = area.IsMask
                ? Color(options.Paper == 0 ? background : background, 255)
                : (isSelected ? new SKColor(color.Red, color.Green, color.Blue, 140) : color);
            using (SKPath path = RingsPath(area.Rings, m))
                canvas.DrawPath(path, fill);
        }

        /// <summary>
        /// Gets whether the canvas has a TrueType typeface of a name, as <see cref="CadFonts"/> asks before it replaces a
        /// style's typeface: Arial on Windows, no typeface of a name no font gives.
        /// </summary>
        /// <param name="family">The typeface.</param>
        /// <returns>True where the font manager matches it.</returns>
        public static bool HasFamily(string family)
        {
            // The typeface matched is not disposed: SkiaSharp may hand back the one the canvas keeps for that family.
            return !string.IsNullOrWhiteSpace(family) && SKFontManager.Default.MatchFamily(family.Trim()) != null;
        }

        /// <summary>
        /// Measures letters as the canvas draws them, for <see cref="CadTrueTypeFonts.MeasureWidth"/>: the width of the letters one
        /// unit high in the family's face, bold or italic, the canvas's own font (Arial) for none.
        /// </summary>
        /// <param name="family">The family, or a font file.</param>
        /// <param name="bold">Whether bold.</param>
        /// <param name="italic">Whether italic.</param>
        /// <param name="text">The letters.</param>
        /// <returns>The width.</returns>
        public static double MeasureWidth(string family, bool bold, bool italic, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0.0;
            }

            string key = (family ?? string.Empty) + "|" + (bold ? "b" : string.Empty) + (italic ? "i" : string.Empty);
            SKTypeface face;

            lock (Measured)
            {
                if (!Measured.TryGetValue(key, out face))
                {
                    string name =
                        string.IsNullOrEmpty(family) ? "Arial"
                        : Path.HasExtension(family) ? Path.GetFileNameWithoutExtension(family)
                        : family;
                    face = SKFontManager.Default.MatchFamily(
                        name,
                        new SKFontStyle(
                            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                            SKFontStyleWidth.Normal,
                            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
                        )
                    ) ?? SKTypeface.Default;
                    Measured[key] = face;
                }
            }

            // The size the canvas sets letters one unit high at (see DrawText): their capitals as high as the unit.
            using (var font = new SKFont(face, (float)(1.0 / 0.72 * 100.0)))
            {
                return font.MeasureText(text) / 100.0;
            }
        }

        /// <summary>The bold or italic face of a typeface's family, the typeface itself where the family has none.</summary>
        private SKTypeface Styled(SKTypeface regular, bool bold, bool italic)
        {
            string key =
                "\u0001" + regular.FamilyName + "|" + (bold ? "b" : string.Empty) + (italic ? "i" : string.Empty);

            if (!_faces.TryGetValue(key, out SKTypeface face))
            {
                face = SKFontManager.Default.MatchFamily(
                    regular.FamilyName,
                    new SKFontStyle(
                        bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                        SKFontStyleWidth.Normal,
                        italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
                    )
                ) ?? regular;
                _faces[key] = face;
            }

            return face;
        }

        /// <summary>
        /// The typeface of a text: the family a TrueType style names; the file a style names, at the full path the fonts found
        /// it at, else from the Fonts folder of Windows, else the family of its name; the canvas's own for an SHX font not
        /// drawn by its strokes, and for none.
        /// </summary>
        private SKTypeface Face(string name, string fallback)
        {
            string key = (name ?? string.Empty) + "|" + fallback;

            if (!_faces.TryGetValue(key, out SKTypeface face))
            {
                string extension = name == null ? string.Empty : Path.GetExtension(name).ToLowerInvariant();

                if (
                    name == null
                    || extension == ".shx"
                    || (extension.Length > 0 && extension != ".ttf" && extension != ".ttc" && extension != ".otf")
                )
                {
                    face = SKTypeface.FromFamilyName(fallback);
                }
                else if (extension.Length > 0)
                {
                    string path = Path.IsPathRooted(name)
                        ? name
                        : Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                            Path.GetFileName(name)
                        );
                    face = (File.Exists(path) ? SKTypeface.FromFile(path) : null)
                        ?? SKFontManager.Default.MatchFamily(Path.GetFileNameWithoutExtension(name))
                        ?? SKTypeface.FromFamilyName(fallback);
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

        private void DrawText(
            SKCanvas canvas,
            DisplayText2 text,
            Affine2 m,
            double zoom,
            SKPaint stroke,
            SKPaint fill,
            SKColor color,
            CanvasOptions options
        )
        {
            double heightPx = text.Height * zoom;

            if (_plot == null && heightPx < LeastTextPixels)
            {
                return;
            }

            if (text.Strokes != null)
            {
                // The letters of an SHX font as their strokes, of the text's line weight, never dashed, as AutoCAD draws them;
                // their ends and corners rounded where the line is wide enough to show it.
                bool wide = stroke.StrokeWidth > 1.5f;
                stroke.PathEffect?.Dispose();
                stroke.PathEffect = null;
                stroke.StrokeCap = wide ? SKStrokeCap.Round : SKStrokeCap.Butt;
                stroke.StrokeJoin = wide ? SKStrokeJoin.Round : SKStrokeJoin.Miter;

                using (var builder = new SKPathBuilder())
                {
                    foreach (double[] xy in text.Strokes)
                    {
                        builder.MoveTo((float)m.X(xy[0], xy[1]), (float)m.Y(xy[0], xy[1]));

                        for (int i = 2; i + 1 < xy.Length; i += 2)
                        {
                            builder.LineTo((float)m.X(xy[i], xy[i + 1]), (float)m.Y(xy[i], xy[i + 1]));
                        }
                    }

                    using (SKPath path = builder.Detach())
                        canvas.DrawPath(path, stroke);
                }

                stroke.StrokeCap = SKStrokeCap.Butt;
                stroke.StrokeJoin = SKStrokeJoin.Miter;

                if (_plot != null && _plot.ShxText)
                {
                    // PDFSHX: the words again in the canvas's own font, 1 part in 255 opaque, which a PDF writer still writes (an
                    // opacity of nought it leaves out) and no eye sees on a sheet; a reader's search finds them over the strokes.
                    DrawLetters(canvas, text, m, heightPx, fill, color.WithAlpha(1), options);
                }

                return;
            }

            DrawLetters(canvas, text, m, heightPx, fill, color, options);
        }

        /// <summary>The letters of a text in a font of the canvas, from its place, turn, height, width factor, slant and alignment.</summary>
        private void DrawLetters(
            SKCanvas canvas,
            DisplayText2 text,
            Affine2 m,
            double heightPx,
            SKPaint fill,
            SKColor color,
            CanvasOptions options
        )
        {
            double cos = Math.Cos(text.Rotation);
            double sin = Math.Sin(text.Rotation);
            double angle = Math.Atan2(m.B * cos + m.D * sin, m.A * cos + m.C * sin);
            float px = (float)m.X(text.X, text.Y);
            float py = (float)m.Y(text.X, text.Y);

            SKTypeface typeface =
                text.IsBold || text.IsItalic
                    ? Styled(Face(text.FontName, options.FontName), text.IsBold, text.IsItalic)
                    : Face(text.FontName, options.FontName);

            using (var font = new SKFont(typeface, (float)(heightPx / 0.72)))
            {
                font.ScaleX = (float)text.WidthFactor;
                font.SkewX = (float)-Math.Tan(text.Oblique);
                fill.Color = color;
                string[] lines = text.Text.Split('\n');
                int n = lines.Length;
                double lineStep = heightPx * 1.25;
                double top = heightPx;
                double bottom = -(n - 1) * lineStep;
                double shift =
                    text.Vertical == 0 ? 0.0
                    : text.Vertical == 1 ? -bottom
                    : text.Vertical == 2 ? -(top + bottom) / 2.0
                    : -top;

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
                    float x =
                        text.Horizontal == 0 ? 0f
                        : text.Horizontal == 1 ? -width / 2f
                        : -width;
                    float y = (float)(-shift + i * lineStep);
                    canvas.DrawText(lines[i], x, y, SKTextAlign.Left, font, fill);
                }

                canvas.Restore();
            }
        }

        private void DrawViewport(
            SKCanvas canvas,
            CadDocument document,
            DisplayViewport2 viewport,
            Affine2 m,
            CanvasOptions options,
            SKPaint stroke,
            int background,
            bool isSelected
        )
        {
            float x0 = (float)m.X(viewport.Bounds.MinX, viewport.Bounds.MaxY);
            float y0 = (float)m.Y(viewport.Bounds.MinX, viewport.Bounds.MaxY);
            float x1 = (float)m.X(viewport.Bounds.MaxX, viewport.Bounds.MinY);
            float y1 = (float)m.Y(viewport.Bounds.MaxX, viewport.Bounds.MinY);
            Affine2 inner = viewport.ModelToPaper.Then(m);
            canvas.Save();

            // A viewport clipped by an entity shows model space through the entity's outline, arcs kept to a tenth of a pixel.
            if (viewport.Clip != null)
            {
                using (SKPath outline = RingsPath(new[] { viewport.Clip.Flatten(OutlineTolerance / m.Scale) }, m))
                {
                    canvas.ClipPath(outline, SKClipOperation.Intersect, true);
                }
            }
            else
            {
                canvas.ClipRect(new SKRect(x0, y0, x1, y1));
            }

            int paperBackground = options.Paper;
            double rotation = viewport.Twist;
            double cos = Math.Cos(-rotation);
            double sin = Math.Sin(-rotation);
            double halfW = viewport.Width / 2.0 / viewport.Scale;
            double halfH = viewport.Height / 2.0 / viewport.Scale;
            double reach = Math.Sqrt(halfW * halfW + halfH * halfH);
            var visible = new DisplayBox(
                viewport.ViewCenterX - reach,
                viewport.ViewCenterY - reach,
                viewport.ViewCenterX + reach,
                viewport.ViewCenterY + reach
            );
            DrawList(
                canvas,
                document,
                document?.ModelSpace,
                viewport.Model,
                inner,
                visible,
                options,
                null,
                null,
                null,
                viewport.FrozenLayers,
                paperBackground
            );
            canvas.Restore();

            if (cos == 0.0 && sin == 0.0)
            {
                return;
            }

            // The border of a clipped viewport is its boundary entity, which the drawing draws as itself; only a selected viewport
            // outlines the boundary here.
            if (viewport.Clip != null)
            {
                if (isSelected)
                {
                    stroke.Color = Color(options.SelectionColor, 255);
                    stroke.StrokeWidth = 2f;

                    using (SKPath outline = RingsPath(new[] { viewport.Clip.Flatten(OutlineTolerance / m.Scale) }, m))
                    {
                        canvas.DrawPath(outline, stroke);
                    }
                }

                return;
            }

            stroke.Color =
                isSelected ? Color(options.SelectionColor, 255)
                : _plot != null ? Color(CadPlotStyles.Map(_plot.Table, 0x808080), 255)
                : Color(Visible(0x808080, paperBackground), 255);
            stroke.StrokeWidth =
                isSelected ? 2f
                : _plot != null ? 0f
                : 1f;
            canvas.DrawRect(x0, y0, x1 - x0, y1 - y0, stroke);
        }

        /// <summary>The rules a plot is drawn by: the table its colours go through, the units of the canvas a millimetre of lineweight is, nought for the thinnest line, and whether the text of an SHX font is also written for a reader to find.</summary>
        private sealed class PlotRules
        {
            public PlotRules(CadPlotStyleTable table, double lineweightScale, bool shxText)
            {
                Table = table;
                LineweightScale = lineweightScale;
                ShxText = shxText;
            }

            public CadPlotStyleTable Table { get; }

            public double LineweightScale { get; }

            public bool ShxText { get; }
        }

        /// <summary>
        /// Single pixels of one colour gathered from items too small to draw, drawn in one call when the colour changes or a
        /// larger item comes: 50 000 items of a sheet shown whole cost a few calls, not 50 000 paths.
        /// </summary>
        private sealed class Dots : IDisposable
        {
            private readonly List<SKPoint> _points = new List<SKPoint>();
            private readonly SKPaint _paint = new SKPaint
            {
                IsAntialias = false,
                IsStroke = true,
                StrokeWidth = 0f,
            };
            private SKColor _color;

            public void Add(SKCanvas canvas, float x, float y, SKColor color)
            {
                if (_points.Count > 0 && color != _color)
                {
                    Flush(canvas);
                }

                _color = color;
                _points.Add(new SKPoint(x, y));
            }

            public void Flush(SKCanvas canvas)
            {
                if (_points.Count == 0)
                {
                    return;
                }

                _paint.Color = _color;
                canvas.DrawPoints(SKPointMode.Points, _points.ToArray(), _paint);
                _points.Clear();
            }

            public void Dispose() => _paint.Dispose();
        }
    }
}
