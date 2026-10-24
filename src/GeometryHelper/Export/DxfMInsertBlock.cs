using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A MINSERT of a DXF file, as AutoCAD's <c>MInsertBlock</c>: an INSERT that places its block in an array of
    /// <see cref="Rows"/> by <see cref="Columns"/> cells, <see cref="RowSpacing"/> and <see cref="ColumnSpacing"/> apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MINSERT of the block BOLT at (200, 0, 0), turned by 15 degrees, of 2 rows by 3 columns 40 and 25 apart, places six
    /// bolts; the spacing runs along the turned axes of the INSERT and is not scaled by its <see cref="DxfBlockReference.ScaleFactors"/>.
    /// It is an INSERT whose group 70 or 71 is above 1, as AutoCAD writes one, and every member of
    /// <see cref="DxfBlockReference"/> reads and sets it as it does an INSERT.
    /// </para>
    /// <para>
    /// <see cref="DxfBlockReference.TransformBy(GeoTransform3)"/> scales the spacing as it scales the axes of the array, so that
    /// the cells stay where the transformation takes them.
    /// </para>
    /// </remarks>
    public sealed class DxfMInsertBlock : DxfBlockReference
    {
        /// <summary>
        /// Initializes a MINSERT of no block of one cell at the origin, as AutoCAD's <c>new MInsertBlock()</c>, in no file; give
        /// it its block before it is added to a drawing.
        /// </summary>
        public DxfMInsertBlock()
            : base("AcDbMInsertBlock", new GeoPoint3(0.0, 0.0, 0.0), null, Cells(1, 1, 0.0, 0.0))
        {
        }

        /// <summary>
        /// Initializes a MINSERT of a block, as AutoCAD's <c>new MInsertBlock(position, blockTableRecord, columns, rows,
        /// colSpacing, rowSpacing)</c>: not turned, scaled by 1, on layer 0, in no file until it is added to a block of the
        /// drawing the block is of.
        /// </summary>
        /// <param name="position">The insertion point of the first cell, in world coordinates.</param>
        /// <param name="blockTableRecord">The block it places, of a drawing.</param>
        /// <param name="columns">How many columns, 1 or more.</param>
        /// <param name="rows">How many rows, 1 or more.</param>
        /// <param name="colSpacing">How far apart the columns stand, along the turned x axis.</param>
        /// <param name="rowSpacing">How far apart the rows stand, along the turned y axis.</param>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, or the block is a layout.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a count is below 1 or a spacing is not a finite number.</exception>
        public DxfMInsertBlock(GeoPoint3 position, DxfBlockTableRecord blockTableRecord, short columns, short rows, double colSpacing, double rowSpacing)
            : base("AcDbMInsertBlock", position, blockTableRecord ?? throw new ArgumentNullException(nameof(blockTableRecord)), Cells(columns, rows, colSpacing, rowSpacing))
        {
        }

        private DxfMInsertBlock(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets how many columns the array has (group 70), as AutoCAD's <c>Columns</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count set is below 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MINSERT is erased.</exception>
        public short Columns
        {
            get => (short)GetInt(70, 1);

            set
            {
                if (value != Columns)
                {
                    Count(value, nameof(value));
                    SetInt(70, value, 1);
                }
            }
        }

        /// <summary>Gets or sets how many rows the array has (group 71), as AutoCAD's <c>Rows</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count set is below 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MINSERT is erased.</exception>
        public short Rows
        {
            get => (short)GetInt(71, 1);

            set
            {
                if (value != Rows)
                {
                    Count(value, nameof(value));
                    SetInt(71, value, 1);
                }
            }
        }

        /// <summary>Gets or sets how far apart the columns stand (group 44), along the turned x axis, not scaled, as AutoCAD's <c>ColumnSpacing</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MINSERT is erased.</exception>
        public double ColumnSpacing
        {
            get => GetDouble(44, 0.0);
            set => SetDouble(44, value, 0.0);
        }

        /// <summary>Gets or sets how far apart the rows stand (group 45), along the turned y axis, not scaled, as AutoCAD's <c>RowSpacing</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MINSERT is erased.</exception>
        public double RowSpacing
        {
            get => GetDouble(45, 0.0);
            set => SetDouble(45, value, 0.0);
        }

        [DxfType("INSERT")]
        internal static DxfEntity CreateArray(DxfItem item) => IsArray(item) ? new DxfMInsertBlock(item) : null;

        /// <summary>
        /// The spacing of the array scaled as the transformation stretches the turned x and y axes of the INSERT, the rows
        /// turned to the other side where its y axis comes out mirrored, so that each cell lands where the transformation takes it.
        /// </summary>
        private protected override KeyValuePair<int, string>[] ArrayGroups(double xStretch, double yStretch)
        {
            double columns = ColumnSpacing * xStretch;
            double rows = RowSpacing * yStretch;
            return Join(Factor(44, columns, ColumnSpacing), Factor(45, rows, RowSpacing));
        }

        private static IEnumerable<DxfTypedValue> Cells(int columns, int rows, double colSpacing, double rowSpacing)
        {
            Count(columns, nameof(columns));
            Count(rows, nameof(rows));
            Guard.Finite(colSpacing, nameof(colSpacing), "A spacing has to be a finite number.");
            Guard.Finite(rowSpacing, nameof(rowSpacing), "A spacing has to be a finite number.");
            return new[]
            {
                new DxfTypedValue(70, columns.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(71, rows.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(44, Numbers.Exact(colSpacing + 0.0)),
                new DxfTypedValue(45, Numbers.Exact(rowSpacing + 0.0)),
            };
        }

        private static void Count(int value, string parameter)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(parameter, value, "An array has 1 column and 1 row or more.");
            }
        }
    }
}
