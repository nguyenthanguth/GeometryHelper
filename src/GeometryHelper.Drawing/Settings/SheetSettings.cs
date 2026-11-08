using System;
using GeometryHelper.Packing;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The sheet a drawing is laid out on: its size, A0 to A4 either way round or one of its own, the margin of its frame,
    /// the spacing of its views, its title block and whether it has a part list.
    /// </summary>
    /// <remarks>
    /// An A3 on its side is 420 by 297; with a margin of 10 its frame is 400 by 277, and the views are packed into the frame
    /// less the strip of the title block along its bottom, 40 high. Where the views do not fit at any scale allowed, a larger
    /// sheet is taken when <see cref="AllowLargerSheet"/> says so.
    /// </remarks>
    public sealed class SheetSettings
    {
        private double _customWidth;
        private double _customHeight;
        private double _margin = 10.0;
        private double _viewSpacing = 10.0;
        private TitleBlock _titleBlock = new TitleBlock();

        /// <summary>Gets or sets the size of the sheet, where it is not one of its own; A2 by default.</summary>
        public PaperSize Size { get; set; } = PaperSize.A2;

        /// <summary>Gets or sets which way round an A sheet lies; across, landscape, by default.</summary>
        public SheetOrientation Orientation { get; set; } = SheetOrientation.Landscape;

        /// <summary>
        /// Gets or sets the width of a sheet of its own, in millimetres; nought, the default, for the A sheet of <see cref="Size"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        public double CustomWidth
        {
            get => _customWidth;
            set => _customWidth = NotNegative(value, nameof(CustomWidth));
        }

        /// <summary>
        /// Gets or sets the height of a sheet of its own, in millimetres; nought, the default, for the A sheet of <see cref="Size"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        public double CustomHeight
        {
            get => _customHeight;
            set => _customHeight = NotNegative(value, nameof(CustomHeight));
        }

        /// <summary>Gets or sets how far the frame stands in from the edge of the sheet, in millimetres; 10 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        public double Margin
        {
            get => _margin;
            set => _margin = NotNegative(value, nameof(Margin));
        }

        /// <summary>
        /// Gets or sets the gap between two views on the sheet, and between a view and the frame, in millimetres; 10 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        public double ViewSpacing
        {
            get => _viewSpacing;
            set => _viewSpacing = NotNegative(value, nameof(ViewSpacing));
        }

        /// <summary>Gets or sets whether the sheet carries a part list; on by default.</summary>
        public bool PartList { get; set; } = true;

        /// <summary>
        /// Gets or sets whether a larger A sheet is taken where the views do not fit at any scale allowed; off by default, when
        /// the drawing says so in its warnings instead.
        /// </summary>
        public bool AllowLargerSheet { get; set; }

        /// <summary>Gets or sets the texts of the title block.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public TitleBlock TitleBlock
        {
            get => _titleBlock;
            set => _titleBlock = value ?? throw new ArgumentNullException(nameof(TitleBlock));
        }

        /// <summary>Gets whether the sheet is one of its own size: both <see cref="CustomWidth"/> and <see cref="CustomHeight"/> set.</summary>
        public bool IsCustom => _customWidth > 0.0 && _customHeight > 0.0;

        internal SheetSettings Clone()
        {
            var copy = (SheetSettings)MemberwiseClone();
            copy._titleBlock = _titleBlock.Clone();
            return copy;
        }

        private static double NotNegative(double value, string name)
        {
            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "The value has to be a finite number of nought or more.");
            }

            return value;
        }
    }
}
