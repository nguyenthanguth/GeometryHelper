using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// STRETCH and ALIGN as AutoCAD 2021 asks them. STRETCH takes its objects by crossing windows and polygons drawn at its own
    /// "Select objects:", moves the ends and vertices inside them, moves whole what lies wholly inside or was picked by itself,
    /// and keeps an arc's height off its chord. ALIGN moves, turns and, asked, scales objects by one, two or three pairs of points.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Stretch(CadCommandContext c)
        {
            c.Write("Select objects to stretch by crossing-window or crossing-polygon...");
            var boxes = new List<DisplayBox>();
            var polygons = new List<List<GeoPoint2>>();
            var gathered = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var single = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            string mode = null;

            while (true)
            {
                yield return CadPrompt.ForPoint(mode == null ? "Select objects" : "Specify first corner", null, null, null, true);

                if (c.Input.IsNone)
                {
                    break;
                }

                if (c.Input.Kind == CadInputKind.Text)
                {
                    string typed = c.Input.Text.Trim().ToUpperInvariant();

                    if (typed == "C" || typed == "W")
                    {
                        mode = typed;
                        continue;
                    }

                    if (typed != "CP")
                    {
                        c.Write("*Invalid selection*");
                        c.Write("Expects a point or Window/Crossing/CPolygon");
                        continue;
                    }

                    var ring = new List<GeoPoint2>();
                    yield return CadPrompt.ForPoint("First polygon point");

                    if (c.Input.IsPoint)
                    {
                        ring.Add(c.Input.Point);
                    }

                    while (ring.Count > 0)
                    {
                        yield return CadPrompt.ForPoint("Specify endpoint of line", ring[ring.Count - 1], new[] { "Undo" });

                        if (c.Input.Is("Undo"))
                        {
                            ring.RemoveAt(ring.Count - 1);
                        }
                        else if (c.Input.IsPoint)
                        {
                            ring.Add(c.Input.Point);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (ring.Count < 3)
                    {
                        c.Write("A polygon needs three points.");
                        continue;
                    }

                    polygons.Add(ring);
                    Found(c, gathered, Crossing(c, Bounds(ring)));
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;

                if (mode == null)
                {
                    IReadOnlyList<string> hit = c.Document.GetPickIndex(c.Space).Pick(first.X, first.Y, c.Session.PickAperture);

                    if (hit.Count > 0)
                    {
                        single.Add(hit[0]);
                        Found(c, gathered, new[] { hit[0] });
                        continue;
                    }
                }

                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                var box = new DisplayBox(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
                bool crossing = mode == "C" || (mode == null && second.X < first.X);
                PickIndex index = c.Document.GetPickIndex(c.Space);
                boxes.Add(box);
                Found(c, gathered, crossing ? index.Crossing(box) : index.Window(box));
                mode = null;
            }

            var handles = new List<string>();

            foreach (string handle in gathered)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && !CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    handles.Add(handle);
                }
                else if (entity != null)
                {
                    c.Write("1 was on a locked layer.");
                }
            }

            if (handles.Count == 0)
            {
                yield break;
            }

            c.ClearsSelection = true;
            var move = new Ref<GeoVector2?>();

            foreach (CadPrompt prompt in Displacement(c, move))
            {
                yield return prompt;
            }

            if (!move.Value.HasValue)
            {
                yield break;
            }

            GeoVector2 by = move.Value.Value;
            Func<GeoPoint3, bool> inside = p => InAny(boxes, polygons, p);
            var planned = new List<CadEditRecord>();
            var stretched = new List<KeyValuePair<string, bool[]>>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                bool[] held = single.Contains(handle) ? null : Held(entity, inside);

                if (held != null && held.Length == 0)
                {
                    continue;
                }

                if (held == null || Array.TrueForAll(held, h => h))
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Transformed, handle, "Move", null, Values(by.X, by.Y)));
                    stretched.Add(new KeyValuePair<string, bool[]>(handle, null));
                    continue;
                }

                if (!Array.Exists(held, h => h))
                {
                    continue;
                }

                if (c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
                {
                    c.Write("This " + CadOwnerEditGate.Describe(entity) + " belongs to the model and cannot be stretched here.");
                    continue;
                }

                planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, handle, "STRETCH"));
                stretched.Add(new KeyValuePair<string, bool[]>(handle, held));
            }

            if (planned.Count == 0)
            {
                yield break;
            }

            c.Apply(planned, s =>
            {
                foreach (KeyValuePair<string, bool[]> one in stretched)
                {
                    if (s.IsHandled(one.Key))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(one.Key);

                    if (one.Value == null)
                    {
                        entity.TransformBy(GeoTransform3.Translation(new GeoVector3(by.X, by.Y, 0.0)));
                    }
                    else
                    {
                        StretchPoints(entity, one.Value, by);
                    }
                }
            });
        }

        /// <summary>Adds what a selection found and says so as AutoCAD does: "2 found", with the total after the first.</summary>
        private static void Found(CadCommandContext c, SortedSet<string> gathered, IReadOnlyList<string> found)
        {
            int before = gathered.Count;

            foreach (string handle in found)
            {
                gathered.Add(handle);
            }

            string line = found.Count.ToString(CultureInfo.InvariantCulture) + " found";

            if (before > 0)
            {
                line += ", " + gathered.Count.ToString(CultureInfo.InvariantCulture) + " total";
            }

            c.Write(line);
        }

        private static IReadOnlyList<string> Crossing(CadCommandContext c, DisplayBox box) => c.Document.GetPickIndex(c.Space).Crossing(box);

        private static DisplayBox Bounds(List<GeoPoint2> ring)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

            foreach (GeoPoint2 point in ring)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new DisplayBox(minX, minY, maxX, maxY);
        }

        /// <summary>Whether a point lies inside one of the windows or the polygons of STRETCH.</summary>
        private static bool InAny(List<DisplayBox> boxes, List<List<GeoPoint2>> polygons, GeoPoint3 point)
        {
            foreach (DisplayBox box in boxes)
            {
                if (box.Contains(point.X, point.Y))
                {
                    return true;
                }
            }

            foreach (List<GeoPoint2> ring in polygons)
            {
                bool odd = false;

                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    if ((ring[i].Y > point.Y) != (ring[j].Y > point.Y)
                        && point.X < (ring[j].X - ring[i].X) * (point.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                    {
                        odd = !odd;
                    }
                }

                if (odd)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Which points of an entity STRETCH moves, each true where it lies in a window: a line's two ends, a polyline's
        /// vertices, an arc's two ends; one point for an entity moved whole by it (a circle's or an ellipse's centre, the point a
        /// text, a block or a point is placed by); for any other entity one point, true where all of its box lies inside.
        /// </summary>
        private static bool[] Held(DxfEntity entity, Func<GeoPoint3, bool> inside)
        {
            switch (entity)
            {
                case DxfLine line:
                    return new[] { inside(line.StartPoint), inside(line.EndPoint) };
                case DxfPolyline polyline when Math.Abs(Math.Abs(polyline.Normal.Z) - 1.0) <= Tolerance.Default.EqualVector:
                    var vertices = new bool[polyline.NumberOfVertices];

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = inside(polyline.GetPoint3dAt(i));
                    }

                    return vertices;
                case DxfArc arc when Math.Abs(Math.Abs(arc.Normal.Z) - 1.0) <= Tolerance.Default.EqualVector:
                    return new[] { inside(arc.StartPoint), inside(arc.EndPoint) };
                case DxfCircle circle:
                    return new[] { inside(circle.Center) };
                case DxfEllipse ellipse:
                    return new[] { inside(ellipse.Center) };
                case DxfDBText text:
                    return new[] { inside(text.Position) };
                case DxfMText mtext:
                    return new[] { inside(mtext.Location) };
                case DxfBlockReference insert:
                    return new[] { inside(insert.Position) };
                case DxfDBPoint point:
                    return new[] { inside(point.Position) };
                default:
                    GeoAabb3 box;

                    try
                    {
                        box = entity.GeometricExtents;
                    }
                    catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException)
                    {
                        return new bool[0];
                    }

                    if (box.IsEmpty)
                    {
                        return new bool[0];
                    }

                    return new[]
                    {
                        inside(box.Min) && inside(box.Max) && inside(new GeoPoint3(box.Min.X, box.Max.Y, 0.0)) && inside(new GeoPoint3(box.Max.X, box.Min.Y, 0.0)),
                    };
            }
        }

        /// <summary>Moves the points STRETCH holds of an entity it stretches: a line's ends, a polyline's vertices, an arc's ends.</summary>
        private static void StretchPoints(DxfEntity entity, bool[] held, GeoVector2 by)
        {
            switch (entity)
            {
                case DxfLine line:
                    if (held[0])
                    {
                        line.StartPoint = new GeoPoint3(line.StartPoint.X + by.X, line.StartPoint.Y + by.Y, line.StartPoint.Z);
                    }

                    if (held[1])
                    {
                        line.EndPoint = new GeoPoint3(line.EndPoint.X + by.X, line.EndPoint.Y + by.Y, line.EndPoint.Z);
                    }

                    break;
                case DxfPolyline polyline:
                    // In the plane of a polyline whose normal points down, x runs along the world's -x.
                    double across = polyline.Normal.Z < 0.0 ? -by.X : by.X;

                    for (int i = 0; i < held.Length; i++)
                    {
                        if (held[i])
                        {
                            GeoPoint2 at = polyline.GetPoint2dAt(i);
                            polyline.SetPointAt(i, new GeoPoint2(at.X + across, at.Y + by.Y));
                        }
                    }

                    break;
                case DxfArc arc:
                    StretchArc(arc, held[0] ? by : default, held[1] ? by : default);
                    break;
            }
        }

        /// <summary>
        /// Moves the ends of an arc, drawing it anew through them in its plane and its sense with the height of its middle off its
        /// chord kept, as AutoCAD 2021 stretches one: the arc of radius 10 from (100, 25) to (90, 35) whose start goes to
        /// (150, 25) keeps 2.929 and takes a radius of 159.37. An arc whose ends would meet moves whole by its start's move.
        /// </summary>
        private static void StretchArc(DxfArc arc, GeoVector2 start, GeoVector2 end)
        {
            GeoPoint3 from = arc.StartPoint;
            GeoPoint3 to = arc.EndPoint;
            GeoPoint3 middle = arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0);
            double cx = to.X - from.X, cy = to.Y - from.Y;
            double chord = Math.Sqrt(cx * cx + cy * cy);
            var a = new GeoPoint3(from.X + start.X, from.Y + start.Y, from.Z);
            var b = new GeoPoint3(to.X + end.X, to.Y + end.Y, to.Z);
            double nx = b.X - a.X, ny = b.Y - a.Y;
            double reach = Math.Sqrt(nx * nx + ny * ny);

            if (!(chord > 0.0) || !(reach > Tolerance.Default.EqualPoint))
            {
                arc.TransformBy(GeoTransform3.Translation(new GeoVector3(start.X, start.Y, 0.0)));
                return;
            }

            // The height of the middle off the chord, signed to the left of the chord from the start to the end, kept.
            double height = ((middle.X - (from.X + to.X) / 2.0) * -cy + (middle.Y - (from.Y + to.Y) / 2.0) * cx) / chord;
            double half = reach / 2.0;
            double radius = (half * half + height * height) / (2.0 * Math.Abs(height));
            double lx = -ny / reach, ly = nx / reach;
            double off = height - Math.Sign(height) * radius;
            var centre = new GeoPoint2((a.X + b.X) / 2.0 + lx * off, (a.Y + b.Y) / 2.0 + ly * off);
            double startAngle = Math.Atan2(a.Y - centre.Y, a.X - centre.X);
            double endAngle = Math.Atan2(b.Y - centre.Y, b.X - centre.X);
            arc.Center = new GeoPoint3(centre.X, centre.Y, arc.Center.Z);
            arc.Radius = radius;

            if (arc.Normal.Z < 0.0)
            {
                // About a normal pointing down the plane's x is the world's -x: an angle there is a half turn less the world's.
                arc.StartAngle = Turn(Math.PI - startAngle);
                arc.EndAngle = Turn(Math.PI - endAngle);
            }
            else
            {
                arc.StartAngle = Turn(startAngle);
                arc.EndAngle = Turn(endAngle);
            }
        }

        private static IEnumerable<CadPrompt> Align(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first source point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 p1 = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify first destination point", p1);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 q1 = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second source point");

            if (!c.Input.IsPoint)
            {
                // One pair moves the objects, as MOVE does.
                Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(q1.X - p1.X, q1.Y - p1.Y, 0.0)), false, "Move", Values(q1.X - p1.X, q1.Y - p1.Y));
                yield break;
            }

            GeoPoint2 p2 = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second destination point", p2);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 q2 = c.Input.Point;
            double tolerance = c.Session.Tolerance.EqualPoint;

            if ((p2 - p1).Length <= tolerance || (q2 - q1).Length <= tolerance)
            {
                c.Write("The source and destination points of a pair cannot be one point.");
                yield break;
            }

            double turn = Math.Atan2((q2 - q1).Y, (q2 - q1).X) - Math.Atan2((p2 - p1).Y, (p2 - p1).X);
            GeoTransform3 moved = GeoTransform3.RotationAxis(P3(q1), Up, turn) * GeoTransform3.Translation(new GeoVector3(q1.X - p1.X, q1.Y - p1.Y, 0.0));
            yield return CadPrompt.ForPoint("Specify third source point or", null, null, "continue");

            if (c.Input.IsPoint)
            {
                GeoPoint2 p3 = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify third destination point", p3);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 q3 = c.Input.Point;
                double sourceSide = Cross(p2 - p1, p3 - p1);
                double targetSide = Cross(q2 - q1, q3 - q1);

                // Three pairs in the plan turn the objects over the line of the first two where the third lies across it.
                if (sourceSide * targetSide < 0.0)
                {
                    GeoVector2 axis = q2 - q1;
                    moved = GeoTransform3.RotationAxis(P3(q1), new GeoVector3(axis.X, axis.Y, 0.0).Normalize(), Math.PI) * moved;
                }
            }
            else if (c.Input.IsNone)
            {
                yield return CadPrompt.ForKeyword("Scale objects based on alignment points?", new[] { "Yes", "No" }, "N");

                if (c.Input.Is("Yes"))
                {
                    moved = GeoTransform3.Scaling(P3(q1), (q2 - q1).Length / (p2 - p1).Length) * moved;
                }
            }
            else
            {
                yield break;
            }

            var free = new List<string>();

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
                {
                    c.Write("This " + CadOwnerEditGate.Describe(entity) + " belongs to the model and cannot be aligned here.");
                    continue;
                }

                free.Add(handle);
            }

            if (free.Count > 0)
            {
                Transform(c, free, moved, false, "Align");
            }
        }
    }
}
