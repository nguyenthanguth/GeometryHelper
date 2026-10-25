using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a layer looks in one viewport, as AutoCAD's <c>LayerViewportProperties</c>: its colour, linetype, lineweight and
    /// transparency there, as <see cref="DxfLayerTableRecord.GetViewportOverrides"/> gives them, read.
    /// </summary>
    /// <remarks>
    /// AutoCAD keeps the overrides of a layer in XRECORDs of its extension dictionary, one for each property, each a list of
    /// a viewport (335) and its value: ADSK_XREC_LAYER_COLOR_OVR a colour (420, 0xC3 and the index above its 24 bits, or 0xC2
    /// and red, green and blue), ADSK_XREC_LAYER_LINETYPE_OVR a linetype (343), ADSK_XREC_LAYER_LINEWT_OVR a lineweight (91),
    /// ADSK_XREC_LAYER_ALPHA_OVR a transparency (440). The layer steel in red, overridden blue in one viewport, reads blue
    /// there with <see cref="IsColorOverridden"/> true, and red in every other.
    /// </remarks>
    public sealed class DxfLayerViewportProperties
    {
        private const string ColorKey = "ADSK_XREC_LAYER_COLOR_OVR";
        private const string LinetypeKey = "ADSK_XREC_LAYER_LINETYPE_OVR";
        private const string LineWeightKey = "ADSK_XREC_LAYER_LINEWT_OVR";
        private const string AlphaKey = "ADSK_XREC_LAYER_ALPHA_OVR";

        private readonly DxfLayerTableRecord _layer;
        private readonly DxfViewport _viewport;

        internal DxfLayerViewportProperties(DxfLayerTableRecord layer, DxfViewport viewport)
        {
            _layer = layer;
            _viewport = viewport;
        }

        /// <summary>The names of the XRECORDs that hold a layer's overrides, in the order they are read.</summary>
        internal static readonly string[] Keys = { ColorKey, LinetypeKey, LineWeightKey, AlphaKey };

        /// <summary>Gets the layer.</summary>
        public DxfLayerTableRecord Layer => _layer;

        /// <summary>Gets the viewport, as AutoCAD's <c>ViewportId</c> names it.</summary>
        public DxfViewport Viewport => _viewport;

        /// <summary>Gets whether the layer's colour is overridden in the viewport, as AutoCAD's <c>IsColorOverridden</c>.</summary>
        public bool IsColorOverridden => Raw(ColorKey, 420) != null;

        /// <summary>
        /// Gets the colour of the layer in the viewport, as AutoCAD's <c>Color</c>: the override, of the index or a true colour,
        /// else the layer's <see cref="DxfLayerTableRecord.Color"/>.
        /// </summary>
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
        }

        /// <summary>Gets whether the layer's linetype is overridden in the viewport, as AutoCAD's <c>IsLinetypeOverridden</c>.</summary>
        public bool IsLinetypeOverridden => Raw(LinetypeKey, 343) != null;

        /// <summary>
        /// Gets the name of the linetype of the layer in the viewport, as AutoCAD's <c>LinetypeObjectId</c> names it: the
        /// linetype the override names by handle, else the layer's <see cref="DxfLayerTableRecord.LinetypeName"/>.
        /// </summary>
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
        }

        /// <summary>Gets whether the layer's lineweight is overridden in the viewport, as AutoCAD's <c>IsLineWeightOverridden</c>.</summary>
        public bool IsLineWeightOverridden => Raw(LineWeightKey, 91) != null;

        /// <summary>Gets the lineweight of the layer in the viewport, as AutoCAD's <c>LineWeight</c>: the override, else the layer's.</summary>
        public DxfLineWeight LineWeight
            => int.TryParse(Raw(LineWeightKey, 91)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) ? (DxfLineWeight)weight : _layer.LineWeight;

        /// <summary>Gets whether the layer's transparency is overridden in the viewport, as AutoCAD's <c>IsTransparencyOverridden</c>.</summary>
        public bool IsTransparencyOverridden => Raw(AlphaKey, 440) != null;

        /// <summary>Gets the transparency of the layer in the viewport, as AutoCAD's <c>Transparency</c>: the override, else the layer's.</summary>
        public DxfTransparency Transparency
            => int.TryParse(Raw(AlphaKey, 440)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int alpha) ? DxfTransparency.OfValue(alpha) : _layer.Transparency;

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

        /// <summary>
        /// Takes the overrides of a viewport out of a layer's XRECORDs, as AutoCAD does when the viewport is erased: each list
        /// <c>102 {...}</c> that names it (335) taken out, and an XRECORD left with none taken out with its entry.
        /// </summary>
        internal static void Forget(DxfLayerTableRecord layer, string viewport)
        {
            DxfDBDictionary dictionary = layer.ExtensionDictionary;

            if (dictionary == null || viewport == null)
            {
                return;
            }

            foreach (string key in Keys)
            {
                if (!dictionary.Contains(key) || !(dictionary.GetAt(key) is DxfXrecord record))
                {
                    continue;
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
                    continue;
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
        }

        /// <summary>Every override of a layer, each the handle of its viewport and its text, of the XRECORDs in the order of <see cref="Keys"/>.</summary>
        internal static List<KeyValuePair<string, string>> Overrides(DxfLayerTableRecord layer)
        {
            var all = new List<KeyValuePair<string, string>>();
            all.AddRange(Values(layer, ColorKey, 420));
            all.AddRange(Values(layer, LinetypeKey, 343));
            all.AddRange(Values(layer, LineWeightKey, 91));
            all.AddRange(Values(layer, AlphaKey, 440));
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
