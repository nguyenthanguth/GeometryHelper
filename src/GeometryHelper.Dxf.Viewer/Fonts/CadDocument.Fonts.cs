using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What <see cref="CadDocument.FontMissing"/> says: a font a text style names that is not found, and what it is drawn in.</summary>
    public sealed class CadFontMissingEventArgs : EventArgs
    {
        /// <summary>Makes the arguments.</summary>
        /// <param name="message">The line to show: "Font romans.shx not found: using GeometryHelper Simplex."</param>
        public CadFontMissingEventArgs(string message)
        {
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the line to show on the command line: "Missing SHX files: romans.shx drawn in GeometryHelper Simplex." the first
        /// time, "Font No Such Face not found: using Arial." for a font met after it.
        /// </summary>
        public string Message { get; }
    }

    /// <summary>The fonts a document's text is drawn in.</summary>
    public sealed partial class CadDocument
    {
        private readonly object _fontLock = new object();
        private CadFonts _fonts = new CadFonts(null, null, null);
        private bool _missingListed;

        /// <summary>
        /// Raised the first time a font a text style names is not found and is replaced, as the spaces are drawn. The first time
        /// for the fonts set, it says in one line every SHX font and big font the drawing's text styles name that is not found,
        /// and what each is drawn in, as AutoCAD lists its missing SHX files on opening a drawing (<see cref="FindMissingFonts"/>);
        /// a font not in that line is said once on its own when it is met.
        /// </summary>
        public event EventHandler<CadFontMissingEventArgs> FontMissing;

        /// <summary>
        /// Gets or sets the fonts the text is drawn in: off until set, every style then drawn as the canvas draws it, an SHX
        /// font in the canvas's own font. Set to fonts other than those it has, every space is drawn again, as REGEN does, and
        /// the fonts missing are said again; fonts set as the same are kept, so that a font not found is not said again. The
        /// fonts last set on any document are those the words of a dimension in an SHX font are measured in, as its picture
        /// is laid out; a TrueType font is measured from the Fonts folder of Windows whatever the fonts.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public CadFonts Fonts
        {
            get => _fonts;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!value.IsSetAs(_fonts))
                {
                    lock (_fontLock)
                    {
                        _fonts = value;
                        _missingListed = false;
                    }

                    // A dimension's words in an SHX font are measured for its picture where these fonts find that font.
                    CadDimensionWords.UseFonts(value, FontFolder);
                    Regen();
                }
            }
        }

        /// <summary>The folder of the drawing's file, looked in first for its fonts; null for a drawing of no file.</summary>
        internal string FontFolder => _hasPath && !string.IsNullOrEmpty(FileName) ? Path.GetDirectoryName(FileName) : null;

        /// <summary>
        /// Gets the SHX fonts and big fonts the drawing's text styles name that <see cref="Fonts"/> does not find, each with what
        /// it is drawn in, in the order of their names: the list AutoCAD shows as Missing SHX files. A style of a TrueType font
        /// is left out, as AutoCAD leaves it out. Each can be given a font of its own by <see cref="CadFonts.WithSubstitution"/>,
        /// or left to the replacement font.
        /// </summary>
        /// <returns>The fonts not found; empty where every one is found or fonts are off.</returns>
        public IReadOnlyList<CadMissingFont> FindMissingFonts() => _fonts.Missing(Database, FontFolder, false);

        /// <summary>
        /// Says a font not found, as the display builder meets it: the first time for the fonts set, the list of every SHX font
        /// the styles name that is not found, then what of the line is not in it.
        /// </summary>
        internal void SayFontMissing(string message)
        {
            IReadOnlyList<CadMissingFont> listed = null;
            CadFonts fonts;

            lock (_fontLock)
            {
                fonts = _fonts;

                if (!_missingListed)
                {
                    _missingListed = true;
                    listed = fonts.Missing(Database, FontFolder, true);
                }
            }

            if (listed != null && listed.Count > 0)
            {
                FontMissing?.Invoke(this, new CadFontMissingEventArgs("Missing SHX files: " + string.Join("; ", listed.Select(f => f.ToString())) + "."));

                foreach (CadMissingFont font in listed)
                {
                    message = message.Replace(font.Line, string.Empty);
                }

                message = string.Join(" ", message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (message.Length > 0)
            {
                FontMissing?.Invoke(this, new CadFontMissingEventArgs(message));
            }
        }
    }
}
