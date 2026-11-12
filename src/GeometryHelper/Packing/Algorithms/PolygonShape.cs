using System;
using GeometryHelper.Clipper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// One polygon prepared for packing: mirrored when the packing starts from a corner on the right or below, moved so
    /// that the lower left corner of the box round it is the nought of its own frame, and measured in whole steps of a
    /// grid so that what is built from it, the no-fit polygons and the places that are free, is worked out on integers.
    /// </summary>
    internal sealed class PolygonShape
    {
        /// <summary>
        /// Initializes the shape of a polygon.
        /// </summary>
        /// <param name="id">Which shape it is, counted over the whole packing.</param>
        /// <param name="group">Which group of the input the polygon belongs to.</param>
        /// <param name="member">Which polygon of the group it is.</param>
        /// <param name="polygon">The polygon, as given.</param>
        /// <param name="signX">-1 to mirror it across X, as a packing from the right does, otherwise 1.</param>
        /// <param name="signY">-1 to mirror it across Y, as a packing from below does, otherwise 1.</param>
        /// <param name="step">The size of one step of the grid.</param>
        internal PolygonShape(
            int id,
            int group,
            int member,
            GeoPolygon2 polygon,
            double signX,
            double signY,
            double step
        )
        {
            Id = id;
            Group = group;
            Member = member;
            Source = polygon;
            Area = polygon.Area;

            int count = polygon.VertexCount;
            double minX = double.PositiveInfinity,
                minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity,
                maxY = double.NegativeInfinity;
            double originalMinX = double.PositiveInfinity,
                originalMinY = double.PositiveInfinity;
            var mirrored = new GeoPoint2[count];
            for (int i = 0; i < count; i++)
            {
                GeoPoint2 vertex = polygon[i];
                mirrored[i] = new GeoPoint2(signX * vertex.X, signY * vertex.Y);
                minX = Math.Min(minX, mirrored[i].X);
                maxX = Math.Max(maxX, mirrored[i].X);
                minY = Math.Min(minY, mirrored[i].Y);
                maxY = Math.Max(maxY, mirrored[i].Y);
                originalMinX = Math.Min(originalMinX, vertex.X);
                originalMinY = Math.Min(originalMinY, vertex.Y);
            }

            MirrorMinX = minX;
            MirrorMinY = minY;
            OriginalMinX = originalMinX;
            OriginalMinY = originalMinY;
            WidthD = maxX - minX;
            HeightD = maxY - minY;
            Width = (long)Math.Round(WidthD / step);
            Height = (long)Math.Round(HeightD / step);

            // Counter-clockwise, whichever way the polygon runs and however many times it was mirrored.
            var local = new GeoPoint2[count];
            for (int i = 0; i < count; i++)
            {
                local[i] = new GeoPoint2(mirrored[i].X - minX, mirrored[i].Y - minY);
            }

            if (new GeoPolygon2(local, count).SignedArea < 0.0)
            {
                Array.Reverse(local);
            }

            Local = local;
            Pieces = new Paths64[2];
        }

        /// <summary>Gets which shape it is, counted over the whole packing.</summary>
        internal int Id { get; }

        /// <summary>Gets which group of the input the polygon belongs to.</summary>
        internal int Group { get; }

        /// <summary>Gets which polygon of the group it is.</summary>
        internal int Member { get; }

        /// <summary>Gets the polygon as given.</summary>
        internal GeoPolygon2 Source { get; }

        /// <summary>Gets the area of the polygon.</summary>
        internal double Area { get; }

        /// <summary>Gets the least X of the polygon as given.</summary>
        internal double OriginalMinX { get; }

        /// <summary>Gets the least Y of the polygon as given.</summary>
        internal double OriginalMinY { get; }

        /// <summary>Gets the least X of the polygon mirrored, as the packing sees it.</summary>
        internal double MirrorMinX { get; }

        /// <summary>Gets the least Y of the polygon mirrored, as the packing sees it.</summary>
        internal double MirrorMinY { get; }

        /// <summary>Gets the extent of the polygon along X.</summary>
        internal double WidthD { get; }

        /// <summary>Gets the extent of the polygon along Y.</summary>
        internal double HeightD { get; }

        /// <summary>Gets the extent of the polygon along X, in steps.</summary>
        internal long Width { get; }

        /// <summary>Gets the extent of the polygon along Y, in steps.</summary>
        internal long Height { get; }

        /// <summary>
        /// Gets the corners of the polygon mirrored, counter-clockwise, in the frame of its own box: the lower left corner
        /// of the box at nought.
        /// </summary>
        internal GeoPoint2[] Local { get; }

        /// <summary>
        /// Gets the convex pieces the polygon is made of, grown by the half of the gap it keeps, for each kind of gap; null
        /// until they are asked for.
        /// </summary>
        internal Paths64[] Pieces { get; }
    }
}
