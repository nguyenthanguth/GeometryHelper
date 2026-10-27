using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a rim in space, an ellipse's, a circle's or an arc's, passes within the point tolerance of another curve without
    /// crossing it where the line two planes share says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The crossings of two curves not in one plane are read where the one's rim pierces the other's plane, kept where the
    /// other curve is within the tolerance. Two rims passing 0.0005 apart, the gap square to the other's plane, have none,
    /// since the first rim reaches that plane a long way off: of 2 514 pairs of ellipses of 4 000 drawn to pass within 0.0012
    /// of each other, not in one plane, 1 517 came within 0.00098 and touched neither way round, and of 2 000 ellipses and
    /// circles so drawn 1 056.
    /// </para>
    /// <para>
    /// A point of the rim within the tolerance of the other curve is within it of the other's plane, and the height of the
    /// rim above that plane is a constant and one harmonic of its angle, so the angles where the two can meet are one or two
    /// stretches, found in closed form, and cut to what an arc sweeps. A stretch is passed over when the gap at its middle,
    /// or a bound under it, is more than the rim can close over half of it, its major radius times its half angle. Otherwise
    /// the runs of it within the tolerance of the other curve are found by branch and bound on the gap (<see cref="Search"/>),
    /// and each run is one place: where the gap along it is least, brought down by Brent's method, the middle of the
    /// shortest line between the two there, unless a place already named lies on the run, stands within the tolerance of
    /// it, or both pass within it of the point halfway between the two. Read only every 32nd of a turn, a rim 139 by 121
    /// passing 0.0005 from one 139 by 32 was missed between two readings.
    /// </para>
    /// </remarks>
    internal static class NearPass3
    {
        /// <summary>
        /// The rim searched along, centre + a cos t U + b sin t V for the angles t it sweeps, U and V of unit length and square
        /// to each other.
        /// </summary>
        internal sealed class Rim
        {
            internal Rim(
                GeoPoint3 center,
                GeoVector3 along,
                GeoVector3 across,
                double major,
                double minor,
                double from,
                double sweep,
                Func<double, GeoPoint3> at,
                Func<GeoPoint3, bool> holds)
            {
                Center = center;
                Along = along;
                Across = across;
                Major = major;
                Minor = minor;
                From = from;
                Sweep = sweep;
                At = at;
                Holds = holds;
            }

            internal GeoPoint3 Center { get; }

            internal GeoVector3 Along { get; }

            internal GeoVector3 Across { get; }

            internal double Major { get; }

            internal double Minor { get; }

            /// <summary>Gets the angle the rim starts at.</summary>
            internal double From { get; }

            /// <summary>Gets the angle it sweeps, negative for clockwise; a whole turn or more for a closed rim.</summary>
            internal double Sweep { get; }

            /// <summary>Gets the point of the rim at an angle.</summary>
            internal Func<double, GeoPoint3> At { get; }

            /// <summary>Gets whether a point lies within the point tolerance of the rim.</summary>
            internal Func<GeoPoint3, bool> Holds { get; }

            /// <summary>
            /// Reads an ellipse, searched along by its eccentric angle as <see cref="Ellipse3.GetPointAtAngle(GeoEllipse3, double)"/>
            /// places it.
            /// </summary>
            internal static Rim Of(GeoEllipse3 ellipse, Func<GeoPoint3, bool> holds)
            {
                return new Rim(
                    ellipse.Center,
                    ellipse.MajorAxis,
                    ellipse.MinorAxis,
                    ellipse.MajorRadius,
                    ellipse.MinorRadius,
                    0.0,
                    2.0 * Math.PI,
                    angle => Ellipse3.GetPointAtAngle(ellipse, angle),
                    holds);
            }

            /// <summary>
            /// Reads an arc, searched along by its angle in the frame of its plane, as <see cref="Gap3"/> reads it.
            /// </summary>
            internal static Rim Of(GeoArc3 arc, Tolerance tolerance)
            {
                GeoPoint3 center = arc.Center;
                double radius = arc.Radius;
                new GeoPlane3(center, arc.Normal).GetAxes(out GeoVector3 u, out GeoVector3 v);

                return new Rim(
                    center,
                    u,
                    v,
                    radius,
                    radius,
                    arc.StartAngle,
                    arc.SweptAngle,
                    angle =>
                    {
                        double c = radius * Math.Cos(angle);
                        double s = radius * Math.Sin(angle);

                        return new GeoPoint3(center.X + c * u.X + s * v.X, center.Y + c * u.Y + s * v.Y, center.Z + c * u.Z + s * v.Z);
                    },
                    point => arc.IsPointOn(point, tolerance));
            }
        }

        /// <summary>
        /// The curve the rim passes: its plane, how far the gap to it is, a bound under that, its point nearest a point, and
        /// whether it holds a point.
        /// </summary>
        internal sealed class Target
        {
            internal Target(
                GeoPlane3 plane,
                GeoVector3 normal,
                GeoPoint3 center,
                double reach,
                Func<GeoPoint3, double> atLeast,
                Func<GeoPoint3, double> gap,
                Func<GeoPoint3, GeoPoint3> nearest,
                Func<GeoPoint3, bool> holds,
                GeoVector3 along,
                GeoVector3 across,
                double major,
                double minor,
                Func<GeoPoint2, GeoPoint2> nearestFlat,
                bool whole)
            {
                Plane = plane;
                Normal = normal;
                Center = center;
                Reach = reach;
                AtLeast = atLeast;
                Gap = gap;
                Nearest = nearest;
                Holds = holds;
                Along = along;
                Across = across;
                Major = major;
                Minor = minor;
                NearestFlat = nearestFlat;
                Whole = whole;
            }

            /// <summary>Gets the unit axis of the curve's frame its major radius runs along.</summary>
            internal GeoVector3 Along { get; }

            /// <summary>Gets the unit axis of the curve's frame its minor radius runs along.</summary>
            internal GeoVector3 Across { get; }

            /// <summary>Gets the major radius of the ellipse or circle the curve lies on, in its frame.</summary>
            internal double Major { get; }

            /// <summary>Gets the minor radius of the ellipse or circle the curve lies on.</summary>
            internal double Minor { get; }

            /// <summary>Gets the point of that ellipse or circle, in its frame, nearest a point of its plane.</summary>
            internal Func<GeoPoint2, GeoPoint2> NearestFlat { get; }

            /// <summary>Gets whether the curve is the whole ellipse or circle, so that its gap is read from its foot.</summary>
            internal bool Whole { get; }

            /// <summary>Gets the angle an arc starts at in the curve's frame; nought for a whole ellipse or circle.</summary>
            internal double From { get; private set; }

            /// <summary>Gets the angle an arc sweeps, negative for clockwise; nought for a whole ellipse or circle.</summary>
            internal double Sweep { get; private set; }

            internal GeoPlane3 Plane { get; }

            internal GeoVector3 Normal { get; }

            internal GeoPoint3 Center { get; }

            /// <summary>Gets how far the curve reaches from its centre.</summary>
            internal double Reach { get; }

            internal Func<GeoPoint3, double> AtLeast { get; }

            internal Func<GeoPoint3, double> Gap { get; }

            internal Func<GeoPoint3, GeoPoint3> Nearest { get; }

            internal Func<GeoPoint3, bool> Holds { get; }

            /// <summary>
            /// Reads an arc, or a circle as the arc of a whole turn, by its exact gap (<see cref="Gap3.Curve.PointGap(GeoPoint3)"/>).
            /// </summary>
            /// <remarks>
            /// Its point nearest a point is found at no tolerance, as the gap is: found at the global one, a point whose foot fell
            /// within a thousandth of the centre was carried to the arc's start, and an ellipse passing 4E-7 under the quarter
            /// of the circle of radius 0.0005 was given its touch 0.00004 off the arc at a tolerance of a millionth.
            /// </remarks>
            internal static Target Of(GeoArc3 arc, Tolerance tolerance)
            {
                Gap3.Curve curve = Gap3.Curve.Of(arc);
                double radius = arc.Radius;
                new GeoPlane3(arc.Center, arc.Normal).GetAxes(out GeoVector3 u, out GeoVector3 v);

                return new Target(
                    arc.GetPlane(),
                    arc.Normal,
                    arc.Center,
                    radius,
                    curve.PointGap,
                    curve.PointGap,
                    point => arc.GetClosestPointOnBoundary(point, default(Tolerance)),
                    point => arc.IsPointOn(point, tolerance),
                    u,
                    v,
                    radius,
                    radius,
                    point =>
                    {
                        double reach = Math.Sqrt(point.X * point.X + point.Y * point.Y);

                        return reach > 0.0 ? new GeoPoint2(point.X * radius / reach, point.Y * radius / reach) : new GeoPoint2(radius, 0.0);
                    },
                    Math.Abs(arc.SweptAngle) >= 2.0 * Math.PI)
                {
                    From = arc.StartAngle,
                    Sweep = arc.SweptAngle,
                };
            }
        }

        /// <summary>
        /// Adds the places where a rim passes within the point tolerance of another curve that no place named already stands
        /// for.
        /// </summary>
        internal static void Add(List<GeoPoint3> found, Rim rim, Target target, Tolerance tolerance)
        {
            double a = rim.Major;
            double b = rim.Minor;
            GeoVector3 normal = target.Normal;
            double alongPart = a * rim.Along.DotProduct(normal);
            double acrossPart = b * rim.Across.DotProduct(normal);
            double swing = Math.Sqrt(alongPart * alongPart + acrossPart * acrossPart);
            double height = Intersection3.HeightAbove(target.Plane, rim.Center);

            // A hair over the tolerance, and the rounding of the heights and gaps read here besides, as an arc reads a segment.
            GeoPoint3 one = rim.Center;
            GeoPoint3 two = target.Center;
            double size = Math.Abs(one.X) + Math.Abs(one.Y) + Math.Abs(one.Z) + Math.Abs(two.X) + Math.Abs(two.Y) + Math.Abs(two.Z)
                + a + target.Reach;
            double near = tolerance.EqualPoint * (1.0 + 1E-6) + 1E-9 + 1E-15 * size;

            // The height is height + swing cos(t - phase); the stretches are where it is within the reach of nought.
            double inner;
            double outer;

            if (swing > 0.0)
            {
                double low = (-near - height) / swing;
                double high = (near - height) / swing;

                if (low > 1.0 || high < -1.0)
                {
                    return;
                }

                inner = high >= 1.0 ? 0.0 : Math.Acos(high);
                outer = low <= -1.0 ? Math.PI : Math.Acos(low);
            }
            else if (Math.Abs(height) <= near)
            {
                inner = 0.0;
                outer = Math.PI;
            }
            else
            {
                return;
            }

            double phase = Math.Atan2(acrossPart, alongPart);
            var stretches = new List<double[]>(2);

            if (inner == 0.0 && outer == Math.PI)
            {
                stretches.Add(new[] { phase, phase + 2.0 * Math.PI });
            }
            else if (inner == 0.0)
            {
                stretches.Add(new[] { phase - outer, phase + outer });
            }
            else if (outer == Math.PI)
            {
                stretches.Add(new[] { phase + inner, phase + 2.0 * Math.PI - inner });
            }
            else
            {
                stretches.Add(new[] { phase + inner, phase + outer });
                stretches.Add(new[] { phase - outer, phase - inner });
            }

            // Whether the one stretch is a whole turn of a closed rim, said here rather than read off its ends: from the
            // phase 2.0169641759523502, the phase plus a turn less the phase measures a hair short of a turn.
            bool round = inner == 0.0 && outer == Math.PI;

            if (Math.Abs(rim.Sweep) < 2.0 * Math.PI)
            {
                stretches = Swept(stretches, rim.From, rim.Sweep);
                round = false;
            }

            double limit = tolerance.EqualPoint;
            var search = new Search(rim, target, height, swing, phase, limit, near, 1E-6 * limit + 1E-12 * size);

            foreach (double[] stretch in stretches)
            {
                double from = stretch[0];
                double to = stretch[1];
                double middle = 0.5 * (from + to);
                double sweep = 0.5 * a * (to - from);

                // A bound the gap cannot be under is asked first, so that a stretch far from the other curve costs no
                // nearest point.
                if (target.AtLeast(rim.At(middle)) - sweep > near || search.Gap(middle) - sweep > near)
                {
                    continue;
                }

                // Each run of the rim within the tolerance of the other is one place, where the gap along it is least.
                foreach (Run run in search.Runs(from, to, round))
                {
                    double angle = run.LeastAt;
                    double least = run.Least;

                    if (run.To > run.From)
                    {
                        double polished = Ellipse2.Least(search.Gap, run.From, run.LeastAt, run.To, out double value);

                        if (value < least)
                        {
                            angle = polished;
                            least = value;
                        }
                    }

                    if (least <= limit && !Named(found, rim, run, tolerance))
                    {
                        AddPlace(found, rim, target, rim.At(angle), search, tolerance);
                    }
                }
            }
        }

        /// <summary>
        /// A stretch of the rim, as angles from and to, within the tolerance of the other curve or not proved clear of it, and
        /// the least gap read along it and where.
        /// </summary>
        private sealed class Run
        {
            internal double From;
            internal double To;
            internal double LeastAt;
            internal double Least;

            /// <summary>Whether the run was parted from others of its group, its ends readings close beside it.</summary>
            internal bool Parted;
        }

        /// <summary>
        /// The search along a rim for the stretches within the tolerance of another curve, by branch and bound on the gap.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three bounds the gap cannot be under on a piece of angle, the greatest of them taken. The gap changes no faster
        /// than the point of the rim moves, at most the root of a² sin² t + b² cos² t over the piece, so a piece of width w
        /// whose ends stand f1 and f2 from the other is no nearer than (f1 + f2 - speed w) / 2. The gap is never less than
        /// the rim's height above the other's plane, a constant and one harmonic of the angle, least over the piece. And the
        /// gap is never less than the distance from the rim's foot on that plane to the ellipse or circle the other lies on,
        /// whose root is the root of the height squared and that distance squared: where the foot lies wholly outside that
        /// ellipse over the piece, or wholly inside, as the ellipse's equation read along the foot, a constant and two
        /// harmonics whose turning points are found once, tells. The foot runs from its chord no further than an eighth of
        /// its greatest bend times the width squared. Outside, the distance to the filled ellipse is convex, so along the
        /// chord it lies above the tangent planes at the ends; inside, the distance to the rim is concave, and lies above the
        /// lesser end. Rims running along each other just outside the tolerance, which the first bound alone proves clear
        /// only in pieces a millionth of the rim's speed wide, are proved so in far fewer.
        /// </para>
        /// <para>
        /// The stretch is read every 64th of a turn first, then the piece with the least bound is halved first. A piece proved
        /// further than the tolerance is passed over. A piece with an end within the tolerance is halved only to a 64th of a
        /// turn: there the two run along each other, and a run of such pieces is one place, unless halved further it holds
        /// two runs the gap rises above the tolerance between (<see cref="Split"/>). Any other piece is halved until
        /// the rim runs no more than twice the tolerance along it, and is then kept: at 64 tolerances, a rim crossing a needle
        /// 0.03 across held both its sides in one piece, and the touch was missed. So nothing is passed over that is not
        /// proved clear. Halving stops after 384 readings in a call, and what is left is kept too. Of 400 pairs drawn to graze
        /// each other, thin or huge, none reaches that; of 400 copies, or rims running along each other within a few
        /// tolerances for a long way, up to 234 do. Along pieces kept and not within the tolerance, each reading no higher
        /// than its neighbours is brought down by Brent's method between them, so a touch there is found where the gap
        /// between two readings has one least: that, and nothing else, is not proved. Walked instead in steps of the gap less
        /// the tolerance and stopped after 20 000 readings, the search dropped the touches of rims running along each other
        /// just outside the tolerance, as two ellipses 3 000 by 1 000, the one 0.0012 above the other and leaning, 0.0001
        /// apart at the end of the minor axis, named a place where it stopped, and took up to 10 milliseconds.
        /// </para>
        /// </remarks>
        private sealed class Search
        {
            private const double Turn = 2.0 * Math.PI;
            private const double Fine = Turn / 64.0;
            private const int Readings = 384;

            private readonly Rim _rim;
            private readonly Target _target;
            private readonly double _height;
            private readonly double _swing;
            private readonly double _phase;
            private readonly double _limit;
            private readonly double _near;
            private readonly double _resolution;
            private readonly double _offset;

            // The rim's foot on the other's plane, in the other's frame: along = u0 + uc cos t + us sin t, and across alike.
            private readonly double _uc;
            private readonly double _us;
            private readonly double _vc;
            private readonly double _vs;
            private int _readings;
            private int _splitting;
            private long _queued;

            internal Search(Rim rim, Target target, double height, double swing, double phase, double limit, double near, double resolution)
            {
                _rim = rim;
                _target = target;
                _height = height;
                _swing = swing;
                _phase = phase;
                _limit = limit;
                _near = near;
                _resolution = resolution;
                Gap = angle => _target.Gap(_rim.At(angle));
                _offset = rim.Center.DistanceTo(target.Center);

                GeoVector3 u = target.Along;
                GeoVector3 v = target.Across;

                _uc = rim.Major * rim.Along.DotProduct(u);
                _us = rim.Minor * rim.Across.DotProduct(u);
                _vc = rim.Major * rim.Along.DotProduct(v);
                _vs = rim.Minor * rim.Across.DotProduct(v);
            }

            /// <summary>Gets the gap from the rim at an angle to the other curve.</summary>
            internal Func<double, double> Gap { get; }

            /// <summary>
            /// Gets the runs of a stretch of angle within the tolerance of the other curve, or not proved clear of it, in
            /// order of angle; a stretch of a whole turn read round, a run over its ends one.
            /// </summary>
            internal List<Run> Runs(double from, double to, bool round)
            {
                var kept = new List<Piece>();
                var queue = new List<Piece>();
                int count = Math.Max(1, (int)Math.Ceiling((to - from) / Fine));
                double previous = Read(from, out Foot previousFoot);

                _readings++;

                for (int k = 1; k <= count; k++)
                {
                    double end = k == count ? to : from + (to - from) * k / count;
                    double start = k == 1 ? from : from + (to - from) * (k - 1) / count;
                    double value = Read(end, out Foot foot);

                    _readings++;

                    Enqueue(queue, new Piece { From = start, To = end, AtFrom = previous, AtTo = value, FootFrom = previousFoot, FootTo = foot });
                    previous = value;
                    previousFoot = foot;
                }

                Settle(queue, kept);
                kept.Sort((x, y) => x.From.CompareTo(y.From));

                // Pieces that meet, end to start, are one group; round a whole turn, a group reaching the end and one
                // leaving the start are one.
                var groups = new List<List<Piece>>();

                foreach (Piece piece in kept)
                {
                    List<Piece> last = groups.Count > 0 ? groups[groups.Count - 1] : null;

                    if (last != null && piece.From <= last[last.Count - 1].To)
                    {
                        last.Add(piece);
                    }
                    else
                    {
                        groups.Add(new List<Piece> { piece });
                    }
                }

                if (round && groups.Count > 1 && groups[0][0].From <= from && groups[groups.Count - 1][groups[groups.Count - 1].Count - 1].To >= to)
                {
                    foreach (Piece piece in groups[0])
                    {
                        groups[groups.Count - 1].Add(new Piece { From = piece.From + Turn, To = piece.To + Turn, AtFrom = piece.AtFrom, AtTo = piece.AtTo, FootFrom = piece.FootFrom, FootTo = piece.FootTo });
                    }

                    groups.RemoveAt(0);
                }

                var runs = new List<Run>();

                foreach (List<Piece> group in groups)
                {
                    // A group within the tolerance somewhere is one run, from the least reading within it.
                    Run run = null;

                    foreach (Piece piece in group)
                    {
                        if (piece.AtFrom <= _near && (run == null || piece.AtFrom < run.Least))
                        {
                            run = run ?? new Run();
                            run.Least = piece.AtFrom;
                            run.LeastAt = piece.From;
                        }

                        if (piece.AtTo <= _near && (run == null || piece.AtTo < run.Least))
                        {
                            run = run ?? new Run();
                            run.Least = piece.AtTo;
                            run.LeastAt = piece.To;
                        }
                    }

                    if (run != null)
                    {
                        run.From = group[0].From;
                        run.To = group[group.Count - 1].To;
                        runs.AddRange(Split(group, run, round && group[0].From <= from && group[group.Count - 1].To >= to));
                        continue;
                    }

                    // Pieces only not proved clear: each reading along them no higher than its neighbours, brought down
                    // between them, as a dip there is one place each.
                    var angles = new List<double>(group.Count + 1) { group[0].From };
                    var values = new List<double>(group.Count + 1) { group[0].AtFrom };

                    foreach (Piece piece in group)
                    {
                        angles.Add(piece.To);
                        values.Add(piece.AtTo);
                    }

                    for (int k = 0; k < angles.Count; k++)
                    {
                        if ((k == 0 || values[k] <= values[k - 1]) && (k == angles.Count - 1 || values[k] <= values[k + 1]))
                        {
                            runs.Add(new Run
                            {
                                From = angles[Math.Max(0, k - 1)],
                                To = angles[Math.Min(angles.Count - 1, k + 1)],
                                LeastAt = angles[k],
                                Least = values[k],
                            });
                        }
                    }
                }

                return runs;
            }

            /// <summary>
            /// Halves pieces, the least bound first, until each is proved clear, has an end within the tolerance and is fine
            /// enough, or is fine enough not proved clear, and keeps what is not proved clear.
            /// </summary>
            private void Settle(List<Piece> queue, List<Piece> kept)
            {
                while (queue.Count > 0)
                {
                    Piece piece = Dequeue(queue);
                    double width = piece.To - piece.From;
                    bool within = piece.AtFrom <= _near || piece.AtTo <= _near;

                    // With an end within the tolerance, the piece is part of a run, and is halved only to a 64th of a turn,
                    // so that two runs further apart are told apart.
                    if (within && (width <= Fine || _readings >= Readings))
                    {
                        kept.Add(piece);
                        continue;
                    }

                    if (!within)
                    {
                        if (piece.Key > _near)
                        {
                            continue;
                        }

                        // Not proved clear, but too fine or too costly to halve: kept.
                        double reach = Speed(piece.From, piece.To) * width;

                        if (reach <= 2.0 * _near || reach <= _resolution || _readings >= Readings)
                        {
                            kept.Add(piece);
                            continue;
                        }
                    }

                    double middle = 0.5 * (piece.From + piece.To);
                    double value = Read(middle, out Foot foot);

                    _readings++;
                    Enqueue(queue, new Piece { From = piece.From, To = middle, AtFrom = piece.AtFrom, AtTo = value, FootFrom = piece.FootFrom, FootTo = foot });
                    Enqueue(queue, new Piece { From = middle, To = piece.To, AtFrom = value, AtTo = piece.AtTo, FootFrom = foot, FootTo = piece.FootTo });
                }
            }

            /// <summary>
            /// Gets the runs a group of pieces within the tolerance somewhere holds: the run read from the whole group, the
            /// least reading and the group's ends, where the group, halved further, holds one run within the tolerance;
            /// otherwise a run for each stretch of readings within it, from the reading before to the reading after, and for
            /// each reading above it no higher than its neighbours between them.
            /// </summary>
            /// <remarks>
            /// Pieces with an end within the tolerance are kept by <see cref="Settle"/> at a 64th of a turn, so two touches
            /// closer than that along the rim fall into one group: a circle of radius 247 passing within 0.018 and 0.017 of a
            /// rim 5 995 by 2 863 at two places 11.4 apart, the gap rising to 0.069 between, at a tolerance of 0.05, was given
            /// one. Here the pieces are halved again, those with an end within the tolerance first, until the rim runs no more
            /// than twice the tolerance along each, a piece with no end within it is proved clear, or one with both ends
            /// within it is proved within it throughout (<see cref="Most"/>). Halving stops after another 384 readings in a
            /// call, and what is left is kept. A piece proved clear, or a reading above the tolerance, between two readings
            /// within it parts two runs, unless the other curve runs within the tolerance of the rim all the way between them
            /// (<see cref="Joined(Run, Run, double)"/>); a group that holds one run is read as it was, and nothing is passed
            /// over that was not before.
            /// </remarks>
            private List<Run> Split(List<Piece> group, Run whole, bool round)
            {
                var queue = new List<Piece>();
                var settled = new List<Piece>();

                foreach (Piece piece in group)
                {
                    Enqueue(queue, new Piece { From = piece.From, To = piece.To, AtFrom = piece.AtFrom, AtTo = piece.AtTo, FootFrom = piece.FootFrom, FootTo = piece.FootTo });
                }

                while (queue.Count > 0)
                {
                    Piece piece = Dequeue(queue);
                    bool fromWithin = piece.AtFrom <= _near;
                    bool toWithin = piece.AtTo <= _near;

                    if (!fromWithin && !toWithin && piece.Key > _near)
                    {
                        continue;
                    }

                    double reach = Speed(piece.From, piece.To) * (piece.To - piece.From);

                    if (reach <= 2.0 * _near || reach <= _resolution || _splitting >= Readings || (fromWithin && toWithin && Most(piece, reach) <= _near))
                    {
                        settled.Add(piece);
                        continue;
                    }

                    double middle = 0.5 * (piece.From + piece.To);
                    double value = Read(middle, out Foot foot);

                    _splitting++;
                    Enqueue(queue, new Piece { From = piece.From, To = middle, AtFrom = piece.AtFrom, AtTo = value, FootFrom = piece.FootFrom, FootTo = foot });
                    Enqueue(queue, new Piece { From = middle, To = piece.To, AtFrom = value, AtTo = piece.AtTo, FootFrom = foot, FootTo = piece.FootTo });
                }

                settled.Sort((x, y) =>
                {
                    int byFrom = x.From.CompareTo(y.From);

                    return byFrom != 0 ? byFrom : x.To.CompareTo(y.To);
                });

                // The readings in order of angle, a piece proved clear between two parting them as a reading above the
                // tolerance would.
                var angles = new List<double>();
                var values = new List<double>();
                var parted = new List<bool>();

                foreach (Piece piece in settled)
                {
                    if (angles.Count == 0 || piece.From > angles[angles.Count - 1])
                    {
                        angles.Add(piece.From);
                        values.Add(piece.AtFrom);
                        parted.Add(angles.Count > 1);
                    }

                    angles.Add(piece.To);
                    values.Add(piece.AtTo);
                    parted.Add(false);
                }

                if (round && !Unwound(angles, values, parted))
                {
                    return new List<Run> { whole };
                }

                var runs = new List<Run>();
                var dips = new List<Run>();
                int last = angles.Count - 1;

                for (int k = 0; k <= last; k++)
                {
                    if (values[k] <= _near)
                    {
                        // A stretch of readings within the tolerance, up to a reading above it or a piece proved clear.
                        var run = new Run { From = angles[k > 0 && !parted[k] ? k - 1 : k], LeastAt = angles[k], Least = values[k] };
                        int end = k;

                        while (end < last && values[end + 1] <= _near && !parted[end + 1])
                        {
                            end++;

                            if (values[end] < run.Least)
                            {
                                run.Least = values[end];
                                run.LeastAt = angles[end];
                            }
                        }

                        run.To = angles[end < last && !parted[end + 1] ? end + 1 : end];
                        runs.Add(run);
                        k = end;
                        continue;
                    }

                    bool first = k == 0 || parted[k];
                    bool final = k == last || parted[k + 1];

                    if ((first || values[k] <= values[k - 1]) && (final || values[k] <= values[k + 1]))
                    {
                        dips.Add(new Run
                        {
                            From = angles[first ? k : k - 1],
                            To = angles[final ? k : k + 1],
                            LeastAt = angles[k],
                            Least = values[k],
                        });
                    }
                }

                // Runs the other curve joins, running within the tolerance of the rim all the way from the one to the other,
                // are one place.
                for (int k = runs.Count - 1; k > 0; k--)
                {
                    if (Joined(runs[k - 1], runs[k], 0.0))
                    {
                        runs[k - 1] = Merged(runs[k - 1], runs[k], 0.0);
                        runs.RemoveAt(k);
                    }
                }

                if (round && runs.Count > 1 && Joined(runs[runs.Count - 1], runs[0], Turn))
                {
                    runs[runs.Count - 1] = Merged(runs[runs.Count - 1], runs[0], Turn);
                    runs.RemoveAt(0);
                }

                foreach (Run run in runs)
                {
                    run.Parted = true;
                }

                foreach (Run dip in dips)
                {
                    dip.Parted = true;
                }

                if (runs.Count > 1)
                {
                    runs.AddRange(dips);
                    runs.Sort((x, y) =>
                    {
                        int byFrom = x.From.CompareTo(y.From);

                        return byFrom != 0 ? byFrom : x.To.CompareTo(y.To);
                    });

                    return runs;
                }

                // One run: read from the whole group as before, and the dips beside it after it.
                dips.Insert(0, whole);

                return dips;
            }

            /// <summary>
            /// Turns the readings of a whole turn, the last the first again, to start and end at the highest, so that a run
            /// over the ends is read as one: read from the start of the turn, one rim of 314 by 4.5 and another a hair
            /// larger, running along each other within the tolerance across it, gave a place at either end of the run.
            /// False where none is above the tolerance, the whole turn one run.
            /// </summary>
            private bool Unwound(List<double> angles, List<double> values, List<bool> parted)
            {
                int count = angles.Count - 1;
                int top = 0;

                for (int k = 1; k < count; k++)
                {
                    if (values[k] > values[top])
                    {
                        top = k;
                    }
                }

                if (!(values[top] > _near))
                {
                    return false;
                }

                var turnedAngles = new List<double>(count + 1);
                var turnedValues = new List<double>(count + 1);
                var turnedParted = new List<bool>(count + 1);

                for (int k = 0; k <= count; k++)
                {
                    int at = (top + k) % count;

                    turnedAngles.Add(top + k >= count ? angles[at] + Turn : angles[at]);
                    turnedValues.Add(values[at]);
                    turnedParted.Add(k > 0 && (at == 0 ? parted[count] : parted[at]));
                }

                angles.Clear();
                angles.AddRange(turnedAngles);
                values.Clear();
                values.AddRange(turnedValues);
                parted.Clear();
                parted.AddRange(turnedParted);

                return true;
            }

            /// <summary>
            /// Gets a bound the gap cannot be over on a piece: from the gap at its ends and the rim's speed; or from the gap
            /// D between the rim and a point carried evenly along the other between its points nearest the ends, where those
            /// lie on it.
            /// </summary>
            /// <remarks>
            /// D strays from its chord by no more than an eighth of the width squared times the greatest D'', and the chord
            /// is no longer than the greater gap at the ends. Both are ellipses read by angle, whose second derivatives are
            /// their points less their centres: with the other's angle k times the rim's, D'' is no more than a + k² A, the
            /// major radii a of the rim and A of the other, which bounds a small rim passing a large one; and
            /// D'' = -D + (k² - 1)(Q - C) + (C' - C), C the other's centre and C' the rim's, which, solved for the greatest
            /// D, bounds two rims running along each other, bending alike. The rim's speed alone proves a rim of radius 500
            /// running 0.0005 from a copy of itself within a tolerance of 0.001 only in pieces a thousandth long, the last
            /// in pieces a radian long.
            /// </remarks>
            private double Most(Piece piece, double reach)
            {
                double most = 0.5 * (piece.AtFrom + piece.AtTo + reach);
                double width = piece.To - piece.From;
                double share = 0.125 * width * width;

                if (!(share < 1.0) || !Carried(piece.FootFrom, piece.FootTo, out double turned))
                {
                    return most;
                }

                double k = turned / width;
                double chord = Math.Max(piece.AtFrom, piece.AtTo);
                double apart = chord + share * (_rim.Major + k * k * _target.Major);
                double alike = (chord + share * (Math.Abs(k * k - 1.0) * _target.Major + _offset)) / (1.0 - share);

                return Math.Min(most, Math.Min(apart, alike) + (_near - _limit));
            }

            /// <summary>
            /// Determines whether the other curve runs within the tolerance of the rim all the way between its points nearest
            /// the least readings of two runs, the second a turn on where asked.
            /// </summary>
            /// <remarks>
            /// The gap read from one rim can rise above the tolerance where the gap read from the other does not: round the
            /// end of a rim 100 000 by 1 that a copy 0.001 larger rings, the gap from the larger rose a hair above the
            /// tolerance either side of its end, and the two sides of the end were two places, the smaller running within
            /// the tolerance of the larger all round its own end. Read from the points between, they are one.
            /// </remarks>
            private bool Joined(Run one, Run two, double shift) => Joined(_rim.At(one.LeastAt), _rim.At(two.LeastAt + shift));

            /// <summary>
            /// Determines whether the other curve runs within the tolerance of the rim all the way between its points nearest
            /// two points: read at points evenly between, no further apart along it than the tolerance, or 255 of them.
            /// </summary>
            internal bool Joined(GeoPoint3 one, GeoPoint3 two) => Linked(one, two) == true;

            /// <summary>
            /// Determines whether the other curve runs within the tolerance of the rim all the way between its points nearest
            /// two points, as <see cref="Joined(GeoPoint3, GeoPoint3)"/> reads it; null where it cannot tell, the points of
            /// an arc nearest them not both on it.
            /// </summary>
            internal bool? Linked(GeoPoint3 one, GeoPoint3 two)
            {
                GeoPoint3 start = _target.Nearest(one);
                GeoPoint3 end = _target.Nearest(two);

                if (!Carried(OnOther(start), OnOther(end), out double turned))
                {
                    return null;
                }

                double from = OnOther(start);

                // A step no longer than the tolerance along the other, at most 256 of them and at least 16: read at 16, the
                // ends of two ellipses 313.85 by 4.54 and 0.001 larger, whose gap rises 0.11 tolerances above it round
                // either end, were read as joined, and gave one place each where they touch at both sides.
                int steps = (int)Math.Min(256.0, Math.Max(16.0, Math.Ceiling(Math.Abs(turned) * _target.Major / _limit)));

                for (int k = 1; k < steps; k++)
                {
                    double angle = from + turned * k / steps;
                    double along = _target.Major * Math.Cos(angle);
                    double across = _target.Minor * Math.Sin(angle);
                    GeoPoint3 point = _target.Center.Add(_target.Along.Multiply(along)).Add(_target.Across.Multiply(across));

                    if (!_rim.Holds(point))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Gets one run from two, the second a turn on where asked: from the start of the first to the end of the
            /// second, and the lesser of their least readings.
            /// </summary>
            private static Run Merged(Run one, Run two, double shift)
            {
                bool second = two.Least < one.Least;

                return new Run
                {
                    From = one.From,
                    To = two.To + shift,
                    LeastAt = second ? two.LeastAt + shift : one.LeastAt,
                    Least = second ? two.Least : one.Least,
                };
            }

            /// <summary>
            /// Gets the angle on the other's ellipse or circle of a point of its plane, read from its own coordinates.
            /// </summary>
            private double OnOther(GeoPoint3 point)
            {
                GeoVector3 offset = _target.Center.GetVectorTo(point);

                return Math.Atan2(offset.DotProduct(_target.Across) / _target.Minor, offset.DotProduct(_target.Along) / _target.Major);
            }

            /// <summary>
            /// Gets the angle the other's point is carried through between its points nearest the rims' feet at two ends,
            /// the short way round a whole ellipse or circle and within what an arc sweeps; false where the nearest point
            /// of an arc's circle at either end lies off the arc.
            /// </summary>
            private bool Carried(Foot start, Foot end, out double turned) => Carried(OnOther(start), OnOther(end), out turned);

            /// <summary>
            /// Gets the angle the other's point is carried through between two angles on its ellipse or circle, the short
            /// way round a whole one and within what an arc sweeps; false where either lies off the arc.
            /// </summary>
            private bool Carried(double from, double to, out double turned)
            {
                if (_target.Whole)
                {
                    turned = Math.IEEERemainder(to - from, Turn);

                    return true;
                }

                // Measured from the arc's start the way it runs, each in [0, a turn).
                double way = _target.Sweep < 0.0 ? -1.0 : 1.0;
                double first = way * (from - _target.From);
                double second = way * (to - _target.From);

                first -= Turn * Math.Floor(first / Turn);
                second -= Turn * Math.Floor(second / Turn);
                turned = way * (second - first);

                return first <= Math.Abs(_target.Sweep) && second <= Math.Abs(_target.Sweep);
            }

            /// <summary>
            /// Gets the angle on the other's ellipse or circle of its point nearest the rim's foot.
            /// </summary>
            private double OnOther(Foot foot)
            {
                double along = foot.Along - foot.Gap * foot.NormalX;
                double across = foot.Across - foot.Gap * foot.NormalY;

                return Math.Atan2(across / _target.Minor, along / _target.Major);
            }

            /// <summary>
            /// Puts a piece in the queue, ordered by the bound its ends, the rim's speed and its height give, the least first;
            /// pieces with an end within the tolerance first of all, ties in the order they came.
            /// </summary>
            private void Enqueue(List<Piece> queue, Piece piece)
            {
                if (piece.AtFrom <= _near || piece.AtTo <= _near)
                {
                    piece.Key = double.NegativeInfinity;
                }
                else
                {
                    piece.Key = Math.Max(0.5 * (piece.AtFrom + piece.AtTo - Speed(piece.From, piece.To) * (piece.To - piece.From)), Lowest(piece.From, piece.To));

                    if (!(piece.Key > _near))
                    {
                        piece.Key = Math.Max(piece.Key, Flat(piece));
                    }
                }

                piece.Order = _queued++;
                queue.Add(piece);

                for (int at = queue.Count - 1; at > 0;)
                {
                    int parent = (at - 1) / 2;

                    if (!Before(queue[at], queue[parent]))
                    {
                        break;
                    }

                    Piece swap = queue[at];
                    queue[at] = queue[parent];
                    queue[parent] = swap;
                    at = parent;
                }
            }

            /// <summary>
            /// Takes the first piece from the queue.
            /// </summary>
            private static Piece Dequeue(List<Piece> queue)
            {
                Piece first = queue[0];
                int last = queue.Count - 1;

                queue[0] = queue[last];
                queue.RemoveAt(last);

                for (int at = 0; ;)
                {
                    int left = 2 * at + 1;
                    int right = left + 1;
                    int least = at;

                    if (left < queue.Count && Before(queue[left], queue[least]))
                    {
                        least = left;
                    }

                    if (right < queue.Count && Before(queue[right], queue[least]))
                    {
                        least = right;
                    }

                    if (least == at)
                    {
                        break;
                    }

                    Piece swap = queue[at];
                    queue[at] = queue[least];
                    queue[least] = swap;
                    at = least;
                }

                return first;
            }

            private static bool Before(Piece one, Piece other)
            {
                int byKey = one.Key.CompareTo(other.Key);

                return byKey != 0 ? byKey < 0 : one.Order < other.Order;
            }

            /// <summary>
            /// Gets a bound the gap cannot be under over a piece from the distance between the rim's foot and the other's
            /// ellipse or circle, with the height bound; nought unless the feet at both ends lie on one side of that ellipse
            /// or circle.
            /// </summary>
            private double Flat(Piece piece)
            {
                double low = piece.From;
                double high = piece.To;
                double width = high - low;
                double lowest = Lowest(low, high);
                double sag = Sag(low, high);

                // The distance at the ends is no more than the gap there, so where even that, less the foot's sag, could not
                // prove the piece clear, the feet are not read.
                double best = Math.Max(0.0, Math.Max(piece.AtFrom, piece.AtTo) - sag);

                if (!(Math.Sqrt(lowest * lowest + best * best) > _near))
                {
                    return 0.0;
                }

                // Which side of the other's ellipse or circle the feet at the ends lie on, read from their own coordinates
                // about its centre: written out as one wave about the rim's centre instead, the terms of a thin ellipse
                // 93 000 away came to 3E16 and cancelled to nothing, a piece holding a touch 0.00092 off was judged
                // inside and proved clear, and the touch was missed.
                const double Margin = 1E-9;
                Foot start = piece.FootFrom;
                Foot end = piece.FootTo;
                double flat;

                if (start.Level > Margin && end.Level > Margin)
                {
                    // Outside, the distance to the filled ellipse is convex, so along the chord it lies above the tangent
                    // planes at the ends; where the chord runs into the ellipse, they fall below nought there.
                    double chordX = end.Along - start.Along;
                    double chordY = end.Across - start.Across;
                    double slopeStart = start.NormalX * chordX + start.NormalY * chordY;
                    double slopeEnd = end.NormalX * chordX + end.NormalY * chordY;

                    // Over s from nought to one: the greater of start.Gap + s slopeStart and end.Gap - (1 - s) slopeEnd.
                    double floor = Math.Min(Math.Max(start.Gap, end.Gap - slopeEnd), Math.Max(start.Gap + slopeStart, end.Gap));

                    if (slopeStart != slopeEnd)
                    {
                        double meet = (end.Gap - slopeEnd - start.Gap) / (slopeStart - slopeEnd);

                        if (meet > 0.0 && meet < 1.0)
                        {
                            floor = Math.Min(floor, start.Gap + slopeStart * meet);
                        }
                    }

                    flat = floor - sag;
                }
                else if (start.Level < -Margin && end.Level < -Margin)
                {
                    // Inside, the chord between the ends lies inside too, where the distance to the rim is concave: along
                    // it, that lies above the lesser end.
                    flat = Math.Min(start.Gap, end.Gap) - sag;
                }
                else
                {
                    return 0.0;
                }

                flat = Math.Max(0.0, flat - (_near - _limit));

                return Math.Sqrt(lowest * lowest + flat * flat);
            }

            /// <summary>
            /// Reads the gap at an angle, and the rim's foot there in the other's frame, its distance to the other's ellipse
            /// or circle and the direction away from it. Where the other is a whole ellipse or circle, the gap is the root of
            /// the height squared and that distance squared, so one nearest point serves both; an arc's own gap is read.
            /// </summary>
            private double Read(double angle, out Foot foot)
            {
                GeoPoint3 point = _rim.At(angle);
                GeoVector3 offset = _target.Center.GetVectorTo(point);
                double along = offset.DotProduct(_target.Along);
                double across = offset.DotProduct(_target.Across);
                GeoPoint2 nearest = _target.NearestFlat(new GeoPoint2(along, across));
                double dx = along - nearest.X;
                double dy = across - nearest.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double scaledAlong = along / _target.Major;
                double scaledAcross = across / _target.Minor;

                foot = new Foot
                {
                    Along = along,
                    Across = across,
                    Gap = distance,
                    NormalX = distance > 0.0 ? dx / distance : 0.0,
                    NormalY = distance > 0.0 ? dy / distance : 0.0,
                    Level = scaledAlong * scaledAlong + scaledAcross * scaledAcross - 1.0,
                };

                if (!_target.Whole)
                {
                    return _target.Gap(point);
                }

                double height = offset.DotProduct(_target.Normal);

                return Math.Sqrt(height * height + distance * distance);
            }

            /// <summary>
            /// Gets how far the foot can stand off the segment between its ends over a piece of angle: the lesser of an eighth
            /// of its greatest second derivative times the width squared, and, where it turns by less than half a turn, an
            /// eighth of its greatest curvature times its length squared. The foot is an ellipse drawn by
            /// along = u0 + uc cos t + us sin t and across alike, so its second derivative is the foot less its centre, the
            /// cross product of its first and second derivatives is uc vs - us vc throughout, and its curvature is that over
            /// its speed cubed. On the long sides of a thin rim the first reading is thousands of times the second.
            /// </summary>
            private double Sag(double from, double to)
            {
                double width = to - from;
                double cc = _uc * _uc + _vc * _vc;
                double ss = _us * _us + _vs * _vs;
                double cs = _uc * _us + _vc * _vs;

                // |X''|² = (cc + ss)/2 + (cc - ss)/2 cos 2t + cs sin 2t, and |X'|² the same with the harmonic turned over.
                Range(0.5 * (cc + ss), 0.5 * (cc - ss), cs, from, to, out _, out double bendSquared);
                Range(0.5 * (cc + ss), -0.5 * (cc - ss), -cs, from, to, out double slowSquared, out double fastSquared);

                double sag = 0.125 * Math.Sqrt(Math.Max(0.0, bendSquared)) * width * width;
                double slow = Math.Sqrt(Math.Max(0.0, slowSquared));

                if (slow > 0.0)
                {
                    double length = Math.Sqrt(Math.Max(0.0, fastSquared)) * width;
                    double curvature = Math.Abs(_uc * _vs - _us * _vc) / (slow * slow * slow);

                    if (curvature * length < Math.PI)
                    {
                        sag = Math.Min(sag, 0.125 * curvature * length * length);
                    }
                }

                return sag;
            }

            /// <summary>
            /// Gets the least and the greatest of mean + c cos 2t + s sin 2t between two angles: at the ends, or where twice
            /// the angle less the phase passes a whole number of half turns.
            /// </summary>
            private static void Range(double mean, double c, double s, double from, double to, out double least, out double most)
            {
                double atFrom = mean + c * Math.Cos(2.0 * from) + s * Math.Sin(2.0 * from);
                double atTo = mean + c * Math.Cos(2.0 * to) + s * Math.Sin(2.0 * to);
                double swing = Math.Sqrt(c * c + s * s);
                double phase = Math.Atan2(s, c);

                least = Math.Min(atFrom, atTo);
                most = Math.Max(atFrom, atTo);

                for (double k = Math.Ceiling((2.0 * from - phase) / Math.PI); k * Math.PI <= 2.0 * to - phase; k++)
                {
                    double value = mean + (Math.Abs(k % 2.0) < 0.5 ? swing : -swing);

                    least = Math.Min(least, value);
                    most = Math.Max(most, value);
                }
            }

            /// <summary>
            /// Gets the greatest speed of the rim between two angles: the root of b² + (a² - b²) sin² t at its greatest.
            /// </summary>
            private double Speed(double from, double to)
            {
                double a = _rim.Major;
                double b = _rim.Minor;
                double most;

                if (to - from >= Math.PI)
                {
                    most = 1.0;
                }
                else
                {
                    double quarter = 0.5 * Math.PI + Math.PI * Math.Ceiling((from - 0.5 * Math.PI) / Math.PI);
                    double sinFrom = Math.Sin(from);
                    double sinTo = Math.Sin(to);

                    most = quarter <= to ? 1.0 : Math.Max(sinFrom * sinFrom, sinTo * sinTo);
                }

                return Math.Sqrt(b * b + (a - b) * (a + b) * most);
            }

            /// <summary>
            /// Gets the least height of the rim above the other curve's plane between two angles, read off the harmonic.
            /// </summary>
            private double Lowest(double from, double to)
            {
                double atFrom = _height + _swing * Math.Cos(from - _phase);
                double atTo = _height + _swing * Math.Cos(to - _phase);
                double low = Math.Min(atFrom, atTo);
                double high = Math.Max(atFrom, atTo);

                // The turning points of the harmonic, where its angle is a whole number of half turns.
                for (double k = Math.Ceiling((from - _phase) / Math.PI); _phase + k * Math.PI < to; k++)
                {
                    double turn = _height + _swing * Math.Cos(k * Math.PI);

                    low = Math.Min(low, turn);
                    high = Math.Max(high, turn);
                }

                // A margin for the rounding of the rim's points, which the harmonic does not see.
                double lowest = low <= 0.0 && high >= 0.0 ? 0.0 : Math.Min(Math.Abs(low), Math.Abs(high));

                return Math.Max(0.0, lowest - (_near - _limit));
            }

            /// <summary>
            /// A piece of angle, the gap at each end, the foot at each end where read, and its place in the queue.
            /// </summary>
            private sealed class Piece
            {
                internal double From;
                internal double To;
                internal double AtFrom;
                internal double AtTo;
                internal Foot FootFrom;
                internal Foot FootTo;
                internal double Key;
                internal long Order;
            }

            /// <summary>
            /// The rim's foot at an angle in the other's frame, its distance to the other's ellipse or circle, the unit
            /// direction away from it, and (along / A)² + (across / B)² - 1, above nought outside and below inside.
            /// </summary>
            private sealed class Foot
            {
                internal double Along;
                internal double Across;
                internal double Gap;
                internal double NormalX;
                internal double NormalY;
                internal double Level;
            }
        }


        /// <summary>
        /// Determines whether a place already named lies on a run: its angle on the rim within the run's.
        /// </summary>
        /// <remarks>
        /// On a run parted from others of its group the angle is read from the rim's point nearest the place, as the run's
        /// ends lie close beside it: read from the place itself, a place 0.0003 off a rim 67 620 by 6.8 was put 0.0000075
        /// radians along it from that point, on the run beside, 0.17 away, and the touch there was not named.
        /// </remarks>
        private static bool Named(List<GeoPoint3> found, Rim rim, Run run, Tolerance tolerance)
        {
            const double Turn = 2.0 * Math.PI;

            foreach (GeoPoint3 named in found)
            {
                GeoVector3 offset = rim.Center.GetVectorTo(named);
                double along = offset.DotProduct(rim.Along);
                double across = offset.DotProduct(rim.Across);

                if (run.Parted && rim.Major != rim.Minor)
                {
                    GeoPoint2 nearest = Ellipse2.GetClosestPointOnBoundary(
                        new GeoEllipse2(new GeoPoint2(0.0, 0.0), GeoVector2.XAxis, rim.Major, rim.Minor, true),
                        new GeoPoint2(along, across));

                    along = nearest.X;
                    across = nearest.Y;
                }

                double at = Math.Atan2(across / rim.Minor, along / rim.Major);

                at += Turn * Math.Ceiling((run.From - at) / Turn);

                if (at <= run.To || rim.At(run.From).IsEqualTo(named, tolerance) || rim.At(run.To).IsEqualTo(named, tolerance))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Cuts stretches of angle, each no longer than a turn, to the angles an arc sweeps, each piece in order of angle,
        /// pieces that meet made one.
        /// </summary>
        private static List<double[]> Swept(List<double[]> stretches, double from, double sweep)
        {
            const double Turn = 2.0 * Math.PI;

            double low = Math.Min(from, from + sweep);
            double high = Math.Max(from, from + sweep);
            var pieces = new List<double[]>();

            foreach (double[] stretch in stretches)
            {
                double first = Math.Floor((low - stretch[1]) / Turn);
                double last = Math.Ceiling((high - stretch[0]) / Turn);

                for (double k = first; k <= last; k++)
                {
                    double start = Math.Max(stretch[0] + k * Turn, low);
                    double end = Math.Min(stretch[1] + k * Turn, high);

                    if (start <= end)
                    {
                        pieces.Add(new[] { start, end });
                    }
                }
            }

            pieces.Sort((x, y) =>
            {
                int byStart = x[0].CompareTo(y[0]);

                return byStart != 0 ? byStart : x[1].CompareTo(y[1]);
            });

            var joined = new List<double[]>(pieces.Count);

            foreach (double[] piece in pieces)
            {
                // A turn added to the end of one and to the start of the next can leave a hair between them.
                if (joined.Count > 0 && piece[0] - joined[joined.Count - 1][1] <= 1E-12 * Turn)
                {
                    joined[joined.Count - 1][1] = Math.Max(joined[joined.Count - 1][1], piece[1]);
                }
                else
                {
                    joined.Add(piece);
                }
            }

            return joined;
        }

        /// <summary>
        /// Adds the middle of the shortest line from a point of the rim to the other curve, unless a place already named
        /// stands within the tolerance of it or is joined to it, the other curve running within the tolerance of the rim all
        /// the way between the two; where that cannot be told, the nearest points of an arc not both on it, where both pass
        /// within the tolerance of the point halfway between.
        /// </summary>
        /// <remarks>
        /// Read by the point halfway, two touches were one wherever both rims ran near straight between them: a needle 19.5
        /// by 0.055 passing an arc of radius 126 within 0.019 and 0.040 at two places 4.3 apart, the gap rising to 1.17
        /// tolerances between at a tolerance of 0.05, was given one, the point halfway 0.047 off the needle and 0.014 off the
        /// arc. Asked of the other curve, a circle crossing a needle 0.00013 wide still touches it once at each crossing, at
        /// places on either side 0.008 apart that the circle runs within the tolerance of the needle between.
        /// </remarks>
        private static void AddPlace(List<GeoPoint3> found, Rim rim, Target target, GeoPoint3 onRim, Search search, Tolerance tolerance)
        {
            GeoPoint3 onOther = target.Nearest(onRim);
            GeoPoint3 place = onRim.Add(onRim.GetVectorTo(onOther).Multiply(0.5));

            foreach (GeoPoint3 named in found)
            {
                if (named.IsEqualTo(place, tolerance))
                {
                    return;
                }

                GeoPoint3 halfway = named.Add(named.GetVectorTo(place).Multiply(0.5));

                if (search.Linked(named, place) ?? (rim.Holds(halfway) && target.Holds(halfway)))
                {
                    return;
                }
            }

            found.Add(place);
        }
    }
}
