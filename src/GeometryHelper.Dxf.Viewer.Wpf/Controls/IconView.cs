using System.Windows;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A line icon drawn from path data on a 24 by 24 grid, as the open-source icon sets give them, scaled to its size: an
    /// icon of size 26 draws the grid 26 pixels wide, its lines <see cref="StrokeThickness"/> grid units thick with round
    /// ends and joins.
    /// </summary>
    public sealed class IconView : FrameworkElement
    {
        /// <summary>Identifies the <see cref="Data"/> property.</summary>
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(System.Windows.Media.Geometry), typeof(IconView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Identifies the <see cref="Size"/> property.</summary>
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(IconView), new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Identifies the <see cref="Stroke"/> property.</summary>
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            nameof(Stroke), typeof(Brush), typeof(IconView), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xDB)), FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Identifies the <see cref="StrokeThickness"/> property.</summary>
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(IconView), new FrameworkPropertyMetadata(1.8, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Gets or sets the path of the icon on its 24 by 24 grid.</summary>
        public System.Windows.Media.Geometry Data
        {
            get => (System.Windows.Media.Geometry)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        /// <summary>Gets or sets the width and height of the icon in pixels; 16 by default.</summary>
        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>Gets or sets the colour of the lines.</summary>
        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        /// <summary>Gets or sets the thickness of the lines in units of the 24 by 24 grid; 1.8 by default.</summary>
        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize) => new Size(Size, Size);

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            System.Windows.Media.Geometry data = Data;

            if (data == null)
            {
                return;
            }

            double scale = Size / 24.0;
            var pen = new Pen(Stroke, StrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            drawingContext.PushTransform(new ScaleTransform(scale, scale));
            drawingContext.DrawGeometry(null, pen, data);
            drawingContext.Pop();
        }
    }
}
