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
    /// </remarks>
    public sealed class DxfLayerTableRecord : DxfSymbolTableRecord
    {
        // The groups of AcDbLayerTableRecord in the order AutoCAD writes them, after AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 100, 2, 70, 62, 420, 6, 290, 370, 390, 347 });

        private int _colour = 7;
        private int? _trueColour;
        private string _linetype = "Continuous";
        private DxfLineWeight _lineWeight = DxfLineWeight.ByLineWeightDefault;
        private bool _off;
        private bool _frozen;
        private bool _locked;
        private bool _plotted = true;

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

            DxfItem item = file.AddLayer(Name, (_off ? -_colour : _colour), _trueColour, _linetype, (int)_lineWeight, (_frozen ? 1 : 0) | (_locked ? 4 : 0), _plotted);

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
