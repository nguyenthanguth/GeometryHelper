using CommonGeometry;

namespace ArrangeAlgorithms
{
    /// <summary>
    /// Label arrangement algorithm strategies.
    /// </summary>
    public enum ArrangeAlgorithmType
    {
        /// <summary>
        /// Greedy algorithm:
        /// <para>- How it works: Sorts labels by priority (freedom degree or geometry), then sequentially places each label at its best candidate position. Already placed labels become static obstacles for subsequent labels.</para>
        /// <para>- Advantages: Extremely fast, low computational complexity, stable and deterministic results.</para>
        /// <para>- Disadvantages: Easily falls into local optima; earlier placed labels take up good spots, causing later labels to be stuck in collisions.</para>
        /// </summary>
        Greedy,

        /// <summary>
        /// Bounded Backtracking algorithm:
        /// <para>- How it works: Places labels sequentially, but when the k-th label encounters a collision, the algorithm backtracks to the (k-1)-th label to try its next candidate position, making room. MaxBacktrackSteps is enforced to prevent thread hangs.</para>
        /// <para>- Advantages: Effectively overcomes the local optima of Greedy, increasing the success rate of collision-free arrangements.</para>
        /// <para>- Disadvantages: Slower than Greedy when dealing with extremely dense drawings requiring many backtrack attempts.</para>
        /// </summary>
        BoundedBacktracking,

        /// <summary>
        /// Simulated Annealing algorithm:
        /// <para>- How it works: Places all labels at their default positions. Runs random optimization based on simulated temperature cooling. In each cycle, randomly changes the position of a label and accepts the change based on global collision reduction (or Boltzmann probability if energy worsens).</para>
        /// <para>- Advantages: Excellent ability to find global optimal solutions even in heavily congested drawings with severe collisions.</para>
        /// <para>- Disadvantages: Non-deterministic results between different runs and consumes more CPU.</para>
        /// </summary>
        SimulatedAnnealing,

        /// <summary>
        /// Force-directed (Spring embedder) algorithm:
        /// <para>- How it works: Simulates a continuous physical system where labels repel each other (Coulomb force) and springs pull them back to their origin. After running simulation steps, performs Discrete Mapping of continuous label centers to the nearest non-colliding discrete candidate positions.</para>
        /// <para>- Advantages: Labels are distributed very naturally, evenly, and visually dynamically.</para>
        /// <para>- Disadvantages: Continuous physical force calculation is complex; sometimes labels can be pushed excessively if repulsion forces are too extreme.</para>
        /// </summary>
        ForceDirected,

        /// <summary>
        /// Constraint Satisfaction Problem (CSP) algorithm:
        /// <para>- How it works: Treats each label as a variable and discrete candidates as the domain. Runs the constraint satisfaction algorithm using MRV Heuristic (Minimum Remaining Values - variables with fewer choices are assigned first) combined with Forward Checking (prunes conflicting candidates in neighboring variables early).</para>
        /// <para>- Advantages: Solves the problem with extremely rigorous mathematical logic, optimizing constraints to avoid overlap completely.</para>
        /// <para>- Disadvantages: High algorithmic complexity; large number of labels can cause combinatorial explosion if domain size is too wide and deep.</para>
        /// </summary>
        ConstraintSatisfaction
    }

    /// <summary>
    /// Parameters controlling candidate position generation and collision checking for label arrangement.
    /// Values are typically in millimeters, suitable for standard structural drawings.
    /// </summary>
    public sealed class ArrangeOptions
    {
        /// <summary>
        /// Default configuration options, used by <see cref="Arrange.Run(System.Collections.Generic.List{Arrange})"/>.
        /// </summary>
        public static ArrangeOptions Default { get; } = new ArrangeOptions();

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
        /// Maximum backtracking steps for the Bounded Backtracking algorithm.
        /// </summary>
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
        /// Tolerance used for geometric calculations and intersection checks.
        /// </summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Global;
    }
}

