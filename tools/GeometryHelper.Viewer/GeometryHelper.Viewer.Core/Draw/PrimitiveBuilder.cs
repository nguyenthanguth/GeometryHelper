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

        /// <summary>
        /// The most triangles one ball is drawn with: 262 144. A ball of radius 50 is cut at the tolerance that fits them, 6 r / 262 144 = 0.00114, and the
        /// library cuts it 20 by 113 by 113, 255 380 triangles.
        /// The ball is cut coarser than the chord tolerance of the scene to keep to it, which a ball of radius 50 is at the default one (it would take
        /// three million at 1E-4).
        /// </summary>
        internal const int MostBallTriangles = 262144;

        /// <summary>
        /// The most triangles all the balls of a scene are drawn with together: four million, which is about 400 MB of triangles in double precision.
        /// Over it every ball is cut at one coarser chord tolerance, the finest at which they fit, as the lines of a scene are over their cap.
        /// </summary>
        internal const int MostBallTrianglesInAScene = 4000000;

        /// <summary>
        /// The normals of the three great circles drawn on a ball and read for its snaps: the planes through its centre square to X, to Y and to Z.
        /// </summary>
        internal static readonly Vec3[] GreatCircleNormals = { new Vec3(1.0, 0.0, 0.0), new Vec3(0.0, 1.0, 0.0), new Vec3(0.0, 0.0, 1.0) };

        internal static Primitives Build(Scene scene, int maxPoints)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            var result = new Primitives { Tolerance = scene.Tolerance };

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

                // A spline is cut into its Bézier pieces once, for the count of the scene's points and for its drawing.
                var cuts = new Dictionary<int, SplineCut>();
                ChordRule curveRule = ChooseRule(scene, items, curved, maxPoints, result.Warnings, cuts);

                // The balls are cut at one tolerance for all of them, coarser than the rule where together they would take more triangles than the budget.
                List<int> balls = curved.Where(i => scene.Items[i].Kind == "GeoSphere3").ToList();
                double ballTolerance = ChooseBallTolerance(scene, balls, curveRule.ChordTolerance);

                ChordRule RuleFor(int index) => new ChordRule(curveRule.ChordTolerance, curveRule.AngleOnly) { BallTolerance = scene.Items[index].Kind == "GeoSphere3" ? ballTolerance : 0.0 };

                foreach (int i in curved)
                {
                    items[i] = BuildItem(scene, scene.Items[i], i, 0.0, RuleFor(i), cuts: cuts);
                }

                // The count of triangles a ball takes is about what the formula says, not exactly: where the balls built took more than the budget, the tolerance is raised by
                // what they were over and they are built again, a few times at most.
                for (int round = 0; round < 6 && balls.Count > 0 && ballTolerance > 0.0; round++)
                {
                    long total = balls.Sum(i => (long)items[i].Triangles.Count);

                    if (total <= MostBallTrianglesInAScene)
                    {
                        break;
                    }

                    ballTolerance *= Math.Max(1.02, total / (double)MostBallTrianglesInAScene);

                    foreach (int i in balls)
                    {
                        items[i] = BuildItem(scene, scene.Items[i], i, 0.0, RuleFor(i), cuts: cuts);
                    }
                }

                if (balls.Count > 0 && ballTolerance > curveRule.ChordTolerance)
                {
                    long after = balls.Sum(i => (long)items[i].Triangles.Count);
                    result.Warnings.Add(
                        "the " + balls.Count + " ball(s) of the scene would take more triangles than the budget of " + MostBallTrianglesInAScene + " at the chord tolerance of "
                        + curveRule.ChordTolerance.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + ": they are all cut within "
                        + ballTolerance.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + " of their spheres instead, which leaves " + after + " triangles"
                        + (after > MostBallTrianglesInAScene ? ", still over the budget" : string.Empty));
                }

                foreach (ItemPrimitives item in items)
                {
                    result.Items.Add(item);
                    bounds = bounds.Include(item.Bounds);
                }

                // Rays and planes have no end; they are drawn out to a length that takes in what else is in the scene. A line without ends is
                // drawn through the box of the scene instead, and a margin past it.
                double extent = bounds.IsEmpty ? EmptySceneExtent : Math.Max(bounds.Size.Length * 2.0, 1.0);

                for (int i = 0; i < scene.Items.Count; i++)
                {
                    if (IsUnbounded(scene.Items[i].Kind))
                    {
                        result.Items[i] = BuildItem(scene, scene.Items[i], i, extent, null, bounds);
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
                case "GeoSphere3":
                case "GeoSpline2":
                case "GeoSpline3":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// What the curves of a scene take in points, apart from the pieces they are cut in: a number of points that do not change, each turn
        /// of a curve with the radius it is cut at, and each spline by the points it takes at the chord tolerance of the scene and by the angle alone.
        /// </summary>
        private sealed class CurveCount
        {
            internal long Fixed;
            internal readonly List<KeyValuePair<double, double>> Turns = new List<KeyValuePair<double, double>>();
            internal readonly List<SplineCut> Splines = new List<SplineCut>();
            internal double Chord;

            internal long At(ChordRule rule)
            {
                long points = Fixed;

                foreach (KeyValuePair<double, double> turn in Turns)
                {
                    points += rule.Pieces(turn.Key, turn.Value);
                }

                // A spline is cut at the rule of the scene, which is what draws it unless the scene is over its cap; at a coarser rule a smooth curve
                // takes chords in proportion to one over the root of the tolerance, and never fewer than the angle alone asks.
                foreach (SplineCut spline in Splines)
                {
                    int fine = spline.Points(new ChordRule(Chord)).Length;

                    if (rule.AngleOnly)
                    {
                        points += spline.Points(new ChordRule(Chord, true)).Length;
                    }
                    else if (rule.ChordTolerance == Chord)
                    {
                        points += fine;
                    }
                    else
                    {
                        points += Math.Max(spline.Points(new ChordRule(Chord, true)).Length, (long)Math.Ceiling(fine * Math.Sqrt(Chord / rule.ChordTolerance)));
                    }
                }

                return points;
            }
        }

        /// <summary>
        /// Reads what an item that is a curve will take in points, without cutting it: an arc takes its pieces and one more, a circle or an
        /// ellipse its pieces, a chain its vertices and the pieces of each arc less one.
        /// </summary>
        private static void Count(SceneItem item, int index, CurveCount count, Dictionary<int, SplineCut> cuts)
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

                    case "GeoSphere3":
                        for (int circle = 0; circle < GreatCircleNormals.Length; circle++)
                        {
                            count.Turns.Add(new KeyValuePair<double, double>(2.0 * Math.PI, DataJson.Number(d, "radius")));
                        }

                        break;

                    case "GeoEllipse2":
                    case "GeoEllipse3":
                        count.Turns.Add(new KeyValuePair<double, double>(2.0 * Math.PI, Math.Max(Math.Abs(DataJson.Number(d, "majorRadius")), Math.Abs(DataJson.Number(d, "minorRadius")))));
                        break;

                    case "GeoSpline2":
                    case "GeoSpline3":
                    {
                        SplineCut cut = CutOf(item);
                        cuts[index] = cut;
                        count.Splines.Add(cut);
                        break;
                    }

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
        private static ChordRule ChooseRule(Scene scene, ItemPrimitives[] items, List<int> curved, int maxPoints, List<string> warnings, Dictionary<int, SplineCut> cuts)
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

            var count = new CurveCount { Chord = chord };

            foreach (int i in curved)
            {
                Count(scene.Items[i], i, count, cuts);
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
        /// <summary>
        /// Gets how many triangles the balls of a scene take, by the formula (a ball cut within a tolerance takes about 6 r / tolerance, at least the
        /// twenty of an icosahedron and at most <see cref="MostBallTriangles"/>).
        /// </summary>
        private static long EstimateBallTriangles(List<double> radii, double tolerance)
        {
            double total = 0.0;

            foreach (double radius in radii)
            {
                double within = Math.Max(tolerance, 6.0 * radius / MostBallTriangles);
                total += Math.Max(20.0, Math.Min(MostBallTriangles, 6.0 * radius / within));
            }

            return (long)Math.Ceiling(total);
        }

        /// <summary>
        /// Chooses the chord tolerance every ball of a scene is cut at: the one of the rule where the balls then take no more than
        /// <see cref="MostBallTrianglesInAScene"/> triangles together, and otherwise the finest coarser one at which they do, found by the formula and
        /// not by cutting them. Gives nought where the scene has no ball.
        /// </summary>
        private static double ChooseBallTolerance(Scene scene, List<int> balls, double chord)
        {
            var radii = new List<double>();

            foreach (int i in balls)
            {
                try
                {
                    double radius = DataJson.Number(scene.Items[i].Data, "radius");

                    if (radius > 0.0 && !double.IsInfinity(radius))
                    {
                        radii.Add(radius);
                    }
                }
                catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is KeyNotFoundException)
                {
                    // The numbers are wrong: the drawing of the item says so, and it takes no triangles here.
                }
            }

            if (balls.Count == 0)
            {
                return 0.0;
            }

            long budget = MostBallTrianglesInAScene - (long)(MostBallTrianglesInAScene * 0.03);

            if (EstimateBallTriangles(radii, chord) <= budget)
            {
                return chord;
            }

            // The finest tolerance that fits lies between the one of the rule, which does not, and the first of its doublings that does.
            double low = chord;
            double high = chord * 2.0;

            for (int doublings = 0; doublings < 200 && EstimateBallTriangles(radii, high) > budget; doublings++)
            {
                low = high;
                high *= 2.0;
            }

            for (int step = 0; step < 40; step++)
            {
                double middle = Math.Sqrt(low * high);

                if (EstimateBallTriangles(radii, middle) > budget)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return high;
        }

        /// <summary>
        /// Cuts a spline item into its Bézier pieces, from the numbers of its data.
        /// </summary>
        private static SplineCut CutOf(SceneItem item)
        {
            JsonElement d = item.Data;
            return SplineCut.Of(DataJson.Degree(d), DataJson.Points(d, "points", item.Kind == "GeoSpline2" ? 2 : 3), DataJson.Numbers(DataJson.Get(d, "knots")), DataJson.Weights(d));
        }

        private static bool IsUnbounded(string kind) => kind == "GeoRay2" || kind == "GeoRay3" || kind == "GeoXLine2" || kind == "GeoXLine3" || kind == "GeoPlane3";

        /// <summary>
        /// Gets how far a chord may lie from its curve: a tenth of the point tolerance of the scene, so that the drawing never
        /// differs from the curve by more than a tenth of what the library takes as one place.
        /// </summary>
        private static double ChordOf(Scene scene)
            => scene.EqualPoint > 0.0 && !double.IsInfinity(scene.EqualPoint) ? scene.EqualPoint / 10.0 : Scene.DefaultEqualPoint / 10.0;

        private static ItemPrimitives BuildItem(Scene scene, SceneItem item, int index, double extent, ChordRule rule, Bounds3 around = default(Bounds3), Dictionary<int, SplineCut> cuts = null)
        {
            using (LibraryLog log = Viewer.LibraryLog.Begin())
            {
                ItemPrimitives built = BuildItemCore(scene, item, index, extent, rule, around, cuts != null && cuts.TryGetValue(index, out SplineCut cut) ? cut : null);
                built.LibraryLog.AddRange(log.Lines.Distinct(StringComparer.Ordinal));
                return built;
            }
        }

        private static ItemPrimitives BuildItemCore(Scene scene, SceneItem item, int index, double extent, ChordRule rule, Bounds3 around, SplineCut cut)
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
                    case "GeoXLine2": fixedBounds = XLine(p, DataJson.Point(d, "a", 2), DataJson.Point(d, "b", 2), extent, around); break;
                    case "GeoXLine3": fixedBounds = XLine(p, DataJson.Point(d, "a", 3), DataJson.Point(d, "b", 3), extent, around); break;
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

                    case "GeoSpline2":
                    case "GeoSpline3":
                        Line(p, false, (cut ?? CutOf(item)).Points(rule));
                        break;

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
                        Surface(p, scene, () => FaceCutter.Cut((GeoFace3)item.ToGeometry()).Select(T).ToList());
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
                        p.IsClosedBody = EveryEdgeUsedTwice(d);
                        break;

                    case "GeoAabb3":
                        Aabb(p, d);
                        p.IsClosedBody = p.Triangles.Count > 0;
                        break;

                    case "GeoObb3":
                        Obb(p, d);
                        p.IsClosedBody = p.Triangles.Count > 0;
                        break;

                    case "GeoSphere3":
                        Ball(p, scene, item, rule);
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

            if (rule.Capped && (item.Kind == "GeoSpline2" || item.Kind == "GeoSpline3"))
            {
                int most = 1 << SplineTessellation.MostHalvings;
                Warn(p, "a Bézier piece of the spline would take more than " + most + " chords at a chord tolerance of " + rule.ChordTolerance.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + " and is cut in that many, halved " + SplineTessellation.MostHalvings + " times");
            }
            else if (rule.Capped)
            {
                Warn(p, "a curve would take more than " + ChordRule.MaxPiecesPerTurn + " pieces a turn at a chord tolerance of " + rule.ChordTolerance.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + " and is cut in that many");
            }

            p.Bounds = fixedBounds ?? Measure(p, rule.Extremes);
            p.BoundsCoverAll = fixedBounds == null;
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
        /// Draws a line without ends as a dot at its origin and, once the box of the scene is known, the stretch of it inside that box grown by a
        /// twentieth of its diagonal on every side, at least a millimetre: the line is cut where it leaves the grown box.
        /// </summary>
        /// <remarks>
        /// The origin is in the box of the scene, which takes in the origin of every ray, line and plane, so the line always has a stretch inside.
        /// </remarks>
        private static Bounds3 XLine(ItemPrimitives p, Vec3 origin, Vec3 direction, double extent, Bounds3 around)
        {
            p.Points.Add(origin);

            if (extent > 0.0 && !around.IsEmpty && direction.Length > 0.0)
            {
                Vec3 unit = direction.Normalized();
                double margin = Math.Max(around.Size.Length * 0.05, 1.0);
                double near = double.NegativeInfinity;
                double far = double.PositiveInfinity;

                if (Slab(origin.X, unit.X, around.Min.X - margin, around.Max.X + margin, ref near, ref far)
                    && Slab(origin.Y, unit.Y, around.Min.Y - margin, around.Max.Y + margin, ref near, ref far)
                    && Slab(origin.Z, unit.Z, around.Min.Z - margin, around.Max.Z + margin, ref near, ref far)
                    && near <= far)
                {
                    Line(p, false, origin + unit * near, origin + unit * far);
                }
            }

            return default(Bounds3).Include(origin);
        }

        /// <summary>
        /// Narrows the stretch of a line that lies between two parallel planes of a box: false when the line runs level with them and outside.
        /// </summary>
        private static bool Slab(double origin, double step, double low, double high, ref double near, ref double far)
        {
            if (step == 0.0)
            {
                return origin >= low && origin <= high;
            }

            double first = (low - origin) / step;
            double second = (high - origin) / step;

            near = Math.Max(near, Math.Min(first, second));
            far = Math.Min(far, Math.Max(first, second));
            return true;
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

        /// <summary>
        /// Draws a ball as a dot at its centre, three great circles cut by the rule, and the mesh of its surface the library cuts at the chord tolerance of the
        /// rule: a closed body. The outline is read from the numbers, so a ball the library refuses is still drawn by its circles.
        /// </summary>
        /// <remarks>
        /// A ball cut within a tolerance takes about 6 r / tolerance triangles, so the tolerance is raised to keep them to <see cref="MostBallTriangles"/>; the item is
        /// warned of only where that leaves the mesh further from the sphere than ten point tolerances of the scene (a ball of radius 500, cut in 255 380 triangles within 0.0114, is; one of radius 400 is not): the great circles, the snaps and the measures are exact whatever the mesh.
        /// Where the balls of a scene together would pass <see cref="MostBallTrianglesInAScene"/>, the rule carries the one tolerance they are all cut at, and the scene is warned of it once instead.
        /// </remarks>
        private static void Ball(ItemPrimitives p, Scene scene, SceneItem item, ChordRule rule)
        {
            JsonElement d = item.Data;
            Vec3 center = DataJson.Point(d, "center", 3);
            double radius = DataJson.Number(d, "radius");

            p.Points.Add(center);

            foreach (Vec3 normal in GreatCircleNormals)
            {
                Line(p, true, ArcTessellation.Circle3(rule, center, normal, radius));
            }

            double basis = rule.BallTolerance > 0.0 ? rule.BallTolerance : rule.ChordTolerance;
            double within = basis;

            Surface(p, scene, () =>
            {
                var ball = (GeoSphere3)item.ToGeometry();
                within = Math.Max(basis, 6.0 * ball.Radius / MostBallTriangles);
                GeoTriangle3[] cut = ball.TriangulateSurface(within);

                // The count is a little over or under what the sag formula says: a tolerance a fifth coarser takes about a tenth fewer pieces an edge.
                while (cut.Length > MostBallTriangles)
                {
                    within *= 1.2;
                    cut = ball.TriangulateSurface(within);
                }

                return cut.Select(T).ToList();
            });

            p.IsClosedBody = p.Triangles.Count > 0;

            if (p.Triangles.Count > 0 && within > basis && within > ChordOf(scene) * 100.0)
            {
                Warn(p, "its mesh is cut in " + p.Triangles.Count + " triangles, within " + within.ToString("G3", System.Globalization.CultureInfo.InvariantCulture)
                    + " of the sphere and not within the chord tolerance of " + ChordOf(scene).ToString("G3", System.Globalization.CultureInfo.InvariantCulture)
                    + ": a ball is drawn with at most " + MostBallTriangles + " triangles (its circles, snaps and measures are exact)");
            }
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
            bool closed = false;

            try
            {
                var cut = scene.Run(
                    () =>
                    {
                        var solid = (GeoSolid3)item.ToGeometry();
                        List<DrawTriangle> all = (solid.Openings.Count == 0 ? solid.Faces.SelectMany(FaceCutter.Cut).ToArray() : solid.TriangulateSurface()).Select(T).ToList();
                        closed = solid.IsClosed(scene.Tolerance);

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
                    p.IsClosedBody = closed;

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
                    p.Triangles.AddRange(scene.Run(() => FaceCutter.Cut(GeometryDecoder.DecodeFace3(face)).Select(T).ToList(), out string faceNote));
                    rebuilt |= faceNote != null;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    refused++;
                }
            }

            Warn(p, trouble + "; its " + faces + " faces are drawn one by one with the openings ignored" + (refused > 0 ? ", " + refused + " of them could not be drawn" : string.Empty) + (rebuilt ? "; some were rebuilt and measured at 1E-12" : string.Empty));
        }
        /// <summary>
        /// Gets whether every edge of a mesh is used by exactly two of its faces, going by the indices of its corners, which is what makes a mesh a closed body.
        /// </summary>
        private static bool EveryEdgeUsedTwice(JsonElement d)
        {
            int points = DataJson.Points(d, "points", 3).Length;
            var uses = new Dictionary<Tuple<int, int>, int>();

            foreach (int[] face in GeometryDecoder.Faces(d))
            {
                if (face.Length < 3 || face.Any(i => i < 0 || i >= points))
                {
                    return false;
                }

                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];
                    var key = a < b ? Tuple.Create(a, b) : Tuple.Create(b, a);
                    uses.TryGetValue(key, out int count);
                    uses[key] = count + 1;
                }
            }

            return uses.Count > 0 && uses.Values.All(c => c == 2);
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
