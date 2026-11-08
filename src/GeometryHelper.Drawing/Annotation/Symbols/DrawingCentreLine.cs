using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// One centre line of a view: across a hole, a bolt or a tube seen as a circle, or through the centres of a row of them,
    /// in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hole on its own gets a cross, a level line and an upright one through its centre, each running past its rim by
    /// <see cref="SymbolSettings.CentreLineExtension"/> on paper. Holes of one part whose centres line up level, or upright,
    /// share one line from the first rim to the last: the plate of TC1, 300 by 200 with holes of 22 at 50 from its edges,
    /// seen from above at 1:5, gets two level lines from x = 24 to 276 and two upright ones from y = 24 to 176, the holes
    /// 11 in radius and the lines running 15 past them. A centre line does not change once made.
    /// </para>
    /// <para>
    /// The circles are those of the view's lines, seen or hidden, whose arcs about one centre and of one radius add up to a
    /// full turn; the largest about a centre is taken. The holes of a part are one group; bolts are grouped as
    /// <see cref="Marker"/> groups them, and hole objects by their part, <see cref="DrawingObject.ParentId"/>. A circle on the
    /// centre of one taken already, as the bolt in a hole of the plate it holds, gets no line of its own; parts are taken
    /// first, then hole objects, then bolts.
    /// </para>
    /// </remarks>
    public sealed class DrawingCentreLine
    {
        private const double FullTurn = 2.0 * Math.PI;

        private readonly string[] _objectIds;

        private DrawingCentreLine(string id, GeoLine2 line, string[] objectIds, double scale)
        {
            Id = id;
            Line = line;
            _objectIds = objectIds;
            Scale = scale;
            Links = new DrawingLinks(objectIds);
        }

        /// <summary>Gets the id of the line, unique among the centre lines of its view: CL-1, CL-2 and on.</summary>
        public string Id { get; }

        /// <summary>Gets the line, in view millimetres: level lines from left to right, upright ones from bottom to top.</summary>
        public GeoLine2 Line { get; }

        /// <summary>Gets the ids of the objects whose circles the line runs through, in ordinal order.</summary>
        public IReadOnlyList<string> ObjectIds => _objectIds;

        /// <summary>Gets the scale the line was laid out for: 20 for 1:20.</summary>
        public double Scale { get; }

        /// <summary>Gets what the line is tied to in the model: the objects whose circles it runs through.</summary>
        public DrawingLinks Links { get; }

        /// <summary>
        /// Finds the centre lines of a view: a cross over each circle of a hole, a bolt or a tube, a row of them in line sharing
        /// one line.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from.</param>
        /// <param name="settings">How far the lines run past the rims, on paper.</param>
        /// <param name="scale">The scale the view is drawn at: 20 for 1:20.</param>
        /// <param name="tolerance">The tolerance centres and radii are told apart within.</param>
        /// <returns>The level lines in the order of their Y, then of where they start; then the upright ones in the order of
        /// their X, then of where they start; their ids CL-1, CL-2 and on in that order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view, the objects or the settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an object is null or two objects have the same id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        public static IReadOnlyList<DrawingCentreLine> Find(ProjectedView2 view, IEnumerable<DrawingObject> objects, SymbolSettings settings, double scale, Tolerance tolerance)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale has to be a finite number above nought.");
            }

            double tol = tolerance.EqualPoint;
            double extension = settings.CentreLineExtension * scale;
            var byId = new Dictionary<string, DrawingObject>(StringComparer.Ordinal);

            foreach (DrawingObject item in objects)
            {
                if (item == null)
                {
                    throw new ArgumentException("An object of a view cannot be null.", nameof(objects));
                }

                if (byId.ContainsKey(item.Id))
                {
                    throw new ArgumentException("The id \"" + item.Id + "\" is given to two objects.", nameof(objects));
                }

                byId.Add(item.Id, item);
            }

            // The arcs of each object that can have a centre line, by the order of the objects' ids.
            var arcsOf = new SortedDictionary<string, List<GeoArc2>>(StringComparer.Ordinal);

            foreach (IReadOnlyList<ProjectedEdge2> list in new[] { view.VisibleEdges, view.HiddenEdges })
            {
                foreach (ProjectedEdge2 edge in list)
                {
                    if (!edge.IsArc)
                    {
                        continue;
                    }

                    foreach (string id in edge.ObjectIds)
                    {
                        if (!byId.TryGetValue(id, out DrawingObject item) || item.Kind == DrawingObjectKind.Weld || item.Kind == DrawingObjectKind.Rebar)
                        {
                            continue;
                        }

                        if (!arcsOf.TryGetValue(id, out List<GeoArc2> arcs))
                        {
                            arcsOf.Add(id, arcs = new List<GeoArc2>());
                        }

                        arcs.Add(edge.Arc);
                    }
                }
            }

            // The circles of each group: the parts first, then the hole objects, then the bolts, each by its key.
            var groups = new SortedDictionary<string, List<Circle>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, List<GeoArc2>> entry in arcsOf)
            {
                DrawingObject item = byId[entry.Key];
                string key = item.Kind == DrawingObjectKind.Part ? "0\u0001" + item.Id
                    : item.Kind == DrawingObjectKind.Hole ? "1\u0001" + (item.ParentId ?? item.Id)
                    : "2\u0001" + MarkTexts.BoltGroupKey(item);

                if (!groups.TryGetValue(key, out List<Circle> circles))
                {
                    groups.Add(key, circles = new List<Circle>());
                }

                circles.AddRange(Circles(item.Id, entry.Value, tol));
            }

            var taken = new List<Circle>();
            var level = new List<Pending>();
            var upright = new List<Pending>();

            foreach (List<Circle> group in groups.Values)
            {
                var own = new List<Circle>();

                foreach (Circle circle in Largest(group, tol))
                {
                    if (!taken.Exists(t => Math.Abs(t.X - circle.X) <= tol && Math.Abs(t.Y - circle.Y) <= tol))
                    {
                        own.Add(circle);
                    }
                }

                taken.AddRange(own);
                Rows(own, true, extension, tol, level);
                Rows(own, false, extension, tol, upright);
            }

            level.Sort(Compare);
            upright.Sort(Compare);
            var lines = new List<DrawingCentreLine>();

            foreach (Pending p in level)
            {
                lines.Add(new DrawingCentreLine("CL-" + (lines.Count + 1).ToString(CultureInfo.InvariantCulture), new GeoLine2(p.From, p.Across, p.To, p.Across), p.Ids, scale));
            }

            foreach (Pending p in upright)
            {
                lines.Add(new DrawingCentreLine("CL-" + (lines.Count + 1).ToString(CultureInfo.InvariantCulture), new GeoLine2(p.Across, p.From, p.Across, p.To), p.Ids, scale));
            }

            GeometryHelperLog.Info(string.Format(CultureInfo.InvariantCulture, "DrawingCentreLine.Find: {0} circles gave {1} centre lines.", taken.Count, lines.Count));
            return lines;
        }

        /// <summary>
        /// Writes the line into the model space of a drawing as a LINE on the layer <see cref="ProjectedView2.CentreLayer"/>,
        /// with the linetype CENTER by layer.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// The layer and the linetype are made as <see cref="ProjectedView2.WriteTo"/> makes them where the drawing has none
        /// of their names, Centre in colour 1, CENTER a dash of 31.75, a gap, a dot and a gap of 6.35. The line's linetype
        /// scale is a tenth on paper, so that a cross of 8 on paper shows its dash and dot: a tenth where the view is drawn at
        /// its scale, 2 for a view at 1:20 drawn at 1:1. It is tagged with <see cref="DrawingEntityTags"/>: the line's id
        /// and the placement's view id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            SymbolDxf.CentreLayer(database);
            DxfLine line = SymbolDxf.Line(database, placement, Line.StartPoint, Line.EndPoint, ProjectedView2.CentreLayer, Id);
            line.LinetypeScale = 0.1 * Scale / placement.Scale;
        }

        /// <inheritdoc/>
        public override string ToString() => $"Centre line {Id} ({Line.StartPoint.X:0.###}, {Line.StartPoint.Y:0.###})-({Line.EndPoint.X:0.###}, {Line.EndPoint.Y:0.###})";

        /// <summary>
        /// The whole circles among the arcs of an object: arcs about one centre and of one radius adding up to a full turn.
        /// </summary>
        private static List<Circle> Circles(string id, List<GeoArc2> arcs, double tol)
        {
            arcs.Sort((a, b) =>
            {
                int c = a.Center.X.CompareTo(b.Center.X);
                c = c != 0 ? c : a.Center.Y.CompareTo(b.Center.Y);
                c = c != 0 ? c : a.Radius.CompareTo(b.Radius);
                c = c != 0 ? c : a.StartAngle.CompareTo(b.StartAngle);
                return c != 0 ? c : a.SweptAngle.CompareTo(b.SweptAngle);
            });

            var used = new bool[arcs.Count];
            var circles = new List<Circle>();

            for (int i = 0; i < arcs.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                double turn = 0.0;

                for (int j = i; j < arcs.Count && arcs[j].Center.X <= arcs[i].Center.X + tol; j++)
                {
                    if (!used[j] && Math.Abs(arcs[j].Center.Y - arcs[i].Center.Y) <= tol && Math.Abs(arcs[j].Radius - arcs[i].Radius) <= tol)
                    {
                        used[j] = true;
                        turn += Math.Abs(arcs[j].SweptAngle);
                    }
                }

                if (turn >= FullTurn * (1.0 - 1E-9))
                {
                    circles.Add(new Circle { Id = id, X = arcs[i].Center.X, Y = arcs[i].Center.Y, Radius = arcs[i].Radius });
                }
            }

            return circles;
        }

        /// <summary>The largest of the circles about each centre of a group, in the order of X, then Y, then id.</summary>
        private static List<Circle> Largest(List<Circle> circles, double tol)
        {
            circles.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                c = c != 0 ? c : a.Y.CompareTo(b.Y);
                c = c != 0 ? c : b.Radius.CompareTo(a.Radius);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });

            var kept = new List<Circle>();

            foreach (Circle circle in circles)
            {
                int same = kept.FindIndex(k => Math.Abs(k.X - circle.X) <= tol && Math.Abs(k.Y - circle.Y) <= tol);

                if (same < 0)
                {
                    kept.Add(circle);
                }
                else if (circle.Radius > kept[same].Radius)
                {
                    kept[same] = new Circle { Id = kept[same].Id + "\u0001" + circle.Id, X = kept[same].X, Y = kept[same].Y, Radius = circle.Radius };
                }
                else if (!string.Equals(kept[same].Id, circle.Id, StringComparison.Ordinal))
                {
                    kept[same] = new Circle { Id = kept[same].Id + "\u0001" + circle.Id, X = kept[same].X, Y = kept[same].Y, Radius = kept[same].Radius };
                }
            }

            return kept;
        }

        /// <summary>
        /// The rows of a group's circles, level or upright: circles whose centres are within the tolerance across share a line
        /// from the first rim to the last, run on past each by the extension.
        /// </summary>
        private static void Rows(List<Circle> circles, bool levelRows, double extension, double tol, List<Pending> rows)
        {
            var sorted = new List<Circle>(circles);
            sorted.Sort((a, b) =>
            {
                int c = levelRows ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                c = c != 0 ? c : levelRows ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });

            int start = 0;

            while (start < sorted.Count)
            {
                double across = levelRows ? sorted[start].Y : sorted[start].X;
                int end = start + 1;

                while (end < sorted.Count && (levelRows ? sorted[end].Y : sorted[end].X) - across <= tol)
                {
                    end++;
                }

                double from = double.PositiveInfinity, to = double.NegativeInfinity;
                var ids = new SortedSet<string>(StringComparer.Ordinal);

                for (int k = start; k < end; k++)
                {
                    double along = levelRows ? sorted[k].X : sorted[k].Y;
                    from = Math.Min(from, along - sorted[k].Radius - extension);
                    to = Math.Max(to, along + sorted[k].Radius + extension);

                    foreach (string id in sorted[k].Id.Split('\u0001'))
                    {
                        ids.Add(id);
                    }
                }

                rows.Add(new Pending { Across = across, From = from, To = to, Ids = new List<string>(ids).ToArray() });
                start = end;
            }
        }

        /// <summary>Orders rows by where they lie across, then where they start and end, then their objects' ids.</summary>
        private static int Compare(Pending a, Pending b)
        {
            int c = a.Across.CompareTo(b.Across);
            c = c != 0 ? c : a.From.CompareTo(b.From);
            c = c != 0 ? c : a.To.CompareTo(b.To);
            return c != 0 ? c : string.CompareOrdinal(string.Join("\u0001", a.Ids), string.Join("\u0001", b.Ids));
        }

        private sealed class Circle
        {
            public string Id;
            public double X;
            public double Y;
            public double Radius;
        }

        private sealed class Pending
        {
            public double Across;
            public double From;
            public double To;
            public string[] Ids;
        }
    }
}
