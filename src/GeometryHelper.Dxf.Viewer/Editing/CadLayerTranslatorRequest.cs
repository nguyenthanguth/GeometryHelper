using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Layer Translator of LAYTRANS, as data its command and the window share. Translate From lists the layers of the
    /// drawing; Translate To lists the layers it can be mapped to, those of the drawing to start with and those of the
    /// standards files loaded (a DWG, DWS, DWT or DXF) added, a layer of a name the list has taking the settings of the file's;
    /// Map and Map same make the mappings, Edit changes the settings a mapping gives, Save writes the standards file of the
    /// Translate To list with the mappings, and Translate carries them out. A drawing with OLD-WALL and OLD-DOOR, loaded with a
    /// standards file of WALL and DOOR, maps OLD-WALL to WALL and OLD-DOOR to DOOR, and translates them as one undo step.
    /// </summary>
    public sealed class CadLayerTranslatorRequest
    {
        /// <summary>The name the mappings are kept under in the named objects dictionary of a standards file this writes.</summary>
        internal const string MappingKey = "GH_LAYTRANS_MAP";

        private readonly List<CadTranslateLayer> _from = new List<CadTranslateLayer>();
        private readonly List<CadTranslateLayer> _to = new List<CadTranslateLayer>();
        private readonly List<CadLayerMapping> _mappings = new List<CadLayerMapping>();
        private readonly List<string> _loaded = new List<string>();
        private CadLayerShowing _showing;

        internal CadLayerTranslatorRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));

            foreach (
                DxfLayerTableRecord layer in document
                    .Database.LayerTable.Where(l => !l.IsDependent)
                    .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(l => l.Name, StringComparer.Ordinal)
            )
            {
                _from.Add(CadTranslateLayer.Of(layer, document.Database));
                _to.Add(CadTranslateLayer.Of(layer, document.Database));
            }
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the layers of the drawing that can be translated, in ordinal order of the names, letters of either case alike: those of external references are left out.</summary>
        public IReadOnlyList<CadTranslateLayer> FromLayers => _from;

        /// <summary>Gets the layers that can be translated to: the drawing's own, then those of the files loaded, in ordinal order of the names.</summary>
        public IReadOnlyList<CadTranslateLayer> ToLayers => _to;

        /// <summary>Gets the mappings made, in ordinal order of the layers translated.</summary>
        public IReadOnlyList<CadLayerMapping> Mappings => _mappings;

        /// <summary>Gets the standards files loaded, in the order loaded.</summary>
        public IReadOnlyList<string> LoadedFiles => _loaded;

        /// <summary>Gets the translator's settings; each is on until set.</summary>
        public CadLayerTranslatorSettings Settings { get; } = new CadLayerTranslatorSettings();

        /// <summary>Gets what the last change said where it was refused or could not be made; null where it was made.</summary>
        public string Message { get; private set; }

        /// <summary>Gets or sets what the dialog closed with: OK for Translate, Cancel for Cancel; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>Gets the lines of the transaction log of the last Translate, in order.</summary>
        public IReadOnlyList<string> Log { get; private set; } = Array.Empty<string>();

        /// <summary>Gets the file the last Translate wrote its log to; null where none was written.</summary>
        public string LogFile { get; private set; }

        /// <summary>
        /// Load…: adds the layers of a standards file to the Translate To list, a layer of a name the list has taking the file's
        /// settings, and the mappings the file keeps (those a Save of this wrote) for layers of the drawing that are not mapped
        /// yet. A file that cannot be read, or has no layer, changes nothing.
        /// </summary>
        /// <param name="path">The DWG, DWS, DWT or DXF file.</param>
        /// <returns>Whether it was loaded; <see cref="Message"/> says why not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool Load(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            DxfDatabase file;

            try
            {
                file = CadDrawingFile.Open(path);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is FormatException
                    || exception is InvalidDataException
                )
            {
                GeometryHelperLog.Warn("CadLayerTranslatorRequest: the file " + path + " was not read.", exception);
                Message = "Cannot read " + path + ": " + exception.Message;
                return false;
            }

            List<DxfLayerTableRecord> layers = file.LayerTable.Where(l => !l.IsDependent).ToList();

            if (layers.Count == 0)
            {
                Message = path + " has no layers.";
                return false;
            }

            foreach (DxfLayerTableRecord layer in layers)
            {
                _to.RemoveAll(t => string.Equals(t.Name, layer.Name, StringComparison.OrdinalIgnoreCase));
                _to.Add(CadTranslateLayer.Of(layer, file));
            }

            SortTo();
            _loaded.Add(path);

            foreach (CadLayerMapping kept in ReadMappings(file))
            {
                CadTranslateLayer from = _from.FirstOrDefault(f =>
                    string.Equals(f.Name, kept.From, StringComparison.OrdinalIgnoreCase)
                );

                if (
                    from != null
                    && _mappings.All(m => !string.Equals(m.From, from.Name, StringComparison.OrdinalIgnoreCase))
                )
                {
                    Put(from.Name, kept.To);
                }
            }

            Message = null;
            return true;
        }

        /// <summary>
        /// New…: adds a layer to the Translate To list, to map layers to and make in the drawing where it has none of the name.
        /// </summary>
        /// <param name="layer">The layer, with the settings it is to have.</param>
        /// <returns>Whether it was added; <see cref="Message"/> says why not: a name AutoCAD refuses, or one the list has.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the layer is null.</exception>
        public bool NewLayer(CadTranslateLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            string name = layer.Name.Trim();

            if (!CadLayerEditor.IsLayerName(name))
            {
                Message = "\"" + name + "\" is an invalid layer name.";
                return false;
            }

            if (_to.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Message = "Layer \"" + name + "\" is in the Translate To list already.";
                return false;
            }

            CadTranslateLayer added = layer.Clone();
            added.Source = null;
            _to.Add(
                new CadTranslateLayer(name)
                {
                    Color = added.Color,
                    LinetypeName = added.LinetypeName,
                    LineWeight = added.LineWeight,
                    TransparencyPercent = added.TransparencyPercent,
                    PlotStyleName = added.PlotStyleName,
                    Source = _to.Select(t => t.Source)
                        .FirstOrDefault(s => s != null && s.LinetypeTable.Has(added.LinetypeName)),
                }
            );
            SortTo();
            Message = null;
            return true;
        }

        /// <summary>
        /// Map: maps layers of the drawing to a layer of the Translate To list; a layer mapped already is mapped again. Layer 0
        /// and Defpoints are not mapped to another layer.
        /// </summary>
        /// <param name="from">The names of the layers selected in the Translate From list.</param>
        /// <param name="to">The name of the layer selected in the Translate To list.</param>
        /// <returns>How many were mapped; <see cref="Message"/> says why where none was.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the names or the layer are null.</exception>
        public int Map(IEnumerable<string> from, string to)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            CadTranslateLayer target = _to.FirstOrDefault(t =>
                string.Equals(t.Name, to, StringComparison.OrdinalIgnoreCase)
            );

            if (target == null)
            {
                Message = "Layer \"" + to + "\" is not in the Translate To list.";
                return 0;
            }

            int mapped = 0;
            Message = null;

            foreach (string name in from)
            {
                CadTranslateLayer layer = _from.FirstOrDefault(f =>
                    string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)
                );

                if (layer == null)
                {
                    continue;
                }

                if (
                    !string.Equals(layer.Name, target.Name, StringComparison.OrdinalIgnoreCase)
                    && (layer.Name == "0" || string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                )
                {
                    Message =
                        "Layer " + layer.Name + " is kept: layer 0 and Defpoints are not translated to another layer.";
                    continue;
                }

                Put(layer.Name, target);
                mapped++;
            }

            return mapped;
        }

        /// <summary>Map same: maps each layer of the drawing to the layer of the Translate To list of its name, letters of either case alike.</summary>
        /// <returns>How many were mapped.</returns>
        public int MapSame()
        {
            int mapped = 0;

            foreach (CadTranslateLayer layer in _from)
            {
                CadTranslateLayer target = _to.FirstOrDefault(t =>
                    string.Equals(t.Name, layer.Name, StringComparison.OrdinalIgnoreCase)
                );

                if (target != null)
                {
                    Put(layer.Name, target);
                    mapped++;
                }
            }

            Message = null;
            return mapped;
        }

        /// <summary>
        /// Edit…: gives the mappings of the layers named the settings of the layer they go to, as the Edit Layer dialog sets
        /// them; the layer in the Translate To list and the file it came from are left as they are.
        /// </summary>
        /// <param name="from">The names of the layers translated, as the mappings list them.</param>
        /// <param name="settings">The layer whose colour, linetype, lineweight, transparency and plot style the mappings take; its name is not used.</param>
        /// <returns>How many mappings were changed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the names or the settings are null.</exception>
        public int Edit(IEnumerable<string> from, CadTranslateLayer settings)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            int changed = 0;

            foreach (string name in from)
            {
                CadLayerMapping mapping = _mappings.FirstOrDefault(m =>
                    string.Equals(m.From, name, StringComparison.OrdinalIgnoreCase)
                );

                if (mapping == null)
                {
                    continue;
                }

                CadTranslateLayer edited = mapping.To.Clone();
                edited.Color = settings.Color;
                edited.LinetypeName = settings.LinetypeName;
                edited.LineWeight = settings.LineWeight;
                edited.TransparencyPercent = settings.TransparencyPercent;
                edited.PlotStyleName = settings.PlotStyleName;

                if (edited.Source == null || !edited.Source.LinetypeTable.Has(edited.LinetypeName))
                {
                    edited.Source = _to.Select(t => t.Source)
                        .FirstOrDefault(s => s != null && s.LinetypeTable.Has(edited.LinetypeName))
                        ?? edited.Source;
                }

                mapping.To = edited;
                changed++;
            }

            Message = null;
            return changed;
        }

        /// <summary>Remove: takes the mappings of the layers named away, so that those layers are not translated.</summary>
        /// <param name="from">The names of the layers translated, as the mappings list them.</param>
        /// <returns>How many mappings were removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the names are null.</exception>
        public int Remove(IEnumerable<string> from)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            var names = new HashSet<string>(from, StringComparer.OrdinalIgnoreCase);
            return _mappings.RemoveAll(m => names.Contains(m.From));
        }

        /// <summary>
        /// Save…: writes the Translate To list as a standards file, a DWG for a name ending in .dws or .dwg and a DXF otherwise,
        /// with the mappings made kept in it, so that Load reads them again. A linetype a layer names is copied into the file
        /// from the drawing or the file it came from.
        /// </summary>
        /// <param name="path">The file to write; one of the name is replaced.</param>
        /// <returns>Whether it was written; <see cref="Message"/> says why not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public bool Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var standards = new DxfDatabase("AC1027");

            foreach (CadTranslateLayer layer in _to)
            {
                DxfDatabase from = layer.Source ?? Document.Database;

                if (!standards.LinetypeTable.Has(layer.LinetypeName))
                {
                    CadSymbolCopy.Linetype(standards, from, layer.LinetypeName);
                }

                if (!standards.LayerTable.Has(layer.Name))
                {
                    standards.LayerTable.Add(new DxfLayerTableRecord { Name = layer.Name, ColorIndex = 7 });
                }

                DxfLayerTableRecord record = standards.LayerTable[layer.Name];
                record.Color = layer.Color;
                record.LineWeight = layer.LineWeight;

                if (standards.LinetypeTable.Has(layer.LinetypeName))
                {
                    record.LinetypeName = standards.LinetypeTable[layer.LinetypeName].Name;
                }

                record.Transparency = new DxfTransparency(
                    (byte)Math.Floor(255.0 * (100 - Math.Max(0, Math.Min(90, layer.TransparencyPercent))) / 100.0)
                );
            }

            WriteMappings(standards);

            try
            {
                CadDrawingFile.Save(standards, path);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("CadLayerTranslatorRequest: the file " + path + " was not written.", exception);
                Message = "Cannot write " + path + ": " + exception.Message;
                return false;
            }

            Message = null;
            return true;
        }

        /// <summary>
        /// Show layer contents when selected: shows only the layers named in the drawing, every other layer turned off, until
        /// <see cref="HideContents"/>; nothing is done where the setting is off. None named is the drawing as it was.
        /// </summary>
        /// <param name="names">The layers selected in the Translate From list.</param>
        public void ShowContents(IReadOnlyCollection<string> names)
        {
            if (!Settings.ShowLayerContents)
            {
                return;
            }

            if (_showing == null)
            {
                _showing = new CadLayerShowing(Document, "LAYTRANS");
            }

            _showing.Show(names);
        }

        /// <summary>Puts the layers back as they were before any contents were shown; done when the dialog closes.</summary>
        public void HideContents() => _showing?.Back();

        /// <summary>
        /// Translate: carries the mappings out as <see cref="CadLayerEditor.Translate"/> says, and writes the transaction log
        /// beside the drawing (<c>plan.dxf</c> as <c>plan.log</c>) where the settings ask and the drawing has a file.
        /// </summary>
        /// <returns>What came of it.</returns>
        public CadEditResult Translate()
        {
            HideContents();
            var log = new List<string>();
            CadEditResult result = CadLayerEditor.Translate(Document, _mappings, Settings, log);
            Message = result.Succeeded ? null : result.Message;
            LogFile = null;
            Log = log;

            if (result.Succeeded && Settings.WriteTransactionLog && Document.HasFile && Document.FileName != null)
            {
                string file = Path.ChangeExtension(Document.FileName, ".log");

                try
                {
                    File.WriteAllLines(
                        file,
                        new[]
                        {
                            "Layer Translator transaction log",
                            "Drawing: " + Path.GetFileName(Document.FileName),
                            "Mappings: " + _mappings.Count.ToString(CultureInfo.InvariantCulture),
                        }.Concat(log)
                    );
                    LogFile = file;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn(
                        "CadLayerTranslatorRequest: the log " + file + " was not written.",
                        exception
                    );
                }
            }

            return result;
        }

        /// <summary>Maps a layer of the drawing to a layer, replacing a mapping it has, the mappings kept in order.</summary>
        private void Put(string from, CadTranslateLayer to)
        {
            _mappings.RemoveAll(m => string.Equals(m.From, from, StringComparison.OrdinalIgnoreCase));
            _mappings.Add(new CadLayerMapping(from, to.Clone()));
            _mappings.Sort(
                (a, b) =>
                {
                    int order = string.Compare(a.From, b.From, StringComparison.OrdinalIgnoreCase);
                    return order != 0 ? order : string.CompareOrdinal(a.From, b.From);
                }
            );
        }

        private void SortTo() =>
            _to.Sort(
                (a, b) =>
                {
                    int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
                }
            );

        /// <summary>Writes the mappings into a standards file's named objects dictionary as one XRECORD: for each, the layer translated, the layer it goes to, its colour, linetype, lineweight, transparency and plot style, each a text.</summary>
        private void WriteMappings(DxfDatabase standards)
        {
            if (_mappings.Count == 0)
            {
                return;
            }

            var data = new List<DxfTypedValue>();

            foreach (CadLayerMapping mapping in _mappings)
            {
                CadTranslateLayer to = mapping.To;
                data.Add(new DxfTypedValue(1, mapping.From));
                data.Add(new DxfTypedValue(1, to.Name));
                data.Add(
                    new DxfTypedValue(
                        1,
                        to.Color.IsByColor
                            ? "T" + to.Color.Red + "," + to.Color.Green + "," + to.Color.Blue
                            : "A" + to.Color.ColorIndex.ToString(CultureInfo.InvariantCulture)
                    )
                );
                data.Add(new DxfTypedValue(1, to.LinetypeName));
                data.Add(new DxfTypedValue(1, ((int)to.LineWeight).ToString(CultureInfo.InvariantCulture)));
                data.Add(new DxfTypedValue(1, to.TransparencyPercent.ToString(CultureInfo.InvariantCulture)));
                data.Add(new DxfTypedValue(1, to.PlotStyleName));
            }

            standards.NamedObjectsDictionary.SetAt(MappingKey, new DxfXrecord { Data = data });
        }

        /// <summary>The mappings a standards file keeps, as <see cref="WriteMappings"/> wrote them; none where it keeps none.</summary>
        private static IEnumerable<CadLayerMapping> ReadMappings(DxfDatabase file)
        {
            DxfDBDictionary named = file.NamedObjectsDictionary;

            if (named == null || !named.Contains(MappingKey) || !(named.GetAt(MappingKey) is DxfXrecord record))
            {
                yield break;
            }

            IReadOnlyList<DxfTypedValue> data = record.Data;

            for (int i = 0; i + 6 < data.Count; i += 7)
            {
                var to = new CadTranslateLayer(data[i + 1].Value.ToString())
                {
                    LinetypeName = data[i + 3].Value.ToString(),
                    PlotStyleName = data[i + 6].Value.ToString(),
                    Source = file,
                };
                string color = data[i + 2].Value.ToString();

                if (color.StartsWith("T", StringComparison.Ordinal))
                {
                    string[] parts = color.Substring(1).Split(',');

                    if (
                        parts.Length == 3
                        && byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out byte red)
                        && byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte green)
                        && byte.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out byte blue)
                    )
                    {
                        to.Color = DxfColor.FromRgb(red, green, blue);
                    }
                }
                else if (
                    color.StartsWith("A", StringComparison.Ordinal)
                    && short.TryParse(
                        color.Substring(1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out short index
                    )
                    && index >= 1
                    && index <= 255
                )
                {
                    to.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);
                }

                if (
                    int.TryParse(
                        data[i + 4].Value.ToString(),
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int weight
                    )
                )
                {
                    to.LineWeight = (DxfLineWeight)weight;
                }

                if (
                    int.TryParse(
                        data[i + 5].Value.ToString(),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int percent
                    )
                )
                {
                    to.TransparencyPercent = Math.Max(0, Math.Min(90, percent));
                }

                yield return new CadLayerMapping(data[i].Value.ToString(), to);
            }
        }
    }
}
