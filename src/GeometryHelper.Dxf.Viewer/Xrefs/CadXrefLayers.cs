using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the drawing has changed of a layer of an external reference (<c>grid|WALL</c>), kept as AutoCAD 2021 keeps it
    /// under VISRETAIN 1: the bits of the 1071 of the layer's extended data of the application
    /// <c>AcXRefLayerPropertyOverride</c>, each set when the drawing changes that property, the rest read from the drawing
    /// referenced. AutoCAD 2021, changing one property at a time of <c>b|WALL</c> with -LAYER and writing the drawing out,
    /// sets 1 for Off, 2 Freeze, 4 Lock, 8 Plot, 16 and 256 Color, 32 Ltype, 64 LWeight, 128 TRansparency and 1024
    /// Description, and writes 0 for a layer it has not changed.
    /// </summary>
    internal static class CadXrefLayers
    {
        internal const string Application = "AcXRefLayerPropertyOverride";
        internal const int Off = 1;
        internal const int Frozen = 2;
        internal const int Locked = 4;
        internal const int Plot = 8;
        internal const int Color = 16 | 256;
        internal const int Linetype = 32;
        internal const int LineWeight = 64;
        internal const int Transparency = 128;
        internal const int Description = 1024;

        /// <summary>The bits a layer of an external reference has, null where it keeps none, as a layer AutoCAD wrote before 2018 keeps none.</summary>
        internal static int? Overrides(DxfLayerTableRecord layer)
        {
            IReadOnlyList<DxfTypedValue> data = layer.GetXDataForApplication(Application);

            for (int i = 1; data != null && i < data.Count; i++)
            {
                if (data[i].TypeCode == 1071 && int.TryParse(data[i].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int bits))
                {
                    return bits;
                }
            }

            return null;
        }

        /// <summary>Sets the bits of a change made to a layer of an external reference; a layer of the drawing's own is left as it is.</summary>
        internal static void Mark(DxfLayerTableRecord layer, int bits)
        {
            if (!layer.IsDependent)
            {
                return;
            }

            int now = (Overrides(layer) ?? 0) | bits;
            layer.XData = new[] { new DxfTypedValue(1001, Application), new DxfTypedValue(1071, now.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        }
    }
}
