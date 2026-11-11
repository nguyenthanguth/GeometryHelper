using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The shapes the library reads from an entity, turned into display items.</summary>
    internal sealed partial class DisplayBuilder
    {
        private const double ArcStep = Math.PI / 90.0;

        private void AddShape(
            object shape,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer,
            bool fill,
            bool mask
        )
        {
            switch (shape)
            {
                case GeoPoint3 point:
                    list.Add(
                        new DisplayPolyline2(
                            new[] { m.X(point.X, point.Y), m.Y(point.X, point.Y) },
                            false,
                            style,
                            handle,
                            layer
                        )
                    );
                    return;
                case GeoLine3 line:
                    list.Add(
                        new DisplayPolyline2(
                            new[]
                            {
                                m.X(line.StartPoint.X, line.StartPoint.Y),
                                m.Y(line.StartPoint.X, line.StartPoint.Y),
                                m.X(line.EndPoint.X, line.EndPoint.Y),
                                m.Y(line.EndPoint.X, line.EndPoint.Y),
                            },
                            false,
                            style,
                            handle,
                            layer
                        )
                    );
                    return;
                case GeoRay3 ray:
                    AddEndless(ray.Origin, ray.Direction, false, m, list, style, handle, layer);
                    return;
                case GeoXLine3 xline:
                    AddEndless(xline.Origin, xline.Direction, true, m, list, style, handle, layer);
                    return;
                case GeoArc3 arc:
                    AddArc(
                        arc.Center,
                        arc.Normal,
                        arc.StartPoint,
                        arc.SweptAngle,
                        arc.Radius,
                        arc,
                        m,
                        list,
                        style,
                        handle,
                        layer
                    );
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
                        AddArcChain(
                            polygon.Vertices,
                            polygon.Bulges,
                            polygon.Normals,
                            true,
                            m,
                            list,
                            style,
                            handle,
                            layer
                        );
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

            list.Add(
                new DisplayPolyline2(xy.ToArray(), closed, style, handle, layer)
                {
                    IsSampled = shape is GeoEllipse3 || shape is GeoSpline3,
                }
            );
        }

        /// <summary>
        /// An XLINE or a RAY as a line without end from its base point the way it runs, both placed by the frame. One that
        /// runs straight out of the drawing's plane is seen end on, as the point it is drawn as.
        /// </summary>
        private static void AddEndless(
            GeoPoint3 origin,
            GeoVector3 direction,
            bool bothWays,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
        {
            double x = m.X(origin.X, origin.Y);
            double y = m.Y(origin.X, origin.Y);
            double dx = m.A * direction.X + m.C * direction.Y;
            double dy = m.B * direction.X + m.D * direction.Y;

            if (Math.Sqrt(dx * dx + dy * dy) <= 1E-12 * Math.Max(1.0, direction.Length) * Math.Max(1.0, m.Scale))
            {
                list.Add(new DisplayPolyline2(new[] { x, y }, false, style, handle, layer));
                return;
            }

            list.Add(new DisplayRay2(x, y, dx, dy, bothWays, style, handle, layer));
        }

        private void AddCircle(
            GeoCircle3 circle,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
        {
            GeoPoint3 c = circle.Center;

            if (IsFlat(circle.Normal) && m.IsSimilarity)
            {
                double radius = circle.Radius * m.Scale;
                list.Add(
                    new DisplayArc2(m.X(c.X, c.Y), m.Y(c.X, c.Y), radius, 0.0, 2.0 * Math.PI, style, handle, layer)
                );
                return;
            }

            var xy = new List<double>();
            FlattenInto(circle, m, xy);
            list.Add(new DisplayPolyline2(xy.ToArray(), true, style, handle, layer) { IsSampled = true });
        }

        private void AddArc(
            GeoPoint3 center,
            GeoVector3 normal,
            GeoPoint3 startPoint,
            double swept,
            double radius,
            GeoArc3 arc,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
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

        private static bool IsFlat(GeoVector3 normal) =>
            Math.Abs(Math.Abs(normal.Z) - normal.Length) <= 1E-9 * Math.Max(1.0, normal.Length);

        private void AddArcChain(
            IReadOnlyList<GeoPoint3> vertices,
            IReadOnlyList<double> bulges,
            IReadOnlyList<GeoVector3> normals,
            bool closed,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
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

                Bulge(
                    p0.X,
                    p0.Y,
                    p1.X,
                    p1.Y,
                    bulge,
                    out double cx,
                    out double cy,
                    out double radius,
                    out double start,
                    out double sweep
                );

                if (m.IsSimilarity)
                {
                    Flush();
                    double mcx = m.X(cx, cy);
                    double mcy = m.Y(cx, cy);
                    double sx = m.X(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
                    double sy = m.Y(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
                    list.Add(
                        new DisplayArc2(
                            mcx,
                            mcy,
                            radius * m.Scale,
                            Math.Atan2(sy - mcy, sx - mcx),
                            m.Determinant < 0.0 ? -sweep : sweep,
                            style,
                            handle,
                            layer
                        )
                    );
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

        /// <summary>
        /// A LWPOLYLINE with a width, as AutoCAD fills it: each segment of a width a band about its centre line, straight or along
        /// its arc, from its start width to its end width, and a segment of none a line. A band wider than twice its arc's
        /// radius crosses the centre, as the dot of AutoCAD's arrowhead _Dot fills a disc. False, nothing added, for a polyline
        /// whose normal is not the world's Z, which is drawn by its centre line.
        /// </summary>
        private bool AddWide(
            DxfPolyline polyline,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
        {
            GeoVector3 normal = polyline.Normal;
            int n = polyline.NumberOfVertices;

            if (n < 2 || !(normal.Z > 0.0) || !IsFlat(normal))
            {
                return false;
            }

            int edges = polyline.Closed ? n : n - 1;

            for (int i = 0; i < edges; i++)
            {
                GeoPoint2 p0 = polyline.GetPoint2dAt(i);
                GeoPoint2 p1 = polyline.GetPoint2dAt((i + 1) % n);
                double w0 = polyline.GetStartWidthAt(i) / 2.0;
                double w1 = polyline.GetEndWidthAt(i) / 2.0;
                double bulge = polyline.GetBulgeAt(i);
                double dx = p1.X - p0.X;
                double dy = p1.Y - p0.Y;
                double chord = Math.Sqrt(dx * dx + dy * dy);

                if (!(chord > 0.0))
                {
                    continue;
                }

                var ring = new List<double>();

                if (Math.Abs(bulge) < 1E-12)
                {
                    double nx = -dy / chord;
                    double ny = dx / chord;

                    if (w0 <= 0.0 && w1 <= 0.0)
                    {
                        list.Add(
                            new DisplayPolyline2(
                                new[] { m.X(p0.X, p0.Y), m.Y(p0.X, p0.Y), m.X(p1.X, p1.Y), m.Y(p1.X, p1.Y) },
                                false,
                                style,
                                handle,
                                layer
                            )
                        );
                        continue;
                    }

                    Ring(ring, m, p0.X + nx * w0, p0.Y + ny * w0);
                    Ring(ring, m, p1.X + nx * w1, p1.Y + ny * w1);
                    Ring(ring, m, p1.X - nx * w1, p1.Y - ny * w1);
                    Ring(ring, m, p0.X - nx * w0, p0.Y - ny * w0);
                }
                else
                {
                    Bulge(
                        p0.X,
                        p0.Y,
                        p1.X,
                        p1.Y,
                        bulge,
                        out double cx,
                        out double cy,
                        out double radius,
                        out double start,
                        out double sweep
                    );
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / ArcStep));

                    if (w0 <= 0.0 && w1 <= 0.0)
                    {
                        for (int k = 0; k <= steps; k++)
                        {
                            double angle = start + sweep * k / steps;
                            Ring(ring, m, cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                        }

                        list.Add(
                            new DisplayPolyline2(ring.ToArray(), false, style, handle, layer) { IsSampled = true }
                        );
                        continue;
                    }

                    // The outer edge forward, then the inner back; an inner radius below nought lies across the centre.
                    for (int k = 0; k <= steps; k++)
                    {
                        double angle = start + sweep * k / steps;
                        double r = radius + w0 + (w1 - w0) * k / steps;
                        Ring(ring, m, cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
                    }

                    for (int k = steps; k >= 0; k--)
                    {
                        double angle = start + sweep * k / steps;
                        double r = radius - w0 - (w1 - w0) * k / steps;
                        Ring(ring, m, cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
                    }
                }

                list.Add(new DisplayFill2(new[] { ring.ToArray() }, false, style, handle, layer));
            }

            return true;
        }

        private static void Ring(List<double> ring, Affine2 m, double x, double y)
        {
            ring.Add(m.X(x, y));
            ring.Add(m.Y(x, y));
        }

        /// <summary>
        /// A POINT as PDMODE draws it, other than the dot of 0: nothing for 1, a plus for 2, a cross for 3, a tick up from the
        /// point for 4, with a circle (32), a square (64) or both (96) about it, each the size PDSIZE gives (see
        /// <see cref="PointSize"/>) and turned back by the point's <see cref="DxfDBPoint.EcsRotation"/>, which AutoCAD measures
        /// clockwise. A mode of 5 to 31 is drawn as its dot.
        /// </summary>
        private void AddPoint(
            DxfDBPoint point,
            Affine2 m,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layer
        )
        {
            GeoPoint3 at = point.Position;
            int shape = _pdmode & 31;
            double half = PointSize(m) / 2.0;
            double turn = -point.EcsRotation;
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);

            void Line(double u0, double v0, double u1, double v1)
            {
                double x0 = at.X + u0 * cos - v0 * sin;
                double y0 = at.Y + u0 * sin + v0 * cos;
                double x1 = at.X + u1 * cos - v1 * sin;
                double y1 = at.Y + u1 * sin + v1 * cos;
                list.Add(
                    new DisplayPolyline2(
                        new[] { m.X(x0, y0), m.Y(x0, y0), m.X(x1, y1), m.Y(x1, y1) },
                        false,
                        style,
                        handle,
                        layer
                    )
                );
            }

            switch (shape)
            {
                case 1:
                    break;
                case 2:
                    Line(-half, 0.0, half, 0.0);
                    Line(0.0, -half, 0.0, half);
                    break;
                case 3:
                    Line(-half, -half, half, half);
                    Line(-half, half, half, -half);
                    break;
                case 4:
                    Line(0.0, 0.0, 0.0, half);
                    break;
                default:
                    list.Add(
                        new DisplayPolyline2(new[] { m.X(at.X, at.Y), m.Y(at.X, at.Y) }, false, style, handle, layer)
                    );
                    break;
            }

            if ((_pdmode & 32) != 0)
            {
                AddCircle(new GeoCircle3(at, new GeoVector3(0.0, 0.0, 1.0), half), m, list, style, handle, layer);
            }

            if ((_pdmode & 64) != 0)
            {
                Line(-half, -half, half, -half);
                Line(half, -half, half, half);
                Line(half, half, -half, half);
                Line(-half, half, -half, -half);
            }
        }

        /// <summary>
        /// The size of a point's symbol in the units of the frame it is drawn in, as PDSIZE gives it: above nought in drawing
        /// units; below nought that percentage of the height of the view; nought five per cent of it, as AutoCAD sizes them at a
        /// regeneration. The view is <see cref="CadDocument.PointViewHeight"/> where it is set, else the height of the drawing's
        /// extents, else of its limits: a symbol of a relative size is of one size on the sheet whatever block it is in.
        /// </summary>
        private double PointSize(Affine2 m)
        {
            if (_pdsize > 0.0)
            {
                return _pdsize;
            }

            double percent = _pdsize < 0.0 ? -_pdsize : 5.0;
            double scale = m.Scale > 0.0 ? m.Scale : 1.0;
            return _pointViewHeight * percent / 100.0 / scale;
        }

        /// <summary>The height of the view relative point sizes are taken from: the document's, else the drawing's extents, else its limits, else 1.</summary>
        private static double ViewHeightOf(DxfDatabase database, CadDocument document)
        {
            if (document != null && document.PointViewHeight > 0.0 && !double.IsInfinity(document.PointViewHeight))
            {
                return document.PointViewHeight;
            }

            try
            {
                double extents = database.Extmax.Y - database.Extmin.Y;

                if (extents > 0.0 && extents < 1E19)
                {
                    return extents;
                }

                double limits = database.Limmax.Y - database.Limmin.Y;
                return limits > 0.0 && !double.IsInfinity(limits) ? limits : 1.0;
            }
            catch (Exception)
            {
                return 1.0;
            }
        }

        /// <summary>The arc a bulge draws from one vertex to the next: its centre, radius, start angle and signed sweep.</summary>
        private static void Bulge(
            double x0,
            double y0,
            double x1,
            double y1,
            double bulge,
            out double cx,
            out double cy,
            out double radius,
            out double start,
            out double sweep
        )
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

        private static void FlattenChain(
            IReadOnlyList<GeoPoint3> vertices,
            IReadOnlyList<double> bulges,
            IReadOnlyList<GeoVector3> normals,
            bool closed,
            Affine2 m,
            List<double> xy
        )
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
                    Bulge(
                        p0.X,
                        p0.Y,
                        p1.X,
                        p1.Y,
                        bulge,
                        out double cx,
                        out double cy,
                        out double radius,
                        out double start,
                        out double sweep
                    );
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
