using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The rows of a table while a cell of it is typed in, as AutoCAD 2021's in-place editor shows them: the row of the cell
    /// grows to hold the lines typed and shrinks back as they are taken out, never below the height it stood at, and the rows
    /// under it move with it. It is a preview of the words typed, kept apart from the drawing, which is written once, when
    /// the editor closes (Esc writes nothing): the rows <see cref="RowHeights"/> give are those the words give once written
    /// by <see cref="CadTableEditor.SetText"/>. In AutoCAD's Standard table style (text 4.5, rows 9 high) a cell of data typed
    /// with two lines makes its row 15.07 high, with three lines 22.57, with four 30.15; the rows under it stand 6.07, 13.57
    /// and 21.15 lower, and with one line nothing moves.
    /// </summary>
    /// <remarks>
    /// Each key costs the layout of the one cell typed in, not of the table: the other cells of a table drawn by AutoCAD
    /// already fit their rows. The table is drawn anew (<see cref="Items"/>) only while a row differs from what it stands at,
    /// and then only when the rows changed since the key before.
    /// </remarks>
    public sealed class CadTableLive
    {
        private static readonly IReadOnlyList<DisplayItem2> NoItems = Array.Empty<DisplayItem2>();

        private readonly CadDocument _document;
        private readonly double[] _was;
        private double[] _heights;
        private IReadOnlyList<DisplayItem2> _items;
        private DisplayBuilder _builder;
        private DxfDatabase _database;

        /// <summary>Starts the preview of a cell of a table typed in, the rows as they stand.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row of the cell, from nought; of a block of merged cells, its top row.</param>
        /// <param name="column">The column of the cell, from nought; of a block of merged cells, its left column.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the handle is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no table of the handle, or the table no such cell.</exception>
        public CadTableLive(CadDocument document, string handle, int row, int column)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));

            if (
                !(document.FindEntity(handle) is DxfTable table)
                || row < 0
                || column < 0
                || row >= table.NumRows
                || column >= table.NumColumns
            )
            {
                throw new ArgumentException("The drawing has no such cell of a table.", nameof(handle));
            }

            Row = row;
            Column = column;
            _was = new double[table.NumRows];

            for (int r = 0; r < _was.Length; r++)
            {
                _was[r] = table.RowHeight(r);
            }

            _heights = _was;
        }

        /// <summary>Gets the table's handle.</summary>
        public string Handle { get; }

        /// <summary>Gets the row of the cell typed in, from nought.</summary>
        public int Row { get; }

        /// <summary>Gets the column of the cell typed in, from nought.</summary>
        public int Column { get; }

        /// <summary>Gets the height of each row once the words typed are written; the heights the rows stand at before any key.</summary>
        public IReadOnlyList<double> RowHeights => _heights;

        /// <summary>Gets whether a row is other than it stands at: the words typed need more room than the row has.</summary>
        public bool ChangesRows
        {
            get
            {
                for (int r = 0; r < _was.Length; r++)
                {
                    if (Math.Abs(_heights[r] - _was[r]) > 1E-9)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a count that goes up each time the rows of the preview change, so that a window knows the table is to be drawn
        /// again; it does not move for a key that leaves the rows as they were.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Gets the table drawn at the heights of <see cref="RowHeights"/>, with the words of the cell typed in left out (the
        /// editor shows them); empty while no row differs from what it stands at, when the table in the drawing is drawn as it is.
        /// </summary>
        public IReadOnlyList<DisplayItem2> Items
        {
            get
            {
                if (!ChangesRows || !(_document.FindEntity(Handle) is DxfTable table))
                {
                    return NoItems;
                }

                if (_items == null)
                {
                    if (_builder == null || !ReferenceEquals(_database, _document.Database))
                    {
                        _database = _document.Database;
                        _builder = new DisplayBuilder(_database, _document);
                    }

                    _items = _builder.BuildTable(table, table.PicturePreview(_heights, Row, Column));
                }

                return _items;
            }
        }

        /// <summary>
        /// Lays the rows out for the words typed so far: the row of the cell is as high as the words need, and no lower than it
        /// stood; a block of merged cells across rows grows the rows it spans in proportion to their heights, as writing the words does.
        /// Nothing is written to the drawing.
        /// </summary>
        /// <param name="words">The words with their MTEXT codes, as the editor would write them; empty for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the words are null.</exception>
        public void Update(string words)
        {
            if (words == null)
            {
                throw new ArgumentNullException(nameof(words));
            }

            double[] heights = _was;

            if (_document.FindEntity(Handle) is DxfTable table)
            {
                double? need = CadTableEditor.BlockNeeds(_document, table, Row, Column, words);

                if (need.HasValue)
                {
                    heights = CadTableEditor.Wanted(
                        _was,
                        new List<KeyValuePair<DxfCellRange, double>>
                        {
                            new KeyValuePair<DxfCellRange, double>(table.GetMergeRange(Row, Column), need.Value),
                        }
                    );
                }
            }

            if (!SameHeights(heights, _heights))
            {
                _heights = heights;
                _items = null;
                Version++;
            }
        }

        /// <summary>
        /// Gets how far the point a text of the cell typed in is attached at has moved along the table, in the drawing: the
        /// block's top edge for a text attached at the top (1 to 3), its bottom edge at the bottom (7 to 9), the middle of the
        /// block between them. Nought while the rows are as they stand.
        /// </summary>
        /// <param name="attachment">How the text is attached, 1 top left to 9 bottom right.</param>
        /// <returns>The shift to add to the point the editor was opened at.</returns>
        public GeoVector2 Shift(int attachment)
        {
            if (!(_document.FindEntity(Handle) is DxfTable table))
            {
                return new GeoVector2(0.0, 0.0);
            }

            DxfCellRange block = table.GetMergeRange(Row, Column);
            bool up = table.FlowDirection == DxfFlowDirection.BottomToTop;
            double top = Moved(up ? block.BottomRow + 1 : block.TopRow);
            double bottom = Moved(up ? block.TopRow : block.BottomRow + 1);
            int placed = (attachment - 1) / 3;
            double by =
                placed == 0 ? top
                : placed == 1 ? (top + bottom) / 2.0
                : bottom;
            return Flow(table) * by;
        }

        /// <summary>The unit vector the rows run along, from the first towards the last, in the drawing, as the first cell's side shows it; nought for a cell with no height.</summary>
        private static GeoVector2 Flow(DxfTable table)
        {
            IReadOnlyList<GeoPoint3> first = table.GetCellExtents(0, 0, true);
            var side = new GeoVector2(first[3].X - first[0].X, first[3].Y - first[0].Y);
            double length = side.Length;

            if (length <= 0.0)
            {
                return new GeoVector2(0.0, 0.0);
            }

            return table.FlowDirection == DxfFlowDirection.BottomToTop ? side / -length : side / length;
        }

        /// <summary>How much the rows before a boundary have grown, along the flow: the sum of what rows 0 to boundary - 1 grew.</summary>
        private double Moved(int boundary)
        {
            double by = 0.0;

            for (int r = 0; r < boundary; r++)
            {
                by += _heights[r] - _was[r];
            }

            return by;
        }

        private static bool SameHeights(double[] a, double[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            for (int r = 0; r < a.Length; r++)
            {
                if (Math.Abs(a[r] - b[r]) > 1E-9)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
