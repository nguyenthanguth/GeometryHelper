using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>The user coordinate systems of the drawing.</summary>
    public sealed partial class CadDocument
    {
        /// <summary>The name of the step and of the setting the records of a change of the user coordinate systems carry.</summary>
        internal const string UcsProperty = "UCS";

        private CadUcsManager _ucs;

        /// <summary>
        /// Gets the user coordinate systems of the drawing, read from it when first asked for, as <see cref="CadUcsManager"/>
        /// says: the current one of each space, the earlier ones and the named ones. A change to them is a step of undo, as in
        /// AutoCAD 2021, and is written into the drawing when it is saved.
        /// </summary>
        public CadUcsManager Ucs => _ucs ?? (_ucs = new CadUcsManager(this));

        /// <summary>
        /// Puts the systems back, or forward, as the records of a step say, where the step changed the drawing as well: the
        /// drawing read again for it does not hold them, as it holds the setting of a viewport (the steps of settings alone are
        /// put back by <see cref="MoveSettings"/>). The old systems from the last record back, or the new ones from the first on.
        /// </summary>
        private void MoveUcs(IReadOnlyList<CadEditRecord> records, bool undo)
        {
            for (int k = 0; k < records.Count; k++)
            {
                CadEditRecord record = records[undo ? records.Count - 1 - k : k];

                if (record.Replay != null && record.Property == UcsProperty)
                {
                    record.Replay(this, !undo);
                }
            }
        }
    }
}
