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
        private readonly Dictionary<int, Drawn> _pictures = new Dictionary<int, Drawn>();
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

        /// <summary>
        /// Draws the tiles again, as after the tiles were laid out anew. A tile's picture is made anew only where what it shows
        /// changed since it was made (the drawing, the view, its size or what is drawn highlighted), so that panning or zooming
        /// the current tile, which changes none of them, makes none.
        /// </summary>
        public void Refresh() => InvalidateVisual();

        /// <summary>Draws the tiles again where a tile shows something other than its picture does: the drawing, its view or the highlight changed.</summary>
        internal void RefreshIfChanged()
        {
            CadTiledViewports tiles = _model?.ModelTiles;

            if (tiles == null || !tiles.IsTiled || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            foreach (CadTiledViewport tile in tiles.Tiles.Skip(1))
            {
                Rect rect = Place(Box(tile), ActualWidth, ActualHeight, 1.0);

                if (
                    !rect.IsEmpty && (!_pictures.TryGetValue(tile.Id, out Drawn kept) || !kept.Shows(KeyOf(tile, rect)))
                )
                {
                    InvalidateVisual();
                    return;
                }
            }
        }

        /// <summary>Gets how many tile pictures were made since the view was made, for the tests and the timings.</summary>
        internal int PicturesMade { get; private set; }

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
            Drawn key = KeyOf(tile, rect);

            if (_pictures.TryGetValue(tile.Id, out Drawn kept) && kept.Shows(key))
            {
                return kept.Picture;
            }

            PicturesMade++;

            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var view = new CadView(Math.Max(1.0, rect.Width * scale), Math.Max(1.0, rect.Height * scale));

            if (tile.Height > 0.0)
            {
                view.ZoomToHeight(tile.Center.X, tile.Center.Y, tile.Height);
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
                key.Picture = picture;
                _pictures[tile.Id] = key;
                return picture;
            }
        }

        /// <summary>What a tile's picture shows, as of now: the drawing and how often it changed, the tile's view, the size in pixels and what is highlighted.</summary>
        private Drawn KeyOf(CadTiledViewport tile, Rect rect)
        {
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            return new Drawn
            {
                Document = _model.Document,
                Revision = _model.DrawingRevision,
                CenterX = tile.Center.X,
                CenterY = tile.Center.Y,
                ViewHeight = tile.Height,
                PixelWidth = Math.Max(1.0, rect.Width * scale),
                PixelHeight = Math.Max(1.0, rect.Height * scale),
                Options = Fingerprint(_options),
            };
        }

        /// <summary>A number that changes whenever an option the tiles are drawn with does, the selection and the entity under the cursor among them.</summary>
        private static int Fingerprint(CanvasOptions options)
        {
            if (options == null)
            {
                return 0;
            }
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + options.Background;
                hash = hash * 31 + options.Paper;
                hash = hash * 31 + options.SelectionColor;
                hash = hash * 31 + (options.ShowLineWeights ? 1 : 0);
                hash = hash * 31 + options.LineweightScale.GetHashCode();
                hash = hash * 31 + (options.FontName?.GetHashCode() ?? 0);
                hash = hash * 31 + options.VisibleLineColor;
                hash = hash * 31 + options.GripColor;
                hash = hash * 31 + (options.Hover?.GetHashCode() ?? 0);

                if (options.Selected != null)
                {
                    hash = hash * 31 + options.Selected.Count;

                    foreach (string handle in options.Selected)
                    {
                        hash = hash * 31 + handle.GetHashCode();
                    }
                }

                return hash;
            }
        }

        /// <summary>A tile's picture with what it was made from.</summary>
        private sealed class Drawn
        {
            internal BitmapSource Picture;
            internal CadDocument Document;
            internal int Revision;
            internal double CenterX;
            internal double CenterY;
            internal double ViewHeight;
            internal double PixelWidth;
            internal double PixelHeight;
            internal int Options;

            /// <summary>Whether this picture, made from what it records, shows what another record says.</summary>
            internal bool Shows(Drawn now) =>
                ReferenceEquals(Document, now.Document)
                && Revision == now.Revision
                && CenterX == now.CenterX
                && CenterY == now.CenterY
                && ViewHeight == now.ViewHeight
                && PixelWidth == now.PixelWidth
                && PixelHeight == now.PixelHeight
                && Options == now.Options;
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
