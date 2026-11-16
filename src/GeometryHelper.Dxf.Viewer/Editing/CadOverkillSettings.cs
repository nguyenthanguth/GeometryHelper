using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The properties OVERKILL can leave out when it compares two objects, as the boxes of its dialog and the options of -OVERKILL's Ignore list them.</summary>
    [Flags]
    public enum CadOverkillIgnore
    {
        /// <summary>No property is ignored: two objects are the same only where all of them agree.</summary>
        None = 0,

        /// <summary>The colour.</summary>
        Color = 1,

        /// <summary>The layer.</summary>
        Layer = 2,

        /// <summary>The linetype.</summary>
        Linetype = 4,

        /// <summary>The linetype scale.</summary>
        LinetypeScale = 8,

        /// <summary>The lineweight.</summary>
        Lineweight = 16,

        /// <summary>The thickness.</summary>
        Thickness = 32,

        /// <summary>The transparency.</summary>
        Transparency = 64,

        /// <summary>The plot style.</summary>
        PlotStyle = 128,

        /// <summary>The material.</summary>
        Material = 256,

        /// <summary>Every property.</summary>
        All = Color | Layer | Linetype | LinetypeScale | Lineweight | Thickness | Transparency | PlotStyle | Material,
    }

    /// <summary>
    /// The settings of OVERKILL as AutoCAD 2021's Delete Duplicate Objects dialog and -OVERKILL keep them from one run to the
    /// next: how close two objects must lie to be taken for one, the properties left out of the comparison, and what is done
    /// with polylines and with objects that overlap. The tolerance is half the distance within which two points are one: of
    /// two lines 0.04 apart and the same length, a tolerance of 0.1 deletes one and 0.05 does not.
    /// </summary>
    public sealed class CadOverkillSettings
    {
        /// <summary>Gets or sets the tolerance of the comparison, 0.000001 until set; nought or more.</summary>
        public double Tolerance { get; set; } = 1E-6;

        /// <summary>Gets or sets the properties ignored when objects are compared; none until set.</summary>
        public CadOverkillIgnore Ignore { get; set; }

        /// <summary>Gets or sets whether the segments within polylines are compared with other objects and with one another, their duplicate vertices and collinear segments removed; true until set.</summary>
        public bool OptimizePolylines { get; set; } = true;

        /// <summary>Gets or sets whether the widths of polyline segments are left out when they are compared.</summary>
        public bool IgnorePolylineWidths { get; set; }

        /// <summary>Gets or sets whether a polyline is kept whole: a segment that overlaps another object is left in it, not cut out, though duplicate vertices still go.</summary>
        public bool DoNotBreakPolylines { get; set; }

        /// <summary>Gets or sets whether collinear objects that overlap in part are made one, and the ones lying wholly within another deleted; true until set.</summary>
        public bool CombinePartialOverlaps { get; set; } = true;

        /// <summary>Gets or sets whether collinear objects that meet end to end are made one; true until set.</summary>
        public bool CombineEndToEnd { get; set; } = true;

        /// <summary>Gets or sets whether associative objects are kept: kept as a setting, which the presenter does not act on, none of its objects being tied to others by OVERKILL's rules.</summary>
        public bool MaintainAssociativity { get; set; } = true;

        /// <summary>Makes a copy.</summary>
        /// <returns>The copy.</returns>
        public CadOverkillSettings Clone() => (CadOverkillSettings)MemberwiseClone();

        /// <summary>The settings as -OVERKILL writes them: "Tolerance=0.000001, Ignore=None, Optimize polylines=Yes, Combine partial overlap=Yes, Combine end-to-end=Yes".</summary>
        internal string Describe() =>
            "Tolerance="
            + Tolerance.ToString("0.000000", CultureInfo.InvariantCulture)
            + ", Ignore="
            + IgnoreWords(Ignore)
            + ", Optimize polylines="
            + (OptimizePolylines ? "Yes" : "No")
            + ", Combine partial overlap="
            + (CombinePartialOverlaps ? "Yes" : "No")
            + ", Combine end-to-end="
            + (CombineEndToEnd ? "Yes" : "No");

        /// <summary>The names of the properties ignored as the Ignore list of -OVERKILL spells them: None, All, or the names with commas.</summary>
        internal static string IgnoreWords(CadOverkillIgnore ignore)
        {
            if (ignore == CadOverkillIgnore.None)
            {
                return "None";
            }

            if (ignore == CadOverkillIgnore.All)
            {
                return "All";
            }

            var words = new List<string>();

            foreach (CadOverkillIgnore one in Each)
            {
                if ((ignore & one) != 0)
                {
                    words.Add(one.ToString());
                }
            }

            return string.Join(",", words);
        }

        /// <summary>The single properties, in the order of the dialog.</summary>
        internal static readonly CadOverkillIgnore[] Each =
        {
            CadOverkillIgnore.Color,
            CadOverkillIgnore.Layer,
            CadOverkillIgnore.Linetype,
            CadOverkillIgnore.LinetypeScale,
            CadOverkillIgnore.Lineweight,
            CadOverkillIgnore.Thickness,
            CadOverkillIgnore.Transparency,
            CadOverkillIgnore.PlotStyle,
            CadOverkillIgnore.Material,
        };
    }

    /// <summary>What an OVERKILL run deleted, as it writes it: "1 duplicate(s) deleted", "2 overlapping object(s) or segment(s) deleted".</summary>
    public sealed class CadOverkillResult
    {
        internal CadOverkillResult(int duplicates, int overlaps)
        {
            Duplicates = duplicates;
            Overlaps = overlaps;
        }

        /// <summary>Gets how many objects were deleted for being the same as another one.</summary>
        public int Duplicates { get; }

        /// <summary>Gets how many objects or polyline segments were deleted or combined for overlapping another.</summary>
        public int Overlaps { get; }
    }

    /// <summary>What the command hands the window's OVERKILL dialog: the settings to show, which OK takes as they stand, and how many objects were selected.</summary>
    public sealed class CadOverkillRequest
    {
        /// <summary>Makes the request.</summary>
        /// <param name="settings">The settings the dialog shows and changes.</param>
        /// <param name="selected">How many objects were selected.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public CadOverkillRequest(CadOverkillSettings settings, int selected)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Selected = selected;
        }

        /// <summary>Gets the settings the dialog shows, a copy the dialog changes; OK makes them the session's.</summary>
        public CadOverkillSettings Settings { get; }

        /// <summary>Gets how many objects were selected.</summary>
        public int Selected { get; }

        /// <summary>Gets or sets what the dialog did: OK or Cancel.</summary>
        public CadDialogAction Action { get; set; }
    }
}
