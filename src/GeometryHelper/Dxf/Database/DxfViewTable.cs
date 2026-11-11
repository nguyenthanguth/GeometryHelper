using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The VIEW table of a DXF file, as AutoCAD's <c>ViewTable</c>: the named views, each a <see cref="DxfViewTableRecord"/>,
    /// in the order of the file, found by name, added to and erased from.
    /// </summary>
    /// <remarks>
    /// <c>db.ViewTable.Add(new DxfViewTableRecord { Name = "Stair core", CenterPoint = new GeoPoint2(18450, 6200), Height = 9000,
    /// Width = 14400 })</c> adds a view of model space 14 400 wide and 9 000 high about (18 450, 6 200), written as AutoCAD
    /// 2021 writes one its -VIEW Window saved: looking down Z at the origin with a lens of 50, no clipping and no twist.
    /// </remarks>
    public sealed class DxfViewTable : DxfSymbolTable, IEnumerable<DxfViewTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfViewTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>Gets the view of a name, letters of either case the same.</summary>
        /// <param name="key">The name.</param>
        /// <returns>The view.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no view of the name.</exception>
        public new DxfViewTableRecord this[string key] => (DxfViewTableRecord)base[key];

        /// <summary>Gets an enumerator over the views, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfViewTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("VIEW").ToArray())
            {
                if (!item.IsRemoved)
                {
                    yield return DxfViewTableRecord.Of(item);
                }
            }
        }

        IEnumerator<DxfViewTableRecord> IEnumerable<DxfViewTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfViewTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfViewTableRecord view))
            {
                throw new ArgumentException(
                    "The VIEW table takes a DxfViewTableRecord, and was given a " + record.GetType().Name + ".",
                    "value"
                );
            }

            view.AddTo(_file);
        }
    }

    /// <summary>
    /// A named view of a DXF file, as AutoCAD's <c>ViewTableRecord</c>, read from its VIEW record: its name, the point at its
    /// centre and its height and width in the units of its space, whether it is of paper space, and the layer state its layer
    /// snapshot is kept in.
    /// </summary>
    /// <remarks>
    /// AutoCAD 2021's -VIEW Window of the corners (10, 20) and (110, 80) saves the view of the centre (60, 50), 100 wide and
    /// 60 high. A view of a file is changed by its setters, its record written anew, every other group kept; a setter given
    /// the value its getter gives changes nothing. A view made in code is of model space, about the origin, until it is given
    /// others, and is written when it is added to a <see cref="DxfViewTable"/>.
    /// </remarks>
    public sealed class DxfViewTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbViewTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[]
            {
                100,
                100,
                2,
                70,
                40,
                10,
                20,
                41,
                11,
                21,
                31,
                12,
                22,
                32,
                42,
                43,
                44,
                50,
                71,
                281,
                72,
                110,
                120,
                130,
                111,
                121,
                131,
                112,
                122,
                132,
                79,
                146,
                345,
                346,
                73,
                332,
                334,
                348,
                361,
            }
        );

        // The extension dictionary's entry AutoCAD 2021 keeps a view's layer snapshot in, its state named by group 302.
        private const string ViewInfo = "ADSK_XREC_VTRVIEWINFO";

        private GeoPoint2 _center = GeoPoint2.Origin;
        private double _height = 1.0;
        private double _width = 1.0;
        private bool _paper;

        /// <summary>Initializes a view made in code, of model space about the origin, to be added to a <see cref="DxfViewTable"/> with its <see cref="DxfSymbolTableRecord.Name"/> given.</summary>
        public DxfViewTableRecord() { }

        private DxfViewTableRecord(DxfItem item)
            : base(item) { }

        /// <summary>Gets or sets the point at the centre of the view (groups 10, 20), as AutoCAD's <c>CenterPoint</c>, in the display coordinates of its space.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the view is erased.</exception>
        public GeoPoint2 CenterPoint
        {
            get => Item == null ? _center : new GeoPoint2(Real(10, 0.0), Real(20, 0.0));
            set
            {
                if (!(Guard.IsFinite(value.X) && Guard.IsFinite(value.Y)))
                {
                    throw new ArgumentException(
                        "A point has to be of finite numbers: " + value + " is not.",
                        nameof(value)
                    );
                }

                if (Item == null)
                {
                    _center = value;
                }
                else if (!(Same(value.X, CenterPoint.X) && Same(value.Y, CenterPoint.Y)))
                {
                    SetValues(
                        new KeyValuePair<int, string>(10, Numbers.Exact(value.X + 0.0)),
                        new KeyValuePair<int, string>(20, Numbers.Exact(value.Y + 0.0))
                    );
                }
            }
        }

        /// <summary>Gets or sets the height of the view (group 40), as AutoCAD's <c>Height</c>, in the units of its space.</summary>
        /// <exception cref="ArgumentException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the view is erased.</exception>
        public double Height
        {
            get => Item == null ? _height : Real(40, 1.0);
            set => SetSize(40, value, Height, ref _height);
        }

        /// <summary>Gets or sets the width of the view (group 41), as AutoCAD's <c>Width</c>, in the units of its space.</summary>
        /// <exception cref="ArgumentException">Thrown when the width set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the view is erased.</exception>
        public double Width
        {
            get => Item == null ? _width : Real(41, 1.0);
            set => SetSize(41, value, Width, ref _width);
        }

        /// <summary>
        /// Gets or sets whether the view is of paper space (flag 1 of group 70), as AutoCAD's <c>IsPaperspaceView</c>: a layout
        /// view, which -VIEW ? lists with a P.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the view is erased.</exception>
        public bool IsPaperspaceView
        {
            get => Item == null ? _paper : (Flags & 1) != 0;
            set
            {
                if (Item == null)
                {
                    _paper = value;
                }
                else if (value != IsPaperspaceView)
                {
                    int flags = value ? Flags | 1 : Flags & ~1;
                    SetValues(new KeyValuePair<int, string>(70, flags.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        /// <summary>Gets the target of the view (groups 12, 22, 32), as AutoCAD's <c>Target</c>, in world coordinates; the origin where the record gives none.</summary>
        public GeoPoint3 Target =>
            Item == null ? GeoPoint3.Origin : new GeoPoint3(Real(12, 0.0), Real(22, 0.0), Real(32, 0.0));

        /// <summary>Gets the direction the view looks from towards its target (groups 11, 21, 31), as AutoCAD's <c>ViewDirection</c>; Z where the record gives none.</summary>
        public GeoVector3 ViewDirection =>
            Item == null ? GeoVector3.ZAxis : new GeoVector3(Real(11, 0.0), Real(21, 0.0), Real(31, 1.0));

        /// <summary>Gets the twist of the view about its direction (group 50), as AutoCAD's <c>ViewTwist</c>, in radians.</summary>
        public double ViewTwist => Item == null ? 0.0 : Real(50, 0.0) * Math.PI / 180.0;

        /// <summary>
        /// Gets or sets the layer state the view's layer snapshot is kept in, as AutoCAD's <c>LayerState</c>: the group 302 of
        /// the XRECORD ADSK_XREC_VTRVIEWINFO of its extension dictionary, ACAD_VIEWS_ and the view's name as AutoCAD 2021 names
        /// the hidden state it saves; empty where the view keeps none.
        /// </summary>
        /// <value>
        /// Set, written as AutoCAD 2021 writes it: the XRECORD's 290 of nought, the 302 and its 293 and 294 of nought, the
        /// extension dictionary made where the view has none; empty takes the 302 out. The state itself is the drawing's, kept
        /// by <see cref="DxfDatabase.LayerStateManager"/>.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a view in no file, or one erased.</exception>
        public string LayerState
        {
            get
            {
                DxfXrecord info = Info();
                return info?.Data.Where(g => g.TypeCode == 302).Select(g => g.Value).FirstOrDefault() ?? string.Empty;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (Item == null || IsErased)
                {
                    throw new InvalidOperationException(
                        "The view " + Name + " is in no file, and is given no layer snapshot."
                    );
                }

                if (string.Equals(value, LayerState, StringComparison.Ordinal))
                {
                    return;
                }

                var data = new List<DxfTypedValue> { new DxfTypedValue(290, "0") };

                if (value.Length > 0)
                {
                    data.Add(new DxfTypedValue(302, value));
                    data.Add(new DxfTypedValue(293, "0"));
                }

                data.Add(new DxfTypedValue(294, "0"));
                DxfXrecord info = Info();

                if (info != null)
                {
                    info.Data = data;
                    return;
                }

                CreateExtensionDictionary();
                ExtensionDictionary.SetAt(ViewInfo, new DxfXrecord { Data = data });
            }
        }

        /// <summary>
        /// Erases the view from its table, as AutoCAD's <c>Erase</c> of a named view: its extension dictionary erased with what it
        /// holds, its VIEW record taken out and the table's count lowered by one.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the view is in no file or is erased already.</exception>
        public override void Erase()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The view " + Name + " is in no file to be erased from.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The view " + Name + " is erased already.");
            }

            DxfDBDictionary extension = ExtensionDictionary;

            if (extension != null)
            {
                foreach (string key in extension.Select(e => e.Key).ToList())
                {
                    extension.Remove(key);
                }

                ReleaseExtensionDictionary();
            }

            Item.File.RemoveTableRecord(Item, "VIEW");
        }

        /// <summary>The object of a VIEW record, made once.</summary>
        internal static DxfViewTableRecord Of(DxfItem item) =>
            item.Wrapper as DxfViewTableRecord ?? (DxfViewTableRecord)item.KeepWrapper(new DxfViewTableRecord(item));

        /// <summary>Writes the view made in code into a file's VIEW table, as AutoCAD 2021 writes one, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            bool since2000 = file.Supports(1015);
            bool since2007 = file.Supports(1021);
            string wireframe = since2007 ? VisualStyle(file) : null;
            DxfItem item = file.AddTableRecord("VIEW", "AcDbViewTableRecord", b =>
            {
                b.Group(2, Name);
                b.Group(70, _paper ? "1" : "0");
                b.Group(40, Numbers.Exact(_height));
                b.Group(10, Numbers.Exact(_center.X + 0.0));
                b.Group(20, Numbers.Exact(_center.Y + 0.0));
                b.Group(41, Numbers.Exact(_width));
                b.Group(11, "0.0");
                b.Group(21, "0.0");
                b.Group(31, "1.0");
                b.Group(12, "0.0");
                b.Group(22, "0.0");
                b.Group(32, "0.0");
                b.Group(42, "50.0");
                b.Group(43, "0.0");
                b.Group(44, "0.0");
                b.Group(50, "0.0");
                b.Group(71, "0");

                if (since2000)
                {
                    b.Group(281, "0");
                    b.Group(72, "0");
                }

                if (since2007)
                {
                    b.Group(73, "0");

                    if (wireframe != null)
                    {
                        b.Group(348, wireframe);
                    }
                }
            });

            if (item == null)
            {
                throw new InvalidOperationException("The file has no VIEW table to add the view " + Name + " to.");
            }

            Bind(item);
        }

        /// <summary>Renames the view: its VIEW record's 2.</summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");

            if (IsErased)
            {
                throw new InvalidOperationException("The view " + Name + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;

            foreach (DxfItem other in file.TableItems("VIEW"))
            {
                if (
                    other != Item
                    && !other.IsRemoved
                    && string.Equals(
                        DxfReader.NameOf(file.ValueOf(other, 2) ?? string.Empty),
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    throw new ArgumentException(
                        "The VIEW table has a view named " + name + " already, letters of either case the same.",
                        "value"
                    );
                }
            }

            file.SetValues(Item, new[] { new KeyValuePair<int, string>(2, name) }, Order, false);
        }

        /// <summary>The handle of the visual style 2D Wireframe AutoCAD 2021 points a view at; null where the drawing has none.</summary>
        private static string VisualStyle(DxfFile file)
        {
            string handle = file.HoldsObjects ? file.RootDictionaryHandle() : null;
            DxfItem item = handle == null ? null : file.ObjectItem(handle);
            DxfDBDictionary styles =
                item != null && DxfDBObject.OfObject(item) is DxfDBDictionary root && root.Contains("ACAD_VISUALSTYLE")
                    ? root.GetAt("ACAD_VISUALSTYLE") as DxfDBDictionary
                    : null;
            return styles != null && styles.Contains("2dWireframe") ? styles.GetAt("2dWireframe")?.Handle : null;
        }

        private DxfXrecord Info()
        {
            DxfDBDictionary extension = Item == null ? null : ExtensionDictionary;
            return extension != null && extension.Contains(ViewInfo) ? extension.GetAt(ViewInfo) as DxfXrecord : null;
        }

        private void SetSize(int code, double value, double now, ref double field)
        {
            if (!(Guard.IsFinite(value) && value > 0.0))
            {
                throw new ArgumentException(
                    "A view's height and width are finite numbers above nought: "
                        + value.ToString("R", CultureInfo.InvariantCulture)
                        + " is not.",
                    nameof(value)
                );
            }

            if (Item == null)
            {
                field = value;
            }
            else if (!Same(value, now))
            {
                SetValues(new KeyValuePair<int, string>(code, Numbers.Exact(value)));
            }
        }

        private void SetValues(params KeyValuePair<int, string>[] values)
        {
            if (IsErased)
            {
                throw new InvalidOperationException("The view " + Name + " is erased, and is not changed.");
            }

            Item.File.SetValues(Item, values, Order, false);
        }

        private double Real(int code, double fallback) =>
            Numbers.TryRead(Item.File.ValueOf(Item, code), out double value) && Guard.IsFinite(value)
                ? value
                : fallback;

        private static bool Same(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
