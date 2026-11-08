using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// A straight piece of a line of the view, seen or hidden along the whole of it, with the objects it is an edge of.
    /// </summary>
    internal struct Piece2
    {
        internal double X0, Y0, X1, Y1;
        internal EdgeVisibility Visibility;
        internal EdgeKind Kind;

        /// <summary>The numbers of the objects, ascending.</summary>
        internal int[] Objects;

        internal double Length => Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0));

        /// <summary>
        /// Orders pieces by their coordinates, then visibility, kind and objects: a full order, so that pieces equal under
        /// it are the same piece.
        /// </summary>
        internal static int Compare(Piece2 p, Piece2 q)
        {
            int c = p.X0.CompareTo(q.X0);
            if (c != 0) return c;
            c = p.Y0.CompareTo(q.Y0);
            if (c != 0) return c;
            c = p.X1.CompareTo(q.X1);
            if (c != 0) return c;
            c = p.Y1.CompareTo(q.Y1);
            if (c != 0) return c;
            c = p.Visibility.CompareTo(q.Visibility);
            if (c != 0) return c;
            c = p.Kind.CompareTo(q.Kind);
            return c != 0 ? c : CompareObjects(p.Objects, q.Objects);
        }

        /// <summary>
        /// Orders lists of objects element by element, a shorter list first where one starts the other.
        /// </summary>
        internal static int CompareObjects(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i].CompareTo(b[i]);
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        /// <summary>
        /// The piece turned, where needed, to run from its end lower in X, then in Y.
        /// </summary>
        internal Piece2 Canonical()
        {
            if (X0 < X1 || X0 == X1 && Y0 <= Y1)
            {
                return this;
            }

            Piece2 turned = this;
            turned.X0 = X1;
            turned.Y0 = Y1;
            turned.X1 = X0;
            turned.Y1 = Y0;
            return turned;
        }
    }

    /// <summary>
    /// Cuts each edge into the pieces seen and the pieces hidden behind the faces turned to the viewer, exactly on the
    /// planar faces, to the point tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An edge is hidden by a face where three things hold: it lies within the face seen in the view, the face's outline
    /// counting as within it to the point tolerance; it stands behind the face's plane by more than the point tolerance,
    /// measured square to the plane; and the face is not one of the faces the edge bounds. The distance to the plane is linear
    /// along the edge, so it passes the tolerance at most once, and the edge is split there: an edge piercing a face is
    /// seen on the near side and hidden on the far one.
    /// </para>
    /// <para>
    /// So an edge lying in a face's plane is not hidden by it, which keeps the edges of two plates touching face to face, or
    /// lying edge to edge with their faces flush, in view. A face seen edge-on, its normal within the vector tolerance of
    /// square to the view, hides nothing, nor does a face turned away, which a closed body always has another face in front
    /// of. An edge running along the outline of a face in front of it is hidden by it, and is then under the line of that
    /// outline, which is drawn instead.
    /// </para>
    /// </remarks>
    internal static class Occlusion
    {
        /// <summary>
        /// Gives the pieces of every edge, each seen or hidden; none shorter than the point tolerance.
        /// </summary>
        internal static List<Piece2> Run(List<CandidateEdge> edges, List<Occluder> occluders, double tol)
        {
            // The faces nearest the viewer first, so that the cells of the grid list them front to back and an edge a face
            // hides whole is mostly found so before the faces behind it are read. The order of the faces changes nothing
            // else: the spans they hide are sorted before they are joined.
            int count = occluders.Count;
            var byDepth = new Occluder[count];

            for (int i = 0; i < count; i++)
            {
                byDepth[i] = occluders[i];
            }

            Array.Sort(byDepth, (a, b) =>
            {
                int c = b.MaxZ.CompareTo(a.MaxZ);
                return c != 0 ? c : a.Face.CompareTo(b.Face);
            });
            var minX = new double[count];
            var minY = new double[count];
            var maxX = new double[count];
            var maxY = new double[count];

            for (int i = 0; i < count; i++)
            {
                minX[i] = byDepth[i].MinX;
                minY[i] = byDepth[i].MinY;
                maxX[i] = byDepth[i].MaxX;
                maxY[i] = byDepth[i].MaxY;
            }

            var grid = new BoxGrid2(minX, minY, maxX, maxY, count, tol);
            var found = new List<int>();
            var hidden = new List<Span>();
            var parameters = new List<double>();
            var pieces = new List<Piece2>(edges.Count * 2);

            foreach (CandidateEdge edge in edges)
            {
                hidden.Clear();

                if (count > 0)
                {
                    grid.QuerySegment(edge.Ax, edge.Ay, edge.Bx, edge.By, tol, found);
                    double dx = edge.Bx - edge.Ax;
                    double dy = edge.By - edge.Ay;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    double reach = tol / length;

                    foreach (int k in found)
                    {
                        int before = hidden.Count;
                        HiddenBy(edge, byDepth[k], tol, hidden, parameters);

                        if (HidesWhole(hidden, before, length, reach, tol))
                        {
                            break;
                        }
                    }
                }

                Split(edge, hidden, tol, pieces);
            }

            return pieces;
        }

        /// <summary>
        /// Whether one of the spans from a place in the list on reaches both ends of the edge, as <see cref="Split"/> reads
        /// them, and is longer than the tolerance: any further span would be joined into it, and the edge is hidden whole
        /// whatever the faces still to be read hide.
        /// </summary>
        private static bool HidesWhole(List<Span> hidden, int from, double length, double reach, double tol)
        {
            for (int i = from; i < hidden.Count; i++)
            {
                Span span = hidden[i];

                if (span.From <= reach && span.To >= 1.0 - reach && (span.To - span.From) * length > tol)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds the spans of an edge, as parameters from nought at its start to one at its end, that a face hides.
        /// </summary>
        private static void HiddenBy(CandidateEdge edge, Occluder o, double tol, List<Span> hidden, List<double> parameters)
        {
            if (Math.Max(edge.Ax, edge.Bx) < o.MinX - tol || Math.Min(edge.Ax, edge.Bx) > o.MaxX + tol
                || Math.Max(edge.Ay, edge.By) < o.MinY - tol || Math.Min(edge.Ay, edge.By) > o.MaxY + tol)
            {
                return;
            }

            // Nothing of an edge in front of the whole face can be behind it.
            if (Math.Min(edge.Az, edge.Bz) >= o.MaxZ - tol)
            {
                return;
            }

            foreach (int face in edge.Faces)
            {
                if (face == o.Face)
                {
                    return;
                }
            }

            // How far each end stands in front of the plane, square to it; below minus the tolerance is behind.
            double sa = o.Nx * edge.Ax + o.Ny * edge.Ay + o.Nz * edge.Az - o.D;
            double sb = o.Nx * edge.Bx + o.Ny * edge.By + o.Nz * edge.Bz - o.D;

            if (sa >= -tol && sb >= -tol)
            {
                return;
            }

            double t0 = 0.0;
            double t1 = 1.0;

            if (sa >= -tol)
            {
                t0 = (-tol - sa) / (sb - sa);
            }
            else if (sb >= -tol)
            {
                t1 = (-tol - sa) / (sb - sa);
            }

            Clip(edge, o, t0, t1, tol, hidden, parameters);
        }

        /// <summary>
        /// Adds the spans between two parameters of an edge that lie within a face seen in the view, its outline counting as
        /// within it to the tolerance.
        /// </summary>
        private static void Clip(CandidateEdge edge, Occluder o, double t0, double t1, double tol, List<Span> hidden, List<double> parameters)
        {
            double ax = edge.Ax;
            double ay = edge.Ay;
            double rx = edge.Bx - ax;
            double ry = edge.By - ay;
            double length = Math.Sqrt(rx * rx + ry * ry);
            parameters.Clear();
            parameters.Add(t0);
            parameters.Add(t1);

            for (int l = 0; l + 1 < o.Starts.Length; l++)
            {
                int start = o.Starts[l];
                int end = o.Starts[l + 1];

                for (int i = start; i < end; i++)
                {
                    int j = i + 1 < end ? i + 1 : start;
                    double cx = o.Xs[i];
                    double cy = o.Ys[i];
                    double sx = o.Xs[j] - cx;
                    double sy = o.Ys[j] - cy;
                    double qx = cx - ax;
                    double qy = cy - ay;

                    // A corner of the face on the edge's line splits the edge there, which is how an edge running along a
                    // side of the face, or touching a corner of it, is cut where the side or the corner ends.
                    if (Math.Abs(rx * qy - ry * qx) <= tol * length)
                    {
                        double t = (qx * rx + qy * ry) / (length * length);

                        if (t > t0 && t < t1)
                        {
                            parameters.Add(t);
                        }
                    }

                    double denominator = rx * sy - ry * sx;
                    double side = Math.Sqrt(sx * sx + sy * sy);

                    if (Math.Abs(denominator) > 1E-12 * length * side)
                    {
                        double t = (qx * sy - qy * sx) / denominator;
                        double u = (qx * ry - qy * rx) / denominator;
                        double reach = tol / side;

                        if (u >= -reach && u <= 1.0 + reach && t > t0 && t < t1)
                        {
                            parameters.Add(t);
                        }
                    }
                }
            }

            parameters.Sort();
            double open = double.NaN;
            double close = double.NaN;

            for (int i = 0; i + 1 < parameters.Count; i++)
            {
                double p = parameters[i];
                double q = parameters[i + 1];

                if ((q - p) * length <= 1E-9)
                {
                    continue;
                }

                double m = 0.5 * (p + q);

                if (Covers(o, ax + m * rx, ay + m * ry, tol))
                {
                    if (!double.IsNaN(open) && p <= close)
                    {
                        close = q;
                    }
                    else
                    {
                        if (!double.IsNaN(open))
                        {
                            hidden.Add(new Span(open, close));
                        }

                        open = p;
                        close = q;
                    }
                }
            }

            if (!double.IsNaN(open))
            {
                hidden.Add(new Span(open, close));
            }
        }

        /// <summary>
        /// Whether a point of the view lies within a face seen in it, or within the tolerance of its outline.
        /// </summary>
        private static bool Covers(Occluder o, double px, double py, double tol)
        {
            if (px < o.MinX - tol || px > o.MaxX + tol || py < o.MinY - tol || py > o.MaxY + tol)
            {
                return false;
            }

            bool inside = false;
            double tol2 = tol * tol;

            for (int l = 0; l + 1 < o.Starts.Length; l++)
            {
                int start = o.Starts[l];
                int end = o.Starts[l + 1];

                for (int i = start; i < end; i++)
                {
                    int j = i + 1 < end ? i + 1 : start;
                    double cx = o.Xs[i];
                    double cy = o.Ys[i];
                    double dx = o.Xs[j] - cx;
                    double dy = o.Ys[j] - cy;
                    double length2 = dx * dx + dy * dy;
                    double t = length2 > 0.0 ? ((px - cx) * dx + (py - cy) * dy) / length2 : 0.0;
                    t = Math.Max(0.0, Math.Min(1.0, t));
                    double ex = cx + t * dx - px;
                    double ey = cy + t * dy - py;

                    if (ex * ex + ey * ey <= tol2)
                    {
                        return true;
                    }

                    if (cy > py != o.Ys[j] > py && px < cx + (py - cy) * dx / dy)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        /// <summary>
        /// Cuts an edge at the ends of its hidden spans: the spans are joined where they overlap or stand within the
        /// tolerance of each other, a hidden span no longer than the tolerance is taken as seen, and no piece of either
        /// kind is kept that is no longer than the tolerance.
        /// </summary>
        private static void Split(CandidateEdge edge, List<Span> hidden, double tol, List<Piece2> pieces)
        {
            double dx = edge.Bx - edge.Ax;
            double dy = edge.By - edge.Ay;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double reach = tol / length;
            hidden.Sort((p, q) => p.From != q.From ? p.From.CompareTo(q.From) : p.To.CompareTo(q.To));
            var merged = new List<Span>(hidden.Count);

            foreach (Span span in hidden)
            {
                if (merged.Count > 0 && span.From <= merged[merged.Count - 1].To + reach)
                {
                    Span last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new Span(last.From, Math.Max(last.To, span.To));
                }
                else
                {
                    merged.Add(span);
                }
            }

            double at = 0.0;

            foreach (Span span in merged)
            {
                if ((span.To - span.From) * length <= tol)
                {
                    continue;
                }

                double from = span.From <= reach ? 0.0 : span.From;
                double to = span.To >= 1.0 - reach ? 1.0 : span.To;
                Add(edge, at, from, EdgeVisibility.Visible, tol, length, pieces);
                Add(edge, from, to, EdgeVisibility.Hidden, tol, length, pieces);
                at = to;
            }

            Add(edge, at, 1.0, EdgeVisibility.Visible, tol, length, pieces);
        }

        private static void Add(CandidateEdge edge, double from, double to, EdgeVisibility visibility, double tol, double length, List<Piece2> pieces)
        {
            if ((to - from) * length <= tol)
            {
                return;
            }

            double dx = edge.Bx - edge.Ax;
            double dy = edge.By - edge.Ay;
            var piece = new Piece2
            {
                X0 = from == 0.0 ? edge.Ax : edge.Ax + from * dx,
                Y0 = from == 0.0 ? edge.Ay : edge.Ay + from * dy,
                X1 = to == 1.0 ? edge.Bx : edge.Ax + to * dx,
                Y1 = to == 1.0 ? edge.By : edge.Ay + to * dy,
                Visibility = visibility,
                Kind = edge.Kind,
                Objects = new[] { edge.Object },
            };

            pieces.Add(piece.Canonical());
        }

        /// <summary>
        /// A span of an edge between two parameters.
        /// </summary>
        private readonly struct Span
        {
            internal Span(double from, double to)
            {
                From = from;
                To = to;
            }

            internal double From { get; }

            internal double To { get; }
        }
    }
}
