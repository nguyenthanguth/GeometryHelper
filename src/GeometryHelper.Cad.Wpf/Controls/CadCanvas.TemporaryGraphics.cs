using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The canvas's part of the temporary graphics of AutoLISP's <c>grdraw</c> and <c>grvecs</c>: the vectors the session keeps
    /// (<see cref="CadCommandSession.TemporaryVectors"/>) drawn over the picture of the drawing, a pixel wide, in the colour of
    /// their index, until REDRAW, REGEN or <c>(redraw)</c> takes them off. The XOR colour (0 or less, as
    /// <c>(grdraw p q -1)</c>) is the one of the cursor, light on a dark background and dark on a light one, as is the colour 7,
    /// which AutoCAD shows white or black by the background; a vector drawn highlighted is dashed.
    /// </summary>
    public sealed partial class CadCanvas
    {
        private readonly Dictionary<long, Pen> _temporaryPens = new Dictionary<long, Pen>();

        private void OnTemporaryGraphicsChanged(object sender, EventArgs e) => InvalidateVisual();

        /// <summary>Draws the vectors the session keeps, over the picture and under what follows the cursor.</summary>
        private void DrawTemporaryGraphics(DrawingContext dc, CadView view, double scale)
        {
            IReadOnlyList<CadTemporaryVector> vectors = Session?.TemporaryVectors;

            if (vectors == null || vectors.Count == 0)
            {
                return;
            }

            foreach (CadTemporaryVector vector in vectors)
            {
                dc.DrawLine(
                    TemporaryPen(vector),
                    ToScreen(view, new GeoPoint2(vector.From.X, vector.From.Y), scale),
                    ToScreen(view, new GeoPoint2(vector.To.X, vector.To.Y), scale)
                );
            }
        }

        /// <summary>The pen of a vector: its index's colour, the one that shows against the background for 7 and the XOR colour, dashed where highlighted.</summary>
        private Pen TemporaryPen(CadTemporaryVector vector)
        {
            int background = Options.Background;
            bool dark =
                0.299 * ((background >> 16) & 0xFF) + 0.587 * ((background >> 8) & 0xFF) + 0.114 * (background & 0xFF)
                < 128.0;
            int rgb =
                vector.Colour <= 0 || vector.Colour == 7 || vector.Colour > 255
                    ? (dark ? 0xFFFFFF : 0x000000)
                    : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)vector.Colour).ColorValue;
            long key = ((long)rgb << 1) | (vector.Highlight ? 1L : 0L);

            if (!_temporaryPens.TryGetValue(key, out Pen pen))
            {
                pen = new Pen(Solid(rgb), 1.0);

                if (vector.Highlight)
                {
                    pen.DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0.0);
                }

                _temporaryPens[key] = Frozen(pen);
            }

            return pen;
        }
    }
}
