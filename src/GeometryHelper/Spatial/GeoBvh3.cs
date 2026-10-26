using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Spatial
{
    /// <summary>
    /// A bounding volume hierarchy over a triangle mesh: a tree of nested axis-aligned boxes that makes
    /// queries against the mesh cost time in the logarithm of its size rather than in its size.
    /// <para>
    /// Every operation on a mesh without an index has to look at every triangle, because nothing rules any
    /// of them out in advance. A tree of boxes rules them out wholesale: a ray that misses a box misses
    /// everything inside it, and a box already farther away than the best answer so far cannot hold
    /// anything nearer. One traversal replaces the scan.
    /// </para>
    /// <para>
    /// Building the tree costs a sort of the triangles, so it pays for itself over repeated queries rather
    /// than on the first one. Build it once and keep it for as long as the mesh does not change — which,
    /// since every geometry type here is immutable, is for as long as the mesh exists.
    /// </para>
    /// </summary>
    public sealed class GeoBvh3
    {
        /// <summary>
        /// How many triangles a node may hold before it is worth splitting.
        /// </summary>
        /// <remarks>
        /// Below a handful, walking the triangles directly beats the bookkeeping of another two nodes.
        /// </remarks>
        private const int LeafSize = 4;

        private readonly GeoTriangle3[] _triangles;
        private readonly GeoAabb3[] _triangleBounds;
        private readonly int[] _order;
        private readonly Node[] _nodes;
        private readonly int _rootCount;

        /// <summary>
        /// One node of the tree: a box, the run of triangles under it, and its two children.
        /// </summary>
        private struct Node
        {
            public GeoAabb3 Bounds;
            public int Start;
            public int Count;
            public int Left;
            public int Right;
        }

        /// <summary>
        /// Initializes a hierarchy over a set of triangles.
        /// </summary>
        /// <param name="triangles">The mesh; it is copied, so later changes to the source do not affect the tree.</param>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        public GeoBvh3(IEnumerable<GeoTriangle3> triangles)
        {
            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            List<GeoTriangle3> kept = new List<GeoTriangle3>(triangles);

            _triangles = kept.ToArray();
            _triangleBounds = new GeoAabb3[_triangles.Length];
            _order = new int[_triangles.Length];

            for (int i = 0; i < _triangles.Length; i++)
            {
                _triangleBounds[i] = _triangles[i].GetAabb();
                _order[i] = i;
            }

            List<Node> nodes = new List<Node>();

            if (_triangles.Length > 0)
            {
                Build(nodes, 0, _triangles.Length);
            }

            _nodes = nodes.ToArray();
            _rootCount = _nodes.Length;
        }

        /// <summary>
        /// Builds a hierarchy over the surface of a solid, using the default tolerance.
        /// </summary>
        public static GeoBvh3 FromSolid(GeoSolid3 solid) => FromSolid(solid, Tolerance.Global);

        /// <summary>
        /// Builds a hierarchy over the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The body whose surface is indexed.</param>
        /// <param name="tolerance">The tolerance the surface is meshed with.</param>
        /// <remarks>
        /// The index is built over <see cref="GeoSolid3.Triangulate(Tolerance)"/>, so it follows the
        /// material of every face: a ray fired through the notch of a concave body or through a hole in a
        /// plate passes clean through, and the nearest point on the surface is a point that is really on
        /// it.
        /// <para>
        /// A tree is worth building once and keeping. Nothing about it depends on what is asked of it, so
        /// a body that is queried repeatedly — clash detection over a model — should be indexed once
        /// rather than through the convenience paths in <c>Collision3</c> and <c>Distance3</c>, which
        /// build a tree per call.
        /// </para>
        /// </remarks>
        public static GeoBvh3 FromSolid(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return new GeoBvh3(solid.TriangulateSurface(tolerance));
        }

        /// <summary>
        /// Gets the triangles the tree was built over, in the order they were given.
        /// </summary>
        public IReadOnlyList<GeoTriangle3> Triangles => _triangles;

        /// <summary>
        /// Gets how many triangles the tree holds.
        /// </summary>
        public int TriangleCount => _triangles.Length;

        /// <summary>
        /// Gets the box enclosing every triangle, or the empty box when the tree holds none.
        /// </summary>
        public GeoAabb3 Bounds => _rootCount == 0 ? GeoAabb3.Empty : _nodes[0].Bounds;

        #region Building

        /// <summary>
        /// Builds one node over a run of triangles and returns its index.
        /// </summary>
        /// <remarks>
        /// The run is split at the median of the triangle centres along whichever axis those centres are
        /// most spread out on. Splitting on the widest spread keeps the two child boxes as separate as the
        /// geometry allows, which is what makes a traversal able to discard one of them; splitting at the
        /// median keeps the tree balanced, so its depth stays logarithmic even for a mesh that is dense in
        /// one place and sparse elsewhere.
        /// </remarks>
        private int Build(List<Node> nodes, int start, int count)
        {
            int self = nodes.Count;
            nodes.Add(default);

            GeoAabb3 bounds = GeoAabb3.Empty;
            for (int i = start; i < start + count; i++)
            {
                bounds = bounds.Union(_triangleBounds[_order[i]]);
            }

            Node node = new Node
            {
                Bounds = bounds,
                Start = start,
                Count = count,
                Left = -1,
                Right = -1
            };

            if (count > LeafSize)
            {
                GeoAabb3 centreBounds = GeoAabb3.Empty;
                for (int i = start; i < start + count; i++)
                {
                    centreBounds = centreBounds.Union(_triangleBounds[_order[i]].Center);
                }

                int axis = WidestAxis(centreBounds);
                int middle = start + count / 2;

                // Every triangle before the middle has a centre no farther along the axis than every
                // triangle after it. A full sort would give the same split for more work.
                PartialSort(start, count, middle, axis);

                node.Left = Build(nodes, start, middle - start);
                node.Right = Build(nodes, middle, start + count - middle);
            }

            nodes[self] = node;
            return self;
        }

        /// <summary>
        /// Gets the axis a box is most spread out along.
        /// </summary>
        private static int WidestAxis(GeoAabb3 bounds)
        {
            if (bounds.SizeX >= bounds.SizeY && bounds.SizeX >= bounds.SizeZ)
            {
                return 0;
            }

            return bounds.SizeY >= bounds.SizeZ ? 1 : 2;
        }

        /// <summary>
        /// Rearranges a run so the entry at a chosen position is the one that would be there after sorting,
        /// with everything smaller before it and everything larger after.
        /// </summary>
        /// <remarks>
        /// This is quickselect. Only the position of the median matters for the split, so paying for a
        /// full sort at every node would be work thrown away.
        /// </remarks>
        private void PartialSort(int start, int count, int wanted, int axis)
        {
            int low = start;
            int high = start + count - 1;

            while (low < high)
            {
                double pivot = Component(_triangleBounds[_order[(low + high) / 2]].Center, axis);
                int i = low;
                int j = high;

                while (i <= j)
                {
                    while (Component(_triangleBounds[_order[i]].Center, axis) < pivot)
                    {
                        i++;
                    }

                    while (Component(_triangleBounds[_order[j]].Center, axis) > pivot)
                    {
                        j--;
                    }

                    if (i <= j)
                    {
                        int swap = _order[i];
                        _order[i] = _order[j];
                        _order[j] = swap;
                        i++;
                        j--;
                    }
                }

                if (wanted <= j)
                {
                    high = j;
                }
                else if (wanted >= i)
                {
                    low = i;
                }
                else
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Reads one coordinate of a point by axis number.
        /// </summary>
        private static double Component(GeoPoint3 point, int axis)
        {
            switch (axis)
            {
                case 0: return point.X;
                case 1: return point.Y;
                default: return point.Z;
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Finds every point where a ray crosses the mesh, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Finds every point where a ray crosses the mesh, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The results come back in no particular order. A ray passing exactly along an edge shared by two
        /// triangles is reported by both of them, so a caller counting crossings should aim the ray away
        /// from edges rather than trust the count. Each triangle is crossed by the ray's own crossing of its plane where the
        /// triangle holds it, not as <see cref="Intersection3.TryIntersectWith(GeoRay3, GeoTriangle3, out GeoPoint3, Tolerance)"/>
        /// reads the ray, as the line without ends along it:
        /// a ray starting within the planar tolerance of a triangle's plane and dipping into it, however shallowly,
        /// crosses where it reaches the plane, one rising from there touches at its origin, and one level with it to
        /// rounding names no place on it. The ray from (5, 5, 0.0004) along (1, 0, -0.01) crosses a mesh whose top lies
        /// in z = 0 at (5.04, 5, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
        {
            List<GeoPoint3> hits = new List<GeoPoint3>();

            if (_rootCount == 0)
            {
                return hits.ToArray();
            }

            // A crossing is named within the point tolerance of its triangle across it and the planar tolerance off its
            // plane, so a branch whose box, grown by the larger of the two, the ray never enters holds none.
            double grow = Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);
            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                if (!RayEntersBox(ray, node.Bounds, grow))
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        if (Intersection3.TryCrossTriangle(ray, _triangles[_order[i]], out GeoPoint3 hit, tolerance, false))
                        {
                            hits.Add(hit);
                        }
                    }

                    continue;
                }

                pending.Push(node.Left);
                pending.Push(node.Right);
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Finds every point where an infinite line crosses the mesh, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Finds every point where an infinite line crosses the mesh, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line runs both ways from its origin, so the answer is the same wherever along it the origin stands, but for a
        /// crossing within rounding of the tolerance: it is what the ray gives from before the mesh, and from behind it,
        /// together. The results come back in no
        /// particular order, and a line through an edge two triangles share is reported by both of them, as a ray's is.
        /// Each triangle is crossed as <see cref="GeoXLine3.TryIntersectWith(GeoTriangle3, out GeoPoint3, Tolerance)"/>
        /// crosses it, so the answer is the plain scan's over every triangle, without looking at the branches whose box,
        /// grown by the larger of the point and planar tolerances, the line never enters. The line through (3, 6, 4)
        /// along Z crosses a mesh of the box 10 by 10 by 10 from the origin at (3, 6, 0) and (3, 6, 10), the ray from the
        /// same point only at the second.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance)
        {
            List<GeoPoint3> hits = new List<GeoPoint3>();

            if (_rootCount == 0)
            {
                return hits.ToArray();
            }

            double grow = Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);
            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                if (!LineEntersBox(xline.Origin, xline.Direction, node.Bounds, grow, double.NegativeInfinity))
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        if (Intersection3.TryIntersectWith(xline, _triangles[_order[i]], out GeoPoint3 hit, tolerance))
                        {
                            hits.Add(hit);
                        }
                    }

                    continue;
                }

                pending.Push(node.Left);
                pending.Push(node.Right);
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Counts where a ray crosses the mesh, unless a crossing lands so near the rim of its triangle that the
        /// count cannot be trusted.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance the crossings are found within.</param>
        /// <param name="rim">How near the rim of its triangle a crossing may not land.</param>
        /// <param name="crossings">The number of crossings when the method returns true; nought otherwise.</param>
        /// <returns>false when a crossing lands within <paramref name="rim"/> of the rim of its triangle.</returns>
        /// <remarks>
        /// A triangle holds a point up to the point tolerance beyond its rim. A ray through an edge two triangles
        /// share is counted by both, and one leaving a body past an edge, at a shallow angle to the face beyond, is
        /// counted by that face as well a little further along: 0.19 further, on a block a ray left 0.2 degrees off
        /// its next face. Neither can be told from two real crossings by where they land on the ray; they are told
        /// by where they land on their triangles.
        /// </remarks>
        internal bool TryCountCrossings(GeoRay3 ray, Tolerance tolerance, double rim, out int crossings)
        {
            crossings = 0;

            if (_rootCount == 0)
            {
                return true;
            }

            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                if (!RayHitsBox(ray, node.Bounds, tolerance))
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        GeoTriangle3 triangle = _triangles[_order[i]];

                        if (!Intersection3.TryCrossTriangle(ray, triangle, out GeoPoint3 hit, tolerance, true))
                        {
                            continue;
                        }

                        if (new GeoLine3(triangle.A, triangle.B).DistanceTo(hit) <= rim ||
                            new GeoLine3(triangle.B, triangle.C).DistanceTo(hit) <= rim ||
                            new GeoLine3(triangle.C, triangle.A).DistanceTo(hit) <= rim)
                        {
                            crossings = 0;
                            return false;
                        }

                        crossings++;
                    }

                    continue;
                }

                pending.Push(node.Left);
                pending.Push(node.Right);
            }

            return true;
        }

        /// <summary>
        /// Gets the point of the mesh closest to a target point, using the default tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPoint(GeoPoint3 point) => GetClosestPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the mesh closest to a target point, within a tolerance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the tree holds no triangles.</exception>
        public GeoPoint3 GetClosestPoint(GeoPoint3 point, Tolerance tolerance)
        {
            if (_rootCount == 0)
            {
                throw new InvalidOperationException("An empty hierarchy has no point to return.");
            }

            GeoPoint3 best = _triangles[0].A;
            double bestDistance = double.MaxValue;

            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                // Nothing under a box farther away than the best answer so far can beat it.
                if (node.Bounds.DistanceTo(point) >= bestDistance)
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        GeoPoint3 candidate = Projection3.ProjectToTriangle(_triangles[_order[i]], point);
                        double distance = candidate.DistanceTo(point);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = candidate;
                        }
                    }

                    continue;
                }

                // Descending into the nearer child first tightens the best answer sooner, which lets the
                // other child be discarded more often.
                int near = node.Left;
                int far = node.Right;

                if (_nodes[far].Bounds.DistanceTo(point) < _nodes[near].Bounds.DistanceTo(point))
                {
                    int swap = near;
                    near = far;
                    far = swap;
                }

                pending.Push(far);
                pending.Push(near);
            }

            return best;
        }

        /// <summary>
        /// Gets the triangles that may come within a reach of a segment, in the order the tree holds them: every one that
        /// does, and of the others those no direction tried shows to stand further off.
        /// </summary>
        /// <remarks>
        /// A branch or a triangle is passed over where the segment itself misses its box grown by the reach, not only where the
        /// box of the segment stands further than the reach: a bar running at a slant past a cylinder of radius 500 and 10 000
        /// sides has a box holding thousands of its triangles, of which a handful lie within the reach of its line. A box is
        /// also passed over where the segment stands further than the reach from it across a direction square to an axis and
        /// to the segment, and a triangle where it does across the triangle's normal, or across a direction square to the
        /// segment or to the normal and to a side: a bar standing up half a thousandth outside a side of that cylinder, through
        /// the rim of both caps, lies in the boxes of some 2 500 of the slivers the caps are cut into, and comes within a
        /// thousandth of 4 triangles, which are what it is given. No point of the segment comes nearer a point of the box or
        /// the triangle than the two stand apart across any direction, so the reach, read a rounding wide, a trillionth of the
        /// largest coordinate of the segment and of the box or the triangle, passes over no triangle the segment comes within
        /// the reach of.
        /// </remarks>
        internal List<GeoTriangle3> TrianglesNear(GeoPoint3 from, GeoPoint3 to, double reach)
        {
            var near = new List<GeoTriangle3>();

            if (_rootCount == 0)
            {
                return near;
            }

            double size = Math.Max(Largest(from), Largest(to));
            double within = reach + 1E-12 * (1.0 + size);
            GeoVector3 step = from.GetVectorTo(to);
            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                if (Apart(from, to, step, node.Bounds, reach, within, size))
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        int t = _order[i];
                        GeoAabb3 bounds = _triangleBounds[t];

                        if (!Apart(from, to, step, bounds, reach, within, size) && !Apart(from, step, _triangles[t], Wider(reach, size, bounds)))
                        {
                            near.Add(_triangles[t]);
                        }
                    }

                    continue;
                }

                pending.Push(node.Right);
                pending.Push(node.Left);
            }

            return near;
        }

        /// <summary>
        /// Gets the largest coordinate of a point, read without its sign.
        /// </summary>
        private static double Largest(GeoPoint3 point) => Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z)));

        /// <summary>
        /// Gets a reach read a rounding wide for a distance taken across a direction from a segment to a box, or to a triangle in
        /// the box: a trillionth of the largest coordinate of either, since the rounding of where each stands across it grows
        /// with how far out its corners lie. A segment near the origin and a triangle of a mesh a million across are read
        /// a millionth wide, where the segment alone gave a trillionth.
        /// </summary>
        /// <param name="reach">The reach.</param>
        /// <param name="size">The largest coordinate of the segment.</param>
        /// <param name="box">The box, or the box of the triangle.</param>
        private static double Wider(double reach, double size, GeoAabb3 box)
            => reach + 1E-12 * (1.0 + Math.Max(size, Math.Max(Largest(box.Min), Largest(box.Max))));

        /// <summary>
        /// Determines whether a segment stands further from a box than a reach: its own box further than the reach, as before,
        /// or the segment itself missing the box grown on every side by the reach read a rounding wide, which holds every point
        /// within the reach of the box, as before, or the two further apart than the reach read wider by the box's coordinates
        /// across a direction square to an axis and the segment.
        /// </summary>
        private static bool Apart(GeoPoint3 from, GeoPoint3 to, GeoVector3 step, GeoAabb3 box, double reach, double within, double size)
            => Gap3.BoxGap(from, to, box) > reach || !LineEntersBox(from, step, box, within, 0.0, 1.0) || ApartAcross(from, step, box, Wider(reach, size, box));

        /// <summary>
        /// Determines whether a segment and a box stand further apart than a distance across one of the three directions square
        /// to an axis and to the segment.
        /// </summary>
        /// <remarks>
        /// Across such a direction the segment is a single place and the box a stretch, so a segment passing a box at a slant,
        /// beside one of its edges, is told by how far it passes the edge rather than by how far it passes the box grown into
        /// a larger box: the segment from (0, -10, 0) to (10, 0, 0) passes 6.36 from the box 1 by 1 by 1 at the origin, and
        /// through that box grown by 5.5 on every side.
        /// </remarks>
        private static bool ApartAcross(GeoPoint3 from, GeoVector3 step, GeoAabb3 box, double within)
        {
            double x = (box.Min.X + box.Max.X) * 0.5 - from.X;
            double y = (box.Min.Y + box.Max.Y) * 0.5 - from.Y;
            double z = (box.Min.Z + box.Max.Z) * 0.5 - from.Z;
            double halfX = (box.Max.X - box.Min.X) * 0.5;
            double halfY = (box.Max.Y - box.Min.Y) * 0.5;
            double halfZ = (box.Max.Z - box.Min.Z) * 0.5;

            // Square to X and to the segment is (0, -step.Z, step.Y), and so on round the axes.
            return Beyond(z * step.Y - y * step.Z, halfY * Math.Abs(step.Z) + halfZ * Math.Abs(step.Y), step.Y * step.Y + step.Z * step.Z, within)
                || Beyond(x * step.Z - z * step.X, halfZ * Math.Abs(step.X) + halfX * Math.Abs(step.Z), step.Z * step.Z + step.X * step.X, within)
                || Beyond(y * step.X - x * step.Y, halfX * Math.Abs(step.Y) + halfY * Math.Abs(step.X), step.X * step.X + step.Y * step.Y, within);
        }

        /// <summary>
        /// Determines whether a segment stands further from a triangle than a distance across the triangle's normal, or across
        /// a direction square to one of its sides and to the segment or to the normal.
        /// </summary>
        private static bool Apart(GeoPoint3 from, GeoVector3 step, GeoTriangle3 triangle, double within)
        {
            GeoVector3 a = from.GetVectorTo(triangle.A);
            GeoVector3 b = from.GetVectorTo(triangle.B);
            GeoVector3 c = from.GetVectorTo(triangle.C);
            GeoVector3 ab = triangle.A.GetVectorTo(triangle.B);
            GeoVector3 bc = triangle.B.GetVectorTo(triangle.C);
            GeoVector3 ca = triangle.C.GetVectorTo(triangle.A);
            GeoVector3 normal = ab.CrossProduct(bc);

            return Apart(normal, step, a, b, c, within)
                || Apart(ab.CrossProduct(step), step, a, b, c, within)
                || Apart(bc.CrossProduct(step), step, a, b, c, within)
                || Apart(ca.CrossProduct(step), step, a, b, c, within)
                || Apart(normal.CrossProduct(ab), step, a, b, c, within)
                || Apart(normal.CrossProduct(bc), step, a, b, c, within)
                || Apart(normal.CrossProduct(ca), step, a, b, c, within);
        }

        /// <summary>
        /// Determines whether a segment from the origin along a step and a triangle, its corners given from the start of the
        /// segment, stand further apart than a distance across a direction: any direction will do, of any length but nought.
        /// </summary>
        private static bool Apart(GeoVector3 across, GeoVector3 step, GeoVector3 a, GeoVector3 b, GeoVector3 c, double within)
        {
            double along = across.DotProduct(step);
            double atA = across.DotProduct(a);
            double atB = across.DotProduct(b);
            double atC = across.DotProduct(c);
            double low = Math.Min(atA, Math.Min(atB, atC));
            double high = Math.Max(atA, Math.Max(atB, atC));
            double gap = Math.Max(low - Math.Max(0.0, along), Math.Min(0.0, along) - high);

            return gap > 0.0 && gap * gap > within * within * across.LengthSquared;
        }

        /// <summary>
        /// Determines whether a gap across a direction is further than a distance, the gap and the direction's square length
        /// taken as they stand rather than for a direction of length one.
        /// </summary>
        private static bool Beyond(double centre, double half, double lengthSquared, double within)
        {
            double gap = Math.Abs(centre) - half;

            return gap > 0.0 && gap * gap > within * within * lengthSquared;
        }

        /// <summary>
        /// Calculates the shortest distance from the mesh to a point, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from the mesh to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => GetClosestPoint(point, tolerance).DistanceTo(point);

        /// <summary>
        /// Calculates the shortest distance between this mesh and another, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoBvh3 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between this mesh and another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Both trees are walked at once. A pair of boxes already farther apart than the closest pair of
        /// triangles found so far cannot hold a nearer pair, so the whole cross product beneath them is
        /// discarded without being looked at. That is what turns a comparison of every triangle against
        /// every other into something usable on a real mesh.
        /// <para>
        /// Two meshes that touch or pass through each other are at distance zero. An empty tree is
        /// infinitely far from everything, since it has nothing to measure to.
        /// </para>
        /// </remarks>
        public double DistanceTo(GeoBvh3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (_rootCount == 0 || other._rootCount == 0)
            {
                return double.PositiveInfinity;
            }

            return Walk(other, double.PositiveInfinity, false, tolerance, out _);
        }

        /// <summary>
        /// Gets the shortest segment from this mesh to another when it is shorter than a reach.
        /// </summary>
        /// <param name="other">The mesh the segment lands on.</param>
        /// <param name="reach">How short the segment has to be; <see cref="double.PositiveInfinity"/> for any.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="line">
        /// The shortest segment leaving a triangle of this mesh and landing on a triangle of the other, the one
        /// <see cref="Projection3.GetShortestLineTo(GeoTriangle3, GeoTriangle3, Tolerance)"/> gives for the nearest
        /// pair; <c>default</c> when the method returns false.
        /// </param>
        /// <returns>false when either mesh is empty, or no pair of triangles comes nearer than the reach.</returns>
        /// <remarks>
        /// A reach lets a caller who only cares about what is near stop early: every pair of boxes at least that
        /// far apart is passed over from the start, so two meshes farther apart than the reach cost the one test
        /// of their outer boxes.
        /// </remarks>
        internal bool TryGetShortestLineTo(GeoBvh3 other, double reach, Tolerance tolerance, out GeoLine3 line)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            line = default(GeoLine3);

            if (_rootCount == 0 || other._rootCount == 0)
            {
                return false;
            }

            return Walk(other, reach, true, tolerance, out line) < reach;
        }

        /// <summary>
        /// Gets the shortest segment from a segment to the mesh when it is shorter than a reach.
        /// </summary>
        /// <param name="segment">The segment the answer leaves from.</param>
        /// <param name="reach">How short the answer has to be; <see cref="double.PositiveInfinity"/> for any.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="line">
        /// The shortest segment leaving <paramref name="segment"/> and landing on a triangle of the mesh, the one
        /// <see cref="Projection3.GetShortestLineTo(GeoLine3, GeoTriangle3, Tolerance)"/> gives for the nearest triangle;
        /// <c>default</c> when the method returns false.
        /// </param>
        /// <returns>false when the mesh is empty, or no triangle comes nearer the segment than the reach.</returns>
        /// <remarks>
        /// The nearer child is opened first, as for a point, nearer by how far its box is from the box of the segment. A box
        /// or a triangle is passed over where it stands no nearer the box of the segment than the nearest found so far, or
        /// where the segment itself stands further off, as <see cref="TrianglesNear"/> reads it, than both that and twice
        /// the larger of the point and planar tolerances, inside which a crossing may be found and measured as of no
        /// length. What is passed over could not have come nearer, so the answer is the one measuring every triangle
        /// gives: 50 bars of radius 40 running at a slant 20 to 30 clear of a cylinder of radius 500 and 10 000 sides, in
        /// pieces 320 long, measure 13 952 triangles where they measured 169 522.
        /// </remarks>
        internal bool TryGetShortestLineTo(GeoLine3 segment, double reach, Tolerance tolerance, out GeoLine3 line)
        {
            line = default(GeoLine3);

            if (_rootCount == 0)
            {
                return false;
            }

            GeoAabb3 box = GeoAabb3.FromPoints(new[] { segment.StartPoint, segment.EndPoint });
            double nearest = reach;
            bool found = false;

            // A segment the crossing test lets through a triangle within the tolerance is measured as of no length, and it
            // comes within the larger of the point and planar tolerances of the triangle; twice that is room to spare.
            double crossing = 2.0 * Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);
            double size = Math.Max(Largest(segment.StartPoint), Largest(segment.EndPoint));
            GeoPoint3 from = segment.StartPoint;
            GeoVector3 step = from.GetVectorTo(segment.EndPoint);

            Stack<int> pending = new Stack<int>();
            pending.Push(0);

            while (pending.Count > 0)
            {
                Node node = _nodes[pending.Pop()];

                if (!(node.Bounds.DistanceTo(box) < nearest))
                {
                    continue;
                }

                // The segment itself further off than the nearest so far, and than a crossing can be found, holds nothing
                // nearer: this passes over what the box of the segment cannot, the branches beside a long slanting run.
                double within = Wider(Math.Max(nearest, crossing), size, node.Bounds);

                if (!LineEntersBox(from, step, node.Bounds, within, 0.0, 1.0) || ApartAcross(from, step, node.Bounds, within))
                {
                    continue;
                }

                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        int t = _order[i];

                        if (!(_triangleBounds[t].DistanceTo(box) < nearest) || Apart(from, step, _triangles[t], Wider(Math.Max(nearest, crossing), size, _triangleBounds[t])))
                        {
                            continue;
                        }

                        GeoLine3 candidate = Projection3.GetShortestLineTo(segment, _triangles[t], tolerance);
                        double length = candidate.Length;

                        if (length < nearest)
                        {
                            nearest = length;
                            line = candidate;
                            found = true;
                        }
                    }

                    continue;
                }

                int near = node.Left;
                int far = node.Right;

                if (_nodes[far].Bounds.DistanceTo(box) < _nodes[near].Bounds.DistanceTo(box))
                {
                    int swap = near;
                    near = far;
                    far = swap;
                }

                pending.Push(far);
                pending.Push(near);
            }

            return found;
        }

        /// <summary>
        /// Walks both trees at once, the nearest pair of boxes first, and measures the pairs of triangles under
        /// every pair of boxes that could still hold a nearer pair than the best found so far.
        /// </summary>
        /// <param name="other">The other tree; neither may be empty.</param>
        /// <param name="reach">The bound to start from: box pairs at least this far apart are never opened.</param>
        /// <param name="line">
        /// Whether to measure each pair of triangles by its shortest segment, keeping the best in
        /// <paramref name="best"/>, rather than by distance alone.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="best">The segment of the nearest pair found, when segments were asked for.</param>
        /// <returns>The distance of the nearest pair found, or <paramref name="reach"/> when none came nearer.</returns>
        /// <remarks>
        /// Of the two pairs of boxes a split gives, the nearer one is opened first. The first pairs of triangles
        /// measured are then among the nearest there are, the bound drops to near the answer at once, and every
        /// pair of boxes farther apart than it is passed over whole — where walking the children in a fixed
        /// order would measure whatever lay first and prune by a bound far from the answer. Under the leaves the
        /// box of each triangle is tested before the triangle is measured, since a leaf holds several.
        /// </remarks>
        private double Walk(GeoBvh3 other, double reach, bool line, Tolerance tolerance, out GeoLine3 best)
        {
            best = default(GeoLine3);
            double nearest = reach;

            double rootGap = _nodes[0].Bounds.DistanceTo(other._nodes[0].Bounds);

            if (!(rootGap < nearest))
            {
                return nearest;
            }

            Stack<NodePair> pending = new Stack<NodePair>();
            pending.Push(new NodePair(0, 0, rootGap));

            while (pending.Count > 0)
            {
                NodePair pair = pending.Pop();

                // The bound may have dropped since the pair was put aside.
                if (pair.Gap >= nearest)
                {
                    continue;
                }

                Node left = _nodes[pair.Left];
                Node right = other._nodes[pair.Right];

                bool leftIsLeaf = left.Left < 0;
                bool rightIsLeaf = right.Left < 0;

                if (leftIsLeaf && rightIsLeaf)
                {
                    for (int i = left.Start; i < left.Start + left.Count; i++)
                    {
                        int a = _order[i];
                        GeoAabb3 box = _triangleBounds[a];

                        for (int j = right.Start; j < right.Start + right.Count; j++)
                        {
                            int b = other._order[j];

                            if (box.DistanceTo(other._triangleBounds[b]) >= nearest)
                            {
                                continue;
                            }

                            if (line)
                            {
                                GeoLine3 candidate = Projection3.GetShortestLineTo(_triangles[a], other._triangles[b], tolerance);
                                double length = candidate.Length;

                                if (length < nearest)
                                {
                                    nearest = length;
                                    best = candidate;
                                }
                            }
                            else
                            {
                                double distance = Distance3.DistanceToWithinBand(_triangles[a], other._triangles[b], tolerance);

                                if (distance < nearest)
                                {
                                    nearest = distance;
                                }
                            }

                            if (nearest <= 0.0)
                            {
                                return 0.0;
                            }
                        }
                    }

                    continue;
                }

                // Splitting the larger side keeps the two descending at a similar rate.
                NodePair first, second;

                if (rightIsLeaf || (!leftIsLeaf && left.Count >= right.Count))
                {
                    first = new NodePair(left.Left, pair.Right, _nodes[left.Left].Bounds.DistanceTo(right.Bounds));
                    second = new NodePair(left.Right, pair.Right, _nodes[left.Right].Bounds.DistanceTo(right.Bounds));
                }
                else
                {
                    first = new NodePair(pair.Left, right.Left, left.Bounds.DistanceTo(other._nodes[right.Left].Bounds));
                    second = new NodePair(pair.Left, right.Right, left.Bounds.DistanceTo(other._nodes[right.Right].Bounds));
                }

                // The nearer pair goes on top of the stack, so that it is opened next.
                if (second.Gap < first.Gap)
                {
                    NodePair swap = first;
                    first = second;
                    second = swap;
                }

                if (second.Gap < nearest)
                {
                    pending.Push(second);
                }

                if (first.Gap < nearest)
                {
                    pending.Push(first);
                }
            }

            return nearest;
        }

        /// <summary>
        /// Checks whether two boxes come within a reach of each other along every axis.
        /// </summary>
        private static bool Near(GeoAabb3 first, GeoAabb3 second, double reach)
        {
            return first.Min.X - reach <= second.Max.X && second.Min.X - reach <= first.Max.X
                && first.Min.Y - reach <= second.Max.Y && second.Min.Y - reach <= first.Max.Y
                && first.Min.Z - reach <= second.Max.Z && second.Min.Z - reach <= first.Max.Z;
        }

        /// <summary>
        /// A node of this tree and a node of another, put aside to be opened, with how far apart their boxes are.
        /// </summary>
        private readonly struct NodePair
        {
            public NodePair(int left, int right, double gap)
            {
                Left = left;
                Right = right;
                Gap = gap;
            }

            public int Left { get; }

            public int Right { get; }

            public double Gap { get; }
        }

        /// <summary>
        /// Checks whether any triangle of this mesh touches any triangle of another, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoBvh3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether any triangle of this mesh touches any triangle of another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The two trees are walked together, and a pair of boxes that do not overlap prunes every pair of
        /// triangles beneath them at once. This answers surface contact only: one mesh sitting wholly
        /// inside another without touching it is not a collision here, which is why
        /// <c>Collision3.CollidesWith</c> tests containment separately.
        /// </remarks>
        public bool CollidesWith(GeoBvh3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (_rootCount == 0 || other._rootCount == 0)
            {
                return false;
            }

            // Two triangles the collision test calls touching are within the larger of the point and planar
            // tolerances of each other, so their boxes are too; twice that is room to spare.
            double reach = 2.0 * Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);

            Stack<int> pending = new Stack<int>();
            pending.Push(0);
            pending.Push(0);

            while (pending.Count > 0)
            {
                int rightIndex = pending.Pop();
                int leftIndex = pending.Pop();

                Node left = _nodes[leftIndex];
                Node right = other._nodes[rightIndex];

                if (!left.Bounds.CollidesWith(right.Bounds, tolerance))
                {
                    continue;
                }

                bool leftIsLeaf = left.Left < 0;
                bool rightIsLeaf = right.Left < 0;

                if (leftIsLeaf && rightIsLeaf)
                {
                    for (int i = left.Start; i < left.Start + left.Count; i++)
                    {
                        int a = _order[i];

                        for (int j = right.Start; j < right.Start + right.Count; j++)
                        {
                            int b = other._order[j];

                            // A leaf holds several triangles, and two leaves whose boxes meet can hold pairs whose
                            // own boxes do not, which cannot meet either.
                            if (!Near(_triangleBounds[a], other._triangleBounds[b], reach))
                            {
                                continue;
                            }

                            if (Collision3.CollidesWith(_triangles[a], other._triangles[b], tolerance))
                            {
                                return true;
                            }
                        }
                    }

                    continue;
                }

                // Splitting the larger box first keeps the two sides descending at a similar rate.
                if (rightIsLeaf || (!leftIsLeaf && left.Count >= right.Count))
                {
                    pending.Push(left.Left);
                    pending.Push(rightIndex);
                    pending.Push(left.Right);
                    pending.Push(rightIndex);
                }
                else
                {
                    pending.Push(leftIndex);
                    pending.Push(right.Left);
                    pending.Push(leftIndex);
                    pending.Push(right.Right);
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a ray meets a box at any non-negative distance.
        /// </summary>
        /// <remarks>
        /// This is the slab test: the box is three pairs of parallel planes, and the ray is inside it over
        /// the stretch where all three intervals overlap. The tolerance widens each interval so a ray
        /// grazing a face is kept rather than dropped, which matters because dropping it would skip a
        /// whole branch of the tree.
        /// </remarks>
        private static bool RayHitsBox(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            double slack = tolerance.EqualPoint;
            double enter = 0.0;
            double exit = double.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                double origin = Component(ray.Origin, axis);
                double step = Component(GeoPoint3.Origin.Add(ray.Direction), axis);
                double low = Component(box.Min, axis);
                double high = Component(box.Max, axis);

                if (Math.Abs(step) <= slack)
                {
                    if (origin < low - slack || origin > high + slack)
                    {
                        return false;
                    }

                    continue;
                }

                double first = (low - origin) / step;
                double second = (high - origin) / step;

                if (first > second)
                {
                    double swap = first;
                    first = second;
                    second = swap;
                }

                enter = Math.Max(enter, first - slack);
                exit = Math.Min(exit, second + slack);

                if (enter > exit)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a ray enters a box grown by a distance on every side at any non-negative distance along it.
        /// </summary>
        /// <remarks>
        /// The slab test of <see cref="RayHitsBox"/>, with the box grown in space rather than the intervals along the
        /// ray: a ray starting 0.0004 over a box no thicker than a face and rising away at a slope of a hundredth passes
        /// the box's height 0.04 behind its origin, which a slack along the ray of a thousandth does not reach, though
        /// its origin is within the tolerance of the face.
        /// </remarks>
        private static bool RayEntersBox(GeoRay3 ray, GeoAabb3 box, double grow)
            => LineEntersBox(ray.Origin, ray.Direction, box, grow, 0.0);

        /// <summary>
        /// Checks whether a line from a point along a direction enters a box grown by a distance on every side, at any
        /// distance along it from <paramref name="lowest"/> on: nought for a ray, minus infinity for a line without ends; and
        /// no further than <paramref name="highest"/>, a segment read from its start along the step to its end reaching 1.
        /// </summary>
        private static bool LineEntersBox(GeoPoint3 start, GeoVector3 direction, GeoAabb3 box, double grow, double lowest, double highest = double.MaxValue)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            double enter = lowest;
            double exit = highest;

            for (int axis = 0; axis < 3; axis++)
            {
                double origin = Component(start, axis);
                double step = Component(GeoPoint3.Origin.Add(direction), axis);
                double low = Component(box.Min, axis) - grow;
                double high = Component(box.Max, axis) + grow;

                if (step == 0.0)
                {
                    if (origin < low || origin > high)
                    {
                        return false;
                    }

                    continue;
                }

                double first = (low - origin) / step;
                double second = (high - origin) / step;

                if (first > second)
                {
                    double swap = first;
                    first = second;
                    second = swap;
                }

                enter = Math.Max(enter, first);
                exit = Math.Min(exit, second);

                if (enter > exit)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Returns a string that represents the current hierarchy.
        /// </summary>
        public override string ToString() => $"Bvh3(Triangles: {TriangleCount}, Nodes: {_rootCount})";
        /// <summary>
        /// Creates a hierarchy over the surface of a face.
        /// </summary>
        public static GeoBvh3 FromFace(GeoFace3 face) => FromFace(face, Tolerance.Global);

        /// <summary>
        /// Creates a hierarchy over the surface of a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance the triangulation works to.</param>
        /// <returns>The hierarchy over the triangles of the face's material.</returns>
        /// <remarks>
        /// The mesh comes from <see cref="GeoFace3.TriangulateSurface(Tolerance)"/> and never from
        /// <c>Triangulate</c>: the latter fans the boundary from one vertex and is meant only for the signed
        /// sums, where the part of a fan reaching outside the face cancels against the part overlapping it.
        /// An index built on that would answer with points that are not on the face.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoBvh3 FromFace(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return new GeoBvh3(face.TriangulateSurface(tolerance));
        }

    }
}
