using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The contextual Hatch Creation and Hatch Editor tabs over their one set of values, as AutoCAD 2021 draws them: Boundaries
    /// (Pick Points, Select, Remove, and Recreate and Display Boundary Objects for a hatch made, Retain Boundary Objects),
    /// Pattern (the type's gallery with its preview), Properties (type, colour, background colour, transparency, angle, scale or
    /// spacing, Double, ISO pen width, layer), Origin, Options (Associative, Annotative, Match Properties, Gap tolerance,
    /// separate hatches, island detection, draw order) and Close. The Hatch Creation tab is shown while HATCH runs with
    /// HPDLGMODE 2 and holds the settings of the next hatch (<see cref="CadDocument.HatchSettings"/>, which HPNAME, HPANG and
    /// the rest read), changed at once; the Hatch Editor tab is shown while hatches are selected and each value written is one
    /// undo step named HATCHEDIT on every hatch selected. The boundary buttons run the commands of AutoCAD's own words (HATCH's
    /// Select objects and remove Boundaries, -HATCHEDIT's ADd boundaries, Remove boundaries and recreate Boundary).
    /// </summary>
    public sealed class HatchTabViewModel : ObservableObject
    {
        private static readonly string[] TypeNames = { "Pattern", "User defined", "Solid", "Gradient" };

        private static readonly string[] IslandNames =
        {
            "Normal Island Detection",
            "Outer Island Detection",
            "Ignore Island Detection",
            "No Island Detection",
        };

        private static readonly string[] OrderNames =
        {
            "Do not assign",
            "Send to back",
            "Bring to front",
            "Send behind boundary",
            "Bring in front of boundary",
        };

        private const string UseCurrent = "Use current";

        private readonly MainViewModel _model;
        private readonly string[] _handles;
        private DxfHatch _hatch;
        private CadHatchSettings _read;
        private string _message = string.Empty;
        private ImageSource _preview;
        private string _previewOf;

        // Above nought while the tab tells its controls their values. What a control answers then is not a choice of the user: a
        // box whose list was made again chooses its item again, and that answer ran the edit again, which told the values again,
        // until the stack ended. So a value set while the tab tells is let go.
        private int _telling;

        /// <summary>Makes the tab over the settings of the next hatch, or over hatches selected.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="handles">The handles of the hatches the Hatch Editor tab changes, in ordinal order; null for the Hatch Creation tab.</param>
        public HatchTabViewModel(MainViewModel model, IReadOnlyList<string> handles)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _handles = handles?.ToArray();
            PickPointsCommand = new DelegateCommand(_ => PickPoints());
            SelectCommand = new DelegateCommand(_ => SelectObjects());
            RemoveCommand = new DelegateCommand(_ => RemoveBoundaries());
            RecreateCommand = new DelegateCommand(_ => Run("B"), _ => IsEditor && CanEditBoundaries);
            DisplayBoundaryCommand = new DelegateCommand(_ => DisplayBoundary(), _ => IsEditor && IsAssociative);
            PickColorCommand = new DelegateCommand(_ => PickColor(false));
            PickBackgroundCommand = new DelegateCommand(_ => PickColor(true));
            UseCurrentColorCommand = new DelegateCommand(_ => SetColor(null, false));
            NoBackgroundCommand = new DelegateCommand(_ => SetColor(null, true));
            SetOriginCommand = new DelegateCommand(_ => OriginOption("S"));
            UseCurrentOriginCommand = new DelegateCommand(_ => OriginOption("U"), _ => IsCreation);
            OriginCornerCommand = new DelegateCommand(corner => OriginCorner(corner as string));
            MatchPropertiesCommand = new DelegateCommand(_ => _model.StartCommand("MATCHPROP"));
            CloseCommand = new DelegateCommand(_ => Close());
            SettingsCommand = new DelegateCommand(_ => ShowSettings());
            Refresh();
        }

        /// <summary>Gets whether this is the Hatch Creation tab, over the settings of the next hatch.</summary>
        public bool IsCreation => _handles == null;

        /// <summary>Gets whether this is the Hatch Editor tab, over the hatches selected.</summary>
        public bool IsEditor => _handles != null;

        /// <summary>Gets the handles of the hatches the Hatch Editor tab changes; none for the Hatch Creation tab.</summary>
        public IReadOnlyList<string> Handles => _handles ?? Array.Empty<string>();

        /// <summary>Gets the name of the tab: Hatch Creation or Hatch Editor.</summary>
        public string Title => IsCreation ? "Hatch Creation" : "Hatch Editor";

        /// <summary>Gets the words of the Close button: Close Hatch Creation or Close Hatch Editor.</summary>
        public string CloseText => "Close " + Title;

        /// <summary>Gets why a value typed was refused; empty where none was.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        private CadDocument Document => _model.Document;

        private CadCommandSession Session => _model.Session;

        /// <summary>The settings the tab shows: those of the next hatch, or those read from the first hatch selected.</summary>
        private CadHatchSettings Settings => IsCreation ? Document?.HatchSettings ?? new CadHatchSettings() : _read;

        #region Boundaries

        /// <summary>Gets the command of Pick Points: the point prompt of HATCH (or -HATCHEDIT's ADd boundaries), asking for a point inside an area.</summary>
        public ICommand PickPointsCommand { get; }

        /// <summary>Gets the command of Select: HATCH's Select objects (or -HATCHEDIT's ADd boundaries, Select objects).</summary>
        public ICommand SelectCommand { get; }

        /// <summary>Gets the command of Remove: HATCH's remove Boundaries (or -HATCHEDIT's Remove boundaries).</summary>
        public ICommand RemoveCommand { get; }

        /// <summary>Gets the command of Recreate: -HATCHEDIT's recreate Boundary, asking the type of object and whether to reassociate.</summary>
        public ICommand RecreateCommand { get; }

        /// <summary>Gets the command of Display Boundary Objects: the objects an associative hatch follows selected with it, for their grips.</summary>
        public ICommand DisplayBoundaryCommand { get; }

        /// <summary>Gets whether the boundaries can be changed: the settings of the next hatch always, a hatch selected where it is the only one.</summary>
        public bool CanEditBoundaries => IsCreation || _handles.Length == 1;

        /// <summary>Gets whether the hatch selected follows objects, which Display Boundary Objects selects.</summary>
        public bool IsAssociative => _hatch != null && _hatch.Associative;

        /// <summary>Gets or sets whether HATCH keeps a polyline of each boundary it found (Retain Boundary Objects, HPBOUNDRETAIN).</summary>
        public bool RetainBoundaries
        {
            get => Kept(CadCommands.HatchRetainKey, 0.0) != 0.0;
            set => Keep(CadCommands.HatchRetainKey, value ? 1.0 : 0.0);
        }

        #endregion

        #region Pattern

        /// <summary>Gets the choices of the Pattern gallery: the presenter's patterns, or the nine gradients where the type is Gradient.</summary>
        public IReadOnlyList<string> PatternChoices =>
            IsGradient
                ? CadHatchSettings.GradientNames
                : CadPatternFile
                    .BuiltIn.Where(p => !string.Equals(p.Name, "SOLID", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Name.ToUpperInvariant())
                    .ToList();

        /// <summary>Gets or sets the pattern or gradient chosen; a pattern chosen makes the type Pattern.</summary>
        public string PatternChoice
        {
            get => IsGradient ? Settings.GradientName : Settings.PatternName;
            set
            {
                if (value == null)
                {
                    return;
                }

                Change(s =>
                {
                    if (s.Type == CadHatchType.Gradient)
                    {
                        s.GradientName = value;
                    }
                    else
                    {
                        s.Type = CadHatchType.Pattern;
                        s.PatternName = value;
                        s.PatternFile = null;
                    }
                });
            }
        }

        /// <summary>Gets the preview of the pattern as the presenter draws it, 72 pixels square; null where it could not be drawn.</summary>
        public ImageSource Preview
        {
            get
            {
                CadHatchSettings s = Settings;
                string key = string.Join(
                    "|",
                    s.Type,
                    s.PatternName,
                    s.Angle,
                    s.Scale,
                    s.Spacing,
                    s.Double,
                    s.GradientName,
                    s.GradientColor1,
                    s.GradientColor2,
                    s.GradientCentered
                );

                if (key != _previewOf)
                {
                    _previewOf = key;

                    try
                    {
                        _preview = HatchSettingsViewModel.Render(s.Preview(Document), 72, 72);
                    }
                    catch (Exception e)
                        when (e is InvalidOperationException || e is ArgumentException || e is IOException)
                    {
                        GeometryHelperLog.Debug("HatchTabViewModel: the preview was not drawn (" + e.Message + ").");
                        _preview = null;
                    }
                }

                return _preview;
            }
        }

        #endregion

        #region Properties

        /// <summary>Gets the hatch types: Pattern, User defined, Solid, Gradient.</summary>
        public IReadOnlyList<string> Types => TypeNames;

        /// <summary>Gets or sets the type, by its name.</summary>
        public string Type
        {
            get => TypeNames[(int)Settings.Type];
            set
            {
                int index = Array.IndexOf(TypeNames, value);

                if (index >= 0 && index != (int)Settings.Type)
                {
                    Change(s => s.Type = (CadHatchType)index);
                }
            }
        }

        /// <summary>Gets whether the type is Pattern.</summary>
        public bool IsPattern => Settings.Type == CadHatchType.Pattern;

        /// <summary>Gets whether the type is User defined.</summary>
        public bool IsUserDefined => Settings.Type == CadHatchType.UserDefined;

        /// <summary>Gets whether the type is Gradient.</summary>
        public bool IsGradient => Settings.Type == CadHatchType.Gradient;

        /// <summary>Gets whether the type has a scale: a pattern.</summary>
        public bool HasScale => IsPattern;

        /// <summary>Gets whether the type has an angle: all but a solid fill.</summary>
        public bool HasAngle => Settings.Type != CadHatchType.Solid;

        /// <summary>Gets whether the type has a background colour: a pattern or lines of the user's; a gradient has its second colour.</summary>
        public bool HasBackground => Settings.Type != CadHatchType.Solid;

        /// <summary>Gets whether the pattern is an ISO one, drawn for a pen of 1 mm, whose pen width is its scale.</summary>
        public bool IsIso =>
            IsPattern
            && (Settings.PatternName ?? string.Empty).StartsWith("ACAD_ISO", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets the angle in degrees, as typed.</summary>
        public string Angle
        {
            get => Text(Settings.Angle);
            set => Number(value, "The angle", false, v => Change(s => s.Angle = v));
        }

        /// <summary>Gets the scale of a pattern, as typed; above nought.</summary>
        public string Scale
        {
            get => Text(Settings.Scale);
            set => Number(value, "The scale", true, v => Change(s => s.Scale = v));
        }

        /// <summary>Gets the spacing of lines of the user's, as typed; above nought.</summary>
        public string Spacing
        {
            get => Text(Settings.Spacing);
            set => Number(value, "The spacing", true, v => Change(s => s.Spacing = v));
        }

        /// <summary>Gets or sets whether lines of the user's are crossed by as many square to them (Double).</summary>
        public bool Double
        {
            get => Settings.Double;
            set => Change(s => s.Double = value);
        }

        /// <summary>Gets the ISO pen widths offered.</summary>
        public IReadOnlyList<string> IsoPenWidths => LinetypeManagerViewModel.IsoPenWidths;

        /// <summary>Gets or sets the ISO pen width: chosen, it sets the scale to the width in millimetres.</summary>
        public string IsoPenWidth
        {
            get
            {
                string scale = Text(Settings.Scale);
                return IsoPenWidths.FirstOrDefault(w => Text(Width(w)) == scale);
            }
            set
            {
                if (value != null)
                {
                    Change(s => s.Scale = Width(value));
                }
            }
        }

        /// <summary>Gets the colour of the hatch as "Use current", ByLayer, a name or red, green and blue (the first colour of a gradient).</summary>
        public string ColorText
        {
            get
            {
                DxfColor color = Color(false);
                return color == null ? UseCurrent : SelectColorViewModel.Text(color);
            }
        }

        /// <summary>Gets the colour of the hatch as 0xRRGGBB, for its swatch; white for the current colour.</summary>
        public int ColorRgb => Rgb(Color(false));

        /// <summary>Gets the background colour as "None", a name or red, green and blue (the second colour of a gradient).</summary>
        public string BackgroundText
        {
            get
            {
                DxfColor color = Color(true);
                return color == null ? "None" : SelectColorViewModel.Text(color);
            }
        }

        /// <summary>Gets the background colour as 0xRRGGBB, for its swatch; the dark of the ribbon for none.</summary>
        public int BackgroundRgb => Color(true) == null ? 0x262B33 : Rgb(Color(true));

        /// <summary>Gets the command of the colour's Select Color dialog.</summary>
        public ICommand PickColorCommand { get; }

        /// <summary>Gets the command of the background colour's Select Color dialog.</summary>
        public ICommand PickBackgroundCommand { get; }

        /// <summary>Gets the command of Use Current: the drawing's current colour.</summary>
        public ICommand UseCurrentColorCommand { get; }

        /// <summary>Gets the command of None: no background colour.</summary>
        public ICommand NoBackgroundCommand { get; }

        /// <summary>
        /// Gets or sets the transparency as the Transparency box shows it: "Use current" (only for the next hatch), ByLayer, ByBlock
        /// or a percent from 0 to 90.
        /// </summary>
        public string Transparency
        {
            get
            {
                DxfTransparency? value = IsCreation ? Settings.Transparency : _hatch?.Transparency;
                return !value.HasValue ? UseCurrent
                    : value.Value.IsByLayer ? "ByLayer"
                    : value.Value.IsByBlock ? "ByBlock"
                    : ((int)Math.Round((255 - value.Value.Alpha) * 100.0 / 255.0)).ToString(
                        CultureInfo.InvariantCulture
                    );
            }
            set
            {
                string text = (value ?? string.Empty).Trim();
                DxfTransparency? chosen;

                if (string.Equals(text, UseCurrent, StringComparison.OrdinalIgnoreCase))
                {
                    chosen = null;
                }
                else if (string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = DxfTransparency.ByLayer;
                }
                else if (string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = DxfTransparency.ByBlock;
                }
                else if (
                    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
                    && percent >= 0
                    && percent <= 90
                )
                {
                    chosen = new DxfTransparency((byte)Math.Floor(255.0 * (100 - percent) / 100.0));
                }
                else
                {
                    Message = "The transparency is 0 to 90, ByLayer or ByBlock.";
                    return;
                }

                Message = string.Empty;

                if (IsCreation)
                {
                    Change(s => s.Transparency = chosen);
                }
                else if (chosen.HasValue)
                {
                    Edit(h => h.Transparency = chosen.Value);
                }
            }
        }

        /// <summary>Gets the transparencies the box offers besides a percent: Use current, ByLayer, ByBlock.</summary>
        public IReadOnlyList<string> TransparencyChoices
        {
            get
            {
                var choices = new List<string>();

                if (IsCreation)
                {
                    choices.Add(UseCurrent);
                }

                choices.Add("ByLayer");
                choices.Add("ByBlock");

                for (int percent = 0; percent <= 90; percent += 10)
                {
                    choices.Add(percent.ToString(CultureInfo.InvariantCulture));
                }

                // A transparency of the hatch that is none of these, as 25, is offered too, so that the box shows it.
                string now = Transparency;

                if (!choices.Contains(now))
                {
                    choices.Add(now);
                }

                return choices;
            }
        }

        /// <summary>Gets the layers a hatch can be put on: the current layer for the next hatch ("Use current"), then those of the drawing.</summary>
        public IReadOnlyList<string> LayerChoices
        {
            get
            {
                var names = new List<string>();

                if (IsCreation)
                {
                    names.Add(UseCurrent);
                }

                names.AddRange(_model.Layers.Select(l => l.Name));
                return names;
            }
        }

        /// <summary>Gets or sets the layer of the hatch, or "Use current" for the next one.</summary>
        public string Layer
        {
            get => IsCreation ? Settings.Layer ?? UseCurrent : _hatch?.Layer;
            set
            {
                if (value == null)
                {
                    return;
                }

                if (IsCreation)
                {
                    Change(s => s.Layer = value == UseCurrent ? null : value);
                }
                else
                {
                    Edit(h => h.Layer = value);
                }
            }
        }

        #endregion

        #region Origin

        /// <summary>Gets the command of Set Origin: the point asked on the canvas, the pattern laid from it.</summary>
        public ICommand SetOriginCommand { get; }

        /// <summary>Gets the command of Use current origin: the origin the settings hold (the Hatch Creation tab only).</summary>
        public ICommand UseCurrentOriginCommand { get; }

        /// <summary>Gets the command of the corner buttons: the corner or the middle of the box round the hatch, by the letter of its name (L, R, I, E, C).</summary>
        public ICommand OriginCornerCommand { get; }

        /// <summary>Gets or sets whether Set Origin stores the point as the default origin (HPORIGIN) rather than for this hatch alone.</summary>
        public bool StoreAsDefaultOrigin
        {
            get => Kept(CadCommands.HatchOriginStoreKey, 0.0) != 0.0;
            set => Keep(CadCommands.HatchOriginStoreKey, value ? 1.0 : 0.0);
        }

        /// <summary>Gets the origin the pattern is laid from, as "x, y".</summary>
        public string OriginText
        {
            get
            {
                if (IsCreation)
                {
                    return Settings.OriginMode == CadHatchOriginMode.Current
                        ? Text(Settings.OriginX) + ", " + Text(Settings.OriginY)
                        : new[] { "Current", "Bottom left", "Bottom right", "Top right", "Top left", "Center" }[
                            (int)Settings.OriginMode
                        ];
                }

                return Text(Settings.OriginX) + ", " + Text(Settings.OriginY);
            }
        }

        #endregion

        #region Options

        /// <summary>Gets or sets whether the hatch follows its boundary objects; turned off on a hatch it lets go of them, turned on it asks for the objects (-HATCHEDIT's ASsociate).</summary>
        public bool Associative
        {
            get => IsCreation ? Settings.Associative : IsAssociative;
            set
            {
                if (IsCreation)
                {
                    Change(s => s.Associative = value);
                }
                else if (!value)
                {
                    Edit(h => h.Associative = false);
                }
                else if (!IsAssociative)
                {
                    Run("AS");
                }
            }
        }

        /// <summary>Gets or sets whether the hatch is annotative, its pattern drawn at the scale of the view.</summary>
        public bool Annotative
        {
            get => Settings.Annotative;
            set => Change(s => s.Annotative = value);
        }

        /// <summary>Gets the command of Match Properties: MATCHPROP, the source hatch and then those that take its properties.</summary>
        public ICommand MatchPropertiesCommand { get; }

        /// <summary>Gets the gap tolerance in drawing units, 0 to 5000, as typed (HPGAPTOL).</summary>
        public string GapTolerance
        {
            get => Text(Document?.HatchSettings.GapTolerance ?? 0.0);
            set => Number(value, "The gap tolerance", false, v =>
                {
                    if (v < 0.0 || v > 5000.0)
                    {
                        Message = "The gap tolerance is 0 to 5000.";
                        return;
                    }

                    Document.HatchSettings.GapTolerance = v;
                    Raise(nameof(GapTolerance));
                });
        }

        /// <summary>Gets or sets whether HATCH makes a hatch of each area picked rather than one of them all.</summary>
        public bool SeparateHatches
        {
            get => Kept(CadCommands.HatchSeparateKey, 0.0) != 0.0;
            set => Keep(CadCommands.HatchSeparateKey, value ? 1.0 : 0.0);
        }

        /// <summary>Gets the island detections offered: Normal, Outer, Ignore and No Island Detection.</summary>
        public IReadOnlyList<string> IslandChoices => IslandNames;

        /// <summary>Gets or sets the island detection: the island style of the hatch, or none, which keeps the outer loop of each area alone.</summary>
        public string IslandDetection
        {
            get =>
                Kept(CadCommands.HatchIslandsKey, 1.0) == 0.0 ? IslandNames[3] : IslandNames[(int)Settings.IslandStyle];
            set
            {
                int index = Array.IndexOf(IslandNames, value);

                if (index < 0)
                {
                    return;
                }

                Keep(CadCommands.HatchIslandsKey, index == 3 ? 0.0 : 1.0);

                if (index < 3)
                {
                    Change(s => s.IslandStyle = (DxfHatchStyle)index);
                }
            }
        }

        /// <summary>Gets the draw orders offered, in the order -HATCH offers them.</summary>
        public IReadOnlyList<string> OrderChoices => OrderNames;

        /// <summary>Gets or sets the draw order the hatch is made in, put at once on the hatches of the Hatch Editor tab.</summary>
        public string DrawOrder
        {
            get => OrderNames[(int)Kept(CadCommands.HatchOrderKey, 3.0)];
            set
            {
                int index = Array.IndexOf(OrderNames, value);

                if (index < 0)
                {
                    return;
                }

                Keep(CadCommands.HatchOrderKey, index);

                if (IsEditor && index > 0)
                {
                    Edit(h => CadHatchEditor.Place(Document, h, index));
                }
            }
        }

        /// <summary>Gets the command of the dialog launcher of Options: the Hatch dialog (HATCH's seTtings, or HATCHEDIT).</summary>
        public ICommand SettingsCommand { get; }

        #endregion

        /// <summary>Gets the command of Close: ends HATCH, making the hatch of the areas picked, or lets go of the hatches selected.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Reads the values again from the drawing, after an edit, an undo or a command, and tells the tab to show them.</summary>
        internal void Refresh()
        {
            if (IsEditor)
            {
                _hatch = Document?.FindEntity(_handles[0]) as DxfHatch;
                _read = _hatch == null ? new CadHatchSettings() : CadCommands.HatchSettingsOf(_hatch);
            }

            RaiseAll();
            (RecreateCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (DisplayBoundaryCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Whether the hatches this tab changes are still hatches of the drawing shown.</summary>
        internal bool IsValid() => IsCreation || _hatch != null;

        private double Kept(string key, double start) =>
            Session != null && Session.Remembered.TryGetValue(key, out double value) ? value : start;

        private void Keep(string key, double value)
        {
            if (_telling > 0)
            {
                return;
            }

            if (Session != null)
            {
                Session.Remembered[key] = value;
            }

            RaiseAll();
        }

        /// <summary>Tells the controls that every value may have changed; what they answer to it is let go (see <see cref="_telling"/>).</summary>
        private void RaiseAll()
        {
            _telling++;

            try
            {
                Raise(string.Empty);
            }
            finally
            {
                _telling--;
            }
        }

        /// <summary>Changes the settings: those of the next hatch in place, or the hatches selected as one undo step.</summary>
        private void Change(Action<CadHatchSettings> edit)
        {
            CadDocument document = Document;

            if (document == null || _telling > 0)
            {
                return;
            }

            if (IsCreation)
            {
                edit(document.HatchSettings);
            }
            else
            {
                CadHatchSettings changed = _read.Clone();
                edit(changed);
                CadEditResult result = CadHatchEditor.ApplySettings(document, _handles, changed, out string fault);
                Report(result, fault);
            }

            Message = string.Empty;
            Refresh();
        }

        /// <summary>Changes the hatches selected as one undo step: what is not in the settings, as the layer and the transparency.</summary>
        private void Edit(Action<DxfHatch> change)
        {
            if (Document == null || IsCreation || _telling > 0)
            {
                return;
            }

            CadEditResult result = CadHatchEditor.Edit(Document, _handles, (s, h) => change(h));
            Report(result, null);
            Refresh();
        }

        private void Report(CadEditResult result, string fault)
        {
            if (!result.Succeeded)
            {
                Session?.Write(result.Message);
            }
            else if (fault != null)
            {
                Session?.Write(fault);
            }
        }

        private void Number(string typed, string what, bool positive, Action<double> take)
        {
            if (
                !double.TryParse(
                    (typed ?? string.Empty).Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                )
                || double.IsNaN(number)
                || double.IsInfinity(number)
                || (positive && !(number > 0.0))
            )
            {
                Message = what + (positive ? " is a number above nought." : " is a number.");
                RaiseAll();
                return;
            }

            Message = string.Empty;
            take(number);
        }

        private DxfColor Color(bool background)
        {
            CadHatchSettings s = Settings;

            if (IsGradient)
            {
                return background ? s.GradientColor2 : s.GradientColor1;
            }

            if (IsCreation)
            {
                return background ? s.BackgroundColor : s.Color;
            }

            if (_hatch == null)
            {
                return null;
            }

            if (!background)
            {
                return _hatch.Color;
            }

            DxfColor behind = _hatch.BackgroundColor;
            return behind.IsNone ? null : behind;
        }

        private static int Rgb(DxfColor color) =>
            color == null || color.IsByLayer || color.IsByBlock || color.IsNone
                ? 0xFFFFFF
                : color.ColorValue & 0xFFFFFF;

        private void PickColor(bool background)
        {
            DxfColor now = Color(background);
            var dialog = new SelectColorViewModel(now ?? DxfColor.ByLayer, !IsGradient);

            if (_model.PickColor == null)
            {
                Session?.Write("The Select Color dialog is shown by the window.");
            }
            else if (_model.PickColor(dialog))
            {
                SetColor(dialog.Color, background);
            }
        }

        /// <summary>Gives the hatch or the next hatch a colour, or the current one and no background for null; a gradient's two colours are its first and second.</summary>
        private void SetColor(DxfColor color, bool background)
        {
            if (IsGradient)
            {
                if (color != null)
                {
                    Change(s =>
                    {
                        if (background)
                        {
                            s.GradientColor2 = color;
                        }
                        else
                        {
                            s.GradientColor1 = color;
                        }
                    });
                }

                return;
            }

            if (IsCreation)
            {
                Change(s =>
                {
                    if (background)
                    {
                        s.BackgroundColor = color;
                    }
                    else
                    {
                        s.Color = color;
                    }
                });
                return;
            }

            DxfColor current = _hatch == null ? null : CurrentColor();
            Edit(h =>
            {
                if (background)
                {
                    h.BackgroundColor = color ?? DxfColor.FromColorIndex(DxfColorMethod.None, 0);
                }
                else
                {
                    h.Color = color ?? current ?? DxfColor.ByLayer;
                }
            });
        }

        /// <summary>The drawing's current colour, as a new object takes it.</summary>
        private DxfColor CurrentColor()
        {
            var probe = new DxfHatch();
            probe.SetDatabaseDefaults(Document.Database);
            return probe.Color;
        }

        #region What the buttons run

        private void PickPoints()
        {
            if (IsCreation)
            {
                // HATCH asks for a point already; a prompt of one of its options is left by Esc first, which keeps what was picked.
                return;
            }

            Run("AD");
        }

        private void SelectObjects()
        {
            if (IsCreation)
            {
                _model.Execute("S");
            }
            else
            {
                Run("AD", "S");
            }
        }

        private void RemoveBoundaries()
        {
            if (IsCreation)
            {
                _model.Execute("B");
            }
            else
            {
                Run("R");
            }
        }

        /// <summary>Runs -HATCHEDIT on the hatches selected and gives it its answers, one after the other, while it asks.</summary>
        private void Run(params string[] answers) => _model.RunHatchEdit(_handles, answers);

        private void OriginOption(string how)
        {
            if (IsCreation)
            {
                _model.Execute("O");
                _model.Execute(how);
            }
            else
            {
                Run("O", how == "U" ? "U" : "Set");
            }
        }

        private void OriginCorner(string corner)
        {
            if (string.IsNullOrEmpty(corner))
            {
                return;
            }

            if (IsCreation)
            {
                _model.Execute("O");
                _model.Execute("Default");
                _model.Execute(corner);
                _model.Execute(StoreAsDefaultOrigin ? "Y" : "N");
            }
            else
            {
                Run("O", "Default", corner);
            }
        }

        private void DisplayBoundary()
        {
            if (_hatch == null || Document == null)
            {
                return;
            }

            var shown = new List<string>(_handles);
            shown.AddRange(_hatch.GetAssociatedObjects().Select(e => e.Handle));
            Document.Selection.Set(shown.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private void Close()
        {
            if (IsCreation)
            {
                // Enter at the point prompt makes the hatch of the areas picked; Esc at another prompt ends HATCH first.
                _model.Execute(string.Empty);
            }
            else
            {
                Document?.Selection.Clear();
            }
        }

        private void ShowSettings()
        {
            if (IsCreation)
            {
                _model.Execute("T");
            }
            else
            {
                _model.RunHatchEdit(_handles, null);
            }
        }

        #endregion

        private static string Text(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static double Width(string penWidth) =>
            double.Parse(penWidth.Replace("mm", string.Empty).Trim(), CultureInfo.InvariantCulture);
    }
}
