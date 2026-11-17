using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What MEASUREGEOM's Quick option shows under the cursor: the dimensions between the object it is on and the geometry near
    /// it, and, where the cursor is inside a closed boundary, the room's width and height through it, its area and perimeter.
    /// This is the presenter's simple version of AutoCAD 2021's Quick measure: lines, circles, arcs and the straight sides of
    /// lightweight polylines are read, a closed boundary is a closed polyline of straight sides or a circle.
    /// </summary>
    internal static class CadQuickMeasure
    {
        // How far from the cursor, in pixels, other geometry is looked for.
        private const double ReachPixels = 200.0;

        // The most objects looked at for neighbours or for a boundary round the cursor; a busier drawing is read in part.
        private const int MostObjects = 20000;

        /// <summary>One dimension drawn: its line, its text and the points on the geometry its extension lines start from.</summary>
        internal sealed class Dimension
        {
            public GeoPoint2 Start;
            public GeoPoint2 End;
            public string Text;
            public GeoPoint2? Anchor1;
            public GeoPoint2? Anchor2;
        }

        /// <summary>What was read at the cursor: the dimensions to draw and the lines a click writes on the command line.</summary>
        internal sealed class Reading
        {
            public List<Dimension> Dimensions { get; } = new List<Dimension>();

            public List<string> Report { get; } = new List<string>();
        }

        private struct Side
        {
            public GeoPoint2 A;
            public GeoPoint2 B;

            public GeoVector2 Direction => (B - A).Normalize();

            public double Length => (B - A).Length;
        }

        /// <summary>Reads what is under the cursor.</summary>
        /// <param name="document">The document.</param>
        /// <param name="cursor">The cursor, in the units of the space worked in.</param>
        /// <param name="pixel">One pixel of the canvas, in those units.</param>
        /// <param name="angle">Writes an angle in radians as the drawing counts them.</param>
        /// <returns>The reading; empty where there is nothing under the cursor.</returns>
        internal static Reading Read(CadDocument document, GeoPoint2 cursor, double pixel, Func<double, string> angle)
        {
            var reading = new Reading();
            CadSpace space = document.WorkingSpace;
            PickIndex index = document.GetPickIndex(space);
            IReadOnlyList<string> hit = index.Pick(cursor.X, cursor.Y, 6.0 * pixel);
            DxfEntity hovered = hit.Count > 0 ? document.FindEntity(hit[0]) : null;

            if (hovered == null)
            {
                Inside(document, space, cursor, reading);
                return reading;
            }

            if (hovered is DxfCircle circle)
            {
                Radial(reading, new GeoPoint2(circle.Center.X, circle.Center.Y), circle.Radius, cursor, true);
                reading.Report.Add("Circumference = " + Number(2.0 * Math.PI * circle.Radius));
                reading.Report.Add("Area = " + Number(Math.PI * circle.Radius * circle.Radius));
                return reading;
            }

            if (hovered is DxfArc arc)
            {
                double sweep = CadGrips.Sweep(arc.StartAngle, arc.EndAngle);
                Radial(reading, new GeoPoint2(arc.Center.X, arc.Center.Y), arc.Radius, cursor, false);
                reading.Report.Add("Angle = " + angle(sweep));
                reading.Report.Add("Arc length = " + Number(arc.Radius * sweep));
                return reading;
            }

            Side? side = SideUnder(hovered, cursor);

            if (side == null)
            {
                return reading;
            }

            Straight(document, index, hovered, side.Value, cursor, pixel, angle, reading);
            return reading;
        }

        /// <summary>
        /// Makes the entities a reading is drawn as under the cursor: each dimension's line and extension lines, the text beside
        /// it at the height given.
        /// </summary>
        internal static List<DxfEntity> Entities(Reading reading, double height)
        {
            var made = new List<DxfEntity>();

            foreach (Dimension dimension in reading.Dimensions)
            {
                GeoVector2 way = dimension.End - dimension.Start;
                bool line = way.Length > 1E-12;

                if (line)
                {
                    made.Add(new DxfLine(P3(dimension.Start), P3(dimension.End)));
                }

                if (dimension.Anchor1.HasValue && (dimension.Anchor1.Value - dimension.Start).Length > 1E-12)
                {
                    made.Add(new DxfLine(P3(dimension.Anchor1.Value), P3(dimension.Start)));
                }

                if (dimension.Anchor2.HasValue && (dimension.Anchor2.Value - dimension.End).Length > 1E-12)
                {
                    made.Add(new DxfLine(P3(dimension.Anchor2.Value), P3(dimension.End)));
                }

                double rotation = 0.0;
                GeoPoint2 at = dimension.Start;

                if (line)
                {
                    rotation = Math.Atan2(way.Y, way.X);

                    // Written so that it reads from the left, over the line.
                    if (rotation > Math.PI / 2.0 + 1E-9 || rotation <= -Math.PI / 2.0 + 1E-9)
                    {
                        rotation += Math.PI;
                    }

                    GeoVector2 along = (dimension.End - dimension.Start) * 0.5;
                    var over = new GeoVector2(-Math.Sin(rotation), Math.Cos(rotation));
                    at = dimension.Start + along - along.Normalize() * (0.5 * height * dimension.Text.Length * 0.6);
                    at = at + over * (0.4 * height);
                }

                var text = new DxfDBText
                {
                    TextString = dimension.Text,
                    Position = P3(at),
                    Height = height,
                    Rotation = rotation,
                };
                made.Add(text);
            }

            return made;
        }

        private static void Straight(
            CadDocument document,
            PickIndex index,
            DxfEntity hovered,
            Side side,
            GeoPoint2 cursor,
            double pixel,
            Func<double, string> angle,
            Reading reading
        )
        {
            GeoVector2 u = side.Direction;
            var n = new GeoVector2(-u.Y, u.X);
            double along = (cursor - side.A).X * u.X + (cursor - side.A).Y * u.Y;
            double toward = (cursor - side.A).X * n.X + (cursor - side.A).Y * n.Y;
            double lift = (toward < 0.0 ? -1.0 : 1.0) * 14.0 * pixel;
            GeoVector2 shift = n * lift;
            reading.Dimensions.Add(
                new Dimension
                {
                    Start = side.A + shift,
                    End = side.B + shift,
                    Text = Number(side.Length),
                    Anchor1 = side.A,
                    Anchor2 = side.B,
                }
            );
            double direction = Math.Atan2(u.Y, u.X);
            reading.Report.Add("Length = " + Number(side.Length));
            reading.Report.Add("Angle in XY Plane = " + angle(direction < 0.0 ? direction + Math.PI * 2.0 : direction));

            double reach = ReachPixels * pixel;
            var box = new DisplayBox(cursor.X - reach, cursor.Y - reach, cursor.X + reach, cursor.Y + reach);
            double meet = 1E-6 * Math.Max(1.0, side.Length);
            double best = double.PositiveInfinity;
            Dimension nearest = null;
            int seen = 0;

            foreach (string handle in index.Crossing(box))
            {
                DxfEntity other = document.FindEntity(handle);

                if (other == null || ReferenceEquals(other, hovered) || ++seen > MostObjects)
                {
                    continue;
                }

                foreach (Side candidate in Sides(other))
                {
                    if (candidate.Length < 1E-12)
                    {
                        continue;
                    }

                    // A side that shares an end with the hovered one makes an angle there.
                    for (int e = 0; e < 2; e++)
                    {
                        GeoPoint2 end = e == 0 ? side.A : side.B;
                        bool atA = (candidate.A - end).Length <= meet;
                        bool atB = (candidate.B - end).Length <= meet;

                        if (atA || atB)
                        {
                            // Each way points away from the corner they share.
                            GeoVector2 mine = e == 0 ? u : -u;
                            GeoVector2 theirs = atA ? candidate.Direction : -candidate.Direction;
                            double between = Math.Acos(
                                Math.Max(-1.0, Math.Min(1.0, mine.X * theirs.X + mine.Y * theirs.Y))
                            );
                            var label = end + (mine + theirs) * (8.0 * pixel);
                            reading.Dimensions.Add(
                                new Dimension
                                {
                                    Start = label,
                                    End = label,
                                    Text = angle(between),
                                }
                            );
                            reading.Report.Add("Angle = " + angle(between));
                        }
                    }

                    // A parallel side whose span the cursor stands within: the distance across to it.
                    GeoVector2 v = candidate.Direction;
                    double cross = u.X * v.Y - u.Y * v.X;

                    if (Math.Abs(cross) > 1E-6)
                    {
                        continue;
                    }

                    double gap = (candidate.A - side.A).X * n.X + (candidate.A - side.A).Y * n.Y;
                    double a0 = (candidate.A - side.A).X * u.X + (candidate.A - side.A).Y * u.Y;
                    double a1 = (candidate.B - side.A).X * u.X + (candidate.B - side.A).Y * u.Y;

                    if (
                        Math.Abs(gap) < 2.0 * pixel
                        || along < Math.Min(a0, a1) - 1E-9
                        || along > Math.Max(a0, a1) + 1E-9
                        || along < -1E-9
                        || along > side.Length + 1E-9
                        || Math.Abs(gap) >= best
                    )
                    {
                        continue;
                    }

                    // Only those on the cursor's side of the hovered one.
                    if ((gap < 0.0) != (toward < 0.0) && Math.Abs(toward) > 2.0 * pixel)
                    {
                        continue;
                    }

                    best = Math.Abs(gap);
                    GeoPoint2 on = side.A + u * along;
                    nearest = new Dimension
                    {
                        Start = on,
                        End = on + n * gap,
                        Text = Number(Math.Abs(gap)),
                    };
                }
            }

            if (nearest != null)
            {
                reading.Dimensions.Add(nearest);
                reading.Report.Add("Distance = " + nearest.Text);
            }
        }

        /// <summary>A circle's or an arc's radius, drawn from the centre towards the cursor, then the circle's diameter or nothing.</summary>
        private static void Radial(Reading reading, GeoPoint2 center, double radius, GeoPoint2 cursor, bool whole)
        {
            GeoVector2 way = cursor - center;
            GeoVector2 u = way.Length < 1E-12 ? new GeoVector2(1.0, 0.0) : way.Normalize();
            reading.Dimensions.Add(
                new Dimension
                {
                    Start = center,
                    End = center + u * radius,
                    Text = "R " + Number(radius),
                }
            );
            reading.Report.Add("Radius = " + Number(radius));

            if (whole)
            {
                reading.Report.Add("Diameter = " + Number(2.0 * radius));
            }
        }

        /// <summary>
        /// The cursor is on nothing: the smallest closed boundary round it, if there is one, gives its width and height through
        /// the cursor, its area and its perimeter.
        /// </summary>
        private static void Inside(CadDocument document, CadSpace space, GeoPoint2 cursor, Reading reading)
        {
            double bestArea = double.PositiveInfinity;
            List<GeoPoint2> room = null;
            GeoPoint2 centre = default(GeoPoint2);
            double round = 0.0;
            int seen = 0;

            foreach (DxfEntity entity in space.Block)
            {
                if (entity.IsErased || ++seen > MostObjects)
                {
                    continue;
                }

                if (entity is DxfCircle circle)
                {
                    var middle = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    double area = Math.PI * circle.Radius * circle.Radius;

                    if ((cursor - middle).Length < circle.Radius && area < bestArea)
                    {
                        bestArea = area;
                        room = null;
                        centre = middle;
                        round = circle.Radius;
                    }
                }
                else if (entity is DxfPolyline polyline && polyline.Closed && polyline.NumberOfVertices >= 3)
                {
                    List<GeoPoint2> corners = StraightCorners(polyline);

                    if (corners == null)
                    {
                        continue;
                    }

                    double area = Math.Abs(Shoelace(corners));

                    if (area < bestArea && Contains(corners, cursor))
                    {
                        bestArea = area;
                        room = corners;
                        round = 0.0;
                    }
                }
            }

            if (double.IsPositiveInfinity(bestArea))
            {
                return;
            }

            double left;
            double right;
            double below;
            double above;

            if (room == null)
            {
                double dx = Math.Sqrt(Math.Max(0.0, round * round - (cursor.Y - centre.Y) * (cursor.Y - centre.Y)));
                double dy = Math.Sqrt(Math.Max(0.0, round * round - (cursor.X - centre.X) * (cursor.X - centre.X)));
                left = centre.X - dx;
                right = centre.X + dx;
                below = centre.Y - dy;
                above = centre.Y + dy;
                reading.Report.Add("Perimeter = " + Number(2.0 * Math.PI * round));
            }
            else
            {
                Span(room, cursor, true, out left, out right);
                Span(room, cursor, false, out below, out above);
                double perimeter = 0.0;

                for (int i = 0; i < room.Count; i++)
                {
                    perimeter += (room[(i + 1) % room.Count] - room[i]).Length;
                }

                reading.Report.Add("Perimeter = " + Number(perimeter));
            }

            reading.Report.Insert(0, "Area = " + Number(bestArea));
            reading.Dimensions.Add(
                new Dimension
                {
                    Start = new GeoPoint2(left, cursor.Y),
                    End = new GeoPoint2(right, cursor.Y),
                    Text = Number(right - left),
                }
            );
            reading.Dimensions.Add(
                new Dimension
                {
                    Start = new GeoPoint2(cursor.X, below),
                    End = new GeoPoint2(cursor.X, above),
                    Text = Number(above - below),
                }
            );
            reading.Dimensions.Add(
                new Dimension
                {
                    Start = cursor,
                    End = cursor,
                    Text = "Area " + Number(bestArea),
                }
            );
        }

        /// <summary>The corners of a closed polyline with no arc in it; null for one with an arc or a normal away from the plan.</summary>
        private static List<GeoPoint2> StraightCorners(DxfPolyline polyline)
        {
            if (Math.Abs(polyline.Normal.Z - 1.0) > 1E-9)
            {
                return null;
            }

            var corners = new List<GeoPoint2>();

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (Math.Abs(polyline.GetBulgeAt(i)) > 1E-12)
                {
                    return null;
                }

                corners.Add(polyline.GetPoint2dAt(i));
            }

            return corners;
        }

        private static double Shoelace(List<GeoPoint2> corners)
        {
            double sum = 0.0;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint2 p = corners[i];
                GeoPoint2 q = corners[(i + 1) % corners.Count];
                sum += p.X * q.Y - q.X * p.Y;
            }

            return sum / 2.0;
        }

        /// <summary>Whether a point is inside a polygon, by counting where a ray to the right crosses its sides.</summary>
        private static bool Contains(List<GeoPoint2> corners, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
            {
                GeoPoint2 p = corners[i];
                GeoPoint2 q = corners[j];

                if ((p.Y > point.Y) != (q.Y > point.Y) && point.X < (q.X - p.X) * (point.Y - p.Y) / (q.Y - p.Y) + p.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Where the line through the cursor, across (horizontal) or up (vertical), meets the polygon on either side of it: the
        /// nearest crossing before and after the cursor's coordinate along the line.
        /// </summary>
        private static void Span(
            List<GeoPoint2> corners,
            GeoPoint2 cursor,
            bool horizontal,
            out double before,
            out double after
        )
        {
            double at = horizontal ? cursor.X : cursor.Y;
            before = double.NegativeInfinity;
            after = double.PositiveInfinity;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint2 p = corners[i];
                GeoPoint2 q = corners[(i + 1) % corners.Count];
                double a0 = horizontal ? p.Y : p.X;
                double a1 = horizontal ? q.Y : q.X;
                double fixedAt = horizontal ? cursor.Y : cursor.X;

                if ((a0 > fixedAt) == (a1 > fixedAt))
                {
                    continue;
                }

                double t = (fixedAt - a0) / (a1 - a0);
                double hit = horizontal ? p.X + t * (q.X - p.X) : p.Y + t * (q.Y - p.Y);

                if (hit <= at && hit > before)
                {
                    before = hit;
                }
                else if (hit > at && hit < after)
                {
                    after = hit;
                }
            }
        }

        /// <summary>The straight side of an object the cursor is nearest to; null for an object with none.</summary>
        private static Side? SideUnder(DxfEntity entity, GeoPoint2 cursor)
        {
            Side? best = null;
            double nearest = double.PositiveInfinity;

            foreach (Side side in Sides(entity))
            {
                double distance = DistanceTo(side, cursor);

                if (distance < nearest)
                {
                    nearest = distance;
                    best = side;
                }
            }

            return best;
        }

        /// <summary>The straight sides of a line or a lightweight polyline in the plan, in the order of the object.</summary>
        private static IEnumerable<Side> Sides(DxfEntity entity)
        {
            if (entity is DxfLine line)
            {
                yield return new Side
                {
                    A = new GeoPoint2(line.StartPoint.X, line.StartPoint.Y),
                    B = new GeoPoint2(line.EndPoint.X, line.EndPoint.Y),
                };
            }
            else if (entity is DxfPolyline polyline && Math.Abs(polyline.Normal.Z - 1.0) <= 1E-9)
            {
                int count = polyline.NumberOfVertices;
                int last = polyline.Closed ? count : count - 1;

                for (int i = 0; i < last; i++)
                {
                    if (Math.Abs(polyline.GetBulgeAt(i)) > 1E-12)
                    {
                        continue;
                    }

                    yield return new Side { A = polyline.GetPoint2dAt(i), B = polyline.GetPoint2dAt((i + 1) % count) };
                }
            }
        }

        private static double DistanceTo(Side side, GeoPoint2 point)
        {
            GeoVector2 way = side.B - side.A;
            double length2 = way.X * way.X + way.Y * way.Y;
            double t = length2 < 1E-24 ? 0.0 : ((point - side.A).X * way.X + (point - side.A).Y * way.Y) / length2;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return (point - (side.A + way * t)).Length;
        }

        private static GeoPoint3 P3(GeoPoint2 point) => new GeoPoint3(point.X, point.Y, 0.0);

        private static string Number(double value) => CadCommandContext.Length(value);
    }
}
