using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The object snaps that may be taken, as AutoCAD's OSMODE sets them.</summary>
    [Flags]
    public enum CadSnapModes
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>The ends of lines, arcs and polyline segments, and the corners of polylines and fills.</summary>
        Endpoint = 1,

        /// <summary>The middle of a line, an arc or a polyline segment.</summary>
        Midpoint = 2,

        /// <summary>The centre of an arc or a circle.</summary>
        Center = 4,

        /// <summary>The points of a circle or an arc at 0, 90, 180 and 270 degrees.</summary>
        Quadrant = 8,

        /// <summary>Where two objects cross.</summary>
        Intersection = 16,

        /// <summary>The foot of the perpendicular from the base point to an object.</summary>
        Perpendicular = 32,

        /// <summary>Where a line from the base point touches a circle or an arc.</summary>
        Tangent = 64,

        /// <summary>The point of an object nearest the cursor.</summary>
        Nearest = 128,

        /// <summary>A POINT object.</summary>
        Node = 256,

        /// <summary>The centroid of the area a closed polyline holds, taken with the cursor on the polyline or near the centroid.</summary>
        GeometricCenter = 512,

        /// <summary>
        /// The eight snaps the presenter had first, Endpoint to Nearest; <see cref="Node"/> and <see cref="GeometricCenter"/>
        /// are not in it, so that a set kept as "every snap but Nearest" takes the same points as before they came.
        /// </summary>
        All = Endpoint | Midpoint | Center | Quadrant | Intersection | Perpendicular | Tangent | Nearest,
    }

    /// <summary>Which snap gave a point.</summary>
    public enum CadSnapKind
    {
        /// <summary>No snap: the cursor itself.</summary>
        None,

        /// <summary>An endpoint.</summary>
        Endpoint,

        /// <summary>A midpoint.</summary>
        Midpoint,

        /// <summary>A centre.</summary>
        Center,

        /// <summary>A quadrant.</summary>
        Quadrant,

        /// <summary>An intersection.</summary>
        Intersection,

        /// <summary>A perpendicular foot.</summary>
        Perpendicular,

        /// <summary>A tangent point.</summary>
        Tangent,

        /// <summary>The nearest point of an object.</summary>
        Nearest,

        /// <summary>A point of the snap grid.</summary>
        Grid,

        /// <summary>The cursor held level or upright from the base point (ORTHO).</summary>
        Ortho,

        /// <summary>The cursor held on a polar angle from the base point (POLAR).</summary>
        Polar,

        /// <summary>A POINT object (NODe).</summary>
        Node,

        /// <summary>The centroid of a closed polyline (GCEn).</summary>
        GeometricCenter,

        /// <summary>The insertion point of a block or a text (INSertion): drawn in Drafting Settings; no snap gives it yet.</summary>
        Insertion,

        /// <summary>A point on the extension of a line or an arc (EXTension): drawn in Drafting Settings; no snap gives it yet.</summary>
        Extension,

        /// <summary>Where two objects would cross if extended (APParent intersection): drawn in Drafting Settings; no snap gives it yet.</summary>
        ApparentIntersection,

        /// <summary>A point on a line parallel to another (PARallel): drawn in Drafting Settings; no snap gives it yet.</summary>
        Parallel,
    }

    /// <summary>The point a snap gave, which snap gave it and, for an object snap, the object.</summary>
    public sealed class CadSnapResult
    {
        internal CadSnapResult(CadSnapKind kind, GeoPoint2 point, string handle)
        {
            Kind = kind;
            Point = point;
            Handle = handle;
        }

        /// <summary>Gets which snap gave the point; <see cref="CadSnapKind.None"/> for the cursor itself.</summary>
        public CadSnapKind Kind { get; }

        /// <summary>Gets the point, in drawing units.</summary>
        public GeoPoint2 Point { get; }

        /// <summary>Gets the handle of the object snapped to, or null for a grid, ortho, polar or no snap.</summary>
        public string Handle { get; }

        /// <inheritdoc/>
        public override string ToString() => Kind + " " + Point;
    }

    /// <summary>
    /// Object snaps, ortho, polar and grid snap, as AutoCAD's: given the cursor, the size of the aperture and the point a
    /// rubber band is drawn from, the point a click takes. Snaps are worked out from the display items under the aperture,
    /// through a space's <see cref="PickIndex"/>, so an INSERT's lines and arcs are snapped to where it places them.
    /// </summary>
    /// <remarks>
    /// AutoCAD's order is kept: a snap to a point near the cursor (endpoint, midpoint, centre, node, quadrant, intersection,
    /// geometric centre) wins, the nearest of them first; then a snap the object under the cursor gives wherever its point
    /// lies (perpendicular, tangent, the centre of a circle whose rim is under the cursor and the centroid of a closed
    /// polyline under it); then nearest. A POINT is a node, not an endpoint, as in AutoCAD. Where no object snap is found, the
    /// cursor is put on the grid, then held level or upright (ortho) or on a polar angle from the base point.
    /// </remarks>
    public sealed class ObjectSnap
    {
        // The snaps of the point being worked out: the override, else the modes while snaps are on.
        private CadSnapModes _active;

        /// <summary>Makes the snaps of AutoCAD's default: endpoint, midpoint, centre, quadrant and intersection, on.</summary>
        /// <param name="tolerance">The tolerance of the comparisons: whether lines are parallel, whether a point lies on an arc's sweep.</param>
        public ObjectSnap(Tolerance tolerance)
        {
            Tolerance = tolerance;
            Modes = CadSnapModes.Endpoint | CadSnapModes.Midpoint | CadSnapModes.Center | CadSnapModes.Quadrant | CadSnapModes.Intersection;
            IsOn = true;
            PolarAngle = Math.PI / 2.0;
            GridX = 10.0;
            GridY = 10.0;
        }

        /// <summary>Gets the tolerance of the comparisons.</summary>
        public Tolerance Tolerance { get; }

        /// <summary>Gets or sets the object snaps that may be taken.</summary>
        public CadSnapModes Modes { get; set; }

        /// <summary>Gets or sets whether object snaps are on (OSNAP, F3).</summary>
        public bool IsOn { get; set; }

        /// <summary>
        /// Gets or sets the object snaps of the next point alone, as AutoCAD's override typed at a point prompt (END, MID) or
        /// chosen from the Shift+right-click menu: taken in place of <see cref="Modes"/>, whether <see cref="IsOn"/> is on or
        /// off; <see cref="CadSnapModes.None"/> takes no object snap for that point (NONe). Null, as it is until set, for no
        /// override. The window clears it once the point is taken.
        /// </summary>
        public CadSnapModes? Override { get; set; }

        /// <summary>Gets or sets whether the cursor is held level or upright from the base point (ORTHO, F8).</summary>
        public bool Ortho { get; set; }

        /// <summary>Gets or sets whether the cursor is held on a polar angle from the base point when it comes near one (POLAR, F10).</summary>
        public bool Polar { get; set; }

        /// <summary>Gets or sets the polar increment in radians; a quarter turn until set.</summary>
        public double PolarAngle { get; set; }

        /// <summary>Gets or sets whether the cursor is put on the snap grid (SNAP, F9).</summary>
        public bool GridSnap { get; set; }

        /// <summary>Gets or sets the spacing of the snap grid along X; 10 until set.</summary>
        public double GridX { get; set; }

        /// <summary>Gets or sets the spacing of the snap grid along Y; 10 until set.</summary>
        public double GridY { get; set; }

        /// <summary>Gets or sets the point the snap grid runs through, in the coordinates of <see cref="Ucs"/>, as AutoCAD's SNAPBASE; the origin until set.</summary>
        public GeoPoint2 GridOrigin { get; set; } = GeoPoint2.Origin;

        /// <summary>
        /// Gets or sets the user coordinate system the grid, ortho and polar tracking follow, as AutoCAD's do: the grid runs
        /// along its axes, ortho holds the cursor along them, and polar angles are measured from its X axis. Object snaps give
        /// world points whatever it is. The world until set; null sets the world.
        /// </summary>
        public CadUcs Ucs
        {
            get => _ucs;
            set => _ucs = value ?? CadUcs.World;
        }

        private CadUcs _ucs = CadUcs.World;

        /// <summary>Gets the point a click at the cursor takes.</summary>
        /// <param name="index">The index of the space under the cursor.</param>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <param name="aperture">How near a snap point or object must come, in drawing units: the aperture in pixels divided by the view's pixels per unit.</param>
        /// <param name="basePoint">The point the rubber band is drawn from, which perpendicular, tangent, ortho and polar work from; null for none.</param>
        /// <returns>The point and the snap that gave it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the index is null.</exception>
        public CadSnapResult Snap(PickIndex index, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint = null)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            _active = Override ?? (IsOn ? Modes : CadSnapModes.None);

            if (_active != CadSnapModes.None && aperture > 0.0)
            {
                CadSnapResult found = ObjectSnapAt(index, cursor, aperture, basePoint);

                if (found != null)
                {
                    return found;
                }
            }

            // The grid, ortho and polar work in the user coordinate system, the point taken back to the world's at the end.
            CadUcs ucs = _ucs;
            GeoPoint2 point = ucs.ToUcs(cursor);
            basePoint = basePoint.HasValue ? ucs.ToUcs(basePoint.Value) : basePoint;
            CadSnapKind kind = CadSnapKind.None;

            if (GridSnap && GridX > 0.0 && GridY > 0.0)
            {
                point = new GeoPoint2(
                    GridOrigin.X + Math.Round((point.X - GridOrigin.X) / GridX, MidpointRounding.AwayFromZero) * GridX,
                    GridOrigin.Y + Math.Round((point.Y - GridOrigin.Y) / GridY, MidpointRounding.AwayFromZero) * GridY);
                kind = CadSnapKind.Grid;
            }

            if (basePoint.HasValue)
            {
                GeoPoint2 from = basePoint.Value;
                double dx = point.X - from.X;
                double dy = point.Y - from.Y;

                if (Ortho)
                {
                    point = Math.Abs(dx) >= Math.Abs(dy) ? new GeoPoint2(point.X, from.Y) : new GeoPoint2(from.X, point.Y);
                    kind = CadSnapKind.Ortho;
                }
                else if (Polar && PolarAngle > Tolerance.EqualAngleRad)
                {
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    double angle = Math.Atan2(dy, dx);
                    double held = Math.Round(angle / PolarAngle, MidpointRounding.AwayFromZero) * PolarAngle;
                    double along = length * Math.Cos(angle - held);
                    double off = Math.Abs(length * Math.Sin(angle - held));

                    // The cursor is held on the polar ray only when it comes within the aperture of it, as AutoCAD tracks it.
                    if (length > 0.0 && off <= aperture)
                    {
                        point = new GeoPoint2(from.X + along * Math.Cos(held), from.Y + along * Math.Sin(held));
                        kind = CadSnapKind.Polar;
                    }
                }
            }

            return new CadSnapResult(kind, ucs.ToWorld(point), null);
        }

        private CadSnapResult ObjectSnapAt(PickIndex index, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint)
        {
            var box = new DisplayBox(cursor.X - aperture, cursor.Y - aperture, cursor.X + aperture, cursor.Y + aperture);
            IReadOnlyList<DisplayItem2> items = index.ItemsNear(box);
            var near = new List<Candidate>();
            var onObject = new List<Candidate>();
            var nearest = new List<Candidate>();
            var curves = new List<Curve>();

            foreach (DisplayItem2 item in items)
            {
                switch (item)
                {
                    case DisplayPolyline2 chain:
                        AddChain(chain.Xy, chain.Count, chain.Closed, chain.IsSampled, item, cursor, aperture, basePoint, near, onObject, nearest, curves);
                        break;
                    case DisplayFill2 fill:
                        foreach (double[] ring in fill.Rings)
                        {
                            AddChain(ring, ring.Length / 2, true, false, item, cursor, aperture, basePoint, near, onObject, nearest, curves);
                        }

                        break;
                    case DisplayArc2 arc:
                        AddArc(arc, cursor, aperture, basePoint, near, onObject, nearest, curves);
                        break;
                }
            }

            if (Has(CadSnapModes.Intersection))
            {
                AddIntersections(curves, cursor, aperture, near);
            }

            Candidate best = Best(near) ?? Best(onObject) ?? Best(nearest);
            return best == null ? null : new CadSnapResult(best.Kind, best.Point, best.Handle);
        }

        private bool Has(CadSnapModes mode) => (_active & mode) != 0;

        private void AddChain(IReadOnlyList<double> xy, int count, bool closed, bool sampled, DisplayItem2 item, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint,
            List<Candidate> near, List<Candidate> onObject, List<Candidate> nearest, List<Curve> curves)
        {
            if (count == 0)
            {
                return;
            }

            if (count == 1)
            {
                // A chain of one point is a POINT: a node, as in AutoCAD, not an endpoint.
                if (Has(CadSnapModes.Node))
                {
                    Near(near, CadSnapKind.Node, new GeoPoint2(xy[0], xy[1]), item.Handle, cursor, aperture);
                }

                return;
            }

            if (Has(CadSnapModes.Endpoint))
            {
                for (int i = 0; i < count; i++)
                {
                    if (sampled && (closed || (i > 0 && i < count - 1)))
                    {
                        continue;
                    }

                    Near(near, CadSnapKind.Endpoint, new GeoPoint2(xy[2 * i], xy[2 * i + 1]), item.Handle, cursor, aperture);
                }
            }

            int segments = closed && count > 2 ? count : count - 1;
            bool under = false;

            for (int i = 0; i < segments; i++)
            {
                var a = new GeoPoint2(xy[2 * i], xy[2 * i + 1]);
                int j = (i + 1) % count;
                var b = new GeoPoint2(xy[2 * j], xy[2 * j + 1]);

                if (!sampled && Has(CadSnapModes.Midpoint))
                {
                    Near(near, CadSnapKind.Midpoint, new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0), item.Handle, cursor, aperture);
                }

                GeoPoint2 foot = Foot(cursor, a, b, out _);
                double reach = (foot - cursor).Length;

                if (reach > aperture)
                {
                    continue;
                }

                under = true;
                curves.Add(Curve.Segment(a, b, item.Handle));

                if (Has(CadSnapModes.Nearest))
                {
                    nearest.Add(new Candidate(CadSnapKind.Nearest, foot, item.Handle, reach));
                }

                if (basePoint.HasValue && Has(CadSnapModes.Perpendicular))
                {
                    GeoPoint2 perpendicular = Foot(basePoint.Value, a, b, out double t);
                    double slack = Tolerance.EqualPoint / Math.Max((b - a).Length, Tolerance.EqualPoint);

                    if (t >= -slack && t <= 1.0 + slack)
                    {
                        onObject.Add(new Candidate(CadSnapKind.Perpendicular, perpendicular, item.Handle, reach));
                    }
                }
            }

            // The centroid of a closed polyline: near the cursor, or with the cursor on the polyline, as the centre of a circle is.
            if (Has(CadSnapModes.GeometricCenter) && closed && count > 2 && item is DisplayPolyline2 && Centroid(xy, count, out GeoPoint2 centroid))
            {
                Near(near, CadSnapKind.GeometricCenter, centroid, item.Handle, cursor, aperture);

                if (under)
                {
                    onObject.Add(new Candidate(CadSnapKind.GeometricCenter, centroid, item.Handle, (centroid - cursor).Length));
                }
            }
        }

        /// <summary>The centroid of the area a ring holds, worked out from its first point; false where the ring holds no area.</summary>
        private bool Centroid(IReadOnlyList<double> xy, int count, out GeoPoint2 centroid)
        {
            double x0 = xy[0];
            double y0 = xy[1];
            double twice = 0.0;
            double cx = 0.0;
            double cy = 0.0;
            double perimeter = 0.0;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                double ax = xy[2 * i] - x0;
                double ay = xy[2 * i + 1] - y0;
                double bx = xy[2 * j] - x0;
                double by = xy[2 * j + 1] - y0;
                double cross = ax * by - bx * ay;
                twice += cross;
                cx += (ax + bx) * cross;
                cy += (ay + by) * cross;
                perimeter += Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            }

            // A ring whose area is no more than a strip of the point tolerance along its outline holds none: a sliver has no centroid.
            if (Math.Abs(twice) / 2.0 <= Tolerance.EqualPoint * perimeter)
            {
                centroid = default;
                return false;
            }

            centroid = new GeoPoint2(x0 + cx / (3.0 * twice), y0 + cy / (3.0 * twice));
            return true;
        }

        private void AddArc(DisplayArc2 arc, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint, List<Candidate> near, List<Candidate> onObject, List<Candidate> nearest, List<Curve> curves)
        {
            var center = new GeoPoint2(arc.Cx, arc.Cy);
            double reach = arc.DistanceTo(cursor.X, cursor.Y);

            if (!arc.IsCircle)
            {
                arc.PointAt(arc.Start, out double sx, out double sy);
                arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
                arc.PointAt(arc.Start + arc.Sweep / 2.0, out double mx, out double my);

                if (Has(CadSnapModes.Endpoint))
                {
                    Near(near, CadSnapKind.Endpoint, new GeoPoint2(sx, sy), arc.Handle, cursor, aperture);
                    Near(near, CadSnapKind.Endpoint, new GeoPoint2(ex, ey), arc.Handle, cursor, aperture);
                }

                if (Has(CadSnapModes.Midpoint))
                {
                    Near(near, CadSnapKind.Midpoint, new GeoPoint2(mx, my), arc.Handle, cursor, aperture);
                }
            }

            if (Has(CadSnapModes.Center))
            {
                Near(near, CadSnapKind.Center, center, arc.Handle, cursor, aperture);

                if (reach <= aperture)
                {
                    onObject.Add(new Candidate(CadSnapKind.Center, center, arc.Handle, reach));
                }
            }

            if (Has(CadSnapModes.Quadrant))
            {
                for (int q = 0; q < 4; q++)
                {
                    double angle = q * Math.PI / 2.0;
                    arc.PointAt(angle, out double qx, out double qy);

                    if (Covers(arc, angle))
                    {
                        Near(near, CadSnapKind.Quadrant, new GeoPoint2(qx, qy), arc.Handle, cursor, aperture);
                    }
                }
            }

            if (reach > aperture)
            {
                return;
            }

            curves.Add(Curve.Arc(arc));

            if (Has(CadSnapModes.Nearest))
            {
                double angle = Math.Atan2(cursor.Y - arc.Cy, cursor.X - arc.Cx);
                GeoPoint2 on;

                if (Covers(arc, angle))
                {
                    arc.PointAt(angle, out double nx, out double ny);
                    on = new GeoPoint2(nx, ny);
                }
                else
                {
                    arc.PointAt(arc.Start, out double sx, out double sy);
                    arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
                    var s = new GeoPoint2(sx, sy);
                    var e = new GeoPoint2(ex, ey);
                    on = (s - cursor).Length <= (e - cursor).Length ? s : e;
                }

                nearest.Add(new Candidate(CadSnapKind.Nearest, on, arc.Handle, reach));
            }

            if (!basePoint.HasValue)
            {
                return;
            }

            GeoVector2 fromCenter = basePoint.Value - center;
            double d = fromCenter.Length;

            if (Has(CadSnapModes.Perpendicular) && d > Tolerance.EqualPoint)
            {
                double angle = Math.Atan2(fromCenter.Y, fromCenter.X);

                // Both points where the line through the centre meets the rim are perpendicular to it; the one nearer the cursor is taken.
                foreach (double a in new[] { angle, angle + Math.PI })
                {
                    if (Covers(arc, a))
                    {
                        arc.PointAt(a, out double px, out double py);
                        var foot = new GeoPoint2(px, py);
                        onObject.Add(new Candidate(CadSnapKind.Perpendicular, foot, arc.Handle, reach + (foot - cursor).Length * 1E-9));
                    }
                }
            }

            if (Has(CadSnapModes.Tangent) && d > arc.Radius + Tolerance.EqualPoint)
            {
                double angle = Math.Atan2(fromCenter.Y, fromCenter.X);
                double spread = Math.Acos(arc.Radius / d);

                foreach (double a in new[] { angle + spread, angle - spread })
                {
                    if (Covers(arc, a))
                    {
                        arc.PointAt(a, out double tx, out double ty);
                        var touch = new GeoPoint2(tx, ty);
                        onObject.Add(new Candidate(CadSnapKind.Tangent, touch, arc.Handle, reach + (touch - cursor).Length * 1E-9));
                    }
                }
            }
        }

        private void AddIntersections(List<Curve> curves, GeoPoint2 cursor, double aperture, List<Candidate> near)
        {
            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    foreach (GeoPoint2 point in Cross(curves[i], curves[j]))
                    {
                        string handle = string.CompareOrdinal(curves[i].Handle, curves[j].Handle) <= 0 ? curves[i].Handle : curves[j].Handle;
                        Near(near, CadSnapKind.Intersection, point, handle, cursor, aperture);
                    }
                }
            }
        }

        private IEnumerable<GeoPoint2> Cross(Curve a, Curve b)
        {
            if (a.Radius < 0.0 && b.Radius < 0.0)
            {
                GeoVector2 r = a.B - a.A;
                GeoVector2 s = b.B - b.A;
                double denominator = r.X * s.Y - r.Y * s.X;
                double lengths = r.Length * s.Length;

                if (lengths <= 0.0 || Math.Abs(denominator) <= Tolerance.EqualVector * lengths)
                {
                    yield break;
                }

                GeoVector2 q = b.A - a.A;
                double t = (q.X * s.Y - q.Y * s.X) / denominator;
                double u = (q.X * r.Y - q.Y * r.X) / denominator;
                double slackT = Tolerance.EqualPoint / r.Length;
                double slackU = Tolerance.EqualPoint / s.Length;

                if (t >= -slackT && t <= 1.0 + slackT && u >= -slackU && u <= 1.0 + slackU)
                {
                    yield return a.A + r * t;
                }

                yield break;
            }

            if (a.Radius >= 0.0 && b.Radius >= 0.0)
            {
                foreach (GeoPoint2 point in CircleCircle(a, b))
                {
                    yield return point;
                }

                yield break;
            }

            Curve line = a.Radius < 0.0 ? a : b;
            Curve circle = a.Radius < 0.0 ? b : a;

            foreach (GeoPoint2 point in LineCircle(line, circle))
            {
                yield return point;
            }
        }

        private IEnumerable<GeoPoint2> LineCircle(Curve line, Curve circle)
        {
            GeoVector2 d = line.B - line.A;
            double length = d.Length;

            if (length <= 0.0)
            {
                yield break;
            }

            GeoVector2 f = line.A - circle.Center;
            double a = d.X * d.X + d.Y * d.Y;
            double b = 2.0 * (f.X * d.X + f.Y * d.Y);
            double c = f.X * f.X + f.Y * f.Y - circle.Radius * circle.Radius;
            double discriminant = b * b - 4.0 * a * c;
            double slack = Tolerance.EqualPoint / length;

            if (discriminant < 0.0)
            {
                // A line within the tolerance of touching the circle touches it at the foot from the centre.
                GeoPoint2 foot = Foot(circle.Center, line.A, line.B, out double tf);

                if (Math.Abs((foot - circle.Center).Length - circle.Radius) <= Tolerance.EqualPoint && tf >= -slack && tf <= 1.0 + slack && circle.Covers(foot, Tolerance))
                {
                    yield return foot;
                }

                yield break;
            }

            double root = Math.Sqrt(discriminant);

            foreach (double t in new[] { (-b - root) / (2.0 * a), (-b + root) / (2.0 * a) })
            {
                GeoPoint2 point = line.A + d * t;

                if (t >= -slack && t <= 1.0 + slack && circle.Covers(point, Tolerance))
                {
                    yield return point;
                }
            }
        }

        private IEnumerable<GeoPoint2> CircleCircle(Curve a, Curve b)
        {
            GeoVector2 between = b.Center - a.Center;
            double d = between.Length;

            if (d <= Tolerance.EqualPoint || d > a.Radius + b.Radius + Tolerance.EqualPoint || d < Math.Abs(a.Radius - b.Radius) - Tolerance.EqualPoint)
            {
                yield break;
            }

            double along = (a.Radius * a.Radius - b.Radius * b.Radius + d * d) / (2.0 * d);
            double h = Math.Sqrt(Math.Max(0.0, a.Radius * a.Radius - along * along));
            GeoVector2 unit = between / d;
            GeoPoint2 middle = a.Center + unit * along;
            var normal = new GeoVector2(-unit.Y, unit.X);
            var points = h <= Tolerance.EqualPoint ? new[] { middle } : new[] { middle + normal * h, middle - normal * h };

            foreach (GeoPoint2 point in points)
            {
                if (a.Covers(point, Tolerance) && b.Covers(point, Tolerance))
                {
                    yield return point;
                }
            }
        }

        private bool Covers(DisplayArc2 arc, double angle)
        {
            if (arc.IsCircle)
            {
                return true;
            }

            arc.PointAt(angle, out double x, out double y);

            // Within the sweep, or within the point tolerance of either end of it.
            if (arc.CoversDirection(x, y))
            {
                return true;
            }

            arc.PointAt(arc.Start, out double sx, out double sy);
            arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
            double toStart = Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
            double toEnd = Math.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
            return Math.Min(toStart, toEnd) <= Tolerance.EqualPoint;
        }

        private static void Near(List<Candidate> list, CadSnapKind kind, GeoPoint2 point, string handle, GeoPoint2 cursor, double aperture)
        {
            double reach = (point - cursor).Length;

            if (reach <= aperture)
            {
                list.Add(new Candidate(kind, point, handle, reach));
            }
        }

        private static GeoPoint2 Foot(GeoPoint2 p, GeoPoint2 a, GeoPoint2 b, out double t)
        {
            GeoVector2 ab = b - a;
            double length2 = ab.X * ab.X + ab.Y * ab.Y;
            t = length2 <= 0.0 ? 0.0 : ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / length2;
            double clamped = Math.Max(0.0, Math.Min(1.0, t));
            return a + ab * clamped;
        }

        private static Candidate Best(List<Candidate> list)
        {
            if (list.Count == 0)
            {
                return null;
            }

            list.Sort((a, b) =>
            {
                int c = a.Reach.CompareTo(b.Reach);

                if (c == 0)
                {
                    c = a.Kind.CompareTo(b.Kind);
                }

                if (c == 0)
                {
                    c = string.CompareOrdinal(a.Handle, b.Handle);
                }

                if (c == 0)
                {
                    c = a.Point.X.CompareTo(b.Point.X);
                }

                return c != 0 ? c : a.Point.Y.CompareTo(b.Point.Y);
            });
            return list[0];
        }

        private sealed class Candidate
        {
            public Candidate(CadSnapKind kind, GeoPoint2 point, string handle, double reach)
            {
                Kind = kind;
                Point = point;
                Handle = handle;
                Reach = reach;
            }

            public CadSnapKind Kind { get; }

            public GeoPoint2 Point { get; }

            public string Handle { get; }

            public double Reach { get; }
        }

        /// <summary>A straight segment (a radius below nought) or an arc, crossed with others for intersections.</summary>
        private sealed class Curve
        {
            private DisplayArc2 _arc;

            public GeoPoint2 A { get; private set; }

            public GeoPoint2 B { get; private set; }

            public GeoPoint2 Center { get; private set; }

            public double Radius { get; private set; } = -1.0;

            public string Handle { get; private set; }

            public static Curve Segment(GeoPoint2 a, GeoPoint2 b, string handle) => new Curve { A = a, B = b, Handle = handle };

            public static Curve Arc(DisplayArc2 arc) => new Curve { _arc = arc, Center = new GeoPoint2(arc.Cx, arc.Cy), Radius = arc.Radius, Handle = arc.Handle };

            public bool Covers(GeoPoint2 point, Tolerance tolerance)
            {
                if (_arc == null || _arc.IsCircle || _arc.CoversDirection(point.X, point.Y))
                {
                    return true;
                }

                _arc.PointAt(_arc.Start, out double sx, out double sy);
                _arc.PointAt(_arc.Start + _arc.Sweep, out double ex, out double ey);
                return (point - new GeoPoint2(sx, sy)).Length <= tolerance.EqualPoint || (point - new GeoPoint2(ex, ey)).Length <= tolerance.EqualPoint;
            }
        }
    }
}
