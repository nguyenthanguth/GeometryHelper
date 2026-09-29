using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// A label to place: the box it takes up, the leader it belongs to, and what it has to keep clear of.
    /// </summary>
    /// <remarks>
    /// <see cref="Arranger.Run(IReadOnlyList{ArrangeItem}, ArrangeOptions)"/> only reads it. Where the label goes
    /// comes back as an <see cref="ArrangeResult"/>, so the same items can be run again, with other options or on
    /// another thread, and are still what they were.
    /// </remarks>
    public sealed partial class ArrangeItem
    {
        private const string GapMessage = "A gap has to be a finite number.";

        private double _offset = 50.0;
        private double? _offsetTop;
        private double? _offsetBottom;

        /// <summary>
        /// Gets or sets the box of the label: the rectangle that is moved.
        /// </summary>
        public GeoRectangle2 Box { get; set; }

        /// <summary>
        /// Gets or sets the leader: the segment of the object the label belongs to. The candidate positions spread out
        /// from its midpoint, in rows on either side of it, each sliding along it.
        /// </summary>
        public GeoLine2 Leader { get; set; }

        /// <summary>
        /// Gets or sets the least gap between the edge of the label and the leader, on both sides of it; 50 unless set.
        /// The first row of candidates lies half the height of the label plus this from the leader. A side given its own
        /// gap, <see cref="OffsetTop"/> or <see cref="OffsetBottom"/>, takes that instead.
        /// <para>
        /// It belongs to the label rather than to <see cref="ArrangeOptions"/> because labels differ: a large text may
        /// have to stand further off than a small one.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
        public double Offset
        {
            get => _offset;
            set
            {
                Guard.Finite(value, nameof(value), GapMessage);
                _offset = value;
            }
        }

        /// <summary>
        /// Gets or sets the least gap between the edge of the label and the leader on the side of the leader that faces
        /// up in the drawing, towards greater Y; null, the default, takes <see cref="Offset"/>.
        /// </summary>
        /// <remarks>
        /// Which side faces up does not depend on which way the leader runs: one drawn from right to left has the same
        /// top as one drawn from left to right. A vertical leader, to within the angle of the options'
        /// <see cref="ArrangeOptions.Tolerance"/>, has its top on the left, towards smaller X, where the text of a
        /// vertical dimension stands.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
        public double? OffsetTop
        {
            get => _offsetTop;
            set
            {
                if (value.HasValue)
                {
                    Guard.Finite(value.Value, nameof(value), GapMessage);
                }

                _offsetTop = value;
            }
        }

        /// <summary>
        /// Gets or sets the least gap between the edge of the label and the leader on the side of the leader that faces
        /// down in the drawing, towards smaller Y, and for a vertical leader the right; null, the default, takes
        /// <see cref="Offset"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
        public double? OffsetBottom
        {
            get => _offsetBottom;
            set
            {
                if (value.HasValue)
                {
                    Guard.Finite(value.Value, nameof(value), GapMessage);
                }

                _offsetBottom = value;
            }
        }

        /// <summary>
        /// Gets or sets the regions the label must not overlap; empty unless set, and null reads as empty.
        /// </summary>
        /// <remarks>
        /// The regions of all the items are gathered into one set before any label is placed, so every label keeps
        /// clear of the regions of every item, and one region given to many items is tested once.
        /// </remarks>
        public IReadOnlyList<GeoPolygon2> BlockPolygons { get; set; } = Array.Empty<GeoPolygon2>();

        /// <summary>
        /// Gets or sets the segments the label must not overlap; empty unless set, and null reads as empty.
        /// </summary>
        /// <remarks>
        /// Gathered with those of every other item, as <see cref="BlockPolygons"/> are, but held less strictly: a label
        /// that finds no clear place is tried once more with the segments lifted, still keeping clear of the labels
        /// placed by then, and if it ends up across one it is reported not <see cref="ArrangeResult.Placed"/>.
        /// </remarks>
        public IReadOnlyList<GeoLine2> BlockLines { get; set; } = Array.Empty<GeoLine2>();

        /// <summary>
        /// Makes a copy of the label that keeps clear of other blocks, as the second pass of a run tries it. Every other
        /// property comes along as it stands, one added later included.
        /// </summary>
        /// <param name="polygons">The regions the copy keeps clear of.</param>
        /// <param name="lines">The segments the copy keeps clear of.</param>
        /// <returns>The copy.</returns>
        internal ArrangeItem WithBlocks(IReadOnlyList<GeoPolygon2> polygons, IReadOnlyList<GeoLine2> lines)
        {
            var copy = (ArrangeItem)MemberwiseClone();
            copy.BlockPolygons = polygons;
            copy.BlockLines = lines;
            return copy;
        }
    }
}
