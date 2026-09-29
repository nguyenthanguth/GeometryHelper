using System;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// How the boxes are packed: the gaps between them, how a group is laid out, and the order the groups go in.
    /// </summary>
    /// <remarks>
    /// The gaps are in millimetres on paper, as the offsets of a <see cref="Sheet"/> are, and are multiplied by its
    /// <see cref="Sheet.Scale"/>.
    /// </remarks>
    public sealed class PackOptions
    {
        private const string GapMessage = "A spacing has to be a finite number, and cannot be negative.";

        private double _spacing;
        private double _groupSpacing;
        private GroupLayout _groupLayout = GroupLayout.Compact;

        /// <summary>
        /// Gets the default options, a new instance each time it is read, so that changing what it gives changes no
        /// other packing, and its <see cref="Tolerance"/> is <see cref="GeometryHelper.Tolerance.Global"/> as it
        /// stands at the time.
        /// </summary>
        public static PackOptions Default => new PackOptions();

        /// <summary>
        /// Gets or sets the least gap between two boxes of one group, in millimetres on paper; nought unless set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double Spacing
        {
            get => _spacing;
            set
            {
                Guard.NonNegative(value, nameof(value), GapMessage);
                _spacing = value;
            }
        }

        /// <summary>
        /// Gets or sets the least gap between two groups, in millimetres on paper; nought unless set. A gap wider than
        /// <see cref="Spacing"/> shows which boxes belong together.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double GroupSpacing
        {
            get => _groupSpacing;
            set
            {
                Guard.NonNegative(value, nameof(value), GapMessage);
                _groupSpacing = value;
            }
        }

        /// <summary>
        /// Gets or sets how the boxes of a group are laid out: afresh, as close together as they go, unless set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not one of <see cref="Packing.GroupLayout"/>.</exception>
        public GroupLayout GroupLayout
        {
            get => _groupLayout;
            set
            {
                if (value != GroupLayout.Compact && value != GroupLayout.Keep)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown layout.");
                }

                _groupLayout = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the groups are packed largest first rather than in the order they are given, a group
        /// split over sheets block by block; false unless set. Largest first packs tighter; in their order, the sheets
        /// read as the groups were given.
        /// </summary>
        public bool LargestGroupsFirst { get; set; }

        /// <summary>
        /// Gets or sets whether a group may go onto a sheet before the last one, where it finds room; false unless
        /// set, so that each sheet is filled before the next is begun and no group goes back. Filling earlier sheets
        /// can take fewer of them.
        /// </summary>
        public bool FillEarlierSheets { get; set; }

        /// <summary>
        /// Gets or sets the tolerance: how far a box may pass a free space, or the edge of the usable area, and still be
        /// taken to fit, so that sizes that add up exactly on paper still do in floating point.
        /// <see cref="GeometryHelper.Tolerance.Global"/> as it stands when the options are made, unless set.
        /// </summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Global;
    }
}
