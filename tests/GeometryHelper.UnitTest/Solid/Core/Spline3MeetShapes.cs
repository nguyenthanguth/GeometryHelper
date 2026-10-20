using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Random shapes of each of the eighteen kinds a spline in space is asked about, drawn about a box on a seeded generator, with the
    /// library's object and the oracle's reading of the same numbers side by side.
    /// </summary>
    internal static class Spline3MeetShapes
    {
        internal static readonly string[] Kinds =
        {
            "segment", "ray", "xline", "plane", "polyline", "polygon", "triangle", "face", "aabb", "obb", "solid", "sphere", "arc", "circle", "ellipse", "edge", "chain", "loop"
        };

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        internal static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        internal static double[] Unit(double[] a)
        {
            double length = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);

            return new[] { a[0] / length, a[1] / length, a[2] / length };
        }

        internal static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        /// <summary>Draws a unit direction, evenly over the sphere.</summary>
        internal static double[] Direction(Random r)
        {
            double z = Between(r, -1.0, 1.0);
            double phi = Between(r, 0.0, 2.0 * Math.PI);
            double rho = Math.Sqrt(1.0 - z * z);

            return new[] { rho * Math.Cos(phi), rho * Math.Sin(phi), z };
        }

        /// <summary>Gets two unit directions square to a unit normal and to each other.</summary>
        internal static void Frame(double[] n, out double[] u, out double[] v)
        {
            double[] helper = Math.Abs(n[0]) <= Math.Abs(n[1]) && Math.Abs(n[0]) <= Math.Abs(n[2]) ? new[] { 1.0, 0.0, 0.0 } : Math.Abs(n[1]) <= Math.Abs(n[2]) ? new[] { 0.0, 1.0, 0.0 } : new[] { 0.0, 0.0, 1.0 };
            u = Unit(Cross(n, helper));
            v = Cross(n, u);
        }

        private static GeoVector3 V(double[] a) => new GeoVector3(a[0], a[1], a[2]);

        /// <summary>
        /// Draws a star-shaped ring of corners about a centre, in the coordinates of a plane: the corners at rising angles, each between a
        /// share of the radius and the radius.
        /// </summary>
        private static GeoPoint2[] Star(Random r, double cx, double cy, double radius, double inner, int corners)
        {
            var ring = new GeoPoint2[corners];

            for (int i = 0; i < corners; i++)
            {
                double angle = 2.0 * Math.PI * (i + Between(r, 0.0, 0.6)) / corners;
                double reach = radius * Between(r, inner, 1.0);
                ring[i] = new GeoPoint2(cx + reach * Math.Cos(angle), cy + reach * Math.Sin(angle));
            }

            return ring;
        }

        private static GeoPoint3[] Lift(Meet3Flat flat, GeoPoint2[] ring) => ring.Select(flat.Corner).ToArray();

        /// <summary>
        /// Gets the arc of an edge with a bulge between two points in space, worked out from the chord, the bulge and the normal alone.
        /// </summary>
        internal static Meet3Part OfBulge(GeoPoint3 a, GeoPoint3 b, double bulge, double[] n)
        {
            if (Math.Abs(bulge) < 1E-12)
            {
                return Meet3Segment.Between(a, b);
            }

            double angle = 4.0 * Math.Atan(bulge);
            double[] chord = { b.X - a.X, b.Y - a.Y, b.Z - a.Z };
            double length = Math.Sqrt(Dot(chord, chord));
            double[] dir = Unit(chord);
            double radius = length / (2.0 * Math.Sin(Math.Abs(angle) / 2.0));

            // The centre is on the left of the chord, looking down the normal, for a counter-clockwise arc.
            double offset = radius * Math.Cos(Math.Abs(angle) / 2.0) * (bulge > 0.0 ? 1.0 : -1.0);
            double[] left = Cross(n, dir);
            double[] centre = { 0.5 * (a.X + b.X) + left[0] * offset, 0.5 * (a.Y + b.Y) + left[1] * offset, 0.5 * (a.Z + b.Z) + left[2] * offset };
            double[] s = Unit(new[] { a.X - centre[0], a.Y - centre[1], a.Z - centre[2] });

            return new Meet3Arc(centre, s, Cross(n, s), radius, angle);
        }

        private static GeoVector3 SquareTo(Random r, GeoPoint3 a, GeoPoint3 b)
        {
            double[] dir = Unit(new[] { b.X - a.X, b.Y - a.Y, b.Z - a.Z });
            double[] any = Direction(r);
            double along = Dot(any, dir);

            return V(Unit(new[] { any[0] - along * dir[0], any[1] - along * dir[1], any[2] - along * dir[2] }));
        }

        /// <summary>
        /// Draws a shape of a kind about a centre, of a size.
        /// </summary>
        internal static Meet3Shape Draw(Random r, string kind, double cx, double cy, double cz, double size)
        {
            var shape = new Meet3Shape { Kind = kind };
            var origin = new[] { cx + Between(r, -size, size), cy + Between(r, -size, size), cz + Between(r, -size, size) };
            double[] n = Direction(r);
            Frame(n, out double[] u, out double[] v);
            double radius = Between(r, 0.15, 1.0) * size;

            switch (kind)
            {
                case "segment":
                {
                    double length = Between(r, 0.1, 2.0) * size;
                    double[] d = Direction(r);
                    var a = P(origin[0], origin[1], origin[2]);
                    var b = P(origin[0] + length * d[0], origin[1] + length * d[1], origin[2] + length * d[2]);
                    shape.Shape = new GeoLine3(a, b);
                    shape.Parts.Add(Meet3Segment.Between(a, b));
                    break;
                }

                case "ray":
                {
                    double[] d = Direction(r);
                    shape.Shape = new GeoRay3(P(origin[0], origin[1], origin[2]), V(d));
                    shape.Parts.Add(new Meet3Segment(origin[0], origin[1], origin[2], d[0], d[1], d[2], 0.0, true, false));
                    break;
                }

                case "xline":
                {
                    double[] d = Direction(r);
                    shape.Shape = new GeoXLine3(P(origin[0], origin[1], origin[2]), V(d));
                    shape.Parts.Add(new Meet3Segment(origin[0], origin[1], origin[2], d[0], d[1], d[2], 0.0, false, true));
                    break;
                }

                case "plane":
                    shape.Shape = new GeoPlane3(P(origin[0], origin[1], origin[2]), V(n));
                    shape.Parts.Add(new Meet3Plane(origin[0], origin[1], origin[2], n[0], n[1], n[2]));
                    break;

                case "polyline":
                {
                    int count = 2 + r.Next(5);
                    var points = new GeoPoint3[count];

                    for (int i = 0; i < count; i++)
                    {
                        points[i] = P(origin[0] + Between(r, -radius, radius), origin[1] + Between(r, -radius, radius), origin[2] + Between(r, -radius, radius));
                    }

                    shape.Shape = new GeoPolyline3(points);

                    for (int i = 0; i + 1 < count; i++)
                    {
                        shape.Parts.Add(Meet3Segment.Between(points[i], points[i + 1]));
                    }

                    break;
                }

                case "polygon":
                {
                    GeoPoint2[] ring = Star(r, 0.0, 0.0, radius, 0.4, 3 + r.Next(5));
                    var flat = new Meet3Flat(origin, u, v, new List<GeoPoint2[]> { ring });
                    shape.Parts.Add(flat);
                    shape.Shape = new GeoPolygon3(Lift(flat, ring));
                    break;
                }

                case "triangle":
                {
                    var a = new[] { origin[0], origin[1], origin[2] };
                    double[] b = { a[0] + Between(r, -radius, radius), a[1] + Between(r, -radius, radius), a[2] + Between(r, -radius, radius) };
                    double[] c = { a[0] + Between(r, -radius, radius), a[1] + Between(r, -radius, radius), a[2] + Between(r, -radius, radius) };
                    double[] ab = { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
                    double[] ac = { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
                    double[] tu = Unit(ab);
                    double[] tn = Unit(Cross(tu, ac));
                    double[] tv = Cross(tn, tu);
                    var ring = new[] { new GeoPoint2(0.0, 0.0), new GeoPoint2(Math.Sqrt(Dot(ab, ab)), 0.0), new GeoPoint2(Dot(ac, tu), Dot(ac, tv)) };
                    shape.Parts.Add(new Meet3Flat(a, tu, tv, new List<GeoPoint2[]> { ring }));
                    shape.Shape = new GeoTriangle3(P(a[0], a[1], a[2]), P(b[0], b[1], b[2]), P(c[0], c[1], c[2]));
                    break;
                }

                case "face":
                {
                    // Four corners or more, each between half the radius and the radius at angles that stay under 2.51 radians apart, hold
                    // the disc of 0.155 of the radius about the origin: a hole of 0.14 of it, drawn about the same point, is inside.
                    GeoPoint2[] outer = Star(r, 0.0, 0.0, radius, 0.5, 4 + r.Next(3));
                    GeoPoint2[] hole = Star(r, 0.0, 0.0, radius * 0.14, 0.5, 3 + r.Next(3));
                    var flat = new Meet3Flat(origin, u, v, new List<GeoPoint2[]> { outer, hole });
                    shape.Parts.Add(flat);
                    shape.Shape = new GeoFace3(new GeoPolygon3(Lift(flat, outer)), new[] { new GeoPolygon3(Lift(flat, hole)) });
                    break;
                }

                case "aabb":
                {
                    double hx = Between(r, 0.1, 1.0) * size;
                    double hy = Between(r, 0.1, 1.0) * size;
                    double hz = Between(r, 0.1, 1.0) * size;
                    shape.Shape = new GeoAabb3(P(origin[0] - hx, origin[1] - hy, origin[2] - hz), P(origin[0] + hx, origin[1] + hy, origin[2] + hz));
                    AddBlock(shape, origin, new[] { new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 } }, new[] { hx, hy, hz });
                    break;
                }

                case "obb":
                case "solid":
                {
                    double sx = Between(r, 0.2, 2.0) * size;
                    double sy = Between(r, 0.2, 2.0) * size;
                    double sz = Between(r, 0.2, 2.0) * size;
                    var box = new GeoObb3(P(origin[0], origin[1], origin[2]), sx, sy, sz, V(u), V(v));
                    shape.Shape = kind == "obb" ? (object)box : box.ToSolid();
                    AddBlock(shape, origin, new[] { u, v, n }, new[] { 0.5 * sx, 0.5 * sy, 0.5 * sz });
                    break;
                }

                case "sphere":
                    shape.Shape = new GeoSphere3(P(origin[0], origin[1], origin[2]), radius);
                    shape.Parts.Add(new Meet3Sphere(origin[0], origin[1], origin[2], radius));
                    shape.Inside = (x, y, z) => Math.Sqrt((x - origin[0]) * (x - origin[0]) + (y - origin[1]) * (y - origin[1]) + (z - origin[2]) * (z - origin[2])) <= radius;
                    break;

                case "arc":
                {
                    double sweep = Between(r, 0.3, 2.0 * Math.PI - 0.3) * (r.Next(2) == 0 ? 1.0 : -1.0);
                    double start = Between(r, 0.0, 2.0 * Math.PI);
                    var arc = new GeoArc3(P(origin[0], origin[1], origin[2]), V(n), radius, start, start + sweep, sweep < 0.0);
                    shape.Shape = arc;
                    shape.Parts.Add(OfArc(arc));
                    break;
                }

                case "circle":
                    shape.Shape = new GeoCircle3(P(origin[0], origin[1], origin[2]), V(n), radius);
                    AddDisc(shape, origin, u, v, radius, radius);
                    break;

                case "ellipse":
                {
                    double b = radius * Between(r, 0.1, 1.0);
                    double angle = Between(r, 0.0, 2.0 * Math.PI);
                    double[] major = { Math.Cos(angle) * u[0] + Math.Sin(angle) * v[0], Math.Cos(angle) * u[1] + Math.Sin(angle) * v[1], Math.Cos(angle) * u[2] + Math.Sin(angle) * v[2] };
                    double[] minor = Cross(n, major);
                    shape.Shape = new GeoEllipse3(P(origin[0], origin[1], origin[2]), V(n), V(major), radius, b);
                    AddDisc(shape, origin, major, minor, radius, b);
                    break;
                }

                case "edge":
                {
                    double[] d = Direction(r);
                    var a = P(origin[0], origin[1], origin[2]);
                    var b = P(origin[0] + radius * d[0], origin[1] + radius * d[1], origin[2] + radius * d[2]);
                    double bulge = r.Next(2) == 0 ? 0.0 : Between(r, -1.5, 1.5);

                    if (Math.Abs(bulge) < 0.05)
                    {
                        bulge = 0.0;
                    }

                    GeoVector3 normal = SquareTo(r, a, b);
                    shape.Shape = bulge == 0.0 ? new GeoEdge3(a, b) : new GeoEdge3(a, b, bulge, normal);
                    shape.Parts.Add(OfBulge(a, b, bulge, new[] { normal.X, normal.Y, normal.Z }));
                    break;
                }

                case "chain":
                {
                    int count = 2 + r.Next(4);
                    var points = new GeoPoint3[count];

                    for (int i = 0; i < count; i++)
                    {
                        points[i] = P(origin[0] + Between(r, -radius, radius), origin[1] + Between(r, -radius, radius), origin[2] + Between(r, -radius, radius));
                    }

                    var bulges = new double[count - 1];
                    var normals = new GeoVector3[count - 1];

                    for (int i = 0; i + 1 < count; i++)
                    {
                        bulges[i] = r.Next(3) == 0 ? 0.0 : Between(r, -1.2, 1.2);
                        normals[i] = SquareTo(r, points[i], points[i + 1]);
                        shape.Parts.Add(OfBulge(points[i], points[i + 1], bulges[i], new[] { normals[i].X, normals[i].Y, normals[i].Z }));
                    }

                    shape.Shape = new GeoPolylineArc3(points, bulges, normals);
                    break;
                }

                default:
                {
                    GeoPoint2[] ring = Star(r, 0.0, 0.0, radius, 0.5, 3 + r.Next(4));
                    var flat = new Meet3Flat(origin, u, v, new List<GeoPoint2[]> { ring });
                    GeoPoint3[] corners = Lift(flat, ring);
                    var bulges = new double[ring.Length];
                    var normals = new GeoVector3[ring.Length];

                    for (int i = 0; i < ring.Length; i++)
                    {
                        bulges[i] = r.Next(3) == 0 ? 0.0 : Between(r, -0.4, 0.4);
                        normals[i] = V(n);
                        shape.Parts.Add(OfBulge(corners[i], corners[(i + 1) % ring.Length], bulges[i], n));
                    }

                    shape.Shape = new GeoPolygonArc3(corners, bulges, normals);
                    break;
                }
            }

            return shape;
        }

        private static void AddBlock(Meet3Shape shape, double[] centre, double[][] axes, double[] half)
        {
            var block = new Meet3Block(centre, axes.Select(Unit).ToArray(), half);
            shape.Parts.Add(block);
            shape.Inside = block.Holds;
        }

        private static void AddDisc(Meet3Shape shape, double[] centre, double[] u, double[] v, double a, double b)
        {
            var disc = new Meet3Ellipse(centre, u, v, a, b, true);
            var rim = new Meet3Ellipse(centre, u, v, a, b, false);
            shape.Parts.Add(disc);
            shape.Parts.Add(rim);
            shape.Rims = new List<Meet3Part> { rim };
        }

        /// <summary>Gets an arc as the oracle reads it, from the centre, start and sweep the library gives it.</summary>
        private static Meet3Part OfArc(GeoArc3 arc)
        {
            GeoPoint3 s = arc.StartPoint;
            double[] centre = { arc.Center.X, arc.Center.Y, arc.Center.Z };
            double[] start = Unit(new[] { s.X - centre[0], s.Y - centre[1], s.Z - centre[2] });
            double[] normal = { arc.Normal.X, arc.Normal.Y, arc.Normal.Z };

            return new Meet3Arc(centre, start, Cross(normal, start), arc.Radius, arc.SweptAngle);
        }
    }
}
