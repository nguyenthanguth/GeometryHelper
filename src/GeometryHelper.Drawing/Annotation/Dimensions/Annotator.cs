using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Dimensions a view by rule: finds the points of the view a dimension can measure to, makes the dimensions the settings
    /// switch on, and places them in tiers outside the view so that none overlaps another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules work along the view's X and along its Y alike. The main part is the object marked
    /// <see cref="DrawingObject.IsMain"/>, or every part where none is; its ends along the axis are the outermost of its
    /// corners. A plate 300 by 200 with a hole of 22 at 50 from each corner, seen from above, gets the chain 50, 200, 50 and
    /// the overall 300 along X, 50, 100, 50 and 200 along Y. A beam 3 000 long with holes at 100 and 2 900 gets the chain
    /// 100, 2800, 100 and the overall 3000.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Rule</term><description>What it gives</description></listheader>
    /// <item><term>Overall</term><description>From the main part's one end to its other, outermost.</description></item>
    /// <item><term>Chain</term><description>End to end through the ends of the main part and the centres of every hole
    /// and bolt, equal neighbours merged into "n x p = total" where the settings say so.</description></item>
    /// <item><term>Holes by group</term><description>For each row of holes or bolts of a part, square to the axis: the
    /// distance from the part's end to the first, the pitches, equal ones merged into "n x p = total", and the distance
    /// from the last to the part's other end.</description></item>
    /// <item><term>Cumulative</term><description>From the main part's first end to each point of the chain, as ordinate
    /// dimensions on one tier.</description></item>
    /// <item><term>Ordinate</term><description>From the main part's work point, the origin of its frame, to each point of
    /// the chain.</description></item>
    /// </list>
    /// <para>
    /// A dimension is never given twice: one that measures between the same places along the same axis as one already made,
    /// to the point tolerance, is left out, the overall before the chain, the chain before the holes. Each value is the
    /// distance of its model points along the axis, every digit kept; its text is rounded as the settings say.
    /// </para>
    /// <para>
    /// Placing is greedy, axis by axis, in the order chain, overall, holes, cumulative, ordinate: each takes the nearest
    /// tier where nothing it draws overlaps what is placed before it, as <see cref="FindOverlaps"/> counts it, the overall
    /// dimension beyond every tier on its side and what comes after it inside it or on the other side. A dimension that
    /// finds no place on either side is left out and named in one Debug line.
    /// </para>
    /// </remarks>
    public static class Annotator
    {
        /// <summary>
        /// Finds the points of a view a dimension can measure to: the corners, the centres of holes and bolts, and the ends of
        /// parts.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from; those with no line in it are passed over.</param>
        /// <param name="frame">The frame the view was projected in.</param>
        /// <param name="tolerance">The tolerance points are told apart within.</param>
        /// <returns>The points, in the order of their objects' ids, then of their kinds, then of X, then of Y.</returns>
        /// <remarks>
        /// <para>
        /// A corner is an end of a straight line of the view where a corner of its object's body falls, the corner nearest
        /// the viewer being its model point. A hole's centre is the centre of a whole circle of a part, its arcs about one
        /// centre adding up to a full turn, its model point on the rim nearest the viewer; a bolt's or a hole object's is the
        /// middle of its axis, its frame's X, half way between the ends of its body. A part's ends are those of its reference
        /// line, from its frame's origin along its X as far as its body reaches; the main part's are its work points.
        /// </para>
        /// <para>
        /// A plate 300 by 200 by 20 from the origin with four holes of 22 at 50 from its edges, seen from above, has the
        /// corners (0, 0) to (300, 200), the hole centres (50, 50), (250, 50), (50, 150) and (250, 150), their model points 20
        /// up, and the work points (0, 0) and (300, 0).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the view or the objects are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is not a frame of three unit axes square to each other,
        /// an object is null, or two objects have the same id.</exception>
        public static IReadOnlyList<DimensionPoint> FindPoints(ProjectedView2 view, IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            List<PointFinder.ObjectPoints> found = Find(view, objects, frame, tolerance);
            var points = new List<DimensionPoint>();

            foreach (PointFinder.ObjectPoints item in found)
            {
                var own = new List<DimensionPoint>();
                own.AddRange(item.Corners);
                own.AddRange(item.Centres);
                own.AddRange(item.Ends);
                own.Sort((a, b) =>
                {
                    int c = a.Kind.CompareTo(b.Kind);
                    return c != 0 ? c : PointFinder.CompareXY(a, b);
                });
                points.AddRange(own);
            }

            return points;
        }

        /// <summary>
        /// Dimensions a view by the rules the settings switch on, placed in tiers outside it with none overlapping another.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from; those with no line in it are passed over.</param>
        /// <param name="frame">The frame the view was projected in.</param>
        /// <param name="settings">The rules, the scale, the sizes on paper and the rounding.</param>
        /// <param name="tolerance">The tolerance points and values are told apart within.</param>
        /// <returns>The dimensions along X, then along Y, each axis in the order of the kinds, then of where they measure
        /// from and to; their ids D-1, D-2 and on in that order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view, the objects or the settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is not a frame of three unit axes square to each other,
        /// an object is null, or two objects have the same id.</exception>
        public static IReadOnlyList<DrawingDimension> Dimension(ProjectedView2 view, IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, DimensionSettings settings, Tolerance tolerance)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            List<PointFinder.ObjectPoints> found = Find(view, objects, frame, tolerance);
            double tol = tolerance.EqualPoint;
            var layout = new DimensionLayout(view.Bounds, settings, tol);
            var linear = new List<DimensionCandidate>();
            var ordinates = new List<DimensionCandidate>();

            for (int axis = 0; axis < 2 && found.Count > 0; axis++)
            {
                var rules = new Rules(found, axis, frame, settings, tol);
                rules.Make();

                // A dimension given twice is left out, the overall kept before the chain, the chain before the holes. The
                // chain and the overall dimension outside it are placed first, so that they find room; the rows of holes and
                // the cumulative and ordinate lines then go inside the overall dimension, or on the other side.
                List<DimensionCandidate> overall = Unique(rules.Overall, linear, ordinates, tol);
                List<DimensionCandidate> chain = Unique(rules.Chain, linear, ordinates, tol);
                List<List<DimensionCandidate>> holes = rules.Holes.ConvertAll(group => Unique(group, linear, ordinates, tol));
                List<DimensionCandidate> cumulative = Unique(rules.Cumulative, linear, ordinates, tol);
                List<DimensionCandidate> ordinate = Unique(rules.Ordinate, linear, ordinates, tol);

                layout.Place(chain, false);
                layout.Place(overall, true);

                foreach (List<DimensionCandidate> group in holes)
                {
                    layout.Place(group, false);
                }

                layout.Place(cumulative, false);
                layout.Place(ordinate, false);
            }

            List<DimensionCandidate> placed = layout.Placed;
            placed.Sort(ComparePlaced);
            var dimensions = new DrawingDimension[placed.Count];

            for (int i = 0; i < placed.Count; i++)
            {
                DimensionCandidate c = placed[i];
                DimensionShape shape = c.Shape;
                dimensions[i] = new DrawingDimension(
                    "D-" + (i + 1).ToString(CultureInfo.InvariantCulture),
                    c.Kind,
                    new[] { c.From, c.To },
                    c.Value,
                    c.Count,
                    c.Text,
                    c.Axis == 0 ? GeoVector2.XAxis : GeoVector2.YAxis,
                    c.Tier,
                    layout.Offset(c.Tier),
                    shape.Line,
                    shape.Extensions,
                    shape.TextBox,
                    shape.TextRotation,
                    layout.TextHeight,
                    settings.Scale,
                    settings.Rounding);
            }

            if (layout.Dropped.Count > 0)
            {
                string left = string.Join(", ", layout.Dropped.ConvertAll(c => c.Kind + " " + c.Text + (c.Axis == 0 ? " along X" : " along Y")));
                GeometryHelperLog.Debug(string.Format(CultureInfo.InvariantCulture, "Annotator: {0} dimensions found no place free of overlaps and were left out: {1}.", layout.Dropped.Count, left));
            }

            GeometryHelperLog.Info(string.Format(CultureInfo.InvariantCulture, "Annotator: {0} objects gave {1} dimensions.", found.Count, dimensions.Length));
            return dimensions;
        }

        /// <summary>
        /// Finds where dimensions overlap: a dimension line on or across another, an extension line or a leader across a
        /// dimension line, a text over a line or over another text.
        /// </summary>
        /// <param name="dimensions">The dimensions of one view.</param>
        /// <param name="tolerance">The tolerance: lines meeting at their ends within it, or overlapping by no more than it,
        /// are not counted.</param>
        /// <returns>One line of words for each pair that overlaps, in the order of the pairs; empty where none does.</returns>
        /// <remarks>
        /// Extension lines meeting extension lines are not counted: those of a chain and its overall dimension run on one
        /// another, as a draughtsman draws them. Nor is a dimension line meeting another at the end of both, as the steps of
        /// a chain meet. Each text is taken as a box 0.8 of its height wide for each character.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the dimensions are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a dimension is null.</exception>
        public static IReadOnlyList<string> FindOverlaps(IEnumerable<DrawingDimension> dimensions, Tolerance tolerance)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            var list = new List<DrawingDimension>();

            foreach (DrawingDimension dimension in dimensions)
            {
                list.Add(dimension ?? throw new ArgumentException("A dimension cannot be null.", nameof(dimensions)));
            }

            return DimensionShape.FindAll(list, tolerance.EqualPoint);
        }

        private static List<PointFinder.ObjectPoints> Find(ProjectedView2 view, IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            if (!frame.IsValid)
            {
                throw new ArgumentException("The frame has to have three unit axes square to each other.", nameof(frame));
            }

            return PointFinder.Find(view, objects, frame, tolerance);
        }

        /// <summary>
        /// The dimensions of a group not given already: a linear one measuring between the same places along the same axis
        /// as one kept, a cumulative or ordinate one from and to the same places; the others kept.
        /// </summary>
        private static List<DimensionCandidate> Unique(List<DimensionCandidate> group, List<DimensionCandidate> linear, List<DimensionCandidate> ordinates, double tol)
        {
            var kept = new List<DimensionCandidate>();

            foreach (DimensionCandidate c in group)
            {
                List<DimensionCandidate> made = c.IsOrdinate ? ordinates : linear;
                bool twice = false;

                foreach (DimensionCandidate other in made)
                {
                    if (other.Axis == c.Axis && Math.Abs(other.Low - c.Low) <= tol && Math.Abs(other.High - c.High) <= tol)
                    {
                        twice = true;
                        break;
                    }
                }

                if (!twice)
                {
                    made.Add(c);
                    kept.Add(c);
                }
            }

            return kept;
        }

        /// <summary>Orders dimensions by axis, kind, where they measure from and to, then by their tier.</summary>
        private static int ComparePlaced(DimensionCandidate a, DimensionCandidate b)
        {
            int c = a.Axis.CompareTo(b.Axis);
            c = c != 0 ? c : a.Kind.CompareTo(b.Kind);
            c = c != 0 ? c : a.Low.CompareTo(b.Low);
            c = c != 0 ? c : a.High.CompareTo(b.High);
            c = c != 0 ? c : a.Tier.CompareTo(b.Tier);
            c = c != 0 ? c : string.CompareOrdinal(a.From.ObjectId, b.From.ObjectId);
            c = c != 0 ? c : string.CompareOrdinal(a.To.ObjectId, b.To.ObjectId);
            c = c != 0 ? c : a.Across(a.From).CompareTo(b.Across(b.From));
            return c != 0 ? c : a.Across(a.To).CompareTo(b.Across(b.To));
        }

        /// <summary>The dimensions each rule asks for along one axis of the view.</summary>
        private sealed class Rules
        {
            private readonly List<PointFinder.ObjectPoints> _found;
            private readonly int _axis;
            private readonly GeoVector3 _along;
            private readonly DimensionSettings _settings;
            private readonly double _tol;

            public Rules(List<PointFinder.ObjectPoints> found, int axis, GeoCoordinateSystem3 frame, DimensionSettings settings, double tol)
            {
                _found = found;
                _axis = axis;
                _along = axis == 0 ? frame.XAxis : frame.YAxis;
                _settings = settings;
                _tol = tol;
            }

            public List<List<DimensionCandidate>> Holes { get; } = new List<List<DimensionCandidate>>();

            public List<DimensionCandidate> Chain { get; } = new List<DimensionCandidate>();

            public List<DimensionCandidate> Cumulative { get; } = new List<DimensionCandidate>();

            public List<DimensionCandidate> Ordinate { get; } = new List<DimensionCandidate>();

            public List<DimensionCandidate> Overall { get; } = new List<DimensionCandidate>();

            public void Make()
            {
                List<PointFinder.ObjectPoints> main = MainObjects();
                var mainPoints = new List<DimensionPoint>();

                foreach (PointFinder.ObjectPoints item in main)
                {
                    mainPoints.AddRange(item.Corners);
                }

                if (mainPoints.Count == 0)
                {
                    return;
                }

                mainPoints.Sort(CompareAlong);
                DimensionPoint low = mainPoints[0];
                DimensionPoint high = mainPoints.Find(p => Along(p) >= Along(mainPoints[mainPoints.Count - 1]) - _tol);

                if (Along(high) - Along(low) <= _tol)
                {
                    return;
                }

                var features = new List<DimensionPoint> { low, high };

                foreach (PointFinder.ObjectPoints item in _found)
                {
                    features.AddRange(item.Centres);
                }

                // The points a place of the chain may be taken at: any corner of the main part or centre there.
                var pool = new List<DimensionPoint>(features);
                pool.AddRange(mainPoints);
                List<DimensionPoint> steps = Distinct(features);

                if (_settings.Overall)
                {
                    Overall.Add(Linear(DimensionKind.Overall, low, high, 1, mainPoints));
                }

                if (_settings.Chain)
                {
                    AddSteps(Chain, DimensionKind.Chain, steps, 0, steps.Count - 1, _settings.MergeEqual, pool);
                }

                if (_settings.HolesByGroup)
                {
                    AddHoles(main);
                }

                if (_settings.Cumulative)
                {
                    AddOrdinates(Cumulative, DimensionKind.Cumulative, steps[0], steps, pool);
                }

                if (_settings.Ordinate)
                {
                    DimensionPoint origin = main[0].Ends.Count > 0 ? main[0].Ends[0] : steps[0];
                    AddOrdinates(Ordinate, DimensionKind.Ordinate, origin, steps, pool);
                }
            }

            /// <summary>The parts marked main; every part where none is; every object where there is no part.</summary>
            private List<PointFinder.ObjectPoints> MainObjects()
            {
                var main = _found.FindAll(o => o.Object.IsMain && o.Object.Kind == DrawingObjectKind.Part);

                if (main.Count == 0)
                {
                    main = _found.FindAll(o => o.Object.Kind == DrawingObjectKind.Part);
                }

                return main.Count > 0 ? main : new List<PointFinder.ObjectPoints>(_found);
            }

            private double Along(DimensionPoint p) => _axis == 0 ? p.ViewPoint.X : p.ViewPoint.Y;

            private double Across(DimensionPoint p) => _axis == 0 ? p.ViewPoint.Y : p.ViewPoint.X;

            /// <summary>
            /// Orders points along the axis, then across it from the side dimensions go to first, above or on the right, so
            /// that of points at one place the one nearest that side is taken and its extension line is the shortest; then by
            /// object and kind.
            /// </summary>
            private int CompareAlong(DimensionPoint a, DimensionPoint b)
            {
                int c = Along(a).CompareTo(Along(b));
                c = c != 0 ? c : Across(b).CompareTo(Across(a));
                c = c != 0 ? c : string.CompareOrdinal(a.ObjectId, b.ObjectId);
                return c != 0 ? c : a.Kind.CompareTo(b.Kind);
            }

            /// <summary>The points in their order along the axis, the first of those within the tolerance of one another kept.</summary>
            private List<DimensionPoint> Distinct(List<DimensionPoint> points)
            {
                points.Sort(CompareAlong);
                var kept = new List<DimensionPoint>();

                foreach (DimensionPoint p in points)
                {
                    if (kept.Count == 0 || Along(p) - Along(kept[kept.Count - 1]) > _tol)
                    {
                        kept.Add(p);
                    }
                }

                return kept;
            }

            /// <summary>The distance between the model points of two points along the axis.</summary>
            private double Value(DimensionPoint a, DimensionPoint b) => Math.Abs(_along.DotProduct(a.ModelPoint.GetVectorTo(b.ModelPoint)));

            /// <summary>
            /// A linear dimension between two points, each with the point of a pool at its place furthest towards either side
            /// of the view.
            /// </summary>
            private DimensionCandidate Linear(DimensionKind kind, DimensionPoint from, DimensionPoint to, int count, List<DimensionPoint> pool)
            {
                double value = Value(from, to);
                string text = count > 1
                    ? count.ToString(CultureInfo.InvariantCulture) + " x " + DrawingDimension.Format(value / count, _settings.Rounding) + " = " + DrawingDimension.Format(value, _settings.Rounding)
                    : DrawingDimension.Format(value, _settings.Rounding);
                return new DimensionCandidate(kind, _axis, _along, Toward(from, pool, 1), Toward(to, pool, 1), Toward(from, pool, -1), Toward(to, pool, -1), count, text);
            }

            /// <summary>
            /// Of the points of a pool within the tolerance of a point along the axis, the point itself among them, the one
            /// furthest across towards a side: up or right for 1, down or left for -1; the first in the order of the pool's
            /// points where two are as far.
            /// </summary>
            private DimensionPoint Toward(DimensionPoint point, List<DimensionPoint> pool, int side)
            {
                DimensionPoint best = point;

                foreach (DimensionPoint p in pool)
                {
                    if (Math.Abs(Along(p) - Along(point)) <= _tol && side * (Across(p) - Across(best)) > 0.0)
                    {
                        best = p;
                    }
                }

                return best;
            }

            /// <summary>
            /// Dimensions end to end between points first to last, runs of equal neighbours merged into one where asked.
            /// </summary>
            private void AddSteps(List<DimensionCandidate> into, DimensionKind kind, List<DimensionPoint> points, int first, int last, bool merge, List<DimensionPoint> pool)
            {
                int i = first;

                while (i < last)
                {
                    double step = Value(points[i], points[i + 1]);
                    int j = i + 1;

                    while (merge && j < last && Math.Abs(Value(points[j], points[j + 1]) - step) <= _tol)
                    {
                        j++;
                    }

                    into.Add(Linear(kind, points[i], points[j], j - i, pool));
                    i = j;
                }
            }

            /// <summary>
            /// For each part with holes or bolts, each row of them square to the axis: the distance from the part's end to the
            /// first, the pitches, and the distance from the last to the part's other end.
            /// </summary>
            private void AddHoles(List<PointFinder.ObjectPoints> main)
            {
                var byOwner = new SortedDictionary<string, List<DimensionPoint>>(StringComparer.Ordinal);
                var owners = new Dictionary<string, PointFinder.ObjectPoints>(StringComparer.Ordinal);

                foreach (PointFinder.ObjectPoints item in _found)
                {
                    foreach (DimensionPoint centre in item.Centres)
                    {
                        PointFinder.ObjectPoints owner = item.Object.Kind == DrawingObjectKind.Part ? item : OwnerOf(centre, main);

                        if (!byOwner.TryGetValue(owner.Object.Id, out List<DimensionPoint> list))
                        {
                            byOwner.Add(owner.Object.Id, list = new List<DimensionPoint>());
                            owners[owner.Object.Id] = owner;
                        }

                        list.Add(centre);
                    }
                }

                foreach (KeyValuePair<string, List<DimensionPoint>> entry in byOwner)
                {
                    PointFinder.ObjectPoints owner = owners[entry.Key];
                    var pool = new List<DimensionPoint>(entry.Value);
                    pool.AddRange(owner.Corners);
                    DimensionPoint? ownerLow = null, ownerHigh = null;

                    if (owner.Corners.Count > 0)
                    {
                        var corners = new List<DimensionPoint>(owner.Corners);
                        corners.Sort(CompareAlong);
                        ownerLow = corners[0];
                        ownerHigh = corners.Find(p => Along(p) >= Along(corners[corners.Count - 1]) - _tol);
                    }

                    foreach (List<DimensionPoint> row in Rows(entry.Value))
                    {
                        var points = new List<DimensionPoint>();

                        if (ownerLow.HasValue && Along(row[0]) - Along(ownerLow.Value) > _tol)
                        {
                            points.Add(ownerLow.Value);
                        }

                        int firstCentre = points.Count;
                        points.AddRange(row);
                        int lastCentre = points.Count - 1;

                        if (ownerHigh.HasValue && Along(ownerHigh.Value) - Along(row[row.Count - 1]) > _tol)
                        {
                            points.Add(ownerHigh.Value);
                        }

                        var group = new List<DimensionCandidate>();

                        if (firstCentre > 0)
                        {
                            group.Add(Linear(DimensionKind.HoleEdge, points[0], points[firstCentre], 1, pool));
                        }

                        AddSteps(group, DimensionKind.HoleGroup, points, firstCentre, lastCentre, true, pool);

                        if (lastCentre < points.Count - 1)
                        {
                            group.Add(Linear(DimensionKind.HoleEdge, points[lastCentre], points[lastCentre + 1], 1, pool));
                        }

                        if (group.Count > 0)
                        {
                            Holes.Add(group);
                        }
                    }
                }
            }

            /// <summary>
            /// The part a bolt's or a hole object's centre goes through: the part of the smallest box in the view that holds
            /// it, ties by id; the first main object where none holds it.
            /// </summary>
            private PointFinder.ObjectPoints OwnerOf(DimensionPoint centre, List<PointFinder.ObjectPoints> main)
            {
                PointFinder.ObjectPoints best = null;

                foreach (PointFinder.ObjectPoints item in _found)
                {
                    if (item.Object.Kind != DrawingObjectKind.Part || !item.Contains(centre.ViewPoint, _tol))
                    {
                        continue;
                    }

                    // The objects are in the order of their ids, so the first of the smallest is the one of the lowest id.
                    if (best == null || item.Area < best.Area)
                    {
                        best = item;
                    }
                }

                return best ?? main[0];
            }

            /// <summary>
            /// The centres in rows square to the axis: those across it within the tolerance of a row's first, each row in its
            /// order along the axis with centres falling on one another kept once, the rows in their order across.
            /// </summary>
            private List<List<DimensionPoint>> Rows(List<DimensionPoint> centres)
            {
                var sorted = new List<DimensionPoint>(centres);
                sorted.Sort((a, b) =>
                {
                    int c = Across(a).CompareTo(Across(b));
                    return c != 0 ? c : CompareAlong(a, b);
                });

                var rows = new List<List<DimensionPoint>>();
                var row = new List<DimensionPoint>();

                foreach (DimensionPoint p in sorted)
                {
                    if (row.Count > 0 && Across(p) - Across(row[0]) > _tol)
                    {
                        rows.Add(Distinct(row));
                        row = new List<DimensionPoint>();
                    }

                    row.Add(p);
                }

                if (row.Count > 0)
                {
                    rows.Add(Distinct(row));
                }

                return rows;
            }

            /// <summary>An ordinate dimension from an origin to the origin itself and to each point.</summary>
            private void AddOrdinates(List<DimensionCandidate> into, DimensionKind kind, DimensionPoint origin, List<DimensionPoint> points, List<DimensionPoint> pool)
            {
                var all = new List<DimensionPoint>(points);

                if (!all.Exists(p => Math.Abs(Along(p) - Along(origin)) <= _tol))
                {
                    all.Add(origin);
                    all = Distinct(all);
                }

                foreach (DimensionPoint p in all)
                {
                    // The origin stays where it is; the leader to a point starts at the point there nearest the side it goes to.
                    bool atOrigin = Math.Abs(Along(p) - Along(origin)) <= _tol;
                    DimensionPoint to = atOrigin ? origin : p;
                    string text = DrawingDimension.Format(Value(origin, to), _settings.Rounding);
                    DimensionPoint above = atOrigin ? origin : Toward(to, pool, 1);
                    DimensionPoint below = atOrigin ? origin : Toward(to, pool, -1);
                    into.Add(new DimensionCandidate(kind, _axis, _along, origin, above, origin, below, 1, text));
                }
            }
        }
    }
}
