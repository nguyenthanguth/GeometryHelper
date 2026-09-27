using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    public sealed partial class ArrangeItem
    {
        /// <summary>
        /// Gets the positions the centre of the label is tried at, with the default options.
        /// </summary>
        /// <returns>The candidate centres, nearest row first; empty when the label cannot be arranged.</returns>
        public List<GeoPoint2> GetPlacePoints()
        {
            return GetPlacePoints(ArrangeOptions.Default);
        }

        /// <summary>
        /// Gets the positions the centre of the label is tried at: rows on either side of the leader, each sliding
        /// along it. Every algorithm chooses among these.
        /// </summary>
        /// <param name="options">The options setting the rows and how far they slide.</param>
        /// <returns>The candidate centres, nearest row first; empty when the label cannot be arranged.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
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
            if (Box.Width < options.MinimumBoxSize || Box.Height < options.MinimumBoxSize)
            {
                return false;
            }

            // STEP 2: Determine directional axis (unit GeoVector2) along the leader.
            // This axis points in the direction where the label can slide longitudinally.
            if (!Leader.Direction.TryGetNormal(out GeoVector2 direction))
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
            var vertices = Box.GetVertices();
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
                Leader.MidPoint, // Anchor point (midpoint of the leader)
                direction,        // Local longitudinal axis
                perpendicular,    // Local perpendicular axis
                height,           // Actual label height along perpendicular axis

                // BaseOffset: Minimum perpendicular distance from the path to the center of the first label row,
                // which equals half the label height plus this label's unique offset margin (Offset)
                height * 0.5 + Offset,

                // MaximumShift: Maximum allowable longitudinal shift distance along the path,
                // which equals half the path length plus a portion of the label width overshooting the ends (LongitudinalOvershootRatio)
                Leader.Length * 0.5 + width * options.LongitudinalOvershootRatio);

            return true;
        }

        /// <summary>
        /// Calculates the longer side of the axis-aligned box around the label.
        /// </summary>
        /// <returns>The longer side of the box around the label.</returns>
        internal double GetBoxSpan()
        {
            var vertices = Box.GetVertices();
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
}
