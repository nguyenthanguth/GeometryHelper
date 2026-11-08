using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What part of a space the canvas shows and at what zoom: the point of the drawing at the centre of the canvas and the
    /// pixels one drawing unit takes. Zooming about the cursor and panning are arithmetic on these three numbers, so they
    /// are tested here with no window.
    /// </summary>
    public sealed class CadView
    {
        private const double MinZoom = 1E-9;
        private const double MaxZoom = 1E9;

        /// <summary>Makes a view of a canvas of a size, showing the origin at 1 pixel to the unit.</summary>
        /// <param name="width">The width of the canvas in pixels.</param>
        /// <param name="height">The height of the canvas in pixels.</param>
        public CadView(double width, double height)
        {
            Width = Math.Max(1.0, width);
            Height = Math.Max(1.0, height);
            PixelsPerUnit = 1.0;
        }

        /// <summary>Gets the width of the canvas in pixels.</summary>
        public double Width { get; private set; }

        /// <summary>Gets the height of the canvas in pixels.</summary>
        public double Height { get; private set; }

        /// <summary>Gets the X of the point of the drawing at the centre of the canvas.</summary>
        public double CenterX { get; private set; }

        /// <summary>Gets the Y of the point of the drawing at the centre of the canvas.</summary>
        public double CenterY { get; private set; }

        /// <summary>Gets the pixels one drawing unit takes.</summary>
        public double PixelsPerUnit { get; private set; }

        /// <summary>Gets the transformation from the drawing to the pixels of the canvas, Y up in the drawing becoming Y down on the screen.</summary>
        public Affine2 DrawingToScreen => new Affine2(PixelsPerUnit, 0.0, 0.0, -PixelsPerUnit, Width / 2.0 - CenterX * PixelsPerUnit, Height / 2.0 + CenterY * PixelsPerUnit);

        /// <summary>Gets the box of the drawing the canvas shows.</summary>
        public DisplayBox VisibleBox
        {
            get
            {
                double halfW = Width / 2.0 / PixelsPerUnit;
                double halfH = Height / 2.0 / PixelsPerUnit;
                return new DisplayBox(CenterX - halfW, CenterY - halfH, CenterX + halfW, CenterY + halfH);
            }
        }

        /// <summary>Changes the size of the canvas, keeping the centre of the view where it is.</summary>
        /// <param name="width">The new width in pixels.</param>
        /// <param name="height">The new height in pixels.</param>
        public void Resize(double width, double height)
        {
            Width = Math.Max(1.0, width);
            Height = Math.Max(1.0, height);
        }

        /// <summary>Gets the point of the drawing under a pixel.</summary>
        /// <param name="px">The X of the pixel.</param>
        /// <param name="py">The Y of the pixel, down from the top.</param>
        /// <param name="x">The X of the drawing point.</param>
        /// <param name="y">The Y of the drawing point.</param>
        public void ScreenToDrawing(double px, double py, out double x, out double y)
        {
            x = CenterX + (px - Width / 2.0) / PixelsPerUnit;
            y = CenterY - (py - Height / 2.0) / PixelsPerUnit;
        }

        /// <summary>Gets the pixel of a point of the drawing.</summary>
        /// <param name="x">The X of the drawing point.</param>
        /// <param name="y">The Y of the drawing point.</param>
        /// <param name="px">The X of the pixel.</param>
        /// <param name="py">The Y of the pixel, down from the top.</param>
        public void DrawingToPixel(double x, double y, out double px, out double py)
        {
            px = Width / 2.0 + (x - CenterX) * PixelsPerUnit;
            py = Height / 2.0 - (y - CenterY) * PixelsPerUnit;
        }

        /// <summary>Pans: moves the drawing under the canvas by a number of pixels, as dragging with the middle button does.</summary>
        /// <param name="dx">Pixels to the right.</param>
        /// <param name="dy">Pixels down.</param>
        public void Pan(double dx, double dy)
        {
            CenterX -= dx / PixelsPerUnit;
            CenterY += dy / PixelsPerUnit;
        }

        /// <summary>Zooms about a pixel: the point of the drawing under it stays under it.</summary>
        /// <param name="px">The X of the pixel.</param>
        /// <param name="py">The Y of the pixel, down from the top.</param>
        /// <param name="factor">The factor by which things grow; 1.2 is one wheel notch in, 1 / 1.2 one out.</param>
        public void ZoomAt(double px, double py, double factor)
        {
            ScreenToDrawing(px, py, out double x, out double y);
            PixelsPerUnit = Math.Max(MinZoom, Math.Min(MaxZoom, PixelsPerUnit * factor));
            CenterX = x - (px - Width / 2.0) / PixelsPerUnit;
            CenterY = y + (py - Height / 2.0) / PixelsPerUnit;
        }

        /// <summary>Shows a box wholly, with a margin, as ZOOM Extents does.</summary>
        /// <param name="box">The box; an empty box shows the origin at 1 pixel to the unit.</param>
        /// <param name="marginFraction">The margin as a fraction of the canvas, 0.05 for 5 per cent on each side.</param>
        public void ZoomTo(DisplayBox box, double marginFraction = 0.05)
        {
            if (box.IsEmpty)
            {
                CenterX = 0.0;
                CenterY = 0.0;
                PixelsPerUnit = 1.0;
                return;
            }

            double w = Math.Max(box.Width, 1E-9);
            double h = Math.Max(box.Height, 1E-9);
            double usable = 1.0 - 2.0 * marginFraction;
            PixelsPerUnit = Math.Max(MinZoom, Math.Min(MaxZoom, Math.Min(Width * usable / w, Height * usable / h)));
            CenterX = (box.MinX + box.MaxX) / 2.0;
            CenterY = (box.MinY + box.MaxY) / 2.0;
        }
    }
}
