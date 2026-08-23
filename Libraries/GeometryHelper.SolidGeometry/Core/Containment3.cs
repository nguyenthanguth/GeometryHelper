using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for deciding where a point sits relative to a 3D shape.
    /// <para>
    /// <c>IsPointOn</c> asks about the boundary of a shape, <c>Contains</c> asks whether the shape holds
    /// the point at all, and <c>Locate</c> returns which of the two — or neither — applies. Only shapes
    /// that enclose something offer <c>Contains</c>; a curve has no interior, so asking it to contain a
    /// point would only restate <c>IsPointOn</c>.
    /// </para>
    /// </summary>
    public static class Containment3
    {
        #region Point on curves

        /// <summary>
        /// Checks whether a point lies on a line segment, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoLine3 line, GeoPoint3 point) => IsPointOn(line, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment is bounded, so a point on its infinite carrier but past an endpoint is not on it.
        /// A degenerate segment is a single point, and the test reduces to comparing against that point.
        /// </remarks>
        public static bool IsPointOn(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            return Distance3.DistanceTo(line, point, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a point lies on a ray, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoRay3 ray, GeoPoint3 point) => IsPointOn(ray, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray starts at its origin, so a point on its carrier line but behind the origin is not on it.
        /// </remarks>
        public static bool IsPointOn(GeoRay3 ray, GeoPoint3 point, Tolerance tolerance)
        {
            return Distance3.DistanceTo(ray, point) <= tolerance.EqualPoint;
        }

        #endregion

        #region Point and plane

        /// <summary>
        /// Checks whether a point lies on a plane, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoPlane3 plane, GeoPoint3 point) => IsPointOn(plane, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The threshold is <see cref="Tolerance.EqualPlanar"/> rather than
        /// <see cref="Tolerance.EqualPoint"/>: this is the flatness question, and a plane is unbounded, so
        /// the point being tested can sit arbitrarily far from the plane origin.
        /// </remarks>
        public static bool IsPointOn(GeoPlane3 plane, GeoPoint3 point, Tolerance tolerance)
        {
            return Math.Abs(plane.SignedDistanceTo(point)) <= tolerance.EqualPlanar;
        }

        /// <summary>
        /// Determines which side of a plane a point lies on, using the default tolerance.
        /// </summary>
        public static PlaneSide GetSide(GeoPlane3 plane, GeoPoint3 point) => GetSide(plane, point, Tolerance.Global);

        /// <summary>
        /// Determines which side of a plane a point lies on, within a tolerance.
        /// </summary>
        public static PlaneSide GetSide(GeoPlane3 plane, GeoPoint3 point, Tolerance tolerance)
        {
            double signed = plane.SignedDistanceTo(point);

            if (Math.Abs(signed) <= tolerance.EqualPlanar)
            {
                return PlaneSide.On;
            }

            return signed > 0.0 ? PlaneSide.Above : PlaneSide.Below;
        }

        #endregion

        #region Point and triangle

        /// <summary>
        /// Locates a point relative to a triangle, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoTriangle3 triangle, GeoPoint3 point) => Locate(triangle, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="point">The point to locate.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// <see cref="PointLocation.Inside"/> when the point lies on the triangle away from its edges,
        /// <see cref="PointLocation.OnSide"/> when it lies on an edge, and
        /// <see cref="PointLocation.OutSide"/> otherwise.
        /// </returns>
        /// <remarks>
        /// A triangle is a planar region, so a point counts as inside only when it lies on the carrier
        /// plane as well as within the edges: a point hovering above the middle of a triangle is outside
        /// it. A degenerate triangle has no interior at all and reports every point as outside, including
        /// points on the sliver itself, because a shape with no area is not a region.
        /// </remarks>
        public static PointLocation Locate(GeoTriangle3 triangle, GeoPoint3 point, Tolerance tolerance)
        {
            if (triangle.IsDegenerate(tolerance))
            {
                return PointLocation.OutSide;
            }

            if (Math.Abs(triangle.GetPlane().SignedDistanceTo(point)) > tolerance.EqualPlanar)
            {
                return PointLocation.OutSide;
            }

            // The edge test comes before the barycentric test so that a point sitting within tolerance of
            // an edge reports OnSide from either side of it. Deciding by sign first would split that band
            // in two and make the answer flip across a boundary the tolerance is meant to blur.
            for (int i = 0; i < 3; i++)
            {
                if (Distance3.DistanceTo(triangle.GetEdgeAt(i), point, tolerance) <= tolerance.EqualPoint)
                {
                    return PointLocation.OnSide;
                }
            }

            if (!triangle.TryGetBarycentric(point, out double u, out double v, out double w))
            {
                return PointLocation.OutSide;
            }

            return u >= 0.0 && v >= 0.0 && w >= 0.0 ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a triangle holds a point, on its surface or along its edges, using the default
        /// tolerance.
        /// </summary>
        public static bool Contains(GeoTriangle3 triangle, GeoPoint3 point) => Contains(triangle, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle holds a point, on its surface or along its edges, within a tolerance.
        /// </summary>
        public static bool Contains(GeoTriangle3 triangle, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(triangle, point, tolerance) != PointLocation.OutSide;
        }

        #endregion

        #region Point and polyline

        /// <summary>
        /// Checks whether a point lies on a polyline, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoPolyline3 polyline, GeoPoint3 point) => IsPointOn(polyline, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on a polyline, within a tolerance.
        /// </summary>
        /// <remarks>
        /// There is deliberately no <c>Contains</c> for a polyline. A curve has no interior, so the
        /// question would only restate this one, and a chain tracing a closed shape would invite the wrong
        /// answer. Convert it with <c>ToPolygon</c> when the enclosed area is what matters.
        /// </remarks>
        public static bool IsPointOn(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance)
        {
            return Distance3.DistanceTo(polyline, point, tolerance) <= tolerance.EqualPoint;
        }

        #endregion

        #region Point and polygon

        /// <summary>
        /// Locates a point relative to a polygon, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoPolygon3 polygon, GeoPoint3 point) => Locate(polygon, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="point">The point to locate.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// <see cref="PointLocation.Inside"/> when the point lies on the polygon away from its edges,
        /// <see cref="PointLocation.OnSide"/> when it lies on an edge, and
        /// <see cref="PointLocation.OutSide"/> otherwise.
        /// </returns>
        /// <remarks>
        /// A polygon is a planar region, so a point counts as inside only when it lies on the carrier
        /// plane as well as within the boundary: a point hovering above the middle of a polygon is outside
        /// it. Within the plane the test is the even-odd rule, counting how many edges a ray from the
        /// point crosses, which is why a self-crossing polygon reports its doubly-enclosed lobes as
        /// outside.
        /// </remarks>
        public static PointLocation Locate(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoPlane3 plane = polygon.GetPlane();

            if (Math.Abs(plane.SignedDistanceTo(point)) > tolerance.EqualPlanar)
            {
                return PointLocation.OutSide;
            }

            // The edge test comes first so that a point within tolerance of an edge reports OnSide from
            // either side of it. Deciding by the crossing count first would split that band in two and make
            // the answer flip across a boundary the tolerance is meant to blur.
            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                if (Distance3.DistanceTo(polygon.GetEdgeAt(i), point, tolerance) <= tolerance.EqualPoint)
                {
                    return PointLocation.OnSide;
                }
            }

            return IsInsideLoop(polygon.Vertices, plane, point) ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Counts edge crossings of a closed loop to decide whether a coplanar point is enclosed.
        /// </summary>
        /// <remarks>
        /// The loop is flattened onto the two in-plane axes and read with the even-odd rule. Working in
        /// the plane rather than in space is what makes this a 2D problem with a known answer; the caller
        /// is responsible for having checked that the point lies on the plane, and for the on-edge band,
        /// which this method does not handle and would otherwise decide arbitrarily.
        /// </remarks>
        private static bool IsInsideLoop(IReadOnlyList<GeoPoint3> loop, GeoPlane3 plane, GeoPoint3 point)
        {
            plane.GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            GeoVector3 offset = plane.Origin.GetVectorTo(point);
            double pu = offset.DotProduct(uAxis);
            double pv = offset.DotProduct(vAxis);

            bool inside = false;
            int count = loop.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                GeoVector3 currentOffset = plane.Origin.GetVectorTo(loop[i]);
                GeoVector3 previousOffset = plane.Origin.GetVectorTo(loop[j]);

                double iu = currentOffset.DotProduct(uAxis);
                double iv = currentOffset.DotProduct(vAxis);
                double ju = previousOffset.DotProduct(uAxis);
                double jv = previousOffset.DotProduct(vAxis);

                if (iv > pv != jv > pv &&
                    pu < (ju - iu) * (pv - iv) / (jv - iv) + iu)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Checks whether a polygon holds a point, on its surface or along its edges, using the default
        /// tolerance.
        /// </summary>
        public static bool Contains(GeoPolygon3 polygon, GeoPoint3 point) => Contains(polygon, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon holds a point, on its surface or along its edges, within a tolerance.
        /// </summary>
        public static bool Contains(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(polygon, point, tolerance) != PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a point lies on the boundary of a polygon, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoPolygon3 polygon, GeoPoint3 point) => IsPointOn(polygon, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the boundary of a polygon, within a tolerance.
        /// </summary>
        public static bool IsPointOn(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(polygon, point, tolerance) == PointLocation.OnSide;
        }

        #endregion

        #region Point and box

        /// <summary>
        /// Locates a point relative to an oriented box, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoObb3 box, GeoPoint3 point) => Locate(box, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The point is taken into the local frame of the box first, which turns the oriented problem into
        /// three independent comparisons against the extents.
        /// </remarks>
        public static PointLocation Locate(GeoObb3 box, GeoPoint3 point, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3 local = box.CoordinateSystem.ToLocal(point);
            double t = tolerance.EqualPoint;

            double overX = Math.Abs(local.X) - box.ExtentX;
            double overY = Math.Abs(local.Y) - box.ExtentY;
            double overZ = Math.Abs(local.Z) - box.ExtentZ;

            if (overX > t || overY > t || overZ > t)
            {
                return PointLocation.OutSide;
            }

            bool onSurface = Math.Abs(overX) <= t || Math.Abs(overY) <= t || Math.Abs(overZ) <= t;

            return onSurface ? PointLocation.OnSide : PointLocation.Inside;
        }

        /// <summary>
        /// Checks whether an oriented box holds a point, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoObb3 box, GeoPoint3 point) => Contains(box, point, Tolerance.Global);

        /// <summary>
        /// Checks whether an oriented box holds a point, within a tolerance.
        /// </summary>
        public static bool Contains(GeoObb3 box, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(box, point, tolerance) != PointLocation.OutSide;
        }

        #endregion

        #region Point and solid

        /// <summary>
        /// Locates a point relative to a solid, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoSolid3 solid, GeoPoint3 point) => Locate(solid, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid; its boundary is assumed closed.</param>
        /// <param name="point">The point to locate.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// <see cref="PointLocation.OnSide"/> when the point lies on a face of the solid or on the wall of
        /// one of its openings, <see cref="PointLocation.Inside"/> when it lies in the material, and
        /// <see cref="PointLocation.OutSide"/> otherwise, openings included.
        /// </returns>
        /// <remarks>
        /// Interior points are found by counting how many faces a ray from the point crosses: an odd count
        /// means it started inside. The answer is only as good as the boundary — an open shell has no
        /// inside and the count means nothing — which is what <see cref="GeoSolid3.IsClosed()"/> is for.
        /// </remarks>
        public static PointLocation Locate(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            foreach (GeoFace3 face in solid.Faces)
            {
                if (Distance3.DistanceTo(face, point, tolerance) <= tolerance.EqualPoint)
                {
                    return PointLocation.OnSide;
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                PointLocation inOpening = Locate(opening, point, tolerance);

                if (inOpening == PointLocation.OnSide)
                {
                    return PointLocation.OnSide;
                }

                if (inOpening == PointLocation.Inside)
                {
                    return PointLocation.OutSide;
                }
            }

            return IsInsideByRayCast(solid, point, tolerance) ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Decides whether a point sits inside a closed boundary by counting ray crossings.
        /// </summary>
        /// <remarks>
        /// A ray that grazes an edge, or passes exactly through a vertex, is counted once by one face and
        /// twice or not at all by its neighbour, and the parity comes out wrong. Rather than trying to
        /// resolve those cases the ray is simply thrown again in another direction, since the chance of
        /// several unrelated directions all landing on an edge is negligible. The directions are fixed
        /// rather than random so that the same question always gets the same answer.
        /// </remarks>
        private static bool IsInsideByRayCast(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            GeoVector3[] directions =
            {
                new GeoVector3(0.5773502691896258, 0.5773502691896258, 0.5773502691896258),
                new GeoVector3(-0.2672612419124244, 0.5345224838248488, 0.8017837257372732),
                new GeoVector3(0.8017837257372732, -0.2672612419124244, 0.5345224838248488),
                new GeoVector3(0.4082482904638631, 0.8164965809277261, -0.4082482904638631),
                new GeoVector3(-0.6666666666666666, 0.3333333333333333, 0.6666666666666666)
            };

            // A crossing landing this close to the rim of a face is what makes the parity unreliable. The
            // band is wider than the point tolerance so that a graze is caught rather than counted.
            double grazeBand = tolerance.EqualPoint * 100.0;

            foreach (GeoVector3 direction in directions)
            {
                GeoRay3 ray = new GeoRay3(point, direction);
                int crossings = 0;
                bool ambiguous = false;

                foreach (GeoFace3 face in solid.Faces)
                {
                    if (!face.TryIntersectWith(ray, out GeoPoint3 hit, tolerance))
                    {
                        continue;
                    }

                    if (Distance3.DistanceTo(Projection3.ProjectToPolygonBoundary(face.Boundary, hit, tolerance), hit) <= grazeBand)
                    {
                        ambiguous = true;
                        break;
                    }

                    crossings++;
                }

                if (!ambiguous)
                {
                    return crossings % 2 == 1;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a solid holds a point, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoSolid3 solid, GeoPoint3 point) => Contains(solid, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a solid holds a point, within a tolerance.
        /// </summary>
        public static bool Contains(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(solid, point, tolerance) != PointLocation.OutSide;
        }

        #endregion

        #region Point and circle

        /// <summary>
        /// Locates a point relative to a circular disc, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoCircle3 circle, GeoPoint3 point) => Locate(circle, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to a circular disc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A disc is a planar region like a polygon, so a point counts as inside only when it lies on the
        /// carrying plane as well as within the radius: a point hovering above the centre is outside it.
        /// </remarks>
        public static PointLocation Locate(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            if (Math.Abs(circle.GetPlane().SignedDistanceTo(point)) > tolerance.EqualPlanar)
            {
                return PointLocation.OutSide;
            }

            double distance = circle.Center.DistanceTo(point);

            if (Math.Abs(distance - circle.Radius) <= tolerance.EqualPoint)
            {
                return PointLocation.OnSide;
            }

            return distance < circle.Radius ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a circular disc holds a point, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoCircle3 circle, GeoPoint3 point) => Contains(circle, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a circular disc holds a point, within a tolerance.
        /// </summary>
        public static bool Contains(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(circle, point, tolerance) != PointLocation.OutSide;
        }

        /// <summary>
        /// Checks whether a point lies on the circumference of a circle, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoCircle3 circle, GeoPoint3 point) => IsPointOn(circle, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the circumference of a circle, within a tolerance.
        /// </summary>
        public static bool IsPointOn(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(circle, point, tolerance) == PointLocation.OnSide;
        }

        #endregion

        #region Point and face

        /// <summary>
        /// Locates a point relative to a face, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoFace3 face, GeoPoint3 point) => Locate(face, point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point inside a hole is outside the face, and a point on the rim of a hole is on the face
        /// boundary, since the rim is as much an edge of the material as the outer loop is.
        /// </remarks>
        public static PointLocation Locate(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            PointLocation outer = Locate(face.Boundary, point, tolerance);

            if (outer != PointLocation.Inside)
            {
                return outer;
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                PointLocation inHole = Locate(hole, point, tolerance);

                if (inHole == PointLocation.OnSide)
                {
                    return PointLocation.OnSide;
                }

                if (inHole == PointLocation.Inside)
                {
                    return PointLocation.OutSide;
                }
            }

            return PointLocation.Inside;
        }

        /// <summary>
        /// Checks whether a face holds a point, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoFace3 face, GeoPoint3 point) => Contains(face, point, Tolerance.Global);

        /// <summary>
        /// Checks whether a face holds a point, within a tolerance.
        /// </summary>
        public static bool Contains(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            return Locate(face, point, tolerance) != PointLocation.OutSide;
        }

        #endregion
    }
}
