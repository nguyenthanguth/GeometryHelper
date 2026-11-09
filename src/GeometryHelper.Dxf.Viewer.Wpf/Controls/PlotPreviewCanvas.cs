using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The sheet of the plot preview, drawn with SkiaSharp as the plot will be: on grey (#5B6573) with its shadow, the printable
    /// area dashed in #9AA4B1, the plot drawn as the PDF writer draws it. A drag pans, zooms in real time or frames a window,
    /// as <see cref="PlotPreviewViewModel.Mode"/> says; the wheel zooms about the cursor.
    /// </summary>
    public sealed class PlotPreviewCanvas : FrameworkElement
    {
        /// <summary>The preview the canvas shows.</summary>
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            nameof(Model), typeof(PlotPreviewViewModel), typeof(PlotPreviewCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly SkiaCanvasRenderer _renderer = new SkiaCanvasRenderer();
        private Point? _from;
        private Point _to;
        private bool _sized;

        /// <summary>Makes the canvas.</summary>
        public PlotPreviewCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Hand;
        }

        /// <summary>Gets or sets the preview the canvas shows.</summary>
        public PlotPreviewViewModel Model
        {
            get => (PlotPreviewViewModel)GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }

        /// <summary>Shows the whole sheet, as Zoom Original does.</summary>
        public void ZoomOriginal()
        {
            Model?.ZoomOriginal(ActualWidth, ActualHeight);
            InvalidateVisual();
        }

        /// <summary>Draws the sheet and the plot into a bitmap of the size given, as the screen shows them.</summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <returns>The bitmap; the caller disposes of it.</returns>
        internal SKBitmap Draw(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(new SKColor(0x5B, 0x65, 0x73));
                PlotPreviewViewModel model = Model;

                if (model != null)
                {
                    if (!(model.PixelsPerMillimetre > 0.0))
                    {
                        model.ZoomOriginal(width, height);
                    }

                    CadPlotPage page = model.Page;
                    Affine2 m = model.SheetToScreen(width, height);
                    SKRect sheet = Rect(m, 0.0, 0.0, page.SheetWidth, page.SheetHeight);

                    using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 89) })
                    using (var paper = new SKPaint { Color = SKColors.White })
                    using (var dashes = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0f))
                    using (var frame = new SKPaint { Color = new SKColor(0x9A, 0xA4, 0xB1), IsStroke = true, StrokeWidth = 1f, PathEffect = dashes })
                    {
                        canvas.DrawRect(new SKRect(sheet.Left + 6f, sheet.Top + 6f, sheet.Right + 6f, sheet.Bottom + 6f), shadow);
                        canvas.DrawRect(sheet, paper);
                        canvas.DrawRect(Rect(m, page.Printable.MinX, page.Printable.MinY, page.Printable.MaxX, page.Printable.MaxY), frame);
                    }

                    _renderer.RenderPlot(canvas, model.Document, model.Space, page, model.Settings, m);

                    if (_from.HasValue && model.Mode == PlotPreviewMode.ZoomWindow)
                    {
                        using (var band = new SKPaint { Color = new SKColor(0x3D, 0x9B, 0xFF), IsStroke = true, StrokeWidth = 1f })
                        {
                            canvas.DrawRect(new SKRect((float)Math.Min(_from.Value.X, _to.X), (float)Math.Min(_from.Value.Y, _to.Y), (float)Math.Max(_from.Value.X, _to.X), (float)Math.Max(_from.Value.Y, _to.Y)), band);
                        }
                    }
                }

                canvas.Flush();
            }

            return bitmap;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            int width = (int)Math.Ceiling(ActualWidth);
            int height = (int)Math.Ceiling(ActualHeight);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (!_sized && Model != null)
            {
                Model.ZoomOriginal(width, height);
                _sized = true;
            }

            using (SKBitmap drawn = Draw(width, height))
            {
                var bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), drawn.GetPixels(), drawn.ByteCount, drawn.RowBytes);
                drawingContext.DrawImage(bitmap, new Rect(0, 0, width, height));
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            _from = e.GetPosition(this);
            _to = _from.Value;
            CaptureMouse();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PlotPreviewViewModel model = Model;

            if (!_from.HasValue || model == null)
            {
                return;
            }

            Point at = e.GetPosition(this);

            if (model.Mode == PlotPreviewMode.Pan)
            {
                model.PanBy(at.X - _to.X, at.Y - _to.Y);
            }
            else if (model.Mode == PlotPreviewMode.Zoom)
            {
                // A drag up of 100 pixels zooms in twice, as AutoCAD's real-time zoom does about the window's middle.
                model.ZoomAt(ActualWidth / 2.0, ActualHeight / 2.0, Math.Pow(2.0, (_to.Y - at.Y) / 100.0), ActualWidth, ActualHeight);
            }

            _to = at;
            InvalidateVisual();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            PlotPreviewViewModel model = Model;

            if (_from.HasValue && model != null && model.Mode == PlotPreviewMode.ZoomWindow)
            {
                model.ZoomWindow(_from.Value.X, _from.Value.Y, _to.X, _to.Y, ActualWidth, ActualHeight);
            }

            _from = null;
            ReleaseMouseCapture();
            InvalidateVisual();
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            Point at = e.GetPosition(this);
            Model?.ZoomAt(at.X, at.Y, e.Delta > 0 ? 1.25 : 0.8, ActualWidth, ActualHeight);
            InvalidateVisual();
            e.Handled = true;
        }

        private static SKRect Rect(Affine2 m, double x0, double y0, double x1, double y1)
        {
            float left = (float)m.X(x0, y1);
            float top = (float)m.Y(x0, y1);
            float right = (float)m.X(x1, y0);
            float bottom = (float)m.Y(x1, y0);
            return new SKRect(Math.Min(left, right), Math.Min(top, bottom), Math.Max(left, right), Math.Max(top, bottom));
        }
    }
}
