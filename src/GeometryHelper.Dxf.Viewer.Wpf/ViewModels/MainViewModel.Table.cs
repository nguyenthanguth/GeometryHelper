using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Tables (C18a): TABLE asking for the Insert Table dialog, TABLESTYLE for the Table Style dialog, and the cells of a table
    /// selected on the canvas with the contextual Table Cell tab, as AutoCAD 2021's: a click in a cell selects it, Shift+click
    /// or a press dragged a range, the arrows move it, Delete empties it, F2 or a double click opens it for typing, Esc lets it
    /// go; the grips of the cells drag the table's lines and autofill, and a right click opens the cells' menu.
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

        /// <summary>
        /// A press in a cell dragged across others, with no command running: the range from the cell pressed to the cell under
        /// the cursor, held at the table's edge where the cursor leaves it, grown to every block of merged cells it meets, as
        /// AutoCAD 2021 selects it; the objects selected let go. Not an edit: no undo step.
        /// </summary>
        /// <param name="anchor">The point pressed, in the drawing.</param>
        /// <param name="cursor">The cursor, in the drawing.</param>
        /// <returns>Whether a range is selected.</returns>
        public bool SelectTableRange(GeoPoint2 anchor, GeoPoint2 cursor)
        {
            if (
                _current == null
                || _current.Session.IsRunning
                || !CadTableEditor.TryCellAt(
                    _current.Document.CurrentSpace,
                    anchor,
                    out string handle,
                    out int row,
                    out int column
                )
                || !(_current.Document.FindEntity(handle) is DxfTable table)
                || !CadTableEditor.CellNear(table, cursor, out int toRow, out int toColumn)
            )
            {
                return false;
            }

            if (
                _tableCell == null
                || !string.Equals(_tableCell.Handle, handle, StringComparison.OrdinalIgnoreCase)
                || !_tableCell.IsAnchoredAt(row, column)
            )
            {
                ClearSelection();
                ShowTableCell(new TableCellViewModel(this, handle, row, column));
            }

            _tableCell.ExtendTo(toRow, toColumn);
            return true;
        }

        /// <summary>
        /// Starts the drag of a grip of the cells selected: 0 the top edge, 1 the right, 2 the bottom and 3 the left edge, each
        /// moving that line of the table (the row above it, or the column left of it, changing alone), and 4 the autofill grip.
        /// </summary>
        /// <param name="grip">The grip, 0 to 4.</param>
        /// <returns>The drag; null where no cells are selected or a command runs.</returns>
        public CadTableDrag StartTableCellDrag(int grip)
        {
            TableCellViewModel cell = _tableCell;

            if (cell == null || _current == null || _current.Session.IsRunning)
            {
                return null;
            }

            CadDocument document = _current.Document;
            DxfCellRange range = cell.Range;

            switch (grip)
            {
                case 0:
                    return CadTableDrag.Row(document, cell.Handle, range.TopRow);
                case 1:
                    return CadTableDrag.Column(document, cell.Handle, range.RightColumn + 1, true);
                case 2:
                    return CadTableDrag.Row(document, cell.Handle, range.BottomRow + 1);
                case 3:
                    return CadTableDrag.Column(document, cell.Handle, range.LeftColumn, true);
                case 4:
                    return CadTableDrag.Autofill(document, cell.Handle, range);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets or sets what Columns › Resize and Rows › Resize of the cells' right-click menu ask of the canvas: the drag of a
        /// grip of the cells selected, 1 their right edge and 2 their bottom edge, started as though the grip had been clicked.
        /// The window gives it to <see cref="CadCanvas.DragCellGrip"/>; null for none, when the items do nothing.
        /// </summary>
        public Action<int> CellGripDragRequested { get; set; }

        /// <summary>
        /// Ends a drag of a table's grip let go or clicked again: made, one undo step, a refusal said on the command line; after
        /// an autofill the source and the cells filled are the cells selected.
        /// </summary>
        /// <param name="drag">The drag.</param>
        /// <returns>What came of it.</returns>
        public CadEditResult EndTableDrag(CadTableDrag drag)
        {
            if (drag == null)
            {
                throw new ArgumentNullException(nameof(drag));
            }

            CadEditResult result = drag.Commit();

            if (!result.Succeeded)
            {
                Say(result.Message);
            }
            else if (drag.Kind == CadTableDragKind.Autofill && drag.FillRange != null)
            {
                DxfCellRange s = drag.Source,
                    f = drag.FillRange;
                var cell = new TableCellViewModel(
                    this,
                    drag.Handle,
                    Math.Min(s.TopRow, f.TopRow),
                    Math.Min(s.LeftColumn, f.LeftColumn)
                );
                ShowTableCell(cell);
                cell.ExtendTo(Math.Max(s.BottomRow, f.BottomRow), Math.Max(s.RightColumn, f.RightColumn));
            }
            else
            {
                SyncTableCell();
            }

            return result;
        }

        /// <summary>
        /// The menu a right click opens while the autofill grip is dragged, as AutoCAD 2021's: Fill Series, Fill Series Without
        /// Formatting, Copy Cells, Copy Cells Without Formatting and Fill Formatting Only, the one chosen ticked; the choice lasts
        /// for this drag.
        /// </summary>
        /// <param name="drag">The drag.</param>
        /// <returns>The items; null for a drag of another kind.</returns>
        public IReadOnlyList<CadMenuItemViewModel> TableFillMenu(CadTableDrag drag)
        {
            if (drag == null || drag.Kind != CadTableDragKind.Autofill)
            {
                return null;
            }

            var choices = new[]
            {
                new KeyValuePair<string, DxfTableFillOptions>("Fill Series", CadTableDrag.FillSeries),
                new KeyValuePair<string, DxfTableFillOptions>(
                    "Fill Series Without Formatting",
                    DxfTableFillOptions.GenerateSeries
                ),
                new KeyValuePair<string, DxfTableFillOptions>(
                    "Copy Cells",
                    DxfTableFillOptions.CopyContent | DxfTableFillOptions.CopyFormat
                ),
                new KeyValuePair<string, DxfTableFillOptions>(
                    "Copy Cells Without Formatting",
                    DxfTableFillOptions.CopyContent
                ),
                new KeyValuePair<string, DxfTableFillOptions>("Fill Formatting Only", DxfTableFillOptions.CopyFormat),
            };
            var items = new List<CadMenuItemViewModel>();

            foreach (KeyValuePair<string, DxfTableFillOptions> choice in choices)
            {
                DxfTableFillOptions options = choice.Value;
                items.Add(
                    new CadMenuItemViewModel(
                        choice.Key,
                        () => drag.FillOptions = options,
                        isChecked: drag.FillOptions == options
                    )
                );
            }

            return items;
        }

        /// <summary>
        /// Table Indicator Color, of the right-click menu of a table selected: the Select Color dialog on the fill of the bands
        /// of column letters and row numbers, kept in Global settings › Appearance, not in the drawing.
        /// </summary>
        private void PickTableIndicatorColor()
        {
            if (PickColor == null)
            {
                Say("The Select Color dialog is shown by the window.");
                return;
            }

            int rgb = _settings.TableIndicatorColor;
            var dialog = new SelectColorViewModel(
                DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb),
                false
            );

            if (PickColor(dialog))
            {
                int chosen = dialog.Color.ColorValue & 0xFFFFFF;
                ChangeSettings(s => s.TableIndicatorColor = chosen);
            }
        }

        /// <summary>Whether a table is among the objects selected, which offers Table Indicator Color on the right-click menu.</summary>
        private bool TableSelected()
        {
            CadDocument document = _current?.Document;

            if (document == null)
            {
                return false;
            }

            foreach (string handle in document.Selection.Handles)
            {
                if (document.FindEntity(handle) is DxfTable)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The corners of the cells selected and of their table, for the canvas to draw; null where none are selected.</summary>
        /// <returns>The range's corners, then the table's, each four points in the plan; then the points of the lines between
        /// columns on the table's top edge and of the lines between rows on its left edge, its corners first and last.</returns>
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
