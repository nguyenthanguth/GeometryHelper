using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What the pruning of a curved offset reads off the shape it came from.
    /// </summary>
    internal static partial class ArcOffset2
    {
        /// <summary>
        /// The shape being offset, read for what the pruning asks of it: how near it comes to a point, and where
        /// a piece comes to the offset distance from one of its parts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A point stands nearer than the distance wherever any part of the shape does, an edge or one of its ends,
        /// and nothing else is asked. The part of the shape nearest a point is the inside of an edge, the foot of
        /// the perpendicular, or a vertex, and then the point lies in the wedge between the two edges' normals
        /// there: so a circle about a vertex bounds the offset only in that wedge.
        /// </para>
        /// <para>
        /// In the wedge of a corner tied by a straight chord, where nothing else could join it, what the offset leaves
        /// out is the triangle between the corner and the chord, not the circle: the chord cuts inside the circle, and
        /// a piece passing between the two still bounds the offset there. In the wedge of a corner joined by a miter or
        /// cut off square, it leaves out the whole of the join, which reaches beyond the circle: from the corner out to
        /// the pieces' ends, along them as run on, and across the cut.
        /// </para>
        /// </remarks>
        private sealed class Shape
        {
            private readonly List<GeoEdge2> _edges;
            private readonly GeoEdge2?[] _left;
            private readonly GeoEdge2?[] _right;
            private readonly Carrier?[] _own;
            private readonly Carrier?[] _leftCarriers;
            private readonly Carrier?[] _rightCarriers;
            private readonly Box[] _boxes;
            private readonly Box[] _leftBoxes;
            private readonly Box[] _rightBoxes;
            private readonly bool[] _movedReady;
            private readonly GeoPoint2[] _vertices;
            private readonly GeoLine2?[] _chords;
            private readonly List<Mitre> _mitres;
            private readonly GeoPolygonArc2[] _mitreLoops;
            private readonly GeoPoint2[][] _mitreCorners;
            private readonly double[] _mitreTurns;
            private readonly bool[] _mitreBuilt;
            private readonly List<Box> _mitreBoxes = new List<Box>();
            private BoxOrder _edgeOrder;
            private BoxOrder _vertexOrder;
            private BoxOrder _mitreOrder;
            private readonly List<int> _near = new List<int>();
            private readonly Tolerance _inside;
            private readonly bool _closed;
            private readonly Spans _spans;
            private readonly Region _region;
            private readonly double _sideways;
            private readonly double _distance;
            private readonly double _slack;
            private readonly double _depth;
            private readonly Tolerance _tolerance;

            internal Shape(List<GeoEdge2> edges, bool closed, Spans spans, Region region, double sideways, double slack, double depth, List<Piece> offset, List<Mitre> mitres, Tolerance tolerance)
            {
                double distance = Math.Abs(sideways);

                _edges = edges;
                _closed = closed;
                _spans = spans;
                _region = region;
                _sideways = sideways;
                _distance = distance;
                _slack = slack;
                _depth = depth;
                _tolerance = tolerance;

                int count = edges.Count;

                _left = new GeoEdge2?[count];
                _right = new GeoEdge2?[count];
                _own = new Carrier?[count];
                _leftCarriers = new Carrier?[count];
                _rightCarriers = new Carrier?[count];
                _boxes = new Box[count];
                _leftBoxes = new Box[count];
                _rightBoxes = new Box[count];
                _movedReady = new bool[count];
                _vertices = new GeoPoint2[closed ? count : count + 1];

                for (int i = 0; i < count; i++)
                {
                    _boxes[i] = Box.Of(edges[i]);
                    _vertices[i] = edges[i].StartPoint;
                }

                if (!closed && count > 0)
                {
                    _vertices[count] = edges[count - 1].EndPoint;
                }

                // The first chord built across each corner; a corner is bridged once.
                _chords = new GeoLine2?[_vertices.Length];

                foreach (Piece piece in offset)
                {
                    if (piece.Bridge && piece.Corner >= 0 && !piece.Across && !_chords[piece.Corner].HasValue)
                    {
                        _chords[piece.Corner] = new GeoLine2(piece.Edge.StartPoint, piece.Edge.EndPoint);
                    }
                }

                // The point of each miter lies between its corner and its two pieces as run on to where they meet; it
                // is drawn as a loop only when a point comes near enough to need it.
                _mitres = mitres;
                _mitreLoops = new GeoPolygonArc2[mitres.Count];
                _mitreCorners = new GeoPoint2[mitres.Count][];
                _mitreTurns = new double[mitres.Count];
                _mitreBuilt = new bool[mitres.Count];
                _inside = new Tolerance(_depth, tolerance.EqualVector, tolerance.EqualAngleRad, tolerance.EqualPlanar);
            }

            /// <summary>
            /// Moves an edge the distance to either side, the first time the pruning asks for it: a point crossing
            /// one of the two comes to the distance from the inside of that edge there.
            /// </summary>
            private void Move(int edge)
            {
                if (_movedReady[edge])
                {
                    return;
                }

                _left[edge] = TryMove(_edges[edge], _distance, _tolerance, out GeoEdge2 left) ? left : (GeoEdge2?)null;
                _right[edge] = TryMove(_edges[edge], -_distance, _tolerance, out GeoEdge2 right) ? right : (GeoEdge2?)null;
                _leftBoxes[edge] = _left[edge].HasValue ? Box.Of(_left[edge].Value).Grown(_tolerance.EqualPoint) : Box.Empty;
                _rightBoxes[edge] = _right[edge].HasValue ? Box.Of(_right[edge].Value).Grown(_tolerance.EqualPoint) : Box.Empty;
                _movedReady[edge] = true;
            }

            /// <summary>
            /// Gets the boxes holding the point of each miter, worked out the first time they are asked for.
            /// </summary>
            private List<Box> MitreBoxes()
            {
                if (_mitreBoxes.Count == _mitres.Count)
                {
                    return _mitreBoxes;
                }

                foreach (Mitre mitre in _mitres)
                {
                    Box first = mitre.StraightFirst ? Box.Of(mitre.Before).With(mitre.First) : Box.Of(CurveMeet2.Stretch(mitre.Before, mitre.First, true));
                    Box second = mitre.StraightSecond ? Box.Of(mitre.After).With(mitre.Second) : Box.Of(CurveMeet2.Stretch(mitre.After, mitre.Second, false));

                    _mitreBoxes.Add(first.With(second).With(_vertices[mitre.Corner]).Grown(_slack));
                }

                return _mitreBoxes;
            }

            /// <summary>
            /// Determines whether some part of the shape comes nearer to a point than a distance.
            /// </summary>
            /// <remarks>
            /// Read by the distance alone. It used to ask, of a point whose nearest part of an edge lay within the
            /// tolerance of the edge's end, whether it lay in that vertex's wedge, and let it stand when it did not: at
            /// a coarse tolerance the foot can lie a tolerance from the end and the point well outside the wedge, and
            /// neither edge at the vertex then claimed it. The U from (32.45, 0) to the origin, round a half turn of
            /// radius 1.1 and back along y = 2.2, offset by 6.28 into its inside at a tolerance of 0.1, kept the round
            /// joins about the half turn's ends, 4.08 from the shape.
            /// </remarks>
            internal bool Nearer(GeoPoint2 point, double limit)
            {
                if (limit > _distance)
                {
                    for (int i = 0; i < _edges.Count; i++)
                    {
                        if (NearerThan(i, point, limit))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                // Only an edge whose box, grown by the distance, holds the point can come nearer to it than that.
                BoxOrder order = EdgeOrder();

                for (int k = order.CountUpTo(point) - 1; k >= 0 && order.Reaches(k, point); k--)
                {
                    if (NearerThan(order[k], point, limit))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Gets the edges in order by their boxes grown by the distance and the tolerance, worked out the first time
            /// they are asked for.
            /// </summary>
            private BoxOrder EdgeOrder()
            {
                if (_edgeOrder == null)
                {
                    double reach = _distance + _tolerance.EqualPoint;
                    var grown = new Box[_edges.Count];

                    for (int i = 0; i < grown.Length; i++)
                    {
                        grown[i] = _boxes[i].Grown(reach);
                    }

                    _edgeOrder = new BoxOrder(grown);
                }

                return _edgeOrder;
            }

            /// <summary>
            /// Gets the vertices in order by the boxes round them as far as the distance and the tolerance, worked out
            /// the first time they are asked for.
            /// </summary>
            private BoxOrder VertexOrder()
            {
                if (_vertexOrder == null)
                {
                    double reach = _distance + _tolerance.EqualPoint;
                    var around = new Box[_vertices.Length];

                    for (int i = 0; i < around.Length; i++)
                    {
                        around[i] = Box.Around(_vertices[i], reach);
                    }

                    _vertexOrder = new BoxOrder(around);
                }

                return _vertexOrder;
            }

            /// <summary>
            /// Determines whether one edge of the shape comes nearer to a point than a distance.
            /// </summary>
            private bool NearerThan(int edge, GeoPoint2 point, double limit)
            {
                if (!_boxes[edge].Grown(limit).Holds(point))
                {
                    return false;
                }

                GeoPoint2 closest = Own(edge).Closest(point, _tolerance);

                if (point.DistanceTo(closest) >= limit)
                {
                    return false;
                }

                // Beyond the chord tied across one of the edge's ends, that end and the edges either side of it are what
                // the offset leaves out there, and they fold only the triangle under the chord.
                return !BeyondChordAt(edge, point) && !BeyondChordAt(Next(edge), point);
            }

            /// <summary>
            /// Determines whether a point lies in the wedge of a corner tied by a chord, beyond the chord.
            /// </summary>
            private bool BeyondChordAt(int corner, GeoPoint2 point)
            {
                return _chords[corner].HasValue && InWedge(_chords[corner].Value, corner, point) && BeyondChord(corner, point);
            }

            /// <summary>
            /// Gets the part of a piece run on past one of its old ends: from that end to the new one, or from the new
            /// start to the old one.
            /// </summary>
            private static GeoEdge2 Beyond(GeoEdge2 grown, GeoPoint2 old, bool atEnd, Tolerance tolerance)
            {
                double parameter = grown.GetParameterAtPoint(old, tolerance);

                if (!grown.TrySplitAtParameter(parameter, out GeoEdge2 before, out GeoEdge2 after, tolerance))
                {
                    if (!grown.IsArc || double.IsNaN(parameter))
                    {
                        return atEnd ? new GeoEdge2(old, grown.EndPoint) : new GeoEdge2(grown.StartPoint, old);
                    }

                    // A piece cut off no longer than the tolerance is not cut off, but the rest of an arc is still an arc: it
                    // keeps its share of the sweep, which a chord would not.
                    double share = Math.Max(0.0, Math.Min(1.0, parameter));
                    double swept = grown.ToArc().SweptAngle;

                    return atEnd
                        ? new GeoEdge2(old, grown.EndPoint, Math.Tan(swept * (1.0 - share) * 0.25))
                        : new GeoEdge2(grown.StartPoint, old, Math.Tan(swept * share * 0.25));
                }

                return atEnd ? after : before;
            }

            /// <summary>
            /// Gets a value indicating whether the side of one piece a crossing piece lies on is read from the angle between
            /// them however small, as it is for loops offset together, whose pieces meet at the narrow corners of a region that
            /// crosses itself, rather than within the angle tolerance.
            /// </summary>
            internal bool ExactSides => _region != null;

            /// <summary>
            /// Determines whether a point lies on the side of the region the offset goes to, which is always so for a shape
            /// that is one loop, whose pieces are moved to that side.
            /// </summary>
            internal bool OnTheOffsetSide(GeoPoint2 point) => _region == null || _region.Holds(point) == _region.WantInside;

            /// <summary>
            /// Determines whether a point lies inside the point of a miter, by more than the depth: the slack, or, in a second reading of an
            /// offset that left a run open, a thousandth of it.
            /// </summary>
            internal bool InMitre(GeoPoint2 point)
            {
                List<Box> boxes = _mitres.Count == 0 ? _mitreBoxes : MitreBoxes();

                if (boxes.Count == 0)
                {
                    return false;
                }

                _mitreOrder = _mitreOrder ?? new BoxOrder(boxes);

                for (int k = _mitreOrder.CountUpTo(point) - 1; k >= 0 && _mitreOrder.Reaches(k, point); k--)
                {
                    int i = _mitreOrder[k];

                    if (!boxes[i].Holds(point))
                    {
                        continue;
                    }

                    GeoPolygonArc2 loop = MitreLoop(i);

                    if (_mitreCorners[i] != null)
                    {
                        if (InsideStraight(_mitreCorners[i], _mitreTurns[i], point))
                        {
                            return true;
                        }

                        continue;
                    }

                    if (loop != null && loop.Locate(point, _inside) == PointLocation.Inside)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Gets the point of a miter as a loop: from the corner out to the end of the first piece, along it as run
            /// on to where the two meet, back along the second to its start, and home; null where it has no inside. A
            /// corner cut off square is the same loop with the cut between the two pieces as run on.
            /// </summary>
            private GeoPolygonArc2 MitreLoop(int index)
            {
                if (_mitreBuilt[index])
                {
                    return _mitreLoops[index];
                }

                Mitre mitre = _mitres[index];
                GeoEdge2 first = mitre.StraightFirst
                    ? new GeoEdge2(mitre.Before.EndPoint, mitre.First)
                    : Beyond(CurveMeet2.Stretch(mitre.Before, mitre.First, true), mitre.Before.EndPoint, true, _tolerance);
                GeoEdge2 second = mitre.StraightSecond
                    ? new GeoEdge2(mitre.Second, mitre.After.StartPoint)
                    : Beyond(CurveMeet2.Stretch(mitre.After, mitre.Second, false), mitre.After.StartPoint, false, _tolerance);

                GeoPoint2[] starts = mitre.Square
                    ? new[] { _vertices[mitre.Corner], first.StartPoint, first.EndPoint, second.StartPoint, second.EndPoint }
                    : new[] { _vertices[mitre.Corner], first.StartPoint, second.StartPoint, second.EndPoint };
                double[] bulges = mitre.Square
                    ? new[] { 0.0, first.Bulge, 0.0, second.Bulge, 0.0 }
                    : new[] { 0.0, first.Bulge, second.Bulge, 0.0 };

                _mitreBuilt[index] = true;

                // A corner cut off square with straight sides all round is a convex loop, read side by side.
                if (mitre.Square && !first.IsArc && !second.IsArc && TryConvex(starts, out double turn))
                {
                    _mitreCorners[index] = starts;
                    _mitreTurns[index] = turn;
                    return null;
                }

                _mitreLoops[index] = GeoPolygonArc2.TryFromEdges(starts, bulges, out GeoPolygonArc2 loop) ? loop : null;

                return _mitreLoops[index];
            }

            /// <summary>
            /// Determines whether a loop of straight sides turns one way only, and which: 1 counter-clockwise, -1
            /// clockwise. Sides of no length are passed over.
            /// </summary>
            private static bool TryConvex(GeoPoint2[] corners, out double turn)
            {
                turn = 0.0;

                int count = corners.Length;

                for (int i = 0; i < count; i++)
                {
                    GeoVector2 side = corners[i].GetVectorTo(corners[(i + 1) % count]);
                    GeoVector2 next = corners[(i + 1) % count].GetVectorTo(corners[(i + 2) % count]);
                    double cross = side.CrossProduct(next);

                    if (cross == 0.0)
                    {
                        continue;
                    }

                    if (turn == 0.0)
                    {
                        turn = Math.Sign(cross);
                    }
                    else if (Math.Sign(cross) != turn)
                    {
                        return false;
                    }
                }

                return turn != 0.0;
            }

            /// <summary>
            /// Determines whether a point lies inside a convex loop of straight sides by more than the depth: further than
            /// the depth inside the line of every side, which is how far it lies inside the loop.
            /// </summary>
            private bool InsideStraight(GeoPoint2[] corners, double turn, GeoPoint2 point)
            {
                int count = corners.Length;

                for (int i = 0; i < count; i++)
                {
                    GeoPoint2 from = corners[i];
                    GeoVector2 side = from.GetVectorTo(corners[(i + 1) % count]);
                    double length = side.Length;

                    if (length <= 0.0)
                    {
                        continue;
                    }

                    if (side.CrossProduct(from.GetVectorTo(point)) * turn / length <= _depth)
                    {
                        return false;
                    }
                }

                return true;
            }

            private Carrier Own(int edge) => (_own[edge] ?? (_own[edge] = new Carrier(_edges[edge]))).Value;

            private Carrier Left(int edge) => (_leftCarriers[edge] ?? (_leftCarriers[edge] = new Carrier(_left[edge].Value))).Value;

            private Carrier Right(int edge) => (_rightCarriers[edge] ?? (_rightCarriers[edge] = new Carrier(_right[edge].Value))).Value;

            /// <summary>
            /// Determines whether the circle of the distance about a vertex bounds the offset at a point of it.
            /// </summary>
            private bool CircleBounds(int corner, GeoPoint2 point)
            {
                return InCone(corner, point) && !(_chords[corner].HasValue && InWedge(_chords[corner].Value, corner, point));
            }

            /// <summary>
            /// Determines whether a point lies in the wedge where a vertex can be the part of the shape nearest it:
            /// behind the edge leaving the vertex and ahead of the one arriving, within the tolerance.
            /// </summary>
            private bool InCone(int corner, GeoPoint2 point)
            {
                GeoVector2 towards = _vertices[corner].GetVectorTo(point);
                int incoming = _closed ? (corner == _spans.First[corner] ? _spans.Last[corner] : corner - 1) : corner - 1;
                int outgoing = _closed || corner < _edges.Count ? corner : -1;

                if (incoming >= 0)
                {
                    GeoVector2 arriving = Heading(_edges[incoming], _edges[incoming].EndPoint);

                    if (towards.DotProduct(arriving) < -_tolerance.EqualPoint * arriving.Length)
                    {
                        return false;
                    }
                }

                if (outgoing >= 0)
                {
                    GeoVector2 leaving = Heading(_edges[outgoing], _edges[outgoing].StartPoint);

                    if (towards.DotProduct(leaving) > _tolerance.EqualPoint * leaving.Length)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Determines whether a point lies in the wedge between a corner and the two moved ends a join spans.
            /// </summary>
            private bool InWedge(GeoLine2 span, int corner, GeoPoint2 point)
            {
                GeoPoint2 vertex = _vertices[corner];
                GeoVector2 first = vertex.GetVectorTo(span.StartPoint);
                GeoVector2 second = vertex.GetVectorTo(span.EndPoint);
                GeoVector2 towards = vertex.GetVectorTo(point);
                double turn = first.CrossProduct(second);

                return first.CrossProduct(towards) * turn > 0.0 && towards.CrossProduct(second) * turn > 0.0;
            }

            /// <summary>
            /// Determines whether a point lies on the far side of a corner's chord from the corner, or no more than the
            /// slack inside it.
            /// </summary>
            private bool BeyondChord(int corner, GeoPoint2 point)
            {
                GeoLine2 chord = _chords[corner].Value;
                GeoVector2 along = chord.StartPoint.GetVectorTo(chord.EndPoint);
                double length = along.Length;

                if (length <= 0.0)
                {
                    return false;
                }

                double inside = along.CrossProduct(chord.StartPoint.GetVectorTo(point)) / length;
                double side = along.CrossProduct(chord.StartPoint.GetVectorTo(_vertices[corner]));

                return (side > 0.0 ? inside : -inside) <= _slack;
            }

            /// <summary>
            /// Notes where a piece comes to the offset distance from a part of the shape other than its own.
            /// </summary>
            internal void AddReaches(Piece piece, List<GeoPoint2> cuts, Tolerance tolerance)
            {
                GeoEdge2 edge = piece.Edge;
                var carrier = new Carrier(edge);
                Box box = Box.Of(edge).Grown(tolerance.EqualPoint);
                double reach = _distance + tolerance.EqualPoint;

                // The edges and vertices near enough to matter, taken in the order they come in the shape.
                EdgeOrder().Overlapping(box, _near);

                foreach (int i in _near)
                {
                    // A moved piece stands the distance from the whole of its own edge, and an edge further from the
                    // piece than the distance cannot come to it.
                    if (i == piece.Source || !box.Overlaps(_boxes[i].Grown(reach)))
                    {
                        continue;
                    }

                    Move(i);

                    if (box.Overlaps(_leftBoxes[i]))
                    {
                        AddCrossings(cuts, edge, carrier, Left(i), tolerance);
                    }

                    if (box.Overlaps(_rightBoxes[i]))
                    {
                        AddCrossings(cuts, edge, carrier, Right(i), tolerance);
                    }
                }

                VertexOrder().Overlapping(box, _near);

                foreach (int i in _near)
                {
                    // And from the ends of its own edge, as a join does from the corner it was built about.
                    if (i == piece.Corner || (piece.Source >= 0 && (i == piece.Source || i == Next(piece.Source))))
                    {
                        continue;
                    }

                    if (!box.Overlaps(Box.Around(_vertices[i], reach)))
                    {
                        continue;
                    }

                    foreach (GeoPoint2 at in carrier.Meet(new GeoCircle2(_vertices[i], _distance), tolerance))
                    {
                        // Only where the circle bounds the offset does the piece come to the distance there.
                        if (CircleBounds(i, at))
                        {
                            Mark(cuts, edge, at, tolerance);
                        }
                    }
                }
            }

            private int Next(int edge) => _closed ? (edge == _spans.Last[edge] ? _spans.First[edge] : edge + 1) : edge + 1;

            private static void AddCrossings(List<GeoPoint2> cuts, GeoEdge2 edge, Carrier carrier, Carrier curve, Tolerance tolerance)
            {
                foreach (GeoPoint2 at in carrier.Meet(curve, tolerance))
                {
                    Mark(cuts, edge, at, tolerance);
                }
            }
        }

        /// <summary>
        /// An edge with the arc it draws worked out once, since the pruning asks the same edges over and over; it
        /// answers exactly as the edge itself does.
        /// </summary>
        private struct Carrier
        {
            private readonly GeoEdge2 _edge;
            private readonly GeoArc2 _arc;

            internal Carrier(GeoEdge2 edge)
            {
                _edge = edge;
                _arc = edge.IsArc ? edge.ToArc() : default(GeoArc2);
            }

            internal GeoEdge2 Edge => _edge;

            internal GeoArc2 Arc => _arc;

            /// <summary>
            /// Gets the point of the edge nearest a point, as <see cref="GeoEdge2.GetClosestPointOnBoundary(GeoPoint2, Tolerance)"/> does.
            /// </summary>
            internal GeoPoint2 Closest(GeoPoint2 point, Tolerance tolerance)
            {
                return _edge.IsArc ? Arc2.ProjectToArc(_arc, point, tolerance) : Projection2.ProjectToLine(_edge.ToLine(), point, tolerance);
            }

            /// <summary>
            /// Gets where the edge meets another, as <see cref="GeoEdge2.GetIntersections(GeoEdge2, Tolerance)"/> does.
            /// </summary>
            internal GeoPoint2[] Meet(Carrier other, Tolerance tolerance)
            {
                if (_edge.IsArc)
                {
                    return other._edge.IsArc
                        ? Arc2.GetIntersections(_arc, other._arc, tolerance)
                        : Arc2.GetIntersections(_arc, other._edge.ToLine(), tolerance);
                }

                return other._edge.IsArc
                    ? Arc2.GetIntersections(other._arc, _edge.ToLine(), tolerance)
                    : _edge.GetIntersections(other._edge.ToLine(), tolerance);
            }

            /// <summary>
            /// Gets where the edge crosses a circle, as <see cref="GeoEdge2.GetIntersections(GeoCircle2, Tolerance)"/> does.
            /// </summary>
            internal GeoPoint2[] Meet(GeoCircle2 circle, Tolerance tolerance)
            {
                return _edge.IsArc ? _arc.GetIntersections(circle, tolerance) : _edge.ToLine().GetIntersections(circle, tolerance);
            }
        }

        /// <summary>
        /// Boxes in order along the axis a point lies within fewest of them on, so that those that can hold a point are
        /// found without trying every one: the boxes starting at or before the point along that axis, back to where none
        /// of the ones before reaches it.
        /// </summary>
        /// <remarks>
        /// The axis is read off the boxes, as a sweep's is: on a shape long in x every box spans little of x, and a
        /// point lies within few of them along it. Ties are broken by the order the boxes came in, so the order never
        /// depends on the sort.
        /// </remarks>
        private sealed class BoxOrder
        {
            /// <summary>
            /// Up to this many boxes, trying every one costs less than putting them in order.
            /// </summary>
            private const int Few = 32;

            private readonly int[] _order;
            private readonly double[] _starts;
            private readonly double[] _reaches;
            private readonly bool _alongX;
            private bool[] _marks;

            internal BoxOrder(IList<Box> boxes)
            {
                _order = new int[boxes.Count];

                for (int i = 0; i < _order.Length; i++)
                {
                    _order[i] = i;
                }

                // A few boxes stay in the order they came, and every one of them is tried.
                if (_order.Length <= Few)
                {
                    return;
                }

                double widthX = 0.0;
                double widthY = 0.0;
                Box all = Box.Empty;

                foreach (Box box in boxes)
                {
                    if (box.MaxX < box.MinX || box.MaxY < box.MinY)
                    {
                        continue;
                    }

                    widthX += box.MaxX - box.MinX;
                    widthY += box.MaxY - box.MinY;
                    all = all.With(box);
                }

                // How many boxes a point lies within along each axis, on average: their widths over their spread.
                bool alongX = widthX * (all.MaxY - all.MinY) <= widthY * (all.MaxX - all.MinX);

                _alongX = alongX;

                Array.Sort(_order, (one, other) =>
                {
                    int byStart = (alongX ? boxes[one].MinX : boxes[one].MinY).CompareTo(alongX ? boxes[other].MinX : boxes[other].MinY);

                    return byStart != 0 ? byStart : one.CompareTo(other);
                });

                _starts = new double[_order.Length];
                _reaches = new double[_order.Length];

                double reach = double.NegativeInfinity;

                for (int k = 0; k < _order.Length; k++)
                {
                    Box box = boxes[_order[k]];

                    _starts[k] = alongX ? box.MinX : box.MinY;
                    reach = Math.Max(reach, alongX ? box.MaxX : box.MaxY);
                    _reaches[k] = reach;
                }
            }

            /// <summary>
            /// Gets the index of the box at a place in the order.
            /// </summary>
            internal int this[int place] => _order[place];

            /// <summary>
            /// Gets how many boxes start at or before a point along the axis.
            /// </summary>
            internal int CountUpTo(GeoPoint2 point) => _starts == null ? _order.Length : CountUpTo(_alongX ? point.X : point.Y);

            /// <summary>
            /// Gets the indices of the boxes that overlap a box along the axis, smallest first: the order they came in.
            /// </summary>
            internal void Overlapping(Box box, List<int> into)
            {
                into.Clear();

                if (_starts == null)
                {
                    into.AddRange(_order);
                    return;
                }

                double low = _alongX ? box.MinX : box.MinY;
                int end = CountUpTo(_alongX ? box.MaxX : box.MaxY);
                int start = end;

                while (start > 0 && _reaches[start - 1] >= low)
                {
                    start--;
                }

                // Many of them, as the long chords of a star across the whole of it are, are put in order by marking them
                // and reading the marks, which costs no more than there are boxes; a few are sorted.
                int count = end - start;

                if (count > 64 && count * Math.Log(count, 2.0) > _order.Length)
                {
                    _marks = _marks ?? new bool[_order.Length];

                    for (int k = start; k < end; k++)
                    {
                        _marks[_order[k]] = true;
                    }

                    for (int i = 0; i < _marks.Length; i++)
                    {
                        if (_marks[i])
                        {
                            into.Add(i);
                            _marks[i] = false;
                        }
                    }

                    return;
                }

                for (int k = end - 1; k >= start; k--)
                {
                    into.Add(_order[k]);
                }

                into.Sort();
            }

            private int CountUpTo(double at)
            {
                int low = 0;
                int high = _starts.Length;

                while (low < high)
                {
                    int middle = low + (high - low) / 2;

                    if (_starts[middle] <= at)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                return low;
            }

            /// <summary>
            /// Determines whether any box up to a place in the order reaches as far as a point along the axis.
            /// </summary>
            internal bool Reaches(int place, GeoPoint2 point) => _reaches == null || _reaches[place] >= (_alongX ? point.X : point.Y);
        }

        /// <summary>
        /// A box with its sides along the axes.
        /// </summary>
        private struct Box
        {
            private Box(double minX, double minY, double maxX, double maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            internal static Box Empty => new Box(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

            internal double MinX { get; }

            internal double MinY { get; }

            internal double MaxX { get; }

            internal double MaxY { get; }

            /// <summary>
            /// Gets a box holding an edge: its chord grown by how far an arc of at most a half turn bulges off it,
            /// or the whole circle of one that bulges further.
            /// </summary>
            internal static Box Of(GeoEdge2 edge)
            {
                GeoPoint2 start = edge.StartPoint;
                GeoPoint2 end = edge.EndPoint;
                var chord = new Box(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

                if (!edge.IsArc)
                {
                    return chord;
                }

                double bulge = Math.Abs(edge.Bulge);

                if (bulge <= 1.0)
                {
                    return chord.Grown(start.DistanceTo(end) * 0.5 * bulge);
                }

                GeoArc2 arc = edge.ToArc();

                return Around(arc.Center, arc.Radius);
            }

            internal static Box Around(GeoPoint2 centre, double radius)
                => new Box(centre.X - radius, centre.Y - radius, centre.X + radius, centre.Y + radius);

            internal Box Grown(double by) => new Box(MinX - by, MinY - by, MaxX + by, MaxY + by);

            internal Box With(Box other)
                => new Box(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

            internal Box With(GeoPoint2 point)
                => new Box(Math.Min(MinX, point.X), Math.Min(MinY, point.Y), Math.Max(MaxX, point.X), Math.Max(MaxY, point.Y));

            internal bool Overlaps(Box other)
                => MinX <= other.MaxX && other.MinX <= MaxX && MinY <= other.MaxY && other.MinY <= MaxY;

            internal bool Holds(GeoPoint2 point)
                => point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;
        }
    }
}
