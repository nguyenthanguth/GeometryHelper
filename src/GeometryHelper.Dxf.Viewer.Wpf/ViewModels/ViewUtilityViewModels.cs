using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Viewports dialog of VPORTS (design ViewportsDialog and ViewportsNamed): New Viewports, a standard configuration
    /// applied to the display or to the current viewport and saved under a new name where one is typed; Named Viewports, a
    /// configuration saved brought back. Its logic is <see cref="CadTiledViewports"/>.
    /// </summary>
    public sealed class ViewportsViewModel : ObservableObject
    {
        /// <summary>The first entry of Standard viewports: the tiles as they are.</summary>
        public const string ActiveConfiguration = "*Active Model Configuration*";

        private readonly CadTiledViewports _tiles;
        private string _configuration = ActiveConfiguration;
        private string _newName = string.Empty;
        private string _applyTo = "Display";
        private bool _isNamedTab;
        private string _named;
        private string _message;

        /// <summary>Makes the dialog over the tiles of a drawing.</summary>
        /// <param name="tiles">The tiles.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tiles are null.</exception>
        public ViewportsViewModel(CadTiledViewports tiles)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            Configurations = new[] { ActiveConfiguration }.Concat(CadTiledViewports.StandardNames).ToList();
            _named = _tiles.Names.FirstOrDefault();
        }

        /// <summary>Gets the entries of Standard viewports, in AutoCAD's order.</summary>
        public IReadOnlyList<string> Configurations { get; }

        /// <summary>Gets or sets the entry picked; the preview follows.</summary>
        public string Configuration
        {
            get => _configuration;
            set
            {
                if (Set(ref _configuration, value))
                {
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets or sets the name the result is saved under; empty for none.</summary>
        public string NewName
        {
            get => _newName;
            set => Set(ref _newName, value ?? string.Empty);
        }

        /// <summary>Gets the choices of Apply to.</summary>
        public IReadOnlyList<string> ApplyToOptions { get; } = new[] { "Display", "Current Viewport" };

        /// <summary>Gets or sets Apply to: Display splits the whole drawing area, Current Viewport the current tile.</summary>
        public string ApplyTo
        {
            get => _applyTo;
            set
            {
                if (Set(ref _applyTo, value))
                {
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets or sets whether Named Viewports is the tab shown.</summary>
        public bool IsNamedTab
        {
            get => _isNamedTab;
            set
            {
                if (Set(ref _isNamedTab, value))
                {
                    Raise(nameof(IsNewTab));
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets or sets whether New Viewports is the tab shown.</summary>
        public bool IsNewTab
        {
            get => !_isNamedTab;
            set => IsNamedTab = !value;
        }

        /// <summary>Gets the names of the configurations saved.</summary>
        public IReadOnlyList<string> Names => _tiles.Names;

        /// <summary>Gets or sets the configuration picked on Named Viewports.</summary>
        public string Named
        {
            get => _named;
            set
            {
                if (Set(ref _named, value))
                {
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets what went wrong with OK, said in the dialog; null for nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>
        /// Gets the tiles the preview draws, as shares of the drawing area from (0, 0) at the lower left: the configuration
        /// picked, applied where Apply to says, or the one picked on Named Viewports; the current one first.
        /// </summary>
        public IReadOnlyList<DisplayBox> Preview
        {
            get
            {
                if (IsNamedTab)
                {
                    return _tiles.Saved(Named)?.Select(Box).ToList() ?? new List<DisplayBox>();
                }

                List<DisplayBox> now = _tiles.Tiles.Select(Box).ToList();
                IReadOnlyList<DisplayBox> pieces = CadTiledViewports.PiecesOf(Configuration);

                if (pieces == null)
                {
                    return now;
                }

                bool whole = ApplyTo == "Display";

                if (whole)
                {
                    return pieces.ToList();
                }

                DisplayBox current = now[0];
                var result = pieces
                    .Select(p => new DisplayBox(
                        current.MinX + p.MinX * current.Width,
                        current.MinY + p.MinY * current.Height,
                        current.MinX + p.MaxX * current.Width,
                        current.MinY + p.MaxY * current.Height
                    ))
                    .ToList();
                result.AddRange(now.Skip(1));
                return result;
            }
        }

        /// <summary>
        /// OK: Named Viewports restores the configuration picked; New Viewports applies the configuration picked and saves the
        /// result under the new name where one is typed. The drawing's tiles change, one undo step with the command's.
        /// </summary>
        /// <returns>Whether it was done; false with <see cref="Message"/> set where refused.</returns>
        public bool Apply()
        {
            if (IsNamedTab)
            {
                return Named == null || _tiles.Restore(Named);
            }

            if (Configuration != ActiveConfiguration)
            {
                string refused = _tiles.SplitAs(Configuration, ApplyTo == "Display");

                if (refused != null)
                {
                    Message = refused;
                    return false;
                }
            }

            if (NewName.Trim().Length > 0)
            {
                _tiles.Save(NewName.Trim());
            }

            return true;
        }

        /// <summary>Deletes the configuration picked on Named Viewports, as its right-click Delete.</summary>
        public void DeleteNamed()
        {
            if (Named == null)
            {
                return;
            }

            _tiles.Delete(Named.Replace("*", "`*").Replace("?", "`?").Replace("#", "`#").Replace("@", "`@"));
            Named = _tiles.Names.FirstOrDefault();
            Raise(nameof(Names));
        }

        private static DisplayBox Box(CadTiledViewport t) => new DisplayBox(t.Left, t.Bottom, t.Right, t.Top);
    }

    /// <summary>
    /// The Object Selection Filters dialog of FILTER (design SelectionFilters): the list of the filter's lines, Select
    /// Filter's property and value with Add to List, Substitute and Add Selected Object, Edit Item, Delete and Clear List,
    /// and Named Filters. The lines are edited on a copy, given to the filter by <see cref="Commit"/> (Apply and Add
    /// Selected Object), so Cancel leaves the filter as it was.
    /// </summary>
    public sealed class SelectionFiltersViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private readonly CadSelectionFilter _filter;
        private readonly List<CadFilterLine> _lines;
        private CadFilterProperty _property = CadFilterProperty.Object;
        private string _value = string.Empty;
        private int _selected = -1;
        private string _saveName = string.Empty;
        private string _message;

        /// <summary>Makes the dialog over a filter.</summary>
        /// <param name="document">The drawing, whose layers, colours, linetypes and kinds Select... lists.</param>
        /// <param name="filter">The filter.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public SelectionFiltersViewModel(CadDocument document, CadSelectionFilter filter)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _lines = new List<CadFilterLine>(filter.Lines);
            Lines = new ObservableCollection<string>(_lines.Select(l => l.Text));
            Names = new ObservableCollection<string>(filter.Names);
        }

        /// <summary>Gets the lines of the list as it shows them: "Layer                = WALLS".</summary>
        public ObservableCollection<string> Lines { get; }

        /// <summary>Gets or sets the index of the line picked; -1 for none.</summary>
        public int SelectedLine
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets the properties Select Filter offers.</summary>
        public IReadOnlyList<CadFilterProperty> Properties { get; } =
            new[]
            {
                CadFilterProperty.Object,
                CadFilterProperty.Layer,
                CadFilterProperty.Color,
                CadFilterProperty.Linetype,
            };

        /// <summary>Gets or sets the property picked; the value's list follows.</summary>
        public CadFilterProperty Property
        {
            get => _property;
            set
            {
                if (Set(ref _property, value))
                {
                    Raise(nameof(Values));
                    Value = Values.FirstOrDefault() ?? string.Empty;
                }
            }
        }

        /// <summary>Gets or sets the value, typed or picked (X: of AutoCAD's dialog).</summary>
        public string Value
        {
            get => _value;
            set => Set(ref _value, value ?? string.Empty);
        }

        /// <summary>
        /// Gets the values Select... lists for the property: the kinds of the objects of the drawing, its layers, the colours
        /// ByLayer, ByBlock and 1 to 7 then those in use, its linetypes; in ordinal order.
        /// </summary>
        public IReadOnlyList<string> Values
        {
            get
            {
                DxfDatabase db = _document.Database;

                switch (Property)
                {
                    case CadFilterProperty.Object:
                        return CadSelectionFilter.KindsIn(_document);
                    case CadFilterProperty.Layer:
                        return db.LayerTable.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    case CadFilterProperty.Linetype:
                        return new[] { "ByLayer", "ByBlock" }
                            .Concat(
                                db.LinetypeTable.Select(l => l.Name)
                                    .Where(n =>
                                        !string.Equals(n, "ByLayer", StringComparison.OrdinalIgnoreCase)
                                        && !string.Equals(n, "ByBlock", StringComparison.OrdinalIgnoreCase)
                                    )
                                    .OrderBy(n => n, StringComparer.Ordinal)
                            )
                            .ToList();
                    default:
                        return new[] { 256, 0, 1, 2, 3, 4, 5, 6, 7 }.Select(CadFilterLine.ColorWords).ToList();
                }
            }
        }

        /// <summary>Gets the names Current lists: *unnamed and the lists saved.</summary>
        public ObservableCollection<string> Names { get; }

        /// <summary>Gets or sets the list current, *unnamed or one saved; picking one saved puts its lines in the list.</summary>
        public string CurrentName
        {
            get => _filter.CurrentName;
            set
            {
                if (value == null || value == _filter.CurrentName)
                {
                    return;
                }

                if (_filter.Restore(value))
                {
                    _lines.Clear();
                    _lines.AddRange(_filter.Lines);
                    Refresh();
                }

                Raise(nameof(CurrentName));
            }
        }

        /// <summary>Gets or sets the name Save As saves under.</summary>
        public string SaveName
        {
            get => _saveName;
            set => Set(ref _saveName, value ?? string.Empty);
        }

        /// <summary>Gets what AutoCAD would say of the last action, as "Invalid Color number."; null for nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets whether OK closed the dialog to pick an object for Add Selected Object.</summary>
        public bool IsAddingSelected { get; private set; }

        /// <summary>Add to List: the property and value as a line under the one picked, or at the end.</summary>
        /// <returns>Whether it was added; a colour AutoCAD refuses is not.</returns>
        public bool AddToList()
        {
            CadFilterLine line = Make();

            if (line == null)
            {
                return false;
            }

            int at = SelectedLine >= 0 && SelectedLine < _lines.Count ? SelectedLine + 1 : _lines.Count;
            _lines.Insert(at, line);
            Refresh();
            SelectedLine = at;
            return true;
        }

        /// <summary>Substitute: the line picked replaced by the property and value.</summary>
        /// <returns>Whether it was replaced.</returns>
        public bool Substitute()
        {
            if (SelectedLine < 0 || SelectedLine >= _lines.Count)
            {
                return false;
            }

            CadFilterLine line = Make();

            if (line == null)
            {
                return false;
            }

            int at = SelectedLine;
            _lines[at] = line;
            Refresh();
            SelectedLine = at;
            return true;
        }

        /// <summary>Edit Item: the line picked put into Select Filter, to be changed and Substituted.</summary>
        public void EditItem()
        {
            if (SelectedLine < 0 || SelectedLine >= _lines.Count)
            {
                return;
            }

            CadFilterLine line = _lines[SelectedLine];
            _property = line.Property;
            Raise(nameof(Property));
            Raise(nameof(Values));
            Value = line.Value;
        }

        /// <summary>Delete: the line picked taken out.</summary>
        public void Delete()
        {
            if (SelectedLine < 0 || SelectedLine >= _lines.Count)
            {
                return;
            }

            int at = SelectedLine;
            _lines.RemoveAt(at);
            Refresh();
            SelectedLine = Math.Min(at, _lines.Count - 1);
        }

        /// <summary>Clear List: every line taken out.</summary>
        public void ClearList()
        {
            _lines.Clear();
            Refresh();
        }

        /// <summary>Save As: the lines saved under <see cref="SaveName"/>, which becomes current.</summary>
        public void SaveAs()
        {
            Commit();
            Message = _filter.SaveAs(SaveName);

            if (Message == null && !Names.Contains(_filter.CurrentName))
            {
                Names.Clear();

                foreach (string name in _filter.Names)
                {
                    Names.Add(name);
                }
            }

            Raise(nameof(CurrentName));
        }

        /// <summary>Delete Current Filter List: the list current deleted where it has a name.</summary>
        public void DeleteCurrentFilter()
        {
            string name = _filter.CurrentName;
            Message = _filter.DeleteCurrent();

            if (Message == null)
            {
                Names.Remove(name);
            }

            Raise(nameof(CurrentName));
        }

        /// <summary>Add Selected Object &lt;: the lines given to the filter and the dialog closed for the pick.</summary>
        public void AddSelectedObject()
        {
            Commit();
            IsAddingSelected = true;
        }

        /// <summary>Gives the lines edited to the filter, as Apply does.</summary>
        public void Commit()
        {
            _filter.Lines.Clear();

            foreach (CadFilterLine line in _lines)
            {
                _filter.Lines.Add(line);
            }
        }

        private CadFilterLine Make()
        {
            try
            {
                Message = null;
                return new CadFilterLine(Property, Value);
            }
            catch (ArgumentException ex)
            {
                Message = ex.Message.Split('\r', '\n')[0].Replace(" (Parameter 'value')", string.Empty);
                Message = Message.StartsWith("Invalid Color number.", StringComparison.Ordinal)
                    ? "Invalid Color number."
                    : Message;
                return null;
            }
        }

        private void Refresh()
        {
            Lines.Clear();

            foreach (CadFilterLine line in _lines)
            {
                Lines.Add(line.Text);
            }
        }
    }

    /// <summary>A line of QuickCalc's history: the expression and its value.</summary>
    public sealed class QuickCalcLine
    {
        /// <summary>Makes a line.</summary>
        /// <param name="expression">The expression.</param>
        /// <param name="value">The value.</param>
        public QuickCalcLine(string expression, CadCalcValue value)
        {
            Expression = expression;
            Value = value;
        }

        /// <summary>Gets the expression.</summary>
        public string Expression { get; }

        /// <summary>Gets the value.</summary>
        public CadCalcValue Value { get; }

        /// <summary>Gets the value as the history shows it: 7, 6.0, (120.0 80.0 0.0).</summary>
        public string Shown => Value.ToString();
    }

    /// <summary>
    /// The QuickCalc palette of QUICKCALC (design QuickCalc): the input, the history, the number pad with its memory, the
    /// scientific functions, and the buttons that pick points in the drawing (Get Coordinates, Distance, Angle,
    /// Intersection) through CAL. The expressions are those of <see cref="CadCalculator"/>.
    /// </summary>
    public sealed class QuickCalcViewModel : ObservableObject
    {
        private readonly Action<string, Action<CadCalcValue>> _pick;
        private readonly Action<string> _paste;
        private string _input = string.Empty;
        private string _message;
        private double _memory;
        private CadCalcValue? _last;

        /// <summary>Makes the palette.</summary>
        /// <param name="pick">Runs CAL with an expression of <c>cur</c> points and gives back its value; null where no drawing is open.</param>
        /// <param name="paste">Puts a text on the command line, as Paste value to command line; null for none.</param>
        public QuickCalcViewModel(Action<string, Action<CadCalcValue>> pick, Action<string> paste)
        {
            _pick = pick;
            _paste = paste;
        }

        /// <summary>Gets the keys of the number pad, row by row: the memory key first, then five.</summary>
        public IReadOnlyList<string> PadKeys { get; } =
            new[]
            {
                "MC",
                "C",
                "←",
                "sqrt",
                "/",
                "1/x",
                "MR",
                "7",
                "8",
                "9",
                "*",
                "x^2",
                "MS",
                "4",
                "5",
                "6",
                "-",
                "x^3",
                "M+",
                "1",
                "2",
                "3",
                "+",
                "x^y",
                "M-",
                "0",
                ".",
                "pi",
                "(",
                ")",
            };

        /// <summary>Gets the keys of Scientific.</summary>
        public IReadOnlyList<string> ScientificKeys { get; } =
            new[]
            {
                "sin",
                "cos",
                "tan",
                "log",
                "10^x",
                "asin",
                "acos",
                "atan",
                "ln",
                "e^x",
                "r2d",
                "d2r",
                "abs",
                "rnd",
                "trunc",
            };

        /// <summary>Gets the lines of the history, the first first.</summary>
        public ObservableCollection<QuickCalcLine> History { get; } = new ObservableCollection<QuickCalcLine>();

        /// <summary>Gets or sets the expression in the input box.</summary>
        public string Input
        {
            get => _input;
            set => Set(ref _input, value ?? string.Empty);
        }

        /// <summary>Gets the value of the last expression, as the box under the input shows it.</summary>
        public string Result => _last?.ToString() ?? string.Empty;

        /// <summary>Gets what was wrong with the last expression, as "Error in Expression: Division by 0"; null for nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the number the memory holds.</summary>
        public double Memory => _memory;

        /// <summary>= or Enter: the input evaluated, a line of history added and the value put in the input.</summary>
        /// <returns>Whether it was evaluated.</returns>
        public bool Evaluate()
        {
            string expression = Input.Trim();

            if (expression.Length == 0)
            {
                return false;
            }

            try
            {
                CadCalcValue value = CadCalculator.Evaluate(expression);
                History.Add(new QuickCalcLine(expression, value));
                _last = value;
                Message = null;
                Input = value.ToInput();
                Raise(nameof(Result));
                return true;
            }
            catch (CadCalcException ex)
            {
                Message = "Error in Expression: " + ex.Message;
                return false;
            }
        }

        /// <summary>A key of the number pad or of Scientific pressed.</summary>
        /// <param name="key">The key, as <see cref="PadKeys"/> and <see cref="ScientificKeys"/> name it.</param>
        public void Press(string key)
        {
            switch (key)
            {
                case "=":
                    Evaluate();
                    return;
                case "C":
                    Input = string.Empty;
                    return;
                case "←":
                    Input = Input.Length > 0 ? Input.Substring(0, Input.Length - 1) : Input;
                    return;
                case "MC":
                    _memory = 0.0;
                    Raise(nameof(Memory));
                    return;
                case "MR":
                    Input += _memory.ToString("R", CultureInfo.InvariantCulture);
                    return;
                case "MS":
                case "M+":
                case "M-":
                    if (Evaluate() && _last.HasValue && !_last.Value.IsVector)
                    {
                        double n = _last.Value.Number;
                        _memory =
                            key == "MS" ? n
                            : key == "M+" ? _memory + n
                            : _memory - n;
                        Raise(nameof(Memory));
                    }

                    return;
                case "1/x":
                    Input = "1/(" + Input + ")";
                    return;
                case "x^2":
                    Input = "(" + Input + ")^2";
                    return;
                case "x^3":
                    Input = "(" + Input + ")^3";
                    return;
                case "x^y":
                    Input += "^";
                    return;
                case "10^x":
                    Input = "exp10(" + Input + ")";
                    return;
                case "e^x":
                    Input = "exp(" + Input + ")";
                    return;
                case "tan":
                    Input = "tang(" + Input + ")";
                    return;
                case "rnd":
                    Input = "round(" + Input + ")";
                    return;
                case "sqrt":
                case "sin":
                case "cos":
                case "log":
                case "asin":
                case "acos":
                case "atan":
                case "ln":
                case "r2d":
                case "d2r":
                case "abs":
                case "trunc":
                    Input = key + "(" + Input + ")";
                    return;
                default:
                    Input += key;
                    return;
            }
        }

        /// <summary>Clear: the input emptied.</summary>
        public void Clear() => Input = string.Empty;

        /// <summary>Clear History.</summary>
        public void ClearHistory() => History.Clear();

        /// <summary>A line of the history clicked: its expression back in the input.</summary>
        /// <param name="line">The line.</param>
        public void Recall(QuickCalcLine line)
        {
            if (line != null)
            {
                Input = line.Expression;
            }
        }

        /// <summary>Paste value to command line: the value of the input given to the command line.</summary>
        public void PasteToCommandLine()
        {
            if (Evaluate() && _last.HasValue)
            {
                CadCalcValue v = _last.Value;
                string text = v.IsVector
                    ? string.Join(
                        ",",
                        new[] { v.X, v.Y, v.Z }.Select(d => d.ToString("R", CultureInfo.InvariantCulture))
                    )
                    : v.Number.ToString("R", CultureInfo.InvariantCulture);
                _paste?.Invoke(text);
            }
        }

        /// <summary>Get Coordinates: a point picked in the drawing, put into the input.</summary>
        public void GetCoordinates() => Pick("cur");

        /// <summary>Distance Between Two Points: two points picked, their distance put into the input.</summary>
        public void Distance() => Pick("dist(cur,cur)");

        /// <summary>Angle of Line Defined by Two Points.</summary>
        public void Angle() => Pick("ang(cur,cur)");

        /// <summary>Intersection of Two Lines Defined by Four Points.</summary>
        public void Intersection() => Pick("ill(cur,cur,cur,cur)");

        private void Pick(string expression)
        {
            if (_pick == null)
            {
                Message = "Open a drawing to pick points in it.";
                return;
            }

            _pick(expression, value => Input += value.ToInput());
        }
    }
}
