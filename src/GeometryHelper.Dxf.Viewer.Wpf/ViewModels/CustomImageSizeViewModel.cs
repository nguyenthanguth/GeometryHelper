using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Custom Image Size, from Custom… of a raster device's Paper size list: the width and the height in pixels, each a whole
    /// number of 16 to 8 000. A side outside the range is drawn red and greys OK; 2480 by 1754 makes "2480 x 1754 Pixels
    /// (Custom)".
    /// </summary>
    public sealed class CustomImageSizeViewModel : ObservableObject
    {
        private string _width;
        private string _height;

        /// <summary>Makes the dialog on the size chosen before, or on 1920 by 1080 where that is no image.</summary>
        /// <param name="current">The size chosen before; null for none.</param>
        public CustomImageSizeViewModel(CadPaper current)
        {
            CadPaper start = current != null && current.IsPixels && !current.IsCustomChoice ? current : CadPaper.DefaultPixels;
            _width = ((int)start.Width).ToString(CultureInfo.InvariantCulture);
            _height = ((int)start.Height).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Gets or sets the width as typed.</summary>
        public string WidthText
        {
            get => _width;
            set
            {
                if (Set(ref _width, value ?? string.Empty))
                {
                    RaiseSize();
                }
            }
        }

        /// <summary>Gets or sets the height as typed.</summary>
        public string HeightText
        {
            get => _height;
            set
            {
                if (Set(ref _height, value ?? string.Empty))
                {
                    RaiseSize();
                }
            }
        }

        /// <summary>Gets whether the width reads as a whole number of 16 to 8 000.</summary>
        public bool IsWidthValid => Side(_width) > 0;

        /// <summary>Gets whether the height reads as a whole number of 16 to 8 000.</summary>
        public bool IsHeightValid => Side(_height) > 0;

        /// <summary>Gets whether OK is offered: both sides valid.</summary>
        public bool CanOk => IsWidthValid && IsHeightValid;

        /// <summary>Gets the tip of a side out of range.</summary>
        public string RangeTip => "16 to 8 000";

        /// <summary>Gets the shape of the image within 70 by 50, its width in the small picture.</summary>
        public double PictureWidth => CanOk ? 70.0 * Math.Min(1.0, (Side(_width) / (double)Side(_height)) / 1.4) : 70.0;

        /// <summary>Gets the height of the small picture.</summary>
        public double PictureHeight => CanOk ? 50.0 * Math.Min(1.0, 1.4 / (Side(_width) / (double)Side(_height))) : 50.0;

        /// <summary>Gets the size made, as the list shows it; null while a side is not valid.</summary>
        public CadPaper Size => CanOk ? CadPaper.Pixels(Side(_width), Side(_height)) : null;

        /// <summary>Gets the note beside the picture: what the list shows, "2480 x 1754 Pixels (Custom)".</summary>
        public string Note => "Each side 16 to 8 000 pixels, whole numbers." + (CanOk ? " The size is added to the Paper size list as \"" + Size.Label + "\" and kept between sessions." : string.Empty);

        /// <summary>A side as typed, 0 where it is not a whole number of 16 to 8 000.</summary>
        private static int Side(string text)
            => int.TryParse((text ?? string.Empty).Trim().Replace(" ", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= CadPaper.MinPixels && value <= CadPaper.MaxPixels ? value : 0;

        private void RaiseSize()
        {
            Raise(nameof(IsWidthValid));
            Raise(nameof(IsHeightValid));
            Raise(nameof(CanOk));
            Raise(nameof(PictureWidth));
            Raise(nameof(PictureHeight));
            Raise(nameof(Size));
            Raise(nameof(Note));
        }
    }
}
