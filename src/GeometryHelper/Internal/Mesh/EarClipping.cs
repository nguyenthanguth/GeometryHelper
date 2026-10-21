using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Breaks a flat face into triangles that each lie inside it, holes included.
    /// <para>
    /// This is what a fan triangulation cannot give. Fanning a polygon from one vertex covers a convex
    /// one exactly, but on a concave one some triangles reach outside the boundary and others overlap
    /// them wound the other way. For a signed sum — area, centroid, volume — that is harmless, because
    /// the overhang cancels against the overlap. For anything geometric it is not: a triangle spanning
    /// the notch of an L is material where there is none, and whatever reads the mesh as a surface
    /// believes it.
    /// </para>
    /// <para>
    /// The face is mapped into the 2D frame of its own plane, the holes are bridged into the outer loop
    /// so that one simple loop describes the whole face, and that loop is reduced by clipping ears. Every
    /// vertex of the result is one of the vertices that went in — the bridging joins existing vertices
    /// rather than inventing points — so the triangles are carried back to 3D by looking the originals up
    /// rather than by mapping coordinates back, and no round-trip error is introduced.
    /// </para>
    /// <para>
    /// The same works on loops of corners a little out of flat, seen from a frame of their own: the triangles are
    /// built on the corners themselves, so each is flat, and that is how <see cref="Loops3"/> keeps such a face.
    /// </para>
    /// </summary>
    internal static class EarClipping
    {
        /// <summary>
        /// One vertex of the working loop: where it sits in the plane, and the point it came from.
        /// </summary>
        internal struct Node
        {
            public double X;
            public double Y;
            public GeoPoint3 Source;

            public Node(double x, double y, GeoPoint3 source)
            {
                X = x;
                Y = y;
                Source = source;
            }
        }

        /// <summary>
        /// Triangulates a face into triangles that each lie within its material.
        /// </summary>
        /// <param name="face">The face to break up.</param>
        /// <param name="tolerance">The tolerance deciding what counts as a degenerate triangle.</param>
        /// <param name="triangles">The triangles covering the face, wound to share its normal.</param>
        /// <returns>
        /// false when the loop could not be reduced — a self-intersecting boundary, or a hole that
        /// reaches outside the face. The caller is expected to fall back rather than to treat this as an
        /// error.
        /// </returns>
        public static bool TryTriangulate(GeoFace3 face, Tolerance tolerance, out GeoTriangle3[] triangles)
        {
            return TryTriangulate(face, tolerance, false, out triangles);
        }

        /// <summary>
        /// Triangulates a face for a mesh of its surface, refusing where the triangles might not all lie within it.
        /// </summary>
        /// <param name="face">The face to break up.</param>
        /// <param name="tolerance">The tolerance deciding what counts as a degenerate triangle, and how near two rings may come.</param>
        /// <param name="triangles">The triangles covering the face, wound to share its normal.</param>
        /// <returns>
        /// false when the loop could not be reduced, as <see cref="TryTriangulate(GeoFace3, Tolerance, out GeoTriangle3[])"/>
        /// returns, and also when two rings of the face come within the point tolerance of each other, or a ring of itself
        /// apart from where its edges meet.
        /// </returns>
        /// <remarks>
        /// The clipping holds only while the loop it reduces is simple, and a loop joined from rings that touch is not. A
        /// hole whose edge lay along the boundary was bridged along that edge, the loop doubled back on itself there, and
        /// the clipping, which only ever cuts off a corner turning the right way, was left with a last triangle turning the
        /// wrong way, laid across the notch of an L. Triangular holes meeting at their corners left a triangle across one
        /// of them. The sums of the areas came out right both times, so nothing showed. A triangle wound backwards still
        /// adds up, and the face keeps its own corners, which is what <see cref="Loops3"/> and the booleans need of the
        /// clipping; a mesh standing for the surface needs every triangle within the material, so such a face is refused
        /// here instead.
        /// </remarks>
        public static bool TryTriangulateSurface(GeoFace3 face, Tolerance tolerance, out GeoTriangle3[] triangles)
        {
            return TryTriangulate(face, tolerance, true, out triangles);
        }

        private static bool TryTriangulate(GeoFace3 face, Tolerance tolerance, bool surface, out GeoTriangle3[] triangles)
        {
            triangles = null;

            if (face == null)
            {
                return false;
            }

            var holes = new IReadOnlyList<GeoPoint3>[face.Holes.Count];

            for (int i = 0; i < holes.Length; i++)
            {
                holes[i] = face.Holes[i].Vertices;
            }

            return TryTriangulate(face.Boundary.Vertices, holes, new GeoCoordinateSystem3(face.GetPlane()), tolerance, surface, out triangles);
        }

        /// <summary>
        /// Triangulates loops of corners as they are seen from a frame, into triangles on the corners themselves.
        /// </summary>
        /// <param name="boundary">The corners of the outer loop, in order.</param>
        /// <param name="holes">The corners of each hole, in order, wound either way.</param>
        /// <param name="frame">The frame the loops are seen in; the triangles face along its Z axis.</param>
        /// <param name="tolerance">The tolerance deciding what counts as a degenerate triangle.</param>
        /// <param name="triangles">The triangles covering the loops.</param>
        /// <returns>false when the loops, as seen from the frame, could not be reduced; see the face overload.</returns>
        /// <remarks>
        /// The loops need not lie flat. How far a corner stands off the frame's plane is dropped, so loops a little out
        /// of flat are split as their outline on that plane is, and each triangle, being three of the corners, is flat
        /// whatever the loops are.
        /// </remarks>
        public static bool TryTriangulate(IReadOnlyList<GeoPoint3> boundary, IReadOnlyList<IReadOnlyList<GeoPoint3>> holes, GeoCoordinateSystem3 frame, Tolerance tolerance, out GeoTriangle3[] triangles)
        {
            return TryTriangulate(boundary, holes, frame, tolerance, false, out triangles);
        }

        private static bool TryTriangulate(IReadOnlyList<GeoPoint3> boundary, IReadOnlyList<IReadOnlyList<GeoPoint3>> holes, GeoCoordinateSystem3 frame, Tolerance tolerance, bool surface, out GeoTriangle3[] triangles)
        {
            triangles = null;

            double low = double.PositiveInfinity, high = double.NegativeInfinity;
            List<Node> outer = Project(boundary, frame, ref low, ref high);

            if (outer.Count < 3)
            {
                return false;
            }

            // The frame takes the face normal to local Z, so a boundary wound along that normal comes out
            // counter-clockwise here. Ear clipping is written for one winding only, so the loop is turned
            // the right way round rather than the test being written twice.
            if (SignedArea(outer) < 0.0)
            {
                outer.Reverse();
            }

            List<List<Node>> rings = new List<List<Node>>();

            foreach (IReadOnlyList<GeoPoint3> hole in holes)
            {
                List<Node> ring = Project(hole, frame, ref low, ref high);

                if (ring.Count < 3)
                {
                    continue;
                }

                // A hole runs against the boundary, so that the material is always on the same side of
                // every edge once the two are joined into one loop.
                if (SignedArea(ring) > 0.0)
                {
                    ring.Reverse();
                }

                rings.Add(ring);
            }

            if (surface && RingsMeet(outer, rings, tolerance.EqualPoint))
            {
                return false;
            }

            if (rings.Count > 0 && !TryBridgeHoles(outer, rings, tolerance.EqualPoint, out outer))
            {
                return false;
            }

            if (!TryClip(outer, tolerance, tolerance.EqualPoint, surface, out triangles))
            {
                return false;
            }

            // Seen from the frame, a corner further than the point tolerance outside an ear's edge can still be a long way
            // off it along the frame's Z, and the ear passing it leave a triangle standing up across the loop: an L with its
            // inner corner a millimetre up, read within a thousandth, took the ear across its diagonal that the inner corner
            // passed 0.005 outside, and what was left closed with a triangle standing up along that diagonal. Blocking every
            // ear within how far out of flat the loops are did not do: it turned clipping away from good ears onto the sliver
            // along a straight run with a corner lifted on it, which stood up instead, and on loops far out of flat it changed
            // what the booleans were left with. So the loops are clipped again only when a triangle stands up, blocking ten
            // times wider each time up to how far out of flat they are, and the first clipping with none standing up is
            // taken; when there is none, the first is kept. Blocking starts no nearer than a millionth of how far out of flat
            // they are, so that a tolerance of nothing, or next to it, cannot keep them being clipped again.
            double offFlat = high - low;

            if (offFlat > tolerance.EqualPoint && StandsUp(triangles, frame.ZAxis))
            {
                for (double near = Math.Max(10.0 * tolerance.EqualPoint, 1E-6 * offFlat); ; near *= 10.0)
                {
                    double blocking = Math.Min(near, offFlat);

                    if (TryClip(outer, tolerance, blocking, surface, out GeoTriangle3[] wider) && !StandsUp(wider, frame.ZAxis))
                    {
                        triangles = wider;
                        break;
                    }

                    if (blocking >= offFlat)
                    {
                        break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether any of the triangles stands up off a frame's plane: faces more than 60 degrees away from its Z.
        /// </summary>
        private static bool StandsUp(GeoTriangle3[] triangles, GeoVector3 axis)
        {
            foreach (GeoTriangle3 triangle in triangles)
            {
                GeoVector3 area = triangle.GetAreaVector();

                if (area.DotProduct(axis) < 0.5 * area.Length)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Maps a ring into the 2D frame of the face, keeping each original point alongside, and widens the span of the
        /// heights its corners stand at over the frame's plane.
        /// </summary>
        private static List<Node> Project(IReadOnlyList<GeoPoint3> vertices, GeoCoordinateSystem3 frame, ref double low, ref double high)
        {
            List<Node> nodes = new List<Node>(vertices.Count);

            foreach (GeoPoint3 vertex in vertices)
            {
                GeoPoint3 local = frame.ToLocal(vertex);
                low = Math.Min(low, local.Z);
                high = Math.Max(high, local.Z);

                // The local Z is dropped rather than checked. For a face, coplanarity was settled when the
                // polygon and the face were built, and what little is left of it is the deviation those
                // constructors already accepted; loops out of flat are split as they are seen from the frame.
                nodes.Add(new Node(local.X, local.Y, vertex));
            }

            return nodes;
        }

        /// <summary>
        /// Gets twice the signed area of a loop, which is positive when it runs counter-clockwise.
        /// </summary>
        private static double SignedArea(List<Node> loop)
        {
            double total = 0.0;

            for (int i = 0; i < loop.Count; i++)
            {
                Node current = loop[i];
                Node next = loop[(i + 1) % loop.Count];

                total += current.X * next.Y - next.X * current.Y;
            }

            return total;
        }

        #region Rings apart

        /// <summary>
        /// One edge of a ring, for <see cref="RingsMeet"/>: its ends, and where it stands in which ring.
        /// </summary>
        internal struct RingEdge
        {
            public Node A;
            public Node B;
            public int Ring;
            public int Index;
            public int Count;
            public double MinX;
            public double MaxX;
        }

        /// <summary>
        /// Determines whether two edges of the rings come within a distance of each other, two edges of one ring meeting
        /// at a corner excepted.
        /// </summary>
        /// <remarks>
        /// The edges are taken left to right, each against those whose span across begins before its own ends, so that
        /// only edges near each other are measured. Rings whose edges overlap more along X than along Y, as a plate of
        /// slots along the frame's X, are taken bottom to top instead; see <see cref="RingsMeetAlongY"/>.
        /// </remarks>
        internal static bool RingsMeet(List<Node> outer, List<List<Node>> holes, double distance)
        {
            var edges = new List<RingEdge>();

            void Add(List<Node> ring, int index)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    Node a = ring[i];
                    Node b = ring[(i + 1) % ring.Count];
                    edges.Add(new RingEdge { A = a, B = b, Ring = index, Index = i, Count = ring.Count, MinX = Math.Min(a.X, b.X), MaxX = Math.Max(a.X, b.X) });
                }
            }

            Add(outer, 0);

            for (int h = 0; h < holes.Count; h++)
            {
                Add(holes[h], h + 1);
            }

            edges.Sort((left, right) => left.MinX.CompareTo(right.MinX));

            if (edges.Count >= SweepAxisFrom && SweepAxis(edges, distance) == 1)
            {
                return RingsMeetAlongY(edges, distance);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                RingEdge e = edges[i];
                double minY = Math.Min(e.A.Y, e.B.Y) - distance;
                double maxY = Math.Max(e.A.Y, e.B.Y) + distance;

                for (int j = i + 1; j < edges.Count && edges[j].MinX <= e.MaxX + distance; j++)
                {
                    RingEdge f = edges[j];

                    if (Math.Max(f.A.Y, f.B.Y) < minY || Math.Min(f.A.Y, f.B.Y) > maxY)
                    {
                        continue;
                    }

                    if (e.Ring == f.Ring && (Math.Abs(e.Index - f.Index) == 1 || Math.Abs(e.Index - f.Index) == e.Count - 1))
                    {
                        continue;
                    }

                    if (SegmentsWithin(e.A, e.B, f.A, f.B, distance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// How many edges the rings need before <see cref="RingsMeet"/> chooses the axis it sweeps along. Choosing costs
        /// two sorts, more than a sweep along X of a few edges costs, so fewer are swept along X; both give the same
        /// answer.
        /// </summary>
        internal const int SweepAxisFrom = 64;

        /// <summary>
        /// The axis a sweep of the edges looks at the fewest pairs along, 0 for X and 1 for Y, X where both look at as many.
        /// </summary>
        /// <param name="edges">The edges.</param>
        /// <param name="distance">How far apart two edges may stand and still be measured.</param>
        internal static int SweepAxis(List<RingEdge> edges, double distance)
        {
            int count = edges.Count;
            var lows = new double[count];
            var highs = new double[count];

            for (int i = 0; i < count; i++)
            {
                lows[i] = edges[i].MinX;
                highs[i] = edges[i].MaxX + distance;
            }

            Array.Sort(lows);
            long alongX = Sweeps.Tried(lows, highs);

            for (int i = 0; i < count; i++)
            {
                RingEdge e = edges[i];
                lows[i] = Math.Min(e.A.Y, e.B.Y);
                highs[i] = Math.Max(e.A.Y, e.B.Y) + distance;
            }

            Array.Sort(lows);
            return Sweeps.Tried(lows, highs) < alongX ? 1 : 0;
        }

        /// <summary>
        /// Determines whether two edges of the rings come within a distance of each other, taking the edges bottom to
        /// top, and measuring every pair the sweep left to right measures, as it measures them.
        /// </summary>
        /// <param name="edges">The edges, in the order the sort left to right left them.</param>
        /// <param name="distance">The distance.</param>
        /// <remarks>
        /// The sweep left to right asked whether the later of two edges in its order lies within the span across of the
        /// earlier, and that is not asked the same, to the last bit, the other way round. So whichever of two edges this
        /// sweep comes to second, their spans are compared the way round the sweep left to right compared them; the
        /// measuring itself is the same either way round. An edge is no longer compared once the next starts clear of it
        /// either way round.
        /// </remarks>
        private static bool RingsMeetAlongY(List<RingEdge> edges, double distance)
        {
            int count = edges.Count;
            var lows = new double[count];
            var highs = new double[count];
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                RingEdge e = edges[i];
                lows[i] = Math.Min(e.A.Y, e.B.Y);
                highs[i] = Math.Max(e.A.Y, e.B.Y);
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byLow = lows[a].CompareTo(lows[b]);
                return byLow != 0 ? byLow : a.CompareTo(b);
            });

            for (int k = 0; k < count; k++)
            {
                int p = order[k];

                for (int m = k + 1; m < count; m++)
                {
                    int q = order[m];

                    if (lows[q] > highs[p] + distance && lows[q] - distance > highs[p])
                    {
                        break;
                    }

                    RingEdge e = edges[Math.Min(p, q)];
                    RingEdge f = edges[Math.Max(p, q)];

                    if (f.MinX > e.MaxX + distance)
                    {
                        continue;
                    }

                    double minY = Math.Min(e.A.Y, e.B.Y) - distance;
                    double maxY = Math.Max(e.A.Y, e.B.Y) + distance;

                    if (Math.Max(f.A.Y, f.B.Y) < minY || Math.Min(f.A.Y, f.B.Y) > maxY)
                    {
                        continue;
                    }

                    if (e.Ring == f.Ring && (Math.Abs(e.Index - f.Index) == 1 || Math.Abs(e.Index - f.Index) == e.Count - 1))
                    {
                        continue;
                    }

                    if (SegmentsWithin(e.A, e.B, f.A, f.B, distance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two segments cross or come within a distance of each other.
        /// </summary>
        /// <remarks>
        /// The same either way round: the two segments taken the other way round swap the cross products and the
        /// distances among themselves.
        /// </remarks>
        internal static bool SegmentsWithin(Node a, Node b, Node c, Node d, double distance)
        {
            double d1 = Cross(a, b, c), d2 = Cross(a, b, d), d3 = Cross(c, d, a), d4 = Cross(c, d, b);

            if (((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0)) && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0)))
            {
                return true;
            }

            return ToSegment(a, c, d) <= distance || ToSegment(b, c, d) <= distance || ToSegment(c, a, b) <= distance || ToSegment(d, a, b) <= distance;
        }

        /// <summary>
        /// Gets the distance from a point to a segment.
        /// </summary>
        private static double ToSegment(Node point, Node a, Node b)
        {
            double ex = b.X - a.X, ey = b.Y - a.Y;
            double lengthSquared = ex * ex + ey * ey;
            double t = lengthSquared > 0.0 ? ((point.X - a.X) * ex + (point.Y - a.Y) * ey) / lengthSquared : 0.0;
            t = Math.Max(0.0, Math.Min(1.0, t));
            double dx = a.X + t * ex - point.X, dy = a.Y + t * ey - point.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        #endregion

        #region Holes

        /// <summary>
        /// Joins every hole into the outer loop, leaving one simple loop describing the whole face.
        /// </summary>
        /// <remarks>
        /// Each hole is cut into the boundary along a bridge traversed once each way. That leaves a loop
        /// with zero width at the bridge, which is exactly what makes the material of the face into one
        /// connected region that ear clipping can reduce.
        /// <para>
        /// The holes are taken rightmost first. A bridge is drawn to the outer loop as it stands, so a
        /// hole joined earlier is already part of it and a later bridge can land on that hole instead of
        /// crossing it. Working from the right means the hole a bridge would have to cross has always
        /// been dealt with already.
        /// </para>
        /// </remarks>
        private static bool TryBridgeHoles(List<Node> outer, List<List<Node>> holes, double pointEpsilon, out List<Node> merged)
        {
            merged = outer;

            holes.Sort((left, right) => MaxX(right).CompareTo(MaxX(left)));

            foreach (List<Node> hole in holes)
            {
                if (!TryBridgeHole(merged, hole, pointEpsilon, out merged))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the largest X in a ring.
        /// </summary>
        private static double MaxX(List<Node> ring)
        {
            double best = double.NegativeInfinity;

            foreach (Node node in ring)
            {
                if (node.X > best)
                {
                    best = node.X;
                }
            }

            return best;
        }

        /// <summary>
        /// Joins one hole into the loop it sits inside.
        /// </summary>
        /// <remarks>
        /// The bridge starts at the rightmost vertex of the hole and runs to the right, because nothing of
        /// the hole itself lies that way and the first thing met is therefore outside it. Where the ray
        /// lands on an edge rather than a vertex, the corner of that edge is taken as the far end — but a
        /// reflex corner of the loop can stand between the two and be cut through, so those are checked
        /// and the one turning least away from the ray is used instead.
        /// </remarks>
        private static bool TryBridgeHole(List<Node> outer, List<Node> hole, double pointEpsilon, out List<Node> merged)
        {
            merged = outer;

            int start = 0;
            for (int i = 1; i < hole.Count; i++)
            {
                if (hole[i].X > hole[start].X || (hole[i].X == hole[start].X && hole[i].Y > hole[start].Y))
                {
                    start = i;
                }
            }

            Node origin = hole[start];

            double closestX = double.PositiveInfinity;
            int edgeIndex = -1;

            for (int i = 0; i < outer.Count; i++)
            {
                Node a = outer[i];
                Node b = outer[(i + 1) % outer.Count];

                // Only an edge straddling the ray in Y can be hit by it. Taking one end as inclusive and
                // the other as exclusive counts a vertex the ray passes exactly through once rather than
                // twice.
                if ((a.Y > origin.Y) == (b.Y > origin.Y))
                {
                    continue;
                }

                double crossingX = a.X + (origin.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X);

                if (crossingX >= origin.X && crossingX < closestX)
                {
                    closestX = crossingX;
                    edgeIndex = i;
                }
            }

            if (edgeIndex < 0)
            {
                // Nothing to the right means the hole is not inside this loop at all.
                return false;
            }

            int target = outer[edgeIndex].X > outer[(edgeIndex + 1) % outer.Count].X
                ? edgeIndex
                : (edgeIndex + 1) % outer.Count;

            target = ResolveBlockingReflex(outer, origin, closestX, target);
            target = NearestInTheWay(outer, origin, target, pointEpsilon);
            target = CopyOpeningToward(outer, target, origin);

            List<Node> bridged = new List<Node>(outer.Count + hole.Count + 2);

            for (int i = 0; i <= target; i++)
            {
                bridged.Add(outer[i]);
            }

            for (int k = 0; k < hole.Count; k++)
            {
                bridged.Add(hole[(start + k) % hole.Count]);
            }

            // The bridge is walked back the other way: the hole is re-entered at the vertex it was entered
            // from, and the loop resumes at the vertex it left.
            bridged.Add(hole[start]);
            bridged.Add(outer[target]);

            for (int i = target + 1; i < outer.Count; i++)
            {
                bridged.Add(outer[i]);
            }

            merged = bridged;
            return true;
        }

        /// <summary>
        /// Picks, among the copies of the vertex a bridge is to reach, the one that opens toward the hole.
        /// </summary>
        /// <remarks>
        /// A vertex an earlier bridge reached stands in the loop twice, once on each side of that bridge, and each copy
        /// opens onto its own part of the face. A second bridge to the same vertex has to leave from the copy that opens
        /// toward its hole: from the other, it runs across the first bridge, the loop crosses itself, and no ear can be
        /// clipped from it. The side of a notched beam met this, two of its three openings bridged to one corner of the
        /// web, and the face fell back to the fan, laid over its openings.
        /// </remarks>
        private static int CopyOpeningToward(List<Node> loop, int target, Node toward)
        {
            Node corner = loop[target];

            if (Opens(loop, target, toward))
            {
                return target;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                if (i != target && loop[i].X == corner.X && loop[i].Y == corner.Y && Opens(loop, i, toward))
                {
                    return i;
                }
            }

            return target;
        }

        /// <summary>
        /// Determines whether a point lies within the corner of the loop at a vertex, on the side the face is.
        /// </summary>
        private static bool Opens(List<Node> loop, int index, Node toward)
        {
            Node previous = loop[(index - 1 + loop.Count) % loop.Count];
            Node current = loop[index];
            Node next = loop[(index + 1) % loop.Count];

            // The loop runs counter-clockwise, so the face is on the left of the edge leaving the vertex and on the left
            // of the edge arriving; a convex corner is where both hold, a reflex one where either does.
            bool leftOfLeaving = Cross(current, next, toward) >= 0.0;
            bool leftOfArriving = Cross(previous, current, toward) >= 0.0;

            return Cross(previous, current, next) > 0.0
                ? leftOfLeaving && leftOfArriving
                : leftOfLeaving || leftOfArriving;
        }

        /// <summary>
        /// Picks the vertex the bridge should reach, given that a reflex corner may stand in the way.
        /// </summary>
        /// <remarks>
        /// The candidate corner of the edge the ray hit is visible from the hole unless some reflex vertex
        /// of the loop lies within the triangle the bridge would sweep. When one does, the bridge would
        /// cut across the boundary, so the reflex vertex turning least away from the ray is taken instead:
        /// it is the first thing the bridge can see.
        /// </remarks>
        private static int ResolveBlockingReflex(List<Node> outer, Node origin, double crossingX, int candidate)
        {
            Node hit = new Node(crossingX, origin.Y, origin.Source);
            Node corner = outer[candidate];

            int best = candidate;
            double bestTangent = double.PositiveInfinity;
            double bestDistance = double.PositiveInfinity;

            for (int i = 0; i < outer.Count; i++)
            {
                if (i == candidate)
                {
                    continue;
                }

                Node previous = outer[(i - 1 + outer.Count) % outer.Count];
                Node current = outer[i];
                Node next = outer[(i + 1) % outer.Count];

                if (Cross(previous, current, next) > 0.0)
                {
                    // Convex corners point away from the interior and can never block the bridge.
                    continue;
                }

                if (!InTriangle(origin, hit, corner, current))
                {
                    continue;
                }

                double dx = current.X - origin.X;
                double dy = current.Y - origin.Y;

                if (dx <= 0.0)
                {
                    continue;
                }

                // How far the vertex turns off the ray, measured as a slope so that no angle has to be
                // taken. Nearer wins when two turn by the same amount.
                double tangent = Math.Abs(dy) / dx;
                double distance = dx * dx + dy * dy;

                if (tangent < bestTangent || (tangent == bestTangent && distance < bestDistance))
                {
                    bestTangent = tangent;
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Walks back from the corner a bridge is to reach to the nearest corner lying within the tolerance of the bridge,
        /// which is the one it meets first.
        /// </summary>
        /// <remarks>
        /// Corners in a line with the bridge, as the corners of holes in a row are, turn about as little off the ray as each
        /// other, and of corners a hair off that line the far one turns least: the same rise over a longer run. A bridge to
        /// it ran along the edge of the near hole, the loop touched itself there, and no ear could be clipped from it. The
        /// pits of a slab, their edges along a row 4.66E-8 out of line, failed so, and the face was meshed as the fan of its
        /// outline, laid across every pit.
        /// </remarks>
        private static int NearestInTheWay(List<Node> loop, Node origin, int target, double pointEpsilon)
        {
            while (true)
            {
                Node end = loop[target];
                double ex = end.X - origin.X, ey = end.Y - origin.Y;
                double length = Math.Sqrt(ex * ex + ey * ey);

                if (length <= pointEpsilon)
                {
                    return target;
                }

                // Short of it by more than the tolerance, and with none by more than rounding: read with no tolerance, a copy
                // of the corner that an earlier bridge left measured a hair short of the corner itself, and the walk went from
                // one copy to the other for ever.
                int nearer = -1;
                double nearest = length - Math.Max(pointEpsilon, length * 1E-12);

                for (int i = 0; i < loop.Count; i++)
                {
                    double px = loop[i].X - origin.X, py = loop[i].Y - origin.Y;
                    double along = (px * ex + py * ey) / length;

                    // Between the hole and the corner, short of it by more than the tolerance, within it of the line, and
                    // with its corner open toward the hole, so that a bridge can reach it.
                    if (along <= 0.0 || along >= nearest || Math.Abs(px * ey - py * ex) / length > pointEpsilon || !Opens(loop, i, origin))
                    {
                        continue;
                    }

                    nearest = along;
                    nearer = i;
                }

                if (nearer < 0)
                {
                    return target;
                }

                target = nearer;
            }
        }

        #endregion

        #region Clipping

        /// <summary>
        /// Reduces a simple loop to triangles by repeatedly cutting off ears.
        /// </summary>
        /// <remarks>
        /// An ear is a convex corner whose triangle holds no other vertex of the loop, so cutting it off
        /// removes material that belongs to the face and leaves a smaller loop of the same shape. Only
        /// reflex vertices are worth testing against: a convex one cannot sit inside an ear without a
        /// reflex one being there too.
        /// <para>
        /// A vertex on the edge of an ear blocks it as surely as one inside: the cut would run through the
        /// boundary there. Testing strictly let an I lose its flanges first and then take, as an ear, a
        /// triangle across its web and the gap beside it, with a corner of the web on its edge; the loop
        /// stuck after that and the face fell back to the fan. What does not block an ear is a vertex
        /// standing on one of its own corners — the doubled ends of a hole's bridge — or no ear next to a
        /// bridge would ever be accepted.
        /// </para>
        /// <para>
        /// On an edge means within <paramref name="near"/> of it, measured from that edge, while another ear can be found:
        /// the point tolerance, or wider when loops out of flat are clipped again because a triangle stood up. An ear
        /// passing a corner closer than the tolerance would leave a triangle thinner than it, which on loops out of flat,
        /// seen from a frame of their own, can stand up across the loop. When none can be found, within a
        /// millionth of the tolerance, the reach of rounding: a corner that close outside an ear is passed by, not cut
        /// through. Read as the point tolerance times the width of the face, as an area, and with nothing to fall back
        /// to, a corner a tenth of a millimetre outside an edge 100 mm long blocked it on a plate a metre across, and
        /// along a rib a millimetre wide between two holes every corner across the rib blocked every ear: none was
        /// clipped, and the plate fell back to the fan of its outline, laid across every hole. So it went with a corner
        /// on the line of an ear with no width, three corners a hair out of a row: read by area, it lay on all three
        /// edges of every such ear along a needle of a hole.
        /// </para>
        /// </remarks>
        private static bool TryClip(List<Node> loop, Tolerance tolerance, double near, bool surface, out GeoTriangle3[] triangles)
        {
            triangles = null;

            List<GeoTriangle3> result = new List<GeoTriangle3>(Math.Max(1, loop.Count - 2));

            // What is left once no ear can be found is measured as an area: a length tolerance across the width of the
            // face. Deriving it from the face rather than fixing it keeps the test behaving the same on a model in
            // millimetres and one in metres.
            double areaEpsilon = tolerance.EqualPoint * Extent(loop);

            // The loop is kept as links from each corner to the ones either side, and its reflex corners as a list of their
            // own. Only a reflex corner can block an ear, so an ear is looked at against the reflex corners alone, not all
            // of them: a convex loop has none, and each ear costs what it costs to find. Taking a corner out changes how
            // the two beside it turn and no other, so those two are looked at again and the list is kept right.
            int size = loop.Count;
            Node[] nodes = loop.ToArray();
            int[] before = new int[size];
            int[] after = new int[size];
            int[] reflexAt = new int[size];
            int[] reflex = new int[size];
            int reflexCount = 0;

            for (int i = 0; i < size; i++)
            {
                before[i] = (i - 1 + size) % size;
                after[i] = (i + 1) % size;
            }

            for (int i = 0; i < size; i++)
            {
                reflexAt[i] = -1;

                if (!(Cross(nodes[before[i]], nodes[i], nodes[after[i]]) > 0.0))
                {
                    reflexAt[i] = reflexCount;
                    reflex[reflexCount++] = i;
                }
            }

            int first = 0;
            int count = size;

            // How near an ear's edge a corner blocks it: within near while another ear can be found, within rounding when
            // none can.
            double rounding = tolerance.EqualPoint * 1E-6;
            double blocking = near;

            int guard = count;

            while (count > 3)
            {
                bool clipped = false;

                // The corners taken in the order of the loop, from the first still in it.
                for (int i = first, seen = 0; seen < count; i = after[i], seen++)
                {
                    int previousIndex = before[i];
                    int nextIndex = after[i];

                    Node previous = nodes[previousIndex];
                    Node current = nodes[i];
                    Node next = nodes[nextIndex];

                    if (Cross(previous, current, next) <= 0.0)
                    {
                        continue;
                    }

                    if (!IsEar(nodes, reflex, reflexCount, previousIndex, i, nextIndex, blocking, tolerance.EqualPoint))
                    {
                        continue;
                    }

                    Emit(result, previous, current, next, tolerance, surface);

                    after[previousIndex] = nextIndex;
                    before[nextIndex] = previousIndex;
                    count--;

                    if (i == first)
                    {
                        first = nextIndex;
                    }

                    // The corner leaves, and the two beside it are read again.
                    SetReflex(reflex, reflexAt, ref reflexCount, i, false);
                    SetReflex(reflex, reflexAt, ref reflexCount, previousIndex, !(Cross(nodes[before[previousIndex]], previous, next) > 0.0));
                    SetReflex(reflex, reflexAt, ref reflexCount, nextIndex, !(Cross(previous, next, nodes[after[nextIndex]]) > 0.0));

                    clipped = true;
                    guard = count;
                    blocking = near;
                    break;
                }

                if (!clipped && blocking > rounding)
                {
                    blocking = rounding;
                    continue;
                }

                if (!clipped)
                {
                    // What is left can be no area at all: clipping takes the first ear round the loop, so a
                    // face with a row of points along one straight edge ends with that row and nothing across
                    // from it, and no corner of it turns. Everything the face covers is already in triangles.
                    // A face whose boundary merged with its neighbours carries such rows wherever a neighbour
                    // had a corner, and giving up on one fell back to the fan, which covers the holes over.
                    var left = new List<Node>(count);

                    for (int i = first, seen = 0; seen < count; i = after[i], seen++)
                    {
                        left.Add(nodes[i]);
                    }

                    if (Math.Abs(SignedArea(left)) * 0.5 <= areaEpsilon)
                    {
                        break;
                    }

                    // Otherwise a full pass with no ear found means the loop is not simple — a boundary
                    // crossing itself, or a hole reaching outside the face. There is no triangulation to give.
                    return false;
                }

                if (--guard < 0)
                {
                    return false;
                }
            }

            if (count == 3)
            {
                int second = after[first];
                Emit(result, nodes[first], nodes[second], nodes[after[second]], tolerance, surface);
            }

            triangles = result.ToArray();
            return triangles.Length > 0;
        }

        /// <summary>
        /// Puts a corner in the list of the reflex corners or takes it out, as it now is.
        /// </summary>
        private static void SetReflex(int[] reflex, int[] reflexAt, ref int reflexCount, int corner, bool isReflex)
        {
            if (isReflex && reflexAt[corner] < 0)
            {
                reflexAt[corner] = reflexCount;
                reflex[reflexCount++] = corner;
            }
            else if (!isReflex && reflexAt[corner] >= 0)
            {
                // The last in the list takes its place.
                int last = reflex[--reflexCount];
                reflex[reflexAt[corner]] = last;
                reflexAt[last] = reflexAt[corner];
                reflexAt[corner] = -1;
            }
        }

        /// <summary>
        /// Gets the larger side of the bounding rectangle of a loop, used to scale the area tolerance.
        /// </summary>
        private static double Extent(List<Node> loop)
        {
            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity;
            double maxY = double.NegativeInfinity;

            foreach (Node node in loop)
            {
                if (node.X < minX) { minX = node.X; }
                if (node.X > maxX) { maxX = node.X; }
                if (node.Y < minY) { minY = node.Y; }
                if (node.Y > maxY) { maxY = node.Y; }
            }

            return Math.Max(maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Checks whether a convex corner is an ear, that is whether its triangle is empty, its edges included.
        /// </summary>
        private static bool IsEar(Node[] nodes, int[] reflex, int reflexCount, int previousIndex, int index, int nextIndex, double blocking, double pointEpsilon)
        {
            Node a = nodes[previousIndex];
            Node b = nodes[index];
            Node c = nodes[nextIndex];

            // How far outside each side of the ear a corner still counts as on it; the same for every corner asked.
            double outsideAB = -blocking * Length(a, b);
            double outsideBC = -blocking * Length(b, c);
            double outsideCA = -blocking * Length(c, a);

            for (int r = 0; r < reflexCount; r++)
            {
                int i = reflex[r];

                if (i == previousIndex || i == index || i == nextIndex)
                {
                    continue;
                }

                Node current = nodes[i];

                // A vertex standing on a corner of the ear, as the doubled ends of a hole's bridge do, leaves it be.
                if (SamePlace(current, a, pointEpsilon) || SamePlace(current, b, pointEpsilon) || SamePlace(current, c, pointEpsilon))
                {
                    continue;
                }

                // Inside the triangle or within that of its sides.
                if (Cross(a, b, current) >= outsideAB && Cross(b, c, current) >= outsideBC && Cross(c, a, current) >= outsideCA)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a triangle to the result unless it has collapsed to nothing.
        /// </summary>
        /// <remarks>
        /// A loop carrying a hole bridge has doubled vertices on it, and reducing one can leave a corner
        /// with no area. Such a triangle describes no material and is dropped rather than handed on, since
        /// anything reading the mesh would only have to guard against it again.
        /// <para>
        /// A mesh of a surface is held to a stricter meaning of nothing: see <see cref="IsCollapsed"/>.
        /// </para>
        /// </remarks>
        private static void Emit(List<GeoTriangle3> result, Node a, Node b, Node c, Tolerance tolerance, bool surface)
        {
            GeoTriangle3 triangle = new GeoTriangle3(a.Source, b.Source, c.Source);

            if (surface ? !IsCollapsed(triangle, tolerance) : !triangle.IsDegenerateByArea(tolerance))
            {
                result.Add(triangle);
            }
        }

        /// <summary>
        /// Checks whether a triangle of a surface mesh describes no material: its corners lie on a line, as far as rounding
        /// can tell.
        /// </summary>
        /// <remarks>
        /// The other meshes read twice the area, a length squared, against a length (see
        /// <see cref="GeoTriangle3.IsDegenerateByArea(Tolerance)"/>). A
        /// triangle 0.002 by 0.002 has twice the area 4E-6, under the 1E-5 of the default vector tolerance, so it was
        /// called nothing, and a wall that thin is real material: a box with a void 0.002 from its corner has the corner
        /// 0.002 thick left on every side, and the faces of 4E-6 square units there were dropped and left the mesh open
        /// along 15 edges. Here a triangle is nothing when its narrowest width is within a millionth of the point
        /// tolerance, so a sliver 100 long and 0.0008 wide is kept and the doubled corners of a hole's bridge are not.
        /// </remarks>
        internal static bool IsCollapsed(GeoTriangle3 triangle, Tolerance tolerance)
        {
            double longest = Math.Max(triangle.A.GetDistanceSquaredTo(triangle.B), Math.Max(triangle.B.GetDistanceSquaredTo(triangle.C), triangle.C.GetDistanceSquaredTo(triangle.A)));
            double width = tolerance.EqualPoint * 1E-6;

            return triangle.GetAreaVector().LengthSquared <= width * width * longest;
        }

        #endregion

        #region Predicates

        /// <summary>
        /// Gets twice the signed area of the corner, positive when it turns counter-clockwise.
        /// </summary>
        private static double Cross(Node a, Node b, Node c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        /// <summary>
        /// Gets the distance between two nodes.
        /// </summary>
        private static double Length(Node a, Node b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Checks whether two nodes stand within a distance of each other.
        /// </summary>
        private static bool SamePlace(Node a, Node b, double pointEpsilon)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy <= pointEpsilon * pointEpsilon;
        }

        /// <summary>
        /// Checks whether a point lies inside a triangle of either winding, edges counted as inside.
        /// </summary>
        private static bool InTriangle(Node a, Node b, Node c, Node point)
        {
            double d1 = Cross(a, b, point);
            double d2 = Cross(b, c, point);
            double d3 = Cross(c, a, point);

            bool anyNegative = d1 < 0.0 || d2 < 0.0 || d3 < 0.0;
            bool anyPositive = d1 > 0.0 || d2 > 0.0 || d3 > 0.0;

            return !(anyNegative && anyPositive);
        }

        #endregion
    }
}
