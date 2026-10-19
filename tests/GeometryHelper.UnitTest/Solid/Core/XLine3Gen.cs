using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Random shapes in space with their pieces for the oracle, and random lines to ask them about, for the fuzz of the
    /// infinite line.
    /// </summary>
    internal static class XLine3Gen
    {
        /// <summary>The kinds of shape the line meets in space, as the fuzz names them.</summary>
        internal static readonly string[] Kinds =
        {
            "Point3", "Line3", "Ray3", "XLine3", "Arc3", "Circle3", "Ellipse3", "Edge3", "Polyline3", "Polygon3", "PolylineArc3",
            "PolygonArc3", "Triangle3", "Face3", "Plane3", "Aabb3", "Obb3", "Solid3"
        };

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        internal static Vec RandomPoint(Random r, double reach = 120.0) => new Vec(Between(r, -reach, reach), Between(r, -reach, reach), Between(r, -reach, reach));

        internal static Vec RandomUnit(Random r)
        {
            while (true)
            {
                var v = new Vec(Between(r, -1, 1), Between(r, -1, 1), Between(r, -1, 1));

                if (v.Length > 0.2 && v.Length <= 1.0)
                {
                    return v.Unit();
                }
            }
        }

        /// <summary>Two unit vectors square to a unit one and to each other, so that u cross v is the unit one.</summary>
        internal static void Frame(Vec n, out Vec u, out Vec v)
        {
            Vec seed = Math.Abs(n.X) < 0.6 ? new Vec(1, 0, 0) : new Vec(0, 1, 0);

            u = n.Cross(seed).Unit();
            v = n.Cross(u);
        }

        /// <summary>A polygon about a centre in the plane of a normal, star-shaped, its corners turning counter-clockwise about the normal.</summary>
        private static GeoPoint3[] StarPolygon(Random r, Vec center, Vec normal, int corners, double minRadius, double maxRadius)
        {
            Frame(normal, out Vec u, out Vec v);

            double[] angles = Enumerable.Range(0, corners).Select(i => (i + Between(r, 0.1, 0.9)) * 2 * Math.PI / corners).ToArray();

            return angles.Select(a =>
            {
                double radius = Between(r, minRadius, maxRadius);

                return (center + u * (radius * Math.Cos(a)) + v * (radius * Math.Sin(a))).ToPoint();
            }).ToArray();
        }

        private static IReadOnlyList<GeoPoint3> Ring(GeoPolygon3 polygon) => polygon.Vertices;

        private static FlatPiece Flat(GeoPolygon3 polygon) => new FlatPiece(Ring(polygon), null);

        private static Func<Random, Vec> PointInFlat(IReadOnlyList<FlatPiece> faces, IReadOnlyList<Vec[]> boxes)
        {
            return r =>
            {
                for (int attempts = 0; attempts < 1000; attempts++)
                {
                    int i = r.Next(faces.Count);
                    Vec[] box = boxes[i];
                    var p = new Vec(Between(r, box[0].X, box[1].X), Between(r, box[0].Y, box[1].Y), Between(r, box[0].Z, box[1].Z));
                    Vec foot = p - faces[i].Normal * faces[i].Normal.Dot(p - box[2]);

                    if (faces[i].Holds(foot))
                    {
                        return foot;
                    }
                }

                return boxes[0][2];
            };
        }

        private static Vec[] BoxOf(IEnumerable<GeoPoint3> points, GeoPoint3 reference)
        {
            GeoPoint3[] list = points.ToArray();

            return new[]
            {
                new Vec(list.Min(p => p.X), list.Min(p => p.Y), list.Min(p => p.Z)),
                new Vec(list.Max(p => p.X), list.Max(p => p.Y), list.Max(p => p.Z)),
                Vec.Of(reference)
            };
        }

        private static XLineSpecimen OfFlat(string kind, object shape, IReadOnlyList<GeoPoint3> outline, IEnumerable<IReadOnlyList<GeoPoint3>> holes)
        {
            var piece = new FlatPiece(outline, holes);

            return new XLineSpecimen(kind, shape, new Piece[] { piece }, PointInFlat(new[] { piece }, new[] { BoxOf(outline, outline[0]) }), true);
        }

        /// <summary>The faces of a solid as flat pieces.</summary>
        private static XLineSpecimen OfSolid(string kind, object shape, IReadOnlyList<GeoFace3> faces)
        {
            var pieces = new List<FlatPiece>();
            var boxes = new List<Vec[]>();

            foreach (GeoFace3 face in faces)
            {
                pieces.Add(new FlatPiece(face.Boundary.Vertices, face.Holes.Select(h => h.Vertices)));
                boxes.Add(BoxOf(face.Boundary.Vertices, face.Boundary.Vertices[0]));
            }

            return new XLineSpecimen(kind, shape, pieces, PointInFlat(pieces, boxes), true);
        }

        private static List<Piece> EdgePieces(GeoEdge3 edge)
        {
            return new List<Piece> { edge.IsArc ? (Piece)new ArcPiece(edge.ToArc()) : new SegPiece(edge.StartPoint, edge.EndPoint) };
        }

        private static GeoEdge3 RandomEdge(Random r, Vec a, Vec b, double curvedShare)
        {
            if (r.NextDouble() >= curvedShare)
            {
                return new GeoEdge3(a.ToPoint(), b.ToPoint());
            }

            Vec chord = (b - a).Unit();
            Vec side = RandomUnit(r);
            Vec normal = (side - chord * side.Dot(chord)).Unit();
            double bulge = Between(r, 0.2, 1.2) * (r.Next(2) == 0 ? 1 : -1);

            return new GeoEdge3(a.ToPoint(), b.ToPoint(), bulge, new GeoVector3(normal.X, normal.Y, normal.Z));
        }

        private static Func<Random, Vec> PointOnEdges(IReadOnlyList<GeoEdge3> edges)
        {
            return r =>
            {
                GeoEdge3 edge = edges[r.Next(edges.Count)];

                return Vec.Of(edge.GetPointAtParameter(r.NextDouble()));
            };
        }

        /// <summary>Makes one random shape of a kind.</summary>
        internal static XLineSpecimen Make(string kind, Random r)
        {
            switch (kind)
            {
                case "Point3":
                {
                    Vec p = RandomPoint(r);

                    return new XLineSpecimen(kind, p.ToPoint(), new Piece[] { new PointPiece(p.ToPoint()) }, rr => p, true);
                }

                case "Line3":
                {
                    Vec a = RandomPoint(r);
                    Vec b = a + RandomUnit(r) * Between(r, 20, 200);
                    var line = new GeoLine3(a.ToPoint(), b.ToPoint());

                    return new XLineSpecimen(kind, line, new Piece[] { new SegPiece(line.StartPoint, line.EndPoint) }, rr => a + (b - a) * rr.NextDouble(), true);
                }

                case "Ray3":
                {
                    Vec o = RandomPoint(r);
                    Vec d = RandomUnit(r);
                    var ray = new GeoRay3(o.ToPoint(), new GeoVector3(d.X, d.Y, d.Z));

                    return new XLineSpecimen(kind, ray, new Piece[] { new RayPiece(ray) }, rr => o + d * Between(rr, 0, 200), true);
                }

                case "XLine3":
                {
                    Vec o = RandomPoint(r);
                    Vec d = RandomUnit(r);
                    var xline = new GeoXLine3(o.ToPoint(), new GeoVector3(d.X, d.Y, d.Z));

                    return new XLineSpecimen(kind, xline, new Piece[] { new LinePiece(xline) }, rr => o + d * Between(rr, -150, 150), true);
                }

                case "Arc3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    double radius = Between(r, 10, 80);
                    double start = Between(r, 0, 2 * Math.PI);
                    double sweep = Between(r, 0.4, 6.0);
                    var arc = new GeoArc3(c.ToPoint(), new GeoVector3(n.X, n.Y, n.Z), radius, start, start + sweep);
                    var piece = new ArcPiece(arc);

                    return new XLineSpecimen(kind, arc, new Piece[] { piece }, rr => Vec.Of(arc.GetPointAtParameter(rr.NextDouble())), false);
                }

                case "Circle3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    double radius = Between(r, 10, 80);
                    var circle = new GeoCircle3(c.ToPoint(), new GeoVector3(n.X, n.Y, n.Z), radius);

                    Frame(n, out Vec u, out Vec v);

                    var piece = new ArcPiece(c, n, c + u * radius, 2 * Math.PI);

                    return new XLineSpecimen(kind, circle, new Piece[] { piece }, rr => { double a = Between(rr, 0, 2 * Math.PI); return c + u * (radius * Math.Cos(a)) + v * (radius * Math.Sin(a)); }, false);
                }

                case "Ellipse3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    Frame(n, out Vec u, out Vec v);

                    double angle = Between(r, 0, 2 * Math.PI);
                    Vec major = u * Math.Cos(angle) + v * Math.Sin(angle);
                    Vec minor = n.Cross(major);
                    double a = Between(r, 30, 100);
                    double b = Between(r, 10, a);
                    var ellipse = new GeoEllipse3(c.ToPoint(), new GeoVector3(n.X, n.Y, n.Z), new GeoVector3(major.X, major.Y, major.Z), a, b);

                    return new XLineSpecimen(kind, ellipse, new Piece[] { new EllipsePiece(ellipse) }, rr => { double t = Between(rr, 0, 2 * Math.PI); return c + major * (a * Math.Cos(t)) + minor * (b * Math.Sin(t)); }, false);
                }

                case "Edge3":
                {
                    Vec a = RandomPoint(r);
                    Vec b = a + RandomUnit(r) * Between(r, 30, 160);
                    GeoEdge3 edge = RandomEdge(r, a, b, 0.6);

                    return new XLineSpecimen(kind, edge, EdgePieces(edge), PointOnEdges(new[] { edge }), false);
                }

                case "Polyline3":
                {
                    int count = r.Next(2, 7);
                    var points = new List<GeoPoint3> { RandomPoint(r).ToPoint() };

                    while (points.Count < count)
                    {
                        points.Add((Vec.Of(points[points.Count - 1]) + RandomUnit(r) * Between(r, 20, 120)).ToPoint());
                    }

                    var polyline = new GeoPolyline3(points);
                    List<GeoLine3> edges = Enumerable.Range(0, polyline.EdgeCount).Select(polyline.GetEdgeAt).ToList();

                    return new XLineSpecimen(kind, polyline, edges.Select(e => (Piece)new SegPiece(e.StartPoint, e.EndPoint)).ToList(), rr => { GeoLine3 e = edges[rr.Next(edges.Count)]; return Vec.Of(e.StartPoint) + (Vec.Of(e.EndPoint) - Vec.Of(e.StartPoint)) * rr.NextDouble(); }, true);
                }

                case "PolylineArc3":
                {
                    int count = r.Next(3, 6);
                    var points = new List<Vec> { RandomPoint(r) };

                    while (points.Count < count)
                    {
                        points.Add(points[points.Count - 1] + RandomUnit(r) * Between(r, 40, 120));
                    }

                    var edges = new List<GeoEdge3>();

                    for (int i = 0; i + 1 < points.Count; i++)
                    {
                        edges.Add(RandomEdge(r, points[i], points[i + 1], 0.5));
                    }

                    var chain = new GeoPolylineArc3(edges);
                    var pieces = new List<Piece>();

                    for (int i = 0; i < chain.EdgeCount; i++)
                    {
                        pieces.AddRange(EdgePieces(chain.GetEdgeAt(i)));
                    }

                    return new XLineSpecimen(kind, chain, pieces, PointOnEdges(Enumerable.Range(0, chain.EdgeCount).Select(chain.GetEdgeAt).ToList()), false);
                }

                case "PolygonArc3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    int corners = r.Next(3, 7);
                    GeoPoint3[] ring = StarPolygon(r, c, n, corners, 40, 90);
                    var bulges = new double[corners];
                    var normals = new GeoVector3[corners];

                    for (int i = 0; i < corners; i++)
                    {
                        bulges[i] = r.Next(2) == 0 ? 0.0 : Between(r, 0.1, 0.4);
                        normals[i] = new GeoVector3(n.X, n.Y, n.Z);
                    }

                    var loop = new GeoPolygonArc3(ring, bulges, normals);
                    var pieces = new List<Piece>();

                    for (int i = 0; i < loop.EdgeCount; i++)
                    {
                        pieces.AddRange(EdgePieces(loop.GetEdgeAt(i)));
                    }

                    return new XLineSpecimen(kind, loop, pieces, PointOnEdges(Enumerable.Range(0, loop.EdgeCount).Select(loop.GetEdgeAt).ToList()), false);
                }

                case "Polygon3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    var polygon = new GeoPolygon3(StarPolygon(r, c, n, r.Next(3, 8), 30, 90));

                    return OfFlat(kind, polygon, polygon.Vertices, null);
                }

                case "Triangle3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    GeoPoint3[] ring = StarPolygon(r, c, n, 3, 30, 90);
                    var triangle = new GeoTriangle3(ring[0], ring[1], ring[2]);

                    return OfFlat(kind, triangle, ring, null);
                }

                case "Face3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    var outline = new GeoPolygon3(StarPolygon(r, c, n, r.Next(3, 7), 60, 100));
                    var hole = new GeoPolygon3(StarPolygon(r, c, n, r.Next(3, 6), 8, 25));
                    var face = new GeoFace3(outline, new[] { hole });

                    return OfFlat(kind, face, outline.Vertices, new[] { hole.Vertices });
                }

                case "Plane3":
                {
                    Vec c = RandomPoint(r);
                    Vec n = RandomUnit(r);
                    var plane = new GeoPlane3(c.ToPoint(), new GeoVector3(n.X, n.Y, n.Z));

                    Frame(n, out Vec u, out Vec v);

                    return new XLineSpecimen(kind, plane, new Piece[] { new PlanePiece(plane) }, rr => c + u * Between(rr, -150, 150) + v * Between(rr, -150, 150), true);
                }

                case "Aabb3":
                {
                    Vec a = RandomPoint(r, 60);
                    Vec b = a + new Vec(Between(r, 20, 120), Between(r, 20, 120), Between(r, 20, 120));
                    var box = new GeoAabb3(a.ToPoint(), b.ToPoint());
                    GeoFace3[] faces = new GeoObb3(((a + b) * 0.5).ToPoint(), b.X - a.X, b.Y - a.Y, b.Z - a.Z).ToSolid().Faces.ToArray();

                    return OfSolid(kind, box, faces);
                }

                case "Obb3":
                {
                    Vec c = RandomPoint(r, 60);
                    Vec n = RandomUnit(r);
                    Frame(n, out Vec u, out Vec v);

                    var box = new GeoObb3(c.ToPoint(), Between(r, 20, 120), Between(r, 20, 120), Between(r, 20, 120), new GeoVector3(u.X, u.Y, u.Z), new GeoVector3(v.X, v.Y, v.Z));

                    return OfSolid(kind, box, box.ToSolid().Faces.ToArray());
                }

                case "Solid3":
                {
                    Vec c = RandomPoint(r, 60);
                    Vec n = RandomUnit(r);
                    var profile = new GeoPolygon3(StarPolygon(r, c, n, r.Next(3, 7), 25, 70));
                    Vec dir = n * Between(r, 20, 100) + RandomUnit(r) * Between(r, 0, 30);
                    var solid = GeoSolid3.Extrude(profile, new GeoVector3(dir.X, dir.Y, dir.Z));

                    return OfSolid(kind, solid, solid.Faces);
                }

                default:
                    throw new ArgumentException("No such kind: " + kind);
            }
        }

        /// <summary>The ways a line is laid for a shape.</summary>
        internal enum Layout
        {
            /// <summary>Anywhere, in any direction.</summary>
            Random,

            /// <summary>Through a point of the shape, in any direction, from anywhere along it.</summary>
            Through,

            /// <summary>Along an axis, from anywhere.</summary>
            Axis,

            /// <summary>Through a point of the shape and then moved square to itself by a few thousandths.</summary>
            NearlyThrough
        }

        /// <summary>Makes one random line for a shape.</summary>
        internal static GeoXLine3 MakeLine(Random r, XLineSpecimen specimen, Layout layout)
        {
            Vec o;
            Vec d;

            switch (layout)
            {
                case Layout.Random:
                    o = RandomPoint(r, 150);
                    d = RandomUnit(r);
                    break;

                case Layout.Axis:
                    o = RandomPoint(r, 150);
                    d = new[] { new Vec(1, 0, 0), new Vec(0, 1, 0), new Vec(0, 0, 1) }[r.Next(3)] * (r.Next(2) == 0 ? 1 : -1);
                    break;

                default:
                {
                    Vec on = specimen.PointOn(r);
                    double[] shifts = { 0.0, Between(r, -150, 150), Between(r, -150, 150), 1E3, -1E3, 1E6, -1E6 };

                    d = RandomUnit(r);
                    o = on + d * shifts[r.Next(shifts.Length)];

                    if (layout == Layout.NearlyThrough)
                    {
                        Vec side = RandomUnit(r);

                        side = (side - d * side.Dot(d)).Unit();
                        o = o + side * (Between(r, 2E-4, 3E-3) * (r.Next(2) == 0 ? 1 : -1));
                    }

                    break;
                }
            }

            return new GeoXLine3(o.ToPoint(), new GeoVector3(d.X, d.Y, d.Z));
        }
    }
}
