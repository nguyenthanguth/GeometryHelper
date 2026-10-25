using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The linetype table of a DXF file, as AutoCAD's <c>LinetypeTable</c>: its linetypes in the order of the file, each a
    /// <see cref="DxfLinetypeTableRecord"/>, found by name and added to.
    /// </summary>
    /// <remarks>
    /// A dashed line of dashes 0.5 long with gaps of 0.25 is
    /// <c>var dashed = new DxfLinetypeTableRecord { Name = "DASHED", AsciiDescription = "__ __ __", NumDashes = 2 };</c>,
    /// <c>dashed.SetDashLengthAt(0, 0.5); dashed.SetDashLengthAt(1, -0.25);</c> and <c>db.LinetypeTable.Add(dashed)</c>.
    /// </remarks>
    public sealed class DxfLinetypeTable : DxfSymbolTable, IEnumerable<DxfLinetypeTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfLinetypeTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>
        /// Gets the linetype of a name, letters of either case the same.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The linetype.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no linetype of the name.</exception>
        public new DxfLinetypeTableRecord this[string key] => (DxfLinetypeTableRecord)base[key];

        /// <summary>Gets an enumerator over the linetypes, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfLinetypeTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("LTYPE").ToArray())
            {
                yield return DxfLinetypeTableRecord.Of(item);
            }
        }

        IEnumerator<DxfLinetypeTableRecord> IEnumerable<DxfLinetypeTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfLinetypeTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfLinetypeTableRecord linetype))
            {
                throw new ArgumentException("The linetype table takes a DxfLinetypeTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            linetype.AddTo(_file);
        }
    }


    /// <summary>
    /// A linetype of a DXF file, as AutoCAD's <c>LinetypeTableRecord</c>: its name, its description, the length of its
    /// pattern and its dashes, a dash above nought drawn, below nought a gap, nought a dot; a dash of a complex linetype
    /// carries a shape or a text, drawn in a style at a scale, a rotation and an offset of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A linetype of a file is read from its LTYPE record; its <see cref="AsciiDescription"/>, <see cref="PatternLength"/>,
    /// and each dash's length, shape or text are set as any record's are, its record written anew and every other group kept;
    /// how many dashes it has is not changed yet. One made in code is given its dashes by <see cref="NumDashes"/> and
    /// <see cref="SetDashLengthAt(int, double)"/>, and is written when it is added to a <see cref="DxfLinetypeTable"/>; its
    /// pattern is as long as its dashes and gaps together unless it is given a length.
    /// </para>
    /// <para>
    /// The gas line of AutoCAD's acadiso.lin, a dash 0.5 long, then the text GAS in the style Standard, 0.1 high, 0.1 back and
    /// 0.05 down, in a gap of 0.2, is <c>SetTextAt(1, "GAS")</c>, <c>SetShapeStyleAt(1, standard)</c>,
    /// <c>SetShapeScaleAt(1, 0.1)</c> and <c>SetShapeOffsetAt(1, new GeoVector2(-0.1, -0.05))</c> on its second dash, written as
    /// AutoCAD writes it: <c>49 -0.2, 74 2, 75 0, 340</c> the handle of Standard<c>, 46 0.1, 50 0.0, 44 -0.1, 45 -0.05, 9 GAS</c>.
    /// A dash given a shape or a text and no scale is drawn at the scale 1. A file of R12 has no complex linetypes, which came
    /// with R13 (AC1012), and refuses them.
    /// </para>
    /// </remarks>
    public sealed class DxfLinetypeTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbLinetypeTableRecord in the order AutoCAD writes them, those of each dash last.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 3, 72, 73, 40, 49, 74, 75, 340, 46, 50, 44, 45, 9 });

        // The groups of a dash, in the order AutoCAD writes them.
        private static readonly int[] DashCodes = { 49, 74, 75, 340, 46, 50, 44, 45, 9 };

        // The sections whose records name a linetype by name (group 6): the layers, the entities, the styles of multilines.
        private static readonly string[] Naming = { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" };

        private string _description = string.Empty;
        private double? _length;
        private List<Dash> _dashes = new List<Dash>();
        private List<DxfTextStyleTableRecord> _styles = new List<DxfTextStyleTableRecord>();

        /// <summary>
        /// Initializes a linetype made in code, with no dashes: a continuous line until it is given some, to be added to a
        /// <see cref="DxfLinetypeTable"/> with its <see cref="DxfSymbolTableRecord.Name"/> given.
        /// </summary>
        public DxfLinetypeTableRecord()
        {
        }

        private DxfLinetypeTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the description of the linetype (group 3), as AutoCAD's <c>AsciiDescription</c>: <c>__ __ __</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description set has a line break in it.</exception>
        public string AsciiDescription
        {
            get => Item == null ? _description : Item.File.ValueOf(Item, 3) ?? string.Empty;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("A description is one line, with no line break in it.", nameof(value));
                }

                if (Item == null)
                {
                    _description = value;
                }
                else if (!string.Equals(value, AsciiDescription, StringComparison.Ordinal))
                {
                    Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(3, value) }, Order, false);
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the linetype, as AutoCAD's <c>Comments</c>, which ObjectARX keeps in the same place
        /// as <see cref="AsciiDescription"/> (group 3): the one is the other.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description set has a line break in it.</exception>
        public string Comments
        {
            get => AsciiDescription;
            set => AsciiDescription = value;
        }

        /// <summary>
        /// Gets or sets the length of the linetype's pattern (group 40), as AutoCAD's <c>PatternLength</c>: the dashes and gaps
        /// together, 0.75 for a dash of 0.5 and a gap of 0.25; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number of nought or more.</exception>
        public double PatternLength
        {
            get
            {
                if (Item == null)
                {
                    if (_length.HasValue)
                    {
                        return _length.Value;
                    }

                    double sum = 0.0;

                    foreach (Dash dash in _dashes)
                    {
                        sum += Math.Abs(dash.Length);
                    }

                    return sum;
                }

                return Numbers.TryRead(Item.File.ValueOf(Item, 40), out double length) && Guard.IsFinite(length) ? length : 0.0;
            }

            set
            {
                // The length it has changes nothing, whatever the file holds.
                if (Item != null && Same(value, PatternLength))
                {
                    return;
                }

                Guard.NonNegative(value, nameof(value), "The length of a pattern has to be a finite number of nought or more.");

                if (Item == null)
                {
                    _length = value;
                }
                else if (!Same(value, PatternLength))
                {
                    Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(40, Numbers.Exact(value + 0.0)) }, Order, false);
                }
            }
        }

        /// <summary>
        /// Gets or sets how many dashes the pattern has (group 73), as AutoCAD's <c>NumDashes</c>; set on a linetype made in
        /// code, its new dashes nought long.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is below nought or above 12, AutoCAD's most.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a linetype of a file: how many dashes it has is not changed yet.</exception>
        public int NumDashes
        {
            get => Dashes().Count;

            set
            {
                // The count it has changes nothing, whatever the file holds.
                if (value == NumDashes)
                {
                    return;
                }

                if (value < 0 || value > 12)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A pattern has from 0 to 12 dashes.");
                }

                if (Item != null)
                {
                    throw new InvalidOperationException("How many dashes the linetype " + Name + " of a file has is not changed yet: a later release changes it.");
                }

                while (_dashes.Count > value)
                {
                    _dashes.RemoveAt(_dashes.Count - 1);
                    _styles.RemoveAt(_styles.Count - 1);
                }

                while (_dashes.Count < value)
                {
                    _dashes.Add(Dash.Simple(0.0));
                    _styles.Add(null);
                }
            }
        }

        /// <summary>
        /// Gets the length of a dash of the pattern (the groups 49 in their order), as AutoCAD's <c>DashLengthAt</c>: above
        /// nought a dash, below nought a gap, nought a dot.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>Its length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or not below <see cref="NumDashes"/>.</exception>
        public double DashLengthAt(int index) => DashAt(index).Length;

        /// <summary>
        /// Sets the length of a dash of the pattern, as AutoCAD's <c>SetDashLengthAt</c>: its group 49; the length of the
        /// pattern is kept as it is, as AutoCAD keeps it.
        /// </summary>
        /// <param name="index">The dash, from nought to <see cref="NumDashes"/> less one.</param>
        /// <param name="value">Its length: above nought a dash, below nought a gap, nought a dot.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern, or the length is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased.</exception>
        public void SetDashLengthAt(int index, double value)
        {
            Guard.Finite(value, nameof(value), "The length of a dash has to be a finite number.");
            Change(index, d => d.Length = value);
        }

        /// <summary>
        /// Gets the text style a dash's shape or text is drawn in (group 340), as AutoCAD's <c>ShapeStyleAt</c> gives its id: a
        /// style of shapes for a shape, one of letters for a text.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The style; null where the dash names none, or one the file has not.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public DxfTextStyleTableRecord ShapeStyleAt(int index)
        {
            Dash dash = DashAt(index);

            if (Item == null)
            {
                return _styles[index];
            }

            string handle = dash.Style;

            foreach (DxfItem style in Item.File.TableItems("STYLE"))
            {
                if (handle != null && string.Equals(style.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return DxfTextStyleTableRecord.Of(style);
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the text style a dash's shape or text is drawn in, as AutoCAD's <c>SetShapeStyleAt</c>: its handle as the group
        /// 340; null takes it out.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="value">The style, of the same file, or null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        /// <exception cref="ArgumentException">Thrown when the style is in no file, or another file than the linetype's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeStyleAt(int index, DxfTextStyleTableRecord value)
        {
            if (value != null && (value.Item == null || value.Handle == null || (Item != null && value.Item.File != Item.File)))
            {
                throw new ArgumentException("A dash is drawn in a text style of the linetype's own file, which has a handle.", nameof(value));
            }

            Change(index, d => d.Style = value?.Handle);

            if (Item == null)
            {
                _styles[index] = value;
            }
        }

        /// <summary>Gets the number of a dash's shape in its style's file of shapes (group 75), as AutoCAD's <c>ShapeNumberAt</c>: nought for a dash of no shape, and for a text.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public int ShapeNumberAt(int index) => DashAt(index).Shape;

        /// <summary>
        /// Sets the number of a dash's shape, as AutoCAD's <c>SetShapeNumberAt</c>: a number other than nought makes the dash
        /// one of a shape (74 of 4) where it has no text; nought one of no shape.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="shapeNumber">The number, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern, or the number is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeNumberAt(int index, int shapeNumber)
        {
            if (shapeNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shapeNumber), shapeNumber, "A shape is numbered from nought.");
            }

            Change(index, d => d.Shape = shapeNumber);
        }

        /// <summary>Gets the offset of a dash's shape or text from where the dash is (groups 44 and 45), as AutoCAD's <c>ShapeOffsetAt</c>; nought where it gives none.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The offset, along the line and across it.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public GeoVector2 ShapeOffsetAt(int index)
        {
            Dash dash = DashAt(index);
            return new GeoVector2(dash.X, dash.Y);
        }

        /// <summary>Sets the offset of a dash's shape or text, as AutoCAD's <c>SetShapeOffsetAt</c>.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="offset">The offset, along the line and across it.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        /// <exception cref="ArgumentException">Thrown when the offset is not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeOffsetAt(int index, GeoVector2 offset)
        {
            if (!Guard.IsFinite(offset.X) || !Guard.IsFinite(offset.Y))
            {
                throw new ArgumentException("An offset has to be of finite numbers: " + offset + " is not.", nameof(offset));
            }

            Change(index, d =>
            {
                d.X = offset.X;
                d.Y = offset.Y;
            });
        }

        /// <summary>
        /// Gets the rotation of a dash's shape or text (group 50) in radians, as AutoCAD's <c>ShapeRotationAt</c>: the record
        /// writes degrees; from the line, or from the x axis where <see cref="ShapeIsUcsOrientedAt"/>; nought where it gives none.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The rotation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public double ShapeRotationAt(int index) => DashAt(index).Rotation * Math.PI / 180.0;

        /// <summary>Sets the rotation of a dash's shape or text in radians, as AutoCAD's <c>SetShapeRotationAt</c>, written as degrees.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="rotation">The rotation, in radians.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern, or the rotation is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeRotationAt(int index, double rotation)
        {
            Guard.Finite(rotation, nameof(rotation), "A rotation has to be a finite number.");

            // The rotation it has, read back from its degrees, changes nothing.
            if (Same(rotation, ShapeRotationAt(index)))
            {
                return;
            }

            Change(index, d => d.Rotation = rotation * 180.0 / Math.PI);
        }

        /// <summary>Gets the scale of a dash's shape or text (group 46), as AutoCAD's <c>ShapeScaleAt</c>; nought where the dash gives none, as one of no shape or text.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The scale.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public double ShapeScaleAt(int index) => DashAt(index).Scale;

        /// <summary>Sets the scale of a dash's shape or text, as AutoCAD's <c>SetShapeScaleAt</c>.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="scale">The scale, a finite number.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern, or the scale is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeScaleAt(int index, double scale)
        {
            Guard.Finite(scale, nameof(scale), "A scale has to be a finite number.");
            Change(index, d => d.Scale = scale);
        }

        /// <summary>Gets the text of a dash (group 9), as AutoCAD's <c>TextAt</c>; empty for a dash of no text.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public string TextAt(int index) => DashAt(index).Text();

        /// <summary>
        /// Sets the text of a dash, as AutoCAD's <c>SetTextAt</c>: a text makes the dash one of a text (74 of 2, its shape number
        /// 0); an empty one, one of no text.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="value">The text.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        /// <exception cref="ArgumentException">Thrown when the text has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetTextAt(int index, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A text is one line, with no line break in it.", nameof(value));
            }

            Change(index, d => d.Text = value);
        }

        /// <summary>
        /// Gets whether a dash's shape or text is turned from the x axis rather than from the line (flag 1 of group 74), as
        /// AutoCAD's <c>ShapeIsUcsOrientedAt</c>.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        public bool ShapeIsUcsOrientedAt(int index) => DashAt(index).Absolute;

        /// <summary>Sets whether a dash's shape or text is turned from the x axis rather than from the line, as AutoCAD's <c>SetShapeIsUcsOrientedAt</c>.</summary>
        /// <param name="index">The dash, from nought.</param>
        /// <param name="isOriented">Whether it is.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is erased, or of a file of R12.</exception>
        public void SetShapeIsUcsOrientedAt(int index, bool isOriented) => Change(index, d => d.Absolute = isOriented);

        /// <summary>
        /// Erases the linetype from its table, as AutoCAD's <c>Erase</c> of a linetype nothing draws with: its LTYPE record taken
        /// out, the table's count lowered by one, its handle not given again.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the linetype is in no file or is erased already; when it is
        /// ByLayer, ByBlock or Continuous, which AutoCAD keeps; when an entity, a layer or a style of multilines draws with it,
        /// or the header names it (<c>$CELTYPE</c>, <c>$DIMLTYPE</c>, <c>$DIMLTEX1</c>, <c>$DIMLTEX2</c>); or when an object of
        /// the file points at it, as a dimension style.</exception>
        public override void Erase()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The linetype " + Name + " is in no file to be erased from.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The linetype " + Name + " is erased already.");
            }

            string name = Name;

            if (string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The linetype " + name + " is one AutoCAD keeps.");
            }

            DxfFile file = Item.File;
            bool named = false;

            foreach (string variable in new[] { "$CELTYPE", "$DIMLTYPE", "$DIMLTEX1", "$DIMLTEX2" })
            {
                IReadOnlyList<DxfTypedValue> groups = file.HeaderOf(variable);
                named |= groups != null && groups.Count > 0 && string.Equals(DxfReader.NameOf(groups[0].Value), name, StringComparison.OrdinalIgnoreCase);
            }

            if (named || file.Names(Naming, (type, code) => code == 6 && type != "XRECORD" && type != "LTYPE", name))
            {
                throw new InvalidOperationException("The linetype " + name + " is drawn with, by an entity, a layer or the header, and is not erased, as AutoCAD erases a linetype only when nothing uses it.");
            }

            if (file.PointedAt(Handle))
            {
                throw new InvalidOperationException("The linetype " + name + " is pointed at by an object of the file, as a dimension style, and is not erased.");
            }

            file.RemoveTableRecord(Item, "LTYPE");
        }

        /// <summary>The object of an LTYPE record, made once.</summary>
        internal static DxfLinetypeTableRecord Of(DxfItem item) => item.Wrapper as DxfLinetypeTableRecord ?? (DxfLinetypeTableRecord)item.KeepWrapper(new DxfLinetypeTableRecord(item));

        /// <summary>Writes the linetype made in code into a file's linetype table, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < _dashes.Count; i++)
            {
                Dash dash = _dashes[i];

                if (_styles[i] != null && _styles[i].Item.File != file)
                {
                    throw new ArgumentException("The dash " + i.ToString(CultureInfo.InvariantCulture) + " is drawn in a text style of another file.", "value");
                }

                if (!file.HasMarkers && dash.IsComplex)
                {
                    throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no complex linetypes, which came with R13 (AC1012).", "value");
                }

                foreach (KeyValuePair<int, string> group in dash.Groups)
                {
                    if (group.Key != 74 || file.HasMarkers)
                    {
                        groups.Add(group);
                    }
                }
            }

            DxfItem item = file.AddLinetype(Name, _description, PatternLength, _dashes.Count, groups);

            if (item == null)
            {
                throw new InvalidOperationException("The file has no linetype table to add the linetype " + Name + " to.");
            }

            Bind(item);
        }

        /// <summary>The dashes of the linetype: of the record, its groups from each 49 to the next; of one made in code, its own.</summary>
        private List<Dash> Dashes()
        {
            if (Item == null)
            {
                return _dashes;
            }

            var dashes = new List<Dash>();

            foreach (DxfTypedValue group in Item.File.OwnGroupsOf(Item, 0))
            {
                if (group.TypeCode == 49)
                {
                    dashes.Add(new Dash());
                }

                if (dashes.Count > 0 && Array.IndexOf(DashCodes, (int)group.TypeCode) >= 0)
                {
                    dashes[dashes.Count - 1].Groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            return dashes;
        }

        private Dash DashAt(int index)
        {
            List<Dash> dashes = Dashes();

            if (index < 0 || index >= dashes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The pattern has " + dashes.Count.ToString(CultureInfo.InvariantCulture) + " dashes.");
            }

            return dashes[index];
        }

        /// <summary>
        /// Changes a dash and writes it, as AutoCAD writes a dash: its 49, its 74 from R13 on, then where it is complex its 75,
        /// its 340 where it names a style, its 46, 50, 44 and 45, and its 9 where it has a text; each value the change leaves as
        /// it was keeping its text. Nothing where the dash is as it was.
        /// </summary>
        private void Change(int index, Action<Dash.Values> change)
        {
            List<Dash> dashes = Dashes();
            Dash dash = DashAt(index);
            Dash.Values values = dash.Read();
            change(values);
            bool markers = Item == null || Item.File.HasMarkers;
            Dash written = dash.Written(values, markers);

            if (written.Same(dash))
            {
                return;
            }

            if (!markers && written.IsComplex)
            {
                throw new InvalidOperationException("A DXF of version " + (Item.File.Version ?? "R12") + " has no complex linetypes, which came with R13 (AC1012).");
            }

            dashes[index] = written;

            if (Item == null)
            {
                return;
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The linetype " + Name + " is erased, and is not changed.");
            }

            var groups = new List<KeyValuePair<int, string>>();

            foreach (Dash each in dashes)
            {
                groups.AddRange(each.Groups);
            }

            Item.File.ReplaceOwn(Item, 0, DashCodes, groups, Order, false);
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        /// <summary>A dash of a pattern, as the groups it is written with.</summary>
        private sealed class Dash
        {
            internal List<KeyValuePair<int, string>> Groups { get; } = new List<KeyValuePair<int, string>>();

            internal double Length => Real(49, 0.0);

            internal bool Absolute => (Whole(74, 0) & 1) != 0;

            internal int Shape => Whole(75, 0);

            internal string Style
            {
                get
                {
                    string handle = Text(340)?.Trim();
                    return string.IsNullOrEmpty(handle) || handle == "0" ? null : handle;
                }
            }

            internal double Scale => Real(46, 0.0);

            internal double Rotation => Real(50, 0.0);

            internal double X => Real(44, 0.0);

            internal double Y => Real(45, 0.0);

            internal string Text() => Text(9) ?? string.Empty;

            /// <summary>Whether the dash carries a shape or a text, or any group of one.</summary>
            internal bool IsComplex => (Whole(74, 0) & 7) != 0 || Groups.Exists(g => g.Key != 49 && g.Key != 74);

            internal static Dash Simple(double length)
            {
                var dash = new Dash();
                dash.Groups.Add(new KeyValuePair<int, string>(49, Numbers.Exact(length + 0.0)));
                dash.Groups.Add(new KeyValuePair<int, string>(74, "0"));
                return dash;
            }

            internal Values Read() => new Values
            {
                Length = Length,
                Absolute = Absolute,
                Shape = Shape,
                Style = Style,
                Scale = Scale,
                Rotation = Rotation,
                X = X,
                Y = Y,
                Text = Text(),
            };

            /// <summary>The dash written with values, each group whose value is as it was keeping its text.</summary>
            internal Dash Written(Values values, bool markers)
            {
                bool text = values.Text.Length > 0;
                bool complex = text || values.Shape != 0 || values.Style != null || values.Absolute || values.Scale != 0.0 || values.Rotation != 0.0 || values.X != 0.0
                    || values.Y != 0.0 || IsComplex;
                var dash = new Dash();
                dash.Real(this, 49, values.Length);

                if (markers || Text(74) != null)
                {
                    int flags = (values.Absolute ? 1 : 0) | (text ? 2 : values.Shape != 0 ? 4 : 0);
                    dash.Whole(this, 74, flags);
                }

                if (!complex)
                {
                    return dash;
                }

                dash.Whole(this, 75, text ? 0 : values.Shape);

                if (values.Style != null)
                {
                    dash.Groups.Add(new KeyValuePair<int, string>(340, string.Equals(Style, values.Style, StringComparison.OrdinalIgnoreCase) ? Text(340) : values.Style));
                }

                // A dash made complex with no scale is drawn at the scale 1, as a linetype file's shape or text without S is.
                dash.Real(this, 46, values.Scale == 0.0 && Text(46) == null ? 1.0 : values.Scale);
                dash.Real(this, 50, values.Rotation);
                dash.Real(this, 44, values.X);
                dash.Real(this, 45, values.Y);

                if (text)
                {
                    dash.Groups.Add(new KeyValuePair<int, string>(9, values.Text));
                }

                return dash;
            }

            internal bool Same(Dash other)
            {
                if (other.Groups.Count != Groups.Count)
                {
                    return false;
                }

                for (int i = 0; i < Groups.Count; i++)
                {
                    if (Groups[i].Key != other.Groups[i].Key || !string.Equals(Groups[i].Value, other.Groups[i].Value, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            private string Text(int code)
            {
                foreach (KeyValuePair<int, string> group in Groups)
                {
                    if (group.Key == code)
                    {
                        return group.Value;
                    }
                }

                return null;
            }

            private double Real(int code, double fallback) => Numbers.TryRead(Text(code), out double value) && Guard.IsFinite(value) ? value : fallback;

            private int Whole(int code, int fallback) => int.TryParse(Text(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

            /// <summary>A number written after the groups so far: the text it had where it is the same to the bit.</summary>
            private void Real(Dash was, int code, double value)
            {
                string text = was.Text(code);
                Groups.Add(new KeyValuePair<int, string>(code, text != null && Numbers.TryRead(text, out double old) && DxfLinetypeTableRecord.Same(old, value) ? text : Numbers.Exact(value + 0.0)));
            }

            private void Whole(Dash was, int code, int value)
            {
                string text = was.Text(code);
                Groups.Add(new KeyValuePair<int, string>(code, text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int old) && old == value ? text : value.ToString(CultureInfo.InvariantCulture)));
            }

            /// <summary>What a dash is, to be changed and written.</summary>
            internal sealed class Values
            {
                internal double Length { get; set; }

                internal bool Absolute { get; set; }

                internal int Shape { get; set; }

                internal string Style { get; set; }

                internal double Scale { get; set; }

                internal double Rotation { get; set; }

                internal double X { get; set; }

                internal double Y { get; set; }

                internal string Text { get; set; }
            }
        }
    }
}
