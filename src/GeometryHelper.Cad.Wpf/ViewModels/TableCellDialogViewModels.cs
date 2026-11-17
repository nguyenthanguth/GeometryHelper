using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Cell Border Properties dialog of the Table Cell tab, as AutoCAD 2021's: the lineweight, the linetype and the colour of
    /// the lines, the border buttons (All, Outside, Inside, Bottom, Left, Top, Right and No Border) that say which lines of the
    /// cells selected they are given to, and Visible. OK gives the lines chosen the properties.
    /// </summary>
    public sealed class CellBordersViewModel : ObservableObject
    {
        private static readonly string[] Lines = { "Outside", "Inside", "Bottom", "Left", "Top", "Right" };

        private static readonly DxfGridLineType[] LineTypes =
        {
            DxfGridLineType.OuterGridLines,
            DxfGridLineType.InnerGridLines,
            DxfGridLineType.HorizontalBottom,
            DxfGridLineType.VerticalLeft,
            DxfGridLineType.HorizontalTop,
            DxfGridLineType.VerticalRight,
        };

        private static readonly DxfLineWeight[] Weights =
        {
            DxfLineWeight.ByBlock,
            DxfLineWeight.ByLayer,
            DxfLineWeight.ByLineWeightDefault,
            DxfLineWeight.LineWeight000,
            DxfLineWeight.LineWeight013,
            DxfLineWeight.LineWeight018,
            DxfLineWeight.LineWeight025,
            DxfLineWeight.LineWeight035,
            DxfLineWeight.LineWeight050,
            DxfLineWeight.LineWeight070,
            DxfLineWeight.LineWeight100,
        };

        private readonly bool[] _chosen = new bool[6];
        private string _weight = "ByBlock";
        private TableFillChoice _color = TableStyleModifyViewModel.Colors[0];
        private bool _visible = true;
        private bool _none;
        private string _linetype = "ByBlock";
        private bool _doubleLine;
        private string _spacing = "1.125";

        /// <summary>Makes the dialog for a range of cells.</summary>
        /// <param name="cellName">The name of the cells, B3 or B3:C4, shown in the title.</param>
        /// <param name="linetypes">The linetypes the drawing has, to offer after ByBlock and ByLayer.</param>
        public CellBordersViewModel(string cellName, IEnumerable<string> linetypes)
        {
            CellName = cellName ?? string.Empty;
            Linetypes = new[] { "ByBlock", "ByLayer" }
                .Concat((linetypes ?? Enumerable.Empty<string>()).Where(n => n != "ByBlock" && n != "ByLayer"))
                .ToList();
            OkCommand = new DelegateCommand(_ => Ok(), _ => IsValid);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the name of the cells.</summary>
        public string CellName { get; }

        /// <summary>Gets the border buttons: All, Outside, Inside, Bottom, Left, Top, Right, No Border.</summary>
        public static IReadOnlyList<string> Buttons { get; } =
            new[] { "All", "Outside", "Inside", "Bottom", "Left", "Top", "Right", "No Border" };

        /// <summary>Gets the lineweights offered, by name.</summary>
        public IReadOnlyList<string> LineWeightChoices => TableStyleModifyViewModel.LineWeights;

        /// <summary>Gets the colours offered.</summary>
        public IReadOnlyList<TableFillChoice> ColorChoices => TableStyleModifyViewModel.Colors;

        /// <summary>Gets the linetypes offered: ByBlock, ByLayer and those of the drawing.</summary>
        public IReadOnlyList<string> Linetypes { get; }

        /// <summary>Gets or sets the lineweight the lines are given.</summary>
        public string LineWeight
        {
            get => _weight;
            set => Set(ref _weight, TableStyleModifyViewModel.LineWeights.Contains(value) ? value : _weight);
        }

        /// <summary>Gets or sets the colour the lines are given.</summary>
        public TableFillChoice Color
        {
            get => _color;
            set => Set(ref _color, value ?? _color);
        }

        /// <summary>Gets or sets the linetype the lines are given.</summary>
        public string Linetype
        {
            get => _linetype;
            set => Set(ref _linetype, Linetypes.Contains(value) ? value : _linetype);
        }

        /// <summary>Gets or sets whether the lines are doubled.</summary>
        public bool DoubleLine
        {
            get => _doubleLine;
            set
            {
                if (Set(ref _doubleLine, value))
                {
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the distance between the two lines of a double line, as typed.</summary>
        public string Spacing
        {
            get => _spacing;
            set
            {
                if (Set(ref _spacing, value))
                {
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets whether the lines are drawn.</summary>
        public bool Visible
        {
            get => _visible;
            set => Set(ref _visible, value);
        }

        /// <summary>Gets whether the buttons are valid: some line is chosen or No Border is, and a double line has a spacing above nought.</summary>
        public bool IsValid =>
            (_none || _chosen.Any(c => c))
            && (
                !_doubleLine
                || (
                    double.TryParse(_spacing, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s > 0.0
                )
            );

        /// <summary>Gets or sets the border button All: Outside and Inside pressed together.</summary>
        public bool All
        {
            get => _chosen[0] && _chosen[1];
            set
            {
                _chosen[0] = value;
                _chosen[1] = value;
                _none = _none && !value;
                Changed();
            }
        }

        /// <summary>Gets or sets the border button Outside: the four lines round the cells.</summary>
        public bool Outside
        {
            get => _chosen[0];
            set => Choose(0, value);
        }

        /// <summary>Gets or sets the border button Inside: the lines between the cells.</summary>
        public bool Inside
        {
            get => _chosen[1];
            set => Choose(1, value);
        }

        /// <summary>Gets or sets the border button Bottom.</summary>
        public bool Bottom
        {
            get => _chosen[2];
            set => Choose(2, value);
        }

        /// <summary>Gets or sets the border button Left.</summary>
        public bool Left
        {
            get => _chosen[3];
            set => Choose(3, value);
        }

        /// <summary>Gets or sets the border button Top.</summary>
        public bool Top
        {
            get => _chosen[4];
            set => Choose(4, value);
        }

        /// <summary>Gets or sets the border button Right.</summary>
        public bool Right
        {
            get => _chosen[5];
            set => Choose(5, value);
        }

        /// <summary>Gets or sets the border button No Border: every line of the cells hidden; it lets the others go.</summary>
        public bool NoBorder
        {
            get => _none;
            set
            {
                _none = value;

                if (value)
                {
                    Array.Clear(_chosen, 0, _chosen.Length);
                }

                Changed();
            }
        }

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Gets the lines the buttons chose, as <see cref="DxfGridLineType"/> flags; none for No Border, which hides all of them.</summary>
        public DxfGridLineType ChosenLines
        {
            get
            {
                if (_none)
                {
                    return DxfGridLineType.AllGridLines;
                }

                DxfGridLineType lines = DxfGridLineType.InvalidGridLine;

                for (int k = 0; k < 6; k++)
                {
                    lines |= _chosen[k] ? LineTypes[k] : DxfGridLineType.InvalidGridLine;
                }

                return lines;
            }
        }

        /// <summary>Gets whether OK was pressed.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Gets the lineweight chosen, as the file holds it.</summary>
        public DxfLineWeight Weight =>
            Weights[Math.Max(0, TableStyleModifyViewModel.LineWeights.ToList().IndexOf(_weight))];

        /// <summary>Gets the colour chosen, ByLayer or ByBlock or of the index.</summary>
        public DxfColor ColorValue =>
            _color.Index == 0 ? DxfColor.ByBlock
            : _color.Index == 256 ? DxfColor.ByLayer
            : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(_color.Index ?? 0));

        /// <summary>Gets whether the lines are drawn as OK gives them: not for No Border.</summary>
        public bool LinesShown => !_none && _visible;

        /// <summary>Gets the distance between the lines of a double line.</summary>
        public double SpacingValue =>
            double.TryParse(_spacing, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 0.0;

        /// <summary>Presses OK.</summary>
        public void Ok()
        {
            if (!IsValid)
            {
                return;
            }

            IsConfirmed = true;
            CloseRequested?.Invoke(this, true);
        }

        private void Choose(int line, bool value)
        {
            _chosen[line] = value;
            _none = _none && !value;
            Changed();
        }

        private void Changed()
        {
            foreach (
                string name in new[]
                {
                    nameof(All),
                    nameof(Outside),
                    nameof(Inside),
                    nameof(Bottom),
                    nameof(Left),
                    nameof(Top),
                    nameof(Right),
                    nameof(NoBorder),
                    nameof(IsValid),
                }
            )
            {
                Raise(name);
            }

            OkCommand?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// A kind of data a Data Format dialog offers: its name, the kind of value and the unit it gives a cell, and what its options
    /// are.
    /// </summary>
    public sealed class DataFormatKind
    {
        internal DataFormatKind(string name, DxfDataType kind, DxfUnitType unit, string options)
        {
            Name = name;
            Kind = kind;
            Unit = unit;
            Options = options;
        }

        /// <summary>Gets the name AutoCAD lists it by: Angle, Currency, Date, Decimal Number, General, Percentage, Point, Text, Whole Number.</summary>
        public string Name { get; }

        /// <summary>Gets the kind of value a cell of it holds.</summary>
        public DxfDataType Kind { get; }

        /// <summary>Gets the unit of it.</summary>
        public DxfUnitType Unit { get; }

        /// <summary>Gets the options it has, by letter: u units, a angle units, p precision, d decimal separator, t thousands separator, w prefix and suffix, f factor, z zeros, e date format.</summary>
        internal string Options { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The Table Cell Format dialog (Data Format, Custom Table Cell Format…) of the Table Cell tab, as AutoCAD 2021's: the data
    /// types on the left (Angle, Currency, Date, Decimal Number, General, Percentage, Point, Text, Whole Number), the format of the
    /// one chosen on the right (its units, precision, separators, prefix, suffix, conversion factor and zeros) and a sample of a
    /// number written so. OK gives the format as the format string of a cell (<c>%lu2%pr2%ps[$,]%th44</c>).
    /// </summary>
    public sealed class DataFormatViewModel : ObservableObject
    {
        /// <summary>The kinds of data, in the order of the list.</summary>
        public static readonly IReadOnlyList<DataFormatKind> Kinds = new[]
        {
            new DataFormatKind("Angle", DxfDataType.Double, DxfUnitType.Angle, "apdw"),
            new DataFormatKind("Currency", DxfDataType.Double, DxfUnitType.Currency, "pdtwf"),
            new DataFormatKind("Date", DxfDataType.Date, DxfUnitType.Unitless, "e"),
            new DataFormatKind("Decimal Number", DxfDataType.Double, DxfUnitType.Unitless, "upwdtfz"),
            new DataFormatKind("General", DxfDataType.General, DxfUnitType.Unitless, string.Empty),
            new DataFormatKind("Percentage", DxfDataType.Double, DxfUnitType.Percentage, "pdw"),
            new DataFormatKind("Point", DxfDataType.Point3d, DxfUnitType.Distance, "p"),
            new DataFormatKind("Text", DxfDataType.String, DxfUnitType.Unitless, string.Empty),
            new DataFormatKind("Whole Number", DxfDataType.Long, DxfUnitType.Unitless, "t"),
        };

        /// <summary>The units a number can be written in, as AutoCAD's Table Cell Format lists them, with their <c>%lu</c> number.</summary>
        public static readonly IReadOnlyList<string> Units = new[]
        {
            "Decimal",
            "Scientific",
            "Engineering",
            "Architectural",
            "Fractional",
            "Current units",
        };

        private static readonly int[] UnitNumbers = { 2, 1, 3, 4, 5, 6 };

        /// <summary>The units an angle can be written in, with their <c>%au</c> number.</summary>
        public static readonly IReadOnlyList<string> AngleUnits = new[]
        {
            "Decimal Degrees",
            "Deg/Min/Sec",
            "Grads",
            "Radians",
            "Surveyor's Units",
        };

        /// <summary>The formats of a date, as the <c>%x</c> short date, <c>%X</c> time and <c>%c</c> both.</summary>
        public static readonly IReadOnlyList<string> DateFormats = new[] { "Date", "Time", "Date and time" };

        private static readonly string[] DateCodes = { "%x", "%X", "%c" };

        /// <summary>The separators a number of thousands can have.</summary>
        public static readonly IReadOnlyList<string> Thousands = new[] { "None", "Comma", "Period", "Space" };

        /// <summary>The separators of the decimals.</summary>
        public static readonly IReadOnlyList<string> Decimals = new[] { "Period", "Comma" };

        private DataFormatKind _kind = Kinds[3];
        private string _unit = "Decimal";
        private string _angle = "Decimal Degrees";
        private int _precision = 4;
        private string _thousand = "None";
        private string _decimal = "Period";
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private string _factor = "1";
        private bool _leading;
        private bool _trailing;
        private string _date = "Date";

        /// <summary>Makes the dialog, starting from a format of cells.</summary>
        /// <param name="cellName">The name of the cells, shown in the title.</param>
        /// <param name="kind">The kind of the first cell's value.</param>
        /// <param name="unit">Its unit.</param>
        /// <param name="format">Its format string.</param>
        public DataFormatViewModel(string cellName, DxfDataType kind, DxfUnitType unit, string format)
        {
            CellName = cellName ?? string.Empty;
            _kind = Kinds.FirstOrDefault(k => k.Kind == kind && k.Unit == unit)
                ?? Kinds.FirstOrDefault(k => k.Kind == kind && kind != DxfDataType.Double)
                ?? (kind == DxfDataType.Double ? Kinds[3] : Kinds[4]);
            Load(format);
            OkCommand = new DelegateCommand(_ => Ok(), _ => IsValid);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the name of the cells.</summary>
        public string CellName { get; }

        /// <summary>Gets the kinds of data of the list.</summary>
        public IReadOnlyList<DataFormatKind> DataTypes => Kinds;

        /// <summary>Gets the units offered for a number.</summary>
        public IReadOnlyList<string> UnitChoices => Units;

        /// <summary>Gets the units offered for an angle.</summary>
        public IReadOnlyList<string> AngleUnitChoices => AngleUnits;

        /// <summary>Gets the date formats offered.</summary>
        public IReadOnlyList<string> DateChoices => DateFormats;

        /// <summary>Gets the precisions offered, 0 to 8 places.</summary>
        public IReadOnlyList<int> PrecisionChoices { get; } = Enumerable.Range(0, 9).ToList();

        /// <summary>Gets the thousands separators offered.</summary>
        public IReadOnlyList<string> ThousandChoices => Thousands;

        /// <summary>Gets the decimal separators offered.</summary>
        public IReadOnlyList<string> DecimalChoices => Decimals;

        /// <summary>Gets or sets the kind of data chosen.</summary>
        public DataFormatKind DataType
        {
            get => _kind;
            set
            {
                if (value != null && Set(ref _kind, value))
                {
                    // The usual options of a kind, as AutoCAD offers it first: money in two places with commas, a percentage whole.
                    if (value.Name == "Currency")
                    {
                        _precision = 2;
                        _thousand = "Comma";
                    }
                    else if (value.Name == "Percentage")
                    {
                        _precision = 0;
                    }

                    Raise(nameof(Precision));
                    Raise(nameof(Thousand));
                    RaiseAll();
                }
            }
        }

        /// <summary>Gets or sets the units of a number.</summary>
        public string Unit
        {
            get => _unit;
            set => SetAndShow(ref _unit, value);
        }

        /// <summary>Gets or sets the units of an angle.</summary>
        public string AngleUnit
        {
            get => _angle;
            set => SetAndShow(ref _angle, value);
        }

        /// <summary>Gets or sets the places of the precision.</summary>
        public int Precision
        {
            get => _precision;
            set
            {
                if (Set(ref _precision, Math.Max(0, Math.Min(8, value))))
                {
                    RaiseAll();
                }
            }
        }

        /// <summary>Gets or sets the separator of thousands.</summary>
        public string Thousand
        {
            get => _thousand;
            set => SetAndShow(ref _thousand, value);
        }

        /// <summary>Gets or sets the separator of decimals.</summary>
        public string Decimal
        {
            get => _decimal;
            set => SetAndShow(ref _decimal, value);
        }

        /// <summary>Gets or sets the words before the number.</summary>
        public string Prefix
        {
            get => _prefix;
            set => SetAndShow(ref _prefix, value ?? string.Empty);
        }

        /// <summary>Gets or sets the words after the number.</summary>
        public string Suffix
        {
            get => _suffix;
            set => SetAndShow(ref _suffix, value ?? string.Empty);
        }

        /// <summary>Gets or sets the factor the number is multiplied by, as typed.</summary>
        public string Factor
        {
            get => _factor;
            set => SetAndShow(ref _factor, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether leading zeros are left out.</summary>
        public bool SuppressLeading
        {
            get => _leading;
            set => SetAndShow(ref _leading, value);
        }

        /// <summary>Gets or sets whether trailing zeros are left out.</summary>
        public bool SuppressTrailing
        {
            get => _trailing;
            set => SetAndShow(ref _trailing, value);
        }

        /// <summary>Gets or sets the format of a date.</summary>
        public string DateFormat
        {
            get => _date;
            set => SetAndShow(ref _date, DateFormats.Contains(value) ? value : _date);
        }

        /// <summary>Gets whether the units of a number are offered.</summary>
        public bool HasUnits => Has('u');

        /// <summary>Gets whether the units of an angle are offered.</summary>
        public bool HasAngleUnits => Has('a');

        /// <summary>Gets whether the precision is offered.</summary>
        public bool HasPrecision => Has('p');

        /// <summary>Gets whether the decimal separator is offered.</summary>
        public bool HasDecimal => Has('d');

        /// <summary>Gets whether the thousands separator is offered.</summary>
        public bool HasThousands => Has('t');

        /// <summary>Gets whether a prefix and a suffix are offered.</summary>
        public bool HasAffixes => Has('w');

        /// <summary>Gets whether the conversion factor is offered.</summary>
        public bool HasFactor => Has('f');

        /// <summary>Gets whether the zeros to leave out are offered.</summary>
        public bool HasZeros => Has('z');

        /// <summary>Gets whether the date format is offered.</summary>
        public bool HasDate => Has('e');

        /// <summary>Gets whether the format is valid: a factor, where offered, is a number.</summary>
        public bool IsValid =>
            !HasFactor
            || (
                double.TryParse(_factor, NumberStyles.Float, CultureInfo.InvariantCulture, out double f)
                && !double.IsInfinity(f)
                && !double.IsNaN(f)
            );

        /// <summary>Gets the format string of a cell the options make; empty for none.</summary>
        public string Format
        {
            get
            {
                if (_kind.Options.Length == 0)
                {
                    return string.Empty;
                }

                if (HasDate)
                {
                    return DateCodes[Math.Max(0, DateFormats.ToList().IndexOf(_date))];
                }

                string text = string.Empty;

                if (HasAngleUnits)
                {
                    text +=
                        "%au" + Math.Max(0, AngleUnits.ToList().IndexOf(_angle)).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    int unit = HasUnits ? UnitNumbers[Math.Max(0, Units.ToList().IndexOf(_unit))] : 2;
                    text += "%lu" + unit.ToString(CultureInfo.InvariantCulture);
                }

                if (HasPrecision)
                {
                    text +=
                        "%pr" + (_kind.Name == "Whole Number" ? 0 : _precision).ToString(CultureInfo.InvariantCulture);
                }
                else if (_kind.Name == "Whole Number")
                {
                    text += "%pr0";
                }

                if (HasZeros && (_leading || _trailing))
                {
                    text += "%zs" + ((_leading ? 1 : 0) | (_trailing ? 8 : 0)).ToString(CultureInfo.InvariantCulture);
                }

                string prefix = _prefix;
                string suffix = _suffix;

                if (_kind.Name == "Currency" && prefix.Length == 0 && suffix.Length == 0)
                {
                    prefix = "$";
                }

                if (_kind.Name == "Percentage" && suffix.Length == 0)
                {
                    suffix = "%";
                }

                if (prefix.Length != 0 || suffix.Length != 0)
                {
                    text += "%ps[" + prefix + "," + suffix + "]";
                }

                if (_kind.Name == "Percentage")
                {
                    text += "%ct8[100]";
                }
                else if (HasFactor && FactorValue != 1.0)
                {
                    text += "%ct8[" + FactorValue.ToString("R", CultureInfo.InvariantCulture) + "]";
                }

                if (HasDecimal && _decimal == "Comma")
                {
                    text += "%ds44";
                }

                if (HasThousands && _thousand != "None")
                {
                    text +=
                        "%th"
                        + (
                            _thousand == "Comma" ? 44
                            : _thousand == "Period" ? 46
                            : 32
                        ).ToString(CultureInfo.InvariantCulture);
                }

                return text;
            }
        }

        /// <summary>Gets what a number of 1234.5678 (a date of the 2nd of January 2020 at 3:04 pm, an angle of 45.5) comes to in the format.</summary>
        public string Sample
        {
            get
            {
                if (_kind.Name == "Text")
                {
                    return "Text";
                }

                if (_kind.Name == "General")
                {
                    return "1234.5678";
                }

                if (_kind.Name == "Point")
                {
                    return "1234.5678, 8765.4321, 0.0000";
                }

                if (HasDate)
                {
                    var day = new DateTime(2020, 1, 2, 15, 4, 5);
                    return _date == "Date" ? day.ToString("d", CultureInfo.CurrentCulture)
                        : _date == "Time" ? day.ToString("T", CultureInfo.CurrentCulture)
                        : day.ToString("G", CultureInfo.CurrentCulture);
                }

                double value =
                    HasAngleUnits ? 45.5
                    : _kind.Name == "Percentage" ? 0.5
                    : 1234.5678;
                string format = Format;

                try
                {
                    return CadFieldFormat.Parse(format).Number(value, HasAngleUnits);
                }
                catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Gets whether OK was pressed.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Presses OK.</summary>
        public void Ok()
        {
            if (!IsValid)
            {
                return;
            }

            IsConfirmed = true;
            CloseRequested?.Invoke(this, true);
        }

        private double FactorValue =>
            double.TryParse(_factor, NumberStyles.Float, CultureInfo.InvariantCulture, out double f) ? f : 1.0;

        private bool Has(char option) => _kind.Options.IndexOf(option) >= 0;

        private void SetAndShow<T>(
            ref T field,
            T value,
            [System.Runtime.CompilerServices.CallerMemberName] string name = null
        )
        {
            if (Set(ref field, value, name))
            {
                RaiseAll();
            }
        }

        private void RaiseAll()
        {
            Raise(nameof(Sample));
            Raise(nameof(Format));
            Raise(nameof(IsValid));
            OkCommand?.RaiseCanExecuteChanged();

            foreach (
                string name in new[]
                {
                    nameof(HasUnits),
                    nameof(HasAngleUnits),
                    nameof(HasPrecision),
                    nameof(HasDecimal),
                    nameof(HasThousands),
                    nameof(HasAffixes),
                    nameof(HasFactor),
                    nameof(HasZeros),
                    nameof(HasDate),
                }
            )
            {
                Raise(name);
            }
        }

        /// <summary>Reads the options out of a format string, so that the dialog opens as the cell is.</summary>
        private void Load(string format)
        {
            string text = format ?? string.Empty;

            if (text == "%x" || text == "%X" || text == "%c")
            {
                _date = DateFormats[Array.IndexOf(DateCodes, text)];
            }

            System.Text.RegularExpressions.Match unit = System.Text.RegularExpressions.Regex.Match(text, "%lu([1-6])");

            if (unit.Success)
            {
                int at = Array.IndexOf(UnitNumbers, unit.Groups[1].Value[0] - '0');
                _unit = Units[Math.Max(0, at)];
            }

            System.Text.RegularExpressions.Match angle = System.Text.RegularExpressions.Regex.Match(text, "%au([0-4])");

            if (angle.Success)
            {
                _angle = AngleUnits[angle.Groups[1].Value[0] - '0'];
            }

            System.Text.RegularExpressions.Match places = System.Text.RegularExpressions.Regex.Match(
                text,
                "%pr([0-9]{1,2})"
            );

            if (places.Success)
            {
                _precision = Math.Min(8, int.Parse(places.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            System.Text.RegularExpressions.Match words = System.Text.RegularExpressions.Regex.Match(
                text,
                @"%ps\[([^,\]]*),([^\]]*)\]"
            );

            if (words.Success)
            {
                _prefix =
                    _kind.Name == "Currency" && words.Groups[1].Value == "$" ? string.Empty : words.Groups[1].Value;
                _suffix =
                    _kind.Name == "Percentage" && words.Groups[2].Value == "%" ? string.Empty : words.Groups[2].Value;
            }

            System.Text.RegularExpressions.Match times = System.Text.RegularExpressions.Regex.Match(
                text,
                @"%ct8\[([^\]]*)\]"
            );

            if (times.Success && _kind.Name != "Percentage")
            {
                _factor = times.Groups[1].Value;
            }

            _decimal = text.Contains("%ds44") ? "Comma" : "Period";
            _thousand =
                text.Contains("%th44") || text.Contains(@"%th[\,]") ? "Comma"
                : text.Contains("%th46") ? "Period"
                : text.Contains("%th32") ? "Space"
                : "None";
            System.Text.RegularExpressions.Match zeros = System.Text.RegularExpressions.Regex.Match(
                text,
                "%zs([0-9]{1,2})"
            );
            int flags = zeros.Success ? int.Parse(zeros.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            _leading = (flags & 1) != 0;
            _trailing = (flags & 8) != 0;
        }
    }

    /// <summary>
    /// The Insert a Block in a Table Cell dialog, as AutoCAD 2021's: the name of a block of the drawing, its scale or AutoFit, its
    /// rotation angle and the overall alignment of the cell. OK puts the block in the cell.
    /// </summary>
    public sealed class InsertTableBlockViewModel : ObservableObject
    {
        private string _name;
        private string _scale = "1";
        private bool _autoFit = true;
        private string _rotation = "0";
        private string _alignment = "Middle Center";

        /// <summary>Makes the dialog for a cell.</summary>
        /// <param name="cellName">The name of the cell, B3.</param>
        /// <param name="blocks">The names of the blocks of the drawing a cell can hold.</param>
        /// <param name="current">The block the cell holds; null for none.</param>
        public InsertTableBlockViewModel(string cellName, IEnumerable<string> blocks, string current)
        {
            CellName = cellName ?? string.Empty;
            Blocks = new ObservableCollection<string>(
                (blocks ?? Enumerable.Empty<string>()).OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
            );
            _name = current != null && Blocks.Contains(current) ? current : Blocks.FirstOrDefault();
            OkCommand = new DelegateCommand(_ => Ok(), _ => IsValid);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the name of the cell.</summary>
        public string CellName { get; }

        /// <summary>Gets the names of the blocks offered.</summary>
        public ObservableCollection<string> Blocks { get; }

        /// <summary>Gets the alignments offered, as the Table Cell tab lists them.</summary>
        public IReadOnlyList<string> Alignments => TableCellViewModel.Alignments;

        /// <summary>Gets or sets the block chosen.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value))
                {
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the scale of the block, as typed; not used where it is fitted.</summary>
        public string Scale
        {
            get => _scale;
            set
            {
                if (Set(ref _scale, value))
                {
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets whether the block is fitted to the cell.</summary>
        public bool AutoFit
        {
            get => _autoFit;
            set
            {
                if (Set(ref _autoFit, value))
                {
                    Raise(nameof(ScaleEnabled));
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets whether the scale box is enabled: not while the block is fitted.</summary>
        public bool ScaleEnabled => !_autoFit;

        /// <summary>Gets or sets the rotation angle in degrees, as typed.</summary>
        public string Rotation
        {
            get => _rotation;
            set
            {
                if (Set(ref _rotation, value))
                {
                    Raise(nameof(IsValid));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the overall alignment of the cell.</summary>
        public string Alignment
        {
            get => _alignment;
            set => Set(ref _alignment, TableCellViewModel.Alignments.Contains(value) ? value : _alignment);
        }

        /// <summary>Gets the scale as a number; 1 where it is not one.</summary>
        public double ScaleValue =>
            double.TryParse(_scale, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 1.0;

        /// <summary>Gets the rotation as a number, in degrees; nought where it is not one.</summary>
        public double RotationValue =>
            double.TryParse(_rotation, NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ? r : 0.0;

        /// <summary>Gets the alignment as the file holds it.</summary>
        public DxfCellAlignment AlignmentValue =>
            (DxfCellAlignment)(TableCellViewModel.Alignments.ToList().IndexOf(_alignment) + 1);

        /// <summary>Gets whether OK is allowed: a block is chosen, the scale (where it is not fitted) is above nought, the rotation is a number.</summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(_name)
            && (
                _autoFit
                || (
                    double.TryParse(_scale, NumberStyles.Float, CultureInfo.InvariantCulture, out double s)
                    && s > 0.0
                    && !double.IsInfinity(s)
                )
            )
            && double.TryParse(_rotation, NumberStyles.Float, CultureInfo.InvariantCulture, out double r)
            && !double.IsInfinity(r)
            && !double.IsNaN(r);

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Gets whether OK was pressed.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Presses OK.</summary>
        public void Ok()
        {
            if (!IsValid)
            {
                return;
            }

            IsConfirmed = true;
            CloseRequested?.Invoke(this, true);
        }
    }

    /// <summary>The Create New Cell Style dialog: the name of the new cell style and the one it starts with.</summary>
    public sealed class CreateCellStyleViewModel : ObservableObject
    {
        private string _name = "Cell Style 1";
        private string _startWith;
        private readonly Func<string, bool> _taken;

        /// <summary>Makes the dialog.</summary>
        /// <param name="existing">The names of the cell styles the table style has.</param>
        public CreateCellStyleViewModel(IReadOnlyList<string> existing)
        {
            StartChoices = existing ?? throw new ArgumentNullException(nameof(existing));
            _startWith = StartChoices.LastOrDefault(n => n == "Data") ?? StartChoices.FirstOrDefault();
            _taken = n => StartChoices.Any(e => string.Equals(e, n, StringComparison.OrdinalIgnoreCase));
            OkCommand = new DelegateCommand(_ => Ok(), _ => IsValid);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the cell styles it can start with.</summary>
        public IReadOnlyList<string> StartChoices { get; }

        /// <summary>Gets or sets the name of the new cell style.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(IsValid));
                    Raise(nameof(Message));
                    OkCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the cell style it starts with.</summary>
        public string StartWith
        {
            get => _startWith;
            set => Set(ref _startWith, StartChoices.Contains(value) ? value : _startWith);
        }

        /// <summary>Gets why the name is not allowed; empty where it is.</summary>
        public string Message =>
            _name.Trim().Length == 0 ? "A cell style needs a name."
            : _taken(_name.Trim()) ? "The cell style \"" + _name.Trim() + "\" already exists."
            : string.Empty;

        /// <summary>Gets whether OK is allowed.</summary>
        public bool IsValid => Message.Length == 0;

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Gets whether OK was pressed.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Presses OK.</summary>
        public void Ok()
        {
            if (!IsValid)
            {
                return;
            }

            IsConfirmed = true;
            CloseRequested?.Invoke(this, true);
        }
    }

    /// <summary>
    /// The Manage Cell Styles dialog: the cell styles of the table style (Title, Header, Data and a user's) and New…, Rename and
    /// Delete for those a user made; Title, Header and Data stay, which their buttons say.
    /// </summary>
    public sealed class ManageCellStylesViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private readonly string _tableStyle;
        private string _selected;
        private string _message = string.Empty;
        private string _newName = string.Empty;

        /// <summary>Makes the dialog over a table style.</summary>
        /// <param name="document">The document.</param>
        /// <param name="tableStyle">The name of the table style.</param>
        /// <param name="ask">Shows the Create New Cell Style dialog and says whether it was answered; null where the window shows none.</param>
        public ManageCellStylesViewModel(
            CadDocument document,
            string tableStyle,
            Func<CreateCellStyleViewModel, bool> ask
        )
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _tableStyle = tableStyle ?? throw new ArgumentNullException(nameof(tableStyle));
            Ask = ask;
            Reload();
            _selected = Styles.FirstOrDefault();
            NewCommand = new DelegateCommand(_ => New());
            RenameCommand = new DelegateCommand(_ => Rename(), _ => CanChange);
            DeleteCommand = new DelegateCommand(_ => Delete(), _ => CanChange);
            CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, true));
        }

        /// <summary>Raised when the dialog is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        private Func<CreateCellStyleViewModel, bool> Ask { get; }

        /// <summary>Gets the name of the table style.</summary>
        public string TableStyle => _tableStyle;

        /// <summary>Gets the cell styles of the table style.</summary>
        public ObservableCollection<string> Styles { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the cell style chosen.</summary>
        public string Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanChange));
                    RenameCommand?.RaiseCanExecuteChanged();
                    DeleteCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the name Rename gives the cell style chosen, as typed.</summary>
        public string NewName
        {
            get => _newName;
            set => Set(ref _newName, value ?? string.Empty);
        }

        /// <summary>Gets whether the cell style chosen is one a user made, which Rename and Delete take.</summary>
        public bool CanChange =>
            !string.IsNullOrEmpty(_selected) && _selected != "Title" && _selected != "Header" && _selected != "Data";

        /// <summary>Gets what the last button said where it was refused; empty otherwise.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets New….</summary>
        public DelegateCommand NewCommand { get; }

        /// <summary>Gets Rename.</summary>
        public DelegateCommand RenameCommand { get; }

        /// <summary>Gets Delete.</summary>
        public DelegateCommand DeleteCommand { get; }

        /// <summary>Gets Close.</summary>
        public DelegateCommand CloseCommand { get; }

        /// <summary>Asks for a name and a start in the Create New Cell Style dialog, then makes the cell style.</summary>
        public void New()
        {
            if (Ask == null)
            {
                Message = "The Create New Cell Style dialog is shown by the window.";
                return;
            }

            var dialog = new CreateCellStyleViewModel(Styles.ToList());

            if (Ask(dialog) && dialog.IsConfirmed)
            {
                Report(
                    CadTableEditor.CreateCellStyle(_document, _tableStyle, dialog.Name, dialog.StartWith),
                    dialog.Name.Trim()
                );
            }
        }

        /// <summary>Renames the cell style chosen to the name typed.</summary>
        public void Rename()
        {
            string was = _selected;
            Report(CadTableEditor.RenameCellStyle(_document, _tableStyle, was, _newName), _newName.Trim());
        }

        /// <summary>Takes the cell style chosen out of the table style.</summary>
        public void Delete()
        {
            Report(CadTableEditor.DeleteCellStyle(_document, _tableStyle, _selected), null);
        }

        private void Report(CadEditResult result, string select)
        {
            Message = result.Succeeded ? string.Empty : result.Message;
            Reload();
            Selected = select != null && Styles.Contains(select) ? select : Styles.FirstOrDefault();
        }

        private void Reload()
        {
            DxfTableStyle style = CadTableStyleEditor.Find(_document.Database, _tableStyle);
            Styles.Clear();

            foreach (string name in style?.GetCellStyleNames() ?? new[] { "Title", "Header", "Data" })
            {
                Styles.Add(name);
            }
        }
    }
}
