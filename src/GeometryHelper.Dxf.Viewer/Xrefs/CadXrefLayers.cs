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

        /// <summary>
        /// Writes into a drawing the records AutoCAD 2021 writes there for an external reference it loads, or says whether any
        /// is to be written (<paramref name="write"/> false): for each linetype of the drawing referenced but ByBlock, ByLayer
        /// and Continuous, <c>x|DASHED</c> with its description and dashes; for each layer but 0, <c>x|WALL</c> with its colour,
        /// linetype (<c>x|DASHED</c>, or ByLayer, ByBlock and Continuous as they are), lineweight, on, frozen, locked, plotted,
        /// transparency and description, and the 1071 0 of <see cref="Application"/>; each flagged 48, dependent and resolved.
        /// AutoCAD 2021's -XREF Attach of x.dwg, its layer WALL green on DASHED, wrote <c>2 x|DASHED, 70 48</c> and <c>2 x|WALL,
        /// 70 48, 62 3, 6 x|DASHED</c> with <c>1001 AcXRefLayerPropertyOverride, 1071 0</c>. A record the drawing has already
        /// keeps what it has, as AutoCAD 2021's reload keeps it under VISRETAIN 1, and is flagged 48 again; one a reference
        /// nested in the drawing referenced brought (a name with a | in it there) is left out.
        /// </summary>
        internal static bool Bring(DxfDatabase host, string xref, DxfDatabase referenced, bool write)
        {
            bool changes = Resolve(host, xref, true, write);

            foreach (DxfLinetypeTableRecord linetype in referenced.LinetypeTable)
            {
                string own = linetype.Name;

                if (IsAutoCadsLinetype(own) || own.IndexOf('|') >= 0)
                {
                    continue;
                }

                string name = xref + "|" + own;

                if (host.LinetypeTable.Has(name))
                {
                    DxfLinetypeTableRecord kept = host.LinetypeTable[name];
                    changes |= !kept.IsResolved;

                    if (write)
                    {
                        kept.SetDependent(true, true);
                    }

                    continue;
                }

                changes = true;

                if (write)
                {
                    var made = new DxfLinetypeTableRecord { Name = name, AsciiDescription = linetype.AsciiDescription, NumDashes = linetype.NumDashes };

                    // The dashes by their lengths: a shape or a text of a dash is drawn from the drawing referenced, whose text
                    // styles AutoCAD writes no record of here.
                    for (int i = 0; i < made.NumDashes; i++)
                    {
                        made.SetDashLengthAt(i, linetype.DashLengthAt(i));
                    }

                    made.PatternLength = linetype.PatternLength;
                    made.SetDependent(true, true);
                    host.LinetypeTable.Add(made);
                }
            }

            foreach (DxfLayerTableRecord layer in referenced.LayerTable)
            {
                string own = layer.Name;

                if (own == "0" || own.IndexOf('|') >= 0)
                {
                    continue;
                }

                string name = xref + "|" + own;

                if (host.LayerTable.Has(name))
                {
                    DxfLayerTableRecord kept = host.LayerTable[name];
                    changes |= !kept.IsResolved;

                    if (write)
                    {
                        kept.SetDependent(true, true);
                    }

                    continue;
                }

                changes = true;

                if (write)
                {
                    Add(host, xref, name, layer);
                }
            }

            return changes;
        }

        /// <summary>Flags the layers and linetypes of an external reference 16 alone, as AutoCAD 2021 flags them once it is unloaded; gives whether any was flagged 48.</summary>
        internal static bool Unresolve(DxfDatabase host, string xref, bool write)
        {
            string prefix = xref + "|";
            bool changes = Resolve(host, xref, false, write);

            foreach (DxfLinetypeTableRecord linetype in host.LinetypeTable)
            {
                if (linetype.Name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) && linetype.IsDependent && linetype.IsResolved)
                {
                    changes = true;

                    if (write)
                    {
                        linetype.SetDependent(true, false);
                    }
                }
            }

            foreach (DxfLayerTableRecord layer in host.LayerTable)
            {
                if (layer.Name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) && layer.IsDependent && layer.IsResolved)
                {
                    changes = true;

                    if (write)
                    {
                        layer.SetDependent(true, false);
                    }
                }
            }

            return changes;
        }

        /// <summary>Flags the block of an external reference resolved (70 of 36) or not (4), as AutoCAD 2021 writes it loaded and unloaded; gives whether it changes.</summary>
        private static bool Resolve(DxfDatabase host, string xref, bool resolved, bool write)
        {
            DxfBlockTableRecord block = host.BlockTable.Has(xref) ? host.BlockTable[xref] : null;

            if (block == null || !block.IsFromExternalReference || block.IsResolved == resolved)
            {
                return false;
            }

            if (write)
            {
                block.SetXrefResolved(resolved);
            }

            return true;
        }

        /// <summary>Adds the record of a layer of a drawing referenced, as <see cref="Bring"/> says.</summary>
        private static void Add(DxfDatabase host, string xref, string name, DxfLayerTableRecord layer)
        {
            string linetype = layer.LinetypeName ?? "Continuous";

            if (!IsAutoCadsLinetype(linetype))
            {
                linetype = host.LinetypeTable.Has(xref + "|" + linetype) ? xref + "|" + linetype : "Continuous";
            }

            var made = new DxfLayerTableRecord
            {
                Name = name,
                ColorIndex = layer.ColorIndex,
                LinetypeName = linetype,
                IsOff = layer.IsOff,
                IsFrozen = layer.IsFrozen,
                IsLocked = layer.IsLocked,
            };
            made.SetDependent(true, true);
            host.LayerTable.Add(made);
            DxfLayerTableRecord record = host.LayerTable[name];

            // What a drawing of its version holds of the rest, as AutoCAD writes it: lineweights and plotting from R2000,
            // true colours and transparency from R2004; a drawing older keeps none of them.
            Try(() => record.LineWeight = layer.LineWeight, layer.LineWeight != DxfLineWeight.ByLineWeightDefault);
            Try(() => record.IsPlottable = layer.IsPlottable, !layer.IsPlottable);
            Try(() => record.TrueColor = layer.TrueColor, layer.TrueColor.HasValue);
            Try(() => record.Transparency = layer.Transparency, !layer.Transparency.IsSolid);
            Try(() => record.Description = layer.Description, layer.Description.Length > 0);
            record.XData = new[] { new DxfTypedValue(1001, Application), new DxfTypedValue(1071, "0") };
        }

        private static void Try(System.Action set, bool needed)
        {
            if (!needed)
            {
                return;
            }

            try
            {
                set();
            }
            catch (System.InvalidOperationException e)
            {
                GeometryHelperLog.Debug("CadXrefLayers: a property of a layer of an external reference is not written in a drawing of this version: " + e.Message);
            }
        }

        /// <summary>The linetypes every drawing has, which an external reference names as they are.</summary>
        private static bool IsAutoCadsLinetype(string name)
            => string.Equals(name, "ByBlock", System.StringComparison.OrdinalIgnoreCase) || string.Equals(name, "ByLayer", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Continuous", System.StringComparison.OrdinalIgnoreCase);

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
