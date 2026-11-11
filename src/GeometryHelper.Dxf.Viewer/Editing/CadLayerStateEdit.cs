using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A layer of a layer state as AutoCAD's Edit Layer State lists it: the properties the state saved for it, each of which
    /// the dialog may change. The state is changed by OK; the drawing's layers are changed only when the state is restored.
    /// </summary>
    public sealed class CadLayerStateLayer
    {
        internal CadLayerStateLayer(string name)
        {
            Name = name;
        }

        /// <summary>Gets the layer's name; for a layer the drawing no longer has, the name the state keeps, or its handle.</summary>
        public string Name { get; }

        /// <summary>Gets whether the drawing has no layer of the state's: kept in the state, passed over on restore.</summary>
        public bool IsMissing { get; internal set; }

        /// <summary>Gets or sets whether the state turns the layer on.</summary>
        public bool IsOn { get; set; } = true;

        /// <summary>Gets or sets whether the state freezes the layer.</summary>
        public bool IsFrozen { get; set; }

        /// <summary>Gets or sets whether the state locks the layer.</summary>
        public bool IsLocked { get; set; }

        /// <summary>Gets or sets whether the state plots the layer.</summary>
        public bool IsPlotted { get; set; } = true;

        /// <summary>Gets or sets whether the state freezes the layer in new viewports.</summary>
        public bool IsNewViewportFrozen { get; set; }

        /// <summary>Gets or sets the colour: one of the index, or a true colour.</summary>
        public DxfColor Color { get; set; } = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

        /// <summary>Gets or sets the linetype's name, one of the drawing's.</summary>
        public string Linetype { get; set; } = "Continuous";

        /// <summary>Gets or sets the lineweight: Default, or one of AutoCAD's widths.</summary>
        public DxfLineWeight LineWeight { get; set; } = DxfLineWeight.ByLineWeightDefault;

        /// <summary>Gets or sets the transparency, 0 to 90 per cent.</summary>
        public int Transparency { get; set; }

        /// <summary>The group naming the layer in the state: 330 and a handle, or 8 and a name.</summary>
        internal DxfTypedValue Key { get; set; }

        /// <summary>The groups the state had for the layer, written back as they were for a layer the drawing has not.</summary>
        internal List<DxfTypedValue> Groups { get; set; } = new List<DxfTypedValue>();

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Edit Layer State of a state: the layers it saved with their properties, those of the drawing not in it, which
    /// Add layers puts in, and the properties it restores. <see cref="CadLayerStatesRequest.Edit"/> writes it into the state as
    /// an edit of LAYERSTATE's one undo step; the drawing's layers are left as they are.
    /// </summary>
    public sealed class CadLayerStateEdit
    {
        private readonly DxfDatabase _database;

        internal CadLayerStateEdit(DxfDatabase database, string name, int mask, List<CadLayerStateLayer> layers)
        {
            _database = database;
            Name = name;
            Mask = mask;
            Layers = layers;
        }

        /// <summary>Gets the state's name.</summary>
        public string Name { get; }

        /// <summary>Gets or sets the properties the state restores, as AutoCAD's mask (1 on, 2 frozen, 4 locked, 8 plot, 0x20 colour and on).</summary>
        public int Mask { get; set; }

        /// <summary>Gets the layers of the state, in the order of the Layer manager: by name, letters of either case alike.</summary>
        public List<CadLayerStateLayer> Layers { get; }

        /// <summary>Gets the drawing's layers the state does not name, by name, which Add layers offers; a layer of an xref is not one.</summary>
        /// <returns>The names.</returns>
        public IReadOnlyList<string> LayersNotInState()
        {
            var listed = new HashSet<string>(Layers.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            return _database
                .LayerTable.Where(l => !l.IsDependent && !listed.Contains(l.Name))
                .Select(l => l.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Puts a layer of the drawing into the state with its properties as they stand, as Add layers does.</summary>
        /// <param name="name">The layer's name.</param>
        /// <returns>The layer put in; null where the drawing has no such layer or the state names it already.</returns>
        public CadLayerStateLayer Add(string name)
        {
            if (
                string.IsNullOrEmpty(name)
                || !_database.LayerTable.Has(name)
                || Layers.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
            )
            {
                return null;
            }

            DxfLayerTableRecord record = _database.LayerTable[name];

            if (record.IsDependent)
            {
                return null;
            }

            var layer = new CadLayerStateLayer(record.Name)
            {
                IsOn = !record.IsOff,
                IsFrozen = record.IsFrozen,
                IsLocked = record.IsLocked,
                IsPlotted = record.IsPlottable,
                IsNewViewportFrozen = record.ViewportVisibilityDefault,
                Color = record.Color,
                Linetype = record.LinetypeName,
                LineWeight = record.LineWeight,
                Transparency = Percent(CadLayerStates.TransparencyValue(record.Transparency)),
                Key = new DxfTypedValue(330, record.Handle),
            };
            Layers.Add(layer);
            Sort(Layers);
            return layer;
        }

        /// <summary>Whether a layer of the state differs from the drawing's layer of the name, in a property the state restores; false for one the drawing has not.</summary>
        /// <param name="layer">The layer of the state.</param>
        /// <returns>True when it differs.</returns>
        public bool Differs(CadLayerStateLayer layer)
        {
            if (layer == null || layer.IsMissing || !_database.LayerTable.Has(layer.Name))
            {
                return false;
            }

            DxfLayerTableRecord record = _database.LayerTable[layer.Name];
            return ((Mask & CadLayerStates.On) != 0 && record.IsOff == layer.IsOn)
                || ((Mask & CadLayerStates.Frozen) != 0 && record.IsFrozen != layer.IsFrozen)
                || ((Mask & CadLayerStates.Locked) != 0 && record.IsLocked != layer.IsLocked)
                || ((Mask & CadLayerStates.Plot) != 0 && record.IsPlottable != layer.IsPlotted)
                || (
                    (Mask & CadLayerStates.NewViewportFrozen) != 0
                    && record.ViewportVisibilityDefault != layer.IsNewViewportFrozen
                )
                || ((Mask & CadLayerStates.Color) != 0 && !record.Color.Equals(layer.Color))
                || (
                    (Mask & CadLayerStates.Linetype) != 0
                    && !string.Equals(record.LinetypeName, layer.Linetype, StringComparison.OrdinalIgnoreCase)
                )
                || ((Mask & CadLayerStates.LineWeight) != 0 && record.LineWeight != layer.LineWeight)
                || (
                    (Mask & CadLayerStates.Transparency) != 0
                    && Percent(CadLayerStates.TransparencyValue(record.Transparency)) != layer.Transparency
                );
        }

        /// <summary>The layers of a state read from its data, by name, those the drawing has not marked missing.</summary>
        internal static CadLayerStateEdit Read(DxfDatabase database, string name, DxfXrecord record)
        {
            var layers = new List<CadLayerStateLayer>();
            CadLayerStateLayer current = null;

            foreach (DxfTypedValue group in record.Data)
            {
                if (group.TypeCode == 330 || group.TypeCode == 8)
                {
                    string layerName =
                        group.TypeCode == 8 ? group.Value
                        : database.TryGetObject(group.Value ?? string.Empty, out DxfDBObject found)
                        && found is DxfLayerTableRecord named
                            ? named.Name
                        : null;
                    current = new CadLayerStateLayer(layerName ?? group.Value) { Key = group };
                    current.IsMissing = layerName == null || !database.LayerTable.Has(layerName);
                    layers.Add(current);
                }
                else if (current != null)
                {
                    current.Groups.Add(group);
                }
            }

            foreach (CadLayerStateLayer layer in layers)
            {
                int flags = CadLayerStates.Whole(layer.Groups, 90);
                flags = flags == int.MinValue ? 8 : flags;
                layer.IsOn = (flags & 1) == 0;
                layer.IsFrozen = (flags & 2) != 0;
                layer.IsLocked = (flags & 4) != 0;
                layer.IsPlotted = (flags & 8) != 0;
                layer.IsNewViewportFrozen = (flags & 16) != 0;
                layer.Color = CadLayerStates.ColorOf(layer.Groups);
                layer.Linetype = CadLayerStates.LinetypeOf(database, layer.Groups) ?? "Continuous";
                int weight = CadLayerStates.Whole(layer.Groups, 370);
                layer.LineWeight =
                    weight != int.MinValue && Enum.IsDefined(typeof(DxfLineWeight), weight)
                        ? (DxfLineWeight)weight
                        : DxfLineWeight.ByLineWeightDefault;
                int transparency = CadLayerStates.Whole(layer.Groups, 440);
                layer.Transparency = transparency == int.MinValue ? 0 : Percent(transparency);
            }

            Sort(layers);
            return new CadLayerStateEdit(database, name, CadLayerStates.MaskOf(record.Data), layers);
        }

        /// <summary>
        /// The state's data written from the edit: its own groups before the layers kept, the mask set, then each layer, last by
        /// name first as AutoCAD writes them, by its handle (or the name of a state read from a .las file); a layer the drawing
        /// has not is written back as the state had it.
        /// </summary>
        internal List<DxfTypedValue> Data(DxfXrecord record)
        {
            var data = new List<DxfTypedValue>();

            foreach (DxfTypedValue group in record.Data)
            {
                if (group.TypeCode == 330 || group.TypeCode == 8)
                {
                    break;
                }

                data.Add(
                    group.TypeCode == 91 ? new DxfTypedValue(91, Mask.ToString(CultureInfo.InvariantCulture)) : group
                );
            }

            for (int i = Layers.Count - 1; i >= 0; i--)
            {
                CadLayerStateLayer layer = Layers[i];
                data.Add(layer.Key);

                if (layer.IsMissing)
                {
                    data.AddRange(layer.Groups);
                    continue;
                }

                int flags =
                    (layer.IsOn ? 0 : 1)
                    | (layer.IsFrozen ? 2 : 0)
                    | (layer.IsLocked ? 4 : 0)
                    | (layer.IsPlotted ? 8 : 0)
                    | (layer.IsNewViewportFrozen ? 16 : 0);
                int index = Math.Max(1, Math.Min(255, (int)Math.Abs(layer.Color.ColorIndex)));
                data.Add(new DxfTypedValue(90, Int(flags)));
                data.Add(new DxfTypedValue(62, Int(index)));
                data.Add(new DxfTypedValue(370, Int((int)layer.LineWeight)));

                if (_database.LinetypeTable.Has(layer.Linetype))
                {
                    DxfLinetypeTableRecord linetype = _database.LinetypeTable[layer.Linetype];
                    data.Add(
                        layer.Key.TypeCode == 8
                            ? new DxfTypedValue(6, linetype.Name)
                            : new DxfTypedValue(331, linetype.Handle)
                    );
                }

                data.Add(new DxfTypedValue(layer.Key.TypeCode == 8 ? 2 : 1, "Color_" + Int(index)));
                int alpha = (int)Math.Floor(255.0 * (100 - layer.Transparency) / 100.0);
                data.Add(new DxfTypedValue(440, Int(layer.Transparency == 0 ? 0 : 0x02000000 | alpha)));

                if (layer.Color.IsByColor)
                {
                    int rgb = (layer.Color.Red << 16) | (layer.Color.Green << 8) | layer.Color.Blue;
                    data.Add(new DxfTypedValue(92, Int(unchecked((int)(0xC2000000u | (uint)rgb)))));
                }
            }

            return data;
        }

        /// <summary>A state's transparency (0, or 0x020000TT) as the per cent the dialog shows: 0x0200007F is 50.</summary>
        private static int Percent(int value)
        {
            if (value == 0 || (value & 0x02000000) == 0)
            {
                return 0;
            }

            int alpha = value & 0xFF;
            return Math.Max(0, Math.Min(90, (int)Math.Round((255 - alpha) * 100.0 / 255.0)));
        }

        private static void Sort(List<CadLayerStateLayer> layers) =>
            layers.Sort(
                (a, b) =>
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                    return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
                }
            );

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
