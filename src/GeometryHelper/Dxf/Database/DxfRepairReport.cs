using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What <see cref="DxfDatabase.Recover(string, out DxfRepairReport)"/> or <see cref="DxfDatabase.Audit"/> found wrong with a
    /// drawing, in the order it was found: each a <see cref="DxfRepair"/>, with how many there are and how many were fixed. A
    /// sound file gives an empty report.
    /// </summary>
    public sealed class DxfRepairReport
    {
        private readonly int[] _byKind;

        internal DxfRepairReport(List<DxfRepair> repairs)
        {
            Repairs = repairs.AsReadOnly();
            _byKind = new int[(int)DxfRepairKind.Unreadable + 1];

            foreach (DxfRepair repair in repairs)
            {
                _byKind[(int)repair.Kind]++;
                FixedCount += repair.IsFixed ? 1 : 0;
            }
        }

        /// <summary>Gets what was found, in the order it was found.</summary>
        public IReadOnlyList<DxfRepair> Repairs { get; }

        /// <summary>Gets how many things were found.</summary>
        public int Count => Repairs.Count;

        /// <summary>Gets how many of them were fixed.</summary>
        public int FixedCount { get; }

        /// <summary>
        /// Gets the report in one sentence, as AutoCAD ends its audit: "No errors found." for a sound file, else how many were
        /// found and fixed, "3 errors found, 3 fixed."
        /// </summary>
        public string Summary =>
            Count == 0
                ? "No errors found."
                : Count.ToString(CultureInfo.InvariantCulture)
                    + (Count == 1 ? " error" : " errors")
                    + " found, "
                    + FixedCount.ToString(CultureInfo.InvariantCulture)
                    + " fixed.";

        /// <summary>Counts what was found of one kind.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>How many.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of <see cref="DxfRepairKind"/>.</exception>
        public int CountOf(DxfRepairKind kind)
        {
            if ((int)kind < 0 || (int)kind >= _byKind.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A kind is one of DxfRepairKind.");
            }

            return _byKind[(int)kind];
        }

        /// <summary>Gives the summary.</summary>
        /// <returns>The summary.</returns>
        public override string ToString() => Summary;
    }
}
