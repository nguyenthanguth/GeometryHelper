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
        /// The label arrangement algorithm to be used.
        /// </summary>
        public ArrangeAlgorithmType Algorithm { get; set; } = ArrangeAlgorithmType.Greedy;

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
        /// The most steps back the two searching algorithms, bounded backtracking and constraint satisfaction, may take:
        /// each time a search takes up a label it has placed, to try it elsewhere, because a label after it found no
        /// place. Once they run out, the search gives up and the greedy algorithm places the labels.
        /// </summary>
        /// <remarks>
        /// Placing a label costs no step, so a search that never has to go back is never cut short, however many labels
        /// there are. Until 6.3.0 every label placed counted as a step, and a run of more labels than steps always gave
        /// up and was placed by the greedy algorithm.
        /// </remarks>
        public int MaxBacktrackSteps { get; set; } = 1000;

        /// <summary>
        /// Initial temperature for the Simulated Annealing algorithm.
        /// </summary>
        public double AnnealingInitialTemperature { get; set; } = 100.0;

        /// <summary>
        /// Cooling rate for the Simulated Annealing algorithm.
        /// </summary>
        public double AnnealingCoolingRate { get; set; } = 0.95;

        /// <summary>
        /// Number of iterations for physical force simulation in the Force-directed algorithm.
        /// </summary>
        public int ForceIterations { get; set; } = 100;

        /// <summary>
        /// Tolerance used for geometric calculations and intersection checks: <see cref="GeometryHelper.Tolerance.Global"/>
        /// as it stands when the options are made, unless set.
        /// </summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Global;
    }
}

