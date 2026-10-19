using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Turns the items of a scene into the primitives the viewer draws. The outline of every shape is read from the numbers in
    /// the item's data, so a shape the library refuses to build, a polygon of two points or a face off its plane, is still
    /// drawn; only the surfaces of faces, bodies and meshes ask the library, which knows how to cut a face with holes into
    /// triangles.
    /// </summary>
    internal static class PrimitiveBuilder
    {
        /// <summary>
        /// How far a ray or a plane reaches in a scene with nothing else in it, in millimetres.
        /// </summary>
        private const double EmptySceneExtent = 1000.0;

        internal static Primitives Build(Scene scene, int maxPoints)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            var result = new Primitives();

            using (Tolerance.Use(scene.Tolerance))
            {
                Bounds3 bounds = default(Bounds3);
                var items = new ItemPrimitives[scene.Items.Count];
                var curved = new List<int>();

                // What is not a curve is drawn as it is; the curves wait to be cut, since how finely depends on how many points the rest takes.
                for (int i = 0; i < scene.Items.Count; i++)
                {
                    if (IsCurved(scene.Items[i].Kind))
                    {
                        curved.Add(i);
                    }
                    else
                    {
                        items[i] = BuildItem(scene, scene.Items[i], i, 0.0, null);
                    }
                }

                ChordRule curveRule = ChooseRule(scene, items, curved, maxPoints, result.Warnings);

                foreach (int i in curved)
                {
                    items[i] = BuildItem(scene, scene.Items[i], i, 0.0, new ChordRule(curveRule.ChordTolerance, curveRule.AngleOnly));
                }

                foreach (ItemPrimitives item in items)
                {
                    result.Items.Add(item);
                    bounds = bounds.Include(item.Bounds);
                }

                // Rays and planes have no end; they are drawn out to a length that takes in what else is in the scene.
                double extent = bounds.IsEmpty ? EmptySceneExtent : Math.Max(bounds.Size.Length * 2.0, 1.0);

                for (int i = 0; i < scene.Items.Count; i++)
                {
                    if (IsUnbounded(scene.Items[i].Kind))
                    {
                        result.Items[i] = BuildItem(scene, scene.Items[i], i, extent, null);
                    }
                }

                result.Bounds = bounds;

                foreach (ItemPrimitives item in result.Items)
                {
                    string name = scene.Items[item.ItemIndex].Name ?? scene.Items[item.ItemIndex].Kind;

                    if (item.Warning != null)
                    {
                        result.Warnings.Add(name + ": " + item.Warning);
                    }

                    foreach (string line in item.LibraryLog)
                    {
                        result.Warnings.Add(name + ": the library logged: " + line);
                    }
                }
            }

            return result;
        }

        private static int PointsOf(ItemPrimitives p)
        {
            int points = p.Points.Count;

            foreach (DrawPolyline line in p.Polylines)
            {
                points += line.Points.Length;
            }

            return points;
        }

        private static bool IsCurved(string kind)
        {
            switch (kind)
            {
                case "GeoArc2":
                case "GeoArc3":
                case "GeoCircle2":
                case "GeoCircle3":
                case "GeoEllipse2":
                case "GeoEllipse3":
                case "GeoPolylineArc2":
                case "GeoPolygonArc2":
                case "GeoPolylineArc3":
                case "GeoPolygonArc3":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// What the curves of a scene take in points, apart from the pieces they are cut in: a number of points that do not change, and each turn
        /// of a curve with the radius it is cut at.
        /// </summary>
        private sealed class CurveCount
        {
            internal long Fixed;
            internal readonly List<KeyValuePair<double, double>> Turns = new List<KeyValuePair<double, double>>();

            internal long At(ChordRule rule)
            {
                long points = Fixed;

                foreach (KeyValuePair<double, double> turn in Turns)
                {
                    points += rule.Pieces(turn.Key, turn.Value);
                }

                return points;
            }
        }

        /// <summary>
        /// Reads what an item that is a curve will take in points, without cutting it: an arc takes its pieces and one more, a circle or an
        /// ellipse its pieces, a chain its vertices and the pieces of each arc less one.
        /// </summary>
        private static void Count(SceneItem item, CurveCount count)
        {
            try
            {
                JsonElement d = item.Data;

                switch (item.Kind)
                {
                    case "GeoArc2":
                    case "GeoArc3":
                        count.Fixed += 1;
                        count.Turns.Add(new KeyValuePair<double, double>(DataJson.Number(d, "sweep"), DataJson.Number(d, "radius")));
                        break;

                    case "GeoCircle2":
                    case "GeoCircle3":
                        count.Turns.Add(new KeyValuePair<double, double>(2.0 * Math.PI, DataJson.Number(d, "radius")));
                        break;

                    case "GeoEllipse2":
                    case "GeoEllipse3":
                        count.Turns.Add(new KeyValuePair<double, double>(2.0 * Math.PI, Math.Max(Math.Abs(DataJson.Number(d, "majorRadius")), Math.Abs(DataJson.Number(d, "minorRadius")))));
                        break;

                    default:
                    {
                        bool space = item.Kind.EndsWith("3", StringComparison.Ordinal);
                        bool closed = item.Kind.StartsWith("GeoPolygon", StringComparison.Ordinal);
                        Vec3[] points = DataJson.Points(d, "points", space ? 3 : 2);
                        double[] bulges = DataJson.Bulges(d, points.Length);
                        IReadOnlyList<Vec3> normals = space ? Normals(d) : null;
                        count.Fixed += points.Length;
                        int edges = closed ? points.Length : points.Length - 1;

                        for (int k = 0; k < edges; k++)
                        {
                            double bulge = bulges[k];
                            double chord = (points[(k + 1) % points.Length] - points[k]).Length;
                            bool normal = !space || (k < normals.Count && normals[k].Length > 0.0);

                            if (bulge != 0.0 && !double.IsNaN(bulge) && !double.IsInfinity(bulge) && chord > 0.0 && !double.IsInfinity(chord) && normal)
                            {
                                count.Fixed -= 1;
                                count.Turns.Add(new KeyValuePair<double, double>(4.0 * Math.Atan(bulge), chord * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge))));
                            }
                        }

                        break;
                    }
                }
            }
            catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is KeyNotFoundException)
            {
                // The numbers are wrong: the drawing of the item says so, and it takes no points here.
            }
        }

        /// <summary>
        /// Chooses how finely the curves of a scene are cut: at the chord tolerance of the scene where the lines then take no more points than the
        /// cap, and otherwise at one coarser tolerance for all of them, the finest at which they do, found by counting the pieces and not by cutting
        /// the curves; failing any, by the angle alone, 2 degrees a piece. Where it is not the chord tolerance of the scene it says so, once.
        /// </summary>
        private static ChordRule ChooseRule(Scene scene, ItemPrimitives[] items, List<int> curved, int maxPoints, List<string> warnings)
        {
            double chord = ChordOf(scene);
            var rule = new ChordRule(chord);
            long others = 0;

            foreach (ItemPrimitives p in items)
            {
                if (p != null)
                {
                    others += PointsOf(p);
                }
            }

            var count = new CurveCount();

            foreach (int i in curved)
            {
                Count(scene.Items[i], count);
            }

            long before = others + count.At(rule);

            if (before <= maxPoints)
            {
                return rule;
            }

            long budget = maxPoints - others;
            ChordRule chosen;

            if (count.At(new ChordRule(chord, true)) > budget)
            {
                chosen = new ChordRule(chord, true);
            }
            else
            {
                // The finest chord tolerance that fits lies between the one of the scene, which does not, and the first of its doublings that does.
                double low = chord;
                double high = chord * 2.0;

                while (count.At(new ChordRule(high)) > budget)
                {
                    low = high;
                    high *= 2.0;
                }

                for (int step = 0; step < 40; step++)
                {
                    double middle = Math.Sqrt(low * high);

                    if (count.At(new ChordRule(middle)) > budget)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                chosen = new ChordRule(high);
            }

            long after = others + count.At(chosen);
            var g3 = System.Globalization.CultureInfo.InvariantCulture;

            warnings.Add(
                "the lines of the scene would take " + before + " points at the chord tolerance of " + chord.ToString("G3", g3) + ", over the cap of " + maxPoints
                + ": its " + curved.Count + " curve(s) are cut "
                + (chosen.AngleOnly ? "at 2 degrees a piece, whatever the chord tolerance" : "at a chord tolerance of " + chosen.ChordTolerance.ToString("G3", g3) + " (2 degrees a piece at most)")
                + " instead, which leaves " + after + " points" + (after > maxPoints ? ", still over the cap, for the rest is not curves" : string.Empty));
            return chosen;
        }
        private static bool IsUnbounded(string kind) => kind == "GeoRay2" || kind == "GeoRay3" || kind == "GeoPlane3";

        /// <summary>
        /// Gets how far a chord may lie from its curve: a tenth of the point tolerance of the scene, so that the drawing never
        /// differs from the curve by more than a tenth of what the library takes as one place.
        /// </summary>
        private static double ChordOf(Scene scene)
            => scene.EqualPoint > 0.0 && !double.IsInfinity(scene.EqualPoint) ? scene.EqualPoint / 10.0 : Scene.DefaultEqualPoint / 10.0;

        private static ItemPrimitives BuildItem(Scene scene, SceneItem item, int index, double extent, ChordRule rule)
        {
            using (LibraryLog log = Viewer.LibraryLog.Begin())
            {
                ItemPrimitives built = BuildItemCore(scene, item, index, extent, rule);
                built.LibraryLog.AddRange(log.Lines.Distinct(StringComparer.Ordinal));
                return built;
            }
        }

        private static ItemPrimitives BuildItemCore(Scene scene, SceneItem item, int index, double extent, ChordRule rule)
        {
            var p = new ItemPrimitives { ItemIndex = index };
            rule = rule ?? new ChordRule(ChordOf(scene));
            Bounds3? fixedBounds = null;

            try
            {
                JsonElement d = item.Data;

                switch (item.Kind)
                {
                    case "GeoPoint2": p.Points.Add(DataJson.Point(d, 2)); break;
                    case "GeoPoint3": p.Points.Add(DataJson.Point(d, 3)); break;
                    case "GeoLine2": Line(p, false, DataJson.Point(d, "a", 2), DataJson.Point(d, "b", 2)); break;
                    case "GeoLine3": Line(p, false, DataJson.Point(d, "a", 3), DataJson.Point(d, "b", 3)); break;
                    case "GeoRay2": fixedBounds = Ray(p, DataJson.Point(d, "a", 2), DataJson.Point(d, "b", 2), extent); break;
                    case "GeoRay3": fixedBounds = Ray(p, DataJson.Point(d, "a", 3), DataJson.Point(d, "b", 3), extent); break;
                    case "GeoPolyline2": Line(p, false, DataJson.Points(d, "points", 2)); break;
                    case "GeoPolygon2": Line(p, true, DataJson.Points(d, "points", 2)); break;
                    case "GeoPolyline3": Line(p, false, DataJson.Points(d, "points", 3)); break;
                    case "GeoPolygon3": Line(p, true, DataJson.Points(d, "points", 3)); break;

                    case "GeoPolylineArc2":
                    {
                        Vec3[] points = DataJson.Points(d, "points", 2);
                        Line(p, false, ArcTessellation.Chain2(rule, points, DataJson.Bulges(d, points.Length), false));
                        break;
                    }

                    case "GeoPolygonArc2":
                    {
                        Vec3[] points = DataJson.Points(d, "points", 2);
                        Line(p, true, ArcTessellation.Chain2(rule, points, DataJson.Bulges(d, points.Length), true));
                        break;
                    }

                    case "GeoPolylineArc3":
                    {
                        Vec3[] points = DataJson.Points(d, "points", 3);
                        Line(p, false, ArcTessellation.Chain3(rule, points, DataJson.Bulges(d, points.Length), Normals(d), false));
                        break;
                    }

                    case "GeoPolygonArc3":
                    {
                        Vec3[] points = DataJson.Points(d, "points", 3);
                        Line(p, true, ArcTessellation.Chain3(rule, points, DataJson.Bulges(d, points.Length), Normals(d), true));
                        break;
                    }

                    case "GeoArc2":
                        Line(p, false, ArcTessellation.Arc2(rule, DataJson.Point(d, "center", 2), DataJson.Number(d, "radius"), DataJson.Number(d, "start"), DataJson.Number(d, "sweep")));
                        break;

                    case "GeoArc3":
                        Line(p, false, ArcTessellation.Arc3(rule, DataJson.Point(d, "center", 3), DataJson.Point(d, "normal", 3), DataJson.Number(d, "radius"), DataJson.Number(d, "start"), DataJson.Number(d, "sweep")));
                        break;

                    case "GeoCircle2":
                        Line(p, true, ArcTessellation.Circle2(rule, DataJson.Point(d, "center", 2), DataJson.Number(d, "radius")));
                        break;

                    case "GeoCircle3":
                        Line(p, true, ArcTessellation.Circle3(rule, DataJson.Point(d, "center", 3), DataJson.Point(d, "normal", 3), DataJson.Number(d, "radius")));
                        break;

                    case "GeoEllipse2":
                    {
                        Vec3 major = DataJson.Point(d, "majorAxis", 2).Normalized();
                        Vec3 minor = new Vec3(-major.Y, major.X, 0.0);
                        Line(p, true, ArcTessellation.Ellipse(rule, DataJson.Point(d, "center", 2), major, minor, DataJson.Number(d, "majorRadius"), DataJson.Number(d, "minorRadius")));
                        break;
                    }

                    case "GeoEllipse3":
                    {
                        Vec3 major = DataJson.Point(d, "majorAxis", 3).Normalized();
                        Vec3 minor = Vec3.Cross(DataJson.Point(d, "normal", 3).Normalized(), major);
                        Line(p, true, ArcTessellation.Ellipse(rule, DataJson.Point(d, "center", 3), major, minor, DataJson.Number(d, "majorRadius"), DataJson.Number(d, "minorRadius")));
                        break;
                    }

                    case "GeoRectangle2":
                        Line(p, true, RectangleCorners(d));
                        break;

                    case "GeoTriangle2":
                    case "GeoTriangle3":
                    {
                        Vec3[] corners = DataJson.Points(d, "points", item.Kind == "GeoTriangle2" ? 2 : 3);
                        Line(p, true, corners);

                        if (corners.Length == 3)
                        {
                            p.Triangles.Add(new DrawTriangle(corners[0], corners[1], corners[2]));
                        }

                        break;
                    }

                    case "GeoFace2":
                        FaceLoops(p, d, 2);
                        Surface(p, scene, () => ((GeoFace2)item.ToGeometry()).TriangulateSurface().Select(t => new DrawTriangle(V(t.A), V(t.B), V(t.C))).ToList());
                        break;

                    case "GeoFace3":
                        FaceLoops(p, d, 3);
                        Surface(p, scene, () => ((GeoFace3)item.ToGeometry()).TriangulateSurface().Select(T).ToList());
                        break;

                    case "GeoSolid3":
                        SolidLoops(p, d);
                        SolidSurface(p, scene, item);
                        break;

                    case "GeoMesh2":
                        Mesh(p, d, 2);
                        break;

                    case "GeoMesh3":
                        Mesh(p, d, 3);
                        break;

                    case "GeoAabb3":
                        Aabb(p, d);
                        break;

                    case "GeoObb3":
                        Obb(p, d);
                        break;

                    case "GeoPlane3":
                        fixedBounds = Plane(p, DataJson.Point(d, "origin", 3), DataJson.Point(d, "normal", 3), extent);
                        break;

                    case SceneKinds.Text:
                    {
                        Vec3 at = DataJson.Point(d, "at", 3);
                        string text = DataJson.TryGet(d, "text", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() : string.Empty;
                        double height = DataJson.TryGet(d, "height", out JsonElement h) ? DataJson.Number(h) : 0.0;
                        p.Labels.Add(new DrawLabel(at, text, height));
                        break;
                    }

                    default:
                        p.Warning = "the kind '" + item.Kind + "' is not one this viewer draws";
                        break;
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Whatever is wrong with the numbers of one item, the others are still drawn.
                Warn(p, "cannot be drawn: " + e.Message);
            }

            int dropped = Sanitize(p);

            if (dropped > 0)
            {
                Warn(p, dropped + " coordinate(s) that are not numbers left out of the drawing");
            }

            if (rule.Capped)
            {
                Warn(p, "a curve would take more than " + ChordRule.MaxPiecesPerTurn + " pieces a turn at a chord tolerance of " + rule.ChordTolerance.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + " and is cut in that many");
            }

            p.Bounds = fixedBounds ?? Measure(p, rule.Extremes);
            return p;
        }

        private static Vec3 V(GeoPoint2 point) => new Vec3(point.X, point.Y, 0.0);

        private static Vec3 V(GeoPoint3 point) => new Vec3(point.X, point.Y, point.Z);

        private static DrawTriangle T(GeoTriangle3 triangle) => new DrawTriangle(V(triangle.A), V(triangle.B), V(triangle.C));

        private static void Line(ItemPrimitives p, bool closed, params Vec3[] points)
        {
            if (points.Length >= 2)
            {
                p.Polylines.Add(new DrawPolyline(points, closed));
            }
            else if (points.Length == 1)
            {
                p.Points.Add(points[0]);
            }
        }

        private static IReadOnlyList<Vec3> Normals(JsonElement d)
            => DataJson.TryGet(d, "normals", out JsonElement list) ? DataJson.Points(list, 3) : new Vec3[0];

        /// <summary>
        /// Draws a ray as a dot at its origin and, once the extent of the scene is known, a line out to it.
        /// </summary>
        private static Bounds3 Ray(ItemPrimitives p, Vec3 origin, Vec3 direction, double extent)
        {
            p.Points.Add(origin);

            if (extent > 0.0)
            {
                Line(p, false, origin, origin + direction.Normalized() * extent);
            }

            return default(Bounds3).Include(origin);
        }

        /// <summary>
        /// Draws a plane as a square patch with a tick along its normal, once the extent of the scene is known.
        /// </summary>
        private static Bounds3 Plane(ItemPrimitives p, Vec3 origin, Vec3 normal, double extent)
        {
            p.Points.Add(origin);

            if (extent > 0.0 && normal.Length > 0.0)
            {
                ArcTessellation.Axes(normal, out Vec3 u, out Vec3 v);
                double half = extent * 0.5;
                Vec3 a = origin - u * half - v * half;
                Vec3 b = origin + u * half - v * half;
                Vec3 c = origin + u * half + v * half;
                Vec3 d = origin - u * half + v * half;

                Line(p, true, a, b, c, d);
                Line(p, false, origin, origin + normal.Normalized() * (half * 0.25));
                p.Triangles.Add(new DrawTriangle(a, b, c));
                p.Triangles.Add(new DrawTriangle(a, c, d));
            }

            return default(Bounds3).Include(origin);
        }

        private static Vec3[] RectangleCorners(JsonElement d)
        {
            Vec3 center = DataJson.Point(d, "center", 2);
            double halfWidth = DataJson.Number(d, "width") * 0.5;
            double halfHeight = DataJson.Number(d, "height") * 0.5;
            double angle = DataJson.Number(d, "angle");
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            Vec3 Corner(double x, double y) => new Vec3(center.X + x * cos - y * sin, center.Y + x * sin + y * cos, 0.0);

            return new[] { Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight), Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight) };
        }

        private static void FaceLoops(ItemPrimitives p, JsonElement d, int dimension)
        {
            Line(p, true, DataJson.Points(DataJson.Get(d, "outer"), dimension));

            foreach (JsonElement hole in DataJson.List(d, "holes"))
            {
                Line(p, true, DataJson.Points(hole, dimension));
            }
        }

        private static void SolidLoops(ItemPrimitives p, JsonElement d)
        {
            foreach (JsonElement face in DataJson.List(d, "faces"))
            {
                FaceLoops(p, face, 3);
            }

            if (DataJson.TryGet(d, "openings", out JsonElement openings) && openings.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement opening in openings.EnumerateArray())
                {
                    SolidLoops(p, opening);
                }
            }
        }

        /// <summary>
        /// Adds the triangles of a surface the library cuts, at the tolerance of the scene or, where the library refuses at that,
        /// at 1E-12; where it refuses at both, the outline stays and the item is warned of.
        /// </summary>
        private static void Surface(ItemPrimitives p, Scene scene, Func<List<DrawTriangle>> triangles)
        {
            try
            {
                p.Triangles.AddRange(scene.Run(triangles, out string note));

                if (note != null)
                {
                    Warn(p, "its surface was " + note);
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Warn(p, "the library could not cut its surface into triangles (" + e.GetType().Name + ": " + e.Message + "); only its outline is drawn");
            }
        }

        /// <summary>
        /// Adds the triangles of the surface of a body: where its material ends, openings cut in. A body that gives none while
        /// openings do not take all of it, or that the library cannot cut, is drawn face by face from the faces it was given,
        /// openings ignored, and warned of.
        /// </summary>
        private static void SolidSurface(ItemPrimitives p, Scene scene, SceneItem item)
        {
            string trouble;

            try
            {
                var cut = scene.Run(
                    () =>
                    {
                        var solid = (GeoSolid3)item.ToGeometry();
                        List<DrawTriangle> all = solid.TriangulateSurface().Select(T).ToList();

                        // A body with openings that gives nothing is not drawn face by face, which would show the material the
                        // openings took: it is warned of, and the library's own log of why goes to the output with it.
                        return Tuple.Create(all, all.Count == 0 && solid.Faces.Count > 0 && solid.Openings.Count > 0);
                    },
                    out string note);

                if (note != null)
                {
                    Warn(p, "its surface was " + note);
                }

                if (cut.Item1.Count > 0 || cut.Item2)
                {
                    p.Triangles.AddRange(cut.Item1);

                    if (cut.Item2)
                    {
                        Warn(p, "nothing left: the openings take all of it, or the library could not cut them");
                    }

                    return;
                }

                trouble = "the library cut the surface of the body into no triangle";
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                trouble = "the library could not cut the surface of the body (" + e.GetType().Name + ": " + e.Message + ")";
            }

            int refused = 0;
            int faces = 0;
            bool rebuilt = false;

            foreach (JsonElement face in DataJson.List(item.Data, "faces"))
            {
                faces++;

                try
                {
                    p.Triangles.AddRange(scene.Run(() => GeometryDecoder.DecodeFace3(face).TriangulateSurface().Select(T).ToList(), out string faceNote));
                    rebuilt |= faceNote != null;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    refused++;
                }
            }

            Warn(p, trouble + "; its " + faces + " faces are drawn one by one with the openings ignored" + (refused > 0 ? ", " + refused + " of them could not be drawn" : string.Empty) + (rebuilt ? "; some were rebuilt and measured at 1E-12" : string.Empty));
        }
        private static void Mesh(ItemPrimitives p, JsonElement d, int dimension)
        {
            Vec3[] points = DataJson.Points(d, "points", dimension);

            foreach (int[] face in GeometryDecoder.Faces(d))
            {
                if (face.Length < 3 || face.Any(i => i < 0 || i >= points.Length))
                {
                    p.Warning = "a mesh face has fewer than three corners or an index outside the points";
                    continue;
                }

                Vec3[] corners = face.Select(i => points[i]).ToArray();
                Line(p, true, corners);

                if (corners.Length == 3)
                {
                    p.Triangles.Add(new DrawTriangle(corners[0], corners[1], corners[2]));
                    continue;
                }

                IEnumerable<DrawTriangle> cut;

                try
                {
                    cut = dimension == 2
                        ? new GeoPolygon2(corners.Select(c => new GeoPoint2(c.X, c.Y))).TriangulateSurface().Select(t => new DrawTriangle(V(t.A), V(t.B), V(t.C))).ToList()
                        : new GeoPolygon3(corners.Select(c => new GeoPoint3(c.X, c.Y, c.Z))).Triangulate().Select(T).ToList();
                }
                catch (ArgumentException)
                {
                    // A face the library will not take as a polygon is drawn as a fan from its first corner.
                    cut = Enumerable.Range(1, corners.Length - 2).Select(k => new DrawTriangle(corners[0], corners[k], corners[k + 1])).ToList();
                    p.Warning = "a mesh face the library refuses as a polygon is drawn as a fan";
                }

                p.Triangles.AddRange(cut);
            }
        }

        private static void Aabb(ItemPrimitives p, JsonElement d)
        {
            if (DataJson.TryGet(d, "empty", out JsonElement empty) && empty.ValueKind == JsonValueKind.True)
            {
                p.Warning = "an empty box has nothing to draw";
                return;
            }

            Vec3 min = DataJson.Point(d, "min", 3);
            Vec3 max = DataJson.Point(d, "max", 3);

            Box(p, new[]
            {
                new Vec3(min.X, min.Y, min.Z), new Vec3(max.X, min.Y, min.Z), new Vec3(max.X, max.Y, min.Z), new Vec3(min.X, max.Y, min.Z),
                new Vec3(min.X, min.Y, max.Z), new Vec3(max.X, min.Y, max.Z), new Vec3(max.X, max.Y, max.Z), new Vec3(min.X, max.Y, max.Z),
            });
        }

        private static void Obb(ItemPrimitives p, JsonElement d)
        {
            Vec3 center = DataJson.Point(d, "center", 3);
            Vec3 axisX = DataJson.Point(d, "axisX", 3);
            Vec3 axisY = DataJson.Point(d, "axisY", 3);
            Vec3 axisZ = Vec3.Cross(axisX, axisY);
            Vec3 dx = axisX * (DataJson.Number(d, "sizeX") * 0.5);
            Vec3 dy = axisY * (DataJson.Number(d, "sizeY") * 0.5);
            Vec3 dz = axisZ * (DataJson.Number(d, "sizeZ") * 0.5);

            Box(p, new[]
            {
                center - dx - dy - dz, center + dx - dy - dz, center + dx + dy - dz, center - dx + dy - dz,
                center - dx - dy + dz, center + dx - dy + dz, center + dx + dy + dz, center - dx + dy + dz,
            });
        }

        /// <summary>
        /// Draws a box from its eight corners: the four of the bottom counter-clockwise seen from above, then the four above
        /// them.
        /// </summary>
        private static void Box(ItemPrimitives p, Vec3[] c)
        {
            Line(p, true, c[0], c[1], c[2], c[3]);
            Line(p, true, c[4], c[5], c[6], c[7]);

            for (int i = 0; i < 4; i++)
            {
                Line(p, false, c[i], c[i + 4]);
            }

            int[][] faces =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };

            foreach (int[] f in faces)
            {
                p.Triangles.Add(new DrawTriangle(c[f[0]], c[f[1]], c[f[2]]));
                p.Triangles.Add(new DrawTriangle(c[f[0]], c[f[2]], c[f[3]]));
            }
        }

        private static void Warn(ItemPrimitives p, string text) => p.Warning = p.Warning == null ? text : p.Warning + "; " + text;

        /// <summary>
        /// Takes out of the drawing every coordinate that is not a finite number, so that none reaches the graphics card: a point
        /// goes, a chain is cut into the runs of finite points either side of it, a triangle or a label goes whole.
        /// </summary>
        /// <returns>How many coordinates were not finite.</returns>
        private static int Sanitize(ItemPrimitives p)
        {
            int bad = 0;
            bad += p.Points.RemoveAll(v => !v.IsFinite);

            var lines = new List<DrawPolyline>();

            foreach (DrawPolyline line in p.Polylines)
            {
                int unusable = line.Points.Count(v => !v.IsFinite);

                if (unusable == 0)
                {
                    lines.Add(line);
                    continue;
                }

                bad += unusable;
                var runs = new List<List<Vec3>>();
                var run = new List<Vec3>();

                foreach (Vec3 v in line.Points.Concat(new[] { new Vec3(double.NaN, 0, 0) }))
                {
                    if (v.IsFinite)
                    {
                        run.Add(v);
                    }
                    else
                    {
                        if (run.Count > 0)
                        {
                            runs.Add(run);
                        }

                        run = new List<Vec3>();
                    }
                }

                // A loop's closing segment joins its last point to its first: where both are finite it is kept, by running the
                // last run on into the first.
                if (line.Closed && runs.Count > 1 && line.Points[0].IsFinite && line.Points[line.Points.Length - 1].IsFinite)
                {
                    List<Vec3> last = runs[runs.Count - 1];
                    last.AddRange(runs[0]);
                    runs.RemoveAt(0);
                }

                foreach (List<Vec3> kept in runs.Where(r => r.Count >= 2))
                {
                    lines.Add(new DrawPolyline(kept.ToArray(), false));
                }
            }

            p.Polylines.Clear();
            p.Polylines.AddRange(lines);

            bad += p.Triangles.RemoveAll(t => !t.A.IsFinite || !t.B.IsFinite || !t.C.IsFinite);
            bad += p.Labels.RemoveAll(l => !l.At.IsFinite);
            return bad;
        }

        private static Bounds3 Measure(ItemPrimitives p, IEnumerable<Vec3> extremes)
        {
            Bounds3 bounds = default(Bounds3);

            foreach (Vec3 point in extremes)
            {
                bounds = bounds.Include(point);
            }

            foreach (Vec3 point in p.Points)
            {
                bounds = bounds.Include(point);
            }

            foreach (DrawPolyline line in p.Polylines)
            {
                foreach (Vec3 point in line.Points)
                {
                    bounds = bounds.Include(point);
                }
            }

            foreach (DrawTriangle triangle in p.Triangles)
            {
                bounds = bounds.Include(triangle.A).Include(triangle.B).Include(triangle.C);
            }

            foreach (DrawLabel label in p.Labels)
            {
                bounds = bounds.Include(label.At);
            }

            return bounds;
        }
    }
}
