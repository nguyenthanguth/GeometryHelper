using System;

namespace GeometryHelper.Dxf
{
    /// <summary>The fonts the drawing's words are measured in, as AutoCAD finds them on its support path.</summary>
    public sealed partial class DxfDatabase
    {
        private string _fontSearchPath = string.Empty;
        private DxfFonts _fonts;

        /// <summary>
        /// Gets or sets the folders the SHX and TrueType fonts of the drawing's text styles are looked for in, separated by
        /// semicolons and searched in their order, as AutoCAD looks for them on its support path; empty, as a drawing is opened
        /// or made, for none.
        /// </summary>
        /// <value>
        /// <para>
        /// Empty, no font is read and every entity is laid out as the library lays it out without fonts: nothing is read from
        /// the disk and nothing is logged. Given folders, a style's font file (<c>romans.shx</c>, <c>txt</c> read as
        /// <c>txt.shx</c>, <c>arial.ttf</c>) is read from the first that holds it, once, and the words of the drawing are measured in it where
        /// AutoCAD 2021 needs their width to place what it places by them, as it measures them: a TEXT, ATTRIB or ATTDEF
        /// justified other than left on its baseline stands where its words put it about its second point, and an aligned or
        /// fitted one takes the height or width its two points give; its <see cref="DxfEntity.GeometricExtents"/> are its
        /// words' ink; an MTEXT of one line gives the <see cref="DxfMText.ActualWidth"/> and
        /// <see cref="DxfMText.ActualHeight"/> of its words; a multileader lands a leader on the right of its words; an
        /// attribute definition is taken into a flip. "ABC" of txt.shx, 4 high and centred on (100, 50, 0), stands at
        /// (94.667, 50, 0), the ink of its letters 10.667 wide, and of arial.ttf at (94.259, 50, 0); with no fonts it stays
        /// where it stood.
        /// </para>
        /// <para>
        /// A TrueType font (.ttf, .ttc or .otf) is measured as AutoCAD 2021 measures it (checks F1a and F2): a text's height the
        /// top of the font's A, no pair of letters kerned, a TEXT boxed from the start of its baseline to its last pen, its
        /// bottom on the font's usWinDescent, and %%c the font's diameter sign, else its Ø. A style that names a typeface
        /// (<see cref="DxfTextStyleTableRecord.Font"/>) is measured where its file is that typeface, bold and italic as the
        /// style says, as AutoCAD draws the style by its typeface; a collection (.ttc) by its first face, where the style
        /// names it.
        /// </para>
        /// <para>
        /// A style's big font (<see cref="DxfTextStyleTableRecord.BigFontFileName"/>) is read from the same folders and its
        /// letters drawn beside the style's SHX font as AutoCAD 2021 draws them (check F3): a text's letters read in the code
        /// page AutoCAD gives the big font's name, GBK for gbcbig.shx, Big5 for chineset.shx, Shift-JIS for bigfont.shx,
        /// extfont.shx, extfont2.shx and @extfont2.shx, and Korean for whgtxt.shx, whgdtxt.shx, whtgtxt.shx and whtmtxt.shx,
        /// so that 中 is gbcbig.shx's D6D0 and chineset.shx's A4A4; a letter of two bytes the big font has drawn from it, but
        /// °, ± and ∅; a byte alone the font's letter, else the big font's; a space the big font's where it has one; and a
        /// letter neither font has the big font's question mark where it has one. "A中B" of txt.shx and gbcbig.shx, 4 high and
        /// centred on (100, 50, 0), stands at (95.211, 50, 0), as AutoCAD puts it. A big font of another name, or named with
        /// its folder, AutoCAD reads in no code page, and its texts are measured where every letter is of ASCII. On .NET, not
        /// .NET Framework, the code pages of big fonts are read where <c>CodePagesEncodingProvider</c> is registered, as for a
        /// DXF of them. A TrueType style's big font is not drawn.
        /// </para>
        /// <para>
        /// The results follow the fonts supplied, so that they differ between machines and between versions of a font, as
        /// AutoCAD's drawings do. Fonts of shapes are not measured, nor one beside a big font, a multibyte code (<c>\M+</c>)
        /// beside a big font, TrueType fonts of CFF outlines or of symbols, a style named by its typeface alone, one of another
        /// typeface than its file, and a collection a style names by no typeface: a style of one, or of a font in none of the
        /// folders, lays its words out as with no fonts, the log saying so at Debug once a file, and a file that is found but
        /// is no font, or cannot be read, is said at Warn; nothing throws. Each set reads the fonts anew. No folder is looked in that is not given: not the Fonts folder of
        /// Windows, and no font is put in for another, as AutoCAD's FONTALT puts one, or as Windows draws a letter a TrueType
        /// font has not from a font it links it to: a TEXT of such a letter is not measured.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        public string FontSearchPath
        {
            get => _fontSearchPath;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _fontSearchPath = value;
                _fonts = value.Trim().Length == 0 ? null : new DxfFonts(value);
            }
        }

        /// <summary>The fonts of <see cref="FontSearchPath"/>, read as they are first asked for; null where it gives no folder.</summary>
        internal DxfFonts Fonts => _fonts;
    }
}
