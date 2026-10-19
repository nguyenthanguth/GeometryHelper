using System;
using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Where two drawn curves cross, from the exact numbers of the curves and not from their chords. Segments cross segments, circles
    /// and arcs, and ellipses; circles and arcs cross circles and arcs, in a plane or across planes. An ellipse crossing a circle, an
    /// arc or another ellipse is not found.
    /// </summary>
    /// <remarks>
    /// Whether two shapes are in one plane is judged by the tolerance, as the library judges it: a segment lies in the plane of a circle
    /// when both its ends are within the tolerance of the plane, and the planes of two circles are one when they differ by an angle that,
    /// over the larger radius, moves a point by no more than the tolerance. A circle or an arc is measured from its start, so an arc of a
    /// bulge of 1E-8 on a chord of 60 000, whose centre is 1.5E12 away, crosses a line where it should to a few ulps of the coordinates.
    /// Two circles or arcs in one plane are crossed from their centres, and where either radius is 100 000 or more the crossing is worked out again from the start of the
    /// first, so that two arcs of a bulge of 1E-7 and 1E-8 on chords of 60 000 that cross at right angles cross to a billionth.
    /// </remarks>
    public static class CurveIntersections
    {
        /// <summary>
        /// How many units in the last place of the coordinates the tolerance is raised to at the least: sixteen. A tolerance of nought, or one
        /// smaller than the rounding of the numbers, finds no crossing of two lines that cross.
        /// </summary>
        public const double MinimumUlps = 16.0;

        private const double Epsilon = 2.220446049250313E-16;

        /// <summary>
        /// Gets whether the crossings of two curves are found: they are unless both are conics and one of them is an ellipse.
        /// </summary>
        /// <param name="first">The first curve.</param>
        /// <param name="second">The second curve.</param>
        /// <exception cref="ArgumentNullException">Thrown when a curve is null.</exception>
        public static bool IsSupported(SceneCurve first, SceneCurve second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            return first.Shape == CurveShape.Segment || second.Shape == CurveShape.Segment || (first.IsCircular && second.IsCircular);
        }

        /// <summary>
        /// Finds the points where two curves cross or touch. Two curves meet where they come within the tolerance of one another; two
        /// segments along one line, or two circles on one centre and radius, share a stretch and not a point, and give none.
        /// </summary>
        /// <param name="first">The first curve.</param>
        /// <param name="second">The second curve.</param>
        /// <param name="tolerance">
        /// How close two curves have to come to meet, in millimetres. It is taken as <see cref="MinimumUlps"/> units in the last place of the
        /// largest coordinate of the two curves where it is less than that, so a tolerance of nought still finds a crossing.
        /// </param>
        /// <returns>The points, at most two, in no promised order; none where <see cref="IsSupported"/> is false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a curve is null.</exception>
        public static IReadOnlyList<Vec3> Find(SceneCurve first, SceneCurve second, double tolerance)
        {
            if (!IsSupported(first, second))
            {
                return new Vec3[0];
            }

            tolerance = Math.Max(tolerance, MinimumUlps * Epsilon * Magnitude(first, second));
            var found = new List<Vec3>();

            if (first.Shape == CurveShape.Segment && second.Shape == CurveShape.Segment)
            {
                SegmentSegment(first, second, tolerance, found);
            }
            else if (first.Shape == CurveShape.Segment)
            {
                SegmentConic(first, second, tolerance, found);
            }
            else if (second.Shape == CurveShape.Segment)
            {
                SegmentConic(second, first, tolerance, found);
            }
            else
            {
                CircleCircle(first, second, tolerance, found);
            }

            return found;
        }

        /// <summary>
        /// Gets the largest coordinate the two curves reach, which sets what a unit in the last place of their numbers is.
        /// </summary>
        private static double Magnitude(SceneCurve first, SceneCurve second)
        {
            double m = 0.0;

            foreach (SceneCurve curve in new[] { first, second })
            {
                curve.BoundingSphere(out Vec3 center, out double radius);
                m = Math.Max(m, Math.Max(Math.Abs(center.X), Math.Max(Math.Abs(center.Y), Math.Abs(center.Z))) + radius);
            }

            return m;
        }

        private static void SegmentSegment(SceneCurve first, SceneCurve second, double tolerance, List<Vec3> found)
        {
            Vec3 e1 = first.End - first.Start;
            Vec3 e2 = second.End - second.Start;
            Vec3 w = first.Start - second.Start;
            double a = Vec3.Dot(e1, e1);
            double b = Vec3.Dot(e1, e2);
            double c = Vec3.Dot(e2, e2);
            double d = Vec3.Dot(e1, w);
            double e = Vec3.Dot(e2, w);
            double den = a * c - b * b;

            if (!(a > 0.0) || !(c > 0.0) || den <= 1E-12 * a * c)
            {
                return;
            }

            double s = (b * e - c * d) / den;
            double u = (a * e - b * d) / den;
            double slack1 = tolerance / Math.Sqrt(a);
            double slack2 = tolerance / Math.Sqrt(c);

            if (s < -slack1 || s > 1.0 + slack1 || u < -slack2 || u > 1.0 + slack2)
            {
                return;
            }

            s = Math.Max(0.0, Math.Min(1.0, s));
            u = Math.Max(0.0, Math.Min(1.0, u));
            Vec3 p = first.Start + e1 * s;
            Vec3 q = second.Start + e2 * u;

            if ((p - q).Length <= tolerance)
            {
                found.Add((p + q) * 0.5);
            }
        }

        private static void SegmentConic(SceneCurve segment, SceneCurve conic, double tolerance, List<Vec3> found)
        {
            Vec3 e = segment.End - segment.Start;
            double length = e.Length;

            if (!(length > 0.0))
            {
                return;
            }

            var hits = new List<double>();

            if (conic.IsCircular)
            {
                conic.ToStartFrame(segment.Start, out _, out _, out double zStart);
                conic.ToStartFrame(segment.End, out _, out _, out double zEnd);
                LineMeetsCircle(segment.Start, e, conic, tolerance, Math.Abs(zStart) <= tolerance && Math.Abs(zEnd) <= tolerance, hits);
            }
            else
            {
                Vec3 fromCentre = segment.Start - conic.Center;
                Vec3 toCentre = segment.End - conic.Center;
                bool inPlane = Math.Abs(Vec3.Dot(fromCentre, conic.Normal)) <= tolerance && Math.Abs(Vec3.Dot(toCentre, conic.Normal)) <= tolerance;
                LineMeetsEllipse(segment.Start, e, conic, tolerance, inPlane, hits);
            }

            double slack = tolerance / length;
            double angleTolerance = tolerance / Math.Max(Math.Min(conic.RadiusU, conic.RadiusV), tolerance);

            foreach (double s in hits)
            {
                if (s < -slack || s > 1.0 + slack)
                {
                    continue;
                }

                Vec3 p = segment.Start + e * Math.Max(0.0, Math.Min(1.0, s));
                double t = conic.ParameterOf(p);

                if (conic.ContainsParameter(t, angleTolerance))
                {
                    found.Add((p + conic.PointAt(t)) * 0.5);
                }
            }
        }

        /// <summary>
        /// Finds where the line <c>origin + s * direction</c> meets the whole of a circle, as values of s: two, or one where it touches or comes
        /// within the tolerance of touching, or none. A line judged in the plane of the circle is flattened into it and meets it where a
        /// quadratic says; one that is not meets the plane in a point that either is on the circle or is not.
        /// </summary>
        private static void LineMeetsCircle(Vec3 origin, Vec3 direction, SceneCurve circle, double tolerance, bool inPlane, List<double> hits)
        {
            double r = circle.RadiusU;
            circle.ToStartFrame(origin, out double uo, out double vo, out double zo);
            double du = Vec3.Dot(direction, circle.RadialAtStart);
            double dv = Vec3.Dot(direction, circle.TangentAtStart);
            double dz = Vec3.Dot(direction, circle.Normal);

            if (!inPlane)
            {
                if (Math.Abs(dz) <= 1E-12 * direction.Length)
                {
                    return;
                }

                double s0 = -zo / dz;
                double u = uo + s0 * du;
                double v = vo + s0 * dv;
                double gap = Math.Abs(u * u + v * v + 2.0 * r * u) / (Math.Sqrt((u + r) * (u + r) + v * v) + r);

                if (gap <= tolerance)
                {
                    hits.Add(s0);
                }

                return;
            }

            // (u + R)^2 + v^2 = R^2 along the line, with R^2 gone from both sides so that no number is as big as R^2.
            double qa = du * du + dv * dv;
            double qb = 2.0 * (uo * du + vo * dv + r * du);
            double qc = uo * uo + vo * vo + 2.0 * r * uo;

            if (!(qa > 0.0))
            {
                return;
            }

            double disc = qb * qb - 4.0 * qa * qc;

            if (disc < 0.0)
            {
                // The line may still pass within the tolerance of the circle: where the centre is nearest the line it is that far off the circle.
                double nearest = -((uo + r) * du + vo * dv) / qa;
                double pu = uo + nearest * du;
                double pv = vo + nearest * dv;
                double radial = Math.Sqrt((pu + r) * (pu + r) + pv * pv);

                if (Math.Abs(radial - r) <= tolerance)
                {
                    hits.Add(nearest);
                }

                return;
            }

            double root = Math.Sqrt(disc);
            double q = -0.5 * (qb + (qb >= 0.0 ? root : -root));
            double r1 = q != 0.0 ? q / qa : -qb / (2.0 * qa);
            double r2 = q != 0.0 ? qc / q : r1;

            if (root == 0.0)
            {
                hits.Add(r1);
            }
            else
            {
                hits.Add(Math.Min(r1, r2));
                hits.Add(Math.Max(r1, r2));
            }
        }

        /// <summary>
        /// Finds where the line <c>origin + s * direction</c> meets the whole of an ellipse, as values of s, in order of s: two, or one where it
        /// touches, or none.
        /// </summary>
        private static void LineMeetsEllipse(Vec3 origin, Vec3 direction, SceneCurve ellipse, double tolerance, bool inPlane, List<double> hits)
        {
            Vec3 w = origin - ellipse.Center;
            double xo = Vec3.Dot(w, ellipse.AxisU);
            double yo = Vec3.Dot(w, ellipse.AxisV);
            double zo = Vec3.Dot(w, ellipse.Normal);
            double dx = Vec3.Dot(direction, ellipse.AxisU);
            double dy = Vec3.Dot(direction, ellipse.AxisV);
            double dz = Vec3.Dot(direction, ellipse.Normal);
            double a = ellipse.RadiusU;
            double b = ellipse.RadiusV;

            if (inPlane)
            {
                double qa = dx * dx / (a * a) + dy * dy / (b * b);
                double qb = 2.0 * (xo * dx / (a * a) + yo * dy / (b * b));
                double qc = xo * xo / (a * a) + yo * yo / (b * b) - 1.0;

                if (!(qa > 0.0))
                {
                    return;
                }

                double disc = qb * qb - 4.0 * qa * qc;

                if (disc < -1E-12 * (qb * qb + Math.Abs(4.0 * qa * qc)))
                {
                    return;
                }

                double root = Math.Sqrt(Math.Max(0.0, disc));
                double q = -0.5 * (qb + (qb >= 0.0 ? root : -root));
                double r1 = q != 0.0 ? q / qa : -qb / (2.0 * qa);
                double r2 = q != 0.0 ? qc / q : r1;

                if (root == 0.0)
                {
                    hits.Add(r1);
                }
                else
                {
                    hits.Add(Math.Min(r1, r2));
                    hits.Add(Math.Max(r1, r2));
                }

                return;
            }

            if (Math.Abs(dz) <= 1E-12 * direction.Length)
            {
                return;
            }

            double s0 = -zo / dz;
            double x = xo + s0 * dx;
            double y = yo + s0 * dy;
            double f = x * x / (a * a) + y * y / (b * b) - 1.0;
            double gradient = Math.Sqrt(4.0 * x * x / (a * a * a * a) + 4.0 * y * y / (b * b * b * b));
            double distance = gradient > 0.0 ? Math.Abs(f) / gradient : double.PositiveInfinity;

            if (distance <= tolerance)
            {
                hits.Add(s0);
            }
        }

        /// <summary>
        /// The radius from which the crossing of two circles in one plane is worked out again from their starts: 100 000, where the arithmetic from the
        /// centres has begun to lose more than a billionth.
        /// </summary>
        private const double RefineFromRadius = 1E5;

        /// <summary>
        /// Works a crossing of two circles in one plane out again by Newton's method along the first, measuring the gap to the second from its start, as an arc is
        /// measured against a line: a point of the first is its start and a turn from it, the gap to the second is found in the frame of its start, and
        /// no number in it is as big as a radius. The crossing from the centres, good to a few digits, is where it begins; a crossing that does not come out
        /// nearer both circles than it began, or that comes out on one with another, is left as it was.
        /// </summary>
        private static void Refine(SceneCurve first, SceneCurve second, List<Vec3> candidates, double tolerance)
        {
            var refined = new Vec3[candidates.Count];
            var moved = new bool[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                moved[i] = TryRefine(first, second, candidates[i], out refined[i]);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    // Two crossings that were apart and come out as one: the one that was further from the root has fallen into the other's, and stays as it was.
                    if (moved[i] && moved[j] && (refined[i] - refined[j]).Length <= tolerance && (candidates[i] - candidates[j]).Length > tolerance)
                    {
                        double shiftI = (refined[i] - candidates[i]).Length;
                        double shiftJ = (refined[j] - candidates[j]).Length;
                        moved[shiftI <= shiftJ ? j : i] = false;
                    }
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (moved[i])
                {
                    candidates[i] = refined[i];
                }
            }
        }

        private static bool TryRefine(SceneCurve first, SceneCurve second, Vec3 start, out Vec3 result)
        {
            result = start;
            double r1 = first.RadiusU;
            double t = first.ParameterOf(start);
            Vec3 point = first.PointAt(t);
            double gap = Math.Abs(second.GapFromCircle(point, out _));

            // How far the crossing from the centres is from the two circles together; what comes out has to be no further.
            double startGap = Math.Abs(first.GapFromCircle(start, out _)) + Math.Abs(second.GapFromCircle(start, out _));

            for (int step = 0; step < 24; step++)
            {
                second.ToStartFrame(point, out double u, out double v, out _);
                double wu = u + second.RadiusU;
                double length = Math.Sqrt(wu * wu + v * v);

                if (!(length > 0.0))
                {
                    break;
                }

                // The gap changes along the first as the normal of the second dotted with the first's tangent there, a turn of f from the start of the first.
                double f = t - first.StartAngle;
                Vec3 tangent = first.RadialAtStart * (-Math.Sin(f)) + first.TangentAtStart * Math.Cos(f);
                Vec3 normal = second.RadialAtStart * (wu / length) + second.TangentAtStart * (v / length);
                double slope = r1 * Vec3.Dot(normal, tangent);
                double signed = second.GapFromCircle(point, out _);

                if (!(Math.Abs(slope) > 1E-12))
                {
                    break;
                }

                double next = t - signed / slope;
                Vec3 candidate = first.PointAt(next);
                double nextGap = Math.Abs(second.GapFromCircle(candidate, out _));

                if (!(nextGap < gap))
                {
                    break;
                }

                bool done = (candidate - point).Length <= 1E-13 * Math.Max(1.0, Math.Abs(point.X) + Math.Abs(point.Y) + Math.Abs(point.Z));
                t = next;
                point = candidate;
                gap = nextGap;

                if (done)
                {
                    break;
                }
            }

            result = point;
            return !point.Equals(start) && gap + Math.Abs(first.GapFromCircle(point, out _)) <= startGap;
        }

        /// <summary>
        /// Finds the crossings of two circles in one plane where one of them is huge, from the centre of the smaller and not from the centres of both: a unit vector from the
        /// centre of the larger to it, and the gap of that centre from the larger circle measured from the start of the larger (an arc is exact from its start; its centre, a
        /// million times further off, is not), give where the crossings lie round the centre of the smaller, at an angle that comes from the factors of the differences of
        /// squares, so that nothing as big as a radius is subtracted from another. A crossing is then as exact as the small circle is, to a few parts in 1E16 of its radius.
        /// </summary>
        private static void HugeCoplanar(SceneCurve first, SceneCurve second, double tolerance, List<Vec3> candidates)
        {
            SceneCurve big = first.RadiusU >= second.RadiusU ? first : second;
            SceneCurve small = ReferenceEquals(big, first) ? second : first;
            double rb = big.RadiusU;
            double rs = small.RadiusU;
            double e = big.GapFromCircle(small.Center, out _);

            // The small circle is clear of the big, or wholly inside it, by more than the tolerance: no crossing. Touching within it is one.
            if (e > rs + tolerance || e < -rs - tolerance || !(rs > 0.0))
            {
                return;
            }

            Vec3 toSmall = small.Center - big.Center;
            toSmall -= big.Normal * Vec3.Dot(toSmall, big.Normal);
            double d = toSmall.Length;

            if (!(d > 0.0))
            {
                return;
            }

            Vec3 n = toSmall * (1.0 / d);
            Vec3 t = Vec3.Cross(big.Normal, n);
            double twoD = 2.0 * (rb + e) * rs;
            double cos = (e * (2.0 * rb + e) + rs * rs) / twoD;
            double sin = Math.Sqrt(Math.Max(0.0, (rs - e) * (2.0 * rb + e - rs) / twoD * ((e + rs) * (2.0 * rb + e + rs) / twoD)));
            Vec3 centre = small.Center - n * (rs * Math.Max(-1.0, Math.Min(1.0, cos)));

            if (sin > 0.0)
            {
                candidates.Add(centre - t * (rs * sin));
                candidates.Add(centre + t * (rs * sin));
            }
            else
            {
                candidates.Add(centre);
            }
        }

        private static void CircleCircle(SceneCurve first, SceneCurve second, double tolerance, List<Vec3> found)
        {
            double r1 = first.RadiusU;
            double r2 = second.RadiusU;
            Vec3 between = second.Center - first.Center;
            Vec3 axis = Vec3.Cross(first.Normal, second.Normal);
            var candidates = new List<Vec3>();

            // One plane where a turn from one to the other moves a point of the larger circle by no more than the tolerance, and the centres
            // are no further off the plane than that.
            if (axis.Length * Math.Max(r1, r2) <= tolerance)
            {
                if (Math.Abs(Vec3.Dot(between, first.Normal)) > tolerance)
                {
                    return;
                }

                Vec3 flat = between - first.Normal * Vec3.Dot(between, first.Normal);
                double d = flat.Length;

                if (Math.Max(r1, r2) >= RefineFromRadius)
                {
                    // Where a circle is huge its centre is a million times the size of the arcs away, and a point worked out from the centres has lost its last digits.
                    HugeCoplanar(first, second, tolerance, candidates);
                    Refine(first, second, candidates, tolerance);
                }
                else
                {
                    if (!(d > 0.0) || d > r1 + r2 + tolerance || d < Math.Abs(r1 - r2) - tolerance)
                    {
                        return;
                    }

                    // How far the chord of the two crossings is short of the far side of the first circle, g = r1 - along, from the factors of the difference of two squares
                    // and not from squares of radii; then h is the half chord, from the factors of r1 squared less along squared, g (2 r1 - g).
                    double dr = d - r1;
                    double g = (r2 - dr) * (r2 + dr) / (2.0 * d);
                    double along = r1 - g;
                    double h = Math.Sqrt(Math.Max(0.0, g * (2.0 * r1 - g)));
                    Vec3 ex = flat * (1.0 / d);
                    Vec3 ey = Vec3.Cross(first.Normal, ex);
                    Vec3 mid = first.Center + ex * along;

                    if (h > 0.0)
                    {
                        candidates.Add(mid - ey * h);
                        candidates.Add(mid + ey * h);
                    }
                    else
                    {
                        candidates.Add(mid);
                    }
                }
            }
            else
            {
                // Two planes meet in a line; the circles meet where the line meets both.
                double h1 = Vec3.Dot(first.Normal, first.Center);
                double h2 = Vec3.Dot(second.Normal, second.Center);
                double nn = Vec3.Dot(axis, axis);
                Vec3 onLine = (Vec3.Cross(second.Normal, axis) * h1 + Vec3.Cross(axis, first.Normal) * h2) * (1.0 / nn);
                Vec3 direction = axis.Normalized();
                var hits = new List<double>();
                LineMeetsCircle(onLine, direction, first, tolerance, true, hits);

                foreach (double s in hits)
                {
                    candidates.Add(onLine + direction * s);
                }
            }

            foreach (Vec3 p in candidates)
            {
                double gap = second.GapFromCircle(p, out double z);

                if (Math.Abs(z) > tolerance || Math.Abs(gap) > tolerance)
                {
                    continue;
                }

                if (first.ContainsParameter(first.ParameterOf(p), tolerance / Math.Max(r1, tolerance))
                    && second.ContainsParameter(second.ParameterOf(p), tolerance / Math.Max(r2, tolerance)))
                {
                    found.Add(p);
                }
            }
        }
    }
}
