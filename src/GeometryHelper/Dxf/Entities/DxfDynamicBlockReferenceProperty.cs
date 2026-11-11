using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One dynamic property of an INSERT of a dynamic block, as AutoCAD's <c>DynamicBlockReferenceProperty</c> gives it: the
    /// name a parameter of the block shows it by, its value for this INSERT as AutoCAD saved it, and the values it may take.
    /// The value of a linear, XY, polar, point, rotation, flip, visibility or lookup parameter, in a block of the actions the library
    /// writes or of the stretches and moves AutoCAD's Block Editor writes besides, is set as AutoCAD sets it
    /// (<see cref="Value"/>); the others are read only.
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

        // Whether the property is set though it reads as read only, a table that must be matched holding it.
        private bool _settable;

        /// <summary>A property read from a parameter, its allowed values kept as given.</summary>
        internal DxfDynamicBlockReferenceProperty(
            string name,
            string description,
            object value,
            short typeCode,
            DxfDynamicBlockReferencePropertyUnitsType units,
            bool readOnly,
            object[] allowed
        )
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
        /// distance and the angle of a polar parameter, the position of a point parameter, the angle of a rotation parameter,
        /// the state of a flip parameter, the state of a visibility parameter and the label of a lookup parameter, in a block of
        /// those, of alignment and base point parameters, and of the stretch, polar stretch, move, scale, rotate, flip, array and
        /// lookup actions the library writes
        /// (<see cref="DxfBlockTableRecord.AddStretchAction(DxfBlockLinearParameter, GeometryHelper.Geometry.GeoPoint2, GeometryHelper.Geometry.GeoPoint2, System.Collections.Generic.IEnumerable{DxfEntity})"/>
        /// and the others), and of the stretches and moves AutoCAD's Block Editor writes besides: tied to a parameter's base
        /// grip, carrying other linear parameters, moving attribute definitions. The value is taken to the parameter's value set, then the block worked out at the values of all its
        /// properties, the copy written as AutoCAD writes it, and the value read back. A Width of the rectangle 100 by 50 set to
        /// 200 moves the right edge to x = 200 in the copy, as AutoCAD 2021 moved it; a Mark set to Tick shows the tick and hides
        /// the cross; an Angle set to Math.PI / 2 turns the entities of its rotate action a quarter turn about its base point.
        /// </para>
        /// <para>
        /// The actions are worked out in the order they were added: stretches in a row are summed point by point (a line's ends,
        /// a polyline's vertices; an arc held by one end has that end moved and keeps the height of its middle off its chord,
        /// as AutoCAD 2021 kept it; an arc held by both ends, a circle, a text, a multiline text or an INSERT held moves whole),
        /// a move moves its entities whole by the change of the point it follows (the end point of a linear or an XY parameter,
        /// the position of a point parameter), each times its distance multiplier and turned by its angle offset; a scale scales
        /// them alike about the base point of its linear parameter by the distance over the block's own, a rotate turns them
        /// about the base point of its rotation parameter by the angle less the block's own, and a flip mirrors them about its
        /// reflection line while the state is 1. A polar stretch turns its entities about the base point of its polar parameter
        /// by the angle less the block's own, and moves the points it holds in its frame besides along the parameter's line by
        /// the distance less the block's own, as AutoCAD 2021 drew it (plugin D25): a pointer from (0, 0) to (100, 0) held by
        /// its tip, set to 150 and a quarter turn, runs to (0, 150), and an entity it turns only turns alone; an entity of one
        /// point held, an arc held by both ends and an entity its selection names holding no point turn and move whole. Then
        /// each array copies its entities, as many as its spacing goes whole into
        /// the distance, one at least: a Length of 250 of a post every 60 shows four posts; an array of an XY parameter copies
        /// in rows and columns, and nothing where either count is nought, as AutoCAD 2021 drew it (plugin v21). A move listing
        /// its own parameter's grip among what it moves, as AutoCAD's point sample does, moves its entities alone. A stretch or
        /// a move following a parameter's base point (BaseXDelta and BaseYDelta, tied to its base grip) moves nothing, as a
        /// value set moves the end point only. A move holding another linear parameter carries it, as AutoCAD's Block Editor
        /// writes such a move (the parameters' node ids after the count 70 of its head): the parameter's base point and end point
        /// move by the move, its grips with them, its distance kept, and its own actions do not follow its being carried (it
        /// chains none); a frame whose Depth of 60 from (40, 60) down to (40, 0) is set to 100 carries Bottom from
        /// (0, 0)-(100, 0) to (0, -40)-(100, -40), still 100, and keeps it there when Bottom is set next. An attribute
        /// definition a move holds moves in the copy, and each ATTRIB of the INSERT of its tag moves by the change of that
        /// move, carried by the INSERT's placing, keeping a place the user gave it: Mark X set from 20 to 30 on an INSERT
        /// turned a quarter turn at the scale 2 moves the ATTRIB 20 along y in the world. A lookup set
        /// to a row's label sets the properties of its input columns to the row's values; any property set, the lookup set
        /// too, each lookup reads the label of the first row of the values then, or its label of no match, Custom as the
        /// library writes it: set to a row of the values of a row above it, it reads that row's label, as AutoCAD 2021 read
        /// Small after Twin was set, a row of Small's values further down (plugin D25b).
        /// </para>
        /// <para>
        /// A distance or a position is a number (a <see cref="double"/>, or a whole number taken as one); a linear distance is
        /// taken, as AutoCAD 2021 took it (plugins v17 to v20), to the nearest step of an increment set, a tie between two steps
        /// to the step below (85 to 80 and 95 to 90 of 50 to 300 by 10 for an own 100), and then into the minimum and the
        /// maximum (400 to 300); to the nearest of a list, a tie to the block's own value where it is one of the two, else to
        /// the one written first, the lesser of a list written from the least up, whatever the value the property had: of 50,
        /// 100 ... 300, 75 and 125 go to 100 and 175 to 150 for an own 100, 125 to 100 and 225 to 250 for an own 250, as
        /// AutoCAD 2021 took all 358 ties of lists of 3 to 6 values of a linear, an XY and a polar parameter, from every value
        /// before and in two sessions alike (plugin D25b); and is kept as given where the parameter has no set. A distance of
        /// an XY or a polar parameter is taken so too. A distance or a position of an INSERT
        /// scaled by 2 is twice the block's, as it is read. A distance of an XY or a polar parameter is above the point
        /// tolerance. An angle is a number of radians, taken into a turn, from nought up to 2 pi, and to the value set of a
        /// rotation or a polar parameter round the circle, as AutoCAD 2021 took it (plugin D25): to the nearest of a list, 338
        /// degrees to 0 of 0, 90, 180 and 270; outside a minimum and a maximum to the one nearer round the circle, 300 degrees to
        /// 0 of 0 to 90; within them to the nearest step of an increment, 40 to 45 of steps of 15. A tie goes to the value
        /// behind the angle asked, clockwise, 22.5 degrees to 15 and 315 to 270: AutoCAD's own choice followed its rounding, the
        /// step behind in 26 of 58 ties of steps of 15 degrees, and the value behind in 23 of 32 of the list above, 135 degrees
        /// going to 180 each time. A flip state is a whole number, 0 or 1, and reads
        /// back as a <see cref="short"/>. A visibility state is one of the parameter's, by its name, letters of either case the
        /// same, and reads back as the parameter names it; so is a lookup's label, one of a row of its table, a label given
        /// exactly first, else trimmed, and reads back as the table holds it, a space after it kept, as AutoCAD 2021 read
        /// "Size 6 " of its lookup sample (plugin v21).
        /// </para>
        /// <para>
        /// A block properties table (Block Table1) is set to the index of a row, a whole number, as AutoCAD 2021 set it (plugin
        /// D26): each parameter's column takes the row's value, in the block's units (twice it on an INSERT scaled by 2), an
        /// angle taken into a turn; each user parameter's column its value; the INSERT's attribute of each attribute
        /// definition's column its text, the plate's MARK P200 for the row 1; an empty cell leaves its column as it is. It reads
        /// back the index of the first row of the values then. A user parameter of text is set to a string, one of a real to a
        /// number. Any other property set leaving no row matched gives each user parameter of a column its value of no match,
        /// as AutoCAD read Spaces Custom once Width was set off the rows of its Parking sample; a user parameter set keeps the
        /// value given.
        /// </para>
        /// <para>
        /// A horizontal, a vertical or a radial constraint parameter is set by solving the block's geometric and dimensional
        /// constraints, kept in the associative network of its BLOCK_RECORD, as AutoCAD 2021 solved its Dynamic Blocks samples
        /// (plugins D27, D27b): the distance taken to its value set and to twelve significant digits, as AutoCAD solved Height
        /// 612.3456789012345 at 612.345678901, the variable of the dimensional constraint it drives given it, the variables of
        /// expressions of it worked out (numbers, names, + - * / ^, brackets and the functions sin, cos, tan, asin, acos,
        /// atan, sqrt, abs and round, angles in degrees), and the lines, arcs, circles, polyline edges and construction lines
        /// the constraints hold moved from the block's own by Newton's method, each step the least that meets the constraints,
        /// before any action works on them. Geometry the constraints leave free keeps the base point of each constraint
        /// parameter set where it is along its distance, as AutoCAD kept the left side of its Door Elevation, fixed nowhere, at
        /// a new Width; what is still free is held where the block has it, the radii and the arcs' ends before the rest where
        /// all at once cannot be met, as AutoCAD kept the fillets of its Hex Head Bolt whole at a new HdCr. Of AutoCAD's
        /// Fluorescent, Door Elevation, Window, Channel, Concrete Pipe, Hex Head Bolt, WF Beam and I Beam samples, metric and
        /// imperial, 77 of the 79 values the library set came out within 3E-12 of AutoCAD's, one of an I Beam whose own
        /// geometry is 7E-6 off its constraints 1.5E-6 off, and one of Fluorescent at Width 800 apart, AutoCAD having taken
        /// its table's row back: Width 1 200 of Fluorescent's rectangle 1 000 by 500, its corner fixed, moves its right side to
        /// x = 1 200. A table whose properties must match a row leaves a constraint parameter
        /// at the value set and the others at theirs, no row matched, as AutoCAD set Fluorescent's Width; a row that would
        /// change a constraint parameter is refused, as AutoCAD left the constraints as they were at a row of its Hex Head
        /// Bolt. A constraint parameter of no column of the table leaves its user parameters as they are, as AutoCAD kept Hex
        /// Head Bolt's Size 1 at Length 80. A flip carrying a constraint parameter mirrors its points too, as AutoCAD's Window
        /// sample's flip carries DoorWidth. The anonymous block is given a copy of the block's associative network at the
        /// values set, as AutoCAD gives its own, the dimensions of the constraints moved to the geometry and showing their
        /// values: AutoCAD 2021 set such an INSERT again from it as from its own, 15 values of 7 samples within 1E-12 of its
        /// own sets, where it set none again whose anonymous block held no copy, or one naming entities gone (eKeyNotFound).
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
        /// (ACAD_ENHANCEDBLOCKHISTORY) is kept, the node's id, the property's name and its value read back added after it, as
        /// AutoCAD 2021 added Block Table1 2, Spaces 5 Spaces and Width 5400 to its Parking sample's (plugin D26); an INSERT of no
        /// history is given none, as AutoCAD gave none to the library's INSERTs it set. The INSERT keeps its place,
        /// scale, attributes and look; the dynamic block is not changed.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value set is not a finite number for a distance, a position or an
        /// angle, not a whole number for a flip state or a block properties table, not the name of a state of a visibility
        /// parameter, not the label of a row of a lookup's table, or not a string for a user parameter of text.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a distance, taken to the value set, is not above the point
        /// tolerance of <see cref="Tolerance.Default"/>, a flip state is neither 0 nor 1, or a row is not one of the block
        /// properties table's, as AutoCAD 2021 refused -1 and the row after the last (eInvalidInput).</exception>
        /// <exception cref="InvalidOperationException">Thrown when the property is read only, or its INSERT is erased, in no
        /// drawing, or scaled unlike along its axes, which AutoCAD does not take as a dynamic block's.</exception>
        /// <exception cref="NotSupportedException">Thrown when the block holds a parameter or an action of a kind the library does
        /// not work out (an aligned constraint parameter), constraints it does not solve (a constraint of a kind other than a
        /// coincidence, a point on a curve, parallel, perpendicular, horizontal, colinear, tangent, a middle, a centre, fixed,
        /// equal lengths or radii, a distance along x or y, an angle of two lines or of three points and a radius; geometry
        /// other than lines, arcs, circles and construction lines flat in the block, or an expression of another function),
        /// constraints that cannot be met at the value set, a block properties table's row that would change a constraint parameter, or an action it does not work out: a stretch of an entity other than those above or of a tilted arc by one end, a stretch, a scale, a rotate, a
        /// flip or an array holding other parameters, a move holding grips of a parameter it does not carry, a move carrying a
        /// parameter other than a linear one, or one that chains its actions or that a scale or an array follows, attribute
        /// definitions of one tag moved apart where the INSERT has an ATTRIB of that tag, an attribute definition worked on by
        /// an action other than a move, a scale or a rotate about another point than its parameter's base point, a polar
        /// stretch tied to its parameter's base point, of a distance multiplier other than 1 or an angle offset other than
        /// nought, or holding an arc by one end, an entity both flipped and moved by other actions, an entity both turned or
        /// scaled and stretched or moved, as AutoCAD 2021 worked such an entity out from the INSERT's last state (plugin v21),
        /// an entity both copied by an array or turned by a polar stretch and worked on by another action, a lookup table of
        /// columns other than a linear or a polar parameter's distance, a point parameter's x and y, an XY parameter's
        /// distances, a polar or a rotation parameter's angle and lookups, a block properties table of columns other than those or of
        /// user parameters, the visibility state and attribute definitions, a lookup or a user parameter of a block whose table
        /// its properties must match (290 of 1), or a parameter of such a table of a column of a user parameter or of the
        /// visibility state, a user parameter of another kind than a text or a real, or of an expression; or when the INSERT keeps
        /// the base point of a linear, an XY, a polar, a rotation or a flip parameter away from the
        /// block's own, as the moves carrying the parameter take it, beyond the point tolerance of <see cref="Tolerance.Default"/>, as a grip moved it: AutoCAD works such
        /// an INSERT out from the base point it keeps, and drew the array of its XY sample's INSERT whose base point a grip
        /// took from (0, 5) to (0, 104.30) at y 99.30 down to 19.30, where the library, working from the block's own base
        /// point, would draw it at 0 down to -80. Nothing is written then.</exception>
        public object Value
        {
            get => _value;
            set
            {
                if (ReadOnly && !_settable)
                {
                    throw new InvalidOperationException(
                        "The property " + PropertyName + " is read only, as AutoCAD keeps it."
                    );
                }

                if (_insert == null)
                {
                    throw new NotSupportedException(
                        "The property "
                            + PropertyName
                            + " is of a parameter the library does not work out: it sets those of linear, XY, polar, point, rotation, flip, visibility, lookup, user and horizontal, vertical and radial constraint parameters and of block properties tables."
                    );
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
        /// Gets whether AutoCAD lets the property be set, as its <c>ReadOnly</c>: true for a value looked up whose table does
        /// not allow looking back (its column's reverse lookup off); and for the distance of a horizontal, a vertical, an
        /// aligned or a radial constraint parameter a column of a block properties table holds whose properties must match a
        /// row, as AutoCAD 2021 read Width and Height of its Fluorescent sample (plugin D27b), whose value is set all the same,
        /// as AutoCAD set Width 1 200 there.
        /// </summary>
        public bool ReadOnly { get; private set; }

        /// <summary>Gets the description the parameter gives the property, as AutoCAD's <c>Description</c>; empty where it gives none.</summary>
        public string Description { get; }

        /// <summary>
        /// Gets the values the property may take, as AutoCAD's <c>GetAllowedValues</c>: the list of a parameter's value set,
        /// lengths scaled as <see cref="Value"/> is, with the block's own value where the list was written without it, after
        /// the last value below it, as AutoCAD 2021 gave Door Size of its Door sample, own 750, the values 600, 700, 750, 800,
        /// 900 and 1 000 (plugin D26b); every visibility state by name; 0 and 1 for a flip; the values a lookup table gives,
        /// and its label of no match last for a lookup that is read only, Custom of AutoCAD's Roller Bearing. Empty where the
        /// parameter keeps no list, as for a value set of an increment or of none.
        /// </summary>
        /// <returns>A new array on each call, its values of the type of <see cref="Value"/>.</returns>
        public object[] GetAllowedValues() => (object[])_allowed.Clone();

        /// <summary>
        /// Gets the names a flip parameter gives its two states, for its values 0 and 1, as AutoCAD's Properties palette shows
        /// them: Not flipped and Flipped as the Block Editor names them, Left and Right for Hinge of AutoCAD's Door sample.
        /// Empty for a property of another kind, whose values name themselves.
        /// </summary>
        public IReadOnlyList<string> StateNames { get; internal set; } = new string[0];

        /// <summary>
        /// Gets whether AutoCAD's Properties palette lists the property, as AutoCAD's <c>Show</c>: its parameter's Show
        /// properties of the Block Editor, false for Stud Height and Stud Width of AutoCAD's Stud sample, whose values its
        /// lookups set; true for a property of no such setting.
        /// </summary>
        public bool Show { get; internal set; } = true;

        /// <summary>
        /// Gets whether the property's parameter is shown in the visibility state the INSERT shows, as AutoCAD's
        /// <c>VisibleInCurrentVisibilityState</c>: true for a block of no visibility parameter and for that parameter's own
        /// property; else whether the state lists the parameter, as AutoCAD 2021 read Angle of its Toilet sample in the state
        /// Elongated (Front) as not visible, and showed no grip of it. AutoCAD's Properties palette lists a property only
        /// where it is visible so and <see cref="Show"/> is true. The library's own blocks of a visibility parameter list no other
        /// parameter in any state, and AutoCAD reads every other property of them as not visible (plugin GhDyn1).
        /// </summary>
        public bool VisibleInCurrentVisibilityState { get; internal set; } = true;

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

        /// <summary>Reads the property as read only, a table whose properties must match holding it, while its value is still set, as AutoCAD 2021 read and set Width of its Fluorescent sample (plugin D27b).</summary>
        internal void HeldByTable()
        {
            ReadOnly = true;
            _settable = true;
        }

        /// <summary>Gives the property as a line for debugging: its name and its value.</summary>
        /// <returns>The line, such as <c>Width = 1000</c>.</returns>
        public override string ToString() =>
            PropertyName + " = " + Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
