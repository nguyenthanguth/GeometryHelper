using System;
using System.Collections.Generic;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    // The internal machinery shared by every public split operation. Kept in its own half of the
    // partial class so the other half reads as the API surface and nothing else.
    //
    // Every split reduces to the same shape: turn whatever the caller supplied — a point, a distance, a
    // cutting line, a polygon — into a list of arc lengths measured along the subject, clean that list
    // up, then walk the subject once and cut it there. Only the first step differs between operations,
    // so only the first step is written more than once.
    //
    // The type summary lives on the other half: a partial class that carries one on each half has both
    // copied into the generated documentation, where a reader sees whichever the tooling picks.
    public static partial class Splition2
    {
        private static readonly double[] NoCuts = new double[0];
        private static readonly GeoLine2[] NoLines = new GeoLine2[0];
        private static readonly GeoPolyline2[] NoPolylines = new GeoPolyline2[0];

        /// <summary>
        /// Reports whether a point falls within any of the cutters, counting the boundary as within.
        /// </summary>
        /// <remarks>
        /// Several cutters together behave as their union: a position is inside when any one of them
        /// holds it, which is why the search stops at the first that does. Null entries are skipped
        /// rather than refused, so a caller can pass a sparse array without filtering it first.
        /// </remarks>
        internal static bool IsInsideAny(GeoPolygon2[] cutters, GeoPoint2 point, Tolerance tolerance)
        {
            foreach (GeoPolygon2 polygon in cutters)
            {
                if (polygon != null && Containment2.Contains(polygon, point, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gathers every point at which any of the cutters meets a line segment.
        /// </summary>
        internal static GeoPoint2[] CollectCrossings(GeoPolygon2[] cutters, GeoLine2 subject, Tolerance tolerance)
        {
            var crossings = new List<GeoPoint2>();
            foreach (GeoPolygon2 polygon in cutters)
            {
                if (polygon != null)
                {
                    crossings.AddRange(Intersection2.GetIntersections(polygon, subject, tolerance));
                }
            }

            return crossings.ToArray();
        }

        /// <summary>
        /// Gathers every point at which any of the cutters meets a polyline.
        /// </summary>
        internal static GeoPoint2[] CollectCrossings(GeoPolygon2[] cutters, GeoPolyline2 subject, Tolerance tolerance)
        {
            var crossings = new List<GeoPoint2>();
            foreach (GeoPolygon2 polygon in cutters)
            {
                if (polygon != null)
                {
                    crossings.AddRange(Intersection2.GetIntersections(subject, polygon, tolerance));
                }
            }

            return crossings.ToArray();
        }


        /// <summary>
        /// Puts a caller supplied set of cut positions into the form the walker expects: ascending, free of
        /// positions closer together than the tolerance, and free of positions at or beyond either end.
        /// </summary>
        /// <param name="totalLength">Arc length of the subject being cut.</param>
        /// <param name="distances">Raw cut positions, in any order.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The cleaned positions, possibly empty.</returns>
        /// <remarks>
        /// The three rules applied here are what keep every downstream piece longer than the tolerance,
        /// which in turn keeps the results clear of the degenerate cases that make the GeoPolyline2
        /// constructor throw. Cutting exactly at an endpoint is not a split, so those positions are
        /// dropped rather than producing an empty piece.
        /// </remarks>
        internal static double[] NormalizeCuts(double totalLength, IEnumerable<double> distances, Tolerance tolerance)
        {
            if (distances == null) throw new ArgumentNullException(nameof(distances));

            double margin = tolerance.EqualPoint;

            var kept = new List<double>();
            foreach (double distance in distances)
            {
                // NaN survives every comparison below, so it has to be rejected on its own.
                if (double.IsNaN(distance)) continue;
                if (distance <= margin) continue;
                if (distance >= totalLength - margin) continue;

                kept.Add(distance);
            }

            if (kept.Count == 0)
            {
                return NoCuts;
            }

            kept.Sort();

            var result = new List<double>(kept.Count) { kept[0] };
            for (int i = 1; i < kept.Count; i++)
            {
                if (kept[i] - result[result.Count - 1] > margin)
                {
                    result.Add(kept[i]);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Converts a point into the arc length at which the subject should be cut, rejecting points that
        /// do not lie on the subject.
        /// </summary>
        /// <remarks>
        /// A point off the subject is refused rather than projected onto it. Projecting would always
        /// succeed and would silently cut somewhere the caller never asked for.
        /// </remarks>
        internal static bool TryGetCutDistance(GeoLine2 source, GeoPoint2 point, Tolerance tolerance, out double distance)
        {
            distance = 0.0;
            if (!Containment2.IsPointOn(source, point, tolerance))
            {
                return false;
            }

            distance = Parametrization2.GetDistanceAtPoint(source, point);
            return true;
        }

        /// <summary>
        /// Converts a point into the arc length at which the subject should be cut, rejecting points that
        /// do not lie on the subject.
        /// </summary>
        internal static bool TryGetCutDistance(GeoPolyline2 source, GeoPoint2 point, Tolerance tolerance, out double distance)
        {
            distance = 0.0;
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!Containment2.IsPointOn(source, point, tolerance))
            {
                return false;
            }

            distance = Parametrization2.GetDistanceAtPoint(source, point);
            return true;
        }

        /// <summary>
        /// Drops the cut positions where the subject does not actually change sides.
        /// </summary>
        /// <param name="totalLength">Arc length of the subject.</param>
        /// <param name="cuts">Normalized cut positions.</param>
        /// <param name="isInsideAt">Reports whether the subject is inside the cutter at an arc length.</param>
        /// <param name="insideOfPiece">
        /// Which side each surviving piece falls on, one entry longer than the returned positions.
        /// </param>
        /// <returns>The positions that are real crossings, a subset of <paramref name="cuts"/>.</returns>
        /// <remarks>
        /// A region cutter can meet the subject without the subject passing through it: grazing a corner,
        /// or touching at a vertex and turning back. Those meetings are intersection points like any
        /// other, so they arrive here as cut positions, and cutting there would leave two pieces on the
        /// same side of the cutter with a seam between them for no reason.
        /// <para>
        /// Rather than trying to recognise each way that can happen, this samples the midpoint of every
        /// interval and keeps only the positions where the answer differs on the two sides. Tangency,
        /// corner grazing and a vertex resting on the boundary all resolve themselves, with no special
        /// case for any of them.
        /// </para>
        /// <para>
        /// The classification is handed back rather than left to be recomputed on the surviving pieces.
        /// Probing a merged piece would sample its midpoint, and when a dropped position sat halfway
        /// along it — which is exactly what happens when a path touches the boundary and turns back at
        /// its own midpoint — that probe lands on the boundary and reports the whole piece as inside.
        /// The samples taken here sit strictly between crossings, so they never have that problem.
        /// </para>
        /// </remarks>
        internal static double[] KeepCrossings(
            double totalLength, double[] cuts, Func<double, bool> isInsideAt, out bool[] insideOfPiece)
        {
            if (cuts.Length == 0)
            {
                insideOfPiece = new[] { isInsideAt(totalLength * 0.5) };
                return cuts;
            }

            // One classification per interval: cuts.Length + 1 of them, the last running to the end.
            var inside = new bool[cuts.Length + 1];
            double previous = 0.0;
            for (int i = 0; i < cuts.Length; i++)
            {
                inside[i] = isInsideAt((previous + cuts[i]) * 0.5);
                previous = cuts[i];
            }
            inside[cuts.Length] = isInsideAt((previous + totalLength) * 0.5);

            var kept = new List<double>(cuts.Length);
            for (int i = 0; i < cuts.Length; i++)
            {
                if (inside[i] != inside[i + 1])
                {
                    kept.Add(cuts[i]);
                }
            }

            // Only positions where the side changes survive, so the pieces they leave behind strictly
            // alternate and the first one inherits the side of the very first interval.
            insideOfPiece = new bool[kept.Count + 1];
            bool current = inside[0];
            for (int i = 0; i < insideOfPiece.Length; i++)
            {
                insideOfPiece[i] = current;
                current = !current;
            }

            return kept.Count == cuts.Length ? cuts : kept.ToArray();
        }

        /// <summary>
        /// Turns caller supplied points into normalized cut positions, dropping any that do not lie on
        /// the subject.
        /// </summary>
        /// <remarks>
        /// This is the difference between a point the caller chose and a point an intersection produced.
        /// An intersection point is on the subject by construction, so <see cref="ToCutDistances(GeoLine2, GeoPoint2[], Tolerance)"/>
        /// converts it without asking. A point handed in by the caller has to be checked, because
        /// GetDistanceAtPoint answers for the nearest position on the subject whether or not the point is
        /// anywhere near it — cutting there would be cutting somewhere nobody asked for. The single point
        /// overloads have always refused such a point, and the array overloads have to agree.
        /// </remarks>
        internal static double[] ToCutDistancesFromPoints(GeoLine2 subject, GeoPoint2[] points, Tolerance tolerance)
        {
            if (points.Length == 0)
            {
                return NoCuts;
            }

            var distances = new List<double>(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                if (TryGetCutDistance(subject, points[i], tolerance, out double distance))
                {
                    distances.Add(distance);
                }
            }

            return NormalizeCuts(subject.Length, distances, tolerance);
        }

        /// <summary>
        /// Turns caller supplied points into normalized cut positions, dropping any that do not lie on
        /// the subject.
        /// </summary>
        internal static double[] ToCutDistancesFromPoints(GeoPolyline2 subject, GeoPoint2[] points, Tolerance tolerance)
        {
            if (points.Length == 0)
            {
                return NoCuts;
            }

            var distances = new List<double>(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                if (TryGetCutDistance(subject, points[i], tolerance, out double distance))
                {
                    distances.Add(distance);
                }
            }

            return NormalizeCuts(subject.Length, distances, tolerance);
        }

        /// <summary>
        /// Turns intersection points into normalized cut positions along a line segment.
        /// </summary>
        internal static double[] ToCutDistances(GeoLine2 subject, GeoPoint2[] crossings, Tolerance tolerance)
        {
            if (crossings.Length == 0)
            {
                return NoCuts;
            }

            var distances = new double[crossings.Length];
            for (int i = 0; i < crossings.Length; i++)
            {
                distances[i] = Parametrization2.GetDistanceAtPoint(subject, crossings[i]);
            }

            return NormalizeCuts(subject.Length, distances, tolerance);
        }

        /// <summary>
        /// Turns intersection points into normalized cut positions along a polyline.
        /// </summary>
        internal static double[] ToCutDistances(GeoPolyline2 subject, GeoPoint2[] crossings, Tolerance tolerance)
        {
            if (crossings.Length == 0)
            {
                return NoCuts;
            }

            var distances = new double[crossings.Length];
            for (int i = 0; i < crossings.Length; i++)
            {
                distances[i] = Parametrization2.GetDistanceAtPoint(subject, crossings[i]);
            }

            return NormalizeCuts(subject.Length, distances, tolerance);
        }

        /// <summary>
        /// Cuts a line segment at already normalized arc lengths.
        /// </summary>
        internal static GeoLine2[] SplitLineAt(GeoLine2 source, double[] cuts)
        {
            if (cuts.Length == 0)
            {
                return new[] { source };
            }

            var pieces = new GeoLine2[cuts.Length + 1];
            GeoPoint2 previous = source.StartPoint;

            for (int i = 0; i < cuts.Length; i++)
            {
                GeoPoint2 cutPoint = Parametrization2.GetPointAtDistance(source, cuts[i]);
                pieces[i] = new GeoLine2(previous, cutPoint);
                previous = cutPoint;
            }

            pieces[cuts.Length] = new GeoLine2(previous, source.EndPoint);
            return pieces;
        }

        /// <summary>
        /// Cuts a polyline at already normalized arc lengths.
        /// </summary>
        /// <remarks>
        /// N cuts yield N + 1 pieces.
        /// </remarks>
        internal static GeoPolyline2[] SplitPolylineAt(GeoPolyline2 source, double[] cuts, Tolerance tolerance)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (cuts.Length == 0)
            {
                return new[] { source };
            }

            BuildStations(source, cuts, tolerance, out GeoPoint2[] stations, out int[] boundaries);

            if (boundaries.Length == 0)
            {
                return new[] { source };
            }

            var pieces = new GeoPolyline2[boundaries.Length + 1];
            pieces[0] = Slice(stations, 0, boundaries[0]);
            for (int i = 1; i < boundaries.Length; i++)
            {
                pieces[i] = Slice(stations, boundaries[i - 1], boundaries[i]);
            }
            pieces[boundaries.Length] = Slice(stations, boundaries[boundaries.Length - 1], stations.Length - 1);

            return pieces;
        }

        /// <summary>
        /// Walks the subject once and records every position the result needs: its own vertices plus the
        /// requested cut positions, in ascending arc length order.
        /// </summary>
        /// <param name="source">The subject being cut.</param>
        /// <param name="cuts">Normalized cut positions.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="stations">All recorded positions, in order.</param>
        /// <param name="boundaries">Indices into <paramref name="stations"/> at which the subject is cut, ascending and distinct.</param>
        /// <remarks>
        /// A cut landing within tolerance of an existing vertex is snapped onto that vertex instead of
        /// becoming a station of its own. Without this, a cut a hair past a vertex would produce a piece
        /// only a hair long, which is exactly the degenerate result the tolerance exists to prevent.
        /// </remarks>
        private static void BuildStations(
            GeoPolyline2 source, double[] cuts, Tolerance tolerance,
            out GeoPoint2[] stations, out int[] boundaries)
        {
            int edgeCount = source.EdgeCount;
            double margin = tolerance.EqualPoint;

            var points = new List<GeoPoint2>(edgeCount + 1 + cuts.Length) { source[0] };
            var cutStation = new List<int>(cuts.Length);

            double accumulated = 0.0;
            int nextCut = 0;

            for (int i = 0; i < edgeCount; i++)
            {
                GeoLine2 edge = source.GetEdgeAt(i);
                double edgeLength = edge.Length;
                double edgeEnd = accumulated + edgeLength;

                // Cuts landing strictly inside this edge, far enough from its end vertex to stand alone.
                while (nextCut < cuts.Length && cuts[nextCut] < edgeEnd - margin)
                {
                    double into = cuts[nextCut] - accumulated;
                    GeoPoint2 cutPoint = edgeLength > 0.0
                        ? edge.GetPointAtParameter(into / edgeLength)
                        : edge.StartPoint;

                    cutStation.Add(points.Count);
                    points.Add(cutPoint);
                    nextCut++;
                }

                accumulated = edgeEnd;
                points.Add(edge.EndPoint);

                // Anything left within tolerance of this vertex snaps onto it.
                while (nextCut < cuts.Length && cuts[nextCut] <= edgeEnd + margin)
                {
                    cutStation.Add(points.Count - 1);
                    nextCut++;
                }
            }

            stations = points.ToArray();
            int lastStation = stations.Length - 1;

            // Two cuts straddling the same vertex are far enough apart to survive merging yet both close
            // enough to snap onto it, which would leave an empty piece between them.
            //
            // The end vertex checks cannot trigger while NormalizeCuts keeps every position a full
            // tolerance clear of both ends, but Slice would build a single vertex polyline if they ever
            // did, so they stay as a guard on that assumption rather than a live case.
            var distinct = new List<int>(cutStation.Count);
            foreach (int station in cutStation)
            {
                if (station <= 0 || station >= lastStation) continue;
                if (distinct.Count > 0 && distinct[distinct.Count - 1] == station) continue;
                distinct.Add(station);
            }

            boundaries = distinct.ToArray();
        }

        /// <summary>
        /// Builds one piece from a run of consecutive stations.
        /// </summary>
        private static GeoPolyline2 Slice(GeoPoint2[] stations, int from, int to)
        {
            int count = to - from + 1;
            var vertices = new GeoPoint2[count];
            Array.Copy(stations, from, vertices, 0, count);
            return new GeoPolyline2(vertices, count);
        }
    }
}
