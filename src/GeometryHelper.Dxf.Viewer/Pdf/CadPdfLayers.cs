using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The layers of a PDF underlay the user switched on or off (PDFLAYERS), which the underlay keeps beside the PDF's own
    /// defaults: AutoCAD keeps them in the underlay itself, in a form its managed API does not give, so the library keeps them
    /// as the extended data of its own application on the PDFUNDERLAY, one text for each layer changed, <c>+Name</c> for a layer
    /// switched on and <c>-Name</c> for one switched off. AutoCAD ignores the data; the presenter reads it.
    /// </summary>
    internal static class CadPdfLayers
    {
        /// <summary>The application the data is of.</summary>
        internal const string Application = "GEOMETRYHELPER_PDFLAYERS";

        /// <summary>Gives the layers an underlay overrides, by name: true for one switched on, false for one switched off.</summary>
        internal static Dictionary<string, bool> Overrides(DxfPdfUnderlay underlay)
        {
            var overrides = new Dictionary<string, bool>(StringComparer.Ordinal);
            IReadOnlyList<DxfTypedValue> data = underlay.GetXDataForApplication(Application);

            if (data == null)
            {
                return overrides;
            }

            foreach (DxfTypedValue value in data)
            {
                if (value.TypeCode == 1000 && value.Value != null && value.Value.Length >= 1)
                {
                    overrides[value.Value.Substring(1)] = value.Value[0] == '+';
                }
            }

            return overrides;
        }

        /// <summary>Writes the layers an underlay overrides; none takes the data away.</summary>
        internal static void Set(DxfPdfUnderlay underlay, IReadOnlyDictionary<string, bool> overrides)
        {
            var data = new List<DxfTypedValue> { new DxfTypedValue(1001, Application) };
            var names = new List<string>(overrides.Keys);
            names.Sort(StringComparer.Ordinal);

            foreach (string name in names)
            {
                data.Add(new DxfTypedValue(1000, (overrides[name] ? "+" : "-") + name));
            }

            underlay.XData = data;
        }

        /// <summary>Whether a layer of a PDF is shown by an underlay: its own override, else the state the PDF opens with.</summary>
        internal static bool IsOn(PdfLayer layer, IReadOnlyDictionary<string, bool> overrides) =>
            overrides.TryGetValue(layer.Name, out bool on) ? on : layer.IsOn;
    }
}
