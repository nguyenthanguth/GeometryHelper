using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// How the corners of an offset are shaped: the join, the miter limit as a multiple of the distance, and
    /// the chord error allowed on a round corner in drawing units.
    /// </summary>
    internal readonly struct CornerStyle
    {
        public CornerStyle(OffsetJoin join, double miterLimit, double arcTolerance)
        {
            Join = join;
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
        }

        public OffsetJoin Join { get; }

        public double MiterLimit { get; }

        public double ArcTolerance { get; }
    }

    /// <summary>
    /// Builds the raw offset of a loop or a chain: every edge moved sideways, with the corners joined.
    /// <para>
    /// The raw offset is not the answer yet. Where the edges pull apart, at the outside of a turn, the gap is
    /// closed by the join. Where they overlap, at the inside of a turn, the outline runs back through the
    /// original corner and out again. That detour is what makes the result exact: the raw loop is then the
    /// sum of the original boundary, a strip swept by every edge and a wedge at every outside corner, so it
    /// winds a positive number of times round exactly the points of the offset region and zero or fewer
    /// round every other. Resolving it under the positive fill rule turns it into that region, however the
    /// strips overlap, split or swallow each other. The solid library resolves it with its own region solver,
    /// the plane library with Clipper2; both read the same raw loops, so both draw the same corners.
    /// </para>
    /// </summary>
    internal static class OffsetOutline
    {
        /// <summary>
        /// Below this sine, two edges running opposite ways are taken to fold straight back, and the corner is
        /// rounded or squared off around the fold rather than on the side the rounding error says.
        /// </summary>
        private const double FoldBackSine = 1e-12;

        /// <summary>
        /// The finest a round corner is ever divided: this many segments to a full turn.
        /// </summary>
        private const int MaxSegmentsPerTurn = 1024;

        /// <summary>
        /// Builds the raw offset of a closed loop whose region lies on its left.
        /// </summary>
        /// <param name="loop">The loop: no repeated points.</param>
        /// <param name="distance">The offset distance, positive outward, to the right of every edge.</param>
        /// <param name="style">The corner shape.</param>
        /// <returns>The raw offset loop, to be resolved under the positive fill rule.</returns>
        public static List<GeoPoint2> BuildClosedLoop(IReadOnlyList<GeoPoint2> loop, double distance, CornerStyle style)
        {
            int count = loop.Count;
            GeoVector2[] tangents = new GeoVector2[count];

            for (int i = 0; i < count; i++)
            {
                tangents[i] = (loop[(i + 1) % count] - loop[i]).Unit();
            }

            List<GeoPoint2> raw = new List<GeoPoint2>(count * 3);

            for (int i = 0; i < count; i++)
            {
                AppendCorner(raw, null, loop[i], tangents[(i - 1 + count) % count], tangents[i], distance, style);
            }

            return raw;
        }

        /// <summary>
        /// Builds the raw offset of an open chain: the start moved sideways, every inner corner joined, and the
        /// end moved sideways.
        /// </summary>
        /// <param name="chain">The chain: at least two points, no repeated points.</param>
        /// <param name="distance">The offset distance, positive to the right of the chain.</param>
        /// <param name="style">The corner shape.</param>
        /// <param name="throughCorner">
        /// For each point but the last, whether the edge leaving it is one of the two that run back through an
        /// original corner on the inside of a turn, rather than an offset edge or part of a join.
        /// </param>
        /// <returns>The raw offset chain, running the same way as the original.</returns>
        public static List<GeoPoint2> BuildOpenChain(IReadOnlyList<GeoPoint2> chain, double distance, CornerStyle style, out List<bool> throughCorner)
        {
            int count = chain.Count;
            GeoVector2[] tangents = new GeoVector2[count - 1];

            for (int i = 0; i + 1 < count; i++)
            {
                tangents[i] = (chain[i + 1] - chain[i]).Unit();
            }

            List<GeoPoint2> raw = new List<GeoPoint2>(count * 3);
            throughCorner = new List<bool>(count * 3);
            raw.Add(chain[0] + tangents[0].Right() * distance);
            throughCorner.Add(false);

            for (int i = 1; i + 1 < count; i++)
            {
                AppendCorner(raw, throughCorner, chain[i], tangents[i - 1], tangents[i], distance, style);
            }

            raw.Add(chain[count - 1] + tangents[count - 2].Right() * distance);
            throughCorner.Add(false);
            return raw;
        }

        /// <summary>
        /// Appends the raw offset of one corner: from the end of the incoming offset edge to the start of the
        /// outgoing one. When <paramref name="throughCorner"/> is given, it gets one entry per point appended,
        /// saying whether the edge leaving that point runs through the original corner.
        /// </summary>
        private static void AppendCorner(List<GeoPoint2> raw, List<bool> throughCorner, GeoPoint2 vertex, GeoVector2 incoming, GeoVector2 outgoing, double distance, CornerStyle style)
        {
            int before = raw.Count;
            AppendCorner(raw, vertex, incoming, outgoing, distance, style, out bool detour);

            if (throughCorner != null)
            {
                for (int i = before; i < raw.Count; i++)
                {
                    // In a detour the points are the end of the incoming edge, the corner, and the start of the
                    // outgoing edge: the edges leaving the first two run through the corner.
                    throughCorner.Add(detour && i < raw.Count - 1);
                }
            }
        }

        private static void AppendCorner(List<GeoPoint2> raw, GeoPoint2 vertex, GeoVector2 incoming, GeoVector2 outgoing, double distance, CornerStyle style, out bool detour)
        {
            detour = false;
            GeoVector2 normalIn = incoming.Right();
            GeoVector2 normalOut = outgoing.Right();
            GeoPoint2 endIn = vertex + normalIn * distance;
            GeoPoint2 startOut = vertex + normalOut * distance;
            double cross = incoming.Cross(outgoing);
            double dot = incoming.Dot(outgoing);
            bool foldBack = dot < 0.0 && Math.Abs(cross) <= FoldBackSine;

            // The offset edges overlap on the inside of a turn: run back through the corner and out again.
            if (!foldBack && cross * distance <= 0.0)
            {
                raw.Add(endIn);
                raw.Add(vertex);
                raw.Add(startOut);
                detour = true;
                return;
            }

            double radius = Math.Abs(distance);
            double sign = distance > 0.0 ? 1.0 : -1.0;
            GeoVector2 fromDirection = normalIn * sign;
            GeoVector2 toDirection = normalOut * sign;

            // The angle the join turns through, from one offset edge to the other: up to a half turn.
            double angle = Math.Atan2(Math.Abs(fromDirection.Cross(toDirection)), Math.Max(-1.0, Math.Min(1.0, fromDirection.Dot(toDirection))));

            if (foldBack)
            {
                angle = Math.PI;
            }

            switch (style.Join)
            {
                case OffsetJoin.Round:
                {
                    // A fold turns the long way round, through the direction the chain was heading.
                    double turn = foldBack ? Math.Sign(fromDirection.Cross(incoming)) : Math.Sign(fromDirection.Cross(toDirection));
                    AppendArc(raw, vertex, endIn, startOut, fromDirection, radius, angle, turn, style.ArcTolerance);
                    return;
                }

                case OffsetJoin.Chamfer:
                    AppendSquare(raw, endIn, startOut, incoming, outgoing, radius, angle, radius);
                    return;

                default:
                {
                    double cosHalf = Math.Cos(angle * 0.5);

                    if (!foldBack && cosHalf * style.MiterLimit >= 1.0)
                    {
                        // The two offset lines meet at radius / cos(half angle) from the corner, on the bisector.
                        GeoVector2 bisector = fromDirection + toDirection;
                        raw.Add(vertex + bisector * (radius / (1.0 + fromDirection.Dot(toDirection))));
                        return;
                    }

                    // A chain folding straight back has its miter point at infinity; with no limit set it is
                    // squared off at the offset distance, as a butt of the chain would be.
                    double reach = double.IsInfinity(style.MiterLimit) ? radius : style.MiterLimit * radius;
                    AppendSquare(raw, endIn, startOut, incoming, outgoing, radius, angle, reach);
                    return;
                }
            }
        }

        /// <summary>
        /// Cuts the corner off square, with a line lying <paramref name="reach"/> from the corner across the
        /// bisector, and appends the two points where it meets the offset edges carried on past their ends.
        /// </summary>
        private static void AppendSquare(List<GeoPoint2> raw, GeoPoint2 endIn, GeoPoint2 startOut, GeoVector2 incoming, GeoVector2 outgoing, double radius, double angle, double reach)
        {
            // How far past the end of each offset edge the cut lies: (reach - r cos(a/2)) / sin(a/2), written
            // so that neither part loses its digits when the corner is nearly straight.
            double along = (reach - radius) / Math.Sin(angle * 0.5) + radius * Math.Tan(angle * 0.25);

            raw.Add(endIn + incoming * along);
            raw.Add(startOut - outgoing * along);
        }

        /// <summary>
        /// Appends an arc round the corner from one offset edge to the other, as chords no farther from the true
        /// arc than the arc tolerance.
        /// </summary>
        private static void AppendArc(List<GeoPoint2> raw, GeoPoint2 center, GeoPoint2 from, GeoPoint2 to, GeoVector2 fromDirection, double radius, double angle, double turn, double arcTolerance)
        {
            double step = arcTolerance >= radius ? Math.PI * 0.5 : 2.0 * Math.Acos(1.0 - arcTolerance / radius);
            step = Math.Max(step, 2.0 * Math.PI / MaxSegmentsPerTurn);
            step = Math.Min(step, Math.PI * 0.5);

            int segments = Math.Max(1, (int)Math.Ceiling(angle / step - 1e-9));

            raw.Add(from);

            for (int k = 1; k < segments; k++)
            {
                raw.Add(center + fromDirection.Rotate(turn * angle * k / segments) * radius);
            }

            raw.Add(to);
        }
    }
}
