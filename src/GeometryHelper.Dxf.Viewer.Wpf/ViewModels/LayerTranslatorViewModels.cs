using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The words a lineweight and a transparency have in the Layer Translator's lists and its Edit Layer dialog.</summary>
    internal static class TranslateWords
    {
        internal static string Weight(DxfLineWeight weight) =>
            weight == DxfLineWeight.ByLineWeightDefault
                ? "Default"
                : ((int)weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";

        internal static int Rgb(DxfColor color) =>
            color.IsByColor || color.IsByAci ? color.ColorValue & 0xFFFFFF : 0xFFFFFF;
    }

    /// <summary>A row of the Translate From or Translate To list: the layer's name and its settings, and whether it is in use.</summary>
    public sealed class TranslateLayerRowViewModel
    {
        internal TranslateLayerRowViewModel(CadTranslateLayer layer)
        {
            Layer = layer;
        }

        /// <summary>Gets the layer.</summary>
        public CadTranslateLayer Layer { get; }

        /// <summary>Gets the name.</summary>
        public string Name => Layer.Name;

        /// <summary>Gets "In use" for a layer something is on, else empty.</summary>
        public string Use => Layer.IsInUse ? "In use" : string.Empty;

        /// <summary>Gets the colour as 0xRRGGBB, for its swatch.</summary>
        public int Rgb => TranslateWords.Rgb(Layer.Color);

        /// <summary>Gets the linetype's name.</summary>
        public string Linetype => Layer.LinetypeName;

        /// <summary>Gets the lineweight as the lists show it: 0.35 mm, Default.</summary>
        public string LineWeight => TranslateWords.Weight(Layer.LineWeight);
    }

    /// <summary>A row of the Layer Translation Mappings list: the old layer, the new one with the settings it gives.</summary>
    public sealed class TranslateMappingRowViewModel
    {
        internal TranslateMappingRowViewModel(CadLayerMapping mapping)
        {
            Mapping = mapping;
        }

        /// <summary>Gets the mapping.</summary>
        public CadLayerMapping Mapping { get; }

        /// <summary>Gets the old layer's name.</summary>
        public string OldName => Mapping.From;

        /// <summary>Gets the new layer's name.</summary>
        public string NewName => Mapping.To.Name;

        /// <summary>Gets the linetype's name.</summary>
        public string Linetype => Mapping.To.LinetypeName;

        /// <summary>Gets the colour as 0xRRGGBB, for its swatch.</summary>
        public int Rgb => TranslateWords.Rgb(Mapping.To.Color);

        /// <summary>Gets the lineweight as the list shows it.</summary>
        public string LineWeight => TranslateWords.Weight(Mapping.To.LineWeight);

        /// <summary>Gets the transparency in per cent.</summary>
        public string Transparency => Mapping.To.TransparencyPercent.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the plot style's name.</summary>
        public string PlotStyle => Mapping.To.PlotStyleName;
    }

    /// <summary>A lineweight the Edit Layer dialog offers, with the words it shows.</summary>
    public sealed class TranslateWeightChoice
    {
        internal TranslateWeightChoice(DxfLineWeight weight)
        {
            Weight = weight;
        }

        /// <summary>Gets the lineweight.</summary>
        public DxfLineWeight Weight { get; }

        /// <summary>Gets the words: Default, 0.00 mm, 0.35 mm.</summary>
        public string Text => TranslateWords.Weight(Weight);

        /// <inheritdoc/>
        public override string ToString() => Text;
    }

    /// <summary>
    /// AutoCAD's New Layer and Edit Layer of the Layer Translator, one dialog: the name (typed for a new layer, fixed for an
    /// edit), the colour and linetype picked with Select Color and Select Linetype, the lineweight, the transparency and the
    /// plot style. OK checks the name and the transparency; <see cref="Commit"/> gives the layer.
    /// </summary>
    public sealed class TranslateLayerViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private readonly Func<string, bool> _taken;
        private string _name;
        private DxfColor _color;
        private string _linetype;
        private TranslateWeightChoice _weight;
        private string _transparency;
        private string _plotStyle;
        private string _error = string.Empty;

        /// <summary>Makes the dialog on a layer.</summary>
        /// <param name="document">The document, whose linetypes Select Linetype lists.</param>
        /// <param name="layer">The layer it opens on.</param>
        /// <param name="isNew">Whether it is New Layer: the name is typed and may not be one the list has.</param>
        /// <param name="taken">Whether a name is one the Translate To list has already; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the layer is null.</exception>
        public TranslateLayerViewModel(
            CadDocument document,
            CadTranslateLayer layer,
            bool isNew,
            Func<string, bool> taken
        )
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            IsNew = isNew;
            _taken = taken ?? (n => false);
            _name = layer.Name;
            _color = layer.Color;
            _linetype = layer.LinetypeName;
            _transparency = layer.TransparencyPercent.ToString(CultureInfo.InvariantCulture);
            _plotStyle = layer.PlotStyleName;
            LineWeights = new[]
            {
                DxfLineWeight.ByLineWeightDefault,
                DxfLineWeight.LineWeight000,
                DxfLineWeight.LineWeight005,
                DxfLineWeight.LineWeight009,
                DxfLineWeight.LineWeight013,
                DxfLineWeight.LineWeight015,
                DxfLineWeight.LineWeight018,
                DxfLineWeight.LineWeight020,
                DxfLineWeight.LineWeight025,
                DxfLineWeight.LineWeight030,
                DxfLineWeight.LineWeight035,
                DxfLineWeight.LineWeight040,
                DxfLineWeight.LineWeight050,
                DxfLineWeight.LineWeight053,
                DxfLineWeight.LineWeight060,
                DxfLineWeight.LineWeight070,
                DxfLineWeight.LineWeight080,
                DxfLineWeight.LineWeight090,
                DxfLineWeight.LineWeight100,
                DxfLineWeight.LineWeight106,
                DxfLineWeight.LineWeight120,
                DxfLineWeight.LineWeight140,
                DxfLineWeight.LineWeight158,
                DxfLineWeight.LineWeight200,
                DxfLineWeight.LineWeight211,
            }
                .Select(w => new TranslateWeightChoice(w))
                .ToList();
            _weight = LineWeights.FirstOrDefault(w => w.Weight == layer.LineWeight) ?? LineWeights[0];
        }

        /// <summary>Gets whether the dialog is New Layer; Edit Layer leaves the name as it is.</summary>
        public bool IsNew { get; }

        /// <summary>Gets whether the name is fixed: an edit leaves it as it is.</summary>
        public bool IsNameFixed => !IsNew;

        /// <summary>Gets the dialog's title: New Layer or Edit Layer.</summary>
        public string Title => IsNew ? "New Layer" : "Edit Layer";

        /// <summary>Gets or sets the name; typed only for a new layer.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets the colour as 0xRRGGBB, for its swatch.</summary>
        public int Rgb => TranslateWords.Rgb(_color);

        /// <summary>Gets the colour's words: its number, or the three values of a true colour.</summary>
        public string ColorText =>
            _color.IsByColor
                ? _color.Red.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + _color.Green.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + _color.Blue.ToString(CultureInfo.InvariantCulture)
                : _color.ColorIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the name of the linetype.</summary>
        public string Linetype => _linetype;

        /// <summary>Gets the lineweights offered, Default first.</summary>
        public IReadOnlyList<TranslateWeightChoice> LineWeights { get; }

        /// <summary>Gets or sets the lineweight chosen.</summary>
        public TranslateWeightChoice SelectedWeight
        {
            get => _weight;
            set => Set(ref _weight, value ?? LineWeights[0]);
        }

        /// <summary>Gets or sets the transparency in per cent, as typed: 0 to 90.</summary>
        public string Transparency
        {
            get => _transparency;
            set => Set(ref _transparency, value ?? string.Empty);
        }

        /// <summary>Gets or sets the plot style's name; empty for none.</summary>
        public string PlotStyle
        {
            get => _plotStyle;
            set => Set(ref _plotStyle, value ?? string.Empty);
        }

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Error
        {
            get => _error;
            private set => Set(ref _error, value ?? string.Empty);
        }

        /// <summary>Gets or sets how the window shows Select Color: true for OK.</summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>Gets or sets how the window shows Select Linetype: true for OK.</summary>
        public Func<SelectLinetypeViewModel, bool> PickLinetype { get; set; }

        /// <summary>The colour button: Select Color, the colour chosen kept.</summary>
        public void ChooseColor()
        {
            var dialog = new SelectColorViewModel(_color, false);

            if (PickColor != null && PickColor(dialog))
            {
                _color = dialog.Color;
                Raise(nameof(Rgb));
                Raise(nameof(ColorText));
            }
        }

        /// <summary>The linetype button: Select Linetype, the linetype chosen kept.</summary>
        public void ChooseLinetype()
        {
            var dialog = new SelectLinetypeViewModel(_document, _linetype);

            if (PickLinetype != null && PickLinetype(dialog) && !string.IsNullOrEmpty(dialog.Linetype))
            {
                _linetype = dialog.Linetype;
                Raise(nameof(Linetype));
            }
        }

        /// <summary>OK: checks the name (one AutoCAD takes, not one the list has, for a new layer) and the transparency (0 to 90).</summary>
        /// <returns>The layer with the settings of the dialog; null, <see cref="Error"/> saying why, where one is wrong.</returns>
        public CadTranslateLayer Commit()
        {
            string name = (_name ?? string.Empty).Trim();

            if (IsNew && !CadLayerEditor.IsLayerName(name))
            {
                Error = name.Length == 0 ? "A layer needs a name." : "\"" + name + "\" is an invalid layer name.";
                return null;
            }

            if (IsNew && _taken(name))
            {
                Error = "Layer \"" + name + "\" is in the Translate To list already.";
                return null;
            }

            if (
                !int.TryParse(
                    (_transparency ?? string.Empty).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int percent
                )
                || percent > 90
            )
            {
                Error = "Transparency is a whole number from 0 to 90.";
                return null;
            }

            Error = string.Empty;
            return new CadTranslateLayer(IsNew ? name : _name)
            {
                Color = _color,
                LinetypeName = _linetype,
                LineWeight = _weight.Weight,
                TransparencyPercent = percent,
                PlotStyleName = (_plotStyle ?? string.Empty).Trim(),
            };
        }
    }

    /// <summary>
    /// AutoCAD's Layer Translator Settings: the six switches of <see cref="CadLayerTranslatorSettings"/>. The window edits a copy
    /// and OK writes it back.
    /// </summary>
    public sealed class TranslateSettingsViewModel : ObservableObject
    {
        /// <summary>Makes the dialog's logic on the settings, copied.</summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public TranslateSettingsViewModel(CadLayerTranslatorSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ForceColorByLayer = settings.ForceColorByLayer;
            ForceLinetypeByLayer = settings.ForceLinetypeByLayer;
            ForceTransparencyByLayer = settings.ForceTransparencyByLayer;
            TranslateObjectsInBlocks = settings.TranslateObjectsInBlocks;
            WriteTransactionLog = settings.WriteTransactionLog;
            ShowLayerContents = settings.ShowLayerContents;
        }

        /// <summary>Gets the settings OK writes to.</summary>
        public CadLayerTranslatorSettings Settings { get; }

        /// <summary>Gets or sets whether the objects of the translated layers get the colour ByLayer.</summary>
        public bool ForceColorByLayer { get; set; }

        /// <summary>Gets or sets whether the objects of the translated layers get the linetype ByLayer.</summary>
        public bool ForceLinetypeByLayer { get; set; }

        /// <summary>Gets or sets whether the objects of the translated layers get the transparency ByLayer.</summary>
        public bool ForceTransparencyByLayer { get; set; }

        /// <summary>Gets or sets whether the objects in blocks are translated.</summary>
        public bool TranslateObjectsInBlocks { get; set; }

        /// <summary>Gets or sets whether a transaction log is written.</summary>
        public bool WriteTransactionLog { get; set; }

        /// <summary>Gets or sets whether a layer selected shows its contents.</summary>
        public bool ShowLayerContents { get; set; }

        /// <summary>OK: writes what was ticked to the settings.</summary>
        public void Commit()
        {
            Settings.ForceColorByLayer = ForceColorByLayer;
            Settings.ForceLinetypeByLayer = ForceLinetypeByLayer;
            Settings.ForceTransparencyByLayer = ForceTransparencyByLayer;
            Settings.TranslateObjectsInBlocks = TranslateObjectsInBlocks;
            Settings.WriteTransactionLog = WriteTransactionLog;
            Settings.ShowLayerContents = ShowLayerContents;
        }
    }

    /// <summary>
    /// AutoCAD's Layer Translator over LAYTRANS's request: Translate From (the layers of the drawing, with a selection filter),
    /// Translate To (its own layers, those of the standards files loaded and the new ones), Map and Map same, the mappings with
    /// Edit, Remove and Save, Settings, and Translate. Load, New, Edit, Save and Settings ask the window through the hooks.
    /// </summary>
    public sealed class LayerTranslatorViewModel : ObservableObject
    {
        private string _message = string.Empty;
        private string _filter = string.Empty;

        /// <summary>Makes the dialog's logic over a request.</summary>
        /// <param name="request">The request of LAYTRANS.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public LayerTranslatorViewModel(CadLayerTranslatorRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Refill();
        }

        /// <summary>Gets the request.</summary>
        public CadLayerTranslatorRequest Request { get; }

        /// <summary>Gets the Translate From rows.</summary>
        public ObservableCollection<TranslateLayerRowViewModel> FromRows { get; } =
            new ObservableCollection<TranslateLayerRowViewModel>();

        /// <summary>Gets the Translate To rows.</summary>
        public ObservableCollection<TranslateLayerRowViewModel> ToRows { get; } =
            new ObservableCollection<TranslateLayerRowViewModel>();

        /// <summary>Gets the Layer Translation Mappings rows.</summary>
        public ObservableCollection<TranslateMappingRowViewModel> MappingRows { get; } =
            new ObservableCollection<TranslateMappingRowViewModel>();

        /// <summary>Gets or sets the selection filter, a wild-card pattern: Select picks the Translate From layers that match.</summary>
        public string SelectFilter
        {
            get => _filter;
            set => Set(ref _filter, value ?? string.Empty);
        }

        /// <summary>Gets what the last change said where it was refused or could not be made; empty where it was made.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets whether Translate can run: at least one layer is mapped.</summary>
        public bool CanTranslate => MappingRows.Count > 0;

        /// <summary>Gets or sets how the window asks the standards file to load: its path, null for Cancel.</summary>
        public Func<string> AskLoadFile { get; set; }

        /// <summary>Gets or sets how the window asks the file to save the standards to: its path, null for Cancel.</summary>
        public Func<string> AskSaveFile { get; set; }

        /// <summary>Gets or sets how the window shows New Layer and Edit Layer: true for OK.</summary>
        public Func<TranslateLayerViewModel, bool> ShowLayerDialog { get; set; }

        /// <summary>Gets or sets how the window shows Settings: true for OK.</summary>
        public Func<TranslateSettingsViewModel, bool> ShowSettings { get; set; }

        /// <summary>Select: the names of the Translate From layers the selection filter matches, for the window to select.</summary>
        /// <returns>The names, in the order of the list.</returns>
        public IReadOnlyList<string> SelectByFilter()
        {
            string pattern = _filter.Trim();
            return FromRows
                .Where(r => pattern.Length == 0 || pattern == "*" || CadWildcard.IsMatch(r.Name, pattern, true))
                .Select(r => r.Name)
                .ToList();
        }

        /// <summary>The Translate From selection changed: with Show layer contents on, only those layers are shown in the drawing.</summary>
        /// <param name="names">The names selected.</param>
        public void FromSelected(IReadOnlyCollection<string> names) => Request.ShowContents(names);

        /// <summary>Load…: asks a standards file and adds its layers to Translate To.</summary>
        /// <returns>Whether a file was loaded.</returns>
        public bool Load()
        {
            string path = AskLoadFile?.Invoke();

            if (path == null)
            {
                return false;
            }

            bool loaded = Request.Load(path);
            Message = loaded ? string.Empty : Request.Message;
            Refill();
            return loaded;
        }

        /// <summary>New…: asks a layer and adds it to Translate To.</summary>
        /// <returns>Whether a layer was added.</returns>
        public bool NewLayer()
        {
            var dialog = new TranslateLayerViewModel(
                Request.Document,
                new CadTranslateLayer(string.Empty),
                true,
                name => Request.ToLayers.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
            );

            if (ShowLayerDialog == null || !ShowLayerDialog(dialog))
            {
                return false;
            }

            CadTranslateLayer layer = dialog.Commit();
            bool added = layer != null && Request.NewLayer(layer);
            Message =
                added ? string.Empty
                : layer == null ? dialog.Error
                : Request.Message;
            Refill();
            return added;
        }

        /// <summary>Map: maps the layers selected in Translate From to the layer selected in Translate To.</summary>
        /// <param name="from">The names selected in Translate From.</param>
        /// <param name="to">The name selected in Translate To; null for none.</param>
        /// <returns>How many layers were mapped.</returns>
        public int Map(IReadOnlyCollection<string> from, string to)
        {
            if (to == null)
            {
                Message = "Select the layer to translate to.";
                return 0;
            }

            int mapped = Request.Map(from, to);
            Message = mapped == 0 && Request.Message == null ? "Select the layers to translate." : Request.Message;
            Refill();
            return mapped;
        }

        /// <summary>Map same: maps every layer of the drawing to the Translate To layer of its name.</summary>
        /// <returns>How many layers were mapped.</returns>
        public int MapSame()
        {
            int mapped = Request.MapSame();
            Message = mapped == 0 ? "No layer of the drawing has a layer of its name in Translate To." : string.Empty;
            Refill();
            return mapped;
        }

        /// <summary>Edit…: shows the layer of the first mapping selected, and gives the settings to every mapping selected.</summary>
        /// <param name="names">The old names of the mappings selected.</param>
        /// <returns>How many mappings were changed.</returns>
        public int Edit(IReadOnlyCollection<string> names)
        {
            TranslateMappingRowViewModel first = MappingRows.FirstOrDefault(r =>
                names.Contains(r.OldName, StringComparer.OrdinalIgnoreCase)
            );

            if (first == null || ShowLayerDialog == null)
            {
                return 0;
            }

            var dialog = new TranslateLayerViewModel(Request.Document, first.Mapping.To, false, null);

            if (!ShowLayerDialog(dialog))
            {
                return 0;
            }

            CadTranslateLayer edited = dialog.Commit();

            if (edited == null)
            {
                Message = dialog.Error;
                return 0;
            }

            int changed = Request.Edit(names, edited);
            Message = string.Empty;
            Refill();
            return changed;
        }

        /// <summary>Remove: takes the mappings selected away.</summary>
        /// <param name="names">The old names of the mappings selected.</param>
        /// <returns>How many were removed.</returns>
        public int Remove(IReadOnlyCollection<string> names)
        {
            int removed = Request.Remove(names);
            Refill();
            return removed;
        }

        /// <summary>Save…: asks a file and writes the Translate To list with the mappings to it.</summary>
        /// <returns>Whether it was written.</returns>
        public bool Save()
        {
            string path = AskSaveFile?.Invoke();

            if (path == null)
            {
                return false;
            }

            bool saved = Request.Save(path);
            Message = saved ? "Saved " + path : Request.Message;
            return saved;
        }

        /// <summary>Settings…: shows the settings, OK writing them back.</summary>
        /// <returns>Whether OK was pressed.</returns>
        public bool OpenSettings()
        {
            var dialog = new TranslateSettingsViewModel(Request.Settings);

            if (ShowSettings == null || !ShowSettings(dialog))
            {
                return false;
            }

            dialog.Commit();

            if (!Request.Settings.ShowLayerContents)
            {
                Request.HideContents();
            }

            return true;
        }

        /// <summary>Translate: closes with OK, the command carrying the mappings out.</summary>
        /// <returns>Whether it can: at least one layer is mapped.</returns>
        public bool Translate()
        {
            if (!CanTranslate)
            {
                Message = "No layer is mapped.";
                return false;
            }

            Request.Action = CadDialogAction.Ok;
            return true;
        }

        /// <summary>Cancel: closes with nothing translated.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void Refill()
        {
            Fill(FromRows, Request.FromLayers.Select(l => new TranslateLayerRowViewModel(l)));
            Fill(ToRows, Request.ToLayers.Select(l => new TranslateLayerRowViewModel(l)));
            Fill(MappingRows, Request.Mappings.Select(m => new TranslateMappingRowViewModel(m)));
            Raise(nameof(CanTranslate));
        }

        private static void Fill<T>(ObservableCollection<T> rows, IEnumerable<T> items)
        {
            rows.Clear();

            foreach (T item in items)
            {
                rows.Add(item);
            }
        }
    }
}
