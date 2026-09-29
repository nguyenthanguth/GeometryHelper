namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// One box the packing has placed: which it is, which sheet and block it went with, and the footprint of the box moved.
    /// </summary>
    internal readonly struct Spot
    {
        internal Spot(int group, int member, int sheet, int cluster, bool kept, Footprint box)
        {
            Group = group;
            Member = member;
            Sheet = sheet;
            Cluster = cluster;
            Kept = kept;
            Box = box;
        }

        /// <summary>Gets which group of the input the box belongs to.</summary>
        internal int Group { get; }

        /// <summary>Gets which box of the group it is.</summary>
        internal int Member { get; }

        /// <summary>Gets which sheet it went on.</summary>
        internal int Sheet { get; }

        /// <summary>Gets which block it went with, counted over the whole packing.</summary>
        internal int Cluster { get; }

        /// <summary>Gets whether its block keeps its boxes as they stood, so that they may overlap.</summary>
        internal bool Kept { get; }

        /// <summary>Gets the footprint of the box moved.</summary>
        internal Footprint Box { get; }
    }
}
