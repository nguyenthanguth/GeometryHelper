using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The preview of the Viewports dialog: each tile a dark box in the area, the current one, the first, framed in the
    /// accent, each labelled *Current* as AutoCAD labels the view it will show.
    /// </summary>
    public sealed class CadTilePreview : FrameworkElement
    {
        /// <summary>The tiles drawn, as shares of the area from (0, 0) at the lower left; the current one first.</summary>
        public static readonly DependencyProperty BoxesProperty = DependencyProperty.Register(
            nameof(Boxes),
            typeof(IReadOnlyList<DisplayBox>),
            typeof(CadTilePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        private static readonly Brush Tile = Frozen(new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x25)));
        private static readonly Brush Text = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB1)));
        private static readonly Pen Accent = Frozen(
            new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x3D, 0x9B, 0xFF))), 2.0)
        );

        /// <summary>Gets or sets the tiles drawn.</summary>
        public IReadOnlyList<DisplayBox> Boxes
        {
            get => (IReadOnlyList<DisplayBox>)GetValue(BoxesProperty);
            set => SetValue(BoxesProperty, value);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            IReadOnlyList<DisplayBox> boxes = Boxes;

            if (boxes == null || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                Rect rect = CadTilesView.Place(boxes[i], ActualWidth, ActualHeight, 1.5);

                if (rect.IsEmpty)
                {
                    continue;
                }

                dc.DrawRectangle(Tile, i == 0 ? Accent : null, rect);
                var label = new FormattedText(
                    "*Current*",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Consolas"),
                    11,
                    Text,
                    1.0
                );
                dc.DrawText(
                    label,
                    new Point(rect.X + (rect.Width - label.Width) / 2, rect.Y + (rect.Height - label.Height) / 2)
                );
            }
        }

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }

    /// <summary>
    /// The tiles of model space beside the canvas (design ModelTiles): each tile but the current one drawn with its own
    /// view, framed, with its label at the upper left; the current tile is the canvas itself, laid over its place and framed
    /// in the accent. A click in a tile makes it the current one.
    /// </summary>
    public sealed class CadTilesView : FrameworkElement
    {
        private static readonly Brush Border = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x41, 0x4C)));
        private static readonly Brush Label = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB1)));
        private static readonly Pen Accent = Frozen(
            new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x3D, 0x9B, 0xFF))), 2.0)
        );
        private readonly Dictionary<int, BitmapSource> _pictures = new Dictionary<int, BitmapSource>();
        private MainViewModel _model;
        private CanvasOptions _options;

        /// <summary>Gets the place of a tile in an area, in device-independent pixels, with a gap between tiles.</summary>
        /// <param name="box">The tile as shares of the area, from (0, 0) at the lower left.</param>
        /// <param name="width">The width of the area.</param>
        /// <param name="height">The height of the area.</param>
        /// <param name="gap">Half the gap left between tiles.</param>
        /// <returns>The rectangle, with y down; empty where nothing is left of it.</returns>
        public static Rect Place(DisplayBox box, double width, double height, double gap)
        {
            double left = Math.Round(box.MinX * width) + (box.MinX > 0.0 ? gap : 0.0);
            double right = Math.Round(box.MaxX * width) - (box.MaxX < 1.0 ? gap : 0.0);
            double top = Math.Round((1.0 - box.MaxY) * height) + (box.MaxY < 1.0 ? gap : 0.0);
            double bottom = Math.Round((1.0 - box.MinY) * height) - (box.MinY > 0.0 ? gap : 0.0);
            return right > left && bottom > top ? new Rect(left, top, right - left, bottom - top) : Rect.Empty;
        }

        /// <summary>Gets the place of the current tile, where the canvas is laid; the whole area where model space is not tiled.</summary>
        public Rect CurrentPlace
        {
            get
            {
                CadTiledViewports tiles = _model?.ModelTiles;
                return tiles == null || !tiles.IsTiled
                    ? new Rect(0, 0, ActualWidth, ActualHeight)
                    : Place(Box(tiles.Current), ActualWidth, ActualHeight, 1.0);
            }
        }

        /// <summary>Follows a view model, drawing with the options of the canvas.</summary>
        /// <param name="model">The view model.</param>
        /// <param name="options">The options the canvas draws with.</param>
        public void Attach(MainViewModel model, CanvasOptions options)
        {
            _model = model;
            _options = options;
        }

        /// <summary>Draws the tiles again, their pictures made anew, as after the drawing or a tile's view changed.</summary>
        public void Refresh()
        {
            _pictures.Clear();
            InvalidateVisual();
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            CadTiledViewports tiles = _model?.ModelTiles;

            if (tiles == null || !tiles.IsTiled || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            dc.DrawRectangle(Border, null, new Rect(0, 0, ActualWidth, ActualHeight));
            tiles.AreaAspect = ActualWidth / ActualHeight;

            foreach (CadTiledViewport tile in tiles.Tiles.Skip(1))
            {
                Rect rect = Place(Box(tile), ActualWidth, ActualHeight, 1.0);

                if (rect.IsEmpty)
                {
                    continue;
                }

                dc.DrawImage(Picture(tile, rect), rect);
                dc.DrawText(
                    new FormattedText(
                        "[-][Top][2D Wireframe]",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Consolas"),
                        11,
                        Label,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip
                    ),
                    new Point(rect.X + 10, rect.Y + 8)
                );
            }

            Rect current = CurrentPlace;
            current.Inflate(-1.0, -1.0);
            dc.DrawRectangle(null, Accent, current);
        }

        /// <inheritdoc/>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _pictures.Clear();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            CadTiledViewports tiles = _model?.ModelTiles;

            if (tiles == null || !tiles.IsTiled)
            {
                return;
            }

            Point at = e.GetPosition(this);
            CadTiledViewport hit = tiles
                .Tiles.Skip(1)
                .FirstOrDefault(t => Place(Box(t), ActualWidth, ActualHeight, 0.0).Contains(at));

            if (hit != null)
            {
                _model.ActivateTile(hit.Id);
                e.Handled = true;
            }
        }

        private BitmapSource Picture(CadTiledViewport tile, Rect rect)
        {
            if (_pictures.TryGetValue(tile.Id, out BitmapSource kept))
            {
                return kept;
            }

            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var view = new CadView(Math.Max(1.0, rect.Width * scale), Math.Max(1.0, rect.Height * scale));

            if (tile.Width > 0.0 && tile.Height > 0.0)
            {
                view.ZoomTo(
                    new DisplayBox(
                        tile.Center.X - tile.Width / 2.0,
                        tile.Center.Y - tile.Height / 2.0,
                        tile.Center.X + tile.Width / 2.0,
                        tile.Center.Y + tile.Height / 2.0
                    ),
                    0.0
                );
            }

            CadDocument document = _model.Document;

            using (
                SKBitmap drawn = CadRasterWriter.Draw(
                    document,
                    document.ModelSpace,
                    view,
                    _options ?? new CanvasOptions(),
                    null
                )
            )
            {
                BitmapSource picture = BitmapSource.Create(
                    drawn.Width,
                    drawn.Height,
                    96.0 * scale,
                    96.0 * scale,
                    PixelFormats.Pbgra32,
                    null,
                    drawn.GetPixels(),
                    drawn.ByteCount,
                    drawn.RowBytes
                );
                picture.Freeze();
                _pictures[tile.Id] = picture;
                return picture;
            }
        }

        private static DisplayBox Box(CadTiledViewport t) => new DisplayBox(t.Left, t.Bottom, t.Right, t.Top);

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
