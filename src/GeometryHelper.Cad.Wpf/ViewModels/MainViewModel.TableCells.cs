using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What the cells of a table need of the window beyond the Table Cell tab itself: the cells held on the clipboard of the tab
    /// (Cut, Copy and Paste), and what shows its dialogs (Cell Border Properties, Table Cell Format, Insert a Block in a Table Cell,
    /// Create New Cell Style, Manage Cell Styles).
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets the cells Cut or Copy of the Table Cell tab took, which Paste gives to cells; null where none were. They are held while the window is open, for any of its drawings.</summary>
        public CadTableCellClip TableCellClip { get; set; }

        /// <summary>Gets or sets what shows the Cell Border Properties dialog and says whether it was answered; with none, the borders menu gives the lines it names.</summary>
        public Func<CellBordersViewModel, bool> ShowCellBorders { get; set; }

        /// <summary>Gets or sets what shows the Table Cell Format dialog (Data Format) and says whether it was answered.</summary>
        public Func<DataFormatViewModel, bool> ShowDataFormat { get; set; }

        /// <summary>Gets or sets what shows the Insert a Block in a Table Cell dialog and says whether it was answered.</summary>
        public Func<InsertTableBlockViewModel, bool> ShowInsertTableBlock { get; set; }

        /// <summary>Gets or sets what shows the Create New Cell Style dialog and says whether it was answered.</summary>
        public Func<CreateCellStyleViewModel, bool> ShowCreateCellStyle { get; set; }

        /// <summary>Gets or sets what shows the Manage Cell Styles dialog and says whether it was answered.</summary>
        public Func<ManageCellStylesViewModel, bool> ShowManageCellStyles { get; set; }
    }
}
