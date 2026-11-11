using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Tables (C18a): TABLE asking for the Insert Table dialog, TABLESTYLE for the Table Style dialog, and the cells of a table
    /// selected on the canvas with the contextual Table Cell tab, as AutoCAD 2021's: a click in a cell selects it, Shift+click
    /// a range, the arrows move it, Delete empties it, F2 or a double click opens it for typing, Esc lets it go.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadTableCellTab _tableCellTab;
        private TableCellViewModel _tableCell;

        /// <summary>Gets or sets what shows the Insert Table dialog and says whether it was answered (TABLE); with none, TABLE asks on the command line.</summary>
        public Func<InsertTableViewModel, bool> ShowInsertTable { get; set; }

        /// <summary>Gets the contextual Table Cell tab, shown while cells of a table are selected.</summary>
        public CadWorkspaceExtension TableCellTab
        {
            get
            {
                if (_tableCellTab == null)
                {
                    _tableCellTab = new CadTableCellTab();
                    _builtInTabs.Add(_tableCellTab);
                }

                return _tableCellTab;
            }
        }

        /// <summary>Gets the cells of a table selected now and what the Table Cell tab does to them; null where none are.</summary>
        public TableCellViewModel TableCell => _tableCell;

        /// <summary>Makes the Table Style dialog's view model over the drawing shown.</summary>
        /// <returns>The view model; null with no drawing.</returns>
        public TableStyleManagerViewModel CreateTableStyleManager() =>
            _current == null ? null : new TableStyleManagerViewModel(_current.Document);

        /// <summary>
        /// A click on the canvas with no command running: where it falls in a cell of a table of the space shown, the cell is
        /// selected (with Shift, the range from the cell selected before), the objects selected let go, and true given.
        /// </summary>
        /// <param name="point">The point clicked, in the drawing.</param>
        /// <param name="shift">Whether Shift was held.</param>
        /// <returns>Whether a cell was selected.</returns>
        public bool SelectTableCellAt(GeoPoint2 point, bool shift)
        {
            if (
                _current == null
                || _current.Session.IsRunning
                || !CadTableEditor.TryCellAt(
                    _current.Document.CurrentSpace,
                    point,
                    out string handle,
                    out int row,
                    out int column
                )
            )
            {
                return false;
            }

            if (
                shift
                && _tableCell != null
                && string.Equals(_tableCell.Handle, handle, StringComparison.OrdinalIgnoreCase)
            )
            {
                _tableCell.ExtendTo(row, column);
            }
            else
            {
                ClearSelection();
                ShowTableCell(new TableCellViewModel(this, handle, row, column));
            }

            return true;
        }

        /// <summary>A double click on the canvas: where it falls in a cell of a table, the cell is opened for typing (TABLEDIT), as F2 opens it.</summary>
        /// <param name="point">The point, in the drawing.</param>
        /// <returns>Whether an edit started.</returns>
        public bool EditTableCellAt(GeoPoint2 point)
        {
            CadCommandSession session = Session;

            if (
                session == null
                || session.IsRunning
                || !CadTableEditor.TryCellAt(_current.Document.CurrentSpace, point, out _, out _, out _)
            )
            {
                return false;
            }

            StartCommand("TABLEDIT");
            session.Feed(point);
            return session.Prompt?.InPlace != null;
        }

        /// <summary>
        /// A key while cells are selected and nothing is typed: the arrows move the cell selected, Shift with them grows the range,
        /// Delete empties the cells, F2 opens the cell for typing, Esc lets the cells go.
        /// </summary>
        /// <param name="key">Left, Right, Up, Down, Delete, F2 or Escape.</param>
        /// <param name="shift">Whether Shift was held.</param>
        /// <returns>Whether the key was taken.</returns>
        public bool TableCellKey(string key, bool shift)
        {
            TableCellViewModel cell = _tableCell;

            if (cell == null || _current == null || _current.Session.IsRunning)
            {
                return false;
            }

            switch (key)
            {
                case "Left":
                    return cell.Step(0, -1, shift);
                case "Right":
                    return cell.Step(0, 1, shift);
                case "Up":
                    return cell.Step(-1, 0, shift);
                case "Down":
                    return cell.Step(1, 0, shift);
                case "Delete":
                    cell.ClearContentsCommand.Execute(null);
                    return true;
                case "F2":
                    return cell.EditCell();
                case "Escape":
                    HideTableCell();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Lets the cells of a table selected go, the Table Cell tab hidden, as a click away from them does.</summary>
        public void ClearTableCell() => HideTableCell();

        /// <summary>The corners of the cells selected and of their table, for the canvas to draw; null where none are selected.</summary>
        /// <returns>The range's corners, then the table's, each four points in the plan.</returns>
        public GeoPoint2[][] TableCellOutline() => _tableCell?.Outline();

        /// <summary>Shows the Table Cell tab for the cells selected.</summary>
        internal void ShowTableCell(TableCellViewModel cell)
        {
            _tableCell = cell;
            ((CadTableCellTab)TableCellTab).Show(cell);
            SelectedTab = TableCellTab;
            Raise(nameof(TableCell));
            TableCellChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Hides the Table Cell tab, the cells let go.</summary>
        internal void HideTableCell()
        {
            if (_tableCell == null)
            {
                return;
            }

            _tableCell = null;

            if (_tableCellTab != null && _tableCellTab.IsTabVisible)
            {
                _tableCellTab.Hide();

                if (SelectedTab == _tableCellTab || SelectedTab == null)
                {
                    SelectedTab = null;
                }
            }

            Raise(nameof(TableCell));
            TableCellChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Raised when the cells selected change, so that the canvas draws them anew.</summary>
        public event EventHandler TableCellChanged;

        /// <summary>Keeps the cells selected in step with the drawing after an edit, an undo or a command: let go where their table is gone or no longer holds them.</summary>
        private void SyncTableCell()
        {
            if (_tableCell == null)
            {
                return;
            }

            if (_current == null || _current.Session.IsRunning || !_tableCell.IsValid(_current.Document))
            {
                HideTableCell();
                return;
            }

            _tableCell.Refresh();
            TableCellChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>TABLE asked for its dialog: shown over the window, its answer written into the request.</summary>
        private void OnInsertTableRequested(CadUiRequestEventArgs e)
        {
            if (ShowInsertTable != null && e.Payload is CadInsertTableRequest request)
            {
                e.Handled = ShowInsertTable(new InsertTableViewModel(request));
            }
        }
    }

    /// <summary>The contextual Table Cell tab, in green as a contextual tab, shown while cells of a table are selected.</summary>
    internal sealed class CadTableCellTab : CadWorkspaceExtension
    {
        public override string TabHeader => "Table Cell";

        public void Show(TableCellViewModel cell)
        {
            TabContent = cell;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}
