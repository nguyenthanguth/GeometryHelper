using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ARC every way AutoCAD 2021 draws one: through three points; by its start, centre and end, included angle or chord length,
    /// the centre first if asked; by its start and end and the included angle, the direction it leaves the start or the radius;
    /// and on from the end of the line, arc or polyline drawn last, Enter at the first prompt. Counter-clockwise unless Ctrl is held as the
    /// last point is picked, or ANGDIR turns the angles typed clockwise. The prompt of the last point previews the arc the
    /// point at the cursor would give, made as the arc is made.
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
                yield return CadPrompt
                    .ForPoint("Specify end point of arc" + HoldCtrl, from)
                    .Previewing(CadPreview.MakingOne(c, input => ArcLeavingOf(c, from, way, input.Point)));

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
                    GeoPoint2 first = start;

                    while (true)
                    {
                        yield return CadPrompt
                            .ForPoint("Specify end point of arc", second)
                            .Previewing(CadPreview.MakingOne(c, input => ArcThrough(c, first, second, input.Point)));

                        if (!c.Input.IsPoint)
                        {
                            yield break;
                        }

                        GeoPoint2 last = c.Input.Point;

                        if (
                            CadGrips.ArcThrough(
                                start,
                                second,
                                last,
                                out GeoPoint2 through,
                                out double radius,
                                out _,
                                out _
                            )
                        )
                        {
                            if (
                                AddedHandle(c.Apply(null, s => s.Append(c.Space, ArcThrough(c, first, second, last))))
                                != null
                            )
                            {
                                // Drawn counter-clockwise where the second point lies left of the way from the start to the end.
                                GeoVector2 outward = (last - through) / radius;
                                bool ccw = Cross(last - start, second - start) < 0.0;
                                RememberLast(
                                    c,
                                    last,
                                    ccw ? new GeoVector2(-outward.Y, outward.X) : new GeoVector2(outward.Y, -outward.X)
                                );
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

        /// <summary>The arc through three points, as ARC makes it; null where they lie in a line.</summary>
        private static DxfArc ArcThrough(CadCommandContext c, GeoPoint2 start, GeoPoint2 second, GeoPoint2 last) =>
            CadGrips.ArcThrough(
                start,
                second,
                last,
                out GeoPoint2 through,
                out double radius,
                out double a0,
                out double a1
            )
                ? Current(c, new DxfArc(P3(through), radius, a0, a1))
                : null;

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
            yield return CadPrompt
                .ForPoint("Specify end point of arc" + HoldCtrl, center, new[] { "Angle", "chord Length" })
                .Previewing(
                    CadPreview.MakingOne(
                        c,
                        input => MakeArc(c, center, radius, a0, SweepTo(c, center, a0, input.Point, false)),
                        true
                    )
                );
            double sweep;

            if (c.Input.IsPoint)
            {
                sweep = SweepTo(c, center, a0, c.Input.Point, c.Input.Control);

                if (double.IsNaN(sweep))
                {
                    c.Write("*Invalid*");
                    yield break;
                }
            }
            else if (c.Input.Is("Angle"))
            {
                yield return CadPrompt
                    .ForAngle("Specify included angle" + HoldCtrl, center)
                    .Turning()
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => MakeArc(c, center, radius, a0, Included(c, input.Number, false)),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out sweep))
                {
                    yield break;
                }
            }
            else if (c.Input.Is("chord Length"))
            {
                yield return CadPrompt
                    .ForDistance("Specify length of chord" + HoldCtrl, start)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => MakeArc(c, center, radius, a0, ChordSweep(c, radius, input.Number, false)),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                sweep = ChordSweep(c, radius, c.Input.Number, c.Input.Control);

                if (double.IsNaN(sweep))
                {
                    c.Write("*Invalid*");
                    yield break;
                }
            }
            else
            {
                yield break;
            }

            AddArc(c, center, radius, a0, sweep);
        }

        /// <summary>
        /// The sweep from the start angle about a centre to the way of an end, counter-clockwise; with Ctrl the other way round
        /// to it. NaN where the end is the centre.
        /// </summary>
        private static double SweepTo(CadCommandContext c, GeoPoint2 center, double a0, GeoPoint2 end, bool control)
        {
            if ((end - center).Length <= c.Session.Tolerance.EqualPoint)
            {
                return double.NaN;
            }

            double sweep = Turn(Math.Atan2(end.Y - center.Y, end.X - center.X) - a0);

            if (sweep <= c.Session.Tolerance.EqualAngleRad)
            {
                sweep = 2.0 * Math.PI;
            }

            return control ? sweep - 2.0 * Math.PI : sweep;
        }

        /// <summary>The sweep a chord's length cuts off a circle, the longer arc for a length below nought; NaN where the chord does not fit.</summary>
        private static double ChordSweep(CadCommandContext c, double radius, double chord, bool control)
        {
            if (
                Math.Abs(chord) <= c.Session.Tolerance.EqualPoint
                || Math.Abs(chord) > 2.0 * radius + c.Session.Tolerance.EqualPoint
            )
            {
                return double.NaN;
            }

            double half = Math.Asin(Math.Min(1.0, Math.Abs(chord) / (2.0 * radius)));
            double sweep = chord > 0.0 ? 2.0 * half : 2.0 * Math.PI - 2.0 * half;
            return control ? -sweep : sweep;
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

            yield return CadPrompt
                .ForPoint("Specify center point of arc" + HoldCtrl, end, new[] { "Angle", "Direction", "Radius" })
                .Previewing(CadPreview.MakingOne(c, input => ArcAbout(c, start, end, input.Point, false), true));
            double sweep;

            if (c.Input.IsPoint)
            {
                // About the centre picked, from the start to the way of the end, as with the centre second.
                DxfArc about = ArcAbout(c, start, end, c.Input.Point, c.Input.Control);

                if (about == null)
                {
                    c.Write("*Invalid*");
                    yield break;
                }

                GeoPoint2 center = c.Input.Point;
                double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
                AddArc(c, center, (start - center).Length, a0, SweepTo(c, center, a0, end, c.Input.Control));
                yield break;
            }

            if (c.Input.Is("Angle"))
            {
                yield return CadPrompt
                    .ForAngle("Specify included angle" + HoldCtrl, start)
                    .Turning()
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => ArcOfEnds(c, start, end, Included(c, input.Number, false)),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number || !IncludedAngle(c, out sweep))
                {
                    yield break;
                }
            }
            else if (c.Input.Is("Direction"))
            {
                yield return CadPrompt
                    .ForAngle("Specify tangent direction for the start point of arc" + HoldCtrl, start)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => ArcLeavingOf(c, start, Direction(c, input.Number, chord, false), end),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                ArcLeaving(c, start, Direction(c, c.Input.Number, chord, c.Input.Control), end);
                yield break;
            }
            else if (c.Input.Is("Radius"))
            {
                yield return CadPrompt
                    .ForDistance("Specify radius of arc" + HoldCtrl, end)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => ArcOfEnds(c, start, end, RadiusSweep(c, length, input.Number, false)),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                sweep = RadiusSweep(c, length, c.Input.Number, c.Input.Control);

                if (double.IsNaN(sweep))
                {
                    c.Write("*Invalid*");
                    yield break;
                }
            }
            else
            {
                yield break;
            }

            ArcOfEnds(c, start, end, sweep, true);
        }

        /// <summary>The arc of a start and an end about a centre picked, as ARC makes it; null where the centre is either end.</summary>
        private static DxfArc ArcAbout(
            CadCommandContext c,
            GeoPoint2 start,
            GeoPoint2 end,
            GeoPoint2 center,
            bool control
        )
        {
            double radius = (start - center).Length;

            if (radius <= c.Session.Tolerance.EqualPoint || (end - center).Length <= c.Session.Tolerance.EqualPoint)
            {
                return null;
            }

            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            return MakeArc(c, center, radius, a0, SweepTo(c, center, a0, end, control));
        }

        /// <summary>The way an arc leaves its start at an angle answered, mirrored over the chord where Ctrl was held.</summary>
        private static GeoVector2 Direction(CadCommandContext c, double number, GeoVector2 chord, bool control)
        {
            double angle = c.Session.Ucs.AngleToWorld(number);
            var way = new GeoVector2(Math.Cos(angle), Math.Sin(angle));

            // Ctrl takes the arc mirrored over its chord, leaving the start the way mirrored too.
            if (control)
            {
                GeoVector2 along = chord / chord.Length;
                way = along * (2.0 * Dot(way, along)) - way;
            }

            return way;
        }

        /// <summary>The sweep of the arc of a radius between two ends a length apart, the longer for a radius below nought; NaN where the radius is too short.</summary>
        private static double RadiusSweep(CadCommandContext c, double length, double radius, bool control)
        {
            // A radius typed below nought takes the longer of the two arcs, as AutoCAD's.
            if (Math.Abs(radius) < length / 2.0 - c.Session.Tolerance.EqualPoint)
            {
                return double.NaN;
            }

            double half = Math.Asin(Math.Min(1.0, length / (2.0 * Math.Abs(radius))));
            double sweep = radius > 0.0 ? 2.0 * half : 2.0 * Math.PI - 2.0 * half;
            return control ? -sweep : sweep;
        }

        /// <summary>
        /// The arc of a sweep from a start to an end: the centre on the left of the chord for a counter-clockwise sweep below a
        /// half turn, the half chord over the tangent of half the sweep from its middle. Added where <paramref name="add"/>;
        /// null for a sweep of NaN.
        /// </summary>
        private static DxfArc ArcOfEnds(
            CadCommandContext c,
            GeoPoint2 start,
            GeoPoint2 end,
            double sweep,
            bool add = false
        )
        {
            if (double.IsNaN(sweep))
            {
                return null;
            }

            GeoVector2 chord = end - start;
            double length = chord.Length;
            var middle = new GeoPoint2((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
            var left = new GeoVector2(-chord.Y, chord.X) / length;
            GeoPoint2 centre = middle + left * (length / 2.0 / Math.Tan(sweep / 2.0));
            double radius = (start - centre).Length;
            double from = Math.Atan2(start.Y - centre.Y, start.X - centre.X);

            if (add)
            {
                AddArc(c, centre, radius, from, sweep);
                return null;
            }

            return MakeArc(c, centre, radius, from, sweep);
        }

        /// <summary>
        /// The included angle answered, in radians, counter-clockwise where above nought, clockwise with ANGDIR 1, and the other
        /// way where Ctrl was held; false, said so, for none or a whole turn.
        /// </summary>
        private static bool IncludedAngle(CadCommandContext c, out double sweep)
        {
            sweep = Included(c, c.Input.Number, c.Input.Control);

            if (double.IsNaN(sweep))
            {
                c.Write("*Invalid*");
                return false;
            }

            return true;
        }

        /// <summary>An included angle of a number answered, turned by Ctrl (the session has counted a typed one by ANGDIR); NaN for none or a whole turn.</summary>
        private static double Included(CadCommandContext c, double number, bool control)
        {
            double sweep = number;

            if (control)
            {
                sweep = -sweep;
            }

            return
                Math.Abs(sweep) <= c.Session.Tolerance.EqualAngleRad
                || Math.Abs(sweep) >= 2.0 * Math.PI - c.Session.Tolerance.EqualAngleRad
                ? double.NaN
                : sweep;
        }

        /// <summary>The arc that leaves a point a way and ends at another, as ARC goes on from the last line; said so where the end lies on that way.</summary>
        private static void ArcLeaving(CadCommandContext c, GeoPoint2 start, GeoVector2 way, GeoPoint2 end)
        {
            if (!Leaving(c, start, way, end, out GeoPoint2 center, out double radius, out double sweep))
            {
                c.Write("*Invalid* The end point lies on the tangent.");
                return;
            }

            AddArc(c, center, radius, Math.Atan2(start.Y - center.Y, start.X - center.X), sweep);
        }

        /// <summary>The arc that leaves a point a way and ends at another, as <see cref="ArcLeaving"/> makes it; null where the end lies on that way.</summary>
        private static DxfArc ArcLeavingOf(CadCommandContext c, GeoPoint2 start, GeoVector2 way, GeoPoint2 end) =>
            Leaving(c, start, way, end, out GeoPoint2 center, out double radius, out double sweep)
                ? MakeArc(c, center, radius, Math.Atan2(start.Y - center.Y, start.X - center.X), sweep)
                : null;

        /// <summary>The centre, radius and sweep of the arc that leaves a point a way and ends at another; false where the end lies on that way.</summary>
        private static bool Leaving(
            CadCommandContext c,
            GeoPoint2 start,
            GeoVector2 way,
            GeoPoint2 end,
            out GeoPoint2 center,
            out double radius,
            out double sweep
        )
        {
            GeoVector2 chord = end - start;
            double length = chord.Length;
            double turn = Math.Atan2(way.X * chord.Y - way.Y * chord.X, Dot(way, chord));
            center = start;
            radius = 0.0;
            sweep = 0.0;

            if (
                length <= c.Session.Tolerance.EqualPoint
                || Math.Abs(Math.Sin(turn)) * length <= c.Session.Tolerance.EqualPoint
            )
            {
                return false;
            }

            // The sweep is twice the turn from the way to the chord; the centre lies across the way, on the side the arc bends to.
            sweep = 2.0 * turn;
            radius = length / (2.0 * Math.Abs(Math.Sin(turn)));
            GeoVector2 unit = way / way.Length;
            GeoVector2 across = turn > 0.0 ? new GeoVector2(-unit.Y, unit.X) : new GeoVector2(unit.Y, -unit.X);
            center = start + across * radius;
            return true;
        }

        /// <summary>
        /// The arc of a centre and radius from an angle through a sweep, counter-clockwise where above nought, as the DXF holds
        /// every arc counter-clockwise, in the current layer and colour, as ARC makes and previews it; null for a sweep of NaN.
        /// </summary>
        private static DxfArc MakeArc(CadCommandContext c, GeoPoint2 center, double radius, double from, double sweep)
        {
            if (double.IsNaN(sweep))
            {
                return null;
            }

            double a0 = sweep >= 0.0 ? from : from + sweep;
            double a1 = sweep >= 0.0 ? from + sweep : from;
            return Current(c, new DxfArc(P3(center), radius, Turn(a0), Turn(a1)));
        }

        /// <summary>
        /// Adds the arc of a centre and radius from an angle through a sweep (<see cref="MakeArc"/>);
        /// the end it was drawn to is kept for an arc or a line to go on from.
        /// </summary>
        private static void AddArc(CadCommandContext c, GeoPoint2 center, double radius, double from, double sweep)
        {
            string handle = AddedHandle(c.Apply(null, s => s.Append(c.Space, MakeArc(c, center, radius, from, sweep))));

            if (handle != null)
            {
                double at = from + sweep;
                var point = new GeoPoint2(center.X + radius * Math.Cos(at), center.Y + radius * Math.Sin(at));
                var way =
                    sweep >= 0.0
                        ? new GeoVector2(-Math.Sin(at), Math.Cos(at))
                        : new GeoVector2(Math.Sin(at), -Math.Cos(at));
                RememberLast(c, point, way);
            }
        }

        /// <summary>
        /// Keeps the end of the line, arc or polyline segment drawn last and the way it left it, which ARC goes on from at Enter
        /// and the first arc segment of PLINE starts along, as AutoCAD 2021 starts it.
        /// </summary>
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
