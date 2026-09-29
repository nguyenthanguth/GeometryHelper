using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// What a packing gives back: where each box goes, the sheets it took, and how much of them the boxes fill.
    /// </summary>
    public sealed class PackResult
    {
        internal PackResult(PackPlacement[][] placements, SheetFrame[] sheets, double utilization)
        {
            Placements = Array.AsReadOnly(placements.Select(group => (IReadOnlyList<PackPlacement>)Array.AsReadOnly(group)).ToArray());
            Sheets = Array.AsReadOnly(sheets);
            Utilization = utilization;
        }

        /// <summary>
        /// Gets where each box goes, in the shape the boxes were given: <c>Placements[g][i]</c> is the box
        /// <c>groups[g][i]</c>. A null group has no placements.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<PackPlacement>> Placements { get; }

        /// <summary>
        /// Gets the sheets the boxes took, in order, the first always among them: where each lies, and its usable area.
        /// </summary>
        public IReadOnlyList<SheetFrame> Sheets { get; }

        /// <summary>
        /// Gets how much of the usable area of the sheets the placed boxes fill, their areas summed over that of the
        /// sheets taken: 1 when they fill them exactly.
        /// </summary>
        public double Utilization { get; }
    }
}
