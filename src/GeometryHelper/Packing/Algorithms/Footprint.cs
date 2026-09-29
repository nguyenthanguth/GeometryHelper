using GeometryHelper.Geometry;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// The upright box a rectangle takes up: its extent along X and along Y. A rectangle that is turned takes up the box
    /// round its corners, and is packed by that box, keeping its turn.
    /// </summary>
    internal readonly struct Footprint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Footprint"/> struct from its lower left corner and its size.
        /// </summary>
        internal Footprint(double minX, double minY, double width, double height)
        {
            MinX = minX;
            MinY = minY;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the least X.</summary>
        internal double MinX { get; }

        /// <summary>Gets the least Y.</summary>
        internal double MinY { get; }

        /// <summary>Gets the extent along X.</summary>
        internal double Width { get; }

        /// <summary>Gets the extent along Y.</summary>
        internal double Height { get; }

        /// <summary>Gets the greatest X.</summary>
        internal double MaxX => MinX + Width;

        /// <summary>Gets the greatest Y.</summary>
        internal double MaxY => MinY + Height;

        /// <summary>Gets whether all four numbers are finite.</summary>
        internal bool IsFinite => Guard.IsFinite(MinX) && Guard.IsFinite(MinY) && Guard.IsFinite(Width) && Guard.IsFinite(Height);

        /// <summary>
        /// Gets the footprint of a rectangle: exactly its own extent when it is not turned, the box round its corners
        /// when it is.
        /// </summary>
        internal static Footprint Of(GeoRectangle2 rectangle)
        {
            if (rectangle.AngleRad == 0.0)
            {
                return new Footprint(rectangle.Center.X - rectangle.Width * 0.5, rectangle.Center.Y - rectangle.Height * 0.5, rectangle.Width, rectangle.Height);
            }

            GeoPoint2[] corners = rectangle.GetVertices();
            double minX = corners[0].X, maxX = corners[0].X, minY = corners[0].Y, maxY = corners[0].Y;
            for (int i = 1; i < corners.Length; i++)
            {
                if (corners[i].X < minX) minX = corners[i].X;
                if (corners[i].X > maxX) maxX = corners[i].X;
                if (corners[i].Y < minY) minY = corners[i].Y;
                if (corners[i].Y > maxY) maxY = corners[i].Y;
            }

            return new Footprint(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
