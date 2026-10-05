using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// How many times as wide as the widest gap a loop may be on average, twice its area over its perimeter, and still be
        /// a crack, two sides of the surface standing apart, rather than a hole, where a face is missing.
        /// </summary>
        /// <remarks>
        /// A thin loop is twice as wide at its widest as on average: a corner of a face standing 0.02 off where the faces
        /// beside it have it leaves a loop 0.01 wide on average. Eight gaps on average takes a crack sixteen gaps wide at
        /// its widest for one, which a wider gap would weld, and a face of a part a few millimetres across, missing, for a
        /// hole.
        /// </remarks>
        private const double CrackGaps = 8.0;

        /// <summary>
        /// Follows the edges left open round into loops, the way a face closing each would run it, and stops where an edge
        /// left open is one more than two faces meet on, or a corner more than one loop runs through.
        /// </summary>
        /// <param name="work">The work, its faces cleaned and turned.</param>
        /// <param name="loops">The loops, in the order their first edges come; empty where nothing is open.</param>
        /// <returns>false, the trouble noted, where the open edges cannot be followed round one way only.</returns>
        /// <remarks>
        /// <para>
        /// A stretch one face runs is open, and the face that would close it runs it the other way. A stretch an odd number
        /// of faces more than one run is open past a fin, a face standing off the surface: which of them a face across it
        /// would close with cannot be told, and a face across the loop round the fin would be the fin again turned over,
        /// two faces back to back that read as closed. So would a stretch run by an even number of faces not as many each
        /// way, after the turning. Either is <see cref="ClosingFailure.NonManifold"/>, at the middle of the stretch, found
        /// before any loop is followed.
        /// </para>
        /// <para>
        /// The open edges are followed from corner to corner, their ends matched within the point tolerance. A corner more
        /// than one loop runs through, two holes meeting at a corner, could be followed either way: that too is
        /// <see cref="ClosingFailure.NonManifold"/>. A corner the open edges arrive at more often than they leave it, or the
        /// other way round, is one the loops do not close at, and the body is still open there.
        /// </para>
        /// </remarks>
        private static bool TryFindLoops(Work work, out List<Loop> loops)
        {
            loops = new List<Loop>();
            var rims = new List<(GeoPoint3 From, GeoPoint3 To)>();

            foreach (Stretch stretch in work.Stretches)
            {
                int count = 0, forward = 0;

                for (int r = 0; r < stretch.Faces.Length; r++)
                {
                    int face = stretch.Faces[r];

                    if (work.Dropped[face])
                    {
                        continue;
                    }

                    count++;

                    if (stretch.Forward[r] != work.Turned[face])
                    {
                        forward++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                int backward = count - forward;
                bool fin = count % 2 != 0 ? count > 1 : forward != backward;

                if (fin)
                {
                    work.Refuse(ClosingFailure.NonManifold, stretch.Middle);
                    return false;
                }

                if (count == 1)
                {
                    rims.Add(forward == 1 ? (stretch.End, stretch.Start) : (stretch.Start, stretch.End));
                }
            }

            if (rims.Count == 0)
            {
                return true;
            }

            var welder = new VertexWelder(work.Tolerance);
            var points = new List<GeoPoint3>();
            int edges = rims.Count;
            var from = new int[edges];
            var to = new int[edges];

            int Index(GeoPoint3 point)
            {
                int index = welder.GetIndex(point);

                if (index == points.Count)
                {
                    points.Add(point);
                }

                return index;
            }

            for (int e = 0; e < edges; e++)
            {
                from[e] = Index(rims[e].From);
                to[e] = Index(rims[e].To);
            }

            var leaving = new List<int>[points.Count];
            var arriving = new int[points.Count];

            for (int e = 0; e < edges; e++)
            {
                // An edge whose ends are one corner within the tolerance closes nothing, and opens nothing.
                if (from[e] == to[e])
                {
                    continue;
                }

                (leaving[from[e]] ?? (leaving[from[e]] = new List<int>())).Add(e);
                arriving[to[e]]++;
            }

            for (int v = 0; v < points.Count; v++)
            {
                int leaves = leaving[v]?.Count ?? 0;

                if (leaves != arriving[v])
                {
                    work.Refuse(ClosingFailure.StillOpen, points[v]);
                    return false;
                }

                if (leaves > 1)
                {
                    work.Refuse(ClosingFailure.NonManifold, points[v]);
                    return false;
                }
            }

            var used = new bool[edges];

            for (int seed = 0; seed < edges; seed++)
            {
                if (used[seed] || from[seed] == to[seed])
                {
                    continue;
                }

                var corners = new List<GeoPoint3>();
                int e = seed;

                while (true)
                {
                    used[e] = true;
                    corners.Add(rims[e].From);

                    if (to[e] == from[seed])
                    {
                        break;
                    }

                    // One edge leaves every corner, as many as arrive: the walk comes back to where it started.
                    e = leaving[to[e]][0];
                }

                loops.Add(new Loop(corners, work.Options.MaxGap));
            }

            return true;
        }

        /// <summary>
        /// Notes the first hole, in the order of the loops, where no hole may be filled: the largest hole nought, or the
        /// strategy <see cref="FillStrategy.None"/>. A crack is not a hole, and is the welds' and the stitches'.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="loops">The loops left open.</param>
        private static void RefuseHolesNotToBeFilled(Work work, List<Loop> loops)
        {
            SolidClosingOptions options = work.Options;

            if (options.MaxHoleArea > 0.0 && options.Fill != FillStrategy.None)
            {
                return;
            }

            foreach (Loop loop in loops)
            {
                if (!loop.IsCrack)
                {
                    work.Refuse(ClosingFailure.HoleTooLarge, PointIn(loop, loops, work.Tolerance));
                    return;
                }
            }
        }

        /// <summary>
        /// A point in a hole, on the plane fitted to its loop: where the loop lies flat, within the face that would fill
        /// it, the loops lying in its plane inside it taken as holes of that face, or within the face it is a hole of; the
        /// middle of its corners otherwise.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="loops">Every loop left open.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static GeoPoint3 PointIn(Loop loop, List<Loop> loops, Tolerance tolerance)
        {
            if (!(loop.Area > 0.0) || loop.OffFlat > tolerance.EqualPlanar)
            {
                return loop.Middle;
            }

            var plane = new GeoPlane3(loop.Middle, loop.Normal);
            var flat = new List<List<GeoPoint3>>();

            foreach (Loop other in loops)
            {
                if (LiesIn(other, plane, tolerance))
                {
                    flat.Add(other.Corners);
                }
            }

            try
            {
                foreach (GeoFace3 face in LoopAssembly.AssembleFacesIn(flat, plane, tolerance))
                {
                    if (HasCorner(face, loop.Corners[0], tolerance) && TryGetPointOn(face, tolerance, out GeoPoint3 inside))
                    {
                        return inside;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Loops that make no face here say only where they are: the middle of the corners, on their plane.
            }

            return loop.Middle;
        }

        /// <summary>
        /// Determines whether every corner of a loop lies within the planar tolerance of a plane, facing either way.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool LiesIn(Loop loop, GeoPlane3 plane, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in loop.Corners)
            {
                if (Math.Abs(plane.SignedDistanceTo(corner)) > tolerance.EqualPlanar)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a point is a corner of a face, of its boundary or of a hole, within the point tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool HasCorner(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (corner.IsEqualTo(point, tolerance))
                {
                    return true;
                }
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    if (corner.IsEqualTo(point, tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// A loop of edges left open, run the way a face closing it would run them, and what it is: its area, how far out of
        /// flat, and whether it is a crack or a hole.
        /// </summary>
        private sealed class Loop
        {
            /// <summary>
            /// Reads a loop.
            /// </summary>
            /// <param name="corners">Its corners, in order.</param>
            /// <param name="maxGap">The widest gap; see <see cref="CrackGaps"/>.</param>
            internal Loop(List<GeoPoint3> corners, double maxGap)
            {
                Corners = corners;
                int count = corners.Count;
                GeoPoint3 first = corners[0];
                GeoVector3 area = GeoVector3.Zero;
                GeoVector3 sum = GeoVector3.Zero;
                double perimeter = 0.0;

                // Measured from its first corner, so that nothing is lost to the size of the coordinates.
                for (int i = 0; i < count; i++)
                {
                    GeoVector3 here = first.GetVectorTo(corners[i]);
                    GeoVector3 next = first.GetVectorTo(corners[(i + 1) % count]);
                    area = area.Add(here.CrossProduct(next));
                    sum = sum.Add(here);
                    perimeter += corners[i].DistanceTo(corners[(i + 1) % count]);
                }

                AreaVector = area.Multiply(0.5);
                Area = AreaVector.Length;
                Perimeter = perimeter;
                Middle = first.Add(sum.Divide(count));
                Normal = Area > 0.0 ? AreaVector.Divide(Area) : GeoVector3.Zero;

                double off = 0.0;

                if (Area > 0.0)
                {
                    foreach (GeoPoint3 corner in corners)
                    {
                        off = Math.Max(off, Math.Abs(Middle.GetVectorTo(corner).DotProduct(Normal)));
                    }
                }

                OffFlat = off;
                IsCrack = !(perimeter > 0.0) || 2.0 * Area <= CrackGaps * maxGap * perimeter;
            }

            /// <summary>Gets its corners, in order.</summary>
            internal List<GeoPoint3> Corners { get; }

            /// <summary>Gets its area as a vector square to it, by Newell's method, along the way a face closing it would face.</summary>
            internal GeoVector3 AreaVector { get; }

            /// <summary>Gets its area: the length of <see cref="AreaVector"/>, its area seen square to it.</summary>
            internal double Area { get; }

            /// <summary>Gets the length of its edges together.</summary>
            internal double Perimeter { get; }

            /// <summary>Gets the middle of its corners.</summary>
            internal GeoPoint3 Middle { get; }

            /// <summary>Gets the unit vector along <see cref="AreaVector"/>; nought where it encloses no area.</summary>
            internal GeoVector3 Normal { get; }

            /// <summary>Gets how far its corner furthest off the plane through its middle square to its area stands off it.</summary>
            internal double OffFlat { get; }

            /// <summary>
            /// Gets whether it is a crack, no wider on average than <see cref="CrackGaps"/> gaps, rather than a hole.
            /// </summary>
            internal bool IsCrack { get; }
        }
    }
}
