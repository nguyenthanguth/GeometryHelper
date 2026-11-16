using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The lists of choices the Plot Style Table Editor offers for a style's properties, by the words AutoCAD 2021 shows.</summary>
    public static class PlotStyleChoices
    {
        /// <summary>Gets the words of the Line end style list, in the order of <see cref="CadPlotLineEnd"/>.</summary>
        public static IReadOnlyList<string> LineEnds { get; } =
            new[] { "Butt", "Square", "Round", "Diamond", "Use object end style" };

        /// <summary>Gets the words of the Line join style list, in the order of <see cref="CadPlotLineJoin"/> (Use object join style last).</summary>
        public static IReadOnlyList<string> LineJoins { get; } =
            new[] { "Miter", "Bevel", "Round", "Diamond", "Use object join style" };

        /// <summary>Gets the words of the Fill style list, in the order of <see cref="CadPlotFill"/>.</summary>
        public static IReadOnlyList<string> Fills { get; } =
            new[]
            {
                "Solid",
                "Checkerboard",
                "Crosshatch",
                "Diamonds",
                "Horizontal Bars",
                "Slant Left",
                "Slant Right",
                "Square Dots",
                "Vertical Bars",
                "Use object fill style",
            };

        /// <summary>Gets the words of the Linetype list: <see cref="CadPlotStyle.LinetypeNames"/>.</summary>
        public static IReadOnlyList<string> Linetypes => CadPlotStyle.LinetypeNames;

        /// <summary>The words of "Use object color".</summary>
        public const string ObjectColor = "Use object color";

        /// <summary>The words of an automatic pen: nought.</summary>
        public const string Automatic = "Automatic";
    }

    /// <summary>
    /// One plot style of the Plot Style Table Editor, a row of its Table View and an entry of its Form View: every property
    /// as text or a flag to bind to, set through to the style. A value typed that is not one the property takes is dropped and
    /// the box shows the value it had.
    /// </summary>
    public sealed class PlotStyleRowViewModel : ObservableObject
    {
        private readonly PlotStyleEditorViewModel _owner;

        internal PlotStyleRowViewModel(PlotStyleEditorViewModel owner, CadPlotStyle style)
        {
            _owner = owner;
            Style = style;
        }

        /// <summary>Gets the style this row edits, a style of the editor's working copy of the table.</summary>
        public CadPlotStyle Style { get; }

        /// <summary>
        /// Gets or sets the name. A colour-dependent table's names are fixed (Color_1 to Color_255), and Normal of a named
        /// one: a rename of those, or to a name the table has or refuses (<see cref="CadPlotStyleSheet.CheckStyleName"/>), is
        /// dropped and said in the editor's <see cref="PlotStyleEditorViewModel.Message"/>.
        /// </summary>
        public string Name
        {
            get => Style.Name;
            set
            {
                if (!CanRename || string.Equals(value?.Trim(), Style.Name, StringComparison.Ordinal))
                {
                    Raise(nameof(Name));
                    return;
                }

                _owner.Rename(this, value);
                Raise(nameof(Name));
            }
        }

        /// <summary>Gets whether the name can be edited: for the styles of a named table but Normal.</summary>
        public bool CanRename =>
            _owner.IsNamed
            && !string.Equals(Style.Name, CadPlotStyleSheet.NormalName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets whether the name box is read-only: the opposite of <see cref="CanRename"/>.</summary>
        public bool IsNameLocked => !CanRename;

        /// <summary>Gets or sets the description, one line.</summary>
        public string Description
        {
            get => Style.Description;
            set =>
                Change(
                    () => Style.Description = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '),
                    nameof(Description)
                );
        }

        /// <summary>
        /// Gets or sets the colour as words: "Use object color", or <c>#RRGGBB</c>. A colour typed as <c>R,G,B</c> or without
        /// the hash is taken and shown as <c>#RRGGBB</c>.
        /// </summary>
        public string Color
        {
            get => Style.UsesObjectColor ? PlotStyleChoices.ObjectColor : GlobalSettings.Hex(Style.Rgb);
            set
            {
                string text = (value ?? string.Empty).Trim();

                if (
                    text.Length == 0
                    || string.Equals(text, PlotStyleChoices.ObjectColor, StringComparison.OrdinalIgnoreCase)
                )
                {
                    Change(() => Style.UseObjectColor(), nameof(Color));
                }
                else if (TryColor(text, out int rgb))
                {
                    Change(() => Style.Rgb = rgb, nameof(Color));
                }
                else
                {
                    Raise(nameof(Color));
                }
            }
        }

        /// <summary>Gets whether the colour is one of its own, which the swatch shows.</summary>
        public bool HasColor => !Style.UsesObjectColor;

        /// <summary>Gets the colour of the swatch, 0xRRGGBB; black for the object's colour, which has none.</summary>
        public int SwatchRgb => Style.Rgb;

        /// <summary>Gets or sets whether the colours are dithered (Enable dithering).</summary>
        public bool Dither
        {
            get => Style.Dither;
            set => Change(() => Style.Dither = value, nameof(Dither));
        }

        /// <summary>Gets or sets whether the colour is plotted as a grey (Convert to grayscale).</summary>
        public bool Grayscale
        {
            get => Style.Grayscale;
            set => Change(() => Style.Grayscale = value, nameof(Grayscale));
        }

        /// <summary>Gets or sets the physical pen, "Automatic" or a number 1 to 32.</summary>
        public string PhysicalPen
        {
            get => Pen(Style.PhysicalPen);
            set => Change(() => Style.PhysicalPen = Number(value, 0, 32, Style.PhysicalPen, true), nameof(PhysicalPen));
        }

        /// <summary>Gets or sets the virtual pen, "Automatic" or a number 1 to 255.</summary>
        public string VirtualPen
        {
            get => Pen(Style.VirtualPen);
            set => Change(() => Style.VirtualPen = Number(value, 0, 255, Style.VirtualPen, true), nameof(VirtualPen));
        }

        /// <summary>Gets or sets the screening, 0 to 100 per cent as text.</summary>
        public string Screening
        {
            get => Style.EffectiveScreening.ToString(CultureInfo.InvariantCulture);
            set =>
                Change(
                    () => Style.Screening = Number(value, 0, 100, Style.EffectiveScreening, false),
                    nameof(Screening)
                );
        }

        /// <summary>Gets or sets the linetype, one of <see cref="PlotStyleChoices.Linetypes"/>.</summary>
        public string Linetype
        {
            get => CadPlotStyle.LinetypeNames[Style.Linetype];
            set =>
                Change(
                    () => Style.Linetype = Index(CadPlotStyle.LinetypeNames, value, Style.Linetype),
                    nameof(Linetype)
                );
        }

        /// <summary>Gets or sets whether the pattern is adjusted to finish whole (Adaptive adjustment).</summary>
        public bool Adaptive
        {
            get => Style.Adaptive;
            set => Change(() => Style.Adaptive = value, nameof(Adaptive));
        }

        /// <summary>Gets or sets the lineweight, one of the table's <see cref="PlotStyleEditorViewModel.LineWeightOptions"/>.</summary>
        public string LineWeight
        {
            get =>
                Style.LineWeightIndex < _owner.LineWeightOptions.Count
                    ? _owner.LineWeightOptions[Style.LineWeightIndex]
                    : _owner.LineWeightOptions[0];
            set =>
                Change(
                    () => Style.LineWeightIndex = Index(_owner.LineWeightOptions, value, Style.LineWeightIndex),
                    nameof(LineWeight)
                );
        }

        /// <summary>Gets or sets the line end style, one of <see cref="PlotStyleChoices.LineEnds"/>.</summary>
        public string LineEnd
        {
            get => PlotStyleChoices.LineEnds[Math.Max(0, Math.Min(4, (int)Style.LineEnd))];
            set =>
                Change(
                    () => Style.LineEnd = (CadPlotLineEnd)Index(PlotStyleChoices.LineEnds, value, (int)Style.LineEnd),
                    nameof(LineEnd)
                );
        }

        /// <summary>Gets or sets the line join style, one of <see cref="PlotStyleChoices.LineJoins"/>.</summary>
        public string LineJoin
        {
            get =>
                PlotStyleChoices.LineJoins[
                    (int)Style.LineJoin >= 0 && (int)Style.LineJoin <= 3 ? (int)Style.LineJoin : 4
                ];
            set =>
                Change(
                    () =>
                    {
                        int chosen = Index(PlotStyleChoices.LineJoins, value, -1);

                        if (chosen >= 0)
                        {
                            Style.LineJoin = chosen == 4 ? CadPlotLineJoin.Object : (CadPlotLineJoin)chosen;
                        }
                    },
                    nameof(LineJoin)
                );
        }

        /// <summary>Gets or sets the fill style, one of <see cref="PlotStyleChoices.Fills"/>.</summary>
        public string Fill
        {
            get => PlotStyleChoices.Fills[Math.Max(0, Math.Min(9, (int)Style.Fill - 64))];
            set =>
                Change(
                    () => Style.Fill = (CadPlotFill)(64 + Index(PlotStyleChoices.Fills, value, (int)Style.Fill - 64)),
                    nameof(Fill)
                );
        }

        /// <inheritdoc/>
        public override string ToString() => Style.Name;

        /// <summary>Raises every property: a style changed through another route.</summary>
        internal void RaiseAll()
        {
            foreach (
                string name in new[]
                {
                    nameof(Name),
                    nameof(Description),
                    nameof(Color),
                    nameof(HasColor),
                    nameof(SwatchRgb),
                    nameof(Dither),
                    nameof(Grayscale),
                    nameof(PhysicalPen),
                    nameof(VirtualPen),
                    nameof(Screening),
                    nameof(Linetype),
                    nameof(Adaptive),
                    nameof(LineWeight),
                    nameof(LineEnd),
                    nameof(LineJoin),
                    nameof(Fill),
                }
            )
            {
                Raise(name);
            }
        }

        private void Change(Action set, string name)
        {
            set();
            _owner.MarkModified();
            Raise(name);

            if (name == nameof(Color))
            {
                Raise(nameof(HasColor));
                Raise(nameof(SwatchRgb));
            }
        }

        private static string Pen(int number) =>
            number == 0 ? PlotStyleChoices.Automatic : number.ToString(CultureInfo.InvariantCulture);

        private static int Number(string text, int min, int max, int kept, bool zeroIsAutomatic)
        {
            string trimmed = (text ?? string.Empty).Trim();

            if (
                zeroIsAutomatic
                && (
                    trimmed.Length == 0
                    || string.Equals(trimmed, PlotStyleChoices.Automatic, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return 0;
            }

            return
                int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                && value >= min
                && value <= max
                ? value
                : kept;
        }

        private static int Index(IReadOnlyList<string> choices, string value, int kept)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return kept;
        }

        private static bool TryColor(string text, out int rgb)
        {
            if (GlobalSettings.TryParseHex(text, out rgb))
            {
                return true;
            }

            string[] parts = text.Split(',');

            if (
                parts.Length == 3
                && int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int r)
                && int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int g)
                && int.TryParse(parts[2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int b)
                && r <= 255
                && g <= 255
                && b <= 255
            )
            {
                rgb = (r << 16) | (g << 8) | b;
                return true;
            }

            rgb = 0;
            return false;
        }
    }

    /// <summary>
    /// AutoCAD 2021's Plot Style Table Editor, with no window: the General tab (the file, its description, the global scale
    /// factor), the Table View (every style a row, every property a column) and the Form View (a list of the styles and the
    /// properties of the one chosen), editing a working copy of a table that Save &amp; Close writes back to its file and Save
    /// As… writes to another. Add Style and Delete Style are the named table's alone.
    /// </summary>
    public sealed class PlotStyleEditorViewModel : ObservableObject
    {
        /// <summary>The name of the General tab.</summary>
        public const string GeneralTab = "General";

        /// <summary>The name of the Table View tab.</summary>
        public const string TableViewTab = "Table View";

        /// <summary>The name of the Form View tab.</summary>
        public const string FormViewTab = "Form View";

        private readonly string _folder;
        private string _fileName;
        private string _tab = GeneralTab;
        private string _scaleText;
        private string _message = string.Empty;
        private bool _modified;
        private PlotStyleRowViewModel _selected;

        /// <summary>Makes the editor over a copy of a table.</summary>
        /// <param name="sheet">The table to edit; it is copied, the editor never changes it.</param>
        /// <param name="folder">The folder Save &amp; Close writes the file to; null for none, when Save says so.</param>
        /// <exception cref="ArgumentNullException">Thrown when the table is null.</exception>
        public PlotStyleEditorViewModel(CadPlotStyleSheet sheet, string folder)
        {
            Sheet = (sheet ?? throw new ArgumentNullException(nameof(sheet))).Clone();
            _folder = folder;
            _fileName = Sheet.FileName;
            _scaleText = Sheet.ScaleFactor.ToString("0.###", CultureInfo.InvariantCulture);
            LineWeightOptions = Options(Sheet);
            Rows = new ObservableCollection<PlotStyleRowViewModel>(
                Sheet.Styles.Select(s => new PlotStyleRowViewModel(this, s))
            );
            _selected = Rows[0];
        }

        /// <summary>Gets the working copy of the table, which the editor changes; <see cref="Save"/> writes it.</summary>
        public CadPlotStyleSheet Sheet { get; }

        /// <summary>Gets or sets how Save As… asks where to write the table: the path chosen, null where it was cancelled. Null for none, when Save As… does nothing.</summary>
        public Func<string, string> AskSaveAs { get; set; }

        /// <summary>Gets the title: "Plot Style Table Editor - monochrome.ctb".</summary>
        public string Title =>
            "Plot Style Table Editor - " + (string.IsNullOrEmpty(_fileName) ? "(not saved)" : _fileName);

        /// <summary>Gets the name of the file the table is kept in.</summary>
        public string FileName => _fileName;

        /// <summary>Gets the folder the file is in; empty for none.</summary>
        public string Location => _folder ?? string.Empty;

        /// <summary>Gets whether the table is a named one (.stb): its styles can be added, deleted and renamed.</summary>
        public bool IsNamed => Sheet.IsNamed;

        /// <summary>Gets what the table is: "Colour-dependent plot style table (.ctb)" or "Named plot style table (.stb)".</summary>
        public string Kind => IsNamed ? "Named plot style table (.stb)" : "Colour-dependent plot style table (.ctb)";

        /// <summary>Gets the number of styles, "255 plot styles".</summary>
        public string StyleCountText =>
            Sheet.Styles.Count.ToString(CultureInfo.InvariantCulture)
            + (Sheet.Styles.Count == 1 ? " plot style" : " plot styles");

        /// <summary>Gets or sets the tab shown: <see cref="GeneralTab"/>, <see cref="TableViewTab"/> or <see cref="FormViewTab"/>; another name is dropped.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (value != GeneralTab && value != TableViewTab && value != FormViewTab)
                {
                    return;
                }

                if (Set(ref _tab, value))
                {
                    Raise(nameof(IsGeneral));
                    Raise(nameof(IsTableView));
                    Raise(nameof(IsFormView));
                }
            }
        }

        /// <summary>Gets whether the General tab is shown.</summary>
        public bool IsGeneral => _tab == GeneralTab;

        /// <summary>Gets whether the Table View tab is shown.</summary>
        public bool IsTableView => _tab == TableViewTab;

        /// <summary>Gets whether the Form View tab is shown.</summary>
        public bool IsFormView => _tab == FormViewTab;

        /// <summary>Gets or sets the description of the table, one line.</summary>
        public string Description
        {
            get => Sheet.Description;
            set
            {
                Sheet.Description = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
                MarkModified();
                Raise(nameof(Description));
            }
        }

        /// <summary>Gets or sets whether the global scale factor is applied to the linetypes that are not ISO's.</summary>
        public bool ApplyGlobalScaleFactor
        {
            get => Sheet.ApplyGlobalScaleFactor;
            set
            {
                Sheet.ApplyGlobalScaleFactor = value;
                MarkModified();
                Raise(nameof(ApplyGlobalScaleFactor));
            }
        }

        /// <summary>Gets or sets the global scale factor as text, above nought; a value that is not is dropped and the box shows the factor it had.</summary>
        public string ScaleFactor
        {
            get => _scaleText;
            set
            {
                if (
                    double.TryParse(
                        (value ?? string.Empty).Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double factor
                    )
                    && factor > 0.0
                    && factor <= 1000000.0
                )
                {
                    Sheet.ScaleFactor = factor;
                    _scaleText = factor.ToString("0.###", CultureInfo.InvariantCulture);
                    MarkModified();
                }

                Raise(nameof(ScaleFactor));
            }
        }

        /// <summary>Gets the words of the Lineweight list: "Use object lineweight", then the table's entries as "0.250 mm".</summary>
        public IReadOnlyList<string> LineWeightOptions { get; }

        /// <summary>Gets the styles, a row each, in the order of the table.</summary>
        public ObservableCollection<PlotStyleRowViewModel> Rows { get; }

        /// <summary>Gets or sets the style the Form View shows.</summary>
        public PlotStyleRowViewModel SelectedRow
        {
            get => _selected;
            set
            {
                if (value != null && Set(ref _selected, value))
                {
                    Raise(nameof(CanDeleteStyle));
                }
            }
        }

        /// <summary>Gets whether Add Style is offered: for a named table.</summary>
        public bool CanAddStyle => IsNamed;

        /// <summary>Gets whether Delete Style is offered: for a named table, with a style but Normal chosen.</summary>
        public bool CanDeleteStyle =>
            IsNamed
            && _selected != null
            && !string.Equals(_selected.Style.Name, CadPlotStyleSheet.NormalName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets whether something was changed since the table was opened or last saved.</summary>
        public bool IsModified => _modified;

        /// <summary>Gets what the editor says of the last thing it refused or failed to do; empty where nothing was.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.PlotDialog;

        /// <summary>
        /// Adds a style to a named table, as Add Style does, named "Style 1" or the first number up that is free, and chooses
        /// it in the Form View for its name to be typed.
        /// </summary>
        /// <returns>Whether a style was added: false for a colour-dependent table.</returns>
        public bool AddStyle()
        {
            if (!IsNamed)
            {
                return false;
            }

            int number = 1;

            while (Sheet.StyleNamed("Style " + number.ToString(CultureInfo.InvariantCulture)) != null)
            {
                number++;
            }

            CadPlotStyle style = Sheet.AddStyle("Style " + number.ToString(CultureInfo.InvariantCulture));
            var row = new PlotStyleRowViewModel(this, style);
            Rows.Add(row);
            SelectedRow = row;
            MarkModified();
            Message = string.Empty;
            Raise(nameof(StyleCountText));
            return true;
        }

        /// <summary>Takes the chosen style out of a named table, as Delete Style does; the one before it is then chosen.</summary>
        /// <returns>Whether a style was deleted: false where it is a colour-dependent table's or Normal.</returns>
        public bool DeleteStyle()
        {
            if (!CanDeleteStyle)
            {
                return false;
            }

            PlotStyleRowViewModel row = _selected;
            int at = Rows.IndexOf(row);
            Sheet.RemoveStyle(row.Style.Name);
            Rows.Remove(row);
            SelectedRow = Rows[Math.Max(0, at - 1)];
            MarkModified();
            Message = string.Empty;
            Raise(nameof(StyleCountText));
            return true;
        }

        /// <summary>
        /// Writes the table to its file in the folder it was opened from, as Save &amp; Close does; the file is not touched
        /// where the table was not changed. A failure is said in <see cref="Message"/>.
        /// </summary>
        /// <returns>True when the table is saved or nothing was changed; false when it could not be written.</returns>
        public bool Save()
        {
            if (!_modified)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_folder) || string.IsNullOrEmpty(_fileName))
            {
                Message = "This table has no folder to be saved in: use Save As.";
                return false;
            }

            return Write(Path.Combine(_folder, _fileName));
        }

        /// <summary>
        /// Writes the table to another file, as Save As… does: <see cref="AskSaveAs"/> gives the path, the editor then keeps that
        /// file as its own. The extension must be the table's (.ctb, .stb), added where it is missing.
        /// </summary>
        /// <returns>True when the table is saved; false when the choice was cancelled or the file could not be written (said in <see cref="Message"/>).</returns>
        public bool SaveAs()
        {
            string suggested = string.IsNullOrEmpty(_fileName) ? "new" + Extension : _fileName;
            string path = AskSaveAs?.Invoke(Path.Combine(_folder ?? string.Empty, suggested));
            return !string.IsNullOrWhiteSpace(path) && SaveAs(path);
        }

        /// <summary>Writes the table to a path, as <see cref="SaveAs()"/> does once the path is chosen.</summary>
        /// <param name="path">The file; the table's extension is added where it has none.</param>
        /// <returns>True when the table is saved.</returns>
        public bool SaveAs(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            string problem = CadPlotStyleFolder.CheckFileName(name);

            if (problem != null)
            {
                Message = problem;
                return false;
            }

            if (!string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), name + Extension);
            }

            if (!Write(path))
            {
                return false;
            }

            _fileName = Path.GetFileName(path);
            Raise(nameof(FileName));
            Raise(nameof(Title));
            return true;
        }

        /// <summary>Takes a name typed for a style of a named table, or says why not.</summary>
        internal void Rename(PlotStyleRowViewModel row, string newName)
        {
            try
            {
                Sheet.RenameStyle(row.Style.Name, newName);
                MarkModified();
                Message = string.Empty;
            }
            catch (ArgumentException exception)
            {
                Message = exception.Message.Split('\n')[0].Trim();
            }
        }

        /// <summary>Notes that the table was changed.</summary>
        internal void MarkModified()
        {
            if (!_modified)
            {
                _modified = true;
                Raise(nameof(IsModified));
            }
        }

        private string Extension =>
            IsNamed ? CadPlotStyleSheet.NamedExtension : CadPlotStyleSheet.ColorDependentExtension;

        private bool Write(string path)
        {
            try
            {
                Sheet.Save(path);
                _modified = false;
                Message = string.Empty;
                Raise(nameof(IsModified));
                return true;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                Message = "The table could not be saved: " + exception.Message;
                return false;
            }
        }

        private static IReadOnlyList<string> Options(CadPlotStyleSheet sheet)
        {
            var options = new List<string> { "Use object lineweight" };

            for (int i = 1; i < sheet.LineWeights.Count; i++)
            {
                options.Add(sheet.LineWeights[i].ToString("0.000", CultureInfo.InvariantCulture) + " mm");
            }

            return options;
        }
    }
}
