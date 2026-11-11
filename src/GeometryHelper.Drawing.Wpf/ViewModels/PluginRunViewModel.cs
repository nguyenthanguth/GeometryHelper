using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>An object of the view as the Run plugin dialog lists it, ticked to be given to the plugin.</summary>
    public sealed class PluginObjectRow : ObservableObject
    {
        private readonly Action _ticked;
        private bool _isChecked = true;

        internal PluginObjectRow(DrawingObject item, Action ticked)
        {
            Id = item.Id;
            Text =
                item.Kind
                + " "
                + item.Mark
                + (string.IsNullOrEmpty(item.Profile) ? string.Empty : " · " + item.Profile);
            _ticked = ticked;
        }

        /// <summary>Gets the object's id in the model, C1-bolt-1.</summary>
        public string Id { get; }

        /// <summary>Gets its kind, mark and profile: "Bolt M20 · M20*80".</summary>
        public string Text { get; }

        /// <summary>Gets or sets whether it is given to the plugin; all are, at first.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    _ticked();
                }
            }
        }
    }

    /// <summary>A setting a drawing plugin declares, as the Run plugin dialog shows it: the value typed, its default and range, and why a value is refused.</summary>
    public sealed class PluginValueRow : ObservableObject
    {
        private readonly DrawingPluginSetting _setting;
        private readonly Action _changed;
        private string _text;
        private string _error = string.Empty;

        internal PluginValueRow(DrawingPluginSetting setting, Action changed)
        {
            _setting = setting;
            _changed = changed;
            _text = DrawingWorkspace.Exact(setting.Default);
            Value = setting.Default;
        }

        /// <summary>Gets the setting's name: Rounding.</summary>
        public string Name => _setting.Name;

        /// <summary>Gets its default and range in words: "1 by default, from 0.01 to 100".</summary>
        public string RangeText =>
            DrawingWorkspace.Exact(_setting.Default) + " by default" + DrawingWorkspace.RangeOf(_setting);

        /// <summary>Gets or sets the value as typed; one that is not a number in the range shows why, and Run waits for a good one.</summary>
        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;

                if (
                    double.TryParse(_text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    && number >= _setting.Minimum
                    && number <= _setting.Maximum
                )
                {
                    Value = number;
                    Error = string.Empty;
                }
                else
                {
                    string range = DrawingWorkspace.RangeOf(_setting);
                    Error =
                        Name
                        + (
                            range.Length == 0
                                ? " has to be a number."
                                : " has to be a number" + range.Substring(1) + "."
                        );
                }

                Raise(nameof(Text));
                _changed();
            }
        }

        /// <summary>Gets why the value typed is refused; empty where it is taken.</summary>
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

        /// <summary>Gets whether the value typed is refused.</summary>
        public bool HasError => _error.Length > 0;

        /// <summary>The value last taken.</summary>
        internal double Value { get; private set; }

        /// <inheritdoc/>
        public override string ToString() => Name + " = " + _text;
    }

    /// <summary>
    /// The Run plugin dialog: a drawing plugin loaded, the view of the drawing shown it runs in, the objects of the view of
    /// the kinds it asks for, each ticked, and the settings it declares, each with its default and range; then Run, one undo
    /// step of the drawing, which keeps the run and runs it again on every update.
    /// </summary>
    /// <remarks>
    /// The sample Bolt dimensions on the column C1 lists its four bolts in the front view, all ticked, and Rounding 1. With all
    /// of them ticked the plugin is given every bolt of the view, those an update adds too; with some unticked, the ticked
    /// ones only. A value of Rounding outside 0.01 to 100 is refused in words and Run waits; a plugin that fails is said under
    /// the dialog, which stays open, and the drawing is left as it was.
    /// </remarks>
    public sealed class PluginRunViewModel : DialogViewModel
    {
        private readonly DrawingWorkspace _workspace;
        private readonly ModelDrawing _drawing;
        private DrawingPlugin _plugin;
        private DrawingPluginInput _input;
        private DrawingView _view;
        private IReadOnlyList<PluginObjectRow> _objects = Array.Empty<PluginObjectRow>();
        private IReadOnlyList<PluginValueRow> _settings = Array.Empty<PluginValueRow>();
        private string _notice = string.Empty;

        /// <summary>Makes the dialog's view model over the drawing shown: the first plugin, the view selected or else the first.</summary>
        /// <param name="workspace">The model drawing of the presenter.</param>
        /// <param name="drawing">The drawing the plugin runs on.</param>
        /// <exception cref="ArgumentNullException">Thrown when the workspace or the drawing is null.</exception>
        public PluginRunViewModel(DrawingWorkspace workspace, ModelDrawing drawing)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            Plugins = workspace.LoadedPlugins;
            Views = drawing.Drawing.Views;
            string selected = workspace.Current == drawing ? workspace.SelectedViewId() : null;
            _view = Views.FirstOrDefault(v => v.Id == selected) ?? Views.FirstOrDefault();
            RunCommand = new DelegateCommand(_ => Run(), _ => CanRun);
            CancelCommand = new DelegateCommand(_ => Close(false));
            Plugin = Plugins.FirstOrDefault();

            if (Views.Count == 0)
            {
                Notice = drawing.Number + " has no views yet: make it again from its model first, with Update.";
            }
        }

        /// <summary>Gets the dialog's title: "Run plugin on C-1".</summary>
        public string Title => "Run plugin on " + _drawing.Number;

        /// <summary>Gets the drawing plugins loaded, in the catalog's order.</summary>
        public IReadOnlyList<DrawingPlugin> Plugins { get; }

        /// <summary>Gets or sets the plugin run; set, its objects and settings are read again.</summary>
        public DrawingPlugin Plugin
        {
            get => _plugin;
            set
            {
                if (Set(ref _plugin, value))
                {
                    string failure = null;
                    _input = value == null ? null : DrawingWorkspace.InputOf(value, out failure);
                    Notice = failure;
                    _settings = (
                        _input?.Settings ?? (IReadOnlyList<DrawingPluginSetting>)Array.Empty<DrawingPluginSetting>()
                    )
                        .Select(s => new PluginValueRow(s, Changed))
                        .ToList();
                    Raise(nameof(Settings));
                    Raise(nameof(HasSettings));
                    Raise(nameof(PluginText));
                    ReadObjects();
                }
            }
        }

        /// <summary>Gets the plugin's version and what it asks for in the view: "Version 1.0.0.0 · asks for a view and its bolts".</summary>
        public string PluginText =>
            _plugin == null
                ? string.Empty
                : "Version "
                    + _plugin.Version
                    + (
                        _input == null
                            ? string.Empty
                            : " · " + (_input.ObjectsPrompt ?? _input.ViewPrompt ?? "runs in a view")
                    );

        /// <summary>Gets the views of the drawing.</summary>
        public IReadOnlyList<DrawingView> Views { get; }

        /// <summary>Gets or sets the view the plugin runs in; set, its objects are listed again.</summary>
        public DrawingView View
        {
            get => _view;
            set
            {
                if (Set(ref _view, value))
                {
                    ReadObjects();
                }
            }
        }

        /// <summary>Gets the title of the objects: "Bolts of the view", or "Objects of the view" for a plugin that asks for any kind.</summary>
        public string ObjectsTitle =>
            (
                _input == null || _input.ObjectKinds.Count == 0
                    ? "Objects"
                    : string.Join(", ", _input.ObjectKinds.Select(k => k + "s"))
            ) + " of the view";

        /// <summary>Gets the objects of the view of the kinds the plugin asks for, in the view's order, each ticked at first.</summary>
        public IReadOnlyList<PluginObjectRow> Objects => _objects;

        /// <summary>Gets what is given to the plugin in words: "4 of 4 ticked: every one of the view, those an update adds too".</summary>
        public string ObjectsText
        {
            get
            {
                int ticked = _objects.Count(o => o.IsChecked);
                string count =
                    ticked.ToString(CultureInfo.InvariantCulture)
                    + " of "
                    + _objects.Count.ToString(CultureInfo.InvariantCulture)
                    + " ticked";
                return _objects.Count == 0 ? "None in this view: the plugin is given none."
                    : ticked == _objects.Count ? count + ": every one of the view, those an update adds too."
                    : ticked == 0 ? count + ": tick one at least."
                    : count + ": those only.";
            }
        }

        /// <summary>Gets the settings the plugin declares, in its order.</summary>
        public IReadOnlyList<PluginValueRow> Settings => _settings;

        /// <summary>Gets whether the plugin declares settings.</summary>
        public bool HasSettings => _settings.Count > 0;

        /// <summary>Gets why the plugin cannot run or did not run; empty for nothing.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets whether Run can be pressed: a plugin and a view chosen, an object ticked where the view has any, and every setting taken.</summary>
        public bool CanRun =>
            _plugin != null
            && _input != null
            && _view != null
            && (_objects.Count == 0 || _objects.Any(o => o.IsChecked))
            && _settings.All(s => !s.HasError);

        /// <summary>Gets the command of Run.</summary>
        public ICommand RunCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Runs the plugin as chosen, one undo step of the drawing, and closes the dialog; where it is not run, says why in
        /// <see cref="Notice"/> and on the command line, and stays open.
        /// </summary>
        /// <returns>What changed; null where it was not run.</returns>
        public UpdateReport Run()
        {
            if (!CanRun)
            {
                return null;
            }

            List<string> ids = _objects.All(o => o.IsChecked)
                ? null
                : _objects.Where(o => o.IsChecked).Select(o => o.Id).ToList();
            List<KeyValuePair<string, double>> values = _settings
                .Select(s => new KeyValuePair<string, double>(s.Name, s.Value))
                .ToList();
            if (_workspace.Current != _drawing)
            {
                Notice = _drawing.Number + " is no longer the drawing shown: show it again first.";
                return null;
            }

            UpdateReport report = _workspace.RunPlugin(_plugin, _view.Id, ids, values, out string failure);

            if (report == null)
            {
                Notice = failure;
                return null;
            }

            Close(true);
            return report;
        }

        /// <summary>Lists the objects of the view of the kinds the plugin asks for, each ticked.</summary>
        private void ReadObjects()
        {
            var rows = new List<PluginObjectRow>();
            DrawingSource source = _drawing.Drawing.Source;

            if (_view != null && _input != null && source != null)
            {
                foreach (string id in _view.ObjectIds)
                {
                    if (
                        source.TryGetObject(id, out DrawingObject item)
                        && (_input.ObjectKinds.Count == 0 || _input.ObjectKinds.Contains(item.Kind))
                        && !rows.Exists(r => r.Id == id)
                    )
                    {
                        rows.Add(new PluginObjectRow(item, Changed));
                    }
                }
            }

            _objects = rows;
            Raise(nameof(Objects));
            Raise(nameof(ObjectsTitle));
            Changed();
        }

        private void Changed()
        {
            Raise(nameof(ObjectsText));
            Raise(nameof(CanRun));
            ((DelegateCommand)RunCommand).RaiseCanExecuteChanged();
        }
    }
}
