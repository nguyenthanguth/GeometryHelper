using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The boolean operations over the loops that may curve.
    /// <para>
    /// These flatten first, and say so by what they give back: Clipper resolves every region operation in
    /// this library and knows only straight edges, so there is no form of a boolean that keeps arcs. The
    /// straight result is the honest one — an answer of the curved type would be a promise that the arcs
    /// had survived, and they have not.
    /// </para>
    /// <para>
    /// <see cref="Offset2"/> is the exception, and the reason is worth knowing: offsetting is done piece by
    /// piece, so an arc can be offset exactly and stay an arc. A boolean cannot be done piece by piece.
    /// </para>
    /// </summary>
    public static partial class Boolean2
    {
        #region Loops that may curve

        /// <summary>
        /// Gets both loops together, with the arcs cut into straight pieces first.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <remarks>
        /// Each arc is cut finely enough to stray no further than the automatic share of its radius, about
        /// a fifth of a percent. Use the overload that names a chord tolerance when that is not fine
        /// enough.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2) => Union(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets both loops together, with the arcs cut into straight pieces first, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Union(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets both loops together, cutting the arcs no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Union(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets both loops together, cutting the arcs no further than a chord tolerance from the curve, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Union(loop1.Flatten(chordTolerance), loop2.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets both loops together where one of them is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygon2 loop2) => Union(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets both loops together where one of them is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Union(loop1.Flatten(chordTolerance), loop2, tolerance);
        }

        /// <summary>
        /// Gets both loops together where the subject is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygon2 loop1, GeoPolygonArc2 loop2) => Union(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets both loops together where the subject is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Union(loop1, loop2.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets what both loops cover, with the arcs cut into straight pieces first.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <remarks>
        /// Each arc is cut finely enough to stray no further than the automatic share of its radius, about
        /// a fifth of a percent. Use the overload that names a chord tolerance when that is not fine
        /// enough.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2) => Intersect(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what both loops cover, with the arcs cut into straight pieces first, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Intersect(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets what both loops cover, cutting the arcs no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Intersect(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets what both loops cover, cutting the arcs no further than a chord tolerance from the curve, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Intersect(loop1.Flatten(chordTolerance), loop2.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets what both loops cover where one of them is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygon2 loop2) => Intersect(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what both loops cover where one of them is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Intersect(loop1.Flatten(chordTolerance), loop2, tolerance);
        }

        /// <summary>
        /// Gets what both loops cover where the subject is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygon2 loop1, GeoPolygonArc2 loop2) => Intersect(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what both loops cover where the subject is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Intersect(loop1, loop2.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets the subject with the tool taken out of it, with the arcs cut into straight pieces first.
        /// </summary>
        /// <param name="subject">The first loop, which may curve.</param>
        /// <param name="tool">The second loop, which may curve.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <remarks>
        /// Each arc is cut finely enough to stray no further than the automatic share of its radius, about
        /// a fifth of a percent. Use the overload that names a chord tolerance when that is not fine
        /// enough.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygonArc2 tool) => Subtract(subject, tool, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets the subject with the tool taken out of it, with the arcs cut into straight pieces first, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygonArc2 tool, Tolerance tolerance) => Subtract(subject, tool, 0.0, tolerance);

        /// <summary>
        /// Gets the subject with the tool taken out of it, cutting the arcs no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="subject">The first loop, which may curve.</param>
        /// <param name="tool">The second loop, which may curve.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygonArc2 tool, double chordTolerance) => Subtract(subject, tool, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the subject with the tool taken out of it, cutting the arcs no further than a chord tolerance from the curve, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygonArc2 tool, double chordTolerance, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            return Subtract(subject.Flatten(chordTolerance), tool.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets the subject with the tool taken out of it where one of them is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygon2 tool) => Subtract(subject, tool, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets the subject with the tool taken out of it where one of them is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygon2 tool, double chordTolerance, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            return Subtract(subject.Flatten(chordTolerance), tool, tolerance);
        }

        /// <summary>
        /// Gets the subject with the tool taken out of it where the subject is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygonArc2 tool) => Subtract(subject, tool, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets the subject with the tool taken out of it where the subject is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygonArc2 tool, double chordTolerance, Tolerance tolerance)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            return Subtract(subject, tool.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets what one loop covers and the other does not, with the arcs cut into straight pieces first.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <remarks>
        /// Each arc is cut finely enough to stray no further than the automatic share of its radius, about
        /// a fifth of a percent. Use the overload that names a chord tolerance when that is not fine
        /// enough.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2) => Xor(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what one loop covers and the other does not, with the arcs cut into straight pieces first, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Xor(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets what one loop covers and the other does not, cutting the arcs no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="loop1">The first loop, which may curve.</param>
        /// <param name="loop2">The second loop, which may curve.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The faces of the result, straight throughout.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Xor(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets what one loop covers and the other does not, cutting the arcs no further than a chord tolerance from the curve, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Xor(loop1.Flatten(chordTolerance), loop2.Flatten(chordTolerance), tolerance);
        }

        /// <summary>
        /// Gets what one loop covers and the other does not where one of them is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygon2 loop2) => Xor(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what one loop covers and the other does not where one of them is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Xor(loop1.Flatten(chordTolerance), loop2, tolerance);
        }

        /// <summary>
        /// Gets what one loop covers and the other does not where the subject is already straight, cutting the arcs of the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygon2 loop1, GeoPolygonArc2 loop2) => Xor(loop1, loop2, 0.0, Tolerance.Global);

        /// <summary>
        /// Gets what one loop covers and the other does not where the subject is already straight, cutting the arcs of the other, within a
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance, Tolerance tolerance)
        {
            if (loop1 == null) throw new ArgumentNullException(nameof(loop1));
            if (loop2 == null) throw new ArgumentNullException(nameof(loop2));

            return Xor(loop1, loop2.Flatten(chordTolerance), tolerance);
        }
        /// <summary>
        /// Gets the result where one shape is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygon2 loop2, Tolerance tolerance) => Union(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where one shape is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance) => Union(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where the subject is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygon2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Union(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where the subject is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Union(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Union(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where one shape is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygon2 loop2, Tolerance tolerance) => Intersect(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where one shape is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance) => Intersect(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where the subject is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygon2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Intersect(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where the subject is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Intersect(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where one shape is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygon2 tool, Tolerance tolerance) => Subtract(subject, tool, 0.0, tolerance);

        /// <summary>
        /// Gets the result where one shape is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygonArc2 subject, GeoPolygon2 tool, double chordTolerance) => Subtract(subject, tool, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where the subject is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygonArc2 tool, Tolerance tolerance) => Subtract(subject, tool, 0.0, tolerance);

        /// <summary>
        /// Gets the result where the subject is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygonArc2 tool, double chordTolerance) => Subtract(subject, tool, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where one shape is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygon2 loop2, Tolerance tolerance) => Xor(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where one shape is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygonArc2 loop1, GeoPolygon2 loop2, double chordTolerance) => Xor(loop1, loop2, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Gets the result where the subject is already straight, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygon2 loop1, GeoPolygonArc2 loop2, Tolerance tolerance) => Xor(loop1, loop2, 0.0, tolerance);

        /// <summary>
        /// Gets the result where the subject is already straight, cutting the arcs no further than a chord
        /// tolerance from the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either loop is null.</exception>
        public static GeoFace2[] Xor(GeoPolygon2 loop1, GeoPolygonArc2 loop2, double chordTolerance) => Xor(loop1, loop2, chordTolerance, Tolerance.Global);

        #endregion
    }
}
