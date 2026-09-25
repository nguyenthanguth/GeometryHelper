using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Carries geometry that curves between the plane and space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rest of <see cref="PlanarMap"/> carries points, segments, polylines, polygons and faces. Arcs were
    /// missing, and that left the round trip half open: a curved edge of a plate could be brought into the
    /// plane to be worked on, but the answer could only be put back by flattening it and losing the curves.
    /// </para>
    /// <para>
    /// A bulge in the plane is read counter-clockwise about the Z axis, so lifting one keeps the bulge as it
    /// is and hands the frame's Z axis to the edge as the plane it bulges in. Bringing one back is the same
    /// step in reverse, with one thing to watch: an arc whose normal runs <b>against</b> the frame is the same
    /// arc seen from behind, so its bulge changes sign on the way down. An arc whose normal is not along the
    /// frame at all does not lie in that plane, and the <c>Try</c> forms say so rather than flattening it.
    /// </para>
    /// </remarks>
    public static partial class PlanarMap
    {
        #region Arcs

        /// <summary>
        /// Lifts an arc out of a frame into the plane of that frame in space.
        /// </summary>
        /// <param name="frame">The frame the arc is laid out in.</param>
        /// <param name="arc">The arc in the plane.</param>
        /// <returns>The same arc, standing in the plane of the frame.</returns>
        public static GeoArc3 ToArc3(GeoCoordinateSystem3 frame, GeoArc2 arc)
        {
            return GeoArc3.FromThreePoints(
                ToPoint3(frame, arc.StartPoint),
                ToPoint3(frame, arc.MidPoint),
                ToPoint3(frame, arc.EndPoint));
        }

        /// <summary>
        /// Lays an arc out in a frame, by projecting it onto the plane of that frame.
        /// </summary>
        /// <remarks>
        /// An arc that does not lie in the plane of the frame comes back as the arc through the projections
        /// of its ends and its middle, which is a different arc. <see cref="TryToArc2(GeoCoordinateSystem3, GeoArc3, out GeoArc2)"/> refuses that case
        /// instead of answering it.
        /// </remarks>
        public static GeoArc2 ProjectToArc2(GeoCoordinateSystem3 frame, GeoArc3 arc)
        {
            return arc.ProjectToArc2(frame);
        }

        /// <summary>
        /// Lays an arc out in a frame when it lies in the plane of that frame.
        /// </summary>
        public static bool TryToArc2(GeoCoordinateSystem3 frame, GeoArc3 arc, out GeoArc2 result)
            => TryToArc2(frame, arc, out result, Tolerance.Global);

        /// <summary>
        /// Lays an arc out in a frame when it lies in the plane of that frame, within a tolerance.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="arc">The arc in space.</param>
        /// <param name="result">The arc in the plane, when it lies in one.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc lies in the plane of the frame; otherwise, false.</returns>
        /// <remarks>
        /// Both tests have to pass: the plane of the arc has to be the plane of the frame, and its centre has
        /// to lie on that plane. A circle of the right tilt sitting above the frame would pass the first test
        /// alone.
        /// </remarks>
        public static bool TryToArc2(GeoCoordinateSystem3 frame, GeoArc3 arc, out GeoArc2 result, Tolerance tolerance)
        {
            if (!arc.Normal.IsParallelTo(frame.ZAxis, tolerance) ||
                Math.Abs(frame.ToLocal(arc.Center).Z) > tolerance.EqualPlanar)
            {
                result = default(GeoArc2);
                return false;
            }

            result = arc.ProjectToArc2(frame);
            return true;
        }

        #endregion

        #region Edges

        /// <summary>
        /// Lifts an edge out of a frame into the plane of that frame in space.
        /// </summary>
        /// <param name="frame">The frame the edge is laid out in.</param>
        /// <param name="edge">The edge in the plane; straight or curved.</param>
        /// <returns>The same edge, standing in the plane of the frame, bulging about its Z axis.</returns>
        public static GeoEdge3 ToEdge3(GeoCoordinateSystem3 frame, GeoEdge2 edge)
        {
            GeoPoint3 start = ToPoint3(frame, edge.StartPoint);
            GeoPoint3 end = ToPoint3(frame, edge.EndPoint);

            return edge.IsArc
                ? new GeoEdge3(start, end, edge.Bulge, frame.ZAxis)
                : new GeoEdge3(start, end);
        }

        /// <summary>
        /// Lays an edge out in a frame when it lies in the plane of that frame.
        /// </summary>
        public static bool TryToEdge2(GeoCoordinateSystem3 frame, GeoEdge3 edge, out GeoEdge2 result)
            => TryToEdge2(frame, edge, out result, Tolerance.Global);

        /// <summary>
        /// Lays an edge out in a frame when it lies in the plane of that frame, within a tolerance.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="edge">The edge in space.</param>
        /// <param name="result">The edge in the plane, when it lies in one.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if both ends lie on the plane and any bulge is read about its Z axis; otherwise, false.</returns>
        /// <remarks>
        /// An arc bulging about the Z axis the other way round is the same arc seen from behind, so its bulge
        /// changes sign coming down. That keeps the round trip exact whichever way the frame was built.
        /// </remarks>
        public static bool TryToEdge2(GeoCoordinateSystem3 frame, GeoEdge3 edge, out GeoEdge2 result, Tolerance tolerance)
        {
            result = default(GeoEdge2);

            if (!TryToPoint2(frame, edge.StartPoint, out GeoPoint2 start, tolerance) ||
                !TryToPoint2(frame, edge.EndPoint, out GeoPoint2 end, tolerance))
            {
                return false;
            }

            if (!edge.IsArc)
            {
                result = new GeoEdge2(start, end);
                return true;
            }

            if (!edge.Normal.IsParallelTo(frame.ZAxis, tolerance))
            {
                return false;
            }

            bool facingAway = edge.Normal.DotProduct(frame.ZAxis) < 0.0;

            result = new GeoEdge2(start, end, facingAway ? -edge.Bulge : edge.Bulge);
            return true;
        }

        #endregion

        #region Chains that curve

        /// <summary>
        /// Lifts a chain that may curve out of a frame into the plane of that frame in space.
        /// </summary>
        /// <param name="frame">The frame the chain is laid out in.</param>
        /// <param name="chain">The chain in the plane.</param>
        /// <returns>The same chain, standing in the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc3 ToPolylineArc3(GeoCoordinateSystem3 frame, GeoPolylineArc2 chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var edges = new List<GeoEdge3>(chain.EdgeCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                edges.Add(ToEdge3(frame, chain.GetEdgeAt(i)));
            }

            return new GeoPolylineArc3(edges);
        }

        /// <summary>
        /// Lays a chain that may curve out in a frame when the whole of it lies in the plane of that frame.
        /// </summary>
        public static bool TryToPolylineArc2(GeoCoordinateSystem3 frame, GeoPolylineArc3 chain, out GeoPolylineArc2 result)
            => TryToPolylineArc2(frame, chain, out result, Tolerance.Global);

        /// <summary>
        /// Lays a chain that may curve out in a frame when the whole of it lies in the plane of that frame, within a tolerance.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="chain">The chain in space.</param>
        /// <param name="result">The chain in the plane, when the whole of it lies in one.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if every vertex lies on the plane and every arc bulges in it; otherwise, false.</returns>
        /// <remarks>
        /// A bar bent about two different axes has no plane to be laid out in, and one arc bulging out of the
        /// plane is enough to refuse the whole chain. That is the same reading
        /// <see cref="GeoPolylineArc3.TryGetPlane(out GeoPlane3)"/> takes.
        /// </remarks>
        public static bool TryToPolylineArc2(
            GeoCoordinateSystem3 frame,
            GeoPolylineArc3 chain,
            out GeoPolylineArc2 result,
            Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            result = null;

            var edges = new List<GeoEdge2>(chain.EdgeCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                if (!TryToEdge2(frame, chain.GetEdgeAt(i), out GeoEdge2 edge, tolerance))
                {
                    return false;
                }

                edges.Add(edge);
            }

            result = new GeoPolylineArc2(edges);
            return true;
        }

        #endregion
    }
}
