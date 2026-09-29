using System.Collections.Generic;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// A group laid out as one block, which is packed onto a sheet whole: which boxes of the group it holds, where the
    /// footprint of each sits in it, and how large it is.
    /// </summary>
    internal sealed class Cluster
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cluster"/> class.
        /// </summary>
        /// <param name="group">Which group of the input the boxes belong to.</param>
        /// <param name="members">Which boxes of the group the block holds, by their place in it.</param>
        /// <param name="offsets">Where the lower left corner of each footprint sits, from the lower left corner of the block.</param>
        /// <param name="width">The width of the block.</param>
        /// <param name="height">The height of the block.</param>
        /// <param name="kept">Whether the boxes stand as they were given, so that they may overlap one another.</param>
        internal Cluster(int group, IReadOnlyList<int> members, IReadOnlyList<(double X, double Y)> offsets, double width, double height, bool kept)
        {
            Group = group;
            Members = members;
            Offsets = offsets;
            Width = width;
            Height = height;
            Kept = kept;
        }

        /// <summary>Gets which group of the input the boxes belong to.</summary>
        internal int Group { get; }

        /// <summary>Gets which boxes of the group the block holds, by their place in it.</summary>
        internal IReadOnlyList<int> Members { get; }

        /// <summary>Gets where the lower left corner of each footprint sits, from the lower left corner of the block.</summary>
        internal IReadOnlyList<(double X, double Y)> Offsets { get; }

        /// <summary>Gets the width of the block.</summary>
        internal double Width { get; }

        /// <summary>Gets the height of the block.</summary>
        internal double Height { get; }

        /// <summary>Gets whether the boxes stand as they were given, so that they may overlap one another.</summary>
        internal bool Kept { get; }

        /// <summary>Gets the area of the block.</summary>
        internal double Area => Width * Height;
    }
}
