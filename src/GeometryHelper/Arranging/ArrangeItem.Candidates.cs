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
        /// Gets the positions the centre of the label is tried at: rows on either side of the leader, or on the one side
        /// <see cref="Side"/> keeps the label to, each sliding along it. Every algorithm chooses among these.
        /// </summary>
        /// <remarks>
        /// The rows come nearest first, each straight across the middle of the leader and then a step back and a step
        /// forward along it in turn, further each time. Two rows as far off, one on each side, as every pair is when
        /// both sides have the same gap, are tried together, place by place, the side the perpendicular of the leader
        /// points to first.
        /// </remarks>
        /// <param name="options">The options setting the rows and how far they slide.</param>
        /// <returns>
        /// The candidate centres, nearest row first, no more than <see cref="ArrangeOptions.MaximumCandidates"/> of them;
        /// empty when the label cannot be arranged.
        /// </returns>
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
        /// Enumerates the positions the centre of the label is tried at, as <see cref="GetPlacePoints(ArrangeOptions)"/>
        /// lists them.
        /// </summary>
        /// <param name="options">The arrangement options.</param>
        /// <returns>An enumerable of candidate points.</returns>
        internal IEnumerable<GeoPoint2> EnumeratePlacePoints(ArrangeOptions options)
        {
            return EnumerateCandidates(options).Select(candidate => candidate.Centre);
        }

        /// <summary>
        /// Enumerates the candidates of the label, each with how much further off its side asks the label to stand.
        /// </summary>
        /// <param name="options">The arrangement options.</param>
        /// <returns>An enumerable of candidates.</returns>
        internal IEnumerable<Candidate> EnumerateCandidates(ArrangeOptions options)
        {
            // No more than MaximumCandidates, however many rows and slides there are. The cap is also what stops a slide
            // step of next to nothing from going on for ever.
            return EnumerateEveryCandidate(options).Take(Math.Max(0, options.MaximumCandidates));
        }

        /// <summary>
        /// Enumerates every candidate of the label, with no cap on how many.
        /// </summary>
        private IEnumerable<Candidate> EnumerateEveryCandidate(ArrangeOptions options)
        {
            if (!TryGetLayout(options, out Layout layout))
            {
                yield break;
            }

            double step = layout.SlideStep;
            // The rows of each side, none on a side the label is kept off.
            int positiveLevels = layout.PositiveOpen ? options.PerpendicularLevels : 0;
            int negativeLevels = layout.NegativeOpen ? options.PerpendicularLevels : 0;

            // What the first row of the side with the wider gap stands further off than that of the other; nought on
            // both sides when they stand as far off, and on a side the label is kept to, with no other to be weighed
            // against.
            bool bothOpen = layout.PositiveOpen && layout.NegativeOpen;
            double positiveSurplus = bothOpen ? Math.Max(0.0, layout.PositiveOffset - layout.NegativeOffset) : 0.0;
            double negativeSurplus = bothOpen ? Math.Max(0.0, layout.NegativeOffset - layout.PositiveOffset) : 0.0;

            // The next row on each side. Of the two, the nearer is tried first, and two as far off together, place by
            // place. Taking the rows level by level instead, as when both sides always stood as far off, tried the first
            // row of the far side before the second of the near one.
            int positive = 0;
            int negative = 0;
            while (positive < positiveLevels || negative < negativeLevels)
            {
                bool takePositive = positive < positiveLevels;
                bool takeNegative = negative < negativeLevels;
                if (takePositive && takeNegative)
                {
                    double upOffset = layout.GetRowOffset(true, positive, options.RowGap);
                    double downOffset = layout.GetRowOffset(false, negative, options.RowGap);
                    takePositive = !(downOffset < upOffset);
                    takeNegative = !(upOffset < downOffset);
                }

                if (takePositive && takeNegative)
                {
                    GeoVector2 up = layout.GetRow(true, positive++, options.RowGap);
                    GeoVector2 down = layout.GetRow(false, negative++, options.RowGap);

                    // Straight across, the side the perpendicular points to first, then sliding along the leader, a step
                    // back and a step forward in turn, the same side first each time.
                    yield return new Candidate(layout.Anchor + up, positiveSurplus);
                    yield return new Candidate(layout.Anchor + down, negativeSurplus);

                    for (double shift = step; shift <= layout.MaximumShift; shift += step)
                    {
                        yield return new Candidate(layout.Anchor + up - layout.Direction * shift, positiveSurplus);
                        yield return new Candidate(layout.Anchor + down - layout.Direction * shift, negativeSurplus);
                        yield return new Candidate(layout.Anchor + up + layout.Direction * shift, positiveSurplus);
                        yield return new Candidate(layout.Anchor + down + layout.Direction * shift, negativeSurplus);
                    }
                }
                else
                {
                    bool side = takePositive;
                    GeoVector2 across = layout.GetRow(side, side ? positive++ : negative++, options.RowGap);
                    double surplus = side ? positiveSurplus : negativeSurplus;

                    // A row on its own: straight across, then a step back and a step forward in turn.
                    yield return new Candidate(layout.Anchor + across, surplus);

                    for (double shift = step; shift <= layout.MaximumShift; shift += step)
                    {
                        yield return new Candidate(layout.Anchor + across - layout.Direction * shift, surplus);
                        yield return new Candidate(layout.Anchor + across + layout.Direction * shift, surplus);
                    }
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

            // STEP 4: The first row on each side: half the label height plus the gap of that side. The perpendicular
            // points up in the drawing (towards greater Y), or, the leader vertical, to the left (towards smaller X):
            // that side is the top, whichever way the leader runs. Vertical is to within the tolerance's angle, and
            // nearer vertical than level whatever that angle: the component of a unit vector is the sine of an angle,
            // which the tolerance of vectors, a length, is no measure of.
            double sine = Math.Abs(perpendicular.Y);
            bool vertical = sine <= options.Tolerance.EqualAngleSin && sine < Math.Abs(perpendicular.X);
            bool perpendicularIsTop = vertical ? perpendicular.X < 0.0 : perpendicular.Y > 0.0;
            double top = height * 0.5 + OffsetTop;
            double bottom = height * 0.5 + OffsetBottom;

            // STEP 5: Set up the complete Layout structure
            layout = new Layout(
                Leader.MidPoint, // Anchor point (midpoint of the leader)
                direction,        // Local longitudinal axis
                perpendicular,    // Local perpendicular axis
                height,           // Actual label height along perpendicular axis

                // The first row on the side the perpendicular points to, and on the other side.
                perpendicularIsTop ? top : bottom,
                perpendicularIsTop ? bottom : top,

                // MaximumShift: Maximum allowable longitudinal shift distance along the path,
                // which equals half the path length plus a portion of the label width overshooting the ends (LongitudinalOvershootRatio)
                Leader.Length * 0.5 + width * options.LongitudinalOvershootRatio,

                // Whether the label may stand on the side the perpendicular points to, and on the other side.
                perpendicularIsTop ? Side != ArrangeSide.Bottom : Side != ArrangeSide.Top,
                perpendicularIsTop ? Side != ArrangeSide.Top : Side != ArrangeSide.Bottom);

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
