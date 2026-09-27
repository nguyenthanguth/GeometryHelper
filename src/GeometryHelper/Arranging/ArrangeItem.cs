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
        /// Gets or sets the least gap between the edge of the label and the leader; 50 unless set. The first row of
        /// candidates lies half the height of the label plus this from the leader.
        /// <para>
        /// It belongs to the label rather than to <see cref="ArrangeOptions"/> because labels differ: a large text may
        /// have to stand further off than a small one.
        /// </para>
        /// </summary>
        public double Offset { get; set; } = 50.0;

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
    }
}
