using System;
using System.Collections.Generic;
using GeometryHelper.Arranging;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The work of <see cref="Marker"/>.Mark: which objects of a view are marked and with what text, and where each mark
    /// goes, through <see cref="Arranger"/>, in rounds until every mark is clear of the dimensions and of the others or
    /// has been tried at every row.
    /// </summary>
    internal sealed class MarkPlacer
    {
        /// <summary>
        /// The rounds of placing: first on either side, four rows, clear of the box of every object of the view, so that a
        /// text stands off the parts rather than over the face of one; then on the side of its line away from its object
        /// only, at the gap of the settings, three rows; then on either side, eight rows; then on either side at no more than
        /// half the text's height off, sixteen rows.
        /// </summary>
        private static readonly (bool OutsideOnly, bool NearGap, int Levels, bool ClearOfObjects)[] Rounds =
        {
            (false, false, 4, true), (true, false, 3, false), (false, false, 8, false), (false, true, 16, false),
        };

        private readonly List<DrawingObject> _objects;
        private readonly GeoCoordinateSystem3 _frame;
        private readonly List<DrawingDimension> _dimensions;
        private readonly double _height;
        private readonly double _gap;
        private readonly Tolerance _tolerance;
        private readonly double _tol;
        private readonly ProjectedView2 _view;
        private readonly List<GeoRectangle2> _clearBoxes;
        private readonly List<GeoLine2> _clearLines;

        // Looked up by id only, never walked, so nothing here follows its order.
        private readonly Dictionary<string, List<ProjectedEdge2>> _visible = new Dictionary<string, List<ProjectedEdge2>>(StringComparer.Ordinal);

        internal MarkPlacer(ProjectedView2 view, List<DrawingObject> objects, GeoCoordinateSystem3 frame, List<DrawingDimension> dimensions, List<GeoRectangle2> clearBoxes, List<GeoLine2> clearLines, double height, double gap, Tolerance tolerance)
        {
            _clearBoxes = clearBoxes;
            _clearLines = clearLines;
            _view = view;
            _objects = objects;
            _frame = frame;
            _dimensions = dimensions;
            _height = height;
            _gap = gap;
            _tolerance = tolerance;
            _tol = tolerance.EqualPoint;

            foreach (ProjectedEdge2 edge in view.VisibleEdges)
            {
                foreach (string id in edge.ObjectIds)
                {
                    if (!_visible.TryGetValue(id, out List<ProjectedEdge2> edges))
                    {
                        _visible.Add(id, edges = new List<ProjectedEdge2>());
                    }

                    edges.Add(edge);
                }
            }
        }

        /// <summary>An object a mark's leader may end on.</summary>
        internal sealed class Target
        {
            public DrawingObject Object;

            /// <summary>Its visible lines, in the view's order.</summary>
            public List<ProjectedEdge2> Edges;

            /// <summary>Whether it is a bolt seen along its axis, its leader then ending at <see cref="Centre"/>.</summary>
            public bool EndOn;

            public GeoPoint2 Centre;

            /// <summary>The point of the model the mark is tied to.</summary>
            public GeoPoint3 Model;
        }

        /// <summary>A mark to be placed.</summary>
        internal sealed class Candidate
        {
            public string Id;
            public MarkKind Kind;
            public string Text;
            public string[] ObjectIds;
            public List<Target> Targets;

            /// <summary>The line the text is tried beside.</summary>
            public GeoLine2 Guide;

            /// <summary>The side of <see cref="Guide"/> away from the object, or both where the object lies along it.</summary>
            public ArrangeSide Outside;
        }

        private sealed class Placed
        {
            public Candidate Candidate;
            public Target Target;
            public GeoRectangle2 Text;
            public GeoRectangle2 Room;
            public GeoPoint2 Start;
            public GeoPoint2 Anchor;
        }

        /// <summary>The marks the settings and the content ask for, of objects with a visible line, in the ordinal order of their ids.</summary>
        internal List<Candidate> Candidates(MarkSettings settings, MarkContent content)
        {
            var candidates = new List<Candidate>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (DrawingObject item in _objects)
            {
                if (item.Kind == DrawingObjectKind.Part && item.Mark.Length > 0)
                {
                    counts.TryGetValue(item.Mark, out int count);
                    counts[item.Mark] = count + 1;
                }
            }

            foreach (DrawingObject item in _objects)
            {
                if (item.Kind != DrawingObjectKind.Part || !_visible.TryGetValue(item.Id, out List<ProjectedEdge2> edges))
                {
                    continue;
                }

                MarkKind kind = item.IsMain && item.ParentId != null ? MarkKind.Assembly : MarkKind.Part;
                bool wanted = item.IsMain ? settings.MainParts : settings.Fittings;
                string text = MarkTexts.Text(kind == MarkKind.Assembly ? content.Assembly : content.Part, item, item.Mark.Length > 0 ? counts[item.Mark] : 1);

                if (!wanted || text.Length == 0)
                {
                    continue;
                }

                var target = new Target { Object = item, Edges = edges, Model = BoxCentre(item.Solid) };
                GeoLine2 guide = Longest(edges);
                candidates.Add(new Candidate
                {
                    Id = "M-" + item.Id,
                    Kind = kind,
                    Text = text,
                    ObjectIds = new[] { item.Id },
                    Targets = new List<Target> { target },
                    Guide = guide,
                    Outside = Outside(guide, edges),
                });
            }

            if (content.Bolt.Fields.Count > 0)
            {
                AddBolts(candidates, content.Bolt);
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return candidates;
        }

        /// <summary>
        /// Places the marks, round by round: each round runs <see cref="Arranger"/> on the marks not yet placed, with the
        /// dimensions and the marks placed before kept clear of, and keeps each mark that meets nothing it may not.
        /// </summary>
        internal List<DrawingMark> Place(List<Candidate> candidates, double scale, out List<string> dropped)
        {
            double thin = Math.Max(0.1 * _height, 2.0 * _tol);
            var hard = new List<GeoPolygon2>();

            foreach (DrawingDimension d in _dimensions)
            {
                hard.Add(new GeoPolygon2(d.TextBox.GetVertices()));

                if (d.DimensionLine.HasValue)
                {
                    hard.Add(Box2.Around(d.DimensionLine.Value.StartPoint, d.DimensionLine.Value.EndPoint, thin));
                }

                foreach (GeoLine2 extension in d.ExtensionLines)
                {
                    hard.Add(Box2.Around(extension.StartPoint, extension.EndPoint, thin));
                }
            }

            // The symbols of the view are kept clear of as the dimensions are: their texts and outlines, and their lines.
            foreach (GeoRectangle2 box in _clearBoxes)
            {
                hard.Add(new GeoPolygon2(box.GetVertices()));
            }

            foreach (GeoLine2 line in _clearLines)
            {
                hard.Add(Box2.Around(line.StartPoint, line.EndPoint, thin));
            }

            List<GeoLine2> lines = ViewLines(_view, _height, _tol);
            var accepted = new List<Placed>();
            List<Candidate> pending = candidates;

            for (int round = 0; round < Rounds.Length; round++)
            {
                if (pending.Count == 0)
                {
                    break;
                }

                (bool outsideOnly, bool nearGap, int levels, bool clearOfObjects) = Rounds[round];
                bool lastRound = round == Rounds.Length - 1;

                var blocks = new List<GeoPolygon2>(hard);

                if (clearOfObjects)
                {
                    blocks.AddRange(ObjectBoxes());
                }

                foreach (Placed p in accepted)
                {
                    blocks.Add(new GeoPolygon2(p.Room.GetVertices()));

                    if (p.Start.DistanceTo(p.Anchor) > 2.0 * _tol)
                    {
                        blocks.Add(Box2.Around(p.Start, p.Anchor, thin));
                    }
                }

                List<ArrangeItem> items = pending.ConvertAll(c => Item(c, outsideOnly, nearGap));

                // The blocks of every item are gathered into one set, so the first carries them all.
                items[0].BlockPolygons = blocks;
                items[0].BlockLines = lines;
                ArrangeResult[] results = Arranger.Run(items, Options(levels));
                var failed = new List<Candidate>();

                for (int i = 0; i < pending.Count; i++)
                {
                    Candidate c = pending[i];
                    GeoRectangle2 room = items[i].Box.Translate(results[i].Translation);
                    var text = new GeoRectangle2(room.Center, SymbolDxf.TextWidth(c.Text, _height), _height);
                    Placed placed = Attach(c, text, room);

                    if (!results[i].Placed && !lastRound || Clashes(placed, accepted))
                    {
                        failed.Add(c);
                    }
                    else
                    {
                        accepted.Add(placed);
                    }
                }

                pending = failed;
            }

            // What the last round could not keep is tried at its places in the order the arranging tries them, the first
            // clear of everything taken, else the first clear of all but the view's lines.
            var left = new List<Candidate>();

            foreach (Candidate c in pending)
            {
                Placed placed = Search(c, accepted, lines);

                if (placed == null)
                {
                    left.Add(c);
                }
                else
                {
                    accepted.Add(placed);
                }
            }

            dropped = left.ConvertAll(c => c.Id + " " + c.Text);
            accepted.Sort((a, b) => string.CompareOrdinal(a.Candidate.Id, b.Candidate.Id));
            return accepted.ConvertAll(p => new DrawingMark(
                p.Candidate.Id,
                p.Candidate.Kind,
                p.Target.Object.Id,
                p.Candidate.ObjectIds,
                p.Candidate.Text,
                p.Text,
                new[] { p.Start, p.Anchor },
                _height,
                scale,
                p.Target.Model));
        }

        /// <summary>
        /// The label of a mark for the arranging: its text's box with a margin of 0.4 of the height round it, so that two
        /// texts never stand closer than 0.8 of it, set on the middle of its line.
        /// </summary>
        private ArrangeItem Item(Candidate c, bool outsideOnly, bool nearGap)
        {
            // The text itself stands the gap off, its margin within it.
            double gap = Math.Max(0.0, (nearGap ? Math.Min(_gap, 0.5 * _height) : _gap) - 0.4 * _height);
            return new ArrangeItem
            {
                Box = Room(c, c.Guide.MidPoint),
                Leader = c.Guide,
                OffsetTop = gap,
                OffsetBottom = gap,
                Side = outsideOnly ? c.Outside : ArrangeSide.Both,
            };
        }

        /// <summary>The box a mark's text takes with its margin, about a point.</summary>
        private GeoRectangle2 Room(Candidate c, GeoPoint2 middle)
            => new GeoRectangle2(middle, SymbolDxf.TextWidth(c.Text, _height) + 0.8 * _height, 1.8 * _height);

        private ArrangeOptions Options(int levels) => new ArrangeOptions
        {
            RowGap = 0.5 * _height,
            PerpendicularLevels = levels,
            MinimumBoxSize = 0.0,
            MinimumMoveDistance = 0.0,
            NeighbourMargin = 2.0 * _height,
            Tolerance = _tolerance,
        };

        /// <summary>
        /// The first of a mark's places, in the order the last round tries them, whose text and margin are clear of the
        /// dimensions and the marks taken and whose leader crosses no text; of those, the first clear of the view's lines
        /// too. Null where there is none.
        /// </summary>
        private Placed Search(Candidate c, List<Placed> accepted, List<GeoLine2> lines)
        {
            (bool outsideOnly, bool nearGap, int levels, _) = Rounds[Rounds.Length - 1];
            Placed fallback = null;

            foreach (GeoPoint2 middle in Item(c, outsideOnly, nearGap).GetPlacePoints(Options(levels)))
            {
                GeoRectangle2 room = Room(c, middle);
                Placed placed = Attach(c, new GeoRectangle2(middle, SymbolDxf.TextWidth(c.Text, _height), _height), room);

                if (Marker.DimensionClash(c.Id, room, placed.Start, placed.Anchor, _dimensions, _tol, null)
                    || Marker.SymbolClash(c.Id, room, placed.Start, placed.Anchor, _clearBoxes, _clearLines, _tol, null)
                    || accepted.Exists(p => Box2.Overlap(room, p.Room, _tol))
                    || Clashes(placed, accepted))
                {
                    continue;
                }

                if (!lines.Exists(l => Box2.Crosses(placed.Text, l.StartPoint, l.EndPoint, _tol)))
                {
                    return placed;
                }

                fallback = fallback ?? placed;
            }

            return fallback;
        }

        /// <summary>
        /// The bolt marks: one for each group of bolts with a visible bolt, tried beside the line through the centres of the
        /// bolts seen along their axes, or beside the longest visible line of the bolts.
        /// </summary>
        private void AddBolts(List<Candidate> candidates, MarkFormat format)
        {
            var groups = new SortedDictionary<string, List<DrawingObject>>(StringComparer.Ordinal);

            foreach (DrawingObject item in _objects)
            {
                if (item.Kind != DrawingObjectKind.Bolt)
                {
                    continue;
                }

                string key = MarkTexts.BoltGroupKey(item);

                if (!groups.TryGetValue(key, out List<DrawingObject> group))
                {
                    groups.Add(key, group = new List<DrawingObject>());
                }

                group.Add(item);
            }

            foreach (List<DrawingObject> group in groups.Values)
            {
                var targets = new List<Target>();
                var ends = new List<Target>();
                var edges = new List<ProjectedEdge2>();

                // The bolts of a group are in the order of their ids, as the objects are.
                foreach (DrawingObject bolt in group)
                {
                    if (!_visible.TryGetValue(bolt.Id, out List<ProjectedEdge2> seen))
                    {
                        continue;
                    }

                    // Seen along its axis, and its rim seen whole round the centre: a bolt partly behind something has its
                    // leader end on what is seen of it instead.
                    GeoPoint3 model = MarkTexts.AxisCentre(bolt);
                    GeoPoint3 local = _frame.ToLocal(model);
                    var centre = new GeoPoint2(local.X, local.Y);
                    bool endOn = Math.Abs(_frame.ZAxis.DotProduct(bolt.Frame.XAxis)) >= 1.0 - _tolerance.EqualVector
                        && seen.Exists(e => e.IsCircle && e.Arc.Center.DistanceTo(centre) <= _tol);
                    var target = new Target { Object = bolt, Edges = seen, EndOn = endOn, Centre = centre, Model = model };
                    targets.Add(target);
                    edges.AddRange(seen);

                    if (endOn)
                    {
                        ends.Add(target);
                    }
                }

                string text = MarkTexts.Text(format, group[0], group.Count);

                if (targets.Count == 0 || text.Length == 0)
                {
                    continue;
                }

                GeoLine2 guide;

                if (ends.Count > 0)
                {
                    ends.Sort((a, b) =>
                    {
                        int c = a.Centre.X.CompareTo(b.Centre.X);
                        c = c != 0 ? c : a.Centre.Y.CompareTo(b.Centre.Y);
                        return c != 0 ? c : string.CompareOrdinal(a.Object.Id, b.Object.Id);
                    });

                    GeoPoint2 first = ends[0].Centre, last = ends[ends.Count - 1].Centre;
                    double radius = 0.5 * MarkTexts.AxisDiameter(ends[0].Object);
                    guide = first.DistanceTo(last) > _tol
                        ? new GeoLine2(first, last)
                        : new GeoLine2(first.X - radius, first.Y, first.X + radius, first.Y);
                    guide = AtLeast(guide);
                }
                else
                {
                    guide = Longest(edges);
                }

                var ids = group.ConvertAll(b => b.Id).ToArray();
                candidates.Add(new Candidate { Id = "M-" + group[0].Id, Kind = MarkKind.Bolt, Text = text, ObjectIds = ids, Targets = targets, Guide = guide, Outside = ends.Count > 0 ? ArrangeSide.Both : Outside(guide, edges) });
            }
        }

        /// <summary>
        /// Ties a placed text to its object: the leader's end is the point of the object nearest the middle of the text, of
        /// its visible lines or the centre of a bolt seen along its axis, the first of those as near; its start the point of
        /// the text's box nearest that end.
        /// </summary>
        private Placed Attach(Candidate c, GeoRectangle2 text, GeoRectangle2 room)
        {
            GeoPoint2 middle = text.Center;
            Target best = null;
            GeoPoint2 anchor = default;
            double nearest = double.PositiveInfinity;

            foreach (Target target in c.Targets)
            {
                if (target.EndOn)
                {
                    double d = target.Centre.DistanceTo(middle);

                    if (d < nearest)
                    {
                        nearest = d;
                        best = target;
                        anchor = target.Centre;
                    }

                    continue;
                }

                foreach (ProjectedEdge2 edge in target.Edges)
                {
                    GeoPoint2 p = edge.IsArc ? edge.Arc.GetClosestPointOnBoundary(middle, _tolerance) : edge.Line.GetClosestPointOnBoundary(middle);
                    double d = p.DistanceTo(middle);

                    if (d < nearest)
                    {
                        nearest = d;
                        best = target;
                        anchor = p;
                    }
                }
            }

            return new Placed { Candidate = c, Target = best, Text = text, Room = room, Start = Box2.Nearest(text, anchor), Anchor = anchor };
        }

        /// <summary>Whether a placed mark meets a dimension, or a mark placed before it, as Marker.FindOverlaps counts it.</summary>
        private bool Clashes(Placed placed, List<Placed> accepted)
        {
            if (Marker.DimensionClash(placed.Candidate.Id, placed.Text, placed.Start, placed.Anchor, _dimensions, _tol, null)
                || Marker.SymbolClash(placed.Candidate.Id, placed.Text, placed.Start, placed.Anchor, _clearBoxes, _clearLines, _tol, null))
            {
                return true;
            }

            foreach (Placed other in accepted)
            {
                if (Marker.MarkClash(placed.Candidate.Id, placed.Text, placed.Start, placed.Anchor, other.Candidate.Id, other.Text, other.Start, other.Anchor, _tol, null))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The box of each object's visible lines, an arc taken whole as its circle's box, in the order of the objects; none
        /// for an object with no visible line.
        /// </summary>
        private List<GeoPolygon2> ObjectBoxes()
        {
            var boxes = new List<GeoPolygon2>();

            foreach (DrawingObject item in _objects)
            {
                if (!_visible.TryGetValue(item.Id, out List<ProjectedEdge2> edges))
                {
                    continue;
                }

                double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;

                foreach (ProjectedEdge2 edge in edges)
                {
                    if (edge.IsArc)
                    {
                        GeoArc2 arc = edge.Arc;
                        x0 = Math.Min(x0, arc.Center.X - arc.Radius);
                        y0 = Math.Min(y0, arc.Center.Y - arc.Radius);
                        x1 = Math.Max(x1, arc.Center.X + arc.Radius);
                        y1 = Math.Max(y1, arc.Center.Y + arc.Radius);
                        continue;
                    }

                    foreach (GeoPoint2 p in new[] { edge.Line.StartPoint, edge.Line.EndPoint })
                    {
                        x0 = Math.Min(x0, p.X);
                        y0 = Math.Min(y0, p.Y);
                        x1 = Math.Max(x1, p.X);
                        y1 = Math.Max(y1, p.Y);
                    }
                }

                // A box seen edge on, a line, is kept clear of as a thin one.
                double half = Math.Max(0.05 * _height, _tol);
                boxes.Add(new GeoPolygon2(new[]
                {
                    new GeoPoint2(x0 - half, y0 - half), new GeoPoint2(x1 + half, y0 - half), new GeoPoint2(x1 + half, y1 + half), new GeoPoint2(x0 - half, y1 + half),
                }));
            }

            return boxes;
        }

        /// <summary>
        /// Every line of the view, seen and hidden, an arc as chords: of 45 degrees where it is no larger than the text is
        /// high, of no more than 15 degrees where it is larger.
        /// </summary>
        internal static List<GeoLine2> ViewLines(ProjectedView2 view, double height, double tol)
        {
            var lines = new List<GeoLine2>();

            foreach (IReadOnlyList<ProjectedEdge2> list in new[] { view.VisibleEdges, view.HiddenEdges })
            {
                foreach (ProjectedEdge2 edge in list)
                {
                    if (!edge.IsArc)
                    {
                        if (edge.Length > tol)
                        {
                            lines.Add(edge.Line);
                        }

                        continue;
                    }

                    GeoArc2 arc = edge.Arc;
                    // An arc no larger than the text is high is taken in eighths of a turn, its chords less than a tenth of the
                    // text's height off it; a larger one in fifteen degrees.
                    double step = arc.Radius <= height ? Math.PI / 4.0 : Math.PI / 12.0;
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweptAngle) / step));
                    GeoPoint2 previous = arc.StartPoint;

                    for (int k = 1; k <= steps; k++)
                    {
                        double angle = arc.StartAngle + arc.SweptAngle * k / steps;
                        var next = new GeoPoint2(arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));

                        if (previous.DistanceTo(next) > tol)
                        {
                            lines.Add(new GeoLine2(previous, next));
                            previous = next;
                        }
                    }
                }
            }

            return lines;
        }

        /// <summary>
        /// The longest straight line of a list, the first of those as long; with none, the chord of the largest arc, or the
        /// level diameter of a whole circle; never shorter than the text is high.
        /// </summary>
        private GeoLine2 Longest(List<ProjectedEdge2> edges)
        {
            ProjectedEdge2 straight = null, round = null;

            foreach (ProjectedEdge2 edge in edges)
            {
                if (!edge.IsArc)
                {
                    if (straight == null || edge.Length > straight.Length)
                    {
                        straight = edge;
                    }
                }
                else if (round == null || edge.Arc.Radius > round.Arc.Radius)
                {
                    round = edge;
                }
            }

            GeoLine2 guide;

            if (straight != null)
            {
                guide = straight.Line;
            }
            else
            {
                GeoArc2 arc = round.Arc;
                guide = !round.IsCircle && arc.StartPoint.DistanceTo(arc.EndPoint) > _tol
                    ? new GeoLine2(arc.StartPoint, arc.EndPoint)
                    : new GeoLine2(arc.Center.X - arc.Radius, arc.Center.Y, arc.Center.X + arc.Radius, arc.Center.Y);
            }

            return AtLeast(guide);
        }

        /// <summary>
        /// The side of a line away from the middle of the box of an object's lines, as <see cref="ArrangeItem.Side"/> names
        /// sides: the top faces up, or for an upright line the left; both where the middle lies on the line.
        /// </summary>
        private ArrangeSide Outside(GeoLine2 guide, List<ProjectedEdge2> edges)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;

            foreach (ProjectedEdge2 edge in edges)
            {
                foreach (GeoPoint2 p in new[] { edge.StartPoint, edge.EndPoint })
                {
                    x0 = Math.Min(x0, p.X);
                    y0 = Math.Min(y0, p.Y);
                    x1 = Math.Max(x1, p.X);
                    y1 = Math.Max(y1, p.Y);
                }
            }

            double length = guide.Length;
            double ux = (guide.EndPoint.X - guide.StartPoint.X) / length, uy = (guide.EndPoint.Y - guide.StartPoint.Y) / length;
            double px = -uy, py = ux;
            GeoPoint2 middle = guide.MidPoint;
            double side = (0.5 * (x0 + x1) - middle.X) * px + (0.5 * (y0 + y1) - middle.Y) * py;

            if (Math.Abs(side) <= _tol)
            {
                return ArrangeSide.Both;
            }

            // As the arranging tells the top from the bottom: up in the drawing, or to the left of a line upright within
            // the tolerance's angle.
            double sine = Math.Abs(py);
            bool upright = sine <= Math.Sin(_tolerance.EqualAngleRad) && sine < Math.Abs(px);
            bool perpendicularIsTop = upright ? px < 0.0 : py > 0.0;
            return (side > 0.0) == perpendicularIsTop ? ArrangeSide.Bottom : ArrangeSide.Top;
        }

        /// <summary>A line made as long as the text is high where it is shorter, about its middle; a point becomes a level line.</summary>
        private GeoLine2 AtLeast(GeoLine2 guide)
        {
            double length = guide.Length;

            if (length >= _height)
            {
                return guide;
            }

            GeoPoint2 middle = guide.MidPoint;
            double ux = length > _tol ? (guide.EndPoint.X - guide.StartPoint.X) / length : 1.0;
            double uy = length > _tol ? (guide.EndPoint.Y - guide.StartPoint.Y) / length : 0.0;
            double half = 0.5 * _height;
            return new GeoLine2(middle.X - ux * half, middle.Y - uy * half, middle.X + ux * half, middle.Y + uy * half);
        }

        private static GeoPoint3 BoxCentre(GeoSolid3 solid)
        {
            GeoAabb3 box = solid.GetAabb();
            return new GeoPoint3((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0, (box.Min.Z + box.Max.Z) / 2.0);
        }
    }
}
