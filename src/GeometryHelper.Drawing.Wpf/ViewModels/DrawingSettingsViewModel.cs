using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Packing;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>What a row of a settings tab is: a switch, a number, a text, a choice from a list, or chips.</summary>
    public enum SettingRowKind
    {
        /// <summary>A switch with a label and a hint.</summary>
        Toggle,

        /// <summary>A number typed, with its unit.</summary>
        Number,

        /// <summary>A text typed.</summary>
        Text,

        /// <summary>A choice from a list.</summary>
        Choice,

        /// <summary>Chips, each on or off.</summary>
        Chips,

        /// <summary>A value shown, not edited.</summary>
        Info,
    }

    /// <summary>A chip of a settings row, as a scale of the allowed scales: its name and whether it is on.</summary>
    public sealed class SettingChip : ObservableObject
    {
        private readonly Func<bool> _get;
        private readonly Func<bool, bool> _set;

        internal SettingChip(string name, Func<bool> get, Func<bool, bool> set)
        {
            Name = name;
            _get = get;
            _set = set;
        }

        /// <summary>Gets the chip's name.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether it is on; a change the settings refuse, as the last scale off, is not taken.</summary>
        public bool IsOn
        {
            get => _get();
            set
            {
                _set(value);
                Raise(nameof(IsOn));
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsOn ? " on" : " off");
    }

    /// <summary>
    /// A row of a settings tab, read from and written to the settings being edited: a switch, a number in millimetres or
    /// degrees, a text, a choice or chips. A number that does not read, or that the settings refuse, shows why and is not taken.
    /// </summary>
    public sealed class SettingRow : ObservableObject
    {
        private readonly Func<string> _get;
        private readonly Action<string> _set;
        private readonly Action _changed;
        private string _value;
        private string _error = string.Empty;

        internal SettingRow(SettingRowKind kind, string label, string hint, string unit, Func<string> get, Action<string> set, Action changed, IReadOnlyList<string> options = null, IReadOnlyList<SettingChip> chips = null)
        {
            Kind = kind;
            Label = label;
            Hint = hint ?? string.Empty;
            Unit = unit ?? string.Empty;
            _get = get;
            _set = set;
            _changed = changed;
            Options = options;
            Chips = chips ?? Array.Empty<SettingChip>();
            _value = get?.Invoke() ?? string.Empty;
        }

        /// <summary>Gets what the row is.</summary>
        public SettingRowKind Kind { get; }

        /// <summary>Gets the label.</summary>
        public string Label { get; }

        /// <summary>Gets the hint under a switch's label; empty for none.</summary>
        public string Hint { get; }

        /// <summary>Gets whether there is a hint.</summary>
        public bool HasHint => Hint.Length > 0;

        /// <summary>Gets the unit after a number: mm, °; empty for none.</summary>
        public string Unit { get; }

        /// <summary>Gets the values a choice offers; null for another kind.</summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>Gets the chips of a chips row; empty for another kind.</summary>
        public IReadOnlyList<SettingChip> Chips { get; }

        /// <summary>Gets or sets the value as text: "1" or "0" for a switch, the number, the text, the choice.</summary>
        public string Value
        {
            get => _value;
            set
            {
                string typed = value ?? string.Empty;

                if (_set == null || string.Equals(typed, _value, StringComparison.Ordinal))
                {
                    return;
                }

                _value = typed;

                try
                {
                    _set(typed);
                    Error = string.Empty;
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
                {
                    Error = ex is FormatException ? "Not a number." : FirstLine(ex.Message);
                }

                Raise(nameof(Value));
                Raise(nameof(IsOn));
                _changed?.Invoke();
            }
        }

        /// <summary>Gets or sets whether a switch is on.</summary>
        public bool IsOn
        {
            get => _value == "1";
            set => Value = value ? "1" : "0";
        }

        /// <summary>Gets why the value typed is not taken; empty where it is.</summary>
        public string Error
        {
            get => _error;
            private set
            {
                if (Set(ref _error, value))
                {
                    Raise(nameof(HasError));
                }
            }
        }

        /// <summary>Gets whether the value typed is not taken.</summary>
        public bool HasError => _error.Length > 0;

        /// <summary>Gets whether the row is a switch.</summary>
        public bool IsToggle => Kind == SettingRowKind.Toggle;

        /// <summary>Gets whether the row is a number or a text typed.</summary>
        public bool IsTyped => Kind == SettingRowKind.Number || Kind == SettingRowKind.Text;

        /// <summary>Gets whether the row is a choice.</summary>
        public bool IsChoice => Kind == SettingRowKind.Choice;

        /// <summary>Gets whether the row is chips.</summary>
        public bool IsChips => Kind == SettingRowKind.Chips;

        /// <summary>Gets whether the row only shows a value.</summary>
        public bool IsInfo => Kind == SettingRowKind.Info;

        private static string FirstLine(string message)
        {
            int end = message.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? message : message.Substring(0, end);
        }

        /// <inheritdoc/>
        public override string ToString() => Label + " = " + _value;
    }

    /// <summary>A titled group of rows of a settings tab.</summary>
    public sealed class SettingSection
    {
        internal SettingSection(string title, IReadOnlyList<SettingRow> rows)
        {
            Title = title;
            Rows = rows;
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets the rows.</summary>
        public IReadOnlyList<SettingRow> Rows { get; }
    }

    /// <summary>
    /// The Drawing settings dialog: the five tabs of the design (Views, Scales, Dimensions, Marks, Sheet) editing a copy of the
    /// drawing's settings. Apply makes the drawing again with them, off the UI thread, keeping its hand edits and what was
    /// drawn on it by hand; OK applies what is not applied yet and closes; Cancel drops it.
    /// </summary>
    /// <remarks>
    /// Apply goes through the core's update with the settings (<see cref="Drawing.Update(DrawingSource, DrawingSettings)"/>),
    /// prepared off the UI thread: one undo step of the document, which takes the settings back with the rest; a mark moved 40
    /// to the right stays 40 to the right, and an edit of a view the new settings no longer make is dropped and said.
    /// </remarks>
    public sealed class DrawingSettingsViewModel : DialogViewModel
    {
        /// <summary>The names of the tabs, in order.</summary>
        public static readonly IReadOnlyList<string> TabNames = new[] { "Views", "Scales", "Dimensions", "Marks", "Sheet" };

        private static readonly double[] KnownScales = { 1, 2, 5, 10, 15, 20, 25, 50, 100, 200, 500 };
        private static readonly ViewKind[] AxisKinds = { ViewKind.Front, ViewKind.Top, ViewKind.Bottom, ViewKind.Right, ViewKind.Left, ViewKind.Back, ViewKind.Isometric };

        private readonly DrawingWorkspace _workspace;
        private readonly Dictionary<string, IReadOnlyList<SettingSection>> _tabs = new Dictionary<string, IReadOnlyList<SettingSection>>(StringComparer.Ordinal);
        private string _tab = "Views";
        private bool _isDirty;
        private string _notice = string.Empty;

        /// <summary>Makes the dialog's view model over a copy of a model drawing's settings.</summary>
        /// <param name="workspace">The model drawing of the presenter.</param>
        /// <param name="drawing">The drawing whose settings are edited.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public DrawingSettingsViewModel(DrawingWorkspace workspace, ModelDrawing drawing)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            Settings = drawing.Drawing.Settings;
            TabCommand = new DelegateCommand(p => Tab = p as string);
            ApplyCommand = new DelegateCommand(_ => _ = ApplyAsync(), _ => CanApply);
            OkCommand = new DelegateCommand(_ => _ = OkAsync(), _ => !IsBusy && !HasErrors);
            CancelCommand = new DelegateCommand(_ => Close(false));
            Build();
        }

        /// <summary>Gets the drawing whose settings are edited.</summary>
        public ModelDrawing Drawing { get; }

        /// <summary>Gets the copy of the settings the tabs edit.</summary>
        public DrawingSettings Settings { get; }

        /// <summary>Gets the header's text: "B-101 · Beam B1 · preset Assembly".</summary>
        public string Title => Drawing.Number + " · " + Drawing.Name + " · preset " + Drawing.PresetName;

        /// <summary>Gets the names of the tabs.</summary>
        public IReadOnlyList<string> Tabs => TabNames;

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (value != null && _tabs.ContainsKey(value) && Set(ref _tab, value))
                {
                    Raise(nameof(Sections));
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets the sections of the tab shown.</summary>
        public IReadOnlyList<SettingSection> Sections => _tabs[_tab];

        /// <summary>Gets every row of every tab.</summary>
        public IEnumerable<SettingRow> AllRows => _tabs.Values.SelectMany(t => t).SelectMany(s => s.Rows);

        /// <summary>Gets the lines of the preview at the right of the tab shown.</summary>
        public IReadOnlyList<string> Preview
        {
            get
            {
                switch (_tab)
                {
                    case "Views":
                        return new[]
                        {
                            "Views: " + string.Join(", ", Settings.Views.Kinds.Select(ViewName)),
                            Settings.Views.Sections.Count == 0 ? "No sections asked for." : "Sections: " + string.Join(", ", Settings.Views.Sections.Select(s => s.Name + "-" + s.Name)),
                            (Settings.ProjectionMethod == ProjectionMethod.FirstAngle ? "First" : "Third") + "-angle projection: the top view goes " + (Settings.ProjectionMethod == ProjectionMethod.FirstAngle ? "below" : "above") + " the front view.",
                        };
                    case "Scales":
                        return new[]
                        {
                            "Allowed: " + string.Join(", ", Settings.Scales.OrderBy(s => s).Select(s => "1:" + Length(s))),
                            "Now: 1:" + Length(Drawing.Drawing.Scale),
                            "The largest allowed scale at which every view and its dimensions fit the room the sheet gives them.",
                        };
                    case "Dimensions":
                        return new[]
                        {
                            "Rules: " + string.Join(", ", DimensionRules()),
                            "Views dimensioned: " + (Settings.Views.Dimensioned.Count == 0 ? "none" : string.Join(", ", Settings.Views.Dimensioned.Select(ViewName))),
                            "Tiers " + Length(Settings.Dimensions.TierSpacing) + " mm apart on the sheet, text " + Length(Settings.Dimensions.TextHeight) + " mm, rounded to " + Length(Settings.Dimensions.Rounding) + " mm.",
                        };
                    case "Marks":
                        return new[]
                        {
                            "Marked: " + (Settings.Marks.MainParts && Settings.Marks.Fittings ? "main parts and fittings" : Settings.Marks.MainParts ? "main parts" : Settings.Marks.Fittings ? "fittings" : "nothing"),
                            "Text " + Length(Settings.Marks.TextHeight) + " mm high, " + Length(Settings.Marks.Offset) + " mm off its part.",
                            "Marks are placed so that none covers another mark, a dimension or a line where it can be avoided.",
                        };
                    default:
                        return new[]
                        {
                            Settings.Sheet.Size + " " + Settings.Sheet.Orientation.ToString().ToLowerInvariant() + ", frame " + Length(Settings.Sheet.Margin) + " mm in from the edge.",
                            "Title block 180 by 40 in the lower right corner" + (Settings.Sheet.PartList ? ", the part list above it." : "; no part list."),
                            Settings.Sheet.AllowLargerSheet ? "A larger sheet is taken where the views do not fit." : "The sheet stays its size; views that do not fit are said.",
                        };
                }
            }
        }

        /// <summary>Gets whether the settings differ from those the drawing was last made with.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (Set(ref _isDirty, value))
                {
                    RaiseCommands();
                }
            }
        }

        /// <summary>Gets whether a value typed is not taken; Apply and OK wait until it is put right.</summary>
        public bool HasErrors => AllRows.Any(r => r.HasError) || Settings.Scales.Count == 0;

        /// <summary>Gets a line at the foot: what Apply did, or why it did not.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the command that shows a tab, given its name.</summary>
        public ICommand TabCommand { get; }

        /// <summary>Gets the command of Apply.</summary>
        public ICommand ApplyCommand { get; }

        /// <summary>Gets the command of OK.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        private bool CanApply => !IsBusy && _isDirty && !HasErrors;

        /// <summary>Makes the drawing again with the settings, hand edits kept; the dialog stays open.</summary>
        /// <returns>Whether the drawing was made again.</returns>
        public async Task<bool> ApplyAsync()
        {
            if (!CanApply)
            {
                return false;
            }

            try
            {
                UpdateReport result = await _workspace.ApplySettingsAsync(Drawing, Settings, "Changed", (text, work) => RunAsync(text, work));
                IsDirty = false;
                Notice = "Made again: " + DrawingWorkspace.Count(result.EditsKept.Count, "hand edit") + " kept" + (result.EditsDropped.Count == 0 ? "." : ", " + result.EditsDropped.Count.ToString(CultureInfo.InvariantCulture) + " no longer apply (see the command line).");
                Raise(nameof(Title));
                return true;
            }
            catch (OperationCanceledException)
            {
                Notice = "Stopped: the drawing was left as it was.";
                return false;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _workspace.ShowMessage(MessageViewModel.Failure("Settings not applied", Drawing.Number + " was left as it was.", ex));
                return false;
            }
        }

        /// <summary>Applies what is not applied yet and closes; a failure leaves the dialog open.</summary>
        /// <returns>Whether the dialog closed.</returns>
        public async Task<bool> OkAsync()
        {
            if (_isDirty && !await ApplyAsync())
            {
                return false;
            }

            Close(true);
            return true;
        }

        /// <inheritdoc/>
        protected override void OnBusyChanged() => RaiseCommands();

        private void Build()
        {
            DrawingSettings s = Settings;
            ViewSettings v = s.Views;
            var views = AxisKinds.Select(k => Toggle(ViewName(k), ViewHint(k), () => v.Kinds.Contains(k), on => Include(v.Kinds, k, on))).ToList();
            _tabs["Views"] = new[]
            {
                new SettingSection("Views to make", views),
                new SettingSection("Sections and details", new[]
                {
                    Info("Sections asked for", v.Sections.Count == 0 ? "None" : string.Join(", ", v.Sections.Select(x => x.Name + " at " + Length(x.Share * 100) + " %, looking as " + ViewName(x.LookingAs)))),
                    Info("Details asked for", v.Details.Count == 0 ? "None" : string.Join(", ", v.Details.Select(x => x.Name + " of " + ViewName(x.Of) + ", " + Length(x.Enlargement) + " times"))),
                    Info("From cut lines", "Drawn on a view: the next step"),
                    Choice("Projection", new[] { "Third angle", "First angle" }, () => s.ProjectionMethod == ProjectionMethod.FirstAngle ? "First angle" : "Third angle",
                        x => s.ProjectionMethod = x == "First angle" ? ProjectionMethod.FirstAngle : ProjectionMethod.ThirdAngle),
                }),
                new SettingSection("Visible area", new[]
                {
                    Number("Crop margin", "mm", () => v.CropMargin, x => v.CropMargin = x),
                    Number("Section depth", "mm", () => v.SectionDepth, x => v.SectionDepth = x),
                    Number("Smooth angle", "°", () => v.SmoothAngleRad * 180.0 / Math.PI, x => v.SmoothAngleRad = x * Math.PI / 180.0),
                    Toggle("Hidden lines", "drawn dashed on the Hidden layer", () => v.HiddenLines, on => v.HiddenLines = on),
                }),
            };

            var scales = KnownScales.Union(s.Scales).OrderBy(x => x).Select(x => new SettingChip("1:" + Length(x), () => s.Scales.Contains(x), on => SetScale(x, on))).ToList();
            _tabs["Scales"] = new[]
            {
                new SettingSection("Allowed scales", new[]
                {
                    new SettingRow(SettingRowKind.Chips, "Views", null, null, () => string.Empty, null, Changed, chips: scales),
                    Info("Chosen", "The largest allowed that fits, every view at one scale; a detail at its own"),
                }),
            };

            AnnotationDimensions(s);
            _tabs["Marks"] = new[]
            {
                new SettingSection("Content", new[]
                {
                    Toggle("Main parts", "the main part of each assembly", () => s.Marks.MainParts, on => s.Marks.MainParts = on),
                    Toggle("Fittings", "the parts that are not main parts", () => s.Marks.Fittings, on => s.Marks.Fittings = on),
                }),
                new SettingSection("Look and placing", new[]
                {
                    Number("Text height", "mm", () => s.Marks.TextHeight, x => s.Marks.TextHeight = x),
                    Number("Offset from the part", "mm", () => s.Marks.Offset, x => s.Marks.Offset = x),
                }),
            };

            SheetSettings sheet = s.Sheet;
            TitleBlock title = sheet.TitleBlock;
            _tabs["Sheet"] = new[]
            {
                new SettingSection("Sheet", new[]
                {
                    Choice("Size", NewDrawingViewModel.SheetSizeNames, () => sheet.Size.ToString(), x => sheet.Size = (PaperSize)Enum.Parse(typeof(PaperSize), x)),
                    Choice("Orientation", new[] { "Landscape", "Portrait" }, () => sheet.Orientation.ToString(), x => sheet.Orientation = (SheetOrientation)Enum.Parse(typeof(SheetOrientation), x)),
                    Number("Margin", "mm", () => sheet.Margin, x => sheet.Margin = x),
                    Number("View spacing", "mm", () => sheet.ViewSpacing, x => sheet.ViewSpacing = x),
                    Toggle("Grow the sheet if the views do not fit", "up to A0", () => sheet.AllowLargerSheet, on => sheet.AllowLargerSheet = on),
                    Toggle("Part list", "mark, quantity, profile, material, length", () => sheet.PartList, on => sheet.PartList = on),
                }),
                new SettingSection("Title block", new[]
                {
                    Text("Project", () => title.Project, x => title.Project = x),
                    Text("Drawing number", () => title.DrawingNumber, x => title.DrawingNumber = x),
                    Text("Title", () => title.Title, x => title.Title = x),
                    Text("Revision", () => title.Revision, x => title.Revision = x),
                    Text("Company", () => title.Company, x => title.Company = x),
                    Text("Drawn by", () => title.DrawnBy, x => title.DrawnBy = x),
                    Text("Date", () => title.Date, x => title.Date = x),
                }),
            };
        }

        private void AnnotationDimensions(DrawingSettings s)
        {
            Annotation.DimensionSettings d = s.Dimensions;
            ViewKind[] dimensionable = { ViewKind.Front, ViewKind.Top, ViewKind.Bottom, ViewKind.Right, ViewKind.Left, ViewKind.Back, ViewKind.Section };
            var dimensioned = dimensionable.Select(k => new SettingChip(ViewName(k), () => s.Views.Dimensioned.Contains(k), on =>
            {
                Include(s.Views.Dimensioned, k, on);
                Changed();
                return true;
            })).ToList();
            _tabs["Dimensions"] = new[]
            {
                new SettingSection("Rules", new[]
                {
                    Toggle("Overall", "length, width and height of the main part", () => d.Overall, on => d.Overall = on),
                    Toggle("Chain", "end to end through the centres of holes and bolts", () => d.Chain, on => d.Chain = on),
                    Toggle("Cumulative", "all from the main part's first end", () => d.Cumulative, on => d.Cumulative = on),
                    Toggle("Ordinate", "all from the main part's work point", () => d.Ordinate, on => d.Ordinate = on),
                    Toggle("Holes by group", "pitch as n x p and the distance to the ends", () => d.HolesByGroup, on => d.HolesByGroup = on),
                    Toggle("Merge equal dimensions", "equal neighbours as one n x p", () => d.MergeEqual, on => d.MergeEqual = on),
                }),
                new SettingSection("Views dimensioned", new[]
                {
                    new SettingRow(SettingRowKind.Chips, "Views", null, null, () => string.Empty, null, Changed, chips: dimensioned),
                }),
                new SettingSection("Placing and text", new[]
                {
                    Number("Tier spacing", "mm", () => d.TierSpacing, x => d.TierSpacing = x),
                    Number("Text height", "mm", () => d.TextHeight, x => d.TextHeight = x),
                    Number("Rounding", "mm", () => d.Rounding, x => d.Rounding = x),
                }),
            };
        }

        private IEnumerable<string> DimensionRules()
        {
            Annotation.DimensionSettings d = Settings.Dimensions;
            var rules = new List<string>();

            if (d.Overall)
            {
                rules.Add("overall");
            }

            if (d.Chain)
            {
                rules.Add("chain");
            }

            if (d.Cumulative)
            {
                rules.Add("cumulative");
            }

            if (d.Ordinate)
            {
                rules.Add("ordinate");
            }

            if (d.HolesByGroup)
            {
                rules.Add("holes by group");
            }

            return rules.Count == 0 ? new[] { "none" } : (IEnumerable<string>)rules;
        }

        private bool SetScale(double scale, bool on)
        {
            if (on && !Settings.Scales.Contains(scale))
            {
                Settings.Scales.Add(scale);
            }
            else if (!on && Settings.Scales.Count > 1)
            {
                Settings.Scales.Remove(scale);
            }
            else if (!on)
            {
                Notice = "One scale at least is allowed.";
                return false;
            }

            Changed();
            return true;
        }

        private static void Include(IList<ViewKind> kinds, ViewKind kind, bool on)
        {
            if (on && !kinds.Contains(kind))
            {
                kinds.Add(kind);
            }
            else if (!on)
            {
                kinds.Remove(kind);
            }
        }

        private void Changed()
        {
            IsDirty = true;
            Raise(nameof(Preview));
            Raise(nameof(HasErrors));
            RaiseCommands();
        }

        private void RaiseCommands()
        {
            ((DelegateCommand)ApplyCommand)?.RaiseCanExecuteChanged();
            ((DelegateCommand)OkCommand)?.RaiseCanExecuteChanged();
        }

        private SettingRow Toggle(string label, string hint, Func<bool> get, Action<bool> set)
            => new SettingRow(SettingRowKind.Toggle, label, hint, null, () => get() ? "1" : "0", x => set(x == "1"), Changed);

        private SettingRow Number(string label, string unit, Func<double> get, Action<double> set)
            => new SettingRow(SettingRowKind.Number, label, null, unit, () => Plain(get()), x => set(double.Parse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)), Changed);

        private SettingRow Text(string label, Func<string> get, Action<string> set)
            => new SettingRow(SettingRowKind.Text, label, null, null, get, set, Changed);

        private SettingRow Choice(string label, IReadOnlyList<string> options, Func<string> get, Action<string> set)
            => new SettingRow(SettingRowKind.Choice, label, null, null, get, set, Changed, options);

        private static SettingRow Info(string label, string value) => new SettingRow(SettingRowKind.Info, label, null, null, () => value, null, null);

        private static string Plain(double value) => Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

        private static string ViewName(ViewKind kind) => kind == ViewKind.Right ? "Right end" : kind == ViewKind.Left ? "Left end" : kind.ToString();

        private static string ViewHint(ViewKind kind)
        {
            switch (kind)
            {
                case ViewKind.Front:
                    return "the main view, the drawing's X to the right";
                case ViewKind.Top:
                    return "from above";
                case ViewKind.Isometric:
                    return "no dimensions by default";
                default:
                    return string.Empty;
            }
        }
    }
}
