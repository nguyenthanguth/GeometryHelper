using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for cutting 3D geometry into pieces.
    /// <para>
    /// A curve is cut at a position along it or wherever a plane crosses it, and the pieces come back in
    /// order along the subject, so the first piece always holds its start point and the last holds its
    /// end point. A region or a body is cut by a plane and the pieces come back sorted by side.
    /// </para>
    /// <para>
    /// Every overload reports <c>false</c> when there was nothing to cut — the cutter missed, or it only
    /// grazed an endpoint — and in that case still hands back the subject as a single piece rather than a
    /// null array, so a caller can use the result either way.
    /// </para>
    /// </summary>
    public static class Splition3
    {
        #region Line

        /// <summary>
        /// Splits a line segment at an arc length from its start, using the default tolerance.
        /// </summary>
        public static bool TrySplitAtDistance(GeoLine3 line, double distance, out GeoLine3[] pieces)
        {
            return TrySplitAtDistance(line, distance, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at an arc length from its start, within a tolerance.
        /// </summary>
        /// <param name="line">The segment to cut.</param>
        /// <param name="distance">Where to cut, measured from the start point.</param>
        /// <param name="pieces">The pieces in order along the segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the position falls outside the segment or onto one of its endpoints.</returns>
        /// <remarks>
        /// A cut at an endpoint is refused rather than producing a piece of zero length, so no piece ever
        /// comes back shorter than the tolerance.
        /// </remarks>
        public static bool TrySplitAtDistance(GeoLine3 line, double distance, out GeoLine3[] pieces, Tolerance tolerance)
        {
            pieces = new[] { line };

            if (distance <= tolerance.EqualPoint || distance >= line.Length - tolerance.EqualPoint)
            {
                return false;
            }

            GeoPoint3 cut = Parametrization3.GetPointAtDistance(line, distance);

            pieces = new[]
            {
                new GeoLine3(line.StartPoint, cut),
                new GeoLine3(cut, line.EndPoint)
            };

            return true;
        }

        /// <summary>
        /// Splits a line segment at a point on it, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 line, GeoPoint3 point, out GeoLine3[] pieces)
        {
            return TrySplitBy(line, point, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at a point on it, within a tolerance.
        /// </summary>
        /// <returns>
        /// false when the point does not lie on the segment, or lies on one of its endpoints.
        /// </returns>
        /// <remarks>
        /// A point off the subject is refused rather than being projected onto it and cut at, because a
        /// caller who passed the wrong point would otherwise get a plausible answer to a question they did
        /// not ask.
        /// </remarks>
        public static bool TrySplitBy(GeoLine3 line, GeoPoint3 point, out GeoLine3[] pieces, Tolerance tolerance)
        {
            pieces = new[] { line };

            if (!Containment3.IsPointOn(line, point, tolerance))
            {
                return false;
            }

            return TrySplitAtDistance(line, Parametrization3.GetDistanceAtPoint(line, point), out pieces, tolerance);
        }

        /// <summary>
        /// Splits a line segment at several arc lengths at once, using the default tolerance.
        /// </summary>
        public static GeoLine3[] SplitAtDistances(GeoLine3 line, IEnumerable<double> distances)
        {
            return SplitAtDistances(line, distances, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment at several arc lengths at once, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Positions outside the segment or on its endpoints are skipped, and positions closer together
        /// than the tolerance are merged, so no piece comes back shorter than the tolerance whatever is
        /// passed in.
        /// </remarks>
        public static GeoLine3[] SplitAtDistances(GeoLine3 line, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (distances == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            List<double> cuts = CollectCuts(distances, line.Length, tolerance);

            if (cuts.Count == 0)
            {
                return new[] { line };
            }

            GeoLine3[] pieces = new GeoLine3[cuts.Count + 1];
            GeoPoint3 previous = line.StartPoint;

            for (int i = 0; i < cuts.Count; i++)
            {
                GeoPoint3 cut = Parametrization3.GetPointAtDistance(line, cuts[i]);
                pieces[i] = new GeoLine3(previous, cut);
                previous = cut;
            }

            pieces[cuts.Count] = new GeoLine3(previous, line.EndPoint);

            return pieces;
        }

        /// <summary>
        /// Sorts, clamps and de-duplicates a set of cut positions along a curve of a given length.
        /// </summary>
        private static List<double> CollectCuts(IEnumerable<double> distances, double totalLength, Tolerance tolerance)
        {
            List<double> sorted = new List<double>();

            foreach (double distance in distances)
            {
                if (distance > tolerance.EqualPoint && distance < totalLength - tolerance.EqualPoint)
                {
                    sorted.Add(distance);
                }
            }

            sorted.Sort();

            List<double> kept = new List<double>();

            foreach (double distance in sorted)
            {
                if (kept.Count == 0 || distance - kept[kept.Count - 1] > tolerance.EqualPoint)
                {
                    kept.Add(distance);
                }
            }

            return kept;
        }

        /// <summary>
        /// Splits a line segment where a plane crosses it, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 line, GeoPlane3 cutter, out GeoLine3[] pieces)
        {
            return TrySplitBy(line, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment where a plane crosses it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The pieces come back in order along the segment, so which side each one is on follows from
        /// where the segment started. A segment lying in the plane is not cut, for the same reason
        /// <c>Intersection3</c> refuses it: every point of it would be a cut.
        /// </remarks>
        public static bool TrySplitBy(GeoLine3 line, GeoPlane3 cutter, out GeoLine3[] pieces, Tolerance tolerance)
        {
            pieces = new[] { line };

            if (!Intersection3.TryIntersectWith(line, cutter, out GeoPoint3 cut, tolerance))
            {
                return false;
            }

            return TrySplitBy(line, cut, out pieces, tolerance);
        }

        #endregion

        #region Polyline

        /// <summary>
        /// Splits a polyline at an arc length from its start, using the default tolerance.
        /// </summary>
        public static bool TrySplitAtDistance(GeoPolyline3 polyline, double distance, out GeoPolyline3[] pieces)
        {
            return TrySplitAtDistance(polyline, distance, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at an arc length from its start, within a tolerance.
        /// </summary>
        public static bool TrySplitAtDistance(GeoPolyline3 polyline, double distance, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            pieces = new[] { polyline };

            GeoPolyline3[] result = SplitAtDistances(polyline, new[] { distance }, tolerance);

            if (result.Length < 2)
            {
                return false;
            }

            pieces = result;
            return true;
        }

        /// <summary>
        /// Splits a polyline at a point on it, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 polyline, GeoPoint3 point, out GeoPolyline3[] pieces)
        {
            return TrySplitBy(polyline, point, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at a point on it, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 polyline, GeoPoint3 point, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            pieces = new[] { polyline };

            if (!Containment3.IsPointOn(polyline, point, tolerance))
            {
                return false;
            }

            return TrySplitAtDistance(polyline, Parametrization3.GetDistanceAtPoint(polyline, point, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Splits a polyline at several arc lengths at once, using the default tolerance.
        /// </summary>
        public static GeoPolyline3[] SplitAtDistances(GeoPolyline3 polyline, IEnumerable<double> distances)
        {
            return SplitAtDistances(polyline, distances, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline at several arc lengths at once, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A cut position that falls within tolerance of an existing vertex snaps onto it rather than
        /// adding a vertex beside it, so no edge comes back shorter than the tolerance either.
        /// </remarks>
        public static GeoPolyline3[] SplitAtDistances(GeoPolyline3 polyline, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (distances == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            List<double> cuts = CollectCuts(distances, polyline.Length, tolerance);

            if (cuts.Count == 0)
            {
                return new[] { polyline };
            }

            List<GeoPolyline3> pieces = new List<GeoPolyline3>();
            List<GeoPoint3> current = new List<GeoPoint3> { polyline.StartPoint };

            double travelled = 0.0;
            int nextCut = 0;

            for (int edge = 0; edge < polyline.EdgeCount; edge++)
            {
                GeoLine3 segment = polyline.GetEdgeAt(edge);
                double edgeEnd = travelled + segment.Length;

                while (nextCut < cuts.Count && cuts[nextCut] < edgeEnd - tolerance.EqualPoint)
                {
                    GeoPoint3 cut = Parametrization3.GetPointAtDistance(segment, cuts[nextCut] - travelled);

                    if (!current[current.Count - 1].IsEqualTo(cut, tolerance))
                    {
                        current.Add(cut);
                    }

                    if (current.Count >= 2)
                    {
                        pieces.Add(new GeoPolyline3(current));
                    }

                    current = new List<GeoPoint3> { cut };
                    nextCut++;
                }

                travelled = edgeEnd;

                if (!current[current.Count - 1].IsEqualTo(segment.EndPoint, tolerance))
                {
                    current.Add(segment.EndPoint);
                }
            }

            if (current.Count >= 2)
            {
                pieces.Add(new GeoPolyline3(current));
            }

            return pieces.Count == 0 ? new[] { polyline } : pieces.ToArray();
        }

        /// <summary>
        /// Splits a polyline wherever a plane crosses it, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 polyline, GeoPlane3 cutter, out GeoPolyline3[] pieces)
        {
            return TrySplitBy(polyline, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline wherever a plane crosses it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A chain can cross the same plane several times, so every crossing is cut at and the pieces come
        /// back in order along the chain, alternating sides.
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 polyline, GeoPlane3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            pieces = new[] { polyline };

            List<double> cuts = new List<double>();
            double travelled = 0.0;

            for (int edge = 0; edge < polyline.EdgeCount; edge++)
            {
                GeoLine3 segment = polyline.GetEdgeAt(edge);

                if (Intersection3.TryIntersectWith(segment, cutter, out GeoPoint3 hit, tolerance))
                {
                    cuts.Add(travelled + Parametrization3.GetDistanceAtPoint(segment, hit));
                }

                travelled += segment.Length;
            }

            if (cuts.Count == 0)
            {
                return false;
            }

            GeoPolyline3[] result = SplitAtDistances(polyline, cuts, tolerance);

            if (result.Length < 2)
            {
                return false;
            }

            pieces = result;
            return true;
        }

        #endregion

        #region Polygon and face

        /// <summary>
        /// Splits a polygon by a plane, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolygon3 polygon, GeoPlane3 cutter, out GeoPolygon3[] above, out GeoPolygon3[] below)
        {
            return TrySplitBy(polygon, cutter, out above, out below, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polygon by a plane, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon to cut.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="above">The pieces on the side the cutter normal points towards.</param>
        /// <param name="below">The pieces on the other side.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the plane does not cut the polygon into two.</returns>
        /// <remarks>
        /// Every piece keeps the orientation of the subject, so each one reports the same normal the
        /// original did. A concave polygon can fall into more than two pieces — a plane through the waist
        /// of a U leaves one piece on one side and two on the other — which is why each side comes back as
        /// an array rather than a single polygon.
        /// <para>
        /// When the method returns false the subject is handed back whole on whichever side it lies, and
        /// the other side comes back empty.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolygon3 polygon, GeoPlane3 cutter, out GeoPolygon3[] above, out GeoPolygon3[] below, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            bool split = TrySplitBy(new GeoFace3(polygon), cutter, out GeoFace3[] faceAbove, out GeoFace3[] faceBelow, tolerance);

            // Cutting a hole-free region with a half-space can only produce hole-free pieces, so taking
            // the boundary of each piece loses nothing here.
            above = ToBoundaries(faceAbove);
            below = ToBoundaries(faceBelow);

            return split;
        }

        /// <summary>
        /// Reads the outer boundary of each face in an array.
        /// </summary>
        private static GeoPolygon3[] ToBoundaries(GeoFace3[] faces)
        {
            GeoPolygon3[] boundaries = new GeoPolygon3[faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                boundaries[i] = faces[i].Boundary;
            }

            return boundaries;
        }

        /// <summary>
        /// Splits a face by a plane, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoFace3 face, GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below)
        {
            return TrySplitBy(face, cutter, out above, out below, Tolerance.Global);
        }

        /// <summary>
        /// Splits a face by a plane, within a tolerance.
        /// </summary>
        /// <param name="face">The face to cut, holes included.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="above">The pieces on the side the cutter normal points towards.</param>
        /// <param name="below">The pieces on the other side.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the plane does not cut the face into two.</returns>
        /// <remarks>
        /// The holes are cut along with the boundary rather than being sorted onto one side afterwards.
        /// That matters when the plane passes through a hole: the piece on each side then has a boundary
        /// made partly of the old outer edge and partly of the old hole rim, so the topology changes and
        /// no amount of sorting whole holes would produce it. A hole the plane misses stays a hole, and is
        /// attached to whichever piece encloses it.
        /// </remarks>
        public static bool TrySplitBy(GeoFace3 face, GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoFace3[] none = new GeoFace3[0];
            GeoFace3[] whole = { face };

            bool anyAbove = false;
            bool anyBelow = false;

            foreach (GeoPoint3 vertex in EnumerateVertices(face))
            {
                PlaneSide side = Containment3.GetSide(cutter, vertex, tolerance);

                if (side == PlaneSide.Above)
                {
                    anyAbove = true;
                }
                else if (side == PlaneSide.Below)
                {
                    anyBelow = true;
                }
            }

            if (!anyAbove || !anyBelow)
            {
                // A face lying entirely in the cutting plane belongs to neither side; hand it back on both
                // so the caller still has it, and so a solid split can tell that case apart.
                above = anyAbove || (!anyAbove && !anyBelow) ? whole : none;
                below = anyBelow || (!anyAbove && !anyBelow) ? whole : none;
                return false;
            }

            if (!Intersection3.TryIntersectWith(face.GetPlane(), cutter, out GeoRay3 cutLine, tolerance))
            {
                above = whole;
                below = none;
                return false;
            }

            List<List<GeoPoint3>> loopsAbove = CutRings(face, cutter, cutLine, 1, tolerance);
            List<List<GeoPoint3>> loopsBelow = CutRings(face, cutter, cutLine, -1, tolerance);

            List<GeoFace3> upper = LoopAssembly.AssembleFaces(loopsAbove, face.Normal, tolerance);
            List<GeoFace3> lower = LoopAssembly.AssembleFaces(loopsBelow, face.Normal, tolerance);

            if (upper.Count == 0 || lower.Count == 0)
            {
                above = upper.Count > 0 ? upper.ToArray() : whole;
                below = lower.Count > 0 ? lower.ToArray() : none;
                return false;
            }

            above = upper.ToArray();
            below = lower.ToArray();
            return true;
        }

        /// <summary>
        /// Walks every ring of a face: the outer boundary first, then each hole.
        /// </summary>
        private static IEnumerable<IReadOnlyList<GeoPoint3>> EnumerateRings(GeoFace3 face)
        {
            yield return face.Boundary.Vertices;

            foreach (GeoPolygon3 hole in face.Holes)
            {
                yield return hole.Vertices;
            }
        }

        /// <summary>
        /// Walks every vertex of every ring of a face.
        /// </summary>
        private static IEnumerable<GeoPoint3> EnumerateVertices(GeoFace3 face)
        {
            foreach (IReadOnlyList<GeoPoint3> ring in EnumerateRings(face))
            {
                foreach (GeoPoint3 vertex in ring)
                {
                    yield return vertex;
                }
            }
        }

        /// <summary>
        /// Collects the closed loops that bound the part of a face lying on one side of a plane.
        /// </summary>
        /// <remarks>
        /// Every ring of the face is rebuilt with a vertex inserted at each crossing, and the crossings of
        /// all rings together are sorted along the line where the two planes meet. Sorted that way they
        /// alternate entering and leaving the material, so consecutive pairs bound the stretches of that
        /// line which are inside the face. Following a run of boundary to its end, stepping across its
        /// paired crossing, and picking up the run that starts there walks a piece back to where it began —
        /// and because the pairing spans all rings at once, a run on the outer boundary joins straight onto
        /// a run on a hole rim when the plane passes through both.
        /// </remarks>
        private static List<List<GeoPoint3>> CutRings(GeoFace3 face, GeoPlane3 cutter, GeoRay3 cutLine, int wantedSide, Tolerance tolerance)
        {
            List<List<GeoPoint3>> loops = new List<List<GeoPoint3>>();

            List<List<GeoPoint3>> ringPoints = new List<List<GeoPoint3>>();
            List<List<int>> ringSides = new List<List<int>>();

            foreach (IReadOnlyList<GeoPoint3> source in LoopAssembly.EnumerateMaterialRings(face))
            {
                BuildCutRing(source, cutter, tolerance, out List<GeoPoint3> points, out List<int> sides);
                ringPoints.Add(points);
                ringSides.Add(sides);
            }

            List<long> crossings = new List<long>();
            Dictionary<long, double> positions = new Dictionary<long, double>();

            for (int ring = 0; ring < ringPoints.Count; ring++)
            {
                for (int index = 0; index < ringPoints[ring].Count; index++)
                {
                    if (ringSides[ring][index] != 0)
                    {
                        continue;
                    }

                    long key = MakeKey(ring, index);
                    crossings.Add(key);
                    positions[key] = Parametrization3.GetDistanceAtPoint(cutLine, ringPoints[ring][index]);
                }
            }

            // A ring the plane never reaches contributes nothing to the walk, but if it sits on the wanted
            // side it is still part of the answer: an untouched hole stays a hole.
            for (int ring = 0; ring < ringPoints.Count; ring++)
            {
                if (RingHasCrossing(ringSides[ring]))
                {
                    continue;
                }

                if (RingSide(ringSides[ring]) == wantedSide)
                {
                    loops.Add(new List<GeoPoint3>(ringPoints[ring]));
                }
            }

            if (crossings.Count < 2)
            {
                return loops;
            }

            crossings.Sort((x, y) => positions[x].CompareTo(positions[y]));

            Dictionary<long, long> partner = new Dictionary<long, long>();
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                partner[crossings[i]] = crossings[i + 1];
                partner[crossings[i + 1]] = crossings[i];
            }

            Dictionary<long, List<GeoPoint3>> runs = new Dictionary<long, List<GeoPoint3>>();
            Dictionary<long, long> runEnds = new Dictionary<long, long>();

            foreach (long start in crossings)
            {
                ReadKey(start, out int ring, out int index);

                List<GeoPoint3> points = ringPoints[ring];
                List<int> sides = ringSides[ring];
                int count = points.Count;

                List<GeoPoint3> walk = new List<GeoPoint3> { points[index] };
                int cursor = (index + 1) % count;
                int runSide = 0;

                while (sides[cursor] != 0 && cursor != index)
                {
                    if (runSide == 0)
                    {
                        runSide = sides[cursor];
                    }

                    walk.Add(points[cursor]);
                    cursor = (cursor + 1) % count;
                }

                if (runSide != wantedSide || cursor == index)
                {
                    continue;
                }

                walk.Add(points[cursor]);
                runs[start] = walk;
                runEnds[start] = MakeKey(ring, cursor);
            }

            HashSet<long> consumed = new HashSet<long>();

            foreach (long seed in runs.Keys)
            {
                if (consumed.Contains(seed))
                {
                    continue;
                }

                List<GeoPoint3> loop = new List<GeoPoint3>();
                long cursor = seed;
                bool closed = false;

                for (int guard = 0; guard <= runs.Count; guard++)
                {
                    if (!runs.ContainsKey(cursor) || consumed.Contains(cursor))
                    {
                        break;
                    }

                    consumed.Add(cursor);
                    loop.AddRange(runs[cursor]);

                    if (!partner.TryGetValue(runEnds[cursor], out long next))
                    {
                        break;
                    }

                    if (next == seed)
                    {
                        closed = true;
                        break;
                    }

                    cursor = next;
                }

                if (closed || loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }

            return loops;
        }

        /// <summary>
        /// Packs a ring number and a position within it into one dictionary key.
        /// </summary>
        private static long MakeKey(int ring, int index) => ((long)ring << 32) | (uint)index;

        /// <summary>
        /// Unpacks a key made by <see cref="MakeKey"/>.
        /// </summary>
        private static void ReadKey(long key, out int ring, out int index)
        {
            ring = (int)(key >> 32);
            index = (int)(key & 0xFFFFFFFFL);
        }

        /// <summary>
        /// Checks whether any vertex of a rebuilt ring lies on the cutting plane.
        /// </summary>
        private static bool RingHasCrossing(List<int> sides)
        {
            foreach (int side in sides)
            {
                if (side == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the side an uncut ring lies on.
        /// </summary>
        private static int RingSide(List<int> sides)
        {
            foreach (int side in sides)
            {
                if (side != 0)
                {
                    return side;
                }
            }

            return 0;
        }

        /// <summary>
        /// Walks one ring and rebuilds it with a vertex inserted at every crossing.
        /// </summary>
        /// <param name="ring">The ring to walk.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="points">The rebuilt ring.</param>
        /// <param name="sides">Which side of the cutter each entry is on: 1, 0 or -1.</param>
        private static void BuildCutRing(IReadOnlyList<GeoPoint3> ring, GeoPlane3 cutter, Tolerance tolerance, out List<GeoPoint3> points, out List<int> sides)
        {
            points = new List<GeoPoint3>();
            sides = new List<int>();

            int count = ring.Count;

            for (int i = 0; i < count; i++)
            {
                GeoPoint3 current = ring[i];
                GeoPoint3 next = ring[(i + 1) % count];

                int currentSide = ToSign(Containment3.GetSide(cutter, current, tolerance));
                int nextSide = ToSign(Containment3.GetSide(cutter, next, tolerance));

                points.Add(current);
                sides.Add(currentSide);

                // Only a genuine crossing needs a new vertex. An edge that merely ends on the plane
                // already has one there.
                if (currentSide != 0 && nextSide != 0 && currentSide != nextSide &&
                    Intersection3.TryIntersectWith(new GeoLine3(current, next), cutter, out GeoPoint3 hit, tolerance))
                {
                    points.Add(hit);
                    sides.Add(0);
                }
            }
        }

        /// <summary>
        /// Maps a plane side onto the sign used while walking a ring.
        /// </summary>
        private static int ToSign(PlaneSide side)
        {
            if (side == PlaneSide.Above)
            {
                return 1;
            }

            return side == PlaneSide.Below ? -1 : 0;
        }

        #endregion

        #region Solid

        /// <summary>
        /// Splits a solid by a plane, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoSolid3 solid, GeoPlane3 cutter, out GeoSolid3 above, out GeoSolid3 below)
        {
            return TrySplitBy(solid, cutter, out above, out below, Tolerance.Global);
        }

        /// <summary>
        /// Splits a solid by a plane, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid to cut; its boundary must be closed and wound outwards.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="above">The piece on the side the cutter normal points towards.</param>
        /// <param name="below">The piece on the other side.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the plane misses the solid or only grazes it.</returns>
        /// <remarks>
        /// The body may be concave and its faces may carry holes. Each face is cut on its own, and the new
        /// surface closing each half is built from the edges the cut left behind: every such edge is
        /// traversed once by the face beside it, so the same edge traversed the other way belongs to the
        /// cap. Chaining those reversed edges gives the section, which may be several loops and may have
        /// one loop inside another — cutting a hollow tube leaves a ring, not a disc — so the loops are
        /// nested into faces rather than assumed to be a single boundary.
        /// <para>
        /// An edge shared by two faces that both survive on the same side is traversed both ways among the
        /// collected edges and cancels out, which is what keeps interior edges from being mistaken for
        /// section boundary.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoSolid3 solid, GeoPlane3 cutter, out GeoSolid3 above, out GeoSolid3 below, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            above = solid;
            below = solid;

            List<GeoFace3> upperFaces = new List<GeoFace3>();
            List<GeoFace3> lowerFaces = new List<GeoFace3>();

            foreach (GeoFace3 face in solid.Faces)
            {
                if (TrySplitBy(face, cutter, out GeoFace3[] faceAbove, out GeoFace3[] faceBelow, tolerance))
                {
                    upperFaces.AddRange(faceAbove);
                    lowerFaces.AddRange(faceBelow);
                    continue;
                }

                bool touchesAbove = faceAbove.Length > 0;
                bool touchesBelow = faceBelow.Length > 0;

                // A face lying in the cutting plane is reported on both sides and belongs to neither: the
                // cap replaces it.
                if (touchesAbove && touchesBelow)
                {
                    continue;
                }

                if (touchesAbove)
                {
                    upperFaces.Add(face);
                }
                else if (touchesBelow)
                {
                    lowerFaces.Add(face);
                }
            }

            if (upperFaces.Count == 0 || lowerFaces.Count == 0)
            {
                return false;
            }

            // Each half is capped from its own rim rather than from the other half turned over. The two
            // rims usually describe the same shape, but not when the cutting plane holds a face of the body
            // already: there the two halves meet the plane over different areas, and one cap cannot serve
            // for both. Cutting an L-shaped prism along the plane of its own notch is exactly that case.
            if (!TryBuildCaps(upperFaces, cutter, cutter.Normal.Negate(), tolerance, out List<GeoFace3> upperCaps))
            {
                return false;
            }

            if (!TryBuildCaps(lowerFaces, cutter, cutter.Normal, tolerance, out List<GeoFace3> lowerCaps))
            {
                return false;
            }

            upperFaces.AddRange(upperCaps);
            lowerFaces.AddRange(lowerCaps);

            if (upperFaces.Count < 4 || lowerFaces.Count < 4)
            {
                return false;
            }

            above = new GeoSolid3(upperFaces);
            below = new GeoSolid3(lowerFaces);
            return true;
        }

        /// <summary>
        /// Builds the faces closing off one half of a cut solid.
        /// </summary>
        /// <param name="halfFaces">The faces of that half, cap excluded.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="outward">The direction the cap should face, which is out of the half it closes.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="caps">The faces closing the half.</param>
        /// <returns>false when the edges left by the cut do not close into loops.</returns>
        private static bool TryBuildCaps(List<GeoFace3> halfFaces, GeoPlane3 cutter, GeoVector3 outward, Tolerance tolerance, out List<GeoFace3> caps)
        {
            caps = new List<GeoFace3>();

            List<GeoLine3> edges = new List<GeoLine3>();

            foreach (GeoFace3 face in halfFaces)
            {
                foreach (IReadOnlyList<GeoPoint3> ring in LoopAssembly.EnumerateMaterialRings(face))
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        GeoPoint3 from = ring[i];
                        GeoPoint3 to = ring[(i + 1) % ring.Count];

                        if (Containment3.GetSide(cutter, from, tolerance) != PlaneSide.On ||
                            Containment3.GetSide(cutter, to, tolerance) != PlaneSide.On)
                        {
                            continue;
                        }

                        // Reversed: in a closed surface the two faces meeting on an edge traverse it in
                        // opposite directions, so the cap runs against the face beside it.
                        edges.Add(new GeoLine3(to, from));
                    }
                }
            }

            LoopAssembly.CancelOpposedEdges(edges, tolerance);

            if (edges.Count < 3)
            {
                return false;
            }

            List<List<GeoPoint3>> loops = new List<List<GeoPoint3>>();

            foreach (GeoPolyline3 chain in Merge3.Join(edges, tolerance))
            {
                if (!chain.StartPoint.IsEqualTo(chain.EndPoint, tolerance))
                {
                    // An open chain means the surface did not close, so the section cannot be trusted.
                    return false;
                }

                loops.Add(new List<GeoPoint3>(chain.Vertices));
            }

            if (loops.Count == 0)
            {
                return false;
            }

            caps = LoopAssembly.AssembleFaces(loops, outward, tolerance);

            return caps.Count > 0;
        }

        #endregion

        #region Curve by a closed volume

        /// <summary>
        /// Splits a polyline by a solid, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoSolid3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by a solid, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutter">The body to cut it by; its boundary must be closed.</param>
        /// <param name="inside">The pieces lying within the body.</param>
        /// <param name="outside">The pieces lying beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain never crosses the surface, so there was nothing to cut.</returns>
        /// <remarks>
        /// A closed body has no side the way a plane does, so the pieces come back sorted into within and
        /// beyond rather than above and below. The chain is cut at every crossing of the surface, which is
        /// what makes each piece lie wholly on one side and lets a single sample decide which.
        /// <para>
        /// A piece lying on the surface counts as inside, following <c>Containment3.Contains</c>, which
        /// reads the boundary as part of the body. A chain running along a face is therefore reported as
        /// inside along its whole length.
        /// </para>
        /// <para>
        /// The body is assumed closed, as <c>Containment3.Locate</c> assumes it: an open shell has no
        /// inside and the answer means nothing. That is not checked here, because checking costs a pass
        /// over every edge of the body on every call; ask <see cref="GeoSolid3.IsClosed()"/> once instead.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoSolid3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            List<double> cuts = CollectSurfaceCrossings(subject, cutter, tolerance);

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => Containment3.Contains(cutter, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a line segment by a solid, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by a solid, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoSolid3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            List<double> cuts = CollectSurfaceCrossings(subject, cutter, 0.0, tolerance);

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => Containment3.Contains(cutter, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a polyline by an oriented box, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoObb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A box is a closed body like any other, but a far cheaper one to cross-check: the crossings come
        /// from the slab test rather than from walking a surface, so this does not build or traverse a mesh.
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoObb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            List<double> cuts = new List<double>();
            double travelled = 0.0;

            for (int i = 0; i < subject.EdgeCount; i++)
            {
                GeoLine3 edge = subject.GetEdgeAt(i);

                foreach (GeoPoint3 hit in Intersection3.GetIntersections(edge, cutter, tolerance))
                {
                    cuts.Add(travelled + Parametrization3.GetDistanceAtPoint(edge, hit));
                }

                travelled += edge.Length;
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => Containment3.Contains(cutter, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a line segment by an oriented box, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by an oriented box, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoObb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            List<double> cuts = new List<double>();

            foreach (GeoPoint3 hit in Intersection3.GetIntersections(subject, cutter, tolerance))
            {
                cuts.Add(Parametrization3.GetDistanceAtPoint(subject, hit));
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => Containment3.Contains(cutter, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a polyline by an axis-aligned box, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoAabb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An empty box holds nothing, so there is nothing to cut and the whole chain comes back as outside.
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoAabb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter.IsEmpty)
            {
                inside = new GeoPolyline3[0];
                outside = new[] { subject };
                return false;
            }

            return TrySplitBy(subject, cutter.ToObb(), out inside, out outside, tolerance);
        }

        /// <summary>
        /// Splits a line segment by an axis-aligned box, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by an axis-aligned box, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoAabb3 cutter, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            if (cutter.IsEmpty)
            {
                inside = new GeoLine3[0];
                outside = new[] { subject };
                return false;
            }

            return TrySplitBy(subject, cutter.ToObb(), out inside, out outside, tolerance);
        }

        /// <summary>
        /// Sorts the pieces of a cut chain into those the cutter holds and those it does not, joining any
        /// run of neighbours that ended up on the same side.
        /// </summary>
        /// <returns>false when everything landed on one side, meaning nothing was really cut.</returns>
        /// <remarks>
        /// Not every crossing of the surface separates inside from outside. A chain running down the shaft
        /// of an opening meets the caps at each end of that shaft without ever entering material, and a
        /// chain grazing a face touches it without going in. Cutting at those places and leaving the pieces
        /// apart would hand back a subject chopped at positions that mean nothing, so neighbours that agree
        /// are joined back up and what comes out is the longest run on each side.
        /// </remarks>
        private static bool SortPieces(GeoPolyline3[] pieces, Func<GeoPoint3, bool> isInside, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            // The chain was cut at every crossing, so each piece lies wholly on one side and the point
            // halfway along it speaks for the whole of it.
            bool[] within = new bool[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                within[i] = isInside(pieces[i].GetPointAtParameter(0.5));
            }

            List<GeoPolyline3> insideRuns = new List<GeoPolyline3>();
            List<GeoPolyline3> outsideRuns = new List<GeoPolyline3>();
            int runs = 0;

            int index = 0;
            while (index < pieces.Length)
            {
                bool state = within[index];
                int first = index;

                while (index < pieces.Length && within[index] == state)
                {
                    index++;
                }

                GeoPolyline3 run;

                if (index - first == 1)
                {
                    run = pieces[first];
                }
                else
                {
                    List<GeoPolyline3> neighbours = new List<GeoPolyline3>();
                    for (int i = first; i < index; i++)
                    {
                        neighbours.Add(pieces[i]);
                    }

                    run = Merge3.Polylines(neighbours);
                }

                (state ? insideRuns : outsideRuns).Add(run);
                runs++;
            }

            inside = insideRuns.ToArray();
            outside = outsideRuns.ToArray();

            return runs > 1;
        }

        /// <summary>
        /// Sorts the pieces of a cut segment into those the cutter holds and those it does not, joining any
        /// run of neighbours that ended up on the same side.
        /// </summary>
        /// <returns>false when everything landed on one side, meaning nothing was really cut.</returns>
        private static bool SortPieces(GeoLine3[] pieces, Func<GeoPoint3, bool> isInside, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            bool[] within = new bool[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                within[i] = isInside(pieces[i].MidPoint);
            }

            List<GeoLine3> insideRuns = new List<GeoLine3>();
            List<GeoLine3> outsideRuns = new List<GeoLine3>();
            int runs = 0;

            int index = 0;
            while (index < pieces.Length)
            {
                bool state = within[index];
                int first = index;

                while (index < pieces.Length && within[index] == state)
                {
                    index++;
                }

                // Every piece of a cut segment is collinear with the rest, so a run of them is just the
                // stretch from the start of the first to the end of the last.
                GeoLine3 run = new GeoLine3(pieces[first].StartPoint, pieces[index - 1].EndPoint);

                (state ? insideRuns : outsideRuns).Add(run);
                runs++;
            }

            inside = insideRuns.ToArray();
            outside = outsideRuns.ToArray();

            return runs > 1;
        }

        /// <summary>
        /// Collects where a chain crosses the surface of a body, as arc lengths from the start of the chain.
        /// </summary>
        private static List<double> CollectSurfaceCrossings(GeoPolyline3 subject, GeoSolid3 cutter, Tolerance tolerance)
        {
            List<double> cuts = new List<double>();
            double travelled = 0.0;

            for (int i = 0; i < subject.EdgeCount; i++)
            {
                GeoLine3 edge = subject.GetEdgeAt(i);
                cuts.AddRange(CollectSurfaceCrossings(edge, cutter, travelled, tolerance));
                travelled += edge.Length;
            }

            return cuts;
        }

        /// <summary>
        /// Collects where one segment crosses the surface of a body, as arc lengths offset by how far along
        /// a longer chain the segment starts.
        /// </summary>
        /// <remarks>
        /// The openings of the body are walked as well as its outer faces. An opening is a void, so its
        /// walls are as much a boundary between inside and outside as the outer surface is; missing them
        /// would leave a piece spanning both material and void, with nothing to say which it belongs to.
        /// <para>
        /// Faces whose bounding box cannot reach the segment are skipped before any real work is done,
        /// which is what keeps a chain against a large body from costing a full surface walk per edge.
        /// </para>
        /// </remarks>
        private static List<double> CollectSurfaceCrossings(GeoLine3 edge, GeoSolid3 cutter, double offset, Tolerance tolerance)
        {
            List<double> cuts = new List<double>();

            GeoAabb3 edgeBounds = new GeoAabb3(edge.StartPoint, edge.EndPoint);

            foreach (GeoFace3 face in cutter.Faces)
            {
                if (!face.GetAabb().CollidesWith(edgeBounds, tolerance))
                {
                    continue;
                }

                if (face.TryIntersectWith(edge, out GeoPoint3 hit, tolerance))
                {
                    cuts.Add(offset + Parametrization3.GetDistanceAtPoint(edge, hit));
                }
            }

            foreach (GeoSolid3 opening in cutter.Openings)
            {
                cuts.AddRange(CollectSurfaceCrossings(edge, opening, offset, tolerance));
            }

            return cuts;
        }

        #endregion

        #region Curve by a plane, sorted by side

        /// <summary>
        /// Splits a polyline by a plane and sorts the pieces by side, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoPlane3 cutter, out GeoPolyline3[] above, out GeoPolyline3[] below)
        {
            return TrySplitBy(subject, cutter, out above, out below, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by a plane and sorts the pieces by side, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="above">The pieces on the side the cutter normal points towards.</param>
        /// <param name="below">The pieces on the other side.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain stays on one side, so there was nothing to cut.</returns>
        /// <remarks>
        /// This is the same cut as the overload that hands back the pieces in order along the chain; what
        /// differs is how the result is presented. Use that one to divide a chain up, and this one to keep
        /// or discard a side.
        /// <para>
        /// A stretch lying in the cutting plane goes with <paramref name="above"/>, so that side reads as
        /// "not strictly below". That follows the convention the rest of the library keeps, where
        /// <c>Contains</c> means "not strictly outside" and a piece on a surface counts as inside it.
        /// </para>
        /// <para>
        /// Neighbouring pieces that end up on the same side are joined back together, so what comes out is
        /// the longest run on each side rather than a chain chopped at positions that separate nothing.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoPlane3 cutter, out GeoPolyline3[] above, out GeoPolyline3[] below, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            TrySplitBy(subject, cutter, out GeoPolyline3[] pieces, tolerance);

            return SortPieces(pieces, point => Containment3.GetSide(cutter, point, tolerance) != PlaneSide.Below, out above, out below);
        }

        /// <summary>
        /// Splits a line segment by a plane and sorts the pieces by side, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below)
        {
            return TrySplitBy(subject, cutter, out above, out below, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by a plane and sorts the pieces by side, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoPlane3 cutter, out GeoLine3[] above, out GeoLine3[] below, Tolerance tolerance)
        {
            TrySplitBy(subject, cutter, out GeoLine3[] pieces, tolerance);

            return SortPieces(pieces, point => Containment3.GetSide(cutter, point, tolerance) != PlaneSide.Below, out above, out below);
        }

        #endregion

        #region Curve by a bounded planar region

        /// <summary>
        /// Splits a polyline wherever it passes through a polygon, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoPolygon3 cutter, out GeoPolyline3[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline wherever it passes through a polygon, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutter">The region to cut it with.</param>
        /// <param name="pieces">The pieces, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain never passes through the region.</returns>
        /// <remarks>
        /// A bounded region is not the plane that carries it. This cuts only where the chain actually goes
        /// through the region, so a chain that crosses the carrying plane out beyond the outline is left
        /// alone — which is what is wanted when the cutter stands for a physical plate rather than for an
        /// endless surface. Cut against <see cref="GeoPlane3"/> instead to get the other reading.
        /// <para>
        /// The pieces come back in order along the chain rather than sorted, because a bounded region has
        /// no side to sort them onto: a chain can pass through it and come back without ever having been
        /// anywhere the region divides.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoPolygon3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            return TryCutAtCrossings(subject, (GeoLine3 edge, out GeoPoint3 hit) => Intersection3.TryIntersectWith(edge, cutter, out hit, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Splits a polyline wherever it passes through a face, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoFace3 cutter, out GeoPolyline3[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline wherever it passes through a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The holes in the face are respected: a chain threading through a hole passes through nothing and
        /// is not cut there.
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoFace3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            return TryCutAtCrossings(subject, (GeoLine3 edge, out GeoPoint3 hit) => cutter.TryIntersectWith(edge, out hit, tolerance), out pieces, tolerance);
        }

        /// <summary>
        /// Splits a line segment wherever it passes through a polygon, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoPolygon3 cutter, out GeoLine3[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment wherever it passes through a polygon, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoPolygon3 cutter, out GeoLine3[] pieces, Tolerance tolerance)
        {
            pieces = new[] { subject };

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            if (!Intersection3.TryIntersectWith(subject, cutter, out GeoPoint3 hit, tolerance))
            {
                return false;
            }

            return TrySplitBy(subject, hit, out pieces, tolerance);
        }

        /// <summary>
        /// Splits a line segment wherever it passes through a face, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoFace3 cutter, out GeoLine3[] pieces)
        {
            return TrySplitBy(subject, cutter, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment wherever it passes through a face, within a tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoFace3 cutter, out GeoLine3[] pieces, Tolerance tolerance)
        {
            pieces = new[] { subject };

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            if (!cutter.TryIntersectWith(subject, out GeoPoint3 hit, tolerance))
            {
                return false;
            }

            return TrySplitBy(subject, hit, out pieces, tolerance);
        }

        /// <summary>
        /// Cuts a chain wherever a per-segment test reports a crossing.
        /// </summary>
        /// <remarks>
        /// A segment can meet a flat region at most once, since the region lies in a single plane and the
        /// segment crosses that plane at most once, so one test per segment is enough.
        /// </remarks>
        private static bool TryCutAtCrossings(GeoPolyline3 subject, CrossingTest test, out GeoPolyline3[] pieces, Tolerance tolerance)
        {
            List<double> cuts = new List<double>();
            double travelled = 0.0;

            for (int i = 0; i < subject.EdgeCount; i++)
            {
                GeoLine3 edge = subject.GetEdgeAt(i);

                if (test(edge, out GeoPoint3 hit))
                {
                    cuts.Add(travelled + Parametrization3.GetDistanceAtPoint(edge, hit));
                }

                travelled += edge.Length;
            }

            pieces = SplitAtDistances(subject, cuts, tolerance);

            return pieces.Length > 1;
        }

        /// <summary>
        /// Asks whether one segment of a chain crosses the cutter, and where.
        /// </summary>
        private delegate bool CrossingTest(GeoLine3 edge, out GeoPoint3 hit);

        #endregion

        #region Region by a cut line

        /// <summary>
        /// Splits a polygon along a chain drawn across it, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolygon3 subject, GeoPolyline3 cutLine, out GeoPolygon3[] pieces)
        {
            return TrySplitBy(subject, cutLine, out pieces, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polygon along a chain drawn across it, within a tolerance.
        /// </summary>
        /// <param name="subject">The region to cut.</param>
        /// <param name="cutLine">The chain to cut along.</param>
        /// <param name="pieces">The two pieces the chain divides the region into.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain does not divide the region in two.</returns>
        /// <remarks>
        /// This is cutting a plate along a line marked on it. The chain has to lie in the plane of the
        /// region, start and finish on its outline, and stay inside in between — anything else does not
        /// separate the region into two and is refused rather than answered.
        /// <para>
        /// Each piece is bounded by part of the original outline and by the chain, so the two pieces share
        /// the chain as their common edge and their outlines together cover the original. Both keep the
        /// orientation of the subject.
        /// </para>
        /// <para>
        /// A chain that wanders back out of the region and in again would cut it into more than two, which
        /// needs the crossings paired up the way a plane cut pairs them; that is not attempted here.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolygon3 subject, GeoPolyline3 cutLine, out GeoPolygon3[] pieces, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutLine == null)
            {
                throw new ArgumentNullException(nameof(cutLine));
            }

            pieces = new[] { subject };

            GeoPlane3 carrier = subject.GetPlane();

            if (!carrier.ContainsAll(cutLine.Vertices, tolerance))
            {
                return false;
            }

            if (!Containment3.IsPointOn(subject, cutLine.StartPoint, tolerance) ||
                !Containment3.IsPointOn(subject, cutLine.EndPoint, tolerance))
            {
                return false;
            }

            if (cutLine.StartPoint.IsEqualTo(cutLine.EndPoint, tolerance))
            {
                return false;
            }

            // Everything between the two ends has to stay in the region, or the chain leaves and comes back
            // and divides it into more than two.
            for (int i = 1; i < cutLine.VertexCount - 1; i++)
            {
                if (Containment3.Locate(subject, cutLine[i], tolerance) == PointLocation.OutSide)
                {
                    return false;
                }
            }

            for (int i = 0; i < cutLine.EdgeCount; i++)
            {
                if (!Containment3.Contains(subject, cutLine.GetEdgeAt(i).MidPoint, tolerance))
                {
                    return false;
                }
            }

            double startDistance = Parametrization3.GetDistanceAtPoint(subject, cutLine.StartPoint, tolerance);
            double endDistance = Parametrization3.GetDistanceAtPoint(subject, cutLine.EndPoint, tolerance);

            List<GeoPoint3> first = BoundarySpan(subject, startDistance, endDistance, tolerance);
            List<GeoPoint3> second = BoundarySpan(subject, endDistance, startDistance, tolerance);

            // Each span already ends where the chain begins, so only the vertices between the ends of the
            // chain are added; repeating its endpoints would leave a zero-length edge at each corner.
            for (int i = cutLine.VertexCount - 2; i >= 1; i--)
            {
                first.Add(cutLine[i]);
            }

            for (int i = 1; i <= cutLine.VertexCount - 2; i++)
            {
                second.Add(cutLine[i]);
            }

            GeoPolygon3 firstPiece = LoopAssembly.TryBuildPolygon(first, subject.Normal, tolerance);
            GeoPolygon3 secondPiece = LoopAssembly.TryBuildPolygon(second, subject.Normal, tolerance);

            if (firstPiece == null || secondPiece == null)
            {
                return false;
            }

            pieces = new[] { firstPiece, secondPiece };
            return true;
        }

        /// <summary>
        /// Collects the run of a polygon outline from one arc length round to another, going forwards.
        /// </summary>
        /// <remarks>
        /// The outline is closed, so going forwards from the later position wraps past the first vertex and
        /// carries on. Measuring each vertex as how far round it is from the starting position turns that
        /// wrap into a plain comparison.
        /// </remarks>
        private static List<GeoPoint3> BoundarySpan(GeoPolygon3 polygon, double from, double to, Tolerance tolerance)
        {
            double perimeter = polygon.Length;

            List<GeoPoint3> span = new List<GeoPoint3>
            {
                Parametrization3.GetPointAtDistance(polygon, from)
            };

            double reach = Ahead(to - from, perimeter);

            // The vertices in between have to be added in the order they are met going forward, which is
            // not their index order once the starting position sits partway round the outline.
            List<double> aheads = new List<double>();
            List<GeoPoint3> candidates = new List<GeoPoint3>();

            double travelled = 0.0;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                travelled += polygon.GetEdgeAt(i).Length;

                // travelled is now the position of vertex i + 1 around the outline.
                double ahead = Ahead(travelled - from, perimeter);

                if (ahead > tolerance.EqualPoint && ahead < reach - tolerance.EqualPoint)
                {
                    aheads.Add(ahead);
                    candidates.Add(polygon[(i + 1) % polygon.VertexCount]);
                }
            }

            int[] order = new int[aheads.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (x, y) => aheads[x].CompareTo(aheads[y]));

            foreach (int index in order)
            {
                span.Add(candidates[index]);
            }

            span.Add(Parametrization3.GetPointAtDistance(polygon, to));

            return span;
        }

        /// <summary>
        /// Gets how far forward one position is from another around a closed curve.
        /// </summary>
        private static double Ahead(double difference, double perimeter)
        {
            if (perimeter <= 0.0)
            {
                return 0.0;
            }

            double ahead = difference % perimeter;

            return ahead < 0.0 ? ahead + perimeter : ahead;
        }

        #endregion

        #region Region by a closed volume

        /// <summary>
        /// Splits a polygon by a solid, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolygon3 subject, GeoSolid3 cutter, out GeoPolygon3[] inside, out GeoPolygon3[] outside)
        {
            return TrySplitBy(subject, cutter, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polygon by a solid, within a tolerance.
        /// </summary>
        /// <param name="subject">The region to cut.</param>
        /// <param name="cutter">The body to cut it by; its boundary must be closed.</param>
        /// <param name="inside">The pieces lying within the body.</param>
        /// <param name="outside">The pieces lying beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the body does not divide the region.</returns>
        /// <remarks>
        /// This answers which part of a plate is embedded in a body and which part stands clear of it.
        /// <para>
        /// The region is cut by the plane of each face of the body in turn. That works because the surface
        /// of the body never leaves those planes, so once the region has been cut by all of them no piece
        /// can straddle the boundary, and a single sample decides each piece. The pieces are then joined
        /// back up where they agree.
        /// </para>
        /// <para>
        /// Cutting by every plane divides the region more finely than the body itself does, and joining the
        /// pieces afterwards recovers most but not always all of that: two pieces that ended up split by
        /// different planes can meet at a T-junction, which nothing joins. What comes back therefore covers
        /// each side exactly, but may be in more pieces than strictly necessary.
        /// </para>
        /// <para>
        /// A body with many faces means many cuts, so this is meant for cutting a plate against a member,
        /// not against a whole model. Planes that miss the region cost almost nothing, since a cut that
        /// separates nothing is rejected before any work is done.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolygon3 subject, GeoSolid3 cutter, out GeoPolygon3[] inside, out GeoPolygon3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            inside = new GeoPolygon3[0];
            outside = new[] { subject };

            List<GeoPolygon3> pieces = new List<GeoPolygon3> { subject };

            foreach (GeoPlane3 plane in CollectFacePlanes(cutter, tolerance))
            {
                List<GeoPolygon3> divided = new List<GeoPolygon3>();

                foreach (GeoPolygon3 piece in pieces)
                {
                    if (TrySplitBy(piece, plane, out GeoPolygon3[] above, out GeoPolygon3[] below, tolerance))
                    {
                        divided.AddRange(above);
                        divided.AddRange(below);
                    }
                    else
                    {
                        divided.Add(piece);
                    }
                }

                pieces = divided;
            }

            List<GeoPolygon3> within = new List<GeoPolygon3>();
            List<GeoPolygon3> beyond = new List<GeoPolygon3>();

            foreach (GeoPolygon3 piece in pieces)
            {
                if (!TryGetInteriorPoint(piece, tolerance, out GeoPoint3 sample))
                {
                    // A sliver with no interior to sample carries no area either; dropping it would lose
                    // nothing, but keeping it on the outside preserves the promise that the pieces cover
                    // the subject.
                    beyond.Add(piece);
                    continue;
                }

                (Containment3.Contains(cutter, sample, tolerance) ? within : beyond).Add(piece);
            }

            if (within.Count == 0 || beyond.Count == 0)
            {
                inside = within.Count > 0 ? Rejoin(within, subject.Normal, tolerance) : new GeoPolygon3[0];
                outside = beyond.Count > 0 ? Rejoin(beyond, subject.Normal, tolerance) : new GeoPolygon3[0];
                return false;
            }

            inside = Rejoin(within, subject.Normal, tolerance);
            outside = Rejoin(beyond, subject.Normal, tolerance);
            return true;
        }

        /// <summary>
        /// Collects one plane per distinct flat surface of a body, its openings included.
        /// </summary>
        /// <remarks>
        /// Several faces of a body often share a plane, and a face and one facing the other way describe
        /// the same flat place. Cutting by each of them separately would divide the region again for no
        /// gain, so a plane already collected — in either direction — is not collected twice.
        /// </remarks>
        internal static List<GeoPlane3> CollectFacePlanes(GeoSolid3 solid, Tolerance tolerance)
        {
            List<GeoPlane3> planes = new List<GeoPlane3>();

            CollectFacePlanes(solid, tolerance, planes);

            return planes;
        }

        /// <summary>
        /// Adds the distinct face planes of a body and of its openings to a running list.
        /// </summary>
        private static void CollectFacePlanes(GeoSolid3 solid, Tolerance tolerance, List<GeoPlane3> planes)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                GeoPlane3 plane = face.GetPlane();
                bool known = false;

                foreach (GeoPlane3 existing in planes)
                {
                    if (existing.IsEqualTo(plane, tolerance) || existing.IsEqualTo(plane.Flip(), tolerance))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    planes.Add(plane);
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                CollectFacePlanes(opening, tolerance, planes);
            }
        }

        /// <summary>
        /// Finds a point strictly inside a polygon.
        /// </summary>
        /// <remarks>
        /// The centroid of a concave polygon can fall outside it, so it cannot be trusted as a sample. The
        /// triangles the polygon fans into are tried instead, and each candidate is checked against the
        /// polygon before it is accepted — for a concave polygon some of those triangles reach outside as
        /// well, and only the ones that do not are of any use.
        /// </remarks>
        private static bool TryGetInteriorPoint(GeoPolygon3 polygon, Tolerance tolerance, out GeoPoint3 point)
        {
            point = polygon[0];

            foreach (GeoTriangle3 triangle in polygon.Triangulate())
            {
                if (triangle.IsDegenerate(tolerance))
                {
                    continue;
                }

                GeoPoint3 candidate = triangle.Centroid;

                if (Containment3.Locate(polygon, candidate, tolerance) == PointLocation.Inside)
                {
                    point = candidate;
                    return true;
                }
            }

            GeoPoint3 centroid = polygon.Centroid;

            if (Containment3.Locate(polygon, centroid, tolerance) == PointLocation.Inside)
            {
                point = centroid;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Joins the pieces of one side back together wherever they touch.
        /// </summary>
        private static GeoPolygon3[] Rejoin(List<GeoPolygon3> pieces, GeoVector3 orientation, Tolerance tolerance)
        {
            if (pieces.Count < 2)
            {
                return pieces.ToArray();
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            foreach (GeoPolygon3 piece in pieces)
            {
                faces.Add(new GeoFace3(piece));
            }

            GeoFace3[] merged = Merge3.CoplanarFaces(faces, tolerance);

            List<GeoPolygon3> boundaries = new List<GeoPolygon3>();

            foreach (GeoFace3 face in merged)
            {
                // A merged piece with a hole cannot be given back as a plain polygon, so that group is left
                // as the pieces it came from rather than losing the hole.
                if (face.Holes.Count > 0)
                {
                    return pieces.ToArray();
                }

                boundaries.Add(face.Boundary);
            }

            return boundaries.ToArray();
        }

        #endregion
    }
}
