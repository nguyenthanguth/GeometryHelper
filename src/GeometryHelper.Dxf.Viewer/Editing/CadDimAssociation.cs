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
    /// segments of light polylines, and POINTs. A point tied by another snap (an intersection) or to another kind of object
    /// stays where it is.
    /// </para>
    /// </remarks>
    internal static class CadDimAssociation
    {
        /// <summary>
        /// The reference an object snap gives for a point of a dimension: its object and where on it the point lies; null for a
        /// snap of no object, of a kind not tied, or of an object not followed.
        /// </summary>
        internal static DxfOsnapPointRef FromSnap(CadDocument document, int pointIndex, CadSnapResult snap)
        {
            if (snap == null || snap.Handle == null)
            {
                return null;
            }

            DxfEntity entity = document.FindEntity(snap.Handle);
            GeoPoint3 at = new GeoPoint3(snap.Point.X, snap.Point.Y, 0.0);
            double least = Tolerance.Default.EqualPoint;

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

        /// <summary>Ties a dimension to references given anew, as DIMREASSOCIATE does, and moves its points to where they say.</summary>
        internal static void Reassociate(CadDocument document, DxfDimension dimension, IReadOnlyList<DxfOsnapPointRef> references)
        {
            dimension.SetDimAssoc(references);
            var points = new Dictionary<int, GeoPoint3>();
            var objects = new Dictionary<int, DxfEntity>();

            foreach (DxfOsnapPointRef reference in references)
            {
                DxfEntity entity = document.FindEntity(reference.ObjectHandle);
                GeoPoint3? point = entity == null ? null : Evaluate(reference, entity);

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
                GeoPoint3? point = Evaluate(reference, entity);

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
            int[] whole = dimension is DxfPoint3AngularDimension ? new[] { 0, 1, 2 } : dimension is DxfRadialDimension || dimension is DxfDiametricDimension ? new[] { 1 } : new[] { 0, 1 };
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
