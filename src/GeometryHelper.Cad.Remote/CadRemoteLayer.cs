using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// A layer of a drawing of a running GeometryHelper CAD, as the Layer manager lists it: its name and how it looks. One
    /// read from <see cref="CadRemoteDocument.Layers"/> is a copy; change it and give it to
    /// <see cref="CadRemoteDocument.ModifyLayer"/> to write the changes, or make a new one and give it to
    /// <see cref="CadRemoteDocument.AddLayer"/>.
    /// </summary>
    /// <remarks>
    /// A layer made in code is on, thawed, unlocked and plotted, white (index 7), with the linetype Continuous, the default
    /// lineweight and no description, as AutoCAD's New layer makes one.
    /// </remarks>
    /// <example>
    /// <code>
    /// var steel = new CadRemoteLayer("Steel") { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1) };
    /// drawing.AddLayer(steel);
    /// drawing.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "Steel" });
    /// drawing.CommitChanges();
    /// </code>
    /// </example>
    public sealed class CadRemoteLayer
    {
        private string _linetype = "Continuous";
        private string _description = string.Empty;
        private DxfColor _color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

        /// <summary>Makes a layer to add to a drawing.</summary>
        /// <param name="name">The name; the drawing refuses one that has a character AutoCAD refuses in a layer name, or that it has already, letters of either case the same.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        public CadRemoteLayer(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("A layer needs a name.", nameof(name));
            }

            Name = name.Trim();
        }

        /// <summary>Gets the name of the layer.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the layer is on: an off layer's objects are not drawn.</summary>
        public bool IsOn { get; set; } = true;

        /// <summary>Gets or sets whether the layer is frozen: a frozen layer's objects are not drawn, and the app refuses to freeze the current layer.</summary>
        public bool IsFrozen { get; set; }

        /// <summary>Gets or sets whether the layer is locked: its objects are drawn and the user cannot change them.</summary>
        public bool IsLocked { get; set; }

        /// <summary>Gets or sets whether the layer is plotted.</summary>
        public bool IsPlottable { get; set; } = true;

        /// <summary>Gets or sets the colour of the layer: an AutoCAD colour index from 1 to 255, or a true colour (<see cref="DxfColor.FromRgb"/>).</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by layer, by block or another a layer does not take.</exception>
        public DxfColor Color
        {
            get => _color;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!value.IsByAci && !value.IsByColor)
                {
                    throw new ArgumentException(
                        "A layer is of a colour of the index or a true colour: " + value.ColorMethod + " is neither.",
                        nameof(value)
                    );
                }

                _color = value;
            }
        }

        /// <summary>Gets or sets the name of the layer's linetype; Continuous until set. The drawing refuses a linetype it does not list.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        public string Linetype
        {
            get => _linetype;
            set => _linetype = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the lineweight of the layer: <see cref="DxfLineWeight.ByLineWeightDefault"/> until set. The drawing refuses a weight AutoCAD has none of, and one by layer or by block.</summary>
        public DxfLineWeight LineWeight { get; set; } = DxfLineWeight.ByLineWeightDefault;

        /// <summary>Gets or sets the description of the layer, as the Layer manager shows it; empty for none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        public string Description
        {
            get => _description;
            set => _description = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public override string ToString() => Name;

        internal JsonObject ToJson()
        {
            var json = new JsonObject()
                .Set("name", Name)
                .Set("on", IsOn)
                .Set("frozen", IsFrozen)
                .Set("locked", IsLocked)
                .Set("plottable", IsPlottable)
                .Set("colorIndex", (long)_color.ColorIndex)
                .Set("linetype", _linetype)
                .Set("lineWeight", (long)LineWeight)
                .Set("description", _description);

            if (_color.IsByColor)
            {
                json.Set("trueColor", (long)_color.ColorValue);
            }

            return json;
        }

        internal static CadRemoteLayer FromJson(JsonObject json)
        {
            var layer = new CadRemoteLayer(json.GetString("name") ?? "0")
            {
                IsOn = json.GetBool("on", true),
                IsFrozen = json.GetBool("frozen"),
                IsLocked = json.GetBool("locked"),
                IsPlottable = json.GetBool("plottable", true),
                Linetype = json.GetString("linetype") ?? "Continuous",
                LineWeight = (DxfLineWeight)json.GetLong("lineWeight", (long)DxfLineWeight.ByLineWeightDefault),
                Description = json.GetString("description") ?? string.Empty,
            };

            if (json.Get("trueColor") != null)
            {
                int rgb = (int)json.GetLong("trueColor");
                layer.Color = DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }
            else
            {
                layer.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)json.GetLong("colorIndex", 7));
            }

            return layer;
        }
    }
}
