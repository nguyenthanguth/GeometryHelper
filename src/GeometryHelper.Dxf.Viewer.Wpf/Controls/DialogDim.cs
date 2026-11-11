using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The shade a dialog takes while a dialog of its own is open over it, as the boards draw it (rgba(10, 12, 16, 0.45)):
    /// laid over its content until disposed.
    /// </summary>
    internal sealed class DialogDim : Adorner, IDisposable
    {
        private static readonly Brush Shade = Frozen(Color.FromArgb(0x73, 0x0A, 0x0C, 0x10));

        private readonly AdornerLayer _layer;

        private DialogDim(UIElement content, AdornerLayer layer)
            : base(content)
        {
            _layer = layer;
            IsHitTestVisible = true;
            _layer.Add(this);
        }

        /// <summary>Shades a dialog's content; nothing where it has no layer to draw on.</summary>
        internal static IDisposable Over(UIElement content)
        {
            AdornerLayer layer = content == null ? null : AdornerLayer.GetAdornerLayer(content);
            return layer == null ? (IDisposable)new Nothing() : new DialogDim(content, layer);
        }

        /// <summary>Takes the shade away.</summary>
        public void Dispose() => _layer.Remove(this);

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext) =>
            drawingContext.DrawRectangle(Shade, null, new Rect(AdornedElement.RenderSize));

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private sealed class Nothing : IDisposable
        {
            public void Dispose() { }
        }
    }
}
