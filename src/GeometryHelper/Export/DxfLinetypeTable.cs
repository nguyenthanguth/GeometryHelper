using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
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
    /// pattern and its dashes, a dash above nought drawn, below nought a gap, nought a dot.
    /// </summary>
    /// <remarks>
    /// A linetype of a file is read from its LTYPE record; its <see cref="AsciiDescription"/> and <see cref="PatternLength"/>
    /// are set as any record's are, and its dashes are not changed yet. One made in code is given its dashes by
    /// <see cref="NumDashes"/> and <see cref="SetDashLengthAt(int, double)"/>, and is written when it is added to a
    /// <see cref="DxfLinetypeTable"/>; its pattern is as long as its dashes and gaps together unless it is given a length.
    /// </remarks>
    public sealed class DxfLinetypeTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbLinetypeTableRecord before its dashes, in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 3, 72, 73, 40 });

        private string _description = string.Empty;
        private double? _length;
        private double[] _dashes = new double[0];

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

                    foreach (double dash in _dashes)
                    {
                        sum += Math.Abs(dash);
                    }

                    return sum;
                }

                return Numbers.TryRead(Item.File.ValueOf(Item, 40), out double length) && Guard.IsFinite(length) ? length : 0.0;
            }

            set
            {
                // The length it has changes nothing, whatever the file holds.
                if (Item != null && BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(PatternLength))
                {
                    return;
                }

                Guard.NonNegative(value, nameof(value), "The length of a pattern has to be a finite number of nought or more.");

                if (Item == null)
                {
                    _length = value;
                }
                else if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(PatternLength))
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
        /// <exception cref="InvalidOperationException">Thrown when set on a linetype of a file: its dashes are not changed yet.</exception>
        public int NumDashes
        {
            get => Item == null ? _dashes.Length : Dashes().Count;

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

                Unread();
                Array.Resize(ref _dashes, value);
            }
        }

        /// <summary>
        /// Gets the length of a dash of the pattern (the groups 49 in their order), as AutoCAD's <c>DashLengthAt</c>: above
        /// nought a dash, below nought a gap, nought a dot.
        /// </summary>
        /// <param name="index">The dash, from nought.</param>
        /// <returns>Its length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or not below <see cref="NumDashes"/>.</exception>
        public double DashLengthAt(int index)
        {
            IReadOnlyList<double> dashes = Item == null ? (IReadOnlyList<double>)_dashes : Dashes();

            if (index < 0 || index >= dashes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The pattern has " + dashes.Count.ToString(CultureInfo.InvariantCulture) + " dashes.");
            }

            return dashes[index];
        }

        /// <summary>
        /// Sets the length of a dash of the pattern of a linetype made in code, as AutoCAD's <c>SetDashLengthAt</c>.
        /// </summary>
        /// <param name="index">The dash, from nought to <see cref="NumDashes"/> less one.</param>
        /// <param name="value">Its length: above nought a dash, below nought a gap, nought a dot.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of the pattern, or the length is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown on a linetype of a file: its dashes are not changed yet.</exception>
        public void SetDashLengthAt(int index, double value)
        {
            Guard.Finite(value, nameof(value), "The length of a dash has to be a finite number.");

            if (index < 0 || index >= NumDashes)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The pattern has " + NumDashes.ToString(CultureInfo.InvariantCulture) + " dashes.");
            }

            if (Item != null && BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(DashLengthAt(index)))
            {
                return;
            }

            Unread();
            _dashes[index] = value;
        }

        /// <summary>The object of an LTYPE record, made once.</summary>
        internal static DxfLinetypeTableRecord Of(DxfItem item) => item.Wrapper as DxfLinetypeTableRecord ?? (DxfLinetypeTableRecord)item.KeepWrapper(new DxfLinetypeTableRecord(item));

        /// <summary>Writes the linetype made in code into a file's linetype table, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            DxfItem item = file.AddLinetype(Name, _description, PatternLength, _dashes);

            if (item == null)
            {
                throw new InvalidOperationException("The file has no linetype table to add the linetype " + Name + " to.");
            }

            Bind(item);
        }

        /// <summary>The lengths of the dashes of the record: its groups 49 in their order.</summary>
        private List<double> Dashes()
        {
            var dashes = new List<double>();

            foreach (DxfTypedValue group in Item.Groups)
            {
                if (group.TypeCode == 49)
                {
                    dashes.Add(Numbers.TryRead(group.Value, out double dash) ? dash : 0.0);
                }
            }

            return dashes;
        }

        private void Unread()
        {
            if (Item != null)
            {
                throw new InvalidOperationException("The dashes of the linetype " + Name + " of a file are not changed yet: a later release changes them.");
            }
        }
    }
}
