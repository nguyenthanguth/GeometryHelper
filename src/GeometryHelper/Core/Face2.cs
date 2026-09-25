using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What a face meets, and how far off it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A face is an outline with holes in it, so none of this is the polygon answer: the boundary of a face
    /// is its outline together with the rim of every hole, and the material is what lies inside the outline
    /// and outside all of them.
    /// </para>
    /// <para>
    /// Crossings are the union over that boundary, and the shortest joining segment is the shortest over it,
    /// which is why a probe standing on the material still gets a segment with a length: it is the reach to
    /// the nearest edge of the material, the same reading
    /// <see cref="Projection2.ProjectToFace(GeoFace2, GeoPoint2)"/> gives.
    /// </para>
    /// <para>
    /// Touching is the one that needs care. A probe collides with the face when it collides with the outline
    /// and no hole has swallowed it whole. A probe that crosses no rim of a hole is either wholly inside that
    /// hole, or wholly outside it, or holds the hole within itself, and those are told apart by asking where
    /// one point of the probe falls and, for a probe that has an inside, whether the rim falls within it.
    /// </para>
    /// </remarks>
    public static class Face2
    {
        /// <summary>
        /// Gets every point where a segment crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoLine2 line) => GetIntersections(face, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where a segment crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoLine2 line, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(line, tolerance));

        /// <summary>
        /// Gets every point where a arc crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoArc2 arc) => GetIntersections(face, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where a arc crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoArc2 arc, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(arc, tolerance));

        /// <summary>
        /// Gets every point where a circle crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoCircle2 circle) => GetIntersections(face, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a circle crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(circle, tolerance));

        /// <summary>
        /// Gets every point where a rectangle crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoRectangle2 rect) => GetIntersections(face, rect, Tolerance.Global);

        /// <summary>
        /// Gets every point where a rectangle crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(rect, tolerance));

        /// <summary>
        /// Gets every point where a polyline crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolyline2 polyline) => GetIntersections(face, polyline, Tolerance.Global);

        /// <summary>
        /// Gets every point where a polyline crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(polyline, tolerance));

        /// <summary>
        /// Gets every point where a polygon crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolygon2 polygon) => GetIntersections(face, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where a polygon crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(polygon, tolerance));

        /// <summary>
        /// Gets every point where a curved loop crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolygonArc2 loop) => GetIntersections(face, loop, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(loop, tolerance));

        /// <summary>
        /// Gets every point where a curved chain crosses the boundary of a face.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolylineArc2 chain) => GetIntersections(face, chain, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses the boundary of a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance) => Crossings(face, rim => rim.GetIntersections(chain, tolerance));

        /// <summary>
        /// Determines whether a segment reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoLine2 line) => CollidesWith(face, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoLine2 line, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(line, tolerance), rim => rim.GetIntersections(line, tolerance), () => line.StartPoint, null, tolerance);

        /// <summary>
        /// Determines whether a arc reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoArc2 arc) => CollidesWith(face, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a arc reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoArc2 arc, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(arc, tolerance), rim => rim.GetIntersections(arc, tolerance), () => arc.StartPoint, null, tolerance);

        /// <summary>
        /// Determines whether a circle reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoCircle2 circle) => CollidesWith(face, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a circle reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(circle, tolerance), rim => rim.GetIntersections(circle, tolerance), () => circle.Center, at => circle.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Determines whether a rectangle reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoRectangle2 rect) => CollidesWith(face, rect, Tolerance.Global);

        /// <summary>
        /// Determines whether a rectangle reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(rect, tolerance), rim => rim.GetIntersections(rect, tolerance), () => rect.Center, at => rect.Contains(at), tolerance);

        /// <summary>
        /// Determines whether a polyline reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolyline2 polyline) => CollidesWith(face, polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether a polyline reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(polyline, tolerance), rim => rim.GetIntersections(polyline, tolerance), () => polyline.Vertices[0], null, tolerance);

        /// <summary>
        /// Determines whether a polygon reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygon2 polygon) => CollidesWith(face, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(polygon, tolerance), rim => rim.GetIntersections(polygon, tolerance), () => polygon.Vertices[0], at => polygon.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Determines whether a curved loop reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygonArc2 loop) => CollidesWith(face, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(loop, tolerance), rim => rim.GetIntersections(loop, tolerance), () => loop.Vertices[0], at => loop.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Determines whether a curved chain reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolylineArc2 chain) => CollidesWith(face, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain reaches the material of a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(chain, tolerance), rim => rim.GetIntersections(chain, tolerance), () => chain.Vertices[0], null, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a point.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPoint2 point) => GetShortestLineTo(face, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a point, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPoint2 point, Tolerance tolerance) => new GeoLine2(Projection2.ProjectToFace(face, point, tolerance), point);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a segment.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoLine2 line) => GetShortestLineTo(face, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a segment, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoLine2 line, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(line, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoArc2 arc) => GetShortestLineTo(face, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a arc, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoArc2 arc, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(arc, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a circle.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoCircle2 circle) => GetShortestLineTo(face, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a circle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(circle, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a rectangle.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoRectangle2 rect) => GetShortestLineTo(face, rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a rectangle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(rect, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a polyline.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolyline2 polyline) => GetShortestLineTo(face, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a polyline, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(polyline, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a polygon.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolygon2 polygon) => GetShortestLineTo(face, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a polygon, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(polygon, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolygonArc2 loop) => GetShortestLineTo(face, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a curved loop, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(loop, tolerance));

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolylineArc2 chain) => GetShortestLineTo(face, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a curved chain, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance) => Nearest(face, rim => rim.GetShortestLineTo(chain, tolerance));

        /// <summary>
        /// Gathers what each of the outline and the rims answers into one.
        /// </summary>
        private static GeoPoint2[] Crossings(GeoFace2 face, Func<GeoPolygon2, GeoPoint2[]> of)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var found = new List<GeoPoint2>(of(face.Boundary));

            foreach (GeoPolygon2 hole in face.Holes)
            {
                found.AddRange(of(hole));
            }

            return found.ToArray();
        }

        /// <summary>
        /// Keeps the shortest of what the outline and the rims answer.
        /// </summary>
        private static GeoLine2 Nearest(GeoFace2 face, Func<GeoPolygon2, GeoLine2> of)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoLine2 best = of(face.Boundary);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                GeoLine2 candidate = of(hole);

                if (candidate.Length < best.Length)
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Decides whether a probe reaches the material: it must reach the outline and no hole may hold it all.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="collides">Whether the probe touches one loop of the face.</param>
        /// <param name="crossings">Where the probe crosses one loop of the face.</param>
        /// <param name="somewhereOnProbe">One point of the probe, asked for only once the probe is known to be there.</param>
        /// <param name="probeHolds">Whether the probe holds a point, or null for a probe with no inside.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool Touches(
            GeoFace2 face,
            Func<GeoPolygon2, bool> collides,
            Func<GeoPolygon2, GeoPoint2[]> crossings,
            Func<GeoPoint2> somewhereOnProbe,
            Func<GeoPoint2, bool> probeHolds,
            Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!collides(face.Boundary))
            {
                return false;
            }

            GeoPoint2 on = somewhereOnProbe();

            foreach (GeoPolygon2 hole in face.Holes)
            {
                if (Swallowed(hole, crossings(hole), on, probeHolds, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Decides whether a hole holds the whole of a probe, so that the probe reaches no material at all.
        /// </summary>
        /// <remarks>
        /// With no crossing of the rim the two cannot be interleaved, so a point of the probe inside the hole
        /// settles it unless the probe is closed and the rim lies within the probe, which is the other way a
        /// point of the probe can fall inside: a ring drawn around the hole.
        /// </remarks>
        private static bool Swallowed(
            GeoPolygon2 hole,
            GeoPoint2[] crossings,
            GeoPoint2 somewhereOnProbe,
            Func<GeoPoint2, bool> probeHolds,
            Tolerance tolerance)
        {
            if (crossings.Length > 0)
            {
                return false;
            }

            if (Containment2.Locate(hole, somewhereOnProbe, tolerance) != PointLocation.Inside)
            {
                return false;
            }

            return probeHolds == null || !probeHolds(hole.Vertices[0]);
        }
    }
}
