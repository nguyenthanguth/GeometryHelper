using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A choice of the fill box of the Table Cell tab: None, by block, by layer or a colour of AutoCAD's index.</summary>
    public sealed class TableFillChoice
    {
        /// <summary>Makes a choice.</summary>
        /// <param name="name">What the box shows: None, ByBlock, Red, Color 8.</param>
        /// <param name="index">The colour's index (0 by block, 256 by layer); null for none.</param>
        public TableFillChoice(string name, int? index)
        {
            Name = name;
            Index = index;
        }

        /// <summary>Gets what the box shows.</summary>
        public string Name { get; }

        /// <summary>Gets the colour's index; null for none.</summary>
        public int? Index { get; }

        /// <summary>Gets the colour of the swatch, 0xRRGGBB; null for none.</summary>
        public int? Rgb =>
            Index.HasValue && Index.Value >= 1 && Index.Value <= 255
                ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)Index.Value).ColorValue & 0xFFFFFF
                : (int?)null;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The cells of a table selected on the canvas and the contextual Table Cell tab over them, as AutoCAD 2021's: Rows (Insert
    /// Above, Insert Below, Delete Row(s)), Columns (Insert Left, Insert Right, Delete Column(s)), Merge (Merge All, By Row,
    /// By Column; Unmerge Cells), and Cell Styles (the alignment, the borders and the fill). Each button is one undo step of
    /// <see cref="CadTableEditor"/>; what is not built is greyed in the tab with its reason.
    /// </summary>
    public sealed class TableCellViewModel : ObservableObject
    {
        /// <summary>The alignments, as the tab's menu lists them.</summary>
        public static readonly IReadOnlyList<string> Alignments = new[]
        {
            "Top Left",
            "Top Center",
            "Top Right",
            "Middle Left",
            "Middle Center",
            "Middle Right",
            "Bottom Left",
            "Bottom Center",
            "Bottom Right",
        };

        private readonly MainViewModel _model;
        private int _anchorRow;
        private int _anchorColumn;
        private int _row;
        private int _column;
        private DxfCellRange _range;

        /// <summary>Makes the selection of one cell of a table.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        public TableCellViewModel(MainViewModel model, string handle, int row, int column)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _anchorRow = _row = row;
            _anchorColumn = _column = column;
            FillChoices = new[]
            {
                new TableFillChoice("None", null),
                new TableFillChoice("ByBlock", 0),
                new TableFillChoice("ByLayer", 256),
                new TableFillChoice("Red", 1),
                new TableFillChoice("Yellow", 2),
                new TableFillChoice("Green", 3),
                new TableFillChoice("Cyan", 4),
                new TableFillChoice("Blue", 5),
                new TableFillChoice("Magenta", 6),
                new TableFillChoice("White", 7),
                new TableFillChoice("Color 8", 8),
                new TableFillChoice("Color 9", 9),
            };
            InsertAboveCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.InsertRow(d, Handle, Range.TopRow, false))
            );
            InsertBelowCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.InsertRow(d, Handle, Range.BottomRow, true))
            );
            DeleteRowsCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.DeleteRows(d, Handle, Range.TopRow, Range.BottomRow - Range.TopRow + 1))
            );
            InsertLeftCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.InsertColumn(d, Handle, Range.LeftColumn, false))
            );
            InsertRightCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.InsertColumn(d, Handle, Range.RightColumn, true))
            );
            DeleteColumnsCommand = new DelegateCommand(_ =>
                Do(d =>
                    CadTableEditor.DeleteColumns(d, Handle, Range.LeftColumn, Range.RightColumn - Range.LeftColumn + 1)
                )
            );
            MergeAllCommand = new DelegateCommand(_ => Do(d => CadTableEditor.Merge(d, Handle, Range)));
            MergeByRowCommand = new DelegateCommand(_ => MergeEach(true));
            MergeByColumnCommand = new DelegateCommand(_ => MergeEach(false));
            UnmergeCommand = new DelegateCommand(_ => Do(d => CadTableEditor.Unmerge(d, Handle, Range)));
            ClearContentsCommand = new DelegateCommand(_ => Do(d => CadTableEditor.ClearContents(d, Handle, Range)));
            AlignCommand = new DelegateCommand(p => Align(p as string));
            BordersCommand = new DelegateCommand(p => Borders(p as string));
            SizeColumnsEquallyCommand = new DelegateCommand(_ => SizeEqually(true));
            SizeRowsEquallyCommand = new DelegateCommand(_ => SizeEqually(false));
            RemoveOverridesCommand = new DelegateCommand(_ =>
                Do(d => CadTableEditor.RemoveOverrides(d, Handle, Range))
            );
            CutCommand = new DelegateCommand(_ => Cut());
            CopyCommand = new DelegateCommand(_ => Copy());
            PasteCommand = new DelegateCommand(_ => Paste(), _ => _model.TableCellClip != null);
            MatchCellCommand = new DelegateCommand(_ => RunOnCell("MATCHCELL"));
            LockCommand = new DelegateCommand(p => Lock(p as string));
            BordersDialogCommand = new DelegateCommand(_ => BordersDialog());
            DataFormatCommand = new DelegateCommand(p => DataFormat(p as string));
            InsertBlockCommand = new DelegateCommand(_ => InsertBlock());
            InsertFieldCommand = new DelegateCommand(_ => InsertField());
            InsertFormulaCommand = new DelegateCommand(p => InsertFormula(p as string));
            LinkCellCommand = new DelegateCommand(_ => LinkCell());
            DownloadCommand = new DelegateCommand(_ => UpdateLinks(false));
            UploadCommand = new DelegateCommand(_ => UpdateLinks(true));
            DetachLinkCommand = new DelegateCommand(_ => DetachLink());
            CellStyleCommand = new DelegateCommand(p => SetCellStyle(p as string));
            CreateCellStyleCommand = new DelegateCommand(_ => CreateCellStyle());
            ManageCellStylesCommand = new DelegateCommand(_ => ManageCellStyles());
            Refresh();
        }

        /// <summary>Gets the handle of the table.</summary>
        public string Handle { get; }

        /// <summary>Gets the cells selected: from the first cell clicked to the last, grown to the blocks of merged cells it meets.</summary>
        public DxfCellRange Range => _range;

        /// <summary>Gets the name of the cells, C3, or B3:C4 for a range.</summary>
        public string CellName =>
            _range.TopRow == _range.BottomRow && _range.LeftColumn == _range.RightColumn
                ? CadTableEditor.CellName(_range.TopRow, _range.LeftColumn)
                : CadTableEditor.CellName(_range.TopRow, _range.LeftColumn)
                    + ":"
                    + CadTableEditor.CellName(_range.BottomRow, _range.RightColumn);

        /// <summary>The By Row/Column entry of the Cell Styles box: the cells take the style of their row.</summary>
        public const string ByRow = "By Row/Column";

        /// <summary>The entry of the Cell Styles box that opens the Create New Cell Style dialog.</summary>
        public const string CreateNewStyle = "Create New Cell Style…";

        /// <summary>The entry of the Cell Styles box that opens the Manage Cell Styles dialog.</summary>
        public const string ManageStyles = "Manage Cell Styles…";

        /// <summary>Gets the cell style of the first cell, as the Cell Styles box shows it: that its TABLECONTENT names, else its row's, Title, Header or Data.</summary>
        public string CellStyleName
        {
            get
            {
                DxfTable table = Table;
                return table == null ? "Data" : CadTableEditor.CellStyleOf(table, _range.TopRow, _range.LeftColumn);
            }
        }

        /// <summary>Gets the entries of the Cell Styles box: the cell styles of the table style, then Create New Cell Style… and Manage Cell Styles….</summary>
        public IReadOnlyList<string> CellStyleChoices
        {
            get
            {
                var list = new List<string>(CadTableEditor.CellStyleNames(Table));
                list.Add(CreateNewStyle);
                list.Add(ManageStyles);
                return list;
            }
        }

        /// <summary>
        /// Gets or sets the cell style of the Cell Styles box; set, the cells selected take it (one undo step), and the last two
        /// entries open the Create New Cell Style and Manage Cell Styles dialogs.
        /// </summary>
        public string CellStyle
        {
            get => CellStyleName;
            set
            {
                if (value == CreateNewStyle)
                {
                    CreateCellStyle();
                }
                else if (value == ManageStyles)
                {
                    ManageCellStyles();
                }
                else if (value != null && value != CellStyleName)
                {
                    SetCellStyle(value);
                }

                Raise(nameof(CellStyle));
            }
        }

        /// <summary>Gets the alignment of the first cell, as the alignment button shows it: Middle Center.</summary>
        public string AlignmentName
        {
            get
            {
                DxfTable table = Table;

                if (table == null)
                {
                    return Alignments[4];
                }

                DxfRowType kind = table.RowType(_range.TopRow);
                DxfCellAlignment alignment = table.Alignment(_range.TopRow, _range.LeftColumn)
                    ?? (table.TableStyle as DxfTableStyle)?.Alignment(kind)
                    ?? (kind == DxfRowType.DataRow ? DxfCellAlignment.TopCenter : DxfCellAlignment.MiddleCenter);
                return Alignments[(int)alignment - 1];
            }
        }

        /// <summary>Gets the choices of the fill box.</summary>
        public IReadOnlyList<TableFillChoice> FillChoices { get; }

        /// <summary>Gets or sets the fill of the first cell, as the fill box shows it; set, every cell selected is filled so, one undo step.</summary>
        public TableFillChoice Fill
        {
            get
            {
                DxfTable table = Table;
                bool? none = table?.IsBackgroundColorNone(_range.TopRow, _range.LeftColumn);
                DxfColor color = table?.BackgroundColor(_range.TopRow, _range.LeftColumn);

                if (none != false || color == null)
                {
                    return FillChoices[0];
                }

                foreach (TableFillChoice choice in FillChoices)
                {
                    if (choice.Index == color.ColorIndex)
                    {
                        return choice;
                    }
                }

                return new TableFillChoice(
                    "Color " + color.ColorIndex.ToString(CultureInfo.InvariantCulture),
                    color.ColorIndex
                );
            }
            set
            {
                if (value == null || value.Name == Fill.Name)
                {
                    return;
                }

                DxfColor color = value.Index.HasValue
                    ? (
                        value.Index.Value == 0 ? DxfColor.ByBlock
                        : value.Index.Value == 256 ? DxfColor.ByLayer
                        : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)value.Index.Value)
                    )
                    : null;
                Do(d => CadTableEditor.SetBackground(d, Handle, Range, color));
            }
        }

        /// <summary>Gets whether the cells selected are two or more, which Merge needs.</summary>
        public bool CanMerge => _range.TopRow != _range.BottomRow || _range.LeftColumn != _range.RightColumn;

        /// <summary>Gets Insert Above.</summary>
        public DelegateCommand InsertAboveCommand { get; }

        /// <summary>Gets Insert Below.</summary>
        public DelegateCommand InsertBelowCommand { get; }

        /// <summary>Gets Delete Row(s).</summary>
        public DelegateCommand DeleteRowsCommand { get; }

        /// <summary>Gets Insert Left.</summary>
        public DelegateCommand InsertLeftCommand { get; }

        /// <summary>Gets Insert Right.</summary>
        public DelegateCommand InsertRightCommand { get; }

        /// <summary>Gets Delete Column(s).</summary>
        public DelegateCommand DeleteColumnsCommand { get; }

        /// <summary>Gets Merge All.</summary>
        public DelegateCommand MergeAllCommand { get; }

        /// <summary>Gets Merge By Row: each row of the range merged on its own.</summary>
        public DelegateCommand MergeByRowCommand { get; }

        /// <summary>Gets Merge By Column: each column of the range merged on its own.</summary>
        public DelegateCommand MergeByColumnCommand { get; }

        /// <summary>Gets Unmerge Cells.</summary>
        public DelegateCommand UnmergeCommand { get; }

        /// <summary>Gets Delete All Contents, as Delete does.</summary>
        public DelegateCommand ClearContentsCommand { get; }

        /// <summary>Gets the alignment of the menu, given its name: Top Left to Bottom Right.</summary>
        public DelegateCommand AlignCommand { get; }

        /// <summary>
        /// Gets the borders of the menu, given their name, as the border buttons of the Modify Table Style dialog: All, Outside,
        /// Inside, Bottom, Left, Top, Right drawn; None hides every line of the cells.
        /// </summary>
        public DelegateCommand BordersCommand { get; }

        /// <summary>Gets Columns › Size Equally: every column of the range made as wide as their mean, the range's width kept, one undo step.</summary>
        public DelegateCommand SizeColumnsEquallyCommand { get; }

        /// <summary>Gets Rows › Size Equally: every row of the range made as high as their mean, none below what its text needs, one undo step.</summary>
        public DelegateCommand SizeRowsEquallyCommand { get; }

        /// <summary>Gets Cut: the cells selected copied to the tab's clipboard and their contents taken out, one undo step.</summary>
        public DelegateCommand CutCommand { get; }

        /// <summary>Gets Copy: the cells selected, with their contents and looks, held on the tab's clipboard.</summary>
        public DelegateCommand CopyCommand { get; }

        /// <summary>Gets Paste: the cells held given to the cells selected, from the top left one, one undo step.</summary>
        public DelegateCommand PasteCommand { get; }

        /// <summary>Gets Match Cell: MATCHCELL, with the first cell selected the source.</summary>
        public DelegateCommand MatchCellCommand { get; }

        /// <summary>Gets Cell Locking, given the choice's name: Unlocked, Content Locked, Format Locked or Content and Format Locked.</summary>
        public DelegateCommand LockCommand { get; }

        /// <summary>Gets Cell Borders…: the Cell Border Properties dialog.</summary>
        public DelegateCommand BordersDialogCommand { get; }

        /// <summary>Gets Data Format, given the name of a kind of data (Angle, Currency, Date, Decimal Number, General, Percentage, Point, Text, Whole Number: its usual format at once) or none for the Table Cell Format dialog.</summary>
        public DelegateCommand DataFormatCommand { get; }

        /// <summary>Gets Insert › Block…: the Insert a Block in a Table Cell dialog.</summary>
        public DelegateCommand InsertBlockCommand { get; }

        /// <summary>Gets Insert › Field…: the Field dialog, its field put in the first cell.</summary>
        public DelegateCommand InsertFieldCommand { get; }

        /// <summary>Gets Insert › Formula, given its kind: Sum, Average, Count, Cell or Equation; EDITTABLECELL then asks for the cells.</summary>
        public DelegateCommand InsertFormulaCommand { get; }

        /// <summary>Gets the choice of a cell style, given its name or none for By Row/Column.</summary>
        public DelegateCommand CellStyleCommand { get; }

        /// <summary>Gets Create New Cell Style….</summary>
        public DelegateCommand CreateCellStyleCommand { get; }

        /// <summary>Gets Manage Cell Styles….</summary>
        public DelegateCommand ManageCellStylesCommand { get; }

        /// <summary>Gets the locking of the first cell, as the Cell Locking button shows it: Unlocked, Content Locked, Format Locked or Content and Format Locked.</summary>
        public string LockName
        {
            get
            {
                DxfTable table = Table;
                DxfCellState state =
                    table == null ? DxfCellState.None : table.GetCellState(_range.TopRow, _range.LeftColumn);
                bool content = (state & (DxfCellState.ContentLocked | DxfCellState.ContentReadOnly)) != 0;
                bool format = (state & (DxfCellState.FormatLocked | DxfCellState.FormatReadOnly)) != 0;
                return content && format ? "Content and Format Locked"
                    : content ? "Content Locked"
                    : format ? "Format Locked"
                    : "Unlocked";
            }
        }

        /// <summary>Gets Remove All Property Overrides: what the cells hold of their own over the table style taken away, one undo step.</summary>
        public DelegateCommand RemoveOverridesCommand { get; }

        /// <summary>Gets Link Cell: a data link picked (Select a Data Link) and the cell linked to it, its data put in from the cell.</summary>
        public DelegateCommand LinkCellCommand { get; }

        /// <summary>Gets Download from Source: the data links of the table updated from their sources, as Update Table Data Links does.</summary>
        public DelegateCommand DownloadCommand { get; }

        /// <summary>Gets Upload to Source: the table's cells of each data link that allows it written back to its source.</summary>
        public DelegateCommand UploadCommand { get; }

        /// <summary>Gets Detach Data Link: the link of the cell's range taken away, the cells keeping what they hold.</summary>
        public DelegateCommand DetachLinkCommand { get; }

        /// <summary>
        /// Gets the right-click menu of the cells, as AutoCAD 2021's: Cut, Copy and Paste; Cell Style, Background Fill,
        /// Alignment, Borders, Locking, Data Format; Match Cell, Remove All Property Overrides, Data Link; Insert, Edit Text,
        /// Manage Content, Delete All Contents; Columns and Rows; Merge and Unmerge; Properties and Quick Properties. What the
        /// presenter does not build is grey, a grey item that opens a menu never opens it.
        /// </summary>
        /// <returns>The items, made anew each time the menu opens.</returns>
        public IReadOnlyList<CadMenuItemViewModel> Menu()
        {
            DxfTable table = Table;
            CadMenuItemViewModel Grey(string name, string reason) =>
                new CadMenuItemViewModel(name, () => { }, false, reason);
            CadMenuItemViewModel Item(string name, Action run, bool on = true, string keys = null) =>
                new CadMenuItemViewModel(name, run, on, shortcut: keys);
            CadMenuItemViewModel Line() => CadMenuItemViewModel.Separator();

            bool everyRow = table != null && _range.TopRow == 0 && _range.BottomRow == table.NumRows - 1;
            bool everyColumn = table != null && _range.LeftColumn == 0 && _range.RightColumn == table.NumColumns - 1;
            bool oneRow = _range.TopRow == _range.BottomRow;
            bool oneColumn = _range.LeftColumn == _range.RightColumn;
            string shared = SharedAlignment(table);
            var alignments = new List<CadMenuItemViewModel>();

            foreach (string name in Alignments)
            {
                string chosen = name;
                alignments.Add(new CadMenuItemViewModel(name, () => Align(chosen), isChecked: name == shared));
            }

            return new[]
            {
                Item("Cut", () => CutCommand.Execute(null), table != null, "Ctrl+X"),
                Item("Copy", () => CopyCommand.Execute(null), table != null, "Ctrl+C"),
                Item("Paste", () => PasteCommand.Execute(null), _model.TableCellClip != null, "Ctrl+V"),
                Line(),
                new CadMenuItemViewModel("Cell Style", true, CellStyleItems(table)),
                Item("Background Fill…", PickFill),
                new CadMenuItemViewModel("Alignment", alignments),
                Item("Borders…", BordersDialog),
                new CadMenuItemViewModel("Locking", true, LockItems()),
                Item("Data Format…", () => DataFormat(null)),
                Line(),
                Item("Match Cell", () => RunOnCell("MATCHCELL"), table != null),
                Item("Remove All Property Overrides", () => RemoveOverridesCommand.Execute(null)),
                table != null && table.GetDataLink(_row, _column) != null
                    ? new CadMenuItemViewModel("Data Links", true, DataLinkItems(table))
                    : new CadMenuItemViewModel("Data Link…", () => LinkCellCommand.Execute(null), table != null),
                Line(),
                new CadMenuItemViewModel(
                    "Insert",
                    true,
                    new[]
                    {
                        Item("Block…", InsertBlock, table != null),
                        Item("Field…", InsertField, table != null && _model.ShowField != null),
                        new CadMenuItemViewModel(
                            "Formula",
                            table != null,
                            new[]
                            {
                                Item("Sum", () => InsertFormula("Sum")),
                                Item("Average", () => InsertFormula("Average")),
                                Item("Count", () => InsertFormula("Count")),
                                Item("Cell", () => InsertFormula("Cell")),
                                Item("Equation", () => InsertFormula("Equation")),
                            }
                        ),
                    }
                ),
                Item("Edit Text", () => EditCell()),
                Grey("Manage Content…", "Not built: a cell holds one content here, so there is none to manage."),
                Item("Delete All Contents", () => ClearContentsCommand.Execute(null), keys: "Del"),
                Line(),
                new CadMenuItemViewModel(
                    "Columns",
                    true,
                    new[]
                    {
                        Item("Insert Left", () => InsertLeftCommand.Execute(null)),
                        Item("Insert Right", () => InsertRightCommand.Execute(null)),
                        Item("Delete", () => DeleteColumnsCommand.Execute(null), !everyColumn),
                        Line(),
                        Item("Size Equally", () => SizeColumnsEquallyCommand.Execute(null), !oneColumn),
                        Item("Resize", () => _model.CellGripDragRequested?.Invoke(1), table != null),
                    }
                ),
                new CadMenuItemViewModel(
                    "Rows",
                    new[]
                    {
                        Item("Insert Above", () => InsertAboveCommand.Execute(null)),
                        Item("Insert Below", () => InsertBelowCommand.Execute(null)),
                        Item("Delete", () => DeleteRowsCommand.Execute(null), !everyRow),
                        Line(),
                        Item("Size Equally", () => SizeRowsEquallyCommand.Execute(null), !oneRow),
                        Item("Resize", () => _model.CellGripDragRequested?.Invoke(2), table != null),
                    }
                ),
                Line(),
                new CadMenuItemViewModel(
                    "Merge",
                    CanMerge,
                    new[]
                    {
                        Item("All", () => MergeAllCommand.Execute(null)),
                        Item("By Row", () => MergeByRowCommand.Execute(null), !oneColumn),
                        Item("By Column", () => MergeByColumnCommand.Execute(null), !oneRow),
                    }
                ),
                Item("Unmerge", () => UnmergeCommand.Execute(null), MeetsMerged(table)),
                Line(),
                Item("Properties", () => _model.IsPropertiesPanelShown = true, keys: "Ctrl+1"),
                Grey(
                    "Quick Properties",
                    "Not built: Quick Properties shows the properties of objects, not those of cells; the Table Cell tab sets them."
                ),
            };
        }

        /// <summary>The Cell Style submenu of the right-click menu: By Row/Column, each cell style (the one the first cell takes checked), then Create New Cell Style… and Manage Cell Styles….</summary>
        private IReadOnlyList<CadMenuItemViewModel> CellStyleItems(DxfTable table)
        {
            var items = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel(ByRow, () => SetCellStyle(null), table != null),
            };
            string current = table == null ? null : CellStyleName;

            foreach (string name in CadTableEditor.CellStyleNames(table))
            {
                string chosen = name;
                items.Add(
                    new CadMenuItemViewModel(
                        name,
                        () => SetCellStyle(chosen),
                        table != null,
                        isChecked: name == current
                    )
                );
            }

            items.Add(CadMenuItemViewModel.Separator());
            items.Add(
                new CadMenuItemViewModel(
                    CreateNewStyle,
                    CreateCellStyle,
                    table != null && _model.ShowCreateCellStyle != null
                )
            );
            items.Add(
                new CadMenuItemViewModel(
                    ManageStyles,
                    ManageCellStyles,
                    table != null && _model.ShowManageCellStyles != null
                )
            );
            return items;
        }

        /// <summary>The Locking submenu: Unlocked, Content Locked, Format Locked and Content and Format Locked, the one the first cell holds checked.</summary>
        private IReadOnlyList<CadMenuItemViewModel> LockItems()
        {
            string current = LockName;
            var items = new List<CadMenuItemViewModel>();

            foreach (
                string name in new[] { "Unlocked", "Content Locked", "Format Locked", "Content and Format Locked" }
            )
            {
                string chosen = name;
                items.Add(
                    new CadMenuItemViewModel(name, () => Lock(chosen), Table != null, isChecked: name == current)
                );
            }

            return items;
        }

        /// <summary>Cut: the cells copied to the tab's clipboard and emptied, one undo step.</summary>
        private void Cut()
        {
            CadTableCellClip taken = null;
            Do(d =>
            {
                CadEditResult result = CadTableEditor.Cut(d, Handle, Range, out CadTableCellClip clip);
                taken = clip;
                return result;
            });

            if (taken != null)
            {
                _model.TableCellClip = taken;
                PasteCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Copy: the cells held on the tab's clipboard.</summary>
        private void Copy()
        {
            DxfTable table = Table;

            if (table != null)
            {
                _model.TableCellClip = CadTableEditor.Copy(table, Range);
                PasteCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Paste: the cells held given to the cells selected, from the top left one.</summary>
        private void Paste()
        {
            CadTableCellClip clip = _model.TableCellClip;

            if (clip != null)
            {
                Do(d => CadTableEditor.Paste(d, Handle, Range, clip));
            }
        }

        /// <summary>Cell Locking: the cells selected locked or unlocked as the choice says.</summary>
        private void Lock(string name)
        {
            DxfCellState state =
                name == "Content Locked" ? DxfCellState.ContentLocked
                : name == "Format Locked" ? DxfCellState.FormatLocked
                : name == "Content and Format Locked" ? DxfCellState.ContentLocked | DxfCellState.FormatLocked
                : DxfCellState.None;
            Do(d => CadTableEditor.SetLocking(d, Handle, Range, state));
        }

        /// <summary>A command of the Table Cell tab that asks for cells (MATCHCELL, EDITTABLECELL), started with the first cell selected as its first answer.</summary>
        private bool RunOnCell(string command)
        {
            DxfTable table = Table;

            if (table == null || _model.Session == null || _model.Session.IsRunning)
            {
                return false;
            }

            IReadOnlyList<GeoPoint3> corners = table.GetCellExtents(_range.TopRow, _range.LeftColumn, true);
            var middle = new GeoPoint2((corners[0].X + corners[2].X) / 2.0, (corners[0].Y + corners[2].Y) / 2.0);
            _model.StartCommand(command);
            _model.Session.Feed(middle);
            return true;
        }

        /// <summary>Insert › Formula: EDITTABLECELL asks for the cells the formula reads (or for the equation).</summary>
        private void InsertFormula(string kind)
        {
            if (RunOnCell("EDITTABLECELL") && !string.IsNullOrEmpty(kind))
            {
                _model.Execute(kind);
            }
        }

        /// <summary>Insert › Block…: the dialog, then the block put in the first cell.</summary>
        private void InsertBlock()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            if (_model.ShowInsertTableBlock == null)
            {
                _model.Session?.Write("The Insert a Block in a Table Cell dialog is shown by the window.");
                return;
            }

            var names = new List<string>();

            foreach (DxfBlockTableRecord block in _model.Document.Database.BlockTable)
            {
                if (!block.IsLayout && !block.IsAnonymous)
                {
                    names.Add(block.Name);
                }
            }

            string current =
                table.CellType(_range.TopRow, _range.LeftColumn) == DxfTableCellType.BlockCell
                    ? table.GetBlockTableRecord(_range.TopRow, _range.LeftColumn)?.Name
                    : null;
            var dialog = new InsertTableBlockViewModel(
                CadTableEditor.CellName(_range.TopRow, _range.LeftColumn),
                names,
                current
            );

            if (_model.ShowInsertTableBlock(dialog) && dialog.IsConfirmed)
            {
                Do(d =>
                    CadTableEditor.InsertBlock(
                        d,
                        Handle,
                        _range.TopRow,
                        _range.LeftColumn,
                        dialog.Name,
                        dialog.ScaleValue,
                        dialog.AutoFit,
                        dialog.RotationValue,
                        dialog.AlignmentValue
                    )
                );
            }
        }

        /// <summary>Insert › Field…: the Field dialog, then the field it ends with put in the first cell.</summary>
        private void InsertField()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            if (_model.ShowField == null)
            {
                _model.Session?.Write("The Field dialog is shown by the window.");
                return;
            }

            CadFieldRequest request = _model.NewFieldRequest();
            string held = table.GetFieldCode(_range.TopRow, _range.LeftColumn, 0);

            if (held != null && table.GetFormula(_range.TopRow, _range.LeftColumn, 0) == null)
            {
                request.Load("%<" + held + ">%");
            }

            string code = _model.AskField(request);

            if (code != null)
            {
                Do(d => CadTableEditor.InsertField(d, Handle, _range.TopRow, _range.LeftColumn, code));
            }
        }

        /// <summary>Cell Borders…: the Cell Border Properties dialog, then the lines it chose given the properties.</summary>
        private void BordersDialog()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            if (_model.ShowCellBorders == null)
            {
                _model.Session?.Write("The Cell Border Properties dialog is shown by the window.");
                return;
            }

            var linetypes = new List<string>();

            foreach (DxfLinetypeTableRecord linetype in _model.Document.Database.LinetypeTable)
            {
                linetypes.Add(linetype.Name);
            }

            var dialog = new CellBordersViewModel(CellName, linetypes);

            if (_model.ShowCellBorders(dialog) && dialog.IsConfirmed)
            {
                Do(d =>
                    CadTableEditor.SetCellBorders(
                        d,
                        Handle,
                        Range,
                        dialog.ChosenLines,
                        dialog.LinesShown,
                        dialog.Weight,
                        dialog.ColorValue
                    )
                );
            }
        }

        /// <summary>Data Format: a kind of data given its usual format at once, or the Table Cell Format dialog for none.</summary>
        private void DataFormat(string name)
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            var dialog = new DataFormatViewModel(
                CellName,
                table.DataType(_range.TopRow, _range.LeftColumn),
                table.UnitType(_range.TopRow, _range.LeftColumn),
                table.Format(_range.TopRow, _range.LeftColumn)
            );

            if (!string.IsNullOrEmpty(name))
            {
                dialog.DataType = DataFormatViewModel.Kinds.First(k => k.Name == name);
            }
            else if (_model.ShowDataFormat == null)
            {
                _model.Session?.Write("The Table Cell Format dialog is shown by the window.");
                return;
            }
            else if (!_model.ShowDataFormat(dialog) || !dialog.IsConfirmed)
            {
                return;
            }

            Do(d =>
                CadTableEditor.SetDataFormat(
                    d,
                    Handle,
                    Range,
                    dialog.DataType.Kind,
                    dialog.DataType.Unit,
                    dialog.Format
                )
            );
        }

        /// <summary>The cells selected given a cell style; null for By Row/Column.</summary>
        private void SetCellStyle(string name)
        {
            Do(d => CadTableEditor.SetCellStyle(d, Handle, Range, name == ByRow ? null : name));
        }

        /// <summary>Create New Cell Style…: the dialog, then the cell style made in the table style of the table.</summary>
        private void CreateCellStyle()
        {
            DxfTable table = Table;
            DxfTableStyle style = CadTableEditor.StyleOf(table);

            if (style == null)
            {
                _model.Session?.Write("The table is written in no table style to make a cell style in.");
                return;
            }

            if (_model.ShowCreateCellStyle == null)
            {
                _model.Session?.Write("The Create New Cell Style dialog is shown by the window.");
                return;
            }

            var dialog = new CreateCellStyleViewModel(CadTableEditor.CellStyleNames(table));

            if (_model.ShowCreateCellStyle(dialog) && dialog.IsConfirmed)
            {
                Do(d => CadTableEditor.CreateCellStyle(d, style.Name, dialog.Name, dialog.StartWith));
                Raise(nameof(CellStyleChoices));
            }
        }

        /// <summary>Manage Cell Styles…: the dialog over the table style of the table.</summary>
        private void ManageCellStyles()
        {
            DxfTableStyle style = CadTableEditor.StyleOf(Table);

            if (style == null || _model.ShowManageCellStyles == null || _model.Document == null)
            {
                _model.Session?.Write(
                    "The table is written in no table style, or the window shows no Manage Cell Styles dialog."
                );
                return;
            }

            _model.ShowManageCellStyles(
                new ManageCellStylesViewModel(_model.Document, style.Name, _model.ShowCreateCellStyle)
            );
            Refresh();
        }

        /// <summary>Gets the table, or null where it is gone.</summary>
        private DxfTable Table => _model.Document?.FindEntity(Handle) as DxfTable;

        /// <summary>Whether the cells are still those of a table of the document.</summary>
        internal bool IsValid(CadDocument document) =>
            document.FindEntity(Handle) is DxfTable table
            && _row < table.NumRows
            && _column < table.NumColumns
            && _anchorRow < table.NumRows
            && _anchorColumn < table.NumColumns;

        /// <summary>Reads the range again from the table, and raises every property the tab shows.</summary>
        internal void Refresh()
        {
            DxfTable table = Table;

            if (table != null)
            {
                _row = Math.Min(_row, table.NumRows - 1);
                _column = Math.Min(_column, table.NumColumns - 1);
                _anchorRow = Math.Min(_anchorRow, table.NumRows - 1);
                _anchorColumn = Math.Min(_anchorColumn, table.NumColumns - 1);
                _range = CadTableEditor.Grown(
                    table,
                    new DxfCellRange(
                        Math.Min(_row, _anchorRow),
                        Math.Min(_column, _anchorColumn),
                        Math.Max(_row, _anchorRow),
                        Math.Max(_column, _anchorColumn)
                    )
                );
            }
            else
            {
                _range = new DxfCellRange(_row, _column, _row, _column);
            }

            Raise(nameof(Range));
            Raise(nameof(CellName));
            Raise(nameof(CellStyleName));
            Raise(nameof(CellStyle));
            Raise(nameof(CellStyleChoices));
            Raise(nameof(LockName));
            Raise(nameof(AlignmentName));
            Raise(nameof(Fill));
            Raise(nameof(CanMerge));
        }

        /// <summary>Whether the cell the selection runs from is the one given.</summary>
        internal bool IsAnchoredAt(int row, int column) => _anchorRow == row && _anchorColumn == column;

        /// <summary>Grows the selection to a cell, as Shift+click does.</summary>
        internal void ExtendTo(int row, int column)
        {
            _row = row;
            _column = column;
            Refresh();
            _model.ShowTableCell(this);
        }

        /// <summary>Moves the cell selected by rows and columns, as the arrows do; with Shift, grows the range.</summary>
        internal bool Step(int rows, int columns, bool shift)
        {
            DxfTable table = Table;

            if (table == null)
            {
                return false;
            }

            // From a block of merged cells, past its far side.
            DxfCellRange block = table.GetMergeRange(_row, _column);
            int row =
                rows > 0 ? block.BottomRow + 1
                : rows < 0 ? block.TopRow - 1
                : _row;
            int column =
                columns > 0 ? block.RightColumn + 1
                : columns < 0 ? block.LeftColumn - 1
                : _column;

            if (row < 0 || column < 0 || row >= table.NumRows || column >= table.NumColumns)
            {
                return true;
            }

            _row = row;
            _column = column;

            if (!shift)
            {
                _anchorRow = row;
                _anchorColumn = column;
            }

            Refresh();
            _model.ShowTableCell(this);
            return true;
        }

        /// <summary>Opens the first cell selected for typing (TABLEDIT at its middle), as F2 does.</summary>
        internal bool EditCell()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return false;
            }

            IReadOnlyList<GeoPoint3> corners = table.GetCellExtents(_range.TopRow, _range.LeftColumn, true);
            var middle = new GeoPoint2((corners[0].X + corners[2].X) / 2.0, (corners[0].Y + corners[2].Y) / 2.0);
            return _model.EditTableCellAt(middle);
        }

        /// <summary>
        /// The corners of the range, then of the table, for the canvas to draw; then the points where the lines between columns
        /// meet the table's top edge, its left and right corners first and last, and those where the lines between rows meet its
        /// left edge, so that the bands of letters and numbers are drawn a column and a row at a time.
        /// </summary>
        internal GeoPoint2[][] Outline()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return null;
            }

            GeoPoint2[] whole = CadTableEditor.Corners(
                table,
                new DxfCellRange(0, 0, table.NumRows - 1, table.NumColumns - 1)
            );
            GeoVector2 across = whole[1] - whole[0];
            GeoVector2 down = whole[3] - whole[0];
            across = across.Length > 0.0 ? across / across.Length : new GeoVector2(1.0, 0.0);
            down = down.Length > 0.0 ? down / down.Length : new GeoVector2(0.0, -1.0);
            var columns = new GeoPoint2[table.NumColumns + 1];
            var rows = new GeoPoint2[table.NumRows + 1];
            double at = 0.0;

            for (int c = 0; c <= table.NumColumns; c++)
            {
                columns[c] = whole[0] + across * at;
                at += c < table.NumColumns ? table.ColumnWidth(c) : 0.0;
            }

            at = 0.0;

            for (int r = 0; r <= table.NumRows; r++)
            {
                rows[r] = whole[0] + down * at;
                at += r < table.NumRows ? table.RowHeight(r) : 0.0;
            }

            return new[] { CadTableEditor.Corners(table, _range), whole, columns, rows };
        }

        /// <summary>The alignment every cell of the range shares, by its name; null where they differ.</summary>
        private string SharedAlignment(DxfTable table)
        {
            if (table == null)
            {
                return null;
            }

            var style = table.TableStyle as DxfTableStyle;
            DxfCellAlignment? shared = null;

            for (int r = _range.TopRow; r <= _range.BottomRow; r++)
            {
                DxfRowType kind = table.RowType(r);

                for (int c = _range.LeftColumn; c <= _range.RightColumn; c++)
                {
                    DxfCellAlignment alignment = table.Alignment(r, c)
                        ?? style?.Alignment(kind)
                        ?? (kind == DxfRowType.DataRow ? DxfCellAlignment.TopCenter : DxfCellAlignment.MiddleCenter);

                    if (shared.HasValue && shared.Value != alignment)
                    {
                        return null;
                    }

                    shared = alignment;
                }
            }

            return shared.HasValue ? Alignments[(int)shared.Value - 1] : null;
        }

        /// <summary>Whether the range meets a block of merged cells, which Unmerge needs.</summary>
        private bool MeetsMerged(DxfTable table)
        {
            for (int r = _range.TopRow; table != null && r <= _range.BottomRow; r++)
            {
                for (int c = _range.LeftColumn; c <= _range.RightColumn; c++)
                {
                    if (table.IsMergedCell(r, c, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Background Fill: the Select Color dialog on the first cell's fill, ByLayer and ByBlock offered, the colour chosen given every cell.</summary>
        private void PickFill()
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            if (_model.PickColor == null)
            {
                _model.Session?.Write("The Select Color dialog is shown by the window.");
                return;
            }

            DxfColor old =
                table.IsBackgroundColorNone(_range.TopRow, _range.LeftColumn) != false
                    ? DxfColor.ByLayer
                    : table.BackgroundColor(_range.TopRow, _range.LeftColumn) ?? DxfColor.ByLayer;
            var dialog = new SelectColorViewModel(old, true);

            if (_model.PickColor(dialog))
            {
                Do(d => CadTableEditor.SetBackground(d, Handle, Range, dialog.Color));
            }
        }

        /// <summary>Size Equally: the columns (or rows) of the range each given their mean, one undo step.</summary>
        private void SizeEqually(bool columns)
        {
            DxfTable table = Table;

            if (table == null)
            {
                return;
            }

            int first = columns ? _range.LeftColumn : _range.TopRow;
            int count = columns ? _range.RightColumn - _range.LeftColumn + 1 : _range.BottomRow - _range.TopRow + 1;
            double sum = 0.0;

            for (int k = 0; k < count; k++)
            {
                sum += columns ? table.ColumnWidth(first + k) : table.RowHeight(first + k);
            }

            var sizes = new double[count];

            for (int k = 0; k < count; k++)
            {
                sizes[k] = sum / count;
            }

            Do(d =>
                columns
                    ? CadTableEditor.SetColumnWidths(d, Handle, first, sizes)
                    : CadTableEditor.SetRowHeights(d, Handle, first, sizes)
            );
        }

        private void Align(string name)
        {
            int at = -1;

            for (int k = 0; k < Alignments.Count; k++)
            {
                at = string.Equals(Alignments[k], name, StringComparison.Ordinal) ? k : at;
            }

            if (at >= 0)
            {
                Do(d => CadTableEditor.SetAlignment(d, Handle, Range, (DxfCellAlignment)(at + 1)));
            }
        }

        private void Borders(string name)
        {
            switch (name)
            {
                case "All":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.AllGridLines, true));
                    break;
                case "Outside":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.OuterGridLines, true));
                    break;
                case "Inside":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.InnerGridLines, true));
                    break;
                case "Bottom":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.HorizontalBottom, true));
                    break;
                case "Left":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.VerticalLeft, true));
                    break;
                case "Top":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.HorizontalTop, true));
                    break;
                case "Right":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.VerticalRight, true));
                    break;
                case "None":
                    Do(d => CadTableEditor.SetBorders(d, Handle, Range, DxfGridLineType.AllGridLines, false));
                    break;
            }
        }

        /// <summary>Each row (or column) of the range merged on its own, one undo step for them all.</summary>
        private void MergeEach(bool byRow)
        {
            DxfCellRange range = Range;
            int count = byRow ? range.BottomRow - range.TopRow + 1 : range.RightColumn - range.LeftColumn + 1;
            CadDocument document = _model.Document;

            if (document == null)
            {
                return;
            }

            document.BeginGroup("TABLEDIT");

            try
            {
                for (int k = 0; k < count; k++)
                {
                    DxfCellRange part = byRow
                        ? new DxfCellRange(range.TopRow + k, range.LeftColumn, range.TopRow + k, range.RightColumn)
                        : new DxfCellRange(range.TopRow, range.LeftColumn + k, range.BottomRow, range.LeftColumn + k);

                    if (part.TopRow != part.BottomRow || part.LeftColumn != part.RightColumn)
                    {
                        Do(d => CadTableEditor.Merge(d, Handle, part));
                    }
                }
            }
            finally
            {
                document.EndGroup();
            }
        }

        /// <summary>One edit of the tab: refused or failed said on the command line, the tab read again.</summary>
        /// <summary>The items of the cell menu's Data Link: Link Cell, Edit Data Link, Update Table Data Links, Download and Upload, Detach.</summary>
        private IReadOnlyList<CadMenuItemViewModel> DataLinkItems(DxfTable table)
        {
            DxfDataLink here = table?.GetDataLink(_row, _column);
            bool any = table != null && table.GetDataLink().Count > 0;
            return new[]
            {
                new CadMenuItemViewModel(
                    "Link Cell…",
                    () => LinkCellCommand.Execute(null),
                    table != null && here == null
                ),
                new CadMenuItemViewModel(
                    here == null ? "Edit Data Link…" : "Edit Data Link \"" + here.Name + "\"…",
                    () => _model.EditDataLink?.Invoke(here.Name),
                    here != null && _model.EditDataLink != null && here.DataAdapterId == CadDataSource.ExcelAdapter
                ),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Update Table Data Links", () => DownloadCommand.Execute(null), any),
                new CadMenuItemViewModel("Download Changes from Source File", () => DownloadCommand.Execute(null), any),
                new CadMenuItemViewModel("Upload User Changes to Source File", () => UploadCommand.Execute(null), any),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Detach Data Link", () => DetachLinkCommand.Execute(null), here != null),
            };
        }

        /// <summary>Link Cell: the Select a Data Link dialog, then the data of the link put in from the cell as <see cref="CadDataLinks.LinkCells"/> puts it.</summary>
        private void LinkCell()
        {
            CadDocument document = _model.Document;

            if (document == null || _model.PickDataLink == null)
            {
                return;
            }

            if (!CadDataLinks.CanHold(document))
            {
                _model.Session?.Write("Data links need a drawing of AutoCAD 2007 or later.");
                return;
            }

            if (CadDataLinks.List(document).Count == 0)
            {
                _model.Session?.Write("No data links exist in the current drawing.");
                _model.Session?.Write("To create a data link, use the DATALINK command.");
                return;
            }

            string name = _model.PickDataLink();

            if (name != null)
            {
                Do(d => CadDataLinks.LinkCells(d, Handle, _range.TopRow, _range.LeftColumn, name));
            }
        }

        /// <summary>Download from Source and Upload to Source: the table's data links updated or written, what went wrong said on the command line.</summary>
        private void UpdateLinks(bool write)
        {
            DxfTable table = Table;
            CadDocument document = _model.Document;

            if (table == null || document == null)
            {
                return;
            }

            IReadOnlyList<DxfDataLink> links = table.GetDataLink();

            if (links.Count == 0)
            {
                _model.Session?.Write("The table has no data link.");
                return;
            }

            CadDataLinkRun run = write ? CadDataLinks.Write(document, links) : CadDataLinks.Update(document, links);

            foreach (string message in run.Messages)
            {
                _model.Session?.Write(message);
            }

            if (!run.Edit.Succeeded)
            {
                _model.Session?.Write(run.Edit.Message);
            }

            if (IsValid(document))
            {
                Refresh();
            }
        }

        /// <summary>Detach Data Link: AutoCAD's "Detaching the table from this link will cause all cells to have static data" taken as read.</summary>
        private void DetachLink() => Do(d => CadDataLinks.Detach(d, Handle, _row, _column));

        private void Do(Func<CadDocument, CadEditResult> edit)
        {
            CadDocument document = _model.Document;

            if (document == null)
            {
                return;
            }

            CadEditResult result = edit(document);

            if (!result.Succeeded)
            {
                _model.Session?.Write(result.Message);
            }

            if (IsValid(document))
            {
                Refresh();
            }
        }
    }
}
