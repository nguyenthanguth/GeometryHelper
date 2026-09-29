using System;
using System.Globalization;
using GeometryHelper.Geometry;
using GeometryHelper.Packing.Algorithms;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// A sheet of paper the boxes are packed onto: its size, the scale it is drawn at, the offsets round its edges that
    /// nothing is packed into, and where it lies. When the boxes do not all fit on it, more sheets like it are laid out
    /// one after another, each beside the one before on the side <see cref="NewSheet"/> names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The paper size, the offsets and <see cref="SheetSpacing"/> are in millimetres on paper. Times
    /// <see cref="Scale"/>, they are in the units of the boxes, as <see cref="Origin"/>, <see cref="Width"/>,
    /// <see cref="Height"/> and every <see cref="SheetFrame"/> are: an A1 sheet at a scale of 50, for boxes in
    /// millimetres of the model, is 42,050 by 29,700.
    /// </para>
    /// <para>
    /// <see cref="SheetPacker.Pack(System.Collections.Generic.IReadOnlyList{GeoRectangle2[]}, Sheet, PackOptions)"/>
    /// only reads it.
    /// </para>
    /// </remarks>
    public sealed class Sheet
    {
        private const string OffsetMessage = "An offset has to be a finite number, and cannot be negative.";

        private double _scale = 1.0;
        private double _offsetLeft;
        private double _offsetRight;
        private double _offsetTop;
        private double _offsetBottom;
        private double _sheetSpacing;
        private GeoPoint2 _origin = new GeoPoint2(0.0, 0.0);
        private SheetDirection _newSheet = SheetDirection.Right;

        /// <summary>
        /// Initializes a sheet of the A series.
        /// </summary>
        /// <param name="size">The size, <see cref="PaperSize.A0"/> to <see cref="PaperSize.A4"/>.</param>
        /// <param name="orientation">Which way it is turned; across, landscape, unless given.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The size is <see cref="PaperSize.Custom"/>, which <see cref="Custom(double, double)"/> makes, or not a
        /// size at all; or the orientation is not one.
        /// </exception>
        public Sheet(PaperSize size, SheetOrientation orientation = SheetOrientation.Landscape)
        {
            double shortSide, longSide;
            switch (size)
            {
                case PaperSize.A0: shortSide = 841.0; longSide = 1189.0; break;
                case PaperSize.A1: shortSide = 594.0; longSide = 841.0; break;
                case PaperSize.A2: shortSide = 420.0; longSide = 594.0; break;
                case PaperSize.A3: shortSide = 297.0; longSide = 420.0; break;
                case PaperSize.A4: shortSide = 210.0; longSide = 297.0; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, "A size of the A series; a size of its own is made by Sheet.Custom.");
            }

            if (orientation != SheetOrientation.Landscape && orientation != SheetOrientation.Portrait)
            {
                throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unknown orientation.");
            }

            Size = size;
            Orientation = orientation;
            PaperWidth = orientation == SheetOrientation.Landscape ? longSide : shortSide;
            PaperHeight = orientation == SheetOrientation.Landscape ? shortSide : longSide;
        }

        private Sheet(double width, double height)
        {
            Size = PaperSize.Custom;
            Orientation = width >= height ? SheetOrientation.Landscape : SheetOrientation.Portrait;
            PaperWidth = width;
            PaperHeight = height;
        }

        /// <summary>
        /// Makes a sheet of a size of its own.
        /// </summary>
        /// <param name="width">Its width, across, in millimetres on paper.</param>
        /// <param name="height">Its height, up, in millimetres on paper.</param>
        /// <returns>The sheet, <see cref="PaperSize.Custom"/>, landscape when it is at least as wide as it is high.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A size is not a finite number above nought.</exception>
        public static Sheet Custom(double width, double height)
        {
            Guard.Positive(width, nameof(width), "A width has to be a finite number above nought.");
            Guard.Positive(height, nameof(height), "A height has to be a finite number above nought.");
            return new Sheet(width, height);
        }

        /// <summary>Gets the size of the paper.</summary>
        public PaperSize Size { get; }

        /// <summary>Gets which way the sheet is turned.</summary>
        public SheetOrientation Orientation { get; }

        /// <summary>Gets the width of the paper, across, in millimetres on paper.</summary>
        public double PaperWidth { get; }

        /// <summary>Gets the height of the paper, up, in millimetres on paper.</summary>
        public double PaperHeight { get; }

        /// <summary>
        /// Gets or sets how many units of the boxes a millimetre of paper stands for: 50 for a drawing at 1:50 of boxes
        /// in millimetres of the model; 1, the default, for boxes already in millimetres on paper.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a finite number above nought.</exception>
        public double Scale
        {
            get => _scale;
            set
            {
                Guard.Positive(value, nameof(value), "A scale has to be a finite number above nought.");
                _scale = value;
            }
        }

        /// <summary>Gets or sets how far in from the left edge the boxes keep, in millimetres on paper; nought unless set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double OffsetLeft
        {
            get => _offsetLeft;
            set
            {
                Guard.NonNegative(value, nameof(value), OffsetMessage);
                _offsetLeft = value;
            }
        }

        /// <summary>Gets or sets how far in from the right edge the boxes keep, in millimetres on paper; nought unless set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double OffsetRight
        {
            get => _offsetRight;
            set
            {
                Guard.NonNegative(value, nameof(value), OffsetMessage);
                _offsetRight = value;
            }
        }

        /// <summary>Gets or sets how far in from the top edge the boxes keep, in millimetres on paper; nought unless set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double OffsetTop
        {
            get => _offsetTop;
            set
            {
                Guard.NonNegative(value, nameof(value), OffsetMessage);
                _offsetTop = value;
            }
        }

        /// <summary>Gets or sets how far in from the bottom edge the boxes keep, in millimetres on paper; nought unless set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double OffsetBottom
        {
            get => _offsetBottom;
            set
            {
                Guard.NonNegative(value, nameof(value), OffsetMessage);
                _offsetBottom = value;
            }
        }

        /// <summary>
        /// Gets or sets where the first sheet lies: its lower left corner, in the units of the boxes; nought, nought
        /// unless set. Every result is where the boxes go with the sheets lying there.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN or infinite.</exception>
        public GeoPoint2 Origin
        {
            get => _origin;
            set
            {
                Guard.Finite(value.X, nameof(value), "A corner has to be at finite coordinates.");
                Guard.Finite(value.Y, nameof(value), "A corner has to be at finite coordinates.");
                _origin = value;
            }
        }

        /// <summary>
        /// Gets or sets which side of the sheet before it each new sheet goes on; to the right unless set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not one of <see cref="SheetDirection"/>.</exception>
        public SheetDirection NewSheet
        {
            get => _newSheet;
            set
            {
                if (value != SheetDirection.Right && value != SheetDirection.Left && value != SheetDirection.Top && value != SheetDirection.Bottom)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown direction.");
                }

                _newSheet = value;
            }
        }

        /// <summary>
        /// Gets or sets the gap between one sheet and the next, in millimetres on paper; nought, the sheets edge to
        /// edge, unless set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative, NaN or infinite.</exception>
        public double SheetSpacing
        {
            get => _sheetSpacing;
            set
            {
                Guard.NonNegative(value, nameof(value), "A spacing has to be a finite number, and cannot be negative.");
                _sheetSpacing = value;
            }
        }

        /// <summary>Gets the width of the sheet in the units of the boxes: its paper width times its scale.</summary>
        public double Width => PaperWidth * Scale;

        /// <summary>Gets the height of the sheet in the units of the boxes: its paper height times its scale.</summary>
        public double Height => PaperHeight * Scale;

        /// <summary>
        /// Moves the sheet so that one of its corners, or its middle, lies at a point, by setting <see cref="Origin"/>.
        /// </summary>
        /// <param name="corner">The point of the sheet to put there.</param>
        /// <param name="point">Where to put it, in the units of the boxes.</param>
        /// <remarks>
        /// The sheet is measured as it stands: a scale set afterwards keeps the lower left corner where it is, and
        /// moves the others.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The corner is not one of <see cref="SheetCorner"/>, or the point is not at finite coordinates.
        /// </exception>
        public void PlaceCorner(SheetCorner corner, GeoPoint2 point)
        {
            switch (corner)
            {
                case SheetCorner.LowerLeft:
                    Origin = point;
                    break;
                case SheetCorner.LowerRight:
                    Origin = new GeoPoint2(point.X - Width, point.Y);
                    break;
                case SheetCorner.UpperLeft:
                    Origin = new GeoPoint2(point.X, point.Y - Height);
                    break;
                case SheetCorner.UpperRight:
                    Origin = new GeoPoint2(point.X - Width, point.Y - Height);
                    break;
                case SheetCorner.Center:
                    Origin = new GeoPoint2(point.X - Width * 0.5, point.Y - Height * 0.5);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corner), corner, "Unknown corner.");
            }
        }

        /// <summary>
        /// Gets where a sheet lies: the first at <see cref="Origin"/>, each after it beside the one before, on the side
        /// <see cref="NewSheet"/> names, <see cref="SheetSpacing"/> apart.
        /// </summary>
        /// <param name="index">Which sheet, the first nought.</param>
        /// <returns>The sheet and the part of it inside its offsets.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The index is negative.</exception>
        /// <exception cref="InvalidOperationException">The offsets leave the sheet no room.</exception>
        public SheetFrame GetFrame(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Sheets are counted from nought.");
            }

            if (!HasRoom)
            {
                throw new InvalidOperationException(NoRoomMessage);
            }

            GeoPoint2 corner = GetCorner(index);
            Footprint usable = GetUsableArea(index);
            return new SheetFrame(index,
                new GeoRectangle2(corner.X, corner.Y, Width, Height),
                new GeoRectangle2(usable.MinX, usable.MinY, usable.Width, usable.Height));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Sheet[{0} {1}, {2:0.###} x {3:0.###} mm, scale {4:0.###}]",
                Size, Orientation, PaperWidth, PaperHeight, Scale);
        }

        /// <summary>Gets whether the offsets leave room on the sheet, across and up.</summary>
        internal bool HasRoom => PaperWidth - OffsetLeft - OffsetRight > 0.0 && PaperHeight - OffsetTop - OffsetBottom > 0.0;

        /// <summary>Gets what is said when the offsets leave the sheet no room.</summary>
        internal string NoRoomMessage => string.Format(CultureInfo.InvariantCulture,
            "The offsets of the sheet leave it no room: {0:0.###} mm across of {1:0.###}, {2:0.###} mm up of {3:0.###}.",
            PaperWidth - OffsetLeft - OffsetRight, PaperWidth, PaperHeight - OffsetTop - OffsetBottom, PaperHeight);

        /// <summary>Gets the lower left corner of a sheet.</summary>
        internal GeoPoint2 GetCorner(int index)
        {
            bool across = NewSheet == SheetDirection.Right || NewSheet == SheetDirection.Left;
            double along = index * ((across ? Width : Height) + SheetSpacing * Scale);
            switch (NewSheet)
            {
                case SheetDirection.Left:
                    return new GeoPoint2(Origin.X - along, Origin.Y);
                case SheetDirection.Top:
                    return new GeoPoint2(Origin.X, Origin.Y + along);
                case SheetDirection.Bottom:
                    return new GeoPoint2(Origin.X, Origin.Y - along);
                default:
                    return new GeoPoint2(Origin.X + along, Origin.Y);
            }
        }

        /// <summary>Gets the part of a sheet inside its offsets.</summary>
        internal Footprint GetUsableArea(int index)
        {
            GeoPoint2 corner = GetCorner(index);
            return new Footprint(
                corner.X + OffsetLeft * Scale,
                corner.Y + OffsetBottom * Scale,
                (PaperWidth - OffsetLeft - OffsetRight) * Scale,
                (PaperHeight - OffsetTop - OffsetBottom) * Scale);
        }
    }
}
