using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The VPORT table of a DXF file, as AutoCAD's <c>ViewportTable</c>: the tiled viewports of model space, each a
    /// <see cref="DxfViewportTableRecord"/>, in the order of the file, found by name. The viewport model space is shown
    /// through is the first named <c>*Active</c>, <c>db.ViewportTable["*Active"]</c>; a configuration saved by VPORTS Save is
    /// named as it was saved.
    /// </summary>
    /// <remarks>
    /// The records are read and changed in place; none is added yet, as a drawing has its <c>*Active</c> from the start.
    /// </remarks>
    public sealed class DxfViewportTable : DxfSymbolTable, IEnumerable<DxfViewportTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfViewportTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>Gets the viewport of a name, letters of either case the same: the first of them where several share it, as the <c>*Active</c> viewports of a model space split in tiles do.</summary>
        /// <param name="key">The name.</param>
        /// <returns>The viewport.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no viewport of the name.</exception>
        public new DxfViewportTableRecord this[string key] => (DxfViewportTableRecord)base[key];

        /// <summary>Gets an enumerator over the viewports, in the order of the file.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfViewportTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("VPORT").ToArray())
            {
                if (!item.IsRemoved)
                {
                    yield return DxfViewportTableRecord.Of(item);
                }
            }
        }

        IEnumerator<DxfViewportTableRecord> IEnumerable<DxfViewportTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfViewportTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record) =>
            throw new InvalidOperationException(
                "The VPORT table is read and changed in place: no viewport is added to it yet."
            );
    }

    /// <summary>
    /// A tiled viewport of model space, as AutoCAD's <c>ViewportTableRecord</c>, read from its VPORT record: the snap and the
    /// grid AutoCAD keeps there for the drawing, SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE, SNAPANG, SNAPSTYL,
    /// SNAPISOPAIR, GRIDDISPLAY and GRIDMAJOR of the <c>*Active</c> viewport.
    /// </summary>
    /// <remarks>
    /// AutoCAD 2021 opened a drawing whose <c>*Active</c> has the groups 75 and 76 of 1, 14 and 24 of 2.5 and 5, 15 and 25 of 10
    /// and 20, 13 and 23 of 1 and 2, 50 of 15, 60 of 5 and 61 of 7 with SNAPMODE 1, GRIDMODE 1, SNAPUNIT (2.5, 5), GRIDUNIT
    /// (10, 20), SNAPBASE (1, 2), SNAPANG 15 degrees, GRIDDISPLAY 5 and GRIDMAJOR 7; one with none of 60 and 61 with
    /// GRIDDISPLAY 3 and GRIDMAJOR 5, which these properties give too. A setter writes its groups in place, every other group
    /// kept, so that the drawing saved keeps the value, as AutoCAD saves it; a setter given the value its getter gives changes
    /// nothing.
    /// </remarks>
    public sealed class DxfViewportTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbViewportTableRecord in the order AutoCAD 2021 gives them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[]
            {
                100,
                100,
                2,
                70,
                10,
                20,
                11,
                21,
                12,
                22,
                13,
                23,
                14,
                24,
                15,
                25,
                16,
                26,
                36,
                17,
                27,
                37,
                40,
                41,
                42,
                43,
                44,
                50,
                51,
                71,
                72,
                73,
                74,
                75,
                76,
                77,
                78,
                281,
                65,
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
                348,
                60,
                61,
                292,
                282,
                141,
                142,
                63,
                421,
                431,
                332,
                333,
                361,
            }
        );

        private DxfViewportTableRecord(DxfItem item)
            : base(item) { }

        /// <summary>Gets or sets whether the snap is on (group 75 of 1), as AutoCAD's <c>SnapEnabled</c> and SNAPMODE; off where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool SnapEnabled
        {
            get => Whole(75, 0) != 0;
            set => SetWhole(75, value ? 1 : 0, SnapEnabled ? 1 : 0);
        }

        /// <summary>Gets or sets whether the grid is shown (group 76 of 1), as AutoCAD's <c>GridEnabled</c> and GRIDMODE; off where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool GridEnabled
        {
            get => Whole(76, 0) != 0;
            set => SetWhole(76, value ? 1 : 0, GridEnabled ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the spacing of the snap across and up (groups 14 and 24), as AutoCAD's <c>SnapIncrements</c> and
        /// SNAPUNIT; 10 by 10 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a spacing set is not a finite number above nought, as SNAPUNIT takes none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoVector2 SnapIncrements
        {
            get => new GeoVector2(Real(14, 10.0), Real(24, 10.0));
            set => SetPair(14, 24, value.X, value.Y, SnapIncrements.X, SnapIncrements.Y, false);
        }

        /// <summary>
        /// Gets or sets the spacing of the grid across and up (groups 15 and 25), as AutoCAD's <c>GridIncrements</c> and
        /// GRIDUNIT; 10 by 10 where the record gives none. Nought along an axis takes the snap's spacing there, as GRIDUNIT of
        /// nought does.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a spacing set is not a finite number, or is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoVector2 GridIncrements
        {
            get => new GeoVector2(Real(15, 10.0), Real(25, 10.0));
            set => SetPair(15, 25, value.X, value.Y, GridIncrements.X, GridIncrements.Y, true);
        }

        /// <summary>Gets or sets the point the snap and the grid are laid from (groups 13 and 23), as AutoCAD's <c>SnapBase</c> and SNAPBASE; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoPoint2 SnapBase
        {
            get => new GeoPoint2(Real(13, 0.0), Real(23, 0.0));
            set
            {
                if (!(Guard.IsFinite(value.X) && Guard.IsFinite(value.Y)))
                {
                    throw new ArgumentException(
                        "A point has to be of finite numbers: " + value + " is not.",
                        nameof(value)
                    );
                }

                GeoPoint2 now = SnapBase;

                if (!(Same(value.X, now.X) && Same(value.Y, now.Y)))
                {
                    SetValues(
                        new KeyValuePair<int, string>(13, Numbers.Exact(value.X + 0.0)),
                        new KeyValuePair<int, string>(23, Numbers.Exact(value.Y + 0.0))
                    );
                }
            }
        }

        /// <summary>
        /// Gets or sets the angle the snap and the grid are turned by (group 50, written in degrees), as AutoCAD's
        /// <c>SnapAngle</c> and SNAPANG, in radians; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double SnapAngle
        {
            get => Real(50, 0.0) * Math.PI / 180.0;
            set
            {
                if (!Guard.IsFinite(value))
                {
                    throw new ArgumentException(
                        "An angle has to be a finite number: "
                            + value.ToString("R", CultureInfo.InvariantCulture)
                            + " is not.",
                        nameof(value)
                    );
                }

                if (!Same(value, SnapAngle))
                {
                    SetValues(new KeyValuePair<int, string>(50, Numbers.Exact(value * 180.0 / Math.PI + 0.0)));
                }
            }
        }

        /// <summary>Gets or sets whether the snap is isometric (group 77 of 1), as AutoCAD's <c>IsometricSnapEnabled</c> and SNAPSTYL of 1; rectangular where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool IsometricSnapEnabled
        {
            get => Whole(77, 0) != 0;
            set => SetWhole(77, value ? 1 : 0, IsometricSnapEnabled ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the isometric plane the snap works in (group 78), as AutoCAD's <c>SnapPair</c> and SNAPISOPAIR: 0 left,
        /// 1 top, 2 right; 0 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the plane set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public int SnapPair
        {
            get => Whole(78, 0);
            set
            {
                if (value < 0 || value > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "SNAPISOPAIR is 0, 1 or 2.");
                }

                SetWhole(78, value, SnapPair);
            }
        }

        /// <summary>
        /// Gets or sets how the grid is shown (group 60), as AutoCAD's GRIDDISPLAY: 1 beyond the limits, 2 adaptive, 4
        /// subdivided below its spacing when zoomed in, 8 following the dynamic UCS; 3 where the record gives none, as
        /// AutoCAD 2021 reads such a drawing. The group came with R2007 (AC1021).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0 to 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007, or the viewport is erased.</exception>
        public int GridDisplay
        {
            get => Whole(60, 3);
            set => SetSince2007(60, value, 0, 15, GridDisplay, "GRIDDISPLAY");
        }

        /// <summary>
        /// Gets or sets how many minor lines of the grid go to a major one (group 61), as AutoCAD's <c>GridMajor</c> and GRIDMAJOR;
        /// 5 where the record gives none, as AutoCAD 2021 reads such a drawing. The group came with R2007 (AC1021).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 1 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007, or the viewport is erased.</exception>
        public int GridMajor
        {
            get => Whole(61, 5);
            set => SetSince2007(61, value, 1, 100, GridMajor, "GRIDMAJOR");
        }

        /// <summary>The object of a VPORT record, made once.</summary>
        internal static DxfViewportTableRecord Of(DxfItem item) =>
            item.Wrapper as DxfViewportTableRecord
            ?? (DxfViewportTableRecord)item.KeepWrapper(new DxfViewportTableRecord(item));

        private void SetSince2007(int code, int value, int least, int most, int now, string name)
        {
            if (value < least || value > most)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    name
                        + " is "
                        + least.ToString(CultureInfo.InvariantCulture)
                        + " to "
                        + most.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }

            if (value != now)
            {
                Item.File.Requires(1021, name, "R2007 (AC1021)");
                SetWhole(code, value, now);
            }
        }

        private void SetPair(int codeX, int codeY, double x, double y, double nowX, double nowY, bool noughtTaken)
        {
            foreach (double v in new[] { x, y })
            {
                if (!Guard.IsFinite(v) || v < 0.0 || (v == 0.0 && !noughtTaken))
                {
                    throw new ArgumentException(
                        "A spacing has to be a finite number "
                            + (noughtTaken ? "not below" : "above")
                            + " nought: "
                            + v.ToString("R", CultureInfo.InvariantCulture)
                            + " is not.",
                        "value"
                    );
                }
            }

            if (!(Same(x, nowX) && Same(y, nowY)))
            {
                SetValues(
                    new KeyValuePair<int, string>(codeX, Numbers.Exact(x + 0.0)),
                    new KeyValuePair<int, string>(codeY, Numbers.Exact(y + 0.0))
                );
            }
        }

        private void SetWhole(int code, int value, int now)
        {
            if (value != now)
            {
                SetValues(new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// Renames the one viewport: its VPORT record's 2. The tiles of one configuration share a name, so no other record is
        /// asked for its name; a caller renaming a whole configuration renames each of its tiles.
        /// </summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");

            if (IsErased)
            {
                throw new InvalidOperationException("The viewport " + Name + " is erased, and is not changed.");
            }

            Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(2, name) }, Order, false);
        }

        private void SetValues(params KeyValuePair<int, string>[] values)
        {
            if (IsErased)
            {
                throw new InvalidOperationException("The viewport " + Name + " is erased, and is not changed.");
            }

            Item.File.SetValues(Item, values, Order, false);
        }

        private int Whole(int code, int fallback) =>
            int.TryParse(
                Item.File.ValueOf(Item, code)?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value
            )
                ? value
                : fallback;

        private double Real(int code, double fallback) =>
            Numbers.TryRead(Item.File.ValueOf(Item, code), out double value) && Guard.IsFinite(value)
                ? value
                : fallback;

        private static bool Same(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
