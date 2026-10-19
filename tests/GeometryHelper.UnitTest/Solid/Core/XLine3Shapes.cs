using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The shapes in space the infinite line is asked about, shared by its query tests. Each is drawn on whole numbers so that
    /// the crossings of lines along the axes and the diagonals are exact.
    /// </summary>
    internal static class XLine3Shapes
    {
        internal static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        internal static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z);

        internal static GeoXLine3 X(double x, double y, double z, double dx, double dy, double dz) => new GeoXLine3(P(x, y, z), V(dx, dy, dz));

        #region Bounded shapes, drawn about the box from the origin to (100, 100, 20)

        /// <summary>The box 100 by 100 by 20 with a corner at the origin.</summary>
        internal static GeoAabb3 Aabb() => new GeoAabb3(P(0, 0, 0), P(100, 100, 20));

        /// <summary>The same box as an oriented box about (50, 50, 10).</summary>
        internal static GeoObb3 Obb() => new GeoObb3(P(50, 50, 10), 100, 100, 20);

        /// <summary>The same box as a solid.</summary>
        internal static GeoSolid3 SolidBox() => Obb().ToSolid();

        /// <summary>The square 100 by 100 in the floor with a corner at the origin.</summary>
        internal static GeoPolygon3 Square() => new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0));

        /// <summary>The square with the square from (30, 30) to (70, 70) cut out of it.</summary>
        internal static GeoFace3 Plate() => new GeoFace3(Square(), new[] { new GeoPolygon3(P(30, 30, 0), P(70, 30, 0), P(70, 70, 0), P(30, 70, 0)) });

        /// <summary>The half of the square below its diagonal x + y = 100.</summary>
        internal static GeoTriangle3 Triangle() => new GeoTriangle3(P(0, 0, 0), P(100, 0, 0), P(0, 100, 0));

        /// <summary>The circle of radius 50 about the origin, lying in the floor.</summary>
        internal static GeoCircle3 Circle() => new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 50);

        /// <summary>The upper half of that circle, from (50, 0, 0) over the top (0, 50, 0) to (-50, 0, 0).</summary>
        internal static GeoArc3 Arc() => ArcFrom(P(0, 0, 0), GeoVector3.ZAxis, 50, P(50, 0, 0), Math.PI);

        /// <summary>
        /// The arc about a centre in the plane of a normal that starts at a point of its circle and turns counter-clockwise about
        /// the normal through a sweep: the start is given as a point, since the angles of an arc are read from an axis of its plane
        /// that is not written down.
        /// </summary>
        internal static GeoArc3 ArcFrom(GeoPoint3 center, GeoVector3 normal, double radius, GeoPoint3 start, double sweep)
        {
            new GeoPlane3(center, normal).GetAxes(out GeoVector3 u, out GeoVector3 v);

            GeoVector3 radial = center.GetVectorTo(start);
            double angle = Math.Atan2(radial.DotProduct(v), radial.DotProduct(u));

            return new GeoArc3(center, normal, radius, angle, angle + sweep);
        }

        /// <summary>The ellipse 200 by 100 about the origin lying in the floor, its long axis along X.</summary>
        internal static GeoEllipse3 Oval() => new GeoEllipse3(P(0, 0, 0), GeoVector3.ZAxis, GeoVector3.XAxis, 100, 50);

        /// <summary>The L from the origin along the bottom to (100, 0, 0) and up to (100, 100, 0), in the floor.</summary>
        internal static GeoPolyline3 Ell() => new GeoPolyline3(P(0, 0, 0), P(100, 0, 0), P(100, 100, 0));

        /// <summary>The L whose upright is a half circle about (100, 50, 0), bulging out to (150, 50, 0), in the floor.</summary>
        internal static GeoPolylineArc3 Hook() => new GeoPolylineArc3(
            new[] { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0) }, new[] { 0.0, 1.0 }, new[] { GeoVector3.ZAxis, GeoVector3.ZAxis });

        /// <summary>The slot 200 by 50 in the floor with round ends of radius 25 about (0, 25, 0) and (200, 25, 0).</summary>
        internal static GeoPolygonArc3 Slot() => new GeoPolygonArc3(
            new[] { P(0, 0, 0), P(200, 0, 0), P(200, 50, 0), P(0, 50, 0) },
            new[] { 0.0, 1.0, 0.0, 1.0 },
            new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis });

        /// <summary>The segment from (50, -10, 0) to (50, 10, 0).</summary>
        internal static GeoLine3 Segment() => new GeoLine3(P(50, -10, 0), P(50, 10, 0));

        /// <summary>The floor, the plane z = 0.</summary>
        internal static GeoPlane3 Floor() => GeoPlane3.XY;

        #endregion
    }
}
