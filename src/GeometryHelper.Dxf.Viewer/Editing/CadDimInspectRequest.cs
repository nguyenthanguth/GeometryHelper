using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Inspection Dimension dialog of DIMINSPECT, as data its command and the window share: the shape of the frame, the label
    /// and the inspection rate that OK writes into every dimension selected, or Remove Inspection, which takes the inspection
    /// out of them. The dialog starts from the first selected dimension that has an inspection, and from a round frame with no
    /// label and a rate of 100% where none has.
    /// </summary>
    public sealed class CadDimInspectRequest
    {
        internal CadDimInspectRequest(IReadOnlyList<DxfDimension> dimensions)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            DimensionCount = dimensions.Count;

            foreach (DxfDimension dimension in dimensions)
            {
                CadDimInspection kept = CadDimInspection.Read(dimension);

                if (kept == null)
                {
                    continue;
                }

                if (!HasInspection)
                {
                    HasInspection = true;
                    Shape = kept.Shape;
                    ShowLabel = kept.ShowLabel;
                    Label = kept.Label;
                    Rate = kept.Rate;
                }

                WithInspection++;
            }
        }

        /// <summary>Gets how many dimensions the inspection is for.</summary>
        public int DimensionCount { get; }

        /// <summary>Gets how many of them have an inspection now.</summary>
        public int WithInspection { get; }

        /// <summary>Gets whether a dimension selected has an inspection, so that Remove Inspection has something to take out.</summary>
        public bool HasInspection { get; }

        /// <summary>Gets or sets the shape of the frame; round until set.</summary>
        public CadInspectionShape Shape { get; set; } = CadInspectionShape.Round;

        /// <summary>Gets or sets whether the label is shown.</summary>
        public bool ShowLabel { get; set; }

        /// <summary>Gets or sets the label, empty for none.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Gets or sets the inspection rate as the words shown, "100%" until set.</summary>
        public string Rate { get; set; } = "100%";

        /// <summary>Gets or sets whether Remove Inspection was pressed: the inspection is taken out of every dimension selected and nothing is written.</summary>
        public bool Remove { get; set; }

        /// <summary>Gets or sets what the dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            "Inspection Dimension ("
            + DimensionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " dimensions)";
    }
}
