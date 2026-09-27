using System;
using System.Collections.Generic;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    public readonly partial struct GeoRectangle2
    {
        /// <summary>
        /// Gets the rectangle of least area round some points, using the default tolerance.
        /// </summary>
        public static GeoRectangle2 Fit(IEnumerable<GeoPoint2> points) => Fit(points, Tolerance.Global);

        /// <summary>
        /// Gets the rectangle of least area round some points, turned whichever way makes it smallest.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="tolerance">The tolerance deciding whether points lie in one line.</param>
        /// <returns>
        /// The rectangle, its width along one edge of the points' convex hull: a rectangle of least area always
        /// has a side along one, so trying each finds it exactly. Points all in one line give a rectangle of no
        /// height along that line.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are no points.</exception>
        public static GeoRectangle2 Fit(IEnumerable<GeoPoint2> points, Tolerance tolerance) => BoxFit.Rectangle(points, tolerance);
    }
}
