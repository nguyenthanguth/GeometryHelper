using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The RASTERVARIABLES of a DXF file, as AutoCAD's <c>RasterVariables</c>: how the images of the drawing are shown, the
    /// entry ACAD_IMAGE_VARS of the named object dictionary, which AutoCAD 2021 writes once a drawing holds an image:
    /// <c>db.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS")</c>.
    /// </summary>
    /// <remarks>
    /// AutoCAD 2021 gives a drawing it opens with images frames shown (70 of 1), pictures shown finely (71 of 1) and no unit
    /// (72 of 0). Each value is written as its number.
    /// </remarks>
    public sealed class DxfRasterVariables : DxfDBObject
    {
        // The groups of AcDbRasterVariables in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 90, 70, 71, 72 });

        internal DxfRasterVariables(DxfItem item)
            : base(item) { }

        /// <summary>
        /// Gets or sets whether the frames of the images are shown (group 70), as AutoCAD's <c>ImageFrame</c>: written as the
        /// number of the setting, 0 to 3, as AutoCAD 2021 writes it; <see cref="DxfFrameSetting.ImageFrameOff"/> where the record
        /// gives none, <see cref="DxfFrameSetting.ImageFrameInvalid"/> for a number outside 0 to 3.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the setting set is not one of 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the variables are erased.</exception>
        public DxfFrameSetting ImageFrame
        {
            get
            {
                int frame = Whole(70, 0);
                return frame >= 0 && frame <= 3 ? (DxfFrameSetting)frame : DxfFrameSetting.ImageFrameInvalid;
            }
            set
            {
                if (value < DxfFrameSetting.ImageFrameOff || value > DxfFrameSetting.ImageFrameOnNoPlot)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The frame of the images is set to one of ImageFrameOff to ImageFrameOnNoPlot."
                    );
                }

                Set(70, (int)value, ImageFrame == value);
            }
        }

        /// <summary>
        /// Gets or sets how finely the images are shown (group 71), as AutoCAD's <c>ImageQuality</c>: draft (0) or high (1);
        /// high where the record gives none, <see cref="DxfImageQuality.Invalid"/> for another number.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the quality set is neither draft nor high.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the variables are erased.</exception>
        public DxfImageQuality ImageQuality
        {
            get
            {
                int quality = Whole(71, 1);
                return quality == 0 || quality == 1 ? (DxfImageQuality)quality : DxfImageQuality.Invalid;
            }
            set
            {
                if (value != DxfImageQuality.Draft && value != DxfImageQuality.High)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The images are shown in draft or high quality."
                    );
                }

                Set(71, (int)value, ImageQuality == value);
            }
        }

        /// <summary>
        /// Gets or sets the unit images are scaled in where the drawing gives none (group 72), as AutoCAD's <c>UserScale</c>:
        /// none (0) where the record gives none, and for a number outside 0 to 20.
        /// </summary>
        /// <value>Set, written as its number; AutoCAD 2021's own <c>UserScale</c> left 72 at 0 when set to millimetres, feet or
        /// microns, which the library does not follow.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not one of <see cref="DxfUnit"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the variables are erased.</exception>
        public DxfUnit UserScale
        {
            get
            {
                int unit = Whole(72, 0);
                return unit >= 0 && unit <= 20 ? (DxfUnit)unit : DxfUnit.None;
            }
            set
            {
                if (value < DxfUnit.None || value > DxfUnit.Parsecs)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The unit images are scaled in is one of DxfUnit."
                    );
                }

                Set(72, (int)value, UserScale == value);
            }
        }

        /// <summary>Gives the variables as a line for debugging: their type, their handle and their three settings.</summary>
        /// <returns>The line, such as <c>RASTERVARIABLES 19A ImageFrameAbove High None</c>.</returns>
        public override string ToString() =>
            "RASTERVARIABLES " + (Handle ?? string.Empty) + " " + ImageFrame + " " + ImageQuality + " " + UserScale;

        /// <summary>Writes a setting where it changes, in AutoCAD's order of the groups; nothing where it is the one read.</summary>
        private void Set(int code, int value, bool same)
        {
            if (!same)
            {
                InFile("be changed")
                    .SetValues(
                        Item,
                        new[] { new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture)) },
                        Order,
                        false
                    );
            }
        }

        /// <summary>The first group of a code of the variables' own part; null where they have none.</summary>
        private string Text(int code)
        {
            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        private int Whole(int code, int fallback) =>
            int.TryParse(Text(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
    }
}
