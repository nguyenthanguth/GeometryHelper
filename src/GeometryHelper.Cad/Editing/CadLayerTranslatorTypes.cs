using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A layer of the Layer Translator's lists, as a layer of the drawing or of a standards file: the settings a layer of that
    /// name has, or is to be given. A layer WALL of colour 1, linetype HIDDEN, 0.35 mm lineweight and no transparency is one
    /// of these; mapped to by OLD-WALL, it makes OLD-WALL's objects WALL's.
    /// </summary>
    public sealed class CadTranslateLayer
    {
        /// <summary>Makes a layer of the colour white (7) and the linetype Continuous, as AutoCAD's New layer in the translator.</summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadTranslateLayer(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Gets the layer's name.</summary>
        public string Name { get; }

        /// <summary>Gets or sets the colour; white (7) until set.</summary>
        public DxfColor Color { get; set; } = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

        /// <summary>Gets or sets the name of the linetype; Continuous until set.</summary>
        public string LinetypeName { get; set; } = "Continuous";

        /// <summary>Gets or sets the lineweight; the drawing's default until set.</summary>
        public DxfLineWeight LineWeight { get; set; } = DxfLineWeight.ByLineWeightDefault;

        /// <summary>Gets or sets the transparency in per cent, from 0 (solid) to 90.</summary>
        public int TransparencyPercent { get; set; }

        /// <summary>Gets or sets the name of the plot style; empty for none.</summary>
        public string PlotStyleName { get; set; } = string.Empty;

        /// <summary>Gets whether anything of the drawing is on the layer, or a block, a style or the header names it; true for a layer of the Translate From list that is used.</summary>
        public bool IsInUse { get; internal set; }

        /// <summary>The drawing the layer was read from where it is of a standards file, which a linetype it names is copied from.</summary>
        internal DxfDatabase Source { get; set; }

        /// <summary>Makes another layer of the same settings.</summary>
        /// <returns>The copy.</returns>
        public CadTranslateLayer Clone() =>
            new CadTranslateLayer(Name)
            {
                Color = Color,
                LinetypeName = LinetypeName,
                LineWeight = LineWeight,
                TransparencyPercent = TransparencyPercent,
                PlotStyleName = PlotStyleName,
                IsInUse = IsInUse,
                Source = Source,
            };

        /// <summary>Gets the settings of a layer of a drawing.</summary>
        internal static CadTranslateLayer Of(DxfLayerTableRecord layer, DxfDatabase source) =>
            new CadTranslateLayer(layer.Name)
            {
                Color = layer.Color,
                LinetypeName = layer.LinetypeName ?? "Continuous",
                LineWeight = layer.LineWeight,
                TransparencyPercent = CadLayerEditor.TransparencyPercent(layer.Transparency),
                PlotStyleName = layer.PlotStyleName ?? string.Empty,
                IsInUse = layer.IsUsed,
                Source = source,
            };
    }

    /// <summary>A layer of the drawing mapped to a layer it is translated to: the old layer's name and the layer, with the settings, it takes the objects to.</summary>
    public sealed class CadLayerMapping
    {
        internal CadLayerMapping(string from, CadTranslateLayer to)
        {
            From = from;
            To = to;
        }

        /// <summary>Gets the name of the layer of the drawing that is translated.</summary>
        public string From { get; }

        /// <summary>Gets the layer it is translated to; Edit changes its settings for this mapping alone.</summary>
        public CadTranslateLayer To { get; internal set; }
    }

    /// <summary>
    /// The Layer Translator's Settings: whether the objects of the translated layers are forced to ByLayer, whether those in
    /// blocks are translated, the transaction log and whether a layer selected shows its contents. All are on until set, as
    /// AutoCAD's dialog opens them.
    /// </summary>
    public sealed class CadLayerTranslatorSettings
    {
        /// <summary>Gets or sets whether the objects of the translated layers are given the colour ByLayer.</summary>
        public bool ForceColorByLayer { get; set; } = true;

        /// <summary>Gets or sets whether the objects of the translated layers are given the linetype ByLayer.</summary>
        public bool ForceLinetypeByLayer { get; set; } = true;

        /// <summary>Gets or sets whether the objects of the translated layers are given the transparency ByLayer.</summary>
        public bool ForceTransparencyByLayer { get; set; } = true;

        /// <summary>Gets or sets whether the objects in the definitions of blocks are translated too; off, only those of model space and the layouts are.</summary>
        public bool TranslateObjectsInBlocks { get; set; } = true;

        /// <summary>Gets or sets whether a transaction log is written beside the drawing: what was moved, created and deleted.</summary>
        public bool WriteTransactionLog { get; set; } = true;

        /// <summary>Gets or sets whether a layer selected in the lists shows only its objects in the drawing, for as long as it is selected.</summary>
        public bool ShowLayerContents { get; set; } = true;
    }
}
