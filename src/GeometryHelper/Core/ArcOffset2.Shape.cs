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
        /// The part of the shape nearest a point is the inside of an edge, the foot of the perpendicular, or a
        /// vertex, and then the point lies in the wedge between the two edges' normals there: anywhere else, one of
        /// the edges runs nearer. A vertex nearer than the distance folds a point only in that wedge, so that what
        /// stands nearer than the distance is exactly what the distance says, and a circle about a vertex bounds the
        /// offset only there.
        /// </para>
        /// <para>
        /// In the wedge of a corner bridged by a straight chord, a chamfer or a miter cut short, what the offset
        /// leaves out is the triangle between the corner and the chord, not the circle: the chord cuts inside the
        /// circle on purpose, and a piece passing between the two still bounds the offset there. In the wedge of a
        /// corner joined by a miter, it leaves out the whole point of the miter, which reaches beyond the circle.
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
            private readonly bool[] _mitreBuilt;
            private readonly List<Box> _mitreBoxes = new List<Box>();
            private readonly Tolerance _inside;
            private readonly bool _closed;
            private readonly double _sideways;
            private readonly double _distance;
            private readonly double _slack;
            private readonly Tolerance _tolerance;

            internal Shape(List<GeoEdge2> edges, bool closed, double sideways, double slack, List<Piece> offset, List<Mitre> mitres, Tolerance tolerance)
            {
                double distance = Math.Abs(sideways);

                _edges = edges;
                _closed = closed;
                _sideways = sideways;
                _distance = distance;
                _slack = slack;
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
                _mitreBuilt = new bool[mitres.Count];
                _inside = new Tolerance(slack, tolerance.EqualVector, tolerance.EqualAngleRad, tolerance.EqualPlanar);
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
                    Box first = Box.Of(CurveMeet2.Stretch(mitre.Before, mitre.Meeting, true));
                    Box second = Box.Of(CurveMeet2.Stretch(mitre.After, mitre.Meeting, false));

                    _mitreBoxes.Add(first.With(second).With(_vertices[mitre.Corner]).Grown(_slack));
                }

                return _mitreBoxes;
            }

            /// <summary>
            /// Determines whether some part of the shape comes nearer to a point than a distance.
            /// </summary>
            internal bool Nearer(GeoPoint2 point, double limit)
            {
                for (int i = 0; i < _edges.Count; i++)
                {
                    if (!_boxes[i].Grown(limit).Holds(point))
                    {
                        continue;
                    }

                    GeoPoint2 closest = Own(i).Closest(point, _tolerance);

                    if (point.DistanceTo(closest) >= limit)
                    {
                        continue;
                    }

                    int corner = closest.IsEqualTo(_edges[i].StartPoint, _tolerance) ? i
                        : closest.IsEqualTo(_edges[i].EndPoint, _tolerance) ? Next(i)
                        : -1;

                    if (corner >= 0 && !Folds(corner, point))
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }

            /// <summary>
            /// Determines whether a vertex nearer to a point than the distance folds it.
            /// </summary>
            private bool Folds(int corner, GeoPoint2 point)
            {
                if (!InCone(corner, point))
                {
                    return false;
                }

                return !_chords[corner].HasValue || !InWedge(_chords[corner].Value, corner, point) || !BeyondChord(corner, point);
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
                    return atEnd ? new GeoEdge2(old, grown.EndPoint) : new GeoEdge2(grown.StartPoint, old);
                }

                return atEnd ? after : before;
            }

            /// <summary>
            /// Determines whether a point lies inside the point of a miter, by more than the slack.
            /// </summary>
            internal bool InMitre(GeoPoint2 point)
            {
                List<Box> boxes = _mitres.Count == 0 ? _mitreBoxes : MitreBoxes();

                for (int i = 0; i < boxes.Count; i++)
                {
                    if (!boxes[i].Holds(point))
                    {
                        continue;
                    }

                    GeoPolygonArc2 loop = MitreLoop(i);

                    if (loop != null && loop.Locate(point, _inside) == PointLocation.Inside)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Gets the point of a miter as a loop: from the corner out to the end of the first piece, along it as run
            /// on to where the two meet, back along the second to its start, and home; null where it has no inside.
            /// </summary>
            private GeoPolygonArc2 MitreLoop(int index)
            {
                if (_mitreBuilt[index])
                {
                    return _mitreLoops[index];
                }

                Mitre mitre = _mitres[index];
                GeoEdge2 first = Beyond(CurveMeet2.Stretch(mitre.Before, mitre.Meeting, true), mitre.Before.EndPoint, true, _tolerance);
                GeoEdge2 second = Beyond(CurveMeet2.Stretch(mitre.After, mitre.Meeting, false), mitre.After.StartPoint, false, _tolerance);

                var starts = new[] { _vertices[mitre.Corner], first.StartPoint, second.StartPoint, second.EndPoint };
                var bulges = new[] { 0.0, first.Bulge, second.Bulge, 0.0 };

                _mitreLoops[index] = GeoPolygonArc2.TryFromEdges(starts, bulges, out GeoPolygonArc2 loop) ? loop : null;
                _mitreBuilt[index] = true;

                return _mitreLoops[index];
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
                int incoming = _closed ? (corner + _edges.Count - 1) % _edges.Count : corner - 1;
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

                for (int i = 0; i < _edges.Count; i++)
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

                for (int i = 0; i < _vertices.Length; i++)
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

            private int Next(int edge) => _closed ? (edge + 1) % _edges.Count : edge + 1;

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
