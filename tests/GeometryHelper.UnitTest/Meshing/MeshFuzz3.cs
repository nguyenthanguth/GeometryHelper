using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Random flat shapes of space and random bodies, meshed and cut every way, held to what the meshes and the cells promise.
    /// <para>
    /// A port of the scratchpad fuzz that found the faults its seeds replay: the generators are its own, unchanged, so that a
    /// seed here is the case it was there. Each case gives back null, or what it found wrong.
    /// </para>
    /// </summary>
    internal static class MeshFuzz3
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        #region Shapes

        /// <summary>A star-shaped loop about the origin: simple whatever its radii.</summary>
        internal static List<GeoPoint2> Star(Random r, int n, double rMin, double rMax, double cx, double cy)
        {
            var points = new List<GeoPoint2>(n);
            double phase = r.NextDouble() * Math.PI * 2;

            for (int i = 0; i < n; i++)
            {
                double a = phase + (i + 0.3 * r.NextDouble()) * 2 * Math.PI / n;
                double radius = rMin + r.NextDouble() * (rMax - rMin);
                points.Add(new GeoPoint2(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a)));
            }

            return points;
        }

        /// <summary>How far a star's sides stand from its middle at the nearest.</summary>
        internal static double Inradius(List<GeoPoint2> star)
        {
            double least = double.MaxValue;
            var middle = new GeoPoint2(0, 0);

            for (int i = 0; i < star.Count; i++)
            {
                least = Math.Min(least, new GeoLine2(star[i], star[(i + 1) % star.Count]).DistanceTo(middle));
            }

            return least;
        }

        /// <summary>Holes inside a star about its middle: apart from each other and well inside its sides.</summary>
        internal static List<List<GeoPoint2>> Holes(Random r, List<GeoPoint2> outer, int count)
        {
            double inradius = Inradius(outer);
            var holes = new List<List<GeoPoint2>>();

            for (int h = 0; h < count; h++)
            {
                double a = h * 2 * Math.PI / Math.Max(1, count);
                double d = count == 1 ? 0.0 : 0.45 * inradius;
                double reach = count == 1 ? 0.6 * inradius : 0.3 * inradius;
                holes.Add(Star(r, 3 + r.Next(5), 0.4 * reach, reach, d * Math.Cos(a), d * Math.Sin(a)));
            }

            return holes;
        }

        /// <summary>A random rigid placement: turned any way, and moved near or far.</summary>
        internal static GeoTransform3 Placement(Random r, bool far)
        {
            var axis = new GeoVector3(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5);

            if (axis.Length < 1E-3)
            {
                axis = GeoVector3.ZAxis;
            }

            double angle = r.NextDouble() * Math.PI * 2;
            double reach = far ? 7E6 : 5000;
            var move = new GeoVector3((r.NextDouble() - 0.5) * reach, (r.NextDouble() - 0.5) * reach, (r.NextDouble() - 0.5) * (far ? 1E5 : 5000));

            switch (r.Next(4))
            {
                case 0:
                    return GeoTransform3.Translation(move);
                case 1:
                    // Upright: turned about Z only, as walls stand.
                    return GeoTransform3.Translation(move) * GeoTransform3.RotationZ(angle);
                case 2:
                    // Standing: turned to stand up, then about Z.
                    return GeoTransform3.Translation(move) * GeoTransform3.RotationZ(angle) * GeoTransform3.RotationX(Math.PI / 2);
                default:
                    return GeoTransform3.Translation(move) * GeoTransform3.RotationAxis(axis, angle);
            }
        }

        /// <summary>A random face of space: a star with holes, laid on a random plane, its corners a hair off it now and then.</summary>
        internal static GeoFace3 RandomFace(Random r, out string what)
        {
            int n = 3 + r.Next(10);
            double size = 500 + r.NextDouble() * 5000;
            List<GeoPoint2> outer = Star(r, n, 0.55 * size, size, 0, 0);
            int holeCount = r.Next(3);
            List<List<GeoPoint2>> holes = Holes(r, outer, holeCount);

            bool far = r.Next(5) == 0;
            bool offFlat = r.Next(4) == 0;
            GeoTransform3 placed = Placement(r, far);
            GeoPoint3 Up(GeoPoint2 p) => placed.Transform(new GeoPoint3(p.X, p.Y, offFlat ? (r.NextDouble() - 0.5) * 0.008 : 0.0));
            what = $"star {n} size {size:F0} holes {holeCount} far {far} offFlat {offFlat}";

            var boundary = new GeoPolygon3(outer.Select(Up).ToList(), Tol);
            var holes3 = holes.Select(h => new GeoPolygon3(h.Select(Up).ToList(), Tol)).ToList();
            return new GeoFace3(boundary, holes3, Tol);
        }

        private static MeshOptions RandomOptions(Random r, double size, out string what)
        {
            MeshKind kind = (MeshKind)r.Next(4);
            double? angle = r.Next(3) == 0 ? r.NextDouble() * Math.PI : (double?)null;

            if (kind != MeshKind.Grid)
            {
                what = $"{kind} angle {angle}";
                return new MeshOptions(kind, angleRad: angle);
            }

            double w = size * (0.05 + r.NextDouble() * 0.6);
            double h = size * (0.05 + r.NextDouble() * 0.6);
            double joint = r.Next(3) == 0 ? r.NextDouble() * 0.05 * Math.Min(w, h) : 0.0;
            var alignU = (GridAlignment)r.Next(4);
            var alignV = (GridAlignment)r.Next(4);
            what = $"Grid {w:F1} x {h:F1} joint {joint:F2} {alignU}/{alignV} angle {angle}";
            return new MeshOptions(MeshKind.Grid, w, h, joint, angle, alignU, alignV);
        }

        private static MeshPlacement3 RandomPlacement(Random r, GeoVector3 normal, GeoPoint3 near, double size, out string what)
        {
            MeshPlacement3 placement;

            switch (r.Next(5))
            {
                case 0:
                    placement = MeshPlacement3.Own;
                    break;
                case 1:
                    placement = MeshPlacement3.Upright;
                    break;
                case 2:
                    // A direction that does not stand square to the face.
                    GeoVector3 d;

                    do
                    {
                        d = new GeoVector3(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5);
                    }
                    while (d.Length < 0.1 || Math.Abs(d.Normalize().DotProduct(normal)) > 0.95);

                    placement = MeshPlacement3.Along(d);
                    break;
                case 3:
                    var x = new GeoVector3(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5);
                    var y = new GeoVector3(r.NextDouble() - 0.5, r.NextDouble() - 0.5, r.NextDouble() - 0.5);

                    if (x.CrossProduct(y).Length < 0.05)
                    {
                        x = GeoVector3.XAxis;
                        y = GeoVector3.YAxis;
                    }

                    placement = MeshPlacement3.Frame(new GeoCoordinateSystem3(near, x, y));
                    break;
                default:
                    placement = MeshPlacement3.World;
                    break;
            }

            if (r.Next(3) == 0)
            {
                bool far = r.Next(3) == 0;
                placement = placement.At(far ? new GeoPoint3(0, 0, 0) : near.Add(new GeoVector3((r.NextDouble() - 0.5) * size, (r.NextDouble() - 0.5) * size, (r.NextDouble() - 0.5) * size)));
            }

            what = placement.ToString();
            return placement;
        }

        #endregion

        #region Planar

        public static string PlanarOne(int caseSeed, out int made)
        {
            made = 0;
            var r = new Random(caseSeed);
            string shapeWhat, optionsWhat, placementWhat;
            GeoFace3 face;

            try
            {
                face = RandomFace(r, out shapeWhat);
            }
            catch (ArgumentException)
            {
                return null; // the random corners came too close to make a face
            }

            double size = Math.Sqrt(face.Area);
            MeshOptions options = RandomOptions(r, size, out optionsWhat);
            MeshPlacement3 placement = RandomPlacement(r, face.Normal, face.Boundary[0], size, out placementWhat);
            string where = $"[{shapeWhat}; {optionsWhat}; {placementWhat}]";


            GeoMesh3 mesh;

            try
            {
                mesh = face.ToMesh(options, placement, Tol);
            }
            catch (ArgumentException e) when (e.Message.Contains("more than the") || e.Message.Contains("no larger than"))
            {
                return null;
            }
            catch (Exception e)
            {
                return $"threw {e.GetType().Name}: {e.Message} {where}";
            }

            made = mesh.FaceCount;
            return CheckPlanar(face, options, placement, mesh, where);
        }

        private static string CheckPlanar(GeoFace3 face, MeshOptions options, MeshPlacement3 placement, GeoMesh3 mesh, string where)
        {
            GeoCoordinateSystem3 frame = mesh.Frame;

            if (!frame.IsValid)
            {
                return "frame invalid " + where;
            }

            if (frame.ZAxis.DotProduct(face.Normal) < 1 - 1E-9)
            {
                return $"normal {frame.ZAxis} is not the face's {face.Normal} {where}";
            }

            GeoMesh2 flat = mesh.ToMesh2();
            double scale = 1.0;

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                scale = Math.Max(scale, frame.ToLocal(corner).DistanceTo(new GeoPoint3(0, 0, 0)));
            }

            // Vertices: where the layout says, on the plane, in the material.
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                GeoPoint3 local = frame.ToLocal(mesh.Vertices[v]);

                if (Math.Abs(local.X - flat.Vertices[v].X) > 1E-8 * scale || Math.Abs(local.Y - flat.Vertices[v].Y) > 1E-8 * scale)
                {
                    return $"vertex {v} at {local} is laid out at {flat.Vertices[v]} {where}";
                }

                if (Math.Abs(local.Z) > 2 * Tol.EqualPlanar + 1E-8 * scale)
                {
                    return $"vertex {v} stands {local.Z} off the plane {where}";
                }

                if (face.Locate(mesh.Vertices[v], Tol) == PointLocation.OutSide)
                {
                    return $"vertex {v} {mesh.Vertices[v]} lies outside the face {where}";
                }
            }

            var vertexSet = new HashSet<GeoPoint3>(mesh.Vertices);

            bool gapped = options.Kind == MeshKind.Grid && options.Joint > Tol.EqualPoint;

            foreach (GeoPoint3 corner in face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices)))
            {
                // A corner standing in a joint is no face's.
                if (!gapped && !vertexSet.Contains(corner))
                {
                    // A corner the meshing need not keep: one a hole's or the boundary's sides leave within the tolerance of
                    // another ring, where the region is resolved as the booleans resolve it.
                    if (!NearAnotherRing(face, corner))
                    {
                        return $"the corner {corner} is not a vertex {where}";
                    }
                }
            }

            // Faces: counter-clockwise about the normal, edge to edge.
            var directed = new HashSet<(int, int)>();
            double sum = 0.0;

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] ix = mesh.GetFaceIndices(f);

                if (ix.Length < 3 || ix.Distinct().Count() != ix.Length)
                {
                    return $"face {f} has corners {string.Join(",", ix)} {where}";
                }

                for (int i = 0; i < ix.Length; i++)
                {
                    if (!directed.Add((ix[i], ix[(i + 1) % ix.Length])))
                    {
                        return $"edge {ix[i]}-{ix[(i + 1) % ix.Length]} run twice the same way {where}";
                    }
                }

                GeoPolygon3 polygon = mesh.GetFace(f);

                if (!(polygon.Area > 0))
                {
                    return $"face {f} has no area {where}";
                }

                sum += polygon.Area;
            }

            if (Math.Abs(sum - mesh.Area) > 1E-9 * Math.Max(1, sum))
            {
                return $"Area {mesh.Area} not the faces' {sum} {where}";
            }

            // The area: the face's, or with joints no more than it.
            bool jointed = options.Kind == MeshKind.Grid && options.Joint > Tol.EqualPoint;
            double expected = face.Area;

            if (!jointed && Math.Abs(mesh.Area - expected) > 1E-7 * Math.Max(1, expected) + 1E-4)
            {
                return $"the faces cover {mesh.Area:R}, the face {expected:R} {where}";
            }

            if (jointed && mesh.Area > expected * (1 + 1E-9) + 1E-6)
            {
                return $"the faces cover {mesh.Area:R}, more than the face's {expected:R} {where}";
            }

            // The triangles are the faces'.
            // Measured along the normal as the faces are: a face a hair out of flat has triangles a hair tilted.
            double triangled = mesh.ToTriangles().Sum(t => 0.5 * t.GetAreaVector().DotProduct(mesh.Normal));

            if (Math.Abs(triangled - mesh.Area) > 1E-8 * Math.Max(1, mesh.Area))
            {
                return $"the triangles cover {triangled:R}, the faces {mesh.Area:R} {where}";
            }

            // The placement: world grids run level, or along X on a face level within the angle tolerance.
            double angle = options.AngleRad ?? 0.0;

            if (placement.Axes == MeshAxes.World || placement.Axes == MeshAxes.Upright)
            {
                GeoVector3 first = frame.XAxis.Multiply(Math.Cos(-angle)).Add(frame.ZAxis.CrossProduct(frame.XAxis).Multiply(Math.Sin(-angle)));
                bool level = Math.Sqrt(Math.Max(0, 1 - face.Normal.Z * face.Normal.Z)) <= Math.Sin(Tol.EqualAngleRad);

                if (!level && Math.Abs(first.Z) > 1E-9)
                {
                    return $"a world grid's first axis {first} is not level {where}";
                }
            }

            // An anchored grid has its cells' corners on the origin's lines.
            if (options.Kind == MeshKind.Grid && placement.Origin.HasValue)
            {
                GeoPoint3 o = frame.ToLocal(placement.Origin.Value);
                double joint = options.Joint > Tol.EqualPoint ? options.Joint : 0.0;
                double pu = options.CellWidth + joint, pv = options.CellHeight + joint;

                // Measured the world's way along each axis, X before Y before Z, as the cells are laid from the origin.
                double su = MeshPlacement3.Canonical(frame.XAxis).Equals(frame.XAxis) ? 1.0 : -1.0;
                double sv = MeshPlacement3.Canonical(frame.YAxis).Equals(frame.YAxis) ? 1.0 : -1.0;

                for (int f = 0; f < mesh.FaceCount; f++)
                {
                    if (!mesh.IsWhole(f))
                    {
                        continue;
                    }

                    foreach (int v in mesh.GetFaceIndices(f))
                    {
                        GeoPoint2 p = flat.Vertices[v];
                        double du = Mod(su * (p.X - o.X), pu), dv = Mod(sv * (p.Y - o.Y), pv);
                        bool onU = du < 1E-6 * scale + Tol.EqualPoint || Math.Abs(du - options.CellWidth) < 1E-6 * scale + Tol.EqualPoint || pu - du < 1E-6 * scale + Tol.EqualPoint;
                        bool onV = dv < 1E-6 * scale + Tol.EqualPoint || Math.Abs(dv - options.CellHeight) < 1E-6 * scale + Tol.EqualPoint || pv - dv < 1E-6 * scale + Tol.EqualPoint;

                        if (!onU && !onV)
                        {
                            return $"a whole cell's corner {p} stands off the origin's lines ({du}, {dv}) {where}";
                        }
                    }
                }
            }

            // Points of the material in one face, points outside in none.
            return Covers(face, mesh, flat, jointed, where);
        }

        private static double Mod(double a, double m)
        {
            double r = a % m;
            return r < 0 ? r + m : r;
        }

        private static bool NearAnotherRing(GeoFace3 face, GeoPoint3 corner)
        {
            var rings = new List<GeoPolygon3> { face.Boundary };
            rings.AddRange(face.Holes);

            foreach (GeoPolygon3 ring in rings)
            {
                if (ring.Vertices.Contains(corner))
                {
                    continue;
                }

                if (ring.GetEdges().Any(e => e.DistanceTo(corner) <= Tol.EqualPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Covers(GeoFace3 face, GeoMesh3 mesh, GeoMesh2 flat, bool jointed, string where)
        {
            GeoPolygon2[] faces = flat.GetFaces();
            double minX = flat.Vertices.Min(p => p.X), maxX = flat.Vertices.Max(p => p.X);
            double minY = flat.Vertices.Min(p => p.Y), maxY = flat.Vertices.Max(p => p.Y);
            var random = new Random(5);

            for (int k = 0; k < 300; k++)
            {
                var p = new GeoPoint2(minX + random.NextDouble() * (maxX - minX), minY + random.NextDouble() * (maxY - minY));
                PointLocation inFace = face.Locate(mesh.Frame.ToGlobal(new GeoPoint3(p.X, p.Y, 0)), Tol);

                if (inFace == PointLocation.OnSide)
                {
                    continue;
                }

                int count = 0;
                bool onSide = false;

                foreach (GeoPolygon2 f in faces)
                {
                    PointLocation l = Containment2.Locate(f, p, Tol);
                    onSide |= l == PointLocation.OnSide;
                    count += l == PointLocation.Inside ? 1 : 0;
                }

                if (onSide)
                {
                    continue;
                }

                if (inFace == PointLocation.Inside && (jointed ? count > 1 : count != 1))
                {
                    double nearest = faces.Min(f => f.DistanceTo(p));
                    GeoFace2 laid = face.ProjectToFace2(mesh.Frame);
                    double toRing = Math.Min(laid.Boundary.ToPolyline().DistanceTo(p), laid.Holes.Count > 0 ? laid.Holes.Min(h => h.ToPolyline().DistanceTo(p)) : double.MaxValue);
                    string inLaid = laid.Locate(p, Tol).ToString();
                    string inHoles = string.Join(",", laid.Holes.Select(h => h.Locate(p, Tol)));
                    return $"a point {p} of the face lies in {count} faces, {nearest:E3} from the nearest, {toRing:E3} from a ring, laid {inLaid} holes {inHoles}, area {mesh.Area:R} vs {face.Area:R} {where}";
                }

                if (inFace == PointLocation.OutSide && count != 0)
                {
                    return $"a point outside the face lies in {count} faces {where}";
                }
            }

            return null;
        }

        #endregion

        #region Cells

        /// <summary>A random body: a prism of a star with holes, a box with a box taken out or added, a cylinder, a body with an opening, or an L with a step near a line.</summary>
        internal static GeoSolid3 RandomBody(Random r, out string what, out double scale)
        {
            int shape = r.Next(7);
            bool far = r.Next(5) == 0;
            GeoTransform3 placed = Placement(r, far);
            double size = 500 + r.NextDouble() * 4000;
            scale = size;

            switch (shape)
            {
                case 0:
                case 1:
                {
                    int n = 3 + r.Next(9);
                    List<GeoPoint2> outer = Star(r, n, 0.55 * size, size, 0, 0);
                    var holes = new List<GeoPolygon3>();
                    int holeCount = shape == 1 ? 1 + r.Next(2) : 0;
                    double height = 100 + r.NextDouble() * size;
                    var profileOuter = new GeoPolygon3(outer.Select(p => new GeoPoint3(p.X, p.Y, 0)).ToList(), Tol);

                    foreach (List<GeoPoint2> hole in Holes(r, outer, holeCount))
                    {
                        holes.Add(new GeoPolygon3(hole.Select(p => new GeoPoint3(p.X, p.Y, 0)).ToList(), Tol));
                    }

                    var profile = new GeoFace3(profileOuter, holes, Tol);
                    what = $"prism star {n} holes {holeCount} size {size:F0} height {height:F0} far {far}";
                    return GeoSolid3.Extrude(profile, new GeoVector3(0, 0, height), Tol).TransformBy(placed);
                }

                case 2:
                {
                    GeoSolid3 a = new GeoObb3(new GeoPoint3(0, 0, 0), size, 0.7 * size, 0.5 * size).ToSolid();
                    GeoSolid3 b = new GeoObb3(new GeoPoint3(0.4 * size * (r.NextDouble() - 0.5), 0.3 * size, 0.2 * size), 0.5 * size, 0.6 * size, 0.6 * size, new GeoVector3(1, r.NextDouble() - 0.5, 0), new GeoVector3(0, 1, 0)).ToSolid();
                    bool subtract = r.Next(2) == 0;
                    GeoSolid3 result;
                    bool ok = subtract ? a.TrySubtract(b, out result, Tol) : a.TryUnion(b, out result, Tol);
                    what = $"boolean {(subtract ? "subtract" : "union")} size {size:F0} far {far}";
                    return ok ? result.TransformBy(placed) : a.TransformBy(placed);
                }

                case 3:
                {
                    int segments = 8 + r.Next(40);
                    what = $"cylinder r {0.5 * size:F0} segments {segments} far {far}";
                    return GeoSolid3.Cylinder(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, size), 0.5 * size, segments, Tol).TransformBy(placed);
                }

                case 4:
                {
                    GeoSolid3 slab = new GeoObb3(new GeoPoint3(0, 0, 0), size, 0.8 * size, 0.1 * size + 100).ToSolid();
                    GeoSolid3 shaft = new GeoObb3(new GeoPoint3(0.3 * size * (r.NextDouble() - 0.5), 0.3 * size * (r.NextDouble() - 0.5), 0), 0.2 * size, 0.15 * size, size).ToSolid();
                    what = $"slab with opening size {size:F0} far {far}";
                    return slab.WithOpenings(new[] { shaft }).TransformBy(placed);
                }

                case 5:
                {
                    // An L whose step stands a hair to either side of where a line of cells is likely to fall.
                    double step = Math.Round(0.5 * size / 100) * 100 + (r.NextDouble() - 0.5) * 0.04;
                    var l = new List<GeoPoint3> { new GeoPoint3(0, 0, 0), new GeoPoint3(size, 0, 0), new GeoPoint3(size, 0.5 * size, 0), new GeoPoint3(step, 0.5 * size, 0), new GeoPoint3(step, size, 0), new GeoPoint3(0, size, 0) };
                    what = $"L step {step:F4} size {size:F0} far {far}";
                    return GeoSolid3.Extrude(new GeoPolygon3(l, Tol), new GeoVector3(0, 0, 0.6 * size), Tol).TransformBy(placed);
                }

                default:
                {
                    // A U whose notch splits cells in two.
                    double w = size;
                    var u = new List<GeoPoint3> { new GeoPoint3(0, 0, 0), new GeoPoint3(w, 0, 0), new GeoPoint3(w, w, 0), new GeoPoint3(0.7 * w, w, 0), new GeoPoint3(0.7 * w, 0.35 * w, 0), new GeoPoint3(0.3 * w, 0.35 * w, 0), new GeoPoint3(0.3 * w, w, 0), new GeoPoint3(0, w, 0) };
                    what = $"U size {size:F0} far {far}";
                    return GeoSolid3.Extrude(new GeoPolygon3(u, Tol), new GeoVector3(0, 0, 0.4 * w), Tol).TransformBy(placed);
                }
            }
        }

        private static CellOptions3 RandomCellOptions(Random r, double size, out string what)
        {
            CellAxis Axis()
            {
                switch (r.Next(6))
                {
                    case 0:
                        return CellAxis.Whole;
                    case 1:
                        return CellAxis.ByCount(1 + r.Next(5));
                    default:
                        return CellAxis.BySize(size * (0.15 + r.NextDouble() * 0.7), (GridAlignment)r.Next(4));
                }
            }

            CellAxis x = Axis(), y = Axis(), z = Axis();
            double joint = r.Next(4) == 0 ? r.NextDouble() * 0.02 * size : 0.0;
            double snap = r.Next(4) == 0 ? r.NextDouble() * 0.02 * size : 0.0;
            what = $"X {x} Y {y} Z {z} joint {joint:F2} snap {snap:F2}";
            return new CellOptions3(x, y, z, joint, snap);
        }

        public static string CellsOne(int caseSeed, out int made)
        {
            made = 0;
            var r = new Random(caseSeed);
            GeoSolid3 body;
            string bodyWhat;
            double size;

            try
            {
                body = RandomBody(r, out bodyWhat, out size);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!body.IsClosed(Tol))
            {
                return null;
            }

            CellOptions3 options = RandomCellOptions(r, size, out string optionsWhat);
            GeoPoint3 near = body.Faces[0].Boundary[0];
            MeshPlacement3 placement = RandomPlacement(r, GeoVector3.ZAxis, near, size, out string placementWhat);
            string where = $"[{bodyWhat}; {optionsWhat}; {placementWhat}]";


            GeoCellGrid3 grid;

            try
            {
                grid = body.ToCells(options, placement, Tol);
            }
            catch (ArgumentException e) when (e.Message.Contains("more than the") || e.Message.Contains("no larger than"))
            {
                return null;
            }
            catch (Exception e)
            {
                return $"threw {e.GetType().Name}: {e.Message} {where}";
            }

            made = grid.CellCount;
            return CheckCells(body, options, grid, where);
        }

        private static string CheckCells(GeoSolid3 body, CellOptions3 options, GeoCellGrid3 grid, string where)
        {
            double net = body.Openings.Count > 0 ? body.GetNetVolume(Tol) : body.Volume;
            bool jointed = options.Joint > Tol.EqualPoint;
            double snap = jointed ? Tol.EqualPoint : Math.Max(options.SnapDistance, Tol.EqualPoint);

            // A face within the tolerance of a cut is taken as lying in it, and the wedge between goes with it: the cells
            // hold the body's volume but for the tolerance times the area cut.
            double cutArea = grid.Cells.Sum(c => c.Solid.SurfaceArea) - body.SurfaceArea;
            double slack = Tol.EqualPoint * Math.Max(0, cutArea) + 1E-9 * Math.Max(1, net);

            if (!jointed && Math.Abs(grid.Volume - net) > slack)
            {
                return $"the cells hold {grid.Volume:R}, the body {net:R} ({(grid.Volume - net) / net:E2}, slack {slack:E2}) {where}";
            }

            if (jointed && grid.Volume > net * (1 + 1E-7))
            {
                return $"the cells hold {grid.Volume:R}, more than the body's {net:R} {where}";
            }

            GeoCell3 previous = null;

            foreach (GeoCell3 cell in grid.Cells)
            {
                if (previous != null)
                {
                    int order = cell.K != previous.K ? cell.K.CompareTo(previous.K) : cell.J != previous.J ? cell.J.CompareTo(previous.J) : cell.I != previous.I ? cell.I.CompareTo(previous.I) : cell.Piece.CompareTo(previous.Piece);

                    if (order <= 0)
                    {
                        return $"{cell} comes after {previous} {where}";
                    }
                }

                previous = cell;

                if (!(cell.Volume > 0))
                {
                    return $"{cell} holds nothing {where}";
                }

                if (!cell.Solid.IsClosed(Tol))
                {
                    return $"{cell} is not closed {where}";
                }

                if (Math.Abs(cell.Solid.Volume - cell.Volume) > 1E-9 * Math.Max(1, cell.Volume))
                {
                    return $"{cell} says {cell.Volume}, its body {cell.Solid.Volume} {where}";
                }

                GeoObb3 box = cell.Box;

                // A cut moved onto a corner by the snap distance, then not made short of a tip past it: four point tolerances,
                // or between cells a joint apart the point tolerance.
                double tip = (jointed ? 1.0 : 4.0) * Tol.EqualPoint;
                double reach = snap + tip + 3 * Tol.EqualPoint + 1E-9 * (box.Center.ToVector().Length + box.SizeX + box.SizeY + box.SizeZ);

                // The point of a needle a cut could not take off stays with the cell: no more than a couple of corners, each
                // counted once however its faces round it, and no further out than a hundred point tolerances.
                var outside = new List<GeoPoint3>();

                foreach (GeoFace3 face in cell.Solid.Faces)
                {
                    foreach (GeoPoint3 corner in face.Boundary.Vertices)
                    {
                        GeoPoint3 local = box.CoordinateSystem.ToLocal(corner);
                        double beyond = Math.Max(Math.Abs(local.X) - box.ExtentX, Math.Max(Math.Abs(local.Y) - box.ExtentY, Math.Abs(local.Z) - box.ExtentZ));

                        if (beyond > reach)
                        {
                            if (!outside.Any(o => o.IsEqualTo(corner, Tol)))
                            {
                                outside.Add(corner);
                            }

                            if (beyond > 100 * Tol.EqualPoint + reach || outside.Count > 2)
                            {
                                return $"{cell} reaches out of its cell to {local} (extents {box.ExtentX:F3} {box.ExtentY:F3} {box.ExtentZ:F3}) {where}";
                            }
                        }
                    }
                }

                if (cell.IsWhole && Math.Abs(cell.Volume - box.Volume) > Tol.EqualPoint * box.SurfaceArea)
                {
                    return $"{cell} whole holds {cell.Volume} of {box.Volume} {where}";
                }
            }

            return CoversCells(body, grid, jointed, where) ?? Neighbours(grid, jointed, where);
        }

        /// <summary>
        /// The neighbours: none with joints; otherwise each other's, never a cell's own, cut apart by one plane, and every
        /// pair of a small grid sharing a clear area of a plane between them among them.
        /// </summary>
        private static string Neighbours(GeoCellGrid3 grid, bool jointed, string where)
        {
            var adjacent = new List<HashSet<int>>(grid.CellCount);

            for (int n = 0; n < grid.CellCount; n++)
            {
                adjacent.Add(new HashSet<int>(grid.GetAdjacentCells(n)));
            }

            for (int n = 0; n < grid.CellCount; n++)
            {
                if (jointed && adjacent[n].Count > 0)
                {
                    return $"{grid.Cells[n]} has neighbours across a joint {where}";
                }

                foreach (int m in adjacent[n])
                {
                    if (m == n || !adjacent[m].Contains(n))
                    {
                        return $"{grid.Cells[n]} and {grid.Cells[m]} are not each other's neighbours {where}";
                    }

                    if (CutPlane(grid.Cells[n], grid.Cells[m]) < 0)
                    {
                        return $"{grid.Cells[n]} and {grid.Cells[m]} are neighbours no plane cut apart {where}";
                    }
                }
            }

            if (jointed || grid.CellCount > 60)
            {
                return null;
            }

            double clear = 100 * Tol.EqualPoint * Tol.EqualPoint;

            for (int n = 0; n < grid.CellCount; n++)
            {
                for (int m = n + 1; m < grid.CellCount; m++)
                {
                    if (!adjacent[n].Contains(m) && CutPlane(grid.Cells[n], grid.Cells[m]) >= 0)
                    {
                        double shared = SharedArea(grid, grid.Cells[n], grid.Cells[m]);

                        if (shared > clear)
                        {
                            return $"{grid.Cells[n]} and {grid.Cells[m]} share {shared:R} of a plane and are not neighbours {where}";
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>The axis along which one cut bounds two cells from opposite sides; -1 for none.</summary>
        private static int CutPlane(GeoCell3 a, GeoCell3 b)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                if ((!double.IsNaN(a.High[axis]) && Math.Abs(a.High[axis] - b.Low[axis]) <= Tol.EqualPoint)
                    || (!double.IsNaN(b.High[axis]) && Math.Abs(b.High[axis] - a.Low[axis]) <= Tol.EqualPoint))
                {
                    return axis;
                }
            }

            return -1;
        }

        /// <summary>The area two cells' faces share on the planes square to the grid's axes, worked out by Boolean2.</summary>
        private static double SharedArea(GeoCellGrid3 grid, GeoCell3 a, GeoCell3 b)
        {
            GeoVector3[] directions = { grid.Frame.XAxis, grid.Frame.YAxis, grid.Frame.ZAxis };
            double total = 0.0;

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                double At(GeoPoint3 p) => grid.Frame.Origin.GetVectorTo(p).DotProduct(directions[axis]);

                GeoFace2 Lay(GeoFace3 face)
                {
                    GeoPolygon2 Ring(IReadOnlyList<GeoPoint3> ring)
                    {
                        GeoPoint2[] flat = ring.Select(p => { GeoVector3 o = grid.Frame.Origin.GetVectorTo(p); return new GeoPoint2(o.DotProduct(directions[u]), o.DotProduct(directions[v])); }).ToArray();
                        GeoPolygon2 polygon = MeshLift3.LayOutPolygon(flat);

                        if (polygon == null)
                        {
                            return null;
                        }

                        GeoPoint2[] corners = Enumerable.Range(0, polygon.VertexCount).Select(i => polygon[i]).ToArray();

                        if (polygon.SignedArea < 0)
                        {
                            Array.Reverse(corners);
                        }

                        return new GeoPolygon2(corners, corners.Length);
                    }

                    GeoPolygon2 boundary = Ring(face.Boundary.Vertices);
                    return boundary == null ? null : new GeoFace2(boundary, face.Holes.Select(h => Ring(h.Vertices)).Where(h => h != null));
                }

                foreach (GeoFace3 fa in a.Solid.Faces)
                {
                    double da = fa.Normal.DotProduct(directions[axis]);

                    if (Math.Abs(da) < 1 - 1E-6)
                    {
                        continue;
                    }

                    double at = At(fa.Boundary[0]);

                    if (fa.Boundary.Vertices.Any(p => Math.Abs(At(p) - at) > Tol.EqualPoint))
                    {
                        continue;
                    }

                    foreach (GeoFace3 fb in b.Solid.Faces)
                    {
                        if (fb.Normal.DotProduct(directions[axis]) * da > -(1 - 1E-6) || fb.Boundary.Vertices.Any(p => Math.Abs(At(p) - at) > Tol.EqualPoint))
                        {
                            continue;
                        }

                        GeoFace2 la = Lay(fa), lb = Lay(fb);

                        if (la != null && lb != null)
                        {
                            total += Boolean2.Intersect(la, lb, Tol).Sum(f => f.Area);
                        }
                    }
                }
            }

            return total;
        }

        private static string CoversCells(GeoSolid3 body, GeoCellGrid3 grid, bool jointed, string where)
        {
            if (grid.CellCount == 0)
            {
                return body.Openings.Count > 0 ? null : "no cells " + where;
            }

            GeoAabb3 bounds = GeoAabb3.Empty;

            foreach (GeoFace3 face in body.Faces)
            {
                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    bounds = bounds.Union(grid.Frame.ToLocal(corner));
                }
            }

            GeoSolid3[] solids = grid.GetSolids();
            GeoAabb3[] boxes = solids.Select(s => s.GetAabb()).ToArray();
            var random = new Random(3);

            // Where each line of cells runs along each axis, from the frame's origin; a point this far inside one of a cell's
            // box along every axis is in no joint.
            var lines = new[] { Lines(grid, 0), Lines(grid, 1), Lines(grid, 2) };
            double inside = 6 * Tol.EqualPoint;

            for (int k = 0; k < 250; k++)
            {
                GeoPoint3 p = grid.Frame.ToGlobal(new GeoPoint3(
                    bounds.Min.X + random.NextDouble() * bounds.SizeX,
                    bounds.Min.Y + random.NextDouble() * bounds.SizeY,
                    bounds.Min.Z + random.NextDouble() * bounds.SizeZ));
                PointLocation inBody = body.Locate(p, Tol);

                if (inBody == PointLocation.OnSide)
                {
                    continue;
                }

                int count = 0;
                bool onSide = false;

                for (int c = 0; c < solids.Length; c++)
                {
                    if (!boxes[c].Contains(p, Tol))
                    {
                        continue;
                    }

                    PointLocation l = solids[c].Locate(p, Tol);
                    onSide |= l == PointLocation.OnSide;
                    count += l == PointLocation.Inside ? 1 : 0;
                }

                if (onSide)
                {
                    continue;
                }

                GeoPoint3 local = grid.Frame.ToLocal(p);
                bool inACell = jointed && InLine(lines[0], local.X, inside) && InLine(lines[1], local.Y, inside) && InLine(lines[2], local.Z, inside);

                if (inBody == PointLocation.Inside && (jointed && !inACell ? count > 1 : count != 1))
                {
                    return $"a point {p} of the body lies in {count} cells{(inACell ? ", in a cell's box" : string.Empty)} {where}";
                }

                if (inBody == PointLocation.OutSide && count != 0)
                {
                    return $"a point {p} outside the body lies in {count} cells {where}";
                }
            }

            return null;
        }

        /// <summary>Where each line of cells along an axis starts and ends, from the grid's frame's origin.</summary>
        private static (double Start, double End)[] Lines(GeoCellGrid3 grid, int axis)
        {
            int count = axis == 0 ? grid.CountX : axis == 1 ? grid.CountY : grid.CountZ;
            var lines = new (double, double)[count];

            for (int i = 0; i < count; i++)
            {
                GeoObb3 box = grid.GetBox(axis == 0 ? i : 0, axis == 1 ? i : 0, axis == 2 ? i : 0);
                GeoPoint3 centre = grid.Frame.ToLocal(box.Center);
                double middle = axis == 0 ? centre.X : axis == 1 ? centre.Y : centre.Z;
                double half = 0.5 * (axis == 0 ? box.SizeX : axis == 1 ? box.SizeY : box.SizeZ);
                lines[i] = (middle - half, middle + half);
            }

            return lines;
        }

        private static bool InLine((double Start, double End)[] lines, double at, double inside)
            => lines.Any(l => at > l.Start + inside && at < l.End - inside);

        #endregion
    }
}
