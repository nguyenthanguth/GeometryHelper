using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Reshapes the corners of a chain or a polygon, as AutoCAD's CHAMFER does.
    /// <para>
    /// A chamfer replaces a corner with one straight cut across it, measured back along each of the two
    /// edges that meet there. What comes back is the kind of shape that went in, because cutting a corner
    /// square adds no curvature: a polygon gives a polygon, a chain gives a chain.
    /// </para>
    /// <para>
    /// <see cref="Lengthen2.TryTrimExtendToCorner(GeoLine2, GeoLine2, out GeoLine2, out GeoLine2)"/> is the other end of the same family: it builds a
    /// corner where two loose segments would meet, rather than cutting one that already exists.
    /// </para>
    /// </summary>
    public static partial class Corner2
    {
        #region Public API

        /// <summary>
        /// Chamfers every corner of a polygon that has room for it, cutting the same distance along both
        /// edges, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance">How far back along each edge the cut is measured.</param>
        /// <returns>The chamfered polygon.</returns>
        public static GeoPolygon2 Chamfer(GeoPolygon2 polygon, double distance) => Chamfer(polygon, distance, distance, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner of a polygon that has room for it, cutting the same distance along both
        /// edges, within a tolerance.
        /// </summary>
        public static GeoPolygon2 Chamfer(GeoPolygon2 polygon, double distance, Tolerance tolerance) => Chamfer(polygon, distance, distance, tolerance);

        /// <summary>
        /// Chamfers every corner of a polygon that has room for it, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <returns>The chamfered polygon.</returns>
        public static GeoPolygon2 Chamfer(GeoPolygon2 polygon, double distance1, double distance2) => Chamfer(polygon, distance1, distance2, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner of a polygon that has room for it, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="tolerance">The tolerance: a corner straighter than <see cref="Tolerance.EqualAngleRad"/> is left alone.</param>
        /// <returns>The chamfered polygon: a corner that was cut becomes two vertices, one that was left alone stays one.</returns>
        /// <remarks>
        /// Which way round the two distances go follows the way the polygon runs, so reversing it swaps
        /// them. A corner is left alone when its cut would not fit, and what was skipped and why is written
        /// to <see cref="GeometryHelperLog"/>; see the class remarks for the rules.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolygon2 Chamfer(GeoPolygon2 polygon, double distance1, double distance2, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            List<GeoPoint2> vertices = Build(polygon.Vertices, true, distance1, distance2, tolerance, "polygon");

            return vertices == null ? polygon.Clone() : new GeoPolygon2(vertices);
        }

        /// <summary>
        /// Chamfers every corner of a chain that has room for it, cutting the same distance along both
        /// edges, using the default tolerance.
        /// </summary>
        public static GeoPolyline2 Chamfer(GeoPolyline2 polyline, double distance) => Chamfer(polyline, distance, distance, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner of a chain that has room for it, cutting the same distance along both
        /// edges, within a tolerance.
        /// </summary>
        public static GeoPolyline2 Chamfer(GeoPolyline2 polyline, double distance, Tolerance tolerance) => Chamfer(polyline, distance, distance, tolerance);

        /// <summary>
        /// Chamfers every corner of a chain that has room for it, using the default tolerance.
        /// </summary>
        public static GeoPolyline2 Chamfer(GeoPolyline2 polyline, double distance1, double distance2) => Chamfer(polyline, distance1, distance2, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner of a chain that has room for it, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="tolerance">The tolerance: a corner straighter than <see cref="Tolerance.EqualAngleRad"/> is left alone.</param>
        /// <returns>The chamfered chain, which keeps both of its end points exactly where they were.</returns>
        /// <remarks>
        /// A chain has corners only where it turns, so its two end points are never touched.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolyline2 Chamfer(GeoPolyline2 polyline, double distance1, double distance2, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            List<GeoPoint2> vertices = Build(polyline.Vertices, false, distance1, distance2, tolerance, "chain");

            return vertices == null ? polyline.Clone() : new GeoPolyline2(vertices);
        }

        /// <summary>
        /// Chamfers one corner of a polygon, using the default tolerance.
        /// </summary>
        public static bool TryChamferAt(GeoPolygon2 polygon, int index, double distance1, double distance2, out GeoPolygon2 result)
        {
            return TryChamferAt(polygon, index, distance1, distance2, out result, Tolerance.Global);
        }

        /// <summary>
        /// Chamfers one corner of a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="index">Which vertex to cut.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="result">The chamfered polygon, or the polygon unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the polygon or either distance is not a positive number.</exception>
        public static bool TryChamferAt(GeoPolygon2 polygon, int index, double distance1, double distance2, out GeoPolygon2 result, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            if (index < 0 || index >= polygon.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            result = polygon;

            if (!TryCut(polygon.Vertices, true, index, distance1, distance2, tolerance, out GeoPoint2 from, out GeoPoint2 to))
            {
                return false;
            }

            var vertices = new List<GeoPoint2>(polygon.VertexCount + 1);

            for (int i = 0; i < polygon.VertexCount; i++)
            {
                if (i == index)
                {
                    vertices.Add(from);
                    vertices.Add(to);
                }
                else
                {
                    vertices.Add(polygon[i]);
                }
            }

            result = new GeoPolygon2(vertices);
            return true;
        }

        /// <summary>
        /// Chamfers one corner of a chain, using the default tolerance.
        /// </summary>
        public static bool TryChamferAt(GeoPolyline2 polyline, int index, double distance1, double distance2, out GeoPolyline2 result)
        {
            return TryChamferAt(polyline, index, distance1, distance2, out result, Tolerance.Global);
        }

        /// <summary>
        /// Chamfers one corner of a chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="index">Which vertex to cut; the two ends have no corner and are refused.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="result">The chamfered chain, or the chain unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain or either distance is not a positive number.</exception>
        public static bool TryChamferAt(GeoPolyline2 polyline, int index, double distance1, double distance2, out GeoPolyline2 result, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (index < 0 || index >= polyline.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            result = polyline;

            // The ends of a chain are not corners: nothing turns there.
            if (index == 0 || index == polyline.VertexCount - 1)
            {
                return false;
            }

            if (!TryCut(polyline.Vertices, false, index, distance1, distance2, tolerance, out GeoPoint2 from, out GeoPoint2 to))
            {
                return false;
            }

            var vertices = new List<GeoPoint2>(polyline.VertexCount + 1);

            for (int i = 0; i < polyline.VertexCount; i++)
            {
                if (i == index)
                {
                    vertices.Add(from);
                    vertices.Add(to);
                }
                else
                {
                    vertices.Add(polyline[i]);
                }
            }

            result = new GeoPolyline2(vertices);
            return true;
        }

        #endregion

        #region Building

        /// <summary>
        /// Cuts every corner that has room for it and returns the vertices of the result, or null when no
        /// corner could be cut and the caller should hand back what it was given.
        /// </summary>
        /// <remarks>
        /// Every cut is measured on the shape as it came in, never on a shape half cut already: measuring
        /// as it goes would make the answer depend on which vertex the walk happened to start at.
        /// <para>
        /// An edge can only give away as much as it is long, and two neighbouring corners both eat into
        /// the edge between them. Where they ask for more than it has, the corner asking for more on that
        /// edge is dropped, and dropping it may free enough for others, so the check runs again until every
        /// edge is within its length. Each pass drops at least one corner, so it ends.
        /// </para>
        /// </remarks>
        private static List<GeoPoint2> Build(
            IReadOnlyList<GeoPoint2> vertices,
            bool closed,
            double distance1,
            double distance2,
            Tolerance tolerance,
            string what)
        {
            int count = vertices.Count;
            bool[] cut = new bool[count];
            GeoPoint2[] from = new GeoPoint2[count];
            GeoPoint2[] to = new GeoPoint2[count];

            int first = closed ? 0 : 1;
            int last = closed ? count - 1 : count - 2;
            int straight = 0;
            int tooShort = 0;

            for (int i = first; i <= last; i++)
            {
                if (TryCut(vertices, closed, i, distance1, distance2, tolerance, out from[i], out to[i], out bool wasStraight))
                {
                    cut[i] = true;
                }
                else if (wasStraight)
                {
                    straight++;
                }
                else
                {
                    tooShort++;
                }
            }

            tooShort += DropCornersThatShareTooLittleEdge(vertices, closed, cut, distance1, distance2, tolerance);

            int cutCount = 0;
            foreach (bool one in cut)
            {
                if (one) cutCount++;
            }

            if (straight > 0 || tooShort > 0)
            {
                GeometryHelperLog.Debug(
                    $"Chamfer left {straight + tooShort} corner(s) of the {what} alone: " +
                    $"{tooShort} had too little edge to cut, {straight} were straight.");
            }

            if (cutCount == 0)
            {
                return null;
            }

            var result = new List<GeoPoint2>(count + cutCount);

            for (int i = 0; i < count; i++)
            {
                if (cut[i])
                {
                    result.Add(from[i]);
                    result.Add(to[i]);
                }
                else
                {
                    result.Add(vertices[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Drops corners until no edge is asked to give away more than its length, and returns how many
        /// were dropped.
        /// </summary>
        private static int DropCornersThatShareTooLittleEdge(
            IReadOnlyList<GeoPoint2> vertices,
            bool closed,
            bool[] cut,
            double distance1,
            double distance2,
            Tolerance tolerance)
        {
            int count = vertices.Count;
            int edgeCount = closed ? count : count - 1;
            int dropped = 0;
            bool again = true;

            while (again)
            {
                again = false;

                for (int e = 0; e < edgeCount; e++)
                {
                    int start = e;
                    int end = (e + 1) % count;

                    // The corner at the start of this edge takes distance2 from it; the one at its end
                    // takes distance1.
                    double taken = (cut[start] ? distance2 : 0.0) + (cut[end] ? distance1 : 0.0);

                    // Exactly enough is enough, and the dust left by measuring the edge must not decide
                    // otherwise: a square of side one hundred takes a chamfer of fifty at every corner.
                    if (taken <= vertices[start].DistanceTo(vertices[end]) + tolerance.EqualPoint)
                    {
                        continue;
                    }

                    // Drop whichever of the two takes more of this edge; on a tie, the earlier one, so the
                    // answer does not depend on where the walk began.
                    int victim = !cut[start] ? end
                               : !cut[end] ? start
                               : distance2 >= distance1 ? start
                               : end;

                    cut[victim] = false;
                    dropped++;
                    again = true;
                }
            }

            return dropped;
        }

        private static bool TryCut(
            IReadOnlyList<GeoPoint2> vertices,
            bool closed,
            int index,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint2 from,
            out GeoPoint2 to)
        {
            return TryCut(vertices, closed, index, distance1, distance2, tolerance, out from, out to, out _);
        }

        private static bool TryCut(
            IReadOnlyList<GeoPoint2> vertices,
            bool closed,
            int index,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint2 from,
            out GeoPoint2 to,
            out bool straight)
        {
            int count = vertices.Count;
            from = default;
            to = default;
            straight = false;

            if (!closed && (index == 0 || index == count - 1))
            {
                return false;
            }

            GeoPoint2 previous = vertices[(index - 1 + count) % count];
            GeoPoint2 corner = vertices[index];
            GeoPoint2 next = vertices[(index + 1) % count];

            GeoVector2 back = corner.GetVectorTo(previous);
            GeoVector2 forward = corner.GetVectorTo(next);

            double backLength = back.Length;
            double forwardLength = forward.Length;

            if (backLength <= tolerance.EqualPoint || forwardLength <= tolerance.EqualPoint)
            {
                return false;
            }

            // A corner that does not turn has nothing to cut off: the two points would land on one line and
            // the cut would be an edge of zero length.
            double sine = Math.Abs(back.CrossProduct(forward)) / (backLength * forwardLength);

            if (sine <= Math.Sin(tolerance.EqualAngleRad))
            {
                straight = true;
                return false;
            }

            if (distance1 > backLength + tolerance.EqualPoint || distance2 > forwardLength + tolerance.EqualPoint)
            {
                return false;
            }

            from = corner.Add(back.Multiply(distance1 / backLength));
            to = corner.Add(forward.Multiply(distance2 / forwardLength));
            return true;
        }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "A chamfer distance must be a positive number.");
            }
        }

        #endregion

        #region Chains that carry arcs

        /// <summary>
        /// Chamfers every corner of a chain that has room for it, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chamfered chain, its arcs untouched.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolylineArc2 Chamfer(GeoPolylineArc2 polyline, double distance1, double distance2, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            return Reshape(polyline, null, distance1, distance2, tolerance);
        }

        /// <summary>
        /// Chamfers every corner of a loop that has room for it, within a tolerance.
        /// </summary>
        /// <returns>The chamfered loop, its arcs untouched.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either distance is not a positive number.</exception>
        public static GeoPolygonArc2 Chamfer(GeoPolygonArc2 polygon, double distance1, double distance2, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            return Reshape(polygon, null, distance1, distance2, tolerance);
        }

        /// <summary>
        /// Rounds every corner of a chain that has room for it with an arc of a given radius, within a
        /// tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radius">The radius of the arcs.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The filleted chain.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public static GeoPolylineArc2 Fillet(GeoPolylineArc2 polyline, double radius, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            RequirePositive(radius, nameof(radius));

            return Reshape(polyline, Everywhere(radius, polyline.EdgeCount), 0.0, 0.0, tolerance);
        }

        /// <summary>
        /// Rounds every corner of a loop that has room for it with an arc of a given radius, within a
        /// tolerance.
        /// </summary>
        /// <returns>The filleted loop.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public static GeoPolygonArc2 Fillet(GeoPolygonArc2 polygon, double radius, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            RequirePositive(radius, nameof(radius));

            return Reshape(polygon, Everywhere(radius, polygon.EdgeCount), 0.0, 0.0, tolerance);
        }

        private static GeoPolylineArc2 Reshape(GeoPolylineArc2 polyline, double?[] radiusAtCorner, double distance1, double distance2, Tolerance tolerance)
        {
            var edges = new List<GeoEdge2>(polyline.EdgeCount);

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                edges.Add(polyline.GetEdgeAt(i));
            }

            return new GeoPolylineArc2(ReshapeCorners(edges, false, radiusAtCorner, distance1, distance2, tolerance));
        }

        private static GeoPolygonArc2 Reshape(GeoPolygonArc2 polygon, double?[] radiusAtCorner, double distance1, double distance2, Tolerance tolerance)
        {
            var edges = new List<GeoEdge2>(polygon.EdgeCount);

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                edges.Add(polygon.GetEdgeAt(i));
            }

            List<GeoEdge2> reshaped = ReshapeCorners(edges, true, radiusAtCorner, distance1, distance2, tolerance);

            var vertices = new List<GeoPoint2>(reshaped.Count);
            var bulges = new List<double>(reshaped.Count);

            foreach (GeoEdge2 edge in reshaped)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        /// <summary>
        /// Cuts or rounds every corner between two straight edges that has room for it, and returns the
        /// edges of the result.
        /// </summary>
        /// <remarks>
        /// Rounding works against a curve as well as against a segment. Chamfering does not: a chamfer is
        /// measured straight back along each edge, and there is no straight way back along an arc, so a
        /// corner where either edge curves is left alone. Everything else follows the straight case: the
        /// cuts are measured on the shape as it came in, and two neighbours cannot take more of the edge
        /// between them than it is long.
        /// </remarks>
        private static List<GeoEdge2> ReshapeCorners(
            List<GeoEdge2> edges,
            bool closed,
            double?[] radiusAtCorner,
            double distance1,
            double distance2,
            Tolerance tolerance)
        {
            int count = edges.Count;
            int corners = closed ? count : count - 1;

            // A corner sits between edge i and edge i + 1; for a loop the last one closes back to the first.
            var arcAt = new GeoArc2?[count];
            var cutFrom = new GeoPoint2[count];
            var cutTo = new GeoPoint2[count];
            var doCut = new bool[count];
            var takenAtStart = new double[count];
            var takenAtEnd = new double[count];

            // How much of each edge is left, as parameters along it. An arc cut back keeps its radius and
            // loses sweep, so its bulge has to be worked out from what survives rather than carried over.
            var keepFrom = new double[count];
            var keepTo = new double[count];

            for (int e = 0; e < count; e++)
            {
                keepTo[e] = 1.0;
            }

            int curved = 0;
            int straightRun = 0;
            int tooShort = 0;

            for (int c = 0; c < corners; c++)
            {
                int before = closed ? c : c;
                int after = (before + 1) % count;

                GeoEdge2 incoming = edges[before];
                GeoEdge2 outgoing = edges[after];

                double? radius = radiusAtCorner == null ? null : radiusAtCorner[c];

                if (radius.HasValue)
                {
                    // A corner against a curve is rounded the same way as one between two segments: the
                    // centre of the arc lies where the two pieces, each moved sideways by the radius,
                    // cross. Only the arithmetic differs.
                    if (!ArcFillet2.TryFillet(incoming, outgoing, radius.Value, out GeoArc2 arc, out GeoEdge2 shorter1, out GeoEdge2 shorter2, tolerance))
                    {
                        tooShort++;
                        continue;
                    }

                    arcAt[before] = arc;
                    cutFrom[before] = shorter1.EndPoint;
                    cutTo[before] = shorter2.StartPoint;
                    doCut[before] = true;
                    takenAtEnd[before] = incoming.Length - shorter1.Length;
                    takenAtStart[after] = outgoing.Length - shorter2.Length;
                    keepTo[before] = incoming.GetParameterAtPoint(shorter1.EndPoint, tolerance);
                    keepFrom[after] = outgoing.GetParameterAtPoint(shorter2.StartPoint, tolerance);
                    continue;
                }

                if (incoming.IsArc || outgoing.IsArc)
                {
                    // A chamfer is measured straight back along each edge, and there is no straight way
                    // back along a curve.
                    curved++;
                    continue;
                }

                GeoLine2 line1 = incoming.ToLine();
                GeoLine2 line2 = outgoing.ToLine();

                if (!TryCutCorner(line1, line2, distance1, distance2, tolerance, out GeoPoint2 from, out GeoPoint2 to, out bool wasStraight))
                {
                    if (wasStraight) straightRun++; else tooShort++;
                    continue;
                }

                cutFrom[before] = from;
                cutTo[before] = to;
                doCut[before] = true;
                takenAtEnd[before] = distance1;
                takenAtStart[after] = distance2;
            }

            // An edge can only give away as much as it is long, counting both of its ends.
            bool again = true;

            while (again)
            {
                again = false;

                for (int e = 0; e < count; e++)
                {
                    double taken = takenAtStart[e] + takenAtEnd[e];

                    // Exactly enough is enough: a square of side one hundred takes a fillet of fifty, and
                    // the dust left by working the tangent points out must not decide otherwise.
                    if (taken <= edges[e].Length + tolerance.EqualPoint)
                    {
                        continue;
                    }

                    int atStart = (e - 1 + count) % count;
                    int atEnd = e;

                    int victim = takenAtEnd[e] >= takenAtStart[e] ? atEnd : atStart;

                    if (!doCut[victim])
                    {
                        victim = victim == atEnd ? atStart : atEnd;
                    }

                    if (!doCut[victim])
                    {
                        break;
                    }

                    doCut[victim] = false;
                    arcAt[victim] = null;
                    takenAtEnd[victim] = 0.0;
                    takenAtStart[(victim + 1) % count] = 0.0;
                    keepTo[victim] = 1.0;
                    keepFrom[(victim + 1) % count] = 0.0;
                    tooShort++;
                    again = true;
                }
            }

            if (curved > 0 || straightRun > 0 || tooShort > 0)
            {
                GeometryHelperLog.Debug(
                    $"{(radiusAtCorner == null ? "Chamfer" : "Fillet")} left {curved + straightRun + tooShort} corner(s) alone: " +
                    $"{tooShort} had too little edge, {straightRun} were straight, {curved} met an arc.");
            }

            // Walk the edges, shortening each end that gave something up and putting the new piece in.
            var result = new List<GeoEdge2>(count * 2);

            for (int e = 0; e < count; e++)
            {
                GeoEdge2 edge = edges[e];
                int atStart = (e - 1 + count) % count;

                GeoPoint2 start = doCut[atStart] ? cutTo[atStart] : edge.StartPoint;
                GeoPoint2 end = doCut[e] ? cutFrom[e] : edge.EndPoint;

                double bulge = edge.IsArc
                    ? Math.Tan(edge.ToArc().SweptAngle * (keepTo[e] - keepFrom[e]) * 0.25)
                    : 0.0;

                result.Add(new GeoEdge2(start, end, bulge));

                if (doCut[e])
                {
                    result.Add(arcAt[e].HasValue
                        ? new GeoEdge2(arcAt[e].Value)
                        : new GeoEdge2(cutFrom[e], cutTo[e]));
                }
            }

            // Two corners can between them take the whole of the edge they share, leaving a straight piece
            // of no length: a square of side one hundred filleted at fifty is four quarter turns and
            // nothing else. Such a piece is not geometry, and leaving it in would cost the arc that
            // follows it its bulge, because a chain drops a repeated vertex and keeps the bulge of the
            // first of the two.
            result.RemoveAll(edge => edge.StartPoint.IsEqualTo(edge.EndPoint, tolerance) && !edge.IsArc);

            return result;
        }

        /// <summary>
        /// Gets one radius for every corner of a run of edges.
        /// </summary>
        private static double?[] Everywhere(double radius, int edgeCount)
        {
            var radii = new double?[edgeCount];

            for (int i = 0; i < edgeCount; i++)
            {
                radii[i] = radius;
            }

            return radii;
        }

        /// <summary>
        /// Turns radii given one per vertex into radii one per corner.
        /// </summary>
        /// <remarks>
        /// A corner sits between two edges and is named here by the vertex the two share, which is how
        /// <see cref="TryChamferAt(GeoPolygonArc2, int, double, double, out GeoPolygonArc2)"/> names it
        /// too. Inside, corners are walked by the edge that runs into them, so corner c is the one at
        /// vertex c + 1. A radius of zero leaves that corner alone.
        /// </remarks>
        private static double?[] ByCorner(IReadOnlyList<double> radii, int edgeCount, bool closed, int vertexCount)
        {
            var byCorner = new double?[edgeCount];

            for (int c = 0; c < edgeCount; c++)
            {
                int vertex = closed ? (c + 1) % vertexCount : c + 1;

                double radius = vertex < radii.Count ? radii[vertex] : 0.0;

                byCorner[c] = radius > 0.0 ? (double?)radius : null;
            }

            return byCorner;
        }

        /// <summary>
        /// Refuses a list of radii that holds something no corner could be rounded by.
        /// </summary>
        private static void RequireRadii(IReadOnlyList<double> radii)
        {
            if (radii == null)
            {
                throw new ArgumentNullException(nameof(radii));
            }

            foreach (double radius in radii)
            {
                if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(radii), "A radius must be a number, and never negative; zero leaves a corner alone.");
                }
            }
        }

        /// <summary>
        /// Rounds the corners of a chain, each by its own radius, using the default tolerance.
        /// </summary>
        public static GeoPolylineArc2 Fillet(GeoPolylineArc2 polyline, IReadOnlyList<double> radii) => Fillet(polyline, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a chain, each by its own radius, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radii">One radius per vertex, in the order the chain holds them; zero leaves that corner alone, and the two ends have no corner to round.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The filleted chain.</returns>
        /// <remarks>
        /// The radii are read the way <see cref="GeoPolylineArc2.GetBulgeAt"/> is read: the entry at an
        /// index belongs to the vertex at that index. A list shorter than the chain leaves the rest of the
        /// corners alone. The rules for a corner that will not fit are the ones a single radius follows,
        /// and they matter more here: where two neighbours together ask for more than the edge between
        /// them is long, the one taking more of it is dropped, so a corner asking for fifty gives way to
        /// one asking for five rather than the other way round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the radii are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is negative or not a number.</exception>
        public static GeoPolylineArc2 Fillet(GeoPolylineArc2 polyline, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            RequireRadii(radii);

            return Reshape(polyline, ByCorner(radii, polyline.EdgeCount, false, polyline.VertexCount), 0.0, 0.0, tolerance);
        }

        /// <summary>
        /// Rounds the corners of a loop, each by its own radius, using the default tolerance.
        /// </summary>
        public static GeoPolygonArc2 Fillet(GeoPolygonArc2 polygon, IReadOnlyList<double> radii) => Fillet(polygon, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a loop, each by its own radius, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="radii">One radius per vertex, in the order the loop holds them; zero leaves that corner alone.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The filleted loop.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the radii are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a radius is negative or not a number.</exception>
        public static GeoPolygonArc2 Fillet(GeoPolygonArc2 polygon, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            RequireRadii(radii);

            return Reshape(polygon, ByCorner(radii, polygon.EdgeCount, true, polygon.VertexCount), 0.0, 0.0, tolerance);
        }

        /// <summary>
        /// Rounds one corner of a chain, using the default tolerance.
        /// </summary>
        public static bool TryFilletAt(GeoPolylineArc2 polyline, int index, double radius, out GeoPolylineArc2 result)
        {
            return TryFilletAt(polyline, index, radius, out result, Tolerance.Global);
        }

        /// <summary>
        /// Rounds one corner of a chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="index">Which vertex to round; the two ends have no corner and are refused.</param>
        /// <param name="radius">The radius of the arc to put there.</param>
        /// <param name="result">The filleted chain, or the chain unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the arc; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain or the radius is not a positive number.</exception>
        public static bool TryFilletAt(GeoPolylineArc2 polyline, int index, double radius, out GeoPolylineArc2 result, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (index < 0 || index >= polyline.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(radius, nameof(radius));

            result = polyline;

            // The ends of a chain are not corners: nothing turns there.
            if (index == 0 || index == polyline.VertexCount - 1)
            {
                return false;
            }

            List<GeoEdge2> edges = RoundOneCorner(polyline.GetEdgeAt(index - 1), polyline.GetEdgeAt(index), radius, tolerance);

            if (edges == null)
            {
                return false;
            }

            var rebuilt = new List<GeoEdge2>(polyline.EdgeCount + 1);

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (i == index - 1)
                {
                    rebuilt.AddRange(edges);
                }
                else if (i != index)
                {
                    rebuilt.Add(polyline.GetEdgeAt(i));
                }
            }

            result = new GeoPolylineArc2(rebuilt);
            return true;
        }

        /// <summary>
        /// Rounds one corner of a loop, using the default tolerance.
        /// </summary>
        public static bool TryFilletAt(GeoPolygonArc2 polygon, int index, double radius, out GeoPolygonArc2 result)
        {
            return TryFilletAt(polygon, index, radius, out result, Tolerance.Global);
        }

        /// <summary>
        /// Rounds one corner of a loop, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="index">Which vertex to round.</param>
        /// <param name="radius">The radius of the arc to put there.</param>
        /// <param name="result">The filleted loop, or the loop unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the arc; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop or the radius is not a positive number.</exception>
        public static bool TryFilletAt(GeoPolygonArc2 polygon, int index, double radius, out GeoPolygonArc2 result, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            if (index < 0 || index >= polygon.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(radius, nameof(radius));

            result = polygon;

            int before = (index - 1 + polygon.EdgeCount) % polygon.EdgeCount;

            List<GeoEdge2> edges = RoundOneCorner(polygon.GetEdgeAt(before), polygon.GetEdgeAt(index), radius, tolerance);

            if (edges == null)
            {
                return false;
            }

            var rebuilt = new List<GeoEdge2>(polygon.EdgeCount + 1);

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                if (i == before)
                {
                    rebuilt.AddRange(edges);
                }
                else if (i != index)
                {
                    rebuilt.Add(polygon.GetEdgeAt(i));
                }
            }

            var vertices = new List<GeoPoint2>(rebuilt.Count);
            var bulges = new List<double>(rebuilt.Count);

            foreach (GeoEdge2 edge in rebuilt)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            result = new GeoPolygonArc2(vertices, bulges);
            return true;
        }

        /// <summary>
        /// Rounds the corner between two edges, and gives back the edges that replace them.
        /// </summary>
        /// <returns>The shortened first edge, the arc, and the shortened second edge, with any piece left
        /// with no length dropped; null when the corner had no room for that radius.</returns>
        private static List<GeoEdge2> RoundOneCorner(GeoEdge2 incoming, GeoEdge2 outgoing, double radius, Tolerance tolerance)
        {
            if (!ArcFillet2.TryFillet(incoming, outgoing, radius, out GeoArc2 arc, out GeoEdge2 shorter1, out GeoEdge2 shorter2, tolerance))
            {
                return null;
            }

            var edges = new List<GeoEdge2>(3);

            // A corner that eats the whole of an edge leaves a piece of no length, which is not geometry.
            if (!shorter1.StartPoint.IsEqualTo(shorter1.EndPoint, tolerance) || shorter1.IsArc)
            {
                edges.Add(shorter1);
            }

            edges.Add(new GeoEdge2(arc));

            if (!shorter2.StartPoint.IsEqualTo(shorter2.EndPoint, tolerance) || shorter2.IsArc)
            {
                edges.Add(shorter2);
            }

            return edges;
        }

        /// <summary>
        /// Chamfers one corner of a chain that may curve, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="index">Which vertex to cut; the two ends have no corner and are refused.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="result">The chamfered chain, or the chain unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the cut; false when it had not, or either edge is an arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain or either distance is not a positive number.</exception>
        public static bool TryChamferAt(GeoPolylineArc2 polyline, int index, double distance1, double distance2, out GeoPolylineArc2 result, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (index < 0 || index >= polyline.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            result = polyline;

            // The ends of a chain are not corners: nothing turns there.
            if (index == 0 || index == polyline.VertexCount - 1)
            {
                return false;
            }

            List<GeoEdge2> edges = CutOneCorner(polyline.GetEdgeAt(index - 1), polyline.GetEdgeAt(index), distance1, distance2, tolerance);

            if (edges == null)
            {
                return false;
            }

            var rebuilt = new List<GeoEdge2>(polyline.EdgeCount + 1);

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (i == index - 1)
                {
                    rebuilt.AddRange(edges);
                }
                else if (i != index)
                {
                    rebuilt.Add(polyline.GetEdgeAt(i));
                }
            }

            result = new GeoPolylineArc2(rebuilt);
            return true;
        }

        /// <summary>
        /// Chamfers one corner of a chain that may curve, using the default tolerance.
        /// </summary>
        public static bool TryChamferAt(GeoPolylineArc2 polyline, int index, double distance1, double distance2, out GeoPolylineArc2 result)
        {
            return TryChamferAt(polyline, index, distance1, distance2, out result, Tolerance.Global);
        }

        /// <summary>
        /// Chamfers one corner of a loop that may curve, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="index">Which vertex to cut.</param>
        /// <param name="distance1">How far back along the edge running into the corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <param name="result">The chamfered loop, or the loop unchanged when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the corner had room for the cut; false when it had not, or either edge is an arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop or either distance is not a positive number.</exception>
        public static bool TryChamferAt(GeoPolygonArc2 polygon, int index, double distance1, double distance2, out GeoPolygonArc2 result, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            if (index < 0 || index >= polygon.VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequirePositive(distance1, nameof(distance1));
            RequirePositive(distance2, nameof(distance2));

            result = polygon;

            int before = (index - 1 + polygon.EdgeCount) % polygon.EdgeCount;

            List<GeoEdge2> edges = CutOneCorner(polygon.GetEdgeAt(before), polygon.GetEdgeAt(index), distance1, distance2, tolerance);

            if (edges == null)
            {
                return false;
            }

            var rebuilt = new List<GeoEdge2>(polygon.EdgeCount + 1);

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                if (i == before)
                {
                    rebuilt.AddRange(edges);
                }
                else if (i != index)
                {
                    rebuilt.Add(polygon.GetEdgeAt(i));
                }
            }

            var vertices = new List<GeoPoint2>(rebuilt.Count);
            var bulges = new List<double>(rebuilt.Count);

            foreach (GeoEdge2 edge in rebuilt)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            result = new GeoPolygonArc2(vertices, bulges);
            return true;
        }

        /// <summary>
        /// Chamfers one corner of a loop that may curve, using the default tolerance.
        /// </summary>
        public static bool TryChamferAt(GeoPolygonArc2 polygon, int index, double distance1, double distance2, out GeoPolygonArc2 result)
        {
            return TryChamferAt(polygon, index, distance1, distance2, out result, Tolerance.Global);
        }

        /// <summary>
        /// Cuts the corner between two edges, and gives back the three edges that replace them.
        /// </summary>
        /// <returns>The shortened first edge, the cut, and the shortened second edge; null when the corner had no room or either edge is an arc.</returns>
        private static List<GeoEdge2> CutOneCorner(GeoEdge2 incoming, GeoEdge2 outgoing, double distance1, double distance2, Tolerance tolerance)
        {
            if (incoming.IsArc || outgoing.IsArc)
            {
                return null;
            }

            if (!TryCutCorner(incoming.ToLine(), outgoing.ToLine(), distance1, distance2, tolerance, out GeoPoint2 from, out GeoPoint2 to, out _))
            {
                return null;
            }

            return new List<GeoEdge2>
            {
                new GeoEdge2(incoming.StartPoint, from),
                new GeoEdge2(from, to),
                new GeoEdge2(to, outgoing.EndPoint)
            };
        }

        /// <summary>
        /// Works out where a chamfer cuts the corner between two segments that meet.
        /// </summary>
        private static bool TryCutCorner(
            GeoLine2 line1,
            GeoLine2 line2,
            double distance1,
            double distance2,
            Tolerance tolerance,
            out GeoPoint2 from,
            out GeoPoint2 to,
            out bool straight)
        {
            from = default;
            to = default;
            straight = false;

            GeoPoint2 corner = line1.EndPoint;

            GeoVector2 back = corner.GetVectorTo(line1.StartPoint);
            GeoVector2 forward = corner.GetVectorTo(line2.EndPoint);

            double backLength = back.Length;
            double forwardLength = forward.Length;

            if (backLength <= tolerance.EqualPoint || forwardLength <= tolerance.EqualPoint)
            {
                return false;
            }

            double sine = Math.Abs(back.CrossProduct(forward)) / (backLength * forwardLength);

            if (sine <= Math.Sin(tolerance.EqualAngleRad))
            {
                straight = true;
                return false;
            }

            if (distance1 > backLength + tolerance.EqualPoint || distance2 > forwardLength + tolerance.EqualPoint)
            {
                return false;
            }

            from = corner.Add(back.Multiply(distance1 / backLength));
            to = corner.Add(forward.Multiply(distance2 / forwardLength));
            return true;
        }

        #endregion
    }
}
