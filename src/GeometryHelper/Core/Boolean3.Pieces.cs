using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The separate pieces a body is made of, and one body per region two bodies share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TryIntersect(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance)"/> hands back the whole shared
    /// region as one body, which is right for its volume and wrong for a clash report: a beam passing through
    /// two separate plates clashes twice, and one body with two shells does not say where either clash is.
    /// </para>
    /// <para>
    /// A body is split where its material does not touch: two blocks that share only an edge or a corner share
    /// no volume, so they are two pieces. A cavity stays with the piece around it, and each opening goes with
    /// every piece it reaches. The work is <see cref="Shells3"/>, which the booleans already rely on to judge
    /// one piece of material at a time.
    /// </para>
    /// </remarks>
    public static partial class Boolean3
    {
        #region Pieces

        /// <summary>
        /// Splits a body into the pieces of material that do not touch.
        /// </summary>
        public static GeoSolid3[] SplitShells(GeoSolid3 solid) => SplitShells(solid, Tolerance.Global);

        /// <summary>
        /// Splits a body into the pieces of material that do not touch, within a tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance deciding which edges are shared.</param>
        /// <returns>
        /// One body per piece, in no particular order; the body itself, alone, when it is all one piece.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public static GeoSolid3[] SplitShells(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Shells3.Split(solid, tolerance).ToArray();
        }

        /// <summary>
        /// Gets every separate region two bodies share, one body each.
        /// </summary>
        public static GeoSolid3[] Intersect(GeoSolid3 first, GeoSolid3 second) => Intersect(first, second, Tolerance.Global);

        /// <summary>
        /// Gets every separate region two bodies share, one body each, within a tolerance.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One body per region they share, in no particular order; empty when they share no volume — which
        /// includes two bodies that only touch, since touching shares a face, an edge or a point and no volume.
        /// </returns>
        /// <remarks>
        /// The same shape of answer <see cref="GeoPolygon3.Intersect(GeoPolygon3)"/> gives in the plane. It is
        /// not an overload of <c>TryIntersect</c>: an <c>out GeoSolid3[]</c> beside the <c>out GeoSolid3</c> one
        /// would make every existing call written with <c>out _</c> ambiguous.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either body is null.</exception>
        public static GeoSolid3[] Intersect(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            return TryIntersect(first, second, out GeoSolid3 shared, tolerance)
                ? Shells3.Split(shared, tolerance).ToArray()
                : new GeoSolid3[0];
        }

        #endregion
    }
}
