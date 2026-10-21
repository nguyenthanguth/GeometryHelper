using System;
using System.Collections.Generic;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    public sealed partial class GeoObb3
    {
        /// <summary>
        /// Gets a small box round some points, turned to fit them, using the default tolerance.
        /// </summary>
        public static GeoObb3 Fit(IEnumerable<GeoPoint3> points) => Fit(points, Tolerance.Global);

        /// <summary>
        /// Gets a small box round some points, turned to fit them.
        /// </summary>
        /// <param name="points">The points, such as the corners of a part.</param>
        /// <param name="tolerance">The tolerance deciding whether points lie in one plane.</param>
        /// <returns>
        /// The box standing on the face of the points' convex hull that gives the least volume, with the rectangle
        /// of least area on that face: the smallest box of all for a block, a prism, or anything with a flat face
        /// to stand on, and close to it otherwise. Of the boxes standing on faces of the hull that are as small to
        /// rounding, the thinnest, its least side compared first, then the middle one: a prism 40 long on a right triangle
        /// with legs 18 and 24 is given the box 14.4 by 30 by 40, not the one 18 by 24 by 40. On one face the rectangle is
        /// the first of least area found, as before. Points all in one plane give a box of no depth.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are no points.</exception>
        public static GeoObb3 Fit(IEnumerable<GeoPoint3> points, Tolerance tolerance) => BoxFit.Box(points, tolerance);
    }
}
