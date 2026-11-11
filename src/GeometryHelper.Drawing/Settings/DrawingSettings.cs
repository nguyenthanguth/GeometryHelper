using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Everything one drawing may vary (16-settings.md): its views, the scales allowed, its dimensions, its marks, its
    /// symbols, its sheet, how the views go round the front view and how the DXF is laid out. There is one kind of drawing;
    /// a single-part, an assembly and a general arrangement drawing are the presets <see cref="Part"/>,
    /// <see cref="Assembly"/> and <see cref="General"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing copies its settings when it is made (<see cref="DrawingBuilder.Build"/>), so changing a settings object
    /// afterwards, or a preset, changes nothing already made. Each preset is a new object each time it is read.
    /// </para>
    /// <para>
    /// The scale of the views is the largest of <see cref="Scales"/> at which every view, with its dimensions, fits the
    /// sheet: a beam 3 000 long with its end plate fits an A3 at 1:10 but not at 1:5. The scales are the denominators, 20 for
    /// 1:20, tried from the smallest up.
    /// </para>
    /// </remarks>
    public sealed class DrawingSettings
    {
        private static readonly double[] DefaultScales = { 1, 2, 5, 10, 15, 20, 25, 50, 100, 200, 500 };

        /// <summary>The fields a mark's format names but attributes, as the document reads them back.</summary>
        private static readonly MarkField[] Fields =
        {
            MarkField.Mark,
            MarkField.Name,
            MarkField.Profile,
            MarkField.Material,
            MarkField.Length,
            MarkField.Count,
            MarkField.Diameter,
        };

        private DimensionSettings _dimensions = new DimensionSettings();
        private ViewSettings _views = new ViewSettings();
        private MarkSettings _marks = new MarkSettings();
        private SymbolSettings _symbols = new SymbolSettings();
        private SheetSettings _sheet = new SheetSettings();

        /// <summary>
        /// Gets the settings of a drawing of one part: its front, top and right views, each dimensioned with every hole,
        /// a section across it at its middle, and its own mark, on an A3.
        /// </summary>
        public static DrawingSettings Part
        {
            get
            {
                var settings = new DrawingSettings();
                settings.Views.Sections.Add(new SectionViewRequest("A", ViewKind.Right, 0.5));
                ViewSettings.Refill(
                    settings.Views.Dimensioned,
                    new[] { ViewKind.Front, ViewKind.Top, ViewKind.Right, ViewKind.Section }
                );
                settings.Marks.MainParts = true;
                settings.Marks.Fittings = false;
                settings.Sheet.Size = PaperSize.A3;
                return settings;
            }
        }

        /// <summary>
        /// Gets the settings of a drawing of an assembly: the main part's frame, its front, top and right views, the front and
        /// top views dimensioned, the fittings marked, and a part list, on an A2. The default.
        /// </summary>
        public static DrawingSettings Assembly
        {
            get
            {
                var settings = new DrawingSettings();
                settings.Marks.MainParts = false;
                settings.Marks.Fittings = true;
                return settings;
            }
        }

        /// <summary>
        /// Gets the settings of a general arrangement drawing of many assemblies: a plan from above and an elevation from the
        /// front, no dimensions of parts, the main part of each assembly marked, the levels of the objects that give one
        /// (<see cref="SymbolSettings.LevelMarks"/>), no hidden lines, on an A1.
        /// </summary>
        public static DrawingSettings General
        {
            get
            {
                var settings = new DrawingSettings();
                ViewSettings.Refill(settings.Views.Kinds, new[] { ViewKind.Front, ViewKind.Top });
                settings.Views.Dimensioned.Clear();
                settings.Views.HiddenLines = false;
                settings.Marks.MainParts = true;
                settings.Marks.Fittings = false;
                settings.Sheet.Size = PaperSize.A1;
                settings.Symbols.LevelMarks = true;
                settings.Sheet.PartList = false;
                return settings;
            }
        }

        /// <summary>Gets or sets which views the drawing has and how they see the model.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public ViewSettings Views
        {
            get => _views;
            set => _views = value ?? throw new ArgumentNullException(nameof(Views));
        }

        /// <summary>
        /// Gets the scales allowed, as denominators: 20 for 1:20. 1, 2, 5, 10, 15, 20, 25, 50, 100, 200 and 500 by default.
        /// </summary>
        public IList<double> Scales { get; } = new List<double>(DefaultScales);

        /// <summary>
        /// Gets or sets the rules and sizes of the dimensions; the scale in it is set by the drawing to each view's.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public DimensionSettings Dimensions
        {
            get => _dimensions;
            set => _dimensions = value ?? throw new ArgumentNullException(nameof(Dimensions));
        }

        /// <summary>Gets or sets which parts are marked and how.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public MarkSettings Marks
        {
            get => _marks;
            set => _marks = value ?? throw new ArgumentNullException(nameof(Marks));
        }

        /// <summary>Gets or sets how the symbols are drawn: centre lines, weld symbols, section, detail and level marks.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public SymbolSettings Symbols
        {
            get => _symbols;
            set => _symbols = value ?? throw new ArgumentNullException(nameof(Symbols));
        }

        /// <summary>Gets or sets the sheet.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public SheetSettings Sheet
        {
            get => _sheet;
            set => _sheet = value ?? throw new ArgumentNullException(nameof(Sheet));
        }

        /// <summary>Gets or sets how the views along the axes go round the front view (D9); third angle by default.</summary>
        public ProjectionMethod ProjectionMethod { get; set; } = ProjectionMethod.ThirdAngle;

        /// <summary>Gets or sets how the drawing is laid out in its DXF (D10); views as viewports of a layout by default.</summary>
        public DrawingExportMode ExportMode { get; set; } = DrawingExportMode.LayoutAndViewports;

        /// <summary>Makes a copy of the settings that shares nothing with them.</summary>
        /// <returns>The copy.</returns>
        public DrawingSettings Clone()
        {
            var copy = new DrawingSettings
            {
                _views = _views.Clone(),
                _dimensions = CloneDimensions(_dimensions),
                _marks = _marks.Clone(),
                _symbols = _symbols.Clone(),
                _sheet = _sheet.Clone(),
                ProjectionMethod = ProjectionMethod,
                ExportMode = ExportMode,
            };

            ViewSettings.Refill(copy.Scales, Scales);
            return copy;
        }

        /// <summary>
        /// The scales allowed, checked and sorted from the smallest denominator up, each once.
        /// </summary>
        internal double[] SortedScales()
        {
            var scales = new List<double>();

            foreach (double scale in Scales)
            {
                if (!(scale > 0.0) || double.IsInfinity(scale))
                {
                    throw new ArgumentException(
                        "A scale allowed has to be a finite number above nought.",
                        nameof(Scales)
                    );
                }

                if (!scales.Contains(scale))
                {
                    scales.Add(scale);
                }
            }

            if (scales.Count == 0)
            {
                throw new ArgumentException("A drawing needs at least one scale allowed.", nameof(Scales));
            }

            scales.Sort();
            return scales.ToArray();
        }

        /// <summary>The dimension settings copied, the scale set to a view's.</summary>
        internal static DimensionSettings CloneDimensions(DimensionSettings source, double scale = 1.0) =>
            new DimensionSettings
            {
                Overall = source.Overall,
                Chain = source.Chain,
                Cumulative = source.Cumulative,
                Ordinate = source.Ordinate,
                HolesByGroup = source.HolesByGroup,
                MergeEqual = source.MergeEqual,
                Scale = scale,
                TextHeight = source.TextHeight,
                TierSpacing = source.TierSpacing,
                Rounding = source.Rounding,
            };

        /// <summary>The settings as values by name, as the drawing document keeps them.</summary>
        internal List<KeyValuePair<string, string>> ToValues()
        {
            var values = new List<KeyValuePair<string, string>>();

            void Add(string name, string value) => values.Add(new KeyValuePair<string, string>(name, value));
            void Number(string name, double value) => Add(name, Write(value));
            void Flag(string name, bool value) => Add(name, value ? "1" : "0");

            Add("Views.Kinds", string.Join(",", Names(_views.Kinds)));
            Add("Views.Dimensioned", string.Join(",", Names(_views.Dimensioned)));
            Add("Views.DimensionedViews", string.Join(",", _views.DimensionedViews));
            Add("Views.MarkedViews", string.Join(",", _views.MarkedViews));
            Number("Views.CropMargin", _views.CropMargin);
            Number("Views.SectionDepth", _views.SectionDepth);
            Number("Views.SmoothAngle", _views.SmoothAngleRad);
            Flag("Views.HiddenLines", _views.HiddenLines);
            Add("Views.Sections", _views.Sections.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < _views.Sections.Count; i++)
            {
                SectionViewRequest section = _views.Sections[i];
                string key = "Views.Section." + i.ToString(CultureInfo.InvariantCulture);
                Add(key + ".Name", section.Name);
                Add(key + ".LookingAs", section.LookingAs.ToString());
                Number(key + ".Share", section.Share);

                if (section.CutLine.HasValue)
                {
                    GeoLine2 cut = section.CutLine.Value;
                    Add(
                        key + ".CutLine",
                        string.Join(
                            ",",
                            Write(cut.StartPoint.X),
                            Write(cut.StartPoint.Y),
                            Write(cut.EndPoint.X),
                            Write(cut.EndPoint.Y)
                        )
                    );
                    Number(key + ".Depth", section.Depth);
                }
            }

            Add("Views.Details", _views.Details.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < _views.Details.Count; i++)
            {
                DetailViewRequest detail = _views.Details[i];
                string key = "Views.Detail." + i.ToString(CultureInfo.InvariantCulture);
                Add(key + ".Name", detail.Name);
                Add(key + ".Of", detail.Of.ToString());
                Number(key + ".CentreX", detail.Area.Center.X);
                Number(key + ".CentreY", detail.Area.Center.Y);
                Number(key + ".Width", detail.Area.Width);
                Number(key + ".Height", detail.Area.Height);
                Number(key + ".Enlargement", detail.Enlargement);
            }

            var scales = new List<string>();

            foreach (double scale in Scales)
            {
                scales.Add(Write(scale));
            }

            Add("Scales", string.Join(",", scales));
            Flag("Dimensions.Overall", _dimensions.Overall);
            Flag("Dimensions.Chain", _dimensions.Chain);
            Flag("Dimensions.Cumulative", _dimensions.Cumulative);
            Flag("Dimensions.Ordinate", _dimensions.Ordinate);
            Flag("Dimensions.HolesByGroup", _dimensions.HolesByGroup);
            Flag("Dimensions.MergeEqual", _dimensions.MergeEqual);
            Number("Dimensions.TextHeight", _dimensions.TextHeight);
            Number("Dimensions.TierSpacing", _dimensions.TierSpacing);
            Number("Dimensions.Rounding", _dimensions.Rounding);
            Flag("Marks.MainParts", _marks.MainParts);
            Flag("Marks.Fittings", _marks.Fittings);
            Number("Marks.TextHeight", _marks.TextHeight);
            Number("Marks.Offset", _marks.Offset);

            foreach (
                (string kind, MarkFormat format) in new[]
                {
                    ("Part", _marks.Content.Part),
                    ("Assembly", _marks.Content.Assembly),
                    ("Bolt", _marks.Content.Bolt),
                }
            )
            {
                string key = "Marks.Content." + kind;
                Add(key + ".Separator", format.Separator);
                Add(key + ".Fields", format.Fields.Count.ToString(CultureInfo.InvariantCulture));

                for (int i = 0; i < format.Fields.Count; i++)
                {
                    MarkField field = format.Fields[i];
                    Add(
                        key + ".Field." + i.ToString(CultureInfo.InvariantCulture),
                        field.Kind == MarkFieldKind.Attribute
                            ? "Attribute:" + field.AttributeName
                            : field.Kind.ToString()
                    );
                }
            }

            Number("Symbols.TextHeight", _symbols.TextHeight);
            Number("Symbols.CentreLineExtension", _symbols.CentreLineExtension);
            Add("Symbols.WeldStandard", _symbols.WeldStandard.ToString());
            Flag("Symbols.LevelMarks", _symbols.LevelMarks);
            Add("Sheet.Size", _sheet.Size.ToString());
            Add("Sheet.Orientation", _sheet.Orientation.ToString());
            Number("Sheet.CustomWidth", _sheet.CustomWidth);
            Number("Sheet.CustomHeight", _sheet.CustomHeight);
            Number("Sheet.Margin", _sheet.Margin);
            Number("Sheet.ViewSpacing", _sheet.ViewSpacing);
            Flag("Sheet.PartList", _sheet.PartList);
            Flag("Sheet.AllowLargerSheet", _sheet.AllowLargerSheet);
            TitleBlock title = _sheet.TitleBlock;
            Add("Title.Project", title.Project);
            Add("Title.DrawingNumber", title.DrawingNumber);
            Add("Title.Title", title.Title);
            Add("Title.Revision", title.Revision);
            Add("Title.Company", title.Company);
            Add("Title.DrawnBy", title.DrawnBy);
            Add("Title.Date", title.Date);
            Add("ProjectionMethod", ProjectionMethod.ToString());
            Add("ExportMode", ExportMode.ToString());
            return values;
        }

        /// <summary>
        /// The settings read back from the values <see cref="ToValues"/> gave; a value missing or not read keeps its default.
        /// </summary>
        internal static DrawingSettings FromValues(IEnumerable<KeyValuePair<string, string>> values)
        {
            var read = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> value in values)
            {
                if (value.Value != null)
                {
                    read[value.Key] = value.Value;
                }
            }

            var settings = new DrawingSettings();
            ViewSettings views = settings.Views;

            if (read.TryGetValue("Views.Kinds", out string kinds))
            {
                ViewSettings.Refill(views.Kinds, Kinds(kinds));
            }

            if (read.TryGetValue("Views.Dimensioned", out string dimensioned))
            {
                ViewSettings.Refill(views.Dimensioned, Kinds(dimensioned));
            }

            ViewSettings.Refill(views.DimensionedViews, Ids(read, "Views.DimensionedViews"));
            ViewSettings.Refill(views.MarkedViews, Ids(read, "Views.MarkedViews"));
            Try(read, "Views.CropMargin", v => views.CropMargin = v);
            Try(read, "Views.SectionDepth", v => views.SectionDepth = v);
            Try(read, "Views.SmoothAngle", v => views.SmoothAngleRad = v);
            TryFlag(read, "Views.HiddenLines", v => views.HiddenLines = v);

            for (int i = 0; i < Count(read, "Views.Sections"); i++)
            {
                string key = "Views.Section." + i.ToString(CultureInfo.InvariantCulture);

                if (
                    !read.TryGetValue(key + ".Name", out string name)
                    || name.Length == 0
                    || !TryKind(read, key + ".LookingAs", out ViewKind kind)
                )
                {
                    continue;
                }

                if (read.TryGetValue(key + ".CutLine", out string cut))
                {
                    string[] parts = cut.Split(',');
                    var numbers = new double[parts.Length];
                    bool read4 = parts.Length == 4;

                    for (int k = 0; k < parts.Length && read4; k++)
                    {
                        read4 = double.TryParse(
                            parts[k],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out numbers[k]
                        );
                    }

                    if (read4 && TryNumber(read, key + ".Depth", out double depth))
                    {
                        try
                        {
                            views.Sections.Add(
                                new SectionViewRequest(
                                    name,
                                    kind,
                                    new GeoLine2(numbers[0], numbers[1], numbers[2], numbers[3]),
                                    depth
                                )
                            );
                        }
                        catch (ArgumentException)
                        {
                            // A section that does not read is left out.
                        }
                    }
                }
                else if (TryNumber(read, key + ".Share", out double share))
                {
                    views.Sections.Add(new SectionViewRequest(name, kind, share));
                }
            }

            for (int i = 0; i < Count(read, "Views.Details"); i++)
            {
                string key = "Views.Detail." + i.ToString(CultureInfo.InvariantCulture);

                if (
                    read.TryGetValue(key + ".Name", out string name)
                    && TryKind(read, key + ".Of", out ViewKind of)
                    && TryNumber(read, key + ".CentreX", out double x)
                    && TryNumber(read, key + ".CentreY", out double y)
                    && TryNumber(read, key + ".Width", out double width)
                    && TryNumber(read, key + ".Height", out double height)
                    && TryNumber(read, key + ".Enlargement", out double enlargement)
                )
                {
                    views.Details.Add(
                        new DetailViewRequest(
                            name,
                            of,
                            new Geometry.GeoRectangle2(new Geometry.GeoPoint2(x, y), width, height),
                            enlargement
                        )
                    );
                }
            }

            if (read.TryGetValue("Scales", out string scales))
            {
                var list = new List<double>();

                foreach (string part in scales.Split(','))
                {
                    if (
                        double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double scale)
                        && scale > 0.0
                        && !double.IsInfinity(scale)
                    )
                    {
                        list.Add(scale);
                    }
                }

                if (list.Count > 0)
                {
                    ViewSettings.Refill(settings.Scales, list);
                }
            }

            DimensionSettings dimensions = settings.Dimensions;
            TryFlag(read, "Dimensions.Overall", v => dimensions.Overall = v);
            TryFlag(read, "Dimensions.Chain", v => dimensions.Chain = v);
            TryFlag(read, "Dimensions.Cumulative", v => dimensions.Cumulative = v);
            TryFlag(read, "Dimensions.Ordinate", v => dimensions.Ordinate = v);
            TryFlag(read, "Dimensions.HolesByGroup", v => dimensions.HolesByGroup = v);
            TryFlag(read, "Dimensions.MergeEqual", v => dimensions.MergeEqual = v);
            Try(read, "Dimensions.TextHeight", v => dimensions.TextHeight = v);
            Try(read, "Dimensions.TierSpacing", v => dimensions.TierSpacing = v);
            Try(read, "Dimensions.Rounding", v => dimensions.Rounding = v);
            MarkSettings marks = settings.Marks;
            TryFlag(read, "Marks.MainParts", v => marks.MainParts = v);
            TryFlag(read, "Marks.Fittings", v => marks.Fittings = v);
            Try(read, "Marks.TextHeight", v => marks.TextHeight = v);
            Try(read, "Marks.Offset", v => marks.Offset = v);
            MarkContent content = marks.Content;

            if (TryFormat(read, "Marks.Content.Part", out MarkFormat partFormat))
            {
                content.Part = partFormat;
            }

            if (TryFormat(read, "Marks.Content.Assembly", out MarkFormat assembly))
            {
                content.Assembly = assembly;
            }

            if (TryFormat(read, "Marks.Content.Bolt", out MarkFormat bolt))
            {
                content.Bolt = bolt;
            }

            SymbolSettings symbols = settings.Symbols;
            Try(read, "Symbols.TextHeight", v => symbols.TextHeight = v);
            Try(read, "Symbols.CentreLineExtension", v => symbols.CentreLineExtension = v);

            if (
                read.TryGetValue("Symbols.WeldStandard", out string standard)
                && Enum.TryParse(standard, out WeldStandard weld)
                && Enum.IsDefined(typeof(WeldStandard), weld)
            )
            {
                symbols.WeldStandard = weld;
            }

            TryFlag(read, "Symbols.LevelMarks", v => symbols.LevelMarks = v);
            SheetSettings sheet = settings.Sheet;

            if (
                read.TryGetValue("Sheet.Size", out string size)
                && Enum.TryParse(size, out PaperSize paper)
                && Enum.IsDefined(typeof(PaperSize), paper)
            )
            {
                sheet.Size = paper;
            }

            if (
                read.TryGetValue("Sheet.Orientation", out string orientation)
                && Enum.TryParse(orientation, out SheetOrientation turned)
                && Enum.IsDefined(typeof(SheetOrientation), turned)
            )
            {
                sheet.Orientation = turned;
            }

            Try(read, "Sheet.CustomWidth", v => sheet.CustomWidth = v);
            Try(read, "Sheet.CustomHeight", v => sheet.CustomHeight = v);
            Try(read, "Sheet.Margin", v => sheet.Margin = v);
            Try(read, "Sheet.ViewSpacing", v => sheet.ViewSpacing = v);
            TryFlag(read, "Sheet.PartList", v => sheet.PartList = v);
            TryFlag(read, "Sheet.AllowLargerSheet", v => sheet.AllowLargerSheet = v);
            TitleBlock title = sheet.TitleBlock;
            title.Project = Text(read, "Title.Project");
            title.DrawingNumber = Text(read, "Title.DrawingNumber");
            title.Title = Text(read, "Title.Title");
            title.Revision = Text(read, "Title.Revision");
            title.Company = Text(read, "Title.Company");
            title.DrawnBy = Text(read, "Title.DrawnBy");
            title.Date = Text(read, "Title.Date");

            if (
                read.TryGetValue("ProjectionMethod", out string method)
                && Enum.TryParse(method, out ProjectionMethod projection)
                && Enum.IsDefined(typeof(ProjectionMethod), projection)
            )
            {
                settings.ProjectionMethod = projection;
            }

            if (
                read.TryGetValue("ExportMode", out string mode)
                && Enum.TryParse(mode, out DrawingExportMode export)
                && Enum.IsDefined(typeof(DrawingExportMode), export)
            )
            {
                settings.ExportMode = export;
            }

            return settings;
        }

        /// <summary>A number in the fewest digits that read back to the same bits.</summary>
        internal static string Write(double value)
        {
            string written = value.ToString("R", CultureInfo.InvariantCulture);
            return double.Parse(written, CultureInfo.InvariantCulture).Equals(value)
                ? written
                : value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static List<string> Names(IEnumerable<ViewKind> kinds)
        {
            var names = new List<string>();

            foreach (ViewKind kind in kinds)
            {
                names.Add(kind.ToString());
            }

            return names;
        }

        private static List<ViewKind> Kinds(string written)
        {
            var kinds = new List<ViewKind>();

            foreach (string part in written.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(part, out ViewKind kind) && Enum.IsDefined(typeof(ViewKind), kind))
                {
                    kinds.Add(kind);
                }
            }

            return kinds;
        }

        /// <summary>The ids of a list written with commas between them, the empty ones left out.</summary>
        private static List<string> Ids(SortedDictionary<string, string> read, string key) =>
            read.TryGetValue(key, out string written)
                ? new List<string>(written.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                : new List<string>();

        private static int Count(SortedDictionary<string, string> read, string key) =>
            read.TryGetValue(key, out string written)
            && int.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
            && count > 0
                ? count
                : 0;

        private static bool TryNumber(SortedDictionary<string, string> read, string key, out double value)
        {
            value = 0.0;
            return read.TryGetValue(key, out string written)
                && double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryKind(SortedDictionary<string, string> read, string key, out ViewKind kind)
        {
            kind = ViewKind.Front;
            return read.TryGetValue(key, out string written)
                && Enum.TryParse(written, out kind)
                && Enum.IsDefined(typeof(ViewKind), kind);
        }

        /// <summary>A mark's format read back: its separator and every field, or nothing where one of them does not read.</summary>
        private static bool TryFormat(SortedDictionary<string, string> read, string key, out MarkFormat format)
        {
            format = null;

            if (
                !read.TryGetValue(key + ".Separator", out string separator)
                || !read.TryGetValue(key + ".Fields", out string written)
                || !int.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                || count < 0
            )
            {
                return false;
            }

            var fields = new List<MarkField>();

            for (int i = 0; i < count; i++)
            {
                if (!read.TryGetValue(key + ".Field." + i.ToString(CultureInfo.InvariantCulture), out string name))
                {
                    return false;
                }

                if (name.StartsWith("Attribute:", StringComparison.Ordinal) && name.Length > "Attribute:".Length)
                {
                    fields.Add(MarkField.Attribute(name.Substring("Attribute:".Length)));
                    continue;
                }

                MarkField field = Array.Find(Fields, f =>
                    string.Equals(f.Kind.ToString(), name, StringComparison.Ordinal)
                );

                if (field == null)
                {
                    return false;
                }

                fields.Add(field);
            }

            format = new MarkFormat(fields, separator);
            return true;
        }

        private static void Try(SortedDictionary<string, string> read, string key, Action<double> set)
        {
            if (TryNumber(read, key, out double value))
            {
                try
                {
                    set(value);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // A value out of its range keeps the default.
                }
            }
        }

        private static void TryFlag(SortedDictionary<string, string> read, string key, Action<bool> set)
        {
            if (read.TryGetValue(key, out string written) && (written == "0" || written == "1"))
            {
                set(written == "1");
            }
        }

        private static string Text(SortedDictionary<string, string> read, string key) =>
            read.TryGetValue(key, out string written) ? written : string.Empty;
    }
}
