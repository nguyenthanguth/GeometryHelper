using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The box of an MTEXT being edited, as the MTEXT editor's ruler and Column Settings change it: its width, its defined
    /// height and its columns. Each change is a new frame, so that the editor's undo keeps the one before.
    /// </summary>
    /// <remarks>
    /// An MTEXT 55 wide of no columns is a frame of <see cref="Width"/> 55. In two static columns 40 wide with a gutter of
    /// 12.5 its <see cref="Width"/> is the total, 92.5, as AutoCAD 2021 gives it; in dynamic columns it is a column's,
    /// the count following the words.
    /// </remarks>
    public sealed class CadMTextFrame : IEquatable<CadMTextFrame>
    {
        private CadMTextFrame(
            double width,
            double height,
            DxfColumnType type,
            double columnWidth,
            double gutter,
            int count,
            bool autoHeight,
            IReadOnlyList<double> heights
        )
        {
            Width = width;
            Height = height;
            ColumnType = type;
            ColumnWidth = columnWidth;
            Gutter = gutter;
            ColumnCount = count;
            AutoHeight = autoHeight;
            ColumnHeights = heights;
        }

        /// <summary>Gets the width of the box: of the MTEXT with no columns, of all the columns with their gutters with static ones, of a column with dynamic ones; nought for none.</summary>
        public double Width { get; }

        /// <summary>Gets the defined height: the box's, or the columns' of one height; nought for none.</summary>
        public double Height { get; }

        /// <summary>Gets how the words are set in columns.</summary>
        public DxfColumnType ColumnType { get; }

        /// <summary>Gets the width of each column; nought with no columns.</summary>
        public double ColumnWidth { get; }

        /// <summary>Gets the space between two columns; nought with no columns.</summary>
        public double Gutter { get; }

        /// <summary>Gets how many static columns, or how many dynamic columns of their own heights have a height; 1 with no columns.</summary>
        public int ColumnCount { get; }

        /// <summary>Gets whether dynamic columns are all of one height, <see cref="Height"/>.</summary>
        public bool AutoHeight { get; }

        /// <summary>Gets the heights of dynamic columns of their own heights, the first column's first; empty for any other.</summary>
        public IReadOnlyList<double> ColumnHeights { get; }

        /// <summary>Gets whether the words are in columns.</summary>
        public bool HasColumns => ColumnType != DxfColumnType.NoColumns;

        /// <summary>Makes the frame of an MTEXT with no columns.</summary>
        /// <param name="width">Its width; nought for none.</param>
        /// <param name="height">Its defined height; nought for none.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is below nought or not a finite number.</exception>
        public static CadMTextFrame Of(double width, double height = 0.0)
        {
            Guard.NonNegative(width, nameof(width), "The width of an MTEXT is a finite number of nought or more.");
            Guard.NonNegative(height, nameof(height), "The height of an MTEXT is a finite number of nought or more.");
            return new CadMTextFrame(width, height, DxfColumnType.NoColumns, 0.0, 0.0, 1, false, Array.Empty<double>());
        }

        /// <summary>Makes the frame of an MTEXT as it is drawn: its width, defined height and columns.</summary>
        /// <param name="text">The MTEXT.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the MTEXT is null.</exception>
        public static CadMTextFrame Of(DxfMText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            double width = Math.Max(0.0, text.Width);
            double height = Math.Max(0.0, text.Height);
            DxfColumnType type = text.ColumnType;

            if (type == DxfColumnType.NoColumns || !(text.ColumnWidth > 0.0))
            {
                return Of(width, height);
            }

            double gutter = Math.Max(0.0, text.ColumnGutterWidth);
            int count = Math.Max(1, text.ColumnCount);

            if (type == DxfColumnType.StaticColumns)
            {
                return new CadMTextFrame(
                    (count * text.ColumnWidth) + ((count - 1) * gutter),
                    height,
                    type,
                    text.ColumnWidth,
                    gutter,
                    count,
                    false,
                    Array.Empty<double>()
                );
            }

            bool auto = text.ColumnAutoHeight;
            double[] heights = auto
                ? Array.Empty<double>()
                : Enumerable.Range(0, count).Select(i => Math.Max(0.0, text.GetColumnHeight(i))).ToArray();
            return new CadMTextFrame(text.ColumnWidth, height, type, text.ColumnWidth, gutter, count, auto, heights);
        }

        /// <summary>Gets the frame with another width: the MTEXT's, the total of static columns (the gutters kept, the columns sharing the rest), or a dynamic column's.</summary>
        /// <param name="width">The width, above nought.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is not a finite number above nought, or leaves static columns none.</exception>
        public CadMTextFrame WithWidth(double width)
        {
            Guard.Positive(width, nameof(width), "The width of an MTEXT is a finite number above nought.");

            switch (ColumnType)
            {
                case DxfColumnType.StaticColumns:
                    double column = (width - ((ColumnCount - 1) * Gutter)) / ColumnCount;
                    Guard.Positive(column, nameof(width), "The gutters take the whole width.");
                    return WithColumnWidth(column);
                case DxfColumnType.DynamicColumns:
                    return WithColumnWidth(width);
                default:
                    return Of(width, Height);
            }
        }

        /// <summary>Gets the frame with another defined height: the box's, or that of columns of one height; with dynamic columns of their own heights, every column's.</summary>
        /// <param name="height">The height; nought for none.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is below nought or not a finite number.</exception>
        public CadMTextFrame WithHeight(double height)
        {
            Guard.NonNegative(height, nameof(height), "The height of an MTEXT is a finite number of nought or more.");
            IReadOnlyList<double> heights =
                ColumnHeights.Count > 0 ? ColumnHeights.Select(_ => height).ToArray() : ColumnHeights;
            return new CadMTextFrame(Width, height, ColumnType, ColumnWidth, Gutter, ColumnCount, AutoHeight, heights);
        }

        /// <summary>Gets the frame with columns of another width, the gutter kept; the total of static ones follows.</summary>
        /// <param name="width">The width of a column, above nought.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame has no columns.</exception>
        public CadMTextFrame WithColumnWidth(double width) =>
            WithColumns(Columned(), width, Gutter, ColumnCount, AutoHeight);

        /// <summary>Gets the frame with another gutter; the total of static columns follows.</summary>
        /// <param name="gutter">The gutter, nought or more.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gutter is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame has no columns.</exception>
        public CadMTextFrame WithGutter(double gutter) =>
            WithColumns(Columned(), ColumnWidth, gutter, ColumnCount, AutoHeight);

        /// <summary>
        /// Gets the frame in other columns, as Column Settings sets them: none (the width the total the columns took), static
        /// of a count, or dynamic of one height or of their own, each then given the defined height to start with.
        /// </summary>
        /// <param name="type">The columns.</param>
        /// <param name="columnWidth">The width of a column, above nought; with none, the MTEXT's width.</param>
        /// <param name="gutter">The gutter, nought or more.</param>
        /// <param name="count">How many static columns, 1 to 100; the count of dynamic ones of their own heights.</param>
        /// <param name="autoHeight">Whether dynamic columns are all of one height.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size or the count is outside what it takes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame has no columns and none are asked for but a width is.</exception>
        public CadMTextFrame WithColumns(
            DxfColumnType type,
            double columnWidth,
            double gutter,
            int count,
            bool autoHeight
        )
        {
            if (type == DxfColumnType.NoColumns)
            {
                return Of(columnWidth > 0.0 ? columnWidth : Width, Height);
            }

            Guard.Positive(columnWidth, nameof(columnWidth), "The width of a column is a finite number above nought.");
            Guard.NonNegative(gutter, nameof(gutter), "The gutter is a finite number of nought or more.");

            if (count < 1 || count > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The count of columns is from 1 to 100.");
            }

            if (type == DxfColumnType.StaticColumns)
            {
                return new CadMTextFrame(
                    (count * columnWidth) + ((count - 1) * gutter),
                    Height,
                    type,
                    columnWidth,
                    gutter,
                    count,
                    false,
                    Array.Empty<double>()
                );
            }

            IReadOnlyList<double> heights = Array.Empty<double>();

            if (!autoHeight)
            {
                heights =
                    ColumnHeights.Count == count && !AutoHeight
                        ? ColumnHeights
                        : Enumerable
                            .Range(0, count)
                            .Select(i => i < ColumnHeights.Count ? ColumnHeights[i] : Height)
                            .ToArray();
            }

            return new CadMTextFrame(columnWidth, Height, type, columnWidth, gutter, count, autoHeight, heights);
        }

        /// <summary>
        /// Gets the frame with one dynamic column of its own height made another height, as its grip drags it; columns up to it
        /// that have none are given the defined height.
        /// </summary>
        /// <param name="index">The column, from nought.</param>
        /// <param name="height">Its height, above nought.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or past 99, or the height not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the columns are not dynamic ones of their own heights.</exception>
        public CadMTextFrame WithColumnHeight(int index, double height)
        {
            if (ColumnType != DxfColumnType.DynamicColumns || AutoHeight)
            {
                throw new InvalidOperationException("Only dynamic columns of their own heights have a height each.");
            }

            if (index < 0 || index > 99)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A column is from 0 to 99.");
            }

            Guard.Positive(height, nameof(height), "The height of a column is a finite number above nought.");
            var heights = ColumnHeights.ToList();

            while (heights.Count <= index)
            {
                heights.Add(Height);
            }

            heights[index] = height;
            return new CadMTextFrame(
                Width,
                index == 0 ? height : Height,
                ColumnType,
                ColumnWidth,
                Gutter,
                heights.Count,
                false,
                heights
            );
        }

        /// <summary>
        /// Gives an MTEXT this frame's width, defined height and columns, as AutoCAD's own calls set them: no columns taken
        /// out, static ones by <see cref="DxfMText.SetStaticColumns"/>, dynamic ones by <see cref="DxfMText.SetDynamicColumns"/>
        /// and each column's height by <see cref="DxfMText.SetColumnHeight"/>.
        /// </summary>
        /// <param name="text">The MTEXT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the MTEXT is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown where the MTEXT's file keeps no columns of this kind (older than R2007).</exception>
        public void ApplyTo(DxfMText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            switch (ColumnType)
            {
                case DxfColumnType.StaticColumns:
                    text.SetStaticColumns(ColumnWidth, Gutter, ColumnCount);
                    text.Height = Height;
                    break;
                case DxfColumnType.DynamicColumns:
                    text.SetDynamicColumns(ColumnWidth, Gutter, AutoHeight);
                    text.Height = Height;

                    if (!AutoHeight)
                    {
                        if (text.ColumnCount != ColumnHeights.Count && ColumnHeights.Count > 0)
                        {
                            text.ColumnCount = ColumnHeights.Count;
                        }

                        for (int i = 0; i < ColumnHeights.Count; i++)
                        {
                            text.SetColumnHeight(i, ColumnHeights[i]);
                        }
                    }

                    break;
                default:
                    if (text.ColumnType != DxfColumnType.NoColumns)
                    {
                        text.ColumnType = DxfColumnType.NoColumns;
                    }

                    text.Width = Width;
                    text.Height = Height;
                    break;
            }
        }

        /// <inheritdoc/>
        public bool Equals(CadMTextFrame other) =>
            other != null
            && Same(Width, other.Width)
            && Same(Height, other.Height)
            && ColumnType == other.ColumnType
            && Same(ColumnWidth, other.ColumnWidth)
            && Same(Gutter, other.Gutter)
            && ColumnCount == other.ColumnCount
            && AutoHeight == other.AutoHeight
            && ColumnHeights.Count == other.ColumnHeights.Count
            && ColumnHeights.Zip(other.ColumnHeights, Same).All(s => s);

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadMTextFrame);

        /// <inheritdoc/>
        public override int GetHashCode() => ((int)ColumnType * 397) ^ ColumnCount;

        private static bool Same(double a, double b) => Math.Abs(a - b) <= Tolerance.Default.EqualPoint;

        private DxfColumnType Columned() =>
            HasColumns
                ? ColumnType
                : throw new InvalidOperationException("The MTEXT is in no columns: give it some first.");
    }
}
