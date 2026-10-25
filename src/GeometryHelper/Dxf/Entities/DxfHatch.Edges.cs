using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfHatch
    {
        // The points and weights of Gauss's rule of sixteen points on [-1, 1], worked out once.
        private static readonly double[][] Gauss = GaussRule(16);

        /// <summary>
        /// An edge of a loop of edges as the file has it, in the system of the HATCH: a line (72 of 1), an arc of a circle (2), of
        /// an ellipse (3) or a spline (4); its groups as read, from its 72.
        /// </summary>
        private sealed class Edge
        {
            private Edge(List<KeyValuePair<int, string>> groups)
            {
                Groups = groups;
                Type = Whole(groups[0].Value, 0);
            }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal int Type { get; }

            private double this[int code] => Num(Text(Groups, code), 0.0);

            internal static Edge Read(List<KeyValuePair<int, string>> groups) => new Edge(groups);

            /// <summary>Whether an arc or an ellipse runs counter-clockwise about the normal (73), as AutoCAD takes one that gives none.</summary>
            private bool Ccw => Whole(Text(Groups, 73), 1) != 0;

            /// <summary>The angles in degrees an arc or an ellipse runs between counter-clockwise, as ezdxf takes them: a clockwise one's 50 and 51 the complements of its ends, swapped.</summary>
            private void Angles(out double start, out double end)
            {
                double a = this[50];
                double b = this[51];
                start = Ccw ? a : 360.0 - b;
                end = Ccw ? b : 360.0 - a;
            }

            /// <summary>
            /// How far an arc or an ellipse runs, in degrees from above nought up to a whole turn: equal angles are a whole turn, an
            /// edge of none being nothing a loop has.
            /// </summary>
            private static double Span(double start, double end)
            {
                double span = (end - start) % 360.0;

                if (span <= 0.0)
                {
                    span += 360.0;
                }

                return span;
            }

            private static bool WholeTurn(double span) => span >= 360.0 - 1E-9;

            /// <summary>The shape of the edge in the world.</summary>
            /// <exception cref="ArgumentException">Thrown when the edge is no shape.</exception>
            internal object Shape(GeoVector3 normal, double elevation)
            {
                GeoPoint3 World(double x, double y) => OcsToWorld(normal, new GeoPoint3(x, y, elevation));

                switch (Type)
                {
                    case 1:
                        return new GeoLine3(World(this[10], this[20]), World(this[11], this[21]));

                    case 2:
                    {
                        double radius = this[40];
                        Angles(out double start, out double end);
                        double span = Span(start, end);
                        GeoPoint3 centre = World(this[10], this[20]);

                        if (!(radius > 0.0))
                        {
                            throw new ArgumentException("An arc edge has a radius above nought.");
                        }

                        if (WholeTurn(span))
                        {
                            return new GeoCircle3(centre, Ccw ? normal : normal.Multiply(-1.0), radius);
                        }

                        // The point the arc starts at, as it runs: a counter-clockwise arc at its start angle, a clockwise one at its end.
                        double first = (Ccw ? start : end) * (Math.PI / 180.0);
                        GeoPoint3 from = World(this[10] + radius * Math.Cos(first), this[20] + radius * Math.Sin(first));
                        return GeoArc3.FromCircle(centre, normal, radius, from, (Ccw ? span : -span) * (Math.PI / 180.0));
                    }

                    case 3:
                    {
                        Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio);
                        double major = Math.Sqrt(mx * mx + my * my);

                        if (!(major > 0.0) || !(ratio > 0.0))
                        {
                            throw new ArgumentException("An ellipse edge has a major axis and a ratio above nought.");
                        }

                        Angles(out double start, out double end);
                        GeoPoint3 centre = World(cx, cy);

                        if (WholeTurn(Span(start, end)))
                        {
                            GeoVector3 axis = ToWorld(normal, ratio > 1.0 ? -my : mx, ratio > 1.0 ? mx : my);
                            double a = ratio > 1.0 ? ratio * major : major;
                            double b = ratio > 1.0 ? major : ratio * major;
                            return new GeoEllipse3(centre, Ccw ? normal : normal.Multiply(-1.0), axis, a, b);
                        }

                        Parameters(start, end, ratio, out double from, out double to);
                        double sweep = to - from;
                        bool ccw = Ccw;
                        var points = new List<GeoPoint3>();
                        var weights = new List<double>();
                        var knots = new List<double>();
                        DxfCurveTools.Conic(centre, u =>
                        {
                            double t = ccw ? from + u : to - u;
                            double kx = -ratio * my;
                            double ky = ratio * mx;
                            return World(cx + Math.Cos(t) * mx + Math.Sin(t) * kx, cy + Math.Cos(t) * my + Math.Sin(t) * ky);
                        }, 0.0, sweep, false, points, weights, knots);
                        return new GeoSpline3(2, points, knots, weights);
                    }

                    case 4:
                    {
                        Spline(out int degree, out List<double> knots, out List<double[]> control, out List<double> weights);

                        if (control.Count == 0)
                        {
                            throw new ArgumentException("A spline edge has no control points, which AutoCAD needs.");
                        }

                        var points = control.ConvertAll(p => World(p[0], p[1]));
                        return new GeoSpline3(degree, points, knots, weights.Count == control.Count ? weights : null);
                    }

                    default:
                        throw new ArgumentException("An edge of a HATCH is a line, an arc, an ellipse or a spline, 1 to 4; this one is " + Type.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
                }
            }

            /// <summary>Half the integral of x dy - y dx along the edge, as it runs: what it adds to the area of its loop.</summary>
            /// <exception cref="ArgumentException">Thrown when the edge is no shape.</exception>
            internal double Green()
            {
                switch (Type)
                {
                    case 1:
                        return 0.5 * (this[10] * this[21] - this[11] * this[20]);

                    case 2:
                    {
                        double cx = this[10];
                        double cy = this[20];
                        double r = this[40];
                        Run(out double from, out double sweep);
                        double xs = cx + r * Math.Cos(from);
                        double ys = cy + r * Math.Sin(from);
                        double xe = cx + r * Math.Cos(from + sweep);
                        double ye = cy + r * Math.Sin(from + sweep);
                        return 0.5 * (cx * (ye - ys) - cy * (xe - xs) + r * r * sweep);
                    }

                    case 3:
                    {
                        Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio);
                        Angles(out double start, out double end);
                        double from;
                        double to;

                        if (WholeTurn(Span(start, end)))
                        {
                            from = 0.0;
                            to = 2.0 * Math.PI;
                        }
                        else
                        {
                            Parameters(start, end, ratio, out from, out to);
                        }

                        if (!Ccw)
                        {
                            double swap = from;
                            from = to;
                            to = swap;
                        }

                        double kx = -ratio * my;
                        double ky = ratio * mx;
                        double dx = (Math.Cos(to) - Math.Cos(from)) * mx + (Math.Sin(to) - Math.Sin(from)) * kx;
                        double dy = (Math.Cos(to) - Math.Cos(from)) * my + (Math.Sin(to) - Math.Sin(from)) * ky;
                        return 0.5 * (cx * dy - cy * dx + (mx * ky - my * kx) * (to - from));
                    }

                    case 4:
                    {
                        GeoSpline2 spline = Spline2();
                        IReadOnlyList<double> knots = spline.Knots;
                        double area = 0.0;

                        for (int i = 0; i + 1 < knots.Count; i++)
                        {
                            double a = Math.Max(knots[i], spline.DomainStart);
                            double b = Math.Min(knots[i + 1], spline.DomainEnd);

                            if (!(b > a))
                            {
                                continue;
                            }

                            for (int k = 0; k < Gauss[0].Length; k++)
                            {
                                double u = 0.5 * (a + b) + 0.5 * (b - a) * Gauss[0][k];
                                GeoPoint2 p = spline.GetPointAtKnot(u);
                                GeoVector2 d = spline.GetDerivativeAtKnot(u, 1);
                                area += 0.5 * (b - a) * Gauss[1][k] * 0.5 * (p.X * d.Y - p.Y * d.X);
                            }
                        }

                        return area;
                    }

                    default:
                        return 0.0;
                }
            }

            /// <summary>Where the edge starts and ends as it runs, in the plane of the HATCH; a whole circle or ellipse where it starts.</summary>
            /// <exception cref="ArgumentException">Thrown when a spline edge has no control points, or knots that do not agree with them.</exception>
            internal void Ends(out GeoPoint2 start, out GeoPoint2 end)
            {
                switch (Type)
                {
                    case 1:
                        start = new GeoPoint2(this[10], this[20]);
                        end = new GeoPoint2(this[11], this[21]);
                        return;

                    case 2:
                    {
                        double r = this[40];
                        Run(out double from, out double sweep);
                        start = new GeoPoint2(this[10] + r * Math.Cos(from), this[20] + r * Math.Sin(from));
                        end = new GeoPoint2(this[10] + r * Math.Cos(from + sweep), this[20] + r * Math.Sin(from + sweep));
                        return;
                    }

                    case 3:
                    {
                        Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio);
                        Angles(out double first, out double last);
                        double from = 0.0;
                        double to = 0.0;

                        if (!WholeTurn(Span(first, last)))
                        {
                            Parameters(first, last, ratio, out from, out to);
                        }

                        GeoPoint2 At(double t) => new GeoPoint2(cx + Math.Cos(t) * mx - Math.Sin(t) * ratio * my, cy + Math.Cos(t) * my + Math.Sin(t) * ratio * mx);
                        start = At(Ccw ? from : to);
                        end = At(Ccw ? to : from);
                        return;
                    }

                    case 4:
                    {
                        GeoSpline2 spline = Spline2();
                        start = spline.GetPointAtKnot(spline.DomainStart);
                        end = spline.GetPointAtKnot(spline.DomainEnd);
                        return;
                    }

                    default:
                        start = end = new GeoPoint2(0.0, 0.0);
                        return;
                }
            }

            /// <summary>Adds points the edge is drawn through, its start first and its end left to the next edge.</summary>
            internal void Sample(List<GeoPoint2> points)
            {
                switch (Type)
                {
                    case 1:
                        points.Add(new GeoPoint2(this[10], this[20]));
                        break;

                    case 2:
                    {
                        double cx = this[10];
                        double cy = this[20];
                        double r = this[40];
                        Run(out double from, out double sweep);
                        int steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 16.0)));

                        for (int i = 0; i < steps; i++)
                        {
                            double t = from + sweep * i / steps;
                            points.Add(new GeoPoint2(cx + r * Math.Cos(t), cy + r * Math.Sin(t)));
                        }

                        break;
                    }

                    case 3:
                    {
                        Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio);
                        Angles(out double start, out double end);
                        double from = 0.0;
                        double to = 2.0 * Math.PI;

                        if (!WholeTurn(Span(start, end)))
                        {
                            Parameters(start, end, ratio, out from, out to);
                        }

                        int steps = Math.Max(4, (int)Math.Ceiling((to - from) / (Math.PI / 16.0)));

                        for (int i = 0; i < steps; i++)
                        {
                            double t = Ccw ? from + (to - from) * i / steps : to - (to - from) * i / steps;
                            points.Add(new GeoPoint2(cx + Math.Cos(t) * mx - Math.Sin(t) * ratio * my, cy + Math.Cos(t) * my + Math.Sin(t) * ratio * mx));
                        }

                        break;
                    }

                    case 4:
                    {
                        GeoSpline2 spline = Spline2();
                        double a = spline.DomainStart;
                        double b = spline.DomainEnd;
                        int steps = Math.Max(8, 8 * (spline.Knots.Count - 1));

                        for (int i = 0; i < steps; i++)
                        {
                            points.Add(spline.GetPointAtKnot(a + (b - a) * i / steps));
                        }

                        break;
                    }
                }
            }

            /// <summary>
            /// The groups of the edge moved by a transformation seen from the HATCH's system, as ezdxf moves one, in their order: a
            /// point through it at the elevation, a radius scaled, an arc's angles turned, an ellipse's axis turned and its angles
            /// measured anew from it, a spline's tangents turned.
            /// </summary>
            internal List<KeyValuePair<int, string>> Moved(DxfOcsTransform ocs, double elevation, bool conformal)
            {
                var moved = new List<KeyValuePair<int, string>>(Groups);
                Angles(out double start, out double end);
                bool whole = WholeTurn(Span(start, end));
                double newStart = start;
                double newEnd = end;

                if (Type == 2 && !whole)
                {
                    newStart = ocs.TransformDegrees(start);
                    newEnd = ocs.TransformDegrees(end);
                }
                else if (Type == 2)
                {
                    newStart = ocs.TransformDegrees(start);
                    newEnd = newStart + 360.0;
                }
                double[] general = null;

                if (Type == 3 && !conformal)
                {
                    general = Affine(ocs, start, end, whole);
                    newStart = general[3];
                    newEnd = general[4];
                }
                else if (Type == 3 && !whole)
                {
                    Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio);
                    GeoPoint3 centre = ocs.TransformVertex(new GeoPoint3(cx, cy, elevation));
                    GeoVector3 axis = ocs.TransformDirection(new GeoVector3(mx, my, 0.0));
                    newStart = EllipseAngle(ocs, cx, cy, mx, my, ratio, start, elevation, centre, axis);
                    newEnd = EllipseAngle(ocs, cx, cy, mx, my, ratio, end, elevation, centre, axis);

                    if (Math.Abs(newEnd) <= 1E-9)
                    {
                        newEnd = 360.0;
                    }
                }

                for (int i = 1; i < moved.Count; i++)
                {
                    int code = moved[i].Key;
                    bool pair = i + 1 < moved.Count && moved[i + 1].Key == code + 10;

                    if (pair && (code == 10 || (code == 11 && Type != 3) || (Type == 4 && (code == 12 || code == 13))))
                    {
                        double x = Num(moved[i].Value, 0.0);
                        double y = Num(moved[i + 1].Value, 0.0);
                        double nx;
                        double ny;

                        if (code == 12 || code == 13)
                        {
                            GeoVector3 d = ocs.TransformDirection(new GeoVector3(x, y, 0.0));
                            nx = d.X;
                            ny = d.Y;
                        }
                        else
                        {
                            GeoPoint3 p = ocs.TransformVertex(new GeoPoint3(x, y, elevation));
                            nx = p.X;
                            ny = p.Y;
                        }

                        moved[i] = G(code, nx);
                        moved[i + 1] = G(code + 10, ny);
                        i++;
                    }
                    else if (pair && code == 11 && Type == 3)
                    {
                        GeoVector3 d = general != null ? new GeoVector3(general[0], general[1], 0.0) : ocs.TransformDirection(new GeoVector3(Num(moved[i].Value, 0.0), Num(moved[i + 1].Value, 0.0), 0.0));
                        moved[i] = G(11, d.X);
                        moved[i + 1] = G(21, d.Y);
                        i++;
                    }
                    else if (code == 40 && Type == 3 && general != null)
                    {
                        moved[i] = G(40, general[2]);
                    }
                    else if (code == 40 && Type == 2)
                    {
                        moved[i] = G(40, ocs.TransformLength(new GeoVector3(Num(moved[i].Value, 0.0), 0.0, 0.0)));
                    }
                    else if ((code == 50 || code == 51) && (Type == 2 || (Type == 3 && !whole)))
                    {
                        // Written back as the file keeps it: a clockwise edge's angles the complements of its ends, swapped.
                        double value = code == 50 ? (Ccw ? newStart : 360.0 - newEnd) : (Ccw ? newEnd : 360.0 - newStart);
                        moved[i] = G(code, value);
                    }
                }

                return moved;
            }

            /// <summary>
            /// An ellipse moved by a transformation that scales its plane unlike along its axes: its half diameters along the old
            /// axes carried, the axes of the new ellipse found where they are longest and shortest, as Rytz's construction finds
            /// them; its major axis (x and y), ratio, and the angles in degrees of its ends measured anew, a whole one's kept.
            /// </summary>
            private double[] Affine(DxfOcsTransform ocs, double start, double end, bool whole)
            {
                Ellipse(out _, out _, out double mx, out double my, out double ratio);
                GeoVector3 u = ocs.TransformDirection(new GeoVector3(mx, my, 0.0));
                GeoVector3 v = ocs.TransformDirection(new GeoVector3(-ratio * my, ratio * mx, 0.0));
                double t0 = 0.5 * Math.Atan2(2.0 * (u.X * v.X + u.Y * v.Y), (u.X * u.X + u.Y * u.Y) - (v.X * v.X + v.Y * v.Y));
                double ax = u.X * Math.Cos(t0) + v.X * Math.Sin(t0);
                double ay = u.Y * Math.Cos(t0) + v.Y * Math.Sin(t0);
                double bx = -u.X * Math.Sin(t0) + v.X * Math.Cos(t0);
                double by = -u.Y * Math.Sin(t0) + v.Y * Math.Cos(t0);
                double sign = 1.0;
                double shift = 0.0;

                // The minor axis a quarter on from the major counter-clockwise, the parameter running back where it was not.
                if (ax * by - ay * bx < 0.0)
                {
                    bx = -bx;
                    by = -by;
                    sign = -1.0;
                }

                double a = Math.Sqrt(ax * ax + ay * ay);
                double b = Math.Sqrt(bx * bx + by * by);

                if (b > a)
                {
                    // The longer half diameter is the major axis: the frame turned a quarter on.
                    double x = ax;
                    double y = ay;
                    ax = bx;
                    ay = by;
                    bx = -x;
                    by = -y;
                    shift = Math.PI * 0.5;
                    double swap = a;
                    a = b;
                    b = swap;
                }

                double k = b / a;
                double first = 0.0;
                double last = 360.0;

                if (!whole)
                {
                    first = Angle(start);
                    last = Angle(end);
                    last = Math.Abs(last) <= 1E-9 ? 360.0 : last;
                }

                return new[] { ax, ay, k, first, last };

                double Angle(double degrees)
                {
                    double radians = degrees * (Math.PI / 180.0);
                    double t = Math.Atan2(Math.Sin(radians) / ratio, Math.Cos(radians));
                    double s = sign * (t - t0) - shift;
                    double angle = Math.Atan2(k * Math.Sin(s), Math.Cos(s)) * (180.0 / Math.PI) % 360.0;
                    angle = angle < 0.0 ? angle + 360.0 : angle;
                    return angle >= 360.0 - 1E-9 ? 0.0 : angle;
                }
            }

            /// <summary>The angle in degrees an ellipse's point at an angle has once moved, measured from the moved major axis, as ezdxf measures it.</summary>
            private static double EllipseAngle(DxfOcsTransform ocs, double cx, double cy, double mx, double my, double ratio, double angle, double elevation, GeoPoint3 centre, GeoVector3 axis)
            {
                double radians = angle * (Math.PI / 180.0);
                double t = Math.Atan2(Math.Sin(radians) / ratio, Math.Cos(radians));
                double x = cx + Math.Cos(t) * mx - Math.Sin(t) * ratio * my;
                double y = cy + Math.Cos(t) * my + Math.Sin(t) * ratio * mx;
                GeoPoint3 p = ocs.TransformVertex(new GeoPoint3(x, y, elevation));
                double major = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y);
                double ux = axis.X / major;
                double uy = axis.Y / major;
                double along = ((p.X - centre.X) * ux + (p.Y - centre.Y) * uy) / major;
                double across = ((p.X - centre.X) * -uy + (p.Y - centre.Y) * ux) / (ratio * major);
                double moved = Math.Atan2(Math.Sin(Math.Atan2(across, along)) * ratio, Math.Cos(Math.Atan2(across, along))) * (180.0 / Math.PI);
                moved %= 360.0;
                moved = moved < 0.0 ? moved + 360.0 : moved;
                return moved >= 360.0 - 1E-9 ? 0.0 : moved;
            }

            /// <summary>Where an arc starts as it runs, in radians, and how far it turns: above nought counter-clockwise.</summary>
            private void Run(out double from, out double sweep)
            {
                Angles(out double start, out double end);
                double span = Span(start, end) * (Math.PI / 180.0);
                from = (Ccw ? start : end) * (Math.PI / 180.0);
                sweep = Ccw ? span : -span;
            }

            private void Ellipse(out double cx, out double cy, out double mx, out double my, out double ratio)
            {
                cx = this[10];
                cy = this[20];
                mx = this[11];
                my = this[21];
                ratio = Num(Text(Groups, 40), 1.0);
            }

            /// <summary>The parameters an ellipse's angles stand at, as ezdxf's <c>angle_to_param</c> gives them, the end above the start.</summary>
            private static void Parameters(double start, double end, double ratio, out double from, out double to)
            {
                double s = start * (Math.PI / 180.0);
                double e = end * (Math.PI / 180.0);
                from = Math.Atan2(Math.Sin(s) / ratio, Math.Cos(s));
                to = Math.Atan2(Math.Sin(e) / ratio, Math.Cos(e));

                while (to <= from)
                {
                    to += 2.0 * Math.PI;
                }
            }

            private void Spline(out int degree, out List<double> knots, out List<double[]> control, out List<double> weights)
            {
                degree = Whole(Text(Groups, 94), 3);
                knots = new List<double>();
                control = new List<double[]>();
                weights = new List<double>();
                double[] point = null;

                foreach (KeyValuePair<int, string> group in Groups)
                {
                    switch (group.Key)
                    {
                        case 40:
                            knots.Add(Num(group.Value, 0.0));
                            break;
                        case 10:
                            point = new[] { Num(group.Value, 0.0), 0.0 };
                            control.Add(point);
                            break;
                        case 20:
                            if (point != null)
                            {
                                point[1] = Num(group.Value, 0.0);
                            }

                            break;
                        case 42:
                            weights.Add(Num(group.Value, 1.0));
                            break;
                        case 11:
                            point = null;
                            break;
                    }
                }
            }

            /// <summary>The spline of a spline edge in the plane of the HATCH.</summary>
            /// <exception cref="ArgumentException">Thrown when it has no control points, or knots that do not agree with them.</exception>
            private GeoSpline2 Spline2()
            {
                Spline(out int degree, out List<double> knots, out List<double[]> control, out List<double> weights);

                if (control.Count == 0)
                {
                    throw new ArgumentException("A spline edge has no control points, which AutoCAD needs.");
                }

                return new GeoSpline2(degree, control.ConvertAll(p => new GeoPoint2(p[0], p[1])), knots, weights.Count == control.Count ? weights : null);
            }
        }

        /// <summary>
        /// A loop made ready to be scaled unlike along the axes of its plane, as ezdxf makes one: a polyline with bulges as edges,
        /// its straight segments lines and its arcs ellipse edges of a ratio of 1, the arc edges of a loop of edges ellipse edges
        /// too; the handles kept. Any other loop as it is.
        /// </summary>
        private static List<KeyValuePair<int, string>> ForAffine(List<KeyValuePair<int, string>> groups)
        {
            LoopData loop = LoopData.Read(groups);
            int body = LoopData.Body(groups);
            var edges = new List<KeyValuePair<int, string>>();
            int count = 0;

            if (loop.IsPolyline)
            {
                if (!loop.Vertices.Exists(vertex => vertex[2] != 0.0))
                {
                    return groups;
                }

                int n = loop.Vertices.Count;

                for (int i = 0; i < n && (loop.Closed || i < n - 1); i++)
                {
                    double[] a = loop.Vertices[i];
                    double[] b = loop.Vertices[(i + 1) % n];
                    count++;

                    if (a[2] == 0.0)
                    {
                        edges.AddRange(new[] { G(72, 1), G(10, a[0]), G(20, a[1]), G(11, b[0]), G(21, b[1]) });
                        continue;
                    }

                    // As ezdxf's bulge_to_arc: the centre, the radius and the angles counter-clockwise, flagged clockwise for a bulge below nought.
                    double chord = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
                    double radius = chord * (1.0 + a[2] * a[2]) / (4.0 * a[2]);
                    double toward = Math.Atan2(b[1] - a[1], b[0] - a[0]) + (Math.PI / 2.0 - Math.Atan(a[2]) * 2.0);
                    double cx = a[0] + radius * Math.Cos(toward);
                    double cy = a[1] + radius * Math.Sin(toward);
                    double from = Math.Atan2((a[2] < 0.0 ? b[1] : a[1]) - cy, (a[2] < 0.0 ? b[0] : a[0]) - cx) * (180.0 / Math.PI);
                    double to = Math.Atan2((a[2] < 0.0 ? a[1] : b[1]) - cy, (a[2] < 0.0 ? a[0] : b[0]) - cx) * (180.0 / Math.PI);
                    edges.AddRange(EllipseOfArc(cx, cy, Math.Abs(radius), from, to, a[2] > 0.0));
                }
            }
            else
            {
                foreach (Edge edge in loop.Edges)
                {
                    count++;

                    if (edge.Type != 2)
                    {
                        edges.AddRange(edge.Groups);
                        continue;
                    }

                    double a50 = Num(Text(edge.Groups, 50), 0.0);
                    double a51 = Num(Text(edge.Groups, 51), 0.0);
                    bool ccw = Whole(Text(edge.Groups, 73), 1) != 0;
                    edges.AddRange(EllipseOfArc(Num(Text(edge.Groups, 10), 0.0), Num(Text(edge.Groups, 20), 0.0), Num(Text(edge.Groups, 40), 0.0), ccw ? a50 : 360.0 - a51, ccw ? a51 : 360.0 - a50, ccw));
                }
            }

            var made = new List<KeyValuePair<int, string>> { G(92, Whole(groups[0].Value, 0) & ~2), G(93, count) };
            made.AddRange(edges);
            made.AddRange(groups.GetRange(body, groups.Count - body));
            return made;
        }

        /// <summary>The groups of an ellipse edge of a ratio of 1 that is an arc: its angles counter-clockwise, kept as the file keeps them for one that runs clockwise.</summary>
        private static KeyValuePair<int, string>[] EllipseOfArc(double cx, double cy, double radius, double from, double to, bool ccw)
        {
            double start = Mod360(from);
            double end = Mod360(to);

            if (Math.Abs(start - end) <= 1E-9 && Math.Abs(start) <= 1E-9)
            {
                end = 360.0;
            }

            return new[]
            {
                G(72, 3), G(10, cx), G(20, cy), G(11, radius), G(21, "0"), G(40, "1"),
                G(50, ccw ? start : 360.0 - end), G(51, ccw ? end : 360.0 - start), G(73, ccw ? 1 : 0),
            };
        }

        /// <summary>A direction of the plane of a normal in the world, as a point of it is taken there.</summary>
        private static GeoVector3 ToWorld(GeoVector3 normal, double x, double y)
        {
            GeoPoint3 p = OcsToWorld(normal, new GeoPoint3(x, y, 0.0));
            return new GeoVector3(p.X, p.Y, p.Z);
        }

        /// <summary>
        /// The plane of a HATCH, to take shapes of the world into its system: a point within <see cref="Tolerance.Default"/> of
        /// the plane at its elevation, an arc, circle or ellipse whose normal runs along the HATCH's either way.
        /// </summary>
        private sealed class LoopPlane
        {
            private readonly GeoVector3 _normal;
            private readonly double _elevation;
            private readonly Tolerance _tolerance = Tolerance.Default;

            internal LoopPlane(GeoVector3 normal, double elevation)
            {
                _normal = normal;
                _elevation = elevation;
            }

            /// <summary>A point of the world in the plane's system.</summary>
            /// <exception cref="ArgumentException">Thrown when the point is not in the plane.</exception>
            internal GeoPoint2 Point(GeoPoint3 point)
            {
                GeoPoint3 local = WorldToOcs(_normal, point);

                if (!point.IsValid || Math.Abs(local.Z - _elevation) > _tolerance.EqualPoint)
                {
                    throw new ArgumentException("The point " + point + " is not in the plane of the HATCH.", "hatchLoop");
                }

                return new GeoPoint2(local.X, local.Y);
            }

            /// <summary>The vertices of a chain in the plane's system, each with its bulge about the normal.</summary>
            internal List<double[]> Vertices(IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> bulges, IReadOnlyList<GeoVector3> normals)
            {
                var vertices = new List<double[]>(points.Count);

                for (int i = 0; i < points.Count; i++)
                {
                    GeoPoint2 p = Point(points[i]);
                    double bulge = i < bulges.Count ? bulges[i] : 0.0;

                    if (bulge != 0.0)
                    {
                        Along(normals[i]);
                        bulge = normals[i].DotProduct(_normal) < 0.0 ? -bulge : bulge;
                    }

                    vertices.Add(new[] { p.X, p.Y, bulge });
                }

                return vertices;
            }

            /// <summary>Adds the groups of the edges of a shape, as AutoCAD writes them; the count of the edges.</summary>
            /// <exception cref="ArgumentException">Thrown when the shape does not lie in the plane.</exception>
            internal int Edges(object curve, List<KeyValuePair<int, string>> groups, bool fitCount)
            {
                switch (curve)
                {
                    case GeoLine3 line:
                        GeoPoint2 a = Point(line.StartPoint);
                        GeoPoint2 b = Point(line.EndPoint);
                        groups.AddRange(new[] { G(72, 1), G(10, a.X), G(20, a.Y), G(11, b.X), G(21, b.Y) });
                        return 1;

                    case GeoEdge3 edge:
                        return edge.IsArc ? Edges(edge.ToArc(), groups, fitCount) : Edges(edge.ToLine(), groups, fitCount);

                    case GeoPolylineArc3 chain:
                        return Chain(chain.GetEdges(), groups, fitCount);

                    case GeoPolygonArc3 polygon:
                        return Chain(polygon.GetEdges(), groups, fitCount);

                    case GeoArc3 arc:
                    {
                        Along(arc.Normal);
                        GeoPoint2 centre = Point(arc.Center);
                        GeoPoint2 start = Point(arc.StartPoint);
                        bool ccw = (arc.Normal.DotProduct(_normal) > 0.0) != arc.IsClockwise;
                        double from = Math.Atan2(start.Y - centre.Y, start.X - centre.X) * (180.0 / Math.PI);
                        double sweep = Math.Abs(arc.SweptAngle) * (180.0 / Math.PI);
                        double first = Mod360(ccw ? from : -from);
                        groups.AddRange(new[] { G(72, 2), G(10, centre.X), G(20, centre.Y), G(40, arc.Radius), G(50, first), G(51, first + sweep), G(73, ccw ? 1 : 0) });
                        return 1;
                    }

                    case GeoCircle3 circle:
                    {
                        Along(circle.Normal);
                        GeoPoint2 centre = Point(circle.Center);
                        bool ccw = circle.Normal.DotProduct(_normal) > 0.0;
                        groups.AddRange(new[] { G(72, 2), G(10, centre.X), G(20, centre.Y), G(40, circle.Radius), G(50, "0"), G(51, "360"), G(73, ccw ? 1 : 0) });
                        return 1;
                    }

                    case GeoEllipse3 ellipse:
                    {
                        Along(ellipse.Normal);
                        GeoPoint2 centre = Point(ellipse.Center);
                        GeoPoint3 axis = WorldToOcs(_normal, new GeoPoint3(ellipse.MajorAxis.X, ellipse.MajorAxis.Y, ellipse.MajorAxis.Z));
                        bool ccw = ellipse.Normal.DotProduct(_normal) > 0.0;
                        groups.AddRange(new[]
                        {
                            G(72, 3), G(10, centre.X), G(20, centre.Y), G(11, axis.X * ellipse.MajorRadius), G(21, axis.Y * ellipse.MajorRadius),
                            G(40, ellipse.MinorRadius / ellipse.MajorRadius), G(50, "0"), G(51, "360"), G(73, ccw ? 1 : 0),
                        });
                        return 1;
                    }

                    case GeoSpline3 spline:
                    {
                        var head = new List<KeyValuePair<int, string>>
                        {
                            G(72, 4), G(94, spline.Degree), G(73, spline.IsRational ? 1 : 0), G(74, "0"), G(95, spline.Knots.Count), G(96, spline.ControlPoints.Count),
                        };

                        foreach (double knot in spline.Knots)
                        {
                            head.Add(G(40, knot));
                        }

                        for (int i = 0; i < spline.ControlPoints.Count; i++)
                        {
                            GeoPoint2 p = Point(spline.ControlPoints[i]);
                            head.Add(G(10, p.X));
                            head.Add(G(20, p.Y));

                            if (spline.IsRational)
                            {
                                head.Add(G(42, spline.Weights[i]));
                            }
                        }

                        if (fitCount)
                        {
                            head.Add(G(97, "0"));
                        }

                        groups.AddRange(head);
                        return 1;
                    }

                    default:
                        throw new ArgumentException("An edge of a loop of a HATCH is a line, an arc, a circle, an ellipse or a spline: a " + curve.GetType().Name + " is none of them.", "hatchLoop");
                }
            }

            private int Chain(IEnumerable<GeoEdge3> edges, List<KeyValuePair<int, string>> groups, bool fitCount)
            {
                int count = 0;

                foreach (GeoEdge3 edge in edges)
                {
                    count += Edges(edge, groups, fitCount);
                }

                return count;
            }

            /// <summary>Refuses a normal that does not run along the HATCH's, either way.</summary>
            private void Along(GeoVector3 normal)
            {
                if (!normal.IsParallelTo(_normal, _tolerance))
                {
                    throw new ArgumentException("An arc of a loop turns about " + normal + ", not about the normal of the HATCH.", "hatchLoop");
                }
            }
        }

        private static double Mod360(double degrees)
        {
            double angle = degrees % 360.0;
            return angle < 0.0 ? angle + 360.0 : angle + 0.0;
        }

        /// <summary>The points and weights of Gauss's rule of n points on [-1, 1], the roots of Legendre's polynomial found by Newton's method.</summary>
        private static double[][] GaussRule(int n)
        {
            var points = new double[n];
            var weights = new double[n];

            for (int i = 0; i < n; i++)
            {
                double x = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5));
                double derivative = 0.0;

                for (int step = 0; step < 100; step++)
                {
                    double p0 = 1.0;
                    double p1 = x;

                    for (int k = 2; k <= n; k++)
                    {
                        double p2 = ((2 * k - 1) * x * p1 - (k - 1) * p0) / k;
                        p0 = p1;
                        p1 = p2;
                    }

                    derivative = n * (x * p1 - p0) / (x * x - 1.0);
                    double next = x - p1 / derivative;

                    if (Math.Abs(next - x) <= 1E-16)
                    {
                        x = next;
                        break;
                    }

                    x = next;
                }

                points[i] = x;
                weights[i] = 2.0 / ((1.0 - x * x) * derivative * derivative);
            }

            return new[] { points, weights };
        }
    }
}
