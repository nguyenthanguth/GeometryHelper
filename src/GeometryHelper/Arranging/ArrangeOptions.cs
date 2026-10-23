namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Parameters controlling candidate position generation and collision checking for label arrangement.
    /// Values are typically in millimeters, suitable for standard structural drawings.
    /// </summary>
    public sealed class ArrangeOptions
    {
        /// <summary>
        /// Gets the default options, a new instance each time it is read, as
        /// <see cref="Arranger.Run(System.Collections.Generic.IReadOnlyList{ArrangeItem})"/> runs with them.
        /// </summary>
        /// <remarks>
        /// A new instance, so that changing what it gives changes no other run, and so that its
        /// <see cref="Tolerance"/> is the one <see cref="GeometryHelper.Tolerance.Global"/> gives at the time, a
        /// <see cref="GeometryHelper.Tolerance.Use(GeometryHelper.Tolerance)"/> scope included. One instance for the
        /// whole process did neither: it kept the tolerance of whichever thread first read it.
        /// </remarks>
        public static ArrangeOptions Default => new ArrangeOptions();

        /// <summary>
        /// The gap between two consecutive label rows, added to the label height
        /// when shifting to the next perpendicular level. Helps prevent rows from overlapping.
        /// </summary>
        public double RowGap { get; set; } = 20.0;

        /// <summary>
        /// Number of perpendicular fallback levels to test on each side of the path.
        /// </summary>
        public int PerpendicularLevels { get; set; } = 3;

        /// <summary>
        /// Ratio of label width added to half of the path segment length to determine longitudinal sliding limits.
        /// Allows the label to overshoot the segment ends slightly.
        /// </summary>
        public double LongitudinalOvershootRatio { get; set; } = 0.75;

        /// <summary>
        /// Labels smaller than this size will be considered invalid/erroneous and ignored.
        /// </summary>
        public double MinimumBoxSize { get; set; } = 10.0;

        /// <summary>
        /// Shifts smaller than this distance will be ignored.
        /// Avoids minor adjustments for labels that are already in the correct position.
        /// </summary>
        public double MinimumMoveDistance { get; set; } = 0.1;

        /// <summary>
        /// Margin to expand the neighbor bounding box when selecting obstacles for collision checks.
        /// Added to the maximum dimension of the label.
        /// </summary>
        public double NeighbourMargin { get; set; } = 50.0;

        /// <summary>
        /// Maximum number of candidate positions generated for a label to prevent infinite loops.
        /// </summary>
        public int MaximumCandidates { get; set; } = 10000;

        /// <summary>
        /// Sorts placement order so that the most constrained label (fewest options) is placed first.
        /// Recommended as the greedy algorithm has no backtracking mechanism.
        /// Set to false to sort purely by default geometric order.
        /// </summary>
        public bool PlaceMostConstrainedFirst { get; set; } = true;

        /// <summary>
        /// Number of candidates evaluated when measuring label freedom degree,
        /// serving the <see cref="PlaceMostConstrainedFirst"/> option.
        /// </summary>
        public int FreedomSampleSize { get; set; } = 12;

        /// <summary>
        /// Number of empty positions evaluated before selection.
        /// Among these candidates, the position with the maximum clearance wins.
        /// Set to 1 to select the first found empty position immediately.
        /// </summary>
        public int LookAheadCandidates { get; set; } = 3;

        /// <summary>
        /// Sorts labels from the inside out (from area centroid to boundary).
        /// Helps labels in crowded center regions get priority placement.
        /// </summary>
        public bool PlaceFromInsideOut { get; set; } = true;

        /// <summary>
        /// Tolerance used for geometric calculations and intersection checks: <see cref="GeometryHelper.Tolerance.Global"/>
        /// as it stands when the options are made, unless set.
        /// </summary>
        /// <remarks>
        /// A place that comes within the point tolerance of a region or a line kept clear of collides with it, though
        /// their boxes do not overlap: at a tolerance of a thousandth, a label whose place ends half a thousandth below a
        /// line is not put there. Two labels are judged on their boxes as they stand.
        /// <para>
        /// Of the free places a label is weighed at, the most open wins, and clearances up to the point tolerance count as
        /// nought, so two places 0.0004 and 0.0007 off what they keep clear of are as open at a tolerance of a thousandth,
        /// and the one tried first wins. Clearances beyond it that differ by no more than a thousandth of it are as open
        /// too: places 0.5 and 0.50000001 clear at a thousandth. A run reads this tolerance throughout, never
        /// <see cref="GeometryHelper.Tolerance.Global"/> as it stands at the time.
        /// </para>
        /// </remarks>
        public Tolerance Tolerance { get; set; } = Tolerance.Global;
    }
}

