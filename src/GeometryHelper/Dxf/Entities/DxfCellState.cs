using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The state of a cell of a table, as AutoCAD's <c>CellStates</c> flags it: what its Cell Locking menu sets (Content Locked,
    /// Format Locked, or both) and what a link to data keeps. A table of R2007 or later holds it in its TABLECONTENT, the 90 after
    /// the cell's LINKEDTABLEDATACELL_BEGIN; <see cref="DxfTable.GetCellState"/> and <see cref="DxfTable.SetCellState"/> read and
    /// write it. The Unlocked choice of the menu is <see cref="None"/>, and Content and Format Locked is
    /// <see cref="ContentLocked"/> with <see cref="FormatLocked"/>, 17.
    /// </summary>
    [Flags]
    public enum DxfCellState
    {
        /// <summary>Unlocked: no flag, 0.</summary>
        None = 0,

        /// <summary>The content cannot be edited, 1.</summary>
        ContentLocked = 1,

        /// <summary>The content cannot be edited, as a link to data makes it, 2.</summary>
        ContentReadOnly = 2,

        /// <summary>The cell is linked to data, 4: kept by <see cref="DxfTable.SetCellState"/> as it stands, never set or cleared by it.</summary>
        Linked = 4,

        /// <summary>The content was changed after its link was updated, 8.</summary>
        ContentModifiedAfterUpdate = 8,

        /// <summary>The format cannot be changed, 16.</summary>
        FormatLocked = 16,

        /// <summary>The format cannot be changed, as a link to data makes it, 32.</summary>
        FormatReadOnly = 32,

        /// <summary>The format was changed after its link was updated, 64.</summary>
        FormatModifiedAfterUpdate = 64,
    }
}
