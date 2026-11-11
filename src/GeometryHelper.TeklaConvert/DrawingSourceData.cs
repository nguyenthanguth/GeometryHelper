using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// What <see cref="DrawingSourceConvert"/> reads of one Tekla part, in the current work plane and in millimetres.
    /// Nothing here touches Tekla, so a test can build one in code.
    /// </summary>
    internal sealed class TeklaPartData
    {
        /// <summary>The part's GUID.</summary>
        internal string Id { get; set; }

        /// <summary>The body, with its cuts and holes; null where Tekla's solid could not be read.</summary>
        internal GeoSolid3 Solid { get; set; }

        /// <summary>The part's coordinate system; null where Tekla gave one of parallel or zero axes.</summary>
        internal GeoCoordinateSystem3? Frame { get; set; }

        /// <summary>The name, as Part.Name.</summary>
        internal string Name { get; set; }

        /// <summary>The position mark, as the report property PART_POS.</summary>
        internal string Mark { get; set; }

        /// <summary>The profile, as Profile.ProfileString.</summary>
        internal string Profile { get; set; }

        /// <summary>The material, as Material.MaterialString.</summary>
        internal string Material { get; set; }

        /// <summary>The GUID of the assembly the part belongs to; null where there is none.</summary>
        internal string AssemblyId { get; set; }

        /// <summary>Whether the part is the main part of its assembly.</summary>
        internal bool IsMain { get; set; }

        /// <summary>The report property ASSEMBLY_POS; null where it was not read.</summary>
        internal string AssemblyPosition { get; set; }

        /// <summary>The report property LENGTH, in millimetres; null where it was not read.</summary>
        internal double? Length { get; set; }

        /// <summary>The report property WEIGHT, in kilograms; null where it was not read.</summary>
        internal double? Weight { get; set; }
    }

    /// <summary>
    /// What <see cref="DrawingSourceConvert"/> reads of one Tekla bolt group: the data its bolts are made from, as
    /// decision D3 of the drawing plan asks, rather than the faceted body Tekla draws.
    /// </summary>
    internal sealed class TeklaBoltGroupData
    {
        /// <summary>The bolt group's GUID.</summary>
        internal string Id { get; set; }

        /// <summary>Whether the group has bolts (BoltGroup.Bolt); a group without them is holes only.</summary>
        internal bool HasBolt { get; set; }

        /// <summary>The bolt's diameter (BoltSize), in millimetres.</summary>
        internal double Diameter { get; set; }

        /// <summary>How much larger than the bolt its hole is (BoltGroup.Tolerance), in millimetres.</summary>
        internal double HoleTolerance { get; set; }

        /// <summary>The bolt's length (BoltGroup.Length, or its cut length where Tekla leaves that nought, as it does for bolts it sizes itself), in millimetres.</summary>
        internal double Length { get; set; }

        /// <summary>The bolt standard, such as "8.8XOX".</summary>
        internal string Standard { get; set; }

        /// <summary>
        /// The group's coordinate system: X from its first position towards its second, Z along the bolts.
        /// </summary>
        internal GeoCoordinateSystem3 Frame { get; set; }

        /// <summary>The centre of each bolt where the group's plane cuts its axis (BoltPositions), in Tekla's order.</summary>
        internal IReadOnlyList<GeoPoint3> Positions { get; set; } = Array.Empty<GeoPoint3>();

        /// <summary>The corners of the body Tekla draws for the bolts, their heads, nuts and washers; empty for holes only.</summary>
        internal IReadOnlyList<GeoPoint3> BodyCorners { get; set; } = Array.Empty<GeoPoint3>();

        /// <summary>The GUID of the part bolted to (PartToBoltTo); null where it was not read.</summary>
        internal string PartToBoltToId { get; set; }

        /// <summary>The GUIDs of every part the group bolts: the part bolted to, the part bolted and the others.</summary>
        internal IReadOnlyList<string> BoltedPartIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// What <see cref="DrawingSourceConvert"/> reads of one Tekla weld.
    /// </summary>
    internal sealed class TeklaWeldData
    {
        /// <summary>The weld's GUID.</summary>
        internal string Id { get; set; }

        /// <summary>The body of the weld metal; null where Tekla's solid could not be read.</summary>
        internal GeoSolid3 Solid { get; set; }

        /// <summary>The weld's coordinate system; null where Tekla gave none that holds.</summary>
        internal GeoCoordinateSystem3? Frame { get; set; }

        /// <summary>The name of TypeAbove, such as "WELD_TYPE_FILLET".</summary>
        internal string TypeAbove { get; set; }

        /// <summary>The name of TypeBelow.</summary>
        internal string TypeBelow { get; set; }

        /// <summary>SizeAbove, in millimetres.</summary>
        internal double SizeAbove { get; set; }

        /// <summary>SizeBelow, in millimetres.</summary>
        internal double SizeBelow { get; set; }

        /// <summary>PrefixAboveLine, such as "a".</summary>
        internal string PrefixAbove { get; set; }

        /// <summary>PrefixBelowLine.</summary>
        internal string PrefixBelow { get; set; }

        /// <summary>LengthAbove, in millimetres; nought where the weld runs the whole joint.</summary>
        internal double LengthAbove { get; set; }

        /// <summary>LengthBelow, in millimetres.</summary>
        internal double LengthBelow { get; set; }

        /// <summary>The report property LENGTH, the length Tekla works out; null where it was not read.</summary>
        internal double? Length { get; set; }

        /// <summary>The GUID of the part welded to (MainObject).</summary>
        internal string MainPartId { get; set; }

        /// <summary>The GUID of the part welded on (SecondaryObject).</summary>
        internal string SecondaryPartId { get; set; }
    }

    /// <summary>
    /// Everything read for one call of <see cref="DrawingSourceConvert"/>, each object once, and what was left out and why.
    /// </summary>
    internal sealed class TeklaDrawingData
    {
        private readonly HashSet<string> _visited = new HashSet<string>(StringComparer.Ordinal);

        // Reason to the ids or names left out for it, both in ordinal order when written, so the one Debug line of a call
        // reads the same every run.
        private readonly SortedDictionary<string, List<string>> _skipped = new SortedDictionary<string, List<string>>(
            StringComparer.Ordinal
        );

        /// <summary>The parts read.</summary>
        internal List<TeklaPartData> Parts { get; } = new List<TeklaPartData>();

        /// <summary>The bolt groups read.</summary>
        internal List<TeklaBoltGroupData> BoltGroups { get; } = new List<TeklaBoltGroupData>();

        /// <summary>The welds read.</summary>
        internal List<TeklaWeldData> Welds { get; } = new List<TeklaWeldData>();

        /// <summary>
        /// Marks an object as read; false where it was read already, so that a part reached through its assembly and
        /// selected as well, or a bolt group reached through each part it bolts, is read once.
        /// </summary>
        internal bool Visit(string id) => _visited.Add(id);

        /// <summary>Counts an object left out, by the reason and its id or the name of its kind.</summary>
        internal void Skip(string reason, string what)
        {
            if (!_skipped.TryGetValue(reason, out List<string> list))
            {
                _skipped.Add(reason, list = new List<string>());
            }

            list.Add(what ?? string.Empty);
        }

        /// <summary>How many objects were left out.</summary>
        internal int SkippedCount
        {
            get
            {
                int count = 0;

                foreach (List<string> list in _skipped.Values)
                {
                    count += list.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// The one line that says what was left out: each reason with its count and its ids or kinds in ordinal order.
        /// </summary>
        internal string DescribeSkipped()
        {
            var reasons = new List<string>();

            foreach (KeyValuePair<string, List<string>> entry in _skipped)
            {
                string[] sorted = entry.Value.ToArray();
                Array.Sort(sorted, StringComparer.Ordinal);
                reasons.Add(entry.Value.Count + " " + entry.Key + " (" + string.Join(", ", sorted) + ")");
            }

            return string.Join("; ", reasons);
        }
    }
}
