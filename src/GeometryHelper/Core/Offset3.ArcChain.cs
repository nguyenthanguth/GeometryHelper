using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Offsetting a curved chain in space within its own plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoPolyline3"/> could be offset within a plane and a <see cref="GeoPolylineArc3"/> could not,
    /// though the plane library offsets a curved chain and a closed curved loop in space already offsets itself.
    /// A bar set out at one cover and wanted at another had to be straightened first, which throws the bends away.
    /// </para>
    /// <para>
    /// A chain holding an arc has a plane of its own, taken from the arc rather than from the vertices — three
    /// vertices are always flat, so the vertices cannot be trusted to give it. The chain is laid out in that
    /// plane, the plane library answers, and the answer is lifted back: exact, with the arcs still arcs and
    /// their radii moved by the distance. A chain holding no arc is handed to the straight version, so the two
    /// cannot disagree, and a chain running straight along one line still has a plane picked for it by the
    /// normal, as the straight version does.
    /// </para>
    /// </remarks>
    public static partial class Offset3
    {
        #region Offsetting a curved chain in its plane

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane.
        /// </summary>
        public static GeoPolylineArc3[] OffsetInPlane(GeoPolylineArc3 chain, double distance, GeoVector3 planeNormal)
            => OffsetInPlane(chain, distance, planeNormal, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane, within a tolerance.
        /// </summary>
        public static GeoPolylineArc3[] OffsetInPlane(GeoPolylineArc3 chain, double distance, GeoVector3 planeNormal, Tolerance tolerance)
            => OffsetInPlane(chain, distance, planeNormal, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane, with the
        /// given shape of corner.
        /// </summary>
        public static GeoPolylineArc3[] OffsetInPlane(GeoPolylineArc3 chain, double distance, GeoVector3 planeNormal, OffsetJoin join)
            => OffsetInPlane(chain, distance, planeNormal, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane, with the
        /// given shape of corner, within a tolerance.
        /// </summary>
        public static GeoPolylineArc3[] OffsetInPlane(GeoPolylineArc3 chain, double distance, GeoVector3 planeNormal, OffsetJoin join, Tolerance tolerance)
            => OffsetInPlane(chain, distance, planeNormal, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane, as the
        /// options say.
        /// </summary>
        public static GeoPolylineArc3[] OffsetInPlane(GeoPolylineArc3 chain, double distance, GeoVector3 planeNormal, OffsetOptions options)
            => OffsetInPlane(chain, distance, planeNormal, options, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to a flat curved chain at a distance to its left within its plane, as the
        /// options say, within a tolerance. A negative distance goes to the right.
        /// </summary>
        /// <param name="chain">The chain, which must lie in one plane.</param>
        /// <param name="distance">How far to move it, to its left seen from the tip of the normal when positive.</param>
        /// <param name="planeNormal">
        /// Which side of the chain's plane it is seen from, and so which way is left. For a chain running straight
        /// along one line, which lies in every plane through it, it also picks the plane.
        /// </param>
        /// <param name="options">The shape of the corners on the outside of its turns.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The pieces of the parallel curve, in order along the chain and running the same way; usually one. An
        /// arc comes back as an arc, with its radius moved by the distance, and a turn tighter than the distance
        /// loses the loop it would have made, as in <c>Offset2</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the chain does not lie in one plane, when the normal has no length, or when it lies in the
        /// chain's plane and so gives no side.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public static GeoPolylineArc3[] OffsetInPlane(
            GeoPolylineArc3 chain,
            double distance,
            GeoVector3 planeNormal,
            OffsetOptions options,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            RequireFinite(distance, nameof(distance));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { chain.Clone() };
            }

            // A chain with no arc in it is a straight chain wearing the curved type. Hand it to the straight
            // version rather than keeping a second answer for it: that one already picks a plane for a chain
            // running along one line, and the two can then never disagree.
            if (!HoldsAnArc(chain))
            {
                GeoPolyline3[] straight = OffsetInPlane(new GeoPolyline3(chain.Vertices), distance, planeNormal, options, tolerance);
                var asChains = new GeoPolylineArc3[straight.Length];

                for (int i = 0; i < straight.Length; i++)
                {
                    asChains[i] = new GeoPolylineArc3(straight[i]);
                }

                return asChains;
            }

            GeoCoordinateSystem3 frame = FrameFacing(chain, planeNormal, tolerance);

            if (!PlanarMap.TryToPolylineArc2(frame, chain, out GeoPolylineArc2 flat, tolerance))
            {
                throw new ArgumentException(
                    "The chain does not lie in one plane, so it has no side to offset to.", nameof(chain));
            }

            var result = new List<GeoPolylineArc3>();

            foreach (GeoPolylineArc2 run in Offset2.Offset(flat, distance, options, tolerance))
            {
                result.Add(PlanarMap.ToPolylineArc3(frame, run));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Determines whether any edge of a chain curves.
        /// </summary>
        private static bool HoldsAnArc(GeoPolylineArc3 chain)
        {
            foreach (GeoEdge3 edge in ArcChain3.EdgesOf(chain))
            {
                if (edge.IsArc)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The chain's own plane as a frame, turned to be seen from the tip of the given normal.
        /// </summary>
        /// <remarks>
        /// The plane comes from <see cref="GeoPolylineArc3.TryGetPlane(out GeoPlane3, Tolerance)"/>, which reads
        /// the arcs and not the vertices, because any three vertices are flat and would agree to any plane. The
        /// normal only says which side the chain is seen from, so it is used to turn the plane over and never to
        /// replace it: that keeps left meaning the same thing here as it does for a straight chain.
        /// </remarks>
        private static GeoCoordinateSystem3 FrameFacing(GeoPolylineArc3 chain, GeoVector3 planeNormal, Tolerance tolerance)
        {
            double length = planeNormal.Length;

            if (length <= tolerance.EqualVector)
            {
                throw new ArgumentException("The plane normal has no length.", nameof(planeNormal));
            }

            if (!chain.TryGetPlane(out GeoPlane3 plane, tolerance))
            {
                throw new ArgumentException(
                    "The chain does not lie in one plane, so it has no side to offset to.", nameof(chain));
            }

            double facing = plane.Normal.DotProduct(planeNormal.Divide(length));

            if (Math.Abs(facing) <= tolerance.EqualAngleSin)
            {
                throw new ArgumentException(
                    "The plane normal lies in the plane of the chain, so it gives no side to offset to.", nameof(planeNormal));
            }

            GeoVector3 normal = facing > 0.0 ? plane.Normal : plane.Normal.Negate();

            return PlanarMap.FrameOf(new GeoPlane3(chain.StartPoint, normal));
        }

        #endregion
    }
}
