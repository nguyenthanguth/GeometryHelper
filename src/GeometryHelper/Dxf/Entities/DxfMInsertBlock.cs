using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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
    /// <see cref="DxfEntity.TransformBy(GeoTransform3)"/> scales the spacing as it scales the axes of the array, so that
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

        /// <summary>
        /// Refuses to give the entities of the block in the MINSERT's cells, as AutoCAD 2021's <c>Explode</c> refused its MINSERT
        /// of a circle in 3 columns by 2 rows and its MINSERT of one cell alike (eNotApplicable), its
        /// <see cref="DxfBlockReference.TreatAsBlockRefForExplode"/> false. <see cref="DxfBlockReference.ExplodeToOwnerSpace"/>
        /// gives a MINSERT's first cell, as AutoCAD's does.
        /// </summary>
        /// <returns>Never: the call is refused.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
            => throw new NotSupportedException("A MINSERT is not exploded, as AutoCAD's Explode refuses one, of one cell or more: ExplodeToOwnerSpace gives its first cell.");

        /// <summary>
        /// Gets the box of the block placed in the MINSERT's first cell, as AutoCAD 2021's <c>GeometricExtents</c> of a MINSERT
        /// gives it: its MINSERT of a circle of radius 3 at the origin in 3 columns 10 apart by 2 rows 8 apart had the box
        /// (-3, -3, 0) to (3, 3, 0), not that of all six. The extents of the drawing still take in every cell.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the cell places no shape the library reads, its drawing has no
        /// block of its name, or it would place more than the 2 000 000 entities of <see cref="DxfReadOptions.MaxPlacedEntities"/>.</exception>
        public override GeoAabb3 GeometricExtents => Box(GeoTransform3.Identity, true);

        /// <summary>The box of every cell, as the extents of the drawing take a MINSERT in.</summary>
        internal GeoAabb3 EveryCellExtents => base.GeometricExtents;

        /// <summary>
        /// The entities of the block placed in every cell of the array: those of the first cell, as
        /// <see cref="DxfBlockReference.Explode"/> places them, then each other cell's, row by row, the first moved along the
        /// turned axes of the INSERT by the spacing of its column and row; a block of one circle in 2 rows by 3 columns gives six.
        /// </summary>
        private protected override IReadOnlyList<DxfEntity> EveryCell()
        {
            IReadOnlyList<DxfEntity> first = FirstCell();
            int columns = Columns;
            int rows = Rows;

            if ((long)first.Count * columns * rows > 2000000L)
            {
                throw new InvalidOperationException("The MINSERT's " + (columns * rows).ToString(CultureInfo.InvariantCulture) + " cells would hold " + ((long)first.Count * columns * rows).ToString(CultureInfo.InvariantCulture) + " entities, more than the 2 000 000 the reader places.");
            }

            OcsAxes(Normal, out GeoVector3 u, out GeoVector3 v);
            double angle = GetDouble(50, 0.0) * (Math.PI / 180.0);
            GeoVector3 along = u.Multiply(Math.Cos(angle)).Add(v.Multiply(Math.Sin(angle)));
            GeoVector3 across = v.Multiply(Math.Cos(angle)).Subtract(u.Multiply(Math.Sin(angle)));
            var parts = new List<DxfEntity>(first.Count * columns * rows);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (row == 0 && column == 0)
                    {
                        parts.AddRange(first);
                        continue;
                    }

                    GeoTransform3 move = GeoTransform3.Translation(along.Multiply(column * ColumnSpacing).Add(across.Multiply(row * RowSpacing)));

                    foreach (DxfEntity part in first)
                    {
                        parts.Add(part.GetTransformedCopy(move));
                    }
                }
            }

            return parts.AsReadOnly();
        }

        [DxfType("INSERT")]
        internal static DxfEntity CreateArray(DxfItem item) => IsArray(item) ? new DxfMInsertBlock(item) : null;

        /// <inheritdoc/>
        private protected override int CellCount => Columns * Rows;

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
