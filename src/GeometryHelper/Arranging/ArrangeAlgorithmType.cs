namespace GeometryHelper.Arranging
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
}
