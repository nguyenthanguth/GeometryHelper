using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One dynamic property of an INSERT of a dynamic block, as AutoCAD's <c>DynamicBlockReferenceProperty</c> gives it: the
    /// name a parameter of the block shows it by, its value for this INSERT as AutoCAD saved it, and the values it may take.
    /// The value of a linear, XY, point, rotation, flip or visibility parameter, in a block of the actions the library writes,
    /// is set as AutoCAD sets it (<see cref="Value"/>); the others are read only.
    /// </summary>
    /// <remarks>
    /// The INSERT of a door whose linear parameter Width AutoCAD stretched from 900 to 1 000 gives the property Width, of the
    /// value 1 000.0 (a <see cref="double"/>), <see cref="DxfDynamicBlockReferencePropertyUnitsType.Distance"/>, and
    /// <see cref="GetAllowedValues"/> 800, 900 and 1 000 where the parameter keeps that list. See
    /// <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/> for what each kind of parameter gives.
    /// </remarks>
    public sealed class DxfDynamicBlockReferenceProperty
    {
        private readonly object[] _allowed;
        private object _value;

        // The INSERT, the id of the parameter's node and which of its values the property is (0, or 1 for the y of a point or an
        // XY parameter), for a property whose value is set; null for one that is read only.
        private DxfBlockReference _insert;
        private string _node;
        private int _part;

        /// <summary>A property read from a parameter, its allowed values kept as given.</summary>
        internal DxfDynamicBlockReferenceProperty(string name, string description, object value, short typeCode, DxfDynamicBlockReferencePropertyUnitsType units, bool readOnly, object[] allowed)
        {
            PropertyName = name;
            Description = description;
            _value = value;
            PropertyTypeCode = typeCode;
            UnitsType = units;
            ReadOnly = readOnly;
            _allowed = allowed;
        }

        /// <summary>
        /// Gets the name the property is shown by, as AutoCAD's <c>PropertyName</c>: the label of its parameter, Width for a
        /// linear parameter labelled so; a point parameter's label with " X" and " Y" after it, Anchor X and Anchor Y.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets or sets the property's value for this INSERT, as AutoCAD's <c>Value</c>: a <see cref="double"/> for a length or
        /// an angle (in radians), a <see cref="short"/> for the state of a flip (0 not flipped, 1 flipped), a <see cref="string"/>
        /// for a visibility state or a value looked up.
        /// </summary>
        /// <value>
        /// <para>
        /// Set, as AutoCAD's <c>Value</c> sets it, on the distance of a linear parameter, the distances of an XY parameter, the
        /// position of a point parameter, the angle of a rotation parameter, the state of a flip parameter and the state of a
        /// visibility parameter, in a block of those and of the stretch, move, scale, rotate and flip actions the library writes
        /// (<see cref="DxfBlockTableRecord.AddStretchAction(DxfBlockLinearParameter, GeometryHelper.Geometry.GeoPoint2, GeometryHelper.Geometry.GeoPoint2, System.Collections.Generic.IEnumerable{DxfEntity})"/>
        /// and the others): the value taken to the parameter's value set, then the block worked out at the values of all its
        /// properties, the copy written as AutoCAD writes it, and the value read back. A Width of the rectangle 100 by 50 set to
        /// 200 moves the right edge to x = 200 in the copy, as AutoCAD 2021 moved it; a Mark set to Tick shows the tick and hides
        /// the cross; an Angle set to Math.PI / 2 turns the entities of its rotate action a quarter turn about its base point.
        /// </para>
        /// <para>
        /// The actions are worked out in the order they were added: stretches in a row are summed point by point (a line's ends,
        /// a polyline's vertices; an arc, a circle, a text, a multiline text or an INSERT held moves whole), a move moves its
        /// entities whole by the change of the point it follows (the end point of a linear or an XY parameter, the position of a
        /// point parameter), a scale scales them alike about the base point of its linear parameter by the distance over the
        /// block's own, a rotate turns them about the base point of its rotation parameter by the angle less the block's own, and
        /// a flip mirrors them about its reflection line while the state is 1.
        /// </para>
        /// <para>
        /// A distance or a position is a number (a <see cref="double"/>, or a whole number taken as one); a linear distance is
        /// taken, as AutoCAD 2021 took it (plugins v17 to v20), to the nearest step of an increment set, a tie between two steps
        /// to the step below (85 to 80 of 50 to 300 by 10), and then into the minimum and the maximum (400 to 300); to the
        /// nearest of a list, a tie to the one farther from the value the property had, beyond the value asked in the way it
        /// changes (125 to 100 from 300 and 225 to 250 from 50 of 50, 100 ... 300), as AutoCAD 2021 took 8 of the 11 ties of
        /// plugin v20, its choice in the other three and in three of the four of earlier rounds being the other one; and is
        /// kept as given where the parameter has no set. A distance or a position of an INSERT
        /// scaled by 2 is twice the block's, as it is read. A distance of an XY parameter is above the point tolerance. An angle
        /// is a number of radians, taken into a turn, from nought up to 2 pi. A flip state is a whole number, 0 or 1, and reads
        /// back as a <see cref="short"/>. A visibility state is one of the parameter's, by its name, letters of either case the
        /// same, and reads back as the parameter names it.
        /// </para>
        /// <para>
        /// The INSERT is made to place an anonymous block of AutoCAD's (<c>*U1</c> and on, flagged anonymous) holding a copy of
        /// each entity of the dynamic block: moved, stretched, scaled, turned or mirrored by the actions, a governed entity
        /// shown or hidden by the state; the block's extended data AcDbBlockRepBTag names the dynamic block, and each copy keeps its
        /// source's tag with its own handle. The anonymous block the INSERT places already is filled anew where no other
        /// INSERT places it, else a new one is made, as AutoCAD 2021 made <c>*U2</c> for a second INSERT whose value it set (plugin
        /// v17). The INSERT's extension dictionary is given
        /// AcDbBlockRepresentation, with the representation data naming the dynamic block and, in AppDataCache, the record of
        /// each parameter and action AutoCAD reads the values from (ACAD_ENHANCEDBLOCKDATA); a history of grip edits it kept
        /// (ACAD_ENHANCEDBLOCKHISTORY) is taken out, as AutoCAD took it out when it set a value. The INSERT keeps its place,
        /// scale, attributes and look; the dynamic block is not changed.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value set is not a finite number for a distance, a position or an
        /// angle, not a whole number for a flip state, or not the name of a state of a visibility parameter.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance, taken to the value set, is not above the point
        /// tolerance of <see cref="Tolerance.Default"/>, or a flip state is neither 0 nor 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the property is read only, or its INSERT is erased, in no
        /// drawing, or scaled unlike along its axes, which AutoCAD does not take as a dynamic block's.</exception>
        /// <exception cref="NotSupportedException">Thrown when the property is of a parameter the library does not work out (a
        /// polar or lookup parameter), or the block holds such a parameter, an action of another kind (an array, a polar
        /// stretch, a lookup), or an action the library does not work out: a stretch of an entity other than those above or of
        /// an arc by one end, an action moving other parameters, a move or a stretch scaled or turned, a scale or a rotate about
        /// another point than its parameter's base point, a rotation or an XY parameter with a value set, an entity both flipped
        /// and moved by other actions. Nothing is written then.</exception>
        public object Value
        {
            get => _value;

            set
            {
                if (ReadOnly)
                {
                    throw new InvalidOperationException("The property " + PropertyName + " is read only, as AutoCAD keeps it.");
                }

                if (_insert == null)
                {
                    throw new NotSupportedException("The property " + PropertyName + " is of a parameter the library does not work out: it sets those of linear, XY, point, rotation, flip and visibility parameters.");
                }

                _value = DxfDynamicBlockRepresentation.Set(_insert, _node, _part, value);
            }
        }

        /// <summary>
        /// Gets the kind of value, as AutoCAD's <c>PropertyTypeCode</c> numbers it by its <c>DwgDataType</c>: 1 for a
        /// <see cref="double"/>, 3 for a <see cref="short"/>, 5 for a <see cref="string"/>.
        /// </summary>
        public short PropertyTypeCode { get; }

        /// <summary>Gets what the value measures, as AutoCAD's <c>UnitsType</c>: a length, an angle, or nothing.</summary>
        public DxfDynamicBlockReferencePropertyUnitsType UnitsType { get; }

        /// <summary>
        /// Gets whether AutoCAD lets the property be set, as its <c>ReadOnly</c>: true only for a value looked up whose table
        /// does not allow looking back (its column's reverse lookup off).
        /// </summary>
        public bool ReadOnly { get; }

        /// <summary>Gets the description the parameter gives the property, as AutoCAD's <c>Description</c>; empty where it gives none.</summary>
        public string Description { get; }

        /// <summary>
        /// Gets the values the property may take, as AutoCAD's <c>GetAllowedValues</c>: the list of a parameter's value set,
        /// lengths scaled as <see cref="Value"/> is; every visibility state by name; 0 and 1 for a flip; the values a lookup
        /// table gives. Empty where the parameter keeps no list, as for a value set of an increment or of none.
        /// </summary>
        /// <returns>A new array on each call, its values of the type of <see cref="Value"/>.</returns>
        public object[] GetAllowedValues() => (object[])_allowed.Clone();

        /// <summary>The id of the node of the parameter whose value is set; null for a property that is read only.</summary>
        internal string Node => _node;

        /// <summary>Which of its parameter's values the property is: 0, or 1 for the y of a point or an XY parameter.</summary>
        internal int Part => _part;

        /// <summary>Ties the property to its INSERT, its parameter's node and which of the parameter's values it is, so that its value may be set.</summary>
        internal void Bind(DxfBlockReference insert, string node, int part)
        {
            _insert = insert;
            _node = node;
            _part = part;
        }

        /// <summary>Gives the property as a line for debugging: its name and its value.</summary>
        /// <returns>The line, such as <c>Width = 1000</c>.</returns>
        public override string ToString() => PropertyName + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
