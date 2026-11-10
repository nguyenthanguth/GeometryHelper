using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The circles CIRCLE draws touching objects: Ttr, tangent to two at a radius, and Tan, Tan, Tan through 3P.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// Asks for one point a circle passes through or one object it touches: a point picked; or, where the prompt takes a
        /// tangent only or <c>_tan</c> was typed first, a point on a line, an arc or a circle, said so where it is on none. Adds
        /// what was given to the list, nothing where the command ends.
        /// </summary>
        private static IEnumerable<CadPrompt> AskTouch(CadCommandContext c, string message, GeoPoint2? basePoint, List<Touch> touches, bool tangentOnly, Func<Touch, DxfEntity> preview = null)
        {
            bool tangent = tangentOnly;

            while (true)
            {
                CadPrompt asked = tangent && !tangentOnly ? CadPrompt.ForPoint("to", basePoint) : CadPrompt.ForPoint(message, basePoint, null, null, !tangentOnly);

                // The circle the point at the cursor would give, or the object under it where a tangent is asked.
                if (preview != null)
                {
                    bool touching = tangent;
                    asked.Previewing(CadPreview.MakingOne(c, input =>
                    {
                        DxfEntity under = touching ? TouchedAt(c, input.Point) : null;
                        return touching && under == null ? null : preview(new Touch(message, input.Point, under));
                    }));
                }

                yield return asked;

                if (c.Input.Kind == CadInputKind.Text && !tangent)
                {
                    string typed = c.Input.Text.Trim();

                    if (string.Equals(typed, "_tan", StringComparison.OrdinalIgnoreCase) || string.Equals(typed, "tan", StringComparison.OrdinalIgnoreCase))
                    {
                        tangent = true;
                        continue;
                    }

                    c.Write("Invalid point.");
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 pick = c.Input.Point;

                if (!tangent)
                {
                    touches.Add(new Touch(message, pick, null));
                    yield break;
                }

                DxfEntity touched = TouchedAt(c, pick);

                if (touched == null)
                {
                    c.Write("*Requires a TAN object-snap and selection of Circle, Arc, or Line.");
                    tangent = tangentOnly;
                    continue;
                }

                touches.Add(new Touch(message, pick, touched));
                yield break;
            }
        }

        /// <summary>The line, arc or circle of the plan nearest a point within the pick aperture; null where there is none.</summary>
        private static DxfEntity TouchedAt(CadCommandContext c, GeoPoint2 at)
        {
            foreach (string handle in c.Document.GetPickIndex(c.Space).Pick(at.X, at.Y, c.Session.PickAperture))
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity is DxfLine line && (P2(line.EndPoint) - P2(line.StartPoint)).Length > c.Session.Tolerance.EqualPoint)
                {
                    return entity;
                }

                if ((entity is DxfArc || entity is DxfCircle) && Flat(entity))
                {
                    return entity;
                }
            }

            return null;
        }

        /// <summary>
        /// The circle through the points and touching the objects of three given, at least one an object: of the circles each
        /// way of touching gives (outside or inside a circle, either side of a line), found by Newton's method from the middle of
        /// the picks, the one whose points of touching lie nearest the picks, ties to the least centre X, then Y, then radius.
        /// </summary>
        private static bool CircleTouching(List<Touch> touches, Tolerance tolerance, out GeoPoint2 center, out double radius)
        {
            center = default;
            radius = 0.0;
            double best = double.PositiveInfinity;
            double mx = (touches[0].Pick.X + touches[1].Pick.X + touches[2].Pick.X) / 3.0;
            double my = (touches[0].Pick.Y + touches[1].Pick.Y + touches[2].Pick.Y) / 3.0;
            double spread = 0.0;

            foreach (Touch touch in touches)
            {
                spread = Math.Max(spread, (touch.Pick - new GeoPoint2(mx, my)).Length);
            }

            spread = Math.Max(spread, tolerance.EqualPoint);

            // Each of the eight ways of touching, from a start at the middle of the picks and a start at each pick.
            for (int sides = 0; sides < 8; sides++)
            {
                var starts = new List<GeoPoint2> { new GeoPoint2(mx, my) };

                foreach (Touch touch in touches)
                {
                    starts.Add(touch.Pick);
                }

                foreach (GeoPoint2 start in starts)
                {
                    if (!Solve(touches, sides, start, spread, tolerance, out GeoPoint2 at, out double r))
                    {
                        continue;
                    }

                    double score = 0.0;

                    foreach (Touch touch in touches)
                    {
                        score += (touch.Foot(at, r) - touch.Pick).Length;
                    }

                    bool better = score < best - tolerance.EqualPoint
                        || (Math.Abs(score - best) <= tolerance.EqualPoint && (at.X < center.X - tolerance.EqualPoint
                            || (Math.Abs(at.X - center.X) <= tolerance.EqualPoint && (at.Y < center.Y - tolerance.EqualPoint
                                || (Math.Abs(at.Y - center.Y) <= tolerance.EqualPoint && r < radius)))));

                    if (better)
                    {
                        best = score;
                        center = at;
                        radius = r;
                    }
                }
            }

            return !double.IsPositiveInfinity(best);
        }

        /// <summary>Newton's method on the three conditions of touching, one way of touching each (a bit of the sides each); false where it does not settle.</summary>
        private static bool Solve(List<Touch> touches, int sides, GeoPoint2 start, double spread, Tolerance tolerance, out GeoPoint2 center, out double radius)
        {
            double x = start.X;
            double y = start.Y;
            double r = spread / 2.0;
            var f = new double[3];
            var j = new double[3, 3];

            for (int step = 0; step < 100; step++)
            {
                for (int i = 0; i < 3; i++)
                {
                    touches[i].Condition(x, y, r, (sides >> i & 1) == 0 ? 1.0 : -1.0, out f[i], out j[i, 0], out j[i, 1], out j[i, 2]);
                }

                double det = j[0, 0] * (j[1, 1] * j[2, 2] - j[1, 2] * j[2, 1]) - j[0, 1] * (j[1, 0] * j[2, 2] - j[1, 2] * j[2, 0]) + j[0, 2] * (j[1, 0] * j[2, 1] - j[1, 1] * j[2, 0]);

                if (Math.Abs(det) < 1E-300)
                {
                    break;
                }

                // Cramer's rule for the step.
                double dx = (f[0] * (j[1, 1] * j[2, 2] - j[1, 2] * j[2, 1]) - j[0, 1] * (f[1] * j[2, 2] - j[1, 2] * f[2]) + j[0, 2] * (f[1] * j[2, 1] - j[1, 1] * f[2])) / det;
                double dy = (j[0, 0] * (f[1] * j[2, 2] - j[1, 2] * f[2]) - f[0] * (j[1, 0] * j[2, 2] - j[1, 2] * j[2, 0]) + j[0, 2] * (j[1, 0] * f[2] - f[1] * j[2, 0])) / det;
                double dr = (j[0, 0] * (j[1, 1] * f[2] - f[1] * j[2, 1]) - j[0, 1] * (j[1, 0] * f[2] - f[1] * j[2, 0]) + f[0] * (j[1, 0] * j[2, 1] - j[1, 1] * j[2, 0])) / det;
                x -= dx;
                y -= dy;
                r -= dr;

                if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(r) || Math.Abs(r) > 1E6 * spread)
                {
                    break;
                }

                if (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dr) <= 1E-12 * spread)
                {
                    break;
                }
            }

            center = new GeoPoint2(x, y);
            radius = r;

            if (!(r > tolerance.EqualPoint))
            {
                return false;
            }

            foreach (Touch touch in touches)
            {
                if (Math.Abs((touch.Foot(center, r) - center).Length - r) > tolerance.EqualPoint || !touch.Touches(center, r, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>One thing a circle of CIRCLE passes through or touches: the point picked, and the line, arc or circle picked where it touches one.</summary>
        private sealed class Touch
        {
            private readonly bool _line;
            private readonly GeoPoint2 _origin;
            private readonly GeoVector2 _way;
            private readonly double _radius;

            public Touch(string asked, GeoPoint2 pick, DxfEntity entity)
            {
                Asked = asked;
                Pick = pick;

                if (entity == null)
                {
                    _origin = pick;
                    return;
                }

                Shape = Shape.Of(entity);
                IsObject = true;

                if (entity is DxfLine line)
                {
                    _line = true;
                    _origin = P2(line.StartPoint);
                    GeoVector2 way = P2(line.EndPoint) - _origin;
                    _way = way / way.Length;
                }
                else
                {
                    _origin = P2(entity is DxfArc arc ? arc.Center : ((DxfCircle)entity).Center);
                    _radius = entity is DxfArc round ? round.Radius : ((DxfCircle)entity).Radius;
                }
            }

            /// <summary>The prompt it answered.</summary>
            public string Asked { get; }

            /// <summary>The point picked.</summary>
            public GeoPoint2 Pick { get; }

            /// <summary>The shape touched, as FILLET reads it; null for a point.</summary>
            public Shape Shape { get; }

            /// <summary>Whether it is an object touched rather than a point passed through.</summary>
            public bool IsObject { get; }

            /// <summary>
            /// The condition of touching and its slopes by x, y and r: a point at the radius from the centre; a line at the radius
            /// on the side given; a circle the radius further out or, the side below nought, the radius in.
            /// </summary>
            public void Condition(double x, double y, double r, double side, out double f, out double fx, out double fy, out double fr)
            {
                if (_line)
                {
                    // The distance of the centre across the line, left of it above nought.
                    double across = -_way.Y * (x - _origin.X) + _way.X * (y - _origin.Y);
                    f = across - side * r;
                    fx = -_way.Y;
                    fy = _way.X;
                    fr = -side;
                    return;
                }

                double ex = x - _origin.X;
                double ey = y - _origin.Y;
                double reach = IsObject ? _radius + side * r : r;
                f = ex * ex + ey * ey - reach * reach;
                fx = 2.0 * ex;
                fy = 2.0 * ey;
                fr = IsObject ? -2.0 * reach * side : -2.0 * r;
            }

            /// <summary>The point the circle of a centre and radius touches it at, or the point itself.</summary>
            public GeoPoint2 Foot(GeoPoint2 center, double r)
            {
                if (!IsObject)
                {
                    return _origin;
                }

                if (_line)
                {
                    return _origin + _way * Dot(center - _origin, _way);
                }

                GeoVector2 outward = center - _origin;
                double length = outward.Length;
                return length <= 0.0 ? _origin : _origin + outward * (_radius / length);
            }

            /// <summary>Whether the circle touches it within the tolerance: at the radius from a line, the circles' centres the sum or difference of the radii apart.</summary>
            public bool Touches(GeoPoint2 center, double r, Tolerance tolerance)
            {
                if (!IsObject)
                {
                    return Math.Abs((center - _origin).Length - r) <= tolerance.EqualPoint;
                }

                if (_line)
                {
                    return Math.Abs(Math.Abs(-_way.Y * (center.X - _origin.X) + _way.X * (center.Y - _origin.Y)) - r) <= tolerance.EqualPoint;
                }

                double apart = (center - _origin).Length;
                return Math.Abs(apart - (_radius + r)) <= tolerance.EqualPoint || Math.Abs(apart - Math.Abs(_radius - r)) <= tolerance.EqualPoint;
            }
        }
    }
}
