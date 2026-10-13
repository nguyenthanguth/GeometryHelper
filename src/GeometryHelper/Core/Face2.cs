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
    /// <para>
    /// The distance reads the face by its material: nought where the probe collides with it, and otherwise the
    /// gap between the boundaries, which is then the gap to the material. A probe wholly inside a hole is as
    /// far away as the rim of that hole, and a probe holding the whole face collides with it. Two faces are
    /// asked the same way, each one's material against the other's.
    /// </para>
    /// </remarks>
    public static partial class Face2
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
        /// Gets every point where the boundaries of two faces cross.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoFace2 other) => GetIntersections(face, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where the boundaries of two faces cross, within a tolerance.
        /// </summary>
        /// <param name="face">The first face.</param>
        /// <param name="other">The second face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The crossings of the outline and then each hole of <paramref name="face"/>, each against the outline and then
        /// each hole of <paramref name="other"/>, in that order, as a polygon's crossings with a face are gathered. The
        /// square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle crosses the square 20 by 20
        /// about (30, 50) twice, at (30, 40) and (30, 60), on the rim of the hole and nowhere on its outline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoFace2 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Crossings(face, rim => Crossings(other, theirs => rim.GetIntersections(theirs, tolerance)));
        }

        /// <summary>
        /// Determines whether a segment reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoLine2 line) => CollidesWith(face, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment collides where it crosses a rim, lies on the material, or comes within the point tolerance of a rim,
        /// the outline from outside or a hole's from within the hole. At a tolerance of a thousandth, the segment from (41, 58) to
        /// (50, 59.9995), in the hole from (40, 40) to (60, 60) of the square 100 by 100, reaches the material half a
        /// thousandth below the hole's top.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoLine2 line, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(line, tolerance), rim => rim.GetIntersections(line, tolerance), () => line.StartPoint, null, tolerance, hole => Collision2.ComeWithin(ToArray(hole.GetEdges()), line, tolerance));

        /// <summary>
        /// Determines whether a arc reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoArc2 arc) => CollidesWith(face, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a arc reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The arc collides where it crosses a rim, lies on the material, or comes within the point tolerance of a rim,
        /// the outline from outside or a hole's from within the hole. At a tolerance of a thousandth, an arc ending half
        /// a thousandth above the top of the square 100 by 100 collides with it, and one ending two thousandths above
        /// does not.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoArc2 arc, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(arc, tolerance), rim => rim.GetIntersections(arc, tolerance), () => arc.StartPoint, null, tolerance, hole => ArcChain2.ComeWithin(ArcChain2.EdgesOf(hole), arc, tolerance));

        /// <summary>
        /// Determines whether a circle reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoCircle2 circle) => CollidesWith(face, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a circle reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The circle, read as its disc, collides where it reaches the material or comes within the point tolerance of
        /// a rim, a hole's from within the hole included.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(circle, tolerance), rim => rim.GetIntersections(circle, tolerance), () => circle.Center, at => circle.Contains(at, tolerance), tolerance, hole => NearDisc(hole, circle, tolerance));

        /// <summary>
        /// Determines whether a rectangle reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoRectangle2 rect) => CollidesWith(face, rect, Tolerance.Global);

        /// <summary>
        /// Determines whether a rectangle reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The rectangle collides where it crosses a rim, where its material and the face's overlap, or where it comes
        /// within the point tolerance of a rim, the outline from outside or a hole's from within the hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(rect, tolerance), rim => rim.GetIntersections(rect, tolerance), () => rect.Center, at => Containment2.Contains(rect, at, tolerance), tolerance, hole => Collision2.ComeWithin(ToArray(hole.GetEdges()), rect.GetEdges(), tolerance));

        /// <summary>
        /// Determines whether a polyline reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolyline2 polyline) => CollidesWith(face, polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether a polyline reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polyline collides where it crosses a rim, lies on the material, or comes within the point tolerance of a
        /// rim, the outline from outside or a hole's from within the hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(polyline, tolerance), rim => rim.GetIntersections(polyline, tolerance), () => polyline.Vertices[0], null, tolerance, hole => Collision2.ComeWithin(ToArray(hole.GetEdges()), ToArray(polyline.GetEdges()), tolerance));

        /// <summary>
        /// Determines whether a polygon reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygon2 polygon) => CollidesWith(face, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon collides where it crosses a rim, where its material and the face's overlap, or where it comes
        /// within the point tolerance of a rim, the outline from outside or a hole's from within the hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(polygon, tolerance), rim => rim.GetIntersections(polygon, tolerance), () => polygon.Vertices[0], at => polygon.Contains(at, tolerance), tolerance, hole => Collision2.ComeWithin(ToArray(hole.GetEdges()), ToArray(polygon.GetEdges()), tolerance));

        /// <summary>
        /// Determines whether a curved loop reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolygonArc2 loop) => CollidesWith(face, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The loop collides where it crosses a rim, where its material and the face's overlap, or where it comes within
        /// the point tolerance of a rim, the outline from outside or a hole's from within the hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(loop, tolerance), rim => rim.GetIntersections(loop, tolerance), () => loop.Vertices[0], at => loop.Contains(at, tolerance), tolerance, hole => ArcChain2.ComeWithin(ArcChain2.EdgesOf(hole), ArcChain2.EdgesOf(loop), tolerance));

        /// <summary>
        /// Determines whether a curved chain reaches the material of a face.
        /// </summary>
        public static bool CollidesWith(GeoFace2 face, GeoPolylineArc2 chain) => CollidesWith(face, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain reaches the material of a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The chain collides where it crosses a rim, lies on the material, or comes within the point tolerance of a rim,
        /// the outline from outside or a hole's from within the hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance) => Touches(face, rim => rim.CollidesWith(chain, tolerance), rim => rim.GetIntersections(chain, tolerance), () => chain.Vertices[0], null, tolerance, hole => ArcChain2.ComeWithin(ArcChain2.EdgesOf(hole), ArcChain2.EdgesOf(chain), tolerance));

        /// <summary>
        /// Determines whether the materials of two faces meet.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool CollidesWith(GeoFace2 face, GeoFace2 other) => CollidesWith(face, other, Tolerance.Global);

        /// <summary>
        /// Determines whether the materials of two faces meet, within a tolerance.
        /// </summary>
        /// <param name="face">The first face.</param>
        /// <param name="other">The second face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the two overlap or touch, one lying on the other's material included; false where they are apart,
        /// and where one lies wholly in a hole of the other. A square 20 by 20 about (50, 50) does not collide with the
        /// square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle; about (15, 50) it does.
        /// </returns>
        /// <remarks>
        /// The other face is the probe, asked as a polygon is: it has to reach the outline, through
        /// <see cref="CollidesWith(GeoFace2, GeoPolygon2, Tolerance)"/>, and no hole may hold it whole. A face that
        /// crosses no rim of a hole lies wholly inside it, wholly outside it, or holds it, and the first is told by a
        /// corner of the probe's outline falling inside the hole while the probe's material does not hold a corner of
        /// the hole. No boolean is made.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool CollidesWith(GeoFace2 face, GeoFace2 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Touches(face, rim => CollidesWith(other, rim, tolerance), rim => GetIntersections(other, rim, tolerance), () => other.Boundary.Vertices[0], at => Containment2.Contains(other, at, tolerance), tolerance, hole => Collision2.ComeWithin(ToArray(hole.GetEdges()), ToArray(other.Boundary.GetEdges()), tolerance));
        }

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
        /// Gets the shortest segment leaving the boundary of a face and landing on the boundary of another.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoFace2 other) => GetShortestLineTo(face, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on the boundary of another, within a
        /// tolerance.
        /// </summary>
        /// <param name="face">The face the answer leaves.</param>
        /// <param name="other">The face the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each loop of <paramref name="face"/>, its outline and then each hole, to
        /// each loop of <paramref name="other"/> in the same order, the first such pair winning a tie. Like every shortest
        /// line here it runs boundary to boundary, so it has a length even where the materials overlap. From the square
        /// 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the square 20 by 20 about (45, 50)
        /// is reached from the rim of the hole across the gap of 5 on its left.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoFace2 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Nearest(face, rim => Nearest(other, theirs => rim.GetShortestLineTo(theirs, tolerance)));
        }

        #region Distances

        /// <summary>
        /// Gets the distance from the material of a face to a segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoLine2 line) => DistanceTo(face, line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a segment, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the segment crosses a rim or lies on the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoLine2, Tolerance)"/>. In the square 100 by
        /// 100 with a corner at the origin and a hole 40 by 40 in its middle, the segment from (40, 50) to (60, 50) lies
        /// in the hole and is 10 from its rim; the one from (10, 50) to (20, 50) lies on the material and is nought away.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoLine2 line, Tolerance tolerance)
            => CrossesOrLiesOn(face, line, tolerance) ? 0.0 : GetShortestLineTo(face, line, tolerance).Length;

        /// <summary>
        /// Whether a segment crosses a rim of a face or lies on its material: where the distance is taken as nought
        /// without measuring.
        /// </summary>
        internal static bool CrossesOrLiesOn(GeoFace2 face, GeoLine2 line, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(rim, line, tolerance), rim => rim.GetIntersections(line, tolerance), () => line.StartPoint, null, tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoArc2 arc) => DistanceTo(face, arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to an arc, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the arc crosses a rim or lies on the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoArc2, Tolerance)"/>. In the square 100 by
        /// 100 with a corner at the origin and a hole 40 by 40 in its middle, the upper half of the circle of radius 10
        /// about (50, 50) lies in the hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoArc2 arc, Tolerance tolerance)
            => CrossesOrLiesOn(face, arc, tolerance) ? 0.0 : GetShortestLineTo(face, arc, tolerance).Length;

        /// <summary>
        /// Whether an arc crosses a rim of a face or lies on its material: where the distance is taken as nought
        /// without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoArc2 arc, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(rim, arc, tolerance), rim => rim.GetIntersections(arc, tolerance), () => arc.StartPoint, null, tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoCircle2 circle) => DistanceTo(face, circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a circle, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="circle">The circle, a region.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the circle crosses a rim or reaches the material, a circle holding the whole face among them;
        /// otherwise the length of
        /// <see cref="GetShortestLineTo(GeoFace2, GeoCircle2, Tolerance)"/>. In the square 100 by 100 with a corner at the
        /// origin and a hole 40 by 40 in its middle, the circle of radius 10 about (50, 50) lies in the hole and is 10
        /// from its rim; about (150, 50) it is 40 from the outline; about (15, 50), and of radius 200 about (50, 50), it
        /// is nought away.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance)
            => CrossesOrLiesOn(face, circle, tolerance) ? 0.0 : GetShortestLineTo(face, circle, tolerance).Length;

        /// <summary>
        /// Whether a circle's disc crosses a rim of a face or lies on its material: where the distance is taken as
        /// nought without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoCircle2 circle, Tolerance tolerance)
            => Touches(face, rim => rim.CollidesWith(circle, tolerance), rim => rim.GetIntersections(circle, tolerance), () => circle.Center, at => circle.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a rectangle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoRectangle2 rect) => DistanceTo(face, rect, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a rectangle, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="rect">The rectangle, a region.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the rectangle crosses a rim or overlaps the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoRectangle2, Tolerance)"/>. In the
        /// square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the rectangle 20 by 20 about
        /// (50, 50) lies in the hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance)
            => CrossesOrLiesOn(face, rect, tolerance) ? 0.0 : GetShortestLineTo(face, rect, tolerance).Length;

        /// <summary>
        /// Whether a rectangle crosses a rim of a face or overlaps its material: where the distance is taken as nought
        /// without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoRectangle2 rect, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(rect, rim, tolerance), rim => rim.GetIntersections(rect, tolerance), () => rect.Center, at => Containment2.Contains(rect, at, tolerance), tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polyline is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolyline2 polyline) => DistanceTo(face, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a polyline, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="polyline">The polyline, a curve.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the chain crosses a rim or lies on the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoPolyline2, Tolerance)"/>. In the
        /// square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the chain (40, 40), (60, 40),
        /// (60, 60) lies in the hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polyline is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance)
        {
            Required(face, polyline, nameof(polyline));

            return CrossesOrLiesOn(face, polyline, tolerance) ? 0.0 : GetShortestLineTo(face, polyline, tolerance).Length;
        }

        /// <summary>
        /// Whether a polyline crosses a rim of a face or lies on its material: where the distance is taken as nought
        /// without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoPolyline2 polyline, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(polyline, rim, tolerance), rim => rim.GetIntersections(polyline, tolerance), () => polyline.Vertices[0], null, tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolygon2 polygon) => DistanceTo(face, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a polygon, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="polygon">The polygon, a region.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the polygon crosses a rim or overlaps the material, a polygon holding the whole face among them;
        /// otherwise the length of
        /// <see cref="GetShortestLineTo(GeoFace2, GeoPolygon2, Tolerance)"/>. In the square 100 by 100 with a corner at the
        /// origin and a hole 40 by 40 in its middle, the square 20 by 20 about (50, 50) lies in the hole and is 10 from
        /// its rim; about (130, 50) it is 20 from the outline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance)
        {
            Required(face, polygon, nameof(polygon));

            return CrossesOrLiesOn(face, polygon, tolerance) ? 0.0 : GetShortestLineTo(face, polygon, tolerance).Length;
        }

        /// <summary>
        /// Whether a polygon crosses a rim of a face or overlaps its material: where the distance is taken as nought
        /// without measuring.
        /// </summary>
        internal static bool CrossesOrLiesOn(GeoFace2 face, GeoPolygon2 polygon, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(rim, polygon, tolerance), rim => rim.GetIntersections(polygon, tolerance), () => polygon.Vertices[0], at => polygon.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a curved loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the loop is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolygonArc2 loop) => DistanceTo(face, loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="loop">The loop, a region.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the loop crosses a rim or overlaps the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoPolygonArc2, Tolerance)"/>,
        /// measured on the arcs. In the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle,
        /// the loop with corners (40, 40), (60, 40), (60, 60), (40, 60) and straight edges lies in the hole and is 10
        /// from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the loop is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            Required(face, loop, nameof(loop));

            return CrossesOrLiesOn(face, loop, tolerance) ? 0.0 : GetShortestLineTo(face, loop, tolerance).Length;
        }

        /// <summary>
        /// Whether a curved loop crosses a rim of a face or overlaps its material: where the distance is taken as
        /// nought without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoPolygonArc2 loop, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(loop, rim, tolerance), rim => rim.GetIntersections(loop, tolerance), () => loop.Vertices[0], at => loop.Contains(at, tolerance), tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a curved chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the chain is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolylineArc2 chain) => DistanceTo(face, chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="chain">The chain, a curve.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the chain crosses a rim or lies on the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoPolylineArc2, Tolerance)"/>,
        /// measured on the arcs. In the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle,
        /// the straight chain (40, 40), (60, 40), (60, 60) lies in the hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the chain is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            Required(face, chain, nameof(chain));

            return CrossesOrLiesOn(face, chain, tolerance) ? 0.0 : GetShortestLineTo(face, chain, tolerance).Length;
        }

        /// <summary>
        /// Whether a curved chain crosses a rim of a face or lies on its material: where the distance is taken as
        /// nought without measuring.
        /// </summary>
        private static bool CrossesOrLiesOn(GeoFace2 face, GeoPolylineArc2 chain, Tolerance tolerance)
            => Touches(face, rim => Collision2.CrossesOrStartsIn(chain, rim, tolerance), rim => rim.GetIntersections(chain, tolerance), () => chain.Vertices[0], null, tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to an edge.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoEdge2 edge) => DistanceTo(face, edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to an edge, within a tolerance: as a segment or as an arc,
        /// whichever the edge is.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// <see cref="DistanceTo(GeoFace2, GeoLine2, Tolerance)"/> or <see cref="DistanceTo(GeoFace2, GeoArc2, Tolerance)"/>
        /// of the edge. In the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the
        /// straight edge from (40, 50) to (60, 50) lies in the hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc ? DistanceTo(face, edge.ToArc(), tolerance) : DistanceTo(face, edge.ToLine(), tolerance);

        /// <summary>
        /// Gets the distance from the material of a face to a triangle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoTriangle2 triangle) => DistanceTo(face, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a triangle, within a tolerance.
        /// </summary>
        /// <param name="face">The face: its outline, less its holes.</param>
        /// <param name="triangle">The triangle, a region; one with corners on each other is read as its longest edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the triangle crosses a rim or overlaps the material; otherwise the length of
        /// <see cref="Triangle2.GetShortestLineTo(GeoTriangle2, GeoFace2, Tolerance)"/>. In the square 100 by 100 with a
        /// corner at the origin and a hole 40 by 40 in its middle, the triangle (40, 40), (60, 40), (50, 60) lies in the
        /// hole and is 10 from its rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoTriangle2 triangle, Tolerance tolerance)
        {
            Required(face, nameof(face));

            bool meets = Triangle2.TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? CrossesOrLiesOn(face, shape, tolerance)
                : CrossesOrLiesOn(face, hull, tolerance);

            return meets ? 0.0 : Triangle2.GetShortestLineTo(triangle, face, tolerance).Length;
        }

        /// <summary>
        /// Gets the distance between the materials of two faces.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoFace2 other) => DistanceTo(face, other, Tolerance.Global);

        /// <summary>
        /// Gets the distance between the materials of two faces, within a tolerance.
        /// </summary>
        /// <param name="face">The first face.</param>
        /// <param name="other">The second face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the materials cross or overlap; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoFace2, Tolerance)"/>. A square 20 by 20 about (50, 50) is
        /// 10 from the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the width of the
        /// gap round it in the hole; about (130, 50) it is 20 away.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoFace2 other, Tolerance tolerance)
            => CrossesOrLiesOn(face, other, tolerance) ? 0.0 : GetShortestLineTo(face, other, tolerance).Length;

        /// <summary>
        /// Whether the materials of two faces cross or overlap: where the distance is taken as nought without measuring.
        /// </summary>
        internal static bool CrossesOrLiesOn(GeoFace2 face, GeoFace2 other, Tolerance tolerance)
            => Touches(face, rim => CrossesOrLiesOn(other, rim, tolerance), rim => GetIntersections(other, rim, tolerance), () => other.Boundary.Vertices[0], at => Containment2.Contains(other, at, tolerance), tolerance);

        /// <summary>
        /// Whether a circle's disc comes within the point tolerance of a polygon's rim.
        /// </summary>
        private static bool NearDisc(GeoPolygon2 rim, GeoCircle2 circle, Tolerance tolerance)
        {
            foreach (GeoLine2 edge in rim.GetEdges())
            {
                if (Distance2.DistanceTo(circle, edge, tolerance) <= tolerance.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The edges of a rim as a list, for measuring against.
        /// </summary>
        private static GeoLine2[] ToArray(IEnumerable<GeoLine2> edges) => new List<GeoLine2>(edges).ToArray();

        #endregion

        /// <summary>
        /// Refuses a null face.
        /// </summary>
        private static void Required(GeoFace2 face, string name)
        {
            if (face == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        /// <summary>
        /// Refuses a null face, and then a null probe.
        /// </summary>
        private static void Required(GeoFace2 face, object probe, string name)
        {
            Required(face, nameof(face));

            if (probe == null)
            {
                throw new ArgumentNullException(name);
            }
        }

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
        /// <param name="nearRim">Whether the probe comes within the point tolerance of a hole's rim, which keeps the hole from holding it; null not to ask.</param>
        private static bool Touches(
            GeoFace2 face,
            Func<GeoPolygon2, bool> collides,
            Func<GeoPolygon2, GeoPoint2[]> crossings,
            Func<GeoPoint2> somewhereOnProbe,
            Func<GeoPoint2, bool> probeHolds,
            Tolerance tolerance,
            Func<GeoPolygon2, bool> nearRim = null)
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
                if (Swallowed(hole, crossings(hole), on, probeHolds, tolerance) && (nearRim == null || !nearRim(hole)))
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
