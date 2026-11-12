using System;
using System.Collections.Generic;
using GeometryHelper.Clipper;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// Polygons that move as one: each shape at a fixed offset from the lower left corner of the box round them all.
    /// </summary>
    internal sealed class PolygonBody
    {
        /// <summary>
        /// Initializes a body.
        /// </summary>
        /// <param name="shapes">The shapes it is made of.</param>
        /// <param name="offsetX">Where the box of each shape begins, across, from the corner of the body.</param>
        /// <param name="offsetY">Where the box of each shape begins, up, from the corner of the body.</param>
        /// <param name="width">The width of the box round the body, in steps.</param>
        /// <param name="height">The height of the box round the body, in steps.</param>
        internal PolygonBody(PolygonShape[] shapes, long[] offsetX, long[] offsetY, long width, long height)
        {
            Shapes = shapes;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = width;
            Height = height;
        }

        /// <summary>A body of one shape.</summary>
        internal static PolygonBody Of(PolygonShape shape) =>
            new PolygonBody(new[] { shape }, new[] { 0L }, new[] { 0L }, shape.Width, shape.Height);

        /// <summary>Gets the shapes it is made of.</summary>
        internal PolygonShape[] Shapes { get; }

        /// <summary>Gets where the box of each shape begins, across, from the corner of the body.</summary>
        internal long[] OffsetX { get; }

        /// <summary>Gets where the box of each shape begins, up, from the corner of the body.</summary>
        internal long[] OffsetY { get; }

        /// <summary>Gets the width of the box round the body, in steps.</summary>
        internal long Width { get; }

        /// <summary>Gets the height of the box round the body, in steps.</summary>
        internal long Height { get; }
    }

    /// <summary>
    /// One bin the polygons are packed into: the room left is whatever the no-fit polygons of the bodies placed leave of
    /// the places where a body's box lies inside the bin, and each body goes to the free place that puts its top highest,
    /// then its left side furthest left, so that the bin fills from its upper left corner as the maximal rectangles
    /// method fills a bin of boxes.
    /// </summary>
    /// <remarks>
    /// Coordinates are the bin's own, in steps of the grid, its lower left corner at nought. The places the reference
    /// point of a body may stand in are the inner-fit rectangle of the bin, let out by the slack, less the no-fit
    /// polygon of every shape placed with every shape of the body; of those places the highest is found among the
    /// corners of what is left, and the leftmost of the highest, and put back inside the bin if it lies past its edge.
    /// A body may pass a no-fit polygon, or the edge of the bin, by the slack and no more.
    /// </remarks>
    internal sealed class PolygonBin
    {
        private readonly PolygonContext _context;
        private readonly int _kind;
        private readonly List<Placed> _placed = new List<Placed>();

        /// <summary>
        /// Initializes an empty bin.
        /// </summary>
        /// <param name="context">What the packing shares.</param>
        /// <param name="kind">The gap the bodies keep: <see cref="PolygonContext.Spacing"/> or <see cref="PolygonContext.Group"/>.</param>
        /// <param name="width">The width of the bin, in steps.</param>
        /// <param name="height">The height of the bin, in steps.</param>
        internal PolygonBin(PolygonContext context, int kind, long width, long height)
        {
            _context = context;
            _kind = kind;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the width of the bin, in steps.</summary>
        internal long Width { get; }

        /// <summary>Gets the height of the bin, in steps.</summary>
        internal long Height { get; }

        /// <summary>
        /// Puts a body in the bin where it goes highest, then furthest left, if it goes anywhere.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="x">Where the lower left corner of its box goes, across, in steps.</param>
        /// <param name="y">Where the lower left corner of its box goes, up, in steps.</param>
        /// <returns>False when the body does not go in; the bin is then as it was.</returns>
        internal bool TryPlace(PolygonBody body, out long x, out long y)
        {
            x = 0;
            y = 0;
            long roomX = Width - body.Width,
                roomY = Height - body.Height;
            long fit = _context.FitSlack;
            if (roomX < -fit || roomY < -fit)
            {
                return false;
            }

            long slack = _context.Slack;
            long lowX = -slack,
                lowY = -slack;
            long highX = Math.Max(roomX, 0L) + slack,
                highY = Math.Max(roomY, 0L) + slack;

            var blocked = new Paths64();
            foreach (Placed other in _placed)
            {
                for (int j = 0; j < body.Shapes.Length; j++)
                {
                    NoFit noFit = _context.GetNoFit(other.Shape, body.Shapes[j], _kind);
                    long shiftX = other.X - body.OffsetX[j],
                        shiftY = other.Y - body.OffsetY[j];
                    if (
                        noFit.MaxX + shiftX <= lowX
                        || noFit.MinX + shiftX >= highX
                        || noFit.MaxY + shiftY <= lowY
                        || noFit.MinY + shiftY >= highY
                    )
                    {
                        continue;
                    }

                    foreach (Path64 path in noFit.Paths)
                    {
                        blocked.Add(Clipper2.TranslatePath(path, shiftX, shiftY));
                    }
                }
            }

            long bestX = lowX,
                bestY = highY;
            if (blocked.Count > 0)
            {
                var inner = new Paths64
                {
                    new Path64(4)
                    {
                        new Point64(lowX, lowY),
                        new Point64(highX, lowY),
                        new Point64(highX, highY),
                        new Point64(lowX, highY),
                    },
                };
                Paths64 free = Clipper2.Difference(inner, blocked, FillRule.NonZero);
                bool any = false;
                bestY = long.MinValue;
                foreach (Path64 path in free)
                {
                    foreach (Point64 point in path)
                    {
                        if (!any || point.Y > bestY || (point.Y == bestY && point.X < bestX))
                        {
                            bestX = point.X;
                            bestY = point.Y;
                            any = true;
                        }
                    }
                }

                if (!any)
                {
                    return false;
                }
            }

            x = Math.Min(Math.Max(bestX, 0L), Math.Max(roomX, 0L));
            y = Math.Min(Math.Max(bestY, 0L), Math.Max(roomY, 0L));
            for (int j = 0; j < body.Shapes.Length; j++)
            {
                _placed.Add(new Placed(body.Shapes[j], x + body.OffsetX[j], y + body.OffsetY[j]));
            }

            return true;
        }

        private readonly struct Placed
        {
            internal Placed(PolygonShape shape, long x, long y)
            {
                Shape = shape;
                X = x;
                Y = y;
            }

            internal PolygonShape Shape { get; }

            internal long X { get; }

            internal long Y { get; }
        }
    }
}
