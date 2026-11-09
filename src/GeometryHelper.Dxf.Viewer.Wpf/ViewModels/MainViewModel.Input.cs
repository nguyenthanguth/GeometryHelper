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
    /// Dynamic input (DYN, F12) with its two fields at a point prompt, the command line shown or hidden (Ctrl+9), and AutoCAD's
    /// Select Color dialog for the Color row of Properties and for the colour of new objects in the ribbon's colour box.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The choice of the Color row of Properties that shows AutoCAD's Select Color dialog.</summary>
        public const string SelectColorOption = "Select Color...";

        /// <summary>The last choice of the colour box of the ribbon's Properties panel, which shows the Select Color dialog for the colour of new objects.</summary>
        public const string MoreColorsOption = "More colors...";

        private static readonly string[] CurrentColorNames = { "ByLayer", "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        private bool _commandLineShown = true;
        private GeoPoint2 _cursorAt;
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

        /// <summary>Gets whether the box of dynamic input is shown: DYN on and a command running, not while it edits a text in place; with none, nothing is shown.</summary>
        public bool IsDynamicInputShown => DynamicInput.IsOn && IsCommandRunning && Prompt.Length > 0 && Session?.Prompt?.InPlace == null;

        /// <summary>Gets the prompt the box of dynamic input shows, as the command line's: "Specify next point or [Undo]:"; empty while it is hidden.</summary>
        public string DynamicInputPrompt => IsDynamicInputShown ? Prompt : string.Empty;

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

                // In the current UCS: its coordinates, and the angle from its X axis.
                if (!prompt.BasePoint.HasValue)
                {
                    GeoPoint2 shown = Ucs.ToUcs(_cursorAt);
                    return _settings.FormatNumber(shown.X, _settings.CoordinateDecimals) + ", " + _settings.FormatNumber(shown.Y, _settings.CoordinateDecimals);
                }

                GeoVector2 way = Ucs.ToUcs(_cursorAt - prompt.BasePoint.Value);
                double dx = way.X;
                double dy = way.Y;
                double degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
                return _settings.FormatNumber(Math.Sqrt(dx * dx + dy * dy), _settings.CoordinateDecimals) + " < " + _settings.FormatNumber(degrees, _settings.AngleDecimals) + "°";
            }
        }

        /// <summary>
        /// Gets whether the box of dynamic input shows two fields, as AutoCAD's while a point is asked: the distance and the
        /// angle from the prompt's base point, or with none the X and the Y of the cursor. Tab moves from one to the other
        /// (<see cref="TabDynamicInput"/>), a number typed locks its field, and Enter takes the point from both.
        /// </summary>
        /// <remarks>A point prompt that also takes text, as ZOOM's takes a scale 2X, keeps the one box: a number typed there is not an X.</remarks>
        public bool IsDynamicInputFields => IsDynamicInputShown && Session?.Prompt?.Kind == CadPromptKind.Point && !Session.Prompt.AcceptsText;

        /// <summary>Gets whether the two fields are the distance and the angle from the base point rather than X and Y.</summary>
        public bool IsDynamicInputPolar => IsDynamicInputFields && Session.Prompt.BasePoint.HasValue;

        /// <summary>
        /// Gets what the first field shows, the distance or the X: what is typed into it, else the value locked by Tab, else the
        /// cursor's, "500.00"; empty while the fields are not shown. A whole coordinate typed, "@300&lt;45" or "10,20", is shown
        /// across both fields, the @ left out.
        /// </summary>
        public string DynamicInputFirst => FieldText(false);

        /// <summary>Gets what the second field shows, the angle in degrees or the Y, as <see cref="DynamicInputFirst"/> does: "53.1".</summary>
        public string DynamicInputSecond => FieldText(true);

        /// <summary>Gets whether the first field is locked, drawn with a lock: a value typed into it or locked by Tab, which Enter takes in place of the cursor's.</summary>
        public bool IsDynamicInputFirstLocked => FieldLocked(false);

        /// <summary>Gets whether the second field is locked, as <see cref="IsDynamicInputFirstLocked"/> says of the first.</summary>
        public bool IsDynamicInputSecondLocked => FieldLocked(true);

        /// <summary>Gets whether what is typed goes into the first field.</summary>
        public bool IsDynamicInputFirstActive => IsDynamicInputFields && !SecondTyped();

        /// <summary>Gets whether what is typed goes into the second field: after Tab, or after the comma or the &lt; of a coordinate typed whole.</summary>
        public bool IsDynamicInputSecondActive => IsDynamicInputFields && SecondTyped();

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
        /// Tab while the fields of dynamic input are shown, as AutoCAD's: a number typed locks the field it was typed into, and
        /// the other field takes what is typed next. Locked distance 300 and Enter take the point 300 from the base point in
        /// the cursor's direction; locked angle 45 takes the cursor's distance at 45°.
        /// </summary>
        /// <returns>Whether the fields took the Tab; false while they are not shown, when Tab is the window's.</returns>
        public bool TabDynamicInput()
        {
            if (!IsDynamicInputFields)
            {
                return false;
            }

            string typed = CommandText.Trim();

            if (typed.Length > 0 && !IsNumber(typed))
            {
                // A coordinate typed whole or a keyword is left as the command line takes it.
                return true;
            }

            if (typed.Length > 0)
            {
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
        /// Gets or sets what shows AutoCAD's Select Color dialog for the Color row of Properties (its "Select Color..."), as the
        /// window shows <see cref="SelectColorWindow"/>: given the dialog's logic, it says whether OK was pressed. Null for
        /// none, when the choice says so on the command line.
        /// </summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>The toggle of the status bar of a name, DYN among them; null for none.</summary>
        private StatusToggleViewModel FindToggle(string name)
            => string.Equals(name, DynamicInput.Name, StringComparison.OrdinalIgnoreCase) ? DynamicInput : Toggles.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>DYN switched: the settings in use follow it, and the box is shown or hidden.</summary>
        private void OnDynamicInputChanged(object sender, PropertyChangedEventArgs e)
        {
            _settings.DynamicInput = DynamicInput.IsOn;
            RaiseDynamicInput();
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
                    RaiseDynamicInput();
                    break;
                case nameof(CommandText):
                case nameof(CursorText):
                    RaiseDynamicInput();
                    break;
            }
        }

        private void RaiseDynamicInput()
        {
            Raise(nameof(IsDynamicInputShown));
            Raise(nameof(DynamicInputPrompt));
            Raise(nameof(DynamicInputValue));
            Raise(nameof(IsDynamicInputFields));
            Raise(nameof(IsDynamicInputPolar));
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
        /// field the cursor's, fed to the prompt as a click is. A coordinate typed whole, a keyword, or Enter with nothing
        /// typed or locked is left for the command line, as is a distance alone, which the command line takes along the
        /// rubber band towards the cursor.
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
            _lockedFirst = null;
            _lockedSecond = null;
            _secondActive = false;
            string text = typed.Trim();

            if (!fields || (text.Length > 0 && !IsNumber(text)))
            {
                return false;
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

            if (polar && second == null)
            {
                Execute(first);
                return true;
            }

            // The fields are in the current UCS, the point fed in the world's.
            GeoPoint2 point;
            CadUcs ucs = Ucs;

            if (polar)
            {
                GeoPoint2 from = Session.Prompt.BasePoint.Value;
                double length = first != null ? Number(first) : (_cursorAt - from).Length;
                double angle = Number(second) * Math.PI / 180.0;
                point = from + ucs.ToWorld(new GeoVector2(length * Math.Cos(angle), length * Math.Sin(angle)));
            }
            else
            {
                GeoPoint2 cursor = ucs.ToUcs(_cursorAt);
                point = ucs.ToWorld(new GeoPoint2(first != null ? Number(first) : cursor.X, second != null ? Number(second) : cursor.Y));
            }

            Session.Feed(point);
            SyncHistory();
            RunAfterCommand();
            return true;
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
                return CommandText;
            }

            string locked = second ? _lockedSecond : _lockedFirst;

            if (locked != null)
            {
                return locked;
            }

            GeoPoint2? from = Session.Prompt.BasePoint;

            if (!from.HasValue)
            {
                GeoPoint2 shown = Ucs.ToUcs(_cursorAt);
                return _settings.FormatNumber(second ? shown.Y : shown.X, _settings.CoordinateDecimals);
            }

            GeoVector2 way = Ucs.ToUcs(_cursorAt - from.Value);
            double dx = way.X;
            double dy = way.Y;

            if (!second)
            {
                return _settings.FormatNumber(Math.Sqrt(dx * dx + dy * dy), _settings.CoordinateDecimals);
            }

            double degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            return _settings.FormatNumber(degrees < 0.0 ? degrees + 360.0 : degrees, _settings.AngleDecimals);
        }

        private bool FieldLocked(bool second)
        {
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

            before = typed.Substring(0, at).Trim().TrimStart('@');
            after = typed.Substring(at + 1).Trim();
            return true;
        }

        private static bool IsNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsNaN(value) && !double.IsInfinity(value);

        private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
