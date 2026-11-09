using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ARC every way AutoCAD 2021 draws one: through three points; by its start, centre and end, included angle or chord length,
    /// the centre first if asked; by its start and end and the included angle, the direction it leaves the start or the radius;
    /// and on from the end of the line or arc drawn last, Enter at the first prompt. Counter-clockwise unless Ctrl is held as the
    /// last point is picked, or ANGDIR turns the angles typed clockwise.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string HoldCtrl = " (hold Ctrl to switch direction)";

        private static IEnumerable<CadPrompt> Arc(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point of arc", null, new[] { "Center" });

            if (c.Input.IsNone)
            {
                // On from the end of the line or arc drawn last, leaving it the way that one ended.
                if (c.Remembered("draw.last", 0.0) == 0.0)
                {
                    c.Write("No line or arc to continue.");
                    yield break;
                }

                var from = new GeoPoint2(c.Remembered("draw.last.x", 0.0), c.Remembered("draw.last.y", 0.0));
                var way = new GeoVector2(c.Remembered("draw.last.dx", 1.0), c.Remembered("draw.last.dy", 0.0));
                yield return CadPrompt.ForPoint("Specify end point of arc" + HoldCtrl, from);

                if (c.Input.IsPoint)
                {
                    ArcLeaving(c, from, c.Input.Control ? way * -1.0 : way, c.Input.Point);
                }

                yield break;
            }

            GeoPoint2 start;
            GeoPoint2 center;

            if (c.Input.Is("Center"))
            {
                yield return CadPrompt.ForPoint("Specify center point of arc");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify start point of arc", center);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                start = c.Input.Point;
            }
            else if (c.Input.IsPoint)
            {
                start = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point of arc", start, new[] { "Center", "End" });

                if (c.Input.Is("End"))
                {
                    foreach (CadPrompt prompt in ArcByEnds(c, start))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (c.Input.IsPoint)
                {
                    GeoPoint2 second = c.Input.Point;

                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify end point of arc", second);

                        if (!c.Input.IsPoint)
                        {
                            yield break;
                        }

                        GeoPoint2 last = c.Input.Point;

                        if (CadGrips.ArcThrough(start, second, last, out GeoPoint2 through, out double radius, out double a0, out double a1))
                        {
                            if (AddedHandle(c.Apply(null, s => s.Append(c.Space, Current(c, new DxfArc(P3(through), radius, a0, a1))))) != null)
                            {
                                // Drawn counter-clockwise where the second point lies left of the way from the start to the end.
                                GeoVector2 outward = (last - through) / radius;
                                bool ccw = Cross(last - start, second - start) < 0.0;
                                RememberLast(c, last, ccw ? new GeoVector2(-outward.Y, outward.X) : new GeoVector2(outward.Y, -outward.X));
                            }

                            yield break;
                        }

                        c.Write("*Invalid* The three points lie in a line.");
                    }
                }

                if (!c.Input.Is("Center"))
                {
                    yield break;
                }

                yield return CadPrompt.ForPoint("Specify center point of arc", start);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = c.Input.Point;
            }
            else
            {
                yield break;
            }

            foreach (CadPrompt prompt in ArcByCenter(c, start, center))
            {
                yield return prompt;
            }
        }

        /// <summary>The arc of a start and a centre known: to the way of an end point, through an included angle, or to a chord's length.</summary>
        private static IEnumerable<CadPrompt> ArcByCenter(CadCommandContext c, GeoPoint2 start, GeoPoint2 center)
        {
            double radius = (start - center).Length;

            if (radius <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The start point is the center.");
                yield break;
            }

            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            yield return CadPrompt.ForPoint("Specify end point of arc" + HoldCtrl, center, new[] { "Angle", "chord Length" });
            double sweep;

            if (c.Input.IsPoint)
            {
                GeoPoint2 end = c.Input.Point;

                if ((end - center).Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("*Invalid*");
                    yield break;
                }

                // Counter-clockwise from the start to the way of the end; Ctrl the other way round to it.
                sweep = Turn(Math.Atan2(end.Y - center.Y, end.X - center.X) - a0);

                if (sweep <= c.Session.Tolerance.EqualAngleRad)
                {
                    sweep = 2.0 * Math.PI;
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
            else if (c.Input.Is("chord Length"))
            {
                yield return CadPrompt.ForDistance("Specify length of chord" + HoldCtrl, start);

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                // A chord typed below nought takes the longer of the two arcs it cuts off.
                double chord = c.Input.Number;

                if (Math.Abs(chord) <= c.Session.Tolerance.EqualPoint || Math.Abs(chord) > 2.0 * radius + c.Session.Tolerance.EqualPoint)
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

            AddArc(c, center, radius, a0, sweep);
        }

        /// <summary>The arc of a start and an end known: about a centre picked, through an included angle, leaving the start a way, or of a radius.</summary>
        private static IEnumerable<CadPrompt> ArcByEnds(CadCommandContext c, GeoPoint2 start)
        {
            yield return CadPrompt.ForPoint("Specify end point of arc", start);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 end = c.Input.Point;
            GeoVector2 chord = end - start;
            double length = chord.Length;

            if (length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The start and end points are one point.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify center point of arc" + HoldCtrl, end, new[] { "Angle", "Direction", "Radius" });
            double sweep;

            if (c.Input.IsPoint)
            {
                // About the centre picked, from the start to the way of the end, as with the centre second.
                GeoPoint2 center = c.Input.Point;
                double radius = (start - center).Length;

                if (radius <= c.Session.Tolerance.EqualPoint || (end - center).Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("*Invalid*");
                    yield break;
                }

                double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
                sweep = Turn(Math.Atan2(end.Y - center.Y, end.X - center.X) - a0);

                if (sweep <= c.Session.Tolerance.EqualAngleRad)
                {
                    sweep = 2.0 * Math.PI;
                }

                AddArc(c, center, radius, a0, c.Input.Control ? sweep - 2.0 * Math.PI : sweep);
                yield break;
            }

            if (c.Input.Is("Angle"))
            {
                yield return CadPrompt.ForAngle("Specify included angle" + HoldCtrl, start);

                if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out sweep))
                {
                    yield break;
                }
            }
            else if (c.Input.Is("Direction"))
            {
                yield return CadPrompt.ForAngle("Specify tangent direction for the start point of arc" + HoldCtrl, start);

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                double angle = c.Session.Ucs.AngleToWorld(c.Input.Number);
                var way = new GeoVector2(Math.Cos(angle), Math.Sin(angle));

                // Ctrl takes the arc mirrored over its chord, leaving the start the way mirrored too.
                if (c.Input.Control)
                {
                    GeoVector2 along = chord / length;
                    way = along * (2.0 * Dot(way, along)) - way;
                }

                ArcLeaving(c, start, way, end);
                yield break;
            }
            else if (c.Input.Is("Radius"))
            {
                yield return CadPrompt.ForDistance("Specify radius of arc" + HoldCtrl, end);

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                // A radius typed below nought takes the longer of the two arcs, as AutoCAD's.
                double radius = c.Input.Number;

                if (Math.Abs(radius) < length / 2.0 - c.Session.Tolerance.EqualPoint)
                {
                    c.Write("*Invalid*");
                    yield break;
                }

                double half = Math.Asin(Math.Min(1.0, length / (2.0 * Math.Abs(radius))));
                sweep = radius > 0.0 ? 2.0 * half : 2.0 * Math.PI - 2.0 * half;

                if (c.Input.Control)
                {
                    sweep = -sweep;
                }
            }
            else
            {
                yield break;
            }

            // The centre of the arc of that sweep from the start to the end: on the left of the chord for a counter-clockwise
            // sweep below a half turn, the half chord over the tangent of half the sweep from its middle.
            var middle = new GeoPoint2((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
            var left = new GeoVector2(-chord.Y, chord.X) / length;
            GeoPoint2 centre = middle + left * (length / 2.0 / Math.Tan(sweep / 2.0));
            AddArc(c, centre, (start - centre).Length, Math.Atan2(start.Y - centre.Y, start.X - centre.X), sweep);
        }

        /// <summary>
        /// The included angle answered, in radians, counter-clockwise where above nought, clockwise with ANGDIR 1, and the other
        /// way where Ctrl was held; false, said so, for none or a whole turn.
        /// </summary>
        private static bool IncludedAngle(CadCommandContext c, out double sweep)
        {
            sweep = c.Input.Number;

            if (c.Database.Angdir)
            {
                sweep = -sweep;
            }

            if (c.Input.Control)
            {
                sweep = -sweep;
            }

            if (Math.Abs(sweep) <= c.Session.Tolerance.EqualAngleRad || Math.Abs(sweep) >= 2.0 * Math.PI - c.Session.Tolerance.EqualAngleRad)
            {
                c.Write("*Invalid*");
                return false;
            }

            return true;
        }

        /// <summary>The arc that leaves a point a way and ends at another, as ARC goes on from the last line; said so where the end lies on that way.</summary>
        private static void ArcLeaving(CadCommandContext c, GeoPoint2 start, GeoVector2 way, GeoPoint2 end)
        {
            GeoVector2 chord = end - start;
            double length = chord.Length;
            double turn = Math.Atan2(way.X * chord.Y - way.Y * chord.X, Dot(way, chord));

            if (length <= c.Session.Tolerance.EqualPoint || Math.Abs(Math.Sin(turn)) * length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The end point lies on the tangent.");
                return;
            }

            // The sweep is twice the turn from the way to the chord; the centre lies across the way, on the side the arc bends to.
            double sweep = 2.0 * turn;
            double radius = length / (2.0 * Math.Abs(Math.Sin(turn)));
            GeoVector2 unit = way / way.Length;
            GeoVector2 across = turn > 0.0 ? new GeoVector2(-unit.Y, unit.X) : new GeoVector2(unit.Y, -unit.X);
            GeoPoint2 center = start + across * radius;
            AddArc(c, center, radius, Math.Atan2(start.Y - center.Y, start.X - center.X), sweep);
        }

        /// <summary>
        /// Adds the arc of a centre and radius from an angle through a sweep, counter-clockwise where above nought, as the DXF holds
        /// every arc counter-clockwise; the end it was drawn to is kept for an arc or a line to go on from.
        /// </summary>
        private static void AddArc(CadCommandContext c, GeoPoint2 center, double radius, double from, double sweep)
        {
            double a0 = sweep >= 0.0 ? from : from + sweep;
            double a1 = sweep >= 0.0 ? from + sweep : from;
            double start = Turn(a0);
            double end = Turn(a1);
            string handle = AddedHandle(c.Apply(null, s => s.Append(c.Space, Current(c, new DxfArc(P3(center), radius, start, end)))));

            if (handle != null)
            {
                double at = from + sweep;
                var point = new GeoPoint2(center.X + radius * Math.Cos(at), center.Y + radius * Math.Sin(at));
                var way = sweep >= 0.0 ? new GeoVector2(-Math.Sin(at), Math.Cos(at)) : new GeoVector2(Math.Sin(at), -Math.Cos(at));
                RememberLast(c, point, way);
            }
        }

        /// <summary>Keeps the end of the line or arc drawn last and the way it left it, which ARC goes on from at Enter.</summary>
        private static void RememberLast(CadCommandContext c, GeoPoint2 end, GeoVector2 way)
        {
            double length = way.Length;

            if (length <= 0.0)
            {
                return;
            }

            c.Remember("draw.last", 1.0);
            c.Remember("draw.last.x", end.X);
            c.Remember("draw.last.y", end.Y);
            c.Remember("draw.last.dx", way.X / length);
            c.Remember("draw.last.dy", way.Y / length);
        }
    }
}
