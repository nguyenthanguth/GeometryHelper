using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Export.DxfLeaderGroups;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A LEADER of a DXF file, as AutoCAD's <c>Leader</c>: a line through its vertices in the world, with an arrow at its first,
    /// drawn straight or as a spline, that points to an MTEXT, a TOLERANCE or an INSERT (<see cref="Annotation"/>), drawn by its
    /// dimension style with the overrides of its extended data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leader through (0, 0, 0), (10, 10, 0) and (20, 10, 0) has three vertices, <see cref="FirstVertex"/> where its arrow
    /// is and <see cref="LastVertex"/> where its words are; as a curve its parameter is AutoCAD's, the index of a vertex plus
    /// how far along the segment it leaves, so that 1.5 is (15, 10, 0), and its length is 24.14.
    /// </para>
    /// <para>
    /// The size of its arrow, the gap to its words and the other variables of its dimension style AutoCAD lets one leader have
    /// of its own are read from its overrides where it has them, the extended data of ACAD between <c>DSTYLE</c> and its
    /// braces, as AutoCAD and ezdxf write them, and from its dimension style where it has not: <see cref="Dimasz"/> 2.5 of the
    /// style Standard until it is set to 3.5, which writes the override <c>1070 41, 1040 3.5</c>. A variable a style gives no
    /// value of is AutoCAD's own default, the one of its imperial drawing (DIMASZ 0.18).
    /// </para>
    /// <para>
    /// Each property is written into the LEADER's own groups, every other group keeping its text; a property given the value
    /// it has changes nothing. AutoCAD draws a leader anew from its vertices and its annotation (<c>EvaluateLeader</c>): what it
    /// draws is not in the file, and the library writes the data it is drawn from. A file older than R13 has no LEADER, and
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> refuses one there.
    /// </para>
    /// </remarks>
    public sealed class DxfLeader : DxfCurve
    {
        // The groups of AcDbLeader in the order AutoCAD writes them, a vertex being 10, 20 and 30.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[]
        {
            100, 3, 71, 72, 73, 74, 75, 40, 41, 76, 10, 20, 30, 77, 340, 210, 220, 230, 211, 221, 231, 212, 222, 232, 213, 223, 233,
        });

        private static readonly IReadOnlyCollection<int> VertexCodes = Array.AsReadOnly(new[] { 10, 20, 30 });

        private static readonly GeoVector3 Nought = new GeoVector3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a LEADER of no vertices with an arrow and no annotation, in the dimension style Standard, as AutoCAD's
        /// <c>new Leader()</c>, on layer 0 in no file; give it its vertices with <see cref="AppendVertex"/>, two at least.
        /// </summary>
        public DxfLeader()
            : base("LEADER", new[]
            {
                new DxfTypedValue(100, "AcDbLeader"), new DxfTypedValue(3, "Standard"), new DxfTypedValue(71, "1"), new DxfTypedValue(72, "0"),
                new DxfTypedValue(73, "3"), new DxfTypedValue(74, "1"), new DxfTypedValue(75, "0"), new DxfTypedValue(76, "0"),
            })
        {
        }

        private DxfLeader(DxfItem item)
            : base(item)
        {
        }

        [DxfType("LEADER")]
        internal static DxfEntity Create(DxfItem item) => new DxfLeader(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>
        /// Gets the plane of the leader, as AutoCAD's <c>GetPlane</c>: that of its <see cref="Normal"/> through its first vertex,
        /// where every vertex lies in it to <see cref="Tolerance.Default"/>.
        /// </summary>
        /// <returns>The plane.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex, or a vertex lies off that plane, as
        /// AutoCAD answers eNotPlanar.</exception>
        public override GeoPlane3 GetPlane()
        {
            if (NumVertices == 0)
            {
                throw new InvalidOperationException("The leader has no vertex: it has no plane.");
            }

            var plane = new GeoPlane3(VertexAt(0), Normal);

            for (int i = 1; i < NumVertices; i++)
            {
                if (Math.Abs(plane.SignedDistanceTo(VertexAt(i))) > Tolerance.Default.EqualPoint)
                {
                    throw new InvalidOperationException("The vertex " + i.ToString(CultureInfo.InvariantCulture) + " of the leader lies off the plane of its normal through its first, as AutoCAD answers eNotPlanar.");
                }
            }

            return plane;
        }

        /// <summary>
        /// Gets the box the leader's vertices fill, as AutoCAD's <c>GeometricExtents</c> of a straight leader, its arrowhead not
        /// counted: a leader from (0, 0, 0) to (10, 5, 0) gives the box from the one to the other.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex, or is splined, whose curve AutoCAD works
        /// out when it draws it and the library does not read.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                if (NumVertices == 0 || IsSplined)
                {
                    throw new InvalidOperationException(NumVertices == 0 ? "The leader has no vertex: it has no extents." : "The leader is splined: its curve is AutoCAD's to work out, and its extents with it.");
                }

                GeoPoint3 first = VertexAt(0);
                double[] box = { first.X, first.Y, first.Z, first.X, first.Y, first.Z };

                for (int i = 1; i < NumVertices; i++)
                {
                    GeoPoint3 p = VertexAt(i);
                    box[0] = Math.Min(box[0], p.X);
                    box[1] = Math.Min(box[1], p.Y);
                    box[2] = Math.Min(box[2], p.Z);
                    box[3] = Math.Max(box[3], p.X);
                    box[4] = Math.Max(box[4], p.Y);
                    box[5] = Math.Max(box[5], p.Z);
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        /// <summary>Refuses a leader of fewer than two vertices, which AutoCAD does not take into a drawing.</summary>
        /// <exception cref="ArgumentException">Thrown when the leader has fewer than two vertices.</exception>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (NumVertices < 2)
            {
                throw new ArgumentException("A leader has two vertices at least, as AutoCAD takes one, and this one has " + NumVertices.ToString(CultureInfo.InvariantCulture) + ": give it them by AppendVertex first.", "entity");
            }
        }

        #region The vertices

        /// <summary>Gets how many vertices the leader has, as AutoCAD's <c>NumVertices</c>: its groups 10.</summary>
        public int NumVertices => Vertices().Count;

        /// <summary>Gets a vertex in the world (groups 10, 20 and 30), as AutoCAD's <c>VertexAt</c>.</summary>
        /// <param name="value">The index of the vertex, from nought.</param>
        /// <returns>The vertex.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the leader has no vertex of the index.</exception>
        public GeoPoint3 VertexAt(int value)
        {
            List<GeoPoint3> vertices = Vertices();
            Index(vertices, value, nameof(value));
            return vertices[value];
        }

        /// <summary>Gets the first vertex, where the arrow is, as AutoCAD's <c>FirstVertex</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex.</exception>
        public GeoPoint3 FirstVertex => Ends(true);

        /// <summary>Gets the last vertex, by the annotation, as AutoCAD's <c>LastVertex</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex.</exception>
        public GeoPoint3 LastVertex => Ends(false);

        /// <summary>
        /// Adds a vertex after the last, as AutoCAD's <c>AppendVertex</c>: written as <see cref="DxfWriter"/> writes a number,
        /// with the count of the vertices (76).
        /// </summary>
        /// <param name="pointToAdd">The vertex, in the world.</param>
        /// <returns>True, as AutoCAD answers for a vertex added.</returns>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the leader is erased.</exception>
        public bool AppendVertex(GeoPoint3 pointToAdd)
        {
            Valid(pointToAdd, nameof(pointToAdd));
            List<GeoPoint3> vertices = Vertices();
            vertices.Add(pointToAdd);
            WriteVertices(vertices, -1);
            return true;
        }

        /// <summary>Moves a vertex, as AutoCAD's <c>SetVertexAt</c>: its three groups written anew where a coordinate differs, the others kept.</summary>
        /// <param name="index">The index of the vertex, from nought.</param>
        /// <param name="pointValue">Where it is to be, in the world.</param>
        /// <returns>True, as AutoCAD answers for a vertex moved.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the leader has no vertex of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the leader is erased.</exception>
        public bool SetVertexAt(int index, GeoPoint3 pointValue)
        {
            Valid(pointValue, nameof(pointValue));
            List<GeoPoint3> vertices = Vertices();
            Index(vertices, index, nameof(index));

            if (!SamePoint(vertices[index], pointValue))
            {
                vertices[index] = pointValue;
                WriteVertices(vertices, index);
            }

            return true;
        }

        /// <summary>
        /// Takes the last vertex out, as AutoCAD's <c>RemoveLastVertex</c>. A leader keeps two vertices, as AutoCAD keeps one: the
        /// vertex is not taken out where fewer would be left.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has two vertices or fewer, or is erased.</exception>
        public void RemoveLastVertex()
        {
            List<GeoPoint3> vertices = Vertices();

            if (vertices.Count <= 2)
            {
                throw new InvalidOperationException("A leader keeps two vertices at least, and this one has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ": erase the leader instead.");
            }

            vertices.RemoveAt(vertices.Count - 1);
            WriteVertices(vertices, -1);
        }

        #endregion

        #region The leader

        /// <summary>Gets or sets whether an arrow is drawn at the first vertex (group 71), as AutoCAD's <c>HasArrowHead</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public bool HasArrowHead
        {
            get => GetInt(71, 1) != 0;
            set => Flag(71, value, 1);
        }

        /// <summary>
        /// Gets whether a short line runs on from the last vertex to the annotation (group 75), as AutoCAD's <c>HasHookLine</c>;
        /// true where the record gives none, as ezdxf reads one, which leaves the group out when it is 1.
        /// </summary>
        public bool HasHookLine => GetInt(75, 1) != 0;

        /// <summary>Gets or sets whether the leader is drawn as a spline through its vertices (group 72 of 1), as AutoCAD's <c>IsSplined</c>, rather than straight.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public bool IsSplined
        {
            get => GetInt(72, 0) != 0;
            set => Flag(72, value, 0);
        }

        /// <summary>Gets the normal of the plane of the leader (groups 210, 220, 230), as AutoCAD's <c>Normal</c>: of unit length, (0, 0, 1) where the record gives none.</summary>
        public GeoVector3 Normal => GetNormal();

        /// <summary>Gets what the leader points to (group 73), as AutoCAD's <c>AnnoType</c>; <see cref="DxfAnnotationType.NoAnnotation"/> where the record gives none.</summary>
        public DxfAnnotationType AnnoType => (DxfAnnotationType)GetInt(73, 3);

        /// <summary>Gets the height of the words the leader points to (group 40) as the file writes it, as AutoCAD's <c>AnnoHeight</c>; nought where it gives none.</summary>
        public double AnnoHeight => GetDouble(40, 0.0);

        /// <summary>Gets the width of the words the leader points to (group 41) as the file writes it, as AutoCAD's <c>AnnoWidth</c>; nought where it gives none.</summary>
        public double AnnoWidth => GetDouble(41, 0.0);

        /// <summary>
        /// Gets or sets what the leader points to, as AutoCAD's <c>Annotation</c> gives its id: the MTEXT, TOLERANCE or INSERT of
        /// its handle (group 340) in the drawing; null where it points to none, or to a handle the drawing has no entity of.
        /// </summary>
        /// <value>
        /// Set, an entity of the same drawing, an MTEXT (a <see cref="DxfMText"/>), a TOLERANCE or an INSERT (a
        /// <see cref="DxfBlockReference"/>): its handle written in group 340 and what it is in group 73; null takes the handle out
        /// and makes group 73 that of no annotation. The leader is put among the reactors of the annotation (102
        /// <c>{ACAD_REACTORS</c>), and taken out of those of the one it pointed to before, as AutoCAD keeps it.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the entity set is not an MTEXT, a TOLERANCE or an INSERT, or is not of
        /// the leader's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public DxfEntity Annotation
        {
            get
            {
                string handle = GetString(340)?.Trim();
                return handle == null || handle == "0" ? null : EntityByHandle(handle);
            }

            set
            {
                DxfEntity was = Annotation;

                if (ReferenceEquals(value, was))
                {
                    return;
                }

                if (value == null)
                {
                    SetGroups(new KeyValuePair<int, string>(340, null), new KeyValuePair<int, string>(73, "3"));
                    React(was, false);
                    return;
                }

                int type = value is DxfMText ? 0 : value.Item.Type == "TOLERANCE" ? 1 : value is DxfBlockReference ? 2 : -1;

                if (type < 0)
                {
                    throw new ArgumentException("A leader points to an MTEXT, a TOLERANCE or an INSERT, and was given a " + value.Item.Type + ".", nameof(value));
                }

                if (!value.IsResident || value.IsErased || value.Item.File != Item.File)
                {
                    throw new ArgumentException("A leader points to an entity of its own drawing: the " + value.Item.Type + " is not of it.", nameof(value));
                }

                SetGroups(new KeyValuePair<int, string>(340, value.Handle), new KeyValuePair<int, string>(73, type.ToString(CultureInfo.InvariantCulture)));
                React(was, false);
                React(value, true);
            }
        }

        /// <summary>Puts the leader among the reactors of an annotation (102 <c>{ACAD_REACTORS</c>), or takes it out, as AutoCAD keeps it there; nothing for none.</summary>
        private void React(DxfEntity annotation, bool present)
        {
            if (annotation != null && IsResident && annotation.IsResident && !annotation.IsErased && annotation.Item.File == Item.File)
            {
                Item.File.SetReactor(annotation.Item, Handle, present);
            }
        }

        /// <summary>
        /// Gets or sets how far the last vertex stands off the point the annotation is placed at (groups 213, 223, 233), as
        /// AutoCAD's <c>AnnotationOffset</c>, in the world; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public GeoVector3 AnnotationOffset
        {
            get => GetPoint(213, new GeoPoint3(0.0, 0.0, 0.0)).ToVector();
            set => SetPoint(213, new GeoPoint3(value.X, value.Y, value.Z), new GeoPoint3(0.0, 0.0, 0.0));
        }

        /// <summary>
        /// Gets or sets the name of the dimension style the leader is drawn by (group 3), as AutoCAD's <c>DimensionStyleName</c>;
        /// <c>Standard</c> where the record gives none.
        /// </summary>
        /// <value>Set, the name of a style of the drawing's DIMSTYLE table, letters of either case the same, as the table names it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no dimension style of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public string DimensionStyleName
        {
            get
            {
                string name = DxfReader.NameOf(GetString(3) ?? string.Empty);
                return name.Length == 0 ? "Standard" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, DimensionStyleName, StringComparison.Ordinal))
                {
                    return;
                }

                string name = value.Trim();
                DxfItem style = Style(name);

                if (style == null && IsResident)
                {
                    throw new ArgumentException("The drawing has no dimension style named " + name + ".", nameof(value));
                }

                SetString(3, style == null ? name : DxfReader.NameOf(style.File.ValueOf(style, 2) ?? name));
            }
        }

        /// <summary>
        /// Gets or sets the dimension style the leader is drawn by, as AutoCAD's <c>DimensionStyle</c> gives its id: by its name,
        /// as <see cref="DimensionStyleName"/> reads and writes it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no dimension style of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public string DimensionStyle
        {
            get => DimensionStyleName;
            set => DimensionStyleName = value;
        }

        #endregion

        #region The overrides of the dimension style

        /// <summary>Gets or sets the size of the arrow (DIMASZ, 41), as AutoCAD's <c>Dimasz</c>: the leader's override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public double Dimasz
        {
            get => OverrideReal(41, 0.18);
            set => SetReal(41, value, true);
        }

        /// <summary>Gets or sets the gap between the leader's hook and its words (DIMGAP, 147), as AutoCAD's <c>Dimgap</c>: the override, else the style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public double Dimgap
        {
            get => OverrideReal(147, 0.09);
            set => SetReal(147, value, false);
        }

        /// <summary>Gets or sets the scale of the sizes of the style (DIMSCALE, 40), as AutoCAD's <c>Dimscale</c>: the override, else the style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public double Dimscale
        {
            get => OverrideReal(40, 1.0);
            set => SetReal(40, value, true);
        }

        /// <summary>Gets or sets the height of the words (DIMTXT, 140), as AutoCAD's <c>Dimtxt</c>: the override, else the style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public double Dimtxt
        {
            get => OverrideReal(140, 0.18);
            set => SetReal(140, value, true);
        }

        /// <summary>Gets or sets where the words stand against the hook (DIMTAD, 77), as AutoCAD's <c>Dimtad</c>: 0 centred, 1 above; the override, else the style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public int Dimtad
        {
            get => OverrideInteger(77, 0);
            set => SetOverrideInteger(77, value);
        }

        /// <summary>Gets or sets whether the arrows are blocks of their own (DIMSAH, 173), as AutoCAD's <c>Dimsah</c>: the override, else the style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public bool Dimsah
        {
            get => OverrideInteger(173, 0) != 0;
            set => SetOverrideInteger(173, value ? 1 : 0);
        }

        /// <summary>Gets or sets the lineweight of the leader (DIMLWD, 371), as AutoCAD's <c>Dimlwd</c>: the override, else the style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public DxfLineWeight Dimlwd
        {
            get => (DxfLineWeight)OverrideInteger(371, -2);
            set => SetOverrideInteger(371, (int)value);
        }

        /// <summary>
        /// Gets or sets the colour of the leader (DIMCLRD, 176), as AutoCAD's <c>Dimclrd</c>: a colour of the index, BYBLOCK or
        /// BYLAYER; the override, else the style's.
        /// </summary>
        /// <value>Set, BYLAYER, BYBLOCK or a colour of the index, written as its index; a true colour, which the variable does not hold, is refused.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is not BYLAYER, BYBLOCK or one of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a leader that is erased.</exception>
        public DxfColor Dimclrd
        {
            get => DxfColor.OfIndex(OverrideInteger(176, 0));

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!value.IsByAci && !value.IsByBlock && !value.IsByLayer)
                {
                    throw new ArgumentException("DIMCLRD holds a colour of AutoCAD's index, BYBLOCK or BYLAYER: " + value + " is none of them.", nameof(value));
                }

                SetOverrideInteger(176, value.ColorIndex);
            }
        }

        /// <summary>
        /// Gets or sets the block drawn as the arrow (DIMLDRBLK, 341), as AutoCAD's <c>Dimldrblk</c> gives its id: the block of the
        /// override's handle (1005), else of the style's; null for AutoCAD's closed filled arrow, which needs no block.
        /// </summary>
        /// <value>Set, a block of the leader's drawing, its handle written as the override; null takes the override out, the arrow
        /// then the style's, which has to be the closed filled one.</value>
        /// <exception cref="ArgumentException">Thrown when the block set is not of the leader's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when null is set on a leader whose style has a block of its own, or
        /// the leader is erased or in no drawing.</exception>
        public DxfBlockTableRecord Dimldrblk
        {
            get
            {
                List<DxfTypedValue> pair = Override(341);
                string handle = pair != null ? pair[1].Value : StyleValue(341);
                return handle == null ? null : BlockByHandle(handle.Trim());
            }

            set
            {
                if (ReferenceEquals(value, Dimldrblk))
                {
                    return;
                }

                if (value == null)
                {
                    string own = StyleValue(341);

                    if (own != null && BlockByHandle(own.Trim()) != null)
                    {
                        throw new InvalidOperationException("The leader's dimension style draws its arrow by a block: the closed filled arrow is set on the style.");
                    }

                    WriteOverride(341, null);
                    return;
                }

                if (!IsResident || value.Handle == null || Database()?.BlockTable == null || BlockByHandle(value.Handle) != value)
                {
                    throw new ArgumentException("The arrow of a leader is a block of its own drawing.", nameof(value));
                }

                WriteOverride(341, new DxfTypedValue(1005, value.Handle));
            }
        }

        #endregion

        #region The curve

        /// <summary>Gets the parameter at the last vertex: the count of the segments, as AutoCAD's.</summary>
        public override double EndParam => Chain().EndParam;

        /// <summary>Gets the first vertex, as AutoCAD's <c>StartPoint</c> of a leader.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex.</exception>
        public override GeoPoint3 StartPoint => FirstVertex;

        /// <summary>Gets the last vertex, as AutoCAD's <c>EndPoint</c> of a leader.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex.</exception>
        public override GeoPoint3 EndPoint => LastVertex;

        /// <summary>Gets false: a leader does not close, as AutoCAD's does not.</summary>
        public override bool Closed => false;

        /// <summary>Gets the point at a parameter, the index of a vertex plus how far along the segment it leaves, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline, which AutoCAD fits through the vertices by its own rule.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.PointAt(value);
        }

        /// <summary>Gets the parameter at a point of the leader, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the leader.</param>
        /// <returns>The parameter: the first segment's where two meet at the point.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the leader.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point) => ParameterOn(Chain(), point);

        /// <summary>Gets the distance along the leader from its first vertex to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.DistanceAt(value);
        }

        /// <summary>Gets the parameter at a distance along the leader from its first vertex, as AutoCAD's <c>GetParameterAtDistance</c>.</summary>
        /// <param name="dist">The distance, from nought to the leader's length.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            DxfBulgeChain chain = Chain();
            Within(dist, chain.Length, nameof(dist));
            return chain.ParameterAt(dist);
        }

        /// <summary>Gets the distance along the leader from its first vertex to a point of it, as AutoCAD's <c>GetDistAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the leader.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the leader.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override double GetDistAtPoint(GeoPoint3 point)
        {
            DxfBulgeChain chain = Chain();
            return chain.DistanceAt(ParameterOn(chain, point));
        }

        /// <summary>Gets the point at a distance along the leader from its first vertex, as AutoCAD's <c>GetPointAtDist</c>.</summary>
        /// <param name="value">The distance, from nought to the leader's length.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.Length, nameof(value));
            return chain.PointAt(chain.ParameterAt(value));
        }

        /// <summary>Gets the point of the leader nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: the first segment's where two are as near.</summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Not read: the leader is taken as it is.</param>
        /// <returns>The nearest point, in the world.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the leader has no vertex.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            Chain().ClosestParameter(givenPoint, out GeoPoint3 closest);
            return closest;
        }

        /// <summary>Gets the derivative of the point by the parameter, as AutoCAD's <c>GetFirstDerivative</c>: on a segment the vector from its start to its end.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>The derivative, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.FirstDerivative(value);
        }

        /// <summary>Gets the second derivative of the point by the parameter, as AutoCAD's <c>GetSecondDerivative</c>: nought on the segments.</summary>
        /// <param name="value">The parameter, from nought to <see cref="EndParam"/>.</param>
        /// <returns>Nought.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            DxfBulgeChain chain = Chain();
            Within(value, chain.EndParam, nameof(value));
            return chain.SecondDerivative(value);
        }

        /// <summary>
        /// Gets the spline that is exactly the leader, as AutoCAD's <c>Spline</c>: of degree 1 through its vertices, a knot at each
        /// step of its parameter.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the leader has fewer than two vertices.</exception>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline, which AutoCAD fits by its own rule.</exception>
        public override DxfSpline Spline
        {
            get
            {
                Straight();
                List<GeoPoint3> vertices = Vertices();

                if (vertices.Count < 2)
                {
                    throw new InvalidOperationException("A leader of fewer than two vertices has no spline.");
                }

                var knots = new List<double> { 0.0 };

                for (int i = 0; i < vertices.Count; i++)
                {
                    knots.Add(i);
                }

                knots.Add(vertices.Count - 1);
                return new DxfSpline(1, false, false, false, vertices, knots, null, 1E-10, 1E-10);
            }
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the leader, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each vertex moved,
        /// the normal carried as the plane's axes are, the direction of the hook turned; and, past ezdxf, the offsets of the last
        /// vertex from the annotation (212 and 213) turned and scaled as AutoCAD turns them. The annotation is an entity of its
        /// own, moved by its own <see cref="DxfEntity.TransformBy"/>.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentException">Thrown when it takes the plane of the leader to no plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the leader is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            List<GeoPoint3> vertices = Vertices();

            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = transform.Transform(vertices[i]);
            }

            var head = new List<KeyValuePair<int, string>>();
            head.AddRange(NormalGroups(new DxfOcsTransform(GetNormal(), transform).NewExtrusion));

            foreach (int code in new[] { 211, 212, 213 })
            {
                // The direction of the hook is (1, 0, 0) where the record gives none, and is turned as ezdxf turns it; the offsets only where it has them.
                if (code == 211 || GetString(code) != null || GetString(code + 10) != null || GetString(code + 20) != null)
                {
                    var fallback = new GeoPoint3(code == 211 ? 1.0 : 0.0, 0.0, 0.0);
                    GeoPoint3 v = GetPoint(code, fallback);
                    GeoVector3 turned = transform.Transform(new GeoVector3(v.X, v.Y, v.Z));
                    head.AddRange(PointGroups(code, new GeoPoint3(turned.X, turned.Y, turned.Z), fallback));
                }
            }

            WriteVertices(vertices, -1, head.ToArray());
        }

        #endregion

        #region The record

        /// <summary>The vertices, in the world.</summary>
        private List<GeoPoint3> Vertices()
        {
            var vertices = new List<GeoPoint3>();
            double[] point = null;

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                switch (group.TypeCode)
                {
                    case 10:
                        point = new[] { Num(group.Value, 0.0), 0.0, 0.0 };
                        vertices.Add(default(GeoPoint3));
                        break;
                    case 20:
                    case 30:
                        if (point == null)
                        {
                            continue;
                        }

                        point[group.TypeCode / 10 - 1] = Num(group.Value, 0.0);
                        break;
                    default:
                        continue;
                }

                vertices[vertices.Count - 1] = new GeoPoint3(point[0], point[1], point[2]);
            }

            return vertices;
        }

        /// <summary>
        /// Writes the vertices and their count in one writing, with other groups of the head: the vertex of an index alone where
        /// only it changes, the run of every vertex otherwise.
        /// </summary>
        private void WriteVertices(List<GeoPoint3> vertices, int changed, params KeyValuePair<int, string>[] head)
        {
            var run = new List<KeyValuePair<int, string>>();
            List<KeyValuePair<int, string>> read = VertexGroups();
            int index = -1;
            List<KeyValuePair<int, string>> current = null;
            var old = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in read)
            {
                if (group.Key == 10)
                {
                    current = new List<KeyValuePair<int, string>>();
                    old.Add(current);
                    index++;
                }

                current?.Add(group);
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                GeoPoint3 now = vertices[i];

                // A vertex as it was, to the bit, keeps the text of its groups.
                if (i < old.Count && (changed < 0 || i != changed) && SamePoint(Point(old[i]), now))
                {
                    run.AddRange(old[i]);
                    continue;
                }

                run.Add(G(10, now.X));
                run.Add(G(20, now.Y));
                run.Add(G(30, now.Z));
            }

            KeyValuePair<int, string>[] count = vertices.Count == GetInt(76, -1) ? new KeyValuePair<int, string>[0] : new[] { G(76, vertices.Count) };
            KeyValuePair<int, string>[] groups = Join(head, count);

            if (groups.Length > 0)
            {
                SetGroups(groups);
            }

            ReplaceGroups(0, VertexCodes, run.ToArray());
        }

        /// <summary>The groups 10, 20 and 30 of the own part, in their order.</summary>
        private List<KeyValuePair<int, string>> VertexGroups()
        {
            var groups = new List<KeyValuePair<int, string>>();

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == 10 || group.TypeCode == 20 || group.TypeCode == 30)
                {
                    groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            return groups;
        }

        private static GeoPoint3 Point(List<KeyValuePair<int, string>> vertex)
            => new GeoPoint3(Num(Text(vertex, 10), 0.0), Num(Text(vertex, 20), 0.0), Num(Text(vertex, 30), 0.0));

        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        private GeoPoint3 Ends(bool first)
        {
            List<GeoPoint3> vertices = Vertices();

            if (vertices.Count == 0)
            {
                throw new InvalidOperationException("The leader has no vertex.");
            }

            return first ? vertices[0] : vertices[vertices.Count - 1];
        }

        private static void Index(List<GeoPoint3> vertices, int index, string parameter)
        {
            if (index < 0 || index >= vertices.Count)
            {
                throw new ArgumentOutOfRangeException(parameter, index, "The leader has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + " vertices, from nought.");
            }
        }

        private static void Valid(GeoPoint3 point, string parameter)
        {
            if (!point.IsValid)
            {
                throw new ArgumentException("A vertex has to be of finite numbers: " + point + " is not.", parameter);
            }
        }

        private void Flag(int code, bool value, int fallback)
        {
            if (value != (GetInt(code, fallback) != 0))
            {
                SetString(code, value ? "1" : "0");
            }
        }

        /// <summary>Refuses a leader drawn as a spline, whose curve AutoCAD fits by its own rule.</summary>
        private void Straight()
        {
            if (IsSplined)
            {
                throw new NotSupportedException("A leader drawn as a spline is fitted through its vertices by AutoCAD's own rule, which the file does not hold.");
            }
        }

        /// <summary>The chain of the vertices.</summary>
        /// <exception cref="NotSupportedException">Thrown for a leader drawn as a spline.</exception>
        private DxfBulgeChain Chain()
        {
            Straight();

            if (NumVertices == 0)
            {
                throw new InvalidOperationException("The leader has no vertex: it is no curve to walk.");
            }

            return new DxfBulgeChain(new GeoVector3(0.0, 0.0, 1.0), new GeoVector3(1.0, 0.0, 0.0), new GeoVector3(0.0, 1.0, 0.0), Vertices(), null, false);
        }

        private static double ParameterOn(DxfBulgeChain chain, GeoPoint3 point)
        {
            double parameter = chain.ClosestParameter(point, out GeoPoint3 closest);

            if (!closest.IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the leader.", nameof(point));
            }

            return parameter;
        }

        private DxfDatabase Database() => IsResident ? Item.File.Database : null;

        /// <summary>The entity of a handle anywhere in the drawing; null where it has none.</summary>
        private DxfEntity EntityByHandle(string handle)
        {
            DxfDatabase database = Database();

            if (database == null)
            {
                return null;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                DxfEntity entity = block.EntityByHandle(handle);

                if (entity != null)
                {
                    return entity;
                }
            }

            return null;
        }

        /// <summary>The block of the handle of its BLOCK_RECORD; null where the drawing has none, or the handle is nought.</summary>
        private DxfBlockTableRecord BlockByHandle(string handle)
        {
            DxfDatabase database = Database();

            if (database == null || handle.Length == 0 || handle == "0")
            {
                return null;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (string.Equals(block.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return block;
                }
            }

            return null;
        }

        /// <summary>The DIMSTYLE of a name, letters of either case the same; null where the leader is in no drawing or it has none.</summary>
        private DxfItem Style(string name)
        {
            if (!IsResident)
            {
                return null;
            }

            foreach (DxfItem item in Item.File.TableItems("DIMSTYLE"))
            {
                if (!item.IsRemoved && string.Equals(DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>The text of a group of the leader's dimension style; null where it has none, or the style is not in the drawing.</summary>
        private string StyleValue(int code)
        {
            DxfItem style = Style(DimensionStyleName);
            return style?.File.ValueOf(style, code);
        }

        /// <summary>The override of a variable: its 1070 of the code and the group of its value; null where the leader has none.</summary>
        private List<DxfTypedValue> Override(int code)
        {
            List<DxfTypedValue> data = Acad(out int open, out int close);

            for (int i = open + 1; open >= 0 && i + 1 < close; i += 2)
            {
                if (data[i].TypeCode == 1070 && Whole(data[i].Value, int.MinValue) == code)
                {
                    return new List<DxfTypedValue> { data[i], data[i + 1] };
                }
            }

            return null;
        }

        /// <summary>The extended data of ACAD after its 1001, and where the braces of DSTYLE stand in it; -1 for each where there are none.</summary>
        private List<DxfTypedValue> Acad(out int open, out int close)
        {
            IReadOnlyList<DxfTypedValue> acad = GetXDataForApplication("ACAD");
            var data = new List<DxfTypedValue>();

            for (int i = 1; acad != null && i < acad.Count; i++)
            {
                data.Add(acad[i]);
            }

            open = -1;
            close = -1;

            for (int i = 0; i + 1 < data.Count; i++)
            {
                if (data[i].TypeCode == 1000 && string.Equals(data[i].Value.Trim(), "DSTYLE", StringComparison.OrdinalIgnoreCase) && data[i + 1].TypeCode == 1002 && data[i + 1].Value.Trim() == "{")
                {
                    open = i + 1;
                    close = data.FindIndex(open + 1, g => g.TypeCode == 1002 && g.Value.Trim() == "}");
                    close = close < 0 ? data.Count : close;
                    break;
                }
            }

            return data;
        }

        /// <summary>
        /// Writes the override of a variable: its value in place where the leader has one, else after the others before the
        /// closing brace, DSTYLE and its braces made where there are none, as AutoCAD writes them; null takes it out, and DSTYLE
        /// with it where nothing is left. The rest of the extended data of ACAD is kept.
        /// </summary>
        private void WriteOverride(int code, DxfTypedValue? value)
        {
            List<DxfTypedValue> data = Acad(out int open, out int close);
            int at = -1;

            for (int i = open + 1; open >= 0 && i + 1 < close; i += 2)
            {
                if (data[i].TypeCode == 1070 && Whole(data[i].Value, int.MinValue) == code)
                {
                    at = i;
                    break;
                }
            }

            if (value == null)
            {
                if (at < 0)
                {
                    return;
                }

                data.RemoveRange(at, 2);
                close -= 2;

                if (close == open + 1)
                {
                    data.RemoveRange(open - 1, 3);
                }
            }
            else if (at >= 0)
            {
                data[at + 1] = value.Value;
            }
            else if (open >= 0)
            {
                data.Insert(close, value.Value);
                data.Insert(close, new DxfTypedValue(1070, code.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                data.Add(new DxfTypedValue(1000, "DSTYLE"));
                data.Add(new DxfTypedValue(1002, "{"));
                data.Add(new DxfTypedValue(1070, code.ToString(CultureInfo.InvariantCulture)));
                data.Add(value.Value);
                data.Add(new DxfTypedValue(1002, "}"));
            }

            var groups = new List<DxfTypedValue> { new DxfTypedValue(1001, "ACAD") };
            groups.AddRange(data);
            XData = groups;
        }

        private double OverrideReal(int code, double fallback)
        {
            List<DxfTypedValue> pair = Override(code);
            return Num(pair != null ? pair[1].Value : StyleValue(code), fallback);
        }

        private int OverrideInteger(int code, int fallback)
        {
            List<DxfTypedValue> pair = Override(code);
            return Whole(pair != null ? pair[1].Value : StyleValue(code), fallback);
        }

        private void SetReal(int code, double value, bool nonNegative)
        {
            Guard.Finite(value, nameof(value), "The variable has to be a finite number.");

            if (Same(value, OverrideReal(code, double.NaN)) || (Override(code) == null && StyleValue(code) == null && Same(value, DefaultOf(code))))
            {
                return;
            }

            if (nonNegative)
            {
                Guard.NonNegative(value, nameof(value), "The variable has to be a finite number, nought or more.");
            }

            WriteOverride(code, new DxfTypedValue(1040, Real(value)));
        }

        private void SetOverrideInteger(int code, int value)
        {
            if (value == OverrideInteger(code, int.MinValue) || (Override(code) == null && StyleValue(code) == null && value == (int)DefaultOf(code)))
            {
                return;
            }

            WriteOverride(code, new DxfTypedValue(1070, value.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>AutoCAD's own value of a variable a style gives none of: that of its imperial drawing.</summary>
        private static double DefaultOf(int code)
        {
            switch (code)
            {
                case 41: return 0.18;
                case 147: return 0.09;
                case 40: return 1.0;
                case 140: return 0.18;
                case 371: return -2;
                default: return 0;
            }
        }

        #endregion
    }
}
