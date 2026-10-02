using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
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

            switch (Cut(face, cutter, tolerance, tolerance, out List<GeoFace3> upper, out List<GeoFace3> lower))
            {
                case FaceCut.InPlane:
                    // A face lying entirely in the cutting plane belongs to neither side; hand it back on both
                    // so the caller still has it.
                    above = whole;
                    below = whole;
                    return false;

                case FaceCut.Above:
                    above = whole;
                    below = none;
                    return false;

                case FaceCut.Below:
                    above = none;
                    below = whole;
                    return false;
            }

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
        /// How a face lies against a cutting plane.
        /// </summary>
        private enum FaceCut
        {
            /// <summary>Every corner is on the plane.</summary>
            InPlane,

            /// <summary>
            /// On the side the normal points to, touching the plane at most; or crossing it so nearly along it,
            /// for the face's size, that there is no line to cut along.
            /// </summary>
            Above,

            /// <summary>On the other side, touching the plane at most.</summary>
            Below,

            /// <summary>Crossed, with the pieces on each side handed back.</summary>
            Crossed,
        }

        /// <summary>
        /// Cuts a face by a plane, handing back the pieces on each side when the plane crosses it.
        /// </summary>
        /// <param name="face">The face to cut, holes included.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="tolerance">The tolerance deciding which side each corner is on and where the edges cross.</param>
        /// <param name="pieces">The tolerance the pieces are built within; see <see cref="LoopAssembly.ForPieces"/>.</param>
        /// <param name="above">The pieces on the side the cutter normal points towards, when crossed; null otherwise.</param>
        /// <param name="below">The pieces on the other side, when crossed; null otherwise.</param>
        /// <returns>How the face lies against the plane.</returns>
        /// <remarks>
        /// A crossed face can hand back nothing on one side: a piece that thin is not a polygon at all. The
        /// caller decides what that means, which is why the pieces are handed back as they are rather than the
        /// whole face in their place.
        /// </remarks>
        private static FaceCut Cut(GeoFace3 face, GeoPlane3 cutter, Tolerance tolerance, Tolerance pieces, out List<GeoFace3> above, out List<GeoFace3> below)
        {
            above = null;
            below = null;

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

            if (!anyAbove && !anyBelow)
            {
                return FaceCut.InPlane;
            }

            if (!anyBelow)
            {
                return FaceCut.Above;
            }

            if (!anyAbove)
            {
                return FaceCut.Below;
            }

            // The corners already show the plane passing through the face, so what decides whether there is a
            // line to cut along is how far the face reaches, not the angle between the two planes.
            GeoAabb3 box = face.GetAabb();
            double extent = box.Min.DistanceTo(box.Max);

            if (!Intersection3.TryGetMeetingLine(face.GetPlane(), cutter, extent, out GeoRay3 cutLine, tolerance))
            {
                return FaceCut.Above;
            }

            above = LoopAssembly.AssembleFaces(CutRings(face, cutter, cutLine, 1, tolerance), face.Normal, pieces);
            below = LoopAssembly.AssembleFaces(CutRings(face, cutter, cutLine, -1, tolerance), face.Normal, pieces);
            return FaceCut.Crossed;
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
                ResolveTouches(sides, wantedSide, points, face.Normal, cutter.Normal);
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

            // The crossings are already in order along the cut line, and walking the runs in that order
            // rather than in whatever order the dictionary hands them back keeps the pieces reproducible.
            // A dictionary makes no promise about the order it enumerates in, and the shape of the answer
            // should not rest on one.
            foreach (long seed in crossings)
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

                // Only a loop that came back to where it started bounds a piece. A walk that ran out of
                // chain describes nothing, and taking it anyway would hand back a piece whose outline is
                // open — which reads as plausible geometry and is not. Dropping it costs the caller a
                // false from the split, which hands the subject back whole.
                if (closed)
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
        /// Tells apart the vertices on the cutting plane that the ring really crosses at from those it
        /// only touches, and reclassifies the ones that are not crossings.
        /// </summary>
        /// <remarks>
        /// A vertex sitting on the plane does not by itself mean the boundary passes through to the other
        /// side. It may only have come down to the plane and gone back the way it came — the tip of a
        /// notch resting exactly on the cut, a corner grazing it. Reading such a vertex as a crossing
        /// costs more than an extra entry in the list: the crossings are paired off in order along the cut
        /// line on the understanding that they alternate entering and leaving the material, so one touch
        /// makes the count odd and leaves the last crossing without a partner, and two of them pair a
        /// touch with a real crossing. Either way the walk that follows runs off the end of a chain, and
        /// the pieces come back overlapping — a body cut that way gains volume out of nothing while both
        /// halves still report themselves closed.
        /// <para>
        /// What settles it is the side the ring is on before reaching the plane against the side it is on
        /// after leaving: the same side means a touch, opposite sides mean a crossing. A run of vertices
        /// on the plane — an edge lying along the cut rather than a single corner — is judged as one unit
        /// for the same reason, since the whole run is one visit to the plane.
        /// </para>
        /// <para>
        /// Where a run crosses, the stretch it lies along is boundary of the side whose material it borders, and
        /// that is what decides which end of it is the crossing: the end the other side leaves the plane from.
        /// Cutting an L along the plane of its own notch, the piece the notch edge borders runs along that edge
        /// to its far end, and the piece on the other side stops at the near end, which is where the two meet.
        /// Picking the end the other way round would leave the other half with a spur of zero width running out
        /// to a vertex that is not on it.
        /// </para>
        /// <para>
        /// It is the same end for both sides, so the two pieces of a face share their crossings, pair them the
        /// same way, and meet along the same edges. Picked by the side being built instead, the piece the run
        /// borders stepped over it straight from the other end, which is the same line only while the run lies
        /// exactly in the plane. A run within the tolerance of it is not: a notch edge within a thousandth of a
        /// millimetre of the plane, at the end of a cut seventy metres long, left a sliver nine ten-thousandths
        /// across between the pieces of a slab's faces, which belonged to neither half, and the two halves were ten
        /// thousand cubic millimetres short of the slab.
        /// </para>
        /// </remarks>
        /// <param name="sides">The side of each ring entry; anything that is not a crossing is rewritten in place.</param>
        /// <param name="wantedSide">The side the piece being built lies on.</param>
        /// <param name="points">The ring the sides are of, walked with the material on its left.</param>
        /// <param name="faceNormal">The normal of the face the ring bounds, which fixes what its left is.</param>
        /// <param name="cutterNormal">The normal of the cutting plane, pointing to the side 1 stands for.</param>
        private static void ResolveTouches(List<int> sides, int wantedSide, List<GeoPoint3> points, GeoVector3 faceNormal, GeoVector3 cutterNormal)
        {
            int count = sides.Count;
            int start = -1;

            for (int i = 0; i < count; i++)
            {
                if (sides[i] != 0)
                {
                    start = i;
                    break;
                }
            }

            // A ring lying wholly in the cutting plane has no side to be on, so there is no touch to tell
            // from a crossing and nothing here can improve on leaving it alone.
            if (start < 0)
            {
                return;
            }

            int offset = 0;

            while (offset < count)
            {
                if (sides[(start + offset) % count] != 0)
                {
                    offset++;
                    continue;
                }

                // The run of entries on the plane that begins here. It cannot swallow the whole ring,
                // because the walk started from an entry that is not on the plane.
                int length = 0;
                while (sides[(start + offset + length) % count] == 0)
                {
                    length++;
                }

                int before = sides[(start + offset - 1 + count) % count];
                int after = sides[(start + offset + length) % count];

                if (before == after && wantedSide == before && length > 1)
                {
                    // The ring came to the plane, ran along it, and went back the way it came. The stretch
                    // it ran is a piece of boundary lying on the cut, so for the side it belongs to both
                    // of its ends bound the piece and the vertices between them are ordinary corners.
                    // This is the rim of a hole meeting the cut, and an outer edge lying along it.
                    for (int k = 1; k < length - 1; k++)
                    {
                        sides[(start + offset + k) % count] = before;
                    }
                }
                else if (before == after)
                {
                    // Either a touch at a single vertex, which separates nothing, or a stretch belonging
                    // to the other side. Neither bounds the piece being built, so no crossing is recorded
                    // and every entry takes the side the ring was on throughout.
                    for (int k = 0; k < length; k++)
                    {
                        sides[(start + offset + k) % count] = before;
                    }
                }
                else if (BordersSide(points, (start + offset) % count, (start + offset + length - 1) % count, faceNormal, cutterNormal, wantedSide == before ? after : before) == before)
                {
                    // A crossing, and the run is boundary of the side before it: the piece before the run runs
                    // along it to its far end, and the piece beyond begins there.
                    for (int k = 0; k < length - 1; k++)
                    {
                        sides[(start + offset + k) % count] = before;
                    }
                }
                else
                {
                    // A crossing, and the run is boundary of the side beyond it: the piece before the run ends
                    // where it begins, and the piece beyond runs along it from there.
                    for (int k = 1; k < length; k++)
                    {
                        sides[(start + offset + k) % count] = after;
                    }
                }

                offset += length;
            }
        }

        /// <summary>
        /// Gets the side of a cutting plane whose material a run of ring entries lying on the plane borders.
        /// </summary>
        /// <param name="points">The ring, walked with the material on its left.</param>
        /// <param name="first">Where the run begins.</param>
        /// <param name="last">Where it ends.</param>
        /// <param name="faceNormal">The normal of the face the ring bounds.</param>
        /// <param name="cutterNormal">The normal of the cutting plane.</param>
        /// <param name="fallback">The side to answer for a run with no length to judge by.</param>
        /// <returns>1 for the side the cutter normal points to, -1 for the other.</returns>
        private static int BordersSide(List<GeoPoint3> points, int first, int last, GeoVector3 faceNormal, GeoVector3 cutterNormal, int fallback)
        {
            GeoVector3 along = points[first].GetVectorTo(points[last]);

            // The material lies on the left of the walk: the face normal crossed with the way the run goes.
            double toward = faceNormal.CrossProduct(along).DotProduct(cutterNormal);

            if (Math.Abs(toward) <= 1E-12 * along.Length || along.Length == 0.0)
            {
                return fallback;
            }

            return toward > 0.0 ? 1 : -1;
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
        /// <para>
        /// A plane that parts a body without crossing any of it — between two blocks of one body, or along the
        /// edge where two parts of it meet, as a body cut through a corner of its notch touches itself — leaves
        /// every edge on the plane run both ways within each half. Each half is closed as it is, and the body is
        /// split with nothing to cap. A plane crossing the body where it is thinner than the point tolerance leaves
        /// the two sides of the section within the tolerance of each other, and their edges cancel too; but they are
        /// two edges, not one, and the halves would be open by the sliver between them, so the body is not split.
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

            if (!TryCut(solid, cutter, tolerance, out List<GeoFace3> upperFaces, out List<GeoFace3> lowerFaces, out List<GeoFace3> upperCaps, out List<GeoFace3> lowerCaps, out _))
            {
                return false;
            }

            upperFaces.AddRange(upperCaps);
            lowerFaces.AddRange(lowerCaps);

            SplitOpenings(solid, cutter, tolerance, out List<GeoSolid3> upperOpenings, out List<GeoSolid3> lowerOpenings);

            above = new GeoSolid3(upperFaces, upperOpenings);
            below = new GeoSolid3(lowerFaces, lowerOpenings);
            return true;
        }

        /// <summary>
        /// Gets where a plane cuts a body's faces: the faces closing the half below it, facing along the plane's normal,
        /// where the body has material on both sides of the plane.
        /// </summary>
        /// <param name="solid">The body, closed and wound outwards; its openings are not read.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One face per region of the cut; none where the plane misses the body, only grazes it, or parts it.</returns>
        /// <remarks>
        /// Each half is capped where the plane meets it, and the halves meet the plane over the same area wherever the plane
        /// crosses the body. Where it lies along a face of the body, the half that face bounds is capped there and the other
        /// is not: the section is what both caps cover. A half with no cap at all, as a plane between two blocks of a body
        /// leaves, makes no section.
        /// </remarks>
        internal static GeoFace3[] Section(GeoSolid3 solid, GeoPlane3 cutter, Tolerance tolerance)
        {
            if (!TryCut(solid, cutter, tolerance, out _, out _, out List<GeoFace3> upperCaps, out List<GeoFace3> lowerCaps, out bool along)
                || upperCaps.Count == 0 || lowerCaps.Count == 0)
            {
                return new GeoFace3[0];
            }

            // With no face of the body in the plane, every edge the cut leaves bounds both halves, and the two caps are one.
            return along ? Common(lowerCaps, upperCaps, cutter, tolerance) : lowerCaps.ToArray();
        }

        /// <summary>
        /// What the caps of the half below a plane and of the half above both cover, facing along the plane's normal.
        /// </summary>
        private static GeoFace3[] Common(List<GeoFace3> lowerCaps, List<GeoFace3> upperCaps, GeoPlane3 cutter, Tolerance tolerance)
        {
            var frame = new GeoCoordinateSystem3(cutter);
            var common = new List<GeoFace3>();

            foreach (GeoFace3 lower in lowerCaps)
            {
                GeoFace2 a = LaidOut(lower, frame);

                if (a == null)
                {
                    continue;
                }

                foreach (GeoFace3 upper in upperCaps)
                {
                    GeoFace2 b = LaidOut(upper, frame);

                    if (b == null)
                    {
                        continue;
                    }

                    foreach (GeoFace2 both in Boolean2.Intersect(a, b, tolerance))
                    {
                        GeoPolygon3 boundary = Lifted(both.Boundary, frame);

                        if (boundary == null)
                        {
                            continue;
                        }

                        var holes = new List<GeoPolygon3>(both.Holes.Count);

                        foreach (GeoPolygon2 hole in both.Holes)
                        {
                            GeoPolygon3 lifted = Lifted(hole, frame);

                            if (lifted != null)
                            {
                                holes.Add(lifted);
                            }
                        }

                        common.Add(new GeoFace3(boundary, holes, tolerance));
                    }
                }
            }

            return common.ToArray();
        }

        /// <summary>
        /// A face lying in the plane of a frame laid out in it, each ring counter-clockwise; null when the boundary encloses
        /// nothing there.
        /// </summary>
        private static GeoFace2 LaidOut(GeoFace3 face, GeoCoordinateSystem3 frame)
        {
            GeoPolygon2 boundary = CounterClockwise(MeshLift3.LayOutPolygon(MeshLift3.LayOut(frame, face.Boundary.Vertices)));

            if (boundary == null)
            {
                return null;
            }

            var holes = new List<GeoPolygon2>(face.Holes.Count);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                GeoPolygon2 laid = CounterClockwise(MeshLift3.LayOutPolygon(MeshLift3.LayOut(frame, hole.Vertices)));

                if (laid != null)
                {
                    holes.Add(laid);
                }
            }

            return new GeoFace2(boundary, holes);
        }

        /// <summary>
        /// A polygon wound counter-clockwise, turned round as it is rather than through a constructor that would check it
        /// against the global tolerance; null for null.
        /// </summary>
        private static GeoPolygon2 CounterClockwise(GeoPolygon2 polygon)
        {
            if (polygon == null || !(polygon.SignedArea < 0.0))
            {
                return polygon;
            }

            var turned = new GeoPoint2[polygon.VertexCount];

            for (int i = 0; i < turned.Length; i++)
            {
                turned[i] = polygon[turned.Length - 1 - i];
            }

            return new GeoPolygon2(turned, turned.Length);
        }

        /// <summary>
        /// A ring of the plane of a frame put back into space, facing along the frame's Z axis; null when it encloses
        /// nothing.
        /// </summary>
        private static GeoPolygon3 Lifted(GeoPolygon2 ring, GeoCoordinateSystem3 frame)
        {
            GeoPolygon2 counter = CounterClockwise(ring);
            double area = counter.SignedArea;

            if (!(area > 0.0))
            {
                return null;
            }

            var corners = new GeoPoint3[counter.VertexCount];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = frame.ToGlobal(new GeoPoint3(counter[i].X, counter[i].Y, 0.0));
            }

            return GeoPolygon3.FromValidated(corners, frame.ZAxis, area);
        }

        /// <summary>
        /// Cuts every face of a body by a plane and caps each half from its own rim.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="upperFaces">The faces and pieces of faces on the side the normal points to, caps left out.</param>
        /// <param name="lowerFaces">Those on the other side.</param>
        /// <param name="upperCaps">The faces closing the half above; none where it touches the plane over no area.</param>
        /// <param name="lowerCaps">The faces closing the half below.</param>
        /// <param name="along">Whether a face of the body lies in the plane.</param>
        /// <returns>false when the plane misses the body or only grazes it, or a half's rim does not close.</returns>
        private static bool TryCut(
            GeoSolid3 solid,
            GeoPlane3 cutter,
            Tolerance tolerance,
            out List<GeoFace3> upperFaces,
            out List<GeoFace3> lowerFaces,
            out List<GeoFace3> upperCaps,
            out List<GeoFace3> lowerCaps,
            out bool along)
        {
            upperFaces = new List<GeoFace3>();
            lowerFaces = new List<GeoFace3>();
            upperCaps = null;
            lowerCaps = null;
            along = false;
            Tolerance pieces = LoopAssembly.ForPieces(tolerance);

            foreach (GeoFace3 face in solid.Faces)
            {
                switch (Cut(face, cutter, tolerance, pieces, out List<GeoFace3> faceAbove, out List<GeoFace3> faceBelow))
                {
                    case FaceCut.InPlane:
                        // A face lying in the cutting plane belongs to neither side: the cap replaces it.
                        along = true;
                        break;

                    case FaceCut.Above:
                        upperFaces.Add(face);
                        break;

                    case FaceCut.Below:
                        lowerFaces.Add(face);
                        break;

                    default:
                        // Each side takes its own pieces and only those. The whole face on one side would reach
                        // past the plane, and dropped from both it would leave a hole in each; either way a rim
                        // that does not close. A piece too thin to be a polygon even here has its two corners on
                        // the plane within the point tolerance of each other, so the faces beside it meet along
                        // the same stretch without it and the half still closes.
                        upperFaces.AddRange(faceAbove);
                        lowerFaces.AddRange(faceBelow);
                        break;
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
            if (!TryBuildCaps(upperFaces, cutter, cutter.Normal.Negate(), tolerance, pieces, out upperCaps))
            {
                return false;
            }

            if (!TryBuildCaps(lowerFaces, cutter, cutter.Normal, tolerance, pieces, out lowerCaps))
            {
                return false;
            }

            return upperFaces.Count + upperCaps.Count >= 4 && lowerFaces.Count + lowerCaps.Count >= 4;
        }

        /// <summary>
        /// Sorts the openings of a solid into the two halves the cut leaves.
        /// </summary>
        /// <remarks>
        /// An opening is a body in its own right, so one straddling the plane is cut by the same method
        /// and each half keeps its piece. One the plane misses belongs whole to the side it sits on, which
        /// its centroid settles: the plane does not pass through it, so every point of it is on one side
        /// and the centroid is as good as any.
        /// <para>
        /// Without this the openings would be dropped and each half would come back as solid material
        /// where the body had a duct or a recess — a loss with nothing to report it, since the halves are
        /// closed and measure correctly in every other respect.
        /// </para>
        /// </remarks>
        private static void SplitOpenings(GeoSolid3 solid, GeoPlane3 cutter, Tolerance tolerance, out List<GeoSolid3> upper, out List<GeoSolid3> lower)
        {
            upper = new List<GeoSolid3>();
            lower = new List<GeoSolid3>();

            foreach (GeoSolid3 opening in solid.Openings)
            {
                if (TrySplitBy(opening, cutter, out GeoSolid3 openingAbove, out GeoSolid3 openingBelow, tolerance))
                {
                    upper.Add(openingAbove);
                    lower.Add(openingBelow);
                    continue;
                }

                if (cutter.SignedDistanceTo(opening.Centroid) >= 0.0)
                {
                    upper.Add(opening);
                }
                else
                {
                    lower.Add(opening);
                }
            }
        }

        /// <summary>
        /// Builds the faces closing off one half of a cut solid.
        /// </summary>
        /// <param name="halfFaces">The faces of that half, cap excluded.</param>
        /// <param name="cutter">The cutting plane.</param>
        /// <param name="outward">The direction the cap should face, which is out of the half it closes.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="pieces">The tolerance the caps are built within; see <see cref="LoopAssembly.ForPieces"/>.</param>
        /// <param name="caps">The faces closing the half.</param>
        /// <returns>
        /// false when the edges left by the cut do not close into loops, or cancel only as the two sides of a sliver thinner
        /// than the point tolerance do; true with no caps when the half has no edge on the plane its own faces do not run
        /// both ways.
        /// </returns>
        private static bool TryBuildCaps(List<GeoFace3> halfFaces, GeoPlane3 cutter, GeoVector3 outward, Tolerance tolerance, Tolerance pieces, out List<GeoFace3> caps)
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

            int found = edges.Count;
            double scale = 0.0;

            foreach (GeoLine3 edge in edges)
            {
                GeoPoint3 start = edge.StartPoint;
                scale = Math.Max(scale, Math.Max(Math.Abs(start.X), Math.Max(Math.Abs(start.Y), Math.Abs(start.Z))));
            }

            LoopAssembly.CancelOpposedEdges(edges, tolerance, out double widest);

            if (edges.Count == 0)
            {
                // Every edge the half has on the plane run both ways by its own faces: the plane passes between parts of
                // the body, or along the edge where two parts meet, and the half is closed as it is, with nothing to cap.
                // Two faces sharing an edge run it both ways but for the rounding of the points the cut put on it, which
                // grows with how far out the body lies; two edges further apart are the two sides of a section thinner
                // than the point tolerance, and the half is open there by a sliver no cap can close.
                return found == 0 || widest <= Math.Max(1E-9, 1E-13 * scale);
            }

            if (edges.Count < 3)
            {
                return false;
            }

            // The cap is traced in the cutting plane, and it must be traced the way the finished cap will
            // face, or a vertex where several rim edges meet would send the walk onto the wrong outline.
            if (!LoopAssembly.TryChainLoops(edges, outward, tolerance, out List<List<GeoPoint3>> loops))
            {
                // An open chain means the surface did not close, so the section cannot be trusted.
                return false;
            }

            // A plane taking a corner off leaves a cap as small as the slivers beside it, and one refused would
            // leave the half open.
            caps = LoopAssembly.AssembleFaces(loops, outward, pieces);

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
        /// Splits a polyline by several solids at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoSolid3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by several solids at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutters">The bodies to cut it by; null entries are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the bodies.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain crosses no surface, so there was nothing to cut.</returns>
        /// <remarks>
        /// The bodies act as their union: a stretch counts as inside when any one of them holds it. That
        /// is what separates this from cutting by each in turn, where the pieces of the first cut would
        /// have to be sorted again against the second and the runs joined back up by hand.
        /// <para>
        /// Because the union is what is being asked about, two bodies that overlap do not each claim their
        /// own piece of the answer, and two that meet face to face do not leave a cut between them: the
        /// stretch running through both comes back as one piece, since a cut that separates nothing is not
        /// a cut. An empty array is the degenerate case of the same rule — nothing holds anything, so the
        /// whole chain is outside.
        /// </para>
        /// <para>
        /// Every body is assumed closed, as <c>Containment3.Locate</c> assumes it. That is not checked
        /// here, because checking costs a pass over every edge of every body on every call; ask
        /// <see cref="GeoSolid3.IsClosed()"/> once instead.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoSolid3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            // The crossings of every body go into one list. Sorting them and dropping those too close
            // together is what SplitAtDistances already does, so nothing here has to care that they
            // arrive out of order or that two bodies meeting on a face cross the chain at the same place.
            List<double> cuts = new List<double>();

            foreach (GeoSolid3 cutter in cutters)
            {
                if (cutter != null)
                {
                    cuts.AddRange(CollectSurfaceCrossings(subject, cutter, tolerance));
                }
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => IsInsideAny(cutters, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a line segment by several solids at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoSolid3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by several solids at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The segment to cut.</param>
        /// <param name="cutters">The bodies to cut it by; null entries are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the bodies.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the segment crosses no surface, so there was nothing to cut.</returns>
        /// <remarks>
        /// The bodies act as their union, exactly as in the polyline overload above.
        /// </remarks>
        public static bool TrySplitBy(GeoLine3 subject, GeoSolid3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            List<double> cuts = new List<double>();

            foreach (GeoSolid3 cutter in cutters)
            {
                if (cutter != null)
                {
                    cuts.AddRange(CollectSurfaceCrossings(subject, cutter, 0.0, tolerance));
                }
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => IsInsideAny(cutters, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Checks whether any of the bodies holds a point.
        /// </summary>
        /// <remarks>
        /// Several cutters together behave as their union, which is why the search stops at the first body
        /// that holds the point. Null entries are skipped rather than refused, so a caller can pass a
        /// sparse array without filtering it first.
        /// </remarks>
        private static bool IsInsideAny(GeoSolid3[] cutters, GeoPoint3 point, Tolerance tolerance)
        {
            foreach (GeoSolid3 cutter in cutters)
            {
                if (cutter != null && Containment3.Contains(cutter, point, tolerance))
                {
                    return true;
                }
            }

            return false;
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
        /// Splits a polyline by several oriented boxes at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoObb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by several oriented boxes at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutters">The boxes to cut it by; null entries are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the boxes.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain crosses no box, so there was nothing to cut.</returns>
        /// <remarks>
        /// The boxes act as their union, exactly as several solids do, and for the same reason: cutting by
        /// each in turn would leave the caller to sort the pieces of the first cut against the second and
        /// to join the runs back up by hand.
        /// <para>
        /// A box is far cheaper to cross-check than a solid, because the crossings come from the slab test
        /// rather than from walking a surface, so no mesh is built or traversed here.
        /// </para>
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoObb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            List<double> cuts = new List<double>();
            double travelled = 0.0;

            // The distance along the chain is what the cuts are expressed in, so the walk over the edges
            // stays outermost and every box is asked about the edge the walk is currently on.
            for (int i = 0; i < subject.EdgeCount; i++)
            {
                GeoLine3 edge = subject.GetEdgeAt(i);

                foreach (GeoObb3 cutter in cutters)
                {
                    if (cutter == null)
                    {
                        continue;
                    }

                    foreach (GeoPoint3 hit in Intersection3.GetIntersections(edge, cutter, tolerance))
                    {
                        cuts.Add(travelled + Parametrization3.GetDistanceAtPoint(edge, hit));
                    }
                }

                travelled += edge.Length;
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => IsInsideAny(cutters, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a line segment by several oriented boxes at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoObb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by several oriented boxes at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The segment to cut.</param>
        /// <param name="cutters">The boxes to cut it by; null entries are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the boxes.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the segment crosses no box, so there was nothing to cut.</returns>
        /// <remarks>
        /// The boxes act as their union, exactly as in the polyline overload above.
        /// </remarks>
        public static bool TrySplitBy(GeoLine3 subject, GeoObb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            List<double> cuts = new List<double>();

            foreach (GeoObb3 cutter in cutters)
            {
                if (cutter == null)
                {
                    continue;
                }

                foreach (GeoPoint3 hit in Intersection3.GetIntersections(subject, cutter, tolerance))
                {
                    cuts.Add(Parametrization3.GetDistanceAtPoint(subject, hit));
                }
            }

            return SortPieces(SplitAtDistances(subject, cuts, tolerance), point => IsInsideAny(cutters, point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Splits a polyline by several axis-aligned boxes at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoAabb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a polyline by several axis-aligned boxes at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The chain to cut.</param>
        /// <param name="cutters">The boxes to cut it by; empty boxes are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the boxes.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the chain crosses no box, so there was nothing to cut.</returns>
        /// <remarks>
        /// The boxes act as their union, exactly as several solids do. An axis-aligned box is a value
        /// rather than a reference, so there are no null entries to skip here; what takes their place is
        /// the empty box, which holds nothing and so can be left out before the work starts.
        /// </remarks>
        public static bool TrySplitBy(GeoPolyline3 subject, GeoAabb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            return TrySplitBy(subject, ToBoxes(cutters), out inside, out outside, tolerance);
        }

        /// <summary>
        /// Splits a line segment by several axis-aligned boxes at once, using the default tolerance.
        /// </summary>
        public static bool TrySplitBy(GeoLine3 subject, GeoAabb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside)
        {
            return TrySplitBy(subject, cutters, out inside, out outside, Tolerance.Global);
        }

        /// <summary>
        /// Splits a line segment by several axis-aligned boxes at once, within a tolerance.
        /// </summary>
        /// <param name="subject">The segment to cut.</param>
        /// <param name="cutters">The boxes to cut it by; empty boxes are skipped.</param>
        /// <param name="inside">The pieces lying within at least one of the boxes.</param>
        /// <param name="outside">The pieces lying beyond all of them.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the segment crosses no box, so there was nothing to cut.</returns>
        /// <remarks>
        /// The boxes act as their union, exactly as in the polyline overload above.
        /// </remarks>
        public static bool TrySplitBy(GeoLine3 subject, GeoAabb3[] cutters, out GeoLine3[] inside, out GeoLine3[] outside, Tolerance tolerance)
        {
            return TrySplitBy(subject, ToBoxes(cutters), out inside, out outside, tolerance);
        }

        /// <summary>
        /// Turns a list of axis-aligned boxes into oriented ones, dropping those that hold nothing.
        /// </summary>
        /// <remarks>
        /// An empty box has no corners to orient, so it cannot be carried over; leaving it out is the same
        /// answer as carrying it, since it could never hold a point or be crossed.
        /// </remarks>
        private static GeoObb3[] ToBoxes(GeoAabb3[] cutters)
        {
            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            List<GeoObb3> boxes = new List<GeoObb3>(cutters.Length);

            foreach (GeoAabb3 cutter in cutters)
            {
                if (!cutter.IsEmpty)
                {
                    boxes.Add(cutter.ToObb());
                }
            }

            return boxes.ToArray();
        }

        /// <summary>
        /// Checks whether any of the boxes holds a point.
        /// </summary>
        /// <remarks>
        /// Several cutters together behave as their union, which is why the search stops at the first box
        /// that holds the point. Null entries are skipped rather than refused, so a caller can pass a
        /// sparse array without filtering it first.
        /// </remarks>
        private static bool IsInsideAny(GeoObb3[] cutters, GeoPoint3 point, Tolerance tolerance)
        {
            foreach (GeoObb3 cutter in cutters)
            {
                if (cutter != null && Containment3.Contains(cutter, point, tolerance))
                {
                    return true;
                }
            }

            return false;
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
