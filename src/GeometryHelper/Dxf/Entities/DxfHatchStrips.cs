using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Cuts the area a HATCH fills into the horizontal trapezoids AutoCAD 2021 draws a solid fill of in R12, each with a left and
    /// a right side that run from one level of the region's corners to the next, and says in which order and with which sides
    /// AutoCAD writes them.
    /// </summary>
    /// <remarks>
    /// A level is the height of a corner of any loop. A strip between two levels is cut by the edges that cross it, paired off
    /// from the left; a trapezoid runs on over a level as long as the same two edges bound it, so a square 100 by 100 with a hole
    /// 20 by 20 has trapezoids of different heights on its left and right. A trapezoid whose top or bottom is a point is a
    /// triangle.
    /// </remarks>
    internal static class DxfHatchStrips
    {
        /// <summary>One trapezoid: its four corners, and whether its top or bottom is a point.</summary>
        internal sealed class Trapezoid
        {
            internal Trapezoid(
                GeoPoint2 topLeft,
                GeoPoint2 topRight,
                GeoPoint2 bottomLeft,
                GeoPoint2 bottomRight,
                bool topPoint,
                bool bottomPoint
            )
            {
                TopLeft = topLeft;
                TopRight = topRight;
                BottomLeft = bottomLeft;
                BottomRight = bottomRight;
                TopPoint = topPoint;
                BottomPoint = bottomPoint;
            }

            internal GeoPoint2 TopLeft { get; }

            internal GeoPoint2 TopRight { get; }

            internal GeoPoint2 BottomLeft { get; }

            internal GeoPoint2 BottomRight { get; }

            /// <summary>Whether the top corners are one point.</summary>
            internal bool TopPoint { get; }

            /// <summary>Whether the bottom corners are one point.</summary>
            internal bool BottomPoint { get; }

            /// <summary>Whether the left side is left out of the lines, as it runs to a corner of the area that is pointed inwards.</summary>
            internal bool LeftPointed { get; set; }

            /// <summary>Whether the right side is left out of the lines, as the left is.</summary>
            internal bool RightPointed { get; set; }

            /// <summary>The level AutoCAD writes the trapezoid at: its bottom, or its top where that is a point.</summary>
            internal double Level { get; set; }

            /// <summary>Where it stands among those of one level: triangles on top, then triangles at the bottom, then the rest.</summary>
            internal int Class =>
                BottomPoint ? 1
                : TopPoint ? 0
                : 2;
        }

        private sealed class Edge
        {
            internal Edge(int id, GeoPoint2 top, GeoPoint2 bottom)
            {
                Id = id;
                Top = top;
                Bottom = bottom;
            }

            internal int Id { get; }

            internal GeoPoint2 Top { get; }

            internal GeoPoint2 Bottom { get; }

            /// <summary>Whether the corner at the top is one where the area is pointed inwards, a corner two edges leave downwards.</summary>
            internal bool TopPointed { get; set; }

            /// <summary>Whether the corner at the bottom is one where the area is pointed inwards, a corner two edges leave upwards.</summary>
            internal bool BottomPointed { get; set; }

            internal bool IsVertical(double eps) => Math.Abs(Top.X - Bottom.X) <= eps;

            internal double XAt(double y)
            {
                double dy = Bottom.Y - Top.Y;
                return Top.X + (y - Top.Y) * (Bottom.X - Top.X) / dy;
            }
        }

        private sealed class Open
        {
            internal Open(Edge left, Edge right, double top)
            {
                Left = left;
                Right = right;
                Top = top;
            }

            internal Edge Left { get; }

            internal Edge Right { get; }

            internal double Top { get; }
        }

        /// <summary>
        /// Cuts the area inside the loops, as the even-odd rule counts it, into trapezoids; sort what several areas give with <see cref="Compare"/> for the order AutoCAD writes them in.
        /// </summary>
        /// <param name="loops">The loops, each a closed run of corners (the first not written again); the first is the outer one, and the others the holes in it.</param>
        /// <param name="tolerance">What is the same height or place.</param>
        /// <returns>The trapezoids, in no order.</returns>
        internal static List<Trapezoid> Cut(IReadOnlyList<IReadOnlyList<GeoPoint2>> loops, Tolerance tolerance)
        {
            double eps = tolerance.EqualPoint;
            var edges = new List<Edge>();
            var heights = new List<double>();

            for (int l = 0; l < loops.Count; l++)
            {
                IReadOnlyList<GeoPoint2> loop = loops[l];
                int n = loop.Count;
                double orientation = Math.Sign(SignedArea(loop)) * (l == 0 ? 1.0 : -1.0);
                var made = new List<Edge>();

                for (int i = 0; i < n; i++)
                {
                    GeoPoint2 a = loop[i];
                    heights.Add(a.Y);
                    GeoPoint2 b = loop[(i + 1) % n];

                    if (Math.Abs(a.Y - b.Y) > eps)
                    {
                        made.Add(
                            a.Y > b.Y
                                ? new Edge(edges.Count + made.Count, a, b)
                                : new Edge(edges.Count + made.Count, b, a)
                        );
                    }
                }

                MarkPointed(loop, orientation, eps, made);
                edges.AddRange(made);
            }

            heights.Sort((p, q) => q.CompareTo(p));
            var levels = new List<double>();

            foreach (double height in heights)
            {
                if (levels.Count == 0 || levels[levels.Count - 1] - height > eps)
                {
                    levels.Add(height);
                }
            }

            var cut = new List<Trapezoid>();
            var open = new List<Open>();

            for (int k = 0; k + 1 < levels.Count; k++)
            {
                double top = levels[k];
                double bottom = levels[k + 1];
                double middle = (top + bottom) * 0.5;
                var crossing = new List<Edge>();

                foreach (Edge edge in edges)
                {
                    if (edge.Top.Y >= top - eps && edge.Bottom.Y <= bottom + eps)
                    {
                        crossing.Add(edge);
                    }
                }

                crossing.Sort(
                    (p, q) =>
                    {
                        int c = p.XAt(middle).CompareTo(q.XAt(middle));
                        return c != 0 ? c : p.Id.CompareTo(q.Id);
                    }
                );
                var spans = new List<Open>();

                for (int i = 0; i + 1 < crossing.Count; i += 2)
                {
                    spans.Add(new Open(crossing[i], crossing[i + 1], top));
                }

                var carried = new List<Open>();

                foreach (Open was in open)
                {
                    Open same = null;

                    foreach (Open span in spans)
                    {
                        if (
                            span.Left == was.Left
                            && span.Right == was.Right
                            && was.Left.IsVertical(eps)
                            && was.Right.IsVertical(eps)
                        )
                        {
                            same = span;
                            break;
                        }
                    }

                    if (same == null)
                    {
                        cut.Add(Close(was, top, eps));
                    }
                    else
                    {
                        spans.Remove(same);
                        carried.Add(was);
                    }
                }

                carried.AddRange(spans);
                open = carried;
            }

            foreach (Open was in open)
            {
                cut.Add(Close(was, levels[levels.Count - 1], eps));
            }

            cut.RemoveAll(t => t == null);
            return cut;
        }

        /// <summary>Marks the edges that leave a corner where the area is pointed inwards: two edges that both go down, or both go up, from a corner the area wraps round.</summary>
        private static void MarkPointed(IReadOnlyList<GeoPoint2> loop, double orientation, double eps, List<Edge> made)
        {
            int n = loop.Count;

            for (int i = 0; i < n; i++)
            {
                GeoPoint2 before = loop[(i + n - 1) % n];
                GeoPoint2 corner = loop[i];
                GeoPoint2 after = loop[(i + 1) % n];
                bool down = before.Y < corner.Y - eps && after.Y < corner.Y - eps;
                bool up = before.Y > corner.Y + eps && after.Y > corner.Y + eps;

                if (!down && !up)
                {
                    continue;
                }

                double turn =
                    (corner.X - before.X) * (after.Y - corner.Y) - (corner.Y - before.Y) * (after.X - corner.X);

                if (turn * orientation >= 0.0)
                {
                    continue;
                }

                foreach (Edge edge in made)
                {
                    if (down && Near(edge.Top, corner, eps))
                    {
                        edge.TopPointed = true;
                    }
                    else if (up && Near(edge.Bottom, corner, eps))
                    {
                        edge.BottomPointed = true;
                    }
                }
            }
        }

        private static bool Near(GeoPoint2 a, GeoPoint2 b, double eps) =>
            Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps;

        private static double SignedArea(IReadOnlyList<GeoPoint2> loop)
        {
            double area = 0.0;

            for (int i = 0; i < loop.Count; i++)
            {
                GeoPoint2 a = loop[i];
                GeoPoint2 b = loop[(i + 1) % loop.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            return area;
        }

        /// <summary>The trapezoid a span of two edges made from its top to a level; null where it has no area.</summary>
        private static Trapezoid Close(Open span, double bottom, double eps)
        {
            double top = span.Top;
            GeoPoint2 topLeft = Corner(span.Left, top, eps);
            GeoPoint2 topRight = Corner(span.Right, top, eps);
            GeoPoint2 bottomLeft = Corner(span.Left, bottom, eps);
            GeoPoint2 bottomRight = Corner(span.Right, bottom, eps);
            bool topPoint = Math.Abs(topLeft.X - topRight.X) <= eps;
            bool bottomPoint = Math.Abs(bottomLeft.X - bottomRight.X) <= eps;

            if (topPoint && bottomPoint)
            {
                return null;
            }

            return new Trapezoid(topLeft, topRight, bottomLeft, bottomRight, topPoint, bottomPoint)
            {
                Level = bottomPoint ? top : bottom,
                LeftPointed = IsPointed(span.Left, top, bottom, eps),
                RightPointed = IsPointed(span.Right, top, bottom, eps),
            };
        }

        /// <summary>Whether a side that runs along an edge from a level to another leaves out a pointed end of it.</summary>
        private static bool IsPointed(Edge edge, double top, double bottom, double eps)
        {
            if (Math.Abs(edge.Top.X - edge.Bottom.X) <= eps)
            {
                return false;
            }

            return (edge.TopPointed && Math.Abs(edge.Top.Y - top) <= eps)
                || (edge.BottomPointed && Math.Abs(edge.Bottom.Y - bottom) <= eps);
        }

        /// <summary>Where an edge stands at a level: the corner itself where the level is its end, so that no rounding comes in.</summary>
        private static GeoPoint2 Corner(Edge edge, double y, double eps)
        {
            if (Math.Abs(edge.Top.Y - y) <= eps)
            {
                return new GeoPoint2(edge.Top.X, y);
            }

            if (Math.Abs(edge.Bottom.Y - y) <= eps)
            {
                return new GeoPoint2(edge.Bottom.X, y);
            }

            return new GeoPoint2(edge.XAt(y), y);
        }

        /// <summary>
        /// AutoCAD's order: from the highest level down; at one level the triangles that stand on a point first, then those that
        /// hang from one, then the rest. Of each kind the rest run from the right to the left, the triangles from the left to the
        /// right. Every tie is broken by the corners.
        /// </summary>
        internal static int Compare(Trapezoid a, Trapezoid b)
        {
            int c = b.Level.CompareTo(a.Level);

            if (c != 0)
            {
                return c;
            }

            c = a.Class.CompareTo(b.Class);

            if (c != 0)
            {
                return c;
            }

            double middleA = a.TopLeft.X + a.TopRight.X + a.BottomLeft.X + a.BottomRight.X;
            double middleB = b.TopLeft.X + b.TopRight.X + b.BottomLeft.X + b.BottomRight.X;
            c = a.Class == 2 ? middleB.CompareTo(middleA) : middleA.CompareTo(middleB);

            if (c != 0)
            {
                return c;
            }

            c = b.TopLeft.Y.CompareTo(a.TopLeft.Y);
            return c != 0 ? c : a.BottomLeft.X.CompareTo(b.BottomLeft.X);
        }
    }
}
