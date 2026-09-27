using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging.Algorithms;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Represents a label to be arranged along with surrounding geometric objects (obstacles).
    /// </summary>
    public partial class Arrange
    {
        /// <summary>Gets or sets the bounding rectangle of the label — the geometry to be translated.</summary>
        public GeoRectangle2 GeoRectangle2 { get; set; }

        /// <summary>
        /// Gets or sets the path segment of the object the label points to.
        /// Its midpoint is the origin for expanding candidate positions.
        /// </summary>
        public GeoLine2 GeoLine2 { get; set; }

        /// <summary>
        /// Gets or sets the minimum perpendicular clearance between the label edge and the path.
        /// Added to half the label height, it determines the distance from the path to the label center.
        /// <para>
        /// This property is defined per label rather than in <see cref="ArrangeOptions"/> because
        /// each label may require a unique offset — e.g., larger text labels may need to be placed further away than smaller ones.
        /// </para>
        /// </summary>
        public double BaseOffsetFromLine { get; set; } = 50.0;

        /// <summary>Gets or sets the list of static block polygons that the label must not overlap.</summary>
        public List<GeoPolygon2> BlockPolygons { get; set; }

        /// <summary>Gets or sets the list of static block lines that the label must not overlap.</summary>
        public List<GeoLine2> BlockLines { get; set; }

        /// <summary>
        /// Indicates whether the label has been successfully placed in a completely empty position.
        /// </summary>
        public bool Placed { get; private set; }

        /// <summary>
        /// Gets the translation vector calculated for the label.
        /// </summary>
        public GeoVector2 TranslationVector { get; internal set; } = GeoVector2.Zero;

        /// <summary>
        /// Sets the placement success status of the label.
        /// </summary>
        /// <param name="value">The success status to set.</param>
        internal void SetPlaced(bool value)
        {
            Placed = value;
        }

        /// <summary>Arranges the list of labels using the default configuration parameters.</summary>
        /// <param name="arranges">List of labels to be arranged.</param>
        /// <returns>List of translation GeoVectors for each label in the same input order.</returns>
        public static List<GeoVector2> Run(List<Arrange> arranges)
        {
            return Run(arranges, ArrangeOptions.Default);
        }

        /// <summary>
        /// Arranges the list of labels to ensure they do not overlap each other or any blocked regions.
        /// </summary>
        /// <param name="arranges">List of labels to be arranged.</param>
        /// <param name="options">Configuration options controlling the algorithm.</param>
        /// <returns>List of translation GeoVectors for each label in the same input order.</returns>
        public static List<GeoVector2> Run(List<Arrange> arranges, ArrangeOptions options)
        {
            if (arranges == null)
            {
                throw new ArgumentNullException(nameof(arranges));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Select arrangement algorithm based on options
            IArrangeAlgorithm algorithm;
            switch (options.Algorithm)
            {
                case ArrangeAlgorithmType.BoundedBacktracking:
                    algorithm = new BoundedBacktrackingAlgorithm();
                    break;
                case ArrangeAlgorithmType.SimulatedAnnealing:
                    algorithm = new SimulatedAnnealingAlgorithm();
                    break;
                case ArrangeAlgorithmType.ForceDirected:
                    algorithm = new ForceDirectedAlgorithm();
                    break;
                case ArrangeAlgorithmType.ConstraintSatisfaction:
                    algorithm = new ConstraintSatisfactionAlgorithm();
                    break;
                case ArrangeAlgorithmType.Greedy:
                default:
                    algorithm = new GreedyAlgorithm();
                    break;
            }

            // --- PASS 1: Strict arrangement with all constraints ---
            List<GeoVector2> translations = algorithm.Arrange(arranges, options);
            MarkPlacementResults(arranges, translations, options);

            // --- PASS 2: Relaxation pass for failed labels ---
            var failedArranges = arranges.Where(w => w is { Placed: false }).ToList();
            if (failedArranges.Count > 0)
            {
                RelaxFailedPlaced(arranges, failedArranges, translations, algorithm, options);
            }

            // Store the calculated translation vectors directly in the Arrange objects
            for (int i = 0; i < arranges.Count; i++)
            {
                if (arranges[i] != null)
                {
                    arranges[i].TranslationVector = translations[i];
                }
            }

            return translations;
        }

        /// <summary>
        /// Re-arranges only the failed labels by allowing them to overlap with their own BlockLines,
        /// while freezing successfully placed labels and treating them as static block obstacles.
        /// </summary>
        private static void RelaxFailedPlaced(
            List<Arrange> arranges,
            List<Arrange> failedArranges,
            List<GeoVector2> translations,
            IArrangeAlgorithm algorithm,
            ArrangeOptions options)
        {
            // Record the original indices of failed labels to map results back later
            var failedIndices = new List<int>();
            for (int i = 0; i < arranges.Count; i++)
            {
                if (arranges[i] != null && !arranges[i].Placed)
                {
                    failedIndices.Add(i);
                }
            }

            // Backup original static constraints of failed labels
            var backupPolygons = new Dictionary<Arrange, List<GeoPolygon2>>();
            var backupLines = new Dictionary<Arrange, List<GeoLine2>>();

            // Convert successfully placed labels into static block polygons for failed labels to avoid
            var greenBoxes = new List<GeoPolygon2>();
            for (int i = 0; i < arranges.Count; i++)
            {
                if (arranges[i] != null && arranges[i].Placed)
                {
                    var rect = arranges[i].GeoRectangle2.Translate(translations[i]);

                    greenBoxes.Add(new GeoPolygon2(rect.GetVertices()));
                }
            }

            // Set up relaxed constraints (clear BlockLines and add green boxes to BlockPolygons)
            foreach (var arrange in failedArranges)
            {
                backupPolygons[arrange] = arrange.BlockPolygons;
                backupLines[arrange] = arrange.BlockLines;

                // Relax: Allow overlaps with guide BlockLines
                arrange.BlockLines = new List<GeoLine2>();

                // Hard block: Do not overlap with successfully placed green labels
                var newPolygons = arrange.BlockPolygons != null 
                                  ? new List<GeoPolygon2>(arrange.BlockPolygons) 
                                  : new List<GeoPolygon2>();
                newPolygons.AddRange(greenBoxes);
                arrange.BlockPolygons = newPolygons;
            }

            // Re-run the arrangement algorithm ONLY for failed labels
            List<GeoVector2> translations2 = algorithm.Arrange(failedArranges, options);

            // Restore original constraints to prevent side-effects on input data
            foreach (var arrange in failedArranges)
            {
                arrange.BlockPolygons = backupPolygons[arrange];
                arrange.BlockLines = backupLines[arrange];
            }

            // Map relaxation results back into the main translations list
            for (int i = 0; i < failedArranges.Count; i++)
            {
                int originalIndex = failedIndices[i];
                translations[originalIndex] = translations2[i];
            }

            // Re-evaluate final Placed flags on the combined layout
            MarkPlacementResults(arranges, translations, options);
        }

        /// <summary>
        /// Re-evaluates final placement results on the final layout and marks Placed flags accordingly.
        /// <para>
        /// Each algorithm only knows the state at the time it places a label, so the flag set by them means
        /// "this spot was empty when my turn came". A label placed later, when stuck, might fallback to a position
        /// that overlaps an already placed label, causing the overlapped label to still report success.
        /// Users need to know if the final layout has overlaps, so the final verification must be done here,
        /// after all labels have settled.
        /// </para>
        /// </summary>
        /// <param name="arranges">The list of labels.</param>
        /// <param name="translations">The calculated translation vectors.</param>
        /// <param name="options">The arrangement options.</param>
        internal static void MarkPlacementResults(List<Arrange> arranges, IList<GeoVector2> translations, ArrangeOptions options)
        {
            var staticObstacles = Obstacle.CollectStatic(arranges);

            var finalBoxes = new GeoRectangle2[arranges.Count];
            var finalBounds = new Bounds[arranges.Count];
            for (int i = 0; i < arranges.Count; i++)
            {
                if (arranges[i] == null) continue;

                finalBoxes[i] = arranges[i].GeoRectangle2.Translate(translations[i]);
                finalBounds[i] = Bounds.Of(finalBoxes[i]);
            }

            for (int i = 0; i < arranges.Count; i++)
            {
                if (arranges[i] == null) continue;

                // A label that cannot form a layout has never been arranged. Just because it randomly
                // does not overlap anyone does not mean success, so it must be filtered out before collision checking.
                if (!arranges[i].TryGetLayout(options, out _))
                {
                    arranges[i].SetPlaced(false);
                    continue;
                }

                bool clean = !Obstacle.AnyCollides(staticObstacles, finalBoxes[i], options.Tolerance);

                for (int j = 0; j < arranges.Count && clean; j++)
                {
                    if (i == j || arranges[j] == null) continue;
                    if (!finalBounds[i].Overlaps(finalBounds[j])) continue;
                    if (finalBoxes[i].CollidesWith(finalBoxes[j], options.Tolerance)) clean = false;
                }

                arranges[i].SetPlaced(clean);
            }
        }
    }
}
