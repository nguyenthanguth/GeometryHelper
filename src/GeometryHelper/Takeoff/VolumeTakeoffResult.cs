using System;
using System.Collections.Generic;

namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// The take-off of one part: its own volume, what the parts ranked before it kept of it, and what is left.
    /// </summary>
    /// <remarks>
    /// A slab 6 000 by 6 000 by 200 met by a column 400 by 400 and a beam 300 wide and 6 000 long running through the
    /// column holds 7 200 000 000 gross; the column keeps 32 000 000 of it, the beam 336 000 000, its 360 000 000 within
    /// the slab less the 24 000 000 the column kept already, and 6 832 000 000 is left, 6.832 cubic metres.
    /// </remarks>
    public sealed class VolumeTakeoffResult
    {
        internal VolumeTakeoffResult(VolumeItem item, double grossVolume, double deductedVolume, double netVolume, VolumeDeduction[] deductions, string[] issues)
        {
            Item = item;
            GrossVolume = grossVolume;
            DeductedVolume = deductedVolume;
            NetVolume = netVolume;
            Deductions = Array.AsReadOnly(deductions);
            Issues = Array.AsReadOnly(issues);
        }

        /// <summary>
        /// Gets the item this is the take-off of; null for a null entry in the items.
        /// </summary>
        public VolumeItem Item { get; }

        /// <summary>
        /// Gets the volume of the part's own material, its openings cut out, as
        /// <see cref="Geometry.GeoSolid3.TryGetVolume(out double, Tolerance)"/> measures it within the options' tolerance.
        /// </summary>
        public double GrossVolume { get; }

        /// <summary>
        /// Gets the volume the parts ranked before this one kept of it: the <see cref="VolumeDeduction.Volume"/> of each
        /// of <see cref="Deductions"/>, added in their order.
        /// </summary>
        public double DeductedVolume { get; }

        /// <summary>
        /// Gets the volume left to the part: <see cref="GrossVolume"/> less <see cref="DeductedVolume"/>, held between
        /// nought and <see cref="GrossVolume"/>.
        /// </summary>
        /// <remarks>
        /// Every bit of material is counted once, by the part ranked first among those holding it, so the net volumes of
        /// all the parts add up to the volume of them all together, within the tolerance and the contact: an overlap
        /// thinner than either comes back as touching and nothing is taken off for it. Where <see cref="Issues"/> is not
        /// empty, it says which way the number may be out.
        /// </remarks>
        public double NetVolume { get; }

        /// <summary>
        /// Gets what each part ranked before this one kept of it, in the order of their ranks; empty where none did.
        /// </summary>
        public IReadOnlyList<VolumeDeduction> Deductions { get; }

        /// <summary>
        /// Gets what could not be worked out exactly, in plain English, each naming the other item by its index where
        /// there is one, as "overlap with #12 could not be worked out: not deducted, so the net volume is an upper bound".
        /// </summary>
        public IReadOnlyList<string> Issues { get; }

        /// <summary>
        /// Gets whether the take-off was worked out with nothing to report: <see cref="Issues"/> is empty.
        /// </summary>
        public bool IsExact => Issues.Count == 0;
    }
}
