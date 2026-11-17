using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The columns of AutoCAD 2021's Layer Properties Manager after Lineweight: Transparency, Plot Style, Plot and Description,
    /// each an edit of the layer through <see cref="CadLayerEditor"/> as one undo step, refused as -LAYER refuses it.
    /// </summary>
    public sealed partial class LayerRowViewModel
    {
        private bool _plotted = true;
        private int _transparency;
        private string _plotStyle = string.Empty;
        private string _description = string.Empty;

        /// <summary>Gets or sets whether the layer is plotted (the printer icon of the Plot column; flag 290 of the layer).</summary>
        public bool IsPlotted
        {
            get => _plotted;
            set =>
                Change(
                    value != _plotted && _owner.Done(CadLayerEditor.SetPlottable(_owner.Document, Name, value)),
                    nameof(IsPlotted)
                );
        }

        /// <summary>
        /// Gets or sets the transparency of the layer in per cent, 0 to 90, as AutoCAD's Transparency column shows it (alpha 127 is
        /// 50); a value outside 0 to 90 is refused on the command line as -LAYER TRansparency refuses it.
        /// </summary>
        public string Transparency
        {
            get => _transparency.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (
                    !int.TryParse(
                        (value ?? string.Empty).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int percent
                    )
                )
                {
                    _owner.Done(CadEditResult.Refuse("Invalid transparency \"" + value + "\": give 0 to 90."));
                    Raise(nameof(Transparency));
                    return;
                }

                Change(
                    percent != _transparency
                        && _owner.Done(CadLayerEditor.SetTransparency(_owner.Document, Name, percent)),
                    nameof(Transparency)
                );
            }
        }

        /// <summary>Gets the transparencies the Transparency cell offers: 0 to 90 by tens, and the layer's own where it is another.</summary>
        public IReadOnlyList<string> TransparencyChoices
        {
            get
            {
                var options = new List<string>(LayerManagerViewModel.TransparencyNames);

                if (!options.Contains(Transparency))
                {
                    options.Add(Transparency);
                }

                return options;
            }
        }

        /// <summary>
        /// Gets or sets the plot style: in a drawing plotted by colour Color_ and the layer's colour, read only and grey, as AutoCAD
        /// greys the column; in a drawing of named plot styles one of its plot style names, which is set.
        /// </summary>
        public string PlotStyle
        {
            get => _plotStyle;
            set =>
                Change(
                    _owner.HasNamedPlotStyles
                        && !string.Equals((value ?? string.Empty).Trim(), _plotStyle, StringComparison.Ordinal)
                        && _owner.Done(CadLayerEditor.SetPlotStyle(_owner.Document, Name, value)),
                    nameof(PlotStyle)
                );
        }

        /// <summary>Gets whether the plot style can be changed: the drawing has named plot styles, and the layer is not one of an external reference.</summary>
        public bool CanEditPlotStyle => _owner.HasNamedPlotStyles && !_dependent;

        /// <summary>Gets the plot styles the Plot Style cell offers: the names of the drawing's plot styles.</summary>
        public IReadOnlyList<string> PlotStyleChoices => _owner.PlotStyleNames;

        /// <summary>Gets or sets the description of the layer, one line; empty takes it out.</summary>
        public string Description
        {
            get => _description;
            set =>
                Change(
                    !string.Equals(value ?? string.Empty, _description, StringComparison.Ordinal)
                        && _owner.Done(CadLayerEditor.SetDescription(_owner.Document, Name, value)),
                    nameof(Description)
                );
        }

        private bool _dependent;

        /// <summary>Reads the columns from the layer's record.</summary>
        internal void ReadColumns(DxfLayerTableRecord record, DxfDatabase database)
        {
            _plotted = record.IsPlottable;
            _transparency = CadLayerEditor.TransparencyPercent(record.Transparency);
            _description = record.Description;
            _dependent = record.IsDependent;
            _plotStyle =
                database.PlotStyleMode ? "Color_" + Math.Abs(record.ColorIndex).ToString(CultureInfo.InvariantCulture)
                : record.PlotStyleName.Length == 0 ? "Normal"
                : record.PlotStyleName;
        }
    }
}
