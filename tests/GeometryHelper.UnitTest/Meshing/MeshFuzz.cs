using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Random faces and random ways to mesh them, and what a mesh promises, as a reason it fails or none: the meshes of
    /// <see cref="MeshFuzzTests"/>.
    /// </summary>
    /// <remarks>
    /// The faces are stars, Ls and boxes, near the origin or kilometres out, with holes inside them that may touch each
    /// other, corner to corner or side to side; the ways are every kind, a grid of any size, joint, angle, alignment and
    /// origin, strips at any angle. <see cref="Random"/> is seeded, so a seed is the same face and way every run.
    /// </remarks>
    internal static class MeshFuzz
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        public static GeoPolygon2 Star(Random random, GeoPoint2 center, double radius, int corners, double wobble)
        {
            var points = new List<GeoPoint2>();
            double start = random.NextDouble() * Math.PI * 2;

            for (int i = 0; i < corners; i++)
            {
                double angle = start + i * 2 * Math.PI / corners;
                double r = radius * (1.0 - wobble * random.NextDouble());
                points.Add(P(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle)));
            }

            return new GeoPolygon2(points);
        }

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        public static GeoFace2 RandomFace(Random random, out string what)
        {
            double offsetX = random.Next(3) == 0 ? 500000 + random.NextDouble() * 1000 : 0.0;
            double offsetY = random.Next(3) == 0 ? 7000000 + random.NextDouble() * 1000 : 0.0;
            var center = P(offsetX, offsetY);
            int style = random.Next(4);
            GeoPolygon2 boundary;

            if (style == 0)
            {
                boundary = Star(random, center, 3000, 5 + random.Next(20), 0.6);
            }
            else if (style == 1)
            {
                // A rectilinear outline: an L or a U.
                double w = 2000 + random.NextDouble() * 4000, h = 2000 + random.NextDouble() * 4000;
                double cx = w * (0.3 + 0.4 * random.NextDouble()), cy = h * (0.3 + 0.4 * random.NextDouble());
                boundary = new GeoPolygon2(P(offsetX, offsetY), P(offsetX + w, offsetY), P(offsetX + w, offsetY + cy), P(offsetX + cx, offsetY + cy), P(offsetX + cx, offsetY + h), P(offsetX, offsetY + h));
            }
            else
            {
                double w = 1000 + random.NextDouble() * 6000, h = 1000 + random.NextDouble() * 4000;
                boundary = Box(offsetX, offsetY, offsetX + w, offsetY + h);
            }

            var holes = new List<GeoPolygon2>();
            int wanted = random.Next(6);
            double minX = boundary.Vertices.Min(p => p.X), maxX = boundary.Vertices.Max(p => p.X);
            double minY = boundary.Vertices.Min(p => p.Y), maxY = boundary.Vertices.Max(p => p.Y);

            for (int attempt = 0; attempt < 60 && holes.Count < wanted; attempt++)
            {
                GeoPolygon2 hole;
                int kind = random.Next(4);

                if (kind == 0 && holes.Count > 0 && holes[holes.Count - 1].VertexCount == 4)
                {
                    // A box beside the last box, sharing a side or a corner.
                    GeoPolygon2 last = holes[holes.Count - 1];
                    double lx0 = last.Vertices.Min(p => p.X), lx1 = last.Vertices.Max(p => p.X);
                    double ly0 = last.Vertices.Min(p => p.Y), ly1 = last.Vertices.Max(p => p.Y);
                    double size = 100 + random.NextDouble() * 300;
                    hole = random.Next(2) == 0 ? Box(lx1, ly0, lx1 + size, ly0 + size) : Box(lx1, ly1, lx1 + size, ly1 + size);
                }
                else if (kind == 1)
                {
                    hole = Star(random, P(minX + random.NextDouble() * (maxX - minX), minY + random.NextDouble() * (maxY - minY)), 100 + random.NextDouble() * 400, 3 + random.Next(8), 0.5);
                }
                else
                {
                    double x = minX + random.NextDouble() * (maxX - minX), y = minY + random.NextDouble() * (maxY - minY);
                    double size = 50 + random.NextDouble() * 600;
                    hole = Box(x, y, x + size * (0.3 + random.NextDouble()), y + size * (0.3 + random.NextDouble()));
                }

                // Within the boundary, and apart from the holes already there or only touching them.
                if (!hole.Vertices.All(v => boundary.Locate(v, Tolerance.Default) != PointLocation.OutSide))
                {
                    continue;
                }

                bool clear = true;

                foreach (GeoPolygon2 other in holes)
                {
                    if (hole.Vertices.Any(v => other.Locate(v, Tolerance.Default) == PointLocation.Inside) || other.Vertices.Any(v => hole.Locate(v, Tolerance.Default) == PointLocation.Inside))
                    {
                        clear = false;
                        break;
                    }

                    // Edges crossing outright would make the holes overlap.
                    for (int i = 0; i < hole.VertexCount && clear; i++)
                    {
                        for (int j = 0; j < other.VertexCount && clear; j++)
                        {
                            var a = new GeoLine2(hole[i], hole[(i + 1) % hole.VertexCount]);
                            var b = new GeoLine2(other[j], other[(j + 1) % other.VertexCount]);

                            if (a.TryIntersectWith(b, out GeoPoint2 at, Tolerance.Default) && !hole.Vertices.Concat(other.Vertices).Any(v => v.IsEqualTo(at, Tolerance.Default)) && a.DistanceTo(at) < 1E-6 && b.DistanceTo(at) < 1E-6)
                            {
                                double ta = a.GetParameterAtPoint(at), tb = b.GetParameterAtPoint(at);

                                if (ta > 1E-6 && ta < 1 - 1E-6 && tb > 1E-6 && tb < 1 - 1E-6 && !a.IsParallelTo(b, Tolerance.Default))
                                {
                                    clear = false;
                                }
                            }
                        }
                    }
                }

                if (clear)
                {
                    holes.Add(hole);
                }
            }

            what = $"style {style}, {boundary.VertexCount} corners, {holes.Count} holes, at ({offsetX:0}, {offsetY:0})";
            return new GeoFace2(boundary, holes);
        }

        public static MeshOptions RandomOptions(Random random, out string what)
        {
            int kind = random.Next(6);

            if (kind == 0)
            {
                what = "triangles";
                return MeshOptions.Triangles;
            }

            if (kind == 1)
            {
                what = "convex";
                return MeshOptions.Convex;
            }

            if (kind == 2)
            {
                double angle = random.Next(3) == 0 ? 0.0 : random.NextDouble() * Math.PI;
                what = $"strips {angle:R}";
                return MeshOptions.Strips(angle);
            }

            double w = 100 + random.NextDouble() * 1500, h = 100 + random.NextDouble() * 1500;

            if (random.Next(3) == 0)
            {
                w = Math.Round(w / 100) * 100;
                h = Math.Round(h / 100) * 100;
            }

            double joint = random.Next(3) == 0 ? 0.0 : random.Next(2) == 0 ? 3.0 : random.NextDouble() * 50;
            double? turn = random.Next(2) == 0 ? (double?)null : random.Next(3) == 0 ? Math.PI / 2 : random.NextDouble() * Math.PI;
            var u = (GridAlignment)random.Next(4);
            var v = (GridAlignment)random.Next(4);
            GeoPoint2? origin = random.Next(4) == 0 ? (GeoPoint2?)P(random.NextDouble() * 5000 - 2500, random.NextDouble() * 5000 - 2500) : null;
            what = $"grid {w:R} x {h:R}, joint {joint:R}, angle {turn?.ToString("R") ?? "none"}, {u}/{v}, origin {origin?.ToString() ?? "none"}";
            return new MeshOptions(MeshKind.Grid, w, h, joint, turn, u, v, origin);
        }

        /// <summary>
        /// What a mesh promises, as a reason it fails, or null.
        /// </summary>
        public static string Check(GeoMesh2 mesh, GeoFace2 face, MeshOptions options, Tolerance tolerance)
        {
            if (mesh.FaceCount == 0)
            {
                return face.Area > 1.0 ? "no faces" : null;
            }

            GeoPolygon2[] faces = mesh.GetFaces();

            for (int i = 0; i < faces.Length; i++)
            {
                int[] indices = mesh.GetFaceIndices(i);

                if (indices.Length < 3 || indices.Distinct().Count() != indices.Length)
                {
                    return $"face {i} has corners {string.Join(" ", indices)}";
                }

                if (!(faces[i].SignedArea > 0.0))
                {
                    return $"face {i} runs clockwise: {faces[i].SignedArea}";
                }

                // Simple as drawn: a sliver of material thinner than the tolerance is meshed with faces as thin.
                if (!faces[i].IsSimple(new Tolerance(1E-9, 1E-9)))
                {
                    return $"face {i} is not simple";
                }

                if (options.Kind == MeshKind.Triangles && indices.Length != 3)
                {
                    return $"face {i} of {indices.Length} corners";
                }
            }

            bool joints = options.Kind == MeshKind.Grid && options.Joint > tolerance.EqualPoint;

            if (!joints)
            {
                // The area the material has, as the booleans read it: a hole reaching past the boundary takes away
                // only what it covers.
                double expected = new GeoFace2(face.Boundary).Subtract(face.Holes.Select(h => new GeoFace2(h)), tolerance).Sum(f => f.Area);

                if (Math.Abs(mesh.Area - expected) > 1E-6 * Math.Max(1.0, expected) + 1.0)
                {
                    return $"area {mesh.Area:R} of {expected:R}";
                }
            }

            // Edge to edge.
            var directed = new HashSet<(int, int)>();
            IReadOnlyList<GeoPoint2> vertices = mesh.Vertices;

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] ring = mesh.GetFaceIndices(f);

                for (int i = 0; i < ring.Length; i++)
                {
                    if (!directed.Add((ring[i], ring[(i + 1) % ring.Length])))
                    {
                        return $"edge {ring[i]}-{ring[(i + 1) % ring.Length]} run the same way twice";
                    }
                }
            }

            var boxes0 = faces.Select(f => (f.Vertices.Min(q => q.X), f.Vertices.Max(q => q.X), f.Vertices.Min(q => q.Y), f.Vertices.Max(q => q.Y))).ToArray();
            var strict = new Tolerance(1E-9, 1E-9);

            foreach ((int a, int b) in directed)
            {
                if (directed.Contains((b, a)))
                {
                    continue;
                }

                GeoPoint2 start = vertices[a];
                GeoPoint2 end = vertices[b];
                GeoVector2 along = start.GetVectorTo(end).Multiply(1.0 / start.DistanceTo(end));
                var across = P(0.5 * (start.X + end.X) + 1E-5 * along.Y, 0.5 * (start.Y + end.Y) - 1E-5 * along.X);

                for (int f = 0; f < faces.Length; f++)
                {
                    if (across.X < boxes0[f].Item1 || across.X > boxes0[f].Item2 || across.Y < boxes0[f].Item3 || across.Y > boxes0[f].Item4)
                    {
                        continue;
                    }

                    if (Containment2.Locate(faces[f], across, strict) == PointLocation.Inside)
                    {
                        return $"face {f} lies across edge {a}-{b} ({start} to {end}) without running back along it";
                    }
                }

                if (!joints && face.Locate(across, tolerance) == PointLocation.Inside)
                {
                    return $"material across edge {a}-{b} ({start} to {end}) is uncovered";
                }
            }

            // The faces' triangles cover them, counter-clockwise, every corner of the mesh a corner of one.
            GeoTriangle2[] triangles = mesh.ToTriangles();
            double triangleArea = triangles.Sum(t => t.SignedArea);

            if (Math.Abs(triangleArea - mesh.Area) > 1E-9 * Math.Max(1.0, mesh.Area) + 1E-6)
            {
                return $"triangles cover {triangleArea:R} of {mesh.Area:R}";
            }

            if (triangles.Any(t => !(t.SignedArea > 0.0)))
            {
                return "a triangle runs clockwise";
            }

            var corners = new HashSet<GeoPoint2>(triangles.SelectMany(t => new[] { t.A, t.B, t.C }));

            if (vertices.Any(v => !corners.Contains(v)))
            {
                return "a vertex is the corner of no triangle";
            }

            // Points of the material in one face, points outside in none.
            var random = new Random(11);
            double minX = vertices.Min(p => p.X), maxX = vertices.Max(p => p.X), minY = vertices.Min(p => p.Y), maxY = vertices.Max(p => p.Y);
            var boxes = faces.Select(f => (f.Vertices.Min(p => p.X), f.Vertices.Max(p => p.X), f.Vertices.Min(p => p.Y), f.Vertices.Max(p => p.Y))).ToArray();

            for (int k = 0; k < 400; k++)
            {
                var point = P(minX + random.NextDouble() * (maxX - minX), minY + random.NextDouble() * (maxY - minY));
                PointLocation where = face.Locate(point, tolerance);

                if (where == PointLocation.OnSide)
                {
                    continue;
                }

                int count = 0;
                bool onSide = false;

                for (int f = 0; f < faces.Length; f++)
                {
                    if (point.X < boxes[f].Item1 - 1 || point.X > boxes[f].Item2 + 1 || point.Y < boxes[f].Item3 - 1 || point.Y > boxes[f].Item4 + 1)
                    {
                        continue;
                    }

                    PointLocation inFace = Containment2.Locate(faces[f], point, tolerance);
                    onSide |= inFace == PointLocation.OnSide;
                    count += inFace == PointLocation.Inside ? 1 : 0;
                }

                if (onSide)
                {
                    continue;
                }

                if (where == PointLocation.Inside && (joints ? count > 1 : count != 1))
                {
                    return $"{point} of the material in {count} faces";
                }

                if (where == PointLocation.OutSide && count != 0)
                {
                    return $"{point} outside in {count} faces";
                }
            }

            return null;
        }
    }
}
