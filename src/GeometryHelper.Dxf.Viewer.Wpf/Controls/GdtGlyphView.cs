using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A symbol of geometric dimensioning and tolerancing drawn in a button of the Geometric Tolerance, Symbol and Material
    /// Condition dialogs, as board GdtGlyphs draws it: its box of 24 scaled to the control, its strokes with round caps and
    /// joins, its arrowheads filled, a material condition's letter centred in its circle. Nothing for the blank.
    /// </summary>
    public sealed class GdtGlyphView : FrameworkElement
    {
        /// <summary>The glyph drawn.</summary>
        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph),
            typeof(GdtGlyphViewModel),
            typeof(GdtGlyphView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        /// <summary>The width of the strokes, in the box of 24.</summary>
        public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
            nameof(Thickness),
            typeof(double),
            typeof(GdtGlyphView),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        /// <summary>The colour of the strokes, the fill and the letter.</summary>
        public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
            nameof(Ink),
            typeof(Brush),
            typeof(GdtGlyphView),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xED)),
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

        /// <summary>Gets or sets the glyph drawn; null or the blank draws nothing.</summary>
        public GdtGlyphViewModel Glyph
        {
            get => (GdtGlyphViewModel)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        /// <summary>Gets or sets the width of the strokes, in the box of 24: 1.5 by default, as the boards draw them.</summary>
        public double Thickness
        {
            get => (double)GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        /// <summary>Gets or sets the colour of the strokes, the fill and the letter.</summary>
        public Brush Ink
        {
            get => (Brush)GetValue(InkProperty);
            set => SetValue(InkProperty, value);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            GdtGlyphViewModel glyph = Glyph;
            double side = System.Math.Min(ActualWidth, ActualHeight);

            if (glyph == null || glyph.Code == null || !(side > 0.0))
            {
                return;
            }

            double scale = side / 24.0;
            drawingContext.PushTransform(
                new TranslateTransform((ActualWidth - side) / 2.0, (ActualHeight - side) / 2.0)
            );
            drawingContext.PushTransform(new ScaleTransform(scale, scale));
            var pen = new Pen(Ink, Thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };

            if (glyph.Stroke != null)
            {
                drawingContext.DrawGeometry(null, pen, glyph.Stroke);
            }

            if (glyph.Fill != null)
            {
                drawingContext.DrawGeometry(Ink, null, glyph.Fill);
            }

            if (glyph.HasLetter)
            {
                var letter = new FormattedText(
                    glyph.Letter,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Medium,
                        FontStretches.Normal
                    ),
                    11.5,
                    Ink,
                    1.0
                );
                drawingContext.DrawText(letter, new Point(12.0 - letter.Width / 2.0, 12.0 - letter.Height / 2.0));
            }

            drawingContext.Pop();
            drawingContext.Pop();
        }
    }
}
