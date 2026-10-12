using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The shapes the ray in the plane is asked about, shared by its query tests. Each is drawn on whole numbers so that
    /// the crossings of rays along the axes and the diagonals are exact.
    /// </summary>
    internal static class Ray2Shapes
    {
        internal static readonly double Root2 = Math.Sqrt(2.0);

        internal static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        internal static GeoVector2 V(double x, double y) => new GeoVector2(x, y);

        /// <summary>The unit vector along (x, y).</summary>
        internal static GeoVector2 U(double x, double y) => V(x, y) * (1 / Math.Sqrt(x * x + y * y));

        #region Bounded shapes, drawn about the square from the origin to (100, 100)

        /// <summary>The square 100 by 100 with a corner at the origin.</summary>
        internal static GeoPolygon2 Square() => new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));

        /// <summary>The same square as a rectangle about (50, 50).</summary>
        internal static GeoRectangle2 Rectangle() => new GeoRectangle2(P(50, 50), 100, 100);

        /// <summary>The square with the square from (30, 30) to (70, 70) cut out of it.</summary>
        internal static GeoFace2 Plate() => new GeoFace2(Square(), new[] { new GeoPolygon2(P(30, 30), P(70, 30), P(70, 70), P(30, 70)) });

        /// <summary>The half of the square below its diagonal x + y = 100.</summary>
        internal static GeoTriangle2 Triangle() => new GeoTriangle2(P(0, 0), P(100, 0), P(0, 100));

        /// <summary>The circle of radius 50 about the origin.</summary>
        internal static GeoCircle2 Circle() => new GeoCircle2(P(0, 0), 50);

        /// <summary>From 30 degrees to 180 on that circle: from (43.30, 25) over the top (0, 50) to (-50, 0).</summary>
        internal static GeoArc2 Arc() => new GeoArc2(P(0, 0), 50, Math.PI / 6, Math.PI);

        /// <summary>The right half of the circle, from (0, -50) through (50, 0) to (0, 50): it reaches 50 past its ends.</summary>
        internal static GeoArc2 RightHalf() => new GeoArc2(P(0, 0), 50, -Math.PI / 2, Math.PI / 2);

        /// <summary>The L from the origin along the bottom to (100, 0) and up to (100, 100).</summary>
        internal static GeoPolyline2 Ell() => new GeoPolyline2(P(0, 0), P(100, 0), P(100, 100));

        /// <summary>The L whose upright is a half circle about (100, 50), bulging out to (150, 50).</summary>
        internal static GeoPolylineArc2 Hook() => new GeoPolylineArc2(new[] { P(0, 0), P(100, 0), P(100, 100) }, new[] { 0.0, 1.0 });

        /// <summary>The slot 200 by 50 with round ends of radius 25 about (0, 25) and (200, 25): from x = -25 to 225.</summary>
        internal static GeoPolygonArc2 Slot() => new GeoPolygonArc2(new[] { P(0, 0), P(200, 0), P(200, 50), P(0, 50) }, new[] { 0.0, 1.0, 0.0, 1.0 });

        /// <summary>The ellipse 200 by 100 about the origin, its long axis along X.</summary>
        internal static GeoEllipse2 Oval() => new GeoEllipse2(P(0, 0), GeoVector2.XAxis, 100, 50);

        /// <summary>
        /// A cog of <paramref name="teeth"/> corners about the origin, reaching <paramref name="radius"/> and seven tenths
        /// of it by turns, every third side bulging by 0.3: many short edges, some curved, for a hierarchy to prune.
        /// </summary>
        internal static GeoPolygonArc2 Cog(int teeth, double radius)
        {
            var vertices = new GeoPoint2[teeth];
            var bulges = new double[teeth];

            for (int i = 0; i < teeth; i++)
            {
                double angle = i * Math.PI * 2.0 / teeth;
                double reach = i % 2 == 0 ? radius : radius * 0.7;

                vertices[i] = P(reach * Math.Cos(angle), reach * Math.Sin(angle));
                bulges[i] = i % 3 == 0 ? 0.3 : 0.0;
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        #endregion

        #region Rays placed against them

        /// <summary>
        /// The ray along the unit direction <paramref name="d"/> passing a corner four tenths outside it, along the unit
        /// normal <paramref name="n"/>, its origin 100 back from the point of passing.
        /// </summary>
        internal static GeoRay2 Grazing(GeoPoint2 corner, GeoVector2 d, GeoVector2 n, out GeoPoint2 passing)
        {
            passing = P(corner.X + 0.4 * n.X, corner.Y + 0.4 * n.Y);
            return new GeoRay2(P(passing.X - 100 * d.X, passing.Y - 100 * d.Y), d);
        }

        /// <summary>The segment from the ray's origin 10 000 along the unit direction it was made with.</summary>
        internal static GeoLine2 FarAlong(GeoRay2 ray, GeoVector2 unit) => new GeoLine2(ray.Origin, P(ray.Origin.X + 10000 * unit.X, ray.Origin.Y + 10000 * unit.Y));

        #endregion
    }
}
