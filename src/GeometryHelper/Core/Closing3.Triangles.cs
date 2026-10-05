using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// The most corners a loop out of flat may have and still be filled by triangles: the least area across one is found
        /// in time growing as the cube of its corners.
        /// </summary>
        private const int MostCornersOutOfFlat = 256;

        /// <summary>
        /// The cosine of the least angle a triangle across a loop must be turned against the loop's normal by, to be turned
        /// back on it: 120 degrees.
        /// </summary>
        /// <remarks>
        /// Across a concave rim nearly flat, a way of filling it that runs a diagonal outside the rim folds a triangle back
        /// over the outside, facing nearly against the loop; such a way is no way of filling the hole, and counted, it parts
        /// the volumes the ways close by far more than the ways across the inside do. Across a loop well out of flat, as the
        /// one round two faces of a box left out side by side, the faces again stand square to the loop's normal, and are
        /// kept.
        /// </remarks>
        private const double TurnedBack = -0.5;

        /// <summary>
        /// Fills a loop out of flat by the triangles of least area across it on its own corners, of the ways of filling it
        /// none of whose triangles lies back to back with a face of the body; false, and why, where no way lies on no face,
        /// or where the strategy takes only an unambiguous fill and the ways part in the volume they close.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="index">The loop, by index, which the fill closes.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="options">The options.</param>
        /// <param name="fill">The triangles taken, one change; null when the method returns false.</param>
        /// <param name="failure">Why the loop is not filled; none when the method returns true.</param>
        /// <param name="at">A point at that trouble; the origin when the method returns true.</param>
        /// <remarks>
        /// <para>
        /// The ways of filling a loop of n corners with triangles on them are the triangulations of a polygon of n corners,
        /// and the least area across it, with the least and the most volume the ways close, are found by the dynamic
        /// program over the runs of its corners: each run from one corner to a later one is closed by a triangle on its two
        /// ends and a corner between, and the runs either side of that corner, the least of every choice kept. Each triangle
        /// is weighed once, so that a loop of 256 corners is some three million triangles. A triangle standing no higher
        /// than the point tolerance over its longest side is a needle, and no way takes it.
        /// </para>
        /// <para>
        /// A triangle lying back to back with a face of the body is a skin of no thickness, and no way taking it is one: the
        /// least area across the loop round two faces of a long box, its top and front, is the plane through the box's
        /// diagonal, but each end of it is a triangle lying on the end face of the box it stands in. Such a triangle has its
        /// three corners on that face, so each corner is read once for the faces it lies on, and a triangle is looked at
        /// closer only where its three corners share one. Nor is a way taking a triangle turned back against the loop's
        /// normal, folding over the outside of a concave rim (see <see cref="TurnedBack"/>): an L-shaped top of 300 lifted
        /// 0.005 at a convex corner is filled within a bound of 0.3 by its ways across the inside, which part by 0.25, where
        /// counted with the ways folding over the outside of its inner corner it read as ambiguous.
        /// </para>
        /// <para>
        /// The ways are taken as one where the most volume any of them closes and the least are no further apart than the
        /// loop's area times the planar tolerance, the uncertainty a face the library calls flat carries already: a top of
        /// four corners, one of them lifted by six times the tolerance, is filled either way across within it. Further
        /// apart, <see cref="FillStrategy.WhenUnambiguous"/> takes none, <see cref="ClosingFailure.HoleAmbiguous"/> at a
        /// point within the least area, and <see cref="FillStrategy.MinArea"/> takes the least area anyway. The volumes are
        /// measured from the middle of the loop's corners: the ways share the loop, so they part by the same, measured from
        /// anywhere.
        /// </para>
        /// </remarks>
        private static bool TryTriangulate(Loop loop, int index, List<GeoFace3> faces, FaceBoxes boxes, SolidClosingOptions options, out Patch fill, out ClosingFailure failure, out GeoPoint3 at)
        {
            fill = null;
            failure = ClosingFailure.None;
            at = GeoPoint3.Origin;
            Tolerance tolerance = options.Tolerance;
            List<GeoPoint3> corners = loop.Corners;
            int count = corners.Count;
            GeoPoint3 apex = loop.Middle;
            var p = new GeoVector3[count];

            for (int i = 0; i < count; i++)
            {
                p[i] = apex.GetVectorTo(corners[i]);
            }

            List<int>[] on = FacesAtCorners(corners, faces, boxes, tolerance);
            var shared = new List<int>[count * count];
            var length = new double[count * count];

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    length[(i * count) + j] = p[j].Subtract(p[i]).Length;
                    shared[(i * count) + j] = Shared(on[i], on[j]);
                }
            }

            // For each run of corners, from i to j: the least area across it and the corner the triangle on its ends takes,
            // and the least and the most volume the ways across it close.
            var area = new double[count * count];
            var least = new double[count * count];
            var most = new double[count * count];
            var split = new int[count * count];

            for (int span = 2; span < count; span++)
            {
                for (int i = 0; i + span < count; i++)
                {
                    int j = i + span;
                    double best = double.PositiveInfinity, low = double.PositiveInfinity, high = double.NegativeInfinity;
                    int choice = -1;

                    for (int k = i + 1; k < j; k++)
                    {
                        double before = area[(i * count) + k];
                        double after = area[(k * count) + j];

                        if (double.IsPositiveInfinity(before) || double.IsPositiveInfinity(after))
                        {
                            continue;
                        }

                        GeoVector3 across = p[k].Subtract(p[i]).CrossProduct(p[j].Subtract(p[i]));
                        double twice = across.Length;
                        double longest = Math.Max(length[(i * count) + j], Math.Max(length[(i * count) + k], length[(k * count) + j]));

                        // A needle within the point tolerance; a triangle turned back against the loop, folding over the outside
                        // of a concave rim; or one back to back with a face of the body.
                        if (!(twice > tolerance.EqualPoint * longest))
                        {
                            continue;
                        }

                        GeoVector3 normal = across.Divide(twice);

                        if (normal.DotProduct(loop.Normal) < TurnedBack || LiesBackToBack(shared[(i * count) + j], on[k], normal, corners, i, k, j, faces, tolerance))
                        {
                            continue;
                        }

                        double through = before + after + (0.5 * twice);

                        if (through < best)
                        {
                            best = through;
                            choice = k;
                        }

                        double volume = p[i].TripleProduct(p[k], p[j]) / 6.0;
                        low = Math.Min(low, least[(i * count) + k] + least[(k * count) + j] + volume);
                        high = Math.Max(high, most[(i * count) + k] + most[(k * count) + j] + volume);
                    }

                    area[(i * count) + j] = best;
                    split[(i * count) + j] = choice;
                    least[(i * count) + j] = low;
                    most[(i * count) + j] = high;
                }
            }

            int whole = count - 1;

            if (double.IsPositiveInfinity(area[whole]))
            {
                failure = ClosingFailure.StillOpen;
                at = apex;
                return false;
            }

            List<(int, int, int)> triangles = Triangles(split, count);

            if (options.Fill == FillStrategy.WhenUnambiguous && most[whole] - least[whole] > loop.Area * tolerance.EqualPlanar)
            {
                failure = ClosingFailure.HoleAmbiguous;
                at = Largest(triangles, corners);
                return false;
            }

            Tolerance build = ForFills(tolerance);
            var made = new GeoFace3[triangles.Count];

            try
            {
                for (int t = 0; t < triangles.Count; t++)
                {
                    (int i, int k, int j) = triangles[t];
                    made[t] = new GeoFace3(new GeoPolygon3(new[] { corners[i], corners[k], corners[j] }, build), null, build);
                }
            }
            catch (ArgumentException)
            {
                failure = ClosingFailure.StillOpen;
                at = apex;
                return false;
            }

            fill = new Patch(made, new[] { index });
            return true;
        }

        /// <summary>
        /// For each corner of a loop, the faces of the body it lies on within the tolerance, in the order of the faces; null
        /// for a corner on none.
        /// </summary>
        /// <param name="corners">The corners.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static List<int>[] FacesAtCorners(List<GeoPoint3> corners, List<GeoFace3> faces, FaceBoxes boxes, Tolerance tolerance)
        {
            var on = new List<int>[corners.Count];
            var near = new List<int>();

            for (int c = 0; c < corners.Count; c++)
            {
                GeoPoint3 corner = corners[c];
                boxes.Meeting(new GeoAabb3(corner, corner), near);

                foreach (int g in near)
                {
                    if (faces[g].Locate(corner, tolerance) != PointLocation.OutSide)
                    {
                        (on[c] ?? (on[c] = new List<int>())).Add(g);
                    }
                }
            }

            return on;
        }

        /// <summary>
        /// The faces two lists of faces, each in order, share, in order; null where they share none.
        /// </summary>
        /// <param name="one">The one list; null where it is empty.</param>
        /// <param name="other">The other.</param>
        private static List<int> Shared(List<int> one, List<int> other)
        {
            if (one == null || other == null)
            {
                return null;
            }

            List<int> shared = null;

            foreach (int face in one)
            {
                if (other.Contains(face))
                {
                    (shared ?? (shared = new List<int>())).Add(face);
                }
            }

            return shared;
        }

        /// <summary>
        /// Determines whether a triangle on three corners of a loop lies back to back with a face of the body all three of
        /// them lie on: the two facing opposite ways, and the triangle's middle on the face.
        /// </summary>
        /// <param name="ends">The faces the triangle's first and last corners share; null where they share none.</param>
        /// <param name="between">The faces its middle corner lies on; null where it lies on none.</param>
        /// <param name="normal">The triangle's unit normal, as the loop runs it.</param>
        /// <param name="corners">The loop's corners.</param>
        /// <param name="i">The triangle's first corner, by index.</param>
        /// <param name="k">Its middle corner.</param>
        /// <param name="j">Its last corner.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool LiesBackToBack(List<int> ends, List<int> between, GeoVector3 normal, List<GeoPoint3> corners, int i, int k, int j, List<GeoFace3> faces, Tolerance tolerance)
        {
            if (ends == null || between == null)
            {
                return false;
            }

            foreach (int g in ends)
            {
                if (!between.Contains(g) || !(normal.DotProduct(faces[g].Normal) < Facing))
                {
                    continue;
                }

                var middle = new GeoPoint3(
                    (corners[i].X + corners[k].X + corners[j].X) / 3.0,
                    (corners[i].Y + corners[k].Y + corners[j].Y) / 3.0,
                    (corners[i].Z + corners[k].Z + corners[j].Z) / 3.0);

                if (faces[g].Locate(middle, tolerance) != PointLocation.OutSide)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The triangles of the least area across a loop, each on its first, middle and last corner by index, as the runs the
        /// dynamic program chose split them, from the run of the whole loop down.
        /// </summary>
        /// <param name="split">For each run of corners, the corner its triangle takes.</param>
        /// <param name="count">How many corners the loop has.</param>
        private static List<(int, int, int)> Triangles(int[] split, int count)
        {
            var triangles = new List<(int, int, int)>(count - 2);
            var runs = new Stack<(int, int)>();
            runs.Push((0, count - 1));

            while (runs.Count > 0)
            {
                (int i, int j) = runs.Pop();
                int k = split[(i * count) + j];
                triangles.Add((i, k, j));

                if (j - k >= 2)
                {
                    runs.Push((k, j));
                }

                if (k - i >= 2)
                {
                    runs.Push((i, k));
                }
            }

            return triangles;
        }

        /// <summary>
        /// The middle of the largest of some triangles on a loop's corners, the first of them where two are as large.
        /// </summary>
        /// <param name="triangles">The triangles, each on its corners by index.</param>
        /// <param name="corners">The loop's corners.</param>
        private static GeoPoint3 Largest(List<(int, int, int)> triangles, List<GeoPoint3> corners)
        {
            double largest = -1.0;
            GeoPoint3 middle = corners[0];

            foreach ((int i, int k, int j) in triangles)
            {
                double area = corners[i].GetVectorTo(corners[k]).CrossProduct(corners[i].GetVectorTo(corners[j])).Length;

                if (area > largest)
                {
                    largest = area;
                    middle = new GeoPoint3(
                        (corners[i].X + corners[k].X + corners[j].X) / 3.0,
                        (corners[i].Y + corners[k].Y + corners[j].Y) / 3.0,
                        (corners[i].Z + corners[k].Z + corners[j].Z) / 3.0);
                }
            }

            return middle;
        }

        /// <summary>
        /// The corner of a loop standing furthest off the plane through the middle of its corners square to its area, the
        /// first of them where two stand as far.
        /// </summary>
        /// <param name="loop">The loop.</param>
        private static GeoPoint3 FurthestOff(Loop loop)
        {
            double furthest = -1.0;
            GeoPoint3 corner = loop.Corners[0];

            foreach (GeoPoint3 point in loop.Corners)
            {
                double off = Math.Abs(loop.Middle.GetVectorTo(point).DotProduct(loop.Normal));

                if (off > furthest)
                {
                    furthest = off;
                    corner = point;
                }
            }

            return corner;
        }
    }
}
