using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A 3D POLYLINE of a DXF file (flag 8 of its group 70), as AutoCAD's <c>Polyline3d</c>: the POLYLINE record with its
    /// <see cref="DxfPolylineVertex3d"/> records and its SEQEND, straight segments from vertex to vertex in the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The polyline is walked as AutoCAD walks one, <c>foreach (DxfPolylineVertex3d vertex in polyline)</c>. The polyline
    /// (0, 0, 0), (3, 4, 0), (3, 4, 12) is <see cref="Length"/> 18; its parameter is AutoCAD's, the index of the vertex a segment
    /// leaves plus how far along it: 1.5 is (3, 4, 6).
    /// </para>
    /// <para>
    /// <c>new DxfPolyline3d(DxfPoly3dType.SimplePoly, points, false)</c> makes one in code, to be added to a drawing of any
    /// version. A vertex set, added or taken out is that one record written, put in or taken out, every other vertex keeping
    /// its handle.
    /// </para>
    /// </remarks>
    public sealed class DxfPolyline3d : DxfCurve, IEnumerable<DxfPolylineVertex3d>
    {
        // The groups of AcDb3dPolyline in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 66, 10, 20, 30, 70, 75 });

        // The groups of a vertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> VertexOrder = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        /// <summary>Initializes a 3D polyline of no vertices, as AutoCAD's <c>new Polyline3d()</c>, on layer 0 in no file.</summary>
        public DxfPolyline3d()
            : base("POLYLINE", Head(false))
        {
            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });
        }

        /// <summary>
        /// Initializes a 3D polyline of vertices, as AutoCAD's <c>new Polyline3d(type, vertices, closed)</c>, on layer 0 in no
        /// file until it is added to one.
        /// </summary>
        /// <param name="type">How it is drawn: <see cref="DxfPoly3dType.SimplePoly"/>, the one made in code.</param>
        /// <param name="vertices">The vertices, in the world.</param>
        /// <param name="closed">Whether a segment runs from the last vertex back to the first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="NotSupportedException">Thrown for a type other than <see cref="DxfPoly3dType.SimplePoly"/>, whose fitted vertices the library does not make yet.</exception>
        public DxfPolyline3d(DxfPoly3dType type, IEnumerable<GeoPoint3> vertices, bool closed)
            : base("POLYLINE", Head(closed))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPoly3dType.SimplePoly)
            {
                throw new NotSupportedException("A polyline fitted with a spline is not made in code yet: make it of SimplePoly.");
            }

            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });
            int record = 1;

            foreach (GeoPoint3 point in vertices)
            {
                InsertFollowerAt(record++, new DxfPolylineVertex3d(point));
            }
        }

        private DxfPolyline3d(DxfItem item)
            : base(item)
        {
        }

        #region The polyline

        /// <summary>
        /// Gets or sets whether a segment runs from the last vertex back to the first (flag 1 of group 70), as AutoCAD's
        /// <c>Closed</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public new bool Closed
        {
            get => (GetInt(70, 0) & 1) != 0;

            set
            {
                int flags = GetInt(70, 0);
                SetInt(70, value ? flags | 1 : flags & ~1, 0);
            }
        }

        /// <summary>
        /// Gets or sets how the polyline is drawn through its vertices, as AutoCAD's <c>PolyType</c>: as a spline (flag 4 of group
        /// 70, quadratic where group 75 is 5, cubic otherwise), or straight.
        /// </summary>
        /// <value>Set, <see cref="DxfPoly3dType.SimplePoly"/>, which straightens it as <see cref="Straighten"/> does.</value>
        /// <exception cref="NotSupportedException">Thrown when set to a fitted type, whose vertices the library does not make yet.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public DxfPoly3dType PolyType
        {
            get => (GetInt(70, 0) & 4) == 0 ? DxfPoly3dType.SimplePoly : GetInt(75, 0) == 5 ? DxfPoly3dType.QuadSplinePoly : DxfPoly3dType.CubicSplinePoly;

            set
            {
                if (value == PolyType)
                {
                    return;
                }

                if (value != DxfPoly3dType.SimplePoly)
                {
                    throw new NotSupportedException("A polyline is not fitted with a spline by the library yet.");
                }

                Straighten();
            }
        }

        /// <summary>Gets the length of the polyline, the sum of those of its segments, as AutoCAD's <c>Length</c>.</summary>
        public double Length => Chain().Length;

        #endregion

        #region The vertices

        /// <summary>
        /// Gets an enumerator over the vertices in their order, as AutoCAD's <c>GetEnumerator</c> walks them: each the same
        /// object while its record is there.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfPolylineVertex3d> GetEnumerator()
        {
            var vertices = new List<DxfPolylineVertex3d>();

            for (int record = 1; record < RecordCount; record++)
            {
                if (RecordType(record) == "VERTEX" && FollowerAt(record) is DxfPolylineVertex3d vertex)
                {
                    vertices.Add(vertex);
                }
            }

            return vertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Appends a vertex made in code after the last, as AutoCAD's <c>AppendVertex</c>: the vertex is that of the polyline from then on.</summary>
        /// <param name="vertexToAppend">The vertex, in no polyline.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertex is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is of a polyline already, or the polyline is erased.</exception>
        public void AppendVertex(DxfPolylineVertex3d vertexToAppend)
        {
            if (vertexToAppend == null)
            {
                throw new ArgumentNullException(nameof(vertexToAppend));
            }

            InsertFollowerAt(RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount, vertexToAppend);
        }

        /// <summary>
        /// Puts a vertex made in code after a vertex of the polyline, as AutoCAD's <c>InsertVertexAt</c>; before the first where
        /// the vertex given is null.
        /// </summary>
        /// <param name="indexVertex">The vertex of this polyline the new one follows; null to put it first.</param>
        /// <param name="newVertex">The vertex, in no polyline.</param>
        /// <exception cref="ArgumentNullException">Thrown when the new vertex is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the vertex it is to follow is not of this polyline.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the new vertex is of a polyline already, or the polyline is erased.</exception>
        public void InsertVertexAt(DxfPolylineVertex3d indexVertex, DxfPolylineVertex3d newVertex)
        {
            if (newVertex == null)
            {
                throw new ArgumentNullException(nameof(newVertex));
            }

            int at = 1;

            if (indexVertex != null)
            {
                if (indexVertex.Polyline != this || indexVertex.IsErased)
                {
                    throw new ArgumentException("The vertex is not one of this polyline.", nameof(indexVertex));
                }

                at = indexVertex.Record + 1;
            }

            InsertFollowerAt(at, newVertex);
        }

        /// <summary>
        /// Straightens a polyline fitted with a spline, as AutoCAD's <c>Straighten</c>: the vertices the fitting made (flag 8)
        /// are taken out, those of the frame (16) are vertices of the polyline again, and flag 4 of group 70 and its group 75 go.
        /// A polyline that is not fitted is left as it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void Straighten()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            var frame = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            var made = new List<int>();
            bool fitted = (GetInt(70, 0) & 4) != 0;

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                int flags = DxfFollowers.Whole(vertex.Value, 70, 0);
                fitted |= (flags & 24) != 0;

                if ((flags & 8) != 0)
                {
                    made.Add(vertex.Key);
                }
                else if ((flags & 16) != 0)
                {
                    frame.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, DxfFollowers.WholeChange(vertex.Value, 70, flags & ~16, 0)));
                }
            }

            if (!fitted)
            {
                return;
            }

            SetGroupsAtMany(frame, VertexOrder);

            for (int i = made.Count - 1; i >= 0; i--)
            {
                FollowerAt(made[i]).Erase();
            }

            SetGroups(new KeyValuePair<int, string>(70, (GetInt(70, 0) & ~4).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
        }

        #endregion

        #region The curve

        /// <summary>Gets the parameter at the end of the polyline: the count of its segments, as AutoCAD's.</summary>
        public override double EndParam => Chain().EndParam;

        /// <summary>Gets the point at a parameter, the index of a vertex plus how far along the segment it leaves, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.PointAt(value);
        }

        /// <summary>Gets the parameter at a point of the polyline, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the polyline.</param>
        /// <returns>The parameter: the first segment's where two meet at the point.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the polyline.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point) => ParameterOn(Chain(), point);

        /// <summary>Gets the distance along the polyline from its start to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.DistanceAt(value);
        }

        /// <summary>Gets the parameter at a distance along the polyline from its start, as AutoCAD's <c>GetParameterAtDistance</c>.</summary>
        /// <param name="dist">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            DxfBulgeChain chain = Chain();
            Within(dist, chain.Length, nameof(dist));
            return chain.ParameterAt(dist);
        }

        /// <summary>Gets the distance along the polyline from its start to a point of it, as AutoCAD's <c>GetDistAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the polyline.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the polyline.</exception>
        public override double GetDistAtPoint(GeoPoint3 point)
        {
            DxfBulgeChain chain = Chain();
            return chain.DistanceAt(ParameterOn(chain, point));
        }

        /// <summary>Gets the point at a distance along the polyline from its start, as AutoCAD's <c>GetPointAtDist</c>.</summary>
        /// <param name="value">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.Length, nameof(value));
            return chain.PointAt(chain.ParameterAt(value));
        }

        /// <summary>Gets the point of the polyline nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: the first segment's where two are as near.</summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Not read: the polyline is taken as it is.</param>
        /// <returns>The nearest point.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has no vertex.</exception>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            Chain().ClosestParameter(givenPoint, out GeoPoint3 closest);
            return closest;
        }

        /// <summary>Gets the derivative of the point by the parameter, as AutoCAD's <c>GetFirstDerivative</c>: the vector from the start of the segment to its end.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The derivative.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.FirstDerivative(value);
        }

        /// <summary>Gets the second derivative of the point by the parameter, as AutoCAD's <c>GetSecondDerivative</c>: nought, the segments being straight.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The vector of no length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.SecondDerivative(value);
        }

        /// <summary>
        /// Turns the polyline to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: the vertices in the other order,
        /// a closed polyline keeping its first vertex. Each vertex record takes the point and flags of its counterpart in one
        /// writing of the polyline, every handle kept.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public override void ReverseCurve()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            int n = vertices.Count;

            if (n < 2)
            {
                return;
            }

            bool closed = Closed;
            var records = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>(n);

            for (int j = 0; j < n; j++)
            {
                IReadOnlyList<DxfTypedValue> own = vertices[j].Value;
                IReadOnlyList<DxfTypedValue> source = vertices[closed ? (n - j) % n : n - 1 - j].Value;
                records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertices[j].Key, Join(
                    DxfFollowers.PointChanges(own, 10, DxfFollowers.Point(source, 10, Origin), Origin, 3),
                    DxfFollowers.WholeChange(own, 70, DxfFollowers.Whole(source, 70, 0), 0))));
            }

            SetGroupsAtMany(records, VertexOrder);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the polyline, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each vertex moved
        /// in the world, in one writing of the polyline, each record in place, its handle kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            SetGroupsAtMany(DxfFollowers.Moved(Item, transform, _ => true), VertexOrder);
        }

        #endregion

        [DxfType("POLYLINE")]
        internal static DxfEntity Create(DxfItem item)
        {
            int flags = DxfPolyline2d.Flags(item);
            return (flags & 8) != 0 && (flags & (16 | 64)) == 0 ? new DxfPolyline3d(item) : null;
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        private static IEnumerable<DxfTypedValue> Head(bool closed) => new[]
        {
            new DxfTypedValue(100, "AcDb3dPolyline"),
            new DxfTypedValue(66, "1"),
            new DxfTypedValue(10, "0"),
            new DxfTypedValue(20, "0"),
            new DxfTypedValue(30, "0"),
            new DxfTypedValue(70, closed ? "9" : "8"),
        };

        /// <summary>The chain of the vertices drawn, those of a fitted polyline's frame left out as the reader leaves them.</summary>
        private DxfBulgeChain Chain()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            bool fitted = vertices.Exists(v => (DxfFollowers.Whole(v.Value, 70, 0) & 8) != 0);
            var points = new List<GeoPoint3>(vertices.Count);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                if (!(fitted && (DxfFollowers.Whole(vertex.Value, 70, 0) & 16) != 0))
                {
                    points.Add(DxfFollowers.Point(vertex.Value, 10, Origin));
                }
            }

            return new DxfBulgeChain(Up, new GeoVector3(1.0, 0.0, 0.0), new GeoVector3(0.0, 1.0, 0.0), points, null, Closed);
        }

        private static double ParameterOn(DxfBulgeChain chain, GeoPoint3 point)
        {
            double parameter = chain.ClosestParameter(point, out GeoPoint3 closest);

            if (!closest.IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the polyline.", nameof(point));
            }

            return parameter;
        }
    }
}
