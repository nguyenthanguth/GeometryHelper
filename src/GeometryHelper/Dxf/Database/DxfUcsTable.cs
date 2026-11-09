using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The UCS table of a DXF file, as AutoCAD's <c>UcsTable</c>: the named user coordinate systems, each a
    /// <see cref="DxfUcsTableRecord"/>, in the order of the file, found by name, added to and erased from.
    /// </summary>
    /// <remarks>
    /// <c>db.UcsTable.Add(new DxfUcsTableRecord { Name = "Slab", Origin = new GeoPoint3(100, 50, 0), XAxis = x, YAxis = y })</c>
    /// adds the system Slab, written as AutoCAD writes one: its name, its origin (10), its X axis (11) and its Y axis (12), and
    /// from R2000 on its orthographic type (79) of nought and its elevation (146) of nought. The header names the current
    /// system by the name of its record, as <see cref="DxfDatabase.Ucsname"/> says.
    /// </remarks>
    public sealed class DxfUcsTable : DxfSymbolTable, IEnumerable<DxfUcsTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfUcsTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>Gets the system of a name, letters of either case the same.</summary>
        /// <param name="key">The name.</param>
        /// <returns>The system.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no system of the name.</exception>
        public new DxfUcsTableRecord this[string key] => (DxfUcsTableRecord)base[key];

        /// <summary>Gets an enumerator over the systems, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfUcsTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("UCS").ToArray())
            {
                if (!item.IsRemoved)
                {
                    yield return DxfUcsTableRecord.Of(item);
                }
            }
        }

        IEnumerator<DxfUcsTableRecord> IEnumerable<DxfUcsTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfUcsTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfUcsTableRecord ucs))
            {
                throw new ArgumentException("The UCS table takes a DxfUcsTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            ucs.AddTo(_file);
        }
    }

    /// <summary>
    /// A named user coordinate system of a DXF file, as AutoCAD's <c>UcsTableRecord</c>, read from its UCS record: its name,
    /// its origin and its X and Y axes, in world coordinates.
    /// </summary>
    /// <remarks>
    /// A system turned 30 degrees about Z at (100, 50, 0) has the <see cref="Origin"/> (100, 50, 0), the <see cref="XAxis"/>
    /// (0.866, 0.5, 0) and the <see cref="YAxis"/> (-0.5, 0.866, 0). A system of a file is changed by its setters, its record
    /// written anew, every other group kept; a setter given the value its getter gives changes nothing. A system made in code
    /// is the world's until it is given others, and is written when it is added to a <see cref="DxfUcsTable"/>.
    /// </remarks>
    public sealed class DxfUcsTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbUCSTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 10, 20, 30, 11, 21, 31, 12, 22, 32, 79, 146, 346, 71, 13, 23, 33 });

        // The header variables that name a system of the table.
        private static readonly string[] Naming = { "$UCSNAME", "$PUCSNAME", "$UCSBASE", "$PUCSBASE" };

        private GeoPoint3 _origin = GeoPoint3.Origin;
        private GeoVector3 _xAxis = GeoVector3.XAxis;
        private GeoVector3 _yAxis = GeoVector3.YAxis;

        /// <summary>Initializes a system made in code, the world's, to be added to a <see cref="DxfUcsTable"/> with its <see cref="DxfSymbolTableRecord.Name"/> given.</summary>
        public DxfUcsTableRecord()
        {
        }

        private DxfUcsTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the origin of the system (groups 10, 20, 30) in world coordinates, as AutoCAD's <c>Origin</c>; the world's origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the system is erased.</exception>
        public GeoPoint3 Origin
        {
            get => Item == null ? _origin : Triple(10, 0.0, 0.0, 0.0, (x, y, z) => new GeoPoint3(x, y, z));

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                GeoPoint3 now = Origin;

                if (Item == null)
                {
                    _origin = value;
                }
                else if (!(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
                {
                    SetTriple(10, value.X, value.Y, value.Z);
                }
            }
        }

        /// <summary>Gets or sets the X axis of the system (groups 11, 21, 31) in world coordinates, as AutoCAD's <c>XAxis</c>; the world's X axis where the record gives none.</summary>
        /// <value>Set, a direction of finite numbers and some length, written as given.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set is not of finite numbers or has no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the system is erased.</exception>
        public GeoVector3 XAxis
        {
            get => Item == null ? _xAxis : Triple(11, 1.0, 0.0, 0.0, (x, y, z) => new GeoVector3(x, y, z));
            set => SetAxis(11, value, XAxis, ref _xAxis);
        }

        /// <summary>Gets or sets the Y axis of the system (groups 12, 22, 32) in world coordinates, as AutoCAD's <c>YAxis</c>; the world's Y axis where the record gives none.</summary>
        /// <value>Set, a direction of finite numbers and some length, written as given.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set is not of finite numbers or has no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the system is erased.</exception>
        public GeoVector3 YAxis
        {
            get => Item == null ? _yAxis : Triple(12, 0.0, 1.0, 0.0, (x, y, z) => new GeoVector3(x, y, z));
            set => SetAxis(12, value, YAxis, ref _yAxis);
        }

        /// <summary>
        /// Erases the system from its table, as AutoCAD's <c>Erase</c> of a named UCS: its UCS record taken out, the table's
        /// count lowered by one, and a header variable that named it (<c>$UCSNAME</c>, <c>$PUCSNAME</c>, <c>$UCSBASE</c>,
        /// <c>$PUCSBASE</c>) left naming none, as AutoCAD leaves the current system unnamed when its name is deleted.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the system is in no file or is erased already, or when an
        /// object of the file points at it, as a viewport whose system it is.</exception>
        public override void Erase()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The UCS " + Name + " is in no file to be erased from.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The UCS " + Name + " is erased already.");
            }

            string name = Name;

            if (Item.File.PointedAt(Handle))
            {
                throw new InvalidOperationException("The UCS " + name + " is pointed at by an object of the file, as a viewport whose system it is, and is not erased.");
            }

            DxfFile file = Item.File;
            file.RemoveTableRecord(Item, "UCS");
            NameAgain(file, name, string.Empty);
        }

        /// <summary>The object of a UCS record, made once.</summary>
        internal static DxfUcsTableRecord Of(DxfItem item) => item.Wrapper as DxfUcsTableRecord ?? (DxfUcsTableRecord)item.KeepWrapper(new DxfUcsTableRecord(item));

        /// <summary>Writes the system made in code into a file's UCS table, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            bool since2000 = file.Supports(1015);
            DxfItem item = file.AddTableRecord("UCS", "AcDbUCSTableRecord", b =>
            {
                b.Group(2, Name);
                b.Group(70, "0");
                Write(b, 10, _origin.X, _origin.Y, _origin.Z);
                Write(b, 11, _xAxis.X, _xAxis.Y, _xAxis.Z);
                Write(b, 12, _yAxis.X, _yAxis.Y, _yAxis.Z);

                if (since2000)
                {
                    b.Group(79, "0");
                    b.Group(146, "0");
                }
            });

            if (item == null)
            {
                throw new InvalidOperationException("The file has no UCS table to add the system " + Name + " to.");
            }

            Bind(item);
        }

        /// <summary>Renames the system: its UCS record's 2, and each header variable that names it.</summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");

            if (IsErased)
            {
                throw new InvalidOperationException("The UCS " + Name + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;
            string old = Name;

            foreach (DxfItem other in file.TableItems("UCS"))
            {
                if (other != Item && !other.IsRemoved && string.Equals(DxfReader.NameOf(file.ValueOf(other, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The UCS table has a system named " + name + " already, letters of either case the same.", "value");
                }
            }

            file.SetValues(Item, new[] { new KeyValuePair<int, string>(2, name) }, Order, false);
            NameAgain(file, old, name);
        }

        /// <summary>Each header variable that names a system by its old name named by its new one, or by none where that is empty.</summary>
        private static void NameAgain(DxfFile file, string old, string name)
        {
            foreach (string variable in Naming)
            {
                IReadOnlyList<DxfTypedValue> groups = file.HeaderOf(variable);

                if (groups != null && groups.Count > 0 && string.Equals(DxfReader.NameOf(groups[0].Value ?? string.Empty), old, StringComparison.OrdinalIgnoreCase))
                {
                    file.SetHeader(variable, new[] { new DxfTypedValue(2, name) });
                }
            }
        }

        private void SetAxis(int code, GeoVector3 value, GeoVector3 now, ref GeoVector3 field)
        {
            if (!(Guard.IsFinite(value.X) && Guard.IsFinite(value.Y) && Guard.IsFinite(value.Z)) || value.Length <= 0.0)
            {
                throw new ArgumentException("An axis has to be a direction of finite numbers and some length: " + value + " is not.", nameof(value));
            }

            if (Item == null)
            {
                field = value;
            }
            else if (!(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
            {
                SetTriple(code, value.X, value.Y, value.Z);
            }
        }

        private void SetTriple(int code, double x, double y, double z)
            => Item.File.SetValues(Item, new[]
            {
                new KeyValuePair<int, string>(code, Numbers.Exact(x + 0.0)),
                new KeyValuePair<int, string>(code + 10, Numbers.Exact(y + 0.0)),
                new KeyValuePair<int, string>(code + 20, Numbers.Exact(z + 0.0)),
            }, Order, false);

        private T Triple<T>(int code, double x, double y, double z, Func<double, double, double, T> make)
            => make(Real(code, x), Real(code + 10, y), Real(code + 20, z));

        private double Real(int code, double fallback) => Numbers.TryRead(Item.File.ValueOf(Item, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private static void Write(DxfFile.Builder b, int code, double x, double y, double z)
        {
            b.Group(code, Numbers.Exact(x + 0.0));
            b.Group(code + 10, Numbers.Exact(y + 0.0));
            b.Group(code + 20, Numbers.Exact(z + 0.0));
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
