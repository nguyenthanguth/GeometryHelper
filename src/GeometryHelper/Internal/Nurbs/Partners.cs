using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// A place where a curve meets or comes near a partner: the point of the curve, the point of the partner nearest it,
    /// and how far apart they are. Nought, exactly, where the two truly meet.
    /// </summary>
    internal struct Contact
    {
        /// <summary>The knot value of the curve at the contact.</summary>
        internal double Knot;

        /// <summary>The distance from the point of the curve to the point of the partner; nought where they meet.</summary>
        internal double Gap;

        /// <summary>The same distance as measured, before one within rounding of nought was read as nought: of two places that both meet, the one that is nearer to the partner by this is the better place.</summary>
        internal double Raw;

        /// <summary>The point of the curve; Z is nought for a curve in the plane.</summary>
        internal double Px, Py, Pz;

        /// <summary>The point of the partner nearest it: the same point as the curve's where they meet.</summary>
        internal double Qx, Qy, Qz;

        /// <summary>Where on the partner: how far along a straight one, the angle of a round one, the knot value of another curve.</summary>
        internal double Other;

        /// <summary>Which of the partners asked about it was found against.</summary>
        internal int Part;

        /// <summary>Whether the curve runs along the partner here, so that it is a meeting for the distance but no crossing.</summary>
        internal bool Overlap;

        /// <summary>Whether it is known to be a local least of the distance, so that it need not be tried.</summary>
        internal bool Verified;

        /// <summary>Whether it is a crossing found by the offset of the curve from the partner changing sign, which is a place even where the curve lies along the partner within the reach.</summary>
        internal bool Crossing;

        /// <summary>
        /// Whether it is a least of the distance of a curve in space from another that it does not share a plane with, inside a stretch
        /// where the two lie along each other, where the two would cross if they were brought into one plane: the near crossing of
        /// two curves in space, a place as a crossing is.
        /// </summary>
        internal bool Dip;

        /// <summary>
        /// Whether it is known to be the place a near touch is, not being a meeting: worked out in closed form, as where two arcs of
        /// circles within the reach of each other come nearest, so that the distance there need not be tried.
        /// </summary>
        internal bool Least;

        /// <summary>The partner it was found against, which can measure the curve's distance to it elsewhere.</summary>
        internal Partner From;
    }

    /// <summary>
    /// One question about a curve and its partners being worked out: what is wanted (the nearest approach, or every
    /// place within a reach), the rounding bands, and what has been found so far.
    /// </summary>
    internal sealed class Hunt
    {
        /// <summary>How far apart, as a share of the distance and of the size of the shapes, two distances may be and still count as the same.</summary>
        internal const double TieShare = 1E-15;

        /// <summary>How near nought, as a share of the size of the shapes, a distance has to be to read as nought: a meeting found to rounding.</summary>
        internal const double SnapShare = 1E-13;

        /// <summary>
        /// When every place is wanted: the stretches of the curve found to lie within the reach of a partner that is another curve, for
        /// a length (<see cref="SplinePartner"/>): no place is given in them.
        /// </summary>
        internal readonly List<AlongRun> Runs = new List<AlongRun>();

        /// <summary>One stretch of the curve that lies along a partner to within the reach.</summary>
        internal struct AlongRun
        {
            /// <summary>The partner's position, as <see cref="Contact.Part"/>.</summary>
            internal int Part;

            /// <summary>The knot values the stretch runs between.</summary>
            internal double From, To;

            /// <summary>
            /// The knot values of the curve the stretch was looked at, in order, and of the partner's nearest point to each: the branch
            /// of the partner the stretch runs along.
            /// </summary>
            internal double[] Knots, Others;

            /// <summary>The span of the partner's knot values where it is closed, which its seam goes round by; nought where it is open.</summary>
            internal double Period;
        }

        /// <summary>
        /// Says whether a place on the curve, inside a stretch along a partner, is on another branch of the partner than the one the
        /// stretch runs along: its point of the partner lies further along the partner from where the two looks either side of it have
        /// their nearest points than those two are from each other. A place no look stands either side of is on the branch.
        /// </summary>
        /// <remarks>
        /// On a closed partner the knot values are read round its seam, a whole period being nothing: the circle spline against the
        /// polygon inscribed in it with a corner on the seam at (5, 0) gave that corner as a place, its partner's knot read at the start
        /// and the looks either side at the end. The looks are a sixteenth of a stretch along apart, so a loop of the partner shorter
        /// than about three of them, which comes back within that of where it left, is not told from the branch the stretch runs along.
        /// </remarks>
        /// <param name="part">The partner's position.</param>
        /// <param name="knot">The knot value of the place on the curve.</param>
        /// <param name="other">The partner's knot value at the place.</param>
        /// <param name="near">How far either side of the place, in the curve's knot values, a look may have its nearest point on the other
        /// branch rather than the one the stretch runs along, and is passed over: the polyline that comes back square across a line at
        /// 50, looked at at 50 itself, has its nearest point there on the square edge, and that look said the edge was the stretch's branch.</param>
        internal bool OnAnotherBranch(int part, double knot, double other, double near)
        {
            for (int i = 0; i < Runs.Count; i++)
            {
                AlongRun run = Runs[i];

                if (run.Part != part || !(knot >= run.From && knot <= run.To) || run.Knots == null)
                {
                    continue;
                }

                int before = Array.BinarySearch(run.Knots, knot - near);
                before = before >= 0 ? before - 1 : ~before - 1;
                int after = Array.BinarySearch(run.Knots, knot + near);
                after = after >= 0 ? after + 1 : ~after;

                if (before < 0 || after >= run.Knots.Length)
                {
                    return false;
                }

                double first = run.Others[before];
                double second = run.Others[after];
                double period = run.Period;

                // Round the seam of a closed partner, the second look and the place are read in the period nearest the first look.
                if (period > 0.0)
                {
                    second -= period * Math.Round((second - first) / period);
                    other -= period * Math.Round((other - 0.5 * (first + second)) / period);
                }

                double low = Math.Min(first, second);
                double high = Math.Max(first, second);
                double room = high - low;

                return other < low - room || other > high + room;
            }

            return false;
        }

        /// <summary>
        /// When every place is wanted: the stretches inside those along a partner where the curve lies on it exactly, to the digits,
        /// between two lobes of the offset that go further than a hundredth of the reach (<see cref="SplinePartner"/>). Each is one place
        /// at most, its end next to the earlier lobe where the lobes lie to either side, and no other place is given in it.
        /// </summary>
        internal readonly List<Overlap> Overlaps = new List<Overlap>();

        /// <summary>One stretch of the curve that lies on a partner to the digits, between two lobes of the offset.</summary>
        internal struct Overlap
        {
            /// <summary>The partner's position, as <see cref="Contact.Part"/>.</summary>
            internal int Part;

            /// <summary>The knot value of the end next to the earlier lobe, and of the other end.</summary>
            internal double From, To;

            /// <summary>Whether the lobes lie to either side, so that the curves cross there, at the end next to the earlier lobe.</summary>
            internal bool Crossed;
        }

        /// <summary>
        /// Says whether the knot values of a stretch of the curve lie inside a stretch that runs along a partner.
        /// </summary>
        internal bool IsAlong(int part, double from, double to)
        {
            for (int i = 0; i < Runs.Count; i++)
            {
                if (Runs[i].Part == part && from >= Runs[i].From && to <= Runs[i].To)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether only the nearest approach is wanted.</summary>
        internal readonly bool OnlyNearest;

        /// <summary>When every place is wanted: how far from the partner a place may be.</summary>
        internal readonly double Reach;

        /// <summary>The distance below which a gap reads as nought.</summary>
        internal readonly double Snap;

        /// <summary>The size of the numbers the shapes are given in, the largest coordinate, which rounding goes by.</summary>
        internal readonly double Scale;

        /// <summary>How large the shapes are, from side to side, which what is near enough to matter goes by: a shape a millimetre across is as small at a coordinate of 30 000 000 as at the origin.</summary>
        internal readonly double Extent;

        /// <summary>Room for the roots of a polynomial, reused.</summary>
        internal readonly List<double> Roots = new List<double>();

        /// <summary>Room for a point, reused; the third number stays nought for a curve in the plane.</summary>
        internal readonly double[] Point = new double[3];

        /// <summary>When every place is wanted: every one found within the reach, before they are judged.</summary>
        internal readonly List<Contact> Found = new List<Contact>();

        private readonly List<Contact> _near = new List<Contact>();
        private double _least = double.PositiveInfinity;
        private double _zero = double.PositiveInfinity;

        /// <summary>
        /// Starts a question.
        /// </summary>
        /// <param name="onlyNearest">Whether only the nearest approach is wanted.</param>
        /// <param name="reach">When every place is wanted, how far from the partner a place may be.</param>
        /// <param name="scale">The size of the numbers the shapes are given in.</param>
        /// <param name="extent">How large the shapes are.</param>
        internal Hunt(bool onlyNearest, double reach, double scale, double extent)
        {
            OnlyNearest = onlyNearest;
            Reach = reach;
            Scale = scale;
            Extent = extent;
            Snap = SnapShare * scale;
        }

        /// <summary>
        /// Gets how far apart two distances of about this size may be and count as one.
        /// </summary>
        internal double Band(double gap) => TieShare * (gap + Scale);

        /// <summary>
        /// Gets how far a part of the curve may be from the partner and still be looked at: the reach, or, for the nearest
        /// approach, the least gap found so far and its band.
        /// </summary>
        internal double Limit
        {
            get
            {
                if (!OnlyNearest)
                {
                    return Reach;
                }

                return double.IsPositiveInfinity(_least) ? double.PositiveInfinity : _least + Band(_least);
            }
        }

        /// <summary>
        /// Gets how far apart two parts of two curves may be, by their boxes, and still be cut again when the curves are
        /// searched against each other: the reach, or for the nearest approach the least gap found less a hair, since a pair that
        /// could be nearer by no more than that cannot change the answer by more than that, which Newton's method then takes
        /// off the pair that was found. Once they are known to meet, only a place that could come first is cut.
        /// </summary>
        /// <remarks>
        /// The hair is a billionth of the size of the shapes, and never more than a thousandth of the least gap itself: a hair of
        /// a hundred-millionth of the size would be 0.01 at a million millimetres, and a least gap of 0.005 would have everything, the
        /// pair that crosses included, cut away, and a near miss of 0.0009 read as 0.01. The cutoff is never below the snap, so that a
        /// pair that meets is always cut.
        /// </remarks>
        internal double Cutoff
        {
            get
            {
                if (!OnlyNearest)
                {
                    return Reach + Snap;
                }

                if (double.IsPositiveInfinity(_least))
                {
                    return double.PositiveInfinity;
                }

                if (_least <= Snap)
                {
                    return Snap;
                }

                return Math.Max(Snap, _least - Math.Min(1E-9 * Extent, 1E-3 * _least));
            }
        }

        /// <summary>
        /// Gets the least gap found so far.
        /// </summary>
        internal double Least => _least;

        /// <summary>
        /// Gets the lowest knot value of the curve at which it has been found to meet the partner.
        /// </summary>
        internal double ZeroKnot => _zero;

        /// <summary>
        /// Notes a knot value at which the curve meets the partner.
        /// </summary>
        internal void Mark(double knot)
        {
            if (knot < _zero)
            {
                _zero = knot;
            }
        }

        /// <summary>
        /// Takes a contact: kept when every place is wanted and it is within the reach, and for the nearest approach kept
        /// when no more than a band above the least.
        /// </summary>
        internal void Offer(Contact contact)
        {
            if (!OnlyNearest)
            {
                if (contact.Gap <= Reach)
                {
                    Found.Add(contact);
                }

                return;
            }

            if (contact.Gap < _least)
            {
                _least = contact.Gap;
                double ceiling = _least + Band(_least);

                for (int i = _near.Count - 1; i >= 0; i--)
                {
                    if (_near[i].Gap > ceiling)
                    {
                        _near.RemoveAt(i);
                    }
                }
            }

            if (contact.Gap <= _least + Band(_least))
            {
                _near.Add(contact);
            }
        }

        /// <summary>
        /// Gets the nearest approach: of those within a band of the least gap, the one at the lowest knot value, then of the
        /// lowest partner and the lowest place on it, so that it does not depend on the order the pieces were looked at.
        /// </summary>
        /// <returns>false when nothing was offered.</returns>
        internal bool TryGetNearest(out Contact nearest)
        {
            nearest = default(Contact);
            bool found = false;
            double ceiling = _least + Band(_least);

            for (int i = 0; i < _near.Count; i++)
            {
                Contact c = _near[i];

                if (!(c.Gap <= ceiling))
                {
                    continue;
                }

                if (!found || Precedes(c, nearest))
                {
                    nearest = c;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Says whether a contact comes before another: the lower knot value, then the lower partner, then the lower place
        /// on the partner, then the lower gap.
        /// </summary>
        internal static bool Precedes(Contact a, Contact b)
        {
            if (a.Knot != b.Knot)
            {
                return a.Knot < b.Knot;
            }

            if (a.Part != b.Part)
            {
                return a.Part < b.Part;
            }

            if (a.Other != b.Other)
            {
                return a.Other < b.Other;
            }

            return a.Gap < b.Gap;
        }
    }

    /// <summary>
    /// What a curve is asked about, in numbers: a straight stretch, a plane or a ball, an arc or a circle, an ellipse or another
    /// curve. It measures the distance of a point to itself, bounds it over a box, and says at what parameters of a Bézier piece
    /// the distance of the curve to it can be least, which together answer the nearest approach and every place the two meet.
    /// The numbers of a point are three, in the plane as in space; the third of a point of the plane is nought.
    /// </summary>
    internal abstract class Partner
    {
        /// <summary>Where this partner stands in the list of partners one question asks about; set by the caller.</summary>
        internal int Position;

        /// <summary>
        /// Gets the size of the partner's numbers, which rounding goes by.
        /// </summary>
        internal abstract double Scale { get; }

        /// <summary>
        /// Gets how large the partner is, from side to side.
        /// </summary>
        internal abstract double Extent { get; }

        /// <summary>
        /// Gets the distance from a point to the partner, with the nearest point of it and where it lies: infinite for a partner
        /// that has no nearest point to it, as a part of a flat shape has none for a point that does not stand over it.
        /// </summary>
        internal abstract double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other);

        /// <summary>
        /// Gets a bound the distance of any point of a box to the partner is not below.
        /// </summary>
        internal abstract double Bound(double[] min, double[] max, int at);

        /// <summary>Gets the number of true ends the partner has: a segment's two, a ray's one, an arc's two, none for a line without ends or a closed curve.</summary>
        internal virtual int EndCount => 0;

        /// <summary>Gets a true end of the partner.</summary>
        internal virtual void EndAt(int index, out double x, out double y, out double z)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
        }

        /// <summary>
        /// Offers the places of one Bézier piece of the curve where its distance to the partner can be least: where it
        /// meets the partner, and where it stops coming nearer.
        /// </summary>
        internal abstract void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt);

        /// <summary>
        /// Gets the first and second derivative of the partner's point at one of its points, by the number its places are given in
        /// (how far along a straight one, the angle of a round one, the knot value of another curve): what the curvature of the
        /// distance from a curve to the partner is made of.
        /// </summary>
        /// <param name="qx">The point of the partner.</param>
        /// <param name="qy">The point of the partner.</param>
        /// <param name="qz">The point of the partner.</param>
        /// <param name="other">Where on the partner it is, as <see cref="Gap"/> gave it.</param>
        /// <param name="r1x">The first derivative.</param>
        /// <param name="r1y">The first derivative.</param>
        /// <param name="r1z">The first derivative.</param>
        /// <param name="r2x">The second derivative.</param>
        /// <param name="r2y">The second derivative.</param>
        /// <param name="r2z">The second derivative.</param>
        /// <returns>false when the partner cannot say, and the distance is probed a little way either side instead.</returns>
        internal virtual bool TryGetFrame(double qx, double qy, double qz, double other, out double r1x, out double r1y, out double r1z, out double r2x, out double r2y, out double r2z)
        {
            r1x = 0.0;
            r1y = 0.0;
            r1z = 0.0;
            r2x = 0.0;
            r2y = 0.0;
            r2z = 0.0;
            return false;
        }

        /// <summary>
        /// Says, for a partner that is a surface, a plane or a ball, which way the distance to it runs from a point, and how it bends.
        /// </summary>
        /// <param name="px">The point.</param>
        /// <param name="py">The point.</param>
        /// <param name="pz">The point.</param>
        /// <param name="ux">The unit direction from the nearest point of the surface to the point.</param>
        /// <param name="uy">The unit direction from the nearest point of the surface to the point.</param>
        /// <param name="uz">The unit direction from the nearest point of the surface to the point.</param>
        /// <param name="bend">
        /// How the distance bends as the point moves square to that direction: 1 over the distance from the centre for a point outside a
        /// ball, minus that for one inside it, nought for a plane. The second derivative of the distance along a curve is then bend times
        /// the speed squared less the slope squared, plus u . P''.
        /// </param>
        /// <param name="noise">How far from the true direction the one given can be, as a cosine: nought for a plane, a few units of rounding over the distance from the centre for a ball.</param>
        /// <returns>false for a partner that is not a surface, which gives its frame instead (<see cref="TryGetFrame"/>).</returns>
        internal virtual bool TryGetSurface(double px, double py, double pz, out double ux, out double uy, out double uz, out double bend, out double noise)
        {
            ux = 0.0;
            uy = 0.0;
            uz = 0.0;
            bend = 0.0;
            noise = 0.0;
            return false;
        }

        /// <summary>
        /// Says whether a nearest point of the partner is free to move along it, and not held at one of its ends: where it is held the
        /// line to it need not be square to the partner.
        /// </summary>
        /// <param name="other">Where on the partner it is, as <see cref="Gap"/> gave it.</param>
        internal virtual bool IsFree(double other) => true;

        /// <summary>
        /// Gets a box the partner lies in: no tighter than the partner, as far out as nothing is known.
        /// </summary>
        internal virtual void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            minX = double.NegativeInfinity;
            minY = double.NegativeInfinity;
            minZ = double.NegativeInfinity;
            maxX = double.PositiveInfinity;
            maxY = double.PositiveInfinity;
            maxZ = double.PositiveInfinity;
        }

        /// <summary>
        /// Says whether a piece of the curve lies along the partner, all of it, to rounding.
        /// </summary>
        internal virtual bool Coincident(NurbsCurve curve, int piece) => false;

        /// <summary>
        /// Says whether the curve, at a knot value, lies in a plane with the partner, so that its distance there is the size of a signed
        /// offset, as it is in the plane, and a least of it has a curvature: a line in space says so of a piece in a plane through it.
        /// </summary>
        internal virtual bool InPlaneAt(NurbsCurve curve, double knot) => false;

        /// <summary>
        /// Looks for the places the curve meets this partner or comes nearest, offering each to the hunt.
        /// </summary>
        internal virtual void Find(NurbsCurve curve, Hunt hunt) => curve.Meet(this, hunt);
    }

    /// <summary>
    /// Makes a partner a bound for the tree of the curve's boxes.
    /// </summary>
    internal readonly struct PartnerBound : IBound
    {
        private readonly Partner _partner;

        internal PartnerBound(Partner partner)
        {
            _partner = partner;
        }

        public double Bound(double[] min, double[] max, int at) => _partner.Bound(min, max, at);
    }

    /// <summary>
    /// A straight stretch of a line: a segment, a ray, or a line without ends cut to the stretch that can matter. It
    /// is the line through an origin along a unit direction, taken from a first distance along to a last.
    /// </summary>
    internal sealed class LinePartner : Partner
    {
        private readonly double _scale;
        private Dictionary<int, bool> _coincident;

        // What has been found about which pieces of the curve lie along this partner, made when first asked.
        private Dictionary<int, bool> Memo => _coincident ?? (_coincident = new Dictionary<int, bool>());

        internal readonly double Ox, Oy, Dx, Dy, T0, T1;
        internal readonly bool EndA, EndB;

        /// <summary>
        /// Makes the stretch.
        /// </summary>
        /// <param name="ox">The origin the distances along are measured from.</param>
        /// <param name="oy">The origin the distances along are measured from.</param>
        /// <param name="dx">The unit direction.</param>
        /// <param name="dy">The unit direction.</param>
        /// <param name="t0">How far along it starts.</param>
        /// <param name="t1">How far along it stops, not below the start.</param>
        /// <param name="endA">Whether the start is a true end of the shape, and not only where a line without ends was cut.</param>
        /// <param name="endB">Whether the stop is a true end of the shape.</param>
        internal LinePartner(double ox, double oy, double dx, double dy, double t0, double t1, bool endA, bool endB)
        {
            Ox = ox;
            Oy = oy;
            Dx = dx;
            Dy = dy;
            T0 = t0;
            T1 = t1;
            EndA = endA;
            EndB = endB;
            _scale = Math.Max(Math.Max(Math.Abs(ox), Math.Abs(oy)), Math.Max(Math.Abs(ox + t0 * dx) + Math.Abs(oy + t0 * dy), Math.Abs(ox + t1 * dx) + Math.Abs(oy + t1 * dy)));
        }

        private double Nx => -Dy;

        private double Ny => Dx;

        internal override double Scale => _scale;

        internal override double Extent => T1 - T0;

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double t = (px - Ox) * Dx + (py - Oy) * Dy;
            double c = t < T0 ? T0 : t > T1 ? T1 : t;
            qx = Ox + c * Dx;
            qy = Oy + c * Dy;
            qz = 0.0;
            other = c;
            double ex = px - qx;
            double ey = py - qy;

            return Math.Sqrt(ex * ex + ey * ey);
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            double lx = min[at];
            double hx = max[at];

            if (lx > hx)
            {
                return double.PositiveInfinity;
            }

            double ly = min[at + 1];
            double hy = max[at + 1];
            double cx = 0.5 * (lx + hx);
            double cy = 0.5 * (ly + hy);
            double ex = 0.5 * (hx - lx);
            double ey = 0.5 * (hy - ly);
            double rx = cx - Ox;
            double ry = cy - Oy;
            double slack = 1E-13 * (Math.Abs(cx) + Math.Abs(cy) + ex + ey + _scale);

            // The box is no nearer the line than its centre less how far the box reaches across it, and no nearer the stretch
            // than that and how far it lies past either end along it.
            double across = Math.Abs(Nx * rx + Ny * ry) - (Math.Abs(Nx) * ex + Math.Abs(Ny) * ey) - slack;
            double along = Dx * rx + Dy * ry;
            double reach = Math.Abs(Dx) * ex + Math.Abs(Dy) * ey;
            double beyond = Math.Max(T0 - (along + reach), (along - reach) - T1) - slack;

            across = across > 0.0 ? across : 0.0;
            beyond = beyond > 0.0 ? beyond : 0.0;

            return Math.Sqrt(across * across + beyond * beyond);
        }

        internal override bool TryGetFrame(double qx, double qy, double qz, double other, out double r1x, out double r1y, out double r1z, out double r2x, out double r2y, out double r2z)
        {
            r1x = Dx;
            r1y = Dy;
            r1z = 0.0;
            r2x = 0.0;
            r2y = 0.0;
            r2z = 0.0;
            return true;
        }

        internal override bool IsFree(double other) => other > T0 && other < T1;

        internal override void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            double x0 = Ox + T0 * Dx;
            double y0 = Oy + T0 * Dy;
            double x1 = Ox + T1 * Dx;
            double y1 = Oy + T1 * Dy;
            minX = Math.Min(x0, x1);
            maxX = Math.Max(x0, x1);
            minY = Math.Min(y0, y1);
            maxY = Math.Max(y0, y1);
            minZ = 0.0;
            maxZ = 0.0;
        }

        internal override int EndCount => (EndA ? 1 : 0) + (EndB ? 1 : 0);

        internal override void EndAt(int index, out double x, out double y, out double z)
        {
            double t = EndA && index == 0 ? T0 : T1;
            x = Ox + t * Dx;
            y = Oy + t * Dy;
            z = 0.0;
        }

        /// <summary>
        /// Gets the numerator of the signed offset from the line along a piece, as a polynomial of the piece.
        /// </summary>
        private double[] Offset(BezierPieces pieces, int piece, out double[] weights, out double scale)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Ox, Oy }, out double[][] numerators, out weights);
            int p = pieces.Degree;
            var offset = new double[p + 1];

            for (int j = 0; j <= p; j++)
            {
                offset[j] = Nx * numerators[0][j] + Ny * numerators[1][j];
            }

            scale = Bernstein.MaxAbs(numerators[0]) + Bernstein.MaxAbs(numerators[1]);

            return offset;
        }

        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            double from = pieces.From[piece];
            double span = pieces.To[piece] - from;

            // The start of the piece; the end of it is the start of the next, or the end of the curve, offered by the caller.
            curve.Consider(hunt, this, from);

            double[] offset = Offset(pieces, piece, out double[] weights, out double scale);
            double noise = NurbsCurve.NoiseShare * scale;

            if (Bernstein.MaxAbs(offset) <= noise)
            {
                Memo[piece] = true;
                return;
            }

            Memo[piece] = false;

            // Where the piece crosses the line.
            hunt.Roots.Clear();
            Bernstein.FindRoots(offset, noise, hunt.Roots);
            List<double> roots = hunt.Roots;

            // Where the offset stops falling or rising, which is where the piece comes nearest the line without crossing it.
            double[] slope = NurbsCurve.CoordinateSlope(offset, weights, out double slopeScale);
            Bernstein.FindRoots(slope, NurbsCurve.NoiseShare * slopeScale, roots);

            for (int i = 0; i < roots.Count; i++)
            {
                double s = roots[i];
                curve.Consider(hunt, this, s <= 0.0 ? from : s >= 1.0 ? pieces.To[piece] : from + s * span);
            }

            // The offered roots are done with; room for the next piece.
            roots.Clear();
        }

        internal override bool Coincident(NurbsCurve curve, int piece)
        {
            if (Memo.TryGetValue(piece, out bool known))
            {
                return known;
            }

            BezierPieces pieces = curve.Pieces;
            double[] offset = Offset(pieces, piece, out _, out double scale);
            bool coincident = Bernstein.MaxAbs(offset) <= NurbsCurve.NoiseShare * scale;
            Memo[piece] = coincident;

            return coincident;
        }
    }

    /// <summary>
    /// A partner that is where a polynomial form of the coordinates of a curve is nought, and so is met by a Bézier piece
    /// where the polynomial the piece makes of the form has its roots: a circle or an ellipse in the plane, a ball in space, and, as
    /// the square of the offset from it, a line in space. The distance of the curve to it stops falling where the form stops
    /// rising or falling, which is where the derivative of the polynomial has its roots.
    /// </summary>
    internal abstract class FormPartner : Partner
    {
        private Dictionary<int, bool> _coincident;

        // What has been found about which pieces of the curve lie along this partner, made when first asked.
        private Dictionary<int, bool> Memo => _coincident ?? (_coincident = new Dictionary<int, bool>());

        /// <summary>
        /// Gets the numerator of the form along a piece as a polynomial of twice the degree of the piece.
        /// </summary>
        /// <param name="pieces">The pieces of the curve.</param>
        /// <param name="piece">Which piece.</param>
        /// <param name="weights">The weights of the piece, or null when it is not rational.</param>
        /// <param name="scale">How large the numbers the form was worked out from are, which noise is a share of.</param>
        protected abstract double[] Form(BezierPieces pieces, int piece, out double[] weights, out double scale);

        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            double from = pieces.From[piece];
            double span = pieces.To[piece] - from;

            curve.Consider(hunt, this, from);

            double[] form = Form(pieces, piece, out double[] weights, out double scale);
            double noise = NurbsCurve.NoiseShare * scale;

            if (Bernstein.MaxAbs(form) <= noise)
            {
                Memo[piece] = true;
                return;
            }

            Memo[piece] = false;

            // Where the piece meets the curve.
            hunt.Roots.Clear();
            Bernstein.FindRoots(form, noise, hunt.Roots);

            double[] slope = FormSlope(form, weights, out double slopeScale);
            Bernstein.FindRoots(slope, NurbsCurve.NoiseShare * slopeScale, hunt.Roots);
            List<double> roots = hunt.Roots;

            for (int i = 0; i < roots.Count; i++)
            {
                double s = roots[i];
                curve.Consider(hunt, this, Refine(curve, hunt, s <= 0.0 ? from : s >= 1.0 ? pieces.To[piece] : from + s * span));
            }

            roots.Clear();
        }

        /// <summary>
        /// Gets where a form stops rising or falling along a piece: its derivative over the weight squared, a polynomial of degree three
        /// times that of the piece less one, since the form is a polynomial over the weight squared.
        /// </summary>
        /// <param name="form">The numerator of the form along the piece.</param>
        /// <param name="weights">The weights of the piece, or null when it is not rational.</param>
        /// <param name="slopeScale">How large the numbers the slope was worked out from are.</param>
        protected static double[] FormSlope(double[] form, double[] weights, out double slopeScale)
        {
            double[] slope = Bernstein.Derivative(form);
            slopeScale = Bernstein.MaxAbs(slope);

            if (weights != null)
            {
                double[] weightSlope = Bernstein.Derivative(weights);
                double[] weighted = Bernstein.Multiply(slope, weights);
                double[] shifted = Bernstein.Multiply(form, weightSlope);
                double larger = Math.Max(Bernstein.MaxAbs(weighted), 2.0 * Bernstein.MaxAbs(shifted));

                for (int i = 0; i < weighted.Length; i++)
                {
                    weighted[i] -= 2.0 * shifted[i];
                }

                slope = weighted;
                slopeScale = larger;
            }

            return slope;
        }

        /// <summary>
        /// Brings a place the roots of the form found to the place the curve is nearest the partner, where the roots are not good to
        /// the last digit: none are, by default.
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="hunt">The hunt.</param>
        /// <param name="knot">The knot value of the place found.</param>
        protected virtual double Refine(NurbsCurve curve, Hunt hunt, double knot) => knot;

        internal override bool Coincident(NurbsCurve curve, int piece)
        {
            if (Memo.TryGetValue(piece, out bool known))
            {
                return known;
            }

            double[] form = Form(curve.Pieces, piece, out _, out double scale);
            bool coincident = Bernstein.MaxAbs(form) <= NurbsCurve.NoiseShare * scale;
            Memo[piece] = coincident;

            return coincident;
        }
    }

    /// <summary>
    /// A curve that is the set of points of the plane where a quadratic form is nought: a circle or an ellipse. A Bézier piece
    /// put into the form gives a polynomial of twice its degree, whose roots are where the piece meets the curve.
    /// </summary>
    /// <remarks>
    /// The form is m11 x² + 2 m12 x y + m22 y² - k, taken from the centre, which is nought on the curve. For a circle of
    /// radius r it is x² + y² - r², and for an ellipse with unit axes u and v and radii a and b it is (u.p)² / a² +
    /// (v.p)² / b² - 1.
    /// </remarks>
    internal abstract class QuadricPartner : FormPartner
    {
        internal readonly double Cx, Cy, M11, M12, M22, K;

        internal QuadricPartner(double cx, double cy, double m11, double m12, double m22, double k)
        {
            Cx = cx;
            Cy = cy;
            M11 = m11;
            M12 = m12;
            M22 = m22;
            K = k;
        }

        /// <summary>
        /// Gets the numerator of the form along a piece as a polynomial of twice the degree of the piece.
        /// </summary>
        protected override double[] Form(BezierPieces pieces, int piece, out double[] weights, out double scale)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Cx, Cy }, out double[][] numerators, out weights);
            double[] x = numerators[0];
            double[] y = numerators[1];
            double[] w = weights ?? Ones(pieces.Degree + 1);

            double[] xx = Bernstein.Multiply(x, x);
            double[] xy = Bernstein.Multiply(x, y);
            double[] yy = Bernstein.Multiply(y, y);
            double[] ww = Bernstein.Multiply(w, w);
            var form = new double[xx.Length];

            for (int i = 0; i < form.Length; i++)
            {
                form[i] = M11 * xx[i] + 2.0 * M12 * xy[i] + M22 * yy[i] - K * ww[i];
            }

            double mx = Bernstein.MaxAbs(x);
            double my = Bernstein.MaxAbs(y);
            double mw = Bernstein.MaxAbs(w);
            scale = Math.Abs(M11) * mx * mx + 2.0 * Math.Abs(M12) * mx * my + Math.Abs(M22) * my * my + K * mw * mw;

            return form;
        }

        private static double[] Ones(int count)
        {
            var ones = new double[count];

            for (int i = 0; i < count; i++)
            {
                ones[i] = 1.0;
            }

            return ones;
        }
    }

    /// <summary>
    /// A circle or an arc of one, a circle when it is whole.
    /// </summary>
    internal sealed class ArcPartner : QuadricPartner
    {
        private const double TwoPi = 2.0 * Math.PI;

        private readonly double _radius, _start, _sweep, _scale;
        private readonly bool _whole;
        private readonly double _sx, _sy, _ex, _ey;

        /// <summary>
        /// Makes the arc.
        /// </summary>
        /// <param name="cx">The centre.</param>
        /// <param name="cy">The centre.</param>
        /// <param name="radius">The radius, above nought.</param>
        /// <param name="start">The angle it starts at, in radians.</param>
        /// <param name="sweep">The angle it sweeps, signed, counter-clockwise when positive; ignored when whole.</param>
        /// <param name="whole">Whether it is the whole circle.</param>
        /// <param name="sx">Its start point.</param>
        /// <param name="sy">Its start point.</param>
        /// <param name="ex">Its end point.</param>
        /// <param name="ey">Its end point.</param>
        internal ArcPartner(double cx, double cy, double radius, double start, double sweep, bool whole, double sx, double sy, double ex, double ey)
            : base(cx, cy, 1.0, 0.0, 1.0, radius * radius)
        {
            _radius = radius;
            _start = start;
            _sweep = sweep;
            _whole = whole;
            _sx = sx;
            _sy = sy;
            _ex = ex;
            _ey = ey;
            _scale = Math.Max(Math.Abs(cx), Math.Abs(cy)) + radius;
        }

        internal override double Scale => _scale;

        internal override double Extent => 2.0 * _radius;

        private bool Within(double angle)
        {
            double along = _sweep >= 0.0 ? angle - _start : _start - angle;
            along -= TwoPi * Math.Floor(along / TwoPi);

            return along <= Math.Abs(_sweep);
        }

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double dx = px - Cx;
            double dy = py - Cy;
            double rho = Math.Sqrt(dx * dx + dy * dy);
            qz = 0.0;

            if (rho == 0.0)
            {
                // Every point of a circle is as near; the arc's start is taken.
                qx = _sx;
                qy = _sy;
                other = _start;
                return _radius;
            }

            double angle = Math.Atan2(dy, dx);

            if (_whole || Within(angle))
            {
                qx = Cx + dx * (_radius / rho);
                qy = Cy + dy * (_radius / rho);
                other = angle;
                return Math.Abs(rho - _radius);
            }

            double sx = px - _sx;
            double sy = py - _sy;
            double ex = px - _ex;
            double ey = py - _ey;
            double toStart = Math.Sqrt(sx * sx + sy * sy);
            double toEnd = Math.Sqrt(ex * ex + ey * ey);

            if (toEnd < toStart)
            {
                qx = _ex;
                qy = _ey;
                other = _start + _sweep;
                return toEnd;
            }

            qx = _sx;
            qy = _sy;
            other = _start;
            return toStart;
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            double lx = min[at];
            double hx = max[at];

            if (lx > hx)
            {
                return double.PositiveInfinity;
            }

            double ly = min[at + 1];
            double hy = max[at + 1];

            // How near and how far the box is from the centre: no point of it is nearer the circle than the nearer of
            // those is to the radius.
            double gx = Cx < lx ? lx - Cx : Cx > hx ? Cx - hx : 0.0;
            double gy = Cy < ly ? ly - Cy : Cy > hy ? Cy - hy : 0.0;
            double fx = Math.Max(Math.Abs(Cx - lx), Math.Abs(Cx - hx));
            double fy = Math.Max(Math.Abs(Cy - ly), Math.Abs(Cy - hy));
            double near = Math.Sqrt(gx * gx + gy * gy);
            double far = Math.Sqrt(fx * fx + fy * fy);
            double slack = 1E-13 * (Math.Abs(Cx) + Math.Abs(Cy) + far + _radius);
            double bound = Math.Max(near - _radius, _radius - far) - slack;

            return bound > 0.0 ? bound : 0.0;
        }

        internal override bool TryGetFrame(double qx, double qy, double qz, double other, out double r1x, out double r1y, out double r1z, out double r2x, out double r2y, out double r2z)
        {
            // By the angle: the point is the centre and the radius turned, so the first derivative is the radius turned a quarter and the
            // second is the radius turned back.
            r1x = -(qy - Cy);
            r1y = qx - Cx;
            r1z = 0.0;
            r2x = -(qx - Cx);
            r2y = -(qy - Cy);
            r2z = 0.0;
            return true;
        }

        internal override bool IsFree(double other) => _whole || (other != _start && other != _start + _sweep);

        internal override void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            minX = Cx - _radius;
            maxX = Cx + _radius;
            minY = Cy - _radius;
            maxY = Cy + _radius;
            minZ = 0.0;
            maxZ = 0.0;
        }

        internal override int EndCount => _whole ? 0 : 2;

        internal override void EndAt(int index, out double x, out double y, out double z)
        {
            x = index == 0 ? _sx : _ex;
            y = index == 0 ? _sy : _ey;
            z = 0.0;
        }

        /// <summary>
        /// Offers the places of a piece: a piece that is itself an arc of a circle, its circle read to rounding, meets this one as the
        /// circles meet, in closed form (<see cref="ArcPairs"/>); any other by the roots of the form.
        /// </summary>
        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            var circle = new PieceArc { Cx = Cx, Cy = Cy, Radius = _radius, Start = _start, Sweep = _sweep, Whole = _whole };

            if (curve.TryGetArc(piece, out PieceArc arc) && ArcPairs.Exact(arc, circle, hunt))
            {
                ArcPairs.Meet(curve, piece, arc, circle, this, hunt);
                return;
            }

            base.PieceCandidates(curve, pieces, piece, hunt);
        }
    }
}
