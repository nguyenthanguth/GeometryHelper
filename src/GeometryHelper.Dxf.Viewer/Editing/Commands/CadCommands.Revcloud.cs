using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>REVCLOUD, as AutoCAD 2021 asks it and stores the cloud (checked against clouds AutoCAD made in its own console).</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] RevCloudOptions =
        {
            "Arc length",
            "Object",
            "Rectangular",
            "Polygonal",
            "Freehand",
            "Style",
            "Modify",
        };

        /// <summary>The bulge of every arc of a cloud, as AutoCAD writes it: an arc of 110 degrees, tan(27.5 degrees).</summary>
        private static readonly double RevCloudBulge = Math.Tan(110.0 / 4.0 * Math.PI / 180.0);

        /// <summary>
        /// REVCLOUD: a cloud of arcs drawn round a rectangle by two corners, a polygon by its points, a path traced with the
        /// cursor, or the outline of an object picked, which it replaces; Modify redraws part of a cloud. The cloud is a
        /// LWPOLYLINE whose vertices lie an arc length apart along the path, each segment an arc of 110 degrees (bulge
        /// 0.52056705) bulging out, a last arc shorter than half an arc length taken into the one before; Calligraphy gives
        /// each arc a width from nought to a tenth of the longest arc, and it carries the data RevcloudProps AutoCAD 2021
        /// marks its clouds with. The approximate arc length is AutoCAD 2021's REVCLOUDAPPROXARCLEN, kept between runs and not
        /// scaled by DIMSCALE, a twentieth of the view's diagonal while it is nought: REVCLOUDARCVARIANCE 1 draws each arc
        /// between two thirds of it and four thirds, 0 each of it. All of it as AutoCAD 2021's console runs it interactively.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloud(CadCommandContext c)
        {
            string[] types = { "Freehand", "Rectangular", "Polygonal" };
            bool header = true;

            while (true)
            {
                double approximate = RevCloudLength(c);
                bool varies = c.Remembered("revcloud.variance", 1.0) != 0.0;
                double least = varies ? approximate * 2.0 / 3.0 : approximate;
                double most = varies ? approximate * 4.0 / 3.0 : approximate;
                bool calligraphy = c.Remembered("revcloud.style", 0.0) != 0.0;
                int type = (int)c.Remembered("revcloud.type", 1.0);

                // AutoCAD says the lengths, style and type when the command starts and again when the type is changed, not after
                // Arc length or Style.
                if (header)
                {
                    c.Write(
                        "Minimum arc length: "
                            + Length(least)
                            + "   Maximum arc length: "
                            + Length(most)
                            + "   Style: "
                            + (calligraphy ? "Calligraphy" : "Normal")
                            + "   Type: "
                            + types[type]
                    );
                    header = false;
                }

                string ask =
                    type == 1 ? "Specify first corner point"
                    : type == 2 ? "Specify start point"
                    : "Specify first point";
                yield return CadPrompt.ForPoint(ask, null, RevCloudOptions, "Object");

                if (c.Input.Is("Arc length"))
                {
                    foreach (CadPrompt prompt in RevCloudArcLength(c, approximate))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Style"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Select arc style",
                        new[] { "Normal", "Calligraphy" },
                        calligraphy ? "Calligraphy" : "Normal"
                    );

                    if (c.Input.Is("Normal") || c.Input.Is("Calligraphy"))
                    {
                        c.Remember("revcloud.style", c.Input.Is("Calligraphy") ? 1.0 : 0.0);
                        c.Write(c.Input.Text);
                    }

                    continue;
                }

                if (c.Input.Is("Rectangular") || c.Input.Is("Polygonal") || c.Input.Is("Freehand"))
                {
                    int chosen =
                        c.Input.Is("Rectangular") ? 1
                        : c.Input.Is("Polygonal") ? 2
                        : 0;
                    c.Remember("revcloud.type", chosen);
                    header = chosen != type;
                    continue;
                }

                var cloud = new RevCloudLook(least, most, calligraphy, approximate);

                if (c.Input.Is("Object") || c.Input.IsNone)
                {
                    foreach (CadPrompt prompt in RevCloudObject(c, cloud))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (c.Input.Is("Modify"))
                {
                    foreach (CadPrompt prompt in RevCloudModify(c, cloud))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                IEnumerable<CadPrompt> drawing =
                    type == 1 ? RevCloudRectangle(c, cloud, first)
                    : type == 2 ? RevCloudPolygon(c, cloud, first)
                    : RevCloudFreehand(c, cloud, first);

                foreach (CadPrompt prompt in drawing)
                {
                    yield return prompt;
                }

                yield break;
            }
        }

        /// <summary>The application AutoCAD 2021 marks the clouds of REVCLOUD with: a style (nought) and the approximate arc length.</summary>
        private const string RevCloudApplication = "RevcloudProps";

        /// <summary>
        /// The share of the view's diagonal AutoCAD 2021 takes for the approximate arc length of the first cloud, while
        /// REVCLOUDAPPROXARCLEN is nought: a twentieth, measured in its console on views of 942 by 534 pixels and smaller ones (a
        /// twentieth of the diagonal of a view 100 high, 10.1392, was 10.1277 there, a tenth of a per cent less).
        /// </summary>
        private const double RevCloudViewShare = 0.05;

        /// <summary>
        /// The approximate arc length a cloud is drawn with: REVCLOUDAPPROXARCLEN where it is set, else a twentieth of the
        /// diagonal of the view in the drawing's units, as AutoCAD 2021 sizes its first cloud (a view 100 units high, 942 by 534
        /// pixels, gives 10.1392); where there is no view, as in a test, 15 in a metric drawing and 0.5 in an imperial one.
        /// </summary>
        private static double RevCloudLength(CadCommandContext c)
        {
            double kept = c.Remembered("revcloud.approx", 0.0);

            if (kept > 0.0)
            {
                return kept;
            }

            CadView view = c.Session.WorkView;

            if (view != null)
            {
                return RevCloudViewShare
                    * Math.Sqrt(view.Width * view.Width + view.Height * view.Height)
                    / view.PixelsPerUnit;
            }

            return c.Database.Measurement == DxfMeasurementValue.Metric ? 15.0 : 0.5;
        }

        /// <summary>Marks a polyline as a cloud of an approximate arc length, as AutoCAD 2021's REVCLOUD does, so that REVCLOUDPROPERTIES finds it.</summary>
        private static void RevCloudMark(DxfPolyline cloud, double approximate)
        {
            cloud.XData = new[]
            {
                new DxfTypedValue(1001, RevCloudApplication),
                new DxfTypedValue(1070, "0"),
                new DxfTypedValue(1040, approximate.ToString("R", CultureInfo.InvariantCulture)),
            };
        }

        /// <summary>Arc length: "Specify approximate length of arc", above nought, kept as REVCLOUDAPPROXARCLEN, as AutoCAD 2021 asks it.</summary>
        private static IEnumerable<CadPrompt> RevCloudArcLength(CadCommandContext c, double approximate)
        {
            while (true)
            {
                yield return CadPrompt.ForDistance(
                    "Specify approximate length of arc",
                    null,
                    null,
                    Length(approximate)
                );
                double length = c.Input.Kind == CadInputKind.Number ? c.Input.Number : approximate;

                if (length > 0.0)
                {
                    c.Remember("revcloud.approx", length);
                    yield break;
                }

                c.Write("Value must be positive and nonzero.");
            }
        }

        /// <summary>Object: a line, arc, circle, ellipse, polyline or spline picked becomes a cloud along it, in its place.</summary>
        private static IEnumerable<CadPrompt> RevCloudObject(CadCommandContext c, RevCloudLook cloud)
        {
            string handle = null;

            while (handle == null)
            {
                yield return CadPrompt.ForEntity("Select object");

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    continue;
                }

                DxfEntity entity = c.Document.FindEntity(c.Input.Handles[0]);

                if (!(entity is DxfCurve) || entity is DxfRay || entity is DxfXline || entity is DxfPolyline3d)
                {
                    c.Write("Unable to convert object, must be a polyline, spline, or circle.");
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    c.Write("Object on locked layer.");
                    continue;
                }

                handle = entity.Handle;
            }

            var curve = (DxfCurve)c.Document.FindEntity(handle);
            List<GeoPoint2> points = RevCloudAlong(curve, curve.Closed, cloud);

            if (points.Count < (curve.Closed ? 3 : 2))
            {
                c.Write("Unable to convert object, object is too small for the cloud's arcs.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Reverse direction", new[] { "Yes", "No" }, "No");
            bool reverse = c.Input.Is("Yes");
            CadSpace space = c.Document.FindSpace(handle);
            double elevation = curve.StartPoint.Z;
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) }, s =>
            {
                DxfEntity own = s.Document.FindEntity(handle);
                DxfPolyline made = RevCloudPolyline(points, curve.Closed, cloud, reverse);
                made.SetPropertiesFrom(own);
                made.Elevation = elevation;
                own.Erase();
                s.Append(space, made);
            });

            if (result.Succeeded)
            {
                c.Write("Revision cloud finished.");
            }
        }

        /// <summary>
        /// Rectangular: a cloud round the rectangle of two corners, from the first along the side of its X to the Y of the second,
        /// as AutoCAD 2021 draws it: the corners (0, 0) and (100, 60) give a cloud going up the left side, across the top and down
        /// the right, clockwise, and the corners (0, 60) and (100, 0) one going down the left side, counter-clockwise.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudRectangle(
            CadCommandContext c,
            RevCloudLook cloud,
            GeoPoint2 first
        )
        {
            List<GeoPoint2> Corners(GeoPoint2 second) =>
                new List<GeoPoint2>
                {
                    first,
                    new GeoPoint2(first.X, second.Y),
                    second,
                    new GeoPoint2(second.X, first.Y),
                };
            yield return CadPrompt
                .ForPoint("Specify opposite corner", first)
                .Previewing(RevCloudPreview(c, input => RevCloudOf(cloud, Corners(input.Point), true, false)));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            RevCloudPath(c, cloud, Corners(c.Input.Point), true, false);
        }

        /// <summary>Polygonal: a cloud round the polygon of the points given until Enter; Undo takes the last point back.</summary>
        private static IEnumerable<CadPrompt> RevCloudPolygon(CadCommandContext c, RevCloudLook cloud, GeoPoint2 first)
        {
            var corners = new List<GeoPoint2> { first };

            // The cloud round the corners given and the point at the cursor, once they make a polygon.
            CadPreview shown = RevCloudPreview(c, input =>
                corners.Count < 2 ? null : RevCloudOf(cloud, new List<GeoPoint2>(corners) { input.Point }, true, false)
            );

            while (true)
            {
                if (corners.Count < 2)
                {
                    yield return CadPrompt.ForPoint("Specify next point", corners[corners.Count - 1]);
                }
                else
                {
                    yield return CadPrompt
                        .ForPoint("Specify next point", corners[corners.Count - 1], new[] { "Undo" })
                        .Previewing(shown);
                }

                if (c.Input.Is("Undo"))
                {
                    corners.RemoveAt(corners.Count - 1);
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    break;
                }

                corners.Add(c.Input.Point);
            }

            if (corners.Count < 3)
            {
                c.Write("Invalid shape to create revision cloud.");
                yield break;
            }

            RevCloudPath(c, cloud, corners, true, false);
        }

        /// <summary>
        /// Freehand: the cloud follows the cursor, a vertex put down each time it is an arc length from the last; it closes when
        /// the cursor comes back within an arc length of the start, and Enter ends it open, asking which way it bulges.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudFreehand(CadCommandContext c, RevCloudLook cloud, GeoPoint2 first)
        {
            c.Write("Guide crosshairs along cloud path...");
            var vertices = new List<GeoPoint2> { first };
            var random = new Random(0);
            double step = cloud.Next(random);
            bool closed = false;

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Guide crosshairs along cloud path...", vertices[vertices.Count - 1])
                    .WithoutColon()
                    .Tracking()
                    .Previewing(
                        RevCloudPreview(c, input =>
                            vertices.Count < 2 ? null : RevCloudPolyline(vertices, false, cloud, false)
                        )
                    );

                if (!c.Input.IsPoint)
                {
                    break;
                }

                GeoPoint2 at = c.Input.Point;

                if (vertices.Count >= 3 && (at - first).Length <= step)
                {
                    closed = true;
                    break;
                }

                if ((at - vertices[vertices.Count - 1]).Length >= step)
                {
                    vertices.Add(at);
                    step = cloud.Next(random);
                }
            }

            if (vertices.Count < (closed ? 3 : 2))
            {
                c.Write("Invalid shape to create revision cloud.");
                yield break;
            }

            bool reverse = false;

            if (!closed)
            {
                yield return CadPrompt.ForKeyword("Reverse direction", new[] { "Yes", "No" }, "No");
                reverse = c.Input.Is("Yes");
            }

            RevCloudAdd(c, RevCloudPolyline(vertices, closed, cloud, reverse), true);
        }

        /// <summary>
        /// Modify: a closed cloud picked is redrawn between where it was picked and where the new path given comes back to it;
        /// the side picked is erased and the new path clouded in its place, one closed cloud again, as AutoCAD 2021 writes it:
        /// beginning at the end of the path, its arcs bulging the way the old cloud's do, or the other way where
        /// "Reverse direction" is Yes.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudModify(CadCommandContext c, RevCloudLook cloud)
        {
            yield return CadPrompt.ForEntity("Select polyline to modify");

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (
                !(c.Document.FindEntity(handle) is DxfPolyline old)
                || !old.Closed
                || Math.Abs(old.Normal.Z - 1.0) > Tolerance.Default.EqualVector
            )
            {
                c.Write("Invalid object selected.");
                yield break;
            }

            if (!CanChange(c, handle, "modified"))
            {
                yield break;
            }

            GeoPoint2 start = RevCloudOn(old, c.Input.Point);
            yield return CadPrompt.ForPoint("Specify next point", start, new[] { "First point" });

            if (c.Input.Is("First point"))
            {
                yield return CadPrompt.ForPoint("Specify a start point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                start = RevCloudOn(old, c.Input.Point);
                yield return CadPrompt.ForPoint("Specify next point", start);
            }

            var path = new List<GeoPoint2> { start };

            while (c.Input.IsPoint || c.Input.Is("Undo"))
            {
                if (c.Input.Is("Undo"))
                {
                    if (path.Count > 1)
                    {
                        path.RemoveAt(path.Count - 1);
                    }
                }
                else
                {
                    // A point on the cloud, within the pickbox, ends the path there.
                    GeoPoint2 on = RevCloudOn(old, c.Input.Point);

                    if (path.Count > 1 && (on - c.Input.Point).Length <= c.Session.PickAperture)
                    {
                        path.Add(on);
                        break;
                    }

                    path.Add(c.Input.Point);
                }

                yield return CadPrompt.ForPoint("Specify next point", path[path.Count - 1], new[] { "Undo" });
            }

            // Enter ends the command with nothing changed, as AutoCAD 2021 does; a point on the cloud has ended the path.
            if (path.Count < 2 || !c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 end = path[path.Count - 1];
            double from = old.GetParameterAtPoint(P3(start));
            double to = old.GetParameterAtPoint(P3(end));

            if (Math.Abs(to - from) <= 1E-9)
            {
                c.Write("Invalid shape to create revision cloud.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Pick a side to erase");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            double picked = old.GetParameterAtPoint(old.GetClosestPointTo(P3(c.Input.Point), false));
            yield return CadPrompt.ForKeyword("Reverse direction", new[] { "Yes", "No" }, "No");
            bool reverse = c.Input.Is("Yes");

            // The cloud is cut where the new path starts (S) and ends (E) into the way on from S round to E and the way on from E
            // round to S; the one the side picked lies on is erased, the new arcs, laid from S along the path, taking its place.
            // The cloud then starts at E, as AutoCAD 2021 writes it: E, the new arcs, S and the way on from S where the way
            // from E to S was erased; the way from E to S and the new arcs from S on where the way from S to E was.
            bool erasesFromStart = RevCloudWithin(from, to, picked, old.EndParam);
            var line = new DxfPolyline();

            for (int i = 0; i < path.Count; i++)
            {
                line.AddVertexAt(i, path[i], 0.0, 0.0, 0.0);
            }

            List<GeoPoint2> arcs = RevCloudAlong(line, false, cloud);
            List<PeditPoint> kept = erasesFromStart ? RevCloudPart(old, to, from) : RevCloudPart(old, from, to);
            double side = RevCloudSide(old);

            if (side == 0.0)
            {
                // A polyline of lines has no arcs to take the way they bulge from: out of the outline.
                var outline = kept.Select(p => p.At).Concat(arcs.Skip(1).Take(arcs.Count - 2)).ToList();
                side = RevCloudArea(outline) > 0.0 ? 1.0 : -1.0;
            }

            double bulge = side * (reverse ? -1.0 : 1.0) * RevCloudBulge;
            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Modify") },
                s =>
                {
                    var own = (DxfPolyline)s.Document.FindEntity(handle);
                    var points = new List<PeditPoint>();

                    PeditPoint Arc(GeoPoint2 at) =>
                        new PeditPoint
                        {
                            At = at,
                            Bulge = bulge,
                            Start = 0.0,
                            End = cloud.Width,
                        };

                    foreach (PeditPoint p in kept)
                    {
                        p.Bulge *= reverse ? -1.0 : 1.0;
                    }

                    if (erasesFromStart)
                    {
                        // The way from E to S, its closing vertex S taking the first new arc, then the new arcs from S on.
                        points.AddRange(kept.Take(kept.Count - 1));
                        points.Add(Arc(kept[kept.Count - 1].At));

                        for (int i = 1; i < arcs.Count - 1; i++)
                        {
                            points.Add(Arc(arcs[i]));
                        }
                    }
                    else
                    {
                        // E, the new arcs from the end back to S, then the way from S round to E, its closing vertex E left out.
                        points.Add(Arc(end));

                        for (int i = arcs.Count - 2; i >= 1; i--)
                        {
                            points.Add(Arc(arcs[i]));
                        }

                        points.AddRange(kept.Take(kept.Count - 1));
                    }

                    PeditWrite(own, points, true, DxfPoly2dType.SimplePoly);
                }
            );
        }

        /// <summary>The way the arcs of a cloud bulge: above nought where the first arc bulges one way, below where the other; nought for a polyline with no arcs.</summary>
        private static double RevCloudSide(DxfPolyline cloud)
        {
            for (int i = 0; i < cloud.NumberOfVertices; i++)
            {
                double bulge = cloud.GetBulgeAt(i);

                if (bulge != 0.0)
                {
                    return Math.Sign(bulge);
                }
            }

            return 0.0;
        }

        /// <summary>The approximate arc length a cloud was drawn with, as its REVCLOUD data hold it; null for a polyline that is no cloud.</summary>
        private static double? RevCloudStored(DxfEntity entity)
        {
            IReadOnlyList<DxfTypedValue> data = (entity as DxfPolyline)?.GetXDataForApplication(RevCloudApplication);

            if (data == null)
            {
                return null;
            }

            foreach (DxfTypedValue value in data)
            {
                if (
                    value.TypeCode == 1040
                    && double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double length)
                )
                {
                    return length;
                }
            }

            return null;
        }

        /// <summary>
        /// REVCLOUDPROPERTIES, as AutoCAD 2021 asks it: "Select objects:" for revision clouds (what is no cloud is not counted),
        /// "Select property to change [Arc length]:" and "Specify approximate length of arc &lt;10.0000&gt;:", the length the first
        /// cloud was drawn with. Each cloud selected is drawn again along the polygon of its own vertices from its first, with
        /// arcs of the new length, bulging the way its own do, in place; REVCLOUDAPPROXARCLEN is set to the length. A rectangle
        /// of arcs 10 long, given 20, is the cloud REVCLOUD draws with 20; a polygon cloud is drawn along the chords of its
        /// arcs, which cut its corners, not along the polygon it was drawn round.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudProperties(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select objects", entity => RevCloudStored(entity).HasValue))
            {
                yield return prompt;
            }

            List<DxfPolyline> clouds = c
                .Selected.Select(h => c.Document.FindEntity(h))
                .Where(e => RevCloudStored(e).HasValue)
                .Cast<DxfPolyline>()
                .ToList();

            if (clouds.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword("Select property to change", new[] { "Arc length" }, null);

            if (!c.Input.Is("Arc length"))
            {
                yield break;
            }

            foreach (CadPrompt prompt in RevCloudArcLength(c, RevCloudStored(clouds[0]).Value))
            {
                yield return prompt;
            }

            double approximate = c.Remembered("revcloud.approx", 0.0);
            bool varies = c.Remembered("revcloud.variance", 1.0) != 0.0;
            double least = varies ? approximate * 2.0 / 3.0 : approximate;
            double most = varies ? approximate * 4.0 / 3.0 : approximate;

            c.Apply(
                clouds.Select(p => new CadEditRecord(CadEditRecordKind.Reshaped, p.Handle, "Arc length")).ToList(),
                s =>
                {
                    foreach (DxfPolyline old in clouds)
                    {
                        var own = (DxfPolyline)s.Document.FindEntity(old.Handle);
                        bool calligraphy = Enumerable
                            .Range(0, own.NumberOfVertices)
                            .Any(i => own.GetEndWidthAt(i) > 0.0);
                        var look = new RevCloudLook(least, most, calligraphy, approximate);
                        var path = new DxfPolyline();

                        for (int i = 0; i < own.NumberOfVertices; i++)
                        {
                            path.AddVertexAt(i, own.GetPoint2dAt(i), 0.0, 0.0, 0.0);
                        }

                        path.Closed = own.Closed;
                        List<GeoPoint2> along = RevCloudAlong(path, own.Closed, look);

                        if (along.Count < (own.Closed ? 3 : 2))
                        {
                            continue;
                        }

                        double bulge = RevCloudSide(own) * RevCloudBulge;
                        var points = new List<PeditPoint>();

                        foreach (GeoPoint2 at in along)
                        {
                            points.Add(
                                new PeditPoint
                                {
                                    At = at,
                                    Bulge = bulge,
                                    Start = 0.0,
                                    End = look.Width,
                                }
                            );
                        }

                        PeditWrite(own, points, own.Closed, DxfPoly2dType.SimplePoly);
                        RevCloudMark(own, approximate);
                    }
                }
            );
        }

        /// <summary>Whether a parameter lies on the way from one to another, forward round a closed polyline of a count of segments.</summary>
        private static bool RevCloudWithin(double from, double to, double at, double count)
        {
            double span = (to - from + count) % count;
            double reach = (at - from + count) % count;
            return reach <= span;
        }

        /// <summary>
        /// The part of a closed polyline from one parameter forward round to another, as vertices: the vertices between, the
        /// arcs at either end cut to the part they keep, the last vertex closing the part.
        /// </summary>
        private static List<PeditPoint> RevCloudPart(DxfPolyline polyline, double from, double to)
        {
            int n = polyline.NumberOfVertices;
            double span = (to - from + n) % n;
            var part = new List<PeditPoint>();
            double at = from;
            double left = span;

            while (left > 1E-12)
            {
                int segment = (int)Math.Floor(at) % n;
                double into = at - Math.Floor(at);
                double reach = Math.Min(1.0 - into, left);
                double bulge = polyline.GetBulgeAt(segment);
                double sweep = 4.0 * Math.Atan(bulge) * reach;
                part.Add(
                    new PeditPoint
                    {
                        At = RevCloudFlat(polyline.GetPointAtParameter(segment + into)),
                        Bulge = Math.Tan(sweep / 4.0),
                        Start = polyline.GetStartWidthAt(segment),
                        End = polyline.GetEndWidthAt(segment),
                    }
                );
                at = (at + reach) % n;
                left -= reach;
            }

            part.Add(new PeditPoint { At = RevCloudFlat(polyline.GetPointAtParameter(to % n)) });
            return part;
        }

        private static GeoPoint2 RevCloudFlat(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>The point of a polyline nearest a point.</summary>
        private static GeoPoint2 RevCloudOn(DxfCurve curve, GeoPoint2 point) =>
            RevCloudFlat(curve.GetClosestPointTo(P3(point), false));

        /// <summary>A cloud along a path of corners (closed or open), added in the current layer as a new polyline.</summary>
        private static void RevCloudPath(
            CadCommandContext c,
            RevCloudLook cloud,
            List<GeoPoint2> corners,
            bool closed,
            bool reverse
        )
        {
            DxfPolyline made = RevCloudOf(cloud, corners, closed, reverse);

            if (made == null)
            {
                c.Write("Invalid shape to create revision cloud.");
                return;
            }

            // Rectangular and Polygonal end with no message, as AutoCAD 2021's do.
            RevCloudAdd(c, made, false);
        }

        /// <summary>The preview of the cloud the point at the cursor would give, in the current layer as it is added.</summary>
        private static CadPreview RevCloudPreview(CadCommandContext c, Func<CadInput, DxfPolyline> make) =>
            CadPreview.MakingOne(c, input => make(input) is DxfPolyline made ? Current(c, made) : null);

        /// <summary>The cloud along a path of corners, as it is previewed and made; null where the path is too short for one.</summary>
        private static DxfPolyline RevCloudOf(RevCloudLook cloud, List<GeoPoint2> corners, bool closed, bool reverse)
        {
            var path = new DxfPolyline();

            for (int i = 0; i < corners.Count; i++)
            {
                path.AddVertexAt(i, corners[i], 0.0, 0.0, 0.0);
            }

            path.Closed = closed;
            List<GeoPoint2> points = RevCloudAlong(path, closed, cloud);
            return points.Count < (closed ? 3 : 2) ? null : RevCloudPolyline(points, closed, cloud, reverse);
        }

        /// <summary>Adds a cloud in the current layer; <paramref name="say"/> whether the command says "Revision cloud finished.", as Object and Freehand do.</summary>
        private static void RevCloudAdd(CadCommandContext c, DxfPolyline cloud, bool say)
        {
            CadEditResult result = c.Apply(null, s => s.Append(c.Space, Current(c, cloud)));

            if (result.Succeeded && say)
            {
                c.Write("Revision cloud finished.");
            }
        }

        /// <summary>
        /// The vertices of a cloud along a curve: from its start, an arc length apart along it (each drawn between the minimum and
        /// the maximum, the same order every time), the last of a closed curve dropped where less than half the minimum is left
        /// after it; an open curve's last vertex its end.
        /// </summary>
        private static List<GeoPoint2> RevCloudAlong(DxfCurve curve, bool closed, RevCloudLook cloud)
        {
            double length = curve.GetDistanceAtParameter(curve.EndParam);
            var points = new List<GeoPoint2>();
            var random = new Random(0);
            double at = 0.0;

            while (length - at >= cloud.Least / 2.0)
            {
                points.Add(RevCloudFlat(curve.GetPointAtDist(at)));
                at += cloud.Next(random);
            }

            if (!closed)
            {
                points.Add(RevCloudFlat(curve.EndPoint));
            }

            return points;
        }

        /// <summary>The cloud of vertices: each segment an arc bulging out of the shape, or into it where reversed.</summary>
        private static DxfPolyline RevCloudPolyline(
            List<GeoPoint2> points,
            bool closed,
            RevCloudLook cloud,
            bool reverse
        )
        {
            double bulge = (RevCloudArea(points) > 0.0) != reverse ? RevCloudBulge : -RevCloudBulge;
            var made = new DxfPolyline();

            // Every vertex is given the bulge, the last of an open cloud too, as AutoCAD writes it.
            for (int i = 0; i < points.Count; i++)
            {
                made.AddVertexAt(i, points[i], bulge, 0.0, cloud.Width);
            }

            made.Closed = closed;
            RevCloudMark(made, cloud.Approximate);
            return made;
        }

        /// <summary>The signed area of the polygon of points, above nought where they run counter-clockwise.</summary>
        private static double RevCloudArea(List<GeoPoint2> points)
        {
            double twice = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint2 a = points[i];
                GeoPoint2 b = points[(i + 1) % points.Count];
                twice += a.X * b.Y - b.X * a.Y;
            }

            return twice / 2.0;
        }

        /// <summary>The arc lengths and style of a cloud, as used.</summary>
        private sealed class RevCloudLook
        {
            public RevCloudLook(double least, double most, bool calligraphy, double approximate)
            {
                Least = least;
                Most = most;
                Width = calligraphy ? most / 10.0 : 0.0;
                Approximate = approximate;
            }

            /// <summary>The approximate arc length the cloud was asked for, which its data keep.</summary>
            public double Approximate { get; }

            public double Least { get; }

            public double Most { get; }

            /// <summary>The width each arc ends with, nought for Normal, a tenth of the longest arc for Calligraphy.</summary>
            public double Width { get; }

            /// <summary>The length of the next arc: the minimum where it is the maximum, else between them.</summary>
            public double Next(Random random) => Most > Least ? Least + (Most - Least) * random.NextDouble() : Least;
        }
    }
}
