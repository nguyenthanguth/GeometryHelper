using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>The object snap a point of an associative dimension was taken by (group 72 of its point reference), as AutoCAD numbers them.</summary>
    public enum DxfOsnapType
    {
        /// <summary>No snap.</summary>
        None = 0,

        /// <summary>The end of a curve (ENDpoint), the far end: a line's end point, an arc's end.</summary>
        Endpoint = 1,

        /// <summary>The middle of a curve (MIDpoint).</summary>
        Midpoint = 2,

        /// <summary>The center of an arc or a circle (CENter).</summary>
        Center = 3,

        /// <summary>A POINT object (NODe).</summary>
        Node = 4,

        /// <summary>A quadrant of an arc or a circle (QUAdrant), its angle the reference's parameter.</summary>
        Quadrant = 5,

        /// <summary>Where two objects cross (INTersection).</summary>
        Intersection = 6,

        /// <summary>The insertion point of a block or a text (INSertion).</summary>
        Insertion = 7,

        /// <summary>The foot of a perpendicular (PERpendicular).</summary>
        Perpendicular = 8,

        /// <summary>A point of tangency (TANgent).</summary>
        Tangent = 9,

        /// <summary>A point on a curve (NEArest), at the reference's parameter.</summary>
        Nearest = 10,

        /// <summary>Where two objects would cross if both ran on (APParent intersection).</summary>
        ApparentIntersection = 11,

        /// <summary>A point on a line parallel to another (PARallel).</summary>
        Parallel = 12,

        /// <summary>The start of a curve: what AutoCAD 2021 writes for an ENDpoint snap at a line's start point or an arc's start.</summary>
        StartPoint = 13,
    }

    /// <summary>
    /// One point of an associative dimension tied to an object (AutoCAD's <c>AcDbOsnapPointRef</c>): which point of the
    /// dimension it is, the object snap it was taken by, the object, the parameter of the point on it and the point itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which point of the dimension (<see cref="PointIndex"/>, the bit <c>1 &lt;&lt; PointIndex</c> of the DIMASSOC's group 90)
    /// is that of AutoCAD's <c>AcDbDimAssoc</c>: for a linear or an aligned dimension 0 is the first extension line's origin
    /// and 1 the second's; for a radius or a diameter 1 is the center of the arc or the circle; for an angle of three points 0
    /// and 1 are the ends of the two rays and 2 their vertex.
    /// </para>
    /// <para>
    /// AutoCAD 2021 writes a DIMLINEAR snapped to both ends of a line from (0, 0) to (100, 0) as two references to the line:
    /// <see cref="DxfOsnapType.StartPoint"/> of parameter 0 at point 0 and <see cref="DxfOsnapType.Endpoint"/> of parameter 1
    /// at point 1; the parameter of a line runs from 0 at its start to 1 at its end, that of an arc and a circle is the angle
    /// in radians.
    /// </para>
    /// </remarks>
    public sealed class DxfOsnapPointRef
    {
        /// <summary>Makes a point reference to an entity.</summary>
        /// <param name="pointIndex">Which point of the dimension it is, 0 to 3 (see the remarks).</param>
        /// <param name="osnapType">The object snap it was taken by.</param>
        /// <param name="objectHandle">The handle of the entity snapped to.</param>
        /// <param name="gsMarker">The segment of a polyline snapped to, 1 for its first; 0 for a line, an arc or a circle.</param>
        /// <param name="nearParameter">The parameter of the point on the entity: 0 to 1 along a line or a segment of a polyline,
        /// the angle on an arc or a circle.</param>
        /// <param name="point">The point snapped, in world coordinates.</param>
        /// <param name="intersectionObjectHandle">The handle of the second entity of an intersection; null for any other snap.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0 to 3.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        public DxfOsnapPointRef(int pointIndex, DxfOsnapType osnapType, string objectHandle, int gsMarker, double nearParameter, GeoPoint3 point, string intersectionObjectHandle)
            : this(pointIndex, osnapType, objectHandle ?? throw new ArgumentNullException(nameof(objectHandle)), 2, gsMarker, nearParameter, point, intersectionObjectHandle, 2, 0, null)
        {
        }

        private DxfOsnapPointRef(int pointIndex, DxfOsnapType osnapType, string objectHandle, int subentType, int gsMarker, double nearParameter, GeoPoint3 point, string intersectionObjectHandle, int intersectionSubentType, int intersectionGsMarker, DxfOsnapPointRef lastPointReference)
        {
            if (pointIndex < 0 || pointIndex > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "A dimension has four points to tie, 0 to 3.");
            }

            PointIndex = pointIndex;
            OsnapType = osnapType;
            ObjectHandle = objectHandle;
            SubentType = subentType;
            GsMarker = gsMarker;
            NearParameter = nearParameter;
            Point = point;
            IntersectionObjectHandle = intersectionObjectHandle;
            IntersectionSubentType = intersectionSubentType;
            IntersectionGsMarker = intersectionGsMarker;
            LastPointReference = lastPointReference;
        }

        /// <summary>Gets which point of the dimension the reference ties, 0 to 3.</summary>
        public int PointIndex { get; }

        /// <summary>Gets the object snap the point was taken by (group 72).</summary>
        public DxfOsnapType OsnapType { get; }

        /// <summary>
        /// Gets the handle of the entity snapped to (group 331); null for a <see cref="LastPointReference"/> of a point tied to
        /// nothing, which AutoCAD writes with no 331.
        /// </summary>
        public string ObjectHandle { get; }

        /// <summary>Gets the kind of the part of the entity snapped to (group 73): 2, an edge, for a line, an arc or a circle.</summary>
        public int SubentType { get; }

        /// <summary>
        /// Gets the marker of the part of the entity snapped to (group 91): 0 for a line, an arc or a circle; the segment of a
        /// polyline, 1 for its first, as AutoCAD 2021 writes the end of the first segment of a rectangle with marker 1 and parameter 1.
        /// </summary>
        public int GsMarker { get; }

        /// <summary>Gets the parameter of the point on the entity (group 40).</summary>
        public double NearParameter { get; }

        /// <summary>Gets the point snapped, in world coordinates (group 10).</summary>
        public GeoPoint3 Point { get; }

        /// <summary>Gets the handle of the second entity of an intersection (group 332); null where there is none.</summary>
        public string IntersectionObjectHandle { get; }

        internal int IntersectionSubentType { get; }

        internal int IntersectionGsMarker { get; }

        /// <summary>
        /// Gets the reference of the point a perpendicular or a tangent was drawn from (group 75 written 1, then that reference),
        /// as AutoCAD 2021 writes a DIMALIGNED from the start of a line to the foot of its perpendicular on another: the
        /// start of the first line; its <see cref="ObjectHandle"/> null and its snap <see cref="DxfOsnapType.None"/> where that
        /// point was tied to nothing. Its <see cref="PointIndex"/> is not written. Null for a point of any other snap.
        /// </summary>
        public DxfOsnapPointRef LastPointReference { get; }

        /// <summary>
        /// Gives a copy of the reference that names the reference of the point its perpendicular or tangent was drawn from, as
        /// AutoCAD 2021 writes a PERpendicular or a TANgent snap: the second point of a DIMALIGNED snapped perpendicular to a
        /// line from the start of another is <c>perpendicular.WithLastPoint(start)</c>.
        /// </summary>
        /// <param name="lastPoint">The reference of the point drawn from; its own last point is left out.</param>
        /// <returns>The copy.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the reference is null.</exception>
        public DxfOsnapPointRef WithLastPoint(DxfOsnapPointRef lastPoint)
        {
            if (lastPoint == null)
            {
                throw new ArgumentNullException(nameof(lastPoint));
            }

            var last = new DxfOsnapPointRef(lastPoint.PointIndex, lastPoint.OsnapType, lastPoint.ObjectHandle, lastPoint.SubentType, lastPoint.GsMarker, lastPoint.NearParameter, lastPoint.Point, lastPoint.IntersectionObjectHandle, lastPoint.IntersectionSubentType, lastPoint.IntersectionGsMarker, null);
            return new DxfOsnapPointRef(PointIndex, OsnapType, ObjectHandle, SubentType, GsMarker, NearParameter, Point, IntersectionObjectHandle, IntersectionSubentType, IntersectionGsMarker, last);
        }

        /// <summary>
        /// Gives a copy of the reference whose perpendicular or tangent was drawn from a point tied to nothing, as AutoCAD 2021
        /// writes one: a last point reference of no snap and no object, at the point.
        /// </summary>
        /// <param name="point">The point drawn from, in world coordinates.</param>
        /// <returns>The copy.</returns>
        public DxfOsnapPointRef WithLastPoint(GeoPoint3 point)
            => new DxfOsnapPointRef(PointIndex, OsnapType, ObjectHandle, SubentType, GsMarker, NearParameter, Point, IntersectionObjectHandle, IntersectionSubentType, IntersectionGsMarker,
                new DxfOsnapPointRef(PointIndex, DxfOsnapType.None, null, 0, 0, 0.0, point, null, 0, 0, null));

        /// <inheritdoc/>
        public override string ToString() => PointIndex.ToString(CultureInfo.InvariantCulture) + " " + OsnapType + " " + ObjectHandle;

        /// <summary>Reads the references of a DIMASSOC from its groups after its 71.</summary>
        internal static List<DxfOsnapPointRef> Read(IReadOnlyList<DxfTypedValue> groups, int from, int flags)
        {
            var found = new List<DxfOsnapPointRef>();
            int next = 0;

            for (int i = from; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 1)
                {
                    continue;
                }

                // The references follow the bits of group 90 from the lowest, as AutoCAD writes them.
                while (next < 4 && (flags & (1 << next)) == 0)
                {
                    next++;
                }

                DxfOsnapPointRef reference = ReadOne(groups, ref i, Math.Min(next, 3));

                if (next < 4 && reference.ObjectHandle != null)
                {
                    found.Add(reference);
                }

                next++;
            }

            return found;
        }

        /// <summary>
        /// Reads one reference from its group 1 at <paramref name="i"/>, and the reference of its last point after it where its
        /// 75 is 1, as AutoCAD writes a perpendicular or a tangent; <paramref name="i"/> left at the last group read.
        /// </summary>
        private static DxfOsnapPointRef ReadOne(IReadOnlyList<DxfTypedValue> groups, ref int i, int index)
        {
            int type = 0;
            string handle = null;
            int subent = 0;
            int marker = 0;
            double parameter = 0.0;
            double x = 0.0;
            double y = 0.0;
            double z = 0.0;
            string other = null;
            int otherSubent = 0;
            int otherMarker = 0;
            bool hasLast = false;
            int j = i + 1;

            for (; j < groups.Count && groups[j].TypeCode != 1; j++)
            {
                string value = (groups[j].Value ?? string.Empty).Trim();

                switch (groups[j].TypeCode)
                {
                    case 72: type = Whole(value); break;
                    case 331: handle = value; break;
                    case 73: subent = Whole(value); break;
                    case 91: marker = Whole(value); break;
                    case 40: parameter = Real(value); break;
                    case 10: x = Real(value); break;
                    case 20: y = Real(value); break;
                    case 30: z = Real(value); break;
                    case 332: other = value; break;
                    case 74: otherSubent = Whole(value); break;
                    case 92: otherMarker = Whole(value); break;
                    case 75: hasLast = Whole(value) == 1; break;
                }
            }

            i = j - 1;
            DxfOsnapPointRef last = null;

            if (hasLast && j < groups.Count)
            {
                i = j;
                last = ReadOne(groups, ref i, index);
            }

            return new DxfOsnapPointRef(index, (DxfOsnapType)type, handle, subent, marker, parameter, new GeoPoint3(x, y, z), other, otherSubent, otherMarker, last);
        }

        /// <summary>
        /// Writes the reference as AutoCAD 2021 writes one: 1, 72, 331, 73, 91, 40, 10, the second entity of an intersection,
        /// 75, then its last point's reference where it has one; a last point tied to nothing with no 331, 73 and 91.
        /// </summary>
        internal void Write(List<KeyValuePair<int, string>> groups)
        {
            groups.Add(Pair(1, "AcDbOsnapPointRef"));
            groups.Add(Pair(72, ((int)OsnapType).ToString(CultureInfo.InvariantCulture)));

            if (ObjectHandle != null)
            {
                groups.Add(Pair(331, ObjectHandle));
                groups.Add(Pair(73, SubentType.ToString(CultureInfo.InvariantCulture)));
                groups.Add(Pair(91, GsMarker.ToString(CultureInfo.InvariantCulture)));
            }

            groups.Add(Pair(40, Numbers.Exact(NearParameter)));
            groups.Add(Pair(10, Numbers.Exact(Point.X)));
            groups.Add(Pair(20, Numbers.Exact(Point.Y)));
            groups.Add(Pair(30, Numbers.Exact(Point.Z)));

            if (IntersectionObjectHandle != null)
            {
                groups.Add(Pair(332, IntersectionObjectHandle));
                groups.Add(Pair(74, IntersectionSubentType.ToString(CultureInfo.InvariantCulture)));
                groups.Add(Pair(92, IntersectionGsMarker.ToString(CultureInfo.InvariantCulture)));
            }

            groups.Add(Pair(75, LastPointReference == null ? "0" : "1"));
            LastPointReference?.Write(groups);
        }

        private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

        private static int Whole(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;

        private static double Real(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : 0.0;
    }

    /// <summary>
    /// The DIMASSOC of an associative dimension, as AutoCAD's <c>AcDbDimAssoc</c>: kept under ACAD_DIMASSOC in the extension
    /// dictionary of the dimension, it names the dimension (group 330) and ties its points to the objects they were snapped
    /// to (<see cref="PointReferences"/>), each of which lists it among its reactors, so that the dimension follows them.
    /// </summary>
    /// <remarks>
    /// <see cref="DxfDimension.DimAssoc"/> gives the DIMASSOC of a dimension, <see cref="DxfDimension.SetDimAssoc"/> ties a
    /// dimension and <see cref="DxfDimension.ReleaseDimAssoc"/> unties it. The library writes the ties and reads them back; the
    /// dimension's points are moved by whoever moves the objects, as the presenter does after each edit.
    /// </remarks>
    public sealed class DxfDimAssoc : DxfDBObject
    {
        /// <summary>The name of the DIMASSOC in the extension dictionary of its dimension, as AutoCAD names it.</summary>
        internal const string Key = "ACAD_DIMASSOC";

        internal DxfDimAssoc(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets the handle of the dimension the object ties (the group 330 after its subclass marker).</summary>
        public string DimensionHandle
        {
            get
            {
                IReadOnlyList<DxfTypedValue> groups = Groups(out int at);
                return at >= 0 && at + 1 < groups.Count && groups[at + 1].TypeCode == 330 ? (groups[at + 1].Value ?? string.Empty).Trim() : null;
            }
        }

        /// <summary>Gets which points of the dimension are tied (group 90): bit 1 the first, 2 the second, 4 the third, 8 the fourth.</summary>
        public int AssociativityFlags => Whole(90);

        /// <summary>Gets whether the dimension is in paper space and its objects in model space, seen through a viewport (group 70).</summary>
        public bool IsTransSpace => Whole(70) != 0;

        /// <summary>Gets the point references, in the order of their points, each once; a new list on each call.</summary>
        public IReadOnlyList<DxfOsnapPointRef> PointReferences
        {
            get
            {
                IReadOnlyList<DxfTypedValue> groups = Groups(out int at);
                return at < 0 ? (IReadOnlyList<DxfOsnapPointRef>)Array.Empty<DxfOsnapPointRef>() : DxfOsnapPointRef.Read(groups, at + 1, AssociativityFlags).AsReadOnly();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => "DIMASSOC " + (Handle ?? string.Empty);

        /// <summary>The groups AutoCAD 2021 writes after the subclass marker: the dimension, the flags 90, 70 and 71, then each reference.</summary>
        internal static List<KeyValuePair<int, string>> Body(string dimension, IReadOnlyList<DxfOsnapPointRef> references)
        {
            int flags = 0;

            foreach (DxfOsnapPointRef reference in references)
            {
                flags |= 1 << reference.PointIndex;
            }

            var groups = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(100, "AcDbDimAssoc"),
                new KeyValuePair<int, string>(330, dimension),
                new KeyValuePair<int, string>(90, flags.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(70, "0"),
                new KeyValuePair<int, string>(71, "0"),
            };

            foreach (DxfOsnapPointRef reference in references)
            {
                reference.Write(groups);
            }

            return groups;
        }

        /// <summary>Writes the groups after the subclass marker anew, the handle, reactors and owner kept.</summary>
        internal void Rewrite(List<KeyValuePair<int, string>> body)
        {
            DxfFile file = InFile("be changed");
            List<DxfTypedValue> tail = file.TailOf(Item, 0);
            int from = tail.FindIndex(g => g.TypeCode == 100 && string.Equals((g.Value ?? string.Empty).Trim(), "AcDbDimAssoc", StringComparison.Ordinal));
            file.ReplaceTail(Item, from < 0 ? tail.Count : from, body);
        }

        private IReadOnlyList<DxfTypedValue> Groups(out int at)
        {
            at = -1;

            if (Item == null || IsErased)
            {
                return Array.Empty<DxfTypedValue>();
            }

            IReadOnlyList<DxfTypedValue> groups = Item.File.ObjectGroupsOf(Item);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 100 && string.Equals((groups[i].Value ?? string.Empty).Trim(), "AcDbDimAssoc", StringComparison.Ordinal))
                {
                    at = i;
                    break;
                }
            }

            return groups;
        }

        private int Whole(int code)
        {
            IReadOnlyList<DxfTypedValue> groups = Groups(out int at);

            for (int i = Math.Max(0, at); i < groups.Count && groups[i].TypeCode != 1; i++)
            {
                if (groups[i].TypeCode == code && int.TryParse((groups[i].Value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    return value;
                }
            }

            return 0;
        }
    }
}
