using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
{
    /// <summary>
    /// Label arrangement algorithm based on Constraint Satisfaction Problem (CSP) theory.
    /// Applies backtracking search combined with MRV (Minimum Remaining Values) variable selection and Forward Checking pruning technique.
    /// </summary>
    internal class ConstraintSatisfactionAlgorithm : IArrangeAlgorithm
    {
        /// <summary>
        /// Represents a variable in the Constraint Satisfaction Problem context.
        /// </summary>
        private class CSPVariable
        {
            /// <summary>Gets the original index of the label.</summary>
            public int OriginalIndex { get; }
            /// <summary>Gets the label.</summary>
            public ArrangeItem Item { get; }
            /// <summary>Gets or sets the list of initial valid translation candidates (no static collisions).</summary>
            public List<GeoVector2> Domain { get; set; }
            /// <summary>Gets or sets the currently assigned translation vector.</summary>
            public GeoVector2 AssignedValue { get; set; }
            /// <summary>Gets or sets a value indicating whether this variable has been assigned.</summary>
            public bool IsAssigned { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="CSPVariable"/> class.
            /// </summary>
            /// <param name="originalIndex">The original index of the label.</param>
            /// <param name="item">The label.</param>
            public CSPVariable(int originalIndex, ArrangeItem item)
            {
                OriginalIndex = originalIndex;
                Item = item;
                Domain = new List<GeoVector2>();
                AssignedValue = GeoVector2.Zero;
                IsAssigned = false;
            }
        }

        /// <summary>
        /// The steps back the search has taken: the times it went back to a variable it had given a value, to try the next.
        /// </summary>
        private int _backtrackSteps;

        /// <summary>
        /// Arranges the labels using a constraint satisfaction algorithm.
        /// </summary>
        /// <param name="items">The labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        public GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            var translations = new GeoVector2[items.Count];
            // STEP 1: Collect initial static obstacles
            var staticObstacles = Obstacle.CollectStatic(items);

            // STEP 2: Initialize CSP variables and filter initial domains
            var variables = new List<CSPVariable>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                var v = new CSPVariable(i, item);
                GeoPoint2 centre = item.Box.Center;

                // Pre-filter obstacles out of the label's reach. The loop below runs
                // (number of candidates x number of obstacles) times, so filtering once here cuts most of the work.
                // Degenerate labels that cannot form bounds retain the full list.
                List<Obstacle> nearby = PlacementHeuristics.TryGetCandidateBounds(item, options, out Bounds region)
                    ? staticObstacles.Where(o => region.Overlaps(o.Box)).ToList()
                    : staticObstacles;

                foreach (GeoPoint2 candidate in item.EnumeratePlacePoints(options))
                {
                    GeoVector2 trans = centre.GetVectorTo(candidate);
                    GeoRectangle2 moved = item.Box.Translate(trans);

                    // Only add to domain if candidate does not collide with static obstacles from the start
                    if (!Obstacle.AnyCollides(nearby, moved, options.Tolerance))
                    {
                        v.Domain.Add(trans);
                    }
                }

                // Pre-sort domain: candidates with smaller longitudinal shift are sorted first
                v.Domain = v.Domain.OrderBy(t => t.Length).ToList();
                variables.Add(v);
            }

            _backtrackSteps = 0;

            // STEP 3: Solve the constraint satisfaction problem
            bool success = SolveCSP(variables, options);

            // STEP 4: If CSP fails completely (no clean solution),
            // automatically fallback to the greedy algorithm to maintain availability
            if (!success)
            {
                var greedy = new GreedyAlgorithm();
                return greedy.Arrange(items, options);
            }

            // STEP 5: Aggregate translation results
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;

                var variable = variables.Find(v => v.OriginalIndex == i);
                translations[i] = variable != null ? variable.AssignedValue : GeoVector2.Zero;
            }

            return translations;
        }

        /// <summary>
        /// A variable being tried, and where its trying stands: the values it had to try, which of them is next, and the
        /// domains of every variable as they stood before it was first given one.
        /// </summary>
        private sealed class Attempt
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Attempt"/> class.
            /// </summary>
            /// <param name="variable">The variable tried.</param>
            /// <param name="domains">The domains of every variable, by original index, before the variable was tried.</param>
            public Attempt(CSPVariable variable, Dictionary<int, List<GeoVector2>> domains)
            {
                Variable = variable;
                Values = variable.Domain;
                Domains = domains;
            }

            /// <summary>Gets the variable tried.</summary>
            public CSPVariable Variable { get; }

            /// <summary>Gets the values the variable had to try, its domain when its trying began.</summary>
            public List<GeoVector2> Values { get; }

            /// <summary>Gets the domains of every variable, by original index, as they stood before the variable was tried.</summary>
            public Dictionary<int, List<GeoVector2>> Domains { get; }

            /// <summary>Gets or sets which of <see cref="Values"/> is tried next.</summary>
            public int Next { get; set; }
        }

        /// <summary>
        /// Solves CSP using MRV heuristics and Forward Checking pruning.
        /// </summary>
        /// <remarks>
        /// A loop over a stack of the variables being tried rather than a call for each: a call for each went as deep
        /// as there were labels, and on some hosts a few hundred of them ran the stack dry.
        /// </remarks>
        private bool SolveCSP(List<CSPVariable> variables, ArrangeOptions options)
        {
            var attempts = new Stack<Attempt>();
            bool choosing = true;

            while (true)
            {
                if (choosing)
                {
                    // Check assignment count
                    var unassigned = variables.Where(v => !v.IsAssigned).ToList();
                    if (unassigned.Count == 0)
                    {
                        return true;
                    }

                    // HEURISTIC MRV: Select the unassigned variable with the smallest domain size
                    CSPVariable currentVar = unassigned.OrderBy(v => v.Domain.Count).First();

                    if (currentVar.Domain.Count == 0)
                    {
                        // Stuck: Unassigned variable has no valid candidates left. Back to the variable tried last.
                        if (!Retreat(attempts, variables, options))
                        {
                            return false;
                        }

                        choosing = false;
                        continue;
                    }

                    // Store backups of domains to restore during backtracking
                    attempts.Push(new Attempt(currentVar, variables.ToDictionary(v => v.OriginalIndex, v => v.Domain.ToList())));
                }

                Attempt attempt = attempts.Peek();
                if (attempt.Next >= attempt.Values.Count)
                {
                    // Every value of this variable failed: back to the variable tried before it.
                    attempts.Pop();
                    if (!Retreat(attempts, variables, options))
                    {
                        return false;
                    }

                    choosing = false;
                    continue;
                }

                // Try the next translation value in the current variable's domain
                GeoVector2 val = attempt.Values[attempt.Next++];
                CSPVariable current = attempt.Variable;
                current.AssignedValue = val;
                current.IsAssigned = true;

                // FORWARD CHECKING: Filter the domains of other unassigned variables
                bool forwardCheckOk = true;
                GeoRectangle2 currentRect = current.Item.Box.Translate(val);

                foreach (var otherVar in variables.Where(v => !v.IsAssigned))
                {
                    // Remove candidates of otherVar that collide with the newly assigned currentVar label
                    var newDomain = new List<GeoVector2>();
                    foreach (GeoVector2 otherVal in otherVar.Domain)
                    {
                        GeoRectangle2 otherRect = otherVar.Item.Box.Translate(otherVal);

                        if (!currentRect.CollidesWith(otherRect, options.Tolerance))
                        {
                            newDomain.Add(otherVal);
                        }
                    }

                    otherVar.Domain = newDomain;

                    // If the domain of any variable becomes empty -> fail early
                    if (otherVar.Domain.Count == 0)
                    {
                        forwardCheckOk = false;
                        break;
                    }
                }

                if (forwardCheckOk)
                {
                    // Assign the next variable
                    choosing = true;
                    continue;
                }

                // BACKTRACK: Restore the previous domain states, and try the next value
                Restore(attempt, variables);
                choosing = false;
            }
        }

        /// <summary>
        /// Goes back to the variable tried last, after the variables after it found no values: undoes its value, so that
        /// it tries its next.
        /// </summary>
        /// <remarks>
        /// A step back, and only that counts: giving a variable a value costs none, so a search that never has to go
        /// back is never cut short, however many labels there are. Each value given counting as a step, a run of more
        /// labels than steps gave up every time and fell back to the greedy algorithm.
        /// </remarks>
        /// <returns>False when no variable is left to go back to, or the steps back have run out.</returns>
        private bool Retreat(Stack<Attempt> attempts, List<CSPVariable> variables, ArrangeOptions options)
        {
            if (attempts.Count == 0)
            {
                return false;
            }

            _backtrackSteps++;
            if (_backtrackSteps > options.MaxBacktrackSteps)
            {
                return false;
            }

            Restore(attempts.Peek(), variables);
            return true;
        }

        /// <summary>
        /// Undoes the value of the variable tried, and the domains of every variable back to what they were before it.
        /// </summary>
        private static void Restore(Attempt attempt, List<CSPVariable> variables)
        {
            attempt.Variable.IsAssigned = false;
            foreach (var v in variables)
            {
                v.Domain = attempt.Domains[v.OriginalIndex].ToList();
            }
        }
    }
}
