using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a layer looks in one viewport, as AutoCAD's <c>LayerViewportProperties</c>: its colour, linetype, lineweight,
    /// transparency and plot style there, as <see cref="DxfLayerTableRecord.GetViewportOverrides"/> gives them, read and set.
    /// </summary>
    /// <remarks>
    /// AutoCAD keeps the overrides of a layer in XRECORDs of its extension dictionary, one for each property, each a list of
    /// a viewport (335) and its value: ADSK_XREC_LAYER_COLOR_OVR a colour (420, 0xC3 and the index above its 24 bits, or 0xC2
    /// and red, green and blue), ADSK_XREC_LAYER_LINETYPE_OVR a linetype (343), ADSK_XREC_LAYER_LINEWT_OVR a lineweight (91),
    /// ADSK_XREC_LAYER_ALPHA_OVR a transparency (440), ADSK_XREC_LAYER_PLOTSTYLE_OVR a plot style name (344). The layer steel
    /// in red, overridden blue in one viewport, reads blue there with <see cref="IsColorOverridden"/> true, and red in every
    /// other. Set, an override is written as AutoCAD 2021 writes one: steel given the colour 1 in the viewport 226 holds the
    /// XRECORD ADSK_XREC_LAYER_COLOR_OVR of <c>102 {ADSK_LYR_COLOR_OVERRIDE, 335 226, 420 -1023410175, 102 }</c>, the
    /// extension dictionary made where the layer has none, and the list of another viewport kept after it.
    /// </remarks>
    public sealed class DxfLayerViewportProperties
    {
        private const string ColorKey = "ADSK_XREC_LAYER_COLOR_OVR";
        private const string LinetypeKey = "ADSK_XREC_LAYER_LINETYPE_OVR";
        private const string LineWeightKey = "ADSK_XREC_LAYER_LINEWT_OVR";
        private const string AlphaKey = "ADSK_XREC_LAYER_ALPHA_OVR";
        private const string PlotStyleKey = "ADSK_XREC_LAYER_PLOTSTYLE_OVR";

        private readonly DxfLayerTableRecord _layer;
        private readonly DxfViewport _viewport;

        internal DxfLayerViewportProperties(DxfLayerTableRecord layer, DxfViewport viewport)
        {
            _layer = layer;
            _viewport = viewport;
        }

        /// <summary>The names of the XRECORDs that hold a layer's overrides, in the order they are read.</summary>
        internal static readonly string[] Keys = { ColorKey, LinetypeKey, LineWeightKey, AlphaKey, PlotStyleKey };

        /// <summary>Gets the layer.</summary>
        public DxfLayerTableRecord Layer => _layer;

        /// <summary>Gets the viewport, as AutoCAD's <c>ViewportId</c> names it.</summary>
        public DxfViewport Viewport => _viewport;

        /// <summary>Gets whether the layer's colour is overridden in the viewport, as AutoCAD's <c>IsColorOverridden</c>.</summary>
        public bool IsColorOverridden => Raw(ColorKey, 420) != null;

        /// <summary>
        /// Gets or sets the colour of the layer in the viewport, as AutoCAD's <c>Color</c>: the override, of the index or a true
        /// colour, else the layer's <see cref="DxfLayerTableRecord.Color"/>.
        /// </summary>
        /// <value>Set, a colour of the index, 1 to 255, or a true colour, written as the override of the viewport even where it is the layer's own.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is BYLAYER, BYBLOCK or another a layer does not take.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer or the viewport is erased or in no file, they are of two
        /// files, the file is older than R2000 (AC1015), or a true colour is set in one older than R2004 (AC1018).</exception>
        public DxfColor Color
        {
            get
            {
                string raw = Raw(ColorKey, 420);

                if (raw != null && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                {
                    int kind = (int)((value >> 24) & 0xFF);

                    if (kind == 0xC2)
                    {
                        return DxfColor.OfRgb((int)(value & 0xFFFFFF), null);
                    }

                    if (kind == 0xC3)
                    {
                        return DxfColor.OfIndex((int)(value & 0xFF));
                    }

                    if (kind == 0xC0 || kind == 0xC1)
                    {
                        return DxfColor.OfIndex(kind == 0xC0 ? 256 : 0);
                    }
                }

                return _layer.Color;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!value.IsByAci && !value.IsByColor)
                {
                    throw new ArgumentException("A layer's colour in a viewport is one of the index, 1 to 255, or a true colour: " + value + " is not.", nameof(value));
                }

                Writable(value.IsByColor ? 1018 : 1015, value.IsByColor ? "true colours" : "overrides of a layer in a viewport");
                int raw = unchecked((int)((value.IsByColor ? 0xC2000000u : 0xC3000000u) | (uint)(value.IsByColor ? value.ColorValue & 0xFFFFFF : value.ColorIndex & 0xFF)));
                Write(ColorKey, "{ADSK_LYR_COLOR_OVERRIDE", 420, raw.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets whether the layer's linetype is overridden in the viewport, as AutoCAD's <c>IsLinetypeOverridden</c>.</summary>
        public bool IsLinetypeOverridden => Raw(LinetypeKey, 343) != null;

        /// <summary>
        /// Gets or sets the name of the linetype of the layer in the viewport, as AutoCAD's <c>LinetypeObjectId</c> names it: the
        /// linetype the override names by handle, else the layer's <see cref="DxfLayerTableRecord.LinetypeName"/>.
        /// </summary>
        /// <value>Set, the name of a linetype of the drawing's table, letters of either case the same, written by its handle.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no linetype of the name set, or it is ByLayer or ByBlock.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer or the viewport is erased or in no file, they are of two
        /// files, or the file is older than R2000 (AC1015).</exception>
        public string Linetype
        {
            get
            {
                string handle = Raw(LinetypeKey, 343)?.Trim();

                if (handle != null && _layer.Item != null)
                {
                    foreach (DxfItem record in _layer.Item.File.TableItems("LTYPE"))
                    {
                        if (string.Equals(record.Handle, handle, StringComparison.OrdinalIgnoreCase))
                        {
                            return DxfReader.NameOf(_layer.Item.File.ValueOf(record, 2) ?? string.Empty);
                        }
                    }
                }

                return _layer.LinetypeName;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                DxfFile file = Writable(1015, "overrides of a layer in a viewport");
                string wanted = value.Trim();
                string handle = null;

                foreach (DxfItem record in file.TableItems("LTYPE"))
                {
                    string name = DxfReader.NameOf(file.ValueOf(record, 2) ?? string.Empty);

                    if (handle == null && string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase) && !string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase))
                    {
                        handle = record.Handle;
                    }
                }

                if (handle == null)
                {
                    throw new ArgumentException("The drawing has no linetype " + wanted + " a layer can take in a viewport.", nameof(value));
                }

                Write(LinetypeKey, "{ADSK_LYR_LINETYPE_OVERRIDE", 343, handle);
            }
        }

        /// <summary>Gets whether the layer's lineweight is overridden in the viewport, as AutoCAD's <c>IsLineWeightOverridden</c>.</summary>
        public bool IsLineWeightOverridden => Raw(LineWeightKey, 91) != null;

        /// <summary>Gets or sets the lineweight of the layer in the viewport, as AutoCAD's <c>LineWeight</c>: the override, else the layer's.</summary>
        /// <value>Set, Default or one of AutoCAD's widths: 0.50 mm is written as 91 of 50.</value>
        /// <exception cref="ArgumentException">Thrown when the lineweight set is ByLayer, ByBlock, ByDIPs or none of AutoCAD's widths.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer or the viewport is erased or in no file, they are of two
        /// files, or the file is older than R2000 (AC1015).</exception>
        public DxfLineWeight LineWeight
        {
            get => int.TryParse(Raw(LineWeightKey, 91)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) ? (DxfLineWeight)weight : _layer.LineWeight;

            set
            {
                if (value == DxfLineWeight.ByLayer || value == DxfLineWeight.ByBlock || value == DxfLineWeight.ByDIPs || !Enum.IsDefined(typeof(DxfLineWeight), value))
                {
                    throw new ArgumentException("A layer's lineweight in a viewport is Default or one of AutoCAD's widths: " + value + " is not.", nameof(value));
                }

                Writable(1015, "overrides of a layer in a viewport");
                Write(LineWeightKey, "{ADSK_LYR_LINEWT_OVERRIDE", 91, ((int)value).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets whether the layer's transparency is overridden in the viewport, as AutoCAD's <c>IsTransparencyOverridden</c>.</summary>
        public bool IsTransparencyOverridden => Raw(AlphaKey, 440) != null;

        /// <summary>Gets or sets the transparency of the layer in the viewport, as AutoCAD's <c>Transparency</c>: the override, else the layer's.</summary>
        /// <value>Set, an alpha: the 50 per cent of AutoCAD's dialog, <c>new DxfTransparency(127)</c>, is written as 440 of 33554559.</value>
        /// <exception cref="ArgumentException">Thrown when the transparency set is by layer, by block or an error value.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer or the viewport is erased or in no file, they are of two
        /// files, or the file is older than R2004 (AC1018), which has no transparency.</exception>
        public DxfTransparency Transparency
        {
            get => int.TryParse(Raw(AlphaKey, 440)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int alpha) ? DxfTransparency.OfValue(alpha) : _layer.Transparency;

            set
            {
                if (!value.IsByAlpha)
                {
                    throw new ArgumentException("A layer's transparency in a viewport is by alpha, from 0 clear to 255 solid: " + value + " is not.", nameof(value));
                }

                Writable(1018, "transparency");
                Write(AlphaKey, "{ADSK_LYR_ALPHA_OVERRIDE", 440, value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets whether the layer's plot style is overridden in the viewport, as AutoCAD's <c>IsPlotStyleOverridden</c>.</summary>
        public bool IsPlotStyleOverridden => Raw(PlotStyleKey, 344) != null;

        /// <summary>
        /// Gets or sets the name of the plot style of the layer in the viewport, as AutoCAD's <c>PlotStyleName</c>: the entry of
        /// the drawing's plot style names (ACAD_PLOTSTYLENAME) the override names by handle, else the one the layer names (its
        /// group 390); empty where neither names one, as in a drawing plotted by colour.
        /// </summary>
        /// <value>Set, a name of the dictionary of plot style names, as Normal, written by the handle of its entry.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing's dictionary of plot style names has no entry of the name set.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing plots by colour (<see cref="DxfDatabase.PlotStyleMode"/>),
        /// as AutoCAD answers ePlotStyleInColorDependentMode; when the layer or the viewport is erased or in no file, they are of
        /// two files, or the file is older than R2000 (AC1015).</exception>
        public string PlotStyleName
        {
            get
            {
                string handle = Raw(PlotStyleKey, 344)?.Trim() ?? (_layer.Item == null ? null : _layer.Item.File.ValueOf(_layer.Item, 390)?.Trim());
                DxfDBDictionary names = _layer.Item?.File.Database?.PlotStyleNameDictionary;

                if (handle == null || names == null)
                {
                    return string.Empty;
                }

                foreach (DxfDBDictionaryEntry entry in names)
                {
                    if (entry.Value != null && string.Equals(entry.Value.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Key;
                    }
                }

                return string.Empty;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                DxfFile file = Writable(1015, "overrides of a layer in a viewport");

                if (file.Database == null || file.Database.PlotStyleMode)
                {
                    throw new InvalidOperationException("The drawing plots by colour, and a layer takes no plot style in a viewport, as AutoCAD answers ePlotStyleInColorDependentMode.");
                }

                DxfDBDictionary names = file.Database.PlotStyleNameDictionary;
                DxfDBObject entry = names != null && names.Contains(value.Trim()) ? names.GetAt(value.Trim()) : null;

                if (entry?.Handle == null)
                {
                    throw new ArgumentException("The drawing has no plot style named " + value.Trim() + ".", nameof(value));
                }

                Write(PlotStyleKey, "{ADSK_LYR_PLOTSTYLE_OVERRIDE", 344, entry.Handle);
            }
        }

        /// <summary>Takes out the override of the layer's colour in the viewport, as AutoCAD's <c>RemoveColorOverride</c>; nothing where it has none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemoveColorOverride() => Forget(_layer, ColorKey, _viewport.Handle);

        /// <summary>Takes out the override of the layer's linetype in the viewport, as AutoCAD's <c>RemoveLinetypeOverride</c>; nothing where it has none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemoveLinetypeOverride() => Forget(_layer, LinetypeKey, _viewport.Handle);

        /// <summary>Takes out the override of the layer's lineweight in the viewport, as AutoCAD's <c>RemoveLineWeightOverride</c>; nothing where it has none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemoveLineWeightOverride() => Forget(_layer, LineWeightKey, _viewport.Handle);

        /// <summary>Takes out the override of the layer's transparency in the viewport, as AutoCAD's <c>RemoveTransparencyOverride</c>; nothing where it has none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemoveTransparencyOverride() => Forget(_layer, AlphaKey, _viewport.Handle);

        /// <summary>Takes out the override of the layer's plot style in the viewport, as AutoCAD's <c>RemovePlotStyleOverride</c>; nothing where it has none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemovePlotStyleOverride() => Forget(_layer, PlotStyleKey, _viewport.Handle);

        /// <summary>Gives the properties as a line for debugging: the layer and the viewport.</summary>
        /// <returns>The line, such as <c>steel in VIEWPORT 9D</c>.</returns>
        public override string ToString() => _layer.Name + " in VIEWPORT " + (_viewport.Handle ?? string.Empty);

        /// <summary>The text of the override of the viewport in an XRECORD of the layer's; null where it has none.</summary>
        private string Raw(string key, int code)
        {
            foreach (KeyValuePair<string, string> value in Values(_layer, key, code))
            {
                if (_viewport.Handle != null && string.Equals(value.Key, _viewport.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return value.Value;
                }
            }

            return null;
        }

        /// <summary>The file an override is written into, checked: the layer and the viewport in it, not erased, of a version that has the property.</summary>
        private DxfFile Writable(int version, string what)
        {
            if (_layer.Item == null || _layer.IsErased || _viewport.Item == null || _viewport.IsErased || _viewport.Handle == null)
            {
                throw new InvalidOperationException("An override is written for a layer and a viewport of a file, neither erased.");
            }

            DxfFile file = _layer.Item.File;

            if (_viewport.Item.File != file)
            {
                throw new InvalidOperationException("The layer and the viewport are of two files.");
            }

            file.Requires(version, what, version >= 1018 ? "R2004 (AC1018)" : "R2000 (AC1015)");
            return file;
        }

        /// <summary>
        /// Writes the override of the viewport into an XRECORD of the layer's extension dictionary: the value of the viewport's
        /// list replaced where it has one, else a list added after the others, as AutoCAD adds the list of a second viewport;
        /// the dictionary and the XRECORD made where the layer has none.
        /// </summary>
        private void Write(string key, string open, int code, string value)
        {
            _layer.CreateExtensionDictionary();
            DxfDBDictionary dictionary = _layer.ExtensionDictionary;
            string viewport = _viewport.Handle;

            if (!dictionary.Contains(key))
            {
                dictionary.SetAt(key, new DxfXrecord { Data = List(open, viewport, code, value) });
                return;
            }

            if (!(dictionary.GetAt(key) is DxfXrecord record))
            {
                throw new InvalidOperationException("The extension dictionary of the layer " + _layer.Name + " holds " + key + " as an object other than an XRECORD.");
            }

            var data = new List<DxfTypedValue>(record.Data);
            bool named = false;

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].TypeCode == 335)
                {
                    named = string.Equals(data[i].Value.Trim(), viewport, StringComparison.OrdinalIgnoreCase);
                }
                else if (named && data[i].TypeCode == code)
                {
                    data[i] = new DxfTypedValue(code, value);
                    record.Data = data;
                    return;
                }
            }

            data.AddRange(List(open, viewport, code, value));
            record.Data = data;
        }

        /// <summary>The list of one viewport's override, as AutoCAD writes it.</summary>
        private static DxfTypedValue[] List(string open, string viewport, int code, string value)
            => new[] { new DxfTypedValue(102, open), new DxfTypedValue(335, viewport), new DxfTypedValue(code, value), new DxfTypedValue(102, "}") };

        /// <summary>
        /// Takes the overrides of a viewport out of a layer's XRECORDs, as AutoCAD does when the viewport is erased: each list
        /// <c>102 {...}</c> that names it (335) taken out, and an XRECORD left with none taken out with its entry.
        /// </summary>
        internal static void Forget(DxfLayerTableRecord layer, string viewport)
        {
            foreach (string key in Keys)
            {
                Forget(layer, key, viewport);
            }
        }

        /// <summary>Takes the override of a viewport out of one XRECORD of a layer's, the XRECORD with its entry where none is left.</summary>
        private static void Forget(DxfLayerTableRecord layer, string key, string viewport)
        {
            DxfDBDictionary dictionary = layer.ExtensionDictionary;

            if (dictionary == null || viewport == null || !dictionary.Contains(key) || !(dictionary.GetAt(key) is DxfXrecord record))
            {
                return;
            }

            var kept = new List<DxfTypedValue>();
            var list = new List<DxfTypedValue>();
            bool named = false;
            bool inside = false;
            bool changed = false;

            foreach (DxfTypedValue group in record.Data)
            {
                if (group.TypeCode == 102 && group.Value.Trim().StartsWith("{", StringComparison.Ordinal))
                {
                    inside = true;
                    named = false;
                    list.Clear();
                }

                if (!inside)
                {
                    kept.Add(group);
                    continue;
                }

                list.Add(group);
                named |= group.TypeCode == 335 && string.Equals(group.Value.Trim(), viewport, StringComparison.OrdinalIgnoreCase);

                if (group.TypeCode == 102 && group.Value.Trim() == "}")
                {
                    inside = false;
                    changed |= named;

                    if (!named)
                    {
                        kept.AddRange(list);
                    }
                }
            }

            kept.AddRange(inside ? list : new List<DxfTypedValue>());

            if (!changed)
            {
                return;
            }

            if (kept.Count == 0)
            {
                dictionary.Remove(key);
            }
            else
            {
                record.Data = kept;
            }
        }

        /// <summary>Every override of a layer, each the handle of its viewport and its text, of the XRECORDs in the order of <see cref="Keys"/>.</summary>
        internal static List<KeyValuePair<string, string>> Overrides(DxfLayerTableRecord layer)
        {
            var all = new List<KeyValuePair<string, string>>();
            all.AddRange(Values(layer, ColorKey, 420));
            all.AddRange(Values(layer, LinetypeKey, 343));
            all.AddRange(Values(layer, LineWeightKey, 91));
            all.AddRange(Values(layer, AlphaKey, 440));
            all.AddRange(Values(layer, PlotStyleKey, 344));
            return all;
        }

        /// <summary>The overrides of an XRECORD of a layer's extension dictionary: each viewport (335) with the value of a code after it, in their order.</summary>
        private static List<KeyValuePair<string, string>> Values(DxfLayerTableRecord layer, string key, int code)
        {
            var values = new List<KeyValuePair<string, string>>();
            DxfDBDictionary dictionary = layer.ExtensionDictionary;

            if (dictionary == null || !dictionary.Contains(key) || !(dictionary.GetAt(key) is DxfXrecord record))
            {
                return values;
            }

            string viewport = null;

            foreach (DxfTypedValue group in record.Data)
            {
                if (group.TypeCode == 335)
                {
                    viewport = group.Value.Trim();
                }
                else if (group.TypeCode == code && viewport != null)
                {
                    values.Add(new KeyValuePair<string, string>(viewport, group.Value));
                    viewport = null;
                }
            }

            return values;
        }
    }
}
