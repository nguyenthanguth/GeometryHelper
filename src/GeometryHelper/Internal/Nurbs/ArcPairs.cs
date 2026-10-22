using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// Two arcs of circles in the plane, a piece of a curve and a piece of its partner, met in closed form as the circles meet: where the
    /// library's own two circles cross or touch, or, where they are one circle, along the stretch they share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two circles nearly about one centre are as near as the digits tell for a long way round, and cut into straight parts they pair
    /// every part of the one with hundreds of the other's: the circle splines of radius 100 000 and 100 000.0005 with their centres 0.01
    /// apart ran through two million pairs in 16 seconds and gave no crossing, where the circles cross at (-4 999.995, 99 874.922) and
    /// (-4 999.995, -99 874.922). Their two arcs are answered by the rule of <see cref="Intersection2.TryIntersectWith(GeoCircle2, GeoCircle2, out GeoPoint2[], Tolerance)"/>
    /// with the reach for the point tolerance, so that a spline made of arcs and the circles it is made from say the same: a crossing or
    /// a touch where the circles have one, kept where both arcs hold it; and two circles within the reach of each other all the way round,
    /// one circle to the rule, lie along each other over the stretch both arcs cover, which gives no place.
    /// </para>
    /// <para>
    /// For the nearest approach the arcs are measured as they are, not through the reach: where the circles truly cross, the crossings,
    /// and otherwise the two points of the first circle on the line through the centres, where two circles come nearest and farthest.
    /// The start of the piece is looked at too, where the curve may turn a corner.
    /// </para>
    /// </remarks>
    internal static class ArcPairs
    {
        /// <summary>
        /// Says whether two arcs are known well enough to be met in closed form: their centres and radii, as read from the numbers they
        /// are made of, within a hundredth of the reach of the true circles for the places, and within a snap for the distance. A flat arc,
        /// of radius 1 000 000 and chord 300, has its centre read from its control points to no better than 2E-4, and the rule of the two
        /// circles read so would give places the arcs do not have; such a pair is cut as any other.
        /// </summary>
        internal static bool Exact(PieceArc first, PieceArc second, Hunt hunt)
            => first.Error + second.Error <= (hunt.OnlyNearest ? hunt.Snap : 1E-2 * hunt.Reach);

        /// <summary>
        /// Offers what a piece of a curve that is an arc gives a hunt against a partner's arc.
        /// </summary>
        /// <param name="a">The curve.</param>
        /// <param name="piece">The piece of the curve.</param>
        /// <param name="first">The arc the piece is.</param>
        /// <param name="second">The partner's arc.</param>
        /// <param name="partner">The partner, which measures the distance from a point of the curve.</param>
        /// <param name="hunt">The hunt.</param>
        internal static void Meet(NurbsCurve a, int piece, PieceArc first, PieceArc second, Partner partner, Hunt hunt)
        {
            a.Consider(hunt, partner, a.Pieces.From[piece]);

            double dx = second.Cx - first.Cx;
            double dy = second.Cy - first.Cy;
            double apart = Math.Sqrt(dx * dx + dy * dy);

            if (hunt.OnlyNearest)
            {
                Nearest(a, piece, first, second, apart, partner, hunt);
                return;
            }

            double reach = hunt.Reach;
            double r1 = first.Radius;
            double r2 = second.Radius;

            if (apart > r1 + r2 + reach || apart < Math.Abs(r1 - r2) - reach)
            {
                return;
            }

            var tolerance = new Tolerance(reach, 0.0);

            if (apart <= reach && Intersection2.AboutOneCentre(apart, Scale(first, second), r1, r2, tolerance))
            {
                Along(a, piece, first, second, partner, hunt);
                return;
            }

            Intersection2.TryIntersectWith(new GeoCircle2(first.Cx, first.Cy, r1), new GeoCircle2(second.Cx, second.Cy, r2), out GeoPoint2[] points, tolerance);

            foreach (GeoPoint2 point in points)
            {
                if (first.Holds(point.X, point.Y, hunt.Snap) && second.Holds(point.X, point.Y, hunt.Snap))
                {
                    Offer(a, KnotOn(a, piece, point.X, point.Y), partner, hunt);
                }
            }
        }

        /// <summary>
        /// Offers the places two arcs come nearest, measured as they are: the crossings, where the circles cross, and the points of the
        /// first circle on the line through the centres.
        /// </summary>
        private static void Nearest(NurbsCurve a, int piece, PieceArc first, PieceArc second, double apart, Partner partner, Hunt hunt)
        {
            if (!(apart > 0.0))
            {
                // About one centre every point of the arc is as near; its start has been looked at.
                return;
            }

            double r1 = first.Radius;
            double r2 = second.Radius;
            double ux = (second.Cx - first.Cx) / apart;
            double uy = (second.Cy - first.Cy) / apart;

            for (int side = -1; side <= 1; side += 2)
            {
                Look(a, piece, first, first.Cx + side * r1 * ux, first.Cy + side * r1 * uy, partner, hunt);
            }

            if (Math.Abs(r1 - r2) <= apart && apart <= r1 + r2)
            {
                // The difference of the squared radii as the product of their difference and their sum, as the circles' own rule takes it.
                double along = ((r1 - r2) * (r1 + r2) + apart * apart) / (2.0 * apart);
                double square = (r1 - along) * (r1 + along);
                double across = square > 0.0 ? Math.Sqrt(square) : 0.0;

                for (int side = -1; side <= 1; side += 2)
                {
                    Look(a, piece, first, first.Cx + along * ux - side * across * uy, first.Cy + along * uy + side * across * ux, partner, hunt);
                }
            }
        }

        private static void Look(NurbsCurve a, int piece, PieceArc first, double x, double y, Partner partner, Hunt hunt)
        {
            if (first.Holds(x, y, hunt.Snap))
            {
                a.Consider(hunt, partner, KnotOn(a, piece, x, y));
            }
        }

        /// <summary>
        /// Lays a stretch along the partner over the part of the piece the partner's arc covers, and the reach beyond either end of it, where
        /// the curve is within the reach of the partner's end: no place is given in it.
        /// </summary>
        private static void Along(NurbsCurve a, int piece, PieceArc first, PieceArc second, Partner partner, Hunt hunt)
        {
            BezierPieces pieces = a.Pieces;
            double from = pieces.From[piece];
            double to = pieces.To[piece];

            if (second.Whole)
            {
                hunt.Runs.Add(new Hunt.AlongRun { Part = partner.Position, From = from, To = to });
                return;
            }

            // Both as counter-clockwise turns about the piece's centre, the partner's read from its two ends, which stand off the piece's
            // centre by as much as the centres are apart, and widened by the reach; the partner's from the far side of the piece's start.
            const double Turn = 2.0 * Math.PI;
            double low = first.Sweep >= 0.0 ? first.Start : first.Start + first.Sweep;
            double length = Math.Abs(first.Sweep);
            double startAngle = Math.Atan2(second.Cy + second.Radius * Math.Sin(second.Start) - first.Cy, second.Cx + second.Radius * Math.Cos(second.Start) - first.Cx);
            double endAngle = Math.Atan2(second.Cy + second.Radius * Math.Sin(second.Start + second.Sweep) - first.Cy, second.Cx + second.Radius * Math.Cos(second.Start + second.Sweep) - first.Cx);
            double otherLow = second.Sweep >= 0.0 ? startAngle : endAngle;
            double drift = (second.Sweep >= 0.0 ? endAngle - startAngle : startAngle - endAngle) - Math.Abs(second.Sweep);
            drift -= Turn * Math.Floor(drift / Turn + 0.5);
            double pad = hunt.Reach / first.Radius;
            double otherLength = Math.Abs(second.Sweep) + drift + 2.0 * pad;
            double shift = otherLow - pad - low;
            shift -= Turn * Math.Floor(shift / Turn);

            for (int round = 0; round < 2; round++)
            {
                double begin = low + shift - round * Turn;
                double u0 = Math.Max(low, begin);
                double u1 = Math.Min(low + length, begin + otherLength);

                if (!(u1 > u0))
                {
                    continue;
                }

                double k0 = AtAngle(a, piece, first, u0, low, length);
                double k1 = AtAngle(a, piece, first, u1, low, length);
                hunt.Runs.Add(new Hunt.AlongRun { Part = partner.Position, From = Math.Min(k0, k1), To = Math.Max(k0, k1) });
            }
        }

        /// <summary>
        /// Gets the knot value of the piece at an angle of its arc: its own ends at the ends of the turn.
        /// </summary>
        private static double AtAngle(NurbsCurve a, int piece, PieceArc first, double angle, double low, double length)
        {
            BezierPieces pieces = a.Pieces;
            bool forward = first.Sweep >= 0.0;

            if (angle <= low)
            {
                return forward ? pieces.From[piece] : pieces.To[piece];
            }

            if (angle >= low + length)
            {
                return forward ? pieces.To[piece] : pieces.From[piece];
            }

            return KnotOn(a, piece, first.Cx + first.Radius * Math.Cos(angle), first.Cy + first.Radius * Math.Sin(angle));
        }

        /// <summary>
        /// Gets the knot value of the point of the curve nearest a point, inside the piece.
        /// </summary>
        private static double KnotOn(NurbsCurve a, int piece, double x, double y)
        {
            double knot = a.ClosestParameter(new[] { x, y }, out _);
            BezierPieces pieces = a.Pieces;

            return knot < pieces.From[piece] ? pieces.From[piece] : knot > pieces.To[piece] ? pieces.To[piece] : knot;
        }

        /// <summary>
        /// Offers the place of the curve at a knot value the circles' rule gave: a crossing where it meets the partner, otherwise the near
        /// touch, known to be one.
        /// </summary>
        private static void Offer(NurbsCurve a, double knot, Partner partner, Hunt hunt)
        {
            var point = new double[a.Dim];
            a.PointAt(knot, point);
            double gap = partner.Gap(point[0], point[1], 0.0, out double qx, out double qy, out double qz, out double other);
            bool meets = gap <= hunt.Snap;

            hunt.Offer(new Contact
            {
                Knot = knot,
                Gap = meets ? 0.0 : gap,
                Raw = gap,
                Px = point[0],
                Py = point[1],
                Qx = meets ? point[0] : qx,
                Qy = meets ? point[1] : qy,
                Other = other,
                Part = partner.Position,
                Verified = meets,
                Crossing = meets,
                Least = !meets,
                From = partner
            });

            if (meets)
            {
                hunt.Mark(knot);
            }
        }

        /// <summary>
        /// Gets the largest coordinate of the centres of two arcs, or radius, whichever is larger, as the circles' rule weighs rounding by.
        /// </summary>
        private static double Scale(PieceArc first, PieceArc second)
            => Math.Max(
                Math.Max(Math.Max(Math.Abs(first.Cx), Math.Abs(first.Cy)), Math.Max(Math.Abs(second.Cx), Math.Abs(second.Cy))),
                Math.Max(first.Radius, second.Radius));
    }
}
