using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What a style of multilines is made of, to give <see cref="DxfDatabase.AddMlineStyle"/> a new style or
    /// <see cref="DxfMlineStyle.Apply"/> an edit of one, as AutoCAD's <c>MlineStyle</c> is set before it is added to the
    /// dictionary: its description, fill, caps, the angles of its ends, whether a line is drawn across each joint, and its
    /// elements.
    /// </summary>
    /// <remarks>
    /// A new one holds what AutoCAD's New Multiline Style dialog starts from, the style STANDARD: two elements 0.5 either side
    /// of the line drawn, each by layer in colour and linetype, square ends (90 degrees), no caps, no fill, no joints.
    /// <see cref="DxfMlineStyle.GetSettings"/> reads one from a style of a drawing. A style of 1 wide, the offsets 0.5 and
    /// -0.5, drawn at scale 20, is 20 wide.
    /// </remarks>
    public sealed class DxfMlineStyleSettings
    {
        /// <summary>The most elements AutoCAD gives a style.</summary>
        internal const int MostElements = 16;

        /// <summary>Initializes the settings of AutoCAD's new style, as the remarks say.</summary>
        public DxfMlineStyleSettings()
        {
            Elements.Add(new DxfMlineStyleElement(0.5, DxfColor.ByLayer, "BYLAYER"));
            Elements.Add(new DxfMlineStyleElement(-0.5, DxfColor.ByLayer, "BYLAYER"));
        }

        /// <summary>Gets or sets the description (group 3), as AutoCAD's <c>Description</c>; empty for none.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the multiline is filled between its outer elements, as AutoCAD's <c>Filled</c>.</summary>
        public bool Filled { get; set; }

        /// <summary>Gets or sets the colour of the fill, as AutoCAD's <c>FillColor</c>: by layer, by block or an index of 1 to 255.</summary>
        public DxfColor FillColor { get; set; } = DxfColor.ByLayer;

        /// <summary>Gets or sets the angle of the start to the line, in radians, as AutoCAD's <c>StartAngle</c>: pi / 2 square across.</summary>
        public double StartAngle { get; set; } = Math.PI / 2.0;

        /// <summary>Gets or sets the angle of the end to the line, in radians, as AutoCAD's <c>EndAngle</c>: pi / 2 square across.</summary>
        public double EndAngle { get; set; } = Math.PI / 2.0;

        /// <summary>Gets or sets whether a line is drawn across each joint, as AutoCAD's <c>ShowMiters</c>.</summary>
        public bool ShowMiters { get; set; }

        /// <summary>Gets or sets whether a line closes the start, as AutoCAD's <c>StartSquareCap</c>.</summary>
        public bool StartSquareCap { get; set; }

        /// <summary>Gets or sets whether arcs join the inner elements at the start, as AutoCAD's <c>StartInnerArcs</c>.</summary>
        public bool StartInnerArcs { get; set; }

        /// <summary>Gets or sets whether an arc joins the outer elements at the start, as AutoCAD's <c>StartRoundCap</c>.</summary>
        public bool StartRoundCap { get; set; }

        /// <summary>Gets or sets whether a line closes the end, as AutoCAD's <c>EndSquareCap</c>.</summary>
        public bool EndSquareCap { get; set; }

        /// <summary>Gets or sets whether arcs join the inner elements at the end, as AutoCAD's <c>EndInnerArcs</c>.</summary>
        public bool EndInnerArcs { get; set; }

        /// <summary>Gets or sets whether an arc joins the outer elements at the end, as AutoCAD's <c>EndRoundCap</c>.</summary>
        public bool EndRoundCap { get; set; }

        /// <summary>
        /// Gets the elements, from one to sixteen, each its offset from the line drawn, its colour and its linetype; written
        /// from the greatest offset to the least, as AutoCAD sorts them, two of one offset in the order given.
        /// </summary>
        public IList<DxfMlineStyleElement> Elements { get; } = new List<DxfMlineStyleElement>();

        /// <summary>
        /// Checks the settings as AutoCAD takes them: one to sixteen elements, finite offsets and angles, a colour that is an
        /// index or by layer or by block.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a setting is not one AutoCAD takes.</exception>
        internal void Check()
        {
            Guard.Argument(
                Description != null && Description.IndexOf('\n') < 0 && Description.IndexOf('\r') < 0,
                nameof(Description),
                "The description of a multiline style is a line of text."
            );
            Guard.Argument(
                Elements.Count >= 1 && Elements.Count <= MostElements,
                nameof(Elements),
                "A multiline style has one to "
                    + MostElements.ToString(CultureInfo.InvariantCulture)
                    + " elements: "
                    + Elements.Count.ToString(CultureInfo.InvariantCulture)
                    + " were given."
            );
            Guard.Argument(
                Guard.IsFinite(StartAngle) && Guard.IsFinite(EndAngle),
                nameof(StartAngle),
                "The angles of a multiline style are finite numbers."
            );
            Guard.Argument(
                FillColor != null && (FillColor.IsByLayer || FillColor.IsByBlock || FillColor.IsByAci),
                nameof(FillColor),
                "The fill of a multiline style is by layer, by block or of an index, as its record keeps one number."
            );

            foreach (DxfMlineStyleElement element in Elements)
            {
                Guard.Argument(element != null, nameof(Elements), "An element of a multiline style is not null.");
                Guard.Argument(
                    Guard.IsFinite(element.Offset),
                    nameof(Elements),
                    "The offset of an element is a finite number."
                );
                Guard.Argument(
                    element.Color.IsByLayer || element.Color.IsByBlock || element.Color.IsByAci,
                    nameof(Elements),
                    "The colour of an element is by layer, by block or an index, as the record keeps one number."
                );
            }
        }

        /// <summary>The groups of the style's own part as AutoCAD writes them, after its name: flags, description, fill, angles, count and elements.</summary>
        internal List<KeyValuePair<int, string>> Groups()
        {
            int flags =
                (Filled ? 1 : 0)
                | (ShowMiters ? 2 : 0)
                | (StartSquareCap ? 16 : 0)
                | (StartInnerArcs ? 32 : 0)
                | (StartRoundCap ? 64 : 0)
                | (EndSquareCap ? 256 : 0)
                | (EndInnerArcs ? 512 : 0)
                | (EndRoundCap ? 1024 : 0);
            var sorted = new List<DxfMlineStyleElement>(Elements);
            var order = new List<int>();

            for (int i = 0; i < sorted.Count; i++)
            {
                order.Add(i);
            }

            order.Sort(
                (a, b) =>
                {
                    int by = sorted[b].Offset.CompareTo(sorted[a].Offset);
                    return by != 0 ? by : a.CompareTo(b);
                }
            );

            var groups = new List<KeyValuePair<int, string>>
            {
                Pair(70, flags),
                new KeyValuePair<int, string>(3, Description),
                Pair(
                    62,
                    FillColor.IsByBlock ? 0
                        : FillColor.IsByLayer ? 256
                        : FillColor.ColorIndex
                ),
                new KeyValuePair<int, string>(51, Degrees(StartAngle)),
                new KeyValuePair<int, string>(52, Degrees(EndAngle)),
                Pair(71, sorted.Count),
            };

            foreach (int i in order)
            {
                DxfMlineStyleElement element = sorted[i];
                groups.Add(new KeyValuePair<int, string>(49, Numbers.Exact(element.Offset + 0.0)));
                groups.Add(
                    Pair(
                        62,
                        element.Color.IsByBlock ? 0
                            : element.Color.IsByLayer ? 256
                            : element.Color.ColorIndex
                    )
                );
                groups.Add(new KeyValuePair<int, string>(6, element.Linetype));
            }

            return groups;
        }

        // An angle of radians as the record keeps it, in degrees, rounded to twelve places so that pi / 3 is 60, as AutoCAD writes it.
        private static string Degrees(double radians) => Numbers.Exact(Math.Round(radians * 180.0 / Math.PI, 12) + 0.0);

        private static KeyValuePair<int, string> Pair(int code, int value) =>
            new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));
    }
}
