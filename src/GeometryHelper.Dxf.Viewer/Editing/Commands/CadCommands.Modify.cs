using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The modify commands that cut and run on curves, as AutoCAD 2021's: TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN and LENGTHEN.
    /// Each works on lines, arcs, circles, ellipses, polylines (LWPOLYLINE), splines, rays and xlines as far as the library's
    /// curves (<see cref="DxfCurve"/>) split, extend and join them, and each run is one undo step; inside TRIM, EXTEND and
    /// LENGTHEN, Undo takes back the last pick. Projections are not read: objects meet where they meet in the plan.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string ModeKey = "trimextend.mode";
        private const string EdgeKey = "trimextend.edge";
        private const string ProjectionKey = "trimextend.projection";

        private static IEnumerable<CadPrompt> Trim(CadCommandContext c) => TrimOrExtend(c, false);

        private static IEnumerable<CadPrompt> ExtendCommand(CadCommandContext c) => TrimOrExtend(c, true);

        /// <summary>
        /// TRIM and EXTEND, one body as AutoCAD's: Quick mode takes every object as a cutting or boundary edge, Standard asks for
        /// them first; an object picked is trimmed at the edges nearest the pick on either side, or extended at its end nearer
        /// the pick to the first edge it meets, and a pick with Shift held does the other.
        /// </summary>
        private static IEnumerable<CadPrompt> TrimOrExtend(CadCommandContext c, bool extend)
        {
            string[] projections = { "None", "UCS", "View" };
            c.Write("Current settings: Projection=" + projections[(int)c.Remembered(ProjectionKey, 1.0)] + ", Edge=" + (c.Remembered(EdgeKey, 0.0) != 0.0 ? "Extend" : "None")
                + ", Mode=" + (c.Remembered(ModeKey, 1.0) != 0.0 ? "Quick" : "Standard"));
            c.OpenUndoStep();
            var edges = new Ref<IReadOnlyList<string>>();
            var undo = new List<CadGroupMark>();

            if (c.Remembered(ModeKey, 1.0) == 0.0)
            {
                foreach (CadPrompt prompt in SelectEdges(c, extend, edges))
                {
                    yield return prompt;
                }
            }

            while (true)
            {
                bool quick = c.Remembered(ModeKey, 1.0) != 0.0;
                var keywords = new List<string>();

                if (quick)
                {
                    keywords.Add(extend ? "Boundary edges" : "cuTting edges");
                }

                keywords.AddRange(new[] { "Fence", "Crossing", "mOde", "Project" });

                if (extend || !quick)
                {
                    keywords.Add("Edge");
                }

                if (!extend)
                {
                    keywords.Add("eRase");
                }

                if (undo.Count > 0)
                {
                    keywords.Add("Undo");
                }

                CadPrompt asked = CadPrompt.ForEntity(extend ? "Select object to extend or shift-select to trim" : "Select object to trim or shift-select to extend", keywords);

                // The object under the cursor previews the piece a trim takes out, faded, or the object an extension runs on,
                // and with Shift held what the other command does to it.
                IReadOnlyList<string> cutting = edges.Value;
                Func<CadInput, IEnumerable<DxfEntity>> cut = input =>
                {
                    var preview = new List<DxfEntity>();
                    return input.Kind == CadInputKind.Selection && CutOne(c, input.Handles[0], input.Point, extend != input.Shift, cutting, preview) ? preview : null;
                };

                yield return asked.Previewing(CadPreview.Cutting(c, cut, extend));

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    CadGroupMark mark = c.Document.MarkGroup();

                    if (Cut(c, c.Input.Handles[0], c.Input.Point, extend != c.Input.Shift, edges.Value))
                    {
                        undo.Add(mark);
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    c.Document.BackTo(undo[undo.Count - 1]);
                    undo.RemoveAt(undo.Count - 1);
                }
                else if (c.Input.Is("Fence") || c.Input.Is("Crossing"))
                {
                    var picks = new List<KeyValuePair<string, GeoPoint2>>();

                    foreach (CadPrompt prompt in c.Input.Is("Fence") ? Fence(c, picks) : Crossing(c, picks))
                    {
                        yield return prompt;
                    }

                    CadGroupMark mark = c.Document.MarkGroup();
                    bool any = false;

                    foreach (KeyValuePair<string, GeoPoint2> pick in picks)
                    {
                        string handle = Repick(c, pick.Key, pick.Value);
                        any |= handle != null && Cut(c, handle, pick.Value, extend, edges.Value);
                    }

                    if (any)
                    {
                        undo.Add(mark);
                    }
                }
                else if (c.Input.Is("mOde"))
                {
                    yield return CadPrompt.ForKeyword(extend ? "Enter an extend mode option" : "Enter a trim mode option", new[] { "Quick", "Standard" }, quick ? "Quick" : "Standard");

                    if (c.Input.Is("Standard"))
                    {
                        c.Remember(ModeKey, 0.0);

                        foreach (CadPrompt prompt in SelectEdges(c, extend, edges))
                        {
                            yield return prompt;
                        }
                    }
                    else if (c.Input.Is("Quick"))
                    {
                        c.Remember(ModeKey, 1.0);
                        edges.Value = null;
                    }
                }
                else if (c.Input.Is("cuTting edges") || c.Input.Is("Boundary edges"))
                {
                    foreach (CadPrompt prompt in SelectEdges(c, extend, edges))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Project"))
                {
                    yield return CadPrompt.ForKeyword("Enter a projection option", new[] { "None", "Ucs", "View" }, new[] { "None", "Ucs", "View" }[(int)c.Remembered(ProjectionKey, 1.0)]);

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        c.Remember(ProjectionKey, c.Input.Is("None") ? 0.0 : c.Input.Is("Ucs") ? 1.0 : 2.0);
                    }
                }
                else if (c.Input.Is("Edge"))
                {
                    yield return CadPrompt.ForKeyword("Enter an implied edge extension mode", new[] { "Extend", "No extend" }, c.Remembered(EdgeKey, 0.0) != 0.0 ? "Extend" : "No extend");

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        c.Remember(EdgeKey, c.Input.Is("Extend") ? 1.0 : 0.0);
                    }
                }
                else if (c.Input.Is("eRase"))
                {
                    yield return CadPrompt.ForSelection("Select objects to erase or <exit>");
                    var erased = new List<string>();

                    foreach (string handle in c.Input.Handles)
                    {
                        if (CanChange(c, handle, "erased"))
                        {
                            erased.Add(handle);
                        }
                    }

                    if (erased.Count > 0)
                    {
                        CadGroupMark mark = c.Document.MarkGroup();
                        EraseAll(c, erased);
                        undo.Add(mark);
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>Asks the cutting or boundary edges, as Standard mode and the edges option do; Enter takes every object.</summary>
        private static IEnumerable<CadPrompt> SelectEdges(CadCommandContext c, bool extend, Ref<IReadOnlyList<string>> edges)
        {
            c.Write(extend ? "Select boundary edges ..." : "Select cutting edges ...");
            yield return CadPrompt.ForSelection("Select objects or <select all>");
            edges.Value = c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0 ? c.Input.Handles : null;
        }

        /// <summary>Asks the points of a fence and gives each crossing of it with an object, along the fence.</summary>
        private static IEnumerable<CadPrompt> Fence(CadCommandContext c, List<KeyValuePair<string, GeoPoint2>> picks)
        {
            var points = new List<GeoPoint2>();
            yield return CadPrompt.ForPoint("Specify first fence point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            points.Add(c.Input.Point);

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify next fence point", points[points.Count - 1], new[] { "Undo" });

                if (c.Input.IsPoint)
                {
                    points.Add(c.Input.Point);
                }
                else if (c.Input.Is("Undo") && points.Count > 1)
                {
                    points.RemoveAt(points.Count - 1);
                }
                else if (!c.Input.Is("Undo"))
                {
                    break;
                }
            }

            double eps = Slack(c);
            var found = new List<Tuple<double, string, GeoPoint2>>();
            double along = 0.0;

            for (int i = 0; i + 1 < points.Count; i++)
            {
                GeoPoint2 a = points[i];
                GeoPoint2 b = points[i + 1];

                if ((b - a).Length <= eps)
                {
                    continue;
                }

                var segment = new DxfLine(P3(a), P3(b));

                foreach (DxfEntity entity in Near(c, new DisplayBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)).Inflate(eps), null, null))
                {
                    foreach (GeoPoint3 point in Meet(entity, segment, false))
                    {
                        var flat = new GeoPoint2(point.X, point.Y);
                        found.Add(Tuple.Create(along + (flat - a).Length, entity.Handle, flat));
                    }
                }

                along += (b - a).Length;
            }

            found.Sort((x, y) => x.Item1 != y.Item1 ? x.Item1.CompareTo(y.Item1) : string.CompareOrdinal(x.Item2, y.Item2));
            picks.AddRange(found.Select(f => new KeyValuePair<string, GeoPoint2>(f.Item2, f.Item3)));
        }

        /// <summary>
        /// Asks the corners of a crossing box and gives each object it takes, picked where it comes nearest the first corner
        /// within the box, the nearest first.
        /// </summary>
        private static IEnumerable<CadPrompt> Crossing(CadCommandContext c, List<KeyValuePair<string, GeoPoint2>> picks)
        {
            yield return CadPrompt.ForPoint("Specify first corner");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify opposite corner", first);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;
            double eps = Slack(c);
            var box = new DisplayBox(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
            var corners = new[] { new GeoPoint2(box.MinX, box.MinY), new GeoPoint2(box.MaxX, box.MinY), new GeoPoint2(box.MaxX, box.MaxY), new GeoPoint2(box.MinX, box.MaxY) };
            var found = new List<Tuple<double, string, GeoPoint2>>();

            foreach (string handle in c.Document.GetPickIndex(c.Space).Crossing(box))
            {
                if (!(c.Document.FindEntity(handle) is DxfCurve curve))
                {
                    continue;
                }

                GeoPoint3 near = curve.GetClosestPointTo(P3(first), false);
                GeoPoint2? pick = box.Inflate(eps).Contains(near.X, near.Y) ? new GeoPoint2(near.X, near.Y) : (GeoPoint2?)null;

                for (int i = 0; i < 4 && pick == null; i++)
                {
                    foreach (GeoPoint3 point in Meet(curve, new DxfLine(P3(corners[i]), P3(corners[(i + 1) % 4])), false))
                    {
                        var flat = new GeoPoint2(point.X, point.Y);

                        if (pick == null || (flat - first).Length < (pick.Value - first).Length)
                        {
                            pick = flat;
                        }
                    }
                }

                if (pick != null)
                {
                    found.Add(Tuple.Create((pick.Value - first).Length, handle, pick.Value));
                }
            }

            found.Sort((x, y) => x.Item1 != y.Item1 ? x.Item1.CompareTo(y.Item1) : string.CompareOrdinal(x.Item2, y.Item2));
            picks.AddRange(found.Select(f => new KeyValuePair<string, GeoPoint2>(f.Item2, f.Item3)));
        }

        /// <summary>The object a fence or crossing found at a point, or what took its place there after an earlier cut; null where none is.</summary>
        private static string Repick(CadCommandContext c, string handle, GeoPoint2 at)
        {
            if (c.Document.FindEntity(handle) != null)
            {
                return handle;
            }

            foreach (string other in c.Document.GetPickIndex(c.Space).Pick(at.X, at.Y, Math.Max(c.Session.PickAperture, Slack(c))))
            {
                if (c.Document.FindEntity(other) is DxfCurve curve && curve.GetClosestPointTo(P3(at), false).DistanceTo(P3(at)) <= 10.0 * Slack(c))
                {
                    return other;
                }
            }

            return null;
        }

        /// <summary>Trims or extends one object picked at a point; false, with the reason written, where nothing was changed.</summary>
        private static bool Cut(CadCommandContext c, string handle, GeoPoint2 at, bool extend, IReadOnlyList<string> edges)
            => Splits(c, extend ? "extended" : "trimmed", () => CutOne(c, handle, at, extend, edges));

        /// <summary>
        /// Trims or extends one object picked at a point. Given a list to preview into, it adds to it what the preview
        /// shows instead, and changes nothing: the piece a trim takes out (the whole object where Quick erases it), or a copy of
        /// the object run on.
        /// </summary>
        private static bool CutOne(CadCommandContext c, string handle, GeoPoint2 at, bool extend, IReadOnlyList<string> edges, List<DxfEntity> preview = null)
        {
            if (!CanChange(c, handle, extend ? "extended" : "trimmed"))
            {
                return false;
            }

            var curve = (DxfCurve)c.Document.FindEntity(handle);
            bool extendEdges = c.Remembered(EdgeKey, 0.0) != 0.0;
            bool quick = c.Remembered(ModeKey, 1.0) != 0.0 && edges == null;

            if (extend)
            {
                if (!TryExtension(c, curve, at, edges, extendEdges, out bool atStart, out GeoPoint3 to))
                {
                    return false;
                }

                if (preview != null)
                {
                    var run = (DxfCurve)curve.Clone();
                    run.Extend(atStart, to);
                    preview.Add(run);
                    return true;
                }

                return c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Extend") }, s => ((DxfCurve)s.Document.FindEntity(handle)).Extend(atStart, to)).Succeeded;
            }

            DisplayBox box = BoxOf(c, curve);
            List<GeoPoint3> meetings = new List<GeoPoint3>();

            foreach (DxfEntity edge in Near(c, box, handle, edges))
            {
                meetings.AddRange(Meet(curve, edge, extendEdges));
            }

            List<DxfCurve> kept = TrimmedPieces(c, curve, P3(at), meetings, out DxfCurve gone);

            if (kept == null)
            {
                if (quick)
                {
                    if (preview != null)
                    {
                        preview.Add((DxfEntity)curve.Clone());
                        return true;
                    }

                    // AutoCAD 2021's Quick mode erases an object that no other object cuts.
                    EraseAll(c, new[] { handle });
                    return true;
                }

                c.Write("Object does not intersect an edge.");
                return false;
            }

            if (preview != null)
            {
                preview.Add(gone);
                return true;
            }

            return Replace(c, handle, kept, "Trim");
        }

        /// <summary>
        /// The pieces left of a curve once the piece about a point between the cuts nearest it on either side is taken out; on
        /// a closed curve the piece between the two cuts round it. Null where no cut lies on either side. The piece taken out
        /// is given apart.
        /// </summary>
        private static List<DxfCurve> TrimmedPieces(CadCommandContext c, DxfCurve curve, GeoPoint3 pick, IEnumerable<GeoPoint3> cuts, out DxfCurve gone)
        {
            gone = null;

            double eps = Slack(c);
            GeoPoint3 near = curve.GetClosestPointTo(pick, false);
            double at = DistanceOf(curve, near) ?? double.NaN;
            Span(curve, out double start, out double length);
            var distances = new List<double>();

            foreach (GeoPoint3 cut in cuts)
            {
                double? d = DistanceOf(curve, cut);

                if (d.HasValue)
                {
                    distances.Add(curve.Closed && d.Value >= length - eps ? 0.0 : d.Value);
                }
            }

            distances.Sort();

            if (double.IsNaN(at) || distances.Count == 0)
            {
                return null;
            }

            if (curve.Closed)
            {
                // The cuts round the pick, one before it and one after it going round; two at least, apart.
                double before = distances.Where(d => d < at - eps).DefaultIfEmpty(distances[distances.Count - 1]).Max();
                double after = distances.Where(d => d > at + eps).DefaultIfEmpty(distances[0]).Min();

                if (Math.Abs(after - before) <= eps || Math.Abs(Math.Abs(after - before) - length) <= eps)
                {
                    return null;
                }

                IReadOnlyList<DxfCurve> pieces = curve.GetSplitCurves(new[] { curve.GetParameterAtDistance(before), curve.GetParameterAtDistance(after) });

                if (pieces.Count != 2)
                {
                    return null;
                }

                // The piece the pick lies on goes; the nearer one, the first where both are as near.
                int taken = pieces[1].GetClosestPointTo(near, false).DistanceTo(near) < pieces[0].GetClosestPointTo(near, false).DistanceTo(near) ? 1 : 0;
                gone = pieces[taken];
                return new List<DxfCurve> { pieces[1 - taken] };
            }

            double? low = null;
            double? high = null;

            foreach (double d in distances)
            {
                if (d < at - eps && d > start + eps)
                {
                    low = d;
                }

                if (d > at + eps && d < length - eps && high == null)
                {
                    high = d;
                }
            }

            if (low == null && high == null)
            {
                return null;
            }

            var parameters = new List<double>();

            foreach (double? d in new[] { low, high })
            {
                if (d.HasValue)
                {
                    parameters.Add(curve.GetParameterAtDistance(d.Value));
                }
            }

            List<DxfCurve> kept = curve.GetSplitCurves(parameters).ToList();

            if (kept.Count != parameters.Count + 1)
            {
                return null;
            }

            gone = kept[low.HasValue ? 1 : 0];
            kept.RemoveAt(low.HasValue ? 1 : 0);
            return kept;
        }

        /// <summary>
        /// Finds where the end of a curve nearer a point runs on to: the first edge its line, circle or ellipse meets past the
        /// end. False, with the reason written, where it meets none or is not a curve that is extended.
        /// </summary>
        private static bool TryExtension(CadCommandContext c, DxfCurve curve, GeoPoint2 at, IReadOnlyList<string> edges, bool extendEdges, out bool atStart, out GeoPoint3 to)
        {
            atStart = false;
            to = default;
            double eps = Slack(c);

            if (curve.Closed || !(curve is DxfLine || curve is DxfArc || curve is DxfPolyline || curve is DxfEllipse))
            {
                c.Write("Cannot extend this object.");
                return false;
            }

            Span(curve, out _, out double length);
            atStart = (DistanceOf(curve, curve.GetClosestPointTo(P3(at), false)) ?? length) < length / 2.0;
            GeoPoint3 end = atStart ? curve.StartPoint : curve.EndPoint;
            DxfCurve run = RunOn(c, curve, atStart, out GeoVector3 way, out double sweep);
            DisplayBox box = BoxOf(c, run);
            double best = double.PositiveInfinity;
            bool found = false;

            foreach (DxfEntity edge in Near(c, box, curve.Handle, edges))
            {
                foreach (GeoPoint3 point in Meet(run, edge, extendEdges))
                {
                    double travel = Travel(curve, run, end, way, sweep, atStart, point);

                    if (travel > eps && travel < best)
                    {
                        best = travel;
                        to = point;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                c.Write("Object does not intersect an edge.");
                return false;
            }

            // Tried on a copy first, so that an extension the curve refuses is said and leaves the command's step whole.
            try
            {
                ((DxfCurve)curve.Clone()).Extend(atStart, to);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException)
            {
                c.Write("Cannot extend this object: " + exception.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// The shape an end of a curve runs on along, in no file: a ray from a straight end, the circle of an arc or of a
        /// polyline's arc end, the whole ellipse of an elliptical arc; with the way the end leaves and the angle the end's arc
        /// sweeps already.
        /// </summary>
        private static DxfCurve RunOn(CadCommandContext c, DxfCurve curve, bool atStart, out GeoVector3 way, out double sweep)
        {
            GeoVector3 tangent = atStart ? curve.GetFirstDerivative(curve.StartParam).Negate() : curve.GetFirstDerivative(curve.EndParam);
            way = tangent.Normalize();
            sweep = 0.0;
            GeoPoint3 end = atStart ? curve.StartPoint : curve.EndPoint;

            switch (curve)
            {
                case DxfArc arc:
                    sweep = arc.TotalAngle;
                    return new DxfCircle(arc.Center, arc.Normal, arc.Radius);
                case DxfEllipse ellipse:
                    sweep = ellipse.EndParam - ellipse.StartParam;
                    return new DxfEllipse(ellipse.Center, ellipse.Normal, ellipse.MajorAxis, ellipse.RadiusRatio, 0.0, 2.0 * Math.PI);
                case DxfPolyline polyline:
                    int segment = atStart ? 0 : polyline.NumberOfVertices - 2;

                    if (polyline.GetSegmentType(segment) == DxfSegmentType.Arc)
                    {
                        GeoArc3 arc = polyline.GetArcSegmentAt(segment);
                        sweep = Math.Abs(arc.SweptAngle);
                        return new DxfCircle(arc.Center, arc.Normal, arc.Radius);
                    }

                    break;
            }

            return new DxfRay { BasePoint = end, UnitDir = way };
        }

        /// <summary>
        /// How far a point lies past an end along the shape it runs on: the distance along a ray, the arc's length round a
        /// circle, the parameter round an ellipse; infinity where it lies back over the curve itself.
        /// </summary>
        private static double Travel(DxfCurve curve, DxfCurve run, GeoPoint3 end, GeoVector3 way, double sweep, bool atStart, GeoPoint3 point)
        {
            const double Turn = 2.0 * Math.PI;

            switch (run)
            {
                case DxfRay _:
                    return end.GetVectorTo(point).DotProduct(way);
                case DxfCircle circle:
                    GeoVector3 normal = circle.Normal;
                    GeoVector3 from = circle.Center.GetVectorTo(end);
                    GeoVector3 to = circle.Center.GetVectorTo(point);
                    double sense = normal.DotProduct(from.CrossProduct(way)) >= 0.0 ? 1.0 : -1.0;
                    double angle = Math.Atan2(sense * normal.DotProduct(from.CrossProduct(to)), from.DotProduct(to));
                    angle = angle < 0.0 ? angle + Turn : angle;
                    return angle < Turn - sweep ? angle * circle.Radius : double.PositiveInfinity;
                case DxfEllipse whole:
                    var ellipse = (DxfEllipse)curve;
                    double q;

                    try
                    {
                        q = whole.GetParameterAtPoint(point);
                    }
                    catch (ArgumentException)
                    {
                        return double.PositiveInfinity;
                    }

                    double past = atStart ? ellipse.StartParam - q : q - ellipse.EndParam;
                    past -= Turn * Math.Floor(past / Turn);
                    return past < Turn - sweep ? past * ellipse.MajorRadius : double.PositiveInfinity;
                default:
                    return double.PositiveInfinity;
            }
        }

        /// <summary>
        /// BREAK: the part of an object between two points taken out, the first point where it was picked unless First point
        /// gives another, @ for the second the same point, which breaks it in two there; round a circle counter-clockwise from
        /// the first point to the second.
        /// </summary>
        private static IEnumerable<CadPrompt> Break(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select object");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];
            GeoPoint2 first = c.Input.Point;

            if (!CanChange(c, handle, "broken"))
            {
                yield break;
            }

            c.Session.LastPoint = first;

            // The part the second point at the cursor would take out, faded as TRIM's. AutoCAD previews the outcome
            // here too: its help for COMMANDPREVIEW names BREAK with TRIM, EXTEND and LENGTHEN.
            CadPreview Gap(GeoPoint2 from) => CadPreview.Fading(c, input =>
            {
                var gone = new List<DxfEntity>();
                return BreakAt(c, handle, from, input.Point, gone) ? gone : null;
            });

            yield return CadPrompt.ForPoint("Specify second break point", null, new[] { "First point" }).Previewing(Gap(first));

            if (c.Input.Is("First point"))
            {
                yield return CadPrompt.ForPoint("Specify first break point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second break point", first).Previewing(Gap(first));
            }

            if (c.Input.IsPoint)
            {
                Splits(c, "broken", () => BreakAt(c, handle, first, c.Input.Point));
            }
        }

        /// <summary>BREAKATPOINT: an object broken in two at a point, as BREAK with @ for its second point.</summary>
        private static IEnumerable<CadPrompt> BreakAtPoint(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select object");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (!CanChange(c, handle, "broken"))
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify break point");

            if (c.Input.IsPoint)
            {
                GeoPoint2 at = c.Input.Point;
                Splits(c, "broken", () => BreakAt(c, handle, at, at));
            }
        }

        /// <summary>
        /// Breaks an object between two points, or in two at one. Given a list to preview into, it adds the pieces the break
        /// takes out to it instead, and changes nothing; false where it takes none out, as a break at one point takes none.
        /// </summary>
        private static bool BreakAt(CadCommandContext c, string handle, GeoPoint2 first, GeoPoint2 second, List<DxfEntity> gone = null)
        {
            var curve = (DxfCurve)c.Document.FindEntity(handle);
            double eps = Slack(c);
            Span(curve, out double start, out double length);
            double? d1 = DistanceOf(curve, curve.GetClosestPointTo(P3(first), false));
            double? d2 = DistanceOf(curve, curve.GetClosestPointTo(P3(second), false));

            if (d1 == null || d2 == null)
            {
                return false;
            }

            IReadOnlyList<DxfCurve> pieces = null;
            var kept = new List<DxfCurve>();

            if (curve.Closed)
            {
                double a = d1.Value >= length - eps ? 0.0 : d1.Value;
                double b = d2.Value >= length - eps ? 0.0 : d2.Value;

                if (Math.Abs(a - b) <= eps)
                {
                    c.Write(curve is DxfCircle || curve is DxfEllipse ? "Arc cannot be full 360 degrees." : "A closed object is not broken at one point.");
                    return false;
                }

                pieces = curve.GetSplitCurves(new[] { curve.GetParameterAtDistance(a), curve.GetParameterAtDistance(b) });

                // The piece that runs from the first point to the second goes.
                GeoPoint3 from = curve.GetPointAtDist(a);
                kept.AddRange(pieces.Where(p => !p.StartPoint.IsEqualTo(from, c.Session.Tolerance)));

                if (kept.Count != 1)
                {
                    return false;
                }
            }
            else
            {
                double low = Math.Min(d1.Value, d2.Value);
                double high = Math.Max(d1.Value, d2.Value);
                bool lowInside = low > start + eps && low < length - eps;
                bool highInside = high > start + eps && high < length - eps;

                if (high - low <= eps)
                {
                    if (!lowInside)
                    {
                        return false;
                    }

                    kept.AddRange(curve.GetSplitCurves(new[] { curve.GetParameterAtDistance(low) }));
                }
                else
                {
                    var parameters = new List<double>();

                    if (lowInside)
                    {
                        parameters.Add(curve.GetParameterAtDistance(low));
                    }

                    if (highInside)
                    {
                        parameters.Add(curve.GetParameterAtDistance(high));
                    }

                    pieces = parameters.Count == 0 ? Array.Empty<DxfCurve>() : curve.GetSplitCurves(parameters);

                    if (lowInside)
                    {
                        kept.Add(pieces[0]);
                    }

                    if (highInside)
                    {
                        kept.Add(pieces[pieces.Count - 1]);
                    }
                }
            }

            if (gone != null)
            {
                if (pieces == null)
                {
                    return false;
                }

                // No cut inside the object takes all of it out.
                gone.AddRange(pieces.Count == 0 ? new[] { (DxfCurve)curve.Clone() } : pieces.Where(p => !kept.Contains(p)));
                return gone.Count > 0;
            }

            return Replace(c, handle, kept, "Break");
        }

        /// <summary>
        /// JOIN: a source object picked alone takes the objects joined to it by its own rules (a line collinear lines, an arc arcs
        /// of its circle, a polyline lines, arcs and polylines end to end); objects picked at once are joined as AutoCAD joins
        /// them: collinear lines into a line, arcs of one circle into an arc, and what meets end to end into a polyline, or into
        /// a spline where a spline or an elliptical arc is among them.
        /// </summary>
        private static IEnumerable<CadPrompt> Join(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select source object or multiple objects to join at once");

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            var picked = new List<string>(c.Input.Handles);
            string source = null;

            if (picked.Count == 1)
            {
                source = picked[0];
                yield return CadPrompt.ForSelection("Select objects to join");

                foreach (string handle in c.Input.Handles)
                {
                    if (!picked.Contains(handle, StringComparer.OrdinalIgnoreCase))
                    {
                        picked.Add(handle);
                    }
                }
            }

            int discarded = 0;
            var usable = new List<string>();

            foreach (string handle in picked)
            {
                if (CanChange(c, handle, "joined") && Joinable(c.Document.FindEntity(handle)))
                {
                    usable.Add(handle);
                }
                else
                {
                    discarded++;
                }
            }

            if (usable.Count < 2 || (source != null && !string.Equals(usable[0], source, StringComparison.OrdinalIgnoreCase)))
            {
                c.Write(discarded.ToString(CultureInfo.InvariantCulture) + (discarded == 1 ? " object" : " objects") + " discarded from the operation");
                yield break;
            }

            var said = new List<string>();
            var joinedAll = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CadEditResult result = c.Apply(source != null ? new[] { new CadEditRecord(CadEditRecordKind.Reshaped, source, "Join") } : null, s =>
            {
                CadSpace space = s.Document.FindSpace(usable[0]);
                var alive = new List<DxfCurve>(usable.Select(h => (DxfCurve)s.Document.FindEntity(h)));

                if (source != null)
                {
                    JoinInto(s, space, alive[0], alive, null, said, joinedAll);
                    return;
                }

                foreach (Type kind in new[] { typeof(DxfLine), typeof(DxfArc), typeof(DxfEllipse) })
                {
                    foreach (DxfCurve seed in alive.Where(e => e.GetType() == kind).ToList())
                    {
                        if (alive.Contains(seed))
                        {
                            JoinInto(s, space, seed, alive, kind, said, joinedAll);
                        }
                    }
                }

                // What is left joins end to end: into a spline where a spline or an elliptical arc is among it, else a polyline.
                foreach (DxfCurve seed in alive.Where(e => e is DxfSpline || e is DxfEllipse).Concat(alive.Where(e => e is DxfPolyline)).Concat(alive.Where(e => e is DxfLine || e is DxfArc)).ToList())
                {
                    if (!alive.Contains(seed))
                    {
                        continue;
                    }

                    bool spline = seed is DxfEllipse;

                    if (seed is DxfSpline || seed is DxfPolyline)
                    {
                        JoinInto(s, space, seed, alive, null, said, joinedAll);
                    }
                    else
                    {
                        DxfCurve made = spline ? seed.Spline : PolylineOf(seed);

                        if (made == null)
                        {
                            continue;
                        }

                        made.SetPropertiesFrom(seed);
                        var others = alive.Where(e => e != seed).Cast<DxfEntity>().ToList();
                        IReadOnlyList<int> taken = made.JoinEntities(others);

                        if (taken.Count == 0)
                        {
                            continue;
                        }

                        s.Append(space, made);
                        Gone(s, seed, alive, joinedAll);

                        foreach (int i in taken)
                        {
                            Gone(s, (DxfCurve)others[i], alive, joinedAll);
                        }

                        said.Add((taken.Count + 1).ToString(CultureInfo.InvariantCulture) + " objects converted to 1 " + (spline ? "spline" : "polyline"));
                    }
                }
            });

            if (!result.Succeeded)
            {
                yield break;
            }

            foreach (string line in said)
            {
                c.Write(line);
            }

            discarded += usable.Count(h => !joinedAll.Contains(h));

            if (said.Count == 0 || discarded > 0)
            {
                c.Write(discarded.ToString(CultureInfo.InvariantCulture) + (discarded == 1 ? " object" : " objects") + " discarded from the operation");
            }
        }

        /// <summary>Joins into a seed what of the others it takes (of one kind where given), erasing them, and says how many.</summary>
        private static void JoinInto(CadEditScope s, CadSpace space, DxfCurve seed, List<DxfCurve> alive, Type kind, List<string> said, HashSet<string> joinedAll)
        {
            var others = alive.Where(e => e != seed && (kind == null || e.GetType() == kind)).Cast<DxfEntity>().ToList();

            if (others.Count == 0)
            {
                return;
            }

            IReadOnlyList<int> taken = seed.JoinEntities(others);

            if (taken.Count == 0)
            {
                return;
            }

            joinedAll.Add(seed.Handle);
            alive.Remove(seed);
            s.Record(new CadEditRecord(CadEditRecordKind.Reshaped, seed.Handle, "Join"));
            bool same = true;

            foreach (int i in taken)
            {
                same &= others[i].GetType() == seed.GetType();
                Gone(s, (DxfCurve)others[i], alive, joinedAll);
            }

            string name = KindWord(seed);
            string count = (taken.Count + 1).ToString(CultureInfo.InvariantCulture);
            said.Add(same ? count + " " + name + "s joined into 1 " + name : count + " objects joined into 1 " + name);

            // The seed stays to join again end to end, as a line joined to a collinear one may still meet an arc.
            if (!(seed is DxfSpline))
            {
                alive.Add(seed);
                alive.Sort((a, b) => string.CompareOrdinal(a.Handle, b.Handle));
            }
        }

        private static void Gone(CadEditScope s, DxfCurve entity, List<DxfCurve> alive, HashSet<string> joinedAll)
        {
            joinedAll.Add(entity.Handle);
            alive.Remove(entity);
            s.Record(new CadEditRecord(CadEditRecordKind.Erased, entity.Handle));
            entity.Erase();
        }

        private static string KindWord(DxfCurve curve)
        {
            switch (curve)
            {
                case DxfLine _: return "line";
                case DxfArc _: return "arc";
                case DxfEllipse _: return "elliptical arc";
                case DxfSpline _: return "spline";
                default: return "polyline";
            }
        }

        /// <summary>Whether JOIN takes an object: an open line, arc, elliptical arc, polyline or spline.</summary>
        private static bool Joinable(DxfEntity entity)
            => (entity is DxfLine || entity is DxfArc || entity is DxfEllipse || entity is DxfPolyline || entity is DxfSpline) && !((DxfCurve)entity).Closed;

        /// <summary>
        /// A polyline of one segment that is a line or an arc lying flat (its normal along Z, either way), in no file, at the
        /// line's or the arc's height; null for one that does not lie flat.
        /// </summary>
        private static DxfPolyline PolylineOf(DxfCurve curve)
        {
            GeoPoint3 start = curve.StartPoint;
            GeoPoint3 end = curve.EndPoint;
            double bulge = 0.0;

            if (curve is DxfArc arc)
            {
                if (Math.Abs(Math.Abs(arc.Normal.Z) - 1.0) > 1E-12)
                {
                    return null;
                }

                bulge = Math.Tan(arc.TotalAngle / 4.0) * (arc.Normal.Z > 0.0 ? 1.0 : -1.0);
            }

            if (Math.Abs(start.Z - end.Z) > Tolerance.Default.EqualPoint)
            {
                return null;
            }

            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(start.X, start.Y), bulge, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(end.X, end.Y), 0.0, 0.0, 0.0);

            if (start.Z != 0.0)
            {
                polyline.Elevation = start.Z;
            }

            return polyline;
        }

        /// <summary>
        /// LENGTHEN: an object picked to measure says its length; DElta, Percent and Total set how an end changes, by a length
        /// or for an arc an angle, and DYnamic moves the end to a point; each object picked after is changed at its end nearer
        /// the pick, Undo taking the last back.
        /// </summary>
        private static IEnumerable<CadPrompt> Lengthen(CadCommandContext c)
        {
            string[] modes = { "DElta", "Percent", "Total", "DYnamic" };
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt.ForEntity("Select an object to measure", modes, modes[(int)c.Remembered("lengthen.mode", 2.0)]);

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    if (c.Document.FindEntity(c.Input.Handles[0]) is DxfCurve measured && !(measured is DxfRay) && !(measured is DxfXline))
                    {
                        Span(measured, out _, out double length);
                        c.Write("Current length: " + Length(length) + (measured is DxfArc arc ? ", included angle: " + Number(arc.TotalAngle * 180.0 / Math.PI) : string.Empty));
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                int mode = Array.IndexOf(modes, c.Input.Text);
                c.Remember("lengthen.mode", mode);
                bool angle = false;

                if (mode == 0)
                {
                    yield return CadPrompt.ForDistance("Enter delta length", null, new[] { "Angle" }, Length(c.Remembered("lengthen.delta", 0.0)));

                    if (c.Input.Is("Angle"))
                    {
                        yield return CadPrompt.ForAngle("Enter delta angle", null, null, Number(c.Remembered("lengthen.deltaAngle", 0.0) * 180.0 / Math.PI));
                        angle = true;

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            c.Remember("lengthen.deltaAngle", c.Input.Number);
                        }
                    }
                    else if (c.Input.Kind == CadInputKind.Number)
                    {
                        c.Remember("lengthen.delta", c.Input.Number);
                    }
                }
                else if (mode == 1)
                {
                    yield return CadPrompt.ForDistance("Enter percentage length", null, null, Length(c.Remembered("lengthen.percent", 100.0)));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        if (!(c.Input.Number > 0.0))
                        {
                            c.Write("Value must be positive and nonzero.");
                            continue;
                        }

                        c.Remember("lengthen.percent", c.Input.Number);
                    }
                }
                else if (mode == 2)
                {
                    yield return CadPrompt.ForDistance("Specify total length", null, new[] { "Angle" }, Length(c.Remembered("lengthen.total", 1.0)));

                    if (c.Input.Is("Angle"))
                    {
                        yield return CadPrompt.ForAngle("Specify total angle", null, null, Number(c.Remembered("lengthen.totalAngle", 1.0) * 180.0 / Math.PI));
                        angle = true;

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            c.Remember("lengthen.totalAngle", c.Input.Number);
                        }
                    }
                    else if (c.Input.Kind == CadInputKind.Number)
                    {
                        if (!(c.Input.Number > 0.0))
                        {
                            c.Write("Value must be positive and nonzero.");
                            continue;
                        }

                        c.Remember("lengthen.total", c.Input.Number);
                    }
                }

                var undo = new List<CadGroupMark>();

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select an object to change", undo.Count > 0 ? new[] { "Undo" } : null);

                    if (c.Input.Is("Undo"))
                    {
                        c.Document.BackTo(undo[undo.Count - 1]);
                        undo.RemoveAt(undo.Count - 1);
                        continue;
                    }

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    string handle = c.Input.Handles[0];
                    GeoPoint2 at = c.Input.Point;

                    if (!CanChange(c, handle, "lengthened"))
                    {
                        continue;
                    }

                    var curve = (DxfCurve)c.Document.FindEntity(handle);

                    if (curve.Closed || !(curve is DxfLine || curve is DxfArc || curve is DxfPolyline || curve is DxfEllipse || curve is DxfSpline))
                    {
                        c.Write("Cannot lengthen this object.");
                        continue;
                    }

                    Span(curve, out _, out double length);
                    bool atStart = (DistanceOf(curve, curve.GetClosestPointTo(P3(at), false)) ?? length) < length / 2.0;
                    CadGroupMark mark = c.Document.MarkGroup();
                    bool done;

                    if (mode == 3)
                    {
                        GeoPoint3 end = atStart ? curve.StartPoint : curve.EndPoint;
                        DxfCurve dragged = curve;
                        yield return CadPrompt.ForPoint("Specify new end point", new GeoPoint2(end.X, end.Y)).Previewing(CadPreview.Making(c, input =>
                        {
                            var preview = new List<DxfEntity>();
                            return MoveEnd(c, dragged, atStart, P3(input.Point), preview) ? preview : null;
                        }));

                        if (!c.Input.IsPoint)
                        {
                            yield break;
                        }

                        GeoPoint3 moved = P3(c.Input.Point);
                        done = Splits(c, "lengthened", () => MoveEnd(c, (DxfCurve)c.Document.FindEntity(handle), atStart, moved));
                    }
                    else
                    {
                        double target;

                        if (angle)
                        {
                            if (!(curve is DxfArc arc))
                            {
                                c.Write("The angle changes arcs only.");
                                continue;
                            }

                            target = arc.Radius * (mode == 0 ? arc.TotalAngle + c.Remembered("lengthen.deltaAngle", 0.0) : c.Remembered("lengthen.totalAngle", 1.0));
                        }
                        else
                        {
                            target = mode == 0 ? length + c.Remembered("lengthen.delta", 0.0) : mode == 1 ? length * c.Remembered("lengthen.percent", 100.0) / 100.0 : c.Remembered("lengthen.total", 1.0);
                        }

                        done = Splits(c, "lengthened", () => ToLength(c, curve, atStart, target));
                    }

                    if (done)
                    {
                        undo.Add(mark);
                    }
                }
            }
        }

        /// <summary>
        /// Makes a curve a length by its end at one side, run on or cut back; false, with the reason written, where it is not.
        /// Given a list to preview into, it adds the curve as it would be to it instead, and changes nothing.
        /// </summary>
        private static bool ToLength(CadCommandContext c, DxfCurve curve, bool atStart, double target, List<DxfEntity> preview = null)
        {
            double eps = Slack(c);
            Span(curve, out _, out double length);
            string handle = curve.Handle;

            if (!(target > eps))
            {
                c.Write("The object would be shortened to nothing.");
                return false;
            }

            if (target < length - eps)
            {
                IReadOnlyList<DxfCurve> pieces = curve.GetSplitCurves(new[] { curve.GetParameterAtDistance(atStart ? length - target : target) });

                if (pieces.Count == 2 && preview != null)
                {
                    preview.Add(pieces[atStart ? 1 : 0]);
                    return true;
                }

                return pieces.Count == 2 && Replace(c, handle, new List<DxfCurve> { pieces[atStart ? 1 : 0] }, "Lengthen");
            }

            if (target <= length + eps)
            {
                return false;
            }

            double more = target - length;
            double parameter;

            switch (curve)
            {
                case DxfLine _:
                    parameter = atStart ? -more : target;
                    break;
                case DxfArc arc:
                    parameter = atStart ? arc.StartParam - more / arc.Radius : arc.EndParam + more / arc.Radius;
                    break;
                case DxfPolyline polyline:
                    int segment = atStart ? 0 : polyline.NumberOfVertices - 2;
                    double run = polyline.GetDistanceAtParameter(segment + 1) - polyline.GetDistanceAtParameter(segment);

                    if (!(run > eps))
                    {
                        c.Write("Cannot lengthen this object: its end segment has no length.");
                        return false;
                    }

                    parameter = atStart ? -more / run : polyline.EndParam + more / run;
                    break;
                case DxfEllipse ellipse:
                    parameter = EllipseReach(ellipse, atStart, more);
                    break;
                default:
                    c.Write("Cannot lengthen this object: a spline is shortened, not run on.");
                    return false;
            }

            var tried = (DxfCurve)curve.Clone();

            try
            {
                tried.Extend(parameter);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException)
            {
                c.Write("Cannot lengthen this object: " + exception.Message);
                return false;
            }

            if (preview != null)
            {
                preview.Add(tried);
                return true;
            }

            return c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Lengthen") }, s => ((DxfCurve)s.Document.FindEntity(handle)).Extend(parameter)).Succeeded;
        }

        /// <summary>
        /// The parameter an elliptical arc reaches run on a length past an end round its ellipse, found by halving: the arc's
        /// length grows with its parameter, and short of the whole turn it cannot reach.
        /// </summary>
        private static double EllipseReach(DxfEllipse ellipse, bool atStart, double more)
        {
            var whole = new DxfEllipse(ellipse.Center, ellipse.Normal, ellipse.MajorAxis, ellipse.RadiusRatio, 0.0, 2.0 * Math.PI);
            double turn = 2.0 * Math.PI;
            double total = whole.GetDistanceAtParameter(turn);
            double from = atStart ? ellipse.StartParam : ellipse.EndParam;
            double low = 0.0;
            double high = turn - (ellipse.EndParam - ellipse.StartParam);

            // The distance round the ellipse from the end over a step of parameter, the step taken the way the end runs.
            Func<double, double> along = step =>
            {
                double a = atStart ? from - step : from;
                a -= turn * Math.Floor(a / turn);
                double b = a + step;
                return b <= turn ? whole.GetDistanceAtParameter(b) - whole.GetDistanceAtParameter(a) : total - whole.GetDistanceAtParameter(a) + whole.GetDistanceAtParameter(b - turn);
            };

            for (int i = 0; i < 100 && high - low > 1E-15; i++)
            {
                double middle = 0.5 * (low + high);

                if (along(middle) < more)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            double reach = 0.5 * (low + high);
            return atStart ? from - reach : from + reach;
        }

        /// <summary>
        /// LENGTHEN DYnamic: the end moved to the point of its line, circle or ellipse nearest a point, run on or cut back. Given
        /// a list to preview into, it adds the curve as it would be to it instead, and changes nothing, as the end previews
        /// following the cursor.
        /// </summary>
        private static bool MoveEnd(CadCommandContext c, DxfCurve curve, bool atStart, GeoPoint3 point, List<DxfEntity> preview = null)
        {
            double eps = Slack(c);
            DxfCurve run = RunOn(c, curve, atStart, out _, out _);
            GeoPoint3 target = run is DxfRay ray ? new DxfLine(ray.BasePoint, ray.BasePoint.Add(ray.UnitDir)).GetClosestPointTo(point, true) : run.GetClosestPointTo(point, false);

            if (curve.GetClosestPointTo(target, false).DistanceTo(target) <= eps)
            {
                Span(curve, out _, out double length);
                double at = DistanceOf(curve, target) ?? length;
                return ToLength(c, curve, atStart, atStart ? length - at : at, preview);
            }

            string handle = curve.Handle;

            var moved = (DxfCurve)curve.Clone();

            try
            {
                moved.Extend(atStart, target);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException)
            {
                c.Write("Cannot lengthen this object: " + exception.Message);
                return false;
            }

            if (preview != null)
            {
                preview.Add(moved);
                return true;
            }

            return c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Lengthen") }, s => ((DxfCurve)s.Document.FindEntity(handle)).Extend(atStart, target)).Succeeded;
        }

        /// <summary>
        /// Puts the pieces kept of a curve in its place, one undo record: a line, an arc or a ray keeps its handle with the
        /// first piece where that is of its kind, as AutoCAD keeps the object; otherwise it is erased and the pieces added.
        /// </summary>
        private static bool Replace(CadCommandContext c, string handle, List<DxfCurve> kept, string how)
        {
            DxfEntity entity = c.Document.FindEntity(handle);
            CadSpace space = c.Document.FindSpace(handle);
            bool inPlace = kept.Count > 0 && kept[0].GetType() == entity.GetType() && (entity is DxfLine || entity is DxfArc || entity is DxfRay);
            var planned = new[] { new CadEditRecord(inPlace ? CadEditRecordKind.Reshaped : CadEditRecordKind.Erased, handle, inPlace ? how : null) };

            return c.Apply(planned, s =>
            {
                DxfEntity target = s.Document.FindEntity(handle);

                if (inPlace)
                {
                    switch (target)
                    {
                        case DxfLine line:
                            line.StartPoint = kept[0].StartPoint;
                            line.EndPoint = kept[0].EndPoint;
                            break;
                        case DxfArc arc:
                            arc.StartAngle = ((DxfArc)kept[0]).StartAngle;
                            arc.EndAngle = ((DxfArc)kept[0]).EndAngle;
                            break;
                        case DxfRay ray:
                            ray.BasePoint = ((DxfRay)kept[0]).BasePoint;
                            break;
                    }
                }
                else
                {
                    target.Erase();
                }

                for (int i = inPlace ? 1 : 0; i < kept.Count; i++)
                {
                    s.Append(space, kept[i]);
                }
            }).Succeeded;
        }

        /// <summary>
        /// Whether an object may be cut, run on or joined: a curve, on no locked layer, of no owner; false with the reason
        /// written, as AutoCAD writes it, or as the owner gate words one of a model drawing.
        /// </summary>
        private static bool CanChange(CadCommandContext c, string handle, string done)
        {
            DxfEntity entity = c.Document.FindEntity(handle);

            if (entity == null)
            {
                return false;
            }

            if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
            {
                c.Write("Object is on a locked layer.");
                return false;
            }

            if (c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
            {
                c.Write("This " + (entity is DxfDimension ? "dimension" : CadOwnerEditGate.Describe(entity)) + " belongs to the model and cannot be " + done + " here.");
                return false;
            }

            if (!(entity is DxfCurve) && done != "erased")
            {
                c.Write("This " + CadOwnerEditGate.Describe(entity) + " cannot be " + done + ".");
                return false;
            }

            return true;
        }

        /// <summary>The objects near a box that may be edges, in ordinal order of their handles: those the pick index draws there, and every xline and ray; or of a list given.</summary>
        private static List<DxfEntity> Near(CadCommandContext c, DisplayBox box, string except, IReadOnlyList<string> only)
        {
            var handles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            if (only != null)
            {
                handles.UnionWith(only);
            }
            else
            {
                foreach (DisplayItem2 item in c.Document.GetPickIndex(c.Space).ItemsNear(box))
                {
                    handles.Add(item.Handle);
                }

                foreach (DxfEntity entity in c.Space.Block)
                {
                    if ((entity is DxfXline || entity is DxfRay) && !string.IsNullOrEmpty(entity.Handle))
                    {
                        handles.Add(entity.Handle);
                    }
                }
            }

            if (except != null)
            {
                handles.Remove(except);
            }

            var found = new List<DxfEntity>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && !entity.IsErased)
                {
                    found.Add(entity);
                }
            }

            return found;
        }

        /// <summary>The points where a curve meets an object, the object run on past its ends where asked; none for an object the library meets nothing of.</summary>
        private static IReadOnlyList<GeoPoint3> Meet(DxfEntity curve, DxfEntity other, bool extendOther)
        {
            try
            {
                return curve.IntersectWith(other, extendOther ? DxfIntersect.ExtendArgument : DxfIntersect.OnBothOperands);
            }
            catch (Exception exception) when (exception is NotSupportedException || exception is InvalidOperationException || exception is ArgumentException)
            {
                return Array.Empty<GeoPoint3>();
            }
        }

        /// <summary>The box a curve is drawn in, a little larger; for one without end, the box of all the space draws and its base point.</summary>
        private static DisplayBox BoxOf(CadCommandContext c, DxfCurve curve)
        {
            double eps = Slack(c);

            if (curve is DxfRay || curve is DxfXline)
            {
                GeoPoint3 start = curve is DxfRay ray ? ray.BasePoint : ((DxfXline)curve).BasePoint;
                DisplayBox all = c.Document.GetDisplay(c.Space).Bounds.Add(start.X, start.Y);
                return all.Inflate(eps + all.Width + all.Height);
            }

            GeoAabb3 box = curve.GeometricExtents;
            return new DisplayBox(box.Min.X, box.Min.Y, box.Max.X, box.Max.Y).Inflate(eps);
        }

        /// <summary>The distance along a curve from its start to a point of it; null where the point is not on it.</summary>
        private static double? DistanceOf(DxfCurve curve, GeoPoint3 point)
        {
            try
            {
                return curve.GetDistAtPoint(point);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Runs a cut, saying so where the library does not split or run on a curve of the kind.</summary>
        private static bool Splits(CadCommandContext c, string done, Func<bool> cut)
        {
            try
            {
                return cut();
            }
            catch (NotSupportedException)
            {
                c.Write("This object cannot be " + done + ".");
                return false;
            }
        }

        /// <summary>
        /// The distances along a curve at its start and at its end, as <see cref="DxfCurve.GetDistAtPoint"/> measures them: from
        /// nought to its length for most, on without end for a ray, and from without end for an xline, measured from its base point.
        /// </summary>
        private static void Span(DxfCurve curve, out double start, out double length)
        {
            start = curve is DxfXline ? double.NegativeInfinity : 0.0;
            length = curve is DxfRay || curve is DxfXline ? double.PositiveInfinity : curve.GetDistanceAtParameter(curve.EndParam);
        }

        /// <summary>How near two points are the same: the session's point tolerance, and never finer than the library's, which splits and meets curves.</summary>
        private static double Slack(CadCommandContext c) => Math.Max(c.Session.Tolerance.EqualPoint, Tolerance.Default.EqualPoint);
    }
}
