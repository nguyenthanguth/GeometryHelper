using System;
using System.Collections.Generic;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// What a curved loop in space can answer by working in its own plane: shaping it, cutting it, and
    /// combining it with another shape lying in that same plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoPolygonArc3"/> enforces coplanarity, which is what makes all of this exact rather than
    /// approximate: the loop is laid out as a <see cref="GeoPolygonArc2"/> in its own frame, the whole of the
    /// plane library answers, and the answer is lifted back. That is already how its area, its centroid, what
    /// is inside it, its offsets and its rounding are worked out.
    /// </para>
    /// <para>
    /// <b>A second shape has to lie in the loop's plane, and is refused when it does not.</b> Projecting it in
    /// would be convenient and quietly wrong — two stirrups a hundred apart would report as overlapping — and
    /// sampling is neither exact nor honest. Refusing is the reading the rest of the library already takes: the
    /// loop refuses a non-planar set of vertices when it is built, <c>PlanarMap.TryToArc2</c> returns false for
    /// an arc that is not in the frame, and an arc in space refuses to be measured where there is no closed
    /// form. This is the same answer to the same kind of question.
    /// </para>
    /// </remarks>
    public sealed partial class GeoPolygonArc3
    {
        #region Working in the loop's own plane

        /// <summary>
        /// Determines whether a plane is the plane this loop lies in.
        /// </summary>
        /// <param name="other">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the two are the same plane, so that work in the loop's frame is exact.</returns>
        /// <remarks>
        /// Parallel is not enough: two parallel planes a metre apart share nothing at all.
        /// </remarks>
        public bool SharesPlaneWith(GeoPlane3 other, Tolerance tolerance)
        {
            return Parallel3.IsParallel(GetPlane(), other, tolerance)
                && Containment3.IsPointOn(other, this[0], tolerance);
        }

        /// <summary>
        /// Lays another loop out in this one's frame, refusing it where it lies in another plane.
        /// </summary>
        private GeoPolygonArc2 Flatten(GeoPolygonArc3 other, GeoCoordinateSystem3 frame, Tolerance tolerance, string name)
        {
            if (other == null)
            {
                throw new ArgumentNullException(name);
            }

            if (!SharesPlaneWith(other.GetPlane(), tolerance))
            {
                throw new ArgumentException(
                    "The two loops lie in different planes, so there is no plane to work in. Bring them into one plane first.",
                    name);
            }

            return other.ProjectToPolygonArc2(frame);
        }

        /// <summary>
        /// Lays a polygon out in this loop's frame, refusing it where it lies in another plane.
        /// </summary>
        private GeoPolygon2 Flatten(GeoPolygon3 other, GeoCoordinateSystem3 frame, Tolerance tolerance, string name)
        {
            if (other == null)
            {
                throw new ArgumentNullException(name);
            }

            if (!SharesPlaneWith(other.GetPlane(), tolerance))
            {
                throw new ArgumentException(
                    "The polygon lies in another plane than the loop, so there is no plane to work in. Bring them into one plane first.",
                    name);
            }

            return PlanarMap.ProjectToPolygon2(frame, other);
        }

        /// <summary>
        /// Lifts the faces a boolean gave back in the plane into the loop's plane in space.
        /// </summary>
        private static GeoFace3[] Lift(GeoCoordinateSystem3 frame, GeoFace2[] flat)
        {
            var lifted = new GeoFace3[flat.Length];

            for (int i = 0; i < flat.Length; i++)
            {
                lifted[i] = PlanarMap.ToFace3(frame, flat[i]);
            }

            return lifted;
        }

        #endregion

        #region Combining with another shape in the same plane

        /// <summary>
        /// Joins this loop to another lying in the same plane.
        /// </summary>
        public GeoFace3[] Union(GeoPolygonArc3 other) => Union(other, Tolerance.Global);

        /// <summary>
        /// Joins this loop to another lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="other">The other loop; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance, which here decides only whether the two share a plane.</param>
        /// <returns>The faces the two cover between them, which may be more than one where they do not touch.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the other loop lies in a different plane.</exception>
        public GeoFace3[] Union(GeoPolygonArc3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Union(Flatten(other, frame, tolerance, nameof(other))));
        }

        /// <summary>
        /// Keeps what this loop and another lying in the same plane both cover.
        /// </summary>
        public GeoFace3[] Intersect(GeoPolygonArc3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this loop and another lying in the same plane both cover, within a tolerance.
        /// </summary>
        /// <param name="other">The other loop; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces both cover, which is empty where they overlap nowhere.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the other loop lies in a different plane.</exception>
        public GeoFace3[] Intersect(GeoPolygonArc3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Intersect(Flatten(other, frame, tolerance, nameof(other))));
        }

        /// <summary>
        /// Takes another loop lying in the same plane out of this one.
        /// </summary>
        public GeoFace3[] Subtract(GeoPolygonArc3 tool) => Subtract(tool, Tolerance.Global);

        /// <summary>
        /// Takes another loop lying in the same plane out of this one, within a tolerance.
        /// </summary>
        /// <param name="tool">The loop to take away; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>What is left of this loop, which may be more than one face and may carry holes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tool lies in a different plane.</exception>
        public GeoFace3[] Subtract(GeoPolygonArc3 tool, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Subtract(Flatten(tool, frame, tolerance, nameof(tool))));
        }

        /// <summary>
        /// Keeps what this loop and another lying in the same plane cover, but not both.
        /// </summary>
        public GeoFace3[] Xor(GeoPolygonArc3 other) => Xor(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this loop and another lying in the same plane cover, but not both, within a tolerance.
        /// </summary>
        /// <param name="other">The other loop; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces exactly one of the two covers.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the other loop lies in a different plane.</exception>
        public GeoFace3[] Xor(GeoPolygonArc3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Xor(Flatten(other, frame, tolerance, nameof(other))));
        }

        /// <summary>
        /// Joins this loop to a polygon lying in the same plane.
        /// </summary>
        public GeoFace3[] Union(GeoPolygon3 other) => Union(other, Tolerance.Global);

        /// <summary>
        /// Joins this loop to a polygon lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the polygon lies in a different plane.</exception>
        public GeoFace3[] Union(GeoPolygon3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Union(Flatten(other, frame, tolerance, nameof(other))));
        }

        /// <summary>
        /// Keeps what this loop and a polygon lying in the same plane both cover.
        /// </summary>
        public GeoFace3[] Intersect(GeoPolygon3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this loop and a polygon lying in the same plane both cover, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the polygon lies in a different plane.</exception>
        public GeoFace3[] Intersect(GeoPolygon3 other, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Intersect(Flatten(other, frame, tolerance, nameof(other))));
        }

        /// <summary>
        /// Takes a polygon lying in the same plane out of this loop.
        /// </summary>
        public GeoFace3[] Subtract(GeoPolygon3 tool) => Subtract(tool, Tolerance.Global);

        /// <summary>
        /// Takes a polygon lying in the same plane out of this loop, within a tolerance.
        /// </summary>
        /// <param name="tool">The polygon to take away; it has to lie in this loop's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the tool lies in a different plane.</exception>
        public GeoFace3[] Subtract(GeoPolygon3 tool, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return Lift(frame, ProjectToPolygonArc2(frame).Subtract(Flatten(tool, frame, tolerance, nameof(tool))));
        }

        #endregion

        #region Shaping

        /// <summary>
        /// Cuts every corner of the loop back by the same distance.
        /// </summary>
        public GeoPolygonArc3 Chamfer(double distance) => Chamfer(distance, Tolerance.Global);

        /// <summary>
        /// Cuts every corner of the loop back by the same distance, within a tolerance.
        /// </summary>
        /// <param name="distance">How far back along each leg to cut.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with its corners mitred.</returns>
        public GeoPolygonArc3 Chamfer(double distance, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return new GeoPolygonArc3(frame, ProjectToPolygonArc2(frame).Chamfer(distance, distance, tolerance));
        }

        /// <summary>
        /// Rounds one named corner of the loop and reports whether it had room.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc3 result)
            => TryFilletAt(index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one named corner of the loop and reports whether it had room, within a tolerance.
        /// </summary>
        /// <param name="index">Which corner to round.</param>
        /// <param name="radius">The radius to round it by.</param>
        /// <param name="result">The loop with that corner rounded, when it fitted.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner had room for the radius; otherwise, false, and the loop is unchanged.</returns>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc3 result, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            if (ProjectToPolygonArc2(frame).TryFilletAt(index, radius, out GeoPolygonArc2 rounded, tolerance))
            {
                result = new GeoPolygonArc3(frame, rounded);
                return true;
            }

            result = this;
            return false;
        }

        /// <summary>
        /// Cuts one named corner of the loop back and reports whether it had room.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygonArc3 result)
            => TryChamferAt(index, distance1, distance2, out result, Tolerance.Global);

        /// <summary>
        /// Cuts one named corner of the loop back and reports whether it had room, within a tolerance.
        /// </summary>
        /// <param name="index">Which corner to cut.</param>
        /// <param name="distance1">How far back along the leg arriving at the corner.</param>
        /// <param name="distance2">How far back along the leg leaving it.</param>
        /// <param name="result">The loop with that corner mitred, when it fitted.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner had room; otherwise, false, and the loop is unchanged.</returns>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygonArc3 result, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            if (ProjectToPolygonArc2(frame).TryChamferAt(index, distance1, distance2, out GeoPolygonArc2 mitred, tolerance))
            {
                result = new GeoPolygonArc3(frame, mitred);
                return true;
            }

            result = this;
            return false;
        }

        #endregion

        #region Cutting

        /// <summary>
        /// Cuts the loop at a point on it.
        /// </summary>
        /// <param name="point">The point to cut at; it has to be on the loop.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <returns>true when the cut was made; otherwise, false, and the loop comes back opened at its start.</returns>
        public bool TrySplitBy(GeoPoint3 point, out GeoPolylineArc3[] pieces)
            => ArcChain3.TrySplitBy(this, point, out pieces);

        /// <summary>
        /// Cuts the loop at a point on it, within a tolerance.
        /// </summary>
        /// <param name="point">The point to cut at; it has to be on the loop.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the cut was made; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint3 point, out GeoPolylineArc3[] pieces, Tolerance tolerance)
            => ArcChain3.TrySplitBy(this, point, out pieces, tolerance);

        /// <summary>
        /// Cuts the loop wherever it crosses a plane.
        /// </summary>
        /// <param name="cutter">The plane to cut at.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolylineArc3[] pieces)
            => ArcChain3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the loop wherever it crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="cutter">The plane to cut at.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        /// <remarks>
        /// A plane through a stirrup cuts it twice, so the usual answer is two chains.
        /// </remarks>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
            => ArcChain3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Cuts the loop wherever it crosses a face.
        /// </summary>
        /// <param name="cutter">The face to cut at; only its material cuts.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool TrySplitBy(GeoFace3 cutter, out GeoPolylineArc3[] pieces)
            => ArcChain3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the loop wherever it crosses a face, within a tolerance.
        /// </summary>
        /// <param name="cutter">The face to cut at; only its material cuts.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool TrySplitBy(GeoFace3 cutter, out GeoPolylineArc3[] pieces, Tolerance tolerance)
            => ArcChain3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Cuts the loop where it crosses the surface of a solid, telling what is in from what is out.
        /// </summary>
        /// <param name="cutter">The body to cut against.</param>
        /// <param name="inside">The chains within the material.</param>
        /// <param name="outside">The chains outside it.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => ArcChain3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Cuts the loop where it crosses the surface of a solid, telling what is in from what is out, within a
        /// tolerance.
        /// </summary>
        /// <param name="cutter">The body to cut against.</param>
        /// <param name="inside">The chains within the material.</param>
        /// <param name="outside">The chains outside it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        /// <remarks>
        /// This is a stirrup trimmed to a member: what is in the concrete and what is sticking out of it.
        /// </remarks>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside, Tolerance tolerance)
            => ArcChain3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Cuts the loop at a set of distances measured along it.
        /// </summary>
        /// <param name="distances">How far along to cut, measured along the arcs.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool SplitAtDistances(IEnumerable<double> distances, out GeoPolylineArc3[] pieces)
            => ArcChain3.SplitAtDistances(this, distances, out pieces);

        /// <summary>
        /// Cuts the loop at a set of distances measured along it, within a tolerance.
        /// </summary>
        /// <param name="distances">How far along to cut, measured along the arcs.</param>
        /// <param name="pieces">The open chains the loop came apart into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false.</returns>
        public bool SplitAtDistances(IEnumerable<double> distances, out GeoPolylineArc3[] pieces, Tolerance tolerance)
            => ArcChain3.SplitAtDistances(this, distances, out pieces, tolerance);

        #endregion
    }
}
