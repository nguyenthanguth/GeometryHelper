using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A loop HATCH or BOUNDARY found: its corners with the bulges of the edges leaving them, or a curve of its own (an ellipse,
    /// a spline) where an object selected is one; the objects it was made of; how deep it lies among the loops found (nought the
    /// outer loop, 1 an island in it, 2 an island in that).
    /// </summary>
    public sealed class CadBoundaryLoop
    {
        internal CadBoundaryLoop(
            IReadOnlyList<GeoPoint2> vertices,
            IReadOnlyList<double> bulges,
            object curve,
            IReadOnlyList<GeoPoint2> outline,
            IReadOnlyList<string> sources,
            double area
        )
        {
            Vertices = vertices;
            Bulges = bulges;
            Curve = curve;
            Outline = outline;
            Sources = sources;
            Area = area;
        }

        /// <summary>Gets the corners, in the world, the first not repeated at the end; none for a loop of a curve.</summary>
        public IReadOnlyList<GeoPoint2> Vertices { get; }

        /// <summary>Gets the bulge of the edge leaving each corner: nought a line, 1 a half circle counter-clockwise.</summary>
        public IReadOnlyList<double> Bulges { get; }

        /// <summary>Gets the curve of a loop that is one object selected, a <see cref="GeoEllipse3"/> or a <see cref="GeoSpline3"/>; null for a loop of corners.</summary>
        public object Curve { get; }

        /// <summary>Gets the handles of the objects the loop was made of, each once, in the order the loop meets them.</summary>
        public IReadOnlyList<string> Sources { get; }

        /// <summary>Gets the area the loop closes, exactly on its arcs; for a loop of a curve, on the points it is drawn through.</summary>
        public double Area { get; }

        /// <summary>Gets how deep the loop lies: nought the outer loop, 1 an island in it, 2 an island in an island.</summary>
        public int Depth { get; internal set; }

        /// <summary>The points the loop is drawn through, for testing what lies inside it.</summary>
        internal IReadOnlyList<GeoPoint2> Outline { get; }

        internal bool Contains(GeoPoint2 point) => CadBoundaryFinder.Inside(Outline, point);
    }

    /// <summary>
    /// Finds the areas HATCH and BOUNDARY fill, as AutoCAD finds them: round a point picked, the smallest area the lines,
    /// arcs, circles, polylines, ellipses and splines of a space close about it, with the islands inside it; or the loops of
    /// closed objects selected. A square 100 by 100 of four LINEs with a square 40 by 40 inside it, picked between the two,
    /// gives the outer square and the inner one as an island; picked inside the inner, the inner alone.
    /// </summary>
    /// <remarks>
    /// The objects are laid out as a planar arrangement, as the library has no public region finder: every crossing and
    /// every end touching another object becomes a corner, pieces on top of each other are kept once, and pieces that end
    /// nowhere are dropped, as AutoCAD passes over a line that closes nothing. The faces are traced turning as far right as
    /// each corner allows; a face round the point is a candidate, the smallest wins. Each other group of connected objects
    /// lying inside it is an island, its outline the loop. Corners are one within the point tolerance. Ellipses and splines
    /// are drawn through 64 points. Objects of blocks, texts and hatches are not boundaries.
    /// </remarks>
    public static class CadBoundaryFinder
    {
        private const int CurvePoints = 64;

        /// <summary>
        /// Finds the area round a point: its outer loop and every island inside it, each with its depth; null where no closed
        /// area is round the point, as AutoCAD says "Valid hatch boundary not found.".
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space whose objects are looked at.</param>
        /// <param name="point">The point picked, in the world.</param>
        /// <param name="tolerance">The tolerance: points within its point tolerance are one corner.</param>
        /// <param name="within">The part of the drawing looked at, as AutoCAD looks at what is shown; null for all of it.</param>
        /// <param name="candidates">The handles of the objects looked at, as HATCH's Boundary set holds them; null for every object of the space.</param>
        /// <param name="gap">The widest gap between two loose ends that is taken as closed, as HPGAPTOL holds it: two open ends no
        /// further apart than this are joined by a straight edge, which belongs to no object; 0 for none.</param>
        /// <returns>The loops, the outer first and then the islands by depth; or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the space is null.</exception>
        public static IReadOnlyList<CadBoundaryLoop> Find(
            CadDocument document,
            CadSpace space,
            GeoPoint2 point,
            Tolerance tolerance,
            DisplayBox? within = null,
            IReadOnlyCollection<string> candidates = null,
            double gap = 0.0
        ) => Find(document, space, point, tolerance, within, candidates, gap, 0.0);

        /// <summary>
        /// As <see cref="Find(CadDocument, CadSpace, GeoPoint2, Tolerance, DisplayBox?, IReadOnlyCollection{string}, double)"/>
        /// with the size of a pixel of the view the gap is closed in, as AutoCAD 2021 closes it: a loose end goes on along its
        /// own way and meets a piece or the way of another end where the two reach no further than twice the gap and a pixel,
        /// the one that goes the shorter way no further than the gap and half a pixel; two ends facing each other are joined
        /// where they stand no further apart than that. (A line ending 3 off a bar closes at a gap of 1.4 and not 1.35 in a
        /// view 140 high on a screen 534 pixels high, where a pixel is 0.262.)
        /// </summary>
        internal static IReadOnlyList<CadBoundaryLoop> Find(
            CadDocument document,
            CadSpace space,
            GeoPoint2 point,
            Tolerance tolerance,
            DisplayBox? within,
            IReadOnlyCollection<string> candidates,
            double gap,
            double pixel
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            double tol = Math.Max(tolerance.EqualPoint, 1E-9);
            List<Raw> raws = Gather(document, space, within, tol, candidates);
            var graph = new Graph(tol);
            graph.Build(raws, gap, pixel);
            List<Cycle> cycles = graph.Trace();
            Cycle outer = null;

            foreach (Cycle cycle in cycles)
            {
                if (
                    cycle.Area > tol * tol
                    && Inside(cycle.Outline, point)
                    && (outer == null || cycle.Area < outer.Area)
                )
                {
                    outer = cycle;
                }
            }

            if (outer == null)
            {
                return null;
            }

            var loops = new List<CadBoundaryLoop> { outer.ToLoop(false) };
            var islands = new List<CadBoundaryLoop>();

            foreach (Cycle cycle in cycles)
            {
                // The outline of each other group of connected objects that lies inside the outer loop is an island.
                if (
                    cycle.Area < -tol * tol
                    && cycle.Component != outer.Component
                    && Inside(outer.Outline, cycle.Outline[0])
                )
                {
                    islands.Add(cycle.ToLoop(true));
                }
            }

            Nest(loops[0], islands);
            loops.AddRange(
                islands
                    .OrderBy(l => l.Depth)
                    .ThenByDescending(l => l.Area)
                    .ThenBy(l => l.Outline[0].X)
                    .ThenBy(l => l.Outline[0].Y)
            );
            return loops.AsReadOnly();
        }

        /// <summary>
        /// Makes the loops of closed objects selected, as HATCH's Select objects takes them: a circle, a closed polyline, a whole
        /// ellipse or a closed spline each a loop, the depth of each the count of the others round it; the others passed over
        /// and counted.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the objects.</param>
        /// <param name="tolerance">The tolerance a closed polyline is read within.</param>
        /// <param name="passed">How many objects were not closed, or could not be read.</param>
        /// <returns>The loops, the outermost first.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
        public static IReadOnlyList<CadBoundaryLoop> FromObjects(
            CadDocument document,
            IEnumerable<string> handles,
            Tolerance tolerance,
            out int passed
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var loops = new List<CadBoundaryLoop>();
            passed = 0;

            foreach (string handle in handles)
            {
                CadBoundaryLoop loop = null;

                try
                {
                    loop = LoopOf(document.FindEntity(handle), tolerance);
                }
                catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
                {
                    loop = null;
                }

                if (loop == null)
                {
                    passed++;
                }
                else
                {
                    loops.Add(loop);
                }
            }

            foreach (CadBoundaryLoop loop in loops)
            {
                loop.Depth = loops.Count(o => o != loop && o.Area > loop.Area && o.Contains(loop.Outline[0]));
            }

            return loops
                .OrderBy(l => l.Depth)
                .ThenByDescending(l => l.Area)
                .ThenBy(l => l.Outline[0].X)
                .ThenBy(l => l.Outline[0].Y)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>The loops a hatch of an island style keeps: all for Normal, the outer and the islands just inside it for Outer, the outer alone for Ignore.</summary>
        /// <param name="loops">The loops, by depth.</param>
        /// <param name="style">The style.</param>
        /// <returns>The loops kept.</returns>
        public static IReadOnlyList<CadBoundaryLoop> Kept(IEnumerable<CadBoundaryLoop> loops, DxfHatchStyle style)
        {
            int deepest =
                style == DxfHatchStyle.Ignore ? 0
                : style == DxfHatchStyle.Outer ? 1
                : int.MaxValue;
            return (loops ?? Enumerable.Empty<CadBoundaryLoop>()).Where(l => l.Depth <= deepest).ToList().AsReadOnly();
        }

        /// <summary>Whether a point lies inside a closed run of points, by the even-odd rule.</summary>
        internal static bool Inside(IReadOnlyList<GeoPoint2> outline, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                GeoPoint2 a = outline[i];
                GeoPoint2 b = outline[j];

                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Gives each island its depth: 1 and the count of the other islands round it.</summary>
        private static void Nest(CadBoundaryLoop outer, List<CadBoundaryLoop> islands)
        {
            foreach (CadBoundaryLoop island in islands)
            {
                island.Depth =
                    1 + islands.Count(o => o != island && o.Area > island.Area && o.Contains(island.Outline[0]));
            }
        }

        /// <summary>The loop of a closed object selected; null for one that is not closed.</summary>
        private static CadBoundaryLoop LoopOf(DxfEntity entity, Tolerance tolerance)
        {
            switch (entity)
            {
                case DxfCircle circle:
                    GeoCircle2 c = circle.ToGeoCircle2(tolerance);
                    var start = new GeoPoint2(c.Center.X - c.Radius, c.Center.Y);
                    var end = new GeoPoint2(c.Center.X + c.Radius, c.Center.Y);
                    return Corners(new[] { start, end }, new[] { 1.0, 1.0 }, new[] { entity.Handle });
                case DxfPolyline polyline when polyline.Closed:
                    GeoPolygonArc2 loop = polyline.ToGeoPolygonArc2(tolerance);
                    return Corners(loop.Vertices, loop.Bulges, new[] { entity.Handle });
                case DxfPolyline2d old when old.Closed:
                    GeoPolygonArc2 chain = old.ToGeoPolygonArc2(tolerance);
                    return Corners(chain.Vertices, chain.Bulges, new[] { entity.Handle });
                case DxfEllipse ellipse when ellipse.Closed:
                    GeoEllipse2 flat = ellipse.ToGeoEllipse2(tolerance);
                    var points = new GeoPoint2[CurvePoints];

                    for (int i = 0; i < CurvePoints; i++)
                    {
                        points[i] = flat.GetPointAtParameter(2.0 * Math.PI * i / CurvePoints);
                    }

                    return new CadBoundaryLoop(
                        Array.Empty<GeoPoint2>(),
                        Array.Empty<double>(),
                        ellipse.ToGeoEllipse3(),
                        points,
                        new[] { entity.Handle },
                        Math.Abs(Signed(points, null))
                    );
                case DxfSpline spline when spline.Closed:
                    GeoSpline2 curve = spline.ToGeoSpline2(tolerance);
                    var through = new GeoPoint2[CurvePoints];

                    for (int i = 0; i < CurvePoints; i++)
                    {
                        through[i] = curve.GetPointAtParameter(
                            curve.DomainStart + (curve.DomainEnd - curve.DomainStart) * i / CurvePoints
                        );
                    }

                    return new CadBoundaryLoop(
                        Array.Empty<GeoPoint2>(),
                        Array.Empty<double>(),
                        spline.ToGeoSpline3(),
                        through,
                        new[] { entity.Handle },
                        Math.Abs(Signed(through, null))
                    );
                default:
                    return null;
            }
        }

        /// <summary>A loop of corners and bulges, its outline drawn through its arcs and its area worked out on them.</summary>
        internal static CadBoundaryLoop Corners(
            IReadOnlyList<GeoPoint2> vertices,
            IReadOnlyList<double> bulges,
            IReadOnlyList<string> sources
        )
        {
            var outline = new List<GeoPoint2>();

            for (int i = 0; i < vertices.Count; i++)
            {
                GeoPoint2 a = vertices[i];
                GeoPoint2 b = vertices[(i + 1) % vertices.Count];
                outline.Add(a);

                if (
                    bulges[i] != 0.0
                    && Arc(a, b, bulges[i], out GeoPoint2 centre, out double radius, out double from, out double sweep)
                )
                {
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 32.0)));

                    for (int k = 1; k < steps; k++)
                    {
                        double angle = from + sweep * k / steps;
                        outline.Add(
                            new GeoPoint2(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle))
                        );
                    }
                }
            }

            return new CadBoundaryLoop(
                vertices.ToList().AsReadOnly(),
                bulges.ToList().AsReadOnly(),
                null,
                outline.AsReadOnly(),
                sources,
                Math.Abs(Signed(vertices, bulges))
            );
        }

        /// <summary>The area a loop of corners closes, positive counter-clockwise, its arcs' segments added: bulges null for straight edges.</summary>
        internal static double Signed(IReadOnlyList<GeoPoint2> vertices, IReadOnlyList<double> bulges)
        {
            double area = 0.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                GeoPoint2 a = vertices[i];
                GeoPoint2 b = vertices[(i + 1) % vertices.Count];
                area += (a.X * b.Y - b.X * a.Y) / 2.0;

                if (
                    bulges != null
                    && bulges[i] != 0.0
                    && Arc(a, b, bulges[i], out _, out double radius, out _, out double sweep)
                )
                {
                    area += radius * radius * (sweep - Math.Sin(sweep)) / 2.0;
                }
            }

            return area;
        }

        /// <summary>The circle of an edge of a bulge: its centre, radius, the angle it starts at and the angle it sweeps, positive counter-clockwise.</summary>
        internal static bool Arc(
            GeoPoint2 a,
            GeoPoint2 b,
            double bulge,
            out GeoPoint2 centre,
            out double radius,
            out double from,
            out double sweep
        )
        {
            sweep = 4.0 * Math.Atan(bulge);
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            centre = default;
            radius = 0.0;
            from = 0.0;

            if (chord == 0.0 || bulge == 0.0)
            {
                return false;
            }

            // The centre lies off the middle of the chord, to its left for a sweep counter-clockwise below half a turn.
            double off = chord / (2.0 * Math.Tan(sweep / 2.0));
            centre = new GeoPoint2((a.X + b.X) / 2.0 - dy / chord * off, (a.Y + b.Y) / 2.0 + dx / chord * off);
            radius = Math.Abs(chord / (2.0 * Math.Sin(sweep / 2.0)));
            from = Math.Atan2(a.Y - centre.Y, a.X - centre.X);
            return true;
        }

        /// <summary>The edges of the objects of a space that may close an area: those of visible layers, within the part looked at.</summary>
        private static List<Raw> Gather(
            CadDocument document,
            CadSpace space,
            DisplayBox? within,
            double tol,
            IReadOnlyCollection<string> candidates
        )
        {
            var hidden = new HashSet<string>(
                document.Layers.Where(l => !l.IsOn || l.IsFrozen).Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase
            );
            HashSet<string> only =
                candidates == null ? null : new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
            var raws = new List<Raw>();

            foreach (DxfEntity entity in space.Block)
            {
                if (hidden.Contains(entity.Layer ?? "0") || (only != null && !only.Contains(entity.Handle)))
                {
                    continue;
                }

                int before = raws.Count;

                try
                {
                    AddEdges(entity, raws);
                }
                catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
                {
                    // An object off the floor, or one that cannot be read, closes nothing here.
                    raws.RemoveRange(before, raws.Count - before);
                }

                if (within.HasValue && within.Value.Width > 0.0 && within.Value.Height > 0.0)
                {
                    DisplayBox box = within.Value;

                    for (int i = raws.Count - 1; i >= before; i--)
                    {
                        Raw raw = raws[i];

                        if (
                            raw.MaxX < box.MinX - tol
                            || raw.MinX > box.MaxX + tol
                            || raw.MaxY < box.MinY - tol
                            || raw.MinY > box.MaxY + tol
                        )
                        {
                            raws.RemoveAt(i);
                        }
                    }
                }
            }

            return raws;
        }

        private static void AddEdges(DxfEntity entity, List<Raw> raws)
        {
            string handle = entity.Handle;

            switch (entity)
            {
                case DxfLine line:
                    GeoLine2 l = line.ToGeoLine2();
                    raws.Add(new Raw(l.StartPoint, l.EndPoint, 0.0, handle));
                    break;
                case DxfArc arc:
                    GeoArc2 a = arc.ToGeoArc2();
                    AddArc(a.StartPoint, a.EndPoint, a.Center, a.Radius, a.StartAngle, a.SweptAngle, handle, raws);
                    break;
                case DxfCircle circle:
                    GeoCircle2 c = circle.ToGeoCircle2();
                    var left = new GeoPoint2(c.Center.X - c.Radius, c.Center.Y);
                    var right = new GeoPoint2(c.Center.X + c.Radius, c.Center.Y);
                    raws.Add(new Raw(left, right, 1.0, handle));
                    raws.Add(new Raw(right, left, 1.0, handle));
                    break;
                case DxfPolyline polyline:
                    AddChain(
                        polyline.Closed
                            ? (IEnumerable<GeoEdge2>)polyline.ToGeoPolygonArc2().GetEdges()
                            : polyline.ToGeoPolylineArc2().GetEdges(),
                        handle,
                        raws
                    );
                    break;
                case DxfPolyline2d old:
                    AddChain(
                        old.Closed
                            ? (IEnumerable<GeoEdge2>)old.ToGeoPolygonArc2().GetEdges()
                            : old.ToGeoPolylineArc2().GetEdges(),
                        handle,
                        raws
                    );
                    break;
                case DxfEllipse ellipse:
                    if (ellipse.Closed)
                    {
                        GeoEllipse2 whole = ellipse.ToGeoEllipse2();
                        AddPoints(i => whole.GetPointAtParameter(2.0 * Math.PI * i / CurvePoints), handle, raws);
                    }
                    else
                    {
                        GeoSpline2 part = ellipse.ToGeoSpline2();
                        AddPoints(
                            i =>
                                part.GetPointAtParameter(
                                    part.DomainStart + (part.DomainEnd - part.DomainStart) * i / CurvePoints
                                ),
                            handle,
                            raws
                        );
                    }

                    break;
                case DxfSpline spline:
                    GeoSpline2 s = spline.ToGeoSpline2();
                    AddPoints(
                        i => s.GetPointAtParameter(s.DomainStart + (s.DomainEnd - s.DomainStart) * i / CurvePoints),
                        handle,
                        raws
                    );
                    break;
            }
        }

        private static void AddArc(
            GeoPoint2 start,
            GeoPoint2 end,
            GeoPoint2 centre,
            double radius,
            double from,
            double sweep,
            string handle,
            List<Raw> raws
        )
        {
            // An arc of more than half a turn is cut in two, so that each piece's tangent and bulge stay plain.
            if (Math.Abs(sweep) > Math.PI)
            {
                double half = from + sweep / 2.0;
                var middle = new GeoPoint2(centre.X + radius * Math.Cos(half), centre.Y + radius * Math.Sin(half));
                raws.Add(new Raw(start, middle, Math.Tan(sweep / 8.0), handle));
                raws.Add(new Raw(middle, end, Math.Tan(sweep / 8.0), handle));
                return;
            }

            raws.Add(new Raw(start, end, Math.Tan(sweep / 4.0), handle));
        }

        private static void AddChain(IEnumerable<GeoEdge2> edges, string handle, List<Raw> raws)
        {
            foreach (GeoEdge2 edge in edges)
            {
                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    AddArc(
                        edge.StartPoint,
                        edge.EndPoint,
                        arc.Center,
                        arc.Radius,
                        arc.StartAngle,
                        arc.SweptAngle,
                        handle,
                        raws
                    );
                }
                else
                {
                    raws.Add(new Raw(edge.StartPoint, edge.EndPoint, 0.0, handle));
                }
            }
        }

        private static void AddPoints(Func<int, GeoPoint2> at, string handle, List<Raw> raws)
        {
            GeoPoint2 last = at(0);

            for (int i = 1; i <= CurvePoints; i++)
            {
                GeoPoint2 next = at(i);
                raws.Add(new Raw(last, next, 0.0, handle));
                last = next;
            }
        }

        /// <summary>An edge of an object: a line, or an arc of a bulge of half a turn at most; and where it is cut.</summary>
        private sealed class Raw
        {
            public Raw(GeoPoint2 start, GeoPoint2 end, double bulge, string source)
            {
                Start = start;
                End = end;
                Bulge = bulge;
                Source = source;
                GeoPoint2 centre = default;
                double radius = 0.0;
                double from = 0.0;
                double sweep = 0.0;
                IsArc = bulge != 0.0 && Arc(start, end, bulge, out centre, out radius, out from, out sweep);
                Centre = centre;
                Radius = radius;
                From = from;
                Sweep = sweep;

                // The box of the arc's chord, widened by how far it bows: enough to find its neighbours.
                double bow = IsArc ? Math.Abs(bulge) * start.DistanceTo(end) / 2.0 : 0.0;
                MinX = Math.Min(start.X, end.X) - bow;
                MaxX = Math.Max(start.X, end.X) + bow;
                MinY = Math.Min(start.Y, end.Y) - bow;
                MaxY = Math.Max(start.Y, end.Y) + bow;
            }

            public GeoPoint2 Start { get; }

            public GeoPoint2 End { get; }

            public double Bulge { get; }

            public string Source { get; }

            public bool IsArc { get; }

            public GeoPoint2 Centre { get; }

            public double Radius { get; }

            public double From { get; }

            public double Sweep { get; }

            public double MinX { get; }

            public double MaxX { get; }

            public double MinY { get; }

            public double MaxY { get; }

            public List<KeyValuePair<double, GeoPoint2>> Cuts { get; } = new List<KeyValuePair<double, GeoPoint2>>();

            public GeoEdge2 Edge => new GeoEdge2(Start, End, Bulge);

            /// <summary>The point at a parameter from nought at the start to 1 at the end.</summary>
            public GeoPoint2 At(double t)
            {
                if (!IsArc)
                {
                    return new GeoPoint2(Start.X + (End.X - Start.X) * t, Start.Y + (End.Y - Start.Y) * t);
                }

                double angle = From + Sweep * t;
                return new GeoPoint2(Centre.X + Radius * Math.Cos(angle), Centre.Y + Radius * Math.Sin(angle));
            }

            /// <summary>The parameter of the point of the edge nearest a point, and how far that is.</summary>
            public double Nearest(GeoPoint2 point, out double distance)
            {
                double t;

                if (!IsArc)
                {
                    double dx = End.X - Start.X;
                    double dy = End.Y - Start.Y;
                    double length = dx * dx + dy * dy;
                    t =
                        length == 0.0
                            ? 0.0
                            : Math.Max(
                                0.0,
                                Math.Min(1.0, ((point.X - Start.X) * dx + (point.Y - Start.Y) * dy) / length)
                            );
                }
                else
                {
                    double angle = Math.Atan2(point.Y - Centre.Y, point.X - Centre.X);
                    double turn = Sweep > 0.0 ? angle - From : From - angle;
                    turn = turn % (2.0 * Math.PI);
                    turn = turn < 0.0 ? turn + 2.0 * Math.PI : turn;
                    double sweep = Math.Abs(Sweep);

                    // Past the end, the nearer end of the two.
                    t = turn <= sweep ? turn / sweep : (turn - sweep < 2.0 * Math.PI - turn ? 1.0 : 0.0);
                }

                distance = At(t).DistanceTo(point);
                return t;
            }
        }

        /// <summary>A face traced: its pieces in order, its signed area, the group of connected objects it belongs to.</summary>
        private sealed class Cycle
        {
            public List<GeoPoint2> Vertices { get; } = new List<GeoPoint2>();

            public List<double> Bulges { get; } = new List<double>();

            public List<string> Sources { get; } = new List<string>();

            public double Area { get; set; }

            public int Component { get; set; }

            public IReadOnlyList<GeoPoint2> Outline { get; set; }

            /// <summary>The loop of the face; an island's outline turned to run counter-clockwise.</summary>
            public CadBoundaryLoop ToLoop(bool reverse)
            {
                List<GeoPoint2> vertices = Vertices;
                List<double> bulges = Bulges;

                if (reverse)
                {
                    int n = vertices.Count;
                    vertices = Enumerable.Range(0, n).Select(i => Vertices[(n - i) % n]).ToList();
                    bulges = Enumerable.Range(0, n).Select(i => -Bulges[(2 * n - i - 1) % n]).ToList();
                }

                var sources = new List<string>();

                foreach (string source in Sources)
                {
                    // The edge that closes a gap belongs to no object.
                    if (source.Length > 0 && !sources.Contains(source, StringComparer.OrdinalIgnoreCase))
                    {
                        sources.Add(source);
                    }
                }

                return Corners(vertices, bulges, sources.AsReadOnly());
            }
        }

        /// <summary>The arrangement: corners merged within the tolerance, pieces between them, and the faces they close.</summary>
        private sealed class Graph
        {
            private readonly double _tol;
            private readonly List<GeoPoint2> _nodes = new List<GeoPoint2>();
            private readonly Dictionary<(long, long), List<int>> _cells = new Dictionary<(long, long), List<int>>();
            private readonly List<Piece> _pieces = new List<Piece>();

            public Graph(double tol)
            {
                _tol = tol;
            }

            public void Build(List<Raw> raws, double gap, double pixel)
            {
                Cut(raws);
                var seen = new Dictionary<(int, int), List<Piece>>();

                foreach (Raw raw in raws)
                {
                    var cuts = new List<KeyValuePair<double, GeoPoint2>>(raw.Cuts)
                    {
                        new KeyValuePair<double, GeoPoint2>(0.0, raw.Start),
                        new KeyValuePair<double, GeoPoint2>(1.0, raw.End),
                    };
                    cuts.Sort(
                        (x, y) =>
                            x.Key.CompareTo(y.Key) != 0 ? x.Key.CompareTo(y.Key)
                            : x.Value.X.CompareTo(y.Value.X) != 0 ? x.Value.X.CompareTo(y.Value.X)
                            : x.Value.Y.CompareTo(y.Value.Y)
                    );
                    int last = Node(cuts[0].Value);
                    double lastT = 0.0;

                    for (int i = 1; i < cuts.Count; i++)
                    {
                        int next = Node(cuts[i].Value);

                        if (next == last)
                        {
                            continue;
                        }

                        double bulge = raw.IsArc ? Math.Tan(raw.Sweep * (cuts[i].Key - lastT) / 4.0) : 0.0;
                        var piece = new Piece(last, next, _nodes[last], _nodes[next], bulge, raw.Source);
                        var key = (Math.Min(last, next), Math.Max(last, next));

                        // A piece lying on one kept already, as two lines drawn over each other, is kept once.
                        if (!seen.TryGetValue(key, out List<Piece> same))
                        {
                            seen.Add(key, same = new List<Piece>());
                        }

                        if (!same.Exists(p => p.Middle.DistanceTo(piece.Middle) <= _tol))
                        {
                            same.Add(piece);
                            _pieces.Add(piece);
                        }

                        last = next;
                        lastT = cuts[i].Key;
                    }
                }

                if (gap > _tol)
                {
                    Bridge(gap, pixel);
                }

                Prune();
            }

            /// <summary>
            /// Joins the loose ends no further apart than the gap, as AutoCAD closes them, by straight pieces of no object: the end
            /// of a line is taken on along its own direction, and it closes where that reaches another piece (the middle of a
            /// line, so that a line standing 2 off the middle of another is joined to it at the foot of its own direction), or
            /// where it meets the way another loose end goes on (two lines whose ends are 3 apart across a corner meet at the
            /// corner), or the other loose end itself; and two loose ends that are not so met are joined directly. The nearest
            /// bridge is made first and each end serves once. The ends are swept along the axis they spread over more, the fewer
            /// pairs.
            /// </summary>
            private void Bridge(double gap, double pixel)
            {
                // How far a way on may go to meet a piece, or the other way on: twice the gap and a pixel; the shorter of two ways
                // that meet, and two ends facing each other, a gap and half a pixel.
                double reachMax = 2.0 * gap + pixel;
                double joinMax = gap + pixel / 2.0;
                var degree = new int[_nodes.Count];

                foreach (Piece piece in _pieces)
                {
                    degree[piece.A]++;
                    degree[piece.B]++;
                }

                var ends = new List<int>();

                for (int n = 0; n < degree.Length; n++)
                {
                    if (degree[n] == 1)
                    {
                        ends.Add(n);
                    }
                }

                bool alongX =
                    ends.Count == 0
                    || ends.Max(n => _nodes[n].X) - ends.Min(n => _nodes[n].X)
                        >= ends.Max(n => _nodes[n].Y) - ends.Min(n => _nodes[n].Y);
                Func<int, double> along = alongX ? (Func<int, double>)(n => _nodes[n].X) : n => _nodes[n].Y;
                ends.Sort((a, b) => along(a) != along(b) ? along(a).CompareTo(along(b)) : a.CompareTo(b));

                // Every way an end could be closed: Kind 0 the straight piece to another end, 1 a line gone on to another piece, 2
                // two lines gone on to where they meet. Cost is how far the end goes (the longer of the two for kind 2).
                var bridges = new List<(long Cost, int Kind, int A, int B, GeoPoint2 Point)>();

                for (int i = 0; i < ends.Count; i++)
                {
                    for (int j = i + 1; j < ends.Count && along(ends[j]) - along(ends[i]) <= joinMax; j++)
                    {
                        double distance = _nodes[ends[i]].DistanceTo(_nodes[ends[j]]);

                        if (distance <= joinMax)
                        {
                            bridges.Add(
                                (Round(distance), 0, Math.Min(ends[i], ends[j]), Math.Max(ends[i], ends[j]), default)
                            );
                        }
                    }
                }

                Dictionary<int, GeoEdge2> rays = Rays(new HashSet<int>(ends), reachMax);
                var tolerance = new Tolerance(_tol, 1E-9);

                // The pieces a way on may reach, their boxes made once.
                Piece[] pieces = _pieces.ToArray();
                var boxes = new PieceBoxes(pieces, alongX, Math.Max(8.0 * reachMax, 8.0 * _tol));
                var found = new List<int>();

                foreach (int end in ends)
                {
                    if (!rays.TryGetValue(end, out GeoEdge2 ray))
                    {
                        continue;
                    }

                    PieceBoxes.BoxOf(
                        ray.StartPoint,
                        ray.EndPoint,
                        ray.Bulge,
                        out double rayMinX,
                        out double rayMaxX,
                        out double rayMinY,
                        out double rayMaxY
                    );
                    boxes.Near(rayMinX - _tol, rayMaxX + _tol, rayMinY - _tol, rayMaxY + _tol, found);

                    foreach (int p in found)
                    {
                        Piece body = pieces[p];

                        if (body.A == end || body.B == end)
                        {
                            continue;
                        }

                        foreach (
                            GeoPoint2 hit in new GeoEdge2(body.P0, body.P1, body.Bulge).GetIntersections(ray, tolerance)
                        )
                        {
                            double reach = _nodes[end].DistanceTo(hit);

                            if (reach > _tol && reach <= reachMax && !FacesAnEnd(rays, body, hit, ray))
                            {
                                bridges.Add((Round(reach), 1, end, body.A, hit));
                            }
                        }
                    }
                }

                // Two ways on can meet only where their ends are no further apart than twice the gap: the ends are sorted, so the
                // pairs are those within a window.
                for (int i = 0; i < ends.Count; i++)
                {
                    if (!rays.TryGetValue(ends[i], out GeoEdge2 ray))
                    {
                        continue;
                    }

                    for (int j = i + 1; j < ends.Count && along(ends[j]) - along(ends[i]) <= 2.0 * reachMax + _tol; j++)
                    {
                        if (
                            !rays.TryGetValue(ends[j], out GeoEdge2 onward)
                            || _nodes[ends[i]].DistanceTo(_nodes[ends[j]]) > 2.0 * reachMax + _tol
                        )
                        {
                            continue;
                        }

                        int first = Math.Min(ends[i], ends[j]);
                        int second = Math.Max(ends[i], ends[j]);

                        foreach (GeoPoint2 hit in ray.GetIntersections(onward, tolerance))
                        {
                            double near = _nodes[ends[i]].DistanceTo(hit);
                            double far = _nodes[ends[j]].DistanceTo(hit);

                            if (near > _tol && far > _tol && Math.Min(near, far) <= joinMax)
                            {
                                bridges.Add((Round(Math.Max(near, far)), 2, first, second, hit));
                            }
                        }
                    }
                }

                bridges.Sort(
                    (x, y) =>
                        x.Cost != y.Cost ? x.Cost.CompareTo(y.Cost)
                        : x.Kind != y.Kind ? x.Kind.CompareTo(y.Kind)
                        : x.A != y.A ? x.A.CompareTo(y.A)
                        : x.B != y.B ? x.B.CompareTo(y.B)
                        : x.Point.X != y.Point.X ? x.Point.X.CompareTo(y.Point.X)
                        : x.Point.Y.CompareTo(y.Point.Y)
                );
                var joined = new HashSet<int>();
                var meets = new HashSet<int>(ends);

                foreach ((long Cost, int Kind, int A, int B, GeoPoint2 Point) bridge in bridges)
                {
                    if (joined.Contains(bridge.A))
                    {
                        continue;
                    }

                    if (bridge.Kind == 0)
                    {
                        if (joined.Contains(bridge.B))
                        {
                            continue;
                        }

                        joined.Add(bridge.A);
                        joined.Add(bridge.B);
                        _pieces.Add(
                            new Piece(bridge.A, bridge.B, _nodes[bridge.A], _nodes[bridge.B], 0.0, string.Empty)
                        );
                    }
                    else if (bridge.Kind == 1)
                    {
                        int meet = Meeting(bridge.Point, boxes, pieces.Length, found);

                        if (meet < 0 || meet == bridge.A || joined.Contains(meet))
                        {
                            continue;
                        }

                        joined.Add(bridge.A);
                        if (meets.Contains(meet))
                        {
                            joined.Add(meet);
                        }
                        _pieces.Add(
                            new Piece(
                                bridge.A,
                                meet,
                                _nodes[bridge.A],
                                _nodes[meet],
                                BulgeTo(rays[bridge.A], _nodes[meet]),
                                string.Empty
                            )
                        );
                    }
                    else
                    {
                        if (joined.Contains(bridge.B))
                        {
                            continue;
                        }

                        int corner = Node(bridge.Point);
                        joined.Add(bridge.A);
                        joined.Add(bridge.B);
                        _pieces.Add(
                            new Piece(
                                bridge.A,
                                corner,
                                _nodes[bridge.A],
                                _nodes[corner],
                                BulgeTo(rays[bridge.A], _nodes[corner]),
                                string.Empty
                            )
                        );
                        _pieces.Add(
                            new Piece(
                                bridge.B,
                                corner,
                                _nodes[bridge.B],
                                _nodes[corner],
                                BulgeTo(rays[bridge.B], _nodes[corner]),
                                string.Empty
                            )
                        );
                    }
                }
            }

            /// <summary>The bulge of the piece that runs from the start of a way on to a point on it: nought for a straight way, else the arc of its circle that far.</summary>
            private static double BulgeTo(GeoEdge2 way, GeoPoint2 point)
            {
                if (
                    way.Bulge == 0.0
                    || !Arc(
                        way.StartPoint,
                        way.EndPoint,
                        way.Bulge,
                        out GeoPoint2 centre,
                        out _,
                        out double from,
                        out double sweep
                    )
                )
                {
                    return 0.0;
                }

                double turn = (sweep > 0.0 ? 1.0 : -1.0) * (Math.Atan2(point.Y - centre.Y, point.X - centre.X) - from);
                turn %= 2.0 * Math.PI;
                turn = turn < 0.0 ? turn + 2.0 * Math.PI : turn;
                return Math.Tan((sweep > 0.0 ? 1.0 : -1.0) * Math.Min(turn, Math.Abs(sweep)) / 4.0);
            }

            /// <summary>
            /// Whether a straight way on meets, at the loose end of a piece, the straight way on of that end head on, on the same
            /// line: the two ends face each other, and AutoCAD joins them as two ends (no further apart than the gap and half a
            /// pixel: two collinear lines 4 apart close at a gap of 3.9 and not 3), not by the longer reach a way on has to a piece.
            /// </summary>
            private bool FacesAnEnd(Dictionary<int, GeoEdge2> rays, Piece body, GeoPoint2 hit, GeoEdge2 ray)
            {
                foreach (int node in new[] { body.A, body.B })
                {
                    if (
                        ray.Bulge != 0.0
                        || !rays.TryGetValue(node, out GeoEdge2 other)
                        || other.Bulge != 0.0
                        || _nodes[node].DistanceTo(hit) > _tol
                    )
                    {
                        continue;
                    }

                    double ax = ray.EndPoint.X - ray.StartPoint.X;
                    double ay = ray.EndPoint.Y - ray.StartPoint.Y;
                    double bx = other.EndPoint.X - other.StartPoint.X;
                    double by = other.EndPoint.Y - other.StartPoint.Y;
                    double lengths = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);

                    if (lengths > 0.0 && Math.Abs(ax * by - ay * bx) <= 1E-6 * lengths && ax * bx + ay * by < 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>A cost to a ten-millionth, so that two bridges as long as each other tie whatever the last bits say.</summary>
            private static long Round(double cost) => (long)Math.Round(cost * 1E7);

            /// <summary>
            /// The way each loose end of a piece goes on: a segment as long as the reach given (twice the gap and a pixel) from the end
            /// along the piece's own direction; for an arc, the arc of its own circle as long (a half turn at most) on past the end, as
            /// AutoCAD 2021 closes an arc whose end stands off a line (the circle of radius 50 about (50, 50) ending at
            /// (96.9846, 67.1010) meets a line at y 64 at (98, 64), where the tangent would have met it at (98.113, 64)).
            /// </summary>
            private Dictionary<int, GeoEdge2> Rays(HashSet<int> ends, double gap)
            {
                var rays = new Dictionary<int, GeoEdge2>();

                foreach (Piece piece in _pieces)
                {
                    if (piece.Bulge != 0.0)
                    {
                        if (
                            !Arc(
                                piece.P0,
                                piece.P1,
                                piece.Bulge,
                                out GeoPoint2 centre,
                                out double radius,
                                out _,
                                out double sweep
                            )
                        )
                        {
                            continue;
                        }

                        foreach (int end in new[] { piece.A, piece.B })
                        {
                            if (ends.Contains(end))
                            {
                                // Past the end of the piece it goes on the way it turns, past its start the way back.
                                GeoPoint2 start = _nodes[end];
                                double turn =
                                    Math.Min(gap / radius, Math.PI)
                                    * (sweep > 0.0 ? 1.0 : -1.0)
                                    * (end == piece.B ? 1.0 : -1.0);
                                double angle = Math.Atan2(start.Y - centre.Y, start.X - centre.X) + turn;
                                var onward = new GeoPoint2(
                                    centre.X + radius * Math.Cos(angle),
                                    centre.Y + radius * Math.Sin(angle)
                                );
                                rays[end] = new GeoEdge2(start, onward, Math.Tan(turn / 4.0));
                            }
                        }

                        continue;
                    }

                    foreach (int end in new[] { piece.A, piece.B })
                    {
                        if (!ends.Contains(end))
                        {
                            continue;
                        }

                        GeoPoint2 tail = end == piece.A ? piece.P1 : piece.P0;
                        GeoPoint2 tip = _nodes[end];
                        double length = tail.DistanceTo(tip);

                        if (length <= _tol)
                        {
                            continue;
                        }

                        GeoPoint2 far = new GeoPoint2(
                            tip.X + (tip.X - tail.X) / length * gap,
                            tip.Y + (tip.Y - tail.Y) / length * gap
                        );
                        rays[end] = new GeoEdge2(tip, far, 0.0);
                    }
                }

                return rays;
            }

            /// <summary>
            /// The corner at a point on a piece: the piece's own end where the point is within the tolerance of it, else the piece
            /// cut in two there and the new corner; -1 where no piece lies on the point. The first piece in the list that lies on
            /// it is the one. Only the pieces whose boxes reach the point are tried: those the boxes were made of (the first
            /// <paramref name="made"/> of the list, each still within its box when it was cut in two since) and those added
            /// after.
            /// </summary>
            private int Meeting(GeoPoint2 point, PieceBoxes boxes, int made, List<int> near)
            {
                double reach = _tol + 1E-9;
                boxes.Near(point.X - reach, point.X + reach, point.Y - reach, point.Y + reach, near);

                for (int p = made; p < _pieces.Count; p++)
                {
                    PieceBoxes.BoxOf(_pieces[p], out double lowX, out double highX, out double lowY, out double highY);

                    if (
                        highX >= point.X - reach
                        && lowX <= point.X + reach
                        && highY >= point.Y - reach
                        && lowY <= point.Y + reach
                    )
                    {
                        near.Add(p);
                    }
                }

                near.Sort();

                foreach (int p in near)
                {
                    Piece piece = _pieces[p];
                    var raw = new Raw(piece.P0, piece.P1, piece.Bulge, piece.Source);
                    double t = raw.Nearest(point, out double distance);

                    if (distance > _tol)
                    {
                        continue;
                    }

                    if (point.DistanceTo(piece.P0) <= _tol)
                    {
                        return piece.A;
                    }

                    if (point.DistanceTo(piece.P1) <= _tol)
                    {
                        return piece.B;
                    }

                    int corner = Node(point);
                    double sweep = raw.IsArc ? raw.Sweep : 0.0;
                    var first = new Piece(
                        piece.A,
                        corner,
                        piece.P0,
                        _nodes[corner],
                        Math.Tan(sweep * t / 4.0),
                        piece.Source
                    );
                    var second = new Piece(
                        corner,
                        piece.B,
                        _nodes[corner],
                        piece.P1,
                        Math.Tan(sweep * (1.0 - t) / 4.0),
                        piece.Source
                    );
                    _pieces[p] = first;
                    _pieces.Add(second);
                    return corner;
                }

                return -1;
            }

            public List<Cycle> Trace()
            {
                // Each piece runs both ways; at each corner the ways out are ordered by the direction they leave in.
                var outgoing = new List<int>[_nodes.Count];

                for (int i = 0; i < _nodes.Count; i++)
                {
                    outgoing[i] = new List<int>();
                }

                var keys = new double[_pieces.Count * 2][];

                for (int p = 0; p < _pieces.Count; p++)
                {
                    if (_pieces[p].Removed)
                    {
                        continue;
                    }

                    for (int side = 0; side < 2; side++)
                    {
                        int half = 2 * p + side;
                        outgoing[From(half)].Add(half);
                        keys[half] = Leaving(half);
                    }
                }

                foreach (List<int> ways in outgoing)
                {
                    ways.Sort(
                        (x, y) =>
                            keys[x][0] != keys[y][0] ? keys[x][0].CompareTo(keys[y][0])
                            : keys[x][1] != keys[y][1] ? keys[x][1].CompareTo(keys[y][1])
                            : x.CompareTo(y)
                    );
                }

                int[] component = Components();
                var used = new bool[_pieces.Count * 2];
                var cycles = new List<Cycle>();

                for (int start = 0; start < used.Length; start++)
                {
                    if (used[start] || _pieces[start / 2].Removed)
                    {
                        continue;
                    }

                    var cycle = new Cycle { Component = component[From(start)] };
                    int half = start;
                    int guard = used.Length + 1;

                    while (!used[half] && guard-- > 0)
                    {
                        used[half] = true;
                        Piece piece = _pieces[half / 2];
                        cycle.Vertices.Add(_nodes[From(half)]);
                        cycle.Bulges.Add(half % 2 == 0 ? piece.Bulge : -piece.Bulge);
                        cycle.Sources.Add(piece.Source);

                        // The next way out is the one just before the way back, turning clockwise round the corner.
                        List<int> ways = outgoing[To(half)];
                        int back = ways.IndexOf(half ^ 1);
                        half = ways[(back - 1 + ways.Count) % ways.Count];
                    }

                    if (half != start)
                    {
                        continue;
                    }

                    cycle.Area = Signed(cycle.Vertices, cycle.Bulges);
                    cycle.Outline = Corners(cycle.Vertices, cycle.Bulges, Array.Empty<string>()).Outline;
                    cycles.Add(cycle);
                }

                return cycles;
            }

            private int From(int half) => half % 2 == 0 ? _pieces[half / 2].A : _pieces[half / 2].B;

            private int To(int half) => half % 2 == 0 ? _pieces[half / 2].B : _pieces[half / 2].A;

            /// <summary>The direction a way leaves its corner in, to a billionth of a radian, then how it bends: a way bending left after a straight one.</summary>
            private double[] Leaving(int half)
            {
                Piece piece = _pieces[half / 2];
                bool forward = half % 2 == 0;
                GeoPoint2 a = forward ? piece.P0 : piece.P1;
                GeoPoint2 b = forward ? piece.P1 : piece.P0;
                double bulge = forward ? piece.Bulge : -piece.Bulge;
                double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
                double bend = 0.0;

                if (bulge != 0.0 && Arc(a, b, bulge, out _, out double radius, out _, out double sweep))
                {
                    // The tangent leaves the chord by half the sweep, to the right of it for an arc turning left.
                    angle -= sweep / 2.0;
                    bend = Math.Sign(sweep) / radius;
                }

                angle = Math.Atan2(Math.Sin(angle), Math.Cos(angle));
                angle = angle <= -Math.PI + 1E-12 ? Math.PI : angle;
                return new[] { Math.Round(angle * 1E9), bend };
            }

            /// <summary>Cuts every pair of edges where they cross or one ends on the other, the pairs found by a sweep along the axis that gives the fewer.</summary>
            private void Cut(List<Raw> raws)
            {
                long acrossX = Pairs(raws, true, long.MaxValue);
                bool alongX = acrossX <= Pairs(raws, false, acrossX);
                Func<Raw, double> low = alongX ? (Func<Raw, double>)(r => r.MinX) : r => r.MinY;
                Func<Raw, double> high = alongX ? (Func<Raw, double>)(r => r.MaxX) : r => r.MaxY;
                int[] order = Enumerable.Range(0, raws.Count).OrderBy(i => low(raws[i])).ThenBy(i => i).ToArray();
                var tolerance = new Tolerance(_tol, 1E-9);

                for (int i = 0; i < order.Length; i++)
                {
                    Raw a = raws[order[i]];

                    for (int j = i + 1; j < order.Length && low(raws[order[j]]) <= high(a) + _tol; j++)
                    {
                        Raw b = raws[order[j]];

                        if (
                            a.MaxX < b.MinX - _tol
                            || b.MaxX < a.MinX - _tol
                            || a.MaxY < b.MinY - _tol
                            || b.MaxY < a.MinY - _tol
                        )
                        {
                            continue;
                        }

                        foreach (GeoPoint2 point in a.Edge.GetIntersections(b.Edge, tolerance))
                        {
                            AddCut(a, point);
                            AddCut(b, point);
                        }

                        // An end lying on the other edge cuts it there: a T, or two lines over each other.
                        foreach (GeoPoint2 end in new[] { b.Start, b.End })
                        {
                            AddCut(a, end);
                        }

                        foreach (GeoPoint2 end in new[] { a.Start, a.End })
                        {
                            AddCut(b, end);
                        }
                    }
                }
            }

            private void AddCut(Raw raw, GeoPoint2 point)
            {
                double t = raw.Nearest(point, out double distance);

                if (distance <= _tol && t > 0.0 && t < 1.0)
                {
                    raw.Cuts.Add(new KeyValuePair<double, GeoPoint2>(t, point));
                }
            }

            /// <summary>How many pairs a sweep along an axis meets, counted up to a most.</summary>
            private long Pairs(List<Raw> raws, bool alongX, long most)
            {
                double[] lows = raws.Select(r => alongX ? r.MinX : r.MinY).ToArray();
                double[] highs = raws.Select(r => alongX ? r.MaxX : r.MaxY).ToArray();
                int[] order = Enumerable.Range(0, raws.Count).OrderBy(i => lows[i]).ThenBy(i => i).ToArray();
                long pairs = 0;

                for (int i = 0; i < order.Length && pairs < most; i++)
                {
                    for (int j = i + 1; j < order.Length && lows[order[j]] <= highs[order[i]] + _tol; j++)
                    {
                        pairs++;
                    }
                }

                return pairs;
            }

            /// <summary>The corner of a point: one within the tolerance of it, else a new one.</summary>
            private int Node(GeoPoint2 point)
            {
                double size = 4.0 * _tol;
                long cx = (long)Math.Floor(point.X / size);
                long cy = (long)Math.Floor(point.Y / size);
                int found = -1;
                double best = double.MaxValue;

                for (long x = cx - 1; x <= cx + 1; x++)
                {
                    for (long y = cy - 1; y <= cy + 1; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out List<int> nodes))
                        {
                            continue;
                        }

                        foreach (int node in nodes)
                        {
                            double distance = _nodes[node].DistanceTo(point);

                            if (distance <= _tol && (distance < best || (distance == best && node < found)))
                            {
                                found = node;
                                best = distance;
                            }
                        }
                    }
                }

                if (found >= 0)
                {
                    return found;
                }

                _nodes.Add(point);

                if (!_cells.TryGetValue((cx, cy), out List<int> cell))
                {
                    _cells.Add((cx, cy), cell = new List<int>());
                }

                cell.Add(_nodes.Count - 1);
                return _nodes.Count - 1;
            }

            /// <summary>Drops, again and again, the pieces with an end no other piece meets: they close nothing.</summary>
            private void Prune()
            {
                var degree = new int[_nodes.Count];

                foreach (Piece piece in _pieces)
                {
                    degree[piece.A]++;
                    degree[piece.B]++;
                }

                bool changed = true;

                while (changed)
                {
                    changed = false;

                    foreach (Piece piece in _pieces)
                    {
                        if (!piece.Removed && (degree[piece.A] < 2 || degree[piece.B] < 2))
                        {
                            piece.Removed = true;
                            degree[piece.A]--;
                            degree[piece.B]--;
                            changed = true;
                        }
                    }
                }
            }

            /// <summary>The group of connected corners each corner belongs to, numbered by its least corner.</summary>
            private int[] Components()
            {
                var parent = Enumerable.Range(0, _nodes.Count).ToArray();

                int Root(int n)
                {
                    while (parent[n] != n)
                    {
                        parent[n] = parent[parent[n]];
                        n = parent[n];
                    }

                    return n;
                }

                foreach (Piece piece in _pieces.Where(p => !p.Removed))
                {
                    int a = Root(piece.A);
                    int b = Root(piece.B);

                    if (a != b)
                    {
                        parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                }

                return Enumerable.Range(0, _nodes.Count).Select(Root).ToArray();
            }
        }

        /// <summary>
        /// The boxes of a list of pieces, made once, so that those near a given box are found without a pass over all of them:
        /// the short pieces are sorted along an axis and only a window of them is looked at; the few long ones (a side of a box)
        /// are looked at every time.
        /// </summary>
        private sealed class PieceBoxes
        {
            private readonly bool _alongX;
            private readonly double[] _minX;
            private readonly double[] _maxX;
            private readonly double[] _minY;
            private readonly double[] _maxY;
            private readonly int[] _shorts;
            private readonly double[] _shortLows;
            private readonly int[] _longs;
            private readonly double _widest;

            /// <param name="pieces">The pieces; a piece cut in two later stays inside the box it had.</param>
            /// <param name="alongX">Whether the short pieces are sorted along x, else along y.</param>
            /// <param name="longer">The extent along the axis past which a piece is long.</param>
            public PieceBoxes(Piece[] pieces, bool alongX, double longer)
            {
                _alongX = alongX;
                _minX = new double[pieces.Length];
                _maxX = new double[pieces.Length];
                _minY = new double[pieces.Length];
                _maxY = new double[pieces.Length];
                var shorts = new List<int>();
                var longs = new List<int>();

                for (int p = 0; p < pieces.Length; p++)
                {
                    BoxOf(pieces[p], out _minX[p], out _maxX[p], out _minY[p], out _maxY[p]);
                    double extent = alongX ? _maxX[p] - _minX[p] : _maxY[p] - _minY[p];

                    if (extent > longer)
                    {
                        longs.Add(p);
                    }
                    else
                    {
                        shorts.Add(p);
                        _widest = Math.Max(_widest, extent);
                    }
                }

                double[] lows = alongX ? _minX : _minY;
                shorts.Sort((a, b) => lows[a] != lows[b] ? lows[a].CompareTo(lows[b]) : a.CompareTo(b));
                _shorts = shorts.ToArray();
                _shortLows = shorts.Select(p => lows[p]).ToArray();
                _longs = longs.ToArray();
            }

            /// <summary>The box of a piece, widened by how far an arc bows: enough to find what lies on it.</summary>
            public static void BoxOf(Piece piece, out double minX, out double maxX, out double minY, out double maxY) =>
                BoxOf(piece.P0, piece.P1, piece.Bulge, out minX, out maxX, out minY, out maxY);

            /// <summary>The box of an edge from one point to another of a bulge, widened by how far it bows.</summary>
            public static void BoxOf(
                GeoPoint2 a,
                GeoPoint2 b,
                double bulge,
                out double minX,
                out double maxX,
                out double minY,
                out double maxY
            )
            {
                double bow = bulge == 0.0 ? 0.0 : Math.Abs(bulge) * a.DistanceTo(b) / 2.0;
                minX = Math.Min(a.X, b.X) - bow;
                maxX = Math.Max(a.X, b.X) + bow;
                minY = Math.Min(a.Y, b.Y) - bow;
                maxY = Math.Max(a.Y, b.Y) + bow;
            }

            /// <summary>Puts into a list, in no order, the pieces whose boxes meet a box (touching counts).</summary>
            public void Near(double minX, double maxX, double minY, double maxY, List<int> into)
            {
                into.Clear();
                double low = _alongX ? minX : minY;
                double high = _alongX ? maxX : maxY;

                // The first short piece that can reach as far back as the box starts, then on while they start before it ends.
                int at = Array.BinarySearch(_shortLows, low - _widest);
                at = at < 0 ? ~at : at;

                while (at > 0 && _shortLows[at - 1] >= low - _widest)
                {
                    at--;
                }

                for (; at < _shorts.Length && _shortLows[at] <= high; at++)
                {
                    int p = _shorts[at];

                    if (_maxX[p] >= minX && _minX[p] <= maxX && _maxY[p] >= minY && _minY[p] <= maxY)
                    {
                        into.Add(p);
                    }
                }

                foreach (int p in _longs)
                {
                    if (_maxX[p] >= minX && _minX[p] <= maxX && _maxY[p] >= minY && _minY[p] <= maxY)
                    {
                        into.Add(p);
                    }
                }
            }
        }

        /// <summary>A piece between two corners: a line or an arc of a bulge, the object it is of.</summary>
        private sealed class Piece
        {
            public Piece(int a, int b, GeoPoint2 p0, GeoPoint2 p1, double bulge, string source)
            {
                A = a;
                B = b;
                P0 = p0;
                P1 = p1;
                Bulge = bulge;
                Source = source;
                Middle =
                    bulge != 0.0
                    && Arc(p0, p1, bulge, out GeoPoint2 centre, out double radius, out double from, out double sweep)
                        ? new GeoPoint2(
                            centre.X + radius * Math.Cos(from + sweep / 2.0),
                            centre.Y + radius * Math.Sin(from + sweep / 2.0)
                        )
                        : new GeoPoint2((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);
            }

            public int A { get; }

            public int B { get; }

            public GeoPoint2 P0 { get; }

            public GeoPoint2 P1 { get; }

            public double Bulge { get; }

            public string Source { get; }

            public GeoPoint2 Middle { get; }

            public bool Removed { get; set; }
        }
    }
}
