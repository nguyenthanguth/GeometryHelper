using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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
        /// <param name="type">How it is drawn: <see cref="DxfPoly3dType.SimplePoly"/>, the one made in code; fit it afterwards with <see cref="ConvertToPolyType"/>.</param>
        /// <param name="vertices">The vertices, in the world.</param>
        /// <param name="closed">Whether a segment runs from the last vertex back to the first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="NotSupportedException">Thrown for a type other than <see cref="DxfPoly3dType.SimplePoly"/>: make the polyline straight and fit it with <see cref="ConvertToPolyType"/>.</exception>
        public DxfPolyline3d(DxfPoly3dType type, IEnumerable<GeoPoint3> vertices, bool closed)
            : base("POLYLINE", Head(closed))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPoly3dType.SimplePoly)
            {
                throw new NotSupportedException("A polyline is made in code straight: make it of SimplePoly and fit it with ConvertToPolyType.");
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
        /// <value>Set, the polyline converted to the type as <see cref="ConvertToPolyType"/> converts it; its own type changes nothing.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfPoly3dType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased, or has too few vertices to fit.</exception>
        public DxfPoly3dType PolyType
        {
            get => (GetInt(70, 0) & 4) == 0 ? DxfPoly3dType.SimplePoly : GetInt(75, 0) == 5 ? DxfPoly3dType.QuadSplinePoly : DxfPoly3dType.CubicSplinePoly;

            set
            {
                if (value != PolyType)
                {
                    ConvertToPolyType(value);
                }
            }
        }

        /// <summary>
        /// Converts the polyline to a type, as AutoCAD's <c>ConvertToPolyType</c>: straightened for
        /// <see cref="DxfPoly3dType.SimplePoly"/>, fitted with a quadratic or cubic B-spline of the header's SPLINESEGS segments
        /// to a span (8 where the file gives none) as <see cref="SplineFit(DxfPoly3dType, int)"/> fits it.
        /// </summary>
        /// <param name="newVal">The type.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfPoly3dType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased, or has too few vertices to fit.</exception>
        public void ConvertToPolyType(DxfPoly3dType newVal)
        {
            switch (newVal)
            {
                case DxfPoly3dType.SimplePoly:
                    Straighten();
                    break;

                case DxfPoly3dType.QuadSplinePoly:
                case DxfPoly3dType.CubicSplinePoly:
                    SplineFit(newVal, DxfPolylineFit.Header(Item, "$SPLINESEGS", 8));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newVal), newVal, "A 3D polyline is simple, or fitted with a quadratic or cubic spline.");
            }
        }

        /// <summary>
        /// Fits the polyline with a spline, as AutoCAD's <c>SplineFit</c>: of the header's SPLINETYPE (5 quadratic, 6 cubic, cubic
        /// where the file gives none) and SPLINESEGS (8 where it gives none), as <see cref="SplineFit(DxfPoly3dType, int)"/> fits it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices, or three for a closed one, or is erased.</exception>
        public void SplineFit()
        {
            int type = DxfPolylineFit.Header(Item, "$SPLINETYPE", 6);
            SplineFit(type == 5 ? DxfPoly3dType.QuadSplinePoly : DxfPoly3dType.CubicSplinePoly, DxfPolylineFit.Header(Item, "$SPLINESEGS", 8));
        }

        /// <summary>
        /// Fits the polyline with a spline, as AutoCAD's <c>SplineFit</c>: its vertices made the frame of a uniform B-spline in
        /// space (flag 16 of their group 70, each keeping its handle), clamped to the first and last vertex of an open polyline
        /// and running round a closed one, and the vertices of the curve added after them (flag 8), as many segments to each span
        /// of the spline as asked (their count is taken where it is below nought, a 3D polyline having no arcs); flag 4 of the
        /// polyline's group 70 and group 75, 5 or 6. A fitted polyline is straightened first.
        /// </summary>
        /// <param name="value">The spline, <see cref="DxfPoly3dType.QuadSplinePoly"/> or <see cref="DxfPoly3dType.CubicSplinePoly"/>.</param>
        /// <param name="segments">The segments to a span, not nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not a spline, or the segments are nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices, or three for a closed one, or is erased.</exception>
        public void SplineFit(DxfPoly3dType value, int segments)
        {
            if (value != DxfPoly3dType.QuadSplinePoly && value != DxfPoly3dType.CubicSplinePoly)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A polyline is fitted with a quadratic or a cubic spline.");
            }

            if (segments == 0 || segments == int.MinValue)
            {
                throw new ArgumentOutOfRangeException(nameof(segments), segments, "A spline-fitted polyline has some segments to each span.");
            }

            Straighten();
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            bool closed = Closed;

            if (vertices.Count < (closed ? 3 : 2))
            {
                throw new InvalidOperationException("A polyline is fitted on two vertices at least, three for a closed one, and this one has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            var frame = new List<GeoPoint3>(vertices.Count);
            var changes = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                frame.Add(DxfFollowers.Point(vertex.Value, 10, Origin));
                changes.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, DxfFollowers.WholeChange(vertex.Value, 70, DxfFollowers.Whole(vertex.Value, 70, 0) | 16, 0)));
            }

            DxfNurbs spline = DxfPolylineFit.Spline(frame, value == DxfPoly3dType.QuadSplinePoly ? 2 : 3, closed, out double start, out double end);
            var made = new List<DxfPolylineVertex3d>();

            foreach (double step in DxfPolylineFit.Steps(start, end, Math.Abs(segments), closed))
            {
                var vertex = new DxfPolylineVertex3d(spline.PointAt(step));
                vertex.SetFlags(8 | 32);
                made.Add(vertex);
            }

            SetGroupsAtMany(changes, VertexOrder);
            int place = RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount;

            foreach (DxfPolylineVertex3d vertex in made)
            {
                InsertFollowerAt(place++, vertex);
            }

            SetGroups(new KeyValuePair<int, string>(70, (GetInt(70, 0) | 4).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, value == DxfPoly3dType.QuadSplinePoly ? "5" : "6"));
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

            Item.File.RemoveFollowers(Item, made);

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

        /// <summary>
        /// Gets the pieces the polyline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: 3D polylines of the same
        /// look in no file, an open one from its start to the first parameter, on to the next and to its end, a closed one from
        /// each to the next and from the last on round its first vertex to the first, given whole where it is cut at fewer than
        /// two places; (0, 0, 0), (3, 4, 0), (3, 4, 12) cut at 1.5 is (0, 0, 0), (3, 4, 0), (3, 4, 6) and (3, 4, 6), (3, 4, 12). A
        /// parameter at an end or within the point tolerance along the polyline of another cuts nothing; a fitted polyline is
        /// cut along the vertices it is drawn through, and its pieces are straight.
        /// </summary>
        /// <param name="parameters">The parameters to cut at, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The pieces, in the order of the polyline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices drawn.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            List<DxfPolyVertex> v = Model(out _);
            DxfBulgeChain chain = Chain();
            Two(v.Count);
            double end = chain.EndParam;
            bool closed = Closed;
            List<double> cuts = DxfCurveTools.Cuts(parameters, 0.0, end, closed, (a, b) => chain.DistanceAt(b) - chain.DistanceAt(a), x => Within(x, end, nameof(parameters)));
            List<List<DxfPolyVertex>> pieces = DxfPolylineEdit.Split(v, closed, cuts);

            if (pieces.Count == 0)
            {
                return new DxfCurve[] { (DxfPolyline3d)Copy() };
            }

            var result = new List<DxfCurve>(pieces.Count);

            foreach (List<DxfPolyVertex> piece in pieces)
            {
                result.Add(Made(piece, false));
            }

            return result;
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in the plane it lies in, as AutoCAD's <c>GetOffsetCurves</c>, as
        /// <see cref="GetOffsetCurvesGivenPlaneNormal"/> offsets it about the normal of that plane turned to the side its largest
        /// part is above nought on, (0, 0, 1) for a polyline on the floor: to the right of its way seen from above, so that a
        /// closed polyline running counter-clockwise grows.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The 3D polylines, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline does not lie in one plane, or has fewer than two vertices drawn.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            List<DxfPolyVertex> v = Model(out _);
            Two(v.Count);

            if (!DxfCurveTools.TryPlane(Points(v), default(GeoVector3), out GeoVector3 normal))
            {
                throw new InvalidOperationException("A 3D polyline that does not lie in one plane has no side to be offset to: give the plane's normal.");
            }

            return Offset(v, normal, offsetDist);
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: the polyline taken into the plane square to the normal it lies in, offset there
        /// as a <see cref="DxfPolyline"/> offsets its own, to the right of its way seen from the tip of the normal where the
        /// distance is above nought, and taken back; an arc a corner comes round by is given as the chords that keep within the
        /// point tolerance of it, a 3D polyline having none.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, square to the polyline.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The 3D polylines, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal has no length, or the polyline does not lie in a plane square to it.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices drawn.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            List<DxfPolyVertex> v = Model(out _);
            Two(v.Count);

            if (!planeNormal.IsValid || !(planeNormal.Length > 0.0) || !DxfCurveTools.TryPlane(Points(v), planeNormal, out GeoVector3 normal) || !normal.IsParallelTo(planeNormal, Tolerance.Default))
            {
                throw new ArgumentException("A 3D polyline is offset in a plane it lies in: the normal " + planeNormal + " is not square to it.", nameof(planeNormal));
            }

            return Offset(v, planeNormal.Normalize(), offsetDist);
        }

        /// <summary>
        /// Runs an end segment of the polyline on to a parameter past an end, as AutoCAD's <c>Extend</c>: above
        /// <see cref="EndParam"/> the last segment on along itself, its end vertex moved to how far past it the parameter is
        /// along that segment; below nought the first back. The one vertex record is written, every handle kept.
        /// </summary>
        /// <param name="newParameter">The parameter, below nought or above <see cref="EndParam"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the polyline, or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed or fitted, has fewer than two vertices or an
        /// end segment of no length, or is erased.</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");
            List<DxfPolyVertex> v = Model(out List<int> records);
            Open(v.Count);

            if (newParameter >= 0.0 && newParameter <= v.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the polyline: one past an end extends it.");
            }

            DxfPolylineEdit.Extend(v, newParameter, nameof(newParameter));
            WriteEnd(v, records, newParameter < 0.0 ? 0 : v.Count - 1);
        }

        /// <summary>Moves an end of the polyline to a point on the line through its end segment past that end, as AutoCAD's <c>Extend</c>; a point on the segment itself is refused.</summary>
        /// <param name="extendStart">Whether the first vertex is moved, else the last.</param>
        /// <param name="toPoint">The point, within the point tolerance of the line through the end segment.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the line through the end segment, or on the polyline itself rather than past the end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed or fitted, has fewer than two vertices or an
        /// end segment of no length, or is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            List<DxfPolyVertex> v = Model(out List<int> records);
            Open(v.Count);
            double share = DxfPolylineEdit.ShareOf(v, extendStart, toPoint.X, toPoint.Y, toPoint.Z, false);

            if (double.IsNaN(share))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the line through the polyline's end segment.", nameof(toPoint));
            }

            DxfPolylineEdit.Beyond(share, extendStart, toPoint, nameof(toPoint));
            DxfPolylineEdit.Extend(v, !extendStart, share, nameof(toPoint));
            WriteEnd(v, records, extendStart ? 0 : v.Count - 1);
        }

        #endregion

        #region The vertices as the editing sees them

        /// <summary>The vertices drawn in the editing's model (those of a fitted polyline's frame left out), in the world; and the index of each one's record.</summary>
        internal List<DxfPolyVertex> Model(out List<int> records)
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            bool fitted = vertices.Exists(v => (DxfFollowers.Whole(v.Value, 70, 0) & 8) != 0);
            var model = new List<DxfPolyVertex>(vertices.Count);
            records = new List<int>(vertices.Count);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                if (fitted && (DxfFollowers.Whole(vertex.Value, 70, 0) & 16) != 0)
                {
                    continue;
                }

                GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                model.Add(new DxfPolyVertex(at.X, at.Y, at.Z, 0.0, 0.0, 0.0, false));
                records.Add(vertex.Key);
            }

            return model;
        }

        /// <summary>The polylines at a distance in the plane of a unit normal, through the plane's system.</summary>
        private IReadOnlyList<DxfCurve> Offset(List<DxfPolyVertex> v, GeoVector3 normal, double distance)
        {
            Guard.Finite(distance, nameof(distance), "An offset has to be a finite number.");
            var local = new List<DxfPolyVertex>(v.Count);

            foreach (DxfPolyVertex q in v)
            {
                GeoPoint3 p = WorldToOcs(normal, new GeoPoint3(q.X, q.Y, q.Z));
                local.Add(new DxfPolyVertex(p.X, p.Y, p.Z, 0.0, 0.0, 0.0, false));
            }

            double elevation = local[0].Z;
            var result = new List<DxfCurve>();

            foreach (KeyValuePair<List<DxfPolyVertex>, bool> one in DxfPolylineEdit.Offset(local, Closed, distance, elevation))
            {
                var world = new List<DxfPolyVertex>();

                foreach (DxfPolyVertex q in DxfPolylineEdit.Straightened(one.Key, one.Value))
                {
                    GeoPoint3 p = OcsToWorld(normal, new GeoPoint3(q.X, q.Y, elevation));
                    world.Add(new DxfPolyVertex(p.X, p.Y, p.Z, 0.0, 0.0, 0.0, false));
                }

                result.Add(Made(world, one.Value));
            }

            return result;
        }

        /// <summary>A 3D polyline of this one's look in no file, of vertices of the editing's model: straight, closed or not.</summary>
        private DxfPolyline3d Made(List<DxfPolyVertex> model, bool closed)
        {
            var copy = (DxfPolyline3d)Copy();
            copy.SetGroups(new KeyValuePair<int, string>(70, ((GetInt(70, 0) & ~5) | (closed ? 1 : 0)).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
            var records = new List<KeyValuePair<string, KeyValuePair<int, string>[]>>(model.Count);

            foreach (DxfPolyVertex q in model)
            {
                records.Add(new KeyValuePair<string, KeyValuePair<int, string>[]>("VERTEX", new[]
                {
                    new KeyValuePair<int, string>(100, "AcDbVertex"),
                    new KeyValuePair<int, string>(100, "AcDb3dPolylineVertex"),
                    new KeyValuePair<int, string>(10, Numbers.Exact(q.X + 0.0)),
                    new KeyValuePair<int, string>(20, Numbers.Exact(q.Y + 0.0)),
                    new KeyValuePair<int, string>(30, Numbers.Exact(q.Z + 0.0)),
                    new KeyValuePair<int, string>(70, "32"),
                }));
            }

            copy.SetFollowers(records);
            return copy;
        }

        /// <summary>Writes one end vertex of the editing's model into its record.</summary>
        private void WriteEnd(List<DxfPolyVertex> model, List<int> records, int index)
        {
            DxfPolyVertex q = model[index];
            IReadOnlyList<DxfTypedValue> groups = GetOwnGroups(records[index]);
            SetGroupsAt(records[index], VertexOrder, DxfFollowers.PointChanges(groups, 10, new GeoPoint3(q.X, q.Y, q.Z), Origin, 3));
        }

        private static List<GeoPoint3> Points(List<DxfPolyVertex> v)
        {
            var points = new List<GeoPoint3>(v.Count);

            foreach (DxfPolyVertex q in v)
            {
                points.Add(new GeoPoint3(q.X, q.Y, q.Z));
            }

            return points;
        }

        /// <summary>Refuses a polyline of fewer than two vertices drawn, which has no segment to work on.</summary>
        private static void Two(int count)
        {
            if (count < 2)
            {
                throw new InvalidOperationException("A polyline of " + count.ToString(CultureInfo.InvariantCulture) + " vertices drawn has no segment.");
            }
        }

        /// <summary>Refuses a closed or fitted polyline, which has no end of its own to be extended, or one of fewer than two vertices.</summary>
        private void Open(int count)
        {
            Two(count);

            if (Closed)
            {
                throw new InvalidOperationException("A closed polyline has no end to be extended.");
            }

            if (PolyType != DxfPoly3dType.SimplePoly)
            {
                throw new InvalidOperationException("A fitted polyline is drawn through vertices the fitting made: straighten it to extend it.");
            }
        }

        #endregion

        /// <summary>
        /// Gets the lines the polyline breaks into, as AutoCAD's <c>Explode</c>: a LINE for each segment between two vertices it
        /// is drawn through, in its order, the closing one of a closed polyline among them, each with its look; a segment of no
        /// length left out. The polyline through (0, 0, 0), (1, 2, 3) and (4, 5, 6) comes out as two lines.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode() => ChainParts(null, null).AsReadOnly();

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
