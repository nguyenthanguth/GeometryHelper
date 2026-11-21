using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The picture of a DCL <c>image</c> or <c>image_button</c>: a ground and the operations <c>fill_image</c> and
    /// <c>vector_image</c> made, in pixels from its top-left, in ACI colours or DCL's own (-2 the graphics background, -15 the
    /// dialog background, -16 its foreground, -18 its line). It has a 1 px frame just outside its area; an image button
    /// switched by <c>mode_tile</c> 4 has a frame of 2 px in the accent colour inside it instead.
    /// </summary>
    internal sealed class DclPictureView : FrameworkElement
    {
        private static readonly Brush Frame = Frozen(0x3A, 0x41, 0x4C);
        private static readonly Brush Highlight = Frozen(0x3D, 0x9B, 0xFF);
        private const int GroundColor = 0x14181D;

        private readonly List<DclImageOp> _operations = new List<DclImageOp>();
        private int _ground = GroundColor;
        private bool _isFlipped;

        /// <summary>Gets the operations drawn, in order; <see cref="Set"/> changes them.</summary>
        internal IReadOnlyList<DclImageOp> Operations => _operations;

        /// <summary>Gets whether the picture draws the frame of a flipped image button.</summary>
        internal bool IsFlipped => _isFlipped;

        /// <summary>Gives the picture what to draw and redraws it where something changed.</summary>
        /// <param name="ground">The <c>color</c> of the tile: an ACI number, a DCL colour number or name; null for the usual dark.</param>
        /// <param name="operations">What <c>fill_image</c> and <c>vector_image</c> made.</param>
        /// <param name="flipped">Whether the tile is an image button that <c>mode_tile</c> 4 flipped.</param>
        internal void Set(string ground, IReadOnlyList<DclImageOp> operations, bool flipped)
        {
            int groundColor = ground == null ? GroundColor : ColorOf(ground, GroundColor);
            bool changed = _isFlipped != flipped || _ground != groundColor || _operations.Count != operations.Count;

            for (int i = 0; !changed && i < operations.Count; i++)
            {
                DclImageOp a = _operations[i];
                DclImageOp b = operations[i];
                changed =
                    a.Kind != b.Kind
                    || a.X1 != b.X1
                    || a.Y1 != b.Y1
                    || a.X2 != b.X2
                    || a.Y2 != b.Y2
                    || a.Color != b.Color;
            }

            if (!changed)
            {
                return;
            }

            _ground = groundColor;
            _isFlipped = flipped;
            _operations.Clear();
            _operations.AddRange(operations);
            InvalidateVisual();
        }

        /// <summary>
        /// Gives the colour a DCL colour is drawn in: the colours of the dialog for -2, -15, -16 and -18 and the names of
        /// them and of the first seven ACI colours, the ACI colour (as the canvas draws it) for 0 to 255.
        /// </summary>
        /// <param name="color">The colour as written: a number or a name.</param>
        /// <param name="otherwise">The colour (0xRRGGBB) for a text that is neither.</param>
        /// <returns>The colour, 0xRRGGBB.</returns>
        internal static int ColorOf(string color, int otherwise)
        {
            switch (color)
            {
                case "graphics_background":
                    return ColorOf(-2);
                case "dialog_background":
                    return ColorOf(-15);
                case "dialog_foreground":
                    return ColorOf(-16);
                case "dialog_line":
                    return ColorOf(-18);
                case "black":
                    return ColorOf(0);
                case "red":
                    return ColorOf(1);
                case "yellow":
                    return ColorOf(2);
                case "green":
                    return ColorOf(3);
                case "cyan":
                    return ColorOf(4);
                case "blue":
                    return ColorOf(5);
                case "magenta":
                    return ColorOf(6);
                case "white":
                    return ColorOf(7);
            }

            return int.TryParse(color, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                ? ColorOf(number)
                : otherwise;
        }

        /// <summary>Gives the colour of a DCL colour number: -2 black, -15 the dialog background, -16 its foreground, -18 its line, 0 to 255 ACI.</summary>
        /// <param name="color">The number.</param>
        /// <returns>The colour, 0xRRGGBB.</returns>
        internal static int ColorOf(int color)
        {
            switch (color)
            {
                case -2:
                    return 0x000000;
                case -15:
                    return 0x262B33;
                case -16:
                    return 0xE6E9ED;
                case -18:
                    return 0x3A414C;
            }

            if (color == 0)
            {
                return 0x000000;
            }

            if (color >= 1 && color <= 255)
            {
                return DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)color).ColorValue & 0xFFFFFF;
            }

            return 0xE6E9ED;
        }

        /// <inheritdoc />
        protected override void OnRender(DrawingContext drawing)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            drawing.DrawRectangle(BrushOf(_ground), null, new Rect(0, 0, width, height));
            drawing.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));

            foreach (DclImageOp operation in _operations)
            {
                Brush brush = BrushOf(ColorOf(operation.Color));

                if (operation.Kind == DclImageOpKind.Fill)
                {
                    drawing.DrawRectangle(
                        brush,
                        null,
                        new Rect(operation.X1, operation.Y1, Math.Max(0, operation.X2), Math.Max(0, operation.Y2))
                    );
                }
                else
                {
                    // A line of 1 px through the middle of the pixels it starts and ends in.
                    var pen = new Pen(brush, 1);
                    pen.Freeze();
                    drawing.DrawLine(
                        pen,
                        new Point(operation.X1 + 0.5, operation.Y1 + 0.5),
                        new Point(operation.X2 + 0.5, operation.Y2 + 0.5)
                    );
                }
            }

            drawing.Pop();

            if (_isFlipped)
            {
                var pen = new Pen(Highlight, 2);
                pen.Freeze();
                drawing.DrawRectangle(null, pen, new Rect(1, 1, Math.Max(0, width - 2), Math.Max(0, height - 2)));
            }
            else
            {
                var pen = new Pen(Frame, 1);
                pen.Freeze();
                drawing.DrawRectangle(null, pen, new Rect(-0.5, -0.5, width + 1, height + 1));
            }
        }

        private static Brush BrushOf(int rgb) => Frozen((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        private static Brush Frozen(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
