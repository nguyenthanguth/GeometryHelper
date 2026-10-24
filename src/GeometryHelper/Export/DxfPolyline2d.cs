using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A POLYLINE of the plane of a DXF file, as AutoCAD's <c>Polyline2d</c>: the POLYLINE record with its
    /// <see cref="DxfVertex2d"/> records and its SEQEND, the vertices in the plane of its <see cref="Normal"/> at its
    /// <see cref="Elevation"/>, each segment a line or, where the vertex it leaves has a bulge, an arc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The polyline is walked as AutoCAD walks one, <c>foreach (DxfVertex2d vertex in polyline)</c>; a vertex's
    /// <see cref="DxfVertex2d.Position"/> is in the system of the normal and <see cref="VertexPosition(DxfVertex2d)"/> gives it
    /// in the world. The polyline (0, 0), (12, 0), (12, 8) closed is <see cref="Length"/> 34.42 and <see cref="DxfCurve.Area"/>
    /// 48; its parameter is AutoCAD's, the index of the vertex a segment leaves plus how far along it.
    /// </para>
    /// <para>
    /// This is the polyline of the plane a file of any version holds, R12 among them, where a <see cref="DxfPolyline"/> cannot
    /// be: <c>new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0, true, 0, 0, null)</c> is appended to a drawing of R12 as
    /// its POLYLINE, VERTEX and SEQEND records. A vertex set, added or taken out is that one record written, put in or taken
    /// out, every other vertex keeping its handle.
    /// </para>
    /// </remarks>
    public sealed class DxfPolyline2d : DxfCurve, IEnumerable<DxfVertex2d>
    {
        // The groups of AcDb2dPolyline in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 66, 10, 20, 30, 39, 70, 40, 41, 71, 72, 73, 74, 75, 210, 220, 230 });

        // The groups of a vertex in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> VertexOrder = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 40, 41, 42, 70, 50, 71, 72, 73, 74, 91 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>Initializes a polyline of no vertices, as AutoCAD's <c>new Polyline2d()</c>, on layer 0 in no file.</summary>
        public DxfPolyline2d()
            : base("POLYLINE", Head(0.0, false, 0.0, 0.0))
        {
            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });
        }

        /// <summary>
        /// Initializes a polyline of vertices, as AutoCAD's <c>new Polyline2d(type, vertices, elevation, closed, startWidth,
        /// endWidth, bulges)</c>, on layer 0 in no file until it is added to one.
        /// </summary>
        /// <param name="type">How it is drawn: <see cref="DxfPoly2dType.SimplePoly"/>, the one made in code.</param>
        /// <param name="vertices">The vertices, in the system of the normal (0, 0, 1); their heights are not read.</param>
        /// <param name="elevation">The height of its plane.</param>
        /// <param name="closed">Whether a segment runs from the last vertex back to the first.</param>
        /// <param name="startWidth">The width where each segment starts, its default start width too.</param>
        /// <param name="endWidth">The width where each segment ends, its default end width too.</param>
        /// <param name="bulges">The bulge of each vertex, as many as there are vertices; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite, or the bulges are not one to each vertex.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite, or a width is below nought.</exception>
        /// <exception cref="NotSupportedException">Thrown for a type other than <see cref="DxfPoly2dType.SimplePoly"/>, whose fitted vertices the library does not make yet.</exception>
        public DxfPolyline2d(DxfPoly2dType type, IEnumerable<GeoPoint3> vertices, double elevation, bool closed, double startWidth, double endWidth, IEnumerable<double> bulges)
            : base("POLYLINE", Head(elevation, closed, startWidth, endWidth))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPoly2dType.SimplePoly)
            {
                throw new NotSupportedException("A polyline fitted with a curve or a spline is not made in code yet: make it of SimplePoly.");
            }

            var points = new List<GeoPoint3>(vertices);
            var bulge = bulges == null ? null : new List<double>(bulges);

            if (bulge != null && bulge.Count != points.Count)
            {
                throw new ArgumentException("The bulges are one to each vertex: " + bulge.Count.ToString(CultureInfo.InvariantCulture) + " for " + points.Count.ToString(CultureInfo.InvariantCulture) + ".", nameof(bulges));
            }

            SetFollowers(new[] { new KeyValuePair<string, KeyValuePair<int, string>[]>("SEQEND", new KeyValuePair<int, string>[0]) });

            for (int i = 0; i < points.Count; i++)
            {
                var p = new GeoPoint3(points[i].X, points[i].Y, elevation);
                InsertFollowerAt(i + 1, new DxfVertex2d(p, bulge == null ? 0.0 : bulge[i], startWidth, endWidth, 0.0));
            }
        }

        private DxfPolyline2d(DxfItem item)
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
            set => SetFlag(1, value);
        }

        /// <summary>
        /// Gets or sets how far the plane of the polyline stands from the origin along its normal (group 30 of its point), as
        /// AutoCAD's <c>Elevation</c>: the height of the first vertex where the record gives none, as the reader reads it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double Elevation
        {
            get
            {
                if (GetString(30) != null)
                {
                    return GetDouble(30, 0.0);
                }

                List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
                return vertices.Count > 0 ? DxfFollowers.Point(vertices[0].Value, 10, Origin).Z : 0.0;
            }

            set
            {
                Guard.Finite(value, nameof(value), "An elevation has to be a finite number.");

                if (!Same(value, Elevation))
                {
                    SetGroups(PointGroups(10, new GeoPoint3(GetDouble(10, 0.0), GetDouble(20, 0.0), value), Origin));
                }
            }
        }

        /// <summary>
        /// Gets or sets the normal of the plane of the polyline (groups 210, 220, 230), of unit length; (0, 0, 1) where the record
        /// gives none.
        /// </summary>
        /// <value>
        /// Set, a direction of some length, written made of unit length. The vertices keep their coordinates in the plane of the
        /// normal, as AutoCAD's polyline keeps them, so the polyline turns with the plane and moves in the world: the vertex
        /// (10, 0) given the normal (0, 0, -1) is at (-10, 0, 0) in the world. A <see cref="DxfCircle"/>, <see cref="DxfArc"/>,
        /// <see cref="DxfDBText"/> or <see cref="DxfBlockReference"/> keeps its place in the world instead, and a
        /// <see cref="DxfSolid"/> its corners.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>Gets or sets how far the polyline stands up along its <see cref="Normal"/> (group 39); nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>Gets or sets the width a vertex added starts with (group 40), as AutoCAD's <c>DefaultStartWidth</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double DefaultStartWidth
        {
            get => GetDouble(40, 0.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");
                SetDouble(40, value, 0.0);
            }
        }

        /// <summary>Gets or sets the width a vertex added ends with (group 41), as AutoCAD's <c>DefaultEndWidth</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double DefaultEndWidth
        {
            get => GetDouble(41, 0.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");
                SetDouble(41, value, 0.0);
            }
        }

        /// <summary>
        /// Gets or sets the width of the whole polyline, as AutoCAD's <c>ConstantWidth</c>: the width every vertex has at both
        /// ends of the segment it starts; nought where the widths differ.
        /// </summary>
        /// <value>Set, nought or more: given to every vertex and as the default widths.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public double ConstantWidth
        {
            get
            {
                List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);

                if (vertices.Count == 0)
                {
                    return 0.0;
                }

                double width = DxfFollowers.Number(vertices[0].Value, 40, 0.0);

                foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
                {
                    if (!Same(DxfFollowers.Number(vertex.Value, 40, 0.0), width) || !Same(DxfFollowers.Number(vertex.Value, 41, 0.0), width))
                    {
                        return 0.0;
                    }
                }

                return width;
            }

            set
            {
                Guard.NonNegative(value, nameof(value), "A width has to be a finite number, nought or more.");

                if (Same(value, ConstantWidth))
                {
                    return;
                }

                var records = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();

                foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in DxfFollowers.Vertices(Item))
                {
                    records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, Join(
                        DxfFollowers.NumberChange(vertex.Value, 40, value, 0.0, true),
                        DxfFollowers.NumberChange(vertex.Value, 41, value, 0.0, true))));
                }

                SetGroupsAtMany(records, VertexOrder);
                DefaultStartWidth = value;
                DefaultEndWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets how the polyline is drawn through its vertices, as AutoCAD's <c>PolyType</c>: fitted with a curve (flag 2
        /// of group 70), with a spline (flag 4, quadratic where group 75 is 5, cubic otherwise), or straight.
        /// </summary>
        /// <value>Set, <see cref="DxfPoly2dType.SimplePoly"/>, which straightens it as <see cref="Straighten"/> does.</value>
        /// <exception cref="NotSupportedException">Thrown when set to a fitted type, whose vertices the library does not make yet.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public DxfPoly2dType PolyType
        {
            get
            {
                int flags = GetInt(70, 0);

                if ((flags & 4) != 0)
                {
                    return GetInt(75, 0) == 5 ? DxfPoly2dType.QuadSplinePoly : DxfPoly2dType.CubicSplinePoly;
                }

                return (flags & 2) != 0 ? DxfPoly2dType.FitCurvePoly : DxfPoly2dType.SimplePoly;
            }

            set
            {
                if (value == PolyType)
                {
                    return;
                }

                if (value != DxfPoly2dType.SimplePoly)
                {
                    throw new NotSupportedException("A polyline is not fitted with a curve or a spline by the library yet.");
                }

                Straighten();
            }
        }

        /// <summary>
        /// Gets or sets whether the linetype runs on round the vertices rather than starting again at each (flag 128 of group
        /// 70), as AutoCAD's <c>LinetypeGenerationOn</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased.</exception>
        public bool LinetypeGenerationOn
        {
            get => (GetInt(70, 0) & 128) != 0;
            set => SetFlag(128, value);
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
        public IEnumerator<DxfVertex2d> GetEnumerator()
        {
            var vertices = new List<DxfVertex2d>();

            for (int record = 1; record < RecordCount; record++)
            {
                if (RecordType(record) == "VERTEX" && FollowerAt(record) is DxfVertex2d vertex)
                {
                    vertices.Add(vertex);
                }
            }

            return vertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets where a vertex of the polyline is in the world, as AutoCAD's <c>VertexPosition</c>: its point at the polyline's
        /// elevation, taken from the system of its normal to the world as <see cref="DxfReader"/> takes it.
        /// </summary>
        /// <param name="vertex">A vertex of this polyline.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the vertex is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the vertex is not of this polyline.</exception>
        public GeoPoint3 VertexPosition(DxfVertex2d vertex)
        {
            Own(vertex, nameof(vertex));
            return OcsToWorld(Normal, vertex.Position);
        }

        /// <summary>
        /// Appends a vertex made in code after the last, as AutoCAD's <c>AppendVertex</c>: the vertex is that of the polyline
        /// from then on, its height the polyline's elevation.
        /// </summary>
        /// <param name="vertexToAppend">The vertex, in no polyline.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertex is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is of a polyline already, or the polyline is erased.</exception>
        public void AppendVertex(DxfVertex2d vertexToAppend)
        {
            if (vertexToAppend == null)
            {
                throw new ArgumentNullException(nameof(vertexToAppend));
            }

            int end = RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount;
            Insert(end, vertexToAppend);
        }

        /// <summary>
        /// Appends a vertex made in code after the last, as AutoCAD's <c>NonDBAppendVertex</c> does for a polyline in no
        /// drawing; here the same as <see cref="AppendVertex(DxfVertex2d)"/>, for a polyline in a drawing or not.
        /// </summary>
        /// <param name="vertexToAppend">The vertex, in no polyline.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertex is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the vertex is of a polyline already, or the polyline is erased.</exception>
        public void NonDBAppendVertex(DxfVertex2d vertexToAppend) => AppendVertex(vertexToAppend);

        /// <summary>
        /// Puts a vertex made in code after a vertex of the polyline, as AutoCAD's <c>InsertVertexAt</c>; before the first where
        /// the vertex given is null.
        /// </summary>
        /// <param name="indexVertex">The vertex of this polyline the new one follows; null to put it first.</param>
        /// <param name="newVertex">The vertex, in no polyline.</param>
        /// <exception cref="ArgumentNullException">Thrown when the new vertex is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the vertex it is to follow is not of this polyline.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the new vertex is of a polyline already, or the polyline is erased.</exception>
        public void InsertVertexAt(DxfVertex2d indexVertex, DxfVertex2d newVertex)
        {
            if (newVertex == null)
            {
                throw new ArgumentNullException(nameof(newVertex));
            }

            int at = 1;

            if (indexVertex != null)
            {
                Own(indexVertex, nameof(indexVertex));
                at = indexVertex.Record + 1;
            }

            Insert(at, newVertex);
        }

        /// <summary>
        /// Straightens a polyline fitted with a curve or a spline, as AutoCAD's <c>Straighten</c>: the vertices the fitting made
        /// (flags 1 and 8) are taken out, those of the frame (16) are vertices of the polyline again, and flags 2 and 4 of group
        /// 70 and its group 75 go. A polyline that is not fitted is left as it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void Straighten()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            var frame = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            var made = new List<int>();
            bool fitted = (GetInt(70, 0) & 6) != 0;

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                int flags = DxfFollowers.Whole(vertex.Value, 70, 0);
                fitted |= (flags & 25) != 0;

                if ((flags & 9) != 0)
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

            SetGroups(new KeyValuePair<int, string>(70, (GetInt(70, 0) & ~6).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
        }

        #endregion

        #region The curve

        /// <summary>Gets the parameter at the end of the polyline: the count of its segments, as AutoCAD's.</summary>
        public override double EndParam => Chain().EndParam;

        /// <summary>
        /// Gets the area the polyline closes, as AutoCAD's <c>Area</c>: that of the polygon of its vertices with what each arc
        /// adds or takes off; an open polyline taken as closed by a line from its last vertex to its first.
        /// </summary>
        public override double Area => Chain().Area;

        /// <summary>Gets the point at a parameter, the index of a vertex plus how far along the segment it leaves, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The point, in the world.</returns>
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
        /// <returns>The point, in the world.</returns>
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
        /// <returns>The nearest point, in the world.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has no vertex.</exception>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            Chain().ClosestParameter(givenPoint, out GeoPoint3 closest);
            return closest;
        }

        /// <summary>
        /// Gets the derivative of the point by the parameter, as AutoCAD's <c>GetFirstDerivative</c>: on a line the vector from
        /// its start to its end, on an arc the tangent as long as the arc.
        /// </summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The derivative, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.FirstDerivative(value);
        }

        /// <summary>Gets the second derivative of the point by the parameter, as AutoCAD's <c>GetSecondDerivative</c>: nought on a line.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The second derivative, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.SecondDerivative(value);
        }

        /// <summary>
        /// Turns the polyline to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: the vertices in the other order,
        /// each segment's bulge turned over and its widths swapped; a closed polyline keeps its first vertex. Each vertex record
        /// takes the point and flags of its counterpart in one writing of the polyline, every handle kept.
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
                int from = closed ? (n - j) % n : n - 1 - j;
                int segment = closed ? (n - 1 - j + n) % n : n - 2 - j;
                IReadOnlyList<DxfTypedValue> own = vertices[j].Value;
                IReadOnlyList<DxfTypedValue> source = vertices[from].Value;
                IReadOnlyList<DxfTypedValue> leaves = segment >= 0 ? vertices[segment].Value : null;
                double bulge = leaves == null ? 0.0 : -DxfFollowers.Number(leaves, 42, 0.0);
                double start = leaves == null ? 0.0 : DxfFollowers.Number(leaves, 41, 0.0);
                double end = leaves == null ? 0.0 : DxfFollowers.Number(leaves, 40, 0.0);
                records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertices[j].Key, Join(
                    DxfFollowers.PointChanges(own, 10, DxfFollowers.Point(source, 10, Origin), Origin, 3),
                    DxfFollowers.NumberChange(own, 42, bulge, 0.0, true),
                    DxfFollowers.NumberChange(own, 40, start, 0.0, true),
                    DxfFollowers.NumberChange(own, 41, end, 0.0, true),
                    DxfFollowers.WholeChange(own, 70, DxfFollowers.Whole(source, 70, 0), 0))));
            }

            SetGroupsAtMany(records, VertexOrder);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the polyline, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each vertex taken
        /// from the old plane at the elevation to the world, moved, and into the plane of the new normal; the elevation that of
        /// the first vertex there, the widths and thickness scaled with it, the bulges kept. The vertices are written in one
        /// writing of the polyline, each in place, its handle kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it takes the plane of the polyline to no plane, or scales it more one
        /// way than another while the polyline has arcs.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public override void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            var ocs = new DxfOcsTransform(Normal, transform);
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);

            if (!ocs.ScaleUniform && vertices.Exists(v => DxfFollowers.Number(v.Value, 42, 0.0) != 0.0))
            {
                throw new ArgumentException("A polyline with arcs is scaled the same along every direction of its plane, or its arcs would be ellipses.", nameof(transform));
            }

            bool elevated = GetString(10) != null || GetString(20) != null || GetString(30) != null;
            double elevation = GetDouble(30, 0.0);
            double first = 0.0;
            var records = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                IReadOnlyList<DxfTypedValue> vertex = vertices[i].Value;
                GeoPoint3 at = DxfFollowers.Point(vertex, 10, Origin);
                GeoPoint3 moved = ocs.Vertex(elevated ? new GeoPoint3(at.X, at.Y, elevation) : at);

                if (i == 0)
                {
                    first = moved.Z;
                }

                var changes = new List<KeyValuePair<int, string>>(DxfFollowers.PointChanges(vertex, 10, moved, Origin, 3));

                foreach (int code in new[] { 40, 41 })
                {
                    if (DxfFollowers.Text(vertex, code) != null)
                    {
                        changes.AddRange(DxfFollowers.NumberChange(vertex, code, ocs.Width(DxfFollowers.Number(vertex, code, 0.0)), 0.0, false));
                    }
                }

                records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertices[i].Key, changes.ToArray()));
            }

            SetGroupsAtMany(records, VertexOrder);
            var head = new List<KeyValuePair<int, string>>();

            if (vertices.Count > 0)
            {
                head.AddRange(PointGroups(10, new GeoPoint3(0.0, 0.0, first), Origin));
            }

            foreach (int code in new[] { 40, 41 })
            {
                if (GetString(code) != null)
                {
                    head.AddRange(NumberGroups(code, ocs.Width(GetDouble(code, 0.0)), 0.0));
                }
            }

            if (GetString(39) != null)
            {
                head.AddRange(NumberGroups(39, ocs.Thickness(GetThickness()), 0.0));
            }

            head.AddRange(NormalGroups(ocs.NewExtrusion));
            SetGroups(head.ToArray());
        }

        #endregion

        [DxfType("POLYLINE")]
        internal static DxfEntity Create(DxfItem item)
            => (Flags(item) & (8 | 16 | 64)) == 0 ? new DxfPolyline2d(item) : null;

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The flags (group 70) of a POLYLINE record, nought where it gives none.</summary>
        internal static int Flags(DxfItem item)
            => int.TryParse(item.File.ValueOf(item, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ? flags : 0;

        /// <summary>The groups of a POLYLINE of the plane made in code: its marker, the flag that vertices follow, its point at the elevation, its flags and widths.</summary>
        private static IEnumerable<DxfTypedValue> Head(double elevation, bool closed, double startWidth, double endWidth)
        {
            Guard.Finite(elevation, nameof(elevation), "An elevation has to be a finite number.");
            Guard.NonNegative(startWidth, nameof(startWidth), "A width has to be a finite number, nought or more.");
            Guard.NonNegative(endWidth, nameof(endWidth), "A width has to be a finite number, nought or more.");
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDb2dPolyline"),
                new DxfTypedValue(66, "1"),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, Numbers.Exact(elevation + 0.0)),
                new DxfTypedValue(70, closed ? "1" : "0"),
            };

            if (startWidth != 0.0)
            {
                groups.Add(new DxfTypedValue(40, Numbers.Exact(startWidth + 0.0)));
            }

            if (endWidth != 0.0)
            {
                groups.Add(new DxfTypedValue(41, Numbers.Exact(endWidth + 0.0)));
            }

            return groups;
        }

        /// <summary>The chain of the vertices drawn (those of a fitted polyline's frame left out, as the reader leaves them), at the elevation, in the system of the normal.</summary>
        private DxfBulgeChain Chain()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            bool fitted = vertices.Exists(v => (DxfFollowers.Whole(v.Value, 70, 0) & 8) != 0);
            double elevation = Elevation;
            var points = new List<GeoPoint3>(vertices.Count);
            var bulges = new List<double>(vertices.Count);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                if (fitted && (DxfFollowers.Whole(vertex.Value, 70, 0) & 16) != 0)
                {
                    continue;
                }

                GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                points.Add(new GeoPoint3(at.X, at.Y, elevation));
                bulges.Add(DxfFollowers.Number(vertex.Value, 42, 0.0));
            }

            GeoVector3 normal = Normal;
            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            return new DxfBulgeChain(normal, u, v, points, bulges, Closed);
        }

        /// <summary>Puts a vertex made in code before a record, at the polyline's elevation, as AutoCAD keeps a vertex in the plane of its polyline.</summary>
        private void Insert(int record, DxfVertex2d vertex)
        {
            if (vertex.Polyline != null || vertex.IsResident || vertex.IsErased)
            {
                throw new InvalidOperationException("The vertex is of a polyline already: append a vertex made in code.");
            }

            vertex.AtElevation(Elevation);
            InsertFollowerAt(record, vertex);
        }

        /// <summary>Refuses a vertex that is not of this polyline.</summary>
        private void Own(DxfVertex2d vertex, string parameter)
        {
            if (vertex == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (vertex.Polyline != this || vertex.IsErased)
            {
                throw new ArgumentException("The vertex is not one of this polyline.", parameter);
            }
        }

        private void SetFlag(int flag, bool on)
        {
            int flags = GetInt(70, 0);
            SetInt(70, on ? flags | flag : flags & ~flag, 0);
        }

        /// <summary>The group that sets a number of the head, none where it is the one read.</summary>
        private KeyValuePair<int, string>[] NumberGroups(int code, double value, double fallback)
            => Same(value, GetDouble(code, fallback)) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };

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
