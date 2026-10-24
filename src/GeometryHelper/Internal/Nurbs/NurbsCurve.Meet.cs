using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// Finds the piece of the curve a knot value lies in: the last piece that starts at or below it.
        /// </summary>
        /// <param name="knot">The knot value, inside the domain.</param>
        /// <param name="atStart">Whether the knot value is where that piece starts.</param>
        internal int PieceAt(double knot, out bool atStart)
        {
            BezierPieces pieces = Pieces;
            int low = 0;
            int high = pieces.Count - 1;

            while (low < high)
            {
                int middle = (low + high + 1) >> 1;

                if (pieces.From[middle] <= knot)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            atStart = pieces.From[low] == knot;
            return low;
        }

        /// <summary>
        /// Says whether the curve runs along a partner at a knot value, so that a meeting there is no crossing: inside a
        /// piece that lies along it, to rounding, or at a join of two such pieces. Where such a piece joins one that does not,
        /// the curve is meeting the partner or leaving it, and that is a crossing.
        /// </summary>
        internal bool OnOverlap(Partner partner, double knot)
        {
            BezierPieces pieces = Pieces;

            if (knot >= End)
            {
                return partner.Coincident(this, pieces.Count - 1);
            }

            int piece = PieceAt(knot, out bool atStart);

            if (atStart && piece > 0)
            {
                return partner.Coincident(this, piece - 1) && partner.Coincident(this, piece);
            }

            return partner.Coincident(this, piece);
        }

        /// <summary>
        /// Says whether another curve has exactly the same degree, control points, weights and knots.
        /// </summary>
        internal bool SameAs(NurbsCurve other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Degree != other.Degree || Dim != other.Dim || Count != other.Count)
            {
                return false;
            }

            for (int i = 0; i < Points.Length; i++)
            {
                if (!Points[i].Equals(other.Points[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < Weights.Length; i++)
            {
                if (!Weights[i].Equals(other.Weights[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < Knots.Length; i++)
            {
                if (!Knots[i].Equals(other.Knots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Measures the curve's distance to a partner at a knot value and offers it to a hunt, as a meeting when it is
        /// nought to rounding.
        /// </summary>
        internal void Consider(Hunt hunt, Partner partner, double knot)
        {
            knot = Clamp(knot);
            PointAt(knot, hunt.Point);
            double px = hunt.Point[0];
            double py = hunt.Point[1];
            double pz = hunt.Point[2];
            double gap = partner.Gap(px, py, pz, out double qx, out double qy, out double qz, out double other);

            // A part of a flat shape has no nearest point to a place that does not stand over it.
            if (double.IsPositiveInfinity(gap))
            {
                return;
            }

            double raw = gap;
            bool overlap = false;
            bool verified = false;

            if (gap <= hunt.Snap)
            {
                overlap = OnOverlap(partner, knot);
                hunt.Mark(knot);
                gap = 0.0;
                qx = px;
                qy = py;
                qz = pz;
                verified = true;
            }

            hunt.Offer(new Contact
            {
                Knot = knot,
                Gap = gap,
                Raw = raw,
                Px = px,
                Py = py,
                Pz = pz,
                Qx = qx,
                Qy = qy,
                Qz = qz,
                Other = other,
                Part = partner.Position,
                Overlap = overlap,
                Verified = verified,
                From = partner
            });
        }

        /// <summary>
        /// Looks over a curve against a partner: the ends of the curve, the true ends of the partner, and each piece of the
        /// curve that can come within the hunt's limit of it.
        /// </summary>
        internal void Meet(Partner partner, Hunt hunt)
        {
            PieceIndex index = Index;
            var min = new double[Dim];
            var max = new double[Dim];
            index.Whole(min, max);

            // The curve is no nearer than its box.
            if (!(partner.Bound(min, max, 0) <= hunt.Limit))
            {
                return;
            }

            Consider(hunt, partner, Start);
            Consider(hunt, partner, End);

            for (int e = 0; e < partner.EndCount; e++)
            {
                partner.EndAt(e, out double ex, out double ey, out double ez);
                NearEnd(hunt, partner, ex, ey, ez);
            }

            BezierPieces pieces = Pieces;
            var bound = new PartnerBound(partner);

            if (hunt.OnlyNearest)
            {
                var visit = new PieceVisit { Curve = this, Pieces = pieces, Partner = partner, Hunt = hunt };
                index.Search(ref bound, ref visit);
                return;
            }

            var near = new List<int>();
            index.Collect(ref bound, hunt.Reach, near);
            near.Sort();

            for (int i = 0; i < near.Count; i++)
            {
                partner.PieceCandidates(this, pieces, near[i], hunt);
            }
        }

        private struct PieceVisit : IVisit
        {
            internal NurbsCurve Curve;
            internal BezierPieces Pieces;
            internal Partner Partner;
            internal Hunt Hunt;

            public double Limit => Hunt.Limit;

            public void Piece(int piece, double bound)
            {
                Partner.PieceCandidates(Curve, Pieces, piece, Hunt);
            }
        }

        /// <summary>
        /// Offers the places where the curve comes nearest a true end of a partner: the nearest, when only that is wanted, and
        /// when every place is, each place within the reach where the curve stops coming nearer the end.
        /// </summary>
        private void NearEnd(Hunt hunt, Partner partner, double ex, double ey, double ez)
        {
            double[] query = Dim == 3 ? new[] { ex, ey, ez } : new[] { ex, ey };

            if (hunt.OnlyNearest)
            {
                Consider(hunt, partner, ClosestParameter(query, out _));
                return;
            }

            BezierPieces pieces = Pieces;
            var region = new PointBound(query);
            var near = new List<int>();
            Index.Collect(ref region, hunt.Reach, near);
            near.Sort();
            var knots = new List<double>();

            for (int i = 0; i < near.Count; i++)
            {
                PointCritical(pieces, near[i], query, knots);
            }

            for (int i = 0; i < knots.Count; i++)
            {
                Consider(hunt, partner, knots[i]);
            }
        }

        /// <summary>
        /// Gets the derivatives of the curve at a knot value from each side: the point, then the first and second derivative.
        /// </summary>
        /// <param name="knot">The knot value.</param>
        /// <param name="left">From the span before it; the same as the right where the curve is smooth there, and not to be used at the start but of a closed curve, where it is from its last span.</param>
        /// <param name="right">From the span after it; from the span before at the end, but of a closed curve, where it is from its first span.</param>
        private void Sides(double knot, out double[][] left, out double[][] right)
        {
            right = Derivatives(knot, 2);
            left = right;

            if (!(knot > Start && knot < End))
            {
                // The seam of a closed curve: it comes in along its last span and goes on along its first.
                if (IsClosed)
                {
                    if (knot <= Start)
                    {
                        left = Derivatives(End, 2);
                    }
                    else
                    {
                        right = Derivatives(Start, 2);
                    }
                }

                return;
            }

            BezierPieces pieces = Pieces;
            int piece = PieceAt(knot, out bool atStart);

            if (atStart && piece > 0)
            {
                double before = knot - 1E-9 * (pieces.To[piece - 1] - pieces.From[piece - 1]);

                if (before < knot)
                {
                    left = Derivatives(before, 2);
                }
            }
        }

        /// <summary>
        /// Says whether a contact that is not a meeting is a place the curve comes near its partner and not only a point it passes at
        /// some distance: the distance D from the curve to the partner is a minimum there, within the reach, or the curve ends or turns
        /// a corner there with the distance not falling either way along it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The slope of D along the curve is the cosine of the angle between the curve's direction and the line to the partner, and its
        /// curvature is worked out from the derivatives of the curve and of the partner (<see cref="Variation"/>). A place is kept where
        /// the slope is nought and the curvature is above nought (a minimum, not a maximum, and not a level run, which is at its least
        /// nowhere); where the slope is not nought but the curvature is positive and the minimum it leads to is within the reach, which is
        /// the same place read to the reach; where the distance falls straight to a meeting within the reach, which is a crossing at an
        /// angle found a few snaps short of the last digit; and at a corner or an end, where D must not fall going either way. A piece
        /// start on a slope, and a stationary point that is a greatest, as lies between two crossings of a line that passes a hair
        /// inside a circle, are not places. Where the partner's nearest point is held at its end, the distance is that to the end only on
        /// the side the partner does not go on, and a least there is looked for a little way either side as well (<see cref="IsLeast"/>).
        /// </para>
        /// <para>
        /// A curve that stops, or has a cusp at the contact, or a partner that cannot give its derivatives, is probed a little way either
        /// side instead (<see cref="IsLeast"/>).
        /// </para>
        /// </remarks>
        private bool IsMinimum(Contact contact, Hunt hunt)
        {
            if (!TryGetFoot(contact.From, contact.Px, contact.Py, contact.Pz, hunt.Scale, Dim, out Foot foot))
            {
                return foot.G <= hunt.Snap || IsLeast(contact, hunt);
            }

            if (!(foot.G > hunt.Snap))
            {
                return true;
            }

            // A closed curve has no ends: at its seam it goes on either way.
            Sides(contact.Knot, out double[][] left, out double[][] right);
            bool hasLeft = contact.Knot > Start || IsClosed;
            bool hasRight = contact.Knot < End || IsClosed;
            double[][] along = hasRight ? right : left;
            double speed = Math.Sqrt(along[1][0] * along[1][0] + along[1][1] * along[1][1] + Z(along[1]) * Z(along[1]));
            double speedLeft = Math.Sqrt(left[1][0] * left[1][0] + left[1][1] * left[1][1] + Z(left[1]) * Z(left[1]));
            double speedRight = Math.Sqrt(right[1][0] * right[1][0] + right[1][1] * right[1][1] + Z(right[1]) * Z(right[1]));

            if (!(speedLeft > StillSpeed) || !(speedRight > StillSpeed))
            {
                return IsLeast(contact, hunt);
            }

            double cosLeft = (foot.Ux * left[1][0] + foot.Uy * left[1][1] + foot.Uz * Z(left[1])) / speedLeft;
            double cosRight = (foot.Ux * right[1][0] + foot.Uy * right[1][1] + foot.Uz * Z(right[1])) / speedRight;
            double turn = Math.Sqrt((left[1][0] - right[1][0]) * (left[1][0] - right[1][0]) + (left[1][1] - right[1][1]) * (left[1][1] - right[1][1]) + (Z(left[1]) - Z(right[1])) * (Z(left[1]) - Z(right[1])));
            bool smooth = hasLeft && hasRight && turn <= 1E-6 * Math.Max(speedLeft, speedRight);

            if (!smooth)
            {
                // At a corner or an end the distance must not fall going away from the contact either way along the curve.
                return (!hasRight || cosRight >= -foot.Slack) && (!hasLeft || cosLeft <= foot.Slack);
            }

            Variation(foot, along, out double slope, out double curvature);
            double slopeAlong = slope / speed;
            double curvatureAlong = curvature / (speed * speed);
            double flat = 1E-9 / hunt.Extent;

            // Curving up too little to rise a snap a thousandth of the size of the curve along it: a least only if the distance falls
            // neither way (FallsEitherWay). One that curves up more is the least the quadratic says, whatever lies beyond.
            bool weak = 0.5 * curvatureAlong * (1E-3 * Extent) * (1E-3 * Extent) < hunt.Snap;

            // Level, the distance is at its least only if it curves up: a distance that is the same all along, as for two curves side
            // by side, is at its least nowhere. In space a curve that runs parallel to its partner for a moment, a ring over a line that
            // is tangent to it and a thousandth above its plane, has a distance of the fourth order about its least, which no second
            // derivative shows; it is told from a level one by looking either way along the curve (<see cref="RisesBothWays"/>). A least that
            // curves up too little is one only if the distance falls neither way along the curve, in the plane as in space: a curve whose
            // offset from a line stops at an inflection, as a cubic's does across the line through its inflection, has a curvature of the
            // distance there that changes sign, and a root found a hair to one side of it reads as one that curves up, as the cubic
            // y = x³ / 100 turned by 0.7 of a radian a hundred thousand out did against the line 0.0001 over its inflection.
            // Held at an end of the partner, the distance is read as to that end, which it is on the side the partner does not go on; on the
            // side it goes on, its inside can be nearer, so a least there is one only where the curve is no nearer the partner either way:
            // the point of a copy moved 0.0007 square from the other's end is no place where the distance falls on into the stretch the two
            // run along, which it was from the side of the copy and was not from the other's.
            if (Math.Abs(slopeAlong) <= foot.Slack)
            {
                return (curvatureAlong > flat && (!weak || !FallsEitherWay(contact, hunt)) && (foot.Free || IsLeast(contact, hunt))) || (Dim == 3 && !contact.From.InPlaneAt(this, contact.Knot) && RisesBothWays(contact, hunt));
            }

            // On a slope: a minimum within the reach, if the distance curves up; or a meeting within the reach, going the way it
            // falls, which is the crossing of a curve at an angle, where the distance is a V and the roots of the polynomials it is
            // found by are found to a few snaps and not to the last digit.
            return (curvatureAlong > flat && Math.Abs(slopeAlong) <= curvatureAlong * hunt.Reach && (!weak || !FallsEitherWay(contact, hunt))) || foot.G <= Math.Abs(slopeAlong) * hunt.Reach;
        }

        /// <summary>
        /// Says whether the distance from the curve to its partner falls, by more than a snap, a hundredth of the size of the curve along it
        /// either way from a contact inside the piece the contact is on, or a thousandth of the size, or half that, and so on eleven times to
        /// a millionth: a least does not. It is asked of a least that curves up too little to rise a snap a thousandth of the size along. The
        /// offset of the cubic y = x³ / 100 from the line y = 0.0001 has fallen 8E-8 a thousandth of the size from its inflection, which a
        /// million out is under the snap, and 8E-5 a hundredth of it; and a crossing between two of the looks, as that of the cubic 1 000
        /// across with the line y = 1E-9 a tenth from its inflection, is told by a look in it, the steps being halved and not cut tenfold.
        /// </summary>
        private bool FallsEitherWay(Contact contact, Hunt hunt)
        {
            double[][] here = Derivatives(contact.Knot, 1);
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1] + Z(here[1]) * Z(here[1]));

            if (!(speed > StillSpeed))
            {
                return false;
            }

            var point = new double[Dim];
            BezierPieces pieces = Pieces;

            for (int side = -1; side <= 1; side += 2)
            {
                // Round the seam of a closed curve, from its other end.
                double from = AcrossSeam(contact.Knot, side);
                int piece = PieceAt(from, out bool atStart);
                int own = side < 0 && atStart && piece > 0 ? piece - 1 : piece;
                double bound = side < 0 ? pieces.From[own] : pieces.To[own];
                double far = from + side * 1E-2 * Extent / speed;
                far = side < 0 ? Math.Max(far, bound) : Math.Min(far, bound);

                if (far != from)
                {
                    PointAt(far, point);

                    if (contact.From.Gap(point[0], point[1], Dim == 3 ? point[2] : 0.0, out _, out _, out _, out _) < contact.Gap - hunt.Snap)
                    {
                        return true;
                    }
                }

                double share = 1E-3;

                for (int ladder = 0; ladder < 11; ladder++, share *= 0.5)
                {
                    double knot = Around(from + side * share * Extent / speed);

                    if (knot == from)
                    {
                        break;
                    }

                    PointAt(knot, point);

                    if (contact.From.Gap(point[0], point[1], Dim == 3 ? point[2] : 0.0, out _, out _, out _, out _) < contact.Gap - hunt.Snap)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The nearest point of a partner to a point of a curve, and what the slope and the curvature of the distance along the curve
        /// are made of there.
        /// </summary>
        internal struct Foot
        {
            /// <summary>The distance from the point to the partner.</summary>
            internal double G;

            /// <summary>The unit direction from the partner to the point.</summary>
            internal double Ux, Uy, Uz;

            /// <summary>The first and second derivative of the partner's nearest point, by its own number.</summary>
            internal double R1x, R1y, R1z, R2x, R2y, R2z;

            /// <summary>The length of the first derivative.</summary>
            internal double PartnerSpeed;

            /// <summary>Whether the nearest point is free to move along the partner, so that it is not held at an end.</summary>
            internal bool Free;

            /// <summary>How near nought a slope that is nought can read, by cosine.</summary>
            internal double Slack;

            /// <summary>Whether the partner is a surface, a plane or a ball, whose distance bends by <see cref="Bend"/> where a curve's partner's frame would give it.</summary>
            internal bool Surface;

            /// <summary>For a surface, how the distance bends square to the direction to the point (<see cref="Partner.TryGetSurface"/>).</summary>
            internal double Bend;
        }

        /// <summary>
        /// Gets a number of a row of derivatives: the third coordinate, which a curve in the plane does not have, is nought.
        /// </summary>
        internal static double Z(double[] row) => row.Length > 2 ? row[2] : 0.0;

        /// <summary>
        /// Finds the nearest point of a partner to a point and the direction to the point.
        /// </summary>
        /// <param name="partner">The partner.</param>
        /// <param name="px">The point.</param>
        /// <param name="py">The point.</param>
        /// <param name="pz">The point; nought in the plane.</param>
        /// <param name="scale">The size of the numbers the shapes are given in, which the digits of the point are measured by.</param>
        /// <param name="dim">The number of coordinates of the point, 2 or 3.</param>
        /// <param name="foot">What was found; its distance is given even when false is returned.</param>
        /// <returns>false when the partner cannot give its derivatives, or the point is on it.</returns>
        /// <remarks>
        /// The direction is the normal of the partner where its nearest point is free to move along it, which is exact, where the
        /// difference of two points a few digits apart at a distance of a few snaps is not. In space a curve has no one normal, and the
        /// direction is the line to the nearest point with what runs along the partner taken off it. A plane and a ball give their own.
        /// </remarks>
        internal static bool TryGetFoot(Partner partner, double px, double py, double pz, double scale, int dim, out Foot foot)
        {
            foot = default(Foot);
            foot.G = partner.Gap(px, py, pz, out double qx, out double qy, out double qz, out double other);

            if (!(foot.G > 0.0))
            {
                return false;
            }

            if (partner.TryGetSurface(px, py, pz, out foot.Ux, out foot.Uy, out foot.Uz, out foot.Bend, out double surfaceNoise))
            {
                foot.Surface = true;
                foot.Free = true;
                foot.Slack = 1E-9 + surfaceNoise;
                return true;
            }

            if (!partner.TryGetFrame(qx, qy, qz, other, out foot.R1x, out foot.R1y, out foot.R1z, out foot.R2x, out foot.R2y, out foot.R2z))
            {
                return false;
            }

            double rx = px - qx;
            double ry = py - qy;
            double rz = pz - qz;
            double noise = 4.0 * 2.220446049250313E-16 * scale / foot.G;
            foot.PartnerSpeed = Math.Sqrt(foot.R1x * foot.R1x + foot.R1y * foot.R1y + foot.R1z * foot.R1z);
            foot.Free = foot.PartnerSpeed > 0.0 && partner.IsFree(other);
            foot.Ux = rx / foot.G;
            foot.Uy = ry / foot.G;
            foot.Uz = rz / foot.G;
            foot.Slack = 1E-9 + noise;

            if (foot.Free)
            {
                if (dim == 2)
                {
                    double side = rx * -foot.R1y + ry * foot.R1x >= 0.0 ? 1.0 : -1.0;
                    foot.Ux = side * -foot.R1y / foot.PartnerSpeed;
                    foot.Uy = side * foot.R1x / foot.PartnerSpeed;
                }
                else
                {
                    double tx = foot.R1x / foot.PartnerSpeed;
                    double ty = foot.R1y / foot.PartnerSpeed;
                    double tz = foot.R1z / foot.PartnerSpeed;
                    double along = rx * tx + ry * ty + rz * tz;
                    double ox = rx - along * tx;
                    double oy = ry - along * ty;
                    double oz = rz - along * tz;
                    double length = Math.Sqrt(ox * ox + oy * oy + oz * oz);

                    if (length > 0.0)
                    {
                        foot.Ux = ox / length;
                        foot.Uy = oy / length;
                        foot.Uz = oz / length;
                    }
                }

                // In the plane the direction is the partner's normal, exact; in space it is the line to the nearest point, which carries
                // the rounding of the coordinates over the distance.
                foot.Slack = dim == 2 ? 1E-9 : 1E-9 + noise;
            }

            return true;
        }

        /// <summary>
        /// Gets the slope and the curvature of the distance from a curve to a partner along the curve, by its parameter, at a point of it.
        /// </summary>
        /// <param name="foot">The nearest point of the partner to the point of the curve.</param>
        /// <param name="along">The point of the curve and its first and second derivative.</param>
        /// <param name="slope">The first derivative of the distance.</param>
        /// <param name="curvature">The second derivative of the distance.</param>
        /// <remarks>
        /// The slope is the cosine of the angle between the curve's direction and the line to the partner, times the speed of the
        /// curve, and the curvature is the second variation of half the squared distance, less the square of the slope, over the
        /// distance. The second variation is, the partner's nearest point moving to keep as near as it can, with a = u . P'' and
        /// b = u . Q'', ((P' x Q')² + g (a |Q'|² - b |P'|²) - g² a b) / (|Q'|² - g b): the first term is |P'|² |Q'|² less (P' . Q')²,
        /// written as the square of a cross product so that two tangents that run side by side, whose two terms are alike to the last
        /// digit, leave no rounding behind. Held at an end of the partner, the point does not move, and it is |P'|² + g a. In space the
        /// first term is the squared length of the cross product, which is the same identity. For a surface the nearest point moves in two
        /// directions and the curvature is its bend times the speed squared less the slope squared, plus a (<see cref="Partner.TryGetSurface"/>).
        /// </remarks>
        internal static void Variation(Foot foot, double[][] along, out double slope, out double curvature)
        {
            double z1 = Z(along[1]);
            double z2 = Z(along[2]);
            double speed2 = along[1][0] * along[1][0] + along[1][1] * along[1][1] + z1 * z1;
            double a = foot.Ux * along[2][0] + foot.Uy * along[2][1] + foot.Uz * z2;

            if (foot.Surface)
            {
                slope = foot.Ux * along[1][0] + foot.Uy * along[1][1] + foot.Uz * z1;
                curvature = foot.Bend * (speed2 - slope * slope) + a;
                return;
            }

            double b = foot.Ux * foot.R2x + foot.Uy * foot.R2y + foot.Uz * foot.R2z;
            double partner2 = foot.R1x * foot.R1x + foot.R1y * foot.R1y + foot.R1z * foot.R1z;
            double stiffness = partner2 - foot.G * b;
            double second = speed2 + foot.G * a;

            if (foot.Free && stiffness > 0.0)
            {
                double crossX = along[1][1] * foot.R1z - z1 * foot.R1y;
                double crossY = z1 * foot.R1x - along[1][0] * foot.R1z;
                double crossZ = along[1][0] * foot.R1y - along[1][1] * foot.R1x;
                double cross2 = crossX * crossX + crossY * crossY + crossZ * crossZ;
                second = (cross2 + foot.G * (a * partner2 - b * speed2) - foot.G * foot.G * a * b) / stiffness;
            }

            slope = foot.Ux * along[1][0] + foot.Uy * along[1][1] + foot.Uz * z1;
            curvature = (second - slope * slope) / foot.G;
        }

        /// <summary>
        /// Brings a place of the curve where the distance to a partner is not nought to the least of it, by Newton's method on the
        /// distance along the curve, not on its square: where the curve touches a straight partner, tangent to it, the distance is
        /// the square of how far from the touch the place is and the squared distance the fourth power, which Newton's method on it
        /// takes a halving of the step at a time to reach, where on the distance it takes one step.
        /// </summary>
        /// <remarks>
        /// It moves only inside the piece it is on, only where the distance has a positive curvature, and only to a place that is no
        /// farther from the partner, so it can leave a place where it was but never make it worse.
        /// </remarks>
        internal double Settle(Partner partner, double knot, Hunt hunt)
        {
            BezierPieces pieces = Pieces;
            int piece = PieceAt(knot, out _);
            double low = pieces.From[piece];
            double high = pieces.To[piece];
            double s = knot;
            var query = new double[Dim];

            for (int step = 0; step < 30; step++)
            {
                double[][] derivatives = Derivatives(s, 2);

                if (!TryGetFoot(partner, derivatives[0][0], derivatives[0][1], Z(derivatives[0]), hunt.Scale, Dim, out Foot foot) || !(foot.G > hunt.Snap))
                {
                    return s;
                }

                Variation(foot, derivatives, out double slope, out double curvature);
                double speed2 = derivatives[1][0] * derivatives[1][0] + derivatives[1][1] * derivatives[1][1] + Z(derivatives[1]) * Z(derivatives[1]);

                if (!(curvature > 1E-9 / hunt.Extent * speed2))
                {
                    return s;
                }

                double move = -slope / curvature;
                bool moved = false;

                for (int half = 0; half < 6 && !moved && Math.Abs(move) > 1E-15 * (1.0 + Math.Abs(s)); half++)
                {
                    double next = Math.Min(Math.Max(s + move, low), high);
                    PointAt(next, query);
                    double distance = partner.Gap(query[0], query[1], Dim == 3 ? query[2] : 0.0, out _, out _, out _, out _);

                    if (next != s && distance <= foot.G)
                    {
                        s = next;
                        moved = true;

                        // A step that gains less than a snap is no gain: along a stretch where the curve is as near as rounding tells, the
                        // distance is level and Newton's method would go on for ever.
                        if (foot.G - distance <= hunt.Snap)
                        {
                            return s;
                        }
                    }

                    move *= 0.5;
                }

                if (!moved)
                {
                    return s;
                }
            }

            return s;
        }

        /// <summary>
        /// Says whether the distance from the curve to its partner has risen a thousandth of the size of the curve either way along it
        /// from a contact, by more than the digits can tell, and has fallen nowhere on the way: a least that has no curvature, a distance
        /// of the fourth order, has risen by then, and a stretch where the distance is level has not.
        /// </summary>
        /// <remarks>
        /// A hump between two crossings of a plane, which falls to nought either side of it, is further out than a thousandth of the size
        /// of the curve from the middle for a plane a ten-thousandth off the curve, and the distance there is higher than at the middle:
        /// the steps taken are a thousandth, a ten-thousandth and so on down to a millionth, and the distance is not to have fallen at
        /// any of them by as much as a snap, which a hump between two near touches that are a few snaps deep has done by the second.
        /// </remarks>
        private bool RisesBothWays(Contact contact, Hunt hunt)
        {
            double[][] here = Derivatives(contact.Knot, 1);
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1] + Z(here[1]) * Z(here[1]));

            if (!(speed > StillSpeed))
            {
                return false;
            }

            double band = 8.0 * hunt.Snap;
            double fall = hunt.Snap;
            var point = new double[Dim];

            for (int side = -1; side <= 1; side += 2)
            {
                double share = 1E-3;
                double from = AcrossSeam(contact.Knot, side);

                for (int ladder = 0; ladder < 4; ladder++, share *= 0.1)
                {
                    double knot = Around(from + side * share * Extent / speed);

                    if (knot == from)
                    {
                        return false;
                    }

                    PointAt(knot, point);
                    double gap = contact.From.Gap(point[0], point[1], Dim == 3 ? point[2] : 0.0, out _, out _, out _, out _);

                    if (gap < contact.Gap - fall || (ladder == 0 && !(gap > contact.Gap + band) && !RisesFurther(contact, from, side, speed, band, fall, point)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Says whether the distance from the curve to its partner, not yet risen a thousandth of the size of the curve along it from a
        /// contact, has risen a hundredth or a tenth of it along, inside the piece the contact is on, and fallen at neither: a least of the
        /// sixth order, a spline whose inflection a line passes over half a thousandth above its plane, has risen 6E-12 a thousandth of the
        /// size along and 6E-6 a hundredth along. The piece bounds it, so that a stretch where the distance is level does not take the
        /// rise of the piece beyond it for its own.
        /// </summary>
        private bool RisesFurther(Contact contact, double from, int side, double speed, double band, double fall, double[] point)
        {
            BezierPieces pieces = Pieces;
            int piece = PieceAt(from, out bool atStart);

            if (side < 0 && atStart && piece > 0)
            {
                piece--;
            }

            double bound = side < 0 ? pieces.From[piece] : pieces.To[piece];

            for (double share = 1E-2; share <= 0.11; share *= 10.0)
            {
                double knot = from + side * share * Extent / speed;
                knot = side < 0 ? Math.Max(knot, bound) : Math.Min(knot, bound);

                if (knot == from)
                {
                    return false;
                }

                PointAt(knot, point);
                double gap = contact.From.Gap(point[0], point[1], Dim == 3 ? point[2] : 0.0, out _, out _, out _, out _);

                if (gap < contact.Gap - fall)
                {
                    return false;
                }

                if (gap > contact.Gap + band)
                {
                    return true;
                }

                if (knot == bound)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Says whether a contact is a local least of the curve's distance to its partner, by looking a little way either
        /// side along the curve.
        /// </summary>
        private bool IsLeast(Contact contact, Hunt hunt)
        {
            BezierPieces pieces = Pieces;
            int piece = PieceAt(contact.Knot, out _);
            double width = pieces.To[piece] - pieces.From[piece];
            double step = 1E-6 * width;
            double noise = Hunt.SnapShare * hunt.Scale;

            for (int side = -1; side <= 1; side += 2)
            {
                double from = AcrossSeam(contact.Knot, side);
                double knot = Around(from + side * step);

                if (knot == from)
                {
                    continue;
                }

                PointAt(knot, hunt.Point);

                if (contact.From.Gap(hunt.Point[0], hunt.Point[1], hunt.Point[2], out _, out _, out _, out _) < contact.Gap - noise)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the nearest approach of a curve to its partners, a crossing where they meet.
        /// </summary>
        /// <param name="partners">The partners: a shape that is several pieces, such as a polyline, gives each.</param>
        /// <param name="nearest">The nearest approach, with ties between places as near broken by the lowest knot value of the curve.</param>
        internal bool TryGetNearest(IList<Partner> partners, out Contact nearest)
        {
            Hunt hunt = Begin(partners, true, 0.0);

            for (int i = 0; i < partners.Count; i++)
            {
                partners[i].Find(this, hunt);
            }

            return hunt.TryGetNearest(out nearest);
        }

        /// <summary>
        /// Gets the places where a curve meets its partners or comes within a reach of them without crossing: where it
        /// crosses, where it touches, and where it ends near, in the order along the curve.
        /// </summary>
        /// <param name="partners">The partners.</param>
        /// <param name="reach">How near a place may be, the point tolerance.</param>
        /// <remarks>
        /// A place is a meeting, or a least of the distance between the two within the reach that no other partner is nearer at
        /// (<see cref="IsMinimum"/>). Those no further apart than the reach, one from the next, are one place, the nearer kept, and so
        /// are two near touches next to each other along the curve within ten times the reach with no point between them farther from the
        /// partners than at either by more than four snaps (<see cref="MergeBasins"/>); two meetings are only one place when they are
        /// within the reach of each other, whatever lies between, a meeting in the plane that is one only to the snap and is no least of the
        /// distance is left out beside a meeting to the digits (<see cref="KeepLoose"/>), and in space a least
        /// within the reach of a meeting is left out, so that it does not join two (<see cref="DropBesideMeetings"/>). A place made of
        /// several contacts is a crossing, or a near crossing, if any of them is. Where the curve runs along a partner for a stretch, to
        /// rounding, or along another curve within the reach and level for a quarter of the size of the smaller (<see cref="DropAlong"/>),
        /// it is not crossing it there, and nothing is given along the stretch.
        /// </remarks>
        internal List<Contact> GetMeetings(IList<Partner> partners, double reach)
        {
            Hunt hunt = Begin(partners, false, reach);

            for (int i = 0; i < partners.Count; i++)
            {
                partners[i].Find(this, hunt);
            }

            var kept = new List<Contact>();
            var pending = new List<Contact>();
            var loose = new List<Contact>();

            for (int i = 0; i < hunt.Found.Count; i++)
            {
                Contact contact = hunt.Found[i];

                if (contact.Overlap)
                {
                    continue;
                }

                if (contact.Verified)
                {
                    (Loose(contact, hunt) ? loose : kept).Add(contact);
                }
                else if (contact.Least || IsMinimum(contact, hunt))
                {
                    pending.Add(contact);
                }
            }

            kept.AddRange(KeepLoose(loose, kept, hunt));
            List<Contact> leasts = DropNearerElsewhere(pending, partners, hunt);
            kept.AddRange(Dim == 3 ? DropBesideMeetings(leasts, kept, reach, hunt) : leasts);
            kept.Sort(Compare);

            // Places no further apart than the reach, one from the next, are one place, the nearer kept: a run of near contacts is the
            // one place it is. Each is checked against those before it within the reach of it, found by the cells of a grid round the one it
            // falls in, so that a thousand places cost a thousand looks and not a million. The cells are as wide as the reach over the
            // square root of two where the digits allow, so that two in one cell are within the reach of each other and are one place
            // already: a cell that has a place within the reach of a new one has no need to be looked at further, which keeps a hundred
            // thousand places that rounding left at one touch from costing ten billion looks.
            // In space the cells are as wide as the reach over the square root of three, so that two in one cell are within it of each other.
            double diagonal = Dim == 3 ? 1.7320508075688772 : 1.4142135623730951;
            double cell = Math.Max(reach / diagonal, 1E-9 * hunt.Scale);
            bool mutual = cell * diagonal <= reach;
            int span = (int)Math.Ceiling(reach / cell);
            var grid = new Dictionary<long, List<int>>(new CellComparer());
            var parent = new int[kept.Count];

            for (int i = 0; i < kept.Count; i++)
            {
                parent[i] = i;
                Contact contact = kept[i];
                long cx = (long)Math.Floor(contact.Px / cell);
                long cy = (long)Math.Floor(contact.Py / cell);
                long cz = Dim == 3 ? (long)Math.Floor(contact.Pz / cell) : 0L;
                long zLow = Dim == 3 ? cz - span : 0L;
                long zHigh = Dim == 3 ? cz + span : 0L;

                for (long gx = cx - span; gx <= cx + span; gx++)
                {
                    for (long gy = cy - span; gy <= cy + span; gy++)
                    {
                        for (long gz = zLow; gz <= zHigh; gz++)
                        {
                            if (!grid.TryGetValue(CellKey(gx, gy, gz), out List<int> inCell))
                            {
                                continue;
                            }

                            foreach (int j in inCell)
                            {
                                double dx = kept[j].Px - contact.Px;
                                double dy = kept[j].Py - contact.Py;
                                double dz = kept[j].Pz - contact.Pz;

                                if (dx * dx + dy * dy + dz * dz <= reach * reach)
                                {
                                    Join(parent, i, j);

                                    if (mutual)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                Register(grid, cx, cy, cz, i);
            }

            // Of each run, the contact nearest the partner, the first along the curve where as near.
            var best = new int[kept.Count];

            for (int i = 0; i < best.Length; i++)
            {
                best[i] = -1;
            }

            for (int i = 0; i < kept.Count; i++)
            {
                int root = Root(parent, i);

                if (best[root] < 0 || kept[i].Raw < kept[best[root]].Raw)
                {
                    best[root] = i;
                }
            }

            // A run that holds a crossing is a crossing, or in space a near crossing, whichever of its contacts is nearest: a crossing that
            // the offset found inside a stretch along is not lost to a meeting Newton's method settled on a few units of rounding nearer.
            var crossing = new bool[kept.Count];
            var dip = new bool[kept.Count];

            for (int i = 0; i < kept.Count; i++)
            {
                int root = Root(parent, i);
                crossing[root] |= kept[i].Crossing;
                dip[root] |= kept[i].Dip;
            }

            var meetings = new List<Contact>();

            for (int i = 0; i < kept.Count; i++)
            {
                if (best[i] >= 0)
                {
                    Contact place = kept[best[i]];
                    place.Crossing |= crossing[i];
                    place.Dip |= dip[i];
                    meetings.Add(place);
                }
            }

            meetings.Sort(Compare);

            return DropAlong(MergeBasins(meetings, partners, hunt), hunt);
        }

        /// <summary>
        /// Says whether a meeting is one only to the snap, in the plane: not a crossing the offset found, and further from the partner than
        /// the numbers can be evaluated to (<see cref="Evaluated"/>).
        /// </summary>
        private bool Loose(Contact contact, Hunt hunt) => Dim == 2 && !contact.Crossing && contact.Raw > Evaluated(hunt);

        /// <summary>
        /// Gets how near nought a distance can be told from nought by evaluating the curves: sixteen units in the last place of the size of
        /// the numbers, a sixth of a hundredth of the snap.
        /// </summary>
        private static double Evaluated(Hunt hunt) => 16.0 * 2.220446049250313E-16 * hunt.Scale;

        /// <summary>
        /// Gives the meetings that are meetings only to the snap, but for one that lies within ten times the reach of a meeting to the digits
        /// on the same partner and is no least of the distance: the curve comes nearer the partner a little way along it, one way or the
        /// other, by more than the numbers can be evaluated to.
        /// </summary>
        /// <remarks>
        /// Along a tangency far from the origin the curve is within the snap of the partner for a long way, and Newton's method settles
        /// on points of the slope there that read as meetings: a quadratic sitting on the circle spline of radius 100 a million from the
        /// origin is 2.3E-8 from it 0.002 either side of the touch, under the snap of 1E-7 and well over the 3.6E-9 the numbers are
        /// evaluated to, and was given those two places besides the touch, where the circle gives one. A meeting to the digits is kept
        /// whatever lies beside it, so two crossings a few snaps deep are two places, as the exact curves have them, and a loose meeting
        /// with no exact one beside it stays, since it is the only reading of the place there is.
        /// </remarks>
        private List<Contact> KeepLoose(List<Contact> loose, List<Contact> exact, Hunt hunt)
        {
            var kept = new List<Contact>(loose.Count);

            if (loose.Count == 0)
            {
                return kept;
            }

            double near = 10.0 * hunt.Reach;
            double evaluated = Evaluated(hunt);

            // The exact meetings in cells as wide as the reach looked within, one grid for each partner, so that a loose meeting looks at the
            // nine cells round it and not at every exact one: an arc a million out against its flattening into 65 536 chords has as many of
            // each.
            double cell = Math.Max(near, 1E-9 * hunt.Scale);
            var grid = new Dictionary<long, List<int>>(new CellComparer());

            for (int i = 0; i < exact.Count; i++)
            {
                Register(grid, (long)Math.Floor(exact[i].Px / cell), (long)Math.Floor(exact[i].Py / cell), exact[i].Part, i);
            }

            foreach (Contact contact in loose)
            {
                bool beside = false;
                long cx = (long)Math.Floor(contact.Px / cell);
                long cy = (long)Math.Floor(contact.Py / cell);

                for (long gx = cx - 1; gx <= cx + 1 && !beside; gx++)
                {
                    for (long gy = cy - 1; gy <= cy + 1 && !beside; gy++)
                    {
                        if (!grid.TryGetValue(CellKey(gx, gy, contact.Part), out List<int> inCell))
                        {
                            continue;
                        }

                        foreach (int j in inCell)
                        {
                            Contact other = exact[j];
                            double dx = other.Px - contact.Px;
                            double dy = other.Py - contact.Py;

                            if (other.Part == contact.Part && dx * dx + dy * dy <= near * near)
                            {
                                beside = true;
                                break;
                            }
                        }
                    }
                }

                if (!beside || !FallsAlong(contact, hunt.Reach, evaluated))
                {
                    kept.Add(contact);
                }
            }

            return kept;
        }

        /// <summary>
        /// Says whether the distance from the curve to the partner falls below that at a contact, by more than a band, a little way along
        /// the curve either side of it: a millionth of the size of the curve, and twice that each time, out to a length.
        /// </summary>
        private bool FallsAlong(Contact contact, double length, double band)
        {
            double[][] here = Derivatives(contact.Knot, 1);
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1] + Z(here[1]) * Z(here[1]));

            if (!(speed > StillSpeed))
            {
                return false;
            }

            var point = new double[Dim];

            for (int side = -1; side <= 1; side += 2)
            {
                double from = AcrossSeam(contact.Knot, side);

                for (double step = 1E-6 * Extent; step <= length; step *= 2.0)
                {
                    double knot = Around(from + side * step / speed);

                    if (knot == from)
                    {
                        break;
                    }

                    PointAt(knot, point);

                    if (contact.From.Gap(point[0], point[1], Dim == 3 ? point[2] : 0.0, out _, out _, out _, out _) < contact.Raw - band)
                    {
                        return true;
                    }

                    if (!IsClosed && (knot == Start || knot == End))
                    {
                        break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Takes out the places inside a stretch where the curve lies along another curve to within the reach (<see cref="SplinePartner"/>
        /// finds them): the places a curve as good as the same as another has are where the rounding of the numbers has the two touch or
        /// cross, which is no place. A crossing found there by the offset of the one from the other changing sign by more than a hundredth
        /// of the reach each way stays: the curves cross at a shallow angle; and so does, for two curves in space that share no plane, a
        /// least of the distance where the two would cross if brought into one plane (<see cref="Contact.Dip"/>).
        /// </summary>
        private static List<Contact> DropAlong(List<Contact> places, Hunt hunt)
        {
            if (hunt.Runs.Count == 0)
            {
                return places;
            }

            var kept = new List<Contact>(places.Count);

            foreach (Contact place in places)
            {
                if (place.Crossing || place.Dip || !hunt.IsAlong(place.Part, place.Knot, place.Knot))
                {
                    kept.Add(place);
                }
            }

            return kept;
        }


        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static void Join(int[] parent, int i, int j)
        {
            int a = Root(parent, i);
            int b = Root(parent, j);

            if (a != b)
            {
                parent[Math.Max(a, b)] = Math.Min(a, b);
            }
        }

        /// <summary>
        /// Finds, for each of a list of points, the least distance to any of the partners but one, among those within a radius of it.
        /// </summary>
        /// <param name="partners">The partners.</param>
        /// <param name="qx">The points.</param>
        /// <param name="qy">The points.</param>
        /// <param name="qz">The points; nought in the plane.</param>
        /// <param name="radius">How far from each point a partner must be to be of interest; no more than nought leaves the point out.</param>
        /// <param name="skip">The position of the one partner to leave out for each point, or -1.</param>
        /// <param name="least">The least distance found for each point, or infinity when no partner is within its radius.</param>
        /// <param name="dim">The number of coordinates of the points, 2 or 3.</param>
        /// <remarks>
        /// The partners are the edges of a polygon, a chain or a polyline, which may be a million, and the points the few places a curve
        /// comes near them: the work is done from the partners' side, each partner looking up the cells of a grid of the points that its
        /// box reaches, so that it costs the number of the partners and of the pairs that are near, not their product.
        /// </remarks>
        private static void MinGaps(IList<Partner> partners, double[] qx, double[] qy, double[] qz, double[] radius, int[] skip, double[] least, int dim)
        {
            int n = qx.Length;
            var probe = new double[3];
            double widest = 0.0;

            for (int k = 0; k < n; k++)
            {
                least[k] = double.PositiveInfinity;
                widest = Math.Max(widest, radius[k]);
            }

            if (!(widest > 0.0))
            {
                return;
            }

            double cell = 2.0 * widest;
            Dictionary<long, List<int>> grid = null;

            if ((long)partners.Count * n > 4096)
            {
                grid = new Dictionary<long, List<int>>(new CellComparer());

                for (int k = 0; k < n; k++)
                {
                    if (radius[k] > 0.0)
                    {
                        Register(grid, (long)Math.Floor(qx[k] / cell), (long)Math.Floor(qy[k] / cell), dim == 3 ? (long)Math.Floor(qz[k] / cell) : 0L, k);
                    }
                }
            }

            for (int j = 0; j < partners.Count; j++)
            {
                Partner partner = partners[j];

                if (grid == null)
                {
                    for (int k = 0; k < n; k++)
                    {
                        Measure(partner, k, qx, qy, qz, radius, skip, least, probe, dim);
                    }

                    continue;
                }

                partner.GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ);
                double cx0 = Math.Floor((minX - widest) / cell);
                double cx1 = Math.Floor((maxX + widest) / cell);
                double cy0 = Math.Floor((minY - widest) / cell);
                double cy1 = Math.Floor((maxY + widest) / cell);
                double cz0 = dim == 3 ? Math.Floor((minZ - widest) / cell) : 0.0;
                double cz1 = dim == 3 ? Math.Floor((maxZ + widest) / cell) : 0.0;

                if (double.IsInfinity(cx0) || double.IsInfinity(cx1) || double.IsInfinity(cy0) || double.IsInfinity(cy1) || double.IsInfinity(cz0) || double.IsInfinity(cz1) || (cx1 - cx0 + 1.0) * (cy1 - cy0 + 1.0) * (cz1 - cz0 + 1.0) > n)
                {
                    // A box that reaches more cells than there are points: look at the points.
                    for (int k = 0; k < n; k++)
                    {
                        if (qx[k] >= minX - radius[k] && qx[k] <= maxX + radius[k] && qy[k] >= minY - radius[k] && qy[k] <= maxY + radius[k] && (dim == 2 || (qz[k] >= minZ - radius[k] && qz[k] <= maxZ + radius[k])))
                        {
                            Measure(partner, k, qx, qy, qz, radius, skip, least, probe, dim);
                        }
                    }

                    continue;
                }

                for (long gx = (long)cx0; gx <= (long)cx1; gx++)
                {
                    for (long gy = (long)cy0; gy <= (long)cy1; gy++)
                    {
                        for (long gz = (long)cz0; gz <= (long)cz1; gz++)
                        {
                            if (grid.TryGetValue(CellKey(gx, gy, gz), out List<int> inCell))
                            {
                                foreach (int k in inCell)
                                {
                                    Measure(partner, k, qx, qy, qz, radius, skip, least, probe, dim);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void Measure(Partner partner, int k, double[] qx, double[] qy, double[] qz, double[] radius, int[] skip, double[] least, double[] probe, int dim)
        {
            if (!(radius[k] > 0.0) || partner.Position == skip[k])
            {
                return;
            }

            probe[0] = qx[k];
            probe[1] = qy[k];
            probe[2] = dim == 3 ? qz[k] : 0.0;

            if (partner.Bound(probe, probe, 0) <= Math.Min(radius[k], least[k]))
            {
                double gap = partner.Gap(qx[k], qy[k], dim == 3 ? qz[k] : 0.0, out _, out _, out _, out _);

                if (gap < least[k])
                {
                    least[k] = gap;
                }
            }
        }

        /// <summary>
        /// Drops the places that another of the partners is nearer to than the one they were found against at their points, so that
        /// they are not leasts of the distance to the shape: the places a curve comes near the edges of a polygon are those of the
        /// polygon, and an edge's own nearest place that another edge is nearer to is not one.
        /// </summary>
        private List<Contact> DropNearerElsewhere(List<Contact> pending, IList<Partner> partners, Hunt hunt)
        {
            if (partners.Count < 2 || pending.Count == 0)
            {
                return pending;
            }

            double slack = 8E-13 * hunt.Scale;
            int n = pending.Count;
            var qx = new double[n];
            var qy = new double[n];
            var qz = new double[n];
            var radius = new double[n];
            var skip = new int[n];
            var least = new double[n];

            for (int i = 0; i < n; i++)
            {
                qx[i] = pending[i].Px;
                qy[i] = pending[i].Py;
                qz[i] = pending[i].Pz;
                radius[i] = pending[i].Gap - slack;
                skip[i] = pending[i].Part;
            }

            MinGaps(partners, qx, qy, qz, radius, skip, least, Dim);
            var kept = new List<Contact>(n);

            for (int i = 0; i < n; i++)
            {
                if (!(least[i] < radius[i]))
                {
                    kept.Add(pending[i]);
                }
            }

            return kept;
        }

        /// <summary>
        /// Drops, in space, the leasts of the distance that lie within the reach of a meeting: such a least would be one place with the
        /// meeting, the meeting kept, and kept until then it joins two meetings further apart than the reach into one, which two crossings
        /// are not. A line a millionth of a radian off the tangent of a spline in space crosses it twice 0.0011 apart, and the slope of
        /// the distance between them, falling to each, was a place that made the two one.
        /// </summary>
        private static List<Contact> DropBesideMeetings(List<Contact> leasts, List<Contact> meetings, double reach, Hunt hunt)
        {
            if (leasts.Count == 0 || meetings.Count == 0)
            {
                return leasts;
            }

            double cell = Math.Max(reach, 1E-9 * hunt.Scale);
            var grid = new Dictionary<long, List<int>>(new CellComparer());

            for (int i = 0; i < meetings.Count; i++)
            {
                Register(grid, (long)Math.Floor(meetings[i].Px / cell), (long)Math.Floor(meetings[i].Py / cell), (long)Math.Floor(meetings[i].Pz / cell), i);
            }

            var kept = new List<Contact>(leasts.Count);

            foreach (Contact least in leasts)
            {
                long cx = (long)Math.Floor(least.Px / cell);
                long cy = (long)Math.Floor(least.Py / cell);
                long cz = (long)Math.Floor(least.Pz / cell);
                bool beside = false;

                for (long gx = cx - 1; gx <= cx + 1 && !beside; gx++)
                {
                    for (long gy = cy - 1; gy <= cy + 1 && !beside; gy++)
                    {
                        for (long gz = cz - 1; gz <= cz + 1 && !beside; gz++)
                        {
                            if (!grid.TryGetValue(CellKey(gx, gy, gz), out List<int> inCell))
                            {
                                continue;
                            }

                            foreach (int j in inCell)
                            {
                                double dx = meetings[j].Px - least.Px;
                                double dy = meetings[j].Py - least.Py;
                                double dz = meetings[j].Pz - least.Pz;

                                if (dx * dx + dy * dy + dz * dz <= reach * reach)
                                {
                                    beside = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!beside)
                {
                    kept.Add(least);
                }
            }

            return kept;
        }

        /// <summary>
        /// Joins the near touches that lie in one basin of the distance: two next to each other along the curve, within ten times the
        /// reach of each other, with no point between them where the curve is farther from the partners than at either, by more than
        /// four snaps. A curve that touches a partner is as near as rounding can tell all along a stretch, and each place the rounding
        /// finds in it is the one touch. Two meetings are not joined, whatever lies between them: crossings are places, and are one
        /// only when they are within the reach of each other, as the exact curves have it.
        /// </summary>
        private List<Contact> MergeBasins(List<Contact> places, IList<Partner> partners, Hunt hunt)
        {
            const int Samples = 8;

            if (places.Count < 2)
            {
                return places;
            }

            double band = 4.0 * hunt.Snap;
            double near = 10.0 * hunt.Reach;
            var pairs = new List<int>();

            for (int i = 0; i + 1 < places.Count; i++)
            {
                double dx = places[i + 1].Px - places[i].Px;
                double dy = places[i + 1].Py - places[i].Py;
                double dz = places[i + 1].Pz - places[i].Pz;

                if (dx * dx + dy * dy + dz * dz <= near * near && !(places[i].Verified && places[i + 1].Verified))
                {
                    pairs.Add(i);
                }
            }

            if (pairs.Count == 0)
            {
                return places;
            }

            int n = pairs.Count * Samples;
            var qx = new double[n];
            var qy = new double[n];
            var qz = new double[n];
            var radius = new double[n];
            var skip = new int[n];
            var least = new double[n];
            var point = new double[Dim];

            for (int p = 0; p < pairs.Count; p++)
            {
                Contact last = places[pairs[p]];
                Contact next = places[pairs[p] + 1];

                for (int k = 1; k <= Samples; k++)
                {
                    PointAt(last.Knot + (next.Knot - last.Knot) * k / (Samples + 1.0), point);
                    int at = p * Samples + k - 1;
                    qx[at] = point[0];
                    qy[at] = point[1];
                    qz[at] = Dim == 3 ? point[2] : 0.0;
                    radius[at] = Math.Max(last.Raw, next.Raw) + band;
                    skip[at] = -1;
                }
            }

            MinGaps(partners, qx, qy, qz, radius, skip, least, Dim);
            var joined = new bool[places.Count];

            for (int p = 0; p < pairs.Count; p++)
            {
                bool same = true;

                for (int k = 0; k < Samples && same; k++)
                {
                    same = least[p * Samples + k] <= radius[p * Samples + k];
                }

                joined[pairs[p]] = same;
            }

            // A run of places joined one to the next is one place, the nearest to the partners, the first along the curve where as near.
            var merged = new List<Contact>();
            int from = 0;

            while (from < places.Count)
            {
                int to = from;

                while (to + 1 < places.Count && joined[to])
                {
                    to++;
                }

                int best = from;
                bool crossing = places[from].Crossing;
                bool dip = places[from].Dip;

                for (int i = from + 1; i <= to; i++)
                {
                    if (places[i].Raw < places[best].Raw)
                    {
                        best = i;
                    }

                    crossing |= places[i].Crossing;
                    dip |= places[i].Dip;
                }

                Contact place = places[best];
                place.Crossing = crossing;
                place.Dip = dip;
                merged.Add(place);
                from = to + 1;
            }

            return merged;
        }

        // The key of a cell: the two halves as they were in the plane, and the third cell number, multiplied by a large odd number, added;
        // two cells that share a key only share a list of candidates, each of which is measured, so a clash costs a look and no more. For a cell of
        // the plane the third number is nought and the key is what it always was.
        private static long CellKey(long x, long y, long z) => unchecked(((x << 32) ^ (y & 0xFFFFFFFFL)) + z * -7046029254386353131L);

        private static void Register(Dictionary<long, List<int>> grid, long x, long y, long z, int index)
        {
            long key = CellKey(x, y, z);

            if (!grid.TryGetValue(key, out List<int> inCell))
            {
                inCell = new List<int>();
                grid[key] = inCell;
            }

            inCell.Add(index);
        }

        /// <summary>
        /// Gives the cells of a grid, named by a number made of the two numbers of a cell, a hash that mixes the two halves: a plain
        /// one sends the cells of a column, whose halves differ by one, to a few buckets.
        /// </summary>
        private sealed class CellComparer : IEqualityComparer<long>
        {
            public bool Equals(long x, long y) => x == y;

            public int GetHashCode(long key)
            {
                unchecked
                {
                    return (int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32);
                }
            }
        }

        private static int Compare(Contact a, Contact b)
        {
            if (Hunt.Precedes(a, b))
            {
                return -1;
            }

            return Hunt.Precedes(b, a) ? 1 : 0;
        }

        private Hunt Begin(IList<Partner> partners, bool onlyNearest, double reach)
        {
            double scale = Scale;
            double extent = Extent;

            for (int i = 0; i < partners.Count; i++)
            {
                partners[i].Position = i;
                scale = Math.Max(scale, partners[i].Scale);
                extent = Math.Max(extent, partners[i].Extent);
            }

            return new Hunt(onlyNearest, reach, scale, extent);
        }
    }
}
