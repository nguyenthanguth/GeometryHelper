using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// Judges the packing as it came out, box by box, so that a box reported placed is one that is: inside the usable area
    /// of its sheet, and clear of every other box but those of a kept block it belongs to.
    /// </summary>
    /// <remarks>
    /// The packing is built to that, and this is the check that it held: it reads the boxes as moved, not the places
    /// the packing meant for them.
    /// </remarks>
    internal static class PackJudge
    {
        /// <summary>
        /// Finds the placed boxes that are not where a placed box has to be.
        /// </summary>
        /// <param name="spots">The placed boxes.</param>
        /// <param name="sheet">The sheet they were packed onto.</param>
        /// <param name="tolerance">How far a box may pass the edge of its area, or into another box, and still be clear.</param>
        /// <returns>For each spot, whether it is out of its area or into another box.</returns>
        internal static bool[] FindFaults(IReadOnlyList<Spot> spots, Sheet sheet, double tolerance)
        {
            var faults = new bool[spots.Count];
            var areas = new Dictionary<int, Footprint>();
            for (int s = 0; s < spots.Count; s++)
            {
                if (!areas.TryGetValue(spots[s].Sheet, out Footprint area))
                {
                    area = sheet.GetUsableArea(spots[s].Sheet);
                    areas.Add(spots[s].Sheet, area);
                }

                Footprint box = spots[s].Box;
                faults[s] = !(box.MinX >= area.MinX - tolerance && box.MinY >= area.MinY - tolerance
                    && box.MaxX <= area.MaxX + tolerance && box.MaxY <= area.MaxY + tolerance);
            }

            // Sheet by sheet, the boxes left to right: a box is only compared with those that begin before it ends.
            foreach (IGrouping<int, int> onSheet in Enumerable.Range(0, spots.Count).GroupBy(s => spots[s].Sheet))
            {
                int[] order = onSheet.OrderBy(s => spots[s].Box.MinX).ToArray();
                for (int a = 0; a < order.Length; a++)
                {
                    Spot first = spots[order[a]];
                    for (int b = a + 1; b < order.Length && spots[order[b]].Box.MinX < first.Box.MaxX - tolerance; b++)
                    {
                        Spot second = spots[order[b]];
                        if (first.Kept && second.Cluster == first.Cluster)
                        {
                            continue;
                        }

                        if (first.Box.MinX < second.Box.MaxX - tolerance
                            && first.Box.MinY < second.Box.MaxY - tolerance && second.Box.MinY < first.Box.MaxY - tolerance)
                        {
                            faults[order[a]] = true;
                            faults[order[b]] = true;
                        }
                    }
                }
            }

            return faults;
        }
    }
}
