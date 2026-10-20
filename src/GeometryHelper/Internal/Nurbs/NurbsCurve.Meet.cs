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
        internal bool OnOverlap(Partner2 partner, double knot)
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
        internal void Consider(Hunt hunt, Partner2 partner, double knot)
        {
            knot = Clamp(knot);
            PointAt(knot, hunt.Point);
            double px = hunt.Point[0];
            double py = hunt.Point[1];
            double gap = partner.Gap(px, py, out double qx, out double qy, out double other);
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
                verified = true;
            }

            hunt.Offer(new Contact
            {
                Knot = knot,
                Gap = gap,
                Raw = raw,
                Px = px,
                Py = py,
                Qx = qx,
                Qy = qy,
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
        internal void Meet(Partner2 partner, Hunt hunt)
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
                partner.EndAt(e, out double ex, out double ey);
                NearEnd(hunt, partner, ex, ey);
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
            internal Partner2 Partner;
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
        private void NearEnd(Hunt hunt, Partner2 partner, double ex, double ey)
        {
            var query = new[] { ex, ey };

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
        /// <param name="left">From the span before it; the same as the right where the curve is smooth there, and not to be used at the start.</param>
        /// <param name="right">From the span after it; from the span before at the end.</param>
        private void Sides(double knot, out double[][] left, out double[][] right)
        {
            right = Derivatives(knot, 2);
            left = right;

            if (!(knot > Start && knot < End))
            {
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
        /// inside a circle, are not places.
        /// </para>
        /// <para>
        /// A curve that stops, or has a cusp at the contact, or a partner that cannot give its derivatives, is probed a little way either
        /// side instead (<see cref="IsLeast"/>).
        /// </para>
        /// </remarks>
        private bool IsMinimum(Contact contact, Hunt hunt)
        {
            if (!TryGetFoot(contact.From, contact.Px, contact.Py, hunt.Scale, out Foot foot))
            {
                return foot.G <= hunt.Snap || IsLeast(contact, hunt);
            }

            if (!(foot.G > hunt.Snap))
            {
                return true;
            }

            Sides(contact.Knot, out double[][] left, out double[][] right);
            bool hasLeft = contact.Knot > Start;
            bool hasRight = contact.Knot < End;
            double[][] along = hasRight ? right : left;
            double speed = Math.Sqrt(along[1][0] * along[1][0] + along[1][1] * along[1][1]);
            double speedLeft = Math.Sqrt(left[1][0] * left[1][0] + left[1][1] * left[1][1]);
            double speedRight = Math.Sqrt(right[1][0] * right[1][0] + right[1][1] * right[1][1]);

            if (!(speedLeft > StillSpeed) || !(speedRight > StillSpeed))
            {
                return IsLeast(contact, hunt);
            }

            double cosLeft = (foot.Ux * left[1][0] + foot.Uy * left[1][1]) / speedLeft;
            double cosRight = (foot.Ux * right[1][0] + foot.Uy * right[1][1]) / speedRight;
            double turn = Math.Sqrt((left[1][0] - right[1][0]) * (left[1][0] - right[1][0]) + (left[1][1] - right[1][1]) * (left[1][1] - right[1][1]));
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

            // Level, the distance is at its least only if it curves up: a distance that is the same all along, as for two curves side
            // by side, is at its least nowhere.
            if (Math.Abs(slopeAlong) <= foot.Slack)
            {
                return curvatureAlong > flat;
            }

            // On a slope: a minimum within the reach, if the distance curves up; or a meeting within the reach, going the way it
            // falls, which is the crossing of a curve at an angle, where the distance is a V and the roots of the polynomials it is
            // found by are found to a few snaps and not to the last digit.
            return (curvatureAlong > flat && Math.Abs(slopeAlong) <= curvatureAlong * hunt.Reach) || foot.G <= Math.Abs(slopeAlong) * hunt.Reach;
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
            internal double Ux, Uy;

            /// <summary>The first and second derivative of the partner's nearest point, by its own number.</summary>
            internal double R1x, R1y, R2x, R2y;

            /// <summary>The length of the first derivative.</summary>
            internal double PartnerSpeed;

            /// <summary>Whether the nearest point is free to move along the partner, so that it is not held at an end.</summary>
            internal bool Free;

            /// <summary>How near nought a slope that is nought can read, by cosine.</summary>
            internal double Slack;
        }

        /// <summary>
        /// Finds the nearest point of a partner to a point and the direction to the point.
        /// </summary>
        /// <param name="partner">The partner.</param>
        /// <param name="px">The point.</param>
        /// <param name="py">The point.</param>
        /// <param name="scale">The size of the numbers the shapes are given in, which the digits of the point are measured by.</param>
        /// <param name="foot">What was found; its distance is given even when false is returned.</param>
        /// <returns>false when the partner cannot give its derivatives, or the point is on it.</returns>
        /// <remarks>
        /// The direction is the normal of the partner where its nearest point is free to move along it, which is exact, where the
        /// difference of two points a few digits apart at a distance of a few snaps is not.
        /// </remarks>
        internal static bool TryGetFoot(Partner2 partner, double px, double py, double scale, out Foot foot)
        {
            foot = default(Foot);
            foot.G = partner.Gap(px, py, out double qx, out double qy, out double other);

            if (!(foot.G > 0.0) || !partner.TryGetFrame(qx, qy, other, out foot.R1x, out foot.R1y, out foot.R2x, out foot.R2y))
            {
                return false;
            }

            double rx = px - qx;
            double ry = py - qy;
            double noise = 4.0 * 2.220446049250313E-16 * scale / foot.G;
            foot.PartnerSpeed = Math.Sqrt(foot.R1x * foot.R1x + foot.R1y * foot.R1y);
            foot.Free = foot.PartnerSpeed > 0.0 && partner.IsFree(other);
            foot.Ux = rx / foot.G;
            foot.Uy = ry / foot.G;
            foot.Slack = 1E-9 + noise;

            if (foot.Free)
            {
                double side = rx * -foot.R1y + ry * foot.R1x >= 0.0 ? 1.0 : -1.0;
                foot.Ux = side * -foot.R1y / foot.PartnerSpeed;
                foot.Uy = side * foot.R1x / foot.PartnerSpeed;
                foot.Slack = 1E-9;
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
        /// digit, leave no rounding behind. Held at an end of the partner, the point does not move, and it is |P'|² + g a.
        /// </remarks>
        internal static void Variation(Foot foot, double[][] along, out double slope, out double curvature)
        {
            double speed2 = along[1][0] * along[1][0] + along[1][1] * along[1][1];
            double a = foot.Ux * along[2][0] + foot.Uy * along[2][1];
            double b = foot.Ux * foot.R2x + foot.Uy * foot.R2y;
            double partner2 = foot.R1x * foot.R1x + foot.R1y * foot.R1y;
            double stiffness = partner2 - foot.G * b;
            double second = speed2 + foot.G * a;

            if (foot.Free && stiffness > 0.0)
            {
                double cross = along[1][0] * foot.R1y - along[1][1] * foot.R1x;
                second = (cross * cross + foot.G * (a * partner2 - b * speed2) - foot.G * foot.G * a * b) / stiffness;
            }

            slope = foot.Ux * along[1][0] + foot.Uy * along[1][1];
            curvature = (second - slope * slope) / foot.G;
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
                double knot = Clamp(contact.Knot + side * step);

                if (knot == contact.Knot)
                {
                    continue;
                }

                PointAt(knot, hunt.Point);

                if (contact.From.Gap(hunt.Point[0], hunt.Point[1], out _, out _, out _) < contact.Gap - noise)
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
        internal bool TryGetNearest(IList<Partner2> partners, out Contact nearest)
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
        /// within the reach of each other, whatever lies between. Where the curve runs along a partner for a stretch, to
        /// rounding, or along another curve within the reach and level for a quarter of the size of the smaller (<see cref="DropAlong"/>),
        /// it is not crossing it there, and nothing is given along the stretch.
        /// </remarks>
        internal List<Contact> GetMeetings(IList<Partner2> partners, double reach)
        {
            Hunt hunt = Begin(partners, false, reach);

            for (int i = 0; i < partners.Count; i++)
            {
                partners[i].Find(this, hunt);
            }

            var kept = new List<Contact>();
            var pending = new List<Contact>();

            for (int i = 0; i < hunt.Found.Count; i++)
            {
                Contact contact = hunt.Found[i];

                if (contact.Overlap)
                {
                    continue;
                }

                if (contact.Verified)
                {
                    kept.Add(contact);
                }
                else if (IsMinimum(contact, hunt))
                {
                    pending.Add(contact);
                }
            }

            kept.AddRange(DropNearerElsewhere(pending, partners, hunt));
            kept.Sort(Compare);

            // Places no further apart than the reach, one from the next, are one place, the nearer kept: a run of near contacts is the
            // one place it is. Each is checked against those before it within the reach of it, found by the cells of a grid round the one it
            // falls in, so that a thousand places cost a thousand looks and not a million. The cells are as wide as the reach over the
            // square root of two where the digits allow, so that two in one cell are within the reach of each other and are one place
            // already: a cell that has a place within the reach of a new one has no need to be looked at further, which keeps a hundred
            // thousand places that rounding left at one touch from costing ten billion looks.
            double cell = Math.Max(reach / 1.4142135623730951, 1E-9 * hunt.Scale);
            bool mutual = cell * 1.4142135623730951 <= reach;
            int span = (int)Math.Ceiling(reach / cell);
            var grid = new Dictionary<long, List<int>>(new CellComparer());
            var parent = new int[kept.Count];

            for (int i = 0; i < kept.Count; i++)
            {
                parent[i] = i;
                Contact contact = kept[i];
                long cx = (long)Math.Floor(contact.Px / cell);
                long cy = (long)Math.Floor(contact.Py / cell);

                for (long gx = cx - span; gx <= cx + span; gx++)
                {
                    for (long gy = cy - span; gy <= cy + span; gy++)
                    {
                        if (!grid.TryGetValue(CellKey(gx, gy), out List<int> inCell))
                        {
                            continue;
                        }

                        foreach (int j in inCell)
                        {
                            double dx = kept[j].Px - contact.Px;
                            double dy = kept[j].Py - contact.Py;

                            if (dx * dx + dy * dy <= reach * reach)
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

                Register(grid, cx, cy, i);
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

            var meetings = new List<Contact>();

            for (int i = 0; i < kept.Count; i++)
            {
                if (best[i] >= 0)
                {
                    meetings.Add(kept[best[i]]);
                }
            }

            meetings.Sort(Compare);

            return DropAlong(MergeBasins(meetings, partners, hunt), hunt);
        }

        /// <summary>
        /// Takes out the places inside a stretch where the curve lies along another curve to within the reach (<see cref="SplinePartner"/>
        /// finds them): the places a curve as good as the same as another has are where the rounding of the numbers has the two touch or
        /// cross, which is no place. A crossing found there by the offset of the one from the other changing sign by more than a tenth
        /// of the reach each way stays: the curves cross at a shallow angle.
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
                if (place.Crossing || !hunt.IsAlong(place.Part, place.Knot, place.Knot))
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
        /// <param name="radius">How far from each point a partner must be to be of interest; no more than nought leaves the point out.</param>
        /// <param name="skip">The position of the one partner to leave out for each point, or -1.</param>
        /// <param name="least">The least distance found for each point, or infinity when no partner is within its radius.</param>
        /// <remarks>
        /// The partners are the edges of a polygon, a chain or a polyline, which may be a million, and the points the few places a curve
        /// comes near them: the work is done from the partners' side, each partner looking up the cells of a grid of the points that its
        /// box reaches, so that it costs the number of the partners and of the pairs that are near, not their product.
        /// </remarks>
        private static void MinGaps(IList<Partner2> partners, double[] qx, double[] qy, double[] radius, int[] skip, double[] least)
        {
            int n = qx.Length;
            var probe = new double[2];
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
                        Register(grid, (long)Math.Floor(qx[k] / cell), (long)Math.Floor(qy[k] / cell), k);
                    }
                }
            }

            for (int j = 0; j < partners.Count; j++)
            {
                Partner2 partner = partners[j];

                if (grid == null)
                {
                    for (int k = 0; k < n; k++)
                    {
                        Measure(partner, k, qx, qy, radius, skip, least, probe);
                    }

                    continue;
                }

                partner.GetBox(out double minX, out double minY, out double maxX, out double maxY);
                double cx0 = Math.Floor((minX - widest) / cell);
                double cx1 = Math.Floor((maxX + widest) / cell);
                double cy0 = Math.Floor((minY - widest) / cell);
                double cy1 = Math.Floor((maxY + widest) / cell);

                if (double.IsInfinity(cx0) || double.IsInfinity(cx1) || double.IsInfinity(cy0) || double.IsInfinity(cy1) || (cx1 - cx0 + 1.0) * (cy1 - cy0 + 1.0) > n)
                {
                    // A box that reaches more cells than there are points: look at the points.
                    for (int k = 0; k < n; k++)
                    {
                        if (qx[k] >= minX - radius[k] && qx[k] <= maxX + radius[k] && qy[k] >= minY - radius[k] && qy[k] <= maxY + radius[k])
                        {
                            Measure(partner, k, qx, qy, radius, skip, least, probe);
                        }
                    }

                    continue;
                }

                for (long gx = (long)cx0; gx <= (long)cx1; gx++)
                {
                    for (long gy = (long)cy0; gy <= (long)cy1; gy++)
                    {
                        if (grid.TryGetValue(CellKey(gx, gy), out List<int> inCell))
                        {
                            foreach (int k in inCell)
                            {
                                Measure(partner, k, qx, qy, radius, skip, least, probe);
                            }
                        }
                    }
                }
            }
        }

        private static void Measure(Partner2 partner, int k, double[] qx, double[] qy, double[] radius, int[] skip, double[] least, double[] probe)
        {
            if (!(radius[k] > 0.0) || partner.Position == skip[k])
            {
                return;
            }

            probe[0] = qx[k];
            probe[1] = qy[k];

            if (partner.Bound(probe, probe, 0) <= Math.Min(radius[k], least[k]))
            {
                double gap = partner.Gap(qx[k], qy[k], out _, out _, out _);

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
        private static List<Contact> DropNearerElsewhere(List<Contact> pending, IList<Partner2> partners, Hunt hunt)
        {
            if (partners.Count < 2 || pending.Count == 0)
            {
                return pending;
            }

            double slack = 8E-13 * hunt.Scale;
            int n = pending.Count;
            var qx = new double[n];
            var qy = new double[n];
            var radius = new double[n];
            var skip = new int[n];
            var least = new double[n];

            for (int i = 0; i < n; i++)
            {
                qx[i] = pending[i].Px;
                qy[i] = pending[i].Py;
                radius[i] = pending[i].Gap - slack;
                skip[i] = pending[i].Part;
            }

            MinGaps(partners, qx, qy, radius, skip, least);
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
        /// Joins the near touches that lie in one basin of the distance: two next to each other along the curve, within ten times the
        /// reach of each other, with no point between them where the curve is farther from the partners than at either, by more than
        /// four snaps. A curve that touches a partner is as near as rounding can tell all along a stretch, and each place the rounding
        /// finds in it is the one touch. Two meetings are not joined, whatever lies between them: crossings are places, and are one
        /// only when they are within the reach of each other, as the exact curves have it.
        /// </summary>
        private List<Contact> MergeBasins(List<Contact> places, IList<Partner2> partners, Hunt hunt)
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

                if (dx * dx + dy * dy <= near * near && !(places[i].Verified && places[i + 1].Verified))
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
                    radius[at] = Math.Max(last.Raw, next.Raw) + band;
                    skip[at] = -1;
                }
            }

            MinGaps(partners, qx, qy, radius, skip, least);
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

                for (int i = from + 1; i <= to; i++)
                {
                    if (places[i].Raw < places[best].Raw)
                    {
                        best = i;
                    }
                }

                merged.Add(places[best]);
                from = to + 1;
            }

            return merged;
        }

        private static long CellKey(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);

        private static void Register(Dictionary<long, List<int>> grid, long x, long y, int index)
        {
            long key = CellKey(x, y);

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

        private Hunt Begin(IList<Partner2> partners, bool onlyNearest, double reach)
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
