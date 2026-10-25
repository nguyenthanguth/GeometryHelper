using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The layer table of a DXF file, as AutoCAD's <c>LayerTable</c>: its layers in the order of the file, each a
    /// <see cref="DxfLayerTableRecord"/>, found by name and added to.
    /// </summary>
    /// <remarks>
    /// <c>db.LayerTable.Add(new DxfLayerTableRecord { Name = "steel", ColorIndex = 1 })</c> adds the layer steel in red, with
    /// the linetype Continuous; <c>db.LayerTable["STEEL"]</c> finds it. A layer an entity is set on that the table does not
    /// list is added to it too, as <see cref="DxfEntity.Layer"/> says.
    /// </remarks>
    public sealed class DxfLayerTable : DxfSymbolTable, IEnumerable<DxfLayerTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfLayerTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>
        /// Gets the layer of a name, letters of either case the same.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The layer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no layer of the name.</exception>
        public new DxfLayerTableRecord this[string key] => (DxfLayerTableRecord)base[key];

        /// <summary>Gets an enumerator over the layers, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfLayerTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("LAYER").ToArray())
            {
                yield return DxfLayerTableRecord.Of(item);
            }
        }

        IEnumerator<DxfLayerTableRecord> IEnumerable<DxfLayerTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfLayerTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfLayerTableRecord layer))
            {
                throw new ArgumentException("The layer table takes a DxfLayerTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            layer.AddTo(_file);
        }

        /// <summary>
        /// Works out which layers are in use, as AutoCAD's <c>GenerateUsageData</c> does before <c>IsUsed</c> is read: the layers
        /// every entity is on, in one walk of the drawing, kept until the drawing next changes;
        /// <see cref="DxfLayerTableRecord.IsUsed"/> works them out itself where they are not kept.
        /// </summary>
        public void GenerateUsageData() => _file.LayersDrawnOn();
    }

    /// <summary>
    /// A layer of a DXF file, as AutoCAD's <c>LayerTableRecord</c>: its name, the colour, linetype and lineweight its
    /// entities take where they say BYLAYER, and whether it is off, frozen, locked and plotted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A layer of the file is read from its LAYER record and changed by its setters, its record written anew, every other
    /// group kept as it was; a setter given the value its getter gives changes nothing. The layer steel in red, turned off,
    /// is written as the colour -1: it is <see cref="ColorIndex"/> 1 and <see cref="IsOff"/> true.
    /// </para>
    /// <para>
    /// A layer made in code is in the colour 7, with the linetype Continuous, its lineweight the default, on, thawed,
    /// unlocked and plotted until it is given others, and is written when it is added to a <see cref="DxfLayerTable"/>.
    /// </para>
    /// <para>
    /// A layer of a file is renamed by its <see cref="DxfSymbolTableRecord.Name"/>, every reference to it with it, and erased
    /// by <see cref="Erase"/> where nothing is drawn on it: the layer steel renamed Steel_S355 has every entity of it, in model
    /// space, paper space and the blocks, its vertices and attributes among them, on Steel_S355, and <c>$CLAYER</c> too where
    /// it was the current layer.
    /// </para>
    /// </remarks>
    public sealed class DxfLayerTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbLayerTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 62, 420, 6, 290, 370, 390, 347 });

        // The sections whose records name a layer by name.
        private static readonly string[] Naming = { "TABLES", "ENTITIES", "BLOCKS", "OBJECTS" };

        private const string DescriptionApplication = "AcAecLayerStandard";
        private const string TransparencyApplication = "AcCmTransparency";

        private int _colour = 7;
        private int? _trueColour;
        private string _linetype = "Continuous";
        private DxfLineWeight _lineWeight = DxfLineWeight.ByLineWeightDefault;
        private bool _off;
        private bool _frozen;
        private bool _locked;
        private bool _plotted = true;
        private bool _frozenInNewViewports;

        /// <summary>
        /// Initializes a layer made in code, to be added to a <see cref="DxfLayerTable"/> with its <see cref="DxfSymbolTableRecord.Name"/> given.
        /// </summary>
        public DxfLayerTableRecord()
        {
        }

        private DxfLayerTableRecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the colour of the layer (group 62), as AutoCAD's <c>Color</c> by its index: 1 to 255, whether the layer
        /// is on or off; 7 where the record gives none or one outside that.
        /// </summary>
        /// <value>Set, from 1 to 255; the layer stays on or off as it was.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour set is not from 1 to 255.</exception>
        public int ColorIndex
        {
            get
            {
                if (Item == null)
                {
                    return _colour;
                }

                int colour = Math.Abs(Whole(62, 7));
                return colour >= 1 && colour <= 255 ? colour : 7;
            }

            set
            {
                if (Item != null && value == ColorIndex)
                {
                    return;
                }

                if (value < 1 || value > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The colour of a layer is an AutoCAD colour index from 1 to 255.");
                }

                if (Item == null)
                {
                    _colour = value;
                }
                else if (value != ColorIndex)
                {
                    Set(62, (IsOff ? -value : value).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the layer, as AutoCAD's <c>Color</c>: its true colour (420, its book's name 430) where it
        /// has one, else the colour of its index (62), whether the layer is on or off. It agrees with <see cref="ColorIndex"/>
        /// and <see cref="TrueColor"/>; a true colour carries the index nearest it, as <see cref="DxfColor.FromRgb"/> gives it.
        /// </summary>
        /// <value>Set, a colour of the index, which takes out a true colour, or a true colour, written with the index nearest it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by layer, by block or another a layer does not take.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a true colour is set in a file older than R2004 (AC1018).</exception>
        public DxfColor Color
        {
            get
            {
                int? colour = TrueColor;
                return colour.HasValue ? DxfColor.OfRgb(colour.Value, Item == null ? null : Item.File.ValueOf(Item, 430)) : DxfColor.OfIndex(ColorIndex);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Equals(Color))
                {
                    return;
                }

                if (value.ColorMethod != DxfColorMethod.ByAci && value.ColorMethod != DxfColorMethod.ByColor)
                {
                    throw new ArgumentException("A layer is of a colour of the index or a true colour: " + value.ColorMethod + " is neither.", nameof(value));
                }

                TrueColor = value.IsByColor ? value.ColorValue : (int?)null;

                if (Item != null)
                {
                    Set(430, null);
                }

                ColorIndex = value.ColorIndex;
            }
        }

        /// <summary>
        /// Gets or sets the colour of the layer as red, green and blue (group 420), 0xRRGGBB; null where it has none.
        /// </summary>
        /// <value>Set, from 0 to 0xFFFFFF; null takes it out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour set is below 0 or above 0xFFFFFF.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no true colours.</exception>
        public int? TrueColor
        {
            get
            {
                if (Item == null)
                {
                    return _trueColour;
                }

                string text = Item.File.ValueOf(Item, 420);
                return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int colour) ? colour & 0xFFFFFF : (int?)null;
            }

            set
            {
                if (Item != null && value == TrueColor)
                {
                    return;
                }

                if (value.HasValue && (value.Value < 0 || value.Value > 0xFFFFFF))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A true colour is red, green and blue, 0 to 0xFFFFFF.");
                }

                if (Item == null)
                {
                    _trueColour = value;
                }
                else if (value != TrueColor)
                {
                    Since(1018, "true colours", "R2004 (AC1018)", value.HasValue);
                    Set(420, value?.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the layer's linetype (group 6), as the file spells it; Continuous where the record gives none.
        /// </summary>
        /// <value>Set, a linetype of the file's linetype table, letters of either case the same.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when set on a layer of a file whose linetype table does not list it.</exception>
        public string LinetypeName
        {
            get
            {
                if (Item == null)
                {
                    return _linetype;
                }

                string name = DxfReader.NameOf(Item.File.ValueOf(Item, 6) ?? string.Empty);
                return name.Length == 0 ? "Continuous" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (Item == null)
                {
                    _linetype = value;
                }
                else if (!string.Equals(value, LinetypeName, StringComparison.Ordinal))
                {
                    if (!Item.File.LinetypeListed(value))
                    {
                        throw new ArgumentException("The linetype " + value + " is not in the file's linetype table.", nameof(value));
                    }

                    Set(6, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the lineweight of the layer (group 370); <see cref="DxfLineWeight.ByLineWeightDefault"/> where the
        /// record gives none.
        /// </summary>
        /// <value>Set, one of AutoCAD's weights or the default; a layer takes no weight by layer or by block.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is by layer, by block, or not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no lineweights.</exception>
        public DxfLineWeight LineWeight
        {
            get => Item == null ? _lineWeight : (DxfLineWeight)Whole(370, -3);

            set
            {
                // The weight it has changes nothing, whatever the file holds.
                if (Item != null && value == LineWeight)
                {
                    return;
                }

                if (value == DxfLineWeight.ByLayer || value == DxfLineWeight.ByBlock || (value != DxfLineWeight.ByLineWeightDefault && !DxfShape.IsLineweight((int)value)))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The lineweight of a layer is one of AutoCAD's, or the default.");
                }

                if (Item == null)
                {
                    _lineWeight = value;
                }
                else if (value != LineWeight)
                {
                    Since(1015, "lineweights", "R2000 (AC1015)", true);
                    Set(370, ((int)value).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is turned off, which the table writes as a colour below nought: its entities are not
        /// drawn, and are still regenerated.
        /// </summary>
        public bool IsOff
        {
            get => Item == null ? _off : Whole(62, 7) < 0;

            set
            {
                if (Item == null)
                {
                    _off = value;
                }
                else if (value != IsOff)
                {
                    int colour = ColorIndex;
                    Set(62, (value ? -colour : colour).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>Gets or sets whether the layer is frozen (flag 1 of group 70): its entities are not drawn.</summary>
        public bool IsFrozen
        {
            get => Item == null ? _frozen : (Flags & 1) != 0;

            set
            {
                if (Item == null)
                {
                    _frozen = value;
                }
                else if (value != IsFrozen)
                {
                    SetFlag(1, value);
                }
            }
        }

        /// <summary>Gets or sets whether the layer is locked (flag 4 of group 70): its entities are drawn and cannot be changed in AutoCAD.</summary>
        public bool IsLocked
        {
            get => Item == null ? _locked : (Flags & 4) != 0;

            set
            {
                if (Item == null)
                {
                    _locked = value;
                }
                else if (value != IsLocked)
                {
                    SetFlag(4, value);
                }
            }
        }

        /// <summary>Gets or sets whether the layer is plotted (group 290); true where the record does not say.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set false in a file older than R2000 (AC1015), which plots every layer.</exception>
        public bool IsPlottable
        {
            get => Item == null ? _plotted : Whole(290, 1) != 0;

            set
            {
                if (Item == null)
                {
                    _plotted = value;
                }
                else if (value != IsPlottable)
                {
                    Since(1015, "layers left out of plotting", "R2000 (AC1015)", !value);
                    Set(290, value ? "1" : "0");
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is frozen in each viewport made from now on (flag 2 of group 70), as AutoCAD's
        /// <c>ViewportVisibilityDefault</c>, its VPDFLT: true where new viewports freeze it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public bool ViewportVisibilityDefault
        {
            get => Item == null ? _frozenInNewViewports : (Flags & 2) != 0;

            set
            {
                if (Item == null)
                {
                    _frozenInNewViewports = value;
                }
                else if (value != ViewportVisibilityDefault)
                {
                    SetFlag(2, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the layer, as AutoCAD's <c>Description</c>, kept as AutoCAD keeps it: the second 1000
        /// of the layer's extended data of the application <c>AcAecLayerStandard</c>, after an empty one; empty where it has none.
        /// </summary>
        /// <value>
        /// Set, written as <c>1001 AcAecLayerStandard, 1000, 1000 Columns of grid A</c>, the application added to the table of
        /// applications where it lacks it; an empty description takes the application's data out. The description the layer
        /// has changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public string Description
        {
            get
            {
                IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(DescriptionApplication);
                int texts = 0;

                for (int i = 1; data != null && i < data.Count; i++)
                {
                    if (data[i].TypeCode == 1000 && ++texts == 2)
                    {
                        return data[i].Value;
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

                if (string.Equals(value, Description, StringComparison.Ordinal))
                {
                    return;
                }

                XData = value.Length == 0
                    ? new[] { new DxfTypedValue(1001, DescriptionApplication) }
                    : new[] { new DxfTypedValue(1001, DescriptionApplication), new DxfTypedValue(1000, string.Empty), new DxfTypedValue(1000, value) };
            }
        }

        /// <summary>
        /// Gets or sets how much the layer's entities by layer let show through them, as AutoCAD's <c>Transparency</c>, kept as
        /// AutoCAD keeps it: the 1071 of the layer's extended data of the application <c>AcCmTransparency</c>, 0x020000TT; solid,
        /// the alpha 255, where it has none.
        /// </summary>
        /// <value>
        /// Set, an alpha: the 50 per cent of AutoCAD's dialog, <c>new DxfTransparency(127)</c>, is written as
        /// <c>1001 AcCmTransparency, 1071 33554559</c>; solid takes the application's data out. The transparency the layer has
        /// changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the transparency set is by layer, by block or an error value, which
        /// a layer does not take.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased, or a transparency other than solid is set
        /// on a layer of a file older than R2004 (AC1018), which has none.</exception>
        public DxfTransparency Transparency
        {
            get
            {
                IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(TransparencyApplication);

                for (int i = 1; data != null && i < data.Count; i++)
                {
                    if (data[i].TypeCode == 1071 && int.TryParse(data[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    {
                        return DxfTransparency.OfValue(value);
                    }
                }

                return new DxfTransparency(255);
            }

            set
            {
                if (value == Transparency)
                {
                    return;
                }

                if (!value.IsByAlpha)
                {
                    throw new ArgumentException("A layer's transparency is by alpha, from 0 clear to 255 solid: " + value + " is not.", nameof(value));
                }

                if (Item != null && !value.IsSolid)
                {
                    Since(1018, "transparency", "R2004 (AC1018)", true);
                }

                XData = value.IsSolid
                    ? new[] { new DxfTypedValue(1001, TransparencyApplication) }
                    : new[] { new DxfTypedValue(1001, TransparencyApplication), new DxfTypedValue(1071, value.Value.ToString(CultureInfo.InvariantCulture)) };
            }
        }

        /// <summary>
        /// Gets whether the layer is in use, as AutoCAD's <c>IsUsed</c> after <see cref="DxfLayerTable.GenerateUsageData"/>:
        /// layer 0, Defpoints and the current layer (<c>$CLAYER</c>) always; any other where an entity is on it, in model space,
        /// paper space or a block, a vertex or an attribute among them, those added since the drawing was opened too. The layers
        /// in use are worked out in one walk of the drawing and kept until it next changes, so that reading it for 400 layers
        /// walks it once; false for a layer made in code.
        /// </summary>
        public bool IsUsed => Item != null && (IsKept(Name) || Item.File.LayersDrawnOn().Contains(Name));

        /// <summary>
        /// Gets whether the layer looks otherwise in some viewport, as AutoCAD's <c>HasOverrides</c>: whether an XRECORD of its
        /// extension dictionary that AutoCAD keeps overrides in (ADSK_XREC_LAYER_COLOR_OVR, ADSK_XREC_LAYER_LINETYPE_OVR,
        /// ADSK_XREC_LAYER_LINEWT_OVR, ADSK_XREC_LAYER_ALPHA_OVR) names a viewport.
        /// </summary>
        public bool HasOverrides => DxfLayerViewportProperties.Overrides(this).Count > 0;

        /// <summary>Gets whether the layer looks otherwise in a viewport, as AutoCAD's <c>HasViewportOverrides</c>.</summary>
        /// <param name="viewport">The viewport.</param>
        /// <returns>Whether it does.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public bool HasViewportOverrides(DxfViewport viewport)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            return viewport.Handle != null && DxfLayerViewportProperties.Overrides(this).Exists(o => string.Equals(o.Key, viewport.Handle, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets how the layer looks in a viewport, as AutoCAD's <c>GetViewportOverrides</c>: its colour, linetype, lineweight and
        /// transparency there, each the override the layer's extension dictionary keeps for the viewport, else the layer's own.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <returns>The properties, read when asked for.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public DxfLayerViewportProperties GetViewportOverrides(DxfViewport viewport)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            return new DxfLayerViewportProperties(this, viewport);
        }

        /// <summary>
        /// Takes out every override of the layer in the viewports, as AutoCAD's <c>RemoveAllOverrides</c>: the XRECORDs of its
        /// extension dictionary that hold them, each with its entry; nothing where it has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is erased.</exception>
        public void RemoveAllOverrides()
        {
            DxfDBDictionary dictionary = ExtensionDictionary;

            foreach (string key in DxfLayerViewportProperties.Keys)
            {
                if (dictionary != null && dictionary.Contains(key))
                {
                    dictionary.Remove(key);
                }
            }
        }

        /// <summary>
        /// Erases the layer from its table, as AutoCAD's <c>Erase</c> of a layer no entity is on: its LAYER record taken out, the
        /// table's count lowered by one, its handle not given again and what it owned, as its extension dictionary, removed with
        /// it when the file is saved.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the layer is in no file or is erased already; when it is layer
        /// 0, Defpoints or the current layer, which AutoCAD keeps; when an entity is on it (<see cref="IsUsed"/>); or when an
        /// object of the file points at it, as a viewport that freezes it (by handle, or by name in a file before R2000).</exception>
        public override void Erase()
        {
            if (Item == null)
            {
                throw new InvalidOperationException("The layer " + Name + " is in no file to be erased from.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The layer " + Name + " is erased already.");
            }

            string name = Name;

            if (IsKept(name))
            {
                throw new InvalidOperationException("The layer " + name + " is layer 0, Defpoints or the current layer, which AutoCAD keeps.");
            }

            if (IsUsed)
            {
                throw new InvalidOperationException("The layer " + name + " has entities on it, and is not erased, as AutoCAD erases a layer only when nothing is on it.");
            }

            if (Item.File.PointedAt(Handle) || FrozenByName())
            {
                throw new InvalidOperationException("The layer " + name + " is pointed at by an object of the file, as a viewport that freezes it, and is not erased.");
            }

            Item.File.RemoveTableRecord(Item, "LAYER");
        }

        /// <summary>
        /// Renames the layer with every reference to it: its LAYER record's 2; each group 8 of the entities of model space, paper
        /// space and the blocks, their BLOCK, ENDBLK, vertices and attributes among them, and of the objects that name a layer,
        /// as a layer filter, an XRECORD's own data aside; each 1003 of extended data; and <c>$CLAYER</c>.
        /// </summary>
        private protected override void Rename(string name)
        {
            DxfSymbolTable.CheckName(name, "value");
            string old = Name;

            if (old == "0" || string.Equals(old, "Defpoints", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The layer " + old + " is not renamed, as AutoCAD keeps its name.");
            }

            if (IsDependent)
            {
                throw new InvalidOperationException("The layer " + old + " comes from an external reference, and is renamed in its own drawing.");
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The layer " + old + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;

            foreach (DxfItem other in file.TableItems("LAYER"))
            {
                if (other != Item && string.Equals(DxfReader.NameOf(file.ValueOf(other, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The layer table has a layer named " + name + " already, letters of either case the same.", "value");
                }
            }

            bool current = file.Database != null && string.Equals(file.Database.Clayer, old, StringComparison.OrdinalIgnoreCase);
            Set(2, name);
            file.Rename(Naming, (type, code) => code == 8 && type != "XRECORD", 1003, old, name);

            if (current)
            {
                file.SetHeader("$CLAYER", new[] { new DxfTypedValue(8, name) });
            }

            GeometryHelperLog.Info("DxfDatabase: the layer " + old + " was renamed " + name + ", with every entity on it.");
        }

        /// <summary>
        /// Whether a viewport of a file before R2000 freezes the layer by its name, in its extended data MVIEW: a file from R2000
        /// on names it by handle, which <see cref="DxfFile.PointedAt"/> finds.
        /// </summary>
        private bool FrozenByName()
        {
            DxfDatabase database = Item.File.Database;

            if (database == null || Item.File.Supports(1015))
            {
                return false;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfViewport viewport && viewport.IsLayerFrozenInViewport(this))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether a layer is one AutoCAD keeps whatever is on it: layer 0, Defpoints and the current layer.</summary>
        private bool IsKept(string name)
            => name == "0" || string.Equals(name, "Defpoints", StringComparison.OrdinalIgnoreCase) || (Item.File.Database != null && string.Equals(Item.File.Database.Clayer, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The object of a LAYER record, made once.</summary>
        internal static DxfLayerTableRecord Of(DxfItem item) => item.Wrapper as DxfLayerTableRecord ?? (DxfLayerTableRecord)item.KeepWrapper(new DxfLayerTableRecord(item));

        /// <summary>Writes the layer made in code into a file's layer table, and stands for its record from then on.</summary>
        internal void AddTo(DxfFile file)
        {
            if (_lineWeight != DxfLineWeight.ByLineWeightDefault && !file.Supports(1015))
            {
                throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no lineweights, which came with R2000 (AC1015).", "value");
            }

            if (_trueColour.HasValue && !file.Supports(1018))
            {
                throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no true colours, which came with R2004 (AC1018).", "value");
            }

            if (!file.LinetypeListed(_linetype))
            {
                throw new ArgumentException("The linetype " + _linetype + " is not in the file's linetype table.", "value");
            }

            if (!Transparency.IsSolid && !file.Supports(1018))
            {
                throw new ArgumentException("A DXF of version " + (file.Version ?? "R12") + " has no transparency, which came with R2004 (AC1018).", "value");
            }

            DxfItem item = file.AddLayer(Name, (_off ? -_colour : _colour), _trueColour, _linetype, (int)_lineWeight, (_frozen ? 1 : 0) | (_frozenInNewViewports ? 2 : 0) | (_locked ? 4 : 0), _plotted);

            if (item == null)
            {
                throw new InvalidOperationException("The file has no layer table to add the layer " + Name + " to.");
            }

            Bind(item);
        }

        private int Whole(int code, int fallback)
            => int.TryParse(Item.File.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private void Set(int code, string value) => Item.File.SetValues(Item, new[] { new KeyValuePair<int, string>(code, value) }, Order, false);

        private void SetFlag(int flag, bool on) => Set(70, (on ? Flags | flag : Flags & ~flag).ToString(CultureInfo.InvariantCulture));

        private void Since(int version, string what, string since, bool needed)
        {
            if (needed && !Item.File.Supports(version))
            {
                throw new InvalidOperationException("A DXF of version " + (Item.File.Version ?? "R12") + " has no " + what + ", which came with " + since + ".");
            }
        }
    }
}
