using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The draw commands of the Draw panel's second row: XLINE and RAY, lines without end; POLYGON, a closed polyline of 3 to
    /// 1024 equal sides; DONUT, a closed polyline of two arcs as wide as the ring, so that a donut of inside diameter 10 and
    /// outside diameter 20 is a polyline of width 5 round a circle of diameter 15.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>XLINE through points, horizontal, vertical, at an angle or bisecting one, each through-point a line, until Enter.</summary>
        private static IEnumerable<CadPrompt> XLine(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify a point", null, new[] { "Hor", "Ver", "Ang", "Bisect" });
            GeoPoint2? from = null;
            GeoVector2 way = new GeoVector2(1.0, 0.0);

            if (c.Input.IsPoint)
            {
                from = c.Input.Point;
            }
            else if (c.Input.Is("Ver"))
            {
                way = new GeoVector2(0.0, 1.0);
            }
            else if (c.Input.Is("Ang"))
            {
                yield return CadPrompt.ForAngle("Enter angle of xline (" + Counted(c, 0.0) + ")");

                if (c.Input.Kind == CadInputKind.Number)
                {
                    way = new GeoVector2(Math.Cos(c.Input.Number), Math.Sin(c.Input.Number));
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }
            }
            else if (c.Input.Is("Bisect"))
            {
                yield return CadPrompt.ForPoint("Specify angle vertex point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 vertex = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify angle start point", vertex);

                if (!c.Input.IsPoint || (c.Input.Point - vertex).Length <= c.Session.Tolerance.EqualPoint)
                {
                    yield break;
                }

                GeoVector2 start = (c.Input.Point - vertex) / (c.Input.Point - vertex).Length;

                // The half of the turn counter-clockwise from the start point's way to the end point's.
                GeoVector2 Bisecting(GeoPoint2 end)
                {
                    double half =
                        CadGrips.Sweep(Math.Atan2(start.Y, start.X), Math.Atan2(end.Y - vertex.Y, end.X - vertex.X))
                        / 2.0;
                    double angle = Math.Atan2(start.Y, start.X) + half;
                    return new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                }

                while (true)
                {
                    yield return CadPrompt
                        .ForPoint("Specify angle end point", vertex)
                        .Previewing(
                            CadPreview.MakingOne(c, input =>
                                Current(c, Aimed(new DxfXline(), vertex, Bisecting(input.Point)))
                            )
                        );

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    AddEndless(c, new DxfXline(), vertex, Bisecting(c.Input.Point));
                }
            }
            else if (!c.Input.Is("Hor"))
            {
                yield break;
            }

            // Hor, Ver and Ang are along the axes of the UCS and from its X axis.
            way = c.Session.Ucs.ToWorld(way);
            GeoPoint2? through = from;
            GeoVector2 along = way;

            // The XLINE through the point at the cursor, as it is made: along the way of Hor, Ver or Ang, or from the first point.
            DxfEntity Previewed(GeoPoint2 at) =>
                through == null ? Current(c, Aimed(new DxfXline(), at, along))
                : (at - through.Value).Length > c.Session.Tolerance.EqualPoint
                    ? Current(c, Aimed(new DxfXline(), through.Value, at - through.Value))
                : null;

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify through point", from)
                    .Previewing(CadPreview.MakingOne(c, input => Previewed(input.Point)));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                if (from == null)
                {
                    AddEndless(c, new DxfXline(), c.Input.Point, way);
                }
                else if ((c.Input.Point - from.Value).Length > c.Session.Tolerance.EqualPoint)
                {
                    AddEndless(c, new DxfXline(), from.Value, c.Input.Point - from.Value);
                }
            }
        }

        /// <summary>RAY from a start point through each point given, until Enter.</summary>
        private static IEnumerable<CadPrompt> Ray(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 from = c.Input.Point;

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify through point", from)
                    .Previewing(
                        CadPreview.MakingOne(c, input =>
                            (input.Point - from).Length > c.Session.Tolerance.EqualPoint
                                ? Current(c, Aimed(new DxfRay(), from, input.Point - from))
                                : null
                        )
                    );

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                if ((c.Input.Point - from).Length > c.Session.Tolerance.EqualPoint)
                {
                    AddEndless(c, new DxfRay(), from, c.Input.Point - from);
                }
            }
        }

        private static void AddEndless(CadCommandContext c, DxfCurve endless, GeoPoint2 at, GeoVector2 way)
        {
            Aimed(endless, at, way);
            c.Apply(null, s => s.Append(c.Space, Current(c, endless)));
        }

        /// <summary>An XLINE or a RAY placed through a point along a way, as XLINE and RAY make and preview it.</summary>
        private static DxfCurve Aimed(DxfCurve endless, GeoPoint2 at, GeoVector2 way)
        {
            var direction = new GeoVector3(way.X, way.Y, 0.0);

            if (endless is DxfRay ray)
            {
                ray.BasePoint = P3(at);
                ray.UnitDir = direction;
            }
            else if (endless is DxfXline xline)
            {
                xline.BasePoint = P3(at);
                xline.UnitDir = direction;
            }

            return endless;
        }

        /// <summary>
        /// POLYGON of 3 to 1024 sides about a centre, inscribed in a circle or circumscribed about it, or on an edge given: a
        /// radius picked turns a corner (inscribed) or the middle of a side (circumscribed) towards the point, a radius typed lays
        /// the bottom side level, as AutoCAD lays it.
        /// </summary>
        private static IEnumerable<CadPrompt> Polygon(CadCommandContext c)
        {
            int sides = (int)c.Remembered("polygon.sides", 4.0);

            while (true)
            {
                yield return CadPrompt.ForInteger(
                    "Enter number of sides",
                    null,
                    sides.ToString(CultureInfo.InvariantCulture)
                );

                if (c.Input.IsNone)
                {
                    break;
                }

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= 3 && c.Input.Number <= 1024)
                {
                    sides = (int)c.Input.Number;
                    break;
                }

                c.Write("Requires an integer between 3 and 1024.");
            }

            c.Remember("polygon.sides", sides);
            yield return CadPrompt.ForPoint("Specify center of polygon", null, new[] { "Edge" });
            var corners = new GeoPoint2[sides];
            double step = 2.0 * Math.PI / sides;

            if (c.Input.Is("Edge"))
            {
                yield return CadPrompt.ForPoint("Specify first endpoint of edge");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt
                    .ForPoint("Specify second endpoint of edge", first)
                    .Previewing(
                        CadPreview.MakingOne(c, input =>
                            (input.Point - first).Length <= c.Session.Tolerance.EqualPoint
                                ? null
                                : PolygonOf(c, OnEdge(first, input.Point, sides))
                        )
                    );

                if (!c.Input.IsPoint || (c.Input.Point - first).Length <= c.Session.Tolerance.EqualPoint)
                {
                    yield break;
                }

                corners = OnEdge(first, c.Input.Point, sides);
            }
            else if (c.Input.IsPoint)
            {
                GeoPoint2 center = c.Input.Point;
                string offered = c.Remembered("polygon.circumscribed", 0.0) != 0.0 ? "C" : "I";
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "Inscribed in circle", "Circumscribed about circle" },
                    offered
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                bool about = c.Input.Is("Circumscribed about circle");
                c.Remember("polygon.circumscribed", about ? 1.0 : 0.0);

                // A radius picked points a corner, or the middle of a side, at the point; one typed lays the bottom side level.
                GeoPoint2[] Round(CadInput input)
                {
                    var round = new GeoPoint2[sides];
                    double toward = Typed(input)
                        ? -Math.PI / 2.0 + (about ? 0.0 : step / 2.0)
                        : Math.Atan2(input.Point.Y - center.Y, input.Point.X - center.X);
                    double first = about ? toward - step / 2.0 : toward;
                    double reach = Math.Abs(input.Number) / (about ? Math.Cos(step / 2.0) : 1.0);

                    for (int i = 0; i < sides; i++)
                    {
                        double angle = first + i * step;
                        round[i] = new GeoPoint2(
                            center.X + reach * Math.Cos(angle),
                            center.Y + reach * Math.Sin(angle)
                        );
                    }

                    return round;
                }

                yield return CadPrompt
                    .ForDistance("Specify radius of circle", center)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input =>
                                Math.Abs(input.Number) <= c.Session.Tolerance.EqualPoint
                                    ? null
                                    : PolygonOf(c, Round(input)),
                            true
                        )
                    );

                if (c.Input.Kind != CadInputKind.Number || Math.Abs(c.Input.Number) <= c.Session.Tolerance.EqualPoint)
                {
                    yield break;
                }

                corners = Round(c.Input);
            }
            else
            {
                yield break;
            }

            c.Apply(null, s => s.Append(c.Space, OfPlineType(c, PolygonOf(c, corners))));
        }

        /// <summary>The corners of a polygon on an edge given, counter-clockwise from it, each side the last turned by a sides' share of a turn.</summary>
        private static GeoPoint2[] OnEdge(GeoPoint2 first, GeoPoint2 second, int sides)
        {
            var corners = new GeoPoint2[sides];
            double step = 2.0 * Math.PI / sides;
            GeoVector2 side = second - first;
            corners[0] = first;

            for (int i = 1; i < sides; i++)
            {
                corners[i] = corners[i - 1] + side;
                side = new GeoVector2(
                    side.X * Math.Cos(step) - side.Y * Math.Sin(step),
                    side.X * Math.Sin(step) + side.Y * Math.Cos(step)
                );
            }

            return corners;
        }

        /// <summary>The closed polyline of a polygon's corners, as POLYGON makes and previews it.</summary>
        private static DxfPolyline PolygonOf(CadCommandContext c, GeoPoint2[] corners)
        {
            var polygon = new DxfPolyline();

            for (int i = 0; i < corners.Length; i++)
            {
                polygon.AddVertexAt(i, corners[i], 0.0, 0.0, 0.0);
            }

            polygon.Closed = true;
            return Current(c, polygon);
        }

        /// <summary>DONUT: the two diameters, AutoCAD's 0.5 and 1 until changed, then a donut at each centre given until Enter.</summary>
        private static IEnumerable<CadPrompt> Donut(CadCommandContext c)
        {
            double inside = c.Remembered("donut.inside", 0.5);
            double outside = c.Remembered("donut.outside", 1.0);
            yield return CadPrompt.ForDistance("Specify inside diameter of donut", null, null, Length(inside));

            if (c.Input.Kind == CadInputKind.Number)
            {
                inside = Math.Abs(c.Input.Number);
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            yield return CadPrompt.ForDistance("Specify outside diameter of donut", null, null, Length(outside));

            if (c.Input.Kind == CadInputKind.Number)
            {
                outside = Math.Abs(c.Input.Number);
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            if (Math.Max(inside, outside) <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The donut has no size.");
                yield break;
            }

            c.Remember("donut.inside", inside);
            c.Remember("donut.outside", outside);

            // The ring's middle circle, drawn as two half circles as wide as the ring; an inside of nought fills it.
            double radius = (inside + outside) / 4.0;
            double width = Math.Abs(outside - inside) / 2.0;

            // The donut about a centre, as it is dragged at the cursor and made.
            DxfPolyline Make(GeoPoint2 center)
            {
                var donut = new DxfPolyline();
                donut.AddVertexAt(0, new GeoPoint2(center.X - radius, center.Y), 1.0, 0.0, 0.0);
                donut.AddVertexAt(1, new GeoPoint2(center.X + radius, center.Y), 1.0, 0.0, 0.0);
                donut.Closed = true;
                donut.ConstantWidth = width;
                return Current(c, donut);
            }

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify center of donut or <exit>")
                    .Previewing(CadPreview.MakingOne(c, input => Make(input.Point)));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 at = c.Input.Point;
                c.Apply(null, s => s.Append(c.Space, OfPlineType(c, Make(at))));
            }
        }

        /// <summary>Whether a number was typed, rather than a point picked or typed, as a radius typed lays a polygon level.</summary>
        private static bool Typed(CadInput input) =>
            input.Text != null
            && double.TryParse(input.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
