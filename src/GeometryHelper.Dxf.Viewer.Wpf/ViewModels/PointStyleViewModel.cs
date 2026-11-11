using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A tile of the Point Style dialog: one PDMODE, its symbol drawn as the canvas draws a point of that mode, chosen when
    /// clicked.
    /// </summary>
    public sealed class PointStyleTileViewModel : ObservableObject
    {
        private readonly PointStyleViewModel _owner;

        internal PointStyleTileViewModel(PointStyleViewModel owner, int mode, string label, string symbol, bool hasDot)
        {
            _owner = owner;
            Mode = mode;
            Label = label;
            Symbol = symbol;
            HasDot = hasDot;
        }

        /// <summary>Gets the PDMODE of the tile: 0 to 4, plus 32 for a circle, 64 for a square or 96 for both.</summary>
        public int Mode { get; }

        /// <summary>Gets what the tile shows in words: "PDMODE 34, plus, circle around".</summary>
        public string Label { get; }

        /// <summary>
        /// Gets the lines and circles of the symbol as path data on a 32 by 32 grid, the point at (16, 16) and the symbol 16
        /// across, y down: "M8,16 L24,16 M16,24 L16,8" for the plus of PDMODE 2. Null where the symbol has none (0 and 1).
        /// </summary>
        public string Symbol { get; }

        /// <summary>Gets whether the symbol has the dot of PDMODE 0 (and of 32, 64 and 96) at its middle.</summary>
        public bool HasDot { get; }

        /// <summary>Gets or sets whether the tile is the one chosen; set to true, it makes its mode the dialog's.</summary>
        public bool IsSelected
        {
            get => _owner.Mode == Mode;
            set
            {
                if (value)
                {
                    _owner.Mode = Mode;
                }
            }
        }

        internal void RaiseSelected() => Raise(nameof(IsSelected));
    }

    /// <summary>
    /// AutoCAD's Point Style dialog, which PTYPE (DDPTYPE) shows: twenty tiles of PDMODE, each drawn as the canvas draws a
    /// point of that mode, the point size, and whether it is a share of the screen or a size in drawing units (PDSIZE below
    /// or above nought). A drawing of PDMODE 0 and PDSIZE 0 opens on the dot, 5.0000 per cent of the screen; the plus with a
    /// circle and 2.5 units, OK pressed, writes PDMODE 34 and PDSIZE 2.5 as one undo step (<see cref="Apply"/>).
    /// </summary>
    public sealed class PointStyleViewModel : ObservableObject
    {
        /// <summary>The size of a symbol on the grid of a tile, the point in its middle.</summary>
        private const double SymbolSize = 16.0;

        private static readonly int[] Modes =
        {
            0,
            1,
            2,
            3,
            4,
            32,
            33,
            34,
            35,
            36,
            64,
            65,
            66,
            67,
            68,
            96,
            97,
            98,
            99,
            100,
        };
        private static readonly string[] ShapeNames = { "dot", "nothing", "plus", "cross", "tick" };
        private static IReadOnlyList<KeyValuePair<string, bool>> _symbols;

        private readonly int _startMode;
        private readonly double _startPdsize;
        private readonly bool _startRelative;
        private readonly string _startSize;
        private int _mode;
        private bool _relative;
        private string _size;

        /// <summary>Makes the dialog over the point style of a document.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public PointStyleViewModel(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _startMode = _mode = document.Database.Pdmode;
            _startPdsize = document.Database.Pdsize;
            _startRelative = _relative = _startPdsize <= 0.0;
            _startSize = _size = Text(_startPdsize == 0.0 ? 5.0 : Math.Abs(_startPdsize));
            IReadOnlyList<KeyValuePair<string, bool>> symbols = Symbols();
            Tiles = Modes
                .Select((m, i) => new PointStyleTileViewModel(this, m, Label(m), symbols[i].Key, symbols[i].Value))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the tiles, five to a row: 0 to 4, then the same with a circle (32), a square (64) and both (96).</summary>
        public IReadOnlyList<PointStyleTileViewModel> Tiles { get; }

        /// <summary>
        /// Gets or sets the PDMODE chosen, the drawing's at first; the tile of it is framed. A mode no tile shows, as 5, is kept
        /// until a tile is clicked.
        /// </summary>
        public int Mode
        {
            get => _mode;
            set
            {
                if (Set(ref _mode, value))
                {
                    foreach (PointStyleTileViewModel tile in Tiles)
                    {
                        tile.RaiseSelected();
                    }

                    Raise(nameof(Summary));
                }
            }
        }

        /// <summary>
        /// Gets or sets the point size as typed: a share of the screen in per cent, or a size in drawing units. It is to be a
        /// number above nought (<see cref="IsSizeValid"/>).
        /// </summary>
        public string PointSize
        {
            get => _size;
            set
            {
                if (Set(ref _size, value ?? string.Empty))
                {
                    Raise(nameof(IsSizeValid));
                    Raise(nameof(Pdsize));
                    Raise(nameof(Summary));
                }
            }
        }

        /// <summary>Gets or sets whether the size is a share of the screen's height (Set size relative to screen): PDSIZE below nought.</summary>
        public bool IsRelative
        {
            get => _relative;
            set
            {
                if (Set(ref _relative, value))
                {
                    Raise(nameof(IsAbsolute));
                    Raise(nameof(UnitText));
                    Raise(nameof(Pdsize));
                    Raise(nameof(Summary));
                }
            }
        }

        /// <summary>Gets or sets whether the size is in drawing units (Set size in absolute units): PDSIZE above nought.</summary>
        public bool IsAbsolute
        {
            get => !_relative;
            set => IsRelative = !value;
        }

        /// <summary>Gets the words after the size box: "% of the screen height" or "drawing units".</summary>
        public string UnitText => _relative ? "% of the screen height" : "drawing units";

        /// <summary>Gets whether the size typed is a number above nought, so that OK can be pressed.</summary>
        public bool IsSizeValid => TryParseSize(out _);

        /// <summary>
        /// Gets the PDSIZE OK writes: the size for absolute units, the size below nought for a share of the screen. The size and
        /// its kind as the dialog opened give the drawing's PDSIZE back, so that 0 (five per cent of the screen) is kept as 0.
        /// The drawing's PDSIZE while the size is not a number.
        /// </summary>
        public double Pdsize
        {
            get
            {
                if (
                    !TryParseSize(out double size)
                    || (
                        _relative == _startRelative && string.Equals(_size.Trim(), _startSize, StringComparison.Ordinal)
                    )
                )
                {
                    return _startPdsize;
                }

                return _relative ? -size : size;
            }
        }

        /// <summary>Gets the line under the choices: "PDMODE 34 · PDSIZE -5.0000".</summary>
        public string Summary => "PDMODE " + _mode.ToString(CultureInfo.InvariantCulture) + " · PDSIZE " + Text(Pdsize);

        /// <summary>
        /// Writes the PDMODE and PDSIZE chosen as one undo step named PTYPE, as OK does, and every point is drawn again; nothing
        /// is written where neither changed, or where the size is not a number above nought, which is refused.
        /// </summary>
        /// <returns>What came of it; null where neither changed.</returns>
        public CadEditResult Apply()
        {
            if (!IsSizeValid)
            {
                return CadEditResult.Refuse("The point size is to be a number above nought.");
            }

            int mode = _mode;
            double size = Pdsize;
            var records = new List<CadEditRecord>();

            if (mode != _startMode)
            {
                records.Add(
                    new CadEditRecord(
                        CadEditRecordKind.SettingChanged,
                        null,
                        "$PDMODE",
                        _startMode.ToString(CultureInfo.InvariantCulture),
                        mode.ToString(CultureInfo.InvariantCulture)
                    )
                );
            }

            if (size.CompareTo(_startPdsize) != 0)
            {
                records.Add(
                    new CadEditRecord(
                        CadEditRecordKind.SettingChanged,
                        null,
                        "$PDSIZE",
                        _startPdsize.ToString("R", CultureInfo.InvariantCulture),
                        size.ToString("R", CultureInfo.InvariantCulture)
                    )
                );
            }

            if (records.Count == 0)
            {
                return null;
            }

            return Document.ApplyEdit("PTYPE", records, s =>
            {
                s.Database.Pdmode = mode;
                s.Database.Pdsize = size;
            });
        }

        private bool TryParseSize(out double size) =>
            double.TryParse((_size ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out size)
            && size > 0.0
            && !double.IsInfinity(size);

        private static string Text(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

        private static string Label(int mode)
        {
            string around =
                (mode & 96) == 96 ? ", circle and square around"
                : (mode & 32) != 0 ? ", circle around"
                : (mode & 64) != 0 ? ", square around"
                : string.Empty;
            return "PDMODE " + mode.ToString(CultureInfo.InvariantCulture) + ", " + ShapeNames[mode & 31] + around;
        }

        /// <summary>
        /// The symbols of the tiles, made once: a point at the origin drawn by the presenter's display builder at each mode and
        /// a PDSIZE of 16 units, so that a tile shows what the canvas draws. Each is its path data and whether it has the dot.
        /// </summary>
        private static IReadOnlyList<KeyValuePair<string, bool>> Symbols()
        {
            if (_symbols != null)
            {
                return _symbols;
            }

            var symbols = new List<KeyValuePair<string, bool>>();

            foreach (int mode in Modes)
            {
                var database = new DxfDatabase("AC1027") { Pdmode = mode, Pdsize = SymbolSize };
                database.ModelSpace.AppendEntity(new DxfDBPoint(new GeoPoint3(0.0, 0.0, 0.0)));
                CadDocument document = CadDocument.From(database, "point-style.dxf");
                var path = new StringBuilder();
                bool dot = false;

                foreach (DisplayItem2 item in document.GetDisplay(document.ModelSpace).Items)
                {
                    if (item is DisplayArc2 arc)
                    {
                        // A circle about the point: two halves, as path data draws no whole circle in one arc.
                        string r = Number(arc.Radius);
                        Append(path, "M", arc.Cx + arc.Radius, arc.Cy);
                        path.Append(" A")
                            .Append(r)
                            .Append(',')
                            .Append(r)
                            .Append(" 0 1 0 ")
                            .Append(Number(16.0 + arc.Cx - arc.Radius))
                            .Append(',')
                            .Append(Number(16.0 - arc.Cy));
                        path.Append(" A")
                            .Append(r)
                            .Append(',')
                            .Append(r)
                            .Append(" 0 1 0 ")
                            .Append(Number(16.0 + arc.Cx + arc.Radius))
                            .Append(',')
                            .Append(Number(16.0 - arc.Cy));
                    }
                    else if (item is DisplayPolyline2 line && line.Xy.Count == 2)
                    {
                        dot = true;
                    }
                    else if (item is DisplayPolyline2 lines)
                    {
                        for (int i = 0; i + 1 < lines.Xy.Count; i += 2)
                        {
                            Append(path, i == 0 ? "M" : "L", lines.Xy[i], lines.Xy[i + 1]);
                        }

                        if (lines.Closed)
                        {
                            path.Append(" Z");
                        }
                    }
                }

                symbols.Add(new KeyValuePair<string, bool>(path.Length == 0 ? null : path.ToString().TrimStart(), dot));
            }

            _symbols = symbols.AsReadOnly();
            return _symbols;
        }

        /// <summary>A point of the drawing on the 32 by 32 grid of a tile, y turned down.</summary>
        private static void Append(StringBuilder path, string verb, double x, double y) =>
            path.Append(' ').Append(verb).Append(Number(16.0 + x)).Append(',').Append(Number(16.0 - y));

        private static string Number(double value) =>
            (Math.Abs(value) < 5E-7 ? 0.0 : value).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
