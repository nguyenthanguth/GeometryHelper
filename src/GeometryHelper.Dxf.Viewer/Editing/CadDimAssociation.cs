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
    /// from the origin it lay nearer to (the first where both lie as near); a circle scaled about its center draws its radius
    /// out along the same direction, the words where they stood; the object erased unties the dimension, which keeps its
    /// points ("Dimension disassociated."). A dimension moved or reshaped on its own, its tied points no longer on their
    /// objects, is untied too, as AutoCAD unties one moved without its line.
    /// </para>
    /// <para>
    /// The points followed are the ends, middles, centers, quadrants and nearest points of lines, arcs, circles and the
    /// segments of light polylines, POINTs, where two lines, arcs or circles cross, and the feet of perpendiculars to lines,
    /// arcs and circles and the points of tangency of arcs and circles, drawn again from the point they were drawn from. A
    /// point tied to another kind of object stays where it is.
    /// </para>
    /// </remarks>
    internal static class CadDimAssociation
    {
        /// <summary>
        /// The reference an object snap gives for a point of a dimension: its object and where on it the point lies; null for a
        /// snap of no object, of a kind not tied, or of an object not followed. A perpendicular or a tangent is tied where the
        /// point it was drawn from is given (<paramref name="from"/>), with the reference of that point
        /// (<paramref name="fromTied"/>, or one of no object), as AutoCAD 2021 writes them.
        /// </summary>
        internal static DxfOsnapPointRef FromSnap(CadDocument document, int pointIndex, CadSnapResult snap, GeoPoint2? from = null, DxfOsnapPointRef fromTied = null)
        {
            if (snap == null || snap.Handle == null)
            {
                return null;
            }

            DxfEntity entity = document.FindEntity(snap.Handle);
            GeoPoint3 at = new GeoPoint3(snap.Point.X, snap.Point.Y, 0.0);
            double least = Tolerance.Default.EqualPoint;

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
                            return new DxfOsnapPointRef(pointIndex, start ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint, line.Handle, 0, start ? 0.0 : 1.0, start ? line.StartPoint : line.EndPoint, null);
                        case CadSnapKind.Midpoint:
                            return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Midpoint, line.Handle, 0, 0.5, at, null);
                        case CadSnapKind.Nearest:
                            return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Nearest, line.Handle, 0, Along(line, snap.Point), at, null);
                        default:
                            return null;
                    }

                case DxfArc arc:
                    switch (snap.Kind)
                    {
                        case CadSnapKind.Endpoint:
                            bool start = Distance(snap.Point, arc.StartPoint) <= Distance(snap.Point, arc.EndPoint);
                            return new DxfOsnapPointRef(pointIndex, start ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint, arc.Handle, 0, start ? arc.StartAngle : arc.EndAngle, start ? arc.StartPoint : arc.EndPoint, null);
                        case CadSnapKind.Midpoint:
                            return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Midpoint, arc.Handle, 0, (arc.StartParam + arc.EndParam) / 2.0, at, null);
                        default:
                            return Round(pointIndex, snap, arc, arc.Center, at);
                    }

                case DxfCircle circle:
                    return Round(pointIndex, snap, circle, circle.Center, at);
                case DxfPolyline polyline when snap.Kind == CadSnapKind.Endpoint || snap.Kind == CadSnapKind.Midpoint:
                    int count = polyline.NumberOfVertices;
                    int segments = polyline.Closed ? count : count - 1;

                    for (int i = 0; i < segments; i++)
                    {
                        double[] places = snap.Kind == CadSnapKind.Midpoint ? new[] { 0.5 } : i == 0 ? new[] { 0.0, 1.0 } : new[] { 1.0 };

                        foreach (double t in places)
                        {
                            GeoPoint3 point = polyline.GetPointAtParameter(i + t);

                            if (Distance(snap.Point, point) <= least)
                            {
                                DxfOsnapType type = snap.Kind == CadSnapKind.Midpoint ? DxfOsnapType.Midpoint : t == 0.0 ? DxfOsnapType.StartPoint : DxfOsnapType.Endpoint;
                                return new DxfOsnapPointRef(pointIndex, type, polyline.Handle, i + 1, t, point, null);
                            }
                        }
                    }

                    return null;
                case DxfDBPoint node when snap.Kind == CadSnapKind.Node:
                    return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Node, node.Handle, 0, 0.0, node.Position, null);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The reference of a point snapped where two objects cross: the line, arc or circle drawn later first (group 331), the
        /// other second (332), its parameter on the first, as AutoCAD 2021 wrote a DIMLINEAR snapped to the crossing of two
        /// lines; null where no second line, arc or circle passes through the point.
        /// </summary>
        private static DxfOsnapPointRef Crossing(CadDocument document, int pointIndex, DxfEntity entity, GeoPoint3 at)
        {
            CadSpace space = entity == null ? null : document.FindSpace(entity.Handle);

            if (!Crossable(entity) || space == null)
            {
                return null;
            }

            double least = Tolerance.Default.EqualPoint;
            DxfEntity other = null;

            foreach (string handle in document.GetPickIndex(space).Pick(at.X, at.Y, least))
            {
                DxfEntity found = document.FindEntity(handle);

                if (!ReferenceEquals(found, entity) && Crossable(found) && Distance(((DxfCurve)found).GetClosestPointTo(at, false), at) <= least
                    && (other == null || Newer(other.Handle, found.Handle)))
                {
                    other = found;
                }
            }

            if (other == null)
            {
                return null;
            }

            DxfEntity first = Newer(entity.Handle, other.Handle) ? other : entity;
            DxfEntity second = ReferenceEquals(first, entity) ? other : entity;
            double parameter = first is DxfLine line ? Along(line, new GeoPoint2(at.X, at.Y)) : AngleAbout(first is DxfArc arc ? arc.Center : ((DxfCircle)first).Center, at);
            return new DxfOsnapPointRef(pointIndex, DxfOsnapType.Intersection, first.Handle, 0, parameter, at, second.Handle);
        }

        /// <summary>
        /// The reference of a point snapped perpendicular to a line, an arc or a circle, or tangent to an arc or a circle, from
        /// another point: its parameter on the object (along a line, run on as far as needed, as AutoCAD 2021 wrote 2.25 for a
        /// foot past the end; the angle on an arc or a circle), and the reference of the point drawn from; null with no point
        /// drawn from, or for another object.
        /// </summary>
        private static DxfOsnapPointRef Drawn(int pointIndex, bool tangent, DxfEntity entity, GeoPoint3 at, GeoPoint2? from, DxfOsnapPointRef fromTied)
        {
            if (!from.HasValue || !Crossable(entity) || (tangent && entity is DxfLine))
            {
                return null;
            }

            double parameter = entity is DxfLine line ? Along(line, new GeoPoint2(at.X, at.Y)) : AngleAbout(entity is DxfArc arc ? arc.Center : ((DxfCircle)entity).Center, at);
            var reference = new DxfOsnapPointRef(pointIndex, tangent ? DxfOsnapType.Tangent : DxfOsnapType.Perpendicular, entity.Handle, 0, parameter, at, null);
            return fromTied != null ? reference.WithLastPoint(fromTied) : reference.WithLastPoint(new GeoPoint3(from.Value.X, from.Value.Y, 0.0));
        }

        /// <summary>
        /// Where a point snapped perpendicular or tangent is now, drawn again from the point it was drawn from: its last point's
        /// object where that is tied and still there, else the dimension's other origin (AutoCAD 2021 moved a DIMLINEAR's
        /// perpendicular first point, drawn from a point tied to nothing, square to the line from its second origin). The foot
        /// on the line run on, or on the arc or the circle; of the two points of tangency, the one nearer the dimension's point.
        /// A tangent of a circle of 25 about (200, 0) from (300, 100), the circle moved 10 down, goes to (184.593, 9.688).
        /// </summary>
        private static GeoPoint3? DrawnAt(CadDocument document, DxfDimension dimension, DxfOsnapPointRef reference, DxfEntity entity)
        {
            DxfOsnapPointRef last = reference.LastPointReference;
            DxfEntity lastObject = last?.ObjectHandle == null ? null : document.FindEntity(last.ObjectHandle);
            GeoPoint3? from = lastObject == null ? null : Evaluate(last, lastObject);

            if (!from.HasValue && reference.PointIndex < 2)
            {
                from = PointOf(dimension, 1 - reference.PointIndex);
            }

            if (!from.HasValue)
            {
                return null;
            }

            var f = new GeoPoint2(from.Value.X, from.Value.Y);
            GeoPoint3 near = PointOf(dimension, reference.PointIndex) ?? reference.Point;

            if (entity is DxfLine line)
            {
                if (reference.OsnapType != DxfOsnapType.Perpendicular || (line.EndPoint - line.StartPoint).Length <= 0.0)
                {
                    return null;
                }

                GeoPoint3 foot = Lerp(line.StartPoint, line.EndPoint, Along(line, f));
                return new GeoPoint3(foot.X, foot.Y, near.Z);
            }

            if (!Crossable(entity))
            {
                return null;
            }

            Round(entity, out GeoPoint2 center, out double radius);
            GeoVector2 outward = f - center;
            double far = outward.Length;

            if (far <= 0.0 || (reference.OsnapType == DxfOsnapType.Tangent && far <= radius))
            {
                return null;
            }

            GeoVector2 unit = outward / far;
            double turn = reference.OsnapType == DxfOsnapType.Tangent ? Math.Acos(radius / far) : 0.0;
            GeoPoint2? best = null;

            foreach (double angle in reference.OsnapType == DxfOsnapType.Tangent ? new[] { turn, -turn } : new[] { 0.0, Math.PI })
            {
                var way = new GeoVector2(unit.X * Math.Cos(angle) - unit.Y * Math.Sin(angle), unit.X * Math.Sin(angle) + unit.Y * Math.Cos(angle));
                GeoPoint2 candidate = center + way * radius;

                if (!best.HasValue || Distance(candidate, near) < Distance(best.Value, near))
                {
                    best = candidate;
                }
            }

            return new GeoPoint3(best.Value.X, best.Value.Y, near.Z);
        }

        private static bool Crossable(DxfEntity entity) => entity is DxfLine || entity is DxfArc || entity is DxfCircle;

        /// <summary>Whether the second handle is of an object made after the first's, by the number the handles are.</summary>
        private static bool Newer(string first, string second)
            => second.Length != first.Length ? second.Length > first.Length : string.CompareOrdinal(second.ToUpperInvariant(), first.ToUpperInvariant()) > 0;

        /// <summary>
        /// A reference given another point of a dimension: the same object and place, and the same point drawn from, numbered as
        /// the point it ties now; null for null.
        /// </summary>
        internal static DxfOsnapPointRef Renumbered(DxfOsnapPointRef reference, int pointIndex)
            => reference == null ? null : WithLast(new DxfOsnapPointRef(pointIndex, reference.OsnapType, reference.ObjectHandle, reference.GsMarker, reference.NearParameter, reference.Point, reference.IntersectionObjectHandle), reference.LastPointReference, null);

        /// <summary>
        /// A reference given the point its perpendicular or tangent was drawn from, as another had it: that point's reference,
        /// its object put for the one named in <paramref name="objects"/> where it is there, or a point of no object where it
        /// was of none or its object is not there; the reference as it is where it had none.
        /// </summary>
        private static DxfOsnapPointRef WithLast(DxfOsnapPointRef reference, DxfOsnapPointRef last, IReadOnlyDictionary<string, DxfEntity> objects)
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

            return objects.TryGetValue(last.ObjectHandle, out DxfEntity to) && (last.IntersectionObjectHandle == null || objects.ContainsKey(last.IntersectionObjectHandle))
                ? reference.WithLastPoint(new DxfOsnapPointRef(last.PointIndex, last.OsnapType, to.Handle, last.GsMarker, last.NearParameter, last.Point, last.IntersectionObjectHandle == null ? null : objects[last.IntersectionObjectHandle].Handle))
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
        /// The references of two lines selected for DIMANGULAR: the start and the end of the first, then of the second, the
        /// four points of the dimension, as AutoCAD 2021 ties each to its line.
        /// </summary>
        internal static DxfOsnapPointRef[] TwoLines(DxfLine first, DxfLine second) => new[]
        {
            new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, first.Handle, 0, 0.0, first.StartPoint, null),
            new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, first.Handle, 0, 1.0, first.EndPoint, null),
            new DxfOsnapPointRef(2, DxfOsnapType.StartPoint, second.Handle, 0, 0.0, second.StartPoint, null),
            new DxfOsnapPointRef(3, DxfOsnapType.Endpoint, second.Handle, 0, 1.0, second.EndPoint, null),
        };

        /// <summary>The references of a line selected for DIMLINEAR or DIMALIGNED: its start and its end, as AutoCAD 2021 writes them.</summary>
        internal static DxfOsnapPointRef[] LineEnds(DxfLine line) => new[]
        {
            new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, line.Handle, 0, 0.0, line.StartPoint, null),
            new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, line.Handle, 0, 1.0, line.EndPoint, null),
        };

        /// <summary>The reference of an arc or a circle selected for DIMRADIUS or DIMDIAMETER: its center, the second point, as AutoCAD 2021 writes it.</summary>
        internal static DxfOsnapPointRef[] CenterOf(DxfEntity curve) => new[] { new DxfOsnapPointRef(1, DxfOsnapType.Center, curve.Handle, 0, 0.0, curve is DxfArc arc ? arc.Center : ((DxfCircle)curve).Center, null) };

        /// <summary>The references of an arc selected for DIMANGULAR: its start, its end and its center, as AutoCAD 2021 writes them.</summary>
        internal static DxfOsnapPointRef[] ArcEnds(DxfArc arc) => new[]
        {
            new DxfOsnapPointRef(0, DxfOsnapType.StartPoint, arc.Handle, 0, arc.StartAngle, arc.StartPoint, null),
            new DxfOsnapPointRef(1, DxfOsnapType.Endpoint, arc.Handle, 0, arc.EndAngle, arc.EndPoint, null),
            new DxfOsnapPointRef(2, DxfOsnapType.Center, arc.Handle, 0, 0.0, arc.Center, null),
        };

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

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Associative", "No", "Yes") }, s => ((DxfDimension)s.Document.FindEntity(handle)).SetDimAssoc(list));
        }

        /// <summary>
        /// Follows an edit: each associative dimension tied to an entity the edit changed or erased has its points moved to where
        /// the references now say and its picture drawn again, or is untied where its objects are gone or it was moved off them
        /// itself. Returns the records of the dimensions changed so, for the edit's step; none where no dimension is tied.
        /// </summary>
        internal static List<CadEditRecord> Follow(CadDocument document, IReadOnlyList<CadEditRecord> planned, IReadOnlyList<CadEditRecord> made)
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
                        if (document.Database.TryGetObject(reactor, out DxfDBObject found) && found is DxfDimAssoc assoc && assoc.DimensionHandle != null)
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
                string said = FollowOne(document, dimension, edited.Contains(handle));

                if (said != null)
                {
                    followed.Add(new CadEditRecord(said == "Untied" ? CadEditRecordKind.PropertyChanged : CadEditRecordKind.Reshaped, dimension.Handle, "Associative", null, said) { Space = space });
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
                if (!(s.Document.FindEntity(original) is DxfDimension dimension) || dimension.DimAssoc == null || !(copies[original] is DxfDimension copy))
                {
                    continue;
                }

                var references = new List<DxfOsnapPointRef>();

                foreach (DxfOsnapPointRef reference in dimension.DimAssoc.PointReferences)
                {
                    DxfEntity other = null;

                    if (!copies.TryGetValue(reference.ObjectHandle, out DxfEntity to) || (reference.IntersectionObjectHandle != null && !copies.TryGetValue(reference.IntersectionObjectHandle, out other)))
                    {
                        continue;
                    }

                    references.Add(WithLast(new DxfOsnapPointRef(reference.PointIndex, reference.OsnapType, to.Handle, reference.GsMarker, reference.NearParameter, PointOf(copy, reference.PointIndex) ?? reference.Point, other?.Handle), reference.LastPointReference, copies));
                }

                if (references.Count > 0)
                {
                    copy.SetDimAssoc(references);
                }
            }
        }

        /// <summary>Ties a dimension to references given anew, as DIMREASSOCIATE does, and moves its points to where they say.</summary>
        internal static void Reassociate(CadDocument document, DxfDimension dimension, IReadOnlyList<DxfOsnapPointRef> references)
        {
            dimension.SetDimAssoc(references);
            var points = new Dictionary<int, GeoPoint3>();
            var objects = new Dictionary<int, DxfEntity>();

            foreach (DxfOsnapPointRef reference in references)
            {
                DxfEntity entity = document.FindEntity(reference.ObjectHandle);
                GeoPoint3? point = entity == null ? null : Locate(document, dimension, reference, entity);

                if (point.HasValue)
                {
                    points[reference.PointIndex] = point.Value;
                    objects[reference.PointIndex] = entity;
                }
            }

            Place(document, dimension, points, objects);
        }

        /// <summary>Follows one dimension: "Moved" where its points moved, "Untied" where it was untied, null where nothing changed.</summary>
        private static string FollowOne(CadDocument document, DxfDimension dimension, bool itselfEdited)
        {
            IReadOnlyList<DxfOsnapPointRef> references = dimension.DimAssoc.PointReferences;
            var kept = new List<DxfOsnapPointRef>();
            var points = new Dictionary<int, GeoPoint3>();
            var objects = new Dictionary<int, DxfEntity>();

            foreach (DxfOsnapPointRef reference in references)
            {
                DxfEntity entity = document.FindEntity(reference.ObjectHandle);

                if (entity == null)
                {
                    continue;
                }

                kept.Add(reference);
                GeoPoint3? point = Locate(document, dimension, reference, entity);

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

            return Place(document, dimension, points, objects) ? "Moved" : untied ? "Untied" : null;
        }

        /// <summary>
        /// Where a reference's point of a dimension is now: where its two objects cross for an intersection, the lines run on as
        /// far as needed and the crossing nearest the dimension's point taken where there are two (AutoCAD 2021 moved a
        /// DIMLINEAR tied to the crossing of two lines with the crossing when one of them moved); a perpendicular or a tangent
        /// drawn again (<see cref="DrawnAt"/>); else as <see cref="Evaluate"/>.
        /// </summary>
        private static GeoPoint3? Locate(CadDocument document, DxfDimension dimension, DxfOsnapPointRef reference, DxfEntity entity)
        {
            if (reference.OsnapType == DxfOsnapType.Perpendicular || reference.OsnapType == DxfOsnapType.Tangent)
            {
                return DrawnAt(document, dimension, reference, entity);
            }

            if (reference.OsnapType != DxfOsnapType.Intersection)
            {
                return Evaluate(reference, entity);
            }

            DxfEntity other = reference.IntersectionObjectHandle == null ? null : document.FindEntity(reference.IntersectionObjectHandle);

            if (!Crossable(entity) || !Crossable(other))
            {
                return null;
            }

            GeoPoint3 near = PointOf(dimension, reference.PointIndex) ?? reference.Point;
            GeoPoint3? best = null;

            foreach (GeoPoint2 point in Crossings(entity, other))
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
        private static List<GeoPoint2> Crossings(DxfEntity a, DxfEntity b)
        {
            var found = new List<GeoPoint2>();

            if (a is DxfLine && !(b is DxfLine))
            {
                DxfEntity swap = a;
                a = b;
                b = swap;
            }

            if (b is DxfLine second)
            {
                GeoPoint2 p = new GeoPoint2(second.StartPoint.X, second.StartPoint.Y);
                GeoVector2 d = new GeoVector2(second.EndPoint.X - p.X, second.EndPoint.Y - p.Y);

                if (a is DxfLine first)
                {
                    GeoPoint2 q = new GeoPoint2(first.StartPoint.X, first.StartPoint.Y);
                    GeoVector2 e = new GeoVector2(first.EndPoint.X - q.X, first.EndPoint.Y - q.Y);
                    double cross = d.X * e.Y - d.Y * e.X;

                    if (Math.Abs(cross) > 1E-12 * d.Length * e.Length)
                    {
                        double t = ((q.X - p.X) * e.Y - (q.Y - p.Y) * e.X) / cross;
                        found.Add(p + d * t);
                    }

                    return found;
                }

                Round(a, out GeoPoint2 c, out double r);
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

            Round(a, out GeoPoint2 c1, out double r1);
            Round(b, out GeoPoint2 c2, out double r2);
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

        private static void Round(DxfEntity entity, out GeoPoint2 center, out double radius)
        {
            GeoPoint3 c = entity is DxfArc arc ? arc.Center : ((DxfCircle)entity).Center;
            center = new GeoPoint2(c.X, c.Y);
            radius = entity is DxfArc a ? a.Radius : ((DxfCircle)entity).Radius;
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
                            case DxfOsnapType.StartPoint: return line.StartPoint;
                            case DxfOsnapType.Endpoint: return line.EndPoint;
                            case DxfOsnapType.Midpoint: return Lerp(line.StartPoint, line.EndPoint, 0.5);
                            case DxfOsnapType.Nearest: return Lerp(line.StartPoint, line.EndPoint, reference.NearParameter);
                            default: return null;
                        }

                    case DxfArc arc:
                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.StartPoint: return arc.StartPoint;
                            case DxfOsnapType.Endpoint: return arc.EndPoint;
                            case DxfOsnapType.Midpoint: return arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0);
                            case DxfOsnapType.Center: return arc.Center;
                            case DxfOsnapType.Quadrant:
                            case DxfOsnapType.Nearest:
                                return OnRound(arc.Center, arc.Radius, reference.NearParameter);
                            default: return null;
                        }

                    case DxfCircle circle:
                        switch (reference.OsnapType)
                        {
                            case DxfOsnapType.Center: return circle.Center;
                            case DxfOsnapType.Quadrant:
                            case DxfOsnapType.Nearest:
                                return OnRound(circle.Center, circle.Radius, reference.NearParameter);
                            default: return null;
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
                            case DxfOsnapType.StartPoint: return polyline.GetPointAtParameter(segment);
                            case DxfOsnapType.Endpoint: return polyline.GetPointAtParameter(segment + 1.0);
                            case DxfOsnapType.Midpoint: return polyline.GetPointAtParameter(segment + 0.5);
                            default: return null;
                        }

                    case DxfDBPoint node when reference.OsnapType == DxfOsnapType.Node:
                        return node.Position;
                    default:
                        return null;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug("CadDimAssociation: the point " + reference + " cannot be found on its object: " + exception.Message);
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
                    return index == 0 ? rotated.XLine1Point : index == 1 ? rotated.XLine2Point : (GeoPoint3?)null;
                case DxfAlignedDimension aligned:
                    return index == 0 ? aligned.XLine1Point : index == 1 ? aligned.XLine2Point : (GeoPoint3?)null;
                case DxfRadialDimension radial:
                    return index == 1 ? radial.Center : (GeoPoint3?)null;
                case DxfDiametricDimension diametric:
                    return index == 1 ? Lerp(diametric.ChordPoint, diametric.FarChordPoint, 0.5) : (GeoPoint3?)null;
                case DxfPoint3AngularDimension angular:
                    return index == 0 ? angular.XLine1Point : index == 1 ? angular.XLine2Point : index == 2 ? angular.CenterPoint : (GeoPoint3?)null;
                case DxfLineAngularDimension2 two:
                    return index == 0 ? two.XLine1Start : index == 1 ? two.XLine1End : index == 2 ? two.XLine2Start : index == 3 ? two.XLine2End : (GeoPoint3?)null;
                case DxfOrdinateDimension ordinate:
                    return index == 1 ? ordinate.DefiningPoint : (GeoPoint3?)null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Moves a dimension's points to where its references say and draws it again: moved whole where every point it is made
        /// of moved by one step; else each tied point set, a linear one's dimension line keeping its distance from the origin it
        /// lay nearer to, an aligned one's its distance from the line measured, a radius drawn out along its direction. False
        /// where nothing moved.
        /// </summary>
        private static bool Place(CadDocument document, DxfDimension dimension, Dictionary<int, GeoPoint3> points, Dictionary<int, DxfEntity> objects)
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

            if ((dimension is DxfRadialDimension || dimension is DxfDiametricDimension) && objects.TryGetValue(1, out DxfEntity round))
            {
                radius = round is DxfCircle circle ? circle.Radius : round is DxfArc arc ? arc.Radius : (double?)null;
                oldRadius = dimension is DxfRadialDimension r ? Distance(r.ChordPoint, r.Center) : Distance(((DxfDiametricDimension)dimension).ChordPoint, ((DxfDiametricDimension)dimension).FarChordPoint) / 2.0;
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
                    double now = Math.Abs(second) < Math.Abs(first) - least ? Dot(p2, across) + second : Dot(p1, across) + first;
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
                    radial.Center = center;
                    radial.ChordPoint = center + outward * radius.Value;
                    break;
                }

                case DxfDiametricDimension diametric when radius.HasValue:
                {
                    GeoPoint3 center = points[1];
                    GeoVector3 outward = Unit(diametric.ChordPoint - diametric.FarChordPoint);
                    diametric.ChordPoint = center + outward * radius.Value;
                    diametric.FarChordPoint = center - outward * radius.Value;
                    break;
                }

                case DxfPoint3AngularDimension angular:
                    angular.XLine1Point = points.TryGetValue(0, out GeoPoint3 start) ? start : angular.XLine1Point;
                    angular.XLine2Point = points.TryGetValue(1, out GeoPoint3 end) ? end : angular.XLine2Point;
                    angular.CenterPoint = points.TryGetValue(2, out GeoPoint3 vertex) ? vertex : angular.CenterPoint;
                    break;
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
                    var axis = ordinate.UsingXAxis ? new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0.0) : new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0.0);
                    ordinate.DefiningPoint = points[1];
                    ordinate.LeaderEndPoint = ordinate.LeaderEndPoint + axis * Dot(step, axis);
                    break;
                }
                default:
                    return false;
            }

            if (!CadCommands.Redraw(dimension))
            {
                GeometryHelperLog.Debug("CadDimAssociation: the dimension " + dimension.Handle + " followed its objects but its picture could not be drawn again.");
            }

            return true;
        }

        /// <summary>Whether every point a dimension is made of moved by one step (and a radius kept its size), so that it moves whole.</summary>
        private static bool MovedWhole(DxfDimension dimension, Dictionary<int, GeoVector3> moves, double? radius, double? oldRadius, double least, out GeoVector3 shift)
        {
            int[] whole = dimension is DxfPoint3AngularDimension ? new[] { 0, 1, 2 } : dimension is DxfLineAngularDimension2 ? new[] { 0, 1, 2, 3 }
                : dimension is DxfRadialDimension || dimension is DxfDiametricDimension ? new[] { 1 } : new[] { 0, 1 };
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

        private static DxfOsnapPointRef Round(int pointIndex, CadSnapResult snap, DxfEntity curve, GeoPoint3 center, GeoPoint3 at)
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
                catch (InvalidOperationException)
                {
                }
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
            two.ArcPoint = new GeoPoint3(v1.X + radius * Math.Cos(from), v1.Y + radius * Math.Sin(from), two.ArcPoint.Z);
            return true;
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
        private static double Between(GeoVector2 a, GeoVector2 b) => Math.Abs(Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y));

        private static double AngleAbout(GeoPoint3 center, GeoPoint3 at)
        {
            double angle = Math.Atan2(at.Y - center.Y, at.X - center.X);
            return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
        }

        private static double Dot(GeoVector3 a, GeoVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        private static GeoPoint3 OnRound(GeoPoint3 center, double radius, double angle) => new GeoPoint3(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), center.Z);

        private static double Along(DxfLine line, GeoPoint2 point)
        {
            GeoVector3 way = line.EndPoint - line.StartPoint;
            double squared = way.X * way.X + way.Y * way.Y;
            return squared <= 0.0 ? 0.0 : ((point.X - line.StartPoint.X) * way.X + (point.Y - line.StartPoint.Y) * way.Y) / squared;
        }

        private static GeoPoint3 Lerp(GeoPoint3 a, GeoPoint3 b, double t) => new GeoPoint3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static double Distance(GeoPoint2 a, GeoPoint3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static double Distance(GeoPoint3 a, GeoPoint3 b) => (a - b).Length;

        private static double Dot(GeoPoint3 point, GeoVector3 way) => point.X * way.X + point.Y * way.Y + point.Z * way.Z;

        private static GeoVector3 Unit(GeoVector3 vector) => vector.Length > 0.0 ? vector / vector.Length : new GeoVector3(1.0, 0.0, 0.0);
    }
}
