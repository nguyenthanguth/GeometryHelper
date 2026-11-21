using System;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// How DCL's units become pixels: widths are counted in character widths and heights in character heights, as DCL counts
    /// them. The window sets the numbers of its dialog font; the defaults are those of a dialog font of 8 points.
    /// </summary>
    internal sealed class DclMetrics
    {
        internal DclMetrics()
        {
            TextWidth = text => (text ?? string.Empty).Length * CharWidth;
        }

        /// <summary>Gets new metrics with the defaults, for a session with no window.</summary>
        internal static DclMetrics Default => new DclMetrics();

        /// <summary>Gets or sets the width of one character, in device-independent pixels.</summary>
        internal double CharWidth { get; set; } = 6;

        /// <summary>Gets or sets the height of one character, in device-independent pixels.</summary>
        internal double CharHeight { get; set; } = 13;

        /// <summary>Gets or sets how wide a text is in the dialog font; by default its character count times <see cref="CharWidth"/>.</summary>
        internal Func<string, double> TextWidth { get; set; }
    }
}
