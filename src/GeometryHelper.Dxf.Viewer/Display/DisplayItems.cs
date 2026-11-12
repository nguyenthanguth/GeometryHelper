using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>How a display item is drawn: its colour, its line weight and its dashes, all resolved from the entity, its layer and its block.</summary>
    public sealed class DisplayStyle
    {
        /// <summary>Makes a style.</summary>
        /// <param name="rgb">The colour as 0xRRGGBB.</param>
        /// <param name="lineWeight">The line weight in millimetres, nought for the thinnest line.</param>
        /// <param name="dashes">The dash pattern in drawing units, as on and off lengths in turn (an on length of nought is a dot); null for a continuous line.</param>
        public DisplayStyle(int rgb, double lineWeight, double[] dashes)
        {
            Rgb = rgb & 0xFFFFFF;
            LineWeight = lineWeight;
            Dashes = dashes;
        }

        /// <summary>Gets the colour as 0xRRGGBB.</summary>
        public int Rgb { get; }

        /// <summary>Gets the line weight in millimetres, nought for the thinnest line.</summary>
        public double LineWeight { get; }

        /// <summary>Gets the dash pattern in drawing units, or null for a continuous line.</summary>
        public double[] Dashes { get; }
    }

    /// <summary>One thing the canvas draws: a chain of segments, an arc, a fill, or a piece of text, in drawing coordinates, with the entity it came from.</summary>
    public abstract class DisplayItem2
    {
        /// <summary>Makes the base of an item.</summary>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity it came from, shared by every item of that entity.</param>
        /// <param name="layer">The layer it is drawn on.</param>
        protected DisplayItem2(DisplayStyle style, string handle, string layer)
        {
            Style = style;
            Handle = handle;
            Layer = layer;
        }

        /// <summary>Gets how the item is drawn.</summary>
        public DisplayStyle Style { get; }

        /// <summary>Gets the handle of the entity the item came from; an entity inside a block gives the handle of the outermost INSERT.</summary>
        public string Handle { get; }

        /// <summary>Gets the layer the item is drawn on.</summary>
        public string Layer { get; }

        /// <summary>Gets the box that holds the item.</summary>
        public abstract DisplayBox Bounds { get; }

        /// <summary>
        /// Gets or sets whether the item is a wall, a symbol or words of a feature control frame, which give the snaps no End,
        /// Mid, Center, Quadrant, Nearest or Perpendicular: AutoCAD 2021 gives a frame's End, Mid, Nearest and Perpendicular on
        /// the rectangles of its rows alone (probes GHTQ2 and GHTH2).
        /// </summary>
        internal bool IsFrameInside { get; set; }

        /// <summary>
        /// A copy of the item moved along X and Y, as the entity it came from would be drawn moved so; null for an item that
        /// is drawn anew rather than moved, as a text or an image.
        /// </summary>
        internal virtual DisplayItem2 Moved(double dx, double dy) => null;

        /// <summary>The numbers of X and Y in turn moved along X and Y.</summary>
        internal static double[] Moved(IReadOnlyList<double> xy, double dx, double dy)
        {
            var moved = new double[xy.Count];

            for (int i = 0; i + 1 < moved.Length; i += 2)
            {
                moved[i] = xy[i] + dx;
                moved[i + 1] = xy[i + 1] + dy;
            }

            return moved;
        }

        /// <summary>Loops moved along X and Y; none kept as none.</summary>
        internal static IReadOnlyList<double[]> Moved(IReadOnlyList<double[]> rings, double dx, double dy)
        {
            if (rings.Count == 0)
            {
                return rings;
            }

            var moved = new double[rings.Count][];

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] = Moved(rings[i], dx, dy);
            }

            return moved;
        }

        /// <summary>Gets the distance from a point to the item, the shortest to any part that is drawn; the distance to the nearest edge for a fill, nought inside it.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>The distance.</returns>
        public abstract double DistanceTo(double x, double y);

        /// <summary>Gets whether the item lies wholly inside a box, as a window selection asks.</summary>
        /// <param name="box">The box.</param>
        /// <returns>True when every part of the item is inside.</returns>
        public virtual bool IsInside(DisplayBox box) => box.Contains(Bounds);

        /// <summary>Gets whether any part of the item touches a box, as a crossing selection asks.</summary>
        /// <param name="box">The box.</param>
        /// <returns>True when the item crosses or lies in the box.</returns>
        public virtual bool Touches(DisplayBox box) => box.Intersects(Bounds);

        internal static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double length2 = dx * dx + dy * dy;
            double t = length2 <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((px - ax) * dx + (py - ay) * dy) / length2));
            double qx = ax + t * dx - px;
            double qy = ay + t * dy - py;
            return Math.Sqrt(qx * qx + qy * qy);
        }

        internal static bool SegmentTouchesBox(double ax, double ay, double bx, double by, DisplayBox box)
        {
            if (box.Contains(ax, ay) || box.Contains(bx, by))
            {
                return true;
            }

            if (
                (ax < box.MinX && bx < box.MinX)
                || (ax > box.MaxX && bx > box.MaxX)
                || (ay < box.MinY && by < box.MinY)
                || (ay > box.MaxY && by > box.MaxY)
            )
            {
                return false;
            }

            return Crosses(ax, ay, bx, by, box.MinX, box.MinY, box.MaxX, box.MinY)
                || Crosses(ax, ay, bx, by, box.MaxX, box.MinY, box.MaxX, box.MaxY)
                || Crosses(ax, ay, bx, by, box.MaxX, box.MaxY, box.MinX, box.MaxY)
                || Crosses(ax, ay, bx, by, box.MinX, box.MaxY, box.MinX, box.MinY);
        }

        private static bool Crosses(
            double ax,
            double ay,
            double bx,
            double by,
            double cx,
            double cy,
            double dx,
            double dy
        )
        {
            double d1 = Cross(cx, cy, dx, dy, ax, ay);
            double d2 = Cross(cx, cy, dx, dy, bx, by);
            double d3 = Cross(ax, ay, bx, by, cx, cy);
            double d4 = Cross(ax, ay, bx, by, dx, dy);
            return ((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0))
                && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0));
        }

        private static double Cross(double ax, double ay, double bx, double by, double px, double py) =>
            (bx - ax) * (py - ay) - (by - ay) * (px - ax);
    }

    /// <summary>A chain of straight segments, open or closed: a LINE, a polyline, a flattened spline or ellipse.</summary>
    public sealed class DisplayPolyline2 : DisplayItem2
    {
        private readonly double[] _xy;
        private readonly DisplayBox _bounds;

        /// <summary>Makes a chain.</summary>
        /// <param name="xy">The vertices as X and Y in turn; at least two.</param>
        /// <param name="closed">Whether the last vertex joins the first.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        public DisplayPolyline2(double[] xy, bool closed, DisplayStyle style, string handle, string layer)
            : base(style, handle, layer)
        {
            _xy = xy ?? throw new ArgumentNullException(nameof(xy));
            Closed = closed;
            DisplayBox box = DisplayBox.Empty;

            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                box = box.Add(xy[i], xy[i + 1]);
            }

            _bounds = box;
        }

        /// <summary>Gets the vertices as X and Y in turn.</summary>
        public IReadOnlyList<double> Xy => _xy;

        /// <summary>Gets the number of vertices.</summary>
        public int Count => _xy.Length / 2;

        /// <summary>Gets whether the last vertex joins the first.</summary>
        public bool Closed { get; }

        /// <summary>
        /// Gets whether the vertices are samples along a curve (an ellipse, a spline, an arc drawn under a skew) rather than
        /// corners of the entity, so that object snaps take only the chain's two ends as endpoints and none of its midpoints.
        /// </summary>
        public bool IsSampled { get; internal set; }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        internal override DisplayItem2 Moved(double dx, double dy) =>
            new DisplayPolyline2(Moved(_xy, dx, dy), Closed, Style, Handle, Layer) { IsSampled = IsSampled };

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double best = double.PositiveInfinity;
            int n = Count;

            if (n == 1)
            {
                return Math.Sqrt((x - _xy[0]) * (x - _xy[0]) + (y - _xy[1]) * (y - _xy[1]));
            }

            for (int i = 0; i + 1 < n; i++)
            {
                best = Math.Min(
                    best,
                    SegmentDistance(x, y, _xy[2 * i], _xy[2 * i + 1], _xy[2 * i + 2], _xy[2 * i + 3])
                );
            }

            if (Closed && n > 2)
            {
                best = Math.Min(best, SegmentDistance(x, y, _xy[2 * n - 2], _xy[2 * n - 1], _xy[0], _xy[1]));
            }

            return best;
        }

        /// <inheritdoc/>
        public override bool Touches(DisplayBox box)
        {
            if (!box.Intersects(_bounds))
            {
                return false;
            }

            int n = Count;

            if (n == 1)
            {
                return box.Contains(_xy[0], _xy[1]);
            }

            for (int i = 0; i + 1 < n; i++)
            {
                if (SegmentTouchesBox(_xy[2 * i], _xy[2 * i + 1], _xy[2 * i + 2], _xy[2 * i + 3], box))
                {
                    return true;
                }
            }

            return Closed && n > 2 && SegmentTouchesBox(_xy[2 * n - 2], _xy[2 * n - 1], _xy[0], _xy[1], box);
        }
    }

    /// <summary>
    /// Separate straight segments drawn alike, each from its own start to its own end: the lines of a hatch's pattern cut by
    /// its loops, kept as one item so that a hatch of 11 000 dashes is one thing to pick and to draw.
    /// </summary>
    public sealed class DisplaySegments2 : DisplayItem2
    {
        private readonly double[] _xy;
        private readonly DisplayBox _bounds;

        /// <summary>Makes a set of segments.</summary>
        /// <param name="xy">The segments, four numbers each: the X and Y of the start, then of the end.</param>
        /// <param name="style">How they are drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        public DisplaySegments2(double[] xy, DisplayStyle style, string handle, string layer)
            : base(style, handle, layer)
        {
            _xy = xy ?? throw new ArgumentNullException(nameof(xy));
            DisplayBox box = DisplayBox.Empty;

            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                box = box.Add(xy[i], xy[i + 1]);
            }

            _bounds = box;
        }

        /// <summary>Gets the segments, four numbers each: the start's X and Y, then the end's.</summary>
        public IReadOnlyList<double> Xy => _xy;

        /// <summary>Gets the number of segments.</summary>
        public int Count => _xy.Length / 4;

        /// <summary>
        /// Gets the loops of the hatch the segments are the pattern of, X and Y in turn, as the display list placed them: the
        /// canvas fills them with a tint where the lines come closer than 2 pixels, as a hatch of ANSI31 at spacing 3 does on
        /// a sheet of 80 metres shown whole. Empty where the segments have no loops.
        /// </summary>
        public IReadOnlyList<double[]> Rings { get; internal set; } = Array.Empty<double[]>();

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        internal override DisplayItem2 Moved(double dx, double dy) =>
            new DisplaySegments2(Moved(_xy, dx, dy), Style, Handle, Layer) { Rings = Moved(Rings, dx, dy) };

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double best = double.PositiveInfinity;

            for (int i = 0; i + 3 < _xy.Length; i += 4)
            {
                best = Math.Min(best, SegmentDistance(x, y, _xy[i], _xy[i + 1], _xy[i + 2], _xy[i + 3]));
            }

            return best;
        }

        /// <inheritdoc/>
        public override bool Touches(DisplayBox box)
        {
            if (!box.Intersects(_bounds))
            {
                return false;
            }

            for (int i = 0; i + 3 < _xy.Length; i += 4)
            {
                if (SegmentTouchesBox(_xy[i], _xy[i + 1], _xy[i + 2], _xy[i + 3], box))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A line without end: an XLINE, running both ways through its base point, or a RAY, running on from it. Its box reaches
    /// without end the ways it runs, so that every view it crosses draws it and a pick anywhere along it finds it; the box of
    /// its <see cref="DisplayList2"/> holds its base point alone, as AutoCAD's ZOOM Extents does. The canvas draws the part
    /// the view shows (<see cref="TryClip"/>).
    /// </summary>
    public sealed class DisplayRay2 : DisplayItem2
    {
        private readonly DisplayBox _bounds;

        /// <summary>Makes a line without end.</summary>
        /// <param name="x">The X of the base point.</param>
        /// <param name="y">The Y of the base point.</param>
        /// <param name="dx">The X of the way it runs; the way need not be of unit length.</param>
        /// <param name="dy">The Y of the way it runs.</param>
        /// <param name="bothWays">True for an XLINE, which runs both ways; false for a RAY, which runs on from its base point only.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        /// <exception cref="ArgumentException">Thrown when the way has no length or is not a finite number.</exception>
        public DisplayRay2(
            double x,
            double y,
            double dx,
            double dy,
            bool bothWays,
            DisplayStyle style,
            string handle,
            string layer
        )
            : base(style, handle, layer)
        {
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A line without end needs a finite way of some length.", nameof(dx));
            }

            X = x;
            Y = y;
            Dx = dx / length;
            Dy = dy / length;
            IsBothWays = bothWays;
            _bounds = new DisplayBox(Reach(x, Dx, -1.0), Reach(y, Dy, -1.0), Reach(x, Dx, 1.0), Reach(y, Dy, 1.0));
        }

        /// <summary>Gets the X of the base point.</summary>
        public double X { get; }

        /// <summary>Gets the Y of the base point.</summary>
        public double Y { get; }

        /// <summary>Gets the X of the way it runs, of unit length.</summary>
        public double Dx { get; }

        /// <summary>Gets the Y of the way it runs, of unit length.</summary>
        public double Dy { get; }

        /// <summary>Gets whether it runs both ways, as an XLINE; false for a RAY.</summary>
        public bool IsBothWays { get; }

        internal override DisplayItem2 Moved(double dx, double dy) =>
            new DisplayRay2(X + dx, Y + dy, Dx, Dy, IsBothWays, Style, Handle, Layer);

        /// <summary>
        /// Gets the box the line reaches: without end along each axis it runs along, its base point's along one it runs
        /// across. An XLINE level through (200, 300) has the box from minus to plus infinity in X and from 300 to 300 in Y.
        /// </summary>
        public override DisplayBox Bounds => _bounds;

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double t = (x - X) * Dx + (y - Y) * Dy;

            if (!IsBothWays && t < 0.0)
            {
                t = 0.0;
            }

            double qx = X + t * Dx - x;
            double qy = Y + t * Dy - y;
            return Math.Sqrt(qx * qx + qy * qy);
        }

        /// <inheritdoc/>
        public override bool Touches(DisplayBox box) => TryClip(box, out _, out _, out _, out _);

        /// <summary>
        /// Gets the part of the line inside a box, as the canvas draws it in the view: an XLINE level through (200, 300) in
        /// the box from (500, 290) to (510, 310) is the segment from (500, 300) to (510, 300). The ends run the way the line does.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="x0">The X of the start of the part.</param>
        /// <param name="y0">The Y of the start of the part.</param>
        /// <param name="x1">The X of the end of the part.</param>
        /// <param name="y1">The Y of the end of the part.</param>
        /// <returns>True when the line meets the box; false, the ends nought, when it passes by.</returns>
        public bool TryClip(DisplayBox box, out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;

            if (box.IsEmpty)
            {
                return false;
            }

            // The distances along the line between which it lies within the box: each pair of sides cuts the range down.
            double from = IsBothWays ? double.NegativeInfinity : 0.0;
            double to = double.PositiveInfinity;

            if (
                !Narrow(X, Dx, box.MinX, box.MaxX, ref from, ref to)
                || !Narrow(Y, Dy, box.MinY, box.MaxY, ref from, ref to)
            )
            {
                return false;
            }

            x0 = X + from * Dx;
            y0 = Y + from * Dy;
            x1 = X + to * Dx;
            y1 = Y + to * Dy;
            return true;
        }

        /// <summary>Cuts the range of distances along the line down to those at which one coordinate lies between two values.</summary>
        private static bool Narrow(double at, double way, double min, double max, ref double from, ref double to)
        {
            if (way == 0.0)
            {
                return at >= min && at <= max;
            }

            double a = (min - at) / way;
            double b = (max - at) / way;
            from = Math.Max(from, Math.Min(a, b));
            to = Math.Min(to, Math.Max(a, b));
            return from <= to;
        }

        /// <summary>How far one coordinate of the line reaches one way along its axis: without end where the line runs that way, else its base point's.</summary>
        private double Reach(double at, double way, double sign)
        {
            if (way == 0.0)
            {
                return at;
            }

            return way * sign > 0.0 || IsBothWays ? sign * double.PositiveInfinity : at;
        }
    }

    /// <summary>An arc of a circle, or a whole circle; the canvas flattens it to the screen's accuracy.</summary>
    public sealed class DisplayArc2 : DisplayItem2
    {
        private readonly DisplayBox _bounds;

        /// <summary>Makes an arc.</summary>
        /// <param name="cx">The X of the centre.</param>
        /// <param name="cy">The Y of the centre.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="start">The angle of its start in radians, from the X axis.</param>
        /// <param name="sweep">The angle it sweeps in radians, positive counter-clockwise; a magnitude of 2 pi or more is a whole circle.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        public DisplayArc2(
            double cx,
            double cy,
            double radius,
            double start,
            double sweep,
            DisplayStyle style,
            string handle,
            string layer
        )
            : base(style, handle, layer)
        {
            Cx = cx;
            Cy = cy;
            Radius = radius;
            Start = start;
            Sweep = Math.Abs(sweep) >= 2.0 * Math.PI - 1E-12 ? (sweep < 0.0 ? -2.0 * Math.PI : 2.0 * Math.PI) : sweep;
            _bounds = ComputeBounds();
        }

        /// <summary>Gets the X of the centre.</summary>
        public double Cx { get; }

        /// <summary>Gets the Y of the centre.</summary>
        public double Cy { get; }

        /// <summary>Gets the radius.</summary>
        public double Radius { get; }

        /// <summary>Gets the angle of the start in radians.</summary>
        public double Start { get; }

        /// <summary>Gets the angle swept in radians, positive counter-clockwise.</summary>
        public double Sweep { get; }

        /// <summary>Gets whether the arc is a whole circle.</summary>
        public bool IsCircle => Math.Abs(Sweep) >= 2.0 * Math.PI - 1E-12;

        internal override DisplayItem2 Moved(double dx, double dy) =>
            new DisplayArc2(Cx + dx, Cy + dy, Radius, Start, Sweep, Style, Handle, Layer);

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <summary>Gets the angle of a point seen from the centre, as the arc measures its own, within the sweep or not.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>Whether the point's direction is within the arc's sweep.</returns>
        public bool CoversDirection(double x, double y)
        {
            if (IsCircle)
            {
                return true;
            }

            double angle = Math.Atan2(y - Cy, x - Cx);
            return Covers(angle);
        }

        /// <summary>Gets the point of the arc at an angle.</summary>
        /// <param name="angle">The angle in radians.</param>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        public void PointAt(double angle, out double x, out double y)
        {
            x = Cx + Radius * Math.Cos(angle);
            y = Cy + Radius * Math.Sin(angle);
        }

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double dx = x - Cx;
            double dy = y - Cy;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (IsCircle || CoversDirection(x, y))
            {
                return Math.Abs(distance - Radius);
            }

            PointAt(Start, out double sx, out double sy);
            PointAt(Start + Sweep, out double ex, out double ey);
            return Math.Min(
                Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy)),
                Math.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey))
            );
        }

        /// <inheritdoc/>
        public override bool Touches(DisplayBox box)
        {
            if (!box.Intersects(_bounds))
            {
                return false;
            }

            double[] xy = Flatten(Math.Max(Radius * 1E-3, 1E-9));
            var chain = new DisplayPolyline2(xy, false, Style, Handle, Layer);
            return chain.Touches(box);
        }

        /// <summary>Gets the points of the arc as a chain whose chords stay within a tolerance of it.</summary>
        /// <param name="tolerance">The greatest distance of the arc from a chord, in drawing units.</param>
        /// <returns>The vertices as X and Y in turn, the start first and the end last.</returns>
        public double[] Flatten(double tolerance)
        {
            double r = Math.Max(Radius, 1E-12);
            double step = tolerance >= r ? Math.PI / 2.0 : 2.0 * Math.Acos(1.0 - tolerance / r);
            step = Math.Max(step, 2.0 * Math.PI / 4096.0);
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(Sweep) / step) + 1);
            var xy = new double[2 * n];

            for (int i = 0; i < n; i++)
            {
                double angle = Start + Sweep * i / (n - 1);
                xy[2 * i] = Cx + Radius * Math.Cos(angle);
                xy[2 * i + 1] = Cy + Radius * Math.Sin(angle);
            }

            return xy;
        }

        private bool Covers(double angle)
        {
            double sweep = Sweep;
            double from = sweep >= 0.0 ? Start : Start + sweep;
            double length = Math.Abs(sweep);
            double delta = angle - from;
            double twoPi = 2.0 * Math.PI;
            delta -= Math.Floor(delta / twoPi) * twoPi;
            return delta <= length;
        }

        private DisplayBox ComputeBounds()
        {
            if (IsCircle)
            {
                return new DisplayBox(Cx - Radius, Cy - Radius, Cx + Radius, Cy + Radius);
            }

            PointAt(Start, out double sx, out double sy);
            PointAt(Start + Sweep, out double ex, out double ey);
            DisplayBox box = DisplayBox.Empty.Add(sx, sy).Add(ex, ey);

            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                double angle = quadrant * Math.PI / 2.0;

                if (Covers(angle))
                {
                    PointAt(angle, out double qx, out double qy);
                    box = box.Add(qx, qy);
                }
            }

            return box;
        }
    }

    /// <summary>A filled area: a hatch, a solid, a wipeout. Its rings are filled by the even-odd rule, so a ring inside another is a hole.</summary>
    public sealed class DisplayFill2 : DisplayItem2
    {
        private readonly List<double[]> _rings;
        private readonly DisplayBox _bounds;

        /// <summary>Makes a fill.</summary>
        /// <param name="rings">The rings, each as X and Y in turn, closed implicitly.</param>
        /// <param name="isMask">Whether the fill hides what is under it and is drawn in the background colour (a WIPEOUT) rather than in the style's colour.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        public DisplayFill2(IEnumerable<double[]> rings, bool isMask, DisplayStyle style, string handle, string layer)
            : base(style, handle, layer)
        {
            _rings = new List<double[]>(rings);
            IsMask = isMask;
            DisplayBox box = DisplayBox.Empty;

            foreach (double[] ring in _rings)
            {
                for (int i = 0; i + 1 < ring.Length; i += 2)
                {
                    box = box.Add(ring[i], ring[i + 1]);
                }
            }

            _bounds = box;
        }

        /// <summary>Gets the rings, each as X and Y in turn.</summary>
        public IReadOnlyList<double[]> Rings => _rings;

        /// <summary>Gets whether the fill is a mask drawn in the background colour.</summary>
        public bool IsMask { get; }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        internal override DisplayItem2 Moved(double dx, double dy) =>
            new DisplayFill2(Moved(_rings, dx, dy), IsMask, Style, Handle, Layer);

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            if (Contains(x, y))
            {
                return 0.0;
            }

            double best = double.PositiveInfinity;

            foreach (double[] ring in _rings)
            {
                int n = ring.Length / 2;

                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    best = Math.Min(
                        best,
                        SegmentDistance(x, y, ring[2 * i], ring[2 * i + 1], ring[2 * j], ring[2 * j + 1])
                    );
                }
            }

            return best;
        }

        /// <summary>Gets whether a point is inside the fill by the even-odd rule.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>True when the point is inside.</returns>
        public bool Contains(double x, double y)
        {
            bool inside = false;

            foreach (double[] ring in _rings)
            {
                int n = ring.Length / 2;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = ring[2 * i];
                    double yi = ring[2 * i + 1];
                    double xj = ring[2 * j];
                    double yj = ring[2 * j + 1];

                    if (((yi > y) != (yj > y)) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        /// <inheritdoc/>
        public override bool Touches(DisplayBox box)
        {
            if (!box.Intersects(_bounds))
            {
                return false;
            }

            if (Contains(box.MinX, box.MinY) || Contains(box.MaxX, box.MaxY))
            {
                return true;
            }

            foreach (double[] ring in _rings)
            {
                int n = ring.Length / 2;

                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;

                    if (SegmentTouchesBox(ring[2 * i], ring[2 * i + 1], ring[2 * j], ring[2 * j + 1], box))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>A piece of text, placed by the point its alignment names; the canvas measures and draws it in a font.</summary>
    public sealed class DisplayText2 : DisplayItem2
    {
        private DisplayBox _bounds;
        private double _width = double.NaN;
        private double _descent;

        /// <summary>Makes a piece of text.</summary>
        /// <param name="text">The text, one or more lines separated by line feeds, with the format codes of an MTEXT already taken out.</param>
        /// <param name="x">The X of the alignment point.</param>
        /// <param name="y">The Y of the alignment point.</param>
        /// <param name="height">The height of a capital letter.</param>
        /// <param name="rotation">The angle of the baseline in radians.</param>
        /// <param name="widthFactor">The width of a letter relative to its height's usual width; 1 is the font's own.</param>
        /// <param name="horizontal">Where the point lies along the text: 0 left, 1 centre, 2 right.</param>
        /// <param name="vertical">Where the point lies up the text: 0 baseline, 1 bottom, 2 middle, 3 top.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        /// <param name="fontName">The font of its text style: the typeface a TrueType style names (Arial), else the file of its font
        /// (romans.shx, arial.ttf); null or empty for the canvas's own.</param>
        /// <param name="oblique">How far the letters lean, in radians clockwise from upright: 0.2618 for the 15 degrees of an
        /// italic look.</param>
        public DisplayText2(
            string text,
            double x,
            double y,
            double height,
            double rotation,
            double widthFactor,
            int horizontal,
            int vertical,
            DisplayStyle style,
            string handle,
            string layer,
            string fontName,
            double oblique
        )
            : this(
                text,
                x,
                y,
                height,
                rotation,
                widthFactor,
                horizontal,
                vertical,
                style,
                handle,
                layer,
                fontName,
                oblique,
                false
            ) { }

        /// <summary>Makes a piece of text that may be drawn backwards, as text in a mirrored block is.</summary>
        /// <param name="text">The text, one or more lines separated by line feeds.</param>
        /// <param name="x">The X of the alignment point.</param>
        /// <param name="y">The Y of the alignment point.</param>
        /// <param name="height">The height of a capital letter.</param>
        /// <param name="rotation">The angle of the baseline in radians.</param>
        /// <param name="widthFactor">The width of a letter relative to its height's usual width; 1 is the font's own.</param>
        /// <param name="horizontal">Where the point lies along the text: 0 left, 1 centre, 2 right.</param>
        /// <param name="vertical">Where the point lies up the text: 0 baseline, 1 bottom, 2 middle, 3 top.</param>
        /// <param name="style">How it is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        /// <param name="fontName">The font of its text style; null or empty for the canvas's own.</param>
        /// <param name="oblique">How far the letters lean, in radians clockwise from upright.</param>
        /// <param name="backward">Whether it is drawn backwards: see <see cref="IsBackward"/>.</param>
        public DisplayText2(
            string text,
            double x,
            double y,
            double height,
            double rotation,
            double widthFactor,
            int horizontal,
            int vertical,
            DisplayStyle style,
            string handle,
            string layer,
            string fontName,
            double oblique,
            bool backward
        )
            : base(style, handle, layer)
        {
            IsBackward = backward;
            FontName = string.IsNullOrEmpty(fontName) ? null : fontName;
            Oblique = oblique;
            Text = text ?? string.Empty;
            X = x;
            Y = y;
            Height = height;
            Rotation = rotation;
            WidthFactor = widthFactor <= 0.0 ? 1.0 : widthFactor;
            Horizontal = horizontal;
            Vertical = vertical;
            _bounds = ComputeBounds();
        }

        /// <summary>A text drawn as the strokes of its SHX font's letters, its width measured from their advances and its depth below the baseline the font's.</summary>
        internal DisplayText2(
            string text,
            double x,
            double y,
            double height,
            double rotation,
            double widthFactor,
            int horizontal,
            int vertical,
            DisplayStyle style,
            string handle,
            string layer,
            string fontName,
            double oblique,
            bool backward,
            IReadOnlyList<double[]> strokes,
            double width,
            double descent
        )
            : this(
                text,
                x,
                y,
                height,
                rotation,
                widthFactor,
                horizontal,
                vertical,
                style,
                handle,
                layer,
                fontName,
                oblique,
                backward
            )
        {
            Strokes = strokes;
            _width = width;
            _descent = descent;
            DisplayBox box = ComputeBounds();

            foreach (double[] stroke in strokes)
            {
                for (int i = 0; i + 1 < stroke.Length; i += 2)
                {
                    box = box.Add(stroke[i], stroke[i + 1]);
                }
            }

            _bounds = box;
        }

        /// <summary>
        /// Gets the strokes of the letters where the text is drawn in an SHX font, each a polyline of x, y pairs in the
        /// drawing's coordinates, the text's height, width factor, slant, alignment, angle and turning over already taken,
        /// then the lines a TEXT's <c>%%U</c>, <c>%%K</c> and <c>%%O</c> draw under, through and over its letters, one
        /// segment each; null where the canvas draws the text in <see cref="FontName"/>.
        /// </summary>
        public IReadOnlyList<double[]> Strokes { get; }

        /// <summary>Gets the text.</summary>
        public string Text { get; }

        /// <summary>Gets the X of the alignment point.</summary>
        public double X { get; }

        /// <summary>Gets the Y of the alignment point.</summary>
        public double Y { get; }

        /// <summary>Gets the height of a capital letter.</summary>
        public double Height { get; }

        /// <summary>Gets the angle of the baseline in radians.</summary>
        public double Rotation { get; }

        /// <summary>Gets the width factor.</summary>
        public double WidthFactor { get; }

        /// <summary>Gets where the point lies along the text: 0 left, 1 centre, 2 right.</summary>
        public int Horizontal { get; }

        /// <summary>Gets where the point lies up the text: 0 baseline, 1 bottom, 2 middle, 3 top.</summary>
        public int Vertical { get; }

        /// <summary>Gets the font of the text's style, a typeface or a font file; null for the canvas's own.</summary>
        public string FontName { get; }

        /// <summary>Gets how far the letters lean, in radians clockwise from upright.</summary>
        public double Oblique { get; }

        /// <summary>
        /// Gets what an MTEXT is drawn as, in place of <see cref="Text"/> drawn whole: its mask (a <see cref="DisplayFill2"/>)
        /// and its frame first, then a text for each run of each line where AutoCAD sets it, in its own font, face, height,
        /// width, slant and colour, the parts of a stacked fraction among them, and a line under, over or through a run its
        /// codes line and for the bar of a fraction; null for a TEXT, drawn whole. The text keeps its words, height and
        /// attachment as one text has them, for what reads it; its box is that of the parts. "AB{\C1;CD}" 2.5 high is drawn
        /// as AB in the MTEXT's colour and CD in red after it; "II II II" 20 wide in letters 10 high as "II II" and, 16.67
        /// below it, "II".
        /// </summary>
        public IReadOnlyList<DisplayItem2> Parts { get; private set; }

        /// <summary>Gets whether the canvas draws the letters in the bold face of <see cref="FontName"/>, as a run of an MTEXT coded <c>|b1</c> is drawn.</summary>
        public bool IsBold { get; internal set; }

        /// <summary>Gets whether the canvas draws the letters in the italic face of <see cref="FontName"/>, as a run of an MTEXT coded <c>|i1</c> is drawn.</summary>
        public bool IsItalic { get; internal set; }

        /// <summary>
        /// Gets whether the text is drawn backwards: laid out and drawn as always in its own frame (along <see cref="Rotation"/>
        /// and up it), that frame then turned over along its baseline, so that the letters run against <see cref="Rotation"/>
        /// and read as in a mirror. A text in a block inserted mirrored, written backwards or upside down is drawn so; a canvas
        /// draws it by scaling its frame by -1 along the baseline after turning it.
        /// </summary>
        public bool IsBackward { get; }

        /// <summary>Gets the number of lines of the text.</summary>
        public int LineCount
        {
            get
            {
                int lines = 1;

                foreach (char c in Text)
                {
                    if (c == '\n')
                    {
                        lines++;
                    }
                }

                return lines;
            }
        }

        /// <summary>
        /// Gets the width the text is taken to have for picking and extents: of its widest line, by the advances of its letters
        /// where it is drawn in an SHX font (<see cref="Strokes"/>); else from its height and the number of its letters, 0.6 of
        /// the height a letter, as a monospace font would, the canvas measuring the real width.
        /// </summary>
        public double EstimatedWidth
        {
            get
            {
                if (!double.IsNaN(_width))
                {
                    return _width;
                }

                int longest = 0;

                foreach (string line in Text.Split('\n'))
                {
                    longest = Math.Max(longest, line.Length);
                }

                return longest * Height * 0.6 * WidthFactor;
            }
        }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <summary>Gives the text the parts it is drawn as (<see cref="Parts"/>), its box that of the parts; its own where none has a box.</summary>
        internal void SetParts(IReadOnlyList<DisplayItem2> parts)
        {
            Parts = parts;
            DisplayBox all = DisplayBox.Empty;
            bool any = false;

            foreach (DisplayItem2 part in parts)
            {
                DisplayBox box = part.Bounds;

                if (box.Width >= 0.0 && box.Height >= 0.0)
                {
                    all = all.Add(box.MinX, box.MinY).Add(box.MaxX, box.MaxY);
                    any = true;
                }
            }

            if (any)
            {
                _bounds = all;
            }
        }

        /// <summary>Gives a text the canvas draws its measured width and its depth below the baseline, for picking and extents, where the letters of its font's file were measured.</summary>
        internal void SetMeasure(double width, double descent)
        {
            _width = width;
            _descent = descent;
            _bounds = ComputeBounds();
        }

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            if (Parts != null)
            {
                double nearest = double.PositiveInfinity;

                foreach (DisplayItem2 part in Parts)
                {
                    nearest = Math.Min(nearest, part.DistanceTo(x, y));
                }

                return nearest;
            }

            // The text's box in its own frame: along the baseline and up it.
            double cos = Math.Cos(-Rotation);
            double sin = Math.Sin(-Rotation);
            double lx = (x - X) * cos - (y - Y) * sin;
            double ly = (x - X) * sin + (y - Y) * cos;
            GetFrame(out double x0, out double y0, out double x1, out double y1);
            double dx = Math.Max(Math.Max(x0 - lx, lx - x1), 0.0);
            double dy = Math.Max(Math.Max(y0 - ly, ly - y1), 0.0);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void GetFrame(out double x0, out double y0, out double x1, out double y1)
        {
            double width = EstimatedWidth;
            x0 =
                Horizontal == 0 ? 0.0
                : Horizontal == 1 ? -width / 2.0
                : -width;
            x1 = x0 + width;

            if (IsBackward)
            {
                double left = -x1;
                x1 = -x0;
                x0 = left;
            }

            // The block of lines in its own frame: the first line's capitals reach up to Height, the last baseline is
            // (lines - 1) line steps below the first, and an SHX font's letters down to its depth below that; the vertical mode
            // then says which of its levels is at nought, the bottom that depth.
            double top = Height;
            double bottom = -(LineCount - 1) * 1.25 * Height;
            double shift =
                Vertical == 0 ? 0.0
                : Vertical == 1 ? -(bottom - _descent)
                : Vertical == 2 ? -(top + bottom) / 2.0
                : -top;
            y0 = bottom - _descent + shift;
            y1 = top + shift;
        }

        private DisplayBox ComputeBounds()
        {
            GetFrame(out double x0, out double y0, out double x1, out double y1);
            double cos = Math.Cos(Rotation);
            double sin = Math.Sin(Rotation);
            DisplayBox box = DisplayBox.Empty;

            foreach (
                double[] corner in new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } }
            )
            {
                box = box.Add(X + corner[0] * cos - corner[1] * sin, Y + corner[0] * sin + corner[1] * cos);
            }

            return box;
        }
    }

    /// <summary>
    /// The picture of an IMAGE: the file it shows laid on the parallelogram of its corner and its two sides, clipped by its
    /// boundary. A picture of 40 by 30 pixels placed at (10, 20) with a pixel 0.25 across is the parallelogram from (10, 20)
    /// along (10, 0) and (0, 7.5). It is never picked by a point: the frame drawn as its own item is, as AutoCAD picks an
    /// image by its frame; a window or a crossing takes it by its box.
    /// </summary>
    public sealed class DisplayImage2 : DisplayItem2
    {
        private readonly DisplayBox _bounds;

        /// <summary>Makes the picture of an image.</summary>
        /// <param name="x">The X of the corner the picture's lower left pixel sits at.</param>
        /// <param name="y">The Y of that corner.</param>
        /// <param name="ux">The X of the side along the picture's width, the whole of it.</param>
        /// <param name="uy">The Y of that side.</param>
        /// <param name="vx">The X of the side up the picture's height, the whole of it.</param>
        /// <param name="vy">The Y of that side.</param>
        /// <param name="clip">The rings it is clipped by, each as X and Y in turn; null where it shows the whole picture.</param>
        /// <param name="clipInverted">Whether it shows what lies outside the rings rather than inside.</param>
        /// <param name="filePath">The full path of the picture file found; null where none was found or it is unloaded.</param>
        /// <param name="fileName">The path the drawing names the picture by, shown in the frame of one not found.</param>
        /// <param name="style">How its frame is drawn.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="layer">The layer.</param>
        public DisplayImage2(
            double x,
            double y,
            double ux,
            double uy,
            double vx,
            double vy,
            IReadOnlyList<double[]> clip,
            bool clipInverted,
            string filePath,
            string fileName,
            DisplayStyle style,
            string handle,
            string layer
        )
            : base(style, handle, layer)
        {
            X = x;
            Y = y;
            Ux = ux;
            Uy = uy;
            Vx = vx;
            Vy = vy;
            Clip = clip;
            IsClipInverted = clipInverted;
            FilePath = filePath;
            FileName = fileName ?? string.Empty;
            DisplayBox box = DisplayBox.Empty;

            if (clip != null && !clipInverted)
            {
                foreach (double[] ring in clip)
                {
                    for (int i = 0; i + 1 < ring.Length; i += 2)
                    {
                        box = box.Add(ring[i], ring[i + 1]);
                    }
                }
            }

            if (box.IsEmpty)
            {
                box = box.Add(x, y).Add(x + ux, y + uy).Add(x + ux + vx, y + uy + vy).Add(x + vx, y + vy);
            }

            _bounds = box;
        }

        /// <summary>Gets the X of the corner the picture's lower left pixel sits at.</summary>
        public double X { get; }

        /// <summary>Gets the Y of that corner.</summary>
        public double Y { get; }

        /// <summary>Gets the X of the side along the picture's width.</summary>
        public double Ux { get; }

        /// <summary>Gets the Y of the side along the picture's width.</summary>
        public double Uy { get; }

        /// <summary>Gets the X of the side up the picture's height.</summary>
        public double Vx { get; }

        /// <summary>Gets the Y of the side up the picture's height.</summary>
        public double Vy { get; }

        /// <summary>Gets the rings the picture is clipped by, each as X and Y in turn; null where it shows the whole picture.</summary>
        public IReadOnlyList<double[]> Clip { get; }

        /// <summary>Gets whether the picture shows what lies outside its rings rather than inside.</summary>
        public bool IsClipInverted { get; }

        /// <summary>Gets the full path of the picture file found; null where none was found or the picture is unloaded.</summary>
        public string FilePath { get; }

        /// <summary>Gets the path the drawing names the picture by.</summary>
        public string FileName { get; }

        /// <summary>Gets whether the picture's transparent pixels let what is under it show (the IMAGE's transparency on).</summary>
        public bool IsTransparent { get; internal set; }

        /// <summary>Gets the brightness of the picture, 0 to 100, 50 as it is.</summary>
        public int Brightness { get; internal set; } = 50;

        /// <summary>Gets the contrast of the picture, 0 to 100, 50 as it is.</summary>
        public int Contrast { get; internal set; } = 50;

        /// <summary>Gets how far the picture is faded into the background, 0 to 100, 0 not at all.</summary>
        public int Fade { get; internal set; }

        /// <summary>Gets the time the picture file was last written, in ticks, when the item was made, so that a picture changed on disk is read again; nought for none.</summary>
        public long FileStamp { get; internal set; }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <summary>Gets an infinite distance: a picture is picked by its frame, never by a point on it.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>Infinity.</returns>
        public override double DistanceTo(double x, double y) => double.PositiveInfinity;
    }
}
