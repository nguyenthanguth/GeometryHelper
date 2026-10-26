using System;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMText
    {
        /// <summary>
        /// Gets the code that sets how the letters stand on the line, as AutoCAD's <c>AlignChange</c>: <c>\A</c>, followed by 0,
        /// 1 or 2 for the bottom, the middle and the top and a semicolon, <c>\A1;</c>.
        /// </summary>
        public static string AlignChange => @"\A";

        /// <summary>Gets the brace that opens a group of words whose formatting ends with it, as AutoCAD's <c>BlockBegin</c>: <c>{</c>, as in <c>{\H2x;Big} small</c>.</summary>
        public static string BlockBegin => "{";

        /// <summary>Gets the brace that closes a group of words opened by <see cref="BlockBegin"/>, as AutoCAD's <c>BlockEnd</c>: <c>}</c>.</summary>
        public static string BlockEnd => "}";

        /// <summary>
        /// Gets the code that sets the colour of the letters, as AutoCAD's <c>ColorChange</c>: <c>\C</c>, followed by an AutoCAD
        /// colour index and a semicolon, <c>\C1;</c> for red.
        /// </summary>
        public static string ColorChange => @"\C";

        /// <summary>
        /// Gets the code that sets the font, as AutoCAD's <c>FontChange</c>: <c>\F</c>, followed by the font's file and a
        /// semicolon, <c>\Fromans.shx;</c>; a TrueType font by its name is <c>\f</c>, <c>\fArial|b1|i0;</c>.
        /// </summary>
        public static string FontChange => @"\F";

        /// <summary>
        /// Gets the code that sets the height of the letters, as AutoCAD's <c>HeightChange</c>: <c>\H</c>, followed by a height
        /// and a semicolon, <c>\H3.5;</c>, or a factor of the height with an x, <c>\H1.5x;</c>.
        /// </summary>
        public static string HeightChange => @"\H";

        /// <summary>
        /// Gets the code that breaks the line, as AutoCAD's <c>LineBreak</c>: <c>\P</c>, the one break an MTEXT has, the same as
        /// <see cref="ParagraphBreak"/>.
        /// </summary>
        public static string LineBreak => @"\P";

        /// <summary>Gets the code of a space the line is not broken at, as AutoCAD's <c>NonBreakSpace</c>: <c>\~</c>.</summary>
        public static string NonBreakSpace => @"\~";

        /// <summary>
        /// Gets the code that slants the letters, as AutoCAD's <c>ObliqueChange</c>: <c>\Q</c>, followed by the angle in degrees
        /// and a semicolon, <c>\Q15;</c>.
        /// </summary>
        public static string ObliqueChange => @"\Q";

        /// <summary>Gets the code that ends a line over the letters, as AutoCAD's <c>OverlineOff</c>: <c>\o</c>.</summary>
        public static string OverlineOff => @"\o";

        /// <summary>Gets the code that begins a line over the letters, as AutoCAD's <c>OverlineOn</c>: <c>\O</c>.</summary>
        public static string OverlineOn => @"\O";

        /// <summary>Gets the code that ends a paragraph, as AutoCAD's <c>ParagraphBreak</c>: <c>\P</c>, <c>B-12\PM16</c> two lines.</summary>
        public static string ParagraphBreak => @"\P";

        /// <summary>
        /// Gets the code that stacks two parts, as AutoCAD's <c>StackStart</c>: <c>\S</c>, followed by the upper part, a <c>^</c>,
        /// <c>/</c> or <c>#</c>, the lower and a semicolon, <c>\S1/2;</c> for a half.
        /// </summary>
        public static string StackStart => @"\S";

        /// <summary>Gets the code that ends a line through the letters, as AutoCAD's <c>StrikethroughOff</c>: <c>\k</c>.</summary>
        public static string StrikethroughOff => @"\k";

        /// <summary>Gets the code that begins a line through the letters, as AutoCAD's <c>StrikethroughOn</c>: <c>\K</c>.</summary>
        public static string StrikethroughOn => @"\K";

        /// <summary>
        /// Gets the code that sets the room between the letters, as AutoCAD's <c>TrackChange</c>: <c>\T</c>, followed by a factor
        /// and a semicolon, <c>\T1.2;</c>.
        /// </summary>
        public static string TrackChange => @"\T";

        /// <summary>Gets the code that ends a line under the letters, as AutoCAD's <c>UnderlineOff</c>: <c>\l</c>.</summary>
        public static string UnderlineOff => @"\l";

        /// <summary>Gets the code that begins a line under the letters, as AutoCAD's <c>UnderlineOn</c>: <c>\L</c>.</summary>
        public static string UnderlineOn => @"\L";

        /// <summary>
        /// Gets the code that widens the letters, as AutoCAD's <c>WidthChange</c>: <c>\W</c>, followed by a factor and a
        /// semicolon, <c>\W0.8;</c>.
        /// </summary>
        public static string WidthChange => @"\W";

        /// <summary>
        /// Gets or sets the words of the MTEXT as RTF, as AutoCAD's <c>ContentsRTF</c>: its <see cref="Contents"/> written as a
        /// document of RTF and read back from one, as <see cref="SetContentsRtf"/> reads it.
        /// </summary>
        /// <value>
        /// <para>
        /// Get, a document <c>{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}...}</c> whose first font is that of the MTEXT's text style,
        /// its typeface, or its font's file where it has none (<c>txt</c> where the MTEXT has no style of the drawing), every
        /// font the words name after it in the order they name them, a font's file marked so by the ignored group
        /// <c>{\*\ghfontfile}</c> of its entry, then a colour table where they have colours, the words, and a paragraph's mark
        /// that closes them, as a word processor closes its last paragraph. The codes
        /// carried over: a paragraph <c>\P</c> as <c>\par</c>, <c>\X</c> as <c>\line</c>, a column <c>\N</c> as <c>\column</c>,
        /// the braces of a group as RTF's, <c>\~</c>, <c>\\</c>, <c>\{</c> and <c>\}</c> as themselves, the line under and through
        /// (<c>\L</c>, <c>\l</c>, <c>\K</c>, <c>\k</c>) as <c>\ul</c>, <c>\ulnone</c>, <c>\strike</c> and <c>\strike0</c>, a font
        /// <c>\F</c> or <c>\f</c> as <c>\f</c> of its entry, with <c>\b</c> and <c>\i</c> where a TrueType font gives them, a colour
        /// <c>\C</c> of AutoCAD's index as <c>\cf</c> of its red, green and blue, BYLAYER and BYBLOCK the automatic colour, a tab
        /// <c>^I</c> as <c>\tab</c>, a letter's escape <c>\U+00E9</c>, %%d, %%c and %%p as the letters, and a letter beyond ASCII as
        /// <c>\u</c>. A stack <c>\S1/2;</c> is the text 1/2, as <see cref="Text"/> reads it. Left out: the heights, widths, slants,
        /// tracking and alignments of the letters (<c>\H</c>, <c>\W</c>, <c>\Q</c>, <c>\T</c>, <c>\A</c>), the paragraph's settings
        /// (<c>\p</c>), the line over the letters (<c>\O</c>, <c>\o</c>), a true colour (<c>\c</c>) and every other code. The MTEXT
        /// <c>B-12\P{\C1;\LM16\l}</c> in the style Standard of the font txt is
        /// <c>{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}}{\colortbl ;\red255\green0\blue0;}B-12\par {\cf1 \ul M16\ulnone }\par}</c>.
        /// </para>
        /// <para>Set, as <see cref="SetContentsRtf"/>.</para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the RTF set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public string ContentsRTF
        {
            get
            {
                string font = StyleFont(out bool isFile);
                return DxfRtf.ToRtf(Contents, font, isFile);
            }
            set => SetContentsRtf(value);
        }

        /// <summary>
        /// Sets the words of the MTEXT from a document of RTF, as AutoCAD's <c>SetContentsRtf</c>: its words, with the codes of
        /// an MTEXT for what it sets that an MTEXT has, each written where the words take it on or off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read: <c>\par</c>, <c>\line</c>, <c>\sect</c> and <c>\page</c> as a paragraph <c>\P</c>, the last of the document left
        /// out; <c>\column</c> as <c>\N</c>; <c>\tab</c> as <c>^I</c>; <c>\~</c> as <c>\~</c>; <c>\ul</c> and its kinds and
        /// <c>\strike</c> as <c>\L</c> and <c>\K</c>, <c>\ulnone</c>, <c>\ul0</c> and <c>\strike0</c> as <c>\l</c> and <c>\k</c>; a
        /// font <c>\f</c> of the font table, with <c>\b</c> and <c>\i</c>, as <c>\fArial|b1|i0;</c>, or <c>\F</c> of its file for a
        /// font whose name ends in .shx or whose entry is marked <c>{\*\ghfontfile}</c>, as <see cref="ContentsRTF"/> marks one, and
        /// for the style's font where the style has a font's file, with no bold or italic, which a font's file has not; a colour <c>\cf</c> as
        /// <c>\C</c> of AutoCAD's index nearest it, the automatic colour as <c>\C256;</c>; <c>\plain</c> taking them all back;
        /// <c>\u</c>, <c>\'e9</c> (Windows-1252), <c>\emdash</c>, the quotes and the other letters RTF names as the letters.
        /// The style's font, plain, is written by no code, and a code only before a letter it changes, so that the RTF
        /// <see cref="ContentsRTF"/> gives comes back as the same words in the same look, its groups made codes on and off:
        /// <c>B-12\P{\C1;\LM16\l}</c> comes back as <c>B-12\P\C1;\LM16</c>, <c>abc\P</c> as <c>abc\P</c> and <c>\Fromans|c0;abc</c>
        /// as <c>\Fromans;abc</c>. Every other word of RTF is left out, the size of the
        /// letters (<c>\fs</c>) and the paragraph's settings among them, with the groups whose words are not the document's:
        /// the style sheet, the information, pictures, headers and footers, and every group marked <c>\*</c>.
        /// </para>
        /// </remarks>
        /// <param name="value">The RTF.</param>
        /// <returns>How many characters the words written have, codes among them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the RTF is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MTEXT is erased.</exception>
        public int SetContentsRtf(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            string font = StyleFont(out bool isFile);
            string words = DxfRtf.FromRtf(value, font, isFile);
            Contents = words;
            return words.Length;
        }

        /// <summary>The font of the MTEXT's text style: its typeface, else its font's file, which it says it is; txt where the MTEXT has no style of the drawing.</summary>
        private string StyleFont(out bool isFile)
        {
            DxfTextStyleTableRecord style = TextStyle;
            string face = style?.Font.TypeFace;

            if (!string.IsNullOrEmpty(face))
            {
                isFile = false;
                return face;
            }

            isFile = true;
            string file = style?.FileName?.Trim();
            return string.IsNullOrEmpty(file) ? "txt" : file;
        }
    }
}
