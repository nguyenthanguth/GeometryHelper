using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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
        /// <param name="type">How it is drawn: <see cref="DxfPoly2dType.SimplePoly"/>, the one made in code; fit it afterwards with <see cref="ConvertToPolyType"/>.</param>
        /// <param name="vertices">The vertices, in the system of the normal (0, 0, 1); their heights are not read.</param>
        /// <param name="elevation">The height of its plane.</param>
        /// <param name="closed">Whether a segment runs from the last vertex back to the first.</param>
        /// <param name="startWidth">The width where each segment starts, its default start width too.</param>
        /// <param name="endWidth">The width where each segment ends, its default end width too.</param>
        /// <param name="bulges">The bulge of each vertex, as many as there are vertices; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite, or the bulges are not one to each vertex.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite, or a width is below nought.</exception>
        /// <exception cref="NotSupportedException">Thrown for a type other than <see cref="DxfPoly2dType.SimplePoly"/>: make the polyline straight and fit it with <see cref="ConvertToPolyType"/>.</exception>
        public DxfPolyline2d(DxfPoly2dType type, IEnumerable<GeoPoint3> vertices, double elevation, bool closed, double startWidth, double endWidth, IEnumerable<double> bulges)
            : base("POLYLINE", Head(elevation, closed, startWidth, endWidth))
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (type != DxfPoly2dType.SimplePoly)
            {
                throw new NotSupportedException("A polyline is made in code straight: make it of SimplePoly and fit it with ConvertToPolyType.");
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
        /// <value>Set, the polyline converted to the type as <see cref="ConvertToPolyType"/> converts it; its own type changes nothing.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfPoly2dType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a polyline that is erased, or has too few vertices to fit.</exception>
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
                if (value != PolyType)
                {
                    ConvertToPolyType(value);
                }
            }
        }

        /// <summary>
        /// Converts the polyline to a type, as AutoCAD's <c>ConvertToPolyType</c>: straightened for
        /// <see cref="DxfPoly2dType.SimplePoly"/>, fitted with a curve for <see cref="DxfPoly2dType.FitCurvePoly"/> as
        /// <see cref="CurveFit"/> fits it, and with a quadratic or cubic B-spline of the header's SPLINESEGS segments to a span
        /// (8 where the file gives none) as <see cref="SplineFit(DxfPoly2dType, int)"/> fits it.
        /// </summary>
        /// <param name="newVal">The type.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfPoly2dType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased, or has too few vertices to fit.</exception>
        public void ConvertToPolyType(DxfPoly2dType newVal)
        {
            switch (newVal)
            {
                case DxfPoly2dType.SimplePoly:
                    Straighten();
                    break;

                case DxfPoly2dType.FitCurvePoly:
                    CurveFit();
                    break;

                case DxfPoly2dType.QuadSplinePoly:
                case DxfPoly2dType.CubicSplinePoly:
                    SplineFit(newVal, DxfPolylineFit.Header(Item, "$SPLINESEGS", 8));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newVal), newVal, "A polyline of the plane is simple, fitted with a curve, or with a quadratic or cubic spline.");
            }
        }

        /// <summary>
        /// Fits the polyline with a curve, as AutoCAD's <c>CurveFit</c> (PEDIT Fit): a pair of arcs on each segment, meeting at a
        /// vertex the fitting adds (flag 1 of its group 70) after the vertex the segment leaves, so that the curve passes through
        /// every vertex in the direction its tangent gives there: the one the vertex is given (<see cref="DxfVertex2d.TangentUsed"/>)
        /// or that of the circle through it and its two neighbours; flag 2 of the polyline's group 70. The pair is the biarc of
        /// equal tangent lengths, so that three vertices on a circle give that circle's arcs; a vertex's widths are split at the
        /// middle between its two arcs. A fitted polyline is straightened first. Each vertex of the frame keeps its handle.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices, or is erased.</exception>
        public void CurveFit()
        {
            Straighten();
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            Frame(vertices.Count);
            bool closed = Closed;
            var points = new List<GeoPoint2>(vertices.Count);
            var given = new List<double?>(vertices.Count);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                points.Add(new GeoPoint2(at.X, at.Y));
                given.Add((DxfFollowers.Whole(vertex.Value, 70, 0) & 2) != 0 ? DxfFollowers.Number(vertex.Value, 50, 0.0) * Math.PI / 180.0 : (double?)null);
            }

            List<GeoVector2> tangents = DxfPolylineFit.Tangents(points, given, closed);
            int segments = closed ? points.Count : points.Count - 1;
            var changes = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            var added = new List<KeyValuePair<int, DxfVertex2d>>();
            double elevation = Elevation;

            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % points.Count;
                DxfPolylineFit.Biarc(points[i], tangents[i], points[j], tangents[j], out GeoPoint2 joint, out double first, out double second);
                IReadOnlyList<DxfTypedValue> own = vertices[i].Value;
                double start = DxfFollowers.Number(own, 40, DefaultStartWidth);
                double end = DxfFollowers.Number(own, 41, DefaultEndWidth);
                double middle = 0.5 * (start + end);
                var change = new List<KeyValuePair<int, string>>(DxfFollowers.NumberChange(own, 42, first, 0.0, true));

                if (start != end)
                {
                    change.AddRange(DxfFollowers.NumberChange(own, 41, middle, double.NaN, false));
                }

                changes.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertices[i].Key, change.ToArray()));
                var extra = new DxfVertex2d(new GeoPoint3(joint.X, joint.Y, elevation), second, start != end ? middle : start, end, 0.0);
                extra.SetFlags(1);
                added.Add(new KeyValuePair<int, DxfVertex2d>(vertices[i].Key, extra));
            }

            SetGroupsAtMany(changes, VertexOrder);
            InsertAfter(added);
            SetGroups(new KeyValuePair<int, string>(70, ((GetInt(70, 0) & ~4) | 2).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
        }

        /// <summary>
        /// Fits the polyline with a spline, as AutoCAD's <c>SplineFit</c> (PEDIT Spline): of the header's SPLINETYPE (5 quadratic,
        /// 6 cubic, cubic where the file gives none) and SPLINESEGS (8 where it gives none), as
        /// <see cref="SplineFit(DxfPoly2dType, int)"/> fits it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices, or three for a closed one, or is erased.</exception>
        public void SplineFit()
        {
            int type = DxfPolylineFit.Header(Item, "$SPLINETYPE", 6);
            SplineFit(type == 5 ? DxfPoly2dType.QuadSplinePoly : DxfPoly2dType.CubicSplinePoly, DxfPolylineFit.Header(Item, "$SPLINESEGS", 8));
        }

        /// <summary>
        /// Fits the polyline with a spline, as AutoCAD's <c>SplineFit</c>: its vertices made the frame of a uniform B-spline
        /// (flag 16 of their group 70, each keeping its handle), clamped to the first and last vertex of an open polyline and
        /// running round a closed one, and the vertices of the curve added after them (flag 8), a number of segments to each
        /// span of the spline; flag 4 of the polyline's group 70 and group 75, 5 or 6. A negative number of segments gives as many,
        /// each the arc through its ends and the spline's point between them, as AutoCAD's SPLINESEGS below nought fits arcs to
        /// them. The widths run evenly from the first vertex's start width to the last's end width. A cubic on the frame (0, 0),
        /// (10, 10), (20, 0) is of degree 2 on its three points, one span drawn through (0, 0) and (20, 0) with (10, 5) at its
        /// middle, in 8 segments where SPLINESEGS is 8; on the frame (0, 0), (10, 10), (20, 0), (30, 10) it is a cubic of one
        /// span too. A fitted polyline is straightened first.
        /// </summary>
        /// <param name="value">The spline, <see cref="DxfPoly2dType.QuadSplinePoly"/> or <see cref="DxfPoly2dType.CubicSplinePoly"/>.</param>
        /// <param name="segments">The segments to a span, not nought; below nought for arcs.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not a spline, or the segments are nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices, or three for a closed one, or is erased.</exception>
        public void SplineFit(DxfPoly2dType value, int segments)
        {
            if (value != DxfPoly2dType.QuadSplinePoly && value != DxfPoly2dType.CubicSplinePoly)
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
            Frame(vertices.Count - (closed ? 1 : 0));
            double elevation = Elevation;
            var frame = new List<GeoPoint3>(vertices.Count);
            var changes = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                frame.Add(new GeoPoint3(at.X, at.Y, elevation));
                changes.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, DxfFollowers.WholeChange(vertex.Value, 70, DxfFollowers.Whole(vertex.Value, 70, 0) | 16, 0)));
            }

            DxfNurbs spline = DxfPolylineFit.Spline(frame, value == DxfPoly2dType.QuadSplinePoly ? 2 : 3, closed, out double start, out double end);
            List<double> steps = DxfPolylineFit.Steps(start, end, Math.Abs(segments), closed);
            double startWidth = DxfFollowers.Number(vertices[0].Value, 40, DefaultStartWidth);
            double endWidth = DxfFollowers.Number(vertices[vertices.Count - 1].Value, 41, DefaultEndWidth);
            var made = new List<DxfVertex2d>(steps.Count);
            int count = steps.Count;

            for (int k = 0; k < count; k++)
            {
                GeoPoint3 at = spline.PointAt(steps[k]);
                double bulge = 0.0;

                if (segments < 0 && (closed || k + 1 < count))
                {
                    double next = k + 1 < count ? steps[k + 1] : end;
                    GeoPoint3 middle = spline.PointAt(0.5 * (steps[k] + next));
                    GeoPoint3 to = spline.PointAt(next);
                    bulge = DxfCurveTools.Bulge(at.X, at.Y, middle.X, middle.Y, to.X, to.Y);
                }

                double w0 = startWidth + (endWidth - startWidth) * k / count;
                double w1 = startWidth + (endWidth - startWidth) * (k + 1) / count;
                var vertex = new DxfVertex2d(new GeoPoint3(at.X, at.Y, elevation), bulge, w0, w1, 0.0);
                vertex.SetFlags(8);
                made.Add(vertex);
            }

            SetGroupsAtMany(changes, VertexOrder);
            int place = RecordCount > 1 && RecordType(RecordCount - 1) == "SEQEND" ? RecordCount - 1 : RecordCount;

            foreach (DxfVertex2d vertex in made)
            {
                InsertFollowerAt(place++, vertex);
            }

            SetGroups(new KeyValuePair<int, string>(70, ((GetInt(70, 0) & ~2) | 4).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, value == DxfPoly2dType.QuadSplinePoly ? "5" : "6"));
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
        /// 70 and its group 75 go. The vertices of a polyline fitted with a curve lose the bulges of the arcs the fitting gave
        /// them, so that its segments are straight again, as AutoCAD's decurving leaves them. A polyline that is not fitted is
        /// left as it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is erased.</exception>
        public void Straighten()
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            var frame = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            var made = new List<int>();
            bool fitted = (GetInt(70, 0) & 6) != 0;
            bool curved = (GetInt(70, 0) & 2) != 0 || vertices.Exists(v => (DxfFollowers.Whole(v.Value, 70, 0) & 1) != 0);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                int flags = DxfFollowers.Whole(vertex.Value, 70, 0);
                fitted |= (flags & 25) != 0;

                if ((flags & 9) != 0)
                {
                    made.Add(vertex.Key);
                }
                else if ((flags & 16) != 0 || (curved && DxfFollowers.Text(vertex.Value, 42) != null))
                {
                    frame.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, Join(
                        DxfFollowers.WholeChange(vertex.Value, 70, flags & ~16, 0),
                        curved ? DxfFollowers.NumberChange(vertex.Value, 42, 0.0, 0.0, true) : new KeyValuePair<int, string>[0])));
                }
            }

            if (!fitted)
            {
                return;
            }

            SetGroupsAtMany(frame, VertexOrder);

            Item.File.RemoveFollowers(Item, made);

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
        /// way than another or shears it while the polyline has arcs.</exception>
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

        /// <summary>
        /// Gets the pieces the polyline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>, as a
        /// <see cref="DxfPolyline"/> cuts its own: polylines of the plane of the same look in no file, each segment cut keeping
        /// the part of its arc and its widths read off along it. A fitted polyline is cut along the vertices it is drawn
        /// through, and its pieces are straight polylines of those.
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
                return new DxfCurve[] { (DxfPolyline2d)Copy() };
            }

            var result = new List<DxfCurve>(pieces.Count);

            foreach (List<DxfPolyVertex> piece in pieces)
            {
                result.Add(Made(piece, false));
            }

            return result;
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in its plane, as AutoCAD's <c>GetOffsetCurves</c>, as a
        /// <see cref="DxfPolyline"/> offsets its own: to the right of its way seen from the tip of its <see cref="Normal"/>
        /// where the distance is above nought, arcs kept and corners run on until the segments meet. A fitted polyline is
        /// offset along the vertices it is drawn through, and gives straight polylines. A closed one that crosses itself has
        /// none, as a <see cref="DxfPolyline"/> has none.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The polylines of the plane, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices drawn.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            Guard.Finite(offsetDist, nameof(offsetDist), "An offset has to be a finite number.");
            List<DxfPolyVertex> v = Model(out _);
            Two(v.Count);
            var result = new List<DxfCurve>();

            foreach (KeyValuePair<List<DxfPolyVertex>, bool> one in DxfPolylineEdit.Offset(v, Closed, offsetDist, Elevation))
            {
                result.Add(Made(one.Key, one.Value));
            }

            return result;
        }

        /// <summary>
        /// Gets the polylines at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: as <see cref="GetOffsetCurves(double)"/>, to the right seen from the tip of
        /// the normal given, which is the polyline's own either way.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, along the polyline's <see cref="Normal"/> either way.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The polylines of the plane, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal is not along the polyline's within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline has fewer than two vertices drawn.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            GeoVector3 normal = Normal;

            if (!planeNormal.IsValid || planeNormal.IsZeroLength(Tolerance.Default) || !planeNormal.IsParallelTo(normal, Tolerance.Default))
            {
                throw new ArgumentException("A polyline is offset in its own plane: the normal " + planeNormal + " is not along the polyline's.", nameof(planeNormal));
            }

            return GetOffsetCurves(planeNormal.DotProduct(normal) < 0.0 ? -offsetDist : offsetDist);
        }

        /// <summary>
        /// Runs an end segment of the polyline on to a parameter past an end, as AutoCAD's <c>Extend</c>, as a
        /// <see cref="DxfPolyline"/> runs its own on: the end vertex moved, and the bulge and widths of the segment that reaches
        /// it, in one writing of the polyline, every handle kept.
        /// </summary>
        /// <param name="newParameter">The parameter, below nought or above <see cref="EndParam"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the polyline, is not finite, or would
        /// take an arc round a whole turn.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed or fitted, has fewer than two vertices or
        /// an end segment of no length, or is erased.</exception>
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
            WriteEnds(v, records);
        }

        /// <summary>
        /// Moves an end of the polyline to a point on its end segment run on, as AutoCAD's <c>Extend</c>, as a
        /// <see cref="DxfPolyline"/> moves its own: on the line through a line segment or the circle of an arc, in its plane, past
        /// the end it moves.
        /// </summary>
        /// <param name="extendStart">Whether the first vertex is moved, else the last.</param>
        /// <param name="toPoint">The point, in the world, within the point tolerance of the end segment run on.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the end segment run on, on the polyline itself rather than past the end, or out of the polyline's plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the polyline is closed or fitted, has fewer than two vertices or
        /// an end segment of no length, or is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            List<DxfPolyVertex> v = Model(out List<int> records);
            Open(v.Count);
            GeoPoint3 local = WorldToOcs(Normal, toPoint);
            double share = Math.Abs(local.Z - Elevation) <= Tolerance.Default.EqualPoint ? DxfPolylineEdit.ShareOf(v, extendStart, local.X, local.Y, 0.0, true) : double.NaN;

            if (double.IsNaN(share))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the polyline's end segment run on.", nameof(toPoint));
            }

            DxfPolylineEdit.Beyond(share, extendStart, toPoint, nameof(toPoint));
            DxfPolylineEdit.Extend(v, !extendStart, share, nameof(toPoint));
            WriteEnds(v, records);
        }

        #endregion

        #region The vertices as the editing sees them

        /// <summary>
        /// The vertices drawn in the editing's model (those of a fitted polyline's frame left out, as the reader leaves them):
        /// points in the system of the normal at the elevation, bulges, widths (the default widths where a vertex gives none);
        /// and the index of each one's record.
        /// </summary>
        /// <summary>
        /// The frame of a polyline fitted with a spline, in the system of its normal at its elevation: its vertices of the frame
        /// (flag 16 of their group 70); null where it is not fitted with a spline or has no frame of two points.
        /// </summary>
        internal List<GeoPoint3> SplineFrame()
        {
            DxfPoly2dType type = PolyType;

            if (type != DxfPoly2dType.QuadSplinePoly && type != DxfPoly2dType.CubicSplinePoly)
            {
                return null;
            }

            double elevation = Elevation;
            var frame = new List<GeoPoint3>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in DxfFollowers.Vertices(Item))
            {
                if ((DxfFollowers.Whole(vertex.Value, 70, 0) & 16) != 0)
                {
                    GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                    frame.Add(new GeoPoint3(at.X, at.Y, elevation));
                }
            }

            return frame.Count >= 2 ? frame : null;
        }

        internal List<DxfPolyVertex> Model(out List<int> records)
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            bool fitted = vertices.Exists(v => (DxfFollowers.Whole(v.Value, 70, 0) & 8) != 0);
            double elevation = Elevation;
            double startWidth = DefaultStartWidth;
            double endWidth = DefaultEndWidth;
            var model = new List<DxfPolyVertex>(vertices.Count);
            records = new List<int>(vertices.Count);

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                if (fitted && (DxfFollowers.Whole(vertex.Value, 70, 0) & 16) != 0)
                {
                    continue;
                }

                GeoPoint3 at = DxfFollowers.Point(vertex.Value, 10, Origin);
                bool widths = DxfFollowers.Text(vertex.Value, 40) != null || DxfFollowers.Text(vertex.Value, 41) != null;
                model.Add(new DxfPolyVertex(at.X, at.Y, elevation, DxfFollowers.Number(vertex.Value, 42, 0.0), DxfFollowers.Number(vertex.Value, 40, startWidth), DxfFollowers.Number(vertex.Value, 41, endWidth), widths));
                records.Add(vertex.Key);
            }

            return model;
        }

        /// <summary>
        /// A polyline of the plane of this one's look and data in no file, of vertices of the editing's model: straight (the
        /// flags of fitting and group 75 taken out), closed or not.
        /// </summary>
        private DxfPolyline2d Made(List<DxfPolyVertex> model, bool closed)
        {
            var copy = (DxfPolyline2d)Copy();
            copy.SetGroups(new KeyValuePair<int, string>(70, ((GetInt(70, 0) & ~7) | (closed ? 1 : 0)).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
            copy.SetFollowers(Records(model));
            return copy;
        }

        /// <summary>Gives the polyline vertices of the editing's model, each at the height it has: its vertex records written anew, every group of its own kept.</summary>
        internal void TakeVertices(List<DxfPolyVertex> model) => SetFollowers(Records(model));

        /// <summary>The VERTEX records of vertices of the editing's model, each at the polyline's elevation.</summary>
        internal static List<KeyValuePair<string, KeyValuePair<int, string>[]>> Records(List<DxfPolyVertex> model)
        {
            var records = new List<KeyValuePair<string, KeyValuePair<int, string>[]>>(model.Count + 1);

            foreach (DxfPolyVertex q in model)
            {
                var groups = new List<KeyValuePair<int, string>>
                {
                    new KeyValuePair<int, string>(100, "AcDbVertex"),
                    new KeyValuePair<int, string>(100, "AcDb2dVertex"),
                    new KeyValuePair<int, string>(10, Numbers.Exact(q.X + 0.0)),
                    new KeyValuePair<int, string>(20, Numbers.Exact(q.Y + 0.0)),
                    new KeyValuePair<int, string>(30, Numbers.Exact(q.Z + 0.0)),
                };

                if (q.Widths || q.StartWidth != 0.0 || q.EndWidth != 0.0)
                {
                    groups.Add(new KeyValuePair<int, string>(40, Numbers.Exact(q.StartWidth + 0.0)));
                    groups.Add(new KeyValuePair<int, string>(41, Numbers.Exact(q.EndWidth + 0.0)));
                }

                if (q.Bulge != 0.0)
                {
                    groups.Add(new KeyValuePair<int, string>(42, Numbers.Exact(q.Bulge + 0.0)));
                }

                groups.Add(new KeyValuePair<int, string>(70, "0"));
                records.Add(new KeyValuePair<string, KeyValuePair<int, string>[]>("VERTEX", groups.ToArray()));
            }

            return records;
        }

        /// <summary>Writes the two vertices at each end of the editing's model into their records, in one writing of the polyline.</summary>
        private void WriteEnds(List<DxfPolyVertex> model, List<int> records)
        {
            List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> vertices = DxfFollowers.Vertices(Item);
            var own = new Dictionary<int, IReadOnlyList<DxfTypedValue>>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in vertices)
            {
                own[vertex.Key] = vertex.Value;
            }

            var changes = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();
            int n = model.Count;

            foreach (int i in new[] { 0, 1, n - 2, n - 1 })
            {
                if (i < 0 || i >= n || changes.Exists(c => c.Key == records[i]))
                {
                    continue;
                }

                DxfPolyVertex q = model[i];
                IReadOnlyList<DxfTypedValue> groups = own[records[i]];
                double startWidth = DxfFollowers.Number(groups, 40, DefaultStartWidth);
                double endWidth = DxfFollowers.Number(groups, 41, DefaultEndWidth);
                var change = new List<KeyValuePair<int, string>>(DxfFollowers.PointChanges(groups, 10, new GeoPoint3(q.X, q.Y, 0.0), Origin, 2));
                change.AddRange(DxfFollowers.NumberChange(groups, 42, q.Bulge, 0.0, true));

                if (!Same(q.StartWidth, startWidth) || !Same(q.EndWidth, endWidth))
                {
                    change.AddRange(DxfFollowers.NumberChange(groups, 40, q.StartWidth, double.NaN, false));
                    change.AddRange(DxfFollowers.NumberChange(groups, 41, q.EndWidth, double.NaN, false));
                }

                changes.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(records[i], change.ToArray()));
            }

            SetGroupsAtMany(changes, VertexOrder);
        }

        /// <summary>
        /// Gives the polyline vertices of the editing's model and the data of a polyline: its normal, elevation, thickness,
        /// closure and linetype generation, straight; its vertex records written anew, as a polyline converted from a
        /// <see cref="DxfPolyline"/> takes them.
        /// </summary>
        internal void Take(List<DxfPolyVertex> vertices, bool closed, GeoVector3 normal, double elevation, double thickness, bool generation)
        {
            SetGroups(Join(NormalGroups(normal), PointGroups(10, new GeoPoint3(0.0, 0.0, elevation), Origin)));
            SetThickness(thickness);
            int flags = (GetInt(70, 0) & ~(1 | 2 | 4 | 128)) | (closed ? 1 : 0) | (generation ? 128 : 0);
            SetGroups(new KeyValuePair<int, string>(70, flags.ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(75, null));
            var model = new List<DxfPolyVertex>(vertices.Count);

            foreach (DxfPolyVertex vertex in vertices)
            {
                DxfPolyVertex q = vertex;
                q.Z = elevation;
                model.Add(q);
            }

            SetFollowers(Records(model));
        }

        /// <summary>
        /// The records of a POLYLINE of the plane of vertices of the editing's model and the data of a polyline, as AutoCAD
        /// writes one: its marker, the flag that vertices follow, its point at the elevation, its thickness where not nought, its
        /// flags and its normal where it is not (0, 0, 1); then its vertices and its SEQEND; for a polyline converted to take its
        /// place.
        /// </summary>
        internal static List<KeyValuePair<string, List<DxfTypedValue>>> Records(List<DxfPolyVertex> model, bool closed, GeoVector3 normal, double elevation, double thickness, bool generation)
        {
            var head = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDb2dPolyline"),
                new DxfTypedValue(66, "1"),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, Numbers.Exact(elevation + 0.0)),
            };

            if (thickness != 0.0)
            {
                head.Add(new DxfTypedValue(39, Numbers.Exact(thickness + 0.0)));
            }

            head.Add(new DxfTypedValue(70, ((closed ? 1 : 0) | (generation ? 128 : 0)).ToString(CultureInfo.InvariantCulture)));

            if (!(normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0))
            {
                head.Add(new DxfTypedValue(210, Numbers.Exact(normal.X + 0.0)));
                head.Add(new DxfTypedValue(220, Numbers.Exact(normal.Y + 0.0)));
                head.Add(new DxfTypedValue(230, Numbers.Exact(normal.Z + 0.0)));
            }

            var records = new List<KeyValuePair<string, List<DxfTypedValue>>> { new KeyValuePair<string, List<DxfTypedValue>>("POLYLINE", head) };
            var flat = new List<DxfPolyVertex>(model.Count);

            foreach (DxfPolyVertex vertex in model)
            {
                DxfPolyVertex q = vertex;
                q.Z = elevation;
                flat.Add(q);
            }

            foreach (KeyValuePair<string, KeyValuePair<int, string>[]> vertex in Records(flat))
            {
                var groups = new List<DxfTypedValue>(vertex.Value.Length);

                foreach (KeyValuePair<int, string> group in vertex.Value)
                {
                    groups.Add(new DxfTypedValue(group.Key, group.Value));
                }

                records.Add(new KeyValuePair<string, List<DxfTypedValue>>(vertex.Key, groups));
            }

            records.Add(new KeyValuePair<string, List<DxfTypedValue>>("SEQEND", new List<DxfTypedValue>()));
            return records;
        }

        /// <summary>Puts vertices made in code each after the record of an index (in increasing order), the last first so that the indices hold.</summary>
        private void InsertAfter(List<KeyValuePair<int, DxfVertex2d>> added)
        {
            for (int i = added.Count - 1; i >= 0; i--)
            {
                InsertFollowerAt(added[i].Key + 1, added[i].Value);
            }
        }

        /// <summary>Refuses a frame of fewer than two vertices, which has nothing to fit.</summary>
        private static void Frame(int count)
        {
            if (count < 2)
            {
                throw new InvalidOperationException("A polyline is fitted on two vertices at least, three for a closed one, and this one has too few.");
            }
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

            if (PolyType != DxfPoly2dType.SimplePoly)
            {
                throw new InvalidOperationException("A fitted polyline is drawn through vertices the fitting made: straighten it to extend it.");
            }
        }

        #endregion

        /// <summary>
        /// Gets the lines and arcs the polyline breaks into, as AutoCAD's <c>Explode</c>: a LINE for each straight segment and an
        /// ARC for each bulged one, in its order, as <see cref="DxfReader"/> draws it (one fitted to a curve or a spline through
        /// the vertices it is drawn through), each with its look and thickness; its widths left out, as AutoCAD leaves them out,
        /// and a segment of no length too. Each LINE and ARC is on the polyline's <see cref="Normal"/>, as AutoCAD gives it the
        /// polyline's, so that its wall stands up the same way.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            // The vertices in the world, as VertexPosition gives each.
            GeoVector3 normal = Normal;
            var vertices = new List<GeoPoint3>();

            foreach (DxfVertex2d vertex in this)
            {
                vertices.Add(OcsToWorld(normal, vertex.Position));
            }

            return ChainParts(normal, vertices).AsReadOnly();
        }

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
