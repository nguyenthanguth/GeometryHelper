using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The volume of a body's material within some bodies and outside others, worked out by slicing it level by level,
    /// with no body built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A level plane cuts each body in a section, and the volume is the area of the section added up the height. Between
    /// two heights where nothing happens, no corner of a body, no edge of one body through a face of another and no three
    /// faces of three bodies meeting, every corner of the section moves along a straight line, so its area is a
    /// polynomial of the second degree in the height. Milne's rule, the sections at a quarter, a half and three quarters
    /// of the layer weighed 2/3, -1/3 and 2/3, then adds the layer up exactly: a box 30 by 20 by 10 turned about two axes
    /// comes out 6 000 to the rounding.
    /// </para>
    /// <para>
    /// A section's area is swept across in x. Between two x where nothing happens, the end of a segment of a section or
    /// two segments crossing, the length of the material along a line of constant x is linear in x, so the line through
    /// the middle of the strip gives the strip exactly. Along that line the inside of each body is read by the segments
    /// it crosses, odd and even, so the way the faces are wound does not matter. A section crossed an odd number of
    /// times gives no volume: the two faces of an edge must cross a level at the same point, and a corner that sits
    /// more than about 1E-9 off the edge of its neighbour, a T-junction, leaves a gap in the section that a line through
    /// it crosses once. <see cref="GeoSolid3.Validate(Tolerance)"/>, which reads within a tolerance, may accept such a
    /// body all the same.
    /// </para>
    /// <para>
    /// The bodies are sliced across the axis whose sections cost the least, z where two cost the same, then x, then y,
    /// the coordinates swapped whole so that the volume is the one the swapped bodies give along z, to the bit. A wall
    /// and a curved girder of a Tekla model, sliced in 0.31 to 0.36 seconds across z, take 0.03 to 0.04 across x; a slab
    /// 6 000 by 6 000 by 200 sloped 5 degrees with 200 round openings scattered over it, 29 seconds across z and 0.37
    /// across x.
    /// </para>
    /// <para>
    /// The material of a body is the body less its openings; the openings' own openings are not read. Nothing is snapped
    /// to a tolerance, so a shell 0.02 thick is counted as one. The coordinates are moved to the subject's least corner
    /// first, so that a part some hundreds of metres from the origin loses no digits. Nothing throws: inconsistent faces,
    /// or a number that is not finite, give false.
    /// </para>
    /// </remarks>
    internal static class Slice3
    {
        // Two levels nearer than this are one: a layer under a millionth of a millimetre thick is not sliced.
        private const double LevelMerge = 1E-6;

        // Two x nearer than this bound one strip.
        private const double StripMerge = 1E-9;

        // How far outside the region, or outside a face's box, a corner, a crossing or a meeting of three faces may lie and
        // still give a level. A level too many costs a little time; a level missed costs exactness.
        private const double Reach = 1E-3;

        private static readonly double[] MilneAt = { 0.25, 0.5, 0.75 };

        private static readonly double[] MilneWeight = { 2.0 / 3.0, -1.0 / 3.0, 2.0 / 3.0 };

        /// <summary>
        /// The volume of the subject's material that lies within the material of at least one body of a list and within
        /// that of none of another.
        /// </summary>
        /// <param name="subject">The body measured.</param>
        /// <param name="within">The bodies the volume must lie within one of; none gives nought.</param>
        /// <param name="outside">The bodies the volume must lie outside all of.</param>
        /// <param name="volume">The volume; nought where the method returns false.</param>
        /// <returns>
        /// true when the volume could be worked out; false where a body is null, a section is crossed an odd number of
        /// times, or a number is not finite.
        /// </returns>
        internal static bool TryVolume(GeoSolid3 subject, IReadOnlyList<GeoSolid3> within, IReadOnlyList<GeoSolid3> outside, out double volume)
        {
            volume = 0.0;

            if (subject == null || within == null || outside == null)
            {
                return false;
            }

            var bodies = new List<GeoSolid3> { subject };
            var roles = new List<Role> { Role.Subject };

            foreach (GeoSolid3 body in within)
            {
                bodies.Add(body);
                roles.Add(Role.Within);
            }

            foreach (GeoSolid3 body in outside)
            {
                bodies.Add(body);
                roles.Add(Role.Outside);
            }

            foreach (GeoSolid3 body in bodies)
            {
                if (body == null || !Finite(body.GetAabb()))
                {
                    return false;
                }

                foreach (GeoSolid3 opening in body.Openings)
                {
                    if (!Finite(opening.GetAabb()))
                    {
                        return false;
                    }
                }
            }

            // Read with z up first; where another axis costs fewer sections, read again with that one up, its coordinates
            // swapped in whole, so the volume is the one slicing the swapped bodies gives, to the bit.
            GeoPoint3 least = subject.GetAabb().Min;
            var frame = new Frame(least, 2);

            if (!Prepare(bodies, roles, frame, out Extent region, out List<Shape> shapes))
            {
                return true;
            }

            int up = Up(shapes, region);

            if (up != 2)
            {
                frame = new Frame(least, up);
                Prepare(bodies, roles, frame, out region, out shapes);
            }

            List<double> levels = Levels(shapes, region);
            var work = new Workspace(shapes, roles);
            double sum = 0.0;

            for (int k = 0; k + 1 < levels.Count; k++)
            {
                double low = levels[k], height = levels[k + 1] - levels[k];
                double layer = 0.0;

                for (int s = 0; s < MilneAt.Length; s++)
                {
                    if (!TryArea(work, region, low + MilneAt[s] * height, out double area))
                    {
                        return false;
                    }

                    layer += MilneWeight[s] * area;
                }

                sum += layer * height;
            }

            if (double.IsNaN(sum) || double.IsInfinity(sum))
            {
                return false;
            }

            volume = sum;
            return true;
        }

        /// <summary>
        /// The region the volume can lie in, the subject's box within the box round every body it must lie within, and
        /// every body and opening that reaches it, in a frame.
        /// </summary>
        /// <returns>false where the region is empty: the volume is nought.</returns>
        private static bool Prepare(List<GeoSolid3> bodies, List<Role> roles, Frame frame, out Extent region, out List<Shape> shapes)
        {
            Extent around = Extent.Empty;

            for (int b = 0; b < bodies.Count; b++)
            {
                if (roles[b] == Role.Within)
                {
                    around = around.Union(frame.Of(bodies[b].GetAabb()));
                }
            }

            region = frame.Of(bodies[0].GetAabb()).Intersect(around);
            shapes = new List<Shape>();

            if (region.IsEmpty)
            {
                return false;
            }

            for (int b = 0; b < bodies.Count; b++)
            {
                if (!frame.Of(bodies[b].GetAabb()).Meets(region, Reach))
                {
                    continue;
                }

                shapes.Add(new Shape(bodies[b], b, false, frame, region));

                foreach (GeoSolid3 opening in bodies[b].Openings)
                {
                    if (frame.Of(opening.GetAabb()).Meets(region, Reach))
                    {
                        shapes.Add(new Shape(opening, b, true, frame, region));
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The axis to slice across: the one whose sections cost the least, z, x and y in that order where two cost the
        /// same.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each section costs about the faces it crosses, so an axis costs about the faces each corner level within the
        /// region crosses, added up, and a section for each level besides. The edges through faces of other bodies, which
        /// add levels too, are left out of the count; they are as many whichever way the bodies are cut.
        /// </para>
        /// <para>
        /// A curved girder in a curved wall has its facets at many heights along z, and each level crosses most of the
        /// facets of both; across x, few levels cross few faces, and the pair takes a tenth of the time. A slab sloped 5
        /// degrees with openings scattered over it crosses nearly every opening at every level along z, and only those
        /// in a strip across x: with 400 openings, 156 seconds against 1.4.
        /// </para>
        /// </remarks>
        private static int Up(List<Shape> shapes, Extent region)
        {
            int best = 2;
            long least = long.MaxValue;

            foreach (int axis in new[] { 2, 0, 1 })
            {
                var corners = new List<double>();

                foreach (Shape shape in shapes)
                {
                    foreach (Face face in shape.Near)
                    {
                        foreach (double[] loop in face.Loops)
                        {
                            for (int i = 0; i < loop.Length; i += 3)
                            {
                                if (region.Holds(loop[i], loop[i + 1], loop[i + 2], Reach))
                                {
                                    corners.Add(loop[i + axis]);
                                }
                            }
                        }
                    }
                }

                corners.Sort();

                var levels = new List<double>();

                foreach (double c in corners)
                {
                    if (levels.Count == 0 || c - levels[levels.Count - 1] > LevelMerge)
                    {
                        levels.Add(c);
                    }
                }

                long cost = levels.Count;

                foreach (Shape shape in shapes)
                {
                    foreach (Face face in shape.Near)
                    {
                        cost += Math.Max(0, FirstAbove(levels, face.Box.Max(axis)) - FirstAbove(levels, face.Box.Min(axis)));
                    }
                }

                if (cost < least)
                {
                    least = cost;
                    best = axis;
                }
            }

            return best;
        }

        /// <summary>
        /// The first place in sorted values holding one above a value; the count where none does.
        /// </summary>
        private static int FirstAbove(List<double> sorted, double value)
        {
            int below = 0, above = sorted.Count;

            while (below < above)
            {
                int middle = (below + above) >> 1;

                if (sorted[middle] <= value)
                {
                    below = middle + 1;
                }
                else
                {
                    above = middle;
                }
            }

            return below;
        }

        private static bool Finite(GeoAabb3 box)
            =>Finite(box.Min.X) && Finite(box.Min.Y) && Finite(box.Min.Z) && Finite(box.Max.X) && Finite(box.Max.Y) && Finite(box.Max.Z);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        #region Levels

        /// <summary>
        /// The heights between which the section's area is a polynomial of the second degree, within the region, those
        /// nearer than <see cref="LevelMerge"/> taken as one: every corner of a shape, every edge of one shape through a
        /// face of another, and every meeting of three faces of three shapes.
        /// </summary>
        /// <remarks>
        /// Only what lies in the region can bend the area, since all the volume lies in it: a wall 30 000 long met by a
        /// girder 500 wide is sliced at its corners within the girder's width, not at all of those along the wall.
        /// </remarks>
        private static List<double> Levels(List<Shape> shapes, Extent region)
        {
            var heights = new List<double> { region.MinZ, region.MaxZ };

            foreach (Shape shape in shapes)
            {
                foreach (Face face in shape.Near)
                {
                    foreach (double[] loop in face.Loops)
                    {
                        for (int i = 0; i < loop.Length; i += 3)
                        {
                            if (region.Holds(loop[i], loop[i + 1], loop[i + 2], Reach))
                            {
                                heights.Add(loop[i + 2]);
                            }
                        }
                    }
                }
            }

            // An edge of one shape through a face of another, and three faces of three shapes meeting, are corners of the
            // result that no shape has: a column turned through a slab bends the slab's section where the column's edges
            // pass through the slab's top and bottom.
            for (int a = 0; a < shapes.Count; a++)
            {
                for (int b = a + 1; b < shapes.Count; b++)
                {
                    foreach (Face fa in shapes[a].Near)
                    {
                        foreach (Face fb in shapes[b].Near)
                        {
                            if (!fa.Box.Meets(fb.Box, Reach))
                            {
                                continue;
                            }

                            AddPiercings(fa, fb, region, heights);
                            AddPiercings(fb, fa, region, heights);

                            Extent both = fa.Box.Intersect(fb.Box);

                            for (int c = b + 1; c < shapes.Count; c++)
                            {
                                foreach (Face fc in shapes[c].Near)
                                {
                                    if (fc.Box.Meets(both, Reach))
                                    {
                                        AddMeeting(fa, fb, fc, both.Intersect(fc.Box), region, heights);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            heights.Sort();

            var levels = new List<double>();

            foreach (double z in heights)
            {
                if (z < region.MinZ || z > region.MaxZ)
                {
                    continue;
                }

                if (levels.Count == 0 || z - levels[levels.Count - 1] > LevelMerge)
                {
                    levels.Add(z);
                }
            }

            return levels;
        }

        /// <summary>
        /// Adds the height at which each edge of one face passes through the plane of another, within that face's box.
        /// </summary>
        private static void AddPiercings(Face edges, Face plane, Extent region, List<double> heights)
        {
            foreach (double[] loop in edges.Loops)
            {
                int count = loop.Length / 3;

                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    double sa = plane.Side(loop[3 * i], loop[3 * i + 1], loop[3 * i + 2]);
                    double sb = plane.Side(loop[3 * j], loop[3 * j + 1], loop[3 * j + 2]);

                    // An edge wholly to one side of the plane, or on it, does not pass through it; its ends are corners.
                    if ((sa > 0.0 && sb > 0.0) || (sa < 0.0 && sb < 0.0) || sa == sb)
                    {
                        continue;
                    }

                    double t = sa / (sa - sb);
                    double x = loop[3 * i] + t * (loop[3 * j] - loop[3 * i]);
                    double y = loop[3 * i + 1] + t * (loop[3 * j + 1] - loop[3 * i + 1]);
                    double z = loop[3 * i + 2] + t * (loop[3 * j + 2] - loop[3 * i + 2]);

                    if (plane.Box.Holds(x, y, z, Reach) && region.Holds(x, y, z, Reach))
                    {
                        heights.Add(z);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the height at which the planes of three faces meet, where they meet in one point within the box the three
        /// share.
        /// </summary>
        private static void AddMeeting(Face f1, Face f2, Face f3, Extent shared, Extent region, List<double> heights)
        {
            // Cramer's rule on n1.p = d1, n2.p = d2, n3.p = d3. Planes all but parallel meet far off, out of every box.
            double c23x = f2.Ny * f3.Nz - f2.Nz * f3.Ny, c23y = f2.Nz * f3.Nx - f2.Nx * f3.Nz, c23z = f2.Nx * f3.Ny - f2.Ny * f3.Nx;
            double c31x = f3.Ny * f1.Nz - f3.Nz * f1.Ny, c31y = f3.Nz * f1.Nx - f3.Nx * f1.Nz, c31z = f3.Nx * f1.Ny - f3.Ny * f1.Nx;
            double c12x = f1.Ny * f2.Nz - f1.Nz * f2.Ny, c12y = f1.Nz * f2.Nx - f1.Nx * f2.Nz, c12z = f1.Nx * f2.Ny - f1.Ny * f2.Nx;
            double det = f1.Nx * c23x + f1.Ny * c23y + f1.Nz * c23z;

            if (det == 0.0)
            {
                return;
            }

            double x = (f1.D * c23x + f2.D * c31x + f3.D * c12x) / det;
            double y = (f1.D * c23y + f2.D * c31y + f3.D * c12y) / det;
            double z = (f1.D * c23z + f2.D * c31z + f3.D * c12z) / det;

            if (shared.Holds(x, y, z, Reach) && region.Holds(x, y, z, Reach))
            {
                heights.Add(z);
            }
        }

        #endregion

        #region Sections

        /// <summary>
        /// The area of the result in the section at a height.
        /// </summary>
        private static bool TryArea(Workspace work, Extent region, double z, out double area)
        {
            area = 0.0;
            List<Segment> segments = work.Segments;
            segments.Clear();

            for (int s = 0; s < work.Shapes.Count; s++)
            {
                if (!TrySection(work, s, z, region))
                {
                    return false;
                }
            }

            // The strips run across the subject's section within the region; outside either there is nothing to measure.
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;

            foreach (Segment segment in segments)
            {
                if (segment.Shape == 0)
                {
                    lo = Math.Min(lo, segment.X1);
                    hi = Math.Max(hi, segment.X2);
                }
            }

            lo = Math.Max(lo, region.MinX);
            hi = Math.Min(hi, region.MaxX);

            if (!(lo < hi))
            {
                return true;
            }

            List<double> xs = work.Xs;
            xs.Clear();
            xs.Add(lo);
            xs.Add(hi);

            foreach (Segment segment in segments)
            {
                if (segment.X1 > lo && segment.X1 < hi)
                {
                    xs.Add(segment.X1);
                }

                if (segment.X2 > lo && segment.X2 < hi)
                {
                    xs.Add(segment.X2);
                }
            }

            AddCrossings(work, lo, hi);
            xs.Sort();

            // The segments by where they start in x, and of two the same by their place, swept across the strips.
            int count = segments.Count;
            int[] order = work.Order(count);
            Array.Sort(order, 0, count, work.ByStart);

            List<int> active = work.Active;
            active.Clear();
            int next = 0;
            double before = xs[0];

            for (int i = 1; i < xs.Count; i++)
            {
                double x = xs[i];

                if (x - before <= StripMerge)
                {
                    continue;
                }

                double middle = 0.5 * (before + x), width = x - before;
                before = x;

                while (next < count && segments[order[next]].X1 < middle)
                {
                    active.Add(order[next]);
                    next++;
                }

                for (int a = active.Count - 1; a >= 0; a--)
                {
                    if (segments[active[a]].X2 <= middle)
                    {
                        active[a] = active[active.Count - 1];
                        active.RemoveAt(active.Count - 1);
                    }
                }

                if (!TryLength(work, middle, out double length))
                {
                    return false;
                }

                area += length * width;
            }

            return true;
        }

        /// <summary>
        /// Adds the segments of the section of a shape at a height, each face giving those where the level crosses it.
        /// </summary>
        /// <returns>false where a face is crossed an odd number of times: its loops do not close.</returns>
        private static bool TrySection(Workspace work, int index, double z, Extent region)
        {
            List<Point> points = work.Points;

            foreach (Face face in work.Shapes[index].Faces)
            {
                // Only a face a line of constant x within the region can cross adds to the counts along it.
                if (!(face.Box.MinZ < z && z <= face.Box.MaxZ) || face.Box.MaxX < region.MinX - Reach || face.Box.MinX > region.MaxX + Reach)
                {
                    continue;
                }

                points.Clear();

                foreach (double[] loop in face.Loops)
                {
                    int count = loop.Length / 3;

                    for (int i = 0; i < count; i++)
                    {
                        int j = (i + 1) % count;

                        if ((loop[3 * i + 2] < z) == (loop[3 * j + 2] < z))
                        {
                            continue;
                        }

                        // The ends in one order whichever face the edge is read from, so that the two faces sharing it
                        // put its crossing at the same point to the last bit, and the section closes.
                        int p = i, q = j;

                        if (Before(loop, j, i))
                        {
                            p = j;
                            q = i;
                        }

                        double t = (z - loop[3 * p + 2]) / (loop[3 * q + 2] - loop[3 * p + 2]);
                        double x = loop[3 * p] + t * (loop[3 * q] - loop[3 * p]);
                        double y = loop[3 * p + 1] + t * (loop[3 * q + 1] - loop[3 * p + 1]);
                        points.Add(new Point(x, y, x * face.Dx + y * face.Dy));
                    }
                }

                if (points.Count == 0)
                {
                    continue;
                }

                if (points.Count % 2 != 0)
                {
                    return false;
                }

                // Along the line the level cuts the face's plane in, the crossings pair off into what lies on the face.
                points.Sort(ByAlong);

                for (int i = 0; i < points.Count; i += 2)
                {
                    Point a = points[i], b = points[i + 1];

                    if (a.X == b.X && a.Y == b.Y)
                    {
                        continue;
                    }

                    work.Segments.Add(a.X <= b.X ? new Segment(a.X, a.Y, b.X, b.Y, index) : new Segment(b.X, b.Y, a.X, a.Y, index));
                }
            }

            return true;
        }

        /// <summary>
        /// Whether corner i of a loop comes before corner j, by z, then x, then y.
        /// </summary>
        private static bool Before(double[] loop, int i, int j)
        {
            if (loop[3 * i + 2] != loop[3 * j + 2])
            {
                return loop[3 * i + 2] < loop[3 * j + 2];
            }

            if (loop[3 * i] != loop[3 * j])
            {
                return loop[3 * i] < loop[3 * j];
            }

            return loop[3 * i + 1] < loop[3 * j + 1];
        }

        private static int ByAlong(Point a, Point b)
        {
            int along = a.Along.CompareTo(b.Along);

            if (along != 0)
            {
                return along;
            }

            int x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        }

        /// <summary>
        /// Adds the x of every crossing of two segments of the section within the strips, two of one shape among them, the
        /// pairs to try found by a sweep along the axis that leaves the fewest.
        /// </summary>
        /// <remarks>
        /// Along x the long sides of a wall running in x overlap every other segment, and along y those of a wall running
        /// in y do, so neither axis will do for every section.
        /// </remarks>
        private static void AddCrossings(Workspace work, double lo, double hi)
        {
            List<Segment> segments = work.Segments;
            int count = segments.Count;
            int axis = Pairs(work, 0) <= Pairs(work, 1) ? 0 : 1;
            int[] order = work.Swept(axis);

            for (int p = 0; p < count; p++)
            {
                Segment a = segments[order[p]];
                double end = axis == 0 ? a.X2 : a.YHigh;

                for (int q = p + 1; q < count; q++)
                {
                    Segment c = segments[order[q]];

                    if ((axis == 0 ? c.X1 : c.YLow) > end)
                    {
                        break;
                    }

                    if (axis == 0 ? c.YLow > a.YHigh || c.YHigh < a.YLow : c.X1 > a.X2 || c.X2 < a.X1)
                    {
                        continue;
                    }

                    // The pair in the order of their places, so that the crossing is worked out the same whichever axis
                    // the sweep took.
                    bool first = order[p] < order[q];

                    if (TryCross(first ? a : c, first ? c : a, out double x) && x > lo && x < hi)
                    {
                        work.Xs.Add(x);
                    }
                }
            }
        }

        /// <summary>
        /// The number of pairs of segments a sweep along an axis tries, the segments left sorted along it.
        /// </summary>
        private static long Pairs(Workspace work, int axis)
        {
            List<Segment> segments = work.Segments;
            int count = segments.Count;
            int[] order = work.Sweep(axis, count);
            Array.Sort(order, 0, count, axis == 0 ? work.ByStart : work.ByLow);

            double[] starts = work.Starts(count);

            for (int p = 0; p < count; p++)
            {
                starts[p] = axis == 0 ? segments[order[p]].X1 : segments[order[p]].YLow;
            }

            long pairs = 0;

            for (int p = 0; p < count; p++)
            {
                double end = axis == 0 ? segments[order[p]].X2 : segments[order[p]].YHigh;
                int below = p + 1, above = count;

                // The first segment after p starting beyond its end.
                while (below < above)
                {
                    int middle = (below + above) >> 1;

                    if (starts[middle] <= end)
                    {
                        below = middle + 1;
                    }
                    else
                    {
                        above = middle;
                    }
                }

                pairs += below - p - 1;
            }

            return pairs;
        }

        /// <summary>
        /// The x where two segments cross, where they do at one point.
        /// </summary>
        private static bool TryCross(Segment a, Segment c, out double x)
        {
            x = 0.0;
            double rx = a.X2 - a.X1, ry = a.Y2 - a.Y1, sx = c.X2 - c.X1, sy = c.Y2 - c.Y1;
            double den = rx * sy - ry * sx;

            if (den == 0.0)
            {
                return false;
            }

            double t = ((c.X1 - a.X1) * sy - (c.Y1 - a.Y1) * sx) / den;
            double u = ((c.X1 - a.X1) * ry - (c.Y1 - a.Y1) * rx) / den;

            if (!(t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0))
            {
                return false;
            }

            x = a.X1 + t * rx;
            return true;
        }

        /// <summary>
        /// The length of the result along the line of constant x through the middle of a strip.
        /// </summary>
        /// <returns>false where a shape is crossed an odd number of times along it: its section does not close.</returns>
        private static bool TryLength(Workspace work, double x, out double length)
        {
            length = 0.0;
            List<Crossing> line = work.Line;
            line.Clear();

            foreach (int a in work.Active)
            {
                Segment s = work.Segments[a];

                if (s.X1 < x && x < s.X2)
                {
                    line.Add(new Crossing(s.Y1 + (s.Y2 - s.Y1) * ((x - s.X1) / (s.X2 - s.X1)), s.Shape));
                }
            }

            if (line.Count == 0)
            {
                return true;
            }

            int[] counts = work.Counts;
            Array.Clear(counts, 0, counts.Length);

            foreach (Crossing crossing in line)
            {
                counts[crossing.Shape]++;
            }

            foreach (int count in counts)
            {
                if (count % 2 != 0)
                {
                    return false;
                }
            }

            // Up the line, each crossing takes its shape in or out; the result is where the subject's material is, some
            // body's within, and no body's outside.
            line.Sort(ByHeight);
            work.Reset();
            double below = line[0].Y;

            foreach (Crossing crossing in line)
            {
                if (work.Kept)
                {
                    length += crossing.Y - below;
                }

                work.Toggle(crossing.Shape);
                below = crossing.Y;
            }

            return true;
        }

        private static int ByHeight(Crossing a, Crossing b)
        {
            int y = a.Y.CompareTo(b.Y);
            return y != 0 ? y : a.Shape.CompareTo(b.Shape);
        }

        #endregion

        #region Working state

        /// <summary>
        /// What a body is to the volume.
        /// </summary>
        private enum Role
        {
            /// <summary>The body measured.</summary>
            Subject,

            /// <summary>A body the volume must lie within one of.</summary>
            Within,

            /// <summary>A body the volume must lie outside of.</summary>
            Outside,
        }

        /// <summary>
        /// The coordinates slicing reads: moved to the subject's least corner, and turned so that the axis sliced across is
        /// the third, by swapping whole coordinates and nothing else.
        /// </summary>
        private readonly struct Frame
        {
            private readonly GeoPoint3 _origin;
            private readonly int _up;

            /// <param name="origin">The subject's least corner.</param>
            /// <param name="up">The axis sliced across: 0 x, 1 y, 2 z; the other two follow it round, so x up reads (y, z, x).</param>
            internal Frame(GeoPoint3 origin, int up)
            {
                _origin = origin;
                _up = up;
            }

            internal double X(GeoPoint3 point) => Moved(point, (_up + 1) % 3);

            internal double Y(GeoPoint3 point) => Moved(point, (_up + 2) % 3);

            internal double Z(GeoPoint3 point) => Moved(point, _up);

            /// <summary>A box of the world as the frame reads it.</summary>
            internal Extent Of(GeoAabb3 box) => box.IsEmpty
                ? Extent.Empty
                : new Extent(X(box.Min), Y(box.Min), Z(box.Min), X(box.Max), Y(box.Max), Z(box.Max));

            private double Moved(GeoPoint3 point, int axis)
                => axis == 0 ? point.X - _origin.X : axis == 1 ? point.Y - _origin.Y : point.Z - _origin.Z;
        }

        /// <summary>
        /// A box in the frame.
        /// </summary>
        private readonly struct Extent
        {
            internal readonly double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

            internal Extent(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
            {
                MinX = minX;
                MinY = minY;
                MinZ = minZ;
                MaxX = maxX;
                MaxY = maxY;
                MaxZ = maxZ;
            }

            internal static Extent Empty => new Extent(
                double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity,
                double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

            internal bool IsEmpty => !(MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ);

            internal double Min(int axis) => axis == 0 ? MinX : axis == 1 ? MinY : MinZ;

            internal double Max(int axis) => axis == 0 ? MaxX : axis == 1 ? MaxY : MaxZ;

            internal Extent Union(Extent other) => new Extent(
                Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Min(MinZ, other.MinZ),
                Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY), Math.Max(MaxZ, other.MaxZ));

            internal Extent Intersect(Extent other) => new Extent(
                Math.Max(MinX, other.MinX), Math.Max(MinY, other.MinY), Math.Max(MinZ, other.MinZ),
                Math.Min(MaxX, other.MaxX), Math.Min(MaxY, other.MaxY), Math.Min(MaxZ, other.MaxZ));

            internal bool Meets(Extent other, double reach)
                => other.MinX <= MaxX + reach && other.MaxX >= MinX - reach
                && other.MinY <= MaxY + reach && other.MaxY >= MinY - reach
                && other.MinZ <= MaxZ + reach && other.MaxZ >= MinZ - reach;

            internal bool Holds(double x, double y, double z, double reach)
                => x >= MinX - reach && x <= MaxX + reach && y >= MinY - reach && y <= MaxY + reach && z >= MinZ - reach && z <= MaxZ + reach;
        }

        /// <summary>
        /// A body or one of its openings, as its faces in the moved coordinates.
        /// </summary>
        private sealed class Shape
        {
            internal Shape(GeoSolid3 solid, int body, bool isOpening, Frame frame, Extent region)
            {
                Body = body;
                IsOpening = isOpening;
                Faces = new Face[solid.Faces.Count];
                var near = new List<Face>();

                for (int f = 0; f < Faces.Length; f++)
                {
                    Faces[f] = new Face(solid.Faces[f], frame);

                    if (Faces[f].Box.Meets(region, Reach))
                    {
                        near.Add(Faces[f]);
                    }
                }

                Near = near.ToArray();
            }

            /// <summary>The place of the body among the subject, the bodies within and the bodies outside.</summary>
            internal int Body { get; }

            internal bool IsOpening { get; }

            internal Face[] Faces { get; }

            /// <summary>The faces whose boxes meet the region.</summary>
            internal Face[] Near { get; }
        }

        /// <summary>
        /// A face: its loops, x y z after x y z, its plane n.p = d, and the way a level cuts its plane.
        /// </summary>
        private sealed class Face
        {
            internal Face(GeoFace3 face, Frame frame)
            {
                Loops = new double[face.Holes.Count + 1][];
                Loops[0] = Coordinates(face.Boundary, frame);

                for (int h = 0; h < face.Holes.Count; h++)
                {
                    Loops[h + 1] = Coordinates(face.Holes[h], frame);
                }

                Extent box = Extent.Empty;

                foreach (double[] loop in Loops)
                {
                    for (int i = 0; i < loop.Length; i += 3)
                    {
                        box = box.Union(new Extent(loop[i], loop[i + 1], loop[i + 2], loop[i], loop[i + 1], loop[i + 2]));
                    }
                }

                Box = box;

                // Newell's normal of the boundary, and the plane through the boundary's mean corner.
                double[] outer = Loops[0];
                int count = outer.Length / 3;
                double nx = 0.0, ny = 0.0, nz = 0.0, cx = 0.0, cy = 0.0, cz = 0.0;

                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    nx += (outer[3 * i + 1] - outer[3 * j + 1]) * (outer[3 * i + 2] + outer[3 * j + 2]);
                    ny += (outer[3 * i + 2] - outer[3 * j + 2]) * (outer[3 * i] + outer[3 * j]);
                    nz += (outer[3 * i] - outer[3 * j]) * (outer[3 * i + 1] + outer[3 * j + 1]);
                    cx += outer[3 * i];
                    cy += outer[3 * i + 1];
                    cz += outer[3 * i + 2];
                }

                Nx = nx;
                Ny = ny;
                Nz = nz;
                D = count == 0 ? 0.0 : (nx * cx + ny * cy + nz * cz) / count;

                // A level cuts the plane along n x z; a face whose normal stands straight up has no such line, and its
                // crossings, of a face a hair off level, are paired along x.
                bool upright = nx == 0.0 && ny == 0.0;
                Dx = upright ? 1.0 : ny;
                Dy = upright ? 0.0 : -nx;
            }

            internal double[][] Loops { get; }

            internal Extent Box { get; }

            internal double Nx { get; }

            internal double Ny { get; }

            internal double Nz { get; }

            internal double D { get; }

            internal double Dx { get; }

            internal double Dy { get; }

            internal double Side(double x, double y, double z) => Nx * x + Ny * y + Nz * z - D;

            private static double[] Coordinates(GeoPolygon3 polygon, Frame frame)
            {
                var coordinates = new double[polygon.Vertices.Count * 3];

                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    GeoPoint3 vertex = polygon.Vertices[i];
                    coordinates[3 * i] = frame.X(vertex);
                    coordinates[3 * i + 1] = frame.Y(vertex);
                    coordinates[3 * i + 2] = frame.Z(vertex);
                }

                return coordinates;
            }
        }

        /// <summary>
        /// Where a level crosses an edge of a face, and how far along the line it cuts the face's plane in.
        /// </summary>
        private readonly struct Point
        {
            internal readonly double X, Y, Along;

            internal Point(double x, double y, double along)
            {
                X = x;
                Y = y;
                Along = along;
            }
        }

        /// <summary>
        /// A segment of a section, its ends in the order of x, and the shape it bounds.
        /// </summary>
        private readonly struct Segment
        {
            internal readonly double X1, Y1, X2, Y2, YLow, YHigh;

            internal readonly int Shape;

            internal Segment(double x1, double y1, double x2, double y2, int shape)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                YLow = Math.Min(y1, y2);
                YHigh = Math.Max(y1, y2);
                Shape = shape;
            }
        }

        /// <summary>
        /// Where a line of constant x crosses a segment of a shape.
        /// </summary>
        private readonly struct Crossing
        {
            internal readonly double Y;

            internal readonly int Shape;

            internal Crossing(double y, int shape)
            {
                Y = y;
                Shape = shape;
            }
        }

        /// <summary>
        /// The lists one volume reuses from section to section, and the state of each shape and body up a line.
        /// </summary>
        private sealed class Workspace
        {
            private readonly bool[] _inside;
            private readonly int[] _holes;
            private readonly bool[] _solid;
            private readonly Role[] _roles;
            private int[] _order = new int[16];
            private int[] _sweepX = new int[16];
            private int[] _sweepY = new int[16];
            private double[] _starts = new double[16];
            private bool _subject;
            private int _within;
            private int _outside;

            internal Workspace(List<Shape> shapes, List<Role> roles)
            {
                Shapes = shapes;
                _roles = roles.ToArray();
                _inside = new bool[shapes.Count];
                _holes = new int[_roles.Length];
                _solid = new bool[_roles.Length];
                Counts = new int[shapes.Count];
                ByStart = Comparer<int>.Create((i, j) =>
                {
                    int x = Segments[i].X1.CompareTo(Segments[j].X1);
                    return x != 0 ? x : i.CompareTo(j);
                });
                ByLow = Comparer<int>.Create((i, j) =>
                {
                    int y = Segments[i].YLow.CompareTo(Segments[j].YLow);
                    return y != 0 ? y : i.CompareTo(j);
                });
            }

            internal List<Shape> Shapes { get; }

            internal List<Segment> Segments { get; } = new List<Segment>();

            internal List<double> Xs { get; } = new List<double>();

            internal List<Point> Points { get; } = new List<Point>();

            internal List<int> Active { get; } = new List<int>();

            internal List<Crossing> Line { get; } = new List<Crossing>();

            internal int[] Counts { get; }

            /// <summary>The places of the segments by where they start in x, then by place.</summary>
            internal IComparer<int> ByStart { get; }

            /// <summary>The places of the segments by where they start in y, then by place.</summary>
            internal IComparer<int> ByLow { get; }

            /// <summary>Whether the line is in the result: in the subject's material, some body's within, no body's outside.</summary>
            internal bool Kept => _subject && _within > 0 && _outside == 0;

            /// <summary>The places 0, 1, ... of as many segments, for the sweep across the strips.</summary>
            internal int[] Order(int count) => Places(ref _order, count);

            /// <summary>The places 0, 1, ... of as many segments, to sort along an axis for the sweep that finds crossings.</summary>
            internal int[] Sweep(int axis, int count) => axis == 0 ? Places(ref _sweepX, count) : Places(ref _sweepY, count);

            /// <summary>The places of the segments as the last sort along an axis left them.</summary>
            internal int[] Swept(int axis) => axis == 0 ? _sweepX : _sweepY;

            internal double[] Starts(int count)
            {
                if (_starts.Length < count)
                {
                    _starts = new double[Math.Max(count, 2 * _starts.Length)];
                }

                return _starts;
            }

            internal void Reset()
            {
                Array.Clear(_inside, 0, _inside.Length);
                Array.Clear(_holes, 0, _holes.Length);
                Array.Clear(_solid, 0, _solid.Length);
                _subject = false;
                _within = 0;
                _outside = 0;
            }

            /// <summary>Takes a shape in or out at a crossing, and the body it belongs to with it.</summary>
            internal void Toggle(int shape)
            {
                Shape crossed = Shapes[shape];
                int body = crossed.Body;
                bool was = _solid[body] && _holes[body] == 0;
                _inside[shape] = !_inside[shape];

                if (crossed.IsOpening)
                {
                    _holes[body] += _inside[shape] ? 1 : -1;
                }
                else
                {
                    _solid[body] = _inside[shape];
                }

                bool now = _solid[body] && _holes[body] == 0;

                if (was == now)
                {
                    return;
                }

                switch (_roles[body])
                {
                    case Role.Subject:
                        _subject = now;
                        break;
                    case Role.Within:
                        _within += now ? 1 : -1;
                        break;
                    default:
                        _outside += now ? 1 : -1;
                        break;
                }
            }

            private static int[] Places(ref int[] buffer, int count)
            {
                if (buffer.Length < count)
                {
                    buffer = new int[Math.Max(count, 2 * buffer.Length)];
                }

                for (int i = 0; i < count; i++)
                {
                    buffer[i] = i;
                }

                return buffer;
            }
        }

        #endregion
    }
}
