using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD 2021's Plot Stamp dialog and its Advanced Options over a stamp: the seven fields, two user fields, a preview of
    /// where the stamp goes on the sheet, and the location, orientation, offset, text, units and log file. A user field typed
    /// "." is cleared, as -PLOTSTAMP clears it; numbers are checked by <see cref="TryAccept"/>.
    /// </summary>
    public sealed class PlotStampViewModel : ObservableObject
    {
        private static readonly string[] LocationNames = { "Bottom Left", "Bottom Right", "Top Left", "Top Right" };
        private static readonly string[] UnitNames = { "Inches", "Millimeters", "Pixels" };
        private readonly CadPlotStamp _stamp;
        private string _offsetX;
        private string _offsetY;
        private string _height;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a stamp, which it changes.</summary>
        /// <param name="stamp">The stamp, a copy of the session's.</param>
        public PlotStampViewModel(CadPlotStamp stamp)
        {
            _stamp = stamp ?? throw new ArgumentNullException(nameof(stamp));
            _offsetX = Text(stamp.OffsetX);
            _offsetY = Text(stamp.OffsetY);
            _height = Text(stamp.TextHeight);
        }

        /// <summary>Gets the stamp as the dialog leaves it.</summary>
        public CadPlotStamp Stamp => _stamp;

        /// <summary>Gets or sets whether the stamp holds the drawing name.</summary>
        public bool DrawingName
        {
            get => _stamp.DrawingName;
            set => _stamp.DrawingName = value;
        }

        /// <summary>Gets or sets whether the stamp holds the layout name.</summary>
        public bool LayoutName
        {
            get => _stamp.LayoutName;
            set => _stamp.LayoutName = value;
        }

        /// <summary>Gets or sets whether the stamp holds the date and time.</summary>
        public bool DateAndTime
        {
            get => _stamp.DateAndTime;
            set => _stamp.DateAndTime = value;
        }

        /// <summary>Gets or sets whether the stamp holds the login name.</summary>
        public bool LoginName
        {
            get => _stamp.LoginName;
            set => _stamp.LoginName = value;
        }

        /// <summary>Gets or sets whether the stamp holds the device name.</summary>
        public bool DeviceName
        {
            get => _stamp.DeviceName;
            set => _stamp.DeviceName = value;
        }

        /// <summary>Gets or sets whether the stamp holds the paper size.</summary>
        public bool PaperSize
        {
            get => _stamp.PaperSize;
            set => _stamp.PaperSize = value;
        }

        /// <summary>Gets or sets whether the stamp holds the plot scale.</summary>
        public bool PlotScale
        {
            get => _stamp.PlotScale;
            set => _stamp.PlotScale = value;
        }

        /// <summary>Gets or sets the first user field; "." clears it.</summary>
        public string UserField1
        {
            get => _stamp.UserField1;
            set
            {
                _stamp.UserField1 = Field(value);
                RaiseFields();
            }
        }

        /// <summary>Gets or sets the second user field; "." clears it.</summary>
        public string UserField2
        {
            get => _stamp.UserField2;
            set
            {
                _stamp.UserField2 = Field(value);
                RaiseFields();
            }
        }

        /// <summary>Gets the corners offered: Bottom Left, Bottom Right, Top Left and Top Right.</summary>
        public IReadOnlyList<string> Locations => LocationNames;

        /// <summary>Gets or sets the corner of the sheet the stamp is put in.</summary>
        public string Location
        {
            get => LocationNames[(int)_stamp.Location];
            set
            {
                int at = Array.IndexOf(LocationNames, value);

                if (at >= 0)
                {
                    _stamp.Location = (CadPlotStampCorner)at;
                    Raise(nameof(Location));
                    RaisePreview();
                }
            }
        }

        /// <summary>Gets the orientations offered: Horizontal and Vertical.</summary>
        public IReadOnlyList<string> Orientations { get; } = new[] { "Horizontal", "Vertical" };

        /// <summary>Gets or sets whether the stamp reads along the sheet or up it.</summary>
        public string Orientation
        {
            get => _stamp.Vertical ? "Vertical" : "Horizontal";
            set
            {
                _stamp.Vertical = value == "Vertical";
                Raise(nameof(Orientation));
                RaisePreview();
            }
        }

        /// <summary>Gets or sets whether the stamp is turned half round.</summary>
        public bool UpsideDown
        {
            get => _stamp.UpsideDown;
            set => _stamp.UpsideDown = value;
        }

        /// <summary>Gets or sets the offset along X, as typed.</summary>
        public string OffsetX
        {
            get => _offsetX;
            set => Set(ref _offsetX, value ?? string.Empty);
        }

        /// <summary>Gets or sets the offset along Y, as typed.</summary>
        public string OffsetY
        {
            get => _offsetY;
            set => Set(ref _offsetY, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the offset is from the printable area (true) or the paper border.</summary>
        public bool OffsetFromPrintableArea
        {
            get => !_stamp.OffsetFromPaperBorder;
            set
            {
                _stamp.OffsetFromPaperBorder = !value;
                Raise(nameof(OffsetFromPrintableArea));
                Raise(nameof(OffsetFromPaperBorder));
            }
        }

        /// <summary>Gets or sets whether the offset is from the paper border.</summary>
        public bool OffsetFromPaperBorder
        {
            get => _stamp.OffsetFromPaperBorder;
            set => OffsetFromPrintableArea = !value;
        }

        /// <summary>Gets the fonts offered: those of Windows, by name.</summary>
        public IReadOnlyList<string> Fonts { get; } = Fonts2().DefaultIfEmpty(CadPlotStamp.StartFont).ToList();

        /// <summary>Gets or sets the font.</summary>
        public string FontName
        {
            get => _stamp.FontName;
            set => _stamp.FontName = string.IsNullOrWhiteSpace(value) ? CadPlotStamp.StartFont : value;
        }

        /// <summary>Gets or sets the text height, as typed.</summary>
        public string TextHeight
        {
            get => _height;
            set => Set(ref _height, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the stamp is one line.</summary>
        public bool SingleLine
        {
            get => _stamp.SingleLine;
            set => _stamp.SingleLine = value;
        }

        /// <summary>Gets the units offered: Inches, Millimeters and Pixels.</summary>
        public IReadOnlyList<string> UnitsOffered => UnitNames;

        /// <summary>Gets or sets the units of the offset and the text height.</summary>
        public string Units
        {
            get => UnitNames[(int)_stamp.Units];
            set
            {
                int at = Array.IndexOf(UnitNames, value);

                if (at >= 0)
                {
                    _stamp.Units = (CadPlotStampUnits)at;
                    Raise(nameof(Units));
                }
            }
        }

        /// <summary>Gets or sets whether each stamp is written to the log file.</summary>
        public bool WriteLog
        {
            get => _stamp.WriteLog;
            set
            {
                _stamp.WriteLog = value;
                Raise(nameof(WriteLog));
            }
        }

        /// <summary>Gets or sets the log file.</summary>
        public string LogFile
        {
            get => _stamp.LogFile;
            set
            {
                _stamp.LogFile = string.IsNullOrWhiteSpace(value) ? CadPlotStamp.StartLogFile : value.Trim();
                Raise(nameof(LogFile));
            }
        }

        /// <summary>Gets the left of the stamp's mark on the preview's sheet of 170 by 120.</summary>
        public double MarkLeft =>
            _stamp.Location == CadPlotStampCorner.BottomLeft || _stamp.Location == CadPlotStampCorner.TopLeft
                ? 6.0
                : 170.0 - 6.0 - MarkWidth;

        /// <summary>Gets the top of the stamp's mark on the preview's sheet.</summary>
        public double MarkTop =>
            _stamp.Location == CadPlotStampCorner.TopLeft || _stamp.Location == CadPlotStampCorner.TopRight
                ? 6.0
                : 120.0 - 6.0 - MarkHeight;

        /// <summary>Gets the width of the stamp's mark: long along the sheet, narrow up it.</summary>
        public double MarkWidth => _stamp.Vertical ? 8.0 : 70.0;

        /// <summary>Gets the height of the stamp's mark.</summary>
        public double MarkHeight => _stamp.Vertical ? 60.0 : 10.0;

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets or sets how the window shows Advanced Options over this dialog: true on OK.</summary>
        public Func<PlotStampViewModel, bool> ShowAdvanced { get; set; }

        /// <summary>Gets or sets how the window asks a log file; null where cancelled.</summary>
        public Func<string, string> BrowseLogFile { get; set; }

        /// <summary>Advanced…: Advanced Options over a copy of the stamp, which the stamp takes on its OK.</summary>
        /// <returns>True where OK was pressed.</returns>
        public bool Advanced()
        {
            if (ShowAdvanced == null)
            {
                return false;
            }

            var copy = new PlotStampViewModel(_stamp.Clone()) { BrowseLogFile = BrowseLogFile };

            if (!ShowAdvanced(copy))
            {
                return false;
            }

            _stamp.CopyFrom(copy.Stamp);
            _offsetX = Text(_stamp.OffsetX);
            _offsetY = Text(_stamp.OffsetY);
            _height = Text(_stamp.TextHeight);

            foreach (
                string name in new[]
                {
                    nameof(Location),
                    nameof(Orientation),
                    nameof(UpsideDown),
                    nameof(OffsetX),
                    nameof(OffsetY),
                    nameof(OffsetFromPrintableArea),
                    nameof(OffsetFromPaperBorder),
                    nameof(FontName),
                    nameof(TextHeight),
                    nameof(SingleLine),
                    nameof(Units),
                    nameof(WriteLog),
                    nameof(LogFile),
                }
            )
            {
                Raise(name);
            }

            RaisePreview();
            return true;
        }

        /// <summary>The item of the Field lists that shows no field.</summary>
        public const string NoField = "< none >";

        private string _parameterFile = string.Empty;

        /// <summary>
        /// Gets what the Field 1 and Field 2 lists offer: &lt; none &gt;, the user's fields (Add / Edit…), and a field typed
        /// before (-PLOTSTAMP) that is not in the list.
        /// </summary>
        public IReadOnlyList<string> UserFieldChoices =>
            new[] { NoField }
                .Concat(_stamp.UserFields)
                .Concat(new[] { _stamp.UserField1, _stamp.UserField2 }.Where(f => f.Length > 0))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        /// <summary>Gets or sets the item of the Field 1 list: a user field, or &lt; none &gt;.</summary>
        public string UserField1Choice
        {
            get => _stamp.UserField1.Length == 0 ? NoField : _stamp.UserField1;
            set => UserField1 = value == NoField ? string.Empty : value;
        }

        /// <summary>Gets or sets the item of the Field 2 list: a user field, or &lt; none &gt;.</summary>
        public string UserField2Choice
        {
            get => _stamp.UserField2.Length == 0 ? NoField : _stamp.UserField2;
            set => UserField2 = value == NoField ? string.Empty : value;
        }

        /// <summary>
        /// Gives the stamp the user's fields as User Defined Fields leaves them; a Field 1 or 2 that showed one no longer
        /// listed goes back to &lt; none &gt;.
        /// </summary>
        /// <param name="fields">The fields, in their order.</param>
        public void SetUserFields(IEnumerable<string> fields)
        {
            _stamp.UserFields = (fields ?? Array.Empty<string>()).ToList();

            if (!_stamp.UserFields.Contains(_stamp.UserField1))
            {
                _stamp.UserField1 = string.Empty;
            }

            if (!_stamp.UserFields.Contains(_stamp.UserField2))
            {
                _stamp.UserField2 = string.Empty;
            }

            RaiseFields();
        }

        /// <summary>Gets the plot stamp parameter file loaded or saved last; empty for none.</summary>
        public string ParameterFile
        {
            get => _parameterFile;
            private set => Set(ref _parameterFile, value ?? string.Empty);
        }

        /// <summary>Load…: every setting of a plot stamp parameter file (.pss) taken; false with the reason said where it is refused.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Whether it was read.</returns>
        public bool LoadParameterFile(string path)
        {
            CadPlotStamp read = _stamp.Clone();

            try
            {
                read.LoadParameterFile(path);
            }
            catch (Exception e)
                when (e is IOException
                    || e is InvalidDataException
                    || e is UnauthorizedAccessException
                    || e is ArgumentException
                )
            {
                Message = e.Message;
                return false;
            }

            _stamp.CopyFrom(read);
            _offsetX = Text(_stamp.OffsetX);
            _offsetY = Text(_stamp.OffsetY);
            _height = Text(_stamp.TextHeight);
            ParameterFile = path;
            Message = string.Empty;
            Raise(string.Empty);
            return true;
        }

        /// <summary>Save As…: the stamp written to a plot stamp parameter file (.pss); false with the reason said where it is not.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Whether it was written.</returns>
        public bool SaveParameterFile(string path)
        {
            if (!TryAccept())
            {
                return false;
            }

            try
            {
                _stamp.SaveParameterFile(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Message = e.Message;
                return false;
            }

            ParameterFile = path;
            return true;
        }

        private void RaiseFields()
        {
            Raise(nameof(UserFieldChoices));
            Raise(nameof(UserField1));
            Raise(nameof(UserField2));
            Raise(nameof(UserField1Choice));
            Raise(nameof(UserField2Choice));
        }

        /// <summary>Browse… of the log file.</summary>
        public void BrowseLog()
        {
            string file = BrowseLogFile?.Invoke(LogFile);

            if (!string.IsNullOrEmpty(file))
            {
                LogFile = file;
            }
        }

        /// <summary>OK: the offsets (numbers) and the text height (above nought) taken into the stamp; false with the reason said.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool TryAccept()
        {
            if (!Number(_offsetX, out double x) || !Number(_offsetY, out double y))
            {
                Message = "Requires an offset as x,y.";
                return false;
            }

            if (!Number(_height, out double height) || !(height > 0.0))
            {
                Message = "The text height is a number above nought.";
                return false;
            }

            _stamp.OffsetX = x;
            _stamp.OffsetY = y;
            _stamp.TextHeight = height;
            Message = string.Empty;
            return true;
        }

        private static IEnumerable<string> Fonts2()
        {
            try
            {
                return System
                    .Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                GeometryHelperLog.Debug(
                    "PlotStampViewModel: the fonts of Windows were not listed (" + e.Message + ")."
                );
                return Array.Empty<string>();
            }
        }

        private static string Field(string value)
        {
            string typed = (value ?? string.Empty).Trim();
            return typed == "." ? string.Empty : typed;
        }

        private static string Text(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static bool Number(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        private void RaisePreview()
        {
            Raise(nameof(MarkLeft));
            Raise(nameof(MarkTop));
            Raise(nameof(MarkWidth));
            Raise(nameof(MarkHeight));
        }
    }

    /// <summary>
    /// AutoCAD 2021's Image Adjust dialog over IMAGEADJUST's request: Brightness, Contrast and Fade, each a slider and a box
    /// from 0 to 100, a preview of the first image's picture adjusted as the canvas draws it, and Reset to 50, 50 and 0.
    /// </summary>
    public sealed class ImageAdjustViewModel : ObservableObject
    {
        private readonly CadImageAdjustRequest _request;
        private readonly BitmapSource _picture;
        private int _brightness;
        private int _contrast;
        private int _fade;
        private ImageSource _preview;

        /// <summary>Makes the dialog over IMAGEADJUST's request.</summary>
        /// <param name="request">The request.</param>
        public ImageAdjustViewModel(CadImageAdjustRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _brightness = request.Brightness;
            _contrast = request.Contrast;
            _fade = request.Fade;
            _picture = Thumbnail(request.ImageFile);
            Draw();
        }

        /// <summary>Gets the line under the sliders: how many images are adjusted.</summary>
        public string Caption =>
            _request.Count == 1
                ? "1 image selected."
                : _request.Count.ToString(CultureInfo.InvariantCulture)
                    + " images selected: each takes the values set, from 50, 50 and 0.";

        /// <summary>Gets or sets the brightness, 0 to 100.</summary>
        public int Brightness
        {
            get => _brightness;
            set => Level(ref _brightness, value, nameof(Brightness));
        }

        /// <summary>Gets or sets the contrast, 0 to 100.</summary>
        public int Contrast
        {
            get => _contrast;
            set => Level(ref _contrast, value, nameof(Contrast));
        }

        /// <summary>Gets or sets the fade, 0 to 100.</summary>
        public int Fade
        {
            get => _fade;
            set => Level(ref _fade, value, nameof(Fade));
        }

        /// <summary>Gets the preview: the picture adjusted; null where the picture is not found.</summary>
        public ImageSource Preview
        {
            get => _preview;
            private set => Set(ref _preview, value);
        }

        /// <summary>Gets whether the picture was found, the preview drawn.</summary>
        public bool HasPicture => _picture != null;

        /// <summary>Reset: brightness 50, contrast 50, fade 0.</summary>
        public void Reset()
        {
            Brightness = 50;
            Contrast = 50;
            Fade = 0;
        }

        /// <summary>OK: the values are given to the images once the dialog closes.</summary>
        public void Accept()
        {
            _request.Brightness = _brightness;
            _request.Contrast = _contrast;
            _request.Fade = _fade;
            _request.IsAccepted = true;
        }

        private void Level(ref int field, int value, string name)
        {
            if (Set(ref field, Math.Max(0, Math.Min(100, value)), name))
            {
                Draw();
            }
        }

        /// <summary>The picture of a file, at most 160 by 120, as 32-bit colour; null where it cannot be read.</summary>
        private static BitmapSource Thumbnail(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(file, UriKind.Absolute);
                image.DecodePixelWidth = 160;
                image.EndInit();
                var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                return converted;
            }
            catch (Exception e)
                when (e is IOException
                    || e is NotSupportedException
                    || e is UriFormatException
                    || e is ArgumentException
                    || e is InvalidOperationException
                )
            {
                GeometryHelperLog.Debug(
                    "ImageAdjustViewModel: the picture " + file + " was not read (" + e.Message + ")."
                );
                return null;
            }
        }

        /// <summary>The picture adjusted as the canvas adjusts an image: contrast about the middle, brightness, then fade towards black.</summary>
        private void Draw()
        {
            if (_picture == null)
            {
                Preview = null;
                return;
            }

            int width = _picture.PixelWidth;
            int height = _picture.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            _picture.CopyPixels(pixels, stride, 0);
            double k = _contrast / 50.0;
            double shift = (0.5 - 0.5 * k + (_brightness - 50) / 100.0) * 255.0;
            double keep = 1.0 - _fade / 100.0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    double value = keep * (k * pixels[i + channel] + shift);
                    pixels[i + channel] = (byte)Math.Max(0.0, Math.Min(255.0, Math.Round(value)));
                }
            }

            BitmapSource adjusted = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride
            );
            adjusted.Freeze();
            Preview = adjusted;
        }
    }

    /// <summary>
    /// The classic Attribute Extraction dialog of ATTEXT over its request: File Format (CDF, SDF, DXX), Select Objects with
    /// the number found, the template file (greyed for DXX) and the output file, whose extension follows the format.
    /// </summary>
    public sealed class AttributeExtractionViewModel : ObservableObject
    {
        private readonly CadAttextRequest _request;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over ATTEXT's request.</summary>
        /// <param name="request">The request.</param>
        public AttributeExtractionViewModel(CadAttextRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets or sets whether the extract is comma delimited.</summary>
        public bool IsCdf
        {
            get => _request.Format == CadAttextFormat.Cdf;
            set => Format(value, CadAttextFormat.Cdf);
        }

        /// <summary>Gets or sets whether the extract is space delimited.</summary>
        public bool IsSdf
        {
            get => _request.Format == CadAttextFormat.Sdf;
            set => Format(value, CadAttextFormat.Sdf);
        }

        /// <summary>Gets or sets whether the extract is a DXF extract.</summary>
        public bool IsDxf
        {
            get => _request.Format == CadAttextFormat.Dxf;
            set => Format(value, CadAttextFormat.Dxf);
        }

        /// <summary>Gets whether a template is needed: for CDF and SDF.</summary>
        public bool NeedsTemplate => _request.Format != CadAttextFormat.Dxf;

        /// <summary>Gets the line beside Select Objects: "Number found: 12", or the whole drawing where none were selected.</summary>
        public string NumberFound =>
            _request.Selected == null
                ? "Number found: all"
                : "Number found: " + _request.Selected.Count.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets or sets the template file.</summary>
        public string TemplateFile
        {
            get => _request.TemplateFile;
            set
            {
                _request.TemplateFile = value ?? string.Empty;
                Raise(nameof(TemplateFile));
            }
        }

        /// <summary>Gets or sets the output file.</summary>
        public string OutputFile
        {
            get => _request.OutputFile;
            set
            {
                _request.OutputFile = value ?? string.Empty;
                Raise(nameof(OutputFile));
            }
        }

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets or sets how the window asks the template file; null where cancelled.</summary>
        public Func<string, string> BrowseTemplate { get; set; }

        /// <summary>Gets or sets how the window asks the output file; null where cancelled.</summary>
        public Func<string, string> BrowseOutput { get; set; }

        /// <summary>Template File…: the file the window asks.</summary>
        public void ChooseTemplate()
        {
            string file = BrowseTemplate?.Invoke(TemplateFile);

            if (!string.IsNullOrEmpty(file))
            {
                TemplateFile = file;
            }
        }

        /// <summary>Output File…: the file the window asks.</summary>
        public void ChooseOutput()
        {
            string file = BrowseOutput?.Invoke(OutputFile);

            if (!string.IsNullOrEmpty(file))
            {
                OutputFile = file;
            }
        }

        /// <summary>Select Objects &lt;: the dialog closes, ATTEXT asks the objects and shows it again.</summary>
        public void SelectObjects() => _request.IsSelecting = true;

        /// <summary>OK: false with the reason said where a CDF or SDF extract has no template or there is no output file.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool TryAccept()
        {
            if (NeedsTemplate && string.IsNullOrWhiteSpace(_request.TemplateFile))
            {
                Message = "A CDF or SDF extract needs a template file.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_request.OutputFile))
            {
                Message = "Enter an output file.";
                return false;
            }

            Message = string.Empty;
            _request.IsAccepted = true;
            return true;
        }

        private void Format(bool on, CadAttextFormat format)
        {
            if (!on || _request.Format == format)
            {
                return;
            }

            _request.Format = format;
            string output = _request.OutputFile ?? string.Empty;

            if (output.Length > 0)
            {
                OutputFile = Path.ChangeExtension(output, format == CadAttextFormat.Dxf ? ".dxx" : ".txt");
            }

            Raise(nameof(IsCdf));
            Raise(nameof(IsSdf));
            Raise(nameof(IsDxf));
            Raise(nameof(NeedsTemplate));
        }
    }

    /// <summary>
    /// A row of the Xbind dialog's tree: an external reference, a kind of definition or a definition. A reference or a kind
    /// opens and closes (▸ closed, ▾ open), as the nodes of AutoCAD's tree; it starts closed.
    /// </summary>
    public sealed class XbindNode : ObservableObject
    {
        private bool _isExpanded;

        internal XbindNode(string text, int depth, string kind, string name, XbindNode parent = null)
        {
            Text = text;
            Depth = depth;
            Kind = kind;
            Name = name;
            Parent = parent;
        }

        /// <summary>Gets the row it is under: a kind's reference, a definition's kind; null for a reference.</summary>
        public XbindNode Parent { get; }

        /// <summary>Gets whether the row opens and closes: a reference or a kind.</summary>
        public bool CanExpand => Name == null;

        /// <summary>Gets or sets whether the rows under it are shown.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (CanExpand && Set(ref _isExpanded, value))
                {
                    Raise(nameof(Glyph));
                }
            }
        }

        /// <summary>Gets the mark before the words: ▾ open, ▸ closed, nothing for a definition.</summary>
        public string Glyph =>
            !CanExpand ? string.Empty
            : _isExpanded ? "▾"
            : "▸";

        /// <summary>Gets whether every row above it is open, so that the tree shows it.</summary>
        public bool IsShown => Parent == null || Parent.IsExpanded && Parent.IsShown;

        /// <summary>Gets the words the row shows.</summary>
        public string Text { get; }

        /// <summary>Gets how deep the row stands: 0 a reference, 1 a kind, 2 a definition.</summary>
        public int Depth { get; }

        /// <summary>Gets the indent of the row, 16 a level.</summary>
        public Thickness Indent => new Thickness(Depth * 16, 0, 0, 0);

        /// <summary>Gets the kind of the definitions under it, or of the definition; null for a reference.</summary>
        public string Kind { get; }

        /// <summary>Gets the name of the definition; null for a reference or a kind.</summary>
        public string Name { get; }

        /// <summary>Returns the words the row shows.</summary>
        /// <returns>The words.</returns>
        public override string ToString() => Text;
    }

    /// <summary>
    /// AutoCAD 2021's Xbind dialog over XBIND's request: the Xrefs tree on the left (each reference, its kinds, their
    /// definitions), Definitions to Bind on the right, Add and Remove between them; OK binds those listed.
    /// </summary>
    public sealed class XbindViewModel : ObservableObject
    {
        private readonly CadXbindRequest _request;
        private XbindNode _picked;
        private XbindNode _chosen;

        /// <summary>Makes the dialog over XBIND's request.</summary>
        /// <param name="request">The request.</param>
        public XbindViewModel(CadXbindRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            var rows = new List<XbindNode>();

            foreach (CadXbindReference reference in request.References)
            {
                var top = new XbindNode(reference.Name, 0, null, null);
                rows.Add(top);

                foreach (KeyValuePair<string, IReadOnlyList<string>> kind in reference.Definitions)
                {
                    var group = new XbindNode(kind.Key, 1, kind.Key, null, top);
                    rows.Add(group);
                    rows.AddRange(kind.Value.Select(n => new XbindNode(n, 2, kind.Key, n, group)));
                }
            }

            Xrefs = rows;
        }

        /// <summary>Gets the rows of the Xrefs tree, each reference followed by its kinds and their definitions.</summary>
        public IReadOnlyList<XbindNode> Xrefs { get; }

        /// <summary>Gets the rows the tree shows: the references, and below each row open the rows under it.</summary>
        public IReadOnlyList<XbindNode> ShownXrefs => Xrefs.Where(r => r.IsShown).ToList();

        /// <summary>Opens a reference or a kind, or closes it; a definition is left as it is.</summary>
        /// <param name="row">The row.</param>
        public void Toggle(XbindNode row)
        {
            if (row == null || !row.CanExpand)
            {
                return;
            }

            row.IsExpanded = !row.IsExpanded;
            Raise(nameof(ShownXrefs));
        }

        /// <summary>Gets the definitions to bind.</summary>
        public ObservableCollection<XbindNode> ToBind { get; } = new ObservableCollection<XbindNode>();

        /// <summary>Gets or sets the row picked in the Xrefs tree.</summary>
        public XbindNode Picked
        {
            get => _picked;
            set => Set(ref _picked, value);
        }

        /// <summary>Gets or sets the row picked in Definitions to Bind.</summary>
        public XbindNode Chosen
        {
            get => _chosen;
            set => Set(ref _chosen, value);
        }

        /// <summary>Add -&gt;: the definition picked, or every definition under the kind or the reference picked, listed to bind once.</summary>
        public void Add()
        {
            if (_picked == null)
            {
                return;
            }

            int at = Xrefs.ToList().IndexOf(_picked);

            for (int i = at; i < Xrefs.Count; i++)
            {
                XbindNode row = Xrefs[i];

                if (i > at && row.Depth <= _picked.Depth)
                {
                    break;
                }

                if (row.Name != null && !ToBind.Any(t => t.Kind == row.Kind && t.Name == row.Name))
                {
                    ToBind.Add(row);
                }
            }
        }

        /// <summary>&lt;- Remove: the definition picked in Definitions to Bind taken off the list.</summary>
        public void Remove()
        {
            if (_chosen != null)
            {
                ToBind.Remove(_chosen);
                Chosen = null;
            }
        }

        /// <summary>OK: the definitions listed are bound once the dialog closes.</summary>
        public void Accept()
        {
            _request.ToBind.Clear();

            foreach (XbindNode row in ToBind)
            {
                _request.ToBind.Add(new KeyValuePair<string, string>(row.Kind, row.Name));
            }

            _request.IsAccepted = true;
        }
    }
}
