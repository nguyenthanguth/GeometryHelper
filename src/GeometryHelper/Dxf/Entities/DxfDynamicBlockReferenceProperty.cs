using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One dynamic property of an INSERT of a dynamic block, as AutoCAD's <c>DynamicBlockReferenceProperty</c> gives it: the
    /// name a parameter of the block shows it by, its value for this INSERT as AutoCAD saved it, and the values it may take.
    /// The value of a linear parameter whose actions are stretches of lines and polylines, and of a visibility parameter, is
    /// set as AutoCAD sets it (<see cref="Value"/>); the others are read only.
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

        // The INSERT and the id of the parameter's node, for a property whose value is set; null for one that is read only.
        private DxfBlockReference _insert;
        private string _node;

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
        /// Set, as AutoCAD's <c>Value</c> sets it, on the distance of a linear parameter whose actions are stretches of lines and
        /// polylines (<see cref="DxfBlockTableRecord.AddStretchAction"/>) and on the state of a visibility parameter
        /// (<see cref="DxfBlockTableRecord.AddVisibilityParameter"/>), in a block of those alone: the value taken to the
        /// parameter's value set, then the block worked out at the values of all its properties, the stretched copy written as
        /// AutoCAD writes it, and the value read back. A Width of the rectangle 100 by 50 set to 200 moves the right edge to
        /// x = 200 in the copy, as AutoCAD 2021 moved it; a Mark set to Tick shows the tick and hides the cross.
        /// </para>
        /// <para>
        /// A distance is a number (a <see cref="double"/>, or a whole number taken as one); it is taken, as AutoCAD 2021 took it
        /// (plugins v17, v18), to the nearest step of an increment set, a tie between two steps to the even step from the
        /// minimum (75 to 70 of 50 to 300 by 10), and then into the minimum and the maximum (400 to 300); to the nearest of a
        /// list, a tie to the one nearer the value the property had (75 to 100 from 100 of 50, 100 ... 300; 100 to 80 from 80
        /// of 50, 80, 120); and is kept as given where the parameter has no set. A distance of an INSERT scaled by 2 is twice
        /// the block's, as it is read. A state is one of the parameter's, by its name, letters of either case the same, and
        /// reads back as the parameter names it.
        /// </para>
        /// <para>
        /// The INSERT is made to place an anonymous block of AutoCAD's (<c>*U1</c> and on, flagged anonymous) holding a copy of
        /// each entity of the dynamic block: the points a stretch frame holds moved by the stretch, a governed entity shown or
        /// hidden by the state; the block's extended data AcDbBlockRepBTag names the dynamic block, and each copy keeps its
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
        /// <exception cref="ArgumentException">Thrown when the value set is not a finite number for a distance, or is not the
        /// name of a state of a visibility parameter.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance, taken to the value set, is not above the point
        /// tolerance of <see cref="Tolerance.Default"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the property is read only, or its INSERT is erased, in no
        /// drawing, or scaled unlike along its axes, which AutoCAD does not take as a dynamic block's.</exception>
        /// <exception cref="NotSupportedException">Thrown when the property is of a parameter the library does not work out (a
        /// polar, XY, rotation, point, flip or lookup parameter), or the block holds such a parameter, an action other than a
        /// stretch, or a stretch the library does not work out: of an entity other than a line or a polyline, moving other
        /// parameters, or scaled or turned. Nothing is written then.</exception>
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
                    throw new NotSupportedException("The property " + PropertyName + " is of a parameter the library does not work out: it sets those of linear and visibility parameters.");
                }

                _value = DxfDynamicBlockRepresentation.Set(_insert, _node, value);
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

        /// <summary>Ties the property to its INSERT and its parameter's node, so that its value may be set.</summary>
        internal void Bind(DxfBlockReference insert, string node)
        {
            _insert = insert;
            _node = node;
        }

        /// <summary>Gives the property as a line for debugging: its name and its value.</summary>
        /// <returns>The line, such as <c>Width = 1000</c>.</returns>
        public override string ToString() => PropertyName + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
