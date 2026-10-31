using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting an area in the plane into areas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoPolygon3"/> and a <see cref="GeoFace3"/> could be cut in two and a
    /// <see cref="GeoPolygon2"/> and a <see cref="GeoFace2"/> could not, though the plane is where that is
    /// usually wanted. Everything already cut in the plane — segments, chains, loops — came back as
    /// <i>lines</i>; nothing came back as an area.
    /// </para>
    /// <para>
    /// Nothing new is worked out here. The shape is laid flat in the world frame at z = 0, cut by the exact
    /// arithmetic <see cref="Splition3"/> already holds, and projected back. Lifting adds a nought and
    /// projecting drops it, so the round trip costs no accuracy, and the two readings cannot drift apart
    /// because there is only one of them.
    /// </para>
    /// <para>
    /// The cutter is a <see cref="GeoLine2"/> read as the <b>whole straight line through it</b>, which is what
    /// a <see cref="GeoPlane3"/> is to space: unbounded, and dividing everything into two sides. Its length is
    /// ignored — a segment crossing half the shape cuts all of it. Where a bounded cutter is meant, the cut
    /// line overload takes a chain that has to start and finish on the boundary.
    /// </para>
    /// </remarks>
    public static partial class Splition2
    {
        #region Cutting an area by a line

        /// <summary>
        /// Cuts a loop by the straight line through a segment.
        /// </summary>
        public static bool TrySplitBy(GeoPolygon2 polygon, GeoLine2 cutter, out GeoPolygon2[] left, out GeoPolygon2[] right)
            => TrySplitBy(polygon, cutter, out left, out right, Tolerance.Global);

        /// <summary>
        /// Cuts a loop by the straight line through a segment, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop to cut.</param>
        /// <param name="cutter">A segment, read as the whole straight line through it; its length is ignored.</param>
        /// <param name="left">The pieces on the left of the cutter's direction of travel.</param>
        /// <param name="right">The pieces on its right.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the line divided the loop, leaving something on both sides; otherwise, false, and the loop
        /// comes back whole on the side it lies on.
        /// </returns>
        /// <remarks>
        /// Left and right are read from the cutter's own direction, so reversing the cutter swaps the two
        /// answers and nothing else. A concave loop can give several pieces to a side, which is why each side
        /// is an array.
        /// <para>
        /// The loop and the pieces are taken corner for corner at the tolerance asked, never at the global one: at a
        /// millionth, the line through (0.0005, 0) and (0, 0.0005) cuts the square 10 by 10 at the origin into its corner, a
        /// triangle of 0.000000125 square units, and the rest, where the global tolerance of a thousandth read the corner as
        /// no polygon and threw.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TrySplitBy(GeoPolygon2 polygon, GeoLine2 cutter, out GeoPolygon2[] left, out GeoPolygon2[] right, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TryCuttingPlane(cutter, tolerance, out GeoPlane3 plane))
            {
                left = new[] { polygon };
                right = new GeoPolygon2[0];
                return false;
            }

            GeoCoordinateSystem3 frame = GeoCoordinateSystem3.Global;
            GeoPolygon3 lifted = LiftedAsItIs(frame, polygon);

            if (lifted == null)
            {
                left = new[] { polygon };
                right = new GeoPolygon2[0];
                return false;
            }

            bool split = Splition3.TrySplitBy(lifted, plane, out GeoPolygon3[] above, out GeoPolygon3[] below, tolerance);

            left = FlattenAsTheyAre(frame, above);
            right = FlattenAsTheyAre(frame, below);

            return split;
        }

        /// <summary>
        /// Cuts a face by the straight line through a segment.
        /// </summary>
        public static bool TrySplitBy(GeoFace2 face, GeoLine2 cutter, out GeoFace2[] left, out GeoFace2[] right)
            => TrySplitBy(face, cutter, out left, out right, Tolerance.Global);

        /// <summary>
        /// Cuts a face by the straight line through a segment, within a tolerance.
        /// </summary>
        /// <param name="face">The face to cut, holes and all.</param>
        /// <param name="cutter">A segment, read as the whole straight line through it; its length is ignored.</param>
        /// <param name="left">The pieces on the left of the cutter's direction of travel.</param>
        /// <param name="right">The pieces on its right.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the line divided the face, leaving something on both sides; otherwise, false, and the face
        /// comes back whole on the side it lies on.
        /// </returns>
        /// <remarks>
        /// A hole the line misses stays a hole in whichever piece keeps it; a hole the line crosses opens into
        /// the outline of both pieces, which is why a face can come back with fewer holes than it went in with.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TrySplitBy(GeoFace2 face, GeoLine2 cutter, out GeoFace2[] left, out GeoFace2[] right, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!TryCuttingPlane(cutter, tolerance, out GeoPlane3 plane))
            {
                left = new[] { face };
                right = new GeoFace2[0];
                return false;
            }

            GeoCoordinateSystem3 frame = GeoCoordinateSystem3.Global;

            bool split = Splition3.TrySplitBy(
                PlanarMap.ToFace3(frame, face), plane, out GeoFace3[] above, out GeoFace3[] below, tolerance);

            left = FlattenFaces(frame, above);
            right = FlattenFaces(frame, below);

            return split;
        }

        #endregion

        #region Cutting an area by a cut line

        /// <summary>
        /// Cuts a loop in two along a chain drawn across it.
        /// </summary>
        public static bool TrySplitBy(GeoPolygon2 subject, GeoPolyline2 cutLine, out GeoPolygon2[] pieces)
            => TrySplitBy(subject, cutLine, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a loop in two along a chain drawn across it, within a tolerance.
        /// </summary>
        /// <param name="subject">The loop to cut.</param>
        /// <param name="cutLine">
        /// The chain to cut along. Both of its ends have to sit on the loop's boundary and everything between
        /// them has to stay inside, or nothing is cut.
        /// </param>
        /// <param name="pieces">The two pieces, or the loop alone.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain divided the loop in two; otherwise, false.</returns>
        /// <remarks>
        /// This is the bounded cutter: the chain gives the exact line of the cut, so a piece keeps the chain's
        /// own vertices rather than a straight line between its ends. A chain that leaves the loop and comes
        /// back would divide it into more than two, so it is refused rather than answered in part.
        /// <para>
        /// An end that meets the boundary within the tolerance of a corner is cut from the corner, so both pieces keep it
        /// and nothing of the loop is lost: at a hundredth, the square 10 by 10 cut from (0.004, 0) to (10, 5) gives the
        /// triangle of 25 square units from (0, 0) and the rest, of 75, where the rest lost a sliver of 0.02 square units
        /// at the corner.
        /// </para>
        /// <para>
        /// The loop, the chain and the pieces are taken corner for corner at the tolerance asked, never at the global one:
        /// at a millionth, the square 10 by 10 with its corner (0, 0) cut off by a side 0.00057 long is cut from (0, 5) to
        /// (10, 5) into the top of 50 square units and the bottom of 50 less the 0.00000008 the cut-off corner takes, where
        /// the global tolerance of a thousandth dropped the short side and refused the cut.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the chain is null.</exception>
        public static bool TrySplitBy(GeoPolygon2 subject, GeoPolyline2 cutLine, out GeoPolygon2[] pieces, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutLine == null)
            {
                throw new ArgumentNullException(nameof(cutLine));
            }

            GeoCoordinateSystem3 frame = GeoCoordinateSystem3.Global;
            GeoPolygon3 lifted = LiftedAsItIs(frame, subject);

            if (lifted == null)
            {
                pieces = new[] { subject };
                return false;
            }

            bool split = Splition3.TrySplitBy(lifted, LiftedAsItIs(frame, cutLine), out GeoPolygon3[] cut, tolerance);

            pieces = split ? FlattenAsTheyAre(frame, cut) : new[] { subject };

            return split;
        }

        #endregion

        #region Laying the plane flat and reading it back

        /// <summary>
        /// The plane standing on the cutting line, square to z = 0, with its normal to the line's left.
        /// </summary>
        /// <remarks>
        /// <c>ZAxis × direction</c> is the left-hand normal in the plane, so the side <see cref="Splition3"/>
        /// calls above is the side a walker along the cutter would call their left. A cutter with no length
        /// picks out no line and cuts nothing.
        /// </remarks>
        private static bool TryCuttingPlane(GeoLine2 cutter, Tolerance tolerance, out GeoPlane3 plane)
        {
            plane = default(GeoPlane3);

            GeoVector2 direction = cutter.Direction;

            if (direction.IsZeroLength(tolerance))
            {
                return false;
            }

            GeoCoordinateSystem3 frame = GeoCoordinateSystem3.Global;
            GeoVector3 along = PlanarMap.ToVector3(frame, direction);

            plane = new GeoPlane3(PlanarMap.ToPoint3(frame, cutter.StartPoint), GeoVector3.ZAxis.CrossProduct(along));

            return true;
        }

        /// <summary>
        /// A loop put into space at z = 0 corner for corner, with the normal and area its corners give; null when it
        /// encloses nothing.
        /// </summary>
        /// <remarks>
        /// <see cref="PlanarMap.ToPolygon3(GeoCoordinateSystem3, GeoPolygon2)"/> builds through the constructor, which
        /// reads <see cref="Tolerance.Global"/>: a side shorter than that was dropped before the cut was asked, and with it
        /// the place a chain started at a millionth, and a loop smaller than that threw.
        /// </remarks>
        private static GeoPolygon3 LiftedAsItIs(GeoCoordinateSystem3 frame, GeoPolygon2 loop)
        {
            var corners = new GeoPoint3[loop.VertexCount];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = PlanarMap.ToPoint3(frame, loop[i]);
            }

            GeoVector3 areaVector = Newell.GetAreaVector(corners);
            double area = areaVector.Length;

            if (!(area > 0.0))
            {
                return null;
            }

            return GeoPolygon3.FromValidated(corners, new GeoVector3(areaVector.X / area, areaVector.Y / area, areaVector.Z / area), area);
        }

        /// <summary>
        /// A chain put into space at z = 0 vertex for vertex.
        /// </summary>
        private static GeoPolyline3 LiftedAsItIs(GeoCoordinateSystem3 frame, GeoPolyline2 chain)
        {
            var vertices = new GeoPoint3[chain.VertexCount];

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = PlanarMap.ToPoint3(frame, chain[i]);
            }

            return new GeoPolyline3(vertices, true);
        }

        /// <summary>
        /// Pieces cut in space laid back in the plane corner for corner: they were built at the tolerance asked, and
        /// the global one would drop a side shorter than it, and with it the place the cut ended.
        /// </summary>
        private static GeoPolygon2[] FlattenAsTheyAre(GeoCoordinateSystem3 frame, GeoPolygon3[] polygons)
        {
            var result = new GeoPolygon2[polygons.Length];

            for (int i = 0; i < polygons.Length; i++)
            {
                var corners = new GeoPoint2[polygons[i].VertexCount];

                for (int j = 0; j < corners.Length; j++)
                {
                    corners[j] = PlanarMap.ProjectToPoint2(frame, polygons[i][j]);
                }

                result[i] = new GeoPolygon2(corners, corners.Length);
            }

            return result;
        }

        private static GeoFace2[] FlattenFaces(GeoCoordinateSystem3 frame, GeoFace3[] faces)
        {
            var result = new GeoFace2[faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                result[i] = PlanarMap.ProjectToFace2(frame, faces[i]);
            }

            return result;
        }

        #endregion
    }
}
