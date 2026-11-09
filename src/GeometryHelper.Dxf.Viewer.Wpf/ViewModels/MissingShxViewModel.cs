using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What an item of the replacement list of the Missing SHX files dialog is.</summary>
    public enum MissingShxOptionKind
    {
        /// <summary>The list's heading, "Found in the font folders"; not chosen.</summary>
        Heading,

        /// <summary>A font: the presenter's own, an SHX file of the font folders, or a file chosen by Browse….</summary>
        Font,

        /// <summary>The line above Browse…; not chosen.</summary>
        Separator,

        /// <summary>Browse…: asks for a file.</summary>
        Browse,
    }

    /// <summary>An item of the replacement list of a row of the Missing SHX files dialog: a font with its tag, or the list's heading, line or Browse….</summary>
    public sealed class MissingShxOption
    {
        internal MissingShxOption(MissingShxOptionKind kind, string name, string tag, string font)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Tag = tag ?? string.Empty;
            Font = font;
        }

        /// <summary>Gets what the item is.</summary>
        public MissingShxOptionKind Kind { get; }

        /// <summary>Gets the words shown: a font's file name (romans.shx), ghsimplex, the heading or Browse….</summary>
        public string Name { get; }

        /// <summary>Gets the tag shown after a font: "big font", "built-in", or empty.</summary>
        public string Tag { get; }

        /// <summary>Gets the font a missing one is drawn in once chosen: a file name, a full path, or ghsimplex; null for the heading, the line and Browse….</summary>
        public string Font { get; }

        /// <summary>Gets whether the item can be chosen: a font or Browse….</summary>
        public bool IsChoosable => Kind == MissingShxOptionKind.Font || Kind == MissingShxOptionKind.Browse;

        /// <summary>Gets whether the item is the line above Browse….</summary>
        public bool IsSeparator => Kind == MissingShxOptionKind.Separator;

        /// <summary>Gets whether the item is the heading of the list.</summary>
        public bool IsHeading => Kind == MissingShxOptionKind.Heading;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// A row of the Missing SHX files dialog: the file a text style names that is not found (hztxt.shx, marked BIG FONT for a
    /// big font), the styles that use it, what it is drawn in now, and the font chosen to draw it in instead.
    /// </summary>
    public sealed class MissingShxRowViewModel : ObservableObject
    {
        private readonly MissingShxViewModel _owner;
        private MissingShxOption _replacement;

        internal MissingShxRowViewModel(MissingShxViewModel owner, CadMissingFont font, string styles, IEnumerable<MissingShxOption> options)
        {
            _owner = owner;
            File = font.Name;
            IsBigFont = font.IsBigFont;
            Styles = styles;
            DrawnInNow = font.DrawnIn;
            Options = new ObservableCollection<MissingShxOption>(options);
            Original = Pick(font);
            _replacement = Original;
        }

        /// <summary>Gets the file the styles name: romans.shx, or txt for txt.shx.</summary>
        public string File { get; }

        /// <summary>Gets whether it is a style's big font, tagged BIG FONT.</summary>
        public bool IsBigFont { get; }

        /// <summary>Gets the names of the text styles that use it, in the order of the drawing's style table: "Dims, TITLE".</summary>
        public string Styles { get; }

        /// <summary>Gets what it is drawn in now, as the replacement names it (GeometryHelper Simplex, simplex.shx); empty for none.</summary>
        public string DrawnInNow { get; }

        /// <summary>
        /// Gets the Drawn in column while the missing files are ignored: the font as a file is named (simplex.shx, ghsimplex for
        /// GeometryHelper Simplex), or the drawing font, or question marks for a big font.
        /// </summary>
        public string DrawnInText
            => DrawnInNow.Length == 0 ? (IsBigFont ? "question marks" : "the drawing font")
               : string.Equals(DrawnInNow, CadFonts.BuiltInTitle, StringComparison.OrdinalIgnoreCase) ? CadFonts.BuiltInName : DrawnInNow;

        /// <summary>Gets whether the FONTALT replacement draws it now, so that "(FONTALT)" is shown after it.</summary>
        public bool IsDrawnByFontAlt => DrawnInNow.Length > 0;

        /// <summary>Gets the list of the replacement box: the heading, the presenter's own font, the SHX files of the font folders, a line and Browse….</summary>
        public ObservableCollection<MissingShxOption> Options { get; }

        /// <summary>
        /// Gets or sets the font chosen to draw it in; at first the font that draws it now. Browse… chosen asks for a file
        /// through <see cref="MissingShxViewModel.BrowseFont"/>, adds it to the list and chooses it, or keeps the font chosen
        /// before where none is given. The heading and the line are not taken.
        /// </summary>
        public MissingShxOption Replacement
        {
            get => _replacement;
            set
            {
                if (value == null || value == _replacement || value.Kind == MissingShxOptionKind.Heading || value.Kind == MissingShxOptionKind.Separator)
                {
                    Raise(nameof(Replacement));
                    return;
                }

                if (value.Kind == MissingShxOptionKind.Browse)
                {
                    string path = _owner.BrowseFont?.Invoke();

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        Raise(nameof(Replacement));
                        return;
                    }

                    value = Options.FirstOrDefault(o => o.Kind == MissingShxOptionKind.Font && string.Equals(o.Font, path, StringComparison.OrdinalIgnoreCase))
                        ?? AddFile(path);
                }

                Set(ref _replacement, value);
            }
        }

        /// <summary>The font that draws it now, as an item of the list; null where none does.</summary>
        internal MissingShxOption Original { get; }

        private MissingShxOption AddFile(string path)
        {
            string name;

            try
            {
                name = Path.GetFileName(path);
            }
            catch (ArgumentException)
            {
                name = path;
            }

            var option = new MissingShxOption(MissingShxOptionKind.Font, name, MissingShxViewModel.IsBigFontFile(path) ? "big font" : string.Empty, path);
            int separator = Options.IndexOf(Options.FirstOrDefault(o => o.Kind == MissingShxOptionKind.Separator));
            Options.Insert(separator < 0 ? Options.Count : separator, option);
            return option;
        }

        /// <summary>The item of the font that draws the missing one now: the presenter's own for GeometryHelper Simplex, else the item of its name, made where the list has none.</summary>
        private MissingShxOption Pick(CadMissingFont font)
        {
            string drawn = font.DrawnIn ?? string.Empty;

            if (drawn.Length == 0)
            {
                return null;
            }

            string wanted = string.Equals(drawn, CadFonts.BuiltInTitle, StringComparison.OrdinalIgnoreCase) ? CadFonts.BuiltInName : drawn;
            MissingShxOption found = Options.FirstOrDefault(o => o.Kind == MissingShxOptionKind.Font
                && (string.Equals(o.Font, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(o.Name, wanted + ".shx", StringComparison.OrdinalIgnoreCase)));

            if (found != null)
            {
                return found;
            }

            var option = new MissingShxOption(MissingShxOptionKind.Font, wanted, string.Empty, wanted);
            int separator = Options.IndexOf(Options.FirstOrDefault(o => o.Kind == MissingShxOptionKind.Separator));
            Options.Insert(separator < 0 ? Options.Count : separator, option);
            return option;
        }
    }

    /// <summary>
    /// AutoCAD's Missing SHX files dialog, shown once as a drawing opens whose text styles name SHX fonts or big fonts that
    /// are not found: a row per file with the styles that use it and what it is drawn in, and the choice of a replacement for
    /// each (a font of the font folders, the presenter's own ghsimplex first, or a file chosen by Browse…) or of keeping the
    /// FONTALT replacement. A drawing whose styles Dims and TITLE name romand.shx, not found, is drawn in ghsimplex until
    /// romans.shx is chosen for it; OK then draws every text of those styles in romans.shx (<see cref="Apply"/>).
    /// </summary>
    public sealed class MissingShxViewModel : ObservableObject
    {
        private bool _replace;
        private bool _doNotShowAgain;

        /// <summary>Makes the dialog over the fonts of a document that are not found.</summary>
        /// <param name="document">The document.</param>
        /// <param name="missing">The fonts not found, as <see cref="CadDocument.FindMissingFonts"/> gives them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the fonts are null.</exception>
        public MissingShxViewModel(CadDocument document, IReadOnlyList<CadMissingFont> missing)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));

            if (missing == null)
            {
                throw new ArgumentNullException(nameof(missing));
            }

            Title = "Missing SHX files · " + document.Title;
            List<MissingShxOption> options = Options(document);
            Rows = missing.Where(f => f != null).Select(f => new MissingShxRowViewModel(this, f, StylesOf(document.Database, f), options)).ToList().AsReadOnly();
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the title: "Missing SHX files · plan-level2.dxf".</summary>
        public string Title { get; }

        /// <summary>Gets the rows, a file each, in the order of their names.</summary>
        public IReadOnlyList<MissingShxRowViewModel> Rows { get; }

        /// <summary>
        /// Gets the words beside the warning: "This drawing uses 3 SHX font files that are not in the font search folders
        /// (Global settings › Fonts). Until you choose otherwise, text in those styles is drawn in the replacement shown."
        /// </summary>
        public string Explanation
            => "This drawing uses " + (Rows.Count == 1 ? "1 SHX font file that is" : Rows.Count.ToString(CultureInfo.InvariantCulture) + " SHX font files that are")
               + " not in the font search folders (Global settings › Fonts). Until you choose otherwise, text in those styles is drawn in the replacement shown.";

        /// <summary>
        /// Gets the words of the second choice: "Text stays in the FONTALT replacement, GeometryHelper Simplex, and gbcbig.shx
        /// for big fonts.", from the replacement of the document's fonts.
        /// </summary>
        public string IgnoreDetail
        {
            get
            {
                string replacement = Document.Fonts.Replacement;
                int comma = replacement.IndexOf(',');
                string font = (comma < 0 ? replacement : replacement.Substring(0, comma)).Trim();
                string big = comma < 0 ? string.Empty : replacement.Substring(comma + 1).Trim();
                font = font.Length == 0 ? "the drawing font"
                    : string.Equals(font, CadFonts.BuiltInName, StringComparison.OrdinalIgnoreCase) || string.Equals(font, CadFonts.BuiltInName + ".shx", StringComparison.OrdinalIgnoreCase) ? CadFonts.BuiltInTitle
                    : font;
                return "Text stays in the FONTALT replacement, " + font + (big.Length > 0 ? ", and " + big + " for big fonts." : "; a big font's letters are drawn as question marks.");
            }
        }

        /// <summary>Gets or sets whether a replacement is chosen for each file (Specify a replacement for each SHX file), the box of each row then shown.</summary>
        public bool Replace
        {
            get => _replace;
            set
            {
                if (Set(ref _replace, value))
                {
                    Raise(nameof(Ignore));
                }
            }
        }

        /// <summary>Gets or sets whether the missing files are ignored and text stays in the FONTALT replacement: the choice at first.</summary>
        public bool Ignore
        {
            get => !_replace;
            set => Replace = !value;
        }

        /// <summary>Gets or sets whether the dialog is not shown again for the drawings opened after (Global settings' <see cref="GlobalSettings.ShowMissingShxDialog"/>).</summary>
        public bool DoNotShowAgain
        {
            get => _doNotShowAgain;
            set => Set(ref _doNotShowAgain, value);
        }

        /// <summary>Gets or sets how the window asks for a font file for Browse…: its path, or null where none was chosen.</summary>
        public Func<string> BrowseFont { get; set; }

        /// <summary>
        /// Draws the missing files in the fonts chosen, as OK does with Specify a replacement chosen: each row whose font was
        /// changed given it through <see cref="CadFonts.WithSubstitution"/> by its name without .shx (romand for romand.shx,
        /// so that styles naming romand and romand.shx are both drawn in it), every space then drawn again. Nothing is
        /// changed while the missing files are ignored.
        /// </summary>
        /// <returns>The replacements made, each name to the font it is drawn in, in the order of the rows; empty for none.</returns>
        public IReadOnlyList<KeyValuePair<string, string>> Apply()
        {
            var made = new List<KeyValuePair<string, string>>();

            if (!_replace)
            {
                return made;
            }

            CadFonts fonts = Document.Fonts;

            foreach (MissingShxRowViewModel row in Rows)
            {
                if (row.Replacement != null && row.Replacement != row.Original && !string.IsNullOrWhiteSpace(row.Replacement.Font))
                {
                    // By the name without .shx, which a style's romand and another's romand.shx both reach.
                    string name = row.File.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? row.File.Substring(0, row.File.Length - 4) : row.File;
                    fonts = fonts.WithSubstitution(name, row.Replacement.Font);
                    made.Add(new KeyValuePair<string, string>(name, row.Replacement.Font));
                }
            }

            if (made.Count > 0)
            {
                Document.Fonts = fonts;
            }

            return made;
        }

        /// <summary>Whether a file is an SHX big font: its first line "AutoCAD-86 bigfont".</summary>
        internal static bool IsBigFontFile(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var head = new byte[18];
                    int read = stream.Read(head, 0, head.Length);
                    return Encoding.ASCII.GetString(head, 0, read).StartsWith("AutoCAD-86 bigfont", StringComparison.Ordinal);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// The list of a replacement box: the heading, ghsimplex, the SHX files of the drawing's folder and the font folders
        /// in the order of their names, the first folder's where two have one name, a line and Browse….
        /// </summary>
        private static List<MissingShxOption> Options(CadDocument document)
        {
            var options = new List<MissingShxOption>
            {
                new MissingShxOption(MissingShxOptionKind.Heading, "Found in the font folders", null, null),
                new MissingShxOption(MissingShxOptionKind.Font, CadFonts.BuiltInName, "built-in", CadFonts.BuiltInName),
            };
            var found = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string own = string.IsNullOrEmpty(document.FileName) || !Path.IsPathRooted(document.FileName) ? null : Path.GetDirectoryName(document.FileName);

            foreach (string folder in new[] { own }.Concat(document.Fonts.Folders).Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        continue;
                    }

                    foreach (string path in Directory.GetFiles(folder, "*.shx").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ThenBy(p => p, StringComparer.Ordinal))
                    {
                        string name = Path.GetFileName(path);

                        if (!found.ContainsKey(name) && !string.Equals(Path.GetFileNameWithoutExtension(name), CadFonts.BuiltInName, StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(name, path);
                        }
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
                {
                    GeometryHelperLog.Debug("MissingShxViewModel: the font folder " + folder + " was not listed (" + e.Message + ").");
                }
            }

            options.AddRange(found.Select(f => new MissingShxOption(MissingShxOptionKind.Font, f.Key, IsBigFontFile(f.Value) ? "big font" : string.Empty, f.Key)));
            options.Add(new MissingShxOption(MissingShxOptionKind.Separator, string.Empty, null, null));
            options.Add(new MissingShxOption(MissingShxOptionKind.Browse, "Browse…", null, null));
            return options;
        }

        /// <summary>The text styles that name a missing file as their font, or as their big font for a big font, by its name with or without .shx.</summary>
        private static string StylesOf(DxfDatabase database, CadMissingFont font)
        {
            string Bare(string name)
            {
                string trimmed = (name ?? string.Empty).Trim();
                return trimmed.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? trimmed.Substring(0, trimmed.Length - 4) : trimmed;
            }

            string wanted = Bare(font.Name);
            var styles = new List<string>();

            foreach (DxfTextStyleTableRecord style in database.TextStyleTable)
            {
                if (style.IsShapeFile)
                {
                    continue;
                }

                string named = font.IsBigFont ? style.BigFontFileName : string.IsNullOrEmpty(style.Font.TypeFace) ? style.FileName : null;

                if (named != null && string.Equals(Bare(named), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    styles.Add(style.Name);
                }
            }

            return string.Join(", ", styles);
        }
    }
}
