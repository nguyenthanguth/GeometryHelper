using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The shapes the library reads from an entity, turned into display items.</summary>
    internal sealed partial class DisplayBuilder
    {
        private const double ArcStep = Math.PI / 90.0;

        private void AddShape(object shape, Affine2 m, DisplayList2 list, DisplayStyle style, string handle, string layer, bool fill, bool mask)
        {
            switch (shape)
            {
                case GeoPoint3 point:
                    list.Add(new DisplayPolyline2(new[] { m.X(point.X, point.Y), m.Y(point.X, point.Y) }, false, style, handle, layer));
                    return;
                case GeoLine3 line:
                    list.Add(new DisplayPolyline2(new[] { m.X(line.StartPoint.X, line.StartPoint.Y), m.Y(line.StartPoint.X, line.StartPoint.Y), m.X(line.EndPoint.X, line.EndPoint.Y), m.Y(line.EndPoint.X, line.EndPoint.Y) }, false, style, handle, layer));
                    return;
                case GeoArc3 arc:
                    AddArc(arc.Center, arc.Normal, arc.StartPoint, arc.SweptAngle, arc.Radius, arc, m, list, style, handle, layer);
                    return;
                case GeoCircle3 circle:
                    AddCircle(circle, m, list, style, handle, layer);
                    return;
                case GeoPolylineArc3 chain:
                    AddArcChain(chain.Vertices, chain.Bulges, chain.Normals, false, m, list, style, handle, layer);
                    return;
                case GeoPolygonArc3 polygon:
                    if (fill || mask)
                    {
                        var ring = new List<double>();
                        FlattenInto(polygon, m, ring);
                        list.Add(new DisplayFill2(new[] { ring.ToArray() }, mask, style, handle, layer));
                    }
                    else
                    {
                        AddArcChain(polygon.Vertices, polygon.Bulges, polygon.Normals, true, m, list, style, handle, layer);
                    }

                    return;
            }

            if (shape is GeoFace3 face)
            {
                // A face, as a 3DFACE of four corners is read, is drawn by its edges: its boundary and those of its holes.
                AddShape(face.Boundary, m, list, style, handle, layer, fill, mask);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    AddShape(hole, m, list, style, handle, layer, fill, mask);
                }

                return;
            }

            var xy = new List<double>();
            bool closed = false;

            switch (shape)
            {
                case GeoPolyline3 polyline:
                    FlattenInto(polyline, m, xy);

                    // The boundary of a WIPEOUT is read as a polyline that ends where it starts: a ring to fill.
                    closed = fill || mask;
                    break;
                case GeoPolygon3 polygon3:
                    FlattenInto(polygon3, m, xy);
                    closed = true;
                    break;
                case GeoTriangle3 triangle:
                    FlattenInto(triangle, m, xy);
                    closed = true;
                    break;
                case GeoEllipse3 _:
                case GeoSpline3 _:
                    FlattenInto(shape, m, xy);
                    closed = shape is GeoEllipse3;
                    break;
                default:
                    return;
            }

            if (xy.Count < 4)
            {
                return;
            }

            if (closed && (fill || mask))
            {
                list.Add(new DisplayFill2(new[] { xy.ToArray() }, mask, style, handle, layer));
                return;
            }

            list.Add(new DisplayPolyline2(xy.ToArray(), closed, style, handle, layer) { IsSampled = shape is GeoEllipse3 || shape is GeoSpline3 });
        }

        private void AddCircle(GeoCircle3 circle, Affine2 m, DisplayList2 list, DisplayStyle style, string handle, string layer)
        {
            GeoPoint3 c = circle.Center;

            if (IsFlat(circle.Normal) && m.IsSimilarity)
            {
                double radius = circle.Radius * m.Scale;
                list.Add(new DisplayArc2(m.X(c.X, c.Y), m.Y(c.X, c.Y), radius, 0.0, 2.0 * Math.PI, style, handle, layer));
                return;
            }

            var xy = new List<double>();
            FlattenInto(circle, m, xy);
            list.Add(new DisplayPolyline2(xy.ToArray(), true, style, handle, layer) { IsSampled = true });
        }

        private void AddArc(GeoPoint3 center, GeoVector3 normal, GeoPoint3 startPoint, double swept, double radius, GeoArc3 arc, Affine2 m, DisplayList2 list, DisplayStyle style, string handle, string layer)
        {
            if (IsFlat(normal) && m.IsSimilarity)
            {
                double cx = m.X(center.X, center.Y);
                double cy = m.Y(center.X, center.Y);
                double sx = m.X(startPoint.X, startPoint.Y);
                double sy = m.Y(startPoint.X, startPoint.Y);
                double start = Math.Atan2(sy - cy, sx - cx);
                double sweep = normal.Z >= 0.0 ? swept : -swept;

                if (m.Determinant < 0.0)
                {
                    sweep = -sweep;
                }

                list.Add(new DisplayArc2(cx, cy, radius * m.Scale, start, sweep, style, handle, layer));
                return;
            }

            var xy = new List<double>();
            FlattenInto(arc, m, xy);
            list.Add(new DisplayPolyline2(xy.ToArray(), false, style, handle, layer) { IsSampled = true });
        }

        private static bool IsFlat(GeoVector3 normal) => Math.Abs(Math.Abs(normal.Z) - normal.Length) <= 1E-9 * Math.Max(1.0, normal.Length);

        private void AddArcChain(IReadOnlyList<GeoPoint3> vertices, IReadOnlyList<double> bulges, IReadOnlyList<GeoVector3> normals, bool closed, Affine2 m, DisplayList2 list, DisplayStyle style, string handle, string layer)
        {
            int n = vertices.Count;
            int edges = closed ? n : n - 1;
            var run = new List<double>();
            bool sampled = false;

            void Flush()
            {
                if (run.Count >= 4)
                {
                    list.Add(new DisplayPolyline2(run.ToArray(), false, style, handle, layer) { IsSampled = sampled });
                }

                run.Clear();
                sampled = false;
            }

            for (int i = 0; i < edges; i++)
            {
                GeoPoint3 p0 = vertices[i];
                GeoPoint3 p1 = vertices[(i + 1) % n];
                double bulge = bulges[i];

                if (normals[i].Z < 0.0)
                {
                    bulge = -bulge;
                }

                if (run.Count == 0)
                {
                    run.Add(m.X(p0.X, p0.Y));
                    run.Add(m.Y(p0.X, p0.Y));
                }

                if (Math.Abs(bulge) < 1E-12)
                {
                    run.Add(m.X(p1.X, p1.Y));
                    run.Add(m.Y(p1.X, p1.Y));
                    continue;
                }

                Bulge(p0.X, p0.Y, p1.X, p1.Y, bulge, out double cx, out double cy, out double radius, out double start, out double sweep);

                if (m.IsSimilarity)
                {
                    Flush();
                    double mcx = m.X(cx, cy);
                    double mcy = m.Y(cx, cy);
                    double sx = m.X(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
                    double sy = m.Y(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
                    list.Add(new DisplayArc2(mcx, mcy, radius * m.Scale, Math.Atan2(sy - mcy, sx - mcx), m.Determinant < 0.0 ? -sweep : sweep, style, handle, layer));
                    run.Add(m.X(p1.X, p1.Y));
                    run.Add(m.Y(p1.X, p1.Y));
                }
                else
                {
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / ArcStep));
                    sampled = true;

                    for (int k = 1; k <= steps; k++)
                    {
                        double angle = start + sweep * k / steps;
                        double x = cx + radius * Math.Cos(angle);
                        double y = cy + radius * Math.Sin(angle);
                        run.Add(m.X(x, y));
                        run.Add(m.Y(x, y));
                    }
                }
            }

            if (closed && run.Count >= 2 && edges > 0)
            {
                bool anyArc = false;

                for (int i = 0; i < edges; i++)
                {
                    if (Math.Abs(bulges[i]) >= 1E-12)
                    {
                        anyArc = true;
                        break;
                    }
                }

                // A closed chain that is only straight edges is one closed polyline; one with arcs keeps its last straight
                // run, which ends back at the first vertex, as it is.
                if (!anyArc)
                {
                    GeoPoint3 first = vertices[0];
                    double fx = m.X(first.X, first.Y);
                    double fy = m.Y(first.X, first.Y);
                    int last = run.Count - 2;

                    if (Math.Abs(run[last] - fx) < 1E-12 && Math.Abs(run[last + 1] - fy) < 1E-12)
                    {
                        run.RemoveRange(last, 2);
                    }

                    if (run.Count >= 6)
                    {
                        list.Add(new DisplayPolyline2(run.ToArray(), true, style, handle, layer));
                        run.Clear();
                    }
                }
            }

            Flush();
        }

        /// <summary>The arc a bulge draws from one vertex to the next: its centre, radius, start angle and signed sweep.</summary>
        private static void Bulge(double x0, double y0, double x1, double y1, double bulge, out double cx, out double cy, out double radius, out double start, out double sweep)
        {
            sweep = 4.0 * Math.Atan(bulge);
            double dx = x1 - x0;
            double dy = y1 - y0;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            double h = chord / 2.0 / Math.Tan(sweep / 2.0);
            double ux = chord > 0.0 ? dx / chord : 1.0;
            double uy = chord > 0.0 ? dy / chord : 0.0;
            cx = (x0 + x1) / 2.0 - uy * h;
            cy = (y0 + y1) / 2.0 + ux * h;
            radius = Math.Sqrt((x0 - cx) * (x0 - cx) + (y0 - cy) * (y0 - cy));
            start = Math.Atan2(y0 - cy, x0 - cx);
        }

        /// <summary>Appends a shape's points, in the drawing's plane under a transformation, to a chain; a curve is sampled finely.</summary>
        private void FlattenInto(object shape, Affine2 m, List<double> xy)
        {
            void Add(GeoPoint3 p)
            {
                xy.Add(m.X(p.X, p.Y));
                xy.Add(m.Y(p.X, p.Y));
            }

            switch (shape)
            {
                case GeoLine3 line:
                    Add(line.StartPoint);
                    Add(line.EndPoint);
                    break;
                case GeoPolyline3 polyline:
                    foreach (GeoPoint3 p in polyline.Vertices)
                    {
                        Add(p);
                    }

                    break;
                case GeoPolygon3 polygon:
                    foreach (GeoPoint3 p in polygon.Vertices)
                    {
                        Add(p);
                    }

                    break;
                case GeoTriangle3 triangle:
                    Add(triangle.A);
                    Add(triangle.B);
                    Add(triangle.C);
                    break;
                case GeoArc3 arc:
                    {
                        int steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.SweptAngle) / ArcStep));

                        for (int i = 0; i <= steps; i++)
                        {
                            Add(arc.GetPointAtParameter((double)i / steps));
                        }

                        break;
                    }

                case GeoCircle3 circle:
                    for (int i = 0; i < 180; i++)
                    {
                        Add(circle.GetPointAtParameter(i / 180.0));
                    }

                    break;
                case GeoEllipse3 ellipse:
                    for (int i = 0; i < 180; i++)
                    {
                        Add(ellipse.GetPointAtAngle(2.0 * Math.PI * i / 180.0));
                    }

                    break;
                case GeoSpline3 spline:
                    for (int i = 0; i <= (int)SplineSegments; i++)
                    {
                        Add(spline.GetPointAtParameter(i / SplineSegments));
                    }

                    break;
                case GeoPolylineArc3 chain:
                    FlattenChain(chain.Vertices, chain.Bulges, chain.Normals, false, m, xy);
                    break;
                case GeoPolygonArc3 ring:
                    FlattenChain(ring.Vertices, ring.Bulges, ring.Normals, true, m, xy);
                    break;
            }
        }

        private static void FlattenChain(IReadOnlyList<GeoPoint3> vertices, IReadOnlyList<double> bulges, IReadOnlyList<GeoVector3> normals, bool closed, Affine2 m, List<double> xy)
        {
            int n = vertices.Count;
            int edges = closed ? n : n - 1;

            for (int i = 0; i < edges; i++)
            {
                GeoPoint3 p0 = vertices[i];
                GeoPoint3 p1 = vertices[(i + 1) % n];
                double bulge = normals[i].Z < 0.0 ? -bulges[i] : bulges[i];
                xy.Add(m.X(p0.X, p0.Y));
                xy.Add(m.Y(p0.X, p0.Y));

                if (Math.Abs(bulge) >= 1E-12)
                {
                    Bulge(p0.X, p0.Y, p1.X, p1.Y, bulge, out double cx, out double cy, out double radius, out double start, out double sweep);
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / ArcStep));

                    for (int k = 1; k < steps; k++)
                    {
                        double angle = start + sweep * k / steps;
                        double x = cx + radius * Math.Cos(angle);
                        double y = cy + radius * Math.Sin(angle);
                        xy.Add(m.X(x, y));
                        xy.Add(m.Y(x, y));
                    }
                }
            }

            if (!closed)
            {
                GeoPoint3 last = vertices[n - 1];
                xy.Add(m.X(last.X, last.Y));
                xy.Add(m.Y(last.X, last.Y));
            }
        }
    }
}
