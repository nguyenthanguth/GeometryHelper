using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What <see cref="CadDocument.FontMissing"/> says: a font a text style names that is not found, and what it is drawn in.</summary>
    public sealed class CadFontMissingEventArgs : EventArgs
    {
        /// <summary>Makes the arguments.</summary>
        /// <param name="message">The line to show: "Font romans.shx not found: using simplex.shx."</param>
        public CadFontMissingEventArgs(string message)
        {
            Message = message ?? string.Empty;
        }

        /// <summary>Gets the line to show on the command line: "Font romans.shx not found: using simplex.shx."</summary>
        public string Message { get; }
    }

    /// <summary>The fonts a document's text is drawn in.</summary>
    public sealed partial class CadDocument
    {
        private CadFonts _fonts = new CadFonts(null, null, null);

        /// <summary>Raised the first time a font a text style names is not found and is replaced, once a font for the fonts set, as the spaces are drawn.</summary>
        public event EventHandler<CadFontMissingEventArgs> FontMissing;

        /// <summary>
        /// Gets or sets the fonts the text is drawn in: off until set, every style then drawn as the canvas draws it, an SHX
        /// font in the canvas's own font. Set to fonts other than those it has, every space is drawn again, as REGEN does;
        /// fonts set as the same are kept, so that a font not found is not said again.
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
                    _fonts = value;
                    Regen();
                }
            }
        }

        /// <summary>The folder of the drawing's file, looked in first for its fonts; null for a drawing of no file.</summary>
        internal string FontFolder => _hasPath && !string.IsNullOrEmpty(FileName) ? Path.GetDirectoryName(FileName) : null;

        /// <summary>Says a font not found, as the display builder meets it.</summary>
        internal void SayFontMissing(string message) => FontMissing?.Invoke(this, new CadFontMissingEventArgs(message));
    }
}
