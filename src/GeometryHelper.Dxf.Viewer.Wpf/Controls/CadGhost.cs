using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The ghost copy a transforming command draws while it waits for its second point, as AutoCAD drags the objects along:
    /// MOVE and COPY by the displacement from the base point to the cursor, ROTATE by the angle about the base point, SCALE by
    /// the distance from it as the factor, MIRROR across the line from the first point to the cursor, and a grip edit's MOVE,
    /// ROTATE, SCALE and MIRROR the same way from its base point. It follows the session's
    /// prompts to know the objects the command took (those selected before it started, or those "Select objects:" gathered),
    /// and keeps their outlines in drawing units so that a move of the mouse only transforms them. The block INSERT drags while
    /// it asks where and how to place it (<see cref="CadPrompt.Drag"/>) is outlined the same way, in the block's coordinates.
    /// </summary>
    internal sealed class CadGhost
    {
        private readonly List<string> _handles = new List<string>();
        private readonly List<double[]> _paths = new List<double[]>();
        private readonly List<int> _pathColours = new List<int>();
        private readonly List<DisplayRay2> _rays = new List<DisplayRay2>();
        private readonly List<double[]> _dragPaths = new List<double[]>();
        private readonly List<int> _dragColours = new List<int>();
        private readonly List<DisplayRay2> _dragRays = new List<DisplayRay2>();
        private IReadOnlyList<DisplayItem2> _dragItems;
        private double _dragTolerance;
        private CadCommand _command;
        private DisplayList2 _list;
        private double _tolerance;
        private bool _built;
        private readonly List<double[]> _movedPaths = new List<double[]>();
        private readonly List<int> _movedColours = new List<int>();
        private readonly List<DisplayRay2> _movedRays = new List<DisplayRay2>();
        private IReadOnlyList<string> _movedHandles;
        private DisplayList2 _movedList;
        private double _movedTolerance;

        /// <summary>Gets the objects the command running took, in ordinal order.</summary>
        public IReadOnlyList<string> Handles => _handles;

        /// <summary>Follows the session after its prompt changed: a new command starts afresh; "Select objects:" gathers.</summary>
        public void Follow(CadCommandSession session)
        {
            if (session == null || !session.IsRunning)
            {
                _command = null;
                Reset();
                return;
            }

            if (!ReferenceEquals(session.Current, _command))
            {
                _command = session.Current;
                Reset();
            }

            if (session.Prompt != null && session.Prompt.Kind == CadPromptKind.Selection)
            {
                Take(session.Gathering);
            }
            else if (_handles.Count == 0)
            {
                Take(session.Document.Selection.Handles);
            }
        }

        /// <summary>Gets the transformation the ghost is drawn under with the cursor at a point, or null where the prompt waiting draws none.</summary>
        public static Affine2? Transform(CadCommandSession session, GeoPoint2 target)
        {
            CadPrompt prompt = session?.Prompt;

            if (prompt?.BasePoint == null || session.Current == null)
            {
                return null;
            }

            GeoPoint2 b = prompt.BasePoint.Value;
            GeoVector2 way = target - b;
            string message = prompt.Message ?? string.Empty;

            // A grip edit drags the objects selected as the command of its mode does: MOVE, ROTATE, SCALE, MIRROR; STRETCH has none.
            string name = session.IsGripEditing
                ? message.StartsWith("Specify move point", StringComparison.Ordinal)
                    ? "MOVE"
                    : message.StartsWith("Specify rotation angle", StringComparison.Ordinal)
                        ? "ROTATE"
                        : message.StartsWith("Specify scale factor", StringComparison.Ordinal)
                            ? "SCALE"
                            : message.StartsWith("Specify second point", StringComparison.Ordinal)
                                ? "MIRROR"
                                : null
                : session.Current.Name;

            if (session.IsGripEditing && name != null)
            {
                message =
                    name == "MOVE" ? "Specify second point"
                    : name == "MIRROR" ? "Specify second point of mirror line"
                    : message;
            }

            switch (name)
            {
                case "MOVE":
                case "COPY":
                    if (
                        prompt.Kind == CadPromptKind.Point
                        && message.StartsWith("Specify second point", StringComparison.Ordinal)
                    )
                    {
                        return Affine2.Translation(way.X, way.Y);
                    }

                    break;
                case "ROTATE":
                    if (
                        prompt.Kind == CadPromptKind.Angle
                        && message.StartsWith("Specify rotation angle", StringComparison.Ordinal)
                    )
                    {
                        double angle = Math.Atan2(way.Y, way.X);
                        double cos = Math.Cos(angle);
                        double sin = Math.Sin(angle);
                        return About(b, cos, sin, -sin, cos);
                    }

                    break;
                case "SCALE":
                    if (
                        prompt.Kind == CadPromptKind.Distance
                        && message.StartsWith("Specify scale factor", StringComparison.Ordinal)
                        && way.Length > 0.0
                    )
                    {
                        return About(b, way.Length, 0.0, 0.0, way.Length);
                    }

                    break;
                case "MIRROR":
                    if (
                        prompt.Kind == CadPromptKind.Point
                        && message.StartsWith("Specify second point of mirror line", StringComparison.Ordinal)
                        && way.Length > 0.0
                    )
                    {
                        double ux = way.X / way.Length;
                        double uy = way.Y / way.Length;
                        return About(b, 2.0 * ux * ux - 1.0, 2.0 * ux * uy, 2.0 * ux * uy, 2.0 * uy * uy - 1.0);
                    }

                    break;
            }

            return null;
        }

        /// <summary>
        /// Gets the outlines of the objects in drawing units, each a run of X and Y: lines, polylines, arcs flattened to the
        /// tolerance, the rings of fills, and a text's box. Kept until the objects, the display list or the zoom much change. An
        /// XLINE or a RAY has no outline here: <see cref="Rays"/> gives the part of it the view shows once it is transformed.
        /// </summary>
        public IReadOnlyList<double[]> Paths(CadDocument document, CadSpace space, double tolerance)
        {
            DisplayList2 list = document.GetDisplay(space);

            if (
                _built
                && ReferenceEquals(list, _list)
                && tolerance <= _tolerance * 2.0
                && tolerance >= _tolerance / 2.0
            )
            {
                return _paths;
            }

            _paths.Clear();
            _pathColours.Clear();
            _rays.Clear();
            _list = list;
            _tolerance = tolerance;
            _built = true;

            if (_handles.Count == 0)
            {
                return _paths;
            }

            Gather(list, _handles, tolerance, _paths, _pathColours, _rays);
            return _paths;
        }

        /// <summary>
        /// Gets the outlines of the objects a prompt's preview moves (<see cref="CadPreview.Handles"/>), as
        /// <see cref="Paths(CadDocument, CadSpace, double)"/> gives those of the objects the command took: kept while the
        /// objects, the display list and the zoom stay much as they were.
        /// </summary>
        public IReadOnlyList<double[]> Paths(
            CadDocument document,
            CadSpace space,
            IReadOnlyList<string> handles,
            double tolerance
        )
        {
            DisplayList2 list = document.GetDisplay(space);

            if (
                ReferenceEquals(handles, _movedHandles)
                && ReferenceEquals(list, _movedList)
                && tolerance <= _movedTolerance * 2.0
                && tolerance >= _movedTolerance / 2.0
            )
            {
                return _movedPaths;
            }

            _movedPaths.Clear();
            _movedColours.Clear();
            _movedRays.Clear();
            _movedHandles = handles;
            _movedList = list;
            _movedTolerance = tolerance;
            Gather(list, handles, tolerance, _movedPaths, _movedColours, _movedRays);
            return _movedPaths;
        }

        /// <summary>
        /// Gets the XLINEs and RAYs of the objects the last <see cref="Paths(CadDocument, CadSpace, IReadOnlyList{string}, double)"/>
        /// outlined, transformed and cut to a view; the colour of each added to <paramref name="colours"/> where it is given.
        /// </summary>
        public IReadOnlyList<double[]> MovedRays(Affine2 moved, DisplayBox view, List<int> colours = null) =>
            Clip(_movedRays, moved, view, colours);

        /// <summary>
        /// Gets the colours of the outlines one of the <c>Paths</c> methods gave, one for each, as 0xRRGGBB: the colour each
        /// object is drawn in, so that a copy dragged shows in it, as AutoCAD 2021 drags objects in their own colours. Empty for
        /// a list this ghost did not give.
        /// </summary>
        public IReadOnlyList<int> ColoursOf(IReadOnlyList<double[]> paths) =>
            ReferenceEquals(paths, _paths) ? _pathColours
            : ReferenceEquals(paths, _movedPaths) ? _movedColours
            : ReferenceEquals(paths, _dragPaths) ? _dragColours
            : (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>The outlines of the items of some objects of a display list, added to the paths with their colours, and the rays.</summary>
        private static void Gather(
            DisplayList2 list,
            IReadOnlyList<string> handles,
            double tolerance,
            List<double[]> paths,
            List<int> colours,
            List<DisplayRay2> rays
        )
        {
            if (handles.Count == 0)
            {
                return;
            }

            var wanted = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);

            foreach (DisplayItem2 item in list.Items)
            {
                if (item.Handle == null || !wanted.Contains(item.Handle))
                {
                    continue;
                }

                Outline(item, tolerance, paths, colours, rays);
            }
        }

        /// <summary>
        /// Gets the XLINEs and RAYs among the objects the last <see cref="Paths(CadDocument, CadSpace, double)"/> found, each
        /// transformed and cut to a view as the canvas draws them: X and Y of its two ends in drawing units, the transformation
        /// already applied; one that misses the view gives none. An XLINE level through the origin, moved by (10, 20), is the run from (-100, 20) to (100, 20) of
        /// the view from (-100, -100) to (100, 100).
        /// </summary>
        public IReadOnlyList<double[]> Rays(Affine2 moved, DisplayBox view, List<int> colours = null) =>
            Clip(_rays, moved, view, colours);

        /// <summary>
        /// Gets the outlines of the block an INSERT drags (<see cref="CadPrompt.Drag"/>), in the block's own coordinates, as
        /// <see cref="Paths(CadDocument, CadSpace, double)"/> gives those of objects: kept while the block's items stay and the
        /// tolerance changes little.
        /// </summary>
        public IReadOnlyList<double[]> Paths(CadBlockDrag drag, double tolerance)
        {
            IReadOnlyList<DisplayItem2> items = drag.Items;

            if (
                ReferenceEquals(items, _dragItems)
                && tolerance <= _dragTolerance * 2.0
                && tolerance >= _dragTolerance / 2.0
            )
            {
                return _dragPaths;
            }

            _dragPaths.Clear();
            _dragColours.Clear();
            _dragRays.Clear();
            _dragItems = items;
            _dragTolerance = tolerance;

            foreach (DisplayItem2 item in items)
            {
                Outline(item, tolerance, _dragPaths, _dragColours, _dragRays);
            }

            return _dragPaths;
        }

        /// <summary>Gets the XLINEs and RAYs of the block the last <see cref="Paths(CadBlockDrag, double)"/> outlined, transformed and cut to a view as <see cref="Rays"/> cuts those of objects.</summary>
        public IReadOnlyList<double[]> DragRays(Affine2 moved, DisplayBox view, List<int> colours = null) =>
            Clip(_dragRays, moved, view, colours);

        /// <summary>
        /// The outline of one display item, each a run of X and Y, added to the paths with the item's colour for each; an XLINE
        /// or a RAY added to the rays.
        /// </summary>
        private static void Outline(
            DisplayItem2 item,
            double tolerance,
            List<double[]> paths,
            List<int> colours,
            List<DisplayRay2> rays
        )
        {
            int count = paths.Count;
            Shape(item, tolerance, paths, rays);

            for (int i = count; i < paths.Count; i++)
            {
                colours.Add(item.Style?.Rgb ?? 0xFFFFFF);
            }
        }

        /// <summary>The runs of X and Y of one display item added to the paths; an XLINE or a RAY added to the rays.</summary>
        private static void Shape(DisplayItem2 item, double tolerance, List<double[]> paths, List<DisplayRay2> rays)
        {
            switch (item)
            {
                case DisplayPolyline2 chain:
                    var xy = new double[chain.Closed ? chain.Xy.Count + 2 : chain.Xy.Count];

                    for (int i = 0; i < chain.Xy.Count; i++)
                    {
                        xy[i] = chain.Xy[i];
                    }

                    if (chain.Closed && chain.Xy.Count >= 2)
                    {
                        xy[xy.Length - 2] = chain.Xy[0];
                        xy[xy.Length - 1] = chain.Xy[1];
                    }

                    paths.Add(xy);
                    break;
                case DisplaySegments2 segments:
                    for (int i = 0; i + 3 < segments.Xy.Count; i += 4)
                    {
                        paths.Add(new[] { segments.Xy[i], segments.Xy[i + 1], segments.Xy[i + 2], segments.Xy[i + 3] });
                    }

                    break;
                case DisplayArc2 arc:
                    paths.Add(arc.Flatten(tolerance));
                    break;
                case DisplayFill2 fill:
                    foreach (double[] ring in fill.Rings)
                    {
                        if (ring.Length >= 4)
                        {
                            var closed = new double[ring.Length + 2];
                            Array.Copy(ring, closed, ring.Length);
                            closed[ring.Length] = ring[0];
                            closed[ring.Length + 1] = ring[1];
                            paths.Add(closed);
                        }
                    }

                    break;
                case DisplayText2 text:
                    DisplayBox box = text.Bounds;
                    paths.Add(
                        new[]
                        {
                            box.MinX,
                            box.MinY,
                            box.MaxX,
                            box.MinY,
                            box.MaxX,
                            box.MaxY,
                            box.MinX,
                            box.MaxY,
                            box.MinX,
                            box.MinY,
                        }
                    );
                    break;
                case DisplayRay2 ray:
                    rays.Add(ray);
                    break;
            }
        }

        /// <summary>
        /// XLINEs and RAYs transformed and cut to a view, each the X and Y of its two ends, its colour added to the colours where
        /// they are given; one that misses the view gives none.
        /// </summary>
        private static IReadOnlyList<double[]> Clip(
            IReadOnlyList<DisplayRay2> rays,
            Affine2 moved,
            DisplayBox view,
            List<int> colours
        )
        {
            var parts = new List<double[]>();

            foreach (DisplayRay2 ray in rays)
            {
                double dx = moved.A * ray.Dx + moved.C * ray.Dy;
                double dy = moved.B * ray.Dx + moved.D * ray.Dy;

                // A transformation that flattens the way (a scale of nought) leaves no line to draw.
                if (!(dx * dx + dy * dy > 0.0))
                {
                    continue;
                }

                var placed = new DisplayRay2(
                    moved.X(ray.X, ray.Y),
                    moved.Y(ray.X, ray.Y),
                    dx,
                    dy,
                    ray.IsBothWays,
                    ray.Style,
                    ray.Handle,
                    ray.Layer
                );

                if (placed.TryClip(view, out double x0, out double y0, out double x1, out double y1))
                {
                    parts.Add(new[] { x0, y0, x1, y1 });
                    colours?.Add(ray.Style?.Rgb ?? 0xFFFFFF);
                }
            }

            return parts;
        }

        /// <summary>A linear map about a point: x' = b + M (x - b), M given by its columns (a, b) and (c, d).</summary>
        private static Affine2 About(GeoPoint2 p, double a, double b, double c, double d) =>
            new Affine2(a, b, c, d, p.X - (a * p.X + c * p.Y), p.Y - (b * p.X + d * p.Y));

        private void Take(IEnumerable<string> handles)
        {
            _handles.Clear();
            _handles.AddRange(handles);
            _handles.Sort(StringComparer.OrdinalIgnoreCase);
            _built = false;
        }

        private void Reset()
        {
            _handles.Clear();
            _paths.Clear();
            _pathColours.Clear();
            _rays.Clear();
            _built = false;
        }
    }
}
