namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// What one part kept of another: the part, and the volume it took.
    /// </summary>
    /// <remarks>
    /// A slab 200 thick met by a column 400 by 400 and a beam 300 wide running into the column has two: 32 000 000 to
    /// the column, and to the beam what the beam shares with the slab less the 300 by 400 by 200 block the column took
    /// already, so that block is taken off the slab once.
    /// </remarks>
    public sealed class VolumeDeduction
    {
        internal VolumeDeduction(int byIndex, double volume)
        {
            ByIndex = byIndex;
            Volume = volume;
        }

        /// <summary>
        /// Gets the index, in the items given, of the part that kept the material.
        /// </summary>
        public int ByIndex { get; }

        /// <summary>
        /// Gets the volume that part took from this one: the material they share, less what parts ranked before it took
        /// already, so that it is disjoint from the other deductions of the same result.
        /// </summary>
        public double Volume { get; }
    }
}
