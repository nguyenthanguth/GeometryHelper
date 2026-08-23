using System;
using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using ArrangeAlgorithms.Algorithms;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms
{
    /// <summary>
    /// Represents a label to be arranged along with surrounding geometric objects (obstacles).
    /// </summary>
    public class Arrange
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
            var failedArranges = arranges.Where(w => w != null && !w.Placed).ToList();
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
        /// Collects and deduplicates static obstacles from the given list of labels.
        /// <para>
        /// Duplicate obstacles are deduplicated to retain only a single instance. A common library usage pattern
        /// is to assign the same set of blocked regions to every label — e.g., each label avoids all other path segments —
        /// causing the list to grow quadratically with the number of labels, even though the number of distinct
        /// geometries remains small. Deduplication here benefits all algorithms.
        /// </para>
        /// </summary>
        /// <param name="arranges">The list of labels containing obstacles.</param>
        /// <returns>A list of deduplicated obstacles.</returns>
        internal static List<Obstacle> CollectStaticObstacles(List<Arrange> arranges)
        {
            var occupied = new List<Obstacle>();
            if (arranges == null) return occupied;

            var seenPolygons = new HashSet<GeoPolygon2>();
            var seenLines = new HashSet<GeoLine2>();

            foreach (Arrange arrange in arranges)
            {
                if (arrange == null) continue;

                if (arrange.BlockPolygons != null)
                {
                    foreach (GeoPolygon2 block in arrange.BlockPolygons)
                    {
                        if (block != null && seenPolygons.Add(block))
                        {
                            occupied.Add(new Obstacle(block));
                        }
                    }
                }

                if (arrange.BlockLines != null)
                {
                    foreach (GeoLine2 block in arrange.BlockLines)
                    {
                        if (seenLines.Add(block))
                        {
                            occupied.Add(new Obstacle(block));
                        }
                    }
                }
            }
            return occupied;
        }

        /// <summary>
        /// Checks whether a translated rectangle collides with any of the static obstacles.
        /// </summary>
        /// <param name="obstacles">The list of static obstacles.</param>
        /// <param name="moved">The translated rectangle to check.</param>
        /// <param name="tolerance">The geometric tolerance.</param>
        /// <returns>True if a collision is detected; otherwise, false.</returns>
        internal static bool Collides(List<Obstacle> obstacles, GeoRectangle2 moved, Tolerance tolerance)
        {
            var movedBox = Bounds.Of(moved);

            foreach (Obstacle obstacle in obstacles)
            {
                // Rough filtering using bounding box (AABB) first to improve collision check performance
                if (!movedBox.Overlaps(obstacle.Box))
                {
                    continue;
                }

                // Detailed collision check based on specific geometric type
                switch (obstacle.Type)
                {
                    case ObstacleType.GeoRectangle2:
                        // OBB vs OBB: Using SAT (Separating Axis Theorem)
                        if (moved.CollidesWith(obstacle.GeoRectangle2, tolerance))
                            return true;
                        break;
                    case ObstacleType.GeoPolygon2:
                        // OBB vs Polygon: Check edge intersections and containment
                        if (moved.CollidesWith(obstacle.GeoPolygon2, tolerance))
                            return true;
                        break;
                    case ObstacleType.GeoLine2:
                        // OBB vs Line Segment: Check edge intersections and endpoints
                        if (moved.CollidesWith(obstacle.GeoLine2, tolerance))
                            return true;
                        break;
                }
            }

            return false;
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
            var staticObstacles = CollectStaticObstacles(arranges);

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

                bool clean = !Collides(staticObstacles, finalBoxes[i], options.Tolerance);

                for (int j = 0; j < arranges.Count && clean; j++)
                {
                    if (i == j || arranges[j] == null) continue;
                    if (!finalBounds[i].Overlaps(finalBounds[j])) continue;
                    if (finalBoxes[i].CollidesWith(finalBoxes[j], options.Tolerance)) clean = false;
                }

                arranges[i].SetPlaced(clean);
            }
        }

        /// <summary>Generates candidate position list with default configuration.</summary>
        public List<GeoPoint2> GetPlacePoints()
        {
            return GetPlacePoints(ArrangeOptions.Default);
        }

        /// <summary>Generates candidate positions for the label center.</summary>
        public List<GeoPoint2> GetPlacePoints(ArrangeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return EnumeratePlacePoints(options).ToList();
        }

        /// <summary>
        /// Enumerates candidate translation center points for the label layout.
        /// The point expansion process starts from the path segment midpoint (Anchor):
        /// - Perpendicular offset to create different label rows.
        /// - Longitudinal shift along the parallel path direction.
        /// </summary>
        /// <param name="options">The arrangement options.</param>
        /// <returns>An enumerable of candidate points.</returns>
        internal IEnumerable<GeoPoint2> EnumeratePlacePoints(ArrangeOptions options)
        {
            if (!TryGetLayout(options, out Layout layout))
            {
                yield break;
            }

            int produced = 0;

            // Calculate dynamic longitudinal shift step based on 5% of maximum shift.
            // Enforce a minimum protection threshold of 0.1 to avoid zero steps causing infinite loops.
            double step = layout.MaximumShift / 20.0;
            if (step < 0.1)
            {
                step = layout.Height;
            }

            // Iterate through each perpendicular distance level (each label row)
            for (int level = 0; level < options.PerpendicularLevels; level++)
            {
                double offset = layout.BaseOffset + level * (layout.Height + options.RowGap);

                // Pure perpendicular shift (no longitudinal shift): right and left sides
                yield return layout.Anchor + layout.Perpendicular * offset;
                yield return layout.Anchor + layout.Perpendicular * -offset;
                produced += 2;

                double shift = step;

                // Slide label longitudinally in both directions (forward and backward) parallel to object direction
                while (shift <= layout.MaximumShift && produced < options.MaximumCandidates)
                {
                    // Top/Right row - backward shift
                    yield return layout.Anchor + layout.Perpendicular * offset - layout.Direction * shift;
                    // Bottom/Left row - backward shift
                    yield return layout.Anchor + layout.Perpendicular * -offset - layout.Direction * shift;
                    // Top/Right row - forward shift
                    yield return layout.Anchor + layout.Perpendicular * offset + layout.Direction * shift;
                    // Bottom/Left row - forward shift
                    yield return layout.Anchor + layout.Perpendicular * -offset + layout.Direction * shift;

                    produced += 4;
                    shift += step;
                }
            }
        }

        /// <summary>
        /// Attempts to calculate the layout parameters of the label based on path and label dimensions.
        /// </summary>
        /// <param name="options">The arrangement options.</param>
        /// <param name="layout">The output layout parameters.</param>
        /// <returns>True if layout calculation is successful; otherwise, false.</returns>
        internal bool TryGetLayout(ArrangeOptions options, out Layout layout)
        {
            layout = default(Layout);

            // STEP 1: Initial validity check of label box dimensions.
            // If width or height is smaller than minimum configuration, ignore it to prevent division by zero or geometric distortion.
            // Strict comparison (<): label dimensions exactly equal to threshold are still valid.
            if (GeoRectangle2.Width < options.MinimumBoxSize || GeoRectangle2.Height < options.MinimumBoxSize)
            {
                return false;
            }

            // STEP 2: Determine directional axis (unit GeoVector2) along the label guide path (Anchor GeoLine2).
            // This axis points in the direction where the label can slide longitudinally.
            if (!GeoLine2.Direction.TryGetNormal(out GeoVector2 direction))
            {
                return false;
            }

            // Determine axis perpendicular to the label guide path.
            // This axis points in the direction to shift the label away or towards the object (forming label rows).
            GeoVector2 perpendicular = direction.GetPerpendicularVector();

            // Initialize extremum values to measure label bounding box after projection onto the new local coordinate system
            double alongMin = double.MaxValue;
            double alongMax = double.MinValue;
            double acrossMin = double.MaxValue;
            double acrossMax = double.MinValue;

            // STEP 3: Measure label bounding box in the new local coordinate system.
            // Iterate through 4 vertices of the label rectangle (which can be rotated at an arbitrary angle)
            var vertices = GeoRectangle2.GetVertices();
            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform vertex coordinates into a GeoVector2 from origin (0,0)
                GeoVector2 offset = new GeoPoint2(0.0, 0.0).GetVectorTo(vertices[i]);

                // Project vertex onto the longitudinal path axis (dot product)
                double along = offset.DotProduct(direction);

                // Project vertex onto the perpendicular path axis (dot product)
                double across = offset.DotProduct(perpendicular);

                // Update minimum and maximum coordinate bounds on both axes
                alongMin = Math.Min(alongMin, along);
                alongMax = Math.Max(alongMax, along);
                acrossMin = Math.Min(acrossMin, across);
                acrossMax = Math.Max(acrossMax, across);
            }

            // Calculate actual width and height of the label in the local coordinate system
            double width = alongMax - alongMin;
            double height = acrossMax - acrossMin;

            // Verify size after projection to ensure it does not degenerate to zero
            if (width < options.MinimumBoxSize || height < options.MinimumBoxSize)
            {
                return false;
            }

            // STEP 4: Set up the complete Layout structure
            layout = new Layout(
                GeoLine2.MidPoint, // Anchor point (midpoint of the guide path)
                direction,        // Local longitudinal axis
                perpendicular,    // Local perpendicular axis
                height,           // Actual label height along perpendicular axis

                // BaseOffset: Minimum perpendicular distance from the path to the center of the first label row,
                // which equals half the label height plus this label's unique offset margin (MarkOffsetFromLine)
                height * 0.5 + BaseOffsetFromLine,

                // MaximumShift: Maximum allowable longitudinal shift distance along the path,
                // which equals half the path length plus a portion of the label width overshooting the ends (LongitudinalOvershootRatio)
                GeoLine2.Length * 0.5 + width * options.LongitudinalOvershootRatio);

            return true;
        }

        /// <summary>
        /// Calculates the maximum diagonal dimension of the label's bounding box.
        /// </summary>
        /// <param name="options">The arrangement options.</param>
        /// <returns>The diagonal span of the bounding box.</returns>
        internal double GetBoxSpan(ArrangeOptions options)
        {
            var vertices = GeoRectangle2.GetVertices();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var v in vertices)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }

            return Math.Max(maxX - minX, maxY - minY);
        }
    }

    /// <summary>
    /// Internal structure containing base geometric layout information to generate candidates.
    /// </summary>
    internal readonly struct Layout
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Layout"/> struct.
        /// </summary>
        /// <param name="anchor">The anchor point on the path segment.</param>
        /// <param name="direction">The longitudinal direction axis of the path.</param>
        /// <param name="perpendicular">The perpendicular direction axis of the path.</param>
        /// <param name="height">The actual height of the label.</param>
        /// <param name="baseOffset">The base perpendicular offset from the path.</param>
        /// <param name="maximumShift">The maximum longitudinal shift distance.</param>
        internal Layout(GeoPoint2 anchor, GeoVector2 direction, GeoVector2 perpendicular,
            double height, double baseOffset, double maximumShift)
        {
            Anchor = anchor;
            Direction = direction;
            Perpendicular = perpendicular;
            Height = height;
            BaseOffset = baseOffset;
            MaximumShift = maximumShift;
        }

        /// <summary>Gets the anchor point on the path segment.</summary>
        internal GeoPoint2 Anchor { get; }
        /// <summary>Gets the longitudinal direction axis of the path.</summary>
        internal GeoVector2 Direction { get; }
        /// <summary>Gets the perpendicular direction axis of the path.</summary>
        internal GeoVector2 Perpendicular { get; }
        /// <summary>Gets the actual height of the label.</summary>
        internal double Height { get; }
        /// <summary>Gets the base perpendicular offset from the path.</summary>
        internal double BaseOffset { get; }
        /// <summary>Gets the maximum longitudinal shift distance.</summary>
        internal double MaximumShift { get; }
    }

    /// <summary>
    /// Specifies the type of geometric obstacle.
    /// </summary>
    internal enum ObstacleType
    {
        /// <summary>A polygon obstacle.</summary>
        GeoPolygon2,
        /// <summary>A line segment obstacle.</summary>
        GeoLine2,
        /// <summary>A rectangular obstacle.</summary>
        GeoRectangle2
    }

    /// <summary>
    /// Represents a static obstacle or an occupied label.
    /// </summary>
    internal readonly struct Obstacle
    {
        /// <summary>Gets the type of the obstacle.</summary>
        internal ObstacleType Type { get; }
        /// <summary>Gets the underlying polygon geometry if type is GeoPolygon2.</summary>
        internal GeoPolygon2 GeoPolygon2 { get; }
        /// <summary>Gets the underlying line segment geometry if type is GeoLine2.</summary>
        internal GeoLine2 GeoLine2 { get; }
        /// <summary>Gets the underlying rectangle geometry if type is GeoRectangle2.</summary>
        internal GeoRectangle2 GeoRectangle2 { get; }
        /// <summary>Gets the bounding box of the obstacle.</summary>
        internal Bounds Box { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a polygon.
        /// </summary>
        /// <param name="polygon">The polygon geometry.</param>
        internal Obstacle(GeoPolygon2 polygon)
        {
            Type = ObstacleType.GeoPolygon2;
            GeoPolygon2 = polygon;
            GeoLine2 = default(GeoLine2);
            GeoRectangle2 = default(GeoRectangle2);
            Box = Bounds.Of(polygon);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a line segment.
        /// </summary>
        /// <param name="line">The line segment geometry.</param>
        internal Obstacle(GeoLine2 line)
        {
            Type = ObstacleType.GeoLine2;
            GeoPolygon2 = null;
            GeoLine2 = line;
            GeoRectangle2 = default(GeoRectangle2);
            Box = Bounds.Of(line);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle geometry.</param>
        internal Obstacle(GeoRectangle2 rectangle)
        {
            Type = ObstacleType.GeoRectangle2;
            GeoPolygon2 = null;
            GeoLine2 = default(GeoLine2);
            GeoRectangle2 = rectangle;
            Box = Bounds.Of(rectangle);
        }
    }

    /// <summary>
    /// Represents an AABB bounding box for fast collision filtering.
    /// </summary>
    internal readonly struct Bounds
    {
        /// <summary>Gets the minimum X coordinate.</summary>
        internal double MinX { get; }
        /// <summary>Gets the minimum Y coordinate.</summary>
        internal double MinY { get; }
        /// <summary>Gets the maximum X coordinate.</summary>
        internal double MaxX { get; }
        /// <summary>Gets the maximum Y coordinate.</summary>
        internal double MaxY { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bounds"/> struct with coordinates.
        /// </summary>
        private Bounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// Creates a bounding box enclosing a polygon.
        /// </summary>
        /// <param name="GeoPolygon2">The polygon.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoPolygon2 GeoPolygon2)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int count = GeoPolygon2.VertexCount;
            for (int i = 0; i < count; i++)
            {
                var v = GeoPolygon2[i];
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }
            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Creates a bounding box enclosing a line segment.
        /// </summary>
        /// <param name="GeoLine2">The line segment.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoLine2 GeoLine2)
        {
            return new Bounds(
                Math.Min(GeoLine2.StartPoint.X, GeoLine2.EndPoint.X),
                Math.Min(GeoLine2.StartPoint.Y, GeoLine2.EndPoint.Y),
                Math.Max(GeoLine2.StartPoint.X, GeoLine2.EndPoint.X),
                Math.Max(GeoLine2.StartPoint.Y, GeoLine2.EndPoint.Y)
            );
        }

        /// <summary>
        /// Creates a bounding box enclosing a rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoRectangle2 rect)
        {
            var vertices = rect.GetVertices();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var v in vertices)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }
            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Creates a bounding box enclosing a list of points.
        /// </summary>
        /// <param name="points">The list of points.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Around(IReadOnlyList<GeoPoint2> points)
        {
            double minX = points[0].X;
            double minY = points[0].Y;
            double maxX = minX;
            double maxY = minY;

            for (int i = 1; i < points.Count; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }

            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Expands the bounds outward by a margin.
        /// </summary>
        /// <param name="margin">The margin to expand.</param>
        /// <returns>The expanded bounds.</returns>
        internal Bounds Expand(double margin)
        {
            return new Bounds(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);
        }

        /// <summary>
        /// Checks whether these bounds overlap other bounds.
        /// </summary>
        /// <param name="other">The other bounds to check overlap against.</param>
        /// <returns>True if they overlap; otherwise, false.</returns>
        internal bool Overlaps(Bounds other)
        {
            return MinX <= other.MaxX && other.MinX <= MaxX
                                      && MinY <= other.MaxY && other.MinY <= MaxY;
        }
    }
}
