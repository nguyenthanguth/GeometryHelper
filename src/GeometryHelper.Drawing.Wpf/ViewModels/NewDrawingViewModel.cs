using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Packing;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>One step of a dialog's header: its number, or a tick once done, and its name.</summary>
    public sealed class StepItem
    {
        internal StepItem(int index, string name, int current)
        {
            Name = name;
            IsDone = index < current;
            IsCurrent = index == current;
            Mark = IsDone ? "✓" : (index + 1).ToString(CultureInfo.InvariantCulture);
            HasLine = index < 2;
        }

        /// <summary>Gets the step's name.</summary>
        public string Name { get; }

        /// <summary>Gets what its badge shows: its number, or a tick once done.</summary>
        public string Mark { get; }

        /// <summary>Gets whether the step is done.</summary>
        public bool IsDone { get; }

        /// <summary>Gets whether the step is the one shown.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets whether a line leads to the next step.</summary>
        public bool HasLine { get; }
    }

    /// <summary>A source as the first step lists it: a card with its name, what it gives, its state and a dot.</summary>
    public sealed class SourceOption : ObservableObject
    {
        private readonly Action<SourceOption> _chosen;
        private bool _isSelected;

        internal SourceOption(IDrawingSourceProvider provider, Action<SourceOption> chosen)
        {
            Provider = provider;
            _chosen = chosen;
        }

        /// <summary>Gets the source.</summary>
        public IDrawingSourceProvider Provider { get; }

        /// <summary>Gets its name.</summary>
        public string Name => Provider.Name;

        /// <summary>Gets what it gives.</summary>
        public string Description => Provider.Description;

        /// <summary>Gets its state in words.</summary>
        public string Availability => Provider.AvailabilityText;

        /// <summary>Gets whether it can give objects now; one that cannot is greyed and cannot be chosen.</summary>
        public bool IsAvailable => Provider.IsAvailable;

        /// <summary>Gets or sets whether it is the one chosen.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value && !IsAvailable)
                {
                    Raise(nameof(IsSelected));
                    return;
                }

                if (Set(ref _isSelected, value) && value)
                {
                    _chosen(this);
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>An object of the source as the second step lists it, ticked to be drawn.</summary>
    public sealed class ObjectRow : ObservableObject
    {
        private readonly Action<ObjectRow> _ticked;
        private bool _isChecked;

        internal ObjectRow(DrawingObject item, Action<ObjectRow> ticked)
        {
            Object = item;
            _ticked = ticked;
        }

        /// <summary>Gets the object.</summary>
        public DrawingObject Object { get; }

        /// <summary>Gets its position mark.</summary>
        public string Mark => Object.Mark;

        /// <summary>Gets its name, "(main part)" after the main part's.</summary>
        public string Name => Object.Name + (Object.IsMain ? " (main part)" : string.Empty);

        /// <summary>Gets its profile.</summary>
        public string Profile => Object.Profile;

        /// <summary>Gets its kind, and the assembly it belongs to: "Part · B1".</summary>
        public string Kind => Object.Kind + (Object.ParentId == null ? string.Empty : " · " + Object.ParentId);

        /// <summary>Gets or sets whether it is drawn.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    _ticked?.Invoke(this);
                }
            }
        }

        /// <summary>Sets the tick without saying the user set it, as a preset's default does.</summary>
        internal void Tick(bool value)
        {
            _isChecked = value;
            Raise(nameof(IsChecked));
        }

        /// <inheritdoc/>
        public override string ToString() => Object.Id + (IsChecked ? " ticked" : string.Empty);
    }

    /// <summary>
    /// The New drawing dialog in three steps, as the design draws it: the source; the objects, ticked; the preset, the sheet,
    /// the number, the name and the revision. Create makes the drawing off the UI thread, with a Stop, and shows it.
    /// </summary>
    /// <remarks>
    /// The objects ticked by default follow the preset: Part ticks one part (the one picked in the list, else the first main
    /// part), Assembly that part's assembly, its main part and its fittings, General every object. Choosing another preset
    /// ticks again, until a tick is changed by hand. The beam B1 of the test bodies with the assembly preset ticks the beam and
    /// its two end plates, and the drawing is numbered B1 and named "Beam B1".
    /// </remarks>
    public sealed class NewDrawingViewModel : DialogViewModel
    {
        /// <summary>The names of the presets, in the order the chips show them.</summary>
        public static readonly IReadOnlyList<string> PresetNames = new[] { "Part", "Assembly", "General" };

        /// <summary>The sheet sizes offered, largest first.</summary>
        public static readonly IReadOnlyList<string> SheetSizeNames = new[] { "A0", "A1", "A2", "A3", "A4" };

        private readonly DrawingWorkspace _workspace;
        private int _step;
        private SourceOption _source;
        private IDrawingSourceProvider _loadedFrom;
        private ObjectRow _focused;
        private string _preset = "Assembly";
        private string _sheetSize;
        private string _number = string.Empty;
        private string _name = string.Empty;
        private string _revision = "A";
        private bool _openAfter = true;
        private bool _ticksByHand;
        private bool _numberByHand;
        private bool _nameByHand;
        private bool _ticking;
        private string _notice = string.Empty;

        /// <summary>Makes the dialog's view model over the model drawing of a presenter.</summary>
        /// <param name="workspace">The model drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public NewDrawingViewModel(DrawingWorkspace workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Sources = workspace.Providers.Select(p => new SourceOption(p, Choose)).ToList();

            // The preset and sheet of Global settings › Drawing defaults.
            _preset = workspace.Model.Settings.DefaultPreset;
            _sheetSize = SheetSizeOf(_preset);
            NextCommand = new DelegateCommand(_ => _ = NextAsync(), _ => CanGoNext);
            BackCommand = new DelegateCommand(_ => Back(), _ => _step > 0 && !IsBusy);
            CancelCommand = new DelegateCommand(_ => Close(false));
            PresetCommand = new DelegateCommand(p => Preset = p as string);
            SourceOption first = Sources.FirstOrDefault(s => s.IsAvailable);

            if (first != null)
            {
                first.IsSelected = true;
            }
        }

        /// <summary>Gets the step shown: 0 Source, 1 Objects, 2 Settings.</summary>
        public int Step => _step;

        /// <summary>Gets the steps of the header.</summary>
        public IReadOnlyList<StepItem> Steps =>
            new[] { "Source", "Objects", "Settings" }.Select((n, i) => new StepItem(i, n, _step)).ToList();

        /// <summary>Gets whether the source step is shown.</summary>
        public bool IsSourceStep => _step == 0;

        /// <summary>Gets whether the objects step is shown.</summary>
        public bool IsObjectsStep => _step == 1;

        /// <summary>Gets whether the settings step is shown.</summary>
        public bool IsSettingsStep => _step == 2;

        /// <summary>Gets whether Back shows.</summary>
        public bool HasBack => _step > 0;

        /// <summary>Gets the label of the main button: Next, then Create.</summary>
        public string NextLabel => _step == 2 ? "Create" : "Next";

        /// <summary>Gets the sources, those not connected greyed.</summary>
        public IReadOnlyList<SourceOption> Sources { get; }

        /// <summary>Gets the source chosen.</summary>
        public SourceOption SelectedSource => _source;

        /// <summary>Gets the objects of the source, in the order of their ids, once the objects step has read them.</summary>
        public ObservableCollection<ObjectRow> Objects { get; } = new ObservableCollection<ObjectRow>();

        /// <summary>Gets or sets the object picked in the list, whose part or assembly the Part and Assembly presets tick.</summary>
        public ObjectRow FocusedObject
        {
            get => _focused;
            set
            {
                if (Set(ref _focused, value) && !_ticksByHand && value != null)
                {
                    ApplyPresetDefault();
                }
            }
        }

        /// <summary>Gets the line over the objects: "Source: Test bodies · Built in · 3 assemblies, 16 objects. Tick the objects to draw."</summary>
        public string SourceLine =>
            _source == null
                ? string.Empty
                : "Source: "
                    + _source.Name
                    + " · "
                    + _source.Availability
                    + ". Tick the objects to draw; the preset ticks its default.";

        /// <summary>Gets what is ticked, in words: "Picked: 1 assembly · 3 parts".</summary>
        public string PickedText
        {
            get
            {
                List<DrawingObject> ticked = Ticked();
                int assemblies = ticked
                    .Where(o => o.ParentId != null)
                    .Select(o => o.ParentId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                return "Picked: "
                    + (
                        assemblies == 0
                            ? string.Empty
                            : DrawingWorkspace.Count(assemblies, "assembly", "assemblies") + " · "
                    )
                    + DrawingProperties.Objects(ticked);
            }
        }

        /// <summary>Gets the names of the presets.</summary>
        public IReadOnlyList<string> Presets => PresetNames;

        /// <summary>Gets or sets the preset: Part, Assembly or General; the objects ticked and the sheet follow it.</summary>
        public string Preset
        {
            get => _preset;
            set
            {
                if (value == null || !PresetNames.Contains(value) || !Set(ref _preset, value))
                {
                    return;
                }

                SheetSize = SheetSizeOf(value);

                if (!_ticksByHand)
                {
                    ApplyPresetDefault();
                }

                RaiseSummary();
            }
        }

        /// <summary>Gets the sheet sizes offered.</summary>
        public IReadOnlyList<string> SheetSizes => SheetSizeNames;

        /// <summary>Gets or sets the sheet size: the preset's until changed, A3 for Part, A2 for Assembly, A1 for General.</summary>
        public string SheetSize
        {
            get => _sheetSize;
            set
            {
                if (value != null && SheetSizeNames.Contains(value) && Set(ref _sheetSize, value))
                {
                    RaiseSummary();
                }
            }
        }

        /// <summary>Gets how the scale is chosen: the largest that fits.</summary>
        public string ScaleText => "Automatic (largest that fits)";

        /// <summary>Gets or sets the drawing number: the main part's mark until typed.</summary>
        public string Number
        {
            get => _number;
            set
            {
                if (Set(ref _number, value ?? string.Empty))
                {
                    _numberByHand = true;
                    RaiseNext();
                }
            }
        }

        /// <summary>Gets or sets the drawing's name, its title block's title: the main part's name and mark until typed, "Beam B1".</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    _nameByHand = true;
                }
            }
        }

        /// <summary>Gets or sets the revision; A by default.</summary>
        public string Revision
        {
            get => _revision;
            set => Set(ref _revision, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the drawing is shown once made; on by default.</summary>
        public bool OpenAfter
        {
            get => _openAfter;
            set => Set(ref _openAfter, value);
        }

        /// <summary>Gets the summary's source line.</summary>
        public string SummarySource => _source?.Name ?? string.Empty;

        /// <summary>Gets the summary's objects line: "3 parts".</summary>
        public string SummaryObjects => DrawingProperties.Objects(Ticked());

        /// <summary>Gets the summary's views line: the views of the preset and its sections.</summary>
        public string SummaryViews
        {
            get
            {
                DrawingSettings settings = PresetSettings(_preset);
                var names = settings
                    .Views.Kinds.Select(k =>
                        k == ViewKind.Right ? "Right end"
                        : k == ViewKind.Left ? "Left end"
                        : k.ToString()
                    )
                    .ToList();
                names.AddRange(settings.Views.Sections.Select(s => "section " + s.Name));
                return string.Join(", ", names);
            }
        }

        /// <summary>Gets the summary's sheet line: "A2 landscape, scale chosen to fit".</summary>
        public string SummarySheet =>
            _sheetSize
            + " "
            + _workspace.Model.Settings.DefaultOrientation.ToLowerInvariant()
            + ", scale chosen to fit";

        /// <summary>Gets a line under the steps: why Create stopped, or what was not right.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the drawing made; null until Create has made it.</summary>
        public ModelDrawing Result { get; private set; }

        /// <summary>Gets the command of Next and Create.</summary>
        public ICommand NextCommand { get; }

        /// <summary>Gets the command of Back.</summary>
        public ICommand BackCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Gets the command that chooses a preset, given its name.</summary>
        public ICommand PresetCommand { get; }

        private bool CanGoNext =>
            !IsBusy
            && (
                _step == 0 ? _source != null && _source.IsAvailable
                : _step == 1 ? Objects.Any(o => o.IsChecked)
                : Objects.Any(o => o.IsChecked) && _number.Trim().Length > 0
            );

        /// <summary>Goes to the next step, reading the source's objects on the way to the second; on the last, makes the drawing.</summary>
        /// <returns>The drawing made on the last step; null otherwise, or where nothing was made.</returns>
        public async Task<ModelDrawing> NextAsync()
        {
            if (!CanGoNext)
            {
                return null;
            }

            Notice = string.Empty;

            if (_step == 0)
            {
                if (!await LoadAsync())
                {
                    return null;
                }

                GoTo(1);
                return null;
            }

            if (_step == 1)
            {
                GoTo(2);
                return null;
            }

            return await CreateAsync();
        }

        /// <summary>Goes back a step.</summary>
        public void Back()
        {
            if (_step > 0 && !IsBusy)
            {
                Notice = string.Empty;
                GoTo(_step - 1);
            }
        }

        /// <inheritdoc/>
        protected override void OnBusyChanged() => RaiseNext();

        /// <summary>Ticks the preset's default objects: one part, an assembly, or all.</summary>
        internal void ApplyPresetDefault()
        {
            if (Objects.Count == 0)
            {
                return;
            }

            DrawingObject focus = _focused?.Object
                ?? Objects.Select(o => o.Object).FirstOrDefault(o => o.IsMain)
                ?? Objects[0].Object;
            _ticking = true;

            foreach (ObjectRow row in Objects)
            {
                bool tick;

                switch (_preset)
                {
                    case "Part":
                        DrawingObject part =
                            focus.Kind == DrawingObjectKind.Part
                                ? focus
                                : Objects
                                    .Select(o => o.Object)
                                    .First(o => o.Kind == DrawingObjectKind.Part || o == focus);
                        tick = row.Object == part;
                        break;
                    case "General":
                        tick = true;
                        break;
                    default:
                        tick =
                            focus.ParentId == null
                                ? row.Object == focus
                                : string.Equals(row.Object.ParentId, focus.ParentId, StringComparison.Ordinal);
                        break;
                }

                row.Tick(tick);
            }

            _ticking = false;
            OnTicksChanged();
        }

        private async Task<bool> LoadAsync()
        {
            IDrawingSourceProvider provider = _source.Provider;

            if (provider == _loadedFrom && Objects.Count > 0)
            {
                return true;
            }

            IReadOnlyList<DrawingObject> objects;

            try
            {
                objects = await RunAsync("Reading the objects of " + provider.Name + "…", provider.CreateObjects);
            }
            catch (OperationCanceledException)
            {
                Notice = "Stopped: the objects were not read.";
                return false;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _workspace.ShowMessage(
                    MessageViewModel.Failure("Objects not read", provider.Name + " could not give its objects.", ex)
                );
                return false;
            }

            Objects.Clear();

            foreach (DrawingObject item in objects.OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                Objects.Add(new ObjectRow(item, OnTicked));
            }

            _loadedFrom = provider;
            _ticksByHand = false;
            _focused = null;
            Raise(nameof(FocusedObject));
            ApplyPresetDefault();
            return true;
        }

        private async Task<ModelDrawing> CreateAsync()
        {
            IDrawingSourceProvider provider = _source.Provider;
            List<DrawingObject> ticked = Ticked();
            DrawingSettings settings = PresetSettings(_preset);
            settings.Sheet.Size = (PaperSize)Enum.Parse(typeof(PaperSize), _sheetSize);
            settings.Sheet.Orientation =
                _workspace.Model.Settings.DefaultOrientation == "Portrait"
                    ? SheetOrientation.Portrait
                    : SheetOrientation.Landscape;
            settings.ProjectionMethod =
                _workspace.Model.Settings.DefaultProjection == "First angle"
                    ? ProjectionMethod.FirstAngle
                    : ProjectionMethod.ThirdAngle;
            settings.ExportMode =
                _workspace.Model.Settings.ExportLayout == "SheetInModelSpace"
                    ? DrawingExportMode.SheetInModelSpace
                    : DrawingExportMode.LayoutAndViewports;
            settings.Sheet.TitleBlock.DrawingNumber = _number.Trim();
            settings.Sheet.TitleBlock.Title = _name.Trim();
            settings.Sheet.TitleBlock.Revision = _revision.Trim();
            var source = new DrawingSource(ticked);
            Tolerance tolerance = _workspace.Tolerance;
            Drawing drawing;

            try
            {
                drawing = await RunAsync("Making the views, dimensions and sheet of " + _number.Trim() + "…", () =>
                    DrawingBuilder.Build(source, settings, tolerance)
                );
            }
            catch (OperationCanceledException)
            {
                Notice = "Stopped: nothing was made.";
                return null;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _workspace.ShowMessage(
                    MessageViewModel.Failure(
                        "Drawing not made",
                        _number.Trim() + " could not be made from " + DrawingProperties.Objects(ticked) + ".",
                        ex
                    )
                );
                return null;
            }

            // An assembly is drawn whole when every object of it is ticked: a fitting added to it later is drawn on update too.
            var whole = ticked
                .Where(o => o.ParentId != null)
                .Select(o => o.ParentId)
                .Distinct(StringComparer.Ordinal)
                .Where(p =>
                    Objects
                        .Where(r => string.Equals(r.Object.ParentId, p, StringComparison.Ordinal))
                        .All(r => r.IsChecked)
                )
                .ToList();
            ModelDrawing before = _workspace.Current;
            FileRowViewModel shown = _workspace.Model.FileRows.FirstOrDefault(r => r.IsCurrent);
            ModelDrawing entry = _workspace.Open(drawing, provider, _preset, whole);

            if (!_openAfter)
            {
                if (before != null)
                {
                    _workspace.Show(before);
                }
                else if (shown != null)
                {
                    _workspace.Model.Activate(shown);
                }
            }

            Result = entry;
            Close(true);
            return entry;
        }

        private static DrawingSettings PresetSettings(string preset) =>
            preset == "Part" ? DrawingSettings.Part
            : preset == "General" ? DrawingSettings.General
            : DrawingSettings.Assembly;

        /// <summary>The sheet of a preset: Global settings' default sheet for the default preset, the preset's own for the others.</summary>
        private string SheetSizeOf(string preset) =>
            string.Equals(preset, _workspace.Model.Settings.DefaultPreset, StringComparison.Ordinal)
                ? _workspace.Model.Settings.DefaultSheetSize
                : PresetSettings(preset).Sheet.Size.ToString();

        private List<DrawingObject> Ticked() => Objects.Where(o => o.IsChecked).Select(o => o.Object).ToList();

        private void Choose(SourceOption option)
        {
            foreach (SourceOption other in Sources)
            {
                if (other != option && other.IsSelected)
                {
                    other.IsSelected = false;
                }
            }

            _source = option;
            Raise(nameof(SelectedSource));
            Raise(nameof(SourceLine));
            RaiseSummary();
            RaiseNext();
        }

        private void OnTicked(ObjectRow row)
        {
            if (!_ticking)
            {
                _ticksByHand = true;
                OnTicksChanged();
            }
        }

        private void OnTicksChanged()
        {
            DrawingObject main = Ticked().FirstOrDefault(o => o.IsMain) ?? Ticked().FirstOrDefault();

            if (main != null)
            {
                if (!_numberByHand)
                {
                    _number =
                        _preset == "General"
                        && Ticked().Select(o => o.ParentId).Distinct(StringComparer.Ordinal).Count() > 1
                            ? "GA-1"
                        : main.Mark.Length > 0 ? main.Mark
                        : main.Id;
                    Raise(nameof(Number));
                }

                if (!_nameByHand)
                {
                    _name =
                        _number == "GA-1"
                            ? "General arrangement"
                            : TitleCase(main.Name) + (main.Mark.Length > 0 ? " " + main.Mark : string.Empty);
                    Raise(nameof(Name));
                }
            }

            Raise(nameof(PickedText));
            RaiseSummary();
            RaiseNext();
        }

        private static string TitleCase(string name) =>
            name.Length == 0
                ? "Drawing"
                : name.Substring(0, 1).ToUpperInvariant() + name.Substring(1).ToLowerInvariant();

        private void GoTo(int step)
        {
            _step = step;
            Raise(nameof(Step));
            Raise(nameof(Steps));
            Raise(nameof(IsSourceStep));
            Raise(nameof(IsObjectsStep));
            Raise(nameof(IsSettingsStep));
            Raise(nameof(HasBack));
            Raise(nameof(NextLabel));
            RaiseNext();
        }

        private void RaiseSummary()
        {
            Raise(nameof(SummarySource));
            Raise(nameof(SummaryObjects));
            Raise(nameof(SummaryViews));
            Raise(nameof(SummarySheet));
        }

        private void RaiseNext()
        {
            ((DelegateCommand)NextCommand)?.RaiseCanExecuteChanged();
            ((DelegateCommand)BackCommand)?.RaiseCanExecuteChanged();
        }
    }
}
