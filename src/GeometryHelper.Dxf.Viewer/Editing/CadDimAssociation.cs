using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Associative dimensions, as AutoCAD 2021 keeps them with DIMASSOC 2: the points a dimension command picked with an
    /// object snap tied to their objects (a DIMASSOC, <see cref="DxfDimension.SetDimAssoc"/>), and every edit of the document
    /// followed by the dimensions tied to what it changed (<see cref="Follow"/>), in the edit's own undo step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What follows is AutoCAD 2021's, read from dimensions it made and edited in its console: a line moved carries a linear
    /// dimension of its two ends with it whole; one end stretched moves that origin, the dimension line keeping its distance
    /// from the origin it lay nearer to, or from the other where that would bring it nearer the other; a circle scaled about
    /// its center draws its radius out along the same direction, the words where they stood, grown from their end on the
    /// leader's side; an arc length dimension keeps its arc as far out; the object erased unties the dimension, which keeps its
    /// points ("Dimension disassociated."). A dimension moved or reshaped on its own, its tied points no longer on their
    /// objects, is untied too, as AutoCAD unties one moved without its line.
    /// </para>
    /// <para>
    /// The points followed are the ends, middles, centers, quadrants and nearest points of lines, arcs, circles and the
    /// segments of light polylines, POINTs, where two lines, arcs, circles or segments cross, and the feet of perpendiculars
    /// to them and the points of tangency of arcs, circles and arcs of polylines (<see cref="DrawnAt"/>). A point tied to
    /// another kind of object stays where it is.
    /// </para>
    /// </remarks>
    internal static class CadDimAssociation
    {
        /// <summary>
        /// The reference an object snap gives for a point of a dimension: its object and where on it the point lies; null for a
        /// snap of no object, of a kind not tied, or of an object not followed. A perpendicular or a tangent is tied where the
        /// point it was drawn from is given (<paramref name="from"/>), with the reference of that point
        /// (<paramref name="fromTied"/>, or one of no object), as AutoCAD 2021 writes them. An end where several objects meet
        /// is tied to the one drawn last (<see cref="NewestEndingAt"/>).
        /// </summary>
        internal static DxfOsnapPointRef FromSnap(
            CadDocument document,
            int pointIndex,
            CadSnapResult snap,
            GeoPoint2? from = null,
            DxfOsnapPointRef fromTied = null
        )
        {
            if (snap == null || snap.Handle == null)
            {
                return null;
            }

            DxfEntity entity = document.FindEntity(snap.Handle);
            GeoPoint3 at = new GeoPoint3(snap.Point.X, snap.Point.Y, 0.0);
            double least = Tolerance.Default.EqualPoint;

            if (snap.Kind == CadSnapKind.Endpoint)
            {
                entity = NewestEndingAt(document, entity, snap.Point);
            }

            if (snap.Kind == CadSnapKind.Intersection)
            {
                return Crossing(document, pointIndex, entity, at);
            }

            if (snap.Kind == CadSnapKind.Perpendicular || snap.Kind == CadSnapKind.Tangent)
            {
                return Drawn(pointIndex, snap.Kind == CadSnapKind.Tangent, entity, at, from, fromTied);
            }

            switch (entity)
            {
                case DxfLine line:
                    switch (snap.Kind)
                    {
                        case CadSnapKind.Endpoint:
                            bool start = Distance(snap.Point, line.StartPoint) <= Distance(snap.Point, line.EndPoint);
                            return new DxfOsnapPointRef(
                                pointIndex,
                                start ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint,
                                line.Handle,
                                0,
                                start ? 0.0 : 1.0,
                                start ? line.StartPoint : line.EndPoint,
                                null
                            );
                        case CadSnapKind.Midpoint:
                            return new DxfOsnapPointRef(
                                pointIndex,
                                DxfOsnapType.Midpoint,
                                line.Handle,
                                0,
                                0.5,
                                at,
                                null
                            );
                        case CadSnapKind.Nearest:
                            return new DxfOsnapPointRef(
                                pointIndex,
                                DxfOsnapType.Nearest,
                                line.Handle,
                                0,
                                Along(line, snap.Point),
                                at,
                                null
                            );
                        default:
                            return null;
                    }

                case DxfArc arc:
                    switch (snap.Kind)
                    {
                        case CadSnapKind.Endpoint:
                            bool start = Distance(snap.Point, arc.StartPoint) <= Distance(snap.Point, arc.EndPoint);
                            return new DxfOsnapPointRef(
                                pointIndex,
                                start ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint,
                                arc.Handle,
                                0,
                                start ? arc.StartAngle : arc.EndAngle,
                                start ? arc.StartPoint : arc.EndPoint,
                                null
                            );
                        case CadSnapKind.Midpoint:
                            return new DxfOsnapPointRef(
                                pointIndex,
                                DxfOsnapType.Midpoint,
                                arc.Handle,
                                0,
                                (arc.StartParam + arc.EndParam) / 2.0,
                                at,
                                null
                            );
                        default:
                            return Round(pointIndex, snap, arc, arc.Center, at);
                    }

                case DxfCircle circle:
                    return Round(pointIndex, snap, circle, circle.Center, at);
                case DxfPolyline polyline:
                    return OnPolyline(pointIndex, snap, polyline, at);
                case DxfDBPoint node when snap.Kind == CadSnapKind.Node:
                    return new DxfOsnapPointRef(
                        pointIndex,
                        DxfOsnapType.Node,
                        node.Handle,
                        0,
                        0.0,
                        node.Position,
                        null
                    );
                default:
                    return null;
            }
        }

        /// <summary>
        /// Of the lines, arcs and light polylines that end at a point (a polyline at any of its vertices), the one made last, as
        /// AutoCAD 2021 ties an end snapped where several objects meet: the end (0, 0) of a line to (100, 0) and of a line drawn
        /// after it up to (0, 100) is tied to the second, though the cursor stood on the first; of three lines from one point, to
        /// the third. The entity snapped where no later one ends there.
        /// </summary>
        private static DxfEntity NewestEndingAt(CadDocument document, DxfEntity entity, GeoPoint2 point)
        {
            CadSpace space = entity == null ? null : document.FindSpace(entity.Handle);

            if (space == null)
            {
                return entity;
            }

            double least = Tolerance.Default.EqualPoint;
            DxfEntity newest = entity;

            foreach (string handle in document.GetPickIndex(space).Pick(point.X, point.Y, least))
            {
                DxfEntity found = document.FindEntity(handle);

                if (found != null && Newer(newest.Handle, found.Handle) && EndsAt(found, point, least))
                {
                    newest = found;
                }
            }

            return newest;
        }

        /// <summary>Whether a line or an arc has an end at a point, or a light polyline a vertex there.</summary>
        private static bool EndsAt(DxfEntity entity, GeoPoint2 point, double least)
        {
            switch (entity)
            {
                case DxfLine line:
                    return Distance(point, line.StartPoint) <= least || Distance(point, line.EndPoint) <= least;
                case DxfArc arc:
                    return Distance(point, arc.StartPoint) <= least || Distance(point, arc.EndPoint) <= least;
                case DxfPolyline polyline:
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        if (Distance(point, polyline.GetPointAtParameter(i)) <= least)
                        {
                            return true;
                        }
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The reference of a point snapped where two objects cross: the line, arc, circle or light polyline drawn later first
        /// (group 331), the other second (332), each polyline with its segment (groups 91 and 92), its parameter on the first,
        /// as AutoCAD 2021 wrote a DIMLINEAR snapped to the crossing of two lines, and of a line drawn after a polyline across
        /// the polyline's third segment (331 the line, 332 the polyline and 92 = 3); null where no second line, arc, circle or
        /// segment passes through the point.
        /// </summary>
        private static DxfOsnapPointRef Crossing(CadDocument document, int pointIndex, DxfEntity entity, GeoPoint3 at)
        {
            CadSpace space = entity == null ? null : document.FindSpace(entity.Handle);
            var p = new GeoPoint2(at.X, at.Y);
            int marker = MarkerAt(entity, p);

            if (space == null || !PieceOf(entity, marker, out _))
            {
                return null;
            }

            double least = Tolerance.Default.EqualPoint;
            DxfEntity other = null;
            int otherMarker = 0;

            foreach (string handle in document.GetPickIndex(space).Pick(at.X, at.Y, least))
            {
                DxfEntity found = document.FindEntity(handle);
                int foundMarker = MarkerAt(found, p);

                if (
                    !ReferenceEquals(found, entity)
                    && PieceOf(found, foundMarker, out Piece piece)
                    && Distance(ClosestOn(piece, p), at) <= least
                    && (other == null || Newer(other.Handle, found.Handle))
                )
                {
                    other = found;
                    otherMarker = foundMarker;
                }
            }

            if (other == null)
            {
                return null;
            }

            bool swap = Newer(entity.Handle, other.Handle);
            DxfEntity first = swap ? other : entity;
            DxfEntity second = swap ? entity : other;
            int firstMarker = swap ? otherMarker : marker;
            PieceOf(first, firstMarker, out Piece on);
            return new DxfOsnapPointRef(
                pointIndex,
                DxfOsnapType.Intersection,
                first.Handle,
                2,
                firstMarker,
                ParameterOn(on, p),
                at,
                second.Handle,
                2,
                swap ? marker : otherMarker,
                null
            );
        }

        /// <summary>
        /// The reference of a point snapped perpendicular to a line, an arc, a circle or a segment of a light polyline, or tangent
        /// to an arc, a circle or an arc of a polyline, from another point: its parameter on the object (along a line or a
        /// segment, run on as far as needed, as AutoCAD 2021 wrote 2.25 for a foot past the end; the angle on an arc or a
        /// circle), its segment (for a foot past the end of a segment, the segment whose line runs on through it,
        /// <see cref="RunOnSegment"/>), and the reference of the point drawn from; null with no point drawn from, or for
        /// another object.
        /// </summary>
        private static DxfOsnapPointRef Drawn(
            int pointIndex,
            bool tangent,
            DxfEntity entity,
            GeoPoint3 at,
            GeoPoint2? from,
            DxfOsnapPointRef fromTied
        )
        {
            var p = new GeoPoint2(at.X, at.Y);
            int marker = MarkerAt(entity, p);

            if (marker == 0 && !tangent && from.HasValue && entity is DxfPolyline polyline)
            {
                marker = RunOnSegment(polyline, from.Value, p);
            }

            if (!from.HasValue || !PieceOf(entity, marker, out Piece piece) || (tangent && !piece.IsRound))
            {
                return null;
            }

            var reference = new DxfOsnapPointRef(
                pointIndex,
                tangent ? DxfOsnapType.Tangent : DxfOsnapType.Perpendicular,
                entity.Handle,
                marker,
                ParameterOn(piece, p),
                at,
                null
            );
            return fromTied != null
                ? reference.WithLastPoint(fromTied)
                : reference.WithLastPoint(new GeoPoint3(from.Value.X, from.Value.Y, 0.0));
        }

        /// <summary>
        /// Where a point snapped perpendicular or tangent is now, as AutoCAD 2021 finds it. Where the point it was drawn from is
        /// tied to an object still there, drawn again from that point: the foot on the line or the segment run on, or on the
        /// arc or the circle; of the two points of tangency, the one nearer the dimension's point. A tangent of a circle of 25
        /// about (200, 0) from (300, 100), the circle moved 10 down, goes to (184.593, 9.688). Else a tangent is drawn again
        /// from the point it was drawn from, where it kept one, and a perpendicular stays at its parameter on the object; a
        /// perpendicular on a line or a segment whose parameter runs past either end is the foot from the origin of the world
        /// instead, as AutoCAD 2021 moved a DIMLINEAR whose first point was drawn from (20, 320) square to the line (100, -20) to
        /// (100, 60), at parameter 4.25: the line moved 20 to the right, the point went to (120, 0). Where no object changed
        /// (<paramref name="unchanged"/>), as when the dimension is tied or edited alone, such a point is where its parameter
        /// puts it, where it was snapped.
        /// </summary>
        private static GeoPoint3? DrawnAt(
            CadDocument document,
            DxfDimension dimension,
            DxfOsnapPointRef reference,
            DxfEntity entity,
            bool unchanged
        )
        {
            if (
                !PieceOf(entity, reference.GsMarker, out Piece piece)
                || (reference.OsnapType == DxfOsnapType.Tangent && !piece.IsRound)
            )
            {
                return null;
            }

            DxfOsnapPointRef last = reference.LastPointReference;
            DxfEntity lastObject = last?.ObjectHandle == null ? null : document.FindEntity(last.ObjectHandle);
            GeoPoint3? from = lastObject == null ? null : Evaluate(last, lastObject);
            GeoPoint3 near = PointOf(dimension, reference.PointIndex) ?? reference.Point;
            GeoPoint2 at;

            if (
                !from.HasValue
                && reference.OsnapType == DxfOsnapType.Tangent
                && last != null
                && last.ObjectHandle == null
            )
            {
                from = last.Point;
            }

            if (from.HasValue)
            {
                if (
                    !DrawnFrom(
                        piece,
                        reference.OsnapType == DxfOsnapType.Tangent,
                        new GeoPoint2(from.Value.X, from.Value.Y),
                        new GeoPoint2(near.X, near.Y),
                        out at
                    )
                )
                {
                    return null;
                }
            }
            else if (!unchanged && !piece.IsRound && (reference.NearParameter < 0.0 || reference.NearParameter > 1.0))
            {
                at = ClosestOn(piece, GeoPoint2.Origin, true);
            }
            else
            {
                at = PointOn(piece, reference.NearParameter);
            }

            return new GeoPoint3(at.X, at.Y, near.Z);
        }

        /// <summary>
        /// The foot of the perpendicular from a point to a piece (its line run on, or its whole circle) or its point of tangency,
        /// of two the one nearer <paramref name="near"/>; false where there is none.
        /// </summary>
        private static bool DrawnFrom(Piece piece, bool tangent, GeoPoint2 from, GeoPoint2 near, out GeoPoint2 at)
        {
            at = default;

            if (!piece.IsRound)
            {
                if ((piece.B - piece.A).Length <= 0.0)
                {
                    return false;
                }

                at = ClosestOn(piece, from, true);
                return true;
            }

            GeoVector2 outward = from - piece.Center;
            double far = outward.Length;

            if (far <= 0.0 || (tangent && far <= piece.Radius))
            {
                return false;
            }

            GeoVector2 unit = outward / far;
            double turn = tangent ? Math.Acos(piece.Radius / far) : 0.0;
            bool found = false;

            foreach (double angle in tangent ? new[] { turn, -turn } : new[] { 0.0, Math.PI })
            {
                var way = new GeoVector2(
                    unit.X * Math.Cos(angle) - unit.Y * Math.Sin(angle),
                    unit.X * Math.Sin(angle) + unit.Y * Math.Cos(angle)
                );
                GeoPoint2 candidate = piece.Center + way * piece.Radius;

                if (!found || (candidate - near).Length < (at - near).Length)
                {
                    at = candidate;
                    found = true;
                }
            }

            return true;
        }

        /// <summary>
        /// A line, an arc or a circle a point of a dimension is tied to, or the segment of a light polyline its marker names
        /// (1 for the first): a line from <see cref="A"/> to <see cref="B"/>, else a round about <see cref="Center"/> from the
        /// angle <see cref="Start"/> counter-clockwise by <see cref="Sweep"/>.
        /// </summary>
        private struct Piece
        {
            internal bool IsRound;
            internal GeoPoint2 A;
            internal GeoPoint2 B;
            internal GeoPoint2 Center;
            internal double Radius;
            internal double Start;
            internal double Sweep;

            // An arc of a polyline counts its angles on from Start, as AutoCAD 2021 wrote 7.85398 for the end of a half circle
            // from -90 degrees; an arc or a circle counts them from 0 to 2 pi.
            internal bool Unwrapped;
        }

        /// <summary>The piece of an entity a reference names: a line, an arc, a circle, or the segment of a light polyline of the marker; false for any other.</summary>
        private static bool PieceOf(DxfEntity entity, int marker, out Piece piece)
        {
            piece = default;

            switch (entity)
            {
                case DxfLine line:
                    piece.A = new GeoPoint2(line.StartPoint.X, line.StartPoint.Y);
                    piece.B = new GeoPoint2(line.EndPoint.X, line.EndPoint.Y);
                    return true;
                case DxfArc arc:
                    piece.IsRound = true;
                    piece.Center = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    piece.Radius = arc.Radius;
                    piece.Start = Normal(arc.StartAngle);
                    piece.Sweep = Normal(arc.EndAngle - arc.StartAngle);
                    return true;
                case DxfCircle circle:
                    piece.IsRound = true;
                    piece.Center = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    piece.Radius = circle.Radius;
                    piece.Sweep = 2.0 * Math.PI;
                    return true;
                case DxfPolyline polyline:
                    int count = polyline.NumberOfVertices;
                    int i = marker - 1;

                    if (i < 0 || i >= (polyline.Closed ? count : count - 1))
                    {
                        return false;
                    }

                    GeoPoint2 a = polyline.GetPoint2dAt(i);
                    GeoPoint2 b = polyline.GetPoint2dAt((i + 1) % count);
                    double bulge = polyline.GetBulgeAt(i);
                    piece.A = a;
                    piece.B = b;

                    if (bulge == 0.0 || (b - a).Length <= 0.0)
                    {
                        return true;
                    }

                    double sweep = 4.0 * Math.Atan(bulge);
                    GeoVector2 chord = b - a;
                    var left = new GeoVector2(-chord.Y, chord.X) / chord.Length;
                    piece.IsRound = true;
                    piece.Unwrapped = true;
                    piece.Center = a + chord / 2.0 + left * (chord.Length / 2.0 / Math.Tan(sweep / 2.0));
                    piece.Radius = (a - piece.Center).Length;
                    piece.Start = Normal(AngleOf(sweep > 0.0 ? a : b, piece.Center));
                    piece.Sweep = Math.Abs(sweep);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The segment of a light polyline a point lies on, 1 for the first, the first of two that meet there; 0 for any other entity or a point on none.</summary>
        private static int MarkerAt(DxfEntity entity, GeoPoint2 point)
        {
            if (!(entity is DxfPolyline polyline))
            {
                return 0;
            }

            int count = polyline.NumberOfVertices;
            int segments = polyline.Closed ? count : count - 1;
            int best = 0;
            double nearest = double.MaxValue;

            for (int i = 1; i <= segments; i++)
            {
                if (PieceOf(polyline, i, out Piece piece))
                {
                    double d = (ClosestOn(piece, point) - point).Length;

                    if (d < nearest - Tolerance.Default.EqualPoint)
                    {
                        nearest = d;
                        best = i;
                    }
                }
            }

            return nearest <= Tolerance.Default.EqualPoint ? best : 0;
        }

        /// <summary>
        /// The straight segment of a light polyline whose line, run on past its ends, has the foot of the perpendicular from a
        /// point at the point given, 1 for the first; of two, the one whose own stretch lies nearer the foot, then the first.
        /// AutoCAD 2021 tied a foot (150, -400) drawn from (150, -450) square to the first segment of (0, -400)-(100, -400)-...
        /// to marker 1 at parameter 1.5. Nought where no segment has the foot there.
        /// </summary>
        private static int RunOnSegment(DxfPolyline polyline, GeoPoint2 from, GeoPoint2 foot)
        {
            int count = polyline.NumberOfVertices;
            int segments = polyline.Closed ? count : count - 1;
            double least = Tolerance.Default.EqualPoint;
            int best = 0;
            double nearest = double.MaxValue;

            for (int i = 1; i <= segments; i++)
            {
                if (
                    !PieceOf(polyline, i, out Piece piece)
                    || piece.IsRound
                    || (piece.B - piece.A).Length <= least
                    || (ClosestOn(piece, from, true) - foot).Length > least
                )
                {
                    continue;
                }

                double d = (ClosestOn(piece, foot) - foot).Length;

                if (d < nearest - least)
                {
                    nearest = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The parameter of a point on a piece: the share of the way along a line, run on; the angle about a round.</summary>
        private static double ParameterOn(Piece piece, GeoPoint2 point)
        {
            if (!piece.IsRound)
            {
                GeoVector2 way = piece.B - piece.A;
                double squared = way.X * way.X + way.Y * way.Y;
                return squared <= 0.0 ? 0.0 : ((point.X - piece.A.X) * way.X + (point.Y - piece.A.Y) * way.Y) / squared;
            }

            double angle = Normal(AngleOf(point, piece.Center));

            if (!piece.Unwrapped)
            {
                return angle;
            }

            // Counted on from the start, a point past the end taken back below the start where it lies nearer that.
            angle = angle < piece.Start ? angle + 2.0 * Math.PI : angle;
            double past = angle - piece.Start - piece.Sweep;
            return past > 0.0 && past > piece.Start + 2.0 * Math.PI - angle ? angle - 2.0 * Math.PI : angle;
        }

        /// <summary>The point of a parameter on a piece (<see cref="ParameterOn"/>).</summary>
        private static GeoPoint2 PointOn(Piece piece, double parameter) =>
            piece.IsRound
                ? new GeoPoint2(
                    piece.Center.X + piece.Radius * Math.Cos(parameter),
                    piece.Center.Y + piece.Radius * Math.Sin(parameter)
                )
                : piece.A + (piece.B - piece.A) * parameter;

        /// <summary>The point of a piece nearest a point: on the line between its ends or run on, on the arc's stretch or anywhere on its circle.</summary>
        private static GeoPoint2 ClosestOn(Piece piece, GeoPoint2 point, bool runOn = false)
        {
            if (!piece.IsRound)
            {
                double t = ParameterOn(piece, point);
                return PointOn(piece, runOn ? t : Math.Max(0.0, Math.Min(1.0, t)));
            }

            double angle = (point - piece.Center).Length > 0.0 ? Normal(AngleOf(point, piece.Center)) : piece.Start;

            if (!runOn && Normal(angle - piece.Start) > piece.Sweep)
            {
                GeoPoint2 s = PointOn(piece, piece.Start);
                GeoPoint2 e = PointOn(piece, piece.Start + piece.Sweep);
                return (s - point).Length <= (e - point).Length ? s : e;
            }

            return PointOn(piece, angle);
        }

        private static double AngleOf(GeoPoint2 point, GeoPoint2 center) =>
            Math.Atan2(point.Y - center.Y, point.X - center.X);

        /// <summary>An angle brought to 0 to 2 pi.</summary>
        private static double Normal(double angle)
        {
            angle %= 2.0 * Math.PI;
            return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
        }

        /// <summary>Whether the second handle is of an object made after the first's, by the number the handles are.</summary>
        private static bool Newer(string first, string second) =>
            second.Length != first.Length
                ? second.Length > first.Length
                : string.CompareOrdinal(second.ToUpperInvariant(), first.ToUpperInvariant()) > 0;

        /// <summary>
        /// A reference given another point of a dimension: the same object and place, and the same point drawn from, numbered as
        /// the point it ties now; null for null.
        /// </summary>
        internal static DxfOsnapPointRef Renumbered(DxfOsnapPointRef reference, int pointIndex) =>
            reference == null
                ? null
                : WithLast(
                    Copy(
                        reference,
                        pointIndex,
                        reference.ObjectHandle,
                        reference.Point,
                        reference.IntersectionObjectHandle
                    ),
                    reference.LastPointReference,
                    null
                );

        /// <summary>The reference without the point its perpendicular or tangent was drawn from, as AutoCAD 2021 ties the feature of an ordinate; null for null.</summary>
        internal static DxfOsnapPointRef WithoutLastPoint(DxfOsnapPointRef reference) =>
            reference == null
                ? null
                : Copy(
                    reference,
                    reference.PointIndex,
                    reference.ObjectHandle,
                    reference.Point,
                    reference.IntersectionObjectHandle
                );

        /// <summary>A copy of a reference without its last point, numbered, tied to the objects and at the point given; its snap, segments and parameter kept.</summary>
        private static DxfOsnapPointRef Copy(
            DxfOsnapPointRef reference,
            int pointIndex,
            string handle,
            GeoPoint3 point,
            string otherHandle
        ) =>
            new DxfOsnapPointRef(
                pointIndex,
                reference.OsnapType,
                handle,
                reference.SubentType,
                reference.GsMarker,
                reference.NearParameter,
                point,
                otherHandle,
                reference.IntersectionSubentType,
                reference.IntersectionGsMarker,
                null
            );

        /// <summary>
        /// A reference given the point its perpendicular or tangent was drawn from, as another had it: that point's reference,
        /// its object put for the one named in <paramref name="objects"/> where it is there, or a point of no object where it
        /// was of none or its object is not there; the reference as it is where it had none.
        /// </summary>
        private static DxfOsnapPointRef WithLast(
            DxfOsnapPointRef reference,
            DxfOsnapPointRef last,
            IReadOnlyDictionary<string, DxfEntity> objects
        )
        {
            if (last == null)
            {
                return reference;
            }

            if (last.ObjectHandle == null)
            {
                return reference.WithLastPoint(last.Point);
            }

            if (objects == null)
            {
                return reference.WithLastPoint(last);
            }

            return
                objects.TryGetValue(last.ObjectHandle, out DxfEntity to)
                && (last.IntersectionObjectHandle == null || objects.ContainsKey(last.IntersectionObjectHandle))
                ? reference.WithLastPoint(
                    Copy(
                        last,
                        last.PointIndex,
                        to.Handle,
                        last.Point,
                        last.IntersectionObjectHandle == null ? null : objects[last.IntersectionObjectHandle].Handle
                    )
                )
                : reference.WithLastPoint(last.Point);
        }

        /// <summary>
        /// The reference of the point of a dimension that stands at a place, numbered as another point, as DIMCONTINUE and
        /// DIMBASELINE tie the first origin of the next dimension to what the origin it starts from was tied to; null where no
        /// tied point of the dimension stands there.
        /// </summary>
        internal static DxfOsnapPointRef TiedAt(DxfDimension dimension, GeoPoint2 at, int pointIndex)
        {
            DxfDimAssoc assoc = dimension?.DimAssoc;

            if (assoc == null)
            {
                return null;
            }

            foreach (DxfOsnapPointRef reference in assoc.PointReferences)
            {
                GeoPoint3? own = PointOf(dimension, reference.PointIndex);

                if (own.HasValue && Distance(at, own.Value) <= Tolerance.Default.EqualPoint)
                {
                    return Renumbered(reference, pointIndex);
                }
            }

            return null;
        }

        /// <summary>
        /// The reference of a vertex of a light polyline, as AutoCAD 2021's QDIM writes it: the start of the segment from it
        /// (its marker the segment, 1 for the first), the end of the last segment for the last vertex of an open one.
        /// </summary>
        internal static DxfOsnapPointRef VertexOf(DxfPolyline polyline, int vertex)
        {
            int segments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
            GeoPoint3 point = polyline.GetPointAtParameter(vertex);
            return vertex < segments
                ? new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, polyline.Handle, vertex + 1, 0.0, point, null)
                : new DxfOsnapPointRef(0, DxfOsnapType.Endpoint, polyline.Handle, vertex, 1.0, point, null);
        }

        /// <summary>
        /// The references of two lines selected for DIMANGULAR: the start and the end of the dimension's first line (the line
        /// selected second, as AutoCAD 2021 writes it), then of its second, the four points of the dimension, as AutoCAD 2021
        /// ties each to its line.
        /// </summary>
        internal static DxfOsnapPointRef[] TwoLines(DxfLine first, DxfLine second) =>
            new[]
            {
                new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, first.Handle, 0, 0.0, first.StartPoint, null),
                new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, first.Handle, 0, 1.0, first.EndPoint, null),
                new DxfOsnapPointRef(2, DxfOsnapType.StartPoint, second.Handle, 0, 0.0, second.StartPoint, null),
                new DxfOsnapPointRef(3, DxfOsnapType.Endpoint, second.Handle, 0, 1.0, second.EndPoint, null),
            };

        /// <summary>The references of a line selected for DIMLINEAR or DIMALIGNED: its start and its end, as AutoCAD 2021 writes them.</summary>
        internal static DxfOsnapPointRef[] LineEnds(DxfLine line) =>
            new[]
            {
                new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, line.Handle, 0, 0.0, line.StartPoint, null),
                new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, line.Handle, 0, 1.0, line.EndPoint, null),
            };

        /// <summary>The reference of an arc or a circle selected for DIMRADIUS or DIMDIAMETER: its center, the second point, as AutoCAD 2021 writes it.</summary>
        internal static DxfOsnapPointRef[] CenterOf(DxfEntity curve) =>
            new[]
            {
                new DxfOsnapPointRef(
                    1,
                    DxfOsnapType.Center,
                    curve.Handle,
                    0,
                    0.0,
                    curve is DxfArc arc ? arc.Center : ((DxfCircle)curve).Center,
                    null
                ),
            };

        /// <summary>The references of an arc selected for DIMANGULAR: its start, its end and its center, as AutoCAD 2021 writes them.</summary>
        internal static DxfOsnapPointRef[] ArcEnds(DxfArc arc) =>
            new[]
            {
                new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, arc.Handle, 0, arc.StartAngle, arc.StartPoint, null),
                new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, arc.Handle, 0, arc.EndAngle, arc.EndPoint, null),
                new DxfOsnapPointRef(2, DxfOsnapType.Center, arc.Handle, 0, 0.0, arc.Center, null),
            };

        /// <summary>
        /// The arc segment of a light polyline that DIMARC measures: the segment nearest the point picked (the first arc segment
        /// where none was), given as its centre, radius, the angle it starts at running counter-clockwise, its sweep and its
        /// marker (1 for the first segment). False where that segment is straight or the polyline has no arc segment.
        /// </summary>
        internal static bool ArcSegmentAt(
            DxfPolyline polyline,
            GeoPoint2? picked,
            out GeoPoint2 center,
            out double radius,
            out double start,
            out double sweep,
            out int marker
        )
        {
            center = default;
            radius = 0.0;
            start = 0.0;
            sweep = 0.0;
            marker = 0;
            int segments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
            double nearest = double.MaxValue;

            for (int i = 1; i <= segments; i++)
            {
                if (!PieceOf(polyline, i, out Piece piece))
                {
                    continue;
                }

                if (!picked.HasValue)
                {
                    if (!piece.IsRound)
                    {
                        continue;
                    }

                    marker = i;
                    break;
                }

                double d = (ClosestOn(piece, picked.Value) - picked.Value).Length;

                if (d < nearest - Tolerance.Default.EqualPoint)
                {
                    nearest = d;
                    marker = i;
                }
            }

            if (marker == 0 || !PieceOf(polyline, marker, out Piece found) || !found.IsRound)
            {
                return false;
            }

            center = found.Center;
            radius = found.Radius;
            start = found.Start;
            sweep = found.Sweep;
            return radius > Tolerance.Default.EqualPoint;
        }

        /// <summary>
        /// The references of an arc length dimension of an arc, or of the arc segment of a polyline (<paramref name="marker"/>,
        /// 1 for its first segment, 0 for an arc), as AutoCAD 2021 writes them: the ends of the length measured as the start and
        /// end of the arc (a polyline's by the way its path runs: the segment's start vertex where it turns counter-clockwise, its
        /// end vertex where it turns clockwise), the parameters counted on from the arc's start, the end past a turn where it
        /// crosses 0: 5.235988 and 8.028514 for the arc from 300 to 100 degrees. A partial length's are nearest points of the arc
        /// at their angles counted so, 0.349066 and 1.745329 for one from 20 to 100 degrees. Then the arc's centre, at the
        /// parameter of the arc's start.
        /// </summary>
        internal static DxfOsnapPointRef[] ArcLengthEnds(
            DxfEntity source,
            int marker,
            GeoPoint2 center,
            double start,
            double sweep,
            DxfArcDimension dimension
        )
        {
            double least = Tolerance.Default.EqualPoint;
            GeoPoint3 startPoint;
            GeoPoint3 endPoint;
            GeoPoint3 centerPoint;
            DxfOsnapType startType = DxfOsnapType.StartPoint;
            DxfOsnapType endType = DxfOsnapType.Endpoint;

            if (source is DxfArc arc)
            {
                startPoint = arc.StartPoint;
                endPoint = arc.EndPoint;
                centerPoint = arc.Center;
            }
            else
            {
                var polyline = (DxfPolyline)source;
                bool counter = polyline.GetBulgeAt(marker - 1) > 0.0;
                GeoPoint3 first = polyline.GetPointAtParameter(marker - 1.0);
                GeoPoint3 last = polyline.GetPointAtParameter((double)marker);
                startPoint = counter ? first : last;
                endPoint = counter ? last : first;
                startType = counter ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint;
                endType = counter ? DxfOsnapType.Endpoint : DxfOsnapType.StartPoint;
                centerPoint = new GeoPoint3(center.X, center.Y, first.Z);
            }

            GeoPoint3[] ends = { dimension.XLine1Point, dimension.XLine2Point };
            var references = new DxfOsnapPointRef[3];

            for (int i = 0; i < 2; i++)
            {
                GeoPoint3 end = ends[i];
                var at = new GeoPoint2(end.X, end.Y);
                references[i] =
                    !dimension.IsPartial && Distance(at, startPoint) <= least
                        ? new DxfOsnapPointRef(i, startType, source.Handle, marker, start, startPoint, null)
                    : !dimension.IsPartial && Distance(at, endPoint) <= least
                        ? new DxfOsnapPointRef(i, endType, source.Handle, marker, start + sweep, endPoint, null)
                    : new DxfOsnapPointRef(
                        i,
                        DxfOsnapType.Nearest,
                        source.Handle,
                        marker,
                        start + Normal(AngleOf(at, center) - start),
                        end,
                        null
                    );
            }

            references[2] = new DxfOsnapPointRef(
                2,
                DxfOsnapType.Center,
                source.Handle,
                marker,
                start,
                centerPoint,
                null
            );
            return references;
        }

        /// <summary>
        /// The references of a circle selected for DIMANGULAR, as AutoCAD 2021 writes them: the point picked on it as its
        /// nearest point at that angle, its center as the vertex, and the second point as a nearest point of the object it was
        /// snapped to, which AutoCAD 2021 wrote so: on a line or a straight segment of a polyline at the parameter of the point
        /// on the circle, run on (-0.9197 along (260, 40)-(300, 40) for the end (300, 40) seen from a circle of 25 about
        /// (200, 0)); on another arc or circle, an arc of a polyline (its segment the marker) or a POINT, at nought, as AutoCAD
        /// 2021 tied the end (780, -1500) of a polyline's second segment, a half circle about (780, -1530), at marker 2 and
        /// parameter 0; on the circle itself at the angle of the point; with no object snapped, or another, on the circle at its
        /// angle.
        /// </summary>
        internal static DxfOsnapPointRef[] CircleAngle(
            CadDocument document,
            DxfCircle circle,
            GeoPoint2 first,
            GeoPoint2 second,
            CadSnapResult secondSnap
        )
        {
            var center = new GeoPoint2(circle.Center.X, circle.Center.Y);
            DxfEntity snapped =
                secondSnap?.Handle == null || secondSnap.Kind == CadSnapKind.None
                    ? null
                    : document.FindEntity(secondSnap.Handle);
            var at = new GeoPoint3(second.X, second.Y, circle.Center.Z);
            DxfOsnapPointRef end = null;

            switch (snapped)
            {
                case DxfLine line:
                    end = new DxfOsnapPointRef(1, DxfOsnapType.Nearest, line.Handle, 0, Along(line, second), at, null);
                    break;
                case DxfPolyline polyline:
                    int marker = MarkerAt(polyline, secondSnap.Point);

                    if (PieceOf(polyline, marker, out Piece piece))
                    {
                        end = new DxfOsnapPointRef(
                            1,
                            DxfOsnapType.Nearest,
                            polyline.Handle,
                            marker,
                            piece.IsRound ? 0.0 : ParameterOn(piece, second),
                            at,
                            null
                        );
                    }

                    break;
                case DxfArc _:
                case DxfCircle other when !ReferenceEquals(other, circle):
                case DxfDBPoint _:
                    end = new DxfOsnapPointRef(1, DxfOsnapType.Nearest, snapped.Handle, 0, 0.0, at, null);
                    break;
            }

            return new[]
            {
                new DxfOsnapPointRef(
                    0,
                    DxfOsnapType.Nearest,
                    circle.Handle,
                    0,
                    Normal(AngleOf(first, center)),
                    new GeoPoint3(first.X, first.Y, circle.Center.Z),
                    null
                ),
                end
                    ?? new DxfOsnapPointRef(
                        1,
                        DxfOsnapType.Nearest,
                        circle.Handle,
                        0,
                        Normal(AngleOf(second, center)),
                        at,
                        null
                    ),
                new DxfOsnapPointRef(2, DxfOsnapType.Center, circle.Handle, 0, 0.0, circle.Center, null),
            };
        }

        /// <summary>
        /// Ties a dimension just made to the objects its points were snapped to, as part of the command's step, where the
        /// drawing's DIMASSOC is 2 and a point was snapped to an object followed; nothing otherwise.
        /// </summary>
        internal static void Tie(CadCommandContext c, string handle, IEnumerable<DxfOsnapPointRef> references)
        {
            var list = new List<DxfOsnapPointRef>();

            foreach (DxfOsnapPointRef reference in references)
            {
                if (reference != null)
                {
                    list.Add(reference);
                }
            }

            if (handle == null || list.Count == 0 || c.Database.DimAssoc != 2)
            {
                return;
            }

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Associative", "No", "Yes") },
                s => ((DxfDimension)s.Document.FindEntity(handle)).SetDimAssoc(list)
            );
        }

        /// <summary>
        /// Follows an edit: each associative dimension tied to an entity the edit changed or erased has its points moved to where
        /// the references now say and its picture drawn again, or is untied where its objects are gone or it was moved off them
        /// itself. Returns the records of the dimensions changed so, for the edit's step; none where no dimension is tied.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();
            var edited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dimensions = new SortedSet<string>(StringComparer.Ordinal);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null)
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                        case CadEditRecordKind.PropertyChanged:
                            edited.Add(record.Handle);
                            break;
                        case CadEditRecordKind.Erased:
                            break;
                        default:
                            continue;
                    }

                    DxfEntity entity = document.FindKept(record.Handle);

                    if (entity == null)
                    {
                        continue;
                    }

                    foreach (string reactor in entity.GetPersistentReactorIds())
                    {
                        if (
                            document.Database.TryGetObject(reactor, out DxfDBObject found)
                            && found is DxfDimAssoc assoc
                            && assoc.DimensionHandle != null
                        )
                        {
                            dimensions.Add(assoc.DimensionHandle.ToUpperInvariant());
                        }
                    }
                }
            }

            foreach (string handle in dimensions)
            {
                if (!(document.FindEntity(handle) is DxfDimension dimension) || dimension.DimAssoc == null)
                {
                    continue;
                }

                string space = document.FindSpace(handle)?.Name;
                string said = FollowOne(document, dimension, edited);

                if (said != null)
                {
                    followed.Add(
                        new CadEditRecord(
                            said == "Untied" ? CadEditRecordKind.PropertyChanged : CadEditRecordKind.Reshaped,
                            dimension.Handle,
                            "Associative",
                            null,
                            said
                        )
                        {
                            Space = space,
                        }
                    );
                }
            }

            return followed;
        }

        /// <summary>
        /// Ties the copies of associative dimensions to the copies of their objects made by the same command, as AutoCAD 2021's
        /// COPY of a line and its dimension ties the new dimension to the new line. A reference to an object not copied with it
        /// is left out; a copy whose objects were none of them copied is tied to nothing, as before.
        /// </summary>
        /// <param name="s">The edit.</param>
        /// <param name="copies">Each copy by the handle of its original.</param>
        internal static void TieCopies(CadEditScope s, IReadOnlyDictionary<string, DxfEntity> copies)
        {
            var originals = new List<string>(copies.Keys);
            originals.Sort(StringComparer.Ordinal);

            foreach (string original in originals)
            {
                if (
                    !(s.Document.FindEntity(original) is DxfDimension dimension)
                    || dimension.DimAssoc == null
                    || !(copies[original] is DxfDimension copy)
                )
                {
                    continue;
                }

                var references = new List<DxfOsnapPointRef>();

                foreach (DxfOsnapPointRef reference in dimension.DimAssoc.PointReferences)
                {
                    DxfEntity other = null;

                    if (
                        !copies.TryGetValue(reference.ObjectHandle, out DxfEntity to)
                        || (
                            reference.IntersectionObjectHandle != null
                            && !copies.TryGetValue(reference.IntersectionObjectHandle, out other)
                        )
                    )
                    {
                        continue;
                    }

                    references.Add(
                        WithLast(
                            Copy(
                                reference,
                                reference.PointIndex,
                                to.Handle,
                                PointOf(copy, reference.PointIndex) ?? reference.Point,
                                other?.Handle
                            ),
                            reference.LastPointReference,
                            copies
                        )
                    );
                }

                if (references.Count > 0)
                {
                    copy.SetDimAssoc(references);
                }
            }
        }

        /// <summary>Ties a dimension to references given anew, as DIMREASSOCIATE does, and moves its points to where they say.</summary>
        internal static void Reassociate(
            CadDocument document,
            DxfDimension dimension,
            IReadOnlyList<DxfOsnapPointRef> references
        )
        {
            dimension.SetDimAssoc(references);
            var points = new Dictionary<int, GeoPoint3>();
            var objects = new Dictionary<int, DxfEntity>();

            foreach (DxfOsnapPointRef reference in references)
            {
                DxfEntity entity = document.FindEntity(reference.ObjectHandle);
                GeoPoint3? point = entity == null ? null : Locate(document, dimension, reference, entity, true);

                if (point.HasValue)
                {
                    points[reference.PointIndex] = point.Value;
                    objects[reference.PointIndex] = entity;
                }
            }

            Place(document, dimension, points, objects);
        }

        /// <summary>
        /// Follows one dimension: "Moved" where its points moved, "Untied" where it was untied, null where nothing changed. A
        /// reference AutoCAD cannot place again (<see cref="Lost"/>) is dropped once an object of the dimension changed, and the
        /// second point of an angle of a circle (<see cref="CircleAngle"/>) is placed again only then: AutoCAD 2021 writes it
        /// where it does not stand, and ties it so without moving it.
        /// </summary>
        private static string FollowOne(CadDocument document, DxfDimension dimension, ISet<string> edited)
        {
            IReadOnlyList<DxfOsnapPointRef> references = dimension.DimAssoc.PointReferences;
            bool itselfEdited = edited.Contains(dimension.Handle);
            bool objectsEdited = false;

            foreach (DxfOsnapPointRef reference in references)
            {
                objectsEdited |=
                    document.FindEntity(reference.ObjectHandle) == null
                    || edited.Contains(reference.ObjectHandle)
                    || (
                        reference.IntersectionObjectHandle != null
                        && edited.Contains(reference.IntersectionObjectHandle)
                    )
                    || (
                        reference.LastPointReference?.ObjectHandle != null
                        && edited.Contains(reference.LastPointReference.ObjectHandle)
                    );
            }

            bool circleAngle = IsCircleAngle(dimension);
            var kept = new List<DxfOsnapPointRef>();
            var points = new Dictionary<int, GeoPoint3>();
            var objects = new Dictionary<int, DxfEntity>();

            foreach (DxfOsnapPointRef reference in references)
            {
                DxfEntity entity = document.FindEntity(reference.ObjectHandle);

                if (entity == null || (objectsEdited && Lost(reference, entity)))
                {
                    continue;
                }

                kept.Add(reference);

                if (!objectsEdited && (Lost(reference, entity) || (circleAngle && reference.PointIndex == 1)))
                {
                    continue;
                }

                GeoPoint3? point = Locate(document, dimension, reference, entity, !objectsEdited);

                if (point.HasValue)
                {
                    points[reference.PointIndex] = point.Value;
                    objects[reference.PointIndex] = entity;
                }
            }

            if (kept.Count == 0 || (itselfEdited && !OnItsPoints(dimension, points)))
            {
                dimension.ReleaseDimAssoc();
                return "Untied";
            }

            bool untied = false;

            if (kept.Count < references.Count)
            {
                dimension.SetDimAssoc(kept);
                untied = true;
            }

            return Place(document, dimension, points, objects) ? "Moved"
                : untied ? "Untied"
                : null;
        }

        /// <summary>
        /// Where a reference's point of a dimension is now: where its two objects cross for an intersection, the lines run on as
        /// far as needed and the crossing nearest the dimension's point taken where there are two (AutoCAD 2021 moved a
        /// DIMLINEAR tied to the crossing of two lines with the crossing when one of them moved); a perpendicular or a tangent
        /// drawn again (<see cref="DrawnAt"/>, where no object changed as <paramref name="unchanged"/> says); else as
        /// <see cref="Evaluate"/>.
        /// </summary>
        private static GeoPoint3? Locate(
            CadDocument document,
            DxfDimension dimension,
            DxfOsnapPointRef reference,
            DxfEntity entity,
            bool unchanged
        )
        {
            if (reference.OsnapType == DxfOsnapType.Perpendicular || reference.OsnapType == DxfOsnapType.Tangent)
            {
                return DrawnAt(document, dimension, reference, entity, unchanged);
            }

            if (reference.OsnapType != DxfOsnapType.Intersection)
            {
                return Evaluate(reference, entity);
            }

            DxfEntity other =
                reference.IntersectionObjectHandle == null
                    ? null
                    : document.FindEntity(reference.IntersectionObjectHandle);

            if (
                !PieceOf(entity, reference.GsMarker, out Piece a)
                || !PieceOf(other, reference.IntersectionGsMarker, out Piece b)
            )
            {
                return null;
            }

            GeoPoint3 near = PointOf(dimension, reference.PointIndex) ?? reference.Point;
            GeoPoint3? best = null;

            foreach (GeoPoint2 point in Crossings(a, b))
            {
                var candidate = new GeoPoint3(point.X, point.Y, near.Z);

                if (!best.HasValue || Distance(candidate, near) < Distance(best.Value, near))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Where two lines (run on), arcs or circles (whole) cross in the plane of the drawing.</summary>
        private static List<GeoPoint2> Crossings(Piece a, Piece b)
        {
            var found = new List<GeoPoint2>();

            if (!a.IsRound && b.IsRound)
            {
                Piece swap = a;
                a = b;
                b = swap;
            }

            if (!b.IsRound)
            {
                GeoPoint2 p = b.A;
                GeoVector2 d = b.B - b.A;

                if (!a.IsRound)
                {
                    GeoPoint2 q = a.A;
                    GeoVector2 e = a.B - a.A;
                    double cross = d.X * e.Y - d.Y * e.X;

                    if (Math.Abs(cross) > 1E-12 * d.Length * e.Length)
                    {
                        double t = ((q.X - p.X) * e.Y - (q.Y - p.Y) * e.X) / cross;
                        found.Add(p + d * t);
                    }

                    return found;
                }

                GeoPoint2 c = a.Center;
                double r = a.Radius;
                double dd = d.X * d.X + d.Y * d.Y;

                if (dd <= 0.0)
                {
                    return found;
                }

                double s = ((c.X - p.X) * d.X + (c.Y - p.Y) * d.Y) / dd;
                GeoPoint2 foot = p + d * s;
                double h = r * r - ((c - foot).X * (c - foot).X + (c - foot).Y * (c - foot).Y);

                if (h >= 0.0)
                {
                    GeoVector2 step = d * (Math.Sqrt(h) / Math.Sqrt(dd));
                    found.Add(foot - step);
                    found.Add(foot + step);
                }

                return found;
            }

            GeoPoint2 c1 = a.Center;
            GeoPoint2 c2 = b.Center;
            double r1 = a.Radius;
            double r2 = b.Radius;
            GeoVector2 between = c2 - c1;
            double far = between.Length;

            if (far <= 0.0 || far > r1 + r2 || far < Math.Abs(r1 - r2))
            {
                return found;
            }

            double along = (r1 * r1 - r2 * r2 + far * far) / (2.0 * far);
            double up = Math.Sqrt(Math.Max(0.0, r1 * r1 - along * along));
            GeoVector2 unit = between / far;
            GeoPoint2 mid = c1 + unit * along;
            found.Add(mid + new GeoVector2(-unit.Y, unit.X) * up);
            found.Add(mid - new GeoVector2(-unit.Y, unit.X) * up);
            return found;
        }

        /// <summary>
        /// Where a reference's point is now on its object: the end, middle, center, quadrant or nearest point of a line, an arc,
        /// a circle or a segment of a light polyline, a POINT's place; null where the object or the snap is not followed.
        /// </summary>
        internal static GeoPoint3? Evaluate(DxfOsnapPointRef reference, DxfEntity entity)
        {
            try
            {
                switch (entity)
                {
                    case DxfLine line:
                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.StartPoint:
                                return line.StartPoint;
                            case DxfOsnapType.Endpoint:
                                return line.EndPoint;
                            case DxfOsnapType.Midpoint:
                                return Lerp(line.StartPoint, line.EndPoint, 0.5);
                            case DxfOsnapType.Nearest:
                                return Lerp(line.StartPoint, line.EndPoint, reference.NearParameter);
                            default:
                                return null;
                        }

                    case DxfArc arc:
                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.StartPoint:
                                return arc.StartPoint;
                            case DxfOsnapType.Endpoint:
                                return arc.EndPoint;
                            case DxfOsnapType.Midpoint:
                                return arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0);
                            case DxfOsnapType.Center:
                                return arc.Center;
                            case DxfOsnapType.Quadrant:
                            case DxfOsnapType.Nearest:
                                return OnRound(arc.Center, arc.Radius, reference.NearParameter);
                            default:
                                return null;
                        }

                    case DxfCircle circle:
                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.Center:
                                return circle.Center;
                            case DxfOsnapType.Quadrant:
                            case DxfOsnapType.Nearest:
                                return OnRound(circle.Center, circle.Radius, reference.NearParameter);
                            default:
                                return null;
                        }

                    case DxfPolyline polyline:
                        int segment = reference.GsMarker - 1;
                        int segments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;

                        if (segment < 0 || segment >= segments)
                        {
                            return null;
                        }

                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.StartPoint:
                                return polyline.GetPointAtParameter(segment);
                            case DxfOsnapType.Endpoint:
                                return polyline.GetPointAtParameter(segment + 1.0);
                            case DxfOsnapType.Midpoint:
                                return polyline.GetPointAtParameter(segment + 0.5);
                        }

                        // A center, a quadrant or a nearest point of a segment: on its line at the share of the way, on its arc at
                        // the angle, as AutoCAD 2021 writes them.
                        if (
                            !PieceOf(polyline, reference.GsMarker, out Piece piece)
                            || (reference.OsnapType != DxfOsnapType.Nearest && !piece.IsRound)
                            || (
                                reference.OsnapType != DxfOsnapType.Nearest
                                && reference.OsnapType != DxfOsnapType.Center
                                && reference.OsnapType != DxfOsnapType.Quadrant
                            )
                        )
                        {
                            return null;
                        }

                        GeoPoint2 on =
                            reference.OsnapType == DxfOsnapType.Center
                                ? piece.Center
                                : PointOn(piece, reference.NearParameter);
                        return new GeoPoint3(on.X, on.Y, polyline.GetPointAtParameter(segment).Z);

                    case DxfDBPoint node when reference.OsnapType == DxfOsnapType.Node:
                        return node.Position;
                    default:
                        return null;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug(
                    "CadDimAssociation: the point " + reference + " cannot be found on its object: " + exception.Message
                );
                return null;
            }
        }

        /// <summary>Whether the tied points of a dimension are still where its own points are, as after it moved with its objects.</summary>
        private static bool OnItsPoints(DxfDimension dimension, Dictionary<int, GeoPoint3> points)
        {
            double least = Tolerance.Default.EqualPoint;

            foreach (KeyValuePair<int, GeoPoint3> pair in points)
            {
                GeoPoint3? own = PointOf(dimension, pair.Key);

                if (own.HasValue && Distance(pair.Value, own.Value) > least)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The point of a dimension a reference ties, as <see cref="DxfOsnapPointRef.PointIndex"/> numbers them; null for one it has not.</summary>
        private static GeoPoint3? PointOf(DxfDimension dimension, int index)
        {
            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    return index == 0 ? rotated.XLine1Point
                        : index == 1 ? rotated.XLine2Point
                        : (GeoPoint3?)null;
                case DxfAlignedDimension aligned:
                    return index == 0 ? aligned.XLine1Point
                        : index == 1 ? aligned.XLine2Point
                        : (GeoPoint3?)null;
                case DxfRadialDimension radial:
                    return index == 1 ? radial.Center : (GeoPoint3?)null;
                case DxfDiametricDimension diametric:
                    return index == 1 ? Lerp(diametric.ChordPoint, diametric.FarChordPoint, 0.5) : (GeoPoint3?)null;
                case DxfPoint3AngularDimension angular:
                    return index == 0 ? angular.XLine1Point
                        : index == 1 ? angular.XLine2Point
                        : index == 2 ? angular.CenterPoint
                        : (GeoPoint3?)null;
                case DxfLineAngularDimension2 two:
                    return index == 0 ? two.XLine1Start
                        : index == 1 ? two.XLine1End
                        : index == 2 ? two.XLine2Start
                        : index == 3 ? two.XLine2End
                        : (GeoPoint3?)null;
                case DxfOrdinateDimension ordinate:
                    return index == 1 ? ordinate.DefiningPoint : (GeoPoint3?)null;
                case DxfArcDimension arcLength:
                    return index == 0 ? arcLength.XLine1Point
                        : index == 1 ? arcLength.XLine2Point
                        : index == 2 ? arcLength.CenterPoint
                        : (GeoPoint3?)null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Moves a dimension's points to where its references say and draws it again: moved whole where every point it is made
        /// of moved by one step; else each tied point set, a linear one's dimension line keeping its distance from the origin it
        /// lay nearer to (or from the other, where that would bring it nearer the other), an aligned one's its distance from the
        /// line measured, a radius drawn out along its direction, an arc length dimension's arc as far out. False where nothing
        /// moved.
        /// </summary>
        private static bool Place(
            CadDocument document,
            DxfDimension dimension,
            Dictionary<int, GeoPoint3> points,
            Dictionary<int, DxfEntity> objects
        )
        {
            double least = Tolerance.Default.EqualPoint;
            var moves = new Dictionary<int, GeoVector3>();
            bool moved = false;

            foreach (KeyValuePair<int, GeoPoint3> pair in points)
            {
                GeoPoint3? own = PointOf(dimension, pair.Key);

                if (own.HasValue)
                {
                    GeoVector3 step = pair.Value - own.Value;
                    moves[pair.Key] = step;
                    moved |= step.Length > least;
                }
            }

            double? radius = null;
            double? oldRadius = null;

            if (
                (dimension is DxfRadialDimension || dimension is DxfDiametricDimension)
                && objects.TryGetValue(1, out DxfEntity round)
            )
            {
                radius =
                    round is DxfCircle circle ? circle.Radius
                    : round is DxfArc arc ? arc.Radius
                    : (double?)null;
                oldRadius = dimension is DxfRadialDimension r
                    ? Distance(r.ChordPoint, r.Center)
                    : Distance(
                        ((DxfDiametricDimension)dimension).ChordPoint,
                        ((DxfDiametricDimension)dimension).FarChordPoint
                    ) / 2.0;
                moved |= radius.HasValue && Math.Abs(radius.Value - oldRadius.Value) > least;
            }

            if (!moved)
            {
                return false;
            }

            CadCommands.OwnPicture(document.Database, dimension);

            if (MovedWhole(dimension, moves, radius, oldRadius, least, out GeoVector3 shift))
            {
                // The library moves the picture with the dimension only where its flag 32 says the block is its own; one the
                // presenter drew is moved here, its words where they stood on it.
                GeoTransform3 move = GeoTransform3.Translation(shift);
                GeoPoint3? before = Corner(dimension.DimBlock);
                dimension.TransformBy(move);
                GeoPoint3? after = Corner(dimension.DimBlock);

                if (before.HasValue && after.HasValue && Distance(before.Value, after.Value) <= least)
                {
                    foreach (DxfEntity entity in dimension.DimBlock)
                    {
                        if (!entity.IsErased)
                        {
                            entity.TransformBy(move);
                        }
                    }
                }

                return true;
            }

            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                {
                    GeoPoint3 p1 = points.TryGetValue(0, out GeoPoint3 a) ? a : rotated.XLine1Point;
                    GeoPoint3 p2 = points.TryGetValue(1, out GeoPoint3 b) ? b : rotated.XLine2Point;
                    var across = new GeoVector3(-Math.Sin(rotated.Rotation), Math.Cos(rotated.Rotation), 0.0);
                    double line = Dot(rotated.DimLinePoint, across);
                    double first = line - Dot(rotated.XLine1Point, across);
                    double second = line - Dot(rotated.XLine2Point, across);
                    // The origin the line lies nearer to; of two as near on either side of it, the one it lies across from
                    // (AutoCAD 2021's vertical one of (0, -4000) and (40, -3940) at x = 20 kept 20 from the second, its line going
                    // to x = -15 when the first went to (5, -4000)); else the first.
                    bool nearFirst =
                        Math.Abs(first) < Math.Abs(second) - least
                        || (!(Math.Abs(second) < Math.Abs(first) - least) && !(first < 0.0 && second > 0.0));
                    double keep = nearFirst ? first : second;
                    double far = Dot(nearFirst ? p2 : p1, across);
                    double now = Dot(nearFirst ? p1 : p2, across) + keep;

                    // Where that brings the line nearer the other origin than it was to the one it lay nearest, it goes as far
                    // from the other one instead, the same way: AutoCAD 2021 put the line of origins (100, 123) and (50, 77), at
                    // y = -60, to y = -137 when the first went to (120, 0), and that of (0, 0) and (60, 40), at y = 20, to y = 60
                    // when the first went to (0, 5).
                    if (Math.Abs(now - far) < Math.Abs(keep) - least)
                    {
                        now = far + keep;
                    }

                    rotated.XLine1Point = p1;
                    rotated.XLine2Point = p2;
                    rotated.DimLinePoint = p2 + across * (now - Dot(p2, across));
                    break;
                }

                case DxfAlignedDimension aligned:
                {
                    GeoPoint3 p1 = points.TryGetValue(0, out GeoPoint3 a) ? a : aligned.XLine1Point;
                    GeoPoint3 p2 = points.TryGetValue(1, out GeoPoint3 b) ? b : aligned.XLine2Point;
                    GeoVector3 was = aligned.XLine2Point - aligned.XLine1Point;
                    GeoVector3 way = p2 - p1;

                    if (way.Length <= least || was.Length <= least)
                    {
                        return false;
                    }

                    var oldAcross = new GeoVector3(-was.Y / was.Length, was.X / was.Length, 0.0);
                    var across = new GeoVector3(-way.Y / way.Length, way.X / way.Length, 0.0);
                    double offset = Dot(aligned.DimLinePoint, oldAcross) - Dot(aligned.XLine1Point, oldAcross);
                    aligned.XLine1Point = p1;
                    aligned.XLine2Point = p2;
                    aligned.DimLinePoint = p2 + across * offset;
                    break;
                }

                case DxfRadialDimension radial when radius.HasValue:
                {
                    GeoPoint3 center = points[1];
                    GeoVector3 outward = Unit(radial.ChordPoint - radial.Center);
                    GeoPoint3 chord = radial.ChordPoint;
                    radial.Center = center;
                    radial.ChordPoint = center + outward * radius.Value;
                    KeepWords(radial, chord, oldRadius.Value, radius.Value);
                    break;
                }

                case DxfDiametricDimension diametric when radius.HasValue:
                {
                    GeoPoint3 center = points[1];
                    GeoVector3 outward = Unit(diametric.ChordPoint - diametric.FarChordPoint);
                    GeoPoint3 chord = diametric.ChordPoint;
                    diametric.ChordPoint = center + outward * radius.Value;
                    diametric.FarChordPoint = center - outward * radius.Value;
                    KeepWords(diametric, chord, 2.0 * oldRadius.Value, 2.0 * radius.Value);
                    break;
                }

                case DxfPoint3AngularDimension angular:
                {
                    GeoPoint3 start = points.TryGetValue(0, out GeoPoint3 a) ? a : angular.XLine1Point;
                    GeoPoint3 end = points.TryGetValue(1, out GeoPoint3 b) ? b : angular.XLine2Point;
                    GeoPoint3 vertex = points.TryGetValue(2, out GeoPoint3 v) ? v : angular.CenterPoint;
                    angular.ArcPoint = ArcKept(
                        angular.CenterPoint,
                        angular.XLine1Point,
                        angular.XLine2Point,
                        angular.ArcPoint,
                        vertex,
                        start,
                        end
                    );
                    angular.XLine1Point = start;
                    angular.XLine2Point = end;
                    angular.CenterPoint = vertex;
                    break;
                }
                case DxfArcDimension arcLength:
                {
                    // AutoCAD 2021 leaves the ends of a leader (16, 17) and the angles (40, 41) as they were.
                    GeoPoint3 start = points.TryGetValue(0, out GeoPoint3 a) ? a : arcLength.XLine1Point;
                    GeoPoint3 end = points.TryGetValue(1, out GeoPoint3 b) ? b : arcLength.XLine2Point;
                    GeoPoint3 center = points.TryGetValue(2, out GeoPoint3 v) ? v : arcLength.CenterPoint;
                    arcLength.ArcPoint = CadCommands.ArcLengthPointAt(arcLength, center, start, end, false);
                    arcLength.XLine1Point = start;
                    arcLength.XLine2Point = end;
                    arcLength.CenterPoint = center;

                    // Where the words no longer fit between the extension lines the arc point goes two thirds of the way along
                    // (a third where they fit), as AutoCAD 2021 puts it again with the arc scaled.
                    if (CadCommands.ArcLengthWordsOutside(arcLength))
                    {
                        arcLength.ArcPoint = CadCommands.ArcLengthPointAt(arcLength, center, start, end, true);
                    }

                    break;
                }

                case DxfLineAngularDimension2 two:
                    if (!TwoLinesMoved(two, points))
                    {
                        return false;
                    }

                    break;
                case DxfOrdinateDimension ordinate when moves.TryGetValue(1, out GeoVector3 step):
                {
                    // AutoCAD 2021 moved the leader's end of an ordinate of X with its point along X alone, of Y along Y alone.
                    double turn = -ordinate.HorizontalRotation;
                    var axis = ordinate.UsingXAxis
                        ? new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0.0)
                        : new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0.0);
                    ordinate.DefiningPoint = points[1];
                    ordinate.LeaderEndPoint = ordinate.LeaderEndPoint + axis * Dot(step, axis);
                    break;
                }
                default:
                    return false;
            }

            if (!CadCommands.Redraw(dimension))
            {
                GeometryHelperLog.Debug(
                    "CadDimAssociation: the dimension "
                        + dimension.Handle
                        + " followed its objects but its picture could not be drawn again."
                );
            }

            return true;
        }

        /// <summary>Whether every point a dimension is made of moved by one step (and a radius kept its size), so that it moves whole.</summary>
        private static bool MovedWhole(
            DxfDimension dimension,
            Dictionary<int, GeoVector3> moves,
            double? radius,
            double? oldRadius,
            double least,
            out GeoVector3 shift
        )
        {
            int[] whole =
                dimension is DxfPoint3AngularDimension || dimension is DxfArcDimension ? new[] { 0, 1, 2 }
                : dimension is DxfLineAngularDimension2 ? new[] { 0, 1, 2, 3 }
                : dimension is DxfRadialDimension || dimension is DxfDiametricDimension ? new[] { 1 }
                : new[] { 0, 1 };
            shift = default;

            if (radius.HasValue && Math.Abs(radius.Value - oldRadius.Value) > least)
            {
                return false;
            }

            for (int i = 0; i < whole.Length; i++)
            {
                if (!moves.TryGetValue(whole[i], out GeoVector3 step))
                {
                    return false;
                }

                if (i == 0)
                {
                    shift = step;
                }
                else if ((step - shift).Length > least)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The reference of a point snapped on a light polyline, its segment the marker (1 for the first), as AutoCAD 2021 writes
        /// them: an end of a segment (the start of the first, else the end of the segment that ends there), its middle, a
        /// nearest point and a quadrant at their parameter, the center of an arc at the angle it starts at. The parameter on a
        /// line is the share of the way along it, on an arc the angle counted on from where it starts: of a polyline from
        /// (0, 200) to (100, 200) and round a half circle to (100, 300), the end of the half circle is of marker 2 and parameter
        /// 7.85398 and its center of parameter 4.71239. Null for another snap.
        /// </summary>
        private static DxfOsnapPointRef OnPolyline(
            int pointIndex,
            CadSnapResult snap,
            DxfPolyline polyline,
            GeoPoint3 at
        )
        {
            double least = Tolerance.Default.EqualPoint;
            int count = polyline.NumberOfVertices;
            int segments = polyline.Closed ? count : count - 1;
            var p = new GeoPoint2(at.X, at.Y);

            switch (snap.Kind)
            {
                case CadSnapKind.Endpoint:
                case CadSnapKind.Midpoint:
                    for (int i = 0; i < segments; i++)
                    {
                        double[] places =
                            snap.Kind == CadSnapKind.Midpoint ? new[] { 0.5 }
                            : i == 0 ? new[] { 0.0, 1.0 }
                            : new[] { 1.0 };

                        foreach (double t in places)
                        {
                            GeoPoint3 point = polyline.GetPointAtParameter(i + t);

                            if (Distance(snap.Point, point) <= least && PieceOf(polyline, i + 1, out Piece piece))
                            {
                                DxfOsnapType type =
                                    snap.Kind == CadSnapKind.Midpoint ? DxfOsnapType.Midpoint
                                    : t == 0.0 ? DxfOsnapType.StartPoint
                                    : DxfOsnapType.Endpoint;
                                double parameter = piece.IsRound
                                    ? ParameterOn(piece, new GeoPoint2(point.X, point.Y))
                                    : t;
                                return new DxfOsnapPointRef(
                                    pointIndex,
                                    type,
                                    polyline.Handle,
                                    i + 1,
                                    parameter,
                                    point,
                                    null
                                );
                            }
                        }
                    }

                    return null;
                case CadSnapKind.Center:
                    for (int i = 1; i <= segments; i++)
                    {
                        if (
                            PieceOf(polyline, i, out Piece piece)
                            && piece.IsRound
                            && (piece.Center - p).Length <= least
                        )
                        {
                            return new DxfOsnapPointRef(
                                pointIndex,
                                DxfOsnapType.Center,
                                polyline.Handle,
                                i,
                                piece.Start,
                                new GeoPoint3(piece.Center.X, piece.Center.Y, at.Z),
                                null
                            );
                        }
                    }

                    return null;
                case CadSnapKind.Nearest:
                case CadSnapKind.Quadrant:
                {
                    int marker = MarkerAt(polyline, p);

                    if (
                        !PieceOf(polyline, marker, out Piece piece)
                        || (snap.Kind == CadSnapKind.Quadrant && !piece.IsRound)
                    )
                    {
                        return null;
                    }

                    return new DxfOsnapPointRef(
                        pointIndex,
                        snap.Kind == CadSnapKind.Quadrant ? DxfOsnapType.Quadrant : DxfOsnapType.Nearest,
                        polyline.Handle,
                        marker,
                        ParameterOn(piece, p),
                        at,
                        null
                    );
                }

                default:
                    return null;
            }
        }

        private static DxfOsnapPointRef Round(
            int pointIndex,
            CadSnapResult snap,
            DxfEntity curve,
            GeoPoint3 center,
            GeoPoint3 at
        )
        {
            double angle = Math.Atan2(snap.Point.Y - center.Y, snap.Point.X - center.X);
            angle = angle < 0.0 ? angle + 2.0 * Math.PI : angle;

            switch (snap.Kind)
            {
                case CadSnapKind.Center:
                    return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Center, curve.Handle, 0, 0.0, center, null);
                case CadSnapKind.Quadrant:
                    return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Quadrant, curve.Handle, 0, angle, at, null);
                case CadSnapKind.Nearest:
                    return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Nearest, curve.Handle, 0, angle, at, null);
                default:
                    return null;
            }
        }

        /// <summary>The least corner of the box of the first entity of a dimension's block that has one, to see whether the block moved.</summary>
        private static GeoPoint3? Corner(DxfBlockTableRecord block)
        {
            if (block == null)
            {
                return null;
            }

            foreach (DxfEntity entity in block)
            {
                try
                {
                    GeoAabb3 box = entity.GeometricExtents;

                    if (!entity.IsErased && !box.IsEmpty)
                    {
                        return box.Min;
                    }
                }
                catch (InvalidOperationException) { }
            }

            return null;
        }

        /// <summary>
        /// Moves the points of an angle of two lines to where its references say, its arc kept as far from the vertex and at the
        /// same share of the angle from the first line, as AutoCAD 2021 kept it: a quarter of the way round from the first line
        /// stays a quarter of the way round when the second line turns. False where the lines no longer meet or the angle is
        /// one no longer.
        /// </summary>
        private static bool TwoLinesMoved(DxfLineAngularDimension2 two, Dictionary<int, GeoPoint3> points)
        {
            GeoPoint3[] was = { two.XLine1Start, two.XLine1End, two.XLine2Start, two.XLine2End };
            var now = new GeoPoint3[4];

            for (int i = 0; i < 4; i++)
            {
                now[i] = points.TryGetValue(i, out GeoPoint3 point) ? point : was[i];
            }

            if (!Vertex(was, out GeoPoint2 v0) || !Vertex(now, out GeoPoint2 v1))
            {
                return false;
            }

            GeoVector2 d1 = Way(was[0], was[1]);
            GeoVector2 d2 = Way(was[2], was[3]);
            GeoVector2 arc = new GeoVector2(two.ArcPoint.X - v0.X, two.ArcPoint.Y - v0.Y);
            double cross = d1.X * d2.Y - d1.Y * d2.X;

            // The rays of the angle the arc lies in: each line's direction or its reverse, as the arc point is made of them.
            double a = (arc.X * d2.Y - arc.Y * d2.X) / cross;
            double b = (d1.X * arc.Y - d1.Y * arc.X) / cross;
            double s1 = a < 0.0 ? -1.0 : 1.0;
            double s2 = b < 0.0 ? -1.0 : 1.0;
            double share = Between(d1 * s1, arc) / Between(d1 * s1, d2 * s2);
            GeoVector2 r1 = Way(now[0], now[1]) * s1;
            GeoVector2 r2 = Way(now[2], now[3]) * s2;
            double turn = Between(r1, r2) * share * (r1.X * r2.Y - r1.Y * r2.X < 0.0 ? -1.0 : 1.0);
            double from = Math.Atan2(r1.Y, r1.X) + turn;
            double radius = arc.Length;

            two.XLine1Start = now[0];
            two.XLine1End = now[1];
            two.XLine2Start = now[2];
            two.XLine2End = now[3];
            two.ArcPoint = new GeoPoint3(
                v1.X + radius * Math.Cos(from),
                v1.Y + radius * Math.Sin(from),
                two.ArcPoint.Z
            );
            return true;
        }

        /// <summary>
        /// Keeps the words of a radius or a diameter where they stand while its circle grows or shrinks, as AutoCAD 2021 keeps
        /// them: their end on the side of the leader (the end nearer the point the line ran to on the rim before, given) stays,
        /// and words grown or shrunk by a letter grow or shrink away from it. AutoCAD 2021 kept group 11 (560.799, 320) of a
        /// radius of a circle of 40 about (500, 300) scaled by 1.5, "R60.0000" as wide as "R40.0000", and moved that of a radius
        /// of 9, its words outside to the right of the rim, from 20.7295 to 20.7994 when "R9.0000" became "R13.5000", half the
        /// width of the digit gained; words inside to the left of the rim moved left as much.
        /// </summary>
        private static void KeepWords(DxfDimension dimension, GeoPoint3 chord, double before, double after)
        {
            GeoPoint3 words = dimension.TextPosition;
            dimension.UsingDefaultTextPosition = false;
            double grown =
                CadCommands.RoundWordsWidth(dimension, after) - CadCommands.RoundWordsWidth(dimension, before);

            if (Math.Abs(grown) <= Tolerance.Default.EqualPoint * 1E-3)
            {
                dimension.TextPosition = words;
                return;
            }

            double turn = 0.0;

            foreach (DxfEntity entity in dimension.DimBlock ?? (IEnumerable<DxfEntity>)Array.Empty<DxfEntity>())
            {
                if (entity is DxfMText text && !text.IsErased)
                {
                    turn = text.Rotation;
                    break;
                }
            }

            // The way the words read, turned to run from their end on the leader's side to their middle.
            var way = new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0.0);
            double toward = (chord.X - words.X) * way.X + (chord.Y - words.Y) * way.Y;
            way = toward > 0.0 ? way * -1.0 : way;
            dimension.TextPosition = words + way * (grown / 2.0);
        }

        /// <summary>
        /// Where the arc of an angle of three points goes when its points move, as AutoCAD 2021 puts it: as far from the vertex,
        /// in the angle between the legs where it measured less than half a turn and outside it where it measured more, at the
        /// same share of the angle (a third, as AutoCAD makes it) counted round from the leg it starts at. AutoCAD 2021 moved the
        /// arc of the angle of a circle of 25 about (200, 0) from (225, 0) round to (222.36, 11.18), drawn the long way 100 out,
        /// to (151.573, 77.492) when the circle moved 10 down: a third of 226.55 degrees on from the second leg; and an arc of
        /// 18 degrees from the second leg to the first stayed inside, now from the first, when the second leg crossed the first.
        /// An arc on a leg, or of an angle that measured nothing, takes a third of the angle it measures now.
        /// </summary>
        private static GeoPoint3 ArcKept(
            GeoPoint3 vertex,
            GeoPoint3 first,
            GeoPoint3 second,
            GeoPoint3 arc,
            GeoPoint3 newVertex,
            GeoPoint3 newFirst,
            GeoPoint3 newSecond
        )
        {
            double a1 = Math.Atan2(first.Y - vertex.Y, first.X - vertex.X);
            double a2 = Math.Atan2(second.Y - vertex.Y, second.X - vertex.X);
            double at = Math.Atan2(arc.Y - vertex.Y, arc.X - vertex.X);
            double turn = CadGrips.Sweep(a1, a2);
            bool fromFirst = CadGrips.Sweep(a1, at) <= turn;
            double sweep = fromFirst ? turn : 2.0 * Math.PI - turn;
            double least = Tolerance.Default.EqualAngleRad;
            double share = sweep > least ? Normal(at - (fromFirst ? a1 : a2)) / sweep : 0.0;

            if (share * sweep <= least || (1.0 - share) * sweep <= least)
            {
                share = 1.0 / 3.0;
            }

            double b1 = Math.Atan2(newFirst.Y - newVertex.Y, newFirst.X - newVertex.X);
            double b2 = Math.Atan2(newSecond.Y - newVertex.Y, newSecond.X - newVertex.X);
            double newTurn = CadGrips.Sweep(b1, b2);
            bool fromNewFirst = newTurn < Math.PI == sweep < Math.PI;
            double angle = fromNewFirst ? b1 + share * newTurn : b2 + share * (2.0 * Math.PI - newTurn);
            double radius = Distance(arc, vertex);
            return new GeoPoint3(newVertex.X + radius * Math.Cos(angle), newVertex.Y + radius * Math.Sin(angle), arc.Z);
        }

        /// <summary>Where the lines of the four points of an angle of two lines meet, run on; false where they are parallel or a line has no length.</summary>
        private static bool Vertex(GeoPoint3[] points, out GeoPoint2 vertex)
        {
            vertex = default;
            GeoVector2 d1 = new GeoVector2(points[1].X - points[0].X, points[1].Y - points[0].Y);
            GeoVector2 d2 = new GeoVector2(points[3].X - points[2].X, points[3].Y - points[2].Y);
            double cross = d1.X * d2.Y - d1.Y * d2.X;

            if (d1.Length <= 0.0 || d2.Length <= 0.0 || Math.Abs(cross) <= 1E-12 * d1.Length * d2.Length)
            {
                return false;
            }

            double t = ((points[2].X - points[0].X) * d2.Y - (points[2].Y - points[0].Y) * d2.X) / cross;
            vertex = new GeoPoint2(points[0].X + d1.X * t, points[0].Y + d1.Y * t);
            return true;
        }

        private static GeoVector2 Way(GeoPoint3 from, GeoPoint3 to)
        {
            var way = new GeoVector2(to.X - from.X, to.Y - from.Y);
            return way.Length > 0.0 ? way / way.Length : new GeoVector2(1.0, 0.0);
        }

        /// <summary>The angle between two directions, 0 to pi.</summary>
        private static double Between(GeoVector2 a, GeoVector2 b) =>
            Math.Abs(Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y));

        private static double Dot(GeoVector3 a, GeoVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        private static GeoPoint3 OnRound(GeoPoint3 center, double radius, double angle) =>
            new GeoPoint3(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), center.Z);

        private static double Along(DxfLine line, GeoPoint2 point)
        {
            GeoVector3 way = line.EndPoint - line.StartPoint;
            double squared = way.X * way.X + way.Y * way.Y;
            return squared <= 0.0
                ? 0.0
                : ((point.X - line.StartPoint.X) * way.X + (point.Y - line.StartPoint.Y) * way.Y) / squared;
        }

        /// <summary>
        /// Whether a reference is one AutoCAD 2021 cannot place again, and drops when its dimension follows an edit, the point
        /// staying where it is: a nearest point past the ends of a line or a straight segment of a polyline, or of a POINT, as it
        /// ties the second point of an angle of a circle (-0.9197 along (260, 40)-(300, 40)).
        /// </summary>
        private static bool Lost(DxfOsnapPointRef reference, DxfEntity entity)
        {
            if (reference.OsnapType != DxfOsnapType.Nearest)
            {
                return false;
            }

            switch (entity)
            {
                case DxfLine line:
                    return !Within(reference.NearParameter, (line.EndPoint - line.StartPoint).Length);
                case DxfPolyline polyline:
                    return PieceOf(polyline, reference.GsMarker, out Piece piece)
                        && !piece.IsRound
                        && !Within(reference.NearParameter, (piece.B - piece.A).Length);
                case DxfDBPoint _:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether a dimension is the angle of a circle as <see cref="CircleAngle"/> ties it: an angle of three points whose
        /// first point is a nearest point of a circle and whose vertex is that circle's center.
        /// </summary>
        private static bool IsCircleAngle(DxfDimension dimension)
        {
            if (!(dimension is DxfPoint3AngularDimension))
            {
                return false;
            }

            DxfOsnapPointRef first = null;
            DxfOsnapPointRef vertex = null;

            foreach (DxfOsnapPointRef reference in dimension.DimAssoc.PointReferences)
            {
                first = reference.PointIndex == 0 ? reference : first;
                vertex = reference.PointIndex == 2 ? reference : vertex;
            }

            return first != null
                && vertex != null
                && first.OsnapType == DxfOsnapType.Nearest
                && vertex.OsnapType == DxfOsnapType.Center
                && string.Equals(first.ObjectHandle, vertex.ObjectHandle, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether a parameter along a line of a length lies between its ends, to the point tolerance.</summary>
        private static bool Within(double parameter, double length)
        {
            double slack = length > 0.0 ? Tolerance.Default.EqualPoint / length : 0.0;
            return parameter >= -slack && parameter <= 1.0 + slack;
        }

        private static GeoPoint3 Lerp(GeoPoint3 a, GeoPoint3 b, double t) =>
            new GeoPoint3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static double Distance(GeoPoint2 a, GeoPoint3 b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static double Distance(GeoPoint3 a, GeoPoint3 b) => (a - b).Length;

        private static double Dot(GeoPoint3 point, GeoVector3 way) =>
            point.X * way.X + point.Y * way.Y + point.Z * way.Z;

        private static GeoVector3 Unit(GeoVector3 vector) =>
            vector.Length > 0.0 ? vector / vector.Length : new GeoVector3(1.0, 0.0, 0.0);
    }
}
