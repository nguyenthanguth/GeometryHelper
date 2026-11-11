using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One check box of the Property Settings dialog: its words, the source's value beside it, whether it is checked, and whether it can be.</summary>
    public sealed class MatchPropOptionViewModel : ObservableObject
    {
        private bool _isChecked;

        internal MatchPropOptionViewModel(
            string name,
            CadMatchProperties property,
            bool isChecked,
            string value = null,
            int? swatchRgb = null
        )
        {
            Name = name;
            Property = property;
            Value = value ?? string.Empty;
            SwatchRgb = swatchRgb;
            _isChecked = isChecked && property != CadMatchProperties.None;
        }

        /// <summary>Gets the words beside the box: "Linetype Scale".</summary>
        public string Name { get; }

        /// <summary>Gets the property it stands for; <see cref="CadMatchProperties.None"/> for one greyed, which MATCHPROP does not copy.</summary>
        public CadMatchProperties Property { get; }

        /// <summary>Gets the source's value of a basic property, as the Properties palette writes it: "ByLayer", "WALLS", "0.25 mm"; empty for a special property.</summary>
        public string Value { get; }

        /// <summary>Gets the swatch of the Color box as 0xRRGGBB: the source's colour, white for ByLayer and ByBlock; null for the other boxes.</summary>
        public int? SwatchRgb { get; }

        /// <summary>Gets whether it can be checked: false for Plot style, Material, Shadow display and Center object.</summary>
        public bool IsEnabled => Property != CadMatchProperties.None;

        /// <summary>Gets or sets whether it is checked; a greyed one stays clear.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value && IsEnabled);
        }
    }

    /// <summary>
    /// The Property Settings dialog of MATCHPROP, as the design lays it: the Basic Properties (Color, Layer, Linetype, Linetype
    /// Scale, Lineweight, Transparency, Thickness, Plot Style), each with the source's value beside it, and the Special
    /// Properties (Dimension, Text, Hatch, Polyline, Viewport, Table, Material, Shadow display, Multileader, Center object), each
    /// checked as the command's settings have it; the source named under them and the prompt that asked for the dialog at the
    /// foot. Plot style, Material, Shadow display and Center object are greyed: the library has no setter of an object's plot
    /// style, and the other three are of 3D and of centre marks, which are not built. OK gives the settings checked
    /// (<see cref="Result"/>).
    /// </summary>
    public sealed class MatchPropSettingsViewModel
    {
        /// <summary>The prompt MATCHPROP answered with Settings, shown at the foot of the dialog.</summary>
        public const string Prompt = "Select destination object(s) or [Settings]: S";

        /// <summary>Makes the dialog over the settings MATCHPROP has.</summary>
        /// <param name="settings">The settings: a box is checked where its property is among them.</param>
        /// <param name="document">The document of the source object; null for none, the values left empty.</param>
        /// <param name="source">The handle of the source object, whose values stand beside the basic properties; null for none.</param>
        public MatchPropSettingsViewModel(
            CadMatchProperties settings,
            CadDocument document = null,
            string source = null
        )
        {
            DxfEntity entity = document == null || source == null ? null : document.FindEntity(source);
            Dictionary<string, string> values = Values(document, entity, out string kind);

            MatchPropOptionViewModel Box(string name, CadMatchProperties property, int? swatch = null) =>
                new MatchPropOptionViewModel(
                    name,
                    property,
                    (settings & property) != 0,
                    values.TryGetValue(name, out string value) ? value : null,
                    swatch
                );

            Basic = new[]
            {
                Box("Color", CadMatchProperties.Color, entity == null ? (int?)null : Swatch(entity.Color)),
                Box("Layer", CadMatchProperties.Layer),
                Box("Linetype", CadMatchProperties.Linetype),
                Box("Linetype Scale", CadMatchProperties.LinetypeScale),
                Box("Lineweight", CadMatchProperties.Lineweight),
                Box("Transparency", CadMatchProperties.Transparency),
                Box("Thickness", CadMatchProperties.Thickness),
                Box("Plot Style", CadMatchProperties.None),
            };

            Special = new[]
            {
                Box("Dimension", CadMatchProperties.Dimension),
                Box("Text", CadMatchProperties.Text),
                Box("Hatch", CadMatchProperties.Hatch),
                Box("Polyline", CadMatchProperties.Polyline),
                Box("Viewport", CadMatchProperties.Viewport),
                Box("Table", CadMatchProperties.Table),
                Box("Material", CadMatchProperties.None),
                Box("Shadow display", CadMatchProperties.None),
                Box("Multileader", CadMatchProperties.Multileader),
                Box("Center object", CadMatchProperties.None),
            };

            SourceText =
                (entity == null ? string.Empty : "Source: " + kind + " on layer " + (entity.Layer ?? "0") + ". ")
                + "The ticks are kept for the session (and the next MATCHPROP), as AutoCAD keeps them.";
        }

        /// <summary>Gets the boxes of Basic Properties, in AutoCAD's order.</summary>
        public IReadOnlyList<MatchPropOptionViewModel> Basic { get; }

        /// <summary>Gets the boxes of Special Properties, in AutoCAD's order.</summary>
        public IReadOnlyList<MatchPropOptionViewModel> Special { get; }

        /// <summary>Gets the line under the boxes: "Source: Line on layer WALLS. The ticks are kept for the session (and the next MATCHPROP), as AutoCAD keeps them."</summary>
        public string SourceText { get; }

        /// <summary>Gets the prompt at the foot of the dialog.</summary>
        public string PromptText => Prompt;

        /// <summary>Gets the settings the boxes checked give: Layer alone where only Layer is checked.</summary>
        public CadMatchProperties Result
        {
            get
            {
                CadMatchProperties result = CadMatchProperties.None;

                foreach (MatchPropOptionViewModel box in Basic.Concat(Special))
                {
                    if (box.IsChecked)
                    {
                        result |= box.Property;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// The source's values of the basic properties, by the boxes' words: those of the Properties palette's General group,
        /// its transparency and thickness, and the plot style its drawing's mode gives (ByColor where its plot styles are
        /// colour-dependent); and its kind, as the palette names it.
        /// </summary>
        private static Dictionary<string, string> Values(CadDocument document, DxfEntity entity, out string kind)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            kind = string.Empty;

            if (entity == null)
            {
                return values;
            }

            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { entity.Handle });
            int count = info.Label.LastIndexOf(" (", StringComparison.Ordinal);
            kind = count > 0 ? info.Label.Substring(0, count) : info.Label;

            foreach (CadPropertyRow row in info.Groups.Where(g => g.Title == "General").SelectMany(g => g.Rows))
            {
                string name = row.Label == "Linetype scale" ? "Linetype Scale" : row.Label;
                values[name] = row.Value;
            }

            values["Transparency"] = Read(() => TransparencyText(entity.Transparency), "ByLayer");
            values["Thickness"] = Thickness(entity)?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
            values["Plot Style"] = Read(() => document.Database.PlotStyleMode ? "ByColor" : "ByLayer", "ByColor");
            return values;
        }

        private static string Read(Func<string> read, string otherwise)
        {
            try
            {
                return read();
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                return otherwise;
            }
        }

        /// <summary>The thickness of an object that has one; null for one that has none.</summary>
        private static double? Thickness(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine line:
                    return line.Thickness;
                case DxfCircle circle:
                    return circle.Thickness;
                case DxfArc arc:
                    return arc.Thickness;
                case DxfPolyline polyline:
                    return polyline.Thickness;
                case DxfPolyline2d polyline:
                    return polyline.Thickness;
                case DxfDBText text:
                    return text.Thickness;
                case DxfDBPoint point:
                    return point.Thickness;
                case DxfSolid solid:
                    return solid.Thickness;
                default:
                    return null;
            }
        }

        private static int Swatch(DxfColor color) =>
            color == null || color.IsByLayer || color.IsByBlock ? 0xFFFFFF : color.ColorValue & 0xFFFFFF;

        /// <summary>A transparency as the palette writes it: ByLayer, ByBlock, or a per cent from 0, solid, to 90.</summary>
        private static string TransparencyText(DxfTransparency transparency)
        {
            if (transparency.IsByBlock)
            {
                return "ByBlock";
            }

            if (!transparency.IsByAlpha)
            {
                return "ByLayer";
            }

            return (
                (int)Math.Round((255 - transparency.Alpha) * 100.0 / 255.0, MidpointRounding.AwayFromZero)
            ).ToString(CultureInfo.InvariantCulture);
        }
    }
}
