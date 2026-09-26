using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Spatial
{
    /// <summary>
    /// A bounding volume hierarchy over the edges of a shape in the plane, the counterpart of
    /// <see cref="GeoBvh3"/> in space.
    /// <para>
    /// It is built over <see cref="GeoEdge2"/>, so one index serves a straight chain and a curved one
    /// alike: an arc is held as an arc and measured as an arc, and nothing is flattened to get it into the
    /// tree. Building costs a walk over the edges; after that, asking what is nearest a point, or where a
    /// segment crosses, stops looking at whole branches that cannot reach.
    /// </para>
    /// <para>
    /// It is worth building when the same shape is asked many questions. For a handful of questions the
    /// plain methods on the shape are quicker, because they do not build anything.
    /// </para>
    /// </summary>
    public sealed class GeoBvh2
    {
        private const int LeafSize = 4;

        private struct Box
        {
            internal double MinX;
            internal double MinY;
            internal double MaxX;
            internal double MaxY;

            internal static Box Empty => new Box { MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue };

            internal bool IsEmpty => MinX > MaxX;

            internal void Add(Box other)
            {
                if (other.MinX < MinX) MinX = other.MinX;
                if (other.MinY < MinY) MinY = other.MinY;
                if (other.MaxX > MaxX) MaxX = other.MaxX;
                if (other.MaxY > MaxY) MaxY = other.MaxY;
            }

            /// <summary>
            /// Gets how far a point stands outside the box, and zero when it is inside.
            /// </summary>
            internal double DistanceTo(GeoPoint2 point)
            {
                double x = point.X < MinX ? MinX - point.X : point.X > MaxX ? point.X - MaxX : 0.0;
                double y = point.Y < MinY ? MinY - point.Y : point.Y > MaxY ? point.Y - MaxY : 0.0;

                return Math.Sqrt(x * x + y * y);
            }

            /// <summary>
            /// Gets how far two boxes stand apart, and zero when they touch or overlap.
            /// </summary>
            internal double DistanceTo(Box other)
            {
                double x = other.MinX > MaxX ? other.MinX - MaxX : MinX > other.MaxX ? MinX - other.MaxX : 0.0;
                double y = other.MinY > MaxY ? other.MinY - MaxY : MinY > other.MaxY ? MinY - other.MaxY : 0.0;

                return Math.Sqrt(x * x + y * y);
            }

            /// <summary>
            /// Determines whether a box reaches a stretch of the plane at all.
            /// </summary>
            internal bool Reaches(double minX, double minY, double maxX, double maxY, double slack)
            {
                return minX <= MaxX + slack && maxX >= MinX - slack && minY <= MaxY + slack && maxY >= MinY - slack;
            }
        }

        private struct Node
        {
            internal Box Bounds;
            internal int Start;
            internal int Count;
            internal int Left;
            internal int Right;
        }

        private readonly GeoEdge2[] _edges;
        private readonly Box[] _boxes;
        private readonly int[] _order;
        private readonly Node[] _nodes;
        private int _nodeCount;

        /// <summary>
        /// Initializes a hierarchy over a run of edges.
        /// </summary>
        /// <param name="edges">The edges to index; they need not join up or be in any order.</param>
        /// <exception cref="ArgumentNullException">Thrown when the edges are null.</exception>
        public GeoBvh2(IEnumerable<GeoEdge2> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));

            var kept = new List<GeoEdge2>(edges);

            _edges = kept.ToArray();
            _boxes = new Box[_edges.Length];
            _order = new int[_edges.Length];

            for (int i = 0; i < _edges.Length; i++)
            {
                _boxes[i] = BoxOf(_edges[i]);
                _order[i] = i;
            }

            // A tree over n leaves needs fewer than 2n nodes, and one more for the empty case.
            _nodes = new Node[Math.Max(1, _edges.Length * 2)];
            _nodeCount = 0;

            if (_edges.Length > 0)
            {
                _nodeCount = 1;
                Build(0, 0, _edges.Length);
            }
        }

        /// <summary>
        /// Builds a hierarchy over the edges of a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoBvh2 FromPolygonArc(GeoPolygonArc2 loop)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return new GeoBvh2(loop.GetEdges());
        }

        /// <summary>
        /// Builds a hierarchy over the edges of a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoBvh2 FromPolylineArc(GeoPolylineArc2 chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return new GeoBvh2(chain.GetEdges());
        }

        /// <summary>
        /// Builds a hierarchy over the edges of a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoBvh2 FromPolygon(GeoPolygon2 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            var edges = new List<GeoEdge2>(polygon.VertexCount);

            for (int i = 0; i < polygon.VertexCount; i++)
            {
                edges.Add(new GeoEdge2(polygon[i], polygon[(i + 1) % polygon.VertexCount]));
            }

            return new GeoBvh2(edges);
        }

        /// <summary>
        /// Builds a hierarchy over the edges of a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoBvh2 FromPolyline(GeoPolyline2 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            var edges = new List<GeoEdge2>(polyline.VertexCount - 1);

            for (int i = 0; i < polyline.VertexCount - 1; i++)
            {
                edges.Add(new GeoEdge2(polyline[i], polyline[i + 1]));
            }

            return new GeoBvh2(edges);
        }

        /// <summary>
        /// Gets the edges the hierarchy was built over, in the order they were given.
        /// </summary>
        public IReadOnlyList<GeoEdge2> Edges => _edges;

        /// <summary>
        /// Gets the number of edges indexed.
        /// </summary>
        public int EdgeCount => _edges.Length;

        /// <summary>
        /// Gets the box round everything indexed, as a rectangle square to the axes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when nothing was indexed.</exception>
        public GeoRectangle2 Bounds
        {
            get
            {
                if (_nodeCount == 0)
                {
                    throw new InvalidOperationException("Nothing was indexed, so there is no box round it.");
                }

                Box box = _nodes[0].Bounds;

                return new GeoRectangle2(
                    new GeoPoint2((box.MinX + box.MaxX) * 0.5, (box.MinY + box.MaxY) * 0.5),
                    box.MaxX - box.MinX,
                    box.MaxY - box.MinY,
                    0.0);
            }
        }

        /// <summary>
        /// Gets the point of the indexed edges nearest a point.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when nothing was indexed.</exception>
        public GeoPoint2 GetClosestPoint(GeoPoint2 point) => GetClosestPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the indexed edges nearest a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A branch whose box is further away than the best found so far cannot hold anything nearer, so
        /// it is not looked at. That is the whole of what the tree buys.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when nothing was indexed.</exception>
        public GeoPoint2 GetClosestPoint(GeoPoint2 point, Tolerance tolerance)
        {
            if (_nodeCount == 0)
            {
                throw new InvalidOperationException("Nothing was indexed, so there is no point to give.");
            }

            GeoPoint2 nearest = _edges[0].GetClosestPointOnBoundary(point, tolerance);
            double best = point.GetDistanceSquaredTo(nearest);

            Search(0, point, tolerance, ref nearest, ref best);

            return nearest;
        }

        /// <summary>
        /// Gets the distance from a point to the nearest indexed edge.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when nothing was indexed.</exception>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a point to the nearest indexed edge, within a tolerance.
        /// </summary>
        /// <remarks>
        /// This is the distance to the edges, not to the region they may enclose: a point well inside a
        /// closed shape is measured out to its boundary rather than called nought, which is what
        /// <see cref="GeoPolygonArc2.DistanceTo(GeoPoint2)"/> would say of it. An index holds edges and
        /// knows nothing of what they enclose.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when nothing was indexed.</exception>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => point.DistanceTo(GetClosestPoint(point, tolerance));

        /// <summary>
        /// Gets the points where a straight segment crosses the indexed edges.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the points where a straight segment crosses the indexed edges, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            if (_nodeCount == 0)
            {
                return found.ToArray();
            }

            double minX = Math.Min(line.StartPoint.X, line.EndPoint.X);
            double maxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
            double minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
            double maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);

            Cross(0, line, minX, minY, maxX, maxY, tolerance, found);

            return found.ToArray();
        }

        /// <summary>
        /// Gets the distance between two hierarchies, measured between the edges they hold.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other hierarchy is null.</exception>
        public double DistanceTo(GeoBvh2 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the distance between two hierarchies, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other hierarchy is null.</exception>
        public double DistanceTo(GeoBvh2 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            if (_nodeCount == 0 || other._nodeCount == 0)
            {
                return double.PositiveInfinity;
            }

            double best = double.MaxValue;

            Between(0, other, 0, tolerance, ref best);

            return best;
        }

        /// <summary>
        /// Determines whether the edges of two hierarchies touch or cross.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other hierarchy is null.</exception>
        public bool CollidesWith(GeoBvh2 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether the edges of two hierarchies touch or cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// This is about the edges meeting, not about one shape lying inside the other; a small loop
        /// wholly within a large one touches nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other hierarchy is null.</exception>
        public bool CollidesWith(GeoBvh2 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return DistanceTo(other, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Describes the hierarchy.
        /// </summary>
        public override string ToString() => $"GeoBvh2[Edges: {EdgeCount}, Nodes: {_nodeCount}]";

        /// <summary>
        /// Gets the box round one edge: round the arc itself rather than round the whole circle.
        /// </summary>
        private static Box BoxOf(GeoEdge2 edge)
        {
            var box = new Box
            {
                MinX = Math.Min(edge.StartPoint.X, edge.EndPoint.X),
                MinY = Math.Min(edge.StartPoint.Y, edge.EndPoint.Y),
                MaxX = Math.Max(edge.StartPoint.X, edge.EndPoint.X),
                MaxY = Math.Max(edge.StartPoint.Y, edge.EndPoint.Y)
            };

            if (!edge.IsArc)
            {
                return box;
            }

            // An arc bulges past its chord, and the furthest it reaches along each axis is wherever it
            // crosses the line through its centre in that direction.
            GeoArc2 arc = edge.ToArc();

            for (int quarter = 0; quarter < 4; quarter++)
            {
                double angle = quarter * Math.PI * 0.5;

                if (!Covers(arc, angle))
                {
                    continue;
                }

                GeoPoint2 at = arc.GetPointAtAngle(angle);

                if (at.X < box.MinX) box.MinX = at.X;
                if (at.Y < box.MinY) box.MinY = at.Y;
                if (at.X > box.MaxX) box.MaxX = at.X;
                if (at.Y > box.MaxY) box.MaxY = at.Y;
            }

            return box;
        }

        /// <summary>
        /// Determines whether an arc sweeps through an angle.
        /// </summary>
        private static bool Covers(GeoArc2 arc, double angle)
        {
            double full = Math.PI * 2.0;
            double from = arc.SweptAngle >= 0.0 ? arc.StartAngle : arc.StartAngle + arc.SweptAngle;
            double swept = Math.Abs(arc.SweptAngle);

            double reach = (angle - from) % full;

            if (reach < 0.0)
            {
                reach += full;
            }

            return reach <= swept;
        }

        /// <summary>
        /// Splits a stretch of the edges in two, over and over, until the pieces are small enough.
        /// </summary>
        private void Build(int node, int start, int count)
        {
            Box bounds = Box.Empty;

            for (int i = start; i < start + count; i++)
            {
                bounds.Add(_boxes[_order[i]]);
            }

            _nodes[node] = new Node { Bounds = bounds, Start = start, Count = count, Left = -1, Right = -1 };

            if (count <= LeafSize)
            {
                return;
            }

            // Split across whichever way the stretch is longer, at the middle of the edges rather than at
            // the middle of the box, so that a crowd on one side does not make a lopsided tree.
            bool byX = bounds.MaxX - bounds.MinX >= bounds.MaxY - bounds.MinY;

            Array.Sort(_order, start, count, new Along(_boxes, byX));

            int half = count / 2;

            int left = _nodeCount++;
            int right = _nodeCount++;

            Node parent = _nodes[node];
            parent.Left = left;
            parent.Right = right;
            _nodes[node] = parent;

            Build(left, start, half);
            Build(right, start + half, count - half);
        }

        /// <summary>
        /// Orders edges by the middle of their boxes along one axis.
        /// </summary>
        private sealed class Along : IComparer<int>
        {
            private readonly Box[] _boxes;
            private readonly bool _byX;

            internal Along(Box[] boxes, bool byX)
            {
                _boxes = boxes;
                _byX = byX;
            }

            public int Compare(int first, int second)
            {
                double one = _byX ? _boxes[first].MinX + _boxes[first].MaxX : _boxes[first].MinY + _boxes[first].MaxY;
                double other = _byX ? _boxes[second].MinX + _boxes[second].MaxX : _boxes[second].MinY + _boxes[second].MaxY;

                return one.CompareTo(other);
            }
        }

        /// <summary>
        /// Looks for the point nearest a point, leaving alone the branches that cannot hold one.
        /// </summary>
        private void Search(int node, GeoPoint2 point, Tolerance tolerance, ref GeoPoint2 nearest, ref double best)
        {
            Node one = _nodes[node];

            double reach = one.Bounds.DistanceTo(point);

            if (reach * reach > best)
            {
                return;
            }

            if (one.Left < 0)
            {
                for (int i = one.Start; i < one.Start + one.Count; i++)
                {
                    GeoPoint2 candidate = _edges[_order[i]].GetClosestPointOnBoundary(point, tolerance);
                    double distance = point.GetDistanceSquaredTo(candidate);

                    if (distance < best)
                    {
                        best = distance;
                        nearest = candidate;
                    }
                }

                return;
            }

            // The nearer child first, so that the further one is more often ruled out before it is opened.
            double toLeft = _nodes[one.Left].Bounds.DistanceTo(point);
            double toRight = _nodes[one.Right].Bounds.DistanceTo(point);

            if (toLeft <= toRight)
            {
                Search(one.Left, point, tolerance, ref nearest, ref best);
                Search(one.Right, point, tolerance, ref nearest, ref best);
            }
            else
            {
                Search(one.Right, point, tolerance, ref nearest, ref best);
                Search(one.Left, point, tolerance, ref nearest, ref best);
            }
        }

        /// <summary>
        /// Collects where a segment crosses the edges, leaving alone the branches it cannot reach.
        /// </summary>
        private void Cross(int node, GeoLine2 line, double minX, double minY, double maxX, double maxY, Tolerance tolerance, List<GeoPoint2> found)
        {
            Node one = _nodes[node];

            if (!one.Bounds.Reaches(minX, minY, maxX, maxY, tolerance.EqualPoint))
            {
                return;
            }

            if (one.Left < 0)
            {
                for (int i = one.Start; i < one.Start + one.Count; i++)
                {
                    foreach (GeoPoint2 meeting in _edges[_order[i]].GetIntersections(line, tolerance))
                    {
                        bool already = false;

                        foreach (GeoPoint2 kept in found)
                        {
                            if (kept.IsEqualTo(meeting, tolerance))
                            {
                                already = true;
                                break;
                            }
                        }

                        if (!already)
                        {
                            found.Add(meeting);
                        }
                    }
                }

                return;
            }

            Cross(one.Left, line, minX, minY, maxX, maxY, tolerance, found);
            Cross(one.Right, line, minX, minY, maxX, maxY, tolerance, found);
        }

        /// <summary>
        /// Looks for the nearest approach between two hierarchies, leaving alone the pairs of branches
        /// that already stand further apart than the best found.
        /// </summary>
        private void Between(int node, GeoBvh2 other, int otherNode, Tolerance tolerance, ref double best)
        {
            Node one = _nodes[node];
            Node two = other._nodes[otherNode];

            if (best <= 0.0 || one.Bounds.DistanceTo(two.Bounds) >= best)
            {
                return;
            }

            if (one.Left < 0 && two.Left < 0)
            {
                for (int i = one.Start; i < one.Start + one.Count; i++)
                {
                    for (int j = two.Start; j < two.Start + two.Count; j++)
                    {
                        double distance = _edges[_order[i]].DistanceTo(other._edges[other._order[j]], tolerance);

                        if (distance < best)
                        {
                            best = distance;

                            if (best <= 0.0)
                            {
                                return;
                            }
                        }
                    }
                }

                return;
            }

            // Open whichever of the two is still a branch, and the bigger one first.
            bool openThis = two.Left < 0 || (one.Left >= 0 && one.Count >= two.Count);

            if (openThis)
            {
                Between(one.Left, other, otherNode, tolerance, ref best);
                Between(one.Right, other, otherNode, tolerance, ref best);
            }
            else
            {
                Between(node, other, two.Left, tolerance, ref best);
                Between(node, other, two.Right, tolerance, ref best);
            }
        }
        /// <summary>
        /// Creates a hierarchy over the boundary of a face: its outline together with the rim of every hole.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <returns>The hierarchy over every edge of the face's boundary.</returns>
        /// <remarks>
        /// The rim of a hole is part of the boundary of the material, which is the reading the whole library
        /// keeps for a face, so the nearest point this finds is the nearest point of the material's edge —
        /// a point sitting in a hole is answered by the rim it sits in, not by the outline far away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoBvh2 FromFace(GeoFace2 face)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var edges = new List<GeoEdge2>();

            AddLoop(edges, face.Boundary);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                AddLoop(edges, hole);
            }

            return new GeoBvh2(edges);
        }

        /// <summary>
        /// Adds the closed run of edges of one loop.
        /// </summary>
        private static void AddLoop(List<GeoEdge2> edges, GeoPolygon2 loop)
        {
            for (int i = 0; i < loop.VertexCount; i++)
            {
                edges.Add(new GeoEdge2(loop[i], loop[(i + 1) % loop.VertexCount]));
            }
        }

    }
}
