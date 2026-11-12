using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words of a dimension's picture measured as AutoCAD 2021 measures them to lay the picture out: as wide as their
    /// MTEXT's <c>ActualWidth</c> and as high as its <c>ActualHeight</c>, laid out as the canvas lays out an MTEXT
    /// (<see cref="CadMTextLayout"/>) in the font of the dimension's text style, its letters measured by their ink
    /// (<see cref="CadTextInk"/>). "R20" of arial.ttf 2.5 high is 5.965 wide and 2.553 high, as AutoCAD's own picture has it,
    /// its digits reaching past the top of the A a text's height stands for.
    /// </summary>
    /// <remarks>
    /// The style's font is found as the canvas finds it to draw the words: an SHX font, and its big font, where the fonts last
    /// given a document (<see cref="CadDocument.Fonts"/>) find it, the folder of that document's drawing first, the canvas's
    /// own ghsimplex wherever fonts are on; a TrueType font from the Fonts folders of Windows. The style's width factor and
    /// oblique angle are measured too. Words of a face whose letters are not read (an SHX font with the fonts off or not
    /// found, a TrueType font with no file), or that the layout gives no ink at all, as words of spaces alone, are not
    /// measured: the picture then takes a letter as 0.7 of the height wide, as before. Words whose ink has no width are
    /// measured nought wide, as AutoCAD measures them: "I" of isocp.shx, a single upright stroke. The measurements are kept
    /// by font, words and height, at most 4096 at once.
    /// </remarks>
    internal static class CadDimensionWords
    {
        private const int MostKept = 4096;

        private static readonly object Gate = new object();
        private static readonly CadFonts Off = new CadFonts(null, null, null);
        private static readonly Dictionary<string, KeyValuePair<double, double>> Kept = new Dictionary<
            string,
            KeyValuePair<double, double>
        >(StringComparer.Ordinal);
        private static CadFonts _fonts;
        private static string _fontFolder;

        /// <summary>
        /// Takes the fonts a document's text is drawn in as those an SHX font of a dimension is measured in, and the folder of its
        /// drawing as the first looked in; the measurements kept are let go.
        /// </summary>
        internal static void UseFonts(CadFonts fonts, string drawingFolder)
        {
            lock (Gate)
            {
                _fonts = fonts;
                _fontFolder = drawingFolder;
                Kept.Clear();
            }
        }

        /// <summary>
        /// Measures words of a text style as AutoCAD lays out a dimension's MTEXT of them: their width and their height; false,
        /// and nothing measured, where the style is null or the layout gives the words no ink. Words inked in a line up and
        /// down alone are measured nought wide.
        /// </summary>
        internal static bool TryMeasure(
            DxfTextStyleTableRecord style,
            string words,
            double height,
            out double width,
            out double tall
        )
        {
            width = 0.0;
            tall = 0.0;

            if (style == null || string.IsNullOrWhiteSpace(words) || !(height > 0.0))
            {
                return false;
            }

            string file;
            string bigFile;
            DxfFontDescriptor font;
            double xscale;
            double oblique;

            try
            {
                file = (style.FileName ?? string.Empty).Trim();
                bigFile = (style.BigFontFileName ?? string.Empty).Trim();
                font = style.Font;
                xscale = style.XScale;
                oblique = style.ObliquingAngle;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                return false;
            }

            string key =
                file
                + "|"
                + bigFile
                + "|"
                + font.TypeFace
                + "|"
                + xscale.ToString("R", CultureInfo.InvariantCulture)
                + "|"
                + oblique.ToString("R", CultureInfo.InvariantCulture)
                + "|"
                + height.ToString("R", CultureInfo.InvariantCulture)
                + "|"
                + words;

            lock (Gate)
            {
                if (Kept.TryGetValue(key, out KeyValuePair<double, double> known))
                {
                    width = known.Key;
                    tall = known.Value;
                    return tall > 0.0;
                }

                Measure(file, bigFile, font.TypeFace, xscale, oblique, words, height, out width, out tall);

                if (Kept.Count >= MostKept)
                {
                    Kept.Clear();
                }

                Kept[key] = new KeyValuePair<double, double>(width, tall);
                return tall > 0.0;
            }
        }

        private static void Measure(
            string file,
            string bigFile,
            string typeface,
            double xscale,
            double oblique,
            string words,
            double height,
            out double width,
            out double tall
        )
        {
            width = 0.0;
            tall = 0.0;
            CadFonts fonts = _fonts ?? Off;
            string folder = _fontFolder;
            double factor = xscale > 0.0 && !double.IsInfinity(xscale) ? xscale : 1.0;

            try
            {
                CadFontFace face = fonts.FaceOf(file, bigFile, typeface, folder);

                // A face whose letters are not read (an SHX font not found, though another is drawn for it, a TrueType font with no
                // file) is left to the estimate.
                string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                bool trueType =
                    extension == ".ttf"
                    || extension == ".ttc"
                    || extension == ".otf"
                    || (file.Length == 0 && typeface.Length > 0);

                if (
                    (
                        !trueType
                        && (face.Shx == null || (!CadFonts.IsBuiltIn(file) && fonts.Locate(file, folder) == null))
                    ) || !CadTextInk.Of(face, false, false).IsRead
                )
                {
                    return;
                }

                var text = new DxfMText
                {
                    Location = new GeoPoint3(0.0, 0.0, 0.0),
                    TextHeight = height,
                    Attachment = DxfAttachmentPoint.MiddleCenter,
                    Contents = words,
                };
                CadMTextContent content = CadMTextContent.Parse(words, height, factor);
                CadMTextLayout layout = CadMTextLayout.Of(
                    text,
                    content,
                    look =>
                        look.Font == null ? face
                        : look.IsShx ? fonts.FaceOf(look.Font, null, null, folder)
                        : fonts.FaceOf(null, null, look.Font, folder),
                    factor,
                    oblique
                );
                double measured = layout.ActualWidth;
                double high = layout.ActualHeight;

                if (measured >= 0.0 && high > 0.0 && !double.IsInfinity(measured) && !double.IsInfinity(high))
                {
                    width = measured;
                    tall = high;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                GeometryHelperLog.Debug(
                    "CadDimensionWords: the words "
                        + words
                        + " of "
                        + (file.Length > 0 ? file : typeface)
                        + " were not measured ("
                        + exception.Message
                        + ")."
                );
            }
        }
    }
}
