using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A layer state of a drawing as the Layer States Manager lists it.</summary>
    public sealed class CadLayerStateRow
    {
        internal CadLayerStateRow(string name, string description, bool layout, bool sameAsDrawing, int mask)
        {
            Name = name;
            Description = description;
            IsLayout = layout;
            SameAsDrawing = sameAsDrawing;
            Mask = mask;
        }

        /// <summary>Gets the name, as Plan.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets whether the state was saved in a layout, which the Space column shows as Layout; else Model.</summary>
        public bool IsLayout { get; }

        /// <summary>Gets whether every layer of the drawing is as the state has it, in the properties it restores ("Same as DWG").</summary>
        public bool SameAsDrawing { get; }

        /// <summary>Gets the properties the state restores, as AutoCAD's mask: 1 on, 2 frozen, 4 locked, 8 plot, 0x20 colour and on (see <see cref="CadLayerStates"/>).</summary>
        public int Mask { get; }
    }

    /// <summary>
    /// The layer states of a drawing kept as AutoCAD 2021 keeps them: an XRECORD for each state in the dictionary
    /// ACAD_LAYERSTATES of the layer table's extension dictionary, its mask of the properties it restores (91), its
    /// description (301), a flag (290), the current layer (302), then for each layer, last of the table first, its handle
    /// (330), its flags (90: 1 off, 2 frozen, 4 locked, 8 plotted, 16 frozen in new viewports), its colour (62, with the true
    /// colour in 92 after the rest), lineweight (370), linetype (331), plot style (1) and transparency (440). A state saved
    /// of a drawing of the layers 0 and A, A red, reads 91 2047, then A with 62 1, then 0 with 62 7.
    /// </summary>
    /// <remarks>
    /// The mask's flags are AutoCAD's: 1 on or off, 2 frozen, 4 locked, 8 plot, 0x10 frozen in new viewports, 0x20 colour,
    /// 0x40 linetype, 0x80 lineweight, 0x100 plot style, 0x200 visibility in the current viewport, 0x400 transparency;
    /// 0x8000 marks a state hidden (as the snapshot of a named view, ACAD_VIEWS_name) and 0x10000 the one restored last. A
    /// .las file is the text AutoCAD's Export writes: LAYERSTATEDICTIONARY, then LAYERSTATE and each layer by name (8) with
    /// its linetype by name (6) and its plot style (2).
    /// </remarks>
    internal static class CadLayerStates
    {
        internal const int On = 0x1;
        internal const int Frozen = 0x2;
        internal const int Locked = 0x4;
        internal const int Plot = 0x8;
        internal const int NewViewportFrozen = 0x10;
        internal const int Color = 0x20;
        internal const int Linetype = 0x40;
        internal const int LineWeight = 0x80;
        internal const int PlotStyle = 0x100;
        internal const int CurrentViewport = 0x200;
        internal const int Transparency = 0x400;
        internal const int All = 0x7FF;
        internal const int Hidden = 0x8000;
        internal const int LastRestored = 0x10000;

        /// <summary>The prefix of the hidden state AutoCAD keeps as the layer snapshot of a named view.</summary>
        internal const string ViewPrefix = "ACAD_VIEWS_";

        /// <summary>The properties of -LAYER State's list, in AutoCAD's order and words, each with its flag.</summary>
        internal static readonly KeyValuePair<string, int>[] Properties =
        {
            new KeyValuePair<string, int>("On", On),
            new KeyValuePair<string, int>("Frozen", Frozen),
            new KeyValuePair<string, int>("Lock", Locked),
            new KeyValuePair<string, int>("Plot", Plot),
            new KeyValuePair<string, int>("Newvpfreeze", NewViewportFrozen),
            new KeyValuePair<string, int>("Color", Color),
            new KeyValuePair<string, int>("lineType", Linetype),
            new KeyValuePair<string, int>("lineWeight", LineWeight),
            new KeyValuePair<string, int>("Transparency", Transparency),
            new KeyValuePair<string, int>("plotStyle", PlotStyle),
        };

        /// <summary>The dictionary of the states; null where the drawing has none.</summary>
        internal static DxfDBDictionary Dictionary(DxfDatabase database) => database.LayerStateManager.LayerStatesDictionary;

        /// <summary>The XRECORD of a state, letters of either case the same; null where there is none.</summary>
        internal static DxfXrecord Find(DxfDatabase database, string name)
        {
            DxfDBDictionary states = Dictionary(database);

            if (states == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (DxfDBDictionaryEntry entry in states)
            {
                if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value as DxfXrecord;
                }
            }

            return null;
        }

        /// <summary>The name of a state as the dictionary has it, letters of either case the same; null where there is none.</summary>
        internal static string NameOf(DxfDatabase database, string name)
        {
            DxfDBDictionary states = Dictionary(database);
            return states?.Select(e => e.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The states the manager lists, in ordinal order of their names, letters of either case alike: the hidden ones (a named
        /// view's snapshot) left out, and those of xrefs where <paramref name="includeXref"/> is false.
        /// </summary>
        internal static List<CadLayerStateRow> Rows(DxfDatabase database, bool includeXref)
        {
            var rows = new List<CadLayerStateRow>();
            DxfDBDictionary states = Dictionary(database);

            if (states == null)
            {
                return rows;
            }

            var listed = new HashSet<string>(database.LayerStateManager.GetLayerStateNames(false, includeXref), StringComparer.Ordinal);

            foreach (DxfDBDictionaryEntry entry in states)
            {
                if (!listed.Contains(entry.Key) || !(entry.Value is DxfXrecord record))
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> data = record.Data;
                int mask = MaskOf(data);
                rows.Add(new CadLayerStateRow(entry.Key, Text(data, 301), Whole(data, 290) > 0, Same(database, data, mask), mask));
            }

            rows.Sort((a, b) =>
            {
                int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            });

            return rows;
        }

        /// <summary>Whether a name is one AutoCAD takes for a layer state: not empty, of 255 letters at most, none of &lt;&gt;/\":;?*|,=`.</summary>
        internal static bool IsName(string name) => !string.IsNullOrEmpty(name) && name.Length <= 255 && name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) < 0;

        /// <summary>The mask of a state's data (91); every property where it has none.</summary>
        internal static int MaskOf(IReadOnlyList<DxfTypedValue> data)
        {
            int mask = Whole(data, 91);
            return mask == int.MinValue ? All : mask;
        }

        /// <summary>
        /// Saves the layers of the drawing as they stand into a state of a name, made or written anew: the dictionary
        /// ACAD_LAYERSTATES made where the drawing has none, as AutoCAD makes it at the first state saved.
        /// </summary>
        internal static void Save(DxfDatabase database, string name, string description, int mask)
        {
            DxfXrecord record = Find(database, name);
            List<DxfTypedValue> data = Snapshot(database, mask, description);

            if (record != null)
            {
                record.Data = data;
                return;
            }

            DxfDBDictionary states = Dictionary(database);

            if (states == null)
            {
                database.LayerTable.CreateExtensionDictionary();
                DxfDBDictionary extension = database.LayerTable.ExtensionDictionary;
                extension.SetAt("ACAD_LAYERSTATES", new DxfDBDictionary());
                states = Dictionary(database);
            }

            states.SetAt(name, new DxfXrecord { Data = data });
        }

        /// <summary>The data of a state of the layers as they stand: the mask, description, flag and current layer, then each layer, last first.</summary>
        internal static List<DxfTypedValue> Snapshot(DxfDatabase database, int mask, string description)
        {
            var data = new List<DxfTypedValue>
            {
                new DxfTypedValue(91, Int(mask)),
                new DxfTypedValue(301, description ?? string.Empty),
                new DxfTypedValue(290, "0"),
                new DxfTypedValue(302, database.Clayer ?? "0"),
            };

            foreach (DxfLayerTableRecord layer in database.LayerTable.Reverse())
            {
                if (layer.Handle == null || layer.IsDependent)
                {
                    continue;
                }

                data.Add(new DxfTypedValue(330, layer.Handle));
                AddLayer(data, database, layer.Name, layer);
            }

            return data;
        }

        /// <summary>A layer's properties after its handle or name: 90, 62, 370, 331, 1, 440 and, of a true colour, 92.</summary>
        private static void AddLayer(List<DxfTypedValue> data, DxfDatabase database, string name, DxfLayerTableRecord layer)
        {
            int flags = (layer.IsOff ? 1 : 0) | (layer.IsFrozen ? 2 : 0) | (layer.IsLocked ? 4 : 0) | (layer.IsPlottable ? 8 : 0) | (layer.ViewportVisibilityDefault ? 16 : 0);
            int index = Math.Abs(layer.ColorIndex);
            data.Add(new DxfTypedValue(90, Int(flags)));
            data.Add(new DxfTypedValue(62, Int(index)));
            data.Add(new DxfTypedValue(370, Int((int)layer.LineWeight)));
            string linetype = database.LinetypeTable.Has(layer.LinetypeName) ? database.LinetypeTable[layer.LinetypeName].Handle : null;

            if (linetype != null)
            {
                data.Add(new DxfTypedValue(331, linetype));
            }

            data.Add(new DxfTypedValue(1, "Color_" + Int(index)));
            data.Add(new DxfTypedValue(440, Int(TransparencyValue(layer.Transparency))));

            if (layer.TrueColor.HasValue)
            {
                data.Add(new DxfTypedValue(92, Int(unchecked((int)(0xC2000000u | (uint)(layer.TrueColor.Value & 0xFFFFFF))))));
            }
        }

        /// <summary>A layer's transparency as a state writes it: nought for solid, else 0x020000TT.</summary>
        private static int TransparencyValue(DxfTransparency transparency) => transparency.IsSolid || !transparency.IsByAlpha ? 0 : 0x02000000 | transparency.Alpha;

        /// <summary>The layers of a state, each its name in the drawing (or as the state names it, where the drawing has no such layer) and its groups.</summary>
        internal static List<KeyValuePair<string, List<DxfTypedValue>>> Layers(DxfDatabase database, IReadOnlyList<DxfTypedValue> data)
        {
            var layers = new List<KeyValuePair<string, List<DxfTypedValue>>>();
            List<DxfTypedValue> current = null;

            foreach (DxfTypedValue group in data)
            {
                if (group.TypeCode == 330 || group.TypeCode == 8)
                {
                    string name = group.TypeCode == 8 ? group.Value : LayerOfHandle(database, group.Value);
                    current = new List<DxfTypedValue>();
                    layers.Add(new KeyValuePair<string, List<DxfTypedValue>>(name, current));
                }
                else if (current != null)
                {
                    current.Add(group);
                }
            }

            return layers;
        }

        private static string LayerOfHandle(DxfDatabase database, string handle)
            => database.TryGetObject(handle ?? string.Empty, out DxfDBObject found) && found is DxfLayerTableRecord layer ? layer.Name : null;

        /// <summary>Whether every layer of the state is as the drawing has it, in the properties of the mask.</summary>
        private static bool Same(DxfDatabase database, IReadOnlyList<DxfTypedValue> data, int mask)
        {
            foreach (KeyValuePair<string, List<DxfTypedValue>> layer in Layers(database, data))
            {
                if (layer.Key == null || !database.LayerTable.Has(layer.Key))
                {
                    continue;
                }

                if (Differs(database, database.LayerTable[layer.Key], layer.Value, mask))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Differs(DxfDatabase database, DxfLayerTableRecord layer, List<DxfTypedValue> groups, int mask)
        {
            int flags = Whole(groups, 90);
            flags = flags == int.MinValue ? 8 : flags;

            return ((mask & On) != 0 && layer.IsOff != ((flags & 1) != 0))
                || ((mask & Frozen) != 0 && layer.IsFrozen != ((flags & 2) != 0))
                || ((mask & Locked) != 0 && layer.IsLocked != ((flags & 4) != 0))
                || ((mask & Plot) != 0 && layer.IsPlottable != ((flags & 8) != 0))
                || ((mask & NewViewportFrozen) != 0 && layer.ViewportVisibilityDefault != ((flags & 16) != 0))
                || ((mask & Color) != 0 && !ColorOf(groups).Equals(layer.Color))
                || ((mask & Linetype) != 0 && !string.Equals(LinetypeOf(database, groups) ?? layer.LinetypeName, layer.LinetypeName, StringComparison.OrdinalIgnoreCase))
                || ((mask & LineWeight) != 0 && Whole(groups, 370) != int.MinValue && Whole(groups, 370) != (int)layer.LineWeight)
                || ((mask & Transparency) != 0 && Whole(groups, 440) != int.MinValue && Whole(groups, 440) != TransparencyValue(layer.Transparency));
        }

        private static DxfColor ColorOf(List<DxfTypedValue> groups)
        {
            int rgb = Whole(groups, 92);

            if (rgb != int.MinValue && (rgb & unchecked((int)0xFF000000)) == unchecked((int)0xC2000000))
            {
                return DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }

            int index = Whole(groups, 62);
            return DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(index == int.MinValue || index == 0 ? 7 : Math.Min(255, Math.Abs(index))));
        }

        private static string LinetypeOf(DxfDatabase database, List<DxfTypedValue> groups)
        {
            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 331 && database.TryGetObject(group.Value ?? string.Empty, out DxfDBObject found) && found is DxfLinetypeTableRecord linetype)
                {
                    return linetype.Name;
                }

                if (group.TypeCode == 6 && database.LinetypeTable.Has(group.Value))
                {
                    return database.LinetypeTable[group.Value].Name;
                }
            }

            return null;
        }

        /// <summary>
        /// Restores a state into the layers of the drawing, in the properties of its mask, as AutoCAD's Restore: its current
        /// layer made current again where it is there and not frozen, the layers it does not name turned off where
        /// <paramref name="turnOffMissing"/> says so, and the state marked as the one restored last.
        /// </summary>
        /// <remarks>
        /// A named view's snapshot, a hidden state AutoCAD 2021 saves with the mask 0x8200, is restored in every property, as
        /// AutoCAD restores a view's layers, and marked as AutoCAD 2021 marked it, 98816 (0x18200).
        /// </remarks>
        /// <returns>The names of the layers changed, in ordinal order.</returns>
        internal static List<string> Restore(DxfDatabase database, string name, bool turnOffMissing)
        {
            DxfXrecord record = Find(database, name);
            var changed = new SortedSet<string>(StringComparer.Ordinal);

            if (record == null)
            {
                return changed.ToList();
            }

            IReadOnlyList<DxfTypedValue> data = record.Data;
            int mask = MaskOf(data);
            mask = (mask & Hidden) != 0 ? All : mask;
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = Text(data, 302);
            List<KeyValuePair<string, List<DxfTypedValue>>> layers = Layers(database, data);

            // The current layer first, so that a layer the state freezes is not the current one when it is frozen.
            if (!string.IsNullOrEmpty(current) && database.LayerTable.Has(current))
            {
                List<DxfTypedValue> its = layers.Where(l => string.Equals(l.Key, current, StringComparison.OrdinalIgnoreCase)).Select(l => l.Value).FirstOrDefault();
                int flags = its == null ? 0 : Whole(its, 90);
                bool frozen = (mask & Frozen) != 0 ? flags != int.MinValue && (flags & 2) != 0 : database.LayerTable[current].IsFrozen;

                if (!frozen && !string.Equals(database.Clayer, database.LayerTable[current].Name, StringComparison.Ordinal))
                {
                    database.Clayer = database.LayerTable[current].Name;
                }
            }

            foreach (KeyValuePair<string, List<DxfTypedValue>> layer in layers)
            {
                if (layer.Key == null || !database.LayerTable.Has(layer.Key))
                {
                    continue;
                }

                DxfLayerTableRecord record2 = database.LayerTable[layer.Key];
                named.Add(record2.Name);

                if (Differs(database, record2, layer.Value, mask))
                {
                    Apply(database, record2, layer.Value, mask);
                    changed.Add(record2.Name);
                }
            }

            if (turnOffMissing)
            {
                foreach (DxfLayerTableRecord layer in database.LayerTable)
                {
                    if (!named.Contains(layer.Name) && !layer.IsOff && !layer.IsDependent)
                    {
                        layer.IsOff = true;
                        changed.Add(layer.Name);
                    }
                }
            }

            MarkRestored(database, record);
            return changed.ToList();
        }

        /// <summary>
        /// Restores a state into one viewport of a layout, as AutoCAD's Restore with "Apply properties as viewport overrides":
        /// the layers themselves are left as they are. In the properties of the state's mask, a colour, linetype, lineweight
        /// or transparency of the state that is not the layer's own becomes the layer's override in the viewport, and one
        /// that is the layer's own takes the override off; a layer the state has off or frozen is frozen in the viewport, and
        /// one it has on and thawed is thawed there. Locking, plotting, the freeze in new viewports and the current layer
        /// have no form of one viewport and are not restored. The layers the state does not name are frozen in the viewport
        /// where <paramref name="turnOffMissing"/> says so, and the state is marked as the one restored last.
        /// </summary>
        /// <returns>The names of the layers changed in the viewport, in ordinal order.</returns>
        internal static List<string> RestoreAsOverrides(DxfDatabase database, string name, DxfViewport viewport, bool turnOffMissing)
        {
            DxfXrecord record = Find(database, name);
            var changed = new SortedSet<string>(StringComparer.Ordinal);

            if (record == null)
            {
                return changed.ToList();
            }

            IReadOnlyList<DxfTypedValue> data = record.Data;
            int mask = MaskOf(data);
            mask = (mask & Hidden) != 0 ? All : mask;
            bool trueColors = string.CompareOrdinal(database.Version, "AC1018") >= 0;
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var freeze = new List<DxfLayerTableRecord>();
            var thaw = new List<DxfLayerTableRecord>();

            foreach (KeyValuePair<string, List<DxfTypedValue>> pair in Layers(database, data))
            {
                if (pair.Key == null || !database.LayerTable.Has(pair.Key))
                {
                    continue;
                }

                DxfLayerTableRecord layer = database.LayerTable[pair.Key];
                List<DxfTypedValue> groups = pair.Value;
                named.Add(layer.Name);
                DxfLayerViewportProperties there = layer.GetViewportOverrides(viewport);
                bool touched = false;

                if ((mask & Color) != 0)
                {
                    DxfColor color = ColorOf(groups);

                    if (color.Equals(layer.Color))
                    {
                        touched |= there.IsColorOverridden;
                        there.RemoveColorOverride();
                    }
                    else if ((trueColors || !color.IsByColor) && !(there.IsColorOverridden && color.Equals(there.Color)))
                    {
                        there.Color = color;
                        touched = true;
                    }
                }

                string linetype = LinetypeOf(database, groups);

                if ((mask & Linetype) != 0 && linetype != null)
                {
                    if (string.Equals(linetype, layer.LinetypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        touched |= there.IsLinetypeOverridden;
                        there.RemoveLinetypeOverride();
                    }
                    else if (!(there.IsLinetypeOverridden && string.Equals(linetype, there.Linetype, StringComparison.OrdinalIgnoreCase)))
                    {
                        there.Linetype = linetype;
                        touched = true;
                    }
                }

                int weight = Whole(groups, 370);

                if ((mask & LineWeight) != 0 && weight != int.MinValue && Enum.IsDefined(typeof(DxfLineWeight), weight) && weight != (int)DxfLineWeight.ByLayer && weight != (int)DxfLineWeight.ByBlock
                    && weight != (int)DxfLineWeight.ByDIPs)
                {
                    if (weight == (int)layer.LineWeight)
                    {
                        touched |= there.IsLineWeightOverridden;
                        there.RemoveLineWeightOverride();
                    }
                    else if (!(there.IsLineWeightOverridden && weight == (int)there.LineWeight))
                    {
                        there.LineWeight = (DxfLineWeight)weight;
                        touched = true;
                    }
                }

                int transparency = Whole(groups, 440);

                if ((mask & Transparency) != 0 && transparency != int.MinValue && trueColors)
                {
                    var wanted = (transparency & 0x02000000) != 0 ? new DxfTransparency((byte)(transparency & 0xFF)) : new DxfTransparency(255);

                    if (TransparencyValue(wanted) == TransparencyValue(layer.Transparency))
                    {
                        touched |= there.IsTransparencyOverridden;
                        there.RemoveTransparencyOverride();
                    }
                    else if (!(there.IsTransparencyOverridden && TransparencyValue(wanted) == TransparencyValue(there.Transparency)))
                    {
                        there.Transparency = wanted;
                        touched = true;
                    }
                }

                if ((mask & (On | Frozen)) != 0)
                {
                    int flags = Whole(groups, 90);
                    flags = flags == int.MinValue ? 8 : flags;
                    bool hidden = ((mask & On) != 0 && (flags & 1) != 0) || ((mask & Frozen) != 0 && (flags & 2) != 0);

                    if (hidden != viewport.IsLayerFrozenInViewport(layer))
                    {
                        (hidden ? freeze : thaw).Add(layer);
                        touched = true;
                    }
                }

                if (touched)
                {
                    changed.Add(layer.Name);
                }
            }

            if (turnOffMissing)
            {
                foreach (DxfLayerTableRecord layer in database.LayerTable)
                {
                    if (!named.Contains(layer.Name) && !layer.IsDependent && !viewport.IsLayerFrozenInViewport(layer))
                    {
                        freeze.Add(layer);
                        changed.Add(layer.Name);
                    }
                }
            }

            if (freeze.Count > 0)
            {
                viewport.FreezeLayersInViewport(freeze);
            }

            if (thaw.Count > 0)
            {
                viewport.ThawLayersInViewport(thaw);
            }

            MarkRestored(database, record);
            return changed.ToList();
        }

        private static void Apply(DxfDatabase database, DxfLayerTableRecord layer, List<DxfTypedValue> groups, int mask)
        {
            int flags = Whole(groups, 90);
            flags = flags == int.MinValue ? 8 : flags;
            bool current = string.Equals(layer.Name, database.Clayer, StringComparison.OrdinalIgnoreCase);

            if ((mask & Color) != 0)
            {
                DxfColor color = ColorOf(groups);

                if (!color.Equals(layer.Color))
                {
                    layer.Color = color;
                }
            }

            if ((mask & On) != 0)
            {
                layer.IsOff = (flags & 1) != 0;
            }

            if ((mask & Frozen) != 0 && !(current && (flags & 2) != 0))
            {
                layer.IsFrozen = (flags & 2) != 0;
            }

            if ((mask & Locked) != 0)
            {
                layer.IsLocked = (flags & 4) != 0;
            }

            if ((mask & Plot) != 0)
            {
                layer.IsPlottable = (flags & 8) != 0;
            }

            if ((mask & NewViewportFrozen) != 0)
            {
                layer.ViewportVisibilityDefault = (flags & 16) != 0;
            }

            string linetype = LinetypeOf(database, groups);

            if ((mask & Linetype) != 0 && linetype != null)
            {
                layer.LinetypeName = linetype;
            }

            int weight = Whole(groups, 370);

            if ((mask & LineWeight) != 0 && weight != int.MinValue && Enum.IsDefined(typeof(DxfLineWeight), weight) && weight != (int)DxfLineWeight.ByLayer && weight != (int)DxfLineWeight.ByBlock)
            {
                layer.LineWeight = (DxfLineWeight)weight;
            }

            int transparency = Whole(groups, 440);

            if ((mask & Transparency) != 0 && transparency != int.MinValue)
            {
                layer.Transparency = (transparency & 0x02000000) != 0 ? new DxfTransparency((byte)(transparency & 0xFF)) : new DxfTransparency(255);
            }
        }

        /// <summary>Marks a state as the one restored last (0x10000 of its mask), the mark taken off every other.</summary>
        private static void MarkRestored(DxfDatabase database, DxfXrecord restored)
        {
            foreach (DxfDBDictionaryEntry entry in Dictionary(database))
            {
                if (entry.Value is DxfXrecord record)
                {
                    int mask = MaskOf(record.Data);
                    int wanted = ReferenceEquals(record, restored) || record.Handle == restored.Handle ? mask | LastRestored : mask & ~LastRestored;

                    if (wanted != mask)
                    {
                        SetMask(record, wanted);
                    }
                }
            }
        }

        /// <summary>A state's mask written, its other groups kept.</summary>
        internal static void SetMask(DxfXrecord record, int mask) => Replace(record, 91, Int(mask));

        /// <summary>A state's description written, its other groups kept.</summary>
        internal static void SetDescription(DxfXrecord record, string description) => Replace(record, 301, description ?? string.Empty);

        private static void Replace(DxfXrecord record, int code, string value)
        {
            var data = new List<DxfTypedValue>(record.Data);
            int at = data.FindIndex(g => g.TypeCode == code);

            if (at >= 0)
            {
                data[at] = new DxfTypedValue(code, value);
            }
            else
            {
                data.Insert(code == 91 ? 0 : Math.Min(1, data.Count), new DxfTypedValue(code, value));
            }

            record.Data = data;
        }

        /// <summary>Deletes a state; false where there is none.</summary>
        internal static bool Delete(DxfDatabase database, string name)
        {
            string key = NameOf(database, name);

            if (key == null)
            {
                return false;
            }

            Dictionary(database).Remove(key);
            return true;
        }

        /// <summary>Renames a state; false where there is none.</summary>
        internal static bool Rename(DxfDatabase database, string name, string newName)
        {
            string key = NameOf(database, name);

            if (key == null)
            {
                return false;
            }

            Dictionary(database).SetName(key, newName);
            return true;
        }

        /// <summary>The text of a .las file of one state, as AutoCAD 2021's Export writes it: each code right aligned in three places, lines ending CR LF.</summary>
        internal static string ExportText(DxfDatabase database, string name)
        {
            DxfXrecord record = Find(database, name);
            var text = new StringBuilder();
            Pair(text, 0, "LAYERSTATEDICTIONARY");
            Pair(text, 0, "LAYERSTATE");
            Pair(text, 1, NameOf(database, name));
            IReadOnlyList<DxfTypedValue> data = record.Data;
            Pair(text, 91, Int(MaskOf(data) & ~LastRestored));
            Pair(text, 301, Text(data, 301));
            Pair(text, 290, Whole(data, 290) == int.MinValue ? "0" : Int(Whole(data, 290)));
            Pair(text, 302, Text(data, 302));

            foreach (KeyValuePair<string, List<DxfTypedValue>> layer in Layers(database, data))
            {
                if (layer.Key == null)
                {
                    continue;
                }

                Pair(text, 8, layer.Key);
                string truecolor = null;

                foreach (DxfTypedValue group in layer.Value)
                {
                    switch (group.TypeCode)
                    {
                        case 331:
                            Pair(text, 6, LinetypeOf(database, new List<DxfTypedValue> { group }) ?? "Continuous");
                            break;
                        case 1:
                            Pair(text, 2, group.Value);
                            break;
                        case 92:
                            truecolor = group.Value;
                            break;
                        default:
                            Pair(text, group.TypeCode, group.Value);
                            break;
                    }
                }

                if (truecolor != null)
                {
                    Pair(text, 92, truecolor);
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// The states of a .las file read into the drawing, each written anew where the drawing has one of its name: its layers
        /// named by handle where the drawing has them, else by name. Null where the file does not read as one.
        /// </summary>
        /// <returns>The names of the states read, in the file's order; null where the text is not a .las file.</returns>
        internal static List<string> Import(DxfDatabase database, string text)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var pairs = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    return null;
                }

                pairs.Add(new KeyValuePair<int, string>(code, lines[i + 1]));
            }

            if (pairs.Count == 0 || pairs[0].Key != 0 || pairs[0].Value.Trim() != "LAYERSTATEDICTIONARY")
            {
                return null;
            }

            var names = new List<string>();
            int at = 1;

            while (at < pairs.Count)
            {
                if (pairs[at].Key != 0 || pairs[at].Value.Trim() != "LAYERSTATE")
                {
                    at++;
                    continue;
                }

                at++;
                string name = null;
                var head = new List<DxfTypedValue>();
                var body = new List<DxfTypedValue>();

                for (; at < pairs.Count && pairs[at].Key != 0; at++)
                {
                    int code = pairs[at].Key;
                    string value = pairs[at].Value;

                    if (code == 1 && name == null)
                    {
                        name = value.Trim();
                    }
                    else if (code == 91 || code == 301 || code == 290 || code == 302)
                    {
                        head.Add(new DxfTypedValue(code, value));
                    }
                    else if (code == 8)
                    {
                        body.Add(database.LayerTable.Has(value) ? new DxfTypedValue(330, database.LayerTable[value].Handle) : new DxfTypedValue(8, value));
                    }
                    else if (code == 6)
                    {
                        string linetype = database.LinetypeTable.Has(value) ? value : "Continuous";

                        if (database.LinetypeTable.Has(linetype))
                        {
                            body.Add(new DxfTypedValue(331, database.LinetypeTable[linetype].Handle));
                        }
                    }
                    else if (code == 2)
                    {
                        body.Add(new DxfTypedValue(1, value));
                    }
                    else if (code == 90 || code == 62 || code == 370 || code == 440 || code == 92)
                    {
                        body.Add(new DxfTypedValue(code, value));
                    }
                }

                if (!IsName(name))
                {
                    continue;
                }

                head.Sort((a, b) => Order(a.TypeCode).CompareTo(Order(b.TypeCode)));
                var data = new List<DxfTypedValue>(head);
                data.AddRange(body);

                if (Find(database, name) != null)
                {
                    Find(database, name).Data = data;
                }
                else
                {
                    Save(database, name, string.Empty, All);
                    Find(database, name).Data = data;
                }

                names.Add(NameOf(database, name));
            }

            return names;
        }

        private static int Order(int code) => code == 91 ? 0 : code == 301 ? 1 : code == 290 ? 2 : 3;

        /// <summary>A file name given for a .las file: its extension added where it has none, a relative one beside the drawing.</summary>
        internal static string LasPath(CadDocument document, string typed)
        {
            string name = (typed ?? string.Empty).Trim().Trim('"');
            name = Path.HasExtension(name) ? name : name + ".las";

            if (!Path.IsPathRooted(name) && document.HasFile)
            {
                name = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(document.FileName)), name);
            }

            return name;
        }

        private static void Pair(StringBuilder text, int code, string value)
            => text.Append(code.ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append("\r\n").Append(value ?? string.Empty).Append("\r\n");

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        internal static string Text(IReadOnlyList<DxfTypedValue> data, int code)
        {
            foreach (DxfTypedValue group in data)
            {
                if (group.TypeCode == code)
                {
                    return group.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>The first whole number of a code; <see cref="int.MinValue"/> where there is none.</summary>
        internal static int Whole(IReadOnlyList<DxfTypedValue> data, int code)
        {
            foreach (DxfTypedValue group in data)
            {
                if (group.TypeCode == code)
                {
                    return int.TryParse((group.Value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : int.MinValue;
                }
            }

            return int.MinValue;
        }
    }
}
