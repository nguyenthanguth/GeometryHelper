using System;
using System.ComponentModel;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Dynamic input (DYN, F12), the command line shown or hidden (Ctrl+9), and AutoCAD's Select Color dialog for the Color
    /// row of Properties.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The choice of the Color row of Properties that shows AutoCAD's Select Color dialog.</summary>
        public const string SelectColorOption = "Select Color...";

        private bool _commandLineShown = true;
        private GeoPoint2 _cursorAt;

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

        /// <summary>Gets whether the box of dynamic input is shown: DYN on and a command running; with none, nothing is shown.</summary>
        public bool IsDynamicInputShown => DynamicInput.IsOn && IsCommandRunning && Prompt.Length > 0;

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

                if (!prompt.BasePoint.HasValue)
                {
                    return _settings.FormatNumber(_cursorAt.X, _settings.CoordinateDecimals) + ", " + _settings.FormatNumber(_cursorAt.Y, _settings.CoordinateDecimals);
                }

                double dx = _cursorAt.X - prompt.BasePoint.Value.X;
                double dy = _cursorAt.Y - prompt.BasePoint.Value.Y;
                double degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
                return _settings.FormatNumber(Math.Sqrt(dx * dx + dy * dy), _settings.CoordinateDecimals) + " < " + _settings.FormatNumber(degrees, _settings.AngleDecimals) + "°";
            }
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

        /// <summary>What the box of dynamic input shows follows the prompt, the cursor and what is typed.</summary>
        private void OnInputSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Prompt):
                case nameof(IsCommandRunning):
                case nameof(CommandText):
                case nameof(CursorText):
                case nameof(HasDocument):
                    RaiseDynamicInput();
                    break;
            }
        }

        private void RaiseDynamicInput()
        {
            Raise(nameof(IsDynamicInputShown));
            Raise(nameof(DynamicInputPrompt));
            Raise(nameof(DynamicInputValue));
        }
    }
}
