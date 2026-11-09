using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// PLINE with every option AutoCAD 2021 offers: straight segments to a point or of a Length along the last, Width and
    /// Halfwidth for the segments drawn next, Undo and Close; and arc segments to an end point, leaving the polyline the way it
    /// came, or by an included Angle, a CEnter, a Direction, a Radius or a Second pt on them.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Polyline(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            // The widths of the segment drawn next, its start and its end, from PLINEWID; each segment's end the next one's start.
            double plinewid = c.Database.Plinewid;
            var widths = new[] { plinewid, plinewid };
            c.Write("Current line-width is " + Length(plinewid));
            var points = new List<GeoPoint2> { c.Input.Point };

            // The way the polyline leaves its last vertex, which an arc segment drawn next starts along.
            var tangents = new List<GeoVector2> { new GeoVector2(1.0, 0.0) };
            string handle = null;
            bool arcs = false;

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                GeoVector2 along = tangents[tangents.Count - 1];
                int segments = points.Count - 1;

                if (arcs)
                {
                    yield return CadPrompt.ForPoint("Specify endpoint of arc" + HoldCtrl, last, segments >= 1
                        ? new[] { "Angle", "CEnter", "CLose", "Direction", "Halfwidth", "Line", "Radius", "Second pt", "Undo", "Width" }
                        : new[] { "Angle", "CEnter", "Direction", "Halfwidth", "Line", "Radius", "Second pt", "Undo", "Width" });
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify next point", last, segments >= 1
                        ? new[] { "Arc", "Close", "Halfwidth", "Length", "Undo", "Width" }
                        : new[] { "Arc", "Halfwidth", "Length", "Undo", "Width" });
                }

                if (c.Input.Is("Arc"))
                {
                    arcs = true;
                    continue;
                }

                if (c.Input.Is("Line"))
                {
                    arcs = false;
                    continue;
                }

                if (c.Input.Is("Width") || c.Input.Is("Halfwidth"))
                {
                    foreach (CadPrompt prompt in AskWidths(c, c.Input.Is("Halfwidth"), last, widths))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    if (segments == 0)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    string removed = handle;
                    int count = points.Count;

                    if (count == 2)
                    {
                        EraseAll(c, new[] { removed });
                        handle = null;
                    }
                    else
                    {
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, removed) }, s =>
                        {
                            var p = (DxfPolyline)s.Document.FindEntity(removed);
                            p.RemoveVertexAt(count - 1);
                            p.SetBulgeAt(count - 2, 0.0);
                        });
                    }

                    points.RemoveAt(points.Count - 1);
                    tangents.RemoveAt(tangents.Count - 1);
                    continue;
                }

                bool close = c.Input.Is("Close") || c.Input.Is("CLose");
                GeoPoint2 next;
                double sweep = 0.0;

                if (close || c.Input.IsPoint)
                {
                    next = close ? points[0] : c.Input.Point;

                    // The arc that leaves the last vertex along the way the polyline came and ends at the point: its sweep is twice
                    // the angle between that way and the chord. Ctrl held takes it leaving the other way.
                    if (arcs)
                    {
                        GeoVector2 way = c.Input.Control ? along * -1.0 : along;
                        GeoVector2 chord = next - last;
                        sweep = 2.0 * Math.Atan2(way.X * chord.Y - way.Y * chord.X, way.X * chord.X + way.Y * chord.Y);
                    }
                }
                else if (!arcs && c.Input.Is("Length"))
                {
                    yield return CadPrompt.ForDistance("Specify length of line", last);

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        continue;
                    }

                    next = last + along * c.Input.Number;
                }
                else if (arcs && !c.Input.IsNone)
                {
                    var end = new PlineArcEnd();

                    foreach (CadPrompt prompt in PlineArc(c, c.Input.Text, last, along, end))
                    {
                        yield return prompt;
                    }

                    if (!end.Done)
                    {
                        continue;
                    }

                    next = end.End;
                    sweep = end.Sweep;
                }
                else
                {
                    yield break;
                }

                GeoVector2 span = next - last;

                if (span.Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("Invalid point: a segment needs two points apart.");
                    continue;
                }

                double bulge = Math.Tan(sweep / 4.0);
                GeoVector2 leaving = span / span.Length;

                if (arcs)
                {
                    double outAngle = Math.Atan2(span.Y, span.X) + sweep / 2.0;
                    leaving = new GeoVector2(Math.Cos(outAngle), Math.Sin(outAngle));
                }

                double w0 = widths[0];
                double w1 = widths[1];

                if (handle == null)
                {
                    GeoPoint2 start = last;
                    handle = AddedHandle(c.Apply(null, s =>
                    {
                        var made = new DxfPolyline();
                        made.AddVertexAt(0, start, bulge, w0, w1);
                        made.AddVertexAt(1, next, 0.0, w1, w1);
                        s.Append(c.Space, Current(c, made));
                    }));

                    if (handle == null)
                    {
                        yield break;
                    }
                }
                else
                {
                    string growing = handle;
                    int at = points.Count - 1;
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, growing) }, s =>
                    {
                        var p = (DxfPolyline)s.Document.FindEntity(growing);
                        p.SetBulgeAt(at, bulge);
                        SetWidths(p, at, w0, w1);

                        if (close)
                        {
                            p.Closed = true;
                        }
                        else
                        {
                            p.AddVertexAt(at + 1, next, 0.0, w1, w1);
                        }
                    });
                }

                widths[0] = w1;

                if (close)
                {
                    yield break;
                }

                points.Add(next);
                tangents.Add(leaving);
            }
        }

        /// <summary>Gives a vertex the widths of the segment leaving it, writing only those that change.</summary>
        private static void SetWidths(DxfPolyline polyline, int index, double start, double end)
        {
            if (polyline.GetStartWidthAt(index) != start)
            {
                polyline.SetStartWidthAt(index, start);
            }

            if (polyline.GetEndWidthAt(index) != end)
            {
                polyline.SetEndWidthAt(index, end);
            }
        }

        /// <summary>
        /// Width or Halfwidth: the starting width offered as the width the next segment would start at, the ending one as the
        /// starting one given; PLINEWID takes the ending width, as AutoCAD keeps it for the next polyline.
        /// </summary>
        private static IEnumerable<CadPrompt> AskWidths(CadCommandContext c, bool half, GeoPoint2 from, double[] widths)
        {
            double scale = half ? 0.5 : 1.0;
            string word = half ? "half-width" : "width";
            double current = widths[0];
            yield return CadPrompt.ForDistance("Specify starting " + word, from, null, Length(current * scale));

            if (c.Input.Kind == CadInputKind.Number)
            {
                current = c.Input.Number / scale;
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            yield return CadPrompt.ForDistance("Specify ending " + word, from, null, Length(current * scale));
            double ending = current;

            if (c.Input.Kind == CadInputKind.Number)
            {
                ending = c.Input.Number / scale;
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            if (current < 0.0 || ending < 0.0)
            {
                c.Write("Value must be positive or zero.");
                yield break;
            }

            widths[0] = current;
            widths[1] = ending;
            SetHeader(c, "$PLINEWID", c.Database.Plinewid, ending, db => db.Plinewid = ending);
        }

        /// <summary>Where an arc segment of PLINE ends and its sweep, counter-clockwise above nought; not done where the command was left.</summary>
        private sealed class PlineArcEnd
        {
            public bool Done;
            public GeoPoint2 End;
            public double Sweep;
        }

        /// <summary>The arc segment of an option of PLINE's arc mode: Angle, CEnter, Direction, Radius or Second pt.</summary>
        private static IEnumerable<CadPrompt> PlineArc(CadCommandContext c, string option, GeoPoint2 from, GeoVector2 along, PlineArcEnd result)
        {
            Tolerance tolerance = c.Session.Tolerance;

            switch (option)
            {
                case "Angle":
                {
                    yield return CadPrompt.ForAngle("Specify included angle", from);

                    if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out double sweep))
                    {
                        yield break;
                    }

                    yield return CadPrompt.ForPoint("Specify endpoint of arc" + HoldCtrl, from, new[] { "CEnter", "Radius" });

                    if (c.Input.IsPoint)
                    {
                        Finish(result, c.Input.Point, c.Input.Control ? -sweep : sweep);
                    }
                    else if (c.Input.Is("CEnter"))
                    {
                        yield return CadPrompt.ForPoint("Specify center point of arc", from);

                        if (c.Input.IsPoint)
                        {
                            Finish(result, Rotated(from, c.Input.Point, sweep), sweep);
                        }
                    }
                    else if (c.Input.Is("Radius"))
                    {
                        yield return CadPrompt.ForDistance("Specify radius of arc", from);

                        if (c.Input.Kind != CadInputKind.Number)
                        {
                            yield break;
                        }

                        double radius = Math.Abs(c.Input.Number);

                        foreach (CadPrompt prompt in ChordDirection(c, from, along, 2.0 * radius * Math.Sin(Math.Abs(sweep) / 2.0), sweep, result))
                        {
                            yield return prompt;
                        }
                    }

                    yield break;
                }

                case "CEnter":
                {
                    yield return CadPrompt.ForPoint("Specify center point of arc", from);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 center = c.Input.Point;
                    double radius = (from - center).Length;

                    if (radius <= tolerance.EqualPoint)
                    {
                        c.Write("*Invalid*");
                        yield break;
                    }

                    double a0 = Math.Atan2(from.Y - center.Y, from.X - center.X);
                    yield return CadPrompt.ForPoint("Specify endpoint of arc" + HoldCtrl, center, new[] { "Angle", "Length" });
                    double sweep;

                    if (c.Input.IsPoint)
                    {
                        sweep = Turn(Math.Atan2(c.Input.Point.Y - center.Y, c.Input.Point.X - center.X) - a0);

                        if (sweep <= tolerance.EqualAngleRad)
                        {
                            c.Write("*Invalid*");
                            yield break;
                        }

                        if (c.Input.Control)
                        {
                            sweep -= 2.0 * Math.PI;
                        }
                    }
                    else if (c.Input.Is("Angle"))
                    {
                        yield return CadPrompt.ForAngle("Specify included angle" + HoldCtrl, center);

                        if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out sweep))
                        {
                            yield break;
                        }
                    }
                    else if (c.Input.Is("Length"))
                    {
                        yield return CadPrompt.ForDistance("Specify length of chord" + HoldCtrl, from);
                        double chord = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 0.0;

                        if (Math.Abs(chord) <= tolerance.EqualPoint || Math.Abs(chord) > 2.0 * radius + tolerance.EqualPoint)
                        {
                            c.Write("*Invalid*");
                            yield break;
                        }

                        double half = Math.Asin(Math.Min(1.0, Math.Abs(chord) / (2.0 * radius)));
                        sweep = chord > 0.0 ? 2.0 * half : 2.0 * Math.PI - 2.0 * half;

                        if (c.Input.Control)
                        {
                            sweep = -sweep;
                        }
                    }
                    else
                    {
                        yield break;
                    }

                    Finish(result, Rotated(from, center, sweep), sweep);
                    yield break;
                }

                case "Direction":
                {
                    yield return CadPrompt.ForAngle("Specify the tangent direction for the start point of arc", from);

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        yield break;
                    }

                    double angle = c.Session.Ucs.AngleToWorld(c.Input.Number);
                    var way = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                    yield return CadPrompt.ForPoint("Specify endpoint of the arc" + HoldCtrl, from);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoVector2 chord = c.Input.Point - from;
                    double turn = Math.Atan2(way.X * chord.Y - way.Y * chord.X, Dot(way, chord));
                    Finish(result, c.Input.Point, c.Input.Control ? -2.0 * turn : 2.0 * turn);
                    yield break;
                }

                case "Radius":
                {
                    yield return CadPrompt.ForDistance("Specify radius of arc", from);

                    if (c.Input.Kind != CadInputKind.Number || Math.Abs(c.Input.Number) <= tolerance.EqualPoint)
                    {
                        yield break;
                    }

                    double radius = c.Input.Number;
                    yield return CadPrompt.ForPoint("Specify endpoint of arc" + HoldCtrl, from, new[] { "Angle" });

                    if (c.Input.IsPoint)
                    {
                        // The shorter arc of the radius counter-clockwise, the longer where the radius is below nought.
                        double half = (c.Input.Point - from).Length / 2.0;

                        if (half > Math.Abs(radius) + tolerance.EqualPoint)
                        {
                            c.Write("*Invalid*");
                            yield break;
                        }

                        double small = 2.0 * Math.Asin(Math.Min(1.0, half / Math.Abs(radius)));
                        double sweep = radius > 0.0 ? small : 2.0 * Math.PI - small;
                        Finish(result, c.Input.Point, c.Input.Control ? -sweep : sweep);
                    }
                    else if (c.Input.Is("Angle"))
                    {
                        yield return CadPrompt.ForAngle("Specify included angle", from);

                        if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out double sweep))
                        {
                            yield break;
                        }

                        foreach (CadPrompt prompt in ChordDirection(c, from, along, 2.0 * Math.Abs(radius) * Math.Sin(Math.Abs(sweep) / 2.0), sweep, result))
                        {
                            yield return prompt;
                        }
                    }

                    yield break;
                }

                case "Second pt":
                {
                    yield return CadPrompt.ForPoint("Specify second point on arc", from);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 second = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify end point of arc", second);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 end = c.Input.Point;

                    if (!CadGrips.CircleThrough(from, second, end, out GeoPoint2 center, out _))
                    {
                        c.Write("*Invalid* The three points lie in a line.");
                        yield break;
                    }

                    double a0 = Math.Atan2(from.Y - center.Y, from.X - center.X);
                    double a1 = Math.Atan2(end.Y - center.Y, end.X - center.X);
                    bool ccw = Cross(end - from, second - from) < 0.0;
                    Finish(result, end, ccw ? Turn(a1 - a0) : -Turn(a0 - a1));
                    yield break;
                }
            }
        }

        /// <summary>The end of a chord of a length from a point, its direction asked with the way the polyline leaves offered.</summary>
        private static IEnumerable<CadPrompt> ChordDirection(CadCommandContext c, GeoPoint2 from, GeoVector2 along, double length, double sweep, PlineArcEnd result)
        {
            double leaving = c.Session.Ucs.AngleToUcs(Math.Atan2(along.Y, along.X));
            yield return CadPrompt.ForAngle("Specify direction of chord for arc" + HoldCtrl, from, null, Number(Turn(leaving) * 180.0 / Math.PI));
            double angle;

            if (c.Input.IsNone)
            {
                angle = leaving;
            }
            else if (c.Input.Kind == CadInputKind.Number)
            {
                angle = c.Input.Number;
            }
            else
            {
                yield break;
            }

            angle = c.Session.Ucs.AngleToWorld(angle);
            Finish(result, from + new GeoVector2(Math.Cos(angle), Math.Sin(angle)) * length, c.Input.Control ? -sweep : sweep);
        }

        /// <summary>A point turned about a centre through an angle, counter-clockwise above nought.</summary>
        private static GeoPoint2 Rotated(GeoPoint2 point, GeoPoint2 center, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return new GeoPoint2(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
        }

        private static void Finish(PlineArcEnd result, GeoPoint2 end, double sweep)
        {
            result.Done = true;
            result.End = end;
            result.Sweep = sweep;
        }
    }
}
