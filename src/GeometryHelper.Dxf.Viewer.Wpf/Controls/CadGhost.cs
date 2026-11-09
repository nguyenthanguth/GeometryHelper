using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The ghost copy a transforming command draws while it waits for its second point, as AutoCAD drags the objects along:
    /// MOVE and COPY by the displacement from the base point to the cursor, ROTATE by the angle about the base point, SCALE by
    /// the distance from it as the factor, MIRROR across the line from the first point to the cursor. It follows the session's
    /// prompts to know the objects the command took (those selected before it started, or those "Select objects:" gathered),
    /// and keeps their outlines in drawing units so that a move of the mouse only transforms them.
    /// </summary>
    internal sealed class CadGhost
    {
        private readonly List<string> _handles = new List<string>();
        private readonly List<double[]> _paths = new List<double[]>();
        private readonly List<DisplayRay2> _rays = new List<DisplayRay2>();
        private CadCommand _command;
        private DisplayList2 _list;
        private double _tolerance;
        private bool _built;

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

            switch (session.Current.Name)
            {
                case "MOVE":
                case "COPY":
                    if (prompt.Kind == CadPromptKind.Point && message.StartsWith("Specify second point", StringComparison.Ordinal))
                    {
                        return Affine2.Translation(way.X, way.Y);
                    }

                    break;
                case "ROTATE":
                    if (prompt.Kind == CadPromptKind.Angle && message.StartsWith("Specify rotation angle", StringComparison.Ordinal))
                    {
                        double angle = Math.Atan2(way.Y, way.X);
                        double cos = Math.Cos(angle);
                        double sin = Math.Sin(angle);
                        return About(b, cos, sin, -sin, cos);
                    }

                    break;
                case "SCALE":
                    if (prompt.Kind == CadPromptKind.Distance && message.StartsWith("Specify scale factor", StringComparison.Ordinal) && way.Length > 0.0)
                    {
                        return About(b, way.Length, 0.0, 0.0, way.Length);
                    }

                    break;
                case "MIRROR":
                    if (prompt.Kind == CadPromptKind.Point && message.StartsWith("Specify second point of mirror line", StringComparison.Ordinal) && way.Length > 0.0)
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

            if (_built && ReferenceEquals(list, _list) && tolerance <= _tolerance * 2.0 && tolerance >= _tolerance / 2.0)
            {
                return _paths;
            }

            _paths.Clear();
            _rays.Clear();
            _list = list;
            _tolerance = tolerance;
            _built = true;

            if (_handles.Count == 0)
            {
                return _paths;
            }

            var wanted = new HashSet<string>(_handles, StringComparer.OrdinalIgnoreCase);

            foreach (DisplayItem2 item in list.Items)
            {
                if (item.Handle == null || !wanted.Contains(item.Handle))
                {
                    continue;
                }

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

                        _paths.Add(xy);
                        break;
                    case DisplaySegments2 segments:
                        for (int i = 0; i + 3 < segments.Xy.Count; i += 4)
                        {
                            _paths.Add(new[] { segments.Xy[i], segments.Xy[i + 1], segments.Xy[i + 2], segments.Xy[i + 3] });
                        }

                        break;
                    case DisplayArc2 arc:
                        _paths.Add(arc.Flatten(tolerance));
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
                                _paths.Add(closed);
                            }
                        }

                        break;
                    case DisplayText2 text:
                        DisplayBox box = text.Bounds;
                        _paths.Add(new[] { box.MinX, box.MinY, box.MaxX, box.MinY, box.MaxX, box.MaxY, box.MinX, box.MaxY, box.MinX, box.MinY });
                        break;
                    case DisplayRay2 ray:
                        _rays.Add(ray);
                        break;
                }
            }

            return _paths;
        }

        /// <summary>
        /// Gets the XLINEs and RAYs among the objects the last <see cref="Paths"/> found, each transformed and cut to a view as
        /// the canvas draws them: X and Y of its two ends in drawing units, the transformation already applied; one that misses
        /// the view gives none. An XLINE level through the origin, moved by (10, 20), is the run from (-100, 20) to (100, 20) of
        /// the view from (-100, -100) to (100, 100).
        /// </summary>
        public IReadOnlyList<double[]> Rays(Affine2 moved, DisplayBox view)
        {
            var parts = new List<double[]>();

            foreach (DisplayRay2 ray in _rays)
            {
                double dx = moved.A * ray.Dx + moved.C * ray.Dy;
                double dy = moved.B * ray.Dx + moved.D * ray.Dy;

                // A transformation that flattens the way (a scale of nought) leaves no line to draw.
                if (!(dx * dx + dy * dy > 0.0))
                {
                    continue;
                }

                var placed = new DisplayRay2(moved.X(ray.X, ray.Y), moved.Y(ray.X, ray.Y), dx, dy, ray.IsBothWays, ray.Style, ray.Handle, ray.Layer);

                if (placed.TryClip(view, out double x0, out double y0, out double x1, out double y1))
                {
                    parts.Add(new[] { x0, y0, x1, y1 });
                }
            }

            return parts;
        }

        /// <summary>A linear map about a point: x' = b + M (x - b), M given by its columns (a, b) and (c, d).</summary>
        private static Affine2 About(GeoPoint2 p, double a, double b, double c, double d)
            => new Affine2(a, b, c, d, p.X - (a * p.X + c * p.Y), p.Y - (b * p.X + d * p.Y));

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
            _rays.Clear();
            _built = false;
        }
    }
}
