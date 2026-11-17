using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>The frame an inspection dimension shows round its words: the ends of the frame rounded, pointed, or no frame.</summary>
    public enum CadInspectionShape
    {
        /// <summary>A frame with round ends, 1.</summary>
        Round = 1,

        /// <summary>A frame with pointed ends, 2.</summary>
        Angular = 2,

        /// <summary>No frame, the label, the words and the rate side by side, 4.</summary>
        None = 4,
    }

    /// <summary>
    /// The inspection data of a dimension, as AutoCAD 2021's DIMINSPECT keeps it: the extended data of the application
    /// <c>ACAD_DSTYLE_DIMINSPECT</c> on the dimension, the groups (1070 396, the inspection rate as the words typed, 1070 395,
    /// the label, 1070 394, the flags, 1070 393, 1). The flags add the shape (1 round, 2 angular, 4 none) to 32 and, where a
    /// label was typed, 16. The rate is the words typed: "100%" where none was, "50" for 50 typed. Enter at the label asks keeps
    /// the label the dimension had but shows none, as AutoCAD 2021 does (probed: the label stays in the data, the flag 16 off).
    /// </summary>
    internal sealed class CadDimInspection
    {
        /// <summary>The name of the application the data is kept for.</summary>
        internal const string Application = "ACAD_DSTYLE_DIMINSPECT";

        private const int ShowsLabel = 16;
        private const int Always = 32;

        /// <summary>Gets or sets the shape of the frame.</summary>
        internal CadInspectionShape Shape { get; set; } = CadInspectionShape.Round;

        /// <summary>Gets or sets the label as it is kept; shown only where <see cref="ShowLabel"/> says so.</summary>
        internal string Label { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the label is shown: the flag 16 of the data.</summary>
        internal bool ShowLabel { get; set; }

        /// <summary>Gets or sets the inspection rate as the words typed, "100%" by default.</summary>
        internal string Rate { get; set; } = "100%";

        /// <summary>Gets the label shown: the label where it is, else nothing.</summary>
        internal string ShownLabel => ShowLabel ? Label : string.Empty;

        /// <summary>Reads the inspection data of a dimension; null where it has none.</summary>
        /// <param name="dimension">The dimension.</param>
        /// <returns>The data, or null.</returns>
        internal static CadDimInspection Read(DxfDimension dimension)
        {
            IReadOnlyList<DxfTypedValue> groups = dimension.GetXDataForApplication(Application);

            if (groups == null)
            {
                return null;
            }

            var found = new CadDimInspection();
            int flags = Always | (int)CadInspectionShape.Round;

            for (int i = 1; i + 1 < groups.Count; i += 2)
            {
                if (
                    groups[i].TypeCode != 1070
                    || !int.TryParse(
                        groups[i].Value.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int code
                    )
                )
                {
                    continue;
                }

                string value = groups[i + 1].Value ?? string.Empty;

                switch (code)
                {
                    case 396:
                        found.Rate = value;
                        break;
                    case 395:
                        found.Label = value;
                        break;
                    case 394:
                        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out flags);
                        break;
                }
            }

            found.ShowLabel = (flags & ShowsLabel) != 0;
            found.Shape =
                (flags & (int)CadInspectionShape.None) != 0 ? CadInspectionShape.None
                : (flags & (int)CadInspectionShape.Angular) != 0 ? CadInspectionShape.Angular
                : CadInspectionShape.Round;
            return found;
        }

        /// <summary>Writes the inspection data into a dimension as AutoCAD writes it.</summary>
        /// <param name="dimension">The dimension.</param>
        internal void Write(DxfDimension dimension)
        {
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(1001, Application),
                new DxfTypedValue(1070, "396"),
                new DxfTypedValue(1000, Rate ?? string.Empty),
            };

            if (!string.IsNullOrEmpty(Label))
            {
                groups.Add(new DxfTypedValue(1070, "395"));
                groups.Add(new DxfTypedValue(1000, Label));
            }

            int flags = (int)Shape | Always | (ShowLabel && !string.IsNullOrEmpty(Label) ? ShowsLabel : 0);
            groups.Add(new DxfTypedValue(1070, "394"));
            groups.Add(new DxfTypedValue(1070, flags.ToString(CultureInfo.InvariantCulture)));
            groups.Add(new DxfTypedValue(1070, "393"));
            groups.Add(new DxfTypedValue(1070, "1"));
            dimension.XData = groups;
        }

        /// <summary>Takes the inspection data out of a dimension.</summary>
        /// <param name="dimension">The dimension.</param>
        /// <returns>Whether it had any.</returns>
        internal static bool Remove(DxfDimension dimension)
        {
            if (dimension.GetXDataForApplication(Application) == null)
            {
                return false;
            }

            dimension.XData = new[] { new DxfTypedValue(1001, Application) };
            return true;
        }
    }
}
