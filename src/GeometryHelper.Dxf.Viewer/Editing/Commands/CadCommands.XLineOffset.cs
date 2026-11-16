using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Offset option of XLINE, and the line object its Reference option takes, as AutoCAD 2021 asks them: "Specify offset
    /// distance or [Through] &lt;Through&gt;:", then "Select a line object:" and "Specify side to offset:" (or "Specify through
    /// point:") for as many xlines as are wanted, until Enter. The xline runs along the line object, a line, a ray, an xline or
    /// the one straight segment of a polyline nearest the pick, and stands its distance to the side picked: along a line from
    /// (0, 0) to (10, 5), at 5, to its left, it is based at (-2.236, 4.472) and runs along (0.894, 0.447), as AutoCAD 2021 lists it.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>A straight piece of the drawing a command takes its way from: where it starts and the unit vector along it.</summary>
        private readonly struct LineWay
        {
            internal LineWay(GeoPoint2 start, GeoVector2 way)
            {
                Start = start;
                Way = way;
            }

            internal GeoPoint2 Start { get; }

            internal GeoVector2 Way { get; }
        }

        /// <summary>
        /// The straight way of an object a "Select a line object:" took: a line, a ray, an xline, or the straight segment of a
        /// polyline nearest the point it was picked at (the first where it was not picked); false for any other object, an arc
        /// segment of a polyline too, and for a line of no length.
        /// </summary>
        private static bool TryLineWay(CadCommandContext c, string handle, GeoPoint2? pickedAt, out LineWay found)
        {
            found = default;
            DxfEntity entity = c.Document.FindEntity(handle);
            double tiny = c.Session.Tolerance.EqualPoint;
            GeoPoint3 start;
            GeoVector3 way;

            switch (entity)
            {
                case DxfLine line:
                    start = line.StartPoint;
                    way = line.EndPoint - line.StartPoint;
                    break;
                case DxfRay ray:
                    start = ray.BasePoint;
                    way = ray.UnitDir;
                    break;
                case DxfXline xline:
                    start = xline.BasePoint;
                    way = xline.UnitDir;
                    break;
                case DxfPolyline _:
                case DxfPolyline2d _:
                    if (!StraightSegment((DxfCurve)entity, pickedAt, tiny, out start, out way))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }

            var along = new GeoVector2(way.X, way.Y);

            if (!(along.Length > 1E-12) || double.IsNaN(along.Length))
            {
                return false;
            }

            found = new LineWay(new GeoPoint2(start.X, start.Y), along / along.Length);
            return true;
        }

        /// <summary>The segment of a polyline nearest a point, its start and the vector to its end; false where it is an arc or the polyline has none.</summary>
        private static bool StraightSegment(
            DxfCurve polyline,
            GeoPoint2? near,
            double tolerance,
            out GeoPoint3 start,
            out GeoVector3 way
        )
        {
            start = default;
            way = default;
            double end = polyline.EndParam;
            int segments = (int)Math.Floor(end + 1E-9);

            if (segments < 1)
            {
                return false;
            }

            int index = 0;

            if (near != null)
            {
                double at = polyline.GetParameterAtPoint(polyline.GetClosestPointTo(P3(near.Value), false));
                index = Math.Max(0, Math.Min(segments - 1, (int)Math.Floor(at + 1E-9)));
            }

            GeoPoint3 from = polyline.GetPointAtParameter(index);
            GeoPoint3 to = polyline.GetPointAtParameter(Math.Min(end, index + 1));
            GeoPoint3 middle = polyline.GetPointAtParameter(index + 0.5);
            var chord = new GeoVector3(to.X - from.X, to.Y - from.Y, 0.0);

            // A segment is an arc where its middle stands off the chord.
            double chordLength = chord.Length;

            if (!(chordLength > 1E-12))
            {
                return false;
            }

            double off = Math.Abs(chord.X * (middle.Y - from.Y) - chord.Y * (middle.X - from.X)) / chordLength;

            if (off > tolerance)
            {
                return false;
            }

            start = from;
            way = chord;
            return true;
        }

        /// <summary>
        /// Asks "Select a line object:" until a line, ray, xline or straight polyline segment is picked (anything else is "No
        /// usable object selected"), or Enter. The result is in <paramref name="chosen"/>, null for Enter.
        /// </summary>
        private static IEnumerable<CadPrompt> AskLineObject(CadCommandContext c, LineWayChoice chosen)
        {
            chosen.Way = null;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select a line object").MissingWith("No usable object selected");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (
                    TryLineWay(
                        c,
                        c.Input.Handles[0],
                        c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null,
                        out LineWay way
                    )
                )
                {
                    chosen.Way = way;
                    yield break;
                }

                c.Write("No usable object selected");
            }
        }

        /// <summary>The line object a prompt took, null where none was.</summary>
        private sealed class LineWayChoice
        {
            internal LineWay? Way { get; set; }
        }

        /// <summary>
        /// The Offset option: a distance, or Through, then each line object with the side to stand at (or the point to run
        /// through), an xline made at each, until Enter. A distance typed or measured between two points is kept for the next
        /// run, <c>Through</c> as well.
        /// </summary>
        private static IEnumerable<CadPrompt> XLineOffset(CadCommandContext c)
        {
            double distance = c.Remembered("xline.offset", -1.0);

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify offset distance",
                    null,
                    new[] { "Through" },
                    distance < 0.0 ? "Through" : Length(distance),
                    true
                );

                if (c.Input.Is("Through") || (c.Input.IsNone && distance < 0.0))
                {
                    distance = -1.0;
                    break;
                }

                if (c.Input.IsNone)
                {
                    break;
                }

                double typed;

                if (c.Input.IsPoint)
                {
                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify second point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    typed = (c.Input.Point - first).Length;
                }
                else if (
                    c.Input.Kind != CadInputKind.Text
                    || !double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out typed)
                    || double.IsNaN(typed)
                    || double.IsInfinity(typed)
                )
                {
                    c.Write("Requires numeric distance, two points, or option keyword.");
                    continue;
                }

                if (!(typed > c.Session.Tolerance.EqualPoint))
                {
                    c.Write("Value must be positive and nonzero.");
                    continue;
                }

                distance = typed;
                break;
            }

            c.Remember("xline.offset", distance);
            bool through = distance < 0.0;
            var chosen = new LineWayChoice();

            while (true)
            {
                foreach (CadPrompt prompt in AskLineObject(c, chosen))
                {
                    yield return prompt;
                }

                if (chosen.Way == null)
                {
                    yield break;
                }

                LineWay line = chosen.Way.Value;

                // The xline the point at the cursor would make: through it, or at the distance to the side it is on.
                DxfEntity Made(GeoPoint2 side) =>
                    OffsetXLine(c, line, side, through ? -1.0 : distance) is DxfXline xline ? Current(c, xline) : null;

                yield return CadPrompt
                    .ForPoint(through ? "Specify through point" : "Specify side to offset")
                    .Previewing(CadPreview.MakingOne(c, input => Made(input.Point)));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                DxfXline made = OffsetXLine(c, line, c.Input.Point, through ? -1.0 : distance);

                if (made != null)
                {
                    c.Apply(null, s => s.Append(c.Space, Current(c, made)));
                }
            }
        }

        /// <summary>
        /// The xline along a line way at a distance to the side of a point, or through the point where the distance is below
        /// nought; null where the point is on the line.
        /// </summary>
        private static DxfXline OffsetXLine(CadCommandContext c, LineWay line, GeoPoint2 side, double distance)
        {
            var normal = new GeoVector2(-line.Way.Y, line.Way.X);
            double across = (side.X - line.Start.X) * normal.X + (side.Y - line.Start.Y) * normal.Y;
            GeoPoint2 base2;

            if (distance < 0.0)
            {
                base2 = side;
            }
            else
            {
                if (Math.Abs(across) <= c.Session.Tolerance.EqualPoint)
                {
                    return null;
                }

                double sign = across > 0.0 ? 1.0 : -1.0;
                base2 = new GeoPoint2(
                    line.Start.X + sign * distance * normal.X,
                    line.Start.Y + sign * distance * normal.Y
                );
            }

            return (DxfXline)Aimed(new DxfXline(), base2, line.Way);
        }
    }
}
