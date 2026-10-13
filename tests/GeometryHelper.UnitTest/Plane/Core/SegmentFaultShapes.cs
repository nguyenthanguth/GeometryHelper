using System;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The shapes and tolerances the five old segment faults (B1 to B5) are shown on, shared by their test files.
    /// </summary>
    internal static class SegmentFaultShapes
    {
        internal static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>The unit vector along (x, y).</summary>
        internal static GeoVector2 U(double x, double y) => new GeoVector2(x, y) * (1 / Math.Sqrt(x * x + y * y));

        #region Tolerances

        /// <summary>The default reading, 1E-3 for points.</summary>
        internal static readonly Tolerance Thousandth = new Tolerance(1E-3, 1E-5);

        /// <summary>A tolerance ten times tighter than the default, 1E-4 for points.</summary>
        internal static readonly Tolerance TenThousandth = new Tolerance(1E-4, 1E-5);

        /// <summary>A loose tolerance, 0.1 for points.</summary>
        internal static readonly Tolerance Tenth = new Tolerance(0.1, 1E-5);

        #endregion

        #region B1: the circle and its tangent band

        /// <summary>The circle of radius 50 about the origin.</summary>
        internal static GeoCircle2 Circle() => new GeoCircle2(P(0, 0), 50);

        /// <summary>The upper half of that circle, from (50, 0) over (0, 50) to (-50, 0).</summary>
        internal static GeoArc2 UpperHalf() => new GeoArc2(P(0, 0), 50, 0.0, Math.PI);

        /// <summary>
        /// The segment 50 long from (0, 49.9995), half a thousandth inside the rim, along (1, 0.001): it leaves the circle
        /// 0.179 along, at (0.1791, 49.99968), and its line runs 49.999475 from the centre, inside the tangent band.
        /// </summary>
        internal static GeoLine2 NearlyAlongTheRim() => new GeoLine2(P(0, 49.9995), P(0, 49.9995).Add(U(1, 0.001) * 50));

        /// <summary>The ray from the start of <see cref="NearlyAlongTheRim"/>, the same way.</summary>
        internal static GeoRay2 RayNearlyAlongTheRim() => new GeoRay2(P(0, 49.9995), U(1, 0.001));

        #endregion

        #region B2: the square with a bowed top

        /// <summary>
        /// The square from the origin to (100, 100) whose top, drawn from (100, 100) to (0, 100), bows by the bulge given:
        /// -0.4 bows it 20 inwards, to (50, 80), on a circle of radius 72.5 about (50, 152.5); 0.4 bows it 20 outwards.
        /// </summary>
        internal static GeoPolygonArc2 SquareWithBowedTop(double bulge)
            => new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 100), P(0, 100) }, new[] { 0.0, 0.0, bulge, 0.0 });

        #endregion

        #region B3: the rectangle

        /// <summary>The rectangle 100 by 100 about the origin, from (-50, -50) to (50, 50).</summary>
        internal static GeoRectangle2 Square() => new GeoRectangle2(P(0, 0), 100, 100);

        #endregion
    }
}
