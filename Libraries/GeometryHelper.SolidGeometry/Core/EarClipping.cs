using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
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
    /// </summary>
    internal static class EarClipping
    {
        /// <summary>
        /// One vertex of the working loop: where it sits in the plane, and the point it came from.
        /// </summary>
        private struct Node
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
        /// error, since a fan is still the right answer for the signed sums.
        /// </returns>
        public static bool TryTriangulate(GeoFace3 face, Tolerance tolerance, out GeoTriangle3[] triangles)
        {
            triangles = null;

            if (face == null)
            {
                return false;
            }

            GeoCoordinateSystem3 frame = new GeoCoordinateSystem3(face.GetPlane());

            List<Node> outer = Project(face.Boundary.Vertices, frame);

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

            List<List<Node>> holes = new List<List<Node>>();

            foreach (GeoPolygon3 hole in face.Holes)
            {
                List<Node> ring = Project(hole.Vertices, frame);

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

                holes.Add(ring);
            }

            if (holes.Count > 0 && !TryBridgeHoles(outer, holes, out outer))
            {
                return false;
            }

            return TryClip(outer, tolerance, out triangles);
        }

        /// <summary>
        /// Maps a ring into the 2D frame of the face, keeping each original point alongside.
        /// </summary>
        private static List<Node> Project(IReadOnlyList<GeoPoint3> vertices, GeoCoordinateSystem3 frame)
        {
            List<Node> nodes = new List<Node>(vertices.Count);

            foreach (GeoPoint3 vertex in vertices)
            {
                GeoPoint3 local = frame.ToLocal(vertex);

                // The local Z is dropped rather than checked. Coplanarity was settled when the polygon and
                // the face were built, and what little is left of it is the deviation those constructors
                // already accepted.
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
        private static bool TryBridgeHoles(List<Node> outer, List<List<Node>> holes, out List<Node> merged)
        {
            merged = outer;

            holes.Sort((left, right) => MaxX(right).CompareTo(MaxX(left)));

            foreach (List<Node> hole in holes)
            {
                if (!TryBridgeHole(merged, hole, out merged))
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
        private static bool TryBridgeHole(List<Node> outer, List<Node> hole, out List<Node> merged)
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
        /// Vertices are tested for containment strictly, which is what lets the doubled vertices of a
        /// hole bridge sit on the edge of an ear without blocking it. Without that no ear next to a
        /// bridge would ever be accepted.
        /// </para>
        /// </remarks>
        private static bool TryClip(List<Node> loop, Tolerance tolerance, out GeoTriangle3[] triangles)
        {
            triangles = null;

            List<GeoTriangle3> result = new List<GeoTriangle3>(Math.Max(1, loop.Count - 2));
            List<Node> working = new List<Node>(loop);

            // The containment test is a cross product, so its natural scale is an area: a length tolerance
            // across the width of the face. Deriving it from the face rather than fixing it keeps the test
            // behaving the same on a model in millimetres and one in metres.
            double areaEpsilon = tolerance.EqualPoint * Extent(working);

            int guard = working.Count;

            while (working.Count > 3)
            {
                bool clipped = false;

                for (int i = 0; i < working.Count; i++)
                {
                    int previousIndex = (i - 1 + working.Count) % working.Count;
                    int nextIndex = (i + 1) % working.Count;

                    Node previous = working[previousIndex];
                    Node current = working[i];
                    Node next = working[nextIndex];

                    if (Cross(previous, current, next) <= 0.0)
                    {
                        continue;
                    }

                    if (!IsEar(working, previousIndex, i, nextIndex, areaEpsilon))
                    {
                        continue;
                    }

                    Emit(result, previous, current, next, tolerance);
                    working.RemoveAt(i);

                    clipped = true;
                    guard = working.Count;
                    break;
                }

                if (!clipped)
                {
                    // A full pass with no ear found means the loop is not simple — a boundary crossing
                    // itself, or a hole reaching outside the face. There is no triangulation to give.
                    return false;
                }

                if (--guard < 0)
                {
                    return false;
                }
            }

            Emit(result, working[0], working[1], working[2], tolerance);

            triangles = result.ToArray();
            return triangles.Length > 0;
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
        /// Checks whether a convex corner is an ear, that is whether its triangle is empty.
        /// </summary>
        private static bool IsEar(List<Node> loop, int previousIndex, int index, int nextIndex, double areaEpsilon)
        {
            Node a = loop[previousIndex];
            Node b = loop[index];
            Node c = loop[nextIndex];

            for (int i = 0; i < loop.Count; i++)
            {
                if (i == previousIndex || i == index || i == nextIndex)
                {
                    continue;
                }

                Node previous = loop[(i - 1 + loop.Count) % loop.Count];
                Node current = loop[i];
                Node next = loop[(i + 1) % loop.Count];

                if (Cross(previous, current, next) > 0.0)
                {
                    continue;
                }

                if (InTriangle(a, b, c, current, areaEpsilon))
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
        /// </remarks>
        private static void Emit(List<GeoTriangle3> result, Node a, Node b, Node c, Tolerance tolerance)
        {
            GeoTriangle3 triangle = new GeoTriangle3(a.Source, b.Source, c.Source);

            if (!triangle.IsDegenerate(tolerance))
            {
                result.Add(triangle);
            }
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
        /// Checks whether a point lies strictly inside a counter-clockwise triangle.
        /// </summary>
        private static bool InTriangle(Node a, Node b, Node c, Node point, double areaEpsilon)
        {
            return Cross(a, b, point) > areaEpsilon
                && Cross(b, c, point) > areaEpsilon
                && Cross(c, a, point) > areaEpsilon;
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
