using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The corner of the sheet a plot stamp is put in, as -PLOTSTAMP's Location names it: TL, TR, BL or BR.</summary>
    public enum CadPlotStampCorner
    {
        /// <summary>The lower left corner, BL, the corner AutoCAD starts with.</summary>
        BottomLeft,

        /// <summary>The lower right corner, BR.</summary>
        BottomRight,

        /// <summary>The upper left corner, TL.</summary>
        TopLeft,

        /// <summary>The upper right corner, TR.</summary>
        TopRight,
    }

    /// <summary>The units the offset and the text height of a plot stamp are given in, as -PLOTSTAMP's Units names them.</summary>
    public enum CadPlotStampUnits
    {
        /// <summary>Inches, the units AutoCAD starts with.</summary>
        Inches,

        /// <summary>Millimetres.</summary>
        Millimeters,

        /// <summary>Pixels, taken as 96 to the inch, a pixel of Windows.</summary>
        Pixels,
    }

    /// <summary>
    /// The plot stamp of AutoCAD's PLOTSTAMP and -PLOTSTAMP: a few lines of text, the name of the drawing, the layout, the date
    /// and time, the login name, the plotter, the sheet and the scale, and two fields of the user's own, put in a corner of the
    /// sheet of a plot, and the same words kept in a log file. The commands keep one for a session
    /// (<see cref="CadCommandSession.PlotStamp"/>) and hand a copy to the settings of each plot while <see cref="On"/>
    /// (<see cref="CadPlotSettings.PlotStamp"/>); the PDF writer puts it on the page.
    /// </summary>
    /// <remarks>
    /// With AutoCAD's own start-up values, a plot of Plan.dxf, model space, to GeometryHelper PDF at 1:20 on 9 June 2026 at
    /// 14:05:09 is stamped with three lines at the lower left of the printable area, 0.1 inch (2.54 mm) in from both its edges,
    /// in letters 0.2 inch (5.08 mm) high: the path of the drawing, 2026-06-09 14:05:09, and GeometryHelper PDF.
    /// </remarks>
    public sealed partial class CadPlotStamp
    {
        /// <summary>The text height AutoCAD starts with, in <see cref="Units"/>: 0.2.</summary>
        public const double StartHeight = 0.2;

        /// <summary>The offset from the corner AutoCAD starts with, along each axis, in <see cref="Units"/>: 0.1.</summary>
        public const double StartOffset = 0.1;

        /// <summary>The font AutoCAD starts with: Segoe UI.</summary>
        public const string StartFont = "Segoe UI";

        /// <summary>The log file AutoCAD starts with: plot.log.</summary>
        public const string StartLogFile = "plot.log";

        /// <summary>Gets or sets whether plots are stamped; off to start with.</summary>
        public bool On { get; set; }

        /// <summary>Gets or sets whether the stamp holds the name of the drawing, with its folder; on to start with.</summary>
        public bool DrawingName { get; set; } = true;

        /// <summary>Gets or sets whether the stamp holds the name of the layout plotted; off to start with.</summary>
        public bool LayoutName { get; set; }

        /// <summary>Gets or sets whether the stamp holds the date and time of the plot; on to start with.</summary>
        public bool DateAndTime { get; set; } = true;

        /// <summary>Gets or sets whether the stamp holds the login name of the user; off to start with.</summary>
        public bool LoginName { get; set; }

        /// <summary>Gets or sets whether the stamp holds the name of the plotter; on to start with.</summary>
        public bool DeviceName { get; set; } = true;

        /// <summary>Gets or sets whether the stamp holds the name of the sheet; off to start with.</summary>
        public bool PaperSize { get; set; }

        /// <summary>Gets or sets whether the stamp holds the plot scale; off to start with.</summary>
        public bool PlotScale { get; set; }

        /// <summary>Gets or sets the first field of the user's own; empty for none, as it starts.</summary>
        public string UserField1 { get; set; } = string.Empty;

        /// <summary>Gets or sets the second field of the user's own; empty for none, as it starts.</summary>
        public string UserField2 { get; set; } = string.Empty;

        /// <summary>Gets or sets whether each stamp is also written to the log file; on to start with.</summary>
        public bool WriteLog { get; set; } = true;

        /// <summary>Gets or sets the log file; a name with no folder is put beside the plot. <see cref="StartLogFile"/> to start with.</summary>
        public string LogFile { get; set; } = StartLogFile;

        /// <summary>Gets or sets the corner of the sheet the stamp is put in; the lower left to start with.</summary>
        public CadPlotStampCorner Location { get; set; } = CadPlotStampCorner.BottomLeft;

        /// <summary>Gets or sets whether the stamp reads up the sheet, a quarter turned, instead of along it; off to start with.</summary>
        public bool Vertical { get; set; }

        /// <summary>Gets or sets whether the stamp is turned half round; off to start with.</summary>
        public bool UpsideDown { get; set; }

        /// <summary>Gets or sets how far the stamp stands from the sides of the corner along X, in <see cref="Units"/>: <see cref="StartOffset"/> to start with.</summary>
        public double OffsetX { get; set; } = StartOffset;

        /// <summary>Gets or sets how far the stamp stands from the sides of the corner along Y, in <see cref="Units"/>.</summary>
        public double OffsetY { get; set; } = StartOffset;

        /// <summary>Gets or sets whether the offset is measured from the edge of the sheet; off, it is from the printable area, as it starts.</summary>
        public bool OffsetFromPaperBorder { get; set; }

        /// <summary>Gets or sets the font of the stamp: <see cref="StartFont"/> to start with.</summary>
        public string FontName { get; set; } = StartFont;

        /// <summary>Gets or sets the height of the letters, in <see cref="Units"/>: <see cref="StartHeight"/> to start with.</summary>
        public double TextHeight { get; set; } = StartHeight;

        /// <summary>Gets or sets whether the stamp is one line of fields set apart by two spaces; off, each field is a line, as it starts.</summary>
        public bool SingleLine { get; set; }

        /// <summary>Gets or sets the units of the offset and the text height; inches to start with.</summary>
        public CadPlotStampUnits Units { get; set; } = CadPlotStampUnits.Inches;

        /// <summary>Makes a copy that changes apart from this one.</summary>
        /// <returns>The copy.</returns>
        public CadPlotStamp Clone() => (CadPlotStamp)MemberwiseClone();

        /// <summary>
        /// Takes every setting of another stamp, as the Plot Stamp dialog's OK gives the session's stamp what it edited in a
        /// copy, so that every drawing sharing the stamp sees it.
        /// </summary>
        /// <param name="other">The stamp whose settings are taken.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other stamp is null.</exception>
        public void CopyFrom(CadPlotStamp other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (System.Reflection.PropertyInfo property in typeof(CadPlotStamp).GetProperties())
            {
                if (property.CanWrite && property.CanRead)
                {
                    property.SetValue(this, property.GetValue(other));
                }
            }
        }

        /// <summary>A length in <see cref="Units"/> in millimetres: 5.08 for 0.2 inch, 2.6458 for 10 pixels.</summary>
        /// <param name="value">The length.</param>
        /// <returns>The length in millimetres.</returns>
        public double ToMillimetres(double value) =>
            Units == CadPlotStampUnits.Inches ? value * 25.4
            : Units == CadPlotStampUnits.Pixels ? value * 25.4 / 96.0
            : value;

        /// <summary>
        /// The words of the stamp for a plot, in the order AutoCAD stamps them: the drawing, the layout, the date and time, the
        /// login name, the plotter, the sheet, the scale, then the two fields of the user's own that are not empty. One line
        /// where <see cref="SingleLine"/>, the fields set apart by two spaces; else a line of each.
        /// </summary>
        /// <param name="document">The document plotted.</param>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted: its plotter and sheet.</param>
        /// <param name="page">The plot laid on its sheet, for its scale.</param>
        /// <param name="when">When the plot is made.</param>
        /// <param name="login">The login name of the user.</param>
        /// <returns>The lines; none where no field is on.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the space, the settings or the page are null.</exception>
        public IReadOnlyList<string> Lines(
            CadDocument document,
            CadSpace space,
            CadPlotSettings settings,
            CadPlotPage page,
            DateTime when,
            string login
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            var fields = new List<string>();

            if (DrawingName)
            {
                fields.Add(document.HasFile ? document.FileName : document.Title);
            }

            if (LayoutName)
            {
                fields.Add(space.Name);
            }

            if (DateAndTime)
            {
                fields.Add(when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }

            if (LoginName)
            {
                fields.Add(login ?? string.Empty);
            }

            if (DeviceName)
            {
                fields.Add(settings.DeviceName);
            }

            if (PaperSize)
            {
                fields.Add(settings.Paper.Label);
            }

            if (PlotScale)
            {
                fields.Add(ScaleText(page.Scale));
            }

            if (!string.IsNullOrEmpty(UserField1))
            {
                fields.Add(UserField1);
            }

            if (!string.IsNullOrEmpty(UserField2))
            {
                fields.Add(UserField2);
            }

            return fields.Count == 0 ? Array.Empty<string>()
                : SingleLine ? new[] { string.Join("  ", fields) }
                : (IReadOnlyList<string>)fields;
        }

        /// <summary>A scale of millimetres of sheet to drawing unit as a ratio: 0.05 is 1:20, 2 is 2:1, to four places.</summary>
        /// <param name="scale">The scale.</param>
        /// <returns>The ratio.</returns>
        public static string ScaleText(double scale) =>
            scale >= 1.0
                ? scale.ToString("0.####", CultureInfo.InvariantCulture) + ":1"
                : "1:" + (1.0 / scale).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// Where a block of the stamp's size stands on a sheet, as a box in the millimetres of the sheet, Y upward from its lower
        /// left corner: in <see cref="Location"/>'s corner of the printable area, or of the whole sheet where
        /// <see cref="OffsetFromPaperBorder"/>, in by the offset on both sides. A <see cref="Vertical"/> stamp has its width and
        /// height changed over, as it lies on the sheet.
        /// </summary>
        /// <param name="page">The plot laid on its sheet.</param>
        /// <param name="width">The width of the block of text, in millimetres.</param>
        /// <param name="height">The height of the block of text, in millimetres.</param>
        /// <returns>The box the stamp takes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the page is null.</exception>
        public DisplayBox Footprint(CadPlotPage page, double width, double height) =>
            Footprint(page, width, height, false, 1.0);

        /// <summary>
        /// Where a block of the stamp's size stands on a sheet, as <see cref="Footprint(CadPlotPage, double, double)"/>, the
        /// corner taken of the image as plotted, as AutoCAD's help puts it: a plot upside down turns the sheet half round, so
        /// that a stamp at the lower left stands at the upper right of the sheet; and the sheet's units given, a sheet of
        /// pixels at 96 to the inch (GeometryHelper PNG and JPG) taking 96 / 25.4 to the millimetre.
        /// </summary>
        /// <param name="page">The plot laid on its sheet.</param>
        /// <param name="width">The width of the block of text, in millimetres.</param>
        /// <param name="height">The height of the block of text, in millimetres.</param>
        /// <param name="plotUpsideDown">Whether the plot is upside down (<see cref="CadPlotSettings.UpsideDown"/>).</param>
        /// <param name="unitsPerMillimetre">The sheet's units to a millimetre: 1 for a sheet in millimetres.</param>
        /// <returns>The box the stamp takes, in the sheet's units, Y upward from its lower left corner.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the page is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the units to a millimetre are not above nought.</exception>
        public DisplayBox Footprint(
            CadPlotPage page,
            double width,
            double height,
            bool plotUpsideDown,
            double unitsPerMillimetre
        )
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (!(unitsPerMillimetre > 0.0) || double.IsInfinity(unitsPerMillimetre))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitsPerMillimetre),
                    "A sheet has a finite number of units to a millimetre above nought."
                );
            }

            double sheetWidth = page.SheetWidth;
            double sheetHeight = page.SheetHeight;
            DisplayBox Turned(DisplayBox box) =>
                new DisplayBox(
                    sheetWidth - box.MaxX,
                    sheetHeight - box.MaxY,
                    sheetWidth - box.MinX,
                    sheetHeight - box.MinY
                );

            DisplayBox reference = OffsetFromPaperBorder
                ? new DisplayBox(0.0, 0.0, sheetWidth, sheetHeight)
                : page.Printable;

            if (plotUpsideDown)
            {
                reference = Turned(reference);
            }

            double w = (Vertical ? height : width) * unitsPerMillimetre;
            double h = (Vertical ? width : height) * unitsPerMillimetre;
            double dx = ToMillimetres(OffsetX) * unitsPerMillimetre;
            double dy = ToMillimetres(OffsetY) * unitsPerMillimetre;
            bool left = Location == CadPlotStampCorner.BottomLeft || Location == CadPlotStampCorner.TopLeft;
            bool bottom = Location == CadPlotStampCorner.BottomLeft || Location == CadPlotStampCorner.BottomRight;
            double x = left ? reference.MinX + dx : reference.MaxX - dx - w;
            double y = bottom ? reference.MinY + dy : reference.MaxY - dy - h;
            var placed = new DisplayBox(x, y, x + w, y + h);
            return plotUpsideDown ? Turned(placed) : placed;
        }

        /// <summary>The line the log file takes for a stamp: its fields set apart by two spaces, on a line of their own.</summary>
        /// <param name="lines">The lines of the stamp.</param>
        /// <returns>The log line, with no line end.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the lines are null.</exception>
        public static string LogLine(IReadOnlyList<string> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            return string.Join("  ", lines);
        }

        /// <summary>The path of the log file for a plot written to a file: <see cref="LogFile"/>, beside the plot where it names no folder.</summary>
        /// <param name="plotFile">The file the plot was written to.</param>
        /// <returns>The path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the plot file is null.</exception>
        public string LogPath(string plotFile)
        {
            if (plotFile == null)
            {
                throw new ArgumentNullException(nameof(plotFile));
            }

            string name = string.IsNullOrWhiteSpace(LogFile) ? StartLogFile : LogFile.Trim();
            return Path.IsPathRooted(name) ? name : Path.Combine(Path.GetDirectoryName(plotFile) ?? string.Empty, name);
        }
    }
}
