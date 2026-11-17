using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TRACE: a path of wide segments, each a four-cornered filled area (a SOLID) of the width given. It asks "Specify trace
    /// width &lt;0.0500&gt;:" (the width kept in the header as <c>$TRACEWID</c>, nought allowed, below nought refused with "Value
    /// must be positive."), "Specify start point:" and "Specify next point:" until Enter. Two segments meet in a mitre: the
    /// corners of the first's end are those of the second's start, where the edges of the two meet, as AutoCAD's do; the first
    /// segment's start and the last one's end are square to the path. A trace of 3 wide from (0, 0) to (100, 0) to (100, 50)
    /// is two solids, the first with corners (0, 1.5), (0, -1.5), (98.5, 1.5) and (101.5, -1.5), the second with
    /// (98.5, 1.5), (101.5, -1.5), (98.5, 50) and (101.5, 50). AutoCAD writes a TRACE entity of the same four corners; the
    /// presenter writes a SOLID, which the library reads, draws and keeps as the same shape (FILL off draws its outline).
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Trace(CadCommandContext c)
        {
            double width = c.Database.Tracewid;

            while (true)
            {
                yield return CadPrompt.ForDistance("Specify trace width", null, null, Length(width));

                if (c.Input.Kind == CadInputKind.Number && c.Input.Number < 0.0)
                {
                    c.Write("Value must be positive.");
                    continue;
                }

                break;
            }

            if (c.Input.Kind == CadInputKind.Number)
            {
                double typed = c.Input.Number;
                SetHeader(c, "$TRACEWID", width, typed, d => d.Tracewid = typed);
                width = typed;
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };
            double half = width / 2.0;
            int made = 0;

            while (true)
            {
                int already = made;
                List<GeoPoint2> have = points;
                yield return CadPrompt
                    .ForPoint("Specify next point", points[points.Count - 1])
                    .Previewing(CadPreview.Making(c, input =>
                        {
                            var ahead = new List<GeoPoint2>(have);

                            if (!SamePlace(c, ahead[ahead.Count - 1], input.Point))
                            {
                                ahead.Add(input.Point);
                            }

                            return TraceSolids(c, ahead, half, already, ahead.Count - 1);
                        }));

                if (!c.Input.IsPoint)
                {
                    break;
                }

                if (SamePlace(c, points[points.Count - 1], c.Input.Point))
                {
                    continue;
                }

                points.Add(c.Input.Point);

                // A segment is finished once the next point gives its end corners; the last waits for Enter.
                int upTo = points.Count - 2;

                if (upTo > made)
                {
                    List<DxfSolid> done = TraceSolids(c, points, half, made, upTo);
                    made = upTo;
                    c.Apply(null, s =>
                    {
                        foreach (DxfSolid solid in done)
                        {
                            s.Append(c.Space, solid);
                        }
                    });
                }
            }

            if (points.Count - 1 > made)
            {
                List<DxfSolid> rest = TraceSolids(c, points, half, made, points.Count - 1);
                c.Apply(null, s =>
                {
                    foreach (DxfSolid solid in rest)
                    {
                        s.Append(c.Space, solid);
                    }
                });
            }
        }

        private static bool SamePlace(CadCommandContext c, GeoPoint2 a, GeoPoint2 b) =>
            (a - b).Length <= c.Session.Tolerance.EqualPoint;

        /// <summary>
        /// The solids of the segments <paramref name="from"/> up to (not including) <paramref name="to"/> of a path, segment k
        /// running from point k to point k + 1: each with the corners of its start and end as <see cref="TraceCorners"/> gives
        /// them, so that one segment's end is the next one's start.
        /// </summary>
        private static List<DxfSolid> TraceSolids(
            CadCommandContext c,
            IReadOnlyList<GeoPoint2> points,
            double half,
            int from,
            int to
        )
        {
            var solids = new List<DxfSolid>();

            for (int k = from; k < to && k + 1 < points.Count; k++)
            {
                TraceCorners(points, k, half, out GeoPoint2 startLeft, out GeoPoint2 startRight);
                TraceCorners(points, k + 1, half, out GeoPoint2 endLeft, out GeoPoint2 endRight);
                solids.Add(Current(c, new DxfSolid(P3(startLeft), P3(startRight), P3(endLeft), P3(endRight))));
            }

            return solids;
        }

        /// <summary>
        /// The two corners of a trace at a point of the path, to the left and to the right of the way it goes: square to the
        /// path at the first and the last point, and at a point between two segments the corners where their edges meet (the
        /// mitre), the edges of the two being the offsets of their lines by half the width; two segments that double back on
        /// each other are cut square, as there is no mitre.
        /// </summary>
        private static void TraceCorners(
            IReadOnlyList<GeoPoint2> points,
            int at,
            double half,
            out GeoPoint2 left,
            out GeoPoint2 right
        )
        {
            GeoVector2 before = at > 0 ? Unit(points[at] - points[at - 1]) : Unit(points[1] - points[0]);
            GeoVector2 after = at + 1 < points.Count ? Unit(points[at + 1] - points[at]) : before;
            var n1 = new GeoVector2(-before.Y, before.X);
            var n2 = new GeoVector2(-after.Y, after.X);
            double along = 1.0 + (n1.X * n2.X + n1.Y * n2.Y);
            GeoVector2 miter = along > 1E-9 ? (n1 + n2) * (half / along) : n1 * half;
            left = points[at] + miter;
            right = points[at] - miter;
        }

        private static GeoVector2 Unit(GeoVector2 way) => way / way.Length;
    }
}
