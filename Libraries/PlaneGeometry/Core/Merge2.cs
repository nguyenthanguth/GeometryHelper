using System;
using System.Collections.Generic;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    /// <summary>
    /// Provides utility methods to merge consecutive collinear line segments and adjacent polylines.
    /// </summary>
    public static class Merge2
    {
        /// <summary>
        /// Rejoins the segments of a classified run that end where the next one begins.
        /// </summary>
        /// <param name="segments">The pieces landing on one side, in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One segment per unbroken stretch, in order along the subject.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <remarks>
        /// Pieces arrive here already sorted onto one side of the cutter, so two that touch were parted
        /// by a cut that turned out to separate nothing — the far side of it landed on this side too.
        /// Handing back both would report a seam the caller has no way to account for.
        /// <para>
        /// Joining start to end looks careless, since it discards whatever lay between them, but every
        /// piece here was cut from a single straight subject and the discarded point was on the line
        /// joining the two ends. The polyline form cannot take this shortcut and has to decide, which is
        /// what <see cref="Polylines"/> is for.
        /// </para>
        /// </remarks>
        public static GeoLine2[] ConsecutiveLines(IEnumerable<GeoLine2> segments, Tolerance tolerance)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));

            var result = new List<GeoLine2>();
            using (var enumerator = segments.GetEnumerator())
            {
                // Return an empty array if there are no segments to process.
                if (!enumerator.MoveNext()) return result.ToArray();
                GeoLine2 current = enumerator.Current;

                while (enumerator.MoveNext())
                {
                    GeoLine2 next = enumerator.Current;
                    // If the current segment ends where the next one begins within tolerance,
                    // merge them by extending the current segment to the end of the next one.
                    if (current.EndPoint.IsEqualTo(next.StartPoint, tolerance))
                    {
                        current = new GeoLine2(current.StartPoint, next.EndPoint);
                    }
                    else
                    {
                        // Otherwise, the current segment is complete. Save it and move to the next.
                        result.Add(current);
                        current = next;
                    }
                }
                // Add the last remaining segment after the loop.
                result.Add(current);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Joins two pieces into one if the first ends where the second begins.
        /// </summary>
        /// <param name="first">The earlier piece along the subject.</param>
        /// <param name="second">The piece that may continue it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The joined piece, or null when the two do not meet.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="first"/> or <paramref name="second"/> is null.</exception>
        /// <remarks>
        /// Null is the answer rather than an exception because not meeting is ordinary: the pieces on one
        /// side of a cutter are usually several separate stretches, and the caller walks the run using
        /// exactly this to tell one stretch from the next.
        /// </remarks>
        public static GeoPolyline2 Polylines(GeoPolyline2 first, GeoPolyline2 second, Tolerance tolerance)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));

            // The two polylines can only merge if the end of the first matches the start of the second.
            if (!first[first.VertexCount - 1].IsEqualTo(second[0], tolerance))
            {
                return null;
            }

            var vertices = new List<GeoPoint2>(first.Vertices);
            int junction = vertices.Count - 1;

            // The junction is where a cut was made that turned out not to separate anything, so the two
            // pieces are being put back together. Where it carries no bend it is an artefact of the cut
            // and goes; where it carries a real corner of the subject it stays. Without this the line and
            // polyline forms of the same split disagree, because merging two GeoLine2 pieces can only
            // produce one straight segment and drops the junction whether anyone decided to or not.
            
            // Check if the junction point is collinear with the previous vertex in first
            // and the second vertex in second. If it is, there is no corner/bend at the junction.
            bool carriesNoBend = Containment2.IsPointOn(
                new GeoLine2(vertices[junction - 1], second[1]), vertices[junction], tolerance);

            if (carriesNoBend)
            {
                // Remove the junction vertex to keep the merged polyline simplified and straight at this point.
                vertices.RemoveAt(junction);
            }

            // Append the remaining vertices of the second polyline (excluding its start point, which is the junction).
            for (int i = 1; i < second.VertexCount; i++)
            {
                vertices.Add(second[i]);
            }

            // The trusted constructor, so a caller supplied tolerance is not overridden by the global one
            // while the pieces are being reassembled.
            return new GeoPolyline2(vertices.ToArray(), vertices.Count);
        }

        /// <summary>
        /// Rejoins the pieces of a classified run that end where the next one begins.
        /// </summary>
        /// <param name="polylines">The pieces landing on one side, in order along the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One piece per unbroken stretch, in order along the subject.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polylines"/> is null.</exception>
        /// <remarks>
        /// The counterpart of <see cref="ConsecutiveLines"/> for a subject that can bend. The walk
        /// is the same; what differs is that whether two pieces join at all is left to
        /// <see cref="Polylines"/>, which returns null when they do not.
        /// </remarks>
        public static GeoPolyline2[] ConsecutivePolylines(IEnumerable<GeoPolyline2> polylines, Tolerance tolerance)
        {
            if (polylines == null) throw new ArgumentNullException(nameof(polylines));

            var result = new List<GeoPolyline2>();
            using (var enumerator = polylines.GetEnumerator())
            {
                // Return an empty array if there are no polylines to process.
                if (!enumerator.MoveNext()) return result.ToArray();
                GeoPolyline2 current = enumerator.Current;

                while (enumerator.MoveNext())
                {
                    GeoPolyline2 next = enumerator.Current;
                    // Attempt to merge the current polyline with the next one.
                    var merged = Merge2.Polylines(current, next, tolerance);
                    if (merged != null)
                    {
                        // Successfully merged, so continue building from the merged polyline.
                        current = merged;
                    }
                    else
                    {
                        // Could not merge, so save the current polyline and start a new stretch.
                        result.Add(current);
                        current = next;
                    }
                }
                // Add the last remaining polyline after the loop.
                result.Add(current);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Joins a collection of line segments into polylines by matching endpoints, similar to AutoCAD's JOIN command.
        /// Non-collinear connected segments form polylines, and collinear segments are simplified by removing redundant junctions.
        /// </summary>
        /// <param name="lines">The line segments to join.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>An array of joined polylines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        /// <remarks>
        /// Unlike <see cref="ConsecutiveLines"/> the segments arrive in no particular order and pointing
        /// in no particular direction, so a run is grown outwards from a seed segment in both
        /// directions at once, turning round whatever it picks up when that is what makes it fit.
        /// <para>
        /// Which segment a run takes up where three or more meet at a point is settled by input order,
        /// there being no geometric reason at a fork to prefer one branch over another. The answer is
        /// the same every time for a given input, but reordering the input can move a branch from one
        /// run to another.
        /// </para>
        /// <para>
        /// Segments are found through a grid keyed on their endpoints rather than by trying every pair,
        /// so the cost follows the number of segments instead of its square. The grid cannot be a plain
        /// lookup on the point, since two ends near enough to count as touching need not be identical
        /// and would hash apart; cells one tolerance wide put any two such points in the same cell or in
        /// adjoining ones, so the cell only narrows the field and the tolerance comparison still decides.
        /// </para>
        /// </remarks>
        public static GeoPolyline2[] Join(IEnumerable<GeoLine2> lines, Tolerance tolerance)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));

            var segments = new List<GeoLine2>();
            foreach (var line in lines)
            {
                // A segment whose ends coincide carries no direction to join along, and admitting it
                // would let a run finish with two identical vertices. The trusted constructor used at
                // the end does not filter those out, so a caller could be handed a polyline the public
                // constructor rejects. Turning the segment away here keeps that from ever arising.
                if (line.StartPoint.IsEqualTo(line.EndPoint, tolerance))
                {
                    continue;
                }

                segments.Add(line);
            }

            var grid = new EndpointGrid(segments, tolerance);
            var consumed = new bool[segments.Count];
            var joined = new List<GeoPolyline2>();

            var ahead = new List<GeoPoint2>();
            var behind = new List<GeoPoint2>();

            for (int seed = 0; seed < segments.Count; seed++)
            {
                if (consumed[seed]) continue;
                consumed[seed] = true;

                // Grow past the seed's end, then past its start. The second walk records the stretch
                // leading into the seed end first, so it is read back to front when the two are joined.
                ahead.Clear();
                ahead.Add(segments[seed].StartPoint);
                ahead.Add(segments[seed].EndPoint);
                Extend(segments, grid, consumed, ahead, tolerance);

                behind.Clear();
                behind.Add(segments[seed].StartPoint);
                Extend(segments, grid, consumed, behind, tolerance);

                var run = new List<GeoPoint2>(behind.Count + ahead.Count - 1);
                for (int i = behind.Count - 1; i >= 1; i--)
                {
                    run.Add(behind[i]);
                }
                run.AddRange(ahead);

                joined.Add(Simplify(run, tolerance));
            }

            return joined.ToArray();
        }

        /// <summary>
        /// Joins line segments into polylines by comparing every pair of runs, the way
        /// <see cref="Join"/> went about it before it was given a grid to search.
        /// </summary>
        /// <param name="lines">The line segments to join.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>An array of joined polylines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        /// <remarks>
        /// Kept as the plain reading of what joining means, to hold <see cref="Join"/> against. It
        /// tries every pair of runs and starts the sweep again after each merge, which is short enough
        /// to check by eye, at a cost that grows with the square of the number of segments: around four
        /// thousand of them it takes half a second where <see cref="Join"/> takes a few milliseconds.
        /// So it is for reading and for testing against, not for drawings.
        /// <para>
        /// The joining is as it was. The two guards around it are not: a null argument used to come
        /// back as a NullReferenceException, and a segment of zero length used to survive to the end
        /// and yield a polyline with two identical vertices, which the public GeoPolyline2 constructor
        /// refuses. Putting those back alongside a working <see cref="Join"/> would only be putting
        /// back two faults.
        /// </para>
        /// <para>
        /// Both hand the finished run to <see cref="Simplify"/>, which is itself unchanged from the
        /// original. What differs between the two is how runs are found, and that is the part worth
        /// holding one against the other.
        /// </para>
        /// </remarks>
        public static GeoPolyline2[] JoinBackup(IEnumerable<GeoLine2> lines, Tolerance tolerance)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));

            var resultPolylines = new List<List<GeoPoint2>>();
            foreach (var line in lines)
            {
                // See Join: a segment whose ends coincide would reach the end as a pair of identical
                // vertices, which the trusted constructor does not filter and the public one rejects.
                if (line.StartPoint.IsEqualTo(line.EndPoint, tolerance))
                {
                    continue;
                }

                resultPolylines.Add(new List<GeoPoint2> { line.StartPoint, line.EndPoint });
            }

            bool mergedAny;
            do
            {
                mergedAny = false;
                for (int i = 0; i < resultPolylines.Count; i++)
                {
                    for (int j = i + 1; j < resultPolylines.Count; j++)
                    {
                        var poly1 = resultPolylines[i];
                        var poly2 = resultPolylines[j];

                        GeoPoint2 start1 = poly1[0];
                        GeoPoint2 end1 = poly1[poly1.Count - 1];
                        GeoPoint2 start2 = poly2[0];
                        GeoPoint2 end2 = poly2[poly2.Count - 1];

                        // Case 1: end1 matches start2 (poly1 then poly2)
                        if (end1.IsEqualTo(start2, tolerance))
                        {
                            poly1.AddRange(poly2.GetRange(1, poly2.Count - 1));
                            resultPolylines.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                        // Case 2: start1 matches end2 (poly2 then poly1)
                        else if (start1.IsEqualTo(end2, tolerance))
                        {
                            poly2.AddRange(poly1.GetRange(1, poly1.Count - 1));
                            resultPolylines.RemoveAt(i);
                            mergedAny = true;
                            break;
                        }
                        // Case 3: end1 matches end2 (poly1 then reversed poly2)
                        else if (end1.IsEqualTo(end2, tolerance))
                        {
                            var rev2 = new List<GeoPoint2>(poly2);
                            rev2.Reverse();
                            poly1.AddRange(rev2.GetRange(1, rev2.Count - 1));
                            resultPolylines.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                        // Case 4: start1 matches start2 (reversed poly1 then poly2)
                        else if (start1.IsEqualTo(start2, tolerance))
                        {
                            poly1.Reverse();
                            poly1.AddRange(poly2.GetRange(1, poly2.Count - 1));
                            resultPolylines.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                    }
                    if (mergedAny) break;
                }
            } while (mergedAny);

            var finalPolylines = new List<GeoPolyline2>();
            foreach (var pts in resultPolylines)
            {
                finalPolylines.Add(Simplify(pts, tolerance));
            }

            return finalPolylines.ToArray();
        }

        /// <summary>
        /// Grows a run past its last vertex for as long as an unused segment meets it there.
        /// </summary>
        private static void Extend(List<GeoLine2> segments, EndpointGrid grid, bool[] consumed, List<GeoPoint2> run, Tolerance tolerance)
        {
            while (true)
            {
                GeoPoint2 tip = run[run.Count - 1];
                int next = grid.TakeSegmentAt(tip, consumed);
                if (next < 0)
                {
                    return;
                }

                consumed[next] = true;
                GeoLine2 segment = segments[next];

                // Whichever end of the segment met the tip is already the last vertex of the run, so it
                // is the far end that carries the run onwards.
                run.Add(segment.StartPoint.IsEqualTo(tip, tolerance) ? segment.EndPoint : segment.StartPoint);
            }
        }

        /// <summary>
        /// Drops the vertices of a run that its neighbours already pass straight through.
        /// </summary>
        /// <remarks>
        /// The test is against the segment joining the neighbours, not the whole line through them, so a
        /// vertex where the run turns back on itself survives: it is collinear with its neighbours but
        /// does not lie between them, and dropping it would hand back a shorter run than was joined.
        /// </remarks>
        private static GeoPolyline2 Simplify(List<GeoPoint2> run, Tolerance tolerance)
        {
            var simplified = new List<GeoPoint2> { run[0] };
            for (int i = 1; i < run.Count - 1; i++)
            {
                GeoPoint2 prev = simplified[simplified.Count - 1];
                GeoPoint2 curr = run[i];
                GeoPoint2 next = run[i + 1];

                if (!Containment2.IsPointOn(new GeoLine2(prev, next), curr, tolerance))
                {
                    simplified.Add(curr);
                }
            }
            simplified.Add(run[run.Count - 1]);

            // The trusted constructor, so a caller supplied tolerance is not overridden by the global
            // one while the pieces are being reassembled. Zero length segments were turned away before
            // the run was grown, which is what leaves every neighbouring pair of vertices distinct.
            return new GeoPolyline2(simplified.ToArray(), simplified.Count);
        }

        /// <summary>
        /// Locates segments by their endpoints, to within a tolerance.
        /// </summary>
        private sealed class EndpointGrid
        {
            private readonly Dictionary<Cell, List<int>> _cells = new Dictionary<Cell, List<int>>();
            private readonly List<GeoLine2> _segments;
            private readonly Tolerance _tolerance;
            private readonly double _cellSize;

            internal EndpointGrid(List<GeoLine2> segments, Tolerance tolerance)
            {
                _segments = segments;
                _tolerance = tolerance;

                // A tolerance of zero would divide by zero, and cells far finer than the numbers being
                // binned buy nothing, so the size has a floor. Cells wider than the tolerance do no
                // harm: searching the neighbours still brackets every candidate.
                _cellSize = Math.Max(tolerance.EqualPoint, 1E-9);

                for (int i = 0; i < segments.Count; i++)
                {
                    Add(segments[i].StartPoint, i);
                    Add(segments[i].EndPoint, i);
                }
            }

            /// <summary>
            /// Returns an unused segment with an endpoint at the given point, or -1 when there is none.
            /// </summary>
            internal int TakeSegmentAt(GeoPoint2 point, bool[] consumed)
            {
                Cell centre = CellOf(point);
                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        List<int> bucket;
                        if (!_cells.TryGetValue(new Cell(centre.X + dx, centre.Y + dy), out bucket))
                        {
                            continue;
                        }

                        int found = TakeFrom(bucket, point, consumed);
                        if (found >= 0)
                        {
                            return found;
                        }
                    }
                }

                return -1;
            }

            private void Add(GeoPoint2 point, int segment)
            {
                Cell cell = CellOf(point);
                List<int> bucket;
                if (!_cells.TryGetValue(cell, out bucket))
                {
                    bucket = new List<int>(2);
                    _cells.Add(cell, bucket);
                }

                bucket.Add(segment);
            }

            private Cell CellOf(GeoPoint2 point)
            {
                return new Cell((long)Math.Floor(point.X / _cellSize), (long)Math.Floor(point.Y / _cellSize));
            }

            /// <summary>
            /// Searches one cell for a segment that has not been taken up yet.
            /// </summary>
            /// <remarks>
            /// Segments already taken up are stepped over rather than cleared out. Clearing them was
            /// tried and measured against a hub of thousands of segments sharing one point, the shape a
            /// grid is worst at, and came out slower: keeping the cell in order costs more per search
            /// than the shortened cell saves. A cell in ordinary work holds a handful of segments.
            /// <para>
            /// Stepping over rather than reordering is also what keeps which branch a run takes at a
            /// fork settled by input order.
            /// </para>
            /// </remarks>
            private int TakeFrom(List<int> bucket, GeoPoint2 point, bool[] consumed)
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    int candidate = bucket[i];
                    if (consumed[candidate])
                    {
                        continue;
                    }

                    if (_segments[candidate].StartPoint.IsEqualTo(point, _tolerance) ||
                        _segments[candidate].EndPoint.IsEqualTo(point, _tolerance))
                    {
                        return candidate;
                    }
                }

                return -1;
            }

            /// <summary>
            /// Identifies one square of the grid.
            /// </summary>
            private readonly struct Cell : IEquatable<Cell>
            {
                internal Cell(long x, long y)
                {
                    X = x;
                    Y = y;
                }

                internal long X { get; }

                internal long Y { get; }

                public bool Equals(Cell other) => X == other.X && Y == other.Y;

                public override bool Equals(object obj) => obj is Cell other && Equals(other);

                public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
            }
        }
    }
}
