using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The exact gap between an arc in space and a point, a segment, a flat region, the surface of a body or another arc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here is widened by a tolerance, and a crossing is not looked for by sampling. An arc is the circle it lies on
    /// less the stretch it does not sweep, and the nearest pair of an arc and anything else is one of a short list, every
    /// entry of which is a real distance from a real point of the arc, so the least of them is the gap: an end of the arc
    /// against the other shape; an end, a corner or an edge of the other shape against the arc; and the places where the
    /// distance along the arc turns, which for a straight line, a plane or a segment are the roots of a trigonometric
    /// polynomial of the second degree in the angle. That polynomial is a quartic in the tangent of half the angle measured
    /// from the middle of the arc's sweep, where no root of the arc's is near infinity, whose real roots are found between
    /// the roots of its derivatives, so none is lost to a coarse step.
    /// </para>
    /// <para>
    /// Two arcs are the one case with no closed form. The distance from a point of one to the other arc is exact, and it
    /// changes by no more than the length moved, so the arc is halved until each stretch of it is proved further than the
    /// distance asked, or a point within it is found: a proof, not a sample. Circles on one axis in parallel planes stand the
    /// same distance apart at every angle, so their ends decide them. Arcs nearly so are compared with the copy of one that
    /// stands exactly on the axis of the other, which the ends decide and which is never further from it than the tilt times
    /// the radius and the offset of the centres; that proves the pair clear when the copy is clear by more. What neither
    /// settles within its budget, a pair of partial arcs nearly on one axis whose distance stays near-constant just beyond
    /// the one asked, is searched and not proved: the walk of the arcs, every low point polished, decides it.
    /// </para>
    /// <para>
    /// A shape's own sides are exact. A segment a caller gave that is no longer than the point tolerance is read as its
    /// start, unless it truly meets the arc, as the straight shapes read it.
    /// </para>
    /// </remarks>
    internal static partial class Gap3
    {
        /// <summary>
        /// How much of the reach of an arc's centre and radius it may bow by and be read as its chord, as in the plane.
        /// </summary>
        private const double ArcFlatFraction = 2.5E-16;

        /// <summary>
        /// An arc read for the gap: its circle in the frame it is swept in, its ends, and the box it lies in.
        /// </summary>
        internal sealed class Curve
        {
            private readonly double _cx;
            private readonly double _cy;
            private readonly double _cz;
            private readonly double _ux;
            private readonly double _uy;
            private readonly double _uz;
            private readonly double _vx;
            private readonly double _vy;
            private readonly double _vz;
            private readonly double _nx;
            private readonly double _ny;
            private readonly double _nz;
            private readonly double _radius;
            private readonly double _start;
            private readonly double _sweep;
            private readonly bool _full;

            private Curve(GeoArc3 arc)
                : this(arc.Center, arc.Normal, arc.Radius, arc.StartAngle, arc.SweptAngle)
            {
            }

            private Curve(GeoPoint3 centre, GeoVector3 normal, double radius, double start, double sweep)
            {
                new GeoPlane3(centre, normal).GetAxes(out GeoVector3 u, out GeoVector3 v);

                _cx = centre.X;
                _cy = centre.Y;
                _cz = centre.Z;
                _ux = u.X;
                _uy = u.Y;
                _uz = u.Z;
                _vx = v.X;
                _vy = v.Y;
                _vz = v.Z;
                _nx = normal.X;
                _ny = normal.Y;
                _nz = normal.Z;
                _radius = radius;
                _start = start;
                _sweep = sweep;
                _full = Math.Abs(_sweep) >= 2.0 * Math.PI;
                StartPoint = At(_start);
                EndPoint = _full ? StartPoint : At(_start + _sweep);

                double reach = Math.Abs(_cx) + Math.Abs(_cy) + Math.Abs(_cz) + _radius;

                // An arc bowing less off its chord than its own points are known to is its chord, and is exact as that.
                if (Math.Abs(_sweep) <= Math.PI && 0.1 * _radius * _sweep * _sweep <= reach * ArcFlatFraction)
                {
                    double quarter = Math.Sin(_sweep * 0.25);

                    IsChord = 2.0 * _radius * quarter * quarter <= reach * ArcFlatFraction;
                }

                Box = BoxOf();
            }

            /// <summary>
            /// Gets the smallest box the arc lies in: its ends, and along each axis the place where it turns back, where it sweeps
            /// that far.
            /// </summary>
            private GeoAabb3 BoxOf()
            {
                double minX = Math.Min(StartPoint.X, EndPoint.X);
                double minY = Math.Min(StartPoint.Y, EndPoint.Y);
                double minZ = Math.Min(StartPoint.Z, EndPoint.Z);
                double maxX = Math.Max(StartPoint.X, EndPoint.X);
                double maxY = Math.Max(StartPoint.Y, EndPoint.Y);
                double maxZ = Math.Max(StartPoint.Z, EndPoint.Z);

                if (!IsChord)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        double turning = Math.Atan2(axis == 0 ? _vx : axis == 1 ? _vy : _vz, axis == 0 ? _ux : axis == 1 ? _uy : _uz);

                        for (int half = 0; half < 2; half++)
                        {
                            double angle = turning + half * Math.PI;

                            if (Sweeps(angle))
                            {
                                GeoPoint3 turn = At(angle);

                                minX = Math.Min(minX, turn.X);
                                minY = Math.Min(minY, turn.Y);
                                minZ = Math.Min(minZ, turn.Z);
                                maxX = Math.Max(maxX, turn.X);
                                maxY = Math.Max(maxY, turn.Y);
                                maxZ = Math.Max(maxZ, turn.Z);
                            }
                        }
                    }
                }

                return new GeoAabb3(new GeoPoint3(minX, minY, minZ), new GeoPoint3(maxX, maxY, maxZ));
            }

            /// <summary>Gets where the arc starts.</summary>
            internal GeoPoint3 StartPoint { get; }

            /// <summary>Gets where the arc ends.</summary>
            internal GeoPoint3 EndPoint { get; }

            /// <summary>Gets whether the arc bows so little that its chord is read for it.</summary>
            internal bool IsChord { get; }

            /// <summary>Gets the box the arc lies in.</summary>
            internal GeoAabb3 Box { get; }

            /// <summary>Gets the centre of the circle the arc lies on.</summary>
            internal GeoPoint3 Centre => new GeoPoint3(_cx, _cy, _cz);

            /// <summary>Reads an arc.</summary>
            internal static Curve Of(GeoArc3 arc) => new Curve(arc);

            /// <summary>Gets the point of the circle at an angle, whether or not the arc sweeps it.</summary>
            private GeoPoint3 At(double angle)
            {
                double c = _radius * Math.Cos(angle);
                double s = _radius * Math.Sin(angle);

                return new GeoPoint3(_cx + c * _ux + s * _vx, _cy + c * _uy + s * _vy, _cz + c * _uz + s * _vz);
            }

            /// <summary>Determines whether the arc sweeps through an angle, with no slack.</summary>
            private bool Sweeps(double angle)
            {
                double into = (angle - _start) * (_sweep < 0.0 ? -1.0 : 1.0);

                into -= 2.0 * Math.PI * Math.Floor(into / (2.0 * Math.PI));

                return into <= Math.Abs(_sweep);
            }

            /// <summary>
            /// Gets the exact distance from a point to the arc: the height over its plane and the distance from its circle where
            /// the point's angle is swept, and the nearer end where it is not.
            /// </summary>
            internal double PointGap(GeoPoint3 point)
            {
                if (IsChord)
                {
                    return PointToSegment(point, StartPoint, EndPoint);
                }

                double wx = point.X - _cx;
                double wy = point.Y - _cy;
                double wz = point.Z - _cz;
                double height = wx * _nx + wy * _ny + wz * _nz;
                double along = wx * _ux + wy * _uy + wz * _uz;
                double across = wx * _vx + wy * _vy + wz * _vz;
                double plan = Math.Sqrt(along * along + across * across);

                if (plan == 0.0)
                {
                    return Math.Sqrt(height * height + _radius * _radius);
                }

                if (_full || Sweeps(Math.Atan2(across, along)))
                {
                    double off = plan - _radius;

                    return Math.Sqrt(height * height + off * off);
                }

                return Math.Min(StartPoint.DistanceTo(point), EndPoint.DistanceTo(point));
            }

            /// <summary>
            /// Gets the exact gap between the arc and a segment, a segment no longer than the point tolerance read as its start
            /// where it does not truly meet the arc.
            /// </summary>
            internal double GapToCallersSegment(GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
            {
                // A segment plainly clear of the circle, or wholly within it, is settled by its distances to the centre.
                double centreGap = LowestToSegment(from, to);

                if (centreGap > tolerance.EqualPoint * (1.0 + 1E-6) + 1E-9)
                {
                    return centreGap;
                }

                double gap = GapToSegment(from, to);

                if (from.GetDistanceSquaredTo(to) <= tolerance.EqualPoint * tolerance.EqualPoint)
                {
                    double rounding = 1E-12 * Math.Max(1.0, Math.Max(Math.Abs(from.X), Math.Max(Math.Abs(from.Y), Math.Abs(from.Z))));

                    return gap <= rounding ? 0.0 : PointGap(from);
                }

                return gap;
            }

            /// <summary>
            /// Gets how near, at the least, a segment can come to the arc: every point of the arc stands a radius from the centre,
            /// so the segment is no nearer than its nearest point to the centre less the radius, nor than the radius less the
            /// farthest it reaches from the centre.
            /// </summary>
            private double LowestToSegment(GeoPoint3 from, GeoPoint3 to)
            {
                double farthest = Math.Max(from.DistanceTo(Centre), to.DistanceTo(Centre));

                return Math.Max(PointToSegment(Centre, from, to) - _radius, _radius - farthest);
            }

            /// <summary>
            /// Gets the exact gap between the arc and a ray: the ray as far as the arc's farthest point, past which it only
            /// draws away.
            /// </summary>
            /// <param name="ray">The ray.</param>
            /// <param name="stopAt">A gap above this may come back as a bound short of the gap; nought asks for the gap.</param>
            internal double GapToRay(GeoRay3 ray, double stopAt)
            {
                double reach = ray.Origin.DistanceTo(Centre) + _radius;
                GeoPoint3 far = Parametrization3.GetPointAtDistance(ray, reach);
                double centreGap = LowestToSegment(ray.Origin, far);

                if (stopAt > 0.0 && centreGap > stopAt * (1.0 + 1E-6) + 1E-9)
                {
                    return centreGap;
                }

                return GapToSegment(ray.Origin, far);
            }

            /// <summary>
            /// Gets the exact gap between the arc and a segment as it is, however short.
            /// </summary>
            internal double GapToSegment(GeoPoint3 from, GeoPoint3 to)
            {
                if (IsChord)
                {
                    return SegmentToSegment(StartPoint, EndPoint, from, to);
                }

                double dx = to.X - from.X;
                double dy = to.Y - from.Y;
                double dz = to.Z - from.Z;
                double squared = dx * dx + dy * dy + dz * dz;

                if (!(squared > 0.0))
                {
                    return PointGap(from);
                }

                double best = Math.Min(
                    Math.Min(PointGap(from), PointGap(to)),
                    Math.Min(PointToSegment(StartPoint, from, to), PointToSegment(EndPoint, from, to)));

                double length = Math.Sqrt(squared);
                double ex = dx / length;
                double ey = dy / length;
                double ez = dz / length;

                // The arc point at an angle, its offset from the line of the segment: w0 + cos U + sin V. The squared offset
                // turns where c1 cos + s1 sin + s2 sin 2t + c2 cos 2t is nought.
                double ax = _cx - from.X;
                double ay = _cy - from.Y;
                double az = _cz - from.Z;
                double along = ax * ex + ay * ey + az * ez;
                double w0x = ax - along * ex;
                double w0y = ay - along * ey;
                double w0z = az - along * ez;

                double us = _ux * ex + _uy * ey + _uz * ez;
                double ux = _radius * (_ux - us * ex);
                double uy = _radius * (_uy - us * ey);
                double uz = _radius * (_uz - us * ez);

                double vs = _vx * ex + _vy * ey + _vz * ez;
                double vx = _radius * (_vx - vs * ex);
                double vy = _radius * (_vy - vs * ey);
                double vz = _radius * (_vz - vs * ez);

                double c1 = vx * w0x + vy * w0y + vz * w0z;
                double s1 = -(ux * w0x + uy * w0y + uz * w0z);
                double s2 = 0.5 * ((vx * vx + vy * vy + vz * vz) - (ux * ux + uy * uy + uz * uz));
                double c2 = ux * vx + uy * vy + uz * vz;

                double scale = _radius * _radius + _radius * Math.Sqrt(w0x * w0x + w0y * w0y + w0z * w0z);

                if (Math.Abs(c1) + Math.Abs(s1) + Math.Abs(s2) + Math.Abs(c2) <= 1E-15 * scale)
                {
                    // The offset from the line does not change along the arc: the segment runs along the axis of its circle.
                    return best;
                }

                var found = new double[64];
                double lowest = Math.Min(_start, _start + _sweep);
                double highest = Math.Max(_start, _start + _sweep);
                var seeds = new double[32];
                int count = Stationary(c1, s1, s2, c2, lowest, highest, found, seeds, out int seedCount);

                for (int i = 0; i < count; i++)
                {
                    best = Math.Min(best, PointToSegment(At(found[i]), from, to));
                }

                // A root the slope of the polynomial barely leaves, a double or a triple one, is known only loosely, and a step of
                // Newton's can carry it away; the distance itself is minimised round it, which cannot lose what was there.
                for (int i = 0; i < seedCount; i++)
                {
                    best = Math.Min(best, NearestRound(from, to, seeds[i], lowest, highest));
                }

                return best;
            }

            /// <summary>
            /// Gets the least distance from the points of the arc within a hundredth of a radian of an angle, and between the limits,
            /// to a segment, by golden section.
            /// </summary>
            private double NearestRound(GeoPoint3 from, GeoPoint3 to, double centre, double low, double high)
            {
                double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;
                double a = Math.Max(low, centre - 0.01);
                double b = Math.Min(high, centre + 0.01);
                double x1 = b - ratio * (b - a);
                double x2 = a + ratio * (b - a);
                double f1 = PointToSegment(At(x1), from, to);
                double f2 = PointToSegment(At(x2), from, to);
                double best = Math.Min(Math.Min(f1, f2), PointToSegment(At(centre < a ? a : centre > b ? b : centre), from, to));

                for (int i = 0; i < 70 && b - a > 1E-10; i++)
                {
                    if (f1 < f2)
                    {
                        b = x2;
                        x2 = x1;
                        f2 = f1;
                        x1 = b - ratio * (b - a);
                        f1 = PointToSegment(At(x1), from, to);
                        best = Math.Min(best, f1);
                    }
                    else
                    {
                        a = x1;
                        x1 = x2;
                        f1 = f2;
                        x2 = a + ratio * (b - a);
                        f2 = PointToSegment(At(x2), from, to);
                        best = Math.Min(best, f2);
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the exact gap between the arc and a flat region.
            /// </summary>
            /// <param name="region">The region.</param>
            /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
            internal double GapToRegion(Region region, double stopAt)
            {
                if (IsChord)
                {
                    return Segment(region, StartPoint, EndPoint, false, Exactly, stopAt);
                }

                // The height of a point of the circle over the plane of the region is h0 + r (a cos + b sin), within amplitude of h0.
                GeoVector3 normal = region.UnitNormal;
                GeoPoint3 origin = region.PlaneOrigin;
                double a = normal.X * _ux + normal.Y * _uy + normal.Z * _uz;
                double b = normal.X * _vx + normal.Y * _vy + normal.Z * _vz;
                double h0 = normal.X * (_cx - origin.X) + normal.Y * (_cy - origin.Y) + normal.Z * (_cz - origin.Z);
                double amplitude = _radius * Math.Sqrt(a * a + b * b);
                double limit = stopAt * (1.0 + 1E-6) + 1E-9;

                // An arc that stays further over the plane than the region's corners stand off it, and the limit, is clear of it.
                if (stopAt > 0.0)
                {
                    double lowest = Math.Max(0.0, Math.Abs(h0) - amplitude) - region.PlaneSlack;

                    if (lowest > limit)
                    {
                        return lowest;
                    }

                    // Nor is any point of the arc further than the radius from its centre, so the region is no nearer than that less the radius.
                    double centreGap = region.PointGap(Centre) - _radius;

                    if (centreGap > limit)
                    {
                        return centreGap;
                    }
                }

                double best = Math.Min(region.PointGap(StartPoint), region.PointGap(EndPoint));

                if (best <= stopAt)
                {
                    return best;
                }

                // Where the arc crosses the plane of the region, and where it comes nearest and farthest over it.
                double turning = Math.Atan2(b, a);

                best = Math.Min(best, Candidate(region, turning));
                best = Math.Min(best, Candidate(region, turning + Math.PI));

                if (amplitude > 0.0 && Math.Abs(h0) <= amplitude)
                {
                    double spread = Math.Acos(-h0 / amplitude);

                    best = Math.Min(best, Candidate(region, turning + spread));
                    best = Math.Min(best, Candidate(region, turning - spread));
                }

                if (best <= stopAt)
                {
                    return best;
                }

                for (int i = 0; i < region.EdgeCount; i++)
                {
                    GeoPoint3 from = region.EdgeFrom(i);
                    GeoPoint3 to = region.EdgeTo(i);

                    if (Beyond(BoxGap(from, to, Box), best) || Beyond(LowestToSegment(from, to), best))
                    {
                        continue;
                    }

                    best = Math.Min(best, GapToSegment(from, to));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the distance from the point of the circle at an angle to a region, the point being on the arc; infinite where
            /// the arc does not sweep that far.
            /// </summary>
            private double Candidate(Region region, double angle)
                => Sweeps(angle) ? region.PointGap(At(angle)) : double.MaxValue;

            /// <summary>
            /// Gets the exact gap between the arc and the surface of a body: the least over its faces, a face read by its material.
            /// </summary>
            /// <param name="material">The body, its openings already cut in.</param>
            /// <param name="within">A face whose box stands further than this from the arc's is passed over.</param>
            /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
            internal double GapToSurface(GeoSolid3 material, double within, double stopAt)
            {
                IReadOnlyList<GeoFace3> faces = material.Faces;
                var reach = new double[faces.Count];

                for (int i = 0; i < reach.Length; i++)
                {
                    reach[i] = Math.Max(0.0, Box.DistanceTo(faces[i].GetAabb()) - PlanarSlack);
                }

                double best = double.MaxValue;

                foreach (int i in Nearest(reach))
                {
                    if (reach[i] > within || Beyond(reach[i], best))
                    {
                        continue;
                    }

                    best = Math.Min(best, GapToRegion(material.RegionOf(i), stopAt));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the exact gap between the arc and a box read as its body: nought where the box holds an end of the arc, and
            /// otherwise the least over its six faces, each made straight from the corners.
            /// </summary>
            /// <param name="block">The box.</param>
            /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
            internal double GapToBlock(Block block, double stopAt)
            {
                // No point of the arc is further than the radius from its centre, so the box is no nearer than that less the radius.
                double centreGap = block.Distance(Centre) - _radius;

                if (stopAt > 0.0 && centreGap > stopAt * (1.0 + 1E-6) + 1E-9)
                {
                    return centreGap;
                }

                if (block.Holds(StartPoint) || block.Holds(EndPoint))
                {
                    return 0.0;
                }

                double best = double.MaxValue;

                foreach (Region face in block.Faces())
                {
                    best = Math.Min(best, GapToRegion(face, stopAt));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            /// <summary>
            /// Determines whether a body holds the arc's end, which an arc lying wholly inside it does without touching a face.
            /// </summary>
            internal bool HeldBy(GeoSolid3 solid) => Holds(solid, StartPoint) || Holds(solid, EndPoint);

            /// <summary>
            /// Gets the exact gap between the arc and another arc.
            /// </summary>
            /// <param name="other">The other arc.</param>
            /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
            internal double GapToCurve(Curve other, double stopAt)
            {
                if (IsChord)
                {
                    return other.GapToSegment(StartPoint, EndPoint);
                }

                if (other.IsChord)
                {
                    return GapToSegment(other.StartPoint, other.EndPoint);
                }

                double between = Centre.DistanceTo(other.Centre) - _radius - other._radius;

                if (stopAt > 0.0 && between > stopAt * (1.0 + 1E-6) + 1E-9 * Math.Max(1.0, _radius + other._radius))
                {
                    return between;
                }

                double best = Math.Min(
                    Math.Min(PointGap(other.StartPoint), PointGap(other.EndPoint)),
                    Math.Min(other.PointGap(StartPoint), other.PointGap(EndPoint)));

                if (best <= stopAt)
                {
                    return best;
                }

                if (IsCoaxialWith(other))
                {
                    // Circles on one axis in parallel planes stand the same distance apart at every angle, so where the arcs share an
                    // angle an end of one is held by the other, and the ends above are the answer.
                    return best;
                }

                if (stopAt > 0.0 && CoaxialCopy(other, out Curve copy, out double shift))
                {
                    // The other arc is never further than shift from its copy on this arc's axis, whose distance the ends decide, so
                    // this one is no nearer than the copy less that.
                    double nearest = Math.Min(
                        Math.Min(PointGap(copy.StartPoint), PointGap(copy.EndPoint)),
                        Math.Min(copy.PointGap(StartPoint), copy.PointGap(EndPoint)));

                    if (nearest - shift > stopAt * (1.0 + 1E-6) + 1E-9)
                    {
                        return nearest - shift;
                    }
                }

                if (stopAt > 0.0)
                {
                    int verdict = Reaches(other, stopAt, out double found);

                    if (verdict != Unknown)
                    {
                        // Clear: every stretch of this arc is proved further than that from the other, and what comes back is a bound
                        // above what was asked. Within: a point of it was found, and what comes back is its distance.
                        return found;
                    }
                }

                // Asked for the gap itself, or the proof outran its budget: the walk of the arcs decides.
                best = Math.Min(best, Walk(other, stopAt));

                return best <= stopAt ? best : Math.Min(best, other.Walk(this, stopAt));
            }

            /// <summary>
            /// Makes the copy of another arc that stands on this arc's axis, turned to lie in a plane parallel to this arc's, when the
            /// two stand nearly so, and gets how far any point of the other is from its place on the copy.
            /// </summary>
            /// <remarks>
            /// The copy is the other arc turned about its centre until its normal is this arc's (or its opposite), then moved across
            /// to the axis, so each of its points stands from the point of the other no further than the chord of the turn at its
            /// radius, 2 r sin(a / 2), and the move. The copy is on one axis with this arc in parallel planes, whose distance the ends
            /// decide. It is made only when the shift is a quarter of the radii or less, where the bound is worth having.
            /// </remarks>
            private bool CoaxialCopy(Curve other, out Curve copy, out double shift)
            {
                copy = null;
                shift = 0.0;

                double dot = _nx * other._nx + _ny * other._ny + _nz * other._nz;
                double sign = dot >= 0.0 ? 1.0 : -1.0;
                double mx = sign * _nx;
                double my = sign * _ny;
                double mz = sign * _nz;

                // The turn taking the other's normal to the copy's normal m.
                double kx = other._ny * mz - other._nz * my;
                double ky = other._nz * mx - other._nx * mz;
                double kz = other._nx * my - other._ny * mx;
                double sine = Math.Sqrt(kx * kx + ky * ky + kz * kz);
                double cosine = other._nx * mx + other._ny * my + other._nz * mz;
                double turn = Math.Atan2(sine, cosine);

                // The other's centre moved across to this arc's axis.
                double dx = other._cx - _cx;
                double dy = other._cy - _cy;
                double dz = other._cz - _cz;
                double along = dx * _nx + dy * _ny + dz * _nz;
                var centre = new GeoPoint3(_cx + along * _nx, _cy + along * _ny, _cz + along * _nz);
                double across = Math.Sqrt((dx - along * _nx) * (dx - along * _nx) + (dy - along * _ny) * (dy - along * _ny) + (dz - along * _nz) * (dz - along * _nz));

                shift = across + other._radius * 2.0 * Math.Sin(0.5 * turn);

                if (!(shift <= 0.25 * (_radius + other._radius)) || other.IsChord)
                {
                    return false;
                }

                // The other's start, turned with it, seen from the copy's centre, in the copy's own axes.
                double sx = other.StartPoint.X - other._cx;
                double sy = other.StartPoint.Y - other._cy;
                double sz = other.StartPoint.Z - other._cz;

                if (sine > 0.0)
                {
                    kx /= sine;
                    ky /= sine;
                    kz /= sine;

                    double kDotS = kx * sx + ky * sy + kz * sz;
                    double cx = ky * sz - kz * sy;
                    double cy = kz * sx - kx * sz;
                    double cz = kx * sy - ky * sx;

                    double tx = sx * cosine + cx * sine + kx * kDotS * (1.0 - cosine);
                    double ty = sy * cosine + cy * sine + ky * kDotS * (1.0 - cosine);
                    double tz = sz * cosine + cz * sine + kz * kDotS * (1.0 - cosine);

                    sx = tx;
                    sy = ty;
                    sz = tz;
                }

                var normal = new GeoVector3(mx, my, mz);
                new GeoPlane3(centre, normal).GetAxes(out GeoVector3 u, out GeoVector3 v);
                double start = Math.Atan2(sx * v.X + sy * v.Y + sz * v.Z, sx * u.X + sy * u.Y + sz * u.Z);

                copy = new Curve(centre, normal, other._radius, start, other._sweep);

                return !copy.IsChord;
            }

            private const int Clear = 0;
            private const int Within = 1;
            private const int Unknown = 2;

            /// <summary>
            /// Determines whether two circles stand on one axis in parallel planes, to within rounding.
            /// </summary>
            private bool IsCoaxialWith(Curve other)
            {
                double nx = _ny * other._nz - _nz * other._ny;
                double ny = _nz * other._nx - _nx * other._nz;
                double nz = _nx * other._ny - _ny * other._nx;
                double dx = other._cx - _cx;
                double dy = other._cy - _cy;
                double dz = other._cz - _cz;
                double along = dx * _nx + dy * _ny + dz * _nz;
                double ox = dx - along * _nx;
                double oy = dy - along * _ny;
                double oz = dz - along * _nz;
                double reach = _radius + other._radius + Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);

                return Math.Sqrt(nx * nx + ny * ny + nz * nz) <= 1E-14 && Math.Sqrt(ox * ox + oy * oy + oz * oz) <= 1E-13 * reach;
            }

            /// <summary>
            /// Decides whether this arc comes within a distance of another arc: clear when every stretch of it is proved further
            /// than that, within when a point of it is found no further, unknown when the proof outruns its budget.
            /// </summary>
            /// <param name="other">The other arc.</param>
            /// <param name="stopAt">The distance.</param>
            /// <param name="found">
            /// When clear, the least of the bounds that proved the stretches clear, above the distance asked; when within, a
            /// distance no more than the one asked.
            /// </param>
            /// <remarks>
            /// The distance from a point of this arc to the other changes by no more than the length moved, so a stretch of width w
            /// whose ends stand f1 and f2 from the other arc is no nearer than (f1 + f2 - radius w) / 2. A stretch not proved clear
            /// is halved until the radius times its width is a millionth of the distance asked, and the least over what is left is
            /// found by golden section. So the answer is a proof where it comes. The one pair of arcs it cannot settle in the
            /// budget is two partial arcs whose distance stays near-constant just beyond the one asked, which takes a stretch as
            /// small as a hundred-thousandth of the circle; the copy on the axis (<see cref="CoaxialCopy"/>) is tried first, and
            /// only if that does not prove it clear is the pair searched by the walk of the arcs, not proved.
            /// </remarks>
            private int Reaches(Curve other, double stopAt, out double found)
            {
                double limit = stopAt * (1.0 + 1E-6) + 1E-12;
                var state = new Proof { Budget = 500, Bound = double.MaxValue, Lipschitz = other._full ? LipschitzTo(other) : _radius };
                const int pieces = 16;
                double step = _sweep / pieces;
                double from = other.PointGap(At(_start));

                found = double.MaxValue;

                for (int i = 0; i < pieces; i++)
                {
                    double to = other.PointGap(At(_start + step * (i + 1)));

                    if (from <= stopAt || to <= stopAt)
                    {
                        found = Math.Min(from, to);
                        return Within;
                    }

                    int verdict = Stretch(other, _start + step * i, _start + step * (i + 1), from, to, stopAt, limit, state);

                    if (verdict == Within)
                    {
                        found = state.Found;
                        return Within;
                    }

                    if (verdict == Unknown)
                    {
                        return Unknown;
                    }

                    from = to;
                }

                found = state.Bound;
                return Clear;
            }

            /// <summary>
            /// Gets how fast the distance from a point of this arc to a whole circle can change per radian: no more than the radius,
            /// and far less where the circles lie nearly on one axis.
            /// </summary>
            /// <remarks>
            /// The distance to a circle has a gradient of unit length, along (h n + (q - r) e) / d for the height h over the
            /// circle's plane, the distance q from its axis, the unit direction e out from the axis and the distance d. The point
            /// moves along the tangent t of this circle, so the rate is the radius times (h (n.t) + (q - r) (e.t)) / d, which is
            /// no more than the radius times the root of (n.t) squared and (e.t) squared. Here n.t is no more than the sine of
            /// the angle between the planes, and e.t is the distance between the centres, less the height times n.t, over q,
            /// which is at least the radius times the cosine of that angle, less the distance between the centres.
            /// </remarks>
            private double LipschitzTo(Curve other)
            {
                double crossX = _ny * other._nz - _nz * other._ny;
                double crossY = _nz * other._nx - _nx * other._nz;
                double crossZ = _nx * other._ny - _ny * other._nx;
                double sine = Math.Sqrt(crossX * crossX + crossY * crossY + crossZ * crossZ);
                double cosine = Math.Abs(_nx * other._nx + _ny * other._ny + _nz * other._nz);
                double dx = other._cx - _cx;
                double dy = other._cy - _cy;
                double dz = other._cz - _cz;
                double between = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                double nearest = _radius * cosine - between;

                if (!(nearest > 0.0))
                {
                    return _radius;
                }

                double height = between + _radius * sine;
                double across = (between + height * sine) / nearest;

                return _radius * Math.Min(1.0, Math.Sqrt(sine * sine + across * across));
            }

            private sealed class Proof
            {
                internal int Budget;
                internal double Bound;
                internal double Found;
                internal double Lipschitz;
            }

            private int Stretch(Curve other, double a, double b, double fa, double fb, double stopAt, double limit, Proof state)
            {
                double width = Math.Abs(b - a);
                double low = 0.5 * (fa + fb - state.Lipschitz * width);

                if (low > limit)
                {
                    state.Bound = Math.Min(state.Bound, low);
                    return Clear;
                }

                if (state.Lipschitz * width <= 1E-6 * stopAt)
                {
                    double least = Polish(other, Math.Min(a, b), Math.Max(a, b));

                    if (least <= stopAt)
                    {
                        state.Found = least;
                        return Within;
                    }

                    state.Bound = Math.Min(state.Bound, least);
                    return Clear;
                }

                if (--state.Budget < 0)
                {
                    return Unknown;
                }

                double middle = 0.5 * (a + b);
                double fm = other.PointGap(At(middle));

                if (fm <= stopAt)
                {
                    state.Found = fm;
                    return Within;
                }

                int first = Stretch(other, a, middle, fa, fm, stopAt, limit, state);

                return first != Clear ? first : Stretch(other, middle, b, fm, fb, stopAt, limit, state);
            }

            /// <summary>
            /// Walks this arc and polishes every low point of its distance to another, the distance from a point of the arc to the
            /// other arc being exact.
            /// </summary>
            private double Walk(Curve other, double stopAt)
            {
                int steps = Math.Max(24, Math.Min(900, (int)Math.Ceiling(Math.Abs(_sweep) / (stopAt > 0.0 ? 0.03 : 0.01))));
                double step = _sweep / steps;
                var at = new double[steps + 1];

                double best = double.MaxValue;

                for (int i = 0; i <= steps; i++)
                {
                    at[i] = other.PointGap(At(_start + step * i));
                    best = Math.Min(best, at[i]);
                }

                if (best <= stopAt)
                {
                    return best;
                }

                // The lowest points of the walk, in order of height with the lower index first, at most eight: the distance
                // from a circle to a circle has no more minima than that.
                var lows = new List<int>();

                for (int i = 0; i <= steps; i++)
                {
                    double left = i > 0 ? at[i - 1] : double.MaxValue;
                    double right = i < steps ? at[i + 1] : double.MaxValue;

                    if (at[i] <= left && at[i] <= right && (at[i] < left || at[i] < right || steps == 0))
                    {
                        lows.Add(i);
                    }
                }

                lows.Sort((p, q) =>
                {
                    int by = at[p].CompareTo(at[q]);

                    return by != 0 ? by : p.CompareTo(q);
                });

                for (int n = 0; n < Math.Min(8, lows.Count); n++)
                {
                    int i = lows[n];
                    double low = _start + step * Math.Max(0, i - 1);
                    double high = _start + step * Math.Min(steps, i + 1);

                    best = Math.Min(best, Polish(other, low, high));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the least distance from the points of this arc between two angles to another arc, by golden section.
            /// </summary>
            private double Polish(Curve other, double low, double high)
            {
                double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;
                double x1 = high - ratio * (high - low);
                double x2 = low + ratio * (high - low);
                double f1 = other.PointGap(At(x1));
                double f2 = other.PointGap(At(x2));
                double best = Math.Min(f1, f2);

                for (int i = 0; i < 90 && Math.Abs(high - low) > 1E-14; i++)
                {
                    if (f1 < f2)
                    {
                        high = x2;
                        x2 = x1;
                        f2 = f1;
                        x1 = high - ratio * (high - low);
                        f1 = other.PointGap(At(x1));
                        best = Math.Min(best, f1);
                    }
                    else
                    {
                        low = x1;
                        x1 = x2;
                        f1 = f2;
                        x2 = low + ratio * (high - low);
                        f2 = other.PointGap(At(x2));
                        best = Math.Min(best, f2);
                    }
                }

                return best;
            }
        }

        /// <summary>
        /// Finds the angles between two limits where c1 cos t + s1 sin t + s2 sin 2t + c2 cos 2t is nought, and the places
        /// beside them where it nearly is.
        /// </summary>
        /// <remarks>
        /// With z = tan(t / 2) the polynomial becomes a quartic: (c2 - c1) z^4 + (2 s1 - 4 s2) z^3 - 6 c2 z^2 + (2 s1 + 4 s2) z +
        /// (c1 + c2). Its real roots are found between the roots of its derivative, which are found between the roots of the
        /// derivative of that, so none is missed and a pair standing close together is told apart. The turning points of the
        /// quartic come with them: where a pair of roots has just left the real line, its real part is such a point, and the
        /// distance there is a real distance all the same. The angle half a turn round, where z is infinite, is asked too. Every
        /// angle is kept as it came and as Newton's steps on the polynomial in t itself polish it, since both are real
        /// distances; and a root the polynomial barely leaves the nought at, a double or a triple one, comes back as a seed
        /// for the caller to minimise the distance itself round.
        /// </remarks>
        private static int Stationary(
            double c1, double s1, double s2, double c2, double low, double high, double[] found, double[] seeds, out int seedCount)
        {
            seedCount = 0;

            // z is infinite half a turn from the origin the angle is measured from, where the quartic is known loosely. Measured
            // from the middle of the sweep that is outside an arc of up to a half turn, so no root of the arc's lies near it; a
            // longer arc is solved again from a quarter turn on, where the roots that stood there are well away from it.
            double middle = 0.5 * (low + high);
            int count = Quartic(c1, s1, s2, c2, middle, low, high, found, 0, seeds, ref seedCount, out bool nearSingular);

            if (nearSingular && high - low > Math.PI)
            {
                count = Quartic(c1, s1, s2, c2, middle + 0.5 * Math.PI, low, high, found, count, seeds, ref seedCount, out nearSingular);
            }

            return count;
        }

        /// <summary>
        /// Solves the quartic for the angle measured from an origin, and adds the candidates it gives between the limits.
        /// </summary>
        private static int Quartic(
            double c1, double s1, double s2, double c2, double origin, double low, double high, double[] found, int count, double[] seeds, ref int seedCount, out bool nearSingular)
        {
            // c1 cos t + s1 sin t + s2 sin 2t + c2 cos 2t for t = origin + p: the same form in p, its coefficients turned.
            double cosOrigin = Math.Cos(origin);
            double sinOrigin = Math.Sin(origin);
            double cosTwice = Math.Cos(2.0 * origin);
            double sinTwice = Math.Sin(2.0 * origin);
            double d1 = c1 * cosOrigin + s1 * sinOrigin;
            double e1 = -c1 * sinOrigin + s1 * cosOrigin;
            double d2 = c2 * cosTwice + s2 * sinTwice;
            double e2 = -c2 * sinTwice + s2 * cosTwice;

            var a = new[] { d1 + d2, 2.0 * e1 + 4.0 * e2, -6.0 * d2, 2.0 * e1 - 4.0 * e2, d2 - d1 };
            double biggest = Math.Max(Math.Max(Math.Abs(a[0]), Math.Abs(a[1])), Math.Max(Math.Max(Math.Abs(a[2]), Math.Abs(a[3])), Math.Abs(a[4])));

            nearSingular = false;

            if (!(biggest > 0.0))
            {
                return count;
            }

            int degree = 4;

            while (degree > 0 && Math.Abs(a[degree]) <= 1E-14 * biggest)
            {
                degree--;
            }

            var zs = new double[8];
            int rootCount = RealRoots(a, degree, zs, out double[] turning, out int turns);
            int zc = rootCount;

            for (int i = 0; i < turns && zc < zs.Length; i++)
            {
                zs[zc++] = turning[i];
            }

            // Whether a root, or a turning point, or the leading coefficient's smallness puts something within some 30 degrees of
            // where z is infinite: only then is a second solve from another origin worth its cost.
            nearSingular = degree < 4 || Math.Abs(a[4]) < 1E-3 * biggest;

            for (int i = 0; i < zc && !nearSingular; i++)
            {
                nearSingular = Math.Abs(zs[i]) > 4.0;
            }

            for (int i = 0; i <= zc; i++)
            {
                double angle = origin + (i < zc ? 2.0 * Math.Atan(zs[i]) : Math.PI);

                for (int turn = -2; turn <= 2 && count < found.Length; turn++)
                {
                    double at = angle + turn * 2.0 * Math.PI;

                    if (at >= low && at <= high)
                    {
                        // The angle as the quartic gave it stays a candidate beside the polished one: each is a real distance, and a
                        // step of Newton's beside a multiple root can carry the polished one away from what the quartic had right.
                        bool isRoot = i < rootCount || i == zc;
                        double polished = Refined(c1, s1, s2, c2, at, low, high, i < rootCount);
                        bool settled = polished >= low && polished <= high;

                        // A root that settled past an end of the arc is none of the arc's: its end is weighed already. One that moved a
                        // billionth or less is the same candidate twice.
                        if (!settled || Math.Abs(polished - at) > 1E-9)
                        {
                            found[count++] = at;
                        }

                        if (settled && count < found.Length)
                        {
                            found[count++] = polished;
                        }

                        // A root the slope barely leaves, or one far out where z is large and the quartic is known loosely, is minimised
                        // round on the distance itself, whatever its slope.
                        if (isRoot && seedCount < seeds.Length
                            && ((i < rootCount && Math.Abs(zs[i]) > 1E3)
                                || Math.Abs(Slope(c1, s1, s2, c2, polished)) <= 1E-6 * (Math.Abs(c1) + Math.Abs(s1) + 2.0 * Math.Abs(s2) + 2.0 * Math.Abs(c2))))
                        {
                            seeds[seedCount++] = polished >= low && polished <= high ? polished : at;
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Moves an angle toward the nearest root of the polynomial by a few Newton steps, keeping it between the limits and
        /// leaving it where a step would carry it far; where it stands for a root, and the steps do not settle it, the nearest
        /// place round it where the polynomial changes sign.
        /// </summary>
        private static double Refined(double c1, double s1, double s2, double c2, double angle, double low, double high, bool root)
        {
            for (int i = 0; i < 4; i++)
            {
                double sine = Math.Sin(angle);
                double cosine = Math.Cos(angle);
                double twice = cosine * cosine - sine * sine;
                double value = c1 * cosine + s1 * sine + s2 * (2.0 * sine * cosine) + c2 * twice;
                double slope = -c1 * sine + s1 * cosine + 2.0 * s2 * twice - 4.0 * c2 * sine * cosine;

                if (value == 0.0 || slope == 0.0)
                {
                    break;
                }

                double step = value / slope;

                if (!(Math.Abs(step) < 0.05) || angle - step < low || angle - step > high)
                {
                    break;
                }

                angle -= step;
            }

            if (!root)
            {
                return angle;
            }

            double k = Math.Abs(c1) + Math.Abs(s1) + 2.0 * Math.Abs(s2) + 2.0 * Math.Abs(c2);
            double atAngle = Trigonometric(c1, s1, s2, c2, angle);

            if (Math.Abs(atAngle) <= 1E-12 * k)
            {
                return angle;
            }

            // Newton's steps did not settle it, as they do not beside a cluster of roots, where the quartic itself is known only
            // loosely: the nearest place round it where the polynomial changes sign holds the root.
            for (double delta = 1E-7; delta <= 0.1; delta *= 3.0)
            {
                double above = Trigonometric(c1, s1, s2, c2, angle + delta);

                if (above * atAngle <= 0.0)
                {
                    return Newton(c1, s1, s2, c2, angle, angle + delta, atAngle);
                }

                double below = Trigonometric(c1, s1, s2, c2, angle - delta);

                if (below * atAngle <= 0.0)
                {
                    return Newton(c1, s1, s2, c2, angle - delta, angle, below);
                }
            }

            return angle;
        }

        /// <summary>
        /// Finds the real roots of a polynomial, in order, and those of its derivative.
        /// </summary>
        /// <param name="a">The coefficients, constant first.</param>
        /// <param name="degree">The degree; the coefficient of it is not nought.</param>
        /// <param name="roots">Receives the roots; room for the degree.</param>
        /// <param name="turning">Receives the real roots of the derivative.</param>
        /// <param name="turns">The number of those.</param>
        /// <returns>The number of roots.</returns>
        private static int RealRoots(double[] a, int degree, double[] roots, out double[] turning, out int turns)
        {
            turning = new double[4];
            turns = 0;

            if (degree <= 0)
            {
                return 0;
            }

            if (degree == 1)
            {
                roots[0] = -a[0] / a[1];
                return 1;
            }

            if (degree == 2)
            {
                turning[0] = -a[1] / (2.0 * a[2]);
                turns = 1;

                return Quadratic(a[2], a[1], a[0], roots);
            }

            var derivative = new double[degree];

            for (int i = 1; i <= degree; i++)
            {
                derivative[i - 1] = i * a[i];
            }

            turns = RealRoots(derivative, degree - 1, turning, out double[] unused, out int none);

            // No root lies beyond a bound of the coefficients; between the turning points the polynomial is monotone, so it has a
            // root in a stretch exactly where its ends stand either side of nought.
            double bound = 1.0;

            for (int i = 0; i < degree; i++)
            {
                bound = Math.Max(bound, 1.0 + Math.Abs(a[i] / a[degree]));
            }

            int count = 0;
            double previous = -bound;
            double atPrevious = Horner(a, degree, previous, out double slope);

            for (int k = 0; k <= turns; k++)
            {
                double next = k < turns ? turning[k] : bound;
                double atNext = Horner(a, degree, next, out slope);

                if (atPrevious * atNext < 0.0)
                {
                    roots[count++] = Bracketed(a, degree, previous, next, atPrevious);
                }
                else if (atNext == 0.0 && k < turns)
                {
                    roots[count++] = next;
                }

                previous = next;
                atPrevious = atNext;
            }

            return count;
        }

        private static int Quadratic(double a, double b, double c, double[] roots)
        {
            double discriminant = b * b - 4.0 * a * c;

            if (discriminant < 0.0)
            {
                return 0;
            }

            double q = -0.5 * (b + (b < 0.0 ? -Math.Sqrt(discriminant) : Math.Sqrt(discriminant)));
            double first = q == 0.0 ? 0.0 : q / a;
            double second = q == 0.0 ? 0.0 : c / q;

            roots[0] = Math.Min(first, second);
            roots[1] = Math.Max(first, second);

            return 2;
        }

        private static double Horner(double[] a, int degree, double x, out double slope)
        {
            double value = a[degree];
            slope = 0.0;

            for (int i = degree - 1; i >= 0; i--)
            {
                slope = slope * x + value;
                value = value * x + a[i];
            }

            return value;
        }

        /// <summary>
        /// Gets the root of the polynomial between two points where it changes sign, by Newton's steps that never leave the
        /// bracket: a step that would is a bisection instead.
        /// </summary>
        private static double Bracketed(double[] a, int degree, double low, double high, double atLow)
        {
            double x = 0.5 * (low + high);

            for (int i = 0; i < 200; i++)
            {
                double value = Horner(a, degree, x, out double slope);

                if (value == 0.0)
                {
                    return x;
                }

                if (value * atLow > 0.0)
                {
                    low = x;
                }
                else
                {
                    high = x;
                }

                double next = x - value / slope;

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (Math.Abs(next - x) <= 1E-16 * Math.Max(1.0, Math.Abs(x)))
                {
                    return next;
                }

                x = next;
            }

            return x;
        }

        /// <summary>
        /// Gets the root of the polynomial between two angles where it changes sign, by Newton's steps that never leave the
        /// bracket: a step that would is a bisection instead.
        /// </summary>
        private static double Newton(double c1, double s1, double s2, double c2, double low, double high, double atLow)
        {
            double x = 0.5 * (low + high);

            for (int i = 0; i < 60; i++)
            {
                double sine = Math.Sin(x);
                double cosine = Math.Cos(x);
                double twice = cosine * cosine - sine * sine;
                double value = c1 * cosine + s1 * sine + s2 * (2.0 * sine * cosine) + c2 * twice;

                if (value == 0.0)
                {
                    return x;
                }

                if (value * atLow > 0.0)
                {
                    low = x;
                }
                else
                {
                    high = x;
                }

                double slope = -c1 * sine + s1 * cosine + 2.0 * s2 * twice - 4.0 * c2 * sine * cosine;
                double next = x - value / slope;

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (Math.Abs(next - x) <= 1E-15 * Math.Max(1.0, Math.Abs(x)))
                {
                    return next;
                }

                x = next;
            }

            return x;
        }

        private static double Slope(double c1, double s1, double s2, double c2, double angle)
        {
            double sine = Math.Sin(angle);
            double cosine = Math.Cos(angle);

            return -c1 * sine + s1 * cosine + 2.0 * s2 * (cosine * cosine - sine * sine) - 4.0 * c2 * sine * cosine;
        }

        private static double Trigonometric(double c1, double s1, double s2, double c2, double angle)
        {
            double sine = Math.Sin(angle);
            double cosine = Math.Cos(angle);

            return c1 * cosine + s1 * sine + s2 * (2.0 * sine * cosine) + c2 * (cosine * cosine - sine * sine);
        }
    }
}
