using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The objects constraints can be tied to, as numbers the solver moves and back: a LINE, a CIRCLE, an ARC and a light
    /// polyline of straight segments, each drawn in the plane of the world's XY (its normal on the Z axis); any other object,
    /// an arc segment of a polyline, a line out of the plane, is not one.
    /// </summary>
    internal static class CadConstraintShapes
    {
        private const double Flat = 1E-9;

        /// <summary>The shape of an object; null for an object constraints cannot be tied to.</summary>
        internal static CadShape Of(DxfEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            string handle = entity.Handle;

            switch (entity)
            {
                case DxfLine line:
                    return Flat3(line.Normal) && Level(line.StartPoint.Z) && Level(line.EndPoint.Z)
                        ? new CadShape(
                            handle,
                            CadShapeKind.Line,
                            new[] { line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y }
                        )
                        : null;
                case DxfCircle circle:
                    return Flat3(circle.Normal) && Level(circle.Center.Z) && circle.Radius > 0.0
                        ? new CadShape(
                            handle,
                            CadShapeKind.Circle,
                            new[] { circle.Center.X, circle.Center.Y, circle.Radius }
                        )
                        : null;
                case DxfArc arc:
                    if (!Flat3(arc.Normal) || !Level(arc.Center.Z) || arc.Radius <= 0.0)
                    {
                        return null;
                    }

                    return new CadShape(
                        handle,
                        CadShapeKind.Arc,
                        new[]
                        {
                            arc.Center.X,
                            arc.Center.Y,
                            arc.Radius,
                            arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle),
                            arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle),
                            arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle),
                            arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle),
                        }
                    );
                case DxfPolyline polyline:
                    if (
                        !Flat3(polyline.Normal)
                        || polyline.NumberOfVertices < 2
                        || !polyline.IsOnlyLines
                        || !Level(polyline.Elevation)
                    )
                    {
                        return null;
                    }

                    var values = new double[polyline.NumberOfVertices * 2];

                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        GeoPoint2 p = polyline.GetPoint2dAt(i);
                        values[2 * i] = p.X;
                        values[2 * i + 1] = p.Y;
                    }

                    return new CadShape(handle, CadShapeKind.Polyline, values, polyline.Closed);
            }

            return null;
        }

        private static bool Flat3(GeoVector3 normal) =>
            Math.Abs(normal.X) < Flat && Math.Abs(normal.Y) < Flat && normal.Z > 0.0;

        private static bool Level(double z) => Math.Abs(z) < 1E-9;

        /// <summary>Whether two sets of numbers differ by more than a hair, which the objects are then written back for.</summary>
        internal static bool Differ(double[] a, double[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (Math.Abs(a[i] - b[i]) > 1E-10 * Math.Max(1.0, Math.Abs(a[i])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Writes the numbers of a shape solved back into its object.</summary>
        internal static void Apply(DxfEntity entity, CadShape shape, double[] values)
        {
            switch (entity)
            {
                case DxfLine line:
                    line.StartPoint = new GeoPoint3(values[0], values[1], line.StartPoint.Z);
                    line.EndPoint = new GeoPoint3(values[2], values[3], line.EndPoint.Z);
                    break;
                case DxfCircle circle:
                    circle.Center = new GeoPoint3(values[0], values[1], circle.Center.Z);
                    circle.Radius = values[2];
                    break;
                case DxfArc arc:
                    arc.Center = new GeoPoint3(values[0], values[1], arc.Center.Z);
                    arc.Radius = values[2];
                    arc.StartAngle = Normalised(Math.Atan2(values[4] - values[1], values[3] - values[0]));
                    arc.EndAngle = Normalised(Math.Atan2(values[6] - values[1], values[5] - values[0]));
                    break;
                case DxfPolyline polyline:
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        polyline.SetPointAt(i, new GeoPoint2(values[2 * i], values[2 * i + 1]));
                    }

                    break;
            }
        }

        private static double Normalised(double angle)
        {
            double turn = 2.0 * Math.PI;
            angle %= turn;
            return angle < 0.0 ? angle + turn : angle;
        }

        /// <summary>
        /// The place of a part of a shape: the start, the end or the middle of a line or of a segment, the start, the end or
        /// the centre of an arc, the centre of a circle; null for a part the shape has none of.
        /// </summary>
        internal static GeoPoint2? PointOf(CadShape shape, CadRef r)
        {
            double[] v = shape.Values;

            switch (shape.Kind)
            {
                case CadShapeKind.Line:
                    switch (r.Part)
                    {
                        case CadPart.Start:
                            return new GeoPoint2(v[0], v[1]);
                        case CadPart.End:
                            return new GeoPoint2(v[2], v[3]);
                        case CadPart.Mid:
                            return new GeoPoint2((v[0] + v[2]) / 2.0, (v[1] + v[3]) / 2.0);
                    }

                    return null;
                case CadShapeKind.Circle:
                    return r.Part == CadPart.Center ? new GeoPoint2(v[0], v[1]) : (GeoPoint2?)null;
                case CadShapeKind.Arc:
                    switch (r.Part)
                    {
                        case CadPart.Center:
                            return new GeoPoint2(v[0], v[1]);
                        case CadPart.Start:
                            return new GeoPoint2(v[3], v[4]);
                        case CadPart.End:
                            return new GeoPoint2(v[5], v[6]);
                    }

                    return null;
                case CadShapeKind.Polyline:
                    if (r.Segment < 0 || r.Segment >= shape.Segments)
                    {
                        return null;
                    }

                    int a = 2 * r.Segment;
                    int b = 2 * ((r.Segment + 1) % shape.Vertices);

                    switch (r.Part)
                    {
                        case CadPart.Start:
                            return new GeoPoint2(v[a], v[a + 1]);
                        case CadPart.End:
                            return new GeoPoint2(v[b], v[b + 1]);
                        case CadPart.Mid:
                            return new GeoPoint2((v[a] + v[b]) / 2.0, (v[a + 1] + v[b + 1]) / 2.0);
                    }

                    return null;
            }

            return null;
        }

        /// <summary>
        /// The parts of a shape (or of one segment of it) that a point can be picked at, with where each lies: the ends, the
        /// middle of a line or a segment, the centre of a circle or an arc, in that order.
        /// </summary>
        internal static List<KeyValuePair<CadPart, GeoPoint2>> PartsOf(CadShape shape, int segment)
        {
            var parts = new List<KeyValuePair<CadPart, GeoPoint2>>();
            var probe = new CadRef(shape.Handle, segment, CadPart.Start);

            foreach (CadPart part in new[] { CadPart.Start, CadPart.End, CadPart.Mid, CadPart.Center })
            {
                GeoPoint2? p = PointOf(shape, probe.Of(part));

                if (p.HasValue)
                {
                    parts.Add(new KeyValuePair<CadPart, GeoPoint2>(part, p.Value));
                }
            }

            return parts;
        }
    }
}
