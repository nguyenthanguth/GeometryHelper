using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>REVCLOUD, as AutoCAD 2021 asks it and stores the cloud (checked against clouds AutoCAD made in its own console).</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] RevCloudOptions = { "Arc length", "Object", "Rectangular", "Polygonal", "Freehand", "Style", "Modify" };

        /// <summary>The bulge of every arc of a cloud, as AutoCAD writes it: an arc of 110 degrees, tan(27.5 degrees).</summary>
        private static readonly double RevCloudBulge = Math.Tan(110.0 / 4.0 * Math.PI / 180.0);

        /// <summary>
        /// REVCLOUD: a cloud of arcs drawn round a rectangle by two corners, a polygon by its points, a path traced with the
        /// cursor, or the outline of an object picked, which it replaces; Modify redraws part of a cloud. The cloud is a
        /// LWPOLYLINE whose vertices lie an arc length apart along the path, each segment an arc of 110 degrees (bulge
        /// 0.52056705) bulging out, a last arc shorter than half an arc length taken into the one before; Calligraphy gives
        /// each arc a width from nought to a tenth of the longest arc. The arc lengths are kept between runs, times DIMSCALE
        /// where they are used, as AutoCAD keeps them.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloud(CadCommandContext c)
        {
            double scale = c.Database.Dimscale > 0.0 ? c.Database.Dimscale : 1.0;
            double fallback = c.Database.Measurement == DxfMeasurementValue.Metric ? 15.0 : 0.5;
            string[] types = { "Freehand", "Rectangular", "Polygonal" };

            while (true)
            {
                double least = c.Remembered("revcloud.min", fallback) * scale;
                double most = c.Remembered("revcloud.max", fallback) * scale;
                bool calligraphy = c.Remembered("revcloud.style", 0.0) != 0.0;
                int type = (int)c.Remembered("revcloud.type", 1.0);
                c.Write("Minimum arc length: " + Length(least) + "   Maximum arc length: " + Length(most) + "   Style: "
                    + (calligraphy ? "Calligraphy" : "Normal") + "   Type: " + types[type]);
                string ask = type == 1 ? "Specify first corner point" : type == 2 ? "Specify first point" : "Specify start point";
                yield return CadPrompt.ForPoint(ask, null, RevCloudOptions, "Object");

                if (c.Input.Is("Arc length"))
                {
                    foreach (CadPrompt prompt in RevCloudArcLength(c, least, scale))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Style"))
                {
                    yield return CadPrompt.ForKeyword("Select arc style", new[] { "Normal", "Calligraphy" }, calligraphy ? "Calligraphy" : "Normal");

                    if (c.Input.Is("Normal") || c.Input.Is("Calligraphy"))
                    {
                        c.Remember("revcloud.style", c.Input.Is("Calligraphy") ? 1.0 : 0.0);
                        c.Write(c.Input.Text);
                    }

                    continue;
                }

                if (c.Input.Is("Rectangular") || c.Input.Is("Polygonal") || c.Input.Is("Freehand"))
                {
                    c.Remember("revcloud.type", c.Input.Is("Rectangular") ? 1.0 : c.Input.Is("Polygonal") ? 2.0 : 0.0);
                    continue;
                }

                var cloud = new RevCloudLook(least, most, calligraphy);

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
                IEnumerable<CadPrompt> drawing = type == 1 ? RevCloudRectangle(c, cloud, first) : type == 2 ? RevCloudPolygon(c, cloud, first) : RevCloudFreehand(c, cloud, first);

                foreach (CadPrompt prompt in drawing)
                {
                    yield return prompt;
                }

                yield break;
            }
        }

        /// <summary>
        /// Arc length: the minimum, then the maximum, which is no less than the minimum and no more than three times it; both kept
        /// divided by DIMSCALE, as AutoCAD keeps them.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudArcLength(CadCommandContext c, double least, double scale)
        {
            double low;

            while (true)
            {
                yield return CadPrompt.ForDistance("Specify minimum length of arc", null, null, Length(least));
                low = c.Input.Kind == CadInputKind.Number ? c.Input.Number : least;

                if (low > 0.0)
                {
                    break;
                }

                c.Write("Value must be positive and nonzero.");
            }

            while (true)
            {
                yield return CadPrompt.ForDistance("Specify maximum length of arc", null, null, Length(low));
                double high = c.Input.Kind == CadInputKind.Number ? c.Input.Number : low;

                if (high < low - Slack(c))
                {
                    c.Write("Maximum arc length must be equal or greater than the minimum arc length.");
                    continue;
                }

                if (high > 3.0 * low + Slack(c))
                {
                    c.Write("Maximum arc length cannot exceed three times the minimum arc length.");
                    continue;
                }

                c.Remember("revcloud.min", low / scale);
                c.Remember("revcloud.max", high / scale);
                yield break;
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

        /// <summary>Rectangular: a cloud round the rectangle of two corners, from the first round by the second.</summary>
        private static IEnumerable<CadPrompt> RevCloudRectangle(CadCommandContext c, RevCloudLook cloud, GeoPoint2 first)
        {
            List<GeoPoint2> Corners(GeoPoint2 second) => new List<GeoPoint2> { first, new GeoPoint2(second.X, first.Y), second, new GeoPoint2(first.X, second.Y) };
            yield return CadPrompt.ForPoint("Specify opposite corner", first)
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
            CadPreview shown = RevCloudPreview(c, input => corners.Count < 2 ? null : RevCloudOf(cloud, new List<GeoPoint2>(corners) { input.Point }, true, false));

            while (true)
            {
                if (corners.Count < 2)
                {
                    yield return CadPrompt.ForPoint("Specify next point", corners[corners.Count - 1]);
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify next point", corners[corners.Count - 1], new[] { "Undo" }).Previewing(shown);
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
                yield return CadPrompt.ForPoint("Guide crosshairs along cloud path...", vertices[vertices.Count - 1]).WithoutColon().Tracking()
                    .Previewing(RevCloudPreview(c, input => vertices.Count < 2 ? null : RevCloudPolyline(vertices, false, cloud, false)));

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

            RevCloudAdd(c, RevCloudPolyline(vertices, closed, cloud, reverse));
        }

        /// <summary>
        /// Modify: a closed cloud picked is redrawn between where it was picked and where the new path given comes back to it;
        /// the side picked is erased and the new path clouded in its place, one closed cloud again.
        /// </summary>
        private static IEnumerable<CadPrompt> RevCloudModify(CadCommandContext c, RevCloudLook cloud)
        {
            yield return CadPrompt.ForEntity("Select polyline to modify");

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (!(c.Document.FindEntity(handle) is DxfPolyline old) || !old.Closed || Math.Abs(old.Normal.Z - 1.0) > Tolerance.Default.EqualVector)
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

            if (path.Count < 2)
            {
                yield break;
            }

            GeoPoint2 end = path[path.Count - 1];

            if (c.Input.IsNone)
            {
                end = RevCloudOn(old, end);
                path[path.Count - 1] = end;
            }

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

            // The side from the end of the new path round to its start is kept where the other side is picked, and the
            // other way about.
            double picked = old.GetParameterAtPoint(old.GetClosestPointTo(P3(c.Input.Point), false));
            double count = old.EndParam;
            bool pickedForward = RevCloudWithin(from, to, picked, count);
            List<PeditPoint> kept = pickedForward ? RevCloudPart(old, to, from) : RevCloudPart(old, from, to);
            List<GeoPoint2> fresh = pickedForward ? path : Enumerable.Reverse(path).ToList();
            var line = new DxfPolyline();
            line.AddVertexAt(0, fresh[0], 0.0, 0.0, 0.0);

            for (int i = 1; i < fresh.Count; i++)
            {
                line.AddVertexAt(i, fresh[i], 0.0, 0.0, 0.0);
            }

            List<GeoPoint2> arcs = RevCloudAlong(line, false, cloud);
            var outline = kept.Select(p => p.At).Concat(arcs.Skip(1).Take(arcs.Count - 2)).ToList();
            bool outward = RevCloudArea(outline) > 0.0;
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Modify") }, s =>
            {
                var own = (DxfPolyline)s.Document.FindEntity(handle);
                var points = new List<PeditPoint>(kept);
                points[points.Count - 1].Bulge = (outward ? 1.0 : -1.0) * RevCloudBulge;

                for (int i = 1; i < arcs.Count - 1; i++)
                {
                    points.Add(new PeditPoint { At = arcs[i], Bulge = (outward ? 1.0 : -1.0) * RevCloudBulge, Start = 0.0, End = cloud.Width });
                }

                points[points.Count - 1].Start = 0.0;
                points[points.Count - 1].End = cloud.Width;
                PeditWrite(own, points, true, DxfPoly2dType.SimplePoly);
            });

            if (result.Succeeded)
            {
                c.Write("Revision cloud finished.");
            }
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
                part.Add(new PeditPoint
                {
                    At = RevCloudFlat(polyline.GetPointAtParameter(segment + into)),
                    Bulge = Math.Tan(sweep / 4.0),
                    Start = polyline.GetStartWidthAt(segment),
                    End = polyline.GetEndWidthAt(segment),
                });
                at = (at + reach) % n;
                left -= reach;
            }

            part.Add(new PeditPoint { At = RevCloudFlat(polyline.GetPointAtParameter(to % n)) });
            return part;
        }

        private static GeoPoint2 RevCloudFlat(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>The point of a polyline nearest a point.</summary>
        private static GeoPoint2 RevCloudOn(DxfCurve curve, GeoPoint2 point) => RevCloudFlat(curve.GetClosestPointTo(P3(point), false));

        /// <summary>A cloud along a path of corners (closed or open), added in the current layer as a new polyline.</summary>
        private static void RevCloudPath(CadCommandContext c, RevCloudLook cloud, List<GeoPoint2> corners, bool closed, bool reverse)
        {
            DxfPolyline made = RevCloudOf(cloud, corners, closed, reverse);

            if (made == null)
            {
                c.Write("Invalid shape to create revision cloud.");
                return;
            }

            RevCloudAdd(c, made);
        }

        /// <summary>The preview of the cloud the point at the cursor would give, in the current layer as it is added.</summary>
        private static CadPreview RevCloudPreview(CadCommandContext c, Func<CadInput, DxfPolyline> make)
            => CadPreview.MakingOne(c, input => make(input) is DxfPolyline made ? Current(c, made) : null);

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

        private static void RevCloudAdd(CadCommandContext c, DxfPolyline cloud)
        {
            CadEditResult result = c.Apply(null, s => s.Append(c.Space, Current(c, cloud)));

            if (result.Succeeded)
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
        private static DxfPolyline RevCloudPolyline(List<GeoPoint2> points, bool closed, RevCloudLook cloud, bool reverse)
        {
            double bulge = (RevCloudArea(points) > 0.0) != reverse ? RevCloudBulge : -RevCloudBulge;
            var made = new DxfPolyline();

            // Every vertex is given the bulge, the last of an open cloud too, as AutoCAD writes it.
            for (int i = 0; i < points.Count; i++)
            {
                made.AddVertexAt(i, points[i], bulge, 0.0, cloud.Width);
            }

            made.Closed = closed;
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

        /// <summary>The arc lengths and style of a cloud, as used: times DIMSCALE.</summary>
        private sealed class RevCloudLook
        {
            public RevCloudLook(double least, double most, bool calligraphy)
            {
                Least = least;
                Most = most;
                Width = calligraphy ? most / 10.0 : 0.0;
            }

            public double Least { get; }

            public double Most { get; }

            /// <summary>The width each arc ends with, nought for Normal, a tenth of the longest arc for Calligraphy.</summary>
            public double Width { get; }

            /// <summary>The length of the next arc: the minimum where it is the maximum, else between them.</summary>
            public double Next(Random random) => Most > Least ? Least + (Most - Least) * random.NextDouble() : Least;
        }
    }
}
