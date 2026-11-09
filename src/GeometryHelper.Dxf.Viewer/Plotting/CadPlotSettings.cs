using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What part of a space is plotted, as the What to plot list of AutoCAD's Plot dialog.</summary>
    public enum CadPlotArea
    {
        /// <summary>What the view shows.</summary>
        Display,

        /// <summary>Every object of the space.</summary>
        Extents,

        /// <summary>The drawing's limits, LIMMIN to LIMMAX in model space, the layout's in a layout.</summary>
        Limits,

        /// <summary>A window of two points.</summary>
        Window,

        /// <summary>The sheet of a layout, its origin at the printable area's corner; a layout only.</summary>
        Layout,
    }

    /// <summary>The plot style table a plot is coloured by, the three the presenter makes in code.</summary>
    public enum CadPlotStyleTable
    {
        /// <summary>Every colour plots black, as AutoCAD's monochrome.ctb.</summary>
        Monochrome,

        /// <summary>Every colour plots as the grey of its brightness, as AutoCAD's grayscale.ctb.</summary>
        Grayscale,

        /// <summary>Colours plot as they are shown, white turned black on the white sheet.</summary>
        None,
    }

    /// <summary>A scale of the Scale list of the Plot dialog: its name and the paper millimetres to the drawing units they stand for.</summary>
    public sealed class CadPlotScale
    {
        /// <summary>Makes a scale.</summary>
        /// <param name="name">The name shown, as "1:20".</param>
        /// <param name="paperUnits">The millimetres of the paper.</param>
        /// <param name="drawingUnits">The units of the drawing they stand for.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when either number is not a finite number above nought.</exception>
        public CadPlotScale(string name, double paperUnits, double drawingUnits)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (!(paperUnits > 0.0) || !(drawingUnits > 0.0) || double.IsInfinity(paperUnits) || double.IsInfinity(drawingUnits))
            {
                throw new ArgumentException("A scale is of finite numbers above nought.", nameof(paperUnits));
            }

            PaperUnits = paperUnits;
            DrawingUnits = drawingUnits;
        }

        /// <summary>Gets the name shown, as "1:20".</summary>
        public string Name { get; }

        /// <summary>Gets the millimetres of the paper: 1 for 1:20.</summary>
        public double PaperUnits { get; }

        /// <summary>Gets the units of the drawing they stand for: 20 for 1:20.</summary>
        public double DrawingUnits { get; }

        /// <summary>
        /// Gets the scales of the list for a drawing: 1:1, 1:2, 1:5, 1:10, 1:20, 1:50, 1:100, 1:200, 1:500 and 1:1000, then
        /// the drawing's own (its ACAD_SCALELIST) that are none of those, in the drawing's order; 1:25 of a drawing is listed
        /// after 1:1000, its 1:20 not again.
        /// </summary>
        /// <param name="database">The drawing; null for the standard scales alone.</param>
        /// <returns>The scales, a new list.</returns>
        public static IReadOnlyList<CadPlotScale> ListFor(DxfDatabase database)
        {
            var list = new List<CadPlotScale>();

            foreach (double units in new[] { 1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0 })
            {
                list.Add(new CadPlotScale("1:" + units.ToString(CultureInfo.InvariantCulture), 1.0, units));
            }

            DxfObjectContextCollection scales = null;

            try
            {
                scales = database?.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPlotScale: the drawing's list of scales could not be read.", exception);
            }

            foreach (DxfObjectContext context in (IEnumerable<DxfObjectContext>)scales ?? Array.Empty<DxfObjectContext>())
            {
                if (context is DxfAnnotationScale scale && scale.PaperUnits > 0.0 && scale.DrawingUnits > 0.0
                    && !list.Exists(s => CadPlotSettings.SameRatio(s.PaperUnits, s.DrawingUnits, scale.PaperUnits, scale.DrawingUnits)))
                {
                    list.Add(new CadPlotScale(scale.Name, scale.PaperUnits, scale.DrawingUnits));
                }
            }

            return list;
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// How a space is plotted, as AutoCAD's Plot dialog sets it: the plotter, the sheet and its turn, what part of the space,
    /// at what scale and where on the sheet, the plot style table and the lineweights. A drawing keeps it in the plot settings
    /// of each layout (the part AcDbPlotSettings of its LAYOUT, model space's in the layout Model): <see cref="Read"/> takes
    /// them from there and <see cref="ApplyTo"/> writes them back, as Apply to Layout and Save changes to layout do.
    /// </summary>
    /// <remarks>
    /// A plan of model space plotted on ISO A3 on its side at 1:20, the window from (120, 60) to (7 620, 5 360) centred,
    /// is 375 by 265 on a printable area of 410 by 287: the plot is moved 17.5 right and 11 up of the printable area's corner.
    /// <see cref="CadPlotPage.Compute"/> works the sheet out.
    /// </remarks>
    public sealed class CadPlotSettings
    {
        /// <summary>The name of the presenter's own plotter, which writes a PDF: "GeometryHelper PDF".</summary>
        public const string PdfDevice = "GeometryHelper PDF";

        private CadPaper _paper = CadPaper.Default;
        private double _paperUnits = 1.0;
        private double _drawingUnits = 1.0;

        /// <summary>Gets or sets the name of the plotter; <see cref="PdfDevice"/> by default, the one the presenter plots with.</summary>
        public string DeviceName { get; set; } = PdfDevice;

        /// <summary>Gets or sets the sheet; ISO A3 by default.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the sheet set is null.</exception>
        public CadPaper Paper
        {
            get => _paper;
            set => _paper = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets what part of the space is plotted; what the view shows by default.</summary>
        public CadPlotArea Area { get; set; } = CadPlotArea.Display;

        /// <summary>Gets or sets the window plotted where <see cref="Area"/> is <see cref="CadPlotArea.Window"/>, in drawing units.</summary>
        public DisplayBox Window { get; set; } = DisplayBox.Empty;

        /// <summary>Gets or sets whether the plot is scaled to fill the printable area; on by default.</summary>
        public bool FitToPaper { get; set; } = true;

        /// <summary>Gets or sets the millimetres of the paper of the scale; 1 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number above nought.</exception>
        public double PaperUnits
        {
            get => _paperUnits;
            set => _paperUnits = Positive(value);
        }

        /// <summary>Gets or sets the drawing units the <see cref="PaperUnits"/> stand for: 20 for 1:20; 1 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number above nought.</exception>
        public double DrawingUnits
        {
            get => _drawingUnits;
            set => _drawingUnits = Positive(value);
        }

        /// <summary>Gets or sets whether the plot is centred on the printable area; on by default. A plot of a layout's sheet is never centred.</summary>
        public bool Centered { get; set; } = true;

        /// <summary>Gets or sets how far right of the printable area's corner the plot is moved, in millimetres, where it is not centred.</summary>
        public double OffsetX { get; set; }

        /// <summary>Gets or sets how far up of the printable area's corner the plot is moved, in millimetres.</summary>
        public double OffsetY { get; set; }

        /// <summary>Gets or sets whether the sheet lies on its side, wider than it is high; on by default.</summary>
        public bool Landscape { get; set; } = true;

        /// <summary>Gets or sets whether the plot is turned half round on the sheet.</summary>
        public bool UpsideDown { get; set; }

        /// <summary>Gets or sets the plot style table; monochrome by default.</summary>
        public CadPlotStyleTable StyleTable { get; set; } = CadPlotStyleTable.Monochrome;

        /// <summary>Gets or sets whether the plot style table is used; off, colours plot as with <see cref="CadPlotStyleTable.None"/>. On by default.</summary>
        public bool PlotWithPlotStyles { get; set; } = true;

        /// <summary>Gets or sets whether the lines plot at their lineweights; off, every line is the thinnest the plotter draws. On by default.</summary>
        public bool PlotLineweights { get; set; } = true;

        /// <summary>Gets or sets whether the lineweights are scaled with the plot: a line of 0.5 plotted at 1:2 is 0.25 wide, at 2:1 it is 1 wide.</summary>
        public bool ScaleLineweights { get; set; }

        /// <summary>
        /// Gets or sets whether the transparency of objects is plotted. The presenter's display lists carry no transparency,
        /// so a plot is the same either way; it is kept for the dialog and is not written to the drawing, which AutoCAD keeps
        /// it in no group of the plot settings a DXF holds.
        /// </summary>
        public bool PlotTransparency { get; set; }

        /// <summary>Gets the plot style table the colours are plotted by: <see cref="StyleTable"/>, or none where <see cref="PlotWithPlotStyles"/> is off.</summary>
        public CadPlotStyleTable EffectiveStyleTable => PlotWithPlotStyles ? StyleTable : CadPlotStyleTable.None;

        /// <summary>Gets the scale as millimetres of paper per drawing unit, where the plot is not fitted: 0.05 for 1:20.</summary>
        public double Scale => PaperUnits / DrawingUnits;

        /// <summary>Makes a copy that changes apart from this one.</summary>
        /// <returns>The copy.</returns>
        public CadPlotSettings Clone() => (CadPlotSettings)MemberwiseClone();

        /// <summary>
        /// Reads how a space is plotted from the drawing, as AutoCAD's Plot dialog opens on a layout's page setup: the plot
        /// settings of the space's LAYOUT, model space's from the layout Model. The sheet is the standard one of the name the
        /// layout gives and its margins, else one made of the layout's size and margins, ISO A3 where it gives none; a named
        /// view plots as the display, a layout's sheet of model space as its extents; <c>monochrome.ctb</c> is read as
        /// <see cref="CadPlotStyleTable.Monochrome"/>, <c>grayscale.ctb</c> as <see cref="CadPlotStyleTable.Grayscale"/>,
        /// any other table and none as <see cref="CadPlotStyleTable.None"/>. A space whose settings cannot be read is given
        /// the defaults, a layout plotting its sheet at 1:1.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space.</param>
        /// <returns>The settings, a new object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the space is null.</exception>
        public static CadPlotSettings Read(CadDocument document, CadSpace space)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            return From(LayoutOf(document, space), space.IsModel, space.Name);
        }

        /// <summary>
        /// Gets the page setups of a drawing by name, as the Name list of the Plot dialog's Page setup offers them: the
        /// PLOTSETTINGS of its dictionary ACAD_PLOTSETTINGS, in ordinal order of their names, letters of either case the same.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>The names, a new list; empty where the drawing has none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<string> PageSetupNames(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var names = new List<string>();

            foreach (KeyValuePair<string, DxfPlotSettings> pair in PageSetups(document))
            {
                names.Add(pair.Key);
            }

            return names;
        }

        /// <summary>
        /// Reads a page setup of the drawing by name, as choosing it in the Page setup list of the Plot dialog applies it to the
        /// space plotted: the settings read as <see cref="Read"/> reads a layout's.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space it is applied to: a layout's sheet is plotted as the layout only in a layout.</param>
        /// <param name="name">The page setup's name, letters of either case the same.</param>
        /// <returns>The settings, a new object; null where the drawing has no page setup of the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static CadPlotSettings ReadPageSetup(CadDocument document, CadSpace space, string name)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            foreach (KeyValuePair<string, DxfPlotSettings> pair in PageSetups(document ?? throw new ArgumentNullException(nameof(document))))
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return From(pair.Value, space.IsModel, name);
                }
            }

            return null;
        }

        /// <summary>The page setups of the drawing's ACAD_PLOTSETTINGS by name, in ordinal order.</summary>
        private static List<KeyValuePair<string, DxfPlotSettings>> PageSetups(CadDocument document)
        {
            var found = new List<KeyValuePair<string, DxfPlotSettings>>();

            try
            {
                DxfDBDictionary dictionary = document.Database.PlotSettingsDictionary;

                foreach (DxfDBDictionaryEntry entry in (IEnumerable<DxfDBDictionaryEntry>)dictionary ?? Array.Empty<DxfDBDictionaryEntry>())
                {
                    if (entry.Value is DxfPlotSettings settings && !(entry.Value is DxfLayout) && !string.IsNullOrEmpty(entry.Key))
                    {
                        found.Add(new KeyValuePair<string, DxfPlotSettings>(entry.Key, settings));
                    }
                }
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPlotSettings: the drawing's page setups could not be read.", exception);
            }

            found.Sort((a, b) =>
            {
                int byName = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(a.Key, b.Key);
            });
            return found;
        }

        /// <summary>Settings read from plot settings: a layout's or a page setup's; the defaults of the space where there are none.</summary>
        private static CadPlotSettings From(DxfPlotSettings layout, bool isModel, string what)
        {
            var settings = new CadPlotSettings();

            if (!isModel)
            {
                settings.Area = CadPlotArea.Layout;
                settings.FitToPaper = false;
                settings.Centered = false;
            }

            if (layout == null)
            {
                return settings;
            }

            try
            {
                GeoPoint2 size = layout.PlotPaperSize;
                DxfExtents2d margins = layout.PlotPaperMargins;
                CadPaper paper = CadPaper.FromLayout(layout.CanonicalMediaName, size.X, size.Y, margins.MinPoint.X, margins.MinPoint.Y, margins.MaxPoint.X, margins.MaxPoint.Y);

                if (paper != null)
                {
                    settings.Paper = paper;
                    int turn = (int)layout.PlotRotation;
                    bool turned = turn == 1 || turn == 3;
                    settings.Landscape = (paper.Width >= paper.Height) != turned;
                    settings.UpsideDown = turn >= 2;
                }

                switch (layout.PlotType)
                {
                    case DxfPlotType.Extents:
                        settings.Area = CadPlotArea.Extents;
                        break;
                    case DxfPlotType.Limits:
                        settings.Area = CadPlotArea.Limits;
                        break;
                    case DxfPlotType.Window:
                        settings.Area = CadPlotArea.Window;
                        break;
                    case DxfPlotType.Layout:
                        settings.Area = isModel ? CadPlotArea.Extents : CadPlotArea.Layout;
                        break;
                    default:
                        settings.Area = CadPlotArea.Display;
                        break;
                }

                DxfExtents2d window = layout.PlotWindowArea;
                settings.Window = new DisplayBox(
                    Math.Min(window.MinPoint.X, window.MaxPoint.X),
                    Math.Min(window.MinPoint.Y, window.MaxPoint.Y),
                    Math.Max(window.MinPoint.X, window.MaxPoint.X),
                    Math.Max(window.MinPoint.Y, window.MaxPoint.Y));

                settings.FitToPaper = layout.UseStandardScale && layout.StdScaleType == DxfStdScaleType.ScaleToFit;
                DxfCustomScale custom = layout.CustomPrintScale;
                double inch = layout.PlotPaperUnits == DxfPlotPaperUnit.Inches ? 25.4 : 1.0;

                if (custom.Numerator > 0.0 && custom.Denominator > 0.0 && !double.IsInfinity(custom.Numerator * inch) && !double.IsInfinity(custom.Denominator))
                {
                    settings.PaperUnits = custom.Numerator * inch;
                    settings.DrawingUnits = custom.Denominator;
                }

                settings.Centered = layout.PlotCentered;
                GeoPoint2 origin = layout.PlotOrigin;
                settings.OffsetX = origin.X;
                settings.OffsetY = origin.Y;
                string table = layout.CurrentStyleSheet ?? string.Empty;
                settings.StyleTable = table.IndexOf("monochrome", StringComparison.OrdinalIgnoreCase) >= 0
                    ? CadPlotStyleTable.Monochrome
                    : table.IndexOf("grayscale", StringComparison.OrdinalIgnoreCase) >= 0 ? CadPlotStyleTable.Grayscale : CadPlotStyleTable.None;
                settings.PlotWithPlotStyles = layout.PlotPlotStyles;
                settings.PlotLineweights = layout.PrintLineweights;
                settings.ScaleLineweights = layout.ScaleLineweights;
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadPlotSettings: the plot settings of " + what + " could not be read; the defaults are taken.", exception);
            }

            return settings;
        }

        /// <summary>
        /// Writes the settings into the plot settings of a space's LAYOUT, as Apply to Layout does, one undo step named PLOT:
        /// the plotter's name (group 2), the sheet's name, size and margins in millimetres (4, 40 to 45, 72), its turn (73) as
        /// AutoCAD reckons it from the sheet as named, the offset (46, 47), what is plotted (74) and the window (48, 49, 140,
        /// 141), the scale (142, 143, and 75 with 147 and flag 16 where it is one of AutoCAD's standard scales, scaled to fit
        /// as 75 of 0), centring (flag 4), the table (7) as <c>monochrome.ctb</c>, <c>grayscale.ctb</c> or none, and the
        /// flags of plot styles (32), lineweights (128) and their scaling (64). Plot transparency is not written. A layout's
        /// limits follow its sheet, as <see cref="DxfLayout"/> makes them.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space.</param>
        /// <returns>What came of the edit; refused where the space has no LAYOUT to write to.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the space is null.</exception>
        public CadEditResult ApplyTo(CadDocument document, CadSpace space)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (LayoutOf(document, space) == null)
            {
                return CadEditResult.Refuse("The space " + space.Name + " has no LAYOUT to keep its page setup in.");
            }

            return document.ApplyEdit("PLOT", Planned(space), EditOf(space));
        }

        /// <summary>The change a page setup written to a space plans: a setting of the drawing, named by the space.</summary>
        internal static IReadOnlyList<CadEditRecord> Planned(CadSpace space)
            => new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Page setup", null, space.Name) };

        /// <summary>The edit that writes a copy of the settings as they are now into a space's LAYOUT, found again by name in the drawing the edit sees.</summary>
        internal Action<CadEditScope> EditOf(CadSpace space)
        {
            string name = space.Name;
            bool isModel = space.IsModel;
            CadPlotSettings settings = Clone();
            return scope =>
            {
                CadSpace target = null;

                foreach (CadSpace each in scope.Document.Spaces)
                {
                    if (target == null && string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase) && each.IsModel == isModel)
                    {
                        target = each;
                    }
                }

                settings.Write(LayoutOf(scope.Document, target) ?? throw new InvalidOperationException("The space " + name + " has no LAYOUT."));
                target.ForgetPaper();
            };
        }

        /// <summary>Whether two ratios are one, to a part in a thousand million.</summary>
        internal static bool SameRatio(double paperA, double drawingA, double paperB, double drawingB)
        {
            double a = paperA / drawingA;
            double b = paperB / drawingB;
            return Math.Abs(a - b) <= 1E-9 * Math.Max(Math.Abs(a), Math.Abs(b));
        }

        /// <summary>The LAYOUT that keeps a space's plot settings: the space's own, the layout Model for model space; null where there is none.</summary>
        internal static DxfLayout LayoutOf(CadDocument document, CadSpace space)
        {
            if (space == null)
            {
                return null;
            }

            if (!space.IsModel)
            {
                return space.Layout;
            }

            DxfLayout found = null;

            foreach (DxfLayout layout in document.Database.Layouts)
            {
                if (found == null && (layout.ModelType || string.Equals(layout.LayoutName, "Model", StringComparison.OrdinalIgnoreCase)))
                {
                    found = layout;
                }
            }

            return found;
        }

        private void Write(DxfLayout layout)
        {
            CadPaper paper = Paper;
            bool turned = Landscape != (paper.Width >= paper.Height);
            layout.PlotConfigurationName = DeviceName ?? PdfDevice;
            layout.PlotPaperUnits = DxfPlotPaperUnit.Millimeters;

            // The margins first, checked against the sheet the layout has, then the sheet: margins of 5 fit any sheet listed.
            var margins = new DxfExtents2d(new GeoPoint2(paper.MarginLeft, paper.MarginBottom), new GeoPoint2(paper.MarginRight, paper.MarginTop));
            GeoPoint2 size = layout.PlotPaperSize;

            if (size.X > 0.0 && size.Y > 0.0 && !(margins.MinPoint.X + margins.MaxPoint.X < size.X && margins.MinPoint.Y + margins.MaxPoint.Y < size.Y))
            {
                layout.CanonicalMediaName = paper.CanonicalName;
                layout.PlotPaperMargins = margins;
            }
            else
            {
                layout.PlotPaperMargins = margins;
                layout.CanonicalMediaName = paper.CanonicalName;
            }

            layout.PlotRotation = (DxfPlotRotation)((turned ? 1 : 0) + (UpsideDown ? 2 : 0));
            layout.PlotOrigin = new GeoPoint2(OffsetX, OffsetY);

            switch (Area)
            {
                case CadPlotArea.Extents:
                    layout.PlotType = DxfPlotType.Extents;
                    break;
                case CadPlotArea.Limits:
                    layout.PlotType = DxfPlotType.Limits;
                    break;
                case CadPlotArea.Window:
                    layout.PlotType = DxfPlotType.Window;
                    break;
                case CadPlotArea.Layout:
                    layout.PlotType = layout.ModelType ? DxfPlotType.Extents : DxfPlotType.Layout;
                    break;
                default:
                    layout.PlotType = DxfPlotType.Display;
                    break;
            }

            if (!Window.IsEmpty)
            {
                layout.PlotWindowArea = new DxfExtents2d(new GeoPoint2(Window.MinX, Window.MinY), new GeoPoint2(Window.MaxX, Window.MaxY));
            }

            layout.CustomPrintScale = new DxfCustomScale(PaperUnits, DrawingUnits);
            DxfStdScaleType standard = FitToPaper ? DxfStdScaleType.ScaleToFit : Standard(PaperUnits, DrawingUnits);
            bool isStandard = FitToPaper || standard != DxfStdScaleType.ScaleToFit;

            if (isStandard)
            {
                layout.StdScaleType = standard;
            }

            layout.UseStandardScale = isStandard;
            layout.PlotCentered = Centered && Area != CadPlotArea.Layout;
            layout.CurrentStyleSheet = StyleTable == CadPlotStyleTable.Monochrome ? "monochrome.ctb" : StyleTable == CadPlotStyleTable.Grayscale ? "grayscale.ctb" : string.Empty;
            layout.PlotPlotStyles = PlotWithPlotStyles;
            layout.PrintLineweights = PlotLineweights;
            layout.ScaleLineweights = ScaleLineweights;
        }

        /// <summary>AutoCAD's standard scale of a ratio, 1:1 to 1:100 and 2:1 to 1000:1; scaled to fit, its mark of none, for any other.</summary>
        private static DxfStdScaleType Standard(double paper, double drawing)
        {
            var standards = new[]
            {
                new KeyValuePair<double, DxfStdScaleType>(1.0, DxfStdScaleType.StdScale1To1),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 2.0, DxfStdScaleType.StdScale1To2),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 4.0, DxfStdScaleType.StdScale1To4),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 5.0, DxfStdScaleType.StdScale1To5),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 8.0, DxfStdScaleType.StdScale1To8),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 10.0, DxfStdScaleType.StdScale1To10),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 16.0, DxfStdScaleType.StdScale1To16),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 20.0, DxfStdScaleType.StdScale1To20),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 30.0, DxfStdScaleType.StdScale1To30),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 40.0, DxfStdScaleType.StdScale1To40),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 50.0, DxfStdScaleType.StdScale1To50),
                new KeyValuePair<double, DxfStdScaleType>(1.0 / 100.0, DxfStdScaleType.StdScale1To100),
                new KeyValuePair<double, DxfStdScaleType>(2.0, DxfStdScaleType.StdScale2To1),
                new KeyValuePair<double, DxfStdScaleType>(4.0, DxfStdScaleType.StdScale4To1),
                new KeyValuePair<double, DxfStdScaleType>(8.0, DxfStdScaleType.StdScale8To1),
                new KeyValuePair<double, DxfStdScaleType>(10.0, DxfStdScaleType.StdScale10To1),
                new KeyValuePair<double, DxfStdScaleType>(100.0, DxfStdScaleType.StdScale100To1),
                new KeyValuePair<double, DxfStdScaleType>(1000.0, DxfStdScaleType.StdScale1000To1),
            };

            foreach (KeyValuePair<double, DxfStdScaleType> pair in standards)
            {
                if (SameRatio(paper, drawing, pair.Key, 1.0))
                {
                    return pair.Value;
                }
            }

            return DxfStdScaleType.ScaleToFit;
        }

        private static double Positive(double value)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A unit of a scale is a finite number above nought.");
            }

            return value;
        }
    }
}
