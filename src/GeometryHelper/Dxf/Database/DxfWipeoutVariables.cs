using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The WIPEOUTVARIABLES of a DXF file, as AutoCAD keeps what WIPEOUTFRAME sets: whether the frames of the wipeouts are
    /// shown, the entry ACAD_WIPEOUT_VARS of the named object dictionary, which AutoCAD 2021 writes once a drawing holds a
    /// wipeout or WIPEOUT Frames is first used (<see cref="GetWipeoutVariables"/>):
    /// <c>db.NamedObjectsDictionary.GetAt("ACAD_WIPEOUT_VARS")</c>.
    /// </summary>
    /// <remarks>
    /// AutoCAD 2021 gives a drawing it opens with wipeouts frames shown (70 of 1). The record keeps no more than shown or not:
    /// WIPEOUTFRAME 2, the frames shown and not plotted, is written as 70 of 1 as well.
    /// </remarks>
    public sealed class DxfWipeoutVariables : DxfDBObject
    {
        // The groups of AcDbWipeoutVariables in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 70 });

        internal DxfWipeoutVariables(DxfItem item)
            : base(item) { }

        /// <summary>
        /// Gets or sets whether the frames of the wipeouts are shown (group 70): true for any number but 0, and where the
        /// record gives none, as AutoCAD 2021 reads it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the variables are erased.</exception>
        public bool ShowFrames
        {
            get => Whole(70, 1) != 0;
            set
            {
                if (value != ShowFrames)
                {
                    InFile("be changed")
                        .SetValues(Item, new[] { new KeyValuePair<int, string>(70, value ? "1" : "0") }, Order, false);
                }
            }
        }

        /// <summary>
        /// Gets the variables of wipeouts of a drawing: its entry ACAD_WIPEOUT_VARS, made where it has none, as AutoCAD 2021
        /// makes it when a wipeout is first made or WIPEOUT Frames first used: frames shown (70 of 1), the class
        /// WIPEOUTVARIABLES listed and counted.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The variables.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no named object dictionary, as one of R12.</exception>
        public static DxfWipeoutVariables GetWipeoutVariables(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfDBDictionary root = database.NamedObjectsDictionary
                ?? throw new InvalidOperationException(
                    "The drawing has no named object dictionary to keep the variables of wipeouts in."
                );

            if (!root.Contains("ACAD_WIPEOUT_VARS"))
            {
                database.File.WipeoutVariables();
            }

            return root.GetAt("ACAD_WIPEOUT_VARS") as DxfWipeoutVariables
                ?? throw new InvalidOperationException(
                    "The entry ACAD_WIPEOUT_VARS of the drawing is no WIPEOUTVARIABLES."
                );
        }

        /// <summary>Gives the variables as a line for debugging: their type, their handle and whether the frames show.</summary>
        /// <returns>The line, such as <c>WIPEOUTVARIABLES 19A frames shown</c>.</returns>
        public override string ToString() =>
            "WIPEOUTVARIABLES " + (Handle ?? string.Empty) + (ShowFrames ? " frames shown" : " frames hidden");

        private int Whole(int code, int fallback)
        {
            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == code)
                {
                    return int.TryParse(
                        group.Value?.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value
                    )
                        ? value
                        : fallback;
                }
            }

            return fallback;
        }
    }
}
