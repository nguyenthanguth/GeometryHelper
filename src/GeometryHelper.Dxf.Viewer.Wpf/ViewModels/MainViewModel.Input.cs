using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A keyword of the options menu of dynamic input, the menu the ↓ of the box at the cursor opens: "Close", "Undo".
    /// </summary>
    public sealed class DynamicInputOptionViewModel : ObservableObject
    {
        private bool _isSelected;

        internal DynamicInputOptionViewModel(string keyword) => Keyword = keyword;

        /// <summary>Gets the keyword as the prompt writes it, the capital letters being what is typed: "Close", "mOde".</summary>
        public string Keyword { get; }

        /// <summary>Gets or sets whether the row is the one Enter or Space picks, highlighted; the first when the menu opens.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Keyword;
    }

    /// <summary>
    /// Dynamic input (DYN, F12) at the cursor, as AutoCAD 2021's: at the Command prompt what is typed with the AutoComplete
    /// list under it; in a command the prompt with the ↓ of its options menu, the pointer input fields X and Y (relative to the
    /// base point, as DYNPIFORMAT 0; # typed first for absolute, @ for relative) and the dimensional input of a second point, a distance and an angle,
    /// the length on a dimension line along the rubber band and the angle on an arc. Then the command line shown or hidden
    /// (Ctrl+9), and AutoCAD's Select Color dialog for the Color row of Properties and for the colour of new objects in the
    /// ribbon's colour box.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The choice of the Color row of Properties that shows AutoCAD's Select Color dialog.</summary>
        public const string SelectColorOption = "Select Color...";

        /// <summary>The choice of the Contents row of one MTEXT in Properties that opens the MTEXT editor on it, as the palette's button does.</summary>
        public const string EditMTextOption = "Edit in the MTEXT editor...";

        /// <summary>The last choice of the colour box of the ribbon's Properties panel, which shows the Select Color dialog for the colour of new objects.</summary>
        public const string MoreColorsOption = "More colors...";

        /// <summary>The commands whose points asked from a base point show the length and the angle, as AutoCAD's dimensional input.</summary>
        private static readonly string[] DimensionalCommands = { "LINE", "PLINE", "ARC", "CIRCLE", "ELLIPSE", "POLYGON", "XLINE", "RAY", "MOVE", "COPY", "STRETCH", "OFFSET" };

        private static readonly string[] CurrentColorNames = { "ByLayer", "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        private bool _commandLineShown = true;
        private IReadOnlyList<DynamicInputOptionViewModel> _dynamicOptions = Array.Empty<DynamicInputOptionViewModel>();
        private GeoPoint2 _bandAt;
        private bool _absolute;
        private string _lockedFirst;
        private string _lockedSecond;
        private bool _secondActive;
        private DelegateCommand _selectCurrentColor;

        /// <summary>
        /// Gets the toggle DYN of the status bar, AutoCAD's Dynamic Input, switched by F12 or typed DYN: while on, the prompt of
        /// the command running, the cursor's coordinates or its distance and angle from the base point, and what is typed are
        /// shown beside the cursor. It starts as Global settings say (<see cref="GlobalSettings.DynamicInput"/>), on by
        /// default as in AutoCAD, and switching it changes the settings in use.
        /// </summary>
        public StatusToggleViewModel DynamicInput { get; }

        /// <summary>
        /// Gets or sets whether the command line is shown under the canvas: Ctrl+9 shows or hides it, as in AutoCAD. Hidden,
        /// what is typed still reaches it, and dynamic input shows it beside the cursor.
        /// </summary>
        public bool IsCommandLineShown
        {
            get => _commandLineShown;
            set => Set(ref _commandLineShown, value);
        }

        /// <summary>
        /// Gets whether the box of dynamic input is shown at the cursor: DYN on and a command running, not while it edits a text
        /// in place, or DYN on at the Command prompt once something is typed ("REC"); with nothing typed and no command, nothing
        /// is shown.
        /// </summary>
        public bool IsDynamicInputShown => DynamicInput.IsOn && _current != null
            && (IsCommandRunning ? Prompt.Length > 0 && Session.Prompt?.InPlace == null : CommandText.Length > 0);

        /// <summary>
        /// Gets the prompt the box of dynamic input shows, as AutoCAD's tooltip words it: the message, "Specify first point:", or
        /// for a prompt with options the message and its "or", "Specify next point or", the options left to the ↓ menu; empty at
        /// the Command prompt and while the box is hidden.
        /// </summary>
        public string DynamicInputPrompt
        {
            get
            {
                if (!IsDynamicInputShown || !IsCommandRunning)
                {
                    return string.Empty;
                }

                CadPrompt prompt = Session.Prompt;

                if (prompt == null || prompt.Keywords.Count == 0)
                {
                    return Prompt;
                }

                return prompt.Kind == CadPromptKind.Keyword ? prompt.Message : prompt.Message + " or";
            }
        }

        /// <summary>Gets whether the prompt in the box has options, drawn as the ↓ that opens their menu (<see cref="OpenDynamicInputMenu"/>).</summary>
        public bool HasDynamicInputOptions => IsDynamicInputShown && IsCommandRunning && Session.Prompt != null && Session.Prompt.Keywords.Count > 0;

        /// <summary>Gets whether the options menu of dynamic input is open under the box: the keywords of the prompt, one per row.</summary>
        public bool IsDynamicInputMenuOpen => _dynamicOptions.Count > 0;

        /// <summary>Gets the rows of the options menu, empty while it is closed: for LINE's third point Close and Undo, the first highlighted.</summary>
        public IReadOnlyList<DynamicInputOptionViewModel> DynamicInputOptions => _dynamicOptions;

        /// <summary>
        /// Gets whether the AutoComplete list shows under the box of dynamic input at the cursor rather than above the command
        /// line: DYN on and no command running. The command line keeps its own list closed meanwhile.
        /// </summary>
        public bool IsSuggestionListAtCursor => IsSuggestionListOpen && DynamicInput.IsOn && !IsCommandRunning;

        /// <summary>Gets whether the AutoComplete list shows above the command line: open, with DYN off.</summary>
        public bool IsSuggestionListAtCommandLine => IsSuggestionListOpen && !IsSuggestionListAtCursor;

        /// <summary>
        /// Gets the value the box of dynamic input shows: what is typed, once something is; else, while a point, a distance or
        /// an angle is asked, the cursor's coordinates, "1 250.00, 400.00", or once the prompt has a base point the distance
        /// and angle to the cursor from it, "500.00 &lt; 90°", in the decimals of Global settings; empty for another prompt.
        /// </summary>
        public string DynamicInputValue
        {
            get
            {
                if (!IsDynamicInputShown)
                {
                    return string.Empty;
                }

                if (CommandText.Length > 0)
                {
                    return CommandText;
                }

                CadPrompt prompt = Session.Prompt;

                if (prompt.Kind != CadPromptKind.Point && prompt.Kind != CadPromptKind.Distance && prompt.Kind != CadPromptKind.Angle)
                {
                    return string.Empty;
                }

                // In the current UCS: its coordinates, and the angle counted from its X axis turned by ANGBASE, by ANGDIR.
                if (!prompt.BasePoint.HasValue)
                {
                    GeoPoint2 shown = Ucs.ToUcs(_bandAt);
                    return _settings.FormatNumber(shown.X, _settings.CoordinateDecimals) + ", " + _settings.FormatNumber(shown.Y, _settings.CoordinateDecimals);
                }

                GeoVector2 way = Ucs.ToUcs(_bandAt - prompt.BasePoint.Value);
                double dx = way.X;
                double dy = way.Y;
                double degrees = Session.CountAngle(Math.Atan2(dy, dx)) * 180.0 / Math.PI;
                return _settings.FormatNumber(Math.Sqrt(dx * dx + dy * dy), _settings.CoordinateDecimals) + " < " + _settings.FormatNumber(degrees, _settings.AngleDecimals) + "°";
            }
        }

        /// <summary>
        /// Gets whether dynamic input shows two fields, as AutoCAD's while a point is asked: at a second point, as LINE's next, the
        /// length and the angle from the base point (<see cref="IsDynamicInputPolar"/>); at another point with a base point the X
        /// and the Y from it, RECTANG's other corner 1 200 by 800 from the first; with none the X and the Y of the cursor. Tab
        /// moves from one to the other (<see cref="TabDynamicInput"/>), a number typed locks its field, and Enter takes the
        /// point from both.
        /// </summary>
        /// <remarks>A point prompt that also takes text, as ZOOM's takes a scale 2X, keeps the one box: a number typed there is not an X.</remarks>
        public bool IsDynamicInputFields => IsDynamicInputShown && Session?.Prompt?.Kind == CadPromptKind.Point && !Session.Prompt.AcceptsText;

        /// <summary>
        /// Gets whether the two fields are dimensional input, the length and the angle from the base point rather than X and Y:
        /// as AutoCAD's while a second point is asked: at a point with a base point of LINE, PLINE, ARC, CIRCLE, ELLIPSE,
        /// POLYGON, XLINE, RAY, MOVE, COPY, STRETCH and OFFSET, and of a grip edit, where the canvas shows the length in a
        /// field beside the middle of the rubber band and the angle in one near the base point, with no dimension line or arc
        /// drawn. RECTANG's other corner keeps X and Y, its two sizes.
        /// </summary>
        public bool IsDynamicInputPolar => IsDynamicInputFields && Session.Prompt.BasePoint.HasValue
            && (Session.IsGripEditing || Array.IndexOf(DimensionalCommands, Session.Current?.Name) >= 0);

        /// <summary>
        /// Gets whether the canvas shows the length in a field beside the middle of the rubber band: with the angle
        /// (<see cref="IsDynamicInputPolar"/>), or alone while a distance is asked from a base point, as CIRCLE's radius 250
        /// from its centre, POLYGON's radius and ELLIPSE's other axis. A number typed goes into the field, and Enter takes it
        /// as the command line does. A scale factor has no field, as in AutoCAD: it is no length of the drawing.
        /// </summary>
        public bool IsDynamicInputLength => IsDynamicInputPolar || Alone(CadPromptKind.Distance);

        /// <summary>
        /// Gets whether the canvas shows the angle in a field near the base point: with the length
        /// (<see cref="IsDynamicInputPolar"/>), or alone while an angle is asked from a base point, as ROTATE's 30° about its
        /// base point.
        /// </summary>
        public bool IsDynamicInputAngle => IsDynamicInputPolar || Alone(CadPromptKind.Angle);

        /// <summary>Gets whether the canvas shows dimensional input, the length (<see cref="IsDynamicInputLength"/>), the angle (<see cref="IsDynamicInputAngle"/>) or both, and the box at the cursor no value of its own.</summary>
        public bool IsDynamicInputDimensional => IsDynamicInputLength || IsDynamicInputAngle;

        /// <summary>
        /// Gets what the first field shows, the distance or the X: what is typed into it, else the value locked by Tab, else the
        /// cursor's, "500.00"; empty while the fields are not shown. A whole coordinate typed, "@300&lt;45" or "10,20", is shown
        /// across both fields, the @ left out.
        /// </summary>
        public string DynamicInputFirst => FieldText(false);

        /// <summary>Gets what the second field shows, the angle in degrees or the Y, as <see cref="DynamicInputFirst"/> does: "53.1".</summary>
        /// <remarks>With a base point and no dimensional input the X and the Y are the cursor's from the base point, relative as AutoCAD's DYNPIFORMAT 0.</remarks>
        public string DynamicInputSecond => FieldText(true);

        /// <summary>Gets whether the first field is locked, drawn with a lock: a value typed into it or locked by Tab, which Enter takes in place of the cursor's.</summary>
        public bool IsDynamicInputFirstLocked => FieldLocked(false);

        /// <summary>Gets whether the second field is locked, as <see cref="IsDynamicInputFirstLocked"/> says of the first.</summary>
        public bool IsDynamicInputSecondLocked => FieldLocked(true);

        /// <summary>Gets whether what is typed goes into the first field.</summary>
        public bool IsDynamicInputFirstActive => (IsDynamicInputFields && !SecondTyped()) || Alone(CadPromptKind.Distance);

        /// <summary>Gets whether what is typed goes into the second field: after Tab, or after the comma or the &lt; of a coordinate typed whole.</summary>
        public bool IsDynamicInputSecondActive => (IsDynamicInputFields && SecondTyped()) || Alone(CadPromptKind.Angle);

        /// <summary>
        /// Gets or sets the colour of new objects, the drawing's $CECOLOR, as the colour box of the ribbon's Properties panel
        /// shows it: ByLayer, ByBlock, Red to White or an index, "30". Setting a colour sets it as one undo step, and
        /// <see cref="MoreColorsOption"/> shows the Select Color dialog for it; empty with no drawing open.
        /// </summary>
        public string CurrentColor
        {
            get => _current == null ? string.Empty : SelectColorViewModel.Text(_current.Document.Database.Cecolor);

            set
            {
                if (_current == null || value == null || value == CurrentColor)
                {
                    return;
                }

                if (value == MoreColorsOption)
                {
                    PickCurrentColor();
                }
                else
                {
                    CadEditResult result = CadPropertyEditor.Set(_current.Document, null, "Color", value);

                    if (!result.Succeeded)
                    {
                        Say(result.Message);
                    }
                }

                RaiseCurrentColor();
            }
        }

        /// <summary>Gets the choices of the colour box: ByLayer, ByBlock, Red to White, the colour of new objects where it is another, and <see cref="MoreColorsOption"/>.</summary>
        public IReadOnlyList<string> CurrentColorOptions
        {
            get
            {
                var options = new List<string>(CurrentColorNames);
                string current = CurrentColor;

                if (current.Length > 0 && !options.Contains(current))
                {
                    options.Add(current);
                }

                options.Add(MoreColorsOption);
                return options;
            }
        }

        /// <summary>Gets the swatch of the colour of new objects as 0xRRGGBB: for ByLayer the current layer's, for ByBlock white.</summary>
        public int CurrentColorRgb
        {
            get
            {
                if (_current == null)
                {
                    return 0xFFFFFF;
                }

                DxfColor color = _current.Document.Database.Cecolor;
                return color.IsByLayer ? CurrentLayer?.Rgb ?? 0xFFFFFF : color.IsByBlock ? 0xFFFFFF : color.ColorValue & 0xFFFFFF;
            }
        }

        /// <summary>Gets the command of the swatch of the colour box: the Select Color dialog for the colour of new objects, as <see cref="MoreColorsOption"/>.</summary>
        public ICommand SelectCurrentColorCommand => _selectCurrentColor ?? (_selectCurrentColor = new DelegateCommand(_ =>
        {
            PickCurrentColor();
            RaiseCurrentColor();
        }));

        /// <summary>
        /// ↓ with the box of dynamic input shown: opens the menu of the prompt's options, the first highlighted.
        /// </summary>
        /// <returns>Whether the menu opened; false where the prompt has no options, when ↓ is the command line's.</returns>
        public bool OpenDynamicInputMenu()
        {
            if (!HasDynamicInputOptions)
            {
                return false;
            }

            var rows = Session.Prompt.Keywords.Select(k => new DynamicInputOptionViewModel(k)).ToList();
            rows[0].IsSelected = true;
            SetDynamicOptions(rows);
            return true;
        }

        /// <summary>Moves the highlighted row of the options menu down or up, as the arrows do, stopping at either end.</summary>
        /// <param name="step">1 for down, -1 for up.</param>
        public void MoveDynamicInputOption(int step)
        {
            if (_dynamicOptions.Count == 0)
            {
                return;
            }

            int index = Math.Max(0, Math.Min(_dynamicOptions.Count - 1, _dynamicOptions.ToList().FindIndex(o => o.IsSelected) + step));

            for (int i = 0; i < _dynamicOptions.Count; i++)
            {
                _dynamicOptions[i].IsSelected = i == index;
            }
        }

        /// <summary>Picks a row of the options menu, or the highlighted one where none is given, as Enter, Space or a click does: the menu closes and the prompt takes the keyword.</summary>
        /// <param name="row">The row; null for the one highlighted.</param>
        /// <returns>False where the menu is closed.</returns>
        public bool PickDynamicInputOption(DynamicInputOptionViewModel row = null)
        {
            row = row ?? _dynamicOptions.FirstOrDefault(o => o.IsSelected);
            CloseDynamicInputMenu();

            if (row == null)
            {
                return false;
            }

            CommandText = string.Empty;
            Execute(row.Keyword);
            return true;
        }

        /// <summary>Closes the options menu, as Esc, a key typed or a click elsewhere does; the command goes on.</summary>
        public void CloseDynamicInputMenu() => SetDynamicOptions(Array.Empty<DynamicInputOptionViewModel>());

        /// <summary>
        /// Tab while the fields of dynamic input are shown, as AutoCAD's: a number typed locks the field it was typed into, and
        /// the other field takes what is typed next. Locked distance 300 and Enter take the point 300 from the base point in
        /// the cursor's direction; locked angle 45 takes the cursor's distance at 45°. A # typed before the first number makes
        /// the point absolute.
        /// </summary>
        /// <returns>Whether the fields took the Tab; false while they are not shown, when Tab is the window's.</returns>
        public bool TabDynamicInput()
        {
            if (!IsDynamicInputFields)
            {
                return false;
            }

            string whole = CommandText.Trim();
            string typed = Bare(whole);

            if (typed.Length > 0 && !IsNumber(typed))
            {
                // A coordinate typed whole or a keyword is left as the command line takes it.
                return true;
            }

            if (typed.Length > 0)
            {
                _absolute |= whole[0] == '#';

                if (_secondActive)
                {
                    _lockedSecond = typed;
                }
                else
                {
                    _lockedFirst = typed;
                }

                CommandText = string.Empty;
            }

            _secondActive = !_secondActive;
            RaiseDynamicInput();
            return true;
        }

        /// <summary>
        /// The point the fields of dynamic input hold the cursor to, which the canvas previews and a click takes, as AutoCAD's
        /// locked fields constrain the cursor: a length of 300 locked by Tab or typed holds it 300 from the base point towards
        /// the cursor, an angle of 45 on the line at 45° at the cursor's distance, an X or a Y on that X or Y. It is the point
        /// Enter would take with the cursor there.
        /// </summary>
        /// <param name="cursor">The cursor, snapped where a snap holds it, in world coordinates.</param>
        /// <returns>The point held; null while no field holds a number, when the cursor is free.</returns>
        public GeoPoint2? HoldDynamicInput(GeoPoint2 cursor)
        {
            string typed = Bare(CommandText.Trim());
            bool length = Alone(CadPromptKind.Distance);

            if (length || Alone(CadPromptKind.Angle))
            {
                return IsNumber(typed) ? FieldPoint(length ? typed : null, length ? null : typed, true, false, cursor) : (GeoPoint2?)null;
            }

            if (!IsDynamicInputFields || (typed.Length > 0 && !IsNumber(typed)))
            {
                return null;
            }

            string first = typed.Length > 0 && !_secondActive ? typed : _lockedFirst;
            string second = typed.Length > 0 && _secondActive ? typed : _lockedSecond;

            if (first == null && second == null)
            {
                return null;
            }

            return FieldPoint(first, second, IsDynamicInputPolar, _absolute || CommandText.TrimStart().StartsWith("#", StringComparison.Ordinal), cursor);
        }

        /// <summary>
        /// Gets or sets what shows AutoCAD's Select Color dialog for the Color row of Properties (its "Select Color..."), as the
        /// window shows <see cref="SelectColorWindow"/>: given the dialog's logic, it says whether OK was pressed. Null for
        /// none, when the choice says so on the command line.
        /// </summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>The toggle of the status bar of a name, DYN among them; null for none.</summary>
        private StatusToggleViewModel FindToggle(string name)
            => string.Equals(name, DynamicInput.Name, StringComparison.OrdinalIgnoreCase) ? DynamicInput : Toggles.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>DYN switched: the settings in use follow it, and the box is shown or hidden; the AutoComplete list moves between the cursor and the command line.</summary>
        private void OnDynamicInputChanged(object sender, PropertyChangedEventArgs e)
        {
            _settings.DynamicInput = DynamicInput.IsOn;
            CloseDynamicInputMenu();
            RaiseDynamicInput();
            RaiseSuggestionPlace();
        }

        private void SetDynamicOptions(IReadOnlyList<DynamicInputOptionViewModel> rows)
        {
            if (_dynamicOptions.Count == 0 && rows.Count == 0)
            {
                return;
            }

            _dynamicOptions = rows;
            Raise(nameof(DynamicInputOptions));
            Raise(nameof(IsDynamicInputMenuOpen));
        }

        private void RaiseSuggestionPlace()
        {
            Raise(nameof(IsSuggestionListAtCursor));
            Raise(nameof(IsSuggestionListAtCommandLine));
        }

        /// <summary>What the box of dynamic input shows follows the prompt, the cursor and what is typed; a new prompt unlocks its fields.</summary>
        private void OnInputSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Prompt):
                case nameof(IsCommandRunning):
                case nameof(HasDocument):
                    _lockedFirst = null;
                    _lockedSecond = null;
                    _secondActive = false;
                    _absolute = false;
                    CloseDynamicInputMenu();
                    RaiseDynamicInput();
                    RaiseSuggestionPlace();
                    break;
                case nameof(CommandText):
                    // A key typed with the options menu open closes it and goes into the field.
                    if (CommandText.Length > 0)
                    {
                        CloseDynamicInputMenu();
                    }

                    RaiseDynamicInput();
                    break;
                case nameof(CursorText):
                    RaiseDynamicInput();
                    break;
                case nameof(IsSuggestionListOpen):
                    RaiseSuggestionPlace();
                    break;
            }
        }

        private void RaiseDynamicInput()
        {
            Raise(nameof(IsDynamicInputShown));
            Raise(nameof(DynamicInputPrompt));
            Raise(nameof(HasDynamicInputOptions));
            Raise(nameof(DynamicInputValue));
            Raise(nameof(IsDynamicInputFields));
            Raise(nameof(IsDynamicInputPolar));
            Raise(nameof(IsDynamicInputLength));
            Raise(nameof(IsDynamicInputAngle));
            Raise(nameof(IsDynamicInputDimensional));
            Raise(nameof(DynamicInputFirst));
            Raise(nameof(DynamicInputSecond));
            Raise(nameof(IsDynamicInputFirstLocked));
            Raise(nameof(IsDynamicInputSecondLocked));
            Raise(nameof(IsDynamicInputFirstActive));
            Raise(nameof(IsDynamicInputSecondActive));
        }

        private void RaiseCurrentColor()
        {
            Raise(nameof(CurrentColor));
            Raise(nameof(CurrentColorOptions));
            Raise(nameof(CurrentColorRgb));
        }

        /// <summary>
        /// Enter with the fields of dynamic input shown: the point from a field locked or a number typed into one, the other
        /// field the cursor's, fed to the prompt as a click is; with a base point the X and the Y are from it unless # was typed
        /// first. A coordinate typed whole goes to the command line, made relative where a base point is ("1200,800" as
        /// "@1200,800", "#1200,800" as "1200,800"). A keyword, or Enter with nothing typed or locked, is left for the command
        /// line, as is a length alone, which the command line takes along the rubber band towards the cursor.
        /// </summary>
        /// <param name="typed">What was typed.</param>
        /// <returns>Whether the fields gave the answer.</returns>
        private bool TakeDynamicInput(string typed)
        {
            bool fields = IsDynamicInputFields;
            bool polar = IsDynamicInputPolar;
            string first = _lockedFirst;
            string second = _lockedSecond;
            bool secondActive = _secondActive;
            bool absolute = _absolute;
            _lockedFirst = null;
            _lockedSecond = null;
            _secondActive = false;
            _absolute = false;
            CloseDynamicInputMenu();
            string text = typed.Trim();

            if (!fields)
            {
                return false;
            }

            GeoPoint2? basePoint = Session.Prompt.BasePoint;

            if (text.Length > 0 && !IsNumber(Bare(text)))
            {
                string whole = WholeCoordinate(text, basePoint.HasValue);

                if (whole == null)
                {
                    return false;
                }

                Execute(whole);
                return true;
            }

            if (text.Length > 0)
            {
                absolute |= text[0] == '#';
                text = Bare(text);
            }

            if (text.Length > 0)
            {
                if (secondActive)
                {
                    second = text;
                }
                else
                {
                    first = text;
                }
            }

            if (first == null && second == null)
            {
                return false;
            }

            if (polar && second == null && !absolute)
            {
                Execute(first);
                return true;
            }

            Session.Feed(FieldPoint(first, second, polar, absolute, _bandAt));
            SyncHistory();
            RunAfterCommand();
            return true;
        }

        /// <summary>The point the two fields give with the cursor at a place: a field with no number takes the cursor's value.</summary>
        private GeoPoint2 FieldPoint(string first, string second, bool polar, bool absolute, GeoPoint2 at)
        {
            // The fields are in the current UCS, the point fed in the world's.
            GeoPoint2? basePoint = Session.Prompt.BasePoint;
            CadUcs ucs = Ucs;

            if (polar && !absolute)
            {
                GeoPoint2 from = basePoint.Value;
                double length = first != null ? Number(first) : (at - from).Length;

                if (second == null)
                {
                    // The length alone: towards the cursor.
                    GeoVector2 towards = at - from;
                    double far = towards.Length;
                    return far > 0.0 ? from + new GeoVector2(towards.X * length / far, towards.Y * length / far) : from;
                }

                // The angle box counts as the drawing does: from ANGBASE, clockwise with ANGDIR 1.
                double angle = CadUnits.ToDirection(Number(second) * Math.PI / 180.0, Session.Document.Database.Angbase, Session.Document.Database.Angdir);
                return from + ucs.ToWorld(new GeoVector2(length * Math.Cos(angle), length * Math.Sin(angle)));
            }

            if (basePoint.HasValue && !absolute)
            {
                // Relative, as DYNPIFORMAT 0: the X and the Y from the base point, the one not typed the cursor's.
                GeoVector2 way = ucs.ToUcs(at - basePoint.Value);
                return basePoint.Value + ucs.ToWorld(new GeoVector2(first != null ? Number(first) : way.X, second != null ? Number(second) : way.Y));
            }

            GeoPoint2 cursor = ucs.ToUcs(at);
            return ucs.ToWorld(new GeoPoint2(first != null ? Number(first) : cursor.X, second != null ? Number(second) : cursor.Y));
        }

        /// <summary>
        /// "More colors..." of the colour box: the Select Color dialog on the colour of new objects, ByLayer and ByBlock offered,
        /// the colour chosen set as one undo step. A true colour is refused, said on the command line: the header holds an index.
        /// </summary>
        private void PickCurrentColor()
        {
            if (_current == null)
            {
                return;
            }

            DxfColor old = _current.Document.Database.Cecolor;
            var dialog = new SelectColorViewModel(old, true);

            if (PickColor == null)
            {
                Say("The Select Color dialog is shown by the window.");
            }
            else if (PickColor(dialog) && !dialog.Color.Equals(old))
            {
                if (dialog.Color.IsByColor)
                {
                    Say("The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.");
                    return;
                }

                CadEditResult result = CadPropertyEditor.Set(_current.Document, null, "Color", SelectColorViewModel.Text(dialog.Color));

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }
            }
        }

        /// <summary>The text of a field: typed, locked or the cursor's.</summary>
        private string FieldText(bool second)
        {
            if (Alone(second ? CadPromptKind.Angle : CadPromptKind.Distance))
            {
                return CommandText.Length > 0 ? CommandText : Measured(second, Session.Prompt.BasePoint.Value);
            }

            if (!IsDynamicInputFields)
            {
                return string.Empty;
            }

            if (Parted(out string before, out string after))
            {
                return second ? after : before;
            }

            if (CommandText.Length > 0 && second == _secondActive)
            {
                return Bare(CommandText);
            }

            string locked = second ? _lockedSecond : _lockedFirst;

            if (locked != null)
            {
                return locked;
            }

            GeoPoint2? from = Session.Prompt.BasePoint;

            if (!from.HasValue)
            {
                GeoPoint2 shown = Ucs.ToUcs(_bandAt);
                return _settings.FormatNumber(second ? shown.Y : shown.X, _settings.CoordinateDecimals);
            }

            if (IsDynamicInputPolar)
            {
                return Measured(second, from.Value);
            }

            GeoVector2 way = Ucs.ToUcs(_bandAt - from.Value);
            return _settings.FormatNumber(second ? way.Y : way.X, _settings.CoordinateDecimals);
        }

        /// <summary>The length to the cursor from a point, or its angle from the X axis of the UCS in degrees, 0 to 360, as a field shows it.</summary>
        private string Measured(bool angle, GeoPoint2 from)
        {
            GeoVector2 way = Ucs.ToUcs(_bandAt - from);
            double dx = way.X;
            double dy = way.Y;

            if (!angle)
            {
                return _settings.FormatNumber(Math.Sqrt(dx * dx + dy * dy), _settings.CoordinateDecimals);
            }

            return _settings.FormatNumber(Session.CountAngle(Math.Atan2(dy, dx)) * 180.0 / Math.PI, _settings.AngleDecimals);
        }

        private bool FieldLocked(bool second)
        {
            if (Alone(second ? CadPromptKind.Angle : CadPromptKind.Distance))
            {
                return CommandText.Length > 0;
            }

            if (!IsDynamicInputFields)
            {
                return false;
            }

            if (Parted(out string before, out string after))
            {
                return (second ? after : before).Trim().Length > 0;
            }

            return (CommandText.Length > 0 && second == _secondActive) || (second ? _lockedSecond : _lockedFirst) != null;
        }

        /// <summary>Whether the prompt asks a distance or an angle from a base point, shown in the one field of its kind on the canvas; a scale factor is not.</summary>
        private bool Alone(CadPromptKind kind)
        {
            CadPrompt prompt = IsDynamicInputShown && IsCommandRunning ? Session.Prompt : null;
            return prompt != null && prompt.Kind == kind && prompt.BasePoint.HasValue && prompt.Message != "Specify scale factor";
        }

        private bool SecondTyped() => Parted(out _, out _) || _secondActive;

        /// <summary>What is typed parted at its &lt; or its comma into the two fields, an @ before it left out; false where it has neither.</summary>
        private bool Parted(out string before, out string after)
        {
            string typed = CommandText;
            int at = typed.IndexOf('<');
            at = at < 0 ? typed.IndexOf(',') : at;

            if (at < 0)
            {
                before = after = null;
                return false;
            }

            before = Bare(typed.Substring(0, at).Trim());
            after = typed.Substring(at + 1).Trim();
            return true;
        }

        /// <summary>A field's text without the # or @ typed first, which say absolute or relative.</summary>
        private static string Bare(string text) => text.Length > 0 && (text[0] == '#' || text[0] == '@') ? text.Substring(1).Trim() : text;

        /// <summary>
        /// A coordinate typed whole into the fields as the command line reads it: "#x,y" absolute as "x,y", "@x,y" as typed, and
        /// with a base point "x,y" or "d&lt;a" made relative, "@x,y"; null where the text is not two numbers parted by a comma
        /// or a &lt;, as a keyword is not.
        /// </summary>
        private static string WholeCoordinate(string text, bool based)
        {
            int at = text.IndexOf('<');
            at = at < 0 ? text.IndexOf(',') : at;

            if (at < 0 || !IsNumber(Bare(text.Substring(0, at).Trim())) || !IsNumber(text.Substring(at + 1).Trim()))
            {
                return null;
            }

            return text[0] == '#' ? text.Substring(1).Trim() : text[0] == '@' || !based ? text : "@" + text;
        }

        private static bool IsNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsNaN(value) && !double.IsInfinity(value);

        private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
