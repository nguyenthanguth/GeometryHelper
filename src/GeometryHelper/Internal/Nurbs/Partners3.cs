using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// Gives the box a partner in space lies in, and how far apart that box is from another: the one bound the straight, flat and
    /// round partners all take from a box of the curve.
    /// </summary>
    internal static class Slab
    {
        /// <summary>
        /// Gets how far a box is from a slab that lies in a frame of three unit directions: for each, the stretch of the slab along it.
        /// </summary>
        /// <param name="min">The smallest coordinates of the box, at an offset.</param>
        /// <param name="max">The largest coordinates of the box, at the same offset.</param>
        /// <param name="at">The offset.</param>
        /// <param name="ox">A point of the frame, which the stretches are measured from.</param>
        /// <param name="oy">A point of the frame, which the stretches are measured from.</param>
        /// <param name="oz">A point of the frame, which the stretches are measured from.</param>
        /// <param name="axes">The three unit directions, nine numbers.</param>
        /// <param name="low">How far along each direction the slab starts, minus infinity for no start.</param>
        /// <param name="high">How far along each direction the slab ends, plus infinity for no end.</param>
        /// <param name="scale">The size of the numbers, which rounding goes by.</param>
        /// <returns>
        /// A bound no point of the box is nearer the slab than: the box lies in the box of its own corners, which in the frame is a box
        /// too, and the distance between two boxes with their sides along the same three directions is the length of the three gaps.
        /// </returns>
        internal static double Gap(double[] min, double[] max, int at, double ox, double oy, double oz, double[] axes, double[] low, double[] high, double scale)
        {
            double lx = min[at];
            double hx = max[at];

            if (lx > hx)
            {
                return double.PositiveInfinity;
            }

            double cx = 0.5 * (lx + hx);
            double cy = 0.5 * (min[at + 1] + max[at + 1]);
            double cz = 0.5 * (min[at + 2] + max[at + 2]);
            double ex = 0.5 * (hx - lx);
            double ey = 0.5 * (max[at + 1] - min[at + 1]);
            double ez = 0.5 * (max[at + 2] - min[at + 2]);
            double rx = cx - ox;
            double ry = cy - oy;
            double rz = cz - oz;
            double slack = 1E-13 * (Math.Abs(cx) + Math.Abs(cy) + Math.Abs(cz) + ex + ey + ez + scale);
            double square = 0.0;

            for (int k = 0; k < 3; k++)
            {
                double ax = axes[3 * k];
                double ay = axes[3 * k + 1];
                double az = axes[3 * k + 2];
                double along = ax * rx + ay * ry + az * rz;
                double reach = Math.Abs(ax) * ex + Math.Abs(ay) * ey + Math.Abs(az) * ez;
                double gap = Math.Max(low[k] - (along + reach), (along - reach) - high[k]) - slack;

                if (gap > 0.0)
                {
                    square += gap * gap;
                }
            }

            return Math.Sqrt(square);
        }

        /// <summary>
        /// Gets the size of the coordinates of a piece, which the rounding of the numbers it is made of goes by: the larger of the largest
        /// coordinate of its control points and a given size.
        /// </summary>
        /// <remarks>
        /// A piece far from the origin carries the rounding of its coordinates, a hundred-thousandth at a hundred thousand, however small it
        /// is, and a polynomial worked out from the piece measured from a point beside it is as far from nought as that, so that what is
        /// read as nought, the piece lying in a plane or along a line, has to be read to that.
        /// </remarks>
        internal static double World(BezierPieces pieces, int piece, double size)
        {
            int at = pieces.PointOffset(piece);
            int count = (pieces.Degree + 1) * pieces.Dim;
            double largest = size;

            for (int i = 0; i < count; i++)
            {
                largest = Math.Max(largest, Math.Abs(pieces.Points[at + i]));
            }

            return largest;
        }

        /// <summary>
        /// Gets the least and the largest weight of a piece: one and one where it is not rational.
        /// </summary>
        internal static void Weights(BezierPieces pieces, int piece, out double least, out double most)
        {
            least = 1.0;
            most = 1.0;

            if (!pieces.Rational)
            {
                return;
            }

            int at = pieces.WeightOffset(piece);
            least = double.PositiveInfinity;
            most = 0.0;

            for (int j = 0; j <= pieces.Degree; j++)
            {
                least = Math.Min(least, pieces.Weights[at + j]);
                most = Math.Max(most, pieces.Weights[at + j]);
            }
        }

        /// <summary>
        /// Gets two unit directions square to a unit direction and to each other: the first square to it and to the coordinate axis it
        /// leans on least, the second square to both.
        /// </summary>
        internal static void Perpendiculars(double dx, double dy, double dz, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz)
        {
            double ax = Math.Abs(dx);
            double ay = Math.Abs(dy);
            double az = Math.Abs(dz);
            double hx = ax <= ay && ax <= az ? 1.0 : 0.0;
            double hy = hx == 0.0 && ay <= az ? 1.0 : 0.0;
            double hz = hx == 0.0 && hy == 0.0 ? 1.0 : 0.0;
            double px = dy * hz - dz * hy;
            double py = dz * hx - dx * hz;
            double pz = dx * hy - dy * hx;
            double length = Math.Sqrt(px * px + py * py + pz * pz);

            ux = px / length;
            uy = py / length;
            uz = pz / length;
            vx = dy * uz - dz * uy;
            vy = dz * ux - dx * uz;
            vz = dx * uy - dy * ux;
        }
    }

    /// <summary>
    /// A straight stretch of a line in space: a segment, a ray, or a line without ends cut to the stretch that can matter. It is
    /// the line through an origin along a unit direction, taken from a first distance along to a last.
    /// </summary>
    /// <remarks>
    /// The offset of a piece from the line, the part of its offset from the origin that stands square to the line, is taken as two signed
    /// polynomials along two directions square to the line (<see cref="Offsets"/>): the curve meets the line where both are nought, and a
    /// piece in a plane through the line has one of them nought all along, the other being its signed offset in that plane, as in the plane.
    /// The squared distance, the form (a polynomial of twice the degree of a Bézier piece), is the sum of their squares, taken after the part
    /// along the line is taken off so that a curve that passes a hair from the line loses no digits to the difference of two large squares;
    /// its derivative is nought where the curve comes nearest.
    /// </remarks>
    internal sealed class LinePartner3 : FormPartner
    {
        private readonly double _scale;
        private readonly double _ux, _uy, _uz, _vx, _vy, _vz;

        internal readonly double Ox, Oy, Oz, Dx, Dy, Dz, T0, T1;
        internal readonly bool EndA, EndB;

        /// <summary>
        /// Makes the stretch.
        /// </summary>
        /// <param name="ox">The origin the distances along are measured from.</param>
        /// <param name="oy">The origin the distances along are measured from.</param>
        /// <param name="oz">The origin the distances along are measured from.</param>
        /// <param name="dx">The unit direction.</param>
        /// <param name="dy">The unit direction.</param>
        /// <param name="dz">The unit direction.</param>
        /// <param name="t0">How far along it starts.</param>
        /// <param name="t1">How far along it stops, not below the start.</param>
        /// <param name="endA">Whether the start is a true end of the shape, and not only where a line without ends was cut.</param>
        /// <param name="endB">Whether the stop is a true end of the shape.</param>
        internal LinePartner3(double ox, double oy, double oz, double dx, double dy, double dz, double t0, double t1, bool endA, bool endB)
        {
            Ox = ox;
            Oy = oy;
            Oz = oz;
            Dx = dx;
            Dy = dy;
            Dz = dz;
            T0 = t0;
            T1 = t1;
            EndA = endA;
            EndB = endB;

            Slab.Perpendiculars(dx, dy, dz, out _ux, out _uy, out _uz, out _vx, out _vy, out _vz);

            double x0 = ox + t0 * dx;
            double y0 = oy + t0 * dy;
            double z0 = oz + t0 * dz;
            double x1 = ox + t1 * dx;
            double y1 = oy + t1 * dy;
            double z1 = oz + t1 * dz;

            _scale = Math.Max(
                Math.Max(Math.Max(Math.Abs(ox), Math.Abs(oy)), Math.Abs(oz)),
                Math.Max(Math.Max(Math.Max(Math.Abs(x0), Math.Abs(y0)), Math.Abs(z0)), Math.Max(Math.Max(Math.Abs(x1), Math.Abs(y1)), Math.Abs(z1))));
        }

        internal override double Scale => _scale;

        internal override double Extent => T1 - T0;

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double t = (px - Ox) * Dx + (py - Oy) * Dy + (pz - Oz) * Dz;
            double c = t < T0 ? T0 : t > T1 ? T1 : t;
            qx = Ox + c * Dx;
            qy = Oy + c * Dy;
            qz = Oz + c * Dz;
            other = c;
            double ex = px - qx;
            double ey = py - qy;
            double ez = pz - qz;

            return Math.Sqrt(ex * ex + ey * ey + ez * ez);
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            double lx = min[at];

            if (lx > max[at])
            {
                return double.PositiveInfinity;
            }

            double cx = 0.5 * (lx + max[at]);
            double cy = 0.5 * (min[at + 1] + max[at + 1]);
            double cz = 0.5 * (min[at + 2] + max[at + 2]);
            double ex = 0.5 * (max[at] - lx);
            double ey = 0.5 * (max[at + 1] - min[at + 1]);
            double ez = 0.5 * (max[at + 2] - min[at + 2]);
            double rx = cx - Ox;
            double ry = cy - Oy;
            double rz = cz - Oz;
            double slack = 1E-13 * (Math.Abs(cx) + Math.Abs(cy) + Math.Abs(cz) + ex + ey + ez + _scale);

            // The box lies in the box of its own corners, which in the frame of the line (along it, and the two directions square to it)
            // is a box with its sides along the frame's: it is no nearer the stretch than the three gaps between that box and the stretch
            // say, which is no thicker than a line in the two directions square to it.
            double along = Dx * rx + Dy * ry + Dz * rz;
            double reach = Math.Abs(Dx) * ex + Math.Abs(Dy) * ey + Math.Abs(Dz) * ez;
            double beyond = Math.Max(T0 - (along + reach), (along - reach) - T1) - slack;
            double acrossU = Math.Abs(_ux * rx + _uy * ry + _uz * rz) - (Math.Abs(_ux) * ex + Math.Abs(_uy) * ey + Math.Abs(_uz) * ez) - slack;
            double acrossV = Math.Abs(_vx * rx + _vy * ry + _vz * rz) - (Math.Abs(_vx) * ex + Math.Abs(_vy) * ey + Math.Abs(_vz) * ez) - slack;

            beyond = beyond > 0.0 ? beyond : 0.0;
            acrossU = acrossU > 0.0 ? acrossU : 0.0;
            acrossV = acrossV > 0.0 ? acrossV : 0.0;

            return Math.Sqrt(beyond * beyond + acrossU * acrossU + acrossV * acrossV);
        }

        internal override bool TryGetFrame(double qx, double qy, double qz, double other, out double r1x, out double r1y, out double r1z, out double r2x, out double r2y, out double r2z)
        {
            r1x = Dx;
            r1y = Dy;
            r1z = Dz;
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
            double z0 = Oz + T0 * Dz;
            double x1 = Ox + T1 * Dx;
            double y1 = Oy + T1 * Dy;
            double z1 = Oz + T1 * Dz;
            minX = Math.Min(x0, x1);
            maxX = Math.Max(x0, x1);
            minY = Math.Min(y0, y1);
            maxY = Math.Max(y0, y1);
            minZ = Math.Min(z0, z1);
            maxZ = Math.Max(z0, z1);
        }

        internal override int EndCount => (EndA ? 1 : 0) + (EndB ? 1 : 0);

        internal override void EndAt(int index, out double x, out double y, out double z)
        {
            double t = EndA && index == 0 ? T0 : T1;
            x = Ox + t * Dx;
            y = Oy + t * Dy;
            z = Oz + t * Dz;
        }

        /// <summary>
        /// Brings a place found from the roots of the squared offset to the least of the distance by Newton's method on the distance:
        /// where the curve touches the line, tangent to it, the offset is the square of how far from the touch the place is and the
        /// squared offset the fourth power, whose derivative has a triple root that no digits can tell better than to a third of them.
        /// </summary>
        protected override double Refine(NurbsCurve curve, Hunt hunt, double knot) => curve.Settle(this, knot, hunt);

        private Dictionary<int, bool> _along;
        private Dictionary<int, bool> _flat;

        // What has been found about which pieces of the curve lie along this line, and which in a plane through it, made when first asked.
        private Dictionary<int, bool> Along => _along ?? (_along = new Dictionary<int, bool>());

        private Dictionary<int, bool> Flat => _flat ?? (_flat = new Dictionary<int, bool>());

        /// <summary>
        /// Gets the offset of a piece from the line, the part of it square to the line, as two signed polynomials: along the direction
        /// square to the line that the offset of the piece leans on most, and along the one square to both.
        /// </summary>
        /// <param name="pieces">The pieces of the curve.</param>
        /// <param name="piece">Which piece.</param>
        /// <param name="weights">The weights of the piece, or null when it is not rational.</param>
        /// <param name="most">The numerator of the offset along the direction it leans on most.</param>
        /// <param name="least">The numerator of the offset along the direction square to that and to the line.</param>
        /// <returns>What is nought to rounding in the two: a share of the size of the numbers they were worked out from or, far from the origin, of the coordinates themselves.</returns>
        /// <remarks>
        /// A piece that lies in a plane through the line has the second nought to rounding, and the first is its signed offset in that
        /// plane, which changes sign where the piece crosses the line however shallow the crossing, as a curve's offset from a line does in
        /// the plane. The direction is the one that holds most of the two polynomials' coefficients, by the angle that makes the second's
        /// sum of squares least.
        /// </remarks>
        private double Offsets(BezierPieces pieces, int piece, out double[] weights, out double[] most, out double[] least)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Ox, Oy, Oz }, out double[][] numerators, out weights);
            int p = pieces.Degree;
            var u = new double[p + 1];
            var v = new double[p + 1];
            double uu = 0.0;
            double vv = 0.0;
            double uv = 0.0;

            for (int j = 0; j <= p; j++)
            {
                u[j] = _ux * numerators[0][j] + _uy * numerators[1][j] + _uz * numerators[2][j];
                v[j] = _vx * numerators[0][j] + _vy * numerators[1][j] + _vz * numerators[2][j];
                uu += u[j] * u[j];
                vv += v[j] * v[j];
                uv += u[j] * v[j];
            }

            double angle = 0.5 * Math.Atan2(2.0 * uv, uu - vv);
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            most = new double[p + 1];
            least = new double[p + 1];

            for (int j = 0; j <= p; j++)
            {
                most[j] = c * u[j] + s * v[j];
                least[j] = c * v[j] - s * u[j];
            }

            double size = Bernstein.MaxAbs(numerators[0]) + Bernstein.MaxAbs(numerators[1]) + Bernstein.MaxAbs(numerators[2]);
            Slab.Weights(pieces, piece, out _, out double heaviest);

            return Math.Max(NurbsCurve.NoiseShare * size, Hunt.SnapShare * Slab.World(pieces, piece, _scale) * heaviest);
        }

        /// <summary>
        /// Offers the places of one piece where its distance to the line can be least: where it meets the line, and where it stops coming
        /// nearer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The offset of the piece from the line is taken as its two signed parts square to the line (<see cref="Offsets"/>), not as their
        /// sum of squares: a crossing is a root of both, and the square of the offset has a double root there, which rounding lumps with
        /// every stretch where the curve is within the snap of the line and leaves the distance a V that Newton's method cannot settle on.
        /// A piece in a plane through the line is the plane's case: its crossings are the roots of its signed offset, however shallow, and
        /// its touches the stationary points of that offset. Otherwise a crossing is a root of either part where the piece is within the
        /// snap of the line, and the places it comes nearest are the stationary points of the squared offset, each brought to the least of
        /// the distance by Newton's method on it and, where that has no curvature to go by, by a golden section (<see cref="Least"/>); those
        /// that end in one basin of the distance are one.
        /// </para>
        /// </remarks>
        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            // The start of the piece; the end of it is the start of the next, or the end of the curve, offered by the caller.
            curve.Consider(hunt, this, pieces.From[piece]);

            double noise = Offsets(pieces, piece, out double[] weights, out double[] most, out double[] least);
            bool flat = Bernstein.MaxAbs(least) <= noise;
            Flat[piece] = flat;

            if (flat && Bernstein.MaxAbs(most) <= noise)
            {
                Along[piece] = true;
                return;
            }

            Along[piece] = false;
            List<double> roots = hunt.Roots;
            roots.Clear();

            if (flat)
            {
                // In a plane through the line: where the signed offset crosses nought, and where it stops falling or rising.
                Bernstein.FindRoots(most, noise, roots);
                double[] slope = NurbsCurve.CoordinateSlope(most, weights, out double slopeScale);
                Bernstein.FindRoots(slope, NurbsCurve.NoiseShare * slopeScale, roots);

                for (int i = 0; i < roots.Count; i++)
                {
                    curve.Consider(hunt, this, At(pieces, piece, roots[i]));
                }

                roots.Clear();
                return;
            }

            // The crossings: a root of either part where the other is nought too, the piece within the snap of the line, which a piece
            // whose box is further from it has none of.
            if (Bound(pieces.BoxMin, pieces.BoxMax, 3 * piece) <= hunt.Snap)
            {
                Bernstein.FindRoots(most, noise, roots);
                Bernstein.FindRoots(least, noise, roots);
                double[] point = hunt.Point;

                for (int i = 0; i < roots.Count; i++)
                {
                    double knot = At(pieces, piece, roots[i]);
                    curve.PointAt(knot, point);

                    if (Gap(point[0], point[1], point[2], out _, out _, out _, out _) <= hunt.Snap)
                    {
                        curve.Consider(hunt, this, knot);
                    }
                }

                roots.Clear();
            }

            // Where the squared offset, the sum of the squares of the two parts, stops falling or rising, each brought to the least of the
            // distance.
            double[] form = Bernstein.Multiply(most, most);
            double[] square = Bernstein.Multiply(least, least);

            for (int j = 0; j < form.Length; j++)
            {
                form[j] += square[j];
            }

            double[] formSlope = FormSlope(form, weights, out double formScale);
            Bernstein.FindRoots(formSlope, NurbsCurve.NoiseShare * formScale, roots);
            var knots = new double[roots.Count];
            var gaps = new double[roots.Count];

            for (int i = 0; i < roots.Count; i++)
            {
                knots[i] = Least(curve, pieces, piece, Refine(curve, hunt, At(pieces, piece, roots[i])), hunt, out gaps[i]);
            }

            roots.Clear();
            Array.Sort(knots, gaps);

            // The places of one basin of the distance are the one place, the nearest kept, the first along where as near: a least with no
            // curvature, of the fourth or sixth order, is as near as the digits tell for a stretch, and its roots fall all along it.
            int kept = 0;

            for (int i = 0; i < knots.Length; i++)
            {
                if (kept > 0 && OneBasin(curve, knots[kept - 1], gaps[kept - 1], knots[i], gaps[i], hunt))
                {
                    if (gaps[i] < gaps[kept - 1])
                    {
                        knots[kept - 1] = knots[i];
                        gaps[kept - 1] = gaps[i];
                    }

                    continue;
                }

                knots[kept] = knots[i];
                gaps[kept] = gaps[i];
                kept++;
            }

            for (int i = 0; i < kept; i++)
            {
                curve.Consider(hunt, this, knots[i]);
            }
        }

        /// <summary>
        /// Gets the distance from a point of the curve to the line.
        /// </summary>
        private double GapAt(NurbsCurve curve, double knot, double[] point)
        {
            curve.PointAt(knot, point);

            return Gap(point[0], point[1], point[2], out _, out _, out _, out _);
        }

        /// <summary>
        /// Brings a place of a piece to the least of its distance from the line by a golden section, inside the piece, of the stretch it
        /// goes down to either way: where the distance has no curvature, Newton's method has nothing to go by, and the roots of the squared
        /// offset's slope are as far out as the digits of a polynomial of the fifth order in the distance from the least are poor.
        /// </summary>
        /// <returns>The knot value, which is the one given unless a nearer one was found.</returns>
        private double Least(NurbsCurve curve, BezierPieces pieces, int piece, double knot, Hunt hunt, out double gap)
        {
            double low = pieces.From[piece];
            double high = pieces.To[piece];
            var point = new double[3];
            gap = GapAt(curve, knot, point);
            double snap = hunt.Snap;

            // A place twice as far as anything wanted is left where it is.
            if (!(gap <= 2.0 * hunt.Limit + snap))
            {
                return knot;
            }

            double step = 1E-6 * (high - low);
            double left = Math.Max(low, knot - step);
            double right = Math.Min(high, knot + step);
            double leftGap = left < knot ? GapAt(curve, left, point) : double.PositiveInfinity;
            double rightGap = right > knot ? GapAt(curve, right, point) : double.PositiveInfinity;

            // A least already, to a snap, or a greatest, which is not brought anywhere: only a place on a slope is.
            if ((leftGap < gap - snap) == (rightGap < gap - snap))
            {
                return knot;
            }

            // Down the slope, the step doubled each time, until the distance rises again or the piece ends: the least lies between the
            // last two places looked at before the one where it rose.
            int side = leftGap < gap - snap ? -1 : 1;
            double behind = knot;
            double here = side < 0 ? left : right;
            double value = side < 0 ? leftGap : rightGap;
            double best = here;
            double bestGap = value;
            double a;
            double b;

            for (double width = 2.0 * step; ; width *= 2.0)
            {
                double ahead = side < 0 ? Math.Max(low, here - width) : Math.Min(high, here + width);
                double there = ahead == here ? value : GapAt(curve, ahead, point);

                if (!(there < value) || ahead == here)
                {
                    a = Math.Min(behind, ahead);
                    b = Math.Max(behind, ahead);
                    break;
                }

                behind = here;
                here = ahead;
                value = there;
                best = here;
                bestGap = value;
            }

            const double Golden = 0.38196601125010515;
            double c = a + Golden * (b - a);
            double d = b - Golden * (b - a);
            double fc = GapAt(curve, c, point);
            double fd = GapAt(curve, d, point);

            for (int i = 0; i < 100 && b - a > 1E-12 * (high - low) + 1E-16 * (Math.Abs(a) + Math.Abs(b)); i++)
            {
                if (fc <= fd)
                {
                    b = d;
                    d = c;
                    fd = fc;
                    c = a + Golden * (b - a);
                    fc = GapAt(curve, c, point);
                }
                else
                {
                    a = c;
                    c = d;
                    fc = fd;
                    d = b - Golden * (b - a);
                    fd = GapAt(curve, d, point);
                }
            }

            double found = fc <= fd ? c : d;
            double foundGap = Math.Min(fc, fd);

            if (foundGap < bestGap)
            {
                best = found;
                bestGap = foundGap;
            }

            gap = bestGap;
            return best;
        }

        /// <summary>
        /// Says whether two places of a piece lie in one basin of the distance from the line: no point between them farther from it than
        /// the farther of the two by more than four snaps, by eight looks.
        /// </summary>
        private bool OneBasin(NurbsCurve curve, double first, double firstGap, double second, double secondGap, Hunt hunt)
        {
            double ceiling = Math.Max(firstGap, secondGap) + 4.0 * hunt.Snap;
            var point = new double[3];

            for (int k = 1; k <= 8; k++)
            {
                if (GapAt(curve, first + (second - first) * k / 9.0, point) > ceiling)
                {
                    return false;
                }
            }

            return true;
        }

        private static double At(BezierPieces pieces, int piece, double s) => s <= 0.0 ? pieces.From[piece] : s >= 1.0 ? pieces.To[piece] : pieces.From[piece] + s * (pieces.To[piece] - pieces.From[piece]);

        internal override bool Coincident(NurbsCurve curve, int piece)
        {
            if (Along.TryGetValue(piece, out bool known))
            {
                return known;
            }

            double noise = Offsets(curve.Pieces, piece, out _, out double[] most, out double[] least);
            Flat[piece] = Bernstein.MaxAbs(least) <= noise;
            bool coincident = Flat[piece] && Bernstein.MaxAbs(most) <= noise;
            Along[piece] = coincident;

            return coincident;
        }

        internal override bool InPlaneAt(NurbsCurve curve, double knot)
        {
            int piece = curve.PieceAt(curve.Clamp(knot), out bool atStart);

            return IsFlat(curve, piece) && (!atStart || piece == 0 || IsFlat(curve, piece - 1));
        }

        private bool IsFlat(NurbsCurve curve, int piece)
        {
            if (!Flat.TryGetValue(piece, out bool flat))
            {
                Coincident(curve, piece);
                flat = Flat[piece];
            }

            return flat;
        }

        protected override double[] Form(BezierPieces pieces, int piece, out double[] weights, out double scale)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Ox, Oy, Oz }, out double[][] numerators, out weights);
            int p = pieces.Degree;
            var along = new double[p + 1];

            for (int j = 0; j <= p; j++)
            {
                along[j] = Dx * numerators[0][j] + Dy * numerators[1][j] + Dz * numerators[2][j];
            }

            double[] form = null;

            for (int k = 0; k < 3; k++)
            {
                double d = k == 0 ? Dx : k == 1 ? Dy : Dz;
                var across = new double[p + 1];

                for (int j = 0; j <= p; j++)
                {
                    across[j] = numerators[k][j] - d * along[j];
                }

                double[] square = Bernstein.Multiply(across, across);

                if (form == null)
                {
                    form = square;
                }
                else
                {
                    for (int j = 0; j < form.Length; j++)
                    {
                        form[j] += square[j];
                    }
                }
            }

            // The piece lies along the line when its offset from it is within rounding of nought, which is a share of the size of
            // the numbers it was worked out from or, far from the origin, of the coordinates themselves, and the form is the square of
            // that offset.
            double size = Bernstein.MaxAbs(numerators[0]) + Bernstein.MaxAbs(numerators[1]) + Bernstein.MaxAbs(numerators[2]);
            Slab.Weights(pieces, piece, out _, out double most);
            double snap = Hunt.SnapShare * Slab.World(pieces, piece, _scale) * most;
            scale = Math.Max(NurbsCurve.NoiseShare * size * size, snap * snap / NurbsCurve.NoiseShare);

            return form;
        }
    }

    /// <summary>
    /// A plane in space, or the part of it a flat shape fills: the curve's signed offset from the plane is a polynomial along each
    /// Bézier piece, whose roots are where the curve crosses it and whose stationary points are where it comes nearest without.
    /// </summary>
    /// <remarks>
    /// For a flat shape the partner has a nearest point only to a place that stands over the shape, where the foot of the place on
    /// the plane is held by it; to any other the gap is infinite, and the outline of the shape, which are segments, answers.
    /// </remarks>
    internal sealed class PlanePartner3 : Partner
    {
        private readonly Func<GeoPoint3, bool> _holds;
        private readonly double[] _boxMin;
        private readonly double[] _boxMax;
        private readonly double[] _axes = new double[9];
        private readonly double[] _low = new double[3];
        private readonly double[] _high = new double[3];
        private readonly double _scale;
        private readonly double _extent;
        private Dictionary<int, bool> _coincident;

        // What has been found about which pieces of the curve lie in this plane, made when first asked.
        private Dictionary<int, bool> Memo => _coincident ?? (_coincident = new Dictionary<int, bool>());

        internal readonly double Ox, Oy, Oz, Nx, Ny, Nz;

        /// <summary>
        /// Makes the partner.
        /// </summary>
        /// <param name="ox">A point of the plane.</param>
        /// <param name="oy">A point of the plane.</param>
        /// <param name="oz">A point of the plane.</param>
        /// <param name="nx">The unit normal.</param>
        /// <param name="ny">The unit normal.</param>
        /// <param name="nz">The unit normal.</param>
        /// <param name="holds">Says whether the foot of a place on the plane lies in the shape that fills it; null for the whole plane.</param>
        /// <param name="boxMin">The smallest coordinates of a box the shape lies in; null for the whole plane.</param>
        /// <param name="boxMax">The largest coordinates of that box.</param>
        /// <param name="scale">The size of the numbers the shapes are given in.</param>
        /// <param name="extent">How large the shape is, from side to side; for a whole plane, how large the curve it is asked about is.</param>
        internal PlanePartner3(double ox, double oy, double oz, double nx, double ny, double nz, Func<GeoPoint3, bool> holds, double[] boxMin, double[] boxMax, double scale, double extent)
        {
            Ox = ox;
            Oy = oy;
            Oz = oz;
            Nx = nx;
            Ny = ny;
            Nz = nz;
            _holds = holds;
            _boxMin = boxMin;
            _boxMax = boxMax;
            _scale = Math.Max(scale, Math.Max(Math.Abs(ox), Math.Max(Math.Abs(oy), Math.Abs(oz))));
            _extent = extent;

            Slab.Perpendiculars(nx, ny, nz, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
            _axes[0] = nx;
            _axes[1] = ny;
            _axes[2] = nz;
            _axes[3] = ux;
            _axes[4] = uy;
            _axes[5] = uz;
            _axes[6] = vx;
            _axes[7] = vy;
            _axes[8] = vz;
            _low[0] = 0.0;
            _high[0] = 0.0;
            _low[1] = double.NegativeInfinity;
            _high[1] = double.PositiveInfinity;
            _low[2] = double.NegativeInfinity;
            _high[2] = double.PositiveInfinity;
        }

        internal override double Scale => _scale;

        internal override double Extent => _extent;

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double height = Nx * (px - Ox) + Ny * (py - Oy) + Nz * (pz - Oz);
            qx = px - height * Nx;
            qy = py - height * Ny;
            qz = pz - height * Nz;
            other = 0.0;

            if (_holds != null)
            {
                // A foot outside the box of the shape is not over it, which is told for nothing: asked of the shape, it costs the number of
                // the shape's corners, and a solid of many faces is asked of each of them.
                double slack = 1E-9 * _scale;

                if (_boxMin != null && (qx < _boxMin[0] - slack || qx > _boxMax[0] + slack || qy < _boxMin[1] - slack || qy > _boxMax[1] + slack || qz < _boxMin[2] - slack || qz > _boxMax[2] + slack))
                {
                    return double.PositiveInfinity;
                }

                if (!_holds(new GeoPoint3(qx, qy, qz)))
                {
                    return double.PositiveInfinity;
                }
            }

            return Math.Abs(height);
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            double bound = Slab.Gap(min, max, at, Ox, Oy, Oz, _axes, _low, _high, _scale);

            if (_boxMin == null || double.IsPositiveInfinity(bound))
            {
                return bound;
            }

            // And no nearer the shape than its own box.
            double square = 0.0;

            for (int k = 0; k < 3; k++)
            {
                double below = _boxMin[k] - max[at + k];
                double above = min[at + k] - _boxMax[k];
                double gap = (below > above ? below : above) - 1E-13 * _scale;

                if (gap > 0.0)
                {
                    square += gap * gap;
                }
            }

            return Math.Max(bound, Math.Sqrt(square));
        }

        internal override bool TryGetSurface(double px, double py, double pz, out double ux, out double uy, out double uz, out double bend, out double noise)
        {
            double height = Nx * (px - Ox) + Ny * (py - Oy) + Nz * (pz - Oz);
            double side = height >= 0.0 ? 1.0 : -1.0;
            ux = side * Nx;
            uy = side * Ny;
            uz = side * Nz;
            bend = 0.0;
            noise = 0.0;
            return true;
        }

        internal override void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            if (_boxMin == null)
            {
                base.GetBox(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                return;
            }

            minX = _boxMin[0];
            minY = _boxMin[1];
            minZ = _boxMin[2];
            maxX = _boxMax[0];
            maxY = _boxMax[1];
            maxZ = _boxMax[2];
        }

        /// <summary>
        /// Gets the numerator of the signed offset from the plane along a piece, as a polynomial of the piece.
        /// </summary>
        private double[] Offset(BezierPieces pieces, int piece, out double[] weights, out double noise)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Ox, Oy, Oz }, out double[][] numerators, out weights);
            int p = pieces.Degree;
            var offset = new double[p + 1];

            for (int j = 0; j <= p; j++)
            {
                offset[j] = Nx * numerators[0][j] + Ny * numerators[1][j] + Nz * numerators[2][j];
            }

            // What is nought to rounding: a share of the size of the numbers the offset was worked out from or, far from the origin, a
            // share of the coordinates themselves, which carry their own rounding into it, over the least weight.
            double size = Bernstein.MaxAbs(numerators[0]) + Bernstein.MaxAbs(numerators[1]) + Bernstein.MaxAbs(numerators[2]);
            Slab.Weights(pieces, piece, out double least, out _);
            noise = Math.Max(NurbsCurve.NoiseShare * size, Hunt.SnapShare * Slab.World(pieces, piece, _scale) * least);

            return offset;
        }

        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            double from = pieces.From[piece];
            double span = pieces.To[piece] - from;

            // The start of the piece; the end of it is the start of the next, or the end of the curve, offered by the caller.
            curve.Consider(hunt, this, from);

            double[] offset = Offset(pieces, piece, out double[] weights, out double noise);

            if (Bernstein.MaxAbs(offset) <= noise)
            {
                Memo[piece] = true;
                return;
            }

            Memo[piece] = false;

            // Where the piece crosses the plane.
            hunt.Roots.Clear();
            Bernstein.FindRoots(offset, noise, hunt.Roots);
            List<double> roots = hunt.Roots;

            // Where the offset stops falling or rising, which is where the piece comes nearest the plane without crossing it.
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

            double[] offset = Offset(curve.Pieces, piece, out _, out double noise);
            bool coincident = Bernstein.MaxAbs(offset) <= noise;
            Memo[piece] = coincident;

            return coincident;
        }

        /// <summary>
        /// Says that the distance of the curve from the plane is the size of its signed offset everywhere, as a curve's from a line is in
        /// the plane: a least of it curves up, and a hump between two crossings is a greatest however few snaps deep it is.
        /// </summary>
        internal override bool InPlaneAt(NurbsCurve curve, double knot) => true;
    }

    /// <summary>
    /// The surface of a ball: the set of points of space where the distance from a centre is a radius. A Bézier piece put into
    /// the form (the squared distance from the centre less the squared radius) gives a polynomial of twice its degree, whose roots
    /// are where the piece meets the surface.
    /// </summary>
    internal sealed class SpherePartner3 : FormPartner
    {
        private readonly double _scale;

        internal readonly double Cx, Cy, Cz, Radius;

        internal SpherePartner3(double cx, double cy, double cz, double radius)
        {
            Cx = cx;
            Cy = cy;
            Cz = cz;
            Radius = radius;
            _scale = Math.Max(Math.Max(Math.Abs(cx), Math.Abs(cy)), Math.Abs(cz)) + radius;
        }

        internal override double Scale => _scale;

        internal override double Extent => 2.0 * Radius;

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double dx = px - Cx;
            double dy = py - Cy;
            double dz = pz - Cz;
            double rho = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            other = 0.0;

            if (rho == 0.0)
            {
                // Every point of the surface is as near; the one straight up the first axis is taken.
                qx = Cx + Radius;
                qy = Cy;
                qz = Cz;
                return Radius;
            }

            qx = Cx + dx * (Radius / rho);
            qy = Cy + dy * (Radius / rho);
            qz = Cz + dz * (Radius / rho);

            return Math.Abs(rho - Radius);
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
            double lz = min[at + 2];
            double hz = max[at + 2];

            // How near and how far the box is from the centre: no point of it is nearer the surface than the nearer of those is to the radius.
            double gx = Cx < lx ? lx - Cx : Cx > hx ? Cx - hx : 0.0;
            double gy = Cy < ly ? ly - Cy : Cy > hy ? Cy - hy : 0.0;
            double gz = Cz < lz ? lz - Cz : Cz > hz ? Cz - hz : 0.0;
            double fx = Math.Max(Math.Abs(Cx - lx), Math.Abs(Cx - hx));
            double fy = Math.Max(Math.Abs(Cy - ly), Math.Abs(Cy - hy));
            double fz = Math.Max(Math.Abs(Cz - lz), Math.Abs(Cz - hz));
            double near = Math.Sqrt(gx * gx + gy * gy + gz * gz);
            double far = Math.Sqrt(fx * fx + fy * fy + fz * fz);
            double slack = 1E-13 * (Math.Abs(Cx) + Math.Abs(Cy) + Math.Abs(Cz) + far + Radius);
            double bound = Math.Max(near - Radius, Radius - far) - slack;

            return bound > 0.0 ? bound : 0.0;
        }

        internal override bool TryGetSurface(double px, double py, double pz, out double ux, out double uy, out double uz, out double bend, out double noise)
        {
            double dx = px - Cx;
            double dy = py - Cy;
            double dz = pz - Cz;
            double rho = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (!(rho > 0.0))
            {
                ux = 0.0;
                uy = 0.0;
                uz = 0.0;
                bend = 0.0;
                noise = 0.0;
                return false;
            }

            // Outside the surface the nearest point is straight in along the radius and the direction to the point is out along it; inside, the other way.
            double side = rho > Radius ? 1.0 : -1.0;
            ux = side * dx / rho;
            uy = side * dy / rho;
            uz = side * dz / rho;
            bend = side / rho;
            noise = 4.0 * 2.220446049250313E-16 * _scale / rho;
            return true;
        }

        internal override void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            minX = Cx - Radius;
            maxX = Cx + Radius;
            minY = Cy - Radius;
            maxY = Cy + Radius;
            minZ = Cz - Radius;
            maxZ = Cz + Radius;
        }

        protected override double[] Form(BezierPieces pieces, int piece, out double[] weights, out double scale)
        {
            NurbsCurve.Numerators(pieces, piece, new[] { Cx, Cy, Cz }, out double[][] numerators, out weights);
            double[] w = weights ?? Ones(pieces.Degree + 1);
            double[] ww = Bernstein.Multiply(w, w);
            double[] form = null;

            for (int k = 0; k < 3; k++)
            {
                double[] square = Bernstein.Multiply(numerators[k], numerators[k]);

                if (form == null)
                {
                    form = square;
                }
                else
                {
                    for (int j = 0; j < form.Length; j++)
                    {
                        form[j] += square[j];
                    }
                }
            }

            for (int j = 0; j < form.Length; j++)
            {
                form[j] -= Radius * Radius * ww[j];
            }

            double mx = Bernstein.MaxAbs(numerators[0]);
            double my = Bernstein.MaxAbs(numerators[1]);
            double mz = Bernstein.MaxAbs(numerators[2]);
            double mw = Bernstein.MaxAbs(w);
            scale = mx * mx + my * my + mz * mz + Radius * Radius * mw * mw;

            // On the surface to rounding: the distance from it is a share of the coordinates, whose rounding the form carries, and the form is
            // that distance times the sum of the radius and the distance from the centre.
            double snap = Hunt.SnapShare * Slab.World(pieces, piece, _scale);
            scale = Math.Max(scale, mw * mw * snap * (2.0 * Radius + snap) / NurbsCurve.NoiseShare);

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
}
